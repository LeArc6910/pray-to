using System.Collections.Generic;

namespace Game.Client
{
    // What the silk mother is doing, which picks its frame (WorldView.Spider.cs). Each attack has a wind-up of its own
    // that the whole body takes part in, and a blow: reared up on its back legs with the front pair high and the fangs
    // spread, then the lunge and bite (Rear, Bite); the front legs cocked back over its head one higher than the other,
    // then driven down one after another (Cock, Stab); the abdomen swung up over its back with the spinnerets forward,
    // then the silk thrown (Spin, Shoot); pressed flat with the knees high, then the spring (Crouch, Rush, Skid); drawn
    // back with the head up and the abdomen pumping, then the spit (Gape, Spit); legs gathered and hauled up its own
    // thread out of sight, then down on its mark (Gather, Rise, Land); sat on its belly with all eight legs lifted like
    // a crown, then all eight driven down round it (Splay, Slam); and a screech, a flinch and its death, curled up.
    enum SpiderPose
    {
        Idle,
        Walk,
        Rear,
        Bite,
        Cock,
        Stab,
        Spin,
        Shoot,
        Crouch,
        Spring,
        Rush,
        Skid,
        Gape,
        Spit,
        Gather,
        Rise,
        Land,
        Splay,
        Slam,
        Screech,
        Flinch,
        Die,
    }

    // The silk mother of the forgotten shrine (2026-11-13, Docs/Bosses.md 실 잣는 어미 아라네): a great cave spider, its
    // swollen abdomen marked in sickly green. The abdomen and the head-and-chest of its five views come from one Retro
    // Diffusion Pro picture turned eight ways (Docs/ArtSources.md, $0.28). The eight legs are not the picture's: each
    // is drawn here from its hip to where its foot stands, bent at the knee (two bones, solved in three dimensions and
    // laid on the screen for the view), so every leg steps, lifts, stabs and curls by itself; the body rides on them:
    // carried forward and back, raised and dropped, pitched head up or down, the abdomen swung and swelling, the fangs
    // opening. Faces right; WorldView flips it for the left.
    static class SpiderArt
    {
        public const int Width = 120;
        public const int Height = 92;
        // Rows under the middle of its body for the legs nearest the viewer; WorldView stands the picture on this row.
        public const int Floor = 26;
        // How tall the body itself stands, for the name and marks over it.
        public const int BodyHeight = 40;

        static readonly Dictionary<char, Rgba> Palette = new Dictionary<char, Rgba>
        {
            ['a'] = new Rgba(0, 0, 0),
            ['b'] = new Rgba(1, 0, 4),
            ['c'] = new Rgba(2, 0, 4),
            ['d'] = new Rgba(15, 0, 16),
            ['e'] = new Rgba(20, 11, 28),
            ['f'] = new Rgba(32, 21, 41),
            ['g'] = new Rgba(34, 25, 41),
            ['h'] = new Rgba(39, 25, 49),
            ['i'] = new Rgba(42, 30, 46),
            ['j'] = new Rgba(45, 31, 54),
            ['k'] = new Rgba(50, 34, 58),
            ['l'] = new Rgba(56, 38, 65),
            ['m'] = new Rgba(58, 41, 64),
            ['n'] = new Rgba(63, 39, 73),
            ['o'] = new Rgba(65, 46, 73),
            ['p'] = new Rgba(67, 50, 71),
            ['q'] = new Rgba(74, 47, 85),
            ['r'] = new Rgba(70, 53, 79),
            ['s'] = new Rgba(74, 54, 84),
            ['t'] = new Rgba(73, 57, 77),
            ['u'] = new Rgba(80, 55, 82),
            ['v'] = new Rgba(78, 60, 84),
            ['w'] = new Rgba(128, 199, 71),
            ['x'] = new Rgba(159, 227, 93),
            ['y'] = new Rgba(178, 240, 105),
        };

        // Its middle is at (11.5, 8.5) of these rows.
        static readonly string[] FrontAbdomenRows =
        {
            ".........bbbaba.........",
            "......bbbfllllhbbb......",
            ".....chhlknmmnlmhhbc....",
            "....blnosqoyyqqqqnlb....",
            "...blnmytspyypqsykllb...",
            "..bknmvyttyxwxppyvllhb..",
            ".bfnnywpstxyyyoopwxonhc.",
            ".bhnmxmnqtyyyypqqoxonhb.",
            "chhnnmlnqtwyywqqqomnnhfa",
            "chhnnoymqtxyyxrqkymnnffa",
            "chhnnlvxrrxyyxrqyvlnnffa",
            "ahhnnnvxpqtyxpqoyvlnnffa",
            ".ffnnnnvxrmwyoqywmnnnff.",
            ".ffhnnnmwtrpvopwmnnnhff.",
            "..fhnnnnmvtttrtmnnnlfh..",
            "...ffnnnnnnnnnlnnnnff...",
            "....ffhnnnnnnlnnnfff....",
            "......ffhhbbbbhhff......",
            "..........kkkj..........",
        };

        // Its middle is at (8.5, 5.0) of these rows.
        static readonly string[] FrontThoraxRows =
        {
            "..fffbb....bbfff..",
            ".fbgbhovvvvjhbfaf.",
            "efabfhkhvvkkklbbff",
            "bbagfjuttvuukkjbab",
            "fbfffhdettdjhhhhbh",
            "fbfffbebbabcbfhfbf",
            "bbffbjsttttvfbffbb",
            "fbhbjqqnuukqqhbfbi",
            ".gbavhfvknvhjvcae.",
            "..avenhenndjneva..",
            "..bhhdnvfnwnejfb..",
            "..abfheeqqeehhba..",
            "..aahkhjqujhhhba..",
            "..afbbvthettbbia..",
            "..lhaavtccp.aahm..",
            "...fea.tca..aee...",
            "...lca.aaaa.abk...",
            "......aa..aav.....",
        };

        // Its middle is at (11.2, 9.2) of these rows.
        static readonly string[] FrontSideAbdomenRows =
        {
            ".........bbaa..........",
            "......abbjkkkbaa.......",
            "....cahhkluooolkc......",
            "...cchkqqqqsvynnlba....",
            "..ceknnsysssqqxxnlha...",
            "..bhlnqsyyywrqqoxoohb..",
            ".bfnnosswxxyyrsqmvnlhb.",
            ".cfnnqssxyyyyvrqvmnngav",
            "cfhnqnqssvxyyyrqryoohfa",
            "affnoynqqqwyyyxoqryojgb",
            "affnnuyoqqqrxyyxrqxonfb",
            "affnnquyvnoqrxyyoqonnfc",
            "affnnnsuqyqqqowyonnnlff",
            ".fflnonussyqqooronnnfff",
            ".cffnoolqqsyvsllnnnmff.",
            "..fffnonnnnovslnnnfff..",
            "...fffnnnnnnnnnnnbaa...",
            ".....fffjnnnnnnjbfk....",
            ".......fffffffhaj......",
        };

        // Its middle is at (8.5, 5.0) of these rows.
        static readonly string[] FrontSideThoraxRows =
        {
            "..........aaff.....",
            ".........lmjaaaj...",
            ".......mvvrjlkech..",
            ".fffbhmvkvvvvlkaf..",
            "fffahjllnsvvhfaaac.",
            "bbacfhlllllfbaroaf.",
            "gaahghklljbcsvlkjb.",
            ".baghgjkkalvvskkn..",
            ".bhafffhblvqlkknej.",
            "..eafffckqlkqsnl.ja",
            "....bffbnnnnjdnqefa",
            "........hlefk.nqhck",
            "........fflkfejhrte",
            ".........fffhlhaj..",
            "..........ccivraa..",
        };

