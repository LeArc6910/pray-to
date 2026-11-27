using System;
using System.Collections.Generic;

namespace Game.Client
{
    // Which of the bosses' own effects (E6, Docs/EffectsRework.md 11).
    enum BossFx : byte
    {
        // ---- the crystal queen ----
        // A beam of light along a line from the origin: thin, full, narrowing, then a thin line left. Size its
        // length, Width how thick, Dir its way.
        Beam,
        // What comes down onto a warned circle (a loop over the warning, Progress how full it is): its shadow
        // growing on the floor, then the thing itself with a streak behind. Size the circle's radius. Count what
        // falls: 0 a crystal, 1 a ball of fire, 2 burning arrows.
        ShardDrop,
        // Where it struck: it stands in the floor with splinters sprung up round it, then all of it breaks.
        ShardFall,
        // Crystals standing up round a ring and breaking (the floor ringing). Size the ring's radius; Inner, if set,
        // the wave runs on out to Size from crystals standing at Inner.
        CrystalRing,
        // Fangs closing at the end of a wedge: a star and shards flung on along Dir. Size the reach.
        Fangs,
        // Light coming down as a column onto a circle. Size the circle's radius.
        Column,
        // Shards and sparks thrown one way: Count of them over Width degrees round Dir, out to Size.
        Spray,

        // ---- shared by the bosses ----
        // A few bits thrown one way, in what the ramp is made of (sparks of fire, chips of crystal, stones, sand,
        // wisps): Count of them round Dir, out to Size. What the old square dots were.
        Cinders,

        // ---- the mad general ----
        // Fire running round a ring's band from Dir: tongues of flame in rows, soot under them, sparks rising. Size the
        // band's outer radius, Inner its inner.
        FireBand,
        // The burning blade through a wedge: a thick smear swept across it, flame along its track, sparks flung on. Size
        // the reach, Width the wedge in degrees, Dir its middle. Big: the blade white-hot (his last phase).
        Cleave,
        // Fire where he pointed: an explosion and what burns on after. Size its radius. Count 2: the volley's arrows.
        FireCall,
        // Ghostly spears thrust up along a line from its middle outward. Size the line's length, Dir its way.
        Spears,
    }

    // What one of the bosses' effects is told. Lengths in pixels, angles in degrees.
    struct BossContext
    {
        public float Size;
        public float Width;
        public float Inner;
        public float Dir;
        public float Progress;
        public int Count;
        public int Seed;
        // The heavier of two blows that share an effect.
        public bool Big;
        public FxRamp Ramp;
    }

    // The bosses' own effects as functions of time that add pieces to a list, as the skills' (SkillFx) and the
    // monsters' (MonsterFx) are, built of the same layers (FxLayers). The blow's shape itself (the warning's shape
    // burning) and its scar are the shared landing's (WorldView.Hazards.cs); these are what each boss adds over it.
    // Positions are in pixels from the effect's origin, y up. At most Most pieces at once.
    static class BossEffects
    {
        // The most pieces one effect may lay at once (Docs/EffectsRework.md 3).
        public const int Most = 72;

        static float U(float t, float from, float to) => SkillFx.U(t, from, to);
        static bool In(float t, float from, float to) => SkillFx.In(t, from, to);
        static float Out(float u) => SkillFx.Out(u);
        static float Rnd(int seed, int i, int salt) => SkillFx.Rnd(seed, i, salt);
        static float Cos(float degrees) => SkillFx.Cos(degrees);
        static float Sin(float degrees) => SkillFx.Sin(degrees);
        static int Even(float size) => Math.Max(2, 2 * (int)Math.Round(size / 2));
        static int Odd(float size) => Even(size) + 1;

        static void Put(List<FxStamp> o, FxShape shape, FxRamp ramp, int size, int frame, float x, float y, float alpha = 1, float angle = 0, bool ground = false) =>
            SkillFx.Put(o, shape, ramp, size, frame, x, y, alpha, angle, ground);

