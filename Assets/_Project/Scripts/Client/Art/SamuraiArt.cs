using System.Collections.Generic;
using System.Linq;

namespace Game.Client
{
    // What the shrine's samurai is doing, which picks its frame (WorldView.Bosses.cs). Its attacks are iaido, in the
    // order of the art: a long, plain stance with the right hand on the hilt at the left hip (Stance); the draw that is
    // the cut, the blade leaving the scabbard level at full reach (Draw, nukitsuke); for a cut from above, the blade
    // raised overhead (Jodan, furikaburi) and brought down (Kiri, kirioroshi); the blood shaken off (Chiburi); the blade
    // slid back home (Noto). A dash with the blade trailing (Dash) and a shout (Kiai).
    enum SamuraiPose
    {
        Idle,
        Walk,
        Stance,
        Draw,
        Dash,
        Jodan,
        Kiai,
        Kiri,
        Chiburi,
        Noto,
        Pull,
    }

    // The samurai of the forgotten shrine (2026-11-12): a ronin in crimson lacquered armour and a straw hat, a katana
    // at the hip. Its five views come from one Retro Diffusion Pro picture turned eight ways (Docs/ArtSources.md,
    // $0.28); the drawn blade is drawn here in code from its hand, and every pose is made in code: the legs move on
    // their own (a box each per view), the body leans, crouches and lunges.
    static class SamuraiArt
    {
        public const int Width = 104;
        public const int Height = 70;
        // How tall the body itself stands, for the name and marks over it.
        public const int BodyHeight = 40;

        static readonly Dictionary<char, Rgba> Palette = new Dictionary<char, Rgba>
        {
            ['a'] = new Rgba(165, 128, 83),
            ['b'] = new Rgba(135, 102, 70),
            ['c'] = new Rgba(127, 92, 56),
            ['d'] = new Rgba(116, 83, 52),
            ['e'] = new Rgba(127, 53, 55),
            ['f'] = new Rgba(114, 50, 56),
            ['g'] = new Rgba(88, 46, 53),
            ['h'] = new Rgba(54, 53, 54),
            ['i'] = new Rgba(58, 33, 43),
            ['j'] = new Rgba(39, 41, 43),
            ['k'] = new Rgba(71, 21, 41),
            ['l'] = new Rgba(75, 14, 32),
            ['m'] = new Rgba(66, 16, 36),
            ['n'] = new Rgba(56, 21, 33),
            ['p'] = new Rgba(34, 19, 26),
            ['q'] = new Rgba(17, 6, 0),
            ['r'] = new Rgba(18, 1, 9),
            ['s'] = new Rgba(11, 0, 4),
            ['t'] = new Rgba(1, 4, 6),
            ['u'] = new Rgba(3, 0, 1),
            ['o'] = new Rgba(0, 0, 0),
        };

        static readonly string[] FrontRows =
        {
            "...................ut...................",
            "................otgaahoo................",
            "..............ouaabacbaaoo..............",
            ".............ubaabaabadaaao.............",
            "............qaaacadaadadaaao............",
            "...........oaaadaadaadbadaabo...........",
            "...........obadaacaaaadaadabo...........",
            "...........obdaacdaaaadaaadco...........",
            "............odabcaaaaadaabbo............",
            ".............oocdaaaaaagdoo.............",
            ".............ojooouuqqquudo.............",
            "............okppradddahtqrgo............",
            "...........upgoppoaaanpoeffro...........",
            "...........ogsjppjooopjummmggu..........",
            "..........onkrjppphjhrjqsmlels..........",
            "..........onsjupkruosnrjogfnlmo.........",
            "..........onojumleeeennojsmlefo.........",
            "..........oohhomklmmlnnohjnfmno.........",
            "...........ojjunmmnnmmmnuhhsoo..........",
            "..........okutonoooonmmnsoojho..........",
            "..........okhjoooajjoosssngoo...........",
            "..........okhoohhoojhpaungno............",
            "..........oqqaoohhhoooaanpoo............",
            "..........oaarinoousioaaoojjooo.........",
            "..........oagrngomkknuooiooohhhttttt....",
            "...........oonkoofflgoonknpootthhhhhu...",
            "............onnuomlmnojomlkt...toooo....",
            ".............oohofeffojoffst............",
            ".............ohgtnnpnojouut.............",
            ".............tjjjpooojhghgt.............",
            "..............tsuto..ojjht..............",
            "..............onio....tout..............",
            "..............uino....oigo..............",
            ".............thhoo....unno..............",
            ".............too.......ogho.............",
            ".......................ohho.............",
            "........................qt..............",
            "........................................",
            "........................................",
            "........................................",
            "........................................",
            "........................................",
        };

