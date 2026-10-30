using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Client
{
    // Something that lights the dark caves: a player, a torch, a fire, a shot. A light that tints mixes its colour
    // into what it lights; the light players carry does not, so the caves keep their colours where you stand.
    readonly struct LightSource
    {
        public LightSource(Vec2 position, float radius, float intensity, Rgba color, bool tints)
        {
            Position = position;
            Radius = radius;
            Intensity = intensity;
            Color = color;
            Tints = tints;
        }

        public Vec2 Position { get; }
        public float Radius { get; }
        public float Intensity { get; }
        public Rgba Color { get; }
        public bool Tints { get; }
    }

    // How dark each tile of a square window of the map is, one value a tile, for the view to stretch over the world
    // with smoothing. Light spreads from each source through open tiles. It goes on into rock it reaches, because
    // the tops of walls are seen from above, but never out of the rock again, so the far side of a wall stays dark.
    // Plain C#, so the preview tool (DevTools/ArtPreview) draws it the same way.
    sealed class LightMap
    {
        // Brightness where no light reaches, and how much of what is under it a fully dark tile hides.
        public const float Ambient = 0.14f;
        public const float MaxDarkness = 0.9f;
        // How strongly a fire's colour shows in what it lights.
        const float HazeStrength = 0.22f;
        // Light goes round corners only so far: the path it takes may be this much longer than its radius.
        const float Detour = 1.4f;
        // The farthest any light reaches, in tiles (tiles.csv allows up to 12).
        public const float MaxRadius = 12;

        readonly float[] _light;
        readonly float[] _red;
        readonly float[] _green;
        readonly float[] _blue;
        readonly float[] _haze;
        // Breadth-first search around one light, in a square of side Span centred on it.
        static readonly int Reach = (int)Math.Ceiling(MaxRadius * Detour);
        static readonly int Span = Reach * 2 + 1;
        readonly int[] _visited = new int[Span * Span];
        readonly int[] _queue = new int[Span * Span];
        int _visit;

        public LightMap(int size)
        {
            Size = size;
            _light = new float[size * size];
            _red = new float[size * size];
            _green = new float[size * size];
            _blue = new float[size * size];
            _haze = new float[size * size];
        }

        public int Size { get; }
        // The window's bottom left tile.
        public int Left { get; private set; }
        public int Bottom { get; private set; }

        // Rock blocks light. Placed things (workbench, torch, machines) let it through.
        public static bool BlocksLight(TileType type) => TileArt.IsRock(type);

        // Lights the window whose bottom left tile is (left, bottom). The ambient function adds to the base
        // brightness of a tile, as the boss arena's glow does; null for none.
        public void Compute(TileMap map, int left, int bottom, IReadOnlyList<LightSource> lights, Func<int, int, float> ambient)
        {
            Left = left;
            Bottom = bottom;
            for (int j = 0; j < Size; j++)
            {
                for (int i = 0; i < Size; i++)
                {
                    int k = j * Size + i;
                    _light[k] = ambient?.Invoke(left + i, bottom + j) ?? 0;
                    _red[k] = _green[k] = _blue[k] = _haze[k] = 0;
                }
            }
            foreach (var light in lights) Spread(map, light);
        }

        void Spread(TileMap map, LightSource light)
        {
            float radius = Math.Min(light.Radius, MaxRadius);
            if (radius <= 0 || light.Intensity <= 0) return;
            int steps = (int)Math.Ceiling(radius * Detour);
            int sx = TileMap.Cell(light.Position.X);
            int sy = TileMap.Cell(light.Position.Y);
            if (sx + steps < Left || sy + steps < Bottom || sx - steps >= Left + Size || sy - steps >= Bottom + Size) return;

            // Each entry is the tile's place in the search square and its steps from the light, packed.
            _visit++;
            int head = 0;
            int tail = 0;
            _queue[tail++] = Pack(Reach, Reach, 0);
            _visited[Reach * Span + Reach] = _visit;
            while (head < tail)
            {
                int entry = _queue[head++];
                int lx = entry & 0xFF;
                int ly = entry >> 8 & 0xFF;
                int distance = entry >> 16;
                int x = sx + lx - Reach;
                int y = sy + ly - Reach;
                Light(light, radius, x, y);
                if (distance == steps) continue;
                // From rock, light only goes on into rock. The light's own tile counts as open, even a torch's.
                bool inRock = distance > 0 && BlocksLight(map[x, y]);
                Visit(map, sx, sy, lx + 1, ly, distance + 1, inRock, ref tail);
                Visit(map, sx, sy, lx - 1, ly, distance + 1, inRock, ref tail);
                Visit(map, sx, sy, lx, ly + 1, distance + 1, inRock, ref tail);
                Visit(map, sx, sy, lx, ly - 1, distance + 1, inRock, ref tail);
            }
        }

        static int Pack(int lx, int ly, int distance) => lx | ly << 8 | distance << 16;

        void Visit(TileMap map, int sx, int sy, int lx, int ly, int distance, bool inRock, ref int tail)
        {
            int index = ly * Span + lx;
            if (_visited[index] == _visit) return;
            if (inRock && !BlocksLight(map[sx + lx - Reach, sy + ly - Reach])) return;
            _visited[index] = _visit;
            _queue[tail++] = Pack(lx, ly, distance);
        }

        void Light(LightSource light, float radius, int x, int y)
        {
            int i = x - Left;
            int j = y - Bottom;
            if (i < 0 || j < 0 || i >= Size || j >= Size) return;
            float t = Vec2.Distance(light.Position, TileMap.Center(x, y)) / radius;
            if (t >= 1) return;
            // Bright in the middle, falling off smoothly to nothing at the edge.
            float falloff = 1 - t * t;
            float amount = light.Intensity * falloff * falloff;
            int k = j * Size + i;
            _light[k] += amount;
            if (!light.Tints) return;
            _red[k] += light.Color.R * amount;
            _green[k] += light.Color.G * amount;
            _blue[k] += light.Color.B * amount;
            _haze[k] += amount;
        }

        // The colour to lay over one tile of the window, (0, 0) its bottom left: black that hides, mixed with the
        // colour of the fires that light it. Laying a fire's haze and then the dark over the world comes to this one
        // colour over it.
        public Rgba Overlay(int i, int j)
        {
            int k = j * Size + i;
            float bright = Math.Clamp(Ambient + _light[k], 0, 1);
            float dark = (1 - bright) * MaxDarkness;
            float haze = HazeStrength * Math.Clamp(_haze[k], 0, 1);
            float alpha = 1 - (1 - haze) * (1 - dark);
            if (alpha <= 0) return Rgba.Clear;
            if (_haze[k] <= 0) return new Rgba(0, 0, 0, (int)(alpha * 255 + 0.5f));
            float share = haze * (1 - dark) / alpha / _haze[k];
            return new Rgba((int)(_red[k] * share + 0.5f), (int)(_green[k] * share + 0.5f), (int)(_blue[k] * share + 0.5f), (int)(alpha * 255 + 0.5f));
        }
    }
}

