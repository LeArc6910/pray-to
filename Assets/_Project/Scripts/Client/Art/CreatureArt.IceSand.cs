using System;
using System.Collections.Generic;

namespace Game.Client
{
    // The ice cave's and the sand cave's monsters (2026-11-24, Docs/BiomeRework.md B2b), in the creature style of the
    // others and at their size class (MonsterSizes: S 12~14 pixels, M 14~18, L 24~32, XL 40~56 with the outline): the
    // frost wolf (M) and the bomb scarab (S) are grids; the yeti and the great worm (L) and the frost mammoth and the
    // temple guardian (XL) are laid out from parts like the first two biomes' big ones (CreatureArt.Biomes.cs). The
    // variants (a frost beetle, a sandstone beetle) are the beetle in their own colours; the frost lord is the frost
    // wisp crowned with ice and the scorpion queen the scorpion with three stings and a gold crown. A mid-boss standing
    // dormant (World.IsDormant) is drawn as a statue: the guardian in grey stone with its eyes out, the mammoth under
    // a crust of ice (Dormant).
    static partial class CreatureArt
    {
        // A frost wolf (M): long and low, a raised brush of a tail tipped with frost, pricked ears, a pale belly and
        // muzzle. It trots: the legs cross between the frames.
        static readonly string[][] FrostWolf =
        {
            new[]
            {
                "............L.L.",
                "...........LMLMm",
                "ff........LMMeMk",
                ".fL.LLLLLLMMMMbb",
                "..MLMMMMMMMMMbb.",
                "..mMMMMMMMMMbb..",
                "...mmMMMMMmmb...",
                "....bbbbbbbbm...",
                "....K.K...K.K...",
                "...K..K...K..K..",
                "...d..d...d..d..",
            },
            new[]
            {
                "............L.L.",
                "...........LMLMm",
                ".f........LMMeMk",
                "ffL.LLLLLLMMMMbb",
                "..MLMMMMMMMMMbb.",
                "..mMMMMMMMMMbb..",
                "...mmMMMMMmmb...",
                "....bbbbbbbbm...",
                "....K.K...K.K...",
                "....KK.....KK...",
                "....dd.....dd...",
            },
        };

        // A bomb scarab (S): a high domed shell split by a gold seam over a swollen glowing abdomen at its rear, a fuse
        // that sparks on top of it and a curved horn in front; its legs scuttle and the spark jumps between the frames.
        // Winding up to go off, the abdomen and the spark flare and pulse (IceSandPose).
        static readonly string[][] Scarab =
        {
            new[]
            {
                "f..........h",
                ".F.LLLLL..hh",
                "..LwwMMMLKh.",
                "gLwwMMyMMKKK",
                "GgMMMMyMMKeK",
                "GGgMMMyMMmKK",
                "GgmMMMyMmmK.",
                "ggyyyyyyyyK.",
                "..K.K..K.K..",
                ".K..K.K..K..",
            },
            new[]
            {
                ".f.........h",
                "F..LLLLL..hh",
                "..LwwMMMLKh.",
                "gLwwMMyMMKKK",
                "GgMMMMyMMKeK",
                "GGgMMMyMMmKK",
                "GgmMMMyMmmK.",
                "ggyyyyyyyyK.",
                ".K..K.K..K..",
                "..K.K..K.K..",
            },
        };

        // A frost lord (S, an elite): taller than a frost wisp, a crown of ice shards over a hood, eyes that glow, a
        // robe of frost that trails off into mist behind it, and two shards of ice orbiting it (they swap sides between
        // the frames).
        static readonly string[][] FrostLord =
        {
            new[]
            {
                "..C..c..C...",
                "..CcCcCcC...",
                "...cCCCc....",
                "..LLLLLLm...",
                ".LLddddMm...",
                ".LdedddeMm.c",
                ".LMddddMmm.C",
                "c.LMMMMMMm..",
                "C.LMwMMMMmm.",
                ".LMMMMMMMmm.",
                ".LMMMMMMMmd.",
                "LMMMMMMMMmmd",
                "LMmMMmMMmMmd",
                ".m.mf.m.dm..",
                "...fff......",
                ".ffff.......",
                "ff..........",
            },
            new[]
            {
                "..C..c..C...",
                "..CcCcCcC...",
                "...cCCCc....",
                "..LLLLLLm..c",
                ".LLddddMm..C",
                ".LdedddeMm..",
                ".LMddddMmm..",
                "..LMMMMMMm..",
                "..LMwMMMMmm.",
                "cLMMMMMMMmm.",
                "CLMMMMMMMmd.",
                "LMMMMMMMMmmd",
                "LMmMMmMMmMmd",
                ".m.mf.m.dm..",
                "..fff.......",
                "..ffff......",
                ".ff.f.......",
            },
        };

