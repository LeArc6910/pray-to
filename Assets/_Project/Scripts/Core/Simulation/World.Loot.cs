using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // Drops and the bag. Monsters roll their drop chances when killed; bosses drop a guaranteed reward for
    // every player in the fight and raise ProgressLevel, so everything afterwards drops the next step of gear.
    // Loot lies on the floor until someone walks over it, and the commands below are how a player's client
    // asks the host to change their gear.
    public sealed partial class World
    {
        const float PickupRadius = 0.6f;
        const float LootSeconds = 300;
        const int MaxGroundLoot = 200;
        const float ScatterRadius = 0.9f;
        // A discarded item lands in front of the player and is left alone this long, so it is not picked
        // straight back up.
        const float DiscardSeconds = 5;
        const float DiscardDistance = 1.2f;

        readonly LootGenerator _lootGenerator;
        readonly List<GroundLoot> _groundLoot = new List<GroundLoot>();

        public LootData Loot { get; }
        public IReadOnlyList<GroundLoot> GroundLoot => _groundLoot;
        // Drops are at least this item level. Starts at the first step; each boss raises it to its DropLevel.
        public int ProgressLevel { get; private set; } = 1;

        public GroundLoot FindLoot(int id) => _groundLoot.FirstOrDefault(l => l.Id == id);

        // Puts an item on the floor, for tests and scripted rewards.
        public GroundLoot DropItem(Item item, Vec2 position) => Place(Register(item), null, position, Tick);

        public GroundLoot DropRune(RuneDefinition rune, Vec2 position) => Place(null, rune, position, Tick);

        Item Register(Item item)
        {
            if (item.Id == 0) item.Id = _nextId++;
            return item;
        }

        void DropFrom(Enemy enemy, Player killer)
        {
            if (_lootGenerator == null) return;
            var definition = enemy.Definition;
            int level = Math.Max(definition.Level, ProgressLevel);
            if (_random.Chance(definition.DropChance / 100f))
            {
                DropItem(_lootGenerator.RollItem(level, _random, (int)killer.Stats.ItemRarity), Scatter(enemy.Position));
            }
            if (_random.Chance(definition.RuneChance / 100f))
            {
                var rune = _lootGenerator.RollRune(level, _random);
                if (rune != null) DropRune(rune, Scatter(enemy.Position));
            }
        }

        void DropFrom(Boss boss)
        {
            var definition = boss.Definition;
            ProgressLevel = Math.Max(ProgressLevel, definition.DropLevel);
            if (_lootGenerator == null) return;

            int fighters = Math.Max(1, Fighters(boss).Count);
            for (int i = 0; i < definition.DropCount * fighters; i++)
            {
                DropItem(_lootGenerator.RollItem(definition.DropLevel, _random, 0, definition.DropRarity), Scatter(boss.Position, 2));
            }
            for (int i = 0; i < definition.RuneCount * fighters; i++)
            {
                var rune = _lootGenerator.RollRune(definition.DropLevel, _random);
                if (rune != null) DropRune(rune, Scatter(boss.Position, 2));
            }
        }

        Vec2 Scatter(Vec2 origin, float spread = ScatterRadius)
        {
            var offset = Vec2.FromAngle(_random.NextFloat() * 2 * MathF.PI) * (MathF.Sqrt(_random.NextFloat()) * spread);
            return Collision.Resolve(Map, origin + offset, 0.2f);
        }

        GroundLoot Place(Item item, RuneDefinition rune, Vec2 position, long readyTick)
        {
            var loot = new GroundLoot(_nextId++, position, item, rune, readyTick, Tick + Ticks(LootSeconds));
            _groundLoot.Add(loot);
            // The oldest goes first when the floor fills up.
            if (_groundLoot.Count > MaxGroundLoot) _groundLoot.RemoveAt(0);
            return loot;
        }

        void UpdateLoot()
        {
            _groundLoot.RemoveAll(l => Tick >= l.ExpireTick);
            foreach (var player in _players)
            {
                if (player.IsDead) continue;
                // Oldest first, so the bag fills in the order things dropped.
                for (int i = 0; i < _groundLoot.Count; i++)
                {
                    var loot = _groundLoot[i];
                    if (Tick < loot.ReadyTick || Vec2.Distance(loot.Position, player.Position) > PickupRadius + PlayerRadius) continue;
                    if (loot.Item != null && !player.Inventory.TryAdd(loot.Item)) continue;
                    if (loot.Rune != null) player.Inventory.AddRune(loot.Rune);
                    _groundLoot.RemoveAt(i--);
                    player.InventoryVersion++;
                    _events.Add(new SimEvent(SimEventType.Pickup, player.Id, loot.Id));
                }
            }
        }

        // Moves an item from the bag into its slot; whatever was there takes its place in the bag.
        public bool EquipItem(int playerId, int itemId)
        {
            var player = FindPlayer(playerId);
            var item = player?.Inventory.Find(itemId);
            if (item == null || player.IsDead) return false;
            player.Inventory.Replace(item, player.Equipment[item.Base.Slot]);
            player.Equip(item);
            return true;
        }

        public bool UnequipItem(int playerId, ItemSlot slot)
        {
            var player = FindPlayer(playerId);
            var item = player?.Equipment[slot];
            if (item == null || player.IsDead || player.Inventory.IsFull) return false;
            player.Unequip(slot);
            player.Inventory.TryAdd(item);
            return true;
        }

        // Fills the item's first empty socket with one of the player's runes. Works on worn items too.
        public bool InsertRune(int playerId, int itemId, RuneDefinition rune)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || rune == null || player.Inventory.CountOf(rune) == 0) return false;
            bool worn = player.Equipment.Items.Any(i => i.Id == itemId);
            var item = worn ? player.Equipment.Items.First(i => i.Id == itemId) : player.Inventory.Find(itemId);
            int socket = item == null ? -1 : Array.IndexOf(item.Sockets.ToArray(), null);
            if (socket < 0) return false;

            player.Inventory.TakeRune(rune);
            item.TryInsertRune(socket, rune, Loot?.Runewords ?? Array.Empty<Runeword>());
            if (worn) player.RefreshStats();
            player.InventoryVersion++;
            return true;
        }

        // Drops an item from the bag on the floor in front of the player.
        public bool DiscardItem(int playerId, int itemId)
        {
            var player = FindPlayer(playerId);
            var item = player?.Inventory.Find(itemId);
            if (item == null || player.IsDead) return false;
            player.Inventory.Remove(item);
            player.InventoryVersion++;
            var position = Collision.Resolve(Map, player.Position + player.Facing * DiscardDistance, 0.2f);
            Place(item, null, position, Tick + Ticks(DiscardSeconds));
            return true;
        }
    }
}
