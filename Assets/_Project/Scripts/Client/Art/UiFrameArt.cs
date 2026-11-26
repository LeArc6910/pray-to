using System;
using System.Collections.Generic;

namespace Game.Client
{
    // What a window is for, which picks the material of its head (Docs/UiRework.md 4-4; the person's pick of 2026-11-22:
    // the stone panels of A for every window, and a head band of B's material for each kind).
    enum UiKind
    {
        // Menus and anything without a kind of its own: dark stone with a gold edge.
        Stone,
        // The bag: leather with stitching and brass studs.
        Bag,
        // A merchant: a striped awning over planks, the name on a hanging sign.
        Shop,
        // Someone speaking: a band in the speaker's colour.
        Talk,
        // The journal, the map, the waypoints: parchment with dark ink.
        Journal,
        // Machines, stations, chests: steel with a hazard stripe and bolts.
        Machine,
        // Skills and the passive tree: a strip of night sky.
        Growth,
    }

    // The pieces of the UI skin beyond the plain frames of UiArt.cs (Docs/UiRework.md U1): the floating card, the item
    // slot with its rarity rim, the head band of each kind of window, the close button, the shop's sign and the small
    // icons of tabs and heads. Plain pixel images like the rest of Art/, so the preview tool draws them too
    // (DevTools/ArtPreview, out/ui/). UiSkin draws them at whole device pixels.
    static partial class UiArt
    {
        static readonly Rgba Black = Rgba.Hex("#0B0807");
        static readonly Rgba GoldLight = Rgba.Hex("#F4DE9C");
        static readonly Rgba GoldMid = Rgba.Hex("#C49A55");
        static readonly Rgba GoldDark = Rgba.Hex("#6B5230");

        // ---- The floating card: descriptions over everything, a step brighter than a panel ----

        public const int CardBorder = 4;

        static readonly Dictionary<char, Rgba> CardColors = new Dictionary<char, Rgba>
        {
            ['o'] = Black,
            ['W'] = GoldLight,
            ['G'] = GoldMid,
            ['s'] = GoldDark,
            ['b'] = Rgba.Hex("#54463C"),
            ['c'] = Rgba.Hex("#3D322B"),
            ['d'] = Rgba.Hex("#2C231E"),
        };

        // Gold rim lit on its top and left, a warm fill a step lighter than the panel's, a lit row under the top.
        public static readonly PixelImage Card = PixelImage.FromRows(CardColors,
            ".oooooooo.",
            "oWWWWWWWGo",
            "oWbbbbbbGo",
            "oWccccccGo",
            "oWccccccGo",
            "oWccccccGo",
            "oWccccccGo",
            "oWddddddGo",
            "oGGGGGGGso",
            ".oooooooo.");

        // ---- An item's slot: a dark well, its rim in the thing's rarity colour ----

        public const int SlotBorder = 3;

        // A well with its shadow at the top left and light at the bottom right. With a colour (a rarity, a potion), a
        // rim of it with bright corners, so it reads as a set stone rather than a line, and the well faintly tinted;
        // glowing (epic and up) a little more, and lit from below.
        public static PixelImage Slot(Rgba? tint = null, bool glow = false)
        {
            const int size = 10;
            var image = new PixelImage(size, size);
            var well = Rgba.Hex("#17120F");
            var shadow = Rgba.Hex("#0A0806");
            var light = Rgba.Hex("#4A3C30");
            image.Fill(0, 0, size, size, well);
            image.Fill(0, 0, size, 1, Rgba.Hex("#0E0A08"));
            image.Fill(0, 0, 1, size, Rgba.Hex("#0E0A08"));
            image.Fill(1, size - 1, size - 1, 1, light);
            image.Fill(size - 1, 1, 1, size - 1, light);
            if (tint is not Rgba color)
            {
                image.Fill(1, 1, size - 2, 1, shadow);
                image.Fill(1, 1, 1, size - 2, shadow);
                return image;
            }
            var rim = color.Scale(0.78f);
            for (int i = 1; i < size - 1; i++)
            {
                image[i, 1] = color.Mix(well, 0.15f);
                image[i, size - 2] = rim;
                image[1, i] = rim;
                image[size - 2, i] = rim;
            }
            var inside = well.Mix(color, glow ? 0.18f : 0.12f);
            image.Fill(2, 2, size - 4, size - 4, inside);
            if (glow) image.Fill(2, size - 3, size - 4, 1, well.Mix(color, 0.3f));
            foreach (var (x, y) in new[] { (1, 1), (size - 2, 1), (1, size - 2), (size - 2, size - 2) }) image[x, y] = color.Mix(Rgba.White, 0.5f);
            return image;
        }

