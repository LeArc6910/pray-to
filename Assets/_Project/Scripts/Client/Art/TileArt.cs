using System.Collections.Generic;
using Game.Core;

namespace Game.Client
{
    // What the ground layer shows on a tile. Rock at the map's edge is bedrock, darker and bluer, because nobody
    // can dig it.
    enum GroundKind
    {
        Floor,
        Arena,
        Rock,
        CopperOre,
        IronOre,
        Bedrock,
    }

    // The cave tiles, 16 pixels a tile, seen from above and a little in front like Core Keeper: a wall shows its
    // top, and its front face where the tile below is open, so the caves read as raised rock around a floor. Each
    // tile depends on which of its four neighbours are rock, and has a few variants picked by position so large
    // areas do not repeat. Placed things (workbench, furnace, chest, machines) are drawn on a second layer over the
    // floor.
    static class TileArt
    {
        public const int Size = 16;
        public const int Variants = 4;
        const int Masks = 16;
        const int Kinds = 6;
        // Neighbour bits: which sides are rock.
        public const int Up = 1;
        public const int Right = 2;
        public const int Down = 4;
        public const int Left = 8;
        const int FaceHeight = 6;

        public static int GroundCount => Kinds * Masks * Variants;

        public static int Index(GroundKind kind, int mask, int variant) => ((int)kind * Masks + mask) * Variants + variant;

        public static bool IsRock(TileType type) => type == TileType.Wall || type == TileType.CopperOre || type == TileType.IronOre;

        // Which ground tile goes at (x, y). Outside the map reads as wall.
        public static int GroundAt(TileMap map, int x, int y, bool inArena)
        {
            int mask = (IsRock(map[x, y + 1]) ? Up : 0)
                | (IsRock(map[x + 1, y]) ? Right : 0)
                | (IsRock(map[x, y - 1]) ? Down : 0)
                | (IsRock(map[x - 1, y]) ? Left : 0);
            var kind = map[x, y] switch
            {
                TileType.Wall => map.IsEdge(x, y) ? GroundKind.Bedrock : GroundKind.Rock,
                TileType.CopperOre => GroundKind.CopperOre,
                TileType.IronOre => GroundKind.IronOre,
                _ => inArena ? GroundKind.Arena : GroundKind.Floor,
            };
            return Index(kind, mask, (int)(PixelNoise.Hash(x, y, 7) % Variants));
        }

        public static PixelImage Ground(int index)
        {
            var kind = (GroundKind)(index / (Masks * Variants));
            int mask = index / Variants % Masks;
            int variant = index % Variants;
            return kind switch
            {
                GroundKind.Floor => Floor(mask, variant),
                GroundKind.Arena => ArenaFloor(mask, variant),
                GroundKind.Rock => Rock(mask, variant, RockColors),
                GroundKind.Bedrock => Rock(mask, variant, BedrockColors),
                GroundKind.CopperOre => Ore(Rock(mask, variant, RockColors), mask, variant, Ramp.Of("#D6783F")),
                _ => Ore(Rock(mask, variant, RockColors), mask, variant, Ramp.Of("#A9BDD6"), sparkle: true),
            };
        }

        // Placed things that sit on the floor, or null for tiles that are only ground.
        public static PixelImage Object(TileType type) => type switch
        {
            TileType.Workbench => Workbench,
            TileType.Furnace => Furnace,
            TileType.Chest => Chest,
            TileType.Drill => Drill,
            TileType.Smelter => Smelter,
            TileType.Conveyor => ConveyorFrame,
            TileType.Torch => Torch,
            _ => null,
        };

        // ---- Floors ----

        static readonly Rgba FloorBase = Rgba.Hex("#352B24");
        static readonly Rgba FloorDark = Rgba.Hex("#2E251F");
        static readonly Rgba FloorLight = Rgba.Hex("#3C3129");
        static readonly Rgba Pebble = Rgba.Hex("#4A3D33");
        static readonly Rgba PebbleLight = Rgba.Hex("#5C4D40");
        static readonly Rgba PebbleShadow = Rgba.Hex("#241C17");

