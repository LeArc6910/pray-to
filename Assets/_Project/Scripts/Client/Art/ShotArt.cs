using System;

namespace Game.Client
{
    // What an enemy's shot is made of (E2 of Docs/EffectsRework.md): each has its own shape and colours, and all of
    // them the same red rim, so that an enemy's shot is told from a player's whatever it is made of.
    enum ShotElement
    {
        Ember,
        Stone,
        Crystal,
        Sand,
        Fire,
        Lava,
        Void,
        Water,
        Wraith,
        // Ordinary monsters' (E5, MonsterFx.ShotOf): a violet lump of spores and a teal one of the glowing wood, a
        // splinter of ice, violet witch-fire, a ball of iron.
        Spore,
        Mote,
        Frost,
        Hex,
        Iron,
    }

    // An enemy's shot (E2): a dark edge, a red rim, the element's shell and a bright core that beats, with a tail of
    // shrinking pieces behind it so the way it flies reads. Drawn at the angle it flies (not turned afterwards), the
    // tail in the same picture: one piece a shot, however many fly. And the ring and bits it leaves where it ends.
    static class ShotArt
    {
        public const int Elements = (int)ShotElement.Iron + 1;
        // The angles a shot is drawn at: a shot flies straight, so the nearest of these is its own for good.
        public const int Angles = 32;
        // The core's beat.
        public const int Frames = 2;
        public const float FramesPerSecond = 12.5f;
        // The ring and bits where one ends.
        public const int PopFrames = 5;
        public const float PopSeconds = 0.24f;
        public const int PopSize = 31;
        // The dark edge and the red rim lie outside the size it is judged at: never drawn smaller than it hits.
        const float Rim = 2;
        const int TailPieces = 4;

        static readonly Rgba EdgeDark = Rgba.Hex("#5A1014");
        static readonly Rgba RimRed = Rgba.Hex("#FF4033");
        static readonly Rgba TailRed = Rgba.Hex("#B8201E");

        // Hot centre, core, shell, shade: light to dark, the dark turning in hue.
        static readonly Rgba[][] Ramps =
        {
            Ramp("#FFFFFF", "#FFD060", "#FF8A2A", "#C2501E"),
            Ramp("#F2EADC", "#C9B79C", "#8E7B66", "#54463E"),
            Ramp("#FFFFFF", "#FFC8F0", "#E070D0", "#8A3A9C"),
            Ramp("#FFF6D0", "#F0D890", "#C8A050", "#8A6430"),
            Ramp("#FFFFFF", "#FFE070", "#FF9A20", "#D8481A"),
            Ramp("#FFF8C0", "#FFB020", "#E8501A", "#4A2420"),
            Ramp("#F0D8FF", "#B060FF", "#5A2A9C", "#180A30"),
            Ramp("#FFFFFF", "#C8F4FF", "#58B8F0", "#2A6AB8"),
            Ramp("#FFFFFF", "#D0FFE8", "#70D8C0", "#2A7A80"),
            Ramp("#F6E0FF", "#D9A8FF", "#A86CE0", "#6A3FA8"),
            Ramp("#F0FFF8", "#A8F0D8", "#4CC8B0", "#1F7A78"),
            Ramp("#FFFFFF", "#C8F4FF", "#7AD0F5", "#3F86D6"),
            Ramp("#FFE8FF", "#E090FF", "#9A48E0", "#4A1C8C"),
            Ramp("#FFFFFF", "#DCE6F2", "#9FB2C8", "#5E6F8A"),
        };

        static Rgba[] Ramp(params string[] hex)
        {
            var ramp = new Rgba[hex.Length];
            for (int i = 0; i < hex.Length; i++) ramp[i] = Rgba.Hex(hex[i]);
            return ramp;
        }

        public static Rgba[] Colors(ShotElement element) => Ramps[(int)element];

        // The light it throws on the floor as it flies (the light map's).
        public static Rgba Glow(ShotElement element) => element == ShotElement.Void ? Ramps[(int)element][1] : Ramps[(int)element][2];

        // Whose shots are made of what (Docs/Bosses.md): the bosses that shot the one orange ball. One with a shot of
        // its own (the vein eater's rocks, the samurai's crescents, the silk mother's venom) keeps it; this is then
        // only what its shot breaks into.
        public static ShotElement ElementOf(string boss) => boss switch
        {
            "cave_warden" => ShotElement.Stone,
            "crystal_queen" => ShotElement.Crystal,
            "lava_titan" => ShotElement.Lava,
            "void_lord" => ShotElement.Void,
            "tide_serpent" => ShotElement.Water,
            "antlion" => ShotElement.Sand,
            "mad_general" => ShotElement.Fire,
            "crown_wraith" => ShotElement.Wraith,
            "vein_eater" => ShotElement.Stone,
            "black_knight" => ShotElement.Stone,
            _ => ShotElement.Ember,
        };