        // ---- The close button: a small red stone with an X, always in the window's top right corner ----

        public const int CloseSize = 13;

        static readonly string[] Cross = { "#...#", ".#.#.", "..#..", ".#.#.", "#...#" };

        public static PixelImage Close(bool hover = false, bool pressed = false)
        {
            var image = new PixelImage(CloseSize, CloseSize);
            var fill = Ramp.Of(hover ? "#C8483A" : "#A8382E");
            Plate(image, 0, 0, CloseSize, CloseSize, Black, hover ? Rgba.White.Mix(GoldLight, 0.5f) : GoldLight, fill, pressed);
            var ink = Rgba.Hex("#FFF2DC");
            int down = pressed ? 1 : 0;
            for (int y = 0; y < Cross.Length; y++)
            {
                for (int x = 0; x < Cross[y].Length; x++)
                {
                    if (Cross[y][x] != '#') continue;
                    image[4 + x, 4 + y + down] = ink;
                    image.Blend(5 + x, 5 + y + down, new Rgba(40, 8, 6, 140));
                }
            }
            return image;
        }

        // A raised plate: outline, a rim, a lit top row and a dark bottom row; sunk, the light and dark swap.
        static void Plate(PixelImage g, int x, int y, int w, int h, Rgba outline, Rgba rim, Ramp fill, bool sunk = false)
        {
            g.Fill(x + 1, y, w - 2, h, outline);
            g.Fill(x, y + 1, w, h - 2, outline);
            g.Fill(x + 1, y + 1, w - 2, h - 2, rim);
            g.Fill(x + 1, y + h - 2, w - 2, 1, rim.Mix(outline, 0.5f));
            g.Fill(x + 2, y + 2, w - 4, h - 4 - (sunk ? 0 : 1), fill.Main);
            g.Fill(x + 2, sunk ? y + h - 3 : y + 2, w - 4, 1, fill.Light);
            g.Fill(x + 2, sunk ? y + 2 : y + h - 4, w - 4, 1, fill.Shade);
        }

        // ---- The shop's sign under its awning: a dark board in a light wooden rim, stretched to its name ----

        public const int SignBorder = 3;

        public static readonly PixelImage Sign = SignPlate();

        static PixelImage SignPlate()
        {
            var image = new PixelImage(9, 9);
            Plate(image, 0, 0, 9, 9, Rgba.Hex("#120A06"), Rgba.Hex("#C89A68"), Ramp.Of("#4A2E18"));
            return image;
        }

        // ---- The head band of a window ----

        // The band is HeadHeight pixels of its material over a black line and a dark gold one. It is drawn from a left
        // cap, a middle piece repeated as far as the window is wide (HeadTile pixels a time, so the patterns meet), and
        // the right cap.
        public const int HeadBand = 15;
        public const int HeadHeight = HeadBand + 2;
        public const int HeadTile = 40;
        public const int HeadCap = 8;

        // The colour of a head's title, and whether it wants a shadow under it (not dark ink on parchment).
        public static Rgba HeadInk(UiKind kind) => kind switch
        {
            UiKind.Journal => Rgba.Hex("#4A2A1A"),
            UiKind.Growth => Rgba.Hex("#DCE6FF"),
            UiKind.Machine => Rgba.Hex("#D8F2FA"),
            _ => Rgba.Hex("#FFF2CC"),
        };

        public static bool HeadShadow(UiKind kind) => kind != UiKind.Journal;

        // The middle of the band, to repeat.
        public static PixelImage HeadMiddle(UiKind kind, Rgba accent)
        {
            var image = new PixelImage(HeadTile, HeadHeight);
            Material(image, 0, HeadTile, kind, accent);
            image.Fill(0, HeadBand, HeadTile, 1, Black);
            image.Fill(0, HeadBand + 1, HeadTile, 1, GoldDark);
            return image;
        }

