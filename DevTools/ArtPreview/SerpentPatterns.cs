using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Client
{
    // The tide serpent's whole pattern in one picture (2026-11-28, serpent_patterns.gif): a cell for each attack and the
    // entrance, each playing as the game does, the body's wind-up with the floor warning filling under it, the blow as
    // the warning fills, the warning's shape burning and the serpent's own water over it, then back to rest. Lengths
    // are cut down to fit the cells (the reach of the jet and the rings), times are the sheet's own telegraphs.
    static partial class Motions
    {
        sealed class SerpentMark
        {
            public HazardForm Form;
            public float Size, Width, Degrees, X, Y, Delay;
            public WarnTone Tone = WarnTone.Danger;
            public BossFx Fx;
            public BossContext Context;
        }

        sealed class SerpentScene
        {
            public string Name;
            public SerpentPose Blow;
            public float Telegraph;
            public List<SerpentMark> Marks = new List<SerpentMark>();
            // Shots let fly in a ring from its head (the bubbles), and how many waves of them.
            public int Shots, Waves;
            public bool Spin, Entrance, Roar;
            public int Phase = 1;
        }

        const int PatternTile = 16;

        static List<SerpentScene> SerpentScenes()
        {
            var cold = FxRamp.Cold;
            SerpentMark Mark(HazardForm form, float size, float width, float degrees, float x, float y, BossFx fx, float delay = 0, WarnTone tone = WarnTone.Danger, bool big = false, float inner = 0)
            {
                var context = new BossContext
                {
                    Ramp = cold, Size = size * PatternTile, Width = form == HazardForm.Cone ? width : width * PatternTile, Dir = degrees, Inner = inner * PatternTile,
                    Big = big, Seed = (int)(x * 7 + y * 13 + size * 3), Form = form,
                };
                return new SerpentMark { Form = form, Size = size, Width = width, Degrees = degrees, X = x, Y = y, Delay = delay, Tone = tone, Fx = fx, Context = context };
            }
            var scenes = new List<SerpentScene>
            {
                new SerpentScene { Name = "enter", Entrance = true },
                new SerpentScene { Name = "tail_sweep", Blow = SerpentPose.Lash, Telegraph = 0.6f, Marks = { Mark(HazardForm.Cone, 3.6f, 120, 0, 0.3f, 0, BossFx.Wave) } },
                new SerpentScene { Name = "water_jet", Blow = SerpentPose.Spit, Telegraph = 0.7f, Marks = { Mark(HazardForm.Band, 7.5f, 1.4f, 0, 0.5f, 0, BossFx.Torrent) } },
                new SerpentScene { Name = "bubble_burst", Blow = SerpentPose.Burst, Telegraph = 0.5f, Shots = 12, Waves = 1 },
                new SerpentScene
                {
                    Name = "tidal_trail", Blow = SerpentPose.Surge, Telegraph = 0.7f,
                    Marks = { Mark(HazardForm.Circle, 1.1f, 0, 0, 1.8f, 0, BossFx.Geyser, 0), Mark(HazardForm.Circle, 1.1f, 0, 0, 3.4f, 0.2f, BossFx.Geyser, 0.18f),
                        Mark(HazardForm.Circle, 1.1f, 0, 0, 5f, 0.4f, BossFx.Geyser, 0.36f), Mark(HazardForm.Circle, 1.1f, 0, 0, 6.6f, 0.5f, BossFx.Geyser, 0.54f) },
                },
                new SerpentScene { Name = "whirlpool", Blow = SerpentPose.Whirl, Telegraph = 1.0f, Phase = 2, Marks = { Mark(HazardForm.Donut, 4.2f, 1.6f, 0, 0, 0, BossFx.Maelstrom, inner: 1.6f) } },
                new SerpentScene { Name = "deep_share", Blow = SerpentPose.Crash, Telegraph = 1.1f, Phase = 2, Marks = { Mark(HazardForm.Circle, 1.8f, 0, 0, 4.2f, 0, BossFx.Geyser, 0, WarnTone.Gather, true) } },
                new SerpentScene
                {
                    Name = "tidal_cross", Blow = SerpentPose.Slam, Telegraph = 1.0f, Phase = 2,
                    Marks = { Mark(HazardForm.Band, 4.5f, 1.4f, 45, 0, 0, BossFx.Torrent), Mark(HazardForm.Band, 4.5f, 1.4f, 135, 0, 0, BossFx.Torrent),
                        Mark(HazardForm.Band, 4.5f, 1.4f, 225, 0, 0, BossFx.Torrent), Mark(HazardForm.Band, 4.5f, 1.4f, 315, 0, 0, BossFx.Torrent) },
                },
                new SerpentScene
                {
                    Name = "spray_rain", Blow = SerpentPose.Spout, Telegraph = 1.4f, Phase = 3,
                    Marks = { Mark(HazardForm.Circle, 1.4f, 0, 0, 3.6f, 1.6f, BossFx.Geyser, 0.1f, big: true), Mark(HazardForm.Circle, 1.4f, 0, 0, 5.2f, -0.6f, BossFx.Geyser, 0.2f, big: true),
                        Mark(HazardForm.Circle, 1.4f, 0, 0, 2.2f, -1.8f, BossFx.Geyser, 0.3f, big: true) },
                },
                new SerpentScene { Name = "bubble_spiral", Blow = SerpentPose.Burst, Telegraph = 0.5f, Phase = 3, Shots = 6, Waves = 5, Spin = true },
                new SerpentScene { Name = "roar", Roar = true, Phase = 3 },
                new SerpentScene { Name = "undertow", Blow = SerpentPose.Whirl, Telegraph = 0.5f, Marks = { Mark(HazardForm.Donut, 4.6f, 1.2f, 0, 2.2f, 0, BossFx.Maelstrom, inner: 1.2f) } },
            };
            return scenes;
        }

        // The pose and frame at so many seconds into a scene's loop, and how far into its blow it is (seconds since
        // the strike, below 0 before it).
        static (SerpentPose Pose, int Frame, float Since) SerpentAt(SerpentScene scene, float time)
        {
            const float idle = 0.4f;
            if (scene.Entrance)
            {
                int frames = SerpentArt.FramesOf(SerpentPose.Enter);
                float length = 3.6f;
                if (time < 0.3f) return (SerpentPose.Enter, 0, -1);
                float progress = (time - 0.3f) / length;
                return progress >= 1 ? (SerpentPose.Idle, (int)(time * 5) % SerpentArt.FramesOf(SerpentPose.Idle), -1) : (SerpentPose.Enter, SerpentArt.EnterFrameAt(progress, false), -1);
            }
            if (scene.Roar)
            {
                if (time < idle) return (SerpentPose.Idle, (int)(time * 5) % SerpentArt.FramesOf(SerpentPose.Idle), -1);
                int rise = (int)((time - idle) / 0.06f);
                if (time > idle + 2.2f) return (SerpentPose.Idle, (int)(time * 5) % SerpentArt.FramesOf(SerpentPose.Idle), -1);
                return (SerpentPose.Roar, rise < SerpentArt.RoarRise ? rise : SerpentArt.RoarRise + (int)(time * 10) % 4, -1);
            }
            if (time < idle) return (SerpentPose.Idle, (int)(time * 5) % SerpentArt.FramesOf(SerpentPose.Idle), -1);
            float t = time - idle;
            var blow = scene.Blow;
            var windUp = SerpentArt.WindUpOf(blow);
            int strike = SerpentArt.StrikeFrame(blow);
            if (t < scene.Telegraph)
            {
                float progress = t / scene.Telegraph;
                float swing = strike * SerpentArt.FrameSeconds(blow, 0);
                float wound = 1 - Math.Min(0.5f, swing / scene.Telegraph);
                if (progress < wound) return (windUp, Math.Min(SerpentArt.FramesOf(windUp) - 1, (int)(progress / wound * SerpentArt.FramesOf(windUp))), t - scene.Telegraph);
                float into = (progress - wound) * scene.Telegraph;
                int f = 0;
                for (; f < strike; f++)
                {
                    into -= SerpentArt.FrameSeconds(blow, f);
                    if (into < 0) break;
                }
                return (blow, Math.Min(strike - 1, f), t - scene.Telegraph);
            }
            float since = t - scene.Telegraph, left = since;
            for (int k = strike; k < SerpentArt.FramesOf(blow); k++)
            {
                left -= SerpentArt.FrameSeconds(blow, k);
                if (left < 0) return (blow, k, since);
            }
            return (SerpentPose.Idle, (int)(time * 5) % SerpentArt.FramesOf(SerpentPose.Idle), since);
        }

        static void SerpentPatterns(string folder, PixelImage arena)
        {
            var scenes = SerpentScenes();
            const int cellWidth = 232, cellHeight = 176, columns = 4, scale = 2;
            int rows = (scenes.Count + columns - 1) / columns;
            int width = cellWidth * columns * scale, height = cellHeight * rows * scale;
            const float loop = 4.4f, step = 0.04f;
            var frames = new List<(Rgba[] Pixels, int Centiseconds)>();
            var sprites = new Dictionary<(SerpentPose, int, int, int), PixelImage>();
            var shotColors = ShotArt.Colors(ShotElement.Water);
            for (float time = 0; time < loop; time += step)
            {
                var pixels = new Rgba[width * height];
                for (int s = 0; s < scenes.Count; s++)
                {
                    var scene = scenes[s];
                    var cell = new PixelImage(cellWidth, cellHeight);
                    for (int y = 0; y < cellHeight; y++)
                        for (int x = 0; x < cellWidth; x++)
                            cell[x, y] = arena[x % arena.Width, y % arena.Height];
                    // Its coils rest a little left of the middle for an attack ahead of it, in the middle for one round it.
                    bool round = scene.Name == "whirlpool" || scene.Name == "tidal_cross" || scene.Name == "bubble_burst" || scene.Name == "bubble_spiral" || scene.Entrance || scene.Roar;
                    float feetX = round ? cellWidth / 2f : 52, feetY = cellHeight * 0.62f;
                    var (pose, frame, since) = SerpentAt(scene, time);
                    float telegraphing = since < 0 && !scene.Entrance && !scene.Roar && time >= 0.4f ? 1 + since / scene.Telegraph : -1;

                    var air = new List<(float X, float Y, FxStamp Stamp)>();
                    foreach (var mark in scene.Marks)
                    {
                        float mx = feetX + mark.X * PatternTile, my = feetY - mark.Y * PatternTile;
                        var field = HazardArt.Field(mark.Form, mark.Size, mark.Form == HazardForm.Cone ? mark.Width : mark.Width, mark.Degrees * (float)Math.PI / 180);
                        if (mark.Form == HazardForm.Donut) field = HazardArt.Field(mark.Form, mark.Size, mark.Width, 0);
                        var buffer = field.Buffer();
                        float age = since - mark.Delay;
                        bool show;
                        if (telegraphing >= 0)
                        {
                            HazardArt.Warn(field, mark.Tone, (float)Math.Floor(telegraphing * HazardArt.FillSteps) / HazardArt.FillSteps, telegraphing > 0.86f && ((int)(time * 16) & 1) == 0, false, buffer, false);
                            show = true;
                        }
                        else if (age >= 0)
                        {
                            show = HazardArt.Blast(field, mark.Tone == WarnTone.Gather ? HazardArt.Tone(WarnTone.Gather) : SkillFxArt.Ramp5(FxRamp.Cold), age, 3, buffer, false);
                            if (!show)
                            {
                                HazardArt.Scar(field, FloorStuff.Stone, 3, buffer, false);
                                show = age < 1.6f;
                            }
                        }
                        else if (since >= 0)
                        {
                            HazardArt.Warn(field, mark.Tone, 1, ((int)(time * 16) & 1) == 0, false, buffer, false);
                            show = true;
                        }
                        else show = false;
                        if (show)
                        {
                            var shape = HazardArt.Image(field, buffer);
                            int left = (int)(mx - field.OriginX * field.Scale), top = (int)(my - field.OriginY * field.Scale);
                            for (int y = 0; y < shape.Height * field.Scale; y++)
                                for (int x = 0; x < shape.Width * field.Scale; x++)
                                {
                                    var c = shape[x / field.Scale, y / field.Scale];
                                    if (c.A > 0 && cell.Inside(left + x, top + y)) cell.Blend(left + x, top + y, c);
                                }
                        }
                        if (age >= 0)
                        {
                            var stamps = new List<FxStamp>();
                            var context = mark.Context;
                            if (mark.Form == HazardForm.Band) context.Width = mark.Width * PatternTile;
                            if (BossEffects.Stamps(mark.Fx, context, age, stamps))
                                foreach (var stamp in stamps)
                                {
                                    if (stamp.Shape == FxShape.Light) continue;
                                    if (stamp.Ground) MonsterFxPreview.Blit(cell, mx, my, stamp);
                                    else air.Add((mx, my, stamp));
                                }
                        }
                    }

                    // The serpent itself, turning round for the spiral.
                    var view = BossView.Side;
                    bool flip = false;
                    if (scene.Spin && since >= 0 && since < 1.2f)
                    {
                        var ways = new (BossView, bool)[] { (BossView.Side, false), (BossView.FrontSide, false), (BossView.Front, false), (BossView.FrontSide, true), (BossView.Side, true), (BossView.BackSide, true), (BossView.Back, false), (BossView.BackSide, false) };
                        (view, flip) = ways[(int)(time * 12) % 8];
                    }
                    else if (round && !scene.Entrance) view = BossView.FrontSide;
                    var key = (pose, frame, (int)view * 2 + (flip ? 1 : 0), scene.Phase);
                    if (!sprites.TryGetValue(key, out var sprite)) sprites[key] = sprite = Flip(SerpentArt.Draw(view, pose, frame, scene.Phase), flip);
                    int sx = (int)Math.Round(feetX - SerpentArt.Width / 2f), sy = (int)Math.Round(feetY - (SerpentArt.Height - SerpentArt.Floor));
                    for (int y = 0; y < sprite.Height; y++)
                        for (int x = 0; x < sprite.Width; x++)
                        {
                            var c = sprite[x, y];
                            if (c.A > 0 && cell.Inside(sx + x, sy + y)) cell.Blend(sx + x, sy + y, c);
                        }

                    // The bubbles: a ring of shots from its head, flying out (the spiral: waves of them, turning).
                    if (scene.Shots > 0 && since >= 0)
                    {
                        for (int wave = 0; wave < scene.Waves; wave++)
                        {
                            float flown = since - wave * 0.22f;
                            if (flown < 0 || flown > 0.9f) continue;
                            var muzzle = new List<FxStamp>();
                            if (BossEffects.Stamps(BossFx.Muzzle, new BossContext { Ramp = FxRamp.Cold, Size = 10, Count = scene.Shots, Width = 360, Dir = 20 + wave * 24, Seed = 4 + wave }, flown, muzzle))
                                foreach (var stamp in muzzle) if (stamp.Shape != FxShape.Light) air.Add((feetX, feetY - 26, stamp));
                            for (int i = 0; i < scene.Shots; i++)
                            {
                                double a = (i * 360.0 / scene.Shots + (scene.Spin ? wave * 24 : 20)) * Math.PI / 180;
                                float far = 10 + flown * 7 * PatternTile;
                                int bx = (int)(feetX + Math.Cos(a) * far), by = (int)(feetY - 26 - Math.Sin(a) * far * 0.8f);
                                var shot = ShotArt.Shot(ShotElement.Water, ShotArt.Reach(0.3f), ShotArt.Step((float)a), (int)(time * 12) & 1);
                                for (int y = 0; y < shot.Height; y++)
                                    for (int x = 0; x < shot.Width; x++)
                                    {
                                        var c = shot[x, y];
                                        int px = bx - shot.Width / 2 + x, py = by - shot.Height / 2 + y;
                                        if (c.A > 0 && cell.Inside(px, py)) cell.Blend(px, py, c);
                                    }
                            }
                        }
                    }
                    foreach (var (x, y, stamp) in air) MonsterFxPreview.Blit(cell, x, y, stamp);

                    int cx = s % columns, cy = s / columns;
                    for (int y = 0; y < cellHeight * scale; y++)
                        for (int x = 0; x < cellWidth * scale; x++)
                        {
                            bool edge = x < 2 || y < 2;
                            var c = edge ? new Rgba(16, 14, 20) : cell[x / scale, y / scale];
                            pixels[(cy * cellHeight * scale + y) * width + cx * cellWidth * scale + x] = c.A == 255 ? c : c.WithAlpha(255);
                        }
                }
                frames.Add((pixels, 4));
            }
            Gif.Save(Path.Combine(folder, "serpent_patterns.gif"), width, height, frames);
            Console.WriteLine("serpent patterns: anim/serpent_patterns.gif (" + string.Join(", ", scenes.Select(s => s.Name)) + ")");
        }
    }
}
