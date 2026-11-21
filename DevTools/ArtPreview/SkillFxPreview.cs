using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core;

namespace Game.Client
{
    // Every skill's effect as the game draws it (SkillFx, Docs/SkillEffects.md), played on a dark floor with the hero
    // and a slime for scale: a sheet of frames and an animated GIF for each class (out/skillfx/<class>.png, .gif), the
    // sheet again in greys (<class>_gray.png: an effect must still read by value alone), and a report of how long each
    // lasts and how many pieces it draws at once. A skill with no effect of its own, one that runs too long or one
    // that draws too much fails the run.
    static class SkillFxPreview
    {
        const int Tile = TileArt.Size;
        const int Columns = 12;
        // The most pieces one effect may draw at once (WebGL: each is a sprite), and the longest a cast may last.
        const int MostStamps = 72;
        const float Longest = 1.4f;

        sealed class Scene
        {
            public SkillDefinition Skill;
            public int Width, Height;
            // The hero's feet, and where the skill's projectiles land.
            public int HeroX, HeroY;
            public float Reach;
            public float Duration;
            public float CastLife;
            public FxContext Context;
            public PixelImage Hero;
            public PixelImage Slime;
            public List<(int X, int Y)> Slimes = new List<(int, int)>();
            public int MostAtOnce;
        }

        public static bool Write(string folder, ClassData classes, PixelImage floor, Rgba slimeColor)
        {
            Directory.CreateDirectory(folder);
            bool fine = true;
            var report = new StringBuilder();
            report.AppendLine("skill            cast s  whole s  most pieces");
            foreach (var cls in classes.Classes)
            {
                var scenes = cls.Skills.Select(skill => Make(skill, slimeColor)).ToList();
                var rows = new List<List<PixelImage>>();
                var frames = new List<(Rgba[] Pixels, int Centiseconds)>();
                int wide = scenes.Max(s => s.Width), tall = scenes.Max(s => s.Height);
                foreach (var scene in scenes)
                {
                    if (!SkillFx.Knows(scene.Skill.Id))
                    {
                        Console.Error.WriteLine($"skill {scene.Skill.Id}: no effect of its own in SkillFx");
                        fine = false;
                        continue;
                    }
                    rows.Add(Enumerable.Range(0, Columns).Select(i => Render(scene, floor, scene.Duration * i / Columns)).ToList());
                    for (float t = 0; t < scene.Duration; t += 0.04f)
                    {
                        var frame = new PixelImage(wide, tall);
                        frame.Fill(0, 0, wide, tall, Rgba.Hex("#0A0A0F"));
                        frame.Draw(Render(scene, floor, t), (wide - scene.Width) / 2, (tall - scene.Height) / 2);
                        frames.Add((Flat(Doubled(frame)), 4));
                    }
                    // A pause between skills.
                    frames.Add((frames[frames.Count - 1].Pixels, 30));
                    report.AppendLine($"{scene.Skill.Id,-16} {scene.CastLife,6:0.00} {scene.Duration,8:0.00} {scene.MostAtOnce,12}");
                    if (scene.CastLife > Longest)
                    {
                        Console.Error.WriteLine($"skill {scene.Skill.Id}: its effect lasts {scene.CastLife:0.00} s, more than {Longest}");
                        fine = false;
                    }
                    if (scene.MostAtOnce > MostStamps)
                    {
                        Console.Error.WriteLine($"skill {scene.Skill.Id}: {scene.MostAtOnce} pieces at once, more than {MostStamps}");
                        fine = false;
                    }
                    if (Count(scene, 0.04f) == 0 || Count(scene, scene.CastLife * 0.9f) == 0)
                    {
                        Console.Error.WriteLine($"skill {scene.Skill.Id}: nothing drawn at its start or its end (anticipation, dissipation)");
                        fine = false;
                    }
                }
                if (rows.Count == 0) continue;
                var sheet = Sheet(rows);
                Png.Save(Scaled(sheet, 3), Path.Combine(folder, cls.Id + ".png"));
                Png.Save(Scaled(Grey(sheet), 3), Path.Combine(folder, cls.Id + "_gray.png"));
                Gif.Save(Path.Combine(folder, cls.Id + ".gif"), wide * 2, tall * 2, frames);
            }
            fine &= WriteRolls(folder, floor, report);
            // Every piece on its own, in every element it is used in most.
            Png.Save(Scaled(Pieces(), 4), Path.Combine(folder, "pieces.png"));
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Console.WriteLine($"Wrote skillfx/<class>.png, <class>_gray.png, <class>.gif, pieces.png and report.txt to {folder}");
            return fine;
        }

