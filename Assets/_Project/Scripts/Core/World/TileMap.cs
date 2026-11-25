using System;

namespace Game.Core
{
    // Everything but floor and conveyors is solid. Which tiles can be dug, and what they give, is in tiles.csv.
    public enum TileType : byte
    {
        Floor,
        Wall,
        CopperOre,
        IronOre,
        // Placed objects: solid like walls, but players build them and stand by them to use them.
        Workbench,
        Furnace,
        Chest,
        // Machines (Docs/Automation.md). A conveyor is walked over like floor.
        Drill,
        Smelter,
        Conveyor,
        // Lights the cave around it (tiles.csv light).
        Torch,
        // The third step's ore, only far from the spawn (WorldGenerator).
        MithrilOre,
        // More machines: a splitter is walked over like a conveyor; a robot arm is solid.
        Splitter,
        Arm,
        // A lamp of glowing spores from the mushroom forest (tiles.csv light).
        Lamp,
        // The lava fields (Docs/Biomes.md): obsidian veins, which only a mithril pickaxe or better digs, and pools of
        // lava, walked over like floor but burning whoever stands in them. Lava cannot be dug or built on.
        ObsidianOre,
        Lava,
        // Where its owner gets up (World.Home.cs).
        Bed,
        // Farm machines (Docs/Farming.md): a sprinkler waters the crops around it, a harvester reaps those ahead of it
        // and plants them again.
        Sprinkler,
        Harvester,
        // The abyss's ore, which only an obsidian pickaxe or better digs (tiles.csv pickaxe).
        VoidOre,
        // Base building (Docs/Crafting.md, 기지 꾸미기): floors laid over bare floor and walked on; a door players walk
        // through and monsters cannot; and things that only look nice.
        StoneFloor,
        BrickFloor,
        GlowFloor,
        Door,
        Table,
        Banner,
        Pot,
        // Pools at the edges of the caves, fished from the floor beside them (Docs/Fishing.md). Nothing walks in them.
        Water,
        // Floor turned with a hoe, where crops start a quarter grown, and the same with fertilizer worked in, which
        // gives one more of the harvest and is used up by it (Docs/Farming.md).
        Soil,
        FertileSoil,
        // More machines (Docs/Automation.md): a filter, walked over like a splitter, that sends one chosen material
        // ahead and the rest aside; a generator that burns fuel and speeds up the machines around it; and a lever that
        // switches the machines around it off and on together.
        Filter,
        Generator,
        Lever,
        // Soil brought from another biome and laid at home, where that biome's crops grow (Docs/Time.md). Turned
        // soil like Soil, but never fertile.
        MushroomSoil,
        FrostSoil,
        AshSoil,
        VoidSoil,
        // Where gear is upgraded, broken down and reworked (World.Refining.cs).
        Anvil,
        // Where a rift key opens a void rift (World.Rift.cs).
        RiftAltar,
        // A machine that makes a chosen recipe over and over from what comes in (Docs/Automation.md).
        Assembler,
        PowerPole,
        FastConveyor,
        UndergroundBelt,
        MerchantStall,
        SmithStall,
        FisherHut,
        Keg,
        PreservesJar,
        Coop,
        CrabPot,
        Barn,
        WardStone,
        Turret,
        // Factory depth (Docs/Automation.md): a furnace that runs only on power, and generators that need no fuel.
        ArcFurnace,
        WaterWheel,
        SolarPanel,
        // A tower that throws lightning at monsters, on power alone (Docs/Defense.md).
        TeslaTower,
        // A floor under a hole cut to the sky: daylight, sunset or moonlight falls on it (Docs/Time.md).
        Skylight,
        // The alchemist's table, where she stands (Docs/Npcs.md, 2026-11-09).
        AlchemyTable,
        // A wall of iron plates (Docs/Defense.md, 2026-11-09): much harder for monsters to break than stone.
        IronWall,
        // A fold for sheep (World.Animals.cs, 2026-11-09).
        Fold,
        // Woven wool rugs, laid like floors, and walls hung with something (Docs/Crafting.md, 2026-11-09).
        RedCarpet,
        BlueCarpet,
        YellowCarpet,
        TapestryWall,
        ShieldWall,
        // Keeps monsters from trampling the crops round it (World.Farming.cs, 2026-11-09).
        Scarecrow,
        // A wooden fence to pen animals in (World.Animals.cs, 2026-11-09).
        Fence,
        // A machine two tiles each way (World.Machines.cs, 2026-11-09): its own tile is the lower left, and the other
        // three are MachinePart tiles that belong to it.
        BigAssembler,
        MachinePart,
        // A smelter two tiles each way (2026-11-09): on power, three times a smelter's pace.
        BigSmelter,
        // A door its owner locked (World.Home.cs, 2026-11-09): solid to everyone else, monsters too.
        LockedDoor,
        // A paved road between the places of the story (Docs/Story.md, 2026-11-10): walked on, never dug or built
        // over, and monsters do not come into being on it.
        Road,
        // A place's waypoint (Docs/Story.md): stood on, it opens; from any open one, a player goes to any other.
        Waypoint,
        // What the places of the story are dressed with (Docs/Story.md, 장소 꾸밈, 2026-11-10): laid by the generator,
        // solid, and never dug (they have no row in tiles.csv).
        TimberWall,
        CastleWall,
        Well,
        Barrel,
        MushroomTree,
        Throne,
        Pillar,
        Fountain,
        Campfire,
        Bones,
        Spire,
        Tent,
        // Structures of the story's places that stand taller than a tile (2026-11-10, Docs/Story.md, 장소 꾸밈): a
        // structure bigger than a tile is its type on its lower left tile and StructurePart on the rest.
        StructurePart,
        Cottage,
        Cabin,
        Tower,
        BigFountain,
        Statue,
        LampPost,
        Crates,
        Signpost,
        Bench,
        Hedge,
        Woodpile,
        TrainingDummy,
        WeaponRack,
        BigTent,
        Totem,
        Cage,
        Brazier,
        RoyalBanner,
        DemonBanner,
        Flowerbed,
        // Walked on like the road (2026-11-10, Docs/Story.md 장소 꾸밈): a harbour's planks over the water, a mine's rails.
        Dock,
        Rail,
        // More dressing: the harbour, the ruined temple and the mining village.
        Boat,
        Lighthouse,
        NetRack,
        Bollard,
        Anchor,
        BrokenPillar,
        FallenColumn,
        Rubble,
        RuneStone,
        Altar,
        MineEntrance,
        MineCart,
        OrePile,
        Lantern,
        // The stalls of the people who keep shop in the places of the story (2026-11-10, Docs/Npcs.md 장소의 상인).
        RoyalStall,
        DemonStall,
        OreStall,
        // What the boss arenas are dressed with (2026-11-17, Docs/Bosses.md 아레나 컨셉, WorldGenerator.Arenas.cs), laid
        // by the generator and never dug. Walked on: a darker inlaid slab, a glowing rune, a web across the floor,
        // fallen petals, a glowing crack, a red runner.
        ArenaInlay,
        RuneFloor,
        Cobweb,
        Petals,
        Fissure,
        ArenaCarpet,
        // Standing (solid): a shrine gate's post, a cherry tree, a cocoon of silk, a suit of armour on a stand.
        Torii,
        CherryTree,
        Cocoon,
        ArmorStand,
        // What the rooms of the generator's version 2 are made of beyond rock and floor (2026-11-21, Docs/WorldRework.md
        // 방의 역할, Docs/Biomes.md 지형). A wall with a crack through it, dug by any pickaxe in a blow or two: behind it
        // a hidden room or a short cut. A grate: nothing walks through it, but it is seen through and light passes. Walls
        // of obsidian and of void stone, which only a pickaxe of their tier digs (tiles.csv pickaxe): the locks of the
        // locked rooms.
        CrackedWall,
        Grate,
        ObsidianWall,
        VoidWall,
        // Floors that change how a player walks (PlayerMotor): ice keeps them sliding, quicksand slows them. Walked on,
        // never dug or built on.
        IceFloor,
        Quicksand,
        // The chests of the rooms of version 2 (2026-11-22, Docs/WorldRework.md 3단계 볼거리): wood, iron and gold,
        // shut until someone opens them (World.Pois.cs), then an open one. Standing (solid), never dug or built on.
        WoodChest,
        IronChest,
        GoldChest,
        OpenChest,
        // A waystone by a camp (2026-11-22, Docs/WorldRework.md 길잡이 돌, 묶음 3c): a standing stone with a blue flame,
        // a waypoint of the field. Standing (solid), never dug or built on.
        Waystone,
        // The wandering merchant's stall (Docs/Npcs.md 떠돌이 상인, 묶음 3c): it stands by a different camp each day.
        WandererStall,
        // The biomes' own props (2026-11-24, Docs/BiomeRework.md 묶음 B1a), laid by the generator last of all and only
        // in worlds of its version 3 (WorldGenerator.Props.cs). Standing things drawn apart from the floor; only their
        // footprint blocks. The dirt cave's: a stalagmite, a pit prop (two posts and a beam), a tipped minecart and a
        // sack of ore (broken for what is in them), a lamp that is out and the same lamp lit (struck, it is lit and
        // stays lit), a boulder and a cave-in of rock and timbers (two tiles wide).
        Stalagmite,
        PitProp,
        TippedCart,
        OreSack,
        UnlitLamp,
        LitLamp,
        Boulder,
        CaveIn,
        // The mushroom forest's: a tall thin cap, a stump with shelf fungus, a spore sac (broken, it bursts into a cloud
        // that fills the monsters in it, World.MonsterAttacks' Balm), a clump of glowing caps (picked for glow spores),
        // a fallen trunk (two tiles wide) and a giant cap whose stem alone blocks.
        PillarCap,
        ShelfStump,
        SporeSac,
        GlowCaps,
        FallenLog,
        GiantCap,
        // The biomes' landmarks (Docs/BiomeRework.md 묶음 B1c, WorldGenerator.BiomeLandmarks.cs), rare, in worlds from
        // version 4: an old miner's hut (three tiles by two, its yard fenced) and the timber frame of a boarded-up
        // drift set against the rock (three tiles wide) in the dirt cave, and the small caps of a fairy ring in the
        // mushroom forest (walked through, picked like any glowing cap). The hut and the frame are never dug.
        MinerHut,
        MineFrame,
        RingCap,
        // A camp's trapdoor down to its cellar (2026-11-24, Docs/BiomeRework.md 묶음 B1c 지하실), laid by the generator's
        // version 4 in some camps: shut until the camp's elites fall (World.OpenCellar), then open for good (a changed
        // tile). Standing (solid, the step down is by its button), never dug or built on.
        Trapdoor,
        OpenTrapdoor,
        // The ice cave's and the sand cave's own props (2026-11-24, Docs/BiomeRework.md 묶음 B2a), laid by the generator's
        // version 5 (WorldGenerator.IceSand.cs). The ice cave's: a spike of ice, a frozen bush (broken for frost berry
        // seeds), a drift of snow, a clump of ice crystals (picked, glowing), a pillar of ice (two tiles wide, glowing)
        // and a frozen fall set against the rock (two tiles wide).
        IceSpike,
        FrozenBush,
        SnowDrift,
        IceCrystals,
        IcePillar,
        FrozenFall,
        // The sand cave's: a cave cactus (picked for its fruit), a beast's skull, a broken urn (broken for coins and
        // amber), a carved sandstone pillar, the ribs of a fossil and a half-buried column (both two tiles wide).
        CaveCactus,
        Skull,
        BrokenUrn,
        SandstonePillar,
        RibArch,
        BuriedColumn,
        // Their landmarks, rare and never dug: a beast frozen in a block of ice (three tiles by two) and the columns of
        // an ice keep's ruin in the ice cave; the front of a buried temple set against the rock (five tiles wide) and a
        // giant fossil skull (three by two) in the sand cave.
        FrozenBeast,
        IceColumn,
        TempleFront,
        GiantSkull,
        // The crystal cave's and the underground lake's own props (2026-11-25, Docs/BiomeRework.md 묶음 B3a), laid by the
        // generator's version 6 (WorldGenerator.CrystalLake.cs). The crystal cave's: a cluster of pink and sky crystals
        // (picked, glowing), a spire taller than its tile, a crystal that rings, a small geode cracked open (broken for
        // coins), a great mass of crystal and a pillar of it leaning (both two tiles wide).
        CrystalCluster,
        CrystalSpire,
        ResonantCrystal,
        CrackedGeode,
        CrystalMass,
        LeaningCrystal,
        // The lake's: a mossy rock, a clump of coral, a giant clam (picked for its pearl), a heap of shells, the
        // wreckage of a boat (two tiles wide) and an islet of moss and glowing caps in a pool (two tiles of its water).
        MossRock,
        Coral,
        GiantClam,
        ShellHeap,
        Wreckage,
        MossIsle,
        // Their landmarks, rare and never dug: a giant geode split open (three tiles by two), the two piers of a
        // crystal arch and the span between them (on the tile half way, walked under); a boat sunk in a pool (three
        // by two, on its water) and an old water wheel by the water (two by two).
        GiantGeode,
        ArchPier,
        CrystalArch,
        SunkenBoat,
        OldWheel,
        // The underground lake's shallows (2026-11-25, Docs/BiomeRework.md 묶음 B3b), laid by the generator's version 6
        // round its water (WorldGenerator.CrystalLakeDungeons.cs) and in the sunken aqueduct's rooms: walked on at
        // PlayerMotor.ShallowSlow, a splash at each step; never dug or built on. A tentacle keeps to it as to the water.
        ShallowWater,
    }

