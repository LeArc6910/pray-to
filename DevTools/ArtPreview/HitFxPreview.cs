using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Client
{
    // What a blow looks like where it lands on a body (out/hitfx/, HitFx, E7 of Docs/EffectsRework.md): a row an
    // element (an ordinary blow, then a strong one), a frame a moment, the blow coming from the left. The sheet again
    // in greys, and report.txt with the most pieces and lights each lays at once. Fails when one lays more than the
    // limits (12 pieces, 1 light) or lasts longer than 0.4 s.
    static class HitFxPreview
    {
        static readonly float[] Ages = { 0.02f, 0.06f, 0.1f, 0.14f, 0.19f, 0.25f, 0.31f, 0.37f };
        const int Wide = 64, Tall = 56;

        public static bool Write(string folder, PixelImage floor)
        {
            Directory.CreateDirectory(folder);
            var report = new List<string>();
            var sheet = new List<List<PixelImage>>();
            bool fine = true;
            void Row(string name, HitElement element, FxRamp ramp, FxRamp? star, bool strong, bool thin = false)
            {
                var cells = new List<PixelImage>();
                foreach (float age in Ages) cells.Add(Cell(floor, element, ramp, star, strong, thin, sheet.Count * 7 + 3, age));
                sheet.Add(cells);
                int most = 0, lights = 0;
                float life = 0;
                var stamps = new List<FxStamp>();
                for (float t = 0; t < 1; t += 1 / 60f)
                {
                    stamps.Clear();
                    if (!HitFx.Stamps(stamps, element, ramp, star, 20, strong, thin, sheet.Count * 7 + 3, t)) break;
                    life = t;
                    most = Math.Max(most, stamps.Count(stamp => stamp.Shape != FxShape.Light));
                    lights = Math.Max(lights, stamps.Count(stamp => stamp.Shape == FxShape.Light));
                }
                report.Add($"{name}: {most} pieces, {lights} lights, {life:0.00} s");
                if (most > HitFx.Most || lights > (strong ? 1 : 0) || life > HitFx.Longest)
                {
                    Console.Error.WriteLine($"hit effect {name}: {most} pieces, {lights} lights, {life:0.00} s; the limits are {HitFx.Most}, {(strong ? 1 : 0)} and {HitFx.Longest}");
                    fine = false;
                }
            }
            for (int i = 0; i < HitFx.Elements; i++)
            {
                var element = (HitElement)i;
                Row(element + "", element, HitFx.RampOf(element), FxRamp.Steel, false);
                Row(element + " strong", element, HitFx.RampOf(element), FxRamp.Gold, true);
            }
            Row("Plain on oneself", HitElement.Plain, FxRamp.Steel, FxRamp.Danger, false);
            Row("Fire added (no star)", HitElement.Fire, FxRamp.Fire, null, false);
            Row("Poison in a monster's violet", HitElement.Poison, FxRamp.Violet, FxRamp.Danger, false);
            Row("Fire among many (thin)", HitElement.Fire, FxRamp.Fire, FxRamp.Steel, false, true);
            var image = SkillFxPreview.Sheet(sheet);
            Png.Save(SkillFxPreview.Scaled(image, 3), Path.Combine(folder, "hits.png"));
            Png.Save(SkillFxPreview.Scaled(SkillFxPreview.Grey(image), 3), Path.Combine(folder, "hits_gray.png"));
            File.WriteAllLines(Path.Combine(folder, "report.txt"), report);
            Console.WriteLine($"Wrote hitfx/hits.png, hits_gray.png and report.txt to {folder}");
            return fine;
        }

        static PixelImage Cell(PixelImage floor, HitElement element, FxRamp ramp, FxRamp? star, bool strong, bool thin, int seed, float t)
        {
            var cell = new PixelImage(Wide, Tall);
            for (int y = 0; y < Tall; y++)
                for (int x = 0; x < Wide; x++)
                    cell[x, y] = floor[x % floor.Width, y % floor.Height];
            var stamps = new List<FxStamp>();
            HitFx.Stamps(stamps, element, ramp, star, 20, strong, thin, seed, t);
            foreach (var stamp in stamps) if (stamp.Shape != FxShape.Light) MonsterFxPreview.Blit(cell, 22, 30, stamp);
            return cell;
        }
    }
}
