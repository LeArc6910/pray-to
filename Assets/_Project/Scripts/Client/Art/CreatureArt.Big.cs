using System;
using System.Collections.Generic;

namespace Game.Client
{
    // The big bodies (2026-11-24, Docs/BiomeRework.md 4-2 크기 등급, B0b). An L monster is drawn 24 to 32 pixels wide and an
    // XL one 40 to 56, at the tiles' own 16 pixels a tile like everything else: never a small picture stretched (a
    // stretched one has pixels of two sizes). Their frames come from parts laid out for the size asked (Blob), so the
    // next big kinds of the biomes (B1~B4) take a size and a few parts rather than a hand-drawn grid each; they go
    // through the same poses, outline and colour ramp as the small ones (Monster). An elite leader is marked by a ring
    // of gold round its picture (Crowned), not by drawing it bigger.
    static partial class CreatureArt
    {
        // A picture this tall or taller is a big body's: its poses move by more than one pixel (PoseStep).
        const int BigRows = 18;

        // How many pixels a pose squashes or stretches a picture of that height.
        static int PoseStep(PixelImage image) => image.Height >= 30 ? 3 : image.Height >= BigRows ? 2 : 1;

        // The colours the big kinds add to their ramp.
        static readonly Dictionary<string, Dictionary<char, Rgba>> BigExtras = new Dictionary<string, Dictionary<char, Rgba>>
        {
            // The slime king's crown: gold, its shade and a red stone.
            ["slime_king"] = new Dictionary<char, Rgba>
            {
                ['y'] = Rgba.Hex("#FFD860"),
                ['Y'] = Rgba.Hex("#C08A28"),
                ['r'] = Rgba.Hex("#E8485A"),
            },
        };

        static readonly Dictionary<string, string[][]> BigMade = new Dictionary<string, string[][]>();

        // The frames of a big kind, or null for one drawn from the small grids.
        static string[][] BigFrames(string id)
        {
            if (BigMade.TryGetValue(id, out var frames)) return frames;
            frames = id switch
            {
                // A big slime (L): the slime's dome at 26 by 19, a cluster of bubbles inside it.
                "big_slime" => new[] { Blob(26, 19, 0, BlobLook.Bubbles), Blob(26, 19, 1, BlobLook.Bubbles) },
                // The slime king (XL): 44 by 31, two small slimes riding its crown of jelly, a gold crown and a wide mouth.
                "slime_king" => new[] { Blob(44, 31, 0, BlobLook.King), Blob(44, 31, 1, BlobLook.King) },
                _ => null,
            };
            BigMade[id] = frames;
            return frames;
        }

        [Flags]
        enum BlobLook
        {
            Plain = 0,
            Bubbles = 1,
            King = 2,
        }

