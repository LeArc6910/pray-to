using System;
using Game.Core;

namespace Game.Client
{
    // The towns' buildings of version 10 (Docs/Towns.md, 2026-11-27): drawn like the cottage (a tiled roof seen from the
    // front, plastered or stone walls between posts, warm windows, a door and step, a stone footing), each a tile wide
    // for every tile of its footprint (16 pixels) and its roof standing above it. The inn with its sign and balcony, the
    // smithy with a glowing forge under an open lean-to, a shop with a striped awning and wares, a temple of pale stone
    // with columns and a rose window, the barracks with banners, a stable of open stalls, the palace (white and gold, a
    // blue roof, towers with domes, a great door, banners), and a market stall.
    static partial class PropArt
    {
        static readonly Ramp Awning = new Ramp(Rgba.Hex("#FFE4A8"), Rgba.Hex("#E0B060"), Rgba.Hex("#A87A38"), Rgba.Hex("#6E4A1E"));
        static readonly Ramp Marble = new Ramp(Rgba.Hex("#FFFDF6"), Rgba.Hex("#ECE6D8"), Rgba.Hex("#C8C0AE"), Rgba.Hex("#948C7A"));

        // A roof of tile courses from y0 to y1 across x0..x1, narrowing toward the ridge, shade at the right and the eave.
        static void TiledRoof(PixelImage body, int x0, int x1, int y0, int y1, Ramp roof, int course = 4)
        {
            for (int y = y0; y < y1; y++)
            {
                int inset = Math.Max(0, (16 - (y - y0)) / 4);
                for (int x = x0 + inset; x < x1 - inset; x++)
                {
                    int c = (y - y0) / course;
                    bool seam = (y - y0) % course == course - 1 || (x + c * 3) % 7 == 0;
                    float t = (x - x0) / (float)Math.Max(1, x1 - x0);
                    var color = t > 0.84f ? roof.Shade : y < y0 + 4 ? roof.Light : roof.Main;
                    body[x, y] = seam ? color.Scale(0.78f) : color;
                }
            }
            Rect(body, x0, y1 - 2, x1 - x0, 2, roof.Dark);
        }

        // Walls from y0 to y1 across x0..x1: plaster (or stone) between posts, shade under the eave and at the right.
        static void Walls(PixelImage body, int x0, int x1, int y0, int y1, Ramp wall, Ramp? posts = null, int every = 0)
        {
            Rect(body, x0, y0, x1 - x0, y1 - y0, wall.Main);
            Rect(body, x0, y0, x1 - x0, 3, wall.Shade);
            Rect(body, x1 - 3, y0, 3, y1 - y0, wall.Shade);
            if (posts is Ramp p && every > 0)
            {
                for (int x = x0; x < x1 - 2; x += every) Rect(body, x, y0, 3, y1 - y0, p.Main);
                Rect(body, x1 - 3, y0, 3, y1 - y0, p.Main);
                Rect(body, x0, y0 + (y1 - y0) / 2 - 1, x1 - x0, 2, p.Main);
            }
        }

        static void LitWindow(PixelImage body, int x, int y, int w = 9, int h = 8)
        {
            Rect(body, x, y, w, h, DarkWood.Dark);
            Rect(body, x + 1, y + 1, w - 2, h - 2, WindowLight);
            Rect(body, x + 1, y + 1, 3, 2, WindowCore);
            Rect(body, x + w / 2, y + 1, 1, h - 2, DarkWood.Main);
            Rect(body, x + 1, y + h / 2, w - 2, 1, DarkWood.Main);
        }

        static void Door(PixelImage body, int x, int y, int w, int h, Ramp wood)
        {
            Rect(body, x, y, w, h, wood.Shade);
            for (int i = x + 1; i < x + w - 1; i += 3) Rect(body, i, y + 1, 1, h - 1, wood.Dark);
            Rect(body, x, y, w, 1, wood.Light);
            body[x + w - 3, y + h / 2] = Gold.Main;
        }

        static void Footing(PixelImage body, int x0, int x1, int y)
        {
            Rect(body, x0, y, x1 - x0, 3, DarkStone.Main);
            for (int x = x0 + 2; x < x1 - 2; x += 5) body[x, y + 1] = DarkStone.Dark;
        }

        static void Pennant(PixelImage body, int x, int top, Ramp cloth)
        {
            Rect(body, x, top, 1, 14, DarkWood.Dark);
            Rect(body, x + 1, top + 1, 6, 4, cloth.Main);
            Rect(body, x + 1, top + 1, 6, 1, cloth.Light);
            body[x + 7, top + 2] = cloth.Main;
            body[x + 1, top] = Gold.Main;
        }

