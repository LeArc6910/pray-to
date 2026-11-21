using System;
using System.Collections.Generic;

namespace Game.Client
{
    // One piece of an effect this frame: a picture from SkillFxArt, where it sits from the effect's origin in pixels
    // (x right, y up), how it is turned (degrees, counterclockwise) and how solid it is.
    struct FxStamp
    {
        public FxShape Shape;
        public FxRamp Ramp;
        public int Size;
        public int Frame;
        public float X;
        public float Y;
        public float Angle;
        public float Alpha;
        // On the floor, under everyone (cracks, burns, rune circles, shadows); otherwise over everyone.
        public bool Ground;

        public long Key => ((long)Shape << 48) | ((long)Ramp << 40) | ((long)(Size & 0xFFFF) << 16) | (long)(Frame & 0xFFFF);
    }

    enum FxProgram
    {
        // What happens where a skill is used.
        Cast,
        // A skill's projectile in flight, drawn round where it is.
        Shot,
        // Where a skill's projectile lands or ends.
        Hit,
        // Lightning jumping between two enemies (origin at the middle, Length and Dir its line).
        Chain,
        // The quiet cue of a buff that is running, for as long as it runs.
        Loop,
    }

    struct FxContext
    {
        // The real reach of the skill in pixels (its radius and the player's), so the effect's edge is where it hits.
        public float Radius;
        // The way the caster faces or the projectile flies, in radians.
        public float Dir;
        // A dash's way back to where it began, or a bolt's length, in pixels.
        public float Length;
        public int Seed;
        // The colours of a skill that has no element of its own: steel or wind, or the element a buff lends.
        public FxRamp Main;
        // Other places the effect reaches, as x, y pairs in pixels from the origin (the caster's spirits).
        public float[] Points;
        // How far above the origin (the caster's feet) the middle of the skill's reach is, in pixels: a nova's whole
        // effect is lifted by this, so its edge is centred where the host measures from.
        public float Lift;
    }

    // Every skill's effect as a function of time (Docs/SkillEffects.md): for a moment after the skill is used it says
    // which pieces are where. Each has three parts, a short gathering (anticipation), the blow itself (the most pieces
    // and the brightest, the shortest part) and what is left to thin out (smoke, embers, mist, motes). Nothing here
    // keeps state, so the game (WorldView.SkillFx) and the preview tool (DevTools/ArtPreview) draw the very same thing.
    static class SkillFx
    {
        // The gathering before a blow, in seconds: the skill has already happened on the host, so it is short.
        public const float Lead = 0.1f;
        // The middle of a body above its feet, in pixels.
        const float Body = 8;
        const float Deg = 180f / (float)Math.PI;

        static readonly HashSet<string> Known = new HashSet<string>
        {
            "whirlwind", "war_cry", "charge", "ground_slam", "iron_skin", "blade_storm",
            "fireball", "flame_brand", "fire_ring", "meteor", "flame_burst", "inferno",
            "volley", "eagle_eye", "gale_step", "power_shot", "explosive_arrow", "arrow_rain",
            "spirits", "spirit_pact", "life_spring", "spirit_horde", "spirit_blast", "guardian",
            "chain_lightning", "frost_ring", "ice_lance", "storm_mantle", "thunderclap", "blizzard",
        };

        // A skill without an effect of its own (a new one in the sheet) keeps the old look of its kind.
        public static bool Knows(string skill) => skill != null && Known.Contains(skill);

        public static bool HasLoop(string skill) =>
            skill == "war_cry" || skill == "iron_skin" || skill == "flame_brand" || skill == "eagle_eye" || skill == "spirit_pact" || skill == "storm_mantle";

        // The colours of a skill with no element of its own.
        public static FxRamp MainOf(string skill, bool fire, bool cold, bool lightning)
        {
            if (fire) return FxRamp.Fire;
            if (cold) return FxRamp.Cold;
            if (lightning) return FxRamp.Bolt;
            return skill == "volley" || skill == "gale_step" || skill == "power_shot" || skill == "arrow_rain" || skill == "explosive_arrow" ? FxRamp.Wind : FxRamp.Steel;
        }

        // Adds this moment's pieces to the list. False once the effect is over (a Shot or a Loop never is).
        public static bool Stamps(FxProgram program, string skill, in FxContext c, float t, List<FxStamp> o)
        {
            if (t < 0) return true;
            switch (program)
            {
                case FxProgram.Cast:
                {
                    int from = o.Count;
                    bool alive = t < Cast(skill, c, t, o);
                    for (int i = from; c.Lift != 0 && i < o.Count; i++)
                    {
                        var stamp = o[i];
                        stamp.Y += c.Lift;
                        o[i] = stamp;
                    }
                    return alive;
                }
                case FxProgram.Hit: return t < Hit(skill, c, t, o);
                case FxProgram.Chain: return t < Chain(c, t, o);
                case FxProgram.Shot:
                    Shot(skill, c, t, o);
                    return true;
                default:
                    Loop(skill, c, t, o);
                    return true;
            }
        }

        // ---- helpers ----

        static void Put(List<FxStamp> o, FxShape shape, FxRamp ramp, int size, int frame, float x, float y, float alpha = 1, float angle = 0, bool ground = false)
        {
            if (alpha <= 0.03f || size <= 0) return;
            o.Add(new FxStamp { Shape = shape, Ramp = ramp, Size = size, Frame = frame, X = x, Y = y, Alpha = Math.Min(1, alpha), Angle = angle, Ground = ground });
        }

        static float U(float t, float from, float to) => Math.Clamp((t - from) / (to - from), 0, 1);

        static bool In(float t, float from, float to) => t >= from && t < to;

        // Fast, then slow: sizes grow on this so the last one is the one that reads (rule 2).
        static float Out(float u) => 1 - (1 - u) * (1 - u) * (1 - u);

        static float Rnd(int seed, int i, int salt) => PixelNoise.Value(seed * 31 + i, i * 17 + salt, salt);

        static float Cos(float degrees) => (float)Math.Cos(degrees / Deg);

        static float Sin(float degrees) => (float)Math.Sin(degrees / Deg);

        static float Frac(float value) => value - (float)Math.Floor(value);

