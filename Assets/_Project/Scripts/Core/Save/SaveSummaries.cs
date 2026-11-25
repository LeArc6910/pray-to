using System;
using System.Collections.Generic;

namespace Game.Core
{
    // A character as its list shows it (CharacterSave.ReadSummary, Docs/TitleRework.md 4-2), read without the sheets.
    public sealed class CharacterSummary
    {
        // The most monster kinds kept for the loading screen's scene.
        public const int MaxMonsters = 12;

        public CharacterSummary(string name, int version, CharacterLook look, int experience, IReadOnlyList<string> classes, double playedSeconds,
            long createdAt, long lastPlayedAt, int visited, IReadOnlyList<string> monsters, IReadOnlyList<string> bosses, int journeyStep)
        {
            Name = name ?? "";
            Version = version;
            Look = look;
            Experience = experience;
            Classes = classes ?? Array.Empty<string>();
            PlayedSeconds = playedSeconds;
            CreatedAt = createdAt;
            LastPlayedAt = lastPlayedAt;
            Visited = visited;
            Monsters = monsters ?? Array.Empty<string>();
            Bosses = bosses ?? Array.Empty<string>();
            JourneyStep = journeyStep;
        }

        // The save's name (SaveKeys.Character), as the caller gave it.
        public string Name { get; }
        // The format it was written in (CharacterSave.FormatVersion or before).
        public int Version { get; }
        public CharacterLook Look { get; }
        public int Experience { get; }
        public int Level => Leveling.LevelFor(Experience);
        // Its classes by id, in the order picked; empty for a save from before format 16 or a character with none yet.
        public IReadOnlyList<string> Classes { get; }
        // 0 when unknown (a save from before format 20).
        public double PlayedSeconds { get; }
        // Seconds since 1970 as the caller counted them; 0 when unknown.
        public long CreatedAt { get; }
        public long LastPlayedAt { get; }
        // The biomes it has stood in (BiomeVisits).
        public int Visited { get; }
        public IReadOnlyList<Biome> VisitedBiomes => BiomeVisits.In(Visited);
        // The monster kinds it has felled (monsters.csv ids), the most felled first, at most MaxMonsters; and the bosses
        // beaten (bosses.csv ids). For the loading screen's scene: only what it has met.
        public IReadOnlyList<string> Monsters { get; }
        public IReadOnlyList<string> Bosses { get; }
        // Where it is in the main story (Player.JourneyStep).
        public int JourneyStep { get; }
    }

    // A world as its list shows it (WorldSave.ReadSummary): the save's head only.
    public sealed class WorldSummary
    {
        public WorldSummary(string name, int version, ulong seed, int width, int height, int generator, long clock, long createdAt, double playedSeconds,
            long lastPlayedAt, int visited, int progressLevel)
        {
            Name = name ?? "";
            Version = version;
            Seed = seed;
            Width = width;
            Height = height;
            Generator = generator;
            Clock = clock;
            CreatedAt = createdAt;
            PlayedSeconds = playedSeconds;
            LastPlayedAt = lastPlayedAt;
            Visited = visited;
            ProgressLevel = progressLevel;
        }

        public string Name { get; }
        public int Version { get; }
        public ulong Seed { get; }
        public int Width { get; }
        public int Height { get; }
        // Which generator made its map (WorldGenerator.CaveVersion ... NewWorldVersion).
        public int Generator { get; }
        // The world's clock (Calendar) and the day it is on, from 1.
        public long Clock { get; }
        public int Day => Calendar.Day(Clock);
        // Seconds since 1970 as the caller counted them; 0 when unknown (CreatedAt before format 17).
        public long CreatedAt { get; }
        public double PlayedSeconds { get; }
        // When it was last saved, which is when it was last played (WorldSave.SavedAt).
        public long LastPlayedAt { get; }
        // The biomes its players have stood in, and the deepest of them (BiomeVisits.Order); null before format 17.
        public int Visited { get; }
        public Biome? Deepest => BiomeVisits.Deepest(Visited);
        // The drop level the bosses beaten have raised it to (World.ProgressLevel).
        public int ProgressLevel { get; }
    }
}
