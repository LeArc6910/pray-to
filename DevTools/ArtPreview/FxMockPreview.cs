using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core;
using static Game.Client.Fx;

namespace Game.Client
{
    // The effects rework's mock-ups (out/fxmock/, Docs/EffectsRework.md): fifteen effects, each as the game draws it
    // now beside how the rework would draw it. "Now" is the game's own code where the preview can run it (skills,
    // ordinary monsters) and a copy of what WorldView does where it cannot (a boss's marks, blasts and shots). "New"
    // is drawn here only, with FxMockKit: nothing in the game changes until a person has chosen.
    static class FxMockPreview
    {
        const int Tile = TileArt.Size;
        const int Columns = 8;
        static readonly Rgba Backdrop = Rgba.Hex("#0A0A0F"), NowTag = Rgba.Hex("#9A9AA4"), NewTag = Rgba.Hex("#7CE08A");

        sealed class Item
        {
            public string Id, Group;
            public int W, H;
            public float Life;
            public Func<float, PixelImage> Now;
            // Drawn twice a frame: the ground pass under whoever stands there, then the pass over them.
            public Action<FxCanvas, float, bool> New;
            public Action<FxCanvas, float> Actors;
            public int Most;
        }

        public static bool Write(string folder, ClassData classes, MonsterData monsters, PixelImage floor, PixelImage hero)
        {
            Directory.CreateDirectory(folder);
            var items = new List<Item>();
            var skills = classes.Classes.SelectMany(c => c.Skills).ToDictionary(s => s.Id);
            items.Add(Skill("s1_fireball", skills["fireball"], floor, Fireball));
            items.Add(Skill("s2_frost_ring", skills["frost_ring"], floor, FrostRing));
            items.Add(Skill("s3_thunderclap", skills["thunderclap"], floor, Thunderclap));
            items.Add(Skill("s4_whirlwind", skills["whirlwind"], floor, Whirlwind));
            items.Add(Skill("s5_meteor", skills["meteor"], floor, Meteor));
            items.Add(Monster("m1_crystal_spray", monsters, "crystal_shooter", "spray", floor, hero, CrystalSpray));
            items.Add(Monster("m2_spore_sac", monsters, "spore_shooter", "spore_sac", floor, hero, SporeSac));
            items.Add(Monster("m3_trample", monsters, "frost_mammoth", "trample", floor, hero, Trample));
            items.Add(Monster("m4_fire_wall", monsters, "demon_caster", "fire_wall", floor, hero, FireWall));
            items.Add(Boss("b1_slam", floor, hero, 1.0f, 1.9f, SlamNow, SlamNew));
            items.Add(Boss("b2_fire_ring", floor, hero, 0.9f, 1.9f, DonutNow, DonutNew, middle: true));
            items.Add(Boss("b3_cleave", floor, hero, 0.8f, 1.6f, ConeNow, ConeNew));
            items.Add(Boss("b4_beam", floor, hero, 0.9f, 1.8f, BeamNow, BeamNew, queen: true));
            items.Add(Boss("b5_crystal_rain", floor, hero, 0.9f, 2.6f, RainNow, RainNew, queen: true));
            items.Add(Boss("b6_shot_fan", floor, hero, 0.5f, 1.7f, ShotsNow, ShotsNew));

            var report = new StringBuilder();
            report.AppendLine("mock              seconds  most pieces (new)");
            var overview = new List<PixelImage>();
            foreach (var group in new[] { "skills", "monsters", "bosses" })
            {
                var sheets = new List<PixelImage>();
                foreach (var item in items.Where(i => i.Group == group))
                {
                    var now = Enumerable.Range(0, Columns).Select(i => item.Now(item.Life * (i + 0.5f) / Columns)).ToList();
                    var next = Enumerable.Range(0, Columns).Select(i => Render(item, floor, item.Life * (i + 0.5f) / Columns)).ToList();
                    var sheet = Strip(item, now, next);
                    Png.Save(SkillFxPreview.Scaled(sheet, 3), Path.Combine(folder, item.Id + ".png"));
                    sheets.Add(sheet);
                    // The overview: four moments of each.
                    var picks = new[] { 1, 3, 4, 6 };
                    overview.Add(Strip(item, picks.Select(i => now[i]).ToList(), picks.Select(i => next[i]).ToList(), sideBySide: true));
                    var frames = new List<(Rgba[] Pixels, int Centiseconds)>();
                    int wide = item.W * 2 + 6, tall = item.H + 10;
                    for (float t = 0; t < item.Life; t += 0.04f)
                    {
                        var frame = new PixelImage(wide, tall);
                        frame.Fill(0, 0, wide, tall, Backdrop);
                        frame.Small(2, 2, "NOW", NowTag);
                        frame.Small(item.W + 8, 2, "NEW", NewTag);
                        frame.Draw(item.Now(t), 0, 10);
                        frame.Draw(Render(item, floor, t), item.W + 6, 10);
                        frames.Add((SkillFxPreview.Flat(SkillFxPreview.Scaled(frame, 2)), 4));
                    }
                    frames.Add((frames[frames.Count - 1].Pixels, 40));
                    Gif.Save(Path.Combine(folder, item.Id + ".gif"), wide * 2, tall * 2, frames);
                    report.AppendLine($"{item.Id,-17} {item.Life,7:0.00}  {item.Most,6}");
                }
                Png.Save(SkillFxPreview.Scaled(Stack(sheets), 2), Path.Combine(folder, group + ".png"));
            }
            Png.Save(SkillFxPreview.Scaled(Stack(overview), 2), Path.Combine(folder, "all.png"));
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Console.WriteLine($"Wrote fxmock/all.png, skills.png, monsters.png, bosses.png, <mock>.png, <mock>.gif and report.txt to {folder}");
            return true;
        }

        static PixelImage Render(Item item, PixelImage floor, float t)
        {
            var cv = new FxCanvas(item.W, item.H, floor);
            item.New(cv, t, true);
            item.Actors(cv, t);
            item.New(cv, t, false);
            item.Most = Math.Max(item.Most, cv.Pieces);
            return cv.Image();
        }

        // Now above new (or, for the overview, now to the left of new), named.
        static PixelImage Strip(Item item, List<PixelImage> now, List<PixelImage> next, bool sideBySide = false)
        {
            int n = now.Count, gap = 2, label = 9;
            int wide = sideBySide ? 2 * n * (item.W + gap) + 8 : n * (item.W + gap), tall = sideBySide ? item.H + label : 2 * (item.H + label);
            var sheet = new PixelImage(wide, tall);
            sheet.Fill(0, 0, wide, tall, Backdrop);
            sheet.Small(2, 2, item.Id.ToUpperInvariant().Replace('_', ' ') + "  NOW", NowTag);
            int newX = sideBySide ? n * (item.W + gap) + 8 : 0, newY = sideBySide ? 0 : item.H + label;
            sheet.Small(newX + 2, newY + 2, "NEW", NewTag);
            for (int i = 0; i < n; i++)
            {
                sheet.Draw(now[i], i * (item.W + gap), label);
                sheet.Draw(next[i], newX + i * (item.W + gap), newY + label);
            }
            return sheet;
        }

        static PixelImage Stack(List<PixelImage> sheets)
        {
            var all = new PixelImage(sheets.Max(s => s.Width), sheets.Sum(s => s.Height + 4));
            all.Fill(0, 0, all.Width, all.Height, Backdrop);
            int y = 0;
            foreach (var sheet in sheets)
            {
                all.Draw(sheet, 0, y);
                y += sheet.Height + 4;
            }
            return all;
        }

        // ---- Skills: now is SkillFx itself, on the scene SkillFxPreview lays for it.

        static Item Skill(string id, SkillDefinition skill, PixelImage floor, Action<FxCanvas, float, bool, SkillFxPreview.Scene> draw)
        {
            var scene = SkillFxPreview.Make(skill, Rgba.Hex("#6FCF5A"));
            return new Item
            {
                Id = id,
                Group = "skills",
                W = scene.Width,
                H = scene.Height,
                Life = Math.Max(scene.Duration, 1.1f) + 0.25f,
                Now = t => SkillFxPreview.Render(scene, floor, t),
                New = (cv, t, ground) => draw(cv, t, ground, scene),
                Actors = (cv, t) =>
                {
                    foreach (var (x, y) in scene.Slimes) cv.Sprite(scene.Slime, x - scene.Slime.Width / 2, y - scene.Slime.Height + 2);
                    cv.Sprite(scene.Hero, scene.HeroX - HeroArt.FeetX, scene.HeroY - HeroArt.FeetY);
                },
            };
        }

