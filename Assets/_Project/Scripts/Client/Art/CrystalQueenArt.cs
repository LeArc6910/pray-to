using System.Collections.Generic;

namespace Game.Client
{
    // What the crystal spider queen is doing, which picks its frame (WorldView.CrystalQueen.cs). Each attack has a
    // wind-up of its own that the whole body takes part in: reared up with the front legs high and the crown lit, then
    // the bite (Rear, Bite); pressed low with the knees high, then the pounce (Crouch, Spring, Rush, Skid); the head
    // raised and the crown gathering light while the front legs open like a fan, then the light let go (Prism, Flash);
    // the legs drawn in and the body hunched round its glowing gems, then every shard thrown off at once (Bristle,
    // Burst); the two front legs lifted high, then driven into the floor so the crystal rings (Lift, Stomp); the
    // abdomen lowered to the floor and swelling with glowing eggs, then the eggs laid (Brood, Lay); and a screech, a
    // flinch, its fall and its entrance.
    enum CrystalQueenPose
    {
        Idle,
        Walk,
        Rear,
        Bite,
        Crouch,
        Spring,
        Rush,
        Skid,
        Prism,
        Flash,
        Bristle,
        Burst,
        Lift,
        Stomp,
        Brood,
        Lay,
        Screech,
        Flinch,
        Die,
        Enter,
    }

    // The crystal spider queen of the crystal caves (2026-11-27, Docs/Bosses.md 수정 거미 여왕): a great spider grown of
    // pink and sky-blue crystal, a crown of pink gems on its head. The abdomen, the head and the crown of its five views
    // come from one Retro Diffusion Pro picture turned eight ways (Docs/ArtSources.md, $0.28); the crown is cut from
    // the picture's abdomen and worn on the head. The eight legs are drawn here as faceted shafts of clear blue crystal
    // with a pink gem at each knee, each from its hip to where its foot stands, bent at the knee (two bones, solved in
    // three dimensions and laid on the screen for the view, as SpiderArt does), so the legs step, lift and strike by
    // themselves and the body rides on them: carried forward and back, raised and dropped, pitched head up or down,
    // the abdomen swung and swelling; the crown and the abdomen's gems light up. Faces right; WorldView flips it.
    static class CrystalQueenArt
    {
        public const int Width = 120;
        public const int Height = 96;
        // Rows under the middle of its body for the legs nearest the viewer; WorldView stands the picture on this row.
        public const int Floor = 26;
        // How tall the body itself stands, for the name and marks over it.
        public const int BodyTall = 44;

        static readonly Dictionary<char, Rgba> Palette = new Dictionary<char, Rgba>
        {
            ['o'] = new Rgba(0, 0, 8),
            ['a'] = new Rgba(29, 3, 30),
            ['b'] = new Rgba(102, 21, 69),
            ['c'] = new Rgba(122, 18, 81),
            ['d'] = new Rgba(57, 112, 145),
            ['e'] = new Rgba(178, 50, 126),
            ['f'] = new Rgba(137, 82, 148),
            ['g'] = new Rgba(89, 142, 187),
            ['h'] = new Rgba(179, 98, 159),
            ['i'] = new Rgba(97, 147, 201),
            ['j'] = new Rgba(100, 160, 196),
            ['k'] = new Rgba(123, 165, 191),
            ['l'] = new Rgba(137, 152, 200),
            ['m'] = new Rgba(198, 134, 158),
            ['n'] = new Rgba(206, 115, 173),
            ['p'] = new Rgba(121, 183, 211),
            ['q'] = new Rgba(222, 127, 185),
            ['r'] = new Rgba(134, 198, 223),
            ['s'] = new Rgba(226, 145, 187),
            ['t'] = new Rgba(217, 167, 202),
            ['u'] = new Rgba(237, 169, 208),
            ['v'] = new Rgba(174, 214, 227),
            ['w'] = new Rgba(231, 190, 202),
            ['x'] = new Rgba(182, 229, 241),
            ['y'] = new Rgba(245, 194, 227),
            ['z'] = new Rgba(238, 220, 242),
            ['A'] = new Rgba(213, 247, 251),
            ['B'] = new Rgba(254, 226, 248),
            ['C'] = new Rgba(254, 254, 254),
        };

        // 27x19, cut at (19, 13) of the picture.
        static readonly string[] FrontAbdomenRows =
        {
            ".........ooooooooo.........",
            "......ooozBBeezwyyooo......",
            ".....oonuyBeyeezssnaoo.....",
            "...ooxxcyyzyeezusqbvvoo....",
            "..ooACCCbbBzyyuubbvrrxoo...",
            ".oovvCCCCCbbbbcbwvrArproo..",
            ".oorwwCCCCyyzyuutrrrAprooo.",
            "ootttyCCCyuuBBuuutwwllllooo",
            "oottutCCuuuzBuyuutyhulllooo",
            "oosssstBuuBuuuuuuzhhhtlloAo",
            "oobtyuBwBuuuuuuuBnnnnhnooAo",
            "oCoutBBsyBuuuuuuBnhynnhoCAo",
            ".oouuCsssBuuuuuBnnytBssoCo.",
            ".oAossssssBuuuBBhnnzCsoxpo.",
            "..oossssysoooooounnhBsoro..",
            "...ooussooxxxvvvoohqsooo...",
            ".....onawwxvCAAvwwonso.....",
            "......oooxvxCCvvwwooo......",
            ".........ooooooooo.........",
        };

        // 21x21, cut at (22, 26) of the picture.
        static readonly string[] FrontHeadRows =
        {
            "..........o..........",
            "......ooooooooo......",
            "....oooxxxvvvoooo....",
            "...oawwxvCAAvwwono...",
            "..oatzxvxCCvvwwtooo..",
            ".oottzvAvxvAvwwttogo.",
            ".ottttppppppplttshao.",
            "ootttntBzBzzynfttmajo",
            "oostfzBCCyzByuyftmago",
            "oomhhzyCzyyyyyysfmaoo",
            "oofcszycyByucyuucbaio",
            "ooaybyysczybsyucuooio",
            "oooccyyyyyyyyyycboooo",
            "ooouueccyyyycccuuooxo",
            "oxomsccycyycyecssoxxo",
            ".okosucccyzcccunojgo.",
            ".ogohmuuuuuuuuumoggo.",
            "..ooobfuuuuuubfoooo..",
            "...okppfhhhhdpjkoo...",
            "....oordmoomdrxoo....",
            "......ooooooooo......",
        };

        // 17x13, cut at (24, 5) of the picture.
        static readonly string[] FrontCrownRows =
        {
            "..o....oo........",
            ".oto..offo.......",
            "..o..oeBsmo......",
            "....ooeCBfoo.....",
            ".oooboeCBeobooo..",
            "obhbmceCzebmbhbo.",
            "obBbmceCzecmcBbo.",
            "obBbecCCxzchcBbo.",
            "obBBcyACCBucBzbo.",
            "obnBzBBeezwyyhbo.",
            "oonuyBeyeezssnaoo",
            "oxcyyzyeezusqbvvo",
            ".ooooooooooooooo.",
        };

        // 26x19, cut at (13, 16) of the picture.
        static readonly string[] FrontSideAbdomenRows =
        {
            ".........oooooooo.........",
            "......oooyyzzzBezoo.......",
            ".....oxxxbbyzyyybbvoo.....",
            "....oACAAAAbbbbbztvApo....",
            "...orACACCCBzzzzByupppo...",
            "..ouwrAACCBBBBzyuuuywpoo..",
            ".osyhrprCBBBBBuuuuuBnnmoo.",
            ".oqnhqppwBBBBuuuuuBnnnhoo.",
            ".orohhppuuuBuuuuuuBqznhoo.",
            "ojAxohqsutttBuuuuBsquzhoAo",
            ".oiAAooqtuuuuBuuzysqyBooo.",
            ".ogjAApostuuuuBuBsssooooo.",
            ".oooovAvosttttuBysooxxxxo.",
            "..oxjooxvotuuutsyovxxvCo..",
            "...oAAAoopouuuusowBvxvo...",
            "....ovAAxooaqusooCBBvo....",
            ".....orrxxvoonhozBBBo.....",
            "......ooorrrkoooyooo......",
            ".........oooooooo.........",
        };

        // 20x21, cut at (27, 26) of the picture.
        static readonly string[] FrontSideHeadRows =
        {
            ".......oooooo.......",
            ".....oooooogooo.....",
            "....ooxxxxvoorjo....",
            "...ovxxvCvvCzodoo...",
            "..owBvxvCCxvBBoovo..",
            "..oCBBvxvvvvrByoppo.",
            ".ozBBBBxvvppptssopo.",
            "ooyBBBzwklzBCCssogao",
            "ohnBBBueyBCzzCCyoooo",
            "ohnhzyhzyzCBBCeBodro",
            "ohneunwByeeBCBcyhlpo",
            "ohqecuBCzucBCcsyybpo",
            "ooeeecyyyyyyyyyccbko",
            "oroqncshccyyyycqcooo",
            ".oposscccycyycsccoo.",
            ".ojoqssscccByncuao..",
            "..opoqsuccsuyynoo...",
            "...oooofsuuusggoo...",
            "....oopkonnnbrAo....",
            ".....ooxrhmaooo.....",
            ".......oooooo.......",
        };

