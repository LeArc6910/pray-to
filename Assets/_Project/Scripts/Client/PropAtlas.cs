using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Client
{
    // The pictures of the things that stand on the floor (WorldView's standing layer, Docs/BiomeRework.md 4-3 웹 성능 4,
    // 묶음 B0), packed into shared textures as each is first needed instead of a texture each, so the sprites sharing a
    // page draw in one batch in a browser. A page is 1024 pixels square (4 MB) and a new one is begun when one is full;
    // each picture keeps a one-pixel border copied from its edge so its neighbours never bleed in. A page is sent to the
    // graphics card once a frame, and only when something was added to it (Upload). The floor's atlas (Sprites) is not
    // touched.
    static class PropAtlas
    {
        const int Page = 1024;

        sealed class Sheet
        {
            public Texture2D Texture;
            public int X, Y, Row;
            public bool Dirty;
        }

        static readonly List<Sheet> Sheets = new List<Sheet>();
        static readonly Dictionary<long, Sprite> Made = new Dictionary<long, Sprite>();

        // For the playtests and the dev panel: pages and pictures.
        public static int Pages => Sheets.Count;
        public static int Count => Made.Count;

        // The sprite for a key already made, without making anything (the every-frame path allocates nothing).
        public static bool TryGet(long key, out Sprite sprite) => Made.TryGetValue(key, out sprite) && sprite != null;

        // The sprite for a key, drawn and packed the first time: pivoted pivotX pixels from the picture's left on its
        // bottom edge.
        public static Sprite Get(long key, Func<PixelImage> draw, Func<PixelImage, int> pivotX = null)
        {
            if (Made.TryGetValue(key, out var sprite) && sprite != null) return sprite;
            var image = draw();
            if (image == null || image.Width == 0 || image.Height == 0) return Made[key] = null;
            int w = image.Width + 2, h = image.Height + 2;
            if (w > Page || h > Page) throw new ArgumentException($"A standing picture of {image.Width}x{image.Height} is bigger than an atlas page.");
            var sheet = Sheets.Count > 0 ? Sheets[Sheets.Count - 1] : null;
            if (sheet != null && sheet.X + w > Page)
            {
                sheet.X = 0;
                sheet.Y += sheet.Row;
                sheet.Row = 0;
            }
            if (sheet == null || sheet.Y + h > Page)
            {
                sheet = NewSheet();
                Sheets.Add(sheet);
            }
            // Packed from the top left; Unity's rows run bottom to top.
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                int sy = Mathf.Clamp(y - 1, 0, image.Height - 1);
                int row = (h - 1 - y) * w;
                for (int x = 0; x < w; x++)
                {
                    var p = image[Mathf.Clamp(x - 1, 0, image.Width - 1), sy];
                    pixels[row + x] = new Color32(p.R, p.G, p.B, p.A);
                }
            }
            int left = sheet.X, bottom = Page - sheet.Y - h;
            sheet.Texture.SetPixels32(left, bottom, w, h, pixels);
            sheet.Dirty = true;
            sheet.X += w;
            sheet.Row = Mathf.Max(sheet.Row, h);
            float pivot = (pivotX?.Invoke(image) ?? image.Width / 2) / (float)image.Width;
            sprite = Sprite.Create(sheet.Texture, new Rect(left + 1, bottom + 1, image.Width, image.Height), new Vector2(pivot, 0), Sprites.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = "stand/" + key;
            Made[key] = sprite;
            return sprite;
        }

        static Sheet NewSheet()
        {
            var texture = new Texture2D(Page, Page, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Standing atlas " + Sheets.Count,
            };
            texture.SetPixels32(new Color32[Page * Page]);
            return new Sheet { Texture = texture, Dirty = true };
        }

        // Sends the pages added to since the last frame to the graphics card (kept readable, to add to later).
        public static void Upload()
        {
            foreach (var sheet in Sheets)
            {
                if (!sheet.Dirty) continue;
                sheet.Texture.Apply(false, false);
                sheet.Dirty = false;
            }
        }

        // The rows from..to (from the top) of a picture: a tall thing's canopy or roof apart from what is under it.
        public static PixelImage Rows(PixelImage image, int from, int to)
        {
            if (image == null) return null;
            to = Mathf.Min(to, image.Height);
            var cut = new PixelImage(image.Width, Mathf.Max(0, to - from));
            for (int y = from; y < to; y++)
                for (int x = 0; x < image.Width; x++)
                    cut[x, y - from] = image[x, y];
            return cut;
        }
    }
}
