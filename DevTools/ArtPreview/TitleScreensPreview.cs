using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;

namespace Game.Client
{
    // The title rework's screens as the game lays them out (묶음 T3, Docs/TitleRework.md, --only title, out/title/screens_*):
    // the title over its drifting cave, the character list, the making page, the world list, the loading screen and the
    // hub, on a PC (1280×720) and a phone held upright (390×844 at two device pixels a point). The preview tool has no
    // Korean font, so the words are the English sheet's in the mock font (capitals); the pieces, their sizes and places
    // are the screens' (GameRoot.Title.cs, GameRoot.Loading.cs, ScreenStack.DrawHub, TitleDraw, TitleBackdrop).
    static class TitleScreensPreview
    {
        // A screen being drawn: its size in device pixels, device pixels a point, and device pixels a pixel of the UI art.
        sealed class Screen
        {
            public Screen(int width, int height, float scale)
            {
                Scale = scale;
                Image = new PixelImage((int)(width * scale), (int)(height * scale));
                Dots = Math.Max(1, (int)MathF.Round(2 * scale));
                Width = width;
                Height = height;
                Touch = scale > 1;
            }

            public PixelImage Image { get; }
            public float Scale { get; }
            public int Dots { get; }
            // In points.
            public int Width { get; }
            public int Height { get; }
            public bool Touch { get; }
            public bool Short => Height < 480;

            public int P(float points) => (int)MathF.Round(points * Scale);
        }

        static readonly Rgba Ink = Rgba.Hex("#F0E6D6"), Faint = Rgba.Hex("#B8A894"), Gold = Rgba.Hex("#F5D68C"), Warn = Rgba.Hex("#FF9E80");

        static readonly CharacterLook[] Looks =
        {
            new CharacterLook(2, 2, 1, 2, 3, 1), new CharacterLook(5, 7, 3, 3, 1, 4), new CharacterLook(7, 1, 0, 5, 9, 6),
        };

        public static void Write(string folder)
        {
            foreach (var (name, width, height, scale) in new[] { ("pc", 1280, 720, 1f), ("phone", 390, 844, 2f), ("phonewide", 844, 390, 2f) })
            {
                var shots = new List<PixelImage>
                {
                    Title(new Screen(width, height, scale)),
                    Characters(new Screen(width, height, scale)),
                    Make(new Screen(width, height, scale)),
                    Worlds(new Screen(width, height, scale)),
                    Loading(new Screen(width, height, scale)),
                    Hub(new Screen(width, height, scale)),
                };
                string[] names = { "title", "characters", "make", "worlds", "loading", "hub" };
                for (int i = 0; i < shots.Count; i++) Png.Save(shots[i], Path.Combine(folder, $"screen_{name}_{names[i]}.png"));
                Png.Save(Sheet(shots, name == "phone" ? 6 : 3), Path.Combine(folder, $"screens_{name}.png"));
            }
        }

        // The screens side by side at half their size.
        static PixelImage Sheet(List<PixelImage> shots, int columns)
        {
            int w = shots[0].Width / 2, h = shots[0].Height / 2;
            int rows = (shots.Count + columns - 1) / columns;
            var sheet = new PixelImage(columns * (w + 8) + 8, rows * (h + 8) + 8);
            sheet.Fill(0, 0, sheet.Width, sheet.Height, Rgba.Hex("#222026"));
            for (int i = 0; i < shots.Count; i++)
            {
                var shot = shots[i];
                int left = 8 + i % columns * (w + 8), top = 8 + i / columns * (h + 8);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++) sheet[left + x, top + y] = shot[x * 2, y * 2];
            }
            return sheet;
        }

        // ---- Pieces ----

        static void Art(Screen s, PixelImage image, float x, float y, int dots)
        {
            if (image == null) return;
            int left = s.P(x), top = s.P(y);
            for (int py = 0; py < image.Height * dots; py++)
                for (int px = 0; px < image.Width * dots; px++)
                {
                    var c = image[px / dots, py / dots];
                    if (c.A > 0 && s.Image.Inside(left + px, top + py)) s.Image.Blend(left + px, top + py, c);
                }
        }

