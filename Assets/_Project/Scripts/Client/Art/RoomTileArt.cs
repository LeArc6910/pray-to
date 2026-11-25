using Game.Core;

namespace Game.Client
{
    // The tiles the rooms of the generator's version 2 brought (2026-11-21, Docs/WorldRework.md 방의 역할): a cracked
    // wall, a grate, walls of obsidian and of void stone, and the two floors that change a walk, ice and quicksand.
    // The three walls stand on the ground layer's rock (TileArt.IsRock), so they keep its top, its front face and its
    // shadow; what is drawn here lies over that. Ice and quicksand are patches on the floor whose edges follow their
    // neighbours (Patch), like the road's.
    static class RoomTileArt
    {
        const int Size = TileArt.Size;
        // A wall's top is the upper ten rows; below it the front face shows where the tile below is open.
        const int Top = 10;

        // And the lake's shallows (B3b), laid round its water.
        public static bool IsPatch(TileType type) => type == TileType.IceFloor || type == TileType.Quicksand || type == TileType.ShallowWater;

        // Which sides of the patch at (x, y) meet more of it (N 1, E 2, S 4, W 8). Rock counts: a sheet of ice runs
        // under the wall rather than stopping a pixel short of it.
        public static int PatchMask(TileMap map, int x, int y)
        {
            var type = map[x, y];
            // Shallows run on into the deep water beside them.
            bool Joins(int nx, int ny) => !map.InBounds(nx, ny) || map[nx, ny] == type || TileArt.IsRock(map[nx, ny])
                || type == TileType.ShallowWater && map[nx, ny] == TileType.Water;
            return (Joins(x, y + 1) ? 1 : 0) | (Joins(x + 1, y) ? 2 : 0) | (Joins(x, y - 1) ? 4 : 0) | (Joins(x - 1, y) ? 8 : 0);
        }

        static readonly PixelImage[] IceTiles = new PixelImage[32];
        static readonly PixelImage[] SandTiles = new PixelImage[32];
        static readonly PixelImage[] ShallowTiles = new PixelImage[32];

        // The patch with these sides joined, in one of two layings picked by where the tile is.
        public static PixelImage Patch(TileType type, int mask, int laying = 0)
        {
            int index = (mask & 15) | (laying & 1) << 4;
            return type == TileType.Quicksand ? SandTiles[index] ??= Quicksand(mask & 15, laying & 1)
                : type == TileType.ShallowWater ? ShallowTiles[index] ??= Shallow(mask & 15, laying & 1)
                : IceTiles[index] ??= Ice(mask & 15, laying & 1);
        }

        public static int Laying(int x, int y) => (int)(PixelNoise.Hash(x, y, 31) & 1);

        // Whether the pixel is inside the patch: its unjoined sides stop a pixel in, and a corner between two of them
        // is cut round.
        static bool InPatch(int x, int y, int mask)
        {
            bool north = (mask & 1) != 0, east = (mask & 2) != 0, south = (mask & 4) != 0, west = (mask & 8) != 0;
            // Image rows run from the top.
            int left = west ? 0 : 1, right = east ? Size - 1 : Size - 2, top = north ? 0 : 1, bottom = south ? Size - 1 : Size - 2;
            if (x < left || x > right || y < top || y > bottom) return false;
            int dx = !west && x - left < 3 ? 2 - (x - left) : !east && right - x < 3 ? 2 - (right - x) : -1;
            int dy = !north && y - top < 3 ? 2 - (y - top) : !south && bottom - y < 3 ? 2 - (bottom - y) : -1;
            return dx < 0 || dy < 0 || dx + dy < 3;
        }

        // The patch's rim: a pixel of it beside one of the tile's pixels that is not.
        static bool Edge(int x, int y, int mask)
        {
            bool Out(int px, int py) => px >= 0 && py >= 0 && px < Size && py < Size && !InPatch(px, py, mask);
            return InPatch(x, y, mask) && (Out(x - 1, y) || Out(x + 1, y) || Out(x, y - 1) || Out(x, y + 1));
        }

