using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // Things to pick up off the cave floor (2026-11-09, Docs/Biomes.md 채집물): each new day every biome gets up to
    // ForagePerBiome of its own lying about on open floor, picked up by walking over them like any drop, until the next
    // day's take their place. Biomes are code (Docs/Biomes.md), so is what each gives.
    public sealed partial class World
    {
        public const int ForagePerBiome = 4;
        // Not at the spawn, where the first base goes.
        const float ForageAwayFromSpawn = 8;
        const int ForageTries = 80;

        public static readonly IReadOnlyDictionary<Biome, string> ForageOf = new Dictionary<Biome, string>
        {
            [Biome.Cave] = "mushroom",
            [Biome.Mushroom] = "glow_spore",
            [Biome.Ice] = "ice_crystal",
            [Biome.Crystal] = "mithril_ore",
            [Biome.Lava] = "fire_pepper",
            [Biome.Abyss] = "void_bean",
            [Biome.Lake] = "pearl",
            [Biome.Sand] = "amber",
        };

        // The forage lying about now, by its ground loot id, and the day it was laid for.
        readonly Dictionary<int, Biome> _forage = new Dictionary<int, Biome>();
        int _forageDay = -1;
        // The world's seed: each day's forage has its own random, so laying it never shifts the world's other rolls.
        readonly ulong _forageSeed;

        public IReadOnlyCollection<int> ForageIds => _forage.Keys;

        void UpdateForage()
        {
            int today = Calendar.Day(Clock);
            if (today == _forageDay || Loot == null || !_config.Forage) return;
            _forageDay = today;
            // Yesterday's left lying goes: today's is fresh.
            _groundLoot.RemoveAll(l => _forage.ContainsKey(l.Id));
            _forage.Clear();
            var random = new GameRandom(_forageSeed ^ (ulong)today * 0x9E3779B97F4A7C15UL, stream: 7);
            foreach (var pair in ForageOf)
            {
                var material = Loot.Materials.FirstOrDefault(m => m.Id == pair.Value);
                if (material == null) continue;
                for (int placed = 0, tries = 0; placed < ForagePerBiome && tries < ForageTries; tries++)
                {
                    int x = random.Next(Map.Width), y = random.Next(Map.Height);
                    if (Map[x, y] != TileType.Floor || Biomes[x, y] != pair.Key || CropAt(x, y) != null) continue;
                    var at = TileMap.Center(x, y);
                    if (Vec2.Distance(at, Spawn) < ForageAwayFromSpawn || _bosses.Any(b => Vec2.Distance(at, b.Home) <= b.ArenaRadius + 1)) continue;
                    var loot = Place(null, null, at, Tick, material: material, expireTick: long.MaxValue);
                    _forage[loot.Id] = pair.Key;
                    placed++;
                }
            }
        }
    }
}