        static void ArtCentred(Screen s, PixelImage image, float cx, float cy, int dots) =>
            Art(s, image, cx - image.Width * dots / s.Scale / 2, cy - image.Height * dots / s.Scale / 2, dots);

        static void Nine(Screen s, PixelImage piece, int border, float x, float y, float w, float h)
        {
            var big = piece.Times(s.Dots);
            int b = border * s.Dots;
            int left = s.P(x), top = s.P(y), width = s.P(w), height = s.P(h);
            for (int py = 0; py < height; py++)
            {
                int sy = py < b ? py : py >= height - b ? big.Height - (height - py) : b + (py - b) % Math.Max(1, big.Height - b * 2);
                for (int px = 0; px < width; px++)
                {
                    int sx = px < b ? px : px >= width - b ? big.Width - (width - px) : b + (px - b) % Math.Max(1, big.Width - b * 2);
                    if (s.Image.Inside(left + px, top + py)) s.Image.Blend(left + px, top + py, big[sx, sy]);
                }
            }
        }

        static int FontScale(Screen s, float size) => Math.Max(1, (int)MathF.Round(size * s.Scale / 7.5f));

        static float TextWidth(Screen s, string text, float size) => MockFont.Width(text, FontScale(s, size)) / s.Scale;

        // Words at the point (their top left), or centred on x when asked.
        static void Words(Screen s, float x, float y, string text, Rgba color, float size = 13, bool centre = false)
        {
            int scale = FontScale(s, size);
            int width = MockFont.Width(text, scale);
            int left = centre ? s.P(x) - width / 2 : s.P(x);
            int top = s.P(y) + (s.P(size * 1.2f) - 7 * scale) / 2;
            MockFont.Draw(s.Image, left + scale, top + scale, text, Rgba.Hex("#000000").WithAlpha(200), scale);
            MockFont.Draw(s.Image, left, top, text, color, scale);
        }

        // A text item as TitleDraw.TextItem draws it: the words, and when lit the marks and the strip.
        static void TextItem(Screen s, float cx, float cy, string text, TitleState state, float size)
        {
            float width = TextWidth(s, text, size);
            float h = size * 1.2f;
            Words(s, cx, cy - h / 2, text, TitleArt.Ink(state), size, true);
            if (state == TitleState.Normal) return;
            var mark = TitleArt.Mark(state);
            float markSize = TitleArt.MarkSize * s.Dots / s.Scale;
            Art(s, mark, cx - width / 2 - 6 - markSize, cy - markSize / 2, s.Dots);
            Art(s, mark, cx + width / 2 + 6, cy - markSize / 2, s.Dots);
            int stripWidth = (int)((width + 12) * s.Scale / s.Dots);
            Art(s, TitleArt.Strip(stripWidth, state), cx - stripWidth * s.Dots / s.Scale / 2, cy + h / 2 - 1, s.Dots);
        }

        // The drifting cave behind the title (TitleBackdrop): caves side by side, whole device pixels a pixel filling the
        // height, the window some way along, dark at the top and bottom, darker again behind a page.
        static void Backdrop(Screen s, float dim, Biome[] biomes)
        {
            const int caveWidth = 256, height = 144;
            int count = Math.Max(2, biomes.Length);
            var strip = new PixelImage(caveWidth * count, height);
            for (int i = 0; i < count; i++) strip.Draw(LoadingArt.Scene(biomes[i % biomes.Length], 11 + i * 7, new (string, Rgba)[0], null, caveWidth, height), i * caveWidth, 0);
            var g = s.Image;
            int dots = (int)Math.Ceiling(g.Height / (float)height);
            int viewHeight = g.Height / dots;
            int offsetY = (height - viewHeight) / 2;
            int offsetX = 180;
            for (int y = 0; y < g.Height; y++)
            {
                float t = (y + 0.5f) / g.Height;
                float edge = MathF.Max(Math.Clamp(1 - t / 0.32f, 0, 1), Math.Clamp((t - 0.7f) / 0.3f, 0, 1));
                float shade = 0.12f + 0.78f * edge * edge;
                for (int x = 0; x < g.Width; x++)
                {
                    var c = strip[(offsetX + x / dots) % strip.Width, Math.Min(height - 1, offsetY + y / dots)];
                    g[x, y] = c.Scale((1 - shade) * (1 - dim)).WithAlpha(255);
                }
            }
        }