        // The pieces of an effect at t seconds of its life; false once it is over. A loop (ShardDrop) is always true.
        public static bool Stamps(BossFx fx, in BossContext c, float t, List<FxStamp> o)
        {
            if (t < 0) return true;
            float life;
            switch (fx)
            {
                case BossFx.Beam: life = Beam(c, t, o); break;
                case BossFx.ShardDrop: ShardDrop(c, t, o); return true;
                case BossFx.ShardFall: life = ShardFall(c, t, o); break;
                case BossFx.CrystalRing: life = CrystalRing(c, t, o); break;
                case BossFx.Fangs: life = Fangs(c, t, o); break;
                case BossFx.Column: life = Column(c, t, o); break;
                case BossFx.Spray: life = Spray(c, t, o); break;
                case BossFx.Cinders: life = Cinders(c, t, o); break;
                case BossFx.FireBand: life = FireBand(c, t, o); break;
                case BossFx.Cleave: life = Cleave(c, t, o); break;
                case BossFx.FireCall: life = FireCall(c, t, o); break;
                case BossFx.Spears: life = Spears(c, t, o); break;
                default: return false;
            }
            return t < life;
        }

        // ---- the crystal queen ----

        // Stretches of beam laid end to end from the origin along dir, the last drawn back so it ends at the length.
        static void BeamBody(List<FxStamp> o, FxRamp m, float length, int thick, int frame, float dir, float alpha = 1, float x = 0, float y = 0)
        {
            float dx = Cos(dir), dy = Sin(dir);
            for (float d = 16; ; d += 32)
            {
                float mid = length < 32 ? length / 2 : Math.Min(d, length - 16);
                Put(o, FxShape.Beam, m, thick, frame, x + dx * mid, y + dy * mid, alpha, dir);
                if (d >= length - 16) break;
            }
        }

        static float Beam(in BossContext c, float t, List<FxStamp> o)
        {
            const float life = 0.45f, line = 0.2f;
            var m = c.Ramp;
            float L = Math.Max(16, c.Size), W = Math.Max(6, c.Width), dx = Cos(c.Dir), dy = Sin(c.Dir);
            if (t >= life)
            {
                // A thin line of it stays a moment.
                BeamBody(o, m, L, 1, 0, c.Dir, 1 - (t - life) / line);
                return life + line;
            }
            // Thin, then full, then narrowing.
            float body = t < 0.04f ? 0.35f : t < 0.3f ? 1 : (life - t) / 0.15f;
            BeamBody(o, m, L, Even(W * body), (int)(t * 25) & 3, c.Dir);
            // A flare at either end.
            int flare = Odd(W * 1.5f * body);
            Put(o, FxShape.Lump, m, Even(W * 0.9f * body), (int)(t * 20) & 3, 0, 0);
            Put(o, FxShape.Glint, m, flare, 1, 0, 0);
            Put(o, FxShape.Glint, m, flare, 1, dx * L, dy * L);
            // Splinters of light shed off its sides.
            for (int i = 0; i < 10; i++)
            {
                float p = SkillFx.Frac(t * 2.5f + Rnd(c.Seed, i, 3)), along = Rnd(c.Seed, i, 4) * L + p * 8, side = (i & 1) == 0 ? 1 : -1;
                float off = side * (W / 2 + 2 + p * 10);
                Put(o, FxShape.Shard, m, Even(6 - 4 * p), 0, dx * along - dy * off, dy * along + dx * off, (1 - p) * U(body, 0, 0.5f), c.Dir + side * 65);
            }
            return life + line;
        }

