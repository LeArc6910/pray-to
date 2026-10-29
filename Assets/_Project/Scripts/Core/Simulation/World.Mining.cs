using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // Digging walls for materials, building with them, and crafting. A wall takes its hardness in hits, less with
    // mining speed; partly dug walls keep their damage. Every tile that changes is recorded, so the host can tell
    // clients that joined later. See Docs/Crafting.md.
    public sealed partial class World
    {
        // How far ahead of the player's centre the tile to dig or build on is.
        const float DigReach = 0.9f;
        const float BuildReach = 1;

        readonly Dictionary<int, float> _wallDamage = new Dictionary<int, float>();
        readonly Dictionary<int, TileType> _changedTiles = new Dictionary<int, TileType>();
        readonly List<TileChange> _newTileChanges = new List<TileChange>();

        public TileData Tiles { get; }

        // Every tile that differs from what the seed made, for players who join later.
        public List<TileChange> ChangedTiles() =>
            _changedTiles.Select(pair => new TileChange(pair.Key % Map.Width, pair.Key / Map.Width, pair.Value)).ToList();

        // Tiles changed since the last call.
        public List<TileChange> TakeTileChanges()
        {
            var changes = new List<TileChange>(_newTileChanges);
            _newTileChanges.Clear();
            return changes;
        }

        // The cell ahead of the player, if it is a wall they can dig.
        (int X, int Y, TileDefinition Tile)? DigTarget(Player player)
        {
            if (Tiles == null) return null;
            var ahead = player.Position + player.Facing * DigReach;
            int x = TileMap.Cell(ahead.X);
            int y = TileMap.Cell(ahead.Y);
            if (Map.IsEdge(x, y)) return null;
            var tile = Tiles.Find(Map[x, y]);
            if (tile == null) return null;
            return (x, y, tile);
        }

        void Dig(Player player)
        {
            var target = DigTarget(player);
            if (target == null) return;
            var (x, y, tile) = target.Value;
            int key = y * Map.Width + x;
            float damage = _wallDamage.GetValueOrDefault(key) + (100 + player.Stats.MiningSpeed) / 100f;
            bool broken = damage >= tile.Hardness - 1e-4f;
            _events.Add(new SimEvent(SimEventType.Dug, player.Id, amount: broken ? 1 : 0, position: TileMap.Center(x, y)));
            if (!broken)
            {
                _wallDamage[key] = damage;
                return;
            }

            _wallDamage.Remove(key);
            SetTile(x, y, TileType.Floor);
            player.Inventory.AddMaterial(tile.Material, tile.Count);
            if (tile.Extra != null && tile.ExtraChance > 0 && _random.Chance(tile.ExtraChance / 100f)) player.Inventory.AddMaterial(tile.Extra);
            player.InventoryVersion++;
        }

        // Puts the material's tile on the floor ahead of the player. False when it cannot be placed there: not a
        // floor, someone or a crop in the way, inside a boss arena, or no such material in the bag.
        public bool PlaceTile(int playerId, MaterialDefinition material)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || material?.Places == null || player.Inventory.CountOf(material) == 0) return false;
            var ahead = player.Position + player.Facing * BuildReach;
            int x = TileMap.Cell(ahead.X);
            int y = TileMap.Cell(ahead.Y);
            if (Map.IsEdge(x, y) || Map[x, y] != TileType.Floor || CropAt(x, y) != null) return false;
            if (_players.Any(p => !p.IsDead && Touches(p.Position, PlayerRadius, x, y))
                || _enemies.Any(e => !e.IsDead && Touches(e.Position, e.Definition.Radius, x, y))
                || _bosses.Any(b => Touches(b.Position, b.Definition.Radius, x, y) || Vec2.Distance(TileMap.Center(x, y), b.Home) <= b.ArenaRadius + 1))
            {
                return false;
            }

            player.Inventory.TakeMaterial(material);
            player.InventoryVersion++;
            SetTile(x, y, material.Places.Value);
            return true;
        }

        // Makes the recipe from the player's materials. False when something is missing, or the bag has no room
        // for an item.
        public bool Craft(int playerId, Recipe recipe)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || recipe == null) return false;
            if (recipe.Ingredients.Any(i => player.Inventory.CountOf(i.Material) < i.Count)) return false;
            if (recipe.Item != null && (player.Inventory.IsFull || _lootGenerator == null)) return false;

            foreach (var (material, count) in recipe.Ingredients) player.Inventory.TakeMaterial(material, count);
            if (recipe.Item != null) player.Inventory.TryAdd(Register(_lootGenerator.RollItem(recipe.Item, recipe.Item.Level, Rarity.Common, _random)));
            if (recipe.Consumable != null) player.Inventory.AddConsumable(recipe.Consumable, recipe.Count);
            if (recipe.Material != null) player.Inventory.AddMaterial(recipe.Material, recipe.Count);
            player.InventoryVersion++;
            return true;
        }

        // Puts back what a world save holds: changed tiles, crops planted the given number of ticks before now, and
        // the drop level bosses had raised. Nothing is announced; players who join get it all as usual.
        public void Restore(IEnumerable<TileChange> tiles, IEnumerable<(CropDefinition Crop, int X, int Y, long Age)> crops, int progressLevel)
        {
            foreach (var tile in tiles)
            {
                if (Map.InBounds(tile.X, tile.Y) && !Map.IsEdge(tile.X, tile.Y)) SetTile(tile.X, tile.Y, tile.Type);
            }
            foreach (var (crop, x, y, age) in crops)
            {
                if (!Map.InBounds(x, y) || Map[x, y] != TileType.Floor) continue;
                _crops[y * Map.Width + x] = new Crop(crop, x, y, Tick - age);
            }
            ProgressLevel = Math.Max(ProgressLevel, progressLevel);
            _newTileChanges.Clear();
            _newCropChanges.Clear();
        }

        void SetTile(int x, int y, TileType type)
        {
            Map[x, y] = type;
            _changedTiles[y * Map.Width + x] = type;
            _newTileChanges.Add(new TileChange(x, y, type));
        }

        // Whether a circle overlaps the tile's square.
        static bool Touches(Vec2 centre, float radius, int x, int y)
        {
            float nearestX = Math.Clamp(centre.X, x, x + 1);
            float nearestY = Math.Clamp(centre.Y, y, y + 1);
            float dx = centre.X - nearestX;
            float dy = centre.Y - nearestY;
            return dx * dx + dy * dy < radius * radius;
        }
    }
}
