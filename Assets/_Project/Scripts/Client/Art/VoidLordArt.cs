using System.Collections.Generic;

namespace Game.Client
{
    // What the void lord is doing, which picks its frame (WorldView.VoidLord.cs, Docs/Bosses.md 공허의 군주). A dark
    // emperor hovering over the floor, a crown of ice, a cape of night that frays into the void below him, a black orb
    // held up in one hand. Every attack has its own wind-up of the whole body: the empty hand drawn back, then raked
    // ahead to grasp (Reach, Grasp); the orb held out swelling with light, then the rift let loose and the recoil
    // (Gather, Cast); both arms raised high, then thrown down to call the stars (Exalt, Starfall); arms and cape spread
    // wide and trembling, then all of it let go at once (Spread, Burst); dissolving into the void, then gone and come
    // back (Fade, Blink); the empty hand raised and clenching, then torn down (Clench, Crush); the orb raised overhead,
    // then brought down onto the floor (Raise, Smite); a roar with everything spread.
    enum VoidLordPose
    {
        Idle,
        Walk,
        Reach,
        Grasp,
        Gather,
        Cast,
        Exalt,
        Starfall,
        Spread,
        Burst,
        Fade,
        Blink,
        Clench,
        Crush,
        Raise,
        Smite,
        Roar,
        // Its entrance: a rift opening in the floor of the altar, the crown rising out of it, the eyes lit, the whole of
        // him drawn up out of it with the cape spread, a roar, and down to hover.
        Enter,
    }

    // The void lord of the abyss (2026-11-28): one picture of the local image models (Z-Image-Turbo with a pixel-art
    // LoRA, turned eight ways by Qwen-Image-Edit with the multiple-angles LoRA, Docs/ArtSources.md) cut to five views;
    // every pose is made in code: the arms moved apart from the body (each bending from its shoulder), the cape's hem
    // spread, swaying and fraying into motes of the void, the whole of him leaning, hovering higher or lower, squashed,
    // dissolving and sunk into a rift; the orb, the stars round his crown and the light of his eyes and crown are drawn
    // in code. Faces right.
    static partial class VoidLordArt
    {
        public const int Width = 96;
        public const int Height = 100;
        // Rows under him; WorldView stands the picture on this row (Pivot).
        public const int Floor = 4;
        // How tall the body itself stands, for the name and marks over it.
        public const int BodyHeight = 66;

        // Room round the picture for the arms raised and thrown out, and how high he hovers over the floor at rest.
        const int Margin = 16;
        const int Headroom = 26;
        const int Hover = 6;

        enum Part : byte
        {
            None,
            Crown,
            Head,
            Eye,
            Body,
            Arm,
            Off,
            Hem,
        }

        // What it knows about a view: its colours and parts, which way it faces on the screen, the rows of the picture's
        // top, bottom and where the hem begins, the middle, the shoulders of the orb arm and the empty arm, the hand that
        // holds the orb, how long each arm is, the middle of the crown, and whether the orb is behind him.
        sealed class ViewData
        {
            public PixelImage Image;
            public Part[,] Parts;
            public float AheadX, AheadY;
            public int Top, Bottom, HemTop;
            public float MiddleX, HalfWidth;
            public float ShoulderX, ShoulderY, OffShoulderX, OffShoulderY, HandX, HandY;
            public float ArmLength, OffLength;
            public float CrownX, CrownY, HeadX, HeadY;
            public bool OrbBehind;
            // Whether the empty arm shows in this view (where it does not, the orb arm does what it would).
            public bool HasOff;
            // Which side of him the arms are on (+1 right, -1 left), for throwing them out.
            public float ArmSide, OffSide;
        }

        static readonly Dictionary<BossView, ViewData> Views = new Dictionary<BossView, ViewData>();

        static Part PartOf(char c) => c switch
        {
            'K' => Part.Crown,
            'H' => Part.Head,
            'E' => Part.Eye,
            'B' => Part.Body,
            'A' => Part.Arm,
            'L' => Part.Off,
            'T' => Part.Hem,
            _ => Part.None,
        };

