using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // A tile that can be dug: how many hits it takes and the material it gives.
    public sealed class TileDefinition
    {
        internal TileDefinition(TileType tile, string name, int hardness, MaterialDefinition material, int count)
        {
            Tile = tile;
            Name = name;
            Hardness = hardness;
            Material = material;
            Count = count;
        }

        public TileType Tile { get; }
        public string Name { get; }
        // Hits at no mining speed.
        public int Hardness { get; }
        public MaterialDefinition Material { get; }
        public int Count { get; }
    }

    public sealed class TileData
    {
        internal TileData(IReadOnlyList<TileDefinition> tiles, IReadOnlyList<string> errors)
        {
            Tiles = tiles;
            Errors = errors;
        }

        public IReadOnlyList<TileDefinition> Tiles { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool Success => Errors.Count == 0;

        // Null for a tile nobody can dig.
        public TileDefinition Find(TileType tile) => Tiles.FirstOrDefault(t => t.Tile == tile);
    }

    // Reads Assets/_Project/Data/World/tiles.csv. Columns are explained in Docs/Crafting.md.
    public static class TileSheet
    {
        public const string File = "tiles.csv";

        public static TileData Parse(string csv, LootData loot)
        {
            var errors = new List<string>();
            var tiles = new List<TileDefinition>();
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns("tile", "name", "hardness", "material", "count");
            if (missing.Count > 0)
            {
                errors.Add($"{File}: 열이 없습니다: {string.Join(", ", missing)}");
                return new TileData(tiles, errors);
            }

            foreach (var row in table.Rows)
            {
                try
                {
                    var tile = SheetValues.ParseEnum<TileType>(row["tile"], "칸 종류");
                    if (tile == TileType.Floor) throw new FormatException("바닥은 캘 수 없습니다.");
                    if (tiles.Any(t => t.Tile == tile)) throw new FormatException($"'{tile}'가 이미 있습니다.");
                    string name = row["name"];
                    if (name.Length == 0) throw new FormatException("'name' 칸이 비어 있습니다.");
                    int hardness = SheetValues.ParseNumber(row["hardness"], "hardness");
                    if (hardness < 1) throw new FormatException("hardness는 1 이상이어야 합니다.");
                    var material = loot?.Materials.FirstOrDefault(m => m.Id == row["material"])
                        ?? throw new FormatException($"재료 '{row["material"]}'가 {LootSheets.MaterialsFile}에 없습니다.");
                    int count = SheetValues.ParseNumber(row["count"], "count");
                    if (count < 1) throw new FormatException("count는 1 이상이어야 합니다.");
                    tiles.Add(new TileDefinition(tile, name, hardness, material, count));
                }
                catch (FormatException e)
                {
                    errors.Add($"{File} {row.Line}행: {e.Message}");
                }
            }
            return new TileData(tiles, errors);
        }
    }
}
