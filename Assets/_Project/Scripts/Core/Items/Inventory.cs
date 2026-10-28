using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // A player's bag: up to Capacity items in the order they arrived, and runes and consumables counted by kind.
    public sealed class Inventory
    {
        public const int Capacity = 24;

        readonly List<Item> _items = new List<Item>();
        readonly Dictionary<RuneDefinition, int> _runes = new Dictionary<RuneDefinition, int>();
        readonly Dictionary<ConsumableDefinition, int> _consumables = new Dictionary<ConsumableDefinition, int>();

        public IReadOnlyList<Item> Items => _items;
        public IReadOnlyDictionary<RuneDefinition, int> Runes => _runes;
        public IReadOnlyDictionary<ConsumableDefinition, int> Consumables => _consumables;
        public bool IsFull => _items.Count >= Capacity;

        public bool TryAdd(Item item)
        {
            if (IsFull) return false;
            _items.Add(item);
            return true;
        }

        public Item Find(int id) => _items.FirstOrDefault(item => item.Id == id);

        public bool Remove(Item item) => _items.Remove(item);

        // Puts replacement where item was, so equipping something keeps the bag's order.
        public void Replace(Item item, Item replacement)
        {
            int index = _items.IndexOf(item);
            if (index < 0) return;
            if (replacement == null) _items.RemoveAt(index);
            else _items[index] = replacement;
        }

        public int CountOf(RuneDefinition rune) => _runes.TryGetValue(rune, out int count) ? count : 0;

        public void AddRune(RuneDefinition rune, int count = 1)
        {
            if (count > 0) _runes[rune] = CountOf(rune) + count;
        }

        public bool TakeRune(RuneDefinition rune)
        {
            int count = CountOf(rune);
            if (count == 0) return false;
            if (count == 1) _runes.Remove(rune);
            else _runes[rune] = count - 1;
            return true;
        }

        public int CountOf(ConsumableDefinition consumable) => _consumables.TryGetValue(consumable, out int count) ? count : 0;

        public void AddConsumable(ConsumableDefinition consumable, int count = 1)
        {
            if (count > 0) _consumables[consumable] = CountOf(consumable) + count;
        }

        public bool TakeConsumable(ConsumableDefinition consumable)
        {
            int count = CountOf(consumable);
            if (count == 0) return false;
            if (count == 1) _consumables.Remove(consumable);
            else _consumables[consumable] = count - 1;
            return true;
        }

        internal void Clear()
        {
            _items.Clear();
            _runes.Clear();
            _consumables.Clear();
        }
    }
}
