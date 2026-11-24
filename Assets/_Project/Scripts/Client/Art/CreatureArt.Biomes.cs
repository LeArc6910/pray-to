using System;
using System.Collections.Generic;

namespace Game.Client
{
    // The first two biomes' monsters (2026-11-24, Docs/BiomeRework.md B1b), in the same code-drawn style and at their size
    // class (MonsterSizes: XS 8~10 pixels, S 12~14, L 24~32, XL 40~56 with the outline): the dirt cave's rat (XS) and
    // big beetle (L) and the slime mother (XL), the mushroom forest's puffball (S), cap giant (L) and mycelium mother
    // (XL). The big ones are laid out from parts like the big slimes (CreatureArt.Big.cs); the small ones are grids. The
    // variants (an armoured beetle, a glowing shooter, a myconid captain) are their kind's picture in their own colours.
    static partial class CreatureArt
    {
        // A cave rat (XS): low and long, a round ear, a pink nose and a bare tail. It scurries: legs and tail between
        // the frames.
        static readonly string[][] Rat =
        {
            new[]
            {
                "....Lm..",
                "..LLMMm.",
                ".LMMMMeM",
                "tMMMMMMp",
                ".tmmmmm.",
                "..d..d..",
            },
            new[]
            {
                "....Lm..",
                "..LLMMm.",
                "tLMMMMeM",
                ".MMMMMMp",
                "..mmmmm.",
                ".d...d..",
            },
        };

        // A puffball (S): a round sac of spores with dark pores and two eyes; it swells and puffs between the frames.
        static readonly string[][] Puffball =
        {
            new[]
            {
                "...........",
                "...LLLLL...",
                ".LLwwMMMLm.",
                "LMwwMMoMMMm",
                "LMwMMMMMMMm",
                "MMMeMMMeMMm",
                "MMMeMMMeMmm",
                "MoMMMMMMMmm",
                "mMMMMMoMmmd",
                ".mmmmmmmmd.",
                "..ddddddd..",
            },
            new[]
            {
                "..y....y...",
                "y..LLLLL..y",
                ".LLwwMMMLm.",
                "LMwwMMoMMMm",
                "MMMeMMMeMMm",
                "MMMeMMMeMmm",
                "MMMMMMMMMmm",
                "MoMMMMMMMmm",
                "mMMMMMoMmmd",
                ".mmmmmmmmd.",
                "..ddddddd..",
            },
        };

        // The colours the first two biomes' kinds add to their ramp.
        static readonly Dictionary<string, Dictionary<char, Rgba>> BiomeExtras = new Dictionary<string, Dictionary<char, Rgba>>
        {
            ["cave_rat"] = new Dictionary<char, Rgba>
            {
                ['t'] = Rgba.Hex("#C89488"),
                ['p'] = Rgba.Hex("#F0A8A8"),
            },
            ["puffball"] = new Dictionary<char, Rgba>
            {
                ['o'] = Rgba.Hex("#7A6A30"),
                ['y'] = Rgba.Hex("#F4FFB8"),
            },
            // Dark legs and head, a bone horn and mandibles.
            ["big_beetle"] = new Dictionary<char, Rgba>
            {
                ['K'] = Rgba.Hex("#4A3424"),
                ['k'] = Rgba.Hex("#6E5038"),
                ['h'] = Rgba.Hex("#EADCBC"),
                ['H'] = Rgba.Hex("#B8A684"),
            },
            // Iron plates: a steel shell (its sheet colour), darker legs, a pale steel horn.
            ["armored_beetle"] = new Dictionary<char, Rgba>
            {
                ['K'] = Rgba.Hex("#34363E"),
                ['r'] = Rgba.Hex("#F05A3A"),
                ['h'] = Rgba.Hex("#D8DCE8"),
            },
            // A cap of the wood (its sheet's blue), white warts, a cream stalk and gills.
            ["cap_giant"] = new Dictionary<char, Rgba>
            {
                ['c'] = Rgba.Hex("#F0E6D6"),
                ['C'] = Rgba.Hex("#C4AE92"),
                ['g'] = Rgba.Hex("#9C8A72"),
                ['s'] = Rgba.Hex("#FFFFFF"),
            },
            ["mycelium_mother"] = new Dictionary<char, Rgba>
            {
                ['c'] = Rgba.Hex("#E6DCC4"),
                ['C'] = Rgba.Hex("#B2A284"),
                ['g'] = Rgba.Hex("#7E7058"),
                ['y'] = Rgba.Hex("#D8FF7A"),
                ['p'] = Rgba.Hex("#E88AB0"),
                ['P'] = Rgba.Hex("#A85478"),
                ['R'] = Rgba.Hex("#6A5A3E"),
                ['r'] = Rgba.Hex("#E8E0C8"),
            },
            // The slime mother's young inside her: a paler green.
            ["slime_mother"] = new Dictionary<char, Rgba>
            {
                ['b'] = Rgba.Hex("#C8F4A0"),
                ['B'] = Rgba.Hex("#8CCB5C"),
            },
            // The glowing shooter's stalk is pale green; the captain's spear has a gold head.
            ["glow_shooter"] = new Dictionary<char, Rgba>
            {
                ['c'] = Rgba.Hex("#E4F8C8"),
                ['C'] = Rgba.Hex("#A8C890"),
            },
            ["myconid_captain"] = new Dictionary<char, Rgba>
            {
                ['c'] = Rgba.Hex("#F4E8D8"),
                ['C'] = Rgba.Hex("#C8B49C"),
                ['K'] = Rgba.Hex("#5A3A22"),
                ['h'] = Rgba.Hex("#FFD860"),
            },
        };

