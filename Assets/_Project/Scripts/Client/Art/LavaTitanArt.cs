using System.Collections.Generic;

namespace Game.Client
{
    // What the lava titan is doing, which picks its frame (WorldView.LavaTitan.cs, Docs/Bosses.md 용암 거인). A hulking
    // giant of black basalt with lava burning in its seams and a molten heart in its chest. Every attack has its own
    // wind-up of the whole body: both fists heaved overhead, then brought down on the floor ahead (Heave, Smash); one arm
    // drawn back and out with its hand ablaze, then swept across (Wind, Sweep); both arms raised in a V with the heart
    // blazing, then thrust at the roof (Call, Hurl); hunched over its swelling heart, then flung open (Swell, Vent); one
    // fist raised high from a crouch, then driven into the floor (Kneel, Punch); stooped with a hand swung back low,
    // then flung forward along the floor (Stoop, Gouge); arms crossed tight before the chest, then thrown wide (Brace,
    // Flare); a foot lifted high, then stamped down (Lift, Stomp); risen tall with its arms spread and everything white
    // hot, then a pillar of fire thrown up (Ignite, Blaze); a rush with the shoulders down; a roar.
    enum LavaTitanPose
    {
        Idle,
        Walk,
        Rush,
        Heave,
        Smash,
        Wind,
        Sweep,
        Call,
        Hurl,
        Swell,
        Vent,
        Kneel,
        Punch,
        Stoop,
        Gouge,
        Brace,
        Flare,
        Lift,
        Stomp,
        Ignite,
        Blaze,
        Roar,
        // Its entrance: under the lava of its caldera with only bubbles showing, a crusted fist breaking the surface,
        // heaving itself up and out, landing with its seams cold and dark, the lava lighting through them from its heart
        // outward, and a roar.
        Enter,
    }

    // The lava titan of the caldera (2026-11-28): one picture of the local image models (Z-Image-Turbo with a pixel-art
    // LoRA, turned eight ways by Qwen-Image-Edit with the multiple-angles LoRA, Docs/ArtSources.md) cut to five views
    // with a part map; every pose is made in code. The arms turn about the shoulders in three dimensions (raised ahead,
    // out to the side, across the body) and are seen from the view's angle, nearer or further than the trunk; the upper
    // body leans about the hips; a foot lifts; the body squashes and rises. The lava in its seams cools to a dark crust
    // or burns white hot, the heart flares, the hands glow molten, flames lick up off its shoulders and head, and it can
    // stand sunk in a pool of lava. Faces right.
    static partial class LavaTitanArt
    {
        public const int Width = 112;
        public const int Height = 100;
        // Rows under its feet; WorldView stands the picture on this row (Pivot).
        public const int Floor = Height - 1 - Headroom - Ground;
        // How tall the body itself stands, for the name and marks over it.
        public const int BodyHeight = 58;

        // Room round the picture for arms flung out and raised overhead.
        const int Margin = 24;
        const int Headroom = 32;
        // The row of the picture its feet stand on.
        const int Ground = 62;
        // How much a step toward the viewer moves a point down the screen (the views are seen from above).
        const float Toward = 0.35f;

        enum Part : byte
        {
            None,
            Head,
            ArmA,
            ArmB,
            LegA,
            LegB,
            Trunk,
            Eye,
        }

        // What it knows about a view: its colours and parts, how hot each pixel of lava is, which way the body faces
        // (Yaw: 0 at the viewer, 90 to the right), the shoulders the arms turn about and how long the arms are, the
        // hips the body leans about, the heart.
        sealed class ViewData
        {
            public PixelImage Image;
            public Part[,] Parts;
            public float[,] Hot;
            public float Yaw;
            public float AX, AY, BX, BY, ALength, BLength;
            // Which way each arm hangs from its shoulder at rest (toward its farthest pixel, the hand).
            public float ADirX, ADirY, BDirX, BDirY;
            public float Hip, HipX, MiddleX, HalfWidth;
            public float CoreX, CoreY, CoreReach;
            public float HeadX, HeadY;
        }

        static readonly Dictionary<BossView, ViewData> Views = new Dictionary<BossView, ViewData>();

        static Part PartOf(char c) => c switch
        {
            'H' => Part.Head,
            'A' => Part.ArmA,
            'B' => Part.ArmB,
            'L' => Part.LegA,
            'M' => Part.LegB,
            'T' => Part.Trunk,
            'E' => Part.Eye,
            _ => Part.None,
        };

        // How much a colour is glowing lava (0: rock).
        static float HotOf(Rgba c)
        {
            if (c.R < 100 || c.R - c.B < 70 || c.G > c.R) return 0;
            float lum = 0.3f * c.R + 0.59f * c.G + 0.11f * c.B;
            return System.Math.Max(0.15f, System.Math.Min(1, (lum - 50) / 170f));
        }

