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
    }

    // A player asking the host to change their gear or use a consumable. Item is an item id; Slot is used by
    // Unequip; Index is the rune sheet index for InsertRune and the consumable sheet index for UseConsumable.
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

    // Everything a player carries: what they wear, their bag, runes and consumables. Sent whenever any of it changes.
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

            w.Write((byte)Bag.Items.Count);
            foreach (var item in Bag.Items) ItemCodec.Write(w, item, data);

            var runes = Bag.Runes.OrderBy(pair => ItemCodec.Index(data.Runes, pair.Key)).ToList();
            w.Write((byte)runes.Count);
            foreach (var pair in runes)
            {
                w.Write((byte)ItemCodec.Index(data.Runes, pair.Key));
                w.Write((ushort)System.Math.Min(pair.Value, ushort.MaxValue));
            }

            var consumables = Bag.Consumables.OrderBy(pair => ItemCodec.Index(data.Consumables, pair.Key)).ToList();
            w.Write((byte)consumables.Count);
            foreach (var pair in consumables)
            {
                w.Write((byte)ItemCodec.Index(data.Consumables, pair.Key));
                w.Write((ushort)System.Math.Min(pair.Value, ushort.MaxValue));
            }
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

            var bag = new Inventory();
            int count = r.ReadByte();
            for (int i = 0; i < count; i++)
            {
                if (!bag.TryAdd(ItemCodec.Read(r, loot))) throw new InvalidDataException("More items than a bag holds.");
            }

            int runeKinds = r.ReadByte();
            for (int i = 0; i < runeKinds; i++)
            {
                int index = r.ReadByte();
                if (index >= loot.Runes.Count) throw new InvalidDataException("Unknown rune.");
                bag.AddRune(loot.Runes[index], r.ReadUInt16());
            }

            int consumableKinds = r.ReadByte();
            for (int i = 0; i < consumableKinds; i++)
            {
                int index = r.ReadByte();
                if (index >= loot.Consumables.Count) throw new InvalidDataException("Unknown consumable.");
                bag.AddConsumable(loot.Consumables[index], r.ReadUInt16());
            }
            return new InventoryMessage(equipment, bag);
        });
    }
}
