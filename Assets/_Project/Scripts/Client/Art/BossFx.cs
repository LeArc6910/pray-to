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
        // A crystal on its way down onto a warned circle (a loop over the warning, Progress how full it is): its
        // shadow growing on the floor, then the crystal itself with a streak of light behind. Size the circle's radius.
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
    }
}
