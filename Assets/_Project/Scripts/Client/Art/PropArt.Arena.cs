using System;
using Game.Core;

namespace Game.Client
{
    // What the boss arenas are dressed with (2026-11-17, Docs/Bosses.md 아레나 컨셉): the patterns laid on their floors
    // (walked on, drawn flat over the arena's own flagstones, so they are part see-through) and four things that stand.
    static partial class PropArt
    {
        // The floor pattern of the type, or null for any other tile.
        public static PixelImage ArenaFloor(TileType type) => type switch
        {
            TileType.ArenaInlay => Inlay.Value,
            TileType.RuneFloor => RuneTile.Value,
            TileType.Cobweb => WebTile.Value,
            TileType.Petals => PetalTile.Value,
            TileType.Fissure => FissureTile.Value,
            TileType.ArenaCarpet => CarpetTile.Value,
            _ => null,
        };

        static readonly Lazy<PixelImage> Inlay = new Lazy<PixelImage>(DrawInlay);
        static readonly Lazy<PixelImage> RuneTile = new Lazy<PixelImage>(DrawRune);
        static readonly Lazy<PixelImage> WebTile = new Lazy<PixelImage>(DrawWeb);
        static readonly Lazy<PixelImage> PetalTile = new Lazy<PixelImage>(DrawPetals);
        static readonly Lazy<PixelImage> FissureTile = new Lazy<PixelImage>(DrawFissure);
        static readonly Lazy<PixelImage> CarpetTile = new Lazy<PixelImage>(DrawCarpet);

        // A darker slab set into the floor, a chiselled line just inside its edge: on any arena's stone it reads as
        // inlaid work, on sand as a ring going down.
        static PixelImage DrawInlay()
        {
            var image = new PixelImage(T, T);
            for (int y = 0; y < T; y++)
            {
                for (int x = 0; x < T; x++)
                {
                    bool edge = x == 0 || y == 0 || x == T - 1 || y == T - 1;
                    bool line = !edge && (x == 2 || y == 2 || x == T - 3 || y == T - 3) && x >= 2 && y >= 2 && x <= T - 3 && y <= T - 3;
                    image[x, y] = edge ? new Rgba(0, 0, 0, 120) : line ? new Rgba(255, 240, 210, 46) : new Rgba(0, 0, 0, 74);
                }
            }
            return image;
        }

        static readonly Rgba RuneGlow = Rgba.Hex("#A9B8FF");
        static readonly Rgba RuneBright = Rgba.Hex("#EEF0FF");

        // A rune cut into a dark slab and glowing: a ring with a stroke through it and two marks.
        static PixelImage DrawRune()
        {
            var image = new PixelImage(T, T);
            image.Fill(0, 0, T, T, new Rgba(16, 10, 34, 110));
            for (int y = 0; y < T; y++)
            {
                for (int x = 0; x < T; x++)
                {
                    float dx = x - 7.5f, dy = y - 7.5f, far = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (far > 4.3f && far < 5.6f) image[x, y] = RuneGlow;
                    else if (far >= 5.6f && far < 6.5f) image[x, y] = RuneGlow.WithAlpha(70);
                }
            }
            for (int i = 3; i <= 12; i++) image[i, 15 - i] = i >= 6 && i <= 9 ? RuneBright : RuneGlow;
            image.Fill(5, 5, 2, 1, RuneBright);
            image.Fill(5, 5, 1, 2, RuneBright);
            image.Fill(9, 10, 2, 1, RuneBright);
            image.Fill(10, 9, 1, 2, RuneBright);
            return image;
        }