        static void Fireball(FxCanvas cv, float t, bool ground, SkillFxPreview.Scene s)
        {
            float land = s.Reach / (s.Skill.Speed * Tile), y = s.HeroY - 8, x0 = s.HeroX + World.PlayerRadius * Tile;
            float x = x0 + Math.Min(t, land) * s.Skill.Speed * Tile;
            if (t < land)
            {
                if (ground)
                {
                    Glow(cv, x, y + 6, 17, Fire[2], 0.45f);
                    return;
                }
                if (t < 0.1f)
                {
                    Star(cv, x0 + 2, y, 5, Fire[0], 1 - t / 0.1f);
                    Ring(cv, x0 + 2, y, 3 + 60 * t, 1, Fire[1], 1 - t / 0.1f);
                }
                Comet(cv, x, y, 1, 0, 5, t, 3);
                return;
            }
            Blast(cv, ground, x, y, s.Context.Radius, t - land, Fire, 3, 12, 0);
        }

        // A ball of fire in flight: a tail of lumps that shrink and darken, embers shed behind, and a white-hot head.
        static void Comet(FxCanvas cv, float x, float y, float dx, float dy, float size, float t, int seed, bool rock = false)
        {
            for (int i = 6; i >= 1; i--)
            {
                float back = i * size * 0.75f, wave = Sin(t * 30 + i * 1.3f) * size * 0.12f * i;
                var c = i > 4 ? (rock ? Smoke : Fire[4]) : i > 2 ? Fire[3] : Fire[2];
                Blob(cv, x - dx * back - dy * wave, y - dy * back + dx * wave, size * (1 - i * 0.13f), seed + i, t * 25, 0.2f, c, 1, i > 4 ? 0.6f : 1);
            }
            for (int i = 0; i < 6; i++)
            {
                float phase = (t * 3.2f + i / 6f) % 1;
                float back = size * 1.5f + phase * size * 4.5f, side = (Rnd(seed, i) - 0.5f) * size * 2.2f;
                cv.Px((int)(x - dx * back - dy * side), (int)(y - dy * back + dx * side - phase * 3), Fire[phase < 0.5f ? 1 : 2], 1 - phase);
            }
            cv.Pieces++;
            Blob(cv, x, y, size, seed, t * 30, 0.14f, rock ? Earth[4] : Fire[2]);
            Blob(cv, x + dx * size * 0.2f, y + dy * size * 0.2f, size * 0.7f, seed + 1, t * 26, 0.12f, rock ? Fire[3] : Fire[1]);
            Blob(cv, x + dx * size * 0.4f, y + dy * size * 0.4f, size * 0.42f, seed + 2, t * 22, 0.1f, rock ? Fire[1] : Fire[0]);
        }

        static void FrostRing(FxCanvas cv, float t, bool ground, SkillFxPreview.Scene s)
        {
            float cx = s.HeroX, cy = s.HeroY - 4, r = s.Context.Radius, age = t - 0.1f;
            if (age < 0)
            {
                if (ground) return;
                for (int i = 0; i < 8; i++)
                {
                    float at = i * Tau / 8, far = r * 0.5f * (1 - t / 0.1f) + 5;
                    Spark(cv, cx + Cos(at) * far, cy - Sin(at) * far, -Cos(at), Sin(at), 4, Ice[0], Ice[2]);
                }
                return;
            }
            float grow = Out3(age / 0.24f), left = C01((0.85f - age) / 0.3f);
            if (ground)
            {
                // Frost over the floor, with cracks running out through it.
                Disc(cv, cx, cy, r * grow, Ice[3], 0.22f * left);
                Disc(cv, cx, cy, r * grow * 0.7f, Ice[2], 0.12f * left);
                for (int i = 0; i < 9; i++)
                {
                    float at = (i + 0.3f * Rnd(4, i)) * Tau / 9, bend = at + (Rnd(5, i) - 0.5f) * 0.5f;
                    float midX = cx + Cos(at) * r * 0.5f * grow, midY = cy - Sin(at) * r * 0.5f * grow;
                    Line(cv, cx + Cos(at) * r * 0.2f, cy - Sin(at) * r * 0.2f, midX, midY, Ice[0], 0.55f * left);
                    Line(cv, midX, midY, midX + Cos(bend) * r * 0.4f * grow, midY - Sin(bend) * r * 0.4f * grow, Ice[1], 0.45f * left);
                }
                Glow(cv, cx, cy, r * 1.25f, Ice[3], 0.5f * C01(1 - age / 0.4f));
                return;
            }
            if (age < 0.3f) Ring(cv, cx, cy, r * grow, 2, Ice[0], 1 - age / 0.3f);
            // Crystals stand up round the edge as the wave reaches it, hold, then break and fall.
            for (int i = 0; i < 16; i++)
            {
                float at = i * Tau / 16 + 0.1f, born = 0.1f + 0.006f * (i * 7 % 16);
                float pop = Out3((age - born) / 0.07f), size = (i % 2 == 0 ? 13 : 8) * pop;
                float bx = cx + Cos(at) * (r - 2), by = cy - Sin(at) * (r - 2);
                // Each leans out from the middle and up a little, as a thing standing on the floor does seen from above.
                float lean = (float)Math.Atan2(Sin(at) + 0.45f, Cos(at));
                if (age < 0.5f) Shard(cv, bx + Cos(lean) * size * 0.25f, by - Sin(lean) * size * 0.25f, size, size * 0.42f, lean, Ice, 1, i % 2 == 0);
                else if (age < 0.85f)
                {
                    float p = (age - 0.5f) / 0.35f;
                    for (int k = 0; k < 2; k++)
                        Shard(cv, bx + Cos(at + k) * p * 7, by - Sin(at + k) * p * 4 + p * p * 9, 4 - 2 * p, 2, at + p * 6 + k * 2, Ice, 1 - p);
                }
            }
            for (int i = 0; i < 6; i++)
            {
                float p = (age - 0.08f - i * 0.03f) / 0.6f;
                if (p < 0 || p > 1) continue;
                float at = i * Tau / 6 + 0.5f;
                Blob(cv, cx + Cos(at) * r * (0.6f + 0.3f * p), cy - Sin(at) * r * (0.6f + 0.3f * p) - p * 4, 4 + 3 * p, i, p * 3, 0.2f, Ice[1], 1, 0.4f * (1 - p));
            }
            for (int i = 0; i < 8; i++)
            {
                float born = 0.1f + 0.07f * i;
                if (age < born || age > born + 0.2f) continue;
                float at = Rnd(6, i) * Tau, far = r * (0.2f + 0.7f * Rnd(7, i));
                Star(cv, cx + Cos(at) * far, cy - Sin(at) * far, age - born < 0.1f ? 2 : 1, Ice[0]);
            }
        }

        static void Thunderclap(FxCanvas cv, float t, bool ground, SkillFxPreview.Scene s)
        {
            float cx = s.HeroX, cy = s.HeroY - 4, r = s.Context.Radius;
            int flicker = (int)(t / 0.04f);
            if (ground)
            {
                // The room dims while the sky gathers, so the light of the strike reads.
                cv.Shade(1 - 0.28f * C01(t / 0.08f) * C01((0.5f - t) / 0.2f));
            }
            for (int i = 0; i < 6; i++)
            {
                float at = i * Tau / 5 + 0.4f, far = i == 5 ? 0 : r * 0.72f;
                float x = cx + Cos(at) * far, y = cy - Sin(at) * far * 0.9f, born = 0.1f + i * 0.035f, age = t - born;
                if (ground)
                {
                    if (age < 0) Ring(cv, x, y, 3 + (flicker + i) % 2, 1, Bolt_[3], 0.8f);
                    else
                    {
                        Scorch(cv, x, y, 6, i, 0.45f * C01((0.9f - age) / 0.4f));
                        Glow(cv, x, y, 26, Bolt_[1], 0.8f * C01(1 - age / 0.3f));
                    }
                    continue;
                }
                if (age < 0) continue;
                if (age < 0.1f)
                {
                    Bolt(cv, x - 10 + 20 * Rnd(i, 3), -2, x, y, i * 13 + flicker, Bolt_, 1, 2);
                    Star(cv, x, y, 6, Rgba.White);
                    Disc(cv, x, y, 3, Rgba.White);
                }
                else if (age < 0.2f) Bolt(cv, x - 10 + 20 * Rnd(i, 3), -2, x, y, i * 13 + 2, Bolt_, 0.5f, 0, false);
                for (int k = 0; k < 4; k++)
                {
                    float life = 0.3f, p = age / life;
                    if (p > 1) continue;
                    float dir = Rnd(i * 9 + k, 1) * Tau, far2 = 12 * Out3(p);
                    Spark(cv, x + Cos(dir) * far2, y - Sin(dir) * far2 * 0.7f - 4 * 4 * p * (1 - p), Cos(dir), -Sin(dir), 3, Bolt_[0], Bolt_[2], 1 - p);
                }
            }
            if (ground) return;
            if (t >= 0.1f && t < 0.14f) cv.Lighten(Rgba.White, 0.14f);
            // Arcs run round the edge of what it hits.
            if (t >= 0.1f && t < 0.42f)
            {
                float fade = C01((0.42f - t) / 0.15f);
                for (int i = 0; i < 10; i++)
                {
                    if ((i + flicker) % 3 == 0) continue;
                    float a0 = i * Tau / 10, a1 = (i + 1) * Tau / 10;
                    Bolt(cv, cx + Cos(a0) * r, cy - Sin(a0) * r, cx + Cos(a1) * r, cy - Sin(a1) * r, i * 5 + flicker, Bolt_, fade, 0, false);
                }
                Ring(cv, cx, cy, r, 1, Bolt_[2], 0.5f * fade);
            }
        }

