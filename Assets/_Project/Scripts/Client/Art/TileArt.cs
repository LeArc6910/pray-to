using System.Collections.Generic;
using System.Linq;
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
        MithrilOre,
        ObsidianOre,
        Lava,
        VoidOre,
        Water,
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
        const int Kinds = 11;
        // Every kind is drawn once for each biome, in that biome's colours (Docs/Biomes.md).
        public static readonly int Biomes = System.Enum.GetValues(typeof(Biome)).Length;
        // Neighbour bits: which sides are rock.
        public const int Up = 1;
        public const int Right = 2;
        public const int Down = 4;
        public const int Left = 8;
        const int FaceHeight = 6;

        public static int GroundCount => Biomes * Kinds * Masks * Variants;

        // The arena floor and bedrock look the same everywhere, so they always use the plain cave's.
        public static int Index(GroundKind kind, int mask, int variant, Biome biome = Biome.Cave)
        {
            if (kind == GroundKind.Arena || kind == GroundKind.Bedrock) biome = Biome.Cave;
            return (((int)biome * Kinds + (int)kind) * Masks + mask) * Variants + variant;
        }

        public static bool IsRock(TileType type) =>
            type == TileType.Wall || type == TileType.CopperOre || type == TileType.IronOre || type == TileType.MithrilOre
            || type == TileType.ObsidianOre || type == TileType.VoidOre;

        // Which ground tile goes at (x, y), in the colours of its biome. Outside the map reads as wall.
        public static int GroundAt(TileMap map, int x, int y, bool inArena, Biome biome = Biome.Cave)
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
                TileType.MithrilOre => GroundKind.MithrilOre,
                TileType.ObsidianOre => GroundKind.ObsidianOre,
                TileType.VoidOre => GroundKind.VoidOre,
                TileType.Lava => GroundKind.Lava,
                TileType.Water => GroundKind.Water,
                _ => inArena ? GroundKind.Arena : GroundKind.Floor,
            };
            // Water's edges are where it meets anything else, like lava's.
            if (kind == GroundKind.Water)
            {
                mask = (map[x, y + 1] != TileType.Water ? Up : 0)
                    | (map[x + 1, y] != TileType.Water ? Right : 0)
                    | (map[x, y - 1] != TileType.Water ? Down : 0)
                    | (map[x - 1, y] != TileType.Water ? Left : 0);
            }
            // Lava's edges are where it meets anything that is not lava.
            if (kind == GroundKind.Lava)
            {
                mask = (map[x, y + 1] != TileType.Lava ? Up : 0)
                    | (map[x + 1, y] != TileType.Lava ? Right : 0)
                    | (map[x, y - 1] != TileType.Lava ? Down : 0)
                    | (map[x - 1, y] != TileType.Lava ? Left : 0);
            }
            return Index(kind, mask, (int)(PixelNoise.Hash(x, y, 7) % Variants), biome);
        }

        public static PixelImage Ground(int index)
        {
            var biome = (Biome)(index / (Kinds * Masks * Variants));
            var kind = (GroundKind)(index / (Masks * Variants) % Kinds);
            int mask = index / Variants % Masks;
            int variant = index % Variants;
            // Kinds that look the same in every biome are only ever asked for as the plain cave's; the other
            // biomes' slots get a blank.
            if (biome != Biome.Cave && (kind == GroundKind.Arena || kind == GroundKind.Bedrock)) return new PixelImage(Size, Size);
            var rock = RockOf(biome);
            return kind switch
            {
                GroundKind.Floor => Floor(mask, variant, FloorOf(biome), biome),
                GroundKind.Arena => ArenaFloor(mask, variant),
                GroundKind.Rock => Decorated(Rock(mask, variant, rock), mask, variant, biome),
                GroundKind.Bedrock => Rock(mask, variant, BedrockColors),
                GroundKind.CopperOre => Ore(Rock(mask, variant, rock), mask, variant, Ramp.Of("#D6783F")),
                GroundKind.MithrilOre => Ore(Rock(mask, variant, rock), mask, variant, Ramp.Of("#7CD2E2"), sparkle: true),
                GroundKind.ObsidianOre => Ore(Rock(mask, variant, rock), mask, variant, Ramp.Of("#4A3268"), sparkle: true),
                GroundKind.Lava => Lava(mask, variant),
                GroundKind.VoidOre => Ore(Rock(mask, variant, rock), mask, variant, Ramp.Of("#B48CFF"), sparkle: true),
                GroundKind.Water => Water(mask, variant, biome),
                _ => Ore(Rock(mask, variant, rock), mask, variant, Ramp.Of("#A9BDD6"), sparkle: true),
            };
        }

        // ---- Water ----

        // A pool: deep water with lighter ripples, a dark lip where it meets the ground, coloured by its biome.
        static PixelImage Water(int mask, int variant, Biome biome)
        {
            var (deep, main, light) = biome switch
            {
                Biome.Ice => ("#2E6A8C", "#4E9CC4", "#A8E0F4"),
                Biome.Mushroom => ("#1E4E5A", "#2E7A84", "#7CC8C0"),
                Biome.Crystal => ("#22386E", "#3A5CA8", "#9AC0F0"),
                Biome.Abyss => ("#1A1438", "#2E2460", "#8A74D8"),
                _ => ("#1C3A5C", "#2C5C8C", "#7CA8D8"),
            };
            var deepColor = Rgba.Hex(deep);
            var mainColor = Rgba.Hex(main);
            var lightColor = Rgba.Hex(light);
            var image = new PixelImage(Size, Size);
            int salt = 900 + variant;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float n = Smooth(x, y, 4, salt, 900);
                    image[x, y] = n > 0.8f ? lightColor : n > 0.35f ? mainColor : deepColor;
                }
            }
            var lip = deepColor.Scale(0.6f);
            for (int i = 0; i < Size; i++)
            {
                if ((mask & Up) != 0) image[i, 0] = lip;
                if ((mask & Down) != 0) image[i, Size - 1] = lip;
                if ((mask & Left) != 0) image[0, i] = lip;
                if ((mask & Right) != 0) image[Size - 1, i] = lip;
            }
            return image;
        }

        // ---- Lava ----

        static readonly Rgba LavaHot = Rgba.Hex("#FFD24A");
        static readonly Rgba LavaMain = Rgba.Hex("#FF7A1E");
        static readonly Rgba LavaDeep = Rgba.Hex("#D8401A");
        static readonly Rgba LavaCrust = Rgba.Hex("#5A2416");
        static readonly Rgba LavaCrustDark = Rgba.Hex("#2E120C");

        // Molten rock: bright and wavy in the middle, cooling to a dark crust where it meets the ground.
        static PixelImage Lava(int mask, int variant)
        {
            var image = new PixelImage(Size, Size);
            int salt = 700 + variant;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float n = Smooth(x, y, 4, salt, 700);
                    image[x, y] = n > 0.78f ? LavaHot : n > 0.42f ? LavaMain : n > 0.2f ? LavaDeep : LavaCrust;
                }
            }
            for (int i = 0; i < Size; i++)
            {
                if ((mask & Up) != 0) Crust(image, i, 0, i, 1);
                if ((mask & Down) != 0) Crust(image, i, Size - 1, i, Size - 2);
                if ((mask & Left) != 0) Crust(image, 0, i, 1, i);
                if ((mask & Right) != 0) Crust(image, Size - 1, i, Size - 2, i);
            }
            return image;
        }

        static void Crust(PixelImage image, int x, int y, int innerX, int innerY)
        {
            image[x, y] = LavaCrustDark;
            image[innerX, innerY] = LavaCrust;
        }

        // ---- Biomes ----

        sealed class FloorPalette
        {
            public Rgba Base;
            public Rgba Dark;
            public Rgba Light;
            public Rgba Pebble;
            public Rgba PebbleLight;
            public Rgba PebbleShadow;
            // A biome's small things on the floor: mushrooms, frost, crystal chips, embers.
            public Rgba Decor;
            public Rgba DecorLight;
        }

        static readonly FloorPalette CaveFloor = new FloorPalette
        {
            Base = Rgba.Hex("#352B24"), Dark = Rgba.Hex("#2E251F"), Light = Rgba.Hex("#3C3129"),
            Pebble = Rgba.Hex("#4A3D33"), PebbleLight = Rgba.Hex("#5C4D40"), PebbleShadow = Rgba.Hex("#241C17"),
        };

        static readonly FloorPalette MushroomFloor = new FloorPalette
        {
            Base = Rgba.Hex("#2C2532"), Dark = Rgba.Hex("#251F2B"), Light = Rgba.Hex("#362D3D"),
            Pebble = Rgba.Hex("#43384A"), PebbleLight = Rgba.Hex("#54465C"), PebbleShadow = Rgba.Hex("#1C1722"),
            Decor = Rgba.Hex("#C86ED8"), DecorLight = Rgba.Hex("#7FF0D8"),
        };

        static readonly FloorPalette IceFloor = new FloorPalette
        {
            Base = Rgba.Hex("#2E3A46"), Dark = Rgba.Hex("#28323D"), Light = Rgba.Hex("#374552"),
            Pebble = Rgba.Hex("#4A5A68"), PebbleLight = Rgba.Hex("#6A7E8E"), PebbleShadow = Rgba.Hex("#1E262E"),
            Decor = Rgba.Hex("#A8D8F0"), DecorLight = Rgba.Hex("#EAF8FF"),
        };

        static readonly FloorPalette CrystalFloor = new FloorPalette
        {
            Base = Rgba.Hex("#262536"), Dark = Rgba.Hex("#201F2E"), Light = Rgba.Hex("#2E2D42"),
            Pebble = Rgba.Hex("#3C3A55"), PebbleLight = Rgba.Hex("#4C4A6A"), PebbleShadow = Rgba.Hex("#18172A"),
            Decor = Rgba.Hex("#7CD2E2"), DecorLight = Rgba.Hex("#E0FAFF"),
        };

        static readonly FloorPalette LavaFloor = new FloorPalette
        {
            Base = Rgba.Hex("#2A2220"), Dark = Rgba.Hex("#231C1A"), Light = Rgba.Hex("#332926"),
            Pebble = Rgba.Hex("#3E3230"), PebbleLight = Rgba.Hex("#4E3E3A"), PebbleShadow = Rgba.Hex("#181210"),
            Decor = Rgba.Hex("#E0541E"), DecorLight = Rgba.Hex("#FFC04A"),
        };

        static FloorPalette FloorOf(Biome biome) => biome switch
        {
            Biome.Mushroom => MushroomFloor,
            Biome.Ice => IceFloor,
            Biome.Crystal => CrystalFloor,
            Biome.Lava => LavaFloor,
            Biome.Abyss => AbyssFloor,
            _ => CaveFloor,
        };

        static RockPalette RockOf(Biome biome) => biome switch
        {
            Biome.Mushroom => MushroomRock,
            Biome.Ice => IceRock,
            Biome.Crystal => CrystalRock,
            Biome.Lava => LavaRock,
            Biome.Abyss => AbyssRock,
            _ => RockColors,
        };

        static readonly RockPalette MushroomRock = new RockPalette
        {
            Top = Rgba.Hex("#4E4458"), TopDark = Rgba.Hex("#453C4E"), TopLight = Rgba.Hex("#5A4F66"), Rim = Rgba.Hex("#8E7EA4"),
            FaceTop = Rgba.Hex("#7A6A8E"), FaceBottom = Rgba.Hex("#5A4E6A"), Strata = Rgba.Hex("#4C4258"), FaceSpeck = Rgba.Hex("#9A8AB0"),
            Contact = Rgba.Hex("#241E2C"), Outline = Rgba.Hex("#16121C"),
        };

        static readonly RockPalette IceRock = new RockPalette
        {
            Top = Rgba.Hex("#5E7488"), TopDark = Rgba.Hex("#546A7E"), TopLight = Rgba.Hex("#6E869A"), Rim = Rgba.Hex("#C8E4F4"),
            FaceTop = Rgba.Hex("#9CB8CC"), FaceBottom = Rgba.Hex("#7690A6"), Strata = Rgba.Hex("#6A8296"), FaceSpeck = Rgba.Hex("#D8F0FF"),
            Contact = Rgba.Hex("#22303C"), Outline = Rgba.Hex("#121A22"),
        };

        static readonly RockPalette CrystalRock = new RockPalette
        {
            Top = Rgba.Hex("#3E3C58"), TopDark = Rgba.Hex("#36344E"), TopLight = Rgba.Hex("#4A4866"), Rim = Rgba.Hex("#7E7AA8"),
            FaceTop = Rgba.Hex("#66628C"), FaceBottom = Rgba.Hex("#4C496C"), Strata = Rgba.Hex("#3E3B5A"), FaceSpeck = Rgba.Hex("#9AD8E8"),
            Contact = Rgba.Hex("#1C1A2C"), Outline = Rgba.Hex("#100F1A"),
        };

        // The abyss: near-black stone with a violet cast, and specks of starlight.
        static readonly FloorPalette AbyssFloor = new FloorPalette
        {
            Base = Rgba.Hex("#16121F"), Dark = Rgba.Hex("#120F19"), Light = Rgba.Hex("#1C1727"),
            Pebble = Rgba.Hex("#282036"), PebbleLight = Rgba.Hex("#362A48"), PebbleShadow = Rgba.Hex("#0B0910"),
            Decor = Rgba.Hex("#9A7CFF"), DecorLight = Rgba.Hex("#F0E8FF"),
        };

        static readonly RockPalette AbyssRock = new RockPalette
        {
            Top = Rgba.Hex("#2A2238"), TopDark = Rgba.Hex("#241D30"), TopLight = Rgba.Hex("#342A46"), Rim = Rgba.Hex("#6A54A0"),
            FaceTop = Rgba.Hex("#46386A"), FaceBottom = Rgba.Hex("#30264A"), Strata = Rgba.Hex("#5A3CA0"), FaceSpeck = Rgba.Hex("#C8B4FF"),
            Contact = Rgba.Hex("#0E0B14"), Outline = Rgba.Hex("#08060C"),
        };

        static readonly RockPalette LavaRock = new RockPalette
        {
            Top = Rgba.Hex("#3A3232"), TopDark = Rgba.Hex("#322A2A"), TopLight = Rgba.Hex("#463C3A"), Rim = Rgba.Hex("#7A5A4A"),
            FaceTop = Rgba.Hex("#5C4A44"), FaceBottom = Rgba.Hex("#403432"), Strata = Rgba.Hex("#9A3A1E"), FaceSpeck = Rgba.Hex("#FF8A3A"),
            Contact = Rgba.Hex("#1A1212"), Outline = Rgba.Hex("#100A0A"),
        };

        // A biome's touch on the top of its rock: caps of mushrooms growing on it, frost, crystal points, glowing
        // seams. Only on some variants, so walls are not all alike.
        static PixelImage Decorated(PixelImage rock, int mask, int variant, Biome biome)
        {
            if (biome == Biome.Cave || variant % 2 == 1) return rock;
            var floor = FloorOf(biome);
            int top = (mask & Down) == 0 ? Size - FaceHeight : Size;
            int salt = 500 + variant;
            int x = 3 + (int)(PixelNoise.Hash(variant, 11, salt) % 9);
            int y = 3 + (int)(PixelNoise.Hash(variant, 12, salt) % System.Math.Max(1, top - 6));
            switch (biome)
            {
                case Biome.Mushroom:
                    rock[x, y] = floor.Decor;
                    rock[x + 1, y] = floor.Decor;
                    rock[x + 2, y] = floor.Decor.Scale(0.8f);
                    rock[x + 1, y - 1] = floor.DecorLight;
                    rock[x + 1, y + 1] = Rgba.Hex("#E8DCC8");
                    break;
                case Biome.Ice:
                    rock[x, y] = floor.DecorLight;
                    rock[x + 1, y + 1] = floor.Decor;
                    rock[x + 2, y] = floor.DecorLight;
                    rock[x + 3, y + 1] = floor.Decor;
                    break;
                case Biome.Crystal:
                    rock[x + 1, y - 2] = floor.DecorLight;
                    rock[x + 1, y - 1] = floor.Decor;
                    rock[x, y] = floor.Decor.Scale(0.8f);
                    rock[x + 1, y] = floor.Decor;
                    rock[x + 2, y] = floor.Decor.Scale(0.7f);
                    break;
                case Biome.Lava:
                    for (int i = 0; i < 4; i++) rock[x + i, y + (i % 2)] = i % 2 == 0 ? floor.Decor : floor.DecorLight;
                    break;
                case Biome.Abyss:
                    // A star caught in the rock.
                    rock[x + 1, y] = floor.DecorLight;
                    rock[x, y] = floor.Decor.Scale(0.7f);
                    rock[x + 2, y] = floor.Decor.Scale(0.7f);
                    rock[x + 1, y - 1] = floor.Decor.Scale(0.7f);
                    rock[x + 1, y + 1] = floor.Decor.Scale(0.7f);
                    break;
            }
            return rock;
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
            TileType.Splitter => SplitterFrame,
            TileType.Arm => ArmBase,
            TileType.Torch => Torch,
            TileType.Lamp => Lamp,
            TileType.Bed => Bed,
            TileType.Sprinkler => SprinklerBase,
            TileType.Harvester => HarvesterBody,
            TileType.StoneFloor => StoneFloorTile,
            TileType.BrickFloor => BrickFloorTile,
            TileType.GlowFloor => GlowFloorTile,
            TileType.Door => DoorTile,
            TileType.Table => TableTile,
            TileType.Banner => BannerTile,
            TileType.Pot => PotTile,
            TileType.Soil => SoilTile,
            TileType.FertileSoil => FertileSoilTile,
            TileType.MushroomSoil => MushroomSoilTile,
            TileType.FrostSoil => FrostSoilTile,
            TileType.AshSoil => AshSoilTile,
            TileType.VoidSoil => VoidSoilTile,
            TileType.Filter => FilterFrame,
            TileType.Generator => GeneratorBody,
            TileType.Lever => LeverBase,
            TileType.Anvil => AnvilTile,
            TileType.RiftAltar => RiftAltarTile,
            TileType.Assembler => AssemblerTile,
            TileType.PowerPole => PowerPoleTile,
            TileType.MerchantStall => MerchantStallTile,
            TileType.FastConveyor => ConveyorFrame,
            TileType.UndergroundBelt => ConveyorFrame,
            _ => null,
        };

        // ---- Floors ----

        static PixelImage Floor(int mask, int variant, FloorPalette colors, Biome biome)
        {
            var image = new PixelImage(Size, Size);
            int salt = 100 + variant;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float n = Smooth(x, y, 4, salt, 100);
                    image[x, y] = n < 0.3f ? colors.Dark : n > 0.72f ? colors.Light : colors.Base;
                }
            }
            int pebbles = (int)(PixelNoise.Hash(variant, 1, salt) % 3);
            for (int i = 0; i < pebbles; i++)
            {
                int px = 1 + (int)(PixelNoise.Hash(i, 2, salt) % 13);
                int py = 1 + (int)(PixelNoise.Hash(i, 3, salt) % 13);
                bool big = PixelNoise.Hash(i, 4, salt) % 3 == 0;
                image[px, py] = colors.PebbleLight;
                image[px + 1, py] = colors.Pebble;
                if (big)
                {
                    image[px, py + 1] = colors.Pebble;
                    image[px + 1, py + 1] = colors.Pebble;
                    image[px + 2, py + 1] = colors.PebbleShadow;
                    image[px, py + 2] = colors.PebbleShadow;
                    image[px + 1, py + 2] = colors.PebbleShadow;
                }
                else
                {
                    image[px, py + 1] = colors.PebbleShadow;
                    image[px + 1, py + 1] = colors.PebbleShadow;
                }
            }
            if (variant == 3) Crack(image, salt, colors.Dark.Scale(0.8f));
            FloorDecor(image, variant, colors, biome);
            CastShadow(image, mask);
            return image;
        }

        // Small things on a biome's floor: a glowing mushroom, frost glints, a crystal chip, embers.
        static void FloorDecor(PixelImage image, int variant, FloorPalette colors, Biome biome)
        {
            if (biome == Biome.Cave) return;
            int salt = 600 + variant;
            int x = 3 + (int)(PixelNoise.Hash(variant, 13, salt) % 10);
            int y = 4 + (int)(PixelNoise.Hash(variant, 14, salt) % 9);
            switch (biome)
            {
                case Biome.Mushroom when variant != 1:
                    image[x, y - 1] = colors.Decor;
                    image[x + 1, y - 1] = colors.Decor;
                    image[x - 1, y - 1] = colors.Decor.Scale(0.75f);
                    image[x, y - 2] = colors.DecorLight;
                    image[x, y] = Rgba.Hex("#D8CCB8");
                    image[x, y + 1] = colors.PebbleShadow;
                    break;
                case Biome.Ice:
                    image[x, y] = colors.DecorLight;
                    image[x + 3, y + 2] = colors.Decor;
                    if (variant % 2 == 0) image[x - 2, y + 3] = colors.Decor;
                    break;
                case Biome.Crystal when variant != 2:
                    image[x, y - 1] = colors.DecorLight;
                    image[x, y] = colors.Decor;
                    image[x + 1, y] = colors.Decor.Scale(0.7f);
                    break;
                case Biome.Lava:
                    image[x, y] = colors.Decor;
                    if (variant % 2 == 0) image[x + 1, y] = colors.DecorLight;
                    image[x - 3, y + 2] = colors.Decor.Scale(0.6f);
                    break;
                case Biome.Abyss:
                    // Motes of light on the floor, one bright.
                    image[x, y] = variant % 2 == 0 ? colors.DecorLight : colors.Decor;
                    image[x - 3, y + 2] = colors.Decor.Scale(0.6f);
                    if (variant == 3) image[x + 3, y - 2] = colors.Decor.Scale(0.8f);
                    break;
            }
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

        // A bed of slime-gel mattress on a stone frame, with a pillow and a blanket.
        static readonly PixelImage Bed = PixelImage.FromRows(new Dictionary<char, Rgba>
            {
                ['o'] = Rgba.Hex("#1A1410"),
                ['s'] = Rgba.Hex("#8A7562"),
                ['S'] = Rgba.Hex("#5C4D41"),
                ['w'] = Rgba.Hex("#F0ECE0"),
                ['W'] = Rgba.Hex("#C8C0B0"),
                ['b'] = Rgba.Hex("#4A7AC8"),
                ['B'] = Rgba.Hex("#2E5498"),
                ['L'] = Rgba.Hex("#7AA8E8"),
                ['_'] = Shadow,
            },
            "................",
            ".oooooooooooooo.",
            ".osSSSSSSSSSSso.",
            ".oswwwwwWWwwwso.",
            ".oswwwwwwWwwWso.",
            ".osWWWWWWWWWWso.",
            ".osLLLLLLLLLLso.",
            ".osbbbbbbbbbbso.",
            ".osbbBbbbbBbbso.",
            ".osbbbbbbbbbbso.",
            ".osbBbbbbBbbbso.",
            ".osBBBBBBBBBBso.",
            ".osSSSSSSSSSSso.",
            ".oSo........oSo.",
            ".ooo........ooo.",
            "..__________....");

        // A lantern of glowing spores from the mushroom forest on an iron post.
        static readonly PixelImage Lamp = PixelImage.FromRows(new Dictionary<char, Rgba>
            {
                ['o'] = Rgba.Hex("#141018"),
                ['g'] = Rgba.Hex("#9A9AA4"),
                ['G'] = Rgba.Hex("#62626E"),
                ['c'] = Rgba.Hex("#5FE0C8"),
                ['C'] = Rgba.Hex("#2E9E8C"),
                ['w'] = Rgba.Hex("#E8FFF8"),
                ['_'] = Shadow,
            },
            "................",
            "......oooo......",
            ".....oGggGo.....",
            ".....ocwwco.....",
            ".....ocwcCo.....",
            ".....ocCcCo.....",
            ".....oGggGo.....",
            "......oGGo......",
            ".......gG.......",
            ".......gG.......",
            ".......gG.......",
            ".......gG.......",
            "......ogGo......",
            ".....oGGGGo.....",
            ".....oooooo.....",
            "....________....");

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

        // A splitter: a low steel plate with a hub in the middle. Its arrows are drawn on top, turned the way it faces.
        static readonly PixelImage SplitterFrame = PixelImage.FromRows(MetalPalette,
            "................",
            ".oooooooooooooo.",
            ".omMMMMMMMMMMmo.",
            ".obbbbbbbbbbbbo.",
            ".obBbbbbbbbbBbo.",
            ".obbbbbbbbbbbbo.",
            ".obbbbkkkkbbbbo.",
            ".obbbkMMMmkbbbo.",
            ".obbbkMLMmkbbbo.",
            ".obbbkmMmdkbbbo.",
            ".obbbbkkkkbbbbo.",
            ".obbbbbbbbbbbbo.",
            ".obBbbbbbbbbBbo.",
            ".odmmmmmmmmmmdo.",
            ".oooooooooooooo.",
            "................");

        // A robot arm's base: a round steel pedestal with a hazard ring. The arm is drawn on top and swings.
        static readonly PixelImage ArmBase = PixelImage.FromRows(MetalPalette,
            "................",
            "................",
            ".....oooooo.....",
            "...ooLLLLLLoo...",
            "..oLLMMMMMMMmo..",
            "..oLMykykykMmo..",
            ".oLMkDDdDDDDkMo.",
            ".oLMyDdDDDdDyMo.",
            ".oLMkDDDDdDDkmo.",
            ".oLMyDdDDDDdymo.",
            "..oMkykykykymo..",
            "..ommMMMMMmmdo..",
            "...oodddddddo...",
            ".....oooooo.....",
            "....________....",
            "................");

        static readonly Dictionary<char, Rgba> FarmMetalPalette = new Dictionary<char, Rgba>(MetalPalette)
        {
            ['w'] = Rgba.Hex("#3E7FD0"),
            ['W'] = Rgba.Hex("#8CC4F4"),
            ['E'] = Rgba.Hex("#9AD46A"),
            ['G'] = Rgba.Hex("#5E9A3E"),
            ['e'] = Rgba.Hex("#3A6428"),
        };

        // A sprinkler: a round water tank on a short steel stand. Its spinning head is drawn on top.
        static readonly PixelImage SprinklerBase = PixelImage.FromRows(FarmMetalPalette,
            "................",
            "................",
            "................",
            ".....oooooo.....",
            "....oWWWWWWo....",
            "...oWwwwwwwwo...",
            "...oWwwwwwwdo...",
            "...owwwwwwwdo...",
            "...owwwwwwwdo...",
            "...odwwwwwddo...",
            "....oddddddo....",
            ".....oLMMmo.....",
            "....oLMMMMmo....",
            "....odddddDo....",
            "....oooooooo....",
            "....________....");

        // A harvester: a green steel box with a window and a hazard stripe. Its reel is drawn on top, at the side it
        // faces.
        static readonly PixelImage HarvesterBody = PixelImage.FromRows(FarmMetalPalette,
            "................",
            "................",
            "..oooooooooooo..",
            ".oEEEEEEEEEEEEo.",
            ".oEGGGGGGGGGGeo.",
            ".oEGkkkkkkkkGeo.",
            ".oEGkDDDDDDkGeo.",
            ".oEGkDLLMDDkGeo.",
            ".oEGkDDDDDDkGeo.",
            ".oeeeeeeeeeeeeo.",
            ".oykykykykykyko.",
            ".odddddgdddrddo.",
            ".oDDDDDDDDDDDDo.",
            "..oooooooooooo..",
            "..____________..",
            "................");

        // A filter: a splitter's plate with a mesh over it. Its arrows and chosen material are drawn on top.
        static readonly PixelImage FilterFrame = PixelImage.FromRows(MetalPalette,
            "................",
            ".oooooooooooooo.",
            ".omMMMMMMMMMMmo.",
            ".obbbbbbbbbbbbo.",
            ".obkbkbkbkbkbbo.",
            ".obbkbkbkbkbkbo.",
            ".obkbkbkbkbkbbo.",
            ".obbkbkbkbkbkbo.",
            ".obkbkbkbkbkbbo.",
            ".obbkbkbkbkbkbo.",
            ".obkbkbkbkbkbbo.",
            ".obbkbkbkbkbkbo.",
            ".obbbbbbbbbbbbo.",
            ".odmmmmmmmmmmdo.",
            ".oooooooooooooo.",
            "................");

        // A generator: a steel box with a firebox behind a grate; its flame is drawn on top while it burns.
        static readonly PixelImage GeneratorBody = PixelImage.FromRows(MetalPalette,
            "................",
            "..oooooooooooo..",
            ".oLLLLLLLLLLLLo.",
            ".oLMMMMMMMMMMmo.",
            ".oLMkkkkkkkkMmo.",
            ".oLMkbbbbbbkMmo.",
            ".oLMkbbbbbbkMmo.",
            ".oLMkbbbbbbkMmo.",
            ".oLMkkkkkkkkMmo.",
            ".oLMMMMMMMMMMmo.",
            ".oykykykykykyko.",
            ".oDdddrdddgdddo.",
            ".oDDDDDDDDDDDDo.",
            "..oooooooooooo..",
            "..____________..",
            "................");

        // A lever's base; the handle is drawn on top, leaning one way when on and the other when off.
        static readonly PixelImage LeverBase = PixelImage.FromRows(MetalPalette,
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "....oooooooo....",
            "...oLLLLLLLLo...",
            "...oLMMMMMMmo...",
            "...oMkkkkkkmo...",
            "...oddddddddo...",
            "....oooooooo....",
            "....________....",
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

        // ---- Cracks ----

        public const int CrackStages = 4;

        // Each crack line, from where it starts to where it ends, and the stage it shows from (1 to CrackStages): a
        // small star where the pick bites, then longer cracks with more branches, reaching the edges at the last
        // stage. Coordinates count from the top left.
        static readonly (int Stage, int X0, int Y0, int X1, int Y1)[] CrackLines =
        {
            (1, 8, 8, 6, 10), (1, 8, 8, 10, 6),
            (2, 6, 10, 4, 11), (2, 10, 6, 11, 4), (2, 8, 8, 9, 11),
            (3, 4, 11, 2, 13), (3, 11, 4, 13, 3), (3, 9, 11, 10, 13), (3, 8, 8, 5, 6), (3, 5, 6, 4, 4),
            (4, 2, 13, 1, 15), (4, 13, 3, 15, 2), (4, 10, 13, 12, 15), (4, 4, 4, 3, 1), (4, 5, 6, 2, 7), (4, 10, 6, 13, 8),
            (4, 13, 8, 14, 11),
        };

        static readonly Rgba CrackDark = Rgba.Hex("#140F0C").WithAlpha(235);
        static readonly Rgba CrackEdge = Rgba.Hex("#D2C4B4").WithAlpha(110);

        // The cracks drawn over a rock tile that has taken some hits, more with each stage (1 to CrackStages). Each
        // crack pixel is dark with a faint light pixel below it, so it reads as cut into the rock.
        public static PixelImage Cracks(int stage)
        {
            var image = new PixelImage(Size, Size);
            var dark = new List<(int X, int Y)>();
            foreach (var (from, x0, y0, x1, y1) in CrackLines)
            {
                if (from > stage) continue;
                int steps = System.Math.Max(System.Math.Abs(x1 - x0), System.Math.Abs(y1 - y0));
                for (int i = 0; i <= steps; i++)
                {
                    int x = x0 + (int)System.MathF.Round((x1 - x0) * (float)i / System.Math.Max(1, steps));
                    int y = y0 + (int)System.MathF.Round((y1 - y0) * (float)i / System.Math.Max(1, steps));
                    if (x >= 0 && y >= 0 && x < Size && y < Size) dark.Add((x, y));
                }
            }
            foreach (var (x, y) in dark)
            {
                if (y + 1 < Size && !dark.Contains((x, y + 1))) image[x, y + 1] = CrackEdge;
            }
            foreach (var (x, y) in dark) image[x, y] = CrackDark;
            return image;
        }
            // ---- Base building (Docs/Crafting.md, 기지 꾸미기) ----

        // Laid floors: slabs with grout lines, drawn edge to edge so a floor reads as one surface.
        static PixelImage Slabs(string main, string light, string shade, string grout, bool glow = false) => PixelImage.FromRows(
            new Dictionary<char, Rgba>
            {
                ['M'] = Rgba.Hex(main), ['L'] = Rgba.Hex(light), ['m'] = Rgba.Hex(shade), ['d'] = Rgba.Hex(grout), ['g'] = Rgba.Hex("#8FE8D8"),
            },
            glow ? GlowSlabRows : SlabRows);

        static readonly string[] SlabRows =
        {
            "MMMMMMMdMMMMMMMd",
            "MLLMMMMdMLMMMMMd",
            "MLMMMMMdMMMMMmMd",
            "MMMMMmMdMMMMMMMd",
            "MMMMMMMdMMMmMMMd",
            "MMmMMMMdMMMMMMMd",
            "MMMMMMMdMMMMMMMd",
            "dddddddddddddddd",
            "MMMdMMMMMMMdMMMM",
            "MLMdMMMMLMMdMMMM",
            "MMMdMMMMMMMdMMmM",
            "MMMdMmMMMMMdMMMM",
            "MMMdMMMMMMMdMMMM",
            "MMmdMMMMMMMdMLMM",
            "MMMdMMMMMMMdMMMM",
            "dddddddddddddddd",
        };

        static readonly string[] GlowSlabRows =
        {
            "MMMMMMMdMMMMMMMd",
            "MLLMMMMdMLMMMMMd",
            "MLMMMgMdMMMMMmMd",
            "MMMMMmMdMMMMMMMd",
            "MMMMMMMdMMMmMMMd",
            "MMmMMMMdMMgMMMMd",
            "MMMMMMMdMMMMMMMd",
            "dddddddddddddddd",
            "MMMdMMMMMMMdMMMM",
            "MLMdMMMMLMMdMMMM",
            "MgMdMMMMMMMdMMmM",
            "MMMdMmMMMMMdMMMM",
            "MMMdMMMMMMMdMMMM",
            "MMmdMMMMMgMdMLMM",
            "MMMdMMMMMMMdMMMM",
            "dddddddddddddddd",
        };

        // Turned soil: ridges and furrows across the tile; fertile soil is darker, with flecks of what was worked in.
        static readonly string[] SoilRows =
        {
            "LdLLLdLLLdLLLdLL",
            "MMmMMMmMMMmMMMmM",
            "dddddddddddddddd",
            "LLdLLLdLLLdLLLdL",
            "MmMMMmMMMmMMMmMM",
            "dddddddddddddddd",
            "LdLLLdLLLdLLLdLL",
            "MMmMMMmMMMmMMMmM",
            "dddddddddddddddd",
            "LLdLLLdLLLdLLLdL",
            "MmMMMmMMMmMMMmMM",
            "dddddddddddddddd",
            "LdLLLdLLLdLLLdLL",
            "MMmMMMmMMMmMMMmM",
            "dddddddddddddddd",
            "LLLdLLLdLLLdLLLd",
        };

        static readonly PixelImage SoilTile = PixelImage.FromRows(new Dictionary<char, Rgba>
            {
                ['L'] = Rgba.Hex("#6E5038"), ['M'] = Rgba.Hex("#5A402C"), ['m'] = Rgba.Hex("#4C3624"), ['d'] = Rgba.Hex("#3A2818"),
            },
            SoilRows);

        static readonly PixelImage FertileSoilTile = PixelImage.FromRows(new Dictionary<char, Rgba>
            {
                ['L'] = Rgba.Hex("#4E3A2A"), ['M'] = Rgba.Hex("#40301F"), ['m'] = Rgba.Hex("#8AB04A"), ['d'] = Rgba.Hex("#2A1E14"),
            },
            SoilRows);

        // Soil from the other biomes, in their colours, with flecks of what makes each (Docs/Time.md).
        static PixelImage BiomeSoil(string ridge, string furrow, string fleck, string deep) => PixelImage.FromRows(new Dictionary<char, Rgba>
            {
                ['L'] = Rgba.Hex(ridge), ['M'] = Rgba.Hex(furrow), ['m'] = Rgba.Hex(fleck), ['d'] = Rgba.Hex(deep),
            },
            SoilRows);

        static readonly PixelImage MushroomSoilTile = BiomeSoil("#5E4A70", "#4C3A5E", "#6FE8D0", "#30243E");
        static readonly PixelImage FrostSoilTile = BiomeSoil("#6A7E92", "#56687C", "#E6F4FF", "#36465A");
        static readonly PixelImage AshSoilTile = BiomeSoil("#4A4444", "#3A3434", "#FF8A3C", "#221E1E");
        static readonly PixelImage VoidSoilTile = BiomeSoil("#3E3254", "#30264A", "#B48CFF", "#1C1630");

        static readonly PixelImage StoneFloorTile = Slabs("#7E7468", "#948A7C", "#6A6156", "#4E4740");
        static readonly PixelImage BrickFloorTile = Slabs("#A0563E", "#B86A50", "#8A4834", "#5A2E22");
        static readonly PixelImage GlowFloorTile = Slabs("#5E6A6E", "#727F84", "#4E585C", "#363E42", glow: true);

        static readonly Dictionary<char, Rgba> BasePalette = new Dictionary<char, Rgba>
        {
            ['o'] = Rgba.Hex("#1A1410"),
            ['S'] = Rgba.Hex("#8A7562"),
            ['w'] = Rgba.Hex("#5C4D41"),
            ['W'] = Rgba.Hex("#7A6656"),
            ['k'] = Rgba.Hex("#3A3E46"),
            ['y'] = Rgba.Hex("#F2C440"),
            ['L'] = Rgba.Hex("#A89C8C"),
            ['M'] = Rgba.Hex("#8E8274"),
            ['m'] = Rgba.Hex("#76695C"),
            ['d'] = Rgba.Hex("#5A4F45"),
            ['H'] = Rgba.Hex("#9A8C7C"),
            ['h'] = Rgba.Hex("#6A5E52"),
            ['r'] = Rgba.Hex("#A8323A"),
            ['R'] = Rgba.Hex("#D2464E"),
            ['C'] = Rgba.Hex("#E8A866"),
            ['c'] = Rgba.Hex("#F6E2C0"),
            ['P'] = Rgba.Hex("#B8664A"),
            ['p'] = Rgba.Hex("#8A4A34"),
            ['_'] = Shadow,
        };

        // A door: a stone frame and an iron-banded slab. Players walk through it; monsters cannot.
        static readonly PixelImage DoorTile = PixelImage.FromRows(BasePalette,
            "................",
            "..oooooooooooo..",
            ".oSSSSSSSSSSSSo.",
            ".oSwwwwwwwwwwSo.",
            ".oSwWWWWWWWWwSo.",
            ".oSwkkkkkkkkwSo.",
            ".oSwWWWWWWWWwSo.",
            ".oSwWWWWWWyWwSo.",
            ".oSwWWWWWWyWwSo.",
            ".oSwWWWWWWWWwSo.",
            ".oSwkkkkkkkkwSo.",
            ".oSwWWWWWWWWwSo.",
            ".oSwWWWWWWWWwSo.",
            ".oSwwwwwwwwwwSo.",
            ".oSSSSSSSSSSSSo.",
            "..____________..");

        static readonly PixelImage TableTile = PixelImage.FromRows(BasePalette,
            "................",
            "................",
            "................",
            "..oooooooooooo..",
            ".oLLLLLLLLLLLLo.",
            ".oLMMMMMMMMMMmo.",
            ".oMMMMMMMMMMMmo.",
            ".ommmmmmmmmmmdo.",
            ".oddddddddddddo.",
            "..oMmo....oMmo..",
            "..oMmo....oMmo..",
            "..oMmo....oMmo..",
            "..oMmo....oMmo..",
            "..oddo....oddo..",
            "..oooo....oooo..",
            "..____....____..");

        // A dark altar with a violet crack hanging over it.
        static readonly PixelImage RiftAltarTile = PixelImage.FromRows(new Dictionary<char, Rgba>(BasePalette)
            {
                ['V'] = Rgba.Hex("#E2C8FF"), ['v'] = Rgba.Hex("#B48CFF"), ['u'] = Rgba.Hex("#6A44B8"), ['A'] = Rgba.Hex("#4A4058"),
                ['a'] = Rgba.Hex("#332C3E"),
            },
            "......ooo.......",
            ".....ouvuo......",
            "....ouvVvuo.....",
            ".....ovVvo......",
            "......ovuo......",
            ".....ouvo.......",
            "......ouo.......",
            "...oooooooooo...",
            "..oAAAAAAAAAAo..",
            "..oAaaAAAAaaAo..",
            "...oAAAAAAAAo...",
            "...oAaAvvAaAo...",
            "...oAAAAAAAAo...",
            "..oaaaaaaaaaao..",
            "..oooooooooooo..",
            "..____________..");

        // Static fields start in the order they are written, so this comes before the stall that uses it.
        static readonly string[] Counter =
        {
            "oooooooooooooooo",
            "oHHyHHHHHPHHyHHo",
            "oWWWWWWWWWWWWWWo",
            "owwwwwwwwwwwwwwo",
            "owWwwwWwwwwWwwwo",
            "oooooooooooooooo",
            "________________",
        };

        // The merchant's stall: a striped awning on two posts over a wooden counter with wares on it. The merchant
        // stands in the gap (WorldView draws them) and MerchantCounter goes over their legs.
        static readonly PixelImage MerchantStallTile = PixelImage.FromRows(BasePalette,
            "oooooooooooooooo",
            "oRRccRRccRRccRRo",
            "oRRccRRccRRccRRo",
            "orrCCrrCCrrCCrro",
            "oo.oo.oo.oo.oo.o",
            ".ow..........wo.",
            ".ow..........wo.",
            ".ow..........wo.",
            ".ow..........wo.",
            Counter[0],
            Counter[1],
            Counter[2],
            Counter[3],
            Counter[4],
            Counter[5],
            Counter[6]);

        // The stall's counter alone, drawn over the merchant standing behind it.
        public static readonly PixelImage MerchantCounter = PixelImage.FromRows(BasePalette,
            Enumerable.Repeat("................", Size - Counter.Length).Concat(Counter).ToArray());

        // A wooden pole with a crossbar and two green glass insulators on it.
        static readonly PixelImage PowerPoleTile = PixelImage.FromRows(new Dictionary<char, Rgba>(BasePalette)
            {
                ['g'] = Rgba.Hex("#8CE0A0"), ['G'] = Rgba.Hex("#4CA86A"),
            },
            "................",
            "..og........go..",
            ".oGgo......oGgo.",
            "oWWWWWWWWWWWWWWo",
            "owwwwwwSWwwwwwwo",
            ".oooooowWooooo..",
            "......owWo......",
            "......owWo......",
            "......owWo......",
            "......owWo......",
            "......owWo......",
            "......owWo......",
            ".....owwWWo.....",
            ".....oooooo.....",
            ".....______.....",
            "................");

        // An iron box with a brass gear on its face and a green lamp: it makes the recipe it was given.
        static readonly PixelImage AssemblerTile = PixelImage.FromRows(new Dictionary<char, Rgba>(BasePalette)
            {
                ['I'] = Rgba.Hex("#9AA0AA"), ['i'] = Rgba.Hex("#747A86"), ['j'] = Rgba.Hex("#525762"), ['G'] = Rgba.Hex("#E2B04A"),
                ['g'] = Rgba.Hex("#A87A2C"), ['e'] = Rgba.Hex("#7CE07C"),
            },
            "................",
            "................",
            "..oooooooooooo..",
            ".oIIIIIIIIIIIIo.",
            ".oIiiiiiiiiiijo.",
            ".oIiioGoGoiiijo.",
            ".oIioGGgGGoiijo.",
            ".oIiGGgogGGiijo.",
            ".oIioGgogGoiijo.",
            ".oIiioGGGoieijo.",
            ".oIiiiiiiiiiijo.",
            ".oIjjjjjjjjjjjo.",
            ".ooiiiiiiiiiioo.",
            ".oojjjjjjjjjjoo.",
            "..oooooooooooo..",
            "..____________..");

        // An iron anvil on a stump: the horn to the left, the face lit along its top.
        static readonly PixelImage AnvilTile = PixelImage.FromRows(new Dictionary<char, Rgba>(BasePalette)
            {
                ['I'] = Rgba.Hex("#B8BCC4"), ['i'] = Rgba.Hex("#8A8E98"), ['j'] = Rgba.Hex("#5E626C"), ['b'] = Rgba.Hex("#7A5236"),
                ['B'] = Rgba.Hex("#9A6A46"),
            },
            "................",
            "................",
            "................",
            "................",
            "..oooooooooooo..",
            ".oIIIIIIIIIIIIo.",
            "oIiiiiiiiiiiiijo",
            ".oojiiiiiiijjoo.",
            "...ojiiiiijjo...",
            "....ojjjjjjo....",
            "...ooooooooooo..",
            "...oBBBBBBBBbo..",
            "...oBbBBBBBBbo..",
            "...obbbbbbbbbo..",
            "...ooooooooooo..",
            "...___________..");

        static readonly PixelImage BannerTile = PixelImage.FromRows(BasePalette,
            "....oo..........",
            "...oyyo.........",
            "....oo..........",
            "....oHoooooooo..",
            "....oHorrrrrrro.",
            "....oHorRRRRRro.",
            "....oHorRyyRRro.",
            "....oHorRyyRRro.",
            "....oHorRRRRRro.",
            "....oHorrrrrrro.",
            "....oHoorrorroo.",
            "....oHo.oo.oo...",
            "....oHo.........",
            "...oHHho........",
            "..oooooooo......",
            "..________......");

        static readonly PixelImage PotTile = PixelImage.FromRows(BasePalette,
            "................",
            "......oooo......",
            "....ooCCCCoo....",
            "...oCCcCCCCCo...",
            "...oCCCCCcCCo...",
            "....ooowwooo....",
            "......owwo......",
            "......owwo......",
            "...oooooooooo...",
            "...oPPPPPPPPo...",
            "...oPpPPPPPPo...",
            "....oPPPPPPo....",
            "....oPPPPPPo....",
            "....oppppppo....",
            ".....oooooo.....",
            "....________....");
    }
}
