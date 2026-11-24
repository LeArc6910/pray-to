using System;
using Game.Core;

namespace Game.Client
{
    // The things that really lie on the ground, drawn so they sit on it rather than stamp a square over it (2026-11-25,
    // "입체감 없이 타일 형태로 칸 꽉 채우는거 다 찾아서 점검해"). Each follows its four neighbours (N 1, E 2, S 4, W 8 set
    // where the same thing goes on, as RailArt and the road do) and shows what thickness it has only where it ends:
    //  - laid floors (stone, brick, glowing, the skylight's): a slab a pixel thick, a lit edge north and west, a dark
    //    lip and a line of shadow on the bare ground south and east;
    //  - carpets (the three wool ones and the throne room's): one cloth, its border and fringe only round the outside;
    //  - turned soil (the six kinds): a bed heaped a little above the floor, its outer edge ragged and lit or shaded;
    //  - the harbour's planks: board ends east and west, a dark face and posts over the water south.
    //  - the rooms' walls of obsidian and void stone: a top and a front face that follow the rock round them, as the
    //    ground layer's rock does (a face only where the tile below is open, outlines only on open sides).
    // One tile each in the ground atlas (Sprites), so a paved hall still draws in one batch.
    static class FlatArt
    {
        public const int Variants = 16;
        const int Size = TileArt.Size;

        // The laid kinds, in the order the atlas holds them.
        public static readonly TileType[] Laid =
        {
            TileType.StoneFloor, TileType.BrickFloor, TileType.GlowFloor, TileType.Skylight,
            TileType.RedCarpet, TileType.BlueCarpet, TileType.YellowCarpet, TileType.ArenaCarpet,
            TileType.Soil, TileType.FertileSoil, TileType.MushroomSoil, TileType.FrostSoil, TileType.AshSoil, TileType.VoidSoil,
            TileType.Dock,
        };

        // The rooms' walls drawn as blocks of their own stone.
        public static readonly TileType[] Blocks = { TileType.ObsidianWall, TileType.VoidWall };

        public static int LaidIndex(TileType type) => Array.IndexOf(Laid, type);

        public static int BlockIndex(TileType type) => Array.IndexOf(Blocks, type);

        static bool IsFloor(TileType type) => type == TileType.StoneFloor || type == TileType.BrickFloor || type == TileType.GlowFloor || type == TileType.Skylight;

        static bool IsCarpet(TileType type) => type == TileType.RedCarpet || type == TileType.BlueCarpet || type == TileType.YellowCarpet || type == TileType.ArenaCarpet;

        // Ground a laid floor ends against: bare floor, soil, a way, a belt, water, lava, ice or quicksand. Anything
        // else beside it (more paving, a wall, a door, furniture) the floor runs on to or under.
        static bool Bare(TileType type) => type == TileType.Floor || TileMap.IsSoil(type) || Structures.IsWay(type) || World.IsBelt(type)
            || type == TileType.Water || type == TileType.Lava || RoomTileArt.IsPatch(type) || type == TileType.Waypoint;

        // Which sides of the laid tile at (x, y) go on: north 1, east 2, south 4, west 8.
        public static int Mask(TileMap map, int x, int y)
        {
            var here = map[x, y];
            bool On(int nx, int ny)
            {
                if (!map.InBounds(nx, ny)) return true;
                var there = map[nx, ny];
                if (IsFloor(here)) return !Bare(there);
                if (IsCarpet(here)) return there == here;
                if (TileMap.IsSoil(here)) return TileMap.IsSoil(there);
                // Planks end only over water: on the shore side they run on to the ground.
                return there != TileType.Water;
            }
            return (On(x, y + 1) ? 1 : 0) | (On(x + 1, y) ? 2 : 0) | (On(x, y - 1) ? 4 : 0) | (On(x - 1, y) ? 8 : 0);
        }

        // Which sides of the room wall at (x, y) are rock too (the ground layer's rock, ore and the rooms' walls).
        public static int BlockMask(TileMap map, int x, int y)
        {
            bool On(int nx, int ny) => !map.InBounds(nx, ny) || TileArt.IsRock(map[nx, ny]);
            return (On(x, y + 1) ? 1 : 0) | (On(x + 1, y) ? 2 : 0) | (On(x, y - 1) ? 4 : 0) | (On(x - 1, y) ? 8 : 0);
        }

        static readonly PixelImage[] LaidTiles = new PixelImage[32 * Variants];
        static readonly PixelImage[] BlockTiles = new PixelImage[2 * Variants];

