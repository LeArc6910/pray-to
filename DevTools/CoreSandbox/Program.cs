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
    // add --monsters <id,id> to --fight for those kinds alone, four at a time round the bot (a biome's own monsters)
    // add --rooms [biome] to --fight to fight in a world of rooms, from pack room to pack room (in that biome, or any),
    // and --no-packs anywhere for the monsters without packs.csv (one by one, as before packs)
    // dotnet run -- --boss [seconds] [seed]         a bot party against the first boss (--boss-id for another)
    // add --players <n> and --reaction <seconds> to --boss, as well as --gear and --no-dodge
    // add --no-roll to --fight or --boss for bots that do not use the dodge roll (as before it was made), or
    // --roll-miss <0..1> for bots that fumble that share of their rolls
    // add --classes <class,class> to --fight or --boss to give the bots classes; they use every skill when it is ready
    // dotnet run -- --farm [minutes] [crop]          a plot of soil kept by a sprinkler and a harvester: harvest an hour
    // dotnet run -- --rift [tiers] [seed]             bots open void rifts tier after tier (--players, --gear, --classes)
    // dotnet run -- --defense [levels] [seed]        bots hold a ward stone level after level (--turrets <n>, --players, --gear, --classes)
    // add --rank <1-5> for the bots' skill ranks, --level <n> for their level (and only the skills open by then)
    // and --build (with --level) to spend the level's skill and passive points and set crystals as a player would
    // dotnet run -- --skills [seconds]               every class skill alone against a pack of dummies, rank 1 and 5 (--gear)
    // dotnet run -- --supports [seconds]             every skill with each support crystal that fits it, against it bare (--gear, --rank)
    // dotnet run -- --economy                         coins an hour from each way of earning them, against what the shop asks
    // dotnet run -- --factory [minutes]               iron bars an hour from small factories, against digging by hand
    // dotnet run -- --hour [minutes] [seed,seed,...] [map size, 512]  a new character's first hour by the guide (FirstHourBot), a table of numbers per seed
    // dotnet run -- --supply                          which system supplies which, from the sheets (Docs/Systems.md)
    // dotnet run -- --flow [seeds]                    steps from the spawn to the first veins, arena and places (Docs/WorldFlow.md)
    // dotnet run -- --pace [minutes] [seeds]          the pacing director's rhythm: a bot roaming a world of rooms, its tension
    //                                                 as a curve, the states, what was sent (--gear, --classes, --level; Docs/WorldRework.md)
    // add --no-director anywhere for the director not acting (as before it was made)
    // dotnet run -- --mapstats [seeds] [map size]     the map as numbers: rock, lumps, open floor, each biome, rooms, time (Docs/WorldRework.md)
    // add --gen <1|2|3|4|5|6> to --flow, --hour or --mapstats for the generator's version (--mapstats shows both without it;
    // the others play what a new world is made with, WorldGenerator.NewWorldVersion)
    static class Program
    {
        // --no-packs: the monsters without packs.csv, one by one as before packs (Docs/WorldRework.md 묶음 2-1).
        static bool NoPacks;
        // --no-director: the pacing director does not act (World.Director.cs, 묶음 4b), as before it was made.
        public static bool Director = true;

        static void Main(string[] args)
        {
            NoPacks = args.Contains("--no-packs");
            args = args.Where(a => a != "--no-packs").ToArray();
            Director = !args.Contains("--no-director");
            args = args.Where(a => a != "--no-director").ToArray();
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
            string bossId = null;
            int bossIdFlag = Array.IndexOf(args, "--boss-id");
            if (bossIdFlag >= 0)
            {
                bossId = args[bossIdFlag + 1];
                args = args.Where((_, i) => i != bossIdFlag && i != bossIdFlag + 1).ToArray();
            }
            string[] classIds = Array.Empty<string>();
            int classesFlag = Array.IndexOf(args, "--classes");
            if (classesFlag >= 0)
            {
                classIds = args[classesFlag + 1].Split(',');
                args = args.Where((_, i) => i != classesFlag && i != classesFlag + 1).ToArray();
            }
            int rankFlag = Array.IndexOf(args, "--rank");
            if (rankFlag >= 0)
            {
                SkillBot.Rank = int.Parse(args[rankFlag + 1]);
                args = args.Where((_, i) => i != rankFlag && i != rankFlag + 1).ToArray();
            }
            int levelFlag = Array.IndexOf(args, "--level");
            if (levelFlag >= 0)
            {
                SkillBot.Level = int.Parse(args[levelFlag + 1]);
                args = args.Where((_, i) => i != levelFlag && i != levelFlag + 1).ToArray();
            }
            if (args.Contains("--build"))
            {
                SkillBot.Build = LoadClassData();
                args = args.Where(a => a != "--build").ToArray();
            }
            int generator = 0;
            int genFlag = Array.IndexOf(args, "--gen");
            if (genFlag >= 0)
            {
                generator = int.Parse(args[genFlag + 1]);
                args = args.Where((_, i) => i != genFlag && i != genFlag + 1).ToArray();
            }
            string[] onlyMonsters = null;
            int monstersFlag = Array.IndexOf(args, "--monsters");
            if (monstersFlag >= 0)
            {
                onlyMonsters = args[monstersFlag + 1].Split(',');
                args = args.Where((_, i) => i != monstersFlag && i != monstersFlag + 1).ToArray();
            }
            // --rooms [biome]: --fight in a world of rooms, from pack room to pack room (RoomFightSim).
            string roomsBiome = null;
            int roomsFlag = Array.IndexOf(args, "--rooms");
            if (roomsFlag >= 0)
            {
                bool named = roomsFlag + 1 < args.Length && !args[roomsFlag + 1].StartsWith("--") && !char.IsDigit(args[roomsFlag + 1][0]);
                roomsBiome = named ? args[roomsFlag + 1] : "any";
                args = args.Where((_, i) => i != roomsFlag && (!named || i != roomsFlag + 1)).ToArray();
            }
            // --camps: with --rooms, from elite camp to elite camp (묶음 2-2).
            bool camps = args.Contains("--camps");
            args = args.Where(a => a != "--camps").ToArray();
            bool dodge = !args.Contains("--no-dodge");
            args = args.Where(a => a != "--no-dodge").ToArray();
            // The bots roll through blows about to land (RollBot); --no-roll is how they fought before the roll.
            RollBot.Enabled = !args.Contains("--no-roll");
            args = args.Where(a => a != "--no-roll").ToArray();
            int missFlag = Array.IndexOf(args, "--roll-miss");
            if (missFlag >= 0)
            {
                RollBot.Miss = float.Parse(args[missFlag + 1], System.Globalization.CultureInfo.InvariantCulture);
                args = args.Where((_, i) => i != missFlag && i != missFlag + 1).ToArray();
            }

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
                case "--fight" when roomsBiome != null:
                    RoomFightSim.Run(LoadSheets(data), Number(1, 180), Seed(2), gear, LoadClasses(classIds), dodge, roomsBiome, camps);
                    break;
                case "--fight":
                    FightSim.Run(data, LoadMonsters(), Number(1, 120), Seed(2), gear, LoadClasses(classIds), dodge, onlyMonsters);
                    break;
                case "--farm":
                    FarmSim.Run(data, LoadTiles(data), LoadMonsters(), Number(1, 120), args.Length > 2 ? args[2] : "mushroom");
                    break;
                case "--rift":
                    var riftMonsters = LoadMonsters();
                    RiftSim.Run(data, riftMonsters, LoadTiles(data), Number(1, 5), Seed(2), players, gear, LoadClasses(classIds), dodge);
                    break;
                case "--defense":
                    int turrets = 0;
                    int turretsFlag = Array.IndexOf(args, "--turrets");
                    if (turretsFlag >= 0)
                    {
                        turrets = int.Parse(args[turretsFlag + 1]);
                        args = args.Where((_, i) => i != turretsFlag && i != turretsFlag + 1).ToArray();
                    }
                    DefenseSim.Run(data, LoadMonsters(), LoadTiles(data), Number(1, 5), Seed(2), players, gear, LoadClasses(classIds), dodge, turrets);
                    break;
                case "--skills":
                    SkillSim.Run(data, LoadClassData(), Number(1, 60), gear);
                    break;
                case "--supports":
                    SkillSim.RunSupports(data, LoadClassData(), Number(1, 60), gear, rankFlag >= 0 ? SkillBot.Rank : SkillRules.MaxRank);
                    break;
                case "--economy":
                    EconomySim.Run(data, LoadTiles(data), LoadNpcs(data));
                    break;
                case "--flow":
                    string flowFolder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Story"));
                    string FlowFile(string file) => File.ReadAllText(Path.Combine(flowFolder, file));
                    var flowMonsters = LoadMonsters();
                    var flowStory = StorySheet.Parse(FlowFile(StorySheet.PlacesFile), FlowFile(StorySheet.CharactersFile), FlowFile(StorySheet.StoryFile), data, LoadBosses(flowMonsters));
                    var seedList = args.Length > 1 && !args[1].StartsWith("--") ? args[1].Split(',').Select(ulong.Parse) : Enumerable.Range(1, 12).Select(i => (ulong)i);
                    FlowSim.Run(flowStory, seedList, generator > 0 ? generator : WorldGenerator.NewWorldVersion, LoadTiles(data).Terrain);
                    break;
                case "--mapstats":
                    var statSheets = LoadSheets(data);
                    var statSeeds = args.Length > 1 ? args[1].Split(',').Select(ulong.Parse) : new ulong[] { 1, 2, 3 };
                    // With the arenas' sheet, as the game makes a world (the other sims fight without entrances).
                    var statArenas = ArenaSheet.Parse(File.ReadAllText(Path.Combine(FindFolder(Path.Combine("Assets", "_Project", "Data", "Bosses")), ArenaSheet.File)), statSheets.Bosses);
                    MapStats.Run(statSheets.Story, statArenas, statSheets.Tiles.Terrain, statSeeds,
                        generator > 0 ? new[] { generator } : new[] { WorldGenerator.CaveVersion, WorldGenerator.RoomVersion },
                        args.Length > 2 ? int.Parse(args[2]) : WorldGenerator.GameSize);
                    break;
                case "--supply":
                    var supplyMonsters = LoadMonsters();
                    var supplyBosses = LoadBosses(supplyMonsters);
                    string storyFolder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Story"));
                    string StoryFile(string file) => File.ReadAllText(Path.Combine(storyFolder, file));
                    var story = StorySheet.Parse(StoryFile(StorySheet.PlacesFile), StoryFile(StorySheet.CharactersFile), StoryFile(StorySheet.StoryFile), data, supplyBosses);
                    SupplySim.Run(data, LoadTiles(data), LoadNpcs(data), supplyMonsters, supplyBosses, story);
                    break;
                case "--factory":
                    FactorySim.Run(data, LoadMonsters(), LoadTiles(data), LoadNpcs(data), Number(1, 30));
                    break;
                case "--hour":
                    HourSim.Run(LoadSheets(data), Number(1, 60), args.Length > 2 ? args[2] : "1,2,3", args.Length > 3 ? int.Parse(args[3]) : WorldGenerator.GameSize,
                        generator > 0 ? generator : WorldGenerator.NewWorldVersion);
                    break;
                // --dungeon <id> [seeds] (묶음 5b): bots run a dungeon of dungeons.csv to its end chest (DungeonSim).
                case "--dungeon":
                    var dungeonSeeds = args.Length > 2 && !args[2].StartsWith("--") ? args[2].Split(',').Select(ulong.Parse) : new ulong[] { 1, 2, 3 };
                    DungeonSim.Run(LoadSheets(data), args.Length > 1 ? args[1] : "abandoned_mine", dungeonSeeds, players, gear, LoadClasses(classIds), dodge);
                    break;
                // --traps [seeds] (묶음 B1c): the grates each trap room of the game's world falls with (TrapCuts).
                case "--traps":
                    var trapSeeds = args.Length > 1 && !args[1].StartsWith("--") ? args[1].Split(',').Select(ulong.Parse) : new ulong[] { 1, 2, 3 };
                    TrapCuts.Run(LoadSheets(data), trapSeeds);
                    break;
                case "--pace":
                    var paceSeeds = args.Length > 2 && !args[2].StartsWith("--") ? args[2].Split(',').Select(ulong.Parse) : new ulong[] { 1, 2, 3 };
                    PaceSim.Run(LoadSheets(data), Number(1, 30), paceSeeds, gearFlag >= 0 ? gear : new[] { "iron_sword", "iron_armor", "iron_helmet", "iron_boots" },
                        LoadClasses(classIds.Length > 0 ? classIds : new[] { "warrior" }), Director, SkillBot.Level ?? 8);
                    break;
                case "--boss":
                    var monsters = LoadMonsters();
                    BossSim.Run(data, monsters, LoadBosses(monsters), Number(1, 300), Seed(2), players, gear, LoadClasses(classIds), reaction, dodge, bossId);
                    break;
                default:
                    Console.WriteLine("usage: dotnet run -- --item [level] [seed] | --loot [level] [count] [seed] | --progression [count] [seed]");
                    Console.WriteLine("       add --rarity <percent> to --item or --loot");
                    Console.WriteLine("       dotnet run -- --fight [seconds] [seed] [--gear copper_sword,copper_armor] [--classes warrior,fire_mage] [--no-dodge]");
                    Console.WriteLine("       dotnet run -- --boss [seconds] [seed] [--players 2] [--reaction 0.25] [--gear ...] [--classes ...] [--no-dodge] [--no-roll] [--roll-miss 0.3] [--boss-id crystal_queen]");
                    Console.WriteLine("       dotnet run -- --farm [minutes] [crop]");
                    Console.WriteLine("       dotnet run -- --rift [tiers] [seed] [--players 2] [--gear ...] [--classes ...]");
                    Console.WriteLine("       dotnet run -- --economy");
                    Console.WriteLine("       dotnet run -- --factory [minutes]");
                    Console.WriteLine("       dotnet run -- --hour [minutes] [seed,seed,...] [map size] [--gen 1|2|3|4|5|6]");
                    Console.WriteLine("       dotnet run -- --flow [seeds] [--gen 1|2|3|4|5|6]");
                    Console.WriteLine("       dotnet run -- --mapstats [seeds] [map size] [--gen 1|2|3|4|5|6]");
                    Console.WriteLine("       dotnet run -- --pace [minutes] [seeds] [--gear ...] [--classes ...] [--level 8] [--no-director]");
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
                Read(LootSheets.RunewordsFile),
                Read(LootSheets.ConsumablesFile),
                Read(LootSheets.MaterialsFile),
                Read(LootSheets.RecipesFile),
                Read(LootSheets.SetsFile));
            if (data.Success) return data;

            foreach (string error in data.Errors) Console.Error.WriteLine(error);
            Environment.Exit(1);
            return null;
        }

        // Every sheet a world of the game is made from, as GameRoot loads them.
        static BotSheets LoadSheets(LootData loot)
        {
            var monsters = LoadMonsters();
            var bosses = LoadBosses(monsters);
            string guideFolder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Guide"));
            string storyFolder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Story"));
            string Story(string file) => File.ReadAllText(Path.Combine(storyFolder, file));
            var guide = GuideSheet.Parse(File.ReadAllText(Path.Combine(guideFolder, GuideSheet.File)), loot);
            var story = StorySheet.Parse(Story(StorySheet.PlacesFile), Story(StorySheet.CharactersFile), Story(StorySheet.StoryFile), loot, bosses);
            foreach (string error in guide.Errors.Concat(story.Errors)) Console.Error.WriteLine(error);
            if (!guide.Success || !story.Success) Environment.Exit(1);
            return new BotSheets
            {
                Monsters = monsters, Loot = loot, Bosses = bosses, Classes = LoadClassData(), Tiles = LoadTiles(loot), Guide = guide,
                Npcs = LoadNpcs(loot), Story = story,
            };
        }

        static TileData LoadTiles(LootData loot)
        {
            string folder = FindFolder(Path.Combine("Assets", "_Project", "Data", "World"));
            string Read(string file) => File.ReadAllText(Path.Combine(folder, file));
            var data = TileSheet.Parse(Read(TileSheet.File), loot, Read(TileSheet.CropsFile), Read(TileSheet.MachinesFile), Read(TerrainSheet.File), Read(RoomSheet.File), Read(RoomSheet.RolesFile), Read(PoiSheet.File),
                Read(DungeonSheet.File), Read(DungeonSheet.GraphsFile));
            if (data.Success) return data;

            foreach (string error in data.Errors) Console.Error.WriteLine(error);
            Environment.Exit(1);
            return null;
        }

        static NpcData LoadNpcs(LootData loot)
        {
            string folder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Npcs"));
            var data = NpcSheet.Parse(File.ReadAllText(Path.Combine(folder, NpcSheet.NpcsFile)), File.ReadAllText(Path.Combine(folder, NpcSheet.ShopFile)), loot,
                File.ReadAllText(Path.Combine(folder, NpcSheet.RequestsFile)));
            if (data.Success) return data;

            foreach (string error in data.Errors) Console.Error.WriteLine(error);
            Environment.Exit(1);
            return null;
        }

        static MonsterData LoadMonsters()
        {
            string folder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Monsters"));
            var data = MonsterSheet.Parse(File.ReadAllText(Path.Combine(folder, MonsterSheet.File)), File.ReadAllText(Path.Combine(folder, MonsterAttackSheet.File)));
            // The packs of the rooms (packs.csv, Docs/WorldRework.md 무리); --no-packs plays without them, as before.
            var packs = NoPacks ? null : PackSheet.Parse(File.ReadAllText(Path.Combine(folder, PackSheet.File)), data);
            foreach (string error in packs?.Errors ?? new List<string>()) Console.Error.WriteLine(error);
            if (packs != null && !packs.Success) Environment.Exit(1);
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

        static ClassData LoadClassData()
        {
            string folder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Classes"));
            var data = ClassSheet.Parse(
                File.ReadAllText(Path.Combine(folder, ClassSheet.ClassesFile)),
                File.ReadAllText(Path.Combine(folder, ClassSheet.SkillsFile)),
                File.ReadAllText(Path.Combine(folder, ClassSheet.PassivesFile)));
            foreach (string error in data.Errors) Console.Error.WriteLine(error);
            if (!data.Success) Environment.Exit(1);
            return data;
        }

        // The classes named on the command line, checked against the sheets.
        static ClassDefinition[] LoadClasses(string[] ids)
        {
            string folder = FindFolder(Path.Combine("Assets", "_Project", "Data", "Classes"));
            var data = ClassSheet.Parse(
                File.ReadAllText(Path.Combine(folder, ClassSheet.ClassesFile)),
                File.ReadAllText(Path.Combine(folder, ClassSheet.SkillsFile)),
                File.ReadAllText(Path.Combine(folder, ClassSheet.PassivesFile)));
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