        // 18x15, cut at (18, 5) of the picture.
        static readonly string[] FrontSideCrownRows =
        {
            ".............oo...",
            ".........oo.oCwo..",
            "........oeeo.oo...",
            ".......oeyyo......",
            "..oo.o.oeBCeooo...",
            ".obbobobnCCeohboo.",
            ".obhbmbhnCCecBboto",
            ".obBbmbhnCCecsbuCo",
            ".obBbeucsCCCczboo.",
            ".obBBchczzCCCybo..",
            ".obBCCbzCtueBmbo..",
            "oooyBBzzCceeBmao..",
            "ovxayyzzzBezybkoo.",
            "oxxxbbyzyyybbvopoo",
            ".ooooooooooooo.oo.",
        };

        // 23x19, cut at (11, 19) of the picture.
        static readonly string[] SideAbdomenRows =
        {
            ".......oooooooo........",
            ".....ooxcuuuuBBoo......",
            "....oCCxxbbcccbbvo.....",
            "...oACCxxxxxxxxxvAo....",
            "..ohrCCxAzCCCCCCyrro...",
            ".ooorrxAwBBCCCCCzhpoo..",
            "oouoarrvBBBBCCCCqhnloo.",
            "oouuwrrtBBByyCCCnsnloo.",
            "oouuurrtBByyyyBszyuloo.",
            "oouuurrssBByyyssszulooo",
            "oouBurssssyBBsssnyumoo.",
            "oosBwrssssnuBssBnnyoso.",
            ".oouuqssssqooosynnooqo.",
            ".ooosqssssojAAoqnhooo..",
            "..oohhqssoAjpAAonhoo...",
            "...ohhnnqoAApjAAooo....",
            "....ohnnoAArrovxoo.....",
            ".....oohoAvroopoo......",
            ".......oooooooo........",
        };

        // 19x19, cut at (30, 23) of the picture.
        static readonly string[] SideHeadRows =
        {
            ".......ooooo.......",
            ".....oorArooo......",
            "....ooooooooo.oo...",
            "...ovxxxxvceeooAo..",
            "..oaxxxCCxxzzeoAvo.",
            ".ooCCzxvCvxrrraprpo",
            ".oCCBBBxvxrryyyajoo",
            "osCBBBBzxpBCyyyqoo.",
            "oqnBBBBzhyCCCyyzoo.",
            "oqhyBBBhyyyCCzccyco",
            "oqseyBeuzyycBzyyyco",
            "omseuyzCCyyuooyycco",
            ".osseetyyyyopvoucco",
            ".oqmooauccoAjxAoeco",
            "..oopkoccaApgrAAoo.",
            "...ovAohoArpojvxo..",
            "....oAoorrpooojo...",
            ".....ooojjotuoo....",
            ".......oooooo......",
        };

        // 19x14, cut at (13, 8) of the picture.
        static readonly string[] SideCrownRows =
        {
            "......oo...oo......",
            ".....oCto.oeeo.....",
            "......oo.oeyBeo....",
            ".......o.oeBCeo....",
            "....ooobooeBCeooo..",
            "...obhbmbcCCChohbo.",
            "...obBbmbyeCChczbo.",
            "...obBbhccCCCzczbo.",
            "...obzBccyCCCBCzbo.",
            "...obsByyCCeeBBubo.",
            "...obsuuzBcyeeysboo",
            ".oookbuuuzzeeBubpAo",
            "oopxAxcuuuuBBzcovAo",
            ".ooooooooooooooooo.",
        };

        // 25x21, cut at (16, 20) of the picture.
        static readonly string[] BackSideAbdomenRows =
        {
            "............o............",
            ".......oooooyooooo.......",
            "......oAbeuuzyybaoo......",
            "....ooxCCecbbbbxxotoo....",
            "...oorAAxAAAACCCxxorro...",
            "..opodxAxxAACCCCAxpprCo..",
            "..owbadxxxxAACCCCxroppo..",
            ".osBafojvvxxxxCApjooBBBo.",
            ".osBCoooorAxxxxooouoyyzo.",
            ".oqBCCnCopAxxxxuuuuonyzo.",
            "oonBCCCooprxxxuuuuuonnyyo",
            ".oouyyuurrrxxwuuuuuonnyo.",
            ".oouuuuutrrrrwuuuuuohhqo.",
            ".ooouuuuutrrrruuuuhoseco.",
            "..oohwuuuutvrrwuuuoooqo..",
            "..oohnsuuuuttssssokgxoo..",
            "...oonnuuuunqsqqoAgxxo...",
            "....oohhsunnssqoxjgoo....",
            "......oehnnnqqojvjo......",
            ".......ooooonooooo.......",
            "............o............",
        };

        // 15x16, cut at (34, 22) of the picture.
        static readonly string[] BackSideHeadRows =
        {
            "....oooo.......",
            "...oxCyoo......",
            "..oCCvvpoo.....",
            "..oCCCpzyoo....",
            ".oppppzzysco...",
            ".oBBBBzzzBco...",
            ".oyyzCCzczcooo.",
            "oonyzBCCBbcooxo",
            ".onnyyCCcycooo.",
            ".onnyBzqesovAo.",
            ".ohhqyecsoxAAo.",
            "..oecueeovAro..",
            "..oqccmooovro..",
            "...oqnokjvoo...",
            "....ooojooo....",
            ".......o.......",
        };

        // 18x16, cut at (20, 8) of the picture.
        static readonly string[] BackSideCrownRows =
        {
            ".....oo...........",
            "....oyCo..........",
            "...oooo.oooo......",
            "..obeo.oezeeo.....",
            "..obtcooezCeo.....",
            ".oobtqccnzCeo.....",
            "occhcschmzCeo.o...",
            "onBncqccqzCeooco..",
            "ozCuecyBczBncCco..",
            "ouucccyCcyzcCCco..",
            "ocsuhsuCBccCCBco..",
            "ooqstuuCCezBBuao..",
            "orbsuuuyzzzyunaoo.",
            "oxxbuuuuyByyuoooo.",
            "oACAbeuuzyybaooooo",
            ".oooooooooooooooo.",
        };
        // ---- The five views: the abdomen, the head and the crown cut from the picture, where the middles of the
        // abdomen and head are, how far the abdomen's middle is from the head's, where the crown's foot sits on the
        // head and where the mouth is (picture pixels), and the way the view faces on the ground (degrees: 0 to the
        // right, 90 toward the viewer). From straight behind the picture shows a face, so the back view wears the front
        // view's abdomen (nearest the viewer) and the three-quarter-back head. ----

        struct Rig
        {
            public string[] Abdomen, Head, Crown;
            public float AbMidX, AbMidY, HdMidX, HdMidY, AbOffX, AbOffY, CrownX, CrownY, MouthX, MouthY, Angle;
        }

        static Rig RigOf(BossView view) => view switch
        {
            BossView.Front => new Rig { Abdomen = FrontAbdomenRows, Head = FrontHeadRows, Crown = FrontCrownRows, AbMidX = 13, AbMidY = 9, HdMidX = 10, HdMidY = 10.5f, AbOffX = 0, AbOffY = -14.5f, CrownX = 0, CrownY = -8, MouthX = 0, MouthY = 8, Angle = 90 },
            BossView.FrontSide => new Rig { Abdomen = FrontSideAbdomenRows, Head = FrontSideHeadRows, Crown = FrontSideCrownRows, AbMidX = 12.5f, AbMidY = 9, HdMidX = 9.5f, HdMidY = 10, AbOffX = -11, AbOffY = -11, CrownX = -1, CrownY = -8, MouthX = 5, MouthY = 6, Angle = 45 },
            BossView.Side => new Rig { Abdomen = SideAbdomenRows, Head = SideHeadRows, Crown = SideCrownRows, AbMidX = 10.5f, AbMidY = 9, HdMidX = 9.5f, HdMidY = 9, AbOffX = -18, AbOffY = -4, CrownX = -1.5f, CrownY = -7, MouthX = 8, MouthY = 2, Angle = 0 },
            BossView.BackSide => new Rig { Abdomen = BackSideAbdomenRows, Head = BackSideHeadRows, Crown = BackSideCrownRows, AbMidX = 12, AbMidY = 10, HdMidX = 7, HdMidY = 7, AbOffX = -13, AbOffY = 1, CrownX = 0, CrownY = -5.5f, MouthX = 4, MouthY = -4, Angle = -45 },
            _ => new Rig { Abdomen = FrontAbdomenRows, Head = BackSideHeadRows, Crown = FrontCrownRows, AbMidX = 13, AbMidY = 9, HdMidX = 7, HdMidY = 7, AbOffX = 0, AbOffY = 10, CrownX = 0, CrownY = -5.5f, MouthX = 0, MouthY = -4, Angle = -90 },
        };