        // Its middle is at (11.7, 10.0) of these rows.
        static readonly string[] SideAbdomenRows =
        {
            ".........aaaaaaa........",
            "......cjjjkklkkiaa......",
            "....chjorxxwpxyykhaa....",
            "...cjlssttttsttvyokdb...",
            "..chlssyyxxxsttruyvjbl..",
            "..hkqssxyyxyyyprrrykfcl.",
            ".hklqsstxxxyyyxxoooohch.",
            ".flnqsvvvvxxyyyyywookeda",
            "bhlqqsvvvvvvvxxxyywmlfba",
            "ahnqquyyxsvvvtvtvtwvlffa",
            "bfnqqsuusyxsooorrrrrnffa",
            "afnnqqsssvsqyyywsolnnfea",
            "bffnlqqssssssvvssllnnfbo",
            ".ffnnnqqussusonnnnnffbkv",
            ".bfffnnnssuuuqnnnnlfbho.",
            "..ffffnnnnnnnnnnnfffahl.",
            "..afffffnnnnnnnffffbhh..",
            "...fffffffbaaafffffah...",
            "....affffbobojaffgab....",
            "......ffgbmamlbggg......",
            ".........rfafbcb........",
        };

        // Its middle is at (7.0, 5.5) of these rows.
        static readonly string[] SideThoraxRows =
        {
            "....klbbb..........",
            "....vkvlrka........",
            "....jvvvmjaa.......",
            "...tomvvcbrvcha....",
            "...srvjjbrvklnna...",
            "..lllllbevvlkkufa..",
            ".flllllbvvlnuknec..",
            "effhllkbvsnnfhnja..",
            "efffffcdolnenvekva.",
            ".fffffbonnekddefj.a",
            "..fffbfjlnnhjheaaa.",
            "....bkaff.fffvtda..",
            "...........fc..v...",
        };

        // Its middle is at (11.0, 10.8) of these rows.
        static readonly string[] BackSideAbdomenRows =
        {
            "...........a...........",
            ".......jmooxxxxt.......",
            ".....flvxorrrooool.....",
            "....hlxvsssvvxyyxxw....",
            "...hlwrsuvxyyyyyxsol...",
            "..aknsssvyyyyyyvssqnk..",
            ".aflouvvyyyxxsssvsqxmf.",
            ".aflovvyyxxwssvvswyrnj.",
            ".afnovvvvvvvsuvvyxonnjf",
            "aafnnvvvvvvvvxyvvqnnnff",
            "aafnnnvvvvvyyyqssunnnff",
            "hbfnnnsvvvvsuuqssunnlff",
            "haffnnnnuvussssssnnnfff",
            "gafffnnnnnuuuuunonoffff",
            "..afffnnononnnnnooffff.",
            "..afffffnnnnnnnnfffffa.",
            "...afffffffffgffffffar.",
            "...afgfgffgfffffffffa..",
            "....afffffffffffffff...",
            ".....abffgfffffffff....",
            ".......aaffffffgfa.....",
            ".........aaaaaa........",
        };

        // Its middle is at (4.5, 7.0) of these rows.
        static readonly string[] BackSideThoraxRows =
        {
            ".....acvhca.",
            "...llalacnca",
            ".kvlvlbffqfa",
            "lrnsouhalqnn",
            "bfuqsunfcvln",
            "hbqsnnllctl.",
            ".celllllcjv.",
            "..blllllhds.",
            "..bfllnnhcj.",
            "...bkklhffe.",
            "...afhhffc..",
            "...acffffe..",
            "...bcefff...",
            "...afbb.....",
        };

        // Its middle is at (11.3, 10.2) of these rows.
        static readonly string[] BackAbdomenRows =
        {
            "..........lllj.........",
            "......ixmowxroxke......",
            ".....myorsyytrpylkb....",
            "...hmymqrxyyxsrrxllb...",
            "..hnomnqsxyyxrsqolnna..",
            "..llymnqrxyyyssnoynnha.",
            ".fnnxmrssyyyysssmxnnla.",
            ".hnooyrssxxxxsssyrnnnfa",
            "bhnnnysvvvyyvvvvxsnnnfa",
            "bfnnnryvvvyyvvvyssnnnfa",
            "afnnnsvvvvvvvvvussnnnfa",
            "aflnnnvvvvvvvvuunnnnffa",
            "affnnnnuvvvvvuslnnnnffa",
            ".fffnnnnnovvvnnnnnnfffa",
            ".bfffnnnnnnnnnnnnffffbh",
            "..fffffnnnnnnnnnfffffc.",
            "..bfffffffffffffffffa..",
            "...fffffffffffffffffa..",
            "....affffffffffffgb....",
            "......afffffffffaa.....",
            "........aaaaaaaa.......",
        };

        // Its middle is at (6.0, 4.0) of these rows.
        static readonly string[] BackThoraxRows =
        {
            "....aaaa..b..",
            ".c.anfhna.bs.",
            "eabnonnokbbnl",
            "akbbaabbfkbhk",
            "acfk....cbabj",
            "...........bb",
        };

        // ---- The five views: the abdomen and the head-and-chest cut from the picture, where their middles are, how far
        // the abdomen's middle is from the chest's and the mouth from it (picture pixels), and the way the view faces
        // on the ground (degrees: 0 to the right, 90 toward the viewer). ----

        struct Rig
        {
            public string[] Abdomen, Thorax;
            public float AbMidX, AbMidY, ThMidX, ThMidY, AbOffX, AbOffY, MouthX, MouthY, Angle;
        }

        static Rig RigOf(BossView view) => view switch
        {
            BossView.Front => new Rig { Abdomen = FrontAbdomenRows, Thorax = FrontThoraxRows, AbMidX = 11.5f, AbMidY = 8.5f, ThMidX = 8.5f, ThMidY = 5, AbOffX = 0, AbOffY = -14.5f, MouthX = 0, MouthY = 10, Angle = 90 },
            BossView.FrontSide => new Rig { Abdomen = FrontSideAbdomenRows, Thorax = FrontSideThoraxRows, AbMidX = 11.2f, AbMidY = 9.2f, ThMidX = 8.5f, ThMidY = 5, AbOffX = -7.3f, AbOffY = -11.8f, MouthX = 6.5f, MouthY = 7.5f, Angle = 45 },
            BossView.Side => new Rig { Abdomen = SideAbdomenRows, Thorax = SideThoraxRows, AbMidX = 11.7f, AbMidY = 10, ThMidX = 7, ThMidY = 5.5f, AbOffX = -15.3f, AbOffY = -6.5f, MouthX = 9.5f, MouthY = 3.5f, Angle = 0 },
            BossView.BackSide => new Rig { Abdomen = BackSideAbdomenRows, Thorax = BackSideThoraxRows, AbMidX = 11, AbMidY = 10.8f, ThMidX = 4.5f, ThMidY = 7, AbOffX = -13.5f, AbOffY = 4.8f, MouthX = 5, MouthY = -5.5f, Angle = -45 },
            _ => new Rig { Abdomen = BackAbdomenRows, Thorax = BackThoraxRows, AbMidX = 11.3f, AbMidY = 10.2f, ThMidX = 6, ThMidY = 4, AbOffX = -0.7f, AbOffY = 10.2f, MouthX = 0, MouthY = -4.5f, Angle = -90 },
        };

        static readonly Dictionary<(BossView, bool), PixelImage> Parts = new Dictionary<(BossView, bool), PixelImage>();