        static void Whirlwind(FxCanvas cv, float t, bool ground, SkillFxPreview.Scene s)
        {
            float cx = s.HeroX, cy = s.HeroY - 6, r = s.Context.Radius, life = 0.62f;
            float turn = Tau * 1.25f * Out3(t / life), pace = C01(1 - t / life);
            if (ground)
            {
                // Dust kicked up round the edge where the blade has passed.
                for (int i = 0; i < 12; i++)
                {
                    float at = i * Tau / 12, born = life * (1 - (float)Math.Pow(1 - Math.Min(1, at / (Tau * 1.25f)), 1 / 3f));
                    float p = (t - born) / 0.45f;
                    if (p < 0 || p > 1) continue;
                    Blob(cv, cx + Cos(at) * (r + 2 + 5 * p), cy - Sin(at) * (r + 2 + 5 * p) - 3 * p, 3 + 3 * p, i, p * 3, 0.25f, Dust, 1, 0.55f * (1 - p));
                }
                if (t < life) Ring(cv, cx, cy, r, 1, Steel[2], 0.35f);
                return;
            }
            if (t < life)
            {
                float sweep = 0.6f + 2.6f * pace;
                Crescent(cv, cx, cy, r, 9, turn, sweep, Steel);
                Crescent(cv, cx, cy, r, 9, turn + (float)Math.PI, sweep, Steel);
                Crescent(cv, cx, cy, r * 0.62f, 3, turn + 0.8f, sweep * 0.7f, Steel, 0.6f);
                Crescent(cv, cx, cy, r * 0.62f, 3, turn + 0.8f + (float)Math.PI, sweep * 0.7f, Steel, 0.6f);
            }
            // Where a blade meets the slime: a star, and sparks thrown along the cut.
            foreach (float hit in new[] { 0.045f, 0.2f })
            {
                float age = t - hit;
                if (age < 0 || age > 0.25f || s.Slimes.Count == 0) continue;
                float x = s.Slimes[0].X - 2, y = s.Slimes[0].Y - 8;
                if (age < 0.08f) Star(cv, x, y, 6, Rgba.White);
                for (int k = 0; k < 6; k++)
                {
                    float dir = 1.2f + (Rnd(k, (int)(hit * 100)) - 0.5f) * 1.6f, far = 14 * Out3(age / 0.25f);
                    Spark(cv, x + Cos(dir) * far, y - Sin(dir) * far, Cos(dir), -Sin(dir), 4, Rgba.White, Steel[2], 1 - age / 0.25f);
                }
            }
            for (int i = 0; i < 5; i++)
            {
                float age = t - life + 0.1f - i * 0.03f;
                if (age < 0 || age > 0.2f) continue;
                float at = i * Tau / 5 + 0.7f;
                Star(cv, cx + Cos(at) * r, cy - Sin(at) * r, age < 0.1f ? 3 : 1, Steel[0]);
            }
        }

        static void Meteor(FxCanvas cv, float t, bool ground, SkillFxPreview.Scene s)
        {
            float land = s.Reach / (s.Skill.Speed * Tile), r = s.Context.Radius;
            float tx = s.HeroX + World.PlayerRadius * Tile + land * s.Skill.Speed * Tile, ty = s.HeroY - 8;
            if (t < land)
            {
                float p = t / land, fall = (float)Math.Pow(1 - p, 1.3);
                // It comes down out of the dark above, onto the spot it was aimed at.
                float x = tx - 46 * fall, y = ty - 78 * fall;
                if (ground)
                {
                    Ring(cv, tx, ty, r, 1, Fire[2], 0.35f + 0.25f * p);
                    for (int i = 0; i < 12; i++) Ring(cv, tx, ty, r - 2, 2, Fire[1], 0.5f, i * Tau / 12 + t * 2, 0.2f);
                    Disc(cv, tx, ty + 2, 5 + r * 0.45f * p, Rgba.Hex("#000000"), 0.3f + 0.25f * p, 1, 0.6f);
                    Glow(cv, tx, ty, r * (0.6f + p), Fire[2], 0.5f * p);
                    return;
                }
                float len = (float)Math.Sqrt(46 * 46 + 78 * 78);
                Comet(cv, x, y, 46 / len, 78 / len, 8, t, 5, true);
                return;
            }
            float age = t - land;
            if (ground)
            {
                Blast(cv, true, tx, ty, r, age, Fire, 5, linger: 1.6f);
                // The floor splits, glowing, and cools.
                for (int i = 0; i < 9; i++)
                {
                    float at = (i + 0.4f * Rnd(2, i)) * Tau / 9, reach = r * (0.55f + 0.4f * Rnd(3, i)) * Out3(age / 0.12f);
                    float midX = tx + Cos(at) * reach * 0.5f, midY = ty - Sin(at) * reach * 0.45f, bend = at + (Rnd(4, i) - 0.5f) * 0.7f;
                    var c = age < 0.3f ? Fire[1] : age < 0.7f ? Fire[3] : Smoke;
                    Line(cv, tx, ty, midX, midY, c, 0.9f, 2);
                    Line(cv, midX, midY, midX + Cos(bend) * reach * 0.5f, midY - Sin(bend) * reach * 0.45f, c, 0.9f);
                }
                return;
            }
            // Four smaller balls of fire round the first.
            for (int i = 0; i < 4; i++)
            {
                float at = i * Tau / 4 + 0.6f;
                Blast(cv, false, tx + Cos(at) * r * 0.5f, ty - Sin(at) * r * 0.4f, r * 0.5f, age - 0.03f - i * 0.02f, Fire, 20 + i, 0, 0, false);
            }
            Blast(cv, false, tx, ty, r, age, Fire, 5, 16, 6);
            if (age < 0.05f) cv.Lighten(Fire[0], 0.18f);
            for (int i = 0; i < 4; i++)
            {
                float left = C01((1.25f - age - i * 0.08f) / 0.5f);
                if (age < 0.3f || left <= 0) continue;
                float at = i * Tau / 4 + 1.9f;
                Flame(cv, tx + Cos(at) * r * 0.45f, ty - Sin(at) * r * 0.35f + 3, 9 * left + 2, 6, Fire, t, i);
            }
        }

        // ---- Ordinary monsters: now is MonsterFx itself, on MonsterFxPreview's scene.

        static Item Monster(string id, MonsterData monsters, string monsterId, string attackId, PixelImage floor, PixelImage hero,
            Action<FxCanvas, float, bool, MonsterFxPreview.Scene, MonsterAttack> draw)
        {
            var monster = monsters.Find(monsterId);
            var attack = monster.Attacks.First(a => a.Id == attackId);
            var scene = new MonsterFxPreview.Scene(monster, attack, floor, hero, monsters);
            var color = Rgba.Hex(monster.Color.Length > 0 ? monster.Color : "#C8C8C8");
            return new Item
            {
                Id = id,
                Group = "monsters",
                W = MonsterFxPreview.Wide,
                H = MonsterFxPreview.Tall,
                Life = Math.Min(scene.Whole, 2.6f),
                Now = scene.Render,
                New = (cv, t, ground) => draw(cv, t, ground, scene, attack),
                Actors = (cv, t) =>
                {
                    var (px, py) = scene.Player;
                    cv.Sprite(hero, (int)px - HeroArt.FeetX, (int)py + 5 - HeroArt.FeetY);
                    var (bx, by, lift) = scene.Body(t);
                    if (lift < 0) return;
                    var sprite = CreatureArt.Monster(monster.Id, color, 0, MonsterView.Side, t < attack.Telegraph ? MonsterPose.Windup : t < attack.Telegraph + 0.15f ? MonsterPose.Strike : MonsterPose.Idle);
                    cv.Sprite(sprite, (int)Math.Round(bx - sprite.Width / 2f), (int)Math.Round(by + 5 - lift - sprite.Height + 2));
                },
            };
        }

