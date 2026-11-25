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
        // The arena floors past the eighth (ArenaIndex): an arena's floor is drawn once for each kind of arena, in the
        // slots a kind's biomes would take.
        ArenaMore,
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
        const int Kinds = 12;
        // Every kind is drawn once for each biome, in that biome's colours (Docs/Biomes.md).
        public static readonly int Biomes = System.Enum.GetValues(typeof(Biome)).Length;
        // Neighbour bits: which sides are rock.
        public const int Up = 1;
        public const int Right = 2;
        public const int Down = 4;
        public const int Left = 8;
        public const int FaceHeight = 6;

        public static int GroundCount => Biomes * Kinds * Masks * Variants;

        // The arena floor and bedrock look the same everywhere, so they always use the plain cave's.
        public static int Index(GroundKind kind, int mask, int variant, Biome biome = Biome.Cave)
        {
            if (kind == GroundKind.Arena || kind == GroundKind.ArenaMore || kind == GroundKind.Bedrock) biome = Biome.Cave;
            return (((int)biome * Kinds + (int)kind) * Masks + mask) * Variants + variant;
        }

        // The floor of an arena of a kind (2026-11-17, Docs/Bosses.md 아레나 컨셉): each kind has its own stone. The
        // kinds take the slots the biomes would (an arena's floor never changed with its biome).
        public static int ArenaIndex(ArenaKind arena, int mask, int variant)
        {
            int style = (int)arena;
            var kind = style < Biomes ? GroundKind.Arena : GroundKind.ArenaMore;
            return ((style % Biomes * Kinds + (int)kind) * Masks + mask) * Variants + variant;
        }

        public static bool IsRock(TileType type) =>
            type == TileType.Wall || type == TileType.CopperOre || type == TileType.IronOre || type == TileType.MithrilOre
            || type == TileType.ObsidianOre || type == TileType.VoidOre || TileMap.IsRoomWall(type);

        // Rounded cave corners (Docs/ArtPipeline.md 둥근 모서리, 2026-11-11): a square corner is cut or filled by a
        // three-pixel step. Where rock sticks out into open floor (its two sides and the diagonal open) the corner is
        // cut back to the floor beside it; where floor goes into rock (rock on both sides of a corner) it is filled with
        // the rock beside it. Drawn as a layer over the ground, from the ground tile beside, so the step carries that
        // tile's colours and texture.
        public const int CornerStep = 3;
        // The four corners as (way across, way up): top-left, top-right, bottom-left, bottom-right.
        public static readonly (int X, int Y)[] CornerWays = { (-1, 1), (1, 1), (-1, -1), (1, -1) };

        // Whether the corner of the tile at (x, y) that lies (sx, sy) of it rounds. Outside the map reads as rock.
        public static bool Rounds(TileMap map, int x, int y, int sx, int sy)
        {
            bool rock = IsRock(map[x, y]);
            bool side = IsRock(map[x + sx, y]), upDown = IsRock(map[x, y + sy]), diagonal = IsRock(map[x + sx, y + sy]);
            return rock ? !side && !upDown && !diagonal : side && upDown;
        }

        // A tile-sized layer with, for each corner given a ground tile (by index, -1 for none), the step taken from
        // that tile as it shows across the corner's side edge.
        public static PixelImage CornerOverlay(int[] sources)
        {
            var image = new PixelImage(Size, Size);
            for (int c = 0; c < CornerWays.Length; c++)
            {
                if (sources[c] < 0) continue;
                var from = Ground(sources[c]);
                var (sx, sy) = CornerWays[c];
                for (int i = 0; i < CornerStep; i++)
                {
                    for (int j = 0; j < CornerStep - i; j++)
                    {
                        // Image rows run from the top.
                        int px = sx < 0 ? i : Size - 1 - i;
                        int py = sy > 0 ? j : Size - 1 - j;
                        image[px, py] = from[px, py];
                    }
                }
            }
            return image;
        }

        // Which ground tile goes at (x, y), in the colours of its biome. Outside the map reads as wall.
        public static int GroundAt(TileMap map, int x, int y, bool inArena, Biome biome = Biome.Cave, ArenaKind arena = ArenaKind.Plain)
        {
            int mask = (IsRock(map[x, y + 1]) ? Up : 0)
                | (IsRock(map[x + 1, y]) ? Right : 0)
                | (IsRock(map[x, y - 1]) ? Down : 0)
                | (IsRock(map[x - 1, y]) ? Left : 0);
            var kind = map[x, y] switch
            {
                TileType.Wall => map.IsEdge(x, y) ? GroundKind.Bedrock : GroundKind.Rock,
                // The rooms' own walls stand on rock; what makes each its own is drawn over it (RoomTileArt).
                TileType.CrackedWall or TileType.ObsidianWall or TileType.VoidWall => GroundKind.Rock,
                TileType.CopperOre => GroundKind.CopperOre,
                TileType.IronOre => GroundKind.IronOre,
                TileType.MithrilOre => GroundKind.MithrilOre,
                TileType.ObsidianOre => GroundKind.ObsidianOre,
                TileType.VoidOre => GroundKind.VoidOre,
                TileType.Lava => GroundKind.Lava,
                TileType.Water => GroundKind.Water,
                // An islet or a sunken boat stands in a pool (묶음 B3a): the pool's water runs under it.
                TileType.MossIsle or TileType.SunkenBoat => GroundKind.Water,
                TileType.StructurePart when CrystalLakeProps.Wet(map, x, y) => GroundKind.Water,
                _ => inArena ? GroundKind.Arena : GroundKind.Floor,
            };
            // Water's edges are where it meets anything else, like lava's.
            if (kind == GroundKind.Water)
            {
                mask = (!CrystalLakeProps.Wet(map, x, y + 1) ? Up : 0)
                    | (!CrystalLakeProps.Wet(map, x + 1, y) ? Right : 0)
                    | (!CrystalLakeProps.Wet(map, x, y - 1) ? Down : 0)
                    | (!CrystalLakeProps.Wet(map, x - 1, y) ? Left : 0);
            }
            // Lava's edges are where it meets anything that is not lava.
            if (kind == GroundKind.Lava)
            {
                mask = (map[x, y + 1] != TileType.Lava ? Up : 0)
                    | (map[x + 1, y] != TileType.Lava ? Right : 0)
                    | (map[x, y - 1] != TileType.Lava ? Down : 0)
                    | (map[x - 1, y] != TileType.Lava ? Left : 0);
            }
            if (kind == GroundKind.Arena) return ArenaIndex(arena, mask, (int)(PixelNoise.Hash(x, y, 7) % Variants));
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
            if (biome != Biome.Cave && kind == GroundKind.Bedrock) return new PixelImage(Size, Size);
            if (kind == GroundKind.Arena || kind == GroundKind.ArenaMore)
            {
                int style = (int)biome + (kind == GroundKind.ArenaMore ? Biomes : 0);
                return style < ArenaStyles.Length ? ArenaFloor(mask, variant, ArenaStyles[style]) : new PixelImage(Size, Size);
            }
            var rock = RockOf(biome);
            return kind switch
            {
                GroundKind.Floor => Floor(mask, variant, FloorOf(biome), biome),
                GroundKind.Rock => Decorated(Faced(Rock(mask, variant, rock), mask, variant, biome), mask, variant, biome),
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
                // The lake's own teal (4-1 지하 호수: #4FD2C0 for its lit ripples, kept dim so the banks' shallows are the
                // bright part).
                Biome.Lake => ("#12525C", "#1A6C76", "#2F8E94"),
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
            if (biome == Biome.Lake)
            {
                // The lake's pools shelve (묶음 B3a): where the water meets a bank it is shallow for three pixels, paler
                // and clearer, a line of foam on it here and there. Top-left origin: Up is row 0.
                var shallow = Rgba.Hex("#3AA8A4");
                var shelf = Rgba.Hex("#2A8A8C");
                lightColor = Rgba.Hex("#9AEADF");
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        int near = Size;
                        if ((mask & Up) != 0) near = System.Math.Min(near, y);
                        if ((mask & Down) != 0) near = System.Math.Min(near, Size - 1 - y);
                        if ((mask & Left) != 0) near = System.Math.Min(near, x);
                        if ((mask & Right) != 0) near = System.Math.Min(near, Size - 1 - x);
                        if (near == 1) image[x, y] = PixelNoise.Hash(x, y, salt + 9) % 6 == 0 ? lightColor : shallow;
                        else if (near == 2) image[x, y] = shallow;
                        else if (near == 3 && (x + y) % 2 == 0) image[x, y] = shelf;
                    }
                }
                lip = Rgba.Hex("#16282A");
            }
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
            // Warm packed earth (Docs/BiomeRework.md 4-1 흙 동굴, 묶음 B1a: #3A2B20, refined from #352B24).
            Base = Rgba.Hex("#3A2B20"), Dark = Rgba.Hex("#32251B"), Light = Rgba.Hex("#423226"),
            Pebble = Rgba.Hex("#4A382A"), PebbleLight = Rgba.Hex("#5A4636"), PebbleShadow = Rgba.Hex("#2A1E16"),
            // Fine roots threading the earth.
            Decor = Rgba.Hex("#4E3C2C"), DecorLight = Rgba.Hex("#5C4836"),
        };

        static readonly FloorPalette MushroomFloor = new FloorPalette
        {
            // Mossy olive earth with lime life (4-1 버섯 숲, 묶음 B1a: #24291C; it was purple, which is now the abyss's
            // alone). Light is the moss that patches it.
            Base = Rgba.Hex("#24291C"), Dark = Rgba.Hex("#1E2217"), Light = Rgba.Hex("#2F3A1E"),
            Pebble = Rgba.Hex("#343A26"), PebbleLight = Rgba.Hex("#434A30"), PebbleShadow = Rgba.Hex("#151810"),
            // White mycelium threads and a lime glint.
            Decor = Rgba.Hex("#8E927A"), DecorLight = Rgba.Hex("#B8F25A"),
        };

        // Smooth frosted blue-grey (4-1 얼음 동굴, 묶음 B2a: the base kept and leant a little toward #34485A; it was
        // #2E3A46 with white specks on every tile). Decor is the hairline of a crack, DecorLight a glint of frost.
        static readonly FloorPalette IceFloor = new FloorPalette
        {
            Base = Rgba.Hex("#2F3F4E"), Dark = Rgba.Hex("#293744"), Light = Rgba.Hex("#384A5A"),
            Pebble = Rgba.Hex("#465866"), PebbleLight = Rgba.Hex("#62788A"), PebbleShadow = Rgba.Hex("#1E2832"),
            Decor = Rgba.Hex("#6E94AE"), DecorLight = Rgba.Hex("#DCEFFA"),
        };

        // Flat blue-grey slate (4-1 수정 동굴, 묶음 B3a: a new palette, #2B2E38; it was the purple #262536, and purple is
        // now the abyss's alone). Decor is a chip of sky crystal, DecorLight its glint; the pink chips are CrystalPink.
        static readonly FloorPalette CrystalFloor = new FloorPalette
        {
            Base = Rgba.Hex("#2B2E38"), Dark = Rgba.Hex("#252832"), Light = Rgba.Hex("#323642"),
            Pebble = Rgba.Hex("#454A5C"), PebbleLight = Rgba.Hex("#5C6278"), PebbleShadow = Rgba.Hex("#191B24"),
            Decor = Rgba.Hex("#7FF2FF"), DecorLight = Rgba.Hex("#F4F0FF"),
        };

        static readonly Rgba CrystalPink = Rgba.Hex("#FF7FD0");

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
            Biome.Lake => LakeFloor,
            Biome.Sand => SandFloor,
            _ => CaveFloor,
        };

        static RockPalette RockOf(Biome biome) => biome switch
        {
            Biome.Mushroom => MushroomRock,
            Biome.Ice => IceRock,
            Biome.Crystal => CrystalRock,
            Biome.Lava => LavaRock,
            Biome.Abyss => AbyssRock,
            Biome.Lake => LakeRock,
            Biome.Sand => SandRock,
            _ => RockColors,
        };

        // Rotten wood and earth swollen round (4-1 버섯 숲: wall #3E3A2A, its top #565038 mossed).
        static readonly RockPalette MushroomRock = new RockPalette
        {
            Top = Rgba.Hex("#565038"), TopDark = Rgba.Hex("#4C4632"), TopLight = Rgba.Hex("#5E5A3C"), Rim = Rgba.Hex("#8E8658"),
            FaceTop = Rgba.Hex("#6A6040"), FaceBottom = Rgba.Hex("#3E3A2A"), Strata = Rgba.Hex("#34301F"), FaceSpeck = Rgba.Hex("#C8A070"),
            Contact = Rgba.Hex("#16180F"), Outline = Rgba.Hex("#0E100A"),
        };

        static readonly RockPalette IceRock = new RockPalette
        {
            Top = Rgba.Hex("#5E7488"), TopDark = Rgba.Hex("#546A7E"), TopLight = Rgba.Hex("#6E869A"), Rim = Rgba.Hex("#DCEFFA"),
            FaceTop = Rgba.Hex("#9CB8CC"), FaceBottom = Rgba.Hex("#7690A6"), Strata = Rgba.Hex("#6A8296"), FaceSpeck = Rgba.Hex("#D8F0FF"),
            Contact = Rgba.Hex("#22303C"), Outline = Rgba.Hex("#121A22"),
        };

        // Sharp slate (4-1 수정 동굴: wall #4B5060): a dark top, a paler cleft face, sky glints in it.
        static readonly RockPalette CrystalRock = new RockPalette
        {
            Top = Rgba.Hex("#3C4150"), TopDark = Rgba.Hex("#343846"), TopLight = Rgba.Hex("#474C5E"), Rim = Rgba.Hex("#8E94AC"),
            FaceTop = Rgba.Hex("#687088"), FaceBottom = Rgba.Hex("#4B5060"), Strata = Rgba.Hex("#3A3E4C"), FaceSpeck = Rgba.Hex("#9ADCEC"),
            Contact = Rgba.Hex("#181A22"), Outline = Rgba.Hex("#0E0F16"),
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

        // The sand caves (2026-11-09): tan drifts with wind ripples and the odd bleached bone, sandstone in warm layers.
        // Warm drifted sand (4-1 모래 동굴, 묶음 B2a: the base kept and warmed a step toward #8C6A44; it was #6A5236).
        // Decor is the crest of a wind ripple, DecorLight a bleached chip of bone.
        static readonly FloorPalette SandFloor = new FloorPalette
        {
            Base = Rgba.Hex("#72583A"), Dark = Rgba.Hex("#654C30"), Light = Rgba.Hex("#806444"),
            Pebble = Rgba.Hex("#8E724C"), PebbleLight = Rgba.Hex("#A6885C"), PebbleShadow = Rgba.Hex("#3E2E1C"),
            Decor = Rgba.Hex("#9A7C50"), DecorLight = Rgba.Hex("#E8DCC0"),
        };

        static readonly RockPalette SandRock = new RockPalette
        {
            Top = Rgba.Hex("#7A5E3E"), TopDark = Rgba.Hex("#6A5034"), TopLight = Rgba.Hex("#8A6C48"), Rim = Rgba.Hex("#C8A46C"),
            FaceTop = Rgba.Hex("#A07C50"), FaceBottom = Rgba.Hex("#7E5E3A"), Strata = Rgba.Hex("#C49656"), FaceSpeck = Rgba.Hex("#F0D49A"),
            Contact = Rgba.Hex("#2A1E12"), Outline = Rgba.Hex("#1C140C"),
        };

        // The underground lake: wet grey-green stone, glistening, with pale shells and weed at the water's edge.
        static readonly FloorPalette LakeFloor = new FloorPalette
        {
            // 4-1 지하 호수, 묶음 B3a: the base kept (#22383A, it was #22343A), its stones wet cobbles.
            Base = Rgba.Hex("#22383A"), Dark = Rgba.Hex("#1C3032"), Light = Rgba.Hex("#2A4446"),
            Pebble = Rgba.Hex("#36545A"), PebbleLight = Rgba.Hex("#4A6C70"), PebbleShadow = Rgba.Hex("#122022"),
            Decor = Rgba.Hex("#E8E0D0"), DecorLight = Rgba.Hex("#5AD2B4"),
        };

        static readonly RockPalette LakeRock = new RockPalette
        {
            // Wet limestone (4-1 지하 호수: wall #3A5650), smooth, moss on its top and runs down its face (묶음 B3a).
            Top = Rgba.Hex("#33504C"), TopDark = Rgba.Hex("#2C4642"), TopLight = Rgba.Hex("#3D5C56"), Rim = Rgba.Hex("#76B0A6"),
            FaceTop = Rgba.Hex("#56807A"), FaceBottom = Rgba.Hex("#3A5650"), Strata = Rgba.Hex("#48706A"), FaceSpeck = Rgba.Hex("#BFF4EC"),
            Contact = Rgba.Hex("#101A1C"), Outline = Rgba.Hex("#0A1214"),
        };

        static readonly RockPalette LavaRock = new RockPalette
        {
            Top = Rgba.Hex("#3A3232"), TopDark = Rgba.Hex("#322A2A"), TopLight = Rgba.Hex("#463C3A"), Rim = Rgba.Hex("#7A5A4A"),
            FaceTop = Rgba.Hex("#5C4A44"), FaceBottom = Rgba.Hex("#403432"), Strata = Rgba.Hex("#9A3A1E"), FaceSpeck = Rgba.Hex("#FF8A3A"),
            Contact = Rgba.Hex("#1A1212"), Outline = Rgba.Hex("#100A0A"),
        };

        // The ice cave's and the sand cave's rock (4-1 바닥과 벽, 묶음 B2a). Ice: angled facets catching the light on
        // its top, clear streaks down its face and icicles hanging off the face's lower edge. Sandstone: its face in
        // level bands of light and dark stone that waver a little, its top worn in streaks by the wind.
        static PixelImage Faced(PixelImage rock, int mask, int variant, Biome biome)
        {
            if (biome == Biome.Crystal || biome == Biome.Lake) return CrystalLakeFaced(rock, mask, variant, biome);
            if (biome != Biome.Ice && biome != Biome.Sand) return rock;
            bool openDown = (mask & Down) == 0, openLeft = (mask & Left) == 0, openRight = (mask & Right) == 0, openUp = (mask & Up) == 0;
            int top = openDown ? Size - FaceHeight : Size;
            var colors = RockOf(biome);
            int salt = 700 + variant;
            bool Edge(int x) => x == 0 && openLeft || x == Size - 1 && openRight;
            if (biome == Biome.Ice)
            {
                for (int i = 0; i < 2; i++)
                {
                    int x = 3 + (int)(PixelNoise.Hash(i, 21, salt) % 8), y = 3 + (int)(PixelNoise.Hash(i, 22, salt) % (uint)System.Math.Max(1, top - 9));
                    int length = 3 + (int)(PixelNoise.Hash(i, 23, salt) % 3);
                    for (int k = 0; k < length; k++)
                    {
                        if (y + k + 1 >= top - 1 || x + k >= Size - 2) break;
                        rock[x + k, y + k] = colors.TopLight.Mix(colors.Rim, 0.3f);
                        rock[x + k, y + k + 1] = colors.TopDark;
                    }
                }
                if (!openDown) return rock;
                for (int x = 0; x < Size; x++)
                {
                    if (Edge(x)) continue;
                    uint hash = PixelNoise.Hash(x, 24, salt) % 5;
                    // A clear streak down the face.
                    if (hash == 1)
                        for (int y = top + 1; y < Size - 1; y++) rock[x, y] = rock[x, y].Mix(colors.FaceSpeck, 0.45f);
                    // An icicle: the face runs down into the contact row, its tip bright.
                    if (hash == 0)
                    {
                        rock[x, Size - 2] = colors.FaceTop;
                        rock[x, Size - 1] = colors.FaceSpeck;
                    }
                    else if (hash == 3) rock[x, Size - 1] = colors.FaceBottom;
                }
                return rock;
            }
            // Sandstone: the wind's streaks along the top.
            for (int i = 0; i < 2; i++)
            {
                int x = 2 + (int)(PixelNoise.Hash(i, 25, salt) % 7), y = 3 + (int)(PixelNoise.Hash(i, 26, salt) % (uint)System.Math.Max(1, top - 7));
                int length = 4 + (int)(PixelNoise.Hash(i, 27, salt) % 4);
                for (int k = 0; k < length && x + k < Size - 2; k++)
                    if (y < top - 1) rock[x + k, y] = k == 0 || k == length - 1 ? colors.Top : colors.TopLight;
            }
            if (!openDown) return rock;
            var pale = colors.Strata.Mix(colors.FaceTop, 0.35f);
            var dark = colors.FaceBottom.Scale(0.86f);
            for (int x = 0; x < Size; x++)
            {
                if (Edge(x)) continue;
                int wobble = Smooth(x, 3, 6, salt, 360) > 0.62f ? 1 : 0;
                for (int y = top + 1; y < Size - 1; y++)
                {
                    int band = y - top + wobble;
                    rock[x, y] = band == 1 ? pale : band == 2 ? colors.FaceTop : band == 3 ? dark : band == 4 ? colors.FaceTop.Mix(colors.FaceBottom, 0.6f) : colors.FaceBottom;
                    if (band == 2 && PixelNoise.Hash(x, y, salt + 5) % 7 == 0) rock[x, y] = colors.FaceSpeck;
                }
            }
            return rock;
        }

        // The crystal cave's and the lake's rock (4-1 바닥과 벽, 묶음 B3a). Slate: cleavage lines slanting across its top,
        // its face split into sharp upright facets, a point of pink or sky crystal standing on its rim here and there.
        // Limestone: smooth, moss in patches along the front of its top, dark runs down its face with a bright drop at
        // the end of some.
        static PixelImage CrystalLakeFaced(PixelImage rock, int mask, int variant, Biome biome)
        {
            bool openDown = (mask & Down) == 0, openLeft = (mask & Left) == 0, openRight = (mask & Right) == 0;
            int top = openDown ? Size - FaceHeight : Size;
            var colors = RockOf(biome);
            int salt = 740 + variant;
            bool Edge(int x) => x == 0 && openLeft || x == Size - 1 && openRight;
            if (biome == Biome.Crystal)
            {
                // Cleavage: two thin slanted lines, light above dark.
                for (int i = 0; i < 2; i++)
                {
                    int x = 2 + (int)(PixelNoise.Hash(i, 31, salt) % 7), y = 3 + (int)(PixelNoise.Hash(i, 32, salt) % (uint)System.Math.Max(1, top - 8));
                    int length = 4 + (int)(PixelNoise.Hash(i, 33, salt) % 4);
                    for (int k = 0; k < length; k++)
                    {
                        int px = x + k, py = y + k / 2;
                        if (py + 1 >= top - 1 || px >= Size - 2) break;
                        rock[px, py] = colors.TopLight;
                        rock[px, py + 1] = colors.TopDark;
                    }
                }
                if (!openDown) return rock;
                // The face: upright facets, each a few pixels wide, alternately lit and shaded, their edges dark.
                int facet = 0, left = 0;
                for (int x = 0; x < Size; x++)
                {
                    if (x - left >= 3 + (int)(PixelNoise.Hash(facet, 34, salt) % 3))
                    {
                        facet++;
                        left = x;
                    }
                    if (Edge(x)) continue;
                    bool lit = facet % 2 == 0;
                    for (int y = top + 1; y < Size - 1; y++)
                    {
                        var color = lit ? colors.FaceTop.Mix(colors.FaceBottom, (y - top) / 8f) : colors.FaceBottom.Mix(colors.Strata, (y - top) / 7f);
                        if (x == left && x > 0) color = colors.Strata;
                        rock[x, y] = color;
                    }
                    if (lit && x == left + 1 && PixelNoise.Hash(facet, 35, salt) % 5 == 0) rock[x, top + 2] = colors.FaceSpeck.Mix(colors.FaceTop, 0.4f);
                }
                // A crystal's point on the rim: pink on some drawings, sky on others.
                if (variant == 3)
                {
                    int x = 3 + (int)(PixelNoise.Hash(mask, 36, salt) % 9);
                    var crystal = PixelNoise.Hash(mask, 40, salt) % 2 == 0 ? CrystalPink : CrystalFloor.Decor;
                    rock[x, top - 1] = crystal;
                    rock[x, top - 2] = crystal;
                    rock[x, top - 3] = CrystalFloor.DecorLight;
                    rock[x + 1, top - 1] = crystal.Scale(0.7f);
                    rock[x + 1, top] = crystal.Scale(0.7f);
                    rock[x, top] = crystal;
                    rock[x, top + 1] = crystal.Scale(0.7f);
                }
                return rock;
            }
            // Limestone: moss along the front of the top.
            var moss = Rgba.Hex("#5E7A3A");
            var mossLight = Rgba.Hex("#7A9440");
            for (int x = 1; x < Size - 1; x++)
            {
                float n = Smooth(x, 2, 5, salt, 370);
                if (n < 0.5f) continue;
                int deep = n > 0.72f ? 3 : 2;
                for (int k = 1; k <= deep; k++)
                {
                    int y = top - k;
                    if (y < 2) break;
                    rock[x, y] = k == deep ? moss.Scale(0.85f) : PixelNoise.Hash(x, y, salt + 3) % 4 == 0 ? mossLight : moss;
                }
                if (openDown && n > 0.72f) rock[x, top] = mossLight;
            }
            if (!openDown) return rock;
            // The face, smoothed, and the runs down it.
            for (int x = 0; x < Size; x++)
            {
                if (Edge(x)) continue;
                for (int y = top + 1; y < Size - 1; y++) rock[x, y] = colors.FaceTop.Mix(colors.FaceBottom, (y - top - 1) / 4f);
                uint hash = PixelNoise.Hash(x, 37, salt) % 6;
                if (hash == 0)
                {
                    int length = 2 + (int)(PixelNoise.Hash(x, 38, salt) % 3);
                    for (int k = 0; k < length; k++) rock[x, top + 1 + k] = colors.Strata.Scale(0.88f);
                    if (top + 1 + length < Size - 1 && PixelNoise.Hash(x, 39, salt) % 2 == 0) rock[x, top + 1 + length] = colors.FaceSpeck;
                }
                else if (hash == 3) rock[x, top + 1] = mossLight.Mix(colors.FaceTop, 0.4f);
            }
            return rock;
        }

        // A biome's touch on the top of its rock: caps of mushrooms growing on it, frost, crystal points, glowing
        // seams. Only on some variants, so walls are not all alike.
        static PixelImage Decorated(PixelImage rock, int mask, int variant, Biome biome)
        {
            if (biome == Biome.Cave || variant % 2 == 1) return rock;
            // The slate's tops are bare but for one drawing in four (묶음 B3a: a crystal on every other read as confetti).
            if (biome == Biome.Crystal && variant != 0) return rock;
            var floor = FloorOf(biome);
            int top = (mask & Down) == 0 ? Size - FaceHeight : Size;
            int salt = 500 + variant;
            int x = 3 + (int)(PixelNoise.Hash(variant, 11, salt) % 9);
            int y = 3 + (int)(PixelNoise.Hash(variant, 12, salt) % System.Math.Max(1, top - 6));
            switch (biome)
            {
                case Biome.Mushroom:
                    // A shelf of tan fungus with a lime spore on it (묶음 B1a; it was a purple cap).
                    rock[x, y] = Rgba.Hex("#E8B070");
                    rock[x + 1, y] = Rgba.Hex("#E8B070");
                    rock[x + 2, y] = Rgba.Hex("#A86A3A");
                    rock[x + 1, y - 1] = Rgba.Hex("#FFD49A");
                    rock[x + 1, y + 1] = Rgba.Hex("#6E4220");
                    if (variant % 4 == 0) rock[x + 3, y - 1] = floor.DecorLight;
                    break;
                case Biome.Ice:
                    rock[x, y] = floor.DecorLight;
                    rock[x + 1, y + 1] = floor.Decor;
                    rock[x + 2, y] = floor.DecorLight;
                    rock[x + 3, y + 1] = floor.Decor;
                    break;
                case Biome.Crystal:
                {
                    // A crystal grown on the rock's top, pink or sky by the drawing (묶음 B3a).
                    var crystal = PixelNoise.Hash(mask, 16, salt) % 2 == 0 ? CrystalPink : floor.Decor;
                    rock[x + 1, y - 2] = floor.DecorLight;
                    rock[x + 1, y - 1] = crystal;
                    rock[x, y] = crystal.Scale(0.8f);
                    rock[x + 1, y] = crystal;
                    rock[x + 2, y] = crystal.Scale(0.7f);
                    break;
                }
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
                case Biome.Lake:
                    // A small pool held in a hollow of the top, a glint on it (묶음 B3a; it was a bright line).
                    rock[x, y] = floor.DecorLight.Scale(0.55f);
                    rock[x + 1, y] = floor.DecorLight.Scale(0.7f);
                    rock[x + 2, y] = floor.DecorLight.Scale(0.55f);
                    rock[x + 1, y - 1] = floor.DecorLight.Scale(0.5f);
                    rock[x + 1, y + 1] = floor.Dark;
                    break;
                case Biome.Sand:
                    // Sand trickling from a crack: a light grain over a short fall.
                    rock[x + 1, y] = floor.Pebble;
                    rock[x + 1, y + 1] = floor.Decor;
                    rock[x + 1, y + 3] = floor.Decor.Scale(0.85f);
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
            TileType.LockedDoor => LockedDoorTile,
            TileType.Road => Road(15),
            TileType.Dock => DockTile,
            // The mines' rails, matched to their neighbours where they are laid (RailArt); alone, north and south.
            TileType.Rail => RailArt.Rail(1 | 4),
            // The patterns on the arenas' floors (PropArt.Arena.cs).
            TileType.ArenaInlay or TileType.RuneFloor or TileType.Cobweb or TileType.Petals or TileType.Fissure or TileType.ArenaCarpet => PropArt.ArenaFloor(type),
            // Under the harbour's boats (PropArt.Underlay): open water with no shore.
            TileType.Water => Ground(Index(GroundKind.Water, 0, 0)),
            TileType.Waypoint => WaypointTile,
            TileType.IronWall => IronWallTile,
            TileType.CrackedWall => RoomTileArt.Cracked,
            TileType.ObsidianWall => RoomTileArt.Obsidian,
            TileType.VoidWall => RoomTileArt.VoidStone,
            TileType.Grate => RoomTileArt.Grate,
            // A camp's trapdoor, shut and open (B1c).
            TileType.Trapdoor => RoomTileArt.Trapdoor,
            TileType.OpenTrapdoor => RoomTileArt.OpenTrapdoor,
            // Alone on the floor; in the world its edges follow its neighbours (RoomTileArt.Patch).
            TileType.IceFloor or TileType.Quicksand => RoomTileArt.Patch(type, 0),
            TileType.Fold => FoldTile,
            TileType.RedCarpet => RedCarpetTile,
            TileType.BlueCarpet => BlueCarpetTile,
            TileType.YellowCarpet => YellowCarpetTile,
            TileType.TapestryWall => TapestryWallTile,
            TileType.ShieldWall => ShieldWallTile,
            TileType.Scarecrow => ScarecrowTile,
            TileType.Fence => FenceTile,
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
            // The bag's icon for a big assembler; on the map it is drawn whole over its four tiles (BigAssembler).
            TileType.BigAssembler => AssemblerTile,
            TileType.BigSmelter => Smelter,
            TileType.PowerPole => PowerPoleTile,
            TileType.MerchantStall => MerchantStallTile,
            TileType.SmithStall => SmithStallTile,
            TileType.FisherHut => FisherHutTile,
            TileType.RoyalStall or TileType.DemonStall or TileType.OreStall or TileType.WandererStall => MerchantStallTile,
            TileType.AlchemyTable => AlchemyTableTile,
            TileType.Keg => KegTile,
            TileType.PreservesJar => JarTile,
            TileType.Coop => CoopTile,
            TileType.Barn => BarnTile,
            TileType.WardStone => WardStoneTile,
            TileType.Turret => TurretBase,
            TileType.CrabPot => CrabPotTile,
            TileType.FastConveyor => ConveyorFrame,
            TileType.UndergroundBelt => ConveyorFrame,
            TileType.ArcFurnace => ArcFurnaceTile,
            TileType.WaterWheel => WaterWheelStand,
            TileType.SolarPanel => SolarPanelTile,
            TileType.TeslaTower => TeslaTowerTile,
            TileType.Skylight => SkylightTile,
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
            // The ice cave's floor is smooth: a stone on one drawing in four (묶음 B2a).
            if (biome == Biome.Ice) pebbles = variant == 3 ? 1 : 0;
            // Slate lies in flat plates: a loose chip on one drawing (묶음 B3a).
            if (biome == Biome.Crystal) pebbles = variant == 1 ? 1 : 0;
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
            int salt = 600 + variant;
            int x = 3 + (int)(PixelNoise.Hash(variant, 13, salt) % 10);
            int y = 4 + (int)(PixelNoise.Hash(variant, 14, salt) % 9);
            switch (biome)
            {
                case Biome.Cave when variant == 2:
                    // A fine root across the packed earth (묶음 B1a): a thread a few pixels long with a fork.
                    for (int i = 0; i < 6; i++) image[x - 2 + i, y + (i > 2 ? 1 : 0) + (i == 5 ? 1 : 0)] = i % 3 == 1 ? colors.DecorLight : colors.Decor;
                    image[x + 1, y - 1] = colors.Decor;
                    image[x + 2, y - 2] = colors.Decor;
                    break;
                case Biome.Mushroom when variant != 1:
                    // A white thread of mycelium over the moss and a lime glint (묶음 B1a; it was a purple mushroom).
                    for (int i = 0; i < 7; i++) image[x - 3 + i, y + (i > 1 ? 1 : 0) - (i > 4 ? 1 : 0)] = colors.Decor.WithAlpha(255).Mix(colors.Base, i % 2 == 0 ? 0.35f : 0.6f);
                    image[x, y - 1] = colors.Decor.Mix(colors.Base, 0.6f);
                    if (variant == 0) image[x + 2, y - 2] = colors.DecorLight.Mix(colors.Base, 0.25f);
                    break;
                case Biome.Ice:
                    // A smooth floor (묶음 B2a; it had three pale specks on every tile): a hairline crack across one
                    // drawing, a forked one on another, a glint of frost on a third.
                    if (variant == 0)
                    {
                        for (int i = 0; i < 6; i++) image[x - 2 + i, y + i / 2] = i % 4 == 3 ? colors.Decor.Mix(colors.DecorLight, 0.3f) : colors.Decor.Mix(colors.Base, 0.35f);
                    }
                    else if (variant == 2)
                    {
                        for (int i = 0; i < 5; i++) image[x + i / 2, y - 2 + i] = colors.Decor.Mix(colors.Base, 0.4f);
                        image[x + 3, y] = colors.Decor.Mix(colors.Base, 0.4f);
                        image[x + 4, y - 1] = colors.Decor.Mix(colors.Base, 0.5f);
                    }
                    else if (variant == 1)
                    {
                        image[x, y] = colors.DecorLight;
                        image[x + 1, y] = colors.Decor;
                        image[x, y + 1] = colors.Decor.Mix(colors.Base, 0.5f);
                    }
                    break;
                case Biome.Crystal:
                    // Flat plates of slate (묶음 B3a): the joint between two of them stepping across the tile, and on
                    // some a chip of crystal lying in it, sky or pink.
                    if (variant != 1)
                    {
                        var joint = colors.Dark.Scale(0.82f);
                        int step = 4 + (int)(PixelNoise.Hash(variant, 15, salt) % 7);
                        for (int i = 0; i < Size; i++)
                        {
                            int jy = y + (i > step ? 2 : 0);
                            if (variant == 3 && i > step + 4) break;
                            image[i, jy] = joint;
                            if (jy > 0) image[i, jy - 1] = image[i, jy - 1].Mix(colors.Light, 0.5f);
                        }
                        image[step, y + 1] = joint;
                    }
                    if (variant == 1)
                    {
                        // Dim: the crystals that stand on the floor are the bright ones.
                        int cx = (x + 5) % 13 + 1, cy = (y + 6) % 11 + 2;
                        image[cx, cy] = colors.Decor.Mix(colors.Base, 0.45f);
                        image[cx + 1, cy] = CrystalPink.Mix(colors.Base, 0.55f);
                    }
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
                case Biome.Sand:
                    // A wind ripple across the drift, or a small bleached bone.
                    if (variant % 2 == 0)
                    {
                        // Two crests of a ripple (묶음 B2a: longer and wavier), each lit above and shaded below.
                        for (int i = -3; i <= 4; i++)
                        {
                            int wave = i == -1 || i == 0 ? -1 : i == 3 ? 1 : 0;
                            image[x + i, y + wave] = colors.Decor;
                            image[x + i, y + wave + 1] = colors.Dark;
                        }
                        for (int i = -1; i <= 3; i++)
                        {
                            image[x + i - 2, y + 4 + (i == 1 ? -1 : 0)] = colors.Decor.Mix(colors.Base, 0.4f);
                        }
                    }
                    else if (variant == 1)
                    {
                        // A chip of bone, dimmer than it was (the white dashes read as litter).
                        image[x - 1, y] = colors.DecorLight.Mix(colors.Base, 0.3f);
                        image[x, y] = colors.DecorLight.Mix(colors.Base, 0.45f);
                        image[x + 1, y + 1] = colors.Dark;
                    }
                    break;
                case Biome.Lake:
                    // Wet cobbles and shallow puddles (묶음 B3a; it was a shell or a wisp of weed on every tile): the
                    // round backs of two cobbles on one drawing, a puddle with a glint on another, weed on a third.
                    if (variant == 0)
                    {
                        foreach (var (cx, cy, w) in new[] { (x - 2, y, 4), (x + 3, y + 3, 3) })
                        {
                            for (int i = 0; i < w; i++)
                            {
                                image[cx + i, cy] = colors.Light;
                                image[cx + i, cy + 2] = colors.Dark.Scale(0.85f);
                            }
                            image[cx - 1, cy + 1] = colors.Light;
                            image[cx + w, cy + 1] = colors.Dark.Scale(0.85f);
                        }
                    }
                    else if (variant == 2)
                    {
                        var puddle = Rgba.Hex("#1E5E66");
                        for (int j = 0; j < 3; j++)
                            for (int i = (j == 1 ? -3 : -2); i <= (j == 1 ? 3 : 1); i++)
                                image[x + i, y + j] = j == 0 ? puddle.Scale(0.8f) : puddle;
                        image[x - 1, y + 1] = colors.DecorLight.Mix(puddle, 0.55f);
                    }
                    else if (variant == 3)
                    {
                        image[x, y] = colors.DecorLight.Mix(colors.Base, 0.6f);
                        image[x, y - 1] = colors.DecorLight.Mix(colors.Base, 0.7f);
                        image[x + 1, y - 2] = colors.DecorLight.Mix(colors.Base, 0.8f);
                    }
                    break;
            }
        }

        // An arena's floor: the stone's two tones, its lit edge and the mortar between; whether it is laid in slabs
        // (one to a tile) or is loose ground (sand, trodden earth); and the colour its cracks show.
        sealed class ArenaStyle
        {
            public Rgba Slab, SlabAlt, Light, Mortar, Crack;
            public bool Loose;
        }

        static ArenaStyle Style(string slab, string alt, string light, string mortar, bool loose = false, string crack = null) => new ArenaStyle
        {
            Slab = Rgba.Hex(slab), SlabAlt = Rgba.Hex(alt), Light = Rgba.Hex(light), Mortar = Rgba.Hex(mortar), Loose = loose,
            Crack = Rgba.Hex(crack ?? mortar),
        };

        // One for each ArenaKind, in its order (Docs/Bosses.md 아레나 컨셉).
        static readonly ArenaStyle[] ArenaStyles =
        {
            // Plain: the old red flagstones.
            Style("#4A302D", "#432B29", "#5B3C37", "#26181A"),
            // Ruin: old grey-green temple stone.
            Style("#4E5248", "#454A40", "#646A5A", "#2A2D27"),
            // Nest: stone dusted blue with crystal.
            Style("#3E4660", "#373F58", "#56618A", "#20263A"),
            // Caldera: black basalt, its cracks glowing.
            Style("#3A2C2A", "#322523", "#503A32", "#1C1414", crack: "#E8642A"),
            // Altar: violet-black void stone.
            Style("#322A48", "#2B2440", "#483C68", "#18142A", crack: "#8A6AE0"),
            // Flooded: wet green-blue stone.
            Style("#2E4A4E", "#284246", "#3E666A", "#162A2E"),
            // Pit: sand.
            Style("#8A7450", "#7E6A48", "#A48E66", "#64533A", loose: true),
            // Warcamp: trodden red earth.
            Style("#54392E", "#4B3329", "#684A3C", "#33221B", loose: true),
            // Chapel: pale blue-grey marble.
            Style("#46506A", "#3F4962", "#5E6C8C", "#262E44"),
            // Mine: packed earth and grit.
            Style("#5A4A3A", "#514233", "#70604C", "#382D22", loose: true),
            // Dojo: a court of light grey stone.
            Style("#5C605A", "#535751", "#74796E", "#33362F"),
            // Hall: black iron slate.
            Style("#2E3038", "#25272E", "#444854", "#14151A"),
            // Web: grey stone under dust and silk.
            Style("#48424E", "#403B46", "#5C5566", "#28232C"),
        };

        // The boss arena: flagstones, one to a tile, so it reads as a made place and not a cave; or loose ground,
        // grained and pebbled, for the pit, the camp and the mine.
        static PixelImage ArenaFloor(int mask, int variant, ArenaStyle style)
        {
            var image = new PixelImage(Size, Size);
            int salt = 200 + variant;
            var stone = variant % 2 == 0 ? style.Slab : style.SlabAlt;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Rgba color;
                    if (style.Loose)
                    {
                        // Soft drifts of two tones, no joints (the tones meet across tiles on the shared lattice).
                        float n = Smooth(x, y, 4, salt, 200);
                        color = n > 0.68f ? style.Light.Mix(style.Slab, 0.5f) : n < 0.3f ? style.SlabAlt.Scale(0.92f) : style.Slab;
                        if (PixelNoise.Hash(x, y, salt) % 41 == 0 && x > 0 && x < Size - 2) color = style.Mortar;
                        else if (x > 1 && PixelNoise.Hash(x - 1, y, salt) % 41 == 0 && x < Size - 1) color = style.Mortar.Mix(style.Slab, 0.4f);
                    }
                    else
                    {
                        color = stone;
                        if (x == 0 || y == 0) color = style.Mortar;
                        else if (x == 1 || y == 1) color = style.Light;
                        else if (x == Size - 1 || y == Size - 1) color = stone.Scale(0.85f);
                        else if (Smooth(x, y, 4, salt, 200) > 0.7f) color = stone.Scale(0.93f);
                    }
                    image[x, y] = color;
                }
            }
            if (variant >= 2 && !style.Loose) Crack(image, salt, style.Crack);
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
            // Warm earth in level bands (Docs/BiomeRework.md 4-1 흙 동굴, 묶음 B1a: the browns #5C4532 and #7A5E44,
            // refined from the greyer #5C4D41 and #8A7562).
            Top = Rgba.Hex("#5C4532"),
            TopDark = Rgba.Hex("#513C2B"),
            TopLight = Rgba.Hex("#684F3A"),
            Rim = Rgba.Hex("#A08260"),
            FaceTop = Rgba.Hex("#866848"),
            FaceBottom = Rgba.Hex("#644C36"),
            Strata = Rgba.Hex("#523D2A"),
            FaceSpeck = Rgba.Hex("#9A7E5C"),
            Contact = Rgba.Hex("#2A1F16"),
            Outline = Rgba.Hex("#1A120C"),
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

        // An arc furnace: a steel box with two electrodes on top and an arc crackling in its window.
        static readonly PixelImage ArcFurnaceTile = PixelImage.FromRows(new Dictionary<char, Rgba>(MetalPalette)
            {
                ['f'] = Rgba.Hex("#F08A30"),
                ['F'] = Rgba.Hex("#FFD860"),
                ['e'] = Rgba.Hex("#9AE0FF"),
                ['E'] = Rgba.Hex("#3A78C8"),
            },
            "................",
            "..oo........oo..",
            "..oe........eo..",
            ".ooeooooooooeoo.",
            ".oLLLLLLLLLLLLo.",
            ".oLMMMMMMMMMMmo.",
            ".oLMkkkkkkkkMmo.",
            ".oLMkfFFFFfkMmo.",
            ".oLMkFFeeFFkMmo.",
            ".oLMkfFFFFfkMmo.",
            ".oLMkkkkkkkkMmo.",
            ".oLMMMMMMMMMMmo.",
            ".oyEyEyEyEyEyEo.",
            ".oDDDDDDDDDDDDo.",
            "..oooooooooooo..",
            "..____________..");

        // A water wheel's two posts and axle block; the wheel is drawn on top, turning while it runs.
        static readonly PixelImage WaterWheelStand = PixelImage.FromRows(new Dictionary<char, Rgba>(MetalPalette)
            {
                ['w'] = Rgba.Hex("#9A6A3C"),
                ['W'] = Rgba.Hex("#6A4424"),
            },
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "..ow........wo..",
            "..ow........wo..",
            "..oW........Wo..",
            "..oW........Wo..",
            ".oWWo......oWWo.",
            ".oooo......oooo.",
            ".____......____.");

        // A tesla tower: a copper coil wound round a steel post on a broad base, a glowing ball on top.
        static readonly PixelImage TeslaTowerTile = PixelImage.FromRows(new Dictionary<char, Rgba>(MetalPalette)
            {
                ['c'] = Rgba.Hex("#D6783F"),
                ['C'] = Rgba.Hex("#9A4E22"),
                ['e'] = Rgba.Hex("#9AE0FF"),
                ['E'] = Rgba.Hex("#3A78C8"),
            },
            "......oooo......",
            ".....oeeEEo.....",
            ".....oeEEEo.....",
            "......oooo......",
            "......oMmo......",
            ".....occCCo.....",
            ".....oMMmmo.....",
            ".....occCCo.....",
            ".....oMMmmo.....",
            ".....occCCo.....",
            "....ooMMmmoo....",
            "...oLLLLLLLLo...",
            "..oLMMMMMMMMmo..",
            "..oDDDDDDDDDDo..",
            "...oooooooooo...",
            "...__________...");

        // A solar panel: blue cells in a steel frame on a short post.
        static readonly PixelImage SolarPanelTile = PixelImage.FromRows(new Dictionary<char, Rgba>(MetalPalette)
            {
                ['C'] = Rgba.Hex("#6A9CE8"),
                ['c'] = Rgba.Hex("#2A4C9C"),
            },
            "................",
            ".oooooooooooooo.",
            ".oLLLLLLLLLLLLo.",
            ".oLCccLCcLCccLo.",
            ".oLcccLccLcccLo.",
            ".oLcccLccLcccLo.",
            ".oLLLLLLLLLLLLo.",
            ".oLCccLCcLCccLo.",
            ".oLcccLccLcccLo.",
            ".oLcccLccLcccLo.",
            ".oLLLLLLLLLLLLo.",
            ".oooooooooooooo.",
            "......oMMo......",
            "......omMo......",
            "....ooddddoo....",
            "....__________..");

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
        // A woven wool rug (2026-11-09): a dark border, a diamond in the light thread, a fringe top and bottom. Laid side by
        // side they read as one patterned carpet.
        static PixelImage Rug(string main, string light, string dark) => PixelImage.FromRows(
            new Dictionary<char, Rgba> { ['M'] = Rgba.Hex(main), ['L'] = Rgba.Hex(light), ['d'] = Rgba.Hex(dark), ['f'] = Rgba.Hex("#E8DCC0") },
            "f.f.f.f.f.f.f.f.",
            "dddddddddddddddd",
            "dMMMMMMMMMMMMMMd",
            "dMLMMMMMMMMMMLMd",
            "dMMMMMMLLMMMMMMd",
            "dMMMMMLddLMMMMMd",
            "dMMMMLdMMdLMMMMd",
            "dMMMLdMLLMdLMMMd",
            "dMMMLdMLLMdLMMMd",
            "dMMMMLdMMdLMMMMd",
            "dMMMMMLddLMMMMMd",
            "dMMMMMMLLMMMMMMd",
            "dMLMMMMMMMMMMLMd",
            "dMMMMMMMMMMMMMMd",
            "dddddddddddddddd",
            ".f.f.f.f.f.f.f.f");

        static readonly PixelImage RedCarpetTile = Rug("#B83A44", "#F0B060", "#5A1A22");
        static readonly PixelImage BlueCarpetTile = Rug("#3A64B8", "#A8D8F0", "#1A2A5A");
        static readonly PixelImage YellowCarpetTile = Rug("#D8A830", "#FFF0A0", "#6A4A14");

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
        // Pale slabs worn smooth under a hole to the sky; the light that falls on them is the view's (WorldView, sky).
        static readonly PixelImage SkylightTile = Slabs("#B8AE98", "#D2C8B0", "#9E9480", "#7A7262");

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

        // Stone bricks to hang things on, like the built stone wall.
        static readonly Dictionary<char, Rgba> HungWallColors = new Dictionary<char, Rgba>(BasePalette)
        {
            ['B'] = Rgba.Hex("#8A8070"), ['b'] = Rgba.Hex("#6E6658"), ['v'] = Rgba.Hex("#A89E8C"),
            ['R'] = Rgba.Hex("#B83A44"), ['r'] = Rgba.Hex("#7A222C"), ['y'] = Rgba.Hex("#F2C440"), ['n'] = Rgba.Hex("#3A64B8"),
            ['s'] = Rgba.Hex("#C8CCD4"), ['k'] = Rgba.Hex("#5A4030"),
        };

        // A wall hung with a woven wool tapestry on a rod: red with a gold stripe and a blue diamond, tasselled.
        static readonly PixelImage TapestryWallTile = PixelImage.FromRows(HungWallColors,
            "oooooooooooooooo",
            "ovvvvvvovvvvvvvo",
            "obbbbkkkkkkkbbbo",
            "oBBBRRRRRRRRBBBo",
            "obbbRyyyyyyRbbbo",
            "oBBBRRRnnRRRBBBo",
            "oBBBRRnRRnRRBBbo",
            "obbbRRRnnRRRbbbo",
            "oBBBRyyyyyyRBBBo",
            "oBBBRRRRRRRRBBBo",
            "obbbRrRrRrRrbbbo",
            "oBBBy.y.y.y.BBBo",
            "ovvvvvvovvvvvvvo",
            "oBBBBBBoBBBBBBBo",
            "obbbbbbobbbbbbbo",
            "oooooooooooooooo");

        // A wall hung with a round red-and-gold shield over two crossed swords.
        static readonly PixelImage ShieldWallTile = PixelImage.FromRows(HungWallColors,
            "oooooooooooooooo",
            "ovvvvvvovvvvvvvo",
            "oBsBBBBoBBBBsBBo",
            "obbsbbooobbsbbbo",
            "oBBBsoRRRosBBBBo",
            "oBBBoRyRyRoBBBBo",
            "ovvvoRRyRRovvvvo",
            "oBBBoRyRyRoBBBBo",
            "obbbboRRRobbbbbo",
            "oBBBsBoooBsBBBBo",
            "oBBsBBBoBBBsBBBo",
            "obkbbbbobbbbkbbo",
            "ovvvvvvovvvvvvvo",
            "oBBBBBBoBBBBBBBo",
            "obbbbbbobbbbbbbo",
            "oooooooooooooooo");

        // A scarecrow (2026-11-09): a straw hat over a sack face, arms out on a crossbar, a patched coat, on a post.
        static readonly PixelImage ScarecrowTile = PixelImage.FromRows(new Dictionary<char, Rgba>(BasePalette)
            {
                ['T'] = Rgba.Hex("#E2C46A"), ['t'] = Rgba.Hex("#B8963E"), ['F'] = Rgba.Hex("#D8C8A0"), ['B'] = Rgba.Hex("#4A6E3A"),
                ['b'] = Rgba.Hex("#34502A"), ['R'] = Rgba.Hex("#B83A44"),
            },
            "......oooo......",
            "....ooTTTToo....",
            "...otttttttto...",
            "....oFFFFFFo....",
            "....oFoFFoFo....",
            "....oFFFFFFo....",
            "..ooooBBBBoooo..",
            ".oTWWWBRRBWWWTo.",
            "..ooooBBBBoooo..",
            ".....oBbbBo.....",
            ".....oBBRBo.....",
            ".....oTbbTo.....",
            "......oWWo......",
            "......oWWo......",
            "......oWWo......",
            "......____......");

        // A wooden fence (2026-11-09): three posts and two rails, low enough to see the pen over.
        static readonly PixelImage FenceTile = PixelImage.FromRows(BasePalette,
            "................",
            "................",
            "................",
            "................",
            ".oo.....oo....oo",
            "oWWo...oWWo..oWW",
            "oWWoooooWWooooWW",
            "oSSSSSSSSSSSSSSS",
            "oWWwwwwwWWwwwwWW",
            "oWWo...oWWo..oWW",
            "oWWoooooWWooooWW",
            "oSSSSSSSSSSSSSSS",
            "oWWwwwwwWWwwwwWW",
            "owwo...owwo..oww",
            "_oo_..._oo_.._oo",
            "................");

        // An open door (2026-11-09, WorldView.SwingDoors): the frame, the floor's dark through it, and the slab swung
        // back against the left post, seen edge on.
        public static readonly PixelImage OpenDoor = PixelImage.FromRows(BasePalette,
            "................",
            "..oooooooooooo..",
            ".oSSSSSSSSSSSSo.",
            ".oSwo________So.",
            ".oSwW________So.",
            ".oSwk________So.",
            ".oSwW________So.",
            ".oSwW________So.",
            ".oSwW________So.",
            ".oSwW________So.",
            ".oSwk________So.",
            ".oSwW________So.",
            ".oSwW________So.",
            ".oSwo________So.",
            ".oSSSSSSSSSSSSo.",
            "..____________..");

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

        // A road between the places of the story (Docs/Story.md; redrawn 2026-11-11, "길 같지가 않아"): rounded cobbles
        // laid in courses, each course shifted so the joints do not line up, in warm greys lighter than the cave floor,
        // and a kerb of long stones wherever the road meets something that is not road (RoadVariant), so its edges
        // read as a way. Two layings, chosen by the tile's place, keep a long road from looking stamped.
        // Sides (4 bits), inner corners (4 bits) and laying (1 bit).
        public const int RoadVariants = 512;
        static readonly PixelImage[] RoadTiles = new PixelImage[RoadVariants];

        // The road with these sides joined to more road or paving (N 1, E 2, S 4, W 8), these inner corners kerbed
        // (NE 16, SE 32, SW 64, NW 128: both sides beside the corner join but the tile across it does not), laid the
        // first or second way (+256).
        public static PixelImage Road(int variant)
        {
            variant = System.Math.Clamp(variant, 0, RoadVariants - 1);
            return RoadTiles[variant] ??= DrawRoad(variant & 15, variant >> 4 & 15, variant >> 8);
        }

        // Which road tile goes at x, y: its sides that meet road, a waypoint, the harbour's planks, the mine's rails or
        // a laid floor, and a laying by a hash of where it is.
        public static int RoadVariant(TileMap map, int x, int y)
        {
            bool Joins(int nx, int ny)
            {
                if (!map.InBounds(nx, ny)) return true;
                var type = map[nx, ny];
                return Structures.IsWay(type) || type == TileType.Waypoint || TileMap.IsPaving(type);
            }
            int sides = (Joins(x, y + 1) ? 1 : 0) | (Joins(x + 1, y) ? 2 : 0) | (Joins(x, y - 1) ? 4 : 0) | (Joins(x - 1, y) ? 8 : 0);
            // An inner corner: the two sides beside it join, the tile across it does not (where a sloping road steps).
            int corners = 0;
            if ((sides & 3) == 3 && !Joins(x + 1, y + 1)) corners |= 1;
            if ((sides & 6) == 6 && !Joins(x + 1, y - 1)) corners |= 2;
            if ((sides & 12) == 12 && !Joins(x - 1, y - 1)) corners |= 4;
            if ((sides & 9) == 9 && !Joins(x - 1, y + 1)) corners |= 8;
            int laying = ((x * 73 + y * 151) >> 2 ^ x ^ y) & 1;
            return sides | corners << 4 | laying << 8;
        }

        static PixelImage DrawRoad(int sides, int corners, int laying)
        {
            var mortar = Rgba.Hex("#5E564C");
            var grit = Rgba.Hex("#4E473F");
            var tints = new[]
            {
                (Light: Rgba.Hex("#C4BAA8"), Main: Rgba.Hex("#A89E8C"), Shade: Rgba.Hex("#8A8070")),
                (Light: Rgba.Hex("#B8B0A0"), Main: Rgba.Hex("#9C9484"), Shade: Rgba.Hex("#80786A")),
                (Light: Rgba.Hex("#CAC0AC"), Main: Rgba.Hex("#B0A48E"), Shade: Rgba.Hex("#928670")),
            };
            var image = new PixelImage(Size, Size);
            image.Fill(0, 0, Size, Size, mortar);
            // Four courses of three stones (5, 6 and 5 wide), each course shifted along; the joints wrap round the tile so
            // tiles meet without a seam.
            int[] widths = { 5, 6, 5 };
            int[] shifts = laying == 0 ? new[] { 0, 3, 7, 2 } : new[] { 2, 6, 1, 4 };
            for (int course = 0; course < 4; course++)
            {
                int top = course * 4;
                int at = shifts[course];
                for (int stone = 0; stone < 3; stone++)
                {
                    var tint = tints[(course * 2 + stone + laying * 2) % tints.Length];
                    int w = widths[(stone + course + laying) % 3];
                    // The stone fills its width less a joint on its right and its height less the course's bottom joint.
                    for (int dy = 0; dy < 3; dy++)
                    {
                        for (int dx = 0; dx < w - 1; dx++)
                        {
                            bool corner = (dx == 0 || dx == w - 2) && (dy == 0 || dy == 2);
                            if (corner) continue;
                            var color = dy == 0 ? tint.Light : dy == 2 || dx == w - 2 ? tint.Shade : tint.Main;
                            image[(at + dx) % Size, top + dy] = color;
                        }
                    }
                    at += w;
                }
            }
            // A few grains of grit in the joints.
            foreach (var (gx, gy) in laying == 0 ? new[] { (4, 3), (11, 7), (6, 11), (14, 15) } : new[] { (1, 7), (9, 3), (13, 11), (5, 15) })
                image[gx, gy] = grit;

            // The kerb along each side that does not join more road: long stones with a dark line outside them; the
            // south kerb (the front, as the view looks) shows its face below.
            var kerb = (Light: Rgba.Hex("#D2CAB8"), Main: Rgba.Hex("#B4AA96"), Shade: Rgba.Hex("#8C826F"));
            var edge = Rgba.Hex("#332E28");
            bool north = (sides & 1) == 0, east = (sides & 2) == 0, south = (sides & 4) == 0, west = (sides & 8) == 0;
            if (north)
            {
                for (int x = 0; x < Size; x++)
                {
                    image[x, 0] = edge;
                    image[x, 1] = x % 8 == 7 ? kerb.Shade : kerb.Light;
                    image[x, 2] = x % 8 == 7 ? kerb.Shade : kerb.Main;
                }
            }
            if (south)
            {
                for (int x = 0; x < Size; x++)
                {
                    image[x, 12] = x % 8 == 3 ? kerb.Shade : kerb.Light;
                    image[x, 13] = x % 8 == 3 ? kerb.Shade : kerb.Main;
                    image[x, 14] = kerb.Shade;
                    image[x, 15] = edge;
                }
            }
            if (west)
            {
                for (int y = 0; y < Size; y++)
                {
                    if (north && y < 1 || south && y > 14) continue;
                    image[0, y] = edge;
                    image[1, y] = y % 8 == 5 ? kerb.Shade : kerb.Light;
                    image[2, y] = y % 8 == 5 ? kerb.Shade : kerb.Main;
                }
            }
            if (east)
            {
                for (int y = 0; y < Size; y++)
                {
                    if (north && y < 1 || south && y > 14) continue;
                    image[13, y] = y % 8 == 1 ? kerb.Shade : kerb.Main;
                    image[14, y] = kerb.Shade;
                    image[15, y] = edge;
                }
            }
            // The inner corners: a short bend of kerb, so the kerbs of a stepping road run on round the step.
            void Corner(bool right, bool top)
            {
                int Fx(int i) => right ? Size - 1 - i : i;
                int Fy(int j) => top ? j : Size - 1 - j;
                image[Fx(0), Fy(0)] = edge;
                image[Fx(1), Fy(0)] = top ? kerb.Light : kerb.Shade;
                image[Fx(0), Fy(1)] = right ? kerb.Shade : kerb.Light;
                image[Fx(1), Fy(1)] = kerb.Main;
                image[Fx(2), Fy(0)] = top ? kerb.Main : kerb.Shade;
                image[Fx(0), Fy(2)] = kerb.Main;
            }
            if ((corners & 1) != 0) Corner(right: true, top: true);
            if ((corners & 2) != 0) Corner(right: true, top: false);
            if ((corners & 4) != 0) Corner(right: false, top: false);
            if ((corners & 8) != 0) Corner(right: false, top: true);
            return image;
        }

        // The harbour's planks over the water (Docs/Story.md, 2026-11-10): boards laid across, dark gaps between,
        // nail heads.
        static readonly PixelImage DockTile = PixelImage.FromRows(new Dictionary<char, Rgba>
            {
                ['l'] = Rgba.Hex("#C8965E"),
                ['p'] = Rgba.Hex("#A87444"),
                ['P'] = Rgba.Hex("#8A5C34"),
                ['g'] = Rgba.Hex("#3A2618"),
                ['n'] = Rgba.Hex("#5A4A40"),
            },
            "llllllllllllllll",
            "pnpppppppppppnpp",
            "pppppppPpppppppp",
            "PPPPPPPPPPPPPPPP",
            "gggggggggggggggg",
            "llllllllllllllll",
            "ppppnpppppppppnp",
            "pppppPpppppppppp",
            "PPPPPPPPPPPPPPPP",
            "gggggggggggggggg",
            "llllllllllllllll",
            "pnppppppppnppppp",
            "pppppppppppPpppp",
            "PPPPPPPPPPPPPPPP",
            "gggggggggggggggg",
            "llllllllllllllll");

        // A place's waypoint (Docs/Story.md): a ring of carved stone round a pool of blue light.
        static readonly PixelImage WaypointTile = PixelImage.FromRows(new Dictionary<char, Rgba>
            {
                ['o'] = Rgba.Hex("#1A1410"),
                ['S'] = Rgba.Hex("#8A8478"),
                ['M'] = Rgba.Hex("#6A645A"),
                ['B'] = Rgba.Hex("#2A4A8A"),
                ['b'] = Rgba.Hex("#3A6ACA"),
                ['c'] = Rgba.Hex("#5A9AF0"),
                ['C'] = Rgba.Hex("#8AC8FF"),
                ['D'] = Rgba.Hex("#E0F4FF"),
            },
            "................",
            ".....oooooo.....",
            "...ooSSSSSSoo...",
            "..oSSMMMMMMSSo..",
            "..oSMBbbbbBMSo..",
            ".oSMBbcccccbBMSo",
            ".oSMbcCCCCCcbMSo",
            ".oSMbcCDDDCcbMSo",
            ".oSMbcCDDDCcbMSo",
            ".oSMbcCCCCCcbMSo",
            ".oSMBbcccccbBMSo",
            "..oSMBbbbbBMSo..",
            "..oSSMMMMMMSSo..",
            "...ooSSSSSSoo...",
            ".....oooooo.....",
            "................");


        // A locked door (2026-11-09, World.LockDoor): the same door with a brass padlock on its middle band in place of
        // the handle. Only its owner walks through.
        static readonly PixelImage LockedDoorTile = PixelImage.FromRows(BasePalette,
            "................",
            "..oooooooooooo..",
            ".oSSSSSSSSSSSSo.",
            ".oSwwwwwwwwwwSo.",
            ".oSwWWWWWWWWwSo.",
            ".oSwkkkkkkkkwSo.",
            ".oSwWWWkkWWWwSo.",
            ".oSwWWkWWkWWwSo.",
            ".oSwWoyyyyoWwSo.",
            ".oSwWoyoyyoWwSo.",
            ".oSwkoyyyyokwSo.",
            ".oSwWWooooWWwSo.",
            ".oSwWWWWWWWWwSo.",
            ".oSwwwwwwwwwwSo.",
            ".oSSSSSSSSSSSSo.",
            "..____________..");

        // An iron wall (2026-11-09): riveted steel plates in a dark frame, a lit top edge, filling the tile like rock.
        static readonly PixelImage IronWallTile = PixelImage.FromRows(new Dictionary<char, Rgba>(BasePalette)
            {
                ['A'] = Rgba.Hex("#C4CCD6"),
                ['B'] = Rgba.Hex("#8E98A6"),
                ['b'] = Rgba.Hex("#6C7684"),
                ['v'] = Rgba.Hex("#E8EEF4"),
            },
            "oooooooooooooooo",
            "oAAAAAAAAAAAAAAo",
            "oBvBBBBBoBBBBvBo",
            "oBBBBBBBoBBBBBBo",
            "oBBBBBBBoBBBBBBo",
            "obbbbbbbobbbbbbo",
            "oooooooooooooooo",
            "oBBBvBBBBBBvBBBo",
            "oBBBBBBBBBBBBBBo",
            "oBBBBBBBBBBBBBBo",
            "obbbbbbbbbbbbbbo",
            "oooooooooooooooo",
            "oBvBBBBBoBBBBvBo",
            "oBBBBBBBoBBBBBBo",
            "obbbbbbbobbbbbbo",
            "oooooooooooooooo");

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

        // A person's stall, two tiles tall (2026-11-09): its own tile has a wooden counter with wares on it and two posts
        // going up; the striped awning on the posts is on the tile above (Awning), drawn over the person standing in
        // the gap (WorldView draws them) so they stand under it. MerchantCounter goes over their legs.
        static readonly PixelImage MerchantStallTile = PixelImage.FromRows(BasePalette,
            ".ow..........wo.",
            ".ow..........wo.",
            ".ow..........wo.",
            ".ow..........wo.",
            ".ow..........wo.",
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

        static readonly PixelImage SmithStallTile = MerchantStallTile;
        static readonly PixelImage FisherHutTile = MerchantStallTile;
        static readonly PixelImage AlchemyTableTile = MerchantStallTile;

        // The awning over a stall, for the tile above it: a scalloped striped canopy on the tops of the posts. Its edge
        // comes a pixel or two over the person's head. The merchant's is red and cream; the blacksmith's soot-dark with
        // ember orange; the fisher's blue and white; the alchemist's violet and pale green.
        static readonly PixelImage MerchantAwning = PixelImage.FromRows(BasePalette,
            "................",
            "................",
            "................",
            "................",
            "................",
            "oooooooooooooooo",
            "oRRccRRccRRccRRo",
            "oRRccRRccRRccRRo",
            "orrCCrrCCrrCCrro",
            "oRRccRRccRRccRRo",
            "oo.oo.oo.oo.oo.o",
            ".ow..........wo.",
            ".ow..........wo.",
            ".ow..........wo.",
            ".ow..........wo.",
            ".ow..........wo.");

        static readonly PixelImage SmithAwning = Restriped(MerchantAwning, "#4A4444", "#3A3434", "#E8783A", "#B8562A");
        static readonly PixelImage FisherAwning = Restriped(MerchantAwning, "#3E7FD0", "#2E5FA0", "#F6F2E8", "#C8C0B0");
        static readonly PixelImage AlchemyAwning = Restriped(MerchantAwning, "#9A5CD8", "#6E3CA8", "#E8F6E0", "#B8D0A8");
        // The shops in the places of the story (2026-11-10): royal red and gold, demon purple and bone, the ore seller's
        // rust and grey.
        static readonly PixelImage RoyalAwning = Restriped(MerchantAwning, "#C8323A", "#8A1E2A", "#F0BE40", "#B8862A");
        static readonly PixelImage DemonAwning = Restriped(MerchantAwning, "#581A34", "#380E22", "#E2DCCA", "#B4AC96");
        static readonly PixelImage OreAwning = Restriped(MerchantAwning, "#985028", "#603018", "#9A9488", "#6E6A62");
        // The wandering merchant's (2026-11-22, 묶음 3c): teal and saffron, a travelling tent's colours.
        static readonly PixelImage WandererAwning = Restriped(MerchantAwning, "#2EA89A", "#1E7068", "#F2B640", "#B8822A");

        // The awning for a person's stall, or null for any other tile.
        public static PixelImage Awning(TileType stall) => stall switch
        {
            TileType.MerchantStall => MerchantAwning,
            TileType.SmithStall => SmithAwning,
            TileType.FisherHut => FisherAwning,
            TileType.AlchemyTable => AlchemyAwning,
            TileType.RoyalStall => RoyalAwning,
            TileType.DemonStall => DemonAwning,
            TileType.OreStall => OreAwning,
            TileType.WandererStall => WandererAwning,
            _ => null,
        };

        // The awning with its stripes in other colours: the red and the cream, and their shades.
        static PixelImage Restriped(PixelImage stall, string red, string redShade, string cream, string creamShade)
        {
            var swap = new Dictionary<Rgba, Rgba>
            {
                [BasePalette['R']] = Rgba.Hex(red),
                [BasePalette['r']] = Rgba.Hex(redShade),
                [BasePalette['c']] = Rgba.Hex(cream),
                [BasePalette['C']] = Rgba.Hex(creamShade),
            };
            var image = stall.Copy();
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    if (swap.TryGetValue(image[x, y], out var to)) image[x, y] = to;
                }
            }
            return image;
        }

        // The stall's counter alone, drawn over the merchant standing behind it.
        public static readonly PixelImage MerchantCounter = PixelImage.FromRows(BasePalette,
            Enumerable.Repeat("................", Size - Counter.Length).Concat(Counter).ToArray());

        // A wooden barrel on its end with two iron hoops and a tap.
        static readonly PixelImage KegTile = PixelImage.FromRows(BasePalette,
            "................",
            "................",
            "....oooooooo....",
            "...oSSSSSSSSo...",
            "...oWWWWWWWWo...",
            "..okkkkkkkkkko..",
            "..oWWwWWWwWWWo..",
            "..oWWwWWWwWWWoo.",
            "..oWWwWWWwWWWoko",
            "..oWWwWWWwWWWoo.",
            "..okkkkkkkkkko..",
            "...owwwwwwwwo...",
            "...owwwwwwwwo...",
            "....oooooooo....",
            "....________....",
            "................");

        // A red barn with a white-framed door.
        static readonly PixelImage BarnTile = PixelImage.FromRows(BasePalette,
            ".....oooooo.....",
            "...ooRRRRRRoo...",
            ".ooRRRRRRRRRRoo.",
            "oRRRRRRRRRRRRRRo",
            "orrrrrrrrrrrrrro",
            ".oRRRRRRRRRRRRo.",
            ".oRrRRoooooRRro.",
            ".oRrRocSScocRro.",
            ".oRrRoScSScoRro.",
            ".oRrRocSScocRro.",
            ".oRrRoScSScoRro.",
            ".oRrRocSScocRro.",
            ".orrroooooooorro",
            ".oooooooooooooo.",
            "..____________..",
            "................");

        // A sheep fold (2026-11-09): a straw-roofed lean-to over a hay bed, a low wooden fence round the front.
        static readonly PixelImage FoldTile = PixelImage.FromRows(new Dictionary<char, Rgba>(BasePalette)
            {
                ['T'] = Rgba.Hex("#E2C46A"),
                ['t'] = Rgba.Hex("#B8963E"),
                ['g'] = Rgba.Hex("#D8C070"),
            },
            "................",
            "....oooooooo....",
            "..ooTTTTTTTToo..",
            ".oTTtTTtTTtTTTo.",
            "otttttttttttttto",
            ".oWoggggggggoWo.",
            ".oWogtggtggtoWo.",
            ".oWoggggggggoWo.",
            "oooooooooooooooo",
            "oWWWWWWWWWWWWWWo",
            "owoowoowoowoowoo",
            "oWWWWWWWWWWWWWWo",
            "owoowoowoowoowoo",
            "oooooooooooooooo",
            ".______________.",
            "................");

        // A pale blue crystal standing on a carved stone base.
        static readonly PixelImage WardStoneTile = PixelImage.FromRows(new Dictionary<char, Rgba>(BasePalette)
            {
                ['B'] = Rgba.Hex("#E6F8FF"), ['b'] = Rgba.Hex("#8CE0FF"), ['n'] = Rgba.Hex("#3E8CC8"),
            },
            "......oo........",
            ".....obBo.......",
            ".....obBbo......",
            "....onbBbo......",
            "....onbbBbo.....",
            "....onnbbbo.....",
            "....onnbbbo.....",
            ".....onnbo......",
            "...oooooooooo...",
            "..oLLLLLLLLLLo..",
            "..oMMmMMMMmMMo..",
            "..oMMMMMMMMMMo..",
            "..ommmmmmmmmmo..",
            "..oooooooooooo..",
            "..____________..",
            "................");

        // A turret's round iron base; the barrel is drawn on top, turned toward its last shot.
        static readonly PixelImage TurretBase = PixelImage.FromRows(MetalPalette,
            "................",
            "................",
            "................",
            ".....oooooo.....",
            "...ooMMMMMMoo...",
            "..oMMLLLLLLMMo..",
            "..oMLLMMMMLLMo..",
            "..oMLMkkkkMLMo..",
            "..oMLMkkkkMLMo..",
            "..oMLLMMMMLLMo..",
            "..omMMMMMMMMmo..",
            "...oommmmmmoo...",
            "....dddddddd....",
            ".....oooooo.....",
            "....________....",
            "................");

        // A little wooden hen house with a red roof and a dark doorway.
        static readonly PixelImage CoopTile = PixelImage.FromRows(BasePalette,
            "................",
            "......oooo......",
            "....ooRRRRoo....",
            "..ooRRRRRRRRoo..",
            ".oRRRRRRRRRRRRo.",
            "orrrrrrrrrrrrrro",
            ".oWWWWWWWWWWWWo.",
            ".oWwWWWWWWWWwWo.",
            ".oWwWWWooWWWwWo.",
            ".oWwWWokkoWWwWo.",
            ".oWwWWokkoWWwWo.",
            ".oWwWWokkoWWwWo.",
            ".owwwwokkowwwwo.",
            ".oooooooooooooo.",
            "..____________..",
            "................");

        // A wicker pot with a rope, set down at the water's edge.
        static readonly PixelImage CrabPotTile = PixelImage.FromRows(BasePalette,
            "................",
            "..........cc....",
            ".........c..c...",
            "....oooooooc....",
            "...oCSCSCSCSo...",
            "..oSCoCoCoCSCo..",
            "..oCSCSCSCSCSo..",
            "..oSoCoCoCoCSo..",
            "..oCSCSCSCSCSo..",
            "..oSCoCoCoCSCo..",
            "...oCSCSCSCSo...",
            "....oooooooo....",
            "....________....",
            "................",
            "................",
            "................");

        // A round clay jar with a cloth tied over its mouth.
        static readonly PixelImage JarTile = PixelImage.FromRows(BasePalette,
            "................",
            "................",
            "................",
            ".....oooooo.....",
            "....occcccco....",
            "....oCrrrrCo....",
            ".....oPPPPo.....",
            "....oPPPPPPo....",
            "...oPPpPPPPPo...",
            "...oPPpPPPPpo...",
            "...oPPPPPPPpo...",
            "....oPpppppo....",
            ".....oooooo.....",
            "....________....",
            "................",
            "................");

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

        // A big assembler (2026-11-09, World.SizeOf), 32 pixels each way for its 2×2 tiles: a steel housing round a dark
        // panel with two gold gears turning against each other, lamps, and a chute at the bottom.
        public static readonly PixelImage BigAssembler = PixelImage.FromRows(new Dictionary<char, Rgba>(BasePalette)
            {
                ['I'] = Rgba.Hex("#9AA0AA"), ['i'] = Rgba.Hex("#747A86"), ['j'] = Rgba.Hex("#525762"), ['G'] = Rgba.Hex("#E2B04A"),
                ['g'] = Rgba.Hex("#A87A2C"), ['e'] = Rgba.Hex("#7CE07C"),
            },
            "................................",
            "................................",
            ".oooooooooooooooooooooooooooooo.",
            ".oIIIIIIIIIIIIIIIIIIIIIIIIIIIIo.",
            ".oIiiiiiiiiiiiiiiiiiiiiiiiiiijo.",
            ".oIiooooooooooooooooooooooooijo.",
            ".oIiokkkjkkkkkkjkkkkkkjkkkkoijo.",
            ".oIiokkjkGGGGGjkkkkkkjkkkkkoijo.",
            ".oIiokjkkGgggGkkkkkGGGGGkkkoijo.",
            ".oIiojkkgggggggkkkkjgggkkkjoijo.",
            ".oIiokkgggggggggkkgggggggjkoijo.",
            ".oIioGGgggGGGgggGGgggggggkGoijo.",
            ".oIioGgggGGoGGggGgggGoGgggGoijo.",
            ".oIioGgggGoooGggGgggooogggGoijo.",
            ".oIioGgggGGoGGggGgggGoGgggGoijo.",
            ".oIioGGgggGGGgggGGgggggggkGoijo.",
            ".oIiojkgggggggggkkgggggggkjoijo.",
            ".oIiokkkgggggggkkkjkgggkkjkoijo.",
            ".oIiokkkkGgggGkkkjkGGGGGjkkoijo.",
            ".oIiokGkkGGGGGkkjkkkkkkjeekoijo.",
            ".oIiokkkjkkkkkkjkkkkkkjkkkkoijo.",
            ".oIiooooooooooooooooooooooooijo.",
            ".oIiiiiiiiiiokkkkkkoiiiiiiiiijo.",
            ".oIiiiiiiiiiokkkkkkoiiiiiiiiijo.",
            ".oIiiiiiiiiiokkkkkkoiiiiiiiiijo.",
            ".oIiiiiiiiiiooooooooiiiiiiiiijo.",
            ".oIjjjjjjjjjjjjjjjjjjjjjjjjjjjo.",
            ".oooooooooooooooooooooooooooooo.",
            "...oooooooooooooooooooooooooo...",
            "..____________________________..",
            "................................",
            "................................");

        // A big smelter (2026-11-09, World.SizeOf), 32 pixels each way for its 2×2 tiles: a brick furnace with a chimney,
        // a mouth full of fire over a grate, and a lamp that shows it has power.
        public static readonly PixelImage BigSmelter = PixelImage.FromRows(new Dictionary<char, Rgba>(BasePalette)
            {
                ['B'] = Rgba.Hex("#A85C3E"), ['b'] = Rgba.Hex("#8C4A32"), ['m'] = Rgba.Hex("#5A3428"), ['M'] = Rgba.Hex("#7A7470"),
                ['f'] = Rgba.Hex("#FF8A3C"), ['F'] = Rgba.Hex("#FFD25A"), ['r'] = Rgba.Hex("#D2462A"), ['j'] = Rgba.Hex("#525762"),
                ['e'] = Rgba.Hex("#7CE07C"), ['s'] = Rgba.Hex("#9A9AA0"),
            },
            ".......ss.......................",
            ".....oooooo.....................",
            ".....oMmMMo.....................",
            ".....omMMmo.....................",
            ".....oMMmMo.....................",
            ".....oMmMMo.....................",
            ".oooooooooooooooooooooooooooooo.",
            ".oBmBBBBBmBBBBBmBBBBBmBBBBBmBBo.",
            ".obmbbbbbmbbbbbmbbbbbmbbbbbmbbo.",
            ".ommmmmmmmmmmmmmmmmmmmmmoeeommo.",
            ".oBBBBmBBBBBmBBBBBmBBBBBoeeoBBo.",
            ".obbbbmbbbbbmbbbbbmbbbbbmbbbbbo.",
            ".ommmmmmmmmmmmmmmmmmmmmmmmmmmmo.",
            ".oBmBBBBBmBoooooooooomBBBBBmBBo.",
            ".obmbbbbboorrrrrrrrrroobbbbmbbo.",
            ".ommmmmmmorrffffffffrrommmmmmmo.",
            ".oBBBBmBBorrffFFFFffrroBmBBBBBo.",
            ".obbbbmbborrffFFFFffrrobmbbbbbo.",
            ".ommmmmmmorrffFFFFffrrommmmmmmo.",
            ".oBmBBBBBorrffFFFFffrroBBBBmBBo.",
            ".obmbbbbborrffFFFFffrrobbbbmbbo.",
            ".ommmmmmmorrffFFFFffrrommmmmmmo.",
            ".oBBBBmBBorrffFFFFffrroBmBBBBBo.",
            ".obbbbmbbokkkkkkkkkkkkobmbbbbbo.",
            ".ommmmmmmokjkjkjkjkjkkommmmmmmo.",
            ".oBmBBBBBooooooooooooooBBBBmBBo.",
            ".obmbbbbbmbbbbbmbbbbbmbbbbbmbbo.",
            ".ommmmmmmmmmmmmmmmmmmmmmmmmmmmo.",
            ".oooooooooooooooooooooooooooooo.",
            "..____________________________..",
            "................................",
            "................................");

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
