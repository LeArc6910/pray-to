using System.Collections.Generic;

namespace Game.Client
{
    // Which way a player sprite looks. Left is Side flipped.
    enum Look
    {
        Down,
        Up,
        Side,
    }

    // Players, monsters and the boss, at the tiles' 16 pixels a tile. Each comes in a few frames: players walk in
    // three directions, monsters bob or squash on the spot. Monsters are drawn in their sheet colour
    // (monsters.csv), so a recoloured monster needs no change here, and a monster with no drawing of its own is a
    // slime in its colour.
    static class CreatureArt
    {
        public const int WalkFrames = 4;
        public const int IdleFrames = 2;

        static readonly Rgba Eye = Rgba.Hex("#1C1626");
        static readonly Rgba Shine = Rgba.Hex("#FFFFFF");

        // ---- Players ----

        static readonly string[] HeadDown =
        {
            "..hhhhhh..",
            ".hHHHHHhh.",
            "hHhhhhhhhh",
            "hhsssssshh",
            "hhsesseshh",
            ".hsesseSh.",
            "..ssssSS..",
        };

        static readonly string[] HeadUp =
        {
            "..hhhhhh..",
            ".hHHHHHhh.",
            "hHHHhhHHhh",
            "hhHhhhhhhh",
            "hhhhhhhhhh",
            ".hhhhhhhh.",
            "..SssssS..",
        };

        static readonly string[] HeadSide =
        {
            "..hhhhh...",
            ".hHHHHHh..",
            "hHHhhhhhh.",
            "hhhhsssss.",
            "hhhsssses.",
            ".hhsssses.",
            "..hSsssS..",
        };

        static readonly string[] BodyDown =
        {
            ".tTTUUTTt.",
            "tTTTUUTTTt",
            "sTTTTTTTTs",
            ".bbbBBbbb.",
            ".tTTTTTTt.",
        };

        static readonly string[] BodyUp =
        {
            ".tTTTTTTt.",
            "tTTTTTTTTt",
            "sTTTTTTTTs",
            ".bbbbbbbb.",
            ".tTTTTTTt.",
        };

        static readonly string[] BodySide =
        {
            "..tTTTTt..",
            "..tTTUTt..",
            "..tTTsTt..",
            "..bbbBbb..",
            "..tTTTTt..",
        };

        // Legs for the walk cycle: stand, step with one foot, stand, step with the other.
        static readonly string[][] LegsFront =
        {
            new[] { "..pP..Pp..", "..oo..oo.." },
            new[] { "..pP..oo..", "..oo......" },
            new[] { "..pP..Pp..", "..oo..oo.." },
            new[] { "..oo..Pp..", "......oo.." },
        };

        static readonly string[][] LegsSide =
        {
            new[] { "...pPp....", "...oooo..." },
            new[] { "..pP.Pp...", ".oo...oo.." },
            new[] { "...pPp....", "...oooo..." },
            new[] { "...PpP....", "..oo.oo..." },
        };

        static readonly Dictionary<char, Rgba> PlayerColors = new Dictionary<char, Rgba>
        {
            ['h'] = Rgba.Hex("#6B4226"),
            ['H'] = Rgba.Hex("#93613A"),
            ['s'] = Rgba.Hex("#F2C49C"),
            ['S'] = Rgba.Hex("#D39A74"),
            ['e'] = Eye,
            ['b'] = Rgba.Hex("#4A3020"),
            ['B'] = Rgba.Hex("#E0B850"),
            ['p'] = Rgba.Hex("#3E3A56"),
            ['P'] = Rgba.Hex("#2C2940"),
            ['o'] = Rgba.Hex("#5A3A24"),
        };

        // A player in a tunic of the given colour: blue for you, other colours for the others.
        public static PixelImage Player(Look look, int frame, Rgba tunic)
        {
            var ramp = Ramp.Of(tunic);
            var colors = new Dictionary<char, Rgba>(PlayerColors)
            {
                ['T'] = ramp.Main,
                ['t'] = ramp.Shade,
                ['U'] = ramp.Light,
            };
            var head = look == Look.Down ? HeadDown : look == Look.Up ? HeadUp : HeadSide;
            var body = look == Look.Down ? BodyDown : look == Look.Up ? BodyUp : BodySide;
            var legs = (look == Look.Side ? LegsSide : LegsFront)[frame % WalkFrames];
            // An empty row on top, so a step can lift the head and body a pixel without clipping the hair.
            var rows = new List<string> { new string('.', head[0].Length) };
            rows.AddRange(head);
            rows.AddRange(body);
            rows.AddRange(legs);
            var image = PixelImage.FromRows(colors, rows.ToArray());
            if (frame % 2 == 1) image = Lift(image, rows.Count - legs.Length);
            return image.Outlined(Rgba.Hex("#140E10"));
        }