        static readonly Dictionary<(BossView, int), PixelImage> Parts = new Dictionary<(BossView, int), PixelImage>();

        // 0 the abdomen, 1 the head, 2 the crown.
        static PixelImage PartOf(BossView view, int part)
        {
            if (Parts.TryGetValue((view, part), out var image)) return image;
            var rig = RigOf(view);
            image = PixelImage.FromRows(Palette, part == 0 ? rig.Abdomen : part == 1 ? rig.Head : rig.Crown);
            Parts[(view, part)] = image;
            return image;
        }

        // ---- The body in three dimensions, in pixels: ahead, to its right, up. The head rides BodyHigh over the
        // ground a little ahead of the middle of the whole body (where the boss stands); each hip is on a ring round
        // it, each foot rests on a wider ring on the ground, and a leg is two bones bent upward at the knee. Legs 0..3
        // are its left ones from the front back, 4..7 its right. Longer and heavier than the silk mother's. ----

        const float ChestAhead = 4;
        const float BodyHigh = 8;
        const float Thigh = 12;
        const float Shin = 17;
        const float Deep = 0.62f;
        const float Tall = 0.9f;
        const float AbdomenBehind = 16;
        const float AbdomenAbove = 4;

        static readonly float[] HipAngle = { 40, 72, 106, 140 };
        const float HipOut = 6;
        static readonly float[] FootAngle = { 36, 66, 98, 130 };
        static readonly float[] FootOut = { 27, 25, 24, 27 };

        const int OriginX = Width / 2;
        const int OriginY = Height - 1 - Floor;

        // ---- A pose: the head carried ahead (Fwd) and up (Up) from rest and pitched up (Pitch, degrees); the abdomen
        // swung ahead and up of where the pitch leaves it and swollen (Swell, a share of its size); the fangs open
        // (0..1); the crown lit (Crown 0..1) and flaring with rays (Flare 0..1); the abdomen's gems glowing (Gem 0..1)
        // and shards bristling out of its crystal (Spike 0..1); and each foot moved from where it rests on the ground:
        // ahead (F), out from the body's line (O) and up (U). Tuck gathers the feet under the body, Curl folds them up
        // over it (dead). ----

        sealed class Pose
        {
            public float Fwd, Up, Pitch, AbFwd, AbUp, Swell, Fang, Crown, Flare, Gem, Spike, Tuck, Curl;
            public readonly float[] F = new float[8], O = new float[8], U = new float[8];
            // The frame before a fast one, for the streaks its legs and body leave.
            public Pose Before;
        }

        static Pose P(float fwd = 0, float up = 0, float pitch = 0, float abFwd = 0, float abUp = 0, float swell = 0, float fang = 0, float crown = 0, float flare = 0,
            float gem = 0, float spike = 0, float tuck = 0, float curl = 0,
            (float F, float O, float U)? p0 = null, (float F, float O, float U)? p1 = null, (float F, float O, float U)? p2 = null, (float F, float O, float U)? p3 = null,
            (float F, float O, float U)? l0 = null, (float F, float O, float U)? r0 = null)
        {
            var pose = new Pose { Fwd = fwd, Up = up, Pitch = pitch, AbFwd = abFwd, AbUp = abUp, Swell = swell, Fang = fang, Crown = crown, Flare = flare, Gem = gem, Spike = spike, Tuck = tuck, Curl = curl };
            void Set(int leg, (float F, float O, float U)? at)
            {
                if (at is not (float f, float o, float u)) return;
                pose.F[leg] = f;
                pose.O[leg] = o;
                pose.U[leg] = u;
            }
            var pairs = new[] { p0, p1, p2, p3 };
            for (int pair = 0; pair < 4; pair++)
            {
                Set(pair, pairs[pair]);
                Set(pair + 4, pairs[pair]);
            }
            Set(0, l0);
            Set(4, r0);
            return pose;
        }

        static Pose Lerp(Pose a, Pose b, float u)
        {
            float L(float x, float y) => x + (y - x) * u;
            var pose = new Pose
            {
                Fwd = L(a.Fwd, b.Fwd), Up = L(a.Up, b.Up), Pitch = L(a.Pitch, b.Pitch), AbFwd = L(a.AbFwd, b.AbFwd), AbUp = L(a.AbUp, b.AbUp), Swell = L(a.Swell, b.Swell),
                Fang = L(a.Fang, b.Fang), Crown = L(a.Crown, b.Crown), Flare = L(a.Flare, b.Flare), Gem = L(a.Gem, b.Gem), Spike = L(a.Spike, b.Spike), Tuck = L(a.Tuck, b.Tuck), Curl = L(a.Curl, b.Curl),
            };
            for (int i = 0; i < 8; i++)
            {
                pose.F[i] = L(a.F[i], b.F[i]);
                pose.O[i] = L(a.O[i], b.O[i]);
                pose.U[i] = L(a.U[i], b.U[i]);
            }
            return pose;
        }

        // Key poses at times 0..1, eased in and out between each pair.
        static Pose Keys(float t, params (float At, Pose Pose)[] keys)
        {
            if (t <= keys[0].At) return keys[0].Pose;
            for (int i = 1; i < keys.Length; i++)
            {
                if (t > keys[i].At) continue;
                float u = (t - keys[i - 1].At) / (keys[i].At - keys[i - 1].At);
                return Lerp(keys[i - 1].Pose, keys[i].Pose, u * u * (3 - 2 * u));
            }
            return keys[keys.Length - 1].Pose;
        }

        // A blow's frames come in stages, each frame shown for its stage's time: the swing (too fast to follow, streaks
        // behind the legs and body), the strike, the rebound, held where it struck, and the way back to rest.
        enum Stage : byte
        {
            Swing,
            Strike,
            Rebound,
            Held,
            Recover,
        }

        static float StageSeconds(Stage stage) => stage switch
        {
            Stage.Swing => 0.03f,
            Stage.Strike => 0.09f,
            Stage.Rebound => 0.08f,
            Stage.Held => 0.1f,
            _ => 0.05f,
        };

        static (Pose[] Frames, Stage[] Stages) Expand(Pose from, params (Stage Stage, int Frames, Pose To)[] stages)
        {
            var frames = new List<Pose>();
            var kinds = new List<Stage>();
            var at = from;
            foreach (var (stage, count, to) in stages)
            {
                for (int k = 1; k <= count; k++)
                {
                    float u = k / (float)count;
                    bool fast = stage == Stage.Swing || stage == Stage.Strike;
                    var p = Lerp(at, to, fast ? (float)System.Math.Pow(u, 1.3) : u * u * (3 - 2 * u));
                    if (fast) p.Before = frames.Count > 0 ? frames[frames.Count - 1] : from;
                    frames.Add(p);
                    kinds.Add(stage);
                }
                at = to;
            }
            return (frames.ToArray(), kinds.ToArray());
        }

        // ---- The poses. Every motion starts from and comes back to the same rest. ----

        static readonly Pose Rest = P(crown: 0.15f);

        // Reared: up on the hind legs, head high, the front pair raised high and wide, the crown lit, fangs open.
        static readonly Pose Reared = P(fwd: -6, up: 9, pitch: 30, fang: 1, crown: 0.45f, p0: (-14, 8, 34), p1: (-3, 6, 15), p2: (-1, 1, 0), p3: (-4, 2, 0));
        static readonly Pose Bitten = P(fwd: 16, up: -3, pitch: -16, crown: 0.3f, p0: (14, 4, 0), p1: (6, 3, 0), p2: (1, 1, 0), p3: (2, 1, 0));
        static readonly (Pose[] Frames, Stage[] Stages) BiteBlow = Expand(Reared,
            (Stage.Swing, 6, P(fwd: 13, up: -1, pitch: -12, fang: 0.3f, crown: 0.4f, p0: (11, 5, 5), p1: (5, 3, 1), p2: (1, 1, 0), p3: (1, 1, 0))),
            (Stage.Strike, 1, Bitten),
            (Stage.Rebound, 1, P(fwd: 14, up: -2, pitch: -12, crown: 0.25f, p0: (12, 4, 1), p1: (5, 3, 0), p2: (1, 1, 0), p3: (1, 1, 0))),
            (Stage.Held, 2, P(fwd: 14, up: -2, pitch: -12, crown: 0.2f, p0: (12, 4, 1), p1: (5, 3, 0), p2: (1, 1, 0), p3: (1, 1, 0))),
            (Stage.Recover, 9, Rest));

