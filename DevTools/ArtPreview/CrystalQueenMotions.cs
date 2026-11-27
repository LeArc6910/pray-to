using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Client
{
    // The crystal spider queen's motions (2026-11-27, CrystalQueenArt), each as WorldView plays it: idle into the
    // wind-up, the blow, back to rest; one row each, side and front (crystal_queen.gif). The five views and the key
    // poses on one sheet (crystal_queen_draft.png), every frame of each motion on a strip
    // (crystal_queen_frames_<motion>.png), the poses turned eight ways (crystal_queen_turn.png), and how far the feet,
    // mouth and abdomen move from each frame to the next (crystal_queen_continuity.txt).
    static partial class Motions
    {
        static readonly string[] QueenRows = { "walk", "idle", "bite", "pounce", "prism", "burst", "stomp", "lay", "screech", "flinch", "die" };

        static (CrystalQueenPose, int, float)[][] QueenSequences()
        {
            (CrystalQueenPose, int, float)[] Run(CrystalQueenPose pose, float seconds, int from = 0, int to = -1)
            {
                int last = to < 0 ? CrystalQueenArt.FramesOf(pose) - 1 : to;
                return Enumerable.Range(from, last - from + 1).Select(f => (pose, f, seconds)).ToArray();
            }
            (CrystalQueenPose, int, float)[] Blow(CrystalQueenPose pose) =>
                Enumerable.Range(0, CrystalQueenArt.FramesOf(pose)).Select(f => (pose, f, CrystalQueenArt.FrameSeconds(pose, f))).ToArray();
            var rest = new[] { (CrystalQueenPose.Idle, 0, 0.4f) };
            (CrystalQueenPose, int, float)[] Attack(CrystalQueenPose blow, float windUp) =>
                rest.Concat(Run(CrystalQueenArt.WindUpOf(blow), windUp / CrystalQueenArt.FramesOf(CrystalQueenArt.WindUpOf(blow)))).Concat(Blow(blow)).ToArray();
            return new[]
            {
                Run(CrystalQueenPose.Walk, 0.1f).Concat(Run(CrystalQueenPose.Walk, 0.1f)).ToArray(),
                Run(CrystalQueenPose.Idle, 0.15f).Concat(Run(CrystalQueenPose.Idle, 0.15f)).ToArray(),
                Attack(CrystalQueenPose.Bite, 0.7f),
                rest.Concat(Run(CrystalQueenPose.Crouch, 0.09f)).Concat(Run(CrystalQueenPose.Spring, 0.04f)).Concat(Run(CrystalQueenPose.Rush, 0.05f))
                    .Concat(Run(CrystalQueenPose.Rush, 0.05f)).Concat(Run(CrystalQueenPose.Skid, 0.06f)).ToArray(),
                Attack(CrystalQueenPose.Flash, 1.0f),
                Attack(CrystalQueenPose.Burst, 0.6f),
                Attack(CrystalQueenPose.Stomp, 1.0f),
                Attack(CrystalQueenPose.Lay, 0.8f),
                rest.Concat(Run(CrystalQueenPose.Screech, 0.08f, 0, CrystalQueenArt.ScreechRise - 1)).Concat(Run(CrystalQueenPose.Screech, 0.1f, CrystalQueenArt.ScreechRise))
                    .Concat(Run(CrystalQueenPose.Screech, 0.1f, CrystalQueenArt.ScreechRise)).ToArray(),
                rest.Concat(Run(CrystalQueenPose.Flinch, 0.05f)).ToArray(),
                rest.Concat(Run(CrystalQueenPose.Die, 0.08f)).Concat(new[] { (CrystalQueenPose.Die, CrystalQueenArt.FramesOf(CrystalQueenPose.Die) - 1, 1.2f) }).ToArray(),
            };
        }

        static PixelImage QueenOnGround(PixelImage image, PixelImage arena, bool flip = false)
        {
            var result = new PixelImage(image.Width, image.Height);
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    var c = image[flip ? image.Width - 1 - x : x, y];
                    var ground = arena[x % arena.Width, y % arena.Height];
                    result[x, y] = c.A == 255 ? c : c.A == 0 ? ground : ground.Mix(c.WithAlpha(255), c.A / 255f);
                }
            return result;
        }

        static void Paste(PixelImage sheet, PixelImage image, int at, int top)
        {
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    sheet[at + x, top + y] = image[x, y];
        }

        static void CrystalQueen(string folder, PixelImage arena)
        {
            Directory.CreateDirectory(folder);
            int w = CrystalQueenArt.Width, h = CrystalQueenArt.Height;

            // The draft: the five views at rest, then the key poses side on (the held end of each wind-up and the frame
            // each blow lands), and the last phase's light on the right of the bottom row.
            var views = new[] { BossView.Front, BossView.FrontSide, BossView.Side, BossView.BackSide, BossView.Back };
            var key = new (CrystalQueenPose Pose, int Frame, int Phase)[]
            {
                (CrystalQueenPose.Rear, -1, 1), (CrystalQueenPose.Prism, -1, 1), (CrystalQueenPose.Bristle, -1, 1), (CrystalQueenPose.Lift, -1, 1), (CrystalQueenPose.Brood, -1, 1),
                (CrystalQueenPose.Bite, CrystalQueenArt.StrikeFrame(CrystalQueenPose.Bite), 1), (CrystalQueenPose.Flash, CrystalQueenArt.StrikeFrame(CrystalQueenPose.Flash), 1),
                (CrystalQueenPose.Burst, CrystalQueenArt.StrikeFrame(CrystalQueenPose.Burst), 1), (CrystalQueenPose.Stomp, CrystalQueenArt.StrikeFrame(CrystalQueenPose.Stomp), 1),
                (CrystalQueenPose.Idle, 0, 3),
            };
            var draft = new PixelImage(5 * w, 3 * h);
            for (int i = 0; i < views.Length; i++) Paste(draft, QueenOnGround(CrystalQueenArt.Draw(views[i], CrystalQueenPose.Idle, 0, 1), arena), i * w, 0);
            for (int i = 0; i < key.Length; i++)
            {
                var (pose, frame, phase) = key[i];
                int f = frame < 0 ? CrystalQueenArt.FramesOf(pose) - 1 : frame;
                var view = i == key.Length - 1 ? BossView.FrontSide : BossView.Side;
                Paste(draft, QueenOnGround(CrystalQueenArt.Draw(view, pose, f, phase), arena), (i % 5) * w, (1 + i / 5) * h);
            }
            Png.Save(Scale(draft, 3), Path.Combine(folder, "crystal_queen_draft.png"));

            // The motions at game speed: side, front.
            var rows = QueenSequences();
            const int scale = 2;
            int cellWidth = w * scale, cellHeight = h * scale;
            int width = cellWidth * 2, height = cellHeight * rows.Length;
            var frames = new List<(Rgba[] Pixels, int Centiseconds)>();
            float longest = rows.Max(r => r.Sum(f => f.Item3));
            var cache = new Dictionary<(BossView, CrystalQueenPose, int), PixelImage>();
            PixelImage Drawn(BossView view, CrystalQueenPose pose, int frame)
            {
                if (!cache.TryGetValue((view, pose, frame), out var image)) cache[(view, pose, frame)] = image = QueenOnGround(CrystalQueenArt.Draw(view, pose, frame, 1), arena);
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
            Gif.Save(Path.Combine(folder, "crystal_queen.gif"), width, height, frames);

            // Every frame of each motion, side over front.
            for (int r = 0; r < rows.Length; r++)
            {
                var steps = rows[r].Select(f => (f.Item1, f.Item2)).Distinct().ToList();
                var strip = new PixelImage(steps.Count * w, 2 * h);
                for (int k = 0; k < steps.Count; k++)
                    for (int v = 0; v < 2; v++)
                        Paste(strip, QueenOnGround(CrystalQueenArt.Draw(v == 0 ? BossView.Side : BossView.Front, steps[k].Item1, steps[k].Item2, 1), arena), k * w, v * h);
                Png.Save(strip, Path.Combine(folder, $"crystal_queen_frames_{QueenRows[r]}.png"));
            }

            // The key poses turned eight ways, as WorldView picks a view and mirrors it.
            var ways = new (BossView View, bool Flip)[]
            {
                (BossView.Side, false), (BossView.FrontSide, false), (BossView.Front, false), (BossView.FrontSide, true),
                (BossView.Side, true), (BossView.BackSide, true), (BossView.Back, false), (BossView.BackSide, false),
            };
            var turn = new PixelImage(ways.Length * w, key.Length * h);
            for (int r = 0; r < key.Length; r++)
            {
                var (pose, frame, phase) = key[r];
                int f = frame < 0 ? CrystalQueenArt.FramesOf(pose) - 1 : frame;
                for (int c = 0; c < ways.Length; c++)
                    Paste(turn, QueenOnGround(CrystalQueenArt.Draw(ways[c].View, pose, f, phase), arena, ways[c].Flip), c * w, r * h);
            }
            Png.Save(turn, Path.Combine(folder, "crystal_queen_turn.png"));

            // Continuity: from each frame to the next, how far each foot, the mouth and the abdomen move, and the body's
            // top and bottom; a fast frame may move further.
            var report = new List<string>();
            int jumps = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                float most = 0;
                foreach (var view in new[] { BossView.Side, BossView.Front })
                {
                    CrystalQueenArt.Joints? was = null;
                    foreach (var (pose, frame, _) in rows[r])
                    {
                        var now = CrystalQueenArt.JointsOf(view, pose, frame);
                        if (was is CrystalQueenArt.Joints a)
                        {
                            float d = 0;
                            for (int leg = 0; leg < 8; leg++) d = Math.Max(d, Dist(a.Feet[leg], now.Feet[leg]));
                            d = Math.Max(d, Dist((a.MouthX, a.MouthY), (now.MouthX, now.MouthY)));
                            d = Math.Max(d, Dist((a.AbdomenX, a.AbdomenY), (now.AbdomenX, now.AbdomenY)));
                            d = Math.Max(d, Math.Abs(a.Top - now.Top));
                            d = Math.Max(d, Math.Abs(a.Bottom - now.Bottom));
                            float allowed = CrystalQueenArt.Swinging(pose, frame) || pose == CrystalQueenPose.Spring || pose == CrystalQueenPose.Rush ? 23 : 7;
                            if (d > allowed)
                            {
                                jumps++;
                                report.Add($"  jump {QueenRows[r]} {view} {pose} {frame}: {d:0.0} (allowed {allowed})");
                            }
                            most = Math.Max(most, d);
                        }
                        was = now;
                    }
                }
                report.Add($"{QueenRows[r]}: largest step {most:0.0}");
            }
            report.Insert(0, $"jumps: {jumps}");
            File.WriteAllLines(Path.Combine(folder, "crystal_queen_continuity.txt"), report);
            Console.WriteLine($"crystal queen: {jumps} jumps");
        }

        static float Dist((float X, float Y) a, (float X, float Y) b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        static PixelImage Scale(PixelImage image, int scale)
        {
            var result = new PixelImage(image.Width * scale, image.Height * scale);
            for (int y = 0; y < result.Height; y++)
                for (int x = 0; x < result.Width; x++)
                    result[x, y] = image[x / scale, y / scale];
            return result;
        }
    }
}
