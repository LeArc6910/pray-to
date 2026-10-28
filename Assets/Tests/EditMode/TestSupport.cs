using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public static class RepoFile
    {
        // Unity runs tests from the project root; dotnet test runs them from the build output folder.
        public static string Find(string relativePath)
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    string candidate = Path.Combine(dir.FullName, relativePath);
                    if (File.Exists(candidate)) return candidate;
                }
            }
            throw new FileNotFoundException(relativePath);
        }
    }

    public static class Mods
    {
        // A single modifier written in the sheet syntax, e.g. "Fire:Damage+12%".
        public static Modifier Of(string text)
        {
            var range = ModifierSyntax.Parse(text);
            Assert.IsTrue(range.IsFixed, text);
            return new Modifier(range.Stat, range.Kind, range.Min, range.Tags);
        }
    }

    public static class ShippedLoot
    {
        public static LootData Load()
        {
            string Read(string file) => File.ReadAllText(RepoFile.Find("Assets/_Project/Data/Loot/" + file));
            return LootSheets.Parse(
                Read(LootSheets.BasesFile),
                Read(LootSheets.AffixesFile),
                Read(LootSheets.RunesFile),
                Read(LootSheets.RunewordsFile),
                Read(LootSheets.ConsumablesFile));
        }
    }

    public static class Gear
    {
        public static readonly LootData Data = ShippedLoot.Load();

        // An item with the lowest implicit rolls, the given affixes and empty sockets.
        public static Item Plain(string baseId, int sockets = 0, Rarity rarity = Rarity.Common, params ItemAffix[] affixes)
        {
            var itemBase = Data.FindBase(baseId);
            var implicits = itemBase.Implicits.Select(r => new Modifier(r.Stat, r.Kind, r.Min, r.Tags)).ToList();
            return new Item(itemBase, itemBase.Level, rarity, implicits, affixes, sockets);
        }

        // The affix's first tier with a chosen value.
        public static ItemAffix Affix(string id, int value)
        {
            var tier = Data.FindAffix(id).Tiers[0];
            return new ItemAffix(tier, new Modifier(tier.Modifier.Stat, tier.Modifier.Kind, value, tier.Modifier.Tags));
        }

        public static Equipment Wearing(params string[] baseIds)
        {
            var equipment = new Equipment();
            foreach (string id in baseIds) equipment.Equip(Plain(id));
            return equipment;
        }
    }

    public static class Maps
    {
        // '#' is wall and anything else floor. The first row is the top of the map, as it reads on screen.
        public static TileMap FromRows(params string[] rows)
        {
            var map = new TileMap(rows[0].Length, rows.Length, TileType.Floor);
            for (int row = 0; row < rows.Length; row++)
            {
                for (int x = 0; x < rows[row].Length; x++)
                {
                    if (rows[row][x] == '#') map[x, rows.Length - 1 - row] = TileType.Wall;
                }
            }
            return map;
        }

        // An open room with walls around it.
        public static TileMap Room(int size)
        {
            var rows = Enumerable.Range(0, size)
                .Select(row => row == 0 || row == size - 1 ? new string('#', size) : "#" + new string('.', size - 2) + "#")
                .ToArray();
            return FromRows(rows);
        }
    }

    public static class Monsters
    {
        public static readonly MonsterData Shipped = MonsterSheet.Parse(File.ReadAllText(RepoFile.Find("Assets/_Project/Data/Monsters/monsters.csv")));

        public const string Header = "id,name,level,health,damage,armor,speed,radius,aggro,reach,windup,cooldown,weight\n";

        // A slime that hits hard enough to drop a bare-handed player in one strike.
        public static readonly MonsterData Brute = MonsterSheet.Parse(Header + "brute,깡패,1,1000,500,0,2,0.4,8,0.5,0.5,1,1\n");

        // Never notices anyone and takes a beating, so targets stay where they were put.
        public static readonly MonsterDefinition Dummy = MonsterSheet.Parse(Header + "dummy,허수아비,1,1000,0,0,1,0.4,0,0.5,0.5,1,1\n").Find("dummy");
    }

    public static class Bosses
    {
        static string Read(string file) => File.ReadAllText(RepoFile.Find("Assets/_Project/Data/Bosses/" + file));

        public static readonly BossData Shipped = BossSheet.Parse(Read(BossSheet.BossesFile), Read(BossSheet.AttacksFile), Monsters.Shipped);

        public const string BossesHeader = "id,name,health,health_per_player,armor,speed,radius,phases,roar,roar_radius,roar_damage\n";
        public const string AttacksHeader =
            "boss,id,name,phases,shape,target,telegraph,damage,radius,length,angle,width,count,spread,speed,cooldown,weight,summon\n";

        // A boss that only ever uses the given attack rows, for testing one pattern at a time.
        public static BossDefinition With(string attackRows, string phases = "", int health = 1000) =>
            Parse(BossesHeader + $"dummy,허수아비,{health},50,0,2,1,{phases},1,3,10\n", AttacksHeader + attackRows).Find("dummy");

        public static BossData Parse(string bosses, string attacks) => BossSheet.Parse(bosses, attacks, Monsters.Shipped);
    }

    public static class Classes
    {
        static string Read(string file) => File.ReadAllText(RepoFile.Find("Assets/_Project/Data/Classes/" + file));

        public static readonly ClassData Shipped = ClassSheet.Parse(Read(ClassSheet.ClassesFile), Read(ClassSheet.SkillsFile));

        public const string ClassesHeader = "id,name,description,passive\n";
        public const string SkillsHeader = "id,name,class,kind,tags,damage,cooldown,radius,speed,range,duration,modifiers,grants,description\n";

        public static SkillDefinition Skill(string id) => Shipped.Skills.First(s => s.Id == id);
    }

    // Small hand-written sheets, so a test can see every row it depends on.
    public static class TestLoot
    {
        public const string BasesHeader = "id,name,slot,level,tags,implicit,sockets,weight\n";
        public const string AffixesHeader = "id,kind,name,slots,level,weight,modifier\n";
        public const string RunesHeader = "id,name,level,weight,weapon,armor,jewelry\n";
        public const string RunewordsHeader = "id,name,slots,runes,modifiers\n";

        public const string Bases = BasesHeader +
            "sword,검,weapon,1,melee,Damage+5,2,1\n" +
            "helm,투구,helmet,1,,Armor+3,2,1\n";

        public const string Affixes = AffixesHeader +
            "sharp,prefix,날카로운,weapon,1,1,Damage+1~2\n" +
            "sharp,,,,10,1,Damage+4~5\n" +
            "swift,suffix,신속의,weapon|ring,1,1,AttackSpeed+3~5%\n";

        public const string Runes = RunesHeader +
            "ember,불씨 룬,1,1,Fire:Damage+12%,Armor+4,Fire:Damage+6%\n" +
            "wind,바람 룬,1,1,AttackSpeed+6%,MoveSpeed+4%,AttackSpeed+3%\n";

        public const string Runewords = RunewordsHeader +
            "storm,폭풍,weapon,wind|ember,Lightning:Damage+40%\n";

        public static LootData Parse(string bases = Bases, string affixes = Affixes, string runes = Runes, string runewords = Runewords, string consumables = "") =>
            LootSheets.Parse(bases, affixes, runes, runewords, consumables);

        public static void AssertError(LootData data, params string[] parts)
        {
            Assert.IsTrue(
                data.Errors.Any(error => parts.All(error.Contains)),
                $"No error mentions {string.Join(" + ", parts)}. Errors:\n{string.Join("\n", data.Errors)}");
        }
    }
}
