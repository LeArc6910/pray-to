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

    public sealed class TileMap
    {
        readonly TileType[] _tiles;

        public TileMap(int width, int height, TileType fill = TileType.Wall)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
            _tiles = new TileType[width * height];
            if (fill != default) Array.Fill(_tiles, fill);
        }

        public int Width { get; }
        public int Height { get; }

        // Outside the map reads as wall, so nothing can walk off the edge.
        public TileType this[int x, int y]
        {
            get => InBounds(x, y) ? _tiles[y * Width + x] : TileType.Wall;
            set
            {
                if (!InBounds(x, y)) throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {y}) is outside the map.");
                _tiles[y * Width + x] = value;
            }
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public bool IsSolid(int x, int y)
        {
            var type = this[x, y];
            return type != TileType.Floor && type != TileType.Conveyor && type != TileType.Splitter && type != TileType.Filter && type != TileType.Lava && !IsPaving(type)
                && type != TileType.Door && !IsSoil(type);
        }

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

        // What stops a monster: everything solid, and doors.
        public bool BlocksMonsters(int x, int y) => IsSolid(x, y) || this[x, y] == TileType.Door;

        // A floor laid over bare floor (StoneFloor, BrickFloor, GlowFloor): walked on like floor, built on like floor.
        public static bool IsPaving(TileType type) => type == TileType.StoneFloor || type == TileType.BrickFloor || type == TileType.GlowFloor;

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

        // The outermost ring, which nobody can dig through.
        public bool IsEdge(int x, int y) => x <= 0 || y <= 0 || x >= Width - 1 || y >= Height - 1;

        public static int Cell(float coordinate) => (int)MathF.Floor(coordinate);

        public static Vec2 Center(int x, int y) => new Vec2(x + 0.5f, y + 0.5f);
    }
}