        // Moves the rows above the legs up by one pixel. The lowest of them stays as well, so the body stretches
        // down to the legs instead of leaving a gap.
        static PixelImage Lift(PixelImage image, int upper)
        {
            var result = image.Copy();
            for (int y = 1; y < upper; y++)
            {
                for (int x = 0; x < image.Width; x++) result[x, y - 1] = image[x, y];
            }
            return result;
        }

        // ---- Monsters ----

        static Dictionary<char, Rgba> RampColors(Ramp ramp) => new Dictionary<char, Rgba>
        {
            ['L'] = ramp.Light,
            ['M'] = ramp.Main,
            ['m'] = ramp.Shade,
            ['d'] = ramp.Dark,
            ['e'] = Eye,
            ['w'] = Shine,
        };

        static readonly string[][] Slime =
        {
            new[]
            {
                ".....LLLL.....",
                "...LLMMMMMm...",
                "..LMwwMMMMMm..",
                ".LMwwMMMMMMMm.",
                ".MMMMMMMMMMMm.",
                "MMMMeMMMMeMMMm",
                "MMMMeMMMMeMMmm",
                "MMMMMMMMMMMmmm",
                "mMMMMMMMMMmmmd",
                ".mmmmmmmmmmmd.",
                "..dddddddddd..",
            },
            new[]
            {
                "..............",
                "....LLLLLL....",
                "..LLMMMMMMMm..",
                ".LMwwMMMMMMMm.",
                "LMwwMMMMMMMMMm",
                "MMMMeMMMMMeMMm",
                "MMMMeMMMMMeMmm",
                "MMMMMMMMMMMMmm",
                "mMMMMMMMMMMmmd",
                "mmmmmmmmmmmmmd",
                ".dddddddddddd.",
            },
        };

        static readonly string[][] Mushroom =
        {
            new[]
            {
                "....LLLL....",
                "..LLMMMMLL..",
                ".LMMwMMMMMm.",
                "LMMwwMMLLMMm",
                "LMMMMMMLLMmm",
                "MLLMMMMMMMmm",
                "mLLMMMMMMmmd",
                ".dmmmmmmmmd.",
                "...cccccC...",
                "...cecceC...",
                "...cecceC...",
                "...cccccC...",
                "..CCCCCCCC..",
            },
            new[]
            {
                "............",
                "....LLLL....",
                ".LLLMMMMLLL.",
                "LMMwwMMMMMMm",
                "LMwwMMMLLMMm",
                "MMMMMMMLLMmm",
                "MLLMMMMMMMmm",
                "mdmmmmmmmmmd",
                "...cecceC...",
                "...cecceC...",
                "...cccccC...",
                "...cccccC...",
                "..CCCCCCCC..",
            },
        };

        static readonly string[][] Beetle =
        {
            new[]
            {
                "....LLLLLL......",
                "..LLMMMMMMLL....",
                ".LMwwMMMMMMMm...",
                "LMwMMMMMdMMMmKKh",
                "MMMMMMMMdMMMmKrK",
                "MMMMMMMMdMMMmKKK",
                "mMMMMMMMdMMmmKK.",
                ".mmmmmmmmmmmdK..",
                "..KK..KK..KK....",
                ".K...K...K......",
            },
            new[]
            {
                "....LLLLLL......",
                "..LLMMMMMMLL....",
                ".LMwwMMMMMMMm...",
                "LMwMMMMMdMMMmKKh",
                "MMMMMMMMdMMMmKrK",
                "MMMMMMMMdMMMmKKK",
                "mMMMMMMMdMMmmKK.",
                ".mmmmmmmmmmmdK..",
                "..KK..KK..KK....",
                "...K...K...K....",
            },
        };

        // Wings up, then down.
        static readonly string[][] Bat =
        {
            new[]
            {
                "M.............M",
                "Mm...L...L...mM",
                ".Mm..LMMML..mM.",
                "..Mm.MeMeM.mM..",
                "...MmMMMMMmM...",
                ".....MwMMm.....",
                "......mmm......",
                ".......d.......",
            },
            new[]
            {
                "...............",
                ".....L...L.....",
                ".....LMMML.....",
                ".....MeMeM.....",
                "..MmmMMMMMmmM..",
                ".Mm..MwMMm..mM.",
                "Mm....mmm....mM",
                "M......d......M",
            },
        };

