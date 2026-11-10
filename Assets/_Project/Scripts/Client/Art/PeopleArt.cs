using System.Collections.Generic;

namespace Game.Client
{
    // The people at their stalls (Docs/Npcs.md), each drawn as themselves (2026-11-09): the merchant in a red
    // headscarf with gold coins, tossing a coin; the blacksmith bald and bearded in a leather apron, hammering on the
    // counter; the fisher under a wide straw hat, the rod out and the float dipping; the alchemist in a pointed hat,
    // bubbles rising from a green flask. All breathe and blink. Their clothes are their colour in npcs.csv, so a
    // person recoloured there needs no change here; a person with no drawing of their own is a standing player.
    static class PeopleArt
    {
        // A loop of Frames frames at FramesPerSecond (2.4 seconds).
        public const int Frames = 12;
        public const float FramesPerSecond = 5;

        static readonly Dictionary<char, Rgba> Colors = new Dictionary<char, Rgba>
        {
            ['h'] = Rgba.Hex("#5A3420"),
            ['H'] = Rgba.Hex("#8A5632"),
            ['s'] = Rgba.Hex("#F2C49C"),
            ['S'] = Rgba.Hex("#D39A74"),
            ['e'] = Rgba.Hex("#1C1626"),
            ['b'] = Rgba.Hex("#4A3020"),
            ['B'] = Rgba.Hex("#E0B850"),
            ['p'] = Rgba.Hex("#3E3A56"),
            ['P'] = Rgba.Hex("#2C2940"),
            ['o'] = Rgba.Hex("#5A3A24"),
            ['Y'] = Rgba.Hex("#F0C850"),
            ['y'] = Rgba.Hex("#B8862A"),
            ['r'] = Rgba.Hex("#A83232"),
            ['R'] = Rgba.Hex("#E05A4A"),
            ['c'] = Rgba.Hex("#F6F2E8"),
            ['w'] = Rgba.Hex("#9A6A3A"),
            ['W'] = Rgba.Hex("#6A4424"),
            ['i'] = Rgba.Hex("#5A5E6A"),
            ['I'] = Rgba.Hex("#B8BEC8"),
            ['a'] = Rgba.Hex("#7A4A2A"),
            ['A'] = Rgba.Hex("#5A3420"),
            ['k'] = Rgba.Hex("#4A2C1C"),
            ['K'] = Rgba.Hex("#6E4228"),
            ['n'] = Rgba.Hex("#ECEAF4"),
            ['N'] = Rgba.Hex("#B4B0CC"),
            ['l'] = Rgba.Hex("#8A90A0"),
            ['g'] = Rgba.Hex("#3AC86A"),
            ['G'] = Rgba.Hex("#B8FFB0"),
            ['F'] = Rgba.Hex("#FFE07A"),
            ['f'] = Rgba.Hex("#F08A2A"),
        };

        // The person by their id in npcs.csv in their colour, at a frame of the loop; null for one not drawn here.
        public static PixelImage Person(string id, Rgba color, int frame)
        {
            frame = (frame % Frames + Frames) % Frames;
            return id switch
            {
                "merchant" => Merchant(color, frame),
                "blacksmith" => Blacksmith(color, frame),
                "fisher" => Fisher(color, frame),
                "alchemist" => Alchemist(color, frame),
                _ => null,
            };
        }

        // ---- The merchant: a coin tossed and caught, then a rest ----

        static readonly string[] MerchantRows =
        {
            "..............",
            ".....rRRr.....",
            "...rRRRRRRr...",
            "..rRYRRYRRYr..",
            "..rrrrrrrrrr..",
            "..hhsssssshh..",
            "..hhsesseshh..",
            "...hsesseSh...",
            "..hhssssSShh..",
            "..hhTYYYYThh..",
            "..tTTTUUTTTt..",
            "..tTTTUUTTTt..",
            "...bbbBBbbb...",
            "..tTTTTTTTTt..",
            "..tTTTTTTTTt..",
            "...tTTTTTTt...",
            "...oo....oo...",
        };

        static PixelImage Merchant(Rgba color, int frame)
        {
            var marks = new List<(int, int, char)>
            {
                // The left hand on her belt, the right raised before her shoulder.
                (1, 11, 't'), (2, 12, 's'),
                (12, 11, 't'), (12, 10, 's'),
            };
            var loose = new List<(int, int, char)>();
            // Up and down over the first six frames, turning (edge on, the darker gold) as it goes.
            int[] heights = { 9, 7, 5, 4, 5, 7 };
            loose.Add(frame < heights.Length ? (12, heights[frame], frame % 2 == 0 ? 'Y' : 'y') : (12, 9, 'Y'));
            return Paint(MerchantRows, color, frame, marks, loose, eyeRow: 6, legs: 16, breathes: true);
        }

