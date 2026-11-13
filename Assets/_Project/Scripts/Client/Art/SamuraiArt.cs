using System.Collections.Generic;
using System.Linq;

namespace Game.Client
{
    // What the shrine's samurai is doing, which picks its frame (WorldView.Bosses.cs). Its attacks are iaido, in the
    // order of the art: a long, plain stance with the right hand on the hilt at the left hip (Stance); the draw that is
    // the cut, the blade leaving the scabbard level at full reach (Draw, nukitsuke); for a cut from above, the blade
    // raised overhead (Jodan, furikaburi) and brought down (Kiri, kirioroshi); the blood shaken off (Chiburi); the blade
    // slid back home (Noto). A dash with the blade trailing (Dash) and the cut that ends it, swept round from behind
    // its right hip (Sweep), or, when another dash follows, the blade home in a flash and the body gathered again
    // (Again); the draw turned right round itself (Whirl); a dart between attacks with the blade still home (Rush) and
    // a shout (Kiai).
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
        Rush,
        Sweep,
        Whirl,
        Again,
    }

    // The samurai of the forgotten shrine (2026-11-12): a ronin in crimson lacquered armour and a straw hat, a katana
    // at the hip. Its five views come from one Retro Diffusion Pro picture turned eight ways (Docs/ArtSources.md,
    // $0.28); the drawn blade is drawn here in code from its hand, and every pose is made in code: the legs, the trunk,
    // the head and the hat move on their own (the rig below), and the sword arm is drawn in code.
    static class SamuraiArt
    {
        public const int Width = 120;
        public const int Height = 84;
        // Rows of the canvas under its feet, for a step, a lunge or the blade coming toward the viewer: WorldView stands
        // the sprite on this line, not on its bottom edge.
        public const int Foot = 14;
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

        // ---- The rig (2026-11-13, the person: the whole body must move with the cut, every frame's body, weapon and
        // continuity checked). Each view's picture is cut into parts that move on their own: the legs lean out from the
        // hip to where each foot stands (no longer boxes slid sideways), the trunk leans over them and sinks onto
        // them when it crouches (covering the thighs, so no row of the picture is squashed away), and the head and the
        // hat stay whole on top, the hat dipping over the face. The sword arm (upper arm, elbow, forearm, hand), the
        // katana and the arc of a cut are drawn in code.
        //
        // The hand and the blade are placed in the frame of the way the samurai aims, not the way the picture looks:
        // so many pixels ahead, to its right and up from the scabbard's mouth, the blade turned so far to the left
        // (yaw) and raised (pitch). Each view lays that on the screen as the camera sees the ground (Ground), so a cut
        // points where the attack goes whichever of the eight ways it faces, and whichever way the trunk is turned
        // under it (TwistOf). ----

        sealed class Rig
        {
            // The legs (left, top, right, bottom, inclusive), the near or left one first. The side view's picture shows
            // one leg: both are drawn from it, the far one darker.
            public (int X0, int Y0, int X1, int Y1) A, B;
            public bool OneLeg;
            // The mouth of the scabbard at the left hip, where the right hand takes the hilt, and which way the
            // scabbard runs from it (degrees on the screen, 0 right, 90 up).
            public (int X, int Y) Hip;
            public float Scabbard;
            // Where the sword arm hangs from; the picture's own right hand, painted out while the arm is drawn; the
            // picture's own hilt (the side view's sticks out ahead), taken away once the hand has it.
            public (int X, int Y) Shoulder;
            public (int X0, int Y0, int X1, int Y1) Hand, Hilt;
            // The scabbard's tail where the view shows it, drawn back along its line as the blade leaves it (sayabiki).
            public (int X0, int Y0, int X1, int Y1) Tail;
            // Rows of the picture: the hat is above Brim, the head above Neck, the legs from Waist down.
            public int Brim, Neck, Waist;
            // The way the view looks: 0 right, 90 away from the viewer, -90 at the viewer. How far a nod dips the hat.
            // How far behind the body the hand at the hilt is (over 2 pixels it is drawn behind). Which way the elbow
            // bends out on the screen.
            public float Yaw, Nod, Away;
            public (float X, float Y) Elbow;
        }

        static readonly (int, int, int, int) NoBox = (0, 0, -1, -1);

        static readonly Dictionary<BossView, Rig> Rigs = new Dictionary<BossView, Rig>
        {
            [BossView.Front] = new Rig
            {
                A = (12, 30, 18, 37), B = (21, 30, 28, 37), Hip = (22, 23), Scabbard = -8, Shoulder = (14, 15), Hand = (11, 22, 13, 24), Hilt = NoBox, Tail = (28, 23, 36, 26),
                Brim = 10, Neck = 14, Waist = 30, Yaw = -90, Nod = 1, Away = -2, Elbow = (-1, 0.6f),
            },
            [BossView.FrontSide] = new Rig
            {
                A = (13, 29, 19, 38), B = (20, 29, 26, 33), Hip = (25, 22), Scabbard = 200, Shoulder = (18, 15), Hand = (11, 25, 13, 27), Hilt = NoBox, Tail = NoBox,
                Brim = 10, Neck = 13, Waist = 29, Yaw = -45, Nod = 1, Away = -1, Elbow = (-0.7f, 0.7f),
            },
            [BossView.Side] = new Rig
            {
                A = (15, 30, 22, 40), B = (15, 30, 22, 40), OneLeg = true, Hip = (24, 20), Scabbard = 213, Shoulder = (19, 16), Hand = (16, 27, 18, 30),
                Hilt = (24, 16, 30, 21), Tail = NoBox, Brim = 11, Neck = 14, Waist = 30, Yaw = 0, Nod = 1, Away = 0, Elbow = (-1, 0.5f),
            },
            [BossView.BackSide] = new Rig
            {
                A = (14, 29, 18, 34), B = (19, 29, 26, 37), Hip = (25, 26), Scabbard = 190, Shoulder = (24, 15), Hand = (26, 26, 27, 27), Hilt = NoBox, Tail = (4, 22, 11, 24),
                Brim = 10, Neck = 12, Waist = 29, Yaw = 45, Nod = 0, Away = 0, Elbow = (1, 0.5f),
            },
            [BossView.Back] = new Rig
            {
                A = (13, 29, 18, 37), B = (21, 29, 27, 37), Hip = (26, 24), Scabbard = 180, Shoulder = (26, 15), Hand = (27, 24, 28, 25), Hilt = NoBox, Tail = (2, 23, 11, 27),
                Brim = 10, Neck = 12, Waist = 29, Yaw = 90, Nod = 0, Away = 6, Elbow = (1, 0.4f),
            },
        };

        // Where the picture's top left corner lands on the canvas, standing still.
        const int OriginX = (Width - 40) / 2;
        const int OriginY = Height - Foot - 42;
        // How long a pixel along the ground shows when it runs toward or away from the viewer.
        const float Ground = 0.6f;
        const int BladeLength = 26;
        const int HiltLength = 6;
        const float Rad = (float)(System.Math.PI / 180);

        static readonly Rgba Steel = new Rgba(214, 224, 234);
        static readonly Rgba Edge = Rgba.White;
        static readonly Rgba SteelShade = new Rgba(140, 152, 168);
        static readonly Rgba Guard = new Rgba(212, 170, 72);
        static readonly Rgba Grip = new Rgba(40, 24, 30);
        static readonly Rgba Wrap = new Rgba(118, 88, 60);
        static readonly Rgba Skin = new Rgba(222, 180, 132);
        static readonly Rgba Sleeve = new Rgba(104, 30, 40);
        static readonly Rgba SleeveLight = new Rgba(136, 56, 58);
        static readonly Rgba SleeveShade = new Rgba(62, 16, 30);
        static readonly Rgba Bracer = new Rgba(58, 54, 58);
        static readonly Rgba Ink = new Rgba(12, 6, 8);
        static readonly Rgba Glint = new Rgba(255, 250, 220);
        static readonly Rgba Blossom = new Rgba(255, 160, 198);
        static readonly Rgba PaleBlue = new Rgba(176, 206, 240);

        // ---- The frames (2026-11-13, the person: more frames for natural motion, each checked against the frames either
        // side). A pose is a set of numbers eased between key poses. The right hand: how far it has come to the hilt
        // (Grip, 0 hanging at its side .. 1 holding it) and, holding it, where it is from the scabbard's mouth in the
        // frame of the aim (Ahead, Side toward its right, Up, in pixels). The blade: how far it is turned to the left
        // of the aim (Yaw) and raised (Pitch), or lying along the scabbard (Scabbard 1, when the hand is as far out
        // along the scabbard as the blade is drawn), and how much of it is out (Out, 0 sheathed .. 1). The feet's steps
        // along the aim and lifts; the trunk's lean, how far it is carried ahead (Shove) and sunk (Drop); the hat's dip
        // (Nod) and the head thrown ahead of the trunk or left behind it (Head); the glint at the scabbard's mouth; the
        // arc the blade has just come through (Smear: 1 a level draw round the front, 2 down from overhead; Trail its
        // strength, 1 the cut itself and less as it fades; 3 and 4 are the level cut out of a dash and turned right
        // round); drops flicked off the point (Flick); how far the left hand
        // has drawn the scabbard back (Saya).
        // The beat stays the person's: a long still stance, a slow pull, the draw in an instant, the blade home in a
        // flash (Docs/Bosses.md). ----

        struct SPose
        {
            public float Grip, Ahead, Side, Up, Yaw, Pitch, Scabbard, Out, StepA, LiftA, StepB, LiftB, Lean, Shove, Drop, Nod, Head, Glint, Trail, Flick, Saya;
            public int Smear;
        }

        static SPose P(float lean = 0, float shove = 0, float drop = 0, float stepA = 0, float stepB = 0, float liftA = 0, float liftB = 0, float nod = 0, float head = 0,
            float grip = 0, float ahead = 0, float side = 0, float up = 0, float yaw = 0, float pitch = 0, float scabbard = 1, float outOf = 0, float glint = 0,
            int smear = 0, float trail = 0, float flick = 0, float saya = 0) => new SPose
        {
            Lean = lean, Shove = shove, Drop = drop, StepA = stepA, StepB = stepB, LiftA = liftA, LiftB = liftB, Nod = nod, Head = head, Grip = grip,
            Ahead = ahead, Side = side, Up = up, Yaw = yaw, Pitch = pitch, Scabbard = scabbard, Out = outOf, Glint = glint, Smear = smear, Trail = trail, Flick = flick, Saya = saya,
        };

        static SPose Lerp(SPose a, SPose b, float u)
        {
            float L(float x, float y) => x + (y - x) * u;
            return new SPose
            {
                Grip = L(a.Grip, b.Grip), Ahead = L(a.Ahead, b.Ahead), Side = L(a.Side, b.Side), Up = L(a.Up, b.Up), Yaw = L(a.Yaw, b.Yaw), Pitch = L(a.Pitch, b.Pitch),
                Scabbard = L(a.Scabbard, b.Scabbard), Out = L(a.Out, b.Out), StepA = L(a.StepA, b.StepA), LiftA = L(a.LiftA, b.LiftA), StepB = L(a.StepB, b.StepB),
                LiftB = L(a.LiftB, b.LiftB), Lean = L(a.Lean, b.Lean), Shove = L(a.Shove, b.Shove), Drop = L(a.Drop, b.Drop), Nod = L(a.Nod, b.Nod), Head = L(a.Head, b.Head),
                Glint = L(a.Glint, b.Glint), Trail = L(a.Trail, b.Trail), Saya = L(a.Saya, b.Saya), Flick = u < 0.5f ? a.Flick : b.Flick, Smear = u < 0.5f ? a.Smear : b.Smear,
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
        // The hand come to the hilt, the weight settling.
        static readonly SPose Set = P(lean: 3, drop: 2, stepA: 3, stepB: -3, nod: 1, grip: 1, glint: 1);
        // Low in the stance over a wide base, the hat down over the eyes, the thumb on the guard.
        static readonly SPose Coiled = P(lean: 6, shove: -1, drop: 4, stepA: 6, stepB: -5, nod: 2, grip: 1, glint: 3, saya: 1);
        // The blade eased a third out; the body sunk back onto the back foot just before the snap (anticipation).
        static readonly SPose Pulled = P(lean: 4, shove: -3, drop: 5, stepA: 5, stepB: -6, nod: 2, head: -1, grip: 1, outOf: 0.34f, glint: 3, saya: 3);
        // Overhead, the blade drawn and raised (jodan), the weight back.
        static readonly SPose Raised = P(lean: -3, shove: -1, stepA: 2, stepB: -3, grip: 1, ahead: 1, up: 19, pitch: 138, scabbard: 0, outOf: 1);
        // After either cut, the blade out to the right, the back foot drawn up and the scabbard let forward again: where
        // the shake of the blood begins.
        static readonly SPose Recovered = P(lean: 4, shove: 7, drop: 2, stepA: 6, stepB: -3, grip: 1, ahead: 5, side: 6, up: 2, yaw: -34, pitch: 4, scabbard: 0, outOf: 1);

        // Nukitsuke: the blade out and round in one flash, the whole body thrown into it (its arc drawn, the front foot
        // far out, the trunk low over it, the hat a pixel ahead); the cut at full reach held as the arc fades; carried on
        // round to the right; recovered.
        static readonly SPose[] DrawFrames = Cut(
            P(lean: 8, shove: 9, drop: 3, stepA: 7, stepB: -6, head: 1, grip: 1, ahead: 9, side: 2, up: 2, yaw: -6, pitch: 1, scabbard: 0, outOf: 1, smear: 1, trail: 1, saya: 4),
            P(lean: 8, shove: 10, drop: 4, stepA: 7, stepB: -6, grip: 1, ahead: 10, side: 3, up: 2, yaw: -12, scabbard: 0, outOf: 1, smear: 1, trail: 0.6f, saya: 4),
            P(lean: 8, shove: 10, drop: 4, stepA: 7, stepB: -6, grip: 1, ahead: 10, side: 3, up: 1, yaw: -15, pitch: -1, scabbard: 0, outOf: 1, smear: 1, trail: 0.3f, saya: 4),
            P(lean: 6, shove: 9, drop: 3, stepA: 7, stepB: -5, grip: 1, ahead: 8, side: 6, up: 2, yaw: -40, pitch: 5, scabbard: 0, outOf: 1, smear: 1, saya: 4));

        // Kirioroshi, slanted as a kesa cut (from over the right shoulder down to the left, so it shows as a slash from
        // the front and the back too, not a line on end): the arc down from overhead, the cut held low in front, on
        // through, then out to the right.
        static readonly SPose[] KiriFrames = Cut(
            P(lean: 8, shove: 8, drop: 3, stepA: 7, stepB: -6, head: 1, grip: 1, ahead: 9, up: 5, yaw: 22, pitch: -22, scabbard: 0, outOf: 1, smear: 2, trail: 1),
            P(lean: 9, shove: 9, drop: 4, stepA: 7, stepB: -6, grip: 1, ahead: 10, up: 2, yaw: 22, pitch: -32, scabbard: 0, outOf: 1, smear: 2, trail: 0.6f),
            P(lean: 9, shove: 9, drop: 4, stepA: 7, stepB: -6, grip: 1, ahead: 10, up: 1, yaw: 22, pitch: -35, scabbard: 0, outOf: 1, smear: 2, trail: 0.3f),
            P(lean: 7, shove: 8, drop: 4, stepA: 7, stepB: -5, grip: 1, ahead: 9, side: 1, yaw: 22, pitch: -44, scabbard: 0, outOf: 1, smear: 2));

        static SPose[] Cut(SPose smear, SPose contact, SPose held, SPose carried) =>
            new[] { smear, contact, held }.Concat(Tween(held, carried, 4)).Concat(Tween(carried, Recovered, 4)).ToArray();

        // The same cut with another arc behind it: out of a dash the blade comes round from where it trailed, behind
        // the right hip (3); the turning draw goes right round the body (4).
        static readonly SPose[] SweepFrames = DrawFrames.Select(p => Arced(p, 3)).ToArray();
        static readonly SPose[] WhirlFrames = DrawFrames.Select(p => Arced(p, 4)).ToArray();

        static SPose Arced(SPose pose, int smear)
        {
            if (pose.Smear != 0) pose.Smear = smear;
            return pose;
        }

        // Yoko chiburi (Eishin-ryu): the blade opened out to the right and up, snapped level (drops flying off its
        // point), then still.
        static readonly SPose Opened = P(lean: 3, shove: 7, drop: 2, stepA: 6, stepB: -3, grip: 1, ahead: 4, side: 8, up: 4, yaw: -36, pitch: 16, scabbard: 0, outOf: 1);
        static readonly SPose Snapped = P(lean: 4, shove: 7, drop: 3, stepA: 6, stepB: -3, grip: 1, ahead: 7, side: 8, yaw: -18, pitch: -12, scabbard: 0, outOf: 1);
        static readonly SPose[] ChiburiFrames = Tween(Recovered, Opened, 2).Concat(new[] { Flicked(Snapped), Snapped }).ToArray();

        static SPose Flicked(SPose pose)
        {
            pose.Flick = 1;
            return pose;
        }

        // Noto: the blade swung back to the scabbard's mouth (its back laid there, the hand out ahead of it), slid home
        // point first as the hand comes back to the hip; the hand left on the hilt, still low and the hat still down
        // (zanshin), then let go as it rises (four frames over the same 0.15 s).
        static readonly SPose Laid = P(lean: 3, shove: 6, drop: 2, stepA: 5, stepB: -3, nod: 1, grip: 1, yaw: -18, pitch: -12, outOf: 0.4f);
        static readonly SPose Home = P(lean: 3, shove: 4, drop: 3, stepA: 5, stepB: -4, nod: 2, grip: 1, glint: 3);
        static readonly SPose[] NotoFrames = Tween(Snapped, Laid, NotoTurn).Concat(Tween(Laid, Home, NotoSlide)).Concat(Tween(Home, Rest, 4)).ToArray();

        // Between the runs of a chain of dashes (the triple draw): the trailing blade laid to the scabbard and slid home
        // in a flash as the body gathers itself low again; the pull follows.
        static readonly SPose Trailing = P(lean: 8, shove: 4, drop: 3, nod: 1, head: 1, grip: 1, ahead: -3, side: 5, up: -1, yaw: 170, pitch: -10, scabbard: 0, outOf: 1);
        static readonly SPose Regathered = P(lean: 7, shove: 2, drop: 4, stepA: 4, stepB: -3, nod: 2, grip: 1, yaw: 170, pitch: -10, outOf: 0.4f);
        static readonly SPose[] AgainFrames = Tween(Trailing, Regathered, 3).Concat(Tween(Regathered, Coiled, 4)).ToArray();

        // How long the blade takes to go home between the runs of a chain.
        public static float AgainSeconds => AgainFrames.Length * FrameSeconds(SamuraiPose.Again, 0);

        // How many frames each pose has.
        public static int FramesOf(SamuraiPose pose) => pose switch
        {
            SamuraiPose.Idle => 4,
            SamuraiPose.Walk => 8,
            SamuraiPose.Stance => 16,
            SamuraiPose.Pull => 12,
            SamuraiPose.Jodan => 24,
            SamuraiPose.Draw or SamuraiPose.Sweep or SamuraiPose.Whirl => DrawFrames.Length,
            SamuraiPose.Kiri => KiriFrames.Length,
            SamuraiPose.Chiburi => ChiburiFrames.Length,
            SamuraiPose.Noto => NotoFrames.Length,
            SamuraiPose.Again => AgainFrames.Length,
            SamuraiPose.Dash => 8,
            SamuraiPose.Rush => 8,
            SamuraiPose.Kiai => KiaiRise + 4,
            _ => 4,
        };

        // The noto: frames turning the blade back to the scabbard and sliding it home; the frame where it is home and
        // the hand stays on the hilt (zanshin).
        const int NotoTurn = 5;
        const int NotoSlide = 4;
        public const int NotoHome = NotoTurn + NotoSlide - 1;

        // How long after the cut begins the blade is home (the click of the guard).
        public static float SecondsToHome => Enumerable.Range(0, FramesOf(SamuraiPose.Draw)).Sum(f => FrameSeconds(SamuraiPose.Draw, f))
            + Enumerable.Range(0, FramesOf(SamuraiPose.Chiburi)).Sum(f => FrameSeconds(SamuraiPose.Chiburi, f))
            + Enumerable.Range(0, NotoHome).Sum(f => FrameSeconds(SamuraiPose.Noto, f));

        // The frames the shout takes to draw and raise the blade, before it shakes there.
        public const int KiaiRise = 24;

        // How long each frame of the cut and what follows it is shown (seconds), the person's beat (2026-11-12): out and
        // through in a third of a second, the blood flicked off in a tenth, home in an eighth, the hand left on the hilt
        // a moment, let go. 0 for poses timed by their telegraph or a loop.
        public static float FrameSeconds(SamuraiPose pose, int frame) => pose switch
        {
            SamuraiPose.Draw or SamuraiPose.Kiri or SamuraiPose.Sweep or SamuraiPose.Whirl => frame == 0 ? 0.03f : frame == 1 ? 0.12f : frame == 2 ? 0.04f : 0.02f,
            SamuraiPose.Chiburi => frame < 2 ? 0.02f : frame == 2 ? 0.03f : 0.05f,
            SamuraiPose.Noto => frame < NotoHome ? 0.0135f : frame == NotoHome ? 0.29f : 0.0375f,
            SamuraiPose.Again => 0.0135f,
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
                    // Breathing: the trunk sinks a pixel and comes back.
                    return P(drop: f >= 2 ? 1 : 0);
                case SamuraiPose.Walk:
                    // Each foot swung out from the hip and lifted as it comes through; the trunk lowest with the feet apart.
                    return P(lean: 1, drop: System.Math.Abs(s) > 0.8f ? 1 : 0, stepA: 3 * s, stepB: -3 * s, liftA: 2 * System.Math.Max(0, c), liftB: 2 * System.Math.Max(0, -c));
                case SamuraiPose.Stance:
                    // The hand goes to the hilt first; then the body sinks over a widening base, the hat dipping over the
                    // eyes, lower the longer it holds, and the thumb eases the guard (the glint brightening).
                    return Keys(t, (0, Rest), (0.3f, Set), (1, Coiled));
                case SamuraiPose.Pull:
                    // The slow start of the draw: the hilt eased out along the scabbard and the blade showing a little more
                    // each frame; at the end the body sinks back before the snap.
                    return Keys(t, (0, Coiled), (0.7f, P(lean: 6, shove: -1.5f, drop: 4, stepA: 6, stepB: -5, nod: 2, grip: 1, outOf: 0.24f, glint: 3, saya: 2)), (1, Pulled));
                case SamuraiPose.Jodan:
                    // Furikaburi: out of the scabbard, the point swung up past the left ear (behind the hat, as the far
                    // side is) and over, the hands raised above the head, higher through the wind-up as the weight goes
                    // back.
                    return Keys(t, (0, Pulled), (0.2f, P(lean: 3, shove: -2, drop: 4, stepA: 4, stepB: -5, nod: 1, grip: 1, yaw: -50, pitch: 150, outOf: 0.52f)),
                        (0.58f, P(lean: 1, shove: -1, drop: 2, stepA: 3, stepB: -3, grip: 1, ahead: 4, side: -1, up: 10, yaw: -50, pitch: 150, scabbard: 0, outOf: 1)), (1, Raised));
                case SamuraiPose.Draw:
                    return DrawFrames[f];
                case SamuraiPose.Sweep:
                    return SweepFrames[f];
                case SamuraiPose.Whirl:
                    return WhirlFrames[f];
                case SamuraiPose.Kiri:
                    return KiriFrames[f];
                case SamuraiPose.Chiburi:
                    return ChiburiFrames[f];
                case SamuraiPose.Noto:
                    return NotoFrames[f];
                case SamuraiPose.Again:
                    return AgainFrames[f];
                case SamuraiPose.Dash:
                    // The dash of a cut: low and far forward, the blade out and trailing behind its right hip.
                    return P(lean: 8, shove: 4, drop: System.Math.Abs(s) > 0.7f ? 4 : 3, stepA: 6 * s, stepB: -6 * s, liftA: 3 * System.Math.Max(0, c), liftB: 3 * System.Math.Max(0, -c),
                        nod: 1, head: 1, grip: 1, ahead: -3, side: 5, up: -1, yaw: 170, pitch: -10 + 3 * c, scabbard: 0, outOf: 1);
                case SamuraiPose.Rush:
                    // Darting in between attacks: as low, but the blade still home and the hand on its hilt.
                    return P(lean: 8, shove: 3, drop: System.Math.Abs(s) > 0.7f ? 4 : 3, stepA: 6 * s, stepB: -6 * s, liftA: 3 * System.Math.Max(0, c), liftB: 3 * System.Math.Max(0, -c),
                        nod: 2, head: 1, grip: 1, glint: 2);
                case SamuraiPose.Kiai:
                    // The blade drawn and raised high, then held there shaking with the shout.
                    if (f < KiaiRise)
                        return Keys(f / (float)KiaiRise, (0, Rest), (0.13f, P(lean: 2, drop: 1, nod: 1, grip: 1, glint: 2)), (0.26f, P(lean: 2, drop: 2, nod: 1, grip: 1, outOf: 0.34f)),
                            (0.36f, P(lean: 1, drop: 2, nod: 1, grip: 1, yaw: -40, pitch: 140, outOf: 0.52f)),
                            (0.76f, P(lean: -1, stepA: 1, stepB: -1, grip: 1, ahead: 4, side: -1, up: 10, yaw: -40, pitch: 140, scabbard: 0, outOf: 1)),
                            (1, P(lean: -3, stepA: 2, stepB: -2, grip: 1, ahead: 2, up: 18, pitch: 98, scabbard: 0, outOf: 1)));
                    return P(lean: -3, stepA: 2, stepB: -2, grip: 1, ahead: 2, up: 18 + (s + 1) / 2, pitch: 98 + 3 * c, scabbard: 0, outOf: 1);
                default:
                    return Rest;
            }
        }

        // How the trunk turns with the cut, a step of the eight views: wound away from the viewer while it gathers (the
        // shoulder turned in, the scabbard drawn back), unwound toward the viewer as the blade comes through, and kept
        // so through the follow-through and the shake of the blood; square again as the blade turns home. It turns once
        // each way and never back and forth.
        public static int TwistOf(SamuraiPose pose, int frame) => pose switch
        {
            SamuraiPose.Stance => frame >= FramesOf(pose) / 3 ? -1 : 0,
            SamuraiPose.Pull => -1,
            SamuraiPose.Jodan => frame < FramesOf(pose) / 4 ? -1 : 0,
            SamuraiPose.Draw or SamuraiPose.Kiri or SamuraiPose.Sweep or SamuraiPose.Whirl or SamuraiPose.Chiburi => 1,
            SamuraiPose.Noto => frame < NotoTurn ? 1 : 0,
            SamuraiPose.Again => frame < 3 ? 0 : -1,
            _ => 0,
        };

        // The eight ways it can face (0 right, then round by the left: 2 up the screen, 4 left, 6 down), as a view and
        // whether the picture is mirrored (WorldView.BossSectors).
        static readonly (BossView View, bool Mirrored)[] Sectors =
        {
            (BossView.Side, false), (BossView.BackSide, false), (BossView.Back, false), (BossView.BackSide, true),
            (BossView.Side, true), (BossView.FrontSide, true), (BossView.Front, false), (BossView.FrontSide, false),
        };

        // How a frame is shown facing one of the eight ways: the view its trunk is turned to by the frame's twist (a
        // step toward the viewer or away; facing straight up or down, a step aside, as WorldView.Twist), whether the
        // picture is mirrored, and the aim to draw it with so the hand and the blade keep to the way it faces.
        public static (BossView View, bool Mirrored, int Aim) Facing(int sector, SamuraiPose pose, int frame)
        {
            int twist = TwistOf(pose, frame), turned = sector;
            if (twist != 0)
            {
                int target = twist > 0 ? 6 : 2;
                if (sector == target || sector == (target + 4) % 8) turned = (sector + twist + 8) % 8;
                else turned = (target - sector + 8) % 8 < 4 ? (sector + 1) % 8 : (sector + 7) % 8;
            }
            int offset = (sector - turned + 12) % 8 - 4;
            var (view, mirrored) = Sectors[turned];
            return (view, mirrored, mirrored ? -offset : offset);
        }

        // The mouth of the scabbard in a view standing at rest: pixels right of the picture's middle and above its foot
        // line (for the glint before a draw and the click of the guard).
        public static (int X, int Y) HipOf(BossView view) => (Rigs[view].Hip.X - 20, 41 - Rigs[view].Hip.Y);

        // One frame, facing right (WorldView flips it for the left). aim is how the way it aims lies from the way the
        // view looks, in steps of the eight ways (1 a step to the left, as when the trunk is turned to the right of its
        // aim): the hand and the blade follow the aim. Phase 3 darkens the armour.
        public static PixelImage Draw(BossView view, SamuraiPose pose, int frame, int phase, int aim = 0) => Render(view, pose, frame, phase, aim, out _);

        // Where the hand and the blade's tip are, and the body's edges, in a frame: for the preview's check that nothing
        // jumps from one frame to the next.
        public struct Joints
        {
            public float HandX, HandY, TipX, TipY;
            public int Top, Bottom;
        }

        public static Joints JointsOf(BossView view, SamuraiPose pose, int frame)
        {
            Render(view, pose, frame, 1, 0, out var joints);
            return joints;
        }

        static int R(float v) => (int)System.Math.Round(v);

        static float Turn(float degrees)
        {
            degrees %= 360;
            return degrees > 180 ? degrees - 360 : degrees <= -180 ? degrees + 360 : degrees;
        }

        static PixelImage Render(BossView view, SamuraiPose pose, int frame, int phase, int aim, out Joints joints)
        {
            var p = PoseOf(pose, frame);
            var rig = Rigs[view];
            var art = ViewOf(view);
            float way = (rig.Yaw + 45 * aim) * Rad;
            float cos = (float)System.Math.Cos(way), sin = (float)System.Math.Sin(way);
            // So far ahead along the aim, to its left and up, as a step on the screen (x right, y down); and how far
            // away from the viewer that is.
            (float X, float Y) Screen(float ahead, float left, float up) => (ahead * cos - left * sin, -(up + Ground * (ahead * sin + left * cos)));
            float Away(float ahead, float left) => ahead * sin + left * cos;

            var body = new PixelImage(Width, Height);
            var under = new PixelImage(Width, Height);
            var over = new PixelImage(Width, Height);
            var arcUnder = new PixelImage(Width, Height);
            var arcOver = new PixelImage(Width, Height);
            Rgba Tone(Rgba c) => phase >= 3 && c.R > c.G + 40 ? c.Mix(new Rgba(70, 8, 16), 0.35f) : c;

            // The whole body carried along its aim; the trunk sunk onto the legs and leaning over them; the head whole
            // on top, coming down over the chest when it leans at the viewer.
            var carry = Screen(p.Shove, 0, 0);
            int ox = OriginX + R(carry.X), oy = OriginY + R(carry.Y);
            var lean = Screen(p.Lean, 0, 0);
            var head = Screen(p.Head, 0, 0);
            int drop = R(p.Drop + System.Math.Max(0, -lean.Y) * 0.25f);
            int bow = R(System.Math.Max(0, lean.Y) * 0.7f + System.Math.Max(0, head.Y));
            int nod = System.Math.Max(0, R(p.Nod * rig.Nod));
            int Shift(int y) => R(lean.X * System.Math.Min(1, System.Math.Max(0, (rig.Waist - y) / (float)(rig.Waist - rig.Neck)))) + (y < rig.Neck ? R(head.X) : 0);
            int Sink(int y) => drop + (y < rig.Neck ? bow : 0) + (y < rig.Brim ? nod : 0);
            (int X, int Y) Place(int x, int y) => (ox + x + Shift(y), oy + y + Sink(y));
            bool In((int X0, int Y0, int X1, int Y1) box, int x, int y) => x >= box.X0 && x <= box.X1 && y >= box.Y0 && y <= box.Y1;

            // Whether the arm drawn here is out (the picture's own right hand is then painted into the armour), and
            // whether it holds the sword.
            bool armed = p.Grip > 0.05f, held = p.Grip >= 0.9f;
            var painted = new Rgba(56, 21, 33);
            bool Skinned(Rgba c) => c.R > 100 && c.G > 80;

            // The legs: each from the hip to where its foot stands, the far one first.
            // Seen along its aim (from the front or behind) a wide base shows as the feet apart to either side.
            float wide = (System.Math.Abs(p.StepA) + System.Math.Abs(p.StepB)) * 0.2f * System.Math.Abs(sin);
            void Leg((int X0, int Y0, int X1, int Y1) box, float step, float lift, bool far)
            {
                // A step toward the viewer shows as a longer leg, one away as a shorter: less than the ground's own
                // measure, or the near leg reads as a stilt and the far one is lost under the trunk.
                var foot = Screen(step, 0, 0);
                if (!rig.OneLeg) foot.X += box.X0 + box.X1 > 40 ? wide : -wide;
                int length = box.Y1 - box.Y0;
                int rows = System.Math.Max(R(length * 0.7f), R(length + foot.Y * 0.5f));
                // A short far leg (the views from a corner) would vanish under a trunk sunk low: its foot is planted
                // nearer, so some of it always shows under the hem.
                rows = System.Math.Max(rows, rig.Waist + drop + 3 - box.Y0);
                var drawn = new List<(int X, int Y)>();
                for (int r = 0; r <= rows; r++)
                {
                    float t = r / (float)rows;
                    int source = box.Y0 + R(t * length);
                    int dx = R(foot.X * t), y = oy + box.Y0 - R(lift) + r;
                    for (int x = box.X0; x <= box.X1; x++)
                    {
                        var c = art[x, source];
                        if (c.A == 0) continue;
                        if (armed && In(rig.Hand, x, source) && Skinned(c)) c = painted;
                        body[ox + x + dx, y] = far ? Tone(c).Scale(0.7f) : Tone(c);
                        drawn.Add((ox + x + dx, y));
                    }
                }
                foreach (var (x, y) in drawn)
                {
                    if (!body.IsSet(x - 1, y)) body[x - 1, y] = Ink;
                    if (!body.IsSet(x + 1, y)) body[x + 1, y] = Ink;
                    if (!body.IsSet(x, y + 1)) body[x, y + 1] = Ink;
                }
            }
            // The leg that stands further along the aim on the screen takes the step ahead, so the feet never cross
            // under a trunk turned across its aim.
            bool crossed = !rig.OneLeg && cos * (rig.B.X0 + rig.B.X1 - rig.A.X0 - rig.A.X1) > 1 && System.Math.Abs(cos) > 0.3f;
            Leg(rig.B, crossed ? p.StepA : p.StepB, crossed ? p.LiftA : p.LiftB, rig.OneLeg);
            Leg(rig.A, crossed ? p.StepB : p.StepA, crossed ? p.LiftB : p.LiftA, false);

            int saya = R(p.Saya), tailWay = System.Math.Cos(rig.Scabbard * Rad) >= 0 ? 1 : -1;
            // The trunk, then the head, then the hat: each may come down over the one below.
            for (int part = 0; part < 3; part++)
            {
                int top = part == 0 ? rig.Neck : part == 1 ? rig.Brim : 0, bottom = part == 0 ? art.Height : part == 1 ? rig.Neck : rig.Brim;
                for (int y = bottom - 1; y >= top; y--)
                {
                    for (int x = 0; x < art.Width; x++)
                    {
                        var c = art[x, y];
                        if (c.A == 0 || In(rig.A, x, y) || In(rig.B, x, y)) continue;
                        // The picture's own hilt goes with the hand that has taken it, and its own right hand is
                        // painted into the armour while the arm drawn here does its work.
                        if (held && In(rig.Hilt, x, y)) c = x == rig.Hilt.X0 && y > rig.Hilt.Y0 ? Ink : Rgba.Clear;
                        else if (armed && In(rig.Hand, x, y) && Skinned(c)) c = painted;
                        if (c.A == 0) continue;
                        var at = Place(x, y);
                        if (saya > 0 && In(rig.Tail, x, y))
                        {
                            // Sayabiki: the tail moves out along its line, the stretch it leaves filled from its root.
                            if (x == (tailWay > 0 ? rig.Tail.X0 : rig.Tail.X1))
                                for (int k = 0; k < saya; k++) body[at.X + k * tailWay, at.Y] = Tone(c);
                            at.X += saya * tailWay;
                        }
                        body[at.X, at.Y] = Tone(c);
                    }
                }
            }
            joints = new Joints { Top = int.MaxValue, Bottom = -1 };
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (!body.IsSet(x, y)) continue;
                    joints.Top = System.Math.Min(joints.Top, y);
                    joints.Bottom = System.Math.Max(joints.Bottom, y);
                }
            }

            var mouth = Place(rig.Hip.X, rig.Hip.Y);
            var shoulder = Place(rig.Shoulder.X, rig.Shoulder.Y);
            var hanging = Place((rig.Hand.X0 + rig.Hand.X1) / 2, (rig.Hand.Y0 + rig.Hand.Y1) / 2);

            // The hand: out along the scabbard as far as the blade is drawn while it lies there, and wherever the pose
            // puts it once free; on its way to the hilt, between where it hangs and there.
            float axis = (rig.Scabbard + 180) * Rad;
            float pull = p.Scabbard * p.Out * BladeLength;
            var free = Screen(p.Ahead, -p.Side, p.Up);
            float gripX = mouth.X + pull * (float)System.Math.Cos(axis) + free.X, gripY = mouth.Y - pull * (float)System.Math.Sin(axis) + free.Y;
            float handX = hanging.X + (gripX - hanging.X) * p.Grip, handY = hanging.Y + (gripY - hanging.Y) * p.Grip;
            int hx = R(handX), hy = R(handY);
            float handAway = rig.Away + Away(p.Ahead, -p.Side) * (1 - p.Scabbard);

            // The blade's way: along the scabbard, or the pose's own as this view sees it, or between them.
            float by = p.Yaw * Rad, bp = p.Pitch * Rad;
            float along = (float)(System.Math.Cos(bp) * System.Math.Cos(by)), aside = (float)(System.Math.Cos(bp) * System.Math.Sin(by)), rise = (float)System.Math.Sin(bp);
            var seenAs = Screen(along, aside, rise);
            float seenLength = (float)System.Math.Sqrt(seenAs.X * seenAs.X + seenAs.Y * seenAs.Y);
            float freeAngle = (float)(System.Math.Atan2(-seenAs.Y, seenAs.X) / Rad);
            float angle = rig.Scabbard + Turn(freeAngle - rig.Scabbard) * (1 - p.Scabbard);
            float seen = 1 + (System.Math.Min(1, System.Math.Max(0.4f, seenLength)) - 1) * (1 - p.Scabbard);
            float shown = BladeLength * seen * p.Out;
            float bx = (float)System.Math.Cos(angle * Rad), bY = -(float)System.Math.Sin(angle * Rad);
            // How fast the blade runs away from the viewer along its length on the screen.
            float bladeAway = Away(along, aside) / System.Math.Max(0.4f, seenLength) * (1 - p.Scabbard);

            joints.HandX = handX;
            joints.HandY = handY;
            joints.TipX = held ? handX + bx * shown : handX;
            joints.TipY = held ? handY + bY * shown : handY;

            if (armed)
            {
                Arm(handAway > 2 ? under : over, body, shoulder.X, shoulder.Y, hx, hy, rig.Elbow, !held);
                if (held)
                {
                    if (p.Smear != 0 && p.Trail > 0.02f)
                    {
                        // The arc the blade has come through, round the hand: a level draw from the left hip round the
                        // front; a cut from above over the top and down in the blade's own slanted plane; out of a dash,
                        // round by the right from where it trailed; the turning draw, right round.
                        var (yaw0, pitch0) = p.Smear switch
                        {
                            1 => (105f, 0f),
                            3 => (-155f, -6f),
                            4 => (340f, 0f),
                            _ => (p.Yaw, 152f),
                        };
                        Arc(arcUnder, arcOver, handX, handY, yaw0, pitch0, p.Yaw, p.Pitch, p.Trail, handAway, Screen, Away);
                    }
                    Blade(under, over, body, hx, hy, bx, bY, R(shown), handAway, bladeAway);
                    if (p.Flick > 0.5f)
                    {
                        // Drops thrown off the point by the snap.
                        float tx = handX + bx * shown, ty = handY + bY * shown;
                        over[R(tx + bx * 3), R(ty + bY * 3 + 1)] = Glint;
                        over[R(tx + bx * 6 + 1), R(ty + bY * 6 + 3)] = Blossom;
                        over[R(tx + bx * 2 - 1), R(ty + bY * 2 + 4)] = Blossom;
                        over[R(tx + bx * 8), R(ty + bY * 8 + 2)] = Glint.WithAlpha(170);
                    }
                }
            }
            if (p.Glint >= 0.5f && p.Out < 0.05f)
            {
                // Koiguchi: the thumb eases the guard from the scabbard, which catches the light, brighter as the draw
                // nears.
                over[mouth.X, mouth.Y - 1] = Glint;
                if (p.Glint >= 1.5f)
                {
                    over[mouth.X - 1, mouth.Y - 1] = Glint;
                    over[mouth.X + 1, mouth.Y - 1] = Glint;
                    over[mouth.X, mouth.Y - 2] = Glint;
                }
                if (p.Glint >= 2.5f)
                {
                    over[mouth.X, mouth.Y] = Glint;
                    over[mouth.X - 2, mouth.Y - 1] = Glint.WithAlpha(150);
                    over[mouth.X + 2, mouth.Y - 1] = Glint.WithAlpha(150);
                    over[mouth.X, mouth.Y - 3] = Glint.WithAlpha(150);
                }
            }

            // Back to front: the arc and the blade where they pass behind, the body, the arc in front, the arm and blade.
            arcUnder.Draw(under, 0, 0);
            arcUnder.Draw(body, 0, 0);
            arcUnder.Draw(arcOver, 0, 0);
            arcUnder.Draw(over, 0, 0);
            return arcUnder;
        }

        // A line of pixels so many wide (stacked across the way it mostly runs), collected for an outline.
        static void Stroke(PixelImage image, List<(int X, int Y)> drawn, float x0, float y0, float x1, float y1, int width, Rgba colour, Rgba light, Rgba shade)
        {
            float dx = x1 - x0, dy = y1 - y0;
            int steps = System.Math.Max(1, R(System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy))));
            bool flat = System.Math.Abs(dx) >= System.Math.Abs(dy);
            for (int i = 0; i <= steps; i++)
            {
                int x = R(x0 + dx * i / steps), y = R(y0 + dy * i / steps);
                for (int k = 0; k < width; k++)
                {
                    int px = flat ? x : x + k - width / 2, py = flat ? y + k - width / 2 : y;
                    image[px, py] = k == 0 && width > 1 ? light : k == width - 1 && width > 2 ? shade : colour;
                    drawn.Add((px, py));
                }
            }
        }

        // The dark line round something drawn: all round it over open ground, only under it where it lies over the body
        // (a shadow that lifts it off the armour without boxing it in).
        static void Outline(PixelImage image, PixelImage body, List<(int X, int Y)> drawn)
        {
            foreach (var (x, y) in drawn)
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                        if (image.Inside(x + ox, y + oy) && image[x + ox, y + oy].A == 0 && (body == null || !body.IsSet(x + ox, y + oy) || ox == 0 && oy == 1))
                            image[x + ox, y + oy] = Ink;
        }

        // The sword arm from the shoulder to the hand: a crimson sleeve over the upper arm, the elbow bent down and
        // back when the hand is near, a dark bracer on the forearm, and the hand itself when it holds nothing.
        static void Arm(PixelImage image, PixelImage body, int sx, int sy, int hx, int hy, (float X, float Y) bend, bool bare)
        {
            float dx = hx - sx, dy = hy - sy;
            float d = (float)System.Math.Sqrt(dx * dx + dy * dy);
            if (d < 2) return;
            const float bone = 7;
            float half = d / 2, off = half < bone ? (float)System.Math.Sqrt(bone * bone - half * half) : 0;
            float nx = -dy / d, ny = dx / d;
            if (nx * bend.X + ny * bend.Y < 0) (nx, ny) = (-nx, -ny);
            float ex = sx + dx / 2 + nx * off, ey = sy + dy / 2 + ny * off;
            var drawn = new List<(int X, int Y)>();
            Stroke(image, drawn, ex, ey, hx, hy, 2, Bracer, Sleeve, Bracer);
            Stroke(image, drawn, sx, sy, ex, ey, 3, Sleeve, SleeveLight, SleeveShade);
            if (bare)
            {
                foreach (var (x, y) in new[] { (hx, hy), (hx + 1, hy), (hx, hy + 1), (hx + 1, hy + 1) })
                {
                    image[x, y] = Skin;
                    drawn.Add((x, y));
                }
            }
            Outline(image, body, drawn);
        }

        // A katana held at (hx, hy), running along (bx, by) on the screen: a wrapped hilt back from the hand with the
        // hand round it, a gold guard, a pale blade with a white edge, as much of it as is out of the scabbard. The
        // parts of it far enough behind the body are drawn under it.
        static void Blade(PixelImage under, PixelImage over, PixelImage body, int hx, int hy, float bx, float by, int n, float handAway, float bladeAway)
        {
            // The edge lies a pixel to one side of the back: straight down or across, so a slanted blade stays solid.
            bool flat = System.Math.Abs(bx) >= System.Math.Abs(by);
            int ex = flat ? 0 : -by > 0 ? 1 : -1, ey = flat ? bx > 0 ? 1 : -1 : 0;
            var behind = new List<(int X, int Y)>();
            var before = new List<(int X, int Y)>();
            void Dot(int i, int x, int y, Rgba c)
            {
                bool back = handAway + i * bladeAway > 2;
                (back ? under : over)[x, y] = c;
                (back ? behind : before).Add((x, y));
            }
            for (int i = -HiltLength; i <= n; i++)
            {
                int x = hx + R(bx * i), y = hy + R(by * i);
                if (i < 0) Dot(i, x, y, i == -HiltLength ? Guard.Scale(0.75f) : i % 2 == 0 ? Grip : Wrap);
                else if (i == 0) Dot(i, x, y, Guard);
                else
                {
                    Dot(i, x, y, i >= n - 1 ? Edge : i % 4 == 1 ? SteelShade : Steel);
                    if (i < n - 1) Dot(i, x + ex, y + ey, Edge);
                }
            }
            Dot(0, hx + ex, hy + ey, Guard);
            Dot(0, hx - ex, hy - ey, Guard);
            // The hand round the hilt, behind the guard.
            for (int i = -2; i <= -1; i++)
            {
                int x = hx + R(bx * i), y = hy + R(by * i);
                Dot(i, x, y, Skin);
                Dot(i, x + ex, y + ey, Skin.Scale(0.85f));
            }
            Outline(under, null, behind);
            Outline(over, body, before);
        }

        // The arc of a cut too fast to see (2026-11-12, after pixel-art practice: one blurred in-between along the path
        // between two strong keys; 2026-11-13: swept in the frame of the aim, so every view sees the same cut): a
        // crescent round the hand from the way the blade pointed to the way it points now, broad and white at the
        // blade, thinning and blushing to blossom pink where it began. Weaker, it is the glow the cut leaves behind:
        // the rim only, then gone.
        static void Arc(PixelImage under, PixelImage over, float hx, float hy, float yaw0, float pitch0, float yaw1, float pitch1, float strength, float handAway,
            System.Func<float, float, float, (float X, float Y)> screen, System.Func<float, float, float> away)
        {
            int steps = (int)(System.Math.Max(System.Math.Abs(yaw1 - yaw0), System.Math.Abs(pitch1 - pitch0)) / 1.2f) + 1;
            float length = BladeLength;
            for (int i = 0; i <= steps; i++)
            {
                // 0 where the blade came from, 1 where it is.
                float t = i / (float)steps;
                float yaw = (yaw0 + (yaw1 - yaw0) * t) * Rad, pitch = (pitch0 + (pitch1 - pitch0) * t) * Rad;
                float ahead = (float)(System.Math.Cos(pitch) * System.Math.Cos(yaw)), left = (float)(System.Math.Cos(pitch) * System.Math.Sin(yaw)), up = (float)System.Math.Sin(pitch);
                var step = screen(ahead, left, up);
                float far = away(ahead, left);
                float age = 1 - t;
                float inner = length * (1 - strength * (0.1f + 0.5f * t * t));
                for (float r = inner; r <= length; r += 0.5f)
                {
                    int x = R(hx + step.X * r), y = R(hy + step.Y * r);
                    float rim = length - r;
                    Rgba colour;
                    float alpha;
                    if (rim < 1.5f)
                    {
                        colour = age < 0.35f ? Edge : Edge.Mix(Blossom, System.Math.Min(1, (age - 0.35f) * 1.8f));
                        alpha = 1 - 0.35f * age * age;
                    }
                    else if (rim < 4)
                    {
                        colour = Steel.Mix(Blossom, age);
                        alpha = 0.95f - 0.45f * age;
                    }
                    else
                    {
                        // Thinner the further behind the blade: more and more of it left out.
                        if (PixelNoise.Value(x, y, 11) > (1 - age) * (1 - age) * 1.15f) continue;
                        colour = PaleBlue.Mix(Blossom, 0.25f + 0.75f * age);
                        alpha = 0.85f - 0.5f * age;
                    }
                    int a = (int)(255 * System.Math.Min(1, System.Math.Max(0, alpha)) * (0.4f + 0.6f * strength));
                    var layer = handAway + r * far > 2 ? under : over;
                    if (layer.Inside(x, y) && layer[x, y].A < a) layer[x, y] = colour.WithAlpha(a);
                }
            }
        }
    }
}