        static void CrystalSpray(FxCanvas cv, float t, bool ground, MonsterFxPreview.Scene s, MonsterAttack a)
        {
            var (mx, my) = s.Monster;
            float age = t - a.Telegraph, reach = 5 * Tile, flight = reach / (a.Speed * Tile);
            for (int i = 0; i < a.Count; i++)
            {
                float dir = -(i - (a.Count - 1) / 2f) * a.Angle / Math.Max(1, a.Count - 1) * (float)Math.PI / 180;
                float dx = Cos(dir), dy = -Sin(dir);
                if (age < 0)
                {
                    if (ground)
                    {
                        // Where each will fly: a thin line of the warning's colour, drawn out as it aims.
                        float p = t / a.Telegraph, far = reach * Out3(p * 1.5f);
                        for (float d = 10; d < far; d += 5)
                            if ((int)(d / 5 + t * 12) % 2 == 0) Line(cv, mx + dx * d, my + dy * d, mx + dx * (d + 3), my + dy * (d + 3), Danger[2], 0.35f + 0.45f * p);
                        cv.Pieces -= (int)(far / 10);
                    }
                    else if (i == 0)
                    {
                        float p = t / a.Telegraph;
                        Glow(cv, mx + 6, my - 2, 6 + 8 * p, Crystal[2], 0.5f * p);
                        Star(cv, mx + 7, my - 2, 1 + (int)(4 * p), Crystal[0], p > 0.85f ? 1 : 0.7f);
                    }
                    continue;
                }
                float gone = Math.Min(age, flight) * a.Speed * Tile + 8, x = mx + dx * gone, y = my + dy * gone;
                if (age < flight)
                {
                    if (ground)
                    {
                        Glow(cv, x, y + 4, 10, Crystal[2], 0.45f);
                        continue;
                    }
                    for (int k = 3; k >= 1; k--) Shard(cv, x - dx * k * 5, y - dy * k * 5, 9, 4, dir, Crystal, 0.5f - k * 0.13f);
                    // A dark rim and a red one inside it: an enemy's shot, whatever its element.
                    Shard(cv, x, y, 13, 7.5f, dir, Danger);
                    Shard(cv, x, y, 11, 5, dir, Crystal, 1, true);
                    if ((int)(t * 25) % 2 == 0) Star(cv, x + dx * 5, y + dy * 5, 1, Rgba.White);
                    cv.Px((int)(x - dx * 9 - dy * 2), (int)(y - dy * 9 + dx * 2), Crystal[1]);
                    cv.Px((int)(x - dx * 13 + dy * 2), (int)(y - dy * 13 - dx * 2), Crystal[2]);
                }
                else if (!ground && age - flight < 0.25f)
                {
                    float p = (age - flight) / 0.25f;
                    if (p < 0.3f) Star(cv, x, y, 4, Rgba.White);
                    Ring(cv, x, y, 3 + 7 * Out3(p), 1, Crystal[1], 1 - p);
                    for (int k = 0; k < 5; k++)
                    {
                        float at = k * Tau / 5 + i;
                        Shard(cv, x + Cos(at) * 8 * Out3(p), y - Sin(at) * 8 * Out3(p) + 5 * p * p, 4 - 2 * p, 2, at + p * 5, Crystal, 1 - p);
                    }
                }
            }
        }

        static void SporeSac(FxCanvas cv, float t, bool ground, MonsterFxPreview.Scene s, MonsterAttack a)
        {
            var (mx, my) = s.Monster;
            var (px, py) = s.Player;
            float r = (a.Radius + World.PlayerRadius) * Tile, age = t - a.Telegraph, pool = a.PoolRadius > 0 ? a.PoolRadius * Tile : r;
            if (age < 0)
            {
                float p = t / a.Telegraph, x = mx + (px - mx) * p, y = my + (py - my) * p - 4 * 34 * p * (1 - p);
                if (ground)
                {
                    Warn(cv, CircleShape(px, py, r), px - r, py - r, px + r, py + r, p, t, Danger);
                    // The sac's shadow slides to where it will fall.
                    Disc(cv, mx + (px - mx) * p, my + (py - my) * p + 3, 2 + 2 * p, Rgba.Hex("#000000"), 0.35f, 1, 0.6f);
                    return;
                }
                for (int k = 1; k <= 3; k++)
                {
                    float q = Math.Max(0, p - k * 0.035f);
                    Disc(cv, mx + (px - mx) * q, my + (py - my) * q - 4 * 34 * q * (1 - q), 3 - k * 0.6f, Spore[3], 0.5f - k * 0.12f);
                }
                Blob(cv, x, y, 4.5f, 2, t * 14, 0.18f, Spore[4]);
                Blob(cv, x, y, 3.5f, 2, t * 14, 0.18f, Spore[2]);
                Disc(cv, x - 1, y - 1, 1.5f, Spore[0]);
                return;
            }
            float left = C01((2.4f - age) / 0.4f);
            if (ground)
            {
                // What it leaves: a pool with a dark rim, lighter within, a red dotted edge where it still hurts.
                float spread = Out3(age / 0.18f);
                Blob(cv, px, py, pool * spread, 4, age * 1.5f, 0.1f, Spore[4], 0.75f * left);
                Blob(cv, px, py, pool * spread - 2, 4, age * 1.5f, 0.1f, Spore[3], 0.75f * left);
                Blob(cv, px - 2, py - 1, pool * spread * 0.62f, 6, -age * 2, 0.16f, Spore[2], 0.6f * left);
                for (int i = 0; i < 20; i++)
                    if ((i + (int)(age * 4)) % 2 == 0) Ring(cv, px, py, pool, 1, Danger[2], 0.7f * left, i * Tau / 20, Tau / 20);
                cv.Pieces -= 9;
                Glow(cv, px, py, pool * 1.3f, Spore[2], 0.25f * left);
                return;
            }
            // The splash: lumps thrown out, a ring, then a cloud of spores rising.
            if (age < 0.3f)
            {
                float p = age / 0.3f;
                Ring(cv, px, py, r * (0.4f + 0.7f * Out3(p)), p < 0.4f ? 2 : 1, Spore[0], 1 - p);
                for (int i = 0; i < 9; i++)
                {
                    float at = i * Tau / 9, far = r * (0.3f + 0.9f * Out3(p));
                    Blob(cv, px + Cos(at) * far, py - Sin(at) * far * 0.8f - 4 * 7 * p * (1 - p), 2.5f * (1 - p) + 1, i, p * 9, 0.2f, Spore[1]);
                }
                if (age < 0.06f) Disc(cv, px, py, r * 0.45f, Spore[0], 0.9f);
            }
            for (int i = 0; i < 5; i++)
            {
                float p = (age - 0.1f - i * 0.06f) / 0.7f;
                if (p < 0 || p > 1) continue;
                Blob(cv, px + (Rnd(8, i) - 0.5f) * r * 1.4f, py - 2 - p * 14, 4 + 3 * p, i, p * 3, 0.22f, Spore[1], 1, 0.5f * (1 - p));
            }
            // Bubbles swell and pop; motes drift up.
            for (int i = 0; i < 6; i++)
            {
                float p = (age * 1.1f + Rnd(9, i)) % 1, at = Rnd(10, i) * Tau, far = pool * 0.7f * Rnd(11, i);
                float x = px + Cos(at) * far, y = py - Sin(at) * far * 0.8f;
                if (left <= 0) continue;
                if (p < 0.75f) Ring(cv, x, y, 1 + 2.2f * p / 0.75f, 1, Spore[1], left);
                else Ring(cv, x, y, 4.5f, 1, Spore[0], left * (1 - p) * 4, i, Tau * 0.6f);
                cv.Px((int)(x + 6 * Rnd(12, i) - 3), (int)(y - 6 - 12 * p), Spore[0], left * (1 - p));
            }
        }