        // Pressed low, knees high, before the pounce; braking at its end.
        static readonly Pose Crouched = P(fwd: -8, up: -6, pitch: 4, abUp: -1, fang: 0.3f, crown: 0.2f, p0: (-1, 2, 0), p1: (0, 1, 0), p3: (2, 2, 0));
        static readonly Pose Braked = P(fwd: 5, up: -2.5f, pitch: -6, abUp: 3, crown: 0.2f, p0: (7, 2, 0), p1: (4, 2, 0), p2: (0, 1, 0), p3: (-3, 0, 3));

        // The prism: the head raised, the crown gathering light, the front legs opened wide like a fan (out, not up)
        // and the second pair after them; then the light let go with a jerk of the head.
        static readonly Pose Prismed = P(fwd: -3, up: 5, pitch: 22, crown: 1, p0: (4, 11, 14), p1: (2, 9, 6), p2: (0, 1, 0), p3: (-2, 1, 0));
        static readonly (Pose[] Frames, Stage[] Stages) FlashBlow = Expand(Prismed,
            (Stage.Swing, 3, P(fwd: 2, up: 2, pitch: 6, crown: 1, flare: 0.6f, p0: (6, 12, 9), p1: (3, 10, 3), p2: (0, 1, 0), p3: (-2, 1, 0))),
            (Stage.Strike, 1, P(fwd: 3, up: 1, pitch: 0, crown: 1, flare: 1, p0: (7, 12, 6), p1: (3, 10, 2), p2: (0, 1, 0), p3: (-2, 1, 0))),
            (Stage.Rebound, 1, P(fwd: 2, up: 1.5f, pitch: 3, crown: 0.8f, flare: 0.6f, p0: (6, 11, 7), p1: (3, 9, 2), p2: (0, 1, 0), p3: (-2, 1, 0))),
            (Stage.Held, 2, P(fwd: 2, up: 1.5f, pitch: 3, crown: 0.6f, flare: 0.2f, p0: (6, 11, 7), p1: (3, 9, 2), p2: (0, 1, 0), p3: (-2, 1, 0))),
            (Stage.Recover, 9, Rest));

        // Bristling: the legs drawn in, the body hunched down round its gems, which blaze; shards rising out of the
        // crystal. Then thrown off at once, the legs flung out and the body jolted up.
        static readonly Pose Bristled = P(up: -4, pitch: -4, abUp: 2, swell: 0.08f, gem: 1, spike: 0.5f, crown: 0.3f, p0: (-4, -4, 2), p1: (-2, -4, 0), p2: (1, -4, 0), p3: (3, -3, 0));
        static readonly (Pose[] Frames, Stage[] Stages) BurstBlow = Expand(Bristled,
            (Stage.Swing, 2, P(up: 2, pitch: 2, abUp: 3, swell: 0.12f, gem: 1, spike: 1, crown: 0.5f, p0: (3, 6, 3), p1: (1, 6, 3), p2: (0, 6, 3), p3: (-2, 6, 3))),
            (Stage.Strike, 1, P(up: 3, pitch: 2, abUp: 3, swell: 0.1f, gem: 0.7f, spike: 1, crown: 0.5f, p0: (4, 7, 1), p1: (1, 7, 1), p2: (0, 7, 1), p3: (-3, 7, 1))),
            (Stage.Rebound, 1, P(up: 1, pitch: 1, abUp: 2, swell: 0.04f, gem: 0.4f, spike: 0.6f, crown: 0.3f, p0: (3, 6, 0), p1: (1, 6, 0), p2: (0, 6, 0), p3: (-2, 6, 0))),
            (Stage.Held, 2, P(up: 1, pitch: 1, abUp: 2, swell: 0.03f, gem: 0.3f, spike: 0.4f, crown: 0.2f, p0: (3, 6, 0), p1: (1, 6, 0), p2: (0, 6, 0), p3: (-2, 6, 0))),
            (Stage.Recover, 9, Rest));

        // The two front legs lifted high and straight, then driven into the floor before it so the crystal rings.
        static readonly Pose Lifted = P(fwd: -4, up: 7, pitch: 24, crown: 0.35f, fang: 0.4f, p0: (-6, 2, 36), p1: (-2, 3, 6), p2: (0, 1, 0), p3: (-3, 2, 0));
        static readonly (Pose[] Frames, Stage[] Stages) StompBlow = Expand(Lifted,
            (Stage.Swing, 4, P(fwd: 4, up: -2, pitch: -8, crown: 0.5f, p0: (8, 3, 2), p1: (2, 3, 0), p2: (0, 1, 0), p3: (-1, 1, 0))),
            (Stage.Strike, 1, P(fwd: 5, up: -4, pitch: -12, crown: 0.6f, p0: (9, 3, 0), p1: (2, 3, 0), p2: (0, 1, 0), p3: (-1, 1, 0))),
            (Stage.Rebound, 1, P(fwd: 4, up: -2.5f, pitch: -8, crown: 0.4f, p0: (9, 3, 0), p1: (2, 3, 0), p2: (0, 1, 0), p3: (-1, 1, 0))),
            (Stage.Held, 2, P(fwd: 4, up: -2.5f, pitch: -8, crown: 0.3f, p0: (9, 3, 0), p1: (2, 3, 0), p2: (0, 1, 0), p3: (-1, 1, 0))),
            (Stage.Recover, 9, Rest));

        // Brooding: the abdomen lowered to the floor and swelling, its gems glowing with eggs; then pressed down as
        // they are laid, and up again.
        static readonly Pose Brooded = P(fwd: 2, up: -1, pitch: -10, abUp: -7, abFwd: -3, swell: 0.18f, gem: 1, crown: 0.3f, p0: (2, 3, 0), p1: (1, 3, 0), p2: (0, 3, 0), p3: (-1, 3, 0));
        static readonly (Pose[] Frames, Stage[] Stages) LayBlow = Expand(Brooded,
            (Stage.Swing, 2, P(fwd: 3, up: -2, pitch: -12, abUp: -9, abFwd: -4, swell: 0.22f, gem: 1, crown: 0.3f, p0: (2, 3, 0), p1: (1, 3, 0), p2: (0, 3, 0), p3: (-1, 3, 0))),
            (Stage.Strike, 1, P(fwd: 3, up: -1, pitch: -10, abUp: -6, abFwd: -3, swell: -0.02f, gem: 0.3f, crown: 0.3f, p0: (2, 3, 0), p1: (1, 3, 0), p2: (0, 3, 0), p3: (-1, 3, 0))),
            (Stage.Held, 2, P(fwd: 3, up: -1, pitch: -9, abUp: -5, abFwd: -3, swell: 0, gem: 0.2f, crown: 0.25f, p0: (2, 3, 0), p1: (1, 3, 0), p2: (0, 3, 0), p3: (-1, 3, 0))),
            (Stage.Recover, 9, Rest));

        // The screech: reared with the crown blazing and the front legs wide.
        static readonly Pose Screeching = P(fwd: -3, up: 8, pitch: 28, swell: 0.08f, fang: 1, crown: 1, p0: (-10, 12, 32), p1: (-3, 9, 14), p2: (0, 2, 0), p3: (-3, 2, 0));
        public const int ScreechRise = 14;

        static (Pose[] Frames, Stage[] Stages)? BlowOf(CrystalQueenPose pose) => pose switch
        {
            CrystalQueenPose.Bite => BiteBlow,
            CrystalQueenPose.Flash => FlashBlow,
            CrystalQueenPose.Burst => BurstBlow,
            CrystalQueenPose.Stomp => StompBlow,
            CrystalQueenPose.Lay => LayBlow,
            _ => null,
        };

        public static int FramesOf(CrystalQueenPose pose) => pose switch
        {
            CrystalQueenPose.Idle => 8,
            CrystalQueenPose.Walk => 12,
            CrystalQueenPose.Rear => 14,
            CrystalQueenPose.Crouch => 10,
            CrystalQueenPose.Spring => 4,
            CrystalQueenPose.Rush => 6,
            CrystalQueenPose.Skid => 9,
            CrystalQueenPose.Prism => 16,
            CrystalQueenPose.Bristle => 12,
            CrystalQueenPose.Lift => 14,
            CrystalQueenPose.Brood => 14,
            CrystalQueenPose.Screech => ScreechRise + 4,
            CrystalQueenPose.Flinch => 6,
            CrystalQueenPose.Die => 20,
            CrystalQueenPose.Enter => 1,
            _ => BlowOf(pose)?.Frames.Length ?? 1,
        };

        public static float FrameSeconds(CrystalQueenPose pose, int frame)
        {
            var stages = BlowOf(pose)?.Stages;
            return stages == null ? 0 : StageSeconds(stages[System.Math.Max(0, System.Math.Min(stages.Length - 1, frame))]);
        }