        // TitleDraw.Halo: a soft dark cloud, darkest in its middle.
        static void Halo(Screen s, float x, float y, float w, float h, float strength)
        {
            int left = s.P(x), top = s.P(y), width = s.P(w), height = s.P(h);
            for (int py = 0; py < height; py++)
                for (int px = 0; px < width; px++)
                {
                    float dx = (px + 0.5f) / width * 2 - 1, dy = (py + 0.5f) / height * 2 - 1;
                    float t = Math.Clamp(1 - MathF.Sqrt(dx * dx + dy * dy), 0, 1);
                    float a = t * t * (3 - 2 * t) * strength;
                    if (s.Image.Inside(left + px, top + py)) s.Image[left + px, top + py] = s.Image[left + px, top + py].Scale(1 - a);
                }
        }

        static void Panel(Screen s, float x, float y, float w, float h) => Nine(s, TitleArt.Panel, TitleArt.PanelBorder, x, y, w, h);

        // A page: the panel in the middle, its name over the rule, [BACK] at the head's left. Returns the body's rect.
        static (float X, float Y, float W, float H) Page(Screen s, string name, float width, float height, float foot)
        {
            width = MathF.Min(width, s.Width - (s.Width < 480 ? 8 : 24));
            height = MathF.Min(height, s.Height - (s.Short ? 6 : 20));
            float x = MathF.Round((s.Width - width) / 2), y = MathF.Round((s.Height - height) / 2);
            Panel(s, x, y, width, height);
            float edge = (TitleArt.PanelBorder + 2) * s.Dots / s.Scale;
            float head = s.Short ? 34 : 44;
            float hx = x + edge, hy = y + edge - 4, hw = width - 2 * edge;
            Words(s, hx + hw / 2, hy + (head - 6 - 22) / 2, name, Gold, s.Short ? 15 : 18, true);
            int ruleWidth = (int)(MathF.Min(hw - 24, 360) * s.Scale / s.Dots);
            Art(s, TitleArt.Divider(ruleWidth), hx + hw / 2 - ruleWidth * s.Dots / s.Scale / 2, hy + head - TitleArt.DividerHeight * s.Dots / s.Scale, s.Dots);
            TextItem(s, hx - 4 + 38, hy + 2 + (head - 10) / 2, "BACK", TitleState.Normal, 13);
            float top = hy + head + 4;
            return (hx, top, hw - 14, y + height - edge - foot - top);
        }

        static float RowHeight(Screen s) => s.Touch ? 44 : 32;
        static float ListRow(Screen s) => s.Touch ? 66 : 60;

        static void Hero(Screen s, CharacterLook look, float cx, float bottom, int dots, Look facing = Look.Down, int frame = 2)
        {
            var canvas = HeroArt.Draw(facing, HeroMove.Walk, frame, HeroGear.None, look);
            var cut = new PixelImage(22, 24);
            for (int y = 0; y < 24; y++)
                for (int x = 0; x < 22; x++)
                    if (canvas.Inside(HeroArt.FeetX - 11 + x, HeroArt.FeetY - 22 + y)) cut[x, y] = canvas[HeroArt.FeetX - 11 + x, HeroArt.FeetY - 22 + y];
            Art(s, cut, cx - 11f * dots / s.Scale, bottom - 24f * dots / s.Scale, dots);
        }

