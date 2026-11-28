using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // ---- The crystal spider queen (2026-11-27, Docs/Bosses.md 수정 거미 여왕): the third boss met, a spider grown of pink
    // and sky-blue crystal with a crown of gems. Where the silk mother takes the floor with silk and venom, the queen
    // fights with light and crystal: the crown gathers light and lets it go in beams and falling shards, the crystal
    // of her body bristles and bursts, her legs ring the floor, and she lays eggs of crystal that hatch her brood.
    // Every attack has a wind-up of the whole body, told apart by the crown and the abdomen as much as the legs. ----
    sealed partial class WorldView
    {
        static readonly Color CrystalPink = new Color(1f, 0.66f, 0.85f);
        static readonly Color CrystalBlue = new Color(0.62f, 0.9f, 1f);
        static readonly Color PrismWhite = new Color(1f, 0.97f, 1f);

        // Which blow follows each of its attacks (by the attack's id, boss_attacks.csv; one it does not know by name by
        // its shape).
        static CrystalQueenPose QueenBlowOf(BossAttack attack) => attack.Id switch
        {
            "bite" => CrystalQueenPose.Bite,
            "crystal_rain" or "shard_hail" or "crystal_cross" or "crystal_lattice" or "venom_share" or "resonance" => CrystalQueenPose.Flash,
            "shard_ring" or "shard_storm" or "crystal_spiral" => CrystalQueenPose.Burst,
            "web_quake" or "crystal_ripple" => CrystalQueenPose.Stomp,
            _ => attack.Shape switch
            {
                AttackShape.Cone => CrystalQueenPose.Bite,
                AttackShape.Burst or AttackShape.Spiral => CrystalQueenPose.Burst,
                AttackShape.Summon => CrystalQueenPose.Lay,
                AttackShape.Ring => CrystalQueenPose.Stomp,
                _ => CrystalQueenPose.Flash,
            },
        };

        // How far through the telegraph the blow lands: at the end, but for the crystal thrown up into the roof (rain and
        // hail), which the crown flings up early so the shards fall as the marks fill.
        static float QueenStrikeAt(BossAttack attack) => attack.Id == "crystal_rain" || attack.Id == "shard_hail" ? 0.4f : 1;

        static int QueenBlowFrame(CrystalQueenPose blow, float seconds)
        {
            for (int f = 0; f < CrystalQueenArt.FramesOf(blow); f++)
            {
                seconds -= CrystalQueenArt.FrameSeconds(blow, f);
                if (seconds < 0) return f;
            }
            return -1;
        }

        int _queenSector = 6;

        void AnimateCrystalQueen(Body body, Vec2 facing, bool dead, bool moving)
        {
            var attack = body.BossAttack;
            float since = Time.time - body.BossSince;
            int sector = SectorOf(facing);
            var pose = CrystalQueenPose.Idle;
            int frame = Mathf.FloorToInt(Time.time * 4) % CrystalQueenArt.FramesOf(CrystalQueenPose.Idle);
            body.Shape.color = Color.white;
            int Through(CrystalQueenPose p, float progress) => Mathf.Clamp(Mathf.FloorToInt(progress * CrystalQueenArt.FramesOf(p)), 0, CrystalQueenArt.FramesOf(p) - 1);
            if (dead)
            {
                frame = 0;
            }
            else if (Entering(body))
            {
                // Asleep in its cocoon of crystal; woken, the cocoon cracks with light and bursts (CrystalQueenPose.Enter).
                (pose, frame) = (CrystalQueenPose.Enter, EnterFrame(body, CrystalQueenArt.EnterFrames(body.EntersAgain)));
            }
            else if (body.BossState == BossState.Roar)
            {
                pose = CrystalQueenPose.Screech;
                int rise = Mathf.FloorToInt(since / 0.07f);
                frame = rise < CrystalQueenArt.ScreechRise ? rise : CrystalQueenArt.ScreechRise + Mathf.FloorToInt(Time.time * 10) % 4;
            }
            else if (body.BossState == BossState.Charge)
            {
                pose = CrystalQueenPose.Rush;
                frame = Mathf.FloorToInt(Time.time * 20) % CrystalQueenArt.FramesOf(pose);
            }
            else if (moving && (body.BossState == BossState.Approach || body.BossState == BossState.Recover || body.BossState == BossState.Dormant))
            {
                pose = CrystalQueenPose.Walk;
                frame = Mathf.FloorToInt(body.Walked * 7) % CrystalQueenArt.FramesOf(pose);
            }
            else if (body.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / TelegraphOf(body, attack));
                if (attack.Shape == AttackShape.Charge)
                {
                    (pose, frame) = (CrystalQueenPose.Crouch, Through(CrystalQueenPose.Crouch, progress));
                }
                else
                {
                    var blow = QueenBlowOf(attack);
                    var windUp = CrystalQueenArt.WindUpOf(blow);
                    float strikeAt = QueenStrikeAt(attack);
                    int strike = CrystalQueenArt.StrikeFrame(blow);
                    float swing = strike * CrystalQueenArt.FrameSeconds(blow, 0);
                    float wound = strikeAt - Mathf.Min(0.5f * strikeAt, swing / TelegraphOf(body, attack));
                    if (progress < wound) (pose, frame) = (windUp, Through(windUp, progress / wound));
                    else
                    {
                        int f = QueenBlowFrame(blow, (progress - wound) * TelegraphOf(body, attack));
                        if (strikeAt >= 1) (pose, frame) = (blow, f < 0 ? strike - 1 : Mathf.Min(strike - 1, f));
                        else if (f >= 0) (pose, frame) = (blow, f);
                    }
                }
            }
            else if (body.BossState == BossState.Recover && attack != null)
            {
                if (attack.Shape == AttackShape.Charge)
                {
                    int f = Mathf.FloorToInt(since / 0.06f);
                    if (f < CrystalQueenArt.FramesOf(CrystalQueenPose.Skid)) (pose, frame) = (CrystalQueenPose.Skid, f);
                }
                else if (QueenStrikeAt(attack) >= 1)
                {
                    var blow = QueenBlowOf(attack);
                    int strike = CrystalQueenArt.StrikeFrame(blow);
                    float left = since;
                    for (int k = strike; k < CrystalQueenArt.FramesOf(blow); k++)
                    {
                        left -= CrystalQueenArt.FrameSeconds(blow, k);
                        if (left >= 0) continue;
                        (pose, frame) = (blow, k);
                        break;
                    }
                }
            }
            _queenSector = sector;

            var (view, turned) = BossSectors[sector];
            int phase = Mathf.Clamp(body.BossPhase, 1, 3);
            int code = 700000 + ((((int)pose * 8 + (int)view) * 64 + frame) * 4 + phase) * 2 + (turned ? 1 : 0);
            if (code == body.Frame) return;
            // Crystal feet ticking on the stone.
            if (pose == CrystalQueenPose.Walk && frame % 6 == 0 && body.KnightStep != frame) Hear(Sound.Skitter, body.Position, 0.35f);
            body.KnightStep = pose == CrystalQueenPose.Walk ? frame : -1;
            if (frame == 0 && CrystalQueenArt.StrikeFrame(pose) > 0) Hear(pose == CrystalQueenPose.Flash ? Sound.Cast : Sound.Swing, body.Position, 0.8f);
            if (pose == CrystalQueenPose.Flash && frame == CrystalQueenArt.StrikeFrame(pose)) CrownFlares(body, attack);
            if (pose == CrystalQueenPose.Lay && frame == CrystalQueenArt.StrikeFrame(pose)) EggsLaid(body);
            body.Frame = code;
            string key = $"queen/{view}/{pose}/{frame}/{phase}";
            System.Func<PixelImage> draw = () => CrystalQueenArt.Draw(view, pose, frame, phase);
            var pivot = new Vector2(0.5f, CrystalQueenArt.Floor / (float)CrystalQueenArt.Height);
            body.Shape.sprite = Sprites.Get(key, draw, pivot);
            body.Glow.sprite = Sprites.Get(key + "/white", () => draw().Silhouette(), pivot);
            body.Shape.flipX = turned;
            body.Glow.flipX = turned;
            body.Top = -body.Feet + CrystalQueenArt.BodyTall / (float)Sprites.PixelsPerUnit;
        }

        // The crown letting its light go: a flash over its head and splinters of light; thrown up into the roof for the
        // rain and the hail.
        void CrownFlares(Body body, BossAttack attack)
        {
            var crown = body.Position + new Vector3(0, 2.1f, 0);
            bool up = attack != null && QueenStrikeAt(attack) < 1;
            AddFlash(crown, 1.4f, PrismWhite, FlashKind.Impact);
            AddBossFx(BossFx.Spray, crown, new BossContext { Ramp = FxRamp.Crystal, Size = up ? 46 : 22, Count = up ? 8 : 6, Width = up ? 60 : 360, Dir = 90 });
            Hear(Sound.Zap, crown, 0.7f);
        }

        // Eggs of crystal pressed out under the abdomen, glowing as they break.
        void EggsLaid(Body body)
        {
            var under = body.Position + new Vector3(0, 0.3f, 0);
            for (int i = 0; i < 3; i++)
            {
                var at = under + Quaternion.Euler(0, 0, 90 + i * 120) * Vector3.right * 1.6f;
                AddFlash(at, 0.7f, CrystalPink, FlashKind.Burst);
                AddBossFx(BossFx.Spray, at, new BossContext { Ramp = i % 2 == 0 ? FxRamp.Crystal : FxRamp.Cold, Size = 14, Count = 4, Width = 120, Dir = 90 });
            }
            Hear(Sound.Break, under, 0.6f);
            Hear(Sound.Squelch, under, 0.5f);
        }

        // While it winds up: motes of light drawn into the crown, glints over the bristling crystal, the floor
        // trembling under the lifted legs, the eggs glowing under the abdomen, grit pawed up before the pounce; in the
        // last phase shards of light drift off the crown.
        void CrystalQueenExtras(Body body, EntityView view)
        {
            var attack = view.BossAttack;
            float since = Time.time - body.BossSince;
            bool puff = Time.time >= body.DustAt;
            if (puff) body.DustAt = Time.time + 0.1f;
            var feet = body.Position;
            var crown = feet + new Vector3(0, 2.1f, 0);
            var middle = feet + new Vector3(0, 0.9f, 0);
            var ahead = new Vector3(view.Facing.X, view.Facing.Y, 0);
            if (view.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / TelegraphOf(body, attack));
                var windUp = attack.Shape == AttackShape.Charge ? CrystalQueenPose.Crouch : CrystalQueenArt.WindUpOf(QueenBlowOf(attack));
                switch (windUp)
                {
                    case CrystalQueenPose.Prism:
                        if (puff)
                        {
                            var from = crown + (Vector3)(Random.insideUnitCircle.normalized * (1.6f - 0.8f * progress));
                            Bits(from, crown - from, Random.value < 0.5f ? CrystalPink : PrismWhite, 1);
                        }
                        break;
                    case CrystalQueenPose.Bristle:
                        if (puff) Bits(middle + (Vector3)(Random.insideUnitCircle * 1.1f), Vector3.up * 0.3f, Random.value < 0.5f ? CrystalBlue : PrismWhite, 1);
                        break;
                    case CrystalQueenPose.Lift:
                        if (Settings.ScreenShake && progress > 0.6f) _shake = Mathf.Max(_shake, 0.03f);
                        break;
                    case CrystalQueenPose.Brood:
                        if (puff) Bits(feet + (Vector3)(Random.insideUnitCircle * 1.2f), Vector3.up * 0.4f, CrystalPink, 1);
                        break;
                    case CrystalQueenPose.Crouch:
                        if (puff) Bits(feet - ahead * 0.8f, -ahead + Vector3.up * 0.3f, DustColor, 2);
                        break;
                }
            }
            if (view.BossState == BossState.Charge)
            {
                var moved = body.Position - body.LastPosition;
                SpeedLines(feet, moved);
                if (puff) Bits(feet, -moved, CrystalBlue, 2);
            }
            if (view.Phase >= 3 && puff && Random.value < 0.3f) Bits(crown, Vector3.up, CrystalPink, 1);
            // Its heaviest (the rain, the hail, the crown's light): the room dims as the crown gathers, so the light
            // that falls is the brightest thing on the screen, and comes back after.
            bool gathers = view.BossState == BossState.Telegraph && attack != null && (attack.Id == "crystal_rain" || attack.Id == "shard_hail" || attack.Id == "venom_share");
            if (gathers) Dim(0.25f * Mathf.Clamp01(since / 0.3f));
            else if (_dim != null && _dim.color.a > 0) Dim(Mathf.Max(0, _dim.color.a - Time.deltaTime * 0.5f));
        }

        // Where one of its blows lands, crystal of its own over the blow's shape burning pink; false for anyone else's.
        bool CrystalQueenLanded(HazardState hazard)
        {
            var (boss, id) = BlowOf(hazard);
            if (boss != "crystal_queen") return false;
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            var c = new BossContext { Ramp = FxRamp.Crystal, Dir = hazard.Direction.Angle * Mathf.Rad2Deg, Size = hazard.Size * Ppu, Width = hazard.Width * Ppu };
            switch (id)
            {
                case "bite":
                {
                    // The fangs closing: a white star, two cuts and a spray of pink shards on past them.
                    BlastLanded(hazard, FxRamp.Crystal, sparks: false, light: false);
                    AddBossFx(BossFx.Fangs, at, c);
                    if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.06f);
                    Hear(Sound.Bite, at, 0.9f);
                    return true;
                }
                case "crystal_rain":
                case "shard_hail":
                {
                    // A crystal out of the roof: it stands in the floor a moment, splinters round it, and shatters.
                    bool hard = id == "shard_hail";
                    c.Big = hard;
                    BlastLanded(hazard, FxRamp.Crystal, sparks: false, chunks: false, light: false, wave: false);
                    AddBossFx(BossFx.ShardFall, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, hard ? 0.22f : 0.12f);
                    Hear(Sound.Ice, at, 0.8f);
                    if (hard) ScreenFlash(new Color(1, 0.85f, 0.95f, 0.2f), 0.05f);
                    return true;
                }
                case "web_quake":
                case "crystal_ripple":
                // A crystal of the order bursting (R4a).
                case "resonance":
                {
                    // The floor ringing: crystals stand up round it and break. The ripple's stand near its inner edge
                    // and its wave runs on out.
                    bool ring = hazard.Shape == HazardShape.Ring;
                    BlastLanded(hazard, FxRamp.Crystal, sparks: false, light: false, wave: ring);
                    if (ring) c.Inner = Mathf.Lerp(hazard.Width, hazard.Size, 0.3f) * Ppu;
                    else c.Size *= 0.86f;
                    AddBossFx(BossFx.CrystalRing, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.18f);
                    Hear(Sound.Ice, at, 0.7f);
                    return true;
                }
                case "crystal_cross":
                case "crystal_lattice":
                // The crown's light is a beam now, turning at crystals (R4a): each leg a beam of light.
                case "venom_share":
                {
                    // A beam of light split through crystal along each arm.
                    BlastLanded(hazard, FxRamp.Crystal, sparks: false, chunks: false);
                    AddBossFx(BossFx.Beam, at, c);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.15f);
                    Hear(Sound.Zap, at, 0.9f);
                    return true;
                }
            }
            // Any other of its blows: the shared landing, in crystal.
            BlastLanded(hazard, FxRamp.Crystal);
            return true;
        }

        // ---- Its fall: the legs give way and fold over it like the silk mother's, but the crystal does not grey and
        // fade: it shatters with a flash, its crown last, and the shards are thrown out over the floor. ----

        void CrystalQueenFalls(Vector3 at)
        {
            var (view, turned) = BossSectors[_queenSector];
            var renderer = Renderer(_root.transform, "Fallen queen", null, Color.white);
            renderer.transform.localPosition = at;
            renderer.sortingOrder = FootOrder(at.y);
            renderer.flipX = turned;
            _falls.Add(new Fall { Renderer = renderer, Since = Time.time, View = view, Turned = turned, Queen = true });
            Hear(Sound.Hiss, at, 0.8f);
            AddFlash(at + new Vector3(0, 2.1f, 0), 1.2f, PrismWhite, FlashKind.Impact);
            if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.18f);
            if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.4f);
            ScreenFlash(new Color(1, 0.92f, 0.98f, 0.3f), 0.15f);
        }

        const float QueenFallFrameSeconds = 0.08f;
        const float QueenFallLie = 0.6f;
        const float QueenFallFade = 0.35f;

        bool UpdateQueenFall(Fall fall)
        {
            float age = Time.time - fall.Since;
            int frames = CrystalQueenArt.FramesOf(CrystalQueenPose.Die);
            float down = frames * QueenFallFrameSeconds;
            var at = fall.Renderer.transform.localPosition;
            if (age > down + QueenFallLie + QueenFallFade)
            {
                Object.Destroy(fall.Renderer.gameObject);
                return false;
            }
            int frame = Mathf.Min(frames - 1, Mathf.FloorToInt(age / QueenFallFrameSeconds));
            if (frame != fall.Frame)
            {
                fall.Frame = frame;
                var view = fall.View;
                fall.Renderer.sprite = Sprites.Get($"queen/{view}/{CrystalQueenPose.Die}/{frame}/1", () => CrystalQueenArt.Draw(view, CrystalQueenPose.Die, frame, 1), new Vector2(0.5f, CrystalQueenArt.Floor / (float)CrystalQueenArt.Height));
                if (frame % 4 == 3) Bits(at + new Vector3(Random.Range(-0.8f, 0.8f), 0.8f, 0), Vector3.up * 0.4f, CrystalBlue, 1);
            }
            // Lying still a moment, then the crystal shatters: the brightest flash of the fight and shards everywhere.
            if (age >= down + QueenFallLie && !fall.Shattered)
            {
                fall.Shattered = true;
                AddFlash(at + new Vector3(0, 0.8f, 0), 3f, PrismWhite, FlashKind.Burst);
                AddFlash(at, 3.4f, CrystalPink, FlashKind.Ring);
                AddBossFx(BossFx.Spray, at + new Vector3(0, 0.7f, 0), new BossContext { Ramp = FxRamp.Crystal, Size = 52, Count = 16 });
                AddBossFx(BossFx.Spray, at + new Vector3(0, 0.7f, 0), new BossContext { Ramp = FxRamp.Cold, Size = 30, Count = 12 });
                if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.35f);
                Hear(Sound.Ice, at, 1);
                Hear(Sound.Break, at, 0.9f);
            }
            float gone = Mathf.Clamp01((age - down - QueenFallLie) / QueenFallFade);
            fall.Renderer.color = new Color(1, 1, 1, 1 - gone);
            return true;
        }
    }
}
