using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // Animated GIFs of the player's and the bosses' motions (2026-11-09), played the way WorldView plays them: the
    // same frames, timings, weapon and bottle positions and turns, lunges and glows, at a fixed 50 frames a second,
    // so the motions can be watched without Unity. Keep the numbers here in step with WorldView. Effects (the swing's
    // arc, rings, sparkles) are left out: they reach past the cells and are drawn in effects.png.
    //
    //   anim/player_turn.gif     eight ways round a compass, walking then standing, a sword in hand; in the middle
    //                            one player turns through all eight
    //   anim/player_motions.gif  a row a motion, a column a way faced (down, down-right, right, up-right, up):
    //                            sword combo, bow, staff, potion, food, skill thrown forward, skill all round, buff
    //                            cast, hit
    //   anim/bosses.gif          a row a boss: turning through eight ways, then its pattern facing down-right and
    //                            facing up-left (idle, wind-up, strike, charge, roar)
    static class Motions
    {
        const float Step = 0.02f;
        const int Ppu = TileArt.Size;

        // WorldView's timings.
        const float SwingSeconds = 0.15f;
        const float FlashSeconds = 0.12f;
        const float LungeSeconds = 0.14f;
        const float StrikeSeconds = 0.24f;
        const float WindUpSeconds = 0.07f;
        const float CastSeconds = 0.3f;
        const float ShootSeconds = 0.3f;
        const float DrinkSeconds = 0.6f;
        const float ThrustSeconds = 0.32f;
        const float SpinSeconds = 0.36f;
        const float HurtSeconds = 0.25f;
        const float KnockSeconds = 0.12f;
        const float BreathSeconds = 0.8f;
        const float BlinkEvery = 3.4f;
        const float BlinkSeconds = 0.13f;
        const float LungeDistance = 2f / Ppu;

        enum Motion
        {
            None,
            Shoot,
            Zap,
            Drink,
            Thrust,
            Spin,
        }

        static readonly (Look Look, bool Left)[] Sectors =
        {
            (Look.Side, false), (Look.UpSide, false), (Look.Up, false), (Look.UpSide, true),
            (Look.Side, true), (Look.DownSide, true), (Look.Down, false), (Look.DownSide, false),
        };

        static readonly (BossView View, bool Left)[] BossSectors =
        {
            (BossView.Side, false), (BossView.BackSide, false), (BossView.Back, false), (BossView.BackSide, true),
            (BossView.Side, true), (BossView.FrontSide, true), (BossView.Front, false), (BossView.FrontSide, false),
        };

        static int SectorOf(Vec2 facing) =>
            facing.LengthSquared < 1e-6f ? 6 : ((int)MathF.Round(MathF.Atan2(facing.Y, facing.X) / (MathF.PI / 4)) % 8 + 8) % 8;

        static Vec2 Way(int sector) => new Vec2(MathF.Cos(sector * MathF.PI / 4), MathF.Sin(sector * MathF.PI / 4));

        // ---- Drawing: sprites placed, turned and scaled the way Unity draws them, pixels sampled point-filtered ----

        struct Draw
        {
            public PixelImage Image;
            public float X;
            public float Y;
            public float PivotX;
            public float PivotY;
            public bool FlipX;
            public float Angle;
            public float ScaleX;
            public float ScaleY;
            public Rgba Tint;
            public int Order;
        }

        static Draw Sprite(PixelImage image, float x, float y, float pivotX, float pivotY, int order, bool flip = false, float angle = 0, float scaleX = 1,
            float scaleY = 1, Rgba? tint = null) => new Draw
        {
            Image = image, X = x, Y = y, PivotX = pivotX, PivotY = pivotY, FlipX = flip, Angle = angle, ScaleX = scaleX, ScaleY = scaleY,
            Tint = tint ?? Rgba.White, Order = order,
        };

        // A cell of the picture: world units round its centre, drawn at Scale screen pixels a sprite pixel.
        sealed class Cell
        {
            public Cell(Rgba[] pixels, int stride, int left, int top, int width, int height, int scale, float centreY)
            {
                Pixels = pixels;
                Stride = stride;
                Left = left;
                Top = top;
                Width = width;
                Height = height;
                Scale = scale;
                CentreY = centreY;
            }

            public Rgba[] Pixels { get; }
            public int Stride { get; }
            public int Left { get; }
            public int Top { get; }
            public int Width { get; }
            public int Height { get; }
            public int Scale { get; }
            // Where the world's origin sits from the top, as a share of the height.
            public float CentreY { get; }

            float Unit => Scale * Ppu;

            public void Floor(PixelImage tile)
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        Pixels[(Top + y) * Stride + Left + x] = tile[x / Scale % tile.Width, y / Scale % tile.Height];
                    }
                }
            }

            public void Render(IEnumerable<Draw> draws)
            {
                foreach (var draw in draws.OrderBy(d => d.Order)) Render(draw);
            }

            void Render(Draw draw)
            {
                float cos = MathF.Cos(draw.Angle * MathF.PI / 180);
                float sin = MathF.Sin(draw.Angle * MathF.PI / 180);
                float ox = Width / 2f;
                float oy = Height * CentreY;
                // The corners on screen, for the box to fill.
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                foreach (var (u, v) in new[] { (0f, 0f), (1f, 0f), (0f, 1f), (1f, 1f) })
                {
                    float lx = (u - draw.PivotX) * draw.Image.Width / (float)Ppu * draw.ScaleX;
                    float ly = (v - draw.PivotY) * draw.Image.Height / (float)Ppu * draw.ScaleY;
                    float wx = draw.X + lx * cos - ly * sin;
                    float wy = draw.Y + lx * sin + ly * cos;
                    minX = MathF.Min(minX, ox + wx * Unit);
                    maxX = MathF.Max(maxX, ox + wx * Unit);
                    minY = MathF.Min(minY, oy - wy * Unit);
                    maxY = MathF.Max(maxY, oy - wy * Unit);
                }
                int x0 = Math.Max(0, (int)MathF.Floor(minX)), x1 = Math.Min(Width - 1, (int)MathF.Ceiling(maxX));
                int y0 = Math.Max(0, (int)MathF.Floor(minY)), y1 = Math.Min(Height - 1, (int)MathF.Ceiling(maxY));
                for (int sy = y0; sy <= y1; sy++)
                {
                    for (int sx = x0; sx <= x1; sx++)
                    {
                        float wx = (sx + 0.5f - ox) / Unit - draw.X;
                        float wy = (oy - sy - 0.5f) / Unit - draw.Y;
                        float lx = (wx * cos + wy * sin) / draw.ScaleX;
                        float ly = (-wx * sin + wy * cos) / draw.ScaleY;
                        if (draw.FlipX) lx = -lx;
                        int u = (int)MathF.Floor(lx * Ppu + draw.PivotX * draw.Image.Width);
                        int v = (int)MathF.Floor(ly * Ppu + draw.PivotY * draw.Image.Height);
                        if (u < 0 || v < 0 || u >= draw.Image.Width || v >= draw.Image.Height) continue;
                        var texel = draw.Image[u, draw.Image.Height - 1 - v];
                        int alpha = texel.A * draw.Tint.A / 255;
                        if (alpha <= 0) continue;
                        int at = (Top + sy) * Stride + Left + sx;
                        var under = Pixels[at];
                        Pixels[at] = new Rgba(
                            Mix(under.R, texel.R * draw.Tint.R / 255, alpha),
                            Mix(under.G, texel.G * draw.Tint.G / 255, alpha),
                            Mix(under.B, texel.B * draw.Tint.B / 255, alpha));
                    }
                }
            }

            static int Mix(int under, int over, int alpha) => (under * (255 - alpha) + over * alpha + 127) / 255;
        }

        // ---- A player, as WorldView's Body for one: the timers its events set and its frame-by-frame pose ----

        sealed class Player
        {
            public Vec2 Facing = new Vec2(0, -1);
            public ItemBase Weapon;
            public bool Moving;
            public float Walked;
            public float StrikeTime, StrikeLength, SwingTime, LungeTime, LungeReach = 1, CastTime, HurtTime, FlashTime;
            public float KnockTime, KnockReach = 1;
            public Vec2 Lunge, Knock;
            public int Combo;
            public Motion Motion;
            public float MotionTime, MotionLength;
            public int SpinFrom;
            public PixelImage Holding;

            void Start(Motion motion, float seconds, Vec2 facing)
            {
                Motion = motion;
                MotionTime = seconds;
                MotionLength = seconds;
                SpinFrom = SectorOf(facing);
                StrikeTime = 0;
            }

            // SimEventType.Attack for a player: the combo step, a swing or a shot.
            public void Attack(int step, BasicAttackKind kind)
            {
                bool finisher = step == World.ComboSteps - 1;
                Combo = step;
                LungeTime = LungeSeconds;
                LungeReach = finisher ? 2 : 1;
                Lunge = Facing;
                StrikeLength = finisher ? StrikeSeconds * 1.4f : StrikeSeconds;
                StrikeTime = StrikeLength;
                if (kind != BasicAttackKind.Swing)
                {
                    Start(kind == BasicAttackKind.Arrow ? Motion.Shoot : Motion.Zap, ShootSeconds, Facing);
                    return;
                }
                SwingTime = SwingSeconds;
            }

            public void Drink(PixelImage held)
            {
                Start(Motion.Drink, DrinkSeconds, new Vec2(0, 0));
                Holding = held;
            }

            public void Nova() => Start(Motion.Spin, SpinSeconds, Facing);

            bool Melee => Weapon == null || (Weapon.Tags & (SkillTag.Ranged | SkillTag.Magic)) == 0;

            public void Projectile()
            {
                Start(Motion.Thrust, ThrustSeconds, Facing);
                if (!Melee) return;
                Combo = 0;
                SwingTime = SwingSeconds;
            }

            public void Buff() => CastTime = CastSeconds;

            public void Hit(Vec2 from)
            {
                FlashTime = FlashSeconds;
                HurtTime = HurtSeconds;
                Knock = from;
                KnockTime = KnockSeconds;
                KnockReach = 1;
            }

            // WorldView.PlayerPose.
            (Pose, int) PoseNow(float time)
            {
                if (HurtTime > 0) return (Client.Pose.Hurt, 0);
                if (Motion != Motion.None && MotionTime > 0)
                {
                    int second = MotionTime < MotionLength * 0.45f ? 1 : 0;
                    return Motion switch
                    {
                        Motion.Shoot => (Client.Pose.Shoot, second),
                        Motion.Zap => (Client.Pose.Zap, second),
                        Motion.Drink => (Client.Pose.Drink, MotionTime < MotionLength * 0.6f ? 1 : 0),
                        Motion.Spin => (Client.Pose.Spread, 0),
                        _ => (Client.Pose.Thrust, 0),
                    };
                }
                if (StrikeTime > 0)
                {
                    bool finisher = Combo == World.ComboSteps - 1;
                    float length = StrikeLength > 0 ? StrikeLength : StrikeSeconds;
                    bool early = StrikeTime > length - (finisher ? WindUpSeconds * 2 : WindUpSeconds);
                    if (finisher && early) return (Client.Pose.Cast, 0);
                    return (Client.Pose.Strike, Combo == 1 ? (early ? 1 : 0) : (early ? 0 : 1));
                }
                if (CastTime > 0) return (Client.Pose.Cast, 0);
                if (Moving) return (Client.Pose.Walk, (int)MathF.Floor(Walked * 8) % CreatureArt.WalkFrames);
                if (time % BlinkEvery < BlinkSeconds) return (Client.Pose.Stand, CreatureArt.Blink);
                return (Client.Pose.Stand, (int)MathF.Floor(time / BreathSeconds) % 2 == 1 ? CreatureArt.Breath : 0);
            }

            // WorldView.Place, Animate and ShowWeapon for this frame, then the timers run down.
            public List<Draw> Frame(float time, float deltaTime)
            {
                const float radius = World.PlayerRadius;
                const float feet = radius * 0.8f;
                var draws = new List<Draw>();
                Walked = Moving ? Walked + deltaTime : 0;

                int sector = SectorOf(Facing);
                if (Motion == Motion.Spin && MotionTime > 0) sector = (SpinFrom + (int)MathF.Floor((1 - MotionTime / MotionLength) * 8)) % 8;
                var (look, left) = Sectors[sector];
                var (pose, frame) = PoseNow(time);
                var body = CreatureArt.Player(look, frame, Tunic, pose);

                float lean = LungeTime > 0 ? MathF.Sin(LungeTime / LungeSeconds * MathF.PI) * LungeDistance * LungeReach : 0;
                float knock = KnockTime > 0 ? MathF.Sin(KnockTime / KnockSeconds * MathF.PI) * LungeDistance * KnockReach : 0;
                float offsetX = Lunge.X * lean + Knock.X * knock;
                float offsetY = Lunge.Y * lean + Knock.Y * knock;
                KnockTime = MathF.Max(0, KnockTime - deltaTime);
                const int order = 10000;
                int shadowWidth = Math.Max(6, (int)MathF.Round(radius * 2 * Ppu * 0.9f));
                draws.Add(Sprite(CreatureArt.Shadow(shadowWidth), 0, -feet + 1f / Ppu, 0.5f, 0.5f, -30));
                draws.Add(Sprite(body, offsetX, -feet + offsetY, 0.5f, 0, order, left));
                if (FlashTime > 0) draws.Add(Sprite(body.Silhouette(), offsetX, -feet + offsetY, 0.5f, 0, order + 1, left, tint: new Rgba(255, 255, 255, 217)));
                LungeTime = MathF.Max(0, LungeTime - deltaTime);
                Held(draws, offsetX, offsetY, feet, order);

                SwingTime = MathF.Max(0, SwingTime - deltaTime);
                FlashTime = MathF.Max(0, FlashTime - deltaTime);
                StrikeTime = MathF.Max(0, StrikeTime - deltaTime);
                CastTime = MathF.Max(0, CastTime - deltaTime);
                HurtTime = MathF.Max(0, HurtTime - deltaTime);
                MotionTime = MathF.Max(0, MotionTime - deltaTime);
                if (MotionTime <= 0) Motion = Motion.None;
                return draws;
            }

            // WorldView.ShowWeapon.
            void Held(List<Draw> draws, float offsetX, float offsetY, float feet, int order)
            {
                var (look, left) = Sectors[SectorOf(Facing)];
                bool away = look == Look.Up || look == Look.UpSide;
                if (Motion == Motion.Drink && MotionTime > 0 && Holding != null)
                {
                    float tip = 20 + (120 - 20) * (1 - MotionTime / MotionLength);
                    bool mirrored = Facing.X < 0;
                    float side = away ? 0.22f : 0.2f;
                    draws.Add(Sprite(Holding, (mirrored ? -side : side) + offsetX, -feet + (away ? 0.6f : 0.52f) + offsetY, 0.5f, 0.5f, away ? order - 1 : order + 2,
                        mirrored, mirrored ? -tip : tip, 0.55f, 0.55f));
                    return;
                }
                if (Weapon == null) return;
                bool bow = (Weapon.Tags & SkillTag.Ranged) != 0;
                var icon = ItemArt.Gear(Weapon);
                float pivotX = bow ? 0.35f : 0.2f;
                float pivotY = bow ? 0.5f : 0.2f;
                float x = look == Look.Up ? -0.22f : look == Look.Down ? 0.24f : left ? -0.24f : 0.24f;
                if (look == Look.UpSide) x = left ? -0.2f : 0.2f;
                if (look == Look.DownSide) x = left ? -0.26f : 0.26f;
                int weaponOrder = away ? order - 1 : order + 2;
                bool aimed = MotionTime > 0 && (Motion == Motion.Shoot || Motion == Motion.Zap || Motion == Motion.Thrust && !Melee);
                if (aimed)
                {
                    float angle = MathF.Atan2(Facing.Y, Facing.X) * 180 / MathF.PI;
                    bool raised = Motion == Motion.Zap && MotionTime >= MotionLength * 0.45f;
                    float length = MathF.Sqrt(Facing.X * Facing.X + Facing.Y * Facing.Y);
                    float wayX = Facing.X / length, wayY = Facing.Y / length;
                    if (raised)
                    {
                        draws.Add(Sprite(icon, x + offsetX, -feet + 0.5f + offsetY, pivotX, pivotY, weaponOrder));
                    }
                    else if (bow)
                    {
                        draws.Add(Sprite(icon, wayX * 0.42f + offsetX, -feet + 0.5f + wayY * 0.38f + offsetY, pivotX, pivotY, weaponOrder, angle: angle + 180));
                    }
                    else
                    {
                        float aheadX = wayX, aheadY = wayY * 0.6f;
                        float ahead = MathF.Sqrt(aheadX * aheadX + aheadY * aheadY);
                        aheadX = aheadX / ahead * 0.28f;
                        aheadY = aheadY / ahead * 0.28f;
                        draws.Add(Sprite(icon, aheadX + offsetX, -feet + 0.42f + aheadY * 0.5f + offsetY, pivotX, pivotY, weaponOrder, angle: angle - 45));
                    }
                    return;
                }
                float through = SwingTime / SwingSeconds;
                float sweep = SwingTime <= 0 ? 0
                    : Combo == 1 ? Lerp(40, -80, through)
                    : Combo == World.ComboSteps - 1 ? Lerp(-115, 70, through)
                    : Lerp(-80, 40, through);
                draws.Add(Sprite(icon, x + offsetX, -feet + 0.3f + offsetY, pivotX, pivotY, weaponOrder, left, left ? -sweep : sweep));
            }
        }

        // ---- A boss, as WorldView draws one: its state from the snapshots, the strike after an attack lands ----

        sealed class Boss
        {
            public string Id;
            public float Radius;
            public Vec2 Facing = new Vec2(0, -1);
            public BossState State = BossState.Recover;
            public bool Moving;
            public float StrikeTime, LungeTime;
            public Vec2 Lunge;

            public void Attack()
            {
                LungeTime = LungeSeconds;
                Lunge = Facing;
                StrikeTime = StrikeSeconds;
            }

            public List<Draw> Frame(float time, float deltaTime)
            {
                float feet = Radius * 0.8f;
                var draws = new List<Draw>();
                int frame = (int)MathF.Floor(time * (Moving ? 4 : 1.5f)) % CreatureArt.IdleFrames;
                var (view, turned) = BossSectors[SectorOf(Facing)];
                var pose = StrikeTime > 0 ? BossPose.Strike
                    : State == BossState.Telegraph ? BossPose.Windup
                    : State == BossState.Charge ? BossPose.Charge
                    : State == BossState.Roar ? BossPose.Roar
                    : BossPose.Idle;
                var body = CreatureArt.Boss(Id, BossColor, frame, view, pose);
                // The glow WorldView lays over a boss for its state, and the roar's pulse.
                Rgba? glow = State switch
                {
                    BossState.Telegraph => new Rgba(255, 255, 255, (int)(255 * (0.25f + 0.2f * MathF.Sin(time * 20)))),
                    BossState.Charge => new Rgba(255, 153, 51, 128),
                    BossState.Roar => new Rgba(255, 77, 51, 115),
                    _ => null,
                };
                float scaleY = State == BossState.Roar ? 1 + 0.05f * MathF.Sin(time * 30) : 1;
                float lean = LungeTime > 0 ? MathF.Sin(LungeTime / LungeSeconds * MathF.PI) * LungeDistance : 0;
                float x = Lunge.X * lean, y = -feet + Lunge.Y * lean;
                int shadowWidth = Math.Max(6, (int)MathF.Round(Radius * 2 * Ppu * 0.9f));
                draws.Add(Sprite(CreatureArt.Shadow(shadowWidth), 0, -feet + 1f / Ppu, 0.5f, 0.5f, -30));
                draws.Add(Sprite(body, x, y, 0.5f, 0, 10000, turned, scaleY: scaleY));
                if (glow != null) draws.Add(Sprite(body.Silhouette(), x, y, 0.5f, 0, 10001, turned, scaleY: scaleY, tint: glow));
                LungeTime = MathF.Max(0, LungeTime - deltaTime);
                StrikeTime = MathF.Max(0, StrikeTime - deltaTime);
                return draws;
            }
        }

        static readonly Rgba Tunic = Rgba.Hex("#3F7FD8");
        static readonly Rgba BossColor = Rgba.Hex("#8C7A6B");

        static int Ticks(float seconds) => (int)MathF.Round(seconds / Step);

        static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0, 1);

        // ---- The pictures ----

        public static void Write(string folder, LootData loot, MonsterData monsters, NpcData npcs)
        {
            Directory.CreateDirectory(folder);
            var sword = loot.Bases.First(b => b.Id == "iron_sword");
            var bow = loot.Bases.First(b => b.Id == "iron_bow");
            var staff = loot.Bases.First(b => b.Id == "iron_staff");
            var potion = ItemArt.Consumable(loot.Consumables.First(c => c.Id == "healing_potion"));
            var food = ItemArt.Consumable(loot.Consumables.First(c => c.Id == "grilled_fish"));
            var floor = TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, 0));
            var arena = TileArt.Ground(TileArt.Index(GroundKind.Arena, 0, 0));

            PlayerTurn(Path.Combine(folder, "player_turn.gif"), sword, floor);
            PlayerMotions(Path.Combine(folder, "player_motions.gif"), sword, bow, staff, potion, food, floor);
            Bosses(Path.Combine(folder, "bosses.gif"), arena);
            Critters(Path.Combine(folder, "monsters.gif"), monsters, floor);
            People(Path.Combine(folder, "people.gif"), npcs, floor);
        }

        // Each person at their stall as WorldView draws them (2026-11-09): the stall, the person standing in it and the
        // counter over their legs; two loops.
        static void People(string path, NpcData npcs, PixelImage floor)
        {
            var list = npcs.Npcs.ToList();
            var counter = TileArt.MerchantCounter;
            Play(path, list.Count, 1, 26, 30, 5, 0.6f, 2 * PeopleArt.Frames / PeopleArt.FramesPerSecond, floor, (column, row, time, step) =>
            {
                var npc = list[column];
                int frame = (int)MathF.Floor(time * PeopleArt.FramesPerSecond) % PeopleArt.Frames;
                var person = PeopleArt.Person(npc.Id, Rgba.Hex(npc.Color), frame) ?? CreatureArt.Player(Look.Down, 0, Rgba.Hex(npc.Color), Pose.Stand);
                return new List<Draw>
                {
                    Sprite(TileArt.Object(npc.Tile), 0, 0, 0.5f, 0.5f, 0),
                    Sprite(person, 0, -0.2f, 0.5f, 0, 10),
                    Sprite(counter, 0, 0, 0.5f, 0.5f, 12),
                };
            });
        }

        // ---- A monster (2026-11-09), as WorldView draws one: two looks flipped for eight ways, and a pose ----

        sealed class Critter
        {
            public string Id;
            public Rgba Color;
            public float Radius;
            public int Seed;
            public Vec2 Facing = new Vec2(1, 0);
            public bool Moving;
            public EnemyState State;
            public float StrikeTime, LungeTime, HurtTime, FlashTime, KnockTime;
            public Vec2 Lunge, Knock;

            public void Attack()
            {
                LungeTime = LungeSeconds;
                Lunge = Facing;
                StrikeTime = StrikeSeconds;
            }

            public void Hit()
            {
                FlashTime = FlashSeconds;
                HurtTime = HurtSeconds;
                Knock = new Vec2(-Facing.X, -Facing.Y);
                KnockTime = KnockSeconds;
            }

            public List<Draw> Frame(float time, float deltaTime)
            {
                float feet = Radius * 0.8f;
                var draws = new List<Draw>();
                int frame = (int)MathF.Floor(time * (Moving ? 6 : 3) + Seed * 0.37f) % CreatureArt.IdleFrames;
                int sector = SectorOf(Facing);
                var view = sector == 1 || sector == 2 || sector == 3 ? MonsterView.Back : MonsterView.Side;
                bool left = Facing.X < 0;
                var pose = HurtTime > 0 ? MonsterPose.Hurt : StrikeTime > 0 ? MonsterPose.Strike : State == EnemyState.Windup ? MonsterPose.Windup : MonsterPose.Idle;
                var body = CreatureArt.Monster(Id, Color, frame, view, pose);
                float lean = LungeTime > 0 ? MathF.Sin(LungeTime / LungeSeconds * MathF.PI) * LungeDistance : 0;
                float knock = KnockTime > 0 ? MathF.Sin(KnockTime / KnockSeconds * MathF.PI) * LungeDistance : 0;
                float x = Lunge.X * lean + Knock.X * knock, y = -feet + Lunge.Y * lean + Knock.Y * knock;
                float scaleY = State == EnemyState.Windup ? 1 + 0.06f * MathF.Sin(time * 30) : 1;
                int shadowWidth = Math.Max(6, (int)MathF.Round(Radius * 2 * Ppu * 0.9f));
                draws.Add(Sprite(CreatureArt.Shadow(shadowWidth), 0, -feet + 1f / Ppu, 0.5f, 0.5f, -30));
                draws.Add(Sprite(body, x, y, 0.5f, 0, 10000, left, scaleY: scaleY));
                // The red glow of a wind-up, and the white flash of a hit.
                if (State == EnemyState.Windup)
                    draws.Add(Sprite(body.Silhouette(), x, y, 0.5f, 0, 10001, left, scaleY: scaleY, tint: new Rgba(255, 77, 51, (int)(255 * (0.35f + 0.25f * MathF.Sin(time * 30))))));
                if (FlashTime > 0) draws.Add(Sprite(body.Silhouette(), x, y, 0.5f, 0, 10002, left, tint: new Rgba(255, 255, 255, 217)));
                LungeTime = MathF.Max(0, LungeTime - deltaTime);
                StrikeTime = MathF.Max(0, StrikeTime - deltaTime);
                HurtTime = MathF.Max(0, HurtTime - deltaTime);
                FlashTime = MathF.Max(0, FlashTime - deltaTime);
                KnockTime = MathF.Max(0, KnockTime - deltaTime);
                return draws;
            }
        }

        // Every monster, seven a row: a second walking right, a second walking away (its back), half a second walking
        // left, a wind-up of 0.6 seconds and the strike, then a hit; 4.8 seconds a loop.
        static void Critters(string path, MonsterData monsters, PixelImage floor)
        {
            var list = monsters.Monsters.ToList();
            const int columns = 7;
            int rows = (list.Count + columns - 1) / columns;
            var critters = list.Select((m, i) => new Critter { Id = m.Id, Color = Rgba.Hex(m.Color), Radius = m.Radius, Seed = i }).ToList();
            Play(path, columns, rows, 26, 22, 4, 0.7f, 4.8f, floor, (column, row, time, step) =>
            {
                int index = row * columns + column;
                if (index >= critters.Count) return null;
                var c = critters[index];
                int tick = Ticks(time);
                c.Moving = time < 2.6f;
                c.Facing = time < 1.0f ? new Vec2(1, 0) : time < 2.0f ? new Vec2(0, 1) : new Vec2(-1, 0);
                c.State = time >= 2.6f && time < 3.2f ? EnemyState.Windup : EnemyState.Chase;
                if (tick == Ticks(3.2f)) c.Attack();
                if (tick == Ticks(3.8f)) c.Hit();
                return c.Frame(time, step);
            });
        }

        // Plays cells side by side for so long, each cell's actor stepping every frame.
        static void Play(string path, int columns, int rows, int cellWidth, int cellHeight, int scale, float centreY, float seconds, PixelImage tile,
            Func<int, int, float, float, List<Draw>> frameOf)
        {
            int width = columns * cellWidth * scale;
            int height = rows * cellHeight * scale;
            var frames = new List<(Rgba[], int)>();
            int count = (int)MathF.Round(seconds / Step);
            for (int tick = 0; tick < count; tick++)
            {
                float time = tick * Step;
                var pixels = new Rgba[width * height];
                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        var cell = new Cell(pixels, width, column * cellWidth * scale, row * cellHeight * scale, cellWidth * scale, cellHeight * scale, scale, centreY);
                        cell.Floor(tile);
                        var draws = frameOf(column, row, time, Step);
                        if (draws != null) cell.Render(draws);
                    }
                }
                // A dark line between cells.
                for (int row = 1; row < rows; row++) for (int x = 0; x < width; x++) pixels[row * cellHeight * scale * width + x] = Line;
                for (int column = 1; column < columns; column++) for (int y = 0; y < height; y++) pixels[y * width + column * cellWidth * scale] = Line;
                frames.Add((pixels, (int)MathF.Round(Step * 100)));
            }
            Gif.Save(path, width, height, frames);
        }

        static readonly Rgba Line = Rgba.Hex("#0A0A0F");

        // The eight ways round a compass: two seconds walking, two standing; in the middle a player turns, 0.4
        // seconds a way.
        static void PlayerTurn(string path, ItemBase sword, PixelImage floor)
        {
            // Compass cells: right, up-right, up, ... as grid places.
            var places = new (int Column, int Row)[] { (2, 1), (2, 0), (1, 0), (0, 0), (0, 1), (0, 2), (1, 2), (2, 2) };
            var players = new Player[3, 3];
            for (int sector = 0; sector < 8; sector++)
                players[places[sector].Column, places[sector].Row] = new Player { Facing = Way(sector), Weapon = sword };
            var turner = new Player { Weapon = sword };
            players[1, 1] = turner;
            Play(path, 3, 3, 32, 32, 5, 0.55f, 4f, floor, (column, row, time, step) =>
            {
                var player = players[column, row];
                if (player == turner) player.Facing = Way((6 + (int)(time / 0.4f)) % 8);
                else player.Moving = time < 2;
                return player.Frame(time, step);
            });
        }

        // A row a motion, a column a way faced. Each starts at 0.3 seconds into a 1.6-second loop; the sword strikes
        // its three steps 0.4 seconds apart.
        static void PlayerMotions(string path, ItemBase sword, ItemBase bow, ItemBase staff, PixelImage potion, PixelImage food, PixelImage floor)
        {
            int[] ways = { 6, 7, 0, 1, 2 };
            var weapons = new[] { sword, bow, staff, sword, sword, staff, sword, staff, sword };
            const int motions = 9;
            var players = new Player[ways.Length, motions];
            for (int column = 0; column < ways.Length; column++)
                for (int row = 0; row < motions; row++)
                    players[column, row] = new Player { Facing = Way(ways[column]), Weapon = weapons[row] };
            const float loop = 1.6f;
            Play(path, ways.Length, motions, 40, 34, 4, 0.55f, loop * 2, floor, (column, row, time, step) =>
            {
                var player = players[column, row];
                // On the frame that a moment of the loop falls on.
                bool At(float moment) => Ticks(time) % Ticks(loop) == Ticks(moment);
                if (row == 0)
                {
                    if (At(0.3f)) player.Attack(0, BasicAttackKind.Swing);
                    if (At(0.7f)) player.Attack(1, BasicAttackKind.Swing);
                    if (At(1.1f)) player.Attack(2, BasicAttackKind.Swing);
                }
                else if (At(0.3f))
                {
                    switch (row)
                    {
                        case 1: player.Attack(0, BasicAttackKind.Arrow); break;
                        case 2: player.Attack(0, BasicAttackKind.Bolt); break;
                        case 3: player.Drink(potion); break;
                        case 4: player.Drink(food); break;
                        case 5: player.Projectile(); break;
                        case 6: player.Nova(); break;
                        case 7: player.Buff(); break;
                        case 8: player.Hit(new Vec2(-player.Facing.X, -player.Facing.Y)); break;
                    }
                }
                return player.Frame(time, step);
            });
        }

        // A row a boss. First column: turning, 0.5 seconds a way. Second and third: the pattern facing down-right and
        // up-left: idle, a wind-up of a second, the strike, idle, a charge, idle, a roar, idle.
        static void Bosses(string path, PixelImage arena)
        {
            string[] ids = { "cave_warden", "antlion", "crystal_queen", "lava_titan", "void_lord", "tide_serpent" };
            float[] radii = { 1.2f, 1.3f, 1.3f, 1.4f, 1.4f, 1.3f };
            var bosses = new Boss[3, ids.Length];
            for (int row = 0; row < ids.Length; row++)
            {
                bosses[0, row] = new Boss { Id = ids[row], Radius = radii[row] };
                bosses[1, row] = new Boss { Id = ids[row], Radius = radii[row], Facing = Way(7) };
                bosses[2, row] = new Boss { Id = ids[row], Radius = radii[row], Facing = Way(3) };
            }
            Play(path, 3, ids.Length, 64, 60, 3, 0.62f, 6f, arena, (column, row, time, step) =>
            {
                var boss = bosses[column, row];
                if (column == 0)
                {
                    boss.Facing = Way((6 + (int)(time / 0.5f)) % 8);
                    return boss.Frame(time, step);
                }
                boss.State = time < 0.6f ? BossState.Approach
                    : time < 1.6f ? BossState.Telegraph
                    : time < 2.3f ? BossState.Recover
                    : time < 3.1f ? BossState.Charge
                    : time < 3.6f ? BossState.Recover
                    : time < 4.8f ? BossState.Roar
                    : BossState.Approach;
                boss.Moving = boss.State == BossState.Charge;
                if (Ticks(time) == Ticks(1.6f)) boss.Attack();
                return boss.Frame(time, step);
            });
        }
    }
}
