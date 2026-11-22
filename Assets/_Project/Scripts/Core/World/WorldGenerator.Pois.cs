using System.Linq;

namespace Game.Core
{
    // ---- The things to find in the rooms of version 2 (2026-11-22, Docs/WorldRework.md 3단계 볼거리, 묶음 3a). Each mark a
    // chunk left that is a thing to find (a camp's fire, a shrine stone, a chest, a cart) becomes one, of a row of
    // pois.csv picked by weight among those of its room's role and biome. A chest's tile becomes its row's (wood, iron
    // or gold). Numbered in the order laid; the host and a client lay the same from the seed, so where they are is
    // never sent or saved, only what became of them (World.Pois.cs). ----
    public static partial class WorldGenerator
    {
        const ulong PoiStream = 50;

        static void LayPois(GeneratedWorld world, ulong seed, TerrainData terrain)
        {
            var graph = world.Rooms;
            var data = terrain?.Pois ?? PoiData.None;
            if (graph == null || data.Pois.Count == 0) return;
            var random = new GameRandom(seed, PoiStream);
            foreach (var mark in graph.Marks)
            {
                var room = graph.Nodes[mark.Room];
                RoomRole? role = mark.Kind switch
                {
                    RoomMarkKind.Campfire when room.Role == RoomRole.Rest => RoomRole.Rest,
                    RoomMarkKind.Shrine => RoomRole.Shrine,
                    RoomMarkKind.Cart when room.Role == RoomRole.Cart => RoomRole.Cart,
                    RoomMarkKind.Chest when room.Role is RoomRole.Treasure or RoomRole.Hidden or RoomRole.Locked or RoomRole.Trap => room.Role,
                    _ => null,
                };
                if (role is not RoomRole kind) continue;
                var choices = data.For(kind, room.Biome).ToList();
                if (choices.Count == 0) continue;
                var definition = random.PickWeighted(choices, c => c.Weight);
                if (definition.Tile is TileType tile) world.Map[mark.X, mark.Y] = tile;
                graph.PoiList.Add(new Poi(graph.PoiList.Count, definition, mark.X, mark.Y, room.Index, room.Biome));
            }
        }
    }
}
