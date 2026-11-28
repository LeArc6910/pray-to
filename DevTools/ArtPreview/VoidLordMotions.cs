using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Client
{
    // The void lord's motions (2026-11-28, VoidLordArt), each as WorldView plays it: idle into the wind-up, the blow, back
    // to rest; one row each, side and front. Every frame of each motion on a strip (void_lord_frames_<motion>.png), the
    // poses turned eight ways (void_lord_turn.png), and how far the head and the body move from each frame to the next
    // (void_lord_continuity.txt).
    static partial class Motions
    {
        static readonly string[] VoidLordRows = { "walk", "grasp", "beam", "star_fall", "void_ring", "blink", "collapse", "void_cross", "roar", "enter" };

        static (VoidLordPose, int, float)[][] VoidLordSequences()
        {
            (VoidLordPose, int, float)[] Run(VoidLordPose pose, float seconds, int from = 0, int to = -1)
            {
                int last = to < 0 ? VoidLordArt.FramesOf(pose) - 1 : to;
                return Enumerable.Range(from, last - from + 1).Select(f => (pose, f, seconds)).ToArray();
            }
            (VoidLordPose, int, float)[] Blow(VoidLordPose pose) =>
                Enumerable.Range(0, VoidLordArt.FramesOf(pose)).Select(f => (pose, f, VoidLordArt.FrameSeconds(pose, f))).ToArray();
            var rest = new[] { (VoidLordPose.Idle, 0, 0.4f) };
            (VoidLordPose, int, float)[] Attack(VoidLordPose blow, float windUp) =>
                rest.Concat(Run(VoidLordArt.WindUpOf(blow), windUp / VoidLordArt.FramesOf(VoidLordArt.WindUpOf(blow)))).Concat(Blow(blow)).ToArray();
            return new[]
            {
                Run(VoidLordPose.Walk, 0.09f).Concat(Run(VoidLordPose.Walk, 0.09f)).ToArray(),
                Attack(VoidLordPose.Grasp, 0.8f),
                Attack(VoidLordPose.Cast, 0.9f),
                Attack(VoidLordPose.Starfall, 1.2f),
                Attack(VoidLordPose.Burst, 0.6f),
                Attack(VoidLordPose.Blink, 0.9f),
                Attack(VoidLordPose.Crush, 1.0f),
                Attack(VoidLordPose.Smite, 1.0f),
                rest.Concat(Run(VoidLordPose.Roar, 0.06f, 0, VoidLordArt.RoarRise - 1)).Concat(Run(VoidLordPose.Roar, 0.08f, VoidLordArt.RoarRise)).Concat(Run(VoidLordPose.Roar, 0.08f, VoidLordArt.RoarRise)).ToArray(),
                new[] { (VoidLordPose.Enter, 0, 0.6f) }.Concat(Blow(VoidLordPose.Enter)).Concat(rest).ToArray(),
            };
        }

        static void VoidLord(string folder, PixelImage arena)
        {
            Directory.CreateDirectory(folder);
            var rows = VoidLordSequences();
            const int scale = 2;
            int cellWidth = VoidLordArt.Width * scale, cellHeight = VoidLordArt.Height * scale;
            int width = cellWidth * 2, height = cellHeight * rows.Length;
            var frames = new List<(Rgba[] Pixels, int Centiseconds)>();
            float longest = rows.Max(r => r.Sum(f => f.Item3));
            var cache = new Dictionary<(BossView, VoidLordPose, int), PixelImage>();
            PixelImage Drawn(BossView view, VoidLordPose pose, int frame)
            {
                if (!cache.TryGetValue((view, pose, frame), out var image)) cache[(view, pose, frame)] = image = OnGround(VoidLordArt.Draw(view, pose, frame, 1), arena);
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
                        var image = Drawn(column == 0 ? BossView.Side : BossView.Front, step.Item1, step.Item2);
                        for (int y = 0; y < cellHeight; y++)
                            for (int x = 0; x < cellWidth; x++)
                                pixels[(row * cellHeight + y) * width + column * cellWidth + x] = image[x / scale, y / scale];
                    }
                }
                frames.Add((pixels, 2));
            }
            Gif.Save(Path.Combine(folder, "void_lord.gif"), width, height, frames);

            // Every frame of each motion in order, side over front, each frame faintly over the one before.
            for (int r = 0; r < rows.Length; r++)
            {
                var steps = rows[r].Select(f => (f.Item1, f.Item2)).Distinct().ToList();
                var strip = new PixelImage(steps.Count * VoidLordArt.Width, 2 * VoidLordArt.Height);
                for (int k = 0; k < steps.Count; k++)
                {
                    for (int v = 0; v < 2; v++)
                    {
                        var view = v == 0 ? BossView.Side : BossView.Front;
                        var image = VoidLordArt.Draw(view, steps[k].Item1, steps[k].Item2, 1);
                        var before = k > 0 ? VoidLordArt.Draw(view, steps[k - 1].Item1, steps[k - 1].Item2, 1) : null;
                        for (int y = 0; y < VoidLordArt.Height; y++)
                            for (int x = 0; x < VoidLordArt.Width; x++)
                            {
                                var c = image[x, y];
                                var ground = arena[x % arena.Width, y % arena.Height];
                                if (c.A == 0 && before != null && before[x, y].A > 0) c = ground.Mix(new Rgba(190, 150, 255), 0.35f);
                                strip[k * VoidLordArt.Width + x, v * VoidLordArt.Height + y] = c.A == 255 ? c : c.A == 0 ? ground : ground.Mix(c.WithAlpha(255), c.A / 255f);
                            }
                    }
                }
                Png.Save(strip, Path.Combine(folder, $"void_lord_frames_{VoidLordRows[r]}.png"));
            }

            VoidLordTurn(Path.Combine(folder, "void_lord_turn.png"), arena);
            VoidLordPatterns(folder, arena);

            // How far the head and the body's edges move from each frame to the next on a fixed view (side and front).
            var report = new System.Text.StringBuilder();
            int jumps = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                var steps = rows[r].Select(f => (f.Item1, f.Item2)).ToList();
                foreach (var view in new[] { BossView.Side, BossView.Front })
                {
                    report.AppendLine($"## {VoidLordRows[r]} ({view})");
                    VoidLordArt.Joints? last = null;
                    (VoidLordPose, int) lastStep = default;
                    foreach (var step in steps)
                    {
                        var j = VoidLordArt.JointsOf(view, step.Item1, step.Item2);
                        string line = $"{step.Item1}{step.Item2,-3} head {j.HeadX,5:0} {j.HeadY,5:0} body {j.Left}-{j.Right} {j.Top}-{j.Bottom}";
                        if (last is VoidLordArt.Joints l && !(lastStep.Item1 == step.Item1 && lastStep.Item2 == step.Item2) && j.Bottom >= 0 && l.Bottom >= 0)
                        {
                            float head = Distance(j.HeadX - l.HeadX, j.HeadY - l.HeadY);
                            int top = Math.Abs(j.Top - l.Top), bottom = Math.Abs(j.Bottom - l.Bottom);
                            bool swing = VoidLordArt.Swinging(step.Item1, step.Item2);
                            bool strike = step.Item2 == VoidLordArt.StrikeFrame(step.Item1);
                            // A lunge or a blink is too fast to see: the body may move far in it (or be gone).
                            // Blinking he is gone into motes: only a few of them left to measure.
                            bool gone = step.Item1 == VoidLordPose.Blink && step.Item2 <= VoidLordArt.StrikeFrame(VoidLordPose.Blink) + 2;
                            bool jump = !gone && (head > (swing ? 14 : 5) || bottom > (swing ? 16 : 4) || top > (swing ? 16 : 6));
                            line += $"   move head {head,4:0.0} top {top} bottom {bottom}" + (jump ? "   <<< JUMP" : "");
                            if (jump) jumps++;
                        }
                        report.AppendLine(line);
                        last = j;
                        lastStep = step;
                    }
                }
            }
            report.Insert(0, $"jumps: {jumps}\n\n");
            File.WriteAllText(Path.Combine(folder, "void_lord_continuity.txt"), report.ToString());
            Console.WriteLine($"void lord continuity: {jumps} jumps (anim/void_lord_continuity.txt)");
        }

        // The key poses turned eight ways (the way WorldView picks a view and mirrors it), with the last phase's
        // violet crown, eyes and orb on the bottom row.
        static void VoidLordTurn(string path, PixelImage arena)
        {
            var ways = new (BossView View, bool Flip)[]
            {
                (BossView.Side, false), (BossView.FrontSide, false), (BossView.Front, false), (BossView.FrontSide, true),
                (BossView.Side, true), (BossView.BackSide, true), (BossView.Back, false), (BossView.BackSide, false),
            };
            int Last(VoidLordPose pose) => VoidLordArt.FramesOf(pose) - 1;
            int Strike(VoidLordPose pose) => VoidLordArt.StrikeFrame(pose);
            var poses = new (VoidLordPose Pose, int Frame, int Phase)[]
            {
                (VoidLordPose.Idle, 0, 1), (VoidLordPose.Reach, Last(VoidLordPose.Reach), 1), (VoidLordPose.Grasp, Strike(VoidLordPose.Grasp), 1),
                (VoidLordPose.Gather, Last(VoidLordPose.Gather), 1), (VoidLordPose.Cast, Strike(VoidLordPose.Cast), 1), (VoidLordPose.Exalt, Last(VoidLordPose.Exalt), 1),
                (VoidLordPose.Starfall, Strike(VoidLordPose.Starfall), 1), (VoidLordPose.Spread, Last(VoidLordPose.Spread), 1), (VoidLordPose.Burst, Strike(VoidLordPose.Burst), 1),
                (VoidLordPose.Fade, Last(VoidLordPose.Fade), 1), (VoidLordPose.Blink, Strike(VoidLordPose.Blink) + 1, 1), (VoidLordPose.Clench, Last(VoidLordPose.Clench), 1),
                (VoidLordPose.Crush, Strike(VoidLordPose.Crush), 1), (VoidLordPose.Raise, Last(VoidLordPose.Raise), 1), (VoidLordPose.Smite, Strike(VoidLordPose.Smite), 1),
                (VoidLordPose.Roar, VoidLordArt.RoarRise, 1), (VoidLordPose.Enter, VoidLordArt.EnterBurst - 2, 1), (VoidLordPose.Idle, 0, 3),
            };
            var turn = new PixelImage(ways.Length * VoidLordArt.Width, poses.Length * VoidLordArt.Height);
            for (int r = 0; r < poses.Length; r++)
                for (int w = 0; w < ways.Length; w++)
                    turn.Draw(OnGround(Flip(VoidLordArt.Draw(ways[w].View, poses[r].Pose, poses[r].Frame, poses[r].Phase), ways[w].Flip), arena), w * VoidLordArt.Width, r * VoidLordArt.Height);
            Png.Save(turn, path);
        }
    }
}
