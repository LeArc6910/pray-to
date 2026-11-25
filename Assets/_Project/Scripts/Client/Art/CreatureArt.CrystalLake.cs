using System;
using System.Collections.Generic;

namespace Game.Client
{
    // The crystal cave's and the underground lake's monsters (2026-11-25, Docs/BiomeRework.md B3b), in the creature style
    // of the others and at their size class (MonsterSizes: S 12~14 pixels, M 14~18, L 24~32, XL 40~56 with the outline):
    // the crystal mimic, the crystal shooter, the prism wisp, the tentacle, the mud slime and the tide chief are grids;
    // the crystal tortoise and the great crab (L) and the geode golem and the moss turtle (XL) are laid out from parts
    // like the other biomes' big ones (CreatureArt.Biomes.cs, CreatureArt.IceSand.cs). Standing dormant
    // (World.IsDormant) a mimic is a clump of crystals with two dim eyes in its base, the golem a boulder of grey stone
    // and the turtle an island of moss (Dormant in CreatureArt.IceSand.cs asks CrystalLakeDormant).
    static partial class CreatureArt
    {
        // A crystal mimic (M): a squat slate body with a clump of pink and sky crystals on its back, an eye and a wide
        // maw of white teeth in front, and six thin jointed legs that scuttle between the frames.
        static readonly string[][] CrystalMimic =
        {
            new[]
            {
                ".....c...p......",
                "....cC..pP..c...",
                "...cCC.pPP.cC...",
                "..cCCCpPPPcCC...",
                ".LLLLLLLLLLLLh..",
                "LMMMMMMMMMMeMhh.",
                "LMMMMMMMMMMMrhrh",
                "mMMMMMMMMMMMrrrr",
                "mmMMMMMMMMMMrhrh",
                ".mmmmmmmmmmmhh..",
                "..K..K..K..K.K..",
                ".K..K..K..K...K.",
                "K..K..K..K.....K",
            },
            new[]
            {
                ".....c...p......",
                "....cC..pP..c...",
                "...cCC.pPP.cC...",
                "..cCCCpPPPcCC...",
                ".LLLLLLLLLLLLh..",
                "LMMMMMMMMMMeMhhh",
                "LMMMMMMMMMMMrrhh",
                "mMMMMMMMMMMMrhhh",
                "mmMMMMMMMMMMhhh.",
                ".mmmmmmmmmmmm...",
                "..K..K..K..K.K..",
                "..K..K..K..K..K.",
                "..K..K..K..K..K.",
            },
        };

        // The same mimic standing still as a clump of crystals on a slate base: the crystals of a cluster, and two dim
        // eyes in the base for whoever looks closely (Docs/BiomeRework.md: a fair tell, as the sand's falling stream is).
        static readonly string[] MimicCluster =
        {
            "......c.........",
            ".....cC...p.....",
            "..p..cC..pP.....",
            "..pP.cCC.pP..c..",
            ".pPP.cCC.pPP.cC.",
            ".pPPcCCCCpPPcCC.",
            "dmmmmmmmmmmmmmmd",
            ".dmmmkmmmmkmmmd.",
            "..dddddddddddd..",
        };

        // A crystal shooter (S, the thorn cap of the crystal cave): a slate stalk with an eye under a faceted cap that
        // bristles with crystal thorns; the thorns glint in turn between the frames.
        static readonly string[][] CrystalShooter =
        {
            new[]
            {
                "....c..p....",
                "..p.cC.pP.c.",
                "..pPcCcpPcC.",
                ".LLLLLLLLLm.",
                "LMMMMMMMMMMm",
                ".mmmmmmmmmm.",
                "....LMMm....",
                "....LeMm....",
                "....LMMm....",
                "....LMMm....",
                "...dLMMmd...",
                "..ddddddd...",
            },
            new[]
            {
                "....w..p....",
                "..p.cC.pP.c.",
                "..pPcCcpPcC.",
                ".LLLLLLLLLm.",
                "LMMMMMMMMMMm",
                ".mmmmmmmmmm.",
                "....LMMm....",
                "....LeMm....",
                "....LMMm....",
                "....LMMm....",
                "...dLMMmd...",
                "..ddddddd...",
            },
        };