        static void ShardDrop(in BossContext c, float t, List<FxStamp> o)
        {
            float p = Math.Clamp(c.Progress, 0, 1), R = c.Size;
            int tall = c.Big ? 32 : 28;
            Put(o, FxShape.Shadow, FxRamp.Smoke, Even(4 + R * 0.9f * p), 0, 0, -1, 0.35f + 0.3f * p, 0, true);
            if (p <= 0.72f) return;
            // The last quarter of the warning it is seen, falling faster and faster.
            float fall = (1 - p) / 0.28f, y = tall / 2f - 2 + fall * fall * 110;
            if (c.Count == 1)
            {
                // A ball of fire, its tail above it.
                Put(o, FxShape.Fireball, c.Ramp, 14, (int)(t * 12) % 3, 0, y, 1, -90);
                FxLayers.Light(o, c.Ramp, R * 1.6f, 0.5f * (1 - fall));
                return;
            }
            if (c.Count == 2)
            {
                // Three burning arrows, a little apart, slanting in.
                for (int i = -1; i <= 1; i++)
                {
                    float ax = i * R * 0.4f + y * 0.25f, ay = y + (i == 0 ? 0 : 9);
                    Put(o, FxShape.Streak, c.Ramp, 16, 1, ax + 3, ay + 13, 0.6f, -104);
                    Put(o, FxShape.Arrow, FxRamp.Dust, 12, 0, ax, ay, 1, -104);
                }
                return;
            }
            Put(o, FxShape.Streak, c.Ramp, 28, 2, 0, y + tall / 2f + 12, 0.6f, -90);
            Put(o, FxShape.Shard, c.Ramp, tall, 2, 0, y, 1, -90);
        }

        static float ShardFall(in BossContext c, float t, List<FxStamp> o)
        {
            const float hold = 0.5f, over = 0.86f;
            var m = c.Ramp;
            float R = c.Size;
            int tall = c.Big ? 32 : 28;
            if (t < 0.08f) FxLayers.Light(o, m, R * 2.8f, 1, 0, 0, 3);
            if (t < 0.45f) FxLayers.Light(o, m, R * 2.2f, 0.8f * (1 - t / 0.45f), 0, 0, 2);
            FxLayers.Shock(o, m, t, 0, R);
            if (t < hold)
            {
                // Splinters spring up round it, leaning out, one after another.
                for (int i = 0; i < 6; i++)
                {
                    float lean = 20 + i * 28, size = (11 + 5 * (i & 1)) * Out(U(t, 0.02f * i, 0.02f * i + 0.08f));
                    if (size < 3) continue;
                    Put(o, FxShape.Shard, m, Even(size), 0, Cos(lean) * (R * 0.5f + size * 0.5f), -2 + Sin(lean) * size * 0.5f, 1, lean);
                }
                Put(o, FxShape.Shard, m, tall, 2, 0, tall / 2f - 2 - 3 * Out(U(t, 0, 0.06f)), 1, -90);
            }
            else if (t < over)
            {
                float p = (t - hold) / (over - hold);
                for (int i = 0; i < 9; i++)
                {
                    float dir = i * 40 + 12;
                    Put(o, FxShape.Shard, m, Even(7 - 4 * p), 0, Cos(dir) * 15 * Out(p), 7 + Sin(dir) * 10 * Out(p) - 13 * p * p, 1 - p * p, dir + 340 * p);
                }
            }
            if (t < 0.06f)
            {
                Put(o, FxShape.Lump, m, Even(R * 1.1f), 0, 0, 0);
                Put(o, FxShape.Glint, m, Odd(R * 2.3f), 1, 0, 0);
            }
            FxLayers.Embers(o, m, 8, t, 0, R * 1.1f, c.Seed);
            return over;
        }

        static float CrystalRing(in BossContext c, float t, List<FxStamp> o)
        {
            var m = c.Ramp;
            float stand = c.Inner > 0 ? c.Inner : c.Size;
            int count = Math.Clamp((int)(stand / 4.5f), 8, 18);
            float lit = Math.Min(96, c.Size * 2);
            if (t < 0.08f) FxLayers.Light(o, m, lit * 1.2f, 0.9f, 0, 0, 3);
            if (t < 0.4f) FxLayers.Light(o, m, lit, 0.7f * (1 - t / 0.4f), 0, 0, 2);
            SkillFx.Crystals(o, m, count, t, 0, 0.3f, 0.62f, stand, c.Big ? 18 : 14, c.Seed);
            // The wave on the floor (a ring too large for one picture is the shared landing's wave).
            if (c.Size <= 80) FxLayers.Shock(o, m, t, 0, c.Size, 0, 0, 0.32f, true);
            // Chips thrown out from where they stand.
            for (int i = 0; i < 8; i++)
            {
                float p = t / 0.4f, dir = (i + Rnd(c.Seed, i, 5)) * 45;
                if (p >= 1) break;
                Put(o, FxShape.Ember, m, 2 + (int)Math.Round(4 * (1 - p)), p < 0.5f ? 0 : 1, Cos(dir) * (stand + 14 * Out(p)), Sin(dir) * (stand + 14 * Out(p)) * 0.9f, Math.Min(1, 2 - 2 * p), dir);
            }
            return 0.62f;
        }

