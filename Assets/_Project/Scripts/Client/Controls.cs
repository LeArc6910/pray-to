using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client
{
    // Keyboard and mouse: WASD or arrow keys to move, the mouse to aim, left button or space to attack (held to keep
    // swinging), M for the map. Touch: two sticks that appear where a finger lands. The left half moves; the right
    // half attacks for as long as it is held and aims where it is pulled. Pressed without pulling, the attack goes
    // where the player walks, or where they already face when standing still. Touches that start on a UI button or
    // window, which OnGUI marks with Block, are left to the UI.
    static class Controls
    {
        // A touch stick: where the finger landed and where it is now, in UI points.
        sealed class Stick
        {
            public int Finger = -1;
            public Vector2 Origin;
            public Vector2 Now;

            public bool Held => Finger >= 0;

            // Pull as a share of the full radius, at most 1. GUI points grow downwards; the world grows upwards.
            public Vec2 Direction
            {
                get
                {
                    Vector2 pull = Vector2.ClampMagnitude((Now - Origin) / StickRadius, 1);
                    return pull.magnitude > DeadZone ? new Vec2(pull.x, -pull.y) : Vec2.Zero;
                }
            }

            public void Draw(Color color)
            {
                if (!Held) return;
                Vector2 pull = Vector2.ClampMagnitude(Now - Origin, StickRadius);
                Disc(Shapes.Ring, Origin, StickRadius, new Color(color.r, color.g, color.b, 0.3f));
                Disc(Shapes.Circle, Origin + pull, StickRadius * 0.45f, new Color(color.r, color.g, color.b, 0.45f));
            }
        }

        // How far the stick can be pulled, in UI points, and the share of that which still counts as standing still.
        const float StickRadius = 60;
        const float DeadZone = 0.15f;
        // Browsers also report taps as mouse clicks, so the mouse is ignored for a moment after any touch.
        const float MouseQuietSeconds = 1;

        static readonly List<(int Id, Vector2 Pixels)> _touches = new List<(int, Vector2)>();
        static readonly HashSet<int> _ignored = new HashSet<int>();
        static readonly List<Rect> _blocked = new List<Rect>();
        static readonly Predicate<int> Released = id => !Held(id);
        static readonly Stick _move = new Stick();
        static readonly Stick _aim = new Stick();
        static readonly Color AimColor = new Color(1f, 0.6f, 0.55f);
        static float _lastTouch = float.NegativeInfinity;

        // A touch has been seen this run: the HUD shows touch buttons and hints.
        public static bool UsingTouch { get; private set; }

        // Once per frame, before Read: follows the fingers. Then drops last frame's UI areas; OnGUI marks them again.
        public static void UpdateTouches()
        {
            _touches.Clear();
            var screen = Touchscreen.current;
            if (screen != null)
            {
                foreach (var touch in screen.touches)
                {
                    if (touch.press.isPressed) _touches.Add((touch.touchId.ReadValue(), touch.position.ReadValue()));
                }
            }
            if (_touches.Count > 0)
            {
                _lastTouch = Time.unscaledTime;
                UsingTouch = true;
            }

            if (!Held(_move.Finger)) _move.Finger = -1;
            if (!Held(_aim.Finger)) _aim.Finger = -1;
            _ignored.RemoveWhere(Released);
            foreach (var (id, pixels) in _touches)
            {
                Vector2 point = Ui.FromScreen(pixels);
                if (id == _move.Finger) _move.Now = point;
                else if (id == _aim.Finger) _aim.Now = point;
                else if (!_ignored.Contains(id)) Begin(id, point);
            }
            _blocked.Clear();
        }

        // Marks a UI area, in points, where new touches belong to the UI. Call from OnGUI.
        public static void Block(Rect area)
        {
            if (Event.current.type == EventType.Repaint) _blocked.Add(area);
        }

        public static (Vec2 Move, Vec2 Aim, bool Attack) Read(Camera camera, Vec2 player)
        {
            var keyboard = Keyboard.current;
            var mouse = Time.unscaledTime - _lastTouch < MouseQuietSeconds ? null : Mouse.current;

            float x = 0;
            float y = 0;
            if (keyboard != null)
            {
                if (keyboard[Key.A].isPressed || keyboard[Key.LeftArrow].isPressed) x -= 1;
                if (keyboard[Key.D].isPressed || keyboard[Key.RightArrow].isPressed) x += 1;
                if (keyboard[Key.S].isPressed || keyboard[Key.DownArrow].isPressed) y -= 1;
                if (keyboard[Key.W].isPressed || keyboard[Key.UpArrow].isPressed) y += 1;
            }
            var move = new Vec2(x, y).Normalized;
            // A half-pulled stick walks at half speed.
            if (_move.Held && _move.Direction.LengthSquared > 0) move = _move.Direction;

            var aim = Vec2.Zero;
            bool attack = keyboard != null && keyboard[Key.Space].isPressed;
            if (_aim.Held)
            {
                aim = _aim.Direction;
                attack = true;
            }
            else if (mouse != null)
            {
                aim = Toward(camera, mouse.position.ReadValue(), player);
                attack |= mouse.leftButton.isPressed;
            }
            // Keyboard-only players, and an attack stick pressed without pulling, face where they walk. Standing still,
            // a zero aim leaves the facing as it was.
            if (aim.LengthSquared < 0.01f) aim = move;
            return (move, aim, attack);
        }

        public static bool LeavePressed() => Keyboard.current != null && Keyboard.current[Key.Escape].wasPressedThisFrame;

        public static bool BagPressed() =>
            Keyboard.current != null && (Keyboard.current[Key.Tab].wasPressedThisFrame || Keyboard.current[Key.I].wasPressedThisFrame);

        public static bool MapPressed() => Keyboard.current != null && Keyboard.current[Key.M].wasPressedThisFrame;

        // Both sticks over the game. Call from OnGUI.
        public static void DrawTouch()
        {
            _move.Draw(Color.white);
            _aim.Draw(AimColor);
        }

        // A new finger, in UI points: the move stick on the left half, the attack stick on the right, unless that
        // stick is taken or the UI owns the spot.
        static void Begin(int id, Vector2 point)
        {
            var stick = point.x < Ui.Width / 2 ? _move : _aim;
            if (Blocked(point) || stick.Held)
            {
                _ignored.Add(id);
                return;
            }
            stick.Finger = id;
            stick.Origin = stick.Now = point;
        }

        static Vec2 Toward(Camera camera, Vector2 screen, Vec2 player)
        {
            if (camera == null) return Vec2.Zero;
            Vector3 world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
            return new Vec2(world.x - player.X, world.y - player.Y);
        }

        static void Disc(Sprite sprite, Vector2 center, float radius, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2), sprite.texture);
            GUI.color = previous;
        }

        static bool Held(int id)
        {
            foreach (var touch in _touches)
            {
                if (touch.Id == id) return true;
            }
            return false;
        }

        static bool Blocked(Vector2 point)
        {
            foreach (var area in _blocked)
            {
                if (area.Contains(point)) return true;
            }
            return false;
        }
    }
}