        // The small grids of the first two biomes' kinds and variants, or null.
        static string[][] BiomeFrames(string id) => id switch
        {
            "cave_rat" => Rat,
            "puffball" => Puffball,
            "armored_beetle" => Beetle,
            "glow_shooter" => ThornCap,
            "myconid_captain" => MushroomSoldier,
            _ => null,
        };

        // ---- The big ones ----

        // A canvas of '.' to lay parts on, and its rows.
        static char[,] Canvas(int width, int height)
        {
            var grid = new char[height, width];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++) grid[y, x] = '.';
            return grid;
        }

        static string[] Rows(char[,] grid)
        {
            int height = grid.GetLength(0), width = grid.GetLength(1);
            var rows = new string[height];
            for (int y = 0; y < height; y++)
            {
                var line = new char[width];
                for (int x = 0; x < width; x++) line[x] = grid[y, x];
                rows[y] = new string(line);
            }
            return Trimmed(rows);
        }

        // An upper half-ellipse (a dome, a shell, a cap) from its top row down to its flat base, lit from the upper left:
        // its rim light on the left two thirds, the right and the lower rows in shade, the base dark. The letters are the
        // ramp's (L M m d) unless others are given.
        static void Dome(char[,] grid, int left, int top, int width, int height, char light = 'L', char main = 'M', char shade = 'm', char dark = 'd')
        {
            float half = width / 2f;
            int previousL = int.MaxValue, previousR = int.MinValue;
            for (int r = 0; r < height; r++)
            {
                float t = (height - r - 0.5f) / height;
                float span = half * MathF.Sqrt(Math.Max(0, 1 - t * t));
                int l = (int)MathF.Round(half - span), rr = (int)MathF.Round(half + span) - 1;
                for (int x = l; x <= rr; x++)
                {
                    bool rim = r == 0 || x < previousL || x > previousR;
                    char c = main;
                    if (rim && x < width * 0.68f) c = light;
                    else if (x == l && r < height * 0.6f) c = light;
                    else if (x >= rr - (width >= 30 ? 2 : 1) || x > width * 0.8f && r > height * 0.35f) c = shade;
                    if (r >= height - 2 && x > width * 0.5f) c = shade;
                    if (r == height - 1) c = dark;
                    Set(grid, top + r, left + x, c);
                }
                previousL = l;
                previousR = rr;
            }
        }

        // A filled ellipse of one letter.
        static void Oval(char[,] grid, int cx, int cy, int rx, int ry, char c)
        {
            for (int y = -ry; y <= ry; y++)
                for (int x = -rx; x <= rx; x++)
                    if (x * x * ry * ry + y * y * rx * rx <= rx * rx * ry * ry + Math.Max(rx, ry)) Set(grid, cy + y, cx + x, c);
        }