        static float Fangs(in BossContext c, float t, List<FxStamp> o)
        {
            var m = c.Ramp;
            float far = c.Size * 0.55f, bx = Cos(c.Dir) * far, by = Sin(c.Dir) * far;
            if (t < 0.1f)
            {
                Put(o, FxShape.Glint, m, Odd(c.Size * 0.5f), 2, bx, by);
                FxLayers.Light(o, m, c.Size * 1.6f, 0.9f, bx, by, 3);
            }
            // Two fangs' cuts closing from either side.
            if (t < 0.16f)
            {
                float open = 30 * (1 - Out(U(t, 0, 0.08f)));
                for (int side = -1; side <= 1; side += 2)
                    Put(o, FxShape.Shard, m, Even(c.Size * 0.42f), 1, bx + Cos(c.Dir + side * 90) * (4 + open * 0.3f), by + Sin(c.Dir + side * 90) * (4 + open * 0.3f), 1 - t / 0.16f, c.Dir + side * (150 - open));
            }
            for (int i = 0; i < 7; i++)
            {
                float own = 0.3f + 0.12f * Rnd(c.Seed, i, 6), p = t / own;
                if (p >= 1) continue;
                float dir = c.Dir + (i - 3) * 14, go = c.Size * 0.5f * Out(p);
                Put(o, FxShape.Shard, m, Even(8 - 5 * p), 0, bx + Cos(dir) * go, by + Sin(dir) * go + 6 * p * (1 - p), 1 - p * p, dir + 300 * p * ((i & 1) == 0 ? 1 : -1));
            }
            FxLayers.Embers(o, m, 8, t, 0, c.Size * 0.5f, c.Seed, bx, by, 100, c.Dir);
            return 0.46f;
        }

        static float Column(in BossContext c, float t, List<FxStamp> o)
        {
            const float life = 0.5f, line = 0.2f;
            var m = c.Ramp;
            float R = c.Size;
            if (t >= life)
            {
                BeamBody(o, m, 96, 1, 0, 90, 1 - (t - life) / line);
                return life + line;
            }
            float body = t < 0.04f ? 0.4f : t < 0.32f ? 1 : (life - t) / 0.18f;
            if (t < 0.08f) FxLayers.Light(o, m, R * 3, 1, 0, 0, 3);
            FxLayers.Light(o, m, R * 2.4f, 0.85f * body, 0, 0, 2);
            // The floor it comes down on: two rings running out.
            FxLayers.Shock(o, m, t, 0, R, 0, 0, 0.3f, true);
            FxLayers.Shock(o, m, t, 0.14f, R * 0.7f, 0, 0, 0.3f, true);
            BeamBody(o, m, 96, Even(R * 1.1f * body), (int)(t * 25) & 3, 90);
            Put(o, FxShape.Lump, m, Even(R * 1.3f * body), (int)(t * 20) & 3, 0, 1);
            if (t < 0.1f) Put(o, FxShape.Glint, m, Odd(R * 2.2f), 1, 0, 0);
            // Motes carried up its sides.
            for (int i = 0; i < 10; i++)
            {
                float p = SkillFx.Frac(t * 2.2f + Rnd(c.Seed, i, 7)), side = (i & 1) == 0 ? 1 : -1;
                Put(o, FxShape.Shard, m, Even(6 - 3 * p), 0, side * (R * 0.55f + 3 + 5 * Rnd(c.Seed, i, 8)), 4 + p * 70, (1 - p) * U(body, 0, 0.5f), 90);
            }
            return life + line;
        }

