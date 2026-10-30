using System;
using System.Collections.Generic;

namespace Game.Client
{
    // A colour in the pixel art, 8 bits a channel.
    readonly struct Rgba
    {
        public Rgba(int r, int g, int b, int a = 255)
        {
            R = (byte)Math.Clamp(r, 0, 255);
            G = (byte)Math.Clamp(g, 0, 255);
            B = (byte)Math.Clamp(b, 0, 255);
            A = (byte)Math.Clamp(a, 0, 255);
        }

        public static readonly Rgba Clear = default;
        public static readonly Rgba White = new Rgba(255, 255, 255);

        public byte R { get; }
        public byte G { get; }
        public byte B { get; }
        public byte A { get; }

        // "#8CD94D" or "8CD94D". Anything else reads as magenta, so a typo shows on screen instead of failing.
        public static Rgba Hex(string hex)
        {
            if (hex != null && hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex == null || hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int value)) return new Rgba(255, 0, 255);
            return new Rgba(value >> 16 & 0xFF, value >> 8 & 0xFF, value & 0xFF);
        }

        public Rgba Scale(float factor) => new Rgba((int)(R * factor + 0.5f), (int)(G * factor + 0.5f), (int)(B * factor + 0.5f), A);

        public Rgba Mix(Rgba other, float t) => new Rgba(
            (int)(R + (other.R - R) * t + 0.5f),
            (int)(G + (other.G - G) * t + 0.5f),
            (int)(B + (other.B - B) * t + 0.5f),
            (int)(A + (other.A - A) * t + 0.5f));

