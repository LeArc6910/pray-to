using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;

namespace CoreSandbox
{
    // dotnet run -- --item [level] [seed]           rolls one item and prints it with its affix tiers
    // dotnet run -- --loot [level] [count] [seed]   rarity, socket, affix, tier and rune spread of many drops
    // dotnet run -- --progression [count] [seed]    rare weapon damage and chest armor by item level (the steps)
    // add --rarity <percent> to --item or --loot for the finder's item rarity bonus
    // dotnet run -- --fight [seconds] [seed]        a bot fights slimes in a generated cave
    // add --gear <base,base> to --fight to change its gear (copper_sword by default), --no-dodge to stand still
    // dotnet run -- --boss [seconds] [seed]         a bot party against the first boss
    // add --players <n> and --reaction <seconds> to --boss, as well as --gear and --no-dodge
    // add --classes <class,class> to --fight or --boss to give the bots classes; they use every skill when it is ready
    static class Program
    {
        static void Main(string[] args)
        {
            int itemRarity = 0;
            int rarityFlag = Array.IndexOf(args, "--rarity");
            if (rarityFlag >= 0)
            {
                itemRarity = int.Parse(args[rarityFlag + 1]);
                args = args.Where((_, i) => i != rarityFlag && i != rarityFlag + 1).ToArray();
            }

            int Number(int index, int fallback) => args.Length > index ? int.Parse(args[index]) : fallback;
            ulong Seed(int index) => args.Length > index ? ulong.Parse(args[index]) : (ulong)Environment.TickCount64;

            string[] gear = { "copper_sword" };
            int gearFlag = Array.IndexOf(args, "--gear");
            if (gearFlag >= 0)
            {
                gear = args[gearFlag + 1].Split(',');
                args = args.Where((_, i) => i != gearFlag && i != gearFlag + 1).ToArray();
            }
            int players = 1;
            int playersFlag = Array.IndexOf(args, "--players");
            if (playersFlag >= 0)
            {
                players = int.Parse(args[playersFlag + 1]);
                args = args.Where((_, i) => i != playersFlag && i != playersFlag + 1).ToArray();
            }
            float reaction = 0.25f;
            int reactionFlag = Array.IndexOf(args, "--reaction");
            if (reactionFlag >= 0)
            {
                reaction = float.Parse(args[reactionFlag + 1], System.Globalization.CultureInfo.InvariantCulture);
                args = args.Where((_, i) => i != reactionFlag && i != reactionFlag + 1).ToArray();
            }
            string[] classIds = Array.Empty<string>();
            int classesFlag = Array.IndexOf(args, "--classes");
            if (classesFlag >= 0)
            {
                classIds = args[classesFlag + 1].Split(',');
                args = args.Where((_, i) => i != classesFlag && i != classesFlag + 1).ToArray();
            }
            bool dodge = !args.Contains("--no-dodge");
            args = args.Where(a => a != "--no-dodge").ToArray();

            var data = LoadLoot();
            switch (args.Length > 0 ? args[0] : "")
            {
                case "--item":
                    PrintItem(data, Number(1, 10), Seed(2), itemRarity);
                    break;
                case "--loot":
                    PrintLoot(data, Number(1, 10), Number(2, 10000), Seed(3), itemRarity);
                    break;
                case "--progression":
                    PrintProgression(data, Number(1, 2000), Seed(2));
                    break;
                case "--fight":
                    FightSim.Run(data, LoadMonsters(), Number(1, 120), Seed(2), gear, LoadClasses(classIds), dodge);
                    break;
                case "--boss":
                    var monsters = LoadMonsters();
                    BossSim.Run(data, monsters, LoadBosses(monsters), Number(1, 300), Seed(2), players, gear, LoadClasses(classIds), reaction, dodge);
                    break;
                default:
                    Console.WriteLine("usage: dotnet run -- --item [level] [seed] | --loot [level] [count] [seed] | --progression [count] [seed]");
                    Console.WriteLine("       add --rarity <percent> to --item or --loot");
                    Console.WriteLine("       dotnet run -- --fight [seconds] [seed] [--gear copper_sword,copper_armor] [--classes warrior,fire_mage] [--no-dodge]");
                    Console.WriteLine("       dotnet run -- --boss [seconds] [seed] [--players 2] [--reaction 0.25] [--gear ...] [--classes ...] [--no-dodge]");
                    break;
            }
        }