        // A web across the floor: threads from corner to corner and side to side, two rings strung between them. Thin
        // and pale, so it is never taken for the webs a spider lays in a fight (those are thick and white).
        static PixelImage DrawWeb()
        {
            var image = new PixelImage(T, T);
            var thread = new Rgba(214, 214, 228, 120);
            var knot = new Rgba(236, 236, 246, 170);
            for (int i = 0; i < T; i++)
            {
                image[i, i] = thread;
                image[i, T - 1 - i] = thread;
                image[i, 8] = thread;
                image[8, i] = thread;
            }
            foreach (int r in new[] { 3, 6 })
            {
                for (int i = 0; i <= r; i++)
                {
                    // A sagging diamond between the threads.
                    foreach (var (sx, sy) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                        image[8 + sx * i, 8 + sy * (r - i)] = thread;
                }
            }
            image[8, 8] = knot;
            image[7, 8] = knot;
            image[8, 7] = knot;
            return image;
        }

        // Fallen cherry petals: a few little heaps of two and three.
        static PixelImage DrawPetals()
        {
            var image = new PixelImage(T, T);
            var pink = Rgba.Hex("#F4A6C0");
            var pale = Rgba.Hex("#FFD6E2");
            var deep = Rgba.Hex("#D07A9C");
            foreach (var (x, y, turn) in new[] { (2, 3, 0), (10, 2, 1), (6, 8, 2), (12, 11, 0), (3, 12, 1) })
            {
                image[x, y] = pale;
                if (turn == 0) image[x + 1, y] = pink;
                else if (turn == 1) image[x, y + 1] = pink;
                else
                {
                    image[x + 1, y] = pink;
                    image[x + 1, y + 1] = deep;
                }
            }
            return image;
        }

        // A crack in the floor with fire far down in it: a jagged glowing split in the middle of the tile, dark-lipped,
        // and hairline cracks running from it to all four sides so cracks laid in a line join.
        static PixelImage DrawFissure()
        {
            var image = new PixelImage(T, T);
            var lip = new Rgba(18, 10, 8, 230);
            var hair = new Rgba(18, 10, 8, 170);
            var ember = Rgba.Hex("#FF8A3C");
            var core = Rgba.Hex("#FFD27A");
            // The hairlines, wandering a pixel.
            int[] wander = { 8, 8, 7, 7, 7, 8, 8, 8, 8, 9, 9, 8, 8, 7, 8, 8 };
            for (int i = 0; i < T; i++)
            {
                image[i, wander[i]] = hair;
                image[wander[T - 1 - i], i] = hair;
            }
            // The split: a slanting gash, widest in the middle.
            for (int i = -5; i <= 5; i++)
            {
                int half = i == -5 || i == 5 ? 0 : Math.Abs(i) >= 3 ? 1 : 2;
                int cx = 8 + i, cy = 8 - i / 2;
                for (int k = -half - 1; k <= half + 1; k++)
                {
                    if (!image.Inside(cx, cy + k)) continue;
                    bool inside = Math.Abs(k) <= half;
                    image[cx, cy + k] = !inside ? lip : k == 0 && half > 0 ? core : ember;
                }
            }
            return image;
        }

        // A red runner: a dark red field with a darker weave, a gold line inside each edge and a gold lozenge in the
        // middle, the same whichever way the hall is turned.
        static PixelImage DrawCarpet()
        {
            var image = new PixelImage(T, T);
            var field = Rgba.Hex("#7A1E28");
            var weave = Rgba.Hex("#641822");
            var edge = Rgba.Hex("#4A1018");
            var gold = Rgba.Hex("#C89A3C");
            for (int y = 0; y < T; y++)
            {
                for (int x = 0; x < T; x++)
                {
                    bool rim = x == 0 || y == 0 || x == T - 1 || y == T - 1;
                    bool line = !rim && (x == 2 || x == T - 3) && y >= 2 && y <= T - 3 || !rim && (y == 2 || y == T - 3) && x >= 2 && x <= T - 3;
                    int lozenge = Math.Abs(x - 8) + Math.Abs(y - 8) + (x < 8 ? 1 : 0) * 0;
                    image[x, y] = rim ? edge : line ? gold : lozenge == 3 ? gold : (x + y) % 4 == 0 ? weave : field;
                }
            }
            return image;
        }

        // ---- Standing ----

        static readonly Ramp Vermilion = new Ramp(Rgba.Hex("#F0704A"), Rgba.Hex("#C8402C"), Rgba.Hex("#8E2A22"), Rgba.Hex("#5A1A18"));
        static readonly Ramp Blossom = new Ramp(Rgba.Hex("#FFE0EA"), Rgba.Hex("#F4A6C0"), Rgba.Hex("#D07A9C"), Rgba.Hex("#9A5274"));
        static readonly Ramp Bark = new Ramp(Rgba.Hex("#7A5A48"), Rgba.Hex("#5A4034"), Rgba.Hex("#3E2C26"), Rgba.Hex("#281C1A"));
        static readonly Ramp Silk = new Ramp(Rgba.Hex("#F6F6FA"), Rgba.Hex("#D2D2DE"), Rgba.Hex("#A2A2B6"), Rgba.Hex("#727288"));
        static readonly Ramp BlackIron = new Ramp(Rgba.Hex("#7C8290"), Rgba.Hex("#4A4E5A"), Rgba.Hex("#2E303A"), Rgba.Hex("#1A1B22"));

        // A small shrine gate: two vermilion posts on dark feet, a tie beam, and a black-capped lintel wider than the
        // posts, its ends turned up.
        static PixelImage Torii()
        {
            const int w = 24, h = 40;
            var body = new PixelImage(w, h);
            foreach (int x in new[] { 5, 16 })
            {
                Column(body, x, 8, 3, 29, Vermilion);
                Rect(body, x - 1, 35, 5, 3, DarkStone.Shade);
                Rect(body, x - 1, 35, 5, 1, DarkStone.Main);
            }
            Rect(body, 3, 13, 18, 2, Vermilion.Main);
            Rect(body, 3, 15, 18, 1, Vermilion.Shade);
            Rect(body, 11, 9, 2, 4, Vermilion.Shade);
            Rect(body, 1, 6, 22, 2, Vermilion.Light);
            Rect(body, 1, 8, 22, 1, Vermilion.Shade);
            Rect(body, 1, 4, 22, 2, Ink);
            Rect(body, 0, 3, 2, 2, Ink);
            Rect(body, 22, 3, 2, 2, Ink);
            Rect(body, 2, 4, 20, 1, DarkStone.Dark);
            return Finish(body, ShadowLayer(w, h, 12, 37, 10, 2.5f));
        }

        // A cherry tree in flower: a dark leaning trunk forking under a wide cloud of blossom, petals coming down.
        static PixelImage CherryTree()
        {
            const int w = 32, h = 46;
            var body = new PixelImage(w, h);
            Column(body, 14, 24, 4, 19, Bark);
            Rect(body, 13, 40, 6, 3, Bark.Shade);
            for (int i = 0; i < 7; i++)
            {
                body[13 - i, 27 - i] = Bark.Main;
                body[14 - i, 27 - i] = Bark.Shade;
                body[18 + i, 26 - i] = Bark.Shade;
                body[19 + i, 26 - i] = Bark.Dark;
            }
            LitOval(body, 9, 17, 8, 6, Blossom);
            LitOval(body, 23, 16, 8, 6, Blossom);
            LitOval(body, 16, 11, 10, 8, Blossom);
            LitOval(body, 16, 20, 7, 4, new Ramp(Blossom.Main, Blossom.Shade, Blossom.Shade, Blossom.Dark));
            foreach (var (sx, sy) in new[] { (10, 8), (17, 6), (23, 11), (7, 15), (14, 13), (20, 17), (26, 18), (12, 19) })
            {
                body[sx, sy] = Rgba.White;
                body[sx + 1, sy] = Blossom.Light;
            }
            // Petals on the air under it.
            foreach (var (sx, sy) in new[] { (5, 27), (25, 29), (9, 33) })
            {
                body[sx, sy] = Blossom.Main;
                body[sx + 1, sy + 1] = Blossom.Shade;
            }
            return Finish(body, ShadowLayer(w, h, 17, 43, 11, 3));
        }

        // Something wound in silk and stood against the wall: a pale oval bound round and round, threads from its top
        // and to the floor.
        static PixelImage Cocoon()
        {
            const int h = 28;
            var body = new PixelImage(T, h);
            LitOval(body, 8, 15, 4.6f, 9.5f, Silk);
            for (int y = 8; y < 24; y += 3)
                for (int x = 4; x < 13; x++)
                    if (body[x, y].A > 0 && (x + y) % 2 == 0) body[x, y] = Silk.Shade;
            for (int y = 0; y < 6; y++) body[8, y] = Silk.Main;
            for (int i = 0; i < 4; i++)
            {
                body[3 - i / 2, 23 + i / 2] = Silk.Main;
                body[13 + i / 2, 23 + i / 2] = Silk.Shade;
            }
            // What is inside shows dark through a gap.
            body[7, 13] = Silk.Dark;
            body[8, 13] = Ink;
            body[8, 14] = Silk.Dark;
            return Finish(body, ShadowLayer(T, h, 8, 26, 6, 2));
        }

        // A suit of black plate on a wooden stand: a great helm with a blue plume, wide pauldrons, a breastplate over a
        // torn blue tabard, the stand's foot.
        static PixelImage ArmorStand()
        {
            const int h = 36;
            var body = new PixelImage(T, h);
            Column(body, 7, 20, 2, 12, DarkWood);
            Box(body, 4, 31, 8, 1, 2, DarkWood);
            // Tabard and breastplate.
            Rect(body, 5, 20, 6, 8, Paint.Shade);
            Rect(body, 5, 26, 2, 3, Paint.Dark);
            Rect(body, 9, 27, 2, 2, Paint.Dark);
            Column(body, 4, 12, 8, 9, BlackIron);
            Rect(body, 4, 16, 8, 1, BlackIron.Dark);
            body[7, 14] = BlackIron.Light;
            // Pauldrons.
            LitOval(body, 3.5f, 13, 2.6f, 2.2f, BlackIron);
            LitOval(body, 12.5f, 13, 2.6f, 2.2f, BlackIron);
            // The helm, its slit, the plume.
            Column(body, 5, 4, 6, 7, BlackIron);
            Rect(body, 5, 4, 6, 1, BlackIron.Light);
            Rect(body, 6, 7, 4, 1, Ink);
            Rect(body, 8, 8, 1, 2, Ink);
            Rect(body, 7, 1, 2, 3, Paint.Main);
            body[9, 2] = Paint.Light;
            body[9, 3] = Paint.Shade;
            return Finish(body, ShadowLayer(T, h, 8, 33, 6, 2));
        }
    }
}
