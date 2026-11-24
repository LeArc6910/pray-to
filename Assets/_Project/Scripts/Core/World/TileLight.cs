using System;
using System.Collections.Generic;

namespace Game.Core
{
    // Which tiles give light and how far (묶음 B1a, Docs/BiomeRework.md 흙 동굴의 등잔): the tiles of the sheet that
    // burn (tiles.csv light: a torch, a lit lamp, a clump of glowing caps, the machines with a fire in them) and the
    // fires the generator dresses places and camps with, which are in no sheet. The screen lights the dark with the
    // same radii (Client WorldLights); the rules ask here whether a spot is in something's light.
    public static class TileLight
    {
        // The dressing that burns, in tiles: a campfire, a lamp post, a brazier, a lantern, a lighthouse. Their colours
        // are the screen's (PropArt.Lights, which has the same radii).
        static readonly (TileType Type, float Radius)[] Dressing =
        {
            (TileType.Campfire, 6), (TileType.LampPost, 5.5f), (TileType.Brazier, 5), (TileType.Lantern, 5), (TileType.Lighthouse, 7),
        };

        // The farthest any tile lights (tiles.csv allows up to 12).
        public const int MaxRadius = 12;

        // How far a tile of the type lights, 0 for one that gives none. tiles: the sheet, or null for the dressing alone.
        public static float RadiusOf(TileType type, TileData tiles)
        {
            var row = tiles?.Find(type);
            if (row != null && row.Light > 0) return row.Light;
            foreach (var (kind, radius) in Dressing)
                if (kind == type) return radius;
            return 0;
        }

        // Every type that gives light with its radius: the sheet's rows, then the dressing.
        public static IReadOnlyList<(TileType Type, float Radius)> Sources(TileData tiles)
        {
            var list = new List<(TileType, float)>();
            if (tiles != null)
                foreach (var row in tiles.Tiles)
                    if (row.Light > 0) list.Add((row.Tile, row.Light));
            foreach (var (kind, radius) in Dressing)
                if (!list.Exists(l => l.Item1 == kind)) list.Add((kind, radius));
            return list;
        }

        // Whether a spot is within the light of a tile that burns: within its radius of the tile's middle, as the crow
        // flies (walls are not asked; a lamp's light goes round a corner on the screen too). At most within: only
        // lights that near count, for asking after one kind's reach (a lit lamp's six tiles).
        public static bool Lit(TileMap map, TileData tiles, Vec2 position, float within = MaxRadius)
        {
            int reach = (int)Math.Ceiling(Math.Min(within, MaxRadius));
            int cx = TileMap.Cell(position.X), cy = TileMap.Cell(position.Y);
            for (int y = cy - reach; y <= cy + reach; y++)
            {
                for (int x = cx - reach; x <= cx + reach; x++)
                {
                    if (!map.InBounds(x, y)) continue;
                    var type = map[x, y];
                    // The common tiles first, so a look over open cave costs a compare a tile.
                    if (type == TileType.Floor || type == TileType.Wall) continue;
                    float radius = Math.Min(RadiusOf(type, tiles), within);
                    if (radius > 0 && Vec2.Distance(position, TileMap.Center(x, y)) <= radius) return true;
                }
            }
            return false;
        }
    }
}
