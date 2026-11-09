using System;
using UnityEngine;

namespace Game.Client
{
    // Smooth white shapes, tinted by their SpriteRenderer: a boss's telegraphs, health bars, the glow under loot and
    // the touch sticks, where a clean edge reads better. Everything that stands in the world is pixel art (Sprites,
    // Art/), and so are the effects of swings and skills (EffectArt). Every shape is one world unit (one tile) across.
    static class Shapes
    {
        const int Size = 64;

        static Sprite _circle;
        static Sprite _ring;
        static Sprite _square;
        static readonly System.Collections.Generic.Dictionary<int, Sprite> Wedges = new System.Collections.Generic.Dictionary<int, Sprite>();

        public static Sprite Circle => _circle ??= Make((u, v) => Edge(1 - Radius(u, v)));

        public static Sprite Ring => _ring ??= Make((u, v) => Mathf.Min(Edge(1 - Radius(u, v)), Edge(Radius(u, v) - 0.75f)));

        public static Sprite Square => _square ??= Make((u, v) => 1);

        static readonly System.Collections.Generic.Dictionary<int, Sprite> Donuts = new System.Collections.Generic.Dictionary<int, Sprite>();

        // A ring whose hole is inner (0 to 1) of its radius across, for a boss's ring telegraphs (safe in the hole).
        public static Sprite Donut(float inner)
        {
            int key = Mathf.Clamp(Mathf.RoundToInt(inner * 100), 0, 99);
            if (Donuts.TryGetValue(key, out var sprite)) return sprite;
            float hole = key / 100f;
            sprite = Make((u, v) => Mathf.Min(Edge(1 - Radius(u, v)), Edge(Radius(u, v) - hole)));
            Donuts[key] = sprite;
            return sprite;
        }

        // A slice of a disc pointing along +x with its tip at the centre, for cone telegraphs.
        public static Sprite Wedge(float degrees)
        {
            int key = Mathf.RoundToInt(degrees);
            if (Wedges.TryGetValue(key, out var sprite)) return sprite;
            float half = key * 0.5f * Mathf.Deg2Rad;
            sprite = Make((u, v) => Mathf.Min(Edge(1 - Radius(u, v)), key >= 360 ? 1 : Edge((half - Mathf.Abs(Mathf.Atan2(v, u))) * 4)));
            Wedges[key] = sprite;
            return sprite;
        }

        static float Radius(float u, float v) => Mathf.Sqrt(u * u + v * v);

        // Soft one-pixel edge: 1 inside, 0 outside.
        static float Edge(float inside) => Mathf.Clamp01(inside * Size / 2f);

        static Sprite Make(Func<float, float, float> alpha)
        {
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size * 2 - 1;
                    float v = (y + 0.5f) / Size * 2 - 1;
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(u, v)) * 255));
                }
            }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }
    }
}