        static PixelImage PartOf(BossView view, bool abdomen)
        {
            if (Parts.TryGetValue((view, abdomen), out var image)) return image;
            var rig = RigOf(view);
            image = PixelImage.FromRows(Palette, abdomen ? rig.Abdomen : rig.Thorax);
            Parts[(view, abdomen)] = image;
            return image;
        }

        // ---- The body in three dimensions, in pixels: ahead, to its right, up. The chest rides BodyHigh over the
        // ground a little ahead of the middle of the whole body (which is where the boss stands); each hip is on a ring
        // round the chest, each foot rests on a wider ring on the ground, and a leg is two bones bent upward at the
        // knee. Legs 0..3 are its left ones from the front back, 4..7 its right. ----

        const float ChestAhead = 4;
        const float BodyHigh = 7;
        const float Thigh = 11;
        const float Shin = 16;
        // How much the ground's depth and height show on the screen (looked down on from the front).
        const float Deep = 0.62f;
        const float Tall = 0.9f;
        // The abdomen's middle from the chest's: behind and a little above.
        const float AbdomenBehind = 15;
        const float AbdomenAbove = 4;

        static readonly float[] HipAngle = { 42, 74, 106, 138 };
        const float HipOut = 5.5f;
        static readonly float[] FootAngle = { 38, 66, 98, 128 };
        static readonly float[] FootOut = { 25, 23, 22, 25 };

        const int OriginX = Width / 2;
        const int OriginY = Height - 1 - Floor;

        // ---- A pose: the chest carried ahead (Fwd) and up (Up) from rest and pitched head up (Pitch, degrees); the
        // abdomen swung ahead and up of where the pitch leaves it and swollen (Swell, a share of its size); the fangs
        // open (0..1), venom glowing at the mouth and silk at the spinnerets (0..1), a thread running up from the
        // abdomen (0..1 of the way to the roof); and each foot moved from where it rests on the ground: ahead (F), out
        // from the body's line (O) and up (U). Tuck gathers the feet under the body wherever it is (hauled up its
        // thread), Curl folds them up over it (dead). ----

        sealed class Pose
        {
            public float Fwd, Up, Pitch, AbFwd, AbUp, Swell, Fang, Glow, Spin, Thread, Tuck, Curl;
            public readonly float[] F = new float[8], O = new float[8], U = new float[8];
            // The frame before a fast one, for the streaks its legs and body leave.
            public Pose Before;
        }

        static Pose P(float fwd = 0, float up = 0, float pitch = 0, float abFwd = 0, float abUp = 0, float swell = 0, float fang = 0, float glow = 0, float spin = 0,
            float thread = 0, float tuck = 0, float curl = 0,
            (float F, float O, float U)? p0 = null, (float F, float O, float U)? p1 = null, (float F, float O, float U)? p2 = null, (float F, float O, float U)? p3 = null,
            (float F, float O, float U)? l0 = null, (float F, float O, float U)? r0 = null, (float F, float O, float U)? l1 = null, (float F, float O, float U)? r1 = null)
        {
            var pose = new Pose { Fwd = fwd, Up = up, Pitch = pitch, AbFwd = abFwd, AbUp = abUp, Swell = swell, Fang = fang, Glow = glow, Spin = spin, Thread = thread, Tuck = tuck, Curl = curl };
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
            Set(1, l1);
            Set(5, r1);
            return pose;
        }

