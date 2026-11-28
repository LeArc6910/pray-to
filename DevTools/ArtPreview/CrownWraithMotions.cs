using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Client
{
    // The crown wraith's motions (2026-11-28, CrownWraithArt), each as WorldView plays it: idle into the wind-up, the blow,
    // back to rest; one row each, side and front. Every frame of each motion on a strip (crown_wraith_frames_<motion>.png),
    // the poses turned eight ways (crown_wraith_turn.png), and how far the head, the first hand and the body move from
    // each frame to the next (crown_wraith_continuity.txt).
    static partial class Motions
    {
        static readonly string[] WraithRows = { "walk", "grasp", "lightning_fall", "soul_lance", "phantom_rush", "crown_ring", "storm_brand", "deep_share", "soul_ripple", "roar", "enter", "die" };

        static (CrownWraithPose, int, float)[][] WraithSequences()
        {
            (CrownWraithPose, int, float)[] Run(CrownWraithPose pose, float seconds, int from = 0, int to = -1)
            {
                int last = to < 0 ? CrownWraithArt.FramesOf(pose) - 1 : to;
                return Enumerable.Range(from, last - from + 1).Select(f => (pose, f, seconds)).ToArray();
            }
            (CrownWraithPose, int, float)[] Blow(CrownWraithPose pose) =>
                Enumerable.Range(0, CrownWraithArt.FramesOf(pose)).Select(f => (pose, f, CrownWraithArt.FrameSeconds(pose, f))).ToArray();
            var rest = new[] { (CrownWraithPose.Idle, 0, 0.4f) };
            (CrownWraithPose, int, float)[] Attack(CrownWraithPose blow, float windUp) =>
                rest.Concat(Run(CrownWraithArt.WindUpOf(blow), windUp / CrownWraithArt.FramesOf(CrownWraithArt.WindUpOf(blow)))).Concat(Blow(blow)).ToArray();
            return new[]
            {
                Run(CrownWraithPose.Walk, 0.09f).Concat(Run(CrownWraithPose.Walk, 0.09f)).ToArray(),
                Attack(CrownWraithPose.Rake, 0.6f),
                Attack(CrownWraithPose.Call, 1.0f),
                Attack(CrownWraithPose.Hurl, 0.7f),
                Attack(CrownWraithPose.Rush, 0.8f),
                Attack(CrownWraithPose.Flare, 1.0f),
                Attack(CrownWraithPose.Brand, 1.4f),
                Attack(CrownWraithPose.Smite, 1.1f),
                Attack(CrownWraithPose.Pulse, 0.5f),
                rest.Concat(Run(CrownWraithPose.Roar, 0.06f, 0, CrownWraithArt.RoarRise - 1)).Concat(Run(CrownWraithPose.Roar, 0.08f, CrownWraithArt.RoarRise)).Concat(Run(CrownWraithPose.Roar, 0.08f, CrownWraithArt.RoarRise)).ToArray(),
                new[] { (CrownWraithPose.Enter, 0, 0.6f) }.Concat(Blow(CrownWraithPose.Enter)).Concat(rest).ToArray(),
                rest.Concat(Blow(CrownWraithPose.Die)).Concat(new[] { (CrownWraithPose.Die, CrownWraithArt.FramesOf(CrownWraithPose.Die) - 1, 0.8f) }).ToArray(),
            };
        }

        static void CrownWraith(string folder, PixelImage arena)
        {
            Directory.CreateDirectory(folder);
            var rows = WraithSequences();
            const int scale = 2;
            int cellWidth = CrownWraithArt.Width * scale, cellHeight = CrownWraithArt.Height * scale;
            int width = cellWidth * 2, height = cellHeight * rows.Length;
            var frames = new List<(Rgba[] Pixels, int Centiseconds)>();
            float longest = rows.Max(r => r.Sum(f => f.Item3));
            var cache = new Dictionary<(BossView, CrownWraithPose, int), PixelImage>();
            PixelImage Drawn(BossView view, CrownWraithPose pose, int frame)
            {
                if (!cache.TryGetValue((view, pose, frame), out var image)) cache[(view, pose, frame)] = image = OnGround(CrownWraithArt.Draw(view, pose, frame, 1), arena);
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
            Gif.Save(Path.Combine(folder, "crown_wraith.gif"), width, height, frames);

            // Every frame of each motion in order, side over front, each frame faintly over the one before.
            for (int r = 0; r < rows.Length; r++)
            {
                var steps = rows[r].Select(f => (f.Item1, f.Item2)).Distinct().ToList();
                var strip = new PixelImage(steps.Count * CrownWraithArt.Width, 2 * CrownWraithArt.Height);
                for (int k = 0; k < steps.Count; k++)
                {
                    for (int v = 0; v < 2; v++)
                    {
                        var view = v == 0 ? BossView.Side : BossView.Front;
                        var image = CrownWraithArt.Draw(view, steps[k].Item1, steps[k].Item2, 1);
                        var before = k > 0 ? CrownWraithArt.Draw(view, steps[k - 1].Item1, steps[k - 1].Item2, 1) : null;
                        for (int y = 0; y < CrownWraithArt.Height; y++)
                            for (int x = 0; x < CrownWraithArt.Width; x++)
                            {
                                var c = image[x, y];
                                var ground = arena[x % arena.Width, y % arena.Height];
                                if (c.A == 0 && before != null && before[x, y].A > 0) c = ground.Mix(new Rgba(255, 230, 120), 0.35f);
                                strip[k * CrownWraithArt.Width + x, v * CrownWraithArt.Height + y] = c.A == 255 ? c : c.A == 0 ? ground : ground.Mix(c.WithAlpha(255), c.A / 255f);
                            }
                    }
                }
                Png.Save(strip, Path.Combine(folder, $"crown_wraith_frames_{WraithRows[r]}.png"));
            }

            WraithTurn(Path.Combine(folder, "crown_wraith_turn.png"), arena);
            WraithPatterns(folder, arena);

            // How far the head and the body's edges move from each frame to the next on a fixed view (side and front).
            var report = new System.Text.StringBuilder();
            int jumps = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                var steps = rows[r].Select(f => (f.Item1, f.Item2)).ToList();
                foreach (var view in new[] { BossView.Side, BossView.Front })
                {
                    report.AppendLine($"## {WraithRows[r]} ({view})");
                    CrownWraithArt.Joints? last = null;
                    (CrownWraithPose, int) lastStep = default;
                    foreach (var step in steps)
                    {
                        var j = CrownWraithArt.JointsOf(view, step.Item1, step.Item2);
                        string line = $"{step.Item1}{step.Item2,-3} head {j.HeadX,5:0} {j.HeadY,5:0} hand {j.HandX,5:0} {j.HandY,5:0} body {j.Left}-{j.Right} {j.Top}-{j.Bottom}";
                        if (last is CrownWraithArt.Joints l && !(lastStep.Item1 == step.Item1 && lastStep.Item2 == step.Item2) && j.Bottom >= 0 && l.Bottom >= 0)
                        {
                            float head = Distance(j.HeadX - l.HeadX, j.HeadY - l.HeadY), hand = Distance(j.HandX - l.HandX, j.HandY - l.HandY);
                            int top = Math.Abs(j.Top - l.Top), bottom = Math.Abs(j.Bottom - l.Bottom);
                            bool swing = CrownWraithArt.Swinging(step.Item1, step.Item2);
                            bool strike = step.Item2 == CrownWraithArt.StrikeFrame(step.Item1);
                            // A lunge is too fast to see: the body may move far in it. The body gathering out of the mist and
                            // coming apart into it changes its edges on purpose (the entrance and the fall).
                            bool forming = step.Item1 == CrownWraithPose.Enter || step.Item1 == CrownWraithPose.Die;
                            bool jump = head > (swing ? 14 : 5) || hand > (swing ? 22 : 7) || !forming && (bottom > (swing ? 16 : 4) || top > (swing ? 16 : 7));
                            line += $"   move head {head,4:0.0} hand {hand,4:0.0} top {top} bottom {bottom}" + (jump ? "   <<< JUMP" : "");
                            if (jump) jumps++;
                        }
                        report.AppendLine(line);
                        last = j;
                        lastStep = step;
                    }
                }
            }
            report.Insert(0, $"jumps: {jumps}\n\n");
            File.WriteAllText(Path.Combine(folder, "crown_wraith_continuity.txt"), report.ToString());
            Console.WriteLine($"crown wraith continuity: {jumps} jumps (anim/crown_wraith_continuity.txt)");
        }

        // The key poses turned eight ways (the way WorldView picks a view and mirrors it), with the last phase's
        // white eyes and sparking crown on the bottom row.
        static void WraithTurn(string path, PixelImage arena)
        {
            var ways = new (BossView View, bool Flip)[]
            {
                (BossView.Side, false), (BossView.FrontSide, false), (BossView.Front, false), (BossView.FrontSide, true),
                (BossView.Side, true), (BossView.BackSide, true), (BossView.Back, false), (BossView.BackSide, false),
            };
            int Last(CrownWraithPose pose) => CrownWraithArt.FramesOf(pose) - 1;
            int Strike(CrownWraithPose pose) => CrownWraithArt.StrikeFrame(pose);
            var poses = new (CrownWraithPose Pose, int Frame, int Phase)[]
            {
                (CrownWraithPose.Idle, 0, 1), (CrownWraithPose.Walk, 2, 1), (CrownWraithPose.Clutch, Last(CrownWraithPose.Clutch), 1), (CrownWraithPose.Rake, Strike(CrownWraithPose.Rake), 1),
                (CrownWraithPose.Beckon, Last(CrownWraithPose.Beckon), 1), (CrownWraithPose.Call, Strike(CrownWraithPose.Call), 1), (CrownWraithPose.Draw, Last(CrownWraithPose.Draw), 1),
                (CrownWraithPose.Hurl, Strike(CrownWraithPose.Hurl), 1), (CrownWraithPose.Hunch, Last(CrownWraithPose.Hunch), 1), (CrownWraithPose.Rush, Strike(CrownWraithPose.Rush), 1),
                (CrownWraithPose.Lift, Last(CrownWraithPose.Lift), 1), (CrownWraithPose.Flare, Strike(CrownWraithPose.Flare), 1), (CrownWraithPose.Storm, Last(CrownWraithPose.Storm), 1),
                (CrownWraithPose.Brand, Strike(CrownWraithPose.Brand), 1), (CrownWraithPose.Loom, Last(CrownWraithPose.Loom), 1), (CrownWraithPose.Smite, Strike(CrownWraithPose.Smite), 1),
                (CrownWraithPose.Clench, Last(CrownWraithPose.Clench), 1), (CrownWraithPose.Pulse, Strike(CrownWraithPose.Pulse), 1), (CrownWraithPose.Roar, CrownWraithArt.RoarRise, 1),
                (CrownWraithPose.Enter, 0, 1), (CrownWraithPose.Enter, CrownWraithArt.EnterStrike + 4, 1), (CrownWraithPose.Die, CrownWraithArt.FallCrownDown - 8, 1), (CrownWraithPose.Idle, 0, 3),
            };
            var turn = new PixelImage(ways.Length * CrownWraithArt.Width, poses.Length * CrownWraithArt.Height);
            for (int r = 0; r < poses.Length; r++)
                for (int w = 0; w < ways.Length; w++)
                    turn.Draw(OnGround(Flip(CrownWraithArt.Draw(ways[w].View, poses[r].Pose, poses[r].Frame, poses[r].Phase), ways[w].Flip), arena), w * CrownWraithArt.Width, r * CrownWraithArt.Height);
            Png.Save(turn, path);
        }
    }
}
