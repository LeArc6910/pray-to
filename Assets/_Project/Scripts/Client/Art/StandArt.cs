using Game.Core;

namespace Game.Client
{
    // How the things that stand on the floor are drawn apart from the floor and put in front of or behind each other
    // (Docs/BiomeRework.md 4-3 그리는 규칙, 묶음 B0). WorldView draws by this and so does the preview tool (ArtPreview's
    // Scene and DepthPreview), so the pictures are the game's.
    //  - Everything that stands (the places' structures, what players build that stands, fences, hedges, walls, a gate
    //    in a fence, a door, a trap's grate, the floor's decor, people, monsters) is its own sprite, sorted by its foot's y to the pixel
    //    (WorldView.FootOrder: lower on the screen is in front). At the same row: decor, then props, then bodies.
    //  - The foot is the bottom edge of the footprint (the tiles it blocks), in the middle; a fence sorts at its posts'
    //    feet, a hedge at its mass's. Only the footprint blocks: the picture rises above it (and a canopy or roof can be
    //    walked behind).
    //  - The shadow is a crisp oval straight under the foot, about four fifths of the footprint wide and a third as
    //    tall, black at alpha 0.35 to 0.45; the lowest row of the thing is its darkest (the contact row).
    //  - A canopy or roof (CanopyRows) fades to FadeAlpha while a player's or a monster's feet are behind the thing and
    //    its body reaches into the canopy (Hides), and back when they leave; the [설정] 물체 뒤 흐리게 turns this off.
    static class StandArt
    {
        public const int Pixels = 16;
        // At the same foot row: the floor's decor, then props and fences, then creatures and people.
        public const int DecorTie = 0, PropTie = 1, BodyTie = 2;
        public const int Ties = 3;
        // A faded canopy's alpha, and how long fading takes (instant in the playtests and with 움직임: 줄이기).
        public const float FadeAlpha = 0.45f, FadeSeconds = 0.15f;
        // How tall a standing body is above its feet, for whether it reaches into a canopy.
        public const float BodyHeight = 1.1f;

        public enum Kind : byte
        {
            None,
            Prop,
            Joined,
            Gate,
            // A door in a wall or alone (2026-11-25, PropArt.Door): framed, seen from the front or the side.
            Door,
        }

        public static Kind KindAt(TileMap map, int x, int y)
        {
            var type = map[x, y];
            if (type == TileType.StructurePart) return Kind.None;
            if (TileMap.IsDoor(type)) return Joins.Gate(map, x, y) != null ? Kind.Gate : Kind.Door;
            if (Joins.Joined(type)) return Kind.Joined;
            return Structures.IsDecor(type) || PropArt.Stands(type) ? Kind.Prop : Kind.None;
        }

        // What shape the thing at (x, y) is drawn in: the joined sides for a fence, hedge or wall, for a gate 1 across an
        // east-west run and 0 in a north-south one, plus 2 open; 0 for anything else.
        // A biome's prop: which of its drawings and whether mirrored (BiomeArt.Variant). The places' tall tree: 1 in the
        // dirt cave (a mine timber with a lantern), 2 in the mushroom forest (the forest's great mushroom), 0 anywhere
        // else or when the biome is not given (the old blue mushroom tree). A place's building or dressing that has a
        // biome look (PropArt.HasBiomeLook): PropArt.CaveLook, ForestLook or 0 the same way.
        public static int ShapeAt(TileMap map, int x, int y, bool open, Biome? biome = null)
        {
            var kind = KindAt(map, x, y);
            if (kind == Kind.Joined) return Joins.Mask(map, x, y);
            if (kind == Kind.Gate) return (Joins.Gate(map, x, y) == true ? 1 : 0) | (open ? 2 : 0);
            var type = map[x, y];
            if (kind == Kind.Door) return PropArt.DoorShape(Joins.DoorAlong(map, x, y), open, type == TileType.LockedDoor);
            if (BiomeProps.Is(type)) return BiomeArt.Variant(type, x, y);
            // 3 in the ice cave (a frozen tree), 4 in the sand cave (a cave palm): 묶음 B2a.
            if (type == TileType.MushroomTree) return biome == Biome.Cave ? 1 : biome == Biome.Mushroom ? 2 : biome == Biome.Ice ? 3 : biome == Biome.Sand ? 4
                // 5 in the crystal cave (a crystal growth), 6 by the lake (a willow): 묶음 B3a.
                : biome == Biome.Crystal ? 5 : biome == Biome.Lake ? 6
                // 7 in the lava zone (a charred tree still alight), 8 in the abyss (a tree of stars): 묶음 B4a.
                : biome == Biome.Lava ? 7 : biome == Biome.Abyss ? 8 : 0;
            if (type == TileType.RingCap || type == TileType.IceColumn) return BiomeArt.Variant(type, x, y);
            // The places' buildings and dressing in the look of the biome under them (묶음 B1c, PropArt.Places.cs).
            return PropArt.BiomeLook(type, biome);
        }

