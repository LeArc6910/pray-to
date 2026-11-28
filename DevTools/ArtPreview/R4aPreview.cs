using System.IO;
using Game.Core;

namespace Game.Client
{
    // --only r4a (2026-11-29, Docs/Bosses.md "R4a 그림"): R4a's pictures on one sheet over the floors they stand on. On
    // the crystal nest's floor: the crystal's four frames unlit, then at a third, two thirds and full light, the
    // chime's ring, the egg whole and at one and two cracks (four frames each), and a scene (three crystals in their
    // order, eggs between). On the war camp's: the banner's four frames, the mark's, the phantom line (a band with an
    // end each side of a gap, four frames; 64 + 32 joined beside one of 96), and a scene. With the eight sounds.
    static class R4aPreview
    {
        const int Scale = 3;

        public static bool Write(string folder, PixelImage nest, PixelImage camp)
        {
            Directory.CreateDirectory(folder);
            const int Width = 300, NestEnd = 330, Height = 620;
            var sheet = new PixelImage(Width, Height);
            for (int ty = 0; ty < Height; ty++)
                for (int tx = 0; tx < Width; tx++)
                {
                    var floor = ty < NestEnd ? nest : camp;
                    sheet[tx, ty] = floor[tx % floor.Width, ty % floor.Height];
                }

            void Put(PixelImage image, int x, int top)
            {
                for (int iy = 0; iy < image.Height; iy++)
                    for (int ix = 0; ix < image.Width; ix++)
                        if (sheet.Inside(x + ix, top + iy) && image[ix, iy].A > 0) sheet.Blend(x + ix, top + iy, image[ix, iy]);
            }

            // Tinted as the game tints a near white picture.
            PixelImage Tint(PixelImage image, Rgba tint)
            {
                var copy = image.Copy();
                for (int iy = 0; iy < copy.Height; iy++)
                    for (int ix = 0; ix < copy.Width; ix++)
                    {
                        var c = copy[ix, iy];
                        if (c.A > 0) copy[ix, iy] = new Rgba(c.R * tint.R / 255, c.G * tint.G / 255, c.B * tint.B / 255, c.A);
                    }
                return copy;
            }

            int F = GroundArt.Frames;
            int y = 4;
            // The crystal: unlit and full, four frames each; then a third and two thirds.
            for (int f = 0; f < F; f++) Put(GroundArt.Crystal(f, 0), 4 + f * 36, y);
            for (int f = 0; f < F; f++) Put(GroundArt.Crystal(f, 1), 152 + f * 36, y);
            y += 44;
            for (int f = 0; f < F; f++) Put(GroundArt.Crystal(f, 0.33f), 4 + f * 36, y);
            for (int f = 0; f < F; f++) Put(GroundArt.Crystal(f, 0.66f), 152 + f * 36, y);
            y += 46;
            // The eggs: whole, one crack, two.
            for (int cracks = 0; cracks <= 2; cracks++)
                for (int f = 0; f < F; f++) Put(GroundArt.Egg(f, cracks), 4 + cracks * 98 + f * 24, y);
            y += 28;
            // The chime's ring: as drawn, and tinted pink and blue.
            for (int f = 0; f < F; f++) Put(ForceArt.Chime(f), 4 + f * 26, y);
            for (int f = 0; f < F; f++) Put(Tint(ForceArt.Chime(f), new Rgba(255, 170, 220)), 112 + f * 26, y);
            y += 30;
            // Together: three crystals in their order (the first full, the second coming on, the third dark), rings
            // going off the lit ones, and eggs on the floor between.
            Put(Tint(ForceArt.Chime(2), new Rgba(190, 235, 255)), 28, y + 26);
            Put(GroundArt.Crystal(1, 1), 24, y + 6);
            Put(Tint(ForceArt.Chime(0), new Rgba(190, 235, 255)), 108, y + 60);
            Put(GroundArt.Crystal(2, 0.5f), 104, y + 40);
            Put(GroundArt.Crystal(0, 0), 190, y + 10);
            Put(GroundArt.Egg(0, 0), 70, y + 24);
            Put(GroundArt.Egg(1, 1), 150, y + 70);
            Put(GroundArt.Egg(2, 2), 236, y + 60);
            Put(GroundArt.Egg(3, 0), 40, y + 76);
            Put(GroundArt.Egg(2, 1), 262, y + 20);
            y = NestEnd + 4;

            // The banner's four frames and the mark's.
            for (int f = 0; f < F; f++) Put(GroundArt.Banner(f), 4 + f * 28, y);
            for (int f = 0; f < F; f++) Put(ForceArt.Target(f), 124 + f * 36, y + 8);
            y += 54;
            // The phantom line: 64 px, an end, a gap, an end, 64 px; four frames.
            for (int f = 0; f < F; f++)
            {
                int top = y + f * 19;
                Put(ForceArt.Phalanx(64, f), 4, top);
                Put(ForceArt.PhalanxEnd(f), 68, top);
                Put(ForceArt.PhalanxEnd(f).FlippedX(), 68 + 8 + 28, top);
                Put(ForceArt.Phalanx(64, f), 68 + 8 + 28 + 8, top);
            }
            // 64 + 32 joined, and one of 96.
            Put(ForceArt.Phalanx(64, 0), 196, y);
            Put(ForceArt.Phalanx(32, 0), 260, y);
            Put(ForceArt.Phalanx(96, 0), 196, y + 19);
            y += 82;
            // Together: the banner, a line coming up the floor with its gap, and the mark where someone stands.
            Put(ForceArt.Target(1), 200, y + 66);
            Put(GroundArt.Banner(1), 140, y + 2);
            Put(ForceArt.Phalanx(96, 2), 10, y + 62);
            Put(ForceArt.PhalanxEnd(2), 106, y + 62);
            Put(ForceArt.PhalanxEnd(2).FlippedX(), 106 + 8 + 30, y + 62);
            Put(ForceArt.Phalanx(128, 2), 106 + 8 + 30 + 8, y + 62);
            Put(ForceArt.Phalanx(288, 0), 6, y + 104);
            y += 124;

            var cut = new PixelImage(Width, y);
            cut.Draw(sheet, 0, 0);
            Png.Save(SkillFxPreview.Scaled(cut, Scale), Path.Combine(folder, "sheet.png"));
            Png.Save(SkillFxPreview.Scaled(SkillFxPreview.Grey(cut), Scale), Path.Combine(folder, "sheet_gray.png"));
            foreach (var sound in new[] { Sound.CrystalChime, Sound.CrystalShatter, Sound.EggCrack, Sound.EggHatch, Sound.BannerPlant, Sound.BannerFall, Sound.WarHorn, Sound.ArrowRain })
            {
                var samples = SoundArt.Make(sound);
                Wav.Save(samples, SoundArt.SampleRate, Path.Combine(folder, sound.ToString().ToLowerInvariant() + ".wav"));
                System.Console.WriteLine($"r4a: {sound} {samples.Length / (float)SoundArt.SampleRate:0.00}s");
            }
            System.Console.WriteLine($"r4a: {Path.Combine(folder, "sheet.png")}");
            return true;
        }
    }
}