        // A beetle of the ice (M): the cave beetle's make, a high segmented shell with a lit ridge and frost crystals
        // growing out of it, a dark head with an eye and mandibles, three legs on the near side that walk.
        static readonly string[][] FrostBeetle =
        {
            new[]
            {
                "...c..C.c.......",
                "...CLLLLCL......",
                "..LLwwLLMMLL....",
                ".LMwMMLMMdMMmKK.",
                "LMMMMMLMMdMMKKKh",
                "LMMMMMdMMdMMKrK.",
                "MMMMMMdMMdMMKKKh",
                "mMMMMMdMMdMmmKKh",
                ".mmmmmmmmmmmdK..",
                "..dddddddddd....",
                "..K...K...K.....",
                ".K...K...K......",
                "K...K...K.......",
            },
            new[]
            {
                "...c..C.c.......",
                "...CLLLLCL......",
                "..LLwwLLMMLL....",
                ".LMwMMLMMdMMmKK.",
                "LMMMMMLMMdMMKKKh",
                "LMMMMMdMMdMMKrK.",
                "MMMMMMdMMdMMKKKh",
                "mMMMMMdMMdMmmKKh",
                ".mmmmmmmmmmmdK..",
                "..dddddddddd....",
                "...K...K...K....",
                "....K...K...K...",
                ".....K...K...K..",
            },
        };

        // A beetle of the sandstone (M): the same make, its shell banded in pale strata, half sunk in a drift of sand
        // that hides its legs (it lives under the sand between its charges); grains shift in the drift as it walks.
        static readonly string[][] SandBeetle =
        {
            new[]
            {
                "....LLLLLL......",
                "..LLwwLLMMLL....",
                ".LMwMMLMMdMMmKK.",
                "LSSSSSSSSSSSKKKh",
                "LMMMMMdMMdMMKrK.",
                "SSSSSSSSSSSSKKKh",
                "mMMMMMdMMdMmmKKh",
                ".mmmmmmmmmmmdK..",
                "..K..sK...K.s...",
                "ssssSsssssSsssss",
            },
            new[]
            {
                "....LLLLLL......",
                "..LLwwLLMMLL....",
                ".LMwMMLMMdMMmKK.",
                "LSSSSSSSSSSSKKKh",
                "LMMMMMdMMdMMKrK.",
                "SSSSSSSSSSSSKKKh",
                "mMMMMMdMMdMmmKKh",
                ".mmmmmmmmmmmdK..",
                "...K.s.K...K..s.",
                "sssSsssssSssssss",
            },
        };

        // A scorpion queen: three stings raised over her back and a gold crown on her head.
        static readonly string[][] QueenScorpion =
        {
            new[]
            {
                ".r...MMr...r....",
                ".M..M....M.M....",
                ".mM.M....mM.....",
                ".mM.mM..mM......",
                "..mM.LLLLLL..y.y",
                "...MLMwwMMMLmyyy",
                "...MMMMMMMMMMrKh",
                "...mMMMMMMMMMmKK",
                "....mmmmmmmmmKKh",
                "....K.K.K.K.K...",
                "...K.K.K.K.K....",
            },
            new[]
            {
                "r...MMr...r.....",
                "M..M....M.M.....",
                ".mM.M...mM......",
                ".mM.mM..mM......",
                "..mM.LLLLLL..y.y",
                "...MLMwwMMMLmyyy",
                "...MMMMMMMMMMrKh",
                "...mMMMMMMMMMmKK",
                "....mmmmmmmmmKKh",
                ".....K.K.K.K.K..",
                "....K.K.K.K.K...",
            },
        };