        // ---- The blacksmith: two blows on the counter a loop, sparks flying ----

        static readonly string[] BlacksmithRows =
        {
            "..............",
            "....ssssss....",
            "...sssssssS...",
            "..bbbbbbbbbb..",
            "..ssKKssKKss..",
            "..sssessesss..",
            "..skkkSSkkks..",
            "..kkkkAAkkkk..",
            "...kkkkkkkk...",
            "....kkKkkk....",
            "..tTTkkkkTTt..",
            "..tTaAAAAaTt..",
            "...taAAAAat...",
            "..tbaAAAAabt..",
            "...aAAAAAAa...",
            "...pp....pp...",
            "...oo....oo...",
        };

        enum Swing { Up, Mid, Down, After }

        static readonly Swing[] Blows = { Swing.Up, Swing.Up, Swing.Mid, Swing.Down, Swing.After, Swing.Mid, Swing.Up, Swing.Up, Swing.Mid, Swing.Down, Swing.After, Swing.Mid };

        static PixelImage Blacksmith(Rgba color, int frame)
        {
            // Bare forearms; the left fist on the apron.
            var marks = new List<(int, int, char)> { (1, 11, 's'), (1, 12, 's') };
            var loose = new List<(int, int, char)>();
            switch (Blows[frame])
            {
                case Swing.Up:
                    // The hammer raised beside his head.
                    marks.AddRange(new[] { (12, 11, 's'), (12, 10, 's'), (12, 9, 'w'), (12, 8, 'w'), (12, 7, 'w'),
                        (11, 5, 'I'), (12, 5, 'I'), (13, 5, 'I'), (11, 6, 'i'), (12, 6, 'i'), (13, 6, 'i') });
                    break;
                case Swing.Mid:
                    // Coming down toward the viewer: the head just over the fist.
                    marks.AddRange(new[] { (12, 11, 's'), (12, 10, 'w'), (12, 8, 'I'), (13, 8, 'I'), (12, 9, 'i'), (13, 9, 'i') });
                    break;
                default:
                    // Down on the counter before him.
                    marks.AddRange(new[] { (12, 11, 's'), (11, 12, 's'), (10, 13, 'w'),
                        (8, 13, 'I'), (9, 13, 'I'), (10, 13, 'I'), (8, 14, 'i'), (9, 14, 'i'), (10, 14, 'i') });
                    if (Blows[frame] == Swing.Down)
                        loose.AddRange(new[] { (7, 12, 'F'), (11, 12, 'F'), (6, 13, 'f'), (12, 13, 'f') });
                    else
                        loose.AddRange(new[] { (6, 10, 'f'), (12, 10, 'f'), (5, 12, 'F'), (13, 12, 'F'), (9, 11, 'F') });
                    break;
            }
            return Paint(BlacksmithRows, color, frame, marks, loose, eyeRow: 5, legs: 15, breathes: false);
        }

        // ---- The fisher: the rod out to the side, the float bobbing and now and then pulled under ----

        static readonly string[] FisherRows =
        {
            "..................",
            "......yYYYYy......",
            "......YYYYYY......",
            ".....rrrrrrrr.....",
            "..yYYYYYYYYYYYYy..",
            "...yyyyyyyyyyyy...",
            "....hhSSSSSShh....",
            "....hhsesseshh....",
            ".....hsesseSh.....",
            "......ssssSS......",
            "....tTTTccTTTt....",
            "....tTTTccTTTt....",
            "....tTTTccTTTt....",
            ".....bbbBBbbb.....",
            ".....pPPppPPp.....",
            ".....pp....pp.....",
            ".....oo....oo.....",
        };

        static PixelImage Fisher(Rgba color, int frame)
        {
            // A bite at frames 4 and 5: the tip bends down and the float goes under.
            bool bite = frame == 4 || frame == 5;
            // The float rides the water a pixel up and down the rest of the time.
            int bob = !bite && frame % 4 >= 2 ? 1 : 0;
            int tip = bite ? 5 : 4;
            var marks = new List<(int, int, char)>
            {
                (3, 11, 't'), (3, 12, 's'),
                (14, 11, 's'), (15, 10, 'W'), (15, 9, 'w'), (16, 8, 'w'), (16, 7, 'w'), (16, 6, 'w'), (17, tip, 'w'),
            };
            if (!bite) marks.Add((16, 5, 'w'));
            var loose = new List<(int, int, char)>();
            int floatAt = bite ? 13 : 12 - bob;
            for (int y = tip + 1; y < floatAt; y++) loose.Add((17, y, 'l'));
            loose.Add((17, floatAt, 'R'));
            if (!bite) loose.Add((17, floatAt - 1, 'c'));
            return Paint(FisherRows, color, frame, marks, loose, eyeRow: 7, legs: 15, breathes: true);
        }

