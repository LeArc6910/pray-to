using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Client
{
    // The antlion's motions (2026-11-27, AntlionArt), each as WorldView plays it: idle into the wind-up, the blow, back
    // to rest; one row each, side and front. Every frame of each motion on a strip (antlion_frames_<motion>.png), the
    // poses turned eight ways (antlion_turn.png), and how far the head and the body move from each frame to the next
    // (antlion_continuity.txt).
    static partial class Motions
    {
        static readonly string[] AntlionRows = { "walk", "bite", "sand_jet", "pit", "sand_trail", "burrow", "quicksand", "sandstorm", "sand_cross", "sinkholes", "roar", "enter" };

        static (AntlionPose, int, float)[][] AntlionSequences()
        {
            (AntlionPose, int, float)[] Run(AntlionPose pose, float seconds, int from = 0, int to = -1)
            {
                int last = to < 0 ? AntlionArt.FramesOf(pose) - 1 : to;
                return Enumerable.Range(from, last - from + 1).Select(f => (pose, f, seconds)).ToArray();
            }
            (AntlionPose, int, float)[] Blow(AntlionPose pose) =>
                Enumerable.Range(0, AntlionArt.FramesOf(pose)).Select(f => (pose, f, AntlionArt.FrameSeconds(pose, f))).ToArray();
            var rest = new[] { (AntlionPose.Idle, 0, 0.4f) };
            (AntlionPose, int, float)[] Attack(AntlionPose blow, float windUp) =>
                rest.Concat(Run(AntlionArt.WindUpOf(blow), windUp / AntlionArt.FramesOf(AntlionArt.WindUpOf(blow)))).Concat(Blow(blow)).ToArray();
            return new[]
            {
                Run(AntlionPose.Walk, 0.09f).Concat(Run(AntlionPose.Walk, 0.09f)).ToArray(),
                Attack(AntlionPose.Snap, 0.4f),
                Attack(AntlionPose.Flick, 0.5f),
                Attack(AntlionPose.Toss, 0.7f),
                Attack(AntlionPose.Gouge, 0.4f),
                rest.Concat(Run(AntlionPose.Dive, 0.6f / 12)).Concat(Run(AntlionPose.Burrow, 0.06f)).Concat(Run(AntlionPose.Burrow, 0.06f)).Concat(Blow(AntlionPose.Emerge)).ToArray(),
                Attack(AntlionPose.Churn, 0.8f),
                Attack(AntlionPose.Crash, 0.9f),
                Attack(AntlionPose.Clap, 0.7f),
                Attack(AntlionPose.Erupt, 1.2f),
                rest.Concat(Run(AntlionPose.Roar, 0.06f, 0, AntlionArt.RoarRise - 1)).Concat(Run(AntlionPose.Roar, 0.08f, AntlionArt.RoarRise)).Concat(Run(AntlionPose.Roar, 0.08f, AntlionArt.RoarRise)).ToArray(),
                new[] { (AntlionPose.Enter, 0, 0.6f) }.Concat(Blow(AntlionPose.Enter)).Concat(rest).ToArray(),
            };
        }

        static void Antlion(string folder, PixelImage arena)
        {
            Directory.CreateDirectory(folder);
            var rows = AntlionSequences();
            const int scale = 2;
            int cellWidth = AntlionArt.Width * scale, cellHeight = AntlionArt.Height * scale;
            int width = cellWidth * 2, height = cellHeight * rows.Length;
            var frames = new List<(Rgba[] Pixels, int Centiseconds)>();
            float longest = rows.Max(r => r.Sum(f => f.Item3));
            var cache = new Dictionary<(BossView, AntlionPose, int), PixelImage>();
            PixelImage Drawn(BossView view, AntlionPose pose, int frame)
            {
                if (!cache.TryGetValue((view, pose, frame), out var image)) cache[(view, pose, frame)] = image = OnGround(AntlionArt.Draw(view, pose, frame, 1), arena);
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
            Gif.Save(Path.Combine(folder, "antlion.gif"), width, height, frames);

            // Every frame of each motion in order, side over front, each frame faintly over the one before.
            for (int r = 0; r < rows.Length; r++)
            {
                var steps = rows[r].Select(f => (f.Item1, f.Item2)).Distinct().ToList();
                var strip = new PixelImage(steps.Count * AntlionArt.Width, 2 * AntlionArt.Height);
                for (int k = 0; k < steps.Count; k++)
                {
                    for (int v = 0; v < 2; v++)
                    {
                        var view = v == 0 ? BossView.Side : BossView.Front;
                        var image = AntlionArt.Draw(view, steps[k].Item1, steps[k].Item2, 1);
                        var before = k > 0 ? AntlionArt.Draw(view, steps[k - 1].Item1, steps[k - 1].Item2, 1) : null;
                        for (int y = 0; y < AntlionArt.Height; y++)
                            for (int x = 0; x < AntlionArt.Width; x++)
                            {
                                var c = image[x, y];
                                var ground = arena[x % arena.Width, y % arena.Height];
                                if (c.A == 0 && before != null && before[x, y].A > 0) c = ground.Mix(new Rgba(120, 200, 255), 0.35f);
                                strip[k * AntlionArt.Width + x, v * AntlionArt.Height + y] = c.A == 255 ? c : c.A == 0 ? ground : ground.Mix(c.WithAlpha(255), c.A / 255f);
                            }
                    }
                }
                Png.Save(strip, Path.Combine(folder, $"antlion_frames_{AntlionRows[r]}.png"));
            }

            AntlionTurn(Path.Combine(folder, "antlion_turn.png"), arena);

            // How far the head and the body's edges move from each frame to the next on a fixed view (side and front).
            var report = new System.Text.StringBuilder();
            int jumps = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                var steps = rows[r].Select(f => (f.Item1, f.Item2)).ToList();
                foreach (var view in new[] { BossView.Side, BossView.Front })
                {
                    report.AppendLine($"## {AntlionRows[r]} ({view})");
                    AntlionArt.Joints? last = null;
                    (AntlionPose, int) lastStep = default;
                    foreach (var step in steps)
                    {
                        var j = AntlionArt.JointsOf(view, step.Item1, step.Item2);
                        string line = $"{step.Item1}{step.Item2,-3} head {j.HeadX,5:0} {j.HeadY,5:0} body {j.Left}-{j.Right} {j.Top}-{j.Bottom}";
                        if (last is AntlionArt.Joints l && !(lastStep.Item1 == step.Item1 && lastStep.Item2 == step.Item2) && j.Bottom >= 0 && l.Bottom >= 0)
                        {
                            float head = Distance(j.HeadX - l.HeadX, j.HeadY - l.HeadY);
                            int top = Math.Abs(j.Top - l.Top), bottom = Math.Abs(j.Bottom - l.Bottom);
                            bool swing = AntlionArt.Swinging(step.Item1, step.Item2);
                            bool strike = step.Item2 == AntlionArt.StrikeFrame(step.Item1);
                            bool jump = head > (swing ? 14 : 5) || bottom > (strike ? 8 : 4) || top > (strike || swing ? 14 : 6);
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
            File.WriteAllText(Path.Combine(folder, "antlion_continuity.txt"), report.ToString());
            Console.WriteLine($"antlion continuity: {jumps} jumps (anim/antlion_continuity.txt)");
        }

        // The key poses turned eight ways (the way WorldView picks a view and mirrors it), with the last phase's
        // stained jaws on the bottom row.
        static void AntlionTurn(string path, PixelImage arena)
        {
            var ways = new (BossView View, bool Flip)[]
            {
                (BossView.Side, false), (BossView.FrontSide, false), (BossView.Front, false), (BossView.FrontSide, true),
                (BossView.Side, true), (BossView.BackSide, true), (BossView.Back, false), (BossView.BackSide, false),
            };
            int Last(AntlionPose pose) => AntlionArt.FramesOf(pose) - 1;
            int Strike(AntlionPose pose) => AntlionArt.StrikeFrame(pose);
            var poses = new (AntlionPose Pose, int Frame, int Phase)[]
            {
                (AntlionPose.Idle, 0, 1), (AntlionPose.Gape, Last(AntlionPose.Gape), 1), (AntlionPose.Snap, Strike(AntlionPose.Snap), 1),
                (AntlionPose.Scoop, Last(AntlionPose.Scoop), 1), (AntlionPose.Flick, Strike(AntlionPose.Flick), 1), (AntlionPose.Dig, Last(AntlionPose.Dig), 1),
                (AntlionPose.Plow, Last(AntlionPose.Plow), 1), (AntlionPose.Dive, Last(AntlionPose.Dive), 1), (AntlionPose.Sink, Last(AntlionPose.Sink), 1),
                (AntlionPose.Rear, Last(AntlionPose.Rear), 1), (AntlionPose.Crash, Strike(AntlionPose.Crash), 1), (AntlionPose.Spread, Last(AntlionPose.Spread), 1),
                (AntlionPose.Submerge, Last(AntlionPose.Submerge), 1), (AntlionPose.Erupt, Strike(AntlionPose.Erupt), 1), (AntlionPose.Roar, AntlionArt.RoarRise, 1),
                (AntlionPose.Enter, 0, 1), (AntlionPose.Idle, 0, 3),
            };
            var turn = new PixelImage(ways.Length * AntlionArt.Width, poses.Length * AntlionArt.Height);
            for (int r = 0; r < poses.Length; r++)
                for (int w = 0; w < ways.Length; w++)
                    turn.Draw(OnGround(Flip(AntlionArt.Draw(ways[w].View, poses[r].Pose, poses[r].Frame, poses[r].Phase), ways[w].Flip), arena), w * AntlionArt.Width, r * AntlionArt.Height);
            Png.Save(turn, path);
        }
    }
}
