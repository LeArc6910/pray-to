using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Client
{
    // The lava titan's motions (2026-11-28, LavaTitanArt), each as WorldView plays it: idle into the wind-up, the blow,
    // back to rest; one row each, side and front. Every frame of each motion on a strip (titan_frames_<motion>.png), the
    // poses turned eight ways (titan_turn.png), and how far the head and the body move from each frame to the next
    // (titan_continuity.txt). Made after SerpentMotions.
    static partial class Motions
    {
        static readonly string[] TitanRows = { "walk", "rush", "slam", "fire_wave", "meteor", "magma_ring", "eruption", "lava_trail", "heat_ring", "lava_cross", "fire_rain", "roar", "enter" };

        static (LavaTitanPose, int, float)[][] TitanSequences()
        {
            (LavaTitanPose, int, float)[] Run(LavaTitanPose pose, float seconds, int from = 0, int to = -1)
            {
                int last = to < 0 ? LavaTitanArt.FramesOf(pose) - 1 : to;
                return Enumerable.Range(from, last - from + 1).Select(f => (pose, f, seconds)).ToArray();
            }
            (LavaTitanPose, int, float)[] Blow(LavaTitanPose pose) =>
                Enumerable.Range(0, LavaTitanArt.FramesOf(pose)).Select(f => (pose, f, LavaTitanArt.FrameSeconds(pose, f))).ToArray();
            var rest = new[] { (LavaTitanPose.Idle, 0, 0.4f) };
            (LavaTitanPose, int, float)[] Attack(LavaTitanPose blow, float windUp) =>
                rest.Concat(Run(LavaTitanArt.WindUpOf(blow), windUp / LavaTitanArt.FramesOf(LavaTitanArt.WindUpOf(blow)))).Concat(Blow(blow)).ToArray();
            return new[]
            {
                Run(LavaTitanPose.Walk, 0.1f).Concat(Run(LavaTitanPose.Walk, 0.1f)).ToArray(),
                Run(LavaTitanPose.Rush, 0.07f).Concat(Run(LavaTitanPose.Rush, 0.07f)).Concat(Run(LavaTitanPose.Rush, 0.07f)).ToArray(),
                Attack(LavaTitanPose.Smash, 0.9f),
                Attack(LavaTitanPose.Sweep, 0.8f),
                Attack(LavaTitanPose.Hurl, 1.2f),
                Attack(LavaTitanPose.Vent, 0.5f),
                Attack(LavaTitanPose.Punch, 1.0f),
                Attack(LavaTitanPose.Gouge, 0.8f),
                Attack(LavaTitanPose.Flare, 1.1f),
                Attack(LavaTitanPose.Stomp, 1.1f),
                Attack(LavaTitanPose.Blaze, 1.4f),
                rest.Concat(Run(LavaTitanPose.Roar, 0.06f, 0, LavaTitanArt.RoarRise - 1)).Concat(Run(LavaTitanPose.Roar, 0.08f, LavaTitanArt.RoarRise)).Concat(Run(LavaTitanPose.Roar, 0.08f, LavaTitanArt.RoarRise)).ToArray(),
                new[] { (LavaTitanPose.Enter, 0, 0.6f) }.Concat(Blow(LavaTitanPose.Enter)).Concat(rest).ToArray(),
            };
        }

        static void Titan(string folder, PixelImage arena)
        {
            Directory.CreateDirectory(folder);
            var rows = TitanSequences();
            const int scale = 2;
            int cellWidth = LavaTitanArt.Width * scale, cellHeight = LavaTitanArt.Height * scale;
            int width = cellWidth * 2, height = cellHeight * rows.Length;
            var frames = new List<(Rgba[] Pixels, int Centiseconds)>();
            float longest = rows.Max(r => r.Sum(f => f.Item3));
            var cache = new Dictionary<(BossView, LavaTitanPose, int), PixelImage>();
            PixelImage Drawn(BossView view, LavaTitanPose pose, int frame)
            {
                if (!cache.TryGetValue((view, pose, frame), out var image)) cache[(view, pose, frame)] = image = OnGround(LavaTitanArt.Draw(view, pose, frame, 1), arena);
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
            Gif.Save(Path.Combine(folder, "titan.gif"), width, height, frames);

            // Every frame of each motion in order, side over front, each frame faintly over the one before.
            for (int r = 0; r < rows.Length; r++)
            {
                var steps = rows[r].Select(f => (f.Item1, f.Item2)).Distinct().ToList();
                var strip = new PixelImage(steps.Count * LavaTitanArt.Width, 2 * LavaTitanArt.Height);
                for (int k = 0; k < steps.Count; k++)
                {
                    for (int v = 0; v < 2; v++)
                    {
                        var view = v == 0 ? BossView.Side : BossView.Front;
                        var image = LavaTitanArt.Draw(view, steps[k].Item1, steps[k].Item2, 1);
                        var before = k > 0 ? LavaTitanArt.Draw(view, steps[k - 1].Item1, steps[k - 1].Item2, 1) : null;
                        for (int y = 0; y < LavaTitanArt.Height; y++)
                            for (int x = 0; x < LavaTitanArt.Width; x++)
                            {
                                var c = image[x, y];
                                var ground = arena[x % arena.Width, y % arena.Height];
                                if (c.A == 0 && before != null && before[x, y].A > 0) c = ground.Mix(new Rgba(255, 160, 90), 0.3f);
                                strip[k * LavaTitanArt.Width + x, v * LavaTitanArt.Height + y] = c.A == 255 ? c : c.A == 0 ? ground : ground.Mix(c.WithAlpha(255), c.A / 255f);
                            }
                    }
                }
                Png.Save(strip, Path.Combine(folder, $"titan_frames_{TitanRows[r]}.png"));
            }

            TitanTurn(Path.Combine(folder, "titan_turn.png"), arena);

            // How far the head and the body's edges move from each frame to the next on a fixed view (side and front).
            var report = new System.Text.StringBuilder();
            int jumps = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                var steps = rows[r].Select(f => (f.Item1, f.Item2)).ToList();
                foreach (var view in new[] { BossView.Side, BossView.Front })
                {
                    report.AppendLine($"## {TitanRows[r]} ({view})");
                    LavaTitanArt.Joints? last = null;
                    (LavaTitanPose, int) lastStep = default;
                    foreach (var step in steps)
                    {
                        var j = LavaTitanArt.JointsOf(view, step.Item1, step.Item2);
                        string line = $"{step.Item1}{step.Item2,-3} head {j.HeadX,5:0} {j.HeadY,5:0} hand {j.HandX,5:0} {j.HandY,5:0} body {j.Left}-{j.Right} {j.Top}-{j.Bottom}";
                        if (last is LavaTitanArt.Joints l && !(lastStep.Item1 == step.Item1 && lastStep.Item2 == step.Item2) && j.Bottom >= 0 && l.Bottom >= 0)
                        {
                            float head = Distance(j.HeadX - l.HeadX, j.HeadY - l.HeadY);
                            float hand = Distance(j.HandX - l.HandX, j.HandY - l.HandY);
                            bool swing = LavaTitanArt.Swinging(step.Item1, step.Item2);
                            // A blow's lunge is too fast to see: the arms may move far in it. A hand is at the end of a long arm and moves
                            // further than the head each frame of a wind-up.
                            bool jump = head > (swing ? 14 : 6) || hand > (swing ? 45 : 16);
                            line += $"   move head {head,4:0.0} hand {hand,4:0.0}" + (jump ? "   <<< JUMP" : "");
                            if (jump) jumps++;
                        }
                        report.AppendLine(line);
                        last = j;
                        lastStep = step;
                    }
                }
            }
            report.Insert(0, $"jumps: {jumps}\n\n");
            File.WriteAllText(Path.Combine(folder, "titan_continuity.txt"), report.ToString());
            Console.WriteLine($"titan continuity: {jumps} jumps (anim/titan_continuity.txt)");
        }

        // The key poses turned eight ways (the way WorldView picks a view and mirrors it), with the last phase's
        // white-hot seams on the bottom row.
        static void TitanTurn(string path, PixelImage arena)
        {
            var ways = new (BossView View, bool Flip)[]
            {
                (BossView.Side, false), (BossView.FrontSide, false), (BossView.Front, false), (BossView.FrontSide, true),
                (BossView.Side, true), (BossView.BackSide, true), (BossView.Back, false), (BossView.BackSide, false),
            };
            int Last(LavaTitanPose pose) => LavaTitanArt.FramesOf(pose) - 1;
            int Strike(LavaTitanPose pose) => LavaTitanArt.StrikeFrame(pose);
            var poses = new (LavaTitanPose Pose, int Frame, int Phase)[]
            {
                (LavaTitanPose.Idle, 0, 1), (LavaTitanPose.Walk, 2, 1), (LavaTitanPose.Rush, 1, 1),
                (LavaTitanPose.Heave, Last(LavaTitanPose.Heave), 1), (LavaTitanPose.Smash, Strike(LavaTitanPose.Smash), 1),
                (LavaTitanPose.Wind, Last(LavaTitanPose.Wind), 1), (LavaTitanPose.Sweep, Strike(LavaTitanPose.Sweep), 1),
                (LavaTitanPose.Call, Last(LavaTitanPose.Call), 1), (LavaTitanPose.Hurl, Strike(LavaTitanPose.Hurl), 1),
                (LavaTitanPose.Swell, Last(LavaTitanPose.Swell), 1), (LavaTitanPose.Vent, Strike(LavaTitanPose.Vent), 1),
                (LavaTitanPose.Kneel, Last(LavaTitanPose.Kneel), 1), (LavaTitanPose.Punch, Strike(LavaTitanPose.Punch), 1),
                (LavaTitanPose.Stoop, Last(LavaTitanPose.Stoop), 1), (LavaTitanPose.Gouge, Strike(LavaTitanPose.Gouge), 1),
                (LavaTitanPose.Brace, Last(LavaTitanPose.Brace), 1), (LavaTitanPose.Flare, Strike(LavaTitanPose.Flare), 1),
                (LavaTitanPose.Lift, Last(LavaTitanPose.Lift), 1), (LavaTitanPose.Stomp, Strike(LavaTitanPose.Stomp), 1),
                (LavaTitanPose.Ignite, Last(LavaTitanPose.Ignite), 1), (LavaTitanPose.Blaze, Strike(LavaTitanPose.Blaze), 1),
                (LavaTitanPose.Roar, LavaTitanArt.RoarRise, 1), (LavaTitanPose.Enter, LavaTitanArt.EnterHand + 4, 1),
                (LavaTitanPose.Enter, LavaTitanArt.EnterKindle + 2, 1), (LavaTitanPose.Idle, 0, 3),
            };
            var turn = new PixelImage(ways.Length * LavaTitanArt.Width, poses.Length * LavaTitanArt.Height);
            for (int r = 0; r < poses.Length; r++)
                for (int w = 0; w < ways.Length; w++)
                    turn.Draw(OnGround(Flip(LavaTitanArt.Draw(ways[w].View, poses[r].Pose, poses[r].Frame, poses[r].Phase), ways[w].Flip), arena), w * LavaTitanArt.Width, r * LavaTitanArt.Height);
            Png.Save(turn, path);
        }
    }
}
