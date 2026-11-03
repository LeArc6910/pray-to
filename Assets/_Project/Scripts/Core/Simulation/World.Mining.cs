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
        public const float BuildReach = 1;
        // A Dug event's Amount when the tile broke, and when the pickaxe in hand is not good enough.
        public const int DugThrough = 100;
        public const int NeedsBetterPickaxe = -2;

        readonly Dictionary<int, float> _wallDamage = new Dictionary<int, float>();
        readonly Dictionary<int, TileType> _changedTiles = new Dictionary<int, TileType>();
        readonly List<TileChange> _newTileChanges = new List<TileChange>();

        public TileData Tiles { get; }

        // Every tile that differs from what the seed made, for players who join later.
        // The tile a player at this position facing this way builds, plants and waters on, and switches machines on.
        public static (int X, int Y) TileAhead(Vec2 position, Vec2 facing)
        {
            var ahead = position + facing * BuildReach;
            return (TileMap.Cell(ahead.X), TileMap.Cell(ahead.Y));
        }

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
            if (Map.IsEdge(x, y) || Map[x, y] == TileType.Lava) return null;
            // A laid floor or a door only comes up to a pickaxe, so swinging at monsters in a base never takes it up.
            if ((TileMap.IsPaving(Map[x, y]) || Map[x, y] == TileType.Door) && !HoldsPickaxe(player)) return null;
            var tile = Tiles.Find(Map[x, y]);
            if (tile == null) return null;
            return (x, y, tile);
        }

        void Dig(Player player)
        {
            var target = DigTarget(player);
            if (target == null) return;
            var (x, y, tile) = target.Value;
            // Obsidian and the like need a better pickaxe: a Dug event with Amount -2 says so.
            int held = HoldsPickaxe(player) ? player.Equipment[ItemSlot.Weapon].Base.Level : 0;
            if (tile.Pickaxe > 0 && held < tile.Pickaxe)
            {
                _events.Add(new SimEvent(SimEventType.Dug, player.Id, amount: NeedsBetterPickaxe, position: TileMap.Center(x, y)));
                return;
            }
            // A chest with something in it stays put, so nothing is lost.
            var chest = ChestAt(x, y);
            if (chest != null && !IsEmpty(chest)) return;
            int key = y * Map.Width + x;
            float damage = _wallDamage.GetValueOrDefault(key) + DigPowerOf(player);
            bool broken = damage >= tile.Hardness - 1e-4f;
            // How far it is dug now, so clients can crack it that much: 1 to 99, or 100 when it gives way.
            int dug = broken ? DugThrough : Math.Clamp((int)MathF.Round(damage / tile.Hardness * 100), 1, DugThrough - 1);
            _events.Add(new SimEvent(SimEventType.Dug, player.Id, amount: dug, position: TileMap.Center(x, y)));
            if (!broken)
            {
                _wallDamage[key] = damage;
                return;
            }

            _wallDamage.Remove(key);
            var machine = MachineAt(x, y);
            if (machine != null) Empty(machine, player);
            SetTile(x, y, TileType.Floor);
            // A third of its hardness, at least one: plain rock 1, ore and harder rock more.
            GainLifeExperience(player, LifeSkill.Mining, Math.Max(1, (tile.Hardness + 2) / 3));
            player.Inventory.AddMaterial(tile.Material, tile.Count);
            if (tile.Extra != null && tile.ExtraChance > 0 && _random.Chance(tile.ExtraChance / 100f)) player.Inventory.AddMaterial(tile.Extra);
            player.InventoryVersion++;
        }

        // Puts the material's tile on the floor ahead of the player. False when it cannot be placed there: not a
        // floor (bare or laid), someone or a crop in the way, inside a boss arena, or no such material in the bag.
        // Building over a laid floor gives the floor back. A floor can be laid where a player stands.
        public bool PlaceTile(int playerId, MaterialDefinition material)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || material?.Places == null || player.Inventory.CountOf(material) == 0) return false;
            var (x, y) = TileAhead(player.Position, player.Facing);
            var under = Map[x, y];
            if (Map.IsEdge(x, y) || !TileMap.IsGround(under) || under == material.Places || CropAt(x, y) != null) return false;
            bool paving = TileMap.IsPaving(material.Places.Value);
            if ((!paving && _players.Any(p => !p.IsDead && Touches(p.Position, PlayerRadius, x, y)))
                || _enemies.Any(e => !e.IsDead && Touches(e.Position, e.Definition.Radius, x, y))
                || _bosses.Any(b => Touches(b.Position, b.Definition.Radius, x, y) || Vec2.Distance(TileMap.Center(x, y), b.Home) <= b.ArenaRadius + 1))
            {
                return false;
            }

            player.Inventory.TakeMaterial(material);
            if (TileMap.IsPaving(under) && Tiles?.Find(under) is TileDefinition laid) player.Inventory.AddMaterial(laid.Material, laid.Count);
            player.InventoryVersion++;
            SetTile(x, y, material.Places.Value);
            var machine = MachineAt(x, y);
            if (machine != null) machine.Direction = Machine.DirectionOf(player.Facing);
            if (material.Places == TileType.Bed) ClaimBed(player, x, y);
            return true;
        }

        // Makes the recipe from the player's materials. False when something is missing, its crafting station is not
        // within StationReach, or the bag has no room for an item.
        // Ingredients come from the bag first, then from chests within CraftChestReach, nearest first. times makes it
        // that many times over, or as many as the materials and the bag allow; returns how many were made.
        public bool Craft(int playerId, Recipe recipe) => CraftMany(playerId, recipe, 1) > 0;

        public int CraftMany(int playerId, Recipe recipe, int times)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || recipe == null || MadeByMachine(recipe)) return 0;
            if (recipe.Station != null && !Map.Near(player.Position, recipe.Station.Value, StationReach)) return 0;
            var chests = ChestsNear(player.Position);
            int made = 0;
            for (; made < times; made++)
            {
                // Any quality of a crop will do (MaterialDefinition.Kind), the plainest first.
                if (recipe.Ingredients.Any(i => player.Inventory.CountOfKind(i.Material) + chests.Sum(c => c.Chest.CountOfKind(i.Material)) < i.Count)) break;
                if (recipe.Item != null && (player.Inventory.IsFull || _lootGenerator == null)) break;
                foreach (var (material, count) in recipe.Ingredients)
                {
                    int fromBag = Math.Min(count, player.Inventory.CountOfKind(material));
                    if (fromBag > 0) player.Inventory.TakeKind(material, fromBag);
                    int left = count - fromBag;
                    foreach (var (key, chest) in chests)
                    {
                        int take = Math.Min(left, chest.CountOfKind(material));
                        if (take == 0) continue;
                        chest.TakeKind(material, take);
                        _chestVersions[key] = _chestVersions.GetValueOrDefault(key) + 1;
                        left -= take;
                    }
                }
                if (recipe.Item != null) player.Inventory.TryAdd(Register(_lootGenerator.RollItem(recipe.Item, recipe.Item.Level, Rarity.Common, _random)));
                if (recipe.Consumable != null)
                {
                    // Cooking at a furnace teaches the skill, and the skill may make one more.
                    int bonus = LifeSkills.Bonus(LifeLevel(player, LifeSkill.Cooking), LifeSkills.ExtraDishPercent);
                    int extra = bonus > 0 && _random.Chance(bonus / 100f) ? 1 : 0;
                    player.Inventory.AddConsumable(recipe.Consumable, recipe.Count + extra);
                    if (recipe.Station == TileType.Furnace) GainLifeExperience(player, LifeSkill.Cooking, 2 + recipe.Ingredients.Sum(i => i.Count));
                }
                if (recipe.Material != null) player.Inventory.AddMaterial(recipe.Material, recipe.Count);
            }
            if (made > 0) player.InventoryVersion++;
            return made;
        }

        // Puts back what a world save holds: changed tiles, crops with how far they had grown and the water they had
        // left, and the drop level bosses had raised. Nothing is announced; players who join get it all as usual.
        public void Restore(
            IEnumerable<TileChange> tiles,
            IEnumerable<(CropDefinition Crop, int X, int Y, long Grown, long WetLeft)> crops,
            int progressLevel,
            IEnumerable<(int X, int Y, Inventory Contents)> chests = null,
            IEnumerable<(int X, int Y, string Owner)> beds = null)
        {
            foreach (var tile in tiles)
            {
                if (Map.InBounds(tile.X, tile.Y) && !Map.IsEdge(tile.X, tile.Y)) SetTile(tile.X, tile.Y, tile.Type);
            }
            foreach (var (x, y, contents) in chests ?? Enumerable.Empty<(int, int, Inventory)>()) FillChest(x, y, contents);
            foreach (var (crop, x, y, grown, wetLeft) in crops)
            {
                if (!Map.InBounds(x, y) || !TileMap.IsFarmland(Map[x, y])) continue;
                _crops[y * Map.Width + x] = new Crop(crop, x, y, grown, Tick, Tick + Math.Clamp(wetLeft, 0, WaterTicks));
            }
            foreach (var (x, y, owner) in beds ?? Enumerable.Empty<(int, int, string)>()) RestoreBed(x, y, owner);
            ProgressLevel = Math.Max(ProgressLevel, progressLevel);
            _newTileChanges.Clear();
            _newCropChanges.Clear();
        }

        void SetTile(int x, int y, TileType type)
        {
            if (Map[x, y] == TileType.Chest || type == TileType.Chest) ChestTileChanged(x, y, type);
            if (Map[x, y] == TileType.Bed) BedTileChanged(x, y, type);
            if (Tiles?.Machine(Map[x, y]) != null || Tiles?.Machine(type) != null) MachineTileChanged(x, y, type);
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
