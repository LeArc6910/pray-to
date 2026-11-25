using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The title's and the menus' look on screen (Docs/TitleRework.md 4-1·4-3, 묶음 T3), with TitleArt's pictures: words
    // with no box (Ui.Look.Text), pictures at whole device pixels, the see-through panel a page lies on, the rows of the
    // character and world lists, a hero in a look, and the words a list writes (how long ago, how long played).
    static class TitleDraw
    {
        // ---- Pictures ----

        static readonly Dictionary<string, PixelImage> Images = new Dictionary<string, PixelImage>();

        // A picture drawn once and kept under its key (the same instance each time, so its texture is kept too).
        public static PixelImage Cached(string key, Func<PixelImage> draw)
        {
            if (Images.TryGetValue(key, out var image)) return image;
            if (Images.Count > 600) Images.Clear();
            image = draw();
            Images[key] = image;
            return image;
        }

        static readonly Dictionary<PixelImage, Texture2D> Textures = new Dictionary<PixelImage, Texture2D>();

        public static Texture2D TextureOf(PixelImage image)
        {
            if (Textures.TryGetValue(image, out var texture) && texture != null) return texture;
            if (Textures.Count > 600)
            {
                foreach (var old in Textures.Values) if (old != null) UnityEngine.Object.Destroy(old);
                Textures.Clear();
            }
            texture = Sprites.TextureOf(image);
            Textures[image] = texture;
            return texture;
        }

        // How big a picture is at about so many points a pixel, rounded to whole device pixels a pixel so its pixels stay
        // square.
        public static float PixelPoints(float pointsPerPixel) => Mathf.Max(1, Mathf.Round(pointsPerPixel * Ui.Scale)) / Ui.Scale;

        public static Vector2 PictureSize(PixelImage image, float pointsPerPixel)
        {
            float pixel = PixelPoints(pointsPerPixel);
            return new Vector2(image.Width * pixel, image.Height * pixel);
        }

        // The picture with its top left at the point, at about so many points a pixel.
        public static void Picture(Vector2 at, PixelImage image, float pointsPerPixel, float alpha = 1)
        {
            if (image == null || Event.current.type != EventType.Repaint) return;
            var size = PictureSize(image, pointsPerPixel);
            var rect = UiSkin.Snap(new Rect(at.x, at.y, size.x, size.y));
            var previous = GUI.color;
            if (alpha < 1) GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * alpha);
            GUI.DrawTexture(rect, TextureOf(image));
            GUI.color = previous;
        }

        public static void PictureCentred(Vector2 center, PixelImage image, float pointsPerPixel, float alpha = 1)
        {
            if (image == null) return;
            var size = PictureSize(image, pointsPerPixel);
            Picture(new Vector2(Mathf.Round(center.x - size.x / 2), Mathf.Round(center.y - size.y / 2)), image, pointsPerPixel, alpha);
        }

        public static Color ToColor(Rgba c) => new Color32(c.R, c.G, c.B, c.A);

        // ---- Words with no box (Ui.Look.Text) ----

        static GUIStyle _ink;
        static readonly GUIContent Words = new GUIContent();

        public static TitleState StateOf(bool enabled, bool hover, bool down, bool chosen) =>
            !enabled ? TitleState.Normal : down ? TitleState.Pressed : hover ? TitleState.Hover : chosen ? TitleState.Selected : TitleState.Normal;

        // A text item: its words in the state's ink over a shadow, a little bigger when pointed at or picked, a gold mark
        // at each side and a strip of light under them. The style gives the size and where in the rect the words sit.
        public static void TextItem(Rect rect, string text, GUIStyle style, bool enabled, bool hover, bool down, bool chosen)
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text)) return;
            var state = StateOf(enabled, hover, down, chosen);
            _ink ??= new GUIStyle(GUI.skin.label) { wordWrap = false, clipping = TextClipping.Overflow, padding = new RectOffset(), margin = new RectOffset() };
            _ink.font = style.font;
            _ink.fontSize = style.fontSize;
            _ink.fontStyle = style.fontStyle;
            _ink.alignment = TextAnchor.UpperLeft;
            Words.text = text;
            var size = _ink.CalcSize(Words);
            var words = Place(rect, size, style.alignment, style.padding);
            float grow = UiMotion.Reduced ? 1 : state == TitleState.Hover ? TitleArt.GrowHover : state == TitleState.Selected ? TitleArt.GrowSelected : 1;
            var matrix = GUI.matrix;
            if (grow != 1) GUIUtility.ScaleAroundPivot(new Vector2(grow, grow), words.center);
            float drop = Mathf.Max(1, UiSkin.Pt / 2);
            _ink.normal.textColor = ToColor(TitleArt.InkShadow);
            GUI.Label(new Rect(words.x + drop, words.y + drop, words.width, words.height), text, _ink);
            _ink.normal.textColor = ToColor(enabled ? TitleArt.Ink(state) : TitleArt.InkOff);
            GUI.Label(words, text, _ink);
            GUI.matrix = matrix;
            if (state == TitleState.Normal) return;
            // The marks beside the words and the strip under them, outside the grown words.
            var mark = Cached("title/mark/" + state, () => TitleArt.Mark(state));
            float pt = UiSkin.Pt;
            float half = words.width * grow / 2, gap = 6;
            float markSize = TitleArt.MarkSize * pt;
            float markY = Mathf.Round(words.center.y - markSize / 2);
            // Only when both marks stay inside the item's own rect, so a narrow cell (the hub's) never spills onto the next.
            if (words.center.x - half - gap - markSize >= rect.x - 2 && words.center.x + half + gap + markSize <= rect.xMax + 2)
            {
                UiSkin.Image(new Vector2(Mathf.Round(words.center.x - half - gap - markSize), markY), mark);
                UiSkin.Image(new Vector2(Mathf.Round(words.center.x + half + gap), markY), mark);
            }
            int stripWidth = Mathf.Max(8, Mathf.RoundToInt((words.width * grow + 2 * gap) / pt));
            var strip = Cached($"title/strip/{stripWidth}/{state}", () => TitleArt.Strip(stripWidth, state));
            float stripY = Mathf.Min(rect.yMax - TitleArt.StripHeight * pt, Mathf.Round(words.center.y + words.height * grow / 2 - pt));
            if (strip != null) UiSkin.Image(new Vector2(Mathf.Round(words.center.x - stripWidth * pt / 2), stripY), strip);
        }

        // Where words of that size sit in the rect by the alignment and padding.
        static Rect Place(Rect rect, Vector2 size, TextAnchor anchor, RectOffset padding)
        {
            int column = (int)anchor % 3, line = (int)anchor / 3;
            float x = column == 0 ? rect.x + padding.left : column == 1 ? rect.center.x - size.x / 2 : rect.xMax - padding.right - size.x;
            float y = line == 0 ? rect.y + padding.top : line == 1 ? rect.center.y - size.y / 2 : rect.yMax - padding.bottom - size.y;
            return new Rect(Mathf.Round(x), Mathf.Round(y), size.x, size.y);
        }

        // ---- Words written on a page ----

        static GUIStyle _plain;

        // Plain words in a colour over a faint shadow (a name, a line under it), cut at the rect's right.
        public static void Text(Rect rect, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool shadow = true)
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text)) return;
            _plain ??= new GUIStyle(GUI.skin.label) { wordWrap = false, clipping = TextClipping.Clip, padding = new RectOffset(), margin = new RectOffset() };
            Ui.Text(_plain, size);
            _plain.alignment = anchor;
            text = Fit(text, rect.width);
            if (shadow)
            {
                _plain.normal.textColor = new Color(0, 0, 0, 0.75f * color.a);
                GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, _plain);
            }
            _plain.normal.textColor = color;
            GUI.Label(rect, text, _plain);
        }

        // The words cut to the width with an ellipsis rather than mid-letter at the rect's edge (_plain sized already).
        static string Fit(string text, float width)
        {
            Words.text = text;
            if (_plain.CalcSize(Words).x <= width) return text;
            for (int length = text.Length - 1; length > 0; length--)
            {
                Words.text = text.Substring(0, length).TrimEnd() + "…";
                if (_plain.CalcSize(Words).x <= width) return Words.text;
            }
            return "…";
        }

        public static float TextWidth(string text, int size)
        {
            _plain ??= new GUIStyle(GUI.skin.label) { wordWrap = false, clipping = TextClipping.Clip, padding = new RectOffset(), margin = new RectOffset() };
            Ui.Text(_plain, size);
            Words.text = text;
            return _plain.CalcSize(Words).x;
        }

        public static readonly Color Ink = new Color(0.94f, 0.9f, 0.84f);
        public static readonly Color Faint = new Color(0.72f, 0.66f, 0.58f);
        public static readonly Color GoldInk = new Color(0.96f, 0.84f, 0.55f);
        public static readonly Color Warn = new Color(1f, 0.62f, 0.5f);

        // A page's name over its rule (TitleArt.Divider), in the middle of the rect.
        public static void Heading(Rect rect, string text)
        {
            if (Event.current.type != EventType.Repaint) return;
            // Smaller when the big words would run into [뒤로] at the head's left.
            int size = Ui.Short || TextWidth(text, 18) > rect.width - 180 ? 15 : 18;
            Text(new Rect(rect.x, rect.y, rect.width, rect.height - 6), text, size, GoldInk, TextAnchor.MiddleCenter);
            float pt = UiSkin.Pt;
            int width = Mathf.Max(20, Mathf.RoundToInt(Mathf.Min(rect.width - 24, 360) / pt));
            var rule = Cached("title/divider/" + width, () => TitleArt.Divider(width));
            UiSkin.Image(new Vector2(Mathf.Round(rect.center.x - width * pt / 2), Mathf.Round(rect.yMax - TitleArt.DividerHeight * pt)), rule);
        }

        static Texture2D _halo;

        // A soft dark cloud behind words laid straight on the cave (the title's column), so they read over a bright prop.
        public static void Halo(Rect area, float strength = 0.55f)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (_halo == null)
            {
                const int size = 64;
                _halo = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2 - 1, dy = (y + 0.5f) / size * 2 - 1;
                        float t = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy));
                        _halo.SetPixel(x, y, new Color(0.02f, 0.015f, 0.025f, Mathf.SmoothStep(0, 1, t)));
                    }
                _halo.Apply();
            }
            var previous = GUI.color;
            GUI.color = new Color(1, 1, 1, strength);
            GUI.DrawTexture(area, _halo, ScaleMode.StretchToFill);
            GUI.color = previous;
        }

        // ---- The panel and the rows ----

        public static void Panel(Rect area) => UiSkin.Sliced(area, TitleArt.Panel, TitleArt.PanelBorder);

        public static void Row(Rect area, TitleState state) =>
            UiSkin.Sliced(area, Cached("title/row/" + state, () => TitleArt.Row(state)), TitleArt.RowBorder);

        // The dashed row that makes a new one, drawn to its size, a plus and its words in the middle.
        public static void NewRow(Rect area, string text, TitleState state)
        {
            if (Event.current.type != EventType.Repaint) return;
            float pt = UiSkin.Pt;
            int w = Mathf.Max(12, Mathf.RoundToInt(area.width / pt)), h = Mathf.Max(8, Mathf.RoundToInt(area.height / pt));
            UiSkin.Image(area.position, Cached($"title/new/{w}/{h}/{state}", () => TitleArt.NewRow(w, h, state)));
            var plus = Cached("title/plus/" + state, () => TitleArt.Plus(state));
            float textWidth = TextWidth(text, 13);
            float plusSize = TitleArt.PlusSize * pt;
            float left = Mathf.Round(area.center.x - (plusSize + 8 + textWidth) / 2);
            UiSkin.Image(new Vector2(left, Mathf.Round(area.center.y - plusSize / 2)), plus);
            Text(new Rect(left + plusSize + 8, area.y, textWidth + 4, area.height), text, 13, ToColor(TitleArt.Ink(state)));
        }

        // A dark see-through veil over everything (behind a question), and a small panel in the middle for it.
        public static void Veil(Rect screen, float alpha = 0.6f)
        {
            if (Event.current.type != EventType.Repaint) return;
            var previous = GUI.color;
            GUI.color = new Color(0, 0, 0, alpha);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        // ---- A hero in a look ----

        // The hero's eight ways and whether each is the picture mirrored, turning round (the making screen's preview).
        static readonly (Look Look, bool Flip)[] Ways =
        {
            (Look.Down, false), (Look.DownSide, false), (Look.Side, false), (Look.UpSide, false),
            (Look.Up, false), (Look.UpSide, true), (Look.Side, true), (Look.DownSide, true),
        };

        public const int HeroWidth = 22, HeroHeight = 24;

        // The figure in that look, way and frame, cut from the hero's canvas with a little room round it (arms swing).
        public static PixelImage Hero(CharacterLook look, int way, bool walking, int frame)
        {
            var (facing, flip) = Ways[(way % Ways.Length + Ways.Length) % Ways.Length];
            var move = walking ? HeroMove.Walk : HeroMove.Stand;
            int frames = HeroArt.FramesOf(move, HeroWeapon.None);
            frame = (frame % frames + frames) % frames;
            return Cached($"title/hero/{HeroArt.Key(look)}/{facing}/{flip}/{move}/{frame}", () =>
            {
                var canvas = HeroArt.Draw(facing, move, frame, HeroGear.None, look);
                var cut = new PixelImage(HeroWidth, HeroHeight);
                int left = HeroArt.FeetX - HeroWidth / 2, top = HeroArt.FeetY - HeroHeight + 2;
                for (int y = 0; y < HeroHeight; y++)
                    for (int x = 0; x < HeroWidth; x++)
                        if (canvas.Inside(left + x, top + y)) cut[x, y] = canvas[left + x, top + y];
                return flip ? cut.FlippedX() : cut;
            });
        }

        // The hero standing on the slot's bottom middle: walking toward the viewer unless motion is reduced; turning
        // round through the eight ways when asked (the making screen).
        public static void HeroIn(Rect slot, CharacterLook look, float pointsPerPixel, bool turning = false, float phase = 0)
        {
            if (Event.current.type != EventType.Repaint) return;
            bool still = Settings.ReduceMotion;
            float time = Time.unscaledTime + phase;
            int frame = still ? 0 : Mathf.FloorToInt(time * 10);
            int way = turning && !still ? Mathf.FloorToInt(time / 1.4f) : 0;
            var image = Hero(look, way, !still, frame);
            var size = PictureSize(image, pointsPerPixel);
            Picture(new Vector2(Mathf.Round(slot.center.x - size.x / 2), Mathf.Round(slot.yMax - size.y)), image, pointsPerPixel);
        }

        // ---- A field to type in ----

        static GUIStyle _hint, _field;

        // A text field, known to the playtests by its name (they tap it as a person would), with a faint hint in it while
        // it is empty. Its words sit in the middle of its height (a phone's taller field had them at the top), and in a
        // web build on a PC Korean is typed through the page's own box (WebText).
        public static string Field(Rect rect, string text, int max, string probe, string hint = "")
        {
            Ui.Probe(probe, rect);
            var skinField = GUI.skin.textField;
            if (_field == null || _field.font != skinField.font) _field = new GUIStyle(skinField) { alignment = TextAnchor.MiddleLeft };
            _field.fontSize = skinField.fontSize;
            string typed = WebText.Field(rect, text, max, probe, _field);
            if (string.IsNullOrEmpty(typed) && hint.Length > 0 && Event.current.type == EventType.Repaint)
            {
                _hint ??= new GUIStyle(GUI.skin.label) { wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft };
                Ui.Text(_hint, 12);
                _hint.normal.textColor = new Color(0.62f, 0.56f, 0.5f, 0.8f);
                GUI.Label(new Rect(rect.x + 8, rect.y, rect.width - 12, rect.height), hint, _hint);
            }
            return typed;
        }

        // ---- What a list writes ----

        // When last played, as a day: "오늘", "어제", "3일 전"; nothing known for an old save.
        public static string Ago(long unixSeconds)
        {
            if (unixSeconds <= 0) return Strings.T("title.ago_unknown", "언제인지 모름");
            var then = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime.Date;
            int days = (DateTime.Now.Date - then).Days;
            if (days <= 0) return Strings.T("title.today", "오늘");
            if (days == 1) return Strings.T("title.yesterday", "어제");
            return string.Format(Strings.T("title.days_ago", "{0}일 전"), days);
        }

        // How long played: "2시간 5분", "12분", "1분 안".
        public static string Played(double seconds)
        {
            int minutes = (int)(seconds / 60);
            if (minutes < 1) return Strings.T("title.played_none", "1분 안");
            if (minutes < 60) return string.Format(Strings.T("title.minutes", "{0}분"), minutes);
            return string.Format(Strings.T("title.hours", "{0}시간 {1}분"), minutes / 60, minutes % 60);
        }

        // A biome's name for a row and the loading line.
        public static string BiomeName(Biome biome) => biome switch
        {
            Biome.Mushroom => Strings.T("biome.mushroom", "버섯 숲"),
            Biome.Ice => Strings.T("biome.ice", "얼음 동굴"),
            Biome.Sand => Strings.T("biome.sand", "모래 동굴"),
            Biome.Crystal => Strings.T("biome.crystal", "수정 동굴"),
            Biome.Lake => Strings.T("biome.lake", "지하 호수"),
            Biome.Lava => Strings.T("biome.lava", "용암 지대"),
            Biome.Abyss => Strings.T("biome.abyss", "공허의 심연"),
            _ => Strings.T("biome.cave", "흙 동굴"),
        };
    }
}