        static readonly string[] FrontSideRows =
        {
            "...................ooo..................",
            "................oodaaooo................",
            "..............oubaaccbaaoo..............",
            ".............obaadaaaaacabo.............",
            "............odaadaabaadadaao............",
            "............obacdaabaacaadaao...........",
            "............obadaaacaaadaadao...........",
            "............ocdaaacaaaadaaaco...........",
            ".............obaaacaaaaadbbo............",
            "..............oicggaaaaagoo.............",
            "...............oorqqquoppogo............",
            ".............oohraaaacappoeko...........",
            "............ofrojugaaanpponfo...........",
            "............oeeeuhoooouhuholko..........",
            "...........oklfmmphhhhhoooongo..........",
            "...........sellnupoououfkojomo..........",
            "..........omlefojunneeekmohooo..........",
            "..........oefmnojommklmkmojoo...........",
            "..........olefoounmmmmmnmoongo..........",
            "..........oooujonmmknnooooopgo..........",
            "...........ohhjonmmmoojbaoooko..........",
            "..........onooojommoghooojuaoo..........",
            "..........ofgpophooooohoiooaao..........",
            "..........omgrooohhgogokppoao...........",
            "..........onrooknoooommerogoo...........",
            "..........ouuaokffookeemmono............",
            "..........oaaaommmojnmleeouo............",
            "...........oaogfeeojofekkoo.............",
            "............oommmmogonnooho.............",
            "..............oooogjoooopo..............",
            "..............ohggho.onno...............",
            "..............oujjo..ojho...............",
            "..............onoo....ooho..............",
            "..............onko......oo..............",
            "..............ohgo......................",
            "..............ohgo......................",
            "...............tho......................",
            "...............oo.......................",
            "........................................",
            "........................................",
            "........................................",
            "........................................",
        };

        static readonly string[] SideRows =
        {
            "........................................",
            "...................oo...................",
            "................oooaaooo................",
            "..............ouaadccaaao...............",
            ".............oaacaaaaaaaaoo.............",
            "............oaadaaaadaacaabo............",
            "...........oaadaadaadaaadaao............",
            "...........ocdaadaaaadaaadao............",
            "...........ocaaadaaaaddaaaco............",
            "............ocbcaaaaaadaado.............",
            ".............ooccccaadgdo...............",
            "..............ooooqqqoou................",
            "...............ooboaaaau................",
            "..............ohjoooaaoo................",
            ".............oouoohhoooo................",
            ".............oeeeegohoko................",
            "............okeeelgoooko....ojo.........",
            "............omlllloosemoo..ojjo.........",
            "............ofeeeeojsmmobotho...........",
            "............ollllmoonmkuoajo............",
            "............oeeellummkouoao.............",
            "............omkkfeummmoaoog.............",
            "............oooooommmojo................",
            ".............ohhooooogaoo...............",
            ".............uuokojjbrsko...............",
            ".............sugmpoooogso...............",
            ".............uungkogfnofo...............",
            ".............uoonoamlmoko...............",
            ".............ooouaageeoko...............",
            "..............okaakklloo................",
            "...............ooaooooo.................",
            "...............ohjjhho..................",
            "...............ojhggho..................",
            "................oojht...................",
            "................onuuo...................",
            "................okgo....................",
            "................onno....................",
            "................ojgo....................",
            "................oohho...................",
            "..................ooo...................",
            "........................................",
            "........................................",
        };

        static readonly string[] BackSideRows =
        {
            "..................oooo..................",
            "................oocbaaooo...............",
            "..............ouababccaaao..............",
            ".............ocadaaaaaacabo.............",
            "............qaadaaaadaaadaaco...........",
            "............occaaaaaadaacdaao...........",
            "............odaaaaaaadaaadcco...........",
            "............odaaaaaadadaaado............",
            ".............odcaaaadadaaco.............",
            "..............ooccccgcccoo..............",
            ".............ogooooooooub...............",
            "............ofohhoooooaoo...............",
            "............okooohhhhhoooo..............",
            "............oookfoohhorfeeo.............",
            "............ojolgfgoufeeelo.............",
            "............ojoffnfkollflllo............",
            "............ojommfmroumlleeo............",
            ".............oopmmffoufeello............",
            ".............okonnmmmonmllffo...........",
            "..............oooonmmoomeeemo...........",
            "............oooohhoomkofekoo............",
            "..........oohhonohhhuuuoooho............",
            ".....ooooohhookinoohhhhohhono...........",
            "....oggjjooo.okoffkooooooogko...........",
            ".....oooo....ooollffummmonngo...........",
            ".............oooellkofffookoo...........",
            "..............oomffojnlmnouao...........",
            "..............ooomnojoeefkaao...........",
            "..............ookooojommmoo.............",
            "...............onnoohhoooo..............",
            "...............oio..ohhhgo..............",
            "...............ouo..ooojo...............",
            "...............oho..okioo...............",
            "...............oo...ongo................",
            "....................onkoo...............",
            "....................ojphho..............",
            ".....................oooo...............",
            "........................................",
            "........................................",
            "........................................",
            "........................................",
            "........................................",
        };