        static PixelImage Building(int w, int h, Action<PixelImage> draw)
        {
            var body = new PixelImage(w, h);
            draw(body);
            return Finish(body, ShadowLayer(w, h, w / 2f + 2, h - 3, w / 2f, 4));
        }

        // Six tiles by three: two storeys, a balcony, a hanging sign with a mug, a chimney.
        static PixelImage Inn() => Building(96, 92, b =>
        {
            Column(b, 74, 4, 8, 18, Stone);
            Rect(b, 73, 3, 10, 2, Stone.Dark);
            TiledRoof(b, 2, 94, 12, 40, RoofMoss);
            Walls(b, 5, 91, 40, 86, Plaster, DarkWood, 21);
            // The balcony across the upper storey.
            Rect(b, 5, 60, 86, 2, Wood.Main);
            for (int x = 7; x < 90; x += 4) Rect(b, x, 56, 1, 4, Wood.Shade);
            Rect(b, 5, 55, 86, 1, Wood.Light);
            foreach (int x in new[] { 10, 31, 56, 77 }) LitWindow(b, x, 44, 9, 8);
            foreach (int x in new[] { 10, 68, 77 }) LitWindow(b, x, 66, 9, 9);
            Door(b, 40, 68, 14, 18, Wood);
            Rect(b, 37, 86, 20, 2, Stone.Light);
            // The sign: a board on an arm, a mug on it.
            Rect(b, 58, 64, 10, 1, DarkWood.Dark);
            Rect(b, 59, 65, 8, 7, Wood.Light);
            Rect(b, 61, 67, 3, 4, Gold.Main);
            b[64, 68] = Gold.Shade;
            Footing(b, 4, 92, 86);
        });

        // Five tiles by three: a stone house with a big chimney, an open lean-to with the forge's glow, an anvil.
        static PixelImage Smithy() => Building(80, 82, b =>
        {
            Column(b, 12, 2, 10, 30, DarkStone);
            Rect(b, 11, 1, 12, 2, DarkStone.Dark);
            b[16, 0] = Rgba.Hex("#8A8478");
            TiledRoof(b, 2, 60, 14, 38, RoofRed);
            Walls(b, 5, 57, 38, 76, Stone);
            for (int y = 42; y < 76; y += 6)
                for (int x = 6 + (y / 6 % 2) * 4; x < 56; x += 9) Rect(b, x, y, 1, 5, Stone.Shade);
            LitWindow(b, 12, 46);
            Door(b, 32, 58, 12, 18, DarkWood);
            // The lean-to: posts, a sloped plank roof, the forge inside glowing.
            Rect(b, 57, 34, 21, 4, Wood.Main);
            Rect(b, 57, 34, 21, 1, Wood.Light);
            foreach (int x in new[] { 58, 75 }) Rect(b, x, 38, 2, 38, DarkWood.Main);
            Rect(b, 60, 50, 15, 26, Rgba.Hex("#2A1E18"));
            Rect(b, 62, 62, 11, 8, DarkStone.Main);
            Rect(b, 63, 63, 9, 4, Flame);
            Rect(b, 65, 64, 5, 2, FlameCore);
            Rect(b, 62, 70, 10, 3, Iron.Main);
            Rect(b, 64, 69, 6, 1, Iron.Light);
            Footing(b, 4, 78, 76);
        });

        // Four tiles by three: a narrow house with a striped awning over a counter of wares.
        static PixelImage Shop() => Building(64, 78, b =>
        {
            TiledRoof(b, 2, 62, 10, 34, RoofBlue);
            Walls(b, 5, 59, 34, 72, Cream, DarkWood, 27);
            LitWindow(b, 10, 38);
            LitWindow(b, 44, 38);
            // The awning: red and cream stripes, scalloped.
            for (int x = 4; x < 60; x++)
            {
                var stripe = (x / 4) % 2 == 0 ? Royal : Cream;
                Rect(b, x, 49, 1, 6, stripe.Main);
                b[x, 49] = stripe.Light;
                if (x % 4 != 3) b[x, 55] = stripe.Shade;
            }
            // The counter with wares: a jar, apples, a bolt of cloth.
            Rect(b, 8, 60, 22, 4, Wood.Main);
            Rect(b, 8, 60, 22, 1, Wood.Light);
            Rect(b, 10, 56, 3, 4, Water.Light);
            Rect(b, 15, 57, 2, 2, Royal.Main);
            Rect(b, 18, 57, 2, 2, Leaf.Main);
            Rect(b, 22, 56, 6, 4, Violet.Main);
            Door(b, 38, 56, 11, 16, Wood);
            Footing(b, 4, 60, 72);
        });