        static ViewData ViewOf(BossView view)
        {
            if (Views.TryGetValue(view, out var known)) return known;
            var (rows, parts, rig) = view switch
            {
                BossView.Front => (FrontRows, FrontParts, FrontRig),
                BossView.FrontSide => (FrontSideRows, FrontSideParts, FrontSideRig),
                BossView.Side => (SideRows, SideParts, SideRig),
                BossView.BackSide => (BackSideRows, BackSideParts, BackSideRig),
                _ => (BackRows, BackParts, BackRig),
            };
            var data = new ViewData { Image = PixelImage.FromRows(Palette, rows), Parts = new Part[64, 64] };
            int top = 64, bottom = -1, hemTop = 64, left = 64, right = -1;
            float crownX = 0, crownY = 0, headX = 0, headY = 0;
            int crowns = 0, heads = 0;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    var part = PartOf(parts[y][x]);
                    data.Parts[x, y] = part;
                    if (part == Part.None) continue;
                    top = System.Math.Min(top, y);
                    bottom = System.Math.Max(bottom, y);
                    if (part == Part.Hem) hemTop = System.Math.Min(hemTop, y);
                    if (part == Part.Body || part == Part.Hem)
                    {
                        left = System.Math.Min(left, x);
                        right = System.Math.Max(right, x);
                    }
                    if (part == Part.Crown)
                    {
                        crownX += x;
                        crownY += y;
                        crowns++;
                    }
                    if (part == Part.Head || part == Part.Eye)
                    {
                        headX += x;
                        headY += y;
                        heads++;
                    }
                }
            data.Top = top;
            data.Bottom = bottom;
            data.HemTop = System.Math.Min(hemTop, bottom - 1);
            data.MiddleX = (left + right) / 2f;
            data.HalfWidth = (right - left) / 2f + 2;
            data.HeadX = heads > 0 ? headX / heads : 32;
            data.HeadY = heads > 0 ? headY / heads : top + 8;
            data.CrownX = crowns > 0 ? crownX / crowns : data.HeadX;
            data.CrownY = crowns > 0 ? crownY / crowns : top + 2;
            (data.ShoulderX, data.ShoulderY, data.OffShoulderX, data.OffShoulderY, data.HandX, data.HandY, data.OrbBehind) = rig;
            float armLength = 1, offLength = 1;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    if (data.Parts[x, y] == Part.Arm) armLength = System.Math.Max(armLength, Distance(x, y, data.ShoulderX, data.ShoulderY));
                    if (data.Parts[x, y] == Part.Off)
                    {
                        offLength = System.Math.Max(offLength, Distance(x, y, data.OffShoulderX, data.OffShoulderY));
                        data.HasOff = true;
                    }
                }
            data.ArmLength = armLength;
            data.OffLength = offLength;
            data.ArmSide = data.ShoulderX >= data.MiddleX ? 1 : -1;
            data.OffSide = data.OffShoulderX >= data.MiddleX ? 1 : -1;
            if (System.Math.Abs(data.OffShoulderX - data.ShoulderX) < 2) data.OffSide = -data.ArmSide;
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

        static float Distance(float x, float y, float x2, float y2) => (float)System.Math.Sqrt((x - x2) * (x - x2) + (y - y2) * (y - y2));

        // ---- A pose: hovering higher (pixels), leaning ahead (pixels at the crown); the orb arm raised, thrust ahead
        // and thrown out; the empty arm likewise; the hem spread (share of its width) and fraying into the void; the orb's
        // size (1 at rest) and how bright his eyes and crown burn (0 to 1); dissolved (0 to 1); sunk into a rift
        // (pixels); shaking; squashed (below 0) or stretched; frost about the orb. ----

        struct Pose
        {
            public float Lift, Lean, ArmUp, ArmAhead, ArmOut, OffUp, OffAhead, OffOut, Spread, Fray, Orb, Glow, Fade, Sink, Shake, Squash, Frost;
        }

        static Pose P(float lift = 0, float lean = 0, float armUp = 0, float armAhead = 0, float armOut = 0, float offUp = 0, float offAhead = 0,
            float offOut = 0, float spread = 0, float fray = 0, float orb = 1, float glow = 0.3f, float fade = 0, float sink = 0, float shake = 0,
            float squash = 0, float frost = 0) =>
            new Pose
            {
                Lift = lift, Lean = lean, ArmUp = armUp, ArmAhead = armAhead, ArmOut = armOut, OffUp = offUp, OffAhead = offAhead, OffOut = offOut,
                Spread = spread, Fray = fray, Orb = orb, Glow = glow, Fade = fade, Sink = sink, Shake = shake, Squash = squash, Frost = frost,
            };

        static readonly Pose Rest = P();

