using System.Collections.Generic;

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
    }

    // The samurai of the forgotten shrine (2026-11-12): a ronin in crimson lacquered armour and a straw hat, a katana
    // at the hip. Its five views come from one Retro Diffusion Pro picture turned eight ways (Docs/ArtSources.md,
    // $0.28); the drawn blade is drawn here in code from its hand, and every pose is made in code: the legs move on
    // their own (a box each per view), the body leans, crouches and lunges.
    static class SamuraiArt
    {
        public const int Width = 80;
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

        // Where the sword is in a pose's frame, after iaido (2026-11-12, the person: "draw it from the hip, swing it and
        // put it away"). The hand's place from the scabbard's mouth (ahead, up, in pixels), the blade's angle (0 ahead, 90
        // up, 180 behind) and how much of it is out of the scabbard (0 sheathed .. 1). From the side ahead is right.
        struct Hold
        {
            public float Ahead, Up, Angle, Out;

            public Hold(float ahead, float up, float angle, float outOf)
            {
                Ahead = ahead;
                Up = up;
                Angle = angle;
                Out = outOf;
            }
        }

        static Hold? HoldOf(SamuraiPose pose, int frame, float scabbard)
        {
            int f = System.Math.Max(0, frame);
            float back = scabbard;
            switch (pose)
            {
                case SamuraiPose.Draw:
                    // Nukitsuke: the hand pulls the hilt forward out of the scabbard, the blade leaving it level and cutting
                    // across at full reach, then sweeping on and up.
                    return f switch
                    {
                        0 => new Hold(3, 1, 4, 0.55f),
                        1 => new Hold(10, 2, 2, 1),
                        _ => new Hold(8, 6, 38, 1),
                    };
                case SamuraiPose.Jodan:
                    // Furikaburi: the blade drawn and raised over the head, higher through the wind-up.
                    float t = f / 5f;
                    return new Hold(1 + 2 * t, 9 + 7 * t, 120 + 30 * t, 1);
                case SamuraiPose.Kiri:
                    // Kirioroshi: brought down from overhead to the front.
                    return f switch
                    {
                        0 => new Hold(7, 13, 70, 1),
                        1 => new Hold(10, 4, -25, 1),
                        _ => new Hold(9, 1, -40, 1),
                    };
                case SamuraiPose.Chiburi:
                    // The blood shaken off: a sharp flick out and down.
                    return new Hold(9, 3, -55, 1);
                case SamuraiPose.Noto:
                    // Noto: the hand back at the hip, the blade laid along the scabbard and slid home, slowly.
                    // The blade points back along the scabbard, tip first into its mouth.
                    return f switch
                    {
                        0 => new Hold(5, 3, back + 25, 1),
                        1 => new Hold(2, 1, back, 0.6f),
                        _ => new Hold(1, 0, back, 0.25f),
                    };
                case SamuraiPose.Dash:
                    // Running in with the hand on the hilt, the blade out and trailing low behind.
                    return new Hold(4, 1, 195, 1);
                case SamuraiPose.Kiai:
                    return new Hold(2, 15 + (f % 2), 100, 1);
                default:
                    return null;
            }
        }

        // How a pose moves the legs: a step forward and a lift for each.
        static (float StepA, float LiftA, float StepB, float LiftB) StepsOf(SamuraiPose pose, int frame)
        {
            float t = frame / 5f;
            int f = System.Math.Max(0, frame);
            switch (pose)
            {
                case SamuraiPose.Walk:
                    float step = new[] { 2, 0, -2, 0 }[f % 4];
                    return (step, f % 4 == 0 ? 2 : 0, -step, f % 4 == 2 ? 2 : 0);
                case SamuraiPose.Stance:
                    // Feet set wide, the front foot forward, sinking lower.
                    return (3 + 2 * t, 0, -2 - t, 0);
                case SamuraiPose.Draw:
                case SamuraiPose.Kiri:
                    // A long lunge on the front foot.
                    return (f == 0 ? 4 : 6, 0, -4, 0);
                case SamuraiPose.Chiburi:
                    return (4, 0, -3, 0);
                case SamuraiPose.Noto:
                    return (3 - f, 0, -2, 0);
                case SamuraiPose.Dash:
                    float run = new[] { 5, 0, -5, 0 }[f % 4];
                    return (run, f % 4 == 0 ? 2 : 0, -run, f % 4 == 2 ? 2 : 0);
                case SamuraiPose.Jodan:
                    return (2, 0, -1 - t, 0);
                case SamuraiPose.Kiai:
                    return (1, 0, -1, 0);
                default:
                    return (0, 0, 0, 0);
            }
        }

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
        public static PixelImage Draw(BossView view, SamuraiPose pose, int frame, int phase)
        {
            var (stepA, liftA, stepB, liftB) = StepsOf(pose, frame);
            var body = Compose(view, stepA, liftA, stepB, liftB);
            var (_, _, hip, scabbard) = Frame(view);
            bool side = view != BossView.Front && view != BossView.Back;
            float t = frame / 5f;
            float lean = 0, stretch = 0;
            switch (pose)
            {
                case SamuraiPose.Idle:
                    stretch = frame % 2 == 1 ? 0.02f : 0;
                    break;
                case SamuraiPose.Walk:
                    stretch = frame % 2 == 1 ? -0.03f : 0;
                    lean = 1;
                    break;
                case SamuraiPose.Stance:
                    // Low and still, leaning in over the hilt: the longer it holds, the lower.
                    lean = 3 + 2 * t;
                    stretch = -0.08f - 0.1f * t;
                    break;
                case SamuraiPose.Draw:
                case SamuraiPose.Kiri:
                    lean = new[] { 6, 8, 5 }[System.Math.Min(frame, 2)];
                    stretch = new[] { -0.16f, -0.12f, -0.06f }[System.Math.Min(frame, 2)];
                    break;
                case SamuraiPose.Chiburi:
                    lean = 4;
                    stretch = -0.08f;
                    break;
                case SamuraiPose.Noto:
                    lean = 2 - frame;
                    stretch = -0.04f + 0.02f * frame;
                    break;
                case SamuraiPose.Dash:
                    lean = 10;
                    stretch = -0.18f;
                    break;
                case SamuraiPose.Jodan:
                    lean = -1 - t;
                    stretch = 0.02f + 0.04f * t;
                    break;
                case SamuraiPose.Kiai:
                    lean = -3;
                    stretch = 0.05f;
                    break;
            }
            // The whole body carried with the cut (2026-11-12, the person: "the sword swings but the body does not move"):
            // shoved forward into the lunge and dropped low on the cut, back and up as the blade goes home.
            int c2 = System.Math.Min(frame, 2);
            int shove = pose switch
            {
                SamuraiPose.Stance => -1,
                SamuraiPose.Draw or SamuraiPose.Kiri => new[] { 4, 9, 8 }[c2],
                SamuraiPose.Chiburi => 6,
                SamuraiPose.Noto => new[] { 4, 2, 0 }[c2],
                SamuraiPose.Dash => 4,
                _ => 0,
            };
            int drop = pose switch
            {
                SamuraiPose.Stance => frame >= 3 ? 1 : 0,
                SamuraiPose.Draw or SamuraiPose.Kiri => new[] { 1, 3, 2 }[c2],
                SamuraiPose.Chiburi => 2,
                SamuraiPose.Noto => new[] { 1, 1, 0 }[c2],
                _ => 0,
            };
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
                    if (image.Inside(px, row)) image[px, row] = c;
                }
            }

            var mouth = Place(hip.X, hip.Y);
            if (HoldOf(pose, frame, scabbard) is Hold grip)
            {
                // From the side ahead is right; from the front ahead is toward the viewer (down and a little aside), from
                // behind away (up). The hand moves with the grip; the arm reaches to it from the shoulder.
                float ahead = grip.Ahead, upward = grip.Up;
                int hx = side ? mouth.X + (int)System.Math.Round(ahead) : mouth.X + (int)System.Math.Round(ahead * 0.35f);
                int hy = mouth.Y - (int)System.Math.Round(upward) + (side ? 0 : view == BossView.Front ? (int)System.Math.Round(ahead * 0.4f) : -(int)System.Math.Round(ahead * 0.3f));
                float angle = side ? grip.Angle : view == BossView.Front ? -70 + grip.Angle * 0.9f : 110 - grip.Angle * 0.4f;
                int length = side ? 26 : 22;
                var shoulder = Place(hip.X - (side ? 2 : -2), hip.Y - 9);
                if (view == BossView.Back)
                {
                    var front = image.Copy();
                    image = new PixelImage(Width, Height);
                    Blade(image, hx, hy, angle, grip.Out, length);
                    image.Draw(front, 0, 0);
                }
                else
                {
                    if (System.Math.Abs(hx - mouth.X) + System.Math.Abs(hy - mouth.Y) > 4) Arm(image, shoulder.X, shoulder.Y, hx, hy);
                    Blade(image, hx, hy, angle, grip.Out, length);
                }
            }
            else if (pose == SamuraiPose.Stance && frame >= 1)
            {
                // Koiguchi: the thumb eases the guard from the scabbard, which catches the light, brighter as the draw
                // nears.
                image[mouth.X, mouth.Y - 1] = Glint;
                if (frame >= 3)
                {
                    image[mouth.X - 1, mouth.Y - 1] = Glint;
                    image[mouth.X + 1, mouth.Y - 1] = Glint;
                    image[mouth.X, mouth.Y - 2] = Glint;
                }
                if (frame >= 5) image[mouth.X, mouth.Y] = Glint;
            }
            return image;
        }
    }
}
