using System;
using System.Collections.Generic;

namespace Game.Client
{
    // The pieces the UI mockups are built from (UiMockPreview, Docs: the UI overhaul's three candidate looks). Only
    // the preview tool uses them: nothing here is in the game. A small Latin pixel font (the tool has no font
    // renderer), glyph icons, drawing helpers, and a skin for each candidate look.
    static class MockFont
    {
        // 5 by 7, capitals and digits.
        static readonly Dictionary<char, string> Big = new Dictionary<char, string>
        {
            ['A'] = "01110 10001 10001 11111 10001 10001 10001", ['B'] = "11110 10001 10001 11110 10001 10001 11110",
            ['C'] = "01110 10001 10000 10000 10000 10001 01110", ['D'] = "11110 10001 10001 10001 10001 10001 11110",
            ['E'] = "11111 10000 10000 11110 10000 10000 11111", ['F'] = "11111 10000 10000 11110 10000 10000 10000",
            ['G'] = "01110 10001 10000 10111 10001 10001 01111", ['H'] = "10001 10001 10001 11111 10001 10001 10001",
            ['I'] = "01110 00100 00100 00100 00100 00100 01110", ['J'] = "00111 00010 00010 00010 00010 10010 01100",
            ['K'] = "10001 10010 10100 11000 10100 10010 10001", ['L'] = "10000 10000 10000 10000 10000 10000 11111",
            ['M'] = "10001 11011 10101 10101 10001 10001 10001", ['N'] = "10001 11001 10101 10011 10001 10001 10001",
            ['O'] = "01110 10001 10001 10001 10001 10001 01110", ['P'] = "11110 10001 10001 11110 10000 10000 10000",
            ['Q'] = "01110 10001 10001 10001 10101 10010 01101", ['R'] = "11110 10001 10001 11110 10100 10010 10001",
            ['S'] = "01111 10000 10000 01110 00001 00001 11110", ['T'] = "11111 00100 00100 00100 00100 00100 00100",
            ['U'] = "10001 10001 10001 10001 10001 10001 01110", ['V'] = "10001 10001 10001 10001 10001 01010 00100",
            ['W'] = "10001 10001 10001 10101 10101 11011 10001", ['X'] = "10001 10001 01010 00100 01010 10001 10001",
            ['Y'] = "10001 10001 01010 00100 00100 00100 00100", ['Z'] = "11111 00001 00010 00100 01000 10000 11111",
            ['0'] = "01110 10001 10011 10101 11001 10001 01110", ['1'] = "00100 01100 00100 00100 00100 00100 01110",
            ['2'] = "01110 10001 00001 00110 01000 10000 11111", ['3'] = "11110 00001 00001 01110 00001 00001 11110",
            ['4'] = "00010 00110 01010 10010 11111 00010 00010", ['5'] = "11111 10000 11110 00001 00001 10001 01110",
            ['6'] = "00110 01000 10000 11110 10001 10001 01110", ['7'] = "11111 00001 00010 00100 01000 01000 01000",
            ['8'] = "01110 10001 10001 01110 10001 10001 01110", ['9'] = "01110 10001 10001 01111 00001 00010 01100",
            ['+'] = "00000 00100 00100 11111 00100 00100 00000", ['-'] = "00000 00000 00000 11111 00000 00000 00000",
            ['/'] = "00001 00001 00010 00100 01000 10000 10000", [':'] = "00000 00100 00000 00000 00000 00100 00000",
            ['.'] = "00000 00000 00000 00000 00000 00100 00000", [','] = "00000 00000 00000 00000 00100 00100 01000",
            ['%'] = "11001 11010 00010 00100 01000 01011 10011", ['!'] = "00100 00100 00100 00100 00100 00000 00100",
            ['?'] = "01110 10001 00001 00110 00100 00000 00100", ['('] = "00010 00100 01000 01000 01000 00100 00010",
            [')'] = "01000 00100 00010 00010 00010 00100 01000", ['\''] = "00100 00100 00000 00000 00000 00000 00000",
            ['*'] = "00000 00000 01010 00100 01010 00000 00000", ['>'] = "01000 00100 00010 00001 00010 00100 01000",
            ['<'] = "00010 00100 01000 10000 01000 00100 00010", ['['] = "01110 01000 01000 01000 01000 01000 01110",
            [']'] = "01110 00010 00010 00010 00010 00010 01110",
        };

        // 3 by 5, for counts, prices, key hints and small labels.
        static readonly Dictionary<char, string> Small = new Dictionary<char, string>
        {
            ['0'] = "111 101 101 101 111", ['1'] = "010 110 010 010 111", ['2'] = "111 001 111 100 111", ['3'] = "111 001 111 001 111",
            ['4'] = "101 101 111 001 001", ['5'] = "111 100 111 001 111", ['6'] = "111 100 111 101 111", ['7'] = "111 001 001 010 010",
            ['8'] = "111 101 111 101 111", ['9'] = "111 101 111 001 111", ['*'] = "000 101 010 101 000", ['+'] = "000 010 111 010 000",
            ['-'] = "000 000 111 000 000", ['/'] = "001 001 010 100 100", ['%'] = "101 001 010 100 101", ['.'] = "000 000 000 000 010",
            [','] = "000 000 000 010 100", [':'] = "000 010 000 010 000",
            ['A'] = "010 101 111 101 101", ['B'] = "110 101 110 101 110", ['C'] = "011 100 100 100 011", ['D'] = "110 101 101 101 110",
            ['E'] = "111 100 110 100 111", ['F'] = "111 100 110 100 100", ['G'] = "011 100 101 101 011", ['H'] = "101 101 111 101 101",
            ['I'] = "111 010 010 010 111", ['J'] = "001 001 001 101 010", ['K'] = "101 101 110 101 101", ['L'] = "100 100 100 100 111",
            ['M'] = "101 111 111 101 101", ['N'] = "110 101 101 101 101", ['O'] = "010 101 101 101 010", ['P'] = "110 101 110 100 100",
            ['Q'] = "010 101 101 110 011", ['R'] = "110 101 110 101 101", ['S'] = "011 100 010 001 110", ['T'] = "111 010 010 010 010",
            ['U'] = "101 101 101 101 111", ['V'] = "101 101 101 101 010", ['W'] = "101 101 111 111 101", ['X'] = "101 101 010 101 101",
            ['Y'] = "101 101 010 010 010", ['Z'] = "111 001 010 100 111", ['!'] = "010 010 010 000 010", ['?'] = "110 001 010 000 010",
            ['('] = "010 100 100 100 010", [')'] = "010 001 001 001 010", ['>'] = "100 010 001 010 100", ['<'] = "001 010 100 010 001",
        };

        public static int Width(string text, int scale = 1) => text.Length == 0 ? 0 : text.Length * 6 * scale - scale;
        public static int SmallWidth(string text) => text.Length == 0 ? 0 : text.Length * 4 - 1;

        public static void Draw(PixelImage g, int x, int y, string text, Rgba color, int scale = 1)
        {
            foreach (char c in text)
            {
                if (Big.TryGetValue(char.ToUpperInvariant(c), out string rows)) Glyph(g, x, y, rows, color, scale);
                x += 6 * scale;
            }
        }

        public static void DrawSmall(PixelImage g, int x, int y, string text, Rgba color)
        {
            foreach (char c in text)
            {
                if (Small.TryGetValue(char.ToUpperInvariant(c), out string rows)) Glyph(g, x, y, rows, color, 1);
                x += 4;
            }
        }

        static void Glyph(PixelImage g, int x, int y, string rows, Rgba color, int scale)
        {
            int col = 0, row = 0;
            foreach (char bit in rows)
            {
                if (bit == ' ')
                {
                    row++;
                    col = 0;
                    continue;
                }
                if (bit == '1') g.Fill(x + col * scale, y + row * scale, scale, scale, color);
                col++;
            }
        }
    }

