using System.IO;


namespace Game.Client
{
    // --only r1b (2026-11-28, Docs/BossRedesign.md R1b): the shrine samurai waiting sheathed with the blossom mark over
    // it, the black knight with its helm off, the helm where it fell, and the near and far marks, on one sheet.
    static class R1bPreview
    {
        const int Scale = 3;

        public static bool Write(string folder, PixelImage floor)
        {
            Directory.CreateDirectory(folder);
            int cellS = SamuraiArt.Width - 40, cellK = KnightArt.Width - 44;
            int width = 8 * cellK, y = 0;
            int rowS = SamuraiArt.Height + 14, rowK = KnightArt.Height - 20, rowSigns = 30;
            var sheet = new PixelImage(width, 2 * rowS + 3 * rowK + rowSigns);
            for (int ty = 0; ty < sheet.Height; ty++)
                for (int tx = 0; tx < sheet.Width; tx++)
                    sheet[tx, ty] = floor[tx % floor.Width, ty % floor.Height];

            void Put(PixelImage image, int x, int top)
            {
                for (int iy = 0; iy < image.Height; iy++)
                    for (int ix = 0; ix < image.Width; ix++)
                        if (image[ix, iy].A > 0) sheet.Blend(x + ix, top + iy, image[ix, iy]);
            }

            PixelImage Samurai(int sector, int frame)
            {
                var (view, mirrored, aim) = SamuraiArt.Facing(sector, SamuraiPose.Sheathe, frame);
                var image = SamuraiArt.Draw(view, SamuraiPose.Sheathe, frame, 2, aim);
                return mirrored ? image.FlippedX() : image;
            }

            // The samurai: waiting, the eight ways, the mark over its hat.
            int frames = SamuraiArt.FramesOf(SamuraiPose.Sheathe);
            for (int sector = 0; sector < 8; sector++)
            {
                int x = sector * cellS + 10;
                Put(Samurai(sector, SamuraiArt.SheatheSettle + sector), x - 20, y + 14);
                var sign = EffectArt.SakuraSign(sector);
                Put(sign, x - 20 + SamuraiArt.Width / 2 - sign.Width / 2, y + 14 + SamuraiArt.Height - SamuraiArt.Foot - SamuraiArt.BodyHeight - sign.Height - 8);
            }
            y += rowS;
            // Sinking into it and a round of the loop, facing right, every other frame.
            for (int k = 0; k < 8; k++)
                Put(Samurai(0, k < 4 ? k * 2 : SamuraiArt.SheatheSettle + (k - 4) * 2), k * cellS - 10, y + 14);
            y += rowS;

            // The knight, helm off: the five views at rest; then a helmed one beside for the difference, and the helm.
            var views = new[] { BossView.Front, BossView.FrontSide, BossView.Side, BossView.BackSide, BossView.Back };
            for (int v = 0; v < views.Length; v++) Put(KnightArt.Draw(views[v], KnightPose.Idle, 0, 3), v * cellK - 22, y - 30);
            Put(KnightArt.Draw(BossView.Front, KnightPose.Idle, 0, 2), 5 * cellK - 22, y - 30);
            Put(KnightArt.Draw(BossView.Side, KnightPose.Idle, 0, 2), 6 * cellK - 22, y - 30);
            var helm = KnightArt.Helmet();
            Put(helm, 7 * cellK + 30, y + rowK - 30);
            y += rowK;
            // In its blows: the hammer raised, the blow landed, the roar, the run; from the side and the front.
            var poses = new[] { (KnightPose.Raise, 5), (KnightPose.Smash, KnightArt.StrikeFrame(KnightPose.Smash)), (KnightPose.Roar, KnightArt.RoarRise), (KnightPose.Charge, 1) };
            for (int k = 0; k < poses.Length; k++)
            {
                Put(KnightArt.Draw(BossView.Side, poses[k].Item1, poses[k].Item2, 3), 2 * k * cellK - 22, y - 30);
                Put(KnightArt.Draw(BossView.Front, poses[k].Item1, poses[k].Item2, 3), (2 * k + 1) * cellK - 22, y - 30);
            }
            y += rowK;
            var back = new[] { (KnightPose.Wind, 5), (KnightPose.Sweep, KnightArt.StrikeFrame(KnightPose.Sweep)), (KnightPose.Walk, 2), (KnightPose.Crouch, 5) };
            for (int k = 0; k < back.Length; k++)
            {
                Put(KnightArt.Draw(BossView.FrontSide, back[k].Item1, back[k].Item2, 3), 2 * k * cellK - 22, y - 30);
                Put(KnightArt.Draw(BossView.BackSide, back[k].Item1, back[k].Item2, 3), (2 * k + 1) * cellK - 22, y - 30);
            }
            y += rowK;

            // The marks: the strong blow's warning for the difference, the blossom, near and far, every frame.
            int sx = 4;
            Put(EffectArt.WarningSign(), sx, y + 8);
            sx += 20;
            for (int f = 0; f < EffectArt.SakuraSignFrames; f++, sx += EffectArt.SakuraSignSize + 3) Put(EffectArt.SakuraSign(f), sx, y + 4);
            sx += 8;
            for (int f = 0; f < EffectArt.ColorSignFrames; f++, sx += EffectArt.ColorSignSize + 2) Put(EffectArt.ColorSign(true, f), sx, y + 3);
            sx += 8;
            for (int f = 0; f < EffectArt.ColorSignFrames; f++, sx += EffectArt.ColorSignSize + 2) Put(EffectArt.ColorSign(false, f), sx, y + 3);

            Png.Save(SkillFxPreview.Scaled(sheet, Scale), Path.Combine(folder, "sheet.png"));
            Png.Save(SkillFxPreview.Scaled(SkillFxPreview.Grey(sheet), Scale), Path.Combine(folder, "sheet_gray.png"));
            System.Console.WriteLine($"r1b: {Path.Combine(folder, "sheet.png")} (sheathe {frames} frames, settle {SamuraiArt.SheatheSettle})");
            return true;
        }
    }
}
