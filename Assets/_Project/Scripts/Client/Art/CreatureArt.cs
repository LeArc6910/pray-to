using System.Collections.Generic;

namespace Game.Client
{
    // Which way a player sprite looks, of eight (2026-11-09): down, up, side, and the two three-quarter views between,
    // down-side (toward the viewer and right) and up-side (away and right). The left ones are these flipped.
    enum Look
    {
        Down,
        Up,
        Side,
        DownSide,
        UpSide,
    }

    // What a player is doing, which decides the frame drawn (WorldView picks it). The frames of each:
    // Stand: 0 still, 1 breathing in (a pixel taller), 2 blinking. Walk: the WalkFrames of a step cycle.
    // Strike: 0 the weapon raised, 1 brought through. Cast: arms raised. Hurt: flinching, eyes shut.
    // Shoot (a bow): 0 the string drawn back, 1 let go. Zap (a staff): 0 raised, 1 pointed. Drink: 0 the bottle lifted,
    // 1 tipped back. Thrust (a skill sent forward, a dash): both hands out ahead. Spread (a skill all round): arms
    // flung wide.
    enum Pose
    {
        Stand,
        Walk,
        Strike,
        Cast,
        Hurt,
        Shoot,
        Zap,
        Drink,
        Thrust,
        Spread,
    }

    // Which way a boss is drawn (2026-11-09): toward the viewer, three-quarters, side on, three-quarters away, away.
    // The left ones are flipped.
    enum BossView
    {
        Front,
        FrontSide,
        Side,
        BackSide,
        Back,
    }

    // Which way a monster is seen (2026-11-09): from the side (or the front: most face the viewer), or from behind,
    // going away, its eyes hidden. WorldView flips the side for the left.
    enum MonsterView
    {
        Side,
        Back,
    }

    // What a monster is doing (2026-11-09): crouched and red-eyed winding up an attack, stretched out striking,
    // squashed with its eyes shut when hit.
    enum MonsterPose
    {
        Idle,
        Windup,
        Strike,
        Hurt,
    }

    // What a boss is doing (WorldView picks it from its state and its last attack).
    enum BossPose
    {
        Idle,
        Windup,
        Strike,
        Charge,
        Roar,
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

        // Three-quarter views: the face turned toward the viewer and right, both eyes showing; and turned away and
        // right, the hair and a sliver of cheek.
        static readonly string[] HeadDownSide =
        {
            "..hhhhhh..",
            ".hHHHHHhh.",
            "hHHhhhhhh.",
            "hhhssssss.",
            "hhhsesses.",
            ".hhsesseS.",
            "..hssssS..",
        };