        // ---- The alchemist: bubbles rising from a flask held up, its brew glowing brighter now and then ----

        static readonly string[] AlchemistRows =
        {
            "..............",
            "........tU....",
            ".......TTt....",
            "......TTTt....",
            ".....TTTTt....",
            "....YYYYYYY...",
            ".tTTTTTTTTTTt.",
            "..nnssssssnn..",
            "..nnsesseSnn..",
            "..nNsesseSNn..",
            "..nNssssSSNn..",
            "..nTTTYTTTTn..",
            "..tTTTYTTTTt..",
            "..tTTTYTTTTt..",
            "..tTTTYTTTTt..",
            "...tTTYTTTt...",
            "...oo....oo...",
        };

        static PixelImage Alchemist(Rgba color, int frame)
        {
            bool bright = frame == 6 || frame == 7;
            char brew = bright ? 'G' : 'g';
            var marks = new List<(int, int, char)>
            {
                (1, 12, 't'), (1, 13, 's'),
                // The flask in her right hand: a glass neck over a round green body.
                (12, 10, 'N'), (12, 11, brew), (13, 11, brew), (12, 12, brew), (13, 12, 'G'), (11, 12, 's'), (11, 13, 's'),
            };
            var loose = new List<(int, int, char)>();
            // Two bubbles a little apart, each rising five pixels over its five frames.
            foreach (int start in new[] { 0, 5 })
            {
                int age = frame - start;
                if (age < 0 || age >= 5) continue;
                loose.Add((age % 2 == 0 ? 12 : 13, 9 - age, age < 3 ? 'G' : 'g'));
            }
            return Paint(AlchemistRows, color, frame, marks, loose, eyeRow: 8, legs: 16, breathes: true);
        }

        // ---- Painting ----

        // The rows with the marks over them, in the person's colours (T, t and U their clothes), blinking at frame 10
        // (eyeRow is the upper row of the eyes) and breathing in at frames 7 and 8 (the rows over the legs a pixel up); outlined, and then the loose marks,
        // things in the air or glowing, drawn over the outline without one of their own.

        // ---- The people of the story (Docs/Story.md, 2026-11-10): one drawing a look in characters.csv, clothes in
        // their colour, breathing and blinking like the rest. A look not drawn here is a villager. ----