        // A prism wisp (S, an elite): a floating prism of pale crystal under a crown of small prisms, two eyes in it and a
        // band of broken light across its middle, sparks of the colours falling from its point; the crown and the
        // sparks shift between the frames.
        static readonly string[][] PrismWisp =
        {
            new[]
            {
                "..B..R..Y...",
                "..BVROYY....",
                "...LLLLL....",
                "..LwwMMMm...",
                ".LwMMMMMMm..",
                ".LMeMMMeMm.B",
                ".MMMMMMMMMmV",
                "RROOYYGGBBV.",
                "..MMMMMMMm..",
                "...MMMMMm...",
                "....mMMm....",
                ".....mm.....",
                "......d.....",
                "....Y...B...",
                "..R....G....",
            },
            new[]
            {
                "...B..R..Y..",
                "...BVROYY...",
                "...LLLLL....",
                "..LwwMMMm...",
                ".LwMMMMMMm..",
                "BLMeMMMeMm..",
                "VMMMMMMMMMm.",
                ".RROOYYGGBBV",
                "..MMMMMMMm..",
                "...MMMMMm...",
                "....mMMm....",
                ".....mm.....",
                "......d.....",
                "...G...R....",
                ".....Y...B..",
            },
        };

        // A tentacle (M): it rises in an S out of a ring of water, suckers down its inner side, its tip curled back; it
        // sways between the frames. Seen only when it comes up: under the water it is ripples (World.UnderWater).
        static readonly string[][] Tentacle =
        {
            new[]
            {
                ".........LLL....",
                "........LMMMm...",
                ".......LMhMMm...",
                ".......LMMmdm...",
                "......LMMm.dd...",
                ".....LMMm.......",
                ".....LMhm.......",
                "....LMMm........",
                "....LMhMm.......",
                "....LMMMm.......",
                ".....LMhMm......",
                ".....LMMMm......",
                "......LMhMm.....",
                "......LMMMm.....",
                "...WWLMMMMmWW...",
                ".WwWbbbbbbbbWwW.",
                "WbbbbbbbbbbbbbbW",
                ".WWWWWWWWWWWWWW.",
            },
            new[]
            {
                "..........LLL...",
                ".........LMMMm..",
                "........LMhMMm..",
                "........LMMmdm..",
                ".......LMMm.dd..",
                "......LMMm......",
                ".....LMhm.......",
                ".....LMMm.......",
                "....LMhMm.......",
                "....LMMMm.......",
                "....LMhMm.......",
                ".....LMMMm......",
                ".....LMhMm......",
                "......LMMMm.....",
                "..WWWLMMMMmWW...",
                ".WwWbbbbbbbbWwW.",
                "WbbbbbbbbbbbbbbW",
                ".WWWWWWWWWWWWWW.",
            },
        };

        // A mud slime (M, the slime of the lake): a slump of brown mud with pebbles in it and a tuft of reeds on top,
        // drips running off its foot; it settles between the frames.
        static readonly string[][] MudSlime =
        {
            new[]
            {
                "....g..G......",
                "....g.Gg......",
                ".....gG.......",
                "....LLLLLL....",
                "..LLMMMMMMm...",
                ".LMwMMMoMMMm..",
                ".LMMMMMMMMMm..",
                "MMMeMMoMMeMMm.",
                "MMMeMMMMMeMmm.",
                "mMMMMMoMMMMmm.",
                "mmmMMMMMMMmmd.",
                ".ddmmmmmmmddd.",
                "..d..d...d....",
            },
            new[]
            {
                "..............",
                ".....g..G.....",
                ".....g.Gg.....",
                "....LLgGLL....",
                "..LLMMMMMMm...",
                ".LMwMMMoMMMm..",
                "MMMMMMMMMMMMm.",
                "MMMeMMoMMeMMm.",
                "MMMeMMMMMeMmm.",
                "mMMMMMoMMMMmm.",
                "mmmMMMMMMMmmd.",
                "dddmmmmmmmdddd",
                "...d...d...d..",
            },
        };