        // A sheet of ice: pale blue, lighter streaks lying one way, a glint or two, a bright rim where it ends.
        static PixelImage Ice(int mask, int laying)
        {
            var ice = Ramp.Of("#A9D8EE");
            var image = new PixelImage(Size, Size);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (!InPatch(x, y, mask)) continue;
                    // Streaks run down to the right, a pixel thick, three to a tile.
                    int streak = (x + 2 * y + laying * 5) % 11;
                    var colour = streak == 0 ? ice.Light : streak == 1 ? ice.Light.Mix(ice.Main, 0.5f) : (x * 7 + y * 3 + laying) % 13 == 0 ? ice.Shade.Mix(ice.Main, 0.5f) : ice.Main;
                    if (Edge(x, y, mask)) colour = Rgba.Hex("#E8F7FF");
                    image[x, y] = colour;
                }
            }
            // Glints.
            foreach (var (gx, gy) in laying == 0 ? new[] { (4, 3), (11, 9) } : new[] { (10, 4), (5, 11) })
            {
                if (!InPatch(gx, gy, mask) || !InPatch(gx + 1, gy, mask)) continue;
                image[gx, gy] = Rgba.White;
                image[gx + 1, gy] = Rgba.Hex("#E8F7FF");
            }
            return image;
        }

        // The lake's shallows (B3b): clear teal water over the stones of the floor, the stones dim through it, slow
        // light ripples lying one way, and a pale wet rim where it laps the dry floor.
        static PixelImage Shallow(int mask, int laying)
        {
            var water = Ramp.Of("#3FA8A0");
            var image = new PixelImage(Size, Size);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (!InPatch(x, y, mask)) continue;
                    // Ripples: short bright dashes in rows, every row shifted, so a sheet of it reads as moving water.
                    int row = (y + laying * 2) % 5;
                    int dash = (x + y * 3 + laying * 4) % 9;
                    var colour = water.Main.Mix(water.Shade, 0.35f);
                    if (row == 0 && dash < 3) colour = water.Light;
                    else if (row == 0 && dash == 3) colour = water.Light.Mix(water.Main, 0.5f);
                    // A stone of the floor seen through the water.
                    else if ((x * 5 + y * 3 + laying * 7) % 19 == 0) colour = water.Shade.Mix(water.Dark, 0.4f);
                    if (Edge(x, y, mask)) colour = Rgba.Hex("#BFF2E8");
                    image[x, y] = colour;
                }
            }
            return image;
        }

        // Quicksand: ochre darker than the dunes, rings sinking toward the middle, a dark rim.
        static PixelImage Quicksand(int mask, int laying)
        {
            var sand = Ramp.Of("#B8944E");
            var image = new PixelImage(Size, Size);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (!InPatch(x, y, mask)) continue;
                    // Ripples: bands across the tile that bend, so neighbours read as one sinking pool.
                    int band = (y + (x + laying * 3) / 4 + ((x >> 2) & 1)) % 5;
                    var colour = band == 0 ? sand.Shade : band == 1 ? sand.Main.Mix(sand.Shade, 0.5f) : band == 3 ? sand.Light.Mix(sand.Main, 0.6f) : sand.Main;
                    if ((x * 5 + y * 11 + laying * 7) % 17 == 0) colour = sand.Dark;
                    if (Edge(x, y, mask)) colour = sand.Dark;
                    image[x, y] = colour;
                }
            }
            return image;
        }

        // Cracks over the rock it stands on: a dark jagged line down the top and the face, splitting twice, with a
        // chink of light where it is widest.
        public static readonly PixelImage Cracked = Draw(image =>
        {
            var dark = Rgba.Hex("#120E0C");
            var deep = Rgba.Hex("#2A211C");
            var chink = Rgba.Hex("#E8D8A8");
            int[] line = { 7, 7, 8, 8, 7, 6, 6, 7, 8, 8, 9, 9, 8, 8, 7, 7 };
            for (int y = 0; y < Size; y++)
            {
                image[line[y], y] = dark;
                if (y % 3 != 2) image[line[y] + 1, y] = deep;
            }
            // Branches.
            for (int i = 0; i < 5; i++) image[6 - i, 5 + (i + 1) / 2] = i < 3 ? dark : deep;
            for (int i = 0; i < 5; i++) image[10 + i, 10 - (i + 1) / 2] = i < 3 ? dark : deep;
            for (int i = 0; i < 3; i++) image[9 + i, 2 + i / 2] = deep;
            image[7, 3] = chink;
            image[8, 9] = chink;
            image[8, 10] = chink.Scale(0.8f);
            // Loose chips at its foot.
            image[4, 14] = deep;
            image[5, 14] = dark;
            image[11, 15] = deep;
            image[12, 14] = dark;
        });

        // A wall of obsidian: black glass in great blocks, violet where the light catches an edge.
        public static readonly PixelImage Obsidian = Block(Rgba.Hex("#2A1F3C"), Rgba.Hex("#171022"), Rgba.Hex("#8A62D0"), Rgba.Hex("#C8A8FF"), false);

        // A wall of void stone: darker still, with veins that glow.
        public static readonly PixelImage VoidStone = Block(Rgba.Hex("#1C1630"), Rgba.Hex("#0C0916"), Rgba.Hex("#B48CFF"), Rgba.Hex("#F0E4FF"), true);

        static PixelImage Block(Rgba main, Rgba shade, Rgba edge, Rgba glint, bool veins) => Draw(image =>
        {
            var outline = Rgba.Hex("#07050C");
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    bool face = y >= Top;
                    var colour = face ? shade : main;
                    // Facets: a lighter plane across the top's upper left, a darker one lower right.
                    if (!face && x + y < 9) colour = main.Mix(edge, 0.18f);
                    if (!face && x + y > 18) colour = main.Mix(shade, 0.6f);
                    if (face && (x == 5 || x == 11)) colour = outline;
                    image[x, y] = colour;
                }
            }
            for (int x = 0; x < Size; x++)
            {
                image[x, 0] = edge.Mix(main, 0.35f);
                image[x, Top - 1] = main.Mix(shade, 0.5f);
                image[x, Top] = edge.Mix(shade, 0.55f);
                image[x, Size - 1] = outline;
            }
            for (int y = 0; y < Size; y++)
            {
                image[0, y] = outline;
                image[Size - 1, y] = outline;
            }
            // The joint between two blocks on the top, and a glint on each.
            for (int y = 1; y < Top - 1; y++) image[8 + (y > 4 ? 1 : 0), y] = shade;
            image[3, 2] = glint;
            image[4, 2] = edge;
            image[3, 3] = edge;
            image[12, 5] = glint;
            image[13, 5] = edge;
            if (!veins) return;
            int[] vein = { 2, 3, 3, 4, 5, 5, 6, 6, 7 };
            for (int i = 0; i < vein.Length; i++) image[4 + i, vein[i]] = i % 3 == 1 ? glint : edge;
            for (int i = 0; i < 4; i++) image[10 + i, 12 + i / 2] = edge;
            image[6, 13] = edge;
            image[7, 14] = glint.Scale(0.8f);
        });

        // A grate: iron bars from floor to lintel with the floor seen between them.
        public static readonly PixelImage Grate = Draw(image =>
        {
            var iron = Ramp.Of("#8E98A6");
            var outline = Rgba.Hex("#14161A");
            // The lintel and the sill.
            for (int x = 0; x < Size; x++)
            {
                image[x, 1] = outline;
                image[x, 2] = iron.Light;
                image[x, 3] = iron.Shade;
                image[x, 4] = outline;
                image[x, 13] = iron.Main;
                image[x, 14] = iron.Dark;
                image[x, 15] = outline;
            }
            // Five bars, lit on the left.
            for (int bar = 0; bar < 5; bar++)
            {
                int x = 1 + bar * 3;
                for (int y = 4; y < 13; y++)
                {
                    image[x, y] = iron.Light;
                    image[x + 1, y] = iron.Shade;
                }
                image[x, 8] = Rgba.White;
            }
            // Rivets on the lintel.
            image[2, 2] = Rgba.White;
            image[13, 2] = Rgba.White;
        });

        // A camp's trapdoor down to its cellar (2026-11-24, Docs/BiomeRework.md B1c): planks in a stone frame, bound with
        // iron, a ring to pull. Shut until the camp's band falls.
        public static readonly PixelImage Trapdoor = Draw(image =>
        {
            var wood = Ramp.Of("#7A5634");
            var iron = Ramp.Of("#7E8792");
            var stone = Ramp.Of("#6E6458");
            var outline = Rgba.Hex("#16100C");
            Frame(image, stone, outline);
            // Four planks, lit on the left, the seams dark.
            for (int y = 3; y < 13; y++)
            {
                for (int x = 3; x < 13; x++)
                {
                    int seam = (x - 3) % 3;
                    image[x, y] = x == 3 ? wood.Light : seam == 2 ? wood.Dark : (x + y * 3) % 7 == 0 ? wood.Shade : wood.Main;
                }
            }
            // Two iron bands across with their rivets.
            foreach (int y in new[] { 5, 10 })
            {
                for (int x = 3; x < 13; x++) image[x, y] = x == 3 ? iron.Light : iron.Main;
                image[4, y] = Rgba.White;
                image[11, y] = Rgba.White;
            }
            // The ring.
            image[7, 7] = iron.Light;
            image[8, 7] = iron.Main;
            image[6, 8] = iron.Light;
            image[9, 8] = iron.Shade;
            image[7, 9] = iron.Shade;
            image[8, 9] = iron.Dark;
        });

        // The same trapdoor thrown open: its lid stood up against the frame's top, the dark below with a ladder's top.
        public static readonly PixelImage OpenTrapdoor = Draw(image =>
        {
            var wood = Ramp.Of("#7A5634");
            var stone = Ramp.Of("#6E6458");
            var outline = Rgba.Hex("#16100C");
            var dark = Rgba.Hex("#0B0806");
            Frame(image, stone, outline);
            for (int y = 3; y < 13; y++)
                for (int x = 3; x < 13; x++) image[x, y] = y < 6 ? dark : Rgba.Hex("#140E0A");
            // The ladder's rails and rungs going down into the dark, fainter the deeper.
            for (int y = 4; y < 13; y++)
            {
                var rail = y < 8 ? wood.Main : wood.Dark;
                image[5, y] = rail;
                image[10, y] = rail;
                if (y % 3 == 1)
                    for (int x = 6; x < 10; x++) image[x, y] = y < 8 ? wood.Light : wood.Shade;
            }
            // The lid, stood up behind the hole (its edge seen from the front), over the frame's top.
            for (int x = 2; x < 14; x++)
            {
                image[x, 0] = outline;
                image[x, 1] = x == 2 ? wood.Light : wood.Main;
                image[x, 2] = wood.Shade;
            }
        });

        // A trapdoor's frame: a ring of stone two pixels wide, outlined, lit top and left.
        static void Frame(PixelImage image, Ramp stone, Rgba outline)
        {
            for (int y = 1; y < 15; y++)
            {
                for (int x = 1; x < 15; x++)
                {
                    bool rim = x == 1 || y == 1 || x == 14 || y == 14;
                    bool ring = x <= 2 || y <= 2 || x >= 13 || y >= 13;
                    if (rim) image[x, y] = outline;
                    else if (ring) image[x, y] = x == 2 || y == 2 ? stone.Light : stone.Shade;
                }
            }
        }

        static PixelImage Draw(System.Action<PixelImage> draw)
        {
            var image = new PixelImage(Size, Size);
            draw(image);
            return image;
        }
    }
}
