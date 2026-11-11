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
        const int Step = 3;

        public static PixelImage Compare(int seed = 7, int width = 24, int height = 14)
        {
            var world = WorldGenerator.Generate((ulong)seed);
            var map = world.Map;
            // Some way off the spawn, where the caves are narrower than round the open start.
            int left = world.SpawnX + 18 - width / 2, bottom = world.SpawnY + 14 - height / 2;
            var plain = Draw(map, world, left, bottom, width, height);
            var rounded = Draw(map, world, left, bottom, width, height);
            Round(rounded, map, left, bottom, width, height);
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

        static bool Rock(TileMap map, int x, int y) => !map.InBounds(x, y) || TileArt.IsRock(map[x, y]);

        static void Round(PixelImage image, TileMap map, int left, int bottom, int width, int height)
        {
            // The four corners as (way across, way up): top-left, top-right, bottom-left, bottom-right.
            var corners = new[] { (-1, 1), (1, 1), (-1, -1), (1, -1) };
            var copy = new PixelImage(image.Width, image.Height);
            copy.Draw(image, 0, 0);
            for (int ty = 0; ty < height; ty++)
            {
                for (int tx = 0; tx < width; tx++)
                {
                    int x = left + tx, y = bottom + ty;
                    bool rock = Rock(map, x, y);
                    foreach (var (sx, sy) in corners)
                    {
                        bool side = Rock(map, x + sx, y), upDown = Rock(map, x, y + sy), diagonal = Rock(map, x + sx, y + sy);
                        // A rock corner sticking out into open floor, or a floor corner pressed in by rock on both sides.
                        bool cut = rock && !side && !upDown && !diagonal;
                        bool fill = !rock && side && upDown;
                        if (!cut && !fill) continue;
                        int ox = tx * Px, oy = (height - 1 - ty) * Px;
                        for (int i = 0; i < Step; i++)
                        {
                            for (int j = 0; j < Step - i; j++)
                            {
                                int px = sx < 0 ? i : Px - 1 - i;
                                int py = sy > 0 ? j : Px - 1 - j;
                                // Taken from the tile beside, across the corner's side edge.
                                int fromX = ox + px + (sx < 0 ? -Px : Px), fromY = oy + py;
                                if (fromX < 0 || fromX >= copy.Width) continue;
                                image[ox + px, oy + py] = copy[fromX, fromY];
                            }
                        }
                    }
                }
            }
        }
    }
}
