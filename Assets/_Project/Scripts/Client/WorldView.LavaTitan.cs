using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // ---- The lava titan of the caldera (2026-11-28, Docs/Bosses.md 용암 거인): a giant of black basalt with lava in its
    // seams and a molten heart. Every attack has its own wind-up of the whole body (both fists heaved overhead before the
    // slam, an arm drawn back ablaze before the fire wave, the arms raised in a V with the heart blazing before the
    // meteors, hunched over the swelling heart before the bursts, a fist raised from a crouch before the eruption, a hand
    // swung back low before the lava trail, the arms crossed tight before the heat ring, a foot lifted before the cross,
    // risen white hot before the fire rain), the blow lands as the mark fills, and after the slam, the eruption's punch
    // or the stomp it stays down a moment, the opening to hit back. Its blows are lava and fire: gobbets bursting out of
    // the floor and a pool left glowing, a wall of fire rolled across, the floor splitting with lava spurting up. ----
    sealed partial class WorldView
    {
        static readonly Color LavaColor = new Color(1f, 0.55f, 0.18f);

        // Which blow follows each of its attacks (by the attack's id, boss_attacks.csv; an attack it does not know by
        // name by its shape).
        static LavaTitanPose LavaTitanBlowOf(BossAttack attack) => attack.Id switch
        {
            "slam" => LavaTitanPose.Smash,
            "fire_wave" => LavaTitanPose.Sweep,
            "meteor" => LavaTitanPose.Hurl,
            "magma_ring" or "magma_storm" or "magma_spiral" => LavaTitanPose.Vent,
            "eruption" => LavaTitanPose.Punch,
            "lava_trail" => LavaTitanPose.Gouge,
            "heat_ring" or "aftershock" => LavaTitanPose.Flare,
            "lava_cross" => LavaTitanPose.Stomp,
            "fire_rain" => LavaTitanPose.Blaze,
            _ => attack.Shape switch
            {
                AttackShape.Cone => LavaTitanPose.Sweep,
                AttackShape.Burst or AttackShape.Spiral => LavaTitanPose.Vent,
                AttackShape.Trail or AttackShape.Line => LavaTitanPose.Gouge,
                AttackShape.Ring => LavaTitanPose.Flare,
                AttackShape.Cross => LavaTitanPose.Stomp,
                _ => LavaTitanPose.Smash,
            },
        };

        // The frame of a blow so many seconds after its first, each frame for its own time; -1 past its end.
        static int LavaTitanBlowFrame(LavaTitanPose blow, float seconds)
        {
            for (int f = 0; f < LavaTitanArt.FramesOf(blow); f++)
            {
                seconds -= LavaTitanArt.FrameSeconds(blow, f);
                if (seconds < 0) return f;
            }
            return -1;
        }

        // Its pose from what it is doing. A wind-up runs through its telegraph, and the blow's lunge frames fill the
        // moment before it lands; after it, each frame of the blow for its own time, then rest.
        void AnimateLavaTitan(Body body, Vec2 facing, bool dead, bool moving)
        {
            var attack = body.BossAttack;
            float since = Time.time - body.BossSince;
            int sector = SectorOf(facing);
            var pose = LavaTitanPose.Idle;
            int frame = Mathf.FloorToInt(Time.time * 5) % LavaTitanArt.FramesOf(LavaTitanPose.Idle);
            body.Shape.color = Color.white;
            int Through(LavaTitanPose p, float progress) => Mathf.Clamp(Mathf.FloorToInt(progress * LavaTitanArt.FramesOf(p)), 0, LavaTitanArt.FramesOf(p) - 1);
            if (dead)
            {
                frame = 0;
            }
            else if (Entering(body))
            {
                // Under the lava of its caldera with only bubbles showing; woken, it heaves itself out.
                pose = LavaTitanPose.Enter;
                frame = body.BossState == BossState.Dormant ? 0 : LavaTitanArt.EnterFrameAt((Time.time - body.BossSince) / body.EnterSeconds, body.EntersAgain);
                body.EnterFrame = body.BossState == BossState.Dormant ? -1 : frame;
            }
            else if (body.BossState == BossState.Roar || body.BossState == BossState.Telegraph && attack != null && attack.Shape == AttackShape.Summon)
            {
                // The arms flung wide and the head back, the flames roaring, then shaking there.
                pose = LavaTitanPose.Roar;
                int rise = Mathf.FloorToInt(since / 0.06f);
                frame = rise < LavaTitanArt.RoarRise ? rise : LavaTitanArt.RoarRise + Mathf.FloorToInt(Time.time * 10) % 4;
            }
            else if (body.BossState == BossState.Charge)
            {
                // Charging with its shoulders down.
                pose = LavaTitanPose.Rush;
                frame = Mathf.FloorToInt(Time.time * 14) % LavaTitanArt.FramesOf(pose);
            }
            else if (moving && (body.BossState == BossState.Approach || body.BossState == BossState.Recover || body.BossState == BossState.Dormant))
            {
                // Lumbering, a foot up in turn.
                pose = LavaTitanPose.Walk;
                frame = Mathf.FloorToInt(body.Walked * 6) % LavaTitanArt.FramesOf(pose);
            }
            else if (body.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / Mathf.Max(0.1f, attack.Telegraph));
                var blow = LavaTitanBlowOf(attack);
                var windUp = LavaTitanArt.WindUpOf(blow);
                int strike = LavaTitanArt.StrikeFrame(blow);
                float swing = strike * LavaTitanArt.FrameSeconds(blow, 0);
                float wound = 1 - Mathf.Min(0.5f, swing / Mathf.Max(0.1f, attack.Telegraph));
                if (progress < wound) (pose, frame) = (windUp, Through(windUp, progress / wound));
                else
                {
                    int f = LavaTitanBlowFrame(blow, (progress - wound) * attack.Telegraph);
                    (pose, frame) = (blow, f < 0 ? strike - 1 : Mathf.Min(strike - 1, f));
                }
            }
            else if (body.BossState == BossState.Recover && attack != null && attack.Shape != AttackShape.Summon)
            {
                // From the strike on, each frame for its own time.
                var blow = LavaTitanBlowOf(attack);
                int strike = LavaTitanArt.StrikeFrame(blow);
                float left = since;
                for (int k = strike; k < LavaTitanArt.FramesOf(blow); k++)
                {
                    left -= LavaTitanArt.FrameSeconds(blow, k);
                    if (left >= 0) continue;
                    (pose, frame) = (blow, k);
                    break;
                }
            }

            // Spraying its lava in a spiral it turns round and round.
            if (attack != null && attack.Shape == AttackShape.Spiral && (pose == LavaTitanPose.Vent || pose == LavaTitanPose.Swell) && body.BossState != BossState.Telegraph)
                sector = (sector + Mathf.FloorToInt(Time.time * 12)) % 8;

            var (view, turned) = BossSectors[sector];
            int phase = Mathf.Clamp(body.BossPhase, 1, 3);
            int code = 900000 + ((((int)pose * 8 + (int)view) * 64 + frame) * 4 + phase) * 2 + (turned ? 1 : 0);
            if (code == body.Frame) return;
            if (pose != LavaTitanPose.Enter && pose != LavaTitanPose.Walk && pose != LavaTitanPose.Idle && pose != LavaTitanPose.Roar && pose != LavaTitanPose.Rush)
            {
                // The lunge begins: a heavy swing.
                if (frame == 0 && LavaTitanArt.StrikeFrame(pose) > 0) Hear(Sound.Heavy, body.Position, 0.8f);
            }
            // Each heavy footfall shakes the floor a little.
            if (pose == LavaTitanPose.Walk && (frame == 2 || frame == 6) && Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.03f);
            body.Frame = code;
            string key = $"titan/{view}/{pose}/{frame}/{phase}";
            System.Func<PixelImage> draw = () => LavaTitanArt.Draw(view, pose, frame, phase);
            var pivot = new Vector2(0.5f, LavaTitanArt.Floor / (float)LavaTitanArt.Height);
            body.Shape.sprite = Sprites.Get(key, draw, pivot);
            body.Glow.sprite = Sprites.Get(key + "/white", () => draw().Silhouette(), pivot);
            body.Shape.flipX = turned;
            body.Glow.flipX = turned;
            body.Top = -body.Feet + LavaTitanArt.BodyHeight / (float)Sprites.PixelsPerUnit;
        }

        // While it winds up: embers shaken off its raised fists, sparks streaming up off it as its heart swells, heat
        // gathering under the stooped hand, cinders drifting down off the raised foot; before the fire rain the hall
        // begins to shake.
        void LavaTitanExtras(Body body, EntityView view)
        {
            var attack = view.BossAttack;
            float since = Time.time - body.BossSince;
            bool puff = Time.time >= body.DustAt;
            if (puff) body.DustAt = Time.time + 0.1f;
            var feet = body.Position;
            var ahead = new Vector3(view.Facing.X, view.Facing.Y, 0);
            var chest = feet + new Vector3(0, 1.3f, 0);
            // Always a few embers rising off its shoulders, more in its last phase.
            if (puff && Random.value < (view.Phase >= 3 ? 0.6f : 0.25f))
                Chips(feet + new Vector3(Random.Range(-0.7f, 0.7f), 1.9f, 0), Vector3.up, FxRamp.Fire, 1, 10, 30);
            if (view.BossState != BossState.Telegraph || attack == null || attack.Shape == AttackShape.Summon) return;
            float progress = Mathf.Clamp01(since / Mathf.Max(0.1f, attack.Telegraph));
            switch (LavaTitanArt.WindUpOf(LavaTitanBlowOf(attack)))
            {
                case LavaTitanPose.Heave:
                case LavaTitanPose.Kneel:
                    // Embers shaken off the fists raised over its head.
                    if (puff) Chips(feet + new Vector3(Random.Range(-0.8f, 0.8f), 3.2f, 0), Vector3.down, FxRamp.Fire, 1, 14, 60);
                    break;
                case LavaTitanPose.Wind:
                case LavaTitanPose.Stoop:
                    // The drawn-back hand burning, sparks off it.
                    if (puff) Chips(feet - ahead * 0.9f + new Vector3(0, 0.8f, 0), Vector3.up - ahead * 0.3f, FxRamp.Fire, 1, 12, 50);
                    break;
                case LavaTitanPose.Call:
                case LavaTitanPose.Ignite:
                {
                    // Sparks streaming up off it to the roof; before the fire rain the hall shakes harder and harder.
                    if (puff) Chips(chest + (Vector3)(Random.insideUnitCircle * 0.6f), Vector3.up, FxRamp.Fire, 2, 24, 30);
                    if (attack.Id == "fire_rain" && Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.03f + 0.09f * progress);
                    break;
                }
                case LavaTitanPose.Swell:
                case LavaTitanPose.Brace:
                    // Heat pouring off the swelling heart.
                    if (puff) Chips(chest + ahead * 0.3f, Vector3.up + (Vector3)(Random.insideUnitCircle * 0.5f), FxRamp.Fire, 1, 12, 120);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.01f + 0.03f * progress);
                    break;
                case LavaTitanPose.Lift:
                    // Cinders falling off the raised foot.
                    if (puff) Chips(feet + ahead * 0.3f + new Vector3(0, 0.5f, 0), Vector3.down, FxRamp.Fire, 1, 8, 40);
                    break;
            }
        }

        // Where one of its blows lands, lava and fire of its own over the blow's shape; false for anyone else's.
        bool LavaTitanLanded(HazardState hazard)
        {
            var (boss, id) = BlowOf(hazard);
            if (boss != "lava_titan") return false;
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            var c = new BossContext { Ramp = FxRamp.Fire, Dir = hazard.Direction.Angle * Mathf.Rad2Deg, Size = hazard.Size * Ppu, Width = hazard.Width * Ppu };
            switch (id)
            {
                case "slam":
                case "eruption":
                case "meteor":
                case "fire_rain":
                {
                    // Lava bursting out of the floor where its fists, a falling rock or its fire came down; the eruption
                    // throws a column of it up first.
                    bool heavy = id == "slam" || id == "fire_rain";
                    BlastLanded(hazard, FxRamp.Fire, sparks: false, light: false, wave: id == "slam");
                    c.Big = heavy;
                    c.Count = id == "eruption" ? 2 : 0;
                    AddBossFx(BossFx.Magma, at, c);
                    if (id == "slam")
                    {
                        Cracks(at, hazard.Size * 1.1f, 5);
                        if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.07f);
                    }
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, heavy ? 0.24f : 0.16f);
                    Hear(Sound.Boom, at, heavy ? 1 : 0.75f);
                    if (id == "slam") Hear(Sound.Heavy, at, 0.9f);
                    return true;
                }
                case "lava_trail":
                    // Lava bursting up after its flung hand, one circle after another.
                    BlastLanded(hazard, FxRamp.Fire, sparks: false, chunks: false, light: false, wave: false);
                    AddBossFx(BossFx.Magma, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.1f);
                    Hear(Sound.Boom, at, 0.55f);
                    return true;
                case "fire_wave":
                    // A wall of fire rolled out across the wedge.
                    BlastLanded(hazard, FxRamp.Fire, sparks: false, chunks: false, light: false);
                    c.Width = hazard.Width;
                    AddBossFx(BossFx.FlameWave, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.16f);
                    HearNova(at, 0.8f, SpellElement.Fire);
                    return true;
                case "lava_cross":
                    // The floor split along the arm, lava spurting out of it.
                    BlastLanded(hazard, FxRamp.Fire, sparks: false, chunks: false, light: false, wave: false);
                    AddBossFx(BossFx.Fissure, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.18f);
                    Hear(Sound.Break, at, 0.7f);
                    return true;
                case "heat_ring":
                case "aftershock":
                    // Fire running round the band.
                    BlastLanded(hazard, FxRamp.Fire, sparks: false, chunks: false, light: false);
                    c.Inner = hazard.Width * Ppu;
                    c.Dir = 90;
                    AddBossFx(BossFx.FireBand, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, id == "heat_ring" ? 0.16f : 0.1f);
                    HearNova(at, id == "heat_ring" ? 0.8f : 0.5f, SpellElement.Fire);
                    return true;
            }
            // Any other of its blows: the shared landing and what fire does over it.
            return false;
        }

        // Its entrance (BossEntrance.Erupt, LavaTitanArt.Enter): the lava of the caldera bubbling and heaving; a fist
        // breaking the surface with a hiss; the floor shaking as it climbs; it bursts up out of the pool in a spray of
        // lava; its seams light from the heart outward with a roar of flame; the roar.
        void LavaTitanEntrance(Body body, int frame, System.Func<int, bool> reached, bool puff)
        {
            var feet = body.Position;
            if (frame < LavaTitanArt.EnterBurst && puff)
            {
                body.DustAt = Time.time + 0.12f;
                Chips(feet + (Vector3)(Random.insideUnitCircle * 1.1f), Vector3.up, FxRamp.Fire, 1, 10, 40);
            }
            if (frame < LavaTitanArt.EnterBurst && frame > LavaTitanArt.EnterHand && Settings.ScreenShake)
                _shake = Mathf.Max(_shake, 0.03f + 0.06f * (frame - LavaTitanArt.EnterHand) / Mathf.Max(1, LavaTitanArt.EnterBurst - LavaTitanArt.EnterHand));
            if (reached(LavaTitanArt.EnterHand))
            {
                AddBossFx(BossFx.Magma, feet + new Vector3(-0.4f, 0.2f, 0), new BossContext { Ramp = FxRamp.Fire, Size = 0.6f * Ppu, Seed = 23 });
                Hear(Sound.Hiss, feet, 0.8f);
            }
            if (reached(LavaTitanArt.EnterBurst))
            {
                AddFlash(feet, 3f, LavaColor, FlashKind.Burst);
                AddBossFx(BossFx.Magma, feet, new BossContext { Ramp = FxRamp.Fire, Size = 1.8f * Ppu, Big = true, Count = 2, Seed = 29 });
                Thud(body, 0.12f, 0.8f);
                Hear(Sound.Boom, feet, 1);
                Hear(Sound.Splash, feet, 0.6f);
            }
            if (reached(LavaTitanArt.EnterKindle))
            {
                AddFlash(feet + new Vector3(0, 1.3f, 0), 1.4f, LavaColor, FlashKind.Ring);
                HearNova(feet, 0.6f, SpellElement.Fire);
            }
            if (!body.EntersAgain && reached(LavaTitanArt.EnterRoar))
            {
                AddFlash(feet + new Vector3(0, 1.8f, 0), 2f, LavaColor, FlashKind.Ring);
                AddBossFx(BossFx.FireBand, feet, new BossContext { Ramp = FxRamp.Fire, Size = 2.2f * Ppu, Inner = 1.2f * Ppu, Dir = 90, Seed = 31 });
                HearRoar(body.Id, feet, 1);
            }
        }
    }
}
