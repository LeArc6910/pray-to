using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // Which of a bag's stacks a chest command moves.
    public enum StackKind : byte
    {
        Rune,
        Consumable,
        Material,
    }

    // Chests: a Chest tile holds an inventory that anyone standing within ChestReach can put into and take from.
    // Each chest counts its changes, so the host knows whom to send new contents. See Docs/Crafting.md.
    public sealed partial class World
    {
        // How close a player must be to use a crafting station or a chest, from their centre to the tile's.
        public const float StationReach = 2.5f;
        public const float ChestReach = 2;

        readonly Dictionary<int, Inventory> _chests = new Dictionary<int, Inventory>();
        readonly Dictionary<int, int> _chestVersions = new Dictionary<int, int>();

        // The chest at a tile, or null.
        public Inventory ChestAt(int x, int y) => _chests.TryGetValue(y * Map.Width + x, out var chest) ? chest : null;

        public IEnumerable<(int X, int Y, Inventory Contents)> Chests =>
            _chests.Select(pair => (pair.Key % Map.Width, pair.Key / Map.Width, pair.Value));

        // The nearest chest within reach of a position, as its tile, or null.
        public (int X, int Y)? ChestNear(Vec2 position)
        {
            (int, int)? best = null;
            float bestDistance = ChestReach;
            foreach (var pair in _chests)
            {
                int x = pair.Key % Map.Width;
                int y = pair.Key / Map.Width;
                float distance = Vec2.Distance(position, TileMap.Center(x, y));
                if (distance > bestDistance) continue;
                bestDistance = distance;
                best = (x, y);
            }
            return best;
        }

        // Goes up whenever the chest's contents change.
        public int ChestVersion(int x, int y) => _chestVersions.TryGetValue(y * Map.Width + x, out int version) ? version : 0;

        // Moves an item between the player's bag and the nearest chest. False when there is no chest in reach, the
        // item is not where it should be, or the other side is full.
        public bool MoveItem(int playerId, int itemId, bool intoChest)
        {
            if (!ChestFor(playerId, out var player, out var chest, out int key)) return false;
            var from = intoChest ? player.Inventory : chest;
            var to = intoChest ? chest : player.Inventory;
            var item = from.Find(itemId);
            if (item == null || to.IsFull) return false;
            from.Remove(item);
            to.TryAdd(item);
            Changed(player, key);
            return true;
        }

        // Moves a whole stack of runes, consumables or materials (by sheet index) between bag and nearest chest.
        public bool MoveStack(int playerId, StackKind kind, int index, bool intoChest)
        {
            if (Loot == null || !ChestFor(playerId, out var player, out var chest, out int key)) return false;
            var from = intoChest ? player.Inventory : chest;
            var to = intoChest ? chest : player.Inventory;
            switch (kind)
            {
                case StackKind.Rune when index < Loot.Runes.Count:
                    var rune = Loot.Runes[index];
                    int runes = from.CountOf(rune);
                    if (runes == 0) return false;
                    for (int i = 0; i < runes; i++) from.TakeRune(rune);
                    to.AddRune(rune, runes);
                    break;
                case StackKind.Consumable when index < Loot.Consumables.Count:
                    var consumable = Loot.Consumables[index];
                    int consumables = from.CountOf(consumable);
                    if (consumables == 0) return false;
                    for (int i = 0; i < consumables; i++) from.TakeConsumable(consumable);
                    to.AddConsumable(consumable, consumables);
                    break;
                case StackKind.Material when index < Loot.Materials.Count:
                    var material = Loot.Materials[index];
                    int materials = from.CountOf(material);
                    if (materials == 0 || !from.TakeMaterial(material, materials)) return false;
                    to.AddMaterial(material, materials);
                    break;
                default:
                    return false;
            }
            Changed(player, key);
            return true;
        }

        bool ChestFor(int playerId, out Player player, out Inventory chest, out int key)
        {
            player = FindPlayer(playerId);
            chest = null;
            key = 0;
            if (player == null || player.IsDead) return false;
            var near = ChestNear(player.Position);
            if (near == null) return false;
            key = near.Value.Y * Map.Width + near.Value.X;
            chest = _chests[key];
            return true;
        }

        void Changed(Player player, int key)
        {
            player.InventoryVersion++;
            _chestVersions[key] = _chestVersions.GetValueOrDefault(key) + 1;
        }

        // A Chest tile appeared or went: give it an empty inventory, or drop its (empty) one.
        void ChestTileChanged(int x, int y, TileType type)
        {
            int key = y * Map.Width + x;
            if (type == TileType.Chest)
            {
                if (!_chests.ContainsKey(key)) _chests[key] = new Inventory();
            }
            else
            {
                _chests.Remove(key);
            }
            _chestVersions[key] = _chestVersions.GetValueOrDefault(key) + 1;
        }

        // Fills a restored chest with what a save held; its items get ids in this world.
        internal void FillChest(int x, int y, Inventory contents)
        {
            var chest = ChestAt(x, y);
            if (chest == null) return;
            foreach (var item in contents.Items) chest.TryAdd(Register(item));
            foreach (var pair in contents.Runes) chest.AddRune(pair.Key, pair.Value);
            foreach (var pair in contents.Consumables) chest.AddConsumable(pair.Key, pair.Value);
            foreach (var pair in contents.Materials) chest.AddMaterial(pair.Key, pair.Value);
        }

        static bool IsEmpty(Inventory bag) =>
            bag.Items.Count == 0 && bag.Runes.Count == 0 && bag.Consumables.Count == 0 && bag.Materials.Count == 0;
    }
}