        static ViewData ViewOf(BossView view)
        {
            if (Views.TryGetValue(view, out var known)) return known;
            var (rows, parts) = view switch
            {
                BossView.Front => (FrontRows, FrontParts),
                BossView.FrontSide => (FrontSideRows, FrontSideParts),
                BossView.Side => (SideRows, SideParts),
                BossView.BackSide => (BackSideRows, BackSideParts),
                _ => (BackRows, BackParts),
            };
            var data = new ViewData { Image = PixelImage.FromRows(Palette, rows), Parts = new Part[64, 64], Hot = new float[64, 64] };
            data.Yaw = view switch
            {
                BossView.Front => 0,
                BossView.FrontSide => 45,
                BossView.Side => 90,
                BossView.BackSide => 135,
                _ => 180,
            };
            int aTop = 64, bTop = 64, left = 64, right = -1, legTop = 64;
            float heads = 0, headX = 0, headY = 0;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    var part = PartOf(parts[y][x]);
                    data.Parts[x, y] = part;
                    if (part == Part.None) continue;
                    data.Hot[x, y] = HotOf(data.Image[x, y]);
                    left = System.Math.Min(left, x);
                    right = System.Math.Max(right, x);
                    if (part == Part.ArmA) aTop = System.Math.Min(aTop, y);
                    if (part == Part.ArmB) bTop = System.Math.Min(bTop, y);
                    if (part == Part.LegA || part == Part.LegB) legTop = System.Math.Min(legTop, y);
                    if (part == Part.Head || part == Part.Eye)
                    {
                        heads++;
                        headX += x;
                        headY += y;
                    }
                }
            data.HeadX = heads > 0 ? headX / heads : 32;
            data.HeadY = heads > 0 ? headY / heads : 12;
            data.MiddleX = (left + right) / 2f;
            data.HalfWidth = (right - left) / 2f + 2;
            data.Hip = legTop < 64 ? legTop : 48;
            // The shoulder an arm turns about: the middle of its top few rows, a little down into it.
            (float X, float Y, float Length, float DirX, float DirY) Shoulder(Part arm, int top)
            {
                if (top >= 64) return (data.MiddleX, 16, 0, 0, 1);
                float sum = 0, count = 0;
                for (int y = top; y < System.Math.Min(64, top + 5); y++)
                    for (int x = 0; x < 64; x++)
                        if (data.Parts[x, y] == arm)
                        {
                            sum += x;
                            count++;
                        }
                float sx = count > 0 ? sum / count : data.MiddleX, sy = top + 4;
                float longest = 0, hx = sx, hy = sy + 1;
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                        if (data.Parts[x, y] == arm && Distance(x - sx, y - sy) > longest)
                        {
                            longest = Distance(x - sx, y - sy);
                            (hx, hy) = (x, y);
                        }
                float far = System.Math.Max(0.01f, Distance(hx - sx, hy - sy));
                return (sx, sy, longest, (hx - sx) / far, (hy - sy) / far);
            }
            (data.AX, data.AY, data.ALength, data.ADirX, data.ADirY) = Shoulder(Part.ArmA, aTop);
            (data.BX, data.BY, data.BLength, data.BDirX, data.BDirY) = Shoulder(Part.ArmB, bTop);
            float hips = 0, hipCount = 0;
            for (int x = 0; x < 64; x++)
                if (data.Parts[x, (int)data.Hip] == Part.LegA || data.Parts[x, (int)data.Hip] == Part.LegB)
                {
                    hips += x;
                    hipCount++;
                }
            data.HipX = hipCount > 0 ? hips / hipCount : data.MiddleX;
            // The heart: where the trunk's lava burns brightest; how far its seams reach from it.
            float coreX = 0, coreY = 0, weight = 0;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    if (data.Parts[x, y] != Part.Trunk) continue;
                    float w = data.Hot[x, y] * data.Hot[x, y];
                    coreX += x * w;
                    coreY += y * w;
                    weight += w;
                }
            data.CoreX = weight > 0 ? coreX / weight : data.MiddleX;
            data.CoreY = weight > 0 ? coreY / weight : 28;
            float reach = 1;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    if (data.Hot[x, y] > 0) reach = System.Math.Max(reach, Distance(x - data.CoreX, y - data.CoreY));
            data.CoreReach = reach;
            Views[view] = data;
            return data;
        }

        static float Distance(float x, float y) => (float)System.Math.Sqrt(x * x + y * y);

        // ---- A pose: each arm raised ahead (Flex, degrees: 0 hanging, 90 straight ahead, 180 overhead, below 0 swung
        // back) and out to its side (Out, degrees; below 0 across the body); the upper body leaned forward (degrees),
        // squashed (below 0) or stretched, lifted off the floor (pixels); a foot raised (pixels); the head dipped
        // (degrees); how hot the lava burns (0 a dark crust, 0.5 as drawn, 1 white hot), the heart's flare, the hands'
        // molten glow, the flames off its shoulders, the eyes; shaking; sunk into lava (pixels); how far out from the
        // heart its seams are lit (1 all of them). ----

        struct Pose
        {
            public float AFlex, AOut, BFlex, BOut, Lean, Squash, Lift, LegA, LegB, Dip, Heat, Core, Fist, Flame, Glow, Shake, Sink, Kindle;
        }

        static Pose P(float aFlex = 0, float aOut = 0, float bFlex = 0, float bOut = 0, float lean = 0, float squash = 0, float lift = 0,
            float legA = 0, float legB = 0, float dip = 0, float heat = 0.5f, float core = 0.3f, float fist = 0, float flame = 0.45f,
            float glow = 0.35f, float shake = 0, float sink = 0, float kindle = 1) =>
            new Pose
            {
                AFlex = aFlex, AOut = aOut, BFlex = bFlex, BOut = bOut, Lean = lean, Squash = squash, Lift = lift, LegA = legA, LegB = legB,
                Dip = dip, Heat = heat, Core = core, Fist = fist, Flame = flame, Glow = glow, Shake = shake, Sink = sink, Kindle = kindle,
            };

        static readonly Pose Rest = P();

        static Pose Lerp(Pose a, Pose b, float u)
        {
            float L(float x, float y) => x + (y - x) * u;
            return P(L(a.AFlex, b.AFlex), L(a.AOut, b.AOut), L(a.BFlex, b.BFlex), L(a.BOut, b.BOut), L(a.Lean, b.Lean), L(a.Squash, b.Squash),
                L(a.Lift, b.Lift), L(a.LegA, b.LegA), L(a.LegB, b.LegB), L(a.Dip, b.Dip), L(a.Heat, b.Heat), L(a.Core, b.Core), L(a.Fist, b.Fist),
                L(a.Flame, b.Flame), L(a.Glow, b.Glow), L(a.Shake, b.Shake), L(a.Sink, b.Sink), L(a.Kindle, b.Kindle));
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
        // strike, the rebound, held (fists on the floor or arms thrown wide: the opening to hit back), pulled back, back
        // to rest; and the slow parts of the entrance.
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

        // Both fists heaved up overhead, leaning back, the hands and seams kindling.
        static readonly Pose Heaved = P(165, 15, 165, 15, -10, 0.04f, heat: 0.8f, core: 0.6f, fist: 0.8f, flame: 0.7f, glow: 0.9f);
        // One arm drawn back and out with its hand ablaze, the other forward.
        static readonly Pose Wound = P(-35, 55, 25, 10, -6, heat: 0.7f, core: 0.5f, fist: 1, flame: 0.7f, glow: 0.7f);
        // Both arms raised in a V, leaning back, the heart blazing, trembling.
        static readonly Pose Called = P(150, 40, 150, 40, -14, 0.05f, heat: 0.9f, core: 1, fist: 0.6f, flame: 1.1f, glow: 1, shake: 0.3f);
        // Hunched over its swelling heart, the arms hugged round it, shaking.
        static readonly Pose Swollen = P(70, -30, 70, -30, 8, -0.1f, dip: 6, heat: 1, core: 1.1f, flame: 0.8f, glow: 0.9f, shake: 0.5f);
        // Down on one knee's worth of crouch, a fist raised high.
        static readonly Pose Knelt = P(160, 10, 30, 25, 4, -0.06f, heat: 0.8f, core: 0.6f, fist: 1, flame: 0.7f, glow: 0.9f);
        // Stooped low with a hand swung back behind it.
        static readonly Pose Stooped = P(-45, 15, 15, 10, 14, -0.05f, heat: 0.7f, core: 0.5f, fist: 0.9f, glow: 0.7f);
        // The arms crossed tight before the chest, hunched.
        static readonly Pose Braced = P(80, -45, 80, -45, 6, -0.06f, dip: 6, heat: 0.9f, core: 1, flame: 0.7f, glow: 0.9f, shake: 0.3f);
        // A foot lifted high, leaning back, the arms out for balance.
        static readonly Pose Lifted = P(20, 30, 20, 30, -8, 0.03f, legA: 12, heat: 0.8f, core: 0.5f, glow: 0.8f);
        // Risen as tall as it goes, the arms spread up, everything white hot, trembling.
        static readonly Pose Ignited = P(150, 70, 150, 70, -16, 0.1f, 3, heat: 1.3f, core: 1.3f, fist: 1, flame: 1.8f, glow: 1, shake: 0.6f);
        static readonly Pose RoarTop = P(25, 75, 25, 75, -12, 0.05f, heat: 0.9f, core: 1, flame: 1.2f, glow: 1, shake: 0.6f);

        // Both fists down on the floor ahead, held there a moment.
        static readonly (Pose[] Frames, Stage[] Stages) SmashBlow = Expand(Heaved,
            (Stage.Swing, 3, P(60, 10, 60, 10, 12, heat: 0.9f, core: 0.7f, fist: 1, flame: 0.8f, glow: 1)),
            (Stage.Strike, 1, P(25, 5, 25, 5, 24, -0.08f, dip: 6, heat: 1, core: 0.8f, fist: 1.2f, flame: 0.9f, glow: 1)),
            (Stage.Rebound, 1, P(30, 5, 30, 5, 20, -0.05f, dip: 5, heat: 0.9f, core: 0.7f, fist: 1, glow: 0.9f)),
            (Stage.Held, 3, P(28, 5, 28, 5, 20, -0.04f, dip: 5, heat: 0.8f, core: 0.6f, fist: 0.8f, glow: 0.7f)),
            (Stage.Pull, 2, P(10, 3, 10, 3, 8, -0.01f, fist: 0.4f)),
            (Stage.Recover, 6, Rest));

        // The burning arm swept across ahead.
        static readonly (Pose[] Frames, Stage[] Stages) SweepBlow = Expand(Wound,
            (Stage.Swing, 2, P(60, 40, 20, 10, 6, heat: 0.8f, core: 0.6f, fist: 1, flame: 0.8f, glow: 0.8f)),
            (Stage.Strike, 1, P(95, -15, 10, 15, 12, -0.03f, heat: 0.9f, core: 0.7f, fist: 1.2f, flame: 0.9f, glow: 1)),
            (Stage.Rebound, 1, P(90, -25, 10, 15, 11, -0.02f, heat: 0.8f, core: 0.6f, fist: 1, glow: 0.8f)),
            (Stage.Held, 2, P(85, -25, 10, 12, 10, -0.02f, heat: 0.7f, fist: 0.8f, glow: 0.7f)),
            (Stage.Pull, 2, P(40, -5, 5, 5, 4, fist: 0.4f)),
            (Stage.Recover, 6, Rest));

        // Both arms thrust up at the roof, flames leaping off it.
        static readonly (Pose[] Frames, Stage[] Stages) HurlBlow = Expand(Called,
            (Stage.Swing, 2, P(175, 20, 175, 20, -18, 0.08f, 2, heat: 1, core: 1.1f, fist: 0.8f, flame: 1.3f, glow: 1)),
            (Stage.Strike, 1, P(180, 10, 180, 10, -20, 0.1f, 3, heat: 1.1f, core: 1.2f, fist: 1, flame: 1.6f, glow: 1)),
            (Stage.Rebound, 2, P(170, 20, 170, 20, -16, 0.06f, 1, heat: 1, core: 1, fist: 0.8f, flame: 1.3f, glow: 1)),
            (Stage.Held, 2, P(160, 25, 160, 25, -12, 0.04f, heat: 0.8f, core: 0.8f, fist: 0.6f, flame: 1, glow: 0.8f)),
            (Stage.Recover, 13, Rest));

        // Flung open: the arms thrown out wide as the lava bursts off it.
        static readonly (Pose[] Frames, Stage[] Stages) VentBlow = Expand(Swollen,
            (Stage.Swing, 2, P(40, 40, 40, 40, 0, 0, heat: 1.1f, core: 1.2f, flame: 1, glow: 1)),
            (Stage.Strike, 1, P(20, 95, 20, 95, -10, 0.06f, 2, heat: 1.2f, core: 1.3f, fist: 0.6f, flame: 1.3f, glow: 1)),
            (Stage.Rebound, 2, P(20, 85, 20, 85, -8, 0.04f, 1, heat: 1, core: 1.1f, flame: 1.1f, glow: 1)),
            (Stage.Held, 2, P(18, 75, 18, 75, -6, 0.02f, heat: 0.8f, core: 0.8f, flame: 0.9f, glow: 0.8f)),
            (Stage.Recover, 8, Rest));

        // The raised fist driven down into the floor ahead.
        static readonly (Pose[] Frames, Stage[] Stages) PunchBlow = Expand(Knelt,
            (Stage.Swing, 3, P(90, 8, 30, 25, 14, -0.07f, heat: 0.9f, core: 0.7f, fist: 1, flame: 0.8f, glow: 1)),
            (Stage.Strike, 1, P(15, 5, 25, 25, 25, -0.1f, dip: 7, heat: 1, core: 0.8f, fist: 1.2f, flame: 0.9f, glow: 1)),
            (Stage.Rebound, 1, P(18, 5, 25, 25, 26, -0.08f, dip: 7, heat: 0.9f, core: 0.7f, fist: 1, glow: 0.9f)),
            (Stage.Held, 3, P(18, 5, 25, 22, 26, -0.08f, dip: 7, heat: 0.8f, core: 0.6f, fist: 0.8f, glow: 0.7f)),
            (Stage.Pull, 4, P(10, 3, 12, 10, 10, -0.02f, fist: 0.4f)),
            (Stage.Recover, 8, Rest));

        // The low hand flung forward along the floor and up.
        static readonly (Pose[] Frames, Stage[] Stages) GougeBlow = Expand(Stooped,
            (Stage.Swing, 2, P(10, 10, 10, 10, 20, -0.06f, heat: 0.8f, core: 0.6f, fist: 1, glow: 0.9f)),
            (Stage.Strike, 1, P(80, 0, 0, 10, 14, -0.03f, heat: 0.9f, core: 0.7f, fist: 1.2f, flame: 0.8f, glow: 1)),
            (Stage.Rebound, 2, P(95, 0, 0, 10, 8, heat: 0.8f, core: 0.6f, fist: 1, glow: 0.8f)),
            (Stage.Held, 2, P(90, 0, 0, 8, 6, heat: 0.7f, fist: 0.8f, glow: 0.7f)),
            (Stage.Recover, 6, Rest));

        // The crossed arms thrown out wide, the heat bursting off it.
        static readonly (Pose[] Frames, Stage[] Stages) FlareBlow = Expand(Braced,
            (Stage.Swing, 3, P(40, 62, 40, 62, 2, -0.02f, heat: 1, core: 1.1f, flame: 0.9f, glow: 1)),
            (Stage.Strike, 1, P(30, 110, 30, 110, -8, 0.05f, 1, heat: 1.2f, core: 1.2f, fist: 0.6f, flame: 1.3f, glow: 1)),
            (Stage.Rebound, 2, P(28, 100, 28, 100, -6, 0.03f, heat: 1, core: 1, flame: 1.1f, glow: 1)),
            (Stage.Held, 2, P(25, 90, 25, 90, -5, 0.02f, heat: 0.8f, core: 0.8f, flame: 0.9f, glow: 0.8f)),
            (Stage.Recover, 8, Rest));

        // The foot stamped down, the body jolting.
        static readonly (Pose[] Frames, Stage[] Stages) StompBlow = Expand(Lifted,
            (Stage.Swing, 2, P(25, 35, 25, 35, 4, -0.02f, legA: 4, heat: 0.9f, core: 0.6f, glow: 0.9f)),
            (Stage.Strike, 1, P(30, 40, 30, 40, 6, -0.1f, heat: 1, core: 0.8f, flame: 0.9f, glow: 1)),
            (Stage.Rebound, 1, P(28, 38, 28, 38, 5, -0.06f, heat: 0.9f, core: 0.7f, glow: 0.9f)),
            (Stage.Held, 3, P(25, 35, 25, 35, 5, -0.05f, heat: 0.8f, core: 0.6f, glow: 0.7f)),
            (Stage.Recover, 6, Rest));

        // A pillar of fire thrown up off it: the arms thrust overhead.
        static readonly (Pose[] Frames, Stage[] Stages) BlazeBlow = Expand(Ignited,
            (Stage.Swing, 2, P(175, 25, 175, 25, -18, 0.12f, 4, heat: 1.4f, core: 1.4f, fist: 1, flame: 2, glow: 1)),
            (Stage.Strike, 1, P(180, 15, 180, 15, -20, 0.12f, 4, heat: 1.5f, core: 1.5f, fist: 1.2f, flame: 2.2f, glow: 1)),
            (Stage.Rebound, 3, P(170, 25, 170, 25, -14, 0.08f, 2, heat: 1.2f, core: 1.2f, fist: 1, flame: 1.8f, glow: 1)),
            (Stage.Held, 4, P(150, 35, 150, 35, -10, 0.05f, heat: 1, core: 1, fist: 0.8f, flame: 1.4f, glow: 0.9f)),
            (Stage.Recover, 13, Rest));

        static (Pose[] Frames, Stage[] Stages)? BlowOf(LavaTitanPose pose) => pose switch
        {
            LavaTitanPose.Smash => SmashBlow,
            LavaTitanPose.Sweep => SweepBlow,
            LavaTitanPose.Hurl => HurlBlow,
            LavaTitanPose.Vent => VentBlow,
            LavaTitanPose.Punch => PunchBlow,
            LavaTitanPose.Gouge => GougeBlow,
            LavaTitanPose.Flare => FlareBlow,
            LavaTitanPose.Stomp => StompBlow,
            LavaTitanPose.Blaze => BlazeBlow,
            LavaTitanPose.Enter => (EnterFrames, EnterStages),
            _ => null,
        };

        // ---- The entrance: under the lava with only bubbles showing; a crusted fist breaks the surface, the other
        // arm and the head follow, it heaves itself up and out and lands, its seams cold, then lit from the heart
        // outward, and roars. ----

        static readonly Pose Under = P(0, 0, 0, 0, sink: 100, heat: 0.2f, core: 0, flame: 0, glow: 0, kindle: 0);
        static readonly (Pose[] Frames, Stage[] Stages) Entrance = Expand(Under,
            (Stage.Slow, 8, P(175, 8, 0, 0, sink: 100, heat: 0.2f, core: 0, flame: 0, glow: 0, shake: 0.6f, kindle: 0)),
            (Stage.Slow, 10, P(175, 8, 40, 10, sink: 64, heat: 0.2f, core: 0, flame: 0, glow: 0, kindle: 0)),
            (Stage.Slow, 5, P(165, 10, 120, 15, sink: 58, heat: 0.2f, core: 0, flame: 0, glow: 0.2f, kindle: 0)),
            (Stage.Slow, 5, P(150, 15, 150, 15, -4, sink: 42, heat: 0.2f, core: 0.1f, flame: 0, glow: 0.5f, shake: 0.5f, kindle: 0)),
            (Stage.Swing, 7, P(110, 45, 110, 45, 4, -0.02f, 1, sink: 5, heat: 0.2f, core: 0.2f, flame: 0, glow: 0.6f, kindle: 0)),
            (Stage.Strike, 1, P(60, 70, 60, 70, -4, 0.05f, 3, heat: 0.3f, core: 0.4f, flame: 0.2f, glow: 0.8f, kindle: 0.1f)),
            (Stage.Rebound, 4, P(35, 45, 35, 45, 6, -0.08f, heat: 0.3f, core: 0.5f, flame: 0.1f, glow: 0.8f, kindle: 0.2f)),
            (Stage.Slow, 8, P(20, 30, 20, 30, 3, -0.02f, heat: 0.8f, core: 1.1f, flame: 0.7f, glow: 1, kindle: 1.05f)),
            (Stage.Slow, 6, Lerp(RoarTop, P(25, 75, 25, 75, -12, 0.05f, heat: 1, core: 1.1f, flame: 1.5f, glow: 1, shake: 1), 1)),
            (Stage.Slow, 6, P(25, 75, 25, 75, -12, 0.05f, heat: 1, core: 1.1f, flame: 1.5f, glow: 1, shake: 1)),
            (Stage.Slow, 8, Rest));

        static readonly Pose[] EnterFrames = Prepend(Under, Entrance.Frames);
        static readonly Stage[] EnterStages = Prepend(Stage.Slow, Entrance.Stages);

        static T[] Prepend<T>(T first, T[] rest)
        {
            var all = new T[rest.Length + 1];
            all[0] = first;
            System.Array.Copy(rest, 0, all, 1, rest.Length);
            return all;
        }

        // The frames of the entrance: the fist breaking the surface, the body heaving out of the lava and landing,
        // the seams lit, the roar.
        public static int EnterHand => 1 + 8 + 3;
        public static int EnterBurst => 1 + System.Array.IndexOf(Entrance.Stages, Stage.Strike);
        public static int EnterKindle => EnterBurst + 3;
        public static int EnterRoar => EnterKindle + 8;
        public static int RoarRise => 12;

        // The frame of the entrance a share of the way through it (a return visit plays only the heave on).
        public static int EnterFrameAt(float progress, bool again)
        {
            int from = again ? EnterBurst - 5 : 0;
            int to = EnterFrames.Length - 1;
            return System.Math.Max(from, System.Math.Min(to, from + (int)(progress * (to - from + 1))));
        }

        static Stage[] StagesOf(LavaTitanPose pose) => BlowOf(pose)?.Stages;

        public static int FramesOf(LavaTitanPose pose) => pose switch
        {
            LavaTitanPose.Idle => 8,
            LavaTitanPose.Walk => 8,
            LavaTitanPose.Rush => 6,
            LavaTitanPose.Heave => 12,
            LavaTitanPose.Wind => 12,
            LavaTitanPose.Call => 14,
            LavaTitanPose.Swell => 12,
            LavaTitanPose.Kneel => 12,
            LavaTitanPose.Stoop => 12,
            LavaTitanPose.Brace => 12,
            LavaTitanPose.Lift => 12,
            LavaTitanPose.Ignite => 18,
            LavaTitanPose.Roar => RoarRise + 4,
            _ => BlowOf(pose)?.Frames.Length ?? 4,
        };

        // How long a blow's frame is shown (seconds); 0 for anything else.
        public static float FrameSeconds(LavaTitanPose pose, int frame)
        {
            var stages = StagesOf(pose);
            return stages == null ? 0 : StageSeconds(stages[System.Math.Max(0, System.Math.Min(stages.Length - 1, frame))]);
        }

        public static bool Swinging(LavaTitanPose pose, int frame)
        {
            var stages = StagesOf(pose);
            return stages != null && frame < stages.Length && (stages[frame] == Stage.Swing || stages[frame] == Stage.Strike);
        }

        public static int StrikeFrame(LavaTitanPose pose) => System.Array.IndexOf(StagesOf(pose) ?? new Stage[0], Stage.Strike);

        // The wind-up that comes before each blow.
        public static LavaTitanPose WindUpOf(LavaTitanPose blow) => blow switch
        {
            LavaTitanPose.Smash => LavaTitanPose.Heave,
            LavaTitanPose.Sweep => LavaTitanPose.Wind,
            LavaTitanPose.Hurl => LavaTitanPose.Call,
            LavaTitanPose.Vent => LavaTitanPose.Swell,
            LavaTitanPose.Punch => LavaTitanPose.Kneel,
            LavaTitanPose.Gouge => LavaTitanPose.Stoop,
            LavaTitanPose.Flare => LavaTitanPose.Brace,
            LavaTitanPose.Stomp => LavaTitanPose.Lift,
            LavaTitanPose.Blaze => LavaTitanPose.Ignite,
            _ => LavaTitanPose.Heave,
        };

        static Pose PoseOf(LavaTitanPose pose, int frame)
        {
            int count = FramesOf(pose);
            int f = ((frame % count) + count) % count;
            float t = count > 1 ? f / (float)(count - 1) : 0;
            float cycle = f / (float)count * 2 * (float)System.Math.PI;
            float s = (float)System.Math.Sin(cycle), c = (float)System.Math.Cos(cycle);
            switch (pose)
            {
                case LavaTitanPose.Idle:
                    // Breathing: the chest rising, the heart and the seams pulsing, the arms stirring, the flames flickering.
                    return P(3 * s, 4, -3 * s, 4, squash: 0.015f * (1 + c) / 2, heat: 0.5f + 0.08f * s, core: 0.3f + 0.12f * c, flame: 0.5f + 0.15f * s);
                case LavaTitanPose.Walk:
                {
                    // Lumbering: a foot up in turn, the body rocking down on each step, the arms swinging.
                    float up = System.Math.Abs(s);
                    return P(14 * s, 6, -14 * s, 6, 4, -0.02f * (1 - up), 0.8f * up, legA: 3.5f * System.Math.Max(0, s), legB: 3.5f * System.Math.Max(0, -s), flame: 0.55f);
                }
                case LavaTitanPose.Rush:
                {
                    // Charging with the shoulders down and the arms back, the flames streaming.
                    float up = System.Math.Abs(s);
                    return P(-25 + 10 * s, 10, -25 - 10 * s, 10, 16, -0.03f, up, legA: 4 * System.Math.Max(0, s), legB: 4 * System.Math.Max(0, -s), dip: 6,
                        heat: 0.7f, core: 0.6f, flame: 0.9f, glow: 0.9f);
                }
                case LavaTitanPose.Heave:
                    return Keys(t, (0, Rest), (0.45f, P(90, 10, 90, 10, -2, 0.01f, heat: 0.6f, fist: 0.4f, glow: 0.6f)), (1, Heaved));
                case LavaTitanPose.Wind:
                    return Keys(t, (0, Rest), (0.45f, P(-10, 30, 15, 6, -2, heat: 0.6f, fist: 0.5f, glow: 0.5f)), (1, Wound));
                case LavaTitanPose.Call:
                    return Keys(t, (0, Rest), (0.4f, P(80, 25, 80, 25, -5, 0.02f, heat: 0.7f, core: 0.6f, flame: 0.8f, glow: 0.7f)), (1, Called));
                case LavaTitanPose.Swell:
                {
                    // The heart swelling in pulses as it hunches over it.
                    var swell = Swollen;
                    swell.Core += 0.15f * s;
                    return Keys(t, (0, Rest), (0.4f, P(40, -10, 40, -10, 4, -0.04f, dip: 3, heat: 0.7f, core: 0.7f, glow: 0.6f)), (1, swell));
                }
                case LavaTitanPose.Kneel:
                    return Keys(t, (0, Rest), (0.45f, P(80, 8, 20, 15, 2, -0.03f, heat: 0.6f, fist: 0.5f, glow: 0.6f)), (1, Knelt));
                case LavaTitanPose.Stoop:
                    return Keys(t, (0, Rest), (0.45f, P(-15, 8, 10, 6, 7, -0.02f, heat: 0.6f, fist: 0.5f, glow: 0.5f)), (1, Stooped));
                case LavaTitanPose.Brace:
                    return Keys(t, (0, Rest), (0.45f, P(50, -20, 50, -20, 3, -0.03f, dip: 3, heat: 0.7f, core: 0.7f, glow: 0.6f)), (1, Braced));
                case LavaTitanPose.Lift:
                    return Keys(t, (0, Rest), (0.45f, P(10, 18, 10, 18, -3, legA: 5, heat: 0.6f, glow: 0.6f)), (1, Lifted));
                case LavaTitanPose.Ignite:
                {
                    // Rising and growing white hot, the flames roaring higher, the hall beginning to shake.
                    var top = Ignited;
                    top.Flame += 0.2f * s;
                    return Keys(t, (0, Rest), (0.35f, P(90, 40, 90, 40, -6, 0.04f, 1, heat: 0.9f, core: 0.9f, fist: 0.5f, flame: 1, glow: 0.9f)), (1, top));
                }
                case LavaTitanPose.Roar:
                    if (f < RoarRise) return Keys(f / (float)RoarRise, (0, Rest), (0.5f, P(15, 40, 15, 40, -5, 0.02f, heat: 0.7f, core: 0.7f, flame: 0.8f, glow: 0.8f)), (1, RoarTop));
                    return P(25 + 3 * s, 75 + 3 * c, 25 - 3 * s, 75 - 3 * c, -12, 0.05f, heat: 0.9f + 0.1f * s, core: 1, flame: 1.2f + 0.2f * c, glow: 1, shake: 1);
                default:
                {
                    var blow = BlowOf(pose);
                    return blow == null ? Rest : blow.Value.Frames[f];
                }
            }
        }

        // ---- Drawing. ----

        static int R(float v) => (int)System.Math.Round(v);

        static readonly Rgba Crust = new Rgba(58, 30, 26);
        static readonly Rgba WhiteHot = new Rgba(255, 244, 196);
        static readonly Rgba Molten = new Rgba(255, 128, 32);
        static readonly Rgba Ember = new Rgba(196, 52, 20);
        static readonly Rgba FlameBase = new Rgba(232, 92, 24);
        static readonly Rgba FlameMid = new Rgba(255, 168, 48);
        static readonly Rgba FlameTip = new Rgba(255, 232, 128);
        static readonly Rgba EyeDim = new Rgba(150, 60, 24);
        static readonly Rgba EyeLit = new Rgba(255, 196, 64);
        static readonly Rgba PoolDark = new Rgba(70, 24, 16);
        static readonly Rgba PoolMain = new Rgba(208, 72, 20);
        static readonly Rgba PoolLight = new Rgba(255, 160, 48);

        // One frame. Faces right; WorldView flips it for the left.
        public static PixelImage Draw(BossView view, LavaTitanPose pose, int frame, int phase) => Render(view, pose, frame, phase, out _);

        // Where the head, the hands and the body are in a frame (picture pixels): for the preview's check that nothing
        // jumps from one frame to the next, and for where WorldView throws fire from.
        public struct Joints
        {
            public float HeadX, HeadY, HandX, HandY, CoreX, CoreY;
            public int Top, Bottom, Left, Right;
        }

        public static Joints JointsOf(BossView view, LavaTitanPose pose, int frame)
        {
            Render(view, pose, frame, 1, out var joints);
            return joints;
        }

        // A vector turned about a unit axis (Rodrigues), degrees.
        static (float X, float Y, float Z) Turn(float x, float y, float z, float kx, float ky, float kz, float degrees)
        {
            if (degrees == 0) return (x, y, z);
            float a = degrees * (float)System.Math.PI / 180, c = (float)System.Math.Cos(a), s = (float)System.Math.Sin(a);
            float dot = kx * x + ky * y + kz * z;
            float cx = ky * z - kz * y, cy = kz * x - kx * z, cz = kx * y - ky * x;
            return (x * c + cx * s + kx * dot * (1 - c), y * c + cy * s + ky * dot * (1 - c), z * c + cz * s + kz * dot * (1 - c));
        }

        static PixelImage Render(BossView view, LavaTitanPose pose, int frame, int phase, out Joints joints)
        {
            var data = ViewOf(view);
            var p = PoseOf(pose, frame);
            var image = data.Image;
            var canvas = new PixelImage(Width, Height);
            var depth = new float[Width, Height];
            var owner = new Part[Width, Height];
            int bodyTop = Height;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    depth[x, y] = float.NegativeInfinity;
            joints = new Joints { Top = int.MaxValue, Left = int.MaxValue, Bottom = -1, Right = -1 };
            float yaw = data.Yaw * (float)System.Math.PI / 180;
            // Ahead (F) and the body's right-to-left (Side) in the view: x right, y down, z toward the viewer.
            float fx = (float)System.Math.Sin(yaw), fz = (float)System.Math.Cos(yaw);
            float sx = (float)System.Math.Cos(yaw), sz = -(float)System.Math.Sin(yaw);
            int shake = p.Shake > 0 ? ((frame & 1) == 0 ? 1 : -1) * R(p.Shake) : 0;
            float stretch = 1 + p.Squash;
            float ground = Headroom + Ground;
            // After the second phase's roar it burns hotter and its flames stand taller, more again in the last phase.
            float hot = p.Heat + (phase >= 3 ? 0.25f : phase >= 2 ? 0.1f : 0);
            float flame = p.Flame + (phase >= 3 ? 0.4f : phase >= 2 ? 0.2f : 0);
            // The near arm in front of the trunk, the far one behind it; seen square on, both beside it.
            float nearness = data.Yaw <= 0.1f || data.Yaw >= 179.9f ? 0.5f : data.Yaw == 90 ? 3 : 1.5f;

            // A point of the picture (x, y, toward the viewer z) to the canvas: the upper body leaned about the hips,
            // squashed toward the floor, lifted, shaken.
            (float X, float Y, float Z) Leaned(float x, float y, float z, float extra)
            {
                if (y >= data.Hip && extra == 0) return (x, y, z);
                var (vx, vy, vz) = Turn(x - data.HipX, y - data.Hip, z, sx, 0, sz, -(p.Lean + extra));
                return (data.HipX + vx, data.Hip + vy + (vz - z) * Toward, vz);
            }
            (float X, float Y) Place(float x, float y)
            {
                float cx = data.MiddleX + (x - data.MiddleX) * (1 - p.Squash * 0.5f) + Margin + shake;
                float cy = ground - (ground - (y + Headroom)) * stretch - p.Lift + p.Sink;
                return (cx, cy);
            }
            void Put(float x, float y, float z, Rgba c, Part part)
            {
                int px = R(x), py = R(y);
                if (!canvas.Inside(px, py) || z < depth[px, py]) return;
                depth[px, py] = z;
                canvas[px, py] = c;
                owner[px, py] = part;
                if (part == Part.Head || part == Part.Trunk) bodyTop = System.Math.Min(bodyTop, py);
            }

            // The colour of a pixel of the body in this pose: lava cooled or burning by how hot it is and how far from
            // the heart the seams are lit, the heart flaring, the hands molten, the eyes.
            float glow = System.Math.Max(0, System.Math.Min(1, p.Glow + (phase >= 3 ? 0.3f : 0)));
            var eye = glow < 0.5f ? EyeDim.Mix(EyeLit, glow * 2) : EyeLit.Mix(WhiteHot, (glow - 0.5f) * 2);
            Rgba Colour(int x, int y, Part part, float reach)
            {
                var c = image[x, y];
                if (part == Part.Eye) return eye;
                float h = data.Hot[x, y];
                float fromCore = Distance(x - data.CoreX, y - data.CoreY);
                if (h > 0)
                {
                    float heat = hot;
                    if (p.Kindle < 1)
                    {
                        // Lit from the heart outward: past the edge of the light the seams are still a dark crust.
                        float edge = p.Kindle * data.CoreReach * 1.1f;
                        heat = fromCore <= edge - 3 ? heat : fromCore >= edge ? 0.05f : 0.05f + (heat - 0.05f) * (edge - fromCore) / 3;
                    }
                    heat += (PixelNoise.Value(x, y + frame * 7, 51) - 0.5f) * 0.14f;
                    if (part == Part.Trunk && fromCore < 10) heat += p.Core * (1 - fromCore / 10) * 0.6f * System.Math.Min(1, p.Kindle * 3);
                    if (heat < 0.5f) c = c.Mix(Crust.Mix(Ember, h * heat * 1.4f), (0.5f - heat) * 1.8f);
                    else c = c.Mix(heat < 0.9f ? FlameTip : WhiteHot, System.Math.Min(0.6f, (heat - 0.5f) * 0.9f * (0.4f + 0.6f * h)));
                }
                if (reach > 0.7f && p.Fist > 0)
                {
                    // The hands burn molten: the rock reddens and cracks open bright in places.
                    float m = System.Math.Min(1, p.Fist * (reach - 0.7f) / 0.3f) * System.Math.Min(1, p.Kindle * 2);
                    if (h > 0 || PixelNoise.Value(x, y + frame / 2, 53) < m * 0.45f) c = c.Mix(h > 0 ? FlameTip : Molten, System.Math.Min(0.9f, m));
                    else c = c.Mix(Ember, m * 0.35f);
                }
                return c;
            }

            // The trunk, the head and the legs, each pixel in four to leave no holes as it leans and squashes.
            float headTurn = p.Dip;
            float headX = 0, headY = 0, heads = 0;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    var part = data.Parts[x, y];
                    if (part == Part.None || part == Part.ArmA || part == Part.ArmB) continue;
                    var c = Colour(x, y, part, 0);
                    float z = part == Part.Head || part == Part.Eye ? 0.2f : 0;
                    for (int k = 0; k < 4; k++)
                    {
                        float ox = x + ((k & 1) == 0 ? -0.25f : 0.25f), oy = y + (k < 2 ? -0.25f : 0.25f);
                        float lx = ox, ly = oy, lz = 0;
                        if (part == Part.LegA || part == Part.LegB)
                        {
                            // A raised foot: the lower the row, the higher it goes, and a little ahead.
                            float lift = part == Part.LegA ? p.LegA : p.LegB;
                            float down = System.Math.Max(0, System.Math.Min(1, (oy - data.Hip) / System.Math.Max(1, Ground - data.Hip)));
                            float up = lift * (0.3f + 0.7f * down), ahead = lift * 0.35f * down;
                            lx += fx * ahead;
                            ly += -up + fz * ahead * Toward;
                            lz = fz * ahead;
                        }
                        else
                        {
                            (lx, ly, lz) = Leaned(ox, oy, 0, part == Part.Head || part == Part.Eye ? headTurn : 0);
                        }
                        var (cx, cy) = Place(lx, ly);
                        Put(cx, cy, z + lz * 0.2f, c, part);
                        if (k == 0 && (part == Part.Head || part == Part.Eye))
                        {
                            headX += cx;
                            headY += cy;
                            heads++;
                        }
                    }
                }

            // What an arm hid where it hung in front of the body (the picture has nothing there): the body behind it,
            // filled from the nearest of the body's own pixels in the row, darker, behind everything else, so that an
            // arm raised or swung away leaves rock and not a hole. Seen square on the arms hang beside the body and
            // hide only its edge; seen from an angle one hangs over the hip and leg.
            int hidden = data.Yaw <= 0.1f || data.Yaw >= 179.9f ? 3 : 9;
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    var part = data.Parts[x, y];
                    if (part != Part.ArmA && part != Part.ArmB) continue;
                    int near = -1;
                    for (int k = 1; k <= hidden && near < 0; k++)
                    {
                        foreach (int nx in new[] { x - k, x + k })
                        {
                            if (nx < 0 || nx >= 64) continue;
                            var other = data.Parts[nx, y];
                            if (other == Part.None || other == Part.ArmA || other == Part.ArmB || other == Part.Eye) continue;
                            near = nx;
                            break;
                        }
                    }
                    if (near < 0) continue;
                    var c = data.Hot[near, y] > 0 ? Crust : image[near, y].Scale(0.8f);
                    var (lx, ly, lz) = Leaned(x, y, 0, 0);
                    var (cx, cy) = Place(lx, ly);
                    for (int k = 0; k < 4; k++) Put(cx + ((k & 1) == 0 ? -0.25f : 0.25f), cy + (k < 2 ? -0.25f : 0.25f), -1 + lz * 0.2f, c, Part.Trunk);
                }
            }

            // The arms: each a bone from the shoulder (which leans with the body) with the picture's width round it. The
            // bone is turned out to the side, then raised ahead, in three dimensions and seen from the view's angle
            // (shorter as it points at the viewer or away); the width stays across the bone as it is seen, so an arm
            // never thins to a line. Nearer than the trunk where it comes toward the viewer.
            float handX = 0, handY = 0, handFar = -1;
            void Arm(Part arm, float px, float py, float length, float dirX, float dirY, float flex, float outward, float baseZ, bool record)
            {
                if (length <= 0) return;
                float side = arm == Part.ArmA ? 1 : -1;
                // An arm drawn reaching ahead at rest (the side views) is raised from there as if from hanging.
                float ahead = (float)System.Math.Atan2(dirX * fx, dirY) * 180 / (float)System.Math.PI * System.Math.Abs(fx);
                flex -= ahead * System.Math.Max(0, System.Math.Min(1, flex / 90));
                var (spx, spy, spz) = Leaned(px, py, 0, 0);
                // The bone and the way across it, turned.
                var bone = Turn(dirX, dirY, 0, side * fx, 0, side * fz, outward);
                bone = Turn(bone.X, bone.Y, bone.Z, sx, 0, sz, flex);
                var across = Turn(-dirY, dirX, 0, side * fx, 0, side * fz, outward);
                across = Turn(across.X, across.Y, across.Z, sx, 0, sz, flex);
                float bx = bone.X, by = bone.Y + bone.Z * Toward;
                float seen = Distance(bx, by);
                float ux = seen > 0.05f ? bx / seen : 0, uy = seen > 0.05f ? by / seen : 1;
                // Across the bone on the screen, on the side the turned width points to.
                float nx = -uy, ny = ux;
                float ax0 = across.X, ay0 = across.Y + across.Z * Toward;
                if (nx * ax0 + ny * ay0 < -0.05f || (System.Math.Abs(nx * ax0 + ny * ay0) <= 0.05f && nx * -dirY + ny * dirX < 0))
                {
                    nx = -nx;
                    ny = -ny;
                }
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        if (data.Parts[x, y] != arm) continue;
                        float reach = Distance(x - px, y - py) / length;
                        var c = Colour(x, y, arm, reach);
                        for (int k = 0; k < 9; k++)
                        {
                            float ox = x + (k % 3 - 1) / 3f - px, oy = y + (k / 3 - 1) / 3f - py;
                            float along = ox * dirX + oy * dirY, wide = ox * -dirY + oy * dirX;
                            float ax = spx + bx * along + nx * wide, ay = spy + by * along + ny * wide;
                            var (cx, cy) = Place(ax, ay);
                            Put(cx, cy, baseZ + spz + bone.Z * along, c, arm);
                            if (record && k == 4 && reach > handFar)
                            {
                                handFar = reach;
                                handX = cx;
                                handY = cy;
                            }
                        }
                    }
            }
            Arm(Part.ArmA, data.AX, data.AY, data.ALength, data.ADirX, data.ADirY, p.AFlex, p.AOut, nearness, true);
            Arm(Part.ArmB, data.BX, data.BY, data.BLength, data.BDirX, data.BDirY, p.BFlex, p.BOut, data.Yaw == 0 || data.Yaw == 180 ? nearness : -nearness, data.ALength <= 0);

            // The heart flaring through the chest (seen from the front).
            var (coreLx, coreLy, _) = Leaned(data.CoreX, data.CoreY, 0, 0);
            var (coreX, coreY) = Place(coreLx, coreLy);
            if (p.Core > 0.55f && data.Yaw < 100 && p.Kindle >= 0.5f)
            {
                float r = 0.8f + (p.Core - 0.55f) * 2.2f;
                for (int y = (int)(coreY - r - 1); y <= coreY + r + 1; y++)
                    for (int x = (int)(coreX - r - 1); x <= coreX + r + 1; x++)
                    {
                        if (!canvas.Inside(x, y) || canvas[x, y].A == 0 || depth[x, y] > 0.5f) continue;
                        float d = Distance(x - coreX, y - coreY);
                        if (d > r + 0.5f) continue;
                        canvas[x, y] = canvas[x, y].Mix(WhiteHot, d < r * 0.5f ? 0.9f : 0.55f);
                    }
            }

            // Flames licking up off its shoulders and head: tongues standing on the top of the body, flickering.
            if (flame > 0.05f)
            {
                var tongues = new List<(int X, int Y, int Tall)>();
                for (int x = 0; x < Width; x++)
                {
                    int y = 0;
                    while (y < Height && canvas[x, y].A == 0) y++;
                    // Only where the top of the body is a shoulder or the head (not an arm raised over it).
                    if (y >= Height || y > bodyTop + 20 || (owner[x, y] != Part.Head && owner[x, y] != Part.Trunk)) continue;
                    float n = PixelNoise.Value(x, 0, 61);
                    if (n < 0.6f) continue;
                    float flick = PixelNoise.Value(x, frame, 62);
                    int tall = R(flame * (1.5f + 3.5f * n) * (0.7f + 0.6f * flick));
                    if (tall > 0) tongues.Add((x, y, tall));
                }
                foreach (var (x, y, tall) in tongues)
                    for (int k = 1; k <= tall; k++)
                    {
                        float u = k / (float)tall;
                        int lean = R((float)System.Math.Sin(frame * 1.3f + x * 0.7f) * u * 1.2f);
                        int px = x + lean, py = y - k;
                        if (!canvas.Inside(px, py) || canvas[px, py].A > 0) continue;
                        canvas[px, py] = u < 0.35f ? FlameBase : u < 0.75f ? FlameMid : FlameTip;
                        depth[px, py] = 0;
                    }
            }

            // Leaning or a raised arm can open a narrow gap where the parts met: within where the body stood at rest, a
            // see-through slit with body both sides is closed with the body's own pixels.
            var rest = new bool[Width, Height];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    if (data.Parts[x, y] == Part.None) continue;
                    var (cx, cy) = Place(x, y);
                    for (int oy = -1; oy <= 1; oy++)
                        if (canvas.Inside(R(cx), R(cy) + oy)) rest[R(cx), R(cy) + oy] = true;
                }
            canvas.CloseGaps(rest, new Rgba(20, 18, 20), 4);
            // And a slit shut in between an arm and the trunk, where the arm hung against it at rest.
            canvas.FillHoles(40);

            joints.HeadX = heads > 0 ? headX / heads : coreX;
            joints.HeadY = heads > 0 ? headY / heads : coreY - 12;
            joints.HandX = handFar >= 0 ? handX : coreX;
            joints.HandY = handFar >= 0 ? handY : coreY;
            joints.CoreX = coreX;
            joints.CoreY = coreY;

            // Sunk into the lava (the whole body let down into the floor): everything under its surface is gone, a pool
            // round where it went down.
            var result = new PixelImage(Width, Height);
            int line = R(ground);
            float middle = data.MiddleX + Margin + shake;
            bool pool = p.Sink >= 3;
            float half = data.HalfWidth * System.Math.Min(1.1f, 0.7f + p.Sink / 60);
            if (pool) Pool(result, middle, line, half, false, frame);
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var c = canvas[x, y];
                    if (c.A == 0) continue;
                    if (p.Sink > 0.5f && y > line) continue;
                    // Just above the lava the body glows with it.
                    if (pool && y > line - 3) c = c.Mix(Molten, 0.35f + 0.1f * (y - line + 3));
                    result[x, y] = c;
                    joints.Top = System.Math.Min(joints.Top, y);
                    joints.Bottom = System.Math.Max(joints.Bottom, y);
                    joints.Left = System.Math.Min(joints.Left, x);
                    joints.Right = System.Math.Max(joints.Right, x);
                }
            if (pool) Pool(result, middle, line, half, true, frame);
            return result;
        }

        // The pool of lava it rose from: a crust broken by bright cracks, slow rings and bubbles, the far half behind
        // the body, the near half over where the body goes into it.
        static void Pool(PixelImage image, float middle, int line, float half, bool near, int frame)
        {
            float rx = half + 5, ry = 4 + half * 0.28f;
            for (int y = (int)(line - ry - 2); y <= line + ry + 2; y++)
                for (int x = (int)(middle - rx - 2); x <= middle + rx + 2; x++)
                {
                    if (!image.Inside(x, y)) continue;
                    if (near != (y >= line)) continue;
                    float u = (x - middle) / rx, v = (y - line) / ry;
                    float d = (float)System.Math.Sqrt(u * u + v * v);
                    if (d > 1) continue;
                    if (near && image[x, y].A > 0 && d < 0.8f) continue;
                    // Rings of bright lava running slowly out under a broken crust, a glowing rim.
                    float ring = (d * 5 - frame / 10f) % 1;
                    if (ring < 0) ring += 1;
                    bool crack = PixelNoise.Value(x / 2, y, 63 + frame / 6) < 0.28f;
                    var c = d > 0.9f ? PoolDark : ring < 0.18f || crack ? PoolLight : d < 0.45f ? PoolMain.Mix(PoolLight, 0.3f) : PoolMain;
                    image[x, y] = c;
                }
            if (!near) return;
            // Bubbles swelling and bursting on the surface.
            for (int i = 0; i < 5; i++)
            {
                int life = 10 + (i * 3) % 7, age = (frame + i * 5) % life;
                float bx = middle + (PixelNoise.Value(i, (frame + i * 5) / life, 64) - 0.5f) * rx * 1.5f;
                float by = line + (PixelNoise.Value(i, (frame + i * 5) / life, 65) - 0.5f) * ry * 1.2f;
                int r = age < life - 2 ? age * 2 / life + 1 : 0;
                for (int y = -r; y <= r; y++)
                    for (int x = -r; x <= r; x++)
                    {
                        int px = R(bx) + x, py = R(by) + y;
                        if (x * x + y * y > r * r + 1 || !image.Inside(px, py) || image[px, py].A == 0) continue;
                        image[px, py] = x * x + y * y >= r * r ? PoolDark : FlameTip;
                    }
                if (r == 0 && image.Inside(R(bx), R(by) - 2)) image[R(bx), R(by) - 2] = FlameMid;
            }
        }
    }
}