        static float Spray(in BossContext c, float t, List<FxStamp> o)
        {
            var m = c.Ramp;
            int count = Math.Clamp(c.Count, 1, 16);
            float cone = c.Width <= 0 ? 360 : c.Width;
            for (int i = 0; i < count; i++)
            {
                float own = 0.34f + 0.2f * Rnd(c.Seed, i, 9), p = t / own;
                if (p >= 1) continue;
                float dir = c.Dir + ((i + Rnd(c.Seed, i, 10)) / count - 0.5f) * cone, go = c.Size * (0.25f + 0.75f * Out(p));
                Put(o, FxShape.Shard, m, Even(7 - 4 * p), i & 1, Cos(dir) * go, Sin(dir) * go * 0.9f + 10 * p * (1 - p), 1 - p * p, dir + 260 * p * ((i & 1) == 0 ? 1 : -1));
            }
            FxLayers.Embers(o, m, Math.Min(10, count + 2), t, 0, c.Size * 0.8f, c.Seed + 3, 0, 0, cone, c.Dir);
            return 0.56f;
        }

        // ---- shared by the bosses ----

        static float Cinders(in BossContext c, float t, List<FxStamp> o)
        {
            var m = c.Ramp;
            int count = Math.Clamp(c.Count, 1, 8);
            float reach = c.Size <= 0 ? 14 : c.Size;
            for (int i = 0; i < count; i++)
            {
                float own = 0.3f + 0.2f * Rnd(c.Seed, i, 11), p = t / own;
                if (p >= 1) continue;
                float dir = c.Dir + (Rnd(c.Seed, i, 12) - 0.5f) * (c.Width <= 0 ? 70 : c.Width), go = reach * (0.2f + 0.8f * Out(p)) * (0.6f + 0.4f * Rnd(c.Seed, i, 13));
                float x = Cos(dir) * go + (Rnd(c.Seed, i, 14) - 0.5f) * 5, y = Sin(dir) * go + (Rnd(c.Seed, i, 15) - 0.5f) * 5;
                switch (m)
                {
                    case FxRamp.Crystal:
                    case FxRamp.Cold:
                        Put(o, FxShape.Shard, m, Even(6 - 3 * p), 0, x, y, 1 - p * p, dir + 240 * p);
                        break;
                    case FxRamp.Dust:
                    case FxRamp.Smoke:
                        // Stones go up and come down.
                        Put(o, FxShape.Chunk, m, 3 + (i & 1), 4 + (i & 3), x, y + 9 * p * (1 - p) * 4 * 0.5f, 1 - p * p * p);
                        break;
                    case FxRamp.Sand:
                        Put(o, FxShape.Grains, m, 8 + 2 * (i & 1), (int)(t * 12) + i, x, y + 6 * p * (1 - p), 1 - p * p, dir);
                        break;
                    case FxRamp.Spirit:
                        Put(o, FxShape.Wisp, m, 3 + (i & 1), (int)(t * 10) + i, x, y, 1 - p, dir);
                        break;
                    default:
                        Put(o, FxShape.Ember, m, 2 + (int)Math.Round(4 * (1 - p)), p < 0.5f ? 0 : 1, x, y, Math.Min(1, 2 - 2 * p), dir);
                        break;
                }
            }
            return 0.5f;
        }

        // ---- the mad general ----