        public Rgba WithAlpha(int alpha) => new Rgba(R, G, B, alpha);
    }

    // Four shades of one colour, light to dark, for shading a sprite drawn in one colour. Shadows lean toward
    // purple and highlights toward yellow, as pixel artists do, so the shading does not look grey.
    readonly struct Ramp
    {
        public Ramp(Rgba light, Rgba main, Rgba shade, Rgba dark)
        {
            Light = light;
            Main = main;
            Shade = shade;
            Dark = dark;
        }

        public Rgba Light { get; }
        public Rgba Main { get; }
        public Rgba Shade { get; }
        public Rgba Dark { get; }
        public Rgba Outline => Dark.Scale(0.45f);

        public static Ramp Of(Rgba color) => new Ramp(
            color.Mix(new Rgba(255, 246, 214), 0.4f),
            color,
            color.Scale(0.72f).Mix(new Rgba(40, 28, 72), 0.12f),
            color.Scale(0.48f).Mix(new Rgba(30, 18, 56), 0.2f));

        public static Ramp Of(string hex) => Of(Rgba.Hex(hex));
    }

    // An image for the pixel art, drawn in plain C# so the same code feeds Unity and the preview tool
    // (DevTools/ArtPreview). Row 0 is the top row, as in the text grids the sprites are drawn in; Unity's textures
    // count from the bottom, and Sprites flips them.
    sealed class PixelImage
    {
        readonly Rgba[] _pixels;

        public PixelImage(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), $"{width}x{height}");
            Width = width;
            Height = height;
            _pixels = new Rgba[width * height];
        }

        public int Width { get; }
        public int Height { get; }

        // Reading outside the image gives a clear pixel and writing there does nothing, so drawing can run off
        // the edges.
        public Rgba this[int x, int y]
        {
            get => Inside(x, y) ? _pixels[y * Width + x] : Rgba.Clear;
            set
            {
                if (Inside(x, y)) _pixels[y * Width + x] = value;
            }
        }

        public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public bool IsSet(int x, int y) => this[x, y].A > 0;

        // A sprite from rows of characters, each a colour from the palette. '.' and ' ' are clear. Every row
        // must be as long as the first, and every character must be in the palette.
        public static PixelImage FromRows(IReadOnlyDictionary<char, Rgba> palette, params string[] rows)
        {
            if (rows.Length == 0) throw new ArgumentException("A sprite needs at least one row.");
            var image = new PixelImage(rows[0].Length, rows.Length);
            for (int y = 0; y < rows.Length; y++)
            {
                if (rows[y].Length != image.Width) throw new ArgumentException($"Row {y} is {rows[y].Length} wide, not {image.Width}: \"{rows[y]}\"");
                for (int x = 0; x < image.Width; x++)
                {
                    char c = rows[y][x];
                    if (c == '.' || c == ' ') continue;
                    if (!palette.TryGetValue(c, out var color)) throw new ArgumentException($"'{c}' in row {y} is not in the palette: \"{rows[y]}\"");
                    image[x, y] = color;
                }
            }
            return image;
        }

        public void Fill(int x, int y, int width, int height, Rgba color)
        {
            for (int j = y; j < y + height; j++)
            {
                for (int i = x; i < x + width; i++) this[i, j] = color;
            }
        }

        // Paints the other image over this one with its top left corner at (x, y), blending by its alpha.
        public void Draw(PixelImage image, int x, int y)
        {
            for (int j = 0; j < image.Height; j++)
            {
                for (int i = 0; i < image.Width; i++) Blend(x + i, y + j, image[i, j]);
            }
        }

        public void Blend(int x, int y, Rgba color)
        {
            if (color.A == 0 || !Inside(x, y)) return;
            var under = this[x, y];
            if (color.A == 255 || under.A == 0)
            {
                this[x, y] = color.A == 255 ? color : new Rgba(color.R, color.G, color.B, Math.Max(color.A, under.A));
                return;
            }
            float t = color.A / 255f;
            this[x, y] = under.Mix(color.WithAlpha(255), t).WithAlpha(Math.Max(under.A, color.A));
        }

        // Darkens (factor below 1) or lightens one pixel, keeping its alpha.
        public void Shade(int x, int y, float factor)
        {
            if (IsSet(x, y)) this[x, y] = this[x, y].Scale(factor);
        }

        // One pixel bigger on every side, with a ring of the colour around everything drawn: the dark outline that
        // keeps a sprite readable on any ground.
        public PixelImage Outlined(Rgba color)
        {
            var result = new PixelImage(Width + 2, Height + 2);
            result.Draw(this, 1, 1);
            for (int y = 0; y < result.Height; y++)
            {
                for (int x = 0; x < result.Width; x++)
                {
                    // Neighbours are read from this image, not the result, so the outline does not spread.
                    if (IsSet(x - 1, y - 1)) continue;
                    if (IsSet(x - 2, y - 1) || IsSet(x, y - 1) || IsSet(x - 1, y - 2) || IsSet(x - 1, y)) result[x, y] = color;
                }
            }
            return result;
        }

        public PixelImage FlippedX()
        {
            var result = new PixelImage(Width, Height);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++) result[Width - 1 - x, y] = this[x, y];
            }
            return result;
        }

        // The same shape in white, for flashing a sprite when it is hit or about to strike.
        public PixelImage Silhouette()
        {
            var result = new PixelImage(Width, Height);
            for (int i = 0; i < _pixels.Length; i++) result._pixels[i] = Rgba.White.WithAlpha(_pixels[i].A);
            return result;
        }

        public PixelImage Copy()
        {
            var result = new PixelImage(Width, Height);
            Array.Copy(_pixels, result._pixels, _pixels.Length);
            return result;
        }

        // Moves every row down by the given number of pixels (up when negative); rows that come in are clear.
        public PixelImage Shifted(int down)
        {
            var result = new PixelImage(Width, Height);
            result.Draw(this, 0, down);
            return result;
        }
    }

    // A small hash for picking tile variants and scattering details. Only looks: it is not GameRandom and nothing
    // in the game depends on it.
    static class PixelNoise
    {
        public static uint Hash(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)salt * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        // 0 to 1.
        public static float Value(int x, int y, int salt) => (Hash(x, y, salt) & 0xFFFF) / 65535f;
    }
}
