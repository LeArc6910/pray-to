using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client
{
    // A small panel of numbers for checking the game on a device, turned on in the menu (Settings.DevInfo): frames a
    // second, how long a frame and the dark (WorldView) take, the screen, and the last inputs. Inputs are logged twice,
    // as the Input System saw them in Update (what the game acts on) and as IMGUI's events saw them in OnGUI (what the
    // browser delivered), so a press the game missed shows up as a line from one and not the other. Each line has the
    // frame it came in.
    static class DevInfo
    {
        const int LogLines = 10;
        // Smoothing for the frame numbers: about the last second.
        const float Smoothing = 0.05f;

        static readonly Key[] Watched = { Key.Tab, Key.I, Key.M, Key.Escape, Key.Q, Key.E, Key.R, Key.F, Key.C, Key.Space };
        static readonly KeyCode[] WatchedCodes =
        {
            KeyCode.Tab, KeyCode.I, KeyCode.M, KeyCode.Escape, KeyCode.Q, KeyCode.E, KeyCode.R, KeyCode.F, KeyCode.C, KeyCode.Space,
        };
        static readonly List<string> Log = new List<string>();
        static float _frameMs = 16;
        static float _darkMs;
        static GUIStyle _style;

        // Once per Update, with the frame's time: the frame numbers, and the watched keys' presses and releases.
        public static void Frame(float deltaTime)
        {
            if (!Settings.DevInfo) return;
            _frameMs += (deltaTime * 1000 - _frameMs) * Smoothing;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            foreach (var key in Watched)
            {
                var control = keyboard[key];
                if (control.wasPressedThisFrame) Add($"입력 시스템 ↓{key}");
                if (control.wasReleasedThisFrame) Add($"입력 시스템 ↑{key}");
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) Add("입력 시스템 ↓마우스");
        }

        // From OnGUI, for every event: the watched keys and mouse presses as IMGUI got them. Layout and repaint
        // events are skipped.
        public static void Read(Event e)
        {
            if (!Settings.DevInfo) return;
            if (e.type == EventType.KeyDown && System.Array.IndexOf(WatchedCodes, e.keyCode) >= 0) Add($"IMGUI ↓{e.keyCode}");
            else if (e.type == EventType.KeyUp && System.Array.IndexOf(WatchedCodes, e.keyCode) >= 0) Add($"IMGUI ↑{e.keyCode}");
            else if (e.type == EventType.MouseDown) Add($"IMGUI ↓마우스{e.button}");
        }

        // How long the dark took this frame, in milliseconds.
        public static void DarkTime(float milliseconds)
        {
            if (Settings.DevInfo) _darkMs += (milliseconds - _darkMs) * Smoothing;
        }

        // Something the game did with an input, such as opening the bag, for reading next to the inputs.
        public static void Note(string text)
        {
            if (Settings.DevInfo) Add("→ " + text);
        }

        static void Add(string text)
        {
            Log.Add($"{Time.frameCount} {Time.unscaledTime:0.00}s {text}");
            if (Log.Count > LogLines) Log.RemoveAt(0);
        }

        // In the bottom left corner, under everything else.
        public static void Draw()
        {
            if (!Settings.DevInfo) return;
            _style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = false };
            Ui.Text(_style, 11);
            float fps = _frameMs > 0 ? 1000 / _frameMs : 0;
            string text = $"{fps:0} fps · 프레임 {_frameMs:0.0}ms · 어둠 {_darkMs:0.0}ms\n"
                + $"화면 {Screen.width}×{Screen.height}px · {Ui.Width:0}×{Ui.Height:0}점 · 배율 {Ui.Scale:0.##}\n"
                + string.Join("\n", Log);
            var content = new GUIContent(text);
            var size = _style.CalcSize(content);
            var area = new Rect(10, Ui.Height - size.y - 10, Mathf.Min(size.x, Ui.Width - 20), size.y);
            var previous = GUI.color;
            GUI.color = new Color(1, 1, 1, 0.85f);
            GUI.Box(area, content, _style);
            GUI.color = previous;
        }
    }
}