        // The dodge roll's three looks (SkillFx.Roll, HeroMove.Roll), which are no class's skill: the hero goes the
        // roll's length in its time with the motion of what they hold, and the effect is laid from where it began
        // (roll.png, roll_gray.png, roll.gif), held to the same budget as a skill's.
        static bool WriteRolls(string folder, PixelImage floor, StringBuilder report)
        {
            bool fine = true;
            var rows = new List<List<PixelImage>>();
            var frames = new List<(Rgba[] Pixels, int Centiseconds)>();
            float length = World.RollDistance * Tile;
            int wide = (int)length + 80, tall = 80, startX = 30;
            var tunic = Rgba.Hex("#4D7FD9");
            foreach (var (id, weapon) in new[] { (SkillFx.Roll, HeroWeapon.Sword), (SkillFx.RollSlide, HeroWeapon.Bow), (SkillFx.RollBlink, HeroWeapon.Staff) })
            {
                var gear = HeroGear.Plain(weapon);
                var context = new FxContext { Dir = 0, Length = length, Seed = 7, Main = FxRamp.Mist };
                var some = new List<FxStamp>();
                int CountAt(float t)
                {
                    some.Clear();
                    SkillFx.Stamps(FxProgram.Cast, id, context, t, some);
                    return some.Count;
                }
                float life = 0;
                while (SkillFx.Stamps(FxProgram.Cast, id, context, life, some) && life < 5) life += 0.01f;
                int most = 0;
                for (float t = 0; t < life; t += 0.02f) most = Math.Max(most, CountAt(t));
                PixelImage At(float t)
                {
                    var scene = new Scene { Width = wide, Height = tall, HeroY = 52 };
                    scene.HeroX = startX + (int)Math.Round(length * Math.Min(1, t / World.RollSeconds));
                    scene.Hero = t < World.RollSeconds
                        ? HeroArt.Draw(Look.Side, HeroMove.Roll, HeroArt.FrameAt(HeroMove.Roll, weapon, t), gear, tunic)
                        : HeroArt.Draw(Look.Side, HeroMove.Stand, 0, gear, tunic);
                    var image = new PixelImage(wide, tall);
                    for (int y = 0; y < tall; y += floor.Height)
                        for (int x = 0; x < wide; x += floor.Width)
                            image.Draw(floor, x, y);
                    CountAt(t);
                    foreach (var stamp in some)
                    {
                        if (stamp.Ground) Blit(image, scene, startX - scene.HeroX, 0, stamp);
                    }
                    image.Draw(scene.Hero, scene.HeroX - HeroArt.FeetX, scene.HeroY - HeroArt.FeetY);
                    foreach (var stamp in some)
                    {
                        if (!stamp.Ground) Blit(image, scene, startX - scene.HeroX, 0, stamp);
                    }
                    return image;
                }
                rows.Add(Enumerable.Range(0, Columns).Select(i => At(life * i / Columns)).ToList());
                for (float t = 0; t < life; t += 0.04f) frames.Add((Flat(Doubled(At(t))), 4));
                frames.Add((frames[frames.Count - 1].Pixels, 30));
                report.AppendLine($"{id,-16} {life,6:0.00} {life,8:0.00} {most,12}");
                if (life > Longest || most > MostStamps || CountAt(0.04f) == 0 || CountAt(life * 0.9f) == 0)
                {
                    Console.Error.WriteLine($"{id}: {life:0.00} s (at most {Longest}), {most} pieces at once (at most {MostStamps}), or nothing drawn at its start or its end");
                    fine = false;
                }
            }
            var sheet = Sheet(rows);
            Png.Save(Scaled(sheet, 3), Path.Combine(folder, "roll.png"));
            Png.Save(Scaled(Grey(sheet), 3), Path.Combine(folder, "roll_gray.png"));
            Gif.Save(Path.Combine(folder, "roll.gif"), wide * 2, tall * 2, frames);
            return fine;
        }

