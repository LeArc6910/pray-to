using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // Every class skill on its own, used whenever it is ready for a while, against the same pack of dummies: one
    // straight ahead 1.6 tiles off (the main target), four more in a line behind it and eight in a ring 2.2 tiles
    // round the player. Mana is topped up, so the numbers are what each skill gives for its cooldown; the mana it
    // costs a second is listed beside them. The player wears the same plain gear for every class and stands still,
    // put back in place after a dash. Shows which skills are too strong or too weak for the level they open at, at
    // rank 1 and rank 5. See Docs/Classes.md (스킬 균형).
    static class SkillSim
    {
        const string Header = "id,name,level,health,damage,armor,speed,radius,aggro,reach,windup,cooldown,weight\n";

        public static void Run(LootData loot, ClassData classes, int seconds, string[] gear)
        {
            var dummy = MonsterSheet.Parse(Header + "dummy,허수아비,1,100000000,0,0,1,0.4,0,0.5,0.5,1,1\n").Find("dummy");
            var monsters = MonsterSheet.Parse(Header + "dummy,허수아비,1,100000000,0,0,1,0.4,0,0.5,0.5,1,1\n");
            Console.WriteLine($"스킬마다 {seconds}초, 장비 {string.Join(", ", gear)}, 마나는 채워 둔다. 한 대상: 앞 1.6칸의 허수아비, 무리: 열셋 모두(초당 피해)");
            Console.WriteLine($"{"스킬",-10} {"클래스",-7} {"레벨",4} {"종류",-6} {"1단계 한 대상",12} {"무리",8} {"5단계 한 대상",12} {"무리",8} {"마나/초",7}");
            foreach (var owner in classes.Classes)
            {
                foreach (var skill in owner.Skills)
                {
                    var low = Measure(loot, monsters, dummy, owner, skill, 1, seconds, gear);
                    var high = Measure(loot, monsters, dummy, owner, skill, 5, seconds, gear);
                    string kind = skill.Kind switch
                    {
                        SkillKind.Nova => "둘레",
                        SkillKind.Projectile => "투사체",
                        SkillKind.Buff => "버프",
                        SkillKind.Summon => "소환",
                        SkillKind.Dash => "돌진",
                        _ => "회복",
                    };
                    string Cell(float value) => skill.Kind == SkillKind.Buff ? "-" : skill.Kind == SkillKind.Heal ? $"{value:0.0}%" : $"{value:0.0}";
                    Console.WriteLine($"{skill.Name,-10} {owner.Name,-7} {skill.Level,4} {kind,-6} {Cell(low.Single),12} {Cell(low.Pack),8} {Cell(high.Single),12} {Cell(high.Pack),8} {low.ManaPerSecond,7:0.0}");
                }
            }
            Console.WriteLine("회복은 최대 생명력의 초당 %(한 대상 칸). 버프는 피해를 주지 않는다");
        }

        static (float Single, float Pack, float ManaPerSecond) Measure(LootData loot, MonsterData monsters, MonsterDefinition dummy, ClassDefinition owner,
            SkillDefinition skill, int rank, int seconds, string[] gear)
        {
            var map = new TileMap(31, 31, TileType.Floor);
            for (int i = 0; i < 31; i++)
            {
                map[i, 0] = map[i, 30] = map[0, i] = map[30, i] = TileType.Wall;
            }
            var centre = TileMap.Center(15, 15);
            var world = new World(map, centre, monsters, 1, new WorldConfig { EnemyTarget = 0 }, loot);
            var random = new GameRandom(7, 99);
            var generator = new LootGenerator(loot);
            var equipment = new Equipment();
            foreach (string id in gear)
            {
                var itemBase = loot.FindBase(id);
                equipment.Equip(generator.RollItem(itemBase, itemBase.Level, Rarity.Common, random));
            }
            var player = world.AddPlayer("bot", equipment, owner);
            foreach (var each in owner.Skills) player.SkillRanks[each] = rank;
            var all = ClassRules.AllSkillsFor(player.Classes);
            var order = new List<int> { all.IndexOf(skill) };
            order.AddRange(Enumerable.Range(0, all.Count).Where(i => !order.Contains(i)).Take(ClassRules.SkillSlots - 1));
            player.ChangeKit(player.Classes, order);
            player.ClearCooldown(skill);
            player.Facing = new Vec2(1, 0);

            var places = new List<Vec2> { centre + new Vec2(1.6f, 0) };
            for (int i = 1; i <= 4; i++) places.Add(centre + new Vec2(1.6f + i * 1.2f, 0));
            for (int i = 0; i < 8; i++)
            {
                double angle = Math.PI / 4 * i + Math.PI / 8;
                places.Add(centre + new Vec2((float)Math.Cos(angle) * 2.2f, (float)Math.Sin(angle) * 2.2f));
            }
            var dummies = places.Select(p => world.SpawnEnemy(dummy, p)).ToList();
            var main = dummies[0];

            long single = 0, pack = 0;
            float mana = 0;
            float maxHealth = player.Stats.MaxHealth;
            for (int tick = 0; tick < seconds * World.TicksPerSecond; tick++)
            {
                player.Mana = player.Stats.MaxMana;
                player.Health = Math.Min(player.Health, maxHealth);
                if (skill.Kind == SkillKind.Heal) player.Health = 1;
                float before = player.Health;
                if (player.CooldownTicks(0) == 0 && world.UseSkill(player.Id, 0, new Vec2(1, 0)))
                {
                    mana += skill.Mana;
                    if (skill.Kind == SkillKind.Heal) single += (long)Math.Round((player.Health - before) / maxHealth * 1000);
                }
                player.Position = centre;
                world.Step();
                foreach (var e in world.TakeEvents())
                {
                    if (e.Type != SimEventType.Hit || e.Source != player.Id || e.Cause != 1) continue;
                    pack += e.Amount;
                    if (e.Target == main.Id) single += e.Amount;
                }
                for (int i = 0; i < dummies.Count; i++)
                {
                    dummies[i].Position = places[i];
                    dummies[i].Health = dummy.Health;
                }
            }
            if (skill.Kind == SkillKind.Heal) return (single / 10f / seconds, 0, mana / seconds);
            return (single / (float)seconds, pack / (float)seconds, mana / seconds);
        }
    }
}
