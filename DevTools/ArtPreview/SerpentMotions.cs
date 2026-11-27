using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Client
{
    // The tide serpent's motions (2026-11-28, SerpentArt), each as WorldView plays it: idle into the wind-up, the blow, back
    // to rest; one row each, side and front. Every frame of each motion on a strip (serpent_frames_<motion>.png), the
    // poses turned eight ways (serpent_turn.png), and how far the head and the body move from each frame to the next
    // (serpent_continuity.txt).
    static partial class Motions
    {
        static readonly string[] SerpentRows = { "walk", "tail_sweep", "water_jet", "bubble_burst", "tidal_trail", "whirlpool", "deep_share", "tidal_cross", "spray_rain", "bubble_spiral", "roar", "enter" };

        static (SerpentPose, int, float)[][] SerpentSequences()
        {
            (SerpentPose, int, float)[] Run(SerpentPose pose, float seconds, int from = 0, int to = -1)
            {
                int last = to < 0 ? SerpentArt.FramesOf(pose) - 1 : to;
                return Enumerable.Range(from, last - from + 1).Select(f => (pose, f, seconds)).ToArray();
            }
            (SerpentPose, int, float)[] Blow(SerpentPose pose) =>
                Enumerable.Range(0, SerpentArt.FramesOf(pose)).Select(f => (pose, f, SerpentArt.FrameSeconds(pose, f))).ToArray();
            var rest = new[] { (SerpentPose.Idle, 0, 0.4f) };
            (SerpentPose, int, float)[] Attack(SerpentPose blow, float windUp) =>
                rest.Concat(Run(SerpentArt.WindUpOf(blow), windUp / SerpentArt.FramesOf(SerpentArt.WindUpOf(blow)))).Concat(Blow(blow)).ToArray();
            return new[]
            {
                Run(SerpentPose.Walk, 0.09f).Concat(Run(SerpentPose.Walk, 0.09f)).ToArray(),
                Attack(SerpentPose.Lash, 0.6f),
                Attack(SerpentPose.Spit, 0.7f),
                Attack(SerpentPose.Burst, 0.5f),
                Attack(SerpentPose.Surge, 0.7f),
                Attack(SerpentPose.Whirl, 1.0f),
                Attack(SerpentPose.Crash, 1.1f),
                Attack(SerpentPose.Slam, 1.0f),
                Attack(SerpentPose.Spout, 1.4f),
                Attack(SerpentPose.Burst, 0.5f),
                rest.Concat(Run(SerpentPose.Roar, 0.06f, 0, SerpentArt.RoarRise - 1)).Concat(Run(SerpentPose.Roar, 0.08f, SerpentArt.RoarRise)).Concat(Run(SerpentPose.Roar, 0.08f, SerpentArt.RoarRise)).ToArray(),
                new[] { (SerpentPose.Enter, 0, 0.6f) }.Concat(Blow(SerpentPose.Enter)).Concat(rest).ToArray(),
            };
        }

        static void Serpent(string folder, PixelImage arena)
        {
            Directory.CreateDirectory(folder);
            var rows = SerpentSequences();
            const int scale = 2;
            int cellWidth = SerpentArt.Width * scale, cellHeight = SerpentArt.Height * scale;
            int width = cellWidth * 2, height = cellHeight * rows.Length;
            var frames = new List<(Rgba[] Pixels, int Centiseconds)>();
            float longest = rows.Max(r => r.Sum(f => f.Item3));
            var cache = new Dictionary<(BossView, SerpentPose, int), PixelImage>();
            PixelImage Drawn(BossView view, SerpentPose pose, int frame)
            {
                if (!cache.TryGetValue((view, pose, frame), out var image)) cache[(view, pose, frame)] = image = OnGround(SerpentArt.Draw(view, pose, frame, 1), arena);
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
            Gif.Save(Path.Combine(folder, "serpent.gif"), width, height, frames);

            // Every frame of each motion in order, side over front, each frame faintly over the one before.
            for (int r = 0; r < rows.Length; r++)
            {
                var steps = rows[r].Select(f => (f.Item1, f.Item2)).Distinct().ToList();
                var strip = new PixelImage(steps.Count * SerpentArt.Width, 2 * SerpentArt.Height);
                for (int k = 0; k < steps.Count; k++)
                {
                    for (int v = 0; v < 2; v++)
                    {
                        var view = v == 0 ? BossView.Side : BossView.Front;
                        var image = SerpentArt.Draw(view, steps[k].Item1, steps[k].Item2, 1);
                        var before = k > 0 ? SerpentArt.Draw(view, steps[k - 1].Item1, steps[k - 1].Item2, 1) : null;
                        for (int y = 0; y < SerpentArt.Height; y++)
                            for (int x = 0; x < SerpentArt.Width; x++)
                            {
                                var c = image[x, y];
                                var ground = arena[x % arena.Width, y % arena.Height];
                                if (c.A == 0 && before != null && before[x, y].A > 0) c = ground.Mix(new Rgba(120, 200, 255), 0.35f);
                                strip[k * SerpentArt.Width + x, v * SerpentArt.Height + y] = c.A == 255 ? c : c.A == 0 ? ground : ground.Mix(c.WithAlpha(255), c.A / 255f);
                            }
                    }
                }
                Png.Save(strip, Path.Combine(folder, $"serpent_frames_{SerpentRows[r]}.png"));
            }

            SerpentTurn(Path.Combine(folder, "serpent_turn.png"), arena);
            SerpentPatterns(folder, arena);

            // How far the head and the body's edges move from each frame to the next on a fixed view (side and front).
            var report = new System.Text.StringBuilder();
            int jumps = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                var steps = rows[r].Select(f => (f.Item1, f.Item2)).ToList();
                foreach (var view in new[] { BossView.Side, BossView.Front })
                {
                    report.AppendLine($"## {SerpentRows[r]} ({view})");
                    SerpentArt.Joints? last = null;
                    (SerpentPose, int) lastStep = default;
                    foreach (var step in steps)
                    {
                        var j = SerpentArt.JointsOf(view, step.Item1, step.Item2);
                        string line = $"{step.Item1}{step.Item2,-3} head {j.HeadX,5:0} {j.HeadY,5:0} body {j.Left}-{j.Right} {j.Top}-{j.Bottom}";
                        if (last is SerpentArt.Joints l && !(lastStep.Item1 == step.Item1 && lastStep.Item2 == step.Item2) && j.Bottom >= 0 && l.Bottom >= 0)
                        {
                            float head = Distance(j.HeadX - l.HeadX, j.HeadY - l.HeadY);
                            int top = Math.Abs(j.Top - l.Top), bottom = Math.Abs(j.Bottom - l.Bottom);
                            bool swing = SerpentArt.Swinging(step.Item1, step.Item2);
                            bool strike = step.Item2 == SerpentArt.StrikeFrame(step.Item1);
                            // A lunge or a burst up out of the sand is too fast to see: the body may move far in it.
                            bool jump = head > (swing ? 14 : 5) || bottom > (swing ? 16 : 4) || top > (swing ? 16 : 6);
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
            File.WriteAllText(Path.Combine(folder, "serpent_continuity.txt"), report.ToString());
            Console.WriteLine($"serpent continuity: {jumps} jumps (anim/serpent_continuity.txt)");
        }

        // The key poses turned eight ways (the way WorldView picks a view and mirrors it), with the last phase's
        // storm-lit eyes and frills on the bottom row.
        static void SerpentTurn(string path, PixelImage arena)
        {
            var ways = new (BossView View, bool Flip)[]
            {
                (BossView.Side, false), (BossView.FrontSide, false), (BossView.Front, false), (BossView.FrontSide, true),
                (BossView.Side, true), (BossView.BackSide, true), (BossView.Back, false), (BossView.BackSide, false),
            };
            int Last(SerpentPose pose) => SerpentArt.FramesOf(pose) - 1;
            int Strike(SerpentPose pose) => SerpentArt.StrikeFrame(pose);
            var poses = new (SerpentPose Pose, int Frame, int Phase)[]
            {
                (SerpentPose.Idle, 0, 1), (SerpentPose.Coil, Last(SerpentPose.Coil), 1), (SerpentPose.Lash, Strike(SerpentPose.Lash), 1),
                (SerpentPose.Draw, Last(SerpentPose.Draw), 1), (SerpentPose.Spit, Strike(SerpentPose.Spit), 1), (SerpentPose.Puff, Last(SerpentPose.Puff), 1),
                (SerpentPose.Burst, Strike(SerpentPose.Burst), 1), (SerpentPose.Dive, Last(SerpentPose.Dive), 1), (SerpentPose.Surge, Strike(SerpentPose.Surge), 1),
                (SerpentPose.Wind, Last(SerpentPose.Wind), 1), (SerpentPose.Loom, Last(SerpentPose.Loom), 1), (SerpentPose.Crash, Strike(SerpentPose.Crash), 1),
                (SerpentPose.Flare, Last(SerpentPose.Flare), 1), (SerpentPose.Rise, Last(SerpentPose.Rise), 1), (SerpentPose.Spout, Strike(SerpentPose.Spout), 1),
                (SerpentPose.Roar, SerpentArt.RoarRise, 1), (SerpentPose.Enter, SerpentArt.EnterPeek + 4, 1), (SerpentPose.Idle, 0, 3),
            };
            var turn = new PixelImage(ways.Length * SerpentArt.Width, poses.Length * SerpentArt.Height);
            for (int r = 0; r < poses.Length; r++)
                for (int w = 0; w < ways.Length; w++)
                    turn.Draw(OnGround(Flip(SerpentArt.Draw(ways[w].View, poses[r].Pose, poses[r].Frame, poses[r].Phase), ways[w].Flip), arena), w * SerpentArt.Width, r * SerpentArt.Height);
            Png.Save(turn, path);
        }
    }
}
