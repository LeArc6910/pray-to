using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Client
{
    // Holes in a body as it moves (2026-11-27, the person: "움직임이 있을 때 신체에 빈 부분이 나오는 경우가 있어"):
    // the bosses that are posed in code move their arms and legs apart from the body, and where a part leaves, the
    // floor can show through the body. For every boss and monster, every view, pose and frame, this counts the
    // see-through pixels inside the body: a hole the outside cannot reach, or a slit closed by the body within two
    // pixels on both sides (left and right, or above and below) and on one end, of six pixels or more together, where
    // the same view at rest is solid (so not the room the picture has anyway, between the legs or inside a weapon's
    // grip). The worst frames are drawn with the gaps in red (gaps.png, gaps_<name>.png). `--only gaps`, gaps.txt.
    static class GapCheck
    {
        const int Reach = 2;
        // See-through pixels (the streak or smear behind a fast blow) are not body.
        static bool Solid(PixelImage image, int x, int y) => image.Inside(x, y) && image[x, y].A == 255;

        public static bool[,] Gaps(PixelImage image)
        {
            int w = image.Width, h = image.Height;
            var outside = new bool[w, h];
            var queue = new Queue<(int, int)>();
            void Reach0(int x, int y)
            {
                if (x < 0 || y < 0 || x >= w || y >= h || outside[x, y] || Solid(image, x, y)) return;
                outside[x, y] = true;
                queue.Enqueue((x, y));
            }
            for (int x = 0; x < w; x++)
            {
                Reach0(x, 0);
                Reach0(x, h - 1);
            }
            for (int y = 0; y < h; y++)
            {
                Reach0(0, y);
                Reach0(w - 1, y);
            }
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                Reach0(x + 1, y);
                Reach0(x - 1, y);
                Reach0(x, y + 1);
                Reach0(x, y - 1);
            }
            bool Near(int x, int y, int dx, int dy)
            {
                for (int k = 1; k <= Reach; k++)
                    if (Solid(image, x + dx * k, y + dy * k)) return true;
                return false;
            }
            var gaps = new bool[w, h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (Solid(image, x, y)) continue;
                    if (!outside[x, y])
                    {
                        gaps[x, y] = true;
                        continue;
                    }
                    bool across = Near(x, y, -1, 0) && Near(x, y, 1, 0) && (Near(x, y, 0, -1) || Near(x, y, 0, 1));
                    bool down = Near(x, y, 0, -1) && Near(x, y, 0, 1) && (Near(x, y, -1, 0) || Near(x, y, 1, 0));
                    gaps[x, y] = across || down;
                }
            }
            // Only gaps of a few pixels together: a stray see-through pixel between two parts is not a hole one sees.
            var seen = new bool[w, h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!gaps[x, y] || seen[x, y]) continue;
                    var group = new List<(int, int)>();
                    queue.Enqueue((x, y));
                    seen[x, y] = true;
                    while (queue.Count > 0)
                    {
                        var (cx, cy) = queue.Dequeue();
                        group.Add((cx, cy));
                        foreach (var (nx, ny) in new[] { (cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1) })
                        {
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h || seen[nx, ny] || !gaps[nx, ny]) continue;
                            seen[nx, ny] = true;
                            queue.Enqueue((nx, ny));
                        }
                    }
                    if (group.Count < Smallest)
                        foreach (var (gx, gy) in group) gaps[gx, gy] = false;
                }
            }
            return gaps;
        }

        const int Smallest = 6;

        public static int Count(PixelImage image) => Count(image, null);

        // Only the gaps where the body stood solid at rest in the same view: a tear in the body, not the room between
        // two legs or inside the bend of an arm, which the picture has at rest too.
        public static int Count(PixelImage image, PixelImage rest)
        {
            var gaps = Gaps(image);
            int n = 0;
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    if (gaps[x, y] && (rest == null || Solid(rest, x, y))) n++;
            return n < Smallest ? 0 : n;
        }

        // A thing drawn frame by frame: its name, every (view, pose, frame) to look at, how to draw one, and which
        // frame is its rest in each view.
        sealed class Subject
        {
            public string Name;
            public List<(BossView View, string Pose, int Frame)> Frames = new List<(BossView, string, int)>();
            public Func<BossView, string, int, PixelImage> Draw;
            public Func<BossView, PixelImage> Rest;
        }

        static readonly BossView[] Views = { BossView.Front, BossView.FrontSide, BossView.Side, BossView.BackSide, BossView.Back };

        static Subject Posed<TPose>(string name, Func<TPose, int> frames, Func<BossView, TPose, int, PixelImage> draw, TPose rest) where TPose : struct, Enum
        {
            var subject = new Subject { Name = name };
            foreach (var view in Views)
                foreach (TPose pose in Enum.GetValues(typeof(TPose)))
                    // A fall that breaks the body up on purpose is not looked at.
                    for (int f = 0; f < (pose.ToString() == "Die" ? 0 : frames(pose)); f++)
                        subject.Frames.Add((view, pose.ToString(), f));
            subject.Draw = (view, pose, frame) => draw(view, Enum.Parse<TPose>(pose), frame);
            subject.Rest = view => draw(view, rest, 0);
            return subject;
        }

        static IEnumerable<Subject> Subjects(string root)
        {
            yield return Posed<WardenPose>("cave_warden", WardenArt.FramesOf, (v, p, f) => WardenArt.Draw(v, p, f, 1), WardenPose.Idle);
            yield return Posed<KnightPose>("black_knight", KnightArt.FramesOf, (v, p, f) => KnightArt.Draw(v, p, f, 1), KnightPose.Idle);
            yield return Posed<SpiderPose>("silk_mother", SpiderArt.FramesOf, (v, p, f) => SpiderArt.Draw(v, p, f, 1), SpiderPose.Idle);
            yield return Posed<SamuraiPose>("shrine_samurai", SamuraiArt.FramesOf, (v, p, f) => SamuraiArt.Draw(v, p, f, 1), SamuraiPose.Idle);
            yield return Posed<VeinPose>("vein_eater", VeinEaterArt.FramesOf, (v, p, f) => VeinEaterArt.Draw(v, p, f, 1, false), VeinPose.Idle);
            yield return Posed<AntlionPose>("antlion", AntlionArt.FramesOf, (v, p, f) => AntlionArt.Draw(v, p, f, 1), AntlionPose.Idle);
            yield return Posed<CrystalQueenPose>("crystal_queen", CrystalQueenArt.FramesOf, (v, p, f) => CrystalQueenArt.Draw(v, p, f, 1), CrystalQueenPose.Idle);
            yield return Posed<GeneralPose>("mad_general", GeneralArt.FramesOf, (v, p, f) => GeneralArt.Draw(v, p, f, 1), GeneralPose.Idle);
            yield return Posed<SerpentPose>("tide_serpent", SerpentArt.FramesOf, (v, p, f) => SerpentArt.Draw(v, p, f, 1), SerpentPose.Idle);
            yield return Posed<LavaTitanPose>("lava_titan", LavaTitanArt.FramesOf, (v, p, f) => LavaTitanArt.Draw(v, p, f, 1), LavaTitanPose.Idle);
            yield return Posed<VoidLordPose>("void_lord", VoidLordArt.FramesOf, (v, p, f) => VoidLordArt.Draw(v, p, f, 1), VoidLordPose.Idle);
            yield return Posed<CrownWraithPose>("crown_wraith", CrownWraithArt.FramesOf, (v, p, f) => CrownWraithArt.Draw(v, p, f, 1), CrownWraithPose.Idle);
            // The bosses with the shared pictures.
            var drawn = new HashSet<string> { "cave_warden", "black_knight", "silk_mother", "shrine_samurai", "vein_eater", "antlion", "crystal_queen", "mad_general", "tide_serpent", "void_lord", "crown_wraith", "lava_titan" };
            var color = Rgba.Hex("#8C7A6B");
            foreach (var id in Ids(Path.Combine(root, "Assets", "_Project", "Data", "Bosses", "bosses.csv")).Where(i => !drawn.Contains(i)))
            {
                var subject = new Subject { Name = id };
                foreach (var view in Views)
                    foreach (BossPose pose in Enum.GetValues(typeof(BossPose)))
                        for (int f = 0; f < CreatureArt.IdleFrames; f++)
                            subject.Frames.Add((view, pose.ToString(), f));
                subject.Draw = (view, pose, frame) => CreatureArt.Boss(id, color, frame, view, Enum.Parse<BossPose>(pose));
                subject.Rest = view => CreatureArt.Boss(id, color, 0, view, BossPose.Idle);
                yield return subject;
            }
            // Every monster, both its views (side, back) and poses.
            foreach (var id in Ids(Path.Combine(root, "Assets", "_Project", "Data", "Monsters", "monsters.csv")))
            {
                var subject = new Subject { Name = id };
                foreach (var view in new[] { BossView.Side, BossView.Back })
                    foreach (MonsterPose pose in Enum.GetValues(typeof(MonsterPose)))
                        for (int f = 0; f < CreatureArt.IdleFrames; f++)
                            subject.Frames.Add((view, pose.ToString(), f));
                MonsterView Way(BossView view) => view == BossView.Back ? MonsterView.Back : MonsterView.Side;
                subject.Draw = (view, pose, frame) => CreatureArt.Monster(id, color, frame, Way(view), Enum.Parse<MonsterPose>(pose));
                subject.Rest = view => CreatureArt.Monster(id, color, 0, Way(view), MonsterPose.Idle);
                yield return subject;
            }
        }

        // A few frames side by side, the gaps in red (gaps_frames.png): to look at one fix before and after.
        public static bool WriteFrames(string folder, string root, string[] picks)
        {
            Directory.CreateDirectory(folder);
            var subjects = Subjects(root).ToDictionary(s => s.Name);
            var images = new List<PixelImage>();
            foreach (var pick in picks)
            {
                var w = pick.Split(':');
                images.Add(subjects[w[0]].Draw(Enum.Parse<BossView>(w[1]), w[2], int.Parse(w[3])));
            }
            const int scale = 4;
            int cw = images.Max(i => i.Width) * scale, ch = images.Max(i => i.Height) * scale;
            var sheet = new PixelImage(images.Count * cw, ch);
            for (int k = 0; k < images.Count; k++)
            {
                var image = images[k];
                var gaps = Gaps(image);
                for (int y = 0; y < image.Height * scale; y++)
                    for (int x = 0; x < image.Width * scale; x++)
                    {
                        var c = image[x / scale, y / scale];
                        sheet[k * cw + x, y] = gaps[x / scale, y / scale] ? new Rgba(255, 30, 30) : c.A > 0 ? c.WithAlpha(255) : new Rgba(40, 40, 52);
                    }
            }
            Png.Save(sheet, Path.Combine(folder, "gaps_frames.png"));
            return true;
        }

        static IEnumerable<string> Ids(string csv) =>
            File.ReadLines(csv).Skip(1).Select(l => l.Split(',')[0].Trim()).Where(i => i.Length > 0 && !i.StartsWith("#")).Distinct();

        public static bool Write(string folder, string root)
        {
            Directory.CreateDirectory(folder);
            var report = new System.Text.StringBuilder();
            var worst = new List<(string Name, BossView View, string Pose, int Frame, int Extra, PixelImage Image)>();
            int total = 0, flagged = 0;
            var lines = new List<string>();
            foreach (var subject in Subjects(root))
            {
                var rests = Views.ToDictionary(v => v, v => subject.Rest(v));
                int bad = 0, most = 0;
                string mostAt = "";
                foreach (var (view, pose, frame) in subject.Frames)
                {
                    total++;
                    var image = subject.Draw(view, pose, frame);
                    int extra = Count(image, rests[view]);
                    if (extra <= 0) continue;
                    bad++;
                    flagged++;
                    lines.Add($"  {subject.Name} {view} {pose} {frame}: +{extra}");
                    worst.Add((subject.Name, view, pose, frame, extra, image));
                    if (extra > most)
                    {
                        most = extra;
                        mostAt = $"{view} {pose} {frame}";
                    }
                }
                report.AppendLine($"{subject.Name}: {bad}/{subject.Frames.Count} frames" + (bad > 0 ? $", most +{most} at {mostAt}" : ""));
            }
            report.Insert(0, $"frames with gaps: {flagged} of {total}\n\n");
            report.AppendLine();
            report.AppendLine("## frames");
            foreach (var line in lines) report.AppendLine(line);
            File.WriteAllText(Path.Combine(folder, "gaps.txt"), report.ToString());

            // The worst frames (one each per thing and pose), the gaps in red, four times as big.
            var shown = worst.OrderByDescending(w => w.Extra).GroupBy(w => (w.Name, w.Pose)).Select(g => g.First()).Take(24).ToList();
            if (shown.Count > 0)
            {
                const int scale = 3;
                int cell = shown.Max(s => Math.Max(s.Image.Width, s.Image.Height)) * scale;
                var sheet = new PixelImage(Math.Min(6, shown.Count) * cell, (shown.Count + 5) / 6 * cell);
                for (int i = 0; i < shown.Count; i++)
                {
                    var image = shown[i].Image;
                    var gaps = Gaps(image);
                    int ox = i % 6 * cell, oy = i / 6 * cell;
                    for (int y = 0; y < image.Height * scale; y++)
                        for (int x = 0; x < image.Width * scale; x++)
                        {
                            var c = image[x / scale, y / scale];
                            sheet[ox + x, oy + y] = gaps[x / scale, y / scale] ? new Rgba(255, 30, 30) : c.A > 0 ? c.WithAlpha(255) : new Rgba(40, 40, 52);
                        }
                }
                Png.Save(sheet, Path.Combine(folder, "gaps.png"));
            }
            // Each thing's worst frames apart (gaps_<name>.png), its six worst poses.
            foreach (var group in worst.GroupBy(w => w.Name))
            {
                var picks = group.OrderByDescending(w => w.Extra).GroupBy(w => w.Pose).Select(g => g.First()).Take(6).ToList();
                const int scale = 4;
                int cw = picks.Max(p => p.Image.Width) * scale, ch = picks.Max(p => p.Image.Height) * scale;
                var sheet = new PixelImage(picks.Count * cw, ch);
                for (int i = 0; i < picks.Count; i++)
                {
                    var image = picks[i].Image;
                    var gaps = Gaps(image);
                    for (int y = 0; y < image.Height * scale; y++)
                        for (int x = 0; x < image.Width * scale; x++)
                        {
                            var c = image[x / scale, y / scale];
                            sheet[i * cw + x, y] = gaps[x / scale, y / scale] ? new Rgba(255, 30, 30) : c.A > 0 ? c.WithAlpha(255) : new Rgba(40, 40, 52);
                        }
                }
                Png.Save(sheet, Path.Combine(folder, $"gaps_{group.Key}.png"));
            }
            Console.WriteLine($"gaps: {flagged} of {total} frames have gaps their rest has not (anim/gaps.txt)");
            return true;
        }
    }
}
