using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Client
{
    // The pixel-art look of every IMGUI screen: a copy of Unity's default skin with its backgrounds swapped for the
    // frames in Art/UiArt.cs, and warmer text. Ui.Begin sets it at the start of every OnGUI, so styles made from
    // GUI.skin elsewhere (tooltips, slot labels) inherit it. Also draws the bars (health, the boss) in the same frames.
    // The frames of windows, buttons, cards and slots are drawn at whole device pixels (Crisp, below).
    static partial class UiSkin
    {
        static readonly Color Text = new Color(0.94f, 0.9f, 0.84f);
        static readonly Color TextBright = Color.white;
        static readonly Color TextPressed = new Color(1f, 0.9f, 0.68f);
        // Space inside a panel's frame before its text.
        public const int PanelPadding = 12;
        static readonly Rgba HealthColor = Rgba.Hex("#D24A3C");
        static readonly Rgba BossColor = Rgba.Hex("#A8324A");
        static readonly Rgba ManaColor = Rgba.Hex("#3C6ED2");

#if UNITY_EDITOR
        // High-resolution backgrounds exist only in the editor; players have none to drop.
        static readonly Texture2D[] NoScaled = new Texture2D[0];
#endif

        static GUISkin _skin;
        static GUIStyle _barWell;
        static GUIStyle _barText;
        static Texture2D _healthFill;
        static Texture2D _bossFill;
        static Texture2D _manaFill;
        static Texture2D _experienceFill;

        // Call inside OnGUI: Unity's default skin can only be read there.
        public static GUISkin Get()
        {
            if (_skin != null) return _skin;
            var skin = Object.Instantiate(GUI.skin);
            skin.name = "Pixel";
            var panel = Texture(UiArt.Panel);
            var inset = Texture(UiArt.Inset);
            var button = Texture(UiArt.Button());
            var hover = Texture(UiArt.Button(hover: true));
            var pressed = Texture(UiArt.Button(pressed: true));
            var chosen = Texture(UiArt.Button(chosen: true));
            var chosenPressed = Texture(UiArt.Button(pressed: true, chosen: true));

            Frame(skin.box, UiArt.PanelBorder, panel, panel, panel, panel, panel, panel);
            Frame(skin.window, UiArt.PanelBorder, panel, panel, panel, panel, panel, panel);
            // Text keeps clear of the frame (six points wide) by as much again.
            skin.box.padding = new RectOffset(PanelPadding, PanelPadding, PanelPadding - 2, PanelPadding - 2);
            skin.window.padding = new RectOffset(PanelPadding, PanelPadding, PanelPadding - 2, PanelPadding - 2);
            // A toolbar draws its tabs with this style; the chosen tab is "on".
            Frame(skin.button, UiArt.ButtonBorder, button, hover, pressed, chosen, chosen, chosenPressed);
            skin.button.hover.textColor = TextBright;
            skin.button.active.textColor = TextPressed;
            skin.button.onNormal.textColor = TextBright;
            skin.button.onHover.textColor = TextBright;
            skin.button.onActive.textColor = TextPressed;
            Frame(skin.textField, UiArt.InsetBorder, inset, inset, inset, inset, inset, inset);
            Frame(skin.textArea, UiArt.InsetBorder, inset, inset, inset, inset, inset, inset);
            skin.textField.focused.background = inset;
            skin.textField.focused.textColor = TextBright;
            Frame(skin.verticalScrollbar, UiArt.InsetBorder, inset, inset, inset, inset, inset, inset);
            Frame(skin.horizontalScrollbar, UiArt.InsetBorder, inset, inset, inset, inset, inset, inset);
            Frame(skin.verticalScrollbarThumb, UiArt.ButtonBorder, button, hover, pressed, button, hover, pressed);
            Frame(skin.horizontalScrollbarThumb, UiArt.ButtonBorder, button, hover, pressed, button, hover, pressed);
            skin.label.normal.textColor = Text;
            skin.scrollView.normal.background = null;
#if UNITY_EDITOR
            skin.scrollView.normal.scaledBackgrounds = NoScaled;
#endif

            _barWell = new GUIStyle { border = new RectOffset(Edge(UiArt.InsetBorder), Edge(UiArt.InsetBorder), Edge(UiArt.InsetBorder), Edge(UiArt.InsetBorder)) };
            _barWell.normal.background = inset;
            _healthFill = Texture(UiArt.BarFill(HealthColor));
            _bossFill = Texture(UiArt.BarFill(BossColor));
            _manaFill = Texture(UiArt.BarFill(ManaColor));
            _experienceFill = Texture(UiArt.BarFill(Rgba.Hex("#E8C04A")));
            _skin = skin;
            return skin;
        }

        // Headings and the game's name; small notes.
        public static readonly Color Gold = new Color(0.95f, 0.78f, 0.42f);
        public static readonly Color Muted = new Color(0.72f, 0.66f, 0.58f);

        static Texture2D _backdrop, _divider;

        // Behind the start screen: deep cave dark, a warm glow low in the middle as of a fire far off, darker at the
        // edges (2026-11-11, the owner: a tidier start screen).
        public static void Backdrop(Rect area)
        {
            if (_backdrop == null)
            {
                const int w = 64, h = 64;
                _backdrop = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var top = new Color(0.035f, 0.028f, 0.03f);
                var glow = new Color(0.22f, 0.12f, 0.06f);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float dx = (x + 0.5f) / w - 0.5f, dy = (y + 0.5f) / h - 0.32f;
                        float light = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx * 2.2f + dy * dy * 3.2f) * 1.6f);
                        float edge = Mathf.Clamp01(1 - (dx * dx + (y / (float)h - 0.5f) * (y / (float)h - 0.5f)) * 1.2f);
                        var c = Color.Lerp(top, glow, light * light) * (0.55f + 0.45f * edge);
                        c.a = 1;
                        _backdrop.SetPixel(x, y, c);
                    }
                }
                _backdrop.Apply();
            }
            GUI.DrawTexture(area, _backdrop, ScaleMode.StretchToFill);
        }

        // A thin gold rule that fades out at both ends, under a page's title.
        public static void Divider(Rect area)
        {
            if (_divider == null)
            {
                const int w = 64;
                _divider = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                for (int x = 0; x < w; x++)
                {
                    float t = Mathf.Sin(Mathf.PI * (x + 0.5f) / w);
                    _divider.SetPixel(x, 0, new Color(Gold.r, Gold.g, Gold.b, 0.75f * t));
                }
                _divider.Apply();
            }
            GUI.DrawTexture(new Rect(area.x, area.y + area.height / 2 - 1, area.width, 2), _divider, ScaleMode.StretchToFill);
        }

        // A bar in a dark well: the fill stretched to the share it shows, and the text over it with a shadow.
        public static void Bar(Rect area, float share, string text, bool boss = false, bool mana = false, int textSize = 13, bool experience = false)
        {
            if (_barWell == null) return;
            _barText ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
            Ui.Text(_barText, textSize, bold: true);
            GUI.Box(area, GUIContent.none, _barWell);
            float edge = UiArt.InsetBorder * UiArt.Scale;
            var inside = new Rect(area.x + edge, area.y + edge, area.width - edge * 2, area.height - edge * 2);
            GUI.DrawTexture(new Rect(inside.x, inside.y, inside.width * Mathf.Clamp01(share), inside.height), boss ? _bossFill : mana ? _manaFill : experience ? _experienceFill : _healthFill);
            if (string.IsNullOrEmpty(text)) return;
            var previous = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.8f * previous.a);
            GUI.Label(new Rect(area.x + 1, area.y + 1, area.width, area.height), text, _barText);
            GUI.color = previous;
            GUI.Label(area, text, _barText);
        }

        // Sets a style's backgrounds for its states (off, then on as for a chosen tab) and how many texture pixels at
        // each edge keep their size. In the editor the default skin's high-resolution backgrounds are dropped, or a
        // high-density screen would still show them.
        static void Frame(GUIStyle style, int border, Texture2D normal, Texture2D hover, Texture2D active, Texture2D onNormal, Texture2D onHover, Texture2D onActive)
        {
            int edge = Edge(border);
            style.border = new RectOffset(edge, edge, edge, edge);
            Set(style.normal, normal);
            Set(style.hover, hover);
            Set(style.active, active);
            Set(style.focused, normal);
            Set(style.onNormal, onNormal);
            Set(style.onHover, onHover);
            Set(style.onActive, onActive);
            Set(style.onFocused, onNormal);
        }

        static void Set(GUIStyleState state, Texture2D background)
        {
            state.background = background;
#if UNITY_EDITOR
            state.scaledBackgrounds = NoScaled;
#endif
            state.textColor = Text;
        }

        static int Edge(int border) => border * UiArt.Scale;

        static Texture2D Texture(PixelImage image) => Sprites.TextureOf(UiArt.Enlarged(image));

        // ---- Crisp: frames at whole device pixels (Docs/UiRework.md 4-4 틀은 기기 픽셀 정수 배) ----

        // How many device pixels one pixel of the UI art takes: the art's own doubling (UiArt.Scale) at the screen's
        // pixel ratio, to the nearest whole number. The skin's frames are stretched by the GUI matrix, so on a phone
        // whose ratio is 2.625 their pixels came out 5 and 6 device pixels across by turns; these are 5 each. On a
        // desktop (ratio 1) both are 2, as before. One art pixel is Pt points.
        public static int Dots => Mathf.Max(1, Mathf.RoundToInt(UiArt.Scale * Ui.Scale));
        public static float Pt => Dots / Ui.Scale;

        // A rect in the current group's points with its edges moved onto device pixels.
        public static Rect Snap(Rect rect)
        {
            float s = Ui.Scale;
            var offset = GUIUtility.GUIToScreenPoint(Vector2.zero);
            return Rect.MinMaxRect(
                Mathf.Round((rect.xMin + offset.x) * s) / s - offset.x, Mathf.Round((rect.yMin + offset.y) * s) / s - offset.y,
                Mathf.Round((rect.xMax + offset.x) * s) / s - offset.x, Mathf.Round((rect.yMax + offset.y) * s) / s - offset.y);
        }

        // A picture of the UI art and how many pixels at each edge keep their size when it is stretched, made into a
        // texture with Dots texels to each pixel (again when the screen's ratio changes).
        sealed class Piece
        {
            readonly bool _repeat;
            Texture2D _texture;
            int _dots;

            public Piece(PixelImage image, int border = 0, bool repeat = false)
            {
                Image = image;
                Border = border;
                _repeat = repeat;
            }

            public PixelImage Image { get; }
            public int Border { get; }

            public Texture2D Texture(int dots)
            {
                if (_texture != null && _dots == dots) return _texture;
                if (_texture != null) Object.Destroy(_texture);
                var big = new PixelImage(Image.Width * dots, Image.Height * dots);
                for (int y = 0; y < big.Height; y++)
                    for (int x = 0; x < big.Width; x++) big[x, y] = Image[x / dots, y / dots];
                _texture = Sprites.TextureOf(big);
                _texture.wrapMode = _repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                _dots = dots;
                return _texture;
            }
        }

        static readonly float[] SliceX = new float[4], SliceY = new float[4], SliceU = new float[4], SliceV = new float[4];

        // The picture stretched over the rect by its middle (nine-slice), its edges Border pixels of Dots device pixels.
        static void Slice(Rect rect, Piece piece)
        {
            if (Event.current.type != EventType.Repaint || rect.width <= 0 || rect.height <= 0) return;
            rect = Snap(rect);
            float pt = Pt, s = Ui.Scale;
            var texture = piece.Texture(Dots);
            float w = piece.Image.Width, h = piece.Image.Height;
            float bx = Mathf.Min(piece.Border * pt, Mathf.Floor(rect.width / 2 * s) / s);
            float by = Mathf.Min(piece.Border * pt, Mathf.Floor(rect.height / 2 * s) / s);
            float ux = bx / pt / w, uy = by / pt / h;
            SliceX[0] = rect.xMin; SliceX[1] = rect.xMin + bx; SliceX[2] = rect.xMax - bx; SliceX[3] = rect.xMax;
            SliceY[0] = rect.yMin; SliceY[1] = rect.yMin + by; SliceY[2] = rect.yMax - by; SliceY[3] = rect.yMax;
            SliceU[0] = 0; SliceU[1] = ux; SliceU[2] = 1 - ux; SliceU[3] = 1;
            // A texture's rows count from the bottom.
            SliceV[0] = 1; SliceV[1] = 1 - uy; SliceV[2] = uy; SliceV[3] = 0;
            for (int j = 0; j < 3; j++)
            {
                for (int i = 0; i < 3; i++)
                {
                    var dest = Rect.MinMaxRect(SliceX[i], SliceY[j], SliceX[i + 1], SliceY[j + 1]);
                    if (dest.width <= 0.001f || dest.height <= 0.001f) continue;
                    GUI.DrawTextureWithTexCoords(dest, texture, new Rect(SliceU[i], SliceV[j + 1], SliceU[i + 1] - SliceU[i], SliceV[j] - SliceV[j + 1]));
                }
            }
        }

        static readonly Dictionary<PixelImage, Piece> Images = new Dictionary<PixelImage, Piece>();

        // A picture of the UI art (an icon, the close button) at its size, its top left at the point.
        public static void Image(Vector2 at, PixelImage image)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!Images.TryGetValue(image, out var piece)) Images[image] = piece = new Piece(image);
            var rect = Snap(new Rect(at.x, at.y, image.Width * Pt, image.Height * Pt));
            GUI.DrawTexture(rect, piece.Texture(Dots));
        }

        public static Vector2 SizeOf(PixelImage image) => new Vector2(image.Width * Pt, image.Height * Pt);

        // One of the small icons (UiIcons) in a colour, centred in the rect.
        public static void Icon(Rect area, string[] rows, Color color)
        {
            var image = UiIcons.Of(rows, ToRgba(color));
            var size = SizeOf(image);
            Image(new Vector2(area.center.x - size.x / 2, area.center.y - size.y / 2), image);
        }

        public static Vector2 IconSize(string[] rows) => SizeOf(UiIcons.Of(rows, Rgba.White));

        static Rgba ToRgba(Color color) => new Rgba(Mathf.RoundToInt(color.r * 255), Mathf.RoundToInt(color.g * 255), Mathf.RoundToInt(color.b * 255), Mathf.RoundToInt(color.a * 255));

        static int Key(Color color)
        {
            var c = ToRgba(color);
            return c.R << 24 | c.G << 16 | c.B << 8 | c.A;
        }

        // ---- The three levels of panel (Docs/UiRework.md 4-4 판 위계 3단) ----

        static Piece _panel, _inset, _card, _sign;

        // The ground of a window or a HUD box: dark stone in a thin gold rim.
        public static void Panel(Rect area) => Slice(area, _panel ??= new Piece(UiArt.Panel, UiArt.PanelBorder));

        // A well inside a panel, darker, for slots, fields and lists.
        public static void Inset(Rect area) => Slice(area, _inset ??= new Piece(UiArt.Inset, UiArt.InsetBorder));

        // What floats over everything (a description): a step lighter, its gold rim lit.
        public static void Card(Rect area) => Slice(area, _card ??= new Piece(UiArt.Card, UiArt.CardBorder));

        static readonly Dictionary<int, Piece> ButtonPieces = new Dictionary<int, Piece>();

        // A button's frame: lit under the pointer, sunk while pressed, gold when chosen or the screen's main action, red
        // rimmed when it does something that cannot be taken back.
        public static void ButtonFrame(Rect area, bool hover, bool pressed, bool chosen, bool danger)
        {
            int key = (hover ? 1 : 0) | (pressed ? 2 : 0) | (chosen ? 4 : 0) | (danger ? 8 : 0);
            if (!ButtonPieces.TryGetValue(key, out var piece))
                ButtonPieces[key] = piece = new Piece(UiArt.Button(hover, pressed, chosen, danger), UiArt.ButtonBorder);
            Slice(area, piece);
        }

        static readonly Dictionary<(int, bool), Piece> SlotPieces = new Dictionary<(int, bool), Piece>();

        // An item's slot: a dark well with a rim of its colour (a rarity, a potion's red), glowing for epic and up.
        public static void SlotFrame(Rect area, Color? tint, bool glow = false)
        {
            int key = tint is Color color ? Key(color) : 0;
            if (!SlotPieces.TryGetValue((key, glow), out var piece))
                SlotPieces[(key, glow)] = piece = new Piece(UiArt.Slot(tint is Color c ? ToRgba(c) : (Rgba?)null, glow), UiArt.SlotBorder);
            Slice(area, piece);
            if (glow && tint is Color shine && !UiMotion.Reduced)
            {
                // Epic and legendary rims breathe (Docs/UiRework.md 4-6 희귀 아이템).
                float breath = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f);
                var previous = GUI.color;
                GUI.color = new Color(shine.r, shine.g, shine.b, 0.22f * breath * previous.a);
                float edge = UiArt.SlotBorder * Pt;
                Outline(new Rect(area.x + Pt, area.y + Pt, area.width - 2 * Pt, area.height - 2 * Pt), Pt);
                GUI.color = new Color(shine.r, shine.g, shine.b, 0.1f * breath * previous.a);
                GUI.DrawTexture(new Rect(area.x + edge, area.y + edge, area.width - 2 * edge, area.height - 2 * edge), Texture2D.whiteTexture);
                GUI.color = previous;
            }
        }

        static void Outline(Rect area, float width)
        {
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, width), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(area.x, area.yMax - width, area.width, width), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(area.x, area.y, width, area.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(area.xMax - width, area.y, width, area.height), Texture2D.whiteTexture);
        }

        // ---- Windows: the panel, a head band of the window's kind, a title and a close button ----

        static readonly Dictionary<(UiKind, int, int), Piece> HeadPieces = new Dictionary<(UiKind, int, int), Piece>();
        static readonly Color DefaultAccent = new Color(0.77f, 0.6f, 0.33f);

        static Piece Head(UiKind kind, Color accent, int part)
        {
            int key = kind == UiKind.Talk || kind == UiKind.Stone ? Key(accent) : 0;
            if (HeadPieces.TryGetValue((kind, key, part), out var piece)) return piece;
            var rgba = ToRgba(accent);
            var image = part == 1 ? UiArt.HeadMiddle(kind, rgba) : UiArt.HeadEnd(kind, rgba, left: part == 0);
            HeadPieces[(kind, key, part)] = piece = new Piece(image, repeat: part == 1);
            return piece;
        }

        // How tall a window's head is, and the frame's width round it.
        public static float HeadHeight => UiArt.HeadHeight * Pt;
        public static float FrameEdge => UiArt.PanelBorder * Pt;

        // The head band along the top of a window, inside its frame: its material, ends and all.
        public static Rect HeadOf(Rect window) => new Rect(window.x + FrameEdge, window.y + FrameEdge, window.width - FrameEdge * 2, HeadHeight);

        // The area a window leaves for what it holds: under the head, inside the padding.
        // On a phone held sideways (Ui.Short) the gaps are smaller, as every row of height counts there.
        public static Rect InsideOf(Rect window)
        {
            float top = HeadOf(window).yMax + (Ui.Short ? 2 : 6);
            return Rect.MinMaxRect(window.x + PanelPadding, top, window.xMax - PanelPadding, window.yMax - (Ui.Short ? FrameEdge + 2 : PanelPadding - 2));
        }

        public static void HeadBand(Rect head, UiKind kind, Color? accent = null)
        {
            if (Event.current.type != EventType.Repaint) return;
            var color = accent ?? DefaultAccent;
            head = Snap(head);
            float pt = Pt;
            float cap = Mathf.Min(UiArt.HeadCap * pt, head.width / 2);
            float height = UiArt.HeadHeight * pt;
            int dots = Dots;
            GUI.DrawTexture(Snap(new Rect(head.x, head.y, cap, height)), Head(kind, color, 0).Texture(dots));
            GUI.DrawTexture(Snap(new Rect(head.xMax - cap, head.y, cap, height)), Head(kind, color, 2).Texture(dots));
            var middle = Snap(new Rect(head.x + cap, head.y, head.width - cap * 2, height));
            if (middle.width <= 0) return;
            // Repeated from where the left end left off, so the pattern runs on.
            float tile = UiArt.HeadTile * pt;
            GUI.DrawTextureWithTexCoords(middle, Head(kind, color, 1).Texture(dots), new Rect(cap / tile, 0, middle.width / tile, 1));
        }

        static GUIStyle _headText;

        // Draws a window and gives the area for what it holds: the stone panel, the head band of its kind with the icon
        // and title (the shop's on a sign under its awning, a speaker's at the left), and room on the right for the close
        // button. `head` is the band, for what else a window puts in it (coins, hearts, the close button).
        public static Rect Window(Rect area, UiKind kind, string title, string[] icon, out Rect head, Color? accent = null, bool closable = true)
        {
            Panel(area);
            head = HeadOf(area);
            HeadBand(head, kind, accent);
            if (!string.IsNullOrEmpty(title)) Title(head, kind, title, icon, closable);
            return InsideOf(area);
        }

        static void Title(Rect head, UiKind kind, string title, string[] icon, bool closable)
        {
            _headText ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            Ui.Text(_headText, 12, bold: true);
            if (Ui.Probing && Event.current.type == EventType.Repaint) Ui.Wrap(title, _headText, 9999);
            float pt = Pt;
            float band = UiArt.HeadBand * pt;
            var iconSize = icon != null ? IconSize(icon) : Vector2.zero;
            float gap = icon != null ? 4 : 0;
            float room = head.width - UiArt.HeadCap * pt * 2 - (closable ? CloseRoom : 0);
            float text = Mathf.Min(_headText.CalcSize(new GUIContent(title)).x, room - iconSize.x - gap);
            float whole = iconSize.x + gap + text;
            bool left = kind == UiKind.Talk;
            float x = left ? head.x + UiArt.HeadCap * pt + 2 : head.x + UiArt.HeadCap * pt + Mathf.Max(0, (room - whole) / 2);
            float middle = head.y + band / 2;
            if (kind == UiKind.Shop)
            {
                // The name on a board hanging under the awning.
                middle = head.y + 10.5f * pt;
                Slice(new Rect(x - 5 * pt, head.y + 6 * pt, whole + 10 * pt, 9 * pt), _sign ??= new Piece(UiArt.Sign, UiArt.SignBorder));
            }
            var ink = UiArt.HeadInk(kind);
            var color = new Color(ink.R / 255f, ink.G / 255f, ink.B / 255f);
            if (icon != null) Icon(new Rect(x, middle - iconSize.y / 2, iconSize.x, iconSize.y), icon, color);
            var line = new Rect(x + iconSize.x + gap, middle - 9, text, 18);
            var previous = GUI.color;
            if (UiArt.HeadShadow(kind))
            {
                GUI.color = new Color(0, 0, 0, 0.7f * previous.a);
                GUI.Label(new Rect(line.x + 1, line.y + 1, line.width, line.height), title, _headText);
            }
            GUI.color = new Color(color.r, color.g, color.b, previous.a);
            GUI.Label(line, title, _headText);
            GUI.color = previous;
        }

        // Room kept at the right of a head for the close button.
        public static float CloseRoom => (UiArt.CloseSize + 6) * Pt;

        static readonly PixelImage[] CloseImages = new PixelImage[4];

        // Where the close button sits: always the head's top right corner (Docs/UiRework.md 4-4 창 머리).
        public static Rect CloseRect(Rect head)
        {
            float size = UiArt.CloseSize * Pt;
            return new Rect(head.xMax - size - 3 * Pt, head.y + (UiArt.HeadBand * Pt - size) / 2, size, size);
        }

        // The close button in a window's head; true when pressed. A finger gets a 44-point target round the small stone.
        // Its name for the playtests is "창 닫기", apart from the [닫기] buttons they already press.
        public static bool CloseButton(Rect head, string label = "창 닫기")
        {
            var shown = CloseRect(head);
            var hit = Controls.UsingTouch
                ? Rect.MinMaxRect(shown.xMax - 44, head.y - FrameEdge, head.xMax + FrameEdge, Mathf.Max(shown.yMax + 4, head.y - FrameEdge + 44))
                : new Rect(shown.x - 2, shown.y - 2, shown.width + 4, shown.height + 4);
            Ui.Probe(label, hit);
            var e = Event.current;
            bool hover = hit.Contains(e.mousePosition) && (!Controls.UsingTouch || Ui.Held);
            bool down = hover && Ui.Held && hit.Contains(Ui.DownAt);
            int index = (hover ? 1 : 0) | (down ? 2 : 0);
            Image(shown.position, CloseImages[index] ??= UiArt.Close(hover, down));
            if (!GUI.Button(hit, GUIContent.none, GUIStyle.none)) return false;
            UiMotion.Cancel();
            return true;
        }

        // A row of small hearts (a merchant's liking, Docs/UiRework.md 4-2), from the right edge leftwards; gives where
        // they start.
        public static float Hearts(Rect head, float right, int hearts, int most)
        {
            var size = IconSize(UiIcons.Heart);
            float x = right - most * (size.x + 1);
            float y = head.y + (UiArt.HeadBand * Pt - size.y) / 2;
            for (int i = 0; i < most; i++)
                Icon(new Rect(x + i * (size.x + 1), y, size.x, size.y), UiIcons.Heart, i < hearts ? new Color(0.9f, 0.25f, 0.3f) : new Color(0.25f, 0.18f, 0.18f));
            return x;
        }

        // ---- Styles for writing over the frames drawn here ----

        static readonly Dictionary<GUIStyle, GUIStyle> BareStyles = new Dictionary<GUIStyle, GUIStyle>();

        // A copy of the style with no backgrounds, its size and colours kept in step with it, for text over a frame
        // drawn here (or a padded area inside one).
        public static GUIStyle Bare(GUIStyle style)
        {
            if (!BareStyles.TryGetValue(style, out var bare))
            {
                bare = new GUIStyle(style);
                foreach (var state in new[] { bare.normal, bare.hover, bare.active, bare.focused, bare.onNormal, bare.onHover, bare.onActive, bare.onFocused })
                {
                    state.background = null;
#if UNITY_EDITOR
                    state.scaledBackgrounds = NoScaled;
#endif
                }
                BareStyles[style] = bare;
            }
            bare.font = style.font;
            bare.fontSize = style.fontSize;
            bare.fontStyle = style.fontStyle;
            bare.alignment = style.alignment;
            bare.wordWrap = style.wordWrap;
            bare.normal.textColor = style.normal.textColor;
            bare.hover.textColor = style.hover.textColor;
            bare.active.textColor = style.active.textColor;
            bare.onNormal.textColor = style.onNormal.textColor;
            return bare;
        }

        // An area inside a panel drawn with Panel: the box's padding, no background.
        public static GUIStyle PanelArea => Bare(GUI.skin.box);

        static GUIStyle _cardText;

        // The writing on a card: the box's padding, wrapped, from the top left.
        public static GUIStyle CardText
        {
            get
            {
                _cardText ??= new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.UpperLeft };
                _cardText.normal.textColor = new Color(0.97f, 0.93f, 0.85f);
                return Bare(_cardText);
            }
        }

        // A group inside a panel set off as a well (the picked item's panel): the well is drawn where the group was last
        // laid out, under it, since IMGUI knows a group's size only once it is drawn. The rect stays the same while the
        // list scrolls (it is in the list's own points), so the well keeps up.
        public static void BeginWell(Rect last)
        {
            Inset(last);
            GUILayout.BeginVertical(PanelArea);
        }

        public static Rect EndWell(Rect last)
        {
            GUILayout.EndVertical();
            return Event.current.type == EventType.Repaint ? GUILayoutUtility.GetLastRect() : last;
        }

        // ---- Tabs (Docs/UiRework.md 4-4 탭 두 단) ----

        // A tab: the chosen one gold and standing two points taller than the rest; its icon beside its name where the
        // tab is wide enough for both. The bag's tabs and the screens' group tabs (ScreenStack.Tabs) both draw with it.
        public static void Tab(Rect tab, GUIContent label, string[] icon, bool chosen, bool hover)
        {
            var face = chosen ? tab : new Rect(tab.x, tab.y + 2, tab.width, tab.height - 2);
            ButtonFrame(face, hover && !chosen, false, chosen, false);
            var style = Bare(GUI.skin.button);
            float text = style.CalcSize(label).x - style.padding.horizontal;
            var iconSize = icon != null ? IconSize(icon) : Vector2.zero;
            bool withIcon = icon != null && face.width >= iconSize.x + 4 + text + 12;
            if (!withIcon)
            {
                style.Draw(face, label, hover, false, chosen, false);
                return;
            }
            float left = face.x + (face.width - iconSize.x - 4 - text) / 2;
            Icon(new Rect(left, face.y, iconSize.x, face.height), icon, chosen ? new Color(1f, 0.92f, 0.62f) : new Color(0.85f, 0.72f, 0.5f));
            style.alignment = TextAnchor.MiddleLeft;
            var padding = style.padding;
            style.padding = new RectOffset(0, 0, padding.top, padding.bottom);
            style.Draw(new Rect(left + iconSize.x + 4, face.y, text + 4, face.height), label, hover, false, chosen, false);
            style.padding = padding;
            style.alignment = TextAnchor.MiddleCenter;
        }

        // ---- The passive tree's night (Docs/UiRework.md 4-3: the one screen unlike the rest) ----

        static Piece _stars;

        // The star field tiled over the rect at whole device pixels, faded to `alpha`, under what the page draws.
        public static void Stars(Rect area, float alpha = 1)
        {
            if (Event.current.type != EventType.Repaint || area.width <= 0 || area.height <= 0) return;
            _stars ??= new Piece(UiArt.StarField(), repeat: true);
            area = Snap(area);
            float tile = UiArt.StarTile * Pt;
            var previous = GUI.color;
            GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * alpha);
            GUI.DrawTextureWithTexCoords(area, _stars.Texture(Dots), new Rect(0, 0, area.width / tile, area.height / tile));
            GUI.color = previous;
        }

        // ---- Portraits (Docs/UiRework.md 4-2 대화 화면, U3) ----

        static readonly Dictionary<(PixelImage, int), Texture2D> Portraits = new Dictionary<(PixelImage, int), Texture2D>();

        // How many device pixels one pixel of a portrait takes at so many points a pixel: a whole number, so its pixels
        // stay square (3 points a pixel on a PC is 3 device pixels; 2 on a phone of ratio 2.625 is 5).
        public static int PortraitDots(float pointsPerPixel) => Mathf.Max(1, Mathf.RoundToInt(pointsPerPixel * Ui.Scale));

        // The size of a portrait at so many points a pixel (its top `rows` rows only when given: the head and shoulders on
        // a low screen).
        public static Vector2 PortraitSize(float pointsPerPixel, int rows = 0)
        {
            float pixel = PortraitDots(pointsPerPixel) / Ui.Scale;
            return new Vector2(PortraitArt.Width * pixel, (rows > 0 ? rows : PortraitArt.Height) * pixel);
        }

        // A person's portrait in its slot: a well filled with their colour, the picture at a whole number of device pixels
        // a pixel, centred across and standing on the slot's bottom (Docs/UiRework.md 4-2). Only the top `rows` rows when
        // given.
        public static void Portrait(Rect slot, PixelImage image, float pointsPerPixel, Color backing, int rows = 0)
        {
            if (Event.current.type != EventType.Repaint) return;
            var previous = GUI.color;
            Inset(slot);
            float edge = UiArt.InsetBorder * Pt;
            var inner = Snap(Rect.MinMaxRect(slot.xMin + edge, slot.yMin + edge, slot.xMax - edge, slot.yMax - edge));
            GUI.color = new Color(backing.r, backing.g, backing.b, 0.55f * previous.a);
            GUI.DrawTexture(inner, Texture2D.whiteTexture);
            GUI.color = previous;
            if (image == null) return;
            int dots = PortraitDots(pointsPerPixel);
            if (!Portraits.TryGetValue((image, dots), out var texture))
            {
                var big = new PixelImage(image.Width * dots, image.Height * dots);
                for (int y = 0; y < big.Height; y++)
                    for (int x = 0; x < big.Width; x++) big[x, y] = image[x / dots, y / dots];
                Portraits[(image, dots)] = texture = Sprites.TextureOf(big);
                texture.wrapMode = TextureWrapMode.Clamp;
            }
            int shown = rows > 0 ? Mathf.Min(rows, image.Height) : image.Height;
            var size = PortraitSize(pointsPerPixel, shown);
            var rect = Snap(new Rect(inner.center.x - size.x / 2, inner.yMax - size.y, size.x, size.y));
            // A texture's rows count from the bottom: the top `shown` rows of the picture.
            float share = shown / (float)image.Height;
            GUI.BeginClip(inner);
            GUI.DrawTextureWithTexCoords(new Rect(rect.x - inner.x, rect.y - inner.y, rect.width, rect.height), texture, new Rect(0, 1 - share, 1, share));
            GUI.EndClip();
        }

        // A small bouncing arrow: more to read (the talk's line is all shown, Docs/UiRework.md 4-2). Still with less motion.
        public static void NextArrow(Vector2 at, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            float bob = UiMotion.Reduced ? 0 : Mathf.Round(Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5)) * 3);
            var previous = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, previous.a);
            for (int row = 0; row < 4; row++)
                GUI.DrawTexture(new Rect(at.x - 4 + row, at.y + bob + row * 1.5f, 8 - row * 2, 1.5f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        // ---- Full screens (Docs/UiRework.md 사람이 정한 것: the world dark behind, red at the edges when hurt) ----

        static Texture2D _edges;

        public static void FullBackdrop(Rect screen)
        {
            var previous = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.7f * previous.a);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
            float hurt = UiMotion.HurtGlow;
            if (hurt > 0)
            {
                _edges ??= Edges();
                GUI.color = new Color(0.9f, 0.08f, 0.06f, hurt * previous.a);
                GUI.DrawTexture(screen, _edges, ScaleMode.StretchToFill);
            }
            GUI.color = previous;
        }

        // White fading in towards the edges, for the red of being hurt.
        static Texture2D Edges()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y)) / (size * 0.22f);
                    float a = Mathf.Clamp01(1 - edge);
                    texture.SetPixel(x, y, new Color(1, 1, 1, a * a));
                }
            }
            texture.Apply();
            return texture;
        }

        // ---- A count that pops when it changes (Docs/UiRework.md 4-6 숫자) ----

        // The text in the rect, a moment larger and gold when the number went up, red when it went down.
        public static void Count(Rect area, string key, int value, string text, GUIStyle style)
        {
            var (scale, tint) = UiMotion.Pop(key, value);
            var previous = GUI.color;
            var matrix = GUI.matrix;
            if (scale != 1) GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), area.center);
            if (tint.a > 0) GUI.color = Color.Lerp(previous, new Color(tint.r, tint.g, tint.b, previous.a), tint.a);
            GUI.Label(area, text, style);
            GUI.color = previous;
            GUI.matrix = matrix;
        }
    }
}
