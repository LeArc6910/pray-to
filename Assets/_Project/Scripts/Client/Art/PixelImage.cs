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

        // The body's own pixel for a spot just off its edge (2026-11-27, the person: a flat shade looked wrong, fill it
        // like the real part): mirrored across the edge into the body along the row (or column), so the gap carries
        // on the body's texture. The dark outline is skipped: further into the body until a pixel that is not; so is
        // anything avoid says (a glowing core that must not be doubled), giving instead if nothing else is found.
        public Rgba MirrorInto(int x, int y, int edge, System.Func<int, bool> body, bool down = false, System.Func<Rgba, bool> avoid = null, Rgba instead = default)
        {
            int dir = edge > (down ? y : x) ? 1 : -1;
            int from = edge + (edge - (down ? y : x));
            for (int k = 0; k < 10; k++)
            {
                int s = from + dir * k;
                if (!body(s)) break;
                var c = down ? this[x, s] : this[s, y];
                if (0.3f * c.R + 0.59f * c.G + 0.11f * c.B >= 22 && (avoid == null || !avoid(c))) return c;
            }
            var last = down ? this[x, edge] : this[edge, y];
            return avoid != null && avoid(last) ? instead : last;
        }

        // Where parts of a body posed in code have moved away (left), the floor must not show through a narrow gap
        // between them and the rest (2026-11-27, Docs/Bosses.md 몸의 틈): a see-through pixel there with the body within
        // reach on both sides (left and right, or above and below) is filled with the body's own pixels mirrored in
        // from the nearer side (MirrorInto); plain if nothing to mirror. Three passes, so a gap a little wider than the
        // reach closes from its edges.
        public void CloseGaps(bool[,] left, Rgba plain, int reach = 3)
        {
            bool Solid(int x, int y) => Inside(x, y) && this[x, y].A == 255;
            for (int pass = 0; pass < 3; pass++)
            {
                var fill = new List<(int X, int Y, Rgba C)>();
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        if (!left[x, y] || this[x, y].A > 0) continue;
                        int Find(int dx, int dy)
                        {
                            for (int k = 1; k <= reach; k++)
                                if (Solid(x + dx * k, y + dy * k)) return k;
                            return 0;
                        }
                        int l = Find(-1, 0), r = Find(1, 0), u = Find(0, -1), d = Find(0, 1);
                        int row = y, column = x;
                        if (l > 0 && r > 0)
                            fill.Add((x, y, l <= r ? MirrorInto(x, y, x - l, s => Solid(s, row)) : MirrorInto(x, y, x + r, s => Solid(s, row))));
                        else if (u > 0 && d > 0)
                            fill.Add((x, y, u <= d ? MirrorInto(x, y, y - u, s => Solid(column, s), true) : MirrorInto(x, y, y + d, s => Solid(column, s), true)));
                    }
                }
                foreach (var (x, y, c) in fill) this[x, y] = c.A == 255 ? c : plain;
                if (fill.Count == 0) break;
            }
        }

        // A small hole the body closes all round (the outside cannot reach it, at most largest pixels) filled with the
        // body's own pixels mirrored in from the nearer side, as CloseGaps does: a fist pressed against the trunk
        // leaves one between them.
        public void FillHoles(int largest)
        {
            bool Solid(int x, int y) => Inside(x, y) && this[x, y].A == 255;
            var seen = new bool[Width, Height];
            var queue = new Queue<(int, int)>();
            for (int y0 = 0; y0 < Height; y0++)
            {
                for (int x0 = 0; x0 < Width; x0++)
                {
                    if (seen[x0, y0] || Solid(x0, y0)) continue;
                    var group = new List<(int X, int Y)>();
                    bool open = false;
                    seen[x0, y0] = true;
                    queue.Enqueue((x0, y0));
                    while (queue.Count > 0)
                    {
                        var (x, y) = queue.Dequeue();
                        group.Add((x, y));
                        if (x == 0 || y == 0 || x == Width - 1 || y == Height - 1) open = true;
                        foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                        {
                            if (!Inside(nx, ny) || seen[nx, ny] || Solid(nx, ny)) continue;
                            seen[nx, ny] = true;
                            queue.Enqueue((nx, ny));
                        }
                    }
                    if (open || group.Count > largest) continue;
                    var fill = new List<(int X, int Y, Rgba C)>();
                    foreach (var (x, y) in group)
                    {
                        int l = 1, r = 1;
                        while (x - l >= 0 && !Solid(x - l, y)) l++;
                        while (x + r < Width && !Solid(x + r, y)) r++;
                        int row = y;
                        fill.Add((x, y, MirrorInto(x, y, l <= r ? x - l : x + r, s => Solid(s, row))));
                    }
                    foreach (var (x, y, c) in fill) this[x, y] = c.WithAlpha(255);
                }
            }
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