        static readonly string[] BackRows =
        {
            "...................oo...................",
            ".................ooaaoo.................",
            "...............ouacccaboo...............",
            ".............oqaaaaaaaaacqo.............",
            "............obabadaaadaaaaco............",
            "...........obbaaadaaaadaacbbo...........",
            "...........obcaadaaaaadaaacbo...........",
            "...........obaaadaaaaaadaaaco...........",
            "............oaacaaaaaaadaaco............",
            ".............occcccccccccco.............",
            ".............ooooooooooooo..............",
            "............ofooooaaaaooouu.............",
            "...........ogffuhoooooohokfu............",
            "..........oillgohhhhhhhoffffo...........",
            "..........ofllshussssuhhmmllgo..........",
            ".........ukmfkoofeeeeeuoomlefs..........",
            ".........ofmmojomlllllmohoflllo.........",
            ".........omguhoufeeeeeeojomleeo.........",
            ".........ooujjomkmmmmkmmojoffoo.........",
            "..........ohjoommmmmmmmmnohooo..........",
            "...........oonoummmmmmmmoohhhoo.........",
            "............ooohoouoooouhooooko.........",
            "............ojohhhhhhghhjoonigo.........",
            ".........ooohokooooooooouguuuoo.........",
            ".......tohhhonffrnlllmokflugao..........",
            "....ooohhooooflmofeeefoomlldao..........",
            "..oogjjoo...omfoomlllloomeloo...........",
            "....ooo.....omnoofeeeeojokoo............",
            ".............oojommlmmojooo.............",
            ".............ohhjoooooohhho.............",
            ".............oohhjo..ojhhoo.............",
            "..............ouuo....uuuo..............",
            "..............okio....uggo..............",
            "..............okio....onno..............",
            "..............osso....osso..............",
            "..............uhj......jjq..............",
            "...............oo......oo...............",
            "........................................",
            "........................................",
            "........................................",
            "........................................",
            "........................................",
        };

        static readonly Dictionary<BossView, PixelImage> Views = new Dictionary<BossView, PixelImage>();

        static PixelImage ViewOf(BossView view)
        {
            if (Views.TryGetValue(view, out var image)) return image;
            image = PixelImage.FromRows(Palette, view switch
            {
                BossView.Front => FrontRows,
                BossView.FrontSide => FrontSideRows,
                BossView.Side => SideRows,
                BossView.BackSide => BackSideRows,
                _ => BackRows,
            });
            Views[view] = image;
            return image;
        }

        // The legs of each view (left, top, right, bottom, inclusive), the near or left one first; the mouth of the
        // scabbard at the left hip, where the right hand takes the hilt; and which way the scabbard runs from it
        // (degrees on the screen, 0 right, 90 up).
        static ((int X0, int Y0, int X1, int Y1) A, (int X0, int Y0, int X1, int Y1) B, (int X, int Y) Hip, float Scabbard) Frame(BossView view) => view switch
        {
            BossView.Front => ((12, 30, 18, 37), (21, 30, 28, 37), (22, 23), -8),
            BossView.FrontSide => ((13, 29, 19, 38), (20, 29, 26, 33), (25, 22), 200),
            BossView.Side => ((15, 30, 18, 40), (19, 30, 21, 40), (24, 20), 200),
            BossView.BackSide => ((14, 29, 18, 34), (19, 29, 26, 37), (25, 26), 190),
            _ => ((13, 29, 18, 37), (21, 29, 27, 37), (26, 24), 180),
        };

        const int Margin = 18;
        const int Headroom = 14;

        static readonly Rgba Steel = new Rgba(214, 224, 234);
        static readonly Rgba Edge = Rgba.White;
        static readonly Rgba SteelShade = new Rgba(140, 152, 168);
        static readonly Rgba Guard = new Rgba(212, 170, 72);
        static readonly Rgba Grip = new Rgba(40, 24, 30);
        static readonly Rgba Skin = new Rgba(222, 180, 132);
        static readonly Rgba Sleeve = new Rgba(92, 24, 34);
        static readonly Rgba Ink = new Rgba(12, 6, 8);
        static readonly Rgba Glint = new Rgba(255, 250, 220);

        // ---- The frames (2026-11-13, the person: more frames for natural motion, each checked against the frames either
        // side). A pose is a set of numbers eased between key poses: whether the right hand is on the hilt (Grip), where
        // it is from the scabbard's mouth (ahead, up, in pixels), the blade's angle (degrees on the screen from the side,
        // 0 ahead, 90 up; plus Scabbard times the scabbard's own angle, so 0 with Scabbard 1 lies along the scabbard) and
        // how much of it is out (0 sheathed .. 1); the feet's steps and lifts; the body's lean, stretch, how far it is
        // carried ahead (shove) and dropped; the glint at the scabbard's mouth; and the smear of a cut too fast to see.
        // The beat stays the person's: a long still stance, a slow pull, the draw in an instant, the blade home in a
        // flash (Docs/Bosses.md). ----

        struct SPose
        {
            public float Grip, Ahead, Up, Angle, Scabbard, Out, StepA, LiftA, StepB, LiftB, Lean, Stretch, Shove, Drop, Glint;
            // 1 a level draw round the front, 2 a cut down from overhead.
            public int Smear;
        }

        static SPose P(float lean = 0, float stretch = 0, float shove = 0, float drop = 0, float stepA = 0, float stepB = 0, float liftA = 0, float liftB = 0,
            float grip = 0, float ahead = 0, float up = 0, float angle = 0, float scabbard = 1, float outOf = 0, float glint = 0, int smear = 0) => new SPose
        {
            Lean = lean, Stretch = stretch, Shove = shove, Drop = drop, StepA = stepA, StepB = stepB, LiftA = liftA, LiftB = liftB, Grip = grip,
            Ahead = ahead, Up = up, Angle = angle, Scabbard = scabbard, Out = outOf, Glint = glint, Smear = smear,
        };