    // Small one-colour pictures for tabs, buttons and the points of the passive tree. '#' is the colour, 'o' its
    // light, 'x' its dark.
    static class MockGlyph
    {
        public static readonly string[] Bag =
        {
            "...#####...", "..#.....#..", "..#.....#..", ".#########.", ".#ooooooo#.", ".####x####.", ".####x####.", ".#########.", ".#########.", ".#########.",
        };
        public static readonly string[] Skills =
        {
            ".....#.....", ".....#.....", "....#o#....", "....#o#....", "..##ooo##..", "###ooooo###", "..##ooo##..", "....#o#....", "....#o#....", ".....#.....", ".....#.....",
        };
        public static readonly string[] Tree =
        {
            "....###....", "....#o#....", "....###....", ".....#.....", "..#######..", "..#.....#..", ".###...###.", ".#o#...#o#.", ".###...###.",
        };
        public static readonly string[] Person =
        {
            "....###....", "...#ooo#...", "...#ooo#...", "....###....", "..#######..", ".#########.", ".##.###.##.", ".##.###.##.", "....###....", "....#.#....", "...##.##...",
        };
        public static readonly string[] Book =
        {
            ".####.####.", ".#oo###oo#.", ".#oo#.#oo#.", ".#oo#.#oo#.", ".#oo#.#oo#.", ".#oo#.#oo#.", ".#########.", ".####.####.",
        };
        public static readonly string[] Map =
        {
            ".##..###..#", ".#o##ooo###", ".#oooo#ooo#", ".#o#oooo#o#", ".#oooo#ooo#", ".#o#ooooo##", ".#o##ooo##.", ".###.###...",
        };
        public static readonly string[] Gear =
        {
            "....###....", ".#..###..#.", ".###ooo###.", "..#o###o#..", "###o#.#o###", "###o#.#o###", "..#o###o#..", ".###ooo###.", ".#..###..#.", "....###....",
        };
        public static readonly string[] Talk =
        {
            ".#########.", "###########", "##o##o##o##", "###########", "###########", ".#########.", "...###.....", "..##.......", ".#.........",
        };
        public static readonly string[] Gift =
        {
            "..##...##..", ".#..#.#..#.", "..###.###..", ".#########.", ".####o####.", ".#########.", "..###o###..", "..###o###..", "..###o###..", "..#######..",
        };
        public static readonly string[] Request =
        {
            ".########..", ".#########.", ".####x####.", ".####x####.", ".####x####.", ".####x####.", ".#########.", ".####x####.", ".#########.", "..########.",
        };
        public static readonly string[] Shop =
        {
            ".#########.", "#o#o#o#o#o#", "#o#o#o#o#o#", ".#.#.#.#.#.", "...........", "..#######..", "..#ooooo#..", "..#o###o#..", "..#o###o#..", "..#######..",
        };
        public static readonly string[] Close =
        {
            "##.....##", "###...###", ".###.###.", "..#####..", "...###...", "..#####..", ".###.###.", "###...###", "##.....##",
        };
        public static readonly string[] SmallX = { "#...#", ".#.#.", "..#..", ".#.#.", "#...#" };
        public static readonly string[] Hub =
        {
            "###.###.###", "###.###.###", "###.###.###", "...........", "###.###.###", "###.###.###", "###.###.###", "...........", "###.###.###", "###.###.###", "###.###.###",
        };
        public static readonly string[] Search =
        {
            "..####.....", ".#....#....", "#......#...", "#......#...", "#......#...", ".#....#....", "..####.#...", ".......##..", "........##.", ".........##",
        };
        public static readonly string[] Coin = { "..###..", ".#ooo#.", "#oo#oo#", "#oo#oo#", "#oo#oo#", ".#ooo#.", "..###.." };
        public static readonly string[] Heart = { ".##.##.", "#o#####", "#######", ".#####.", "..###..", "...#..." };
        public static readonly string[] Next = { "#######", ".#####.", "..###..", "...#..." };
        public static readonly string[] Pointer = { "#......", "##.....", "#o#....", "#oo#...", "#ooo#..", "#oooo#.", "#oo###.", "#.#o#..", "...#o#.", "....#.." };

        // The points of the passive tree, 7 by 7.
        public static readonly string[] Sword = { "......#", ".....##", "....##.", "#..##..", ".###...", ".##....", "#..#..." };
        public static readonly string[] Arrow = { "...####", "....###", "...#.##", "..#...#", ".#.....", "#......" };
        public static readonly string[] Star = { "...#...", "...#...", "..###..", "###.###", "..###..", "...#...", "...#..." };
        public static readonly string[] Flame = { "...#...", "..##...", "..###..", ".####..", ".#####.", ".##o##.", "..###.." };
        public static readonly string[] Snow = { "#..#..#", ".#.#.#.", "..###..", "###.###", "..###..", ".#.#.#.", "#..#..#" };
        public static readonly string[] Bolt = { "...###.", "..###..", ".###...", ".#####.", "...##..", "..##...", ".#....." };
        public static readonly string[] Shield = { "#######", "#o#####", "#o#####", "#######", ".#####.", "..###..", "...#..." };
        public static readonly string[] Boot = { "..###..", "..###..", "..###..", "..###..", "..#####", ".######", ".######" };
        public static readonly string[] Spirit = { "..###..", ".#####.", "#.###.#", "#######", "#######", "#######", "#.#.#.#" };
        public static readonly string[] Pick = { ".####..", "....##.", "...#.#.", "..#..#.", ".#....#", "#......" };
        public static readonly string[] Gem = { "..###..", ".#o###.", "#o#####", ".#####.", "..###..", "...#..." };
        public static readonly string[] Crit = { "...#...", "..###..", ".#.#.#.", "###.###", ".#.#.#.", "..###..", "...#..." };
        public static readonly string[] Glass = { "#######", ".#...#.", "..#.#..", "...#...", "..#.#..", ".#####.", "#######" };
        public static readonly string[] Drop = { "...#...", "..###..", "..###..", ".#o###.", ".#o###.", ".#####.", "..###.." };
        public static readonly string[] Rank = { "...#...", "..###..", ".##.##.", "##.#.##", "..###..", ".##.##.", "##...##" };
        public static readonly string[] Sun = { "#..#..#", ".#...#.", "..###..", "#.###.#", "..###..", ".#...#.", "#..#..#" };

        public static int WidthOf(string[] rows)
        {
            int width = 0;
            foreach (string row in rows) width = Math.Max(width, row.Length);
            return width;
        }

        // Centred on (cx, cy).
        public static void Centered(PixelImage g, int cx, int cy, string[] rows, Rgba color)
        {
            Draw(g, cx - WidthOf(rows) / 2, cy - rows.Length / 2, rows, color);
        }

        public static void Draw(PixelImage g, int x, int y, string[] rows, Rgba color)
        {
            var light = color.Mix(Rgba.White, 0.45f);
            var dark = color.Scale(0.5f);
            for (int j = 0; j < rows.Length; j++)
            {
                for (int i = 0; i < rows[j].Length; i++)
                {
                    char c = rows[j][i];
                    if (c == '#') g.Blend(x + i, y + j, color);
                    else if (c == 'o') g.Blend(x + i, y + j, light.WithAlpha(color.A));
                    else if (c == 'x') g.Blend(x + i, y + j, dark.WithAlpha(color.A));
                }
            }
        }
    }

    // Drawing helpers over a PixelImage.
    static class Px
    {
        public static Rgba Hex(string hex) => Rgba.Hex(hex);

        public static void BlendFill(this PixelImage g, int x, int y, int width, int height, Rgba color)
        {
            for (int j = y; j < y + height; j++)
                for (int i = x; i < x + width; i++) g.Blend(i, j, color);
        }

        // A one-pixel rectangle.
        public static void Box(this PixelImage g, int x, int y, int width, int height, Rgba color)
        {
            g.BlendFill(x, y, width, 1, color);
            g.BlendFill(x, y + height - 1, width, 1, color);
            g.BlendFill(x, y + 1, 1, height - 2, color);
            g.BlendFill(x + width - 1, y + 1, 1, height - 2, color);
        }

        // A frame stretched by its middle, as the game's skin stretches UiArt's frames.
        public static void Slice(this PixelImage g, PixelImage frame, int border, int left, int top, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                int sy = y < border ? y : y >= height - border ? frame.Height - (height - y) : border + (y - border) % (frame.Height - 2 * border);
                for (int x = 0; x < width; x++)
                {
                    int sx = x < border ? x : x >= width - border ? frame.Width - (width - x) : border + (x - border) % (frame.Width - 2 * border);
                    var c = frame[sx, sy];
                    if (c.A > 0) g[left + x, top + y] = c;
                }
            }
        }

        public static void Disc(this PixelImage g, int cx, int cy, float radius, Rgba color)
        {
            int r = (int)MathF.Ceiling(radius);
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    if (x * x + y * y <= radius * radius) g.Blend(cx + x, cy + y, color);
        }