        static Scene Make(SkillDefinition skill, Rgba slimeColor)
        {
            var weapon = skill.Class.Id == "warrior" ? HeroWeapon.Sword : skill.Class.Id == "archer" ? HeroWeapon.Bow : HeroWeapon.Staff;
            var scene = new Scene
            {
                Skill = skill,
                Hero = HeroArt.Draw(Look.Side, HeroMove.Stand, 0, HeroGear.Plain(weapon), Rgba.Hex("#4D7FD9")),
                Slime = CreatureArt.Monster("slime", slimeColor, 0),
            };
            float radius = skill.Radius > 0 ? (skill.Radius + World.PlayerRadius) * Tile : 0;
            bool fire = (skill.Tags & SkillTag.Fire) != 0, cold = (skill.Tags & SkillTag.Cold) != 0, lightning = (skill.Tags & SkillTag.Lightning) != 0;
            scene.Context = new FxContext { Radius = radius, Dir = 0, Seed = 7, Main = SkillFx.MainOf(skill.Id, false, false, false) };
            switch (skill.Kind)
            {
                case SkillKind.Projectile:
                    scene.Reach = Math.Min(skill.Range, 5) * Tile;
                    float burst = skill.Radius * Tile;
                    scene.Width = 40 + (int)scene.Reach + (int)Math.Max(burst, 28) + 12;
                    scene.Height = Math.Max(96, 2 * (int)burst + 40);
                    if (skill.Count > 1) scene.Height = Math.Max(scene.Height, (int)(2 * scene.Reach * Math.Sin((skill.Count - 1) / 2f * 12 * Math.PI / 180)) + 64);
                    scene.HeroX = 28;
                    scene.HeroY = scene.Height / 2 + 8;
                    scene.Context.Radius = burst;
                    scene.Slimes.Add((scene.HeroX + (int)scene.Reach + 6, scene.HeroY));
                    if (skill.Id == "chain_lightning")
                    {
                        scene.Slimes.Add((scene.HeroX + (int)scene.Reach - 20, scene.HeroY - 30));
                        scene.Slimes.Add((scene.HeroX + (int)scene.Reach + 22, scene.HeroY + 30));
                    }
                    break;
                case SkillKind.Dash:
                    scene.Context.Length = skill.Range * Tile;
                    scene.Width = (int)scene.Context.Length + 80;
                    scene.Height = 96;
                    scene.HeroX = scene.Width - 44;
                    scene.HeroY = 56;
                    if (skill.Damage > 0) scene.Slimes.Add((scene.HeroX + 16, scene.HeroY));
                    break;
                default:
                    int side = Math.Max(104, 2 * (int)radius + 36);
                    scene.Width = side;
                    scene.Height = skill.Id == "thunderclap" ? side + 40 : side;
                    scene.HeroX = side / 2;
                    scene.HeroY = scene.Height - side / 2 + 4;
                    if (radius > 0) scene.Slimes.Add((scene.HeroX + (int)radius - 8, scene.HeroY - 6));
                    // As the game does: a nova's effect is centred on the body, a little above the feet.
                    if (skill.Kind == SkillKind.Nova) scene.Context.Lift = 4;
                    // The summoner's spirits stand where the game puts them (World.WaitOffset), for the pact's lines.
                    if (skill.Id == "spirit_pact") scene.Context.Points = new[] { -10f, 10f, 30f, -18f };
                    break;
            }
            var none = new List<FxStamp>();
            float life = 0;
            while (SkillFx.Stamps(FxProgram.Cast, skill.Id, scene.Context, life, none) && life < 5) life += 0.01f;
            scene.CastLife = life;
            if (skill.Kind == SkillKind.Projectile)
            {
                float land = scene.Reach / (skill.Speed * Tile), after = 0;
                none.Clear();
                while (SkillFx.Stamps(FxProgram.Hit, skill.Id, scene.Context, after, none) && after < 5) after += 0.01f;
                scene.CastLife = Math.Max(life, after);
                scene.Duration = Math.Max(life, land + Math.Max(after, skill.Id == "chain_lightning" ? 0.4f : 0));
            }
            else scene.Duration = life + (SkillFx.HasLoop(skill.Id) ? 1.2f : 0);
            for (float t = 0; t < scene.Duration; t += 0.02f) scene.MostAtOnce = Math.Max(scene.MostAtOnce, Count(scene, t));
            return scene;
        }

