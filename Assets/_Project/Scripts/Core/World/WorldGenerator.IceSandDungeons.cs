namespace Game.Core
{
    public static partial class WorldGenerator
    {
        // The generator's fifth version (2026-11-24, Docs/BiomeRework.md 묶음 B2): version 4 with the lairs of the ice
        // cave's and the sand cave's mid-bosses (the frost mammoth, the temple guardian) beside those of the dirt cave
        // and the mushroom forest (WorldGenerator.Cellars.cs). A world made at version 4 or before keeps its lairs and
        // its map as made (their hashes are pinned in the tests). IceSandVersion itself is in WorldGenerator.IceSand.cs,
        // with the ice cave's and the sand cave's props and landmarks laid in the same version. Which biomes a version
        // lays lairs in, and how far out (LairsIn, LairRingsOf), are with version 6's in WorldGenerator.CrystalLakeDungeons.cs.
    }
}