        static SPose Lerp(SPose a, SPose b, float u)
        {
            float L(float x, float y) => x + (y - x) * u;
            return new SPose
            {
                Grip = L(a.Grip, b.Grip), Ahead = L(a.Ahead, b.Ahead), Up = L(a.Up, b.Up), Angle = L(a.Angle, b.Angle), Scabbard = L(a.Scabbard, b.Scabbard),
                Out = L(a.Out, b.Out), StepA = L(a.StepA, b.StepA), LiftA = L(a.LiftA, b.LiftA), StepB = L(a.StepB, b.StepB), LiftB = L(a.LiftB, b.LiftB),
                Lean = L(a.Lean, b.Lean), Stretch = L(a.Stretch, b.Stretch), Shove = L(a.Shove, b.Shove), Drop = L(a.Drop, b.Drop), Glint = L(a.Glint, b.Glint),
                Smear = u < 0.5f ? a.Smear : b.Smear,
            };
        }

        static SPose Keys(float t, params (float At, SPose Pose)[] keys)
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

        // Frames from one pose to the next, eased (or, for a cut, all at once).
        static IEnumerable<SPose> Tween(SPose from, SPose to, int count)
        {
            for (int k = 1; k <= count; k++)
            {
                float u = k / (float)count;
                yield return Lerp(from, to, u * u * (3 - 2 * u));
            }
        }

        static readonly SPose Rest = P();
        // Low in the stance, the hand on the hilt.
        static readonly SPose Coiled = P(lean: 5, stretch: -0.18f, shove: -1, drop: 1, stepA: 5, stepB: -3, grip: 1, ahead: 1, up: 0.5f, glint: 3);
        // The blade eased a third out; the body sunk back onto the back foot just before the snap (anticipation).
        static readonly SPose Pulled = P(lean: 2, stretch: -0.22f, shove: -3, drop: 2, stepA: 4, stepB: -4, grip: 1, ahead: 6.5f, up: 0.5f, outOf: 0.36f, glint: 3);
        // Overhead, the blade drawn and raised (jodan).
        static readonly SPose Raised = P(lean: -2, stretch: 0.06f, stepA: 2, stepB: -2, grip: 1, ahead: 3, up: 16, angle: 150, scabbard: 0, outOf: 1);
        // After either cut, the blade coming back down level to recover: where the shake of the blood begins.
        static readonly SPose Recovered = P(lean: 3, stretch: -0.03f, shove: 6, drop: 1, stepA: 5, stepB: -1, grip: 1, ahead: 7, up: 3, angle: 10, scabbard: 0, outOf: 1);

        // Nukitsuke: the smear (too fast to see), the cut at full reach held, carried on and up, back down to recover.
        static readonly SPose[] DrawFrames = Cut(
            P(lean: 9, stretch: -0.2f, shove: 10, drop: 2, stepA: 5, stepB: -4, grip: 1, ahead: 7, up: 1, scabbard: 0, smear: 1),
            P(lean: 8, stretch: -0.12f, shove: 9, drop: 3, stepA: 6, stepB: -4, grip: 1, ahead: 10, up: 2, angle: 2, scabbard: 0, outOf: 1),
            P(lean: 8, stretch: -0.11f, shove: 9, drop: 3, stepA: 6, stepB: -4, grip: 1, ahead: 10, up: 3, angle: 8, scabbard: 0, outOf: 1),
            P(lean: 5, stretch: -0.06f, shove: 8, drop: 2, stepA: 6, stepB: -3, grip: 1, ahead: 8, up: 6, angle: 38, scabbard: 0, outOf: 1));

        // Kirioroshi: the smear down from overhead, the cut held low in front, on through, back up a little.
        static readonly SPose[] KiriFrames = Cut(
            P(lean: 8, stretch: -0.18f, shove: 9, drop: 2, stepA: 5, stepB: -4, grip: 1, ahead: 9, up: 8, angle: 60, scabbard: 0, smear: 2),
            P(lean: 8, stretch: -0.12f, shove: 9, drop: 3, stepA: 6, stepB: -4, grip: 1, ahead: 10, up: 4, angle: -25, scabbard: 0, outOf: 1),
            P(lean: 8, stretch: -0.11f, shove: 9, drop: 3, stepA: 6, stepB: -4, grip: 1, ahead: 10, up: 3, angle: -30, scabbard: 0, outOf: 1),
            P(lean: 6, stretch: -0.08f, shove: 8, drop: 2, stepA: 6, stepB: -3, grip: 1, ahead: 9, up: 1, angle: -40, scabbard: 0, outOf: 1));

        static SPose[] Cut(SPose smear, SPose contact, SPose held, SPose carried) =>
            new[] { smear, contact, held }.Concat(Tween(held, carried, 4)).Concat(Tween(carried, Recovered, 4)).ToArray();

        // Yoko chiburi (Eishin-ryu): the blade opened out to the side and up, snapped level, then still.
        static readonly SPose Opened = P(lean: 4, stretch: -0.08f, shove: 6, drop: 2, stepA: 4, stepB: -3, grip: 1, ahead: 9, up: 4, angle: 22, scabbard: 0, outOf: 1);
        static readonly SPose Snapped = P(lean: 4, stretch: -0.08f, shove: 6, drop: 2, stepA: 4, stepB: -3, grip: 1, ahead: 11, up: 2, angle: -12, scabbard: 0, outOf: 1);
        static readonly SPose[] ChiburiFrames = Tween(Recovered, Opened, 2).Concat(new[] { Snapped, Snapped }).ToArray();