    // The dressing of the story's places (Docs/Story.md, 장소 꾸밈): what it is, how many tiles it stands on.
    public static class Structures
    {
        public const TileType First = TileType.TimberWall;
        public const TileType Last = TileType.Flowerbed;
        // The second run, after the dock and rail floors.
        public const TileType FirstMore = TileType.Boat;
        public const TileType LastMore = TileType.Lantern;

        // Laid by the generator round a place, never dug (no row in tiles.csv), drawn standing on the floor.
        public static bool IsDecor(TileType type) => type >= First && type <= Last || type >= FirstMore && type <= LastMore || type >= TileType.Torii && type <= TileType.ArmorStand;

        // The floors the arenas are dressed with: walked like the road, never built on or dug.
        public static bool IsArenaFloor(TileType type) => type >= TileType.ArenaInlay && type <= TileType.ArenaCarpet;

        // How many tiles across and up a structure stands on, from the tile its type is on (its lower left).
        public static (int W, int H) Footprint(TileType type) => type switch
        {
            TileType.Cottage or TileType.Cabin => (4, 3),
            TileType.Boat or TileType.MineEntrance or TileType.MinerHut or TileType.FrozenBeast or TileType.GiantSkull => (3, 2),
            // The crystal cave's and the lake's (묶음 B3a).
            TileType.GiantGeode or TileType.SunkenBoat => (3, 2),
            TileType.OldWheel => (2, 2),
            TileType.CrystalMass or TileType.LeaningCrystal or TileType.Wreckage or TileType.MossIsle => (2, 1),
            TileType.Tower or TileType.BigFountain or TileType.BigTent or TileType.Lighthouse => (2, 2),
            TileType.Altar or TileType.MineFrame => (3, 1),
            TileType.FallenColumn => (2, 1),
            // The biomes' wide props (묶음 B1a): pictures two tiles tall, standing on their lower row alone.
            TileType.Boulder or TileType.CaveIn or TileType.FallenLog => (2, 1),
            // The ice cave's and the sand cave's (묶음 B2a).
            TileType.IcePillar or TileType.FrozenFall or TileType.RibArch or TileType.BuriedColumn => (2, 1),
            TileType.TempleFront => (5, 1),
            _ => (1, 1),
        };