        // A slime's body at any size, as rows of the ramp's letters (L light, M main, m shade, d dark, e eyes, w shine):
        // a dome over a flat foot, lit from the upper left, its shade on the right and below, two eyes a little below the
        // middle. Frame 1 squats: two wider and a row lower, standing on the same foot. The eyes, shine and bubbles grow
        // with the body in whole pixels.
        static string[] Blob(int width, int height, int frame, BlobLook look)
        {
            int w = frame == 1 ? width + 2 : width;
            int h = frame == 1 ? height - 1 : height;
            bool king = (look & BlobLook.King) != 0;
            // Room above the body for the king's crown and riders.
            int top = king ? 9 : 0;
            int canvasWidth = w;
            var grid = new char[top + h, canvasWidth];
            for (int y = 0; y < top + h; y++)
                for (int x = 0; x < canvasWidth; x++) grid[y, x] = '.';

            // The silhouette: a half-ellipse dome over the upper part, straight sides below, the foot's corners cut.
            int dome = (int)MathF.Round(h * 0.62f);
            float half = w / 2f;
            var left = new int[h];
            var right = new int[h];
            for (int r = 0; r < h; r++)
            {
                float span;
                if (r < dome)
                {
                    float t = (dome - r - 0.5f) / dome;
                    span = half * MathF.Sqrt(Math.Max(0, 1 - t * t));
                }
                else span = half;
                if (r == h - 1) span -= 2;
                else if (r == h - 2) span -= 1;
                int l = (int)MathF.Round(half - span), rr = (int)MathF.Round(half + span) - 1;
                left[r] = l;
                right[r] = rr;
                for (int x = l; x <= rr; x++) grid[top + r, x] = 'M';
            }

            // Shading: the rim of the dome light on the left two thirds, the right and the lower part in shade, the foot
            // dark.
            for (int r = 0; r < h; r++)
            {
                for (int x = left[r]; x <= right[r]; x++)
                {
                    bool rim = r == 0 || x < left[r - 1] || x > right[r - 1];
                    char c = 'M';
                    if (rim && x < w * 0.68f) c = 'L';
                    else if (x == left[r] && r < h * 0.55f) c = 'L';
                    else if (x >= right[r] - (w >= 30 ? 2 : 1) || x > w * 0.8f && r > h * 0.3f) c = 'm';
                    if (r >= h - 3 && (x > w * 0.55f || r >= h - 2)) c = 'm';
                    if (r == h - 1) c = 'd';
                    if (r == h - 2 && (x == left[r] || x == right[r])) c = 'd';
                    grid[top + r, x] = c;
                }
            }

            // The shine: an oval of white on the upper left of the dome, and a speck beside it.
            int shineX = (int)(w * 0.2f), shineY = Math.Max(1, (int)(h * 0.14f));
            int shineW = Math.Max(2, w / 9), shineH = Math.Max(2, h / 9);
            for (int y = 0; y < shineH; y++)
                for (int x = 0; x < shineW; x++)
                {
                    if ((y == 0 || y == shineH - 1) && (x == 0 || x == shineW - 1)) continue;
                    Set(grid, top + shineY + y, shineX + x + y, 'w');
                }
            Set(grid, top + shineY + shineH + 1, shineX + 1, 'w');

            // The bubbles caught inside: rings of light in the body, fewer and bigger as it grows.
            if ((look & BlobLook.Bubbles) != 0 || king)
            {
                var bubbles = new (float X, float Y, int R)[] { (0.72f, 0.38f, 1), (0.6f, 0.72f, 1), (0.28f, 0.66f, 0), (0.82f, 0.62f, 0), (0.45f, 0.85f, 0) };
                foreach (var (bx, by, br) in bubbles)
                {
                    int cx = (int)(w * bx), cy = top + (int)(h * by);
                    int radius = br + (w >= 40 ? 1 : 0);
                    if (radius == 0)
                    {
                        if (grid[cy, cx] == 'M' || grid[cy, cx] == 'm') grid[cy, cx] = 'L';
                        continue;
                    }
                    for (int y = -radius; y <= radius; y++)
                        for (int x = -radius; x <= radius; x++)
                        {
                            int d = Math.Abs(x) + Math.Abs(y);
                            if (d != radius || !Inside(grid, cy + y, cx + x) || grid[cy + y, cx + x] == '.' || grid[cy + y, cx + x] == 'd') continue;
                            grid[cy + y, cx + x] = x < 0 || y < 0 ? 'L' : 'm';
                        }
                }
            }

            // The eyes: tall dark ovals a little below the middle, wider apart on a wider body.
            int eyeW = w >= 40 ? 3 : 2, eyeH = w >= 40 ? 5 : 3;
            int eyeY = top + (int)(h * 0.44f);
            foreach (float at in new[] { 0.34f, 0.62f })
            {
                int ex = (int)(w * at) - eyeW / 2;
                for (int y = 0; y < eyeH; y++)
                    for (int x = 0; x < eyeW; x++)
                    {
                        if (eyeW == 3 && (y == 0 || y == eyeH - 1) && x != 1) continue;
                        Set(grid, eyeY + y, ex + x, 'e');
                    }
            }

            if (king)
            {
                // A wide mouth under the eyes, turned up at its ends: drawn as the eyes are, so it goes with them from
                // behind and reddens with them as it winds up.
                int mouthY = eyeY + eyeH + 2, from = (int)(w * 0.38f), to = (int)(w * 0.6f);
                for (int x = from; x <= to; x++) Set(grid, mouthY, x, 'e');
                Set(grid, mouthY - 1, from - 1, 'e');
                Set(grid, mouthY - 1, to + 1, 'e');
                // Two small slimes riding the dome, either side of the crown.
                foreach (float at in new[] { 0.2f, 0.8f })
                {
                    int cx = (int)(w * at);
                    int baseRow = top + DomeTop(left, right, cx) + 1;
                    Rider(grid, cx, baseRow);
                }
                // The crown: a band of gold with three points and a red stone, sitting on the top of the dome.
                int crownX = (int)(w / 2f) - 6, crownBase = top + 1;
                string[] crown =
                {
                    "y....y....y.",
                    "yy..yyy..yy.",
                    "yyyyyyyyyyy.",
                    "yyyyyrryyyyy",
                    "YYYYYYYYYYYY",
                };
                for (int y = 0; y < crown.Length; y++)
                    for (int x = 0; x < crown[y].Length; x++)
                        if (crown[y][x] != '.') Set(grid, crownBase - crown.Length + y + 1, crownX + x, crown[y][x]);
            }

            var rows = new string[top + h];
            for (int y = 0; y < top + h; y++)
            {
                var line = new char[canvasWidth];
                for (int x = 0; x < canvasWidth; x++) line[x] = grid[y, x];
                rows[y] = new string(line);
            }
            return Trimmed(rows);
        }