        // The colours the two biomes' kinds add to their ramp.
        static readonly Dictionary<string, Dictionary<char, Rgba>> IceSandExtras = new Dictionary<string, Dictionary<char, Rgba>>
        {
            // A white belly and muzzle, frost on the brush, a dark nose and paws.
            ["frost_wolf"] = new Dictionary<char, Rgba>
            {
                ['b'] = Rgba.Hex("#F2F8FF"),
                ['f'] = Rgba.Hex("#FFFFFF"),
                ['k'] = Rgba.Hex("#1E2A36"),
                ['K'] = Rgba.Hex("#6E8AA4"),
            },
            // A gold seam and horn, a swollen abdomen glowing like a coal, a fuse that sparks.
            ["bomb_scarab"] = new Dictionary<char, Rgba>
            {
                ['y'] = Rgba.Hex("#E8C050"),
                ['K'] = Rgba.Hex("#22282E"),
                ['h'] = Rgba.Hex("#E8C050"),
                ['g'] = Rgba.Hex("#FF6A20"),
                ['G'] = Rgba.Hex("#FFC050"),
                ['f'] = Rgba.Hex("#FFE890"),
                ['F'] = Rgba.Hex("#8A5A2A"),
            },
            // The beetle in frost: dark blue legs and head, a pale horn, a cold glint of an eye.
            ["frost_beetle"] = new Dictionary<char, Rgba>
            {
                ['K'] = Rgba.Hex("#2E4A62"),
                ['h'] = Rgba.Hex("#E8F8FF"),
                ['r'] = Rgba.Hex("#8FF0FF"),
                ['c'] = Rgba.Hex("#F0FCFF"),
                ['C'] = Rgba.Hex("#7FD8F8"),
            },
            // And in sandstone: brown legs, a bone horn, an amber eye.
            ["sandstone_beetle"] = new Dictionary<char, Rgba>
            {
                ['K'] = Rgba.Hex("#5A4028"),
                ['h'] = Rgba.Hex("#F0E0B8"),
                ['r'] = Rgba.Hex("#F0B040"),
                ['S'] = Rgba.Hex("#8E6A40"),
                ['s'] = Rgba.Hex("#D2AE7C"),
            },
            // A crown and orbiting shards of ice, eyes that glow white-blue, a robe trailing into pale mist.
            ["frost_lord"] = new Dictionary<char, Rgba>
            {
                ['c'] = Rgba.Hex("#F0FCFF"),
                ['C'] = Rgba.Hex("#6FC8F0"),
                ['e'] = Rgba.Hex("#E8FFFF"),
                ['f'] = Rgba.Hex("#C8E8F8"),
            },
            ["scorpion_queen"] = new Dictionary<char, Rgba>
            {
                ['K'] = Rgba.Hex("#4A1E10"),
                ['r'] = Rgba.Hex("#C8FF60"),
                ['h'] = Rgba.Hex("#F8C870"),
                ['y'] = Rgba.Hex("#FFD860"),
            },
            // A white yeti: a slate face, pale horns, dark claws.
            ["yeti"] = new Dictionary<char, Rgba>
            {
                ['f'] = Rgba.Hex("#7896B4"),
                ['F'] = Rgba.Hex("#4A6684"),
                ['h'] = Rgba.Hex("#C4CCD8"),
                ['K'] = Rgba.Hex("#34404E"),
            },
            // The great worm: a dark red maw, bone teeth, the sand it comes up out of.
            ["great_worm"] = new Dictionary<char, Rgba>
            {
                ['r'] = Rgba.Hex("#6A1E2A"),
                ['R'] = Rgba.Hex("#C8404A"),
                ['h'] = Rgba.Hex("#F0E6CC"),
                ['s'] = Rgba.Hex("#9C7E52"),
                ['S'] = Rgba.Hex("#C8A070"),
            },
            // The frost mammoth: ivory tusks, an ice crust on its back.
            ["frost_mammoth"] = new Dictionary<char, Rgba>
            {
                ['h'] = Rgba.Hex("#F4ECD8"),
                ['H'] = Rgba.Hex("#C8BC9C"),
                ['c'] = Rgba.Hex("#E4F8FF"),
                ['C'] = Rgba.Hex("#8CD0F0"),
                ['K'] = Rgba.Hex("#3A4652"),
            },
            // The temple guardian: a headdress of gold and lapis, a sun on its chest, eyes that glow, cracks.
            ["temple_guardian"] = new Dictionary<char, Rgba>
            {
                ['y'] = Rgba.Hex("#E0B048"),
                ['Y'] = Rgba.Hex("#A07828"),
                ['b'] = Rgba.Hex("#3A5A92"),
                ['g'] = Rgba.Hex("#7FF2FF"),
                ['k'] = Rgba.Hex("#6A5034"),
            },
        };

