using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Client
{
    // The void lord's whole pattern in one picture (2026-11-28, void_lord_patterns.gif): a cell for each attack and the
    // entrance, each playing as the game does, the body's wind-up with the floor warning filling under it, the blow as
    // the warning fills, the warning's shape burning and his own void and frost over it, then back to rest. Lengths
    // are cut down to fit the cells (the reach of the beams and the rings), times are the sheet's own telegraphs.
    static partial class Motions
    {
        sealed class VoidLordMark
        {
            public HazardForm Form;
            public float Size, Width, Degrees, X, Y, Delay;
            public WarnTone Tone = WarnTone.Danger;
            public BossFx Fx;
            public BossContext Context;
        }

        sealed class VoidLordScene
        {
            public string Name;
            public VoidLordPose Blow;
            public float Telegraph;
            public List<VoidLordMark> Marks = new List<VoidLordMark>();
            // Shots let fly in a ring from his orb, and how many waves of them.
            public int Shots, Waves;
            public bool Spin, Entrance, Roar;
            public int Phase = 1;
        }

        static List<VoidLordScene> VoidLordScenes()
        {
            var cold = FxRamp.Cold;
            VoidLordMark Mark(HazardForm form, float size, float width, float degrees, float x, float y, BossFx fx, float delay = 0, WarnTone tone = WarnTone.Danger, bool big = false, float inner = 0)
            {
                var context = new BossContext
                {
                    Ramp = cold, Size = size * PatternTile, Width = form == HazardForm.Cone ? width : width * PatternTile, Dir = degrees, Inner = inner * PatternTile,
                    Big = big, Seed = (int)(x * 7 + y * 13 + size * 3), Form = form,
                };
                return new VoidLordMark { Form = form, Size = size, Width = width, Degrees = degrees, X = x, Y = y, Delay = delay, Tone = tone, Fx = fx, Context = context };
            }
            var scenes = new List<VoidLordScene>
            {
                new VoidLordScene { Name = "enter", Entrance = true },
                new VoidLordScene { Name = "grasp", Blow = VoidLordPose.Grasp, Telegraph = 0.8f, Marks = { Mark(HazardForm.Cone, 3.8f, 110, 0, 0.3f, 0, BossFx.VoidClaw) } },
                new VoidLordScene { Name = "beam", Blow = VoidLordPose.Cast, Telegraph = 0.9f, Marks = { Mark(HazardForm.Band, 7.5f, 1.6f, 0, 0.5f, 0, BossFx.Rift) } },
                new VoidLordScene { Name = "void_ring", Blow = VoidLordPose.Burst, Telegraph = 0.6f, Shots = 20, Waves = 1 },
                new VoidLordScene
                {
                    Name = "star_fall", Blow = VoidLordPose.Starfall, Telegraph = 1.2f, Phase = 2,
                    Marks = { Mark(HazardForm.Circle, 1.4f, 0, 0, 2.4f, 1.4f, BossFx.Comet, 0), Mark(HazardForm.Circle, 1.4f, 0, 0, 5.2f, -0.6f, BossFx.Comet, 0.12f),
                        Mark(HazardForm.Circle, 1.4f, 0, 0, -3.6f, 1.2f, BossFx.Comet, 0.24f), Mark(HazardForm.Circle, 1.4f, 0, 0, -2f, -1.8f, BossFx.Comet, 0.36f) },
                },
                new VoidLordScene { Name = "collapse", Blow = VoidLordPose.Crush, Telegraph = 1.0f, Phase = 2, Marks = { Mark(HazardForm.Circle, 2.2f, 0, 0, 4f, 0, BossFx.Singularity) } },
                new VoidLordScene
                {
                    Name = "void_cross", Blow = VoidLordPose.Smite, Telegraph = 1.0f, Phase = 2,
                    Marks = { Mark(HazardForm.Band, 4.5f, 1.4f, 45, 0, 0, BossFx.Rift), Mark(HazardForm.Band, 4.5f, 1.4f, 135, 0, 0, BossFx.Rift),
                        Mark(HazardForm.Band, 4.5f, 1.4f, 225, 0, 0, BossFx.Rift), Mark(HazardForm.Band, 4.5f, 1.4f, 315, 0, 0, BossFx.Rift) },
                },
                new VoidLordScene { Name = "void_lance", Blow = VoidLordPose.Cast, Telegraph = 1.0f, Phase = 2, Marks = { Mark(HazardForm.Band, 7.5f, 1.6f, 0, 0.5f, 0, BossFx.Rift, big: true) } },
                new VoidLordScene { Name = "gravity_share", Blow = VoidLordPose.Crush, Telegraph = 1.4f, Phase = 2, Marks = { Mark(HazardForm.Circle, 2f, 0, 0, 4.2f, 0, BossFx.Singularity, 0, WarnTone.Gather, true) } },
                new VoidLordScene { Name = "event_horizon", Blow = VoidLordPose.Smite, Telegraph = 1.3f, Phase = 3, Marks = { Mark(HazardForm.Donut, 5.5f, 3f, 0, 0, 0, BossFx.Horizon, inner: 3f) } },
                new VoidLordScene
                {
                    Name = "black_holes", Blow = VoidLordPose.Crush, Telegraph = 1.4f, Phase = 3,
                    Marks = { Mark(HazardForm.Circle, 1.6f, 0, 0, 3.4f, 1.4f, BossFx.Singularity, 0, big: true), Mark(HazardForm.Circle, 1.6f, 0, 0, 5.4f, -1f, BossFx.Singularity, 0.1f, big: true),
                        Mark(HazardForm.Circle, 1.6f, 0, 0, 1.8f, -2f, BossFx.Singularity, 0.2f, big: true) },
                },
                new VoidLordScene { Name = "void_spiral", Blow = VoidLordPose.Burst, Telegraph = 0.7f, Phase = 3, Shots = 8, Waves = 5, Spin = true },
                new VoidLordScene { Name = "void_storm", Blow = VoidLordPose.Burst, Telegraph = 0.6f, Phase = 3, Shots = 32, Waves = 1 },
                new VoidLordScene { Name = "void_ripple", Blow = VoidLordPose.Crush, Telegraph = 0.5f, Phase = 2, Marks = { Mark(HazardForm.Donut, 4.4f, 1.2f, 0, 2.6f, 0, BossFx.Horizon, inner: 1.2f) } },
                new VoidLordScene { Name = "call_wisps", Roar = true, Phase = 3 },
            };
            return scenes;
        }

        // The pose and frame at so many seconds into a scene's loop, and how far into its blow it is (seconds since
        // the strike, below 0 before it).
        static (VoidLordPose Pose, int Frame, float Since) VoidLordAt(VoidLordScene scene, float time)
        {
            const float idle = 0.4f;
            if (scene.Entrance)
            {
                int frames = VoidLordArt.FramesOf(VoidLordPose.Enter);
                float length = 3.8f;
                if (time < 0.3f) return (VoidLordPose.Enter, 0, -1);
                float progress = (time - 0.3f) / length;
                return progress >= 1 ? (VoidLordPose.Idle, (int)(time * 5) % VoidLordArt.FramesOf(VoidLordPose.Idle), -1) : (VoidLordPose.Enter, VoidLordArt.EnterFrameAt(progress, false), -1);
            }
            if (scene.Roar)
            {
                if (time < idle) return (VoidLordPose.Idle, (int)(time * 5) % VoidLordArt.FramesOf(VoidLordPose.Idle), -1);
                int rise = (int)((time - idle) / 0.06f);
                if (time > idle + 2.2f) return (VoidLordPose.Idle, (int)(time * 5) % VoidLordArt.FramesOf(VoidLordPose.Idle), -1);
                return (VoidLordPose.Roar, rise < VoidLordArt.RoarRise ? rise : VoidLordArt.RoarRise + (int)(time * 10) % 4, -1);
            }
            if (time < idle) return (VoidLordPose.Idle, (int)(time * 5) % VoidLordArt.FramesOf(VoidLordPose.Idle), -1);
            float t = time - idle;
            var blow = scene.Blow;
            var windUp = VoidLordArt.WindUpOf(blow);
            int strike = VoidLordArt.StrikeFrame(blow);
            if (t < scene.Telegraph)
            {
                float progress = t / scene.Telegraph;
                float swing = strike * VoidLordArt.FrameSeconds(blow, 0);
                float wound = 1 - Math.Min(0.5f, swing / scene.Telegraph);
                if (progress < wound) return (windUp, Math.Min(VoidLordArt.FramesOf(windUp) - 1, (int)(progress / wound * VoidLordArt.FramesOf(windUp))), t - scene.Telegraph);
                float into = (progress - wound) * scene.Telegraph;
                int f = 0;
                for (; f < strike; f++)
                {
                    into -= VoidLordArt.FrameSeconds(blow, f);
                    if (into < 0) break;
                }
                return (blow, Math.Min(strike - 1, f), t - scene.Telegraph);
            }
            float since = t - scene.Telegraph, left = since;
            for (int k = strike; k < VoidLordArt.FramesOf(blow); k++)
            {
                left -= VoidLordArt.FrameSeconds(blow, k);
                if (left < 0) return (blow, k, since);
            }
            return (VoidLordPose.Idle, (int)(time * 5) % VoidLordArt.FramesOf(VoidLordPose.Idle), since);
        }

        static void VoidLordPatterns(string folder, PixelImage arena)
        {
            var scenes = VoidLordScenes();
            const int cellWidth = 232, cellHeight = 176, columns = 4, scale = 2;
            int rows = (scenes.Count + columns - 1) / columns;
            int width = cellWidth * columns * scale, height = cellHeight * rows * scale;
            const float loop = 4.4f, step = 0.04f;
            var frames = new List<(Rgba[] Pixels, int Centiseconds)>();
            var sprites = new Dictionary<(VoidLordPose, int, int, int), PixelImage>();
            var shotColors = ShotArt.Colors(ShotElement.Void);
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
                    // He hovers a little left of the middle for an attack ahead of him, in the middle for one round him.
                    bool round = scene.Name == "event_horizon" || scene.Name == "void_cross" || scene.Name == "void_ring" || scene.Name == "void_spiral" || scene.Name == "void_storm" || scene.Name == "star_fall" || scene.Entrance || scene.Roar;
                    float feetX = round ? cellWidth / 2f : 52, feetY = cellHeight * 0.62f;
                    var (pose, frame, since) = VoidLordAt(scene, time);
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
                            show = HazardArt.Blast(field, mark.Tone == WarnTone.Gather ? HazardArt.Tone(WarnTone.Gather) : SkillFxArt.Ramp5(FxRamp.Violet), age, 3, buffer, false);
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

                    // The void lord himself, turning round for the spiral.
                    var view = BossView.Side;
                    bool flip = false;
                    if (scene.Spin && since >= 0 && since < 1.2f)
                    {
                        var ways = new (BossView, bool)[] { (BossView.Side, false), (BossView.FrontSide, false), (BossView.Front, false), (BossView.FrontSide, true), (BossView.Side, true), (BossView.BackSide, true), (BossView.Back, false), (BossView.BackSide, false) };
                        (view, flip) = ways[(int)(time * 12) % 8];
                    }
                    else if (round && !scene.Entrance) view = BossView.FrontSide;
                    var key = (pose, frame, (int)view * 2 + (flip ? 1 : 0), scene.Phase);
                    if (!sprites.TryGetValue(key, out var sprite)) sprites[key] = sprite = Flip(VoidLordArt.Draw(view, pose, frame, scene.Phase), flip);
                    int sx = (int)Math.Round(feetX - VoidLordArt.Width / 2f), sy = (int)Math.Round(feetY - (VoidLordArt.Height - VoidLordArt.Floor));
                    for (int y = 0; y < sprite.Height; y++)
                        for (int x = 0; x < sprite.Width; x++)
                        {
                            var c = sprite[x, y];
                            if (c.A > 0 && cell.Inside(sx + x, sy + y)) cell.Blend(sx + x, sy + y, c);
                        }

                    // The shots: a ring of them from his orb, flying out (the spiral: waves of them, turning).
                    if (scene.Shots > 0 && since >= 0)
                    {
                        for (int wave = 0; wave < scene.Waves; wave++)
                        {
                            float flown = since - wave * 0.22f;
                            if (flown < 0 || flown > 0.9f) continue;
                            var muzzle = new List<FxStamp>();
                            if (BossEffects.Stamps(BossFx.Muzzle, new BossContext { Ramp = FxRamp.Violet, Size = 10, Count = scene.Shots, Width = 360, Dir = 20 + wave * 24, Seed = 4 + wave }, flown, muzzle))
                                foreach (var stamp in muzzle) if (stamp.Shape != FxShape.Light) air.Add((feetX, feetY - 40, stamp));
                            for (int i = 0; i < scene.Shots; i++)
                            {
                                double a = (i * 360.0 / scene.Shots + (scene.Spin ? wave * 24 : 20)) * Math.PI / 180;
                                float far = 10 + flown * 7 * PatternTile;
                                int bx = (int)(feetX + Math.Cos(a) * far), by = (int)(feetY - 40 - Math.Sin(a) * far * 0.8f);
                                var shot = ShotArt.Shot(ShotElement.Void, ShotArt.Reach(0.3f), ShotArt.Step((float)a), (int)(time * 12) & 1);
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
            Gif.Save(Path.Combine(folder, "void_lord_patterns.gif"), width, height, frames);
            Console.WriteLine("void lord patterns: anim/void_lord_patterns.gif (" + string.Join(", ", scenes.Select(s => s.Name)) + ")");
        }
    }
}
