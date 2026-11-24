using System;
using Game.Core;

namespace Game.Client
{
    // The mines' rails laid on the ground (2026-11-25, "철도도 마치 타일처럼 입체감 없이 칸을 꽉 채우고"). The old tile
    // was a square of its own packed earth with two rails always running north and south, so a run east and west read
    // as a band of stripes across the way. Now a tile holds only the track: a bed of dark ballast, wooden sleepers with
    // a lit top and a dark foot, and two iron rails with a bright head, a dark web and a short shadow cast south, so
    // the cave's own floor shows between the sleepers and the rails sit up on them. The track follows its neighbours
    // (RailMask): straight either way, a quarter curve where it turns, a crossing or a branch where three or four meet,
    // and a timber stop at a dead end.
    static class RailArt
    {
        // Sides joined to more rail: N 1, E 2, S 4, W 8.
        public const int Variants = 16;
        static readonly PixelImage[] Tiles = new PixelImage[Variants];

        const int Size = 16;
        static readonly Rgba Ballast = Rgba.Hex("#4A4038").WithAlpha(170);
        static readonly Rgba Grit = Rgba.Hex("#7A6E60").WithAlpha(190);
        static readonly Rgba SleeperLit = Rgba.Hex("#B4844E");
        static readonly Rgba SleeperTop = Rgba.Hex("#8A6440");
        static readonly Rgba SleeperFoot = Rgba.Hex("#4A321E");
        static readonly Rgba SleeperEnd = Rgba.Hex("#6E4E30");
        static readonly Rgba RailHead = Rgba.Hex("#D2D8E0");
        static readonly Rgba RailWeb = Rgba.Hex("#727886");
        static readonly Rgba RailDark = Rgba.Hex("#4A4E58");
        static readonly Rgba Shadow = new Rgba(0, 0, 0, 90);

        // Which sides of the rail at x, y meet more rail.
        public static int Mask(TileMap map, int x, int y)
        {
            bool Joins(int nx, int ny) => map.InBounds(nx, ny) && map[nx, ny] == TileType.Rail;
            return (Joins(x, y + 1) ? 1 : 0) | (Joins(x + 1, y) ? 2 : 0) | (Joins(x, y - 1) ? 4 : 0) | (Joins(x - 1, y) ? 8 : 0);
        }

        public static PixelImage Rail(int mask)
        {
            mask &= Variants - 1;
            return Tiles[mask] ??= Draw(mask);
        }

        static PixelImage Draw(int mask)
        {
            var image = new PixelImage(Size, Size);
            switch (mask)
            {
                // A turn: a quarter curve round the corner the two sides share.
                case 1 | 2: Curve(image, Size, 0); break;
                case 2 | 4: Curve(image, Size, Size); break;
                case 4 | 8: Curve(image, 0, Size); break;
                case 8 | 1: Curve(image, 0, 0); break;
                default:
                {
                    bool north = (mask & 1) != 0, east = (mask & 2) != 0, south = (mask & 4) != 0, west = (mask & 8) != 0;
                    bool across = east || west, along = north || south;
                    // Alone, or only joined east and west, the track runs that way; otherwise north and south first,
                    // and a third or fourth side branches off over it.
                    bool vertical = along || !across;
                    if (vertical)
                    {
                        // A branch off a run east and west reaches only to the middle; a dead end runs the whole tile.
                        bool branch = across && (north != south);
                        bool fullNorth = north || !branch, fullSouth = south || !branch;
                        Straight(image, true, fullNorth ? 0 : 8, fullSouth ? Size : 8);
                    }
                    if (across)
                    {
                        // Over a run north and south, a side on its own reaches only to the middle.
                        bool fullWest = west || !vertical, fullEast = east || !vertical;
                        Straight(image, false, fullWest ? 0 : 8, fullEast ? Size : 8);
                    }
                    // A dead end: a timber stop across the rails at the open end.
                    if (mask == 1) Stop(image, true, Size - 3);
                    else if (mask == 4) Stop(image, true, 1);
                    else if (mask == 2) Stop(image, false, 1);
                    else if (mask == 8) Stop(image, false, Size - 3);
                    break;
                }
            }
            return image;
        }

