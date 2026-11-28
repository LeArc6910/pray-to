using System.Collections.Generic;

namespace Game.Client
{
    // What the crown wraith is doing, which picks its frame (WorldView.CrownWraith.cs, Docs/Bosses.md 왕관의 망령). A
    // drowned king risen from a sunken warship of the demon army: a skull under a rusted crown, barnacled armour, a
    // ghostly tail where its legs were, floating over the floor. Every attack has its own wind-up of the whole body:
    // the clawed hand drawn up and back crackling, then raked down through the wedge (Clutch, Rake); both arms raised
    // to the dark, then flung down as the bolts fall (Beckon, Call); the arm drawn back with a spear of lightning
    // forming in the hand, then thrown (Draw, Hurl); hunched low and going thin as a ghost, then the rush (Hunch,
    // Rush); the crown lifting off its head blazing, the arms opening, then the ring let go (Lift, Flare); rising
    // high, both arms up and the crown storming, then the arms swept down at everyone at once (Storm, Brand);
    // towering with a ball of lightning held overhead, then brought down on the shared mark (Loom, Smite); the fists
    // pulled in to the chest, then flung open (Clench, Pulse); a threat with the arms spread and the crown sparking.
    enum CrownWraithPose
    {
        Idle,
        Walk,
        Clutch,
        Rake,
        Beckon,
        Call,
        Draw,
        Hurl,
        Hunch,
        Rush,
        Lift,
        Flare,
        Storm,
        Brand,
        Loom,
        Smite,
        Clench,
        Pulse,
        Roar,
        // Its entrance: only its rusted crown lying on the chapel floor in a little mist; the crown stirs and rises
        // to where a head would be, lightning strikes it, the body gathers out of the mist down from it, the eyes
        // light, and it spreads its arms with a roar.
        Enter,
        // Its fall: thrown back, the eyes and the lightning flickering out, sagging, the body coming apart into mist
        // from the tail up, and the crown dropping to the floor where it was.
        Die,
    }

    // The crown wraith of the misty harbour (2026-11-28): one picture of the local image models (Z-Image-Turbo with a
    // pixel-art LoRA, turned eight ways by Qwen-Image-Edit with the multiple-angles LoRA, Docs/ArtSources.md) cut to
    // five views; every pose is made in code: the upper body leaning over its tail, the arms turned about their
    // shoulders, the crown lifted off the head, lightning crackling in the hands and over the crown, the body gone
    // thin as a ghost, gathered out of mist or coming apart into it. Faces right.
    static partial class CrownWraithArt
    {
        public const int Width = 96;
        public const int Height = 96;
        // Rows under its tail; WorldView stands the picture on this row (Pivot).
        public const int Floor = 4;
        // How tall the body itself stands, for the name and marks over it.
        public const int BodyHeight = 58;

        // Room round the picture for the arms raised and the crown lifted.
        const int Margin = 16;
        const int Headroom = 28;
        // The row of the picture its shadow lies on, and how far over it it floats at rest.
        const int Ground = 63;
        const float Hover = 2;

        enum Part : byte
        {
            None,
            Crown,
            Head,
            Arm1,
            Arm2,
            Tail,
            Body,
            Eye,
        }

        static Part PartOf(char c) => c switch
        {
            'K' => Part.Crown,
            'H' => Part.Head,
            '1' => Part.Arm1,
            '2' => Part.Arm2,
            'T' => Part.Tail,
            'B' => Part.Body,
            'E' => Part.Eye,
            _ => Part.None,
        };

        // What it knows about a view: its colours and parts, which way it faces on the screen, the crown's top, where
        // the tail leaves the body and its tip, the middle, the head and the crown, each arm's shoulder, hand and
        // reach.
        sealed class ViewData
        {
            public PixelImage Image;
            public Part[,] Parts;
            public float AheadX, AheadY;
            public float Top, Base, Bottom;
            public float MiddleX;
            public float HeadX, HeadY;
            public float CrownBottom;
            public float[] PivotX = new float[2], PivotY = new float[2];
            public int[] HandX = new int[2], HandY = new int[2];
            public float[] Reach = new float[2];
            // The angle each arm hangs at in the picture (degrees, the way the pose's swing or spread is measured).
            public float[] RestAngle = new float[2];
            // The way each arm turns outward in the front and back views (+1 its hand toward +x).
            public int[] Out = new int[2];
            public bool Facing;
        }

        static readonly Dictionary<BossView, ViewData> Views = new Dictionary<BossView, ViewData>();