        static void Trample(FxCanvas cv, float t, bool ground, MonsterFxPreview.Scene s, MonsterAttack a)
        {
            var (mx, my) = s.Monster;
            var (_, length, wide) = s.Geometry();
            float age = t - a.Telegraph;
            var (bx, by, _) = s.Body(t);
            if (age < 0)
            {
                if (!ground) return;
                float p = t / a.Telegraph;
                Warn(cv, BandShape(mx, my, length, wide), mx, my - wide / 2, Math.Min(cv.W - 1, mx + length), my + wide / 2, p, t, Rush);
                // Arrowheads march the way it will run.
                for (int i = 0; i < 6; i++)
                {
                    float x = mx + 16 + (i * 22 + t * 40) % (cv.W - mx - 20);
                    for (int k = 0; k < 6; k++)
                    {
                        cv.Px((int)(x - k), (int)(my - k), Rush[0], 0.8f);
                        cv.Px((int)(x - k), (int)(my + k), Rush[0], 0.8f);
                        cv.Px((int)(x - k - 1), (int)(my - k), Rush[3], 0.8f);
                        cv.Px((int)(x - k - 1), (int)(my + k), Rush[3], 0.8f);
                    }
                }
                cv.Pieces += 6;
                return;
            }
            if (ground)
            {
                // Frost left where it ran, and the floor cracked under it.
                float from = mx, to = Math.Max(mx, bx - 10);
                for (float x = from; x < to; x += 9)
                {
                    float seen = C01((2.4f - age) / 0.6f);
                    Disc(cv, x, my + 6 + 4 * (Rnd((int)x, 1) - 0.5f), 7, Ice[2], 0.2f * seen, 1, 0.5f);
                    if ((int)(x / 9) % 2 == 0) Line(cv, x, my + 2, x + 6, my + 9 + 5 * (Rnd((int)x, 2) - 0.5f), Ice[0], 0.5f * seen);
                }
                cv.Pieces -= (int)((to - from) / 14);
                return;
            }
            bool running = bx > mx + 2 && bx < cv.W + 30 && age < length / (a.Speed * Tile);
            if (!running) return;
            // Speed lines off its back, dust and splinters of ice thrown up behind its feet.
            for (int i = 0; i < 5; i++)
            {
                float y = by - 22 + i * 7, lag = 26 + 14 * Rnd(i, (int)(t * 25));
                Line(cv, bx - 22, y, bx - 22 - lag, y, Ice[1], 0.55f);
                Line(cv, bx - 22 - lag, y, bx - 30 - lag * 1.5f, y, Ice[2], 0.3f);
            }
            for (int i = 0; i < 7; i++)
            {
                float p = (t * 3 + i / 7f) % 1, x = bx - 14 - p * 34, y = by + 6 - 4 * 5 * p * (1 - p) + (Rnd(i, 4) - 0.5f) * 8;
                Blob(cv, x, y, 3 + 4 * p, i, p * 4, 0.25f, Dust, 1, 0.7f * (1 - p));
                if (i % 2 == 0) Shard(cv, x + 4, y - 6 - 4 * 8 * p * (1 - p), 5 - 2 * p, 2.5f, 1 + p * 7 + i, Ice, 1 - p);
            }
            Ring(cv, bx + 20, by - 4, 16, 2, Rgba.White, 0.5f, -0.7f, 1.4f);
        }

        static void FireWall(FxCanvas cv, float t, bool ground, MonsterFxPreview.Scene s, MonsterAttack a)
        {
            var (mx, my) = s.Monster;
            var (_, length, wide) = s.Geometry();
            float age = t - a.Telegraph, x0 = mx;
            if (age < 0)
            {
                float p = t / a.Telegraph;
                if (ground) Warn(cv, BandShape(x0, my, length, wide), x0, my - wide / 2, Math.Min(cv.W - 1, x0 + length), my + wide / 2, p, t, Danger);
                else
                {
                    // Embers gather to its hand.
                    for (int i = 0; i < 6; i++)
                    {
                        float at = i * Tau / 6 + t * 5, far = 12 * (1 - (p * 3 + i / 6f) % 1);
                        cv.Px((int)(mx + 6 + Cos(at) * far), (int)(my - 6 - Sin(at) * far), Fire[1]);
                    }
                    cv.Pieces++;
                    Glow(cv, mx + 6, my - 6, 5 + 7 * p, Fire[2], 0.5f * p);
                }
                return;
            }
            float left = C01((1.5f - age) / 0.4f);
            int n = (int)(Math.Min(length, cv.W - x0 - 4) / 9);
            if (ground)
            {
                for (int i = 0; i < n; i++)
                {
                    float x = x0 + 5 + i * 9, born = i * 0.018f;
                    if (age < born) continue;
                    Scorch(cv, x, my + 1, 8, i, 0.35f);
                    Glow(cv, x, my, 17, Fire[2], (age - born < 0.15f ? 0.55f : 0.22f + 0.05f * Sin(t * 20 + i)) * left);
                }
                cv.Pieces -= n;
                // Where it still burns: a red dotted edge.
                for (float x = x0; x < x0 + n * 9; x += 6)
                    if ((int)(x / 6 + age * 5) % 2 == 0)
                    {
                        Line(cv, x, my - wide / 2, x + 3, my - wide / 2, Danger[2], 0.7f * left);
                        Line(cv, x, my + wide / 2, x + 3, my + wide / 2, Danger[2], 0.7f * left);
                    }
                cv.Pieces -= n;
                return;
            }
            // The wall runs out from the caster, each tongue leaping up as the front passes, then burning lower.
            for (int i = 0; i < n; i++)
            {
                float x = x0 + 5 + i * 9, own = age - i * 0.018f;
                if (own < 0) continue;
                float leap = own < 0.16f ? 1 + 0.9f * Sin(own / 0.16f * (float)Math.PI) : 1;
                float tall = (11 + 4 * Rnd(i, 5)) * leap * (0.35f + 0.65f * left);
                Flame(cv, x + 4, my + 5, tall * 0.7f, 7, Fire, t + 0.3f, i + 20);
                Flame(cv, x, my + 6, tall, 9, Fire, t, i);
                if (own < 0.07f) Disc(cv, x, my + 2, 5, Fire[0], 0.9f);
                float p = (t * 1.6f + Rnd(i, 6)) % 1;
                cv.Px((int)(x + 6 * Rnd(i, 7) - 3 + Sin(p * 6) * 2), (int)(my - 4 - p * 22), Fire[p < 0.5f ? 1 : 2], left * (1 - p));
            }
        }

        // ---- Bosses: now is a copy of what WorldView does (a smooth red shape that fills, then EffectArt's blast in
        // one colour; a spiked ball in one orange for every boss's shot).

        const int BossW = 13 * Tile, BossH = 9 * Tile;
        static readonly Rgba DangerRed = new Rgba(255, 64, 51), BlastOrange = new Rgba(255, 115, 64), ShotOrange = new Rgba(255, 153, 64);

        static Item Boss(string id, PixelImage floor, PixelImage hero, float telegraph, float life, Action<PixelImage, float, float, Place> now,
            Action<FxCanvas, float, bool, float, Place> next, bool middle = false, bool queen = false)
        {
            var body = queen ? CrystalQueenArt.Draw(BossView.Front, CrystalQueenPose.Idle, 0, 1) : GeneralArt.Draw(BossView.Front, GeneralPose.Idle, 0, 1);
            int feet = queen ? CrystalQueenArt.Height - CrystalQueenArt.Floor : GeneralArt.Height - GeneralArt.Floor;
            var place = new Place { BossX = middle ? BossW / 2 : 44, BossY = middle ? BossH / 2 + 6 : BossH / 2 + 10, HeroX = middle ? BossW / 2 + 62 : 150, HeroY = BossH / 2 + 14 };
            void Stand(Action<PixelImage, int, int> put)
            {
                put(body, place.BossX - body.Width / 2, place.BossY - feet);
                put(hero, place.HeroX - HeroArt.FeetX, place.HeroY - HeroArt.FeetY);
            }
            return new Item
            {
                Id = id,
                Group = "bosses",
                W = BossW,
                H = BossH,
                Life = life,
                Now = t =>
                {
                    var image = new PixelImage(BossW, BossH);
                    for (int y = 0; y < BossH; y += floor.Height)
                        for (int x = 0; x < BossW; x += floor.Width)
                            image.Draw(floor, x, y);
                    now(image, t, telegraph, place);
                    Stand((sprite, x, y) => image.Draw(sprite, x, y));
                    // (The game's blasts and shots are drawn over the bodies.)
                    now(image, -t - 1, telegraph, place);
                    return image;
                },
                New = (cv, t, ground) => next(cv, t, ground, telegraph, place),
                Actors = (cv, t) => Stand((sprite, x, y) => cv.Sprite(sprite, x, y)),
            };
        }