        // An end of the band: the material's own edge (a stitched turn, a bolt, a stud).
        public static PixelImage HeadEnd(UiKind kind, Rgba accent, bool left)
        {
            var image = new PixelImage(HeadCap, HeadHeight);
            Material(image, left ? 0 : HeadTile - HeadCap, HeadCap, kind, accent);
            image.Fill(0, HeadBand, HeadCap, 1, Black);
            image.Fill(0, HeadBand + 1, HeadCap, 1, GoldDark);
            int edge = left ? 0 : HeadCap - 1;
            int inner = left ? 2 : HeadCap - 3;
            switch (kind)
            {
                case UiKind.Bag:
                    // The stitching turns down the end, with a brass stud inside it.
                    for (int y = 2; y < HeadBand - 2; y++)
                        image[inner, y] = y % 5 < 3 ? Rgba.Hex("#E6C898") : y % 5 == 3 ? Rgba.Hex("#4A2E1C") : image[inner, y];
                    Stud(image, left ? 4 : HeadCap - 7, 6);
                    image.Fill(edge, 0, 1, HeadBand, Rgba.Hex("#2A180E"));
                    break;
                case UiKind.Shop:
                    image.Fill(edge, 9, 1, HeadBand - 9, Rgba.Hex("#2A180C"));
                    image[left ? 3 : HeadCap - 4, 11] = Rgba.Hex("#D8D0C0");
                    image[left ? 4 : HeadCap - 3, 12] = Rgba.Hex("#2A180C");
                    break;
                case UiKind.Journal:
                    for (int y = 0; y < HeadBand; y++)
                    {
                        image[edge, y] = image[edge, y].Mix(Rgba.Hex("#6E5632"), 0.8f);
                        image[left ? 1 : HeadCap - 2, y] = image[left ? 1 : HeadCap - 2, y].Mix(Rgba.Hex("#A88A52"), 0.4f);
                    }
                    break;
                case UiKind.Machine:
                    Bolt(image, left ? 2 : HeadCap - 5, 7);
                    image.Fill(edge, 0, 1, HeadBand, Rgba.Hex("#2E363E"));
                    break;
                case UiKind.Growth:
                    Diamond(image, left ? 3 : HeadCap - 4, 8, 2, Rgba.Hex("#DCE6FF"));
                    image[left ? 3 : HeadCap - 4, 8] = Rgba.White;
                    break;
                default:
                    Stud(image, left ? 3 : HeadCap - 6, 6);
                    image.Fill(edge, 0, 1, HeadBand, Black.Mix(accent, 0.2f));
                    break;
            }
            return image;
        }

        // The band's material from column `from` of the repeating piece, `width` wide.
        static void Material(PixelImage g, int from, int width, UiKind kind, Rgba accent)
        {
            for (int i = 0; i < width; i++)
            {
                int x = from + i;
                for (int y = 0; y < HeadBand; y++) g[i, y] = MaterialAt(kind, accent, x, y);
            }
        }