        // The picture for a tile's kind and shape (ShapeAt); lit: a house's windows lit (evening and night).
        public static PixelImage Picture(TileType type, Kind kind, int shape, bool lit)
        {
            if (kind == Kind.Gate) return PropArt.FenceGate((shape & 1) != 0, (shape & 2) != 0);
            if (kind == Kind.Door) return PropArt.Door(shape);
            if (BiomeProps.Is(type)) return BiomeArt.Prop(type, shape);
            if (type == TileType.MushroomTree && shape != 0) return BiomeArt.PlaceTree(shape == 2 ? Biome.Mushroom : shape == 3 ? Biome.Ice : shape == 4 ? Biome.Sand : shape == 5 ? Biome.Crystal : shape == 6 ? Biome.Lake : shape == 7 ? Biome.Lava : shape == 8 ? Biome.Abyss : Biome.Cave);
            if (type == TileType.RingCap) return BiomeArt.RingCap(shape);
            if (type == TileType.IceColumn) return BiomeArt.IceColumn(shape);
            if (BiomeLandmarks.Is(type)) return PropArt.Landmark(type);
            if (shape != 0 && PropArt.HasBiomeLook(type))
            {
                var look = PropArt.PlaceProp(type, shape);
                return PropArt.LitAtNight(type) && !lit ? PropArt.Daylit(look) : look;
            }
            if (PropArt.LitAtNight(type)) return lit ? PropArt.Prop(type) : PropArt.Daylit(PropArt.Prop(type));
            return PropArt.Prop(type, kind == Kind.Joined ? shape : 0);
        }

        // Where on its picture, from the left, a thing stands: the middle, but for an open gate in a north-south run,
        // whose leaf swings over the tile to the east.
        public static int PivotX(Kind kind, int shape, PixelImage picture) => (kind == Kind.Gate || kind == Kind.Door) && (shape & 1) == 0 && (shape & 2) != 0
            ? Pixels / 2 : picture.Width / 2;

        // How far above the bottom edge of its footprint a thing's foot line is, in tiles.
        public static float FootLift(TileType type, Kind kind) =>
            kind == Kind.Gate || type == TileType.Fence || type == TileType.Grate ? PropArt.FenceFoot / (float)Pixels
            : type == TileType.Hedge ? PropArt.HedgeFoot / (float)Pixels
            : BiomeProps.Is(type) || type == TileType.IceColumn ? BiomeArt.FootRows / (float)Pixels
            : type == TileType.RingCap ? BiomeArt.RingFoot / (float)Pixels
            : 0;

        // The rows from the top of the picture that are its canopy or roof (fading when someone is behind), none for 0.
        public static int CanopyRows(TileType type) => type switch
        {
            TileType.MushroomTree => 26,
            TileType.CherryTree => 26,
            TileType.Cottage => 46,
            TileType.Inn or TileType.Smithy or TileType.Shop or TileType.Temple or TileType.Barracks or TileType.Stable or TileType.Palace or TileType.MarketStall => PropArt.TownCanopy(type),
            TileType.Cabin => 44,
            TileType.Tower => 30,
            TileType.BigTent => 12,
            TileType.Lighthouse => 48,
            TileType.MineEntrance => 20,
            TileType.MinerHut or TileType.FrozenBeast or TileType.GiantSkull => PropArt.LandmarkCanopy(type),
            TileType.GiantGeode or TileType.CrystalArch or TileType.SunkenBoat or TileType.OldWheel or TileType.ArchPier => PropArt.LandmarkCanopy(type),
            TileType.FallenChimney or TileType.GiantAnvil or TileType.BrokenGate or TileType.StarAltar => PropArt.LandmarkCanopy(type),
            _ => BiomeArt.CanopyRows(type),
        };

        // The canopy's box in world units for a thing whose picture (w by h pixels, canopy rows on top) stands with its
        // pivot at (x, foot).
        public static (float Left, float Right, float Bottom, float Top) Canopy(float x, float foot, int width, int height, int rows) =>
            (x - width / 2f / Pixels, x + width / 2f / Pixels, foot + (height - rows) / (float)Pixels, foot + height / (float)Pixels);

        // Whether a body with its feet at (fx, fy) is behind a thing whose foot line is footY and reaches into its canopy.
        public static bool Hides(float fx, float fy, float footY, (float Left, float Right, float Bottom, float Top) canopy) =>
            fy > footY && fx > canopy.Left + 0.15f && fx < canopy.Right - 0.15f && fy + BodyHeight > canopy.Bottom && fy < canopy.Top;

        // The decor on a tile, if any shows: the pick for the tile, kept only on clear open floor (Decor.Clear) and away
        // from what the caller keeps clear (the places, the arenas).
        public static DecorPick DecorAt(TileMap map, ulong seed, Biome biome, int x, int y, System.Func<int, int, bool> keptClear)
        {
            var pick = Decor.On(map, seed, biome, x, y);
            if (!pick.Any || keptClear != null && keptClear(x, y)) return default;
            return pick;
        }

        // Where a decor thing's foot is: the tile's middle, nudged by its pick, in tiles. Its picture's bottom edge is
        // DecorDrop below it (the rows of shadow under the contact row).
        public static (float X, float Y) DecorFoot(int x, int y, DecorPick pick) =>
            (x + 0.5f + pick.OffsetX / (float)Pixels, y + 0.5f + pick.OffsetY / (float)Pixels);

        public const float DecorDrop = 2f / Pixels;

        // A decor thing's picture, mirrored by its pick (its shadow and light are near enough symmetric to mirror).
        public static PixelImage DecorPicture(DecorPick pick, Biome biome, int frame = 0)
        {
            var image = DecorArt.Image(pick.Kind, biome, pick.Variant, frame);
            return pick.Flip ? image.FlippedX() : image;
        }
    }
}