        sealed class Place
        {
            public int BossX, BossY, HeroX, HeroY;
        }

        static void Tint(PixelImage image, int x, int y, Rgba c, float a)
        {
            if (!image.Inside(x, y) || a <= 0) return;
            var under = image[x, y];
            image[x, y] = new Rgba((int)(under.R + (c.R - under.R) * a), (int)(under.G + (c.G - under.G) * a), (int)(under.B + (c.B - under.B) * a));
        }

        // The game's mark: the whole shape faintly (0.22) and, over it, the same shape growing as the blow nears
        // (0.25 to 0.55), both in the one red.
        static void MarkNow(PixelImage image, Func<float, float, (float Inside, float Along)> shape, float progress, bool grows = true)
        {
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var (inside, along) = shape(x + 0.5f, y + 0.5f);
                    if (inside >= 0) continue;
                    Tint(image, x, y, DangerRed, 0.22f);
                    if (!grows) Tint(image, x, y, DangerRed, 0.1f + 0.4f * progress);
                    else if (along <= progress) Tint(image, x, y, DangerRed, 0.25f + 0.3f * progress);
                }
            }
        }

        // The game's blast: EffectArt's six grey frames in 0.35 seconds, in the one colour.
        static void FlashNow(PixelImage image, float cx, float cy, float radius, float age, bool ring = false)
        {
            if (age < 0 || age >= 0.35f) return;
            int diameter = EffectArt.Diameter(radius * 2 / Tile), frame = Math.Min(EffectArt.Frames - 1, (int)(age / 0.35f * EffectArt.Frames));
            var piece = ring ? EffectArt.Ring(diameter, frame) : EffectArt.Burst(diameter, frame);
            float fade = age > 0.245f ? (0.35f - age) / 0.105f : 1;
            for (int y = 0; y < piece.Height; y++)
            {
                for (int x = 0; x < piece.Width; x++)
                {
                    if (!piece.IsSet(x, y)) continue;
                    var p = piece[x, y];
                    Tint(image, (int)(cx - piece.Width / 2f) + x, (int)(cy - piece.Height / 2f) + y, new Rgba(p.R * BlastOrange.R / 255, p.G * BlastOrange.G / 255, p.B * BlastOrange.B / 255), 0.9f * fade);
                }
            }
        }

        // Every "now" below is called twice: once under the bodies with the time, once over them with -(time) - 1.
        static bool Over(ref float t)
        {
            if (t >= 0) return false;
            t = -t - 1;
            return true;
        }

        const float SlamR = 2.5f * Tile;

        static void SlamNow(PixelImage image, float t, float telegraph, Place at)
        {
            bool over = Over(ref t);
            float x = at.HeroX - 6, y = at.HeroY - 6;
            if (!over && t < telegraph) MarkNow(image, CircleShape(x, y, SlamR), t / telegraph);
            if (over) FlashNow(image, x, y, SlamR, t - telegraph);
        }

        static void SlamNew(FxCanvas cv, float t, bool ground, float telegraph, Place at)
        {
            float x = at.HeroX - 6, y = at.HeroY - 6, age = t - telegraph;
            if (age < 0)
            {
                if (ground) Warn(cv, CircleShape(x, y, SlamR), x - SlamR, y - SlamR, x + SlamR, y + SlamR, t / telegraph, t, Danger);
                return;
            }
            if (ground)
            {
                float left = C01((0.9f - age) / 0.3f);
                Scorch(cv, x, y, SlamR * 0.85f, 3, 0.4f * left);
                // The floor breaks: cracks out from the middle, wider near it.
                for (int i = 0; i < 11; i++)
                {
                    float dir = (i + 0.4f * Rnd(1, i)) * Tau / 11, reach = SlamR * (0.6f + 0.35f * Rnd(2, i)) * Out3(age / 0.1f), bend = dir + (Rnd(3, i) - 0.5f) * 0.8f;
                    float midX = x + Cos(dir) * reach * 0.55f, midY = y - Sin(dir) * reach * 0.5f;
                    Line(cv, x, y, midX, midY, Earth[4], 0.9f * left, 2);
                    Line(cv, midX, midY, midX + Cos(bend) * reach * 0.45f, midY - Sin(bend) * reach * 0.4f, Earth[4], 0.9f * left);
                }
                Glow(cv, x, y, SlamR * 1.5f, Danger[1], 0.5f * C01(1 - age / 0.25f));
                return;
            }
            if (age < 0.05f)
            {
                Disc(cv, x, y, SlamR * 0.5f, Rgba.White, 0.85f);
                Star(cv, x, y, (int)SlamR, Rgba.White, 0.8f);
            }
            // The blow is the warning's own colour for a moment, so it reads as the same thing landing.
            if (age < 0.35f)
            {
                Ring(cv, x, y, SlamR * (0.3f + 0.95f * Out3(age / 0.28f)), age < 0.12f ? 3 : 2, Danger[0], 1 - age / 0.35f);
                Ring(cv, x, y, SlamR * (0.2f + 0.8f * Out3(age / 0.34f)), 1, Danger[1], 0.8f * (1 - age / 0.35f));
            }
            // Dust rolls out along the ground and rocks are thrown up.
            for (int i = 0; i < 14; i++)
            {
                float p = age / (0.5f + 0.2f * Rnd(4, i));
                if (p > 1) continue;
                float dir = i * Tau / 14, far = SlamR * (0.35f + 0.8f * Out3(p));
                Blob(cv, x + Cos(dir) * far, y - Sin(dir) * far * 0.85f - 5 * p, 4 + 4 * p, i, p * 4, 0.25f, p < 0.3f ? Earth[1] : Dust, 1, 0.8f * (1 - p));
            }
            Blast(cv, false, x, y, SlamR, age, Earth, 8, 0, 9, false);
        }

        const float DonutIn = 1.7f * Tile, DonutOut = 3.6f * Tile;

        static void DonutNow(PixelImage image, float t, float telegraph, Place at)
        {
            bool over = Over(ref t);
            float x = at.BossX, y = at.BossY - 4;
            if (!over && t < telegraph) MarkNow(image, DonutShape(x, y, DonutIn, DonutOut), t / telegraph, false);
            if (!over) return;
            FlashNow(image, x, y, DonutOut, t - telegraph, true);
            for (int i = 0; i < 8; i++)
                FlashNow(image, x + Cos(i * Tau / 8) * (DonutIn + DonutOut) / 2, y - Sin(i * Tau / 8) * (DonutIn + DonutOut) / 2, Math.Max(8, (DonutOut - DonutIn) / 2), t - telegraph);
        }

        static void DonutNew(FxCanvas cv, float t, bool ground, float telegraph, Place at)
        {
            float x = at.BossX, y = at.BossY - 4, age = t - telegraph, mid = (DonutIn + DonutOut) / 2;
            if (age < 0)
            {
                if (ground) Warn(cv, DonutShape(x, y, DonutIn, DonutOut), x - DonutOut, y - DonutOut, x + DonutOut, y + DonutOut, t / telegraph, t, Danger);
                return;
            }
            float left = C01((0.85f - age) / 0.35f);
            for (int ringNo = 0; ringNo < 2; ringNo++)
            {
                int n = ringNo == 0 ? 14 : 20;
                float far = ringNo == 0 ? DonutIn + 7 : DonutOut - 7;
                for (int i = 0; i < n; i++)
                {
                    // The fire runs round the band from where the banner was planted.
                    float dir = i * Tau / n + ringNo * 0.15f, own = age - ringNo * 0.05f - 0.1f * Math.Abs(i - n / 2) / n;
                    float fx = x + Cos(dir) * far, fy = y - Sin(dir) * far;
                    if (own < 0) continue;
                    if (ground)
                    {
                        if (i % 2 == 0) Scorch(cv, fx, fy, 9, i, 0.32f * C01((1.0f - age) / 0.2f));
                        if (i % 3 == 0) Glow(cv, fx, fy, 22, Fire[2], (own < 0.15f ? 0.5f : 0.2f) * left);
                        continue;
                    }
                    float leap = own < 0.18f ? 1 + Sin(own / 0.18f * (float)Math.PI) : 1;
                    Flame(cv, fx, fy + 5, (10 + 5 * Rnd(i, ringNo)) * leap * left, 8, Fire, t, i + ringNo * 30);
                    if (own < 0.06f) Disc(cv, fx, fy, 5, Fire[0], 0.9f);
                }
            }
            if (ground) return;
            if (age < 0.3f)
            {
                Ring(cv, x, y, DonutIn + (DonutOut - DonutIn) * Out3(age / 0.25f), 2, Fire[0], 1 - age / 0.3f);
                Ring(cv, x, y, DonutIn, 1, Fire[1], 1 - age / 0.3f);
                Ring(cv, x, y, DonutOut, 1, Fire[1], 1 - age / 0.3f);
            }
            for (int i = 0; i < 16; i++)
            {
                float p = (age * 1.3f + Rnd(i, 8)) % 1, dir = i * Tau / 16;
                cv.Px((int)(x + Cos(dir) * mid + Sin(p * 7 + i) * 3), (int)(y - Sin(dir) * mid - p * 20), Fire[p < 0.5f ? 1 : 2], left * (1 - p));
            }
            cv.Pieces++;
        }

        const float ConeR = 4.2f * Tile, ConeWide = 80 * (float)Math.PI / 180;

        static void ConeNow(PixelImage image, float t, float telegraph, Place at)
        {
            bool over = Over(ref t);
            float x = at.BossX + 10, y = at.BossY - 8;
            if (!over && t < telegraph) MarkNow(image, ConeShape(x, y, ConeR, 0, ConeWide), t / telegraph);
            if (over) FlashNow(image, x + ConeR * 0.55f, y, ConeR * 0.5f, t - telegraph);
        }

        static void ConeNew(FxCanvas cv, float t, bool ground, float telegraph, Place at)
        {
            float x = at.BossX + 10, y = at.BossY - 8, age = t - telegraph;
            if (age < 0)
            {
                if (ground) Warn(cv, ConeShape(x, y, ConeR, 0, ConeWide), x, y - ConeR, x + ConeR, y + ConeR, t / telegraph, t, Danger);
                return;
            }
            // The blade's cut fills the wedge it warned of: a smear swept across it in a tenth of a second, fire along
            // its track, sparks flung out past the edge, and the track scorched into the floor.
            float swept = C01(age / 0.1f), head = -ConeWide / 2 + ConeWide * swept, left = C01((0.7f - age) / 0.3f);
            if (ground)
            {
                for (int i = 0; i < 7; i++)
                {
                    float dir = -ConeWide / 2 + ConeWide * (i + 0.5f) / 7;
                    if (dir > head) continue;
                    Scorch(cv, x + Cos(dir) * ConeR * 0.8f, y - Sin(dir) * ConeR * 0.8f, 9, i, 0.35f * left);
                }
                Glow(cv, x + ConeR * 0.6f, y, ConeR * 0.9f, Fire[2], 0.55f * C01(1 - age / 0.3f));
                return;
            }
            if (age < 0.1f)
            {
                // The whole wedge is lit as the cut crosses it.
                var wedge = ConeShape(x, y, ConeR, 0, ConeWide);
                cv.Pieces++;
                for (int py = (int)(y - ConeR); py <= y + ConeR; py++)
                    for (int px = (int)x; px <= x + ConeR; px++)
                        if (wedge(px + 0.5f, py + 0.5f).Item1 < 0 && Math.Atan2(-(py + 0.5f - y), px + 0.5f - x) < head) cv.Px(px, py, Fire[1], 0.4f);
            }
            if (age < 0.26f)
            {
                float fade = age < 0.12f ? 1 : C01((0.26f - age) / 0.14f);
                Crescent(cv, x, y, ConeR * 0.8f, 14, head, ConeWide * swept * 0.9f + 0.1f, Fire, fade * 0.9f);
                Crescent(cv, x, y, ConeR, 20, head, ConeWide * swept + 0.1f, Fire, fade);
                Crescent(cv, x, y, ConeR * 0.6f, 9, head, ConeWide * swept * 0.8f + 0.1f, Fire, fade * 0.8f);
            }
            for (int i = 0; i < 9; i++)
            {
                float dir = -ConeWide / 2 + ConeWide * (i + 0.5f) / 9, own = age - 0.1f * (i + 0.5f) / 9;
                if (own < 0) continue;
                float fx = x + Cos(dir) * ConeR * 0.88f, fy = y - Sin(dir) * ConeR * 0.88f;
                if (own < 0.5f) Flame(cv, fx, fy + 4, 11 * C01((0.5f - own) / 0.3f) + 1, 7, Fire, t, i);
                if (own < 0.4f && i % 2 == 1) Flame(cv, x + Cos(dir) * ConeR * 0.5f, y - Sin(dir) * ConeR * 0.5f + 4, 8 * C01((0.4f - own) / 0.3f) + 1, 6, Fire, t, i + 9);
                float p = own / 0.35f;
                if (p < 1) Spark(cv, fx + Cos(dir) * 16 * Out3(p), fy - Sin(dir) * 16 * Out3(p), Cos(dir), -Sin(dir), 5, Fire[0], Fire[2], 1 - p);
            }
            if (age < 0.05f) cv.Lighten(Fire[0], 0.1f);
        }

        const float BeamLong = 9 * Tile, BeamWide = 1.3f * Tile;

        static void BeamNow(PixelImage image, float t, float telegraph, Place at)
        {
            bool over = Over(ref t);
            float x = at.BossX + 14, y = at.HeroY - 8;
            if (!over && t < telegraph) MarkNow(image, BandShape(x, y, BeamLong, BeamWide), t / telegraph);
            if (!over) return;
            for (float d = 0.6f * Tile; d < BeamLong; d += 1.4f * Tile) FlashNow(image, x + d, y, BeamWide / 2 + 0.2f * Tile, t - telegraph);
        }

        static void BeamNew(FxCanvas cv, float t, bool ground, float telegraph, Place at)
        {
            float x = at.BossX + 14, y = at.HeroY - 8, age = t - telegraph, far = Math.Min(cv.W, x + BeamLong);
            if (age < 0)
            {
                float p = t / telegraph;
                if (ground)
                {
                    Warn(cv, BandShape(x, y, BeamLong, BeamWide), x, y - BeamWide / 2, far - 1, y + BeamWide / 2, p, t, Danger);
                    return;
                }
                // Light gathers at the crown, and a thin line of it already lies where the beam will be.
                Line(cv, x, y, far, y, Crystal[1], 0.25f + 0.5f * p);
                Glow(cv, x, y, 6 + 12 * p, Crystal[2], 0.7f * p);
                for (int i = 0; i < 8; i++)
                {
                    float dir = i * Tau / 8 + t * 3, gap = 16 * (1 - (p * 4 + i / 8f) % 1);
                    cv.Px((int)(x + Cos(dir) * gap), (int)(y - Sin(dir) * gap), Crystal[0]);
                }
                return;
            }
            float life = 0.45f;
            if (ground)
            {
                for (float d = 0; d < far - x; d += 9) Scorch(cv, x + d, y + 1, 6, (int)d, 0.3f * C01((1.1f - age) / 0.3f));
                cv.Pieces -= (int)((far - x) / 10);
                for (float d = 8; d < far - x; d += 22) Glow(cv, x + d, y, 30, Crystal[2], 0.4f * C01(1 - age / life));
                return;
            }
            if (age < life)
            {
                // Thin, then full, then narrowing: a core of white inside two shells, its edge rippling along its length.
                float body = age < 0.04f ? 0.35f : age < 0.3f ? 1 : (life - age) / 0.15f;
                cv.Pieces += 3;
                for (int px = (int)x; px < far; px++)
                {
                    float ripple = Sin(px * 0.55f - t * 40) * 1.2f + Sin(px * 0.21f + t * 23) * 0.8f;
                    float half = (BeamWide / 2 + ripple) * body;
                    for (int oy = (int)-half - 1; oy <= half + 1; oy++)
                    {
                        float u = Math.Abs(oy) / Math.Max(1, half);
                        if (u > 1) continue;
                        cv.Px(px, (int)y + oy, u < 0.3f ? Crystal[0] : u < 0.62f ? Crystal[1] : u < 0.85f ? Crystal[2] : Crystal[3]);
                    }
                }
                Blob(cv, x, y, (BeamWide * 0.8f + 2 * Sin(t * 50)) * body, 1, t * 30, 0.2f, Crystal[1]);
                Blob(cv, x, y, BeamWide * 0.5f * body, 2, t * 30, 0.15f, Crystal[0]);
                Star(cv, x, y, (int)(BeamWide * 1.1f * body), Crystal[0]);
                for (int i = 0; i < 12; i++)
                {
                    float p = (t * 2.5f + Rnd(i, 3)) % 1, px = x + Rnd(i, 4) * (far - x), up = i % 2 == 0 ? -1 : 1;
                    Shard(cv, px + p * 10, y + up * (BeamWide / 2 + 2 + p * 10), 5 - 3 * p, 2.5f, up * 0.6f, Crystal, 1 - p);
                }
                if (age < 0.05f) cv.Lighten(Crystal[1], 0.14f);
            }
            else if (age < life + 0.2f) Line(cv, x, y, far, y, Crystal[2], 1 - (age - life) / 0.2f);
        }

        // Where the crystal rain's seven circles fall, and when each begins.
        static readonly (float X, float Y, float Begins)[] Rain =
        {
            (0.52f, 0.62f, 0), (0.70f, 0.30f, 0.18f), (0.84f, 0.66f, 0.36f), (0.36f, 0.26f, 0.54f), (0.62f, 0.86f, 0.72f), (0.90f, 0.24f, 0.9f), (0.74f, 0.52f, 1.08f),
        };
        const float RainR = 1.3f * Tile;

        static void RainNow(PixelImage image, float t, float telegraph, Place at)
        {
            bool over = Over(ref t);
            foreach (var (fx, fy, begins) in Rain)
            {
                float x = fx * BossW, y = fy * BossH, own = t - begins;
                if (own < 0) continue;
                if (!over && own < telegraph) MarkNow(image, CircleShape(x, y, RainR), own / telegraph);
                if (over) FlashNow(image, x, y, RainR, own - telegraph);
            }
        }

        static void RainNew(FxCanvas cv, float t, bool ground, float telegraph, Place at)
        {
            // An ultimate dims the room, so its light is the brightest thing on the screen.
            if (ground) cv.Shade(1 - 0.25f * C01(t / 0.3f) * C01((2.5f - t) / 0.3f));
            int n = 0;
            foreach (var (fx, fy, begins) in Rain)
            {
                float x = fx * BossW, y = fy * BossH, own = t - begins, age = own - telegraph;
                n++;
                if (own < 0) continue;
                if (age < 0)
                {
                    float p = own / telegraph;
                    if (ground)
                    {
                        Warn(cv, CircleShape(x, y, RainR), x - RainR, y - RainR, x + RainR, y + RainR, p, t, Danger);
                        Disc(cv, x, y + 1, 2 + 6 * p, Rgba.Hex("#000000"), 0.3f, 1, 0.6f);
                    }
                    else if (p > 0.72f)
                    {
                        // The crystal itself, falling the last quarter of the warning with a streak of light behind.
                        float fall = (1 - p) / 0.28f, cy = y - 8 - fall * 90;
                        Line(cv, x, cy - 30, x, cy - 8, Crystal[1], 0.5f, 3);
                        Line(cv, x, cy - 46, x, cy - 30, Crystal[2], 0.3f);
                        Shard(cv, x, cy, 20, 8, -(float)Math.PI / 2, Crystal, 1, true);
                    }
                    continue;
                }
                if (ground)
                {
                    Scorch(cv, x, y, RainR * 0.8f, n, 0.3f * C01((1.2f - age) / 0.3f));
                    Glow(cv, x, y, RainR * 2.2f, Crystal[2], 0.75f * C01(1 - age / 0.4f));
                    continue;
                }
                // It stands in the floor where it struck, splinters springing up round it, then all of it breaks.
                float hold = 0.5f;
                if (age < hold)
                {
                    float sink = Out3(age / 0.06f);
                    for (int i = 0; i < 6; i++)
                    {
                        float dir = (i + 0.5f) * (float)Math.PI / 6, size = (9 + 4 * (i % 2)) * Out3((age - 0.02f * i) / 0.08f);
                        Shard(cv, x + Cos(dir) * RainR * 0.55f, y + 3 - Sin(dir) * size * 0.3f, size, 4, dir, Crystal);
                    }
                    Shard(cv, x, y - 8 + 3 * sink, 22, 9, -(float)Math.PI / 2, Crystal, 1, true);
                }
                else if (age < hold + 0.35f)
                {
                    float p = (age - hold) / 0.35f;
                    for (int i = 0; i < 9; i++)
                    {
                        float dir = i * Tau / 9;
                        Shard(cv, x + Cos(dir) * 14 * Out3(p), y - 6 - Sin(dir) * 10 * Out3(p) + 12 * p * p, 6 - 3 * p, 3, dir + p * 6, Crystal, 1 - p);
                    }
                }
                if (age < 0.06f)
                {
                    Disc(cv, x, y, RainR * 0.55f, Rgba.White, 0.9f);
                    Star(cv, x, y, (int)(RainR * 1.2f), Rgba.White);
                }
                if (age < 0.3f) Ring(cv, x, y, RainR * (0.4f + 0.85f * Out3(age / 0.25f)), age < 0.1f ? 2 : 1, Crystal[1], 1 - age / 0.3f);
                for (int i = 0; i < 8; i++)
                {
                    float p = age / 0.4f, dir = i * Tau / 8 + n;
                    if (p < 1) Spark(cv, x + Cos(dir) * RainR * 1.2f * Out3(p), y - Sin(dir) * RainR * Out3(p), Cos(dir), -Sin(dir), 4, Crystal[0], Crystal[2], 1 - p);
                }
            }
        }

        const int Shots = 5;
        const float ShotSpeed = 6 * Tile, ShotSpread = 16 * (float)Math.PI / 180;

        static void ShotsNow(PixelImage image, float t, float telegraph, Place at)
        {
            if (!Over(ref t)) return;
            float age = t - telegraph, x0 = at.BossX + 12, y0 = at.BossY - 12;
            if (age < 0) return;
            var ball = ItemArt.Spore;
            for (int i = 0; i < Shots; i++)
            {
                float dir = -0.12f + (i - (Shots - 1) / 2f) * ShotSpread, gone = age * ShotSpeed;
                float x = x0 + Cos(dir) * gone, y = y0 - Sin(dir) * gone;
                for (int py = 0; py < ball.Height; py++)
                    for (int px = 0; px < ball.Width; px++)
                        if (ball.IsSet(px, py))
                        {
                            var p = ball[px, py];
                            Tint(image, (int)(x - ball.Width / 2f) + px, (int)(y - ball.Height / 2f) + py, new Rgba(p.R * ShotOrange.R / 255, p.G * ShotOrange.G / 255, p.B * ShotOrange.B / 255), 1);
                        }
            }
        }

        static void ShotsNew(FxCanvas cv, float t, bool ground, float telegraph, Place at)
        {
            float age = t - telegraph, x0 = at.BossX + 12, y0 = at.BossY - 12;
            for (int i = 0; i < Shots; i++)
            {
                float dir = -0.12f + (i - (Shots - 1) / 2f) * ShotSpread, dx = Cos(dir), dy = -Sin(dir);
                if (age < 0)
                {
                    // Which way each will go, before it goes.
                    float p = t / telegraph;
                    if (ground)
                        for (float d = 14; d < 70 * Out3(p * 1.4f); d += 6)
                            if ((int)(d / 6 + t * 12) % 2 == 0) Line(cv, x0 + dx * d, y0 + dy * d, x0 + dx * (d + 3), y0 + dy * (d + 3), Danger[2], 0.3f + 0.5f * p);
                    if (!ground && i == 0)
                    {
                        Glow(cv, x0, y0, 8 + 10 * p, Fire[2], 0.6f * p);
                        Ring(cv, x0, y0, 12 * (1 - p) + 2, 1, Fire[1], p);
                    }
                    continue;
                }
                float gone = age * ShotSpeed, x = x0 + dx * gone, y = y0 + dy * gone;
                if (x > cv.W + 10) continue;
                if (ground)
                {
                    Glow(cv, x, y + 5, 13, Fire[2], 0.45f);
                    continue;
                }
                // A tail that thins, a dark rim, a red shell, a hot core that beats: bigger than the ball it replaces.
                for (int k = 4; k >= 1; k--) Disc(cv, x - dx * k * 4.5f, y - dy * k * 4.5f, 4.2f - k * 0.8f, k > 2 ? Danger[3] : Danger[2], 1, k > 2 ? 0.5f : 0.8f);
                bool beat = (int)(t * 12.5f) % 2 == 0;
                Disc(cv, x, y, 6, Danger[4]);
                Disc(cv, x, y, 5, Danger[2]);
                Disc(cv, x + dx, y + dy, beat ? 3.4f : 2.8f, Fire[1]);
                Disc(cv, x + dx, y + dy, beat ? 1.8f : 1.3f, Rgba.White);
            }
            if (!ground && age >= 0 && age < 0.12f)
            {
                Star(cv, x0, y0, 9, Fire[0], 1 - age / 0.12f);
                Ring(cv, x0, y0, 5 + 90 * age, 2, Fire[1], 1 - age / 0.12f);
            }
        }
    }
}