        // A tide chief (S, an elite): the tide wisp's jelly crowned with coral, a glowing heart in its bell and long
        // trailing threads; the threads drift between the frames.
        static readonly string[][] TideChief =
        {
            new[]
            {
                "..k.K..k....",
                ".kKk.kKk....",
                "..LLLLLLL...",
                ".LMMMMMMMLL.",
                "LMwMMMMMwMML",
                "LMMMMooMMMML",
                "LMeMMooMMeML",
                ".mMMMMMMMMm.",
                "..mmmmmmmm..",
                "..h.hh.h.h..",
                ".h..h.h..h..",
                "..h..h.h..h.",
                ".h..h...h...",
                "..h...h....h",
                "...h.....h..",
            },
            new[]
            {
                "..k.K..k....",
                ".kKk.kKk....",
                "..LLLLLLL...",
                ".LMMMMMMMLL.",
                "LMwMMMMMwMML",
                "LMMMMooMMMML",
                "LMeMMooMMeML",
                ".mMMMMMMMMm.",
                "..mmmmmmmm..",
                "..h.hh.h.h..",
                "..h.h..h.h..",
                ".h..h.h..h..",
                "..h..h..h...",
                ".h..h....h..",
                "..h....h....",
            },
        };

        // The colours the two biomes' kinds add to their ramp.
        static readonly Dictionary<string, Dictionary<char, Rgba>> CrystalLakeExtras = new Dictionary<string, Dictionary<char, Rgba>>
        {
            // Pink and sky crystals on its back, white teeth in a dark maw, dark jointed legs; the dim eyes of its base.
            ["crystal_mimic"] = new Dictionary<char, Rgba>
            {
                ['p'] = Rgba.Hex("#FF9ADC"),
                ['P'] = Rgba.Hex("#C8509E"),
                ['c'] = Rgba.Hex("#9AF6FF"),
                ['C'] = Rgba.Hex("#3AA8C8"),
                ['h'] = Rgba.Hex("#F4F0FF"),
                ['r'] = Rgba.Hex("#3A1030"),
                ['K'] = Rgba.Hex("#4A5266"),
                ['k'] = Rgba.Hex("#C86AA0"),
            },
            ["crystal_shooter"] = new Dictionary<char, Rgba>
            {
                ['p'] = Rgba.Hex("#FF9ADC"),
                ['P'] = Rgba.Hex("#C8509E"),
                ['c'] = Rgba.Hex("#C8FCFF"),
                ['C'] = Rgba.Hex("#3AA8C8"),
            },
            // A crown of small prisms and a band of broken light in the same six colours, sparks of it (sixteen colours at most).
            ["prism_wisp"] = new Dictionary<char, Rgba>
            {
                ['Y'] = Rgba.Hex("#FFE070"),
                ['R'] = Rgba.Hex("#FF6A7A"),
                ['O'] = Rgba.Hex("#FFB050"),
                ['G'] = Rgba.Hex("#8AF07A"),
                ['B'] = Rgba.Hex("#6AC8FF"),
                ['V'] = Rgba.Hex("#B07AFF"),
            },
            // Pale suckers; the water it rises out of, with white foam.
            ["tentacle"] = new Dictionary<char, Rgba>
            {
                ['h'] = Rgba.Hex("#F0C8C0"),
                ['W'] = Rgba.Hex("#6FD8CC"),
                ['w'] = Rgba.Hex("#F0FFFC"),
                ['b'] = Rgba.Hex("#1E5A5E"),
            },
            // Green reeds, dark pebbles.
            ["mud_slime"] = new Dictionary<char, Rgba>
            {
                ['g'] = Rgba.Hex("#8AB040"),
                ['G'] = Rgba.Hex("#4E6A2A"),
                ['o'] = Rgba.Hex("#3E3424"),
            },
            // A coral crown, a glowing heart, pale threads.
            ["tide_chief"] = new Dictionary<char, Rgba>
            {
                ['k'] = Rgba.Hex("#FF8A8A"),
                ['K'] = Rgba.Hex("#C84A5A"),
                ['o'] = Rgba.Hex("#F0FFF8"),
                ['h'] = Rgba.Hex("#A8F4EC"),
            },
            // The tortoise: crystal plates on a slate shell, a bright crystal brow over its head (the face that turns
            // shots), a horn beak, dark legs.
            ["crystal_tortoise"] = new Dictionary<char, Rgba>
            {
                ['p'] = Rgba.Hex("#FF9ADC"),
                ['P'] = Rgba.Hex("#C8509E"),
                ['c'] = Rgba.Hex("#9AF6FF"),
                ['C'] = Rgba.Hex("#3AA8C8"),
                ['h'] = Rgba.Hex("#D8D0C0"),
                ['K'] = Rgba.Hex("#2E3240"),
                ['s'] = Rgba.Hex("#5A6070"),
                ['S'] = Rgba.Hex("#8A92A8"),
            },
            // The great crab: barnacles and moss on its back, a dark red claw tip, stalked eyes.
            ["great_crab"] = new Dictionary<char, Rgba>
            {
                ['h'] = Rgba.Hex("#E8E4D4"),
                ['g'] = Rgba.Hex("#7A9A40"),
                ['r'] = Rgba.Hex("#A8383A"),
                ['R'] = Rgba.Hex("#E86A5A"),
                ['K'] = Rgba.Hex("#1E3A44"),
            },
            // The geode golem: the violet hollow of its chest lined with pink and sky crystals round a white heart,
            // crystal fists and shoulders, eyes that glow pink, cracks.
            ["geode_golem"] = new Dictionary<char, Rgba>
            {
                ['v'] = Rgba.Hex("#4A2A6A"),
                ['V'] = Rgba.Hex("#8A5AC8"),
                ['p'] = Rgba.Hex("#FF9ADC"),
                ['P'] = Rgba.Hex("#C8509E"),
                ['c'] = Rgba.Hex("#9AF6FF"),
                ['C'] = Rgba.Hex("#3AA8C8"),
                ['g'] = Rgba.Hex("#FF8AD8"),
                ['k'] = Rgba.Hex("#3A3448"),
            },
            // The moss turtle: moss and its darker tufts on the shell, reeds, the glowing lamps of moss (its name) and
            // their stalks, a horn beak, dark plates.
            ["moss_turtle"] = new Dictionary<char, Rgba>
            {
                ['g'] = Rgba.Hex("#8AC050"),
                ['G'] = Rgba.Hex("#4A7A2E"),
                ['y'] = Rgba.Hex("#E8FF9A"),
                ['Y'] = Rgba.Hex("#B8E05A"),
                ['t'] = Rgba.Hex("#3E5A2A"),
                ['h'] = Rgba.Hex("#C8B890"),
                ['s'] = Rgba.Hex("#5A5038"),
                ['S'] = Rgba.Hex("#7E7050"),
                ['K'] = Rgba.Hex("#2A3420"),
            },
        };