        // A line of a letter, a pixel at a time.
        static void Stroke(char[,] grid, int x0, int y0, int x1, int y1, char c)
        {
            int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0 : i / (float)steps;
                Set(grid, (int)MathF.Round(y0 + (y1 - y0) * t), (int)MathF.Round(x0 + (x1 - x0) * t), c);
            }
        }

        // A big beetle (L): a high round shell split by its seam, a dark head with a horn and mandibles in front, three
        // pairs of legs that step between the frames. About 28 by 18.
        static string[] BigBeetle(int frame)
        {
            var g = Canvas(28, 19);
            // The legs first, under the shell: three on the near side, stepping.
            int[] feet = { 5, 11, 17 };
            for (int i = 0; i < feet.Length; i++)
            {
                int lean = (i + frame) % 2 == 0 ? -2 : 2;
                Stroke(g, feet[i], 13, feet[i] + lean / 2, 16, 'K');
                Stroke(g, feet[i] + lean / 2, 16, feet[i] + lean, 18, 'K');
                Set(g, 18, feet[i] + lean + (lean > 0 ? 1 : -1), 'K');
            }
            Dome(g, 0, 1, 22, 13);
            // The seam between the wing cases and the plates' edge.
            for (int y = 2; y <= 12; y++) Set(g, y, 13, y < 4 ? 'm' : 'd');
            for (int x = 2; x <= 20; x++)
                if (g[11, x] != '.') g[11, x] = x < 13 ? 'm' : 'd';
            // The gloss: a streak of white high on the near case, a speck beside it.
            foreach (var (x, y) in new[] { (5, 3), (6, 3), (7, 3), (4, 4), (5, 4), (9, 5) }) Set(g, y, x, 'w');
            // The head: dark, a lighter brow, an eye; the horn swept up and forward, mandibles below.
            Oval(g, 22, 10, 4, 3, 'K');
            Stroke(g, 20, 8, 24, 8, 'k');
            Set(g, 9, 23, 'e');
            Set(g, 9, 24, 'e');
            Stroke(g, 23, 7, 25, 5, 'h');
            Stroke(g, 25, 5, 26, 2, 'h');
            Set(g, 1, 27, 'h');
            Set(g, 6, 24, 'H');
            Stroke(g, 25, 12, 27, 13, 'h');
            Stroke(g, 25, 13, 26, 14, 'H');
            return Rows(g);
        }

        // A cap giant (L): a broad blue cap with white warts over a cream stalk with a face, stubby arms and two feet
        // that step. About 28 by 25.
        static string[] CapGiant(int frame)
        {
            var g = Canvas(28, 26);
            // The stalk: a barrel, shaded on its right, with a face.
            for (int y = 11; y <= 21; y++)
            {
                int bulge = y >= 13 && y <= 19 ? 1 : 0;
                for (int x = 8 - bulge; x <= 19 + bulge; x++) Set(g, y, x, x >= 17 + bulge ? 'C' : 'c');
            }
            foreach (var (x, y) in new[] { (11, 13), (11, 14), (16, 13), (16, 14) }) Set(g, y, x, 'e');
            Stroke(g, 12, 17, 15, 17, 'C');
            // Arms hanging, knuckles at their ends.
            int swing = frame == 0 ? 0 : 1;
            Stroke(g, 7, 14, 4, 18 - swing, 'C');
            Set(g, 19 - swing, 4, 'c');
            Set(g, 19 - swing, 3, 'C');
            Stroke(g, 20, 14, 23, 18 + swing - 1, 'C');
            Set(g, 18 + swing, 23, 'c');
            Set(g, 18 + swing, 24, 'C');
            // Feet: one a row up as it steps.
            for (int foot = 0; foot < 2; foot++)
            {
                int x0 = foot == 0 ? 9 : 15, lift = foot == frame ? 1 : 0;
                for (int y = 22; y <= 24 - lift; y++)
                    for (int x = x0; x <= x0 + 3; x++) Set(g, y, x, y == 24 - lift ? 'C' : x == x0 + 3 ? 'C' : 'c');
            }
            // The cap, its gills under it, and its warts.
            Dome(g, 0, 0, 28, 10);
            for (int x = 3; x <= 24; x++) Set(g, 10, x, x % 2 == 0 ? 'g' : 'C');
            foreach (var (x, y) in new[] { (6, 3), (7, 3), (6, 4), (12, 1), (13, 1), (13, 2), (19, 4), (20, 4), (20, 5), (9, 7), (10, 7), (23, 7), (16, 7), (3, 7) })
                Set(g, y, x, 's');
            return Rows(g);
        }

        // The mycelium mother (XL): a wide frilled bracket of a cap studded with glowing spores and young pink caps,
        // over a thick stalk with a broad face, its roots spread over the floor. The spores twinkle and the roots creep
        // between the frames. About 50 by 38.
        static string[] MyceliumMother(int frame)
        {
            var g = Canvas(50, 40);
            int top = 4;
            // The roots first, under everything: tendrils out to either side along the floor.
            var roots = new (int X0, int Y0, int X1, int Y1)[]
            {
                (18, 33, 6, 38), (20, 34, 13, 39), (24, 35, 22, 39), (29, 34, 33, 39), (31, 33, 43, 38), (16, 32, 3, 35), (33, 32, 47, 35),
            };
            for (int i = 0; i < roots.Length; i++)
            {
                var (x0, y0, x1, y1) = roots[i];
                int creep = frame == 1 && i % 2 == 0 ? (x1 < x0 ? -1 : 1) : 0;
                Stroke(g, x0, y0, x1 + creep, y1, 'R');
            }
            // The stalk: thick and cream, shaded right, threads of mycelium down it, a broad face.
            for (int y = top + 12; y <= 34; y++)
            {
                int widen = y > 29 ? (y - 29) : 0;
                for (int x = 15 - widen; x <= 34 + widen; x++) Set(g, y, x, x >= 31 + widen ? 'C' : 'c');
            }
            for (int y = top + 13; y <= 33; y += 2)
            {
                Set(g, y, 17 + (y / 2) % 3, 'r');
                Set(g, y + 1, 29 - (y / 3) % 2, 'r');
            }
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 3; x++)
                {
                    if ((y == 0 || y == 3) && x != 1) continue;
                    Set(g, top + 18 + y, 19 + x, 'e');
                    Set(g, top + 18 + y, 28 + x, 'e');
                }
            Stroke(g, 22, top + 25, 27, top + 25, 'e');
            Set(g, top + 24, 21, 'e');
            Set(g, top + 24, 28, 'e');
            // The bracket: a wide low dome, a frilled rim under it, gills.
            Dome(g, 0, top, 50, 12);
            for (int x = 1; x <= 48; x++)
            {
                Set(g, top + 12, x, x % 3 == 0 ? 'm' : 'g');
                if (x % 4 == 1 && x > 2 && x < 47) Set(g, top + 13, x, 'm');
            }
            // Young pink caps on the bracket.
            foreach (int cx in new[] { 12, 25, 37 })
            {
                int lift = cx == 25 ? 1 : 0;
                Dome(g, cx - 3, top - 3 - lift, 7, 3, 'p', 'p', 'P', 'P');
                Set(g, top - lift, cx, 'r');
            }
            // Spores that glow, a different few lit each frame.
            var spores = new (int X, int Y)[] { (6, 8), (10, 5), (16, 9), (20, 4), (30, 6), (34, 9), (41, 6), (44, 9), (27, 10), (8, 10), (38, 3), (14, 2) };
            for (int i = 0; i < spores.Length; i++)
                if ((i + frame) % 3 != 0) Set(g, top + spores[i].Y - 1, spores[i].X, 'y');
            return Rows(g);
        }

        // The big kinds of the first two biomes, or null.
        static string[][] BiomeBigFrames(string id) => id switch
        {
            "big_beetle" => new[] { BigBeetle(0), BigBeetle(1) },
            "cap_giant" => new[] { CapGiant(0), CapGiant(1) },
            "mycelium_mother" => new[] { MyceliumMother(0), MyceliumMother(1) },
            // The slime mother (XL): a slime 46 wide with her young swimming inside her.
            "slime_mother" => new[] { Blob(46, 32, 0, BlobLook.Brood), Blob(46, 32, 1, BlobLook.Brood) },
            _ => null,
        };

        // The young inside a slime mother: little slimes of the paler green, seven by five, their eyes the body's.
        static void Brood(char[,] grid, int top, int width, int height, int frame)
        {
            string[] young =
            {
                "..bbb..",
                ".bbbbB.",
                "bbebebB",
                "bbbbbBB",
                ".BBBBB.",
            };
            var places = new (float X, float Y)[] { (0.22f, 0.64f), (0.5f, 0.74f), (0.78f, 0.6f) };
            for (int i = 0; i < places.Length; i++)
            {
                int cx = (int)(width * places[i].X) + (frame == 1 && i == 1 ? 1 : 0), cy = top + (int)(height * places[i].Y) - (frame == 1 && i != 1 ? 1 : 0);
                for (int y = 0; y < young.Length; y++)
                    for (int x = 0; x < young[y].Length; x++)
                        if (young[y][x] != '.' && Inside(grid, cy + y, cx - 3 + x) && grid[cy + y, cx - 3 + x] != '.') grid[cy + y, cx - 3 + x] = young[y][x];
            }
        }
    }
}