        static ViewData ViewOf(BossView view)
        {
            if (Views.TryGetValue(view, out var known)) return known;
            var (rows, parts, pivots) = view switch
            {
                BossView.Front => (FrontRows, FrontParts, FrontPivots),
                BossView.FrontSide => (FrontSideRows, FrontSideParts, FrontSidePivots),
                BossView.Side => (SideRows, SideParts, SidePivots),
                BossView.BackSide => (BackSideRows, BackSideParts, BackSidePivots),
                _ => (BackRows, BackParts, BackPivots),
            };
            var data = new ViewData { Image = PixelImage.FromRows(Palette, rows), Parts = new Part[64, 64] };
            float headX = 0, headY = 0;
            int heads = 0, top = 64, tailTop = 64, bottom = -1, left = 64, right = -1, crownBottom = -1;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    var part = PartOf(parts[y][x]);
                    data.Parts[x, y] = part;
                    if (part == Part.None) continue;
                    top = System.Math.Min(top, y);
                    bottom = System.Math.Max(bottom, y);
                    if (part == Part.Head || part == Part.Eye)
                    {
                        headX += x;
                        headY += y;
                        heads++;
                    }
                    if (part == Part.Tail) tailTop = System.Math.Min(tailTop, y);
                    if (part == Part.Crown) crownBottom = System.Math.Max(crownBottom, y);
                    if (part == Part.Body)
                    {
                        left = System.Math.Min(left, x);
                        right = System.Math.Max(right, x);
                    }
                }
            data.HeadX = heads > 0 ? headX / heads : 32;
            data.HeadY = heads > 0 ? headY / heads : 14;
            data.Top = top;
            data.Bottom = System.Math.Max(top + 1, bottom);
            data.Base = tailTop < 64 ? tailTop : bottom - 8;
            data.MiddleX = left <= right ? (left + right) / 2f : 32;
            data.CrownBottom = crownBottom >= 0 ? crownBottom : top + 4;
            for (int k = 0; k < 2; k++)
            {
                data.PivotX[k] = pivots[k].X;
                data.PivotY[k] = pivots[k].Y;
                var arm = k == 0 ? Part.Arm1 : Part.Arm2;
                float far = -1;
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        if (data.Parts[x, y] != arm) continue;
                        float d = (x - pivots[k].X) * (x - pivots[k].X) + (y - pivots[k].Y) * (y - pivots[k].Y);
                        if (d <= far) continue;
                        far = d;
                        data.HandX[k] = x;
                        data.HandY[k] = y;
                    }
                data.Reach[k] = far > 0 ? (float)System.Math.Sqrt(far) : 0;
                data.Out[k] = pivots[k].X >= data.MiddleX ? 1 : -1;
            }
            data.Facing = view == BossView.Front || view == BossView.Back;
            for (int k = 0; k < 2; k++)
            {
                float dx = data.HandX[k] - data.PivotX[k], dy = data.HandY[k] - data.PivotY[k];
                data.RestAngle[k] = data.Reach[k] <= 0 ? 0 : (float)(System.Math.Atan2(data.Facing ? data.Out[k] * dx : dx, dy) * 180 / System.Math.PI);
            }
            (data.AheadX, data.AheadY) = view switch
            {
                BossView.Front => (0f, 1f),
                BossView.FrontSide => (0.72f, 0.7f),
                BossView.Side => (1f, 0f),
                BossView.BackSide => (0.72f, -0.7f),
                _ => (0f, -1f),
            };
            Views[view] = data;
            return data;
        }

        // ---- A pose: the upper body leant ahead over the tail (pixels at the crown), the whole risen (pixels),
        // squashed (below 0) or stretched; the arms swung (degrees, seen from the side: 0 hanging, 90 held straight
        // ahead, 180 straight up, below 0 drawn back), spread (degrees outward, seen from the front) and splayed apart
        // (from the side, one ahead and one back); the crown lifted off the head (pixels); lightning in the hands, a
        // spear of it in the first hand, a ball of it between them, over the crown (0 to 1); how brightly the eyes
        // burn; how thin it has gone (0 whole, 1 gone), how much of it has gathered out of the mist from the crown
        // down (1 all), the tail's sway, a shake, and how far the crown has fallen to the floor (0 to 1). ----

        struct Pose
        {
            public float Lean, Rise, Squash, Swing, Spread, Splay, Crown, Spark, Spear, Orb, CrownSpark, Glow, Fade, Form, Writhe, Shake, Fallen;
        }

        static Pose P(float lean = 0, float rise = 0, float squash = 0, float swing = 0, float spread = 0, float splay = 0, float crown = 0, float spark = 0,
            float spear = 0, float orb = 0, float crownSpark = 0, float glow = 0.3f, float fade = 0, float form = 1, float writhe = 1, float shake = 0, float fallen = 0) =>
            new Pose
            {
                Lean = lean, Rise = rise, Squash = squash, Swing = swing, Spread = spread, Splay = splay, Crown = crown, Spark = spark, Spear = spear, Orb = orb,
                CrownSpark = crownSpark, Glow = glow, Fade = fade, Form = form, Writhe = writhe, Shake = shake, Fallen = fallen,
            };

        static readonly Pose Rest = P();

        static Pose Lerp(Pose a, Pose b, float u)
        {
            float L(float x, float y) => x + (y - x) * u;
            return P(L(a.Lean, b.Lean), L(a.Rise, b.Rise), L(a.Squash, b.Squash), L(a.Swing, b.Swing), L(a.Spread, b.Spread), L(a.Splay, b.Splay), L(a.Crown, b.Crown),
                L(a.Spark, b.Spark), L(a.Spear, b.Spear), L(a.Orb, b.Orb), L(a.CrownSpark, b.CrownSpark), L(a.Glow, b.Glow), L(a.Fade, b.Fade), L(a.Form, b.Form),
                L(a.Writhe, b.Writhe), L(a.Shake, b.Shake), L(a.Fallen, b.Fallen));
        }

        // Key poses at times 0..1, eased in and out between each pair.
        static Pose Keys(float t, params (float At, Pose Pose)[] keys)
        {
            for (int i = 1; i < keys.Length; i++)
            {
                if (t > keys[i].At && i < keys.Length - 1) continue;
                float u = System.Math.Max(0, System.Math.Min(1, (t - keys[i - 1].At) / System.Math.Max(0.001f, keys[i].At - keys[i - 1].At)));
                return Lerp(keys[i - 1].Pose, keys[i].Pose, u * u * (3 - 2 * u));
            }
            return keys[keys.Length - 1].Pose;
        }

        // A blow's frames come in stages, each frame shown for its stage's time: the lunge (too fast to see), the
        // strike, the rebound, held (the opening to hit back), pulled back, back to rest; and the slow parts of the
        // entrance and the fall.
        enum Stage : byte
        {
            Swing,
            Strike,
            Rebound,
            Held,
            Pull,
            Recover,
            Slow,
        }

        static float StageSeconds(Stage stage) => stage switch
        {
            Stage.Swing => 0.03f,
            Stage.Strike => 0.1f,
            Stage.Rebound => 0.08f,
            Stage.Held => 0.12f,
            Stage.Pull => 0.08f,
            Stage.Recover => 0.06f,
            _ => 0.085f,
        };

        // A blow laid out from key poses: each stage moves from the pose before to its own over so many frames.
        static (Pose[] Frames, Stage[] Stages) Expand(Pose from, params (Stage Stage, int Frames, Pose To)[] stages)
        {
            var frames = new List<Pose>();
            var kinds = new List<Stage>();
            var last = from;
            foreach (var (stage, count, to) in stages)
            {
                for (int i = 1; i <= count; i++)
                {
                    float u = i / (float)count;
                    float eased = stage == Stage.Swing ? u * u : u * u * (3 - 2 * u);
                    frames.Add(Lerp(last, to, eased));
                    kinds.Add(stage);
                }
                last = to;
            }
            return (frames.ToArray(), kinds.ToArray());
        }

        // ---- The wind-ups' last poses, and the blows out of them. ----

        // The clawed hand drawn up and back, crackling, the body leant back over its tail.
        static readonly Pose Clutched = P(-4, 2, 0.02f, -115, 25, 10, spark: 0.8f, glow: 0.6f, writhe: 1.5f);
        // Both arms raised to the dark, risen, the crown sparking.
        static readonly Pose Beckoned = P(-1, 6, 0.05f, 165, 150, 0, 2, spark: 0.9f, crownSpark: 0.8f, glow: 0.9f, writhe: 2);
        // The arm drawn back with a spear of lightning forming in the hand.
        static readonly Pose Drawn = P(-5, 1, 0, -125, 40, 0, spark: 0.6f, spear: 1, glow: 0.8f, writhe: 1.5f);
        // Hunched low and leant far over, the arms swept back, going thin as a ghost.
        static readonly Pose Hunched = P(7, -2, -0.08f, -45, 20, 0, glow: 1, fade: 0.3f, writhe: 3);
        // The crown lifting off its head blazing, the arms opening.
        static readonly Pose Lifted = P(-1, 3, 0.03f, 35, 60, 30, 8, spark: 0.3f, crownSpark: 1, glow: 0.8f, writhe: 1.5f);
        // Risen as high as it goes, both arms up, the crown up and storming, shaking.
        static readonly Pose Stormed = P(-2, 11, 0.07f, 172, 165, 0, 4, spark: 1, crownSpark: 1, glow: 1, writhe: 3, shake: 0.6f);
        // Towering with a ball of lightning held overhead.
        static readonly Pose Loomed = P(-3, 9, 0.08f, 168, 160, 0, 1, spark: 0.8f, orb: 1, crownSpark: 0.6f, glow: 1, writhe: 2);
        // The fists pulled in to the chest, hunched a little.
        static readonly Pose Clenched = P(-1, 0, -0.05f, 60, -15, -10, spark: 0.5f, glow: 0.6f);
        static readonly Pose RoarTop = P(-2, 6, 0.06f, 120, 120, 30, 3, spark: 0.8f, crownSpark: 1, glow: 1, writhe: 2.5f, shake: 1);

        // Out of the clutch: the claws raked down through the wedge ahead, the body following, low.
        static readonly (Pose[] Frames, Stage[] Stages) RakeBlow = Expand(Clutched,
            (Stage.Swing, 3, P(6, 0, 0, 20, 30, 5, spark: 1, glow: 0.8f)),
            (Stage.Strike, 1, P(10, -2, -0.04f, 75, 20, 0, spark: 1, glow: 0.9f)),
            (Stage.Rebound, 1, P(9, -2, -0.03f, 70, 18, 0, spark: 0.6f, glow: 0.7f)),
            (Stage.Held, 3, P(9, -2, -0.03f, 68, 16, 0, spark: 0.3f, glow: 0.5f)),
            (Stage.Pull, 2, P(4, 0, 0, 30, 8)),
            (Stage.Recover, 6, Rest));

        // The arms flung down and ahead as the bolts fall, the crown blazing.
        static readonly (Pose[] Frames, Stage[] Stages) CallBlow = Expand(Beckoned,
            (Stage.Swing, 3, P(2, 5, 0.03f, 110, 110, 0, 2, spark: 1, crownSpark: 1, glow: 1)),
            (Stage.Swing, 1, P(4, 3, 0, 82, 82, 0, 1.5f, spark: 1, crownSpark: 1, glow: 1)),
            (Stage.Strike, 1, P(5, 2, -0.03f, 60, 60, 0, 1, spark: 1, crownSpark: 1, glow: 1)),
            (Stage.Rebound, 3, P(4, 2, -0.02f, 55, 55, 0, 1, spark: 0.6f, crownSpark: 0.6f, glow: 0.8f)),
            (Stage.Held, 2, P(3, 2, 0, 50, 50, 0, 0.5f, spark: 0.3f, glow: 0.6f)),
            (Stage.Recover, 8, Rest));

        // The spear thrown: the arm whipped through ahead, the body thrown after it.
        static readonly (Pose[] Frames, Stage[] Stages) HurlBlow = Expand(Drawn,
            (Stage.Swing, 4, P(2, 1, 0, -10, 30, 0, spark: 0.8f, spear: 1, glow: 1)),
            (Stage.Swing, 1, P(6, 1, -0.01f, 45, 22, 0, spark: 0.9f, spear: 0.5f, glow: 1)),
            (Stage.Strike, 1, P(9, 0, -0.03f, 95, 15, 0, spark: 1, glow: 1)),
            (Stage.Rebound, 2, P(8, 0, -0.02f, 90, 12, 0, spark: 0.5f, glow: 0.8f)),
            (Stage.Held, 2, P(7, 0, 0, 85, 10, 0, spark: 0.2f, glow: 0.6f)),
            (Stage.Recover, 8, Rest));

        // The rush: thrown far ahead, thin as a ghost, the tail streaming; then whole again.
        static readonly (Pose[] Frames, Stage[] Stages) RushBlow = Expand(Hunched,
            (Stage.Swing, 2, P(11, 0, -0.06f, -60, 25, 0, glow: 1, fade: 0.45f, writhe: 4)),
            (Stage.Strike, 1, P(13, 1, -0.05f, -65, 25, 0, glow: 1, fade: 0.5f, writhe: 4.5f)),
            (Stage.Rebound, 2, P(10, 0, -0.04f, -30, 20, 0, glow: 0.9f, fade: 0.3f, writhe: 3)),
            (Stage.Held, 3, P(8, -1, -0.04f, -10, 15, 0, glow: 0.7f, fade: 0.15f, writhe: 2)),
            (Stage.Pull, 2, P(4, 0, 0, 0, 5, glow: 0.5f)),
            (Stage.Recover, 8, Rest));

        // The ring let go: the crown flaring at the top of its rise, the arms flung wide; then it settles back.
        static readonly (Pose[] Frames, Stage[] Stages) FlareBlow = Expand(Lifted,
            (Stage.Swing, 2, P(0, 4, 0.04f, 45, 90, 40, 10, spark: 0.6f, crownSpark: 1, glow: 1)),
            (Stage.Strike, 1, P(0, 4, 0.05f, 50, 115, 50, 11, spark: 1, crownSpark: 1, glow: 1, shake: 0.4f)),
            (Stage.Rebound, 3, P(0, 4, 0.04f, 45, 105, 45, 9, spark: 0.6f, crownSpark: 0.8f, glow: 0.9f)),
            (Stage.Held, 2, P(0, 3, 0.02f, 40, 95, 40, 6, spark: 0.3f, crownSpark: 0.5f, glow: 0.7f)),
            (Stage.Recover, 10, Rest));

        // The arms swept down and out at everyone at once, the crown blazing, the whole body shaking with it.
        static readonly (Pose[] Frames, Stage[] Stages) BrandBlow = Expand(Stormed,
            (Stage.Swing, 2, P(1, 9, 0.04f, 120, 120, 20, 3, spark: 1, crownSpark: 1, glow: 1, writhe: 3)),
            (Stage.Strike, 1, P(2, 6, -0.02f, 80, 85, 35, 2, spark: 1, crownSpark: 1, glow: 1, writhe: 3, shake: 0.8f)),
            (Stage.Rebound, 3, P(2, 5, -0.01f, 75, 80, 30, 2, spark: 0.7f, crownSpark: 0.8f, glow: 1, writhe: 2.5f)),
            (Stage.Held, 4, P(1, 4, 0, 70, 75, 25, 1, spark: 0.4f, crownSpark: 0.5f, glow: 0.8f, writhe: 2)),
            (Stage.Recover, 10, Rest));

        // The ball brought down onto the shared mark: the body dropping with it, low, held there.
        static readonly (Pose[] Frames, Stage[] Stages) SmiteBlow = Expand(Loomed,
            (Stage.Swing, 3, P(3, 5, 0.03f, 120, 110, 0, 1, spark: 1, orb: 1, crownSpark: 0.8f, glow: 1)),
            (Stage.Swing, 3, P(6, 0, -0.04f, 65, 55, 0, 0.5f, spark: 1, orb: 0.5f, crownSpark: 1, glow: 1)),
            (Stage.Strike, 1, P(7, -3, -0.09f, 45, 35, 0, 0, spark: 1, crownSpark: 1, glow: 1)),
            (Stage.Rebound, 1, P(6, -2, -0.07f, 45, 35, 0, spark: 0.6f, glow: 0.8f)),
            (Stage.Held, 3, P(6, -2, -0.06f, 45, 35, 0, spark: 0.3f, glow: 0.6f)),
            (Stage.Pull, 2, P(3, 0, -0.02f, 20, 15)),
            (Stage.Recover, 8, Rest));

        // The fists flung open: a pulse out of its chest.
        static readonly (Pose[] Frames, Stage[] Stages) PulseBlow = Expand(Clenched,
            (Stage.Swing, 3, P(0, 1, 0.02f, 40, 60, 30, spark: 0.8f, glow: 0.8f)),
            (Stage.Strike, 1, P(0, 2, 0.05f, 35, 95, 45, spark: 1, glow: 1)),
            (Stage.Rebound, 2, P(0, 2, 0.04f, 30, 85, 40, spark: 0.5f, glow: 0.8f)),
            (Stage.Held, 2, P(0, 1, 0.02f, 25, 70, 30, spark: 0.2f, glow: 0.6f)),
            (Stage.Recover, 8, Rest));

        static (Pose[] Frames, Stage[] Stages)? BlowOf(CrownWraithPose pose) => pose switch
        {
            CrownWraithPose.Rake => RakeBlow,
            CrownWraithPose.Call => CallBlow,
            CrownWraithPose.Hurl => HurlBlow,
            CrownWraithPose.Rush => RushBlow,
            CrownWraithPose.Flare => FlareBlow,
            CrownWraithPose.Brand => BrandBlow,
            CrownWraithPose.Smite => SmiteBlow,
            CrownWraithPose.Pulse => PulseBlow,
            CrownWraithPose.Enter => (EnterFrames, EnterStages),
            CrownWraithPose.Die => Fall,
            _ => null,
        };

        // ---- The entrance: only the crown lying on the floor in a little mist; it stirs, rises to where a head would
        // be, lightning strikes it, the body gathers down from it out of the mist, the eyes light, a roar. ----

        static readonly Pose Lying = P(glow: 0, form: 0, writhe: 0, fallen: 1);
        static readonly (Pose[] Frames, Stage[] Stages) Entrance = Expand(Lying,
            (Stage.Slow, 8, P(glow: 0, form: 0, crownSpark: 0.2f, writhe: 0, shake: 0.6f, fallen: 1)),
            (Stage.Slow, 6, P(glow: 0, form: 0, crownSpark: 0.5f, writhe: 0, fallen: 0.45f)),
            (Stage.Slow, 6, P(rise: 2, glow: 0, form: 0.04f, crownSpark: 0.7f, writhe: 0.5f, fallen: 0)),
            (Stage.Strike, 1, P(rise: 2, glow: 0.2f, form: 0.2f, crownSpark: 1, writhe: 1)),
            (Stage.Slow, 10, P(rise: 2, glow: 0.4f, form: 1, crownSpark: 0.5f, writhe: 2)),
            (Stage.Slow, 4, P(rise: 2, glow: 1, crownSpark: 0.4f, writhe: 1.5f)),
            (Stage.Slow, 13, RoarTop),
            (Stage.Slow, 8, P(-2, 6, 0.06f, 125, 125, 30, 3, spark: 1, crownSpark: 1, glow: 1, writhe: 2.5f, shake: 1)),
            (Stage.Slow, 13, Rest));

        static readonly Pose[] EnterFrames = Prepend(Lying, Entrance.Frames);
        static readonly Stage[] EnterStages = Prepend(Stage.Slow, Entrance.Stages);

        static T[] Prepend<T>(T first, T[] rest)
        {
            var all = new T[rest.Length + 1];
            all[0] = first;
            System.Array.Copy(rest, 0, all, 1, rest.Length);
            return all;
        }

        // The frames of the entrance: the crown lifting off the floor, the lightning striking it, the roar.
        public static int EnterLift => 1 + 8;
        public static int EnterStrike => 1 + System.Array.IndexOf(Entrance.Stages, Stage.Strike);
        public static int EnterEyes => EnterStrike + 1 + 10;
        public static int EnterRoar => EnterEyes + 4 + 13;
        public static int RoarRise => 18;

        // The frame of the entrance a share of the way through it (a return visit plays only from the strike on).
        public static int EnterFrameAt(float progress, bool again)
        {
            int from = again ? EnterStrike - 1 : 0;
            int to = EnterFrames.Length - 1;
            return System.Math.Max(from, System.Math.Min(to, from + (int)(progress * (to - from + 1))));
        }

        // ---- The fall. ----

        static readonly (Pose[] Frames, Stage[] Stages) Fall = Expand(Rest,
            (Stage.Strike, 1, P(-6, 4, 0.04f, -30, 40, 20, 1, crownSpark: 1, glow: 1, writhe: 3)),
            (Stage.Slow, 3, P(-7, 5, 0.04f, -40, 50, 25, 1, crownSpark: 0.6f, glow: 1, writhe: 3, shake: 0.6f)),
            (Stage.Slow, 5, P(-4, 3, 0, -10, 30, 10, glow: 0.2f, writhe: 2, shake: 0.4f)),
            (Stage.Slow, 5, P(2, -3, -0.08f, 20, 10, 0, glow: 0.1f, writhe: 1)),
            (Stage.Slow, 10, P(3, -4, -0.1f, 25, 10, 0, glow: 0, fade: 0.2f, form: 0, writhe: 1)),
            (Stage.Slow, 6, P(glow: 0, form: 0, writhe: 0, fallen: 1)));

        // The frame of the fall where the crown hits the floor.
        public static int FallCrownDown => Fall.Frames.Length - 1;

        static Stage[] StagesOf(CrownWraithPose pose) => BlowOf(pose)?.Stages;

        public static int FramesOf(CrownWraithPose pose) => pose switch
        {
            CrownWraithPose.Idle => 8,
            CrownWraithPose.Walk => 8,
            CrownWraithPose.Clutch => 10,
            CrownWraithPose.Beckon => 22,
            CrownWraithPose.Draw => 12,
            CrownWraithPose.Hunch => 10,
            CrownWraithPose.Lift => 14,
            CrownWraithPose.Storm => 34,
            CrownWraithPose.Loom => 30,
            CrownWraithPose.Clench => 8,
            CrownWraithPose.Roar => RoarRise + 4,
            _ => BlowOf(pose)?.Frames.Length ?? 4,
        };

        // How long a blow's frame is shown (seconds); 0 for anything else.
        public static float FrameSeconds(CrownWraithPose pose, int frame)
        {
            var stages = StagesOf(pose);
            return stages == null ? 0 : StageSeconds(stages[System.Math.Max(0, System.Math.Min(stages.Length - 1, frame))]);
        }

        public static bool Swinging(CrownWraithPose pose, int frame)
        {
            var stages = StagesOf(pose);
            return stages != null && frame < stages.Length && (stages[frame] == Stage.Swing || stages[frame] == Stage.Strike);
        }

        public static int StrikeFrame(CrownWraithPose pose) => System.Array.IndexOf(StagesOf(pose) ?? new Stage[0], Stage.Strike);

        // The wind-up that comes before each blow.
        public static CrownWraithPose WindUpOf(CrownWraithPose blow) => blow switch
        {
            CrownWraithPose.Rake => CrownWraithPose.Clutch,
            CrownWraithPose.Call => CrownWraithPose.Beckon,
            CrownWraithPose.Hurl => CrownWraithPose.Draw,
            CrownWraithPose.Rush => CrownWraithPose.Hunch,
            CrownWraithPose.Flare => CrownWraithPose.Lift,
            CrownWraithPose.Brand => CrownWraithPose.Storm,
            CrownWraithPose.Smite => CrownWraithPose.Loom,
            CrownWraithPose.Pulse => CrownWraithPose.Clench,
            _ => CrownWraithPose.Clutch,
        };

        static Pose PoseOf(CrownWraithPose pose, int frame)
        {
            int count = FramesOf(pose);
            int f = ((frame % count) + count) % count;
            float t = count > 1 ? f / (float)(count - 1) : 0;
            float cycle = f / (float)count * 2 * (float)System.Math.PI;
            float s = (float)System.Math.Sin(cycle), c = (float)System.Math.Cos(cycle);
            switch (pose)
            {
                case CrownWraithPose.Idle:
                    // Bobbing on the air, the tail swaying, the hands stirring, a spark now and then.
                    return P(0.5f * s, 1.2f * s, 0.01f * c, 4 * c, 4 + 3 * s, 0, 0.4f * (1 + s), spark: f == 3 ? 0.35f : 0, glow: 0.3f + 0.1f * s, writhe: 1.4f);
                case CrownWraithPose.Walk:
                    // Drifting: leant into the way it goes, the arms trailing, the tail streaming.
                    return P(3 + 0.5f * c, 1.5f + s, -0.01f, -12 + 4 * s, 6, 5, 0.3f, glow: 0.35f, writhe: 2.6f);
                case CrownWraithPose.Clutch:
                    return Keys(t, (0, Rest), (0.45f, P(-2, 1, 0, -60, 15, 5, spark: 0.4f, glow: 0.45f)), (1, Clutched));
                case CrownWraithPose.Beckon:
                    return Keys(t, (0, Rest), (0.4f, P(-1, 3, 0.02f, 90, 80, 0, 1, spark: 0.4f, crownSpark: 0.4f, glow: 0.6f)), (1, Beckoned));
                case CrownWraithPose.Draw:
                    return Keys(t, (0, Rest), (0.45f, P(-3, 1, 0, -60, 25, 0, spark: 0.4f, spear: 0.4f, glow: 0.6f)), (1, Drawn));
                case CrownWraithPose.Hunch:
                    return Keys(t, (0, Rest), (0.45f, P(3, -1, -0.04f, -20, 10, 0, glow: 0.7f, fade: 0.1f, writhe: 2)), (1, Hunched));
                case CrownWraithPose.Lift:
                    return Keys(t, (0, Rest), (0.4f, P(0, 2, 0.01f, 15, 30, 15, 4, crownSpark: 0.6f, glow: 0.6f)), (1, Lifted));
                case CrownWraithPose.Storm:
                {
                    // Up and up, the crown storming: the shake grows to the end.
                    var storm = Stormed;
                    storm.Shake = 0.3f + 0.5f * t;
                    return Keys(t, (0, Rest), (0.35f, P(-1, 6, 0.03f, 110, 100, 0, 2, spark: 0.6f, crownSpark: 0.7f, glow: 0.8f, writhe: 2)), (1, storm));
                }
                case CrownWraithPose.Loom:
                    return Keys(t, (0, Rest), (0.4f, P(-2, 5, 0.04f, 110, 100, 0, 1, spark: 0.5f, orb: 0.4f, crownSpark: 0.3f, glow: 0.8f)), (1, Loomed));
                case CrownWraithPose.Clench:
                    return Keys(t, (0, Rest), (0.5f, P(-1, 0, -0.02f, 35, -5, -5, spark: 0.3f, glow: 0.5f)), (1, Clenched));
                case CrownWraithPose.Roar:
                    if (f < RoarRise) return Keys(f / (float)RoarRise, (0, Rest), (0.5f, P(-1, 3, 0.03f, 70, 70, 15, 1, spark: 0.4f, crownSpark: 0.5f, glow: 0.7f)), (1, RoarTop));
                    return P(-2, 6 + s, 0.06f, 120 + 4 * c, 120 + 4 * s, 30, 3, spark: 0.8f, crownSpark: 1, glow: 1, writhe: 2.5f, shake: 1);
                default:
                {
                    var blow = BlowOf(pose);
                    return blow == null ? Rest : blow.Value.Frames[f];
                }
            }
        }

        // ---- Drawing. ----

        static int R(float v) => (int)System.Math.Round(v);

        static bool Same(Rgba a, Rgba b) => a.R == b.R && a.G == b.G && a.B == b.B;

        static readonly Rgba EyeDim = new Rgba(110, 96, 40);
        static readonly Rgba EyeLit = new Rgba(255, 226, 96);
        static readonly Rgba EyeHot = new Rgba(255, 255, 224);
        static readonly Rgba BoltCore = new Rgba(255, 252, 226);
        static readonly Rgba BoltMid = new Rgba(255, 226, 96);
        static readonly Rgba BoltEdge = new Rgba(176, 138, 40);
        // Its ghostly side: what it goes thin to, and the mist it gathers out of and comes apart into.
        static readonly Rgba Ghost = new Rgba(196, 250, 236);
        static readonly Rgba Mist = new Rgba(150, 196, 190);
        // The inside of its cape, where an arm hung over it.
        static readonly Rgba CapeBehind = new Rgba(14, 38, 40);
        static readonly Rgba CapeFold = new Rgba(24, 56, 56);
        // In the second phase its eyes burn brighter and its crown sparks now and then; in the last its eyes burn white
        // and its crown never stops sparking.
        static readonly Rgba Storm = new Rgba(232, 236, 255);

        // One frame. Faces right; WorldView flips it for the left.
        public static PixelImage Draw(BossView view, CrownWraithPose pose, int frame, int phase) => Render(view, pose, frame, phase, out _);

        // One of its false selves (R4b, Docs/Bosses.md "R4b 그림"): the same picture in the same pose, same size and
        // footing, but the crown a dull grey with rust on it and no light, the eyes faint and the body a little
        // thinner. Only the true one's crown is gold and shining; that is how to tell them apart.
        public static PixelImage DrawFalse(BossView view, CrownWraithPose pose, int frame, int phase = 1) => Render(view, pose, frame, phase, out _, true);

        // The true crown's gold (dark to light, by how light the drawn crown's pixel is) and the false one's grey.
        static readonly Rgba[] CrownGold = { new Rgba(74, 40, 6), new Rgba(168, 104, 14), new Rgba(240, 180, 36), new Rgba(255, 228, 92), new Rgba(255, 252, 204) };
        static readonly Rgba[] CrownDull = { new Rgba(26, 28, 34), new Rgba(62, 64, 72), new Rgba(96, 96, 102), new Rgba(128, 126, 128), new Rgba(150, 148, 146) };
        static readonly Rgba CrownRust = new Rgba(104, 62, 44);
        static readonly Rgba CrownShine = new Rgba(255, 214, 72);

        static Rgba Along(Rgba[] ramp, float t)
        {
            t = System.Math.Max(0, System.Math.Min(1, t)) * (ramp.Length - 1);
            int k = System.Math.Min(ramp.Length - 2, (int)t);
            return ramp[k].Mix(ramp[k + 1], t - k);
        }

        // Where the head, the first hand and the body are in a frame (picture pixels): for the preview's check that
        // nothing jumps from one frame to the next.
        public struct Joints
        {
            public float HeadX, HeadY, HandX, HandY;
            public int Top, Bottom, Left, Right;
        }

        public static Joints JointsOf(BossView view, CrownWraithPose pose, int frame)
        {
            Render(view, pose, frame, 1, out var joints);
            return joints;
        }

        static float Smooth(float u)
        {
            u = System.Math.Max(0, System.Math.Min(1, u));
            return u * u * (3 - 2 * u);
        }

        static PixelImage Render(BossView view, CrownWraithPose pose, int frame, int phase, out Joints joints, bool fake = false)
        {
            var data = ViewOf(view);
            var p = PoseOf(pose, frame);
            // A false one's crown never sparks, whatever the phase.
            if (fake)
            {
                phase = 1;
                p.CrownSpark = 0;
            }
            // From the second phase its crown sparks now and then (in the last, never stops), so the roar that brings the
            // wider blows reads as a change in it too (Docs/Bosses.md 페이즈 배치).
            if (phase >= 3) p.CrownSpark = System.Math.Max(p.CrownSpark, 0.35f * p.Form);
            else if (phase == 2 && frame % 3 == 0) p.CrownSpark = System.Math.Max(p.CrownSpark, 0.2f * p.Form);
            var image = data.Image;
            var canvas = new PixelImage(Width, Height);
            joints = new Joints { Top = int.MaxValue, Left = int.MaxValue, Bottom = -1, Right = -1 };
            float ax = data.AheadX, ay = data.AheadY * 0.55f;
            int shake = p.Shake > 0 ? ((frame & 1) == 0 ? 1 : -1) * R(p.Shake) : 0;
            float stretch = 1 + p.Squash;
            int ground = Headroom + Ground;
            float lift = Hover + p.Rise;
            // How much of the lean a row takes: all at the crown, none where the tail leaves the body; the tail trails
            // the other way and sways, more toward its tip.
            float Weight(float y) => y <= data.Base ? Smooth((data.Base - y) / System.Math.Max(1, data.Base - data.Top)) : -0.4f * (y - data.Base) / System.Math.Max(1, data.Bottom - data.Base);
            float Sway(float y) => y <= data.Base ? 0 : p.Writhe * (y - data.Base) / System.Math.Max(1, data.Bottom - data.Base) * (float)System.Math.Sin(y * 0.5f + frame * 0.9f);
            // Where a pixel of the picture lands on the canvas (before the shake).
            (float X, float Y) Place(float x, float y)
            {
                float w = Weight(y);
                float px = x + p.Lean * ax * w + Sway(y) + Margin;
                float py = y + p.Lean * ay * w - lift + Headroom;
                return (px, ground - (ground - py) * stretch);
            }
            // The gathered body: rows below the line have not come out of the mist yet.
            float formed = data.Top + (data.Bottom - data.Top + 3) * p.Form;
            var mask = new bool[Width, Height];
            void Put(float x, float y, Rgba c, int sx, int sy, bool crown = false)
            {
                int px = R(x) + shake, py = R(y);
                if (!canvas.Inside(px, py)) return;
                if (!crown && sy > formed)
                {
                    // Not gathered yet: a thin mist where it will be.
                    float near = 1 - (sy - formed) / 10f;
                    if (near > 0 && PixelNoise.Value(sx * 3 + frame, sy, 91) < 0.35f * near) canvas[px, py] = Mist.WithAlpha(120 + R(80 * near));
                    return;
                }
                canvas[px, py] = c;
                mask[px, py] = true;
            }

            // The tail, the body and the head, then the crown, then the arms over them, then the eyes.
            foreach (var part in new[] { Part.Tail, Part.Body, Part.Head })
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        if (data.Parts[x, y] != part) continue;
                        var (px, py) = Place(x, y);
                        Put(px, py, image[x, y], x, y);
                        // A stretched row leaves no gap under it.
                        if (stretch > 1.001f) Put(px, py + 1, image[x, y], x, y);
                    }
            var (hx, hy) = Place(data.HeadX, data.HeadY);
            // The crown: lifted off the head, or fallen to the floor (lying where the body stood, a little tilted).
            float fallen = p.Fallen;
            int crownTop = int.MaxValue, crownLeft = int.MaxValue, crownRight = -1;
            float gilt = System.Math.Max(0, System.Math.Min(1, p.Form)) * (1 - System.Math.Max(0, System.Math.Min(1, fallen)));
            var crownPixels = new List<(int X, int Y)>();
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    if (data.Parts[x, y] != Part.Crown) continue;
                    var (px, py) = Place(x, y);
                    py -= p.Crown;
                    if (fallen > 0)
                    {
                        float floorX = x + Margin + (x - data.MiddleX) * 0.1f, floorY = ground - 1 - (data.CrownBottom - y) * 0.75f + (x - data.MiddleX) * 0.12f;
                        px += (floorX - px) * fallen;
                        py += (floorY - py) * fallen;
                    }
                    // The true crown is gold once the body has gathered under it (rusted while it lies on the floor);
                    // a false one's is grey with rust.
                    var metal = image[x, y];
                    float light = (metal.R * 0.4f + metal.G * 0.45f + metal.B * 0.15f) / 150f;
                    if (fake) metal = PixelNoise.Hash(x, y, 104) % 5 == 0 && light > 0.3f ? CrownRust : Along(CrownDull, light);
                    else metal = metal.Mix(Along(CrownGold, light * 1.08f), gilt);
                    Put(px, py, metal, x, y, true);
                    crownPixels.Add((R(px) + shake, R(py)));
                    crownTop = System.Math.Min(crownTop, R(py));
                    crownLeft = System.Math.Min(crownLeft, R(px));
                    crownRight = System.Math.Max(crownRight, R(px));
                }

            // Where an arm hung across the body, the cape behind it shows once the arm moves (drawn first; the arm at
            // rest covers it again).
            for (int y = 0; y < 64; y++)
            {
                int left = 64, right = -1;
                for (int x = 0; x < 64; x++)
                {
                    var part = data.Parts[x, y];
                    if (part != Part.Body && part != Part.Tail && part != Part.Head) continue;
                    left = System.Math.Min(left, x);
                    right = System.Math.Max(right, x);
                }
                for (int x = left + 1; x < right; x++)
                {
                    var part = data.Parts[x, y];
                    if (part != Part.Arm1 && part != Part.Arm2) continue;
                    var (px, py) = Place(x, y);
                    var behind = ((x + y) & 3) == 0 ? CapeFold : CapeBehind;
                    Put(px, py, behind, x, y);
                    if (stretch > 1.001f) Put(px, py + 1, behind, x, y);
                }
            }

            // The arms, each turned about its shoulder: every canvas pixel round the shoulder takes the arm's own pixel
            // turned back to it, so a turned arm keeps no holes.
            var hands = new (float X, float Y)[2];
            for (int k = 0; k < 2; k++)
            {
                var arm = k == 0 ? Part.Arm1 : Part.Arm2;
                float angle;
                if (data.Facing) angle = data.Out[k] * p.Spread;
                else if (view == BossView.Side) angle = p.Swing + (k == 0 ? 1 : -1) * p.Splay;
                else angle = 0.75f * p.Swing + 0.25f * data.Out[k] * p.Spread + (k == 0 ? 0.5f : -0.5f) * p.Splay;
                // A pose's angles are the arm's own from hanging straight down; the picture's arm hangs at its own, so a
                // small angle moves it from where it is and a large one takes it to that angle whatever it was.
                angle -= data.RestAngle[k] * System.Math.Min(1, System.Math.Abs(angle) / 90);
                float a = angle * (float)System.Math.PI / 180, ca = (float)System.Math.Cos(a), sa = (float)System.Math.Sin(a);
                float ox = data.PivotX[k], oy = data.PivotY[k];
                var (sx0, sy0) = Place(ox, oy);
                int reach = R(data.Reach[k]) + 2;
                for (int dy = -reach; dy <= reach; dy++)
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        // Turned back by the angle to the arm's own picture.
                        int ux = R(ox + dx * ca - dy * sa), uy = R(oy + dx * sa + dy * ca);
                        if (ux < 0 || uy < 0 || ux >= 64 || uy >= 64 || data.Parts[ux, uy] != arm) continue;
                        Put(sx0 + dx, sy0 + dy, image[ux, uy], ux, uy);
                    }
                float hdx = data.HandX[k] - ox, hdy = data.HandY[k] - oy;
                hands[k] = (sx0 + hdx * ca + hdy * sa, sy0 - hdx * sa + hdy * ca);
            }
            // A view that shows one arm only: the other hand is where the one is.
            if (data.Reach[1] <= 0) hands[1] = hands[0];

            float glow = System.Math.Max(0, System.Math.Min(1, p.Glow + (phase >= 3 ? 0.3f : phase == 2 ? 0.15f : 0)));
            if (fake) glow *= 0.25f;
            var eye = fake ? EyeDim.Mix(Mist, 0.35f + 0.3f * glow) : phase >= 3 ? EyeHot.Mix(Storm, 0.5f) : glow < 0.5f ? EyeDim.Mix(EyeLit, glow * 2) : EyeLit.Mix(EyeHot, (glow - 0.5f) * 2);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    if (data.Parts[x, y] != Part.Eye) continue;
                    var (px, py) = Place(x, y);
                    Put(px, py, p.Form < 0.3f ? image[x, y] : eye, x, y);
                    // A burning eye lights the bone next to it.
                    if (glow > 0.75f && p.Form >= 1)
                        foreach (var (ex, ey) in new[] { (1, 0), (-1, 0) })
                        {
                            int qx = R(px) + ex + shake, qy = R(py) + ey;
                            if (canvas.Inside(qx, qy) && canvas[qx, qy].A == 255 && !Same(canvas[qx, qy], eye)) canvas[qx, qy] = canvas[qx, qy].Mix(eye, 0.4f);
                        }
                }

            // Where an arm turned away from the body or the lean drew the head off it, a narrow gap may open: within
            // where the body stood at rest, a see-through slit with body both sides is closed with the body's own pixels.
            if (p.Form >= 1)
            {
                var rest = new bool[Width, Height];
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        var part = data.Parts[x, y];
                        if (part == Part.None || part == Part.Arm1 || part == Part.Arm2 || part == Part.Crown) continue;
                        var (px, py) = Place(x, y);
                        int qx = R(px) + shake, qy = R(py);
                        for (int oy = -1; oy <= 1; oy++)
                            if (canvas.Inside(qx, qy + oy)) rest[qx, qy + oy] = true;
                    }
                canvas.CloseGaps(rest, new Rgba(16, 26, 30), 3);
            }

            // Thin as a ghost: paler and see-through.
            if (p.Fade > 0.01f)
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                    {
                        var c = canvas[x, y];
                        if (c.A == 0) continue;
                        canvas[x, y] = c.Mix(Ghost, p.Fade * 0.65f).WithAlpha(R(c.A * (1 - p.Fade * 0.8f)));
                    }

            // A false one: paler and a little see-through all over, but for its crown, which stays plain to see.
            if (fake)
            {
                var keep = new HashSet<(int, int)>(crownPixels);
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                    {
                        var c = canvas[x, y];
                        if (c.A == 0 || keep.Contains((x, y))) continue;
                        canvas[x, y] = c.Mix(Mist, 0.16f).WithAlpha(R(c.A * 0.88f));
                    }
            }
            // The true crown shines: gold light on the empty pixels round it, and a glint that walks along its top.
            else if (gilt > 0.6f && p.Fade < 0.5f)
            {
                foreach (var (cx, cy) in crownPixels)
                    foreach (var (ox, oy) in new[] { (1, 0), (-1, 0), (0, -1), (0, 1), (0, -2) })
                    {
                        int qx = cx + ox, qy = cy + oy;
                        if (canvas.Inside(qx, qy) && canvas[qx, qy].A == 0) canvas[qx, qy] = CrownShine.WithAlpha(oy == -2 ? 70 : 130);
                    }
                if (crownRight > crownLeft)
                {
                    int gx = crownLeft + 1 + (frame * 3 % System.Math.Max(1, crownRight - crownLeft - 1)), gy = int.MaxValue;
                    foreach (var (cx, cy) in crownPixels)
                        if (cx == gx + shake) gy = System.Math.Min(gy, cy);
                    if (gy < int.MaxValue)
                        foreach (var (ox, oy) in new[] { (0, 0), (0, -1), (1, 0), (-1, 0), (0, 1) })
                            if (canvas.Inside(gx + shake + ox, gy + oy)) canvas[gx + shake + ox, gy + oy] = ox == 0 && oy == 0 ? EyeHot : BoltCore.WithAlpha(235);
                }
            }

            // Lightning: crackling in the hands, a spear of it in the first, a ball between them, over the crown.
            for (int k = 0; k < 2; k++)
            {
                if (p.Spark <= 0.05f || p.Form < 1 || data.Reach[k] <= 0) continue;
                int arcs = R(1 + 3 * p.Spark);
                for (int i = 0; i < arcs; i++)
                    Zigzag(canvas, hands[k].X + shake, hands[k].Y, -90 + (PixelNoise.Value(i + 4 * k, frame, 92) - 0.5f) * 260, 4 + 9 * p.Spark * PixelNoise.Value(i, frame + k, 93), i * 31 + k * 7 + frame * 3, p.Spark > 0.7f);
                if (p.Spark > 0.6f) Halo(canvas, hands[k].X + shake, hands[k].Y, 2);
            }
            if (p.Spear > 0.05f && p.Form >= 1)
            {
                // Along the arm's line through the hand, both ways from it.
                float dx = hands[0].X - (data.PivotX[0] + Margin), dy = hands[0].Y - (data.PivotY[0] + Headroom - lift);
                float len = (float)System.Math.Sqrt(dx * dx + dy * dy);
                if (len > 0.5f)
                {
                    dx /= len;
                    dy /= len;
                }
                float half = 4 + 9 * p.Spear;
                for (float u = -half; u <= half; u += 1)
                {
                    int px = R(hands[0].X + shake - dx * u), py = R(hands[0].Y - dy * u);
                    if (!canvas.Inside(px, py)) continue;
                    canvas[px, py] = System.Math.Abs(u) < half - 2 ? BoltCore : BoltMid;
                    int qx = px + R(-dy), qy = py + R(dx);
                    if (canvas.Inside(qx, qy) && PixelNoise.Value(R(u * 3), frame, 94) < 0.6f) canvas[qx, qy] = BoltMid;
                }
                Halo(canvas, hands[0].X + shake - dx * half, hands[0].Y - dy * half, 1);
            }
            if (p.Orb > 0.05f && p.Form >= 1)
            {
                // Held between the hands, over the head.
                float mx = (hands[0].X + hands[1].X) / 2 + shake, my = System.Math.Min(System.Math.Min(hands[0].Y, hands[1].Y), hy - 10) - 2;
                float radius = 2 + 3.5f * p.Orb;
                for (int y = R(my - radius - 1); y <= R(my + radius + 1); y++)
                    for (int x = R(mx - radius - 1); x <= R(mx + radius + 1); x++)
                    {
                        if (!canvas.Inside(x, y)) continue;
                        float d = (float)System.Math.Sqrt((x - mx) * (x - mx) + (y - my) * (y - my));
                        if (d > radius + 0.5f) continue;
                        canvas[x, y] = d < radius * 0.45f ? BoltCore : d < radius * 0.8f ? BoltMid : BoltEdge.Mix(BoltMid, 0.4f).WithAlpha(230);
                    }
                for (int i = 0; i < 3; i++)
                    Zigzag(canvas, mx, my, (PixelNoise.Value(i, frame, 95) - 0.5f) * 360, radius + 3 + 4 * PixelNoise.Value(i, frame, 96), i * 13 + frame * 5, false);
                for (int k = 0; k < 2; k++) Line(canvas, hands[k].X + shake, hands[k].Y, mx, my, frame + k);
            }
            if (p.CrownSpark > 0.05f && crownRight >= 0)
            {
                int arcs = R(1 + 4 * p.CrownSpark);
                for (int i = 0; i < arcs; i++)
                {
                    float x = crownLeft + (crownRight - crownLeft) * PixelNoise.Value(i, frame, 97);
                    Zigzag(canvas, x, crownTop, -90 + (PixelNoise.Value(i, frame, 98) - 0.5f) * 120, 3 + 7 * p.CrownSpark * PixelNoise.Value(i, frame, 99), i * 17 + frame * 7, p.CrownSpark > 0.8f);
                }
            }

            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    if (canvas[x, y].A == 0) continue;
                    joints.Top = System.Math.Min(joints.Top, y);
                    joints.Bottom = System.Math.Max(joints.Bottom, y);
                    joints.Left = System.Math.Min(joints.Left, x);
                    joints.Right = System.Math.Max(joints.Right, x);
                }
            joints.HeadX = hx;
            joints.HeadY = hy;
            joints.HandX = hands[0].X;
            joints.HandY = hands[0].Y;
            return canvas;
        }

        // A crackle of lightning from a point: a walk of pale core pixels bending this way and that, an edge of yellow
        // beside it, a fork for the brighter.
        static void Zigzag(PixelImage canvas, float x, float y, float degrees, float length, int seed, bool fork)
        {
            float a = degrees * (float)System.Math.PI / 180;
            float cx = x, cy = y;
            int steps = System.Math.Max(2, R(length / 2));
            for (int i = 0; i < steps; i++)
            {
                float bend = (PixelNoise.Value(seed, i, 101) - 0.5f) * 2.2f;
                float da = a + bend;
                cx += (float)System.Math.Cos(da) * 2;
                cy += (float)System.Math.Sin(da) * 2;
                int px = R(cx), py = R(cy);
                if (canvas.Inside(px, py)) canvas[px, py] = i < steps - 1 ? BoltCore : BoltMid;
                int ex = px + (bend > 0 ? 1 : -1);
                if (canvas.Inside(ex, py) && canvas[ex, py].A == 0) canvas[ex, py] = BoltMid.WithAlpha(210);
                if (fork && i == steps / 2) Zigzag(canvas, cx, cy, degrees + (bend > 0 ? -50 : 50), length * 0.45f, seed + 7, false);
            }
        }

        // A thin crackling line from a hand to the ball it holds.
        static void Line(PixelImage canvas, float x0, float y0, float x1, float y1, int seed)
        {
            float dx = x1 - x0, dy = y1 - y0, len = (float)System.Math.Sqrt(dx * dx + dy * dy);
            for (float u = 0; u <= len; u += 1)
            {
                float k = u / System.Math.Max(1, len);
                int px = R(x0 + dx * k + (PixelNoise.Value(R(u), seed, 102) - 0.5f) * 2), py = R(y0 + dy * k + (PixelNoise.Value(R(u), seed, 103) - 0.5f) * 2);
                if (canvas.Inside(px, py)) canvas[px, py] = BoltMid;
            }
        }

        // The glow round a crackling hand: the pixels near it lit yellow.
        static void Halo(PixelImage canvas, float x, float y, int radius)
        {
            for (int oy = -radius; oy <= radius; oy++)
                for (int ox = -radius; ox <= radius; ox++)
                {
                    int px = R(x) + ox, py = R(y) + oy;
                    if (!canvas.Inside(px, py) || ox * ox + oy * oy > radius * radius + 1) continue;
                    var c = canvas[px, py];
                    canvas[px, py] = c.A == 0 ? BoltMid.WithAlpha(150) : c.Mix(BoltMid, 0.45f).WithAlpha(System.Math.Max((int)c.A, 200));
                }
        }
    }
}
