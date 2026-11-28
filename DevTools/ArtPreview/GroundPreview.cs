using System.IO;
using Game.Core;

namespace Game.Client
{
    // --only ground (2026-11-28, Docs/Bosses.md "R2a 그림"): a boss's ground on one sheet, each kind's four frames: a
    // pool and a strip of lava, cooled rock, shallow water, a crack as a strip and as a pool, a piece of the lava edge
    // with an island on it, the vent (and R2b's pillar and vein as they stand), and a small scene of them together.
    static class GroundPreview
    {
        const int Scale = 3;

        public static bool Write(string folder, PixelImage floor)
        {
            Directory.CreateDirectory(folder);
            const int Width = 216;
            var sheet = new PixelImage(Width, 560);
            for (int ty = 0; ty < sheet.Height; ty++)
                for (int tx = 0; tx < sheet.Width; tx++)
                    sheet[tx, ty] = floor[tx % floor.Width, ty % floor.Height];

            void Put(PixelImage image, int x, int top, int fromX = 0, int fromY = 0, int w = -1, int h = -1)
            {
                if (w < 0) w = image.Width;
                if (h < 0) h = image.Height;
                for (int iy = 0; iy < h; iy++)
                    for (int ix = 0; ix < w; ix++)
                        if (image[fromX + ix, fromY + iy].A > 0) sheet.Blend(x + ix, top + iy, image[fromX + ix, fromY + iy]);
            }

            int y = 4;
            int F = GroundArt.Frames;
            // Lava: a pool three tiles across; a strip six tiles by one and a half.
            for (int f = 0; f < F; f++) Put(GroundArt.Ground(HazardShape.Lava, false, 48, 48, f % 3, f), 4 + f * 53, y);
            y += 52;
            for (int f = 0; f < F; f++) Put(GroundArt.Ground(HazardShape.Lava, true, 96, 24, 0, f), 6 + f % 2 * 104, y + f / 2 * 28);
            y += 60;
            // Cooled rock: a meteor's stepping stone and an island.
            for (int f = 0; f < F; f++) Put(GroundArt.Ground(HazardShape.Rock, false, 40, 40, f % 3, f), 8 + f * 53, y);
            y += 46;
            // Shallow water.
            for (int f = 0; f < F; f++) Put(GroundArt.Ground(HazardShape.Water, false, 48, 48, f % 3, f), 4 + f * 53, y);
            y += 54;
            // A crack: an arm of the cross (strip), and one as a pool.
            for (int f = 0; f < F; f++) Put(GroundArt.Ground(HazardShape.Crack, true, 96, 16, 0, f), 6 + f % 2 * 104, y + f / 2 * 20);
            y += 44;
            for (int f = 0; f < F; f++) Put(GroundArt.Ground(HazardShape.Crack, false, 40, 40, f % 3, f), 8 + f * 53, y);
            y += 46;
            // The lava edge (arena 10 tiles, floor 7): its top, with an island of rock in the band.
            const int Outer = 11 * 16, Floor = 7 * 16;
            for (int f = 0; f < F; f++)
            {
                int x = 6 + f % 2 * 104, top = y + f / 2 * 64;
                Put(GroundArt.Edge(EdgeKind.Lava, Outer, Floor, f), x, top, Outer - 50, 14, 100, 60);
                Put(GroundArt.Ground(HazardShape.Rock, false, 28, 28, 1, f), x + 36, top + 10);
            }
            y += 132;
            // The vent's four frames, then R2b's pillar and vein as they stand.
            for (int f = 0; f < F; f++) Put(GroundArt.Prop(PropKind.Vent, f), 8 + f * 32, y);
            Put(GroundArt.Prop(PropKind.Pillar, 0), 146, y);
            Put(GroundArt.Prop(PropKind.Vein, 0), 180, y);
            y += 32;
            // Together: lava with a rock on it, a vent, a strip of lava and a crack across.
            Put(GroundArt.Ground(HazardShape.Lava, false, 64, 64, 2, 1), 4, y);
            Put(GroundArt.Ground(HazardShape.Rock, false, 32, 32, 0, 0), 26, y + 20);
            Put(GroundArt.Ground(HazardShape.Lava, true, 128, 16, 1, 2), 78, y + 6);
            Put(GroundArt.Prop(PropKind.Vent, 2), 90, y + 30);
            Put(GroundArt.Ground(HazardShape.Crack, true, 80, 16, 2, 1), 126, y + 34);
            Put(GroundArt.Ground(HazardShape.Water, false, 32, 32, 1, 0), 176, y + 44);
            y += 80;

            var cut = new PixelImage(Width, y);
            cut.Draw(sheet, 0, 0);
            Png.Save(SkillFxPreview.Scaled(cut, Scale), Path.Combine(folder, "sheet.png"));
            Png.Save(SkillFxPreview.Scaled(SkillFxPreview.Grey(cut), Scale), Path.Combine(folder, "sheet_gray.png"));
            // The sounds that go with them.
            foreach (var sound in new[] { Sound.LavaBubble, Sound.Overheat })
                Wav.Save(SoundArt.Make(sound), SoundArt.SampleRate, Path.Combine(folder, sound.ToString().ToLowerInvariant() + ".wav"));
            System.Console.WriteLine($"ground: {Path.Combine(folder, "sheet.png")}");
            return true;
        }
    }
}
