using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;

namespace Game.Client
{
    // The boss's warnings and the blow that lands in their shape (out/hazard/, HazardArt, E1 of Docs/EffectsRework.md):
    // warn.png has a row a shape (circle, a place to gather, ring, wedge, line, a charge's path), filling from left to
    // right with its last two frames the blink; blast.png has the blow a frame at a time and what it leaves on each
    // floor. Each again in greys (_gray: a warning must read by value alone), and a report of sizes.
    static class HazardPreview
    {
        static readonly (string Name, HazardForm Form, float Size, float Width, float Degrees, WarnTone Tone)[] Rows =
        {
            ("circle", HazardForm.Circle, 2.5f, 0, 0, WarnTone.Danger),
            ("small circle", HazardForm.Circle, 1f, 0, 0, WarnTone.Danger),
            ("gather", HazardForm.Circle, 2f, 0, 0, WarnTone.Gather),
            ("ring", HazardForm.Donut, 3.6f, 1.7f, 0, WarnTone.Danger),
            ("wedge", HazardForm.Cone, 4.2f, 80, 20, WarnTone.Danger),
            ("line", HazardForm.Band, 7f, 1.3f, -25, WarnTone.Danger),
            ("path", HazardForm.Band, 6f, 1.5f, 0, WarnTone.Rush),
        };

        static readonly float[] Fills = { 0f, 0.2f, 0.45f, 0.7f, 0.84f, 0.9f, 0.94f };
        static readonly float[] Ages = { 0.02f, 0.07f, 0.11f, 0.16f, 0.22f, 0.28f, 0.34f, 0.4f };

        public static bool Write(string folder, PixelImage floor)
        {
            Directory.CreateDirectory(folder);
            var warn = new List<List<PixelImage>>();
            var blast = new List<List<PixelImage>>();
            var report = new List<string>();
            int n = 0;
            foreach (var row in Rows)
            {
                var field = HazardArt.Field(row.Form, row.Size, row.Width, row.Degrees * (float)Math.PI / 180);
                var buffer = field.Buffer();
                var cells = new List<PixelImage>();
                for (int i = 0; i < Fills.Length; i++)
                {
                    float p = (float)Math.Floor(Fills[i] * HazardArt.FillSteps) / HazardArt.FillSteps;
                    HazardArt.Warn(field, row.Tone, p, Fills[i] > HazardArt.BlinkFrom && i % 2 == 1, false, buffer, false);
                    cells.Add(Over(floor, field, buffer));
                }
                warn.Add(cells);
                cells = new List<PixelImage>();
                var ramp = HazardArt.Tone(row.Tone);
                foreach (float age in Ages)
                {
                    HazardArt.Blast(field, ramp, age, n, buffer, false);
                    cells.Add(Over(floor, field, buffer));
                }
                // What it leaves, on stone and on the floor of another biome.
                foreach (var stuff in new[] { FloorStuff.Stone, (FloorStuff)(1 + n % 6) })
                {
                    HazardArt.Scar(field, stuff, n, buffer, false);
                    cells.Add(Over(floor, field, buffer));
                }
                blast.Add(cells);
                report.Add($"{row.Name}: {field.Width}x{field.Height} pixels, scale {field.Scale}, blow {HazardArt.BlastSeconds(field.Form):0.00}s");
                n++;
            }
            var bits = new List<PixelImage>();
            for (int s = 0; s <= (int)FloorStuff.Void; s++)
            {
                for (int v = 0; v < HazardArt.Chunks; v++) bits.Add(HazardArt.Chunk((FloorStuff)s, v));
                bits.Add(HazardArt.Spark(HazardArt.Stuff((FloorStuff)s)[0], HazardArt.Stuff((FloorStuff)s)[1]));
            }
            bits.Add(HazardArt.Puff(7));
            bits.Add(HazardArt.Puff(9));
            var warnSheet = SkillFxPreview.Sheet(warn);
            var blastSheet = SkillFxPreview.Sheet(blast);
            Png.Save(SkillFxPreview.Scaled(warnSheet, 2), Path.Combine(folder, "warn.png"));
            Png.Save(SkillFxPreview.Scaled(SkillFxPreview.Grey(warnSheet), 2), Path.Combine(folder, "warn_gray.png"));
            Png.Save(SkillFxPreview.Scaled(blastSheet, 2), Path.Combine(folder, "blast.png"));
            Png.Save(SkillFxPreview.Scaled(SkillFxPreview.Grey(blastSheet), 2), Path.Combine(folder, "blast_gray.png"));
            Png.Save(SkillFxPreview.Scaled(SkillFxPreview.Sheet(new List<List<PixelImage>> { Padded(bits) }), 6), Path.Combine(folder, "pieces.png"));
            File.WriteAllLines(Path.Combine(folder, "report.txt"), report);
            Console.WriteLine($"Wrote hazard/warn.png, warn_gray.png, blast.png, blast_gray.png, pieces.png and report.txt to {folder}");
            return true;
        }

        static List<PixelImage> Padded(List<PixelImage> bits)
        {
            var cells = new List<PixelImage>();
            foreach (var bit in bits)
            {
                var cell = new PixelImage(11, 11);
                cell.Fill(0, 0, 11, 11, Rgba.Hex("#3A3440"));
                for (int y = 0; y < bit.Height; y++)
                    for (int x = 0; x < bit.Width; x++)
                        if (bit.IsSet(x, y)) cell.Blend(x + (11 - bit.Width) / 2, y + (11 - bit.Height) / 2, bit[x, y]);
                cells.Add(cell);
            }
            return cells;
        }

        // The picture laid over the floor, with a margin.
        static PixelImage Over(PixelImage floor, HazardField field, byte[] buffer)
        {
            const int margin = 4;
            var cell = new PixelImage(field.Width + margin * 2, field.Height + margin * 2);
            for (int y = 0; y < cell.Height; y++)
                for (int x = 0; x < cell.Width; x++)
                    cell[x, y] = floor[x % floor.Width, y % floor.Height];
            var image = HazardArt.Image(field, buffer);
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    if (image.IsSet(x, y)) cell.Blend(x + margin, y + margin, image[x, y]);
            return cell;
        }
    }
}
