using System.Collections.Generic;
using UnityEngine;

namespace Game.Client
{
    // How the UI moves and sounds (Docs/UiRework.md 4-6 손맛, U1): windows open by growing from 0.92 to full with a
    // little overshoot (easeOutBack) and fading in over 0.15 seconds, and close in 0.08 seconds without one; a button
    // lifts a point under the pointer, sinks a point and shrinks to 0.95 while pressed, and shakes with a low sound when
    // pressed while it cannot be used; a count grows a moment and turns gold (up) or red (down) when it changes; and the
    // six UI sounds. Timed by Time.unscaledTime in the client alone; nothing here touches the game's state.
    // "움직임: 줄이기" in the menu (Settings.ReduceMotion) keeps only the fades; the playtests (Ui.Probing) get none, so
    // a picture never catches a window half open and a probed rect is where the button is.
    static class UiMotion
    {
        const float OpenSeconds = 0.15f;
        const float CloseSeconds = 0.08f;
        const float PopSeconds = 0.2f;
        const float ShakeSeconds = 0.25f;

        public static bool Instant => Ui.Probing;
        public static bool Reduced => Instant || Settings.ReduceMotion;

        static float Now => Time.unscaledTime;

        // ---- Windows ----

        sealed class Window
        {
            public bool Open;
            public int Seen;
            public float Opened;
            public Rect Area;
            public UiKind Kind;
            public Color? Accent;
        }

        static readonly Dictionary<string, Window> Windows = new Dictionary<string, Window>();
        static readonly List<(Rect Area, UiKind Kind, Color? Accent, float Closed)> Ghosts = new List<(Rect, UiKind, Color?, float)>();
        static readonly Stack<(Matrix4x4 Matrix, Color Color)> Saved = new Stack<(Matrix4x4, Color)>();

        // Overshoots its end a little and settles (easings.net easeOutBack).
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1;
            float u = t - 1;
            return 1 + c3 * u * u * u + c1 * u * u;
        }

        // How far the window has opened, 0 to 1 (1 once open, or at once in the playtests).
        public static float Opening(string key) =>
            Instant || !Windows.TryGetValue(key, out var window) ? 1 : Mathf.Clamp01((Now - window.Opened) / OpenSeconds);