        public static bool Swinging(CrystalQueenPose pose, int frame)
        {
            var stages = BlowOf(pose)?.Stages;
            return stages != null && frame >= 0 && frame < stages.Length && (stages[frame] == Stage.Swing || stages[frame] == Stage.Strike);
        }

        public static int StrikeFrame(CrystalQueenPose pose) => System.Array.IndexOf(BlowOf(pose)?.Stages ?? new Stage[0], Stage.Strike);

        // The wind-up that leads into a blow.
        public static CrystalQueenPose WindUpOf(CrystalQueenPose blow) => blow switch
        {
            CrystalQueenPose.Bite => CrystalQueenPose.Rear,
            CrystalQueenPose.Flash => CrystalQueenPose.Prism,
            CrystalQueenPose.Burst => CrystalQueenPose.Bristle,
            CrystalQueenPose.Stomp => CrystalQueenPose.Lift,
            CrystalQueenPose.Lay => CrystalQueenPose.Brood,
            CrystalQueenPose.Rush => CrystalQueenPose.Crouch,
            _ => CrystalQueenPose.Idle,
        };

        // Walking, the legs take turns in two sets of four.
        static bool FirstSet(int leg) => (leg % 4 + leg / 4) % 2 == 0;

        static Pose PoseOf(CrystalQueenPose pose, int frame)
        {
            int count = FramesOf(pose);
            int f = ((frame % count) + count) % count;
            float t = count > 1 ? f / (float)(count - 1) : 0;
            float cycle = f / (float)count;
            float turn = cycle * 2 * (float)System.Math.PI;
            float s = (float)System.Math.Sin(turn), c = (float)System.Math.Cos(turn);
            switch (pose)
            {
                case CrystalQueenPose.Idle:
                {
                    // Breathing: the body rising a little, the abdomen swelling, the crown's light coming and going.
                    float b = 0.5f - 0.5f * c;
                    return P(up: 0.8f * b, abUp: 0.7f * b, swell: 0.03f * b, fang: 0.2f * b, crown: 0.1f + 0.2f * b, gem: 0.15f * b, l0: (0.5f * b, 0, 1.4f * System.Math.Max(0, s)));
                }
                case CrystalQueenPose.Walk:
                case CrystalQueenPose.Rush:
                {
                    // A stride each way per loop; she walks stately, the body level. Running is stretched out low.
                    bool run = pose == CrystalQueenPose.Rush;
                    float stride = run ? 7 : 5, lift = run ? 5 : 3.5f;
                    var p = run
                        ? P(fwd: 6, up: -2 + 0.5f * (float)System.Math.Cos(2 * turn), pitch: -5, abUp: 2 + 0.8f * (float)System.Math.Sin(2 * turn), fang: 0.6f, crown: 0.3f)
                        : P(up: 0.4f * (float)System.Math.Cos(2 * turn), pitch: 1, abUp: 0.7f * (float)System.Math.Sin(2 * turn - 1), abFwd: 0.4f * s, crown: 0.15f);
                    for (int leg = 0; leg < 8; leg++)
                    {
                        float at = (cycle + (FirstSet(leg) ? 0 : 0.5f)) % 1;
                        if (at < 0.5f)
                        {
                            float u = at / 0.5f;
                            p.F[leg] = -stride + 2 * stride * (u * u * (3 - 2 * u));
                            p.U[leg] = lift * (float)System.Math.Sin(u * System.Math.PI);
                        }
                        else p.F[leg] = stride - 2 * stride * ((at - 0.5f) / 0.5f);
                        if (run)
                        {
                            int pair = leg % 4;
                            p.F[leg] += pair == 0 ? 6 : pair == 1 ? -1 : pair == 2 ? -4 : -6;
                            p.O[leg] = pair == 0 ? -2 : -3;
                        }
                    }
                    return p;
                }
                case CrystalQueenPose.Rear:
                    return Keys(t,
                        (0, Rest),
                        (0.35f, P(fwd: -2.5f, up: 1, pitch: 8, fang: 0.4f, crown: 0.25f, p0: (-4, 2, 9), p1: (0, 1, 1.5f), p3: (-1, 1, 0))),
                        (0.7f, P(fwd: -5, up: 6, pitch: 24, fang: 0.9f, crown: 0.4f, p0: (-11, 6, 27), p1: (-2, 4, 10), p2: (-1, 1, 0), p3: (-3, 2, 0))),
                        (1, Reared));
                case CrystalQueenPose.Crouch:
                    return Keys(t,
                        (0, Rest),
                        (0.5f, P(fwd: -3, up: -2.5f, pitch: 1, fang: 0.2f, crown: 0.2f, p0: (-0.5f, 0.5f, 0), p3: (1, 0.5f, 0))),
                        (1, Crouched));
                case CrystalQueenPose.Spring:
                    return Lerp(Crouched, PoseOf(CrystalQueenPose.Rush, 0), (f + 1) / (float)(count + 1));
                case CrystalQueenPose.Skid:
                    return Keys(t,
                        (0, PoseOf(CrystalQueenPose.Rush, 0)),
                        (0.35f, Braked),
                        (1, Rest));
                case CrystalQueenPose.Prism:
                    // The head comes up first, then the legs open, and the crown fills with light all the while.
                    return Keys(t,
                        (0, Rest),
                        (0.3f, P(fwd: -1, up: 2, pitch: 9, crown: 0.4f, p0: (1, 3, 3), p1: (0, 1, 1))),
                        (0.65f, P(fwd: -2.5f, up: 4, pitch: 18, crown: 0.75f, p0: (3, 8, 11), p1: (1.5f, 6, 4), p3: (-1, 1, 0))),
                        (1, Prismed));
                case CrystalQueenPose.Bristle:
                    return Keys(t,
                        (0, Rest),
                        (0.4f, P(up: -1.5f, pitch: -2, abUp: 1, swell: 0.03f, gem: 0.4f, spike: 0.1f, crown: 0.2f, p0: (-2, -2, 1), p1: (-1, -2, 0), p2: (0.5f, -2, 0), p3: (1.5f, -1.5f, 0))),
                        (1, Bristled));
                case CrystalQueenPose.Lift:
                    // The left leg goes up a little before the right.
                    return Keys(t,
                        (0, Rest),
                        (0.35f, P(fwd: -1, up: 1, pitch: 6, crown: 0.2f, l0: (-3, 1, 12), r0: (-1, 1, 5), p1: (0, 1.5f, 1))),
                        (0.7f, P(fwd: -3, up: 5, pitch: 18, crown: 0.3f, fang: 0.3f, l0: (-5, 2, 30), r0: (-4, 2, 24), p1: (-1, 2.5f, 4), p3: (-2, 1, 0))),
                        (1, Lifted));
                case CrystalQueenPose.Brood:
                    return Keys(t,
                        (0, Rest),
                        (0.4f, P(fwd: 1, pitch: -4, abUp: -3, abFwd: -1, swell: 0.06f, gem: 0.4f, crown: 0.2f, p0: (1, 1.5f, 0), p1: (0.5f, 1.5f, 0), p2: (0, 1.5f, 0), p3: (-0.5f, 1.5f, 0))),
                        (1, Brooded));
                case CrystalQueenPose.Screech:
                    if (f < ScreechRise)
                        return Keys(f / (float)(ScreechRise - 1),
                            (0, Rest),
                            (0.4f, P(fwd: -1, up: 2, pitch: 8, swell: 0.03f, fang: 0.5f, crown: 0.5f, p0: (-4, 4, 10), p1: (0, 3, 2), p3: (-1, 1, 0))),
                            (1, Screeching));
                    {
                        float shake = (f - ScreechRise) % 2 == 0 ? 1 : -1;
                        var p = Lerp(Screeching, Screeching, 0);
                        p.U[0] += shake;
                        p.U[4] -= shake;
                        p.Swell += 0.02f * shake;
                        p.Up += 0.3f * shake;
                        p.Flare = 0.3f + 0.2f * shake;
                        return p;
                    }
                case CrystalQueenPose.Flinch:
                    return Keys(t,
                        (0, Rest),
                        (0.3f, P(fwd: -3, up: -1, pitch: 7, swell: -0.04f, fang: 0.5f, crown: 0.4f, p0: (-2, 1, 4), p1: (-1, 0, 0), p3: (1, 0, 0))),
                        (1, Rest));
                case CrystalQueenPose.Die:
                    return Keys(t,
                        (0, Rest),
                        (0.2f, P(fwd: -2, up: -3, pitch: 9, swell: -0.03f, fang: 0.8f, crown: 0.8f, p0: (-2, 1, 6), p1: (-1, 1, 0), p3: (1, 0, 0))),
                        (0.45f, P(up: -6, pitch: 2, swell: -0.06f, fang: 0.4f, crown: 0.2f, curl: 0.25f, p0: (-2, 2, 4), p1: (-1, 3, 5), p2: (1, 3, 5), p3: (2, 2, 4))),
                        (1, P(up: -6.5f, swell: -0.1f, curl: 1)));
                default:
                    return BlowOf(pose) is (Pose[] frames, Stage[] _) ? frames[System.Math.Max(0, System.Math.Min(frames.Length - 1, f))] : Rest;
            }
        }

