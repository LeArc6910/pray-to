using System.IO;
using Game.Core;

namespace Game.Client
{
    // --only forces (2026-11-28, Docs/Bosses.md "R3 그림"): R3's pictures on one sheet over the floors they lie on. On
    // the sand pit arena's floor: the sand pit at radius 1.1, 1.5, 2 and 3 tiles (four frames each) and the sand grain.
    // On the drowned hall's: a piece of the shallows (four frames), the wall of water (a band with both ends, four
    // frames, and two bands joined), the wake (four frames) and the drop. On the nest's: the thread (four frames, and
    // two lengths joined). With the five sounds.
    static class ForcesPreview
    {
        const int Scale = 3;

        public static bool Write(string folder, PixelImage sand, PixelImage flooded, PixelImage nest)
        {
            Directory.CreateDirectory(folder);
            const int Width = 400, SandEnd = 268, WaterEnd = 548, Height = 600;
            var sheet = new PixelImage(Width, Height);
            for (int ty = 0; ty < Height; ty++)
                for (int tx = 0; tx < Width; tx++)
                {
                    var floor = ty < SandEnd ? sand : ty < WaterEnd ? flooded : nest;
                    sheet[tx, ty] = floor[tx % floor.Width, ty % floor.Height];
                }

            void Put(PixelImage image, int x, int top, int fromX = 0, int fromY = 0, int w = -1, int h = -1)
            {
                if (w < 0) w = image.Width;
                if (h < 0) h = image.Height;
                for (int iy = 0; iy < h; iy++)
                    for (int ix = 0; ix < w; ix++)
                        if (sheet.Inside(x + ix, top + iy) && image[fromX + ix, fromY + iy].A > 0) sheet.Blend(x + ix, top + iy, image[fromX + ix, fromY + iy]);
            }

            int F = GroundArt.Frames;
            // The sand pit: radius 3 (96 px), then 2 (64), 1.5 (48) and 1.1 (35).
            int y = 4;
            for (int f = 0; f < F; f++) Put(GroundArt.Ground(HazardShape.Pit, false, 96, 96, 1, f), 4 + f * 99, y);
            y += 100;
            for (int f = 0; f < F; f++) Put(GroundArt.Ground(HazardShape.Pit, false, 64, 64, 2, f), 4 + f * 67, y);
            for (int f = 0; f < 2; f++) Put(GroundArt.Ground(HazardShape.Pit, false, 35, 35, 3, f * 2), 280 + f * 40, y);
            for (int f = 0; f < GroundArt.Frames; f++) Put(ForceArt.Grain(false, f), 364 + f % 2 * 10, y + 4 + f / 2 * 10);
            y += 68;
            for (int f = 0; f < F; f++) Put(GroundArt.Ground(HazardShape.Pit, false, 48, 48, 0, f), 4 + f * 51, y);
            // Grains flowing toward a pit, as the pull shows them.
            for (int i = 0; i < 14; i++) Put(ForceArt.Grain(false, i), 230 + i * 11, y + 10 + i * 5 % 31);
            y = SandEnd + 4;

            // The shallows (arena 10 tiles, 2 of water): its top, four frames.
            const int Outer = 10 * 16, Floor = 8 * 16;
            for (int f = 0; f < F; f++) Put(GroundArt.Edge(EdgeKind.Water, Outer, Floor, f), 2 + f * 100, y, Outer - 48, 0, 96, 52);
            y += 58;
            // The wall of water: 96 px with an end each side of a gap, four frames; then 64 + 32 joined beside a single 96.
            for (int f = 0; f < F; f++)
            {
                int x = 4 + f % 2 * 198, top = y + f / 2 * 22;
                Put(ForceArt.WaveBand(64, f), x, top);
                Put(ForceArt.WaveEnd(f), x + 64, top);
                Put(ForceArt.WaveEnd(f).FlippedX(), x + 64 + 8 + 24, top);
                Put(ForceArt.WaveBand(64, f), x + 64 + 8 + 24 + 8, top);
            }
            y += 48;
            Put(ForceArt.WaveBand(64, 0), 4, y);
            Put(ForceArt.WaveBand(32, 0), 68, y);
            Put(ForceArt.WaveBand(96, 0), 4, y + 18);
            // The wake's four frames, the drop's two, and drops flowing.
            for (int f = 0; f < F; f++) Put(ForceArt.Wake(f), 120 + f * 30, y + 6);
            for (int f = 0; f < ForceArt.GrainFrames; f++) Put(ForceArt.Grain(true, f), 250 + f * 10, y + 4);
            for (int i = 0; i < 12; i++) Put(ForceArt.Grain(true, i), 250 + i * 12, y + 14 + i * 7 % 19);
            y += 42;
            // Together: the wall crossing the floor toward the top with its gap, the wake behind it, over the shallows.
            Put(GroundArt.Edge(EdgeKind.Water, Outer, Floor, 1), 40, y, 0, Outer - 48, 320, 96);
            Put(ForceArt.WaveBand(128, 2), 60, y + 30);
            Put(ForceArt.WaveEnd(2), 188, y + 30);
            Put(ForceArt.WaveEnd(2).FlippedX(), 188 + 8 + 36, y + 30);
            Put(ForceArt.WaveBand(96, 2), 188 + 8 + 36 + 8, y + 30);
            Put(ForceArt.Wake(1), 150, y + 62);
            y = WaterEnd + 6;

            // The thread: four frames of 96 px; 64 + 32 joined; and one stretched across.
            for (int f = 0; f < F; f++) Put(ForceArt.Thread(96, f), 4 + f % 2 * 104, y + f / 2 * 8);
            Put(ForceArt.Thread(64, 0), 220, y);
            Put(ForceArt.Thread(32, 0), 284, y);
            Put(ForceArt.Thread(170, 1), 220, y + 10);
            y += 22;

            var cut = new PixelImage(Width, y);
            cut.Draw(sheet, 0, 0);
            Png.Save(SkillFxPreview.Scaled(cut, Scale), Path.Combine(folder, "sheet.png"));
            Png.Save(SkillFxPreview.Scaled(SkillFxPreview.Grey(cut), Scale), Path.Combine(folder, "sheet_gray.png"));
            foreach (var sound in new[] { Sound.SandDrag, Sound.WaveRush, Sound.ThreadCut, Sound.ThreadSnap, Sound.Tunnel })
            {
                var samples = SoundArt.Make(sound);
                Wav.Save(samples, SoundArt.SampleRate, Path.Combine(folder, sound.ToString().ToLowerInvariant() + ".wav"));
                System.Console.WriteLine($"forces: {sound} {samples.Length / (float)SoundArt.SampleRate:0.00}s");
            }
            System.Console.WriteLine($"forces: {Path.Combine(folder, "sheet.png")}");
            return true;
        }
    }
}