        // A floating flame with a face; its wisps and tail flicker between the frames.
        static readonly string[][] Wisp =
        {
            new[]
            {
                ".....LL.....",
                "....LwwL....",
                "...LwMMwL...",
                "..LwMMMMwL..",
                ".LMMMMMMMML.",
                ".MMeMMMMeMM.",
                ".MMeMMMMeMM.",
                ".mMMMMMMMMm.",
                "..mMMmmMMm..",
                "...mm..mm...",
                "....m..m....",
            },
            new[]
            {
                "....L..L....",
                "....LwwL....",
                "...LwMMwL...",
                "..LwMMMMwL..",
                ".LMMMMMMMML.",
                ".MMeMMMMeMM.",
                ".MMeMMMMeMM.",
                ".mMMMMMMMMm.",
                "..mMMmmMMm..",
                "..mm..mm....",
                "...m....m...",
            },
        };

        // A summoned spirit: a little ghost whose tail ripples between the frames.
        static readonly string[][] Spirit =
        {
            new[]
            {
                "...LLLL...",
                "..LwwMML..",
                ".LwMMMMMm.",
                ".MMeMMeMm.",
                ".MMeMMeMm.",
                ".MMMMMMMm.",
                ".mMMMMMmm.",
                ".mMmMMmMm.",
                "..m.mm.m..",
            },
            new[]
            {
                "...LLLL...",
                "..LwwMML..",
                ".LwMMMMMm.",
                ".MMeMMeMm.",
                ".MMeMMeMm.",
                ".MMMMMMMm.",
                ".mMMMMMmm.",
                ".mmMmmMmm.",
                ".m..mm..m.",
            },
        };

        // A spirit in its skill's colour. It faces right; the view flips it to face left.
        public static PixelImage Minion(Rgba color, int frame)
        {
            var ramp = Ramp.Of(color);
            return PixelImage.FromRows(RampColors(ramp), Spirit[frame % Spirit.Length]).Outlined(ramp.Outline);
        }

        static readonly Dictionary<string, Dictionary<char, Rgba>> Extras = new Dictionary<string, Dictionary<char, Rgba>>
        {
            ["spore_shooter"] = new Dictionary<char, Rgba>
            {
                ['c'] = Rgba.Hex("#ECDCC4"),
                ['C'] = Rgba.Hex("#BFA88C"),
            },
            ["myconid"] = new Dictionary<char, Rgba>
            {
                ['c'] = Rgba.Hex("#F0E2D0"),
                ['C'] = Rgba.Hex("#C8B49C"),
            },
            ["cave_beetle"] = new Dictionary<char, Rgba>
            {
                ['K'] = Rgba.Hex("#58422E"),
                ['r'] = Rgba.Hex("#F05A3A"),
                ['h'] = Rgba.Hex("#EADCBC"),
            },
        };

        // Monsters face right; the view flips them to face left.
        public static PixelImage Monster(string id, Rgba color, int frame)
        {
            var ramp = Ramp.Of(color);
            var colors = RampColors(ramp);
            if (Extras.TryGetValue(id, out var extra))
            {
                foreach (var pair in extra) colors[pair.Key] = pair.Value;
            }
            var frames = id switch
            {
                "spore_shooter" => Mushroom,
                "myconid" => Mushroom,
                "crystal_bat" => Bat,
                "glow_bat" => Bat,
                "fire_bat" => Bat,
                "crystal_wisp" => Wisp,
                "frost_wisp" => Wisp,
                "cave_beetle" => Beetle,
                _ => Slime,
            };
            return PixelImage.FromRows(colors, frames[frame % frames.Length]).Outlined(ramp.Outline);
        }

        // ---- The boss ----

        // The cave warden, a golem of stacked stone blocks with a glowing core. Drawn from blocks rather than a
        // grid because it is big; the second frame breathes, raising the upper body a pixel.
        public static PixelImage Boss(string id, Rgba color, int frame) => id switch
        {
            "crystal_queen" => Queen(frame),
            // The lava titan: the warden's build in black basalt, its seams and core burning.
            "lava_titan" => Warden(Rgba.Hex("#4A3A36"), frame, molten: true),
            _ => Warden(color, frame),
        };