        // The nearest drawn angle to the way it flies.
        public static int Step(float angle)
        {
            int step = (int)Math.Round(angle / (2 * Math.PI) * Angles) % Angles;
            return step < 0 ? step + Angles : step;
        }

        // The radius it is drawn at, in pixels, for the radius it hits at, in tiles: the rim outside it.
        public static int Reach(float radius) => Math.Max(6, (int)Math.Ceiling(radius * TileArt.Size + Rim));

        // How deep a point lies inside the body (pixels; under 0 is outside), u along the flight and v across it.
        static float Depth(ShotElement element, float u, float v, float r, int seed)
        {
            switch (element)
            {
                case ShotElement.Stone:
                case ShotElement.Iron:
                {
                    // A chipped rock: a circle with five flat faces cut off it.
                    float depth = r - (float)Math.Sqrt(u * u + v * v);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 1.2566f + 0.4f;
                        depth = Math.Min(depth, r * (0.78f + 0.14f * ((i * 7 + 3) % 5) / 4f) - (u * (float)Math.Cos(a) + v * (float)Math.Sin(a)));
                    }
                    return depth;
                }
                case ShotElement.Crystal:
                case ShotElement.Frost:
                    // A shard, point first: as wide as it hits, longer than wide.
                    return (1 - Math.Abs(u - r * 0.2f) / (r * 1.7f) - Math.Abs(v) / (r * 1.02f)) * r * 0.9f;
                case ShotElement.Sand:
                case ShotElement.Spore:
                case ShotElement.Mote:
                {
                    // A clod, its edge crumbling.
                    float bump = PixelNoise.Value((int)Math.Floor(u + 64), (int)Math.Floor(v + 64), seed) - 0.5f;
                    return r - (float)Math.Sqrt(u * u + v * v) + bump * 1.6f;
                }
                case ShotElement.Fire:
                case ShotElement.Wraith:
                case ShotElement.Hex:
                {
                    // Round in front, drawn out to a point behind.
                    if (u >= 0) return r - (float)Math.Sqrt(u * u + v * v);
                    float back = -u / (r * 1.6f);
                    return back >= 1 ? -1 : r * (1 - back) * (1 - back * 0.25f) - Math.Abs(v);
                }
                default:
                    return r - (float)Math.Sqrt(u * u + v * v);
            }
        }

        // The body's colour at a depth: edge, rim, shell, core, hot centre.
        static Rgba Shade(ShotElement element, float depth, float u, float v, float wx, float wy, float r, int frame)
        {
            var c = Ramps[(int)element];
            if (depth < 1) return EdgeDark;
            if (depth < 2) return RimRed;
            float inner = r - Rim;
            float d = (float)Math.Sqrt((u - 0.6f) * (u - 0.6f) + v * v);
            float hot = inner * (frame == 0 ? 0.34f : 0.24f), core = inner * (frame == 0 ? 0.68f : 0.56f);
            switch (element)
            {
                case ShotElement.Stone:
                case ShotElement.Iron:
                    // Lit from above, a crack of the rim's red showing through.
                    if (Math.Abs(u * 0.5f + v) < 0.7f && frame == 0 && d < core) return c[0];
                    return wy > inner * 0.15f ? c[1] : wy > -inner * 0.35f ? c[2] : c[3];
                case ShotElement.Crystal:
                case ShotElement.Frost:
                    // A bright facet down its length, pale on the upper side.
                    if (Math.Abs(v) < (frame == 0 ? 1.1f : 0.6f)) return c[0];
                    return wy > 0 ? c[1] : c[2];
                case ShotElement.Sand:
                    if (d < hot) return c[0];
                    if (PixelNoise.Value((int)Math.Floor(u + 64), (int)Math.Floor(v + 64), 7 + frame) > 0.72f) return c[3];
                    return d < core ? c[1] : c[2];
                case ShotElement.Lava:
                    // A dark crust, cracked over the glow inside.
                    if (d < hot) return c[0];
                    if (d < core) return c[1];
                    return PixelNoise.Value((int)Math.Floor(u * 0.7f + 64), (int)Math.Floor(v * 0.7f + 64), 11) > 0.45f ? c[3] : c[2];
                case ShotElement.Void:
                    // Dark at heart, a bright ring round it.
                    if (d < hot) return c[0];
                    if (d < core) return c[3];
                    return d < core + 1.2f ? c[1] : c[2];
                case ShotElement.Spore:
                case ShotElement.Mote:
                    // A soft lump: pale spots on it, a bright one near the front.
                    if (d < hot) return c[0];
                    if (PixelNoise.Value((int)Math.Floor(u * 0.8f + 64), (int)Math.Floor(v * 0.8f + 64), 23) > 0.7f) return c[frame == 0 ? 0 : 1];
                    return d < core ? c[1] : depth < 3 ? c[3] : c[2];
                case ShotElement.Water:
                {
                    // A bubble: its glint up and to the left, wherever it flies.
                    float gx = wx + inner * 0.35f, gy = wy - inner * 0.35f;
                    if (gx * gx + gy * gy < (frame == 0 ? 1.6f : 1f)) return c[0];
                    return d < core ? c[1] : depth < 3 ? c[3] : c[2];
                }
                default:
                    return d < hot ? c[0] : d < core ? c[1] : c[2];
            }
        }