        // A straight run, north-south (vertical) or east-west, from `from` to `to` along it (in pixels, 0..16).
        static void Straight(PixelImage image, bool vertical, int from, int to)
        {
            void Put(int along, int across, Rgba color, bool blend)
            {
                int x = vertical ? across : along, y = vertical ? along : across;
                if (blend) image.Blend(x, y, color);
                else image[x, y] = color;
            }
            // The bed of ballast, a little narrower than the sleepers, speckled.
            for (int a = from; a < to; a++)
                for (int c = 2; c <= 13; c++)
                {
                    Put(a, c, Ballast, true);
                    if (PixelNoise.Hash(a, c, vertical ? 41 : 43) % 7 == 0) Put(a, c, Grit, true);
                }
            // Sleepers every five pixels: three thick, lit on the north (or west) face, dark at the foot.
            for (int a = from; a < to; a++)
            {
                int phase = ((a - 1) % 5 + 5) % 5;
                if (phase > 2) continue;
                for (int c = 1; c <= 14; c++)
                {
                    var color = c == 1 || c == 14 ? SleeperEnd : phase == 0 ? SleeperLit : phase == 1 ? SleeperTop : SleeperFoot;
                    Put(a, c, color, false);
                }
            }
            // The rails: a bright head, a dark web, and a pixel of shadow beside them (south, or east of a north-south
            // rail, the way the light falls from the top left).
            foreach (int c in new[] { 4, 11 })
            {
                for (int a = from; a < to; a++)
                {
                    if (vertical)
                    {
                        Put(a, c - 1, RailWeb, false);
                        Put(a, c, RailHead, false);
                        Put(a, c + 1, RailDark, false);
                        Put(a, c + 2, Shadow, true);
                    }
                    else
                    {
                        Put(a, c - 1, RailHead, false);
                        Put(a, c, RailWeb, false);
                        Put(a, c + 1, RailDark, false);
                        Put(a, c + 2, Shadow, true);
                    }
                }
            }
        }

        // A quarter curve round the corner (cx, cy) of the tile: the track's middle at radius 8.
        static void Curve(PixelImage image, int cx, int cy)
        {
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    if (d >= 2 && d <= 14)
                    {
                        image.Blend(x, y, Ballast);
                        if (PixelNoise.Hash(x, y, 47) % 7 == 0) image.Blend(x, y, Grit);
                    }
                }
            // Sleepers across the curve, every 4.2 pixels along its middle.
            for (float t = 0.12f; t < MathF.PI / 2; t += 5f / 8f)
            {
                // The angle from the corner, swept across the quarter that lies inside the tile.
                float angle = Start(cx, cy) + t;
                float ux = MathF.Cos(angle), uy = MathF.Sin(angle);
                for (float r = 1.5f; r <= 14.5f; r += 0.5f)
                {
                    int x = (int)MathF.Floor(cx + ux * r), y = (int)MathF.Floor(cy + uy * r);
                    var color = r < 2.5f || r > 13.5f ? SleeperEnd : SleeperLit;
                    image[x, y] = color;
                    // The middle of the sleeper, the pixel just past it round the curve.
                    float mid = angle + 0.08f;
                    int mx = (int)MathF.Floor(cx + MathF.Cos(mid) * r), my = (int)MathF.Floor(cy + MathF.Sin(mid) * r);
                    if (!Same(image[mx, my], SleeperLit) && !Same(image[mx, my], SleeperEnd)) image[mx, my] = SleeperTop;
                    // The foot of the sleeper: the pixel just past it round the curve.
                    float back = angle + 0.17f;
                    int fx = (int)MathF.Floor(cx + MathF.Cos(back) * r), fy = (int)MathF.Floor(cy + MathF.Sin(back) * r);
                    var under = image[fx, fy];
                    if (!Same(under, SleeperLit) && !Same(under, SleeperTop) && !Same(under, SleeperEnd)) image[fx, fy] = SleeperFoot;
                }
            }
            // The rails at radii 4.5 and 11.5: head inside, web and dark outside, shadow beyond.
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    foreach (float r in new[] { 4.5f, 11.5f })
                    {
                        float off = d - r;
                        if (off >= -1.5f && off < -0.5f) image[x, y] = RailWeb;
                        else if (off >= -0.5f && off < 0.5f) image[x, y] = RailHead;
                        else if (off >= 0.5f && off < 1.5f) image[x, y] = RailDark;
                        else if (off >= 1.5f && off < 2.3f) image.Blend(x, y, Shadow);
                    }
                }
        }

        static bool Same(Rgba a, Rgba b) => a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A;

        // The angle at which the quarter inside the tile begins, from its corner.
        static float Start(int cx, int cy) => (cx, cy) switch
        {
            (0, 0) => 0,
            (Size, 0) => MathF.PI / 2,
            (Size, Size) => MathF.PI,
            _ => MathF.PI * 1.5f,
        };

        // A timber stop across the rails at `at` along the track: two pixels of beam with a dark face.
        static void Stop(PixelImage image, bool vertical, int at)
        {
            for (int c = 2; c <= 13; c++)
            {
                for (int k = 0; k < 2; k++)
                {
                    int x = vertical ? c : at + k, y = vertical ? at + k : c;
                    image[x, y] = k == 0 ? Rgba.Hex("#9A6A3A") : Rgba.Hex("#5A3A20");
                }
            }
            // Two iron caps where the rails meet it.
            foreach (int c in new[] { 4, 11 })
            {
                int x = vertical ? c : at, y = vertical ? at : c;
                image[x, y] = RailHead;
            }
        }
    }
}
