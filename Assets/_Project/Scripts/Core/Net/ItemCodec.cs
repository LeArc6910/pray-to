using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Core
{
    // Items on the wire. Bases, affixes and runes travel as indexes into the loot sheets, which host and client
    // both load (the protocol version check keeps them the same), and only the rolled values are sent.
    public static class ItemCodec
    {
        const byte EmptySocket = 255;

        public static void Write(BinaryWriter writer, Item item, LootData data)
        {
            writer.Write(item.Id);
            writer.Write((ushort)Index(data.Bases, item.Base));
            writer.Write((byte)item.Level);
            writer.Write((byte)item.Rarity);

            writer.Write((byte)item.Implicits.Count);
            foreach (var modifier in item.Implicits) writer.Write((short)modifier.Value);

            writer.Write((byte)item.Affixes.Count);
            foreach (var affix in item.Affixes)
            {
                writer.Write((ushort)Index(data.Affixes, affix.Affix));
                writer.Write((byte)affix.Tier.Number);
                writer.Write((short)affix.Modifier.Value);
            }

            writer.Write((byte)item.Sockets.Count);
            foreach (var rune in item.Sockets) writer.Write(rune == null ? EmptySocket : (byte)Index(data.Runes, rune));
        }

        public static Item Read(BinaryReader reader, LootData data)
        {
            int id = reader.ReadInt32();
            var itemBase = At(data.Bases, reader.ReadUInt16());
            int level = reader.ReadByte();
            var rarity = (Rarity)reader.ReadByte();

            int implicitCount = reader.ReadByte();
            if (implicitCount != itemBase.Implicits.Count) throw new InvalidDataException("Implicit count does not match the base.");
            var implicits = new List<Modifier>();
            foreach (var range in itemBase.Implicits) implicits.Add(new Modifier(range.Stat, range.Kind, reader.ReadInt16(), range.Tags));

            int affixCount = reader.ReadByte();
            var affixes = new List<ItemAffix>();
            for (int i = 0; i < affixCount; i++)
            {
                var affix = At(data.Affixes, reader.ReadUInt16());
                var tier = At(affix.Tiers, reader.ReadByte() - 1);
                var range = tier.Modifier;
                affixes.Add(new ItemAffix(tier, new Modifier(range.Stat, range.Kind, reader.ReadInt16(), range.Tags)));
            }

            int socketCount = reader.ReadByte();
            if (socketCount > itemBase.MaxSockets) throw new InvalidDataException("More sockets than the base allows.");
            var item = new Item(itemBase, level, rarity, implicits, affixes, socketCount) { Id = id };
            for (int socket = 0; socket < socketCount; socket++)
            {
                byte rune = reader.ReadByte();
                if (rune != EmptySocket) item.TryInsertRune(socket, At(data.Runes, rune), data.Runewords);
            }
            return item;
        }

        // A bag or a chest: its items, then its runes, consumables and materials by sheet index and count.
        public static void WriteBag(BinaryWriter w, Inventory bag, LootData data)
        {
            w.Write((byte)bag.Items.Count);
            foreach (var item in bag.Items) Write(w, item, data);
            WriteStacks(w, bag.Runes, data.Runes);
            WriteStacks(w, bag.Consumables, data.Consumables);
            WriteStacks(w, bag.Materials, data.Materials);
        }

        public static Inventory ReadBag(BinaryReader r, LootData loot)
        {
            var bag = new Inventory();
            int count = r.ReadByte();
            for (int i = 0; i < count; i++)
            {
                if (!bag.TryAdd(Read(r, loot))) throw new InvalidDataException("More items than a bag holds.");
            }
            foreach (var (rune, amount) in ReadStacks(r, loot.Runes)) bag.AddRune(rune, amount);
            foreach (var (consumable, amount) in ReadStacks(r, loot.Consumables)) bag.AddConsumable(consumable, amount);
            foreach (var (material, amount) in ReadStacks(r, loot.Materials)) bag.AddMaterial(material, amount);
            return bag;
        }

        static void WriteStacks<T>(BinaryWriter w, IReadOnlyDictionary<T, int> stacks, IReadOnlyList<T> sheet) where T : class
        {
            var ordered = stacks.OrderBy(pair => Index(sheet, pair.Key)).ToList();
            w.Write((byte)ordered.Count);
            foreach (var pair in ordered)
            {
                w.Write((byte)Index(sheet, pair.Key));
                w.Write((ushort)System.Math.Min(pair.Value, ushort.MaxValue));
            }
        }

        static List<(T, int)> ReadStacks<T>(BinaryReader r, IReadOnlyList<T> sheet)
        {
            var stacks = new List<(T, int)>();
            for (int kinds = r.ReadByte(); kinds > 0; kinds--) stacks.Add((At(sheet, r.ReadByte()), r.ReadUInt16()));
            return stacks;
        }

        internal static int Index<T>(IReadOnlyList<T> list, T item) where T : class
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == item) return i;
            }
            throw new InvalidDataException($"{typeof(T).Name} is not in the loot sheets.");
        }

        static T At<T>(IReadOnlyList<T> list, int index) =>
            index >= 0 && index < list.Count ? list[index] : throw new InvalidDataException($"No {typeof(T).Name} at index {index}.");
    }

    public enum InventoryCommand : byte
    {
        Equip,
        Unequip,
        InsertRune,
        Discard,
        UseConsumable,
        Craft,
        Place,
        Plant,
        // Chest commands: Item is the item id, or for a stack its StackKind with Index its sheet index.
        StoreItem,
        TakeItem,
        StoreStack,
        TakeStack,
    }

    // A player asking the host to change their gear, use a consumable, craft or build. Item is an item id; Slot is
    // used by Unequip; Index is a sheet index: the rune for InsertRune, the consumable for UseConsumable, the
    // recipe for Craft, the material for Place and the crop for Plant.
    public sealed class InventoryCommandMessage
    {
        public InventoryCommandMessage(InventoryCommand command, int item, ItemSlot slot = ItemSlot.Weapon, int index = 0)
        {
            Command = command;
            Item = item;
            Slot = slot;
            Index = index;
        }

        public InventoryCommand Command { get; }
        public int Item { get; }
        public ItemSlot Slot { get; }
        public int Index { get; }

        public byte[] Encode() => Protocol.Build(MessageType.InventoryCommand, w =>
        {
            w.Write((byte)Command);
            w.Write(Item);
            w.Write((byte)Slot);
            w.Write((byte)Index);
        });

        public static InventoryCommandMessage Decode(byte[] data) => Protocol.Read(data, MessageType.InventoryCommand, r =>
            new InventoryCommandMessage((InventoryCommand)r.ReadByte(), r.ReadInt32(), (ItemSlot)r.ReadByte(), r.ReadByte()));
    }

    // Everything a player carries: what they wear, their bag, runes, consumables and materials. Sent whenever any of
    // it changes.
    public sealed class InventoryMessage
    {
        public InventoryMessage(Equipment equipment, Inventory bag)
        {
            Equipment = equipment;
            Bag = bag;
        }

        public Equipment Equipment { get; }
        public Inventory Bag { get; }

        public byte[] Encode(LootData data) => Protocol.Build(MessageType.Inventory, w =>
        {
            foreach (ItemSlot slot in System.Enum.GetValues(typeof(ItemSlot)))
            {
                var item = Equipment[slot];
                w.Write(item != null);
                if (item != null) ItemCodec.Write(w, item, data);
            }
            ItemCodec.WriteBag(w, Bag, data);
        });

        public static InventoryMessage Decode(byte[] data, LootData loot) => Protocol.Read(data, MessageType.Inventory, r =>
        {
            var equipment = new Equipment();
            foreach (ItemSlot slot in System.Enum.GetValues(typeof(ItemSlot)))
            {
                if (!r.ReadBoolean()) continue;
                var item = ItemCodec.Read(r, loot);
                if (item.Base.Slot != slot) throw new InvalidDataException("Item in the wrong slot.");
                equipment.Equip(item);
            }
            return new InventoryMessage(equipment, ItemCodec.ReadBag(r, loot));
        });
    }

    // What the chest the player stands by holds, sent when they come near it and whenever it changes; a closed
    // message (no chest) when they walk away. See Docs/Crafting.md.
    public sealed class ChestMessage
    {
        public ChestMessage(int x, int y, Inventory contents)
        {
            X = x;
            Y = y;
            Contents = contents;
        }

        public static ChestMessage Closed { get; } = new ChestMessage(-1, -1, null);

        public int X { get; }
        public int Y { get; }
        public Inventory Contents { get; }
        public bool IsOpen => Contents != null;

        public byte[] Encode(LootData data) => Protocol.Build(MessageType.Chest, w =>
        {
            w.Write(IsOpen);
            if (!IsOpen) return;
            w.Write((ushort)X);
            w.Write((ushort)Y);
            ItemCodec.WriteBag(w, Contents, data);
        });

        public static ChestMessage Decode(byte[] data, LootData loot) => Protocol.Read(data, MessageType.Chest, r =>
            r.ReadBoolean() ? new ChestMessage(r.ReadUInt16(), r.ReadUInt16(), ItemCodec.ReadBag(r, loot)) : Closed);
    }
}
