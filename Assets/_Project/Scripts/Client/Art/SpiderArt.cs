using System.Collections.Generic;

namespace Game.Client
{
    // What the venom spider is doing, which picks its frame (WorldView.Spider.cs). Every attack has a wind-up of its own
    // that the whole body takes part in: reared up with the fangs spread and the first legs lifted (Rear, then Bite),
    // reared higher with both front pairs held up like spears (Raise, then Stab: four stabs one after another), the
    // abdomen swung up over its back to aim the spinnerets (Aim, then Shoot: a web), the head drawn back with the fangs
    // dripping (Gag, then Spit), crouched and climbing up its own thread out of sight (Crouch, Climb) to fall on its mark
    // (Fall), the front legs plucking at its webs like strings (Tug, then Snap), the abdomen raised and quivering to drop
    // its eggs (Brood), turning on the spot spraying venom (Spin), and a roar reared up with every front leg spread.
    // Struck it flinches; felled, its legs curl up under it (Die).
    enum SpiderPose
    {
        Idle,
        Walk,
        Rear,
        Bite,
        Raise,
        Stab,
        Aim,
        Shoot,
        Gag,
        Spit,
        Crouch,
        Climb,
        Fall,
        Tug,
        Snap,
        Brood,
        Spin,
        Roar,
        Flinch,
        Die,
    }

    // The venom spider of the mushroom hollow (2026-11-13, Docs/Bosses.md 독그물 거미 아락사): a giant cave spider, its
    // bloated purple-black abdomen marked in glowing green. Its body comes from one Retro Diffusion Pro picture turned
    // eight ways (Docs/ArtSources.md, $0.28), cut into its two parts, the cephalothorax with the head and the abdomen, and
    // stripped of the picture's legs. Everything else is code: the two parts are placed on a small 3D model of the
    // spider (its forward, its side and up) seen from each of the five views, so they move apart and together as the
    // spider rears, tilts its abdomen or lunges; the eight legs are drawn anew in every frame, each a femur rising from
    // the cephalothorax to a high knee and a long tibia down to its foot, found from where the foot is (two-bone IK)
    // and projected the same way, so they walk in the alternating tetrapods real spiders walk in (L1 R2 L3 R4, then R1
    // L2 R3 L4), lift, stab and curl; the fangs and the palps are drawn at the head. Faces right.
    static class SpiderArt
    {
        public const int Width = 128;
        public const int Height = 104;
        // Rows under the ground point of its middle for the legs reaching toward the viewer seen from the front;
        // WorldView stands the picture on this row (Pivot).
        public const int Floor = 22;
        // How tall the body stands, for the name and marks over it.
        public const int BodyHeight = 40;

        // ---- The model: forward (f), the spider's right (l) and up (z), in pixels, from the ground under its middle. ----

        // The ground seen from above at a slant: a step toward the viewer is this much of a step up the screen.
        const float Slant = 0.6f;
        const float Ground = Height - 1 - Floor;
        // The middles of the cephalothorax and of the abdomen at rest; the abdomen hangs from the pedicel between them and
        // swings up and down about it (a tilt of a pixel turns it four degrees).
        const float CephF = 9, CephZ = 13, AbdomenF = -9, AbdomenZ = 20;
        const float PedicelF = 0, PedicelZ = 15;
        static readonly float PedicelReach = (float)System.Math.Sqrt((PedicelF - AbdomenF) * (PedicelF - AbdomenF) + (AbdomenZ - PedicelZ) * (AbdomenZ - PedicelZ));
        static readonly double PedicelAngle = System.Math.Atan2(AbdomenZ - PedicelZ, PedicelF - AbdomenF);
        const double TiltPerPixel = 4 * System.Math.PI / 180;
        // Each leg: where it joins the cephalothorax (from its middle), the way (degrees from forward) and reach of its
        // foot at rest; the femur and the tibia (with the metatarsus) long enough for the knee to stand above the back.
        static readonly float[] RootF = { 4, 1.5f, -1.5f, -4 };
        const float RootL = 4.5f;
        static readonly float[] FootWay = { 35, 70, 110, 150 };
        static readonly float[] FootReach = { 23, 24, 24, 26 };
        const float Femur = 13, Tibia = 19;
        const float LegThickness = 1.6f;

        // Leg k is pair k / 2 (0 the front pair) on the right (k odd) or left (k even).
        static int PairOf(int k) => k / 2;
        static int SideOf(int k) => k % 2 == 1 ? 1 : -1;

        // The ground's way on the screen for each view: forward is right from the side, toward the viewer (down the
        // screen) from the front, away (up) from behind.
        static (float C, float S) Way(BossView view) => view switch
        {
            BossView.Front => (0, 1),
            BossView.FrontSide => (0.7071f, 0.7071f),
            BossView.BackSide => (0.7071f, -0.7071f),
            BossView.Back => (0, -1),
            _ => (1, 0),
        };

        // Where a point of the model lands on the canvas, and how near the viewer it is (bigger is nearer).
        static (float X, float Y) Project(BossView view, float f, float l, float z)
        {
            var (c, s) = Way(view);
            float gx = f * c - l * s, gy = f * s + l * c;
            return (Width / 2f + gx, Ground + gy * Slant - z);
        }

        static float Depth(BossView view, float f, float l)
        {
            var (c, s) = Way(view);
            return f * s + l * c;
        }

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

        // ---- Parts: in each view the picture's cephalothorax (with the head) and abdomen are the pixels inside a few
        // ellipses (centre x, y on the picture, radii), a pixel going to the first that holds it; the first of each part is
        // its middle. The picture's own legs
        // fall outside them and are left out. ----

        static (bool Abdomen, float X, float Y, float RX, float RY)[] EllipsesOf(BossView view) => view switch
        {
            BossView.Front => new[] { (false, 32f, 28f, 9f, 8f), (true, 32f, 11.5f, 12f, 10.5f) },
            BossView.FrontSide => new[] { (false, 37.5f, 26f, 8.5f, 8f), (false, 31f, 21f, 5f, 4f), (true, 25f, 12f, 11f, 10.5f) },
            BossView.Side => new[] { (false, 40.5f, 24.5f, 8.5f, 7.5f), (false, 32f, 21f, 4f, 4f), (true, 21f, 16f, 11.5f, 11f) },
            // From behind the abdomen is nearer: its pixels come first.
            BossView.BackSide => new[] { (true, 23f, 24f, 11.5f, 11f), (false, 37.5f, 17f, 8f, 7.5f), (false, 31f, 21f, 4f, 4f) },
            _ => new[] { (true, 32f, 24f, 11.5f, 11.5f), (false, 32f, 14f, 7.5f, 6f) },
        };

        sealed class Parts
        {
            public PixelImage Ceph, Abdomen;
            public float CephX, CephY, AbdomenX, AbdomenY;
        }

        static readonly Dictionary<BossView, Parts> PartsByView = new Dictionary<BossView, Parts>();

