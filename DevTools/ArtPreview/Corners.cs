using Game.Core;

namespace Game.Client
{
    // A trial of softer cave corners before anything changes in the game (Docs/ArtPipeline.md 둥근 모서리 시험,
    // 2026-11-11, the research report's candidate 23): a patch of cave drawn as the game draws it, beside the same patch
    // with each square corner rounded by a three-pixel step. Where rock sticks out (a rock tile whose two open
    // neighbours meet at a corner) the corner is cut back to the floor beside it; where floor goes into rock (a floor
    // tile with rock on two sides of a corner) the corner is filled with the rock beside it. Doing this in the game
    // would mean a quarter-tile or dual-grid layer; the picture is for a person to say first whether it is worth it.
    static class Corners
    {
        const int Px = TileArt.Size;

        public static PixelImage Compare(int seed = 7, int width = 24, int height = 14)
        {
            var world = WorldGenerator.Generate((ulong)seed);
            var map = world.Map;
            // Some way off the spawn, where the caves are narrower than round the open start.
            int left = world.SpawnX + 18 - width / 2, bottom = world.SpawnY + 14 - height / 2;
            var plain = Draw(map, world, left, bottom, width, height);
            var rounded = Draw(map, world, left, bottom, width, height);
            Round(rounded, world, left, bottom, width, height);
            var both = new PixelImage(width * Px * 2 + 8, height * Px);
            both.Draw(plain, 0, 0);
            both.Draw(rounded, width * Px + 8, 0);
            return both;
        }

        static PixelImage Draw(TileMap map, GeneratedWorld world, int left, int bottom, int width, int height)
        {
            var image = new PixelImage(width * Px, height * Px);
            for (int ty = 0; ty < height; ty++)
                for (int tx = 0; tx < width; tx++)
                {
                    int x = left + tx, y = bottom + ty;
                    image.Draw(TileArt.Ground(TileArt.GroundAt(map, x, y, false, world.Biomes[x, y])), tx * Px, (height - 1 - ty) * Px);
                }
            return image;
        }

        // The game's own corner layer (TileArt.Rounds, TileArt.CornerOverlay) over each tile.
        static void Round(PixelImage image, GeneratedWorld world, int left, int bottom, int width, int height)
        {
            var map = world.Map;
            var sources = new int[4];
            for (int ty = 0; ty < height; ty++)
            {
                for (int tx = 0; tx < width; tx++)
                {
                    int x = left + tx, y = bottom + ty;
                    bool any = false;
                    for (int c = 0; c < TileArt.CornerWays.Length; c++)
                    {
                        var (sx, sy) = TileArt.CornerWays[c];
                        bool rounds = map.InBounds(x + sx, y) && TileArt.Rounds(map, x, y, sx, sy);
                        sources[c] = rounds ? TileArt.GroundAt(map, x + sx, y, false, world.Biomes[x + sx, y]) : -1;
                        any |= rounds;
                    }
                    if (any) image.Draw(TileArt.CornerOverlay(sources), tx * Px, (height - 1 - ty) * Px);
                }
            }
        }
    }
}