        static void PrintItem(LootData data, int level, ulong seed, int itemRarity)
        {
            Console.WriteLine($"level = {level}, seed = {seed}, item rarity = {itemRarity}%");
            var item = new LootGenerator(data).RollItem(level, new GameRandom(seed), itemRarity);
            foreach (string line in ItemText.Describe(item, showTiers: true)) Console.WriteLine(line);
        }

        static void PrintLoot(LootData data, int level, int count, ulong seed, int itemRarity)
        {
            Console.WriteLine($"level = {level}, items = {count}, seed = {seed}, item rarity = {itemRarity}%");
            var generator = new LootGenerator(data);
            var random = new GameRandom(seed);
            var items = Enumerable.Range(0, count).Select(_ => generator.RollItem(level, random, itemRarity)).ToList();
            var affixes = items.SelectMany(item => item.Affixes).ToList();
            var runes = Enumerable.Range(0, count).Select(_ => generator.RollRune(level, random)).Where(rune => rune != null).ToList();

            PrintSpread("등급", items.GroupBy(i => i.Rarity).OrderBy(g => g.Key), ItemText.RarityName, count);
            PrintSpread("소켓", items.GroupBy(i => i.Sockets.Count).OrderBy(g => g.Key), sockets => $"{sockets}개", count);
            PrintSpread(
                $"접두사·접미사 (아이템당 평균 {affixes.Count / (double)count:0.00}개)",
                affixes.GroupBy(a => a.Affix).OrderByDescending(g => g.Count()),
                affix => affix.Name,
                affixes.Count);
            PrintSpread("티어", affixes.GroupBy(a => a.Tier.Number).OrderBy(g => g.Key), tier => $"T{tier}", affixes.Count);
            PrintSpread($"룬 ({count}번 굴림)", runes.GroupBy(r => r).OrderByDescending(g => g.Count()), rune => rune.Name, runes.Count);

            Console.WriteLine();
            Console.WriteLine("등급별 예시");
            foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
            {
                var sample = items.FirstOrDefault(item => item.Rarity == rarity);
                if (sample == null) continue;
                foreach (string line in ItemText.Describe(sample, showTiers: true)) Console.WriteLine(line);
            }
        }

        static void PrintSpread<TKey, TItem>(string title, IEnumerable<IGrouping<TKey, TItem>> groups, Func<TKey, string> label, int total)
        {
            Console.WriteLine();
            Console.WriteLine(title);
            if (total == 0) return;
            foreach (var group in groups)
            {
                double share = group.Count() / (double)total;
                Console.WriteLine($"  {Pad(label(group.Key), 12)} {share * 100,5:0.0}%  {new string('#', (int)Math.Round(share * 40))}");
            }
        }

        // Rarity is fixed to Rare so the steps between base tiers are not hidden by rarity luck.
        static void PrintProgression(LootData data, int count, ulong seed)
        {
            Console.WriteLine($"희귀 등급 {count}개씩, seed = {seed}. 무기 피해는 무기 태그 기준, 원소 피해 증가는 빠짐");
            var generator = new LootGenerator(data);
            var random = new GameRandom(seed);
            var rows = new[] { 1, 5, 9, 10, 15, 19, 20, 25, 29, 30, 35, 40 }
                .Select(level => (
                    Level: level,
                    Weapon: Spread(generator, random, level, ItemSlot.Weapon, StatType.Damage, count),
                    Armor: Spread(generator, random, level, ItemSlot.Chest, StatType.Armor, count)))
                .ToList();

            double weaponScale = 30 / rows.Max(r => r.Weapon.Top);
            double armorScale = 30 / rows.Max(r => r.Armor.Top);
            Console.WriteLine($"{"level",5}  {Pad("무기 피해 중앙값 / 상위10%", 46)}  갑옷 방어도 중앙값 / 상위10%");
            foreach (var row in rows)
            {
                Console.WriteLine(
                    $"{row.Level,5}  {row.Weapon.Median,6:0.0} / {row.Weapon.Top,6:0.0} {Pad(new string('#', (int)(row.Weapon.Median * weaponScale)), 30)}" +
                    $"  {row.Armor.Median,6:0.0} / {row.Armor.Top,6:0.0} {new string('#', (int)(row.Armor.Median * armorScale))}");
            }
        }