        // One pixel of a band's material at (x, y) of the repeating piece.
        static Rgba MaterialAt(UiKind kind, Rgba accent, int x, int y)
        {
            switch (kind)
            {
                case UiKind.Bag:
                {
                    // Leather: grain in two sizes, stitched along the top and bottom.
                    if ((y == 2 || y == HeadBand - 3) && x % 5 < 3) return Rgba.Hex("#E6C898");
                    if ((y == 2 || y == HeadBand - 3) && x % 5 == 3) return Rgba.Hex("#4A2E1C");
                    float n = PixelNoise.Value(x / 3, y / 2, 11) * 0.6f + PixelNoise.Value(x, y, 12) * 0.4f;
                    var c = n > 0.78f ? Rgba.Hex("#93684A") : n < 0.3f ? Rgba.Hex("#68452E") : Rgba.Hex("#7C5438");
                    if (y == 0) c = c.Mix(Rgba.Hex("#C8A07A"), 0.4f);
                    if (y == HeadBand - 1) c = c.Mix(Rgba.Hex("#2A180E"), 0.5f);
                    return c;
                }
                case UiKind.Shop:
                {
                    if (y < 9)
                    {
                        // The awning: red and cream stripes, each ending in a rounded tongue.
                        const int stripe = 10;
                        bool red = x / stripe % 2 == 0;
                        var ramp = red ? Ramp.Of("#C8483A") : Ramp.Of("#EEDFC0");
                        int inStripe = x % stripe;
                        int cut = inStripe == 0 || inStripe == stripe - 1 ? 3 : inStripe == 1 || inStripe == stripe - 2 ? 1 : 0;
                        if (y < 9 - cut)
                        {
                            var c = y == 0 ? ramp.Light : y >= 9 - cut - 2 ? ramp.Shade : ramp.Main;
                            if (inStripe == 0 && y < 6) c = ramp.Shade;
                            return c;
                        }
                    }
                    // Planks under it, a shadow where the awning ends.
                    int board = x % 10;
                    float grain = PixelNoise.Value(x, (y + x / 10 * 7) / 16, 21 + x / 10);
                    var wood = grain > 0.9f ? Rgba.Hex("#6E4A2A") : grain < 0.14f ? Rgba.Hex("#4E3018") : Rgba.Hex("#5C3C20");
                    if (x / 10 % 2 == 1) wood = wood.Mix(Rgba.Hex("#3E2614"), 0.3f);
                    if (board == 0) wood = Rgba.Hex("#2E1A0C");
                    if (y >= 6 && y <= 10)
                    {
                        const int stripe = 10;
                        int inStripe = x % stripe;
                        int cut = inStripe == 0 || inStripe == stripe - 1 ? 3 : inStripe == 1 || inStripe == stripe - 2 ? 1 : 0;
                        if (y == 9 - cut || y == 10 - cut) wood = wood.Mix(Black, y == 9 - cut ? 0.45f : 0.25f);
                    }
                    return wood;
                }
                case UiKind.Journal:
                {
                    // Parchment: stains, a scorched top and bottom edge.
                    float n = PixelNoise.Value(x / 4, y / 3, 31) * 0.7f + PixelNoise.Value(x, y, 32) * 0.3f;
                    var c = n > 0.72f ? Rgba.Hex("#D2BE8A") : n < 0.12f ? Rgba.Hex("#F0E4BE") : Rgba.Hex("#E6D6AA");
                    int edge = Math.Min(y, HeadBand - 1 - y);
                    if (edge < 2) c = c.Mix(Rgba.Hex("#A88A52"), (2 - edge) * 0.3f);
                    return c;
                }
                case UiKind.Machine:
                {
                    // Steel under a hazard stripe.
                    if (y < 3) return (x + y) / 4 % 2 == 0 ? Rgba.Hex("#E8B830") : Rgba.Hex("#2A2420");
                    if (y == 3) return Rgba.Hex("#2E363E");
                    float n = PixelNoise.Value(x / 14, y, 41);
                    var c = n > 0.92f ? Rgba.Hex("#74808E") : n < 0.1f ? Rgba.Hex("#4A535E") : Rgba.Hex("#5C6672");
                    if (y == 4) c = c.Mix(Rgba.Hex("#A6B2C0"), 0.5f);
                    if (y == HeadBand - 1) c = Rgba.Hex("#2E363E");
                    return c;
                }
                case UiKind.Growth:
                {
                    // The night: deep blue to violet, banded, a few stars.
                    var c = Rgba.Hex("#0A0E26").Mix(Rgba.Hex("#1C1240"), y / (float)HeadBand);
                    float cloud = PixelNoise.Value(x / 8, y / 5, 51);
                    if (cloud > 0.6f) c = c.Mix(Rgba.Hex("#3A2A78"), (cloud - 0.6f) * 1.2f);
                    c = new Rgba(c.R / 6 * 6, c.G / 6 * 6, c.B / 6 * 6);
                    uint hash = PixelNoise.Hash(x, y, 53);
                    if (hash % 61 == 0) c = hash / 61 % 2 == 0 ? Rgba.White : Rgba.Hex("#9AB4FF");
                    if (y == 0) c = c.Mix(Rgba.Hex("#8A8CD0"), 0.6f);
                    return c;
                }
                case UiKind.Talk:
                {
                    // The speaker's colour as a plate: a lit top, its shade, a dark bottom.
                    var ramp = Ramp.Of(accent);
                    if (y == 0) return ramp.Light;
                    if (y == 1) return ramp.Main;
                    if (y == HeadBand - 1) return ramp.Dark;
                    return ramp.Shade.Mix(ramp.Dark, 0.25f);
                }
                default:
                {
                    // Stone: the panel's dark with a gold rule along the top and a faint grain.
                    if (y == 0) return GoldMid;
                    if (y == 1) return GoldDark;
                    var c = Rgba.Hex("#2A211B");
                    float n = PixelNoise.Value(x, y, 3);
                    if (n > 0.93f) c = c.Scale(0.82f);
                    else if (n < 0.05f) c = c.Scale(1.25f);
                    if (y == HeadBand - 1) c = Rgba.Hex("#16110E");
                    return c;
                }
            }
        }