        // The small grids of the two biomes' kinds and variants, or null.
        static string[][] IceSandFrames(string id) => id switch
        {
            "frost_wolf" => FrostWolf,
            "bomb_scarab" => Scarab,
            "frost_beetle" => FrostBeetle,
            "sandstone_beetle" => SandBeetle,
            "frost_lord" => FrostLord,
            "scorpion_queen" => QueenScorpion,
            // The crystal cave's and the underground lake's (CreatureArt.CrystalLake.cs, B3b).
            _ => CrystalLakeFrames(id),
        };

        // ---- The big ones ----

        // A yeti (L): a hunched white bulk with a slate face and two short horns, long arms to fists on the floor, two
        // stumpy legs. The arms swing between the frames. About 28 by 27.
        static string[] Yeti(int frame)
        {
            var g = Canvas(30, 28);
            int swing = frame == 0 ? 0 : 1;
            // The legs, under the body.
            for (int leg = 0; leg < 2; leg++)
            {
                int x0 = leg == 0 ? 9 : 16, lift = leg == frame ? 1 : 0;
                for (int y = 22; y <= 27 - lift; y++)
                    for (int x = x0; x <= x0 + 4; x++) Set(g, y, x, y == 27 - lift ? 'K' : x == x0 + 4 ? 'm' : 'M');
            }
            // The far arm, behind the body: dark, its fist by the far foot.
            Oval(g, 24, 10, 3, 3, 'm');
            for (int y = 10; y <= 21; y++)
                for (int x = 24; x <= 26; x++) Set(g, y, x + (y > 15 ? 1 - swing : 0), x == 26 ? 'd' : 'm');
            Oval(g, 26 + (1 - swing), 22, 2, 2, 'm');
            Set(g, 24, 25 + (1 - swing), 'K');
            Set(g, 24, 27 + (1 - swing), 'K');
            // The body: a tall dome, its fur ragged at the hem, tufts lit on the near side.
            Dome(g, 5, 6, 20, 18);
            for (int x = 6; x <= 23; x += 2) Set(g, 24, x, 'm');
            foreach (var (x, y) in new[] { (9, 12), (11, 16), (13, 10), (16, 19), (20, 14), (18, 9) })
            {
                Set(g, y, x, 'm');
                Set(g, y + 1, x + 1, 'm');
            }
            // The near arm: a round shoulder, then thick, lit, hanging to a fist by the near foot.
            Oval(g, 5, 10, 3, 3, 'M');
            Set(g, 8, 4, 'L');
            Set(g, 8, 5, 'L');
            for (int y = 10; y <= 20; y++)
                for (int x = 2; x <= 5; x++) Set(g, y, x - (y > 15 ? swing : 0), x == 2 ? 'L' : x == 5 ? 'm' : 'M');
            Oval(g, 3 - swing, 22, 3, 2, 'L');
            for (int x = 1; x <= 5; x++) Set(g, 23, x - swing, 'M');
            Set(g, 24, 1 - swing, 'K');
            Set(g, 24, 3 - swing, 'K');
            Set(g, 24, 5 - swing, 'K');
            // The head, forward on the shoulders: white fur round a slate face, two eyes, a wide mouth, short horns.
            Oval(g, 18, 6, 6, 5, 'M');
            Dome(g, 12, 1, 13, 5);
            Oval(g, 19, 7, 4, 3, 'f');
            Set(g, 6, 17, 'e');
            Set(g, 6, 21, 'e');
            Stroke(g, 17, 9, 21, 9, 'F');
            Set(g, 10, 18, 'w');
            Set(g, 10, 20, 'w');
            Stroke(g, 13, 2, 12, 0, 'h');
            Stroke(g, 23, 2, 24, 0, 'h');
            return Rows(g);
        }

