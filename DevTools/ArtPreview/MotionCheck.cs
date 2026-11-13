using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Client
{
    // A boss's motions laid out to be checked frame by frame (2026-11-13, the person: "more frames; check every frame's
    // body, the weapon's place and way, and that it follows on from the frames either side"). Each motion is a row of
    // steps as the game plays it (rest, wind-up, blow, rest). It writes the moving preview (side as turned, and front),
    // every frame of each motion in a strip over a faint ghost of the frame before (<name>_frames_<motion>.png), and a
    // report of how far the tracked points and the body's edges move between frames, marking every step that moves more
    // than it should for its kind (<name>_continuity.txt).
    static class MotionCheck
    {
        // How a step may move from the one before: an ordinary step, a swing too fast to see, the strike (the body
        // squashed at once), up in the air or down into the earth, or a cut (out of sight, not checked).
        public enum Kind
        {
            Normal,
            Swing,
            Strike,
            // The iaido draw: the blade out and through in an instant, the body carried with it (the person's beat).
            Instant,
            Air,
            Cut,
        }

        public struct Measure
        {
            public (float X, float Y)[] Points;
            public int Top, Bottom;
        }

        public sealed class Step
        {
            public string Label;
            public float Seconds;
            public BossView SideView = BossView.Side;
            public Kind Kind;
            public Func<BossView, PixelImage> Draw;
            public Func<BossView, Measure> Measure;
        }

        // How far (pixels) each tracked point and the body's top and bottom may move in one step of each kind.
        static (float Point, int Top, int Bottom) Allowed(Kind kind, float point) => kind switch
        {
            Kind.Swing => (point * 3.3f, 8, 4),
            Kind.Strike => (point * 3.3f, 20, 6),
            // The feet's line may move as far as the lunge carries the body: seen from the front the samurai's draw now
            // truly comes at the viewer (2026-11-13; before, that view only squashed the body where it stood).
            Kind.Instant => (1000, 20, 8),
            Kind.Air => (16, 16, 16),
            _ => (point, 6, 4),
        };

        // A pixel of a frame over the ground: as the game shows it, a half-clear pixel (the fading arc of a cut) lets the
        // ground through.
        static Rgba Over(Rgba ground, Rgba c) => c.A == 255 ? c : c.A == 0 ? ground : ground.Mix(c.WithAlpha(255), c.A / 255f);

        public static int Write(string folder, string name, string[] rowNames, List<Step>[] rows, PixelImage arena, int width, int height, float point)
        {
            // The moving preview.
            const int scale = 2;
            int cellWidth = width * scale, cellHeight = height * scale;
            int gifWidth = cellWidth * 2, gifHeight = cellHeight * rows.Length;
            var frames = new List<(Rgba[] Pixels, int Centiseconds)>();
            float longest = rows.Max(r => r.Sum(s => s.Seconds));
            var cache = new Dictionary<(Step, BossView), PixelImage>();
            PixelImage Drawn(Step step, BossView view)
            {
                if (!cache.TryGetValue((step, view), out var image)) cache[(step, view)] = image = step.Draw(view);
                return image;
            }
            for (float time = 0; time < longest; time += 0.02f)
            {
                var pixels = new Rgba[gifWidth * gifHeight];
                for (int r = 0; r < rows.Length; r++)
                {
                    float at = time % rows[r].Sum(s => s.Seconds);
                    var step = rows[r][0];
                    foreach (var s in rows[r])
                    {
                        step = s;
                        if (at < s.Seconds) break;
                        at -= s.Seconds;
                    }
                    for (int column = 0; column < 2; column++)
                    {
                        var image = Drawn(step, column == 0 ? step.SideView : BossView.Front);
                        for (int y = 0; y < cellHeight; y++)
                            for (int x = 0; x < cellWidth; x++)
                            {
                                var c = image[x / scale, y / scale];
                                pixels[(r * cellHeight + y) * gifWidth + column * cellWidth + x] = Over(arena[x / scale % arena.Width, y / scale % arena.Height], c);
                            }
                    }
                }
                frames.Add((pixels, 2));
            }
            Gif.Save(Path.Combine(folder, name + ".gif"), gifWidth, gifHeight, frames);

            // Every frame in a strip, over a ghost of the one before.
            for (int r = 0; r < rows.Length; r++)
            {
                var steps = new List<Step>();
                foreach (var s in rows[r])
                    if (steps.Count == 0 || steps[steps.Count - 1].Label != s.Label) steps.Add(s);
                var strip = new PixelImage(steps.Count * width, 2 * height);
                for (int k = 0; k < steps.Count; k++)
                {
                    for (int v = 0; v < 2; v++)
                    {
                        var image = Drawn(steps[k], v == 0 ? steps[k].SideView : BossView.Front);
                        var before = k > 0 ? Drawn(steps[k - 1], v == 0 ? steps[k - 1].SideView : BossView.Front) : null;
                        for (int y = 0; y < height; y++)
                            for (int x = 0; x < width; x++)
                            {
                                var c = image[x, y];
                                var ground = arena[x % arena.Width, y % arena.Height];
                                if (c.A == 0 && before != null && before[x, y].A > 0) c = ground.Mix(new Rgba(120, 200, 255), 0.35f);
                                strip[k * width + x, v * height + y] = Over(ground, c);
                            }
                    }
                }
                Png.Save(strip, Path.Combine(folder, $"{name}_frames_{rowNames[r]}.png"));
            }

            // How far things move between frames, on a fixed view.
            var report = new System.Text.StringBuilder();
            int jumps = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                foreach (var view in new[] { BossView.Side, BossView.Front })
                {
                    report.AppendLine($"## {rowNames[r]} ({view})");
                    Measure? last = null;
                    string lastLabel = null;
                    foreach (var step in rows[r])
                    {
                        if (step.Label == lastLabel) continue;
                        var m = step.Measure(view);
                        string line = $"{step.Label,-12} " + string.Join(" ", m.Points.Select(p => $"({p.X,4:0},{p.Y,4:0})")) + $" body {m.Top}-{m.Bottom}";
                        if (last is Measure l && step.Kind != Kind.Cut)
                        {
                            var allowed = Allowed(step.Kind, point);
                            float moved = m.Points.Zip(l.Points, (a, b) => (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y))).Max();
                            // A body gone out of sight (all under the earth) has no edges to compare.
                            bool seen = m.Bottom >= 0 && l.Bottom >= 0;
                            int top = seen ? Math.Abs(m.Top - l.Top) : 0, bottom = seen ? Math.Abs(m.Bottom - l.Bottom) : 0;
                            bool jump = moved > allowed.Point || top > allowed.Top || bottom > allowed.Bottom;
                            line += $"   move {moved,4:0.0} top {top} bottom {bottom}" + (jump ? "   <<< JUMP" : "");
                            if (jump) jumps++;
                        }
                        report.AppendLine(line);
                        last = m;
                        lastLabel = step.Label;
                    }
                }
            }
            report.Insert(0, $"jumps: {jumps}\n\n");
            File.WriteAllText(Path.Combine(folder, name + "_continuity.txt"), report.ToString());
            Console.WriteLine($"{name} continuity: {jumps} jumps (anim/{name}_continuity.txt)");
            return jumps;
        }
    }
}
