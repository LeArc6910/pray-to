using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Client
{
    // The mad general's motions (2026-11-27, GeneralArt), each as WorldView plays it: idle into the wind-up, the blow,
    // back to rest; one row each, side (turned with the blow) and front. Every frame of each motion on a strip
    // (general_frames_<motion>.png), the poses turned eight ways (general_turn.png), and how far the hands and the
    // body move from each frame to the next (general_continuity.txt).
    static partial class Motions
    {
        static readonly string[] GeneralRows = { "walk", "cleave", "command", "stab", "plant", "spin", "charge", "rally", "roar", "enter" };

        static (GeneralPose, int, float)[][] GeneralSequences()
        {
            (GeneralPose, int, float)[] Run(GeneralPose pose, float seconds, int from = 0, int to = -1)
            {
                int last = to < 0 ? GeneralArt.FramesOf(pose) - 1 : to;
                return Enumerable.Range(from, last - from + 1).Select(f => (pose, f, seconds)).ToArray();
            }
            (GeneralPose, int, float)[] Blow(GeneralPose pose) =>
                Enumerable.Range(0, GeneralArt.FramesOf(pose)).Select(f => (pose, f, GeneralArt.FrameSeconds(pose, f))).ToArray();
            var rest = new[] { (GeneralPose.Idle, 0, 0.4f) };
            (GeneralPose, int, float)[] Attack(GeneralPose blow, float windUp) =>
                rest.Concat(Run(GeneralArt.WindUpOf(blow), windUp / GeneralArt.FramesOf(GeneralArt.WindUpOf(blow)))).Concat(Blow(blow)).ToArray();
            return new[]
            {
                Run(GeneralPose.Walk, 0.1f).Concat(Run(GeneralPose.Walk, 0.1f)).ToArray(),
                Attack(GeneralPose.Cleave, 0.7f),
                Attack(GeneralPose.Command, 1.1f),
                Attack(GeneralPose.Stab, 0.9f),
                Attack(GeneralPose.Plant, 1.1f),
                Attack(GeneralPose.Spin, 0.6f),
                rest.Concat(Run(GeneralPose.Brace, 0.1f)).Concat(Run(GeneralPose.Charge, 0.05f)).Concat(Run(GeneralPose.Charge, 0.05f))
                    .Concat(Enumerable.Range(0, GeneralArt.FramesOf(GeneralPose.Brace)).Reverse().Select(f => (GeneralPose.Brace, f, 0.07f))).ToArray(),
                rest.Concat(Run(GeneralPose.Rally, 0.06f, 0, GeneralArt.RallyRise - 1)).Concat(Run(GeneralPose.Rally, 0.1f, GeneralArt.RallyRise)).Concat(Run(GeneralPose.Rally, 0.1f, GeneralArt.RallyRise)).ToArray(),
                rest.Concat(Run(GeneralPose.Roar, 0.07f, 0, GeneralArt.RoarRise - 1)).Concat(Run(GeneralPose.Roar, 0.1f, GeneralArt.RoarRise)).Concat(Run(GeneralPose.Roar, 0.1f, GeneralArt.RoarRise)).ToArray(),
                new[] { (GeneralPose.Enter, 0, 0.6f) }.Concat(Blow(GeneralPose.Enter)).Concat(rest).ToArray(),
            };
        }

        // The body turns with the blow as WorldView turns it (AnimateGeneral): the spin a view a frame.
        static BossView GeneralSideView(GeneralPose pose, int frame)
        {
            if (GeneralArt.Spinning(pose, frame))
            {
                var views = new[] { BossView.Side, BossView.FrontSide, BossView.Front, BossView.FrontSide, BossView.Side, BossView.BackSide, BossView.Back, BossView.BackSide };
                return views[frame % 8];
            }
            int twist = GeneralArt.TwistOf(pose, frame);
            return twist < 0 ? BossView.BackSide : twist > 0 ? BossView.FrontSide : BossView.Side;
        }

        static void General(string folder, PixelImage arena)
        {
            Directory.CreateDirectory(folder);
            var rows = GeneralSequences();
            const int scale = 2;
            int cellWidth = GeneralArt.Width * scale, cellHeight = GeneralArt.Height * scale;
            int width = cellWidth * 2, height = cellHeight * rows.Length;
            var frames = new List<(Rgba[] Pixels, int Centiseconds)>();
            float longest = rows.Max(r => r.Sum(f => f.Item3));
            var cache = new Dictionary<(BossView, GeneralPose, int), PixelImage>();
            PixelImage Drawn(BossView view, GeneralPose pose, int frame)
            {
                if (!cache.TryGetValue((view, pose, frame), out var image)) cache[(view, pose, frame)] = image = OnGround(GeneralArt.Draw(view, pose, frame, 1), arena);
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
                        var image = Drawn(column == 0 ? GeneralSideView(step.Item1, step.Item2) : BossView.Front, step.Item1, step.Item2);
                        for (int y = 0; y < cellHeight; y++)
                            for (int x = 0; x < cellWidth; x++)
                                pixels[(row * cellHeight + y) * width + column * cellWidth + x] = image[x / scale, y / scale];
                    }
                }
                frames.Add((pixels, 2));
            }
            Gif.Save(Path.Combine(folder, "general.gif"), width, height, frames);

            // Every frame of each motion in order, side (as turned) over front, each frame faintly over the one before.
            for (int r = 0; r < rows.Length; r++)
            {
                var steps = rows[r].Select(f => (f.Item1, f.Item2)).Distinct().ToList();
                var strip = new PixelImage(steps.Count * GeneralArt.Width, 2 * GeneralArt.Height);
                for (int k = 0; k < steps.Count; k++)
                {
                    for (int v = 0; v < 2; v++)
                    {
                        BossView ViewOf((GeneralPose, int) s) => v == 0 ? GeneralSideView(s.Item1, s.Item2) : BossView.Front;
                        var image = GeneralArt.Draw(ViewOf(steps[k]), steps[k].Item1, steps[k].Item2, 1);
                        var before = k > 0 ? GeneralArt.Draw(ViewOf(steps[k - 1]), steps[k - 1].Item1, steps[k - 1].Item2, 1) : null;
                        for (int y = 0; y < GeneralArt.Height; y++)
                            for (int x = 0; x < GeneralArt.Width; x++)
                            {
                                var c = image[x, y];
                                var ground = arena[x % arena.Width, y % arena.Height];
                                if (c.A == 0 && before != null && before[x, y].A > 0) c = ground.Mix(new Rgba(120, 200, 255), 0.35f);
                                strip[k * GeneralArt.Width + x, v * GeneralArt.Height + y] = c.A == 255 ? c : c.A == 0 ? ground : ground.Mix(c.WithAlpha(255), c.A / 255f);
                            }
                    }
                }
                Png.Save(strip, Path.Combine(folder, $"general_frames_{GeneralRows[r]}.png"));
            }

            // The key poses turned eight ways (the way WorldView picks a view and mirrors it), with the last phase's
            // red-hot core on the bottom row.
            var ways = new (BossView View, bool Flip)[]
            {
                (BossView.Side, false), (BossView.FrontSide, false), (BossView.Front, false), (BossView.FrontSide, true),
                (BossView.Side, true), (BossView.BackSide, true), (BossView.Back, false), (BossView.BackSide, false),
            };
            var poses = new (GeneralPose Pose, int Frame, int Phase)[]
            {
                (GeneralPose.Idle, 0, 1), (GeneralPose.Walk, 2, 1), (GeneralPose.Wind, 11, 1), (GeneralPose.Cleave, GeneralArt.StrikeFrame(GeneralPose.Cleave), 1),
                (GeneralPose.Order, 13, 1), (GeneralPose.Command, GeneralArt.StrikeFrame(GeneralPose.Command), 1), (GeneralPose.Heft, 11, 1),
                (GeneralPose.Stab, GeneralArt.StrikeFrame(GeneralPose.Stab), 1), (GeneralPose.Hoist, 11, 1), (GeneralPose.Plant, GeneralArt.StrikeFrame(GeneralPose.Plant), 1),
                (GeneralPose.Coil, 9, 1), (GeneralPose.Spin, 4, 1), (GeneralPose.Brace, 7, 1), (GeneralPose.Rally, GeneralArt.RallyRise, 1),
                (GeneralPose.Roar, GeneralArt.RoarRise, 1), (GeneralPose.Enter, 0, 1), (GeneralPose.Idle, 0, 3), (GeneralPose.Cleave, GeneralArt.StrikeFrame(GeneralPose.Cleave), 3),
            };
            var turn = new PixelImage(ways.Length * GeneralArt.Width, poses.Length * GeneralArt.Height);
            for (int r = 0; r < poses.Length; r++)
                for (int w = 0; w < ways.Length; w++)
                    turn.Draw(OnGround(Flip(GeneralArt.Draw(ways[w].View, poses[r].Pose, poses[r].Frame, poses[r].Phase), ways[w].Flip), arena), w * GeneralArt.Width, r * GeneralArt.Height);
            Png.Save(turn, Path.Combine(folder, "general_turn.png"));

            // How far the fists and the body's edges move from each frame to the next on a fixed view (side and front),
            // and every step that moves more than a heavy body should.
            var report = new System.Text.StringBuilder();
            int jumps = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                var steps = rows[r].Select(f => (f.Item1, f.Item2)).ToList();
                foreach (var view in new[] { BossView.Side, BossView.Front })
                {
                    report.AppendLine($"## {GeneralRows[r]} ({view})");
                    GeneralArt.Joints? last = null;
                    (GeneralPose, int) lastStep = default;
                    foreach (var step in steps)
                    {
                        var j = GeneralArt.JointsOf(view, step.Item1, step.Item2);
                        string line = $"{step.Item1}{step.Item2,-3} tip {j.TipX,5:0} {j.TipY,5:0} fistA {j.FistAX,5:0} {j.FistAY,5:0} fistB {j.FistBX,5:0} {j.FistBY,5:0} body {j.Left}-{j.Right} {j.Top}-{j.Bottom}";
                        if (last is GeneralArt.Joints l && !(lastStep.Item1 == step.Item1 && lastStep.Item2 == step.Item2))
                        {
                            float a = Distance(j.FistAX - l.FistAX, j.FistAY - l.FistAY), b = Distance(j.FistBX - l.FistBX, j.FistBY - l.FistBY);
                            int top = Math.Abs(j.Top - l.Top), bottom = Math.Abs(j.Bottom - l.Bottom);
                            // A swing may move fast and the strike squashes the body at once; the entrance's sleeping heap
                            // is a still frame held before the first stir.
                            bool swing = GeneralArt.Swinging(step.Item1, step.Item2);
                            bool strike = step.Item2 == GeneralArt.StrikeFrame(step.Item1);
                            float fist = swing ? 26 : 7;
                            // The spin turns him a view a frame, and the banner leaves or reaches his back at once.
                            bool spin = GeneralArt.Spinning(step.Item1, step.Item2);
                            // The strike squashes the body at once (as the black knight's blade into the floor); the banner's
                            // pole counts in the top, so its hand moves the top with it, and it goes back on his back at once;
                            // a step lifts a foot a little higher seen from the front.
                            bool banner = step.Item1 == GeneralPose.Plant || step.Item1 == GeneralPose.Hoist || step.Item1 == GeneralPose.Rally;
                            bool jump = !spin && (a > fist || b > fist || bottom > (strike ? 10 : 5) || top > (strike ? 22 : swing ? 16 : banner ? 10 : 7));
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
            File.WriteAllText(Path.Combine(folder, "general_continuity.txt"), report.ToString());
            Console.WriteLine($"general continuity: {jumps} jumps (anim/general_continuity.txt)");
        }
    }
}