        // The small grids of the two biomes' kinds and variants, or null.
        static string[][] CrystalLakeFrames(string id) => id switch
        {
            "crystal_mimic" => CrystalMimic,
            "crystal_shooter" => CrystalShooter,
            "prism_wisp" => PrismWisp,
            "tentacle" => Tentacle,
            "mud_slime" => MudSlime,
            "tide_chief" => TideChief,
            _ => null,
        };

        // ---- The big ones ----

        // A crystal tortoise (L): a low slate shell plated with big crystals that stand up from it in pink and sky, a
        // bright crystal brow over the front of the shell (the face that turns shots), a heavy head with a horn beak and
        // four stumpy legs that step between the frames. About 30 by 21.
        static string[] CrystalTortoise(int frame)
        {
            var g = Canvas(31, 22);
            // The legs: the far pair dark, the near pair lit, stepping.
            foreach (var (x0, far) in new[] { (6, true), (19, true), (4, false), (17, false) })
            {
                int lift = (x0 < 10) == (frame == 0) ? 1 : 0;
                for (int y = 17; y <= 21 - lift; y++)
                    for (int x = x0; x <= x0 + 3; x++) Set(g, y, x, far ? 'd' : y >= 21 - lift ? 'K' : x == x0 ? 'L' : x == x0 + 3 ? 'm' : 'M');
            }
            // The shell: a broad low dome, its rim a darker band of plates.
            Dome(g, 1, 8, 25, 11, 'S', 's', 'm', 'd');
            for (int x = 2; x <= 24; x++) Set(g, 17, x, x % 3 == 0 ? 'd' : 'm');
            for (int x = 3; x <= 23; x += 5) Stroke(g, x, 11, x + 1, 16, 'd');
            // The crystals standing up from the shell: tall in the middle, leaning out at the sides, pink and sky by turns.
            var crystals = new (int X, int Base, int H, bool Pink)[] { (5, 11, 4, false), (9, 9, 6, true), (13, 8, 8, false), (17, 9, 6, true), (21, 11, 4, false) };
            foreach (var (x, b, h, pink) in crystals)
            {
                char light = pink ? 'p' : 'c', dark = pink ? 'P' : 'C';
                for (int i = 0; i < h; i++)
                {
                    int y = b - i;
                    int w = i < h - 2 ? 1 : 0;
                    Set(g, y, x, light);
                    if (w > 0) Set(g, y, x + 1, dark);
                    if (i < h / 2) Set(g, y, x - 1, light);
                }
                Set(g, b - h, x, 'w');
            }
            // The bright brow of crystal over the front of the shell.
            for (int y = 10; y <= 16; y++) Set(g, y, 25 + (y < 12 ? 0 : 1), y % 2 == 0 ? 'c' : 'C');
            Set(g, 9, 25, 'w');
            // The head: heavy, low, forward, a horn beak and an eye under the brow.
            Oval(g, 27, 15 + (frame == 1 ? 1 : 0), 3, 3, 'M');
            int hy = 15 + (frame == 1 ? 1 : 0);
            Set(g, hy - 3, 26, 'L');
            Set(g, hy - 2, 25, 'L');
            Set(g, hy - 1, 27, 'e');
            Set(g, hy - 2, 27, 'K');
            Set(g, hy, 30, 'h');
            Set(g, hy + 1, 30, 'h');
            Set(g, hy + 1, 29, 'h');
            Set(g, hy + 2, 28, 'm');
            Set(g, hy + 2, 27, 'm');
            return Rows(g);
        }