        // Noto: the hand back to the hip, the blade turned back along the scabbard and slid home tip first; the hand left
        // on the hilt (zanshin), then let go.
        static readonly SPose[] NotoFrames = Tween(Snapped, P(lean: 2, stretch: -0.05f, shove: 5, drop: 1, stepA: 3, stepB: -2, grip: 1, ahead: 5, up: 3, angle: -335, scabbard: 1, outOf: 1), NotoTurn)
            .Concat(Tween(P(lean: 2, stretch: -0.05f, shove: 5, drop: 1, stepA: 3, stepB: -2, grip: 1, ahead: 5, up: 3, angle: -335, scabbard: 1, outOf: 1),
                P(lean: 0, stretch: -0.01f, shove: 2, stepA: 3, stepB: -2, grip: 1, ahead: 0.5f, up: 0, angle: -360, outOf: 0), NotoSlide))
            .Concat(Tween(P(lean: 0, stretch: -0.01f, shove: 2, stepA: 3, stepB: -2, grip: 1, ahead: 0.5f, up: 0, angle: -360, outOf: 0), P(angle: -360), 3))
            .ToArray();

        // How many frames each pose has.
        public static int FramesOf(SamuraiPose pose) => pose switch
        {
            SamuraiPose.Idle => 4,
            SamuraiPose.Walk => 8,
            SamuraiPose.Stance => 12,
            SamuraiPose.Pull => 8,
            SamuraiPose.Jodan => 24,
            SamuraiPose.Draw => DrawFrames.Length,
            SamuraiPose.Kiri => KiriFrames.Length,
            SamuraiPose.Chiburi => ChiburiFrames.Length,
            SamuraiPose.Noto => NotoFrames.Length,
            SamuraiPose.Dash => 8,
            SamuraiPose.Kiai => KiaiRise + 4,
            _ => 4,
        };

        // The noto: frames turning the blade back along the scabbard and sliding it home; the frame where it is home and
        // the hand stays on the hilt (zanshin).
        const int NotoTurn = 5;
        const int NotoSlide = 4;
        public const int NotoHome = NotoTurn + NotoSlide - 1;

        // How long after the cut begins the blade is home (the click of the guard).
        public static float SecondsToHome => Enumerable.Range(0, FramesOf(SamuraiPose.Draw)).Sum(f => FrameSeconds(SamuraiPose.Draw, f))
            + Enumerable.Range(0, FramesOf(SamuraiPose.Chiburi)).Sum(f => FrameSeconds(SamuraiPose.Chiburi, f))
            + Enumerable.Range(0, NotoHome).Sum(f => FrameSeconds(SamuraiPose.Noto, f));

        // The frames the shout takes to draw and raise the blade, before it shakes there.
        public const int KiaiRise = 20;

        // How long each frame of the cut and what follows it is shown (seconds), the person's beat (2026-11-12): out and
        // through in a third of a second, the blood flicked off in a tenth, home in an eighth, the hand left on the hilt
        // a moment, let go. 0 for poses timed by their telegraph or a loop.
        public static float FrameSeconds(SamuraiPose pose, int frame) => pose switch
        {
            SamuraiPose.Draw or SamuraiPose.Kiri => frame == 0 ? 0.03f : frame == 1 ? 0.12f : frame == 2 ? 0.04f : 0.02f,
            SamuraiPose.Chiburi => frame < 2 ? 0.02f : frame == 2 ? 0.03f : 0.05f,
            SamuraiPose.Noto => frame < NotoHome ? 0.0135f : frame == NotoHome ? 0.29f : 0.05f,
            _ => 0,
        };