namespace Game.Client
{
    // The lights the world itself gives: what players carry, the tiles that burn (tiles.csv light, light_color) and
    // the glow of the boss arena. Shared by the view and the preview tool so both light the caves alike.
    sealed class WorldLights
    {
        // Every player carries a light this far, bright enough to see a screen's height around them.
        public const float CarriedRadius = 8.5f;
        const float CarriedIntensity = 1.25f;
        // The boss arena glows faintly, so a fight there can be read.
        public const float ArenaGlow = 0.45f;
        static readonly Rgba Flame = Rgba.Hex("#FFC27A");

        readonly float[] _radius;
        readonly Rgba[] _color;
        readonly float _reach;

        public WorldLights(TileData tiles)
        {
            int kinds = Enum.GetValues(typeof(TileType)).Length;
            _radius = new float[kinds];
            _color = new Rgba[kinds];
            foreach (var tile in tiles?.Tiles ?? Array.Empty<TileDefinition>())
            {
                int index = (int)tile.Tile;
                if (index >= kinds) continue;
                _radius[index] = Math.Min(tile.Light, LightMap.MaxRadius);
                _color[index] = tile.LightColor.Length > 0 ? Rgba.Hex(tile.LightColor) : Flame;
                _reach = Math.Max(_reach, _radius[index]);
            }
        }

        public static LightSource Carried(Vec2 position) => new LightSource(position, CarriedRadius, CarriedIntensity, Rgba.White, tints: false);

        // Adds the burning tiles near the window, flickering with the time in seconds.
        public void AddTiles(List<LightSource> lights, TileMap map, int left, int bottom, int size, float time)
        {
            int margin = (int)Math.Ceiling(_reach);
            if (margin == 0) return;
            for (int y = Math.Max(0, bottom - margin); y < Math.Min(map.Height, bottom + size + margin); y++)
            {
                for (int x = Math.Max(0, left - margin); x < Math.Min(map.Width, left + size + margin); x++)
                {
                    int index = (int)map[x, y];
                    if (index >= _radius.Length || _radius[index] <= 0) continue;
                    float flicker = 0.92f + 0.05f * (float)Math.Sin(time * 7.3 + x * 1.7) + 0.03f * (float)Math.Sin(time * 13.1 + y * 2.3);
                    lights.Add(new LightSource(TileMap.Center(x, y), _radius[index] * (0.97f + 0.03f * flicker), flicker, _color[index], tints: true));
                }
            }
        }

        public static Func<int, int, float> ArenaAmbient(Vec2 arena, float arenaRadius) =>
            (x, y) => Vec2.Distance(TileMap.Center(x, y), arena) <= arenaRadius + 1 ? ArenaGlow : 0;
    }
}