        static void Delete(Screen s, float right, float cy) => TextItem(s, right - (s.Touch ? 35 : 31), cy, "DELETE", TitleState.Normal, 13);

        // ---- The screens ----

        static PixelImage Title(Screen s)
        {
            Backdrop(s, 0, new[] { Biome.Cave });
            string[] items = { "START GAME", "PLAY TOGETHER", "SAVES", "SETTINGS", "SEND FEEDBACK" };
            float itemHeight = s.Touch ? 44 : s.Short ? 32 : 38;
            float itemWidth = MathF.Min(300, s.Width - 32);
            bool side = s.Short && s.Width >= s.Height * 1.5f;
            float target = side ? s.Width * 0.4f : MathF.Min(s.Width - 48, 440);
            int logoDots = Math.Max(1, (int)MathF.Floor(target * s.Scale / TitleArt.LogoWidth));
            float logoW = TitleArt.LogoWidth * logoDots / s.Scale, logoH = TitleArt.LogoHeight * logoDots / s.Scale;
            float column = items.Length * itemHeight, gap = s.Short ? 10 : 30;
            float lx, ly, ix, iy;
            if (side)
            {
                float half = s.Width / 2f;
                ly = MathF.Round((s.Height - logoH - 22) / 2) - 10;
                lx = MathF.Round(half / 2 - logoW / 2);
                ix = MathF.Round(half + (half - itemWidth) / 2);
                iy = MathF.Round((s.Height - column) / 2);
            }
            else
            {
                float block = logoH + 22 + gap + column;
                ly = MathF.Max(8, MathF.Round((s.Height - block) / 2 - (s.Short ? 0 : 24)));
                lx = MathF.Round((s.Width - logoW) / 2);
                ix = MathF.Round((s.Width - itemWidth) / 2);
                iy = ly + logoH + 22 + gap;
            }
            Halo(s, ix - 70, iy - 30, itemWidth + 140, column + 60, 0.55f);
            Art(s, TitleArt.LogoWithGlint(4), lx, ly, logoDots);
            Words(s, lx + logoW / 2, ly + logoH + 2, "BENEATH THE CAVES, THE TALE OF A FALLEN KINGDOM", Faint, 13, true);
            for (int i = 0; i < items.Length; i++)
                TextItem(s, ix + itemWidth / 2, iy + i * itemHeight + itemHeight / 2, items[i], i == 0 ? TitleState.Selected : i == 3 ? TitleState.Hover : TitleState.Normal, 18);
            Words(s, s.Width - 8 - TextWidth(s, "VERSION 0.108.0", 12), s.Height - 22, "VERSION 0.108.0", Faint, 12);
            return s.Image;
        }

