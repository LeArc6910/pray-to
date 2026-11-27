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
                float progress = Mathf.Clamp01(since / Mathf.Max(0.1f, attack.Telegraph));
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
                    float wound = strikeAt - Mathf.Min(0.5f * strikeAt, swing / Mathf.Max(0.1f, attack.Telegraph));
                    if (progress < wound) (pose, frame) = (windUp, Through(windUp, progress / wound));
                    else
                    {
                        int f = GeneralBlowFrame(blow, (progress - wound) * attack.Telegraph);
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
                    Hear(Sound.Roar, body.Position, 0.5f);
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
            float since = Time.time - body.BossSince;
            bool puff = Time.time >= body.DustAt;
            if (puff) body.DustAt = Time.time + 0.1f;
            var feet = body.Position;
            var chest = feet + new Vector3(0, 1.4f, 0);
            var ahead = new Vector3(view.Facing.X, view.Facing.Y, 0);
            if (view.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / Mathf.Max(0.1f, attack.Telegraph));
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
                        if (puff) Kick(feet + new Vector3(Random.Range(-0.3f, 0.3f), 3f, 0), Vector3.up, blow == GeneralPose.Rally ? BannerColor : WarFireColor, 1);
                        break;
                    }
                    case GeneralPose.Plant:
                        if (puff && progress > 0.35f) Kick(feet + new Vector3(Random.Range(-0.4f, 0.4f), 2.8f, 0), Vector3.up * 0.5f, WarFireColor, 1);
                        break;
                    case GeneralPose.Stab:
                        // Ghostly spearmen's breath gathering at his feet.
                        if (puff && progress > 0.4f) Kick(feet + (Vector3)(Random.insideUnitCircle * 1.6f), Vector3.up * 0.6f, WarGhostColor, 1);
                        break;
                    case GeneralPose.Spin:
                        if (puff) Kick(feet + (Vector3)(Random.insideUnitCircle.normalized * 1.8f), Vector3.zero - (Vector3)(Random.insideUnitCircle.normalized), DustColor, 1);
                        break;
                    case GeneralPose.Charge:
                        if (puff) Kick(feet - ahead * 0.8f, -ahead + Vector3.up * 0.3f, DirtColor, 2);
                        break;
                }
            }
            if (view.BossState == BossState.Charge)
            {
                var moved = body.Position - body.LastPosition;
                SpeedLines(feet, moved);
                if (puff) Kick(feet + new Vector3(0, 0.8f, 0), -moved, WarFireColor, 2);
            }
            // His madness in the last phase: red mist curling off him.
            if (view.Phase >= 3 && puff && Random.value < 0.5f) Kick(chest + (Vector3)(Random.insideUnitCircle * 0.6f), Vector3.up, RageColor, 1);
        }

        // Where one of his blows lands, a burst of its own; false for anyone else's.
        bool GeneralLanded(HazardState hazard)
        {
            var attack = AttackOf(hazard);
            if (attack == null || _bossInView == null || _bossInView.Id != "mad_general") return false;
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            var along = new Vector3(hazard.Direction.X, hazard.Direction.Y, 0);
            switch (attack.Id)
            {
                case "cleave":
                {
                    // The blade down through the wedge: fire thrown along it, a heavy jolt.
                    float half = attack.Angle / 2;
                    for (float a = -half; a <= half; a += 15)
                    {
                        var dir = Quaternion.Euler(0, 0, a) * along;
                        Kick(at + dir * hazard.Size * 0.7f, dir, WarFireColor, 2);
                    }
                    AddFlash(at + along * hazard.Size * 0.5f, hazard.Size * 0.8f, WarFireColor, FlashKind.Burst);
                    Cracks(at + along * 1.2f, 1.2f, 3);
                    if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.06f);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.22f);
                    Hear(Sound.Heavy, at, 0.9f);
                    return true;
                }
                case "hellfire":
                case "meteors":
                {
                    // Fire called down where he pointed: a pillar of flame, burning arrows shattering for the volley.
                    bool volley = attack.Id == "meteors";
                    AddFlash(at, hazard.Size * 1.2f, WarFireColor, FlashKind.Burst);
                    Kick(at, Vector3.up, WarFireColor, volley ? 4 : 6);
                    if (volley) Kick(at, Vector3.up * 0.5f, ArrowShaftColor, 2);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, volley ? 0.16f : 0.12f);
                    Hear(volley ? Sound.Shoot : Sound.Boom, at, 0.7f);
                    return true;
                }
                case "dark_cross":
                {
                    // Ghostly spears thrust up along both arms of the cross, out from where the blade went in.
                    var across = new Vector3(-along.y, along.x, 0);
                    foreach (var arm in new[] { along, -along, across, -across })
                        for (float d = 0.8f; d <= attack.Length; d += 1.1f)
                            Kick(at + arm * d, Vector3.up, WarGhostColor, 1);
                    AddFlash(at, 1.6f, WarGhostColor, FlashKind.Impact);
                    Cracks(at, 1.4f, 4);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.2f);
                    Hear(Sound.Hammer, at, 0.8f);
                    Hear(Sound.Roar, at, 0.35f);
                    return true;
                }
                case "flame_ring":
                case "ember_wake":
                {
                    // Fire running out round the planted banner (or the struck floor): flame all round its edge.
                    float inner = attack.Radius, outer = attack.Radius + 0.5f * (attack.Length - attack.Radius);
                    for (int i = 0; i < 14; i++)
                    {
                        var dir = Quaternion.Euler(0, 0, i * 360f / 14 + Random.Range(-8f, 8f)) * Vector3.right;
                        Kick(at + dir * Random.Range(inner, outer), dir + Vector3.up * 0.6f, WarFireColor, 1);
                    }
                    AddFlash(at, attack.Length, WarFireColor, FlashKind.Ring);
                    if (attack.Id == "flame_ring")
                    {
                        Impact(at, 0.7f);
                        Hear(Sound.Boom, at, 0.8f);
                    }
                    else Hear(Sound.Nova, at, 0.5f);
                    return true;
                }
            }
            return false;
        }

        // The banner's wooden pole, for the arrows' shafts.
        static readonly Color ArrowShaftColor = new Color(0.28f, 0.18f, 0.13f);
    }
}
