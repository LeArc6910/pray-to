using System;
using System.Collections.Generic;

namespace Game.Core
{
    // The biomes' things to pick or break (2026-11-27, Docs/Harvest.md, the owner: "버섯 같은 것이 바닥에 떨어진 아이템이
    // 아니라 오브젝트가 있고, 파괴하거나 수확하면 얻게"): one kind to a biome, standing in its rooms. Picked (one swing) or
    // broken (a few) for what tiles.csv gives, it leaves its spent form, walked over, and grows back at the next dawn
    // (World.Harvest.cs). They take the place of the forage that lay on the floor (World.Forage.cs).
    public static class HarvestProps
    {
        public static bool IsGrown(TileType type) => type >= TileType.MushroomPatch && type <= TileType.AmberRock;
        public static bool IsSpent(TileType type) => type >= TileType.MushroomPatchPicked && type <= TileType.AmberRockBroken;
        public static bool Is(TileType type) => IsGrown(type) || IsSpent(type);

        // Picked with a swing (the rest are broken).
        public static bool Picked(TileType type) =>
            type == TileType.MushroomPatch || type == TileType.HerbBush || type == TileType.PepperBush || type == TileType.VoidPod;

        public static TileType SpentOf(TileType grown) => grown + (TileType.MushroomPatchPicked - TileType.MushroomPatch);
        public static TileType GrownOf(TileType spent) => spent - (TileType.MushroomPatchPicked - TileType.MushroomPatch);

        // Each biome's kind.
        public static TileType Of(Biome biome) => biome switch
        {
            Biome.Cave => TileType.MushroomPatch,
            Biome.Mushroom => TileType.HerbBush,
            Biome.Ice => TileType.FrostGeode,
            Biome.Crystal => TileType.MithrilRock,
            Biome.Lava => TileType.PepperBush,
            Biome.Abyss => TileType.VoidPod,
            Biome.Lake => TileType.ClamBed,
            _ => TileType.AmberRock,
        };
    }

    public static partial class WorldGenerator
    {
        // The first version with harvest props in place of the floor's forage.
        public const int HarvestVersion = 9;
        const ulong HarvestStream = 58;
        // A room gets one, and one more for every HarvestPerRadius of its radius; they stand HarvestGap apart.
        public const int HarvestPerRadius = 5;
        const int HarvestGap = 3;

        // In every room, on bare floor with bare floor all round, off what the props keep clear, its biome's kind.
        static void LayHarvest(GeneratedWorld world, ulong seed, bool[] kept)
        {
            var map = world.Map;
            var graph = world.Rooms;
            int width = map.Width, height = map.Height;
            var random = new GameRandom(seed, HarvestStream);
            var laid = new List<(int X, int Y)>();
            bool Bare(int x, int y)
            {
                if (x < 2 || y < 2 || x >= width - 2 || y >= height - 2 || kept[y * width + x]) return false;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (map[x + dx, y + dy] != TileType.Floor) return false;
                return true;
            }
            foreach (var room in graph.Nodes)
            {
                if (room.Kind != RoomKind.Room || room.Role == RoomRole.Rest) continue;
                int want = 1 + room.Radius / HarvestPerRadius, placed = 0;
                for (int attempt = 0; attempt < want * 16 && placed < want; attempt++)
                {
                    int reach = Math.Max(1, room.Radius - 2);
                    int x = room.X + random.Next(2 * reach + 1) - reach, y = room.Y + random.Next(2 * reach + 1) - reach;
                    if ((x - room.X) * (x - room.X) + (y - room.Y) * (y - room.Y) > reach * reach || !Bare(x, y)) continue;
                    bool near = false;
                    foreach (var (lx, ly) in laid)
                        if (Math.Abs(lx - x) <= HarvestGap && Math.Abs(ly - y) <= HarvestGap) near = true;
                    if (near) continue;
                    map[x, y] = HarvestProps.Of(world.Biomes[x, y]);
                    laid.Add((x, y));
                    placed++;
                }
            }
        }
    }
}