        // A great worm (L): a thick ringed body come up in an arch out of a drift of sand, its round maw ringed with
        // teeth turned forward. The maw opens and the body leans between the frames. About 30 by 24.
        static string[] GreatWorm(int frame)
        {
            var g = Canvas(30, 26);
            // The drift it comes up out of: a low mound, lit on top.
            for (int x = 0; x < 30; x++)
            {
                float t = (x - 14.5f) / 15f;
                int top = 26 - (int)MathF.Round(6 * MathF.Sqrt(Math.Max(0, 1 - t * t)));
                for (int y = top; y < 26; y++) Set(g, y, x, y == top ? 'S' : 's');
            }
            // The ringed body, out of the mound and bending forward to the head: segments laid from the mound up so the
            // higher ones lie on top, each with a dark ring under it and a light on its near shoulder.
            var segments = new (int X, int Y)[] { (11, 20), (11, 16), (12, 12), (15, 9) };
            for (int i = 0; i < segments.Length; i++)
            {
                var (x, y) = segments[i];
                y -= frame * (i >= 2 ? 1 : 0);
                Oval(g, x, y, 5, 4, 'M');
                for (int k = -4; k <= 4; k++) Set(g, y + 3, x + k, k < 2 ? 'm' : 'd');
                Set(g, y - 2, x - 3, 'L');
                Set(g, y - 1, x - 4, 'L');
                Set(g, y + 1, x + 4, 'm');
            }
            // The head: a broad end, a dark maw in front ringed with teeth.
            int hx = 20, hy = 7 - frame;
            Oval(g, hx, hy, 6, 5, 'M');
            for (int k = -5; k <= 2; k++) Set(g, hy - 4, hx + k, 'L');
            for (int k = -4; k <= 3; k++) Set(g, hy + 4, hx + k, 'm');
            Set(g, hy - 2, hx - 3, 'w');
            int open = frame == 0 ? 3 : 2;
            Oval(g, hx + 5, hy, open, open + 1, 'r');
            Oval(g, hx + 5, hy, Math.Max(1, open - 2), open - 1, 'R');
            foreach (var (dx, dy) in new[] { (0, -4), (3, -3), (4, 0), (3, 3), (0, 4), (-2, 3), (-2, -3) })
                Set(g, hy + dy, hx + 5 + dx, 'h');
            Set(g, hy - 3, hx - 1, 'e');
            // Sand still running off it.
            foreach (var (x, y) in new[] { (6, 18), (5, 21), (16, 14), (17, 17) }) Set(g, y, x, 'S');
            return Rows(g);
        }

        // The frost mammoth (XL): a shaggy hump of a body crusted with ice, icicles hanging from its fur, a great domed
        // head with a curled trunk and two long tusks curving up, four pillar legs. The trunk sways and the legs step
        // between the frames. About 52 by 40.
        static string[] FrostMammoth(int frame)
        {
            var g = Canvas(54, 42);
            // The far legs, then the near ones.
            foreach (int x0 in new[] { 14, 34 })
            {
                int lift = (x0 == 14) == (frame == 0) ? 1 : 0;
                for (int y = 30; y <= 39 - lift; y++)
                    for (int x = x0; x <= x0 + 5; x++) Set(g, y, x, 'd');
            }
            foreach (int x0 in new[] { 8, 28 })
            {
                int lift = (x0 == 8) == (frame == 1) ? 1 : 0;
                for (int y = 31; y <= 41 - lift; y++)
                    for (int x = x0; x <= x0 + 6; x++) Set(g, y, x, y >= 40 - lift ? 'K' : x == x0 + 6 ? 'm' : x == x0 ? 'L' : 'M');
            }
            // The body: a long high hump, its fur ragged at the hem.
            Dome(g, 1, 6, 42, 26);
            for (int x = 2; x <= 41; x++)
                if (x % 3 != 1) Set(g, 32 + (x % 2), x, 'm');
            // Shaggy fur: long strands down the flank, darker toward the hem.
            for (int x = 5; x <= 39; x += 4)
            {
                int from = 14 + (x * 7) % 7, to = 29 + (x % 3);
                for (int y = from; y <= to; y++)
                    if (g[y, x] != '.') Set(g, y, x + (y > 22 ? 1 : 0), y > 25 ? 'd' : 'm');
            }
            foreach (var (x, y) in new[] { (6, 14), (12, 11), (18, 16), (26, 12), (30, 21) }) Set(g, y, x, 'L');
            // The ice on its back, and icicles from its hem.
            foreach (var (x, y, w) in new[] { (8, 9, 6), (17, 7, 9), (29, 8, 6) })
            {
                for (int k = 0; k < w; k++) Set(g, y - (k % 3 == 1 ? 1 : 0), x + k, k % 2 == 0 ? 'c' : 'C');
            }
            foreach (int x in new[] { 5, 13, 21, 25, 37 })
            {
                Set(g, 34, x, 'c');
                Set(g, 35, x, 'C');
            }
            // The head: high and round, forward of the hump, an ear, an eye.
            Oval(g, 42, 15, 9, 9, 'M');
            Dome(g, 34, 5, 17, 8);
            Oval(g, 37, 17, 3, 5, 'm');
            Set(g, 13, 45, 'e');
            Set(g, 12, 45, 'w');
            // The trunk: down from the face and curled at its end, swaying.
            int sway = frame == 0 ? 0 : 1;
            for (int y = 18; y <= 33; y++)
            {
                int x = 47 + (y > 26 ? sway : 0);
                Set(g, y, x, 'M');
                Set(g, y, x + 1, y % 3 == 0 ? 'd' : 'm');
            }
            Set(g, 34, 46 + sway, 'M');
            Set(g, 34, 47 + sway, 'm');
            Set(g, 33, 45 + sway, 'M');
            // A beard of shaggy fur under the face.
            for (int x = 36; x <= 46; x++) Set(g, 24 + (x % 2), x, 'm');
            // The tusks: long, out from under the face, down and forward and curving up past the trunk.
            foreach (var (x, y) in new[] { (42, 23), (43, 24), (44, 25), (45, 26), (46, 27), (47, 27), (48, 27), (49, 27), (50, 26), (51, 25), (52, 23), (52, 22), (53, 21), (53, 20) })
            {
                Set(g, y, x, 'h');
                Set(g, y + 1, x, 'H');
            }
            return Rows(g);
        }

