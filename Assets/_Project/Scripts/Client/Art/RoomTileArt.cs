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
        public static bool IsPatch(TileType type) => type == TileType.IceFloor || type == TileType.Quicksand || type == TileType.ShallowWater
            // The lava fields' steam vents and the abyss's cracked plates and the holes they leave (B4b).
            || type == TileType.SteamVent || type == TileType.CrackedPlate || type == TileType.BrokenPlate;

        // Which sides of the patch at (x, y) meet more of it (N 1, E 2, S 4, W 8). Rock counts: a sheet of ice runs
        // under the wall rather than stopping a pixel short of it.
        public static int PatchMask(TileMap map, int x, int y)
        {
            var type = map[x, y];
            // A vent is a spot of its own; a plate and a hole meet edge to edge (B4b).
            if (type == TileType.SteamVent) return 0;
            if (type == TileType.CrackedPlate || type == TileType.BrokenPlate)
            {
                bool Plate(int nx, int ny) => map.InBounds(nx, ny) && (map[nx, ny] == TileType.CrackedPlate || map[nx, ny] == TileType.BrokenPlate);
                return (Plate(x, y + 1) ? 1 : 0) | (Plate(x + 1, y) ? 2 : 0) | (Plate(x, y - 1) ? 4 : 0) | (Plate(x - 1, y) ? 8 : 0);
            }
            // Shallows run on into the deep water beside them.
            bool Joins(int nx, int ny) => !map.InBounds(nx, ny) || map[nx, ny] == type || TileArt.IsRock(map[nx, ny])
                || type == TileType.ShallowWater && map[nx, ny] == TileType.Water;
            return (Joins(x, y + 1) ? 1 : 0) | (Joins(x + 1, y) ? 2 : 0) | (Joins(x, y - 1) ? 4 : 0) | (Joins(x - 1, y) ? 8 : 0);
        }

        static readonly PixelImage[] IceTiles = new PixelImage[32];
        static readonly PixelImage[] SandTiles = new PixelImage[32];
        static readonly PixelImage[] ShallowTiles = new PixelImage[32];
        static readonly PixelImage[] VentTiles = new PixelImage[32];
        static readonly PixelImage[] PlateTiles = new PixelImage[32];
        static readonly PixelImage[] HoleTiles = new PixelImage[32];

        // The patch with these sides joined, in one of two layings picked by where the tile is.
        public static PixelImage Patch(TileType type, int mask, int laying = 0)
        {
            int index = (mask & 15) | (laying & 1) << 4;
            return type == TileType.Quicksand ? SandTiles[index] ??= Quicksand(mask & 15, laying & 1)
                : type == TileType.ShallowWater ? ShallowTiles[index] ??= Shallow(mask & 15, laying & 1)
                : type == TileType.SteamVent ? VentTiles[index] ??= Vent(laying & 1)
                : type == TileType.CrackedPlate ? PlateTiles[index] ??= Plate(mask & 15, laying & 1)
                : type == TileType.BrokenPlate ? HoleTiles[index] ??= Hole(mask & 15, laying & 1)
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

        // A steam vent of the lava fields (B4b): a low mound of basalt crust standing a little off the floor (a lit top,
        // a dark front face, soot round its foot), split along its length by a jagged fissure that glows from below,
        // hottest in its middle, with a few cinders and a thin wisp of steam. A long crack across a mound, not a ring
        // with spots in it, so it never reads as a face.
        static PixelImage Vent(int laying)
        {
            var image = new PixelImage(Size, Size);
            var crustTop = Rgba.Hex("#5A4840");
            var crust = Rgba.Hex("#3E302B");
            var face = Rgba.Hex("#241A17");
            var deep = Rgba.Hex("#8A2A10");
            var glow = Rgba.Hex("#FF7A30");
            var hot = Rgba.Hex("#FFD060");
            float cx = 7.5f, cy = 8.5f;
            // Soot round the foot, fading out.
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x + 0.5f - cx) / 7.4f, dy = (y + 0.5f - cy - 1) / 5.2f;
                    float d = dx * dx + dy * dy;
                    if (d < 1) image.Blend(x, y, new Rgba(10, 6, 4, (int)(110 * (1 - d))));
                }
            // The mound: its top, then two rows of front face below it.
            bool Top(int x, int y)
            {
                float dx = (x + 0.5f - cx) / 6.2f, dy = (y + 0.5f - cy) / 3.6f;
                return dx * dx + dy * dy <= 1;
            }
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    if (Top(x, y)) image[x, y] = y < cy - 1 ? crustTop : crust;
                    else if (Top(x, y - 1) || Top(x, y - 2)) image[x, y] = face;
                }
            // A few lumps of crust on its rim, lit on top.
            foreach (var (lx, ly) in laying == 0 ? new[] { (3, 6), (11, 6), (13, 9) } : new[] { (2, 8), (5, 6), (12, 7) })
                if (Top(lx, ly)) image[lx, ly] = Rgba.Hex("#6E5A50");
            // The fissure: corner to corner of the mound, jagged, a dark lip below it, a branch off its middle.
            int[] line = laying == 0 ? new[] { 9, 9, 8, 8, 9, 8, 8, 7, 8, 8, 7 } : new[] { 8, 8, 9, 9, 8, 8, 9, 8, 8, 9, 9 };
            for (int i = 0; i < line.Length; i++)
            {
                int x = 2 + i, y = line[i];
                if (!Top(x, y)) continue;
                bool middle = i >= 3 && i <= 7;
                image[x, y] = middle ? hot : glow;
                if (Top(x, y + 1)) image[x, y + 1] = middle ? glow.Mix(deep, 0.5f) : deep;
            }
            int bx = laying == 0 ? 7 : 8;
            image[bx, line[bx - 2] - 1] = glow;
            image[bx + (laying == 0 ? 1 : -1), line[bx - 2] - 2] = deep;
            // Cinders on the crust, and a thin wisp of steam rising off the hottest part.
            image[laying == 0 ? 4 : 11, 10] = Rgba.Hex("#C84A1C");
            image[laying == 0 ? 10 : 5, 11] = Rgba.Hex("#C84A1C");
            image.Blend(6 + laying, 5, new Rgba(214, 206, 206, 150));
            image.Blend(7 + laying, 4, new Rgba(214, 206, 206, 110));
            image.Blend(7 + laying, 3, new Rgba(214, 206, 206, 70));
            image.Blend(8 + laying, 2, new Rgba(214, 206, 206, 40));
            return image;
        }

        // A cracked plate of the abyss (B4b): not one slab painted over the cell but two or three broken flagstones of
        // grey-violet stone, each with a lit top and a dark front face standing up off the floor and a shadow under it,
        // the gaps between them open to the dark below with a faint violet light seeping up through them, and a hairline
        // crack across a stone. Paler than the abyss's floor so it still reads as "thin ice" at a glance, but stones
        // with depth, not a flat tile; each tile breaks its own way, so a run of them reads as a cracked causeway.
        static PixelImage Plate(int mask, int laying)
        {
            var image = new PixelImage(Size, Size);
            var tops = new[] { Ramp.Of("#5E5676"), Ramp.Of("#665C80"), Ramp.Of("#56506E") };
            var face = Rgba.Hex("#241C34");
            var faceLit = Rgba.Hex("#322846");
            var seep = new Rgba(150, 90, 255, 90);
            var crack = Rgba.Hex("#1A1028");
            // Two or three stones, where they lie picked by the tile's laying and which sides meet more plate, so a run of
            // them never repeats one pattern.
            uint pick = PixelNoise.Hash(mask, laying, 61);
            int count = 2 + (int)(pick % 2);
            var seeds = new (float X, float Y)[count];
            for (int i = 0; i < count; i++)
            {
                uint h = PixelNoise.Hash(mask * 7 + i, laying * 13 + i, 67);
                // Spread round the tile: each stone in its own slice, nudged.
                float angle = (float)(i * 2 * System.Math.PI / count + (pick % 360) * System.Math.PI / 180);
                float reach = 2.5f + (h % 20) / 10f;
                seeds[i] = (7.5f + (float)System.Math.Cos(angle) * reach + (h >> 8) % 3 - 1, 6.5f + (float)System.Math.Sin(angle) * reach * 0.9f + (h >> 12) % 3 - 1);
            }
            // Which stone a pixel's top belongs to, or -1 for a gap: the nearest seed, if it is clearly nearer than the
            // next. The tile's own sides are gaps, so tiles never run into one sheet.
            int Stone(int x, int y)
            {
                if (x < 1 || x > Size - 2 || y < 1 || y > Size - 4) return -1;
                // The stones' outer corners are knocked off, a different corner on each tile.
                bool cornerX = x < 3 || x > Size - 4, cornerY = y < 3 || y > Size - 6;
                if (cornerX && cornerY && PixelNoise.Hash(x < 8 ? 0 : 1, y < 8 ? 0 : 1, 71 + mask + laying * 16) % 3 != 0) return -1;
                int best = -1;
                float near = float.MaxValue, next = float.MaxValue;
                for (int i = 0; i < seeds.Length; i++)
                {
                    float dx = x + 0.5f - seeds[i].X, dy = y + 0.5f - seeds[i].Y;
                    float d = (float)System.Math.Sqrt(dx * dx + dy * dy);
                    if (d < near) { next = near; near = d; best = i; }
                    else if (d < next) next = d;
                }
                return next - near < 1.4f ? -1 : best;
            }
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int stone = Stone(x, y);
                    if (stone >= 0)
                    {
                        var ramp = tops[stone];
                        // Lit along its upper edge, a speck of grain here and there.
                        var colour = Stone(x, y - 1) != stone ? ramp.Light : Stone(x - 1, y) != stone ? ramp.Main.Mix(ramp.Light, 0.4f) : ramp.Main;
                        if (PixelNoise.Hash(x, y, 53 + laying) % 9 == 0) colour = ramp.Shade;
                        image[x, y] = colour;
                        continue;
                    }
                    // The front face of the stone above, two pixels deep, then its shadow on the dark.
                    int above = Stone(x, y - 1), above2 = Stone(x, y - 2);
                    if (above >= 0) image[x, y] = faceLit;
                    else if (above2 >= 0 && Stone(x, y - 1) < 0) image[x, y] = face;
                    else if (Stone(x, y - 3) >= 0) image.Blend(x, y, new Rgba(0, 0, 0, 110));
                    // A gap between two stones lets the violet up; round the tile's outside the floor shows bare.
                    else if (x > 1 && x < Size - 2 && y > 1 && y < Size - 4 && (Stone(x - 1, y) >= 0 || Stone(x + 1, y) >= 0)
                        && (Stone(x, y + 1) >= 0 || Stone(x, y - 1) >= 0 || Stone(x - 2, y) >= 0 && Stone(x + 2, y) >= 0)) image.Blend(x, y, seep);
                }
            }
            // A hairline crack across the biggest stone, one pixel of it lit from below.
            int[] across = laying == 0 ? new[] { 6, 6, 7, 7, 8, 8, 9 } : new[] { 9, 9, 8, 8, 7, 7, 7 };
            int target = Stone(7, across[3]);
            for (int i = 0; i < across.Length; i++)
            {
                int x = 4 + i, y = across[i];
                if (Stone(x, y) != target || Stone(x, y - 1) != target) continue;
                image[x, y] = i == 3 ? Rgba.Hex("#B080FF") : crack;
            }
            return image;
        }

        // The hole a plate leaves (B4b): the void under it, black with a few far stars, its rim crumbled stone.
        static PixelImage Hole(int mask, int laying)
        {
            var image = new PixelImage(Size, Size);
            var dark = Rgba.Hex("#05030A");
            var deep = Rgba.Hex("#120A22");
            var rim = Rgba.Hex("#3A3050");
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (!InPatch(x, y, mask)) continue;
                    image[x, y] = Edge(x, y, mask) ? rim : (x + y + laying) % 7 == 0 ? deep : dark;
                }
            }
            foreach (var (sx, sy) in laying == 0 ? new[] { (4, 5), (11, 10) } : new[] { (9, 4), (5, 11) })
                if (InPatch(sx, sy, mask) && !Edge(sx, sy, mask)) image[sx, sy] = Rgba.Hex("#E6D6FF");
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
