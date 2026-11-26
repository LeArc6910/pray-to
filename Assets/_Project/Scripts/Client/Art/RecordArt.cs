using System;

namespace Game.Client
{
    // The records' pictures (Docs/UiRework.md 4-4 기록은 양피지, U5): the journal's and the map's parchment, the burnt edge
    // round a page, the wax seal on a finished deed and on the book, the ribbon that marks the page, the compass in the
    // map's corner, and the small marks drawn on the map instead of plain dots (MapIcons). Plain pixel images like the
    // rest of Art/, so the preview tool draws them too (DevTools/ArtPreview, out/ui/records.png). UiSkin draws them at
    // whole device pixels.
    static class RecordArt
    {
        // The parchment repeats every PageTile pixels across and down without a seam: its waves have whole periods over
        // the tile and its specks are per pixel.
        public const int PageTile = 64;

        static readonly Rgba PaperLight = Rgba.Hex("#F0E2B8");
        static readonly Rgba PaperMid = Rgba.Hex("#E3CF9C");
        static readonly Rgba PaperDark = Rgba.Hex("#CDB47C");
        public static readonly Rgba Ink = Rgba.Hex("#3A2414");
        public static readonly Rgba InkFaint = Rgba.Hex("#6E5436");
        public static readonly Rgba InkRed = Rgba.Hex("#8A2A1A");

        public static PixelImage Parchment()
        {
            var g = new PixelImage(PageTile, PageTile);
            const float tau = (float)(Math.PI * 2);
            for (int y = 0; y < PageTile; y++)
            {
                for (int x = 0; x < PageTile; x++)
                {
                    float u = x / (float)PageTile, v = y / (float)PageTile;
                    // Slow blotches of age: three waves with whole periods, so the edges meet.
                    float stain = 0.5f + 0.22f * (float)Math.Sin(tau * (u + 2 * v)) + 0.18f * (float)Math.Sin(tau * (3 * u - v) + 1.3f)
                        + 0.1f * (float)Math.Sin(tau * (2 * u + 3 * v) + 0.4f);
                    float grain = PixelNoise.Value(x, y, 71);
                    var c = stain > 0.72f ? PaperMid.Mix(PaperDark, (stain - 0.72f) * 2.2f) : PaperLight.Mix(PaperMid, Math.Max(0, stain - 0.2f) * 1.6f);
                    if (grain > 0.93f) c = c.Mix(PaperDark, 0.45f);
                    else if (grain < 0.05f) c = c.Mix(Rgba.White, 0.35f);
                    // Fibres: short streaks along the sheet.
                    uint fibre = PixelNoise.Hash(x / 5, y, 73);
                    if (fibre % 23 == 0 && (x + (int)(fibre >> 8)) % 5 < 4) c = c.Mix(PaperDark, 0.3f);
                    g[x, y] = new Rgba(c.R / 3 * 3, c.G / 3 * 3, c.B / 3 * 3);
                }
            }
            // A few darker flecks, as old paper has.
            for (int i = 0; i < 7; i++)
            {
                uint h = PixelNoise.Hash(i, 3, 79);
                int fx = (int)(h % PageTile), fy = (int)(h / PageTile % PageTile);
                g[fx, fy] = PaperDark.Mix(Ink, 0.25f);
                g[(fx + 1) % PageTile, fy] = PaperDark.Mix(PaperMid, 0.5f);
            }
            return g;
        }

        // The unexplored part of the map: faint diagonal strokes of ink, as a mapmaker hatches what is not yet known.
        // Repeats every HatchTile pixels.
        public const int HatchTile = 8;

        public static PixelImage Hatch()
        {
            var g = new PixelImage(HatchTile, HatchTile);
            for (int i = 0; i < HatchTile; i++)
            {
                g[i, (HatchTile - 1 - i + HatchTile) % HatchTile] = Ink.WithAlpha(70);
                g[i, (HatchTile * 2 - 2 - i) % HatchTile] = Ink.WithAlpha(22);
            }
            return g;
        }

        // The burnt, darkened edge of a page, to stretch round it by its middle (nine-slice, PageBorder pixels on every side);
        // its middle is clear, so the parchment under it shows.
        public const int PageBorder = 6;

        public static readonly PixelImage PageFrame = MakePageFrame();

