using System.IO;
using Game.Core;

namespace Game.Client
{
    // --only r4b (2026-11-29, Docs/Bosses.md "R4b 그림"): R4b's pictures on one sheet over the floors they stand on. On
    // the sunken chapel's: the crown wraith true and false side by side (front, side and back at rest, then a blow),
    // the three together at game size, the mist's four frames, the drowned hand's six, and a scene. On the altar's:
    // the gate's four frames, the star's, the hourglass's six, and a scene (two gates, stars falling, the glass).
    // With the eight sounds.
    static class R4bPreview
    {
        const int Scale = 3;

        public static bool Write(string folder, PixelImage chapel, PixelImage altar)
        {
            Directory.CreateDirectory(folder);
            const int Width = 400, ChapelEnd = 410, Height = 680;
            var sheet = new PixelImage(Width, Height);
            for (int ty = 0; ty < Height; ty++)
                for (int tx = 0; tx < Width; tx++)
                {
                    var floor = ty < ChapelEnd ? chapel : altar;
                    sheet[tx, ty] = floor[tx % floor.Width, ty % floor.Height];
                }

            void Put(PixelImage image, int x, int top)
            {
                for (int iy = 0; iy < image.Height; iy++)
                    for (int ix = 0; ix < image.Width; ix++)
                        if (sheet.Inside(x + ix, top + iy) && image[ix, iy].A > 0) sheet.Blend(x + ix, top + iy, image[ix, iy]);
            }

            int F = ForceArt.Frames;
            int y = -16;
            // True then false, pair by pair: front, side, back at rest; a blow from the front side.
            var views = new[] { (BossView.Front, CrownWraithPose.Idle, 0), (BossView.Side, CrownWraithPose.Idle, 2), (BossView.Back, CrownWraithPose.Idle, 0), (BossView.FrontSide, CrownWraithPose.Storm, 6) };
            for (int k = 0; k < views.Length; k++)
            {
                var (view, pose, frame) = views[k];
                int row = y + (k / 2) * 92, x = (k % 2) * 200 - 8;
                Put(CrownWraithArt.Draw(view, pose, frame, 1), x, row);
                Put(CrownWraithArt.DrawFalse(view, pose, frame), x + 88, row);
            }
            y += 184;
            // The three together as the fight shows them: the true one between two false ones, and phase 3's true one.
            Put(CrownWraithArt.DrawFalse(BossView.Front, CrownWraithPose.Idle, 1), 0, y);
            Put(CrownWraithArt.Draw(BossView.Front, CrownWraithPose.Idle, 1, 2), 76, y);
            Put(CrownWraithArt.DrawFalse(BossView.Front, CrownWraithPose.Idle, 1), 152, y);
            Put(CrownWraithArt.DrawFalse(BossView.FrontSide, CrownWraithPose.Lift, 5), 228, y);
            Put(CrownWraithArt.Draw(BossView.FrontSide, CrownWraithPose.Lift, 5, 3), 304, y);
            y += 100;
            // The mist's four frames.
            for (int f = 0; f < F; f++) Put(ForceArt.Mist(f), 4 + f * 46, y);
            // The hand's six.
            for (int f = 0; f < ForceArt.HandFrames; f++) Put(ForceArt.Hand(f), 200 + f * 22, y);
            y += 34;
            // Together: the mist where the wraith was, a puddle with the hand up in it and one shut.
            Put(ForceArt.Mist(1), 30, y + 30);
            Put(ForceArt.Mist(3), 96, y + 10);
            Put(ForceArt.Hand(2), 190, y + 20);
            Put(ForceArt.Hand(3), 240, y + 40);
            Put(ForceArt.Hand(1), 300, y + 16);
            Put(ForceArt.Hand(5), 340, y + 44);
            y = ChapelEnd + 6;

            // The gate's four frames, the star's, the hourglass's six.
            for (int f = 0; f < F; f++) Put(ForceArt.Gate(f), 4 + f * 30, y);
            for (int f = 0; f < F; f++) Put(ForceArt.Star(f), 136 + f * 14, y + 6);
            for (int f = 0; f < ForceArt.HourglassFrames; f++) Put(ForceArt.Hourglass(f), 204 + f * 22, y + 6);
            y += 44;
            // Together: two gates across the floor, stars at different heights on their way down, the glass.
            Put(ForceArt.Gate(0), 30, y + 40);
            Put(ForceArt.Gate(2), 300, y + 90);
            Put(ForceArt.Star(0), 120, y + 20);
            Put(ForceArt.Star(1), 150, y + 60);
            Put(ForceArt.Star(2), 180, y + 100);
            Put(ForceArt.Star(3), 210, y + 36);
            Put(ForceArt.Star(1), 240, y + 120);
            Put(ForceArt.Hourglass(0), 120, y + 130);
            Put(ForceArt.Hourglass(3), 150, y + 150);
            Put(ForceArt.Hourglass(5), 60, y + 110);
            y += 180;

            var cut = new PixelImage(Width, y);
            cut.Draw(sheet, 0, 0);
            Png.Save(SkillFxPreview.Scaled(cut, Scale), Path.Combine(folder, "sheet.png"));
            Png.Save(SkillFxPreview.Scaled(SkillFxPreview.Grey(cut), Scale), Path.Combine(folder, "sheet_gray.png"));
            // The three at the size a phone shows them (one to one), to check the crowns tell at a glance.
            var small = new PixelImage(Width, 100);
            small.Draw(cut, 0, -168);
            Png.Save(small, Path.Combine(folder, "three_small.png"));
            foreach (var sound in new[] { Sound.CloneSplit, Sound.CloneBreak, Sound.PuddleZap, Sound.HandGrab, Sound.Rewind, Sound.GateWarp, Sound.StarFall, Sound.GravityFlip })
            {
                var samples = SoundArt.Make(sound);
                Wav.Save(samples, SoundArt.SampleRate, Path.Combine(folder, sound.ToString().ToLowerInvariant() + ".wav"));
                float peak = 0;
                foreach (var s in samples) peak = System.Math.Max(peak, System.Math.Abs(s));
                System.Console.WriteLine($"r4b: {sound} {samples.Length / (float)SoundArt.SampleRate:0.00}s peak {peak:0.00}");
            }
            System.Console.WriteLine($"r4b: {Path.Combine(folder, "sheet.png")}");
            return true;
        }
    }
}