        // Call before drawing a window that is open, every pass, and EndWindow after it (at the top level, outside any
        // group). Notices the window opening (its sound and the start of the tween) and draws it grown and faded.
        public static void BeginWindow(string key, Rect area, UiKind kind, Color? accent = null)
        {
            if (!Windows.TryGetValue(key, out var window)) Windows[key] = window = new Window();
            if (Event.current.type == EventType.Repaint)
            {
                if (!window.Open)
                {
                    window.Open = true;
                    window.Opened = Now;
                    Sounds.Play(Sound.UiOpen, 0.7f);
                }
                window.Seen = Time.frameCount;
                window.Area = area;
                window.Kind = kind;
                window.Accent = accent;
            }
            Saved.Push((GUI.matrix, GUI.color));
            float t = Opening(key);
            if (t >= 1) return;
            float fade = Mathf.Clamp01(t / 0.6f);
            GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, GUI.color.a * fade);
            if (Reduced) return;
            float scale = 0.92f + 0.08f * EaseOutBack(t);
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), area.center);
        }

        // At the start of each pass: a window left unfinished by the last one (IMGUI ends a pass early with ExitGUI, as
        // a menu page does when it changes) gives back the colour it took.
        public static void StartPass()
        {
            if (Saved.Count == 0) return;
            (Matrix4x4, Color) first = default;
            while (Saved.Count > 0) first = Saved.Pop();
            GUI.color = first.Item2;
        }

        public static void EndWindow()
        {
            if (Saved.Count == 0) return;
            var (matrix, color) = Saved.Pop();
            GUI.matrix = matrix;
            GUI.color = color;
        }

        // At the end of each pass: a window not drawn in this repaint has closed. It leaves its frame shrinking and fading
        // for a moment, with the close sound.
        public static void EndPass()
        {
            if (Event.current.type != EventType.Repaint) return;
            foreach (var window in Windows.Values)
            {
                if (!window.Open || window.Seen >= Time.frameCount) continue;
                window.Open = false;
                Sounds.Play(Sound.UiClose, 0.6f);
                if (!Instant) Ghosts.Add((window.Area, window.Kind, window.Accent, Now));
            }
        }

        // The closing windows' frames, under whatever is drawn after.
        public static void DrawGhosts()
        {
            for (int i = Ghosts.Count - 1; i >= 0; i--)
            {
                var (area, kind, accent, closed) = Ghosts[i];
                float t = (Now - closed) / CloseSeconds;
                if (t >= 1 || Instant)
                {
                    Ghosts.RemoveAt(i);
                    continue;
                }
                var matrix = GUI.matrix;
                var color = GUI.color;
                GUI.color = new Color(color.r, color.g, color.b, color.a * (1 - t));
                if (!Reduced)
                {
                    float scale = 1 - 0.06f * t;
                    GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), area.center);
                }
                UiSkin.Panel(area);
                UiSkin.HeadBand(UiSkin.HeadOf(area), kind, accent);
                GUI.matrix = matrix;
                GUI.color = color;
            }
        }

        // ---- Buttons ----

        // Where a button's face is drawn: a point up under the pointer; a point down and 0.95 of its size while pressed.
        // The button itself (what a press hits) does not move.
        public static Rect Face(Rect rect, bool hover, bool down)
        {
            if (Reduced) return rect;
            if (down)
            {
                float dx = rect.width * 0.025f, dy = rect.height * 0.025f;
                return new Rect(rect.x + dx, rect.y + dy + 1, rect.width - dx * 2, rect.height - dy * 2);
            }
            return hover ? new Rect(rect.x, rect.y - 1, rect.width, rect.height) : rect;
        }

        static readonly Dictionary<(string, Vector2), float> Refused = new Dictionary<(string, Vector2), float>();

        // A button that cannot be used now was pressed: it shakes and sounds low (Docs/UiRework.md 4-6 실패), rather than
        // only staying dim. Known by its text and place, so only that one shakes.
        public static void Refuse(string text, Rect rect)
        {
            Refused[(text ?? "", rect.position)] = Now;
            Sounds.Play(Sound.UiFail, 0.7f);
        }

        // How far the refused button is pushed sideways now: two points either way, dying out.
        public static float Shake(string text, Rect rect)
        {
            if (Refused.Count == 0 || !Refused.TryGetValue((text ?? "", rect.position), out float at)) return 0;
            float t = (Now - at) / ShakeSeconds;
            if (t >= 1)
            {
                Refused.Remove((text ?? "", rect.position));
                return 0;
            }
            return Reduced ? 0 : Mathf.Sin(t * Mathf.PI * 6) * 2 * (1 - t);
        }

        static string _hovered, _hovering;

        // The pointer is over a button in this repaint: the hover sound once as it arrives. Not for fingers.
        public static void Hover(string key)
        {
            if (Controls.UsingTouch) return;
            _hovering = key;
            if (key != _hovered) Sounds.Play(Sound.UiHover, 0.35f);
            _hovered = key;
        }

        // At the start of each repaint: what was hovered last time, so leaving and coming back sounds again.
        public static void StartRepaint()
        {
            if (_hovering == null) _hovered = null;
            _hovering = null;
        }

        public static void Confirm() => Sounds.Play(Sound.UiConfirm, 0.6f);
        public static void Cancel() => Sounds.Play(Sound.UiCancel, 0.6f);

        // ---- Counts ----

        static readonly Dictionary<string, (int Value, float Changed, bool Up)> Counts = new Dictionary<string, (int, float, bool)>();

        // How big and what tint (its alpha how much) the count shows at now: it grows by a quarter and fades back over
        // 0.2 seconds after it changes, gold when it went up and red when it went down. Not the first time it is seen.
        public static (float Scale, Color Tint) Pop(string key, int value)
        {
            if (!Counts.TryGetValue(key, out var count))
            {
                Counts[key] = (value, -10, true);
                return (1, Color.clear);
            }
            if (count.Value != value) Counts[key] = count = (value, Now, value > count.Value);
            float t = (Now - count.Changed) / PopSeconds;
            if (t >= 1 || Instant) return (1, Color.clear);
            var tint = count.Up ? new Color(1f, 0.82f, 0.3f, 1 - t) : new Color(1f, 0.35f, 0.3f, 1 - t);
            return (Reduced ? 1 : 1 + 0.25f * Mathf.Sin(t * Mathf.PI), tint);
        }

        // ---- Being hurt (the red edges behind a full screen) ----

        static float _health = -1, _hurtAt = -10;

        // Called with the player's health every frame; a drop starts the red.
        public static void NoteHealth(float health)
        {
            if (_health >= 0 && health < _health - 0.01f) _hurtAt = Now;
            _health = health;
        }

        // How strong the red is now: full when hurt, gone in 0.8 seconds.
        public static float HurtGlow => Mathf.Clamp01(1 - (Now - _hurtAt) / 0.8f) * 0.85f;

        // ---- Low health (Docs/UiRework.md U7) ----

        // The player's share of health (GameRoot sets it each frame; 0 while down).
        public static float HealthShare { get; set; } = 1;

        // How strong the red at the screen's edges is while health is low (at or under `low`, not while down): deeper the
        // lower it is, pulsing slowly (about a beat a second); steady when motion is reduced and in the playtests.
        public static float LowHealthGlow(float low)
        {
            float share = HealthShare;
            if (share <= 0 || share > low) return 0;
            float strength = 0.3f + 0.35f * (1 - share / low);
            if (Reduced) return strength;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Now * Mathf.PI * 2 * 0.9f);
            return strength * (0.6f + 0.4f * pulse);
        }

        // The heart by the health bar beating while health is low: its brightness now (1 when still).
        public static float LowHealthBeat() => Reduced ? 1 : 0.7f + 0.3f * (0.5f + 0.5f * Mathf.Sin(Now * Mathf.PI * 2 * 1.4f));
    }
}