        static float FireBand(in BossContext c, float t, List<FxStamp> o)
        {
            const float life = 0.95f;
            var m = c.Ramp;
            float inner = c.Inner, outer = c.Size, band = outer - inner, mid = (inner + outer) / 2;
            float left = U(life - 0.1f - t, 0, 0.35f);
            int rows = band > 44 ? 3 : 2;
            float lit = Math.Min(110, outer * 1.5f);
            if (t < 0.08f) FxLayers.Light(o, m, lit * 1.2f, 1, 0, 0, 3);
            FxLayers.Light(o, m, lit, 0.8f * left, 0, 0, 1);
            // Soot under the fire, a patch here and there round the band.
            int soot = Math.Clamp((int)(mid / 9), 5, 8);
            for (int i = 0; i < soot; i++)
            {
                float dir = c.Dir + (i + 0.5f) * 360f / soot;
                Put(o, FxShape.Scorch, FxRamp.Smoke, SkillFxArt.Step4(Math.Min(24, band * 0.7f)), 4 + (i & 3), Cos(dir) * mid, Sin(dir) * mid, 0.6f * U(life - t, 0, 0.25f) * U(t, 0, 0.1f), 0, true);
            }
            // The band's own edges, crisp for a moment.
            if (t < 0.3f && outer <= 80)
            {
                Put(o, FxShape.Edge, m, SkillFxArt.Even(2 * outer), 1, 0, 0, 1 - t / 0.3f, 0, true);
                Put(o, FxShape.Edge, m, SkillFxArt.Even(2 * inner), 1, 0, 0, 1 - t / 0.3f, 0, true);
            }
            for (int row = 0; row < rows; row++)
            {
                float far = rows == 2 ? (row == 0 ? inner + 6 : outer - 6) : row == 0 ? inner + 6 : row == 1 ? mid : outer - 6;
                int n = Math.Clamp((int)(2 * Math.PI * far / 20), 6, 16);
                for (int i = 0; i < n; i++)
                {
                    // The fire runs round both ways from where it was lit.
                    float turn = (i + 0.5f * (row & 1)) / n, dir = c.Dir + turn * 360;
                    float own = t - row * 0.04f - 0.2f * Math.Min(turn, 1 - turn);
                    if (own < 0) continue;
                    float leap = own < 0.18f ? 1 + Sin(own / 0.18f * 180) : 1;
                    int size = Even((9 + 5 * Rnd(c.Seed, i + row * 20, 16)) * leap * left);
                    if (size < 4) continue;
                    Put(o, FxShape.Flame, m, size, (int)(t * 14) + i + row, Cos(dir) * far, Sin(dir) * far + size / 2f - 2);
                }
            }
            // Sparks carried up off it.
            for (int i = 0; i < 12; i++)
            {
                float p = SkillFx.Frac(t * 1.3f + Rnd(c.Seed, i, 17)), dir = c.Dir + i * 30;
                Put(o, FxShape.Ember, m, 3, p < 0.5f ? 0 : 1, Cos(dir) * mid + Sin(p * 400 + i * 50) * 3, Sin(dir) * mid + 4 + p * 22, left * (1 - p), 90);
            }
            return life;
        }

        static float Cleave(in BossContext c, float t, List<FxStamp> o)
        {
            const float sweep = 0.1f;
            var m = c.Ramp;
            float R = c.Size, wide = Math.Clamp(c.Width, 20, 300), half = wide / 2;
            float swept = U(t, 0, sweep), left = U(0.7f - t, 0, 0.3f);
            float mx = Cos(c.Dir) * R * 0.6f, my = Sin(c.Dir) * R * 0.6f;
            if (t < 0.06f) FxLayers.Light(o, m, R * 2, 1, mx, my, 3);
            if (t < 0.4f) FxLayers.Light(o, m, R * 1.7f, 0.8f * (1 - t / 0.4f), mx, my, 1);
            // Soot along the blade's track.
            for (int i = 0; i < 5; i++)
            {
                float dir = c.Dir - half + wide * (i + 0.5f) / 5;
                if ((i + 0.5f) / 5 > swept) continue;
                Put(o, FxShape.Scorch, FxRamp.Smoke, 12, 4 + (i & 3), Cos(dir) * R * 0.8f, Sin(dir) * R * 0.8f, 0.6f * left, 0, true);
            }
            if (t < 0.26f)
            {
                // The smear's head runs across the wedge in a tenth of a second and its tail follows.
                float fade = t < 0.12f ? 1 : (0.26f - t) / 0.14f;
                int span = 10 * Math.Max(2, (int)Math.Round(wide * (0.25f + 0.75f * swept) / 10));
                float head = c.Dir - half + wide * swept;
                Put(o, FxShape.Smear, m, SkillFxArt.Even(R * 0.62f), span, 0, 0, fade * 0.8f, head - span / 2f);
                Put(o, FxShape.Smear, m, SkillFxArt.Even(R * 0.82f), span, 0, 0, fade * 0.9f, head - span / 2f);
                Put(o, FxShape.Smear, c.Big ? FxRamp.Steel : m, SkillFxArt.Even(R), span, 0, 0, fade, head - span / 2f);
            }
            for (int i = 0; i < 9; i++)
            {
                float turn = (i + 0.5f) / 9, dir = c.Dir - half + wide * turn, own = t - sweep * turn;
                if (own < 0) continue;
                float fx = Cos(dir) * R * 0.88f, fy = Sin(dir) * R * 0.88f;
                int tall = Even(12 * U(0.5f - own, 0, 0.3f));
                if (tall >= 4) Put(o, FxShape.Flame, m, tall, (int)(t * 14) + i, fx, fy + tall / 2f - 2);
                if ((i & 1) == 1)
                {
                    tall = Even(9 * U(0.4f - own, 0, 0.3f));
                    if (tall >= 4) Put(o, FxShape.Flame, m, tall, (int)(t * 14) + i + 9, Cos(dir) * R * 0.5f, Sin(dir) * R * 0.5f + tall / 2f - 2);
                }
                float p = own / 0.35f;
                if (p < 1) Put(o, FxShape.Ember, c.Big ? FxRamp.Gold : m, 2 + (int)Math.Round(4 * (1 - p)), p < 0.5f ? 0 : 1, fx + Cos(dir) * 18 * Out(p), fy + Sin(dir) * 18 * Out(p), 1 - p, dir);
            }
            return 0.7f;
        }