        // Floors the places lay that are walked like the road: never built on or dug.
        public static bool IsWay(TileType type) => type == TileType.Road || type == TileType.Dock || type == TileType.Rail || IsArenaFloor(type);
    }

    // One tile that is no longer what the seed made it: dug out or built on.
    public readonly struct TileChange
    {
        public TileChange(int x, int y, TileType type)
        {
            X = x;
            Y = y;
            Type = type;
        }

        public int X { get; }
        public int Y { get; }
        public TileType Type { get; }
    }

    // The world's tiles. Height is the world's rows, which everything that walks the world loops over (the generator,
    // the biomes, saving); Rows is what is stored, the world and above it the dungeons' pocket (Docs/WorldRework.md 5단계
    // 주머니의 모양, DungeonPocket): rows no generator, save or map of the world knows of, read and written like any
    // other, and never dug or built on (IsEdge holds them all).
    public sealed class TileMap
    {
        TileType[] _tiles;

        public TileMap(int width, int height, TileType fill = TileType.Wall)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
            Rows = height;
            _tiles = new TileType[width * height];
            if (fill != default) Array.Fill(_tiles, fill);
        }

        public int Width { get; }
        public int Height { get; }
        // Height and the pocket's rows above it (AddPocket); Height alone without one.
        public int Rows { get; private set; }
        public bool HasPocket => Rows > Height;