        static PixelImage Warden(Rgba color, int frame, bool molten = false)
        {
            const int width = 36;
            const int height = 34;
            var image = new PixelImage(width, height);
            var stone = Ramp.Of(color);
            var darkStone = Ramp.Of(color.Scale(0.8f));
            var glow = Ramp.Of(Rgba.Hex("#FFB040"));
            var moss = Rgba.Hex("#5E8A3C");
            int bob = frame % 2 == 1 ? -1 : 0;

            Block(image, 9, 25, 7, 9, darkStone, 1);
            Block(image, 20, 25, 7, 9, darkStone, 2);
            Block(image, 5, 10 + bob, 26, 17, stone, 3);
            Block(image, 0, 16 + bob, 8, 12, darkStone, 4);
            Block(image, 28, 16 + bob, 8, 12, darkStone, 5);
            Block(image, 0, 7 + bob, 10, 10, stone, 6);
            Block(image, 26, 7 + bob, 10, 10, stone, 7);
            Block(image, 12, 1 + bob, 12, 10, stone, 8);

            // Brow and eyes.
            for (int x = 13; x < 23; x++) image[x, 5 + bob] = stone.Dark;
            foreach (int x in new[] { 15, 16, 19, 20 })
            {
                image[x, 6 + bob] = glow.Light;
                image[x, 7 + bob] = glow.Main;
            }

            // The core: a gem in the chest with a ring of light around it.
            int cx = 18;
            int cy = 17 + bob;
            for (int dy = -3; dy <= 3; dy++)
            {
                for (int dx = -3; dx <= 3; dx++)
                {
                    int d = System.Math.Abs(dx) + System.Math.Abs(dy);
                    if (d == 3) image[cx + dx, cy + dy] = stone.Dark;
                    else if (d == 2) image[cx + dx, cy + dy] = glow.Shade;
                    else if (d == 1) image[cx + dx, cy + dy] = glow.Main;
                    else if (d == 0) image[cx + dx, cy + dy] = glow.Light;
                }
            }
            image[cx - 1, cy - 1] = Rgba.White;

            // Cracks and moss.
            Line(image, 8, 14 + bob, new[] { (1, 1), (1, 0), (1, 1), (0, 1), (1, 1) }, stone.Dark);
            Line(image, 26, 20 + bob, new[] { (-1, 1), (0, 1), (-1, 1), (-1, 0) }, stone.Dark);
            if (molten) moss = Rgba.Hex("#FF7A1E");
            foreach (var (x, y) in new[] { (2, 8), (3, 8), (4, 8), (3, 9), (29, 8), (30, 8), (13, 2), (14, 2), (15, 2), (14, 3), (22, 11), (23, 11) })
            {
                image[x, y + bob] = moss;
            }
            if (molten)
            {
                // Glowing seams down the body and arms.
                Line(image, 10, 12 + bob, new[] { (1, 1), (0, 1), (1, 1), (1, 0), (0, 1) }, Rgba.Hex("#FFB040"));
                Line(image, 24, 12 + bob, new[] { (1, 1), (0, 1), (-1, 1), (0, 1), (1, 1) }, Rgba.Hex("#FFB040"));
                Line(image, 3, 18 + bob, new[] { (1, 1), (0, 1), (1, 1) }, Rgba.Hex("#FF7A1E"));
                Line(image, 31, 18 + bob, new[] { (-1, 1), (0, 1), (-1, 1) }, Rgba.Hex("#FF7A1E"));
            }
            return image.Outlined(stone.Outline);
        }