        public static void Ring(this PixelImage g, int cx, int cy, float radius, Rgba color, float thick = 1f)
        {
            int r = (int)MathF.Ceiling(radius);
            float inner = (radius - thick) * (radius - thick);
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    int d = x * x + y * y;
                    if (d <= radius * radius && d > inner) g.Blend(cx + x, cy + y, color);
                }
        }

        public static void Diamond(this PixelImage g, int cx, int cy, int radius, Rgba color)
        {
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                    if (Math.Abs(x) + Math.Abs(y) <= radius) g.Blend(cx + x, cy + y, color);
        }

        // Light falling off from a point.
        public static void Glow(this PixelImage g, int cx, int cy, float radius, Rgba color, int strength)
        {
            int r = (int)MathF.Ceiling(radius);
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    float d = MathF.Sqrt(x * x + y * y) / radius;
                    if (d >= 1) continue;
                    g.Blend(cx + x, cy + y, color.WithAlpha((int)(strength * (1 - d) * (1 - d))));
                }
        }

        public static void Line(this PixelImage g, int x0, int y0, int x1, int y1, Rgba color, int thick = 1, int dash = 0)
        {
            int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx - dy, step = 0;
            while (true)
            {
                if (dash == 0 || step / dash % 2 == 0)
                {
                    if (thick == 1) g.Blend(x0, y0, color);
                    else g.BlendFill(x0 - thick / 2, y0 - thick / 2, thick, thick, color);
                }
                step++;
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        // Text with a dark pixel under it to the lower right, for light text on dark ground; no shadow when the
        // shadow is clear.
        public static int Text(this PixelImage g, int x, int y, string text, Rgba color, Rgba shadow = default, int scale = 1)
        {
            if (shadow.A > 0) MockFont.Draw(g, x + scale, y + scale, text, shadow, scale);
            MockFont.Draw(g, x, y, text, color, scale);
            return MockFont.Width(text, scale);
        }

        public static int Small(this PixelImage g, int x, int y, string text, Rgba color, Rgba shadow = default)
        {
            if (shadow.A > 0) MockFont.DrawSmall(g, x + 1, y + 1, text, shadow);
            MockFont.DrawSmall(g, x, y, text, color);
            return MockFont.SmallWidth(text);
        }

        // Grey bars standing for lines of running text the font cannot write.
        public static void Bars(this PixelImage g, int x, int y, int width, int lines, Rgba color, int salt = 0, int pitch = 8)
        {
            for (int line = 0; line < lines; line++)
            {
                int at = x, word = 0;
                int limit = line == lines - 1 ? width * 2 / 3 : width;
                while (true)
                {
                    int w = 5 + (int)(PixelNoise.Hash(line, word, salt) % 16);
                    if (at + w > x + limit) break;
                    g.BlendFill(at, y + line * pitch, w, 3, color);
                    at += w + 3;
                    word++;
                }
            }
        }

        // A scaled copy of a sprite, each pixel a block.
        public static PixelImage Times(this PixelImage image, int scale)
        {
            var result = new PixelImage(image.Width * scale, image.Height * scale);
            for (int y = 0; y < result.Height; y++)
                for (int x = 0; x < result.Width; x++) result[x, y] = image[x / scale, y / scale];
            return result;
        }

        // The shape of a sprite in one colour, for an empty slot's hint.
        public static PixelImage Shape(this PixelImage image, Rgba color)
        {
            var result = new PixelImage(image.Width, image.Height);
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    if (image.IsSet(x, y)) result[x, y] = color;
            return result;
        }

        // A raised plate: outline, a rim, a lit top row, a dark bottom row.
        public static void Raised(this PixelImage g, int x, int y, int w, int h, Rgba outline, Rgba rim, Ramp fill, bool sunk = false)
        {
            g.Fill(x + 1, y, w - 2, h, outline);
            g.Fill(x, y + 1, w, h - 2, outline);
            g.Fill(x + 1, y + 1, w - 2, h - 2, rim);
            g.Fill(x + 1, y + h - 2, w - 2, 1, rim.Mix(outline, 0.5f));
            g.Fill(x + 2, y + 2, w - 4, h - 4 - (sunk ? 0 : 1), fill.Main);
            g.Fill(x + 2, sunk ? y + h - 3 : y + 2, w - 4, 1, fill.Light);
            g.Fill(x + 2, sunk ? y + 2 : y + h - 4, w - 4, 1, fill.Shade);
        }

        // A well: dark inside, its shadow at the top left and light at the bottom right.
        public static void Sunk(this PixelImage g, int x, int y, int w, int h, Rgba fill, Rgba shadow, Rgba light)
        {
            g.Fill(x, y, w, h, fill);
            g.Fill(x, y, w, 1, shadow);
            g.Fill(x, y, 1, h, shadow);
            g.Fill(x + 1, y + h - 1, w - 1, 1, light);
            g.Fill(x + w - 1, y + 1, 1, h - 1, light);
        }
    }

    enum MockStyle
    {
        // The cave stone panel extended: three levels of panel, a title ribbon, icon tabs, rarity rims, a close button.
        A,
        // A material for each kind of window.
        B,
        // Minimal: translucent dark, colour only as an accent.
        C,
        // The recommended mix: A's frames and rules, B's material in the header and the floor of each kind of window,
        // and the passive tree alone as a night sky.
        R,
    }

    enum MockKind
    {
        Bag,
        Shop,
        Talk,
        Growth,
        Journal,
        Machine,
        Hud,
    }

    // One candidate look: how it draws a window, a well, a slot, a floating card, a button and a tab.
    sealed class MockSkin
    {
        public readonly MockStyle Style;
        public static readonly Rgba Black = Px.Hex("#0B0807");
        public static readonly Rgba GoldLight = Px.Hex("#F4DE9C");
        public static readonly Rgba Gold = Px.Hex("#C49A55");
        public static readonly Rgba GoldDark = Px.Hex("#6B5230");
        public static readonly Rgba Good = Px.Hex("#7CE06A");
        public static readonly Rgba Bad = Px.Hex("#F0605A");
        public static readonly Rgba[] RarityColors =
        {
            Px.Hex("#A8A096"), Px.Hex("#73F273"), Px.Hex("#73B3FF"), Px.Hex("#CC80FF"), Px.Hex("#FF9E2E"),
        };

        public MockSkin(MockStyle style) => Style = style;

        bool Stone => Style == MockStyle.A || Style == MockStyle.R;

        // ---- Colours of the writing on each ground ----

        public Rgba Ink(MockKind kind) => Style switch
        {
            MockStyle.B => kind switch
            {
                MockKind.Journal => Px.Hex("#3A2A1A"),
                MockKind.Growth => Px.Hex("#E4E8FF"),
                MockKind.Machine => Px.Hex("#EEF2F6"),
                _ => Px.Hex("#FBEED2"),
            },
            MockStyle.C => Rgba.White,
            _ => Px.Hex("#EFE4CC"),
        };

        public Rgba Dim(MockKind kind) => Style switch
        {
            MockStyle.B => kind switch
            {
                MockKind.Journal => Px.Hex("#7A6444"),
                MockKind.Growth => Px.Hex("#8E94C8"),
                MockKind.Machine => Px.Hex("#A4AEBA"),
                MockKind.Shop => Px.Hex("#D8B890"),
                _ => Px.Hex("#D0AE8A"),
            },
            MockStyle.C => Px.Hex("#A6A8B2"),
            _ => Px.Hex("#A8977C"),
        };

        // The shadow under light writing (clear on parchment, where the ink is dark).
        public Rgba Shadow(MockKind kind) => Style == MockStyle.B && kind == MockKind.Journal ? Rgba.Clear : Style == MockStyle.C ? Rgba.Clear : new Rgba(0, 0, 0, 150);

        public Rgba Title(MockKind kind) => Style switch
        {
            MockStyle.C => Rgba.White,
            MockStyle.B => kind == MockKind.Journal ? Px.Hex("#5A2A1A") : kind == MockKind.Growth ? Px.Hex("#CFE0FF") : Px.Hex("#FFE9B0"),
            _ => GoldLight,
        };

        public Rgba Accent(MockKind kind) => kind switch
        {
            MockKind.Bag => Px.Hex("#F2B84A"),
            MockKind.Shop => Px.Hex("#6AD08A"),
            MockKind.Growth => Px.Hex("#9A84FF"),
            MockKind.Journal => Px.Hex("#E8D8A8"),
            MockKind.Machine => Px.Hex("#6AC8E8"),
            _ => Px.Hex("#F2B84A"),
        };

        // ---- Materials (B) ----

        static void Leather(PixelImage g, int x, int y, int w, int h, float dark = 0)
        {
            var baseColor = Px.Hex("#7C5438").Mix(Px.Hex("#2A1A10"), dark);
            var deep = Px.Hex("#553623").Mix(Px.Hex("#1A100A"), dark);
            var pale = Px.Hex("#93684A").Mix(Px.Hex("#2A1A10"), dark);
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    float n = PixelNoise.Value((x + i) / 3, (y + j) / 2, 11) * 0.6f + PixelNoise.Value(x + i, y + j, 12) * 0.4f;
                    var c = n > 0.78f ? pale : n < 0.3f ? deep.Mix(baseColor, 0.5f) : baseColor;
                    if (PixelNoise.Value((x + i) / 9, y + j, 13) > 0.985f) c = pale;
                    g[x + i, y + j] = c;
                }
            }
        }

        static void Wood(PixelImage g, int x, int y, int w, int h, float dark = 0, int board = 18)
        {
            var baseColor = Px.Hex("#7A5230").Mix(Px.Hex("#22140A"), dark);
            var deep = Px.Hex("#684426").Mix(Px.Hex("#1A0E06"), dark);
            var pale = Px.Hex("#8A6038").Mix(Px.Hex("#22140A"), dark);
            var gap = Px.Hex("#3E2614").Mix(Px.Hex("#120A04"), dark);
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    int bx = (x + i) % board, plank = (x + i) / board;
                    float n = PixelNoise.Value(x + i, (y + j + plank * 7) / 16, 21 + plank);
                    var c = n > 0.9f ? pale : n < 0.14f ? deep : baseColor;
                    if (plank % 2 == 1) c = c.Mix(deep, 0.35f);
                    if (bx == 0) c = gap;
                    else if (bx == 1) c = Px.Hex("#9A6E44").Mix(Px.Hex("#22140A"), dark);
                    // A nail near each board's ends.
                    g[x + i, y + j] = c;
                }
            }
        }

        static void Parchment(PixelImage g, int x, int y, int w, int h)
        {
            var baseColor = Px.Hex("#E6D6AA");
            var stain = Px.Hex("#D2BE8A");
            var burnt = Px.Hex("#A88A52");
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    float n = PixelNoise.Value((x + i) / 4, (y + j) / 3, 31) * 0.7f + PixelNoise.Value(x + i, y + j, 32) * 0.3f;
                    var c = n > 0.72f ? stain : n < 0.12f ? Px.Hex("#F0E4BE") : baseColor;
                    int edge = Math.Min(Math.Min(i, w - 1 - i), Math.Min(j, h - 1 - j));
                    if (edge < 3) c = c.Mix(burnt, (3 - edge) * 0.22f);
                    g[x + i, y + j] = c;
                }
            }
        }

        static void Steel(PixelImage g, int x, int y, int w, int h, float dark = 0)
        {
            var baseColor = Px.Hex("#5C6672").Mix(Px.Hex("#14181E"), dark);
            var pale = Px.Hex("#74808E").Mix(Px.Hex("#14181E"), dark);
            var deep = Px.Hex("#4A535E").Mix(Px.Hex("#14181E"), dark);
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    float n = PixelNoise.Value((x + i) / 14, y + j, 41);
                    g[x + i, y + j] = n > 0.92f ? pale : n < 0.1f ? deep : baseColor;
                }
            }
        }

        // The night sky of the passive tree: deep blue running to violet, a few clouds of light and stars.
        public static void Night(PixelImage g, int x, int y, int w, int h, int alpha = 255)
        {
            var top = Px.Hex("#070A1E");
            var bottom = Px.Hex("#1C1240");
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    float t = (float)j / h;
                    float cloud = PixelNoise.Value((x + i) / 24, (y + j) / 18, 51) * 0.5f + PixelNoise.Value((x + i) / 9, (y + j) / 7, 52) * 0.3f;
                    var c = top.Mix(bottom, t);
                    if (cloud > 0.5f) c = c.Mix(Px.Hex("#3A2A78"), (cloud - 0.5f) * 0.9f);
                    // Banded, so the sky reads as drawn pixels rather than a smooth wash.
                    c = new Rgba(c.R / 6 * 6, c.G / 6 * 6, c.B / 6 * 6);
                    g.Blend(x + i, y + j, c.WithAlpha(alpha));
                    uint hash = PixelNoise.Hash(x + i, y + j, 53);
                    if (hash % 190 == 0) g.Blend(x + i, y + j, (hash / 190 % 3 == 0 ? Px.Hex("#FFFFFF") : hash / 190 % 3 == 1 ? Px.Hex("#9AB4FF") : Px.Hex("#6A72B8")).WithAlpha(alpha));
                    if (hash % 2900 == 7)
                    {
                        var star = Px.Hex("#DCE6FF").WithAlpha(alpha);
                        g.Blend(x + i - 1, y + j, star);
                        g.Blend(x + i + 1, y + j, star);
                        g.Blend(x + i, y + j - 1, star);
                        g.Blend(x + i, y + j + 1, star);
                        g.Blend(x + i, y + j, Rgba.White.WithAlpha(alpha));
                    }
                }
            }
        }

        static void Bolt(PixelImage g, int x, int y)
        {
            g.Fill(x, y, 3, 3, Px.Hex("#8A96A4"));
            g[x, y] = Px.Hex("#D8E0E8");
            g[x + 2, y + 2] = Px.Hex("#2A3038");
            g[x + 1, y + 1] = Px.Hex("#5A6470");
        }

        static void Stud(PixelImage g, int x, int y)
        {
            g.Fill(x, y, 3, 3, Px.Hex("#C49A55"));
            g[x, y] = Px.Hex("#FFF0B8");
            g[x + 2, y + 2] = Px.Hex("#6B4A20");
        }

        // A brass plate over a corner, cut on the slant.
        static void CornerPlate(PixelImage g, int x, int y, int sx, int sy, int size = 9)
        {
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size - j; i++)
                {
                    var c = i + j == size - 1 ? Px.Hex("#6B4A20") : i == 0 || j == 0 ? Px.Hex("#F0D488") : Px.Hex("#C49A55");
                    g[x + sx * i, y + sy * j] = c;
                }
            }
            g[x + sx * 2, y + sy * 2] = Px.Hex("#5A3C18");
            g[x + sx * 3, y + sy * 2] = Px.Hex("#FFF0B8");
        }

        static void Stitch(PixelImage g, int x, int y, int w, int h, Rgba thread, Rgba hole)
        {
            for (int i = 0; i < w; i++)
            {
                if (i % 5 < 3)
                {
                    g[x + i, y] = thread;
                    g[x + i, y + h - 1] = thread;
                }
                else if (i % 5 == 3)
                {
                    g[x + i, y] = hole;
                    g[x + i, y + h - 1] = hole;
                }
            }
            for (int j = 0; j < h; j++)
            {
                if (j % 5 < 3)
                {
                    g[x, y + j] = thread;
                    g[x + w - 1, y + j] = thread;
                }
                else if (j % 5 == 3)
                {
                    g[x, y + j] = hole;
                    g[x + w - 1, y + j] = hole;
                }
            }
        }

        // The striped cloth over a stall, scalloped along its lower edge.
        public static void Awning(PixelImage g, int x, int y, int w, int h)
        {
            const int stripe = 10;
            for (int i = 0; i < w; i++)
            {
                bool red = i / stripe % 2 == 0;
                var ramp = red ? Ramp.Of("#C8483A") : Ramp.Of("#EEDFC0");
                int inStripe = i % stripe;
                // Each stripe ends in a rounded tongue.
                int cut = inStripe == 0 || inStripe == stripe - 1 ? 3 : inStripe == 1 || inStripe == stripe - 2 ? 1 : 0;
                for (int j = 0; j < h - cut; j++)
                {
                    var c = j == 0 ? ramp.Light : j >= h - cut - 2 ? ramp.Shade : ramp.Main;
                    if (inStripe == 0 && j < h - 3) c = ramp.Shade;
                    g[x + i, y + j] = c;
                }
                g.Blend(x + i, y + h - cut, new Rgba(0, 0, 0, 110));
                g.Blend(x + i, y + h - cut + 1, new Rgba(0, 0, 0, 60));
            }
        }

        static void Hazard(PixelImage g, int x, int y, int w, int h)
        {
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    g[x + i, y + j] = (i + j) / 4 % 2 == 0 ? Px.Hex("#E8B830") : Px.Hex("#2A2420");
        }

        public static void WaxSeal(PixelImage g, int cx, int cy)
        {
            g.Disc(cx + 1, cy + 1, 7.4f, new Rgba(0, 0, 0, 70));
            g.Disc(cx, cy, 7.4f, Px.Hex("#8E1C1C"));
            g.Disc(cx, cy, 6.2f, Px.Hex("#C0302C"));
            g.Disc(cx - 1, cy - 1, 4.4f, Px.Hex("#D8483C"));
            MockGlyph.Centered(g, cx, cy, MockGlyph.Star, Px.Hex("#7A1414"));
            g[cx - 4, cy - 4] = Px.Hex("#F08A78");
            g[cx - 3, cy - 5] = Px.Hex("#F08A78");
            // A drip.
            g.Disc(cx + 5, cy + 6, 1.6f, Px.Hex("#C0302C"));
        }

        // ---- Level 1: a window ----

        // Draws the window and its head, and gives the top of its content (below the head).
        public int Window(PixelImage g, int x, int y, int w, int h, MockKind kind, string title, string[] icon, Rgba? accent = null, bool close = true)
        {
            switch (Style)
            {
                case MockStyle.A:
                    StonePanel(g, x, y, w, h);
                    if (title != null) Ribbon(g, x, y, w, title, icon);
                    if (close) CloseStone(g, x + w - 12, y - 5);
                    return y + (title != null ? 15 : 7);
                case MockStyle.R:
                    StonePanel(g, x, y, w, h, kind);
                    HeadStrip(g, x + 4, y + 4, w - 8, 17, kind, title, icon, accent);
                    if (close) CloseStone(g, x + w - 18, y + 6);
                    return y + 25;
                case MockStyle.B:
                    return MaterialWindow(g, x, y, w, h, kind, title, icon, accent, close);
                default:
                    g.BlendFill(x, y, w, h, new Rgba(8, 8, 12, 196));
                    g.BlendFill(x, y, w, 2, accent ?? Accent(kind));
                    if (title != null)
                    {
                        g.Fill(x + 9, y + 8, 2, 9, accent ?? Accent(kind));
                        g.Text(x + 15, y + 9, title, Rgba.White);
                        g.BlendFill(x + 9, y + 22, w - 18, 1, new Rgba(255, 255, 255, 36));
                    }
                    if (close) MockGlyph.Draw(g, x + w - 18, y + 8, MockGlyph.Close, new Rgba(255, 255, 255, 200));
                    return y + (title != null ? 28 : 8);
            }
        }

        void StonePanel(PixelImage g, int x, int y, int w, int h, MockKind? floor = null)
        {
            g.BlendFill(x + 3, y + 4, w, h, new Rgba(0, 0, 0, 110));
            g.Slice(UiArt.Panel, UiArt.PanelBorder, x, y, w, h);
            // Stone: a few darker and lighter grains, and with a floor (the mix) a hint of the window's material.
            for (int j = 4; j < h - 4; j++)
            {
                for (int i = 4; i < w - 4; i++)
                {
                    float n = PixelNoise.Value(x + i, y + j, 3);
                    if (floor == MockKind.Bag)
                    {
                        float m = PixelNoise.Value((x + i) / 3, (y + j) / 2, 11);
                        g[x + i, y + j] = g[x + i, y + j].Mix(Px.Hex("#4A2E1C"), m > 0.7f ? 0.55f : 0.36f);
                    }
                    else if (floor == MockKind.Shop)
                    {
                        g[x + i, y + j] = g[x + i, y + j].Mix(Px.Hex("#553618"), (x + i) % 18 == 0 ? 0f : 0.42f);
                        if ((x + i) % 18 == 0) g[x + i, y + j] = Px.Hex("#14100C");
                    }
                    else if (floor == MockKind.Journal)
                    {
                        g[x + i, y + j] = g[x + i, y + j].Mix(Px.Hex("#5A4A30"), 0.3f);
                    }
                    else if (floor == MockKind.Machine)
                    {
                        g[x + i, y + j] = g[x + i, y + j].Mix(Px.Hex("#2E363E"), 0.45f);
                    }
                    if (n > 0.95f) g.Shade(x + i, y + j, 0.82f);
                    else if (n < 0.035f) g.Shade(x + i, y + j, 1.22f);
                }
            }
            // A small gold bracket inside each corner.
            foreach (var (cx, cy, sx, sy) in new[] { (x + 6, y + 6, 1, 1), (x + w - 7, y + 6, -1, 1), (x + 6, y + h - 7, 1, -1), (x + w - 7, y + h - 7, -1, -1) })
            {
                for (int i = 0; i < 5; i++)
                {
                    g[cx + sx * i, cy] = GoldDark;
                    g[cx, cy + sy * i] = GoldDark;
                }
                g[cx, cy] = Gold;
            }
        }

        // A crimson banner with gold edging, hung over the top edge, its tails folded behind.
        static void Ribbon(PixelImage g, int x, int y, int w, string title, string[] icon)
        {
            int text = MockFont.Width(title);
            int iconW = icon != null ? MockGlyph.WidthOf(icon) + 5 : 0;
            int rw = text + iconW + 30, rx = x + (w - rw) / 2, ry = y - 7;
            const int rh = 16;
            var dark = Px.Hex("#4E1814");
            // Tails.
            foreach (int side in new[] { -1, 1 })
            {
                int tx = side < 0 ? rx - 8 : rx + rw;
                for (int j = 0; j < rh - 3; j++)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        int fromTip = side < 0 ? i : 7 - i;
                        // The swallowtail's notch.
                        if (fromTip < 4 - Math.Abs(j - (rh - 3) / 2)) continue;
                        g[tx + i, ry + 4 + j] = j == 0 ? Px.Hex("#7A2A22") : dark;
                    }
                }
                g.Fill(side < 0 ? rx - 1 : rx + rw, ry + rh, 1, 1, Black);
            }
            g.Fill(rx, ry, rw, rh, Black);
            g.Fill(rx + 1, ry + 1, rw - 2, rh - 2, Px.Hex("#8E2E28"));
            g.Fill(rx + 1, ry + 1, rw - 2, 1, GoldLight);
            g.Fill(rx + 1, ry + 2, rw - 2, 1, Px.Hex("#B44A3C"));
            g.Fill(rx + 1, ry + rh - 3, rw - 2, 1, Px.Hex("#641E1C"));
            g.Fill(rx + 1, ry + rh - 2, rw - 2, 1, Gold);
            int at = rx + (rw - text - iconW) / 2;
            if (icon != null) MockGlyph.Draw(g, at, ry + 2 + (12 - icon.Length) / 2, icon, GoldLight);
            g.Text(at + iconW, ry + 5, title, Px.Hex("#FFF2CC"), Px.Hex("#3E100E"));
        }

        static void CloseStone(PixelImage g, int x, int y)
        {
            g.Raised(x, y, 13, 13, Black, GoldLight, Ramp.Of("#A8382E"));
            MockGlyph.Draw(g, x + 4, y + 4, MockGlyph.SmallX, Px.Hex("#FFF2DC"));
        }

        // The head of a window in the mix: a strip of the window's material inside the gold rim.
        void HeadStrip(PixelImage g, int x, int y, int w, int h, MockKind kind, string title, string[] icon, Rgba? accent)
        {
            var ink = Px.Hex("#FFF2CC");
            var shadow = new Rgba(0, 0, 0, 170);
            switch (kind)
            {
                case MockKind.Bag:
                    Leather(g, x, y, w, h);
                    Stitch(g, x + 2, y + 2, w - 4, h - 4, Px.Hex("#E6C898"), Px.Hex("#4A2E1C"));
                    Stud(g, x + 6, y + 7);
                    Stud(g, x + w - 9, y + 7);
                    break;
                case MockKind.Shop:
                    Wood(g, x, y, w, h, 0.25f);
                    Awning(g, x, y, w, 9);
                    break;
                case MockKind.Journal:
                    Parchment(g, x, y, w, h);
                    ink = Px.Hex("#4A2A1A");
                    shadow = Rgba.Clear;
                    break;
                case MockKind.Machine:
                    Steel(g, x, y, w, h);
                    Hazard(g, x, y, w, 3);
                    Bolt(g, x + 3, y + 8);
                    Bolt(g, x + w - 6, y + 8);
                    break;
                case MockKind.Growth:
                    Night(g, x, y, w, h);
                    ink = Px.Hex("#DCE6FF");
                    break;
                default:
                    var plate = Ramp.Of(accent ?? Gold);
                    g.Fill(x, y, w, h, plate.Shade);
                    g.Fill(x, y, w, 1, plate.Light);
                    break;
            }
            g.Fill(x, y + h, w, 1, Black);
            g.Fill(x, y + h + 1, w, 1, GoldDark);
            if (title == null) return;
            int ty = kind == MockKind.Shop ? y + 9 : y + 5;
            int whole = MockFont.Width(title) + (icon != null ? MockGlyph.WidthOf(icon) + 5 : 0);
            // A speaker's name stands at the left; the name of a window in the middle.
            int at = kind == MockKind.Talk || kind == MockKind.Hud ? x + 8 : x + (w - whole) / 2;
            if (kind == MockKind.Shop)
            {
                // A sign board hung under the awning.
                int tw = MockFont.Width(title) + (icon != null ? 16 : 0) + 10;
                int sx = x + (w - tw) / 2;
                g.Fill(sx, y + 8, tw, 10, Black);
                g.Fill(sx + 1, y + 8, tw - 2, 9, Px.Hex("#3A2414"));
                at = sx + 5;
                ty = y + 9;
                if (icon != null)
                {
                    MockGlyph.Draw(g, at, ty - 1, icon.Length > 8 ? new[] { icon[0], icon[1], icon[2], icon[3], icon[5], icon[6], icon[7], icon[9] } : icon, ink);
                    at += 15;
                }
                g.Text(at, ty, title, ink, shadow);
                return;
            }
            if (icon != null)
            {
                if (shadow.A > 0) MockGlyph.Draw(g, at + 1, y + (h - icon.Length) / 2 + 1, icon, new Rgba(20, 10, 6));
                MockGlyph.Draw(g, at, y + (h - icon.Length) / 2, icon, ink);
                at += MockGlyph.WidthOf(icon) + 5;
            }
            g.Text(at, ty, title, ink, shadow);
        }

        int MaterialWindow(PixelImage g, int x, int y, int w, int h, MockKind kind, string title, string[] icon, Rgba? accent, bool close)
        {
            g.BlendFill(x + 3, y + 4, w, h, new Rgba(0, 0, 0, 110));
            var outline = Px.Hex("#120A06");
            switch (kind)
            {
                case MockKind.Bag:
                {
                    g.Fill(x + 1, y, w - 2, h, outline);
                    g.Fill(x, y + 1, w, h - 2, outline);
                    Leather(g, x + 1, y + 1, w - 2, h - 2);
                    g.Box(x + 1, y + 1, w - 2, h - 2, Px.Hex("#4A2E1E"));
                    g.BlendFill(x + 2, y + 2, w - 4, 1, new Rgba(255, 230, 190, 70));
                    Stitch(g, x + 4, y + 4, w - 8, h - 8, Px.Hex("#E6C898"), Px.Hex("#3E2616"));
                    CornerPlate(g, x + 1, y + 1, 1, 1);
                    CornerPlate(g, x + w - 2, y + 1, -1, 1);
                    CornerPlate(g, x + 1, y + h - 2, 1, -1);
                    CornerPlate(g, x + w - 2, y + h - 2, -1, -1);
                    // The strap across the top, with a brass buckle holding the title.
                    int sy = y + 8;
                    Leather(g, x + 1, sy, w - 2, 16, 0.42f);
                    g.Fill(x + 1, sy - 1, w - 2, 1, Px.Hex("#2A180E"));
                    g.Fill(x + 1, sy + 16, w - 2, 1, Px.Hex("#2A180E"));
                    g.BlendFill(x + 1, sy, w - 2, 1, new Rgba(255, 220, 170, 60));
                    for (int i = 6; i < w - 6; i += 5)
                    {
                        g[x + i, sy + 2] = Px.Hex("#C8A878");
                        g[x + i + 1, sy + 2] = Px.Hex("#C8A878");
                        g[x + i, sy + 13] = Px.Hex("#C8A878");
                        g[x + i + 1, sy + 13] = Px.Hex("#C8A878");
                    }
                    int tw = MockFont.Width(title ?? "") + (icon != null ? 16 : 0) + 14;
                    int bx = x + (w - tw) / 2;
                    g.Raised(bx, sy - 2, tw, 20, outline, Px.Hex("#F0D488"), Ramp.Of("#B88A3A"));
                    g.Sunk(bx + 3, sy + 1, tw - 6, 14, Px.Hex("#3A2416"), Px.Hex("#1E120A"), Px.Hex("#7A5A2A"));
                    int at = bx + 7;
                    if (icon != null)
                    {
                        MockGlyph.Draw(g, at, sy + 8 - icon.Length / 2, icon, Px.Hex("#FFE9B0"));
                        at += 16;
                    }
                    if (title != null) g.Text(at, sy + 5, title, Px.Hex("#FFE9B0"), new Rgba(0, 0, 0, 160));
                    if (close) SnapClose(g, x + w - 21, sy + 1, Ramp.Of("#B88A3A"), Px.Hex("#F0D488"));
                    return y + 30;
                }
                case MockKind.Shop:
                {
                    g.Fill(x, y + 2, w, h - 2, outline);
                    Wood(g, x + 1, y + 3, w - 2, h - 4);
                    g.Box(x + 1, y + 3, w - 2, h - 4, Px.Hex("#5A3A20"));
                    g.Box(x + 2, y + 4, w - 4, h - 6, Px.Hex("#B88858"));
                    g.BlendFill(x + 3, y + 5, w - 6, h - 8, new Rgba(0, 0, 0, 0));
                    foreach (int nx in new[] { x + 5, x + w - 7 })
                    {
                        foreach (int ny in new[] { y + 22, y + h - 7 })
                        {
                            g[nx, ny] = Px.Hex("#D8D0C0");
                            g[nx + 1, ny + 1] = Px.Hex("#2A180C");
                            g[nx + 1, ny] = Px.Hex("#6A6258");
                            g[nx, ny + 1] = Px.Hex("#6A6258");
                        }
                    }
                    // The awning hangs over the top, a little wider than the window.
                    g.Fill(x - 4, y - 2, w + 8, 2, outline);
                    g.Fill(x - 3, y - 1, w + 6, 1, Px.Hex("#8A6A48"));
                    Awning(g, x - 3, y, w + 6, 14);
                    // The sign under it.
                    int tw = MockFont.Width(title ?? "") + (icon != null ? 16 : 0) + 16;
                    int bx = x + (w - tw) / 2, by = y + 13;
                    g.Fill(bx + 6, by - 3, 1, 4, Px.Hex("#2A2420"));
                    g.Fill(bx + tw - 7, by - 3, 1, 4, Px.Hex("#2A2420"));
                    g.Raised(bx, by, tw, 17, outline, Px.Hex("#C89A68"), Ramp.Of("#4A2E18"));
                    int at = bx + 8;
                    if (icon != null)
                    {
                        MockGlyph.Draw(g, at, by + 8 - icon.Length / 2, icon, Px.Hex("#FFE9B0"));
                        at += 16;
                    }
                    if (title != null) g.Text(at, by + 5, title, Px.Hex("#FFE9B0"), new Rgba(0, 0, 0, 160));
                    if (close) SnapClose(g, x + w - 19, y + 17, Ramp.Of("#7A4E2A"), Px.Hex("#C89A68"));
                    return y + 34;
                }
                case MockKind.Journal:
                {
                    Parchment(g, x, y, w, h);
                    // A torn edge: bites out of the border.
                    var edge = Px.Hex("#6E5632");
                    for (int i = 0; i < w; i++)
                    {
                        bool bite = PixelNoise.Hash(i / 3, 0, 61) % 5 == 0;
                        if (bite) g[x + i, y] = Rgba.Clear;
                        g[x + i, y + (bite ? 1 : 0)] = edge;
                        bite = PixelNoise.Hash(i / 3, 1, 61) % 5 == 0;
                        if (bite) g[x + i, y + h - 1] = Rgba.Clear;
                        g[x + i, y + h - 1 - (bite ? 1 : 0)] = edge;
                    }
                    for (int j = 0; j < h; j++)
                    {
                        bool bite = PixelNoise.Hash(j / 3, 2, 61) % 5 == 0;
                        if (bite) g[x, y + j] = Rgba.Clear;
                        g[x + (bite ? 1 : 0), y + j] = edge;
                        bite = PixelNoise.Hash(j / 3, 3, 61) % 5 == 0;
                        if (bite) g[x + w - 1, y + j] = Rgba.Clear;
                        g[x + w - 1 - (bite ? 1 : 0), y + j] = edge;
                    }
                    int at = x + 12;
                    if (icon != null)
                    {
                        MockGlyph.Draw(g, at, y + 14 - icon.Length / 2, icon, Px.Hex("#5A2A1A"));
                        at += 16;
                    }
                    if (title != null) g.Text(at, y + 11, title, Px.Hex("#5A2A1A"));
                    g.Fill(x + 10, y + 23, w - 20, 1, Px.Hex("#8A6E44"));
                    g.Fill(x + 10, y + 25, w - 20, 1, Px.Hex("#B89A66"));
                    WaxSeal(g, x + w - 18, y + 14);
                    return y + 32;
                }
                case MockKind.Machine:
                {
                    g.Fill(x + 1, y, w - 2, h, outline);
                    g.Fill(x, y + 1, w, h - 2, outline);
                    Steel(g, x + 1, y + 1, w - 2, h - 2);
                    g.Fill(x + 1, y + 1, w - 2, 1, Px.Hex("#A6B2C0"));
                    g.Fill(x + 1, y + 1, 1, h - 2, Px.Hex("#A6B2C0"));
                    g.Fill(x + 1, y + h - 2, w - 2, 1, Px.Hex("#2E363E"));
                    g.Fill(x + w - 2, y + 1, 1, h - 2, Px.Hex("#2E363E"));
                    Hazard(g, x + 2, y + 2, w - 4, 5);
                    g.Fill(x + 2, y + 7, w - 4, 1, Px.Hex("#2E363E"));
                    foreach (int bx in new[] { x + 5, x + w - 8 })
                        foreach (int by in new[] { y + 11, y + h - 8 }) Bolt(g, bx, by);
                    int tw = MockFont.Width(title ?? "") + (icon != null ? 16 : 0) + 12;
                    g.Sunk(x + 12, y + 11, tw, 15, Px.Hex("#262C34"), Px.Hex("#14181E"), Px.Hex("#8A96A4"));
                    int at = x + 18;
                    if (icon != null)
                    {
                        MockGlyph.Draw(g, at, y + 18 - icon.Length / 2, icon, Px.Hex("#9AE0F4"));
                        at += 16;
                    }
                    if (title != null) g.Text(at, y + 15, title, Px.Hex("#D8F2FA"), new Rgba(0, 0, 0, 160));
                    if (close) SnapClose(g, x + w - 24, y + 12, Ramp.Of("#5C6672"), Px.Hex("#A6B2C0"));
                    return y + 31;
                }
                case MockKind.Growth:
                {
                    g.Fill(x, y, w, h, Px.Hex("#05061A"));
                    Night(g, x + 1, y + 1, w - 2, h - 2);
                    g.Box(x + 1, y + 1, w - 2, h - 2, Px.Hex("#8A8CD0"));
                    g.Box(x + 2, y + 2, w - 4, h - 4, Px.Hex("#2A2A66"));
                    foreach (var (cx, cy) in new[] { (x + 1, y + 1), (x + w - 2, y + 1), (x + 1, y + h - 2), (x + w - 2, y + h - 2) })
                        g.Diamond(cx, cy, 2, Px.Hex("#DCE6FF"));
                    if (title != null) g.Text(x + 12, y + 9, title, Px.Hex("#DCE6FF"), new Rgba(0, 0, 0, 160));
                    if (close) SnapClose(g, x + w - 20, y + 6, Ramp.Of("#2A2A66"), Px.Hex("#8A8CD0"));
                    return y + 24;
                }
                default:
                {
                    // Talking and the HUD: a dark, quiet panel whose rim is the speaker's colour.
                    var rim = Ramp.Of(accent ?? Gold);
                    g.Fill(x + 1, y, w - 2, h, outline);
                    g.Fill(x, y + 1, w, h - 2, outline);
                    g.Fill(x + 1, y + 1, w - 2, h - 2, rim.Main);
                    g.Fill(x + 1, y + 1, w - 2, 1, rim.Light);
                    g.Fill(x + 1, y + h - 2, w - 2, 1, rim.Dark);
                    g.Fill(x + 2, y + 2, w - 4, h - 4, Px.Hex("#221C1A").Mix(rim.Main, 0.08f));
                    g.Fill(x + 2, y + 2, w - 4, 1, Px.Hex("#0E0A08"));
                    g.Fill(x + 3, y + 3, w - 6, 1, Px.Hex("#3A302C").Mix(rim.Main, 0.1f));
                    if (close) SnapClose(g, x + w - 17, y + 5, Ramp.Of("#3A302C"), rim.Main);
                    return y + 7;
                }
            }
        }

        static void SnapClose(PixelImage g, int x, int y, Ramp fill, Rgba rim)
        {
            g.Raised(x, y, 13, 13, Px.Hex("#120A06"), rim, fill);
            MockGlyph.Draw(g, x + 4, y + 4, MockGlyph.SmallX, Px.Hex("#FFF2DC"));
        }

        // ---- Level 2: a well inside a window ----

        public void Well(PixelImage g, int x, int y, int w, int h, MockKind kind)
        {
            if (Stone)
            {
                g.Slice(UiArt.Inset, UiArt.InsetBorder, x, y, w, h);
                return;
            }
            if (Style == MockStyle.C)
            {
                g.BlendFill(x, y, w, h, new Rgba(255, 255, 255, 16));
                return;
            }
            switch (kind)
            {
                case MockKind.Bag:
                    g.Sunk(x, y, w, h, Px.Hex("#35221A"), Px.Hex("#1C100A"), Px.Hex("#A87A52"));
                    g.Fill(x + 1, y + 1, w - 2, 1, Px.Hex("#281810"));
                    break;
                case MockKind.Shop:
                    g.Sunk(x, y, w, h, Px.Hex("#3A2616"), Px.Hex("#1E1208"), Px.Hex("#B88858"));
                    g.Fill(x + 1, y + 1, w - 2, 1, Px.Hex("#2A1A0E"));
                    break;
                case MockKind.Journal:
                    g.BlendFill(x, y, w, h, new Rgba(120, 90, 40, 46));
                    g.Box(x, y, w, h, Px.Hex("#9A7E50"));
                    break;
                case MockKind.Growth:
                    g.BlendFill(x, y, w, h, new Rgba(4, 4, 20, 190));
                    g.Box(x, y, w, h, Px.Hex("#4A4C96"));
                    break;
                case MockKind.Machine:
                    g.Sunk(x, y, w, h, Px.Hex("#23282E"), Px.Hex("#12161A"), Px.Hex("#8A96A4"));
                    break;
                default:
                    g.Sunk(x, y, w, h, Px.Hex("#16110F"), Px.Hex("#0A0806"), Px.Hex("#4A3C30"));
                    break;
            }
        }

        // ---- A slot: 22 by 22 with a 16 by 16 picture ----

        public const int SlotSize = 22;

        // rarity: -1 for none, 0 common to 4 legendary.
        public void Slot(PixelImage g, int x, int y, MockKind kind, PixelImage icon, int rarity = -1, string count = null, bool chosen = false, bool hover = false, string[] hint = null)
        {
            const int s = SlotSize;
            var tint = rarity >= 0 ? RarityColors[rarity] : default;
            if (Style == MockStyle.C)
            {
                g.BlendFill(x, y, s - 1, s - 1, new Rgba(255, 255, 255, hover ? 52 : 22));
                if (rarity > 0)
                {
                    g.BlendFill(x, y, s - 1, s - 1, tint.WithAlpha(30));
                    g.BlendFill(x, y + s - 3, s - 1, 2, tint);
                }
                if (chosen || hover) g.Box(x, y, s - 1, s - 1, Rgba.White);
            }
            else
            {
                Well(g, x, y, s, s, kind);
                if (rarity > 0)
                {
                    g.BlendFill(x + 2, y + 2, s - 4, s - 4, tint.WithAlpha(rarity >= 3 ? 44 : 30));
                    g.Box(x + 1, y + 1, s - 2, s - 2, tint.Scale(0.78f));
                    g.BlendFill(x + 1, y + 1, s - 2, 1, tint.WithAlpha(120));
                    // Bright corners, so the rim reads as set stone rather than a plain line.
                    foreach (var (cx, cy) in new[] { (x + 1, y + 1), (x + s - 2, y + 1), (x + 1, y + s - 2), (x + s - 2, y + s - 2) }) g[cx, cy] = tint.Mix(Rgba.White, 0.5f);
                    if (rarity >= 3)
                    {
                        g.BlendFill(x + 2, y + s - 5, s - 4, 3, tint.WithAlpha(40));
                        g.BlendFill(x + 2, y + s - 3, s - 4, 1, tint.WithAlpha(60));
                    }
                }
                if (chosen || hover)
                {
                    var mark = chosen ? GoldLight : Rgba.White;
                    g.Box(x - 1, y - 1, s + 2, s + 2, Black);
                    g.Box(x, y, s, s, mark);
                }
            }
            if (icon != null) g.Draw(icon, x + (s - icon.Width) / 2 - (Style == MockStyle.C ? 1 : 0), y + (s - icon.Height) / 2 - (Style == MockStyle.C ? 1 : 0));
            else if (hint != null) MockGlyph.Centered(g, x + s / 2, y + s / 2, hint, Style == MockStyle.C ? new Rgba(255, 255, 255, 50) : Dim(kind).WithAlpha(90));
            if (count != null)
            {
                int cw = MockFont.SmallWidth(count);
                g.Small(x + s - 3 - cw, y + s - 8, count, Rgba.White, new Rgba(0, 0, 0, 230));
            }
        }

        // ---- Level 3: a card floating over everything ----

        public (Rgba Ink, Rgba Dim, Rgba Shadow) Card(PixelImage g, int x, int y, int w, int h, Rgba accent)
        {
            switch (Style)
            {
                case MockStyle.B:
                {
                    g.BlendFill(x + 3, y + 4, w, h, new Rgba(0, 0, 0, 120));
                    Parchment(g, x, y, w, h);
                    g.Box(x, y, w, h, Px.Hex("#6E5632"));
                    g[x, y] = Rgba.Clear;
                    g[x + w - 1, y] = Rgba.Clear;
                    g[x, y + h - 1] = Rgba.Clear;
                    g[x + w - 1, y + h - 1] = Rgba.Clear;
                    // A tack through the top.
                    g.Disc(x + w / 2, y + 3, 2.2f, Px.Hex("#8E1C1C"));
                    g[x + w / 2 - 1, y + 2] = Px.Hex("#F08A78");
                    return (Px.Hex("#3A2A1A"), Px.Hex("#7A6444"), Rgba.Clear);
                }
                case MockStyle.C:
                    g.BlendFill(x, y, w, h, new Rgba(4, 4, 8, 236));
                    g.BlendFill(x, y, 2, h, accent);
                    return (Rgba.White, Px.Hex("#A6A8B2"), Rgba.Clear);
                default:
                    g.BlendFill(x + 3, y + 4, w, h, new Rgba(0, 0, 0, 130));
                    g.Fill(x + 1, y, w - 2, h, Black);
                    g.Fill(x, y + 1, w, h - 2, Black);
                    g.Fill(x + 1, y + 1, w - 2, h - 2, Gold);
                    g.Fill(x + 1, y + 1, w - 2, 1, GoldLight);
                    g.Fill(x + 1, y + 1, 1, h - 2, GoldLight);
                    g.Fill(x + 2, y + 2, w - 4, h - 4, Px.Hex("#3D322B"));
                    g.Fill(x + 2, y + 2, w - 4, 1, Px.Hex("#54463C"));
                    g.Fill(x + 2, y + h - 3, w - 4, 1, Px.Hex("#2C231E"));
                    // The rarity as a band under the top edge.
                    g.Fill(x + 2, y + 3, w - 4, 2, accent.Scale(0.8f));
                    g.Fill(x + 2, y + 3, w - 4, 1, accent);
                    return (Px.Hex("#F6ECD6"), Px.Hex("#B8A88E"), new Rgba(0, 0, 0, 150));
            }
        }

        // ---- Buttons and tabs ----

        public enum Press
        {
            Normal,
            Hover,
            Primary,
            Off,
        }

        (Rgba Rim, Ramp Fill, Rgba Ink) Plate(MockKind kind, Press press, Rgba? accent)
        {
            if (press == Press.Primary)
            {
                return kind switch
                {
                    MockKind.Growth => (Px.Hex("#DCE6FF"), Ramp.Of("#5A5CC8"), Rgba.White),
                    MockKind.Machine => (Px.Hex("#D8F2FA"), Ramp.Of("#2E8CA8"), Rgba.White),
                    MockKind.Journal => (Px.Hex("#5A2A1A"), Ramp.Of("#B8483A"), Px.Hex("#FFF2DC")),
                    _ => (Px.Hex("#FFF0B8"), Ramp.Of("#C49A44"), Px.Hex("#2A1808")),
                };
            }
            float lift = press == Press.Hover ? 0.18f : 0f;
            var (rim, fill, ink) = kind switch
            {
                MockKind.Bag => (Px.Hex("#C49A55"), Px.Hex("#5C3C28"), Px.Hex("#FBEED2")),
                MockKind.Shop => (Px.Hex("#C89A68"), Px.Hex("#6A4426"), Px.Hex("#FBEED2")),
                MockKind.Journal => (Px.Hex("#7A5A30"), Px.Hex("#CDB680"), Px.Hex("#3A2A1A")),
                MockKind.Growth => (Px.Hex("#8A8CD0"), Px.Hex("#1E1C4C"), Px.Hex("#E4E8FF")),
                MockKind.Machine => (Px.Hex("#A6B2C0"), Px.Hex("#46505C"), Px.Hex("#EEF2F6")),
                _ => (accent ?? Gold, Px.Hex("#3A302C"), Px.Hex("#FBEED2")),
            };
            if (press == Press.Off) return (rim.Mix(fill, 0.6f), Ramp.Of(fill.Scale(0.8f)), ink.WithAlpha(110));
            return (rim.Mix(Rgba.White, lift), Ramp.Of(fill.Mix(Rgba.White, lift * 0.5f)), ink);
        }

        public void Button(PixelImage g, int x, int y, int w, int h, MockKind kind, string label, string[] icon = null, Press press = Press.Normal, Rgba? accent = null, string key = null)
        {
            Rgba ink;
            Rgba shadow = new Rgba(0, 0, 0, 150);
            switch (Style)
            {
                case MockStyle.A:
                case MockStyle.R:
                    g.Slice(UiArt.Button(hover: press == Press.Hover, chosen: press == Press.Primary), UiArt.ButtonBorder, x, y, w, h);
                    ink = press == Press.Primary ? Px.Hex("#FFF6DC") : press == Press.Off ? Px.Hex("#7A6C5A") : Px.Hex("#EFE4CC");
                    if (press == Press.Off) g.BlendFill(x + 1, y + 1, w - 2, h - 2, new Rgba(11, 8, 7, 110));
                    break;
                case MockStyle.B:
                {
                    var (rim, fill, plateInk) = Plate(kind, press, accent);
                    g.Raised(x, y, w, h, Px.Hex("#120A06"), rim, fill);
                    ink = plateInk;
                    if (kind == MockKind.Journal && press != Press.Primary) shadow = Rgba.Clear;
                    if (press == Press.Primary && kind != MockKind.Growth && kind != MockKind.Machine && kind != MockKind.Journal) shadow = new Rgba(255, 240, 190, 90);
                    break;
                }
                default:
                {
                    var a = accent ?? Accent(kind);
                    if (press == Press.Primary) g.BlendFill(x, y, w, h, a);
                    else g.BlendFill(x, y, w, h, new Rgba(255, 255, 255, press == Press.Hover ? 60 : press == Press.Off ? 10 : 28));
                    ink = press == Press.Primary ? Px.Hex("#101014") : press == Press.Off ? new Rgba(255, 255, 255, 90) : Rgba.White;
                    shadow = Rgba.Clear;
                    break;
                }
            }
            int content = (label != null ? MockFont.Width(label) : 0) + (icon != null ? MockGlyph.WidthOf(icon) + (label != null ? 4 : 0) : 0);
            int at = x + (w - content) / 2;
            if (icon != null)
            {
                MockGlyph.Draw(g, at, y + (h - icon.Length) / 2, icon, ink);
                at += MockGlyph.WidthOf(icon) + 4;
            }
            if (label != null) g.Text(at, y + (h - 7) / 2, label, ink, shadow);
            if (key != null) g.Small(x + 3, y + 3, key, Style == MockStyle.C ? new Rgba(255, 255, 255, 130) : ink.Mix(Px.Hex("#6A5A48"), 0.5f));
        }

        // A tab on the top edge of what it turns: the chosen one stands taller and lit.
        public void Tab(PixelImage g, int x, int y, int w, int h, MockKind kind, string label, string[] icon, bool chosen, bool dot = false)
        {
            if (Style == MockStyle.C)
            {
                var a = Accent(kind);
                var ink = chosen ? Rgba.White : new Rgba(255, 255, 255, 140);
                int content = (label != null ? MockFont.Width(label) : 0) + (icon != null ? MockGlyph.WidthOf(icon) + (label != null ? 4 : 0) : 0);
                int at = x + (w - content) / 2;
                if (icon != null)
                {
                    MockGlyph.Draw(g, at, y + (h - 3 - icon.Length) / 2, icon, chosen ? a : ink);
                    at += MockGlyph.WidthOf(icon) + 4;
                }
                if (label != null) g.Text(at, y + (h - 3 - 7) / 2, label, ink);
                if (chosen) g.Fill(x + 2, y + h - 2, w - 4, 2, a);
                else g.BlendFill(x + 2, y + h - 1, w - 4, 1, new Rgba(255, 255, 255, 40));
                if (dot) g.Fill(x + w - 7, y + 2, 3, 3, Px.Hex("#FFC43A"));
                return;
            }
            int top = chosen ? y : y + 2;
            int height = chosen ? h : h - 2;
            Button(g, x, top, w, height, kind, label, icon, chosen ? Press.Primary : Press.Normal);
            if (dot)
            {
                g.Fill(x + w - 7, top + 2, 5, 5, Black);
                g.Fill(x + w - 6, top + 3, 3, 3, Px.Hex("#FFC43A"));
                g[x + w - 6, top + 3] = Px.Hex("#FFF0B8");
            }
        }

        // A rule between parts of a window.
        public void Rule(PixelImage g, int x, int y, int w, MockKind kind)
        {
            if (Style == MockStyle.C)
            {
                g.BlendFill(x, y, w, 1, new Rgba(255, 255, 255, 36));
                return;
            }
            if (Stone)
            {
                g.Fill(x, y, w, 1, Px.Hex("#0E0A08"));
                g.Fill(x, y + 1, w, 1, Px.Hex("#4A3C30"));
                g.Fill(x + w / 2 - 2, y, 5, 2, Gold);
                g[x + w / 2, y - 1] = GoldLight;
                g[x + w / 2, y + 2] = GoldDark;
                return;
            }
            g.BlendFill(x, y, w, 1, new Rgba(0, 0, 0, 120));
            g.BlendFill(x, y + 1, w, 1, new Rgba(255, 235, 200, 60));
        }

        // A bar (health, mana, experience) in a well.
        public void Bar(PixelImage g, int x, int y, int w, int h, float share, Rgba color, MockKind kind)
        {
            if (Style == MockStyle.C)
            {
                g.BlendFill(x, y, w, h, new Rgba(255, 255, 255, 30));
                g.BlendFill(x, y, (int)(w * share), h, color);
                return;
            }
            g.Slice(UiArt.Inset, UiArt.InsetBorder, x, y, w, h);
            var ramp = Ramp.Of(color);
            int fw = (int)((w - 2) * share);
            g.Fill(x + 1, y + 1, fw, h - 2, ramp.Main);
            g.Fill(x + 1, y + 1, fw, 1, ramp.Light);
            if (h > 4) g.Fill(x + 1, y + h - 2, fw, 1, ramp.Shade);
        }
    }
}
