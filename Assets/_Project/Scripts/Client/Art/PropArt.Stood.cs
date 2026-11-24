using System;
using Game.Core;

namespace Game.Client
{
    // What used to be stamped on the floor as a square of its tile and now stands on it (2026-11-25, "입체감 없이 타일
    // 형태로 칸 꽉 채우는거 다 찾아서 점검해"), drawn by the standing layer like the fences (StandArt, sorted by foot, a
    // shadow under it, the lowest row its darkest):
    //  - a door in a wall or alone (Door): a framed leaf seen from the front across an east-west run, its jambs and the
    //    leaf's top seen from above in a north-south one, swung aside while someone passes; a locked one banded in
    //    iron with a padlock;
    //  - a trap room's grate (Railing): iron bars with spear heads that join into a run and into the rock beside them;
    //  - the hung walls (HungWall): a wall block like the iron wall's, in stone, the tapestry or the shield on its face;
    //  - the things drawn from the side inside their tile (a power pole, a solar panel, a scarecrow, a crab pot, a
    //    fold, a lever, a sprinkler, a water wheel's stand): the same drawing on a longer pole where it has one, stood
    //    on its foot so people pass behind and in front of it.
    static partial class PropArt
    {
        // ---- Doors ----

        static readonly Rgba DoorLight = Rgba.Hex("#C89A5E"), DoorMain = Rgba.Hex("#A2743E"), DoorShade = Rgba.Hex("#7A5228"),
            DoorDark = Rgba.Hex("#4E321A"), JambLight = Rgba.Hex("#8E6844"), JambMain = Rgba.Hex("#684A30"), JambShade = Rgba.Hex("#4A3422"),
            JambDark = Rgba.Hex("#2E2014");
        static readonly Rgba LockBrass = Rgba.Hex("#F0BE40"), LockShade = Rgba.Hex("#B8862A");

        // Rows of headroom over the tile in a door's picture.
        public const int DoorHead = 10;

        // A door's shape for the standing layer: 1 across an east-west run (seen from the front), 2 open, 4 locked.
        public static int DoorShape(bool along, bool open, bool locked) => (along ? 1 : 0) | (open ? 2 : 0) | (locked ? 4 : 0);

        public static PixelImage Door(int shape)
        {
            bool along = (shape & 1) != 0, open = (shape & 2) != 0, locked = (shape & 4) != 0;
            return along ? DoorAlong(open, locked) : DoorAcross(open, locked);
        }

        // A jamb: a post four wide from `top` to `foot`, lit on the left, its cap light and its foot the darkest.
        static void Jamb(PixelImage g, int x, int top, int foot, int wide = 3)
        {
            for (int y = top; y <= foot; y++)
            {
                if (g.Inside(x - 1, y)) g[x - 1, y] = Ink;
                for (int i = 0; i < wide; i++) g[x + i, y] = i == 0 ? JambLight : i >= wide - 1 - wide / 4 ? JambShade : JambMain;
                if (g.Inside(x + wide, y)) g[x + wide, y] = Ink;
            }
            for (int i = 0; i < wide; i++)
            {
                g[x + i, top - 1] = Ink;
                g[x + i, top] = JambLight.Mix(Rgba.White, 0.2f);
                g[x + i, foot] = JambDark;
            }
        }

