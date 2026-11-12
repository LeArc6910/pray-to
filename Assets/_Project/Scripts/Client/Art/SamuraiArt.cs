using System.Collections.Generic;

namespace Game.Client
{
    // What the shrine's samurai is doing, which picks its frame (WorldView.Bosses.cs). Its attacks are iaido: a long,
    // plain stance with a hand on the hilt (Stance), then the draw too fast to follow (Draw), the blade shaken and put
    // back (Sheathe); a dash with the blade trailing (Dash), the blade raised overhead (Jodan) and a shout (Kiai).
    enum SamuraiPose
    {
        Idle,
        Walk,
        Stance,
        Draw,
        Sheathe,
        Dash,
        Jodan,
        Kiai,
    }

    // The samurai of the forgotten shrine (2026-11-12): a ronin in crimson lacquered armour and a straw hat, a katana
    // at the hip. Its five views come from one Retro Diffusion Pro picture turned eight ways (Docs/ArtSources.md,
    // $0.28); the drawn blade is drawn here in code from its hand, and every pose is made in code: the legs move on
    // their own (a box each per view), the body leans, crouches and lunges.
    static class SamuraiArt
    {
        public const int Width = 72;
        public const int Height = 64;
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

        // The legs of each view (left, top, right, bottom, inclusive), the near or left one first; and where the hand
        // holds the hilt, from which the blade is drawn.
        static ((int X0, int Y0, int X1, int Y1) A, (int X0, int Y0, int X1, int Y1) B, (int X, int Y) Hand) Frame(BossView view) => view switch
        {
            BossView.Front => ((12, 30, 18, 37), (21, 30, 28, 37), (22, 22)),
            BossView.FrontSide => ((13, 29, 19, 38), (20, 29, 26, 33), (25, 21)),
            BossView.Side => ((15, 30, 18, 40), (19, 30, 21, 40), (25, 18)),
            BossView.BackSide => ((14, 29, 18, 34), (19, 29, 26, 37), (26, 26)),
            _ => ((13, 29, 18, 37), (21, 29, 27, 37), (27, 24)),
        };

        const int Margin = 16;
        const int Headroom = 8;

        static readonly Rgba Steel = new Rgba(214, 224, 234);
        static readonly Rgba Edge = Rgba.White;
        static readonly Rgba SteelShade = new Rgba(140, 152, 168);
        static readonly Rgba Guard = new Rgba(212, 170, 72);
        static readonly Rgba Grip = new Rgba(40, 24, 30);
        static readonly Rgba Ink = new Rgba(12, 6, 8);
        static readonly Rgba Glint = new Rgba(255, 250, 220);

        // How a pose moves the legs (step forward, lift), and where the blade is: its angle in degrees (0 ahead, 90 up,
        // 180 behind; null when sheathed) and how far out of the scabbard (0..1).
        static (float StepA, float LiftA, float StepB, float LiftB, float? Blade, float Out) MovesOf(SamuraiPose pose, int frame)
        {
            float t = frame / 5f;
            int f = System.Math.Max(0, frame);
            switch (pose)
            {
                case SamuraiPose.Walk:
                    float step = new[] { 2, 0, -2, 0 }[f % 4];
                    return (step, f % 4 == 0 ? 2 : 0, -step, f % 4 == 2 ? 2 : 0, null, 0);
                case SamuraiPose.Stance:
                    // Feet set wide, sinking lower; the blade still in its scabbard.
                    return (2 + 2 * t, 0, -2 - t, 0, null, 0);
                case SamuraiPose.Draw:
                    // Out and through in one: level at full reach, then rising, then high.
                    return (4, 0, -3, 0, new[] { -4f, 28, 64 }[System.Math.Min(f, 2)], 1);
                case SamuraiPose.Sheathe:
                    // A flick of the blade down (chiburi), then slid home.
                    return (2, 0, -2, 0, f >= 2 ? (float?)null : new[] { -40f, 200 }[f], f == 1 ? 0.5f : 1);
                case SamuraiPose.Dash:
                    float run = new[] { 4, 0, -4, 0 }[f % 4];
                    return (run, f % 4 == 0 ? 2 : 0, -run, f % 4 == 2 ? 2 : 0, 192, 1);
                case SamuraiPose.Jodan:
                    // The blade raised overhead, higher through the wind-up.
                    return (2, 0, -1, 0, 70 + 30 * t, 1);
                case SamuraiPose.Kiai:
                    return (1, 0, -1, 0, 80 + 10 * (f % 2), 1);
                default:
                    return (0, 0, 0, 0, null, 0);
            }
        }