        static PixelImage Characters(Screen s)
        {
            Backdrop(s, 0.3f, new[] { Biome.Cave, Biome.Mushroom, Biome.Ice });
            var body = Page(s, "PICK A CHARACTER", 560, 640, 0);
            float y = body.Y + 4;
            string[] names = { "ARIN", "BORA", "CHUL" };
            string[] lines = { "LV 12 - WARRIOR + FIRE MAGE", "LV 4 - STORM CALLER", "LV 1" };
            string[] more = { "3 H 12 MIN - TODAY", "48 MIN - YESTERDAY", "UNDER A MINUTE - 6 DAYS AGO" };
            Biome[] deepest = { Biome.Ice, Biome.Mushroom, Biome.Cave };
            for (int i = 0; i < 3; i++)
            {
                float h = ListRow(s);
                var state = i == 0 ? TitleState.Hover : TitleState.Normal;
                Nine(s, TitleArt.Row(state), TitleArt.RowBorder, body.X, y, body.W, h);
                Hero(s, Looks[i], body.X + 6 + 22, y + h - 4, s.Dots, Look.Down, i * 2);
                float x = body.X + 58, line = MathF.Floor((h - 6) / 3);
                Words(s, x, y + 3, names[i], state == TitleState.Normal ? Ink : Gold, 13);
                Words(s, x, y + 3 + line, lines[i], Faint, 12);
                Words(s, x, y + 3 + line * 2, more[i], Faint, 12);
                float right = body.X + body.W - (s.Touch ? 70 : 62) - 40;
                ArtCentred(s, TitleArt.BiomeIcon(deepest[i]), right + 20, y + h / 2, s.Dots);
                Delete(s, body.X + body.W, y + h / 2);
                y += h + 4;
            }
            float nh = s.Touch ? 48 : 40;
            int nw = (int)(body.W * s.Scale / s.Dots), nhp = (int)(nh * s.Scale / s.Dots);
            Art(s, TitleArt.NewRow(nw, nhp, TitleState.Normal), body.X, y + 2, s.Dots);
            float plus = TitleArt.PlusSize * s.Dots / s.Scale;
            float tw = TextWidth(s, "NEW CHARACTER", 13);
            float left = body.X + body.W / 2 - (plus + 8 + tw) / 2;
            Art(s, TitleArt.Plus(TitleState.Normal), left, y + 2 + nh / 2 - plus / 2, s.Dots);
            Words(s, left + plus + 8, y + 2 + nh / 2 - 8, "NEW CHARACTER", TitleArt.Ink(TitleState.Normal), 13);
            return s.Image;
        }

        static PixelImage Make(Screen s)
        {
            Backdrop(s, 0.3f, new[] { Biome.Cave });
            float foot = s.Touch ? 48 : 42;
            var body = Page(s, "NEW CHARACTER", 660, 640, foot + 6);
            var look = new CharacterLook(6, 3, 2, 2, 4, 1);
            bool wide = body.W >= 520;
            float row = RowHeight(s), y = body.Y + 4, x = body.X, width = body.W;
            if (wide)
            {
                Nine(s, TitleArt.Row(TitleState.Normal), TitleArt.RowBorder, body.X, y, 200, 260);
                Hero(s, look, body.X + 100, y + 250, (int)MathF.Round(5 * s.Scale), Look.DownSide, 3);
                x = body.X + 212;
                width = body.W - 212;
            }
            else
            {
                float h = s.Short ? 92 : 124;
                Nine(s, TitleArt.Row(TitleState.Normal), TitleArt.RowBorder, body.X, y, body.W, h);
                Hero(s, look, body.X + body.W / 2, y + h - 10, (int)MathF.Round((s.Short ? 3 : 4) * s.Scale), Look.DownSide, 3);
                y += h;
            }
            float label = MathF.Min(96, width * 0.3f);
            Words(s, x, y + row / 2 - 8, "NAME", Ink, 13);
            Nine(s, UiArt.Inset, UiArt.InsetBorder, x + label, y + 3, width - label, row - 6);
            Words(s, x + label + 8, y + row / 2 - 8, "ADVENTURER", Ink, 12);
            y += row + 4;
            string[] parts = { "HAIR", "HAIR COLOUR", "SKIN", "EYES", "SHIRT", "TROUSERS" };
            string[] values = { "BUN", "RED", "TANNED", "BLUE", "PURPLE", "NAVY" };
            Rgba?[] swatches = { null, HeroArt.HairColor(3), HeroArt.SkinColor(2), HeroArt.EyeColor(2), HeroArt.ShirtColor(4), HeroArt.PantsColor(1) };
            float arrow = s.Touch ? 44 : 34;
            for (int i = 0; i < parts.Length; i++)
            {
                Words(s, x, y + row / 2 - 8, parts[i], Ink, 13);
                var state = i == 1 ? TitleState.Hover : TitleState.Normal;
                ArtCentred(s, TitleArt.Arrow(true, state), x + label + arrow / 2, y + row / 2, s.Dots);
                ArtCentred(s, TitleArt.Arrow(false, TitleState.Normal), x + width - arrow / 2, y + row / 2, s.Dots);
                float mid = (x + label + arrow + x + width - arrow) / 2;
                float tw = TextWidth(s, values[i], 13);
                float start = mid - (tw + (swatches[i] != null ? 20 : 0)) / 2;
                if (swatches[i] is Rgba c)
                {
                    s.Image.Fill(s.P(start) - 1, s.P(y + row / 2 - 7) - 1, s.P(14) + 2, s.P(14) + 2, Rgba.Hex("#000000"));
                    s.Image.Fill(s.P(start), s.P(y + row / 2 - 7), s.P(14), s.P(14), c);
                    start += 20;
                }
                Words(s, start, y + row / 2 - 8, values[i], Gold, 13);
                y += row;
            }
            float fy = body.Y + body.H + 6, half = MathF.Min(220, (body.W - 12) / 2);
            TextItem(s, body.X + body.W / 2 - 6 - half / 2, fy + foot / 2, "DICE", TitleState.Normal, 15);
            Nine(s, UiArt.Button(chosen: true), UiArt.ButtonBorder, body.X + body.W / 2 + 6, fy, half, foot - 4);
            Words(s, body.X + body.W / 2 + 6 + half / 2, fy + (foot - 4) / 2 - 9, "MAKE", Ink, 15, true);
            return s.Image;
        }

