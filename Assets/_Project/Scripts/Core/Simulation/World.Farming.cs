using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // A crop growing on a floor tile since PlantedTick. Host and clients work out how far along it is from the tick,
    // so only planting, watering and harvesting are ever sent. Watering moves PlantedTick back by half the time it
    // still had to grow.
    public sealed class Crop
    {
        public Crop(CropDefinition definition, int x, int y, long plantedTick, bool watered = false)
        {
            Definition = definition;
            X = x;
            Y = y;
            PlantedTick = plantedTick;
            Watered = watered;
        }

        public CropDefinition Definition { get; }
        public int X { get; }
        public int Y { get; }
        public long PlantedTick { get; }
        // A crop takes water once.
        public bool Watered { get; }

        // 0 when planted, 1 when ripe.
        public float Growth(double tick) => Math.Clamp((float)((tick - PlantedTick) / Definition.GrowTicks), 0, 1);

        public bool IsRipe(double tick) => tick - PlantedTick >= Definition.GrowTicks;
    }

    // A crop planted or watered (Crop set) or harvested (Crop null) at a tile.
    public readonly struct CropChange
    {
        public CropChange(int x, int y, Crop crop)
        {
            X = x;
            Y = y;
            Crop = crop;
        }

        public int X { get; }
        public int Y { get; }
        public Crop Crop { get; }
    }

    // Planting seeds on the floor ahead, watering them and harvesting what is ripe by walking over it. See
    // Docs/Farming.md.
    public sealed partial class World
    {
        // The material that waters crops. It is a tool: watering does not use it up.
        public const string WateringCan = "watering_can";

        readonly Dictionary<int, Crop> _crops = new Dictionary<int, Crop>();
        readonly List<CropChange> _newCropChanges = new List<CropChange>();

        public IReadOnlyCollection<Crop> Crops => _crops.Values;

        public Crop CropAt(int x, int y) => _crops.TryGetValue(y * Map.Width + x, out var crop) ? crop : null;

        // Crops planted or harvested since the last call.
        public List<CropChange> TakeCropChanges()
        {
            var changes = new List<CropChange>(_newCropChanges);
            _newCropChanges.Clear();
            return changes;
        }

        // Plants one of the crop's seeds on the floor ahead of the player. False when it cannot: no seed, not a
        // floor, something already growing there, or inside a boss arena. Crops do not block, so anyone may stand
        // on the tile.
        public bool Plant(int playerId, CropDefinition crop)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || crop == null || player.Inventory.CountOf(crop.Seed) == 0) return false;
            var ahead = player.Position + player.Facing * BuildReach;
            int x = TileMap.Cell(ahead.X);
            int y = TileMap.Cell(ahead.Y);
            if (Map.IsEdge(x, y) || Map[x, y] != TileType.Floor || CropAt(x, y) != null) return false;
            if (_bosses.Any(b => Vec2.Distance(TileMap.Center(x, y), b.Home) <= b.ArenaRadius + 1)) return false;

            player.Inventory.TakeMaterial(crop.Seed);
            player.InventoryVersion++;
            var planted = new Crop(crop, x, y, Tick);
            _crops[y * Map.Width + x] = planted;
            _newCropChanges.Add(new CropChange(x, y, planted));
            return true;
        }

        // Waters the crops on the tile ahead of the player and the eight around it, with a watering can in the bag.
        // Each crop not yet watered or ripe has the time it still needs halved. False when none could be watered.
        public bool Water(int playerId)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || !player.Inventory.Materials.Keys.Any(m => m.Id == WateringCan)) return false;
            var ahead = player.Position + player.Facing * BuildReach;
            int cx = TileMap.Cell(ahead.X);
            int cy = TileMap.Cell(ahead.Y);
            int watered = 0;
            for (int y = cy - 1; y <= cy + 1; y++)
            {
                for (int x = cx - 1; x <= cx + 1; x++)
                {
                    var crop = Map.InBounds(x, y) ? CropAt(x, y) : null;
                    if (crop == null || crop.Watered || crop.IsRipe(Tick)) continue;
                    long left = crop.Definition.GrowTicks - (Tick - crop.PlantedTick);
                    var wet = new Crop(crop.Definition, x, y, crop.PlantedTick - left / 2, watered: true);
                    _crops[y * Map.Width + x] = wet;
                    _newCropChanges.Add(new CropChange(x, y, wet));
                    watered++;
                }
            }
            if (watered == 0) return false;
            _events.Add(new SimEvent(SimEventType.Watered, player.Id, amount: watered, position: TileMap.Center(cx, cy)));
            return true;
        }

        // Anyone standing on a ripe crop harvests it.
        void UpdateCrops()
        {
            if (_crops.Count == 0) return;
            foreach (var player in _players)
            {
                if (player.IsDead) continue;
                var crop = CropAt(TileMap.Cell(player.Position.X), TileMap.Cell(player.Position.Y));
                if (crop != null && crop.IsRipe(Tick)) Harvest(player, crop);
            }
        }

        void Harvest(Player player, Crop crop)
        {
            _crops.Remove(crop.Y * Map.Width + crop.X);
            _newCropChanges.Add(new CropChange(crop.X, crop.Y, null));

            var definition = crop.Definition;
            if (definition.HarvestMaterial != null) player.Inventory.AddMaterial(definition.HarvestMaterial, definition.Count);
            else player.Inventory.AddConsumable(definition.HarvestConsumable, definition.Count);
            int seeds = definition.Seeds;
            if (definition.SeedChance > 0 && _random.Chance(definition.SeedChance / 100f)) seeds++;
            player.Inventory.AddMaterial(definition.Seed, seeds);
            player.InventoryVersion++;
            _events.Add(new SimEvent(SimEventType.Harvested, player.Id, amount: definition.Count, position: TileMap.Center(crop.X, crop.Y)));
        }
    }
}
