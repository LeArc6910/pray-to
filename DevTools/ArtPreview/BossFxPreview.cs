using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Client
{
    // The bosses' own effects (out/bossfx/, BossEffects, E6 of Docs/EffectsRework.md): a sheet a boss, a row an
    // effect, a frame a moment, each laid over the blow's own shape burning under it as the game lays it. Each sheet
    // again in greys, and report.txt with the most pieces and lights each effect lays at once. Fails when one lays
    // more than the limits (72 pieces, 3 lights).
    static class BossFxPreview
    {
        sealed class Row
        {
            public string Boss, Name;
            public BossFx Fx;
            public BossContext Context;
            // The blow's shape under it (sizes in tiles, as a hazard has them), and the colours it burns in.
            public HazardForm Form;
            public float Size, Width, Degrees;
            public FxRamp? Element;
            public WarnTone Tone = WarnTone.Danger;
            public bool NoShape;
            // A loop over the warning: the frames are moments of its filling.
            public bool Warning;
            public int Wide = 112, Tall = 96;
            // Where the origin is in the cell.
            public float AtX = 0.5f, AtY = 0.6f;
        }

        const int T = 16;

        static readonly float[] Ages = { 0.02f, 0.07f, 0.14f, 0.24f, 0.36f, 0.5f, 0.62f, 0.8f };
        static readonly float[] Fills = { 0.1f, 0.4f, 0.7f, 0.78f, 0.86f, 0.92f, 0.97f, 1f };

        static List<Row> Rows()
        {
            var rows = new List<Row>();
            void Add(string boss, string name, BossFx fx, HazardForm form, float size, float width, float degrees, FxRamp? element, Action<Row> more = null)
            {
                var row = new Row
                {
                    Boss = boss, Name = name, Fx = fx, Form = form, Size = size, Width = width, Degrees = degrees, Element = element,
                    Context = new BossContext { Size = size * T, Width = width * T, Dir = degrees, Ramp = element ?? FxRamp.Fire, Seed = rows.Count * 7 + 3 },
                };
                more?.Invoke(row);
                rows.Add(row);
            }
            var crystal = FxRamp.Crystal;
            Add("crystal_queen", "bite", BossFx.Fangs, HazardForm.Cone, 3.2f, 90, 0, crystal, r => { r.AtX = 0.25f; r.AtY = 0.5f; });
            Add("crystal_queen", "crystal_rain warned", BossFx.ShardDrop, HazardForm.Circle, 1.4f, 0, 0, crystal, r => { r.Warning = true; r.Tall = 144; r.AtY = 0.78f; });
            Add("crystal_queen", "crystal_rain", BossFx.ShardFall, HazardForm.Circle, 1.4f, 0, 0, crystal);
            Add("crystal_queen", "shard_hail", BossFx.ShardFall, HazardForm.Circle, 1.3f, 0, 0, crystal, r => r.Context.Big = true);
            Add("crystal_queen", "web_quake", BossFx.CrystalRing, HazardForm.Circle, 2f, 0, 0, crystal, r => r.Context.Size *= 0.86f);
            Add("crystal_queen", "crystal_ripple", BossFx.CrystalRing, HazardForm.Donut, 7f, 1.2f, 0, crystal, r =>
            {
                r.Context.Inner = (1.2f + 0.3f * (7 - 1.2f)) * T;
                r.Context.Width = 0;
                (r.Wide, r.Tall, r.AtY) = (240, 240, 0.5f);
            });
            Add("crystal_queen", "crystal_cross", BossFx.Beam, HazardForm.Band, 12f, 1.2f, 0, crystal, r => { r.Wide = 216; r.Tall = 64; r.AtX = 0.06f; r.AtY = 0.5f; });
            Add("crystal_queen", "crystal_lattice", BossFx.Beam, HazardForm.Band, 8f, 1.2f, 45, crystal, r => { r.Wide = 120; r.Tall = 120; r.AtX = 0.1f; r.AtY = 0.9f; });
            Add("crystal_queen", "venom_share", BossFx.Column, HazardForm.Circle, 1.8f, 0, 0, null, r => { r.Tone = WarnTone.Gather; r.Context.Ramp = crystal; r.Tall = 160; r.AtY = 0.78f; });
            Add("crystal_queen", "crown", BossFx.Spray, HazardForm.Circle, 1, 0, 90, crystal, r => { r.NoShape = true; r.Context = new BossContext { Ramp = crystal, Size = 46, Count = 8, Width = 60, Dir = 90, Seed = 5 }; });
            Add("crystal_queen", "shatter", BossFx.Spray, HazardForm.Circle, 1, 0, 0, crystal, r => { r.NoShape = true; r.Context = new BossContext { Ramp = crystal, Size = 52, Count = 16, Seed = 6 }; });
            var fire = FxRamp.Fire;
            Add("mad_general", "cleave", BossFx.Cleave, HazardForm.Cone, 3.4f, 110, 0, fire, r => { r.Context.Width = 110; r.AtX = 0.22f; r.AtY = 0.5f; r.Tall = 128; });
            Add("mad_general", "cleave white-hot", BossFx.Cleave, HazardForm.Cone, 3.4f, 110, 0, fire, r => { r.Context.Width = 110; r.Context.Big = true; r.AtX = 0.22f; r.AtY = 0.5f; r.Tall = 128; });
            Add("mad_general", "hellfire warned", BossFx.ShardDrop, HazardForm.Circle, 1.5f, 0, 0, fire, r => { r.Warning = true; r.Context.Count = 1; r.Tall = 144; r.AtY = 0.78f; });
            Add("mad_general", "hellfire", BossFx.FireCall, HazardForm.Circle, 1.5f, 0, 0, fire, r => r.Context.Count = 1);
            Add("mad_general", "meteors warned", BossFx.ShardDrop, HazardForm.Circle, 1.4f, 0, 0, fire, r => { r.Warning = true; r.Context.Count = 2; r.Tall = 144; r.AtY = 0.78f; });
            Add("mad_general", "meteors", BossFx.FireCall, HazardForm.Circle, 1.4f, 0, 0, fire, r => r.Context.Count = 2);
            Add("mad_general", "dark_cross", BossFx.Spears, HazardForm.Band, 12f, 1.2f, 0, null, r => { r.Context.Ramp = FxRamp.Danger; r.Wide = 216; r.Tall = 80; r.AtX = 0.06f; r.AtY = 0.7f; });
            Add("mad_general", "flame_ring", BossFx.FireBand, HazardForm.Donut, 6f, 2.4f, 0, fire, r => { r.Context.Inner = 2.4f * T; r.Context.Dir = 90; (r.Wide, r.Tall, r.AtY) = (216, 216, 0.5f); });
            Add("mad_general", "ember_wake", BossFx.FireBand, HazardForm.Donut, 3.6f, 1.2f, 0, fire, r => { r.Context.Inner = 1.2f * T; r.Context.Dir = 90; (r.Wide, r.Tall, r.AtY) = (136, 136, 0.5f); });
            Add("mad_general", "cinders", BossFx.Cinders, HazardForm.Circle, 1, 0, 0, fire, r => { r.NoShape = true; r.Tall = 48; r.Context = new BossContext { Ramp = fire, Size = 18, Count = 3, Seed = 4 }; });
            var dust = FxRamp.Dust;
            Add("stone", "slam", BossFx.Slam, HazardForm.Circle, 2.5f, 0, 0, dust, r => r.Tall = 112);
            Add("stone", "slam over cracks", BossFx.Slam, HazardForm.Circle, 2f, 0, 0, dust, r => r.Context.Plain = true);
            Add("stone", "cave_in", BossFx.Slam, HazardForm.Circle, 1.6f, 0, 0, dust, r => r.Context.Big = true);
            Add("stone", "sweep", BossFx.Sweep, HazardForm.Cone, 4f, 120, 0, dust, r => { r.Context.Width = 120; r.AtX = 0.2f; r.AtY = 0.5f; r.Tall = 144; });
            Add("stone", "shockwave", BossFx.Rumble, HazardForm.Donut, 5f, 2f, 0, dust, r => { r.Context.Inner = 2 * T; (r.Wide, r.Tall, r.AtY) = (184, 184, 0.5f); });
            Add("stone", "rock_trail", BossFx.Spike, HazardForm.Circle, 1.1f, 0, 0, dust);
            Add("stone", "stones", BossFx.Cinders, HazardForm.Circle, 1, 0, 0, dust, r => { r.NoShape = true; r.Tall = 64; r.Context = new BossContext { Ramp = dust, Size = 28, Count = 8, Width = 360, Dir = 90, Seed = 4 }; });
            var sand = FxRamp.Sand;
            Add("antlion", "bite", BossFx.SandBurst, HazardForm.Cone, 3.2f, 90, 0, sand, r => { r.Context.Width = 90; r.AtX = 0.25f; r.AtY = 0.5f; });
            Add("antlion", "sand_jet", BossFx.SandJet, HazardForm.Band, 10f, 1.4f, 0, sand, r => { r.Wide = 184; r.Tall = 64; r.AtX = 0.06f; r.AtY = 0.5f; });
            Add("antlion", "pit", BossFx.Whirl, HazardForm.Circle, 2.2f, 0, 0, sand);
            Add("antlion", "sandstorm", BossFx.Whirl, HazardForm.Circle, 1.8f, 0, 0, null, r => { r.Tone = WarnTone.Gather; r.Context.Ramp = sand; r.Context.Big = true; });
            Add("antlion", "sand_trail", BossFx.SandBurst, HazardForm.Circle, 1.1f, 0, 0, sand, r => r.Context.Width = 0);
            Add("antlion", "quicksand", BossFx.Rumble, HazardForm.Donut, 6f, 2.4f, 0, sand, r => { r.Context.Inner = 2.4f * T; (r.Wide, r.Tall, r.AtY) = (216, 216, 0.5f); });
            Add("antlion", "sand", BossFx.Cinders, HazardForm.Circle, 1, 0, 0, sand, r => { r.NoShape = true; r.Tall = 48; r.Context = new BossContext { Ramp = sand, Size = 20, Count = 3, Dir = 120, Seed = 4 }; });
            foreach (var (name, ramp) in new[] { ("lava", FxRamp.Fire), ("void", FxRamp.Violet), ("water", FxRamp.Cold), ("wraith", FxRamp.Spirit), ("wraith bolt", FxRamp.Bolt) })
            {
                Add("elements", name + " circle", BossFx.Element, HazardForm.Circle, 2.2f, 0, 0, ramp, r => r.Tall = 128);
                Add("elements", name + " wedge", BossFx.Element, HazardForm.Cone, 3.8f, 90, 0, ramp, r => { r.Context.Width = 90; r.Context.Form = HazardForm.Cone; r.AtX = 0.2f; r.AtY = 0.55f; r.Tall = 128; });
                Add("elements", name + " line", BossFx.Element, HazardForm.Band, 11f, 1.5f, 0, ramp, r => { r.Context.Form = HazardForm.Band; r.Wide = 200; r.Tall = 96; r.AtX = 0.06f; r.AtY = 0.6f; });
                Add("elements", name + " ring", BossFx.Element, HazardForm.Donut, 6.5f, 2.4f, 0, ramp, r => { r.Context.Form = HazardForm.Donut; r.Context.Inner = 2.4f * T; r.Context.Width = 0; (r.Wide, r.Tall, r.AtY) = (232, 232, 0.5f); });
            }
            return rows;
        }

        public static bool Write(string folder, PixelImage floor)
        {
            Directory.CreateDirectory(folder);
            var report = new List<string>();
            bool fine = true;
            foreach (var boss in Rows().GroupBy(row => row.Boss))
            {
                var sheet = new List<List<PixelImage>>();
                foreach (var row in boss)
                {
                    var cells = new List<PixelImage>();
                    int most = 0, lights = 0;
                    var stamps = new List<FxStamp>();
                    for (int i = 0; i < Ages.Length; i++)
                    {
                        cells.Add(Cell(row, floor, row.Warning ? Fills[i] : Ages[i]));
                    }
                    // The limits, at every thirtieth of a second of its life.
                    for (float t = 0; t < 2; t += 1 / 30f)
                    {
                        stamps.Clear();
                        var context = row.Context;
                        if (row.Warning) context.Progress = t / 2;
                        if (!BossEffects.Stamps(row.Fx, context, row.Warning ? 0 : t, stamps)) break;
                        most = Math.Max(most, stamps.Count(stamp => stamp.Shape != FxShape.Light));
                        lights = Math.Max(lights, stamps.Count(stamp => stamp.Shape == FxShape.Light));
                    }
                    report.Add($"{row.Boss} {row.Name}: {most} pieces, {lights} lights");
                    if (most > BossEffects.Most || lights > FxLayers.MostLights)
                    {
                        Console.Error.WriteLine($"boss effect {row.Boss} {row.Name}: {most} pieces and {lights} lights at once, more than {BossEffects.Most} and {FxLayers.MostLights}");
                        fine = false;
                    }
                    sheet.Add(cells);
                }
                var image = SkillFxPreview.Sheet(sheet);
                Png.Save(SkillFxPreview.Scaled(image, 2), Path.Combine(folder, boss.Key + ".png"));
                Png.Save(SkillFxPreview.Scaled(SkillFxPreview.Grey(image), 2), Path.Combine(folder, boss.Key + "_gray.png"));
            }
            File.WriteAllLines(Path.Combine(folder, "report.txt"), report);
            Console.WriteLine($"Wrote bossfx/<boss>.png, <boss>_gray.png and report.txt to {folder}");
            return fine;
        }

        static PixelImage Cell(Row row, PixelImage floor, float t)
        {
            var cell = new PixelImage(row.Wide, row.Tall);
            for (int y = 0; y < cell.Height; y++)
                for (int x = 0; x < cell.Width; x++)
                    cell[x, y] = floor[x % floor.Width, y % floor.Height];
            float ox = (float)Math.Round(row.Wide * row.AtX), oy = (float)Math.Round(row.Tall * row.AtY);
            var stamps = new List<FxStamp>();
            var context = row.Context;
            if (row.Warning) context.Progress = t;
            BossEffects.Stamps(row.Fx, context, row.Warning ? 0 : t, stamps);
            foreach (var stamp in stamps) if (stamp.Ground) MonsterFxPreview.Blit(cell, ox, oy, stamp);
            if (!row.NoShape)
            {
                var field = HazardArt.Field(row.Form, row.Size, row.Width, row.Degrees * (float)Math.PI / 180);
                var buffer = field.Buffer();
                bool drawn = true;
                if (row.Warning) HazardArt.Warn(field, row.Tone, (float)Math.Floor(t * HazardArt.FillSteps) / HazardArt.FillSteps, false, false, buffer, false);
                else
                {
                    drawn = HazardArt.Blast(field, row.Element.HasValue ? SkillFxArt.Ramp5(row.Element.Value) : HazardArt.Tone(row.Tone), t, 3, buffer, false);
                    if (!drawn) HazardArt.Scar(field, FloorStuff.Stone, 3, buffer, false);
                }
                var shape = HazardArt.Image(field, buffer);
                int left = (int)(ox - field.OriginX * field.Scale), top = (int)(oy - field.OriginY * field.Scale);
                for (int y = 0; y < shape.Height * field.Scale; y++)
                    for (int x = 0; x < shape.Width * field.Scale; x++)
                    {
                        var c = shape[x / field.Scale, y / field.Scale];
                        if (c.A > 0 && cell.Inside(left + x, top + y)) cell.Blend(left + x, top + y, c);
                    }
            }
            foreach (var stamp in stamps) if (!stamp.Ground && stamp.Shape != FxShape.Light) MonsterFxPreview.Blit(cell, ox, oy, stamp);
            return cell;
        }
    }
}
