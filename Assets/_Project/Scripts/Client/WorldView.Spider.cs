using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // ---- The venom spider of the mushroom hollow (2026-11-13, Docs/Bosses.md 독그물 거미 아락사). Each attack has a
    // wind-up of the whole body (SpiderArt): reared up with the fangs spread to bite, both front pairs held up like
    // spears to stab, the abdomen swung over its back to shoot a web, the head drawn back dripping to spit, crouched and
    // gone up its own thread to fall on its mark, the front legs plucking at its webs. Its webs lie on the floor and
    // hold whoever steps in them; plucked, they glow and tremble and then burst. Where a blow lands: the picture stops a
    // moment, the screen jolts, venom splashes and stone breaks; struck, it flinches and bleeds green; felled, its legs
    // curl up under it. ----
    sealed partial class WorldView
    {
        static readonly Color VenomColor = new Color(0.62f, 0.9f, 0.36f);
        static readonly Color VenomBright = new Color(0.85f, 1f, 0.6f);
        static readonly Color SilkColor = new Color(0.92f, 0.93f, 0.97f);
        static readonly Color IchorColor = new Color(0.55f, 0.85f, 0.3f);

        static Sprite VenomSprite => Sprites.Centered("shot/venom", () => EffectArt.Venom(7));
        static Sprite EggSprite => Sprites.Centered("fx/egg", () => EffectArt.EggSac(9));

        static Sprite WebSprite(float size, bool lit)
        {
            int diameter = Mathf.Max(8, Mathf.RoundToInt(size * 2 * Sprites.PixelsPerUnit));
            return Sprites.Centered($"fx/web/{diameter}/{(lit ? 1 : 0)}", () => EffectArt.Web(diameter, lit));
        }

        // How far through a ceiling drop's telegraph it crouches, and climbs out of sight; after that it hangs over its
        // mark, coming down its thread.
        const float SpiderCrouch = 0.35f;
        const float SpiderClimb = 0.6f;
        // How long the spin of the venom spray turns, and the abdomen's settling after a spray or a brood.
        const float SpiderSpin = 0.9f;
        const float SpiderSettle = 0.3f;
        // Reared up after a roar, how long it takes to come down.
        const float SpiderRoarDown = 0.36f;

        // How far through the plucking it is (0 when it is not plucking): the webs on the floor glow and tremble.
        float _spiderPluck;
        float _spiderBurstAt;

        // The wind-up and the blow of each attack.
        static (SpiderPose WindUp, SpiderPose Blow) SpiderMoves(BossAttack attack) => attack.Shape switch
        {
            AttackShape.Trail => (SpiderPose.Raise, SpiderPose.Stab),
            AttackShape.Web => (SpiderPose.Aim, SpiderPose.Shoot),
            AttackShape.Burst => (SpiderPose.Gag, SpiderPose.Spit),
            AttackShape.Pluck => (SpiderPose.Tug, SpiderPose.Snap),
            _ => (SpiderPose.Rear, SpiderPose.Bite),
        };

        // Its pose from what it is doing: a wind-up runs through its telegraph with the blow's swing in the last moment so
        // the blow lands as the mark does, then each frame of the blow for its own time (SpiderArt.FrameSeconds), then
        // rest.
        void AnimateSpider(Body body, Vec2 facing, bool dead, bool moving, float speed)
        {
            var attack = body.BossAttack;
            float since = Time.time - body.BossSince;
            int sector = SectorOf(facing);
            var pose = SpiderPose.Idle;
            int frame = Mathf.FloorToInt(Time.time * 2.4f) % SpiderArt.FramesOf(SpiderPose.Idle);
            int Through(SpiderPose p, float progress) => Mathf.Clamp(Mathf.FloorToInt(progress * SpiderArt.FramesOf(p)), 0, SpiderArt.FramesOf(p) - 1);
            // A blow from its strike on, each frame for its own time; false once it is over.
            bool After(SpiderPose blow, float elapsed)
            {
                for (int f = SpiderArt.StrikeFrame(blow); f < SpiderArt.FramesOf(blow); f++)
                {
                    elapsed -= SpiderArt.FrameSeconds(blow, f);
                    if (elapsed >= 0) continue;
                    (pose, frame) = (blow, f);
                    return true;
                }
                return false;
            }
            if (dead)
            {
                (pose, frame) = (SpiderPose.Die, SpiderArt.FramesOf(SpiderPose.Die) - 1);
            }
            else if (body.BossState == BossState.Roar)
            {
                // Reared up with every front leg spread, then shaking there.
                pose = SpiderPose.Roar;
                int rise = Mathf.FloorToInt(since / 0.06f);
                frame = rise < SpiderArt.RoarRise ? rise : SpiderArt.RoarRise + Mathf.FloorToInt(Time.time * 12) % 4;
            }
            else if (body.BossWas == BossState.Roar && since < SpiderRoarDown && body.BossState != BossState.Telegraph)
            {
                (pose, frame) = (SpiderPose.Roar, Mathf.Max(0, SpiderArt.RoarRise - 1 - Mathf.FloorToInt(since / SpiderRoarDown * SpiderArt.RoarRise)));
            }
            else if (body.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / Mathf.Max(0.1f, attack.Telegraph));
                switch (attack.Shape)
                {
                    case AttackShape.Leap:
                        // Crouched, then up its thread and out of sight; it hangs over the mark (SpiderExtras).
                        if (progress < SpiderCrouch) (pose, frame) = (SpiderPose.Crouch, Through(SpiderPose.Crouch, progress / SpiderCrouch));
                        else
                        {
                            pose = SpiderPose.Climb;
                            frame = Through(SpiderPose.Climb, (progress - SpiderCrouch) / (SpiderClimb - SpiderCrouch));
                        }
                        break;
                    case AttackShape.Summon:
                        (pose, frame) = (SpiderPose.Brood, Through(SpiderPose.Brood, progress));
                        break;
                    case AttackShape.Spiral:
                        (pose, frame) = (SpiderPose.Aim, Through(SpiderPose.Aim, progress));
                        break;
                    default:
                    {
                        var (windUp, blow) = SpiderMoves(attack);
                        int strike = SpiderArt.StrikeFrame(blow);
                        float swing = 0;
                        for (int f = 0; f < strike; f++) swing += SpiderArt.FrameSeconds(blow, f);
                        float wound = 1 - Mathf.Min(0.5f, swing / Mathf.Max(0.1f, attack.Telegraph));
                        if (progress < wound) (pose, frame) = (windUp, Through(windUp, progress / wound));
                        else
                        {
                            float into = (progress - wound) * attack.Telegraph;
                            pose = blow;
                            frame = 0;
                            for (int f = 0; f < strike - 1 && into >= SpiderArt.FrameSeconds(blow, f); f++)
                            {
                                into -= SpiderArt.FrameSeconds(blow, f);
                                frame = f + 1;
                            }
                        }
                        break;
                    }
                }
            }
            else if (body.BossState == BossState.Recover && attack != null && attack.Shape == AttackShape.Summon && since < SpiderSettle)
            {
                (pose, frame) = (SpiderPose.Brood, Mathf.Max(0, SpiderArt.FramesOf(SpiderPose.Brood) - 1 - Mathf.FloorToInt(since / SpiderSettle * SpiderArt.FramesOf(SpiderPose.Brood))));
            }
            else if (body.BossState == BossState.Recover && attack != null && attack.Shape == AttackShape.Spiral && since < SpiderSpin + SpiderSettle)
            {
                // Turning on the spot spraying venom all round, then settling.
                if (since < SpiderSpin)
                {
                    pose = SpiderPose.Spin;
                    frame = Mathf.FloorToInt(since * 26) % SpiderArt.FramesOf(pose);
                    sector = (sector + Mathf.FloorToInt(since * 14)) % 8;
                }
                else (pose, frame) = (SpiderPose.Aim, Mathf.Max(0, SpiderArt.FramesOf(SpiderPose.Aim) - 1 - Mathf.FloorToInt((since - SpiderSpin) / SpiderSettle * SpiderArt.FramesOf(SpiderPose.Aim))));
            }
            else if (body.BossState == BossState.Recover && attack != null && attack.Shape != AttackShape.Summon && attack.Shape != AttackShape.Spiral
                && After(attack.Shape == AttackShape.Leap ? SpiderPose.Fall : SpiderMoves(attack).Blow, since))
            {
                // The blow plays on (After set it).
            }
            else if (moving)
            {
                // Its legs in their tetrapods: quicker when it darts.
                pose = SpiderPose.Walk;
                frame = Mathf.FloorToInt(Time.time * (speed > 5 ? 40 : 20)) % SpiderArt.FramesOf(pose);
            }
            else if (body.FlashTime > 0)
            {
                // Struck: a flinch.
                (pose, frame) = (SpiderPose.Flinch, Mathf.Clamp(Mathf.FloorToInt((1 - body.FlashTime / FlashSeconds) * 4), 0, 3));
            }

            var (view, turned) = BossSectors[sector];
            body.SpiderPose = pose;
            body.SpiderFrame = frame;
            body.SpiderView = view;
            body.SpiderTurned = turned;
            // Only the last phase looks different (its markings burn), so the first two share their pictures.
            int phase = body.BossPhase >= 3 ? 3 : 1;
            int code = 600000 + ((((int)pose * 8 + (int)view) * 64 + frame) * 4 + phase) * 2 + (turned ? 1 : 0);
            if (code == body.Frame) return;
            // Each set of legs coming down ticks on the stone.
            if (pose == SpiderPose.Walk && frame % 4 == 0 && Time.time >= body.SkitterAt)
            {
                body.SkitterAt = Time.time + 0.3f;
                Hear(Sound.Skitter, body.Position, 0.5f);
            }
            body.Frame = code;
            string key = $"spider/{view}/{pose}/{frame}/{phase}";
            System.Func<PixelImage> draw = () => SpiderArt.Draw(view, pose, frame, phase);
            var pivot = new Vector2(0.5f, SpiderArt.Floor / (float)SpiderArt.Height);
            body.Shape.sprite = Sprites.Get(key, draw, pivot);
            // Its white picture waits until a flash shows it (WhiteOf), like the samurai's.
            body.White = () => Sprites.Get(key + "/white", () => draw().Silhouette(), pivot);
            body.Shape.flipX = turned;
            body.Glow.flipX = turned;
            body.Top = -body.Feet + SpiderArt.BodyHeight / (float)Sprites.PixelsPerUnit;
        }

        // A point of its picture (canvas pixels) in the world, as it is drawn now.
        Vector3 SpiderPoint(Body body, (float X, float Y) canvas)
        {
            float x = (canvas.X - SpiderArt.Width / 2f) / Sprites.PixelsPerUnit;
            float y = (SpiderArt.Height - SpiderArt.Floor - canvas.Y) / Sprites.PixelsPerUnit;
            return body.Position + new Vector3(body.SpiderTurned ? -x : x, y - body.Feet, 0);
        }

        Vector3 SpiderHead(Body body) => SpiderPoint(body, SpiderArt.HeadOf(body.SpiderView, body.SpiderPose, body.SpiderFrame));

        Vector3 SpiderSpinnerets(Body body) => SpiderPoint(body, SpiderArt.SpinneretsOf(body.SpiderView, body.SpiderPose, body.SpiderFrame));

        // A thread of silk from one point to another, left a moment and fading.
        void Thread(Vector3 from, Vector3 to, float life, Color color)
        {
            var line = Renderer(_root.transform, "Thread", Shapes.Square, color);
            var way = to - from;
            line.transform.localPosition = (from + to) / 2;
            line.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(way.y, way.x) * Mathf.Rad2Deg);
            line.transform.localScale = new Vector3(way.magnitude, 1f / Sprites.PixelsPerUnit, 1);
            line.sortingOrder = 19400;
            _bits.Add(new Bit { Renderer = line, Velocity = Vector3.zero, Color = color, Life = life, Drag = 0 });
        }

        // Drops of venom thrown one way (or all round when toward is zero), falling as they go.
        void Venom(Vector3 at, Vector3 toward, int count, float speed)
        {
            if (!Feel.Bits) return;
            var way = toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                var drop = Renderer(_root.transform, "Venom", Shapes.Square, i % 3 == 0 ? VenomBright : VenomColor);
                drop.transform.localPosition = at + (Vector3)(Random.insideUnitCircle * 0.2f);
                drop.transform.localScale = Vector3.one * ((Random.value < 0.5f ? 2f : 3f) / Sprites.PixelsPerUnit);
                drop.sortingOrder = 19500;
                var velocity = (way + (Vector3)(Random.insideUnitCircle * (way == Vector3.zero ? 1 : 0.5f))) * speed * Random.Range(0.6f, 1.2f);
                _bits.Add(new Bit { Renderer = drop, Velocity = velocity + Vector3.up * 1.2f, Color = i % 3 == 0 ? VenomBright : VenomColor, Life = 0.55f, Drag = 3, Fall = new Vector3(0, -7, 0) });
            }
        }

        // Spores of the hollow drifting up through the air round it, thicker each phase.
        void SporeDrift(int phase)
        {
            if (!Feel.Bits || Random.value > 0.12f + 0.08f * phase) return;
            float high = _camera.orthographicSize, wide = high * _camera.aspect;
            var middle = _camera.transform.position - _root.transform.position;
            var at = new Vector3(middle.x + Random.Range(-wide, wide), middle.y + Random.Range(-high, high * 0.6f), 0);
            var color = Random.value < 0.7f ? new Color(0.7f, 1f, 0.55f, 0.7f) : new Color(0.85f, 0.7f, 1f, 0.6f);
            var mote = Renderer(_root.transform, "Spore", Shapes.Square, color);
            mote.transform.localPosition = at;
            mote.transform.localScale = Vector3.one * (2f / Sprites.PixelsPerUnit);
            mote.sortingOrder = 19300;
            _bits.Add(new Bit { Renderer = mote, Velocity = new Vector3(0, 0.5f, 0), Color = color, Life = 2.4f, Drag = 0, Sway = 0.4f, Phase = Random.Range(0, 6.3f), LateFade = true });
        }

        // While it winds up: venom dripping from its fangs, silk gathering at its spinnerets, the webs glowing as it
        // plucks them (and the room dimming), its thread up to the roof and its body coming down it over the mark; the
        // spores of the hollow drifting all through the fight.
        void SpiderExtras(Body body, EntityView view)
        {
            var attack = view.BossAttack;
            float since = Time.time - body.BossSince;
            bool puff = Time.time >= body.DustAt;
            if (puff) body.DustAt = Time.time + 0.08f;
            if (puff) SporeDrift(Mathf.Clamp(body.BossPhase, 1, 3));
            _spiderPluck = 0;
            bool hanging = false;
            if (view.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / Mathf.Max(0.1f, attack.Telegraph));
                if (!body.Hissed && attack.Shape != AttackShape.Leap)
                {
                    body.Hissed = true;
                    Hear(attack.Shape == AttackShape.Web ? Sound.Web : Sound.Hiss, body.Position, 0.8f);
                }
                switch (attack.Shape)
                {
                    case AttackShape.Cone:
                    case AttackShape.Burst:
                        // Venom welling at the fangs and dripping.
                        if (puff && progress > 0.25f && Random.value < 0.6f) Venom(SpiderHead(body), Vector3.down, 1, 0.6f);
                        if (puff && attack.Shape == AttackShape.Burst && progress > 0.5f) AddFlash(SpiderHead(body), 0.35f + 0.3f * progress, VenomColor, FlashKind.Sparkles);
                        break;
                    case AttackShape.Web:
                    case AttackShape.Spiral:
                    case AttackShape.Summon:
                        // Silk motes (or venom) drawn in to the spinnerets, the markings brightening.
                        if (puff)
                        {
                            var tip = SpiderSpinnerets(body);
                            var from = tip + (Vector3)(Random.insideUnitCircle.normalized * 1.2f);
                            Kick(from, tip - from, attack.Shape == AttackShape.Web ? SilkColor : VenomColor, 1);
                        }
                        if (attack.Shape == AttackShape.Summon && puff && progress > 0.4f && Random.value < 0.5f) Venom(SpiderSpinnerets(body), Vector3.down, 1, 0.5f);
                        break;
                    case AttackShape.Pluck:
                    {
                        // Every web on the floor glows and trembles; each pluck twangs; the room darkens round the glow.
                        _spiderPluck = Mathf.Max(0.01f, progress);
                        Dim(0.45f * progress);
                        int plucks = progress < 0.3f ? 0 : 1 + Mathf.FloorToInt((progress - 0.3f) / 0.7f * 2);
                        if (plucks > body.Twangs)
                        {
                            body.Twangs = plucks;
                            Hear(Sound.Web, body.Position, 1);
                            Hear(Sound.Click, body.Position, 0.8f);
                            AddFlash(body.Position + new Vector3(0, 0.6f, 0), 1.4f, VenomColor, FlashKind.Ring);
                        }
                        break;
                    }
                    case AttackShape.Leap:
                    {
                        // Its thread up to the roof as it climbs; then down it comes over the mark, its shadow growing.
                        if (progress >= SpiderCrouch && progress < SpiderClimb && puff)
                            Thread(SpiderSpinnerets(body), SpiderSpinnerets(body) + new Vector3(0, 9, 0), 0.12f, SilkColor);
                        if (progress >= SpiderClimb && LandingOf(attack, out var at))
                        {
                            hanging = true;
                            float down = (progress - SpiderClimb) / (1 - SpiderClimb);
                            float height = 9 * (1 - down * down);
                            ShowFaller(body, at, height, down);
                            if (!body.Hissed)
                            {
                                body.Hissed = true;
                                Hear(Sound.Hiss, at, 1);
                            }
                        }
                        break;
                    }
                }
                if (attack.Warning == AttackWarning.Strong && Settings.ScreenShake && puff) _shake = Mathf.Max(_shake, 0.02f + 0.04f * progress);
            }
            else
            {
                body.Hissed = false;
                body.Twangs = 0;
            }
            if (!hanging) HideFaller(body);
            // Gone up its thread: the body where it stood is not there (set here, after Place has tinted it).
            if (hanging)
            {
                body.Shape.color = Color.clear;
                body.Glow.color = Color.clear;
                body.Shadow.enabled = false;
            }
            // The room darkens only while it plucks.
            if (_spiderPluck <= 0 && body.Plucked) Dim(0);
            body.Plucked = _spiderPluck > 0;
            // In the last phase venom drips from it as it goes.
            if (puff && body.BossPhase >= 3 && Random.value < 0.15f) Venom(SpiderSpinnerets(body), Vector3.down, 1, 0.4f);
            // The spray: venom flung out all round as it turns.
            if (view.BossState == BossState.Recover && attack != null && attack.Shape == AttackShape.Spiral && since < SpiderSpin && puff)
                Venom(SpiderSpinnerets(body), (Vector3)Random.insideUnitCircle, 2, 3);
        }

        // Its body coming down its thread onto the mark, with the thread over it and its shadow on the floor below.
        void ShowFaller(Body body, Vector3 at, float height, float down)
        {
            if (body.Faller == null)
            {
                body.Faller = Renderer(body.Root, "Falling spider", null, Color.white);
                body.FallerThread = Renderer(body.Root, "Thread", Shapes.Square, SilkColor);
                body.FallerShadow = Renderer(body.Root, "Shadow", Shapes.Circle, new Color(0, 0, 0, 0.3f));
            }
            var view = body.SpiderView;
            int phase = body.BossPhase >= 3 ? 3 : 1;
            body.Faller.sprite = Sprites.Get($"spider/hang/{view}/{phase}", () => SpiderArt.Hanging(view, phase), new Vector2(0.5f, SpiderArt.Floor / (float)SpiderArt.Height));
            body.Faller.flipX = body.SpiderTurned;
            body.Faller.enabled = body.FallerThread.enabled = body.FallerShadow.enabled = true;
            var local = at - body.Position;
            body.Faller.transform.localPosition = local + new Vector3(0, height - body.Feet, 0);
            body.Faller.sortingOrder = 21000;
            float top = height + 12;
            body.FallerThread.transform.localPosition = local + new Vector3(0, height + 1.6f + (top - height) / 2, 0);
            body.FallerThread.transform.localScale = new Vector3(1f / Sprites.PixelsPerUnit, top - height, 1);
            body.FallerThread.sortingOrder = 20990;
            body.FallerShadow.transform.localPosition = local;
            body.FallerShadow.transform.localScale = new Vector3(1.6f + 1.4f * down, (1.6f + 1.4f * down) * 0.5f, 1);
            body.FallerShadow.color = new Color(0, 0, 0, 0.15f + 0.3f * down);
            body.FallerShadow.sortingOrder = DarkOrder + 12;
        }

        void HideFaller(Body body)
        {
            if (body.Faller == null) return;
            body.Faller.enabled = body.FallerThread.enabled = body.FallerShadow.enabled = false;
        }

        // The moment an attack goes: the fangs snapping, a thread shot to each web, venom spat, the egg sacs dropped and
        // bursting, the spray beginning.
        void SpiderUnleashes(Body body)
        {
            var attack = body.BossAttack;
            if (attack == null) return;
            switch (attack.Shape)
            {
                case AttackShape.Cone:
                    body.LungeReach = 3;
                    Hear(Sound.Squelch, body.Position, 1);
                    break;
                case AttackShape.Web:
                {
                    // A thread out to each web as it flies.
                    int cause = _bossInView == null ? 0 : IndexOf(_bossInView, attack) + 1;
                    var from = SpiderSpinnerets(body);
                    foreach (var mark in _marks.Values)
                    {
                        if (mark.State.Cause != cause || mark.State.Shape == HazardShape.Web) continue;
                        Thread(from, new Vector3(mark.State.Position.X, mark.State.Position.Y, 0), 0.3f, SilkColor);
                    }
                    Hear(Sound.Web, body.Position, 1);
                    break;
                }
                case AttackShape.Burst:
                    AddFlash(SpiderHead(body), 0.9f, VenomColor, FlashKind.Burst);
                    Venom(SpiderHead(body), (Vector3)(Vector2)body.Lunge, 5, 3);
                    Hear(Sound.Squelch, body.Position, 0.7f);
                    Hear(Sound.Shoot, body.Position, 0.6f);
                    break;
                case AttackShape.Spiral:
                    Hear(Sound.Hiss, body.Position, 1);
                    AddFlash(body.Position + new Vector3(0, 0.6f, 0), 2.2f, VenomColor, FlashKind.Vortex);
                    break;
                case AttackShape.Summon:
                    // Three egg sacs dropped round it, splitting open a moment later.
                    for (int i = 0; i < 3; i++)
                    {
                        float angle = (i + Random.value * 0.5f) * Mathf.PI * 2 / 3;
                        var at = body.Position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * attack.Spread;
                        var egg = Renderer(_root.transform, "Egg sac", EggSprite, Color.white);
                        egg.transform.localPosition = at;
                        egg.sortingOrder = 19400;
                        _bits.Add(new Bit { Renderer = egg, Velocity = Vector3.zero, Color = Color.white, Life = 0.3f, Drag = 0 });
                        Later(0.25f, () =>
                        {
                            AddFlash(at, 0.9f, VenomColor, FlashKind.Burst);
                            Kick(at, Vector3.up, SilkColor, 4);
                            Venom(at, Vector3.zero, 3, 2);
                            Hear(Sound.Squelch, at, 0.5f);
                        });
                    }
                    Hear(Sound.Web, body.Position, 0.7f);
                    break;
                case AttackShape.Pluck:
                    ScreenFlash(new Color(0.75f, 1f, 0.5f, 0.3f), 0.12f);
                    break;
            }
        }

        static int IndexOf(BossDefinition boss, BossAttack attack)
        {
            for (int i = 0; i < boss.Attacks.Count; i++)
                if (boss.Attacks[i] == attack) return i;
            return -1;
        }

        // Struck: green ichor out of the far side, and a flinch (AnimateSpider).
        void SpiderStruck(Body body, Body hitter, bool heavy)
        {
            if (!Feel.Bits) return;
            var away = hitter != null ? body.Position - hitter.Position : Vector3.up;
            Venom(body.Position + new Vector3(0, 0.7f, 0), away, heavy ? 5 : 2, heavy ? 3.5f : 2.2f);
            if (heavy) AddFlash(body.Position + new Vector3(0, 0.7f, 0), 0.7f, IchorColor, FlashKind.Burst);
        }

        // A web on the floor (HazardShape.Web): fading in where it landed and out at its end; plucked, it glows and
        // trembles; whoever stands in it is caught in strands.
        void SpiderWeb(Mark mark, HazardView view)
        {
            var hazard = view.State;
            bool lit = _spiderPluck > 0;
            mark.Edge.color = Color.clear;
            mark.Fill.sprite = WebSprite(hazard.Size, lit);
            float alpha = Mathf.Clamp01(view.Progress * 25) * Mathf.Clamp01((1 - view.Progress) * 8);
            float glow = lit ? 0.6f + 0.4f * Mathf.Sin(Time.time * 30) : 0;
            mark.Fill.color = lit ? new Color(1, 1, 1, alpha) : new Color(1, 1, 1, alpha * 0.85f);
            float shiver = lit ? 1 + 0.04f * glow * _spiderPluck : 1;
            mark.Fill.transform.localScale = Vector3.one * shiver;
            mark.Fill.transform.localPosition = lit ? (Vector3)(Random.insideUnitCircle * 0.04f * _spiderPluck) : Vector3.zero;
            if (!Feel.Bits || Time.time < mark.StuckAt) return;
            mark.StuckAt = Time.time + 0.18f;
            var middle = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            foreach (var who in Stuck())
            {
                if (Vector3.Distance(who.Position, middle) > hazard.Size) continue;
                // Strands clinging round the feet.
                Kick(who.Position + new Vector3(0, 0.1f, 0), Vector3.up * 0.2f, SilkColor, 1);
                Thread(who.Position + new Vector3(-0.3f, 0.05f, 0), who.Position + new Vector3(0.3f, 0.25f, 0), 0.2f, SilkColor);
            }
        }

        IEnumerable<Body> Stuck()
        {
            yield return _self;
            foreach (var body in _others.Values)
                if (body.Kind == BodyKind.Player) yield return body;
        }

        // Where one of its attacks lands, a burst of its own; false for anyone else's.
        bool SpiderLanded(HazardState hazard)
        {
            var attack = AttackOf(hazard);
            if (attack == null || _bossInView.Id != "venom_spider") return false;
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            var along = new Vector3(hazard.Direction.X, hazard.Direction.Y, 0);
            switch (attack.Shape)
            {
                case AttackShape.Cone:
                {
                    // The fangs closing: a white snap where they meet, venom sprayed ahead, the picture caught a moment.
                    var bite = at + along * hazard.Size * 0.55f;
                    AddFlash(bite, 1.3f, Color.white, FlashKind.Impact);
                    AddFlash(bite, hazard.Size * 0.7f, VenomColor, FlashKind.Burst);
                    Venom(bite, along, 7, 3.5f);
                    if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.08f);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.3f);
                    Hear(Sound.Squelch, bite, 1);
                    Hear(Sound.Crit, bite, 0.5f);
                    return true;
                }
                case AttackShape.Trail:
                    // A leg spearing the floor: a white point, stone broken round it.
                    AddFlash(at, 0.7f, Color.white, FlashKind.Impact);
                    AddFlash(at, hazard.Size, RockColor, FlashKind.Burst);
                    Kick(at, Vector3.up, RockColor, 3);
                    Cracks(at, hazard.Size * 0.8f, 3);
                    if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.035f);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.15f);
                    Hear(Sound.Thump, at, 0.7f);
                    return true;
                case AttackShape.Web:
                    // The web splatting open on the floor.
                    AddFlash(at, hazard.Size * 1.3f, SilkColor, FlashKind.Ring);
                    for (int i = 0; i < 8; i++) Kick(at, Quaternion.Euler(0, 0, i * 45) * Vector3.right, SilkColor, 1);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.08f);
                    Hear(Sound.Web, at, 0.6f);
                    return true;
                case AttackShape.Leap:
                    // Down on its mark from the roof: the heaviest landing, venom thrown all round.
                    Impact(at, 1.6f);
                    AddFlash(at, hazard.Size * 1.5f, VenomColor, FlashKind.Ring);
                    Venom(at, Vector3.zero, 12, 4);
                    ScreenFlash(new Color(0.7f, 1f, 0.45f, 0.28f), 0.14f);
                    Hear(Sound.Squelch, at, 1);
                    return true;
                case AttackShape.Pluck:
                {
                    // Each web bursting in venom; the screen's jolt and stop once for them all.
                    AddFlash(at, hazard.Size * 1.2f, VenomColor, FlashKind.Burst);
                    AddFlash(at, hazard.Size * 1.5f, VenomBright, FlashKind.Ring);
                    Venom(at, Vector3.zero, 6, 3.5f);
                    for (int i = 0; i < 6; i++) Kick(at, Quaternion.Euler(0, 0, i * 60 + 15) * Vector3.right, SilkColor, 1);
                    if (Time.time - _spiderBurstAt > 0.1f)
                    {
                        _spiderBurstAt = Time.time;
                        if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.12f);
                        if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.45f);
                        ScreenFlash(new Color(0.75f, 1f, 0.5f, 0.35f), 0.16f);
                        Hear(Sound.Squelch, at, 1);
                        Hear(Sound.Boom, at, 0.6f);
                    }
                    return true;
                }
            }
            return false;
        }

        // Felled: its legs curling up under it where it fell, then fading (SpiderArt Die).
        readonly List<(SpriteRenderer Renderer, float Start, BossView View)> _spiderRemains = new List<(SpriteRenderer, float, BossView)>();

        void SpiderFalls(Vector3 at, BossView view, bool turned)
        {
            var remains = Renderer(_root.transform, "Fallen spider", null, Color.white);
            remains.transform.localPosition = at;
            remains.flipX = turned;
            remains.sortingOrder = 10000 - Mathf.RoundToInt(at.y * 10);
            _spiderRemains.Add((remains, Time.time, view));
            AddFlash(at + new Vector3(0, 0.6f, 0), 2.4f, VenomColor, FlashKind.Burst);
            AddFlash(at, 3, VenomColor, FlashKind.Ring);
            Venom(at + new Vector3(0, 0.6f, 0), Vector3.zero, 14, 4);
            Hear(Sound.Squelch, at, 1);
            Hear(Sound.Hiss, at, 0.8f);
            if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.15f);
            if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.4f);
        }

        const float RemainsCurl = 1.1f;
        const float RemainsStay = 2.4f;
        const float RemainsFade = 1.2f;

        void UpdateSpiderRemains()
        {
            for (int i = _spiderRemains.Count - 1; i >= 0; i--)
            {
                var (renderer, start, view) = _spiderRemains[i];
                float t = Time.time - start;
                if (t > RemainsStay + RemainsFade)
                {
                    Object.Destroy(renderer.gameObject);
                    _spiderRemains.RemoveAt(i);
                    continue;
                }
                int count = SpiderArt.FramesOf(SpiderPose.Die);
                int frame = Mathf.Clamp(Mathf.FloorToInt(t / RemainsCurl * count), 0, count - 1);
                renderer.sprite = Sprites.Get($"spider/{view}/{SpiderPose.Die}/{frame}/1", () => SpiderArt.Draw(view, SpiderPose.Die, frame, 1),
                    new Vector2(0.5f, SpiderArt.Floor / (float)SpiderArt.Height));
                renderer.color = new Color(1, 1, 1, Mathf.Clamp01(1 - (t - RemainsStay) / RemainsFade));
            }
        }
    }
}
