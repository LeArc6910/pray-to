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
    // The font travels with the game because web builds cannot use the system's fonts (no Korean otherwise): the
    // pixel font Galmuri11 Bold, or the smooth Noto Sans KR when the menu says so (Settings.PixelFont). The pixel font
    // is drawn at its 12-pixel design size or a whole multiple, unsmoothed and sampled to the nearest pixel, so its
    // pixels stay square however far the GUI matrix scales it (Text, Docs/WebDemo.md). The pixel-art frames come
    // from UiSkin.
    static class Ui
    {
        const string SmoothFontPath = "Fonts/NotoSansKR-Game";
        const string PixelFontPath = "Fonts/Galmuri11Bold-Game";
        // Galmuri11's design size in pixels (the editor imports it at this size, ProjectSetup).
        public const int PixelFontSize = 12;

        static Font _font;
        // The menu's choice the font was loaded for, so a change loads the other one.
        static bool? _fontChoice;

        // Whether the text is in the pixel font. Not when the menu asks for the smooth one, or before the pixel font
        // is in the project.
        public static bool PixelFont { get; private set; }

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

        // Screen size in points: a test's stand-in size when there is one (SizeOverride), so the play mode tests can
        // check the layout for a phone's screen on any machine.
        public static float Width => SizeOverride?.x ?? Screen.width / Scale;
        public static float Height => SizeOverride?.y ?? Screen.height / Scale;

        internal static Vector2? SizeOverride { get; set; }

        // Call at the start of every OnGUI.
        public static void Begin()
        {
            if (_font == null || _fontChoice != Settings.PixelFont) LoadFont(Settings.PixelFont);
            GUI.skin = UiSkin.Get();
            if (_font != null) GUI.skin.font = _font;
            Scale = PixelRatio();
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1));
            StartProbe();
        }

        static void LoadFont(bool pixel)
        {
            _fontChoice = pixel;
            var font = pixel ? Resources.Load<Font>(PixelFontPath) : null;
            PixelFont = font != null;
            _font = font != null ? font : Resources.Load<Font>(SmoothFontPath);
            // Lines broken for the other font no longer fit.
            Wrapped.Clear();
            if (!PixelFont) return;
            if (!_watchingFontTextures)
            {
                Font.textureRebuilt += Sharpen;
                _watchingFontTextures = true;
            }
            Sharpen(_font);
        }

        static bool _watchingFontTextures;

        // A dynamic font draws its letters into a texture it rebuilds as it meets new ones; the pixel font's is
        // sampled to the nearest pixel, or scaling would blur it.
        static void Sharpen(Font font)
        {
            if (font != _font || !PixelFont || font.material == null || font.material.mainTexture == null) return;
            font.material.mainTexture.filterMode = FilterMode.Point;
        }

        // A text size for the font in use: the pixel font's to the nearest whole multiple of its design size (13 and
        // 15 are 12, 20 is 24), the smooth font's as asked.
        public static int TextSize(int size) =>
            PixelFont ? Mathf.Max(1, Mathf.RoundToInt(size / (float)PixelFontSize)) * PixelFontSize : size;

        // Sets a style's size and boldness for the font in use; call each time before drawing with it, since the menu
        // may change the font. The pixel font is already bold, and Unity would smear it bolder.
        public static GUIStyle Text(GUIStyle style, int size, bool bold = false)
        {
            style.fontSize = TextSize(size);
            style.fontStyle = bold && !PixelFont ? FontStyle.Bold : FontStyle.Normal;
            return style;
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
                foreach (string word in Words(paragraphs[p]))
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

        // The words of a line, with a lone separator ("·", "+", "/") kept on the word before it, so no line starts
        // with one.
        static List<string> Words(string line)
        {
            var words = new List<string>();
            foreach (string word in line.Split(' '))
            {
                bool separator = word.Length == 1 && !char.IsLetterOrDigit(word[0]);
                if (separator && words.Count > 0) words[words.Count - 1] += " " + word;
                else words.Add(word);
            }
            return words;
        }

        static float TextWidth(GUIStyle style, string text)
        {
            Measured.text = text;
            return style.CalcSize(Measured).x;
        }

        // The buttons the last frame drew, for the cloud's playtests (Playtest.cs): what each said, where it was on the
        // screen in points, whether it could be pressed, and whether it showed (not scrolled out of its list). Every
        // button goes through Button, LayoutButton or Toolbar so a playtest can press it with a real click or tap,
        // as a person would. Kept only while a playtest asks (Probing), and only in repaints, when the rects are real.
        internal readonly struct Drawn
        {
            public Drawn(string label, string row, Rect rect, bool enabled, bool shown)
            {
                Label = label;
                Row = row;
                Rect = rect;
                Enabled = enabled;
                Shown = shown;
            }

            public string Label { get; }
            // The text written last before the button (Row): the name of the thing its row is about.
            public string Row { get; }
            public Rect Rect { get; }
            public bool Enabled { get; }
            public bool Shown { get; }
        }

        internal static bool Probing { get; set; }
        internal static IReadOnlyList<Drawn> LastDrawn { get; private set; } = new List<Drawn>();
        static readonly List<Drawn> Drawing = new List<Drawn>();
        static readonly Stack<int> ScrollStarts = new Stack<int>();
        static string _row = "";
        // GUIUtility.GUIToScreenPoint's units per point (the GUI matrix may or may not be in it).
        static float _screenPerPoint = 1;

        // At the start of each OnGUI pass (Begin), the last repaint's buttons become the drawn ones.
        static void StartProbe()
        {
            if (!Probing || Event.current.type != EventType.Repaint) return;
            LastDrawn = Drawing.ToArray();
            Drawing.Clear();
            _row = "";
            ScrollStarts.Clear();
            _screenPerPoint = Mathf.Max(0.01f, GUIUtility.GUIToScreenPoint(new Vector2(1000, 0)).x / 1000f);
        }

        static Rect ToPoints(Rect rect)
        {
            var min = GUIUtility.GUIToScreenPoint(rect.min) / _screenPerPoint;
            var max = GUIUtility.GUIToScreenPoint(rect.max) / _screenPerPoint;
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        internal static void Probe(string label, Rect rect)
        {
            if (Probing && Event.current.type == EventType.Repaint) Drawing.Add(new Drawn(label, _row, ToPoints(rect), GUI.enabled, true));
        }

        // Notes the text a row starts with, for the buttons after it.
        internal static void Row(string text)
        {
            if (Probing && Event.current.type == EventType.Repaint) _row = text ?? "";
        }

        public static bool Button(Rect rect, string text)
        {
            Probe(text, rect);
            return GUI.Button(rect, text);
        }

        public static bool LayoutButton(string text, params GUILayoutOption[] options)
        {
            bool pressed = GUILayout.Button(text, options);
            Probe(text, GUILayoutUtility.GetLastRect());
            return pressed;
        }

        public static bool LayoutButton(string text, GUIStyle style, params GUILayoutOption[] options)
        {
            bool pressed = GUILayout.Button(text, style, options);
            Probe(text, GUILayoutUtility.GetLastRect());
            return pressed;
        }

        public static bool LayoutButton(GUIContent content, params GUILayoutOption[] options)
        {
            bool pressed = GUILayout.Button(content, options);
            Probe(content.text, GUILayoutUtility.GetLastRect());
            return pressed;
        }

        public static int Toolbar(int selected, string[] texts, params GUILayoutOption[] options)
        {
            int chosen = GUILayout.Toolbar(selected, texts, options);
            var row = GUILayoutUtility.GetLastRect();
            float width = row.width / Mathf.Max(1, texts.Length);
            for (int i = 0; i < texts.Length; i++) Probe(texts[i], new Rect(row.x + i * width, row.y, width, row.height));
            return chosen;
        }

        public static Vector2 BeginScrollView(Vector2 position, params GUILayoutOption[] options)
        {
            ScrollStarts.Push(Drawing.Count);
            return GUILayout.BeginScrollView(position, options);
        }

        public static Vector2 BeginScrollView(Vector2 position, GUIStyle horizontal, GUIStyle vertical, params GUILayoutOption[] options)
        {
            ScrollStarts.Push(Drawing.Count);
            return GUILayout.BeginScrollView(position, horizontal, vertical, options);
        }

        // Buttons scrolled out of the list's view do not show.
        public static void EndScrollView()
        {
            GUILayout.EndScrollView();
            int start = ScrollStarts.Count > 0 ? ScrollStarts.Pop() : Drawing.Count;
            if (!Probing || Event.current.type != EventType.Repaint) return;
            var view = ToPoints(GUILayoutUtility.GetLastRect());
            for (int i = start; i < Drawing.Count; i++)
            {
                var drawn = Drawing[i];
                if (!view.Contains(drawn.Rect.center)) Drawing[i] = new Drawn(drawn.Label, drawn.Row, drawn.Rect, drawn.Enabled, false);
            }
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
