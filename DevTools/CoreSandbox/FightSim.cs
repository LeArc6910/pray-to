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
        public static void Run(LootData loot, MonsterData monsters, int seconds, ulong seed, string[] gear, ClassDefinition[] classes, bool dodge)
        {
            var generated = WorldGenerator.Generate(seed);
            var world = new World(generated.Map, generated.Spawn, monsters, seed, new WorldConfig { EnemyTarget = 40, SpawnDistance = 6 }, loot);
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

            int kills = 0, deaths = 0, dealt = 0, taken = 0, swings = 0, hits = 0;
            var killsByMonster = new Dictionary<string, int>();
            var takenByMonster = new Dictionary<string, int>();
            var names = new Dictionary<int, string>();
            // What hurt the bot, by kind and by which of its attacks (monster_attacks.csv): one attack past 60% of a
            // kind's damage means its telegraph is too short (Docs/WorldRework.md 봇과 시험).
            var kinds = new Dictionary<int, MonsterDefinition>();
            var takenByAttack = new Dictionary<string, Dictionary<string, int>>();
            for (int tick = 0; tick < seconds * World.TicksPerSecond; tick++)
            {
                if (tick % 10 == 0) toEnemies.Build(world.Enemies.Select(e => e.Position), 64);
                Drive(world, player, toEnemies, dodge);
                var nearest = world.Enemies.OrderBy(e => Vec2.Distance(e.Position, player.Position)).FirstOrDefault();
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
            Console.WriteLine($"준 피해 {dealt} (초당 {dealt / (double)seconds:0.0}), 기본 공격 {swings}회, 맞힌 횟수 {hits}회 (한 번에 여럿 맞힐 수 있음)");
            Console.WriteLine($"  나눠 보면: {skills.Summary()}");
            Console.WriteLine($"받은 피해 {taken} (초당 {taken / (double)seconds:0.0}), 최대 생명력 {player.Stats.MaxHealth:0}, 마신 물약 {skills.Drunk}개, 구르기 {player.Rolls}번(흘린 타격 {player.Dodges})");
            Console.WriteLine($"주운 것: 아이템 {player.Inventory.Items.Count}개, 룬 {player.Inventory.Runes.Values.Sum()}개, 남은 물약 {player.Inventory.Consumables.Values.Sum()}개 (바닥에 남은 것 {world.GroundLoot.Count}개)");
            foreach (var item in player.Inventory.Items.Where(i => i.Rarity >= Rarity.Rare)) Console.WriteLine("  " + ItemText.Describe(item)[0]);
        }

        public static string ClassNames(ClassDefinition[] classes) => classes.Length == 0 ? "없음" : string.Join(" + ", classes.Select(c => c.Name));

        internal static void Drive(World world, Player player, FlowField toEnemies, bool dodge)
        {
            if (player.IsDead) return;
            var target = world.Enemies.OrderBy(e => Vec2.Distance(e.Position, player.Position)).FirstOrDefault();
            if (target == null)
            {
                world.SetAttacking(player.Id, false);
                return;
            }

            var toTarget = target.Position - player.Position;
            float gap = toTarget.Length - target.Definition.Radius - World.PlayerRadius;
            float reach = Reach(player);
            var input = Vec2.Zero;
            var escape = dodge ? BossSim.Escape(world, player, World.Ticks(0.25f)) : Vec2.Zero;
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
            else if (gap > reach * 0.7f)
            {
                input = gap < 2 ? toTarget.Normalized : toEnemies.DirectionFrom(player.Position);
                if (input.LengthSquared < 1e-6f) input = toTarget.Normalized;
            }

            RollBot.Move(world, player, input, toTarget, escape, dodge);
            world.SetAttacking(player.Id, gap <= reach);
        }

        // How close a bot gets before attacking: in swinging reach, or well inside a shot's range so the target
        // cannot walk out of it.
        public static float Reach(Player player) =>
            player.Stats.Attack.Shoots ? player.Stats.Attack.Range * 0.6f : World.MeleeReach;
    }
}
