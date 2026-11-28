using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Client
{
    // The cave warden's motions (2026-11-26, WardenArt), each as WorldView plays it: idle into the wind-up, the blow,
    // back to rest; one row each, side (turned with the blow) and front. Every frame of each motion on a strip
    // (warden_frames_<motion>.png), the poses turned eight ways (warden_turn.png), and how far the fists and the body
    // move from each frame to the next (warden_continuity.txt).
    static partial class Motions
    {
        static readonly string[] WardenRows = { "walk", "slam", "sweep", "burst", "stomp", "punch", "gouge", "uppercut", "cave_in", "charge", "roar", "enter", "die", "stun" };

        static (WardenPose, int, float)[][] WardenSequences()
        {
            (WardenPose, int, float)[] Run(WardenPose pose, float seconds, int from = 0, int to = -1)
            {
                int last = to < 0 ? WardenArt.FramesOf(pose) - 1 : to;
                return Enumerable.Range(from, last - from + 1).Select(f => (pose, f, seconds)).ToArray();
            }
            (WardenPose, int, float)[] Blow(WardenPose pose) =>
                Enumerable.Range(0, WardenArt.FramesOf(pose)).Select(f => (pose, f, WardenArt.FrameSeconds(pose, f))).ToArray();
            var rest = new[] { (WardenPose.Idle, 0, 0.4f) };
            (WardenPose, int, float)[] Attack(WardenPose blow, float windUp) =>
                rest.Concat(Run(WardenArt.WindUpOf(blow), windUp / WardenArt.FramesOf(WardenArt.WindUpOf(blow)))).Concat(Blow(blow)).ToArray();
            return new[]
            {
                Run(WardenPose.Walk, 0.12f).Concat(Run(WardenPose.Walk, 0.12f)).ToArray(),
                Attack(WardenPose.Slam, 1.0f),
                Attack(WardenPose.Sweep, 0.9f),
                Attack(WardenPose.Burst, 0.6f),
                Attack(WardenPose.Stomp, 1.1f),
                Attack(WardenPose.Punch, 1.1f),
                Attack(WardenPose.Gouge, 0.8f),
                Attack(WardenPose.Uppercut, 0.5f),
                rest.Concat(Run(WardenPose.Gather, 0.7f / 20)).Concat(Blow(WardenPose.Pound)).ToArray(),
                rest.Concat(Run(WardenPose.Brace, 0.12f)).Concat(Run(WardenPose.Charge, 0.05f)).Concat(Run(WardenPose.Charge, 0.05f))
                    .Concat(Enumerable.Range(0, WardenArt.FramesOf(WardenPose.Brace)).Reverse().Select(f => (WardenPose.Brace, f, 0.07f))).ToArray(),
                rest.Concat(Run(WardenPose.Roar, 0.08f, 0, WardenArt.RoarRise - 1)).Concat(Run(WardenPose.Roar, 0.1f, WardenArt.RoarRise)).Concat(Run(WardenPose.Roar, 0.1f, WardenArt.RoarRise)).ToArray(),
                new[] { (WardenPose.Enter, 0, 0.6f) }.Concat(Blow(WardenPose.Enter)).Concat(rest).ToArray(),
                rest.Concat(Run(WardenPose.Die, 0.085f)).Concat(new[] { (WardenPose.Die, WardenArt.FramesOf(WardenPose.Die) - 1, 1.2f) }).ToArray(),
                // Stunned against a pillar: out of its run, crushed and thrown back, then reeling (the loop twice).
                Run(WardenPose.Charge, 0.05f).Concat(Run(WardenPose.Stun, 0.035f, 0, WardenArt.StunHit - 1)).Concat(Run(WardenPose.Stun, 0.11f, WardenArt.StunHit))
                    .Concat(Run(WardenPose.Stun, 0.11f, WardenArt.StunHit)).ToArray(),
            };
        }

        // The body turns with the blow as WorldView turns it (AnimateWarden).
        static BossView WardenSideView(WardenPose pose, int frame)
        {
            int twist = WardenArt.TwistOf(pose, frame);
            return twist < 0 ? BossView.BackSide : twist > 0 ? BossView.FrontSide : BossView.Side;
        }

        static PixelImage OnGround(PixelImage image, PixelImage arena)
        {
            var result = new PixelImage(image.Width, image.Height);
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    var c = image[x, y];
                    var ground = arena[x % arena.Width, y % arena.Height];
                    result[x, y] = c.A == 255 ? c : c.A == 0 ? ground : ground.Mix(c.WithAlpha(255), c.A / 255f);
                }
            return result;
        }

        static void Warden(string folder, PixelImage arena)
        {
            Directory.CreateDirectory(folder);
            var rows = WardenSequences();
            const int scale = 2;
            int cellWidth = WardenArt.Width * scale, cellHeight = WardenArt.Height * scale;
            int width = cellWidth * 2, height = cellHeight * rows.Length;
            var frames = new List<(Rgba[] Pixels, int Centiseconds)>();
            float longest = rows.Max(r => r.Sum(f => f.Item3));
            var cache = new Dictionary<(BossView, WardenPose, int), PixelImage>();
            PixelImage Drawn(BossView view, WardenPose pose, int frame)
            {
                if (!cache.TryGetValue((view, pose, frame), out var image)) cache[(view, pose, frame)] = image = OnGround(WardenArt.Draw(view, pose, frame, 1), arena);
                return image;
            }
            for (float time = 0; time < longest; time += 0.02f)
            {
                var pixels = new Rgba[width * height];
                for (int row = 0; row < rows.Length; row++)
                {
                    float at = time % rows[row].Sum(f => f.Item3);
                    var step = rows[row][0];
                    foreach (var f in rows[row])
                    {
                        step = f;
                        if (at < f.Item3) break;
                        at -= f.Item3;
                    }
                    for (int column = 0; column < 2; column++)
                    {
                        var image = Drawn(column == 0 ? WardenSideView(step.Item1, step.Item2) : BossView.Front, step.Item1, step.Item2);
                        for (int y = 0; y < cellHeight; y++)
                            for (int x = 0; x < cellWidth; x++)
                                pixels[(row * cellHeight + y) * width + column * cellWidth + x] = image[x / scale, y / scale];
                    }
                }
                frames.Add((pixels, 2));
            }
            Gif.Save(Path.Combine(folder, "warden.gif"), width, height, frames);

            // Every frame of each motion in order, side (as turned) over front, each frame faintly over the one before.
            for (int r = 0; r < rows.Length; r++)
            {
                var steps = rows[r].Select(f => (f.Item1, f.Item2)).Distinct().ToList();
                var strip = new PixelImage(steps.Count * WardenArt.Width, 2 * WardenArt.Height);
                for (int k = 0; k < steps.Count; k++)
                {
                    for (int v = 0; v < 2; v++)
                    {
                        BossView ViewOf((WardenPose, int) s) => v == 0 ? WardenSideView(s.Item1, s.Item2) : BossView.Front;
                        var image = WardenArt.Draw(ViewOf(steps[k]), steps[k].Item1, steps[k].Item2, 1);
                        var before = k > 0 ? WardenArt.Draw(ViewOf(steps[k - 1]), steps[k - 1].Item1, steps[k - 1].Item2, 1) : null;
                        for (int y = 0; y < WardenArt.Height; y++)
                            for (int x = 0; x < WardenArt.Width; x++)
                            {
                                var c = image[x, y];
                                var ground = arena[x % arena.Width, y % arena.Height];
                                if (c.A == 0 && before != null && before[x, y].A > 0) c = ground.Mix(new Rgba(120, 200, 255), 0.35f);
                                strip[k * WardenArt.Width + x, v * WardenArt.Height + y] = c.A == 255 ? c : c.A == 0 ? ground : ground.Mix(c.WithAlpha(255), c.A / 255f);
                            }
                    }
                }
                Png.Save(strip, Path.Combine(folder, $"warden_frames_{WardenRows[r]}.png"));
            }

            // The key poses turned eight ways (the way WorldView picks a view and mirrors it), with the last phase's
            // red-hot core on the bottom row.
            var ways = new (BossView View, bool Flip)[]
            {
                (BossView.Side, false), (BossView.FrontSide, false), (BossView.Front, false), (BossView.FrontSide, true),
                (BossView.Side, true), (BossView.BackSide, true), (BossView.Back, false), (BossView.BackSide, false),
            };
            var poses = new (WardenPose Pose, int Frame, int Phase)[]
            {
                (WardenPose.Idle, 0, 1), (WardenPose.Raise, 13, 1), (WardenPose.Slam, WardenArt.StrikeFrame(WardenPose.Slam), 1),
                (WardenPose.Wind, 11, 1), (WardenPose.Sweep, WardenArt.StrikeFrame(WardenPose.Sweep), 1), (WardenPose.Hunch, 9, 1),
                (WardenPose.Burst, WardenArt.StrikeFrame(WardenPose.Burst), 1), (WardenPose.Lift, 9, 1), (WardenPose.Coil, 13, 1),
                (WardenPose.Scoop, 9, 1), (WardenPose.Gouge, WardenArt.StrikeFrame(WardenPose.Gouge), 1), (WardenPose.Uppercut, WardenArt.StrikeFrame(WardenPose.Uppercut), 1),
                (WardenPose.Gather, 19, 1), (WardenPose.Brace, 7, 1), (WardenPose.Roar, WardenArt.RoarRise, 1), (WardenPose.Enter, 0, 1), (WardenPose.Die, WardenArt.DieKnees, 1), (WardenPose.Die, WardenArt.FramesOf(WardenPose.Die) - 1, 1),
                (WardenPose.Stun, 8, 1), (WardenPose.Stun, WardenArt.StunHit + 2, 1), (WardenPose.Stun, WardenArt.StunHit + 2, 3), (WardenPose.Idle, 0, 3),
            };
            var turn = new PixelImage(ways.Length * WardenArt.Width, poses.Length * WardenArt.Height);
            for (int r = 0; r < poses.Length; r++)
                for (int w = 0; w < ways.Length; w++)
                    turn.Draw(OnGround(Flip(WardenArt.Draw(ways[w].View, poses[r].Pose, poses[r].Frame, poses[r].Phase), ways[w].Flip), arena), w * WardenArt.Width, r * WardenArt.Height);
            Png.Save(turn, Path.Combine(folder, "warden_turn.png"));

            // How far the fists and the body's edges move from each frame to the next on a fixed view (side and front),
            // and every step that moves more than a heavy body should.
            var report = new System.Text.StringBuilder();
            int jumps = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                var steps = rows[r].Select(f => (f.Item1, f.Item2)).ToList();
                foreach (var view in new[] { BossView.Side, BossView.Front })
                {
                    report.AppendLine($"## {WardenRows[r]} ({view})");
                    WardenArt.Joints? last = null;
                    (WardenPose, int) lastStep = default;
                    foreach (var step in steps)
                    {
                        var j = WardenArt.JointsOf(view, step.Item1, step.Item2);
                        string line = $"{step.Item1}{step.Item2,-3} fistA {j.FistAX,5:0} {j.FistAY,5:0} fistB {j.FistBX,5:0} {j.FistBY,5:0} body {j.Left}-{j.Right} {j.Top}-{j.Bottom}";
                        if (last is WardenArt.Joints l && !(lastStep.Item1 == step.Item1 && lastStep.Item2 == step.Item2))
                        {
                            float a = Distance(j.FistAX - l.FistAX, j.FistAY - l.FistAY), b = Distance(j.FistBX - l.FistBX, j.FistBY - l.FistBY);
                            int top = Math.Abs(j.Top - l.Top), bottom = Math.Abs(j.Bottom - l.Bottom);
                            // A swing may move fast and the strike squashes the body at once; the entrance's sleeping heap
                            // is a still frame held before the first stir.
                            bool swing = WardenArt.Swinging(step.Item1, step.Item2);
                            bool strike = step.Item2 == WardenArt.StrikeFrame(step.Item1);
                            float fist = swing ? 26 : 7;
                            bool crumbling = step.Item1 == WardenPose.Die && step.Item2 > WardenArt.DieCrumble;
                            bool jump = a > fist || b > fist || bottom > (strike ? 10 : crumbling ? 8 : 4) || top > (strike || swing ? 16 : 6);
                            line += $"   move fists {a,4:0.0} {b,4:0.0} top {top} bottom {bottom}" + (jump ? "   <<< JUMP" : "");
                            if (jump) jumps++;
                        }
                        report.AppendLine(line);
                        last = j;
                        lastStep = step;
                    }
                }
            }
            report.Insert(0, $"jumps: {jumps}\n\n");
            File.WriteAllText(Path.Combine(folder, "warden_continuity.txt"), report.ToString());
            Console.WriteLine($"warden continuity: {jumps} jumps (anim/warden_continuity.txt)");
        }
    }
}