        static Pose Lerp(Pose a, Pose b, float u)
        {
            float L(float x, float y) => x + (y - x) * u;
            var pose = new Pose
            {
                Fwd = L(a.Fwd, b.Fwd), Up = L(a.Up, b.Up), Pitch = L(a.Pitch, b.Pitch), AbFwd = L(a.AbFwd, b.AbFwd), AbUp = L(a.AbUp, b.AbUp), Swell = L(a.Swell, b.Swell),
                Fang = L(a.Fang, b.Fang), Glow = L(a.Glow, b.Glow), Spin = L(a.Spin, b.Spin), Thread = L(a.Thread, b.Thread), Tuck = L(a.Tuck, b.Tuck), Curl = L(a.Curl, b.Curl),
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

        // A blow laid out from key poses: each stage moves from the pose before to its own over so many frames; a swing
        // speeds up into the strike and remembers the frame before it for its streaks, the rest ease in and out.
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

        // ---- The poses. Every motion starts from and comes back to the same rest (all eight feet on their ring). ----

        static readonly Pose Rest = P();

        // Reared: back on its hind legs, head high, the front pair lifted over its head and the second pair half up,
        // fangs spread.
        static readonly Pose Reared = P(fwd: -6, up: 9, pitch: 34, fang: 1, glow: 0.3f, p0: (-16, 7, 38), p1: (-4, 6, 18), p2: (-1, 1, 0), p3: (-4, 2, 0));

        // The lunge and the bite: the whole body thrown ahead and down, the front legs slapped down either side of what
        // it bites, the fangs closing; held there a moment, then drawn back.
        static readonly (Pose[] Frames, Stage[] Stages) BiteBlow = Expand(Reared,
            (Stage.Swing, 7, P(fwd: 9, up: -2, pitch: -10, fang: 1, glow: 0.5f, p0: (10, -2, 4), p1: (5, 1, 3), p2: (0, 1, 0), p3: (-2, 2, 0))),
            (Stage.Strike, 1, P(fwd: 14, up: -4, pitch: -18, fang: 0, glow: 0.8f, p0: (12, -5, 0), p1: (6, 1, 0), p2: (1, 1, 0), p3: (-1, 2, 0))),
            (Stage.Rebound, 1, P(fwd: 12.5f, up: -3, pitch: -14, fang: 0.2f, glow: 0.6f, p0: (12, -5, 0), p1: (6, 1, 0), p2: (1, 1, 0), p3: (-1, 2, 0))),
            (Stage.Held, 2, P(fwd: 13, up: -3.5f, pitch: -15, fang: 0.1f, glow: 0.4f, p0: (12, -5, 0), p1: (6, 1, 0), p2: (1, 1, 0), p3: (-1, 2, 0))),
            (Stage.Recover, 4, P(fwd: 5, up: -1, pitch: -4, fang: 0.4f, p0: (7, -2, 2.5f), p1: (3, 0.5f, 1.5f), p2: (0.5f, 0.5f, 0), p3: (-0.5f, 1, 0))),
            (Stage.Recover, 5, Rest));

        // Cocked: the front legs drawn back high over its head, the left higher than the right; the second pair braced
        // wide.
        static readonly Pose Cocked = P(fwd: -4, up: 6, pitch: 22, fang: 0.4f, l0: (-18, 7, 40), r0: (-12, 6, 30), p1: (2, 3, 0), p2: (0, 1, 0), p3: (-2, 1, 0));

        // Three stabs in a breath: the left front leg, the right, then the body's weight behind a second left; the
        // points stay in the floor a moment.
        static readonly (Pose[] Frames, Stage[] Stages) StabBlow = Expand(Cocked,
            (Stage.Swing, 3, P(fwd: 2, up: 2, pitch: 6, fang: 0.4f, l0: (10, -4, 0), r0: (-12, 6, 32), p1: (2, 3, 0), p2: (0, 1, 0), p3: (-2, 1, 0))),
            (Stage.Swing, 3, P(fwd: 5, up: -0.5f, pitch: -3, fang: 0.4f, l0: (10, -4, 0), r0: (12, -4, 0), l1: (4, 0, 12), r1: (2, 3, 0), p2: (0, 1, 0), p3: (-2, 1, 0))),
            (Stage.Strike, 1, P(fwd: 8, up: -2.5f, pitch: -9, fang: 0.2f, l0: (10, -4, 0), r0: (12, -4, 0), l1: (9, -2, 0), r1: (2, 3, 0), p2: (0, 1, 0), p3: (-2, 1, 0))),
            (Stage.Rebound, 1, P(fwd: 7, up: -1.5f, pitch: -7, fang: 0.2f, l0: (10, -4, 0), r0: (12, -4, 0), l1: (9, -2, 0), r1: (2, 3, 0), p2: (0, 1, 0), p3: (-2, 1, 0))),
            (Stage.Held, 2, P(fwd: 7.5f, up: -2, pitch: -8, fang: 0.1f, l0: (10, -4, 0), r0: (12, -4, 0), l1: (9, -2, 0), r1: (2, 3, 0), p2: (0, 1, 0), p3: (-2, 1, 0))),
            (Stage.Recover, 4, P(fwd: 3, up: -0.5f, pitch: -2, l0: (6, -1, 3), r0: (7, -1, 2), l1: (5, 0, 2), r1: (1, 1.5f, 0), p2: (0, 0.5f, 0), p3: (-1, 0.5f, 0))),
            (Stage.Recover, 5, Rest));

        // Spinning: head down, the abdomen swung up over its back with the spinnerets forward, swelling; the legs
        // braced wide.
        static readonly Pose Spun = P(fwd: -1, up: -2, pitch: -12, abFwd: 11, abUp: 15, swell: 0.15f, spin: 1, p0: (1, 3, 0), p1: (0, 3, 0), p2: (0, 3, 0), p3: (-1, 3, 0));

        // The silk thrown: the abdomen snapped forward and squeezed, the body kicked back by it.
        static readonly (Pose[] Frames, Stage[] Stages) ShootBlow = Expand(Spun,
            (Stage.Swing, 2, P(fwd: 1, up: -1.5f, pitch: -14, abFwd: 16, abUp: 13, swell: -0.02f, spin: 1, p0: (1, 3, 0), p1: (0, 3, 0), p2: (0, 3, 0), p3: (-1, 3, 0))),
            (Stage.Strike, 1, P(fwd: -4.5f, up: -3, pitch: -5, abFwd: 9, abUp: 9, swell: -0.14f, spin: 1, p0: (1, 3, 0), p1: (0, 3, 0), p2: (0, 3, 0), p3: (-1, 3, 0))),
            (Stage.Rebound, 1, P(fwd: -2.5f, up: -2, pitch: -5, abFwd: 9, abUp: 10, swell: -0.06f, spin: 0.6f, p0: (1, 3, 0), p1: (0, 3, 0), p2: (0, 3, 0), p3: (-1, 3, 0))),
            (Stage.Held, 2, P(fwd: -2.5f, up: -2, pitch: -4, abFwd: 8, abUp: 9, swell: -0.03f, spin: 0.3f, p0: (1, 3, 0), p1: (0, 3, 0), p2: (0, 3, 0), p3: (-1, 3, 0))),
            (Stage.Recover, 4, P(fwd: -1, up: -1, pitch: -2, abFwd: 4, abUp: 5, p0: (0.5f, 1.5f, 0), p1: (0, 1.5f, 0), p2: (0, 1.5f, 0), p3: (-0.5f, 1.5f, 0))),
            (Stage.Recover, 5, Rest));

        // Pressed flat and back, the knees standing high over it.
        static readonly Pose Crouched = P(fwd: -8, up: -5.5f, pitch: 4, abUp: -1, fang: 0.3f, p0: (-1, 2, 0), p1: (0, 1, 0), p3: (2, 2, 0));

        // Out of a run: thrown onto the front legs, which brake it.
        static readonly Pose Braked = P(fwd: 5, up: -2.5f, pitch: -6, abUp: 3, p0: (7, 2, 0), p1: (4, 2, 0), p2: (0, 1, 0), p3: (-3, 0, 3));

        // Drawn back with the head up and the fangs wide, all eight feet planted, the abdomen pumping.
        static readonly Pose Gaped = P(fwd: -6, up: 5, pitch: 25, swell: 0.2f, fang: 1, glow: 1, p0: (3, 5, 0), p1: (1, 3, 0), p3: (-2, 1, 0));

        // The spit: the head whipped forward and down, the abdomen squeezed empty.
        static readonly (Pose[] Frames, Stage[] Stages) SpitBlow = Expand(Gaped,
            (Stage.Swing, 4, P(fwd: 4, up: 0, pitch: -6, swell: 0.04f, fang: 1, glow: 1, p0: (3, 5, 0), p1: (1, 3, 0), p3: (-2, 1, 0))),
            (Stage.Strike, 1, P(fwd: 8, up: -2, pitch: -14, swell: -0.15f, fang: 1, glow: 1, p0: (3, 5, 0), p1: (1, 3, 0), p3: (-2, 1, 0))),
            (Stage.Rebound, 1, P(fwd: 5, up: -1, pitch: -8, swell: -0.08f, fang: 0.8f, glow: 0.6f, p0: (3, 5, 0), p1: (1, 3, 0), p3: (-2, 1, 0))),
            (Stage.Held, 2, P(fwd: 5.5f, up: -1, pitch: -9, swell: -0.06f, fang: 0.6f, glow: 0.3f, p0: (3, 5, 0), p1: (1, 3, 0), p3: (-2, 1, 0))),
            (Stage.Recover, 4, P(fwd: 2, pitch: -3, swell: -0.02f, fang: 0.3f, p0: (1.5f, 2.5f, 0), p1: (0.5f, 1.5f, 0), p3: (-1, 0.5f, 0))),
            (Stage.Recover, 5, Rest));

        // Gathered under its thread: the legs drawn half in, the abdomen up, the thread run up to the roof.
        static readonly Pose Gathered = P(up: -3, pitch: -5, abUp: 5, abFwd: 2, tuck: 0.4f, thread: 1);

        // Hauled up out of sight, the legs folded under it.
        static readonly Pose Hauled = P(up: 95, pitch: -5, abUp: 5, abFwd: 2, tuck: 1, thread: 1);

        // Down on its mark: falling with the legs opening, flat on the floor with all eight thrown wide, then pushing
        // itself up again.
        static readonly (Pose[] Frames, Stage[] Stages) LandBlow = Expand(P(up: 30, pitch: -4, abUp: 4, tuck: 0.7f, thread: 1),
            (Stage.Swing, 2, P(up: 7, pitch: -2, abUp: 3, tuck: 0.25f, thread: 1, p0: (2, 3, 4), p1: (0, 4, 4), p2: (0, 4, 4), p3: (-2, 3, 4))),
            (Stage.Strike, 1, P(up: -5.5f, abUp: -2, swell: 0.08f, p0: (4, 7, 0), p1: (0, 8, 0), p2: (0, 8, 0), p3: (-4, 7, 0))),
            (Stage.Rebound, 1, P(up: -3, abUp: 1.5f, swell: -0.03f, p0: (4, 7, 0), p1: (0, 8, 0), p2: (0, 8, 0), p3: (-4, 7, 0))),
            (Stage.Held, 2, P(up: -4, abUp: 0, p0: (4, 7, 0), p1: (0, 8, 0), p2: (0, 8, 0), p3: (-4, 7, 0))),
            (Stage.Recover, 4, P(up: -1.5f, p0: (2, 3.5f, 1.5f), p1: (0, 4, 0), p2: (0, 4, 1.5f), p3: (-2, 3.5f, 0))),
            (Stage.Recover, 5, Rest));

        // Sat on its belly with all eight legs lifted high round it like a crown.
        static readonly Pose Splayed = P(up: -6, swell: 0.06f, fang: 0.6f, p0: (-3, 2, 28), p1: (-1, 3, 31), p2: (1, 3, 31), p3: (3, 2, 28));

        // All eight driven down and out at once, which throws the body up off the floor; the points stay in.
        static readonly (Pose[] Frames, Stage[] Stages) SlamBlow = Expand(Splayed,
            (Stage.Swing, 4, P(up: 0, fang: 0.6f, p0: (3, 5, 6), p1: (1, 7, 6), p2: (-1, 7, 6), p3: (-3, 5, 6))),
            (Stage.Strike, 1, P(up: 5, swell: -0.06f, fang: 0.2f, p0: (5, 8, 0), p1: (2, 10, 0), p2: (-2, 10, 0), p3: (-5, 8, 0))),
            (Stage.Rebound, 1, P(up: 3.5f, fang: 0.2f, p0: (5, 8, 0), p1: (2, 10, 0), p2: (-2, 10, 0), p3: (-5, 8, 0))),
            (Stage.Held, 3, P(up: 4, fang: 0.1f, p0: (5, 8, 0), p1: (2, 10, 0), p2: (-2, 10, 0), p3: (-5, 8, 0))),
            (Stage.Recover, 5, P(up: 0.5f, p0: (2, 3, 1.5f), p1: (0.5f, 4, 0), p2: (-0.5f, 4, 1.5f), p3: (-2, 3, 0))),
            (Stage.Recover, 5, Rest));

        // The frames a screech takes to rear up, before it shakes there (frames ScreechRise.. loop).
        public const int ScreechRise = 14;

        static readonly Pose Screeching = P(fwd: -3, up: 8, pitch: 30, swell: 0.1f, fang: 1, glow: 0.7f, p0: (-14, 11, 36), p1: (-3, 9, 16), p2: (0, 2, 0), p3: (-3, 2, 0));

        static (Pose[] Frames, Stage[] Stages)? BlowOf(SpiderPose pose) => pose switch
        {
            SpiderPose.Bite => BiteBlow,
            SpiderPose.Stab => StabBlow,
            SpiderPose.Shoot => ShootBlow,
            SpiderPose.Spit => SpitBlow,
            SpiderPose.Land => LandBlow,
            SpiderPose.Slam => SlamBlow,
            _ => null,
        };

        // How many frames each pose has: a wind-up through its telegraph, a blow through its swing and recovery, a loop.
        public static int FramesOf(SpiderPose pose) => pose switch
        {
            SpiderPose.Idle => 6,
            SpiderPose.Walk => 12,
            SpiderPose.Rear => 14,
            SpiderPose.Cock => 14,
            SpiderPose.Spin => 14,
            SpiderPose.Crouch => 10,
            SpiderPose.Spring => 4,
            SpiderPose.Rush => 6,
            SpiderPose.Skid => 9,
            SpiderPose.Gape => 14,
            SpiderPose.Gather => 10,
            SpiderPose.Rise => 16,
            SpiderPose.Splay => 20,
            SpiderPose.Screech => ScreechRise + 4,
            SpiderPose.Flinch => 6,
            SpiderPose.Die => 20,
            _ => BlowOf(pose)?.Frames.Length ?? 1,
        };

        // How long a blow's frame is shown (seconds); 0 for anything else.
        public static float FrameSeconds(SpiderPose pose, int frame)
        {
            var stages = BlowOf(pose)?.Stages;
            return stages == null ? 0 : StageSeconds(stages[System.Math.Max(0, System.Math.Min(stages.Length - 1, frame))]);
        }

        // The frames of a blow too fast to follow (drawn with streaks), and the frame it strikes.
        public static bool Swinging(SpiderPose pose, int frame)
        {
            var stages = BlowOf(pose)?.Stages;
            return stages != null && frame >= 0 && frame < stages.Length && (stages[frame] == Stage.Swing || stages[frame] == Stage.Strike);
        }

        public static int StrikeFrame(SpiderPose pose) => System.Array.IndexOf(BlowOf(pose)?.Stages ?? new Stage[0], Stage.Strike);

        // Walking, the legs take turns in two sets of four (the first and third of one side with the second and fourth
        // of the other): a set swings forward off the floor while the other carries the body.
        static bool FirstSet(int leg) => (leg % 4 + leg / 4) % 2 == 0;

        static Pose PoseOf(SpiderPose pose, int frame)
        {
            int count = FramesOf(pose);
            int f = ((frame % count) + count) % count;
            float t = count > 1 ? f / (float)(count - 1) : 0;
            float cycle = f / (float)count;
            float turn = cycle * 2 * (float)System.Math.PI;
            float s = (float)System.Math.Sin(turn), c = (float)System.Math.Cos(turn);
            switch (pose)
            {
                case SpiderPose.Idle:
                {
                    // Breathing: the body rising a little on its legs, the abdomen swelling, the fangs working, one
                    // front leg feeling the floor.
                    float b = 0.5f - 0.5f * c;
                    return P(up: 0.8f * b, abUp: 0.7f * b, swell: 0.035f * b, fang: 0.2f * b, l0: (0.5f * b, 0, 1.6f * System.Math.Max(0, s)));
                }
                case SpiderPose.Walk:
                case SpiderPose.Rush:
                {
                    // A stride each way per loop. Running it is stretched out low, the strides long, the front pair
                    // reaching.
                    bool run = pose == SpiderPose.Rush;
                    float stride = run ? 6.5f : 5, lift = run ? 5 : 3.5f;
                    var p = run
                        ? P(fwd: 6, up: -2 + 0.5f * (float)System.Math.Cos(2 * turn), pitch: -5, abUp: 2 + 0.8f * (float)System.Math.Sin(2 * turn), fang: 0.6f)
                        : P(up: 0.5f * (float)System.Math.Cos(2 * turn), pitch: 1, abUp: 0.9f * (float)System.Math.Sin(2 * turn - 1), abFwd: 0.4f * s);
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
                            // Swept back along the body, but for the front pair.
                            int pair = leg % 4;
                            p.F[leg] += pair == 0 ? 6 : pair == 1 ? -1 : pair == 2 ? -4 : -6;
                            p.O[leg] = pair == 0 ? -2 : -3;
                        }
                    }
                    return p;
                }
                case SpiderPose.Rear:
                    // Weight back first, the front feet leaving the floor; then up onto the hind legs.
                    return Keys(t,
                        (0, Rest),
                        (0.35f, P(fwd: -2.5f, up: 1, pitch: 8, fang: 0.4f, p0: (-4, 2, 9), p1: (0, 1, 1.5f), p3: (-1, 1, 0))),
                        (0.7f, P(fwd: -5, up: 6, pitch: 25, fang: 0.9f, glow: 0.2f, p0: (-11, 5, 27), p1: (-2, 4, 10), p2: (-1, 1, 0), p3: (-3, 2, 0))),
                        (1, Reared));
                case SpiderPose.Cock:
                    // The left leg goes up first, the right after it.
                    return Keys(t,
                        (0, Rest),
                        (0.35f, P(fwd: -1, up: 1, pitch: 5, fang: 0.2f, l0: (-5, 2, 12), r0: (-1, 1, 3), p1: (1, 1.5f, 0), p3: (-1, 0.5f, 0))),
                        (0.7f, P(fwd: -3, up: 4.5f, pitch: 17, fang: 0.3f, l0: (-13, 5, 30), r0: (-5, 3, 16), p1: (2, 2.5f, 0), p2: (0, 1, 0), p3: (-2, 1, 0))),
                        (1, Cocked));
                case SpiderPose.Spin:
                    // The legs spread and the head goes down; then the abdomen comes up and over, and swells.
                    return Keys(t,
                        (0, Rest),
                        (0.3f, P(up: -1, pitch: -3, abUp: 3, abFwd: 1, p0: (0.5f, 2, 0), p1: (0, 2, 0), p2: (0, 2, 0), p3: (-0.5f, 2, 0))),
                        (0.7f, P(fwd: -1, up: -1.5f, pitch: -6, abUp: 10, abFwd: 6, swell: 0.04f, spin: 0.5f, p0: (1, 3, 0), p1: (0, 3, 0), p2: (0, 3, 0), p3: (-1, 3, 0))),
                        (1, Spun));
                case SpiderPose.Crouch:
                    return Keys(t,
                        (0, Rest),
                        (0.5f, P(fwd: -3, up: -2.5f, pitch: 1, fang: 0.2f, p0: (-0.5f, 0.5f, 0), p3: (1, 0.5f, 0))),
                        (1, Crouched));
                case SpiderPose.Spring:
                    // Out of the crouch into the run's first stride.
                    return Lerp(Crouched, PoseOf(SpiderPose.Rush, 0), (f + 1) / (float)(count + 1));
                case SpiderPose.Skid:
                    // The run's last stride into the braking legs, then settling.
                    return Keys(t,
                        (0, PoseOf(SpiderPose.Rush, 0)),
                        (0.35f, Braked),
                        (1, Rest));
                case SpiderPose.Gape:
                    // Drawn back and up; the abdomen pumps as the venom comes.
                    {
                        var p = Keys(t,
                            (0, Rest),
                            (0.3f, P(fwd: -2, up: 1, pitch: 6, swell: 0.03f, fang: 0.5f, glow: 0.2f, p0: (1.5f, 2.5f, 0), p1: (0.5f, 1.5f, 0), p3: (-1, 0.5f, 0))),
                            (0.7f, P(fwd: -5, up: 4, pitch: 20, swell: 0.08f, fang: 1, glow: 0.7f, p0: (3, 5, 0), p1: (1, 3, 0), p3: (-2, 1, 0))),
                            (1, Gaped));
                        return p;
                    }
                case SpiderPose.Gather:
                    // The thread goes up first; then the legs come in under it.
                    return Keys(t,
                        (0, Rest),
                        (0.4f, P(up: -1.5f, pitch: -3, abUp: 4, abFwd: 1.5f, spin: 1, thread: 0.5f, tuck: 0.1f)),
                        (1, Gathered));
                case SpiderPose.Rise:
                    // Off the floor slowly, then away.
                    return Keys(t,
                        (0, Gathered),
                        (0.3f, P(up: 4, pitch: -5, abUp: 5, abFwd: 2, tuck: 0.8f, thread: 1)),
                        (0.6f, P(up: 26, pitch: -5, abUp: 5, abFwd: 2, tuck: 1, thread: 1)),
                        (1, Hauled));
                case SpiderPose.Splay:
                    // Down onto its belly; the legs go up pair by pair from the front, then all shake.
                    {
                        var p = Keys(t,
                            (0, Rest),
                            (0.25f, P(up: -3.5f, fang: 0.3f, p0: (-1, -1, 8), p1: (0, 0, 1))),
                            (0.5f, P(up: -5, swell: 0.03f, fang: 0.5f, p0: (-2.5f, 1.5f, 23), p1: (-1, 2, 16), p2: (0.5f, 1, 5))),
                            (0.75f, P(up: -6, swell: 0.05f, fang: 0.6f, p0: (-3, 2, 28), p1: (-1, 3, 30), p2: (1, 2.5f, 24), p3: (2.5f, 1.5f, 13))),
                            (1, Splayed));
                        return p;
                    }
                case SpiderPose.Screech:
                    if (f < ScreechRise)
                        return Keys(f / (float)(ScreechRise - 1),
                            (0, Rest),
                            (0.4f, P(fwd: -1, up: 2, pitch: 8, swell: 0.03f, fang: 0.5f, glow: 0.2f, p0: (-4, 4, 10), p1: (0, 3, 2), p3: (-1, 1, 0))),
                            (1, Screeching));
                    {
                        // Shaking with it: the front legs and the abdomen.
                        float shake = (f - ScreechRise) % 2 == 0 ? 1 : -1;
                        var p = Lerp(Screeching, Screeching, 0);
                        p.U[0] += shake;
                        p.U[4] -= shake;
                        p.O[0] += 0.5f * shake;
                        p.O[4] += 0.5f * shake;
                        p.Swell += 0.02f * shake;
                        p.Up += 0.3f * shake;
                        return p;
                    }
                case SpiderPose.Flinch:
                    // Knocked back off its front feet, and down again.
                    return Keys(t,
                        (0, Rest),
                        (0.3f, P(fwd: -3, up: -1, pitch: 7, swell: -0.04f, fang: 0.5f, p0: (-2, 1, 4), p1: (-1, 0, 0), p3: (1, 0, 0))),
                        (1, Rest));
                case SpiderPose.Die:
                    // The legs give way and it drops; then they curl in over it.
                    return Keys(t,
                        (0, Rest),
                        (0.2f, P(fwd: -2, up: -3, pitch: 9, swell: -0.03f, fang: 0.8f, p0: (-2, 1, 6), p1: (-1, 1, 0), p3: (1, 0, 0))),
                        (0.45f, P(up: -6, pitch: 2, swell: -0.06f, fang: 0.4f, curl: 0.25f, p0: (-2, 2, 4), p1: (-1, 3, 5), p2: (1, 3, 5), p3: (2, 2, 4))),
                        (1, P(up: -6.5f, swell: -0.12f, curl: 1)));
                default:
                    return BlowOf(pose) is (Pose[] frames, Stage[] _) ? frames[System.Math.Max(0, System.Math.Min(frames.Length - 1, f))] : Rest;
            }
        }