        static int Count(Scene scene, float t) => Gather(scene, t).Count;

        // What the game would draw at this moment: the cast, each projectile in flight or landing, the buff's cue after.
        static List<(float X, float Y, FxStamp Stamp)> Gather(Scene scene, float t)
        {
            var all = new List<(float, float, FxStamp)>();
            var some = new List<FxStamp>();
            var skill = scene.Skill;
            void Take(FxProgram program, in FxContext context, float time, float x, float y)
            {
                some.Clear();
                SkillFx.Stamps(program, skill.Id, context, time, some);
                foreach (var stamp in some) all.Add((x, y, stamp));
            }
            if (t < scene.CastLife || skill.Kind == SkillKind.Projectile) Take(FxProgram.Cast, scene.Context, t, 0, 0);
            else if (SkillFx.HasLoop(skill.Id)) Take(FxProgram.Loop, scene.Context, t, 0, 0);
            if (skill.Kind == SkillKind.Projectile)
            {
                int count = Math.Max(1, skill.Count);
                float land = scene.Reach / (skill.Speed * Tile);
                for (int i = 0; i < count; i++)
                {
                    float angle = (i - (count - 1) / 2f) * 12 * (float)Math.PI / 180;
                    var context = scene.Context;
                    context.Dir = angle;
                    context.Seed = 7 + i;
                    float gone = Math.Min(t, land) * skill.Speed * Tile + World.PlayerRadius * Tile;
                    float x = (float)Math.Cos(angle) * gone, y = (float)Math.Sin(angle) * gone + 8;
                    if (t < land) Take(FxProgram.Shot, context, t, x, y);
                    else Take(FxProgram.Hit, context, t - land, x, y);
                }
                if (skill.Id == "chain_lightning" && t >= land)
                {
                    for (int i = 1; i < scene.Slimes.Count; i++)
                    {
                        float fx = scene.Slimes[0].X - scene.HeroX, fy = scene.HeroY - scene.Slimes[0].Y + 6;
                        float tx = scene.Slimes[i].X - scene.HeroX, ty = scene.HeroY - scene.Slimes[i].Y + 6;
                        var context = new FxContext { Length = (float)Math.Sqrt((tx - fx) * (tx - fx) + (ty - fy) * (ty - fy)), Dir = (float)Math.Atan2(ty - fy, tx - fx), Seed = 3 + i };
                        Take(FxProgram.Chain, context, t - land, (fx + tx) / 2, (fy + ty) / 2);
                    }
                }
            }
            return all;
        }

        static PixelImage Render(Scene scene, PixelImage floor, float t)
        {
            var image = new PixelImage(scene.Width, scene.Height);
            for (int y = 0; y < scene.Height; y += floor.Height)
                for (int x = 0; x < scene.Width; x += floor.Width)
                    image.Draw(floor, x, y);
            var stamps = Gather(scene, t);
            foreach (var (x, y, stamp) in stamps)
            {
                if (stamp.Ground) Blit(image, scene, x, y, stamp);
            }
            foreach (var (x, y) in scene.Slimes) image.Draw(scene.Slime, x - scene.Slime.Width / 2, y - scene.Slime.Height + 2);
            image.Draw(scene.Hero, scene.HeroX - HeroArt.FeetX, scene.HeroY - HeroArt.FeetY);
            foreach (var (x, y, stamp) in stamps)
            {
                if (!stamp.Ground) Blit(image, scene, x, y, stamp);
            }
            return image;
        }

