using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // ---- The tide serpent of the underground lake (2026-11-28, Docs/Bosses.md 물살 뱀): a sea serpent coiled in three
    // loops, its head high on a tall neck. Every attack has its own wind-up of the whole body (the head drawn back low
    // and the coils wound tight before the lash, the head reared and the throat swelling before the jet, swelling all
    // over before the bubbles burst, the head dipped to the floor before the surge, the coils winding down into the
    // water before the whirlpool, towering before the crash, the frills flared before the cross, rearing as high as it
    // goes before the spout), the blow lands as the mark fills, and after a crash or a slam its head lies on the floor a
    // moment, the opening to hit back. Its blows are water: a wave swept across, a jet, a whirl, columns bursting up. ----
    sealed partial class WorldView
    {
        static readonly Color TideColor = new Color(0.55f, 0.85f, 0.95f);

        // Which blow follows each of its attacks (by the attack's id, boss_attacks.csv; an attack it does not know by
        // name by its shape).
        static SerpentPose SerpentBlowOf(BossAttack attack) => attack.Id switch
        {
            "tail_sweep" => SerpentPose.Lash,
            "water_jet" => SerpentPose.Spit,
            "bubble_burst" or "bubble_spiral" => SerpentPose.Burst,
            "tidal_trail" => SerpentPose.Surge,
            "whirlpool" or "undertow" => SerpentPose.Whirl,
            "deep_share" => SerpentPose.Crash,
            "tidal_cross" => SerpentPose.Slam,
            "spray_rain" => SerpentPose.Spout,
            _ => attack.Shape switch
            {
                AttackShape.Cone => SerpentPose.Lash,
                AttackShape.Line => SerpentPose.Spit,
                AttackShape.Burst or AttackShape.Spiral => SerpentPose.Burst,
                AttackShape.Trail => SerpentPose.Surge,
                AttackShape.Ring => SerpentPose.Whirl,
                AttackShape.Cross => SerpentPose.Slam,
                AttackShape.Share => SerpentPose.Crash,
                _ => SerpentPose.Spout,
            },
        };

        // The frame of a blow so many seconds after its first, each frame for its own time; -1 past its end.
        static int SerpentBlowFrame(SerpentPose blow, float seconds)
        {
            for (int f = 0; f < SerpentArt.FramesOf(blow); f++)
            {
                seconds -= SerpentArt.FrameSeconds(blow, f);
                if (seconds < 0) return f;
            }
            return -1;
        }

        // Its pose from what it is doing. A wind-up runs through its telegraph, and the blow's lunge frames fill the
        // moment before it lands; after it, each frame of the blow for its own time, then rest.
        void AnimateSerpent(Body body, Vec2 facing, bool dead, bool moving)
        {
            var attack = body.BossAttack;
            float since = Time.time - body.BossSince;
            int sector = SectorOf(facing);
            var pose = SerpentPose.Idle;
            int frame = Mathf.FloorToInt(Time.time * 5) % SerpentArt.FramesOf(SerpentPose.Idle);
            body.Shape.color = Color.white;
            int Through(SerpentPose p, float progress) => Mathf.Clamp(Mathf.FloorToInt(progress * SerpentArt.FramesOf(p)), 0, SerpentArt.FramesOf(p) - 1);
            if (dead)
            {
                frame = 0;
            }
            else if (Entering(body))
            {
                // Under the water of its hall with only ripples showing; woken, it rises out of it.
                pose = SerpentPose.Enter;
                frame = body.BossState == BossState.Dormant ? 0 : SerpentArt.EnterFrameAt((Time.time - body.BossSince) / body.EnterSeconds, body.EntersAgain);
                body.EnterFrame = body.BossState == BossState.Dormant ? -1 : frame;
            }
            else if (body.BossState == BossState.Roar || body.BossState == BossState.Telegraph && attack != null && attack.Shape == AttackShape.Summon)
            {
                // The frills spread and the head high, then shaking there.
                pose = SerpentPose.Roar;
                int rise = Mathf.FloorToInt(since / 0.06f);
                frame = rise < SerpentArt.RoarRise ? rise : SerpentArt.RoarRise + Mathf.FloorToInt(Time.time * 10) % 4;
            }
            else if (body.BossState == BossState.Charge)
            {
                // It has no charge of its own; any rush is a surge low along the floor.
                pose = SerpentPose.Surge;
                frame = SerpentArt.StrikeFrame(pose);
            }
            else if (moving && (body.BossState == BossState.Approach || body.BossState == BossState.Recover || body.BossState == BossState.Dormant))
            {
                // Slithering, the coils rippling.
                pose = SerpentPose.Walk;
                frame = Mathf.FloorToInt(body.Walked * 8) % SerpentArt.FramesOf(pose);
            }
            else if (body.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / Mathf.Max(0.1f, attack.Telegraph));
                var blow = SerpentBlowOf(attack);
                var windUp = SerpentArt.WindUpOf(blow);
                int strike = SerpentArt.StrikeFrame(blow);
                float swing = strike * SerpentArt.FrameSeconds(blow, 0);
                float wound = 1 - Mathf.Min(0.5f, swing / Mathf.Max(0.1f, attack.Telegraph));
                if (progress < wound) (pose, frame) = (windUp, Through(windUp, progress / wound));
                else
                {
                    int f = SerpentBlowFrame(blow, (progress - wound) * attack.Telegraph);
                    (pose, frame) = (blow, f < 0 ? strike - 1 : Mathf.Min(strike - 1, f));
                }
            }
            else if (body.BossState == BossState.Recover && attack != null && attack.Shape != AttackShape.Summon)
            {
                // From the strike on, each frame for its own time.
                var blow = SerpentBlowOf(attack);
                int strike = SerpentArt.StrikeFrame(blow);
                float left = since;
                for (int k = strike; k < SerpentArt.FramesOf(blow); k++)
                {
                    left -= SerpentArt.FrameSeconds(blow, k);
                    if (left >= 0) continue;
                    (pose, frame) = (blow, k);
                    break;
                }
            }

            // Spraying its bubbles in a spiral it turns round and round.
            if (attack != null && attack.Shape == AttackShape.Spiral && (pose == SerpentPose.Burst || pose == SerpentPose.Puff) && body.BossState != BossState.Telegraph)
                sector = (sector + Mathf.FloorToInt(Time.time * 12)) % 8;

            var (view, turned) = BossSectors[sector];
            int phase = Mathf.Clamp(body.BossPhase, 1, 3);
            int code = 800000 + ((((int)pose * 8 + (int)view) * 64 + frame) * 4 + phase) * 2 + (turned ? 1 : 0);
            if (code == body.Frame) return;
            if (pose != SerpentPose.Enter && pose != SerpentPose.Walk && pose != SerpentPose.Idle && pose != SerpentPose.Roar)
            {
                // The lunge begins: a rush of water.
                if (frame == 0 && SerpentArt.StrikeFrame(pose) > 0) Hear(Sound.Water, body.Position, 0.7f);
            }
            body.Frame = code;
            string key = $"serpent/{view}/{pose}/{frame}/{phase}";
            System.Func<PixelImage> draw = () => SerpentArt.Draw(view, pose, frame, phase);
            var pivot = new Vector2(0.5f, SerpentArt.Floor / (float)SerpentArt.Height);
            body.Shape.sprite = Sprites.Get(key, draw, pivot);
            body.Glow.sprite = Sprites.Get(key + "/white", () => draw().Silhouette(), pivot);
            body.Shape.flipX = turned;
            body.Glow.flipX = turned;
            body.Top = -body.Feet + SerpentArt.BodyHeight / (float)Sprites.PixelsPerUnit;
        }

        // While it winds up: water dripping off it and drops flicked up as it coils, mist breathed from its jaws as the
        // throat fills, water drawn round and down into the floor as it winds, spray streaming off it as it rears.
        void SerpentExtras(Body body, EntityView view)
        {
            var attack = view.BossAttack;
            float since = Time.time - body.BossSince;
            bool puff = Time.time >= body.DustAt;
            if (puff) body.DustAt = Time.time + 0.1f;
            var feet = body.Position;
            var ahead = new Vector3(view.Facing.X, view.Facing.Y, 0);
            var head = feet + new Vector3(0, 2.2f, 0);
            if (view.BossState != BossState.Telegraph || attack == null || attack.Shape == AttackShape.Summon) return;
            float progress = Mathf.Clamp01(since / Mathf.Max(0.1f, attack.Telegraph));
            switch (SerpentArt.WindUpOf(SerpentBlowOf(attack)))
            {
                case SerpentPose.Coil:
                case SerpentPose.Dive:
                    // Water flicked up off the coils as they tighten.
                    if (puff) Chips(feet + (Vector3)(Random.insideUnitCircle * 1.2f), Vector3.up + ahead * 0.3f, FxRamp.Cold, 1, 12, 40);
                    break;
                case SerpentPose.Draw:
                case SerpentPose.Puff:
                    // Mist breathed out of its jaws as the throat fills.
                    if (puff) Chips(head + ahead * 0.4f, ahead + Vector3.up * 0.3f, FxRamp.Mist, 1, 10, 50);
                    break;
                case SerpentPose.Wind:
                {
                    // Water drawn round it and down; the floor trembling.
                    if (puff)
                    {
                        float a = Time.time * 8;
                        var from = feet + new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.5f, 0) * 2f;
                        Chips(from, new Vector3(-Mathf.Sin(a), Mathf.Cos(a) * 0.5f, 0) + (feet - from) * 0.4f, FxRamp.Cold, 1, 16, 20);
                    }
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.02f + 0.05f * progress);
                    break;
                }
                case SerpentPose.Loom:
                case SerpentPose.Rise:
                    // Spray streaming off it as it rears; before the spout the hall begins to shake.
                    if (puff) Chips(head + (Vector3)(Random.insideUnitCircle * 0.6f), Vector3.down + ahead * 0.2f, FxRamp.Cold, 2, 18, 40);
                    if (attack.Id == "spray_rain" && Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.03f + 0.08f * progress);
                    break;
                case SerpentPose.Flare:
                    // A shimmer over its spread frills.
                    if (puff) Chips(head, Vector3.up, FxRamp.Mist, 1, 8, 120);
                    break;
            }
        }

        // Where one of its blows lands, water of its own over the blow's shape; false for anyone else's.
        bool SerpentLanded(HazardState hazard)
        {
            var (boss, id) = BlowOf(hazard);
            if (boss != "tide_serpent") return false;
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            var c = new BossContext { Ramp = FxRamp.Cold, Dir = hazard.Direction.Angle * Mathf.Rad2Deg, Size = hazard.Size * Ppu, Width = hazard.Width * Ppu, Seed = hazard.Id };
            switch (id)
            {
                case "tail_sweep":
                    // Its body lashed across the wedge: a wave swept over it and a heavy splash.
                    BlastLanded(hazard, FxRamp.Cold, sparks: false, chunks: false, light: false);
                    c.Width = hazard.Width;
                    AddBossFx(BossFx.Wave, at, c);
                    if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.06f);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.2f);
                    Hear(Sound.Splash, at, 1);
                    Hear(Sound.Heavy, at, 0.6f);
                    return true;
                case "water_jet":
                case "tidal_cross":
                    // A jet of water down the line (each arm of the cross its own).
                    BlastLanded(hazard, FxRamp.Cold, sparks: false, chunks: false, light: false, wave: false);
                    AddBossFx(BossFx.Torrent, at, c);
                    if (id == "tidal_cross" && Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.18f);
                    Hear(Sound.Splash, at, 0.8f);
                    return true;
                case "whirlpool":
                case "undertow":
                    // The water churning round the band.
                    BlastLanded(hazard, FxRamp.Cold, sparks: false, chunks: false, light: false);
                    c.Inner = hazard.Width * Ppu;
                    AddBossFx(BossFx.Maelstrom, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, id == "whirlpool" ? 0.2f : 0.12f);
                    Hear(Sound.Water, at, 0.9f);
                    return true;
                case "tidal_trail":
                case "spray_rain":
                    // A column of water bursting up out of the floor and raining back.
                    BlastLanded(hazard, FxRamp.Cold, sparks: false, chunks: false, light: false, wave: false);
                    c.Big = id == "spray_rain";
                    AddBossFx(BossFx.Geyser, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, id == "spray_rain" ? 0.22f : 0.12f);
                    Hear(Sound.Splash, at, id == "spray_rain" ? 1 : 0.7f);
                    return true;
                case "deep_share":
                    // Its head crashing down where it is shared (the gold circle burns gold): the floor heaves water up.
                    BlastLanded(hazard, null, sparks: false, chunks: false, light: false);
                    Impact(at, 0.9f);
                    c.Big = true;
                    AddBossFx(BossFx.Geyser, at, c);
                    Hear(Sound.Splash, at, 1);
                    return true;
            }
            // Any other of its blows: the shared landing and what water does over it.
            return false;
        }
    }
}
