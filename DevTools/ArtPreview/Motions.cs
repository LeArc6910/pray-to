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
            VeinEater(Path.Combine(folder, "vein_eater.gif"), arena);
            Samurai(Path.Combine(folder, "samurai.gif"), arena);
            Critters(Path.Combine(folder, "monsters.gif"), monsters, floor);
            People(Path.Combine(folder, "people.gif"), npcs, floor);
            StoryPeople(Path.Combine(folder, "story_people.gif"), floor);
            Animals(Path.Combine(folder, "animals.gif"), floor);
            Decor(Path.Combine(folder, "decor.gif"), sword, floor);
            Standing(Path.Combine(folder, "structures.gif"), sword);
            Machines(Path.Combine(folder, "machines.gif"), sword);
        }

        // Machines standing as blocks (2026-11-11, PropArt.Raised): a drill turning its bit, a turret sweeping, a
        // generator burning and the others, their parts on their tops as WorldView.DrawMachines places them, and a
        // player walking round them, in front and behind.
        static void Machines(string path, ItemBase sword)
        {
            var walker = new Player { Weapon = sword, Facing = new Vec2(1, 0) };
            var row = new (TileType Type, int X, int Y)[]
            {
                (TileType.Drill, -3, -1), (TileType.Smelter, -2, -1), (TileType.Generator, -1, -1), (TileType.Turret, 0, -1), (TileType.ArcFurnace, 1, -1),
                (TileType.Assembler, 2, -1), (TileType.Keg, -3, 1), (TileType.PreservesJar, -2, 1), (TileType.Coop, -1, 1), (TileType.Barn, 0, 1),
                (TileType.AlchemyTable, 1, 1), (TileType.TeslaTower, 2, 1),
            };
            var corners = new[] { new Vec2(-3.9f, -1.6f), new Vec2(3.4f, -1.6f), new Vec2(3.4f, 0.5f), new Vec2(-3.9f, 0.5f) };
            float perimeter = 0;
            for (int i = 0; i < 4; i++) perimeter += (corners[(i + 1) % 4] - corners[i]).Length;
            const float seconds = 9;
            Play(path, 1, 1, 160, 112, 3, 0.5f, seconds, TileArt.Object(TileType.StoneFloor), (column, r, time, step) =>
            {
                var draws = new List<Draw>();
                foreach (var (type, x, y) in row)
                {
                    int body = 10000 - y * 10;
                    draws.Add(Sprite(PropArt.Prop(type), x + 0.5f, y, 0.5f, 0, body));
                    float top = y + 0.5f + PropArt.LiftOf(type) / (float)TileArt.Size;
                    if (type == TileType.Drill) draws.Add(Sprite(ItemArt.DrillBit, x + 0.5f, top, 0.5f, 0.5f, body + 1, angle: time * 360 % 360));
                    if (type == TileType.Turret) draws.Add(Sprite(ItemArt.TurretBarrel, x + 0.5f, top, 0.5f, 0.5f, body + 1, angle: 90 + 60 * MathF.Sin(time * 2)));
                    if (type == TileType.Generator)
                        draws.Add(Sprite(ItemArt.Flame((int)(time * 8) % ItemArt.FlameFrames), x + 0.5f, top, 0.5f, 0.5f, body + 1));
                }
                float along = time / seconds * perimeter;
                var at = corners[0];
                var facing = new Vec2(1, 0);
                for (int i = 0; i < 4; i++)
                {
                    var from = corners[i];
                    var to = corners[(i + 1) % 4];
                    float length = (to - from).Length;
                    if (along <= length)
                    {
                        facing = (to - from) * (1 / length);
                        at = from + facing * along;
                        break;
                    }
                    along -= length;
                }
                walker.Facing = facing;
                walker.Moving = true;
                int lift = -(int)MathF.Round(at.Y * 10);
                foreach (var d in walker.Frame(time, step))
                {
                    var moved = d;
                    moved.X += at.X;
                    moved.Y += at.Y;
                    if (moved.Order > 0) moved.Order += lift;
                    draws.Add(moved);
                }
                return draws;
            });
        }

        // Structures standing on the floor (2026-11-10, PropArt): a player walks round a cottage, in front of it and
        // then behind it, where the roof hides them. Props sort as WorldView.DrawProps does (10000 - y*10 from the
        // bottom edge of the anchor tile) and people as WorldView.Place (10000 - y*10 from their centre).
        static void Standing(string path, ItemBase sword)
        {
            var walker = new Player { Weapon = sword, Facing = new Vec2(1, 0) };
            var props = new (TileType Type, int X, int Y)[]
            {
                (TileType.Cottage, -2, 0), (TileType.LampPost, -4, 1), (TileType.Barrel, 3, 0), (TileType.Crates, 3, 1),
                (TileType.Flowerbed, -4, -1), (TileType.Bench, 3, 3), (TileType.MushroomTree, -5, 3),
            };
            // The way round: along the front, up the right side, along the back, down the left side.
            var corners = new[] { new Vec2(-2.6f, -0.9f), new Vec2(2.6f, -0.9f), new Vec2(2.6f, 3.6f), new Vec2(-2.6f, 3.6f) };
            float perimeter = 0;
            for (int i = 0; i < 4; i++) perimeter += (corners[(i + 1) % 4] - corners[i]).Length;
            const float seconds = 8;
            Play(path, 1, 1, 160, 112, 3, 5f / 7, seconds, TileArt.Object(TileType.StoneFloor), (column, row, time, step) =>
            {
                var draws = new List<Draw>();
                foreach (var (type, x, y) in props)
                {
                    int w = Structures.Footprint(type).W;
                    draws.Add(Sprite(PropArt.Prop(type), x + w / 2f, y, 0.5f, 0, 10000 - y * 10));
                }
                float along = time / seconds * perimeter;
                var at = corners[0];
                var facing = new Vec2(1, 0);
                for (int i = 0; i < 4; i++)
                {
                    var from = corners[i];
                    var to = corners[(i + 1) % 4];
                    float length = (to - from).Length;
                    if (along <= length)
                    {
                        facing = (to - from) * (1 / length);
                        at = from + facing * along;
                        break;
                    }
                    along -= length;
                }
                walker.Facing = facing;
                walker.Moving = true;
                int lift = -(int)MathF.Round(at.Y * 10);
                foreach (var d in walker.Frame(time, step))
                {
                    var moved = d;
                    moved.X += at.X;
                    moved.Y += at.Y;
                    // The foot shadow stays under everything; the body and what it holds sort by where they stand.
                    if (moved.Order > 0) moved.Order += lift;
                    draws.Add(moved);
                }
                return draws;
            });
        }

        // A room (2026-11-09): a back wall hung with tapestries and shields, the three rugs on the floor, and a door in
        // the right wall that swings open as a player walks out through it and shuts behind them (WorldView.SwingDoors).
        static void Decor(string path, ItemBase sword, PixelImage floor)
        {
            var walker = new Player { Weapon = sword, Facing = new Vec2(1, 0) };
            var rock = TileArt.Ground(TileArt.Index(GroundKind.Rock, 0, 0));
            var back = new[] { TileType.TapestryWall, TileType.ShieldWall, TileType.TapestryWall, TileType.ShieldWall, TileType.TapestryWall };
            var rugs = new[] { TileType.RedCarpet, TileType.BlueCarpet, TileType.YellowCarpet };
            const float seconds = 6;
            Play(path, 1, 1, 128, 80, 5, 0.5f, seconds, floor, (column, row, time, step) =>
            {
                var draws = new List<Draw>();
                for (int i = 0; i < back.Length; i++) draws.Add(Sprite(TileArt.Object(back[i]), -2.5f + i, 1.5f, 0.5f, 0.5f, 0));
                for (int x = 0; x < 5; x++)
                {
                    for (int y = 0; y < 2; y++) draws.Add(Sprite(TileArt.Object(rugs[(x + y) % 3]), -2.5f + x, 0.5f - y, 0.5f, 0.5f, -1));
                }
                // The right wall: rock above and below the door.
                draws.Add(Sprite(rock, 2.5f, 1.5f, 0.5f, 0.5f, 0));
                draws.Add(Sprite(rock, 2.5f, -0.5f, 0.5f, 0.5f, 0));
                // Walking right along y = 0.5 from the left of the room and out through the door.
                float t = time % seconds;
                // Out of sight past the right edge for the last second, the door shut behind them.
                float x0 = -3f + MathF.Min(t, 5) * 1.6f;
                walker.Moving = true;
                bool open = MathF.Abs(x0 - 2.5f) <= 1.05f;
                draws.Add(Sprite(open ? TileArt.OpenDoor : TileArt.Object(TileType.Door), 2.5f, 0.5f, 0.5f, 0.5f, 0));
                foreach (var d in walker.Frame(time, step))
                {
                    var moved = d;
                    moved.X += x0;
                    moved.Y += 0.5f;
                    draws.Add(moved);
                }
                return draws;
            });
        }

        // The coop, barn and fold with their animals wandering in front as WorldView.DrawChickens moves them, now and
        // then pecking or grazing (2026-11-09), and the iron wall beside a stone one.
        static void Animals(string path, PixelImage floor)
        {
            var houses = new[] { TileType.Coop, TileType.Barn, TileType.Fold };
            // The fifth: a fold opening into a fenced pen (World.Grazes), its sheep roaming the pen as WorldView does.
            var pen = new List<(int X, int Y)>();
            for (int y = 0; y >= -1; y--) for (int x = -1; x <= 1; x++) pen.Add((x, y));
            Play(path, 5, 1, 64, 76, 4, 0.5f, 8f, floor, (column, row, time, step) =>
            {
                if (column == 4)
                {
                    var penned = new List<Draw> { Sprite(TileArt.Object(TileType.Fold), 0, 1, 0.5f, 0.5f, 0) };
                    foreach (int x in new[] { -2, -1, 1, 2 }) penned.Add(Sprite(TileArt.Object(TileType.Fence), x, 1, 0.5f, 0.5f, 0));
                    for (int y = 0; y >= -1; y--) foreach (int x in new[] { -2, 2 }) penned.Add(Sprite(TileArt.Object(TileType.Fence), x, y, 0.5f, 0.5f, 0));
                    for (int x = -2; x <= 2; x++) penned.Add(Sprite(TileArt.Object(TileType.Fence), x, -2, 0.5f, 0.5f, 20));
                    for (int i = 0; i < 2; i++)
                    {
                        float leg = time / 5f + i * 0.37f;
                        int k = (int)MathF.Floor(leg);
                        float f = Math.Clamp((leg - k) * 1.6f, 0, 1);
                        f = f * f * (3 - 2 * f);
                        var from = pen[(i * 7 + k * 5) % pen.Count];
                        var to = pen[(i * 7 + (k + 1) * 5) % pen.Count];
                        float x = from.X + (to.X - from.X) * f, y = from.Y + (to.Y - from.Y) * f - 0.25f;
                        int frame = leg - k > 0.65f ? 1 : 0;
                        penned.Add(Sprite(ItemArt.Sheep(frame), x, y, 0.5f, 0, 10 - (int)(y * 10), flip: to.X >= from.X));
                    }
                    return penned;
                }
                if (column == 3)
                {
                    return new List<Draw>
                    {
                        Sprite(TileArt.Object(TileType.IronWall), -0.55f, 0.3f, 0.5f, 0.5f, 0),
                        Sprite(TileArt.Ground(TileArt.Index(GroundKind.Rock, 0, 0)), 0.55f, 0.3f, 0.5f, 0.5f, 0),
                        Sprite(TileArt.Cracks(2), -0.55f, 0.3f, 0.5f, 0.5f, 1),
                    };
                }
                var kind = houses[column];
                bool big = kind != TileType.Coop;
                int animals = kind == TileType.Coop ? 4 : kind == TileType.Barn ? 2 : 3;
                var draws = new List<Draw> { Sprite(TileArt.Object(kind), 0, 0.9f, 0.5f, 0.5f, 0) };
                for (int i = 0; i < animals; i++)
                {
                    float t = time * 0.45f + i * 1.7f + column * 0.3f;
                    if (big) t *= 0.6f;
                    float x = MathF.Sin(t) * (big ? 1.4f : 1.1f);
                    float y = -0.4f + MathF.Sin(t * 1.9f + i) * 0.3f;
                    int frame = MathF.Sin(t * 3.1f) > 0.6f ? 1 : 0;
                    var image = kind == TileType.Barn ? ItemArt.Cow(frame) : kind == TileType.Fold ? ItemArt.Sheep(frame) : ItemArt.Chicken(frame);
                    draws.Add(Sprite(image, x, y, 0.5f, 0, 10 - (int)(y * 10), flip: MathF.Cos(t) > 0));
                }
                return draws;
            });
        }

        // The people of the story in each look (Docs/Story.md, 2026-11-10), breathing and blinking; two loops.
        static void StoryPeople(string path, PixelImage floor)
        {
            var colors = new[] { "#E8C860", "#4A7AD8", "#8A6A4A", "#B84AC8", "#6A3A8A", "#5A7AB8", "#C85A6A" };
            Play(path, PeopleArt.Looks.Length, 1, 22, 26, 5, 0.72f, 2 * PeopleArt.Frames / PeopleArt.FramesPerSecond, floor, (column, row, time, step) =>
            {
                int frame = (int)MathF.Floor(time * PeopleArt.FramesPerSecond) % PeopleArt.Frames;
                return new List<Draw> { Sprite(PeopleArt.Look(PeopleArt.Looks[column], Rgba.Hex(colors[column]), frame), 0, -0.2f, 0.5f, 0, 10) };
            });
        }

        // Each person at their stall as WorldView draws them (2026-11-09): the stall, the person standing in it, the
        // counter over their legs and the awning on the tile above over their head; two loops.
        static void People(string path, NpcData npcs, PixelImage floor)
        {
            var list = npcs.Npcs.ToList();
            var counter = TileArt.MerchantCounter;
            Play(path, list.Count, 1, 26, 44, 5, 0.72f, 2 * PeopleArt.Frames / PeopleArt.FramesPerSecond, floor, (column, row, time, step) =>
            {
                var npc = list[column];
                int frame = (int)MathF.Floor(time * PeopleArt.FramesPerSecond) % PeopleArt.Frames;
                var person = PeopleArt.Person(npc.Id, Rgba.Hex(npc.Color), frame) ?? CreatureArt.Player(Look.Down, 0, Rgba.Hex(npc.Color), Pose.Stand);
                return new List<Draw>
                {
                    Sprite(TileArt.Object(npc.Tile), 0, 0, 0.5f, 0.5f, 0),
                    Sprite(person, 0, -0.2f, 0.5f, 0, 10),
                    Sprite(counter, 0, 0, 0.5f, 0.5f, 12),
                    Sprite(TileArt.Awning(npc.Tile), 0, 1, 0.5f, 0.5f, 13),
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
        // The vein eater's walk, wind-ups and blows (2026-11-12, VeinEaterArt), a row each as WorldView plays them,
        // facing right and facing down: walk, claw, rock throw, rush, burrow, cave-in, spikes, spin, frenzy, quake,
        // roar.
        static void VeinEater(string path, PixelImage arena)
        {
            var rows = new (VeinPose Pose, int Frame, float Seconds, bool Rage, int Phase)[][]
            {
                new[] { (VeinPose.Walk, 0, 0.14f, false, 1), (VeinPose.Walk, 1, 0.14f, false, 1), (VeinPose.Walk, 2, 0.14f, false, 1), (VeinPose.Walk, 3, 0.14f, false, 1) },
                new[] { (VeinPose.Idle, 0, 0.3f, false, 1), (VeinPose.Rear, 0, 0.13f, false, 1), (VeinPose.Rear, 1, 0.13f, false, 1), (VeinPose.Rear, 2, 0.13f, false, 1), (VeinPose.Rear, 3, 0.13f, false, 1), (VeinPose.Rear, 4, 0.13f, false, 1), (VeinPose.Rear, 5, 0.13f, false, 1), (VeinPose.Swipe, 0, 0.15f, false, 1), (VeinPose.Swipe, 1, 0.15f, false, 1), (VeinPose.Swipe, 2, 0.15f, false, 1), (VeinPose.Idle, 0, 0.3f, false, 1) },
                new[] { (VeinPose.Idle, 0, 0.3f, false, 1), (VeinPose.Lift, 0, 0.15f, false, 1), (VeinPose.Lift, 1, 0.15f, false, 1), (VeinPose.Lift, 2, 0.15f, false, 1), (VeinPose.Lift, 3, 0.15f, false, 1), (VeinPose.Lift, 4, 0.15f, false, 1), (VeinPose.Lift, 5, 0.15f, false, 1), (VeinPose.Throw, 0, 0.15f, false, 1), (VeinPose.Throw, 1, 0.15f, false, 1), (VeinPose.Throw, 2, 0.15f, false, 1), (VeinPose.Idle, 0, 0.3f, false, 1) },
                new[] { (VeinPose.Idle, 0, 0.3f, false, 1), (VeinPose.Crouch, 0, 0.1f, false, 1), (VeinPose.Crouch, 1, 0.1f, false, 1), (VeinPose.Crouch, 0, 0.1f, false, 1), (VeinPose.Crouch, 1, 0.1f, false, 1), (VeinPose.Rush, 0, 0.07f, false, 1), (VeinPose.Rush, 1, 0.07f, false, 1), (VeinPose.Rush, 2, 0.07f, false, 1), (VeinPose.Rush, 3, 0.07f, false, 1), (VeinPose.Rush, 0, 0.07f, false, 1), (VeinPose.Rush, 1, 0.07f, false, 1), (VeinPose.Rush, 2, 0.07f, false, 1), (VeinPose.Rush, 3, 0.07f, false, 1) },
                new[] { (VeinPose.Idle, 0, 0.3f, false, 1), (VeinPose.Sink, 0, 0.2f, false, 1), (VeinPose.Sink, 1, 0.2f, false, 1), (VeinPose.Sink, 2, 0.2f, false, 1), (VeinPose.Sink, 3, 0.5f, false, 1), (VeinPose.Rise, 0, 0.3f, false, 1) },
                new[] { (VeinPose.Idle, 0, 0.3f, false, 2), (VeinPose.Slam, 0, 0.2f, false, 2), (VeinPose.Slam, 1, 0.2f, false, 2), (VeinPose.Slam, 2, 0.2f, false, 2), (VeinPose.Slam, 3, 0.2f, false, 2), (VeinPose.Slam, 4, 0.2f, false, 2), (VeinPose.Slam, 5, 0.2f, false, 2), (VeinPose.Pound, 0, 0.15f, false, 2), (VeinPose.Pound, 1, 0.15f, false, 2), (VeinPose.Pound, 2, 0.15f, false, 2), (VeinPose.Idle, 0, 0.3f, false, 2) },
                new[] { (VeinPose.Idle, 0, 0.3f, false, 2), (VeinPose.Plunge, 0, 0.12f, false, 2), (VeinPose.Plunge, 1, 0.12f, false, 2), (VeinPose.Plunge, 2, 0.12f, false, 2), (VeinPose.Plunge, 3, 0.12f, false, 2), (VeinPose.Plunge, 4, 0.12f, false, 2), (VeinPose.Plunge, 5, 0.12f, false, 2) },
                new[] { (VeinPose.Idle, 0, 0.3f, false, 2), (VeinPose.Curl, 0, 0.12f, false, 2), (VeinPose.Curl, 1, 0.12f, false, 2), (VeinPose.Curl, 2, 0.12f, false, 2), (VeinPose.Curl, 3, 0.12f, false, 2), (VeinPose.Curl, 4, 0.12f, false, 2), (VeinPose.Curl, 5, 0.12f, false, 2) },
                new[] { (VeinPose.Idle, 0, 0.3f, false, 3), (VeinPose.Crouch, 0, 0.08f, true, 3), (VeinPose.Crouch, 1, 0.08f, true, 3), (VeinPose.Rush, 0, 0.07f, true, 3), (VeinPose.Rush, 1, 0.07f, true, 3), (VeinPose.Rush, 2, 0.07f, true, 3), (VeinPose.Rush, 3, 0.07f, true, 3), (VeinPose.Crouch, 0, 0.08f, true, 3), (VeinPose.Crouch, 1, 0.08f, true, 3), (VeinPose.Rush, 0, 0.07f, true, 3), (VeinPose.Rush, 1, 0.07f, true, 3), (VeinPose.Rush, 2, 0.07f, true, 3), (VeinPose.Rush, 3, 0.07f, true, 3) },
                new[] { (VeinPose.Idle, 0, 0.3f, false, 3), (VeinPose.Leap, 0, 0.4f, false, 3), (VeinPose.Leap, 1, 0.12f, false, 3), (VeinPose.Leap, 2, 0.12f, false, 3), (VeinPose.Leap, 3, 0.4f, false, 3), (VeinPose.Pound, 0, 0.15f, false, 3), (VeinPose.Pound, 1, 0.15f, false, 3), (VeinPose.Pound, 2, 0.15f, false, 3) },
                new[] { (VeinPose.Idle, 0, 0.3f, false, 3), (VeinPose.Roar, 0, 0.25f, false, 3), (VeinPose.Roar, 1, 0.25f, false, 3), (VeinPose.Roar, 0, 0.25f, false, 3), (VeinPose.Roar, 1, 0.25f, false, 3) },
            };
            const int scale = 2;
            int cellWidth = VeinEaterArt.Width * scale;
            int cellHeight = VeinEaterArt.Height * scale;
            int width = cellWidth * 2;
            int height = cellHeight * rows.Length;
            var frames = new List<(Rgba[] Pixels, int Centiseconds)>();
            for (float time = 0; time < 3.2f; time += 0.04f)
            {
                var pixels = new Rgba[width * height];
                for (int row = 0; row < rows.Length; row++)
                {
                    float total = rows[row].Sum(f => f.Seconds);
                    float at = time % total;
                    var step = rows[row].First();
                    foreach (var f in rows[row])
                    {
                        step = f;
                        if (at < f.Seconds) break;
                        at -= f.Seconds;
                    }
                    for (int column = 0; column < 2; column++)
                    {
                        var image = VeinEaterArt.Draw(column == 0 ? BossView.Side : BossView.Front, step.Pose, step.Frame, step.Phase, step.Rage);
                        for (int y = 0; y < cellHeight; y++)
                        {
                            for (int x = 0; x < cellWidth; x++)
                            {
                                var floor = arena[x / scale % arena.Width, y / scale % arena.Height];
                                var c = image[x / scale, y / scale];
                                pixels[(row * cellHeight + y) * width + column * cellWidth + x] = c.A > 0 ? c : floor;
                            }
                        }
                    }
                }
                frames.Add((pixels, 4));
            }
            Gif.Save(path, width, height, frames);
        }

        // The shrine samurai's walk and iaido (2026-11-12, SamuraiArt), a row each, facing right and facing down: walk,
        // stance and draw and sheathing, a dash, the blade raised overhead and brought down, a shout.
        static void Samurai(string path, PixelImage arena)
        {
            (SamuraiPose, int, float)[] Run(SamuraiPose pose, int frames, float seconds) =>
                Enumerable.Range(0, frames).Select(f => (pose, f, seconds)).ToArray();
            (SamuraiPose, int, float)[] Sheathe() => new[] { (SamuraiPose.Chiburi, 0, 0.14f) }.Concat(Run(SamuraiPose.Noto, 3, 0.18f)).ToArray();
            var rows = new[]
            {
                Run(SamuraiPose.Walk, 4, 0.14f).Concat(Run(SamuraiPose.Walk, 4, 0.14f)).ToArray(),
                new[] { (SamuraiPose.Idle, 0, 0.3f) }.Concat(Run(SamuraiPose.Stance, 6, 0.15f)).Concat(new[] { (SamuraiPose.Draw, 0, 0.04f), (SamuraiPose.Draw, 1, 0.1f), (SamuraiPose.Draw, 2, 0.12f) })
                    .Concat(Sheathe()).ToArray(),
                new[] { (SamuraiPose.Idle, 0, 0.3f) }.Concat(Run(SamuraiPose.Stance, 6, 0.1f)).Concat(Run(SamuraiPose.Dash, 4, 0.05f))
                    .Concat(new[] { (SamuraiPose.Draw, 1, 0.1f), (SamuraiPose.Draw, 2, 0.12f) }).Concat(Sheathe()).ToArray(),
                new[] { (SamuraiPose.Idle, 0, 0.3f) }.Concat(Run(SamuraiPose.Jodan, 6, 0.15f)).Concat(new[] { (SamuraiPose.Kiri, 0, 0.04f), (SamuraiPose.Kiri, 1, 0.1f), (SamuraiPose.Kiri, 2, 0.12f) })
                    .Concat(Sheathe()).ToArray(),
                new[] { (SamuraiPose.Idle, 0, 0.3f), (SamuraiPose.Kiai, 0, 0.2f), (SamuraiPose.Kiai, 1, 0.2f), (SamuraiPose.Kiai, 0, 0.2f), (SamuraiPose.Kiai, 1, 0.2f) },
            };
            const int scale = 2;
            int cellWidth = SamuraiArt.Width * scale;
            int cellHeight = SamuraiArt.Height * scale;
            int width = cellWidth * 2;
            int height = cellHeight * rows.Length;
            var frames = new List<(Rgba[] Pixels, int Centiseconds)>();
            for (float time = 0; time < 3.4f; time += 0.04f)
            {
                var pixels = new Rgba[width * height];
                for (int row = 0; row < rows.Length; row++)
                {
                    float at = time % rows[row].Sum(f => f.Item3);
                    var step = rows[row][0];
                    foreach (var f in rows[row])
                    {
                        step = f;
                        if (at < f.Item3) break;
                        at -= f.Item3;
                    }
                    for (int column = 0; column < 2; column++)
                    {
                        var image = SamuraiArt.Draw(column == 0 ? BossView.Side : BossView.Front, step.Item1, step.Item2, 1);
                        for (int y = 0; y < cellHeight; y++)
                        {
                            for (int x = 0; x < cellWidth; x++)
                            {
                                var floor = arena[x / scale % arena.Width, y / scale % arena.Height];
                                var c = image[x / scale, y / scale];
                                pixels[(row * cellHeight + y) * width + column * cellWidth + x] = c.A > 0 ? c : floor;
                            }
                        }
                    }
                }
                frames.Add((pixels, 4));
            }
            Gif.Save(path, width, height, frames);
        }

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