        // A piece laid on the picture as a sprite would be: centred on its place, turned without smoothing.
        static void Blit(PixelImage image, Scene scene, float originX, float originY, FxStamp stamp)
        {
            PixelImage piece;
            int anchorX, anchorY;
            if (stamp.Shape == FxShape.Ghost)
            {
                // The caster's own picture in one pale colour, standing on the spot.
                var tint = SkillFxArt.Ramp(stamp.Ramp)[2];
                piece = new PixelImage(scene.Hero.Width, scene.Hero.Height);
                for (int y = 0; y < piece.Height; y++)
                    for (int x = 0; x < piece.Width; x++)
                        if (scene.Hero.IsSet(x, y)) piece[x, y] = tint;
                anchorX = HeroArt.FeetX;
                anchorY = HeroArt.FeetY;
            }
            else
            {
                piece = SkillFxArt.Draw(stamp.Shape, stamp.Ramp, stamp.Size, stamp.Frame);
                anchorX = -1;
                anchorY = -1;
            }
            float cx = scene.HeroX + originX + stamp.X, cy = scene.HeroY - originY - stamp.Y;
            if (anchorX >= 0)
            {
                Over(image, piece, (int)Math.Round(cx) - anchorX, (int)Math.Round(cy) - anchorY, stamp.Alpha);
                return;
            }
            if (Math.Abs(stamp.Angle % 360) < 0.01f)
            {
                Over(image, piece, (int)Math.Round(cx - piece.Width / 2f), (int)Math.Round(cy - piece.Height / 2f), stamp.Alpha);
                return;
            }
            double radians = stamp.Angle * Math.PI / 180, cos = Math.Cos(radians), sin = Math.Sin(radians);
            int reach = (int)Math.Ceiling(Math.Sqrt(piece.Width * piece.Width + piece.Height * piece.Height) / 2) + 1;
            for (int y = (int)cy - reach; y <= (int)cy + reach; y++)
            {
                for (int x = (int)cx - reach; x <= (int)cx + reach; x++)
                {
                    double dx = x + 0.5 - cx, dy = -(y + 0.5 - cy);
                    double ux = dx * cos + dy * sin, uy = -dx * sin + dy * cos;
                    int sx = (int)Math.Floor(ux + piece.Width / 2.0), sy = (int)Math.Floor(-uy + piece.Height / 2.0);
                    if (!piece.Inside(sx, sy)) continue;
                    var c = piece[sx, sy];
                    if (c.A > 0) image.Blend(x, y, c.WithAlpha((int)(c.A * stamp.Alpha)));
                }
            }
        }

        static void Over(PixelImage image, PixelImage piece, int left, int top, float alpha)
        {
            for (int y = 0; y < piece.Height; y++)
            {
                for (int x = 0; x < piece.Width; x++)
                {
                    var c = piece[x, y];
                    if (c.A > 0) image.Blend(left + x, top + y, c.WithAlpha((int)(c.A * alpha)));
                }
            }
        }

        static PixelImage Sheet(List<List<PixelImage>> rows)
        {
            const int gap = 2;
            int width = rows.Max(row => row.Sum(i => i.Width + gap)) + gap, height = rows.Sum(row => row[0].Height + gap) + gap;
            var sheet = new PixelImage(width, height);
            sheet.Fill(0, 0, width, height, Rgba.Hex("#0A0A0F"));
            int y = gap;
            foreach (var row in rows)
            {
                int x = gap;
                foreach (var image in row)
                {
                    sheet.Draw(image, x, y);
                    x += image.Width + gap;
                }
                y += row[0].Height + gap;
            }
            return sheet;
        }