        static float FireCall(in BossContext c, float t, List<FxStamp> o)
        {
            float R = c.Size;
            float life = FxLayers.Blast(o, t, R, c.Ramp, c.Seed, 10, 0, true, 1.0f);
            SkillFx.Flames(o, 4, t, 0.25f, 1.0f, R, c.Seed);
            if (c.Count == 2 && t < 0.5f)
            {
                // The volley's shafts, broken and thrown.
                for (int i = 0; i < 3; i++)
                {
                    float p = t / 0.5f, dir = 40 + i * 50;
                    Put(o, FxShape.Arrow, FxRamp.Dust, 9, 0, Cos(dir) * R * 0.7f * Out(p), 4 + Sin(dir) * R * 0.5f * Out(p) + 30 * p * (1 - p), 1 - p * p, dir + 500 * p);
                }
            }
            return life;
        }

        static float Spears(in BossContext c, float t, List<FxStamp> o)
        {
            var m = c.Ramp;
            float L = c.Size, dx = Cos(c.Dir), dy = Sin(c.Dir);
            int count = Math.Clamp((int)(L / 18), 4, 16);
            for (int i = 0; i < count; i++)
            {
                // From the middle outward, each a moment after the one inside it, leaning a little by turns.
                float along = (i + 0.5f) / count, born = 0.25f * Math.Abs(along - 0.5f), own = t - born;
                if (own < 0 || own >= 0.5f) continue;
                float lean = 90 + ((i & 1) == 0 ? -9 : 9), bx = dx * L * along, by = dy * L * along;
                int tall = 26 + 4 * (i % 3);
                float up = tall * (Out(U(own, 0, 0.07f)) - 0.5f);
                if (own < 0.3f) Put(o, FxShape.Spear, m, tall, 0, bx + Cos(lean) * up, by + Sin(lean) * up, own < 0.22f ? 1 : (0.3f - own) / 0.08f, lean);
                // Its afterimage lifts off it and thins.
                if (own > 0.1f) Put(o, FxShape.Spear, m, tall, 1, bx + Cos(lean) * (up + 5 + 14 * (own - 0.1f)), by + Sin(lean) * (up + 5 + 14 * (own - 0.1f)), 0.7f * (0.5f - own) / 0.4f, lean);
                if (own < 0.08f) Put(o, FxShape.Glint, m, 7, 0, bx, by);
            }
            FxLayers.Embers(o, m, 8, t, 0, 20, c.Seed, dx * L / 2, dy * L / 2);
            return 0.25f * 0.5f + 0.5f;
        }
    }
}
