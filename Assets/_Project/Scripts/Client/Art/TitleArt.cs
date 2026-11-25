using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Client
{
    // How a text item, a row or anything picked from a list stands (TitleArt).
    enum TitleState : byte
    {
        Normal,
        // The pointer is on it.
        Hover,
        // Picked with the keys or a pad, or the row that is chosen.
        Selected,
        Pressed,
    }

    // The title screen's and the menus' art (2026-11-25, Docs/TitleRework.md 4-1·4-3, 묶음 T2): the logo with the glint
    // that crosses it; the look of a text button, which has no box (its words in a colour by its state, a small gold
    // mark at each side when it is pointed at or picked, a strip of light under it); the thin gold-rimmed panel the
    // menus' pages lie on, see-through so the cave behind shows; the rows of the character and world lists; the row
    // that makes a new one; an icon for each biome. Plain pixel images like the rest of Art/, drawn here and nowhere
    // hooked to a screen: the screens draw them (and the preview tool, out/title/).
    static class TitleArt
    {
        static readonly Rgba Black = Rgba.Hex("#0B0807");
        static readonly Rgba GoldPale = Rgba.Hex("#FFF4C8");
        static readonly Rgba GoldLight = Rgba.Hex("#F4DE9C");
        static readonly Rgba GoldBright = Rgba.Hex("#E8BC5C");
        static readonly Rgba GoldMid = Rgba.Hex("#C49A55");
        static readonly Rgba GoldDeep = Rgba.Hex("#96703A");
        static readonly Rgba GoldDark = Rgba.Hex("#6B5230");
        static readonly Rgba Stone = Rgba.Hex("#2A2230");
        static readonly Rgba Night = Rgba.Hex("#0E0B12");

        // ---- The logo: "pray-to" in thick gold letters with a dark line round them and a shadow of stone under them,
        // the hyphen a cut gem. Drawn small (LogoWidth by LogoHeight) to be shown at two, three or more times its size. ----

        // Rows 0 to 3 are for what stands above the small letters (the t), 4 to 13 the small letters, 14 to 18 what
        // hangs below (the p and the y).
        static readonly string[][] Letters =
        {
            // p
            new[]
            {
                ".........", ".........", ".........", ".........",
                "###.####.", "#########", "####..###", "###...###", "###...###", "###...###", "###...###", "####..###", "#########", "###.####.",
                "###......", "###......", "###......", "###......", "###......",
            },
            // r
            new[]
            {
                ".......", ".......", ".......", ".......",
                "###.###", "#######", "####.##", "###....", "###....", "###....", "###....", "###....", "###....", "###....",
                ".......", ".......", ".......", ".......", ".......",
            },
            // a
            new[]
            {
                ".........", ".........", ".........", ".........",
                ".#######.", "#########", "###...###", "......###", ".########", "#########", "###...###", "###..####", "#########", ".####.###",
                ".........", ".........", ".........", ".........", ".........",
            },
            // y
            new[]
            {
                ".........", ".........", ".........", ".........",
                "###...###", "###...###", "###...###", "###...###", "###...###", "###...###", "###...###", "###..####", "#########", ".####.###",
                "......###", "......###", "###...###", "########.", ".######..",
            },
            // The hyphen: a gem.
            new[]
            {
                ".......", ".......", ".......", ".......",
                ".......", ".......", "...*...", "..***..", ".*****.", "*******", ".*****.", "..***..", "...*...", ".......",
                ".......", ".......", ".......", ".......", ".......",
            },
            // t
            new[]
            {
                ".......", ".###...", ".###...", ".###...",
                "#######", "#######", ".###...", ".###...", ".###...", ".###...", ".###...", ".###..#", ".######", "..####.",
                ".......", ".......", ".......", ".......", ".......",
            },
            // o
            new[]
            {
                ".........", ".........", ".........", ".........",
                ".#######.", "#########", "###...###", "###...###", "###...###", "###...###", "###...###", "###...###", "#########", ".#######.",
                ".........", ".........", ".........", ".........", ".........",
            },
        };

        const int LetterRows = 19, LogoPad = 2, LetterGap = 1, GemGap = 2;
        public static readonly int LogoWidth = LogoPad * 2 + WordWidth();
        // The line round the letters above, and the line and the shadow below.
        public const int LogoHeight = LetterRows + 1 + 3;
        // How many steps the glint takes to cross the logo (LogoGlint): at a twelfth of a second each it crosses in
        // well under a second, and the screen waits some seconds between crossings.
        public const int GlintFrames = 12;

        static int WordWidth()
        {
            int width = 0;
            for (int i = 0; i < Letters.Length; i++) width += Letters[i][0].Length + (i == 0 ? 0 : Gap(i));
            return width;
        }

        // The space before a letter: wider round the gem.
        static int Gap(int letter) => letter == 4 || letter == 5 ? GemGap : LetterGap;

        // 0 nothing, 1 a letter's face, 2 the gem's.
        static byte[,] LogoFaces()
        {
            var faces = new byte[LogoWidth, LogoHeight];
            int x = LogoPad;
            for (int i = 0; i < Letters.Length; i++)
            {
                if (i > 0) x += Gap(i);
                var rows = Letters[i];
                for (int row = 0; row < rows.Length; row++)
                    for (int column = 0; column < rows[row].Length; column++)
                        if (rows[row][column] != '.') faces[x + column, 1 + row] = (byte)(rows[row][column] == '*' ? 2 : 1);
                x += rows[0].Length;
            }
            return faces;
        }

        static PixelImage _logo;

        // The logo at rest.
        public static PixelImage Logo => _logo ??= DrawLogo();

        static PixelImage DrawLogo()
        {
            var faces = LogoFaces();
            byte At(int x, int y) => x < 0 || y < 0 || x >= LogoWidth || y >= LogoHeight ? (byte)0 : faces[x, y];
            var image = new PixelImage(LogoWidth, LogoHeight);
            // The shadow: the letters again two rows down, in stone.
            for (int y = 0; y < LogoHeight; y++)
                for (int x = 0; x < LogoWidth; x++)
                    if (At(x, y - 2) != 0 || At(x - 1, y - 2) != 0 || At(x + 1, y - 2) != 0) image[x, y] = Stone;
            // The dark line round them.
            for (int y = 0; y < LogoHeight; y++)
                for (int x = 0; x < LogoWidth; x++)
                {
                    if (At(x, y) != 0) continue;
                    bool near = false;
                    for (int dy = -1; dy <= 1 && !near; dy++)
                        for (int dx = -1; dx <= 1 && !near; dx++)
                            near = At(x + dx, y + dy) != 0;
                    if (near) image[x, y] = Black;
                }
            var gem = Ramp.Of("#5CC8E8");
            for (int y = 0; y < LogoHeight; y++)
                for (int x = 0; x < LogoWidth; x++)
                {
                    byte face = At(x, y);
                    if (face == 0) continue;
                    bool top = At(x, y - 1) == 0, bottom = At(x, y + 1) == 0, left = At(x - 1, y) == 0, right = At(x + 1, y) == 0;
                    if (face == 2)
                    {
                        // Cut faces: light up and to the left, dark down and to the right, a spark near the top.
                        int row = y - 1;
                        image[x, y] = top && left || row == 7 && left ? Rgba.White : top || left ? gem.Light : bottom || right ? gem.Dark : row <= 9 ? gem.Main : gem.Shade;
                        continue;
                    }
                    // Gold lit from above: a pale top edge, bright over deep down the letter's height, a dark foot.
                    int band = y - 1;
                    var body = band <= 6 ? GoldLight : band <= 9 ? GoldBright : band <= 12 ? GoldMid : band <= 15 ? GoldBright : GoldMid;
                    image[x, y] = top ? GoldPale : bottom ? GoldDark : right ? GoldDeep : left ? GoldLight : body;
                }
            return image;
        }

        // The glint at one step of its crossing (0 to GlintFrames - 1): only its light, to lay over the logo; clear
        // everywhere else. A slanted band of white over the letters' faces, bright in its middle.
        public static PixelImage LogoGlint(int frame)
        {
            var faces = LogoFaces();
            var image = new PixelImage(LogoWidth, LogoHeight);
            frame = Math.Max(0, Math.Min(GlintFrames - 1, frame));
            // The band's middle where it meets the top row, from before the left edge to past the right one.
            float middle = -5 + (LogoWidth + LogoHeight * 0.75f + 10) * (frame + 0.5f) / GlintFrames;
            for (int y = 0; y < LogoHeight; y++)
                for (int x = 0; x < LogoWidth; x++)
                {
                    if (faces[x, y] == 0) continue;
                    float off = MathF.Abs(x + y * 0.75f - middle);
                    if (off <= 1.5f) image[x, y] = Rgba.White.WithAlpha(230);
                    else if (off <= 3.5f) image[x, y] = GoldPale.WithAlpha(150);
                    else if (off <= 5f) image[x, y] = GoldPale.WithAlpha(60);
                }
            return image;
        }

        // The logo with the glint on it, for a picture of one step.
        public static PixelImage LogoWithGlint(int frame)
        {
            var image = Logo.Copy();
            image.Draw(LogoGlint(frame), 0, 0);
            return image;
        }

        // ---- A text button: no box. Its words in Ink(state) over InkShadow; when pointed at or picked a Mark at each
        // side of the words and a Strip under them. ----

        // The words' colour.
        public static Rgba Ink(TitleState state) => state switch
        {
            TitleState.Hover => GoldLight,
            TitleState.Selected => GoldPale,
            TitleState.Pressed => GoldMid,
            _ => Rgba.Hex("#CFC4AE"),
        };

        // Under the words, a pixel down, so they read over a bright piece of the cave.
        public static readonly Rgba InkShadow = Rgba.Hex("#0B0807").WithAlpha(220);
        // Words that cannot be picked.
        public static readonly Rgba InkOff = Rgba.Hex("#7A7068");
        // How much bigger the words are drawn when pointed at or picked.
        public const float GrowHover = 1.08f, GrowSelected = 1.12f;

        public const int MarkSize = 7;

        static readonly string[] MarkRows =
        {
            "...o...",
            "..oLo..",
            ".oLLMo.",
            "oLLMMDo",
            ".oMMDo.",
            "..oDo..",
            "...o...",
        };

        // The mark at a side of a text item that is pointed at or picked: a small gold diamond; picked it is brighter
        // with a pale heart, pressed it is dull. Nothing is drawn for an item at rest (null).
        public static PixelImage Mark(TitleState state)
        {
            if (state == TitleState.Normal) return null;
            var colors = new Dictionary<char, Rgba>
            {
                ['o'] = Black,
                ['L'] = state == TitleState.Selected ? GoldPale : state == TitleState.Pressed ? GoldMid : GoldLight,
                ['M'] = state == TitleState.Pressed ? GoldDeep : GoldBright,
                ['D'] = state == TitleState.Pressed ? GoldDark : GoldDeep,
            };
            return PixelImage.FromRows(colors, MarkRows);
        }

        public const int BracketWidth = 5, BracketHeight = 11;

        static readonly string[] BracketRows =
        {
            ".oooo",
            "oLLMo",
            "oLooo",
            "oLo..",
            "oMo..",
            "oMo..",
            "oMo..",
            "oMo..",
            "oMooo",
            "oMDDo",
            ".oooo",
        };

        // A carved bracket for the left of a value that can be turned ("◀ 값 ▶" rows use the arrows; a framed word,
        // like a page's name, uses these): mirrored for the right.
        public static PixelImage Bracket(bool left)
        {
            var colors = new Dictionary<char, Rgba> { ['o'] = Black, ['L'] = GoldLight, ['M'] = GoldMid, ['D'] = GoldDark };
            var image = PixelImage.FromRows(colors, BracketRows);
            return left ? image : image.FlippedX();
        }

        public const int ArrowWidth = 6, ArrowHeight = 9;

        static readonly string[] ArrowRows =
        {
            "....oo",
            "...oLo",
            "..oLMo",
            ".oLMMo",
            "oLMMMo",
            ".oMMDo",
            "..oMDo",
            "...oDo",
            "....oo",
        };

        // The arrow at each side of a value that is turned through its choices (the making screen's rows), pointing
        // left; mirrored for the right.
        public static PixelImage Arrow(bool left, TitleState state)
        {
            var colors = new Dictionary<char, Rgba>
            {
                ['o'] = Black,
                ['L'] = state == TitleState.Normal ? GoldMid : state == TitleState.Pressed ? GoldDeep : GoldPale,
                ['M'] = state == TitleState.Normal ? GoldDeep : state == TitleState.Pressed ? GoldDark : GoldBright,
                ['D'] = state == TitleState.Normal ? GoldDark : state == TitleState.Pressed ? GoldDark : GoldDeep,
            };
            var image = PixelImage.FromRows(colors, ArrowRows);
            return left ? image : image.FlippedX();
        }

        public const int StripHeight = 5;

        // The strip of light under a text item that is pointed at or picked, as wide as asked: a gold line that fades
        // to nothing at both ends with a glow above and below it; picked it is brighter with a pale middle, pressed it
        // is short and dull. Null for an item at rest.
        public static PixelImage Strip(int width, TitleState state)
        {
            if (state == TitleState.Normal || width < 4) return null;
            var image = new PixelImage(width, StripHeight);
            var line = state == TitleState.Selected ? GoldLight : state == TitleState.Pressed ? GoldDeep : GoldBright;
            float reach = state == TitleState.Pressed ? 0.7f : 1;
            for (int x = 0; x < width; x++)
            {
                // 1 in the middle, 0 at the ends.
                float t = 1 - MathF.Abs((x + 0.5f) / width * 2 - 1) / reach;
                if (t <= 0) continue;
                float strength = MathF.Min(1, t * 2.2f);
                image[x, 2] = line.WithAlpha((int)(255 * strength));
                if (state == TitleState.Selected && t > 0.55f) image[x, 2] = GoldPale.WithAlpha(255);
                int glow = (int)((state == TitleState.Selected ? 90 : state == TitleState.Hover ? 60 : 30) * strength * strength);
                image[x, 1] = image[x, 3] = GoldBright.WithAlpha(glow);
                image[x, 0] = image[x, 4] = GoldBright.WithAlpha(glow / 3);
            }
            return image;
        }

        // ---- The panel a menu's page lies on: see-through night with a thin gold rim, a small set stone at each
        // corner. Stretched by its middle (PanelBorder pixels are kept at each side). ----

        public const int PanelBorder = 6;
        // How much of the cave shows through the panel's middle (0 to 255: the fill's alpha).
        public const int PanelAlpha = 178;

        static PixelImage _panel;

        public static PixelImage Panel => _panel ??= DrawPanel();

        static PixelImage DrawPanel()
        {
            const int size = PanelBorder * 2 + 4;
            var image = new PixelImage(size, size);
            var fill = Night.WithAlpha(PanelAlpha);
            for (int y = 1; y < size - 1; y++)
                for (int x = 1; x < size - 1; x++)
                    image[x, y] = fill;
            // A soft dark edge outside the rim, the rim, and a faint line of light inside it.
            for (int i = 2; i < size - 2; i++)
            {
                image[i, 0] = image[i, size - 1] = image[0, i] = image[size - 1, i] = Black.WithAlpha(120);
                image[i, 1] = GoldLight;
                image[1, i] = GoldMid;
                image[i, size - 2] = GoldDark;
                image[size - 2, i] = GoldDeep;
                image[i, 2] = image[2, i] = Black.WithAlpha(200);
                image[i, size - 3] = image[size - 3, i] = Black.WithAlpha(200);
            }
            // The corners: cut, with a stone set in each.
            foreach (var (cx, cy, sx, sy) in new[] { (0, 0, 1, 1), (size - 1, 0, -1, 1), (0, size - 1, 1, -1), (size - 1, size - 1, -1, -1) })
            {
                for (int j = 0; j < 4; j++)
                    for (int i = 0; i < 4; i++)
                        image[cx + sx * i, cy + sy * j] = Rgba.Clear;
                void Set(int i, int j, Rgba color) => image[cx + sx * i, cy + sy * j] = color;
                // A small diamond: dark line, gold, pale heart.
                Set(2, 0, Black); Set(1, 1, Black); Set(0, 2, Black); Set(3, 1, Black); Set(1, 3, Black); Set(4, 2, Black); Set(2, 4, Black); Set(3, 3, Black);
                Set(2, 1, GoldLight); Set(1, 2, GoldLight); Set(2, 2, GoldPale); Set(3, 2, GoldMid); Set(2, 3, GoldMid);
                Set(4, 1, GoldLight); Set(1, 4, GoldMid); Set(4, 3, fill); Set(3, 4, fill);
                Set(4, 0, Black.WithAlpha(120)); Set(0, 4, Black.WithAlpha(120));
                Set(0, 3, Rgba.Clear); Set(3, 0, Rgba.Clear); Set(0, 0, Rgba.Clear); Set(1, 0, Rgba.Clear); Set(0, 1, Rgba.Clear);
            }
            return image;
        }

        public const int DividerHeight = 5;

        // The line under a page's name, as wide as asked: gold fading to nothing at both ends, a small diamond in its
        // middle.
        public static PixelImage Divider(int width)
        {
            width = Math.Max(9, width);
            var image = new PixelImage(width, DividerHeight);
            int middle = width / 2;
            for (int x = 0; x < width; x++)
            {
                float t = 1 - MathF.Abs((x + 0.5f) / width * 2 - 1);
                image[x, 2] = GoldMid.WithAlpha((int)(255 * MathF.Min(1, t * 1.6f)));
                image[x, 3] = Black.WithAlpha((int)(150 * MathF.Min(1, t * 1.6f)));
            }
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    int reach = Math.Abs(dx) + Math.Abs(dy);
                    if (reach > 2) continue;
                    image[middle + dx, 2 + dy] = reach == 2 ? GoldDeep : reach == 1 ? GoldLight : GoldPale;
                }
            return image;
        }

        // ---- A row of a list (a character, a world): stretched by its middle (RowBorder pixels kept at each side). At
        // rest a faint dark band with a line under it; pointed at, lighter with a gold bar at its left; chosen, a thin
        // gold rim with a warm fill; pressed, the chosen one sunk. ----

        public const int RowBorder = 4;

        public static PixelImage Row(TitleState state)
        {
            const int size = RowBorder * 2 + 4;
            var image = new PixelImage(size, size);
            var fill = state switch
            {
                TitleState.Hover => Rgba.Hex("#3A2E30").WithAlpha(190),
                TitleState.Selected => Rgba.Hex("#4A3828").WithAlpha(215),
                TitleState.Pressed => Rgba.Hex("#2A2018").WithAlpha(225),
                _ => Rgba.Hex("#1A141C").WithAlpha(140),
            };
            for (int y = 1; y < size - 1; y++)
                for (int x = 1; x < size - 1; x++)
                    image[x, y] = fill;
            for (int i = 2; i < size - 2; i++)
            {
                image[i, 0] = image[i, size - 1] = image[0, i] = image[size - 1, i] = fill;
            }
            if (state == TitleState.Normal)
            {
                for (int i = 2; i < size - 2; i++) image[i, size - 1] = GoldDark.WithAlpha(150);
                return image;
            }
            if (state == TitleState.Hover)
            {
                for (int i = 2; i < size - 2; i++)
                {
                    image[i, size - 1] = GoldDeep.WithAlpha(210);
                    image[0, i] = GoldBright;
                    image[1, i] = GoldDeep;
                }
                return image;
            }
            bool pressed = state == TitleState.Pressed;
            for (int i = 2; i < size - 2; i++)
            {
                image[i, 0] = pressed ? GoldDark : GoldLight;
                image[0, i] = pressed ? GoldDark : GoldMid;
                image[i, size - 1] = pressed ? GoldMid : GoldDark;
                image[size - 1, i] = pressed ? GoldMid : GoldDeep;
            }
            foreach (var (x, y) in new[] { (1, 1), (size - 2, 1), (1, size - 2), (size - 2, size - 2) }) image[x, y] = pressed ? GoldDeep : GoldPale;
            // A line of light under the top edge, a shadow there when sunk.
            for (int i = 2; i < size - 2; i++) image[i, 1] = pressed ? Black.WithAlpha(150) : GoldLight.WithAlpha(50);
            return image;
        }

        // The row that makes a new character or world ("+ 새 캐릭터"), at the size asked: an empty slot, a dashed gold
        // line round a faint fill (drawn to size, since stretching would smear the dashes); pointed at or picked the
        // dashes are bright and the fill warm.
        public static PixelImage NewRow(int width, int height, TitleState state)
        {
            width = Math.Max(12, width);
            height = Math.Max(8, height);
            var image = new PixelImage(width, height);
            bool lit = state != TitleState.Normal;
            var fill = (state == TitleState.Pressed ? Rgba.Hex("#2A2018") : lit ? Rgba.Hex("#3A2E28") : Rgba.Hex("#1A141C")).WithAlpha(lit ? 170 : 90);
            var dash = state == TitleState.Selected ? GoldLight : state == TitleState.Hover ? GoldBright : state == TitleState.Pressed ? GoldDeep : GoldDark;
            image.Fill(1, 1, width - 2, height - 2, fill);
            bool On(int along) => along % 6 < 4;
            for (int x = 2; x < width - 2; x++)
            {
                if (!On(x - 2)) continue;
                image[x, 0] = dash;
                image[x, height - 1] = dash;
            }
            for (int y = 2; y < height - 2; y++)
            {
                if (!On(y - 2)) continue;
                image[0, y] = dash;
                image[width - 1, y] = dash;
            }
            image[1, 1] = image[width - 2, 1] = image[1, height - 2] = image[width - 2, height - 2] = dash;
            return image;
        }

        public const int PlusSize = 9;

        static readonly string[] PlusRows =
        {
            "...ooo...",
            "...oLo...",
            "...oLo...",
            "ooooLoooo",
            "oLLLMMMDo",
            "ooooMoooo",
            "...oMo...",
            "...oDo...",
            "...ooo...",
        };

        // The plus before a new row's words.
        public static PixelImage Plus(TitleState state)
        {
            var colors = new Dictionary<char, Rgba>
            {
                ['o'] = Black,
                ['L'] = state == TitleState.Normal ? GoldMid : state == TitleState.Pressed ? GoldDeep : GoldPale,
                ['M'] = state == TitleState.Normal ? GoldDeep : state == TitleState.Pressed ? GoldDark : GoldBright,
                ['D'] = state == TitleState.Normal ? GoldDark : state == TitleState.Pressed ? GoldDark : GoldDeep,
            };
            return PixelImage.FromRows(colors, PlusRows);
        }

        // ---- The biomes' icons, for a world's row (the deepest biome reached): a small medallion in the biome's own
        // colour with one thing of it. ----

        public const int BiomeIconSize = 16;

        // The biomes in the order a character meets them, for a sheet of the icons.
        public static readonly Biome[] BiomeOrder = { Biome.Cave, Biome.Mushroom, Biome.Ice, Biome.Sand, Biome.Crystal, Biome.Lake, Biome.Lava, Biome.Abyss };

        static readonly Dictionary<Biome, PixelImage> Icons = new Dictionary<Biome, PixelImage>();

        public static PixelImage BiomeIcon(Biome biome)
        {
            if (!Icons.TryGetValue(biome, out var icon)) Icons[biome] = icon = DrawBiomeIcon(biome);
            return icon;
        }

        static PixelImage DrawBiomeIcon(Biome biome)
        {
            var (rows, back, colors) = IconOf(biome);
            var image = new PixelImage(BiomeIconSize, BiomeIconSize);
            var rim = Ramp.Of(back);
            for (int y = 0; y < BiomeIconSize; y++)
                for (int x = 0; x < BiomeIconSize; x++)
                {
                    // A square with its corners cut.
                    int cx = Math.Min(x, BiomeIconSize - 1 - x), cy = Math.Min(y, BiomeIconSize - 1 - y);
                    if (cx + cy < 2) continue;
                    bool edge = cx == 0 || cy == 0 || cx + cy == 2;
                    bool inner = !edge && (cx == 1 || cy == 1 || cx + cy == 3);
                    image[x, y] = edge ? Black : inner ? (y < BiomeIconSize / 2 && x < BiomeIconSize - 2 ? rim.Main : rim.Shade) : back.Scale(0.22f).Mix(Night, 0.35f);
                }
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                {
                    char c = rows[y][x];
                    if (c == '.') continue;
                    image[2 + x, 2 + y] = colors[c];
                }
            return image;
        }

        static Dictionary<char, Rgba> IconColors(string main, string accent, string third = null)
        {
            var m = Ramp.Of(main);
            var a = Ramp.Of(accent);
            var colors = new Dictionary<char, Rgba>
            {
                ['L'] = m.Light, ['M'] = m.Main, ['D'] = m.Shade, ['d'] = m.Dark,
                ['A'] = a.Light, ['a'] = a.Main, ['b'] = a.Shade,
                ['w'] = Rgba.White,
            };
            if (third != null)
            {
                var t = Ramp.Of(third);
                colors['T'] = t.Light;
                colors['t'] = t.Main;
                colors['u'] = t.Shade;
            }
            return colors;
        }

        static (string[] Rows, Rgba Back, Dictionary<char, Rgba> Colors) IconOf(Biome biome) => biome switch
        {
            // Mounds of rock and a lamp's flame.
            Biome.Cave => (new[]
            {
                "............",
                ".....A......",
                "....AwA.....",
                ".....a......",
                "..L.....L...",
                ".LMD...LM...",
                ".LMD..LMMD..",
                "LMMD..LMMD..",
                "LMMDD.LMMDD.",
                "LMMMDLMMMMD.",
                "dddddddddddd",
                "............",
            }, Rgba.Hex("#A8743C"), IconColors("#9A7450", "#FFC850")),
            // A capped mushroom, spotted.
            Biome.Mushroom => (new[]
            {
                "............",
                "...LLLLL....",
                "..LMMwMMM...",
                ".LMwMMMMwM..",
                ".MMMMwMMMMD.",
                ".MMMMMMMMDD.",
                "..DDDDDDDD..",
                "....AaA.....",
                "....Aab.....",
                "....Aab.....",
                "...AAabb....",
                "............",
            }, Rgba.Hex("#7CC850"), IconColors("#4F86E8", "#E8E0C8")),
            // Spikes of ice.
            Biome.Ice => (new[]
            {
                ".....w......",
                ".....LM.....",
                "....wLM.....",
                "....LMM.....",
                "....LMMD....",
                ".w..LMMD..w.",
                ".LM.LMMD.LM.",
                ".LM.LMMD.LMD",
                "LMMDLMMDLMMD",
                "LMMDLMMDLMMD",
                "dddddddddddd",
                "............",
            }, Rgba.Hex("#8CC8F0"), IconColors("#9CD4F4", "#FFFFFF")),
            // A cactus on a dune.
            Biome.Sand => (new[]
            {
                "............",
                ".....Aa.....",
                ".....Aa.....",
                ".A...Aa.....",
                ".Aa..Aa..A..",
                ".AaaaAa..Aa.",
                "..bbbAaaaAa.",
                ".....Aabbb..",
                ".....Aa.....",
                "LLLMMAaMMLLL",
                "MMMMMMMMMMDD",
                "DDDDDDDDDDDD",
            }, Rgba.Hex("#D8A850"), IconColors("#E0B468", "#6CA858")),
            // A cut crystal.
            Biome.Crystal => (new[]
            {
                "............",
                ".....LL.....",
                "....LLMM....",
                "...LLwMMM...",
                "..LLwLMMMD..",
                ".LLLLLMMMDD.",
                ".MMMMMDDDDD.",
                "..MMMMDDDD..",
                "...MMMDDD...",
                "....MMDD....",
                ".....MD.....",
                "............",
            }, Rgba.Hex("#E87CD0"), IconColors("#F08CDC", "#8AF0FF")),
            // A lily on the water.
            Biome.Lake => (new[]
            {
                "............",
                "....Tt......",
                "...TttT.....",
                "..AAAAAAa...",
                ".AAaaAaaab..",
                "............",
                ".LL...LL...L",
                "L..LLL..LLL.",
                "MMMMMMMMMMMM",
                "MLLMMMMLLMMM",
                "DDDDDDDDDDDD",
                "DDDMMDDDDMMD",
            }, Rgba.Hex("#48C0B8"), IconColors("#3EA8C0", "#5CB060", "#F08CB8")),
            // A cone of dark rock, fire running out of it.
            Biome.Lava => (new[]
            {
                "......A.....",
                ".....Aw.....",
                "....AwwA....",
                "....waaw....",
                "...Aaaaaa...",
                "...DDaaDD...",
                "..DDDaaDDD..",
                "..DDMaaMDD..",
                ".DDMMDaMMDD.",
                ".DMMDDDDMMD.",
                "dddddddddddd",
                "............",
            }, Rgba.Hex("#E8642A"), IconColors("#6A4A4C", "#FF8A30")),
            // A star in the dark.
            _ => (new[]
            {
                ".....M......",
                ".....M......",
                "....DMD.....",
                "...DMwMD....",
                "MMMMwwwMMMM.",
                "...DMwMD....",
                "....DMD.....",
                ".....M......",
                ".....M......",
                "............",
                ".A......A...",
                "............",
            }, Rgba.Hex("#8A5ADA"), IconColors("#B48CFF", "#70E0FF")),
        };
    }
}
