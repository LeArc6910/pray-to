using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // Behind the title and its pages (Docs/TitleRework.md 4-1, the owner's call of 2026-11-25: a slow drift over a cave
    // drawn with the game's own art). A strip of small caves, one for each biome a character on this device has been to
    // (only the dirt cave at first: nothing not yet seen shows), each laid and lit as the loading screen's picture is
    // (LoadingArt), side by side. It is drawn once into a texture and only its window moves: the camera drifts along it a
    // few pixels a second and round again, the lamps breathe a little. With "움직임: 줄이기" it stands still. The top and
    // bottom are darkened so the words over it read.
    static class TitleBackdrop
    {
        // One cave of the strip, in world pixels (sixteen to nine), and how fast the window drifts (pixels a second).
        public const int CaveWidth = 256, Height = 144;
        const float Speed = 5;

        static Texture2D _strip;
        static int _width;
        static int _builtFor = -1;
        static Texture2D _shade;

        // The biomes the strip shows (BiomeVisits bits) and where its window is now (pixels from the strip's left), for the
        // playtests.
        public static int Biomes => _builtFor;
        public static float Offset { get; private set; }

        // The biomes in the strip for these visits: those been to, in the order they are met, the dirt cave when none.
        public static List<Biome> BiomesFor(int visited)
        {
            var biomes = BiomeVisits.In(visited);
            if (biomes.Count == 0) biomes.Add(Biome.Cave);
            return biomes;
        }

        static void Build(int visited)
        {
            if (_strip != null) Object.Destroy(_strip);
            var biomes = BiomesFor(visited);
            // At least two caves, so the drift passes from one to the next and back round.
            int count = Mathf.Max(2, biomes.Count);
            var image = new PixelImage(CaveWidth * count, Height);
            for (int i = 0; i < count; i++)
            {
                var cave = LoadingArt.Scene(biomes[i % biomes.Count], 11 + i * 7, System.Array.Empty<(string, Rgba)>(), null, CaveWidth, Height);
                image.Draw(cave, i * CaveWidth, 0);
            }
            _strip = Sprites.TextureOf(image);
            _strip.wrapMode = TextureWrapMode.Repeat;
            _width = image.Width;
            _builtFor = visited;
        }

        // The strip over the area, its window drifting (still with less motion), darkened at the top and bottom, and the
        // whole a little darker again by `dim` (0 to 1) behind a page.
        public static void Draw(Rect area, int visited, float dim = 0)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (_strip == null || visited != _builtFor) Build(visited);
            // Whole device pixels to a pixel of the strip, enough to fill the height.
            float devicePixels = area.height * Ui.Scale;
            int dots = Mathf.Max(1, Mathf.CeilToInt(devicePixels / Height));
            float pixel = dots / Ui.Scale;
            float viewWidth = area.width / pixel, viewHeight = area.height / pixel;
            bool still = Settings.ReduceMotion;
            float x = still ? CaveWidth * 0.25f : Time.unscaledTime * Speed % _width;
            // On whole device pixels, so the pixels do not shimmer as they pass.
            x = Mathf.Round(x * dots) / dots;
            Offset = x;
            float v = (Height - viewHeight) / 2 / Height;
            GUI.DrawTextureWithTexCoords(area, _strip, new Rect(x / _width, v, viewWidth / _width, viewHeight / Height));
            // The lamps breathe: a warm wash coming and going slowly.
            var previous = GUI.color;
            if (!still)
            {
                float breath = 0.035f + 0.025f * Mathf.Sin(Time.unscaledTime * 1.7f) * Mathf.Sin(Time.unscaledTime * 0.63f + 1);
                GUI.color = new Color(1f, 0.62f, 0.28f, breath);
                GUI.DrawTexture(area, Texture2D.whiteTexture);
            }
            GUI.color = previous;
            GUI.DrawTexture(area, Shade(), ScaleMode.StretchToFill);
            if (dim > 0)
            {
                GUI.color = new Color(0, 0, 0, dim);
                GUI.DrawTexture(area, Texture2D.whiteTexture);
                GUI.color = previous;
            }
        }

        // Dark at the top and the bottom, clear through the middle.
        static Texture2D Shade()
        {
            if (_shade != null) return _shade;
            const int h = 64;
            _shade = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++)
            {
                float t = (y + 0.5f) / h;
                float edge = Mathf.Max(Mathf.Clamp01(1 - t / 0.32f), Mathf.Clamp01((t - 0.7f) / 0.3f));
                _shade.SetPixel(0, y, new Color(0.02f, 0.015f, 0.025f, 0.12f + 0.78f * edge * edge));
            }
            _shade.Apply();
            return _shade;
        }
    }
}
