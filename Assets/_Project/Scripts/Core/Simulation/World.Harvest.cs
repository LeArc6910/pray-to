using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // The harvest props growing back (2026-11-27, Docs/Harvest.md): one picked or broken (World.Dig) is its spent form,
    // walked over, until the next dawn brings it back (with nobody standing on it). Which are spent is the map itself,
    // saved and sent like any changed tile; after a load they are found on the map and count as spent before today.
    public sealed partial class World
    {
        // A world of version 9 and after has harvest props in place of the floor's forage.
        bool Harvests => _rooms != null && _rooms.Version >= WorldGenerator.HarvestVersion;

        // The spent ones by tile (y * width + x), and the day each was spent.
        readonly Dictionary<int, int> _spent = new Dictionary<int, int>();
        bool _spentFound;

        public int SpentHarvests => _spent.Count;

        void Spend(int x, int y, TileType grown)
        {
            SetTile(x, y, HarvestProps.SpentOf(grown));
            _spent[y * Map.Width + x] = Calendar.Day(Clock);
        }

        // Once a second: those spent before today grow back where nothing stands on them.
        void UpdateHarvest()
        {
            if (Tick % TicksPerSecond != 0 || !Harvests) return;
            if (!_spentFound)
            {
                _spentFound = true;
                for (int y = 0; y < Map.Height; y++)
                    for (int x = 0; x < Map.Width; x++)
                        if (HarvestProps.IsSpent(Map[x, y])) _spent.TryAdd(y * Map.Width + x, -1);
            }
            if (_spent.Count == 0) return;
            int today = Calendar.Day(Clock);
            foreach (var (key, day) in _spent.ToList())
            {
                if (day >= today) continue;
                int x = key % Map.Width, y = key / Map.Width;
                if (!HarvestProps.IsSpent(Map[x, y]))
                {
                    _spent.Remove(key);
                    continue;
                }
                if (_players.Any(p => !p.IsDead && Touches(p.Position, PlayerRadius, x, y)) || _enemies.Any(e => !e.IsDead && Touches(e.Position, e.Definition.Radius, x, y)))
                    continue;
                SetTile(x, y, HarvestProps.GrownOf(Map[x, y]));
                _spent.Remove(key);
            }
        }
    }
}
