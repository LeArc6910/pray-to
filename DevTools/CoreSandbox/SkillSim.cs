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
                    string Cell(float value) => skill.Kind == SkillKind.Buff ? $"{value:0}%켬" : skill.Kind == SkillKind.Heal ? $"{value:0.0}%" : $"{value:0.0}";
                    Console.WriteLine($"{skill.Name,-10} {owner.Name,-7} {skill.Level,4} {kind,-6} {Cell(low.Single),12} {Cell(low.Pack),8} {Cell(high.Single),12} {Cell(high.Pack),8} {low.ManaPerSecond,7:0.0}");
                }
            }
            Console.WriteLine("회복은 최대 생명력의 초당 %(한 대상 칸). 버프는 켜져 있는 시간의 비율(한 대상 칸)");
        }

        static readonly (SupportKind Kind, string Name)[] SupportNames =
        {
            (SupportKind.Area, "확산"), (SupportKind.Multi, "분열"), (SupportKind.Haste, "가속"), (SupportKind.Power, "증폭"),
            (SupportKind.Fire, "화염"), (SupportKind.Cold, "냉기"), (SupportKind.Lightning, "번개"),
            (SupportKind.Pierce, "관통"), (SupportKind.Duration, "지속"), (SupportKind.Leech, "흡혈"),
        };

        // Every skill with each crystal that fits it (Docs/Classes.md, 스킬 변형), against the skill bare at the same
        // rank: how much the crystal multiplies its damage a second on the main dummy and on the whole pack (heal a
        // second for heals, time on for buffs), and the mana it then costs a second. Cold only slows, which dummies
        // that stand still do not show.
        public static void RunSupports(LootData loot, ClassData classes, int seconds, string[] gear, int rank)
        {
            var dummy = MonsterSheet.Parse(Header + "dummy,허수아비,1,100000000,0,0,1,0.4,0,0.5,0.5,1,1\n").Find("dummy");
            var monsters = MonsterSheet.Parse(Header + "dummy,허수아비,1,100000000,0,0,1,0.4,0,0.5,0.5,1,1\n");
            var crystals = SkillSupports.Ids.Select(loot.FindMaterial).Where(m => m != null).ToDictionary(m => SkillSupports.KindOf(m).Value);
            Console.WriteLine($"결정마다 {seconds}초, 스킬 {rank}단계, 장비 {string.Join(", ", gear)}. 칸: 한 대상 배율/무리 배율 (마나/초). 결정 없는 값에 대한 배율");
            Console.WriteLine($"{"스킬",-10} {"종류",-4} {"결정 없음",16} " + string.Join(" ", SupportNames.Select(n => $"{n.Name,17}")));
            float regen = PlayerStats.BaseManaRegen;
            var flagged = new List<string>();
            foreach (var owner in classes.Classes)
            {
                foreach (var skill in owner.Skills)
                {
                    var bare = Measure(loot, monsters, dummy, owner, skill, rank, seconds, gear);
                    string kind = skill.Kind switch
                    {
                        SkillKind.Nova => "둘레", SkillKind.Projectile => "투사체", SkillKind.Buff => "버프",
                        SkillKind.Summon => "소환", SkillKind.Dash => "돌진", _ => "회복",
                    };
                    var cells = new List<string>();
                    foreach (var (support, name) in SupportNames)
                    {
                        if (!SkillSupports.Fits(skill, support) || !crystals.TryGetValue(support, out var crystal))
                        {
                            cells.Add($"{"-",17}");
                            continue;
                        }
                        var with = Measure(loot, monsters, dummy, owner, skill, rank, seconds, gear, new[] { crystal });
                        float single = bare.Single > 0 ? with.Single / bare.Single : 0;
                        float pack = bare.Pack > 0 ? with.Pack / bare.Pack : single;
                        cells.Add($"{$"x{single:0.00}/x{pack:0.00} ({with.ManaPerSecond:0.0})",17}");
                        float best = Math.Max(single, pack);
                        // A skill that deals nothing (a dash only for moving) has nothing to compare.
                        if (bare.Single <= 0 && bare.Pack <= 0) continue;
                        if (best >= 1.6f) flagged.Add($"{skill.Name}+{name}: x{best:0.00} (너무 셈)");
                        // Cold slows and drinking heals: what they give does not show in damage.
                        else if (best <= 0.97f && support != SupportKind.Cold && support != SupportKind.Leech) flagged.Add($"{skill.Name}+{name}: x{best:0.00} (손해만)");
                    }
                    string Bare() => skill.Kind == SkillKind.Buff ? $"{bare.Single:0}%켬" : skill.Kind == SkillKind.Heal ? $"{bare.Single:0.0}%/초" : $"{bare.Single:0}/{bare.Pack:0}";
                    Console.WriteLine($"{skill.Name,-10} {kind,-4} {$"{Bare()} ({bare.ManaPerSecond:0.0})",16} " + string.Join(" ", cells));
                }
            }
            Console.WriteLine($"마나는 채워 두고 쟀다. 기본 마나 재생은 초당 {regen:0}");
            Console.WriteLine(flagged.Count == 0 ? "눈에 띄는 것 없음" : "눈에 띄는 것(x1.6 이상, x0.97 이하):\n  " + string.Join("\n  ", flagged));
            if (rank >= 5) Best(loot, classes, monsters, dummy, crystals, seconds, gear, rank);
        }

        // At rank 5 a skill holds three crystals: for each skill, the three that make it strongest on the pack and on
        // one target, against the skill bare, and the mana that costs.
        static void Best(LootData loot, ClassData classes, MonsterData monsters, MonsterDefinition dummy, Dictionary<SupportKind, MaterialDefinition> crystals,
            int seconds, string[] gear, int rank)
        {
            Console.WriteLine($"\n결정 셋(스킬 {rank}단계): 무리에 가장 센 셋과 한 대상에 가장 센 셋. 배율은 결정 없을 때에 대한 것");
            foreach (var owner in classes.Classes)
            {
                foreach (var skill in owner.Skills)
                {
                    var bare = Measure(loot, monsters, dummy, owner, skill, rank, seconds, gear);
                    if (bare.Single <= 0 && bare.Pack <= 0) continue;
                    var fits = SupportNames.Where(n => SkillSupports.Fits(skill, n.Kind) && crystals.ContainsKey(n.Kind)).ToList();
                    if (fits.Count < 3) continue;
                    (string Names, float Single, float Pack, float Mana) bestPack = default, bestSingle = default;
                    for (int a = 0; a < fits.Count; a++)
                    for (int b = a + 1; b < fits.Count; b++)
                    for (int c = b + 1; c < fits.Count; c++)
                    {
                        var set = new[] { fits[a], fits[b], fits[c] };
                        var with = Measure(loot, monsters, dummy, owner, skill, rank, seconds, gear, set.Select(n => crystals[n.Kind]));
                        float single = bare.Single > 0 ? with.Single / bare.Single : 0;
                        float pack = bare.Pack > 0 ? with.Pack / bare.Pack : single;
                        string names = string.Join("+", set.Select(n => n.Name));
                        if (pack > bestPack.Pack) bestPack = (names, single, pack, with.ManaPerSecond);
                        if (single > bestSingle.Single) bestSingle = (names, single, pack, with.ManaPerSecond);
                    }
                    Console.WriteLine($"{skill.Name,-10} 무리 {bestPack.Names,-12} x{bestPack.Pack:0.00} (마나 {bestPack.Mana:0.0}/초)  한 대상 {bestSingle.Names,-12} x{bestSingle.Single:0.00} (마나 {bestSingle.Mana:0.0}/초)");
                }
            }
        }

        static (float Single, float Pack, float ManaPerSecond) Measure(LootData loot, MonsterData monsters, MonsterDefinition dummy, ClassDefinition owner,
            SkillDefinition skill, int rank, int seconds, string[] gear, IEnumerable<MaterialDefinition> supports = null)
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
            if (supports != null) player.Supports[skill] = supports.ToList();
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
            int buffed = 0;
            float maxHealth = player.Stats.MaxHealth;
            for (int tick = 0; tick < seconds * World.TicksPerSecond; tick++)
            {
                player.Mana = player.Stats.MaxMana;
                player.Health = Math.Min(player.Health, maxHealth);
                if (skill.Kind == SkillKind.Heal) player.Health = 1;
                float before = player.Health;
                if (player.CooldownTicks(0) == 0 && world.UseSkill(player.Id, 0, new Vec2(1, 0)))
                {
                    mana += player.ManaCost(skill);
                    if (skill.Kind == SkillKind.Heal) single += (long)Math.Round((player.Health - before) / maxHealth * 1000);
                }
                if (player.HasBuff(0)) buffed++;
                player.Position = centre;
                world.Step();
                foreach (var e in world.TakeEvents())
                {
                    // The skill's hits, its lightning's jumps, and burns (which carry no cause: nothing else here
                    // hits for the player).
                    if (e.Type != SimEventType.Hit || e.Source != player.Id || e.Cause != 1 && e.Cause != 0) continue;
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
            // A buff's worth is the share of the time it is on.
            if (skill.Kind == SkillKind.Buff) return (buffed * 100f / (seconds * World.TicksPerSecond), 0, mana / seconds);
            return (single / (float)seconds, pack / (float)seconds, mana / seconds);
        }
    }
}
