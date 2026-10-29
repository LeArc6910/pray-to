using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public sealed class LootData
    {
        internal LootData(
            IReadOnlyList<ItemBase> bases,
            IReadOnlyList<Affix> affixes,
            IReadOnlyList<RuneDefinition> runes,
            IReadOnlyList<Runeword> runewords,
            IReadOnlyList<ConsumableDefinition> consumables,
            IReadOnlyList<MaterialDefinition> materials,
            IReadOnlyList<Recipe> recipes,
            IReadOnlyList<string> errors)
        {
            Bases = bases;
            Affixes = affixes;
            Runes = runes;
            Runewords = runewords;
            Consumables = consumables;
            Materials = materials;
            Recipes = recipes;
            Errors = errors;
        }

        public IReadOnlyList<ItemBase> Bases { get; }
        public IReadOnlyList<Affix> Affixes { get; }
        public IReadOnlyList<RuneDefinition> Runes { get; }
        public IReadOnlyList<Runeword> Runewords { get; }
        public IReadOnlyList<ConsumableDefinition> Consumables { get; }
        public IReadOnlyList<MaterialDefinition> Materials { get; }
        // In the order the crafting list shows them.
        public IReadOnlyList<Recipe> Recipes { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool Success => Errors.Count == 0;

        public ItemBase FindBase(string id) =>
            Bases.FirstOrDefault(b => b.Id == id) ?? throw new ArgumentException($"Unknown item base '{id}'.", nameof(id));

        public Affix FindAffix(string id) =>
            Affixes.FirstOrDefault(a => a.Id == id) ?? throw new ArgumentException($"Unknown affix '{id}'.", nameof(id));

        public RuneDefinition FindRune(string id) =>
            Runes.FirstOrDefault(r => r.Id == id) ?? throw new ArgumentException($"Unknown rune '{id}'.", nameof(id));

        public ConsumableDefinition FindConsumable(string id) =>
            Consumables.FirstOrDefault(c => c.Id == id) ?? throw new ArgumentException($"Unknown consumable '{id}'.", nameof(id));

        public MaterialDefinition FindMaterial(string id) =>
            Materials.FirstOrDefault(m => m.Id == id) ?? throw new ArgumentException($"Unknown material '{id}'.", nameof(id));
    }

    // Reads the loot sheets in Assets/_Project/Data/Loot (columns and syntax in Docs/Loot.md). A bad row is skipped
    // and reported in Errors with its sheet and line, so one typo does not hide the problems in other rows.
    public static class LootSheets
    {
        public const string BasesFile = "item_bases.csv";
        public const string AffixesFile = "affixes.csv";
        public const string RunesFile = "runes.csv";
        public const string RunewordsFile = "runewords.csv";
        public const string ConsumablesFile = "consumables.csv";
        public const string MaterialsFile = "materials.csv";
        public const string RecipesFile = "recipes.csv";

        const int MaxSockets = 6;

        // Without the consumables, materials or recipes sheet there are none of those.
        public static LootData Parse(
            string basesCsv,
            string affixesCsv,
            string runesCsv,
            string runewordsCsv,
            string consumablesCsv = "",
            string materialsCsv = "",
            string recipesCsv = "")
        {
            var errors = new List<string>();
            var bases = ParseBases(basesCsv, errors);
            var affixes = ParseAffixes(affixesCsv, errors);
            var runes = ParseRunes(runesCsv, errors);
            var runewords = ParseRunewords(runewordsCsv, runes, bases, errors);
            var consumables = ParseConsumables(consumablesCsv, errors);
            var materials = ParseMaterials(materialsCsv, errors);
            var recipes = ParseRecipes(recipesCsv, bases, consumables, materials, errors);
            return new LootData(bases, affixes, runes, runewords, consumables, materials, recipes, errors);
        }

        static List<ItemBase> ParseBases(string csv, List<string> errors)
        {
            var bases = new List<ItemBase>();
            var table = Read(csv, BasesFile, errors, "id", "name", "slot", "level", "tags", "implicit", "sockets", "weight");
            if (table == null) return bases;

            foreach (var row in table.Rows)
            {
                TryRow(BasesFile, row, errors, () =>
                {
                    int sockets = SheetValues.ParseNumber(row["sockets"], "sockets");
                    if (sockets > MaxSockets) throw new FormatException($"sockets는 {MaxSockets} 이하여야 합니다.");
                    bases.Add(new ItemBase(
                        NewId(row, bases.Select(b => b.Id)),
                        Required(row, "name"),
                        SheetValues.ParseEnum<ItemSlot>(row["slot"], "슬롯"),
                        Level(row),
                        ParseTags(row["tags"]),
                        ModifierSyntax.ParseList(row["implicit"]),
                        sockets,
                        SheetValues.ParseNumber(row["weight"], "weight")));
                });
            }
            return bases;
        }

        // One row per tier. Later rows of the same id may leave kind, name and slots blank.
        static List<Affix> ParseAffixes(string csv, List<string> errors)
        {
            var affixes = new List<Affix>();
            var table = Read(csv, AffixesFile, errors, "id", "kind", "name", "slots", "level", "weight", "modifier");
            if (table == null) return affixes;

            foreach (var row in table.Rows)
            {
                TryRow(AffixesFile, row, errors, () =>
                {
                    string id = row["id"];
                    var affix = affixes.FirstOrDefault(a => a.Id == id);
                    if (affix == null)
                    {
                        affix = new Affix(
                            NewId(row, Enumerable.Empty<string>()),
                            SheetValues.ParseEnum<AffixKind>(row["kind"], "kind"),
                            Required(row, "name"),
                            ParseSlots(row["slots"]));
                    }
                    else
                    {
                        if (row["kind"] != "" && SheetValues.ParseEnum<AffixKind>(row["kind"], "kind") != affix.Kind)
                            throw new FormatException($"'{id}'의 kind가 앞 줄과 다릅니다.");
                        if (row["name"] != "" && row["name"] != affix.Name)
                            throw new FormatException($"'{id}'의 name이 앞 줄과 다릅니다.");
                        if (row["slots"] != "" && !ParseSlots(row["slots"]).SequenceEqual(affix.Slots))
                            throw new FormatException($"'{id}'의 slots가 앞 줄과 다릅니다.");
                    }

                    int level = Level(row);
                    var modifier = ModifierSyntax.Parse(Required(row, "modifier"));
                    if (affix.Tiers.Count > 0)
                    {
                        var previous = affix.Tiers[affix.Tiers.Count - 1];
                        if (level <= previous.Level)
                            throw new FormatException($"'{id}'의 티어는 level이 올라가는 순서로 적어야 합니다.");
                        var first = affix.Tiers[0].Modifier;
                        if (modifier.Stat != first.Stat || modifier.Kind != first.Kind || modifier.Tags != first.Tags)
                            throw new FormatException($"'{id}'의 티어끼리 능력치가 다릅니다: {first} / {modifier}");
                    }

                    affix.AddTier(level, SheetValues.ParseNumber(row["weight"], "weight"), modifier);
                    if (!affixes.Contains(affix)) affixes.Add(affix);
                });
            }
            return affixes;
        }

        static List<RuneDefinition> ParseRunes(string csv, List<string> errors)
        {
            var runes = new List<RuneDefinition>();
            var table = Read(csv, RunesFile, errors, "id", "name", "level", "weight", "weapon", "armor", "jewelry");
            if (table == null) return runes;

            foreach (var row in table.Rows)
            {
                TryRow(RunesFile, row, errors, () =>
                {
                    runes.Add(new RuneDefinition(
                        NewId(row, runes.Select(r => r.Id)),
                        Required(row, "name"),
                        Level(row),
                        SheetValues.ParseNumber(row["weight"], "weight"),
                        FixedModifiers(row["weapon"]),
                        FixedModifiers(row["armor"]),
                        FixedModifiers(row["jewelry"])));
                });
            }
            return runes;
        }

        static List<ConsumableDefinition> ParseConsumables(string csv, List<string> errors)
        {
            var consumables = new List<ConsumableDefinition>();
            if (string.IsNullOrWhiteSpace(csv)) return consumables;
            var table = Read(csv, ConsumablesFile, errors, "id", "name", "level", "heal", "seconds", "cooldown", "weight", "description");
            if (table == null) return consumables;

            foreach (var row in table.Rows)
            {
                TryRow(ConsumablesFile, row, errors, () =>
                {
                    int heal = SheetValues.ParseNumber(row["heal"], "heal");
                    if (heal < 1 || heal > 100) throw new FormatException("heal은 1~100(최대 생명력의 %)이어야 합니다.");
                    float cooldown = SheetValues.ParseDecimal(row["cooldown"], "cooldown");
                    if (cooldown <= 0) throw new FormatException("cooldown은 0보다 커야 합니다.");
                    consumables.Add(new ConsumableDefinition(
                        NewId(row, consumables.Select(c => c.Id)),
                        Required(row, "name"),
                        Level(row),
                        heal,
                        SheetValues.ParseDecimal(row["seconds"], "seconds"),
                        cooldown,
                        SheetValues.ParseNumber(row["weight"], "weight"),
                        row["description"]));
                });
            }
            return consumables;
        }

        static List<MaterialDefinition> ParseMaterials(string csv, List<string> errors)
        {
            var materials = new List<MaterialDefinition>();
            if (string.IsNullOrWhiteSpace(csv)) return materials;
            var table = Read(csv, MaterialsFile, errors, "id", "name", "places", "description");
            if (table == null) return materials;

            foreach (var row in table.Rows)
            {
                TryRow(MaterialsFile, row, errors, () =>
                {
                    TileType? places = null;
                    if (row["places"].Length > 0)
                    {
                        places = SheetValues.ParseEnum<TileType>(row["places"], "칸 종류");
                        if (places == TileType.Floor) throw new FormatException("places에는 벽 같은 단단한 칸만 적을 수 있습니다.");
                    }
                    materials.Add(new MaterialDefinition(NewId(row, materials.Select(m => m.Id)), Required(row, "name"), places, row["description"]));
                });
            }
            return materials;
        }

        static List<Recipe> ParseRecipes(
            string csv, List<ItemBase> bases, List<ConsumableDefinition> consumables, List<MaterialDefinition> materials, List<string> errors)
        {
            var recipes = new List<Recipe>();
            if (string.IsNullOrWhiteSpace(csv)) return recipes;
            var table = Read(csv, RecipesFile, errors, "id", "result", "count", "ingredients");
            if (table == null) return recipes;

            foreach (var row in table.Rows)
            {
                TryRow(RecipesFile, row, errors, () =>
                {
                    string id = NewId(row, recipes.Select(r => r.Id));
                    string result = row["result"];
                    var item = bases.FirstOrDefault(b => b.Id == result);
                    var consumable = consumables.FirstOrDefault(c => c.Id == result);
                    var material = materials.FirstOrDefault(m => m.Id == result);
                    if (item == null && consumable == null && material == null)
                        throw new FormatException($"result '{result}'가 아이템 베이스, 소모품, 재료 어디에도 없습니다.");
                    int count = SheetValues.ParseNumber(row["count"], "count");
                    if (count < 1) throw new FormatException("count는 1 이상이어야 합니다.");
                    if (item != null && count != 1) throw new FormatException("장비는 하나씩 만듭니다. count를 1로 적습니다.");

                    var ingredients = new List<(MaterialDefinition, int)>();
                    foreach (string part in SplitList(row["ingredients"]))
                    {
                        string[] pieces = part.Split(':');
                        if (pieces.Length != 2) throw new FormatException($"'{part}': 재료는 id:개수로 적습니다. 예: stone:3");
                        var ingredient = materials.FirstOrDefault(m => m.Id == pieces[0].Trim())
                            ?? throw new FormatException($"재료 '{pieces[0].Trim()}'가 {MaterialsFile}에 없습니다.");
                        int amount = SheetValues.ParseNumber(pieces[1].Trim(), $"'{part}'의 개수");
                        if (amount < 1) throw new FormatException($"'{part}': 개수는 1 이상이어야 합니다.");
                        ingredients.Add((ingredient, amount));
                    }
                    if (ingredients.Count == 0) throw new FormatException("ingredients가 비어 있습니다.");
                    TileType? station = null;
                    if (row["station"].Length > 0)
                    {
                        station = SheetValues.ParseEnum<TileType>(row["station"], "제작대");
                        if (station == TileType.Floor) throw new FormatException("station에는 작업대 같은 칸 종류를 적습니다.");
                    }
                    recipes.Add(new Recipe(id, item, consumable, material, count, ingredients, station));
                });
            }
            return recipes;
        }

        static List<Runeword> ParseRunewords(string csv, List<RuneDefinition> runes, List<ItemBase> bases, List<string> errors)
        {
            var runewords = new List<Runeword>();
            var table = Read(csv, RunewordsFile, errors, "id", "name", "slots", "runes", "modifiers");
            if (table == null) return runewords;

            foreach (var row in table.Rows)
            {
                TryRow(RunewordsFile, row, errors, () =>
                {
                    string id = NewId(row, runewords.Select(w => w.Id));
                    var slots = ParseSlots(row["slots"]);
                    var sequence = SplitList(row["runes"])
                        .Select(runeId => runes.FirstOrDefault(r => r.Id == runeId)
                            ?? throw new FormatException($"룬 '{runeId}'가 {RunesFile}에 없습니다."))
                        .ToList();
                    if (sequence.Count == 0) throw new FormatException("runes가 비어 있습니다.");
                    if (!bases.Any(b => slots.Contains(b.Slot) && b.MaxSockets >= sequence.Count))
                        throw new FormatException($"소켓이 {sequence.Count}개 이상인 {string.Join("|", slots)} 베이스가 없어서 만들 수 없습니다.");

                    runewords.Add(new Runeword(id, Required(row, "name"), slots, sequence, FixedModifiers(row["modifiers"])));
                });
            }
            return runewords;
        }

        static CsvTable Read(string csv, string sheet, List<string> errors, params string[] columns)
        {
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns(columns);
            if (missing.Count == 0) return table;
            errors.Add($"{sheet}: 열이 없습니다: {string.Join(", ", missing)}");
            return null;
        }

        static void TryRow(string sheet, CsvRow row, List<string> errors, Action parse)
        {
            try
            {
                parse();
            }
            catch (FormatException e)
            {
                errors.Add($"{sheet} {row.Line}행: {e.Message}");
            }
        }

        static string NewId(CsvRow row, IEnumerable<string> taken)
        {
            string id = row["id"];
            if (!SheetValues.IsValidId(id)) throw new FormatException($"id '{id}'에는 영문, 숫자, _, -만 쓸 수 있습니다.");
            if (taken.Contains(id)) throw new FormatException($"id '{id}'가 이미 있습니다.");
            return id;
        }

        static string Required(CsvRow row, string column)
        {
            string value = row[column];
            if (value.Length == 0) throw new FormatException($"'{column}' 칸이 비어 있습니다.");
            return value;
        }

        static int Level(CsvRow row)
        {
            int level = SheetValues.ParseNumber(row["level"], "level");
            if (level < 1) throw new FormatException("level은 1 이상이어야 합니다.");
            return level;
        }

        static List<string> SplitList(string cell) =>
            cell.Split('|').Select(part => part.Trim()).Where(part => part.Length > 0).ToList();

        static SkillTag ParseTags(string cell) =>
            SplitList(cell).Aggregate(SkillTag.None, (tags, part) => tags | SheetValues.ParseEnum<SkillTag>(part, "태그"));

        // Slot names joined with '|'. "armor" means helmet|chest|boots and "jewelry" means ring|amulet.
        static List<ItemSlot> ParseSlots(string cell)
        {
            var slots = new List<ItemSlot>();
            foreach (string part in SplitList(cell))
            {
                switch (part.ToLowerInvariant())
                {
                    case "armor":
                        slots.AddRange(new[] { ItemSlot.Helmet, ItemSlot.Chest, ItemSlot.Boots });
                        break;
                    case "jewelry":
                        slots.AddRange(new[] { ItemSlot.Ring, ItemSlot.Amulet });
                        break;
                    default:
                        slots.Add(SheetValues.ParseEnum<ItemSlot>(part, "슬롯"));
                        break;
                }
            }
            if (slots.Count == 0) throw new FormatException("slots가 비어 있습니다.");
            return slots.Distinct().ToList();
        }

        // Runes and runewords always give the same amount, so their modifiers are written without a range.
        static List<Modifier> FixedModifiers(string cell) =>
            ModifierSyntax.ParseList(cell)
                .Select(range => range.IsFixed
                    ? new Modifier(range.Stat, range.Kind, range.Min, range.Tags)
                    : throw new FormatException($"'{range}'처럼 범위로 적을 수 없습니다. 하나의 값으로 적습니다."))
                .ToList();
    }
}