        static readonly string[] HeadUpSide =
        {
            "..hhhhhh..",
            ".hHHHHHhh.",
            "hHHHhhHhh.",
            "hhHhhhhhs.",
            "hhhhhhhhs.",
            ".hhhhhhhS.",
            "..SsssS...",
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

        static readonly string[] BodyDownSide =
        {
            "..tTTUUTt.",
            ".tTTTUUTTt",
            ".tTTTTTTTt",
            "..bbbBBbb.",
            "..tTTTTTt.",
        };

        static readonly string[] BodyUpSide =
        {
            "..tTTTTTt.",
            ".tTTTTTTTt",
            ".tTTTTTTTt",
            "..bbbbbbb.",
            "..tTTTTTt.",
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

        // Three-quarter on, the legs sit a little to the near side, closer together.
        static readonly string[][] LegsSlant =
        {
            new[] { "...pP.Pp..", "...oo.oo.." },
            new[] { "...pP.oo..", "...oo....." },
            new[] { "...pP.Pp..", "...oo.oo.." },
            new[] { "...oo.Pp..", "......oo.." },
        };

        // Feet planted apart for a strike, and drawn together in a flinch (facing down or up; side on, a strike
        // takes a stride and a flinch stands).
        static readonly string[] LegsWide = { ".pP....Pp.", ".oo....oo." };
        static readonly string[] LegsSlantWide = { "..pP...Pp.", "..oo...oo." };
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
            var head = look switch { Look.Down => HeadDown, Look.Up => HeadUp, Look.DownSide => HeadDownSide, Look.UpSide => HeadUpSide, _ => HeadSide };
            var body = look switch { Look.Down => BodyDown, Look.Up => BodyUp, Look.DownSide => BodyDownSide, Look.UpSide => BodyUpSide, _ => BodySide };
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
            // A step's bob, breathing in, tipping a bottle back and a cast's stretch: the head and body a pixel up over
            // the same legs.
            if (pose == Pose.Walk && frame % 2 == 1 || pose == Pose.Stand && frame == Breath || pose == Pose.Drink && frame == 1
                || pose == Pose.Cast || pose == Pose.Zap && frame == 0)
                image = Lift(image, bodyTop + body.Length);
            return image.Outlined(Rgba.Hex("#140E10"));
        }

        static string[] Legs(Look look, Pose pose, int frame)
        {
            bool side = look == Look.Side;
            bool slant = look == Look.DownSide || look == Look.UpSide;
            var walk = side ? LegsSide : slant ? LegsSlant : LegsFront;
            return pose switch
            {
                Pose.Walk => walk[frame % WalkFrames],
                // A stance for striking, shooting and casting out or round: a stride side on, feet apart otherwise.
                Pose.Strike or Pose.Shoot or Pose.Thrust or Pose.Spread => side ? LegsSide[1] : slant ? LegsSlantWide : LegsWide,
                Pose.Hurt => side ? LegsSide[0] : LegsTogether,
                _ => walk[0],
            };
        }

        // The hands, and the sleeves that move with them, in rows from the top of the body (−1 is beside the chin;
        // '.' clears a sleeve the arm has left). At the sides they swing against the legs as the player walks; the
        // weapon hand goes up and then through a strike; both go up for a cast; a flinch draws them in. Facing down
        // the weapon is on the right of the picture and facing up on the left, as WorldView.ShowWeapon holds it;
        // side on, only the near hand shows.
        static IEnumerable<(int X, int Y, char C)> Hands(Look look, Pose pose, int frame)
        {
            if (look == Look.DownSide || look == Look.UpSide) return SlantHands(look, pose, frame);
            return look == Look.Side ? SideHands(pose, frame) : FrontHands(look, pose, frame);
        }

        // Side on: only the near hand shows (and the far one when flung back or out).
        static IEnumerable<(int X, int Y, char C)> SideHands(Pose pose, int frame)
        {
            {
                switch (pose)
                {
                    case Pose.Shoot when frame == 0:
                        // Bow arm out ahead, the string hand drawn back to the chin.
                        yield return (9, 1, 's');
                        yield return (8, 1, 't');
                        yield return (5, 0, 's');
                        break;
                    case Pose.Shoot:
                        yield return (9, 1, 's');
                        yield return (8, 1, 't');
                        yield return (1, 1, 's');
                        break;
                    case Pose.Zap when frame == 0:
                        yield return (7, -1, 's');
                        yield return (7, 0, 't');
                        break;
                    case Pose.Zap:
                        yield return (9, 1, 's');
                        yield return (8, 1, 't');
                        break;
                    case Pose.Drink:
                        yield return (8, frame == 0 ? -1 : -2, 's');
                        yield return (7, 0, 't');
                        break;
                    case Pose.Thrust:
                        yield return (9, 1, 's');
                        yield return (8, 1, 't');
                        yield return (9, 2, 's');
                        break;
                    case Pose.Spread:
                        yield return (9, 1, 's');
                        yield return (8, 1, 't');
                        yield return (0, 1, 's');
                        yield return (1, 1, 't');
                        break;
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
            }
        }

        // Three-quarter on: the near (weapon) hand on the right edge, the far one at the left, a shade darker.
        static IEnumerable<(int X, int Y, char C)> SlantHands(Look look, Pose pose, int frame)
        {
            bool toward = look == Look.DownSide;
            switch (pose)
            {
                case Pose.Walk when frame % 2 == 1:
                    bool nearBack = frame == 1;
                    yield return (9, nearBack ? 1 : 3, 's');
                    if (nearBack) yield return (9, 2, '.');
                    yield return (1, nearBack ? 3 : 1, 'S');
                    if (!nearBack) yield return (1, 2, '.');
                    break;
                case Pose.Strike:
                    yield return (1, 2, 'S');
                    if (frame == 0)
                    {
                        yield return (9, -1, 's');
                        yield return (9, 0, 't');
                        yield return (9, 2, '.');
                    }
                    else yield return (9, 3, 's');
                    break;
                case Pose.Cast:
                    foreach (int x in new[] { 1, 9 })
                    {
                        yield return (x, -1, x == 1 ? 'S' : 's');
                        yield return (x, 0, 't');
                        yield return (x, 2, '.');
                    }
                    break;
                case Pose.Hurt:
                    yield return (1, 1, 'S');
                    yield return (1, 2, '.');
                    yield return (9, 1, 's');
                    yield return (9, 2, '.');
                    break;
                case Pose.Shoot:
                    // Facing toward the viewer the bow is held out low in front; facing away, out to the side.
                    yield return (9, toward ? 3 : 1, 's');
                    yield return (9, 2, toward ? 't' : '.');
                    yield return (1, 2, '.');
                    yield return (frame == 0 ? 6 : 0, frame == 0 ? (toward ? 2 : 1) : 1, frame == 0 ? 's' : 'S');
                    break;
                case Pose.Zap:
                    yield return (1, 2, 'S');
                    if (frame == 0)
                    {
                        yield return (9, -1, 's');
                        yield return (9, 0, 't');
                        yield return (9, 2, '.');
                    }
                    else yield return (9, toward ? 3 : 1, 's');
                    break;
                case Pose.Drink:
                    // The near hand up at the mouth, a shade darker so it shows against the face, the arm across.
                    yield return (1, 2, 'S');
                    yield return (9, 2, '.');
                    yield return (toward ? 8 : 9, frame == 0 ? -1 : -2, toward ? 'S' : 's');
                    yield return (8, 1, 't');
                    yield return (toward ? 8 : 9, 0, 't');
                    break;
                case Pose.Thrust:
                    yield return (9, toward ? 3 : 1, 's');
                    yield return (8, toward ? 3 : 1, 's');
                    yield return (9, 2, toward ? 't' : '.');
                    yield return (1, 2, '.');
                    break;
                case Pose.Spread:
                    yield return (9, 1, 's');
                    yield return (9, 2, '.');
                    yield return (0, 1, 'S');
                    yield return (1, 2, '.');
                    break;
                default:
                    yield return (9, 2, 's');
                    yield return (1, 2, 'S');
                    break;
            }
        }

        // Facing down or up: both hands at the sides.
        static IEnumerable<(int X, int Y, char C)> FrontHands(Look look, Pose pose, int frame)
        {
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
                case Pose.Shoot when look == Look.Down:
                    // Both hands in front aiming down; letting go, the string hand flies back out.
                    yield return (0, 2, '.');
                    yield return (9, 2, '.');
                    yield return (4, 3, 's');
                    yield return (frame == 0 ? 5 : 9, frame == 0 ? 3 : 1, 's');
                    break;
                case Pose.Shoot:
                case Pose.Thrust when look == Look.Up:
                    // Facing away, the elbows show out at the sides with the hands ahead.
                    yield return (0, 1, 's');
                    yield return (0, 2, '.');
                    yield return (9, frame == 1 && pose == Pose.Shoot ? 2 : 1, 's');
                    if (!(frame == 1 && pose == Pose.Shoot)) yield return (9, 2, '.');
                    break;
                case Pose.Zap when frame == 0:
                    yield return (other, 2, 's');
                    yield return (weapon, -1, 's');
                    yield return (weapon, 0, 't');
                    yield return (weapon, 2, '.');
                    break;
                case Pose.Zap:
                    yield return (other, 2, 's');
                    yield return (weapon, 2, '.');
                    yield return (look == Look.Down ? 8 : 1, look == Look.Down ? 3 : 1, 's');
                    break;
                case Pose.Drink:
                    yield return (other, 2, 's');
                    yield return (weapon, 2, '.');
                    if (look == Look.Down)
                    {
                        // The hand at the mouth a shade darker than the face, the arm drawn across the chest.
                        yield return (frame == 0 ? 6 : 5, -1, 'S');
                        yield return (7, 0, 't');
                        yield return (8, 1, 't');
                    }
                    else yield return (weapon, 0, 's');
                    break;
                case Pose.Thrust:
                    yield return (0, 2, '.');
                    yield return (9, 2, '.');
                    yield return (3, 3, 's');
                    yield return (6, 3, 's');
                    break;
                case Pose.Spread:
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

        // A demon soldier of the nest (2026-11-10): horns, a dark iron cuirass over crimson skin, a curved blade.
        static readonly string[][] DemonSoldier =
        {
            new[]
            {
                "..h.....h.....",
                "..hh...hh.....",
                "...hMMMh......",
                "...MLLMMm.....",
                "...MeMMem.....",
                "....MMMm......",
                "..kKKKKKKk...b",
                ".kKkKKKKKKk.bB",
                ".MkKKKKKKKMbB.",
                ".M.KKKKKK.MB..",
                "...KKdKKK.....",
                "...Km..mK.....",
                "...mm..mm.....",
                "..dd....dd....",
            },
            new[]
            {
                "..h.....h.....",
                "..hh...hh.....",
                "...hMMMh......",
                "...MLLMMm.....",
                "...MeMMem.....",
                "....MMMm......",
                "..kKKKKKKk...b",
                ".kKkKKKKKKk.bB",
                ".MkKKKKKKKMbB.",
                ".M.KKKKKK.MB..",
                "...KKdKKK.....",
                "....Km.mK.....",
                "....mmmm......",
                "...dd.dd......",
            },
        };

        // A demon caster: a hooded robe with horns through the hood, eyes in its shadow, a staff of dark wood crowned
        // with a flame that flickers.
        static readonly string[][] DemonCaster =
        {
            new[]
            {
                "...........F..",
                "..h....h..fFf.",
                "..hh..hh...f..",
                "...LLMMM...K..",
                "..LMMMMMm..K..",
                "..MdeddeM..K..",
                "..MddddddM.K..",
                "...MMMMMMmmK..",
                "..LMMMMMMMmK..",
                ".LMMMMMMMMmK..",
                ".LMMMMMMMMMm..",
                ".MMMMMMMMMMm..",
                "..mmmmmmmmm...",
                "..dd.....dd...",
            },
            new[]
            {
                "..........fF..",
                "..h....h...Ff.",
                "..hh..hh..ff..",
                "...LLMMM...K..",
                "..LMMMMMm..K..",
                "..MdeddeM..K..",
                "..MddddddM.K..",
                "...MMMMMMmmK..",
                "..LMMMMMMMmK..",
                ".LMMMMMMMMmK..",
                ".LMMMMMMMMMm..",
                ".MMMMMMMMMMm..",
                "..mmmmmmmmm...",
                "...dd...dd....",
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

        // A lake crab: a wide low shell on six legs, two big claws raised, tipped red. The claws open and close.
        static readonly string[][] Crab =
        {
            new[]
            {
                "..KK........KK..",
                ".KrrK......KrrK.",
                ".KrK........KrK.",
                "..K.LLLLLLLL.K..",
                "...LMMwMMwMMML..",
                "..LMMMMMMMMMMMm.",
                "..mMMMMMMMMMMmm.",
                "...mmmmmmmmmmm..",
                "..K.K.K..K.K.K..",
                ".K.K.K....K.K.K.",
            },
            new[]
            {
                ".KK..........KK.",
                "KrrK........KrrK",
                ".KrK........KrK.",
                "..K.LLLLLLLL.K..",
                "...LMMwMMwMMML..",
                "..LMMMMMMMMMMMm.",
                "..mMMMMMMMMMMmm.",
                "...mmmmmmmmmmm..",
                "...K.K.K.K.K.K..",
                "..K.K.K..K.K.K..",
            },
        };

        // A water wisp: a pale bell like a jellyfish, two dark eyes, trailing threads that sway.
        static readonly string[][] Jelly =
        {
            new[]
            {
                "....LLLLLL......",
                "..LLMMMMMMLL....",
                ".LMMwMMMMwMML...",
                ".LMMMMMMMMMML...",
                "..mMMMMMMMMm....",
                "...mmmmmmmm.....",
                "...h.h..h.h.....",
                "..h..h..h..h....",
                "...h..h..h..h...",
                "..h..h....h.....",
            },
            new[]
            {
                "....LLLLLL......",
                "..LLMMMMMMLL....",
                ".LMMwMMMMwMML...",
                ".LMMMMMMMMMML...",
                "..mMMMMMMMMm....",
                "...mmmmmmmm.....",
                "....h.h..h.h....",
                "...h..h..h..h...",
                "..h..h..h..h....",
                "...h....h..h....",
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

        // A spiderling of the venom spider's brood (2026-11-13): a small purple-black spider, its abdomen marked green,
        // its legs stepping in turn.
        static readonly string[][] Spiderling =
        {
            new[]
            {
                "................",
                ".....K..K..K....",
                "....K.KK.KK.K...",
                "..LLL.K..K.K.K..",
                ".LMgMM.LLLL..K..",
                ".MgggMMMMMMeM...",
                ".MMgMMmMMMMMMd..",
                "..mMMm.mmmmm.K..",
                "..K..K.K..K.K.K.",
                ".K..K...K..K...K",
            },
            new[]
            {
                "................",
                "....K..K..K.....",
                "...K.KK.KK.K....",
                "..LLLK..K.K.K...",
                ".LMgMM.LLLL.K...",
                ".MgggMMMMMMeM...",
                ".MMgMMmMMMMMMd..",
                "..mMMm.mmmmm..K.",
                ".K..K.K..K.K.K..",
                "K..K...K..K...K.",
            },
        };

        // A sand worm (2026-11-09): a banded body risen out of a drift of sand, its round mouth open to spit, then shut
        // and leaning the other way.
        static readonly string[][] SandWorm =
        {
            new[]
            {
                "......LLLL......",
                ".....LMMMMm.....",
                "....LMeMMeMm....",
                "....MrrrrrrMm...",
                "....mrrrrrrm....",
                ".....mMMMMm.....",
                "......dddd......",
                ".....LMMMMm.....",
                ".....MMMMMm.....",
                "......dddd......",
                "....LMMMMMMm....",
                "..ssssssssssss..",
            },
            new[]
            {
                ".....LLLL.......",
                "....LMMMMm......",
                "...LMeMMeMm.....",
                "...MMMrrMMm.....",
                "....mMMMMm......",
                "....mMMMMm......",
                ".....dddd.......",
                ".....LMMMMm.....",
                ".....MMMMMm.....",
                "......dddd......",
                "....LMMMMMMm....",
                "..ssssssssssss..",
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
            ["lake_crab"] = new Dictionary<char, Rgba>
            {
                ['K'] = Rgba.Hex("#1E3A44"),
                ['r'] = Rgba.Hex("#E86E5A"),
            },
            ["tide_wisp"] = new Dictionary<char, Rgba>
            {
                ['w'] = Rgba.Hex("#103038"),
                ['h'] = Rgba.Hex("#BFF4EC"),
            },
            ["cave_beetle"] = new Dictionary<char, Rgba>
            {
                ['K'] = Rgba.Hex("#58422E"),
                ['r'] = Rgba.Hex("#F05A3A"),
                ['h'] = Rgba.Hex("#EADCBC"),
            },
            // A stalker of the abyss: legs and claws of night, a cold glowing eye and sting.
            // The sand scorpion is the stalker's scorpion in sand, its claws dark, its sting amber.
            ["sand_scorpion"] = new Dictionary<char, Rgba>
            {
                ['K'] = Rgba.Hex("#5A3E1E"),
                ['r'] = Rgba.Hex("#F0B040"),
                ['h'] = Rgba.Hex("#F0D090"),
            },
            ["sand_worm"] = new Dictionary<char, Rgba>
            {
                ['r'] = Rgba.Hex("#C8404A"),
                ['s'] = Rgba.Hex("#9C7E52"),
            },
            ["abyss_stalker"] = new Dictionary<char, Rgba>
            {
                ['K'] = Rgba.Hex("#22183A"),
                ['r'] = Rgba.Hex("#BFF4FF"),
                ['h'] = Rgba.Hex("#C8B4FF"),
            },
            // The demons of the nest: bone horns, dark iron, a steel blade; the caster's staff and flame.
            ["demon_soldier"] = new Dictionary<char, Rgba>
            {
                ['h'] = Rgba.Hex("#E2DCCA"),
                ['K'] = Rgba.Hex("#3A3440"),
                ['k'] = Rgba.Hex("#6A6470"),
                ['b'] = Rgba.Hex("#E0E4EE"),
                ['B'] = Rgba.Hex("#9AA0B0"),
            },
            ["demon_caster"] = new Dictionary<char, Rgba>
            {
                ['h'] = Rgba.Hex("#E2DCCA"),
                ['K'] = Rgba.Hex("#4A3422"),
                ['f'] = Rgba.Hex("#FF9A3A"),
                ['F'] = Rgba.Hex("#FFE07A"),
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
            // The venom spider's young: dark legs, a green mark, a red eye.
            ["spiderling"] = new Dictionary<char, Rgba>
            {
                ['K'] = Rgba.Hex("#1E1428"),
                ['g'] = Rgba.Hex("#9FE35D"),
            },
        };

        // Monsters face right; the view flips them to face left. Seen from behind their eyes are hidden; a pose reshapes
        // them about their middle, standing on the same feet (Reshaped).
        public static PixelImage Monster(string id, Rgba color, int frame, MonsterView view = MonsterView.Side, MonsterPose pose = MonsterPose.Idle)
        {
            var ramp = Ramp.Of(color);
            var colors = RampColors(ramp);
            if (Extras.TryGetValue(id, out var extra))
            {
                foreach (var pair in extra) colors[pair.Key] = pair.Value;
            }
            if (view == MonsterView.Back) colors['e'] = ramp.Main;
            else if (pose == MonsterPose.Windup) colors['e'] = AngryEye;
            else if (pose == MonsterPose.Hurt) colors['e'] = ramp.Dark;
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
                "lake_crab" => Crab,
                "tide_wisp" => Jelly,
                "sand_scorpion" => Scorpion,
                "sand_worm" => SandWorm,
                "demon_soldier" => DemonSoldier,
                "demon_caster" => DemonCaster,
                "spiderling" => Spiderling,
                _ => Slime,
            };
            var image = PixelImage.FromRows(colors, frames[frame % frames.Length]);
            image = pose switch
            {
                // Crouched: two rows shorter and two wider (one each for the small ones). Striking: a row taller, a
                // column narrower. Hit: squashed by one.
                MonsterPose.Windup => image.Height >= 14 ? Reshaped(image, -2, 2) : Reshaped(image, -1, 1),
                MonsterPose.Strike => Reshaped(image, 1, -1),
                MonsterPose.Hurt => Reshaped(image, -1, 1),
                _ => image,
            };
            return image.Outlined(ramp.Outline);
        }

        static readonly Rgba AngryEye = Rgba.Hex("#FF4A3A");

        // The picture with rows added (or taken away, negative) through its middle and columns likewise, so the
        // outline stays crisp and it stands on the same feet.
        static PixelImage Reshaped(PixelImage image, int rows, int columns)
        {
            int width = System.Math.Max(1, image.Width + columns);
            int height = System.Math.Max(1, image.Height + rows);
            int midX = image.Width / 2;
            int midY = image.Height / 2;
            int Source(int at, int middle, int added) => added >= 0
                ? at < middle ? at : at < middle + added ? middle : at - added
                : at < middle ? at : at - added;
            var result = new PixelImage(width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++) result[x, y] = image[Source(x, midX, columns), Source(y, midY, rows)];
            }
            return result;
        }

        // ---- The boss ----

        // The bosses (2026-11-09: eight ways and moving with their attacks). Each is drawn from parts for the view
        // (BossView: front, front three-quarter, side, back three-quarter, back; WorldView flips them for the left)
        // and the pose (BossPose): idle breathing, winding up while a telegraph shows, the strike as it lands, a
        // charge's lean and a roar. The canvases have room round the body for raised or flung-out limbs.
        public static PixelImage Boss(string id, Rgba color, int frame, BossView view = BossView.Front, BossPose pose = BossPose.Idle) => id switch
        {
            "crystal_queen" => Queen(frame, view, pose),
            // The lava titan: the warden's build in black basalt, its seams and core burning.
            "lava_titan" => Warden(Rgba.Hex("#4A3A36"), frame, view, pose, molten: true),
            "void_lord" => VoidLord(frame, view, pose),
            // The mad general of the demon nest (2026-11-10): the void lord's robed build in crimson and ember, a ball
            // of fire for the orb, horns through the hood.
            "mad_general" => VoidLord(frame, view, pose, "#5A1A24", "#E86A3A", "#FF9A4A", "#FFD27A", horns: true),
            "tide_serpent" => Serpent(frame, view, pose),
            // The crown wraith under the harbour's lake (2026-11-11): the robed build drowned, sea-green and pale, a cold
            // orb, the sunken crown on its hood.
            "crown_wraith" => VoidLord(frame, view, pose, "#1E4A4E", "#7AD8C8", "#A8F0FF", "#E8FFFF", crown: true),
            "antlion" => Antlion(frame, view, pose),
            // The vein eater of the mining village (2026-11-12): a Retro Diffusion picture posed in code. WorldView draws
            // its own wind-up for each attack (VeinPose); these are its plain poses for everywhere else.
            "vein_eater" => VeinEaterArt.Draw(view, pose switch
            {
                BossPose.Windup => VeinPose.Rear,
                BossPose.Strike => VeinPose.Swipe,
                BossPose.Charge => VeinPose.Rush,
                BossPose.Roar => VeinPose.Roar,
                _ => VeinPose.Idle,
            }, frame, 1, false),
            // The samurai of the forgotten shrine (2026-11-12): likewise a Retro Diffusion picture posed in code, its blade
            // drawn in code (SamuraiPose in WorldView).
            "shrine_samurai" => SamuraiArt.Draw(view, pose switch
            {
                BossPose.Windup => SamuraiPose.Stance,
                BossPose.Strike => SamuraiPose.Draw,
                BossPose.Charge => SamuraiPose.Dash,
                BossPose.Roar => SamuraiPose.Kiai,
                _ => SamuraiPose.Idle,
            }, frame, 1),
            // The black knight of the hero's hut (2026-11-12): likewise, its war hammer drawn in code (KnightPose in
            // WorldView).
            "black_knight" => KnightArt.Draw(view, pose switch
            {
                BossPose.Windup => KnightPose.Raise,
                BossPose.Strike => KnightPose.Smash,
                BossPose.Charge => KnightPose.Charge,
                BossPose.Roar => KnightPose.Roar,
                _ => KnightPose.Idle,
            }, frame, 1),
            // The venom spider of the mushroom hollow (2026-11-13): likewise, its legs drawn in code (SpiderPose in
            // WorldView).
            "venom_spider" => SpiderArt.Draw(view, pose switch
            {
                BossPose.Windup => SpiderPose.Rear,
                BossPose.Strike => SpiderPose.Bite,
                BossPose.Charge => SpiderPose.Walk,
                BossPose.Roar => SpiderPose.Roar,
                _ => SpiderPose.Idle,
            }, pose == BossPose.Windup ? 11 : pose == BossPose.Strike ? SpiderArt.StrikeFrame(SpiderPose.Bite) : pose == BossPose.Roar ? SpiderArt.RoarRise : frame, 1),
            _ => Warden(color, frame, view, pose),
        };

        static bool Behind(BossView view) => view == BossView.Back || view == BossView.BackSide;

        // How far a view turns the face toward the right, in steps (0 front or back, 1 three-quarter, 2 side).
        static int Turn(BossView view) => view == BossView.Side ? 2 : view == BossView.FrontSide || view == BossView.BackSide ? 1 : 0;

        // The tide serpent: a long teal body in three coils, a pale belly, its head reared up on a curving neck with a
        // fin crest, gold eyes and fangs. Its head turns with the view; it rears back to wind up, lunges to strike and
        // opens its jaws to roar.
        static PixelImage Serpent(int frame, BossView view, BossPose pose)
        {
            const int width = 44;
            const int height = 42;
            var image = new PixelImage(width, height);
            var skin = Ramp.Of("#2E8C9A");
            var belly = Ramp.Of("#BFE8DC");
            var fin = Ramp.Of("#5AD2E6");
            var eye = Rgba.Hex("#FFE85A");
            int sway = frame % 2 == 1 ? 1 : 0;
            bool behind = Behind(view);
            int turn = Turn(view);
            void Set(int x, int y, Rgba c)
            {
                if (image.Inside(x, y)) image[x, y] = c;
            }

            // The coils, widest at the bottom, each a flattened ring with a light top and a dark underside; from behind
            // no pale belly shows.
            for (int ring = 0; ring < 3; ring++)
            {
                int cy = 37 - ring * 5;
                int rx = 16 - ring * 4;
                const int ry = 4;
                for (int y = cy - ry; y <= cy + ry; y++)
                {
                    for (int x = 22 - rx; x <= 22 + rx; x++)
                    {
                        float dx = (x - 22) / (float)rx;
                        float dy = (y - cy) / (float)ry;
                        float d = dx * dx + dy * dy;
                        if (d > 1) continue;
                        var c = dy < -0.45f ? skin.Light : dy > 0.55f ? skin.Dark : (x * 3 + y) % 7 == 0 ? skin.Shade : skin.Main;
                        if (!behind && dy > 0.1f && dy < 0.45f && d < 0.8f) c = x % 2 == 0 ? belly.Main : belly.Shade;
                        Set(x, y, c);
                    }
                }
            }
            // Where the head goes: reared back and high to wind up, lunging forward and low to strike, up for a roar.
            int reachX = pose switch { BossPose.Windup => -3, BossPose.Strike => 5, BossPose.Charge => 4, _ => 0 } + (turn == 0 ? 0 : sway);
            int reachY = pose switch { BossPose.Windup => -3, BossPose.Strike => 6, BossPose.Charge => 4, BossPose.Roar => -2, _ => 0 } - (turn == 0 ? sway : 0);
            // The neck curving up out of the top coil to the head, bending toward the side it faces.
            int hx = 22 + turn * 2 + reachX;
            int hy = 10 + reachY;
            for (int i = 0; i <= 14; i++)
            {
                float t = i / 14f;
                int nx = (int)System.Math.Round(22 + (hx - 22) * t + System.Math.Sin(t * System.Math.PI) * (turn == 0 ? 2 : 4));
                int ny = (int)System.Math.Round(26 + (hy + 3 - 26) * t);
                for (int w = -2; w <= 2; w++)
                {
                    var c = w == -2 ? skin.Light : w == 2 ? skin.Dark : w == 0 && !behind ? belly.Main : skin.Main;
                    Set(nx + w, ny, c);
                }
                if (i % 2 == 0) Set(nx - 3, ny, fin.Main);
            }
            bool open = pose == BossPose.Roar || pose == BossPose.Strike;
            if (turn == 0)
            {
                // Head on: a round head, the crest fanned behind it; facing the viewer, two eyes and fangs.
                for (int y = -3; y <= 3; y++)
                {
                    for (int x = -4; x <= 4; x++)
                    {
                        if (x * x + y * y * 2 > 22) continue;
                        Set(hx + x, hy + y, y <= -2 ? skin.Light : y >= 2 ? skin.Dark : skin.Main);
                    }
                }
                for (int i = -5; i <= 5; i += 2) Set(hx + i, hy - 4 - (5 - System.Math.Abs(i)) / 2, fin.Light);
                if (!behind)
                {
                    Set(hx - 2, hy - 1, eye);
                    Set(hx + 2, hy - 1, eye);
                    Set(hx - 1, hy + 2 + (open ? 1 : 0), Rgba.White);
                    Set(hx + 1, hy + 2 + (open ? 1 : 0), Rgba.White);
                    if (open) Set(hx, hy + 2, Rgba.Hex("#7A1E2E"));
                }
                return image.Outlined(Rgba.Hex("#0A1A1E"));
            }
            // Turned: a wedge pointing right, the crest behind it.
            for (int y = -3; y <= 3; y++)
            {
                int reach = 5 - System.Math.Abs(y) / 2 + (turn == 2 ? 1 : 0);
                for (int x = -4; x <= reach + 2; x++)
                {
                    var c = y <= -2 ? skin.Light : y >= 2 ? skin.Dark : skin.Main;
                    if (!behind && y == 2 && x > 0) c = belly.Main;
                    Set(hx + x, hy + y + (open && y >= 2 && x > 2 ? 1 : 0), c);
                }
            }
            for (int i = 0; i < 4; i++)
            {
                Set(hx - 5 - i, hy - 3 + i, fin.Light);
                Set(hx - 4 - i, hy - 4 + i, fin.Main);
            }
            if (!behind)
            {
                Set(hx + 3, hy - 1, eye);
                if (turn == 1) Set(hx, hy - 1, eye);
                Set(hx + 6, hy + 2 + (open ? 1 : 0), Rgba.White);
                Set(hx + 4, hy + 3 + (open ? 1 : 0), Rgba.White);
                if (open) Set(hx + 5, hy + 2, Rgba.Hex("#7A1E2E"));
            }
            return image.Outlined(Rgba.Hex("#0A1A1E"));
        }

        // The void lord: a hooded shape in a long cloak that floats over the ground, two cold eyes in the dark of the
        // hood, an orb of the void held before it and stars turning round its head. It lifts the orb overhead to wind
        // up, thrusts it out to strike and flares its cloak to roar; side on, the cloak narrows and the orb is held
        // out in front; from behind, the hood is closed and the orb hidden.
        static PixelImage VoidLord(int frame, BossView view, BossPose pose, string cloakColor = "#3A2C5C", string trimColor = "#9A7CFF",
            string orbColor = "#C8A8FF", string eyeColor = "#BFF4FF", bool horns = false, bool crown = false)
        {
            const int width = 46;
            const int height = 46;
            var image = new PixelImage(width, height);
            var cloak = Ramp.Of(cloakColor);
            var trim = Ramp.Of(trimColor);
            var orb = Ramp.Of(orbColor);
            var eye = Rgba.Hex(eyeColor);
            int bob = (frame % 2 == 1 ? -1 : 0) + (pose == BossPose.Windup ? -2 : pose == BossPose.Strike ? 1 : 0);
            bool behind = Behind(view);
            int turn = Turn(view);
            const int cx = 23;
            int face = cx + turn * 2;
            int flare = pose == BossPose.Roar ? 4 : pose == BossPose.Strike ? 2 : 0;
            float narrow = turn == 2 ? 0.65f : turn == 1 ? 0.85f : 1;
            void Set(int x, int y, Rgba c)
            {
                if (image.Inside(x, y)) image[x, y] = c;
            }

            // The cloak: wider toward the hem, frayed at the bottom, with a lit edge down each side; a charge drags it
            // back away from the way it rushes.
            for (int y = 13; y < 40; y++)
            {
                int half = (int)System.Math.Round((5 + (y - 13) * (9 + flare) / 27f) * narrow);
                int drag = pose == BossPose.Charge ? -(y - 13) / 6 : 0;
                bool frayed = y >= 37;
                for (int x = cx - half; x <= cx + half; x++)
                {
                    if (frayed && (x + y) % 3 == 0) continue;
                    var color = x <= cx - half + 1 ? cloak.Light : x >= cx + half - 1 ? cloak.Dark : (x + y) % 7 == 0 ? cloak.Shade : cloak.Main;
                    Set(x + drag, y + bob, color);
                }
                if (!frayed)
                {
                    Set(cx - half + drag, y + bob, trim.Main);
                    Set(cx + half + drag, y + bob, trim.Shade);
                }
            }
            // The hood: dark inside facing the viewer, closed cloth from behind.
            for (int y = 6; y < 16; y++)
            {
                int half = y < 9 ? y - 4 : 6;
                for (int x = face - half; x <= face + half; x++) Set(x, y + bob, cloak.Main);
            }
            if (!behind)
            {
                int left = face - 3 + turn;
                for (int y = 9; y < 15; y++)
                {
                    for (int x = left; x <= face + 3; x++) Set(x, y + bob, Rgba.Hex("#08060C"));
                }
                var bright = pose == BossPose.Roar || pose == BossPose.Windup ? Rgba.White : eye;
                if (turn < 2) Set(face - 2 + turn, 11 + bob, bright);
                Set(face + 2, 11 + bob, bright);
                if (turn < 2) Set(face - 2 + turn, 12 + bob, trim.Main);
                Set(face + 2, 12 + bob, trim.Main);
            }
            else
            {
                for (int y = 8; y < 15; y++) Set(face, y + bob, cloak.Shade);
            }
            // Horns of bone curving up out of the hood.
            if (horns)
            {
                var bone = Ramp.Of("#E2DCCA");
                for (int i = 0; i < 6; i++)
                {
                    Set(face - 5 - i / 2, 7 - i + bob, i < 3 ? bone.Main : bone.Light);
                    Set(face - 4 - i / 2, 7 - i + bob, bone.Shade);
                    Set(face + 5 + i / 2, 7 - i + bob, i < 3 ? bone.Shade : bone.Main);
                    Set(face + 4 + i / 2, 7 - i + bob, bone.Dark);
                }
            }
            // A sunken king's crown over the hood (2026-11-11, the crown wraith): a gold band with three points, green
            // with the lake's tarnish in places.
            if (crown)
            {
                var gold = Ramp.Of("#F0BE40");
                var tarnish = Rgba.Hex("#5AA88A");
                for (int x = face - 5; x <= face + 5; x++)
                {
                    Set(x, 6 + bob, (x - face) % 4 == 0 ? tarnish : gold.Main);
                    Set(x, 7 + bob, gold.Shade);
                }
                foreach (int px in new[] { face - 4, face, face + 4 })
                {
                    Set(px, 5 + bob, gold.Light);
                    Set(px, 4 + bob, gold.Main);
                }
                Set(face, 3 + bob, gold.Light);
            }
            // The orb with sleeves reaching to it: before the chest, overhead to wind up, thrust out and down to strike;
            // side on it is held out ahead. From behind only the sleeves show.
            int ox = cx + turn * 4 + (pose == BossPose.Strike ? turn * 2 : 0);
            int oy = pose == BossPose.Windup ? 3 : pose == BossPose.Strike ? 27 : 24;
            if (!behind)
            {
                int glow = pose == BossPose.Strike || pose == BossPose.Windup ? 14 : 10;
                for (int dy = -4; dy <= 4; dy++)
                {
                    for (int dx = -4; dx <= 4; dx++)
                    {
                        int d = dx * dx + dy * dy;
                        if (d > glow) continue;
                        Set(ox + dx, oy + dy + bob, d <= 1 ? Rgba.White : d <= 4 ? orb.Light : d <= 8 ? orb.Main : orb.Shade);
                    }
                }
            }
            int sleeveY = pose == BossPose.Windup ? 8 : 21;
            for (int i = 0; i < 5; i++)
            {
                int toward = pose == BossPose.Windup ? -i : i / 2;
                Set(cx - (int)(9 * narrow) + i, sleeveY + toward + bob, cloak.Light);
                Set(cx + (int)(9 * narrow) - i, sleeveY + toward + bob, cloak.Shade);
            }
            // Stars turning round the head, faster in a roar.
            for (int i = 0; i < 6; i++)
            {
                double angle = (i + frame * (pose == BossPose.Roar ? 1.5 : 0.5)) / 6 * System.Math.PI * 2;
                int sx = face + (int)System.Math.Round(System.Math.Cos(angle) * 13);
                int sy = 11 + (int)System.Math.Round(System.Math.Sin(angle) * 4) + bob;
                Set(sx, sy, i % 2 == 0 ? Rgba.White : trim.Light);
            }
            return image.Outlined(Rgba.Hex("#08060C"));
        }

        // The cave warden (and in basalt the lava titan): a golem of stacked stone blocks with a glowing core. Its
        // arms go up over its head to wind up, smash down in front to strike, swing back in a charge and fling wide
        // to roar. Side on it narrows to one arm in front of the other; from behind, a back of stones, no face or core.
        static PixelImage Warden(Rgba color, int frame, BossView view, BossPose pose, bool molten = false)
        {
            const int width = 44;
            const int height = 42;
            const int ox = 4;
            const int oy = 6;
            var image = new PixelImage(width, height);
            var stone = Ramp.Of(color);
            var darkStone = Ramp.Of(color.Scale(0.8f));
            var farStone = Ramp.Of(color.Scale(0.65f));
            var glow = Ramp.Of(Rgba.Hex("#FFB040"));
            var moss = molten ? Rgba.Hex("#FF7A1E") : Rgba.Hex("#5E8A3C");
            bool behind = Behind(view);
            int turn = Turn(view);
            int bob = (frame % 2 == 1 ? -1 : 0) + (pose == BossPose.Windup ? -1 : pose == BossPose.Strike ? 1 : 0);
            // The arms: how far up, and how far in toward the body (out when negative), by the pose.
            var (shoulderUp, armUp, armIn) = pose switch
            {
                BossPose.Windup => (3, 19, 1),
                BossPose.Strike => (-2, -4, 3),
                BossPose.Charge => (0, 2, -1),
                BossPose.Roar => (2, 6, -3),
                _ => (0, 0, 0),
            };
            void Put(int x, int y, Rgba c)
            {
                if (image.Inside(x + ox, y + oy)) image[x + ox, y + oy] = c;
            }
            void Stone(int x, int y, int w, int h, Ramp ramp, int salt)
            {
                var part = new PixelImage(w, h);
                Block(part, 0, 0, w, h, ramp, salt);
                for (int j = 0; j < h; j++)
                {
                    for (int i = 0; i < w; i++)
                    {
                        if (part[i, j].A > 0) Put(x + i, y + j, part[i, j]);
                    }
                }
            }

            // Side on the body is narrower and moves toward the way it faces; turned three-quarters, a little.
            int torsoX = turn == 2 ? 9 : 5 + turn;
            int torsoW = turn == 2 ? 18 : 26 - turn * 2;
            int headX = 12 + turn * 3;
            int lean = pose == BossPose.Charge ? turn * 2 : 0;
            // The far arm first (darker, partly behind the body), then legs, body, head and the near arm over them.
            int nearArmX = 26 + (turn == 2 ? -4 : 0) - armIn;
            int farArmX = turn == 2 ? 6 + armIn : 0 + armIn;
            if (turn > 0)
            {
                Stone(farArmX, 16 + bob - armUp, 8, 12, farStone, 4);
                Stone(farArmX, 7 + bob - shoulderUp, 10, 10, farStone, 6);
            }
            if (turn == 2)
            {
                Stone(12, 25, 7, 9, farStone, 1);
                Stone(17, 25, 7, 9, darkStone, 2);
            }
            else
            {
                Stone(9 + turn, 25, 7, 9, darkStone, 1);
                Stone(20 + turn, 25, 7, 9, darkStone, 2);
            }
            Stone(torsoX + lean, 10 + bob, torsoW, 17, stone, 3);
            if (turn == 0)
            {
                Stone(0 + armIn, 16 + bob - armUp, 8, 12, darkStone, 4);
                Stone(0 + armIn, 7 + bob - shoulderUp, 10, 10, stone, 6);
            }
            Stone(headX + lean, 1 + bob - (pose == BossPose.Roar ? 1 : 0), 12, 10, stone, 8);
            Stone(nearArmX + 2 + lean, 16 + bob - armUp, 8, 12, darkStone, 5);
            Stone(nearArmX + lean, 7 + bob - shoulderUp, 10, 10, stone, 7);

            int hy = 1 + bob - (pose == BossPose.Roar ? 1 : 0);
            int cx = 18 + turn * 3 + lean;
            int cy = 17 + bob;
            if (!behind)
            {
                // Brow and eyes: both facing the viewer, the near one side on.
                for (int x = headX + 1 + lean; x < headX + 11 + lean; x++) Put(x, hy + 4, stone.Dark);
                var eyes = turn == 2 ? new[] { 20, 21 } : new[] { 15, 16, 19, 20 };
                foreach (int x in eyes)
                {
                    int ex = x + (turn == 2 ? 3 : turn * 3) + lean;
                    Put(ex, hy + 5, pose == BossPose.Windup || pose == BossPose.Roar ? Rgba.White : glow.Light);
                    Put(ex, hy + 6, glow.Main);
                }
                // The core: a gem in the chest with a ring of light, brighter as it winds up; side on, its edge.
                int reach = pose == BossPose.Windup || pose == BossPose.Strike ? 4 : 3;
                for (int dy = -reach; dy <= reach; dy++)
                {
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        if (turn == 2 && dx > 1) continue;
                        int d = System.Math.Abs(dx) + System.Math.Abs(dy);
                        if (d == reach) Put(cx + dx, cy + dy, stone.Dark);
                        else if (d == reach - 1) Put(cx + dx, cy + dy, glow.Shade);
                        else if (d >= 1) Put(cx + dx, cy + dy, glow.Main);
                        else Put(cx + dx, cy + dy, glow.Light);
                    }
                }
                Put(cx - 1, cy - 1, Rgba.White);
            }
            else
            {
                // From behind: a spine of darker stones down the back.
                for (int y = 12; y < 25; y += 3)
                {
                    for (int x = cx - 2; x <= cx + 1; x++) Put(x, y + bob, stone.Shade);
                    Put(cx - 2, y + bob + 1, stone.Dark);
                }
            }

            // Cracks and moss, riding on their parts.
            Line(image, torsoX + 3 + ox + lean, 14 + bob + oy, new[] { (1, 1), (1, 0), (1, 1), (0, 1), (1, 1) }, stone.Dark);
            foreach (var (x, y) in new[] { (2, 8), (3, 8), (4, 8), (3, 9) }) if (turn == 0) Put(x + armIn, y + bob - shoulderUp, moss);
            foreach (var (x, y) in new[] { (3, 1), (4, 1), (2, 1), (3, 2) }) Put(nearArmX + x, 7 + y + bob - shoulderUp, moss);
            foreach (var (x, y) in new[] { (1, 1), (2, 1), (3, 1), (2, 2) }) Put(headX + x + lean, hy + y, moss);
            if (molten)
            {
                // Glowing seams down the body and the near arm.
                Line(image, torsoX + 5 + ox + lean, 12 + bob + oy, new[] { (1, 1), (0, 1), (1, 1), (1, 0), (0, 1) }, Rgba.Hex("#FFB040"));
                if (turn < 2) Line(image, torsoX + torsoW - 7 + ox + lean, 12 + bob + oy, new[] { (1, 1), (0, 1), (-1, 1), (0, 1), (1, 1) }, Rgba.Hex("#FFB040"));
                Line(image, nearArmX + 5 + ox + lean, 18 + bob - armUp + oy, new[] { (-1, 1), (0, 1), (-1, 1) }, Rgba.Hex("#FF7A1E"));
            }
            return image.Outlined(stone.Outline);
        }

        // The antlion (2026-11-09): a sand-brown abdomen in dark bands, a small head with two great sickle jaws, six
        // legs. It lifts its jaws wide to wind up, snaps them shut lunging to strike, drops its head low in a charge
        // and throws its jaws open with sand flying to roar. The head is toward the way it faces: below the body
        // facing the viewer, hidden above it from behind, out to the side side on.
        static PixelImage Antlion(int frame, BossView view, BossPose pose)
        {
            const int width = 46;
            const int height = 42;
            const int ox = 2;
            const int oy = 4;
            var image = new PixelImage(width, height);
            var body = Ramp.Of("#9C7A4A");
            var leg = Ramp.Of("#6E5232");
            var head = Ramp.Of("#7E6038");
            var jaw = Rgba.Hex("#EADCB8");
            var jawShade = Rgba.Hex("#B8A27A");
            var eye = Rgba.Hex("#1E140A");
            int shift = frame % 2;
            bool behind = Behind(view);
            int turn = Turn(view);
            int lunge = pose == BossPose.Strike || pose == BossPose.Charge ? 3 : pose == BossPose.Windup ? -2 : 0;
            int low = pose == BossPose.Charge ? 2 : 0;
            int bx = 21 - turn * 2;
            int by = behind ? 16 : 13;
            int hx = 21 + turn * 6 + (turn > 0 ? lunge : 0);
            int hy = (behind ? 6 : 21 - turn * 3) + (turn == 0 ? (behind ? -lunge : lunge) : 0) + low;
            void Leg(int x0, int y0, int x1, int y1, Rgba color, Rgba light) => Thick(image, x0 + ox, y0 + oy, x1 + ox, y1 + oy, color, light);
            void Put(int x, int y, Rgba color) => image[x + ox, y + oy] = color;

            // Six legs, three a side, stepping pair by pair; the roar lifts them and a wind-up braces the front pair.
            for (int i = 0; i < 3; i++)
            {
                int lift = (i + shift) % 2 == 0 ? -1 : 1;
                if (pose == BossPose.Roar) lift -= 2;
                int startY = by + 3 + i * 4;
                int kneeY = by - 3 + i * 6 + lift;
                int footY = by + 4 + i * 6 + lift + (pose == BossPose.Windup && i == 0 ? 2 : 0);
                foreach (int side in new[] { -1, 1 })
                {
                    bool far = turn == 2 && side < 0;
                    int spread = far ? 10 : 13;
                    int startX = bx + side * 7;
                    int kneeX = bx + side * (spread + i);
                    int footX = bx + side * (spread + 3 + i) + (pose == BossPose.Strike ? side * 2 : 0);
                    Leg(startX, startY, kneeX, kneeY, far ? leg.Shade : leg.Main, far ? leg.Main : leg.Light);
                    Leg(kneeX, kneeY, footX, footY, far ? leg.Dark : leg.Shade, far ? leg.Shade : leg.Main);
                }
            }

            void Oval(int cx, int cy, int rx, int ry, Ramp ramp)
            {
                var part = new PixelImage(width, height);
                Ellipse(part, cx + ox, cy + oy, rx, ry, ramp);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (part[x, y].A > 0) image[x, y] = part[x, y];
                    }
                }
            }

            // The jaws: two sickles out of the head, open by the pose. Facing the viewer they curve down and in; side
            // on they point ahead, one over the other.
            int open = pose == BossPose.Windup || pose == BossPose.Roar ? 6 : pose == BossPose.Strike ? 0 : 3;
            void Jaws()
            {
                if (turn == 2)
                {
                    // Upper and lower jaw ahead of the head, parted by the pose.
                    Thick(image, hx + 4 + ox, hy - 2 + oy, hx + 9 + ox, hy - 3 - open / 2 + oy, jawShade, jaw);
                    Thick(image, hx + 9 + ox, hy - 3 - open / 2 + oy, hx + 12 + ox, hy - 1 + oy, jawShade, jaw);
                    Thick(image, hx + 4 + ox, hy + 1 + oy, hx + 9 + ox, hy + 2 + open / 2 + oy, jawShade, jaw);
                    Thick(image, hx + 9 + ox, hy + 2 + open / 2 + oy, hx + 12 + ox, hy + 1 + oy, jawShade, jaw);
                    return;
                }
                foreach (int s in new[] { -1, 1 })
                {
                    // A sickle: out from the head, round, and its point turned back in.
                    int x0 = hx + s * 3, y0 = hy + 2;
                    int x1 = hx + s * (5 + open), y1 = hy + 5;
                    int x2 = hx + s * (4 + open), y2 = hy + 9;
                    int x3 = hx + s * (2 + open / 2), y3 = hy + 11;
                    Thick(image, x0 + ox, y0 + oy, x1 + ox, y1 + oy, jawShade, jaw);
                    Thick(image, x1 + ox, y1 + oy, x2 + ox, y2 + oy, jawShade, jaw);
                    Thick(image, x2 + ox, y2 + oy, x3 + ox, y3 + oy, jawShade, jaw);
                }
            }

            if (behind) Oval(hx, hy, 6, 4, head);
            // The abdomen, banded.
            Oval(bx, by, turn == 2 ? 11 : 12, 9, body);
            for (int band = -6; band <= 6; band += 3)
            {
                int y = by + band;
                int half = (int)((turn == 2 ? 11 : 12) * System.Math.Sqrt(System.Math.Max(0, 1 - band * band / 81.0))) - 1;
                for (int x = bx - half; x <= bx + half; x++)
                {
                    if (image[x + ox, y + oy].A > 0) image[x + ox, y + oy] = body.Shade;
                }
            }
            if (!behind)
            {
                Jaws();
                Oval(hx, hy, turn == 2 ? 6 : 7, 5, head);
                // Two small black eyes.
                if (turn == 2) Put(hx + 2, hy - 2, eye);
                else
                {
                    Put(hx - 2, hy - 2, eye);
                    Put(hx + 2, hy - 2, eye);
                }
            }
            // Roaring, sand thrown up round it.
            if (pose == BossPose.Roar)
            {
                var sand = Rgba.Hex("#D8B878");
                foreach (var (x, y) in new[] { (bx - 14, by - 10), (bx - 9, by - 13), (bx + 10, by - 12), (bx + 15, by - 8), (bx - 4, by - 15), (bx + 4, by - 14) })
                {
                    Put(x, y + shift, sand);
                    Put(x + 1, y + 1 + shift, sand.Scale(0.8f));
                }
            }
            return image.Outlined(body.Outline);
        }

        // The crystal spider queen: a dark round body grown over with crystals, a head with a row of pink eyes, and
        // eight jointed legs. In the second frame her legs shift, pair by pair. She rears her front legs to wind up,
        // lunges head first to strike or charge, and lifts every leg to roar. The head is toward the way she faces:
        // below her body facing the viewer, hidden above it from behind, out to the side side on.
        static PixelImage Queen(int frame, BossView view, BossPose pose)
        {
            const int width = 46;
            const int height = 42;
            const int ox = 2;
            const int oy = 4;
            var image = new PixelImage(width, height);
            var body = Ramp.Of("#4E4062");
            var leg = Ramp.Of("#6E5C88");
            var crystal = Ramp.Of("#7CD2E2");
            var eye = Rgba.Hex("#FF5C8A");
            int shift = frame % 2;
            bool behind = Behind(view);
            int turn = Turn(view);
            int lunge = pose == BossPose.Strike || pose == BossPose.Charge ? 3 : pose == BossPose.Windup ? -2 : 0;
            // The body's centre and the head's, by the view.
            int bx = 21 - turn * 2;
            int by = behind ? 17 : 13;
            int hx = 21 + turn * 5 + (turn > 0 ? lunge : 0);
            int hy = (behind ? 7 : 25 - turn * 3) + (turn == 0 ? (behind ? -lunge : lunge) : 0);
            void Leg(int x0, int y0, int x1, int y1, Rgba color, Rgba light)
            {
                Thick(image, x0 + ox, y0 + oy, x1 + ox, y1 + oy, color, light);
            }

            // Legs first, under the body: from the body's sides out to a knee and down to a foot. The front pair rears
            // up to wind up; a roar lifts them all.
            for (int i = 0; i < 4; i++)
            {
                int lift = (i + shift) % 2 == 0 ? -1 : 1;
                if (pose == BossPose.Roar) lift -= 3;
                if (pose == BossPose.Windup && i == 0) lift -= 6;
                int startY = by + 4 + i * 3;
                int kneeY = by - 5 + i * 5 + lift;
                int footY = by + 2 + i * 5 + lift + (pose == BossPose.Windup && i == 0 ? -4 : 0);
                foreach (int side in new[] { -1, 1 })
                {
                    // Side on, the far legs are shorter and darker.
                    bool far = turn == 2 && side < 0;
                    int spread = far ? 10 : 14;
                    int startX = bx + side * 6;
                    int kneeX = bx + side * (spread + i);
                    int footX = bx + side * (spread + 5 + i / 2) + (pose == BossPose.Strike ? side * 2 : 0);
                    Leg(startX, startY, kneeX, kneeY, far ? leg.Shade : leg.Main, far ? leg.Main : leg.Light);
                    Leg(kneeX, kneeY, footX, footY, far ? leg.Dark : leg.Shade, far ? leg.Shade : leg.Main);
                }
            }

            void Oval(int cx, int cy, int rx, int ry, Ramp ramp)
            {
                var part = new PixelImage(width, height);
                Ellipse(part, cx + ox, cy + oy, rx, ry, ramp);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (part[x, y].A > 0) image[x, y] = part[x, y];
                    }
                }
            }

            // From behind the head is hidden past the body, so it goes first; otherwise after.
            if (behind) Oval(hx, hy, 6, 4, Ramp.Of("#5C4C72"));
            Oval(bx, by, turn == 2 ? 10 : 11, 9, body);
            if (!behind) Oval(hx, hy, turn == 2 ? 6 : 7, 5, Ramp.Of("#5C4C72"));

            // Crystals on her back.
            foreach (var (x, y, h) in new[] { (-6, -4, 5), (-1, -7, 7), (5, -4, 5), (-3, 1, 4), (3, 1, 4) })
            {
                int cx = bx + x + ox;
                int cy = by + y + oy;
                for (int j = 0; j < h; j++)
                {
                    int half = (h - j) / 3;
                    for (int dx = -half; dx <= half; dx++) image[cx + dx, cy - j + h / 2] = dx < 0 ? crystal.Light : dx > 0 ? crystal.Shade : crystal.Main;
                }
                image[cx, cy - h / 2] = Rgba.White;
            }

            if (!behind)
            {
                // Her eyes, a row of four and two above, and fangs below that part to strike or roar.
                var bright = pose == BossPose.Windup || pose == BossPose.Roar ? Rgba.White : eye;
                foreach (int x in turn == 2 ? new[] { 1, 3, 5 } : new[] { -4, -2, 2, 4 }) image[hx + x + ox, hy - 1 + oy] = bright;
                if (turn < 2)
                {
                    image[hx - 1 + ox, hy - 2 + oy] = eye;
                    image[hx + 1 + ox, hy - 2 + oy] = eye;
                }
                int part = pose == BossPose.Strike || pose == BossPose.Roar ? 1 : 0;
                var fang = Rgba.Hex("#E8E0D0");
                int fx = hx + (turn == 2 ? 4 : 0);
                for (int dy = 0; dy < 2; dy++)
                {
                    image[fx - 2 - part + ox, hy + 4 + dy + oy] = fang;
                    image[fx + 2 + part + ox, hy + 4 + dy + oy] = fang;
                }
            }
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