        public static PixelImage Tile(TileType type, int mask)
        {
            mask &= Variants - 1;
            int index = LaidIndex(type);
            if (index < 0) return TileArt.Object(type);
            return LaidTiles[index * Variants + mask] ??= IsFloor(type) ? Floor(TileArt.Object(type), mask)
                : IsCarpet(type) ? Carpet(type, mask)
                : type == TileType.Dock ? Dock(TileArt.Object(type), mask)
                : Soil(TileArt.Object(type), mask, index);
        }

        public static PixelImage Block(TileType type, int mask)
        {
            mask &= Variants - 1;
            int index = Math.Max(0, BlockIndex(type));
            return BlockTiles[index * Variants + mask] ??= type == TileType.VoidWall
                ? DrawBlock(mask, Rgba.Hex("#1C1630"), Rgba.Hex("#0C0916"), Rgba.Hex("#B48CFF"), Rgba.Hex("#F0E4FF"), true)
                : DrawBlock(mask, Rgba.Hex("#2A1F3C"), Rgba.Hex("#171022"), Rgba.Hex("#8A62D0"), Rgba.Hex("#C8A8FF"), false);
        }

        static readonly Rgba LipShadow = new Rgba(0, 0, 0, 96);
        static readonly Rgba SideShadow = new Rgba(0, 0, 0, 64);

        // A slab floor: the whole tile where it goes on; where it ends, a lit edge (north, west) or a dark lip with a
        // line of shadow on the ground beyond it (south, east), and the outer corners clipped.
        static PixelImage Floor(PixelImage slab, int mask)
        {
            var image = slab.Copy();
            bool north = (mask & 1) == 0, east = (mask & 2) == 0, south = (mask & 4) == 0, west = (mask & 8) == 0;
            if (north) for (int x = 0; x < Size; x++) image.Shade(x, 0, 1.22f);
            if (west) for (int y = 0; y < Size; y++) image.Shade(0, y, 1.14f);
            if (south)
            {
                for (int x = 0; x < Size; x++)
                {
                    image[x, Size - 2] = slab[x, Size - 3].Scale(0.55f);
                    image[x, Size - 1] = LipShadow;
                }
                image[0, Size - 1] = west ? Rgba.Clear : LipShadow;
            }
            if (east)
            {
                for (int y = 0; y < Size; y++)
                {
                    if (south && y >= Size - 2) continue;
                    image[Size - 2, y] = slab[Size - 3, y].Scale(0.7f);
                    image[Size - 1, y] = SideShadow;
                }
                if (north) image[Size - 1, 0] = Rgba.Clear;
                if (south)
                {
                    image[Size - 2, Size - 2] = slab[Size - 3, Size - 3].Scale(0.5f);
                    image[Size - 1, Size - 2] = SideShadow;
                    image[Size - 1, Size - 1] = Rgba.Clear;
                }
            }
            return image;
        }

