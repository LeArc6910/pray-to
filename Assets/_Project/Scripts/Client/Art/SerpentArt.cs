using System.Collections.Generic;

namespace Game.Client
{
    // What the tide serpent is doing, which picks its frame (WorldView.Serpent.cs, Docs/Bosses.md 물살 뱀). A great sea
    // serpent coiled in three loops with its head raised on a tall neck. Every attack has its own wind-up of the whole
    // body: the head drawn back low and the coils wound tight, then the body lashed across ahead (Coil, Lash); the head
    // reared back with the throat swelling, then thrust out spitting a jet (Draw, Spit); swelling all over, then the
    // head thrown up as bubbles burst from it (Puff, Burst); the head dipped to the water ahead and the coils sinking,
    // then a lunge low along the floor (Dive, Surge); the coils winding round and sinking, then churning (Wind,
    // Whirl); towering up, then the head crashing down (Loom, Crash); the fin frills flared wide and the head low, then
    // a blow straight down (Flare, Slam); rearing as high as it goes, the throat full, then a spout thrown at the roof
    // (Rise, Spout); a threat with the frills spread and the head high.
    enum SerpentPose
    {
        Idle,
        Walk,
        Coil,
        Lash,
        Draw,
        Spit,
        Puff,
        Burst,
        Dive,
        Surge,
        Wind,
        Whirl,
        Loom,
        Crash,
        Flare,
        Slam,
        Rise,
        Spout,
        Roar,
        // Its entrance: under the water of its flooded hall with only ripples to show, bubbles rising, the head breaking
        // the surface, the whole body rising out of it with the frills flared, and down to rest.
        Enter,
    }

    // The tide serpent of the underground lake (2026-11-28): one picture of the local image models (Z-Image-Turbo with a
    // pixel-art LoRA, turned eight ways by Qwen-Image-Edit with the multiple-angles LoRA, Docs/ArtSources.md) cut to
    // five views; every pose is made in code: the head and the neck above the coils moved apart from them (the neck
    // bending from where it leaves the coils), the fin frills flared, the throat swollen, the coils squashed,
    // swollen and writhing, the whole body sunk into the water behind a ring of ripples, and the eyes lit. Faces right.
    static partial class SerpentArt
    {
        public const int Width = 96;
        public const int Height = 92;
        // Rows under its coils; WorldView stands the picture on this row (Pivot).
        public const int Floor = 4;
        // How tall the body itself stands, for the name and marks over it.
        public const int BodyHeight = 50;

        // Room round the picture for the head thrust out and reared up.
        const int Margin = 16;
        const int Headroom = 26;
        // The row of the picture its coils rest on.
        const int Ground = 61;

        enum Part : byte
        {
            None,
            Head,
            Neck,
            Coils,
            Eye,
        }

        // What it knows about a view: its colours and parts, which way it faces on the screen, where the neck leaves
        // the coils (Base) and meets the head (Joint), the middle of the head, the middle and half width of the coils.
        sealed class ViewData
        {
            public PixelImage Image;
            public Part[,] Parts;
            public float AheadX, AheadY;
            public float Base, Joint;
            public float HeadX, HeadY;
            public float MiddleX, HalfWidth;
            public float[] NeckMiddle;
        }

        static readonly Dictionary<BossView, ViewData> Views = new Dictionary<BossView, ViewData>();

        static Part PartOf(char c) => c switch
        {
            'H' => Part.Head,
            'N' => Part.Neck,
            'C' => Part.Coils,
            'E' => Part.Eye,
            _ => Part.None,
        };

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
            var data = new ViewData { Image = PixelImage.FromRows(Palette, rows), Parts = new Part[64, 64], NeckMiddle = new float[64] };
            float headX = 0, headY = 0;
            int heads = 0, top = 64, bottom = -1, left = 64, right = -1;
            for (int y = 0; y < 64; y++)
            {
                float neckSum = 0;
                int neckCount = 0;
                for (int x = 0; x < 64; x++)
                {
                    var part = PartOf(parts[y][x]);
                    data.Parts[x, y] = part;
                    if (part == Part.Head || part == Part.Eye)
                    {
                        headX += x;
                        headY += y;
                        heads++;
                    }
                    if (part == Part.Neck)
                    {
                        top = System.Math.Min(top, y);
                        bottom = System.Math.Max(bottom, y);
                        neckSum += x;
                        neckCount++;
                    }
                    if (part == Part.Coils)
                    {
                        left = System.Math.Min(left, x);
                        right = System.Math.Max(right, x);
                    }
                }
                data.NeckMiddle[y] = neckCount > 0 ? neckSum / neckCount : 32;
            }
            data.HeadX = heads > 0 ? headX / heads : 32;
            data.HeadY = heads > 0 ? headY / heads : 16;
            data.Joint = top;
            // The neck bends from a little above where it goes into the coils.
            data.Base = bottom - (bottom - top) * 0.2f;
            data.MiddleX = (left + right) / 2f;
            data.HalfWidth = (right - left) / 2f + 2;
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