        // A great crab (L): a wide carapace crusted with barnacles and moss, eyes on stalks, one great claw raised high
        // in front and a smaller one low, four thin jointed legs a side. The great claw opens and the legs step between
        // the frames. About 30 by 21.
        static string[] GreatCrab(int frame)
        {
            var g = Canvas(31, 22);
            // The legs: jointed, out from under the carapace to the floor, the far ones dark.
            for (int i = 0; i < 4; i++)
            {
                int x = 3 + i * 4 + ((i + frame) % 2), knee = 15;
                Stroke(g, x + 1, 13, x - 1, knee, 'd');
                Stroke(g, x - 1, knee, x - 2, 21, 'd');
                Stroke(g, x + 3, 14, x + 2, knee + 1, 'K');
                Stroke(g, x + 2, knee + 1, x + 1, 21, 'K');
            }
            // The carapace: a wide low dome, a dark rim, barnacles and moss on it.
            Dome(g, 1, 6, 22, 10);
            for (int x = 2; x <= 21; x++) Set(g, 15, x, x % 2 == 0 ? 'd' : 'm');
            foreach (var (x, y) in new[] { (6, 8), (11, 7), (15, 9), (8, 11), (18, 11) })
            {
                Set(g, y, x, 'h');
                Set(g, y, x + 1, 'h');
                Set(g, y - 1, x, 'h');
            }
            foreach (var (x, y) in new[] { (4, 9), (13, 8), (17, 7), (10, 12), (19, 13) }) Set(g, y, x, 'g');
            // The eyes on stalks.
            foreach (int x in new[] { 15, 18 })
            {
                Stroke(g, x, 6, x, 3, 'm');
                Set(g, 2, x, 'K');
                Set(g, 3, x, 'e');
            }
            // The small claw, low and forward.
            Stroke(g, 21, 13, 24, 15, 'M');
            Oval(g, 25, 15, 2, 1, 'M');
            Set(g, 15, 27, 'r');
            Set(g, 14, 27, 'R');
            // The great claw: an arm up from the front of the carapace to a big pincer held high, its jaws opening.
            Stroke(g, 21, 9, 24, 6, 'M');
            Stroke(g, 22, 10, 25, 7, 'm');
            Oval(g, 26, 5, 4, 3, 'M');
            Set(g, 3, 24, 'L');
            Set(g, 3, 25, 'L');
            Set(g, 4, 23, 'L');
            int open = frame == 0 ? 1 : 2;
            for (int x = 28; x <= 30; x++)
            {
                Set(g, 4 - open, x, x == 30 ? 'R' : 'r');
                Set(g, 6 + open, x, x == 30 ? 'R' : 'r');
                Set(g, 5, x, '.');
                if (open == 2) Set(g, 4, x, '.');
            }
            return Rows(g);
        }