        static Pose Lerp(Pose a, Pose b, float u)
        {
            float L(float x, float y) => x + (y - x) * u;
            return P(L(a.Lift, b.Lift), L(a.Lean, b.Lean), L(a.ArmUp, b.ArmUp), L(a.ArmAhead, b.ArmAhead), L(a.ArmOut, b.ArmOut), L(a.OffUp, b.OffUp),
                L(a.OffAhead, b.OffAhead), L(a.OffOut, b.OffOut), L(a.Spread, b.Spread), L(a.Fray, b.Fray), L(a.Orb, b.Orb), L(a.Glow, b.Glow),
                L(a.Fade, b.Fade), L(a.Sink, b.Sink), L(a.Shake, b.Shake), L(a.Squash, b.Squash), L(a.Frost, b.Frost));
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
        // strike, the rebound, held (stooped and open: the opening to hit back), pulled back, back to rest; and the slow
        // parts of the entrance.
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

        // Leaning back, the empty hand drawn back and up behind him.
        static readonly Pose Reached = P(lean: -2, armUp: 1, offUp: 4, offAhead: -4, offOut: 1, fray: 0.3f, glow: 0.6f);
        // The orb held out ahead, swelling, frost drawn into it.
        static readonly Pose Gathered = P(lean: -1, armUp: 1, armAhead: 5, orb: 2.1f, glow: 0.9f, frost: 1);
        // Risen higher, both arms raised as high as they go, the orb up with them.
        static readonly Pose Exalted = P(lift: 5, armUp: 10, armAhead: -1, offUp: 8, spread: 0.15f, orb: 1.6f, glow: 1, frost: 0.6f);
        // Arms and cape spread wide, trembling, the hem fraying.
        static readonly Pose Spreaded = P(lift: 3, armUp: 2, armOut: 4, offUp: 2, offOut: 4, spread: 0.35f, fray: 0.8f, orb: 1.4f, glow: 0.9f, shake: 0.4f);
        // Half gone into the void, leaning on, the hem all motes.
        static readonly Pose Faded = P(lift: 2, lean: 2, fray: 1, glow: 0.8f, fade: 0.7f);
        // The empty hand raised ahead and clenching, hunched, the orb drawn small and dark.
        static readonly Pose Clenched = P(lean: 1, offUp: 5, offAhead: 5, squash: -0.04f, orb: 0.7f, glow: 1, shake: 0.3f);
        // The orb raised high over his crown, swelling.
        static readonly Pose Raised = P(lift: 4, armUp: 12, armAhead: 1, orb: 1.8f, glow: 1, squash: 0.04f, frost: 1);
        static readonly Pose RoarTop = P(lift: 4, armUp: 6, armOut: 3, offUp: 6, offOut: 3, spread: 0.3f, fray: 0.6f, orb: 1.5f, glow: 1, shake: 0.6f);

        // The empty hand raked ahead and shut.
        static readonly (Pose[] Frames, Stage[] Stages) GraspBlow = Expand(Reached,
            (Stage.Swing, 2, P(lean: 3, offUp: 1, offAhead: 8, glow: 0.8f)),
            (Stage.Strike, 1, P(lean: 4, offAhead: 10, glow: 1, fray: 0.4f)),
            (Stage.Rebound, 1, P(lean: 3, offAhead: 9, glow: 0.8f)),
            (Stage.Held, 2, P(lean: 3, offAhead: 8, glow: 0.6f)),
            (Stage.Pull, 2, P(lean: 1, offAhead: 3)),
            (Stage.Recover, 6, Rest));

        // The orb let loose: a flash out of it and the recoil throwing him back.
        static readonly (Pose[] Frames, Stage[] Stages) CastBlow = Expand(Gathered,
            (Stage.Swing, 1, P(armUp: 1, armAhead: 6, orb: 2.3f, glow: 1, frost: 1)),
            (Stage.Strike, 1, P(lean: -3, armUp: 2, armAhead: 5, orb: 1.2f, glow: 1, frost: 1.5f)),
            (Stage.Strike, 1, P(lean: -3, armUp: 2, armAhead: 5, orb: 1.1f, glow: 1, frost: 1)),
            (Stage.Rebound, 3, P(lean: -2, armUp: 1, armAhead: 4, glow: 0.7f)),
            (Stage.Held, 2, P(lean: -1, armAhead: 3, glow: 0.5f)),
            (Stage.Recover, 8, Rest));

        // The arms thrown down and ahead out of their height: the stars called down.
        static readonly (Pose[] Frames, Stage[] Stages) StarfallBlow = Expand(Exalted,
            (Stage.Swing, 2, P(lift: 5, armUp: 8, armAhead: 2, offUp: 7, offAhead: 2, orb: 1.7f, glow: 1, frost: 0.8f)),
            (Stage.Strike, 1, P(lift: 2, lean: 2, armUp: 3, armAhead: 4, offUp: 2, offAhead: 4, orb: 1.2f, glow: 1, frost: 1.5f)),
            (Stage.Rebound, 2, P(lift: 2, lean: 2, armUp: 2, armAhead: 3, offUp: 1, offAhead: 3, glow: 0.8f)),
            (Stage.Held, 3, P(lift: 1, lean: 2, armAhead: 3, offAhead: 3, glow: 0.6f)),
            (Stage.Recover, 8, Rest));

        // All of it let go at once: the cape thrown wide, the hem burst into motes.
        static readonly (Pose[] Frames, Stage[] Stages) BurstBlow = Expand(Spreaded,
            (Stage.Swing, 2, P(lift: 4, armUp: 3, armOut: 4, offUp: 3, offOut: 4, spread: 0.45f, fray: 1, orb: 1.6f, glow: 1)),
            (Stage.Strike, 1, P(lift: 4, armUp: 4, armOut: 5, offUp: 4, offOut: 5, spread: 0.6f, fray: 1.4f, orb: 2, glow: 1, squash: 0.05f, frost: 2)),
            (Stage.Rebound, 3, P(lift: 3, armUp: 2, armOut: 3, offUp: 2, offOut: 3, spread: 0.3f, fray: 0.8f, orb: 1.3f, glow: 0.8f, frost: 1)),
            (Stage.Held, 2, P(lift: 2, armOut: 2, offOut: 2, spread: 0.15f, fray: 0.4f, glow: 0.6f)),
            (Stage.Recover, 8, Rest));

        // Gone into the void and back: wholly dissolved, then put together again further on.
        static readonly (Pose[] Frames, Stage[] Stages) BlinkBlow = Expand(Faded,
            (Stage.Swing, 2, P(lift: 2, lean: 3, fray: 1.5f, glow: 0.9f, fade: 1)),
            (Stage.Strike, 1, P(lift: 2, lean: 4, fray: 1.5f, glow: 1, fade: 1)),
            (Stage.Rebound, 2, P(lift: 2, lean: 3, fray: 1.2f, glow: 1, fade: 0.55f)),
            (Stage.Held, 2, P(lift: 1, lean: 2, fray: 0.6f, glow: 0.8f, fade: 0.2f)),
            (Stage.Recover, 6, Rest));

        // The clenched hand torn down: whatever it held collapses.
        static readonly (Pose[] Frames, Stage[] Stages) CrushBlow = Expand(Clenched,
            (Stage.Swing, 2, P(lean: 2, offUp: 1, offAhead: 7, squash: -0.02f, orb: 0.7f, glow: 1)),
            (Stage.Strike, 1, P(lift: -2, lean: 3, offUp: -3, offAhead: 6, squash: -0.07f, orb: 0.8f, glow: 1, shake: 0.5f)),
            (Stage.Rebound, 1, P(lift: -1, lean: 3, offUp: -2, offAhead: 6, squash: -0.05f, glow: 0.8f)),
            (Stage.Held, 3, P(lift: -1, lean: 3, offUp: -2, offAhead: 5, squash: -0.05f, glow: 0.6f)),
            (Stage.Pull, 2, P(lean: 1, offAhead: 2, squash: -0.02f)),
            (Stage.Recover, 6, Rest));

        // The orb brought down out of its height onto the floor ahead, held there stooped.
        static readonly (Pose[] Frames, Stage[] Stages) SmiteBlow = Expand(Raised,
            (Stage.Swing, 3, P(lift: 2, lean: 1, armUp: 5, armAhead: 6, orb: 1.8f, glow: 1, frost: 1)),
            (Stage.Strike, 1, P(lift: -3, lean: 3, armUp: -4, armAhead: 8, orb: 1.6f, glow: 1, squash: -0.08f, frost: 1.6f)),
            (Stage.Rebound, 1, P(lift: -2, lean: 3, armUp: -3, armAhead: 7, orb: 1.3f, glow: 0.8f, squash: -0.06f)),
            (Stage.Held, 3, P(lift: -2, lean: 3, armUp: -3, armAhead: 7, orb: 1.1f, glow: 0.6f, squash: -0.05f)),
            (Stage.Pull, 2, P(lean: 1, armUp: 0, armAhead: 3, squash: -0.02f)),
            (Stage.Recover, 8, Rest));

        static (Pose[] Frames, Stage[] Stages)? BlowOf(VoidLordPose pose) => pose switch
        {
            VoidLordPose.Grasp => GraspBlow,
            VoidLordPose.Cast => CastBlow,
            VoidLordPose.Starfall => StarfallBlow,
            VoidLordPose.Burst => BurstBlow,
            VoidLordPose.Blink => BlinkBlow,
            VoidLordPose.Crush => CrushBlow,
            VoidLordPose.Smite => SmiteBlow,
            VoidLordPose.Enter => (EnterFrames, EnterStages),
            _ => null,
        };

        // ---- The entrance: a rift opening in the altar's floor, his crown rising out of it, the eyes lit, the whole
        // of him drawn up out of it with the cape spread, a roar, and down to hover. ----

        static readonly Pose Under = P(sink: 74, glow: 0, orb: 0);
        static readonly (Pose[] Frames, Stage[] Stages) Entrance = Expand(Under,
            (Stage.Slow, 8, P(sink: 72, glow: 0, orb: 0, shake: 0.3f)),
            (Stage.Slow, 6, P(sink: 60, glow: 0, orb: 0)),
            (Stage.Slow, 6, P(sink: 50, glow: 0.1f, orb: 0)),
            (Stage.Slow, 4, P(sink: 48, glow: 1, orb: 0)),
            (Stage.Slow, 4, P(sink: 46, glow: 0.8f, orb: 0.5f, shake: 0.5f)),
            (Stage.Swing, 9, P(sink: 3, lift: 4, armUp: 2, armOut: 2, offUp: 2, offOut: 2, spread: 0.3f, glow: 1, orb: 1.3f)),
            (Stage.Strike, 1, P(sink: 0, lift: 6, armUp: 4, armOut: 3, offUp: 4, offOut: 3, spread: 0.4f, fray: 1, glow: 1, orb: 1.6f, frost: 1.5f)),
            (Stage.Rebound, 3, P(lift: 5, armUp: 4, armOut: 3, offUp: 4, offOut: 3, spread: 0.35f, fray: 0.8f, glow: 1, orb: 1.4f)),
            (Stage.Slow, 8, RoarTop),
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

        // The frames of the entrance: the crown out of the rift, the eyes lit, drawn up out of it, the roar.
        public static int EnterCrown => 1 + 8 + 6;
        public static int EnterEyes => EnterCrown + 6 + 1;
        public static int EnterBurst => 1 + System.Array.IndexOf(Entrance.Stages, Stage.Strike);
        public static int EnterRoar => EnterBurst + 4;
        public static int RoarRise => 12;

        // The frame of the entrance a share of the way through it (a return visit plays only the rise on).
        public static int EnterFrameAt(float progress, bool again)
        {
            int from = again ? EnterBurst - 5 : 0;
            int to = EnterFrames.Length - 1;
            return System.Math.Max(from, System.Math.Min(to, from + (int)(progress * (to - from + 1))));
        }

        static Stage[] StagesOf(VoidLordPose pose) => BlowOf(pose)?.Stages;

        public static int FramesOf(VoidLordPose pose) => pose switch
        {
            VoidLordPose.Idle => 12,
            VoidLordPose.Walk => 8,
            VoidLordPose.Reach => 12,
            VoidLordPose.Gather => 12,
            VoidLordPose.Exalt => 14,
            VoidLordPose.Spread => 12,
            VoidLordPose.Fade => 12,
            VoidLordPose.Clench => 12,
            VoidLordPose.Raise => 14,
            VoidLordPose.Roar => RoarRise + 4,
            _ => BlowOf(pose)?.Frames.Length ?? 4,
        };

        // How long a blow's frame is shown (seconds); 0 for anything else.
        public static float FrameSeconds(VoidLordPose pose, int frame)
        {
            var stages = StagesOf(pose);
            return stages == null ? 0 : StageSeconds(stages[System.Math.Max(0, System.Math.Min(stages.Length - 1, frame))]);
        }

        public static bool Swinging(VoidLordPose pose, int frame)
        {
            var stages = StagesOf(pose);
            return stages != null && frame < stages.Length && (stages[frame] == Stage.Swing || stages[frame] == Stage.Strike);
        }

        public static int StrikeFrame(VoidLordPose pose) => System.Array.IndexOf(StagesOf(pose) ?? new Stage[0], Stage.Strike);

        // The wind-up that comes before each blow.
        public static VoidLordPose WindUpOf(VoidLordPose blow) => blow switch
        {
            VoidLordPose.Grasp => VoidLordPose.Reach,
            VoidLordPose.Cast => VoidLordPose.Gather,
            VoidLordPose.Starfall => VoidLordPose.Exalt,
            VoidLordPose.Burst => VoidLordPose.Spread,
            VoidLordPose.Blink => VoidLordPose.Fade,
            VoidLordPose.Crush => VoidLordPose.Clench,
            VoidLordPose.Smite => VoidLordPose.Raise,
            _ => VoidLordPose.Reach,
        };

        static Pose PoseOf(VoidLordPose pose, int frame)
        {
            int count = FramesOf(pose);
            int f = ((frame % count) + count) % count;
            float t = count > 1 ? f / (float)(count - 1) : 0;
            float cycle = f / (float)count * 2 * (float)System.Math.PI;
            float s = (float)System.Math.Sin(cycle), c = (float)System.Math.Cos(cycle);
            switch (pose)
            {
                case VoidLordPose.Idle:
                    // Hovering, rising and falling, the orb hand bobbing a little after him.
                    return P(lift: 1.4f * s, armUp: 0.6f * c, offUp: 0.4f * s, orb: 1 + 0.08f * c, fray: 0.15f);
                case VoidLordPose.Walk:
                    // Gliding: leaning into it, the hem streaming behind.
                    return P(lift: 1 + 0.8f * s, lean: 2, armUp: 0.5f * c, fray: 0.4f);
                case VoidLordPose.Reach:
                    return Keys(t, (0, Rest), (0.45f, P(lean: -1, offUp: 2, offAhead: -2, glow: 0.5f)), (1, Reached));
                case VoidLordPose.Gather:
                    return Keys(t, (0, Rest), (0.45f, P(armAhead: 3, armUp: 1, orb: 1.5f, glow: 0.6f, frost: 0.6f)), (1, Gathered));
                case VoidLordPose.Exalt:
                    return Keys(t, (0, Rest), (0.4f, P(lift: 2, armUp: 5, offUp: 4, orb: 1.3f, glow: 0.7f)), (1, Exalted));
                case VoidLordPose.Spread:
                {
                    var shaking = Spreaded;
                    shaking.Spread += 0.04f * s;
                    return Keys(t, (0, Rest), (0.4f, P(lift: 1, armOut: 2, offOut: 2, spread: 0.15f, fray: 0.4f, glow: 0.6f)), (1, shaking));
                }
                case VoidLordPose.Fade:
                    return Keys(t, (0, Rest), (0.45f, P(lift: 1, lean: 1, fray: 0.6f, glow: 0.6f, fade: 0.25f)), (1, Faded));
                case VoidLordPose.Clench:
                    return Keys(t, (0, Rest), (0.45f, P(offUp: 3, offAhead: 3, orb: 0.85f, glow: 0.7f)), (1, Clenched));
                case VoidLordPose.Raise:
                    return Keys(t, (0, Rest), (0.4f, P(lift: 2, armUp: 6, orb: 1.4f, glow: 0.7f)), (1, Raised));
                case VoidLordPose.Roar:
                    if (f < RoarRise) return Keys(f / (float)RoarRise, (0, Rest), (0.5f, P(lift: 2, armUp: 3, offUp: 3, spread: 0.15f, glow: 0.7f)), (1, RoarTop));
                    return P(lift: 4 + s, armUp: 6, armOut: 3, offUp: 6, offOut: 3, spread: 0.3f + 0.04f * c, fray: 0.6f, orb: 1.5f, glow: 1, shake: 1);
                default:
                {
                    var blow = BlowOf(pose);
                    return blow == null ? Rest : blow.Value.Frames[f];
                }
            }
        }

        // ---- Drawing. ----

        static int R(float v) => (int)System.Math.Round(v);

        static readonly Rgba Underlay = new Rgba(20, 14, 36);
        static readonly Rgba VoidDeep = new Rgba(14, 6, 28);
        static readonly Rgba VoidDark = new Rgba(52, 26, 98);
        static readonly Rgba VoidMain = new Rgba(120, 70, 214);
        static readonly Rgba VoidLight = new Rgba(196, 156, 255);
        static readonly Rgba Frost = new Rgba(150, 226, 250);
        static readonly Rgba FrostHot = new Rgba(236, 252, 255);
        static readonly Rgba EyeDim = new Rgba(70, 130, 160);
        static readonly Rgba EyeLit = new Rgba(160, 236, 255);
        // From the second phase the orb's ring of light never goes out and the hem frays more; in the last phase his
        // eyes, crown and orb burn violet-white and five stars turn round his crown.
        static readonly Rgba Rage = new Rgba(226, 180, 255);

        // One frame. Faces right; WorldView flips it for the left.
        public static PixelImage Draw(BossView view, VoidLordPose pose, int frame, int phase) => Render(view, pose, frame, phase, out _);

        // Where the head and the body are in a frame (picture pixels): for the preview's check that nothing jumps from
        // one frame to the next.
        public struct Joints
        {
            public float HeadX, HeadY;
            public int Top, Bottom, Left, Right;
        }

        public static Joints JointsOf(BossView view, VoidLordPose pose, int frame)
        {
            Render(view, pose, frame, 1, out var joints);
            return joints;
        }

        static float Smooth(float u)
        {
            u = System.Math.Max(0, System.Math.Min(1, u));
            return u * u * (3 - 2 * u);
        }

        static PixelImage Render(BossView view, VoidLordPose pose, int frame, int phase, out Joints joints)
        {
            var data = ViewOf(view);
            var p = PoseOf(pose, frame);
            var image = data.Image;
            var canvas = new PixelImage(Width, Height);
            joints = new Joints { Top = int.MaxValue, Left = int.MaxValue, Bottom = -1, Right = -1 };
            float ax = data.AheadX, ay = data.AheadY * 0.55f;
            int shake = p.Shake > 0 ? ((frame & 1) == 0 ? 1 : -1) * R(p.Shake) : 0;
            float stretch = 1 + p.Squash;
            // The floor's row on the canvas, and where the picture's bottom row hangs over it.
            int floor = Height - Floor;
            float drop = floor - Hover - (data.Bottom + 1) - p.Lift + p.Sink;
            float height = System.Math.Max(1, data.Bottom - data.Top);
            float fray = p.Fray + (phase >= 2 ? 0.15f : 0) + (phase >= 3 ? 0.15f : 0);
            // Lean: the rows shifted ahead the more the higher they are (the hem stays behind).
            (float X, float Y) LeanAt(float y)
            {
                float h = System.Math.Max(0, System.Math.Min(1, (data.Bottom - y) / height));
                return (p.Lean * ax * h, p.Lean * ay * h * 0.6f);
            }
            int bottomRow = R(floor - (floor - (data.Bottom + drop)) * stretch);
            void Put(float x, float y, Rgba c)
            {
                int px = R(x) + Margin + shake, py = R(floor - (floor - (y + drop)) * stretch);
                if (canvas.Inside(px, py)) canvas[px, py] = c;
            }
            // Motes let go of the hem, laid after the body is measured (they drift free of it).
            var motes = new List<(int X, int Y, Rgba C)>();
            // A pixel and the rows down to where the row below it lands, so leaning and stretching open no seams.
            void PutDown(float x, float y, float below, Rgba c)
            {
                int px = R(x) + Margin + shake, py = R(floor - (floor - (y + drop)) * stretch), to = R(floor - (floor - (below + drop)) * stretch);
                for (int row = py; row < System.Math.Max(py + 1, to); row++)
                    if (canvas.Inside(px, row)) canvas[px, row] = c;
            }
            bool Dissolved(int x, int y) => p.Fade > 0 && PixelNoise.Value(x, y, 91) < p.Fade;
            bool Edge(int x, int y) => p.Fade > 0 && PixelNoise.Value(x, y, 91) < p.Fade + 0.14f;

            // An arm's pixel moved from its shoulder: the further out along it, the more it follows the hand.
            (float X, float Y) ArmShift(float x, float y, float sx, float sy, float length, float up, float ahead, float outward, float side)
            {
                float w = Smooth(Distance(x, y, sx, sy) / length * 1.15f);
                return ((ahead * ax + outward * side) * w, (ahead * ay - up) * w);
            }

            // Where the empty arm is hidden (the side and back views), the orb arm grasps and clenches for it.
            if (!data.HasOff)
            {
                p.ArmUp += p.OffUp;
                p.ArmAhead += p.OffAhead;
                p.ArmOut += p.OffOut;
            }
            // The orb's middle and size, the stars' ring round the crown.
            var hand = ArmShift(data.HandX, data.HandY, data.ShoulderX, data.ShoulderY, data.ArmLength, p.ArmUp, p.ArmAhead, p.ArmOut, data.ArmSide);
            var handLean = LeanAt(data.HandY);
            float orbX = data.HandX + hand.X + handLean.X, orbY = data.HandY + hand.Y + handLean.Y - 2;
            float orbR = p.Orb <= 0.05f ? 0 : 1.9f + 1.6f * p.Orb + (phase >= 3 ? 0.4f : 0);
            var crownLean = LeanAt(data.CrownY);
            float starX = data.CrownX + crownLean.X, starY = data.CrownY + crownLean.Y - 1;
            int stars = phase >= 3 ? 5 : 3;
            float starTurn = frame * (phase >= 3 ? 0.5f : 0.3f);
            float glow = System.Math.Max(0, System.Math.Min(1, p.Glow + (phase >= 3 ? 0.25f : phase >= 2 ? 0.12f : 0)));

            if (data.OrbBehind) Orb(canvas, orbX, orbY, orbR, p, frame, phase, Put);
            Stars(false);

            // Where an arm lay over the body, the body under it (dark) so moving it opens no hole.
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    var part = data.Parts[x, y];
                    if (part != Part.Arm && part != Part.Off) continue;
                    bool left = false, right = false;
                    for (int k = 1; k <= 6; k++)
                    {
                        if (x - k >= 0 && (data.Parts[x - k, y] == Part.Body || data.Parts[x - k, y] == Part.Hem)) left = true;
                        if (x + k < 64 && (data.Parts[x + k, y] == Part.Body || data.Parts[x + k, y] == Part.Hem)) right = true;
                    }
                    // The empty arm hangs at the cape's edge: its place is the cape's wherever there is cape beside it.
                    if (part == Part.Off ? !left && !right : !left || !right) continue;
                    var lean = LeanAt(y);
                    if (!Dissolved(x, y)) Put(x + lean.X, y + lean.Y, Underlay);
                }

            // The body, then the hem below it, the arms over them, the head and crown on top.
            float hemRows = System.Math.Max(1, data.Bottom - data.HemTop);
            foreach (var layer in new[] { Part.Hem, Part.Body, Part.Off, Part.Arm, Part.Head, Part.Crown, Part.Eye })
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        var part = data.Parts[x, y];
                        if (part != layer) continue;
                        if (Dissolved(x, y)) continue;
                        var c = image[x, y];
                        if (Edge(x, y)) c = VoidLight;
                        var lean = LeanAt(y);
                        float px = x + lean.X, py = y + lean.Y;
                        // How far a spread hem's pixel is pushed out from its neighbour toward the middle (filled).
                        float spreadFill = 0;
                        switch (part)
                        {
                            case Part.Hem:
                            {
                                // Spread out the further down, swaying, the last rows fraying into motes.
                                float v = (y - data.HemTop) / hemRows;
                                float sway = (0.6f + fray) * (float)System.Math.Sin(y * 0.5f + frame * 0.9f + x * 0.15f) * v;
                                px = data.MiddleX + (px - data.MiddleX) * (1 + p.Spread * v) + sway;
                                spreadFill = (x > data.MiddleX ? -1 : 1) * p.Spread * v;
                                int fromBottom = data.Bottom - y;
                                float band = 1.5f + fray * 6;
                                if (fromBottom < band && PixelNoise.Value(x, y + frame / 2 * 7, 92) < (1 - fromBottom / band) * 0.85f)
                                {
                                    // A mote let go, drifting down and away.
                                    float drift = (frame + x * 3) % 4;
                                    motes.Add((R(px + (x > data.MiddleX ? 1 : -1) * drift * 0.5f) + Margin + shake, R(floor - (floor - (py + 1 + drift + drop)) * stretch),
                                        ((x + y) & 1) == 0 ? VoidMain : VoidDark));
                                    continue;
                                }
                                if (v > 0.7f && phase >= 3 && c.R + c.B > 200) c = c.Mix(VoidLight, 0.3f);
                                break;
                            }
                            case Part.Arm:
                            {
                                var a = ArmShift(x, y, data.ShoulderX, data.ShoulderY, data.ArmLength, p.ArmUp, p.ArmAhead, p.ArmOut, data.ArmSide);
                                // Stretched, an arm leaves no gaps: it is put down along the way from its shoulder.
                                Put(px + a.X * 0.5f, py + a.Y * 0.5f, c);
                                Put(px + a.X * 0.75f, py + a.Y * 0.75f, c);
                                px += a.X;
                                py += a.Y;
                                break;
                            }
                            case Part.Off:
                            {
                                var a = ArmShift(x, y, data.OffShoulderX, data.OffShoulderY, data.OffLength, p.OffUp, p.OffAhead, p.OffOut, data.OffSide);
                                Put(px + a.X * 0.5f, py + a.Y * 0.5f, c);
                                Put(px + a.X * 0.75f, py + a.Y * 0.75f, c);
                                px += a.X;
                                py += a.Y;
                                break;
                            }
                            case Part.Crown:
                            {
                                // The ice of the crown lit by his anger; violet in the last phase.
                                float heat = System.Math.Max(0, (glow - 0.45f) * 1.4f);
                                if (c.R + c.G + c.B > 180) c = c.Mix(phase >= 3 ? Rage : FrostHot, heat * 0.6f);
                                break;
                            }
                            case Part.Eye:
                                c = phase >= 3 ? EyeLit.Mix(Rage, 0.6f).Mix(FrostHot, glow * 0.4f) : glow < 0.5f ? EyeDim.Mix(EyeLit, glow * 2) : EyeLit.Mix(FrostHot, (glow - 0.5f) * 2);
                                break;
                        }
                        if (part == Part.Arm || part == Part.Off || y == 63 || data.Parts[x, y + 1] == Part.None) Put(px, py, c);
                        else
                        {
                            PutDown(px, py, y + 1 + LeanAt(y + 1).Y, c);
                            if (System.Math.Abs(spreadFill) > 0.25f) PutDown(px + spreadFill * 1.5f, py, y + 1 + LeanAt(y + 1).Y, c);
                        }
                    }

            // Burning eyes light the helm about them.
            if (glow > 0.7f)
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        if (data.Parts[x, y] != Part.Eye || Dissolved(x, y)) continue;
                        var lean = LeanAt(y);
                        foreach (var (ox, oy) in new[] { (1, 0), (-1, 0) })
                        {
                            int nx = x + ox;
                            if (nx < 0 || nx > 63 || data.Parts[nx, y] != Part.Head) continue;
                            int cx = R(nx + lean.X) + Margin + shake, cy = R(floor - (floor - (y + lean.Y + drop)) * stretch);
                            if (canvas.Inside(cx, cy) && canvas[cx, cy].A > 0) canvas[cx, cy] = canvas[cx, cy].Mix(phase >= 3 ? Rage : EyeLit, 0.45f);
                        }
                    }

