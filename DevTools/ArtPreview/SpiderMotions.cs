using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Client
{
    // The venom spider's motions (2026-11-13, SpiderArt), each as WorldView plays it (AnimateSpider): rest, the wind-up
    // through its telegraph, the blow in its stages, back to rest. A row each, facing right and facing down. Written with
    // every frame in strips and a continuity report (MotionCheck: the two front feet and the head are tracked), and a
    // sheet of the key poses in all eight directions as the game turns and flips them (spider_dirs.png).
    static class SpiderMotions
    {
        public static readonly string[] Rows = { "walk", "bite", "stab", "web", "spit", "drop", "pluck", "brood", "spin", "roar", "flinch", "die" };

        static MotionCheck.Step S(SpiderPose pose, int frame, float seconds, MotionCheck.Kind kind = MotionCheck.Kind.Normal, int phase = 1) => new MotionCheck.Step
        {
            Label = $"{pose}{frame}", Seconds = seconds, Kind = kind,
            Draw = view => SpiderArt.Draw(view, pose, frame, phase),
            Measure = view =>
            {
                var j = SpiderArt.JointsOf(view, pose, frame);
                return new MotionCheck.Measure { Points = new[] { (j.AX, j.AY), (j.BX, j.BY), (j.HeadX, j.HeadY) }, Top = j.Top, Bottom = j.Bottom };
            },
        };

        // A wind-up's frames spread over its telegraph.
        static IEnumerable<MotionCheck.Step> Run(SpiderPose pose, float seconds, bool backward = false, MotionCheck.Kind kind = MotionCheck.Kind.Normal)
        {
            int count = SpiderArt.FramesOf(pose);
            var frames = Enumerable.Range(0, count);
            return (backward ? frames.Reverse() : frames).Select(f => S(pose, f, seconds / count, kind));
        }

        // A blow's frames, each for its stage's time; the swing frames fill the telegraph's last moment.
        static IEnumerable<MotionCheck.Step> Blow(SpiderPose pose, MotionCheck.Kind swing = MotionCheck.Kind.Swing) =>
            Enumerable.Range(0, SpiderArt.FramesOf(pose)).Select(f => S(pose, f, SpiderArt.FrameSeconds(pose, f),
                SpiderArt.Striking(pose, f) ? MotionCheck.Kind.Strike : SpiderArt.Swinging(pose, f) ? swing : MotionCheck.Kind.Normal));

        static IEnumerable<MotionCheck.Step> Rest => new[] { S(SpiderPose.Idle, 0, 0.4f) };

        // The telegraph's length with the blow's swing frames taken out of its end (WorldView.AnimateSpider).
        static float WindUp(float telegraph, SpiderPose blow)
        {
            float swing = 0;
            for (int f = 0; f < SpiderArt.StrikeFrame(blow); f++) swing += SpiderArt.FrameSeconds(blow, f);
            return telegraph - swing;
        }

        public static List<MotionCheck.Step>[] Sequences() => new[]
        {
            Run(SpiderPose.Walk, 0.48f).Concat(Run(SpiderPose.Walk, 0.48f)),
            Rest.Concat(Run(SpiderPose.Rear, WindUp(0.8f, SpiderPose.Bite))).Concat(Blow(SpiderPose.Bite)).Concat(Rest),
            Rest.Concat(Run(SpiderPose.Raise, WindUp(1.0f, SpiderPose.Stab))).Concat(Blow(SpiderPose.Stab)).Concat(Rest),
            Rest.Concat(Run(SpiderPose.Aim, WindUp(1.1f, SpiderPose.Shoot))).Concat(Blow(SpiderPose.Shoot)).Concat(Rest),
            Rest.Concat(Run(SpiderPose.Gag, WindUp(0.8f, SpiderPose.Spit))).Concat(Blow(SpiderPose.Spit)).Concat(Rest),
            // The drop: crouched, up its thread out of sight; it falls on the mark (the swing frames in the air).
            Rest.Concat(Run(SpiderPose.Crouch, 0.66f)).Concat(Run(SpiderPose.Climb, 0.42f, kind: MotionCheck.Kind.Air))
                .Concat(new[] { S(SpiderPose.Fall, 0, 0.5f, MotionCheck.Kind.Cut) }).Concat(Blow(SpiderPose.Fall, MotionCheck.Kind.Air).Skip(1)).Concat(Rest),
            Rest.Concat(Run(SpiderPose.Tug, WindUp(1.6f, SpiderPose.Snap))).Concat(Blow(SpiderPose.Snap)).Concat(Rest),
            Rest.Concat(Run(SpiderPose.Brood, 1.0f)).Concat(Run(SpiderPose.Brood, 0.36f, backward: true)).Concat(Rest),
            Rest.Concat(Run(SpiderPose.Aim, 0.9f)).Concat(Run(SpiderPose.Spin, 0.3f)).Concat(Run(SpiderPose.Spin, 0.3f)).Concat(Run(SpiderPose.Spin, 0.3f))
                .Concat(Run(SpiderPose.Aim, 0.3f, backward: true)).Concat(Rest),
            Rest.Concat(Enumerable.Range(0, SpiderArt.RoarRise).Select(f => S(SpiderPose.Roar, f, 0.06f)))
                .Concat(Enumerable.Range(SpiderArt.RoarRise, 4).Select(f => S(SpiderPose.Roar, f, 0.08f)))
                .Concat(Enumerable.Range(SpiderArt.RoarRise, 4).Select(f => S(SpiderPose.Roar, f, 0.08f)))
                .Concat(Enumerable.Range(0, SpiderArt.RoarRise).Reverse().Select(f => S(SpiderPose.Roar, f, 0.03f))).Concat(Rest),
            Rest.Concat(Run(SpiderPose.Flinch, 0.2f)).Concat(Rest),
            Rest.Concat(Run(SpiderPose.Die, 1.1f)).Concat(new[] { S(SpiderPose.Die, SpiderArt.FramesOf(SpiderPose.Die) - 1, 0.8f) }),
        }.Select(r => r.ToList()).ToArray();

        public static int Write(string folder, PixelImage arena)
        {
            int jumps = MotionCheck.Write(folder, "spider", Rows, Sequences(), arena, SpiderArt.Width, SpiderArt.Height, 8);
            Directions(Path.Combine(folder, "spider_dirs.png"), arena);
            return jumps;
        }

        // The eight ways the game turns it (WorldView.BossSectors: side, back three-quarter, back, ... flipped for the
        // left), a column each, for a few key poses, a row each.
        static readonly (BossView View, bool Left)[] Sectors =
        {
            (BossView.Side, false), (BossView.BackSide, false), (BossView.Back, false), (BossView.BackSide, true),
            (BossView.Side, true), (BossView.FrontSide, true), (BossView.Front, false), (BossView.FrontSide, false),
        };

        static void Directions(string path, PixelImage arena)
        {
            var poses = new (SpiderPose Pose, int Frame)[]
            {
                (SpiderPose.Idle, 0), (SpiderPose.Walk, 1), (SpiderPose.Walk, 5), (SpiderPose.Rear, 11), (SpiderPose.Bite, SpiderArt.StrikeFrame(SpiderPose.Bite)),
                (SpiderPose.Raise, 11), (SpiderPose.Stab, SpiderArt.LastStrikeFrame(SpiderPose.Stab)), (SpiderPose.Aim, 11), (SpiderPose.Gag, 11),
                (SpiderPose.Fall, SpiderArt.StrikeFrame(SpiderPose.Fall)), (SpiderPose.Tug, 6), (SpiderPose.Roar, SpiderArt.RoarRise), (SpiderPose.Die, SpiderArt.FramesOf(SpiderPose.Die) - 1),
            };
            int w = SpiderArt.Width, h = SpiderArt.Height;
            var sheet = new PixelImage(8 * w, poses.Length * h);
            for (int r = 0; r < poses.Length; r++)
            {
                for (int s = 0; s < 8; s++)
                {
                    var image = SpiderArt.Draw(Sectors[s].View, poses[r].Pose, poses[r].Frame, 1);
                    if (Sectors[s].Left) image = image.FlippedX();
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            var ground = arena[x % arena.Width, y % arena.Height];
                            if (y == h - 1 - SpiderArt.Floor && x % 4 < 2) ground = ground.Mix(new Rgba(255, 255, 255), 0.25f);
                            var c = image[x, y];
                            sheet[s * w + x, r * h + y] = c.A == 0 ? ground : c.A == 255 ? c : ground.Mix(c.WithAlpha(255), c.A / 255f);
                        }
                }
            }
            Png.Save(sheet, path);
        }
    }
}