        static SPose PoseOf(SamuraiPose pose, int frame)
        {
            int count = FramesOf(pose);
            int f = ((frame % count) + count) % count;
            float t = count > 1 ? f / (float)(count - 1) : 0;
            float cycle = f / (float)count * 2 * (float)System.Math.PI;
            float s = (float)System.Math.Sin(cycle), c = (float)System.Math.Cos(cycle);
            switch (pose)
            {
                case SamuraiPose.Idle:
                    return P(stretch: 0.02f * (0.5f - 0.5f * c));
                case SamuraiPose.Walk:
                    return P(lean: 1, stretch: -0.03f * (1 - System.Math.Abs(c)), stepA: 2 * s, stepB: -2 * s, liftA: 2 * System.Math.Max(0, c), liftB: 2 * System.Math.Max(0, -c));
                case SamuraiPose.Stance:
                {
                    // Low and still, leaning in over the hilt, lower the longer it holds; the hand takes the hilt early
                    // and the thumb eases the guard (the glint brightening).
                    var p = Keys(t, (0, Rest), (0.25f, P(lean: 3, stretch: -0.08f, shove: -1, stepA: 3, stepB: -2, grip: 1, glint: 1)), (1, Coiled));
                    p.Grip = t >= 0.12f ? 1 : 0;
                    return p;
                }
                case SamuraiPose.Pull:
                    // The slow start of the draw: the hilt eased forward and the blade showing a little more each frame;
                    // at the end the body sinks back before the snap.
                    return Keys(t, (0, Coiled), (0.7f, P(lean: 5, stretch: -0.19f, shove: -1.5f, drop: 1, stepA: 5, stepB: -3, grip: 1, ahead: 5, up: 0.5f, outOf: 0.3f, glint: 3)),
                        (1, Pulled));
                case SamuraiPose.Jodan:
                    // Furikaburi: out of the scabbard and raised overhead, higher through the wind-up.
                    return Keys(t, (0, Pulled), (0.45f, P(stretch: -0.05f, stepA: 2, stepB: -1, grip: 1, ahead: 2, up: 6, angle: 165, scabbard: 0, outOf: 1)), (1, Raised));
                case SamuraiPose.Draw:
                    return DrawFrames[f];
                case SamuraiPose.Kiri:
                    return KiriFrames[f];
                case SamuraiPose.Chiburi:
                    return ChiburiFrames[f];
                case SamuraiPose.Noto:
                    return NotoFrames[f];
                case SamuraiPose.Dash:
                    // Running in with the hand on the hilt, the blade out and trailing low behind.
                    return P(lean: 10, stretch: -0.18f, shove: 4, stepA: 5 * s, stepB: -5 * s, liftA: 2 * System.Math.Max(0, c), liftB: 2 * System.Math.Max(0, -c),
                        grip: 1, ahead: 4, up: 1, angle: 195, scabbard: 0, outOf: 1);
                case SamuraiPose.Kiai:
                    // The blade drawn and raised high, then held there shaking with the shout.
                    if (f < KiaiRise)
                        return Keys(f / (float)KiaiRise, (0, Rest), (0.25f, P(stretch: -0.04f, grip: 1, ahead: 3, up: 1, outOf: 0.3f)),
                            (0.7f, P(lean: -1, stretch: 0.01f, stepA: 1, stepB: -1, grip: 1, ahead: 3, up: 8, angle: 130, scabbard: 0, outOf: 1)),
                            (1, P(lean: -3, stretch: 0.05f, stepA: 1, stepB: -1, grip: 1, ahead: 2, up: 15, angle: 100, scabbard: 0, outOf: 1)));
                    return P(lean: -3, stretch: 0.05f, stepA: 1, stepB: -1, grip: 1, ahead: 2, up: 15 + (s + 1) / 2, angle: 100, scabbard: 0, outOf: 1);
                default:
                    return Rest;
            }
        }

        // How the body turns with the cut, a step of the eight views: wound away from the viewer while it gathers (the
        // shoulder turned in, the scabbard drawn back), unwound toward the viewer as the blade comes through and the
        // blood is shaken off, back square as it sheathes.
        public static int TwistOf(SamuraiPose pose, int frame) => pose switch
        {
            SamuraiPose.Stance => frame >= FramesOf(pose) / 3 ? -1 : 0,
            SamuraiPose.Pull => -1,
            SamuraiPose.Jodan => frame >= FramesOf(pose) / 2 ? -1 : 0,
            SamuraiPose.Draw or SamuraiPose.Kiri => frame < FramesOf(pose) - 3 ? 1 : 0,
            SamuraiPose.Chiburi => 1,
            _ => 0,
        };