        // ---- Laying it on the screen. ----

        struct Dot
        {
            public float X, Y, Deep;
        }

        // The body's solved points in three dimensions for a pose.
        sealed class Skeleton
        {
            public (float F, float R, float U) Chest, Abdomen, Mouth, Spinneret;
            public readonly (float F, float R, float U)[] Hip = new (float F, float R, float U)[8], Knee = new (float F, float R, float U)[8], Foot = new (float F, float R, float U)[8];
        }

        static Skeleton Solve(Pose p)
        {
            var body = new Skeleton();
            float pitch = p.Pitch * (float)System.Math.PI / 180;
            float cos = (float)System.Math.Cos(pitch), sin = (float)System.Math.Sin(pitch);
            var chest = (F: ChestAhead + p.Fwd, R: 0f, U: BodyHigh + p.Up);
            // A point of the body from the chest's middle, pitched about it.
            (float F, float R, float U) Carried(float ahead, float right, float up) => (chest.F + ahead * cos - up * sin, chest.R + right, chest.U + ahead * sin + up * cos);
            body.Chest = chest;
            var abdomen = Carried(-AbdomenBehind, 0, AbdomenAbove);
            body.Abdomen = (abdomen.F + p.AbFwd, 0, abdomen.U + p.AbUp);
            body.Mouth = Carried(9, 0, -2);
            // The spinnerets: at the abdomen's far end, and brought round to point ahead when it is swung over to spin.
            body.Spinneret = (body.Abdomen.F + (-9 + 17 * p.Spin) * (1 + p.Swell), 0, body.Abdomen.U - 2 + 4 * p.Spin);
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
                    // Under the body, wherever it is.
                    var under = (F: chest.F + hipAhead * 1.5f, R: side * (hipOut * 1.6f + 2), U: chest.U - 9);
                    foot = (foot.F + (under.F - foot.F) * p.Tuck, foot.R + (under.R - foot.R) * p.Tuck, foot.U + (under.U - foot.U) * p.Tuck);
                }
                if (p.Curl > 0)
                {
                    // Folded up over the body.
                    var over = (F: chest.F + hipAhead * 1.2f - 3, R: side * (hipOut * 0.9f + 1.5f), U: chest.U + 7);
                    foot = (foot.F + (over.F - foot.F) * p.Curl, foot.R + (over.R - foot.R) * p.Curl, foot.U + (over.U - foot.U) * p.Curl);
                }
                // Two bones: the knee up out of the line from hip to foot.
                float dx = foot.F - hip.F, dy = foot.R - hip.R, dz = foot.U - hip.U;
                float reach = (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
                float most = Thigh + Shin - 0.5f;
                if (reach > most)
                {
                    // Out of reach: the leg straight, the foot as far as it goes.
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
                // Up, less what of it lies along the leg; a leg straight up or down bends outward instead.
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

        static readonly Rgba Ink = new Rgba(1, 0, 4);
        static readonly Rgba LegLight = new Rgba(74, 54, 84);
        static readonly Rgba LegMain = new Rgba(56, 38, 65);
        static readonly Rgba LegDark = new Rgba(32, 21, 41);
        static readonly Rgba LegBand = new Rgba(80, 55, 82);
        static readonly Rgba FarLight = new Rgba(50, 34, 58);
        static readonly Rgba FarMain = new Rgba(39, 25, 49);
        static readonly Rgba FarDark = new Rgba(20, 11, 28);
        static readonly Rgba Chitin = new Rgba(39, 25, 49);
        static readonly Rgba ChitinLight = new Rgba(63, 39, 73);
        static readonly Rgba Venom = new Rgba(159, 227, 93);
        static readonly Rgba VenomDim = new Rgba(128, 199, 71);
        static readonly Rgba VenomBright = new Rgba(214, 255, 150);
        static readonly Rgba Silk = new Rgba(236, 236, 244);
        static readonly Rgba Streak = new Rgba(210, 190, 235);

        static bool IsMarking(Rgba c) => c.A > 0 && c.G > c.R + 40 && c.G > c.B + 40;

        // One leg: a thick thigh and a shin tapering to its point, lit along the top, banded, outlined.
        static void Leg(PixelImage image, bool[,] mask, Dot hip, Dot knee, Dot foot, bool far, bool glow)
        {
            System.Array.Clear(mask, 0, mask.Length);
            int w = image.Width, h = image.Height;
            void Stamp(float x, float y, float radius)
            {
                int x0 = (int)System.Math.Floor(x - radius), x1 = (int)System.Math.Ceiling(x + radius);
                int y0 = (int)System.Math.Floor(y - radius), y1 = (int)System.Math.Ceiling(y + radius);
                for (int py = y0; py <= y1; py++)
                    for (int px = x0; px <= x1; px++)
                        if (px >= 0 && py >= 0 && px < w && py < h && (px - x) * (px - x) + (py - y) * (py - y) <= radius * radius) mask[px, py] = true;
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
            Bone(hip, knee, 1.45f, 1.3f);
            Bone(knee, foot, 1.2f, 0.55f);
            var light = far ? FarLight : LegLight;
            var main = far ? FarMain : LegMain;
            var dark = far ? FarDark : LegDark;
            int minX = (int)System.Math.Max(1, System.Math.Min(hip.X, System.Math.Min(knee.X, foot.X)) - 3), maxX = (int)System.Math.Min(w - 2, System.Math.Max(hip.X, System.Math.Max(knee.X, foot.X)) + 3);
            int minY = (int)System.Math.Max(1, System.Math.Min(hip.Y, System.Math.Min(knee.Y, foot.Y)) - 3), maxY = (int)System.Math.Min(h - 2, System.Math.Max(hip.Y, System.Math.Max(knee.Y, foot.Y)) + 3);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    if (mask[x, y]) continue;
                    if (mask[x - 1, y] || mask[x + 1, y] || mask[x, y - 1] || mask[x, y + 1]) image[x, y] = Ink;
                }
            float shin = (float)System.Math.Sqrt((foot.X - knee.X) * (foot.X - knee.X) + (foot.Y - knee.Y) * (foot.Y - knee.Y));
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    if (!mask[x, y]) continue;
                    bool top = !mask[x, y - 1], bottom = !mask[x, y + 1];
                    var c = top && !bottom ? light : bottom && !top ? dark : main;
                    // Bands across the shin, as a spider's leg is jointed.
                    float along = shin < 1 ? 0 : ((x - knee.X) * (foot.X - knee.X) + (y - knee.Y) * (foot.Y - knee.Y)) / shin;
                    if (!far && along > 1 && (System.Math.Abs(along - shin * 0.42f) < 0.6f || System.Math.Abs(along - shin * 0.74f) < 0.6f)) c = LegBand;
                    image[x, y] = c;
                }
            int kx = (int)System.Math.Round(knee.X), ky = (int)System.Math.Round(knee.Y);
            if (image.Inside(kx, ky) && mask[kx, ky]) image[kx, ky] = glow ? Venom : far ? FarLight : LegBand;
            int fx = (int)System.Math.Round(foot.X), fy = (int)System.Math.Round(foot.Y);
            if (image.Inside(fx, fy) && mask[fx, fy]) image[fx, fy] = dark;
        }

        // A shaded lump (the chest under the picture's, and the waist between chest and abdomen).
        static void Lump(PixelImage image, float cx, float cy, float rx, float ry)
        {
            for (int y = (int)(cy - ry - 1); y <= (int)(cy + ry + 1); y++)
                for (int x = (int)(cx - rx - 1); x <= (int)(cx + rx + 1); x++)
                {
                    if (!image.Inside(x, y)) continue;
                    float d = (x - cx) * (x - cx) / (rx * rx) + (y - cy) * (y - cy) / (ry * ry);
                    float edge = (x - cx) * (x - cx) / ((rx + 1) * (rx + 1)) + (y - cy) * (y - cy) / ((ry + 1) * (ry + 1));
                    if (d <= 1) image[x, y] = y < cy - ry * 0.3f ? ChitinLight : Chitin;
                    else if (edge <= 1) image[x, y] = Ink;
                }
        }

        // A part of the picture with its middle at (cx, cy), grown or shrunk about it.
        static void Lay(PixelImage image, PixelImage part, float midX, float midY, float cx, float cy, float scale, ref int top, ref int bottom)
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

        // One frame of a pose. In the second phase its markings burn brighter; in the last they blaze and its knees
        // glow with venom.
        public static PixelImage Draw(BossView view, SpiderPose pose, int frame, int phase) => Render(view, pose, frame, phase, out _);

        // Where its feet, mouth and abdomen are in a frame (picture pixels) and the rows its body covers: for the
        // preview's check that nothing jumps from one frame to the next, and for effects at its mouth and spinnerets.
        public struct Joints
        {
            public (float X, float Y)[] Feet;
            public float MouthX, MouthY, AbdomenX, AbdomenY, SpinX, SpinY;
            public int Top, Bottom;
        }

        public static Joints JointsOf(BossView view, SpiderPose pose, int frame)
        {
            Render(view, pose, frame, 1, out var joints);
            return joints;
        }

        static PixelImage Render(BossView view, SpiderPose pose, int frame, int phase, out Joints joints)
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
            // The picture's own abdomen sits where the picture has it from the chest; a pose moves it from there.
            var abdomenRest = Put((ChestAhead - AbdomenBehind, 0, BodyHigh + AbdomenAbove));
            var chestRest = Put((ChestAhead, 0, BodyHigh));
            var abdomenNow = Put(body.Abdomen);
            float abX = chest.X + rig.AbOffX + (abdomenNow.X - chest.X) - (abdomenRest.X - chestRest.X);
            float abY = chest.Y + rig.AbOffY + (abdomenNow.Y - chest.Y) - (abdomenRest.Y - chestRest.Y);
            var mouthRest = Put((ChestAhead + 9, 0, BodyHigh - 2));
            var mouthNow = Put(body.Mouth);
            // The head-and-chest picture leans a little with the pitch (a third of what the mouth moves).
            float lean = 0.6f;
            float thX = chest.X + ((mouthNow.X - chest.X) - (mouthRest.X - chestRest.X)) * lean;
            float thY = chest.Y + ((mouthNow.Y - chest.Y) - (mouthRest.Y - chestRest.Y)) * lean;
            float mouthX = chest.X + rig.MouthX + (mouthNow.X - chest.X) - (mouthRest.X - chestRest.X);
            float mouthY = chest.Y + rig.MouthY + (mouthNow.Y - chest.Y) - (mouthRest.Y - chestRest.Y);
            var spinNow = Put(body.Spinneret);
            float tipX = abX + spinNow.X - abdomenNow.X, tipY = abY + spinNow.Y - abdomenNow.Y;

            var hips = new Dot[8];
            var knees = new Dot[8];
            var feet = new Dot[8];
            for (int leg = 0; leg < 8; leg++)
            {
                hips[leg] = Put(body.Hip[leg]);
                knees[leg] = Put(body.Knee[leg]);
                feet[leg] = Put(body.Foot[leg]);
            }
            joints = new Joints { Feet = new (float X, float Y)[8], MouthX = mouthX, MouthY = mouthY, AbdomenX = abX, AbdomenY = abY, SpinX = tipX, SpinY = tipY, Top = int.MaxValue, Bottom = -1 };
            for (int leg = 0; leg < 8; leg++) joints.Feet[leg] = (feet[leg].X, feet[leg].Y);

            // What is drawn, from the furthest back to the nearest: legs by their hips, the abdomen, the chest.
            float abdomenDeep = abdomenNow.Deep + 0.1f, chestDeep = chest.Deep;
            var order = new List<(float Deep, int What)>();
            for (int leg = 0; leg < 8; leg++) order.Add((hips[leg].Deep * 1.2f + (hips[leg].Deep < chest.Deep ? -6 : 3), leg));
            order.Add((abdomenDeep, 8));
            order.Add((chestDeep, 9));
            order.Sort((a, b) => a.Deep.CompareTo(b.Deep));

            // The streaks of a frame too fast to follow: behind each leg that moved far, and behind the body.
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

            // Its thread, from the roof down to its abdomen.
            if (p.Thread > 0)
            {
                float from = abY - 6 - (abY - 6) * p.Thread;
                for (int y = (int)System.Math.Max(0, from); y < (int)System.Math.Min(Height, abY - 4); y++)
                {
                    int x = (int)System.Math.Round(abX);
                    if (image.Inside(x, y)) image[x, y] = y % 5 == 0 ? Silk.Scale(0.8f) : Silk;
                }
            }

            var mask = new bool[Width, Height];
            int top = int.MaxValue, bottom = -1;
            bool faceHidden = view == BossView.Back || view == BossView.BackSide;
            foreach (var (_, what) in order)
            {
                if (what < 8)
                {
                    Leg(image, mask, hips[what], knees[what], feet[what], hips[what].Deep < chest.Deep - 0.5f, phase >= 3);
                    continue;
                }
                if (what == 8)
                {
                    // The waist first, so that the abdomen never floats free of the chest.
                    Lump(image, (thX + abX) / 2, (thY + abY) / 2 + 1, 4, 3.5f);
                    Lay(image, PartOf(view, true), rig.AbMidX, rig.AbMidY, abX, abY, 1 + p.Swell, ref top, ref bottom);
                    continue;
                }
                Lump(image, thX, thY, 6.5f, 5);
                // From behind the head is under the body: no fangs from straight behind, and from three-quarters behind
                // only what shows past the chest.
                if (view == BossView.BackSide) Fangs(image, mouthX, mouthY, cos, sin, p, phase);
                Lay(image, PartOf(view, false), rig.ThMidX, rig.ThMidY, thX, thY, 1, ref top, ref bottom);
                if (!faceHidden) Fangs(image, mouthX, mouthY, cos, sin, p, phase);
            }
            joints.Top = top;
            joints.Bottom = bottom;

            // Silk gathering at the spinnerets.
            if (p.Spin > 0.05f)
            {
                float radius = 1 + 1.8f * p.Spin;
                for (int y = (int)(tipY - radius - 1); y <= (int)(tipY + radius + 1); y++)
                    for (int x = (int)(tipX - radius - 1); x <= (int)(tipX + radius + 1); x++)
                    {
                        float d = (x - tipX) * (x - tipX) + (y - tipY) * (y - tipY);
                        if (!image.Inside(x, y) || d > radius * radius) continue;
                        image[x, y] = d < radius * radius * 0.4f ? Rgba.White : Silk;
                    }
            }

            if (phase >= 2)
            {
                // The markings: brighter, then blazing.
                float heat = phase >= 3 ? 0.6f : 0.25f;
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                        if (IsMarking(image[x, y])) image[x, y] = image[x, y].Mix(VenomBright, heat);
            }
            return image;
        }