        static PixelImage MakePageFrame()
        {
            const int size = 16;
            var g = new PixelImage(size, size);
            Rgba[] ring = { Rgba.Hex("#3A2414"), Rgba.Hex("#6E4E2E"), Rgba.Hex("#9C7848").WithAlpha(220), Rgba.Hex("#B8945C").WithAlpha(140), Rgba.Hex("#C8A86C").WithAlpha(80), Rgba.Hex("#CDB47C").WithAlpha(36) };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int edge = Math.Min(Math.Min(x, size - 1 - x), Math.Min(y, size - 1 - y));
                    if (edge >= ring.Length) continue;
                    // The corners worn round.
                    int cx = Math.Min(x, size - 1 - x), cy = Math.Min(y, size - 1 - y);
                    if (cx + cy < 2) continue;
                    if (cx + cy == 2 && edge == 0) continue;
                    g[x, y] = ring[edge];
                }
            }
            return g;
        }

        // A wax seal: a round blob of wax with drips at its rim, a pressed ring and a star in its middle. Red on a deed done
        // and on the journal's cover; other colours for other uses.
        public static PixelImage Seal(Rgba wax)
        {
            const int size = 17;
            var g = new PixelImage(size, size);
            var ramp = Ramp.Of(wax);
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    // The rim bulges where wax ran.
                    float angle = (float)Math.Atan2(dy, dx);
                    float rim = 7.2f + 0.7f * (float)Math.Sin(angle * 5 + 0.6f) + 0.4f * (float)Math.Sin(angle * 3 - 1.1f);
                    if (d > rim) continue;
                    var p = ramp.Main;
                    // Lit from the upper left.
                    if (dx + dy < -4 && d > rim - 2.2f) p = ramp.Light;
                    else if (dx + dy > 4 && d > rim - 2.2f) p = ramp.Shade;
                    // The pressed ring.
                    if (d > 4.2f && d < 5.4f) p = ramp.Shade;
                    if (d > 3.6f && d <= 4.2f && dx + dy < 0) p = ramp.Light;
                    g[x, y] = p;
                }
            }
            // A star pressed in the middle.
            int m = size / 2;
            foreach (var (sx, sy) in new[] { (0, -2), (0, -1), (0, 0), (0, 1), (0, 2), (-2, 0), (-1, 0), (1, 0), (2, 0), (-1, -1), (1, 1), (1, -1), (-1, 1) })
                g[m + sx, m + sy] = Math.Abs(sx) + Math.Abs(sy) == 2 && sx != 0 && sy != 0 ? ramp.Shade : ramp.Dark;
            g[m - 1, m - 2] = ramp.Light;
            g[m - 2, m - 1] = ramp.Light;
            return g.Outlined(ramp.Outline);
        }

        // The ribbon that marks the page open: a strip hanging down with a notch cut in its end.
        public static PixelImage Ribbon(Rgba color)
        {
            const int width = 7, height = 22;
            var g = new PixelImage(width, height);
            var ramp = Ramp.Of(color);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // The swallowtail: the middle cut away toward the end.
                    int cut = height - 1 - y;
                    if (cut < 3 && Math.Abs(x - width / 2) < 3 - cut) continue;
                    g[x, y] = x == 0 ? ramp.Light : x == width - 1 ? ramp.Dark : x == width - 2 ? ramp.Shade : ramp.Main;
                }
            }
            // A crease where it folds over the top of the page.
            g.Fill(0, 2, width, 1, ramp.Shade);
            return g.Outlined(ramp.Outline);
        }

        // The compass in the map's corner: a ring with the four points, north long and red.
        public static PixelImage Compass()
        {
            const int size = 21;
            var g = new PixelImage(size, size);
            var ink = Ink;
            var faint = InkFaint;
            int m = size / 2;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - m, dy = y - m;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d > 6.4f && d < 7.6f) g[x, y] = faint;
                }
            }
            // The four points: north long and red, the rest short.
            for (int i = 0; i <= 9; i++)
            {
                int w = Math.Max(0, 2 - i / 3);
                for (int s = -w; s <= w; s++) g[m + s, m - i] = s < 0 ? InkRed.Mix(Rgba.White, 0.25f) : InkRed;
            }
            for (int i = 0; i <= 6; i++)
            {
                int w = Math.Max(0, 1 - i / 3);
                for (int s = -w; s <= w; s++)
                {
                    g[m + s, m + i] = ink;
                    g[m + i, m + s] = ink;
                    g[m - i, m + s] = ink;
                }
            }
            g[m, m] = PaperLight;
            return g;
        }
    }

    // The marks on the full map (Docs/UiRework.md U5: drawn in code instead of plain dots), in UiIcons' format: '#' the
    // colour, 'o' its light, 'x' its dark, outlined in dark when drawn (UiIcons.Of). Seven or nine pixels, so they read as
    // a house, a stone, a cart, a gate and the rest at a glance.
    static class MapIcons
    {
        // A place of the story: a house with a pointed roof.
        public static readonly string[] Place = { "...x...", "..xxx..", ".xxxxx.", "xxxxxxx", ".#o#o#.", ".#####.", ".##x##.", ".##x##." };
        // A waypoint: the crystal on its plinth.
        public static readonly string[] Waypoint = { "...#...", "..#o#..", "..#o#..", ".#oo##.", ".#o###.", "..###..", "...#...", ".xxxxx.", "xxxxxxx" };
        // A waystone: a standing stone with a rune.
        public static readonly string[] Stone = { "..###..", ".#ooo#.", ".#o#o#.", ".#oo#x.", ".#o#ox.", ".#ooox.", "xxxxxxx" };
        // A minecart station: a cart on its rail.
        public static readonly string[] Station = { "#.....#", "#######", "#ooooo#", "#ooooo#", ".#####.", ".x...x.", "xxxxxxx" };
        // A dungeon's gate (and, inside one, a door out): an arch.
        public static readonly string[] Gate = { "..###..", ".#ooo#.", "#o...x#", "#o...x#", "#o...x#", "#o...x#", "###.###" };
        public static readonly string[] Chest = { ".#####.", "#ooooo#", "#######", "###o###", "#######", "#xxxxx#", "#######" };
        // A shrine: a flame over its altar.
        public static readonly string[] Shrine = { "...o...", "..#o#..", "..###..", "...#...", ".#####.", "..###..", ".#####." };
        // A camp: a tent.
        public static readonly string[] Camp = { "...#...", "..#o#..", "..#o##.", ".#o.x#.", ".#o.x#.", "#o...x#", "#######" };
        public static readonly string[] Cart = { ".......", "#######", "#ooooo#", "#ooooo#", "#######", ".x...x.", ".x...x." };
        // An elite camp or a dungeon's master: a skull.
        public static readonly string[] Skull = { ".#####.", "#######", "#..#..#", "#######", ".##.##.", ".#.#.#.", "......." };
        // A hunting ground (Docs/HuntingGrounds.md): a claw's three marks.
        public static readonly string[] Hunt = { "#..#..#", "#..#..#", "#o.#o.#", ".#o.#o.", ".#o.#o.", "..#..#.", "..#..#." };
        // A boss's arena: two swords crossed.
        public static readonly string[] Arena = { "#.....#", ".#...#.", "..#.#..", "...#...", "..#.#..", ".x#.#x.", "x.....x" };
        // The nearest vein of an ore: a cut stone.
        public static readonly string[] Ore = { "..#..", ".#o#.", "#oo##", ".#x#.", "..#.." };
        // The wandering merchant's stall: an awning over a counter.
        public static readonly string[] Stall = { "#######", "#o#o#o#", "x.x.x.x", ".#####.", ".#ooo#.", ".#####." };
        // Where the story leads: a star.
        public static readonly string[] Lead = { "...#...", "..#o#..", "#######", ".##o##.", "..###..", ".##.##.", ".#...#." };
        // A rumour: a question mark.
        public static readonly string[] Rumor = { ".###.", "#...#", "...#.", "..#..", "..#..", ".....", "..#.." };
        // A dungeon's rest room: a fire.
        public static readonly string[] Fire = { "...#...", "..#o#..", ".#oo#..", ".#ooo#.", "#oooo#.", ".#####." };
        // A pack not yet cleared: a dot.
        public static readonly string[] Pack = { ".###.", "#####", "#####", "#####", ".###." };
        // A spot being guided to: a flag on its pole.
        public static readonly string[] Pin = { "#####..", "#ooo##.", "#oooo##", "#ooo##.", "#####..", "#......", "#......", "#......" };
        // The player and the others: an arrow pointing up, turned to where each faces.
        public static readonly string[] Arrow = { "....#....", "...#o#...", "...#o#...", "..#oo##..", "..#oo##..", ".#ooo###.", ".#oo#x##.", "#oo#.#x##", "#o#...#x#", "##.....##" };
    }
}
