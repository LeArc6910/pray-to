using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Client
{
    // The crown wraith's whole pattern in one picture (2026-11-28, crown_wraith_patterns.gif): a cell for each attack and
    // the entrance, each playing as the game does, the body's wind-up with the floor warning filling under it, the blow
    // as the warning fills, the warning's shape burning and the wraith's own lightning over it, then back to rest; the
    // rush slides across its cell leaving afterimages. Lengths are cut down to fit the cells, times are the sheet's own
    // telegraphs.
    static partial class Motions
    {
        sealed class WraithMark
        {
            public HazardForm Form;
            public float Size, Width, Degrees, X, Y, Delay;
            public WarnTone Tone = WarnTone.Danger;
            public BossFx Fx;
            public BossContext Context;
        }

        sealed class WraithScene
        {
            public string Name;
            public CrownWraithPose Blow;
            public float Telegraph;
            public List<WraithMark> Marks = new List<WraithMark>();
            public bool Rush, Entrance, Roar;
            public int Phase = 1;
        }


        static List<WraithScene> WraithScenes()
        {
            var bolt = FxRamp.Bolt;
            WraithMark Mark(HazardForm form, float size, float width, float degrees, float x, float y, BossFx fx, float delay = 0, WarnTone tone = WarnTone.Danger, bool big = false, float inner = 0, bool plain = false)
            {
                var context = new BossContext
                {
                    Ramp = bolt, Size = size * PatternTile, Width = form == HazardForm.Cone ? width : width * PatternTile, Dir = degrees, Inner = inner * PatternTile,
                    Big = big, Plain = plain, Seed = (int)(x * 7 + y * 13 + size * 3), Form = form,
                };
                return new WraithMark { Form = form, Size = size, Width = width, Degrees = degrees, X = x, Y = y, Delay = delay, Tone = tone, Fx = fx, Context = context };
            }
            var scenes = new List<WraithScene>
            {
                new WraithScene { Name = "enter", Entrance = true },
                new WraithScene { Name = "grasp", Blow = CrownWraithPose.Rake, Telegraph = 0.6f, Marks = { Mark(HazardForm.Cone, 3.4f, 110, 0, 0.3f, 0, BossFx.Talons) } },
                new WraithScene
                {
                    Name = "lightning_fall", Blow = CrownWraithPose.Call, Telegraph = 1.0f,
                    Marks = { Mark(HazardForm.Circle, 1.5f, 0, 0, 3.6f, 1.6f, BossFx.Thunder, 0), Mark(HazardForm.Circle, 1.5f, 0, 0, 6.4f, 0.4f, BossFx.Thunder, 0.06f),
                        Mark(HazardForm.Circle, 1.5f, 0, 0, 4.4f, -1.8f, BossFx.Thunder, 0.12f), Mark(HazardForm.Circle, 1.5f, 0, 0, 1.6f, -2.2f, BossFx.Thunder, 0.18f) },
                },
                new WraithScene { Name = "soul_lance", Blow = CrownWraithPose.Hurl, Telegraph = 0.7f, Marks = { Mark(HazardForm.Band, 7.5f, 1.2f, 0, 0.5f, 0, BossFx.Lance) } },
                new WraithScene { Name = "phantom_rush", Blow = CrownWraithPose.Rush, Telegraph = 0.8f, Phase = 2, Rush = true, Marks = { Mark(HazardForm.Band, 7.5f, 2.2f, 0, 0.5f, 0, BossFx.Cinders, 99) } },
                new WraithScene { Name = "crown_ring", Blow = CrownWraithPose.Flare, Telegraph = 1.0f, Phase = 2, Marks = { Mark(HazardForm.Donut, 4.2f, 2.0f, 0, 0, 0, BossFx.ArcRing, inner: 2.0f) } },
                new WraithScene
                {
                    Name = "storm_brand", Blow = CrownWraithPose.Brand, Telegraph = 1.4f, Phase = 3,
                    Marks = { Mark(HazardForm.Circle, 1.4f, 0, 0, 3.6f, 1.4f, BossFx.Thunder, 0, big: true), Mark(HazardForm.Circle, 1.4f, 0, 0, 5.4f, -0.6f, BossFx.Thunder, 0.05f, big: true),
                        Mark(HazardForm.Circle, 1.4f, 0, 0, 2.0f, -2.0f, BossFx.Thunder, 0.1f, big: true) },
                },
                new WraithScene { Name = "deep_share", Blow = CrownWraithPose.Smite, Telegraph = 1.1f, Phase = 2, Marks = { Mark(HazardForm.Circle, 1.8f, 0, 0, 4.2f, 0, BossFx.Thunder, 0, WarnTone.Gather, true) } },
                new WraithScene { Name = "soul_ripple", Blow = CrownWraithPose.Pulse, Telegraph = 0.5f, Phase = 2, Marks = { Mark(HazardForm.Donut, 4.4f, 1.2f, 0, 2.6f, 0, BossFx.ArcRing, inner: 1.2f, plain: true) } },
                new WraithScene { Name = "roar", Roar = true, Phase = 3 },
            };
            return scenes;
        }

        // The pose and frame at so many seconds into a scene's loop, and how far into its blow it is (seconds since
        // the strike, below 0 before it).
        static (CrownWraithPose Pose, int Frame, float Since) WraithAt(WraithScene scene, float time)
        {
            const float idle = 0.4f;
            if (scene.Entrance)
            {
                float length = 3.8f;
                if (time < 0.3f) return (CrownWraithPose.Enter, 0, -1);
                float progress = (time - 0.3f) / length;
                return progress >= 1 ? (CrownWraithPose.Idle, (int)(time * 5) % CrownWraithArt.FramesOf(CrownWraithPose.Idle), -1) : (CrownWraithPose.Enter, CrownWraithArt.EnterFrameAt(progress, false), -1);
            }
            if (scene.Roar)
            {
                if (time < idle) return (CrownWraithPose.Idle, (int)(time * 5) % CrownWraithArt.FramesOf(CrownWraithPose.Idle), -1);
                int rise = (int)((time - idle) / 0.06f);
                if (time > idle + 2.2f) return (CrownWraithPose.Idle, (int)(time * 5) % CrownWraithArt.FramesOf(CrownWraithPose.Idle), -1);
                return (CrownWraithPose.Roar, rise < CrownWraithArt.RoarRise ? rise : CrownWraithArt.RoarRise + (int)(time * 10) % 4, -1);
            }
            if (time < idle) return (CrownWraithPose.Idle, (int)(time * 5) % CrownWraithArt.FramesOf(CrownWraithPose.Idle), -1);
            float t = time - idle;
            var blow = scene.Blow;
            var windUp = CrownWraithArt.WindUpOf(blow);
            int strike = CrownWraithArt.StrikeFrame(blow);
            if (t < scene.Telegraph)
            {
                float progress = t / scene.Telegraph;
                float swing = strike * CrownWraithArt.FrameSeconds(blow, 0);
                float wound = 1 - Math.Min(0.5f, swing / scene.Telegraph);
                if (progress < wound) return (windUp, Math.Min(CrownWraithArt.FramesOf(windUp) - 1, (int)(progress / wound * CrownWraithArt.FramesOf(windUp))), t - scene.Telegraph);
                float into = (progress - wound) * scene.Telegraph;
                int f = 0;
                for (; f < strike; f++)
                {
                    into -= CrownWraithArt.FrameSeconds(blow, f);
                    if (into < 0) break;
                }
                return (blow, Math.Min(strike - 1, f), t - scene.Telegraph);
            }
            float since = t - scene.Telegraph, left = since;
            for (int k = strike; k < CrownWraithArt.FramesOf(blow); k++)
            {
                left -= CrownWraithArt.FrameSeconds(blow, k);
                if (left < 0) return (blow, k, since);
            }
            return (CrownWraithPose.Idle, (int)(time * 5) % CrownWraithArt.FramesOf(CrownWraithPose.Idle), since);
        }

        static void WraithPatterns(string folder, PixelImage arena)
        {
            var scenes = WraithScenes();
            const int cellWidth = 232, cellHeight = 176, columns = 4, scale = 2;
            int rows = (scenes.Count + columns - 1) / columns;
            int width = cellWidth * columns * scale, height = cellHeight * rows * scale;
            const float loop = 4.4f, step = 0.04f;
            var frames = new List<(Rgba[] Pixels, int Centiseconds)>();
            var sprites = new Dictionary<(CrownWraithPose, int, int, int), PixelImage>();
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
                    // It floats a little left of the middle for an attack ahead of it, in the middle for one round it.
                    bool round = scene.Name == "crown_ring" || scene.Entrance || scene.Roar;
                    float feetX = round ? cellWidth / 2f : 52, feetY = cellHeight * 0.66f;
                    var (pose, frame, since) = WraithAt(scene, time);
                    float startX = feetX;
                    // The rush: thrown across the cell down its mark.
                    float Travel(float at) => at < 0 ? 0 : 7.2f * PatternTile * (float)Math.Min(1, Math.Sqrt(at / 0.3f));
                    if (scene.Rush) feetX += Travel(since);
                    float telegraphing = since < 0 && !scene.Entrance && !scene.Roar && time >= 0.4f ? 1 + since / scene.Telegraph : -1;

                    var air = new List<(float X, float Y, FxStamp Stamp)>();
                    foreach (var mark in scene.Marks)
                    {
                        float mx = startX + mark.X * PatternTile, my = feetY - mark.Y * PatternTile;
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
                            show = HazardArt.Blast(field, mark.Tone == WarnTone.Gather ? HazardArt.Tone(WarnTone.Gather) : SkillFxArt.Ramp5(FxRamp.Bolt), age, 3, buffer, false);
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
                        if (age >= 0 && mark.Delay < 90)
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

                    // The wraith itself, and behind its rush pale afterimages of it.
                    var view = round && !scene.Entrance ? BossView.FrontSide : BossView.Side;
                    bool flip = false;
                    if (scene.Rush && since >= 0 && since < 0.7f)
                    {
                        var ghost = CrownWraithArt.Draw(view, CrownWraithPose.Rush, CrownWraithArt.StrikeFrame(CrownWraithPose.Rush), scene.Phase).Silhouette();
                        for (int k = 1; k <= 4; k++)
                        {
                            float back = since - k * 0.05f;
                            if (back < 0) continue;
                            int gx = (int)Math.Round(startX + Travel(back) - CrownWraithArt.Width / 2f), gy = (int)Math.Round(feetY - (CrownWraithArt.Height - CrownWraithArt.Floor));
                            for (int y = 0; y < ghost.Height; y++)
                                for (int x = 0; x < ghost.Width; x++)
                                    if (ghost[x, y].A > 0 && cell.Inside(gx + x, gy + y)) cell.Blend(gx + x, gy + y, new Rgba(200, 255, 236, (int)(110 * (1 - since / 0.7f) * (1 - k * 0.18f))));
                        }
                    }
                    var key = (pose, frame, (int)view * 2 + (flip ? 1 : 0), scene.Phase);
                    if (!sprites.TryGetValue(key, out var sprite)) sprites[key] = sprite = Flip(CrownWraithArt.Draw(view, pose, frame, scene.Phase), flip);
                    int sx = (int)Math.Round(feetX - CrownWraithArt.Width / 2f), sy = (int)Math.Round(feetY - (CrownWraithArt.Height - CrownWraithArt.Floor));
                    for (int y = 0; y < sprite.Height; y++)
                        for (int x = 0; x < sprite.Width; x++)
                        {
                            var c = sprite[x, y];
                            if (c.A > 0 && cell.Inside(sx + x, sy + y)) cell.Blend(sx + x, sy + y, c);
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
            Gif.Save(Path.Combine(folder, "crown_wraith_patterns.gif"), width, height, frames);
            Console.WriteLine("crown wraith patterns: anim/crown_wraith_patterns.gif (" + string.Join(", ", scenes.Select(s => s.Name)) + ")");
        }
    }
}
