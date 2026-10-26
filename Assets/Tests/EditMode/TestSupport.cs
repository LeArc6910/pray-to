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
                Read(LootSheets.RunewordsFile));
        }
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

        public static LootData Parse(string bases = Bases, string affixes = Affixes, string runes = Runes, string runewords = Runewords) =>
            LootSheets.Parse(bases, affixes, runes, runewords);

        public static void AssertError(LootData data, params string[] parts)
        {
            Assert.IsTrue(
                data.Errors.Any(error => parts.All(error.Contains)),
                $"No error mentions {string.Join(" + ", parts)}. Errors:\n{string.Join("\n", data.Errors)}");
        }
    }
}