        // The crystal spider queen: a dark round body grown over with crystals, a head with a row of pink eyes, and
        // eight jointed legs. In the second frame her legs shift, pair by pair.
        static PixelImage Queen(int frame)
        {
            const int width = 42;
            const int height = 36;
            var image = new PixelImage(width, height);
            var body = Ramp.Of("#4E4062");
            var leg = Ramp.Of("#6E5C88");
            var crystal = Ramp.Of("#7CD2E2");
            var eye = Rgba.Hex("#FF5C8A");
            int shift = frame % 2;

            // Legs first, under the body: from the body's sides out to a knee and down to a foot.
            for (int i = 0; i < 4; i++)
            {
                int lift = (i + shift) % 2 == 0 ? -1 : 1;
                int startY = 17 + i * 3;
                int kneeY = 8 + i * 5 + lift;
                int footY = 15 + i * 5 + lift;
                foreach (int side in new[] { -1, 1 })
                {
                    int startX = 21 + side * 6;
                    int kneeX = 21 + side * (14 + i);
                    int footX = 21 + side * (19 + i / 2);
                    Thick(image, startX, startY, kneeX, kneeY, leg.Main, leg.Light);
                    Thick(image, kneeX, kneeY, footX, footY, leg.Shade, leg.Main);
                }
            }

            Ellipse(image, 21, 13, 11, 9, body);
            Ellipse(image, 21, 25, 7, 5, Ramp.Of("#5C4C72"));

            // Crystals on her back.
            foreach (var (x, y, h) in new[] { (15, 9, 5), (20, 6, 7), (26, 9, 5), (18, 14, 4), (24, 14, 4) })
            {
                for (int j = 0; j < h; j++)
                {
                    int half = (h - j) / 3;
                    for (int dx = -half; dx <= half; dx++) image[x + dx, y - j + h / 2] = dx < 0 ? crystal.Light : dx > 0 ? crystal.Shade : crystal.Main;
                }
                image[x, y - h / 2] = Rgba.White;
            }

            foreach (int x in new[] { 17, 19, 23, 25 }) image[x, 24] = eye;
            image[20, 23] = eye;
            image[22, 23] = eye;
            image[19, 29] = Rgba.Hex("#E8E0D0");
            image[23, 29] = Rgba.Hex("#E8E0D0");
            image[19, 30] = Rgba.Hex("#E8E0D0");
            image[23, 30] = Rgba.Hex("#E8E0D0");
            return image.Outlined(body.Outline);
        }

        // A filled oval lit from the top left.
        static void Ellipse(PixelImage image, int cx, int cy, int rx, int ry, Ramp ramp)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
            {
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    float u = (x - cx) / (float)rx;
                    float v = (y - cy) / (float)ry;
                    float d = u * u + v * v;
                    if (d > 1) continue;
                    float light = -u * 0.4f - v * 0.6f;
                    image[x, y] = d > 0.8f ? ramp.Dark : light > 0.35f ? ramp.Light : light < -0.3f ? ramp.Shade : ramp.Main;
                }
            }
        }

        // A line two pixels thick, lit along its top edge.
        static void Thick(PixelImage image, int x0, int y0, int x1, int y1, Rgba color, Rgba light)
        {
            int steps = System.Math.Max(System.Math.Abs(x1 - x0), System.Math.Abs(y1 - y0));
            for (int i = 0; i <= steps; i++)
            {
                int x = x0 + (x1 - x0) * i / System.Math.Max(1, steps);
                int y = y0 + (y1 - y0) * i / System.Math.Max(1, steps);
                image[x, y] = light;
                image[x, y + 1] = color;
            }
        }

        // A stone block lit from the top left, with its corners knocked off and a few dark specks.
        static void Block(PixelImage image, int x, int y, int width, int height, Ramp ramp, int salt)
        {
            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    bool corner = (i == 0 || i == width - 1) && (j == 0 || j == height - 1);
                    if (corner) continue;
                    var color = ramp.Main;
                    if (j == 0 || (j == 1 && (i == 0 || i == width - 1))) color = ramp.Light;
                    else if (j == height - 1) color = ramp.Dark;
                    else if (i == width - 1 || j == height - 2) color = ramp.Shade;
                    else if (i == 0) color = ramp.Main.Mix(ramp.Light, 0.5f);
                    else if (PixelNoise.Hash(i, j, salt) % 11 == 0) color = ramp.Shade;
                    image[x + i, y + j] = color;
                }
            }
        }

        static void Line(PixelImage image, int x, int y, (int X, int Y)[] steps, Rgba color)
        {
            image[x, y] = color;
            foreach (var (dx, dy) in steps)
            {
                x += dx;
                y += dy;
                image[x, y] = color;
            }
        }

        // ---- Shadows ----

        // A soft dark oval under something standing on the floor, the given number of pixels across.
        public static PixelImage Shadow(int width)
        {
            int height = System.Math.Max(3, width / 3);
            var image = new PixelImage(width, height);
            float rx = width / 2f;
            float ry = height / 2f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f - rx) / rx;
                    float v = (y + 0.5f - ry) / ry;
                    float d = u * u + v * v;
                    if (d <= 1) image[x, y] = new Rgba(0, 0, 0, d < 0.45f ? 110 : 70);
                }
            }
            return image;
        }
    }
}