        // The passive tree's night (Docs/UiRework.md 4-3, the one screen meant to look unlike the rest): a tile of deep
        // blue to violet sky with drifts of cloud and stars of three sizes, repeated behind the tree (U2 lays it under the
        // page; U4 builds the full-screen tree on it). Its edges meet, so it tiles without seams.
        public const int StarTile = 96;

        public static PixelImage StarField()
        {
            var g = new PixelImage(StarTile, StarTile);
            for (int y = 0; y < StarTile; y++)
            {
                for (int x = 0; x < StarTile; x++)
                {
                    // A slow wave of light down the tile, so the repeat does not read as stripes.
                    float wave = 0.5f + 0.5f * (float)Math.Sin((x + y * 0.6f) / StarTile * Math.PI * 2);
                    var c = Rgba.Hex("#070A1E").Mix(Rgba.Hex("#140D33"), 0.35f + 0.4f * wave);
                    float cloud = PixelNoise.Value(x / 12, y / 8, 61) * 0.7f + PixelNoise.Value(x / 4, y / 3, 62) * 0.3f;
                    if (cloud > 0.58f) c = c.Mix(Rgba.Hex("#2E2066"), (cloud - 0.58f) * 1.4f);
                    // Banded to a few steps, as the head band's night is.
                    g[x, y] = new Rgba(c.R / 4 * 4, c.G / 4 * 4, c.B / 4 * 4);
                }
            }
            for (int y = 0; y < StarTile; y++)
            {
                for (int x = 0; x < StarTile; x++)
                {
                    uint hash = PixelNoise.Hash(x, y, 67);
                    if (hash % 97 == 0) g[x, y] = hash / 97 % 3 == 0 ? Rgba.Hex("#9AB4FF") : Rgba.Hex("#C8C8E8");
                    else if (hash % 1499 == 1)
                    {
                        // A brighter star with a small cross of light.
                        var light = hash / 1499 % 2 == 0 ? Rgba.White : Rgba.Hex("#FFE9A8");
                        var glow = light.Mix(g[x, y], 0.55f);
                        g[x, y] = light;
                        g[(x + 1) % StarTile, y] = glow;
                        g[(x + StarTile - 1) % StarTile, y] = glow;
                        g[x, (y + 1) % StarTile] = glow;
                        g[x, (y + StarTile - 1) % StarTile] = glow;
                    }
                }
            }
            return g;
        }

        static void Stud(PixelImage g, int x, int y)
        {
            g.Fill(x, y, 3, 3, GoldMid);
            g[x, y] = Rgba.Hex("#FFF0B8");
            g[x + 2, y + 2] = Rgba.Hex("#6B4A20");
        }

        static void Bolt(PixelImage g, int x, int y)
        {
            g.Fill(x, y, 3, 3, Rgba.Hex("#8A96A4"));
            g[x, y] = Rgba.Hex("#D8E0E8");
            g[x + 2, y + 2] = Rgba.Hex("#2A3038");
            g[x + 1, y + 1] = Rgba.Hex("#5A6470");
        }

        static void Diamond(PixelImage g, int cx, int cy, int radius, Rgba color)
        {
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                    if (Math.Abs(x) + Math.Abs(y) <= radius) g[cx + x, cy + y] = color;
        }
    }