        // ---- Laying it on the screen. ----

        struct Dot
        {
            public float X, Y, Deep;
        }

        sealed class Skeleton
        {
            public (float F, float R, float U) Chest, Abdomen, Mouth;
            public readonly (float F, float R, float U)[] Hip = new (float F, float R, float U)[8], Knee = new (float F, float R, float U)[8], Foot = new (float F, float R, float U)[8];
        }

        static Skeleton Solve(Pose p)
        {
            var body = new Skeleton();
            float pitch = p.Pitch * (float)System.Math.PI / 180;
            float cos = (float)System.Math.Cos(pitch), sin = (float)System.Math.Sin(pitch);
            var chest = (F: ChestAhead + p.Fwd, R: 0f, U: BodyHigh + p.Up);
            (float F, float R, float U) Carried(float ahead, float right, float up) => (chest.F + ahead * cos - up * sin, chest.R + right, chest.U + ahead * sin + up * cos);
            body.Chest = chest;
            var abdomen = Carried(-AbdomenBehind, 0, AbdomenAbove);
            body.Abdomen = (abdomen.F + p.AbFwd, 0, abdomen.U + p.AbUp);
            body.Mouth = Carried(9, 0, -2);
            for (int leg = 0; leg < 8; leg++)
            {
                int pair = leg % 4;
                float side = leg < 4 ? -1 : 1;
                double ha = HipAngle[pair] * System.Math.PI / 180, fa = FootAngle[pair] * System.Math.PI / 180;
                float hipAhead = HipOut * (float)System.Math.Cos(ha), hipOut = HipOut * (float)System.Math.Sin(ha);
                var hip = Carried(hipAhead, side * hipOut, -1);
                var foot = (F: ChestAhead + FootOut[pair] * (float)System.Math.Cos(fa) + p.F[leg], R: side * (FootOut[pair] * (float)System.Math.Sin(fa) + p.O[leg]), U: System.Math.Max(0, p.U[leg]));
                if (p.Tuck > 0)
                {
                    var under = (F: chest.F + hipAhead * 1.5f, R: side * (hipOut * 1.6f + 2), U: chest.U - 9);
                    foot = (foot.F + (under.F - foot.F) * p.Tuck, foot.R + (under.R - foot.R) * p.Tuck, foot.U + (under.U - foot.U) * p.Tuck);
                }
                if (p.Curl > 0)
                {
                    var over = (F: chest.F + hipAhead * 1.2f - 3, R: side * (hipOut * 0.9f + 1.5f), U: chest.U + 7);
                    foot = (foot.F + (over.F - foot.F) * p.Curl, foot.R + (over.R - foot.R) * p.Curl, foot.U + (over.U - foot.U) * p.Curl);
                }
                float dx = foot.F - hip.F, dy = foot.R - hip.R, dz = foot.U - hip.U;
                float reach = (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
                float most = Thigh + Shin - 0.5f;
                if (reach > most)
                {
                    foot = (hip.F + dx * most / reach, hip.R + dy * most / reach, hip.U + dz * most / reach);
                    dx = foot.F - hip.F;
                    dy = foot.R - hip.R;
                    dz = foot.U - hip.U;
                    reach = most;
                }
                reach = System.Math.Max(reach, System.Math.Abs(Thigh - Shin) + 0.5f);
                float along = (Thigh * Thigh - Shin * Shin + reach * reach) / (2 * reach);
                float off = (float)System.Math.Sqrt(System.Math.Max(0, Thigh * Thigh - along * along));
                float ux = dx / reach, uy = dy / reach, uz = dz / reach;
                float nx = -uz * ux, ny = -uz * uy, nz = 1 - uz * uz;
                float n = (float)System.Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (n < 0.15f)
                {
                    nx = (float)System.Math.Cos(ha);
                    ny = side * (float)System.Math.Sin(ha);
                    nz = 0;
                    n = 1;
                }
                body.Hip[leg] = hip;
                body.Foot[leg] = foot;
                body.Knee[leg] = (hip.F + ux * along + nx / n * off, hip.R + uy * along + ny / n * off, hip.U + uz * along + nz / n * off);
            }
            return body;
        }

        static readonly Rgba Ink = new Rgba(0, 0, 8);
        static readonly Rgba Glint = new Rgba(254, 254, 254);
        static readonly Rgba IceLight = new Rgba(213, 247, 251);
        static readonly Rgba IceMain = new Rgba(134, 198, 223);
        static readonly Rgba IceShade = new Rgba(100, 160, 196);
        static readonly Rgba IceDark = new Rgba(57, 112, 145);
        static readonly Rgba FarLight = new Rgba(174, 214, 227);
        static readonly Rgba FarMain = new Rgba(121, 183, 211);
        static readonly Rgba FarDark = new Rgba(89, 142, 187);
        static readonly Rgba GemPink = new Rgba(237, 169, 208);
        static readonly Rgba GemDeep = new Rgba(178, 50, 126);
        static readonly Rgba GemHot = new Rgba(255, 120, 200);
        static readonly Rgba Blush = new Rgba(254, 226, 248);
        static readonly Rgba Streak = new Rgba(220, 236, 250);

        static bool IsPink(Rgba c) => c.A > 0 && c.R > c.B + 15 && c.R > 150;

        // One leg: a faceted shaft of clear crystal, thick at the hip and sharpening to a point, a glint along the lit
        // edge, a ridge down the shin and a pink gem at the knee; outlined.
        static void Leg(PixelImage image, bool[,] mask, Dot hip, Dot knee, Dot foot, bool far, int phase)
        {
            System.Array.Clear(mask, 0, mask.Length);
            int w = image.Width, h = image.Height;
            void Stamp(float x, float y, float radius)
            {
                int x0 = (int)System.Math.Floor(x - radius), x1 = (int)System.Math.Ceiling(x + radius);
                int y0 = (int)System.Math.Floor(y - radius), y1 = (int)System.Math.Ceiling(y + radius);
                for (int py = y0; py <= y1; py++)
                    for (int px = x0; px <= x1; px++)
                        if (px >= 1 && py >= 1 && px < w - 1 && py < h - 1 && (px - x) * (px - x) + (py - y) * (py - y) <= radius * radius) mask[px, py] = true;
            }
            void Bone(Dot a, Dot b, float from, float to)
            {
                float length = (float)System.Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
                int steps = System.Math.Max(1, (int)(length * 2));
                for (int i = 0; i <= steps; i++)
                {
                    float u = i / (float)steps;
                    Stamp(a.X + (b.X - a.X) * u, a.Y + (b.Y - a.Y) * u, from + (to - from) * u);
                }
            }
            Bone(hip, knee, 1.9f, 1.6f);
            Bone(knee, foot, 1.7f, 0.45f);
            var light = far ? FarLight : IceLight;
            var main = far ? FarMain : IceMain;
            var dark = far ? FarDark : IceDark;
            int minX = (int)System.Math.Max(1, System.Math.Min(hip.X, System.Math.Min(knee.X, foot.X)) - 3), maxX = (int)System.Math.Min(w - 2, System.Math.Max(hip.X, System.Math.Max(knee.X, foot.X)) + 3);
            int minY = (int)System.Math.Max(1, System.Math.Min(hip.Y, System.Math.Min(knee.Y, foot.Y)) - 3), maxY = (int)System.Math.Min(h - 2, System.Math.Max(hip.Y, System.Math.Max(knee.Y, foot.Y)) + 3);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    if (mask[x, y]) continue;
                    if (mask[x - 1, y] || mask[x + 1, y] || mask[x, y - 1] || mask[x, y + 1]) image[x, y] = Ink;
                }
            float shin = (float)System.Math.Sqrt((foot.X - knee.X) * (foot.X - knee.X) + (foot.Y - knee.Y) * (foot.Y - knee.Y));
            float thigh = (float)System.Math.Sqrt((knee.X - hip.X) * (knee.X - hip.X) + (knee.Y - hip.Y) * (knee.Y - hip.Y));
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    if (!mask[x, y]) continue;
                    bool top = !mask[x, y - 1], bottom = !mask[x, y + 1];
                    var c = top && !bottom ? light : bottom && !top ? dark : main;
                    // The glint: every few pixels of the lit edge catch the light.
                    if (top && !bottom && !far && (x + 2 * y) % 4 == 0) c = Glint;
                    // A ridge down the middle of the shin, its facet in shade below.
                    if (shin > 2)
                    {
                        float a = ((x - knee.X) * (foot.X - knee.X) + (y - knee.Y) * (foot.Y - knee.Y)) / shin;
                        float across = ((x - knee.X) * -(foot.Y - knee.Y) + (y - knee.Y) * (foot.X - knee.X)) / shin;
                        if (a > 1.5f && a < shin - 1 && !top && !bottom && System.Math.Abs(across) < 0.55f) c = far ? main : light;
                    }
                    if (thigh > 2 && !top && !bottom)
                    {
                        float a = ((x - hip.X) * (knee.X - hip.X) + (y - hip.Y) * (knee.Y - hip.Y)) / thigh;
                        if (a > thigh * 0.45f && a < thigh * 0.55f) c = far ? dark : IceShade;
                    }
                    image[x, y] = c;
                }
            // The gem at the knee: brighter as the fight goes on.
            int kx = (int)System.Math.Round(knee.X), ky = (int)System.Math.Round(knee.Y);
            var gem = phase >= 3 ? GemHot : GemPink;
            if (image.Inside(kx, ky)) image[kx, ky] = far ? GemDeep : gem;
            if (!far && image.Inside(kx + 1, ky)) image[kx + 1, ky] = GemDeep;
            if (!far && image.Inside(kx, ky - 1)) image[kx, ky - 1] = phase >= 2 ? Blush : gem;
            int fx = (int)System.Math.Round(foot.X), fy = (int)System.Math.Round(foot.Y);
            if (image.Inside(fx, fy) && mask[fx, fy]) image[fx, fy] = dark;
        }

