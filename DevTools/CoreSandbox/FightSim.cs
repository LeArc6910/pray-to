using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // One bot player in a generated cave full of monsters. It walks to the nearest enemy, holds attack once in
    // reach (a bow or staff stops further off and steps back from enemies that get close), uses its class skills
    // when ready and, unless told not to, backs out of wind-ups. Shows how fast gear
    // and classes kill and how much the bot gets hurt.
    static class FightSim
    {
        // only: instead of what the cave brings, these kinds alone, kept at four round the bot (--monsters a,b: how a
        // biome's own monsters fight, whatever zone they live in; Docs/MonsterPatterns.md).
        public static void Run(LootData loot, MonsterData monsters, int seconds, ulong seed, string[] gear, ClassDefinition[] classes, bool dodge, string[] only = null)
        {
            var generated = WorldGenerator.Generate(seed);
            var kindsOnly = (only ?? Array.Empty<string>()).Select(id => monsters.Find(id) ?? throw new ArgumentException("몬스터가 없습니다: " + id)).ToList();
            var world = new World(generated.Map, generated.Spawn, monsters, seed, new WorldConfig { EnemyTarget = kindsOnly.Count > 0 ? 0 : 40, SpawnDistance = 6 }, loot);
            var bring = new GameRandom(seed, 7);
            var random = new GameRandom(seed, 99);
            var generator = new LootGenerator(loot);
            var equipment = new Equipment();
            foreach (string id in gear)
            {
                var itemBase = loot.FindBase(id);
                equipment.Equip(generator.RollItem(itemBase, itemBase.Level, Rarity.Common, random));
            }
            var player = world.AddPlayer("bot", equipment, classes);
            var skills = new SkillBot(player, loot);
            var toEnemies = new FlowField(world.Map);

            int kills = 0, deaths = 0, dealt = 0, taken = 0, swings = 0, hits = 0, guarded = 0;
            var killsByMonster = new Dictionary<string, int>();
            var takenByMonster = new Dictionary<string, int>();
            var names = new Dictionary<int, string>();
            // What hurt the bot, by kind and by which of its attacks (monster_attacks.csv): one attack past 60% of a
            // kind's damage means its telegraph is too short (Docs/WorldRework.md 봇과 시험).
            var kinds = new Dictionary<int, MonsterDefinition>();
            var takenByAttack = new Dictionary<string, Dictionary<string, int>>();
            for (int tick = 0; tick < seconds * World.TicksPerSecond; tick++)
            {
                if (kindsOnly.Count > 0 && tick % 30 == 0 && world.Enemies.Count(e => !e.IsDead) < 4)
                {
                    // Somewhere open six to eight tiles off, so they come as a field's monsters do.
                    for (int tries = 0; tries < 20; tries++)
                    {
                        var at = player.Position + Vec2.FromAngle(bring.NextFloat() * 2 * MathF.PI) * (6 + bring.NextFloat() * 2);
                        if (!world.Map.InBounds(TileMap.Cell(at.X), TileMap.Cell(at.Y)) || Collision.Overlaps(world.Map, at, 0.5f, monster: true)) continue;
                        var kind = kindsOnly[bring.Next(kindsOnly.Count)];
                        // A big body (L, XL) only where it fits (World.SpawnFitting, B0b).
                        if (kind.Big && world.SpawnFitting(kind, at, reach: 2) == null) continue;
                        if (!kind.Big) world.SpawnEnemy(kind, at);
                        break;
                    }
                }
                if (tick % 10 == 0) toEnemies.Build(world.Enemies.Where(e => !e.Away).Select(e => e.Position), 64);
                Drive(world, player, toEnemies, dodge);
                var nearest = world.Enemies.Where(e => !e.Away).OrderBy(e => Vec2.Distance(e.Position, player.Position)).FirstOrDefault();
                skills.Use(world, nearest?.Position ?? player.Position, nearest != null && Vec2.Distance(nearest.Position, player.Position) < 8);
                foreach (var enemy in world.Enemies)
                {
                    names[enemy.Id] = enemy.Definition.Name;
                    kinds[enemy.Id] = enemy.Definition;
                }
                world.Step();
                foreach (var e in world.TakeEvents())
                {
                    skills.Count(e);
                    if (e.Type == SimEventType.Died && names.TryGetValue(e.Source, out string killed)) killsByMonster[killed] = killsByMonster.GetValueOrDefault(killed) + 1;
                    if (e.Type == SimEventType.Hit && e.Target == player.Id && names.TryGetValue(e.Source, out string hitter)) takenByMonster[hitter] = takenByMonster.GetValueOrDefault(hitter) + e.Amount;
                    if (e.Type == SimEventType.Hit && e.Target == player.Id && kinds.TryGetValue(e.Source, out var kind) && kind.Attacks.Count > 0)
                    {
                        string attack = e.Cause >= 1 && e.Cause <= kind.Attacks.Count ? kind.Attacks[e.Cause - 1].Name : "그 밖(화상, 웅덩이)";
                        if (!takenByAttack.TryGetValue(kind.Name, out var byAttack)) takenByAttack[kind.Name] = byAttack = new Dictionary<string, int>();
                        byAttack[attack] = byAttack.GetValueOrDefault(attack) + e.Amount;
                    }
                    if (e.Type == SimEventType.Attack && e.Source == player.Id) swings++;
                    if (e.Type == SimEventType.Guarded) guarded++;
                    if (e.Type == SimEventType.Hit && e.Source == player.Id)
                    {
                        hits++;
                        dealt += e.Amount;
                    }
                    if (e.Type == SimEventType.Hit && e.Target == player.Id) taken += e.Amount;
                    if (e.Type == SimEventType.Died && e.Source == player.Id) deaths++;
                    else if (e.Type == SimEventType.Died) kills++;
                }
            }

            Console.WriteLine($"seed = {seed}, {seconds}초, 장비: {string.Join(", ", gear)}, 클래스: {ClassNames(classes)}, 피하기: {(dodge ? "켬" : "끔")}, 구르기: {(dodge && RollBot.Enabled ? "켬" : "끔")}");
            foreach (var item in equipment.Items) Console.WriteLine("  " + ItemText.Describe(item)[0] + " / " + string.Join(", ", item.Implicits.Select(StatText.Describe)));
            Console.WriteLine($"처치 {kills} (분당 {kills * 60.0 / seconds:0.0}), 사망 {deaths}");
            Console.WriteLine("  몬스터별 처치: " + string.Join(", ", killsByMonster.Select(p => $"{p.Key} {p.Value}")) + " / 받은 피해: " + string.Join(", ", takenByMonster.Select(p => $"{p.Key} {p.Value}")));
            foreach (var pair in takenByAttack)
            {
                int all = pair.Value.Values.Sum();
                Console.WriteLine($"  {pair.Key}의 공격별: " + string.Join(", ", pair.Value.OrderByDescending(p => p.Value).Select(p => $"{p.Key} {p.Value} ({p.Value * 100 / Math.Max(1, all)}%)")));
            }
            Console.WriteLine($"준 피해 {dealt} (초당 {dealt / (double)seconds:0.0}), 기본 공격 {swings}회, 맞힌 횟수 {hits}회 (한 번에 여럿 맞힐 수 있음)"
                + (guarded > 0 ? $", 등딱지·방패에 막힌 타격 {guarded}회" : ""));
            Console.WriteLine($"  나눠 보면: {skills.Summary()}");
            Console.WriteLine($"받은 피해 {taken} (초당 {taken / (double)seconds:0.0}), 최대 생명력 {player.Stats.MaxHealth:0}, 마신 물약 {skills.Drunk}개, 구르기 {player.Rolls}번(흘린 타격 {player.Dodges})");
            Console.WriteLine($"주운 것: 아이템 {player.Inventory.Items.Count}개, 룬 {player.Inventory.Runes.Values.Sum()}개, 남은 물약 {player.Inventory.Consumables.Values.Sum()}개 (바닥에 남은 것 {world.GroundLoot.Count}개)");
            foreach (var item in player.Inventory.Items.Where(i => i.Rarity >= Rarity.Rare)) Console.WriteLine("  " + ItemText.Describe(item)[0]);
        }

        public static string ClassNames(ClassDefinition[] classes) => classes.Length == 0 ? "없음" : string.Join(" + ", classes.Select(c => c.Name));

        // reaction: the seconds a telegraph is up before the bot steps out of it (--reaction, as --boss has it).
        internal static void Drive(World world, Player player, FlowField toEnemies, bool dodge, float reaction = 0.25f)
        {
            if (player.IsDead) return;
            // One under the ground cannot be hit: the bot goes for what it can reach (and steps off marks meanwhile).
            // One that mends or rouses the rest is gone for first when it is near (BotPilot.First), else the nearest.
            var nearestOf = world.Enemies.Where(e => !e.Away).OrderBy(e => Vec2.Distance(e.Position, player.Position)).FirstOrDefault();
            var target = world.Enemies.Where(e => !e.Away)
                .OrderBy(e => BotPilot.First(e) && Vec2.Distance(e.Position, player.Position) < 6 ? 0 : 1)
                .ThenBy(e => Vec2.Distance(e.Position, player.Position)).FirstOrDefault();
            if (target == null)
            {
                world.SetAttacking(player.Id, false);
                var flee = dodge ? BossSim.Escape(world, player, World.Ticks(reaction)) : Vec2.Zero;
                // Nothing to hit but mounds: toward the nearest, as a player who sees one goes to spring it.
                var mound = world.Enemies.Where(e => e.Buried).OrderBy(e => Vec2.Distance(e.Position, player.Position)).FirstOrDefault();
                var go = flee.LengthSquared > 0 || mound == null ? flee : (mound.Position - player.Position).Normalized;
                RollBot.Move(world, player, go, go, flee, dodge);
                return;
            }

            var toTarget = target.Position - player.Position;
            float gap = toTarget.Length - target.Definition.Radius - World.PlayerRadius;
            float reach = Reach(player);
            var input = Vec2.Zero;
            var escape = dodge ? BossSim.Escape(world, player, World.Ticks(reaction)) : Vec2.Zero;
            if (escape.LengthSquared > 0)
            {
                // Out of a charger's marked path or a shot's way.
                input = escape;
            }
            else if (dodge && target.State == EnemyState.Windup && target.Definition.Behavior == MonsterBehavior.Melee && gap < target.Definition.Reach + 0.6f)
            {
                input = -toTarget.Normalized;
            }
            else if (player.Stats.Attack.Shoots && gap < 1.5f)
            {
                input = -toTarget.Normalized;
            }
            else if (dodge && gap <= reach && BotPilot.Flank(target, player.Position) is Vec2 round && round.LengthSquared > 0)
            {
                // In reach of a guard's front: round to its side while striking.
                input = round;
            }
            else if (gap > reach * 0.7f)
            {
                // Straight at one that is not the nearest (a mender gone for first) while rock is not in the way, else
                // the way to the nearest.
                bool straight = gap < 2 || !ReferenceEquals(target, nearestOf)
                    && !Collision.Overlaps(world.Map, player.Position + toTarget.Normalized * 0.6f, World.PlayerRadius);
                input = straight ? toTarget.Normalized : toEnemies.DirectionFrom(player.Position);
                if (input.LengthSquared < 1e-6f) input = toTarget.Normalized;
            }

            // Not through what burns or bites on the floor (a magma slime's trail, a spore cloud): round it.
            if (dodge && escape.LengthSquared == 0 && input.LengthSquared > 0) input = RoundPools(world, player, input.Normalized);
            RollBot.Move(world, player, input, toTarget, escape, dodge);
            world.SetAttacking(player.Id, gap <= reach);
        }

        static bool Harms(World world, Vec2 at) => world.Hazards.Any(h => (h.Shape == HazardShape.Fire || h.Shape == HazardShape.Spore || h.Shape == HazardShape.Venom)
            && Vec2.Distance(h.Position, at) <= h.Size + 0.15f);

        // The way the bot meant to go, turned aside while the next step of it lands in a pool; stood still if every way does.
        static Vec2 RoundPools(World world, Player player, Vec2 way)
        {
            foreach (float turn in new[] { 0f, 0.7f, -0.7f, 1.4f, -1.4f })
            {
                var turned = Vec2.FromAngle(way.Angle + turn);
                if (!Harms(world, player.Position + turned * 0.5f) && !Collision.Overlaps(world.Map, player.Position + turned * 0.5f, World.PlayerRadius)) return turned;
            }
            return Harms(world, player.Position) ? way : Vec2.Zero;
        }

        // How close a bot gets before attacking: in swinging reach, or well inside a shot's range so the target
        // cannot walk out of it.
        public static float Reach(Player player) =>
            player.Stats.Attack.Shoots ? player.Stats.Attack.Range * 0.6f : World.MeleeReach;
    }
}