            if (!data.OrbBehind) Orb(canvas, orbX, orbY, orbR, p, frame, phase, Put);
            Stars(true);

            // Sunk into a rift: everything under the floor is gone, the rift's far rim behind, its near rim over.
            var result = new PixelImage(Width, Height);
            bool rift = p.Sink >= 3;
            float middle = data.MiddleX + Margin + shake;
            float half = data.HalfWidth * System.Math.Min(1.1f, 0.5f + p.Sink / 60);
            if (rift) RiftRim(result, middle, floor, half, false, frame);
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var c = canvas[x, y];
                    if (c.A == 0) continue;
                    if (rift && y > floor) continue;
                    // Just above the rift he is lit by it.
                    if (rift && y > floor - 3) c = c.Mix(VoidMain, 0.4f);
                    result[x, y] = c;
                    joints.Top = System.Math.Min(joints.Top, y);
                    joints.Bottom = System.Math.Max(joints.Bottom, y);
                    joints.Left = System.Math.Min(joints.Left, x);
                    joints.Right = System.Math.Max(joints.Right, x);
                }
            foreach (var (mx, my, mc) in motes)
                if (result.Inside(mx, my) && result[mx, my].A == 0 && !(rift && my > floor)) result[mx, my] = mc;
            if (rift) RiftRim(result, middle, floor, half, true, frame);
            var headLean = LeanAt(data.HeadY);
            joints.HeadX = data.HeadX + headLean.X + Margin;
            joints.HeadY = R(floor - (floor - (data.HeadY + headLean.Y + drop)) * stretch);
            return result;

            // Stars turning round his crown, behind him on the far side of the ring and over him on the near side.
            void Stars(bool near)
            {
                if (p.Sink > 40) return;
                for (int k = 0; k < stars; k++)
                {
                    float a = starTurn + k * 2 * (float)System.Math.PI / stars;
                    float sin = (float)System.Math.Sin(a);
                    if (near != (sin >= 0)) continue;
                    float sx = starX + 12 * (float)System.Math.Cos(a), sy = starY - 3 + 3.5f * sin;
                    bool big = (k + frame / 3) % stars == 0;
                    var core = phase >= 3 ? Rage : FrostHot;
                    Put(sx, sy, core);
                    if (big)
                        foreach (var (ox, oy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) Put(sx + ox, sy + oy, phase >= 3 ? VoidLight : Frost);
                }
            }
        }

        // The black orb: a dark core with a violet rim and a pale glint, a ring of light turning round it when it burns,
        // frost turning about it while it gathers.
        static void Orb(PixelImage canvas, float cx, float cy, float r, Pose p, int frame, int phase, System.Action<float, float, Rgba> put)
        {
            if (r <= 0) return;
            float glow = System.Math.Max(0, System.Math.Min(1, p.Glow + (phase >= 3 ? 0.25f : phase >= 2 ? 0.12f : 0)));
            int reach = (int)System.Math.Ceiling(r) + 1;
            for (int dy = -reach; dy <= reach; dy++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    float d = (float)System.Math.Sqrt(dx * dx + dy * dy);
                    if (d > r) continue;
                    var c = d > r - 1.1f ? (phase >= 3 ? VoidLight : VoidMain) : d > r - 2.1f ? VoidDark : VoidDeep;
                    if (dx == -1 && dy == -1 && r >= 2) c = phase >= 3 ? Rage : VoidLight;
                    if (dx == 1 && dy == 1 && r >= 3) c = Frost.Mix(VoidMain, 0.5f);
                    put(cx + dx, cy + dy, c);
                }
            if (glow > 0.5f || phase >= 2)
            {
                // The ring of light round it, broken, turning (always from the second phase on).
                float ring = r + 1.6f;
                for (int k = 0; k < 8; k++)
                {
                    if (((k + frame) & 3) == 3) continue;
                    float a = k * (float)System.Math.PI / 4 + frame * 0.35f;
                    put(cx + ring * (float)System.Math.Cos(a), cy + ring * 0.8f * (float)System.Math.Sin(a), glow > 0.85f ? VoidLight : VoidMain);
                }
            }
            int motes = (int)(p.Frost * 4);
            for (int k = 0; k < motes; k++)
            {
                // Frost drawn round it and in.
                float a = k * 2.4f + frame * 0.7f, far = r + 3 + ((frame + k * 2) % 4);
                put(cx + far * (float)System.Math.Cos(a), cy + far * 0.8f * (float)System.Math.Sin(a), (k & 1) == 0 ? FrostHot : Frost);
            }
        }

        // The rift he rises out of or sinks into: a dark ellipse with specks of stars, a rim of violet light, the far half
        // behind him and the near half over where he goes into it.
        static void RiftRim(PixelImage image, float middle, int line, float half, bool near, int frame)
        {
            float rx = half + 4, ry = 3 + half * 0.25f;
            for (int y = (int)(line - ry - 1); y <= line + ry + 1; y++)
                for (int x = (int)(middle - rx - 1); x <= middle + rx + 1; x++)
                {
                    if (!image.Inside(x, y)) continue;
                    if (near != (y >= line)) continue;
                    float u = (x - middle) / rx, v = (y - line) / ry;
                    float d = (float)System.Math.Sqrt(u * u + v * v);
                    if (d > 1) continue;
                    if (near && image[x, y].A > 0 && d < 0.8f) continue;
                    var c = d > 0.86f ? ((x + frame) % 5 == 0 ? Frost : VoidLight) : d > 0.7f ? VoidMain : VoidDeep;
                    // Stars far down in it, twinkling.
                    if (d < 0.7f && PixelNoise.Value(x, y, 93 + frame / 3) > 0.93f) c = VoidLight;
                    image[x, y] = c.WithAlpha(d > 0.86f ? 230 : 245);
                }
        }
    }
}