        // The body with its legs moved, on a canvas with room round it for the blade.
        static PixelImage Compose(BossView view, float stepA, float liftA, float stepB, float liftB)
        {
            var image = ViewOf(view);
            var (a, b, _) = Frame(view);
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
                    if (c.A == 0) continue;
                    if (In(a, x, y)) continue;
                    if (In(b, x, y)) continue;
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

        // A katana from the hand at an angle (degrees on the screen, 0 right, 90 up): a grip, a gold guard, a long
        // pale blade with a white edge and a dark outline, as much of it as is out of the scabbard.
        static void Blade(PixelImage image, int hx, int hy, float angle, float outOf, int length)
        {
            double a = angle * System.Math.PI / 180;
            double dx = System.Math.Cos(a), dy = -System.Math.Sin(a);
            int n = System.Math.Max(3, (int)System.Math.Round(length * outOf));
            var points = new List<(int X, int Y, Rgba C)>();
            for (int i = -3; i <= n; i++)
            {
                int x = hx + (int)System.Math.Round(dx * i), y = hy + (int)System.Math.Round(dy * i);
                var c = i < 0 ? Grip : i == 0 ? Guard : i >= n - 1 ? Edge : (i % 4 == 1 ? SteelShade : Steel);
                points.Add((x, y, c));
                if (i > 0 && i < n - 1)
                {
                    // The edge: a white line along one side of the steel.
                    int ex = x + (int)System.Math.Round(-dy), ey = y + (int)System.Math.Round(dx);
                    points.Add((ex, ey, Edge));
                }
            }
            // The guard is a little bar across the blade.
            for (int s = -1; s <= 1; s += 2) points.Add((hx + (int)System.Math.Round(-dy * s), hy + (int)System.Math.Round(dx * s), Guard));
            foreach (var (x, y, _) in points)
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                        if (image.Inside(x + ox, y + oy) && image[x + ox, y + oy].A == 0) image[x + ox, y + oy] = Ink;
            foreach (var (x, y, c) in points)
                if (image.Inside(x, y)) image[x, y] = c;
        }

        // One frame. A wind-up (Stance, Jodan) runs frame 0..5 through its telegraph, the draw and the sheathing 0..2,
        // a walk or a dash 0..3 (and 0..1 breathing at rest); phase 3 tints the armour darker and the eyes of the hat's
        // shadow red. Faces right; WorldView flips it for the left.
        public static PixelImage Draw(BossView view, SamuraiPose pose, int frame, int phase)
        {
            var (stepA, liftA, stepB, liftB, blade, outOf) = MovesOf(pose, frame);
            var body = Compose(view, stepA, liftA, stepB, liftB);
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
                    lean = 2 + 2 * t;
                    stretch = -0.06f - 0.1f * t;
                    break;
                case SamuraiPose.Draw:
                    lean = new[] { 7, 5, 2 }[System.Math.Min(frame, 2)];
                    stretch = new[] { -0.14f, -0.06f, 0.02f }[System.Math.Min(frame, 2)];
                    break;
                case SamuraiPose.Sheathe:
                    lean = 1;
                    stretch = -0.03f;
                    break;
                case SamuraiPose.Dash:
                    lean = 9;
                    stretch = -0.16f;
                    break;
                case SamuraiPose.Jodan:
                    lean = -1 - t;
                    stretch = 0.03f + 0.04f * t;
                    break;
                case SamuraiPose.Kiai:
                    lean = -3;
                    stretch = 0.05f;
                    break;
            }
            if (!side)
            {
                stretch -= lean * 0.012f;
                lean = 0;
            }

            int w = body.Width, h = body.Height;
            int tall = System.Math.Max(4, (int)System.Math.Round(h * (1 + stretch)));
            int wide = System.Math.Max(4, (int)System.Math.Round(w * (1 - stretch * 0.5f)));
            var image = new PixelImage(Width, Height);
            int left = (Width - wide) / 2;
            int ground = Height - 1;
            var hand = Frame(view).Hand;
            int handX = 0, handY = 0;
            for (int y = 0; y < tall; y++)
            {
                int row = ground - (tall - 1 - y);
                if (row < 0) continue;
                int sy = y * h / tall;
                float up = (tall - 1 - y) / (float)(tall - 1);
                int shift = (int)System.Math.Round(lean * up);
                if (sy == hand.Y + Headroom)
                {
                    handX = left + (int)System.Math.Round((hand.X + Margin) * wide / (float)w) + shift;
                    handY = row;
                }
                for (int x = 0; x < wide; x++)
                {
                    var c = body[x * w / wide, sy];
                    if (c.A == 0) continue;
                    if (phase >= 3 && c.R > c.G + 40) c = c.Mix(new Rgba(70, 8, 16), 0.35f);
                    int px = left + x + shift;
                    if (image.Inside(px, row)) image[px, row] = c;
                }
            }
            if (blade is float angle)
            {
                // From the side ahead is right; from the front ahead is toward the viewer (down and a little aside),
                // from behind away (up); the blade is drawn behind the body seen from the back.
                float screen = side ? angle : view == BossView.Front ? -60 + angle * 1.2f : 110 - angle * 0.4f;
                int length = side ? 26 : 22;
                if (view == BossView.Back)
                {
                    var front = image.Copy();
                    image = new PixelImage(Width, Height);
                    Blade(image, handX, handY, screen, outOf, length);
                    image.Draw(front, 0, 0);
                }
                else
                {
                    Blade(image, handX, handY, screen, outOf, length);
                }
            }
            else if (pose == SamuraiPose.Stance && frame >= 2)
            {
                // The hilt catching the light, brighter as the draw nears.
                image[handX, handY - 1] = Glint;
                if (frame >= 4)
                {
                    image[handX - 1, handY - 1] = Glint;
                    image[handX + 1, handY - 1] = Glint;
                    image[handX, handY - 2] = Glint;
                }
            }
            return image;
        }
    }
}