        // Seen from the front, in a wall that runs east and west: two jambs and a lintel, the leaf of planks between
        // them with two bands and a handle (a padlock when locked); open, the leaf stands swung toward the viewer on
        // the west jamb, edge on, and the way through is clear.
        static PixelImage DoorAlong(bool open, bool locked)
        {
            const int h = T + DoorHead;
            var g = new PixelImage(T, h);
            int foot = h - 1;
            Shade(g, 8, foot - 0.5f, 15, 90);
            // The leaf.
            if (!open)
            {
                for (int x = 3; x <= 12; x++)
                {
                    for (int y = 5; y <= foot - 1; y++)
                    {
                        int plank = (x - 3) % 5;
                        g[x, y] = plank == 4 ? DoorShade : plank == 0 ? DoorLight : DoorMain;
                    }
                    g[x, foot - 1] = DoorDark;
                    g[x, foot] = Ink;
                    // Two bands across: iron when locked, a darker rail of wood otherwise.
                    foreach (int y in new[] { 9, 19 })
                    {
                        g[x, y] = locked ? Iron.Main : DoorShade;
                        g[x, y + 1] = locked ? Iron.Dark : DoorDark;
                    }
                }
                if (locked)
                {
                    // A brass padlock on the middle of the leaf.
                    Rect(g, 7, 13, 3, 3, LockBrass);
                    Rect(g, 7, 15, 3, 1, LockShade);
                    g[8, 14] = Ink;
                    g[7, 12] = Iron.Light;
                    g[9, 12] = Iron.Light;
                    g[8, 11] = Iron.Light;
                }
                else
                {
                    g[10, 14] = LockBrass;
                    g[10, 15] = LockShade;
                }
            }
            else
            {
                // The dark of the way through at the sill, and the leaf edge on beside the west jamb.
                for (int x = 3; x <= 12; x++) g.Blend(x, foot, new Rgba(0, 0, 0, 70));
                for (int y = 6; y <= foot + 0; y++)
                {
                    g[3, y] = DoorLight;
                    g[4, y] = DoorMain;
                    g[5, y] = DoorShade;
                    g[6, y] = Ink;
                }
                foreach (int y in new[] { 9, 19 }) for (int x = 3; x <= 5; x++) g[x, y] = locked ? Iron.Main : DoorDark;
                for (int x = 3; x <= 5; x++)
                {
                    g[x, 5] = Ink;
                    g[x, foot] = DoorDark;
                }
            }
            // The frame over it: jambs either side and the lintel across their heads.
            Jamb(g, 0, 3, foot);
            Jamb(g, 13, 3, foot);
            for (int x = 0; x < T; x++)
            {
                g[x, 0] = Ink;
                g[x, 1] = JambLight.Mix(Rgba.White, 0.2f);
                g[x, 2] = JambLight;
                g[x, 3] = JambMain;
                g[x, 4] = x >= 3 && x <= 12 ? JambDark : g[x, 4];
            }
            g[0, 0] = Rgba.Clear;
            g[T - 1, 0] = Rgba.Clear;
            return g;
        }

        // In a wall that runs north and south (the way through goes east and west): a jamb at the north end and one at
        // the south, the lintel between them seen from above; shut, the leaf's planked top lies between the jambs; open,
        // the leaf stands swung east from the north jamb, its face to the viewer, over the next tile.
        static PixelImage DoorAcross(bool open, bool locked)
        {
            const int h = T + DoorHead;
            var g = new PixelImage(open ? T + GateSwing : T, h);
            int foot = h - 1;
            Shade(g, 8, foot - 0.5f, 11, 100);
            // The north jamb stands on the tile's north edge: its foot six rows into the tile.
            Jamb(g, 4, 2, DoorHead + 5, 8);
            if (!open)
            {
                // The lintel over the way, seen from above, and the leaf's top under it: planks between the frame's two
                // sides, a band at each third.
                for (int y = 3; y <= DoorHead + 5; y++)
                {
                    g[3, y] = Ink;
                    g[4, y] = JambLight;
                    g[5, y] = JambMain;
                    g[6, y] = DoorLight;
                    g[7, y] = DoorLight;
                    g[8, y] = DoorMain;
                    g[9, y] = DoorShade;
                    g[10, y] = JambMain;
                    g[11, y] = JambShade;
                    g[12, y] = Ink;
                    if (y % 5 == 2)
                    {
                        for (int x = 6; x <= 9; x++) g[x, y] = locked ? Iron.Main : DoorDark;
                    }
                }
                for (int x = 4; x <= 11; x++) g[x, 2] = Ink;
                if (locked) Rect(g, 7, 9, 2, 2, LockBrass);
            }
            else
            {
                const int left = 10, right = T + GateSwing - 2;
                Shade(g, (left + right) / 2f + 1, DoorHead + 5.5f, right - left, 80);
                for (int x = left; x <= right; x++)
                {
                    g[x, 3] = Ink;
                    for (int y = 4; y <= DoorHead + 4; y++)
                    {
                        int plank = (x - left) % 5;
                        g[x, y] = plank == 4 ? DoorShade : plank == 0 ? DoorLight : DoorMain;
                    }
                    foreach (int y in new[] { 6, 12 }) g[x, y] = locked ? Iron.Main : DoorShade;
                    g[x, DoorHead + 4] = DoorDark;
                    g[x, DoorHead + 5] = Ink;
                }
                for (int y = 3; y <= DoorHead + 5; y++) g[right + 1, y] = Ink;
                if (locked) Rect(g, right - 3, 8, 2, 3, LockBrass);
                else g[right - 2, 9] = LockBrass;
            }
            // The south jamb in front of it all.
            Jamb(g, 4, DoorHead + 2, foot, 8);
            return g;
        }