        // The temple guardian (XL): a statue of sandstone, a striped headdress of gold and lapis about a blank face
        // with glowing eyes, a sun carved on its chest, a gold belt, arms with heavy fists, two pillar legs; cracks
        // in the stone. The fists rise and the eyes flicker between the frames. About 46 by 48.
        static string[] TempleGuardian(int frame)
        {
            var g = Canvas(46, 48);
            int lift = frame;
            // The legs.
            foreach (int x0 in new[] { 13, 26 })
                for (int y = 38; y <= 47; y++)
                    for (int x = x0; x <= x0 + 6; x++) Set(g, y, x, y == 47 ? 'd' : x == x0 ? 'L' : x == x0 + 6 ? 'm' : 'M');
            // The body: a block lit from the left, a carved sun and a belt.
            for (int y = 18; y <= 38; y++)
                for (int x = 9; x <= 36; x++) Set(g, y, x, y == 38 ? 'd' : x <= 10 ? 'L' : x >= 34 ? 'm' : 'M');
            for (int x = 9; x <= 36; x++) Set(g, 18, x, 'L');
            for (int x = 9; x <= 36; x++) Set(g, 33, x, x % 4 == 0 ? 'Y' : 'y');
            Oval(g, 22, 25, 3, 3, 'y');
            Oval(g, 22, 25, 1, 1, 'Y');
            foreach (var (dx, dy) in new[] { (0, -5), (5, 0), (-5, 0), (0, 5), (4, -4), (-4, -4), (4, 4), (-4, 4) }) Set(g, 25 + dy, 22 + dx, 'y');
            // The arms: hanging blocks with fists, raised a little in the second frame.
            foreach (int x0 in new[] { 2, 37 })
            {
                for (int y = 18 - lift; y <= 33 - lift; y++)
                    for (int x = x0; x <= x0 + 6; x++) Set(g, y, x, x == x0 ? (x0 == 2 ? 'L' : 'M') : x == x0 + 6 ? 'm' : 'M');
                for (int y = 34 - lift; y <= 39 - lift; y++)
                    for (int x = x0 - 1; x <= x0 + 7; x++) Set(g, y, x, y == 39 - lift ? 'd' : x == x0 - 1 ? 'L' : x == x0 + 7 ? 'm' : 'M');
                Set(g, 26 - lift, x0 + 3, 'k');
            }
            // The head and its headdress: stripes falling to the shoulders, a band over the brow, a blank face.
            for (int y = 3; y <= 19; y++)
            {
                for (int x = 13; x <= 32; x++)
                {
                    bool side = x <= 15 || x >= 30;
                    if (y < 6 || side) Set(g, y, x, (y + (side ? 0 : 1)) % 2 == 0 ? 'b' : 'y');
                }
            }
            for (int x = 14; x <= 31; x++) Set(g, 2, x, 'y');
            for (int y = 6; y <= 17; y++)
                for (int x = 16; x <= 29; x++) Set(g, y, x, x == 16 ? 'L' : x == 29 ? 'm' : 'M');
            char eye = frame == 0 ? 'g' : 'w';
            foreach (int x in new[] { 19, 20, 25, 26 }) Set(g, 10, x, eye);
            Stroke(g, 21, 15, 24, 15, 'd');
            Stroke(g, 22, 12, 22, 13, 'm');
            // Cracks in the stone.
            Stroke(g, 31, 20, 29, 24, 'k');
            Stroke(g, 29, 24, 30, 27, 'k');
            Stroke(g, 12, 28, 14, 31, 'k');
            Stroke(g, 27, 8, 28, 10, 'k');
            return Rows(g);
        }

