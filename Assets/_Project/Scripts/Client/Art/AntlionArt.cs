using System.Collections.Generic;

namespace Game.Client
{
    // What the antlion is doing, which picks its frame (WorldView.Antlion.cs, Docs/Bosses.md 개미귀신). A pit-trap
    // larva: it waits buried, flicks sand with its head, seizes with its sickle jaws and drags under. Every attack has
    // its own wind-up of the whole body: the head reared back and the jaws spread wide, then a lunge that snaps them
    // shut (Gape, Snap); the head shoved down into the sand, then jerked up to throw it (Scoop, Flick); the body
    // rocking as the legs rake a pit, then a great toss of the head (Dig, Toss); the head ploughed low ahead (Plow,
    // Gouge); diving under (Dive), a mound running under the sand (Burrow), up again (Emerge); sunk to the middle with
    // its jaws wide, then the sand churned (Sink, Churn); reared up on its abdomen, then crashing down (Rear, Crash);
    // the jaws thrown wide to both sides, then clapped shut on the sand (Spread, Clap); gone under entirely, then up
    // through the floor (Submerge, Erupt); a threat with the jaws high and wide.
    enum AntlionPose
    {
        Idle,
        Walk,
        Gape,
        Snap,
        Scoop,
        Flick,
        Dig,
        Toss,
        Plow,
        Gouge,
        Dive,
        Burrow,
        Emerge,
        Sink,
        Churn,
        Rear,
        Crash,
        Spread,
        Clap,
        Submerge,
        Erupt,
        Roar,
        // Its entrance: buried at the bottom of its pit with only the jaw tips showing; sand flicked out twice, then
        // up out of the sand, shaking it off, the jaws snapping.
        Enter,
    }

    // The antlion of the sand cave (2026-11-27): a pit-trap larva with a banded abdomen and two great sickle jaws. Its
    // five views come from one Retro Diffusion Pro picture turned eight ways (Docs/ArtSources.md, $0.28); every pose
    // is made in code: the head, the two jaws, the abdomen and the legs either side are moved apart (the jaws turned
    // on their roots), and the whole body sinks into the sand behind a crater rim. Faces right.
    static partial class AntlionArt
    {
        public const int Width = 96;
        public const int Height = 84;
        // Rows under its feet; WorldView stands the picture on this row (Pivot).
        public const int Floor = 7;
        // How tall the body itself stands, for the name and marks over it.
        public const int BodyHeight = 40;

        // Room round the picture for the head thrust out and reared up.
        const int Margin = 16;
        const int Headroom = 20;
        // The row of the picture its feet stand on.
        const int Ground = 56;

        enum Part : byte
        {
            None,
            Abdomen,
            Thorax,
            Head,
            JawA,
            JawB,
            LegA,
            LegB,
        }

        // What it knows about a view: its colours and parts, which way it faces on the screen (from tail to jaws), the
        // roots of the two jaws and which way each opens, and the middle and half width of the body for the crater.
        sealed class ViewData
        {
            public PixelImage Image;
            public Part[,] Parts;
            public float AheadX, AheadY;
            public float RootAX, RootAY, RootBX, RootBY;
            public float TipAX, TipAY, TipBX, TipBY, AwayAX, AwayAY, AwayBX, AwayBY;
            public float MiddleX, HalfWidth;
            public Part[] Order;
        }

        static readonly Dictionary<BossView, ViewData> Views = new Dictionary<BossView, ViewData>();