        static PixelImage Floor(int mask, int variant)
        {
            var image = new PixelImage(Size, Size);
            int salt = 100 + variant;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float n = Smooth(x, y, 4, salt, 100);
                    image[x, y] = n < 0.3f ? FloorDark : n > 0.72f ? FloorLight : FloorBase;
                }
            }
            int pebbles = (int)(PixelNoise.Hash(variant, 1, salt) % 3);
            for (int i = 0; i < pebbles; i++)
            {
                int px = 1 + (int)(PixelNoise.Hash(i, 2, salt) % 13);
                int py = 1 + (int)(PixelNoise.Hash(i, 3, salt) % 13);
                bool big = PixelNoise.Hash(i, 4, salt) % 3 == 0;
                image[px, py] = PebbleLight;
                image[px + 1, py] = Pebble;
                if (big)
                {
                    image[px, py + 1] = Pebble;
                    image[px + 1, py + 1] = Pebble;
                    image[px + 2, py + 1] = PebbleShadow;
                    image[px, py + 2] = PebbleShadow;
                    image[px + 1, py + 2] = PebbleShadow;
                }
                else
                {
                    image[px, py + 1] = PebbleShadow;
                    image[px + 1, py + 1] = PebbleShadow;
                }
            }
            if (variant == 3) Crack(image, salt, FloorDark.Scale(0.8f));
            CastShadow(image, mask);
            return image;
        }

        static readonly Rgba Slab = Rgba.Hex("#4A302D");
        static readonly Rgba SlabAlt = Rgba.Hex("#432B29");
        static readonly Rgba SlabLight = Rgba.Hex("#5B3C37");
        static readonly Rgba Mortar = Rgba.Hex("#26181A");

        // The boss arena: old red flagstones, one to a tile, so it reads as a made place and not a cave.
        static PixelImage ArenaFloor(int mask, int variant)
        {
            var image = new PixelImage(Size, Size);
            int salt = 200 + variant;
            var stone = variant % 2 == 0 ? Slab : SlabAlt;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var color = stone;
                    if (x == 0 || y == 0) color = Mortar;
                    else if (x == 1 || y == 1) color = SlabLight;
                    else if (x == Size - 1 || y == Size - 1) color = stone.Scale(0.85f);
                    else if (Smooth(x, y, 4, salt, 200) > 0.7f) color = stone.Scale(0.93f);
                    image[x, y] = color;
                }
            }
            if (variant >= 2) Crack(image, salt, Mortar);
            CastShadow(image, mask);
            return image;
        }

        // Rock casts a soft shadow onto the floor below it, and a little to the sides.
        static void CastShadow(PixelImage image, int mask)
        {
            if ((mask & Up) != 0)
            {
                for (int x = 0; x < Size; x++)
                {
                    image.Shade(x, 0, 0.5f);
                    image.Shade(x, 1, 0.66f);
                    image.Shade(x, 2, 0.82f);
                }
            }
            for (int y = 0; y < Size; y++)
            {
                if ((mask & Left) != 0) image.Shade(0, y, 0.8f);
                if ((mask & Right) != 0) image.Shade(Size - 1, y, 0.8f);
            }
        }

        static void Crack(PixelImage image, int salt, Rgba color)
        {
            int x = 3 + (int)(PixelNoise.Hash(0, 9, salt) % 9);
            int y = 3 + (int)(PixelNoise.Hash(1, 9, salt) % 9);
            for (int i = 0; i < 5; i++)
            {
                image[x, y] = color;
                if (PixelNoise.Hash(i, 10, salt) % 2 == 0) x++;
                else y++;
                if (i == 2) x--;
            }
        }

        // ---- Rock ----

        sealed class RockPalette
        {
            public Rgba Top;
            public Rgba TopDark;
            public Rgba TopLight;
            public Rgba Rim;
            public Rgba FaceTop;
            public Rgba FaceBottom;
            public Rgba Strata;
            public Rgba FaceSpeck;
            public Rgba Contact;
            public Rgba Outline;
        }

        static readonly RockPalette RockColors = new RockPalette
        {
            Top = Rgba.Hex("#5C4D41"),
            TopDark = Rgba.Hex("#514337"),
            TopLight = Rgba.Hex("#68584A"),
            Rim = Rgba.Hex("#9C8670"),
            FaceTop = Rgba.Hex("#8A7562"),
            FaceBottom = Rgba.Hex("#65554A"),
            Strata = Rgba.Hex("#56483D"),
            FaceSpeck = Rgba.Hex("#9A846E"),
            Contact = Rgba.Hex("#2C241E"),
            Outline = Rgba.Hex("#1B1511"),
        };

        static readonly RockPalette BedrockColors = new RockPalette
        {
            Top = Rgba.Hex("#2A2830"),
            TopDark = Rgba.Hex("#232129"),
            TopLight = Rgba.Hex("#33303A"),
            Rim = Rgba.Hex("#5B5769"),
            FaceTop = Rgba.Hex("#4A4654"),
            FaceBottom = Rgba.Hex("#383541"),
            Strata = Rgba.Hex("#2E2C36"),
            FaceSpeck = Rgba.Hex("#5A5666"),
            Contact = Rgba.Hex("#1A1920"),
            Outline = Rgba.Hex("#100F14"),
        };

        // The top of the rock, and where the tile below is open, a front face with layered strata. Sides that
        // face open ground get a dark outline and a lit rim.
        static PixelImage Rock(int mask, int variant, RockPalette colors)
        {
            var image = new PixelImage(Size, Size);
            int salt = 300 + variant;
            bool openUp = (mask & Up) == 0;
            bool openDown = (mask & Down) == 0;
            bool openLeft = (mask & Left) == 0;
            bool openRight = (mask & Right) == 0;
            int top = openDown ? Size - FaceHeight : Size;

            for (int y = 0; y < top; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float n = Smooth(x, y, 4, salt, 300);
                    image[x, y] = n < 0.3f ? colors.TopDark : n > 0.74f ? colors.TopLight : colors.Top;
                }
            }
            // Chips: a lit pixel over a dark one.
            for (int i = 0; i < 3; i++)
            {
                int x = 2 + (int)(PixelNoise.Hash(i, 5, salt) % 12);
                int y = 2 + (int)(PixelNoise.Hash(i, 6, salt) % 10);
                if (y + 1 >= top) continue;
                image[x, y] = colors.TopLight;
                image[x, y + 1] = colors.TopDark;
            }

            if (openDown)
            {
                for (int y = top; y < Size; y++)
                {
                    float t = (y - top) / (float)(FaceHeight - 1);
                    var row = colors.FaceTop.Mix(colors.FaceBottom, t);
                    for (int x = 0; x < Size; x++)
                    {
                        var color = row;
                        if ((y - top) % 2 == 0 && y > top && Smooth(x, y, 4, salt + 50, 350) > 0.38f) color = colors.Strata;
                        else if (PixelNoise.Hash(x, y, salt + 60) % 9 == 0) color = colors.FaceSpeck;
                        image[x, y] = color;
                    }
                }
                for (int x = 0; x < Size; x++)
                {
                    image[x, top] = colors.Rim;
                    image[x, Size - 1] = colors.Contact;
                }
            }

            if (openUp)
            {
                for (int x = 0; x < Size; x++)
                {
                    image[x, 0] = colors.Outline;
                    if (top > 1) image[x, 1] = colors.Rim;
                }
            }
            for (int y = 0; y < Size; y++)
            {
                bool onTop = y < top && y != top;
                if (openLeft)
                {
                    image[0, y] = colors.Outline;
                    if (onTop && !(openUp && y == 0)) image[1, y] = colors.Rim;
                }
                if (openRight)
                {
                    image[Size - 1, y] = colors.Outline;
                    if (onTop && !(openUp && y <= 1)) image.Shade(Size - 2, y, 0.82f);
                }
            }
            return image;
        }

        // Ore: chunks of the ore's colour set into the rock.
        static PixelImage Ore(PixelImage rock, int mask, int variant, Ramp ore, bool sparkle = false)
        {
            int salt = 400 + variant;
            int top = (mask & Down) == 0 ? Size - FaceHeight : Size;
            var spots = new List<(int X, int Y)>();
            for (int i = 0; spots.Count < 3 && i < 12; i++)
            {
                int x = 2 + (int)(PixelNoise.Hash(i, 7, salt) % 10);
                int y = 2 + (int)(PixelNoise.Hash(i, 8, salt) % 10);
                bool clear = true;
                foreach (var (sx, sy) in spots)
                {
                    if (System.Math.Abs(sx - x) < 5 && System.Math.Abs(sy - y) < 4) clear = false;
                }
                if (clear) spots.Add((x, y));
            }
            foreach (var (x, y) in spots)
            {
                // A chunk four pixels across set in a dark socket; on the face it catches more light.
                float light = y + 2 >= top ? 1.08f : 1f;
                foreach (var (dx, dy) in Socket) rock[x + dx, y + dy] = ore.Outline.Mix(rock[x + dx, y + dy], 0.35f);
                rock[x + 1, y] = ore.Light.Scale(light);
                rock[x + 2, y] = ore.Main.Scale(light);
                rock[x, y + 1] = ore.Light.Scale(light);
                rock[x + 1, y + 1] = ore.Main.Scale(light);
                rock[x + 2, y + 1] = ore.Main.Scale(light);
                rock[x + 3, y + 1] = ore.Shade;
                rock[x, y + 2] = ore.Shade;
                rock[x + 1, y + 2] = ore.Shade;
                rock[x + 2, y + 2] = ore.Dark;
            }
            if (sparkle && spots.Count > 0)
            {
                var (x, y) = spots[variant % spots.Count];
                rock[x + 1, y] = Rgba.White;
            }
            return rock;
        }

        static readonly (int X, int Y)[] Socket = { (0, 0), (3, 0), (-1, 1), (4, 1), (-1, 2), (3, 2), (0, 3), (1, 3), (2, 3) };

        // Value noise in cells of the given size, tiling every 16 pixels. The lattice points on a tile's edges come
        // from the shared salt and the rest from the variant's, so every variant of a kind meets every other at its
        // edges without a seam.
        static float Smooth(int x, int y, int cell, int salt, int shared)
        {
            int cells = Size / cell;
            int cx = x / cell;
            int cy = y / cell;
            float fx = (x % cell + 0.5f) / cell;
            float fy = (y % cell + 0.5f) / cell;
            float Lattice(int lx, int ly) => PixelNoise.Value(lx % cells, ly % cells, lx % cells == 0 || ly % cells == 0 ? shared : salt);
            float a = Lattice(cx, cy);
            float b = Lattice(cx + 1, cy);
            float c = Lattice(cx, cy + 1);
            float d = Lattice(cx + 1, cy + 1);
            float top = a + (b - a) * fx;
            float bottom = c + (d - c) * fx;
            return top + (bottom - top) * fy;
        }

        // ---- Placed things ----

        static readonly Rgba Shadow = new Rgba(0, 0, 0, 90);

        static readonly Dictionary<char, Rgba> WoodPalette = new Dictionary<char, Rgba>
        {
            ['o'] = Rgba.Hex("#22160E"),
            ['W'] = Rgba.Hex("#C9925A"),
            ['w'] = Rgba.Hex("#A57040"),
            ['v'] = Rgba.Hex("#80522C"),
            ['V'] = Rgba.Hex("#5C3A1E"),
            ['g'] = Rgba.Hex("#B4BAC2"),
            ['G'] = Rgba.Hex("#6C727C"),
            ['h'] = Rgba.Hex("#6E4426"),
            ['y'] = Rgba.Hex("#F0C858"),
            ['Y'] = Rgba.Hex("#C08A2A"),
            ['k'] = Rgba.Hex("#3A2A10"),
            ['s'] = Shadow,
        };

        // A table with a hammer and a saw on it.
        static readonly PixelImage Workbench = PixelImage.FromRows(WoodPalette,
            "................",
            "................",
            "................",
            ".oooooooooooooo.",
            "oWWWWWWWWWWWWWWo",
            "oWwwgGGwwwwwwwwo",
            "owwwwhhhhwwgggwo",
            "owwwwwwwwwwggggo",
            "oWwwwwwwwwwwwwwo",
            "ovvvvvvvvvvvvvvo",
            "oVVvvVVVVVVvvVVo",
            "oVhVvVVVVVVvVhVo",
            "ooVVoooooooooVVo",
            ".oVVo.......oVVo",
            ".ooooo.....ooooo",
            "..ssssssssssss..");

        // A wooden chest with gold bands and a lock.
        static readonly PixelImage Chest = PixelImage.FromRows(WoodPalette,
            "................",
            "................",
            "................",
            "................",
            "..oooooooooooo..",
            ".oWWyWWWWWWyWWo.",
            ".owwywwwwwwywwo.",
            ".owwywwwwwwywwo.",
            ".oYYYYYooYYYYYo.",
            ".ovvYvvoyyovYvvo",
            ".ovvYvvokkovYvvo",
            ".oVVYVVVooVVYVVo",
            ".oVVYVVVVVVVYVVo",
            "..oooooooooooo..",
            "..ssssssssssss..",
            "................");

        static readonly Dictionary<char, Rgba> StonePalette = new Dictionary<char, Rgba>
        {
            ['o'] = Rgba.Hex("#16161C"),
            ['S'] = Rgba.Hex("#9A9AA4"),
            ['s'] = Rgba.Hex("#787884"),
            ['d'] = Rgba.Hex("#585864"),
            ['D'] = Rgba.Hex("#42424C"),
            ['k'] = Rgba.Hex("#1E1414"),
            ['r'] = Rgba.Hex("#B03A1A"),
            ['f'] = Rgba.Hex("#F08A30"),
            ['F'] = Rgba.Hex("#FFD860"),
            ['c'] = Rgba.Hex("#C56A3C"),
            ['C'] = Rgba.Hex("#E89A5C"),
            ['b'] = Rgba.Hex("#8A4E3A"),
            ['B'] = Rgba.Hex("#6A382A"),
            ['m'] = Rgba.Hex("#3C2620"),
            ['_'] = Shadow,
        };

        // A stone furnace with fire in its mouth.
        static readonly PixelImage Furnace = PixelImage.FromRows(StonePalette,
            "................",
            "................",
            ".....oooooo.....",
            "...ooSSSSSSoo...",
            "..oSSsSSSSsSSo..",
            ".oSsssSsssSsssdo",
            ".osSssssssssssdo",
            ".oSdddddddddddSo",
            ".osdokkkkkkodsdo",
            ".osdkrrfrrrkdsdo",
            ".osdkrfFFfrkdsdo",
            ".oddkfFFFFfkddDo",
            ".oDDoooooooooDDo",
            ".oDDDDDDDDDDDDDo",
            "..oooooooooooooo",
            "..______________");

        // A brick smelter with a molten window, bigger and hotter than a furnace.
        static readonly PixelImage Smelter = PixelImage.FromRows(StonePalette,
            "................",
            "..oooooooooooo..",
            ".oCCcCCCCcCCCCo.",
            ".occccccccccccbo",
            ".ocCCcCCCCcCCcbo",
            ".obbbbbbbbbbbbbo",
            ".obBbbBbbbBbbBBo",
            ".obbBbbokoobBbBo",
            ".oBbbbokfFkobbBo",
            ".obBbBofFFfkBbBo",
            ".oBbbbokffkobbBo",
            ".obBbbBookobBbBo",
            ".oBBBBBBBBBBBBBo",
            ".ommmmmmmmmmmmmo",
            "..ooooooooooooo.",
            "..______________");

        static readonly Dictionary<char, Rgba> TorchPalette = new Dictionary<char, Rgba>
        {
            ['o'] = Rgba.Hex("#1A120C"),
            ['h'] = Rgba.Hex("#9A6A3C"),
            ['H'] = Rgba.Hex("#6A4424"),
            ['g'] = Rgba.Hex("#9A9AA4"),
            ['G'] = Rgba.Hex("#62626E"),
            ['r'] = Rgba.Hex("#D8502A"),
            ['f'] = Rgba.Hex("#FF9A30"),
            ['F'] = Rgba.Hex("#FFD860"),
            ['w'] = Rgba.Hex("#FFF6D0"),
            ['_'] = Shadow,
        };

        // A post with a stone cup of burning slime gel. Its light comes from tiles.csv, not from the drawing.
        static readonly PixelImage Torch = PixelImage.FromRows(TorchPalette,
            "................",
            ".......r........",
            "......rfr.......",
            ".....rfFfr......",
            ".....fFwFf......",
            ".....fFwwF......",
            "....ogGGGgo.....",
            ".....oGGGo......",
            "......hH........",
            "......hH........",
            "......hH........",
            "......hH........",
            "......hH........",
            ".....ohHo.......",
            ".....oooo.......",
            "....______......");

        static readonly Dictionary<char, Rgba> MetalPalette = new Dictionary<char, Rgba>
        {
            ['o'] = Rgba.Hex("#14161C"),
            ['L'] = Rgba.Hex("#C8D0DC"),
            ['M'] = Rgba.Hex("#96A0B0"),
            ['m'] = Rgba.Hex("#6E7888"),
            ['d'] = Rgba.Hex("#4C5462"),
            ['D'] = Rgba.Hex("#353B46"),
            ['y'] = Rgba.Hex("#F2C440"),
            ['k'] = Rgba.Hex("#22252C"),
            ['r'] = Rgba.Hex("#E05040"),
            ['g'] = Rgba.Hex("#7CD070"),
            ['b'] = Rgba.Hex("#2A2C34"),
            ['B'] = Rgba.Hex("#3E414C"),
            ['_'] = Shadow,
        };

        // A steel drill housing with a hazard stripe; the bit is drawn on top, facing the way the drill faces.
        static readonly PixelImage Drill = PixelImage.FromRows(MetalPalette,
            "................",
            "................",
            "..oooooooooooo..",
            ".oLLLLLLLLLLLLo.",
            ".oLMMMMMMMMMMmo.",
            ".oLMMkkkkkkMMmo.",
            ".oLMkDmmmmDkMmo.",
            ".oLMkmMddMmkMmo.",
            ".oLMkDmmmmDkMmo.",
            ".ommmmmmmmmmmmo.",
            ".oykykykykykyko.",
            ".odddddgdddrddo.",
            ".oDDDDDDDDDDDDo.",
            "..oooooooooooo..",
            "..____________..",
            "................");

        // The conveyor's frame: rails on both sides. The belt is drawn on top, running the way it faces.
        static readonly PixelImage ConveyorFrame = PixelImage.FromRows(MetalPalette,
            "................",
            ".oooooooooooooo.",
            ".omMMMMMMMMMMmo.",
            ".obbbbbbbbbbbbo.",
            ".obbbbbbbbbbbbo.",
            ".obbbbbbbbbbbbo.",
            ".obbbbbbbbbbbbo.",
            ".obbbbbbbbbbbbo.",
            ".obbbbbbbbbbbbo.",
            ".obbbbbbbbbbbbo.",
            ".obbbbbbbbbbbbo.",
            ".obbbbbbbbbbbbo.",
            ".obbbbbbbbbbbbo.",
            ".odmmmmmmmmmmdo.",
            ".oooooooooooooo.",
            "................");
    }
}
