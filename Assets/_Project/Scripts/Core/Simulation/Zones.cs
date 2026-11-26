using System;

namespace Game.Core
{
    // The level a spot is for (2026-11-27, Docs/ZoneLevels.md, NextTasks 3b): its biome's band (DamageBands.LevelOf, the
    // level its monsters are weighed at) and its zone (World.ZoneLevelAt: a zone more every ZoneRing tiles from the
    // spawn, each past the first +50% health and +35% hits), two levels a zone. What the screen shows by the place's
    // name, in its banner and on the map, so a player sees where they are ahead of themselves.
    public static class Zones
    {
        // Levels a zone past the first adds, and the range either side of the middle.
        public const int LevelsPerZone = 2;
        public const int Spread = 2;
        // Below the range by more than this: far too deep (red); below it at all: deep (orange).
        public const int FarBelow = 4;

        // The zone at a spot as World.ZoneLevelAt works it out with the game's WorldConfig, out of a dungeon.
        public static int ZoneAt(Vec2 spawn, Vec2 at, int mapScale, int zoneLevel = 1, float ring = 30) =>
            zoneLevel + (ring > 0 ? (int)(Vec2.Distance(at, spawn) / (ring * Math.Max(1, mapScale))) : 0);

        public static (int Low, int High) LevelRange(Biome biome, int zone)
        {
            int middle = DamageBands.LevelOf(DamageBands.BandOf(biome)) + LevelsPerZone * Math.Max(0, zone - 1);
            return (Math.Max(1, middle - Spread), middle + Spread);
        }

        // A dungeon's: its own level, the same spread.
        public static (int Low, int High) LevelRange(int level) => (Math.Max(1, level - Spread), level + Spread);

        // 0: the player is up to it; 1: below it (orange); 2: far below (red).
        public static int Danger(int playerLevel, (int Low, int High) range) =>
            playerLevel >= range.Low ? 0 : playerLevel >= range.Low - FarBelow ? 1 : 2;

        public static string Text((int Low, int High) range) => $"Lv {range.Low}~{range.High}";
    }
}