        // A carpet: the field and its small diamond on every tile, so a patch reads as one patterned cloth; the dark
        // border and a line of the light thread only along the sides where the carpet ends, the fringe only at its
        // north and south ends.
        static PixelImage Carpet(TileType type, int mask)
        {
            var (main, light, dark, weave) = type switch
            {
                TileType.RedCarpet => (Rgba.Hex("#B83A44"), Rgba.Hex("#F0B060"), Rgba.Hex("#5A1A22"), Rgba.Hex("#A8323C")),
                TileType.BlueCarpet => (Rgba.Hex("#3A64B8"), Rgba.Hex("#A8D8F0"), Rgba.Hex("#1A2A5A"), Rgba.Hex("#3258A6")),
                TileType.YellowCarpet => (Rgba.Hex("#D8A830"), Rgba.Hex("#FFF0A0"), Rgba.Hex("#6A4A14"), Rgba.Hex("#C89A28")),
                _ => (Rgba.Hex("#7A1E28"), Rgba.Hex("#C89A3C"), Rgba.Hex("#4A1018"), Rgba.Hex("#641822")),
            };
            bool arena = type == TileType.ArenaCarpet;
            var fringe = Rgba.Hex("#E8DCC0");
            var image = new PixelImage(Size, Size);
            bool north = (mask & 1) == 0, east = (mask & 2) == 0, south = (mask & 4) == 0, west = (mask & 8) == 0;
            // The throne room's carpet has no fringe: it runs to its border.
            int top = north && !arena ? 1 : 0, bottom = south && !arena ? Size - 2 : Size - 1;
            for (int y = top; y <= bottom; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int lozenge = Math.Abs(2 * x - 15) + Math.Abs(2 * y - 15);
                    var colour = (x + y) % 4 == 0 ? weave : main;
                    if (lozenge == 8 || lozenge == 10) colour = light;
                    else if (lozenge == 4 || lozenge == 6) colour = dark;
                    else if (lozenge < 4) colour = light;
                    image[x, y] = colour;
                }
            }
            // The border: dark outside, a light thread a pixel in from it.
            void Border(int x, int y, bool outer) => image[x, y] = outer ? dark : light;
            if (north) for (int x = 0; x < Size; x++) { Border(x, top, true); Border(x, top + 2, false); }
            if (south) for (int x = 0; x < Size; x++) { Border(x, bottom, true); Border(x, bottom - 2, false); }
            if (west) for (int y = top; y <= bottom; y++) { Border(0, y, true); if (image[2, y].Equals(dark)) continue; Border(2, y, false); }
            if (east) for (int y = top; y <= bottom; y++) { Border(Size - 1, y, true); if (image[Size - 3, y].Equals(dark)) continue; Border(Size - 3, y, false); }
            // Where two ends meet, the light thread turns the corner inside the border instead of crossing it.
            void Trim(int x0, int x1, int y0, int y1)
            {
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        image[x, y] = x == x0 && x0 == 0 || x == x1 && x1 == Size - 1 || y == top && y0 == top || y == bottom && y1 == bottom ? dark : main;
            }
            if (north && west) Trim(0, 1, top, top + 1);
            if (north && east) Trim(Size - 2, Size - 1, top, top + 1);
            if (south && west) Trim(0, 1, bottom - 1, bottom);
            if (south && east) Trim(Size - 2, Size - 1, bottom - 1, bottom);
            if (!arena)
            {
                if (north) for (int x = 0; x < Size; x += 2) image[x, 0] = fringe;
                if (south) for (int x = 1; x < Size; x += 2) image[x, Size - 1] = fringe;
            }
            return image;
        }

        // Turned soil: the rows of the tile where the bed goes on; where it ends, the outermost pixels are a ragged
        // rim (some left to the floor), lit north and west and dark south and east, so a plot is a bed heaped on the
        // floor and not a square cut out of it.
        static PixelImage Soil(PixelImage rows, int mask, int salt)
        {
            var image = rows.Copy();
            bool north = (mask & 1) == 0, east = (mask & 2) == 0, south = (mask & 4) == 0, west = (mask & 8) == 0;
            var deep = rows[0, 2];
            var ridge = rows[0, 0];
            bool Gap(int along, int side) => PixelNoise.Hash(along, side, 90 + salt) % 3 == 0;
            for (int i = 0; i < Size; i++)
            {
                if (north)
                {
                    image[i, 0] = Gap(i, 0) ? Rgba.Clear : ridge.Scale(1.22f);
                    if (!image.IsSet(i, 0)) image[i, 1] = ridge.Scale(1.22f);
                }
                if (south)
                {
                    image[i, Size - 1] = Gap(i, 2) ? Rgba.Clear : deep.Scale(0.7f);
                    image[i, Size - 2] = deep;
                }
                if (west)
                {
                    image[0, i] = Gap(i, 3) ? Rgba.Clear : rows[0, i].Scale(1.12f);
                }
                if (east)
                {
                    image[Size - 1, i] = Gap(i, 1) ? Rgba.Clear : deep.Scale(0.8f);
                    image.Shade(Size - 2, i, 0.82f);
                }
            }
            // The outer corners are rounded off.
            void Round(int cx, int cy, int sx, int sy)
            {
                image[cx, cy] = Rgba.Clear;
                image[cx + sx, cy] = Rgba.Clear;
                image[cx, cy + sy] = Rgba.Clear;
            }
            if (north && west) Round(0, 0, 1, 1);
            if (north && east) Round(Size - 1, 0, -1, 1);
            if (south && west) Round(0, Size - 1, 1, -1);
            if (south && east) Round(Size - 1, Size - 1, -1, -1);
            return image;
        }