        static PixelImage Worlds(Screen s)
        {
            Backdrop(s, 0.3f, new[] { Biome.Cave, Biome.Mushroom });
            float foot = s.Touch ? 48 : 42;
            var body = Page(s, "PICK A WORLD", 560, 640, foot + 6);
            float y = body.Y + 4;
            Hero(s, Looks[0], body.X + 22, y + 42, s.Dots);
            Words(s, body.X + 50, y + 14, "ARIN - LV 12", Gold, 13);
            y += 46;
            Words(s, body.X + 2, y, "ARIN'S CLASSES: WARRIOR + FIRE MAGE", Faint, 13);
            y += 24;
            string[] names = { "SLEEPING MINE", "DEEP LAKE", "OLD ROOTS" };
            string[] lines = { "DAY 14 - 5 H 2 MIN", "DAY 3 - 40 MIN", "DAY 1 - UNDER A MINUTE" };
            string[] more = { "TODAY - BEEN HERE", "3 DAYS AGO", "12 DAYS AGO" };
            Biome[] deepest = { Biome.Ice, Biome.Mushroom, Biome.Cave };
            for (int i = 0; i < 3; i++)
            {
                float h = ListRow(s);
                var state = i == 0 ? TitleState.Selected : TitleState.Normal;
                Nine(s, TitleArt.Row(state), TitleArt.RowBorder, body.X, y, body.W, h);
                ArtCentred(s, TitleArt.BiomeIcon(deepest[i]), body.X + 26, y + h / 2, s.Dots);
                float x = body.X + 54, line = MathF.Floor((h - 6) / 3);
                Words(s, x, y + 3, names[i], state == TitleState.Normal ? Ink : Gold, 13);
                Words(s, x, y + 3 + line, lines[i], Faint, 12);
                Words(s, x, y + 3 + line * 2, more[i], i == 0 ? Gold : Faint, 12);
                Delete(s, body.X + body.W, y + h / 2);
                y += h + 4;
            }
            float width = MathF.Min(260, body.W);
            float fy = body.Y + body.H + 6;
            Nine(s, UiArt.Button(chosen: true), UiArt.ButtonBorder, body.X + body.W / 2 - width / 2, fy, width, foot - 4);
            Words(s, body.X + body.W / 2, fy + (foot - 4) / 2 - 9, "PLAY SOLO", Ink, 15, true);
            return s.Image;
        }

