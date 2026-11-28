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
        // In the air but under the warnings on the floor (smoke and dust of the monsters' effects, E5): over the
        // bodies, never over a mark.
        public bool Low;
        // 0, or a colour (0x1RRGGBB) the view multiplies the piece by: a monster's own, over the Plain ramp (MonsterFx).
        public uint Tint;

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
        // A projectile's whole flight when nothing stops it, in seconds (its range over its speed; Length its range
        // in pixels): what is drawn coming down from above (the meteor) lands as the flight ends. 0 when not known.
        public float Span;
        // Where a projectile will end as things stand, in pixels from where it is, and the radius of what it does
        // there (World.Landing, 0.113.0); Mark 0 when not known. The meteor shows it as a pale amber ring of dashes: the
        // player's own mark, never the red of an enemy's.
        public float MarkX, MarkY, Mark;
    }

    // Every skill's effect as a function of time (Docs/SkillEffects.md): for a moment after the skill is used it says
    // which pieces are where. Each has three parts, a short gathering (anticipation), the blow itself (the most pieces
    // and the brightest, the shortest part) and what is left to thin out (smoke, embers, mist, motes). Nothing here
    // keeps state, so the game (WorldView.SkillFx) and the preview tool (DevTools/ArtPreview) draw the very same thing.
    static partial class SkillFx
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
            Roll, RollSlide, RollBlink,
        };

        // The dodge roll's three looks (HeroMove.Roll), which are no skill's: a tumble's dust, a slide's scuffed
        // floor, a gliding step's afterimages. Each starts where the roll begins and stays there, with Dir the way
        // of the roll and Length how far it goes (a wall cuts it short), so the pieces lie along the way the body goes.
        public const string Roll = "roll", RollSlide = "roll_slide", RollBlink = "roll_blink";
        // How long the roll takes to go its Length, in seconds (World.RollSeconds).
        const float RollTime = 0.3f;

        // A skill without an effect of its own (a new one in the sheet) keeps the old look of its kind.
        public static bool Knows(string skill) => skill != null && (Known.Contains(skill) || IsArt(skill));

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

        internal static void Put(List<FxStamp> o, FxShape shape, FxRamp ramp, int size, int frame, float x, float y, float alpha = 1, float angle = 0, bool ground = false)
        {
            if (alpha <= 0.03f || size <= 0) return;
            o.Add(new FxStamp { Shape = shape, Ramp = ramp, Size = size, Frame = frame, X = x, Y = y, Alpha = Math.Min(1, alpha), Angle = angle, Ground = ground });
        }

        // How strongly a landing mark shows by how far along the flight is: pale, never as strong as an enemy's warning.
        internal static float MarkAlpha(float p) => 0.35f + 0.25f * Math.Clamp(p, 0, 1);

        internal static float U(float t, float from, float to) => Math.Clamp((t - from) / (to - from), 0, 1);

        internal static bool In(float t, float from, float to) => t >= from && t < to;

        // Fast, then slow: sizes grow on this so the last one is the one that reads (rule 2).
        internal static float Out(float u) => 1 - (1 - u) * (1 - u) * (1 - u);

        internal static float Rnd(int seed, int i, int salt) => PixelNoise.Value(seed * 31 + i, i * 17 + salt, salt);

        internal static float Cos(float degrees) => (float)Math.Cos(degrees / Deg);

        internal static float Sin(float degrees) => (float)Math.Sin(degrees / Deg);

        internal static float Frac(float value) => value - (float)Math.Floor(value);

        // Lines drawn in toward a point: radial lines pull the eye to the middle (rule 3), the anticipation.
        internal static void Gather(List<FxStamp> o, FxRamp ramp, float t, float until, float radius, int count, int seed, float cx = 0, float cy = Body)
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
        internal static void Ring(List<FxStamp> o, FxRamp ramp, float t, float from, float radius, int thick = 2, float grow = 0.12f, float life = 0.32f, float cx = 0, float cy = 0, bool ground = false)
        {
            if (!In(t, from, from + life)) return;
            float k = (t - from) / life;
            float diameter = 2 * radius * (0.3f + 0.7f * Out(U(t, from, from + grow)));
            int now = k < 0.4f ? thick : k < 0.7f ? Math.Max(1, thick - 1) : 1;
            Put(o, FxShape.Edge, ramp, SkillFxArt.Even(diameter), now, cx, cy, k < 0.5f ? 1 : 1 - (k - 0.5f) / 0.5f, 0, ground);
        }

        // Bits thrown out from a point, slowing as they go; with a hop they arc up and come down, with a rise they
        // drift up. Turned the way they fly when aimed.
        internal static void Fly(List<FxStamp> o, FxShape shape, FxRamp ramp, int size, int frame, int count, float t, float from, float life, float near, float far, int seed,
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
        internal static void Float(List<FxStamp> o, FxShape shape, FxRamp ramp, int size, int frame, int count, float t, float from, float to, float radius, float rise, int seed,
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
        static void Zap(List<FxStamp> o, float t, float x0, float y0, float x1, float y1, int seed, bool thick = false, float alpha = 1, FxRamp ramp = FxRamp.Bolt, bool halo = false)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 4) return;
            int kind = (int)((uint)(seed * 7 + (int)(t * 25)) % 6);
            Put(o, FxShape.Bolt, ramp, SkillFxArt.Step4(length), kind + (thick ? 8 : 0) + (halo ? 16 : 0), (x0 + x1) / 2, (y0 + y1) / 2, alpha, (float)Math.Atan2(dy, dx) * Deg);
        }

        // Lightning's flicker: on, a blink off, on again, off, and a last dim one.
        static float Flicker(float t) => t < 0.05f ? 1 : t < 0.07f ? 0 : t < 0.12f ? 1 : t < 0.14f ? 0 : t < 0.18f ? 0.6f : 0;

        // A flash at the hand the skill leaves from, and sparks off it.
        static float Muzzle(List<FxStamp> o, FxRamp ramp, in FxContext c, float t, int size)
        {
            float hx = (float)Math.Cos(c.Dir) * 9, hy = Body + (float)Math.Sin(c.Dir) * 9;
            Gather(o, ramp, t, 0.06f, 12, 5, c.Seed, hx, hy);
            if (In(t, 0.03f, 0.13f))
            {
                Put(o, FxShape.Blob, ramp, size, t < 0.07f ? 0 : t < 0.1f ? 1 : 3, hx, hy);
                FxLayers.Light(o, ramp, size * 2.4f, 0.6f, hx, hy);
            }
            Fly(o, FxShape.Dot, ramp, 1, 2, 5, t, 0.05f, 0.22f, 2, 11, c.Seed, cx: hx, cy: hy, cone: 120, toward: c.Dir * Deg);
            return 0.3f;
        }

        // A ball of fire in flight (the rework's comet): a tail of lumps that shrink and darken, sparks shed behind,
        // and a head of three lumps, the palest foremost. dx, dy is the way it goes on the screen; size the head's
        // radius. A rock is dark with a glowing heart and drags smoke. Thirteen pieces.
        static void Comet(List<FxStamp> o, FxRamp ramp, float t, int seed, float dx, float dy, float size, float x = 0, float y = 0, bool rock = false)
        {
            float face = (float)Math.Atan2(dy, dx) * Deg;
            for (int i = 5; i >= 1; i--)
            {
                float back = i * size * 0.8f, wave = (float)Math.Sin(t * 30 + i * 1.3f) * size * 0.12f * i;
                bool smoke = rock && i > 3;
                Put(o, FxShape.Lump, smoke ? FxRamp.Smoke : ramp, Math.Max(2, (int)Math.Round(2 * size * (1 - i * 0.15f))), (smoke ? 2 : i > 3 ? 4 : i > 1 ? 3 : 2) * 4 + (((int)(t * 25) + i) & 3),
                    x - dx * back - dy * wave, y - dy * back + dx * wave, i > 3 ? 0.65f : 1);
            }
            for (int i = 0; i < 5; i++)
            {
                float phase = Frac(t * 3.2f + i / 5f);
                float back = size * 1.6f + phase * size * 4.5f, side = (Rnd(seed, i, 35) - 0.5f) * size * 2.2f;
                Put(o, FxShape.Ember, ramp, phase < 0.4f ? 3 : 2, phase < 0.5f ? 0 : 1, x - dx * back - dy * side, y - dy * back + dx * side + phase * 3, 1 - phase, face);
            }
            int wave3 = (int)(t * 25) & 3;
            Put(o, FxShape.Lump, rock ? FxRamp.Dust : ramp, (int)Math.Round(2 * size), (rock ? 4 : 2) * 4 + wave3, x, y);
            Put(o, FxShape.Lump, ramp, (int)Math.Round(1.4f * size), (rock ? 3 : 1) * 4 + ((wave3 + 1) & 3), x + dx * size * 0.2f, y + dy * size * 0.2f);
            Put(o, FxShape.Lump, ramp, Math.Max(2, (int)Math.Round(0.84f * size)), (rock ? 1 : 0) * 4 + ((wave3 + 2) & 3), x + dx * size * 0.4f, y + dy * size * 0.4f);
        }

        // Flames left burning on the floor a while after a blast, shrinking as they go out.
        internal static void Flames(List<FxStamp> o, int count, float t, float from, float until, float R, int seed, float cx = 0, float cy = 0)
        {
            for (int i = 0; i < count; i++)
            {
                float left = U(until - t - i * 0.08f, 0, 0.5f);
                if (t < from || left <= 0) continue;
                float angle = (i + 0.3f) / count * 360 + 110;
                int size = 2 * (int)Math.Round((2 + 8 * left) / 2);
                Put(o, FxShape.Flame, FxRamp.Fire, size, (int)(t * 14) + i, cx + Cos(angle) * R * 0.45f, cy + Sin(angle) * R * 0.35f + size / 2f - 2, 1);
            }
        }

        // Crystals standing round the edge of a reach as the wave gets there, each leaning out from the middle and up
        // a little (as a thing standing on a floor does, seen from above), tall and short by turns; they hold until
        // breaks, then fall in two pieces each until over.
        internal static void Crystals(List<FxStamp> o, FxRamp ramp, int count, float t, float from, float breaks, float over, float R, int tall, int seed)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = (i + 0.4f * Rnd(seed, i, 19)) / count * 360, born = from + 0.05f * Rnd(seed, i, 20);
                float bx = Cos(angle) * (R - 3), by = Sin(angle) * (R - 3);
                float lean = (float)Math.Atan2(Sin(angle) + 0.45f, Cos(angle)) * Deg;
                if (In(t, born, breaks))
                {
                    int length = 2 * (int)Math.Round((4 + ((i & 1) == 0 ? tall : tall * 0.6f) * Out(U(t, born, born + 0.07f))) / 2);
                    Put(o, FxShape.Shard, ramp, length, (i & 1) == 0 ? 1 : 0, bx + Cos(lean) * length * 0.3f, by + Sin(lean) * length * 0.3f, 1, lean);
                }
                else if (In(t, breaks, over))
                {
                    float u = U(t, breaks, over);
                    for (int piece = 0; piece < 2; piece++)
                    {
                        float away = angle + (piece == 0 ? -35 : 35), distance = 3 + 8 * Out(u) * (piece + 1) / 2f;
                        Put(o, FxShape.Shard, ramp, 4 + 2 * (1 - piece), 0, bx + Cos(away) * distance, by + Sin(away) * distance + 5 * (1 - u) - 9 * u * u, 1 - u * u, away + 220 * u);
                    }
                }
            }
        }

        // The element lying over the floor out to the reach, spreading fast, with pale veins running out through it.
        internal static void Floor(List<FxStamp> o, FxRamp ramp, int veins, float t, float from, float until, float R, int seed)
        {
            if (!In(t, from, until)) return;
            float grow = Out(U(t, from, from + 0.22f)), left = U(until - t, 0, 0.3f);
            Put(o, FxShape.Wash, ramp, SkillFxArt.Step4(Math.Min(128, 2 * R * grow)), seed & 3, 0, 0, left, 0, true);
            for (int i = 0; i < veins; i++)
            {
                float angle = (i + 0.3f * Rnd(seed, i, 36)) / veins * 360;
                int length = SkillFxArt.Step4(R * (0.6f + 0.25f * Rnd(seed, i, 37)) * grow);
                Put(o, FxShape.Crack, ramp, length, SkillFxArt.SplitFrame(i + seed, 1), Cos(angle) * (R * 0.12f + length / 2f), Sin(angle) * (R * 0.12f + length / 2f), 0.6f * left, angle, true);
            }
        }

        // ---- where a skill is used ----

        static float Cast(string skill, in FxContext c, float t, List<FxStamp> o)
        {
            // The weapon arts' (SkillFx.Arts.cs).
            if (IsArt(skill)) return ArtCast(skill, c, t, o);
            float R = c.Radius, face = c.Dir * Deg;
            int seed = c.Seed;
            var m = c.Main;
            const float A = Lead;
            switch (skill)
            {
                // ---------------- warrior ----------------
                case "whirlwind":
                {
                    // Two heavy smears sweep round at the true reach, a turn and a quarter, slowing: broad and pale
                    // at the head, drawn out thin behind, shorter as they slow, with a lighter pair inside. Dust
                    // lifts from the edge where a blade has just passed, sparks fly off along the cut.
                    const float life = 0.62f;
                    Gather(o, m, t, A, R * 0.8f, 6, seed, cy: 5);
                    if (In(t, A, life))
                    {
                        float u = U(t, A, life), turn = face + 450 * Out(u), alpha = u < 0.75f ? 1 : 1 - (u - 0.75f) / 0.25f;
                        int wide = 10 * (int)Math.Round((40 + 150 * (1 - u)) / 10), inner = 10 * (int)Math.Round(wide * 0.07f);
                        for (int k = 0; k < 2; k++)
                        {
                            Put(o, FxShape.Smear, m, (int)R - 1, wide, 0, 0, alpha, turn + k * 180 - wide / 2f);
                            Put(o, FxShape.Smear, m, (int)(R * 0.62f), inner, 0, 0, alpha * 0.6f, turn + 45 + k * 180 - inner / 2f);
                        }
                        Put(o, FxShape.Edge, m, SkillFxArt.Even(2 * R), 1, 0, 0, 0.35f * alpha, 0, true);
                    }
                    Ring(o, m, t, A, R, 1, 0.08f, 0.2f);
                    for (int i = 0; i < 10; i++)
                    {
                        float swept = (i + 0.5f) * 36, born = A + (life - A) * (1 - (float)Math.Pow(1 - swept / 450, 1 / 3f)), p = (t - born) / 0.4f;
                        if (p < 0 || p >= 1) continue;
                        float angle = face + swept, far = R + 2 + 5 * p;
                        Put(o, FxShape.Smoke, FxRamp.Dust, 5 + 2 * (int)(p * 2), p < 0.3f ? 0 : p < 0.6f ? 1 : 2, Cos(angle) * far, Sin(angle) * far + 3 * p, 0.55f * (1 - p));
                    }
                    for (int k = 0; k < 2; k++)
                    {
                        float at = A + 0.03f + k * 0.14f, turn = face + 450 * Out(U(at, A, life)) + k * 180;
                        FxLayers.Embers(o, m, 4, t, at, 12, seed + k, Cos(turn) * R * 0.9f, Sin(turn) * R * 0.9f, 70, turn + 90, 0.22f);
                    }
                    // Glints where the blades stop.
                    for (int i = 0; i < 5; i++)
                    {
                        float born = life - 0.1f + i * 0.03f;
                        if (In(t, born, born + 0.2f)) Put(o, FxShape.Glint, m, t - born < 0.1f ? 7 : 3, 0, Cos(i * 72 + 40) * R, Sin(i * 72 + 40) * R);
                    }
                    return 0.75f;
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
                    FxLayers.Shock(o, FxRamp.Gold, t, A, 20, 0, 2, 0.26f, true);
                    if (In(t, A, 0.5f)) FxLayers.Light(o, FxRamp.Gold, 40, 0.7f * (1 - U(t, 0.25f, 0.5f)), 0, 6);
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
                        FxLayers.Shock(o, FxRamp.Wind, t, 0, 12, 0, 0, 0.24f, true);
                        return 0.8f;
                    }
                    // The blow at the end of it: a crescent ahead, the edge of its reach, dust.
                    // The blow at the end of it: a heavy smear across the front, a wave over the floor at its true
                    // reach, sparks thrown on ahead and dust left along the way.
                    if (In(t, 0, 0.22f)) Put(o, FxShape.Smear, m, 19, 130, (float)Math.Cos(c.Dir) * 3, Body + (float)Math.Sin(c.Dir) * 3, 1 - U(t, 0.12f, 0.22f), face - 20);
                    Ring(o, m, t, 0, R, 2, 0.08f, 0.22f);
                    FxLayers.Shock(o, m, t, 0, R / 1.3f, 0, 0, 0.24f, true);
                    FxLayers.Embers(o, m, 6, t, 0, 16, seed, (float)Math.Cos(c.Dir) * 10, Body + (float)Math.Sin(c.Dir) * 10, 120, face, 0.24f);
                    for (int i = 0; i < 3; i++) Puffs(o, FxRamp.Dust, 2, t, 0.02f * i, 0.4f, 1, 5, seed + i, 5, 0.45f, bx * L * (i + 0.5f) / 3.5f, by * L * (i + 0.5f) / 3.5f, 3);
                    Fly(o, FxShape.Dot, FxRamp.Dust, 2, 2, 6, t, 0.08f, 0.4f, 4, 16, seed, rise: 3);
                    return 0.55f;
                }
                // ---------------- the dodge roll ----------------
                case Roll:
                {
                    float fx = (float)Math.Cos(c.Dir), fy = (float)Math.Sin(c.Dir), L = c.Length;
                    // Dust where they threw themselves down, a puff each time the body comes round onto the floor, and
                    // grit kicked ahead where they come up.
                    Puffs(o, FxRamp.Dust, 4, t, 0, 0.35f, 2, 8, seed, 5, 0.55f, 0, 0, 4);
                    for (int i = 0; i < 3; i++)
                    {
                        float share = (i + 1) / 3.5f, from = RollTime * share;
                        Puffs(o, FxRamp.Dust, 2, t, from, from + 0.3f, 1, 5, seed + i + 1, 5, 0.45f, fx * L * share, fy * L * share, 3);
                    }
                    Fly(o, FxShape.Dot, FxRamp.Dust, 2, 2, 5, t, RollTime - 0.03f, 0.26f, 2, 10, seed, rise: 3, cx: fx * L, cy: fy * L, cone: 140, toward: face);
                    // A faint wave over the floor where they come up.
                    FxLayers.Shock(o, FxRamp.Dust, t, RollTime - 0.03f, 6, fx * L, fy * L, 0.18f, true);
                    return 0.58f;
                }
                case RollSlide:
                {
                    float fx = (float)Math.Cos(c.Dir), fy = (float)Math.Sin(c.Dir), L = c.Length;
                    Puffs(o, FxRamp.Dust, 3, t, 0, 0.3f, 2, 7, seed, 5, 0.5f, 0, 0, 3);
                    // The floor scuffed along the way, each mark laid as the feet pass and thinning after.
                    for (int i = 0; i < 6; i++)
                    {
                        float share = (i + 0.5f) / 6, from = RollTime * share, aside = (i % 2 == 0 ? 3 : -3) + (Rnd(seed, i, 13) - 0.5f) * 2;
                        if (!In(t, from, from + 0.24f)) continue;
                        Put(o, FxShape.Streak, FxRamp.Dust, 8 + 2 * (i % 2), 1, fx * L * share - fy * aside, 1 + fy * L * share + fx * aside, 0.7f * (1 - U(t, from, from + 0.24f)), face);
                    }
                    // Grit sprayed ahead off the leading foot where the slide stops.
                    Fly(o, FxShape.Dot, FxRamp.Dust, 2, 2, 6, t, RollTime - 0.06f, 0.28f, 3, 14, seed, rise: 2, cx: fx * L, cy: fy * L, cone: 70, toward: face);
                    Puffs(o, FxRamp.Dust, 2, t, RollTime - 0.04f, RollTime + 0.3f, 2, 6, seed + 9, 5, 0.4f, fx * L, fy * L, 3);
                    return 0.6f;
                }
                case RollBlink:
                {
                    float fx = (float)Math.Cos(c.Dir), fy = (float)Math.Sin(c.Dir), L = c.Length;
                    // A glint where they left and where they arrive, afterimages left along the way as the body passes
                    // (the first to be left the first to go), and motes that hang a moment and rise.
                    if (In(t, 0, 0.12f))
                    {
                        Put(o, FxShape.Glint, m, 9, 1, 0, Body, 1 - U(t, 0.05f, 0.12f));
                        FxLayers.Light(o, m, 20, 0.45f * (1 - U(t, 0.05f, 0.12f)), 0, Body);
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        float share = (k + 0.3f) / 3.3f, from = RollTime * share;
                        if (In(t, from, from + 0.26f)) Put(o, FxShape.Ghost, m, 1, 0, fx * L * share, fy * L * share, 0.5f * (1 - U(t, from, from + 0.26f)));
                    }
                    for (int i = 0; i < 7; i++)
                    {
                        float share = (i + Rnd(seed, i, 14)) / 7, from = RollTime * share, u = U(t, from, from + 0.32f);
                        if (!In(t, from, from + 0.32f)) continue;
                        float aside = (Rnd(seed, i, 15) - 0.5f) * 10;
                        Put(o, FxShape.Dot, m, 1, 2, fx * L * share - fy * aside, Body - 4 + fy * L * share + fx * aside + 7 * u, 1 - u);
                    }
                    if (In(t, RollTime - 0.04f, RollTime + 0.12f))
                    {
                        Put(o, FxShape.Glint, m, 7, 0, fx * L, Body + fy * L, 1 - U(t, RollTime + 0.02f, RollTime + 0.12f));
                        FxLayers.Light(o, m, 18, 0.4f * (1 - U(t, RollTime + 0.02f, RollTime + 0.12f)), fx * L, Body + fy * L);
                    }
                    return 0.62f;
                }
                case "ground_slam":
                {
                    // The floor splits from the blow, a cloud of dust bursts up, a wave runs over the floor to the
                    // true reach, rocks are thrown up over their shadows, and the splits stay a while.
                    Gather(o, FxRamp.Dust, t, A, R * 0.6f, 8, seed, cy: 2);
                    if (In(t, A, 0.95f))
                    {
                        float grown = Math.Max(0.3f, Out(U(t, A, A + 0.1f))), alpha = t < 0.6f ? 1 : 1 - U(t, 0.6f, 0.95f);
                        for (int i = 0; i < 8; i++)
                        {
                            float angle = (i + Rnd(seed, i, 13) * 0.6f) / 8 * 360;
                            int length = SkillFxArt.Step4(R * (0.6f + 0.3f * Rnd(seed, i, 14)) * grown);
                            Put(o, FxShape.Crack, FxRamp.Dust, length, SkillFxArt.SplitFrame(i + seed, t < 0.2f ? 1 : 2), Cos(angle) * (length / 2f + 2), Sin(angle) * (length / 2f + 2), alpha, angle, true);
                        }
                    }
                    FxLayers.Ball(o, FxRamp.Dust, t, A, R * 0.36f, seed, 0, 2);
                    FxLayers.Shock(o, FxRamp.Dust, t, A, R / 1.3f, 0, 0, 0.3f, true);
                    Ring(o, m, t, A, R, 3, 0.12f, 0.34f);
                    FxLayers.Rubble(o, FxRamp.Dust, 6, t, A, R * 0.75f, seed);
                    Fly(o, FxShape.Chunk, FxRamp.Dust, 3, 0, 6, t, A, 0.42f, 6, R * 0.9f, seed, hop: 9);
                    Puffs(o, FxRamp.Dust, 10, t, 0.2f, 0.85f, R * 0.8f, R, seed, 7, 0.55f);
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
                    FxLayers.Embers(o, FxRamp.Steel, 5, t, 0.2f, 13, seed, 0, Body, life: 0.22f);
                    if (In(t, 0.14f, 0.4f)) FxLayers.Light(o, FxRamp.Steel, 28, 0.5f * (1 - U(t, 0.25f, 0.4f)), 0, Body, 3);
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
                            Put(o, FxShape.Smear, m, SkillFxArt.Step4(orbit), 60, 0, 0, alpha * 0.85f, angle - 62);
                            Put(o, FxShape.Blade, m, 15, 0, Cos(angle) * (orbit - 2), Sin(angle) * (orbit - 2), alpha, angle + 90);
                        }
                    }
                    Ring(o, m, t, 0.12f, R, 2, 0.14f, 0.3f);
                    FxLayers.Shock(o, m, t, 0.12f, R / 1.3f, 0, 0, 0.32f, true);
                    FxLayers.Embers(o, m, 8, t, 0.56f, R * 0.25f, seed, life: 0.24f);
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
                    FxLayers.Embers(o, FxRamp.Fire, 5, t, A, 13, seed, 0, Body);
                    Float(o, FxShape.Dot, FxRamp.Fire, 1, 2, 7, t, 0.3f, 0.8f, 9, 18, seed, cy: 6);
                    if (In(t, A, 0.6f)) FxLayers.Light(o, FxRamp.Fire, 34, 0.7f * (1 - U(t, 0.3f, 0.6f)), 0, Body);
                    return 0.8f;
                }
                case "fire_ring":
                {
                    // A wave of fire runs out to the true reach and stands there as a ring of flames, over soot, and
                    // goes out into sparks and smoke; no filled hoop, only tongues and the edge.
                    Gather(o, FxRamp.Fire, t, A, R * 0.6f, 8, seed);
                    FxLayers.Scorch(o, t, A + 0.06f, 0.85f, R, seed);
                    if (In(t, A, A + 0.08f)) FxLayers.Light(o, FxRamp.Fire, R * 1.5f, 1, 0, 0, 3);
                    if (In(t, A, 0.7f)) FxLayers.Light(o, FxRamp.Fire, R * 1.7f, 0.85f * (1 - U(t, 0.35f, 0.7f)));
                    FxLayers.Shock(o, FxRamp.Fire, t, A, R / 1.3f, life: 0.26f);
                    const int flames = 14;
                    if (In(t, A, 0.62f))
                    {
                        float u = U(t, A, A + 0.16f), late = U(t, 0.4f, 0.62f), at = R * (0.3f + 0.66f * Out(u));
                        for (int i = 0; i < flames; i++)
                        {
                            float angle = (i + 0.5f * Rnd(seed, i, 15)) / flames * 360;
                            int size = late > 0.5f ? 6 : late > 0 ? 10 : (i & 1) == 0 ? 16 : 12;
                            Put(o, FxShape.Flame, FxRamp.Fire, size, (int)(t * 14) + i, Cos(angle) * at, Sin(angle) * at + size / 2f - 2, 1 - late * 0.8f);
                        }
                    }
                    Ring(o, FxRamp.Fire, t, A, R, 2, 0.14f, 0.36f);
                    FxLayers.Embers(o, FxRamp.Fire, 10, t, A + 0.1f, R * 0.85f, seed, life: 0.4f);
                    Float(o, FxShape.Dot, FxRamp.Fire, 1, 2, 8, t, 0.35f, 0.85f, R, 14, seed);
                    Puffs(o, FxRamp.Smoke, 6, t, 0.42f, 0.85f, R * 0.85f, R * 0.95f, seed, 6, 0.45f, rise: 9);
                    return 0.85f;
                }
                case "inferno":
                {
                    // The floor splits and glows, pillars of flame burst up in two rings, the inner first, round a
                    // ball of fire, and the ground is left burnt.
                    Gather(o, FxRamp.Fire, t, 0.12f, R * 0.6f, 12, seed);
                    if (In(t, 0.08f, 1.25f)) Put(o, FxShape.Scorch, FxRamp.Fire, SkillFxArt.Step4(R * 1.5f), 0, 0, 0, 0.6f * U(t, 0.08f, 0.2f) * (1 - U(t, 0.85f, 1.25f)), 0, true);
                    FxLayers.Splits(o, FxRamp.Fire, 7, t, 0.12f, 1.25f, R * 0.9f, seed);
                    if (In(t, 0.12f, 0.2f)) FxLayers.Light(o, FxRamp.Fire, R * 1.6f, 1, 0, 0, 3);
                    if (In(t, 0.08f, 0.8f)) FxLayers.Light(o, FxRamp.Fire, R * 1.8f, 0.95f * U(t, 0.08f, 0.14f) * (1 - U(t, 0.45f, 0.8f)));
                    if (In(t, 0.5f, 1.15f)) FxLayers.Light(o, FxRamp.Fire, R, 0.4f * (1 - U(t, 0.8f, 1.15f)), 0, 0, 0);
                    for (int ring = 0; ring < 2; ring++)
                    {
                        float from = 0.12f + ring * 0.08f, at = R * (ring == 0 ? 0.45f : 0.9f);
                        int count = ring == 0 ? 6 : 12;
                        if (!In(t, from, from + 0.46f)) continue;
                        float up = Out(U(t, from, from + 0.07f)), down = U(t, from + 0.3f, from + 0.46f);
                        int size = 2 * (int)Math.Round((8 + 16 * up) * (1 - down * 0.6f) / 2);
                        for (int i = 0; i < count; i++)
                        {
                            float angle = (i + ring * 0.5f) / count * 360 + seed % 30;
                            Put(o, FxShape.Flame, FxRamp.Fire, size + (i & 1) * 2, (int)(t * 14) + i, Cos(angle) * at, Sin(angle) * at + size / 2f - 2, 1 - down * 0.7f);
                        }
                    }
                    FxLayers.Ball(o, FxRamp.Fire, t, 0.12f, R * 0.3f, seed, 0, 3);
                    FxLayers.Shock(o, FxRamp.Fire, t, 0.12f, R / 1.3f, life: 0.34f);
                    Ring(o, FxRamp.Fire, t, 0.12f, R, 3, 0.14f, 0.42f);
                    FxLayers.Embers(o, FxRamp.Fire, 14, t, 0.14f, R * 0.8f, seed, life: 0.45f);
                    Float(o, FxShape.Dot, FxRamp.Fire, 2, 2, 8, t, 0.4f, 1.05f, R * 0.9f, 20, seed);
                    FxLayers.Plumes(o, 5, t, 0.5f, R * 0.45f, seed, life: 0.55f);
                    Puffs(o, FxRamp.Smoke, 6, t, 0.6f, 1.05f, R * 0.8f, R * 0.92f, seed, 6, 0.4f, rise: 10);
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
                    if (strong)
                    {
                        Ring(o, m, t, 0.08f, 10, 1, 0.06f, 0.18f, hx, hy);
                        FxLayers.Shock(o, m, t, 0.08f, 12, hx, hy, 0.2f);
                        FxLayers.Embers(o, FxRamp.Gold, 5, t, 0.08f, 14, seed, hx, hy, 90, face + 180, 0.2f);
                    }
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
                    FxLayers.Embers(o, FxRamp.Gold, 4, t, 0.28f, 11, seed, 0, 14, life: 0.2f);
                    if (In(t, 0.24f, 0.5f)) FxLayers.Light(o, FxRamp.Gold, 30, 0.6f * (1 - U(t, 0.36f, 0.5f)), 0, 10);
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
                    FxLayers.Shock(o, FxRamp.Spirit, t, 0.04f, circle / 2.6f, 0, 0, 0.24f, true);
                    if (In(t, 0.08f, 0.9f)) FxLayers.Light(o, FxRamp.Spirit, circle * 0.9f, 0.7f * U(t, 0.08f, 0.16f) * (1 - U(t, 0.55f, 0.9f)));
                    if (In(t, 0.14f, 0.85f))
                    {
                        float alpha = 0.9f * (1 - U(t, 0.55f, 0.85f));
                        // The floor inside the circle takes the spirits' colour while it is open.
                        Put(o, FxShape.Wash, FxRamp.Spirit, SkillFxArt.Step4(circle), seed & 3, 0, 0, alpha, 0, true);
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
                    // Sparks where the wisps arrive.
                    FxLayers.Embers(o, FxRamp.Spirit, 3 + count, t, 0.48f, 14, seed, 0, 10, life: 0.22f);
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
                    FxLayers.Embers(o, FxRamp.Spirit, 4, t, A, 12, seed, 0, Body, life: 0.2f);
                    Float(o, FxShape.Dot, FxRamp.Spirit, 1, 2, 6, t, 0.35f, 0.8f, 10, 14, seed, cy: 4);
                    if (In(t, A, 0.55f)) FxLayers.Light(o, FxRamp.Spirit, 32, 0.65f * (1 - U(t, 0.3f, 0.55f)), 0, Body);
                    return 0.8f;
                }
                case "life_spring":
                {
                    // A fountain: motes and plus signs thrown up from the feet, spreading as they slow, a ring on the ground.
                    Gather(o, FxRamp.Heal, t, A, 20, 6, seed, cy: 2);
                    Ring(o, FxRamp.Heal, t, A, 13, 2, 0.1f, 0.3f, ground: true);
                    FxLayers.Shock(o, FxRamp.Heal, t, A, 12, 0, 0, 0.28f, true);
                    if (In(t, A, 0.85f)) Put(o, FxShape.Wash, FxRamp.Heal, 32, seed & 3, 0, 0, U(t, A, A + 0.1f) * U(0.85f - t, 0, 0.3f), 0, true);
                    if (In(t, A, 0.8f)) FxLayers.Light(o, FxRamp.Heal, 38, 0.7f * (1 - U(t, 0.45f, 0.8f)), 0, 4);
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
                    // The spirits' own blast: a ball that swells and hollows, a wave to the true reach, the floor left
                    // in their colour a moment, sparks; no smoke and no soot.
                    Floor(o, FxRamp.Spirit, 0, t, lead, 0.85f, R, seed);
                    if (In(t, lead, lead + 0.08f)) FxLayers.Light(o, FxRamp.Spirit, R * 1.6f, 1, 0, 0, 3);
                    if (In(t, lead, 0.7f)) FxLayers.Light(o, FxRamp.Spirit, R * 1.7f, 0.85f * (1 - U(t, 0.3f, 0.7f)));
                    FxLayers.Ball(o, FxRamp.Spirit, t, lead, R * 0.4f, seed, 0, 4);
                    FxLayers.Shock(o, FxRamp.Spirit, t, lead, R / 1.3f, life: 0.3f);
                    FxLayers.Embers(o, FxRamp.Spirit, 10, t, lead, R * 0.8f, seed, life: 0.36f);
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
                        FxLayers.Light(o, FxRamp.Bolt, 22, 0.7f, hx, hy, 2);
                    }
                    if (In(t, 0, 0.1f)) Put(o, FxShape.Glint, FxRamp.Bolt, 7, 1, hx, hy);
                    Fly(o, FxShape.Dot, FxRamp.Bolt, 1, 2, 4, t, 0.04f, 0.2f, 2, 10, seed, cx: hx, cy: hy);
                    return 0.26f;
                }
                case "ice_lance":
                    return Muzzle(o, FxRamp.Cold, c, t, 8);
                case "frost_ring":
                {
                    // Frost runs out over the floor behind a wave, crystals stand up round the true reach as it gets
                    // there, hold a moment, then break and fall.
                    Gather(o, FxRamp.Cold, t, A, R * 0.6f, 8, seed);
                    Floor(o, FxRamp.Cold, 8, t, A, 0.85f, R, seed);
                    if (In(t, A, A + 0.08f)) FxLayers.Light(o, FxRamp.Cold, R * 1.5f, 1, 0, 0, 3);
                    if (In(t, A, 0.6f)) FxLayers.Light(o, FxRamp.Cold, R * 1.6f, 0.8f * (1 - U(t, 0.3f, 0.6f)));
                    FxLayers.Shock(o, FxRamp.Cold, t, A, R / 1.3f, life: 0.28f);
                    Ring(o, FxRamp.Cold, t, A, R, 2, 0.1f, 0.34f);
                    Crystals(o, FxRamp.Cold, 14, t, A + 0.07f, 0.5f, 0.85f, R, 12, seed);
                    Puffs(o, FxRamp.Mist, 6, t, 0.2f, 0.8f, R * 0.6f, R * 0.9f, seed, 7, 0.32f, rise: 3);
                    // Glints over the frost, one after another.
                    for (int i = 0; i < 6; i++)
                    {
                        float born = A + 0.1f + 0.08f * i;
                        if (!In(t, born, born + 0.16f)) continue;
                        float angle = Rnd(seed, i, 38) * 360, far = R * (0.2f + 0.6f * Rnd(seed, i, 39));
                        Put(o, FxShape.Glint, FxRamp.Cold, t - born < 0.08f ? 5 : 3, 0, Cos(angle) * far, Sin(angle) * far);
                    }
                    return 0.85f;
                }
                case "storm_mantle":
                {
                    Gather(o, FxRamp.Bolt, t, A, 20, 6, seed);
                    // Arcs crackle round the body, new ones every moment, with a rim of cold; each lights the floor.
                    if (In(t, A, 0.44f) && (int)(t * 40) % 4 != 3)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            float angle = i * 72 + (int)(t * 25) * 31;
                            Zap(o, t, Cos(angle) * 4, Body + Sin(angle) * 4, Cos(angle + 20) * 17, Body + Sin(angle + 20) * 15, seed + i);
                        }
                        FxLayers.Light(o, FxRamp.Bolt, 36, 0.75f, 0, Body, 2);
                    }
                    Ring(o, FxRamp.Cold, t, A, 14, 2, 0.08f, 0.28f, cy: Body);
                    FxLayers.Embers(o, FxRamp.Bolt, 5, t, A, 14, seed, 0, Body);
                    if (In(t, 0.2f, 0.8f))
                    {
                        float u = U(t, 0.2f, 0.8f);
                        for (int i = 0; i < 6; i++) Put(o, FxShape.Dot, FxRamp.Cold, 1, 3, Cos(i * 60 + 300 * u) * (11 + 4 * u), Body + Sin(i * 60 + 300 * u) * (7 + 2 * u), 1 - u);
                    }
                    return 0.8f;
                }
                case "thunderclap":
                {
                    // Six strikes one after another, five round the caster and the last on them: where each will
                    // fall a small ring first, then a thick bolt from the sky, drawn anew every 0.04 s, with a halo
                    // and a star where it lands, sparks thrown off, and a burnt spot left. Arcs run round the edge of
                    // what it hits. Each strike flashes on the light map; the room is never darkened.
                    Gather(o, FxRamp.Bolt, t, A, R * 0.5f, 6, seed, cy: 2);
                    int flicker = (int)(t / 0.04f);
                    float lit = 0, lx = 0, ly = 0;
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = i * 72 + 23 + seed % 40, far = i == 5 ? 0 : R * 0.68f;
                        float x = Cos(angle) * far, y = Sin(angle) * far * 0.9f, born = A + i * 0.035f, age = t - born;
                        if (age < 0)
                        {
                            Put(o, FxShape.Edge, FxRamp.Bolt, 6 + 2 * ((flicker + i) & 1), 1, x, y, 0.8f, 0, true);
                            continue;
                        }
                        if (age < 0.75f) Put(o, FxShape.Scorch, FxRamp.Smoke, 12, 4 + (i & 3), x, y, 0.85f * (1 - U(age, 0.4f, 0.75f)), 0, true);
                        float top = x + (Rnd(seed, i, 21) - 0.5f) * 24;
                        if (age < 0.1f)
                        {
                            Zap(o, t, top, y + 100, x, y, seed + i * 13, true, 1, FxRamp.Bolt, true);
                            Put(o, FxShape.Glint, FxRamp.Bolt, 11, 2, x, y);
                            if (age < 0.08f) (lit, lx, ly) = (1, x, y);
                        }
                        else if (age < 0.2f) Put(o, FxShape.Bolt, FxRamp.Bolt, 100, (seed + i) % 6, (top + x) / 2, y + 50, 0.45f, (float)Math.Atan2(-100, x - top) * Deg);
                        FxLayers.Embers(o, FxRamp.Bolt, 3, t, born, 13, seed + i, x, y, life: 0.2f);
                    }
                    if (lit > 0)
                    {
                        FxLayers.Light(o, FxRamp.Bolt, R * 1.7f, (flicker & 1) == 0 ? 1 : 0.7f, 0, 0, 3);
                        FxLayers.Light(o, FxRamp.Bolt, 34, 1, lx, ly, 3);
                    }
                    if (In(t, 0.3f, 0.7f)) FxLayers.Light(o, FxRamp.Bolt, R * 1.2f, 0.45f * (1 - U(t, 0.4f, 0.7f)), 0, 0, 1);
                    if (In(t, A, 0.44f))
                    {
                        float fade = U(0.44f - t, 0, 0.15f);
                        for (int i = 0; i < 10; i++)
                        {
                            if ((i + flicker) % 3 == 0) continue;
                            Zap(o, t, Cos(i * 36) * R, Sin(i * 36) * R, Cos(i * 36 + 36) * R, Sin(i * 36 + 36) * R, seed + i * 5, false, fade);
                        }
                    }
                    Ring(o, FxRamp.Bolt, t, A, R, 3, 0.1f, 0.3f);
                    return 0.95f;
                }
                case "blizzard":
                {
                    // Frost over the whole reach, snow and shards wheeling over it and slowing; crystals stand at its
                    // rim and break as it ends.
                    Gather(o, FxRamp.Cold, t, 0.12f, R * 0.6f, 10, seed);
                    Floor(o, FxRamp.Cold, 0, t, 0.12f, 1.05f, R, seed);
                    if (In(t, 0.12f, 0.2f)) FxLayers.Light(o, FxRamp.Cold, R * 1.5f, 1, 0, 0, 3);
                    if (In(t, 0.12f, 0.9f)) FxLayers.Light(o, FxRamp.Cold, R * 1.6f, 0.8f * (1 - U(t, 0.5f, 0.9f)));
                    FxLayers.Shock(o, FxRamp.Cold, t, 0.12f, R / 1.3f, life: 0.34f);
                    Ring(o, FxRamp.Cold, t, 0.12f, R, 3, 0.14f, 0.5f);
                    if (In(t, 0.1f, 1.05f))
                    {
                        float u = U(t, 0.1f, 1.05f), swirl = 420 * Out(u), alpha = u < 0.7f ? 1 : 1 - (u - 0.7f) / 0.3f;
                        for (int i = 0; i < 30; i++)
                        {
                            float distance = R * (0.2f + 0.78f * (float)Math.Sqrt(Rnd(seed, i, 26))) * (0.5f + 0.5f * Out(U(t, 0.1f, 0.3f)));
                            float angle = Rnd(seed, i, 27) * 360 + swirl * (1.3f - distance / R * 0.6f);
                            float fall = u > 0.7f ? -10 * (u - 0.7f) : 0;
                            if (i % 3 == 0) Put(o, FxShape.Shard, FxRamp.Cold, 8 + (i & 2), 0, Cos(angle) * distance, Sin(angle) * distance + fall, alpha, angle + 90);
                            else Put(o, FxShape.Dot, FxRamp.Cold, 2, 3 - (i & 1), Cos(angle) * distance, Sin(angle) * distance + fall, alpha);
                        }
                    }
                    Crystals(o, FxRamp.Cold, 10, t, 0.18f, 0.75f, 1.05f, R, 16, seed);
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
                    // The comet: a tail of shrinking lumps, sparks shed behind, its light on the floor under it.
                    bool big = skill == "fireball";
                    Comet(o, FxRamp.Fire, t, seed, dx, dy, big ? 4.5f : 3.5f);
                    FxLayers.Light(o, FxRamp.Fire, big ? 20 : 14, big ? 0.6f : 0.45f, 0, -6);
                    break;
                }
                case "meteor":
                {
                    // A burning rock coming down out of the dark onto the place its flight ends, over a shadow that
                    // grows and a light that brightens under it. Only the picture falls: the host's projectile goes
                    // along the floor as before, and the shadow is where it is.
                    float p = c.Span > 0 ? Math.Min(1, t / c.Span) : 1, fall = (1 - p) * (1 - p);
                    float high = 6 + 74 * fall, back = 34 * fall;
                    // The way it goes on the screen: the flight along the floor, less the height and the lag it loses.
                    float rate = c.Span > 0 ? 2 * (1 - p) / c.Span : 0, pace = c.Span > 0 ? c.Length / c.Span : 1;
                    float vx = dx * (pace + 34 * rate), vy = dy * (pace + 34 * rate) - 74 * rate;
                    float speed = (float)Math.Sqrt(vx * vx + vy * vy);
                    if (speed < 0.01f) (vx, vy, speed) = (dx, dy, 1);
                    Put(o, FxShape.Shadow, FxRamp.Smoke, SkillFxArt.Even(8 + 10 * p), 0, 0, -3, 0.6f + 0.4f * p, 0, true);
                    FxLayers.Light(o, FxRamp.Fire, 22 + 26 * p, 0.35f + 0.55f * p, 0, -3);
                    // Where it will come down: a ring of dashes the size of its burst, turning slowly, surer as it nears.
                    if (c.Mark > 0)
                        Put(o, FxShape.Broken, FxRamp.Gold, SkillFxArt.Even(2 * c.Mark), SkillFxArt.BrokenFrame(1, 8, (int)(t * 5)), c.MarkX, c.MarkY, MarkAlpha(p), 0, true);
                    float x = -dx * back, y = high - dy * back;
                    for (int k = 0; k < 3; k++)
                    {
                        float phase = Frac(t * 4 + k / 3f), far = 22 + phase * 26;
                        Put(o, FxShape.Plume, FxRamp.Smoke, 6 + 2 * (int)(phase * 3), Math.Min(5, 1 + (int)(phase * 5)), x - vx / speed * far, y - vy / speed * far + phase * 5, 0.7f * (1 - phase));
                    }
                    Comet(o, FxRamp.Fire, t, seed, vx / speed, vy / speed, 7.5f, x, y, true);
                    break;
                }
                case "volley":
                    // A long thin streak and a short bright one close behind the arrow.
                    Put(o, FxShape.Streak, m, 24, 1, -dx * 18, -dy * 18, 0.7f, face);
                    Put(o, FxShape.Streak, m, 10, 2, -dx * 9, -dy * 9, 0.9f, face);
                    Put(o, FxShape.Arrow, m, 11, 0, 0, 0, 1, face);
                    break;
                case "power_shot":
                    // A long bright streak and air winding round it.
                    Put(o, FxShape.Streak, m, 36, 2, -dx * 30, -dy * 30, 0.6f, face);
                    Put(o, FxShape.Streak, m, 28, 3, -dx * 18, -dy * 18, 0.9f, face);
                    for (int k = 0; k < 3; k++)
                    {
                        float phase = Frac(t * 5 + k / 3f);
                        Put(o, FxShape.Ember, FxRamp.Gold, 3, phase < 0.5f ? 0 : 1, -dx * (10 + phase * 22) - dy * (k - 1) * 3, -dy * (10 + phase * 22) + dx * (k - 1) * 3, 1 - phase, face);
                    }
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
                    // Where it will go off (F2, as the meteor's): its burst is wide, so a faint ring of dashes its size.
                    // The quick, small ones (the fireball, the flame burst) show none: the screen would fill with rings.
                    if (c.Mark > 0)
                        Put(o, FxShape.Broken, FxRamp.Gold, SkillFxArt.Even(2 * c.Mark), SkillFxArt.BrokenFrame(1, 8, (int)(t * 5)), c.MarkX, c.MarkY, MarkAlpha(c.Span > 0 ? t / c.Span : 1) * 0.85f, 0, true);
                    // Its charge blinks.
                    bool lit = ((int)(t * 12) & 1) == 0;
                    Put(o, FxShape.Dot, FxRamp.Fire, lit ? 3 : 2, lit ? 3 : 1, dx * 5, dy * 5);
                    // Sparks off the fuse, and its small light on the floor.
                    for (int k = 0; k < 2; k++)
                    {
                        float phase = Frac(t * 6 + k / 2f);
                        Put(o, FxShape.Ember, FxRamp.Fire, 3, phase < 0.5f ? 0 : 1, dx * 3 - dx * phase * 12 - dy * (k * 4 - 2) * phase, dy * 3 - dy * phase * 12 + dx * (k * 4 - 2) * phase + 3 * phase, 1 - phase, face);
                    }
                    FxLayers.Light(o, FxRamp.Fire, 12, lit ? 0.5f : 0.35f, 0, -6);
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
                    Put(o, FxShape.Bolt, FxRamp.Bolt, 20, (int)(t * 25) % 6 + 16, -dx * 9, -dy * 9, 1, face);
                    Put(o, FxShape.Glint, FxRamp.Bolt, 7, (int)(t * 20) & 1, 0, 0);
                    FxLayers.Light(o, FxRamp.Bolt, 18, ((int)(t * 25) & 1) == 0 ? 0.7f : 0.5f, 0, -6, 2);
                    break;
                case "ice_lance":
                    // A lance with a bright spine, frost shed behind it in motes that shrink, a cold light under it.
                    Put(o, FxShape.Streak, FxRamp.Cold, 16, 2, -dx * 24, -dy * 24, 0.6f, face);
                    for (int k = 0; k < 3; k++)
                    {
                        float phase = Frac(t * 4 + k / 3f);
                        Put(o, FxShape.Lump, FxRamp.Cold, 5 - (int)(phase * 3), 2 * 4 + k, -dx * (16 + phase * 16) - dy * (k - 1) * 2, -dy * (16 + phase * 16) + dx * (k - 1) * 2, 0.6f * (1 - phase));
                    }
                    Trail(o, FxShape.Dot, FxRamp.Cold, t, c, 4, 14, 16, 5, 0, true);
                    Put(o, FxShape.Shard, FxRamp.Cold, 20, 1, -dx * 8, -dy * 8, 1, face);
                    FxLayers.Light(o, FxRamp.Cold, 16, 0.5f, 0, -6);
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
                // The rework's blast (FxLayers.Blast), sized by how much each is.
                case "fireball":
                    return FxLayers.Blast(o, t, R, FxRamp.Fire, seed, embers: 12);
                case "flame_burst":
                    return FxLayers.Blast(o, t, R, FxRamp.Fire, seed, embers: 6, linger: 0.9f);
                case "explosive_arrow":
                    // Powder: rocks thrown up, and flames left burning on the floor a moment.
                    Flames(o, 3, t, 0.24f, 0.95f, Math.Max(10, R), seed);
                    return FxLayers.Blast(o, t, R, FxRamp.Fire, seed, embers: 10, rubble: 4, linger: 1.05f);
                case "meteor":
                {
                    R = Math.Max(10, R);
                    // The floor splits, glowing, and cools; a soft light lies on it while the fire burns.
                    FxLayers.Splits(o, FxRamp.Fire, 9, t, 0, 1.38f, R, seed);
                    if (t < 0.5f) Put(o, FxShape.Glow, FxRamp.Fire, SkillFxArt.Step4(R * 2.4f), 0, 0, 0, 0.55f * (1 - t / 0.5f), 0, true);
                    // Four smaller balls of fire round the first, a moment after it.
                    for (int i = 0; i < 4; i++) FxLayers.Ball(o, FxRamp.Fire, t, 0.03f + i * 0.02f, R * 0.5f, i + 1, Cos(i * 90 + 34) * R * 0.5f, Sin(i * 90 + 34) * R * 0.4f);
                    Flames(o, 4, t, 0.3f, 1.3f, R, seed);
                    return FxLayers.Blast(o, t, R, FxRamp.Fire, seed, embers: 16, rubble: 6, linger: 1.38f);
                }
                case "volley":
                    // A small star where it strikes, chips thrown back off it, a puff of dust.
                    if (In(t, 0, 0.06f)) Put(o, FxShape.Glint, m, 5, 1, 0, 0);
                    Puffs(o, FxRamp.Dust, 1, t, 0, 0.3f, 0, 2, seed, 5, 0.5f);
                    FxLayers.Embers(o, m, 4, t, 0, 8, seed, 0, 0, 150, face + 180, 0.16f);
                    return 0.3f;
                case "power_shot":
                    // It goes in hard: a star, a wave, gold sparks sprayed on past, dust.
                    if (In(t, 0, 0.08f)) Put(o, FxShape.Glint, FxRamp.Gold, 11, 2, 0, 0);
                    Ring(o, FxRamp.Steel, t, 0, 7, 1, 0.05f, 0.15f);
                    FxLayers.Shock(o, FxRamp.Gold, t, 0, 11, life: 0.22f);
                    Fly(o, FxShape.Streak, FxRamp.Gold, 6, 1, 5, t, 0, 0.2f, 2, 15, seed, aimed: true, cone: 110, toward: face);
                    FxLayers.Embers(o, FxRamp.Gold, 8, t, 0, 17, seed, 0, 0, 120, face, 0.22f);
                    Puffs(o, FxRamp.Dust, 2, t, 0.04f, 0.36f, 1, 6, seed, 5, 0.45f, rise: 3);
                    return 0.4f;
                case "arrow_rain":
                    // The arrow stands in the ground a moment in a puff of dust.
                    if (In(t, 0, 0.5f)) Put(o, FxShape.Arrow, m, 11, 0, 1, 3, t < 0.3f ? 1 : 1 - U(t, 0.3f, 0.5f), -65);
                    Ring(o, FxRamp.Dust, t, 0, 5, 1, 0.05f, 0.18f, ground: true);
                    Puffs(o, FxRamp.Dust, 2, t, 0, 0.35f, 1, 5, seed, 4, 0.5f, rise: 3);
                    FxLayers.Embers(o, FxRamp.Dust, 3, t, 0, 7, seed, 0, 0, 160, 90, 0.18f);
                    return 0.5f;
                case "chain_lightning":
                    if (In(t, 0, 0.14f) && (int)(t * 40) % 3 != 2)
                    {
                        for (int i = 0; i < 5; i++) Put(o, FxShape.Bolt, FxRamp.Bolt, 8, (seed + i + (int)(t * 25)) % 6, Cos(i * 72 + seed) * 5, Sin(i * 72 + seed) * 5, 1, i * 72 + seed);
                    }
                    if (In(t, 0, 0.08f))
                    {
                        Put(o, FxShape.Glint, FxRamp.Bolt, 11, 2, 0, 0);
                        FxLayers.Light(o, FxRamp.Bolt, 30, 1, 0, 0, 3);
                    }
                    // A burnt spot where it struck, and sparks with tails.
                    if (In(t, 0, 0.5f)) Put(o, FxShape.Scorch, FxRamp.Smoke, 8, 4 + (seed & 3), 0, -3, 0.8f * (1 - U(t, 0.25f, 0.5f)), 0, true);
                    FxLayers.Embers(o, FxRamp.Bolt, 6, t, 0.02f, 11, seed, life: 0.2f);
                    return 0.5f;
                case "ice_lance":
                    // It shatters: shards fly on past and fall, a breath of mist.
                    if (In(t, 0, 0.07f))
                    {
                        Put(o, FxShape.Glint, FxRamp.Cold, 9, 2, 0, 0);
                        FxLayers.Light(o, FxRamp.Cold, 26, 0.9f, 0, 0, 3);
                    }
                    if (In(t, 0, 0.6f)) Put(o, FxShape.Wash, FxRamp.Cold, 20, seed & 3, 0, -4, U(0.6f - t, 0, 0.3f), 0, true);
                    FxLayers.Shock(o, FxRamp.Cold, t, 0, 9, life: 0.2f);
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
                    return 0.6f;
            }
            return 0;
        }

        // Lightning between two enemies: a jagged line, a new one each flicker, a glint and sparks where it lands.
        static float Chain(in FxContext c, float t, List<FxStamp> o)
        {
            float dx = (float)Math.Cos(c.Dir) * c.Length / 2, dy = (float)Math.Sin(c.Dir) * c.Length / 2;
            float flick = Flicker(t);
            if (flick > 0)
            {
                Zap(o, t, -dx, -dy, dx, dy, c.Seed, c.Length >= 24, flick, FxRamp.Bolt, c.Length >= 24);
                FxLayers.Light(o, FxRamp.Bolt, 26, flick, dx, dy, 3);
            }
            if (In(t, 0, 0.12f)) Put(o, FxShape.Glint, FxRamp.Bolt, 9, 2, dx, dy);
            if (In(t, 0, 0.45f)) Put(o, FxShape.Scorch, FxRamp.Smoke, 8, 4 + (c.Seed & 3), dx, dy - 5, 0.8f * (1 - U(t, 0.25f, 0.45f)), 0, true);
            FxLayers.Embers(o, FxRamp.Bolt, 4, t, 0.03f, 10, c.Seed, dx, dy, life: 0.2f);
            return 0.45f;
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
                    FxLayers.Light(o, FxRamp.Fire, 16, 0.32f + 0.06f * (float)Math.Sin(t * 9), hx, hy);
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
                        FxLayers.Light(o, FxRamp.Bolt, 20, 0.4f, 0, Body, 2);
                    }
                    for (int k = 0; k < 2; k++) Put(o, FxShape.Dot, FxRamp.Cold, 1, 3, Cos(t * 200 + k * 180) * 10, Body + Sin(t * 200 + k * 180) * 5, 0.7f);
                    break;
                }
            }
        }
    }
}