        // The big kinds of the two biomes, or null.
        static string[][] IceSandBigFrames(string id) => id switch
        {
            "yeti" => new[] { Yeti(0), Yeti(1) },
            "great_worm" => new[] { GreatWorm(0), GreatWorm(1) },
            "frost_mammoth" => new[] { FrostMammoth(0), FrostMammoth(1) },
            "temple_guardian" => new[] { TempleGuardian(0), TempleGuardian(1) },
            _ => CrystalLakeBigFrames(id),
        };

        // What a pose changes in the two biomes' colours: a bomb scarab winding up to go off flares, its abdomen and
        // spark pulsing between white-hot and orange from frame to frame (its fuse is burning).
        static void IceSandPose(string id, MonsterPose pose, int frame, Dictionary<char, Rgba> colors)
        {
            // The crystal cave's and the lake's (B3b).
            CrystalLakePose(id, pose, frame, colors);
            if (id != "bomb_scarab" || pose != MonsterPose.Windup) return;
            bool flare = frame % 2 == 0;
            colors['g'] = Rgba.Hex(flare ? "#FFD060" : "#FF8A30");
            colors['G'] = Rgba.Hex(flare ? "#FFFCE0" : "#FFD060");
            colors['f'] = Rgba.Hex(flare ? "#FFFFFF" : "#FFE890");
        }

        // ---- Dormant (World.IsDormant) ----

        // Whether a kind is drawn as a statue while it stands dormant (MonsterTraitKind.Dormant).
        public static bool Sleeps(string id) => id == "frost_mammoth" || id == "temple_guardian" || CrystalLakeSleeps(id);

        static readonly Rgba Stone = Rgba.Hex("#8E8478"), Ice = Rgba.Hex("#BFE8FF"), Rime = Rgba.Hex("#F4FCFF");

        // A dormant mid-boss: the guardian turned to plain grey stone, its eyes dark; the mammoth under a crust of ice,
        // pale and blue with white rime on its upper edges.
        public static PixelImage Dormant(string id, PixelImage image)
        {
            // A mimic's crystals, the golem's stone, the turtle's moss (CreatureArt.CrystalLake.cs, B3b).
            if (CrystalLakeDormant(id, image) is PixelImage other) return other;
            var result = image.Copy();
            bool ice = id == "frost_mammoth";
            for (int y = 0; y < result.Height; y++)
            {
                for (int x = 0; x < result.Width; x++)
                {
                    var c = result[x, y];
                    if (c.A == 0) continue;
                    float light = (c.R * 0.3f + c.G * 0.55f + c.B * 0.15f) / 255f;
                    Rgba shown;
                    if (ice)
                    {
                        shown = c.Mix(Ice, 0.55f);
                        // Rime where the picture's top edge is (nothing above it).
                        if (y == 0 || result[x, y - 1].A == 0) shown = Rime;
                    }
                    // The glow of its eyes is out.
                    else if (c.B > 200 && c.R < 160) shown = Stone.Scale(0.35f);
                    else shown = Stone.Scale(0.55f + 0.7f * light);
                    result[x, y] = new Rgba(shown.R, shown.G, shown.B, c.A);
                }
            }
            return result;
        }
    }
}