        static Part PartOf(char c) => c switch
        {
            'A' => Part.Abdomen,
            'M' => Part.Thorax,
            'H' => Part.Head,
            'J' => Part.JawA,
            'K' => Part.JawB,
            'L' => Part.LegA,
            'R' => Part.LegB,
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
            var data = new ViewData { Image = PixelImage.FromRows(Palette, rows), Parts = new Part[64, 64] };
            float Mean(Part part, bool x)
            {
                float sum = 0;
                int count = 0;
                for (int y = 0; y < 64; y++)
                    for (int i = 0; i < 64; i++)
                        if (PartOf(parts[y][i]) == part)
                        {
                            sum += x ? i : y;
                            count++;
                        }
                return count == 0 ? 32 : sum / count;
            }
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    data.Parts[x, y] = PartOf(parts[y][x]);
            float tailX = Mean(Part.Abdomen, true), tailY = Mean(Part.Abdomen, false);
            float headX = Mean(Part.Head, true), headY = Mean(Part.Head, false);
            float length = (float)System.Math.Sqrt((headX - tailX) * (headX - tailX) + (headY - tailY) * (headY - tailY));
            data.AheadX = (headX - tailX) / length;
            data.AheadY = (headY - tailY) / length;
            // A jaw's root is its pixel nearest the middle of the head; it opens the way that carries its tip (its pixel
            // farthest from the root) away from the other jaw.
            (float, float, float, float) RootAndTip(Part jaw)
            {
                float best = float.MaxValue, rx = headX, ry = headY;
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        if (data.Parts[x, y] != jaw) continue;
                        float d = (x - headX) * (x - headX) + (y - headY) * (y - headY);
                        if (d < best) (best, rx, ry) = (d, x, y);
                    }
                float far = -1, tx = rx, ty = ry;
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        if (data.Parts[x, y] != jaw) continue;
                        float d = (x - rx) * (x - rx) + (y - ry) * (y - ry);
                        if (d > far) (far, tx, ty) = (d, x, y);
                    }
                return (rx, ry, tx, ty);
            }
            var jawA = RootAndTip(Part.JawA);
            var jawB = RootAndTip(Part.JawB);
            (data.RootAX, data.RootAY, data.TipAX, data.TipAY) = jawA;
            (data.RootBX, data.RootBY, data.TipBX, data.TipBY) = jawB;
            // Each jaw opens away from the other.
            float gapX = Mean(Part.JawA, true) - Mean(Part.JawB, true), gapY = Mean(Part.JawA, false) - Mean(Part.JawB, false);
            float gap = System.Math.Max(0.1f, (float)System.Math.Sqrt(gapX * gapX + gapY * gapY));
            (data.AwayAX, data.AwayAY, data.AwayBX, data.AwayBY) = (gapX / gap, gapY / gap, -gapX / gap, -gapY / gap);
            int left = 64, right = 0;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    if (data.Parts[x, y] == Part.Abdomen || data.Parts[x, y] == Part.Thorax || data.Parts[x, y] == Part.Head)
                    {
                        left = System.Math.Min(left, x);
                        right = System.Math.Max(right, x);
                    }
            data.MiddleX = (left + right) / 2f;
            data.HalfWidth = (right - left) / 2f + 3;
            // Far parts first: from the front the head and jaws are nearest the viewer, from behind the abdomen.
            data.Order = view == BossView.Back || view == BossView.BackSide
                ? new[] { Part.JawA, Part.JawB, Part.Head, Part.LegA, Part.LegB, Part.Thorax, Part.Abdomen }
                : new[] { Part.Abdomen, Part.LegA, Part.LegB, Part.Thorax, Part.Head, Part.JawA, Part.JawB };
            Views[view] = data;
            return data;
        }

        // ---- A pose: the head thrust ahead along the body (pixels) and raised; the jaws open (degrees, below 0 clamped
        // shut); the abdomen carried ahead, raised and swollen; each side's legs stepped ahead and lifted; the body
        // squashed (below 0) or stretched, sunk into the sand (pixels), shaken; how much sand it throws. ----

        struct Pose
        {
            public float HeadAhead, HeadUp, Open, AbdAhead, AbdUp, Swell, StepA, StepB, Lift, Stretch, Sink, Shake, Sand;
        }

        static Pose P(float headAhead = 0, float headUp = 0, float open = 0, float abdAhead = 0, float abdUp = 0, float swell = 0,
            float stepA = 0, float stepB = 0, float lift = 0, float stretch = 0, float sink = 0, float shake = 0, float sand = 0) =>
            new Pose
            {
                HeadAhead = headAhead, HeadUp = headUp, Open = open, AbdAhead = abdAhead, AbdUp = abdUp, Swell = swell,
                StepA = stepA, StepB = stepB, Lift = lift, Stretch = stretch, Sink = sink, Shake = shake, Sand = sand,
            };

        static readonly Pose Rest = P(open: 4);

        static Pose Lerp(Pose a, Pose b, float u)
        {
            float L(float x, float y) => x + (y - x) * u;
            return P(L(a.HeadAhead, b.HeadAhead), L(a.HeadUp, b.HeadUp), L(a.Open, b.Open), L(a.AbdAhead, b.AbdAhead), L(a.AbdUp, b.AbdUp),
                L(a.Swell, b.Swell), L(a.StepA, b.StepA), L(a.StepB, b.StepB), L(a.Lift, b.Lift), L(a.Stretch, b.Stretch), L(a.Sink, b.Sink),
                L(a.Shake, b.Shake), L(a.Sand, b.Sand));
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
        // strike, the rebound, held (the jaws clamped in the sand: the opening to hit back), pulled free, back to rest;
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

        // Head reared back and up, the jaws spread as wide as they go, the abdomen braced behind.
        static readonly Pose Gaped = P(-4, 5, 38, abdAhead: -2, swell: 0.05f, stepA: -1, stepB: -1, stretch: 0.04f);
        // The head shoved down into the sand ahead, jaws shut, sand heaped on them.
        static readonly Pose Scooped = P(3, -5, -6, abdAhead: 1, abdUp: 1, stepA: 1, stepB: 1, stretch: -0.06f);
        // The body rocking low, the legs raking: the abdomen pumping.
        static readonly Pose Dug = P(1, -3, 8, abdUp: 1, swell: 0.08f, stretch: -0.08f, sink: 4);
        // Head low and far ahead, half in the sand.
        static readonly Pose Plowed = P(5, -6, 10, abdAhead: 2, stepA: 2, stepB: -2, stretch: -0.1f, sink: 2);
        // Down to the jaws.
        static readonly Pose Dived = P(2, -3, -4, stretch: -0.05f, sink: 38);
        // Sunk to the middle, jaws wide, the sand round it starting to turn.
        static readonly Pose Sunk = P(0, 2, 30, swell: 0.04f, sink: 18, shake: 0.5f);
        // Reared up on the abdomen: head and jaws high and wide, the front legs off the sand.
        static readonly Pose Reared = P(-2, 12, 30, abdAhead: -3, abdUp: -1, stepA: 1, stepB: 1, lift: 4, stretch: 0.12f);
        // Jaws thrown open to both sides, head low between them.
        static readonly Pose Spreaded = P(-1, -2, 52, abdAhead: -1, swell: 0.04f, stepA: -1, stepB: 1, stretch: -0.05f);
        // Gone under entirely.
        static readonly Pose Submerged = P(0, 2, 6, sink: 42, shake: 0.5f);
        static readonly Pose RoarTop = P(-2, 9, 46, abdAhead: -2, swell: 0.06f, lift: 3, stretch: 0.08f, shake: 0.6f, sand: 0.3f);

        // Out of the wind-up: a lunge too fast to see, the jaws snapping shut on the sand ahead and clamped there.
        static readonly (Pose[] Frames, Stage[] Stages) SnapBlow = Expand(Gaped,
            (Stage.Swing, 3, P(7, -2, 12, abdAhead: 2, stepA: 2, stepB: 2, stretch: -0.06f)),
            (Stage.Strike, 1, P(9, -4, -8, abdAhead: 3, stepA: 2, stepB: 2, stretch: -0.1f, sand: 0.6f)),
            (Stage.Rebound, 1, P(8, -3, -6, abdAhead: 3, stepA: 2, stepB: 2, stretch: -0.07f)),
            (Stage.Held, 3, P(8, -3, -6, abdAhead: 3, stepA: 2, stepB: 2, stretch: -0.07f)),
            (Stage.Pull, 2, P(4, 0, 4, abdAhead: 1, stepA: 1, stepB: 1)),
            (Stage.Recover, 6, Rest));

        // Jerked up and back: the sand on its head thrown ahead in a jet.
        static readonly (Pose[] Frames, Stage[] Stages) FlickBlow = Expand(Scooped,
            (Stage.Swing, 2, P(0, 4, 6, abdAhead: 0, stretch: 0.02f)),
            (Stage.Strike, 1, P(-3, 9, 14, abdAhead: -1, stretch: 0.06f, sand: 1)),
            (Stage.Rebound, 1, P(-2, 7, 10, stretch: 0.04f, sand: 0.5f)),
            (Stage.Held, 2, P(-2, 6, 8, stretch: 0.03f)),
            (Stage.Recover, 7, Rest));

        // A great toss: the head flung up from deep in the sand, sand thrown high to fall far off.
        static readonly (Pose[] Frames, Stage[] Stages) TossBlow = Expand(Dug,
            (Stage.Swing, 3, P(2, 6, 12, abdUp: 1, stretch: 0.04f, sink: 2)),
            (Stage.Strike, 1, P(-2, 13, 20, abdAhead: -2, stretch: 0.1f, sand: 1)),
            (Stage.Rebound, 1, P(-1, 10, 16, abdAhead: -1, stretch: 0.07f, sand: 0.5f)),
            (Stage.Held, 2, P(-1, 9, 14, stretch: 0.06f)),
            (Stage.Recover, 8, Rest));

        // Driven ahead through the sand, the head ploughing a furrow.
        static readonly (Pose[] Frames, Stage[] Stages) GougeBlow = Expand(Plowed,
            (Stage.Swing, 3, P(9, -6, 4, abdAhead: 4, stepA: 3, stepB: 3, stretch: -0.1f, sink: 2)),
            (Stage.Strike, 1, P(11, -6, 0, abdAhead: 5, stepA: -2, stepB: 3, stretch: -0.12f, sink: 2, sand: 1)),
            (Stage.Rebound, 1, P(10, -5, 2, abdAhead: 4, stretch: -0.1f, sink: 2, sand: 0.5f)),
            (Stage.Held, 2, P(10, -5, 2, abdAhead: 4, stretch: -0.1f, sink: 2)),
            (Stage.Pull, 2, P(5, -2, 4, abdAhead: 2, sink: 1)),
            (Stage.Recover, 6, Rest));

        // Up out of the sand where the run ends, the jaws snapping.
        static readonly (Pose[] Frames, Stage[] Stages) EmergeBlow = Expand(Dived,
            (Stage.Swing, 4, P(4, 6, 30, stretch: 0.06f, sink: 8)),
            (Stage.Strike, 1, P(5, 8, -4, stretch: 0.08f, sand: 1)),
            (Stage.Rebound, 1, P(4, 5, 0, stretch: 0.03f, sand: 0.5f)),
            (Stage.Held, 2, P(3, 3, 0)),
            (Stage.Recover, 7, Rest));

        // The sand churned out from it: the jaws thrashing, the body shaking, then up out of it.
        static readonly (Pose[] Frames, Stage[] Stages) ChurnBlow = Expand(Sunk,
            (Stage.Swing, 2, P(2, 0, 6, sink: 16, shake: 1)),
            (Stage.Strike, 1, P(0, 3, 36, swell: 0.08f, sink: 16, shake: 1, sand: 1)),
            (Stage.Rebound, 1, P(1, 1, 10, sink: 16, shake: 1, sand: 0.6f)),
            (Stage.Held, 2, P(0, 2, 26, sink: 15, shake: 1, sand: 0.4f)),
            (Stage.Pull, 5, P(0, 1, 10, sink: 6)),
            (Stage.Recover, 6, Rest));

        // Down from high, the whole front crashing onto the sand.
        static readonly (Pose[] Frames, Stage[] Stages) CrashBlow = Expand(Reared,
            (Stage.Swing, 3, P(4, 2, 20, abdAhead: 0, stretch: -0.02f)),
            (Stage.Strike, 1, P(6, -5, 4, abdAhead: 2, abdUp: 1, stepA: 2, stepB: 2, stretch: -0.16f, sand: 1)),
            (Stage.Rebound, 1, P(5, -3, 6, abdAhead: 2, stepA: 2, stepB: 2, stretch: -0.1f, sand: 0.5f)),
            (Stage.Held, 3, P(5, -3, 6, abdAhead: 2, stepA: 2, stepB: 2, stretch: -0.1f)),
            (Stage.Pull, 2, P(2, -1, 5, abdAhead: 1, stepA: 1, stepB: 1)),
            (Stage.Recover, 7, Rest));

        // The jaws clapped shut on the sand: it sprays out to all four sides.
        static readonly (Pose[] Frames, Stage[] Stages) ClapBlow = Expand(Spreaded,
            (Stage.Swing, 2, P(2, -3, 20, abdAhead: 0, stretch: -0.06f)),
            (Stage.Strike, 1, P(4, -5, -10, abdAhead: 1, stretch: -0.12f, sand: 1)),
            (Stage.Rebound, 1, P(3, -4, -6, abdAhead: 1, stretch: -0.09f, sand: 0.5f)),
            (Stage.Held, 3, P(3, -4, -6, abdAhead: 1, stretch: -0.09f)),
            (Stage.Pull, 2, P(1, -1, 2)),
            (Stage.Recover, 6, Rest));

        // Up through the floor: the jaws first, then the whole body flung clear of the sand.
        static readonly (Pose[] Frames, Stage[] Stages) EruptBlow = Expand(Submerged,
            (Stage.Swing, 3, P(2, 6, 40, sink: 18)),
            (Stage.Strike, 1, P(2, 10, 24, lift: 4, stretch: 0.12f, sand: 1)),
            (Stage.Rebound, 3, P(1, 6, 10, lift: 2, stretch: 0.05f, sand: 0.6f)),
            (Stage.Held, 2, P(1, 3, 6, stretch: -0.06f)),
            (Stage.Recover, 8, Rest));

        static (Pose[] Frames, Stage[] Stages)? BlowOf(AntlionPose pose) => pose switch
        {
            AntlionPose.Snap => SnapBlow,
            AntlionPose.Flick => FlickBlow,
            AntlionPose.Toss => TossBlow,
            AntlionPose.Gouge => GougeBlow,
            AntlionPose.Emerge => EmergeBlow,
            AntlionPose.Churn => ChurnBlow,
            AntlionPose.Crash => CrashBlow,
            AntlionPose.Clap => ClapBlow,
            AntlionPose.Erupt => EruptBlow,
            AntlionPose.Enter => (EnterFrames, EnterStages),
            _ => null,
        };

        // ---- The entrance: buried at the bottom of its pit, the jaw tips showing; it flicks sand out twice, bursts up
        // and shakes itself off, the jaws snapping. ----

        static readonly Pose Buried = P(1, 2, 16, sink: 40);
        static readonly Pose Peek = P(2, 3, 24, sink: 38);
        static readonly Pose PeekFlick = P(-1, 8, 14, sink: 38, sand: 1);
        static readonly (Pose[] Frames, Stage[] Stages) Entrance = Expand(Buried,
            (Stage.Slow, 6, Peek),
            (Stage.Slow, 2, PeekFlick),
            (Stage.Slow, 4, Peek),
            (Stage.Slow, 2, PeekFlick),
            (Stage.Slow, 6, P(2, 0, 8, sink: 40, shake: 1)),
            (Stage.Swing, 3, P(3, 8, 36, sink: 12)),
            (Stage.Strike, 1, P(2, 12, 30, lift: 3, stretch: 0.1f, sand: 1)),
            (Stage.Rebound, 3, P(1, 6, 14, lift: 1, stretch: 0.03f, sand: 0.5f)),
            (Stage.Slow, 6, P(0, 2, 6, shake: 1)),
            (Stage.Slow, 3, P(3, 0, 34)),
            (Stage.Strike, 1, P(5, -2, -8, abdAhead: 1, stretch: -0.06f)),
            (Stage.Slow, 8, Rest));

        static readonly Pose[] EnterFrames = Prepend(Buried, Entrance.Frames);
        static readonly Stage[] EnterStages = Prepend(Stage.Slow, Entrance.Stages);

        static T[] Prepend<T>(T first, T[] rest)
        {
            var all = new T[rest.Length + 1];
            all[0] = first;
            System.Array.Copy(rest, 0, all, 1, rest.Length);
            return all;
        }

        // The frames of the entrance: the first flick, the burst up out of the sand, the snap of the jaws.
        public static int EnterFlick => 7;
        public static int EnterBurst => 1 + System.Array.IndexOf(Entrance.Stages, Stage.Strike);
        public static int EnterSnap => System.Array.LastIndexOf(EnterStages, Stage.Strike);
        public static int RoarRise => 12;

        // The frame of the entrance a share of the way through it (a return visit plays only the burst on).
        public static int EnterFrameAt(float progress, bool again)
        {
            int from = again ? EnterBurst - 4 : 0;
            int to = EnterFrames.Length - 1;
            return System.Math.Max(from, System.Math.Min(to, from + (int)(progress * (to - from + 1))));
        }

        static Stage[] StagesOf(AntlionPose pose) => BlowOf(pose)?.Stages;

        public static int FramesOf(AntlionPose pose) => pose switch
        {
            AntlionPose.Idle => 6,
            AntlionPose.Walk => 8,
            AntlionPose.Gape => 12,
            AntlionPose.Scoop => 10,
            AntlionPose.Dig => 12,
            AntlionPose.Plow => 10,
            AntlionPose.Dive => 18,
            AntlionPose.Burrow => 6,
            AntlionPose.Sink => 12,
            AntlionPose.Rear => 14,
            AntlionPose.Spread => 12,
            AntlionPose.Submerge => 20,
            AntlionPose.Roar => RoarRise + 4,
            _ => BlowOf(pose)?.Frames.Length ?? 4,
        };

        // How long a blow's frame is shown (seconds); 0 for anything else.
        public static float FrameSeconds(AntlionPose pose, int frame)
        {
            var stages = StagesOf(pose);
            return stages == null ? 0 : StageSeconds(stages[System.Math.Max(0, System.Math.Min(stages.Length - 1, frame))]);
        }

        public static bool Swinging(AntlionPose pose, int frame)
        {
            var stages = StagesOf(pose);
            return stages != null && frame < stages.Length && (stages[frame] == Stage.Swing || stages[frame] == Stage.Strike);
        }

        public static int StrikeFrame(AntlionPose pose) => System.Array.IndexOf(StagesOf(pose) ?? new Stage[0], Stage.Strike);

        // The wind-up that comes before each blow.
        public static AntlionPose WindUpOf(AntlionPose blow) => blow switch
        {
            AntlionPose.Snap => AntlionPose.Gape,
            AntlionPose.Flick => AntlionPose.Scoop,
            AntlionPose.Toss => AntlionPose.Dig,
            AntlionPose.Gouge => AntlionPose.Plow,
            AntlionPose.Emerge => AntlionPose.Dive,
            AntlionPose.Churn => AntlionPose.Sink,
            AntlionPose.Crash => AntlionPose.Rear,
            AntlionPose.Clap => AntlionPose.Spread,
            AntlionPose.Erupt => AntlionPose.Submerge,
            _ => AntlionPose.Gape,
        };

        static Pose PoseOf(AntlionPose pose, int frame)
        {
            int count = FramesOf(pose);
            int f = ((frame % count) + count) % count;
            float t = count > 1 ? f / (float)(count - 1) : 0;
            float cycle = f / (float)count * 2 * (float)System.Math.PI;
            float s = (float)System.Math.Sin(cycle), c = (float)System.Math.Cos(cycle);
            switch (pose)
            {
                case AntlionPose.Idle:
                    // Breathing through the abdomen, the jaws working a little.
                    return P(0.5f * s, 0, 4 + 4 * System.Math.Max(0, s), swell: 0.03f * (1 + c) / 2);
                case AntlionPose.Walk:
                    // The legs either side stepping in turn, the body carried between them.
                    return P(1 + 0.5f * c, 0.5f * s, 4 + 2 * c, abdAhead: 0.5f * s, stepA: 2.5f * s, stepB: -2.5f * s, lift: System.Math.Abs(s));
                case AntlionPose.Gape:
                    return Keys(t, (0, Rest), (0.45f, P(-2, 2, 22, abdAhead: -1, stretch: 0.02f)), (1, Gaped));
                case AntlionPose.Scoop:
                    return Keys(t, (0, Rest), (0.4f, P(2, -2, 0, stretch: -0.02f)), (1, Scooped));
                case AntlionPose.Dig:
                {
                    // Rocking side to side as the legs rake, sinking deeper.
                    var rock = P(1, -3, 8, abdUp: 1, swell: 0.08f * (1 + s) / 2, stepA: 2 * s, stepB: -2 * s, stretch: -0.08f, sink: 4 * t, shake: 0.6f);
                    return Keys(t, (0, Rest), (0.25f, Lerp(Rest, rock, 0.6f)), (1, rock));
                }
                case AntlionPose.Plow:
                    return Keys(t, (0, Rest), (0.45f, P(2, -3, 8, abdAhead: 1, stretch: -0.04f)), (1, Plowed));
                case AntlionPose.Dive:
                    // Head first into the sand, the abdomen last.
                    return Keys(t, (0, Rest), (0.3f, P(3, -4, 0, abdUp: 2, stretch: -0.06f, sink: 6)), (1, Dived));
                case AntlionPose.Burrow:
                    // Running under the sand: only the jaw tips cutting through it.
                    return P(2, -3 + 0.5f * s, -2 + 3 * c, sink: 40 + s, shake: 1, sand: 0.4f);
                case AntlionPose.Sink:
                    return Keys(t, (0, Rest), (0.4f, P(0, 0, 14, sink: 8, shake: 0.3f)), (1, Sunk));
                case AntlionPose.Rear:
                    return Keys(t, (0, Rest), (0.4f, P(-1, 5, 16, abdAhead: -1, stretch: 0.05f)), (1, Reared));
                case AntlionPose.Spread:
                    return Keys(t, (0, Rest), (0.45f, P(-1, 0, 30, stretch: -0.02f)), (1, Spreaded));
                case AntlionPose.Submerge:
                    return Keys(t, (0, Rest), (0.35f, P(2, -3, 0, stretch: -0.05f, sink: 14)), (1, Submerged));
                case AntlionPose.Roar:
                    if (f < RoarRise) return Keys(f / (float)RoarRise, (0, Rest), (0.5f, P(-1, 4, 24, stretch: 0.03f)), (1, RoarTop));
                    return Lerp(RoarTop, P(-2, 9 + s, 40 + 6 * s, abdAhead: -2, swell: 0.06f, lift: 3, stretch: 0.08f, shake: 1, sand: 0.3f), 1);
                default:
                {
                    var blow = BlowOf(pose);
                    return blow == null ? Rest : blow.Value.Frames[f];
                }
            }
        }

        // ---- Drawing. ----

        static int R(float v) => (int)System.Math.Round(v);

        static readonly Rgba SandLight = new Rgba(242, 196, 120);
        static readonly Rgba SandMain = new Rgba(214, 164, 96);
        static readonly Rgba SandShade = new Rgba(170, 120, 70);
        static readonly Rgba SandDark = new Rgba(96, 62, 40);
        static readonly Rgba Blood = new Rgba(150, 30, 24);

        // One frame. In the last phase its jaws are stained red. Faces right; WorldView flips it for the left.
        public static PixelImage Draw(BossView view, AntlionPose pose, int frame, int phase) => Render(view, pose, frame, phase, out _);

        // Where the jaw roots and the body are in a frame (picture pixels): for the preview's check that nothing jumps
        // from one frame to the next.
        public struct Joints
        {
            public float HeadX, HeadY;
            public int Top, Bottom, Left, Right;
        }

        public static Joints JointsOf(BossView view, AntlionPose pose, int frame)
        {
            Render(view, pose, frame, 1, out var joints);
            return joints;
        }

        static PixelImage Render(BossView view, AntlionPose pose, int frame, int phase, out Joints joints)
        {
            var data = ViewOf(view);
            var p = PoseOf(pose, frame);
            var image = data.Image;
            var canvas = new PixelImage(Width, Height);
            joints = new Joints { Top = int.MaxValue, Left = int.MaxValue, Bottom = -1, Right = -1 };
            // Ahead along the body; seen end on, a thrust toward or away from the viewer is foreshortened.
            float ax = data.AheadX, ay = data.AheadY * 0.55f;
            // Seen end on (from the front or behind) the head thrust ahead moves down or up the screen; raised, it
            // goes up the screen.
            int shake = p.Shake > 0 ? ((frame & 1) == 0 ? 1 : -1) * R(p.Shake) : 0;
            int lift = R(p.Lift);
            // The ground line moves with the squash; rows are scaled toward the feet.
            float stretch = 1 + p.Stretch;
            int ground = Headroom + Ground;
            // The head moves less than the pose says, so the thorax between it and the abdomen can still join them.
            float head = p.HeadAhead * 0.75f, up = p.HeadUp * 0.6f;
            (int X, int Y) Offset(Part part)
            {
                float x = 0, y = 0;
                switch (part)
                {
                    case Part.Head:
                    case Part.JawA:
                    case Part.JawB:
                        x = head * ax;
                        y = head * ay - up;
                        break;
                    case Part.Abdomen:
                        x = p.AbdAhead * ax;
                        y = p.AbdAhead * ay - p.AbdUp;
                        break;
                    case Part.LegA:
                        x = p.StepA * ax;
                        y = p.StepA * ay - p.Lift * 0.5f;
                        break;
                    case Part.LegB:
                        x = p.StepB * ax;
                        y = p.StepB * ay - p.Lift * 0.5f;
                        break;
                    case Part.Thorax:
                        x = (head * 0.55f + p.AbdAhead * 0.3f) * ax;
                        y = (head * 0.55f + p.AbdAhead * 0.3f) * ay - up * 0.55f;
                        break;
                }
                return (R(x) + shake, R(y) - lift);
            }
            // Put a pixel of the picture on the canvas, the rows scaled toward the ground by the squash.
            void Put(float x, float y, Rgba c)
            {
                int cx = R(x) + Margin, cy = R(ground - (ground - (y + Headroom)) * stretch);
                if (canvas.Inside(cx, cy)) canvas[cx, cy] = c;
            }
            // The abdomen swells about its middle.
            float swellX = 0, swellY = 0;
            int swellN = 0;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    if (data.Parts[x, y] == Part.Abdomen)
                    {
                        swellX += x;
                        swellY += y;
                        swellN++;
                    }
            if (swellN > 0)
            {
                swellX /= swellN;
                swellY /= swellN;
            }
            foreach (var part in data.Order)
            {
                var (dx, dy) = Offset(part);
                bool jaw = part == Part.JawA || part == Part.JawB;
                if (jaw)
                {
                    // Bent open from its root: the farther along toward the tip, the farther out (away from the other
                    // jaw); below 0 the two cross, clamped shut.
                    bool first = part == Part.JawA;
                    float rx = first ? data.RootAX : data.RootBX, ry = first ? data.RootAY : data.RootBY;
                    float tx = (first ? data.TipAX : data.TipBX) - rx, ty = (first ? data.TipAY : data.TipBY) - ry;
                    float reach = System.Math.Max(1, tx * tx + ty * ty);
                    float wx = first ? data.AwayAX : data.AwayBX, wy = first ? data.AwayAY : data.AwayBY;
                    float spread = p.Open / 40f * 9;
                    for (int y = 0; y < 64; y++)
                        for (int x = 0; x < 64; x++)
                        {
                            if (data.Parts[x, y] != part) continue;
                            float along = System.Math.Max(0, System.Math.Min(1, ((x - rx) * tx + (y - ry) * ty) / reach));
                            float push = spread * along * along;
                            var c = image[x, y];
                            if (phase >= 3 && along > 0.45f && c.R > 60) c = c.Mix(Blood, 0.45f);
                            Put(x + dx + wx * push, y + dy + wy * push, c);
                            // Bent far, a pixel spans two, so no gap opens in the jaw.
                            if (System.Math.Abs(push) > 2) Put(x + dx + wx * push * 0.9f, y + dy + wy * push * 0.9f, c);
                        }
                    continue;
                }
                bool swells = part == Part.Abdomen && p.Swell != 0;
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        if (data.Parts[x, y] != part) continue;
                        float px = x, py = y;
                        if (swells)
                        {
                            px = swellX + (x - swellX) * (1 + p.Swell);
                            py = swellY + (y - swellY) * (1 + p.Swell);
                        }
                        var c = image[x, y];
                        Put(px + dx, py + dy, c);
                        if (swells && p.Swell > 0.04f) Put(px + dx + 1, py + dy, c);
                    }
            }
            // The abdomen pushed back or the head thrust ahead opens a narrow gap across the body where they met
            // (2026-11-27, the sand showed through between the thorax and the abdomen, seen from behind): within where
            // the body stood at rest, a see-through slit with body both sides is closed in shade.
            var rest = new bool[canvas.Width, canvas.Height];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    var part = data.Parts[x, y];
                    if (part != Part.Abdomen && part != Part.Thorax && part != Part.Head) continue;
                    int cx = R(x) + Margin + shake, cy = R(ground - (ground - (y - lift + Headroom)) * stretch);
                    for (int oy = -1; oy <= 1; oy++)
                        if (canvas.Inside(cx, cy + oy)) rest[cx, cy + oy] = true;
                }
            canvas.CloseGaps(rest, new Rgba(40, 26, 16), 6);
            var headAt = Offset(Part.Head);
            joints.HeadX = (data.RootAX + data.RootBX) / 2 + headAt.X + Margin;
            joints.HeadY = (data.RootAY + data.RootBY) / 2 + headAt.Y + Headroom;

            // Sunk into the sand: everything below the sand line is gone, a crater rim round where it went down.
            var result = new PixelImage(Width, Height);
            int sandLine = ground - R(p.Sink);
            float middle = data.MiddleX + Margin + shake;
            // The rim shows once it is well in, and widens as it goes deeper.
            bool rim = p.Sink >= 6;
            float half = data.HalfWidth * System.Math.Min(1, 0.45f + p.Sink / 40);
            if (rim) Rim(result, middle, sandLine, half, false);
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var c = canvas[x, y];
                    if (c.A == 0) continue;
                    if (p.Sink > 0.5f && y > sandLine) continue;
                    result[x, y] = c;
                    joints.Top = System.Math.Min(joints.Top, y);
                    joints.Bottom = System.Math.Max(joints.Bottom, y);
                    joints.Left = System.Math.Min(joints.Left, x);
                    joints.Right = System.Math.Max(joints.Right, x);
                }
            if (rim) Rim(result, middle, sandLine, half, true);
            if (p.Sand > 0.05f) Sand(result, joints.HeadX, joints.HeadY, ax, ay, p.Sand, frame);
            return result;
        }

        // The crater's rim: the far half behind the body, the near half over where the body goes into the sand.
        static void Rim(PixelImage image, float middle, int line, float half, bool near)
        {
            float rx = half + 4, ry = 5 + half * 0.22f;
            for (int y = (int)(line - ry - 2); y <= line + ry + 2; y++)
                for (int x = (int)(middle - rx - 2); x <= middle + rx + 2; x++)
                {
                    if (!image.Inside(x, y)) continue;
                    if (near != (y >= line)) continue;
                    float u = (x - middle) / rx, v = (y - line) / ry;
                    float d = u * u + v * v;
                    if (d > 1) continue;
                    // A ring of heaped sand, lit on top, a dark hollow inside it.
                    if (d < 0.45f)
                    {
                        // The hollow: dark behind the body, and in front of it where nothing of the body shows.
                        if (!near || image[x, y].A == 0) image[x, y] = d < 0.2f ? SandDark.Scale(0.75f) : SandDark;
                        continue;
                    }
                    var c = d > 0.86f ? SandShade : v < -0.2f ? SandLight : SandMain;
                    if (near && v > 0.55f && d > 0.8f) c = SandShade.Mix(SandDark, 0.4f);
                    image[x, y] = c;
                }
        }

        // Sand thrown ahead and up from its head: grains in a fan, more for a bigger throw.
        static void Sand(PixelImage image, float hx, float hy, float ax, float ay, float amount, int frame)
        {
            int grains = (int)(26 * amount);
            for (int i = 0; i < grains; i++)
            {
                float r1 = PixelNoise.Value(i, frame, 71), r2 = PixelNoise.Value(i, frame, 72);
                float spread = (r1 - 0.5f) * 1.6f;
                float c = (float)System.Math.Cos(spread), s = (float)System.Math.Sin(spread);
                float dx = ax * c - ay * s, dy = ax * s + ay * c - 0.7f;
                float far = 4 + 16 * r2 * amount;
                int x = R(hx + dx * far), y = R(hy + dy * far);
                if (!image.Inside(x, y) || image[x, y].A > 0) continue;
                image[x, y] = r2 > 0.6f ? SandLight : SandMain;
                if (r2 > 0.8f && image.Inside(x + 1, y) && image[x + 1, y].A == 0) image[x + 1, y] = SandShade;
            }
        }
    }
}
