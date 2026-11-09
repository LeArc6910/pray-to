using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Game.Core
{
    // A character as saved on the player's own device and carried into whichever world they join: worn gear and the
    // bag. Everything is written by sheet id rather than index, so a save still reads after the sheets change;
    // whatever the sheets no longer have is skipped and reported in Warnings. See Docs/Saving.md.
    public sealed class CharacterSave
    {
        internal const string Magic = "PTCH";
        // 2: experience (character levels). 3: the guide goals whose rewards it has had. 4: the points taken on the
        // passive tree, by id. 5: friendships (World.Merchant.cs), by person id. 6: life skill experience
        // (World.Life.cs). 7: each friendship's request day and heart gifts had. 8: the skills learned and their ranks
        // (Docs/Classes.md, 스킬 포인트와 단계), by id. 9: how far through each person's story (Docs/Npcs.md, 이야기).
        // 10: the support crystals in each skill (Docs/Classes.md, 스킬 변형), by ids, one pair a crystal (up to three a
        // skill since 2026-11-09, in slot order). 11: the records of the bestiary and the achievements had (Records.cs).
        const int FormatVersion = 11;

        CharacterSave(Equipment equipment, Inventory bag, int experience, List<string> warnings, List<string> claimed = null, List<string> passives = null,
            List<(string, int, int, int, int, int)> friendships = null, List<int> life = null, List<(string, int)> skills = null, List<(string, int)> stories = null, List<(string, string)> supports = null,
            List<(string, string, int)> records = null, List<string> achievements = null)
        {
            Records = records ?? new List<(string, string, int)>();
            Achievements = achievements ?? new List<string>();
            Supports = supports ?? new List<(string, string)>();
            Stories = stories ?? new List<(string, int)>();
            SkillRanks = skills ?? new List<(string, int)>();
            LifeExperience = life ?? new List<int>();
            Friendships = friendships ?? new List<(string, int, int, int, int, int)>();
            ClaimedGoals = claimed ?? new List<string>();
            Passives = passives ?? new List<string>();
            Equipment = equipment;
            Bag = bag;
            Experience = experience;
            Warnings = warnings;
        }

        public Equipment Equipment { get; }
        public Inventory Bag { get; }
        // -1 for a save from before levels (Leveling.StartFor decides then).
        public int Experience { get; }
        public IReadOnlyList<string> ClaimedGoals { get; }
        // Ids of the passive tree points it took (World.RestorePassives puts back what still fits).
        public IReadOnlyList<string> Passives { get; }
        // Each person it knows: their id, the friendship points, the days it last talked, gave them a gift and did their
        // request, and the heart gifts it has had from them (a bit per heart).
        public IReadOnlyList<(string Npc, int Points, int TalkedDay, int GiftedDay, int RequestDay, int HeartGifts)> Friendships { get; }
        // Experience in each life skill, in LifeSkill order (empty for a save from before them).
        public IReadOnlyList<int> LifeExperience { get; }
        // The skills it learned and their ranks, by id (World.RestoreSkills puts back what the sheet still has).
        public IReadOnlyList<(string Id, int Rank)> SkillRanks { get; }
        // The last story step done with each person, by person id.
        public IReadOnlyList<(string Npc, int Step)> Stories { get; }
        // The crystals set in the skills, one pair each: the skill's id and the crystal's (World.RestoreSupports).
        public IReadOnlyList<(string Skill, string Gem)> Supports { get; }
        // The bestiary: what kind of record (RecordKind's name), whose id, how many (World.RestoreRecords).
        public IReadOnlyList<(string Kind, string Id, int Count)> Records { get; }
        // The ids of the achievements had.
        public IReadOnlyList<string> Achievements { get; }
        public IReadOnlyList<string> Warnings { get; }

        public static byte[] Write(Equipment equipment, Inventory bag, int experience = 0, IEnumerable<string> claimedGoals = null,
            IEnumerable<string> passives = null, IEnumerable<(string Npc, int Points, int TalkedDay, int GiftedDay, int RequestDay, int HeartGifts)> friendships = null,
            IEnumerable<int> life = null, IEnumerable<(string Id, int Rank)> skillRanks = null, IEnumerable<(string Npc, int Step)> stories = null,
            IEnumerable<(string Skill, string Gem)> supports = null, IEnumerable<(string Kind, string Id, int Count)> records = null,
            IEnumerable<string> achievements = null)
        {
            using var stream = new MemoryStream();
            using (var w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                w.Write(Encoding.ASCII.GetBytes(Magic));
                w.Write(FormatVersion);
                var worn = equipment.Items.ToList();
                w.Write((byte)worn.Count);
                foreach (var item in worn) SavedItems.Write(w, item);
                w.Write((ushort)bag.Items.Count);
                foreach (var item in bag.Items) SavedItems.Write(w, item);
                SavedItems.WriteCounts(w, bag.Runes.Select(p => (p.Key.Id, p.Value)));
                SavedItems.WriteCounts(w, bag.Consumables.Select(p => (p.Key.Id, p.Value)));
                SavedItems.WriteCounts(w, bag.Materials.Select(p => (p.Key.Id, p.Value)));
                w.Write(experience);
                var claimed = (claimedGoals ?? Enumerable.Empty<string>()).ToList();
                w.Write((ushort)claimed.Count);
                foreach (string goal in claimed) w.Write(goal);
                var taken = (passives ?? Enumerable.Empty<string>()).ToList();
                w.Write((ushort)taken.Count);
                foreach (string point in taken) w.Write(point);
                var known = (friendships ?? Enumerable.Empty<(string, int, int, int, int, int)>()).ToList();
                w.Write((ushort)known.Count);
                foreach (var (npc, points, talked, gifted, request, heartGifts) in known)
                {
                    w.Write(npc);
                    w.Write(points);
                    w.Write(talked);
                    w.Write(gifted);
                    w.Write(request);
                    w.Write(heartGifts);
                }
                var skills = (life ?? Enumerable.Empty<int>()).ToList();
                w.Write((byte)skills.Count);
                foreach (int points in skills) w.Write(points);
                var ranks = (skillRanks ?? Enumerable.Empty<(string, int)>()).ToList();
                w.Write((ushort)ranks.Count);
                foreach (var (id, rank) in ranks)
                {
                    w.Write(id);
                    w.Write((byte)rank);
                }
                var told = (stories ?? Enumerable.Empty<(string, int)>()).ToList();
                w.Write((ushort)told.Count);
                foreach (var (npc, step) in told)
                {
                    w.Write(npc);
                    w.Write((byte)step);
                }
                var set = (supports ?? Enumerable.Empty<(string, string)>()).ToList();
                w.Write((ushort)set.Count);
                foreach (var (skill, gem) in set)
                {
                    w.Write(skill);
                    w.Write(gem);
                }
                var seen = (records ?? Enumerable.Empty<(string, string, int)>()).ToList();
                w.Write((ushort)seen.Count);
                foreach (var (kind, id, count) in seen)
                {
                    w.Write(kind);
                    w.Write(id);
                    w.Write(count);
                }
                var had = (achievements ?? Enumerable.Empty<string>()).ToList();
                w.Write((ushort)had.Count);
                foreach (string id in had) w.Write(id);
            }
            return stream.ToArray();
        }

        // Throws InvalidDataException when the data is not a character save this game can read.
        public static CharacterSave Read(byte[] data, LootData loot)
        {
            var warnings = new List<string>();
            try
            {
                using var r = new BinaryReader(new MemoryStream(data ?? Array.Empty<byte>()), Encoding.UTF8);
                int version = SavedItems.ReadHeader(r, Magic, FormatVersion, "캐릭터");

                var equipment = new Equipment();
                var bag = new Inventory();
                int worn = r.ReadByte();
                for (int i = 0; i < worn; i++)
                {
                    var item = SavedItems.Read(r, loot, warnings);
                    if (item == null) continue;
                    if (equipment[item.Base.Slot] == null) equipment.Equip(item);
                    else if (!bag.TryAdd(item)) warnings.Add($"가방이 가득 차서 {item.Base.Name}을(를) 버렸습니다.");
                }
                int carried = r.ReadUInt16();
                for (int i = 0; i < carried; i++)
                {
                    var item = SavedItems.Read(r, loot, warnings);
                    if (item != null && !bag.TryAdd(item)) warnings.Add($"가방이 가득 차서 {item.Base.Name}을(를) 버렸습니다.");
                }
                foreach (var (id, count) in SavedItems.ReadCounts(r))
                {
                    var rune = loot.Runes.FirstOrDefault(x => x.Id == id);
                    if (rune != null) bag.AddRune(rune, count);
                    else warnings.Add($"없어진 룬 '{id}' {count}개를 건너뛰었습니다.");
                }
                foreach (var (id, count) in SavedItems.ReadCounts(r))
                {
                    var consumable = loot.Consumables.FirstOrDefault(x => x.Id == id);
                    if (consumable != null) bag.AddConsumable(consumable, count);
                    else warnings.Add($"없어진 소모품 '{id}' {count}개를 건너뛰었습니다.");
                }
                foreach (var (id, count) in SavedItems.ReadCounts(r))
                {
                    var material = loot.Materials.FirstOrDefault(x => x.Id == id);
                    if (material != null) bag.AddMaterial(material, count);
                    else warnings.Add($"없어진 재료 '{id}' {count}개를 건너뛰었습니다.");
                }
                int experience = version >= 2 ? Math.Max(0, r.ReadInt32()) : -1;
                var claimed = new List<string>();
                for (int count = version >= 3 ? r.ReadUInt16() : 0; count > 0; count--) claimed.Add(r.ReadString());
                var passives = new List<string>();
                for (int count = version >= 4 ? r.ReadUInt16() : 0; count > 0; count--) passives.Add(r.ReadString());
                var friendships = new List<(string, int, int, int, int, int)>();
                for (int count = version >= 5 ? r.ReadUInt16() : 0; count > 0; count--)
                {
                    string npc = r.ReadString();
                    int points = r.ReadInt32(), talked = r.ReadInt32(), gifted = r.ReadInt32();
                    int request = version >= 7 ? r.ReadInt32() : 0;
                    int heartGifts = version >= 7 ? r.ReadInt32() : 0;
                    friendships.Add((npc, points, talked, gifted, request, heartGifts));
                }
                var life = new List<int>();
                for (int count = version >= 6 ? r.ReadByte() : 0; count > 0; count--) life.Add(r.ReadInt32());
                var ranks = new List<(string, int)>();
                for (int count = version >= 8 ? r.ReadUInt16() : 0; count > 0; count--) ranks.Add((r.ReadString(), r.ReadByte()));
                var stories = new List<(string, int)>();
                for (int count = version >= 9 ? r.ReadUInt16() : 0; count > 0; count--) stories.Add((r.ReadString(), r.ReadByte()));
                var supports = new List<(string, string)>();
                for (int count = version >= 10 ? r.ReadUInt16() : 0; count > 0; count--) supports.Add((r.ReadString(), r.ReadString()));
                var records = new List<(string, string, int)>();
                for (int count = version >= 11 ? r.ReadUInt16() : 0; count > 0; count--) records.Add((r.ReadString(), r.ReadString(), r.ReadInt32()));
                var achievements = new List<string>();
                for (int count = version >= 11 ? r.ReadUInt16() : 0; count > 0; count--) achievements.Add(r.ReadString());
                return new CharacterSave(equipment, bag, experience, warnings, claimed, passives, friendships, life, ranks, stories, supports, records, achievements);
            }
            catch (Exception e) when (e is EndOfStreamException || e is IOException || e is ArgumentException)
            {
                throw new InvalidDataException("캐릭터 저장본이 깨졌습니다.", e);
            }
        }
    }

    // Items and stacks in save files, by id.
    static class SavedItems
    {
        // Returns the format version the save was written in.
        public static int ReadHeader(BinaryReader r, string magic, int version, string what)
        {
            if (Encoding.ASCII.GetString(r.ReadBytes(magic.Length)) != magic) throw new InvalidDataException($"{what} 저장본이 아닙니다.");
            int saved = r.ReadInt32();
            if (saved > version) throw new InvalidDataException($"더 새 게임에서 만든 {what} 저장본입니다(형식 {saved}).");
            return saved;
        }

        public static void Write(BinaryWriter w, Item item)
        {
            w.Write(item.Base.Id);
            w.Write((short)item.Level);
            // The anvil's upgrade in the top half of the rarity byte; saves from before it read as +0.
            w.Write((byte)((int)item.Rarity | item.Upgrade << 4));
            w.Write((byte)item.Implicits.Count);
            foreach (var modifier in item.Implicits) w.Write(modifier.Value);
            w.Write((byte)item.Affixes.Count);
            foreach (var affix in item.Affixes)
            {
                w.Write(affix.Affix.Id);
                w.Write((byte)affix.Tier.Number);
                w.Write(affix.Modifier.Value);
            }
            w.Write((byte)item.Sockets.Count);
            foreach (var rune in item.Sockets) w.Write(rune?.Id ?? "");
        }

        // Null, with a warning, when the item's base is gone.
        public static Item Read(BinaryReader r, LootData loot, List<string> warnings)
        {
            string baseId = r.ReadString();
            int level = r.ReadInt16();
            int rarityByte = r.ReadByte();
            var rarity = (Rarity)(rarityByte & 0x0F);
            var implicitValues = new List<int>();
            for (int count = r.ReadByte(); count > 0; count--) implicitValues.Add(r.ReadInt32());
            var affixes = new List<(string Id, int Tier, int Value)>();
            for (int count = r.ReadByte(); count > 0; count--) affixes.Add((r.ReadString(), r.ReadByte(), r.ReadInt32()));
            var runes = new List<string>();
            for (int count = r.ReadByte(); count > 0; count--) runes.Add(r.ReadString());

            var itemBase = loot.Bases.FirstOrDefault(b => b.Id == baseId);
            if (itemBase == null)
            {
                warnings.Add($"없어진 아이템 '{baseId}'를 건너뛰었습니다.");
                return null;
            }
            // Implicits by position: a base that gained one gets its lowest roll for it.
            var implicits = itemBase.Implicits
                .Select((range, i) => new Modifier(range.Stat, range.Kind, i < implicitValues.Count ? implicitValues[i] : range.Min, range.Tags))
                .ToList();
            var rolled = new List<ItemAffix>();
            foreach (var (id, tierNumber, value) in affixes)
            {
                var affix = loot.Affixes.FirstOrDefault(a => a.Id == id);
                if (affix == null || affix.Tiers.Count == 0)
                {
                    warnings.Add($"{itemBase.Name}의 없어진 접사 '{id}'를 건너뛰었습니다.");
                    continue;
                }
                var tier = affix.Tiers[Math.Clamp(tierNumber, 1, affix.Tiers.Count) - 1];
                rolled.Add(new ItemAffix(tier, new Modifier(tier.Modifier.Stat, tier.Modifier.Kind, value, tier.Modifier.Tags)));
            }
            var item = new Item(itemBase, level, rarity, implicits, rolled, Math.Min(runes.Count, itemBase.MaxSockets))
            {
                Upgrade = Math.Min(Item.MaxUpgrade, rarityByte >> 4),
            };
            for (int socket = 0; socket < item.Sockets.Count; socket++)
            {
                if (runes[socket].Length == 0) continue;
                var rune = loot.Runes.FirstOrDefault(x => x.Id == runes[socket]);
                if (rune != null) item.TryInsertRune(socket, rune, loot.Runewords);
                else warnings.Add($"{itemBase.Name}에 끼워 둔 없어진 룬 '{runes[socket]}'를 건너뛰었습니다.");
            }
            return item;
        }

        public static void WriteCounts(BinaryWriter w, IEnumerable<(string Id, int Count)> stacks)
        {
            var list = stacks.ToList();
            w.Write((ushort)list.Count);
            foreach (var (id, count) in list)
            {
                w.Write(id);
                w.Write(count);
            }
        }

        public static List<(string Id, int Count)> ReadCounts(BinaryReader r)
        {
            var list = new List<(string, int)>();
            for (int count = r.ReadUInt16(); count > 0; count--)
            {
                string id = r.ReadString();
                int amount = r.ReadInt32();
                if (amount > 0) list.Add((id, amount));
            }
            return list;
        }
    }
}