        // Whether a row is the pocket's, not the world's.
        public bool InPocket(int y) => y >= Height && y < Rows;
        public bool InPocket(Vec2 position) => InPocket(Cell(position.Y));

        // Adds that many rows of rock above the world, once (WorldGenerator.Generate, for a world of rooms wide enough).
        public void AddPocket(int rows)
        {
            if (rows <= 0 || HasPocket) return;
            var tiles = new TileType[Width * (Height + rows)];
            Array.Copy(_tiles, tiles, _tiles.Length);
            Array.Fill(tiles, TileType.Wall, _tiles.Length, Width * rows);
            _tiles = tiles;
            Rows = Height + rows;
            Version++;
        }

        // Outside the map reads as wall, so nothing can walk off the edge.
        public TileType this[int x, int y]
        {
            get => InBounds(x, y) ? _tiles[y * Width + x] : TileType.Wall;
            set
            {
                if (!InBounds(x, y)) throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {y}) is outside the map.");
                _tiles[y * Width + x] = value;
                Version++;
            }
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Rows;

        // Counts every change of a tile, so what is worked out from the tiles (Clearance) knows when to work it out again.
        public int Version { get; private set; }

        public bool IsSolid(int x, int y)
        {
            var type = this[x, y];
            return type != TileType.Floor && !World.IsBelt(type) && type != TileType.Splitter && type != TileType.Filter && type != TileType.Lava && !IsPaving(type)
                && type != TileType.Door && !IsSoil(type) && !Structures.IsWay(type) && type != TileType.Waypoint
                && type != TileType.IceFloor && type != TileType.Quicksand && type != TileType.ShallowWater
                // A fairy ring's small caps are walked through like the floor's cover (묶음 B1c).
                && type != TileType.RingCap
                // A crystal arch's span stands over a tile that is walked (묶음 B3a).
                && type != TileType.CrystalArch;
        }

