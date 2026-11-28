using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // A party of bots against a boss in its arena (the first unless --boss-id names another). Each bot walks in, hits the boss, uses its class skills
    // when ready, and steps out of a telegraph once it has had `reaction` seconds to notice it, the way a person
    // would; a blow about to land on it anyway it rolls through (RollBot, --no-roll to compare). Prints how long the kill took, how often the party wiped, which attacks did the damage, and how the
    // bots' own damage splits between the basic attack and each skill.
    static class BossSim
    {
        sealed class Bot
        {
            public Player Player;
            public SkillBot Skills;
            public int Deaths;
            public int Taken;
        }

        public static void Run(
            LootData loot, MonsterData monsters, BossData bosses, int seconds, ulong seed, int players, string[] gear, ClassDefinition[] classes, float reaction, bool dodge,
            string bossId = null)
        {
            var generated = WorldGenerator.Generate(seed);
            var world = new World(generated.Map, generated.Spawn, monsters, seed, new WorldConfig { EnemyTarget = 0 }, loot);
            int index = bossId == null ? 0 : bosses.Bosses.ToList().FindIndex(b => b.Id == bossId);
            if (index < 0) throw new ArgumentException($"No boss '{bossId}' in bosses.csv.");
            var definition = bosses.Bosses[index];
            var arena = generated.Arenas[Math.Min(index, generated.Arenas.Count - 1)];
            world.PlaceBoss(definition, arena, generated.ArenaRadius);

            var random = new GameRandom(seed, 99);
            var generator = new LootGenerator(loot);
            var bots = new List<Bot>();
            for (int i = 0; i < players; i++)
            {
                var equipment = new Equipment();
                foreach (string id in gear)
                {
                    var itemBase = loot.FindBase(id);
                    var item = generator.RollItem(itemBase, itemBase.Level, Rarity.Common, random);
                    // A second weapon goes to the spare slot (Docs/Weapons.md): "--gear iron_bow,iron_sword" holds the bow.
                    if (itemBase.Slot == ItemSlot.Weapon && equipment[ItemSlot.Weapon] != null) equipment.EquipSpare(item);
                    else equipment.Equip(item);
                }
                var player = world.AddPlayer("bot" + (i + 1), equipment, classes);
                bots.Add(new Bot { Player = player, Skills = new SkillBot(player, loot) });
            }

            var toArena = new FlowField(world.Map);
            toArena.Build(new[] { arena }, 200);
            int reactionTicks = World.Ticks(reaction);
            var takenByAttack = new Dictionary<string, int>();
            var phaseStarts = new List<(int Phase, double Seconds)>();
            int wakes = 0;
            double? killedAt = null;
            int dealt = 0;

            for (int tick = 0; tick < seconds * World.TicksPerSecond && killedAt == null; tick++)
            {
                foreach (var bot in bots)
                {
                    Drive(world, bot.Player, toArena, arena, reactionTicks, dodge);
                    var boss = world.Bosses.FirstOrDefault();
                    bool fighting = boss != null && Vec2.Distance(bot.Player.Position, boss.Home) <= boss.ArenaRadius + 2;
                    bot.Skills.Use(world, boss?.Position ?? arena, fighting);
                }
                world.Step();
                foreach (var e in world.TakeEvents())
                {
                    foreach (var counting in bots) counting.Skills.Count(e);
                    var bot = bots.FirstOrDefault(b => b.Player.Id == e.Target);
                    if (e.Type == SimEventType.Hit && bot != null)
                    {
                        bot.Taken += e.Amount;
                        string cause = world.FindBoss(e.Source) == null && e.Cause == 0 ? "슬라임"
                            : e.Cause == 0 ? "포효" : definition.Attacks[e.Cause - 1].Name;
                        takenByAttack[cause] = takenByAttack.GetValueOrDefault(cause) + e.Amount;
                    }
                    if (e.Type == SimEventType.Hit && bots.Any(b => b.Player.Id == e.Source) && world.Bosses.Any(b => b.Id == e.Target)) dealt += e.Amount;
                    if (e.Type == SimEventType.Died && bots.Any(b => b.Player.Id == e.Source)) bots.First(b => b.Player.Id == e.Source).Deaths++;
                    if (e.Type == SimEventType.BossAwake)
                    {
                        wakes++;
                        phaseStarts.Add((1, world.Tick / (double)World.TicksPerSecond));
                    }
                    if (e.Type == SimEventType.PhaseChanged) phaseStarts.Add((e.Amount, world.Tick / (double)World.TicksPerSecond));
                    if (e.Type == SimEventType.BossDefeated) killedAt = world.Tick / (double)World.TicksPerSecond;
                }
            }

            Console.WriteLine($"{definition.Name}: seed = {seed}, 봇 {players}명, 장비 {string.Join(", ", gear)}, 클래스 {FightSim.ClassNames(classes)}, 반응 {reaction}초, 피하기 {(dodge ? "켬" : "끔")}, 구르기 {(dodge && RollBot.Enabled ? "켬" : "끔")}");
            var first = bots[0].Player.Stats;
            Console.WriteLine($"  봇 능력치: 생명력 {first.MaxHealth:0}, 피해 {first.Damage:0.#}, 초당 공격 {first.AttacksPerSecond:0.##}, 방어도 {first.Armor:0}");
            Console.WriteLine(killedAt.HasValue
                ? $"처치: {killedAt:0.0}초 (보스를 깨운 횟수 {wakes}, 전멸 후 초기화 {wakes - 1}번)"
                : $"처치 못 함 ({seconds}초, 보스를 깨운 횟수 {wakes})");
            Console.WriteLine("  페이즈 시작: " + string.Join(", ", phaseStarts.Select(p => $"{p.Phase}페이즈 {p.Seconds:0.0}초")));
            Console.WriteLine($"  보스에게 준 피해 {dealt}");
            foreach (var bot in bots) Console.WriteLine($"  {bot.Player.Name}: 사망 {bot.Deaths}, 받은 피해 {bot.Taken}, 마신 물약 {bot.Skills.Drunk}개, 구르기 {bot.Player.Rolls}번(흘린 타격 {bot.Player.Dodges}), 준 피해 {bot.Skills.Summary()}");
            Console.WriteLine("  받은 피해 출처:");
            foreach (var pair in takenByAttack.OrderByDescending(p => p.Value)) Console.WriteLine($"    {pair.Key,-8} {pair.Value}");

            if (killedAt == null) return;
            Console.WriteLine($"  보상 (이후 드랍 레벨 {world.ProgressLevel}):");
            var rewards = world.GroundLoot.Select(l => l.Item).Where(i => i != null)
                .Concat(bots.SelectMany(b => b.Player.Inventory.Items))
                .Where(i => i.Level == definition.DropLevel);
            foreach (var item in rewards) Console.WriteLine("    " + ItemText.Describe(item)[0]);
            foreach (var rune in world.GroundLoot.Where(l => l.Rune != null).Select(l => l.Rune)) Console.WriteLine("    " + rune.Name);
        }

        static void Drive(World world, Player player, FlowField toArena, Vec2 arena, int reactionTicks, bool dodge)
        {
            if (player.IsDead) return;
            var boss = world.Bosses.FirstOrDefault();
            var target = boss?.Position ?? arena;
            float targetRadius = boss?.Definition.Radius ?? 0;
            // A vein the boss would eat to rage is broken first (R2b, the vein eater).
            var vein = boss == null ? null : world.Props.Where(p => p.Owner == boss.Id && p.Kind == PropKind.Vein)
                .OrderBy(p => Vec2.Distance(p.Position, player.Position)).FirstOrDefault();
            if (vein != null)
            {
                target = vein.Position;
                targetRadius = vein.Radius;
            }
            var toTarget = target - player.Position;

            // A slime in the way gets hit first.
            var slime = world.Enemies.Where(e => Vec2.Distance(e.Position, player.Position) < 1.6f).OrderBy(e => Vec2.Distance(e.Position, player.Position)).FirstOrDefault();
            var facing = slime != null ? slime.Position - player.Position : toTarget;

            var input = Vec2.Zero;
            var escape = dodge ? Escape(world, player, reactionTicks) : Vec2.Zero;
            float gap = toTarget.Length - targetRadius - World.PlayerRadius;
            if (escape.LengthSquared > 0)
            {
                input = escape;
            }
            else if (Vec2.Distance(player.Position, arena) > WorldGenerator.ArenaRadius)
            {
                input = toArena.DirectionFrom(player.Position);
            }
            else if (player.Stats.Attack.Shoots && gap < FightSim.Reach(player) * 0.5f)
            {
                // A bow or staff backs off from a boss that comes close.
                input = -toTarget.Normalized;
            }
            else if (gap > FightSim.Reach(player) * 0.7f)
            {
                input = toTarget.Normalized;
            }

            // Walking (not escaping), it goes round a boss's lava, cracks and biting edge rather than through (R2).
            if (dodge && escape.LengthSquared == 0 && input.LengthSquared > 0) input = RoundGround(world, player, input);
            RollBot.Move(world, player, input, facing, escape, dodge);
            bool inReach = slime != null || ((boss != null || vein != null) && gap <= FightSim.Reach(player));
            world.SetAttacking(player.Id, inReach);
        }

        // Where to step to get out of every noticed telegraph and the path of nearby projectiles.
        internal static Vec2 Escape(World world, Player player, int reactionTicks)
        {
            var away = Vec2.Zero;
            // Beams that cross where the bot stands (a star of them, 2026-11-13): their sideways pushes cancel, so it
            // heads out from the crossing and toward the edge of the beam it is nearest to leaving.
            int beams = 0;
            var outOfStar = Vec2.Zero;
            float widest = -1;
            foreach (var hazard in world.Hazards)
            {
                if (world.Tick - hazard.StartTick < reactionTicks) continue;
                // A cloud that only fills monsters and the mark of where one will come out harm no one.
                if (hazard.Shape == HazardShape.Balm || hazard.Damage == 0 && hazard.Mark == (byte)MonsterAttackShape.Blink + 1) continue;
                // A boss's ground (R2): water and cooled rock harm no one; a blast that spares rock is no danger on it,
                // and off it the bot heads for the nearest rock.
                if (hazard.Shape == HazardShape.Water || hazard.Shape == HazardShape.Rock) continue;
                if (!hazard.Contains(player.Position, World.PlayerRadius + 0.3f)) continue;
                // A blast that spares the shaded (R2b): behind the nearest pillar from the boss.
                if (hazard.Shades)
                {
                    if (world.Shaded(hazard.Source, hazard.Position, player.Position)) continue;
                    var shade = NearestShade(world, hazard, player.Position);
                    if (shade != null) away += (shade.Value - player.Position).Normalized * 2;
                    continue;
                }
                if (hazard.Spares)
                {
                    if (world.OnRock(hazard.Source, player.Position)) continue;
                    var rock = NearestRock(world, hazard.Source, player.Position);
                    if (rock != null) away += (rock.Value - player.Position).Normalized * 2;
                    continue;
                }
                var offset = player.Position - hazard.Position;
                if (hazard.Shape == HazardShape.Line || hazard.IsGround && hazard.Width > 0)
                {
                    // Sideways, toward whichever edge is nearer.
                    var side = new Vec2(-hazard.Direction.Y, hazard.Direction.X);
                    float across = Vec2.Dot(offset, side);
                    away += side * (across >= 0 ? 1 : -1);
                    beams++;
                    if (MathF.Abs(across) > widest)
                    {
                        widest = MathF.Abs(across);
                        var fromMiddle = player.Position - (hazard.Position + hazard.Direction * (hazard.Size / 2));
                        outOfStar = (fromMiddle.LengthSquared > 1e-4f ? fromMiddle.Normalized : hazard.Direction) + side * (across >= 0 ? 1 : -1);
                    }
                }
                else if (hazard.Shape == HazardShape.Ring)
                {
                    // Into the safe middle when nearer it than the outer edge, else out past the edge.
                    float distance = offset.Length;
                    var outward = distance > 1e-4f ? offset / distance : new Vec2(1, 0);
                    away += distance - hazard.Width < hazard.Size - distance ? outward * -1 : outward;
                }
                else if (hazard.Shape == HazardShape.Share || hazard.Shape == HazardShape.Web)
                {
                    // A web only slows: a bot walks through it to get at the boss.
                    // Taken together: the bots stay in and share it (alone, the one in it takes it all).
                    continue;
                }
                else
                {
                    away += offset.LengthSquared > 1e-4f ? offset.Normalized : new Vec2(1, 0);
                }
            }
            foreach (var projectile in world.Projectiles)
            {
                if (projectile.Friendly) continue;
                var toPlayer = player.Position - projectile.Position;
                var heading = projectile.Velocity.Normalized;
                float along = Vec2.Dot(toPlayer, heading);
                if (along < 0 || along > 3) continue;
                var side = new Vec2(-heading.Y, heading.X);
                float across = Vec2.Dot(toPlayer, side);
                if (MathF.Abs(across) > projectile.Radius + World.PlayerRadius + 0.2f) continue;
                away += side * (across >= 0 ? 1 : -1);
            }
            if (beams >= 2 && away.Length < 1.2f) away = outOfStar;
            // Out of a boss's edge that bites, back toward its middle.
            var edged = world.Bosses.FirstOrDefault(b => !b.IsDead && World.InEdge(b, player.Position));
            if (edged != null && world.BadGround(player.Position)) away += (edged.Home - player.Position).Normalized * 2;
            return away.Normalized;
        }

        // The way to walk: as given unless a step that way goes onto a boss's ground that bites, then the nearest turn
        // that does not (or stand still).
        static Vec2 RoundGround(World world, Player player, Vec2 way)
        {
            bool Bad(Vec2 at) => world.BadGround(at) || world.Hazards.Any(h => (h.Shape == HazardShape.Lava || h.Shape == HazardShape.Crack) && h.Damage > 0
                && h.Contains(at, World.PlayerRadius) && !(h.Shape == HazardShape.Lava && world.OnRock(h.Source, at)));
            if (Bad(player.Position)) return way;
            foreach (float turn in new[] { 0f, 45, -45, 90, -90 })
            {
                var dir = Vec2.FromAngle(way.Angle + turn * MathF.PI / 180);
                if (!Bad(player.Position + dir * 0.7f)) return dir * way.Length;
            }
            return Vec2.Zero;
        }

        // The spot just behind the nearest pillar of the boss's, from where its blast comes.
        static Vec2? NearestShade(World world, Hazard blast, Vec2 from)
        {
            var spots = world.Props.Where(p => p.Owner == blast.Source && p.Kind == PropKind.Pillar)
                .Select(p => p.Position + (p.Position - blast.Position).Normalized * (p.Radius + World.PlayerRadius + 0.2f)).ToList();
            return spots.Count == 0 ? (Vec2?)null : spots.OrderBy(s => Vec2.Distance(s, from)).First();
        }

        // The middle of the nearest cooled rock of the boss's (a pool, or an edge's island).
        static Vec2? NearestRock(World world, int bossId, Vec2 from)
        {
            var spots = world.Hazards.Where(h => h.Shape == HazardShape.Rock && h.Source == bossId)
                .Select(h => h.Width > 0 ? h.Position + h.Direction * (h.Size / 2) : h.Position).ToList();
            var boss = world.FindBoss(bossId);
            if (boss != null) spots.AddRange(World.Islands(boss));
            return spots.Count == 0 ? (Vec2?)null : spots.OrderBy(s => Vec2.Distance(s, from)).First();
        }
    }
}