        static PixelImage Pieces()
        {
            var list = new List<PixelImage>();
            foreach (FxRamp ramp in new[] { FxRamp.Fire, FxRamp.Cold, FxRamp.Bolt, FxRamp.Steel, FxRamp.Spirit })
            {
                for (int f = 0; f < 4; f++) list.Add(SkillFxArt.Draw(FxShape.Flame, ramp, 10 + 4 * f, f));
                for (int f = 0; f < 6; f++) list.Add(SkillFxArt.Draw(FxShape.Blob, ramp, 24, f));
                for (int f = 0; f < 4; f++) list.Add(SkillFxArt.Draw(FxShape.Smoke, ramp, 8, f));
                list.Add(SkillFxArt.Draw(FxShape.Shard, ramp, 12, 0));
                list.Add(SkillFxArt.Draw(FxShape.Shard, ramp, 20, 1));
                for (int f = 0; f < 3; f++) list.Add(SkillFxArt.Draw(FxShape.Bolt, ramp, 32, f));
                list.Add(SkillFxArt.Draw(FxShape.Bolt, ramp, 64, 8));
                list.Add(SkillFxArt.Draw(FxShape.Crescent, ramp, 20, 110));
                list.Add(SkillFxArt.Draw(FxShape.Crack, ramp, 28, 0));
                list.Add(SkillFxArt.Draw(FxShape.Streak, ramp, 16, 1));
                list.Add(SkillFxArt.Draw(FxShape.Streak, ramp, 28, 3));
                list.Add(SkillFxArt.Draw(FxShape.Arrow, ramp, 11, 0));
                list.Add(SkillFxArt.Draw(FxShape.Wisp, ramp, 5, 0));
                list.Add(SkillFxArt.Draw(FxShape.Wisp, ramp, 9, 1));
                list.Add(SkillFxArt.Draw(FxShape.Rune, ramp, 40, 0));
                list.Add(SkillFxArt.Draw(FxShape.Glint, ramp, 9, 2));
                list.Add(SkillFxArt.Draw(FxShape.Reticle, ramp, 18, 0));
                for (int f = 0; f < 3; f++) list.Add(SkillFxArt.Draw(FxShape.Fireball, ramp, 9 + 3 * f, f));
                list.Add(SkillFxArt.Draw(FxShape.Scorch, ramp, 40, 0));
                list.Add(SkillFxArt.Draw(FxShape.Edge, ramp, 40, 3));
                list.Add(SkillFxArt.Draw(FxShape.Broken, ramp, 30, SkillFxArt.BrokenFrame(2, 4)));
                list.Add(SkillFxArt.Draw(FxShape.Blade, ramp, 11, 0));
                list.Add(SkillFxArt.Draw(FxShape.Tether, ramp, 24, 0));
                list.Add(SkillFxArt.Draw(FxShape.Plus, ramp, 5, 0));
                list.Add(SkillFxArt.Draw(FxShape.Leaf, ramp, 1, 0));
            }
            var rows = new List<List<PixelImage>>();
            int per = list.Count / 5;
            for (int i = 0; i < 5; i++)
            {
                // Each on a dark tile so it is seen as in a cave.
                rows.Add(list.Skip(i * per).Take(per).Select(piece =>
                {
                    var tile = new PixelImage(piece.Width + 4, 70);
                    tile.Fill(0, 0, tile.Width, tile.Height, Rgba.Hex("#24202A"));
                    tile.Draw(piece, 2, (70 - Math.Min(66, piece.Height)) / 2);
                    return tile;
                }).ToList());
            }
            return Sheet(rows);
        }

        static PixelImage Grey(PixelImage image)
        {
            var grey = new PixelImage(image.Width, image.Height);
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var c = image[x, y];
                    int value = (int)(0.299f * c.R + 0.587f * c.G + 0.114f * c.B);
                    grey[x, y] = new Rgba(value, value, value, c.A);
                }
            }
            return grey;
        }

        static PixelImage Scaled(PixelImage image, int scale)
        {
            var result = new PixelImage(image.Width * scale, image.Height * scale);
            for (int y = 0; y < result.Height; y++)
                for (int x = 0; x < result.Width; x++)
                    result[x, y] = image[x / scale, y / scale];
            return result;
        }

        static PixelImage Doubled(PixelImage image) => Scaled(image, 2);

        static Rgba[] Flat(PixelImage image)
        {
            var pixels = new Rgba[image.Width * image.Height];
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    pixels[y * image.Width + x] = image[x, y];
            return pixels;
        }
    }
}