        // The body with its legs moved, on a canvas with room round it for the blade.
        static PixelImage Compose(BossView view, float stepA, float liftA, float stepB, float liftB)
        {
            var image = ViewOf(view);
            var (a, b, _, _) = Frame(view);
            bool side = view != BossView.Front && view != BossView.Back;
            int w = image.Width, h = image.Height;
            var canvas = new PixelImage(w + 2 * Margin, h + Headroom);
            bool In((int X0, int Y0, int X1, int Y1) r, int x, int y) => x >= r.X0 && x <= r.X1 && y >= r.Y0 && y <= r.Y1;
            int R(float v) => (int)System.Math.Round(v);
            int ax = side ? R(stepA) : 0, ay = R(-liftA - (side ? 0 : System.Math.Max(0, stepA) * 0.5f));
            int bx = side ? R(stepB) : 0, by = R(-liftB - (side ? 0 : System.Math.Max(0, stepB) * 0.5f));
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = image[x, y];
                    if (c.A == 0 || In(a, x, y) || In(b, x, y)) continue;
                    canvas[x + Margin, y + Headroom] = c;
                }
            }
            void Lay((int X0, int Y0, int X1, int Y1) r, int dx, int dy)
            {
                for (int y = r.Y0; y <= r.Y1 && y < h; y++)
                    for (int x = r.X0; x <= r.X1 && x < w; x++)
                        if (image[x, y].A > 0) canvas[x + Margin + dx, y + Headroom + dy] = image[x, y];
            }
            Lay(b, bx, by);
            Lay(a, ax, ay);
            return canvas;
        }

        // A katana held at (hx, hy) at an angle (degrees on the screen): a grip back from the hand, a gold guard, a long
        // pale blade with a white edge and a dark outline, as much of it as is out of the scabbard.
        static void Blade(PixelImage image, int hx, int hy, float angle, float outOf, int length)
        {
            double a = angle * System.Math.PI / 180;
            double dx = System.Math.Cos(a), dy = -System.Math.Sin(a);
            int n = System.Math.Max(2, (int)System.Math.Round(length * outOf));
            var points = new List<(int X, int Y, Rgba C)>();
            for (int i = -4; i <= n; i++)
            {
                int x = hx + (int)System.Math.Round(dx * i), y = hy + (int)System.Math.Round(dy * i);
                var c = i < 0 ? Grip : i == 0 ? Guard : i >= n - 1 ? Edge : (i % 4 == 1 ? SteelShade : Steel);
                points.Add((x, y, c));
                if (i > 0 && i < n - 1) points.Add((x + (int)System.Math.Round(-dy), y + (int)System.Math.Round(dx), Edge));
            }
            for (int s = -1; s <= 1; s += 2) points.Add((hx + (int)System.Math.Round(-dy * s), hy + (int)System.Math.Round(dx * s), Guard));
            foreach (var (x, y, _) in points)
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                        if (image.Inside(x + ox, y + oy) && image[x + ox, y + oy].A == 0) image[x + ox, y + oy] = Ink;
            foreach (var (x, y, c) in points)
                if (image.Inside(x, y)) image[x, y] = c;
            // The hand round the grip.
            int gx = hx - (int)System.Math.Round(dx * 2), gy = hy - (int)System.Math.Round(dy * 2);
            if (image.Inside(gx, gy)) image[gx, gy] = Skin;
            if (image.Inside(gx + 1, gy)) image[gx + 1, gy] = Skin;
        }

        // A smear frame (2026-11-12, after pixel-art practice: one blurred in-between along the arc between two strong keys):
        // a crescent band from the angle the blade left to the one it reaches, bright at the tip's edge and thinning in
        // toward the hand; flatten squashes it top to bottom (a level sweep seen from above).
        static void Smear(PixelImage image, int hx, int hy, float from, float to, int length, float flatten)
        {
            float span = to - from;
            int steps = (int)(System.Math.Abs(span) / 3) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float a = (from + span * i / steps) * (float)System.Math.PI / 180;
                // Wider toward the end of the sweep, where the blade is fastest.
                float t = i / (float)steps;
                int inner = (int)(length * (0.75f - 0.45f * t));
                for (int r = inner; r <= length; r++)
                {
                    int x = hx + (int)System.Math.Round(System.Math.Cos(a) * r);
                    int y = hy - (int)System.Math.Round(System.Math.Sin(a) * r * flatten);
                    if (!image.Inside(x, y)) continue;
                    var c = r >= length - 1 ? Edge : r >= length - 3 ? Steel : (r + i) % 3 == 0 ? new Rgba(170, 200, 235) : (Rgba?)null;
                    if (c is Rgba colour) image[x, y] = colour;
                }
            }
        }

        // The arm from the shoulder to the hand: a crimson sleeve, two pixels wide, outlined.
        static void Arm(PixelImage image, int sx, int sy, int hx, int hy)
        {
            int steps = System.Math.Max(System.Math.Abs(hx - sx), System.Math.Abs(hy - sy));
            if (steps < 3) return;
            var points = new List<(int X, int Y)>();
            for (int i = 0; i <= steps; i++)
            {
                int x = sx + (hx - sx) * i / steps, y = sy + (hy - sy) * i / steps;
                points.Add((x, y));
                points.Add((x, y + 1));
            }
            foreach (var (x, y) in points)
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                        if (image.Inside(x + ox, y + oy) && image[x + ox, y + oy].A == 0) image[x + ox, y + oy] = Ink;
            foreach (var (x, y) in points)
                if (image.Inside(x, y)) image[x, y] = Sleeve;
        }

        // One frame. A wind-up (Stance, Jodan) runs frame 0..5 through its telegraph; a cut (Draw, Kiri) and the
        // sheathing (Noto) 0..2; a walk or a dash 0..3 (and 0..1 breathing at rest); phase 3 darkens the armour. Faces
        // right; WorldView flips it for the left.
        public static PixelImage Draw(BossView view, SamuraiPose pose, int frame, int phase) => Render(view, pose, frame, phase, out _);

        // Where the hand and the blade's tip are, and the body's edges, in a frame: for the preview's check that nothing
        // jumps from one frame to the next.
        public struct Joints
        {
            public float HandX, HandY, TipX, TipY;
            public int Top, Bottom;
        }

        public static Joints JointsOf(BossView view, SamuraiPose pose, int frame)
        {
            Render(view, pose, frame, 1, out var joints);
            return joints;
        }

        // One frame. Phase 3 darkens the armour. Faces right; WorldView flips it for the left.
        static PixelImage Render(BossView view, SamuraiPose pose, int frame, int phase, out Joints joints)
        {
            joints = new Joints { Top = int.MaxValue, Bottom = -1 };
            var p = PoseOf(pose, frame);
            var body = Compose(view, p.StepA, p.LiftA, p.StepB, p.LiftB);
            var (_, _, hip, scabbard) = Frame(view);
            bool side = view != BossView.Front && view != BossView.Back;
            float lean = p.Lean, stretch = p.Stretch;
            int shove = (int)System.Math.Round(p.Shove), drop = (int)System.Math.Round(p.Drop);
            if (!side)
            {
                stretch -= lean * 0.012f;
                lean = 0;
                // From the front or back a shove forward shows as a step down or up the screen.
                drop += view == BossView.Front ? shove / 3 : -shove / 3;
                shove = 0;
            }

            int w = body.Width, h = body.Height;
            int tall = System.Math.Max(4, (int)System.Math.Round(h * (1 + stretch)));
            int wide = System.Math.Max(4, (int)System.Math.Round(w * (1 - stretch * 0.5f)));
            var image = new PixelImage(Width, Height);
            int left = (Width - wide) / 2 + shove;
            int ground = Height - 1 - System.Math.Max(0, -drop);
            tall -= System.Math.Max(0, drop);
            // Where a picture point lands once leaned and squashed.
            (int X, int Y) Place(int x, int y)
            {
                int row = ground - (tall - 1) + (y + Headroom) * tall / h;
                float up = (ground - row) / (float)(tall - 1);
                return (left + (int)System.Math.Round((x + Margin) * wide / (float)w + lean * up), row);
            }
            for (int y = 0; y < tall; y++)
            {
                int row = ground - (tall - 1 - y);
                if (row < 0) continue;
                int sy = y * h / tall;
                float up = (tall - 1 - y) / (float)(tall - 1);
                int shift = (int)System.Math.Round(lean * up);
                for (int x = 0; x < wide; x++)
                {
                    var c = body[x * w / wide, sy];
                    if (c.A == 0) continue;
                    if (phase >= 3 && c.R > c.G + 40) c = c.Mix(new Rgba(70, 8, 16), 0.35f);
                    int px = left + x + shift;
                    if (!image.Inside(px, row)) continue;
                    image[px, row] = c;
                    joints.Top = System.Math.Min(joints.Top, row);
                    joints.Bottom = System.Math.Max(joints.Bottom, row);
                }
            }

            var mouth = Place(hip.X, hip.Y);
            joints.HandX = joints.TipX = mouth.X;
            joints.HandY = joints.TipY = mouth.Y;
            if (p.Grip >= 0.5f)
            {
                // From the side ahead is right; from the front ahead is toward the viewer (down and a little aside), from
                // behind away (up). The hand moves with the grip; the arm reaches to it from the shoulder.
                float ahead = p.Ahead, upward = p.Up;
                int hx = side ? mouth.X + (int)System.Math.Round(ahead) : mouth.X + (int)System.Math.Round(ahead * 0.35f);
                int hy = mouth.Y - (int)System.Math.Round(upward) + (side ? 0 : view == BossView.Front ? (int)System.Math.Round(ahead * 0.4f) : -(int)System.Math.Round(ahead * 0.3f));
                // The blade's way seen from the side (the side view's scabbard), carried to this view without a break:
                // from the front ahead points at the viewer (down the screen, shortened), from behind away (up).
                var (_, _, _, sideScabbard) = Frame(BossView.Side);
                float sideAngle = p.Angle + p.Scabbard * (side ? scabbard : sideScabbard);
                double radians = sideAngle * System.Math.PI / 180;
                float forward = (float)System.Math.Cos(radians), rise = (float)System.Math.Sin(radians);
                float vx = view == BossView.Front ? 0.35f * forward : -0.35f * forward, vy = view == BossView.Front ? rise - 0.45f * forward : rise + 0.4f * forward;
                float angle = side ? sideAngle : (float)(System.Math.Atan2(vy, vx) * 180 / System.Math.PI);
                float seen = side ? 1 : System.Math.Max(0.45f, System.Math.Min(1, (float)System.Math.Sqrt(vx * vx + vy * vy)));
                int length = (int)System.Math.Round((side ? 26 : 22) * seen);
                var shoulder = Place(hip.X - (side ? 2 : -2), hip.Y - 9);
                float shown = System.Math.Max(2, length * p.Out);
                joints.HandX = hx;
                joints.HandY = hy;
                // A smear frame has no blade to see; its tip is where the smear ends.
                float tipAngle = p.Smear == 1 ? (side ? 360 : 330) : p.Smear == 2 ? (side ? -25 : -40) : angle;
                if (p.Smear != 0) shown = length;
                joints.TipX = hx + (float)System.Math.Cos(tipAngle * System.Math.PI / 180) * shown;
                joints.TipY = hy - (float)System.Math.Sin(tipAngle * System.Math.PI / 180) * shown;
                if (view == BossView.Back)
                {
                    var front = image.Copy();
                    image = new PixelImage(Width, Height);
                    Blade(image, hx, hy, angle, p.Out, length);
                    image.Draw(front, 0, 0);
                }
                else
                {
                    if (System.Math.Abs(hx - mouth.X) + System.Math.Abs(hy - mouth.Y) > 4) Arm(image, shoulder.X, shoulder.Y, hx, hy);
                    if (p.Smear == 1)
                    {
                        // The smear: the blade's path from where it started to the cut, too fast to see as a blade. A
                        // draw sweeps level round the front of the body (from behind, past the viewer, to ahead: flattened
                        // as the ground is); a cut from above comes over the top.
                        Smear(image, hx, hy, side ? 200 : 160, side ? 360 : 330, length, side ? 0.5f : 0.8f);
                    }
                    else if (p.Smear == 2) Smear(image, hx, hy, side ? 150 : 120, side ? -25 : -40, length, 1);
                    else Blade(image, hx, hy, angle, p.Out, length);
                }
            }
            if (p.Glint >= 0.5f && p.Out < 0.05f)
            {
                // Koiguchi: the thumb eases the guard from the scabbard, which catches the light, brighter as the draw
                // nears.
                image[mouth.X, mouth.Y - 1] = Glint;
                if (p.Glint >= 1.5f)
                {
                    image[mouth.X - 1, mouth.Y - 1] = Glint;
                    image[mouth.X + 1, mouth.Y - 1] = Glint;
                    image[mouth.X, mouth.Y - 2] = Glint;
                }
                if (p.Glint >= 2.5f) image[mouth.X, mouth.Y] = Glint;
            }
            return image;
        }
    }
}
