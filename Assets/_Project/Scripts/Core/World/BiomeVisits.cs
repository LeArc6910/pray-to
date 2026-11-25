using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // The biomes a character, or a world's players, have stood in, as a bit for each Biome (2026-11-25, Docs/TitleRework.md
    // 4-2·4-4: the loading screen and the title's back show only the biomes been to; the world list shows the deepest).
    public static class BiomeVisits
    {
        // The biomes from the first to the last, as the levels go (the dungeons' levels: the dirt cave 5, the mushroom
        // forest 8, ice and sand 10, crystal 16, the lake 20, the lava 25, the abyss 32).
        public static readonly IReadOnlyList<Biome> Order = new[]
        {
            Biome.Cave, Biome.Mushroom, Biome.Ice, Biome.Sand, Biome.Crystal, Biome.Lake, Biome.Lava, Biome.Abyss,
        };

        public static int Bit(Biome biome) => 1 << (int)biome;

        public static int Mark(int visited, Biome biome) => visited | Bit(biome);

        public static bool Has(int visited, Biome biome) => (visited & Bit(biome)) != 0;

        // The biomes in the set, first to last.
        public static List<Biome> In(int visited) => Order.Where(b => Has(visited, b)).ToList();

        // The last of them along Order, or null for none.
        public static Biome? Deepest(int visited)
        {
            for (int i = Order.Count - 1; i >= 0; i--)
                if (Has(visited, Order[i])) return Order[i];
            return null;
        }
    }
}