        // The harbour's planks: boards across, as they were; over the water to the south their dark front face and a
        // shadow on the water, to the east and west the boards' sawn ends (lit west, shaded east), and a post head at
        // each outer corner of the south edge.
        static PixelImage Dock(PixelImage planks, int mask)
        {
            var image = planks.Copy();
            bool north = (mask & 1) == 0, east = (mask & 2) == 0, south = (mask & 4) == 0, west = (mask & 8) == 0;
            var face = Rgba.Hex("#5E3E24");
            var faceDark = Rgba.Hex("#3A2618");
            var end = Rgba.Hex("#D8AC74");
            var post = Rgba.Hex("#7A5230");
            var postLight = Rgba.Hex("#B88A54");
            if (north) for (int x = 0; x < Size; x++) image[x, 0] = end;
            if (west) for (int y = 0; y < Size; y++) if (y % 5 != 4) image[0, y] = end;
            if (east)
            {
                for (int y = 0; y < Size; y++)
                {
                    image[Size - 1, y] = SideShadow;
                    if (y % 5 != 4) image[Size - 2, y] = face;
                }
            }
            if (south)
            {
                for (int x = 0; x < Size; x++)
                {
                    bool beyond = east && x == Size - 1;
                    image[x, Size - 4] = beyond ? SideShadow : planks[x, 3];
                    image[x, Size - 3] = beyond ? SideShadow : x % 6 == 5 ? faceDark : face;
                    image[x, Size - 2] = beyond ? SideShadow : faceDark;
                    image[x, Size - 1] = LipShadow;
                }
                // Posts at the outer corners, standing a little over the boards.
                void Post(int left)
                {
                    for (int y = Size - 7; y < Size - 1; y++)
                    {
                        for (int x = left; x < left + 3; x++)
                            image[x, y] = y == Size - 7 ? postLight : x == left ? postLight : x == left + 2 ? faceDark : post;
                    }
                }
                if (west) Post(0);
                if (east) Post(Size - 4);
            }
            return image;
        }

        // A block of the rooms' stone (the look of RoomTileArt's old square, made to follow its neighbours): its top
        // over the whole tile where rock or more of it stands to the south, a front face only where the tile below is
        // open, a dark outline only on the open sides.
        static PixelImage DrawBlock(int mask, Rgba main, Rgba shade, Rgba edge, Rgba glint, bool veins)
        {
            var image = new PixelImage(Size, Size);
            var outline = Rgba.Hex("#07050C");
            bool north = (mask & 1) == 0, east = (mask & 2) == 0, south = (mask & 4) == 0, west = (mask & 8) == 0;
            int top = south ? Size - TileArt.FaceHeight : Size;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    bool face = y >= top;
                    var colour = face ? shade : main;
                    // Facets: a lighter plane across the top's upper left, a darker one lower right.
                    if (!face && (x + y) % 16 < 5 && (x * 3 + y) % 7 < 4) colour = main.Mix(edge, 0.16f);
                    if (!face && (x + y) % 16 > 12) colour = main.Mix(shade, 0.55f);
                    if (face && (x == 5 || x == 11)) colour = outline;
                    image[x, y] = colour;
                }
            }
            // Joints between the great blocks on the top.
            for (int y = 1; y < top - 1; y++) image[8 + (y > 4 ? 1 : 0), y] = shade;
            for (int x = 1; x < 8; x++) if (top > 9) image[x, 9] = shade;
            image[3, 2] = glint;
            image[4, 2] = edge;
            image[3, 3] = edge;
            image[12, 5] = glint;
            image[13, 5] = edge;
            if (veins)
            {
                int[] vein = { 2, 3, 3, 4, 5, 5, 6, 6, 7 };
                for (int i = 0; i < vein.Length; i++) image[4 + i, vein[i]] = i % 3 == 1 ? glint : edge;
                if (south)
                {
                    for (int i = 0; i < 4; i++) image[10 + i, top + 2 + i / 2] = edge;
                    image[6, top + 3] = edge;
                    image[7, top + 4] = glint.Scale(0.8f);
                }
                else
                {
                    for (int i = 0; i < 4; i++) image[10 + i, 11 + i / 2] = edge;
                }
            }
            if (south)
            {
                for (int x = 0; x < Size; x++)
                {
                    image[x, top - 1] = main.Mix(shade, 0.5f);
                    image[x, top] = edge.Mix(shade, 0.55f);
                    image[x, Size - 1] = outline;
                }
            }
            if (north) for (int x = 0; x < Size; x++) image[x, 0] = edge.Mix(main, 0.35f);
            for (int y = 0; y < Size; y++)
            {
                if (west) image[0, y] = outline;
                if (east) image[Size - 1, y] = outline;
            }
            if (north)
            {
                if (west) image[0, 0] = outline;
                if (east) image[Size - 1, 0] = outline;
            }
            return image;
        }
    }
}
