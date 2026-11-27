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

        // Shots let fly (F2), bosses' and monsters' alike: a short flash and sparks at each muzzle, a ring where
        // they left. The origin is the shooter's middle, Size how far out the shots leave it, Dir the middle of their
        // ways, Width the degrees they spread over (300 and more: all round, one ring at Size and a flash for up to
        // eight of them), Count how many. Ramp what the shots are made of (ShotArt.RampOf); Tint a kind's own colour.
        Muzzle,

        // The rework's explosion (FxLayers.Blast) wherever the view has one that is no skill's or attack's own (F2,
        // in place of the old one-picture burst): Size its reach. Big: rocks thrown. Plain: in the air, so only the
        // ball, the wave, the sparks and the light (no soot or smoke on the floor).
        Blast,

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

        // ---- the bosses of stone (the cave warden, the vein eater, the black knight) ----
        // A blow into the floor or a rock come down on it: a cloud of dust, the floor split, stones thrown up over
        // their shadows, a wave over the floor. Size its radius, Count how many stones (0: by its weight), Big the
        // heavier; Plain leaves the splits out (where the floor is cracked another way).
        Slam,
        // An arm or a hammer swept through a wedge: a smear of dust across it, dust rolling off its arc, stones flung
        // on. Size the reach, Width the wedge in degrees, Dir its middle.
        Sweep,
        // A ring of broken floor rolling out: dust and stones all round the band. Size its outer radius, Inner its inner.
        Rumble,
        // Stone bursting up through the floor: a spike that stands a moment and crumbles. Size its radius.
        Spike,

        // ---- the antlion ----
        // A jet of sand along a line: streams of grains running down it, grains scattered off its sides, dust where
        // it ends. Size its length, Width how wide, Dir its way.
        SandJet,
        // The floor sliding into a funnel: grains running round and in, broken rings closing, a dark middle. Size its
        // radius. Big: the storm's front, thrown up as well.
        Whirl,
        // Sand flung up out of the floor in a fan, falling back, over rolling dust. Size its radius; Width, if set,
        // the fan is thrown along Dir over that many degrees instead of straight up.
        SandBurst,

        // ---- the tide serpent (2026-11-28) ----
        // Its body lashed through a wedge: a wall of water swept across it, foam left along its arc, drops flung on.
        // Size the reach, Width the wedge in degrees, Dir its middle.
        Wave,
        // A jet of water along a line: lumps of water driven down it in three streams, spray off its sides, a splash
        // where it ends. Size its length, Width how wide, Dir its way.
        Torrent,
        // Water churning round a ring's band: broken rings turning, foam running round, drops thrown off the outside.
        // Size the band's outer radius, Inner its inner (0: a whole circle dragged round and in).
        Maelstrom,
        // A column of water bursting up out of the floor and falling back as rain over spreading ripples. Size its
        // radius. Big: the heavier (taller, more of it).
        Geyser,

        // ---- the bosses with no effects of their own (the lava titan, the void lord, the crown wraith) ----
        // What the blow is made of, laid over the shared landing at a few places in its shape (Form): fire that
        // bursts and burns on (Ramp Fire), rings and scraps drawn into the middle (Violet), water thrown up in a
        // column over ripples (Cold), souls scattering (Spirit), a bolt come down (Bolt). Size the reach, Inner a
        // ring's inner radius, Width a wedge's degrees or a line's width, Dir its way.
        Element,
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
        // Without the part of the effect something else already draws there.
        public bool Plain;
        public FxRamp Ramp;
        // A colour (0x1RRGGBB) the Plain ramp's pieces take (a monster's own, for its shots' flash).
        public uint Tint;
        // The blow's shape, for an effect laid over any of them (BossFx.Element).
        public HazardForm Form;
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
                case BossFx.Blast:
                    if (!c.Plain) life = FxLayers.Blast(o, t, c.Size, c.Ramp, c.Seed, c.Big ? 12 : 8, c.Big ? 4 : 0, true, 1f);
                    else
                    {
                        life = 0.6f;
                        if (t < 0.08f) FxLayers.Light(o, c.Ramp, c.Size * 2.8f, 1, 0, 0, 3);
                        if (t < 0.45f) FxLayers.Light(o, c.Ramp, c.Size * 2.1f, 0.9f * (1 - t / 0.45f));
                        FxLayers.Ball(o, c.Ramp, t, 0, c.Size, c.Seed & 3);
                        FxLayers.Shock(o, c.Ramp, t, 0.03f, c.Size);
                        FxLayers.Embers(o, c.Ramp, 8, t, 0.03f, c.Size * 1.3f, c.Seed);
                    }
                    break;
                case BossFx.Muzzle:
                {
                    int from = o.Count;
                    life = Muzzle(c, t, o);
                    if (c.Tint != 0)
                        for (int i = from; i < o.Count; i++)
                        {
                            if (o[i].Ramp != FxRamp.Plain) continue;
                            var stamp = o[i];
                            stamp.Tint = c.Tint;
                            o[i] = stamp;
                        }
                    break;
                }
                case BossFx.FireBand: life = FireBand(c, t, o); break;
                case BossFx.Cleave: life = Cleave(c, t, o); break;
                case BossFx.FireCall: life = FireCall(c, t, o); break;
                case BossFx.Spears: life = Spears(c, t, o); break;
                case BossFx.Slam: life = Slam(c, t, o); break;
                case BossFx.Sweep: life = Sweep(c, t, o); break;
                case BossFx.Rumble: life = Rumble(c, t, o); break;
                case BossFx.Spike: life = Spike(c, t, o); break;
                case BossFx.SandJet: life = SandJet(c, t, o); break;
                case BossFx.Whirl: life = Whirl(c, t, o); break;
                case BossFx.SandBurst: life = SandBurst(c, t, o); break;
                case BossFx.Wave: life = Wave(c, t, o); break;
                case BossFx.Torrent: life = Torrent(c, t, o); break;
                case BossFx.Maelstrom: life = Maelstrom(c, t, o); break;
                case BossFx.Geyser: life = Geyser(c, t, o); break;
                case BossFx.Element: life = Element(c, t, o); break;
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

        public const float MuzzleSeconds = 0.24f;

        // Shots let fly: brightest in its first two frames, gone in a quarter of a second, so it never hides the
        // shots it sends off.
        static float Muzzle(in BossContext c, float t, List<FxStamp> o)
        {
            var m = c.Ramp;
            bool round = c.Width >= 300;
            int count = Math.Max(1, c.Count), shown = Math.Min(count, round ? 8 : 5);
            float far = Math.Max(4, c.Size);
            FxLayers.Light(o, m, round ? far * 2.2f + 24 : 34, 0.8f * (1 - t / 0.16f), round ? 0 : Cos(c.Dir) * far, round ? 0 : Sin(c.Dir) * far);
            // Where they left: a ring opening fast (round the shooter, when they go all ways).
            if (round) FxLayers.Shock(o, m, t, 0, far + 8, life: 0.22f);
            else FxLayers.Shock(o, m, t, 0, 9 + Math.Min(6, count), Cos(c.Dir) * far, Sin(c.Dir) * far, 0.2f);
            for (int i = 0; i < shown; i++)
            {
                float a = round ? c.Dir + i * 360f / shown : shown == 1 ? c.Dir : c.Dir + (i / (float)(shown - 1) - 0.5f) * c.Width;
                float x = Cos(a) * far, y = Sin(a) * far;
                if (t < 0.07f) Put(o, FxShape.Glint, m == FxRamp.Plain ? FxRamp.Plain : FxRamp.Steel, t < 0.035f ? 11 : 9, 2, x, y);
                FxLayers.Embers(o, m, round ? 1 : 2, t, 0, 13, c.Seed + i * 7, x, y, 50, a, 0.16f);
            }
            return MuzzleSeconds;
        }

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
                    case FxRamp.Danger:
                        // A lump of red mist lifting off, thinning as it swells (a boss's last phase).
                        Put(o, FxShape.Smoke, m, Even(5 + 5 * p), Math.Min(3, (int)(p * 4)), x, y + 5 * p, 0.55f * (1 - p));
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
            // Soot along the blade's track (Plain: the blade's smear alone, over a blow that burns its own way).
            for (int i = 0; i < 5 && !c.Plain; i++)
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
            for (int i = 0; i < 9 && !c.Plain; i++)
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

        // ---- the bosses of stone ----

        // A piece in the air but under the warnings on the floor: dust never hides a warning.
        static void PutLow(List<FxStamp> o, FxShape shape, FxRamp ramp, int size, int frame, float x, float y, float alpha)
        {
            if (alpha <= 0.03f || size <= 0) return;
            o.Add(new FxStamp { Shape = shape, Ramp = ramp, Size = size, Frame = frame, X = x, Y = y, Alpha = Math.Min(1, alpha), Low = true });
        }

        // Puffs of dust rolling out from a circle of radius at (or, over a wedge, from its arc), thinning as they go.
        static void Dust(List<FxStamp> o, FxRamp m, int count, float t, float from, float at, float roll, int seed, float dir = 0, float wide = 360, float life = 0.6f, float stagger = 0)
        {
            for (int i = 0; i < count; i++)
            {
                float turn = (i + 0.5f) / count, p = (t - from - stagger * turn - 0.03f * Rnd(seed, i, 21)) / life;
                if (p < 0 || p >= 1) continue;
                float angle = dir + (turn - 0.5f) * wide + (wide >= 360 ? 180 : 0) + 12 * (Rnd(seed, i, 22) - 0.5f), far = at + roll * Out(p);
                PutLow(o, FxShape.Smoke, m, Even(9 + 7 * p + 3 * Rnd(seed, i, 23)), Math.Min(3, (int)(p * 4)), Cos(angle) * far, Sin(angle) * far * 0.92f + 3 * p, 0.85f * (1 - p));
            }
        }

        static float Slam(in BossContext c, float t, List<FxStamp> o)
        {
            var m = c.Ramp;
            float R = c.Size;
            if (!c.Plain) FxLayers.Splits(o, FxRamp.Smoke, c.Big ? 9 : 6, t, 0, 1.1f, R * 0.95f, c.Seed);
            if (R * 2.2f <= 160) FxLayers.Shock(o, m, t, 0, R * 1.1f, 0, 0, 0.32f, true);
            // The cloud where it struck, swelling and thinning, and dust rolling away all round.
            if (In(t, 0.03f, 0.4f))
            {
                float u = U(t, 0.03f, 0.4f);
                PutLow(o, FxShape.Lump, m, Even(R * (0.9f + 0.7f * Out(u))), 4 * (u < 0.4f ? 1 : 2) + ((int)(t * 12) & 3), 0, 2 + 5 * u, 0.8f * (1 - u));
            }
            Dust(o, m, c.Big ? 10 : 8, t, 0.02f, R * 0.6f, R * 0.5f + 6, c.Seed);
            if (t < 0.05f) Put(o, FxShape.Lump, m, Even(R * 0.9f), (int)(t * 40) & 3, 0, 0);
            FxLayers.Rubble(o, m == FxRamp.Sand ? FxRamp.Sand : FxRamp.Dust, c.Count > 0 ? c.Count : c.Big ? 8 : 6, t, 0.02f, R * 0.9f, c.Seed + 1);
            return c.Plain ? 0.8f : 1.1f;
        }

        static float Sweep(in BossContext c, float t, List<FxStamp> o)
        {
            const float sweep = 0.12f;
            var m = c.Ramp;
            float R = c.Size, wide = Math.Clamp(c.Width, 20, 300), half = wide / 2, swept = U(t, 0, sweep);
            if (t < 0.28f)
            {
                float fade = t < 0.14f ? 1 : (0.28f - t) / 0.14f;
                int span = 10 * Math.Max(2, (int)Math.Round(wide * (0.25f + 0.75f * swept) / 10));
                float head = c.Dir - half + wide * swept;
                Put(o, FxShape.Smear, m, SkillFxArt.Even(R * 0.78f), span, 0, 0, fade * 0.7f, head - span / 2f);
                Put(o, FxShape.Smear, m, SkillFxArt.Even(R), span, 0, 0, fade, head - span / 2f);
            }
            // Dust off the arc as the arm passes, and stones flung on past it.
            Dust(o, m, 7, t, 0, R * 0.85f, 14, c.Seed, c.Dir, wide, 0.55f, sweep);
            for (int i = 0; i < 6; i++)
            {
                float turn = (i + 0.5f) / 6, p = (t - sweep * turn) / (0.4f + 0.15f * Rnd(c.Seed, i, 24));
                if (p < 0 || p >= 1) continue;
                float dir = c.Dir - half + wide * turn, far = R * (0.85f + 0.5f * Out(p));
                Put(o, FxShape.Chunk, FxRamp.Dust, 3 + (i & 1), 4 + (i & 3), Cos(dir) * far, Sin(dir) * far + 28 * p * (1 - p), 1 - p * p * p);
            }
            return 0.72f;
        }

        static float Rumble(in BossContext c, float t, List<FxStamp> o)
        {
            var m = c.Ramp;
            float inner = c.Inner, outer = c.Size, mid = (inner + outer) / 2;
            int count = Math.Clamp((int)(mid / 5), 8, 14);
            Dust(o, m, count, t, 0, mid - 4, (outer - inner) * 0.4f + 6, c.Seed, 0, 360, 0.65f);
            // Stones thrown up all round the band.
            for (int i = 0; i < count; i++)
            {
                float own = 0.45f + 0.2f * Rnd(c.Seed, i, 25), p = (t - 0.02f) / own;
                if (p < 0 || p >= 1) continue;
                float dir = (i + 0.5f * Rnd(c.Seed, i, 26)) * 360f / count + 13, far = mid + (outer - mid) * 0.6f * p;
                float x = Cos(dir) * far, y = Sin(dir) * far * 0.95f;
                if (m == FxRamp.Sand)
                {
                    // Sand is thrown out along the floor in streaks, not up in stones.
                    Put(o, FxShape.Grains, m, 12 + 4 * (i & 1), (int)(t * 12) + i, x, y + 5 * p * (1 - p) * 4, 1 - p * p, dir);
                    continue;
                }
                Put(o, FxShape.Shadow, FxRamp.Smoke, 4, 0, x, y - 1, 0.7f, 0, true);
                Put(o, FxShape.Chunk, FxRamp.Dust, 3 + (i & 1), 4 + (i & 3), x, y + 4 * (9 + 8 * Rnd(c.Seed, i, 27)) * p * (1 - p));
            }
            return 0.75f;
        }

        static float Spike(in BossContext c, float t, List<FxStamp> o)
        {
            const float hold = 0.34f, over = 0.7f;
            var m = c.Ramp;
            float R = c.Size;
            FxLayers.Splits(o, FxRamp.Smoke, 4, t, 0, 1.0f, R * 0.9f, c.Seed);
            Dust(o, m, 5, t, 0.02f, R * 0.35f, R * 0.5f + 4, c.Seed, 0, 360, 0.5f);
            int tall = Even(Math.Clamp(R * 1.1f, 14, 30));
            if (t < hold)
            {
                // Up through the floor in a moment, a smaller one either side.
                float up = Out(U(t, 0, 0.07f));
                for (int side = -1; side <= 1; side++)
                {
                    int size = Even(tall * (side == 0 ? 1 : 0.6f) * up);
                    if (size < 4) continue;
                    Put(o, FxShape.Shard, FxRamp.Dust, size, 2, side * R * 0.38f + Cos(90 + side * 14) * size * 0.4f, -2 + size * 0.45f, 1, 90 - side * 14);
                }
            }
            else if (t < over)
            {
                float p = (t - hold) / (over - hold);
                for (int i = 0; i < 6; i++)
                {
                    float dir = 20 + i * 28;
                    Put(o, FxShape.Chunk, FxRamp.Dust, 3 + (i & 1), 4 + (i & 3), Cos(dir) * R * 0.7f * Out(p), tall * 0.5f * (1 - p) + Sin(dir) * 8 * Out(p) - 10 * p * p, 1 - p * p);
                }
            }
            return 1.0f;
        }

        // ---- the antlion ----

        static float SandJet(in BossContext c, float t, List<FxStamp> o)
        {
            const float life = 0.6f;
            var m = c.Ramp;
            float L = Math.Max(24, c.Size), W = Math.Max(8, c.Width), dx = Cos(c.Dir), dy = Sin(c.Dir), left = U(life - t, 0, 0.25f);
            int each = Math.Clamp((int)(L / 40), 2, 5);
            // Three streams side by side, each a run of streaks driven down the line.
            for (int lane = -1; lane <= 1; lane++)
            {
                for (int k = 0; k < each; k++)
                {
                    float p = SkillFx.Frac(t * 2.6f + (k + 0.37f * (lane + 1)) / each), along = 12 + p * (L - 24), off = lane * W * 0.3f + 2 * Sin(p * 720 + k * 90);
                    Put(o, FxShape.Grains, m, lane == 0 ? 32 : 24, (int)(t * 14) + k + lane, dx * along - dy * off, dy * along + dx * off, left * Math.Min(1, 4 * p) * Math.Min(1, 4 - 4 * p), c.Dir);
                }
            }
            // Grains thrown off its sides, and dust where it runs out.
            for (int i = 0; i < 8; i++)
            {
                float p = SkillFx.Frac(t * 2.2f + Rnd(c.Seed, i, 28)), along = Rnd(c.Seed, i, 29) * L, side = (i & 1) == 0 ? 1 : -1, off = side * (W / 2 + 10 * p);
                Put(o, FxShape.Grains, m, 8, i, dx * along - dy * off, dy * along + dx * off + 5 * p * (1 - p), left * (1 - p), c.Dir + side * 50);
            }
            Dust(o, m, 4, t, 0.05f, 4, 12, c.Seed, c.Dir, 140, 0.5f);
            for (int i = 0; i < 3; i++)
            {
                float p = (t - 0.08f - 0.05f * i) / 0.45f;
                if (p < 0 || p >= 1) continue;
                float turn = c.Dir + (i - 1) * 40;
                PutLow(o, FxShape.Smoke, m, Even(10 + 6 * p), Math.Min(3, (int)(p * 4)), dx * L + Cos(turn) * 14 * Out(p), dy * L + Sin(turn) * 14 * Out(p), 0.8f * (1 - p));
            }
            return life;
        }

        static float Whirl(in BossContext c, float t, List<FxStamp> o)
        {
            const float life = 0.85f;
            var m = c.Ramp;
            float R = c.Size, left = U(life - t, 0, 0.3f), u = t / life;
            // The hole in the middle and the rings closing on it.
            Put(o, FxShape.Scorch, FxRamp.Smoke, SkillFxArt.Step4(R * (0.5f + 0.4f * Out(U(t, 0, 0.2f)))), 4 + (c.Seed & 3), 0, 0, 0.75f * left, 0, true);
            int turn = (int)(t * 12);
            Put(o, FxShape.Broken, m, SkillFxArt.Even(2 * R * (1 - 0.35f * u)), SkillFxArt.BrokenFrame(2, 5, turn), 0, 0, 0.9f * left, 0, true);
            Put(o, FxShape.Broken, m, SkillFxArt.Even(2 * R * (0.62f - 0.3f * u)), SkillFxArt.BrokenFrame(1, 4, turn + 3), 0, 0, 0.8f * left, 0, true);
            // Grains running round and in, faster as they near the middle.
            int grains = c.Big ? 16 : 12;
            for (int i = 0; i < grains; i++)
            {
                float p = SkillFx.Frac(t * 1.5f + (i * 7 % grains) / (float)grains), far = R * (1 - 0.82f * p), angle = i * 360f / grains + 380 * p * p + 60 * t;
                Put(o, FxShape.Grains, m, 10 + 4 * (i & 1), (int)(t * 14) + i, Cos(angle) * far, Sin(angle) * far * 0.9f, left * Math.Min(1, 5 * p) * (1 - 0.5f * p), angle + 112);
            }
            Dust(o, m, 6, t, 0.02f, R * 0.85f, 10, c.Seed, 0, 360, 0.5f);
            if (c.Big)
            {
                // The storm's front crashing down: sand thrown up off it as well.
                for (int i = 0; i < 8; i++)
                {
                    float p = t / (0.4f + 0.15f * Rnd(c.Seed, i, 30));
                    if (p >= 1) continue;
                    float dir = 30 + i * 17;
                    Put(o, FxShape.Grains, m, 12, i, Cos(dir) * R * 0.9f * Out(p), 4 + Sin(dir) * 34 * Out(p) - 26 * p * p, 1 - p * p, dir);
                }
            }
            return life;
        }

        static float SandBurst(in BossContext c, float t, List<FxStamp> o)
        {
            var m = c.Ramp;
            float R = c.Size;
            bool thrown = c.Width > 0;
            int count = thrown ? 9 : 8;
            for (int i = 0; i < count; i++)
            {
                float own = 0.38f + 0.16f * Rnd(c.Seed, i, 31), p = t / own;
                if (p >= 1) continue;
                float turn = (i + 0.5f) / count - 0.5f;
                if (thrown)
                {
                    float dir = c.Dir + turn * c.Width, far = R * (0.5f + 0.6f * Out(p));
                    Put(o, FxShape.Grains, m, 12 + 4 * (i & 1), (int)(t * 14) + i, Cos(dir) * far, Sin(dir) * far + 22 * p * (1 - p), 1 - p * p, dir);
                }
                else
                {
                    float dir = 90 + turn * 130;
                    Put(o, FxShape.Grains, m, 12 + 4 * (i & 1), (int)(t * 14) + i, Cos(dir) * R * 0.9f * Out(p), 3 + Sin(dir) * (26 + 10 * Rnd(c.Seed, i, 32)) * Out(p) - 24 * p * p, 1 - p * p, p < 0.55f ? dir : -dir);
                }
            }
            if (thrown) Dust(o, m, 5, t, 0.02f, R * 0.75f, 12, c.Seed, c.Dir, c.Width, 0.5f);
            else Dust(o, m, 5, t, 0.02f, R * 0.4f, R * 0.5f + 4, c.Seed, 0, 360, 0.5f);
            if (!thrown && R * 2.2f <= 160) FxLayers.Shock(o, m, t, 0, R, 0, 0, 0.3f, true);
            return 0.56f;
        }

        // ---- the tide serpent ----

        // A lump of water in shade (0 core to 4 darkest), wavering with the time.
        static void Water(List<FxStamp> o, float size, int shade, float t, int i, float x, float y, float alpha = 1, bool low = false)
        {
            int frame = 4 * shade + (((int)(t * 14) + i) & 3);
            if (low) PutLow(o, FxShape.Lump, FxRamp.Cold, Even(size), frame, x, y, alpha);
            else Put(o, FxShape.Lump, FxRamp.Cold, Even(size), frame, x, y, alpha);
        }

        static float Wave(in BossContext c, float t, List<FxStamp> o)
        {
            const float sweep = 0.14f;
            float R = c.Size, wide = Math.Clamp(c.Width, 20, 300), half = wide / 2, swept = U(t, 0, sweep);
            if (t < 0.3f)
            {
                // The wall of water: a pale crest over a deep body, swept across the wedge.
                float fade = t < 0.16f ? 1 : (0.3f - t) / 0.14f;
                int span = 10 * Math.Max(2, (int)Math.Round(wide * (0.25f + 0.75f * swept) / 10));
                float head = c.Dir - half + wide * swept;
                Put(o, FxShape.Smear, FxRamp.Cold, SkillFxArt.Even(R * 0.72f), span, 0, 0, fade * 0.8f, head - span / 2f);
                Put(o, FxShape.Smear, FxRamp.Mist, SkillFxArt.Even(R), span, 0, 0, fade, head - span / 2f);
            }
            // Foam left lying along the arc where the wave passed, thinning.
            for (int i = 0; i < 9; i++)
            {
                float turn = (i + 0.5f) / 9, p = (t - sweep * turn) / 0.55f;
                if (p < 0 || p >= 1) continue;
                float dir = c.Dir - half + wide * turn, far = R * (0.88f + 0.08f * Rnd(c.Seed, i, 41));
                Water(o, 6 + 3 * (i & 1), 1 + (i & 1), t, i, Cos(dir) * far, Sin(dir) * far * 0.95f, 0.9f * (1 - p), true);
            }
            // Drops flung on past it, arcing down.
            for (int i = 0; i < 8; i++)
            {
                float turn = (i + 0.5f) / 8, p = (t - sweep * turn) / (0.38f + 0.14f * Rnd(c.Seed, i, 42));
                if (p < 0 || p >= 1) continue;
                float dir = c.Dir - half + wide * turn, far = R * (0.9f + 0.45f * Out(p));
                Put(o, FxShape.Ember, FxRamp.Cold, 3 + (i & 1), 0, Cos(dir) * far, Sin(dir) * far + 24 * p * (1 - p), 1 - p * p, dir);
            }
            return 0.75f;
        }

        static float Torrent(in BossContext c, float t, List<FxStamp> o)
        {
            const float life = 0.62f;
            float L = Math.Max(24, c.Size), W = Math.Max(8, c.Width), dx = Cos(c.Dir), dy = Sin(c.Dir), left = U(life - t, 0, 0.25f);
            int each = Math.Clamp((int)(L / 36), 2, 6);
            // Three streams side by side, lumps of water running down them, the middle the fullest.
            for (int lane = -1; lane <= 1; lane++)
            {
                for (int k = 0; k < each; k++)
                {
                    float p = SkillFx.Frac(t * 2.4f + (k + 0.33f * (lane + 1)) / each), along = 8 + p * (L - 16), off = lane * W * 0.28f + 2 * Sin(p * 540 + k * 80);
                    float a = left * Math.Min(1, 4 * p) * Math.Min(1, 4 - 4 * p);
                    Water(o, lane == 0 ? 12 : 9, lane == 0 ? 0 : 1, t, k + lane + 1, dx * along - dy * off, dy * along + dx * off, a);
                }
            }
            // Spray off its sides.
            for (int i = 0; i < 8; i++)
            {
                float p = SkillFx.Frac(t * 2.2f + Rnd(c.Seed, i, 43)), along = Rnd(c.Seed, i, 44) * L, side = (i & 1) == 0 ? 1 : -1, off = side * (W / 2 + 9 * p);
                Put(o, FxShape.Ember, FxRamp.Cold, 3, 0, dx * along - dy * off, dy * along + dx * off + 6 * p * (1 - p), left * (1 - p), c.Dir + side * 60);
            }
            // A splash where it runs out.
            Puff(new BossContext { Ramp = FxRamp.Cold, Seed = c.Seed }, t - 0.06f, o, dx * L, dy * L, Math.Clamp(W * 0.8f, 10, 18), 1, false);
            return life;
        }

        static float Maelstrom(in BossContext c, float t, List<FxStamp> o)
        {
            const float life = 0.9f;
            float outer = c.Size, inner = c.Inner, mid = inner > 0 ? (inner + outer) / 2 : outer * 0.6f, left = U(life - t, 0, 0.3f);
            int turn = (int)(t * 12);
            // Broken rings turning at the band's edges (or closing on the middle of a whole circle).
            Put(o, FxShape.Broken, FxRamp.Cold, SkillFxArt.Even(2 * outer * (inner > 0 ? 1 : 1 - 0.2f * t)), SkillFxArt.BrokenFrame(2, 5, turn), 0, 0, 0.85f * left, 0, true);
            Put(o, FxShape.Broken, FxRamp.Mist, SkillFxArt.Even(2 * (inner > 0 ? inner : outer * 0.55f)), SkillFxArt.BrokenFrame(1, 4, turn + 3), 0, 0, 0.75f * left, 0, true);
            if (inner <= 0)
                Put(o, FxShape.Scorch, FxRamp.Smoke, SkillFxArt.Step4(outer * 0.6f), 4 + (c.Seed & 3), 0, 0, 0.45f * left, 0, true);
            // Foam running round the band.
            int foam = Math.Clamp((int)(mid / 6), 8, 14);
            for (int i = 0; i < foam; i++)
            {
                float p = SkillFx.Frac(t * 1.2f + (i * 5 % foam) / (float)foam), angle = i * 360f / foam + 300 * t + 40 * p;
                float far = inner > 0 ? mid + (outer - inner) * 0.35f * Sin(i * 97 + t * 400) : outer * (1 - 0.75f * p);
                Water(o, 6 + 2 * (i & 1), 1 + (i % 3), t, i, Cos(angle) * far, Sin(angle) * far * 0.9f, left * Math.Min(1, 5 * p), true);
            }
            // Drops thrown off the outside, up and out.
            for (int i = 0; i < 8; i++)
            {
                float p = (t - 0.04f * (i & 3)) / (0.4f + 0.15f * Rnd(c.Seed, i, 45));
                if (p < 0 || p >= 1) continue;
                float dir = i * 45 + 20, far = outer * (0.9f + 0.25f * Out(p));
                Put(o, FxShape.Ember, FxRamp.Cold, 3, 0, Cos(dir) * far, Sin(dir) * far * 0.9f + 20 * p * (1 - p), 1 - p * p, dir + 90);
            }
            return life;
        }

        static float Geyser(in BossContext c, float t, List<FxStamp> o)
        {
            float R = c.Size, high = (c.Big ? 56 : 40) * Math.Min(1.4f, 0.6f + R / 40);
            // Ripples spreading over the floor.
            for (int ring = 0; ring < 3; ring++)
            {
                float p = (t - 0.1f * ring) / 0.6f;
                if (p < 0 || p >= 1) continue;
                Put(o, FxShape.Edge, ring == 0 ? FxRamp.Mist : FxRamp.Cold, SkillFxArt.Even(2 * R * (0.3f + 0.9f * Out(p))), 1, 0, 0, 0.85f * (1 - p), 0, true);
            }
            // The column: up fast, standing a moment, falling back.
            float rise = t / 0.55f;
            if (rise < 1)
            {
                float up = high * (rise < 0.25f ? Out(rise / 0.25f) : rise < 0.6f ? 1 : 1 - (rise - 0.6f) / 0.4f);
                int lumps = c.Big ? 6 : 5;
                for (int i = 0; i < lumps; i++)
                {
                    float at = up * (i + 0.5f) / lumps, wob = 2 * Sin(t * 900 + i * 70);
                    Water(o, Math.Max(5, R * 0.7f * (1 - 0.45f * i / lumps)), i == lumps - 1 ? 0 : 1 + (i & 1), t, i, wob, 2 + at, 1 - 0.4f * rise * rise);
                }
                // A crown of foam on top.
                if (up > 6) Water(o, Math.Max(6, R * 0.55f), 0, t, 7, 0, 2 + up + 3, 0.9f);
            }
            // Rain falling back round it.
            int drops = c.Big ? 12 : 8;
            for (int i = 0; i < drops; i++)
            {
                float p = (t - 0.18f - 0.03f * (i % 4)) / (0.45f + 0.15f * Rnd(c.Seed, i, 46));
                if (p < 0 || p >= 1) continue;
                float dir = (i + 0.5f) * 360f / drops, go = R * (0.4f + 0.8f * Rnd(c.Seed, i, 47));
                Put(o, FxShape.Ember, FxRamp.Cold, 3, 0, Cos(dir) * go * Out(p), high * (1 - p) * (1 - p) + Sin(dir) * go * 0.5f * p, 1 - p * p * p, -90);
            }
            if (t < 0.5f) FxLayers.Light(o, FxRamp.Cold, R * 2.2f, 0.6f * (1 - t / 0.5f));
            return 0.85f;
        }

        // ---- the bosses with no effects of their own ----

        static float Element(in BossContext c, float t, List<FxStamp> o)
        {
            float life = 0;
            switch (c.Form)
            {
                case HazardForm.Donut:
                {
                    // Four places round the band.
                    float mid = (c.Inner + c.Size) / 2, r = Math.Min(28, (c.Size - c.Inner) * 0.45f);
                    for (int i = 0; i < 4; i++) life = Math.Max(life, Puff(c, t - 0.03f * i, o, Cos(45 + 90 * i) * mid, Sin(45 + 90 * i) * mid, r, i, false));
                    break;
                }
                case HazardForm.Cone:
                {
                    // Three across the wedge.
                    float wide = Math.Min(300, c.Width);
                    for (int i = 0; i < 3; i++)
                    {
                        float dir = c.Dir + (i - 1) * wide * 0.3f;
                        life = Math.Max(life, Puff(c, t - 0.03f * i, o, Cos(dir) * c.Size * 0.62f, Sin(dir) * c.Size * 0.62f, Math.Min(26, c.Size * 0.3f), i, false));
                    }
                    break;
                }
                case HazardForm.Band:
                {
                    // Up to four along the line, one after another.
                    int n = Math.Clamp((int)(c.Size / 48), 2, 4);
                    for (int i = 0; i < n; i++)
                    {
                        float along = c.Size * (i + 0.5f) / n;
                        life = Math.Max(life, Puff(c, t - 0.04f * i, o, Cos(c.Dir) * along, Sin(c.Dir) * along, Math.Clamp(c.Width * 0.7f, 10, 24), i, false));
                    }
                    break;
                }
                default:
                    life = Puff(c, t, o, 0, 0, c.Size, 0, true);
                    break;
            }
            return life + 0.12f;
        }

        // One place of an element's blow: whole (a circle's own) or one of several, which is lighter. Returns its life.
        static float Puff(in BossContext c, float t, List<FxStamp> o, float x, float y, float r, int n, bool whole)
        {
            if (t < 0) return 1;
            var m = c.Ramp;
            int seed = c.Seed + n * 13;
            switch (m)
            {
                case FxRamp.Fire:
                    // Lava: an explosion, and fire left burning.
                    if (whole)
                    {
                        FxLayers.Blast(o, t, r, m, seed, 10, r >= 28 ? 4 : 0, true, 1.0f, x, y);
                        SkillFx.Flames(o, 4, t, 0.25f, 1.1f, r, seed, x, y);
                        return 1.1f;
                    }
                    if (n < 3 && t < 0.4f) FxLayers.Light(o, m, r * 2.4f, 0.8f * (1 - t / 0.4f), x, y);
                    FxLayers.Ball(o, m, t, 0, r, seed, x, y);
                    FxLayers.Embers(o, m, 5, t, 0, r, seed, x, y);
                    SkillFx.Flames(o, 2, t, 0.2f, 0.95f, r, seed, x, y);
                    return 0.95f;
                case FxRamp.Violet:
                {
                    // The void: rings closing on the middle, scraps drawn in after them, a glint where they meet.
                    if (n < 3 && t < 0.45f) FxLayers.Light(o, m, r * 2.2f, 0.7f * (1 - t / 0.45f), x, y, 2);
                    for (int ring = 0; ring < (whole ? 3 : 2); ring++)
                    {
                        float p = (t - 0.07f * ring) / 0.36f;
                        if (p < 0 || p >= 1) continue;
                        Put(o, FxShape.Edge, m, SkillFxArt.Even(2 * r * (1.1f - 0.9f * p * p)), p < 0.5f ? 1 : 2, x, y, 0.5f + 0.5f * p, 0, true);
                    }
                    int scraps = whole ? 8 : 4;
                    for (int i = 0; i < scraps; i++)
                    {
                        float p = (t - 0.02f * i) / 0.4f;
                        if (p < 0 || p >= 1) continue;
                        float dir = (i + Rnd(seed, i, 33)) * 360f / scraps + 140 * p, far = r * 1.15f * (1 - p * p);
                        Put(o, FxShape.Lump, m, Even(7 - 4 * p), 4 * (2 + (i & 1)) + (i & 3), x + Cos(dir) * far, y + Sin(dir) * far * 0.9f, Math.Min(1, 3 * p));
                    }
                    if (In(t, 0.36f, 0.52f))
                    {
                        Put(o, FxShape.Glint, m, Odd(Math.Min(21, r * 0.9f)), 1, x, y, 1 - (t - 0.36f) / 0.16f);
                        Put(o, FxShape.Lump, m, Even(r * 0.45f), 16 + ((int)(t * 20) & 3), x, y);
                    }
                    return 0.52f;
                }
                case FxRamp.Cold:
                {
                    // Water: a column thrown up that falls back, drops flung out, ripples over the floor.
                    for (int ring = 0; ring < 2; ring++)
                    {
                        float p = (t - 0.12f * ring) / 0.5f;
                        if (p < 0 || p >= 1) continue;
                        Put(o, FxShape.Edge, m, SkillFxArt.Even(2 * r * (0.4f + 0.8f * Out(p))), 1, x, y, 0.8f * (1 - p), 0, true);
                    }
                    float rise = t / 0.42f;
                    if (rise < 1)
                    {
                        float high = (whole ? 30 : 20) * (float)Math.Sin(rise * Math.PI);
                        int lumps = whole ? 4 : 3;
                        for (int i = 0; i < lumps; i++)
                        {
                            float up = high * (i + 0.6f) / lumps;
                            Put(o, FxShape.Lump, m, Even(Math.Max(4, r * (whole ? 0.75f : 0.6f) * (1 - 0.6f * i / lumps))), 4 * (i == lumps - 1 ? 0 : 1 + (i & 1)) + ((int)(t * 14) + i & 3), x, y + 2 + up, 1 - 0.5f * rise * rise);
                        }
                    }
                    int drops = whole ? 8 : 4;
                    for (int i = 0; i < drops; i++)
                    {
                        float p = t / (0.42f + 0.14f * Rnd(seed, i, 34));
                        if (p >= 1) continue;
                        float dir = 25 + (i + 0.5f) * 130f / drops, go = r * (0.5f + 0.7f * Rnd(seed, i, 35));
                        float px = x + Cos(dir) * go * p, py = y + 3 + Sin(dir) * (28 + 10 * Rnd(seed, i, 36)) * p - 34 * p * p;
                        Put(o, FxShape.Ember, m, 3, 0, px, py, 1 - p * p, p < 0.5f ? dir : -dir);
                    }
                    return 0.62f;
                }
                case FxRamp.Bolt:
                {
                    // Lightning: a thick bolt down onto it, drawn anew three times, a star, sparks, a singed spot.
                    if (t < 0.16f)
                    {
                        if (n < 3) FxLayers.Light(o, m, r * 3, 1, x, y, 3);
                        Put(o, FxShape.Bolt, m, 72, 8 + 16 + ((int)(t / 0.04f) + n) % 6, x, y + 36, 1, 90);
                        Put(o, FxShape.Glint, m, Odd(Math.Min(25, r * 1.2f)), 1, x, y);
                    }
                    FxLayers.Scorch(o, t, 0.04f, 0.9f, r * 0.5f, seed, x, y);
                    FxLayers.Shock(o, m, t, 0, r, x, y);
                    FxLayers.Embers(o, m, whole ? 8 : 5, t, 0, r, seed, x, y);
                    return 0.9f;
                }
                default:
                {
                    // A wraith's: souls torn loose, curling up and away, a ring left thinning.
                    if (n < 3 && t < 0.45f) FxLayers.Light(o, m, r * 2.2f, 0.7f * (1 - t / 0.45f), x, y, 2);
                    SkillFx.Ring(o, m, t, 0, r, 2, 0.12f, 0.34f, x, y, true);
                    int souls = whole ? 8 : 4;
                    for (int i = 0; i < souls; i++)
                    {
                        float own = 0.5f + 0.2f * Rnd(seed, i, 37), p = (t - 0.02f * i) / own;
                        if (p < 0 || p >= 1) continue;
                        float dir = (i + 0.5f) * 360f / souls, far = r * (0.3f + 0.8f * Out(p)), sway = 4 * Sin(p * 500 + i * 70);
                        float px = x + Cos(dir) * far + sway, py = y + Sin(dir) * far * 0.6f + 22 * p;
                        Put(o, FxShape.Wisp, m, whole ? 5 : 4, (int)(t * 10) + i, px, py, 1 - p * p, 90 + (Cos(dir) > 0 ? -35 : 35) * (1 - p));
                    }
                    if (t < 0.08f) Put(o, FxShape.Glint, m, Odd(Math.Min(21, r)), 1, x, y);
                    return 0.72f;
                }
            }
        }
    }
}