        // A shaded lump of crystal (under the head, and the waist between head and abdomen).
        static void Lump(PixelImage image, float cx, float cy, float rx, float ry)
        {
            for (int y = (int)(cy - ry - 1); y <= (int)(cy + ry + 1); y++)
                for (int x = (int)(cx - rx - 1); x <= (int)(cx + rx + 1); x++)
                {
                    if (!image.Inside(x, y)) continue;
                    float d = (x - cx) * (x - cx) / (rx * rx) + (y - cy) * (y - cy) / (ry * ry);
                    float edge = (x - cx) * (x - cx) / ((rx + 1) * (rx + 1)) + (y - cy) * (y - cy) / ((ry + 1) * (ry + 1));
                    if (d <= 1) image[x, y] = y < cy - ry * 0.3f ? IceLight : IceShade;
                    else if (edge <= 1) image[x, y] = Ink;
                }
        }

        // A part of the picture with its anchor at (cx, cy), grown or shrunk about it; lit toward white by glow.
        static void Lay(PixelImage image, PixelImage part, float midX, float midY, float cx, float cy, float scale, float glow, ref int top, ref int bottom)
        {
            int halfW = (int)System.Math.Ceiling(part.Width * scale), halfH = (int)System.Math.Ceiling(part.Height * scale);
            int baseX = (int)System.Math.Round(cx - midX), baseY = (int)System.Math.Round(cy - midY);
            for (int y = (int)(cy - halfH); y <= (int)(cy + halfH); y++)
                for (int x = (int)(cx - halfW); x <= (int)(cx + halfW); x++)
                {
                    if (!image.Inside(x, y)) continue;
                    int sx, sy;
                    if (System.Math.Abs(scale - 1) < 0.005f)
                    {
                        sx = x - baseX;
                        sy = y - baseY;
                    }
                    else
                    {
                        sx = (int)System.Math.Floor(midX + (x - (baseX + midX)) / scale + 0.5f);
                        sy = (int)System.Math.Floor(midY + (y - (baseY + midY)) / scale + 0.5f);
                    }
                    if (sx < 0 || sy < 0 || sx >= part.Width || sy >= part.Height) continue;
                    var c = part[sx, sy];
                    if (c.A == 0) continue;
                    if (glow > 0 && IsPink(c)) c = c.Mix(Blush, System.Math.Min(1, glow * 0.6f));
                    image[x, y] = c;
                    top = System.Math.Min(top, y);
                    bottom = System.Math.Max(bottom, y);
                }
        }

        static void Line(PixelImage image, float x0, float y0, float x1, float y1, Rgba color, bool over = true)
        {
            int steps = System.Math.Max(1, (int)(System.Math.Max(System.Math.Abs(x1 - x0), System.Math.Abs(y1 - y0)) * 1.5f));
            for (int i = 0; i <= steps; i++)
            {
                int x = (int)System.Math.Round(x0 + (x1 - x0) * i / steps), y = (int)System.Math.Round(y0 + (y1 - y0) * i / steps);
                if (!image.Inside(x, y)) continue;
                if (color.A == 255) image[x, y] = color;
                else if (over || image[x, y].A == 0) image.Blend(x, y, color);
            }
        }

        // A four-pointed sparkle.
        static void Sparkle(PixelImage image, float cx, float cy, int arm, Rgba color)
        {
            int x = (int)System.Math.Round(cx), y = (int)System.Math.Round(cy);
            for (int k = -arm; k <= arm; k++)
            {
                var c = System.Math.Abs(k) == arm && arm > 1 ? color.WithAlpha(140) : color;
                if (image.Inside(x + k, y)) image.Blend(x + k, y, c);
                if (k != 0 && image.Inside(x, y + k)) image.Blend(x, y + k, c);
            }
        }

        public static PixelImage Draw(BossView view, CrystalQueenPose pose, int frame, int phase) => Render(view, pose, frame, phase, out _);

        // Where its feet, mouth, crown and abdomen are in a frame (picture pixels) and the rows its body covers: for the
        // preview's check that nothing jumps from one frame to the next, and for effects at its crown and abdomen.
        public struct Joints
        {
            public (float X, float Y)[] Feet;
            public float MouthX, MouthY, AbdomenX, AbdomenY, CrownX, CrownY;
            public int Top, Bottom;
        }

        public static Joints JointsOf(BossView view, CrystalQueenPose pose, int frame)
        {
            Render(view, pose, frame, 1, out var joints);
            return joints;
        }

