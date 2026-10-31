using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Game.Client
{
    // Shared setup for the IMGUI screens. Layout is done in points, not pixels: a browser draws the game at the
    // device's full pixel density (about 3 pixels per point on phones), which would leave 13-pixel text unreadably
    // small, so the GUI matrix scales points up by that ratio. Elsewhere one point is one pixel.
    // The font travels with the game because web builds cannot use the system's fonts (no Korean otherwise). The
    // pixel-art frames come from UiSkin.
    static class Ui
    {
        const string FontPath = "Fonts/NotoSansKR-Game";

        static Font _font;

        // The status box in the top left corner during play. Other screens keep clear of it. On a narrow (portrait)
        // screen it gives way to the minimap column, and on a wide but short one (a phone held sideways) it takes
        // less of the width. It is as tall as its lines needed when it was last drawn.
        public static Rect HudArea => new Rect(10, 10, Mathf.Min(340, MinimapArea.x - 20, Mathf.Max(220, Width * 0.42f)), HudHeight);

        // A phone held sideways: too short for the HUD's how-to-play lines.
        public static bool Short => Height < 480;

        public static float HudHeight { get; set; } = 160;

        public const float MinimapSize = 120;
        public const float TouchButtonHeight = 40;
        const float ColumnGap = 10;

        // The top right column: the minimap, and on touch screens the bag and leave buttons under it.
        public static Rect MinimapArea => new Rect(Width - MinimapSize - 10, 10, MinimapSize, MinimapSize);

        public static Rect TouchButtonArea(int index, int count)
        {
            float width = (MinimapSize - ColumnGap * (count - 1)) / count;
            var minimap = MinimapArea;
            return new Rect(minimap.x + index * (width + ColumnGap), minimap.yMax + ColumnGap, width, TouchButtonHeight);
        }

        public static float RightColumnBottom => MinimapArea.yMax + (Controls.UsingTouch ? ColumnGap + TouchButtonHeight : 0);

        public static float Scale { get; private set; } = 1;

        // Screen size in points.
        public static float Width => Screen.width / Scale;
        public static float Height => Screen.height / Scale;

        // Call at the start of every OnGUI.
        public static void Begin()
        {
            if (_font == null) _font = Resources.Load<Font>(FontPath);
            GUI.skin = UiSkin.Get();
            if (_font != null) GUI.skin.font = _font;
            Scale = PixelRatio();
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1));
        }

        static readonly Dictionary<(string, int, int, FontStyle), string> Wrapped = new Dictionary<(string, int, int, FontStyle), string>();
        static readonly GUIContent Measured = new GUIContent();

        // The text with line breaks between words so each line fits the width in the style. Unity breaks Korean
        // anywhere, even inside a word ("클 / 릭"); Korean is written with spaces between words and should break only
        // there. A word wider than a line is left for the style to break. Kept per text and width, since labels are
        // drawn every frame.
        public static string Wrap(string text, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(text) || width <= 0) return text;
            var key = (text, Mathf.RoundToInt(width), style.fontSize, style.fontStyle);
            if (Wrapped.TryGetValue(key, out string wrapped)) return wrapped;
            if (Wrapped.Count > 2000) Wrapped.Clear();

            var result = new StringBuilder(text.Length + 8);
            var line = new StringBuilder();
            string[] paragraphs = text.Split('\n');
            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0) result.Append('\n');
                line.Clear();
                foreach (string word in paragraphs[p].Split(' '))
                {
                    // A little short of the width, or Unity's own wrapping may still split the line's last word.
                    if (line.Length > 0 && TextWidth(style, line + " " + word) > width - 4)
                    {
                        result.Append(line).Append('\n');
                        line.Clear();
                    }
                    else if (line.Length > 0)
                    {
                        line.Append(' ');
                    }
                    line.Append(word);
                }
                result.Append(line);
            }
            wrapped = result.ToString();
            Wrapped[key] = wrapped;
            return wrapped;
        }

        static float TextWidth(GUIStyle style, string text)
        {
            Measured.text = text;
            return style.CalcSize(Measured).x;
        }

        // Screen pixels as cameras and touches give them (origin bottom left) to GUI points (origin top left).
        public static Vector2 FromScreen(Vector2 pixels) => new Vector2(pixels.x / Scale, (Screen.height - pixels.y) / Scale);

        static float PixelRatio()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Mathf.Clamp(GetDevicePixelRatio(), 1, 4);
#else
            return 1;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // Plugins/WebGL/Display.jslib
        [DllImport("__Internal")]
        static extern float GetDevicePixelRatio();
#endif
    }
}
