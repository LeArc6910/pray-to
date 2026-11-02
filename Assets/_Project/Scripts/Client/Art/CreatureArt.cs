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

    // What a player is doing, which decides the frame drawn (WorldView picks it). The frames of each:
    // Stand: 0 still, 1 breathing in (a pixel taller), 2 blinking. Walk: the WalkFrames of a step cycle.
    // Strike: 0 the weapon raised, 1 brought through. Cast: arms raised. Hurt: flinching, eyes shut.
    enum Pose
    {
        Stand,
        Walk,
        Strike,
        Cast,
        Hurt,
    }

    // Players, monsters and the boss, at the tiles' 16 pixels a tile. Each comes in a few frames: players walk in
    // three directions, monsters bob or squash on the spot. Monsters are drawn in their sheet colour
    // (monsters.csv), so a recoloured monster needs no change here, and a monster with no drawing of its own is a
    // slime in its colour.
    static class CreatureArt
    {
        public const int WalkFrames = 4;
        public const int IdleFrames = 2;
        public const int StandFrames = 3;
        public const int Breath = 1;
        public const int Blink = 2;
        public const int StrikeFrames = 2;

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

        // The bodies without hands: Hands puts them where the pose has them.
        static readonly string[] BodyDown =
        {
            ".tTTUUTTt.",
            "tTTTUUTTTt",
            "tTTTTTTTTt",
            ".bbbBBbbb.",
            ".tTTTTTTt.",
        };

        static readonly string[] BodyUp =
        {
            ".tTTTTTTt.",
            "tTTTTTTTTt",
            "tTTTTTTTTt",
            ".bbbbbbbb.",
            ".tTTTTTTt.",
        };

        static readonly string[] BodySide =
        {
            "..tTTTTt..",
            "..tTTUTt..",
            "..tTTTTt..",
            "..bbbBbb..",
            "..tTTTTt..",
        };

        // The head's row with the upper half of the eyes; a blink or a flinch leaves only the lower half.
        const int EyeRow = 4;

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

        // Feet planted apart for a strike, and drawn together in a flinch (facing down or up; side on, a strike
        // takes a stride and a flinch stands).
        static readonly string[] LegsWide = { ".pP....Pp.", ".oo....oo." };
        static readonly string[] LegsTogether = { "...pPPp...", "...oooo..." };

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

        // A player in a tunic of the given colour (blue for you, other colours for the others), in a pose and its
        // frame (Pose).
        public static PixelImage Player(Look look, int frame, Rgba tunic, Pose pose = Pose.Walk)
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
            var legs = Legs(look, pose, frame);
            // An empty row on top, so a step can lift the head and body a pixel without clipping the hair.
            var rows = new List<char[]> { new string('.', head[0].Length).ToCharArray() };
            foreach (string row in head) rows.Add(row.ToCharArray());
            int bodyTop = rows.Count;
            foreach (string row in body) rows.Add(row.ToCharArray());
            foreach (string row in legs) rows.Add(row.ToCharArray());
            if (pose == Pose.Hurt || pose == Pose.Stand && frame == Blink)
            {
                var eyes = rows[1 + EyeRow];
                for (int x = 0; x < eyes.Length; x++) if (eyes[x] == 'e') eyes[x] = 's';
            }
            foreach (var (x, y, c) in Hands(look, pose, frame)) rows[bodyTop + y][x] = c;

            var grid = new string[rows.Count];
            for (int y = 0; y < rows.Count; y++) grid[y] = new string(rows[y]);
            var image = PixelImage.FromRows(colors, grid);
            // A step's bob, and breathing in: the head and body a pixel up over the same legs.
            if (pose == Pose.Walk && frame % 2 == 1 || pose == Pose.Stand && frame == Breath) image = Lift(image, bodyTop + body.Length);
            return image.Outlined(Rgba.Hex("#140E10"));
        }

        static string[] Legs(Look look, Pose pose, int frame)
        {
            bool side = look == Look.Side;
            return pose switch
            {
                Pose.Walk => (side ? LegsSide : LegsFront)[frame % WalkFrames],
                Pose.Strike => side ? LegsSide[1] : LegsWide,
                Pose.Hurt => side ? LegsSide[0] : LegsTogether,
                _ => (side ? LegsSide : LegsFront)[0],
            };
        }

        // The hands, and the sleeves that move with them, in rows from the top of the body (−1 is beside the chin;
        // '.' clears a sleeve the arm has left). At the sides they swing against the legs as the player walks; the
        // weapon hand goes up and then through a strike; both go up for a cast; a flinch draws them in. Facing down
        // the weapon is on the right of the picture and facing up on the left, as WorldView.ShowWeapon holds it;
        // side on, only the near hand shows.
        static IEnumerable<(int X, int Y, char C)> Hands(Look look, Pose pose, int frame)
        {
            if (look == Look.Side)
            {
                switch (pose)
                {
                    case Pose.Walk:
                        yield return (frame == 1 ? 7 : frame == 3 ? 2 : 5, 2, 's');
                        break;
                    case Pose.Strike when frame == 0:
                        yield return (3, 0, 's');
                        break;
                    case Pose.Strike:
                        yield return (8, 2, 's');
                        break;
                    case Pose.Cast:
                        yield return (8, 1, 't');
                        yield return (8, 0, 's');
                        break;
                    case Pose.Hurt:
                        yield return (3, 1, 's');
                        break;
                    default:
                        yield return (5, 2, 's');
                        break;
                }
                yield break;
            }

            int weapon = look == Look.Down ? 9 : 0;
            int other = 9 - weapon;
            switch (pose)
            {
                case Pose.Walk when frame % 2 == 1:
                    // The hand on the side of the lifted foot swings back (up, shorter), the other forward (down).
                    int back = frame == 1 ? 9 : 0;
                    yield return (back, 1, 's');
                    yield return (back, 2, '.');
                    yield return (9 - back, 3, 's');
                    break;
                case Pose.Strike:
                    yield return (other, 2, 's');
                    if (frame == 0)
                    {
                        yield return (weapon, -1, 's');
                        yield return (weapon, 0, 't');
                        yield return (weapon, 2, '.');
                    }
                    else
                    {
                        yield return (weapon, 3, 's');
                    }
                    break;
                case Pose.Cast:
                    foreach (int x in new[] { 0, 9 })
                    {
                        yield return (x, -1, 's');
                        yield return (x, 0, 't');
                        yield return (x, 2, '.');
                    }
                    break;
                case Pose.Hurt when look == Look.Down:
                    yield return (0, 2, '.');
                    yield return (9, 2, '.');
                    yield return (2, 1, 's');
                    yield return (7, 1, 's');
                    break;
                case Pose.Hurt:
                    yield return (0, 1, 's');
                    yield return (0, 2, '.');
                    yield return (9, 1, 's');
                    yield return (9, 2, '.');
                    break;
                default:
                    yield return (0, 2, 's');
                    yield return (9, 2, 's');
                    break;
            }
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

        // An ice slime: a cube of ice with a cluster of frost shards on top. It squats lower and wider in the second frame.
        static readonly string[][] IceCube =
        {
            new[]
            {
                "....w.........",
                "...LwL.w......",
                ".LLLLLLLLLLLm.",
                ".LwwMMMMMMMMm.",
                ".LwMMMMMMMMMm.",
                ".LMMeMMMMeMMm.",
                ".LMMeMMMMeMMm.",
                ".LMMMMMMMMMmm.",
                ".mMMMMMMMMmmd.",
                ".mmmmmmmmmmmd.",
                "..dddddddddd..",
            },
            new[]
            {
                "..............",
                "....w.........",
                "...LwL.w......",
                "LLLLLLLLLLLLLm",
                "LwwMMMMMMMMMMm",
                "LMMeMMMMMMeMMm",
                "LMMeMMMMMMeMmm",
                "LMMMMMMMMMMMmm",
                "mMMMMMMMMMMmmd",
                "mmmmmmmmmmmmmd",
                ".dddddddddddd.",
            },
        };

        // A magma slime: a crust of cooled rock on top, glowing cracks, and a bubble that bursts.
        static readonly string[][] MagmaBlob =
        {
            new[]
            {
                "..............",
                "....kkkkkk....",
                "..kkkykkkkk...",
                ".kkkMyyMkkkm..",
                ".MMyMMMMyMMMm.",
                "MMMMeMMMMeMMMm",
                "MMMMeMMMMeMMmm",
                "MMMyMMMMMMMmmm",
                "mMMMMMMMyMmmmd",
                ".mmmymmmmmmmd.",
                "..dddyddddddd.",
            },
            new[]
            {
                ".....y.y......",
                "....kkykkk....",
                "..kkkkkkkkk...",
                ".kkkMyyMkkkm..",
                "LMMyMMMMMyMMMm",
                "MMMMeMMMMMeMMm",
                "MMMMeMMMMMeMmm",
                "MMMyMMMMMMMMmm",
                "mMMMMMMMMyMmmd",
                "mmmmymmmmmmmmd",
                ".ddddyddddddd.",
            },
        };

        // A spore shooter: a cap bristling with thorns over one big eye. The thorns flare and spores puff as it fires.
        static readonly string[][] ThornCap =
        {
            new[]
            {
                "............",
                "..L..L..L...",
                "..LL.LL.LL..",
                ".LMMMMMMMMm.",
                "LMwwMMMMMMMm",
                "LMwMMdMMMdMm",
                "MMMMMMMMMMmm",
                "mMdMMMMdMmmd",
                ".dmmmmmmmmd.",
                "...cccccC...",
                "...cweccC...",
                "...ceeccC...",
                "..CCCCCCCC..",
            },
            new[]
            {
                ".w...w..w...",
                ".L..L..L..L.",
                "LL.LL.LL.LL.",
                ".LMMMMMMMMm.",
                "LMwwMMMMMMMm",
                "LMwMMdMMMdMm",
                "MMMMMMMMMMmm",
                "mMdMMMMdMmmd",
                ".dmmmmmmmmd.",
                "...cccccC...",
                "...cweccC...",
                "...ceeccC...",
                "..CCCCCCCC..",
            },
        };

        // A myconid: a mushroom soldier on two legs with a spear. Its legs step between the frames.
        static readonly string[][] MushroomSoldier =
        {
            new[]
            {
                "...........h.",
                "...LLLLLL..h.",
                "..LMwwMMML.K.",
                ".LMwwMMLLMmK.",
                ".MMMMMMLLMmK.",
                ".mLLMMMMMmmK.",
                "..dmmmmmmmdK.",
                "...cecceC..K.",
                "...ccccccccK.",
                "...cccccC..K.",
                "...cccccC..K.",
                "...cC..cC..K.",
                "..CC....CC.K.",
            },
            new[]
            {
                "...........h.",
                "...LLLLLL..h.",
                "..LMwwMMML.K.",
                ".LMwwMMLLMmK.",
                ".MMMMMMLLMmK.",
                ".mLLMMMMMmmK.",
                "..dmmmmmmmdK.",
                "...cecceC..K.",
                "...ccccccccK.",
                "...cccccC..K.",
                "...cccccC..K.",
                "....cCcC...K.",
                "...CC.CC...K.",
            },
        };

        // A cave beetle: a round shell, a horned head and six legs that step between the frames.
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

        // An abyss stalker: a low shell on many legs, claws out front and a glowing sting curled over its back.
        static readonly string[][] Scorpion =
        {
            new[]
            {
                "...MMM..........",
                "..M...r.........",
                ".mM.............",
                ".mM.............",
                "..mM.LLLLLL.....",
                "...MLMwwMMMLm...",
                "...MMMMMMMMMMrKh",
                "...mMMMMMMMMMmKK",
                "....mmmmmmmmmKKh",
                "....K.K.K.K.K...",
                "...K.K.K.K.K....",
            },
            new[]
            {
                "..MMM...........",
                ".M...r..........",
                ".mM.............",
                ".mM.............",
                "..mM.LLLLLL.....",
                "...MLMwwMMMLm...",
                "...MMMMMMMMMMrKh",
                "...mMMMMMMMMMmKK",
                "....mmmmmmmmmKKh",
                ".....K.K.K.K.K..",
                "....K.K.K.K.K...",
            },
        };

        // A crystal bat: wings edged and tipped with crystal, a crystal horn. Wings up, then down.
        static readonly string[][] CrystalBat =
        {
            new[]
            {
                "w......w......w",
                "Lm...L.w.L...mL",
                ".Lw..LMMML..wL.",
                "..Lm.MeMeM.mL..",
                "...LmMMMMMmL...",
                ".....MwMMm.....",
                "......mmm......",
                ".......d.......",
            },
            new[]
            {
                ".......w.......",
                ".....L.w.L.....",
                ".....LMMML.....",
                ".....MeMeM.....",
                "..LwmMMMMMmwL..",
                ".Lw..MwMMm..wL.",
                "Lw....mmm....wL",
                "w......d......w",
            },
        };

        // A glow bat: glowing feelers and a glowing belly. Wings up, then down.
        static readonly string[][] GlowBat =
        {
            new[]
            {
                "....y.....y....",
                "Mm...m...m...mM",
                ".Mm..LMMML..mM.",
                "..Mm.MeMeM.mM..",
                "...MmMyyMMmM...",
                ".....MyyMm.....",
                "......mmm......",
                ".......d.......",
            },
            new[]
            {
                "....y.....y....",
                ".....m...m.....",
                ".....LMMML.....",
                ".....MeMeM.....",
                "..MmmMyyMMmmM..",
                ".Mm..MyyMm..mM.",
                "Mm....mmm....mM",
                "M......d......M",
            },
        };

        // A fire bat: horns, glowing eyes, flames at the wing tips and the tail. Wings up, then down.
        static readonly string[][] FireBat =
        {
            new[]
            {
                "y.............y",
                "My...d...d...yM",
                ".Mm..LMMML..mM.",
                "..Mm.MyMyM.mM..",
                "...MmMMMMMmM...",
                ".....MwMMm.....",
                "......mmm......",
                ".......y.......",
            },
            new[]
            {
                "...............",
                ".....d...d.....",
                ".....LMMML.....",
                ".....MyMyM.....",
                "..MmmMMMMMmmM..",
                ".Mm..MwMMm..mM.",
                "Mm....mmm....mM",
                "y......y......y",
            },
        };

        // A crystal wisp: a floating gem with a face, sparkles turning about it.
        static readonly string[][] Gem =
        {
            new[]
            {
                ".....LL.....",
                "....LwwL....",
                "...LwwMML...",
                "..LwMMMMMm..",
                ".LwMMMMMMMm.",
                ".MMeMMMMeMm.",
                "..MeMMMMeM..",
                "...mMMMMm...",
                "....mMMm....",
                ".....mm.....",
                "w.........w.",
            },
            new[]
            {
                "w....LL....w",
                "....LwwL....",
                "...LwwMML...",
                "..LwMMMMMm..",
                ".LwMMMMMMMm.",
                ".MMeMMMMeMm.",
                "..MeMMMMeM..",
                "...mMMMMm...",
                "....mMMm....",
                ".....mm.....",
                "............",
            },
        };

        // A frost wisp: a cold ghost with a frost crown and icicles hanging from its hem.
        static readonly string[][] FrostWisp =
        {
            new[]
            {
                "....w..w....",
                ".....ww.....",
                "...LLLLLL...",
                "..LwwMMMMm..",
                ".LwMMMMMMMm.",
                ".MMeMMMMeMm.",
                ".MMeMMMMeMm.",
                ".MMMMMMMMMm.",
                ".mMmMMmMMmm.",
                "..L.L..L.L..",
                "..L....L....",
            },
            new[]
            {
                "...w....w...",
                ".....ww.....",
                "...LLLLLL...",
                "..LwwMMMMm..",
                ".LwMMMMMMMm.",
                ".MMeMMMMeMm.",
                ".MMeMMMMeMm.",
                ".MMMMMMMMMm.",
                ".mMmMMmMMmm.",
                "..L.L..L.L..",
                "....L....L..",
            },
        };

        // A void wisp: one great eye in a dark orb, its tendrils swaying.
        static readonly string[][] EyeWisp =
        {
            new[]
            {
                "....mmmm....",
                "..mmMMMMmm..",
                ".mMMMMMMMMm.",
                "mMMLLLLLLMMm",
                "mMLLweeLLLMm",
                "mMLLeeeLLLMm",
                "mMMLLLLLLMMm",
                ".mMMMMMMMMm.",
                "..mmMmmMmm..",
                "..d.m..m.d..",
                ".d..d..d..d.",
            },
            new[]
            {
                "....mmmm....",
                "..mmMMMMmm..",
                ".mMMMMMMMMm.",
                "mMMLLLLLLMMm",
                "mMLLweeLLLMm",
                "mMLLeeeLLLMm",
                "mMMLLLLLLMMm",
                ".mMMMMMMMMm.",
                "..mmMmmMmm..",
                "...dm..md...",
                "..d..dd..d..",
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
            ["cave_beetle"] = new Dictionary<char, Rgba>
            {
                ['K'] = Rgba.Hex("#58422E"),
                ['r'] = Rgba.Hex("#F05A3A"),
                ['h'] = Rgba.Hex("#EADCBC"),
            },
            // A stalker of the abyss: legs and claws of night, a cold glowing eye and sting.
            ["abyss_stalker"] = new Dictionary<char, Rgba>
            {
                ['K'] = Rgba.Hex("#22183A"),
                ['r'] = Rgba.Hex("#BFF4FF"),
                ['h'] = Rgba.Hex("#C8B4FF"),
            },
            // The spear: a wooden shaft and an iron head.
            ["myconid"] = new Dictionary<char, Rgba>
            {
                ['c'] = Rgba.Hex("#F0E2D0"),
                ['C'] = Rgba.Hex("#C8B49C"),
                ['K'] = Rgba.Hex("#7A5230"),
                ['h'] = Rgba.Hex("#D8DCE8"),
            },
            // Cooled crust and the glow of the cracks.
            ["magma_slime"] = new Dictionary<char, Rgba>
            {
                ['k'] = Rgba.Hex("#3A1C16"),
                ['y'] = Rgba.Hex("#FFD45A"),
            },
            ["glow_bat"] = new Dictionary<char, Rgba>
            {
                ['y'] = Rgba.Hex("#F4FFB8"),
            },
            ["fire_bat"] = new Dictionary<char, Rgba>
            {
                ['y'] = Rgba.Hex("#FFE070"),
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
                "ice_slime" => IceCube,
                "magma_slime" => MagmaBlob,
                "spore_shooter" => ThornCap,
                "myconid" => MushroomSoldier,
                "cave_beetle" => Beetle,
                "abyss_stalker" => Scorpion,
                "crystal_bat" => CrystalBat,
                "glow_bat" => GlowBat,
                "fire_bat" => FireBat,
                "crystal_wisp" => Gem,
                "frost_wisp" => FrostWisp,
                "void_wisp" => EyeWisp,
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
            "void_lord" => VoidLord(frame),
            _ => Warden(color, frame),
        };

        // The void lord: a hooded shape in a long cloak that floats over the ground, two cold eyes in the dark of the
        // hood, an orb of the void held before it and stars turning round its head. The second frame rises a pixel
        // and turns the stars.
        static PixelImage VoidLord(int frame)
        {
            const int width = 40;
            const int height = 40;
            var image = new PixelImage(width, height);
            var cloak = Ramp.Of("#3A2C5C");
            var trim = Ramp.Of("#9A7CFF");
            var orb = Ramp.Of("#C8A8FF");
            var eye = Rgba.Hex("#BFF4FF");
            int bob = frame % 2 == 1 ? -1 : 0;
            const int cx = 20;

            // The cloak: wider towards the hem, frayed at the bottom, with a lit edge down each side.
            for (int y = 10; y < 36; y++)
            {
                int half = 5 + (y - 10) * 9 / 26;
                bool frayed = y >= 33;
                for (int x = cx - half; x <= cx + half; x++)
                {
                    if (frayed && (x + y) % 3 == 0) continue;
                    var color = x <= cx - half + 1 ? cloak.Light : x >= cx + half - 1 ? cloak.Dark : (x + y) % 7 == 0 ? cloak.Shade : cloak.Main;
                    image[x, y + bob] = color;
                }
                if (!frayed)
                {
                    image[cx - half, y + bob] = trim.Main;
                    image[cx + half, y + bob] = trim.Shade;
                }
            }
            // The hood, dark inside, and the eyes.
            for (int y = 3; y < 13; y++)
            {
                int half = y < 6 ? y - 1 : 6;
                for (int x = cx - half; x <= cx + half; x++) image[x, y + bob] = cloak.Main;
            }
            for (int y = 6; y < 12; y++)
            {
                for (int x = cx - 3; x <= cx + 3; x++) image[x, y + bob] = Rgba.Hex("#08060C");
            }
            image[cx - 2, 8 + bob] = eye;
            image[cx + 2, 8 + bob] = eye;
            image[cx - 2, 9 + bob] = trim.Main;
            image[cx + 2, 9 + bob] = trim.Main;
            // The orb in front, with sleeves reaching to it.
            for (int dy = -3; dy <= 3; dy++)
            {
                for (int dx = -3; dx <= 3; dx++)
                {
                    int d = dx * dx + dy * dy;
                    if (d > 10) continue;
                    image[cx + dx, 21 + dy + bob] = d <= 1 ? Rgba.White : d <= 4 ? orb.Light : d <= 7 ? orb.Main : orb.Shade;
                }
            }
            for (int i = 0; i < 5; i++)
            {
                image[cx - 9 + i, 18 + i / 2 + bob] = cloak.Light;
                image[cx + 9 - i, 18 + i / 2 + bob] = cloak.Shade;
            }
            // Stars turning round the head.
            for (int i = 0; i < 6; i++)
            {
                double angle = (i + frame * 0.5) / 6 * System.Math.PI * 2;
                int sx = cx + (int)System.Math.Round(System.Math.Cos(angle) * 13);
                int sy = 8 + (int)System.Math.Round(System.Math.Sin(angle) * 4) + bob;
                if (!image.Inside(sx, sy)) continue;
                image[sx, sy] = i % 2 == 0 ? Rgba.White : trim.Light;
            }
            return image.Outlined(Rgba.Hex("#08060C"));
        }

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
