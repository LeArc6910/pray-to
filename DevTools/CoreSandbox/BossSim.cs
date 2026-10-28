using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // A party of bots against the first boss in its arena. Each bot walks in, hits the boss, uses its class skills
    // when ready, and steps out of a telegraph once it has had `reaction` seconds to notice it, the way a person
    // would. Prints how long the kill took, how often the party wiped, which attacks did the damage, and how the
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
            LootData loot, MonsterData monsters, BossData bosses, int seconds, ulong seed, int players, string[] gear, ClassDefinition[] classes, float reaction, bool dodge)
        {
            var generated = WorldGenerator.Generate(seed);
            var world = new World(generated.Map, generated.Spawn, monsters, seed, new WorldConfig { EnemyTarget = 0 }, loot);
            var definition = bosses.Bosses[0];
            world.PlaceBoss(definition, generated.Arena, generated.ArenaRadius);

            var random = new GameRandom(seed, 99);
            var generator = new LootGenerator(loot);
            var bots = new List<Bot>();
            for (int i = 0; i < players; i++)
            {
                var equipment = new Equipment();
                foreach (string id in gear)
                {
                    var itemBase = loot.FindBase(id);
                    equipment.Equip(generator.RollItem(itemBase, itemBase.Level, Rarity.Common, random));
                }
                var player = world.AddPlayer("bot" + (i + 1), equipment, classes);
                bots.Add(new Bot { Player = player, Skills = new SkillBot(player) });
            }

            var toArena = new FlowField(world.Map);
            toArena.Build(new[] { generated.Arena }, 200);
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
                    Drive(world, bot.Player, toArena, generated.Arena, reactionTicks, dodge);
                    var boss = world.Bosses.FirstOrDefault();
                    if (boss != null) bot.Skills.Use(world, boss.Position, Vec2.Distance(boss.Position, boss.Home) <= boss.ArenaRadius && Vec2.Distance(bot.Player.Position, boss.Home) <= boss.ArenaRadius + 2);
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

            Console.WriteLine($"{definition.Name}: seed = {seed}, 봇 {players}명, 장비 {string.Join(", ", gear)}, 클래스 {FightSim.ClassNames(classes)}, 반응 {reaction}초, 피하기 {(dodge ? "켬" : "끔")}");
            var first = bots[0].Player.Stats;
            Console.WriteLine($"  봇 능력치: 생명력 {first.MaxHealth:0}, 피해 {first.Damage:0.#}, 초당 공격 {first.AttacksPerSecond:0.##}, 방어도 {first.Armor:0}");
            Console.WriteLine(killedAt.HasValue
                ? $"처치: {killedAt:0.0}초 (보스를 깨운 횟수 {wakes}, 전멸 후 초기화 {wakes - 1}번)"
                : $"처치 못 함 ({seconds}초, 보스를 깨운 횟수 {wakes})");
            Console.WriteLine("  페이즈 시작: " + string.Join(", ", phaseStarts.Select(p => $"{p.Phase}페이즈 {p.Seconds:0.0}초")));
            Console.WriteLine($"  보스에게 준 피해 {dealt}");
            foreach (var bot in bots) Console.WriteLine($"  {bot.Player.Name}: 사망 {bot.Deaths}, 받은 피해 {bot.Taken}, 준 피해 {bot.Skills.Summary()}");
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
            var toTarget = target - player.Position;

            // A slime in the way gets hit first.
            var slime = world.Enemies.Where(e => Vec2.Distance(e.Position, player.Position) < 1.6f).OrderBy(e => Vec2.Distance(e.Position, player.Position)).FirstOrDefault();
            var facing = slime != null ? slime.Position - player.Position : toTarget;

            var input = Vec2.Zero;
            var escape = dodge ? Escape(world, player, reactionTicks) : Vec2.Zero;
            float gap = toTarget.Length - (boss?.Definition.Radius ?? 0) - World.PlayerRadius;
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

            var next = PlayerMotor.Step(world.Map, player.Position, input, player.Stats.MoveSpeed);
            world.MovePlayer(player.Id, next, facing, 1);
            bool inReach = slime != null || (boss != null && gap <= FightSim.Reach(player));
            world.SetAttacking(player.Id, inReach);
        }

        // Where to step to get out of every noticed telegraph and the path of nearby projectiles.
        static Vec2 Escape(World world, Player player, int reactionTicks)
        {
            var away = Vec2.Zero;
            foreach (var hazard in world.Hazards)
            {
                if (world.Tick - hazard.StartTick < reactionTicks) continue;
                if (!hazard.Contains(player.Position, World.PlayerRadius + 0.3f)) continue;
                var offset = player.Position - hazard.Position;
                if (hazard.Shape == HazardShape.Line)
                {
                    // Sideways, toward whichever edge is nearer.
                    var side = new Vec2(-hazard.Direction.Y, hazard.Direction.X);
                    away += side * (Vec2.Dot(offset, side) >= 0 ? 1 : -1);
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
            return away.Normalized;
        }
    }
}