        // Lines drawn in toward a point: radial lines pull the eye to the middle (rule 3), the anticipation.
        static void Gather(List<FxStamp> o, FxRamp ramp, float t, float until, float radius, int count, int seed, float cx = 0, float cy = Body)
        {
            if (!In(t, 0, until)) return;
            float u = t / until;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Rnd(seed, i, 1) * 0.7f) / count * 360;
                float distance = 4 + (radius * (0.7f + 0.3f * Rnd(seed, i, 2)) - 4) * (1 - u * u);
                Put(o, FxShape.Streak, ramp, 5, 1, cx + Cos(angle) * distance, cy + Sin(angle) * distance, 0.45f + 0.55f * u, angle + 180);
            }
        }

        // The thin edge at the skill's real reach (rule 5): opens fast and slows, thins, and is gone.
        static void Ring(List<FxStamp> o, FxRamp ramp, float t, float from, float radius, int thick = 2, float grow = 0.12f, float life = 0.32f, float cx = 0, float cy = 0, bool ground = false)
        {
            if (!In(t, from, from + life)) return;
            float k = (t - from) / life;
            float diameter = 2 * radius * (0.3f + 0.7f * Out(U(t, from, from + grow)));
            int now = k < 0.4f ? thick : k < 0.7f ? Math.Max(1, thick - 1) : 1;
            Put(o, FxShape.Edge, ramp, SkillFxArt.Even(diameter), now, cx, cy, k < 0.5f ? 1 : 1 - (k - 0.5f) / 0.5f, 0, ground);
        }

        // Bits thrown out from a point, slowing as they go; with a hop they arc up and come down, with a rise they
        // drift up. Turned the way they fly when aimed.
        static void Fly(List<FxStamp> o, FxShape shape, FxRamp ramp, int size, int frame, int count, float t, float from, float life, float near, float far, int seed,
            float hop = 0, float rise = 0, float cx = 0, float cy = 0, bool aimed = false, float cone = 360, float toward = 0, float alpha = 1)
        {
            if (!In(t, from, from + life * 1.2f)) return;
            for (int i = 0; i < count; i++)
            {
                float own = life * (0.75f + 0.45f * Rnd(seed, i, 3));
                float u = (t - from) / own;
                if (u >= 1) continue;
                float angle = toward + ((i + Rnd(seed, i, 4)) / count - 0.5f) * cone;
                float distance = near + (far * (0.6f + 0.4f * Rnd(seed, i, 5)) - near) * Out(u);
                float y = cy + Sin(angle) * distance + (float)Math.Sin(u * Math.PI) * hop + rise * u;
                Put(o, shape, ramp, size, frame, cx + Cos(angle) * distance, y, alpha * (1 - u * u), aimed ? angle : 0);
            }
        }

        // Bits rising from somewhere in a disc, one after another: embers, motes, sparkles.
        static void Float(List<FxStamp> o, FxShape shape, FxRamp ramp, int size, int frame, int count, float t, float from, float to, float radius, float rise, int seed,
            float cx = 0, float cy = 0, float alpha = 1)
        {
            if (!In(t, from, to)) return;
            float span = to - from;
            for (int i = 0; i < count; i++)
            {
                float start = from + span * 0.45f * Rnd(seed, i, 6);
                float u = (t - start) / (span * 0.55f);
                if (u < 0 || u >= 1) continue;
                float angle = Rnd(seed, i, 7) * 360, distance = radius * (float)Math.Sqrt(Rnd(seed, i, 8));
                float sway = (float)Math.Sin(u * 6 + i) * 1.5f;
                Put(o, shape, ramp, size, frame, cx + Cos(angle) * distance + sway, cy + Sin(angle) * distance * 0.6f + rise * u, alpha * (1 - u));
            }
        }

        // Puffs that swell a little, drift up and thin out: smoke, dust, mist. Low in contrast and never for long.
        static void Puffs(List<FxStamp> o, FxRamp ramp, int count, float t, float from, float to, float near, float far, int seed, int size = 7, float alpha = 0.55f,
            float cx = 0, float cy = 0, float rise = 6)
        {
            if (!In(t, from, to)) return;
            float u = (t - from) / (to - from);
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Rnd(seed, i, 9)) / count * 360;
                float distance = near + (far - near) * Out(u) * (0.7f + 0.3f * Rnd(seed, i, 10));
                int frame = u < 0.3f ? 0 : u < 0.55f ? 1 : u < 0.8f ? 2 : 3;
                Put(o, FxShape.Smoke, ramp, size + (i & 1) * 2, frame, cx + Cos(angle) * distance, cy + Sin(angle) * distance * 0.8f + rise * u, alpha * (1 - u * 0.7f));
            }
        }

        // A bolt from one point to another, a new one every 1/25 s.
        static void Zap(List<FxStamp> o, float t, float x0, float y0, float x1, float y1, int seed, bool thick = false, float alpha = 1, FxRamp ramp = FxRamp.Bolt)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 4) return;
            int kind = (int)((uint)(seed * 7 + (int)(t * 25)) % 6);
            Put(o, FxShape.Bolt, ramp, SkillFxArt.Step4(length), kind + (thick ? 8 : 0), (x0 + x1) / 2, (y0 + y1) / 2, alpha, (float)Math.Atan2(dy, dx) * Deg);
        }

        // Lightning's flicker: on, a blink off, on again, off, and a last dim one.
        static float Flicker(float t) => t < 0.05f ? 1 : t < 0.07f ? 0 : t < 0.12f ? 1 : t < 0.14f ? 0 : t < 0.18f ? 0.6f : 0;

        // A flash at the hand the skill leaves from, and sparks off it.
        static float Muzzle(List<FxStamp> o, FxRamp ramp, in FxContext c, float t, int size)
        {
            float hx = (float)Math.Cos(c.Dir) * 9, hy = Body + (float)Math.Sin(c.Dir) * 9;
            Gather(o, ramp, t, 0.06f, 12, 5, c.Seed, hx, hy);
            if (In(t, 0.03f, 0.13f)) Put(o, FxShape.Blob, ramp, size, t < 0.07f ? 0 : t < 0.1f ? 1 : 3, hx, hy);
            Fly(o, FxShape.Dot, ramp, 1, 2, 5, t, 0.05f, 0.22f, 2, 11, c.Seed, cx: hx, cy: hy, cone: 120, toward: c.Dir * Deg);
            return 0.3f;
        }

        // ---- where a skill is used ----

        static float Cast(string skill, in FxContext c, float t, List<FxStamp> o)
        {
            float R = c.Radius, face = c.Dir * Deg;
            int seed = c.Seed;
            var m = c.Main;
            const float A = Lead;
            switch (skill)
            {
                // ---------------- warrior ----------------
                case "whirlwind":
                {
                    // Steel crescents sweep round at the true reach, a turn and a quarter, slowing.
                    Gather(o, m, t, A, R * 0.8f, 6, seed, cy: 5);
                    if (In(t, A, 0.44f))
                    {
                        float u = U(t, A, 0.44f), turn = face + 450 * Out(u), alpha = u < 0.6f ? 1 : 1 - (u - 0.6f) / 0.4f;
                        for (int k = 0; k < 2; k++)
                        {
                            Put(o, FxShape.Crescent, m, (int)R - 1, 140, 0, 0, alpha, turn + k * 180);
                        }
                    }
                    Ring(o, m, t, A, R, 1, 0.08f, 0.2f);
                    Puffs(o, FxRamp.Dust, 6, t, 0.26f, 0.72f, R * 0.7f, R * 0.95f, seed, 5, 0.5f);
                    return 0.72f;
                }
                case "war_cry":
                {
                    // A shout: broken arcs opening from the head one after another, a moment of gold flame at the feet.
                    Gather(o, FxRamp.Gold, t, A, 22, 6, seed, cy: 14);
                    for (int k = 0; k < 3; k++)
                    {
                        float from = A + k * 0.06f;
                        if (!In(t, from, from + 0.3f)) continue;
                        float u = U(t, from, from + 0.3f);
                        Put(o, FxShape.Broken, FxRamp.Gold, SkillFxArt.Even(16 + (26 + k * 14) * Out(u)), SkillFxArt.BrokenFrame(k == 0 ? 2 : 1, 4, k * 2), 0, 14, 1 - u * u);
                    }
                    if (In(t, A, 0.5f))
                    {
                        float u = U(t, A, 0.5f);
                        for (int i = 0; i < 5; i++)
                        {
                            float angle = i * 72 + 20;
                            Put(o, FxShape.Flame, FxRamp.Gold, 7, (int)(t * 14) + i, Cos(angle) * 9, 3 + Sin(angle) * 4 + 8 * u, 1 - u);
                        }
                    }
                    Float(o, FxShape.Dot, FxRamp.Gold, 1, 2, 6, t, 0.35f, 0.8f, 10, 18, seed, cy: 6);
                    return 0.8f;
                }
                case "charge":
                case "gale_step":
                {
                    bool wind = skill == "gale_step";
                    float bx = -(float)Math.Cos(c.Dir), by = -(float)Math.Sin(c.Dir), L = c.Length;
                    // Dust (or a swirl of air) kicked up where the rush began.
                    Puffs(o, wind ? FxRamp.Wind : FxRamp.Dust, 4, t, 0, 0.4f, 2, 9, seed, 5, 0.55f, bx * L, by * L, 4);
                    // Afterimages along the way, the farthest back gone first.
                    int ghosts = wind ? 2 : 4;
                    for (int k = 0; k < ghosts; k++)
                    {
                        float at = L * (1 - (k + 0.6f) / (ghosts + 0.6f)), from = k * 0.05f;
                        if (In(t, 0, from + 0.2f)) Put(o, FxShape.Ghost, m, 1, 0, bx * at, by * at, (wind ? 0.3f : 0.45f) * (1 - U(t, from, from + 0.2f)));
                    }
                    // Streaks along the way, their bright heads forward.
                    if (In(t, 0, 0.28f))
                    {
                        float u = t / 0.28f;
                        int lines = wind ? 10 : 6;
                        for (int i = 0; i < lines; i++)
                        {
                            float along = L * Rnd(seed, i, 11) - 8 * u, aside = (Rnd(seed, i, 12) - 0.5f) * 16;
                            Put(o, FxShape.Streak, m, 12 + 4 * (i % 3), wind ? 1 : 2, bx * along - by * aside, Body + by * along + bx * aside, 1 - u, face);
                        }
                    }
                    if (wind)
                    {
                        // Leaves caught up along the way, then let fall.
                        if (In(t, 0, 0.8f))
                        {
                            float u = t / 0.8f;
                            for (int i = 0; i < 6; i++)
                            {
                                float along = L * (i + 0.5f) / 6 - 14 * Out(u), aside = (float)Math.Sin(u * 7 + i * 2) * 6;
                                Put(o, FxShape.Leaf, FxRamp.Heal, 1, (int)(t * 10) + i, bx * along - by * aside, Body + by * along + bx * aside - 10 * u * u, 1 - u * u);
                            }
                        }
                        Ring(o, FxRamp.Wind, t, 0, 11, 1, 0.08f, 0.22f);
                        return 0.8f;
                    }
                    // The blow at the end of it: a crescent ahead, the edge of its reach, dust.
                    if (In(t, 0, 0.2f)) Put(o, FxShape.Crescent, m, 14, 100, (float)Math.Cos(c.Dir) * 4, Body + (float)Math.Sin(c.Dir) * 4, 1 - U(t, 0.1f, 0.2f), face);
                    Ring(o, m, t, 0, R, 2, 0.08f, 0.22f);
                    Fly(o, FxShape.Dot, FxRamp.Dust, 2, 2, 6, t, 0.08f, 0.4f, 4, 16, seed, rise: 3);
                    return 0.55f;
                }
                case "ground_slam":
                {
                    Gather(o, FxRamp.Dust, t, A, R * 0.6f, 8, seed, cy: 2);
                    // Cracks run out from the blow and stay a moment.
                    if (In(t, A, 0.95f))
                    {
                        float grown = Math.Max(0.3f, Out(U(t, A, A + 0.08f))), alpha = t < 0.6f ? 0.9f : 0.9f * (1 - U(t, 0.6f, 0.95f));
                        for (int i = 0; i < 7; i++)
                        {
                            float angle = (i + Rnd(seed, i, 13) * 0.6f) / 7 * 360;
                            int length = SkillFxArt.Step4(R * (0.5f + 0.3f * Rnd(seed, i, 14)) * grown);
                            Put(o, FxShape.Crack, FxRamp.Dust, length, i & 3, Cos(angle) * (length / 2f + 2), Sin(angle) * (length / 2f + 2), alpha, angle, true);
                        }
                    }
                    if (In(t, A, 0.34f)) Put(o, FxShape.Blob, FxRamp.Dust, 22, (int)(U(t, A, 0.34f) * 5.99f), 0, 2);
                    Ring(o, m, t, A, R, 3, 0.12f, 0.34f);
                    Fly(o, FxShape.Chunk, FxRamp.Dust, 3, 0, 10, t, A, 0.42f, 6, R * 0.9f, seed, hop: 9);
                    Puffs(o, FxRamp.Dust, 10, t, 0.22f, 0.85f, R * 0.82f, R, seed, 6, 0.5f);
                    return 0.95f;
                }
                case "iron_skin":
                {
                    // Plates close on the body and a glint runs across it.
                    Gather(o, FxRamp.Steel, t, A, 20, 6, seed);
                    if (In(t, A, 0.42f))
                    {
                        float u = U(t, A, 0.3f);
                        Put(o, FxShape.Broken, FxRamp.Steel, SkillFxArt.Even(36 - 14 * Out(u)), SkillFxArt.BrokenFrame(2, 4, (int)(u * 3)), 0, Body, t < 0.34f ? 1 : 1 - U(t, 0.34f, 0.42f));
                    }
                    if (In(t, 0.14f, 0.4f))
                    {
                        float u = U(t, 0.14f, 0.4f);
                        Put(o, FxShape.Glint, FxRamp.Steel, 9, u < 0.7f ? 1 : 0, -6 + 12 * u, 2 + 14 * u);
                    }
                    Ring(o, FxRamp.Steel, t, 0.18f, 12, 1, 0.06f, 0.2f, cy: Body);
                    Fly(o, FxShape.Dot, FxRamp.Steel, 1, 2, 5, t, 0.36f, 0.3f, 10, 17, seed, cy: Body);
                    return 0.7f;
                }
                case "blade_storm":
                {
                    // Six blades swing out and round, each dragging its arc, to the true reach.
                    const int blades = 6;
                    Gather(o, m, t, 0.12f, R * 0.5f, 8, seed, cy: 4);
                    if (In(t, 0.04f, 0.12f))
                    {
                        for (int i = 0; i < blades; i++) Put(o, FxShape.Blade, m, 15, 0, Cos(face + i * 60) * 11, 4 + Sin(face + i * 60) * 11, U(t, 0.04f, 0.12f), face + i * 60);
                    }
                    if (In(t, 0.12f, 0.64f))
                    {
                        float u = U(t, 0.12f, 0.64f), orbit = R * (0.35f + 0.55f * Out(u)), turn = face + 630 * Out(u), alpha = u < 0.75f ? 1 : 1 - (u - 0.75f) / 0.25f;
                        for (int i = 0; i < blades; i++)
                        {
                            float angle = turn + i * 60;
                            Put(o, FxShape.Crescent, m, SkillFxArt.Step4(orbit), 52, 0, 0, alpha * 0.8f, angle - 26);
                            Put(o, FxShape.Blade, m, 15, 0, Cos(angle) * (orbit - 2), Sin(angle) * (orbit - 2), alpha, angle + 90);
                        }
                    }
                    Ring(o, m, t, 0.12f, R, 2, 0.14f, 0.3f);
                    if (In(t, 0.58f, 0.95f))
                    {
                        float u = U(t, 0.58f, 0.95f);
                        for (int i = 0; i < blades; i++) Put(o, FxShape.Glint, m, 7, 0, Cos(face + 630 + i * 60) * R * 0.9f, Sin(face + 630 + i * 60) * R * 0.9f, 1 - u);
                    }
                    Puffs(o, FxRamp.Dust, 8, t, 0.5f, 0.95f, R * 0.75f, R * 0.95f, seed, 6, 0.45f);
                    return 0.95f;
                }

                // ---------------- fire mage ----------------
                case "fireball":
                case "flame_burst":
                    return Muzzle(o, FxRamp.Fire, c, t, 9);
                case "meteor":
                    Gather(o, FxRamp.Fire, t, 0.08f, 20, 8, seed);
                    return Muzzle(o, FxRamp.Fire, c, t, 14);
                case "flame_brand":
                {
                    // Flames climb the body in a ring and leave embers.
                    Gather(o, FxRamp.Fire, t, A, 20, 6, seed);
                    if (In(t, A, 0.5f))
                    {
                        float u = U(t, A, 0.5f);
                        for (int i = 0; i < 6; i++)
                        {
                            float angle = i * 60 + 200 * u;
                            Put(o, FxShape.Flame, FxRamp.Fire, u < 0.6f ? 9 : 7, (int)(t * 14) + i, Cos(angle) * 9, 3 + Sin(angle) * 4 + 10 * u, u < 0.6f ? 1 : 1 - (u - 0.6f) / 0.4f);
                        }
                    }
                    Ring(o, FxRamp.Fire, t, A, 12, 1, 0.06f, 0.2f, cy: 2);
                    Float(o, FxShape.Dot, FxRamp.Fire, 1, 2, 7, t, 0.3f, 0.8f, 9, 18, seed, cy: 6);
                    return 0.8f;
                }
                case "fire_ring":
                {
                    // A ring of flames spreads to the true reach; no filled hoop, only tongues and a thin edge.
                    Gather(o, FxRamp.Fire, t, A, R * 0.6f, 8, seed);
                    const int flames = 14;
                    if (In(t, A, 0.62f))
                    {
                        float u = U(t, A, A + 0.16f), late = U(t, 0.4f, 0.62f), at = R * (0.3f + 0.66f * Out(u));
                        for (int i = 0; i < flames; i++)
                        {
                            float angle = (i + 0.5f * Rnd(seed, i, 15)) / flames * 360;
                            int size = late > 0.5f ? 6 : late > 0 ? 8 : (i & 1) == 0 ? 12 : 10;
                            Put(o, FxShape.Flame, FxRamp.Fire, size, (int)(t * 14) + i, Cos(angle) * at, Sin(angle) * at + size / 2f - 2, 1 - late * 0.8f);
                        }
                    }
                    Ring(o, FxRamp.Fire, t, A, R, 2, 0.14f, 0.36f);
                    Float(o, FxShape.Dot, FxRamp.Fire, 1, 2, 10, t, 0.35f, 0.85f, R, 14, seed);
                    Puffs(o, FxRamp.Smoke, 6, t, 0.42f, 0.85f, R * 0.85f, R * 0.95f, seed, 6, 0.4f, rise: 9);
                    return 0.85f;
                }
                case "inferno":
                {
                    // Pillars of flame burst up in two rings, the inner first, and leave the ground burnt.
                    Gather(o, FxRamp.Fire, t, 0.12f, R * 0.6f, 12, seed);
                    if (In(t, 0.08f, 1.25f)) Put(o, FxShape.Scorch, FxRamp.Fire, SkillFxArt.Step4(R * 1.5f), 0, 0, 0, 0.6f * U(t, 0.08f, 0.2f) * (1 - U(t, 0.85f, 1.25f)), 0, true);
                    for (int ring = 0; ring < 2; ring++)
                    {
                        float from = 0.12f + ring * 0.08f, at = R * (ring == 0 ? 0.45f : 0.9f);
                        int count = ring == 0 ? 6 : 12;
                        if (!In(t, from, from + 0.46f)) continue;
                        float up = Out(U(t, from, from + 0.07f)), down = U(t, from + 0.3f, from + 0.46f);
                        int size = 2 * (int)Math.Round((8 + 14 * up) * (1 - down * 0.6f) / 2);
                        for (int i = 0; i < count; i++)
                        {
                            float angle = (i + ring * 0.5f) / count * 360 + seed % 30;
                            Put(o, FxShape.Flame, FxRamp.Fire, size + (i & 1) * 2, (int)(t * 14) + i, Cos(angle) * at, Sin(angle) * at + size / 2f - 2, 1 - down * 0.7f);
                        }
                    }
                    if (In(t, 0.12f, 0.36f)) Put(o, FxShape.Blob, FxRamp.Fire, 26, (int)(U(t, 0.12f, 0.36f) * 5.99f), 0, 3);
                    Ring(o, FxRamp.Fire, t, 0.12f, R, 3, 0.14f, 0.42f);
                    Float(o, FxShape.Dot, FxRamp.Fire, 2, 2, 14, t, 0.4f, 1.05f, R * 0.9f, 20, seed);
                    Puffs(o, FxRamp.Smoke, 8, t, 0.55f, 1.05f, R * 0.5f, R * 0.8f, seed, 8, 0.45f, rise: 12);
                    return 1.25f;
                }

                // ---------------- archer ----------------
                case "volley":
                case "explosive_arrow":
                case "arrow_rain":
                case "power_shot":
                {
                    float hx = (float)Math.Cos(c.Dir) * 9, hy = Body + (float)Math.Sin(c.Dir) * 9;
                    bool strong = skill == "power_shot";
                    // The string let go: air drawn to the bow, a glint, streaks thrown forward (up and forward for the rain).
                    Gather(o, m, t, strong ? 0.1f : 0.05f, strong ? 18 : 10, strong ? 10 : 5, seed, hx, hy);
                    if (In(t, 0.03f, 0.14f)) Put(o, FxShape.Glint, skill == "explosive_arrow" ? FxRamp.Fire : FxRamp.Gold, strong ? 9 : 7, t < 0.09f ? 1 : 0, hx, hy);
                    if (strong) Ring(o, m, t, 0.08f, 10, 1, 0.06f, 0.18f, hx, hy);
                    Fly(o, FxShape.Streak, m, strong ? 10 : 7, 1, skill == "arrow_rain" ? 8 : strong ? 6 : 5, t, 0.04f, 0.2f, 4, strong ? 22 : 16, seed,
                        cx: hx, cy: hy, aimed: true, cone: skill == "arrow_rain" ? 100 : strong ? 40 : 70, toward: face, rise: skill == "arrow_rain" ? 8 : 0);
                    return 0.3f;
                }
                case "eagle_eye":
                {
                    // A reticle closes on the eye and a glint answers.
                    Gather(o, FxRamp.Gold, t, A, 20, 6, seed, cy: 13);
                    if (In(t, A, 0.5f))
                    {
                        float u = U(t, A, 0.36f);
                        Put(o, FxShape.Reticle, FxRamp.Gold, SkillFxArt.Even(44 - 26 * Out(u)), 0, 0, 13, t < 0.4f ? 1 : 1 - U(t, 0.4f, 0.5f), 90 * Out(u));
                    }
                    if (In(t, 0.26f, 0.42f)) Put(o, FxShape.Glint, FxRamp.Gold, 9, t < 0.36f ? 2 : 0, (float)Math.Cos(c.Dir) * 2, 14);
                    Float(o, FxShape.Dot, FxRamp.Gold, 1, 2, 5, t, 0.4f, 0.75f, 9, 12, seed, cy: 10);
                    return 0.75f;
                }

                // ---------------- summoner ----------------
                case "spirits":
                case "spirit_horde":
                case "guardian":
                {
                    int count = skill == "spirits" ? 2 : skill == "spirit_horde" ? 4 : 1;
                    int circle = skill == "spirits" ? 40 : skill == "spirit_horde" ? 52 : 46;
                    Gather(o, FxRamp.Spirit, t, A, circle * 0.5f, 6, seed, cy: 2);
                    // The rune circle opens on the ground, turns, and fades.
                    Ring(o, FxRamp.Spirit, t, 0.04f, circle / 2f, 1, 0.1f, 0.14f, ground: true);
                    if (In(t, 0.14f, 0.85f))
                    {
                        float alpha = 0.9f * (1 - U(t, 0.55f, 0.85f));
                        Put(o, FxShape.Rune, FxRamp.Spirit, circle, (int)(t * 16) % 16, 0, 0, alpha, 0, true);
                        if (skill == "guardian") Put(o, FxShape.Broken, FxRamp.Spirit, circle + 8, SkillFxArt.BrokenFrame(1, 6, (int)(t * 8)), 0, 0, alpha, 0, true);
                    }
                    // Wisps wind up out of it to where the spirits wait (World.WaitOffset).
                    if (In(t, A, 0.55f))
                    {
                        float u = U(t, A, 0.5f), alpha = t < 0.45f ? 1 : 1 - U(t, 0.45f, 0.55f);
                        for (int i = 0; i < count; i++)
                        {
                            float end = 135 + i * 360f / count, angle = end - 300 * (1 - Out(u)), distance = 14 * Out(u);
                            Put(o, FxShape.Wisp, FxRamp.Spirit, skill == "guardian" ? 9 : 5, (int)(t * 12) + i, Cos(angle) * distance, 2 + Sin(angle) * distance + 6 * u, alpha, angle + 90);
                        }
                    }
                    if (skill == "guardian" && In(t, A, 0.5f))
                    {
                        float u = U(t, A, 0.5f);
                        for (int i = 0; i < 6; i++) Put(o, FxShape.Streak, FxRamp.Spirit, 10, 1, Cos(i * 60 + 30) * 16, Sin(i * 60 + 30) * 10 + 26 * u, 1 - u, 90);
                    }
                    Float(o, FxShape.Dot, FxRamp.Spirit, 1, 2, 6 + 2 * count, t, 0.3f, 0.95f, circle * 0.45f, 16, seed);
                    return 0.95f;
                }
                case "spirit_pact":
                {
                    Gather(o, FxRamp.Spirit, t, A, 18, 6, seed);
                    int spirits = c.Points == null ? 0 : c.Points.Length / 2;
                    // A line to each spirit, a wisp running out along it, a ring where it lands.
                    for (int i = 0; i < spirits; i++)
                    {
                        float px = c.Points[i * 2], py = c.Points[i * 2 + 1] + Body - Body;
                        float length = (float)Math.Sqrt(px * px + py * py), angle = (float)Math.Atan2(py, px) * Deg;
                        if (length < 6) continue;
                        if (In(t, A, 0.6f)) Put(o, FxShape.Tether, FxRamp.Spirit, SkillFxArt.Step4(length), (int)(t * 24) & 3, px / 2, Body + py / 2, 0.9f * (1 - U(t, 0.45f, 0.6f)), angle);
                        if (In(t, A, 0.34f))
                        {
                            float u = Out(U(t, A, 0.34f));
                            Put(o, FxShape.Wisp, FxRamp.Spirit, 5, (int)(t * 12), px * u, Body + py * u, 1, angle);
                        }
                        Ring(o, FxRamp.Spirit, t, 0.3f, 8, 1, 0.06f, 0.2f, px, py + Body);
                    }
                    if (spirits == 0)
                    {
                        if (In(t, A, 0.6f)) Put(o, FxShape.Rune, FxRamp.Spirit, 28, (int)(t * 16) % 16, 0, 0, 0.9f * (1 - U(t, 0.4f, 0.6f)), 0, true);
                    }
                    Ring(o, FxRamp.Spirit, t, A, 11, 1, 0.06f, 0.2f, cy: Body);
                    Float(o, FxShape.Dot, FxRamp.Spirit, 1, 2, 6, t, 0.35f, 0.8f, 10, 14, seed, cy: 4);
                    return 0.8f;
                }
                case "life_spring":
                {
                    // A fountain: motes and plus signs thrown up from the feet, spreading as they slow, a ring on the ground.
                    Gather(o, FxRamp.Heal, t, A, 20, 6, seed, cy: 2);
                    Ring(o, FxRamp.Heal, t, A, 13, 2, 0.1f, 0.3f, ground: true);
                    if (In(t, A, 0.62f))
                    {
                        for (int i = 0; i < 14; i++)
                        {
                            float from = A + 0.2f * Rnd(seed, i, 16), u = (t - from) / 0.36f;
                            if (u < 0 || u >= 1) continue;
                            float aside = (Rnd(seed, i, 17) - 0.5f) * 22;
                            Put(o, FxShape.Dot, FxRamp.Heal, 1 + (i & 1), 2 + (i % 3 == 0 ? 1 : 0), aside * Out(u), 2 + (22 + 8 * Rnd(seed, i, 18)) * Out(u) - 6 * u * u, 1 - u * u);
                        }
                    }
                    Float(o, FxShape.Plus, FxRamp.Heal, 5, 0, 3, t, 0.2f, 0.9f, 9, 20, seed, cy: 8);
                    Float(o, FxShape.Plus, FxRamp.Heal, 3, 0, 4, t, 0.3f, 0.9f, 12, 18, seed + 1, cy: 6);
                    return 0.9f;
                }
                case "spirit_blast":
                {
                    // Wisps wind in to the caster, then burst out to the true reach.
                    const float lead = 0.14f;
                    if (In(t, 0, lead))
                    {
                        float u = t / lead;
                        for (int i = 0; i < 6; i++)
                        {
                            float angle = i * 60 + 160 * u, distance = R * 0.85f * (1 - u * u) + 3;
                            Put(o, FxShape.Wisp, FxRamp.Spirit, 5, (int)(t * 12) + i, Cos(angle) * distance, 4 + Sin(angle) * distance, 0.5f + 0.5f * u, angle + 180 + 40);
                        }
                    }
                    if (In(t, lead, 0.38f)) Put(o, FxShape.Blob, FxRamp.Spirit, 22, (int)(U(t, lead, 0.38f) * 5.99f), 0, 4);
                    Ring(o, FxRamp.Spirit, t, lead, R, 3, 0.13f, 0.36f);
                    Fly(o, FxShape.Wisp, FxRamp.Spirit, 5, 0, 8, t, lead, 0.3f, 6, R * 1.05f, seed, aimed: true, cy: 2);
                    Float(o, FxShape.Dot, FxRamp.Spirit, 1, 2, 12, t, 0.34f, 0.85f, R * 0.9f, 12, seed);
                    return 0.85f;
                }

                // ---------------- storm caller ----------------
                case "chain_lightning":
                {
                    float hx = (float)Math.Cos(c.Dir) * 9, hy = Body + (float)Math.Sin(c.Dir) * 9;
                    if (In(t, 0, 0.12f) && (int)(t * 40) % 3 != 2)
                    {
                        for (int i = 0; i < 3; i++) Zap(o, t, hx, hy, hx + Cos(face + (i - 1) * 55) * 10, hy + Sin(face + (i - 1) * 55) * 10, seed + i);
                    }
                    if (In(t, 0, 0.1f)) Put(o, FxShape.Glint, FxRamp.Bolt, 7, 1, hx, hy);
                    Fly(o, FxShape.Dot, FxRamp.Bolt, 1, 2, 4, t, 0.04f, 0.2f, 2, 10, seed, cx: hx, cy: hy);
                    return 0.26f;
                }
                case "ice_lance":
                    return Muzzle(o, FxRamp.Cold, c, t, 8);
                case "frost_ring":
                {
                    // Spikes of ice erupt round the ring at the true reach, stand a moment and shatter.
                    Gather(o, FxRamp.Cold, t, A, R * 0.6f, 8, seed);
                    const int spikes = 12;
                    for (int i = 0; i < spikes; i++)
                    {
                        float angle = (i + 0.4f * Rnd(seed, i, 19)) / spikes * 360, from = A + 0.03f * Rnd(seed, i, 20);
                        float bx = Cos(angle) * (R - 9), by = Sin(angle) * (R - 9);
                        if (In(t, from, 0.36f))
                        {
                            int length = 2 * (int)Math.Round((4 + 9 * Out(U(t, from, from + 0.06f))) / 2) + (i & 1) * 2;
                            Put(o, FxShape.Shard, FxRamp.Cold, length, 0, bx + Cos(angle) * length / 2, by + Sin(angle) * length / 2, 1, angle);
                        }
                        else if (In(t, 0.36f, 0.75f))
                        {
                            float u = U(t, 0.36f, 0.75f);
                            for (int piece = 0; piece < 2; piece++)
                            {
                                float away = angle + (piece == 0 ? -25 : 25), distance = 6 + 9 * Out(u) * (piece + 1) / 2f;
                                Put(o, FxShape.Shard, FxRamp.Cold, 4, 0, bx + Cos(away) * distance, by + Sin(away) * distance - 14 * u * u, 1 - u * u, away + 200 * u);
                            }
                        }
                    }
                    Ring(o, FxRamp.Cold, t, A, R, 2, 0.1f, 0.34f);
                    Puffs(o, FxRamp.Mist, 6, t, 0.34f, 0.8f, R * 0.8f, R * 0.95f, seed, 7, 0.32f, rise: 3);
                    return 0.8f;
                }
                case "storm_mantle":
                {
                    Gather(o, FxRamp.Bolt, t, A, 20, 6, seed);
                    // Arcs crackle round the body, new ones every moment, with a rim of cold.
                    if (In(t, A, 0.44f) && (int)(t * 40) % 4 != 3)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            float angle = i * 72 + (int)(t * 25) * 31;
                            Zap(o, t, Cos(angle) * 4, Body + Sin(angle) * 4, Cos(angle + 20) * 17, Body + Sin(angle + 20) * 15, seed + i);
                        }
                    }
                    Ring(o, FxRamp.Cold, t, A, 14, 2, 0.08f, 0.28f, cy: Body);
                    if (In(t, 0.2f, 0.8f))
                    {
                        float u = U(t, 0.2f, 0.8f);
                        for (int i = 0; i < 6; i++) Put(o, FxShape.Dot, FxRamp.Cold, 1, 3, Cos(i * 60 + 300 * u) * (11 + 4 * u), Body + Sin(i * 60 + 300 * u) * (7 + 2 * u), 1 - u);
                    }
                    return 0.8f;
                }
                case "thunderclap":
                {
                    // A leader from the sky, then the bolt itself and forks along the ground to the true reach.
                    Gather(o, FxRamp.Bolt, t, A, R * 0.5f, 6, seed, cy: 2);
                    if (In(t, 0.02f, A)) Put(o, FxShape.Streak, FxRamp.Bolt, 40, 1, 0, 60, 0.5f, -90);
                    float flick = Flicker(t - A);
                    if (t >= A && flick > 0)
                    {
                        Zap(o, t, (Rnd(seed, 0, 21) - 0.5f) * 30, 112, 0, 2, seed, true, flick);
                        for (int i = 0; i < 6; i++)
                        {
                            float angle = (i + 0.5f * Rnd(seed, i, 22)) / 6 * 360;
                            Zap(o, t, Cos(angle) * 5, 2 + Sin(angle) * 5, Cos(angle) * (R - 1), Sin(angle) * (R - 1), seed + 3 + i, false, flick);
                        }
                    }
                    if (In(t, A, 0.3f)) Put(o, FxShape.Blob, FxRamp.Bolt, 18, (int)(U(t, A, 0.3f) * 5.99f), 0, 3);
                    Ring(o, FxRamp.Bolt, t, A, R, 3, 0.1f, 0.3f);
                    // What is left: sparks hopping on the ground, a small burn.
                    if (In(t, 0.28f, 0.62f) && (int)(t * 30) % 3 != 2)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            int now = (int)(t * 12) * 13 + i;
                            float angle = Rnd(seed, now, 23) * 360, distance = R * (0.3f + 0.6f * Rnd(seed, now, 24));
                            Put(o, FxShape.Bolt, FxRamp.Bolt, 8, now % 6, Cos(angle) * distance, Sin(angle) * distance, 1 - U(t, 0.28f, 0.62f), Rnd(seed, now, 25) * 360);
                        }
                    }
                    if (In(t, A, 0.75f)) Put(o, FxShape.Scorch, FxRamp.Bolt, 16, 1, 0, 0, 0.55f * (1 - U(t, 0.45f, 0.75f)), 0, true);
                    return 0.75f;
                }
                case "blizzard":
                {
                    // Snow and shards wheel over the whole reach, slowing; spikes stand at its rim and fall.
                    Gather(o, FxRamp.Cold, t, 0.12f, R * 0.6f, 10, seed);
                    Ring(o, FxRamp.Cold, t, 0.12f, R, 3, 0.14f, 0.5f);
                    if (In(t, 0.1f, 1.05f))
                    {
                        float u = U(t, 0.1f, 1.05f), swirl = 420 * Out(u), alpha = u < 0.7f ? 1 : 1 - (u - 0.7f) / 0.3f;
                        for (int i = 0; i < 36; i++)
                        {
                            float distance = R * (0.2f + 0.78f * (float)Math.Sqrt(Rnd(seed, i, 26))) * (0.5f + 0.5f * Out(U(t, 0.1f, 0.3f)));
                            float angle = Rnd(seed, i, 27) * 360 + swirl * (1.3f - distance / R * 0.6f);
                            float fall = u > 0.7f ? -10 * (u - 0.7f) : 0;
                            if (i % 3 == 0) Put(o, FxShape.Shard, FxRamp.Cold, 8 + (i & 2), 0, Cos(angle) * distance, Sin(angle) * distance + fall, alpha, angle + 90);
                            else Put(o, FxShape.Dot, FxRamp.Cold, 2, 3 - (i & 1), Cos(angle) * distance, Sin(angle) * distance + fall, alpha);
                        }
                    }
                    for (int i = 0; i < 9; i++)
                    {
                        float angle = (i + 0.3f) / 9 * 360, from = 0.16f + 0.04f * Rnd(seed, i, 28);
                        float bx = Cos(angle) * (R - 12), by = Sin(angle) * (R - 12);
                        if (!In(t, from, 0.8f)) continue;
                        int length = 2 * (int)Math.Round((4 + 12 * Out(U(t, from, from + 0.07f))) / 2);
                        Put(o, FxShape.Shard, FxRamp.Cold, length, 0, bx + Cos(angle) * length / 2, by + Sin(angle) * length / 2, 1 - U(t, 0.6f, 0.8f), angle);
                    }
                    Puffs(o, FxRamp.Mist, 8, t, 0.6f, 1.05f, R * 0.5f, R * 0.85f, seed, 8, 0.3f, rise: 3);
                    return 1.05f;
                }
            }
            return 0;
        }

        // ---- a projectile in flight (round where it is, t its age) ----

        static void Shot(string skill, in FxContext c, float t, List<FxStamp> o)
        {
            float dx = (float)Math.Cos(c.Dir), dy = (float)Math.Sin(c.Dir), face = c.Dir * Deg;
            int seed = c.Seed;
            var m = c.Main;
            switch (skill)
            {
                case "fireball":
                case "flame_burst":
                {
                    int head = skill == "fireball" ? 9 : 7;
                    Put(o, FxShape.Fireball, FxRamp.Fire, head, (int)(t * 16) % 3, -dx * head / 2, -dy * head / 2, 1, face);
                    Trail(o, FxShape.Dot, FxRamp.Fire, t, c, skill == "fireball" ? 4 : 2, head, 18, 5, 5, true);
                    break;
                }
                case "meteor":
                {
                    // High over its shadow, dragging smoke.
                    Put(o, FxShape.Shadow, FxRamp.Smoke, 14, 0, 0, -3, 1, 0, true);
                    for (int k = 0; k < 4; k++)
                    {
                        float phase = Frac(t * 4 + k * 0.25f);
                        Put(o, FxShape.Smoke, FxRamp.Smoke, 6 + (k & 1) * 2, phase < 0.4f ? 1 : phase < 0.7f ? 2 : 3, -dx * (12 + phase * 24), 7 - dy * (12 + phase * 24) + phase * 7, 0.6f * (1 - phase));
                    }
                    Put(o, FxShape.Fireball, FxRamp.Fire, 15, (int)(t * 16) % 3, -dx * 7, 7 - dy * 7, 1, face);
                    Trail(o, FxShape.Dot, FxRamp.Fire, t, c, 4, 12, 22, 9, 7, true, 7);
                    break;
                }
                case "volley":
                    Put(o, FxShape.Streak, m, 20, 1, -dx * 15, -dy * 15, 0.85f, face);
                    Put(o, FxShape.Arrow, m, 11, 0, 0, 0, 1, face);
                    break;
                case "power_shot":
                    // A long bright streak and air winding round it.
                    Put(o, FxShape.Streak, m, 28, 3, -dx * 18, -dy * 18, 0.9f, face);
                    for (int k = 1; k <= 5; k++)
                    {
                        float back = 5 + k * 5, aside = (float)Math.Sin(k * 1.4 - t * 40) * 4;
                        Put(o, FxShape.Dot, m, 1, k < 3 ? 3 : 2, -dx * back - dy * aside, -dy * back + dx * aside, 1 - k / 6f);
                    }
                    Put(o, FxShape.Arrow, FxRamp.Steel, 13, 0, 0, 0, 1, face);
                    break;
                case "explosive_arrow":
                {
                    Trail(o, FxShape.Smoke, FxRamp.Smoke, t, c, 2, 8, 14, 3, 3, false);
                    Put(o, FxShape.Arrow, FxRamp.Steel, 11, 0, 0, 0, 1, face);
                    // Its charge blinks.
                    bool lit = ((int)(t * 12) & 1) == 0;
                    Put(o, FxShape.Dot, FxRamp.Fire, lit ? 3 : 2, lit ? 3 : 1, dx * 5, dy * 5);
                    break;
                }
                case "arrow_rain":
                {
                    // Lobbed: over its shadow, coming down as it reaches the end of its range.
                    float high = 14 * Math.Max(0, 1 - t / 0.56f);
                    Put(o, FxShape.Shadow, FxRamp.Smoke, 6, 0, 0, -1, 1, 0, true);
                    Put(o, FxShape.Streak, m, 10, 1, -dx * 10, high - dy * 10, 0.5f, face);
                    Put(o, FxShape.Arrow, m, 11, 0, 0, high, 1, face);
                    break;
                }
                case "chain_lightning":
                    Put(o, FxShape.Bolt, FxRamp.Bolt, 16, (int)(t * 25) % 6, -dx * 7, -dy * 7, 1, face);
                    Put(o, FxShape.Glint, FxRamp.Bolt, 7, (int)(t * 20) & 1, 0, 0);
                    break;
                case "ice_lance":
                    Put(o, FxShape.Streak, FxRamp.Cold, 12, 1, -dx * 22, -dy * 22, 0.5f, face);
                    Trail(o, FxShape.Dot, FxRamp.Cold, t, c, 4, 14, 16, 5, 0, true);
                    Put(o, FxShape.Shard, FxRamp.Cold, 20, 1, -dx * 8, -dy * 8, 1, face);
                    break;
            }
        }

        // Bits shed behind a projectile, each drifting back and aside as it fades and then starting over.
        static void Trail(List<FxStamp> o, FxShape shape, FxRamp ramp, float t, in FxContext c, int count, float near, float length, float wide, float rise, bool dots, float up = 0)
        {
            float dx = (float)Math.Cos(c.Dir), dy = (float)Math.Sin(c.Dir);
            for (int k = 0; k < count; k++)
            {
                float turn = t * 5 + k / (float)count, phase = Frac(turn);
                float aside = (Rnd(c.Seed, (int)Math.Floor(turn) * 7 + k, 29) - 0.5f) * wide, back = near + phase * length;
                if (dots) Put(o, shape, ramp, phase < 0.5f ? 2 : 1, phase < 0.3f ? 3 : phase < 0.6f ? 2 : 1, -dx * back - dy * aside, up - dy * back + dx * aside + rise * phase, 1 - phase);
                else Put(o, shape, ramp, 4, phase < 0.5f ? 1 : 2, -dx * back - dy * aside, up - dy * back + dx * aside + rise * phase, 0.5f * (1 - phase));
            }
        }

        // ---- where a projectile lands ----

        static float Hit(string skill, in FxContext c, float t, List<FxStamp> o)
        {
            float R = c.Radius, face = c.Dir * Deg;
            int seed = c.Seed;
            var m = c.Main;
            switch (skill)
            {
                case "fireball":
                case "flame_burst":
                    return Explosion(o, t, R, 0, seed);
                case "explosive_arrow":
                    return Explosion(o, t, R, 1, seed);
                case "meteor":
                    return Explosion(o, t, R, 2, seed);
                case "volley":
                    Puffs(o, FxRamp.Dust, 1, t, 0, 0.3f, 0, 2, seed, 5, 0.5f);
                    Fly(o, FxShape.Dot, m, 1, 2, 3, t, 0, 0.2f, 1, 7, seed);
                    return 0.3f;
                case "power_shot":
                    if (In(t, 0, 0.07f)) Put(o, FxShape.Glint, FxRamp.Gold, 9, 2, 0, 0);
                    Ring(o, FxRamp.Steel, t, 0, 7, 1, 0.05f, 0.15f);
                    Fly(o, FxShape.Streak, FxRamp.Gold, 6, 1, 6, t, 0, 0.2f, 2, 15, seed, aimed: true, cone: 110, toward: face);
                    return 0.26f;
                case "arrow_rain":
                    // The arrow stands in the ground a moment in a puff of dust.
                    if (In(t, 0, 0.5f)) Put(o, FxShape.Arrow, m, 11, 0, 1, 3, t < 0.3f ? 1 : 1 - U(t, 0.3f, 0.5f), -65);
                    Ring(o, FxRamp.Dust, t, 0, 5, 1, 0.05f, 0.18f, ground: true);
                    Puffs(o, FxRamp.Dust, 2, t, 0, 0.35f, 1, 5, seed, 4, 0.5f, rise: 3);
                    return 0.5f;
                case "chain_lightning":
                    if (In(t, 0, 0.14f) && (int)(t * 40) % 3 != 2)
                    {
                        for (int i = 0; i < 5; i++) Put(o, FxShape.Bolt, FxRamp.Bolt, 8, (seed + i + (int)(t * 25)) % 6, Cos(i * 72 + seed) * 5, Sin(i * 72 + seed) * 5, 1, i * 72 + seed);
                    }
                    if (In(t, 0, 0.08f)) Put(o, FxShape.Glint, FxRamp.Bolt, 9, 2, 0, 0);
                    Fly(o, FxShape.Dot, FxRamp.Bolt, 1, 2, 4, t, 0.04f, 0.2f, 3, 11, seed);
                    return 0.26f;
                case "ice_lance":
                    // It shatters: shards fly on past and fall, a breath of mist.
                    if (In(t, 0, 0.07f)) Put(o, FxShape.Glint, FxRamp.Cold, 9, 2, 0, 0);
                    if (In(t, 0, 0.42f))
                    {
                        float u = t / 0.42f;
                        for (int i = 0; i < 7; i++)
                        {
                            float angle = face + (i - 3) * 38 + 20 * Rnd(seed, i, 30), distance = 3 + (10 + 6 * Rnd(seed, i, 31)) * Out(u);
                            Put(o, FxShape.Shard, FxRamp.Cold, 4 + (i & 1) * 2, 0, Cos(angle) * distance, Sin(angle) * distance - 12 * u * u, 1 - u * u, angle + 260 * u);
                        }
                    }
                    Puffs(o, FxRamp.Mist, 2, t, 0.04f, 0.5f, 1, 6, seed, 7, 0.32f, rise: 3);
                    return 0.5f;
            }
            return 0;
        }

        // A bright core, a ball swelling to the blast's reach, bits thrown out, then smoke rising and thinning
        // (Docs/SkillEffects.md, source 5). Bigger blasts throw rubble, leave flames and, the largest, a burn.
        static float Explosion(List<FxStamp> o, float t, float R, int level, int seed)
        {
            R = Math.Max(10, R);
            if (level == 2 && In(t, 0.05f, 1.3f)) Put(o, FxShape.Scorch, FxRamp.Fire, SkillFxArt.Step4(R * 1.4f), 2, 0, 0, 0.6f * (1 - U(t, 0.8f, 1.3f)), 0, true);
            if (In(t, 0, 0.08f)) Put(o, FxShape.Blob, FxRamp.Fire, SkillFxArt.Even(R * 1.1f), t < 0.04f ? 0 : 1, 0, 0);
            else if (In(t, 0.08f, 0.36f))
            {
                float u = U(t, 0.08f, 0.36f);
                Put(o, FxShape.Blob, FxRamp.Fire, SkillFxArt.Even(R * (1.3f + 0.4f * Out(u))), 2 + Math.Min(3, (int)(u * 4)), 0, 0);
            }
            Ring(o, FxRamp.Fire, t, 0, R, level == 2 ? 3 : 2, 0.1f, 0.3f);
            Fly(o, FxShape.Dot, FxRamp.Fire, 2, 2, 6 + 4 * level, t, 0.03f, 0.42f, 4, R * 1.1f, seed, rise: 6);
            if (level > 0)
            {
                Fly(o, FxShape.Chunk, FxRamp.Smoke, 3, 0, 5 * level, t, 0.03f, 0.4f, 4, R, seed + 1, hop: 8);
                if (In(t, 0.12f, 0.52f))
                {
                    float u = U(t, 0.12f, 0.52f);
                    for (int i = 0; i < 3 * level; i++)
                    {
                        float angle = Rnd(seed, i, 32) * 360, distance = R * 0.65f * (float)Math.Sqrt(Rnd(seed, i, 33));
                        Put(o, FxShape.Flame, FxRamp.Fire, u < 0.6f ? 8 : 6, (int)(t * 14) + i, Cos(angle) * distance, Sin(angle) * distance + 2, 1 - u);
                    }
                }
            }
            Puffs(o, FxRamp.Smoke, 3 + 2 * level, t, 0.2f, 0.75f + 0.15f * level, R * 0.15f, R * 0.6f, seed, 6 + 2 * level, 0.5f, rise: 10);
            return level == 2 ? 1.3f : 0.8f + 0.1f * level;
        }

        // Lightning between two enemies: a jagged line, a new one each flicker, a glint and sparks where it lands.
        static float Chain(in FxContext c, float t, List<FxStamp> o)
        {
            float dx = (float)Math.Cos(c.Dir) * c.Length / 2, dy = (float)Math.Sin(c.Dir) * c.Length / 2;
            float flick = Flicker(t);
            if (flick > 0) Zap(o, t, -dx, -dy, dx, dy, c.Seed, c.Length >= 24, flick);
            if (In(t, 0, 0.12f)) Put(o, FxShape.Glint, FxRamp.Bolt, 7, 1, dx, dy);
            Fly(o, FxShape.Dot, FxRamp.Bolt, 1, 2, 3, t, 0.05f, 0.22f, 2, 9, c.Seed, cx: dx, cy: dy);
            return 0.3f;
        }

        // ---- a buff that is running: small, slow, near the body, never over what is aimed at ----

        static void Loop(string skill, in FxContext c, float t, List<FxStamp> o)
        {
            float dx = (float)Math.Cos(c.Dir), dy = (float)Math.Sin(c.Dir);
            switch (skill)
            {
                case "war_cry":
                {
                    float p = Frac(t / 0.9f);
                    Put(o, FxShape.Broken, FxRamp.Gold, SkillFxArt.Even(18 + 10 * Out(p)), SkillFxArt.BrokenFrame(1, 4, 1), 0, 12, 0.55f * (1 - p));
                    for (int k = 0; k < 2; k++)
                    {
                        float q = Frac(t / 0.9f + k * 0.5f);
                        Put(o, FxShape.Dot, FxRamp.Gold, 1, 2, (k == 0 ? -7 : 7) + (float)Math.Sin(q * 6) * 1.5f, 4 + 14 * q, 0.8f * (1 - q));
                    }
                    break;
                }
                case "iron_skin":
                {
                    Put(o, FxShape.Broken, FxRamp.Steel, 22, SkillFxArt.BrokenFrame(1, 4, (int)(t * 4)), 0, Body, 0.5f);
                    float p = Frac(t / 1.2f);
                    if (p < 0.14f) Put(o, FxShape.Glint, FxRamp.Steel, 7, p < 0.08f ? 1 : 0, 5, 13);
                    break;
                }
                case "flame_brand":
                {
                    // At the weapon hand, the way the player faces.
                    float hx = dx * 7, hy = 6 + dy * 5;
                    Put(o, FxShape.Flame, FxRamp.Fire, 6, (int)(t * 12), hx, hy + 3, 0.85f);
                    Put(o, FxShape.Flame, FxRamp.Fire, 5, (int)(t * 12) + 2, hx - dy * 3, hy + 2 + dx * 2, 0.7f);
                    float p = Frac(t / 0.6f);
                    Put(o, FxShape.Dot, FxRamp.Fire, 1, 2, hx + (float)Math.Sin(p * 7) * 2, hy + 5 + 10 * p, 0.9f * (1 - p));
                    break;
                }
                case "eagle_eye":
                {
                    Put(o, FxShape.Reticle, FxRamp.Gold, 16, 0, 0, 13, 0.42f + 0.14f * (float)Math.Sin(t * 2 * Math.PI), 22.5f * (int)(t * 2));
                    float p = Frac(t);
                    if (p < 0.12f) Put(o, FxShape.Glint, FxRamp.Gold, 5, 1, dx * 2, 14);
                    break;
                }
                case "spirit_pact":
                {
                    int spirits = c.Points == null ? 0 : c.Points.Length / 2;
                    for (int i = 0; i < spirits; i++)
                    {
                        float px = c.Points[i * 2], py = c.Points[i * 2 + 1];
                        float length = (float)Math.Sqrt(px * px + py * py);
                        if (length < 8) continue;
                        Put(o, FxShape.Tether, FxRamp.Spirit, SkillFxArt.Step4(length), (int)(t * 12) & 3, px / 2, Body + py / 2, 0.4f + 0.12f * (float)Math.Sin(t * 7), (float)Math.Atan2(py, px) * Deg);
                    }
                    float p = Frac(t / 0.8f);
                    Put(o, FxShape.Dot, FxRamp.Spirit, 1, 2, (float)Math.Cos(t * 4) * 8, 5 + 12 * p, 0.8f * (1 - p));
                    break;
                }
                case "storm_mantle":
                {
                    // A short arc round the body now and then, two flakes circling.
                    float cycle = t / 0.45f, p = Frac(cycle);
                    int index = (int)Math.Floor(cycle);
                    if (p < 0.27f && (int)(p * 30) % 3 != 2)
                    {
                        float angle = Rnd(c.Seed, index, 34) * 360;
                        Put(o, FxShape.Bolt, FxRamp.Bolt, 10, (index + (int)(p * 25)) % 6, Cos(angle) * 9, Body + Sin(angle) * 8, 0.9f, angle + 90);
                    }
                    for (int k = 0; k < 2; k++) Put(o, FxShape.Dot, FxRamp.Cold, 1, 3, Cos(t * 200 + k * 180) * 10, Body + Sin(t * 200 + k * 180) * 5, 0.7f);
                    break;
                }
            }
        }
    }
}
