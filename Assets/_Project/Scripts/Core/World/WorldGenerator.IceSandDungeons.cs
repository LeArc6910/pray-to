namespace Game.Core
{
    public static partial class WorldGenerator
    {
        // The generator's fifth version (2026-11-24, Docs/BiomeRework.md 묶음 B2): version 4 with the lairs of the ice
        // cave's and the sand cave's mid-bosses (the frost mammoth, the temple guardian) beside those of the dirt cave
        // and the mushroom forest (WorldGenerator.Cellars.cs). A world made at version 4 or before keeps its lairs and
        // its map as made (their hashes are pinned in the tests). IceSandVersion itself is in WorldGenerator.IceSand.cs,
        // with the ice cave's and the sand cave's props and landmarks laid in the same version.

        // The biomes a world of this version lays lairs in, of those whose dungeon has a mid-boss in person for master:
        // version 4 only the two that had one when it was made (the dirt cave, the mushroom forest).
        static bool LairsIn(Biome biome, int version) => version >= IceSandVersion || biome == Biome.Cave || biome == Biome.Mushroom;

        // How many of the zones' rings (World.ZoneRing at the map's scale) a biome's lairs keep from the spawn: two for the
        // ice cave and the sand cave (their mid-bosses are of the third zone, and the sand cave holds the first boss's
        // arena in some worlds: the first hour's bot met a temple guardian there), one for the rest.
        static int LairRingsOf(Biome biome) => biome == Biome.Ice || biome == Biome.Sand ? 2 : 1;
    }
}
