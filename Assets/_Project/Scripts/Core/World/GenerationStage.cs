using System.Collections.Generic;

namespace Game.Core
{
    // One stage of making a world (WorldGenerator.Steps), announced before it runs: its key, for the loading screen's
    // line (the client turns it into words, Docs/TitleRework.md 4-4), and how much of the whole was done before it, 0 to 1.
    // The last stage, Done, carries the world made.
    public readonly struct GenerationStage
    {
        // Version 1: the caves, all at once.
        public const string Caves = "caves";
        // Version 2 on: the frame and the biomes, the rooms and the ways between them, the rooms' floors, the rivers and the
        // ways dug, what stands in the rooms, the ore in the rock, the pools.
        public const string Biomes = "biomes";
        public const string Rooms = "rooms";
        public const string Floors = "floors";
        public const string Ways = "ways";
        public const string Features = "features";
        public const string Ore = "ore";
        public const string Pools = "pools";
        // The places of the story and their roads (with the story sheet).
        public const string Places = "places";
        // The arenas shaped and dressed for their bosses.
        public const string Arenas = "arenas";
        // Version 2 on: what each room is for, the things to find, the minecart lines and waystones, every room reached.
        public const string Furnish = "furnish";
        public const string Pois = "pois";
        public const string Rails = "rails";
        public const string Join = "join";
        // Version 3 on the biomes' props, 4 on the cellars and lairs, 6 on the shallows (7 also the vents and plates).
        public const string Props = "props";
        public const string Cellars = "cellars";
        public const string Water = "water";
        // The dungeons' pocket above the world, on a map wide enough.
        public const string Pocket = "pocket";
        public const string Done = "done";

        public GenerationStage(string key, float fraction, GeneratedWorld world = null)
        {
            Key = key;
            Fraction = fraction;
            World = world;
        }

        public string Key { get; }
        public float Fraction { get; }
        // The world made, on the last stage only.
        public GeneratedWorld World { get; }

        // The stages a world of this version goes through, in order (without Done).
        public static List<string> KeysFor(int version, bool places, bool pocket)
        {
            var keys = new List<string>();
            if (version >= WorldGenerator.RoomVersion) keys.AddRange(new[] { Biomes, Rooms, Floors, Ways, Features, Ore, Pools });
            else keys.Add(Caves);
            if (places) keys.Add(Places);
            keys.Add(Arenas);
            if (version < WorldGenerator.RoomVersion) return keys;
            keys.AddRange(new[] { Furnish, Pois, Rails, Join });
            if (version >= WorldGenerator.PropVersion) keys.Add(Props);
            if (version >= WorldGenerator.LandmarkVersion) keys.Add(Cellars);
            if (version >= WorldGenerator.CrystalLakeVersion) keys.Add(Water);
            if (pocket) keys.Add(Pocket);
            return keys;
        }

        // About how many milliseconds each stage takes on the game's map (1024, generator 7; version 1's caves at 1),
        // measured on a desktop with DevTools/CoreSandbox --loadtime (2026-11-25), so the bar moves at about an even pace.
        public static float WeightOf(string key) => key switch
        {
            Caves => 380,
            Biomes => 54,
            Rooms => 112,
            Floors => 19,
            Ways => 11,
            Features => 27,
            Ore => 48,
            Pools => 23,
            Places => 67,
            Arenas => 1,
            Furnish => 25,
            Pois => 1,
            Rails => 53,
            Join => 30,
            Props => 34,
            Cellars => 2,
            Water => 14,
            Pocket => 1,
            _ => 0,
        };
    }

    // A world being made a stage at a time (WorldGenerator.Steps): Step runs the stage announced last and announces the
    // next, until the world is made. The caller decides how many stages fit in a frame (Client/WorldLoader.cs).
    public sealed class WorldGeneration
    {
        readonly IEnumerator<GenerationStage> _steps;

        public WorldGeneration(IEnumerable<GenerationStage> steps) => _steps = steps.GetEnumerator();

        public WorldGeneration(ulong seed, int width, int height, StoryData story = null, ArenaData arenas = null, int version = WorldGenerator.CaveVersion,
            TerrainData terrain = null) : this(WorldGenerator.Steps(seed, width, height, story, arenas, version, terrain))
        {
        }

        // The stage to run next ("" before the first Step), and how much was done before it, 0 to 1.
        public string Stage { get; private set; } = "";
        public float Fraction { get; private set; }
        public int StagesRun { get; private set; }
        // The world, once made.
        public GeneratedWorld World { get; private set; }
        public bool Done => World != null;

        // False once the world is made.
        public bool Step()
        {
            if (Done) return false;
            if (!_steps.MoveNext()) throw new System.InvalidOperationException("The steps ended without a world.");
            var stage = _steps.Current;
            StagesRun++;
            Stage = stage.Key;
            Fraction = stage.Fraction;
            if (stage.World != null) World = stage.World;
            return !Done;
        }
    }
}