        // A shot flying at the step's angle, the picture's middle where it is. A still one (step under 0) has no tail.
        public static PixelImage Shot(ShotElement element, int reach, int step, int frame)
        {
            float r = reach;
            int half = (int)Math.Ceiling(r * (step < 0 ? 1.2f : 3.5f)) + 1, size = half * 2 + 1;
            var image = new PixelImage(size, size);
            double angle = step < 0 ? 0 : step * 2 * Math.PI / Angles;
            float cos = (float)Math.Cos(angle), sin = (float)Math.Sin(angle);
            float gap = r * 0.74f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // The picture's rows run down; the world's y runs up.
                    float wx = x - half, wy = half - y;
                    float u = wx * cos + wy * sin, v = -wx * sin + wy * cos;
                    float depth = Depth(element, u, v, r, 5);
                    if (depth >= 0)
                    {
                        image[x, y] = Shade(element, depth, u, v, wx, wy, r, frame % Frames);
                        continue;
                    }
                    if (step < 0) continue;
                    // The tail: the same shape, smaller and fainter the farther back, stepping between the frames.
                    for (int k = 1; k <= TailPieces; k++)
                    {
                        float scale = 0.62f - k * 0.12f, back = gap * (k + 0.55f + (frame % Frames) * 0.35f);
                        // (A flame is drawn out already, and a clod's crumbs are round.)
                        var piece = element == ShotElement.Stone || element == ShotElement.Iron || element == ShotElement.Crystal || element == ShotElement.Frost ? element : ShotElement.Ember;
                        if (Depth(piece, (u + back) / scale, v / scale, r, 5) * scale < 0.4f) continue;
                        var c = Ramps[(int)element];
                        image[x, y] = k == 1 ? c[2] : k == 2 ? c[2].Mix(TailRed, 0.5f).WithAlpha(215) : TailRed.WithAlpha((byte)(k == 3 ? 170 : 110));
                        break;
                    }
                }
            }
            return image;
        }

        // Where it ended: a white spark, a ring opening out and thinning, bits of what it was made of flying off.
        public static PixelImage Pop(ShotElement element, int frame)
        {
            var c = Ramps[(int)element];
            var image = new PixelImage(PopSize, PopSize);
            float mid = PopSize / 2, t = (frame + 0.5f) / PopFrames;
            float ring = 3.5f + 10.5f * (1 - (1 - t) * (1 - t)), thick = frame < 2 ? 2f : frame < 4 ? 1.2f : 0.8f;
            for (int y = 0; y < PopSize; y++)
            {
                for (int x = 0; x < PopSize; x++)
                {
                    float dx = x - mid, dy = y - mid, d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (frame == 0 && d < 3.2f) image[x, y] = d < 2 ? Rgba.White : c[1];
                    else if (Math.Abs(d - ring) < thick / 2 + 0.25f)
                    {
                        // The ring breaks up as it goes.
                        if (frame >= 3 && ((x + y * 3) & 3) < frame - 2) continue;
                        image[x, y] = frame < 2 ? c[0] : frame < 3 ? c[1] : RimRed.WithAlpha((byte)(frame == 3 ? 220 : 150));
                    }
                }
            }
            // Six bits, slowing as they go, the far ones going dark.
            for (int i = 0; i < 6; i++)
            {
                double a = i * Math.PI / 3 + 0.5 + (int)element * 0.7;
                float far = (4 + 9.5f * (1 - (1 - t) * (1 - t))) * (i % 2 == 0 ? 1 : 0.72f);
                int bx = (int)Math.Round(mid + Math.Cos(a) * far), by = (int)Math.Round(mid - Math.Sin(a) * far + t * t * 2);
                var colour = frame < 2 ? c[1] : frame < 4 ? c[2] : c[3];
                bool big = i % 2 == 0 ? frame < 4 : frame < 2;
                for (int oy = 0; oy < (big ? 2 : 1); oy++)
                    for (int ox = 0; ox < (big ? 2 : 1); ox++)
                        if (image.Inside(bx + ox, by + oy)) image[bx + ox, by + oy] = colour;
                if (big && image.Inside(bx, by)) image[bx, by] = c[0];
            }
            return image;
        }

        // A dot of the line that shows where a shot will go before it goes: red, edged dark so it reads on any floor.
        public static PixelImage AimDot()
        {
            var image = new PixelImage(3, 3);
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                    image[x, y] = x == 1 && y == 1 ? Rgba.Hex("#FFE0D0") : x == 1 || y == 1 ? RimRed : EdgeDark.WithAlpha(200);
            return image;
        }
    }
}
