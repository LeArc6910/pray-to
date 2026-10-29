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
        const string Magic = "PTCH";
        const int FormatVersion = 1;

        CharacterSave(Equipment equipment, Inventory bag, List<string> warnings)
        {
            Equipment = equipment;
            Bag = bag;
            Warnings = warnings;
        }

        public Equipment Equipment { get; }
        public Inventory Bag { get; }
        public IReadOnlyList<string> Warnings { get; }

        public static byte[] Write(Equipment equipment, Inventory bag)
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
                SavedItems.ReadHeader(r, Magic, FormatVersion, "캐릭터");

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
                return new CharacterSave(equipment, bag, warnings);
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
        public static void ReadHeader(BinaryReader r, string magic, int version, string what)
        {
            if (Encoding.ASCII.GetString(r.ReadBytes(magic.Length)) != magic) throw new InvalidDataException($"{what} 저장본이 아닙니다.");
            int saved = r.ReadInt32();
            if (saved > version) throw new InvalidDataException($"더 새 게임에서 만든 {what} 저장본입니다(형식 {saved}).");
        }

        public static void Write(BinaryWriter w, Item item)
        {
            w.Write(item.Base.Id);
            w.Write((short)item.Level);
            w.Write((byte)item.Rarity);
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
            var rarity = (Rarity)r.ReadByte();
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
            var item = new Item(itemBase, level, rarity, implicits, rolled, Math.Min(runes.Count, itemBase.MaxSockets));
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
