using System;
using System.Linq;
using System.Text;
using Game.Core;

namespace CoreSandbox
{
    // --town [seed] (Docs/Towns.md): the dawn village and the royal city of the new world as text, one letter a tile, and
    // what stands in each: to see the grown towns' layout without the art.
    static class TownMap
    {
        public static void Run(BotSheets sheets, ulong seed)
        {
            var world = WorldGenerator.Generate(seed, WorldGenerator.GameSize, WorldGenerator.GameSize, sheets.Story, sheets.Bosses?.Arenas, WorldGenerator.NewWorldVersion, sheets.Tiles.Terrain);
            foreach (var site in world.Places.Where(p => p.Definition.Style == "village" || p.Definition.Style == "kingdom"))
            {
                int r = site.Radius + 2;
                Console.WriteLine($"== {site.Definition.Name} (반지름 {site.Radius}, 생성기 {world.Version}판)");
                var counts = world.Map.Width > 0 ? new System.Collections.Generic.Dictionary<TileType, int>() : null;
                for (int y = site.Y + r; y >= site.Y - r; y--)
                {
                    var line = new StringBuilder();
                    for (int x = site.X - r; x <= site.X + r; x++)
                    {
                        var t = world.Map.InBounds(x, y) ? world.Map[x, y] : TileType.Wall;
                        if (t != TileType.StructurePart) counts[t] = counts.TryGetValue(t, out int c) ? c + 1 : 1;
                        line.Append(Letter(t));
                    }
                    Console.WriteLine(line.ToString());
                }
                Console.WriteLine(string.Join(", ", counts.Where(c => Structures.IsDecor(c.Key) || c.Key == TileType.Fence || c.Key == TileType.Scarecrow).OrderByDescending(c => c.Value).Select(c => $"{c.Key} {c.Value}")));
                Console.WriteLine($"사람: {string.Join(", ", site.People.Select(p => p.Character.Name))}");
                Console.WriteLine();
            }
        }

        static char Letter(TileType t) => t switch
        {
            TileType.Wall => '#',
            TileType.Floor or TileType.BrickFloor or TileType.StoneFloor => '.',
            TileType.Road => ',',
            TileType.Water => '~',
            TileType.Dock => '=',
            TileType.RedCarpet => 'r',
            TileType.CastleWall => 'W',
            TileType.TimberWall => 'w',
            TileType.Tower => 'T',
            TileType.Palace => 'P',
            TileType.Inn => 'I',
            TileType.Smithy => 'S',
            TileType.Shop => 'H',
            TileType.Temple => 'M',
            TileType.Barracks => 'B',
            TileType.Stable => 'L',
            TileType.MarketStall => 'm',
            TileType.Cottage or TileType.Cabin => 'h',
            TileType.StructurePart => '+',
            TileType.Soil => '_',
            TileType.Fence => 'f',
            TileType.Waypoint => '@',
            _ => Structures.IsDecor(t) ? '*' : '?',
        };
    }
}