        static Parts PartsOf(BossView view)
        {
            if (PartsByView.TryGetValue(view, out var known)) return known;
            var image = ViewOf(view);
            var ellipses = EllipsesOf(view);
            var parts = new Parts { Ceph = new PixelImage(image.Width, image.Height), Abdomen = new PixelImage(image.Width, image.Height) };
            var abdomen = System.Array.Find(ellipses, e => e.Abdomen);
            var ceph = System.Array.Find(ellipses, e => !e.Abdomen);
            (parts.AbdomenX, parts.AbdomenY) = (abdomen.X, abdomen.Y);
            (parts.CephX, parts.CephY) = (ceph.X, ceph.Y);
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    if (image[x, y].A == 0) continue;
                    foreach (var e in ellipses)
                    {
                        float u = (x + 0.5f - e.X) / e.RX, v = (y + 0.5f - e.Y) / e.RY;
                        if (u * u + v * v > 1) continue;
                        (e.Abdomen ? parts.Abdomen : parts.Ceph)[x, y] = image[x, y];
                        break;
                    }
                }
            }
            PartsByView[view] = parts;
            return parts;
        }

        // ---- A pose: the body lifted (z), carried forward (lunge) or aside (sway); the cephalothorax reared up; the
        // abdomen tilted up and tucked forward over the back; the fangs spread (0 shut .. 1 wide) and the palps raised;
        // the markings' glow; how far the legs curl up under it (dead); how much the feet go with the body (1 in the
        // air); and each foot moved from its place at rest (forward, outward from the body, up). ----

        struct Pose
        {
            public float Lift, Lunge, Sway, Rear, Tilt, Tuck, Fang, Palp, Glow, Curl, Carry;
            public float[] F, L, Z;
        }

        static Pose P(float lift = 0, float lunge = 0, float sway = 0, float rear = 0, float tilt = 0, float tuck = 0, float fang = 0, float palp = 0,
            float glow = 0, float curl = 0, float carry = 0) => new Pose
        {
            Lift = lift, Lunge = lunge, Sway = sway, Rear = rear, Tilt = tilt, Tuck = tuck, Fang = fang, Palp = palp, Glow = glow, Curl = curl, Carry = carry,
            F = new float[8], L = new float[8], Z = new float[8],
        };

        // The pose with one pair of feet moved (outward is away from the body on each side), or one foot of it (side
        // -1 left, 1 right).
        static Pose Feet(this Pose p, int pair, float forward, float outward, float up, int side = 0)
        {
            var q = p;
            q.F = (float[])p.F.Clone();
            q.L = (float[])p.L.Clone();
            q.Z = (float[])p.Z.Clone();
            for (int k = pair * 2; k <= pair * 2 + 1; k++)
            {
                if (side != 0 && SideOf(k) != side) continue;
                q.F[k] = forward;
                q.L[k] = outward;
                q.Z[k] = up;
            }
            return q;
        }

        static Pose Lerp(Pose a, Pose b, float u)
        {
            float L(float x, float y) => x + (y - x) * u;
            var p = P(L(a.Lift, b.Lift), L(a.Lunge, b.Lunge), L(a.Sway, b.Sway), L(a.Rear, b.Rear), L(a.Tilt, b.Tilt), L(a.Tuck, b.Tuck), L(a.Fang, b.Fang),
                L(a.Palp, b.Palp), L(a.Glow, b.Glow), L(a.Curl, b.Curl), L(a.Carry, b.Carry));
            for (int k = 0; k < 8; k++)
            {
                p.F[k] = L(a.F[k], b.F[k]);
                p.L[k] = L(a.L[k], b.L[k]);
                p.Z[k] = L(a.Z[k], b.Z[k]);
            }
            return p;
        }

        static Pose Add(Pose a, Pose b)
        {
            var p = P(a.Lift + b.Lift, a.Lunge + b.Lunge, a.Sway + b.Sway, a.Rear + b.Rear, a.Tilt + b.Tilt, a.Tuck + b.Tuck, a.Fang + b.Fang, a.Palp + b.Palp,
                a.Glow + b.Glow, a.Curl + b.Curl, a.Carry + b.Carry);
            for (int k = 0; k < 8; k++)
            {
                p.F[k] = a.F[k] + b.F[k];
                p.L[k] = a.L[k] + b.L[k];
                p.Z[k] = a.Z[k] + b.Z[k];
            }
            return p;
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

        // ---- The frames. Wind-ups and loops are key poses eased into each other; each blow is laid out in stages, a
        // number of frames each, from the end of its wind-up back to rest. Every motion starts from and comes back to the
        // same rest. ----

        enum Stage : byte
        {
            Swing,
            Strike,
            Rebound,
            Held,
            Return,
        }

        static float StageSeconds(Stage stage) => stage switch
        {
            Stage.Swing => 0.025f,
            Stage.Strike => 0.07f,
            Stage.Rebound => 0.06f,
            Stage.Held => 0.1f,
            _ => 0.05f,
        };

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
                    float eased = fast ? (float)System.Math.Pow(u, 1.4) : u * u * (3 - 2 * u);
                    frames.Add(Lerp(at, to, eased));
                    kinds.Add(stage);
                }
                at = to;
            }
            return (frames.ToArray(), kinds.ToArray());
        }

        static readonly Pose Rest = P();

        // Reared to bite: the cephalothorax up, the abdomen low behind, the first legs lifted and spread, fangs wide.
        static readonly Pose RearTop = P(lift: 2, lunge: -3, rear: 6, tilt: -1, fang: 1, palp: 4, glow: 0.2f)
            .Feet(0, 0, 5, 24).Feet(1, 1, 1, 4).Feet(3, -2, 0, 0);
        // Reared high, both front pairs held up like spears over the head.
        static readonly Pose RaiseTop = P(lift: 3, lunge: -4, rear: 7, tilt: 1, fang: 0.5f, palp: 5, glow: 0.2f)
            .Feet(0, -10, 4, 42).Feet(1, -6, 5, 30).Feet(2, -1, 1, 0).Feet(3, -3, 0, 0);
        // The abdomen swung up and forward over its back to aim the spinnerets; the front legs braced apart.
        static readonly Pose AimTop = P(lift: -1, lunge: -1, rear: -3, tilt: 11, tuck: 7, palp: 2, glow: 0.7f)
            .Feet(0, 2, 3, 0).Feet(3, -4, 1, 0);
        // The head drawn back and up, fangs wide and dripping.
        static readonly Pose GagTop = P(lift: 1, lunge: -5, rear: 6, tilt: 2, fang: 1, palp: 5, glow: 0.5f)
            .Feet(0, 1, 2, 5).Feet(3, -3, 0, 0);
        // Crouched low before the climb, the abdomen up (the thread goes up from its spinnerets).
        static readonly Pose Crouched = P(lift: -5, rear: -2, tilt: 5, glow: 0.3f).Feet(0, 2, 3, 0).Feet(1, 0, 3, 0).Feet(2, 0, 3, 0).Feet(3, -1, 3, 0);
        // Hanging in the air, legs reaching wide and down for the floor.
        static readonly Pose HangingPose = P(lift: 48, carry: 1, rear: 2, tilt: 3, fang: 1, glow: 0.4f)
            .Feet(0, 4, 6, 8).Feet(1, 1, 7, 6).Feet(2, -1, 7, 6).Feet(3, -3, 6, 8);
        // Plucking: low, the front legs drawn back on the threads.
        static readonly Pose TugTop = P(lift: -2, lunge: -2, rear: 2, tilt: 3, palp: 3, glow: 1)
            .Feet(0, -5, 1, 9).Feet(1, -4, 1, 6).Feet(3, -2, 1, 0);
        // The abdomen raised and swollen with eggs.
        static readonly Pose BroodTop = P(lift: 1, rear: -2, tilt: 9, tuck: -3, glow: 0.8f).Feet(3, -3, 2, 0).Feet(2, -1, 2, 0);
        // Reared up in a roar, every front leg spread.
        static readonly Pose RoarTop = P(lift: 4, lunge: -3, rear: 8, tilt: 0, fang: 1, palp: 6, glow: 1)
            .Feet(0, -10, 9, 40).Feet(1, -5, 8, 26).Feet(3, -3, 1, 0);
        // Struck: down a little, legs drawn in.
        static readonly Pose Struck = P(lift: -2, lunge: -2, rear: -2, tilt: 2, fang: 0.4f)
            .Feet(0, -2, -2, 2).Feet(1, -1, -2, 1).Feet(2, 1, -2, 1).Feet(3, 2, -2, 2);
        // Dead: down on the floor, the legs curled up over it.
        static readonly Pose Dead = P(lift: -9, rear: -3, tilt: -6, curl: 1);

        // The bite: the lunge with the fangs spread, the fangs snapping shut as it lands, held, back to rest.
        static readonly (Pose[] Frames, Stage[] Stages) BiteBlow = Expand(RearTop,
            (Stage.Swing, 5, P(lift: 0, lunge: 7, rear: 1, tilt: -1, fang: 1, palp: 2, glow: 0.2f).Feet(0, 9, 3, 2).Feet(1, 4, 1, 0).Feet(3, 2, 0, 0)),
            (Stage.Strike, 1, P(lift: -2, lunge: 9, rear: -2, tilt: 0, fang: 0, palp: 1, glow: 0.2f).Feet(0, 10, 3, 0).Feet(1, 4, 1, 0).Feet(3, 2, 0, 0)),
            (Stage.Rebound, 1, P(lift: -1, lunge: 8, rear: -1, fang: 0.15f, palp: 1).Feet(0, 10, 3, 0).Feet(1, 4, 1, 0).Feet(3, 2, 0, 0)),
            (Stage.Held, 2, P(lift: -1, lunge: 7, rear: -1, fang: 0.1f, palp: 1).Feet(0, 9, 3, 0).Feet(1, 4, 1, 0).Feet(3, 2, 0, 0)),
            (Stage.Return, 8, Rest));

        // The stab: down come the four front legs one after another (left first, right, then the second pair), each
        // spearing the floor a little further ahead as the body leans on into it; held; back to rest.
        static readonly Pose Stab1 = P(lift: 3, lunge: -1, rear: 6, tilt: 1, fang: 0.6f, palp: 4).Feet(0, -10, 4, 42).Feet(1, -6, 5, 30).Feet(2, -1, 1, 0).Feet(3, -3, 0, 0)
            .Feet(0, 13, 2, 0, -1);
        static readonly Pose Stab2 = P(lift: 2, lunge: 1, rear: 5, tilt: 0, fang: 0.6f, palp: 3).Feet(0, 13, 2, 0, -1).Feet(0, 15, 2, 0, 1).Feet(1, -6, 5, 30)
            .Feet(2, -1, 1, 0).Feet(3, -3, 0, 0);
        static readonly Pose Stab3 = P(lift: 1, lunge: 3, rear: 4, tilt: 0, fang: 0.7f, palp: 3).Feet(0, 13, 2, 0, -1).Feet(0, 15, 2, 0, 1).Feet(1, 12, 1, 0, -1)
            .Feet(1, -6, 5, 30, 1).Feet(2, -1, 1, 0).Feet(3, -3, 0, 0);
        static readonly Pose Stab4 = P(lift: 0, lunge: 5, rear: 2, tilt: 0, fang: 0.8f, palp: 2).Feet(0, 13, 2, 0, -1).Feet(0, 15, 2, 0, 1).Feet(1, 12, 1, 0, -1)
            .Feet(1, 14, 1, 0, 1).Feet(2, 0, 1, 0).Feet(3, -2, 0, 0);
        static Pose Up(Pose p, float by) { p.Lift += by; p.Rear += by; return p; }
        static readonly (Pose[] Frames, Stage[] Stages) StabBlow = Expand(RaiseTop,
            (Stage.Swing, 2, Lerp(RaiseTop, Stab1, 0.7f)), (Stage.Strike, 1, Stab1),
            (Stage.Swing, 2, Lerp(Stab1, Stab2, 0.7f)), (Stage.Strike, 1, Stab2),
            (Stage.Swing, 2, Lerp(Stab2, Stab3, 0.7f)), (Stage.Strike, 1, Stab3),
            (Stage.Swing, 2, Lerp(Stab3, Stab4, 0.7f)), (Stage.Strike, 1, Stab4),
            (Stage.Held, 2, Up(Stab4, -1)),
            (Stage.Return, 10, Rest));

        // The web: the abdomen pumps once (the thread shot), recoils, and settles.
        static readonly (Pose[] Frames, Stage[] Stages) ShootBlow = Expand(AimTop,
            (Stage.Swing, 2, P(lift: -1, lunge: -2, rear: -4, tilt: 14, tuck: 10, palp: 2, glow: 1).Feet(0, 2, 3, 0).Feet(3, -5, 1, 0)),
            (Stage.Strike, 1, P(lift: -2, lunge: -3, rear: -3, tilt: 7, tuck: 3, palp: 1, glow: 0.6f).Feet(0, 2, 3, 0).Feet(3, -5, 1, 0)),
            (Stage.Rebound, 1, P(lift: -1, lunge: -2, rear: -2, tilt: 8, tuck: 4, palp: 1, glow: 0.5f).Feet(0, 2, 3, 0).Feet(3, -4, 1, 0)),
            (Stage.Return, 8, Rest));

        // The spit: the head thrust forward and down, venom out, back to rest.
        static readonly (Pose[] Frames, Stage[] Stages) SpitBlow = Expand(GagTop,
            (Stage.Swing, 4, P(lift: 0, lunge: 2, rear: 0, tilt: 1, fang: 1, palp: 3, glow: 0.6f).Feet(0, 3, 2, 2).Feet(3, -1, 0, 0)),
            (Stage.Strike, 1, P(lift: -1, lunge: 3, rear: -1, tilt: 0, fang: 0.7f, palp: 2, glow: 0.3f).Feet(0, 4, 2, 0).Feet(3, 0, 0, 0)),
            (Stage.Rebound, 1, P(lift: -1, lunge: 4, rear: -1, fang: 0.4f, palp: 1).Feet(0, 4, 2, 0)),
            (Stage.Return, 8, Rest));

        // Coming down on the mark from above: the last of the fall, the slam with the legs splayed flat, back to rest.
        static readonly (Pose[] Frames, Stage[] Stages) FallBlow = Expand(HangingPose,
            (Stage.Swing, 6, P(lift: 0, carry: 1, rear: 1, tilt: 2, fang: 1, glow: 0.4f).Feet(0, 5, 7, 2).Feet(1, 1, 8, 1).Feet(2, -1, 8, 1).Feet(3, -4, 7, 2)),
            (Stage.Strike, 1, P(lift: -3, rear: -2, tilt: 0, fang: 0.6f, glow: 0.4f).Feet(0, 5, 8, 0).Feet(1, 1, 9, 0).Feet(2, -1, 9, 0).Feet(3, -4, 8, 0)),
            (Stage.Rebound, 1, P(lift: -5, rear: -3, tilt: -1, fang: 0.4f, glow: 0.3f).Feet(0, 5, 7, 0).Feet(1, 1, 8, 0).Feet(2, -1, 8, 0).Feet(3, -4, 7, 0)),
            (Stage.Held, 2, P(lift: -4, rear: -2, tilt: -1, fang: 0.3f, glow: 0.2f).Feet(0, 5, 7, 0).Feet(1, 1, 8, 0).Feet(2, -1, 8, 0).Feet(3, -4, 7, 0)),
            (Stage.Return, 8, Rest));

        // The pluck let go: the front legs flung out, every thread twanging.
        static readonly (Pose[] Frames, Stage[] Stages) SnapBlow = Expand(TugTop,
            (Stage.Swing, 2, P(lift: 1, lunge: 0, rear: 5, tilt: 2, fang: 0.6f, palp: 4, glow: 1).Feet(0, 6, 7, 10).Feet(1, 3, 6, 6).Feet(3, -2, 2, 0)),
            (Stage.Strike, 1, P(lift: 2, lunge: 1, rear: 6, tilt: 1, fang: 0.8f, palp: 4, glow: 1).Feet(0, 7, 9, 11).Feet(1, 4, 8, 7).Feet(3, -2, 2, 0)),
            (Stage.Rebound, 1, P(lift: 1, lunge: 1, rear: 5, tilt: 1, fang: 0.6f, palp: 3, glow: 0.8f).Feet(0, 6, 8, 9).Feet(1, 3, 7, 5).Feet(3, -2, 2, 0)),
            (Stage.Held, 2, P(lift: 1, lunge: 1, rear: 4, fang: 0.5f, palp: 2, glow: 0.6f).Feet(0, 5, 7, 7).Feet(1, 3, 6, 4).Feet(3, -2, 1, 0)),
            (Stage.Return, 8, Rest));

        static (Pose[] Frames, Stage[] Stages) BlowOf(SpiderPose pose) => pose switch
        {
            SpiderPose.Bite => BiteBlow,
            SpiderPose.Stab => StabBlow,
            SpiderPose.Shoot => ShootBlow,
            SpiderPose.Spit => SpitBlow,
            SpiderPose.Fall => FallBlow,
            SpiderPose.Snap => SnapBlow,
            _ => (null, null),
        };

        public static bool IsBlow(SpiderPose pose) => BlowOf(pose).Frames != null;

        // How many frames each pose has.
        public static int FramesOf(SpiderPose pose) => pose switch
        {
            SpiderPose.Idle => 4,
            SpiderPose.Walk => 8,
            SpiderPose.Rear or SpiderPose.Raise or SpiderPose.Aim or SpiderPose.Gag or SpiderPose.Tug or SpiderPose.Brood => 12,
            SpiderPose.Crouch => 8,
            SpiderPose.Climb => 20,
            SpiderPose.Spin => 8,
            SpiderPose.Roar => RoarRise + 4,
            SpiderPose.Flinch => 4,
            SpiderPose.Die => 16,
            _ => BlowOf(pose).Frames?.Length ?? 4,
        };

        // How long a blow's frame is shown (seconds); 0 for anything else.
        public static float FrameSeconds(SpiderPose pose, int frame)
        {
            var stages = BlowOf(pose).Stages;
            return stages == null ? 0 : StageSeconds(stages[System.Math.Max(0, System.Math.Min(stages.Length - 1, frame))]);
        }

        // The frames of a blow too fast to follow, and the frame it lands (the first strike).
        public static bool Swinging(SpiderPose pose, int frame)
        {
            var stages = BlowOf(pose).Stages;
            return stages != null && frame < stages.Length && (stages[frame] == Stage.Swing || stages[frame] == Stage.Strike);
        }

        public static bool Striking(SpiderPose pose, int frame)
        {
            var stages = BlowOf(pose).Stages;
            return stages != null && frame < stages.Length && stages[frame] == Stage.Strike;
        }

        public static int StrikeFrame(SpiderPose pose) => System.Array.IndexOf(BlowOf(pose).Stages ?? new Stage[0], Stage.Strike);

        // The frames up to the last strike (the stab strikes four times), which fill a telegraph's last moment.
        public static int LastStrikeFrame(SpiderPose pose) => System.Array.LastIndexOf(BlowOf(pose).Stages ?? new Stage[0], Stage.Strike);

        // The frames a roar takes to rear up, before it shakes there.
        public const int RoarRise = 12;

        // Walking: the alternating tetrapods, a full step of each in a loop.
        static Pose Gait(float cycle, float stride, float lift, Pose under)
        {
            var p = Lerp(under, under, 0);
            p.F = (float[])under.F.Clone();
            p.L = (float[])under.L.Clone();
            p.Z = (float[])under.Z.Clone();
            for (int k = 0; k < 8; k++)
            {
                // L1 R2 L3 R4 against R1 L2 R3 L4.
                bool first = (PairOf(k) % 2 == 0) == (SideOf(k) < 0);
                double u = cycle + (first ? 0 : System.Math.PI);
                // Lifted swinging forward (sin > 0), planted sliding back.
                p.F[k] += -stride * (float)System.Math.Cos(u);
                p.Z[k] += lift * (float)System.Math.Max(0, System.Math.Sin(u));
            }
            // The body dips a little as each set comes down.
            p.Lift += -0.6f * (float)System.Math.Abs(System.Math.Sin(cycle));
            p.Sway += 0.6f * (float)System.Math.Sin(cycle);
            return p;
        }

        static Pose PoseOf(SpiderPose pose, int frame)
        {
            int count = FramesOf(pose);
            int f = ((frame % count) + count) % count;
            float t = count > 1 ? f / (float)(count - 1) : 0;
            float cycle = f / (float)count * 2 * (float)System.Math.PI;
            float s = (float)System.Math.Sin(cycle), c = (float)System.Math.Cos(cycle);
            switch (pose)
            {
                case SpiderPose.Idle:
                {
                    // Breathing: the abdomen swells, the markings glow, the palps twitch.
                    float b = 0.5f - 0.5f * c;
                    return P(tilt: 0.8f * b, palp: 1.2f * b, glow: 0.15f * b, fang: 0.1f * b);
                }
                case SpiderPose.Walk:
                    return Gait(cycle, 5, 4, Rest);
                case SpiderPose.Rear:
                    return Keys(t, (0, Rest), (0.4f, P(lift: 1, lunge: -1, rear: 3, tilt: -1, fang: 0.4f, palp: 2).Feet(0, 1, 3, 10)), (1, RearTop));
                case SpiderPose.Raise:
                    return Keys(t, (0, Rest),
                        (0.35f, P(lift: 1, lunge: -1, rear: 3, tilt: 0, fang: 0.2f, palp: 2).Feet(0, -2, 2, 14).Feet(1, 1, 1, 6)),
                        (0.7f, P(lift: 2, lunge: -3, rear: 6, tilt: 1, fang: 0.4f, palp: 4).Feet(0, -7, 3, 32).Feet(1, -4, 4, 20).Feet(3, -2, 0, 0)),
                        (1, RaiseTop));
                case SpiderPose.Aim:
                    return Keys(t, (0, Rest), (0.45f, P(rear: -1, tilt: 5, tuck: 2, palp: 1, glow: 0.3f).Feet(3, -2, 0, 0)), (1, AimTop));
                case SpiderPose.Gag:
                    return Keys(t, (0, Rest), (0.5f, P(lunge: -2, rear: 3, tilt: 1, fang: 0.5f, palp: 3, glow: 0.2f).Feet(0, 0, 1, 2)), (1, GagTop));
                case SpiderPose.Crouch:
                    return Keys(t, (0, Rest), (1, Crouched));
                case SpiderPose.Climb:
                    // Out of the crouch: a push down, then up its thread fast, legs drawn in, out of the top of the picture.
                    return Keys(t, (0, Crouched),
                        (0.2f, P(lift: -6, rear: -1, tilt: 6, glow: 0.4f).Feet(0, 2, 3, 0).Feet(1, 0, 3, 0).Feet(2, 0, 3, 0).Feet(3, -1, 3, 0)),
                        (0.45f, P(lift: 10, carry: 1, rear: 1, tilt: 4, glow: 0.4f, curl: 0.2f).Feet(0, 1, 2, 2).Feet(3, -1, 2, 2)),
                        (1, P(lift: 100, carry: 1, rear: 1, tilt: 4, glow: 0.4f, curl: 0.45f)));
                case SpiderPose.Tug:
                {
                    // Lowered, plucking the threads twice with the front legs, the markings brightening.
                    var under = Keys(t, (0, Rest), (0.3f, P(lift: -1, lunge: -1, rear: 1, tilt: 2, palp: 2, glow: 0.4f).Feet(0, -2, 1, 6).Feet(1, -2, 1, 3)), (1, TugTop));
                    float pluck = t < 0.3f ? 0 : (float)System.Math.Sin((t - 0.3f) / 0.7f * 4 * System.Math.PI);
                    return Add(under, P(rear: 0.8f * pluck).Feet(0, 4 * pluck, 0, -2 * pluck).Feet(1, -3 * pluck, 0, 2 * pluck));
                }
                case SpiderPose.Brood:
                {
                    // The abdomen raised and quivering harder as the eggs come.
                    var under = Keys(t, (0, Rest), (1, BroodTop));
                    float quiver = (f % 2 == 0 ? 1 : -1) * t * (t < 1 ? 1 : 0);
                    return Add(under, P(tilt: quiver, sway: 0.6f * quiver));
                }
                case SpiderPose.Spin:
                    // Turning on the spot (WorldView turns the view round), the abdomen up spraying, legs scurrying.
                    return Gait(cycle, 3, 3, AimTop);
                case SpiderPose.Roar:
                    if (f < RoarRise) return Keys(f / (float)RoarRise, (0, Rest), (0.55f, P(lift: 2, lunge: -1, rear: 5, tilt: -1, fang: 0.6f, palp: 3, glow: 0.5f)
                        .Feet(0, -5, 5, 20).Feet(1, -2, 4, 12)), (1, RoarTop));
                    return Add(RoarTop, P(rear: s, fang: 0, lift: 0.5f * s).Feet(0, 0, s, 2 * s));
                case SpiderPose.Flinch:
                    return Keys(t, (0, Rest), (0.34f, Struck), (1, Rest));
                case SpiderPose.Die:
                    return Keys(t, (0, Rest), (0.3f, P(lift: 2, rear: 4, tilt: -2, fang: 1, palp: 6).Feet(0, 2, 4, 14).Feet(1, 1, 4, 10).Feet(2, 0, 4, 8).Feet(3, -1, 4, 10)),
                        (1, Dead));
                default:
                {
                    var frames = BlowOf(pose).Frames;
                    return frames == null ? Rest : frames[System.Math.Min(frames.Length - 1, f)];
                }
            }
        }

        // ---- Drawing ----

        static readonly Rgba Ink = new Rgba(4, 2, 8);
        static readonly Rgba LegDark = new Rgba(32, 21, 41);
        static readonly Rgba Leg = new Rgba(45, 31, 54);
        static readonly Rgba LegLight = new Rgba(67, 50, 71);
        static readonly Rgba Knee = new Rgba(80, 60, 84);
        static readonly Rgba Pedicel = new Rgba(20, 11, 28);
        static readonly Rgba Venom = new Rgba(159, 227, 93);
        static readonly Rgba VenomLight = new Rgba(214, 255, 150);
        static readonly Rgba FangDark = new Rgba(39, 25, 49);
        static readonly Rgba Glow = new Rgba(232, 255, 176);

        static bool Marking(Rgba c) => c.G > c.R + 60;

        // A leg (or a palp): a thick femur up to the knee, the tibia tapering to the foot, outlined, lit from above,
        // with a few bristles along the femur.
        static void Limb(PixelImage image, (float X, float Y) root, (float X, float Y) knee, (float X, float Y) foot, float thick, bool bristles)
        {
            void Segment(bool outline, (float X, float Y) a, (float X, float Y) b, float from, float to, bool femur)
            {
                float dx = b.X - a.X, dy = b.Y - a.Y, length = System.Math.Max(0.01f, (float)System.Math.Sqrt(dx * dx + dy * dy));
                int x0 = (int)System.Math.Floor(System.Math.Min(a.X, b.X) - from - 2), x1 = (int)System.Math.Ceiling(System.Math.Max(a.X, b.X) + from + 2);
                int y0 = (int)System.Math.Floor(System.Math.Min(a.Y, b.Y) - from - 2), y1 = (int)System.Math.Ceiling(System.Math.Max(a.Y, b.Y) + from + 2);
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        if (!image.Inside(x, y)) continue;
                        float px = x + 0.5f - a.X, py = y + 0.5f - a.Y;
                        float u = System.Math.Max(0, System.Math.Min(1, (px * dx + py * dy) / (length * length)));
                        float ex = px - u * dx, ey = py - u * dy;
                        float d = (float)System.Math.Sqrt(ex * ex + ey * ey);
                        float r = from + (to - from) * u;
                        if (outline)
                        {
                            if (d <= r + 1) image[x, y] = Ink;
                            continue;
                        }
                        if (d > r) continue;
                        // Lit on the side facing up; darker toward the foot.
                        float across = -(ex * -dy + ey * dx) / length * (dx >= 0 ? 1 : -1);
                        var color = ey < -0.3f ? LegLight : across > 0.4f ? LegDark : Leg;
                        if (!femur && u > 0.82f) color = LegDark;
                        image[x, y] = color;
                    }
                }
            }
            Segment(true, root, knee, thick, thick * 0.85f, true);
            Segment(true, knee, foot, thick * 0.8f, thick * 0.35f, false);
            Segment(false, root, knee, thick, thick * 0.85f, true);
            Segment(false, knee, foot, thick * 0.8f, thick * 0.35f, false);
            int kx = (int)System.Math.Round(knee.X - 0.5f), ky = (int)System.Math.Round(knee.Y - 0.5f);
            if (image.Inside(kx, ky)) image[kx, ky] = Knee;
            if (!bristles) return;
            // Bristles standing off the femur's upper edge.
            float fx = knee.X - root.X, fy = knee.Y - root.Y, flen = System.Math.Max(0.01f, (float)System.Math.Sqrt(fx * fx + fy * fy));
            float nx = fy / flen, ny = -fx / flen;
            if (ny > 0) (nx, ny) = (-nx, -ny);
            for (float d = 4; d < flen - 2; d += 4.5f)
            {
                int bx = (int)System.Math.Round(root.X + fx * d / flen + nx * (thick + 1.6f) - 0.5f);
                int by = (int)System.Math.Round(root.Y + fy * d / flen + ny * (thick + 1.6f) - 0.5f);
                if (image.Inside(bx, by) && image[bx, by].A == 0) image[bx, by] = Ink;
            }
        }

        // Where the knee goes between root and foot (model space): the femur and tibia meet above the line between them,
        // bent up and out from the body, as a spider's legs stand.
        static (float F, float L, float Z) KneeOf((float F, float L, float Z) root, (float F, float L, float Z) foot, int side)
        {
            float dx = foot.F - root.F, dy = foot.L - root.L, dz = foot.Z - root.Z;
            float d = (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
            float reach = Femur + Tibia - 0.5f;
            if (d > reach)
            {
                // Out of reach: the foot comes in along the line.
                float k = reach / d;
                dx *= k;
                dy *= k;
                dz *= k;
                d = reach;
            }
            d = System.Math.Max(d, System.Math.Abs(Tibia - Femur) + 0.5f);
            float ux = dx / d, uy = dy / d, uz = dz / d;
            // Bend up and outward, at right angles to the line.
            float bx = 0, by = 1.0f * side, bz = 0.8f;
            float along = bx * ux + by * uy + bz * uz;
            bx -= along * ux;
            by -= along * uy;
            bz -= along * uz;
            float bl = (float)System.Math.Sqrt(bx * bx + by * by + bz * bz);
            if (bl < 1e-3f) (bx, by, bz, bl) = (0, side, 0, 1);
            bx /= bl;
            by /= bl;
            bz /= bl;
            float a = (Femur * Femur + d * d - Tibia * Tibia) / (2 * Femur * d);
            a = System.Math.Max(-1, System.Math.Min(1, a));
            float along2 = Femur * a, up = Femur * (float)System.Math.Sqrt(1 - a * a);
            return (root.F + ux * along2 + bx * up, root.L + uy * along2 + by * up, root.Z + uz * along2 + bz * up);
        }

        // One frame. Faces right; WorldView flips it for the left. In the last phase the markings burn brighter.
        public static PixelImage Draw(BossView view, SpiderPose pose, int frame, int phase) => Render(view, pose, frame, phase, out _);

        // Where the two front feet and the head are in a frame, and the body's edges (canvas pixels): for the preview's
        // check that nothing jumps from one frame to the next.
        public struct Joints
        {
            public float AX, AY, BX, BY, HeadX, HeadY;
            public int Top, Bottom, Left, Right;
        }

        public static Joints JointsOf(BossView view, SpiderPose pose, int frame)
        {
            Render(view, pose, frame, 1, out var joints);
            return joints;
        }

        // Where on the canvas the head is in a frame (for the venom and the web to come out of), and the spinnerets.
        public static (float X, float Y) HeadOf(BossView view, SpiderPose pose, int frame)
        {
            var p = PoseOf(pose, frame);
            return Project(view, CephF + p.Lunge + 8, p.Sway, CephZ + p.Lift + p.Rear * 1.3f - 2);
        }

        public static (float X, float Y) SpinneretsOf(BossView view, SpiderPose pose, int frame)
        {
            var p = PoseOf(pose, frame);
            double swing = PedicelAngle + p.Tilt * TiltPerPixel;
            // The far end of the abdomen from the pedicel.
            float reach = PedicelReach + 10;
            return Project(view, PedicelF + p.Lunge + p.Tuck - reach * (float)System.Math.Cos(swing), p.Sway,
                PedicelZ + p.Lift + p.Rear * 0.3f + reach * (float)System.Math.Sin(swing) - 2);
        }

        // Dropping on its thread (the ceiling drop, WorldView): the legs reaching wide and down for the floor, the body at
        // the bottom of the picture (WorldView carries it down to the mark).
        public static PixelImage Hanging(BossView view, int phase)
        {
            var p = Lerp(HangingPose, HangingPose, 0);
            p.Lift = 0;
            return Render(view, p, phase, out _);
        }

        static PixelImage Render(BossView view, SpiderPose pose, int frame, int phase, out Joints joints) => Render(view, PoseOf(pose, frame), phase, out joints);

        static PixelImage Render(BossView view, Pose p, int phase, out Joints joints)
        {
            var parts = PartsOf(view);
            var image = new PixelImage(Width, Height);
            float glow = System.Math.Min(1, p.Glow + (phase >= 3 ? 0.45f : 0));

            // The body's two parts and where they stand.
            float bodyF = p.Lunge, bodyL = p.Sway, bodyZ = p.Lift;
            var ceph = (F: CephF + bodyF, L: bodyL, Z: CephZ + bodyZ + p.Rear);
            // The abdomen swings about the pedicel: tilted up it rises over the back, tilted down it sinks behind.
            double swing = PedicelAngle + p.Tilt * TiltPerPixel;
            var abdomen = (F: PedicelF + bodyF + p.Tuck - PedicelReach * (float)System.Math.Cos(swing), L: bodyL,
                Z: PedicelZ + bodyZ + p.Rear * 0.3f + PedicelReach * (float)System.Math.Sin(swing));
            var cephAt = Project(view, ceph.F, ceph.L, ceph.Z);
            var abdomenAt = Project(view, abdomen.F, abdomen.L, abdomen.Z);

            // The legs: roots on the cephalothorax (rising with it as it rears), feet where the pose puts them.
            var legs = new List<(float Depth, int K, (float, float) Root, (float, float) Knee, (float, float) Foot)>();
            joints = new Joints { Top = int.MaxValue, Left = int.MaxValue, Bottom = -1, Right = -1 };
            for (int k = 0; k < 8; k++)
            {
                int pair = PairOf(k), side = SideOf(k);
                var root = (F: ceph.F + RootF[pair], L: ceph.L + side * RootL, Z: CephZ + bodyZ + p.Rear * (0.75f - 0.15f * pair) - 1);
                double way = FootWay[pair] * System.Math.PI / 180;
                var foot = (F: CephF + FootReach[pair] * (float)System.Math.Cos(way) + p.F[k] + bodyF * p.Carry,
                    L: side * (FootReach[pair] * (float)System.Math.Sin(way) + p.L[k]) + bodyL * p.Carry,
                    Z: p.Z[k] + bodyZ * p.Carry);
                if (p.Curl > 0)
                {
                    // Curled up over the body, the way a dead spider's legs fold.
                    var curled = (F: root.F + (RootF[pair] - 1) * 1.2f, L: root.L + side * 6, Z: root.Z + 9);
                    foot = (foot.F + (curled.F - foot.F) * p.Curl, foot.L + (curled.L - foot.L) * p.Curl, foot.Z + (curled.Z - foot.Z) * p.Curl);
                }
                var knee = KneeOf(root, foot, side);
                var r = Project(view, root.F, root.L, root.Z);
                var kn = Project(view, knee.F, knee.L, knee.Z);
                var ft = Project(view, foot.F, foot.L, foot.Z);
                legs.Add((Depth(view, root.F, root.L), k, r, kn, ft));
                if (k == 0) (joints.AX, joints.AY) = ft;
                if (k == 1) (joints.BX, joints.BY) = ft;
            }
            legs.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            // A leg comes out in front of the body where its root is nearer the viewer than the body's nearer part is
            // (from behind, the abdomen hides where every leg joins).
            float front = System.Math.Max(Depth(view, ceph.F, ceph.L), Depth(view, abdomen.F, abdomen.L));
            foreach (var leg in legs)
                if (leg.Depth < front) Limb(image, leg.Root, leg.Knee, leg.Foot, LegThickness, true);

            // The body: the pedicel between the parts, then the further part and the nearer, outlined together.
            var body = new PixelImage(Width, Height);
            Limb(body, cephAt, ((cephAt.X + abdomenAt.X) / 2, (cephAt.Y + abdomenAt.Y) / 2), abdomenAt, 2.2f, false);
            void Lay(PixelImage part, float px, float py, (float X, float Y) at)
            {
                int ox = (int)System.Math.Round(at.X - px), oy = (int)System.Math.Round(at.Y - py);
                for (int y = 0; y < part.Height; y++)
                {
                    for (int x = 0; x < part.Width; x++)
                    {
                        var col = part[x, y];
                        if (col.A == 0 || !body.Inside(x + ox, y + oy)) continue;
                        if (Marking(col)) col = col.Mix(Glow, glow * 0.6f);
                        body[x + ox, y + oy] = col;
                    }
                }
            }
            bool cephNearer = Depth(view, ceph.F, ceph.L) >= Depth(view, abdomen.F, abdomen.L);
            if (cephNearer)
            {
                Lay(parts.Abdomen, parts.AbdomenX, parts.AbdomenY, abdomenAt);
                Lay(parts.Ceph, parts.CephX, parts.CephY, cephAt);
            }
            else
            {
                Lay(parts.Ceph, parts.CephX, parts.CephY, cephAt);
                Lay(parts.Abdomen, parts.AbdomenX, parts.AbdomenY, abdomenAt);
            }
            body = body.Outlined(Ink);
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (body[x, y].A > 0) image[x, y] = body[x, y];

            // The head: the palps either side and the fangs under it, dripping venom when spread.
            var head = (F: ceph.F + 8, L: ceph.L, Z: ceph.Z + p.Rear * 0.3f - 3);
            (joints.HeadX, joints.HeadY) = Project(view, head.F, head.L, head.Z);
            if (view != BossView.Back && view != BossView.BackSide)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    var palpRoot = Project(view, head.F - 1, head.L + side * 3, head.Z + 1);
                    var palpKnee = Project(view, head.F + 3, head.L + side * 4.5f, head.Z + 3 + p.Palp * 0.6f);
                    var palpTip = Project(view, head.F + 6, head.L + side * 4, head.Z - 2 + p.Palp);
                    Limb(image, palpRoot, palpKnee, palpTip, 0.9f, false);
                }
                foreach (int side in new[] { -1, 1 })
                {
                    float open = p.Fang;
                    var baseAt = (F: head.F + 0.5f, L: head.L + side * 1.8f, Z: head.Z - 1);
                    var mid = (F: baseAt.F + 1.5f + 1.5f * open, L: baseAt.L + side * (0.6f + 2.4f * open), Z: baseAt.Z - 2.5f);
                    var tip = (F: mid.F + 0.5f + 1 * open, L: mid.L - side * (1.4f - 0.6f * open), Z: mid.Z - 2.2f);
                    var b = Project(view, baseAt.F, baseAt.L, baseAt.Z);
                    var m = Project(view, mid.F, mid.L, mid.Z);
                    var tp = Project(view, tip.F, tip.L, tip.Z);
                    Limb(image, b, m, tp, 1.1f, false);
                    int tx = (int)System.Math.Round(tp.X - 0.5f), ty = (int)System.Math.Round(tp.Y - 0.5f);
                    if (image.Inside(tx, ty)) image[tx, ty] = open > 0.5f ? VenomLight : Venom;
                    if (open > 0.5f && image.Inside(tx, ty + 1)) image[tx, ty + 1] = Venom;
                    if (open > 0.8f && image.Inside(tx, ty + 2) && image[tx, ty + 2].A == 0) image[tx, ty + 2] = Venom.WithAlpha(200);
                }
            }

            foreach (var leg in legs)
                if (leg.Depth >= front) Limb(image, leg.Root, leg.Knee, leg.Foot, LegThickness, true);

            // The body's own top and bottom (the legs are followed by their feet), and the whole picture's sides.
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (body[x, y].A > 0)
                    {
                        joints.Top = System.Math.Min(joints.Top, y);
                        joints.Bottom = System.Math.Max(joints.Bottom, y);
                    }
                    if (image[x, y].A == 0) continue;
                    joints.Left = System.Math.Min(joints.Left, x);
                    joints.Right = System.Math.Max(joints.Right, x);
                }
            }
            if (joints.Bottom < 0) joints.Top = joints.Bottom = -1;
            return image;
        }

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

        static readonly string[] FrontRows =
        {
            "................................................................",
            "................................................................",
            ".............................bbbaba.............................",
            "..........................bbbfllllhbbb..........................",
            ".........................chhlknmmnlmhhbc........................",
            "........................blnosqoyyqqqqnlb........................",
            ".......................blnmytspyypqsykllb.......................",
            "......................bknmvyttyxwxppyvllhb......................",
            ".....................bfnnywpstxyyyoopwxonhc.....................",
            ".....................bhnmxmnqtyyyypqqoxonhb.....................",
            "....................chhnnmlnqtwyywqqqomnnhfa....................",
            "....................chhnnoymqtxyyxrqkymnnffa....................",
            "....................chhnnlvxrrxyyxrqyvlnnffa....................",
            "....................ahhnnnvxpqtyxpqoyvlnnffa....................",
            ".....................ffnnnnvxrmwyoqywmnnnff.....................",
            ".....................ffhnnnmwtrpvopwmnnnhff.....................",
            ".....................bfhnnnnmvtttrtmnnnlfhb.....................",
            "......................cffnnnnnnnnnlnnnnffc......................",
            ".......................fffhnnnnnnlnnnffff.......................",
            "........................ffffhhbbbbhhffff........................",
            "..........................ffbbkkkjbbff..........................",
            "...........................bhovvvvjhb...........................",
            ".........................abfhkhvvkkklbb.........................",
            ".........................agfjuttvuukkjb.........................",
            "........................bfffhdettdjhhhhb........................",
            ".......................fbfffbebbabcbfhfbf.......................",
            ".......................bbffbjsttttvfbffbb.......................",
            ".......................fbhbjqqnuukqqhbfbi.......................",
            ".......................bgbavhfvknvhjvcaec.......................",
            ".......................bbavenhenndjnevabb.......................",
            ".......................ffbhhdnvfnwnejfbff.......................",
            "........................fabfheeqqeehhbag........................",
            ".........................aahkhjqujhhhba.........................",
            ".........................afbbvthettbbia.........................",
            "...........................aavtccp.aa...........................",
            "..............................tca...............................",
            "................................................................",
            "................................................................",
        };

        static readonly string[] FrontSideRows =
        {
            "................................................................",
            "................................................................",
            "........................bbaa....................................",
            ".....................abbjkkkbaa.................................",
            "...................cahhkluooolkc................................",
            "..................cchkqqqqsvynnlba..............................",
            ".................ceknnsysssqqxxnlh..............................",
            ".................bhlnqsyyywrqqoxooh.............................",
            "................bfnnosswxxyyrsqmvnl.............................",
            "................cfnnqssxyyyyvrqvmnng............................",
            "...............cfhnqnqssvxyyyrqryooh............................",
            "...............affnoynqqqwyyyxoqryoj............................",
            "...............affnnuyoqqqrxyyxrqxon............................",
            "...............affnnquyvnoqrxyyoqonn............................",
            "...............affnnnsuqyqqqowyonnnl............................",
            "...............afflnonussyqqooronnn.............................",
            "................cffnoolqqsyvsllnnnm.............................",
            "................afffnonnnnovslnnnf..............................",
            "................cffffnnnnnnnnnnnbaaaaffa........................",
            ".................afffffjnnnnnnjbfklmjaaajh......................",
            "...................aaafffffffhajmvvrjlkechfb....................",
            "....................vlbcfffffbhmvkvvvvlkafacs...................",
            "..........................ffahjllnsvvhfaaacnf...................",
            "...........................acfhlllllfbaroaffbd..................",
            ".............................ghklljbcsvlkjbbeo..................",
            ".............................hgjkkalvvskkncalh..................",
            ".............................fffhblvqlkknejbhb..................",
            ".............................fffckqlkqsnl.jaa...................",
            ".............................bffbnnnnjdnqefaa...................",
            "..............................bbdhlefk.nqhcka...................",
            "..............................jaefflkfejhrtee...................",
            "...............................ecafffhlhaj......................",
            ".................................fbccivraa......................",
            "...................................aaa..........................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
        };

        static readonly string[] SideRows =
        {
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            ".................aaaaaaaaa......................................",
            "...............bcjjjkklkkiaa....................................",
            "..............chjorxxwpxyykha...................................",
            ".............cjlssttttsttvyokd..................................",
            "............chlssyyxxxsttruyvjb.................................",
            "...........chkqssxyyxyyyprrrykf.................................",
            "..........bhklqsstxxxyyyxxoooohc................................",
            "..........cflnqsvvvvxxyyyyywooke................................",
            "..........bhlqqsvvvvvvvxxxyywmlf................................",
            "..........ahnqquyyxsvvvtvtvtwvlf................................",
            "..........bfnqqsuusyxsooorrrrrnf................................",
            "..........afnnqqsssvsqyyywsolnnfea...bbbahjc....................",
            "..........bffnlqqssssssvvssllnnfbovkvlrkaffa....................",
            "..........affnnnqqussusonnnnnffbkvjvvvmjaab.....................",
            "...........bfffnnnssuuuqnnnnlfbhotomvvcbrvcha...................",
            "...........affffnnnnnnnnnnnfffahlsrvjjbrvklnna..................",
            "............afffffnnnnnnnffffbhhlllllbevvlkkufa.................",
            ".............fffffffbaaafffffahflllllbvvlnuknec.................",
            "..............affffbobojaffg..effhllkbvsnnfhnja.................",
            "...............cffgbmamlbgg.....ffffcdolnenvekva................",
            "..................brfafb........ffffbonnekddefj.a...............",
            ".................................ffbfjlnnhjheaaa................",
            ".................................bbkaffhfffvtdav................",
            "..................................bocbaaffc..va.................",
            "...................................okadhbaaaaw..................",
            ".....................................bchmoa.....................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
        };

        static readonly string[] BackSideRows =
        {
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            ".....................................a..........................",
            "..................................jcahaaa.......................",
            "................................alcaaahkloc.....................",
            "...............................nfaaaacvhcaai....................",
            "....................aaaaba....llllllalacncaba...................",
            ".................bjmooxxxxtfa.llkvlvlbffqfa.....................",
            "................flvxorrroooolfalrnsouhalqnna....................",
            "...............hlxvsssvvxyyxxwhbfuqsunfcvlnd....................",
            "..............hlwrsuvxyyyyyxsolhbqsnnllctljne...................",
            ".............aknsssvyyyyyyvssqnkcelllllcjvrhe...................",
            ".............flouvvyyyxxsssvsqxmfblllllhdssfa...................",
            "............aflovvyyxxwssvvswyrnjbfllnnhcjhfa...................",
            "............afnovvvvvvvsuvvyxonnjfbkklhffefa....................",
            "............afnnvvvvvvvvxyvvqnnnffafhhffcea.....................",
            "............afnnnvvvvvyyyqssunnnffacffffe.......................",
            "............bfnnnsvvvvsuuqssunnlff...f..........................",
            "............affnnnnuvussssssnnnfff..............................",
            "............afffnnnnnuuuuunonoffff..............................",
            ".............afffnnononnnnnooffffa..............................",
            ".............afffffnnnnnnnnfffffa...............................",
            "..............afffffffffgffffffar...............................",
            "..............afgfgffgfffffffffa................................",
            "...............afffffffffffffff.................................",
            "................abffgfffffffff..................................",
            "..................aaffffffgfa...................................",
            "....................aaaaaa......................................",
            "................................................................",
            "................................................................",
            "................................................................",
        };

        static readonly string[] BackRows =
        {
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "................................................................",
            "..........................c.........ak..........................",
            ".........................abb..aaaa..bba.........................",
            ".........................koc.anfhna.bsf.........................",
            ".........................leabnonnokbbnl.........................",
            ".........................kakbbaabbfkbhk.........................",
            "........................aaacfkllljcbabja........................",
            "........................bbixmowxroxkebbb........................",
            ".......................bhmyorsyytrpylkba........................",
            "......................chmymqrxyyxsrrxllbba......................",
            ".....................chnomnqsxyyxrsqolnnakk.....................",
            ".....................bllymnqrxyyyssnoynnhaf.....................",
            ".....................fnnxmrssyyyysssmxnnlab.....................",
            ".....................hnooyrssxxxxsssyrnnnfa.....................",
            ".....................hnnnysvvvyyvvvvxsnnnfa.....................",
            ".....................fnnnryvvvyyvvvyssnnnfa.....................",
            ".....................fnnnsvvvvvvvvvussnnnfa.....................",
            ".....................flnnnvvvvvvvvuunnnnffa.....................",
            ".....................ffnnnnuvvvvvuslnnnnffa.....................",
            ".....................fffnnnnnovvvnnnnnnfffa.....................",
            "......................fffnnnnnnnnnnnnffffb......................",
            ".......................ffffnnnnnnnnnfffff.......................",
            ".......................fffffffffffffffffa.......................",
            "........................ffffffffffffffff........................",
            "..........................fffffffffffg..........................",
            "...........................fffffffffa...........................",
            "................................................................",
            "................................................................",
            "................................................................",
        };
    }
}