        // The geode golem (XL): a hulk of grey stone whose chest is a geode split open, its violet hollow lined with
        // pink and sky crystals round a white heart; crystals jut from its shoulders, its fists are clusters of crystal,
        // its small head sunk between its shoulders has two eyes that glow pink. Cracks run through the stone. The fists
        // rise and the eyes flicker between the frames. About 48 by 46.
        static string[] GeodeGolem(int frame)
        {
            var g = Canvas(50, 48);
            int lift = frame;
            // The legs: two short pillars of stone.
            foreach (int x0 in new[] { 14, 28 })
                for (int y = 38; y <= 47; y++)
                    for (int x = x0; x <= x0 + 7; x++) Set(g, y, x, y == 47 ? 'd' : x == x0 ? 'L' : x == x0 + 7 ? 'm' : 'M');
            // The body: a great rounded boulder.
            Dome(g, 7, 10, 36, 30);
            for (int x = 9; x <= 40; x += 3) Set(g, 39, x, 'd');
            // The geode in its chest: a stone rim, the violet hollow, crystals pointing in, a white heart.
            Oval(g, 25, 25, 9, 8, 'L');
            Oval(g, 25, 25, 8, 7, 'v');
            Oval(g, 25, 25, 6, 5, 'V');
            foreach (var (dx, dy, pink) in new[] { (-7, 0, true), (7, 0, false), (0, -6, false), (0, 6, true), (-5, -4, false), (5, -4, true), (-5, 4, false), (5, 4, true) })
            {
                char light = pink ? 'p' : 'c', dark = pink ? 'P' : 'C';
                int sx = Math.Sign(dx), sy = Math.Sign(dy);
                Set(g, 25 + dy, 25 + dx, dark);
                Set(g, 25 + dy - sy, 25 + dx - sx, light);
                Set(g, 25 + dy - 2 * sy, 25 + dx - 2 * sx, light);
            }
            Oval(g, 25, 25, 2, 2, frame == 0 ? 'w' : 'p');
            Set(g, 25, 25, 'w');
            // Cracks in the stone round the geode.
            Stroke(g, 14, 18, 11, 24, 'k');
            Stroke(g, 11, 24, 12, 30, 'k');
            Stroke(g, 36, 19, 39, 23, 'k');
            Stroke(g, 33, 34, 36, 37, 'k');
            // Crystals jutting from the shoulders.
            foreach (var (x, h, pink) in new[] { (11, 7, true), (14, 9, false), (17, 6, true), (33, 6, false), (36, 9, true), (39, 7, false) })
            {
                char light = pink ? 'p' : 'c', dark = pink ? 'P' : 'C';
                for (int i = 0; i < h; i++)
                {
                    Set(g, 14 - i, x, light);
                    if (i < h - 2) Set(g, 14 - i, x + 1, dark);
                }
                Set(g, 14 - h, x, 'w');
            }
            // The arms: thick and hanging, fists of crystal, raised a little in the second frame.
            foreach (int x0 in new[] { 0, 42 })
            {
                for (int y = 17 - lift; y <= 33 - lift; y++)
                    for (int x = x0; x <= x0 + 7; x++) Set(g, y, x, x == x0 ? (x0 == 0 ? 'L' : 'M') : x == x0 + 7 ? 'm' : 'M');
                Stroke(g, x0 + 2, 22 - lift, x0 + 5, 26 - lift, 'k');
                int fy = 35 - lift;
                for (int y = fy - 1; y <= fy + 4; y++)
                    for (int x = x0 - 1; x <= x0 + 8; x++) Set(g, y, x, (x + y) % 3 == 0 ? 'C' : (x + y) % 3 == 1 ? 'c' : 'p');
                Set(g, fy - 2, x0 + 2, 'c');
                Set(g, fy - 2, x0 + 6, 'p');
                Set(g, fy - 3, x0 + 6, 'w');
            }
            // The head, sunk between the shoulders: a block of stone, two glowing eyes.
            Dome(g, 19, 2, 12, 10);
            char eye = frame == 0 ? 'g' : 'w';
            foreach (int x in new[] { 22, 23, 27, 28 }) Set(g, 7, x, eye);
            Stroke(g, 22, 9, 28, 9, 'd');
            return Rows(g);
        }