        // The first row of the body at a column.
        static int DomeTop(int[] left, int[] right, int x)
        {
            for (int r = 0; r < left.Length; r++)
                if (x >= left[r] && x <= right[r]) return r;
            return 0;
        }

        // A little slime, seven by five, its foot on that row.
        static void Rider(char[,] grid, int cx, int footRow)
        {
            string[] rider =
            {
                "..LLL..",
                ".LwMMm.",
                "LMeMeMm",
                "MMMMMmm",
                ".mmmmm.",
            };
            for (int y = 0; y < rider.Length; y++)
                for (int x = 0; x < rider[y].Length; x++)
                    if (rider[y][x] != '.') Set(grid, footRow - rider.Length + y, cx - 3 + x, rider[y][x]);
        }

        static bool Inside(char[,] grid, int y, int x) => y >= 0 && x >= 0 && y < grid.GetLength(0) && x < grid.GetLength(1);

        static void Set(char[,] grid, int y, int x, char c)
        {
            if (Inside(grid, y, x)) grid[y, x] = c;
        }

        // Without the empty rows at the top, so the picture's height is the body's.
        static string[] Trimmed(string[] rows)
        {
            int first = 0;
            while (first < rows.Length - 1 && rows[first].Trim('.').Length == 0) first++;
            var result = new string[rows.Length - first];
            Array.Copy(rows, first, result, 0, result.Length);
            return result;
        }

        // An elite leader (2026-11-24, in place of drawing it 1.3 times bigger, which made its pixels uneven): a ring of
        // its rank's colour one pixel round its outline, on its sides and top but not under its feet, so it stands where
        // it stood. The picture grows two pixels wider and one taller.
        public static PixelImage Crowned(PixelImage image, Rgba color)
        {
            var result = new PixelImage(image.Width + 2, image.Height + 1);
            result.Draw(image, 1, 1);
            for (int y = 0; y < result.Height; y++)
            {
                for (int x = 0; x < result.Width; x++)
                {
                    if (image.IsSet(x - 1, y - 1)) continue;
                    bool beside = image.IsSet(x - 2, y - 1) || image.IsSet(x, y - 1) || image.IsSet(x - 1, y - 2) || image.IsSet(x - 1, y);
                    if (beside) result[x, y] = color;
                }
            }
            return result;
        }
    }
}
