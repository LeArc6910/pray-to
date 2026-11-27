using System;
using System.Collections.Generic;
using System.IO;

namespace Game.Client
{
    // The enemies' shots (out/shot/, ShotArt, E2 of Docs/EffectsRework.md): shots.png has a row an element — the two
    // frames of its beat flying right, three more ways it flies, a bigger one, a still one, then the five frames of
    // where it ends. The last row is what it must be told from: the old orange ball, a player's orb and arrow, and
    // the dots of the aim line. Again in greys (_gray), and a report of sizes.
    static class ShotPreview
    {
        public static bool Write(string folder, PixelImage floor)
        {
            Directory.CreateDirectory(folder);
            var rows = new List<List<PixelImage>>();
            var report = new List<string>();
            int reach = ShotArt.Reach(0.3f);
            for (int e = 0; e < ShotArt.Elements; e++)
            {
                var element = (ShotElement)e;
                var cells = new List<PixelImage>();
                cells.Add(Over(floor, ShotArt.Shot(element, reach, 0, 0)));
                cells.Add(Over(floor, ShotArt.Shot(element, reach, 0, 1)));
                foreach (int step in new[] { 5, 13, 22 }) cells.Add(Over(floor, ShotArt.Shot(element, reach, step, step % 2)));
                cells.Add(Over(floor, ShotArt.Shot(element, ShotArt.Reach(0.5f), 29, 0)));
                cells.Add(Over(floor, ShotArt.Shot(element, reach, -1, 0)));
                for (int f = 0; f < ShotArt.PopFrames; f++) cells.Add(Over(floor, ShotArt.Pop(element, f)));
                rows.Add(cells);
                var shot = ShotArt.Shot(element, reach, 0, 0);
                report.Add($"{element}: drawn {reach * 2 + 1}px across for {0.3f * 2 * TileArt.Size:0.0}px judged, picture {shot.Width}x{shot.Height}, one piece a shot and one where it ends");
            }
            var others = new List<PixelImage>
            {
                Over(floor, ItemArt.Spore), Over(floor, ItemArt.Orb), Over(floor, ItemArt.Arrow), Over(floor, Aim()),
            };
            rows.Add(others);
            var sheet = SkillFxPreview.Sheet(rows);
            Png.Save(SkillFxPreview.Scaled(sheet, 3), Path.Combine(folder, "shots.png"));
            Png.Save(SkillFxPreview.Scaled(SkillFxPreview.Grey(sheet), 3), Path.Combine(folder, "shots_gray.png"));
            File.WriteAllLines(Path.Combine(folder, "report.txt"), report);
            Console.WriteLine($"Wrote shot/shots.png, shots_gray.png and report.txt to {folder}");
            return true;
        }

        // The aim line as the view lays it: a dot every 0.6 tiles.
        static PixelImage Aim()
        {
            var image = new PixelImage(52, 21);
            var dot = ShotArt.AimDot();
            for (int i = 0; i < 5; i++)
                for (int y = 0; y < 3; y++)
                    for (int x = 0; x < 3; x++)
                        image[2 + i * 10 + x, 14 - i * 2 + y] = dot[x, y];
            return image;
        }

        static PixelImage Over(PixelImage floor, PixelImage image)
        {
            const int margin = 2;
            var cell = new PixelImage(image.Width + margin * 2, image.Height + margin * 2);
            for (int y = 0; y < cell.Height; y++)
                for (int x = 0; x < cell.Width; x++)
                    cell[x, y] = floor[x % floor.Width, y % floor.Height];
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    if (image.IsSet(x, y)) cell.Blend(x + margin, y + margin, image[x, y]);
            return cell;
        }
    }
}