        static PixelImage Loading(Screen s)
        {
            s.Image.Fill(0, 0, s.Image.Width, s.Image.Height, Rgba.Hex("#08060A"));
            float room = s.Short ? 96 : 130;
            float wanted = MathF.Min(MathF.Min(s.Width - 40, 720), (s.Height - room - 40) * LoadingArt.Width / LoadingArt.Height);
            int dots = Math.Max(1, (int)MathF.Floor(wanted * s.Scale / LoadingArt.Width));
            float w = LoadingArt.Width * dots / s.Scale, h = LoadingArt.Height * dots / s.Scale;
            float top = MathF.Max(12, MathF.Round((s.Height - h - room) / 2));
            float x = MathF.Round((s.Width - w) / 2);
            float frame = TitleArt.PanelBorder * s.Dots / s.Scale;
            Panel(s, x - frame, top - frame, w + 2 * frame, h + 2 * frame);
            Art(s, LoadingArt.Scene(Biome.Mushroom, 3, new[] { ("myconid", Rgba.Hex("#C86ED8")) }, Looks[0]), x, top, dots);
            Words(s, x + 8, top + h - 24, "MUSHROOM WOOD", Faint, 12);
            float width = MathF.Max(w, MathF.Min(s.Width - 32, 300));
            float bx = MathF.Round((s.Width - width) / 2), y = top + h + frame + 8;
            Words(s, s.Width / 2f, y, "BURYING THE ORE", Gold, 13, true);
            y += 24;
            Nine(s, TitleArt.Row(TitleState.Normal), TitleArt.RowBorder, bx, y, width, 14);
            s.Image.Fill(s.P(bx + 3), s.P(y + 3), s.P((width - 6) * 0.45f), s.P(8), Rgba.Hex("#C49A55"));
            s.Image.Fill(s.P(bx + 3), s.P(y + 3), s.P((width - 6) * 0.45f), Math.Max(1, s.P(1)), Rgba.Hex("#F4DE9C"));
            y += 22;
            Words(s, s.Width / 2f, y, "NOTHING HITS YOU MID-ROLL. ROLL AWAY WHEN", Ink, 13, true);
            Words(s, s.Width / 2f, y + 17, "RED MARKS SHOW ON THE GROUND", Ink, 13, true);
            return s.Image;
        }

        static PixelImage Hub(Screen s)
        {
            // The game behind, darkened as the hub leaves it (a lit room standing for the world).
            var room = LoadingArt.Scene(Biome.Cave, 9, null, Looks[1], 320, 180);
            var g = s.Image;
            int dots = (int)Math.Ceiling(g.Height / 180f);
            for (int y = 0; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++) g[x, y] = room[Math.Min(room.Width - 1, x / dots), Math.Min(room.Height - 1, y / dots)].WithAlpha(255);
            string[] names = { "BAG", "SKILLS", "PASSIVES", "CHARACTER", "JOURNAL", "MAP", "SETTINGS", "LEAVE" };
            const float cell = 92, cellH = 44, gap = 6, pad = 8;
            int columns = s.Touch ? 2 : 4;
            int rows = (names.Length + columns - 1) / columns;
            float pw = columns * cell + (columns - 1) * gap + 2 * pad, ph = rows * cellH + (rows - 1) * gap + 2 * pad;
            float px = s.Touch ? s.Width - 10 - pw : MathF.Round((s.Width - pw) / 2), py = s.Touch ? 10 + 120 + 10 + 40 + 6 : MathF.Round((s.Height - ph) / 2);
            Panel(s, px, py, pw, ph);
            for (int i = 0; i < names.Length; i++)
            {
                float cx = px + pad + i % columns * (cell + gap) + cell / 2, cy = py + pad + i / columns * (cellH + gap);
                var state = i == 1 ? TitleState.Hover : i == 0 ? TitleState.Selected : TitleState.Normal;
                if (i < names.Length - 1) s.Image.Fill(s.P(cx - 6), s.P(cy + 6), s.P(12), s.P(12), Rgba.Hex("#C8AE84"));
                TextItem(s, cx, cy + (i < names.Length - 1 ? cellH - 14 : cellH / 2), names[i], state, 12);
            }
            return s.Image;
        }
    }
}