        // The moss turtle (XL): a great domed shell grown over with moss, its plates showing through, reeds standing in
        // it and lamps of moss glowing on stalks (its name); a broad head with a horn beak and a heavy eyelid stretched
        // out in front; four thick legs. The lamps swell and the head bobs between the frames. About 54 by 38.
        static string[] MossTurtle(int frame)
        {
            var g = Canvas(56, 40);
            // The legs: thick, the far ones dark, the near ones lit, stepping.
            foreach (var (x0, far) in new[] { (12, true), (34, true), (7, false), (29, false) })
            {
                int lift = (x0 < 20) == (frame == 0) ? 1 : 0;
                for (int y = 31; y <= 39 - lift; y++)
                    for (int x = x0; x <= x0 + 7; x++) Set(g, y, x, far ? 'd' : y >= 38 - lift ? 'K' : x == x0 ? 'L' : x == x0 + 7 ? 'm' : 'M');
            }
            // The shell: a high dome of brown plates under moss.
            Dome(g, 1, 10, 44, 22, 'S', 's', 'd', 'K');
            for (int x = 2; x <= 43; x++) Set(g, 31, x, x % 4 == 0 ? 'K' : 'd');
            // The plates' seams.
            foreach (int x in new[] { 10, 19, 28, 37 }) Stroke(g, x, 18, x + 1, 30, 'K');
            Stroke(g, 5, 22, 41, 22, 'K');
            // Moss over the top of the shell, thick, its tufts darker.
            for (int x = 3; x <= 42; x++)
            {
                for (int y = 9; y < 32; y++)
                {
                    if (g[y, x] == '.') continue;
                    int depth = 5 + (int)(2 * MathF.Sin(x * 0.7f)) + (x % 5 == 0 ? 2 : 0);
                    for (int k = 0; k < depth && y + k < 32; k++) Set(g, y + k, x, k == 0 ? 'g' : (x + k) % 3 == 0 ? 'G' : 'g');
                    break;
                }
            }
            // Reeds standing in the moss.
            foreach (int x in new[] { 8, 9, 30, 31, 33 })
            {
                int top = 4 + (x % 3);
                for (int y = top; y <= 11; y++) Set(g, y, x, y == top ? 'Y' : 't');
            }
            // The moss lamps: a stalk and a glowing bulb, swelling between the frames.
            foreach (var (x, top) in new[] { (15, 3), (23, 1), (38, 5) })
            {
                for (int y = top + 2; y <= 11; y++) Set(g, y, x, 't');
                Oval(g, x, top + 1, 1 + frame, 1 + frame, 'Y');
                Set(g, top + 1, x, 'y');
                Set(g, top, x, 'y');
            }
            // The head: broad, out in front on a thick neck, a horn beak, a heavy-lidded eye; it bobs.
            int hy = 22 + frame;
            for (int y = hy - 2; y <= hy + 4; y++)
                for (int x = 41; x <= 45; x++) Set(g, y, x, y == hy - 2 ? 'L' : y == hy + 4 ? 'm' : 'M');
            Oval(g, 48, hy, 6, 5, 'M');
            for (int x = 44; x <= 52; x++) Set(g, hy - 5, x, 'L');
            Set(g, hy - 4, 43, 'L');
            Set(g, hy - 2, 49, 'e');
            Set(g, hy - 3, 48, 'd');
            Set(g, hy - 3, 49, 'd');
            Set(g, hy - 3, 50, 'd');
            for (int y = hy; y <= hy + 3; y++) Set(g, y, 54 - (y - hy) / 2, 'h');
            Set(g, hy + 1, 52, 'h');
            Stroke(g, 46, hy + 3, 52, hy + 3, 'd');
            return Rows(g);
        }