        static PixelImage Render(BossView view, CrystalQueenPose pose, int frame, int phase, out Joints joints)
        {
            var p = PoseOf(pose, frame);
            var rig = RigOf(view);
            var body = Solve(p);
            double angle = rig.Angle * System.Math.PI / 180;
            float cos = (float)System.Math.Cos(angle), sin = (float)System.Math.Sin(angle);
            Dot Put((float F, float R, float U) at)
            {
                float gx = at.F * cos - at.R * sin, gy = at.F * sin + at.R * cos;
                return new Dot { X = OriginX + gx, Y = OriginY + gy * Deep - at.U * Tall, Deep = gy };
            }
            var image = new PixelImage(Width, Height);
            var chest = Put(body.Chest);
            var abdomenRest = Put((ChestAhead - AbdomenBehind, 0, BodyHigh + AbdomenAbove));
            var chestRest = Put((ChestAhead, 0, BodyHigh));
            var abdomenNow = Put(body.Abdomen);
            float abX = chest.X + rig.AbOffX + (abdomenNow.X - chest.X) - (abdomenRest.X - chestRest.X);
            float abY = chest.Y + rig.AbOffY + (abdomenNow.Y - chest.Y) - (abdomenRest.Y - chestRest.Y);
            var mouthRest = Put((ChestAhead + 9, 0, BodyHigh - 2));
            var mouthNow = Put(body.Mouth);
            float lean = 0.6f;
            float hdX = chest.X + ((mouthNow.X - chest.X) - (mouthRest.X - chestRest.X)) * lean;
            float hdY = chest.Y + ((mouthNow.Y - chest.Y) - (mouthRest.Y - chestRest.Y)) * lean;
            float mouthX = chest.X + rig.MouthX + (mouthNow.X - chest.X) - (mouthRest.X - chestRest.X);
            float mouthY = chest.Y + rig.MouthY + (mouthNow.Y - chest.Y) - (mouthRest.Y - chestRest.Y);
            // The crown sits on the head and tips back a little as the head comes up.
            float crX = hdX + rig.CrownX - p.Pitch * 0.04f * cos, crY = hdY + rig.CrownY;

            var hips = new Dot[8];
            var knees = new Dot[8];
            var feet = new Dot[8];
            for (int leg = 0; leg < 8; leg++)
            {
                hips[leg] = Put(body.Hip[leg]);
                knees[leg] = Put(body.Knee[leg]);
                feet[leg] = Put(body.Foot[leg]);
            }
            var crown = PartOf(view, 2);
            joints = new Joints { Feet = new (float X, float Y)[8], MouthX = mouthX, MouthY = mouthY, AbdomenX = abX, AbdomenY = abY, CrownX = crX, CrownY = crY - crown.Height * 0.6f, Top = int.MaxValue, Bottom = -1 };
            for (int leg = 0; leg < 8; leg++) joints.Feet[leg] = (feet[leg].X, feet[leg].Y);

            float abdomenDeep = abdomenNow.Deep + 0.1f, chestDeep = chest.Deep;
            var order = new List<(float Deep, int What)>();
            // The legs are thick, so the near ones go under the head (their hips are hidden by it) unless lifted high
            // over it; the head and its crown come last.
            for (int leg = 0; leg < 8; leg++)
                order.Add((body.Foot[leg].U > 10 && hips[leg].Deep >= chest.Deep ? chestDeep + 100 + leg : hips[leg].Deep * 1.2f + (hips[leg].Deep < chest.Deep ? -6 : 3), leg));
            // Seen from behind the abdomen is nearest and covers the head; otherwise the head is.
            bool abdomenNear = abdomenDeep > chestDeep;
            order.Add((abdomenNear ? chestDeep + 60 : abdomenDeep, 8));
            order.Add((abdomenNear ? chestDeep + 50 : chestDeep + 50, 9));
            order.Sort((a, b) => a.Deep.CompareTo(b.Deep));

            // The streaks of a frame too fast to follow.
            if (p.Before != null)
            {
                var was = Solve(p.Before);
                for (int leg = 0; leg < 8; leg++)
                {
                    var from = Put(was.Foot[leg]);
                    float moved = (float)System.Math.Sqrt((from.X - feet[leg].X) * (from.X - feet[leg].X) + (from.Y - feet[leg].Y) * (from.Y - feet[leg].Y));
                    if (moved < 5) continue;
                    int steps = (int)moved;
                    for (int i = 0; i < steps; i++)
                    {
                        float u = i / (float)steps;
                        Line(image, knees[leg].X, knees[leg].Y, from.X + (feet[leg].X - from.X) * u, from.Y + (feet[leg].Y - from.Y) * u, Streak.WithAlpha((int)(30 + 110 * u)), false);
                    }
                }
                var chestWas = Put(was.Chest);
                float carried = (float)System.Math.Sqrt((chestWas.X - chest.X) * (chestWas.X - chest.X) + (chestWas.Y - chest.Y) * (chestWas.Y - chest.Y));
                if (carried > 3)
                    for (int i = -2; i <= 2; i++)
                        Line(image, chestWas.X + (chestWas.X - chest.X) * 0.8f, chestWas.Y + (chestWas.Y - chest.Y) * 0.8f + i * 3 - 4, chest.X, chest.Y + i * 3 - 4, Streak.WithAlpha(i % 2 == 0 ? 120 : 70), false);
            }

            var mask = new bool[Width, Height];
            int top = int.MaxValue, bottom = -1;
            bool faceHidden = view == BossView.Back || view == BossView.BackSide;
            float gemGlow = p.Gem + (phase >= 3 ? 0.35f : phase >= 2 ? 0.15f : 0);
            foreach (var (_, what) in order)
            {
                if (what < 8)
                {
                    Leg(image, mask, hips[what], knees[what], feet[what], hips[what].Deep < chest.Deep - 0.5f, phase);
                    continue;
                }
                if (what == 8)
                {
                    Lump(image, (hdX + abX) / 2, (hdY + abY) / 2 + 1, 4, 3.5f);
                    if (p.Spike > 0.02f) Spikes(image, abX, abY, 11 * (1 + p.Swell), 8 * (1 + p.Swell), p.Spike, behind: true);
                    Lay(image, PartOf(view, 0), rig.AbMidX, rig.AbMidY, abX, abY, 1 + p.Swell, gemGlow, ref top, ref bottom);
                    if (p.Spike > 0.02f) Spikes(image, abX, abY, 11 * (1 + p.Swell), 8 * (1 + p.Swell), p.Spike, behind: false);
                    if (gemGlow > 0.3f)
                    {
                        // Eggs of light deep in the abdomen.
                        int n = 3;
                        for (int k = 0; k < n; k++)
                        {
                            float a = (k * 2.1f + frame * 0.35f) % 6.28f;
                            Sparkle(image, abX + 6 * (float)System.Math.Cos(a), abY + 3 * (float)System.Math.Sin(a), gemGlow > 0.8f ? 2 : 1, Glint.WithAlpha((int)(120 + 120 * System.Math.Min(1, gemGlow))));
                        }
                    }
                    continue;
                }
                Lump(image, hdX, hdY + 1, 6.5f, 5);
                if (view == BossView.BackSide) Fangs(image, mouthX, mouthY, cos, sin, p);
                Lay(image, PartOf(view, 1), rig.HdMidX, rig.HdMidY, hdX, hdY, 1, 0, ref top, ref bottom);
                if (!faceHidden) Fangs(image, mouthX, mouthY, cos, sin, p);
                float crownGlow = p.Crown + (phase >= 3 ? 0.3f : phase >= 2 ? 0.15f : 0);
                Lay(image, crown, (crown.Width - 1) / 2f, crown.Height - 1, crX, crY, 1, crownGlow, ref top, ref bottom);
            }
            joints.Top = top;
            joints.Bottom = bottom;

            // The crown's light: a sparkle over it as it fills, rays when it flares.
            float lit = System.Math.Min(1, p.Crown + (phase >= 3 ? 0.3f : 0));
            float cx = crX, cy = crY - crown.Height * 0.6f;
            if (lit > 0.35f) Sparkle(image, cx + (frame % 3 - 1), cy - 3, lit > 0.8f ? 3 : 2, Glint);
            if (p.Flare > 0.05f)
            {
                for (int k = 0; k < 8; k++)
                {
                    float a = k * (float)System.Math.PI / 4 + 0.2f;
                    float r0 = 4, r1 = 4 + 12 * p.Flare * (k % 2 == 0 ? 1 : 0.6f);
                    Line(image, cx + r0 * (float)System.Math.Cos(a), cy + r0 * (float)System.Math.Sin(a), cx + r1 * (float)System.Math.Cos(a), cy + r1 * (float)System.Math.Sin(a),
                        (k % 2 == 0 ? Glint : GemPink).WithAlpha((int)(90 + 150 * p.Flare)), true);
                }
                Sparkle(image, cx, cy, 4, Glint);
            }
            return image;
        }

        // Shards standing out of the abdomen's crystal, round its rim: the far ones behind it, the near ones in front.
        static void Spikes(PixelImage image, float cx, float cy, float rx, float ry, float amount, bool behind)
        {
            for (int k = 0; k < 9; k++)
            {
                float a = -2.6f + k * 0.65f;
                float sy = (float)System.Math.Sin(a);
                if ((sy < -0.2f) != behind) continue;
                float bx = cx + rx * 0.8f * (float)System.Math.Cos(a), by = cy + ry * 0.8f * sy;
                float len = (3 + 3 * (k % 3 == 1 ? 1 : 0.5f)) * amount;
                float tx = bx + len * (float)System.Math.Cos(a), ty = by + len * (float)System.Math.Sin(a) - 2 * amount;
                float px = -(ty - by), py = tx - bx;
                float pl = (float)System.Math.Sqrt(px * px + py * py);
                if (pl < 0.1f) continue;
                px /= pl;
                py /= pl;
                Line(image, bx + px * 1.6f, by + py * 1.6f, tx, ty, Ink);
                Line(image, bx - px * 1.6f, by - py * 1.6f, tx, ty, Ink);
                Line(image, bx + px * 0.8f, by + py * 0.8f, tx - px * 0.3f, ty - py * 0.3f, IceLight);
                Line(image, bx - px * 0.6f, by - py * 0.6f, tx, ty, k % 2 == 0 ? IceMain : GemPink);
                Line(image, bx, by, tx, ty, k % 2 == 0 ? Glint : Blush);
            }
        }

        // The fangs under its head: small hooks of pink crystal; closed they point down and in, open they spread.
        static void Fangs(PixelImage image, float mx, float my, float cos, float sin, Pose p)
        {
            float rx = -sin, ry = cos * Deep, ax = cos, ay = sin * Deep;
            for (int side = -1; side <= 1; side += 2)
            {
                float bx = mx + rx * side * 2.2f, by = my + ry * side * 2.2f;
                float spread = 0.4f + 2.2f * p.Fang, reach = 0.5f + 2.6f * p.Fang, drop = 3 - 0.7f * p.Fang;
                float tx = bx + rx * side * spread + ax * reach, ty = by + ry * side * spread + ay * reach + drop;
                Line(image, bx, by - 1, (bx + tx) / 2 + rx * side * 0.8f, (by + ty) / 2, Ink);
                Line(image, bx, by, (bx + tx) / 2 + rx * side * 0.8f, (by + ty) / 2 + 1, GemDeep);
                Line(image, (bx + tx) / 2 + rx * side * 0.8f, (by + ty) / 2 + 1, tx, ty, p.Fang > 0.5f ? Blush : GemPink);
            }
        }
    }
}
