using Game.Core;

namespace Game.Client
{
    // The chests of the rooms (2026-11-22, Docs/WorldRework.md 3단계 볼거리, 묶음 3a): wood, iron and gold, shut with a
    // round lid and a lock, and one thrown open. A player's own storage chest (Chest) is a flat-lidded box; these have
    // a domed lid so they read as found, not built.
    static partial class PropArt
    {
        static readonly Ramp Brass = new Ramp(Rgba.Hex("#FFF0A8"), Rgba.Hex("#E8B84A"), Rgba.Hex("#A87A2A"), Rgba.Hex("#6E4A18"));
        static readonly Ramp Steel = new Ramp(Rgba.Hex("#C8D0DC"), Rgba.Hex("#8A94A4"), Rgba.Hex("#5C6474"), Rgba.Hex("#3A404C"));
        static readonly Ramp Gilt = new Ramp(Rgba.Hex("#FFF4B0"), Rgba.Hex("#F6CC52"), Rgba.Hex("#C08A26"), Rgba.Hex("#7E5414"));
        static readonly Ramp Crimson = new Ramp(Rgba.Hex("#E46A62"), Rgba.Hex("#A83A3A"), Rgba.Hex("#782428"), Rgba.Hex("#4C161A"));

        static PixelImage PoiChest(TileType type) => type switch
        {
            TileType.IronChest => TreasureChest(DarkWood, Steel, open: false),
            TileType.GoldChest => TreasureChest(Crimson, Gilt, open: false),
            TileType.OpenChest => TreasureChest(Wood, Iron, open: true),
            _ => TreasureChest(Wood, Iron, open: false),
        };

        // A chest seen a little from above: a domed lid (its curve lit from the left), the body below it, two bands
        // of the trim down the front and a lock plate in the middle. Open, the lid stands back behind it and the inside
        // shows gold.
        static PixelImage TreasureChest(Ramp body, Ramp trim, bool open)
        {
            const int h = 20;
            var image = new PixelImage(T, h);
            if (!open)
            {
                // The lid's dome: rows 4 to 9, wider as it comes down.
                for (int y = 4; y <= 9; y++)
                {
                    int inset = y == 4 ? 3 : y == 5 ? 2 : 1;
                    for (int x = 1 + inset; x <= 14 - inset; x++)
                    {
                        float across = (x - 1) / 13f;
                        image[x, y] = y <= 5 ? (across < 0.6f ? body.Light : body.Main) : across < 0.25f ? body.Light : across < 0.8f ? body.Main : body.Shade;
                    }
                }
                Rect(image, 2, 9, 12, 1, body.Dark);
                // The body.
                Rect(image, 2, 10, 12, 7, body.Main);
                Rect(image, 12, 10, 2, 7, body.Shade);
                Rect(image, 2, 16, 12, 1, body.Shade);
                // Bands over the lid and down the front, and the lock.
                foreach (int x in new[] { 4, 11 })
                {
                    Rect(image, x, 4, 1, 13, trim.Main);
                    image[x, 5] = trim.Light;
                }
                Rect(image, 2, 10, 12, 1, trim.Shade);
                Rect(image, 7, 9, 2, 4, Brass.Main);
                image[7, 9] = Brass.Light;
                image[8, 11] = Brass.Dark;
            }
            else
            {
                // The lid thrown back: its inside face standing behind the body.
                Rect(image, 2, 3, 12, 5, body.Shade);
                Rect(image, 3, 4, 10, 3, body.Dark);
                foreach (int x in new[] { 4, 11 }) Rect(image, x, 3, 1, 5, trim.Shade);
                // The open top: the rim and the gold inside.
                Rect(image, 2, 8, 12, 3, body.Light);
                Rect(image, 3, 9, 10, 2, body.Dark);
                Rect(image, 4, 9, 8, 1, Gilt.Shade);
                image[5, 9] = Gilt.Light;
                image[9, 9] = Gilt.Main;
                // The body.
                Rect(image, 2, 11, 12, 6, body.Main);
                Rect(image, 12, 11, 2, 6, body.Shade);
                Rect(image, 2, 16, 12, 1, body.Shade);
                foreach (int x in new[] { 4, 11 }) Rect(image, x, 11, 1, 6, trim.Main);
                Rect(image, 7, 11, 2, 2, Brass.Shade);
            }
            return Finish(image, ShadowLayer(T, h, 8, 17.5f, 7, 2));
        }
    }
}