        // The big kinds of the two biomes, or null.
        static string[][] CrystalLakeBigFrames(string id) => id switch
        {
            "crystal_tortoise" => Sides(CrystalTortoise(0), CrystalTortoise(1)),
            "great_crab" => Sides(GreatCrab(0), GreatCrab(1)),
            "geode_golem" => Sides(GeodeGolem(0), GeodeGolem(1)),
            "moss_turtle" => Sides(MossTurtle(0), MossTurtle(1)),
            _ => null,
        };

        // The frames without the columns empty in all of them at either side (a part laid one short of the canvas's
        // edge), so the picture is as wide as the body.
        static string[][] Sides(params string[][] frames)
        {
            bool Empty(int x) => Array.TrueForAll(frames, f => Array.TrueForAll(f, row => x >= row.Length || row[x] == '.'));
            int width = frames[0][0].Length, left = 0, right = width - 1;
            while (left < right && Empty(left)) left++;
            while (right > left && Empty(right)) right--;
            return Array.ConvertAll(frames, f => Array.ConvertAll(f, row => row.Substring(left, right - left + 1)));
        }

        // What a pose changes in the two biomes' colours: a mimic winding up flares its crystals, a prism wisp winding up
        // lights its band white-hot from frame to frame, and the tentacle's suckers redden.
        static void CrystalLakePose(string id, MonsterPose pose, int frame, Dictionary<char, Rgba> colors)
        {
            if (pose != MonsterPose.Windup) return;
            bool flare = frame % 2 == 0;
            switch (id)
            {
                case "crystal_mimic":
                    colors['p'] = Rgba.Hex(flare ? "#FFE0F4" : "#FF9ADC");
                    colors['c'] = Rgba.Hex(flare ? "#F0FFFF" : "#9AF6FF");
                    colors['r'] = Rgba.Hex("#7A1A50");
                    break;
                case "prism_wisp":
                    foreach (char c in "ROYGBV") colors[c] = flare ? Rgba.Hex("#FFFFFF") : colors[c];
                    break;
                case "tentacle":
                    colors['h'] = Rgba.Hex("#FF8A80");
                    break;
            }
        }

        // ---- Dormant ----

        // Whether a kind of these biomes is drawn still while it stands dormant (a mimic is crystals).
        static bool CrystalLakeSleeps(string id) => Mimics(id) || id == "geode_golem" || id == "moss_turtle";

        // Whether a kind stands as something else while dormant (World.Mimics: an elite one does not).
        public static bool Mimics(string id) => id == "crystal_mimic";

        static readonly Rgba Slate = Rgba.Hex("#7A7C88"), Moss = Rgba.Hex("#6E9A48"), MossTop = Rgba.Hex("#A8D070");

        // A dormant kind of these biomes, or null for another: a mimic is a clump of crystals (another picture, not its
        // body), the golem grey stone with its geode's light out, the turtle an island of moss with its lamps dim.
        static PixelImage CrystalLakeDormant(string id, PixelImage image)
        {
            if (id == "crystal_mimic")
            {
                var colors = RampColors(Ramp.Of(Rgba.Hex("#7C86A8")));
                foreach (var pair in CrystalLakeExtras[id]) colors[pair.Key] = pair.Value;
                return PixelImage.FromRows(colors, MimicCluster).Outlined(Ramp.Of(Rgba.Hex("#7C86A8")).Outline);
            }
            if (id != "geode_golem" && id != "moss_turtle") return null;
            var result = image.Copy();
            bool moss = id == "moss_turtle";
            for (int y = 0; y < result.Height; y++)
            {
                for (int x = 0; x < result.Width; x++)
                {
                    var c = result[x, y];
                    if (c.A == 0) continue;
                    float light = (c.R * 0.3f + c.G * 0.55f + c.B * 0.15f) / 255f;
                    Rgba shown = moss ? Moss.Scale(0.5f + 0.7f * light) : Slate.Scale(0.5f + 0.7f * light);
                    // Moss grows over its top edge.
                    if (moss && (y == 0 || result[x, y - 1].A == 0)) shown = MossTop;
                    result[x, y] = new Rgba(shown.R, shown.G, shown.B, c.A);
                }
            }
            return result;
        }
    }
}