        // The walls only the generator lays (version 2's rooms): rock to the eye and to light, each dug by its own rule.
        public static bool IsRoomWall(TileType type) => type == TileType.CrackedWall || type == TileType.ObsidianWall || type == TileType.VoidWall;

        public static bool IsSoil(TileType type) => type == TileType.Soil || type == TileType.FertileSoil || SoilBiome(type) != null;

        // The biome a laid soil counts as for crops, or null for any other tile.
        public static Biome? SoilBiome(TileType type) => type switch
        {
            TileType.MushroomSoil => Biome.Mushroom,
            TileType.FrostSoil => Biome.Ice,
            TileType.AshSoil => Biome.Lava,
            TileType.VoidSoil => Biome.Abyss,
            _ => null,
        };

        // Where crops are planted: bare floor or turned soil.
        public static bool IsFarmland(TileType type) => type == TileType.Floor || IsSoil(type);

        // Either kind of door: an open one or one locked (World.LockDoor).
        public static bool IsDoor(TileType type) => type == TileType.Door || type == TileType.LockedDoor;

        // What stops a monster: everything solid, and doors.
        public bool BlocksMonsters(int x, int y) => IsSolid(x, y) || this[x, y] == TileType.Door;

        // A floor laid over bare floor (StoneFloor, BrickFloor, GlowFloor): walked on like floor, built on like floor.
        public static bool IsPaving(TileType type) =>
            type == TileType.StoneFloor || type == TileType.BrickFloor || type == TileType.GlowFloor || type == TileType.Skylight
            || type == TileType.RedCarpet || type == TileType.BlueCarpet || type == TileType.YellowCarpet;

        // Bare floor or a laid floor: where things can be built.
        public static bool IsGround(TileType type) => type == TileType.Floor || IsPaving(type);

        // Whether a tile of this type has its centre within reach of the position.
        public bool Near(Vec2 position, TileType type, float reach)
        {
            int range = (int)MathF.Ceiling(reach) + 1;
            int cx = Cell(position.X);
            int cy = Cell(position.Y);
            for (int y = cy - range; y <= cy + range; y++)
            {
                for (int x = cx - range; x <= cx + range; x++)
                {
                    if (this[x, y] == type && InBounds(x, y) && Vec2.Distance(position, Center(x, y)) <= reach) return true;
                }
            }
            return false;
        }

        // The outermost ring, which nobody can dig through; and the whole pocket above the world, where nothing is dug or
        // built (Docs/WorldRework.md 주머니의 모양).
        public bool IsEdge(int x, int y) => x <= 0 || y <= 0 || x >= Width - 1 || y >= Height - 1;

        public static int Cell(float coordinate) => (int)MathF.Floor(coordinate);

        public static Vec2 Center(int x, int y) => new Vec2(x + 0.5f, y + 0.5f);
    }
}