        // The fangs under its head: closed they point down and in, open they spread out and ahead; venom beads on
        // them and gathers at the mouth.
        static void Fangs(PixelImage image, float mx, float my, float cos, float sin, Pose p, int phase)
        {
            // On the screen: across the face (its right), and ahead.
            float rx = -sin, ry = cos * Deep, ax = cos, ay = sin * Deep;
            for (int side = -1; side <= 1; side += 2)
            {
                float bx = mx + rx * side * 2.2f, by = my + ry * side * 2.2f;
                float spread = 0.4f + 2.4f * p.Fang, reach = 0.5f + 3 * p.Fang, drop = 3.2f - 0.7f * p.Fang;
                float tx = bx + rx * side * spread + ax * reach, ty = by + ry * side * spread + ay * reach + drop;
                Line(image, bx, by - 1, (bx + tx) / 2 + rx * side * 0.8f, (by + ty) / 2, Ink);
                Line(image, bx, by, (bx + tx) / 2 + rx * side * 0.8f, (by + ty) / 2 + 1, new Rgba(20, 11, 28));
                Line(image, (bx + tx) / 2 + rx * side * 0.8f, (by + ty) / 2 + 1, tx, ty, p.Glow > 0.3f ? VenomBright : phase >= 2 || p.Fang > 0.5f ? Venom : VenomDim);
            }
            if (p.Glow > 0.1f)
            {
                // Venom welling between them.
                float radius = 0.8f + 1.8f * p.Glow;
                float gx = mx + ax * 1.5f, gy = my + ay * 1.5f + 2.5f;
                for (int y = (int)(gy - radius - 1); y <= (int)(gy + radius + 1); y++)
                    for (int x = (int)(gx - radius - 1); x <= (int)(gx + radius + 1); x++)
                    {
                        float d = (x - gx) * (x - gx) + (y - gy) * (y - gy);
                        if (!image.Inside(x, y) || d > radius * radius) continue;
                        image[x, y] = d < radius * radius * 0.35f ? VenomBright : Venom;
                    }
            }
        }
    }
}
