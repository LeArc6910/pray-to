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
    // small, so the GUI matrix scales points up by that ratio. A phone's own build takes it from the screen's dots per
    // inch (160 a point, as Android counts), a PC's from the system's (96 a point, so 1 unless the display is scaled
    // up), and the menu's "UI 크기" multiplies it (NativeScale). The points also start inside the screen's safe area
    // (Screen.safeArea: a phone's notch, camera hole and rounded corners), so every screen lays itself out there; what
    // covers the whole screen draws over Ui.FullScreen (Docs/NativeBuild.md).
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

        // Where the goal list under the status box ends (the box's own bottom when there is none), and where the time
        // and place lines under the minimap column end, as last drawn.
        public static float LeftColumnBottom { get; set; } = 170;
        public static float InfoBottom { get; set; }

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

        // The time and place lines under the minimap column (GameRoot.DrawInfo), as tall as they need.
        public const float InfoWidth = 200;

        // Room kept for them: four small lines.
        const float InfoReserve = 60;

        // Under the column; but on a low touch screen (a phone held sideways) lines there would run into the skill
        // buttons in the corner below (667×375, the 2026-11-09 layout test), so there they stand to the left of the
        // column, their bottom level with its bottom: clear of the HUD, of a boss bar along the top and of the skill
        // buttons. Under the column again when the HUD leaves no room beside it.
        public static Rect InfoArea(float height)
        {
            var under = new Rect(MinimapArea.xMax - InfoWidth, RightColumnBottom + 4, InfoWidth, height);
            if (!Controls.UsingTouch || under.y + InfoReserve <= SkillBar.Bounds(SkillBar.MostSkills).yMin) return under;
            var beside = new Rect(MinimapArea.x - 8 - InfoWidth, RightColumnBottom - InfoReserve, InfoWidth, height);
            return beside.x >= HudArea.xMax + 6 ? beside : under;
        }

        public static float RightColumnBottom => MinimapArea.yMax + (Controls.UsingTouch ? ColumnGap + TouchButtonHeight : 0);

        public static float Scale { get; private set; } = 1;

        // Device pixels per CSS pixel in a browser (Scale without the menu's "UI 크기"), for the page's own text box
        // (WebText); 1 elsewhere.
        public static float PageRatio { get; private set; } = 1;

        // The size of the safe area in points, where every screen lays itself out: a test's stand-in screen when there
        // is one (SizeOverride, the whole screen), so the play mode tests can check the layout for a phone's screen on
        // any machine, less the edges kept clear (Inset).
        public static float Width => FullWidth - Inset.Left - Inset.Right;
        public static float Height => FullHeight - Inset.Top - Inset.Bottom;

        // The whole screen in points, edges outside the safe area included.
        public static float FullWidth => SizeOverride?.x ?? Screen.width / Scale;
        public static float FullHeight => SizeOverride?.y ?? Screen.height / Scale;

        // The whole screen in the points the screens draw in (its corner left of and above 0, 0 when there are insets):
        // for what covers everything, a dimming, a backdrop or a press anywhere.
        public static Rect FullScreen => new Rect(-Inset.Left, -Inset.Top, FullWidth, FullHeight);

        internal static Vector2? SizeOverride { get; set; }

        // How far in from each edge, in points, the safe area starts: a test's stand-in (InsetOverride) or the device's
        // (Screen.safeArea, read at the start of each OnGUI; none in a browser).
        public static Edges Inset => InsetOverride ?? _deviceInset;

        internal static Edges? InsetOverride { get; set; }

        static Edges _deviceInset;

        public readonly struct Edges
        {
            public readonly float Left, Top, Right, Bottom;

            public Edges(float left, float top, float right, float bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public bool Any => Left > 0 || Top > 0 || Right > 0 || Bottom > 0;

            public override string ToString() => $"{Left:0} {Top:0} {Right:0} {Bottom:0}";
        }

        // Call at the start of every OnGUI.
        public static void Begin()
        {
            if (_font == null || _fontChoice != Settings.PixelFont) LoadFont(Settings.PixelFont);
            GUI.skin = UiSkin.Get();
            if (_font != null) GUI.skin.font = _font;
            PageRatio = PixelRatio();
            Scale = PointScale(PageRatio, Screen.width, Screen.height, Screen.dpi, Application.isMobilePlatform, Settings.UiSize);
            _deviceInset = DeviceInset();
            // Moved to the safe area by whole device pixels, so the pixel art stays on the pixel grid.
            var inset = Inset;
            GUI.matrix = Matrix4x4.TRS(new Vector3(Mathf.Round(inset.Left * Scale), Mathf.Round(inset.Top * Scale), 0), Quaternion.identity, new Vector3(Scale, Scale, 1));
            _rootOffset = GUIUtility.GUIToScreenPoint(Vector2.zero);
            StartProbe();
            WebText.Frame();
            ProbeCover = Rect.zero;
            // Whether a mouse button or finger is down, and where it went down: a button shows itself pressed only while
            // the press that started on it lasts (UiMotion.Face).
            var e = Event.current;
            if (e.type == EventType.MouseDown)
            {
                Held = true;
                DownAt = e.mousePosition;
            }
            else if (e.type == EventType.MouseUp)
            {
                Held = false;
            }
            UiMotion.StartPass();
            if (e.type == EventType.Repaint) UiMotion.StartRepaint();
            WorldTaken.Clear();
            _late = false;
        }

        // At the end of every OnGUI: the card held back for the top (LateCard), and the windows that closed this frame.
        public static void End()
        {
            DrawLate();
            UiMotion.EndPass();
        }

        public static bool Held { get; private set; }
        public static Vector2 DownAt { get; private set; }

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
            if (Probing && !string.IsNullOrEmpty(text) && Event.current != null && Event.current.type == EventType.Repaint) Writing.Add(text);
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
        // The texts laid out with Wrap in the last repaint (most labels: menus, the bag's lines), for the playtest's
        // "texts" (a check can read a label as a person reads it, not only buttons).
        internal static IReadOnlyList<string> LastWritten { get; private set; } = new List<string>();
        static readonly List<string> Writing = new List<string>();
        static readonly Stack<int> ScrollStarts = new Stack<int>();
        static string _row = "";

        // At the start of each OnGUI pass (Begin), the last repaint's buttons become the drawn ones.
        static void StartProbe()
        {
            if (!Probing || Event.current.type != EventType.Repaint) return;
            LastDrawn = Drawing.ToArray();
            Drawing.Clear();
            LastWritten = Writing.ToArray();
            Writing.Clear();
            _row = "";
            ScrollStarts.Clear();
        }

        // A rect in the current group or list to points on the screen. GUIUtility.GUIToScreenPoint scales a point by
        // the GUI matrix but adds the groups' and lists' offsets unscaled, so where the matrix is not 1 (a phone's
        // pixel ratio) its answer is neither pixels nor points. The offset alone is in points: the point (0, 0) of
        // the current group gives it.
        // A rect drawn here as points on the whole screen: the areas, groups and scroll views it is in undone
        // (GUIToScreenPoint, in points), then the safe area's move. GUIToScreenPoint also counts the GUI matrix's move,
        // but in device pixels and unscaled (the 2026-11-26 sweep: a notched phone's buttons were reported 88 points
        // in for 44), so what it gives at the top (RootOffset, Begin) is taken off and the move added in points.
        internal static Rect ToPoints(Rect rect)
        {
            var offset = GUIUtility.GUIToScreenPoint(Vector2.zero) - _rootOffset;
            var inset = Inset;
            var moved = new Vector2(Mathf.Round(inset.Left * Scale) / Scale, Mathf.Round(inset.Top * Scale) / Scale);
            return new Rect(rect.position + offset + moved, rect.size);
        }

        // GUIToScreenPoint at the top of the GUI, read once the matrix is set each OnGUI.
        static Vector2 _rootOffset;

        internal static void Probe(string label, Rect rect)
        {
            if (!Probing || Event.current.type != EventType.Repaint) return;
            var points = ToPoints(rect);
            Drawing.Add(new Drawn(label, _row, points, GUI.enabled, !(ProbeCover.width > 0 && ProbeCover.Contains(points.center))));
        }

        // Where something drawn later covers the screen and takes the presses there (the fallen card, U7), in points; zero
        // for none. A button drawn under it is not shown to the playtests: a press there is the card's.
        internal static Rect ProbeCover { get; set; }

        // Notes the text a row starts with, for the buttons after it.
        internal static void Row(string text)
        {
            if (Probing && Event.current.type == EventType.Repaint) _row = text ?? "";
        }

        // The three kinds of button (Docs/UiRework.md 4-4 단추 세 가지): the usual one; the screen's one main action, in
        // gold; and one that throws something away or cannot be taken back, red rimmed. And the title's and the menus'
        // words with no box (Docs/TitleRework.md 4-1, 묶음 T3): lit gold, grown a little, a mark at each side and a strip of
        // light under them when pointed at or picked (TitleArt, drawn by TitleDraw.TextItem).
        public enum Look
        {
            Normal,
            Primary,
            Danger,
            Text,
        }

        public static bool Button(Rect rect, string text) => Button(rect, text, GUI.skin.button, Look.Normal);

        public static bool Button(Rect rect, string text, GUIStyle style) => Button(rect, text, style, Look.Normal);

        public static bool Button(Rect rect, string text, Look look) => Button(rect, text, GUI.skin.button, look);

        // A button that shows one text and is known to the playtests by another (a setting shows its value, and is
        // found as "name: value").
        public static bool Button(Rect rect, string shown, string probed) => Button(rect, shown, probed, Look.Normal);

        public static bool Button(Rect rect, string shown, string probed, Look look)
        {
            Probe(probed, rect);
            Temp.text = shown;
            Temp.tooltip = null;
            return Pressable(rect, Temp, GUI.skin.button, look, false);
        }

        // Shown one way, probed another, in a style and look (the title's setting rows, Ui.Look.Text).
        public static bool Button(Rect rect, string shown, string probed, GUIStyle style, Look look, bool chosen)
        {
            Probe(probed, rect);
            Temp.text = shown;
            Temp.tooltip = null;
            return Pressable(rect, Temp, style, look, chosen);
        }

        // A press over something drawn by its caller (a row of the title's lists), known to the playtests by the label: the
        // pointer's tick and the press's sound as a button's.
        public static bool Invisible(Rect rect, string probed)
        {
            Probe(probed, rect);
            var e = Event.current;
            if (!GUI.enabled && e.type == EventType.MouseDown && rect.Contains(e.mousePosition)) UiMotion.Refuse(probed, rect);
            if (e.type == EventType.Repaint && GUI.enabled && rect.Contains(e.mousePosition) && (!Controls.UsingTouch || Held)) UiMotion.Hover(probed + rect.position);
            bool pressed = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            if (pressed) UiMotion.Confirm();
            return pressed;
        }

        public static bool Button(Rect rect, string text, GUIStyle style, Look look, bool chosen = false)
        {
            Probe(text, rect);
            Temp.text = text;
            Temp.tooltip = null;
            return Pressable(rect, Temp, style, look, chosen);
        }

        public static bool LayoutButton(string text, params GUILayoutOption[] options) => LayoutButton(text, GUI.skin.button, Look.Normal, options);

        public static bool LayoutButton(string text, GUIStyle style, params GUILayoutOption[] options) => LayoutButton(text, style, Look.Normal, options);

        public static bool LayoutButton(string text, Look look, params GUILayoutOption[] options) => LayoutButton(text, GUI.skin.button, look, options);

        public static bool LayoutButton(GUIContent content, params GUILayoutOption[] options) => LayoutButton(content, GUI.skin.button, Look.Normal, options);

        public static bool LayoutButton(string text, GUIStyle style, Look look, params GUILayoutOption[] options)
        {
            Temp.text = text;
            Temp.tooltip = null;
            return LayoutButton(Temp, style, look, options);
        }

        // Laid out as GUILayout.Button lays out (its rect from the style and options), then drawn here.
        static bool LayoutButton(GUIContent content, GUIStyle style, Look look, GUILayoutOption[] options)
        {
            var rect = GUILayoutUtility.GetRect(content, style, options);
            string text = content.text;
            bool pressed = Pressable(rect, content, style, look, false);
            Probe(text, rect);
            return pressed;
        }

        static readonly GUIContent Temp = new GUIContent();

        // Segments in a row, one chosen, as GUILayout.Toolbar, drawn with the buttons' frames.
        public static int Toolbar(int selected, string[] texts, params GUILayoutOption[] options)
        {
            Temp.text = texts.Length > 0 ? texts[0] : "";
            Temp.tooltip = null;
            var row = GUILayoutUtility.GetRect(Temp, GUI.skin.button, options.Length > 0 ? options : new[] { GUILayout.ExpandWidth(true) });
            float width = row.width / Mathf.Max(1, texts.Length);
            int chosen = selected;
            for (int i = 0; i < texts.Length; i++)
            {
                var segment = new Rect(row.x + i * width, row.y, width - (i < texts.Length - 1 ? 2 : 0), row.height);
                Probe(texts[i], segment);
                Temp.text = texts[i];
                Temp.tooltip = null;
                if (Pressable(segment, Temp, GUI.skin.button, Look.Normal, i == selected)) chosen = i;
            }
            return chosen;
        }

        // A button: its frame drawn crisp (UiSkin.ButtonFrame), lit under the pointer, sunk while pressed, a little
        // shake when it cannot be used and is pressed anyway (UiMotion), and the text over it in the style's colours.
        // The press itself is an invisible GUI.Button on the rect, so a button takes clicks and taps exactly as before.
        // A style without a background (a bare label, GUIStyle.none) gets no frame.
        static bool Pressable(Rect rect, GUIContent content, GUIStyle style, Look look, bool chosen)
        {
            var e = Event.current;
            bool enabled = GUI.enabled;
            string key = content.text;
            if (!enabled && e.type == EventType.MouseDown && rect.Contains(e.mousePosition)) UiMotion.Refuse(key, rect);
            if (e.type == EventType.Repaint)
            {
                float shake = UiMotion.Shake(key, rect);
                bool hover = enabled && rect.Contains(e.mousePosition) && (!Controls.UsingTouch || Held);
                bool down = hover && Held && rect.Contains(DownAt);
                if (look == Look.Text)
                {
                    TitleDraw.TextItem(new Rect(rect.x + shake, rect.y, rect.width, rect.height), content.text, style, enabled, hover, down, chosen);
                    if (hover) UiMotion.Hover(key + rect.position);
                }
                else Framed(rect, content, style, look, chosen, enabled, shake, hover, down, key);
            }
            bool pressed = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            if (pressed) UiMotion.Confirm();
            return pressed;
        }

        // A framed button's face (the three kinds), in a repaint.
        static void Framed(Rect rect, GUIContent content, GUIStyle style, Look look, bool chosen, bool enabled, float shake, bool hover, bool down, string key)
        {
            var face = UiMotion.Face(new Rect(rect.x + shake, rect.y, rect.width, rect.height), hover, down);
            bool framed = style.normal.background != null;
            if (framed)
            {
                var previous = GUI.color;
                if (!enabled) GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * 0.55f);
                UiSkin.ButtonFrame(face, hover && !down, down, chosen || look == Look.Primary, look == Look.Danger);
                GUI.color = previous;
            }
            var bare = framed ? UiSkin.Bare(style) : style;
            bare.Draw(face, content, hover, down, chosen, false);
            if (hover) UiMotion.Hover(key + rect.position);
        }

        // ---- Buttons over things in the world ----

        static readonly List<Rect> WorldTaken = new List<Rect>();

        // How tall a button over the world is: a finger's 40 points on a touch screen (Docs/Design.md 기술 원칙 7).
        public static float WorldButtonHeight => Controls.UsingTouch ? TouchButtonHeight : 28;

        // A button over something in the world, its middle at the point, moved up past those already drawn this pass so
        // two never sit on one spot (Docs/UiRework.md 2절 겹침 버그: [끄기] under [○○와 말하기] on a phone).
        public static bool WorldButton(Vector2 point, string text, float width)
        {
            float height = WorldButtonHeight;
            var wanted = new Rect(point.x - width / 2, point.y - height / 2, width, height);
            var rect = Stack(wanted, WorldTaken, Width);
            WorldTaken.Add(rect);
            Controls.Block(rect);
            return Button(rect, text);
        }

        // Where a button wanted at that rect goes: on the screen across, and above any taken one it would touch.
        internal static Rect Stack(Rect wanted, IReadOnlyList<Rect> taken, float screenWidth)
        {
            var rect = wanted;
            rect.x = Mathf.Clamp(rect.x, 4, Mathf.Max(4, screenWidth - rect.width - 4));
            for (int round = 0; round < 8; round++)
            {
                bool moved = false;
                foreach (var other in taken)
                {
                    if (rect.xMin >= other.xMax + 2 || other.xMin >= rect.xMax + 2 || rect.yMin >= other.yMax + 2 || other.yMin >= rect.yMax + 2) continue;
                    rect.y = other.y - rect.height - 4;
                    moved = true;
                }
                if (!moved) break;
            }
            return rect;
        }

        // ---- A card over everything ----

        static bool _late;
        static Rect _lateRect;
        static string _lateText;

        // A card (a description) to draw when the pass ends, over everything drawn after the thing it describes: drawn
        // at once, the skill slot's description went under the quick slots (Docs/UiRework.md 2절, pc-boss-bar). The last
        // asked for in a pass is the one drawn.
        public static void LateCard(Rect rect, string text)
        {
            _late = true;
            _lateRect = rect;
            _lateText = text;
        }

        // How big a card for the text is at most `width` wide (wrapped between words), as narrow as its longest line.
        public static Vector2 CardSize(string text, float width, out string wrapped)
        {
            var style = UiSkin.CardText;
            wrapped = Wrap(text, style, width);
            var content = new GUIContent(wrapped);
            float w = Mathf.Min(width, Mathf.Ceil(style.CalcSize(content).x) + 2);
            return new Vector2(w, style.CalcHeight(content, w));
        }

        static void DrawLate()
        {
            if (!_late) return;
            _late = false;
            UiSkin.Card(_lateRect);
            GUI.Label(_lateRect, _lateText, UiSkin.CardText);
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

        // A finger dragged over a list scrolls it, as on any phone: IMGUI itself scrolls only by its bar and the wheel.
        // Call before drawing the list, with the area it is in. Once the finger has moved a few points it is a drag,
        // not a press, so the button it started on is let go and does not fire when the finger lifts.
        const float DragStart = 8;
        const float JumpLimit = 48;
        static bool _dragArmed;
        static bool _dragging;
        static Vector2 _dragFrom;
        static Vector2 _dragLast;

        public static void TouchScroll(Rect area, ref Vector2 scroll)
        {
            if (!Controls.UsingTouch) return;
            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    _dragArmed = area.Contains(e.mousePosition);
                    _dragging = false;
                    _dragFrom = _dragLast = e.mousePosition;
                    break;
                case EventType.MouseDrag:
                    if (!_dragArmed) break;
                    // A jump at once, before any drag, is no finger moving but the pointer catching up with a tap
                    // somewhere else (a phone's touch, 2026-11-22): it starts the drag from there, so the tap stays a tap.
                    if (!_dragging && (e.mousePosition - _dragLast).magnitude > JumpLimit)
                    {
                        _dragFrom = _dragLast = e.mousePosition;
                        break;
                    }
                    if (!_dragging && (e.mousePosition - _dragFrom).magnitude > DragStart)
                    {
                        _dragging = true;
                        GUIUtility.hotControl = 0;
                    }
                    if (_dragging)
                    {
                        scroll.y = Mathf.Max(0, scroll.y - (e.mousePosition.y - _dragLast.y));
                        e.Use();
                    }
                    _dragLast = e.mousePosition;
                    break;
                case EventType.MouseUp:
                    if (_dragging) e.Use();
                    _dragArmed = _dragging = false;
                    break;
            }
        }

        // Screen pixels as cameras and touches give them (origin bottom left) to GUI points (origin top left of the safe
        // area).
        public static Vector2 FromScreen(Vector2 pixels) =>
            new Vector2(pixels.x / Scale - Mathf.Round(Inset.Left * Scale) / Scale, (Screen.height - pixels.y) / Scale - Mathf.Round(Inset.Top * Scale) / Scale);

        // The menu's "UI 크기" (Settings.UiSize), in percent.
        public static readonly int[] UiSizes = { 90, 100, 115, 130 };

        // Device pixels per point. In a browser the page's pixel ratio (as before); a phone's own build 160 dots per
        // inch a point (Android's measure: 2.5 on a 400-dpi phone, as its browser gives), or its screen's short side over
        // 360 when it does not say; a PC 96 dots a point, so 1 at 100 % and 1.5 on a display scaled to 150 %, rounded to
        // quarters. Then times the menu's UI size, but a larger size only as far as the screen keeps 320 points across
        // its short side (a phone's layouts are tested down to 360×640; a PC's own build 480).
        internal static float PointScale(float pageRatio, int width, int height, float dpi, bool mobile, int uiSize)
        {
            float size = Mathf.Clamp(uiSize, 50, 200) / 100f;
            float shortSide = Mathf.Min(width, height);
#if UNITY_WEBGL && !UNITY_EDITOR
            float natural = pageRatio;
            float least = 320;
#else
            float natural = mobile ? (dpi > 0 ? dpi / 160f : shortSide / 360f) : dpi >= 120 ? dpi / 96f : 1;
#if UNITY_EDITOR
            // The editor's game view stays one pixel a point (the play mode tests' pictures), unless its device
            // simulator plays a phone.
            if (!mobile) natural = 1;
#endif
            natural = Mathf.Clamp(Mathf.Round(natural * 4) / 4, 1, 4);
            float least = mobile ? 320 : 480;
            // The base itself held too (a tablet saying an odd dpi), never below one pixel a point.
            if (shortSide > 0) natural = Mathf.Max(1, Mathf.Min(natural, shortSide / least));
#endif
            float scale = natural * size;
            if (size > 1 && shortSide > 0) scale = Mathf.Max(natural, Mathf.Min(scale, shortSide / least));
            return Mathf.Max(0.5f, scale);
        }

        // Screen.safeArea as points in from each edge; none in a browser, whose page has no notch to keep clear of.
        static Edges DeviceInset()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return default;
#else
            var safe = Screen.safeArea;
            float width = Screen.width, height = Screen.height;
            if (safe.width <= 0 || safe.height <= 0 || width <= 0 || height <= 0) return default;
            // Unity's safe area has its origin at the bottom left.
            return new Edges(Mathf.Ceil(Mathf.Max(0, safe.xMin) / Scale), Mathf.Ceil(Mathf.Max(0, height - safe.yMax) / Scale),
                Mathf.Ceil(Mathf.Max(0, width - safe.xMax) / Scale), Mathf.Ceil(Mathf.Max(0, safe.yMin) / Scale));
#endif
        }

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
