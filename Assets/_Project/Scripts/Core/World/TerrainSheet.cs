using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // How a biome's rooms are shaped (terrain.csv style, Docs/WorldRework.md 1단계): what the generator's version 2
    // carves there (WorldGenerator.Rooms.cs).
    public enum TerrainStyle : byte
    {
        // Round rooms with rough edges and winding ways between: the plain cave, where the game is learnt.
        Cave,
        // A tight maze: small square rooms, several to a cell, narrow ways and many forks.
        Maze,
        // Wide frozen halls and long straight galleries.
        Hall,
        // Narrow straight cracks and a few great hollows.
        Fissure,
        // Open dunes: rooms so wide that neighbours run together.
        Dunes,
        // Halls cut by rivers of lava, crossed where the ways bridge them.
        Rivers,
        // Islands in water, joined by causeways.
        Islands,
        // Broken slabs of ground joined by thin straight bridges.
        Shards,
    }

    // One biome's terrain (a row of terrain.csv). Sizes are for the game's map and shrink on smaller ones.
    public sealed class TerrainRule
    {
        internal TerrainRule(Biome biome, TerrainStyle style, int roomFill, int rooms, int roomMin, int roomMax, int corridorMin, int corridorMax,
            int loopPercent, int deadEndPercent, int wallTarget, int liquidTarget, TileType? feature, int featurePercent,
            TileType? ground = null, int groundPercent = 0, int chunkPercent = 0)
        {
            Ground = ground;
            GroundPercent = groundPercent;
            ChunkPercent = chunkPercent;
            Biome = biome;
            Style = style;
            RoomFill = roomFill;
            Rooms = rooms;
            RoomMin = roomMin;
            RoomMax = roomMax;
            CorridorMin = corridorMin;
            CorridorMax = corridorMax;
            LoopPercent = loopPercent;
            DeadEndPercent = deadEndPercent;
            WallTarget = wallTarget;
            LiquidTarget = liquidTarget;
            Feature = feature;
            FeaturePercent = featurePercent;
        }

        public Biome Biome { get; }
        public TerrainStyle Style { get; }
        // Out of a hundred cells (or parts of a cell), how many have a room.
        public int RoomFill { get; }
        // Rooms to a cell: 1, 2 or 4.
        public int Rooms { get; }
        // A room's width in tiles, from and to.
        public int RoomMin { get; }
        public int RoomMax { get; }
        // A way's width in tiles, from and to.
        public int CorridorMin { get; }
        public int CorridorMax { get; }
        // Of the ways the spanning tree left out, how many in a hundred are opened again, each closing a loop.
        public int LoopPercent { get; }
        // Of the rooms with one way in, how many in a hundred stay dead ends; the rest get a second way.
        public int DeadEndPercent { get; }
        // What the terrain is tuned to: the share of the biome's tiles that are rock, and that are lava or water.
        // The generator does not read them; --mapstats and the tests hold the map to them.
        public int WallTarget { get; }
        public int LiquidTarget { get; }
        // Something solid standing in the rooms (a pillar of rock, a mushroom tree), and how many in a hundred of a
        // room's tiles it takes; null for none.
        public TileType? Feature { get; }
        public int FeaturePercent { get; }
        // A floor laid in patches over the rooms' own (ice, quicksand), and how many in a hundred of a room's tiles it
        // takes; null for none.
        public TileType? Ground { get; }
        public int GroundPercent { get; }
        // Of the pack rooms, how many in a hundred are set with a chunk from rooms.csv; the other roles always are,
        // where one fits.
        public int ChunkPercent { get; }
    }

    public sealed class TerrainData
    {
        readonly Dictionary<Biome, TerrainRule> _byBiome = new Dictionary<Biome, TerrainRule>();

        internal TerrainData(IEnumerable<TerrainRule> rules, IReadOnlyList<string> errors, RoomData rooms = null, PoiData pois = null, DungeonData dungeons = null)
        {
            foreach (var rule in rules) _byBiome[rule.Biome] = rule;
            Rooms = rooms ?? RoomData.None;
            Pois = pois ?? PoiData.None;
            Dungeons = dungeons ?? DungeonData.None;
            var all = errors.ToList();
            if (rooms != null) all.AddRange(rooms.Errors.Where(e => !all.Contains(e)));
            if (pois != null) all.AddRange(pois.Errors.Where(e => !all.Contains(e)));
            if (dungeons != null) all.AddRange(dungeons.Errors.Where(e => !all.Contains(e)));
            Errors = all;
        }

        // The instance dungeons and their graphs (dungeons.csv, dungeon_graphs.csv, Docs/WorldRework.md 5단계, 묶음 5b): none
        // when the game did not read them, and then a gate leads to the plain dungeon of 묶음 5a.
        public DungeonData Dungeons { get; }

        // The same rules with the dungeons beside them.
        internal TerrainData With(DungeonData dungeons) => new TerrainData(_byBiome.Values, Errors, Rooms, Pois, dungeons);

        // The things to find in the rooms (pois.csv, Docs/WorldRework.md 3단계): none when the game did not read them,
        // and then the rooms keep their chests shut and their fires only warm.
        public PoiData Pois { get; }

        // The room chunks and the roles' numbers (rooms.csv, room_roles.csv); none when the game did not read them, and
        // then the rooms get their roles by the code's numbers and no chunks.
        public RoomData Rooms { get; }

        // The same rules with the room sheets beside them.
        internal TerrainData With(RoomData rooms) => new TerrainData(_byBiome.Values, Errors, rooms, Pois, Dungeons);

        // The same rules with the things to find beside them.
        internal TerrainData With(PoiData pois) => new TerrainData(_byBiome.Values, Errors, Rooms, pois, Dungeons);

        // The same without the things to find (a game whose asset was made before pois.csv).
        internal TerrainData WithoutPois() => new TerrainData(_byBiome.Values, Errors, Rooms, PoiData.None, Dungeons);

        public IReadOnlyList<string> Errors { get; }
        public bool Success => Errors.Count == 0;
        public IEnumerable<TerrainRule> Rules => _byBiome.Values;

        // A biome's rule; one the sheet left out is as the code has it (TerrainSheet.Default).
        public TerrainRule Of(Biome biome) => _byBiome.TryGetValue(biome, out var rule) ? rule : TerrainSheet.Default._byBiome[biome];
    }

    // Reads Assets/_Project/Data/World/terrain.csv (Docs/Biomes.md 지형).
    public static class TerrainSheet
    {
        public const string File = "terrain.csv";

        // The sheet as shipped, for a world whose game did not read it (tests, sims, an asset made before the sheet
        // existed). TerrainTests holds the two together: the host and a client must carve the same rooms.
        public static readonly TerrainData Default = new TerrainData(new[]
        {
            new TerrainRule(Biome.Cave, TerrainStyle.Cave, 85, 1, 18, 26, 3, 3, 30, 15, 55, 0, null, 0, null, 0, DefaultChunkPercent),
            new TerrainRule(Biome.Mushroom, TerrainStyle.Maze, 80, 4, 7, 12, 2, 2, 45, 25, 60, 0, TileType.MushroomTree, 2, null, 0, DefaultChunkPercent),
            new TerrainRule(Biome.Ice, TerrainStyle.Hall, 80, 1, 22, 30, 4, 5, 20, 10, 45, 0, null, 0, TileType.IceFloor, 30, DefaultChunkPercent),
            new TerrainRule(Biome.Crystal, TerrainStyle.Fissure, 90, 1, 26, 34, 2, 2, 25, 20, 65, 0, null, 0, null, 0, DefaultChunkPercent),
            new TerrainRule(Biome.Lava, TerrainStyle.Rivers, 85, 1, 20, 28, 3, 3, 30, 15, 48, 8, null, 0, null, 0, DefaultChunkPercent),
            new TerrainRule(Biome.Abyss, TerrainStyle.Shards, 88, 1, 18, 28, 2, 2, 20, 25, 68, 0, null, 0, null, 0, DefaultChunkPercent),
            new TerrainRule(Biome.Lake, TerrainStyle.Islands, 92, 1, 16, 24, 2, 2, 35, 15, 35, 28, null, 0, null, 0, DefaultChunkPercent),
            new TerrainRule(Biome.Sand, TerrainStyle.Dunes, 92, 1, 26, 34, 6, 6, 60, 5, 35, 0, TileType.Wall, 2, TileType.Quicksand, 8, DefaultChunkPercent),
        }, Array.Empty<string>());

        public const int DefaultChunkPercent = 35;

        public static TerrainData Parse(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return Default;
            var errors = new List<string>();
            var rules = new List<TerrainRule>();
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns("biome", "style", "room_fill", "rooms", "room_min", "room_max", "corridor_min", "corridor_max",
                "loop_percent", "deadend_percent", "wall_target");
            if (missing.Count > 0)
            {
                errors.Add($"{File}: 열이 없습니다: {string.Join(", ", missing)}");
                return new TerrainData(rules, errors);
            }
            foreach (var row in table.Rows)
            {
                try
                {
                    var biome = SheetValues.ParseEnum<Biome>(row["biome"], "biome");
                    if (rules.Any(r => r.Biome == biome)) throw new FormatException($"'{row["biome"]}'가 두 번 있습니다.");
                    int Number(string column, int min, int max)
                    {
                        int value = row[column].Length == 0 ? 0 : SheetValues.ParseNumber(row[column], column);
                        if (value < min || value > max) throw new FormatException($"{column}는 {min}~{max}입니다.");
                        return value;
                    }
                    int rooms = Number("rooms", 1, 4);
                    if (rooms == 3) throw new FormatException("rooms는 1, 2, 4 가운데 하나입니다.");
                    int roomMin = Number("room_min", 4, 40), roomMax = Number("room_max", 4, 40);
                    if (roomMax < roomMin) throw new FormatException("room_max는 room_min 이상이어야 합니다.");
                    int corridorMin = Number("corridor_min", 1, 8), corridorMax = Number("corridor_max", 1, 8);
                    if (corridorMax < corridorMin) throw new FormatException("corridor_max는 corridor_min 이상이어야 합니다.");
                    TileType? feature = row["feature"].Length == 0 ? (TileType?)null : SheetValues.ParseEnum<TileType>(row["feature"], "feature");
                    // Columns added with the rooms' roles (2026-11-21); a sheet without them has no patches and the usual share of chunks.
                    bool patches = table.Header.Contains("ground");
                    TileType? ground = !patches || row["ground"].Length == 0 ? (TileType?)null : SheetValues.ParseEnum<TileType>(row["ground"], "ground");
                    if (ground is TileType laid && laid != TileType.IceFloor && laid != TileType.Quicksand) throw new FormatException("ground는 IceFloor나 Quicksand입니다.");
                    rules.Add(new TerrainRule(biome, SheetValues.ParseEnum<TerrainStyle>(row["style"], "style"), Number("room_fill", 1, 100), rooms, roomMin, roomMax,
                        corridorMin, corridorMax, Number("loop_percent", 0, 100), Number("deadend_percent", 0, 100), Number("wall_target", 0, 100),
                        Number("liquid_target", 0, 100), feature, Number("feature_percent", 0, 20),
                        ground, patches ? Number("ground_percent", 0, 60) : 0, table.Header.Contains("chunk_percent") ? Number("chunk_percent", 0, 100) : DefaultChunkPercent));
                }
                catch (FormatException e)
                {
                    errors.Add($"{File} {row.Line}행: {e.Message}");
                }
            }
            return new TerrainData(rules, errors);
        }
    }
}
