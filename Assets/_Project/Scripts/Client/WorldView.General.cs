using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // ---- The mad general Morg of the demon nest (2026-11-27, Docs/Bosses.md 미쳐 버린 장군 모르그): a commander gone
    // mad, still giving orders to an army that is no longer there. Unlike the black knight's slow lone hammer, every
    // attack is an order: the sword thrust to the sky and swept down to point, and fire falls where it points (the war
    // drums beat as he raises it); the blade driven into the floor and ghostly spears cross there; the banner pulled
    // from his back and planted, and the fire rings out from the pole; the banner waved over his head and his soldiers
    // come. In between, his own greatsword: a great overhand cleave, a spin, a run with the blade levelled. In the last
    // phase his madness shows: red mist off him, the blade white-hot. ----
    sealed partial class WorldView
    {
        static readonly Color WarFireColor = new Color(1f, 0.5f, 0.15f);
        static readonly Color WarGhostColor = new Color(1f, 0.42f, 0.4f, 0.75f);
        static readonly Color BannerColor = new Color(0.6f, 0.1f, 0.13f);

        // Which blow follows each of its attacks (by the attack's id, boss_attacks.csv; an attack it does not know by
        // name by its shape).
        static GeneralPose GeneralBlowOf(BossAttack attack) => attack.Id switch
        {
            "cleave" => GeneralPose.Cleave,
            "hellfire" or "meteors" => GeneralPose.Command,
            "dark_cross" => GeneralPose.Stab,
            "flame_ring" => GeneralPose.Plant,
            "ember_spiral" => GeneralPose.Spin,
            _ => attack.Shape switch
            {
                AttackShape.Cone => GeneralPose.Cleave,
                AttackShape.Circle => GeneralPose.Command,
                AttackShape.Cross => GeneralPose.Stab,
                AttackShape.Ring => GeneralPose.Plant,
                AttackShape.Spiral or AttackShape.Burst => GeneralPose.Spin,
                _ => GeneralPose.Cleave,
            },
        };

        // How far through the telegraph the blow lands: at the end for his own blade (it meets the floor as the mark
        // fills), early for an order (he points, and the fire he calls lands as the marks fill).
        static float GeneralStrikeAt(BossAttack attack) => attack.Id == "hellfire" || attack.Id == "meteors" ? 0.45f : 1;

        static int GeneralBlowFrame(GeneralPose blow, float seconds)
        {
            for (int f = 0; f < GeneralArt.FramesOf(blow); f++)
            {
                seconds -= GeneralArt.FrameSeconds(blow, f);
                if (seconds < 0) return f;
            }
            return -1;
        }

        void AnimateGeneral(Body body, Vec2 facing, bool dead, bool moving)
        {
            var attack = body.BossAttack;
            float since = Time.time - body.BossSince;
            int sector = SectorOf(facing);
            var pose = GeneralPose.Idle;
            int frame = Mathf.FloorToInt(Time.time * 3f) % GeneralArt.FramesOf(GeneralPose.Idle);
            body.Shape.color = Color.white;
            int Through(GeneralPose p, float progress) => Mathf.Clamp(Mathf.FloorToInt(progress * GeneralArt.FramesOf(p)), 0, GeneralArt.FramesOf(p) - 1);
            if (dead)
            {
                frame = 0;
            }
            else if (Entering(body))
            {
                // Kneeling before his sword planted in the floor; woken, he rises, pulls it free and roars.
                pose = GeneralPose.Enter;
                frame = body.BossState == BossState.Dormant ? 0 : GeneralArt.EnterFrameAt((Time.time - body.BossSince) / body.EnterSeconds, body.EntersAgain);
                body.EnterFrame = body.BossState == BossState.Dormant ? -1 : frame;
            }
            else if (body.BossState == BossState.Telegraph && attack != null && attack.Shape == AttackShape.Summon)
            {
                // The banner hauled up over his head and waved: the soldiers' call.
                pose = GeneralPose.Rally;
                int rise = Mathf.FloorToInt(since / 0.05f);
                frame = rise < GeneralArt.RallyRise ? rise : GeneralArt.RallyRise + Mathf.FloorToInt(Time.time * 10) % 4;
            }
            else if (body.BossState == BossState.Roar)
            {
                pose = GeneralPose.Roar;
                int rise = Mathf.FloorToInt(since / 0.06f);
                frame = rise < GeneralArt.RoarRise ? rise : GeneralArt.RoarRise + Mathf.FloorToInt(Time.time * 10) % 4;
            }
            else if (body.BossState == BossState.Charge)
            {
                pose = GeneralPose.Charge;
                frame = Mathf.FloorToInt(Time.time * 20) % GeneralArt.FramesOf(pose);
            }
            else if (moving && (body.BossState == BossState.Approach || body.BossState == BossState.Recover || body.BossState == BossState.Dormant))
            {
                pose = GeneralPose.Walk;
                frame = Mathf.FloorToInt(body.Walked * 9) % GeneralArt.FramesOf(pose);
            }
            else if (body.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / TelegraphOf(body, attack));
                if (attack.Shape == AttackShape.Charge)
                {
                    (pose, frame) = (GeneralPose.Brace, Through(GeneralPose.Brace, progress));
                }
                else
                {
                    var blow = GeneralBlowOf(attack);
                    var windUp = GeneralArt.WindUpOf(blow);
                    float strikeAt = GeneralStrikeAt(attack);
                    int strike = GeneralArt.StrikeFrame(blow);
                    float swing = strike * GeneralArt.FrameSeconds(blow, 0);
                    float wound = strikeAt - Mathf.Min(0.5f * strikeAt, swing / TelegraphOf(body, attack));
                    if (progress < wound) (pose, frame) = (windUp, Through(windUp, progress / wound));
                    else
                    {
                        int f = GeneralBlowFrame(blow, (progress - wound) * TelegraphOf(body, attack));
                        if (strikeAt >= 1) (pose, frame) = (blow, f < 0 ? strike - 1 : Mathf.Min(strike - 1, f));
                        else if (f >= 0) (pose, frame) = (blow, f);
                    }
                }
            }
            else if (body.BossState == BossState.Recover && attack != null)
            {
                if (attack.Shape == AttackShape.Charge)
                {
                    int back = GeneralArt.FramesOf(GeneralPose.Brace) - 1 - Mathf.FloorToInt(since / 0.06f);
                    if (back >= 0) (pose, frame) = (GeneralPose.Brace, back);
                }
                else if (GeneralStrikeAt(attack) >= 1 && attack.Shape != AttackShape.Summon)
                {
                    var blow = GeneralBlowOf(attack);
                    int strike = GeneralArt.StrikeFrame(blow);
                    float left = since;
                    for (int k = strike; k < GeneralArt.FramesOf(blow); k++)
                    {
                        left -= GeneralArt.FrameSeconds(blow, k);
                        if (left >= 0) continue;
                        (pose, frame) = (blow, k);
                        break;
                    }
                }
            }

            // The body turns with the motion: away from the viewer as the sword is drawn back; all the way round, a
            // view a frame, in the spin.
            sector = GeneralArt.Spinning(pose, frame) ? (sector + frame) % 8 : Twist(sector, GeneralArt.TwistOf(pose, frame));

            var (view, turned) = BossSectors[sector];
            int phase = Mathf.Clamp(body.BossPhase, 1, 3);
            int code = 700000 + ((((int)pose * 8 + (int)view) * 64 + frame) * 4 + phase) * 2 + (turned ? 1 : 0);
            if (code == body.Frame) return;
            if (pose == GeneralPose.Walk && (frame == 2 || frame == 6) && body.KnightStep != frame) Hear(Sound.Step, body.Position, 0.6f);
            body.KnightStep = pose == GeneralPose.Walk ? frame : -1;
            if (pose == GeneralPose.Cleave || pose == GeneralPose.Command || pose == GeneralPose.Stab || pose == GeneralPose.Plant || pose == GeneralPose.Spin)
            {
                // The swing begins: a whoosh of flame; the spin roars round with each turn.
                if (frame == 0) Hear(Sound.Swing, body.Position, 1);
                if (pose == GeneralPose.Spin && frame > 0 && frame % 4 == 0 && GeneralArt.Spinning(pose, frame)) Hear(Sound.Swing, body.Position, 0.7f);
                // The order given: a horn-like roar as the sword comes down to point.
                if (pose == GeneralPose.Command && frame == GeneralArt.StrikeFrame(pose))
                {
                    HearRoar(body.Id, body.Position, 0.5f);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.08f);
                }
            }
            body.Frame = code;
            string key = $"general/{view}/{pose}/{frame}/{phase}";
            System.Func<PixelImage> draw = () => GeneralArt.Draw(view, pose, frame, phase);
            var pivot = new Vector2(0.5f, GeneralArt.Floor / (float)GeneralArt.Height);
            body.Shape.sprite = Sprites.Get(key, draw, pivot);
            body.Glow.sprite = Sprites.Get(key + "/white", () => draw().Silhouette(), pivot);
            body.Shape.flipX = turned;
            body.Glow.flipX = turned;
            body.Top = -body.Feet + GeneralArt.BodyHeight / (float)Sprites.PixelsPerUnit;
        }

        // While he winds up: the war drums beat faster and faster as the sword goes up for an order, embers stream
        // off the raised blade; the banner's cloth throws sparks as it is hoisted; dust is kicked up behind before a
        // run; in the last phase a red mist of madness comes off him.
        void GeneralExtras(Body body, EntityView view)
        {
            var attack = view.BossAttack;
            _generalPhase = view.Phase;
            float since = Time.time - body.BossSince;
            bool puff = Time.time >= body.DustAt;
            if (puff) body.DustAt = Time.time + 0.1f;
            var feet = body.Position;
            var chest = feet + new Vector3(0, 1.4f, 0);
            var ahead = new Vector3(view.Facing.X, view.Facing.Y, 0);
            if (view.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / TelegraphOf(body, attack));
                var blow = attack.Shape == AttackShape.Charge ? GeneralPose.Charge : attack.Shape == AttackShape.Summon ? GeneralPose.Rally : GeneralBlowOf(attack);
                switch (blow)
                {
                    case GeneralPose.Command:
                    case GeneralPose.Rally:
                    {
                        // The drums: a beat every so often, quickening toward the order.
                        float beat = Mathf.Lerp(0.36f, 0.14f, progress);
                        if (Time.time >= body.DrumAt)
                        {
                            body.DrumAt = Time.time + beat;
                            Hear(Sound.Thump, feet, 0.55f + 0.35f * progress);
                            if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.02f + 0.04f * progress);
                        }
                        if (puff) Chips(feet + new Vector3(Random.Range(-0.3f, 0.3f), 3f, 0), Vector3.up, blow == GeneralPose.Rally ? FxRamp.Danger : FxRamp.Fire, 1);
                        break;
                    }
                    case GeneralPose.Plant:
                        if (puff && progress > 0.35f) Chips(feet + new Vector3(Random.Range(-0.4f, 0.4f), 2.8f, 0), Vector3.up, FxRamp.Fire, 1);
                        break;
                    case GeneralPose.Stab:
                        // Ghostly spearmen's breath gathering at his feet.
                        if (puff && progress > 0.4f) Chips(feet + (Vector3)(Random.insideUnitCircle * 1.6f), Vector3.up, FxRamp.Danger, 1);
                        break;
                    case GeneralPose.Spin:
                        if (puff) Bits(feet + (Vector3)(Random.insideUnitCircle.normalized * 1.8f), Vector3.zero - (Vector3)(Random.insideUnitCircle.normalized), DustColor, 1);
                        break;
                    case GeneralPose.Charge:
                        if (puff) Bits(feet - ahead * 0.8f, -ahead + Vector3.up * 0.3f, DirtColor, 2);
                        break;
                }
            }
            if (view.BossState == BossState.Charge)
            {
                var moved = body.Position - body.LastPosition;
                SpeedLines(feet, moved);
                if (puff) Chips(feet + new Vector3(0, 0.8f, 0), -moved, FxRamp.Fire, 3, 18);
            }
            // His madness in the last phase: red mist curling off him.
            if (view.Phase >= 3 && puff && Random.value < 0.5f) Bits(chest + (Vector3)(Random.insideUnitCircle * 0.6f), Vector3.up, RageColor, 1);
        }

        // Where one of his blows lands, fire of his own over the blow's shape burning; false for anyone else's.
        bool GeneralLanded(HazardState hazard)
        {
            var (boss, id) = BlowOf(hazard);
            if (boss != "mad_general") return false;
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            var along = new Vector3(hazard.Direction.X, hazard.Direction.Y, 0);
            var c = new BossContext { Ramp = FxRamp.Fire, Dir = hazard.Direction.Angle * Mathf.Rad2Deg, Size = hazard.Size * Ppu, Width = hazard.Width * Ppu };
            switch (id)
            {
                case "cleave":
                {
                    // The burning blade down through the wedge: a thick smear of fire swept across it, flame left
                    // along its track, sparks flung on, a heavy jolt. White-hot in his last phase.
                    BlastLanded(hazard, FxRamp.Fire, sparks: false, light: false);
                    c.Width = hazard.Width;
                    c.Big = _generalPhase >= 3 && hazard.Id < StagedIds;
                    AddBossFx(BossFx.Cleave, at, c);
                    Cracks(at + along * 1.2f, 1.2f, 3);
                    if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.06f);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.22f);
                    Hear(Sound.Heavy, at, 0.9f);
                    return true;
                }
                case "hellfire":
                case "meteors":
                {
                    // Fire called down where he pointed: an explosion and flames left burning; the volley's arrows
                    // break and fly.
                    bool volley = id == "meteors";
                    BlastLanded(hazard, FxRamp.Fire, sparks: false, chunks: false, light: false, wave: false);
                    c.Count = volley ? 2 : 1;
                    AddBossFx(BossFx.FireCall, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, volley ? 0.16f : 0.12f);
                    Hear(volley ? Sound.Shoot : Sound.Boom, at, 0.7f);
                    return true;
                }
                case "dark_cross":
                {
                    // Ghostly spears thrust up along the arm from its middle outward, their afterimages lifting off.
                    BlastLanded(hazard, null, sparks: false);
                    c.Ramp = FxRamp.Danger;
                    AddBossFx(BossFx.Spears, at, c);
                    Cracks(at + along * hazard.Size * 0.5f, 1.4f, 4);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.2f);
                    Hear(Sound.Hammer, at, 0.8f);
                    HearRoar("mad_general", at, 0.35f);
                    return true;
                }
                case "flame_ring":
                case "ember_wake":
                {
                    // Fire running round the band from the planted banner (or the struck floor): rows of flame, soot
                    // under them, sparks rising.
                    BlastLanded(hazard, FxRamp.Fire, sparks: false, chunks: false, light: false);
                    c.Inner = hazard.Width * Ppu;
                    c.Dir = 90;
                    AddBossFx(BossFx.FireBand, at, c);
                    if (id == "flame_ring")
                    {
                        Impact(at, 0.7f);
                        Hear(Sound.Boom, at, 0.8f);
                    }
                    else HearNova(at, 0.5f, SpellElement.Fire);
                    return true;
                }
            }
            // Any other of his blows: the shared landing, in fire. In his last phase a blow of the blade (a wedge)
            // shows the white-hot blade through it too, as the cleave does (F2).
            BlastLanded(hazard, FxRamp.Fire);
            if (hazard.Shape == HazardShape.Cone && _generalPhase >= 3 && hazard.Id < StagedIds)
            {
                c.Width = hazard.Width;
                c.Big = true;
                c.Plain = true;
                AddBossFx(BossFx.Cleave, at, c);
                WhiteBlades++;
            }
            return true;
        }

        // His phase, for the blade white-hot in the last.
        int _generalPhase = 1;
        // For the playtest: blows other than the cleave that showed the white-hot blade.
        public int WhiteBlades { get; private set; }

        // The banner's wooden pole, for the arrows' shafts.
        static readonly Color ArrowShaftColor = new Color(0.28f, 0.18f, 0.13f);
    }
}
