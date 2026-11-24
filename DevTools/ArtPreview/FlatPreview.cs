using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // --only flat (2026-11-25, "입체감 없이 타일 형태로 칸 꽉 채우는거 다 찾아서 점검해"): every tile the game can draw as a
    // flat square on the object layer, each on the dirt cave's floor at game scale, marked whether it stands (the standing
    // layer, with a foot and a shadow) or lies flat, so the ones that fill their cell like a stamp can be found by eye.
    // And the mines' rails as they join (RailArt): straight both ways, the four turns, branches, a crossing and the stops.
    static class FlatPreview
    {
        const int Tile = 16;
        static readonly Rgba Back = Rgba.Hex("#14110E");
        static readonly Rgba Ink = Rgba.Hex("#E8DCC8");
        static readonly Rgba Stand = Rgba.Hex("#7CC47C");
        static readonly Rgba Flat = Rgba.Hex("#E8946A");

        public static void Write(string output)
        {
            string folder = Path.Combine(output, "flat");
            Directory.CreateDirectory(folder);
            var floor = TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, 0));

            // Every tile type with a drawing on the object layer, as the game now draws it alone on a floor: standing
            // (the standing layer's picture), joined (matched to its neighbours: here with none), a belt, or still a flat
            // tile.
            var found = new List<(TileType Type, PixelImage Image, string How)>();
            foreach (TileType type in Enum.GetValues(typeof(TileType)))
            {
                PixelImage image = null;
                try { image = TileArt.Object(type); } catch (Exception) { }
                if (image == null) continue;
                string how = "FLAT";
                if (TileMap.IsDoor(type))
                {
                    image = PropArt.Door(PropArt.DoorShape(true, false, type == TileType.LockedDoor));
                    how = "STANDS";
                }
                else if (PropArt.Stands(type))
                {
                    image = PropArt.Prop(type) ?? image;
                    how = "STANDS";
                }
                else if (World.SizeOf(type) > 1)
                {
                    image = MachineArt.Part(type, 0, null, 0, 0, 0).Image;
                    how = "STANDS";
                }
                else if (World.IsBelt(type))
                {
                    image = MachineArt.Part(type, 0, null, 0, 0, 0).Image;
                    how = "BELT";
                }
                else if (FlatArt.LaidIndex(type) >= 0)
                {
                    image = FlatArt.Tile(type, 0);
                    how = "JOINED";
                }
                else if (FlatArt.BlockIndex(type) >= 0)
                {
                    image = FlatArt.Block(type, 0);
                    how = "JOINED";
                }
                else if (type == TileType.Road || type == TileType.Rail || RoomTileArt.IsPatch(type))
                {
                    image = type == TileType.Road ? TileArt.Road(0) : type == TileType.Rail ? RailArt.Rail(0) : RoomTileArt.Patch(type, 0);
                    how = "JOINED";
                }
                found.Add((type, image, how));
            }
            const int Columns = 6, CellW = 3 * Tile + 70, CellH = 4 * Tile + 14;
            int rows = (found.Count + Columns - 1) / Columns;
            var sheet = new PixelImage(Columns * CellW + 8, rows * CellH + 8);
            sheet.Fill(0, 0, sheet.Width, sheet.Height, Back);
            for (int i = 0; i < found.Count; i++)
            {
                var (type, image, how) = found[i];
                int left = 4 + i % Columns * CellW, top = 4 + i / Columns * CellH;
                for (int ty = 0; ty < 4; ty++)
                    for (int tx = 0; tx < 3; tx++)
                        sheet.Draw(floor, left + tx * Tile, top + ty * Tile);
                // On the middle tile of the third row, standing from its bottom edge.
                sheet.Draw(image, left + Tile + Tile / 2 - Math.Min(image.Width, Tile) / 2, top + 3 * Tile - image.Height);
                sheet.Small(left + 3 * Tile + 4, top + 4, type.ToString().ToUpperInvariant(), Ink);
                sheet.Small(left + 3 * Tile + 4, top + 14, how, how == "FLAT" ? Flat : Stand);
            }
            Png.Save(Scaled(sheet, 3), Path.Combine(folder, "objects.png"));
            File.WriteAllLines(Path.Combine(folder, "objects.txt"),
                found.Select(f => $"{f.How.ToLowerInvariant(),-6} {f.Type}"));

            // The joined sets: every laid kind and the rooms' blocks in their sixteen shapes, the belts each way and
            // fed from each side, the doors' eight shapes and a grate's sixteen.
            var laid = new PixelImage(16 * (Tile + 2) + 4, (FlatArt.Laid.Length + FlatArt.Blocks.Length) * (Tile + 2) + 4);
            laid.Fill(0, 0, laid.Width, laid.Height, Back);
            for (int k = 0; k < FlatArt.Laid.Length + FlatArt.Blocks.Length; k++)
            {
                for (int m = 0; m < 16; m++)
                {
                    int x = 2 + m * (Tile + 2), y = 2 + k * (Tile + 2);
                    laid.Draw(k == FlatArt.Laid.Length - 1 ? TileArt.Object(TileType.Water) : floor, x, y);
                    laid.Draw(k < FlatArt.Laid.Length ? FlatArt.Tile(FlatArt.Laid[k], m) : FlatArt.Block(FlatArt.Blocks[k - FlatArt.Laid.Length], m), x, y);
                }
            }
            Png.Save(Scaled(laid, 4), Path.Combine(folder, "laid_variants.png"));
            var stood = new PixelImage(16 * (Tile + 4) + 4, 3 * 34 + 4);
            stood.Fill(0, 0, stood.Width, stood.Height, Back);
            for (int m = 0; m < 16; m++)
            {
                int x = 2 + m * (Tile + 4);
                for (int r = 0; r < 3; r++) stood.Draw(floor, x, 2 + r * 34 + 16);
                // Belts: plain and fast, the four ways, straight and fed from each side (twelve of the sixteen columns).
                if (m < 12) stood.Draw(BeltArt.Belt(false, m / 3, m % 3, 0), x, 2 + 16);
                else stood.Draw(BeltArt.Underground(m - 12, 0), x, 2 + 16);
                var railing = PropArt.Prop(TileType.Grate, m);
                stood.Draw(railing, x, 2 + 34 + 32 - railing.Height);
                if (m < 8)
                {
                    var door = PropArt.Door(m);
                    stood.Draw(door, x, 2 + 68 + 32 - door.Height);
                }
                else
                {
                    var hung = PropArt.Prop(m < 12 ? TileType.TapestryWall : TileType.ShieldWall, new[] { 0, 2, 10, 8 }[m % 4]);
                    stood.Draw(hung, x, 2 + 68 + 32 - hung.Height);
                }
            }
            Png.Save(Scaled(stood, 4), Path.Combine(folder, "stood_variants.png"));

            // The rails as they join.
            string[] layout =
            {
                "..............",
                ".RRRRRR...R...",
                ".R....R...R...",
                ".R..RRRRR.RRR.",
                ".R..R.R...R...",
                ".RRRR.R...R...",
                "......R.......",
                "..RRRRRRRR....",
                "......R.......",
                "..............",
            };
            int w = layout[0].Length, h = layout.Length;
            var rails = new PixelImage(w * Tile, h * Tile);
            bool Rail(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && layout[y][x] == 'R';
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    rails.Draw(floor, x * Tile, y * Tile);
                    if (!Rail(x, y)) continue;
                    // The layout's rows run down the image; north is up.
                    int mask = (Rail(x, y - 1) ? 1 : 0) | (Rail(x + 1, y) ? 2 : 0) | (Rail(x, y + 1) ? 4 : 0) | (Rail(x - 1, y) ? 8 : 0);
                    rails.Draw(RailArt.Rail(mask), x * Tile, y * Tile);
                }
            Png.Save(Scaled(rails, 4), Path.Combine(folder, "rails.png"));
            // And the old way beside it is gone: every variant alone, for a close look.
            var variants = new PixelImage(RailArt.Variants * (Tile + 4) + 4, Tile + 8);
            variants.Fill(0, 0, variants.Width, variants.Height, Back);
            for (int m = 0; m < RailArt.Variants; m++)
            {
                variants.Draw(floor, 4 + m * (Tile + 4), 4);
                variants.Draw(RailArt.Rail(m), 4 + m * (Tile + 4), 4);
            }
            Png.Save(Scaled(variants, 6), Path.Combine(folder, "rail_variants.png"));
        }

        static PixelImage Scaled(PixelImage image, int scale)
        {
            var big = new PixelImage(image.Width * scale, image.Height * scale);
            for (int y = 0; y < big.Height; y++)
                for (int x = 0; x < big.Width; x++)
                    big[x, y] = image[x / scale, y / scale];
            return big;
        }
    }
}