        public static readonly string[] Looks = { "queen", "hero", "elder", "girl", "demon", "guard", "villager" };
        static readonly string[] QueenLook =
        {
            "..............",
            "....Y.YY.Y....",
            "....YYrYYY....",
            "...HHHHHHHH...",
            "..HHssssssHH..",
            "..HHsesseSHH..",
            "..HHssssSSHH..",
            "..HHHssssHHH..",
            "..HtTTYYTTtH..",
            "..tTTTYYTTTt..",
            "..tTTTYYTTTt..",
            "..sTTTTTTTTs..",
            "..tTTTTTTTTt..",
            ".tTTTTTTTTTTt.",
            ".tTTTTTTTTTTt.",
            ".tYYYYYYYYYYt.",
            "..............",
        };
        static readonly string[] HeroLook =
        {
            "..............",
            "....hhhhhh....",
            "...hhHHHHhh...",
            "...hssssssh...",
            "...sesssess...",
            "...ssssssSs...",
            "....sSSSSs....",
            "..TTIIIIIITT..",
            ".TTiIIIIIIiTT.",
            ".TtsIIBBIIstT.",
            ".Tt.IibbiI.tT.",
            ".Tt.IIIIII.tT.",
            ".T..pppppp..T.",
            "....pp..pp....",
            "....pp..pp....",
            "....oo..oo....",
            "..............",
        };
        static readonly string[] ElderLook =
        {
            "..............",
            "....ssssss....",
            "...sssssssS...",
            "..nssssssssn..",
            "..nsesssessn..",
            "..nnssssssnn..",
            "...nnnnnnnn...",
            "..TTnnnnnnTT..",
            ".wTTTnnnnTTT..",
            ".wtTTTnnTTTt..",
            ".wsTTTTTTTTs..",
            ".wtTTTTTTTTt..",
            ".w.tTTTTTTt...",
            ".w.tTTTTTTt...",
            ".w.tTTTTTTt...",
            ".w..oo..oo....",
            "..............",
        };
        static readonly string[] GirlLook =
        {
            "..............",
            "..............",
            "..............",
            "....PPPPPP....",
            "...PPPPPPPP...",
            "..PPssssssPP..",
            "..PPssssssPP..",
            "..PPsesseSPP..",
            "..PPssssSSPP..",
            "..PP.sSSs.PP..",
            "...tTTTTTTt...",
            "..sTTTUUTTTs..",
            "...tTTTTTTt...",
            "..tTTTTTTTTt..",
            "..tTTTTTTTTt..",
            "....ss..ss....",
            "....oo..oo....",
        };
        static readonly string[] DemonLook =
        {
            "..............",
            "..N........N..",
            "..cN......Nc..",
            "...cNTTTTNc...",
            "...TTTTTTTT...",
            "...TRRTTRRT...",
            "...TTTTTTtT...",
            "....TttttT....",
            "..pPPPPPPPPp..",
            ".pPPPPYYPPPPp.",
            ".pTPPPPPPPPTp.",
            ".p.PPPPPPPP.p.",
            "...PPPPPPPP...",
            "...PPPPPPPP...",
            "...PP....PP...",
            "...oo....oo...",
            "..............",
        };
        static readonly string[] GuardLook =
        {
            "............I.",
            "...iiiiii..IIw",
            "..iIIIIIIi..w.",
            "..issssssi..w.",
            "...sesssess.w.",
            "...ssssssSs.w.",
            "....sSSSSs..w.",
            "..IITTTTTTIIw.",
            ".IiTTTYYTTTiw.",
            ".IsTTTYYTTTsw.",
            ".Ii.TTTTTT..w.",
            "....TTTTTT..w.",
            "....pppppp..w.",
            "....pp..pp..w.",
            "....pp..pp..w.",
            "....oo..oo....",
            "..............",
        };
        static readonly string[] VillagerLook =
        {
            "..............",
            "....hhhhhh....",
            "...hhhhhhhh...",
            "...hssssssh...",
            "...sesssess...",
            "...ssssssSs...",
            "....sSSSSs....",
            "...tTTTTTTt...",
            "..tTTccccTTt..",
            "..sTTccccTTs..",
            "..tTTccccTTt..",
            "...TTccccTT...",
            "...TTTTTTTT...",
            "....pp..pp....",
            "....pp..pp....",
            "....oo..oo....",
            "..............",
        };

        // The person of the story in their look and colour, at a frame of the loop.
        public static PixelImage Look(string look, Rgba color, int frame)
        {
            frame = (frame % Frames + Frames) % Frames;
            var (rows, eyeRow) = look switch
            {
                "queen" => (QueenLook, 5),
                "hero" => (HeroLook, 4),
                "elder" => (ElderLook, 4),
                "girl" => (GirlLook, 7),
                "demon" => (DemonLook, 5),
                "guard" => (GuardLook, 4),
                _ => (VillagerLook, 4),
            };
            return Paint(rows, color, frame, System.Array.Empty<(int, int, char)>(), System.Array.Empty<(int, int, char)>(), eyeRow, legs: 16, breathes: true);
        }

        static PixelImage Paint(string[] rows, Rgba color, int frame, IEnumerable<(int X, int Y, char C)> marks, IEnumerable<(int X, int Y, char C)> loose,
            int eyeRow, int legs, bool breathes)
        {
            var ramp = Ramp.Of(color);
            var colors = new Dictionary<char, Rgba>(Colors) { ['T'] = ramp.Main, ['t'] = ramp.Shade, ['U'] = ramp.Light };
            var grid = new char[rows.Length][];
            for (int y = 0; y < rows.Length; y++) grid[y] = rows[y].ToCharArray();
            foreach (var (x, y, c) in marks) grid[y][x] = c;
            // A blink leaves the lower half of each eye (all of an eye one pixel tall is gone).
            if (frame == 10)
            {
                for (int x = 0; x < grid[eyeRow].Length; x++) if (grid[eyeRow][x] == 'e') grid[eyeRow][x] = 's';
            }
            var lines = new string[grid.Length];
            for (int y = 0; y < grid.Length; y++) lines[y] = new string(grid[y]);
            var image = PixelImage.FromRows(colors, lines);
            if (breathes && (frame == 7 || frame == 8))
            {
                var lifted = image.Copy();
                for (int y = 1; y < legs; y++)
                {
                    for (int x = 0; x < image.Width; x++) lifted[x, y - 1] = image[x, y];
                }
                image = lifted;
            }
            image = image.Outlined(Rgba.Hex("#140E10"));
            foreach (var (x, y, c) in loose) image[x + 1, y + 1] = colors[c];
            return image;
        }
    }
}
