using System;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // One bot player in a generated cave full of monsters. It walks to the nearest enemy, holds attack once in
    // reach, uses its class skills when ready and, unless told not to, backs out of wind-ups. Shows how fast gear
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
            var skills = new SkillBot(player);
            var toEnemies = new FlowField(world.Map);

            int kills = 0, deaths = 0, dealt = 0, taken = 0, swings = 0, hits = 0;
            for (int tick = 0; tick < seconds * World.TicksPerSecond; tick++)
            {
                if (tick % 10 == 0) toEnemies.Build(world.Enemies.Select(e => e.Position), 64);
                Drive(world, player, toEnemies, dodge);
                var nearest = world.Enemies.OrderBy(e => Vec2.Distance(e.Position, player.Position)).FirstOrDefault();
                if (nearest != null) skills.Use(world, nearest.Position, Vec2.Distance(nearest.Position, player.Position) < 8);
                world.Step();
                foreach (var e in world.TakeEvents())
                {
                    skills.Count(e);
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

            Console.WriteLine($"seed = {seed}, {seconds}초, 장비: {string.Join(", ", gear)}, 클래스: {ClassNames(classes)}, 피하기: {(dodge ? "켬" : "끔")}");
            foreach (var item in equipment.Items) Console.WriteLine("  " + ItemText.Describe(item)[0] + " / " + string.Join(", ", item.Implicits.Select(StatText.Describe)));
            Console.WriteLine($"처치 {kills} (분당 {kills * 60.0 / seconds:0.0}), 사망 {deaths}");
            Console.WriteLine($"준 피해 {dealt} (초당 {dealt / (double)seconds:0.0}), 휘두르기 {swings}회, 맞힌 횟수 {hits}회 (한 번에 여럿 맞힐 수 있음)");
            Console.WriteLine($"  나눠 보면: {skills.Summary()}");
            Console.WriteLine($"받은 피해 {taken} (초당 {taken / (double)seconds:0.0}), 최대 생명력 {player.Stats.MaxHealth:0}");
            Console.WriteLine($"주운 것: 아이템 {player.Inventory.Items.Count}개, 룬 {player.Inventory.Runes.Values.Sum()}개 (바닥에 남은 것 {world.GroundLoot.Count}개)");
            foreach (var item in player.Inventory.Items.Where(i => i.Rarity >= Rarity.Rare)) Console.WriteLine("  " + ItemText.Describe(item)[0]);
        }

        public static string ClassNames(ClassDefinition[] classes) => classes.Length == 0 ? "없음" : string.Join(" + ", classes.Select(c => c.Name));

        static void Drive(World world, Player player, FlowField toEnemies, bool dodge)
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
            var input = Vec2.Zero;
            if (dodge && target.State == EnemyState.Windup && gap < target.Definition.Reach + 0.6f)
            {
                input = -toTarget.Normalized;
            }
            else if (gap > World.MeleeReach * 0.7f)
            {
                input = gap < 2 ? toTarget.Normalized : toEnemies.DirectionFrom(player.Position);
                if (input.LengthSquared < 1e-6f) input = toTarget.Normalized;
            }

            var next = PlayerMotor.Step(world.Map, player.Position, input, player.Stats.MoveSpeed);
            world.MovePlayer(player.Id, next, toTarget, 1);
            world.SetAttacking(player.Id, gap <= World.MeleeReach);
        }
    }
}