    // Small one-colour pictures for tabs, heads and buttons (Docs/UiRework.md 4-4 아이콘을 더 그린다), each lit and
    // shaded from its colour and ringed in a dark outline so it reads on any ground. '#' is the colour, 'o' its light,
    // 'x' its dark.
    static class UiIcons
    {
        // A pouch tied at the neck (not a box, which a padlock or the chest would be).
        public static readonly string[] Bag =
        {
            "...#...#...", "....###....", "...#xxx#...", "..#o#####..", ".#o#######.", "#o#########", "#o########x", "##########x", ".#######xx.", "..######x..",
        };
        public static readonly string[] Hammer =
        {
            ".#######...", ".#ooooo##..", ".#######...", "....#x.....", "....#x.....", "....#x.....", "....#x.....", "....#x.....", "...###.....", "...###.....",
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
        public static readonly string[] Chest =
        {
            "..#######..", ".#ooooooo#.", "###########", "#####o#####", "####ooo####", "#####x#####", "#xxxxxxxxx#", "###########",
        };
        public static readonly string[] Shop =
        {
            ".#########.", "#o#o#o#o#o#", "#o#o#o#o#o#", ".#.#.#.#.#.", "...........", "..#######..", "..#ooooo#..", "..#o###o#..", "..#o###o#..", "..#######..",
        };
        // A sheet with an X and the dotted way to it.
        public static readonly string[] Map =
        {
            "###########", "#ooooooooo#", "#o#o#ooooo#", "#oo#oooo.o#", "#o#o#oo..o#", "#ooooo..oo#", "#oooo.oo#o#", "###########",
        };
        public static readonly string[] Waypoint =
        {
            "....#....", "...#o#...", "..#oo##..", ".#ooo###.", "#ooo####x", ".#o###xx.", "..#x#xx..", "...#xx...", "....#....",
        };
        public static readonly string[] Talk =
        {
            ".#########.", "###########", "##o##o##o##", "###########", "###########", ".#########.", "...###.....", "..##.......", ".#.........",
        };
        public static readonly string[] Gear =
        {
            "....###....", ".#..###..#.", ".###ooo###.", "..#o###o#..", "###o#.#o###", "###o#.#o###", "..#o###o#..", ".###ooo###.", ".#..###..#.", "....###....",
        };
        // A target with a dummy's mark in the middle: the test ground's panel (TestPanel, Docs/TestGround.md).
        public static readonly string[] Target =
        {
            "...#####...", "..#ooooo#..", ".#o#####o#.", "#o#ooooo#o#", "#o#o###o#o#", "#o#o#x#o#o#", "#o#o###o#o#", "#o#ooooo#o#", ".#o#####o#.", "..#ooooo#..", "...#####...",
        };
        public static readonly string[] Coin = { "..###..", ".#ooo#.", "#oo#oo#", "#oo#oo#", "#oo#oo#", ".#ooo#.", "..###.." };
        public static readonly string[] Heart = { ".##.##.", "#o#####", "#######", ".#####.", "..###..", "...#..." };
        public static readonly string[] Lock = { "..###..", ".#...#.", ".#...#.", "#######", "###o###", "###o###", "#######" };
        public static readonly string[] Check = { "......#", ".....##", "#...##.", "##.##..", ".###...", "..#...." };

        static readonly Dictionary<(string[], int), PixelImage> Made = new Dictionary<(string[], int), PixelImage>();

        // The icon in that colour, outlined. Kept, as the same few are drawn every frame.
        public static PixelImage Of(string[] rows, Rgba color)
        {
            int key = color.R << 24 | color.G << 16 | color.B << 8 | color.A;
            if (Made.TryGetValue((rows, key), out var made)) return made;
            int width = 0;
            foreach (string row in rows) width = Math.Max(width, row.Length);
            var image = new PixelImage(width, rows.Length);
            var light = color.Mix(Rgba.White, 0.45f);
            var dark = color.Scale(0.55f);
            for (int y = 0; y < rows.Length; y++)
            {
                for (int x = 0; x < rows[y].Length; x++)
                {
                    char c = rows[y][x];
                    if (c == '#') image[x, y] = color;
                    else if (c == 'o') image[x, y] = light;
                    else if (c == 'x') image[x, y] = dark;
                }
            }
            made = image.Outlined(Rgba.Hex("#140C08"));
            Made[(rows, key)] = made;
            return made;
        }
    }
}