        // Six tiles by four: pale stone, a pediment, four columns, a rose window, steps.
        static PixelImage Temple() => Building(96, 100, b =>
        {
            // The pediment: a triangle with a gold disc.
            for (int y = 8; y < 30; y++)
            {
                int half = (y - 8) * 2 + 2;
                Rect(b, 48 - half, y, half * 2, 1, y < 12 ? Marble.Light : Marble.Main);
            }
            Rect(b, 4, 28, 88, 3, Marble.Shade);
            for (int i = 0; i < 6; i++) Rect(b, 45 + (i % 3), 16 + i / 3, 1, 1, Gold.Main);
            Rect(b, 44, 15, 8, 7, Gold.Main);
            Rect(b, 45, 16, 3, 2, Gold.Light);
            Walls(b, 8, 88, 31, 88, Marble);
            // The rose window and the door between the columns.
            Rect(b, 41, 36, 14, 14, Marble.Dark);
            Rect(b, 43, 38, 10, 10, Violet.Light);
            Rect(b, 47, 38, 2, 10, Gold.Main);
            Rect(b, 43, 42, 10, 2, Gold.Main);
            Door(b, 41, 62, 14, 26, DarkWood);
            foreach (int x in new[] { 12, 26, 64, 78 })
            {
                Column(b, x, 32, 6, 56, Marble);
                Rect(b, x - 1, 31, 8, 2, Marble.Light);
                Rect(b, x - 1, 86, 8, 2, Marble.Shade);
            }
            for (int s = 0; s < 3; s++) Rect(b, 6 + s * 2, 88 + s * 2, 84 - s * 4, 2, s % 2 == 0 ? Stone.Light : Stone.Main);
        });

        // Seven tiles by three: a long stone hall, a slate roof, two banners, a row of narrow windows, a double door.
        static PixelImage Barracks() => Building(112, 82, b =>
        {
            TiledRoof(b, 2, 110, 14, 38, Iron);
            Pennant(b, 10, 2, Royal);
            Pennant(b, 96, 2, Royal);
            Walls(b, 5, 107, 38, 76, Stone);
            for (int y = 42; y < 76; y += 6)
                for (int x = 6 + (y / 6 % 2) * 5; x < 106; x += 10) Rect(b, x, y, 1, 5, Stone.Shade);
            foreach (int x in new[] { 12, 26, 72, 86, 98 }) LitWindow(b, x, 46, 6, 10);
            Door(b, 44, 56, 10, 20, DarkWood);
            Door(b, 55, 56, 10, 20, DarkWood);
            // Shields over the door.
            Rect(b, 49, 44, 12, 8, Royal.Main);
            Rect(b, 54, 44, 2, 8, Gold.Main);
            Footing(b, 4, 108, 76);
        });

        // Six tiles by three: a long low roof over three open stalls, hay in them, a horse's head in one.
        static PixelImage Stable() => Building(96, 76, b =>
        {
            TiledRoof(b, 2, 94, 14, 34, Straw, 3);
            Walls(b, 5, 91, 34, 70, Wood);
            foreach (int x in new[] { 5, 33, 61, 88 }) Rect(b, x, 34, 3, 36, DarkWood.Main);
            foreach (int x in new[] { 9, 37, 65 })
            {
                Rect(b, x, 44, 23, 26, Rgba.Hex("#3A2A1C"));
                Rect(b, x, 62, 23, 8, Straw.Main);
                Rect(b, x, 62, 23, 1, Straw.Light);
                Rect(b, x, 52, 23, 2, Wood.Light);
            }
            // A horse looking out of the middle stall.
            Rect(b, 45, 46, 7, 10, Rgba.Hex("#8A5A34"));
            Rect(b, 46, 54, 5, 4, Rgba.Hex("#6E4426"));
            b[47, 48] = Ink;
            Rect(b, 45, 44, 2, 3, Rgba.Hex("#3A2416"));
            Footing(b, 4, 92, 70);
        });

