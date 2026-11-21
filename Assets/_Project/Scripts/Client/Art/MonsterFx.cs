using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Client
{
    // Which effect of an ordinary monster.
    enum MobFx : byte
    {
        // The warning on the ground at the mark's own place: a thin red rim at the real reach, the inside filling in.
        Mark,
        // The monster's own wind-up cue, round its feet.
        Tell,
        // Where the attack landed: the blow, then what thins out.
        Land,
        // What a blow left on the floor, for as long as it lies there.
        Pool,
        // A shot in flight.
        Shot,
        // One-offs: a shot off a wall, a returning shot turning, where a blink left from, a split, a charge into rock,
        // a call made, shards of the fallen, a trap set off, a blow turned by a guard.
        Bounce,
        Turn,
        BlinkOut,
        Split,
        Stagger,
        Rally,
        Deathburst,
        Trip,
        Guard,
        // Loops on a body: stars over a dazed one, flames at the feet of an empowered one, a mending line (from the
        // mender, Size its length) and the plus signs over whoever it fills.
        Stars,
        Empowered,
        Tether,
        Mended,
    }

    struct MobContext
    {
        // The attack (Mark, Tell, Land, Shot).
        public MonsterAttackShape Shape;
        // A mark's geometry, or which pool.
        public HazardShape Area;
        // The monster's colours: an element's ramp, or Plain with its own colour as Tint.
        public FxRamp Ramp;
        public uint Tint;
        // Radians.
        public float Dir;
        // In pixels: a circle's radius as it really reaches (the player's radius included), a cone's or a line's length.
        public float Size;
        // A cone's degrees, a line's width or a ring's inner radius in pixels.
        public float Width;
        // A mark's: 0 when laid, 1 when it lands. A pool's: how far faded in (or not yet out).
        public float Progress;
        // A wind-up's whole length in seconds.
        public float Life;
        public int Seed;
        // A trap that is armed; a returning shot standing still at its turn.
        public bool Armed;
        // A mark that harms nothing: where a blink will come out, the flash before the shards of the fallen.
        public bool Harmless;
    }

    // Ordinary monsters' warnings and effects as functions of time (2026-11-21, Docs/Combat.md 몬스터의 공격 시트,
    // Docs/SkillEffects.md): like SkillFx it keeps no state and says which pieces are where at a moment, so the game
    // (WorldView.MonsterFx) and the preview (ArtPreview --only monsterfx) draw the same. The rules: three parts
    // (gathering = the warning, the blow at its brightest and shortest, what thins out in 0.2 to 0.4 s); the rim at
    // the real reach; anything of an enemy's on the ground wears a thin red rim, its inside the element's own shape;
    // a monster's shot is its body's colour with a bright head and a dark rim; no more than Most pieces to an effect.
    static class MonsterFx
    {
        public const int Most = 24;
        const float Deg = 180f / (float)Math.PI;
        // The middle of a monster's body above its feet, in pixels.
        const float Body = 6;

        // The colours a monster's effects are drawn in: cold, fire or the void's violet, else greys tinted its own colour.
        public static FxRamp RampOf(string id, SkillTag element)
        {
            if (id != null && (id.StartsWith("void") || id.StartsWith("abyss"))) return FxRamp.Violet;
            if (element == SkillTag.Cold) return FxRamp.Cold;
            if (element == SkillTag.Fire) return FxRamp.Fire;
            return FxRamp.Plain;
        }

        public static uint TintOf(Rgba color) => 0x1000000u | (uint)color.R << 16 | (uint)color.G << 8 | color.B;

        // Adds this moment's pieces. False once a one-off is over (a Mark, Pool, Shot or loop never is).
        public static bool Stamps(MobFx fx, in MobContext c, float t, List<FxStamp> o)
        {
            if (t < 0) return true;
            int from = o.Count;
            float life = float.PositiveInfinity;
            switch (fx)
            {
                case MobFx.Mark: Mark(c, t, o); break;
                case MobFx.Tell: Tell(c, t, o); break;
                case MobFx.Land: life = Land(c, t, o); break;
                case MobFx.Pool: Pool(c, t, o); break;
                case MobFx.Shot: Shot(c, t, o); break;
                case MobFx.Bounce:
                    life = 0.25f;
                    if (t < 0.08f) Put(o, FxShape.Glint, FxRamp.Steel, 9, 2, 0, 0);
                    SkillFx.Fly(o, FxShape.Streak, c.Ramp, 4, 1, 4, t, 0, 0.2f, 2, 9, c.Seed, aimed: true);
                    break;
                case MobFx.Turn:
                    life = 0.3f;
                    if (SkillFx.Frac(t * 8) < 0.6f) Put(o, FxShape.Glint, c.Ramp, 9, 1, 0, 0);
                    Put(o, FxShape.Broken, c.Ramp, SkillFxArt.Even(10 + 14 * t / life), SkillFxArt.BrokenFrame(1, 4, (int)(t * 20)), 0, 0, 1 - t / life);
                    break;
                case MobFx.BlinkOut:
                    life = 0.35f;
                    Put(o, FxShape.Smoke, c.Ramp, 12, Math.Min(3, (int)(t / life * 4)), 0, Body, 0.9f);
                    Put(o, FxShape.Broken, c.Ramp, SkillFxArt.Even(18 * (1 - t / life) + 4), SkillFxArt.BrokenFrame(1, 5, (int)(t * 24)), 0, Body, 0.8f);
                    SkillFx.Fly(o, FxShape.Dot, c.Ramp, 2, 2, 5, t, 0, 0.3f, 2, 10, c.Seed, rise: 6, cy: Body);
                    break;
                case MobFx.Split:
                    life = 0.35f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float u = t / life;
                        float along = c.Dir * Deg + 90 * side;
                        Put(o, FxShape.Blob, c.Ramp, 8, 1, SkillFx.Cos(along) * 8 * SkillFx.Out(u), Body + SkillFx.Sin(along) * 8 * SkillFx.Out(u) + (float)Math.Sin(u * Math.PI) * 5, 1 - u * u);
                    }
                    SkillFx.Fly(o, FxShape.Dot, c.Ramp, 2, 1, 6, t, 0, 0.3f, 2, 11, c.Seed, hop: 6, cy: 2);
                    break;
                case MobFx.Stagger:
                    life = 0.3f;
                    SkillFx.Fly(o, FxShape.Chunk, FxRamp.Dust, 3, 0, 5, t, 0, 0.28f, 2, 12, c.Seed, hop: 5, cy: Body, cone: 120, toward: c.Dir * Deg + 180);
                    SkillFx.Ring(o, FxRamp.Dust, t, 0, 10, 2, cy: Body);
                    if (t < 0.08f) Put(o, FxShape.Glint, FxRamp.Steel, 11, 2, SkillFx.Cos(c.Dir * Deg) * 6, Body + SkillFx.Sin(c.Dir * Deg) * 6);
                    break;
                case MobFx.Rally:
                    life = 0.4f;
                    SkillFx.Ring(o, FxRamp.Violet, t, 0, c.Size, 2, life: 0.36f, ground: true);
                    SkillFx.Float(o, FxShape.Dot, FxRamp.Violet, 2, 2, 6, t, 0, life, 10, 14, c.Seed, cy: 4);
                    break;
                case MobFx.Deathburst:
                    life = 0.3f;
                    if (t < 0.08f) Put(o, FxShape.Glint, FxRamp.Steel, 13, 2, 0, Body);
                    SkillFx.Ring(o, c.Ramp, t, 0, 9, 2, cy: Body);
                    SkillFx.Fly(o, FxShape.Shard, c.Ramp, 6, 0, 6, t, 0, 0.26f, 3, 14, c.Seed, cy: Body, aimed: true);
                    break;
                case MobFx.Trip:
                    life = 0.2f;
                    if (t < 0.1f) Put(o, FxShape.Glint, FxRamp.Danger, 11, 1, 0, 0);
                    SkillFx.Ring(o, FxRamp.Danger, t, 0, c.Size, 2, grow: 0.08f, life: 0.2f, ground: true);
                    break;
                case MobFx.Guard:
                    life = 0.2f;
                    if (t < 0.07f) Put(o, FxShape.Glint, FxRamp.Steel, 9, 2, SkillFx.Cos(c.Dir * Deg) * 6, Body + SkillFx.Sin(c.Dir * Deg) * 6);
                    SkillFx.Fly(o, FxShape.Streak, FxRamp.Steel, 4, 1, 3, t, 0, 0.18f, 4, 10, c.Seed, cx: SkillFx.Cos(c.Dir * Deg) * 6, cy: Body + SkillFx.Sin(c.Dir * Deg) * 6,
                        aimed: true, cone: 100, toward: c.Dir * Deg);
                    break;
                case MobFx.Stars:
                    for (int i = 0; i < 3; i++)
                    {
                        float angle = t * 220 + i * 120;
                        Put(o, FxShape.Glint, FxRamp.Bolt, 5, SkillFx.Sin(angle) > 0 ? 1 : 0, SkillFx.Cos(angle) * 6, 17 + SkillFx.Sin(angle) * 2);
                    }
                    break;
                case MobFx.Empowered:
                    for (int i = 0; i < 2; i++)
                        Put(o, FxShape.Flame, FxRamp.Danger, 6, (int)(t * 9 + i * 2) & 3, i == 0 ? -5 : 5, 3, 0.9f);
                    Put(o, FxShape.Broken, FxRamp.Danger, 14, SkillFxArt.BrokenFrame(1, 4, (int)(t * 6)), 0, 0, 0.6f, 0, true);
                    break;
                case MobFx.Tether:
                {
                    // A dashed line walking from the mender to whoever it fills, in two pieces when it is long.
                    int pieces = c.Size > 120 ? 2 : 1;
                    float each = c.Size / pieces;
                    for (int i = 0; i < pieces; i++)
                    {
                        float middle = each * (i + 0.5f);
                        Put(o, FxShape.Tether, FxRamp.Spirit, SkillFxArt.Step4(each), (int)(t * 14) & 3, SkillFx.Cos(c.Dir * Deg) * middle, Body + SkillFx.Sin(c.Dir * Deg) * middle, 0.9f, c.Dir * Deg);
                    }
                    Put(o, FxShape.Glint, FxRamp.Spirit, 7, (int)(t * 6) & 1, 0, Body, 0.9f);
                    break;
                }
                case MobFx.Mended:
                    for (int i = 0; i < 2; i++)
                    {
                        float u = SkillFx.Frac(t * 1.2f + i * 0.5f);
                        Put(o, FxShape.Plus, FxRamp.Heal, 5, 0, i == 0 ? -4 : 4, 12 + 8 * u, 1 - u);
                    }
                    break;
            }
            // Greys take the monster's own colour.
            for (int i = from; c.Tint != 0 && i < o.Count; i++)
            {
                if (o[i].Ramp != FxRamp.Plain) continue;
                var stamp = o[i];
                stamp.Tint = c.Tint;
                o[i] = stamp;
            }
            return t < life;
        }

        static void Put(List<FxStamp> o, FxShape shape, FxRamp ramp, int size, int frame, float x, float y, float alpha = 1, float angle = 0, bool ground = false) =>
            SkillFx.Put(o, shape, ramp, size, frame, x, y, alpha, angle, ground);

        static bool Dash(MonsterAttackShape shape) => MonsterAttackSheet.TellOf(shape) == Core.Tell.Dash;

        // ---- The warning on the ground ----

        static void Mark(in MobContext c, float t, List<FxStamp> o)
        {
            // A path or a place to come out at is yellow; anything that will hurt is red.
            var ramp = Dash(c.Shape) ? FxRamp.Bolt : FxRamp.Danger;
            float p = Math.Clamp(c.Progress, 0, 1);
            // The rim beats faster as the blow nears.
            float beat = 0.72f + 0.28f * (float)Math.Sin(t * (8 + 26 * p));
            float dir = c.Dir * Deg;
            switch (c.Area)
            {
                case HazardShape.Cone:
                {
                    float half = c.Width / 2;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float a = dir + half * side;
                        Put(o, FxShape.Tether, ramp, SkillFxArt.Step4(c.Size), 0, SkillFx.Cos(a) * c.Size / 2, SkillFx.Sin(a) * c.Size / 2, beat, a, true);
                    }
                    int dots = Math.Clamp((int)(c.Width / 12), 3, 12);
                    for (int i = 0; i <= dots; i++)
                    {
                        float a = dir - half + c.Width * i / dots;
                        Put(o, FxShape.Dot, ramp, 2, 2, SkillFx.Cos(a) * c.Size, SkillFx.Sin(a) * c.Size, beat, 0, true);
                        // The inside fills outward, faintly.
                        if (i % 2 == 0 && p > 0.1f) Put(o, FxShape.Dot, ramp, 2, 1, SkillFx.Cos(a) * c.Size * p, SkillFx.Sin(a) * c.Size * p, 0.5f, 0, true);
                    }
                    break;
                }
                case HazardShape.Line:
                {
                    float side = c.Width / 2;
                    int frame = Dash(c.Shape) ? (int)(t * 16) & 3 : 0;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float x = SkillFx.Cos(dir) * c.Size / 2 - SkillFx.Sin(dir) * side * s, y = SkillFx.Sin(dir) * c.Size / 2 + SkillFx.Cos(dir) * side * s;
                        Put(o, FxShape.Tether, ramp, SkillFxArt.Step4(c.Size), frame, x, y, beat, dir, true);
                    }
                    for (int i = -1; i <= 1; i++)
                        Put(o, FxShape.Dot, ramp, 2, 2, SkillFx.Cos(dir) * c.Size - SkillFx.Sin(dir) * side * i, SkillFx.Sin(dir) * c.Size + SkillFx.Cos(dir) * side * i, beat, 0, true);
                    // A thin line runs out along it: the aim of a beam, the way of a rush.
                    float reach = Math.Max(4, c.Size * (Dash(c.Shape) ? 1 : p));
                    Put(o, FxShape.Streak, ramp, SkillFxArt.Step4(reach), 1, SkillFx.Cos(dir) * reach / 2, SkillFx.Sin(dir) * reach / 2, 0.5f, dir, true);
                    break;
                }
                case HazardShape.Ring:
                    Put(o, FxShape.Edge, ramp, SkillFxArt.Even(2 * c.Size), 1, 0, 0, beat, 0, true);
                    Put(o, FxShape.Edge, ramp, SkillFxArt.Even(2 * c.Width), 1, 0, 0, beat, 0, true);
                    Put(o, FxShape.Broken, ramp, SkillFxArt.Even(2 * (c.Width + (c.Size - c.Width) * p)), SkillFxArt.BrokenFrame(1, 6, (int)(t * 6)), 0, 0, 0.5f, 0, true);
                    break;
                default:
                    if (c.Shape == MonsterAttackShape.Blink)
                    {
                        // Where it will come out: a turning yellow ring and a wisp of what it is made of.
                        Put(o, FxShape.Broken, FxRamp.Bolt, SkillFxArt.Even(2 * c.Size), SkillFxArt.BrokenFrame(1, 5, (int)(t * 14)), 0, 0, beat, 0, true);
                        Put(o, FxShape.Smoke, c.Ramp, 8, 3 - Math.Min(3, (int)(p * 4)), 0, Body, 0.5f * p);
                        break;
                    }
                    Put(o, FxShape.Edge, ramp, SkillFxArt.Even(2 * c.Size), 1, 0, 0, beat, 0, true);
                    if (c.Harmless)
                    {
                        // The flash of the fallen before its shards.
                        if (SkillFx.Frac(t * (6 + 10 * p)) < 0.5f) Put(o, FxShape.Glint, FxRamp.Steel, 9, 1, 0, 3);
                        break;
                    }
                    if (p > 0.08f) Put(o, FxShape.Broken, ramp, SkillFxArt.Even(2 * c.Size * p), SkillFxArt.BrokenFrame(1, 6, (int)(t * 6)), 0, 0, 0.5f, 0, true);
                    switch (c.Shape)
                    {
                        case MonsterAttackShape.Lob:
                        case MonsterAttackShape.Delayed:
                        case MonsterAttackShape.Leap:
                            // What is coming down throws a growing shadow, and a thrown thing is seen falling at the end.
                            Put(o, FxShape.Shadow, FxRamp.Smoke, Math.Max(4, (int)(c.Size * 1.3f * (0.3f + 0.7f * p))), 0, 0, 0, 0.7f, 0, true);
                            if (c.Shape != MonsterAttackShape.Leap && p > 0.55f)
                            {
                                float u = (p - 0.55f) / 0.45f;
                                Put(o, FxShape.Shard, c.Ramp, 9, 1, 0, 60 * (1 - u * u) + 4, 1, -90);
                            }
                            break;
                        case MonsterAttackShape.Burrow:
                        case MonsterAttackShape.Ambush:
                            for (int i = 0; i < 4; i++)
                                Put(o, FxShape.Chunk, FxRamp.Dust, 3, i, (SkillFx.Rnd(c.Seed, i, 1) - 0.5f) * c.Size, (SkillFx.Rnd(c.Seed, i, 2) - 0.5f) * c.Size * 0.6f + (float)Math.Abs(Math.Sin(t * 14 + i)) * 3, 0.9f);
                            break;
                    }
                    if (c.Area == HazardShape.Pull)
                    {
                        // Motes drawn round and in toward the middle.
                        for (int i = 0; i < 8; i++)
                        {
                            float u = SkillFx.Frac(t * 0.6f + i / 8f);
                            float a = i * 45 + t * 150;
                            Put(o, FxShape.Dot, c.Ramp, 2, u > 0.6f ? 3 : 2, SkillFx.Cos(a) * c.Size * (1 - u), SkillFx.Sin(a) * c.Size * (1 - u), 0.4f + 0.6f * u, 0, true);
                        }
                        Put(o, FxShape.Glint, c.Ramp, 7, (int)(t * 8) & 1, 0, 0, 0.5f + 0.5f * p);
                    }
                    else if (c.Area == HazardShape.Mine)
                    {
                        // A rune drawn on the floor: faint until it is armed.
                        Put(o, FxShape.Rune, FxRamp.Danger, SkillFxArt.Even(2 * c.Size * 0.8f), c.Armed ? (int)(t * 3) & 3 : 0, 0, 0, c.Armed ? 0.9f : 0.35f, 0, true);
                    }
                    break;
            }
        }

        // ---- The monster's own cue while it winds up ----

        static void Tell(in MobContext c, float t, List<FxStamp> o)
        {
            float p = c.Life > 0 ? Math.Clamp(t / c.Life, 0, 1) : 1;
            float dir = c.Dir * Deg;
            switch (MonsterAttackSheet.TellOf(c.Shape))
            {
                case Core.Tell.Dash:
                    if (c.Shape == MonsterAttackShape.Blink)
                    {
                        Put(o, FxShape.Broken, c.Ramp, SkillFxArt.Even(22 * (1 - p) + 6), SkillFxArt.BrokenFrame(1, 5, (int)(t * 20)), 0, Body, 0.9f);
                        Put(o, FxShape.Smoke, c.Ramp, 10, Math.Min(3, (int)(p * 4)), 0, Body, 0.6f);
                        break;
                    }
                    // Dust kicked up behind it as it paws the ground.
                    for (int i = 0; i < 3; i++)
                    {
                        float u = SkillFx.Frac(t * 3 + i / 3f);
                        float a = dir + 180 + (i - 1) * 25;
                        Put(o, FxShape.Smoke, FxRamp.Dust, 6, Math.Min(3, (int)(u * 4)), SkillFx.Cos(a) * (5 + 8 * u), 1 + SkillFx.Sin(a) * (5 + 8 * u) + 3 * u, 0.8f * (1 - u));
                    }
                    break;
                case Core.Tell.Barrage:
                    // A quick flicker where the shot will leave from.
                    if (Math.Sin(t * 40) > 0) Put(o, FxShape.Glint, FxRamp.Steel, 7 + (int)(4 * p), 1, SkillFx.Cos(dir) * 7, Body + SkillFx.Sin(dir) * 7);
                    for (int i = 0; i < 2; i++)
                    {
                        float a = t * 500 + i * 180;
                        Put(o, FxShape.Dot, c.Ramp, 2, 3, SkillFx.Cos(dir) * 7 + SkillFx.Cos(a) * 5 * (1 - p), Body + SkillFx.Sin(dir) * 7 + SkillFx.Sin(a) * 5 * (1 - p));
                    }
                    break;
                case Core.Tell.Call:
                    Put(o, FxShape.Broken, FxRamp.Violet, 22, SkillFxArt.BrokenFrame(2, 5, (int)(t * 8)), 0, 0, 0.9f, 0, true);
                    Put(o, FxShape.Rune, FxRamp.Violet, 16, (int)(t * 3) & 3, 0, 0, 0.3f + 0.5f * p, 0, true);
                    SkillFx.Float(o, FxShape.Dot, FxRamp.Violet, 2, 2, 4, SkillFx.Frac(t * 1.5f), 0, 1, 9, 14, c.Seed, cy: 3);
                    break;
                default:
                    switch (c.Shape)
                    {
                        case MonsterAttackShape.Leap:
                            // It gathers itself: dust at its feet, and off the ground as it goes up.
                            for (int i = -1; i <= 1; i += 2)
                                Put(o, FxShape.Smoke, FxRamp.Dust, 6, Math.Min(3, (int)(p * 4)), i * (5 + 3 * p), 1, 0.7f);
                            break;
                        case MonsterAttackShape.Strike:
                        case MonsterAttackShape.Sweep:
                        case MonsterAttackShape.Thrust:
                        case MonsterAttackShape.Beam:
                            // A glint where the blow will come from, growing.
                            Put(o, FxShape.Glint, c.Shape == MonsterAttackShape.Beam ? FxRamp.Steel : c.Ramp, 5 + 2 * (int)(3 * p), p > 0.7f ? 2 : 1,
                                SkillFx.Cos(dir) * 6, Body + SkillFx.Sin(dir) * 6, 0.6f + 0.4f * p);
                            break;
                        case MonsterAttackShape.Lob:
                        case MonsterAttackShape.Delayed:
                        case MonsterAttackShape.Mine:
                        case MonsterAttackShape.Push:
                            // What it will throw gathers over its head.
                            Put(o, FxShape.Blob, c.Ramp, 4 + 2 * (int)(3 * p), 1, 0, 14 + 2 * p);
                            SkillFx.Gather(o, c.Ramp, SkillFx.Frac(t * 2.5f) * 0.4f, 0.4f, 12, 4, c.Seed, 0, 14);
                            break;
                        case MonsterAttackShape.Burrow:
                        case MonsterAttackShape.Ambush:
                            SkillFx.Fly(o, FxShape.Chunk, FxRamp.Dust, 3, 0, 6, t, 0, 0.3f, 2, 10, c.Seed, hop: 7);
                            break;
                        default:
                            // A ring of itself, a pull, a blast: lines drawn in to its middle, again and again.
                            SkillFx.Gather(o, c.Ramp, SkillFx.Frac(t * 2.2f) * 0.45f, 0.45f, Math.Min(c.Size, 30), 7, c.Seed, 0, Body);
                            if (c.Shape == MonsterAttackShape.Explode && SkillFx.Frac(t * (4 + 10 * p)) < 0.5f) Put(o, FxShape.Glint, FxRamp.Steel, 9, 1, 0, Body);
                            break;
                    }
                    break;
            }
        }

        // ---- Where it landed ----

        static float Land(in MobContext c, float t, List<FxStamp> o)
        {
            float dir = c.Dir * Deg;
            float r = c.Size;
            switch (c.Shape)
            {
                case MonsterAttackShape.Strike:
                case MonsterAttackShape.Sweep:
                {
                    // The cut itself, then sparks off it.
                    float life = 0.3f;
                    int span = (int)Math.Clamp(c.Width, 40, 170);
                    Put(o, FxShape.Crescent, c.Ramp, (int)Math.Max(6, r * 0.8f), span, 0, 0, t < 0.08f ? 1 : 1 - (t - 0.08f) / 0.2f, dir);
                    SkillFx.Fly(o, FxShape.Streak, c.Ramp, 4, 1, c.Shape == MonsterAttackShape.Sweep ? 6 : 3, t, 0.02f, 0.24f, r * 0.6f, r * 1.1f, c.Seed, aimed: true, cone: c.Width * 0.8f, toward: dir);
                    if (c.Shape == MonsterAttackShape.Sweep) SkillFx.Float(o, FxShape.Smoke, FxRamp.Dust, 6, 2, 2, t, 0.08f, life, r * 0.5f, 5, c.Seed, cx: SkillFx.Cos(dir) * r * 0.5f, cy: SkillFx.Sin(dir) * r * 0.5f);
                    return life;
                }
                case MonsterAttackShape.Thrust:
                case MonsterAttackShape.Beam:
                case MonsterAttackShape.Charge:
                case MonsterAttackShape.Swoop:
                {
                    // A streak the whole way, bright for a moment; a beam has a white heart and leaves dust falling.
                    float life = 0.35f;
                    float alpha = t < 0.1f ? 1 : 1 - (t - 0.1f) / 0.18f;
                    float mx = SkillFx.Cos(dir) * r / 2, my = SkillFx.Sin(dir) * r / 2;
                    Put(o, FxShape.Streak, c.Ramp, SkillFxArt.Step4(r), 3, mx, my, alpha, dir);
                    if (c.Shape == MonsterAttackShape.Beam) Put(o, FxShape.Streak, FxRamp.Steel, SkillFxArt.Step4(r), 2, mx, my, alpha, dir);
                    for (int i = 0; i < 5; i++)
                    {
                        float along = r * (i + 0.5f) / 5, u = SkillFx.U(t, 0.06f + 0.02f * i, life);
                        if (u <= 0 || u >= 1) continue;
                        Put(o, FxShape.Dot, c.Ramp, 2, 2, SkillFx.Cos(dir) * along + (SkillFx.Rnd(c.Seed, i, 1) - 0.5f) * 6, SkillFx.Sin(dir) * along - 5 * u + (SkillFx.Rnd(c.Seed, i, 2) - 0.5f) * 6, 1 - u);
                    }
                    return life;
                }
                case MonsterAttackShape.Leap:
                {
                    // It comes down: a flash of itself, drops flung up and out, a ring, a puddle that sinks away.
                    float life = 0.4f;
                    if (t < 0.09f) Put(o, FxShape.Blob, c.Ramp, SkillFxArt.Even(r * 1.2f), t < 0.04f ? 0 : 1, 0, 2);
                    SkillFx.Ring(o, c.Ramp, t, 0, r, 2, ground: true);
                    SkillFx.Fly(o, FxShape.Dot, c.Ramp, 2, 2, 6, t, 0.02f, 0.3f, 3, r, c.Seed, hop: 8);
                    Put(o, FxShape.Smoke, c.Ramp, SkillFxArt.Even(r * 0.9f), Math.Min(3, (int)(t / life * 4)), 0, 0, 0.5f * (1 - t / life), 0, true);
                    return life;
                }
                case MonsterAttackShape.Nova:
                {
                    // Spikes stand up all round the rim, then mist.
                    float life = 0.45f;
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * 45 + 22;
                        float rise = SkillFx.Out(SkillFx.U(t, 0, 0.1f));
                        Put(o, FxShape.Shard, c.Ramp, 6 + (int)(5 * rise), 0, SkillFx.Cos(a) * r * 0.86f, SkillFx.Sin(a) * r * 0.86f, t < 0.2f ? 1 : 1 - (t - 0.2f) / 0.2f, a);
                    }
                    SkillFx.Ring(o, c.Ramp, t, 0, r, 2, ground: true);
                    if (t < 0.07f) Put(o, FxShape.Glint, FxRamp.Steel, 13, 2, 0, Body);
                    SkillFx.Float(o, FxShape.Smoke, c.Ramp == FxRamp.Cold ? FxRamp.Mist : FxRamp.Smoke, 8, 2, 5, t, 0.1f, life, r * 0.7f, 5, c.Seed);
                    return life;
                }
                case MonsterAttackShape.Pull:
                {
                    // It caves in on itself: the rim rushes to the middle and a point of light goes out.
                    float life = 0.35f;
                    float u = SkillFx.U(t, 0, 0.16f);
                    if (u < 1) Put(o, FxShape.Edge, c.Ramp, SkillFxArt.Even(2 * r * (1 - u) + 4), 2, 0, 0, 1, 0, true);
                    if (t > 0.1f && t < 0.2f) Put(o, FxShape.Glint, FxRamp.Steel, 15, 2, 0, 2);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * 45 + c.Seed % 45;
                        Put(o, FxShape.Streak, c.Ramp, 6, 1, SkillFx.Cos(a) * r * (1 - u) * 0.9f, SkillFx.Sin(a) * r * (1 - u) * 0.9f, 1 - u, a + 180);
                    }
                    SkillFx.Float(o, FxShape.Dot, c.Ramp, 2, 2, 6, t, 0.16f, life, 8, 10, c.Seed);
                    return life;
                }
                case MonsterAttackShape.Push:
                {
                    // A column thrown up, and spray.
                    float life = 0.35f;
                    for (int i = -1; i <= 1; i++)
                        Put(o, FxShape.Streak, c.Ramp, SkillFxArt.Step4(10 + 8 * SkillFx.Out(SkillFx.U(t, 0, 0.1f))), 2, i * r * 0.4f, 8, t < 0.12f ? 1 : 1 - (t - 0.12f) / 0.2f, 90);
                    SkillFx.Ring(o, c.Ramp, t, 0, r, 1, ground: true);
                    SkillFx.Fly(o, FxShape.Dot, c.Ramp, 2, 3, 6, t, 0.03f, 0.3f, 3, r, c.Seed, hop: 9);
                    return life;
                }
                case MonsterAttackShape.Slam:
                case MonsterAttackShape.Burrow:
                case MonsterAttackShape.Ambush:
                {
                    // The ground takes it: a ring, cracks, rubble thrown up.
                    float life = 0.4f;
                    SkillFx.Ring(o, FxRamp.Dust, t, 0, r, 2, ground: true);
                    for (int i = 0; i < 3; i++)
                        Put(o, FxShape.Crack, FxRamp.Smoke, SkillFxArt.Step4(r * 0.7f), i, SkillFx.Cos(i * 120 + 30) * r * 0.35f, SkillFx.Sin(i * 120 + 30) * r * 0.35f, 1 - t / life, i * 120 + 30, true);
                    SkillFx.Fly(o, FxShape.Chunk, FxRamp.Dust, 3, 0, 7, t, 0, 0.32f, 3, r, c.Seed, hop: 9);
                    if (t < 0.08f) Put(o, FxShape.Blob, c.Ramp, SkillFxArt.Even(r * 0.9f), 0, 0, 3);
                    return life;
                }
                default:
                {
                    // Something thrown or left has gone off: a ball from a bright heart to dark scraps, chips, smoke.
                    float life = 0.42f;
                    Put(o, FxShape.Blob, c.Ramp, SkillFxArt.Even(Math.Min(44, r * 1.5f)), Math.Min(5, (int)(t / 0.3f * 6)), 0, 3, t < 0.3f ? 1 : 1 - (t - 0.3f) / 0.12f);
                    SkillFx.Ring(o, c.Ramp, t, 0, r, 2, ground: true);
                    SkillFx.Fly(o, c.Ramp == FxRamp.Cold ? FxShape.Shard : FxShape.Dot, c.Ramp, c.Ramp == FxRamp.Cold ? 5 : 2, c.Ramp == FxRamp.Cold ? 0 : 2, 6, t, 0.02f, 0.3f, 3, r * 1.1f, c.Seed, hop: 6, aimed: c.Ramp == FxRamp.Cold);
                    if (c.Shape == MonsterAttackShape.Mine || c.Shape == MonsterAttackShape.Explode)
                    {
                        for (int i = 0; i < 4; i++)
                            Put(o, FxShape.Flame, c.Ramp, 8 + 2 * (i & 1), (int)(t * 12 + i) & 3, SkillFx.Cos(i * 90 + 45) * r * 0.45f, SkillFx.Sin(i * 90 + 45) * r * 0.45f + 4, 1 - t / life);
                    }
                    SkillFx.Float(o, FxShape.Smoke, c.Ramp == FxRamp.Cold ? FxRamp.Mist : FxRamp.Smoke, 8, 2, 3, t, 0.12f, life, r * 0.5f, 6, c.Seed);
                    return life;
                }
            }
        }

        // ---- What lies on the floor ----

        static void Pool(in MobContext c, float t, List<FxStamp> o)
        {
            float fade = Math.Clamp(c.Progress, 0, 1);
            float r = c.Size;
            // An enemy's, so it wears the thin red rim whatever it is made of; a cloud that only fills monsters does not.
            if (c.Area != HazardShape.Balm) Put(o, FxShape.Edge, FxRamp.Danger, SkillFxArt.Even(2 * r), 1, 0, 0, 0.75f * fade, 0, true);
            int count = Math.Clamp((int)(r / 4) + 2, 3, 7);
            switch (c.Area)
            {
                case HazardShape.Fire:
                    // Low flames that flicker, and an ember or two going up.
                    for (int i = 0; i < count; i++)
                    {
                        float a = SkillFx.Rnd(c.Seed, i, 1) * 360, d = r * 0.7f * (float)Math.Sqrt(SkillFx.Rnd(c.Seed, i, 2));
                        Put(o, FxShape.Flame, FxRamp.Fire, 6 + 2 * (i & 1), (int)(t * 9 + i * 1.7f) & 3, SkillFx.Cos(a) * d, SkillFx.Sin(a) * d * 0.7f + 3, fade);
                    }
                    Put(o, FxShape.Scorch, FxRamp.Fire, SkillFxArt.Even(r * 1.4f), 0, 0, 0, 0.5f * fade, 0, true);
                    SkillFx.Float(o, FxShape.Dot, FxRamp.Fire, 1, 3, 3, SkillFx.Frac(t * 0.9f), 0, 1, r * 0.6f, 12, c.Seed, alpha: fade);
                    break;
                case HazardShape.Ice:
                    // Crystals lying where they froze, one glinting now and then.
                    for (int i = 0; i < count; i++)
                    {
                        float a = SkillFx.Rnd(c.Seed, i, 3) * 360, d = r * 0.72f * (float)Math.Sqrt(SkillFx.Rnd(c.Seed, i, 4));
                        Put(o, FxShape.Shard, FxRamp.Cold, 5 + 2 * (i % 3), 0, SkillFx.Cos(a) * d, SkillFx.Sin(a) * d * 0.7f, 0.9f * fade, a * 3, true);
                    }
                    if (SkillFx.Frac(t * 0.8f) < 0.25f) Put(o, FxShape.Glint, FxRamp.Cold, 7, 1, SkillFx.Cos(t * 40) * r * 0.3f, 2, fade);
                    break;
                case HazardShape.Spore:
                    // Motes drifting up out of it.
                    Put(o, FxShape.Smoke, FxRamp.Violet, SkillFxArt.Even(r * 1.2f), 2 + ((int)(t * 2) & 1), 0, 1, 0.35f * fade, 0, true);
                    SkillFx.Float(o, FxShape.Dot, FxRamp.Violet, 2, 2, count + 2, SkillFx.Frac(t * 0.7f), 0, 1, r * 0.8f, 11, c.Seed, alpha: fade);
                    SkillFx.Float(o, FxShape.Dot, FxRamp.Violet, 1, 3, 3, SkillFx.Frac(t * 0.7f + 0.5f), 0, 1, r * 0.8f, 13, c.Seed + 7, alpha: fade);
                    break;
                case HazardShape.Sand:
                    // Grains turning round and down to its middle.
                    for (int i = 0; i < count + 2; i++)
                    {
                        float u = SkillFx.Frac(t * 0.5f + i / (float)(count + 2));
                        float a = i * 360f / (count + 2) + t * 200;
                        Put(o, FxShape.Dot, FxRamp.Dust, 2, u > 0.5f ? 0 : 2, SkillFx.Cos(a) * r * 0.85f * (1 - u), SkillFx.Sin(a) * r * 0.6f * (1 - u), fade, 0, true);
                    }
                    break;
                case HazardShape.Balm:
                    // A soft green cloud: it harms no one, so no red.
                    Put(o, FxShape.Smoke, FxRamp.Heal, SkillFxArt.Even(r * 1.3f), 2, 0, 2, 0.3f * fade, 0, true);
                    SkillFx.Float(o, FxShape.Dot, FxRamp.Heal, 2, 2, count + 2, SkillFx.Frac(t * 0.6f), 0, 1, r * 0.8f, 12, c.Seed, alpha: fade);
                    for (int i = 0; i < 2; i++)
                    {
                        float u = SkillFx.Frac(t * 0.8f + i * 0.5f);
                        Put(o, FxShape.Plus, FxRamp.Heal, 3, 0, (i == 0 ? -1 : 1) * r * 0.35f, 4 + 9 * u, fade * (1 - u));
                    }
                    break;
            }
        }

        // ---- A shot: the body's colour, a bright head, a dark rim ----

        static void Shot(in MobContext c, float t, List<FxStamp> o)
        {
            float dir = c.Dir * Deg;
            float bx = -SkillFx.Cos(dir), by = -SkillFx.Sin(dir);
            switch (c.Shape)
            {
                case MonsterAttackShape.Boomerang:
                    // An orb with stars strung out behind it; it blinks where it stands before it comes back.
                    if (c.Armed && SkillFx.Frac(t * 10) < 0.4f) Put(o, FxShape.Glint, c.Ramp, 13, 2, 0, 0);
                    Put(o, FxShape.Dot, c.Ramp, 7, 0, 0, 0, 1, 45);
                    Put(o, FxShape.Blob, c.Ramp, 8, 1, 0, 0);
                    for (int i = 1; i <= 3 && !c.Armed; i++)
                        Put(o, FxShape.Glint, c.Ramp, 5, (i + (int)(t * 12)) & 1, bx * i * 5, by * i * 5, 1 - i * 0.25f);
                    break;
                case MonsterAttackShape.Bounce:
                    // A hard diamond of light.
                    Put(o, FxShape.Dot, c.Ramp, 7, 0, 0, 0, 1, 45);
                    Put(o, FxShape.Dot, c.Ramp, 5, 2, 0, 0, 1, 45);
                    Put(o, FxShape.Glint, FxRamp.Steel, 5, 1, -bx * 2, -by * 2);
                    for (int i = 1; i <= 2; i++) Put(o, FxShape.Dot, c.Ramp, 3 - i, 1, bx * i * 4, by * i * 4, 1 - i * 0.3f, 45);
                    break;
                default:
                    // A thorn or a spat thing: pointed the way it flies.
                    Put(o, FxShape.Dot, c.Ramp, 5, 0, 0, 0, 1, 45);
                    Put(o, FxShape.Shard, c.Ramp, 8, 0, 0, 0, 1, dir);
                    Put(o, FxShape.Dot, c.Ramp, 1, 3, -bx * 3, -by * 3);
                    for (int i = 1; i <= 2; i++) Put(o, FxShape.Dot, c.Ramp, 2, 1, bx * (3 + i * 3), by * (3 + i * 3), 0.7f - i * 0.25f);
                    break;
            }
        }
    }
}