        // ---- A trap's grate ----

        static readonly Ramp Bar = new Ramp(Rgba.Hex("#C8D0DC"), Rgba.Hex("#8E98A6"), Rgba.Hex("#5E6672"), Rgba.Hex("#363A44"));

        // Iron bars that join into a run (Joins.Mask): a post in the middle, across an east-west side two rails with
        // upright bars and spear heads between them, along a north-south side the rails' tops and the bars' heads one
        // behind another. Sixteen wide, twenty-four tall, sorted at its post's foot like a fence.
        public static PixelImage Railing(int mask)
        {
            var g = new PixelImage(T, T + FenceHead);
            void P(int x, int y, Rgba c)
            {
                if (g.Inside(x, y + FenceHead)) g[x, y + FenceHead] = c;
            }
            // A bar from its spear head down to the foot.
            void Upright(int x, int top, int foot, bool thick)
            {
                P(x, top - 1, Bar.Light);
                for (int y = top; y <= foot; y++)
                {
                    P(x, y, Bar.Light);
                    if (thick) P(x + 1, y, Bar.Shade);
                    else if (g.Inside(x + 1, y + FenceHead) && g[x + 1, y + FenceHead].A == 0) P(x + 1, y, new Rgba(0, 0, 0, 80));
                }
                P(x, foot, Bar.Dark);
                if (thick) P(x + 1, foot, Bar.Dark);
            }
            void Rails(int x0, int x1)
            {
                Shade(g, (x0 + x1) / 2f + 0.5f, FenceHead + FencePost + 0.5f, x1 - x0 + 2, 90);
                foreach (int r in new[] { -3, 7 })
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        P(x, r - 1, Ink);
                        P(x, r, Bar.Light);
                        P(x, r + 1, Bar.Shade);
                        P(x, r + 2, Ink);
                    }
                }
                for (int x = x0 + ((x0 & 1) == 0 ? 1 : 0); x <= x1; x += 3) Upright(x, -6, FencePost, false);
            }
            bool east = (mask & Joins.E) != 0, west = (mask & Joins.W) != 0, north = (mask & Joins.N) != 0, south = (mask & Joins.S) != 0;
            // A run north and south is seen from above and behind: the top rail as a beaded strip (a bar's head every
            // third row) from the post behind to the one in front. Only its southern end, or a tile with an arm east or
            // west, shows a post's whole height.
            void Strip(int from, int to)
            {
                for (int y = from; y <= to; y++)
                {
                    bool head = ((y % 3) + 3) % 3 == 0;
                    P(5, y, Ink);
                    P(6, y, Bar.Light);
                    P(7, y, head ? Rgba.White : Bar.Main);
                    P(8, y, head ? Bar.Light : Bar.Shade);
                    P(9, y, Bar.Dark);
                    P(10, y, Ink);
                    // Its height, as a shadow on the floor to the east.
                    if (y >= 0)
                    {
                        g.Blend(11, y + FenceHead, new Rgba(0, 0, 0, 90));
                        g.Blend(12, y + FenceHead, new Rgba(0, 0, 0, 50));
                    }
                }
            }
            if (south && !east && !west)
            {
                Strip(north ? -8 : -6, T - 1);
                if (!north) for (int x = 6; x <= 9; x++) P(x, -7, Ink);
                return g;
            }
            Shade(g, 8, FenceHead + FencePost + 0.5f, 5, 105);
            if (west) Rails(0, 5);
            if (east) Rails(10, 15);
            // The post: a thicker bar with a spear head of its own.
            for (int y = -7; y <= FencePost; y++)
            {
                P(5, y, Ink);
                P(6, y, Bar.Light);
                P(7, y, Bar.Main);
                P(8, y, Bar.Shade);
                P(9, y, Bar.Dark);
                P(10, y, Ink);
            }
            for (int x = 6; x <= 9; x++)
            {
                P(x, FencePost, Bar.Dark.Scale(0.7f));
                P(x, FencePost + 1, Ink);
                P(x, -8, Ink);
            }
            P(7, -8, Bar.Light);
            P(8, -8, Bar.Main);
            P(7, -9, Ink);
            P(8, -9, Ink);
            if (south) Strip(FencePost + 1, T - 1);
            return g;
        }

        // ---- Hung walls ----

        // A wall hung with a tapestry or a shield (Docs/Crafting.md): a block of dressed stone that joins its own kind
        // and the iron wall, the hanging on its face.
        public static PixelImage HungWall(TileType type, int sides)
        {
            var image = Wall(sides, DarkStone, castle: true, merlons: false);
            const int face = T;
            if ((sides & 1) == 0) for (int x = 0; x < T; x++) image[x, 0] = Ink;
            if (type == TileType.TapestryWall)
            {
                // A rod and a red cloth with a gold band, hanging from the wall's head over its face.
                Rect(image, 3, face - 3, 10, 1, Ink);
                Rect(image, 3, face - 2, 10, 1, Gold.Main);
                Rect(image, 4, face - 1, 8, 7, Royal.Main);
                Rect(image, 4, face - 1, 1, 7, Royal.Light);
                Rect(image, 11, face - 1, 1, 7, Royal.Shade);
                Rect(image, 5, face + 1, 6, 1, Gold.Main);
                Rect(image, 7, face + 3, 2, 2, Gold.Light);
                // A notched hem.
                image[5, face + 5] = Royal.Dark;
                image[8, face + 5] = Royal.Dark;
                image[10, face + 5] = Royal.Dark;
            }
            else
            {
                // A round shield on crossed blades.
                for (int i = 0; i < 8; i++)
                {
                    image[4 + i, face - 2 + i] = Iron.Light;
                    image[11 - i, face - 2 + i] = Iron.Light;
                }
                for (int y = -1; y <= 5; y++)
                {
                    for (int x = 5; x <= 10; x++)
                    {
                        float dx = x - 7.5f, dy = y - 2f;
                        float far = dx * dx + dy * dy;
                        if (far > 10.5f) continue;
                        image[x, face + y] = far > 6.5f ? Iron.Main : dx + dy < 0 ? Royal.Light : Royal.Main;
                    }
                }
                image[7, face + 2] = Gold.Light;
                image[8, face + 2] = Gold.Main;
            }
            return image;
        }

        // ---- Tiles stood on their foot ----

        // The kinds whose tile is a drawing from the side: stood on the floor by the standing layer as they are, or on
        // a longer pole.
        public static bool IsStood(TileType type) => type == TileType.PowerPole || type == TileType.SolarPanel || type == TileType.Scarecrow
            || type == TileType.CrabPot || type == TileType.Fold || type == TileType.Lever || type == TileType.Sprinkler || type == TileType.WaterWheel;

        static readonly PixelImage[] StoodPictures = new PixelImage[256];

        static PixelImage Stood(TileType type) => !IsStood(type) ? null : StoodPictures[(int)type] ??= type switch
        {
            // The pole three times as tall, so its arm and wires pass over people's heads.
            TileType.PowerPole => Grounded(Stretched(TileArt.Object(type), 8, PoleRise), 4),
            TileType.SolarPanel => Grounded(Stretched(TileArt.Object(type), 13, 3), 8),
            TileType.Scarecrow => Grounded(Stretched(TileArt.Object(type), 13, 5), 5),
            TileType.WaterWheel => WheelStand(),
            TileType.Fold => Grounded(TileArt.Object(type), 14),
            _ => TileArt.Object(type),
        };

        // How many rows taller a stood power pole is than its tile, and so how high its wires hang (WorldView.DrawWires).
        public const int PoleRise = 12;
        public const float PoleWire = (TileArt.Size - 3 + PoleRise) / (float)TileArt.Size;

        // How far above the tile's middle a water wheel turns on its stand, in pixels.
        public const int WheelLift = 4;

        // The drawing with one of its rows (a pole's) repeated `extra` more times.
        static PixelImage Stretched(PixelImage tile, int row, int extra)
        {
            var image = new PixelImage(tile.Width, tile.Height + extra);
            for (int y = 0; y < image.Height; y++)
            {
                int from = y <= row ? y : y <= row + extra ? row : y - extra;
                for (int x = 0; x < tile.Width; x++) image[x, y] = tile[x, from];
            }
            return image;
        }

        // The drawing over a crisp shadow under its foot, `width` pixels wide (Shade), in place of its own soft one.
        static PixelImage Grounded(PixelImage picture, float width)
        {
            var image = new PixelImage(picture.Width, picture.Height);
            int foot = picture.Height - 1;
            while (foot > 0 && !RowSolid(picture, foot)) foot--;
            Shade(image, picture.Width / 2f, foot + 0.5f, width + 2);
            for (int y = 0; y < picture.Height; y++)
                for (int x = 0; x < picture.Width; x++)
                    if (picture[x, y].A >= 200) image[x, y] = picture[x, y];
            return image;
        }

        static bool RowSolid(PixelImage picture, int y)
        {
            for (int x = 0; x < picture.Width; x++) if (picture[x, y].A >= 200) return true;
            return false;
        }

        // A water wheel's stand: two timber legs braced apart with the axle's bearing between their heads, tall enough
        // that the wheel (drawn by the view, turning) clears the floor.
        static PixelImage WheelStand()
        {
            const int h = T + WheelLift;
            var g = new PixelImage(T, h);
            Shade(g, 8, h - 1.5f, 15);
            int axle = h - T / 2 - WheelLift;
            foreach (int x in new[] { 1, 12 })
            {
                for (int y = axle - 1; y < h - 1; y++)
                {
                    g[x, y] = Ink;
                    g[x + 1, y] = Wood.Main;
                    g[x + 2, y] = Wood.Shade;
                    if (x == 12) g[x + 3, y] = Ink;
                }
                g[x + 1, axle - 2] = Ink;
                g[x + 2, axle - 2] = Ink;
                g[x + 1, axle - 1] = Wood.Light;
                g[x + 1, h - 2] = Wood.Dark;
                g[x + 2, h - 2] = Wood.Dark;
            }
            g[4, h - 2] = Ink;
            // A sill between the feet.
            for (int x = 4; x <= 11; x++)
            {
                g[x, h - 4] = Ink;
                g[x, h - 3] = Wood.Shade;
                g[x, h - 2] = Wood.Dark;
            }
            return g;
        }
    }
}