        // Fourteen tiles by five: white walls and gold trim, a blue roof, a tower with a dome at each end, a higher middle
        // with the great door and a balcony, banners, many lit windows, steps down to the court.
        static PixelImage Palace() => Building(224, 150, b =>
        {
            // The towers either end: round, domed, tall.
            foreach (int tx in new[] { 4, 196 })
            {
                Column(b, tx, 30, 24, 112, Marble);
                for (int y = 8; y < 30; y++)
                {
                    float t = (y - 8) / 22f;
                    int half = (int)(12 * MathF.Sqrt(1 - (1 - t) * (1 - t)));
                    Rect(b, tx + 12 - half, y, half * 2, 1, y < 14 ? RoofBlue.Light : RoofBlue.Main);
                }
                Rect(b, tx + 11, 2, 2, 7, Gold.Main);
                b[tx + 11, 1] = Gold.Light;
                Rect(b, tx - 1, 28, 26, 3, Gold.Main);
                foreach (int y in new[] { 44, 68, 92 }) LitWindow(b, tx + 8, y, 8, 11);
                Rect(b, tx - 1, 140, 26, 2, Marble.Shade);
            }
            // The body: a blue roof over white walls with gold string courses and two rows of windows.
            TiledRoof(b, 26, 198, 34, 64, RoofBlue);
            Walls(b, 28, 196, 64, 142, Marble);
            Rect(b, 28, 64, 168, 2, Gold.Main);
            Rect(b, 28, 100, 168, 2, Gold.Shade);
            foreach (int x in new[] { 36, 54, 72, 140, 158, 176 })
            {
                LitWindow(b, x, 72, 9, 14);
                LitWindow(b, x, 108, 9, 14);
            }
            // The middle: higher, a gabled front with a gold sun, the great door, a balcony with a red cloth.
            for (int y = 16; y < 46; y++)
            {
                int half = Math.Min(40, (y - 16) * 2 + 4);
                Rect(b, 112 - half, y, half * 2, 1, y < 22 ? RoofBlue.Light : RoofBlue.Shade);
            }
            Walls(b, 74, 150, 46, 142, Marble);
            Rect(b, 72, 44, 80, 3, Gold.Main);
            Rect(b, 105, 52, 14, 14, Gold.Main);
            Rect(b, 108, 55, 8, 8, Gold.Light);
            Rect(b, 90, 82, 44, 3, Marble.Dark);
            Rect(b, 92, 85, 40, 8, Royal.Main);
            Rect(b, 92, 85, 40, 1, Royal.Light);
            for (int x = 94; x < 132; x += 6) Rect(b, x, 86, 2, 6, Gold.Main);
            Rect(b, 98, 104, 28, 38, Rgba.Hex("#5A3A22"));
            for (int y = 104; y < 112; y++)
            {
                int half = 14 - Math.Max(0, 112 - y - 1);
                Rect(b, 112 - half, y, half * 2, 1, Gold.Shade);
            }
            Rect(b, 100, 110, 24, 32, DarkWood.Shade);
            Rect(b, 111, 110, 2, 32, Gold.Main);
            foreach (int x in new[] { 80, 140 }) LitWindow(b, x, 60, 9, 14);
            // Banners down the front and pillars by the door.
            foreach (int x in new[] { 30, 64, 156, 186 })
            {
                Rect(b, x, 102, 7, 26, Royal.Main);
                Rect(b, x, 102, 7, 2, Gold.Main);
                Rect(b, x + 2, 110, 3, 4, Gold.Light);
            }
            foreach (int x in new[] { 88, 130 }) Column(b, x, 96, 6, 46, Marble);
            for (int s = 0; s < 3; s++) Rect(b, 80 - s * 4, 142 + s * 2, 64 + s * 8, 2, s % 2 == 0 ? Marble.Light : Stone.Main);
        });

        // Two tiles by one: a small trestle under a striped cloth on poles, wares on it.
        static PixelImage MarketStall() => Building(32, 40, b =>
        {
            foreach (int x in new[] { 3, 27 }) Rect(b, x, 8, 2, 28, DarkWood.Main);
            for (int x = 1; x < 31; x++)
            {
                var stripe = (x / 3) % 2 == 0 ? Royal : Awning;
                Rect(b, x, 6, 1, 7, stripe.Main);
                b[x, 6] = stripe.Light;
            }
            Rect(b, 4, 24, 24, 4, Wood.Main);
            Rect(b, 4, 24, 24, 1, Wood.Light);
            Rect(b, 6, 21, 3, 3, Royal.Main);
            Rect(b, 10, 21, 3, 3, Leaf.Main);
            Rect(b, 15, 20, 4, 4, Gold.Main);
            Rect(b, 21, 21, 4, 3, Water.Light);
            Rect(b, 6, 28, 20, 8, Wood.Shade);
        });

        static PixelImage TownBuilding(TileType type) => type switch
        {
            TileType.Inn => Inn(),
            TileType.Smithy => Smithy(),
            TileType.Shop => Shop(),
            TileType.Temple => Temple(),
            TileType.Barracks => Barracks(),
            TileType.Stable => Stable(),
            TileType.Palace => Palace(),
            TileType.MarketStall => MarketStall(),
            _ => null,
        };

        // How many rows of each's picture are roof (fading while someone is behind).
        public static int TownCanopy(TileType type) => type switch
        {
            TileType.Inn => 40,
            TileType.Smithy => 38,
            TileType.Shop => 34,
            TileType.Temple => 31,
            TileType.Barracks => 38,
            TileType.Stable => 34,
            TileType.Palace => 64,
            TileType.MarketStall => 13,
            _ => 0,
        };
    }
}