        // ---- A pose: the head thrust ahead (pixels) and raised; the fin frills flared; the throat swollen; the coils
        // swollen, squashed (below 0) or stretched, writhing (pixels of sway); the body sunk into the water (pixels),
        // shaken; how brightly the eyes burn (0 to 1); how much water it throws. ----

        struct Pose
        {
            public float HeadAhead, HeadUp, Flare, Throat, Swell, Squash, Writhe, Sink, Shake, Glow, Spray;
        }

        static Pose P(float headAhead = 0, float headUp = 0, float flare = 0, float throat = 0, float swell = 0, float squash = 0,
            float writhe = 0, float sink = 0, float shake = 0, float glow = 0.25f, float spray = 0) =>
            new Pose
            {
                HeadAhead = headAhead, HeadUp = headUp, Flare = flare, Throat = throat, Swell = swell, Squash = squash,
                Writhe = writhe, Sink = sink, Shake = shake, Glow = glow, Spray = spray,
            };

        static readonly Pose Rest = P();

        static Pose Lerp(Pose a, Pose b, float u)
        {
            float L(float x, float y) => x + (y - x) * u;
            return P(L(a.HeadAhead, b.HeadAhead), L(a.HeadUp, b.HeadUp), L(a.Flare, b.Flare), L(a.Throat, b.Throat), L(a.Swell, b.Swell),
                L(a.Squash, b.Squash), L(a.Writhe, b.Writhe), L(a.Sink, b.Sink), L(a.Shake, b.Shake), L(a.Glow, b.Glow), L(a.Spray, b.Spray));
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
        // strike, the rebound, held (the head down on the floor: the opening to hit back), pulled back, back to rest;
        // and the slow parts of the entrance.
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

        // The head drawn back and low behind its coils, the coils wound tight and swaying one way.
        static readonly Pose Coiled = P(-6, -3, 1, squash: -0.08f, writhe: 2.5f, glow: 0.5f);
        // Reared back with the head high, the frills up, the throat swelling with water.
        static readonly Pose Drawn = P(-6, 7, 2, 3, squash: 0.05f, glow: 0.9f);
        // Swollen all over, the head pulled in, the frills half up.
        static readonly Pose Puffed = P(-2, 2, 1.5f, 2.5f, 0.09f, -0.06f, glow: 0.7f);
        // The head dipped to the floor ahead, the coils going down into the water.
        static readonly Pose Dived = P(6, -11, 0, squash: -0.06f, sink: 6, glow: 0.6f);
        // The coils wound round and sinking, the head up and shaking.
        static readonly Pose Wound = P(0, 4, 2, swell: 0.03f, squash: -0.08f, writhe: 2, sink: 5, shake: 0.5f, glow: 0.8f);
        // Towering up as tall as the neck reaches, the frills spread.
        static readonly Pose Loomed = P(-3, 15, 3, squash: 0.1f, glow: 1);
        // The frills flared wide, the head raised a little between them to come down.
        static readonly Pose Flared = P(1, 3, 5, 1, squash: -0.02f, glow: 1);
        // Reared as high as it goes, the throat full, the frills wide, trembling.
        static readonly Pose Risen = P(-2, 19, 4, 2.5f, squash: 0.14f, shake: 0.4f, glow: 1);
        static readonly Pose RoarTop = P(-3, 10, 5, 1, squash: 0.06f, shake: 0.6f, glow: 1);

        // Out of the coil: the head and the body lashed across ahead, low, the water thrown off it.
        static readonly (Pose[] Frames, Stage[] Stages) LashBlow = Expand(Coiled,
            (Stage.Swing, 3, P(6, -3, 1, squash: 0.02f, writhe: -2, glow: 0.5f)),
            (Stage.Strike, 1, P(10, -5, 2, squash: -0.04f, writhe: -3.5f, glow: 0.6f, spray: 0.7f)),
            (Stage.Rebound, 1, P(9, -5, 1, squash: -0.03f, writhe: -2.5f, glow: 0.5f)),
            (Stage.Held, 3, P(9, -5, 1, squash: -0.03f, writhe: -2, glow: 0.4f)),
            (Stage.Pull, 2, P(4, -2, 0, writhe: -1)),
            (Stage.Recover, 6, Rest));

        // The head thrust out and down: a jet of water out of its jaws, the throat emptying.
        static readonly (Pose[] Frames, Stage[] Stages) SpitBlow = Expand(Drawn,
            (Stage.Swing, 2, P(3, 4, 2, 2, glow: 1)),
            (Stage.Strike, 1, P(8, 1, 3, 0, squash: -0.03f, glow: 1, spray: 1)),
            (Stage.Strike, 1, P(9, 0, 3, -0.5f, squash: -0.04f, glow: 1, spray: 1)),
            (Stage.Rebound, 3, P(7, 1, 2, 0, glow: 0.7f, spray: 0.5f)),
            (Stage.Held, 2, P(6, 1, 1, glow: 0.5f)),
            (Stage.Recover, 8, Rest));

        // The head thrown up as it bursts: the swelling let go all at once.
        static readonly (Pose[] Frames, Stage[] Stages) BurstBlow = Expand(Puffed,
            (Stage.Swing, 2, P(-1, 7, 2, 1, 0.04f, 0.04f, glow: 0.9f)),
            (Stage.Strike, 1, P(-2, 11, 4, -0.5f, -0.02f, 0.1f, glow: 1, spray: 1)),
            (Stage.Rebound, 3, P(-1, 7, 3, 0, 0, 0.04f, glow: 0.7f, spray: 0.5f)),
            (Stage.Held, 2, P(0, 4, 1, glow: 0.5f)),
            (Stage.Recover, 8, Rest));

        // A lunge low along the floor out of the water, the coils pushing it on.
        static readonly (Pose[] Frames, Stage[] Stages) SurgeBlow = Expand(Dived,
            (Stage.Swing, 3, P(11, -9, 1, squash: -0.02f, writhe: 2, sink: 4, glow: 0.7f)),
            (Stage.Strike, 1, P(13, -8, 2, squash: -0.06f, writhe: 2.5f, sink: 3, glow: 0.8f, spray: 1)),
            (Stage.Rebound, 2, P(12, -7, 1, squash: -0.04f, writhe: 1.5f, sink: 2, glow: 0.6f)),
            (Stage.Held, 3, P(12, -7, 1, squash: -0.04f, writhe: 1, sink: 2, glow: 0.5f)),
            (Stage.Pull, 2, P(6, -3, 0, sink: 1)),
            (Stage.Recover, 8, Rest));

        // The coils churning round: the water dragged round with them.
        static readonly (Pose[] Frames, Stage[] Stages) WhirlBlow = Expand(Wound,
            (Stage.Swing, 2, P(1, 5, 2, swell: 0.02f, squash: -0.04f, writhe: -3, sink: 4, glow: 1)),
            (Stage.Strike, 1, P(2, 3, 3, swell: 0.04f, squash: -0.08f, writhe: 4, sink: 4, glow: 1, spray: 1)),
            (Stage.Rebound, 3, P(1, 3, 2, squash: -0.05f, writhe: -3, sink: 3, glow: 0.8f, spray: 0.6f)),
            (Stage.Held, 4, P(0, 3, 1, squash: -0.04f, writhe: 2, sink: 2, glow: 0.6f, spray: 0.3f)),
            (Stage.Recover, 8, Rest));

        // Down from its height: the head crashing onto the floor ahead, held there.
        static readonly (Pose[] Frames, Stage[] Stages) CrashBlow = Expand(Loomed,
            (Stage.Swing, 4, P(8, -4, 2, squash: 0.02f, glow: 1)),
            (Stage.Strike, 1, P(10, -11, 2, squash: -0.1f, glow: 1, spray: 1)),
            (Stage.Rebound, 1, P(9, -9, 1, squash: -0.07f, glow: 0.8f)),
            (Stage.Held, 3, P(9, -9, 1, squash: -0.06f, glow: 0.6f)),
            (Stage.Pull, 2, P(4, -3, 0, squash: -0.02f)),
            (Stage.Recover, 8, Rest));

        // The flared head driven straight down: the water thrown out four ways.
        static readonly (Pose[] Frames, Stage[] Stages) SlamBlow = Expand(Flared,
            (Stage.Swing, 3, P(4, -4, 4, 1, squash: -0.05f, glow: 1)),
            (Stage.Strike, 1, P(5, -9, 2, squash: -0.1f, glow: 1, spray: 1)),
            (Stage.Rebound, 1, P(5, -7, 2, squash: -0.07f, glow: 0.8f)),
            (Stage.Held, 3, P(5, -7, 1, squash: -0.06f, glow: 0.6f)),
            (Stage.Pull, 2, P(2, -2, 0, squash: -0.02f)),
            (Stage.Recover, 6, Rest));

        // The spout thrown at the roof: the head jerked up, water pouring out of its jaws, falling back as rain.
        static readonly (Pose[] Frames, Stage[] Stages) SpoutBlow = Expand(Risen,
            (Stage.Swing, 2, P(-3, 21, 4, 2, squash: 0.15f, glow: 1)),
            (Stage.Strike, 1, P(-2, 23, 5, -0.5f, squash: 0.16f, glow: 1, spray: 1.4f)),
            (Stage.Rebound, 3, P(-1, 18, 4, 0, squash: 0.1f, glow: 1, spray: 1)),
            (Stage.Held, 4, P(-1, 15, 3, 0, squash: 0.08f, glow: 0.8f, spray: 0.6f)),
            (Stage.Recover, 10, Rest));

        static (Pose[] Frames, Stage[] Stages)? BlowOf(SerpentPose pose) => pose switch
        {
            SerpentPose.Lash => LashBlow,
            SerpentPose.Spit => SpitBlow,
            SerpentPose.Burst => BurstBlow,
            SerpentPose.Surge => SurgeBlow,
            SerpentPose.Whirl => WhirlBlow,
            SerpentPose.Crash => CrashBlow,
            SerpentPose.Slam => SlamBlow,
            SerpentPose.Spout => SpoutBlow,
            SerpentPose.Enter => (EnterFrames, EnterStages),
            _ => null,
        };

        // ---- The entrance: under the water with only ripples, bubbles rising; the head breaks the surface and looks
        // about, the whole body rises out with the frills flared, a roar, and down to rest. ----

        static readonly Pose Under = P(0, 0, 0, glow: 0, sink: 62);
        static readonly Pose Peek = P(2, 2, 1, glow: 0.4f, sink: 44);
        static readonly (Pose[] Frames, Stage[] Stages) Entrance = Expand(Under,
            (Stage.Slow, 8, P(0, 0, 0, glow: 0, sink: 60, spray: 0.3f)),
            (Stage.Slow, 6, P(0, 0, 0, glow: 0.2f, sink: 52, shake: 0.6f, spray: 0.4f)),
            (Stage.Slow, 6, Peek),
            (Stage.Slow, 4, P(4, 2, 1, glow: 0.6f, sink: 44)),
            (Stage.Slow, 4, P(-2, 3, 1, glow: 0.6f, sink: 42)),
            (Stage.Swing, 5, P(-1, 10, 3, squash: 0.08f, sink: 12, glow: 0.9f)),
            (Stage.Strike, 1, P(-2, 14, 5, squash: 0.12f, sink: 0, glow: 1, spray: 1.2f)),
            (Stage.Rebound, 3, P(-2, 11, 5, squash: 0.08f, glow: 1, spray: 0.6f)),
            (Stage.Slow, 8, P(-3, 10, 5, 1, squash: 0.06f, shake: 1, glow: 1)),
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

        // The frames of the entrance: the head breaking the surface, the body bursting up out of the water, the roar.
        public static int EnterPeek => 1 + 8 + 6;
        public static int EnterBurst => 1 + System.Array.IndexOf(Entrance.Stages, Stage.Strike);
        public static int EnterRoar => EnterBurst + 4;
        public static int RoarRise => 12;

        // The frame of the entrance a share of the way through it (a return visit plays only the burst on).
        public static int EnterFrameAt(float progress, bool again)
        {
            int from = again ? EnterBurst - 4 : 0;
            int to = EnterFrames.Length - 1;
            return System.Math.Max(from, System.Math.Min(to, from + (int)(progress * (to - from + 1))));
        }

        static Stage[] StagesOf(SerpentPose pose) => BlowOf(pose)?.Stages;

        public static int FramesOf(SerpentPose pose) => pose switch
        {
            SerpentPose.Idle => 8,
            SerpentPose.Walk => 8,
            SerpentPose.Coil => 12,
            SerpentPose.Draw => 12,
            SerpentPose.Puff => 12,
            SerpentPose.Dive => 12,
            SerpentPose.Wind => 14,
            SerpentPose.Loom => 14,
            SerpentPose.Flare => 12,
            SerpentPose.Rise => 18,
            SerpentPose.Roar => RoarRise + 4,
            _ => BlowOf(pose)?.Frames.Length ?? 4,
        };

        // How long a blow's frame is shown (seconds); 0 for anything else.
        public static float FrameSeconds(SerpentPose pose, int frame)
        {
            var stages = StagesOf(pose);
            return stages == null ? 0 : StageSeconds(stages[System.Math.Max(0, System.Math.Min(stages.Length - 1, frame))]);
        }

        public static bool Swinging(SerpentPose pose, int frame)
        {
            var stages = StagesOf(pose);
            return stages != null && frame < stages.Length && (stages[frame] == Stage.Swing || stages[frame] == Stage.Strike);
        }

        public static int StrikeFrame(SerpentPose pose) => System.Array.IndexOf(StagesOf(pose) ?? new Stage[0], Stage.Strike);

        // The wind-up that comes before each blow.
        public static SerpentPose WindUpOf(SerpentPose blow) => blow switch
        {
            SerpentPose.Lash => SerpentPose.Coil,
            SerpentPose.Spit => SerpentPose.Draw,
            SerpentPose.Burst => SerpentPose.Puff,
            SerpentPose.Surge => SerpentPose.Dive,
            SerpentPose.Whirl => SerpentPose.Wind,
            SerpentPose.Crash => SerpentPose.Loom,
            SerpentPose.Slam => SerpentPose.Flare,
            SerpentPose.Spout => SerpentPose.Rise,
            _ => SerpentPose.Coil,
        };

        static Pose PoseOf(SerpentPose pose, int frame)
        {
            int count = FramesOf(pose);
            int f = ((frame % count) + count) % count;
            float t = count > 1 ? f / (float)(count - 1) : 0;
            float cycle = f / (float)count * 2 * (float)System.Math.PI;
            float s = (float)System.Math.Sin(cycle), c = (float)System.Math.Cos(cycle);
            switch (pose)
            {
                case SerpentPose.Idle:
                    // Swaying on its neck, the coils breathing, the frills stirring.
                    return P(0.8f * s, 0.6f * c, 0.6f + 0.6f * s, swell: 0.02f * (1 + c) / 2, writhe: 0.5f * s);
                case SerpentPose.Walk:
                    // Slithering: the coils rippling, the head carried ahead and bobbing.
                    return P(2 + 0.6f * c, 0.8f * s, 0.5f, squash: -0.02f, writhe: 2.2f * s);
                case SerpentPose.Coil:
                    return Keys(t, (0, Rest), (0.45f, P(-3, -1, 1, squash: -0.04f, writhe: 1.5f, glow: 0.4f)), (1, Coiled));
                case SerpentPose.Draw:
                    return Keys(t, (0, Rest), (0.45f, P(-3, 4, 1, 1.5f, squash: 0.02f, glow: 0.6f)), (1, Drawn));
                case SerpentPose.Puff:
                    return Keys(t, (0, Rest), (0.4f, P(-1, 1, 1, 1, 0.04f, -0.03f, glow: 0.5f)), (1, Puffed));
                case SerpentPose.Dive:
                    return Keys(t, (0, Rest), (0.45f, P(3, -5, 0, squash: -0.03f, sink: 2, glow: 0.4f)), (1, Dived));
                case SerpentPose.Wind:
                {
                    // Round and round: the coils writhing as they wind, going down.
                    var wind = Lerp(Wound, P(0, 4, 2, swell: 0.03f, squash: -0.08f, writhe: 2.5f * s, sink: 5, shake: 0.5f, glow: 0.8f), 1);
                    return Keys(t, (0, Rest), (0.3f, Lerp(Rest, wind, 0.6f)), (1, wind));
                }
                case SerpentPose.Loom:
                    return Keys(t, (0, Rest), (0.4f, P(-2, 7, 2, squash: 0.05f, glow: 0.7f)), (1, Loomed));
                case SerpentPose.Flare:
                    return Keys(t, (0, Rest), (0.45f, P(0, 0, 3, squash: -0.03f, glow: 0.7f)), (1, Flared));
                case SerpentPose.Rise:
                    return Keys(t, (0, Rest), (0.4f, P(-1, 9, 2, 1, squash: 0.07f, glow: 0.8f)), (1, Risen));
                case SerpentPose.Roar:
                    if (f < RoarRise) return Keys(f / (float)RoarRise, (0, Rest), (0.5f, P(-1, 5, 3, squash: 0.03f, glow: 0.7f)), (1, RoarTop));
                    return P(-3, 10 + s, 5 + c, 1, squash: 0.06f, shake: 1, glow: 1);
                default:
                {
                    var blow = BlowOf(pose);
                    return blow == null ? Rest : blow.Value.Frames[f];
                }
            }
        }

        // ---- Drawing. ----

        static int R(float v) => (int)System.Math.Round(v);

        static readonly Rgba WaterDark = new Rgba(22, 58, 92);
        static readonly Rgba WaterMain = new Rgba(52, 118, 160);
        static readonly Rgba WaterLight = new Rgba(120, 196, 224);
        static readonly Rgba Foam = new Rgba(214, 244, 250);
        static readonly Rgba EyeDim = new Rgba(70, 150, 160);
        static readonly Rgba EyeLit = new Rgba(168, 240, 255);
        static readonly Rgba EyeHot = new Rgba(240, 255, 255);
        // In the last phase its eyes and the tips of its frills burn a stormy violet.
        static readonly Rgba Storm = new Rgba(200, 120, 255);

        // One frame. Faces right; WorldView flips it for the left.
        public static PixelImage Draw(BossView view, SerpentPose pose, int frame, int phase) => Render(view, pose, frame, phase, out _);

        // Where the head and the body are in a frame (picture pixels): for the preview's check that nothing jumps from
        // one frame to the next.
        public struct Joints
        {
            public float HeadX, HeadY;
            public int Top, Bottom, Left, Right;
        }

        public static Joints JointsOf(BossView view, SerpentPose pose, int frame)
        {
            Render(view, pose, frame, 1, out var joints);
            return joints;
        }

        static float Smooth(float u)
        {
            u = System.Math.Max(0, System.Math.Min(1, u));
            return u * u * (3 - 2 * u);
        }

        static PixelImage Render(BossView view, SerpentPose pose, int frame, int phase, out Joints joints)
        {
            var data = ViewOf(view);
            var p = PoseOf(pose, frame);
            var image = data.Image;
            var canvas = new PixelImage(Width, Height);
            joints = new Joints { Top = int.MaxValue, Left = int.MaxValue, Bottom = -1, Right = -1 };
            // Ahead along the way it faces; seen end on, a thrust toward or away from the viewer is foreshortened.
            float ax = data.AheadX, ay = data.AheadY * 0.55f;
            int shake = p.Shake > 0 ? ((frame & 1) == 0 ? 1 : -1) * R(p.Shake) : 0;
            float stretch = 1 + p.Squash;
            int ground = Headroom + Ground;
            // How far up the neck a row is: 0 where it leaves the coils, 1 at the head.
            float Bend(float y) => Smooth((data.Base - y) / System.Math.Max(1, data.Base - data.Joint));
            float hx = p.HeadAhead * ax, hy = p.HeadAhead * ay - p.HeadUp;
            float cx = data.MiddleX, cy = (data.Base + Ground) / 2f;
            // Writhing: the coils' rows swaying side to side, the sway running along them with the frame.
            float Sway(int y) => p.Writhe * (float)System.Math.Sin(y * 0.45f + frame * 0.9f);
            // Put a pixel of the picture on the canvas, the rows scaled toward the floor by the squash.
            void Put(float x, float y, Rgba c)
            {
                int px = R(x) + Margin + shake, py = R(ground - (ground - (y + Headroom)) * stretch);
                if (canvas.Inside(px, py)) canvas[px, py] = c;
            }
            void Column(float x, float y0, float y1, Rgba c)
            {
                // A pixel of the neck reaching down to where the row below it landed, so no gap opens as it stretches.
                Put(x, y0, c);
                for (float y = y0 + 1; y <= y1 + 0.01f; y += 1) Put(x, y, c);
            }

            // The coils first, then the neck from the bottom up, then the head over it.
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    if (data.Parts[x, y] != Part.Coils) continue;
                    float px = cx + (x - cx) * (1 + p.Swell), py = cy + (y - cy) * (1 + p.Swell * 0.6f);
                    Put(px + Sway(y), py, image[x, y]);
                    if (p.Swell > 0.04f) Put(px + Sway(y) + (x > cx ? 1 : -1), py, image[x, y]);
                }
            for (int y = 63; y >= 0; y--)
            {
                float b = Bend(y), below = Bend(y + 1);
                // The throat swells about the middle of the neck.
                float throat = p.Throat * (float)System.Math.Sin(System.Math.PI * System.Math.Max(0, System.Math.Min(1, b * 1.3f)));
                for (int x = 0; x < 64; x++)
                {
                    if (data.Parts[x, y] != Part.Neck) continue;
                    float side = x - data.NeckMiddle[y];
                    float push = side == 0 ? 0 : System.Math.Sign(side) * throat * System.Math.Min(1, System.Math.Abs(side) / 3);
                    float sway = Sway(y) * (1 - b);
                    float dx = hx * b + push + sway, dy = hy * b;
                    float dyBelow = hy * below;
                    Column(x + dx, y + dy, y + 1 + dyBelow - 1, image[x, y]);
                    if (System.Math.Abs(push) >= 1) Put(x + dx - System.Math.Sign(side), y + dy, image[x, y]);
                }
            }
            float glow = System.Math.Max(0, System.Math.Min(1, p.Glow + (phase >= 3 ? 0.3f : 0)));
            var eye = phase >= 3 ? EyeLit.Mix(Storm, 0.6f).Mix(EyeHot, glow * 0.4f) : glow < 0.5f ? EyeDim.Mix(EyeLit, glow * 2) : EyeLit.Mix(EyeHot, (glow - 0.5f) * 2);
            var eyes = new List<(float X, float Y)>();
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    var part = data.Parts[x, y];
                    if (part != Part.Head && part != Part.Eye) continue;
                    // The fin frills flare out sideways and up from the middle of the head.
                    float dx = x - data.HeadX, dy = y - data.HeadY;
                    float far = (float)System.Math.Sqrt(dx * dx + dy * dy);
                    float out1 = far > 4 ? p.Flare * System.Math.Min(1, (far - 4) / 6) : 0;
                    float fx = far > 0.1f ? dx / far : 0, fy = far > 0.1f ? dy / far : 0;
                    var c = image[x, y];
                    if (part == Part.Eye)
                    {
                        eyes.Add((x + hx, y + hy));
                        c = eye;
                    }
                    else if (phase >= 3 && far > 8 && c.R + c.G + c.B > 120) c = c.Mix(Storm, 0.35f);
                    Put(x + hx + fx * out1, y + hy + fy * out1 * 0.6f, c);
                    if (out1 > 2) Put(x + hx + fx * out1 * 0.5f, y + hy + fy * out1 * 0.3f, c);
                }
            // A burning eye lights the scales next to it.
            if (glow > 0.75f || phase >= 3)
                foreach (var (ex, ey) in eyes)
                    foreach (var (ox, oy) in new[] { (1, 0), (-1, 0), (0, -1) })
                    {
                        int px = R(ex + ox) + Margin + shake, py = R(ground - (ground - (ey + oy + Headroom)) * stretch);
                        if (canvas.Inside(px, py) && canvas[px, py].A > 0 && data.Parts[System.Math.Max(0, System.Math.Min(63, R(ex - hx) + ox)), System.Math.Max(0, System.Math.Min(63, R(ey - hy) + oy))] != Part.Eye)
                            canvas[px, py] = canvas[px, py].Mix(eye, 0.45f);
                    }

            // The neck drawn out or the head thrust ahead can open a narrow gap where the parts met: within where the
            // body stood at rest, a see-through slit with body both sides is closed with the body's own pixels.
            var rest = new bool[canvas.Width, canvas.Height];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    if (data.Parts[x, y] == Part.None) continue;
                    int px = x + Margin + shake, py = R(ground - (ground - (y + Headroom)) * stretch);
                    for (int oy = -1; oy <= 1; oy++)
                        if (canvas.Inside(px, py + oy)) rest[px, py + oy] = true;
                }
            canvas.CloseGaps(rest, new Rgba(10, 20, 31), 4);
            joints.HeadX = data.HeadX + hx + Margin;
            joints.HeadY = data.HeadY + hy + Headroom;