        static (double Median, double Top) Spread(LootGenerator generator, GameRandom random, int level, ItemSlot slot, StatType stat, int count)
        {
            var bases = generator.BasesAt(level).Where(b => b.Slot == slot).ToList();
            var values = Enumerable.Range(0, count)
                .Select(_ =>
                {
                    var item = generator.RollItem(random.PickWeighted(bases, b => b.Weight), level, Rarity.Rare, random);
                    var sheet = new StatSheet();
                    sheet.Add(item.Modifiers());
                    return sheet.Get(stat, context: item.Base.Tags);
                })
                .OrderBy(v => v)
                .ToList();
            return (values[values.Count / 2], values[values.Count * 9 / 10]);
        }

        // Korean characters take two columns in a terminal.
        static string Pad(string text, int width)
        {
            int used = text.Sum(c => c >= 0x1100 ? 2 : 1);
            return text + new string(' ', Math.Max(0, width - used));
        }

        static LootData LoadLoot()
        {
            string folder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Loot"));
            string Read(string file) => File.ReadAllText(Path.Combine(folder, file));
            var data = LootSheets.Parse(
                Read(LootSheets.BasesFile),
                Read(LootSheets.AffixesFile),
                Read(LootSheets.RunesFile),
                Read(LootSheets.RunewordsFile));
            if (data.Success) return data;

            foreach (string error in data.Errors) Console.Error.WriteLine(error);
            Environment.Exit(1);
            return null;
        }

        static MonsterData LoadMonsters()
        {
            string folder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Monsters"));
            var data = MonsterSheet.Parse(File.ReadAllText(Path.Combine(folder, MonsterSheet.File)));
            if (data.Success) return data;

            foreach (string error in data.Errors) Console.Error.WriteLine(error);
            Environment.Exit(1);
            return null;
        }

        static BossData LoadBosses(MonsterData monsters)
        {
            string folder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Bosses"));
            var data = BossSheet.Parse(
                File.ReadAllText(Path.Combine(folder, BossSheet.BossesFile)),
                File.ReadAllText(Path.Combine(folder, BossSheet.AttacksFile)),
                monsters);
            if (data.Success) return data;

            foreach (string error in data.Errors) Console.Error.WriteLine(error);
            Environment.Exit(1);
            return null;
        }

        // The classes named on the command line, checked against the sheets.
        static ClassDefinition[] LoadClasses(string[] ids)
        {
            string folder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Classes"));
            var data = ClassSheet.Parse(
                File.ReadAllText(Path.Combine(folder, ClassSheet.ClassesFile)),
                File.ReadAllText(Path.Combine(folder, ClassSheet.SkillsFile)));
            foreach (string error in data.Errors) Console.Error.WriteLine(error);
            if (!data.Success) Environment.Exit(1);
            var unknown = ids.Where(id => data.Classes.All(c => c.Id != id)).ToList();
            if (unknown.Count > 0)
            {
                Console.Error.WriteLine($"없는 클래스: {string.Join(", ", unknown)} (있는 것: {string.Join(", ", data.Classes.Select(c => c.Id))})");
                Environment.Exit(1);
            }
            return ClassRules.Choose(data, ids, ids.Length).ToArray();
        }

        static string FindFolder(string relativePath)
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    string candidate = Path.Combine(dir.FullName, relativePath);
                    if (Directory.Exists(candidate)) return candidate;
                }
            }
            throw new DirectoryNotFoundException(relativePath);
        }
    }
}