            // Sunk into the water: everything under the waterline is gone, a ring of ripples round where it went down.
            var result = new PixelImage(Width, Height);
            int waterLine = ground - R(p.Sink);
            float middle = data.MiddleX + Margin + shake;
            bool ripples = p.Sink >= 3;
            float half = data.HalfWidth * System.Math.Min(1, 0.55f + p.Sink / 50);
            if (ripples) Ripples(result, middle, waterLine, half, false, frame);
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var c = canvas[x, y];
                    if (c.A == 0) continue;
                    if (p.Sink > 0.5f && y > waterLine) continue;
                    // Just above the water the body is wet and darker.
                    if (ripples && y > waterLine - 2) c = c.Mix(WaterDark, 0.4f);
                    result[x, y] = c;
                    joints.Top = System.Math.Min(joints.Top, y);
                    joints.Bottom = System.Math.Max(joints.Bottom, y);
                    joints.Left = System.Math.Min(joints.Left, x);
                    joints.Right = System.Math.Max(joints.Right, x);
                }
            if (ripples) Ripples(result, middle, waterLine, half, true, frame);
            if (p.Spray > 0.05f) Spray(result, joints.HeadX, joints.HeadY, ax, ay, p.Spray, frame);
            return result;
        }

        // The pool it rose from or sank into: rings of ripples, the far half behind the body, the near half over where
        // the body goes into the water, dark water in the middle.
        static void Ripples(PixelImage image, float middle, int line, float half, bool near, int frame)
        {
            float rx = half + 4, ry = 4 + half * 0.25f;
            int beat = frame % 6;
            for (int y = (int)(line - ry - 2); y <= line + ry + 2; y++)
                for (int x = (int)(middle - rx - 2); x <= middle + rx + 2; x++)
                {
                    if (!image.Inside(x, y)) continue;
                    if (near != (y >= line)) continue;
                    float u = (x - middle) / rx, v = (y - line) / ry;
                    float d = (float)System.Math.Sqrt(u * u + v * v);
                    if (d > 1) continue;
                    if (near && image[x, y].A > 0 && d < 0.8f) continue;
                    // Rings running outward, foam on the outermost.
                    float ring = (d * 6 - beat / 6f * 1.0f) % 1;
                    var c = d > 0.92f ? Foam : ring < 0.22f ? WaterLight : d < 0.55f ? WaterDark : WaterMain;
                    image[x, y] = c.WithAlpha(d > 0.92f ? 220 : 235);
                }
        }

        // Water thrown off its head: drops in a fan ahead and up, more for a bigger throw.
        static void Spray(PixelImage image, float hx, float hy, float ax, float ay, float amount, int frame)
        {
            int drops = (int)(22 * amount);
            for (int i = 0; i < drops; i++)
            {
                float r1 = PixelNoise.Value(i, frame, 81), r2 = PixelNoise.Value(i, frame, 82);
                float spread = (r1 - 0.5f) * 2.2f;
                float c = (float)System.Math.Cos(spread), s = (float)System.Math.Sin(spread);
                float dx = ax * c - ay * s, dy = ax * s + ay * c - 0.8f;
                float far = 3 + 15 * r2 * amount;
                int x = R(hx + dx * far), y = R(hy + dy * far);
                if (!image.Inside(x, y) || image[x, y].A > 0) continue;
                image[x, y] = r2 > 0.55f ? Foam : WaterLight;
                if (r2 > 0.8f && image.Inside(x, y + 1) && image[x, y + 1].A == 0) image[x, y + 1] = WaterMain;
            }
        }
    }
}
