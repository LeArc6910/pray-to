using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // ---- The silk mother of the forgotten shrine (2026-11-13, Docs/Bosses.md 실 잣는 어미 아라네): a spider that takes
    // the arena piece by piece. Every attack has a wind-up of its own that the whole body takes part in (SpiderArt):
    // reared with the front legs high for its fangs, legs cocked back over its head for the stabs, the abdomen swung
    // over its back to spin, pressed flat to spring, drawn back with the abdomen pumping to spit, hauled up its thread
    // to come down on someone, sat on its belly with all eight legs up like a crown. Its silk and venom are thrown
    // while the marks still fill and land as they finish, and stay: webs that snare, pools that burn. Blows land with
    // a stop of the picture, a jolt, cracks, dust and spatter. ----
    sealed partial class WorldView
    {
        static readonly Color VenomColor = new Color(0.62f, 0.9f, 0.36f);
        static readonly Color VenomBright = new Color(0.84f, 1f, 0.6f);
        static readonly Color SilkColor = new Color(0.93f, 0.93f, 0.97f);
        static readonly Color IchorColor = new Color(0.45f, 0.75f, 0.3f);

        static Sprite VenomGlobSprite => Sprites.Centered("shot/venom", () => EffectArt.Glob(7, false));
        static Sprite SilkGlobSprite => Sprites.Centered("shot/silk", () => EffectArt.Glob(9, true));

        // How far through its telegraph a thrown thing (silk, a gob of venom) leaves the spider, to land as the mark
        // fills; and how far through a drop's telegraph it has gathered itself and starts up its thread, and is gone.
        const float SpiderThrowAt = 0.68f;
        const float SpiderGatherEnd = 0.38f;
        const float SpiderGoneAt = 0.72f;

        static bool IsPool(HazardState hazard) => hazard.Shape == HazardShape.Web || hazard.Shape == HazardShape.Venom;

        // Webs and pools on the ground now, for the playtests.
        public int PoolsShown => _marks.Values.Count(m => IsPool(m.State));

        // An attack whose blow is thrown and lands a moment later: circles laid away from it that leave silk or venom.
        static bool Thrown(BossAttack attack) => attack.Shape == AttackShape.Circle && attack.Pool != PoolKind.None;

        // The wind-up and the blow of each attack, by what it does.
        static (SpiderPose WindUp, SpiderPose Blow) SpiderMoves(BossAttack attack) => attack.Shape switch
        {
            AttackShape.Cross => (SpiderPose.Splay, SpiderPose.Slam),
            AttackShape.Burst => (SpiderPose.Gape, SpiderPose.Spit),
            AttackShape.Circle when attack.Pool == PoolKind.Venom => (SpiderPose.Gape, SpiderPose.Spit),
            AttackShape.Circle when attack.Pool == PoolKind.Web => (SpiderPose.Spin, SpiderPose.Shoot),
            AttackShape.Circle or AttackShape.Trail => (SpiderPose.Cock, SpiderPose.Stab),
            _ => (SpiderPose.Rear, SpiderPose.Bite),
        };

        // Seconds from the wind-up's end to the strike: the swing frames.
        static float SpiderSwing(SpiderPose blow)
        {
            float seconds = 0;
            for (int f = 0; f < SpiderArt.StrikeFrame(blow); f++) seconds += SpiderArt.FrameSeconds(blow, f);
            return seconds;
        }

        // The blow's frame a time after its strike (negative: still swinging); false once it is over.
        static bool BlowFrame(SpiderPose blow, float sinceStrike, out int frame)
        {
            int strike = SpiderArt.StrikeFrame(blow);
            if (sinceStrike < 0)
            {
                frame = Mathf.Clamp(strike + Mathf.FloorToInt(sinceStrike / SpiderArt.FrameSeconds(blow, 0)), 0, strike - 1);
                return true;
            }
            float left = sinceStrike;
            for (frame = strike; frame < SpiderArt.FramesOf(blow); frame++)
            {
                left -= SpiderArt.FrameSeconds(blow, frame);
                if (left < 0) return true;
            }
            frame = 0;
            return false;
        }

        // Its pose from what it is doing. A wind-up runs through the telegraph up to the swing, the swing fills the
        // moment before the strike, and the strike falls as the marks land (or, thrown, SpiderThrowAt of the way
        // through, the silk in the air for the rest); after it each frame of the blow for its own time, then rest.
        void AnimateSpider(Body body, Vec2 facing, bool dead, bool moving)
        {
            var attack = body.BossAttack;
            float since = Time.time - body.BossSince;
            int sector = SectorOf(facing);
            var pose = SpiderPose.Idle;
            int frame = Mathf.FloorToInt(Time.time * 5) % SpiderArt.FramesOf(SpiderPose.Idle);
            float alpha = 1;
            int Through(SpiderPose p, float progress) => Mathf.Clamp(Mathf.FloorToInt(progress * SpiderArt.FramesOf(p)), 0, SpiderArt.FramesOf(p) - 1);
            if (dead)
            {
                frame = 0;
            }
            else if (body.BossState == BossState.Roar)
            {
                pose = SpiderPose.Screech;
                int rise = Mathf.FloorToInt(since / 0.06f);
                frame = rise < SpiderArt.ScreechRise ? rise : SpiderArt.ScreechRise + Mathf.FloorToInt(Time.time * 14) % 4;
            }
            else if (body.BossState == BossState.Charge)
            {
                // The spring out of the crouch, then running flat out.
                int spring = Mathf.FloorToInt(since / 0.025f);
                if (spring < SpiderArt.FramesOf(SpiderPose.Spring)) (pose, frame) = (SpiderPose.Spring, spring);
                else (pose, frame) = (SpiderPose.Rush, Mathf.FloorToInt(Time.time * 26) % SpiderArt.FramesOf(SpiderPose.Rush));
            }
            else if (moving && (body.BossState == BossState.Approach || body.BossState == BossState.Recover || body.BossState == BossState.Dormant))
            {
                // Scuttling: a stride each way about every tile and a half.
                pose = SpiderPose.Walk;
                frame = Mathf.FloorToInt(body.Walked * 26) % SpiderArt.FramesOf(pose);
            }
            else if (body.BossState == BossState.Telegraph && attack != null)
            {
                float telegraph = Mathf.Max(0.1f, body.BossAgain ? attack.Telegraph * World.AgainTelegraph : attack.Telegraph);
                float progress = Mathf.Clamp01(since / telegraph);
                switch (attack.Shape)
                {
                    case AttackShape.Charge:
                        (pose, frame) = (SpiderPose.Crouch, Through(SpiderPose.Crouch, progress));
                        break;
                    case AttackShape.Burrow:
                    case AttackShape.Leap:
                        // Legs in under its thread, then hauled up out of sight; it comes down where the mark is.
                        if (progress < SpiderGatherEnd) (pose, frame) = (SpiderPose.Gather, Through(SpiderPose.Gather, progress / SpiderGatherEnd));
                        else
                        {
                            (pose, frame) = (SpiderPose.Rise, Through(SpiderPose.Rise, (progress - SpiderGatherEnd) / (SpiderGoneAt - SpiderGatherEnd)));
                            alpha = Mathf.Clamp01((SpiderGoneAt + 0.04f - progress) * 25);
                        }
                        break;
                    case AttackShape.Summon:
                    {
                        pose = SpiderPose.Screech;
                        int rise = Mathf.FloorToInt(since / 0.06f);
                        frame = rise < SpiderArt.ScreechRise ? rise : SpiderArt.ScreechRise + Mathf.FloorToInt(Time.time * 14) % 4;
                        break;
                    }
                    default:
                    {
                        var (windUp, blow) = SpiderMoves(attack);
                        // When the strike falls: as the marks land, or earlier for what is thrown.
                        float strikeAt = Thrown(attack) ? telegraph * SpiderThrowAt : telegraph;
                        float wound = Mathf.Max(strikeAt * 0.5f, strikeAt - SpiderSwing(blow));
                        if (since < wound) (pose, frame) = (windUp, Through(windUp, since / wound));
                        else if (BlowFrame(blow, since - strikeAt, out int f)) (pose, frame) = (blow, f);
                        break;
                    }
                }
            }
            else if (body.BossState == BossState.Recover && attack != null)
            {
                switch (attack.Shape)
                {
                    case AttackShape.Charge:
                    {
                        // Out of the run onto its braking legs.
                        int skid = Mathf.FloorToInt(since / 0.05f);
                        if (skid < SpiderArt.FramesOf(SpiderPose.Skid)) (pose, frame) = (SpiderPose.Skid, skid);
                        break;
                    }
                    case AttackShape.Summon:
                        break;
                    case AttackShape.Burrow:
                    case AttackShape.Leap:
                        if (BlowFrame(SpiderPose.Land, since, out int landed)) (pose, frame) = (SpiderPose.Land, landed);
                        break;
                    default:
                    {
                        var (_, blow) = SpiderMoves(attack);
                        // A thrown blow struck before the telegraph ended.
                        float before = Thrown(attack) ? attack.Telegraph * (1 - SpiderThrowAt) : 0;
                        if (BlowFrame(blow, since + before, out int f)) (pose, frame) = (blow, f);
                        break;
                    }
                }
            }
            // Struck while it stands: knocked back off its front feet for a moment.
            if (pose == SpiderPose.Idle && !dead && body.HurtTime > 0 && body.BossState != BossState.Dormant)
                (pose, frame) = (SpiderPose.Flinch, Mathf.Clamp(Mathf.FloorToInt((HurtSeconds - body.HurtTime) / 0.04f), 0, SpiderArt.FramesOf(SpiderPose.Flinch) - 1));

            var (view, turned) = BossSectors[sector];
            _spiderSector = sector;
            int phase = Mathf.Clamp(body.BossPhase, 1, 3);
            body.SpiderAlpha = alpha;
            int code = 600000 + ((((int)pose * 8 + (int)view) * 64 + frame) * 4 + phase) * 2 + (turned ? 1 : 0);
            if (code == body.Frame) return;
            // A foot coming down as it scuttles: a dry tick, now and then a puff.
            if (pose == SpiderPose.Walk && frame % 3 == 0 && body.SpiderStep != frame)
            {
                Hear(Sound.Skitter, body.Position, 0.5f);
                if (frame % 6 == 0) Kick(body.Position + new Vector3(UnityEngine.Random.Range(-1f, 1f), -0.3f, 0), Vector3.up * 0.3f, DustColor, 1);
            }
            body.SpiderStep = pose == SpiderPose.Walk ? frame : -1;
            // The swing begins.
            if (frame == 0 && (pose == SpiderPose.Bite || pose == SpiderPose.Stab || pose == SpiderPose.Slam)) Hear(Sound.Heavy, body.Position, 0.8f);
            if (frame == 0 && (pose == SpiderPose.Shoot || pose == SpiderPose.Spit)) Hear(Sound.Shoot, body.Position, 0.9f);
            body.Frame = code;
            string key = $"spider/{view}/{pose}/{frame}/{phase}";
            System.Func<PixelImage> draw = () => SpiderArt.Draw(view, pose, frame, phase);
            // Stood on the row under the middle of its body, above the room left for its nearest legs.
            var pivot = new Vector2(0.5f, SpiderArt.Floor / (float)SpiderArt.Height);
            body.Shape.sprite = Sprites.Get(key, draw, pivot);
            // Its white picture waits until a flash shows it (WhiteOf): most of its many frames are never struck.
            body.White = () => Sprites.Get(key + "/white", () => draw().Silhouette(), pivot);
            body.Shape.flipX = turned;
            body.Glow.flipX = turned;
            body.Top = -body.Feet + SpiderArt.BodyHeight / (float)Sprites.PixelsPerUnit;
            // Where its mouth and spinnerets are from its middle, for what comes out of them.
            if (!_spiderPoints.TryGetValue(code, out var points))
            {
                var joints = SpiderArt.JointsOf(view, pose, frame);
                Vector3 From(float x, float y) => new Vector3((turned ? -1 : 1) * (x - SpiderArt.Width / 2f), SpiderArt.Height - 1 - SpiderArt.Floor - y, 0) / Sprites.PixelsPerUnit;
                _spiderPoints[code] = points = (From(joints.MouthX, joints.MouthY), From(joints.SpinX, joints.SpinY));
            }
            (body.SpiderMouth, body.SpiderSpinneret) = points;
        }

        readonly System.Collections.Generic.Dictionary<int, (Vector3 Mouth, Vector3 Spinneret)> _spiderPoints = new System.Collections.Generic.Dictionary<int, (Vector3, Vector3)>();

        int _spiderSector = 6;

        // Its wind-ups' own signs, its thread and its drop, what drips from it, and what it bleeds when struck.
        void SpiderExtras(Body body, EntityView view)
        {
            var attack = view.BossAttack;
            float since = Time.time - body.BossSince;
            bool puff = Time.time >= body.DustAt;
            if (puff) body.DustAt = Time.time + 0.08f;
            var feet = body.Position;
            var ahead = new Vector3(view.Facing.X, view.Facing.Y, 0);
            var mouth = feet + new Vector3(0, -body.Feet, 0) + body.SpiderMouth;
            var spinneret = feet + new Vector3(0, -body.Feet, 0) + body.SpiderSpinneret;
            bool first = body.CueSince != body.BossSince;
            if (first)
            {
                body.CueSince = body.BossSince;
                body.CueLate = false;
            }
            bool dropping = false;
            // Gone up its thread it is not there to see (Place has just tinted it).
            if (body.SpiderAlpha < 1)
            {
                var shown = body.Shape.color;
                body.Shape.color = new Color(shown.r, shown.g, shown.b, shown.a * body.SpiderAlpha);
                var glowing = body.Glow.color;
                body.Glow.color = new Color(glowing.r, glowing.g, glowing.b, glowing.a * body.SpiderAlpha);
            }

            if (view.BossState == BossState.Telegraph && attack != null)
            {
                float telegraph = Mathf.Max(0.1f, body.BossAgain ? attack.Telegraph * World.AgainTelegraph : attack.Telegraph);
                float progress = Mathf.Clamp01(since / telegraph);
                bool strong = attack.Warning == AttackWarning.Strong;
                switch (attack.Shape)
                {
                    case AttackShape.Cone:
                        // Reared: a hiss, and venom running off the fangs faster and faster.
                        if (first) Hear(Sound.Hiss, feet, 0.8f);
                        if (puff && UnityEngine.Random.value < 0.3f + progress) Drip(mouth, VenomColor);
                        break;
                    case AttackShape.Charge:
                        // Pressed flat: grit skittering out from under its feet, the floor ticking.
                        if (puff) Kick(feet - ahead * 0.8f + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.8f), -ahead, DustColor, 1);
                        if (puff && progress > 0.5f) Hear(Sound.Skitter, feet, 0.4f);
                        break;
                    case AttackShape.Burst:
                    case AttackShape.Circle when attack.Pool == PoolKind.Venom:
                        // The abdomen pumps; venom wells at the mouth and spills.
                        if (first) Hear(Sound.Hiss, feet, 0.5f);
                        if (puff && progress < SpiderThrowAt + 0.2f) Drip(mouth, UnityEngine.Random.value < 0.5f ? VenomColor : VenomBright);
                        if (puff && progress > 0.4f && progress < SpiderThrowAt) Kick(mouth, Vector3.up, VenomBright, 1);
                        break;
                    case AttackShape.Circle when attack.Pool == PoolKind.Web:
                        // Silk drawn in to the spinnerets from all round, a glint as it lets fly.
                        if (puff && progress < SpiderThrowAt) Gather(spinneret, SilkColor, 1.6f);
                        if (!body.CueLate && progress >= SpiderThrowAt - 0.12f)
                        {
                            body.CueLate = true;
                            AddFlash(spinneret, 0.5f, Color.white, FlashKind.Impact);
                        }
                        break;
                    case AttackShape.Circle:
                    case AttackShape.Trail:
                        // Legs cocked: a glint off the points just before they come down.
                        if (!body.CueLate && progress >= 0.8f)
                        {
                            body.CueLate = true;
                            AddFlash(feet + new Vector3(0, 2.3f, 0), 0.45f, Color.white, FlashKind.Impact);
                            Hear(Sound.Skitter, feet, 0.8f);
                        }
                        break;
                    case AttackShape.Cross:
                        // All eight up: the room dims, the floor trembles, grit falls from the roof.
                        if (first) Hear(Sound.Hiss, feet, 1);
                        Dim(0.3f * Mathf.Clamp01(progress * 2.5f));
                        if (puff) Kick(feet + (Vector3)(UnityEngine.Random.insideUnitCircle * 5) + new Vector3(0, 2.5f, 0), Vector3.down, RockColor, 1);
                        if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.02f + 0.07f * progress);
                        if (!body.CueLate && progress >= 0.88f)
                        {
                            body.CueLate = true;
                            for (int i = 0; i < 8; i++)
                                AddFlash(feet + Quaternion.Euler(0, 0, i * 45 + 22.5f) * Vector3.right * 1.3f + new Vector3(0, 1.4f, 0), 0.35f, Color.white, FlashKind.Impact);
                        }
                        break;
                    case AttackShape.Burrow:
                    case AttackShape.Leap:
                        // Gone up its thread, it leaves no shadow; then it comes down its thread over the mark.
                        if (first) Hear(Sound.Hiss, feet, 0.6f);
                        if (progress > SpiderGatherEnd && progress < SpiderGoneAt && puff) Kick(feet + new Vector3(0, 1, 0), Vector3.down, SilkColor, 1);
                        if (progress >= SpiderGoneAt - 0.05f) body.Shadow.enabled = false;
                        if (progress >= SpiderGoneAt && LandingOf(attack, out var land))
                        {
                            ShowDrop(body, land, (progress - SpiderGoneAt) / (1 - SpiderGoneAt), view.Phase);
                            dropping = true;
                        }
                        break;
                    case AttackShape.Summon:
                        // Its eggs stir: the floor trembles and bits of silk shake loose all round.
                        if (first) Hear(Sound.Hiss, feet, 1);
                        if (puff) Kick(feet + (Vector3)(UnityEngine.Random.insideUnitCircle * 3.5f), Vector3.up * 0.5f, SilkColor, 1);
                        if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.04f);
                        break;
                }
                if (strong && puff && progress > 0.5f) Kick(feet + (Vector3)(UnityEngine.Random.insideUnitCircle * 1.8f), Vector3.up * 0.4f, RockColor, 1);
            }
            else if (view.BossState == BossState.Charge)
            {
                // The spring: streaks, dust thrown up behind, the floor ticking under eight feet.
                var moved = body.Position - body.LastPosition;
                SpeedLines(feet, moved);
                if (puff)
                {
                    Kick(feet, -moved + Vector3.up * 0.4f, DustColor, 3);
                    Hear(Sound.Skitter, feet, 0.9f);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.05f);
                }
            }
            else if (view.BossState == BossState.Roar)
            {
                // Screeching: venom flung off the fangs, the floor shaking.
                if (first) Hear(Sound.Hiss, feet, 1);
                if (puff) Kick(mouth, ahead + Vector3.up * 0.5f, VenomBright, 2);
                if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.06f);
            }
            if (!(view.BossState == BossState.Telegraph && attack != null && attack.Shape == AttackShape.Cross) && _dim != null && _bossInView?.Id == "silk_mother" && _dim.color.a > 0)
                Dim(Mathf.Max(0, _dim.color.a - Time.deltaTime * 2));
            if (!dropping && body.Dropper != null)
            {
                body.Dropper.enabled = false;
                body.DropThread.enabled = false;
                body.DropShadow.enabled = false;
            }
            // From the second phase venom drips from its fangs; in the last it runs.
            if (view.Phase >= 2 && view.BossState != BossState.Dormant && body.SpiderAlpha > 0.5f && puff && UnityEngine.Random.value < (view.Phase >= 3 ? 0.3f : 0.1f)) Drip(mouth, VenomColor);
            // Struck: ichor spatters out of it.
            if (body.HurtTime > body.SpiderHurt + 0.01f && Feel.Bits) Kick(feet + new Vector3(0, 0.9f, 0), UnityEngine.Random.insideUnitCircle.normalized, IchorColor, 2);
            body.SpiderHurt = body.HurtTime;
        }

        // A drop of venom falling from its mouth to the floor.
        void Drip(Vector3 at, Color color)
        {
            if (!Feel.Bits) return;
            var renderer = Renderer(_root.transform, "Drip", Shapes.Square, color);
            renderer.transform.localPosition = at + new Vector3(UnityEngine.Random.Range(-0.12f, 0.12f), 0, 0);
            renderer.transform.localScale = new Vector3(1, 2, 1) / Sprites.PixelsPerUnit;
            renderer.sortingOrder = 19000;
            _bits.Add(new Bit { Renderer = renderer, Velocity = new Vector3(0, -0.5f, 0), Fall = new Vector3(0, -9, 0), Color = color, Life = 0.4f, Drag = 0 });
        }

        // A mote drawn in to a point from somewhere round it.
        void Gather(Vector3 to, Color color, float from)
        {
            if (!Feel.Bits) return;
            var start = to + (Vector3)(UnityEngine.Random.insideUnitCircle.normalized * from);
            var renderer = Renderer(_root.transform, "Mote", Shapes.Square, color);
            renderer.transform.localPosition = start;
            renderer.transform.localScale = Vector3.one * (2f / Sprites.PixelsPerUnit);
            renderer.sortingOrder = 19000;
            _bits.Add(new Bit { Renderer = renderer, Velocity = (to - start) / 0.3f, Color = color, Life = 0.3f, Drag = 0 });
        }

        // The spider coming down its thread over where it will land: the thread first, then the body, legs tucked,
        // faster and faster, its shadow growing on the mark.
        void ShowDrop(Body body, Vector3 land, float progress, int phase)
        {
            if (body.Dropper == null)
            {
                body.Dropper = Renderer(_root.transform, "Dropping", null, Color.white);
                body.DropThread = Renderer(_root.transform, "Thread", Shapes.Square, new Color(SilkColor.r, SilkColor.g, SilkColor.b, 0.8f));
                body.DropShadow = Renderer(_root.transform, "Drop shadow", Shapes.Circle, new Color(0, 0, 0, 0.3f));
                body.DropShadow.sortingOrder = 40;
            }
            const float high = 9;
            float fall = Mathf.Clamp01((progress - 0.25f) / 0.75f);
            float height = high * (1 - fall * fall);
            float px = 1f / Sprites.PixelsPerUnit;
            // The legs tucked, as it left (the last frame of the rise is out of the picture: one from its middle).
            int frame = SpiderArt.FramesOf(SpiderPose.Rise) / 3;
            int clamped = Mathf.Clamp(phase, 1, 3);
            body.Dropper.sprite = Sprites.Get($"spider/drop/{clamped}", () => SpiderArt.Draw(BossView.Front, SpiderPose.Rise, frame, clamped), new Vector2(0.5f, SpiderArt.Floor / (float)SpiderArt.Height));
            body.Dropper.enabled = fall > 0;
            body.Dropper.transform.localPosition = land + new Vector3(0, height, 0);
            body.Dropper.sortingOrder = 20600;
            body.DropThread.enabled = true;
            float top = land.y + high + 6;
            float bottom = land.y + (fall > 0 ? height + 1.5f : high + 6 - (high + 4.5f) * Mathf.Clamp01(progress / 0.25f));
            body.DropThread.transform.localPosition = new Vector3(land.x, (top + bottom) / 2, 0);
            body.DropThread.transform.localScale = new Vector3(px, top - bottom, 1);
            body.DropThread.sortingOrder = 20599;
            body.DropShadow.enabled = true;
            body.DropShadow.transform.localPosition = land;
            body.DropShadow.transform.localScale = new Vector3(1, 0.5f, 1) * (0.8f + 2.2f * progress);
            body.DropShadow.color = new Color(0, 0, 0, 0.15f + 0.3f * progress);
        }

        // ---- What it throws: a gob of silk or venom from the spider to each mark, in the air while the mark fills. ----

        // The gob over one of the spider's thrown marks; true when the mark is one of them.
        bool FlyingGob(Mark mark, HazardState hazard, float progress)
        {
            var attack = AttackOf(hazard);
            if (attack == null || _bossInView.Id != "silk_mother" || !Thrown(attack) || IsPool(hazard)) return false;
            if (mark.Rock == null)
            {
                mark.Rock = Renderer(_root.transform, "Gob", attack.Pool == PoolKind.Web ? SilkGlobSprite : VenomGlobSprite, Color.white);
                mark.Rock.sortingOrder = 20500;
            }
            float fly = (progress - SpiderThrowAt) / (1 - SpiderThrowAt);
            mark.Rock.enabled = fly > 0;
            if (fly <= 0) return true;
            var boss = _others.Values.FirstOrDefault(b => b.Kind == BodyKind.Boss && b.Id == "silk_mother");
            var from = boss == null ? new Vector3(hazard.Position.X, hazard.Position.Y + 6, 0)
                : boss.Position + new Vector3(0, -boss.Feet, 0) + (attack.Pool == PoolKind.Web ? boss.SpiderSpinneret : boss.SpiderMouth);
            var to = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            // Lobbed: up and over, coming down steeply.
            float arc = Mathf.Sin(fly * Mathf.PI) * (1.5f + Vector3.Distance(from, to) * 0.18f);
            var at = Vector3.Lerp(from, to, fly) + new Vector3(0, arc, 0);
            var before = Vector3.Lerp(from, to, fly - 0.05f) + new Vector3(0, Mathf.Sin((fly - 0.05f) * Mathf.PI) * (1.5f + Vector3.Distance(from, to) * 0.18f), 0);
            mark.Rock.transform.localPosition = at;
            mark.Rock.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(at.y - before.y, at.x - before.x) * Mathf.Rad2Deg);
            // A thread paid out behind the silk; drops shed by the venom.
            if (Feel.Bits && UnityEngine.Random.value < 0.5f)
            {
                var color = attack.Pool == PoolKind.Web ? SilkColor : VenomColor;
                var trail = Renderer(_root.transform, "Trail", Shapes.Square, color);
                trail.transform.localPosition = at;
                trail.transform.localScale = Vector3.one * ((attack.Pool == PoolKind.Web ? 1f : 2f) / Sprites.PixelsPerUnit);
                trail.sortingOrder = 20400;
                _bits.Add(new Bit { Renderer = trail, Velocity = Vector3.zero, Fall = attack.Pool == PoolKind.Web ? Vector3.zero : new Vector3(0, -6, 0), Color = color, Life = attack.Pool == PoolKind.Web ? 0.35f : 0.3f, Drag = 0 });
            }
            return true;
        }

        // ---- What stays on the ground: a web or a pool of venom, drawn on the floor under everyone. ----

        Mark MakePool(HazardState hazard)
        {
            var root = new GameObject("Pool").transform;
            root.SetParent(_root.transform, false);
            root.localPosition = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            int diameter = Mathf.Max(8, Mathf.RoundToInt(hazard.Size * 2 * Sprites.PixelsPerUnit));
            bool web = hazard.Shape == HazardShape.Web;
            var sprite = web ? Sprites.Centered($"fx/web/{diameter}/{hazard.Id % 3}", () => EffectArt.Web(diameter, hazard.Id % 3)) : VenomSprite(diameter, 0);
            var mark = new Mark
            {
                Root = root,
                Edge = Renderer(root, "Pool", sprite, Color.clear),
                Fill = Renderer(root, "Glow", Shapes.Circle, Color.clear),
                State = hazard,
            };
            mark.Fill.transform.localScale = Vector3.one * (hazard.Size * 2);
            // Under every body, over the floor's cracks.
            mark.Fill.sortingOrder = 33;
            mark.Edge.sortingOrder = 34;
            root.localRotation = web ? Quaternion.Euler(0, 0, hazard.Id * 47 % 360) : Quaternion.identity;
            return mark;
        }

        static Sprite VenomSprite(int diameter, int frame) => Sprites.Centered($"fx/venom/{diameter}/{frame}", () => EffectArt.VenomPool(diameter, frame));

        // A web spreads out in a moment and shimmers; venom seeps out and bubbles; each fades as its time runs out.
        void DrawPool(Mark mark, HazardView view)
        {
            var hazard = view.State;
            bool web = hazard.Shape == HazardShape.Web;
            float age = view.Progress * hazard.Duration / World.TicksPerSecond;
            float left = (1 - view.Progress) * hazard.Duration / World.TicksPerSecond;
            float grown = Mathf.Clamp01(age / 0.18f);
            float fade = Mathf.Clamp01(left / 1.2f);
            // The last second it flickers: about to go.
            if (left < 1 && Mathf.Sin(Time.time * 26) > 0.3f) fade *= 0.6f;
            mark.Edge.transform.localScale = Vector3.one * (0.4f + 0.6f * (1 - (1 - grown) * (1 - grown)));
            if (web)
            {
                mark.Edge.color = new Color(1, 1, 1, fade * (0.88f + 0.12f * Mathf.Sin(Time.time * 3 + hazard.Id)));
                mark.Fill.color = new Color(SilkColor.r, SilkColor.g, SilkColor.b, 0.07f * fade);
                return;
            }
            int diameter = Mathf.Max(8, Mathf.RoundToInt(hazard.Size * 2 * Sprites.PixelsPerUnit));
            mark.Edge.sprite = VenomSprite(diameter, Mathf.FloorToInt(Time.time * 5 + hazard.Id) % EffectArt.VenomFrames);
            mark.Edge.color = new Color(1, 1, 1, fade);
            mark.Fill.color = new Color(VenomColor.r, VenomColor.g, VenomColor.b, fade * (0.1f + 0.06f * Mathf.Sin(Time.time * 6 + hazard.Id)));
            // Fumes off it.
            if (Feel.Bits && UnityEngine.Random.value < 0.06f)
            {
                var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0) + (Vector3)(UnityEngine.Random.insideUnitCircle * hazard.Size * 0.8f);
                var fume = Renderer(_root.transform, "Fume", Shapes.Square, VenomBright);
                fume.transform.localPosition = at;
                fume.transform.localScale = Vector3.one * (2f / Sprites.PixelsPerUnit);
                fume.sortingOrder = 19000;
                _bits.Add(new Bit { Renderer = fume, Velocity = new Vector3(0, 0.9f, 0), Color = VenomBright, Life = 0.7f, Drag = 0.5f, Sway = 0.4f, Phase = UnityEngine.Random.value * 6 });
            }
        }

        // Caught in a web oneself: strands cling and pull at the feet, and the first moment says what it is.
        bool _snared;

        void ShowSnare()
        {
            bool snared = (_client.SelfStatus & StatusBits.Snared) != 0;
            if (snared && !_snared)
            {
                _floating.Add(new Floating { Position = _self.Position + new Vector3(0, 0.5f, 0), Text = "거미줄!", Color = SilkColor });
                Hear(Sound.Squelch, _self.Position, 0.6f);
            }
            _snared = snared;
            if (!snared || !Feel.Bits || UnityEngine.Random.value > 0.25f) return;
            var strand = Renderer(_root.transform, "Strand", Shapes.Square, SilkColor);
            strand.transform.localPosition = _self.Position + new Vector3(UnityEngine.Random.Range(-0.35f, 0.35f), UnityEngine.Random.Range(-0.1f, 0.3f), 0);
            strand.transform.localScale = new Vector3(1, UnityEngine.Random.Range(3, 6), 1) / Sprites.PixelsPerUnit;
            strand.transform.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(-30f, 30f));
            strand.sortingOrder = 19000;
            _bits.Add(new Bit { Renderer = strand, Velocity = Vector3.zero, Color = SilkColor, Life = 0.3f, Drag = 0 });
        }

        // ---- Where its blows land. ----

        float _spiderSlamAt = -10;

        // A point driven into the floor: a white star, grit, a crack or two.
        void Pierce(Vector3 at, float weight)
        {
            AddFlash(at, 0.5f + 0.3f * weight, Color.white, FlashKind.Impact);
            Kick(at, Vector3.up, RockColor, 2 + Mathf.RoundToInt(weight));
            Kick(at, Vector3.up, DustColor, 2);
            Cracks(at, 0.5f + 0.4f * weight, 3);
        }

        // Silk or venom bursting where it lands: a splash, drops thrown out, the wet slap.
        void Splat(Vector3 at, float size, bool silk)
        {
            var color = silk ? SilkColor : VenomColor;
            AddFlash(at, size * 1.1f, color, FlashKind.Burst);
            AddFlash(at, size, silk ? Color.white : VenomBright, FlashKind.Ring);
            if (Feel.Bits)
            {
                for (int i = 0; i < 10; i++)
                {
                    var way = Quaternion.Euler(0, 0, i * 36 + UnityEngine.Random.Range(-12f, 12f)) * Vector3.right;
                    var drop = Renderer(_root.transform, "Spatter", Shapes.Square, color);
                    drop.transform.localPosition = at + way * (size * 0.3f);
                    // Silk goes out in strands, venom in drops.
                    drop.transform.localScale = (silk ? new Vector3(5, 1, 1) : new Vector3(2, 2, 1)) / Sprites.PixelsPerUnit;
                    drop.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(way.y, way.x) * Mathf.Rad2Deg);
                    drop.sortingOrder = 19000;
                    _bits.Add(new Bit { Renderer = drop, Velocity = way * UnityEngine.Random.Range(2.5f, 5f) * Mathf.Max(1, size * 0.5f), Color = color, Life = 0.4f, Drag = 5 });
                }
            }
            Hear(Sound.Squelch, at, 0.9f);
        }

        // Where one of the silk mother's blows lands, a burst of its own; false for anyone else's.
        bool SpiderLanded(HazardState hazard)
        {
            var attack = AttackOf(hazard);
            if (attack == null || _bossInView.Id != "silk_mother") return false;
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            var along = new Vector3(hazard.Direction.X, hazard.Direction.Y, 0);
            switch (attack.Shape)
            {
                case AttackShape.Cone:
                {
                    // The bite: the fangs meet where the wedge ends; venom bursts from them.
                    var bite = at + along * hazard.Size * 0.7f;
                    AddFlash(bite, 0.9f, Color.white, FlashKind.Impact);
                    AddFlash(bite, 1.3f, VenomColor, FlashKind.Burst);
                    for (int side = -1; side <= 1; side += 2)
                        Kick(bite, Quaternion.Euler(0, 0, side * 50) * along, VenomBright, 4);
                    Kick(bite, Vector3.up, DustColor, 2);
                    if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.07f);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.22f);
                    Hear(Sound.Thump, bite, 0.8f);
                    Hear(Sound.Squelch, bite, 0.7f);
                    return true;
                }
                case AttackShape.Circle when attack.Pool == PoolKind.Web:
                    Splat(at, hazard.Size, true);
                    return true;
                case AttackShape.Circle when attack.Pool == PoolKind.Venom:
                    Splat(at, hazard.Size, false);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.08f);
                    return true;
                case AttackShape.Circle:
                case AttackShape.Trail:
                    // A leg's point through the floor.
                    Pierce(at, 1);
                    AddFlash(at, hazard.Size, DustColor, FlashKind.Ring);
                    if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.06f);
                    if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.2f);
                    Hear(Sound.Thump, at, 0.8f);
                    return true;
                case AttackShape.Burrow:
                case AttackShape.Leap:
                    // Down on its mark: the knight's weight of impact, and silk thrown out all round under it.
                    Impact(at, 1.7f);
                    AddFlash(at, hazard.Size, SilkColor, FlashKind.Ring);
                    Splat(at, hazard.Size * 0.8f, true);
                    Cracks(at, hazard.Size * 0.8f, 8);
                    ScreenFlash(new Color(0.9f, 1f, 0.85f, 0.3f), 0.12f);
                    return true;
                case AttackShape.Cross:
                {
                    // Each leg comes down along its arm: its point at the near end, the floor torn along it.
                    Pierce(at + along * 0.3f, 1.5f);
                    for (float d = 1.2f; d < hazard.Size; d += 1.4f)
                    {
                        Kick(at + along * d, Vector3.up, d % 2.8f < 1.4f ? RockColor : DustColor, 2);
                        Cracks(at + along * d, 0.5f, 2);
                    }
                    AddFlash(at + along * hazard.Size * 0.5f, 0.8f, DustColor, FlashKind.Burst);
                    // Once for all eight: the longest stop, the hardest jolt, the screen washed pale green.
                    if (Time.time - _spiderSlamAt > 0.3f)
                    {
                        _spiderSlamAt = Time.time;
                        var middle = at - along * attack.Radius;
                        if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.16f);
                        if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.6f);
                        ScreenFlash(new Color(0.88f, 1f, 0.8f, 0.4f), 0.15f);
                        AddFlash(middle, attack.Length * 0.9f, DustColor, FlashKind.Ring);
                        AddFlash(middle, attack.Radius * 1.6f, Color.white, FlashKind.Ring);
                        Hear(Sound.Boom, middle, 1);
                        Hear(Sound.Thump, middle, 1);
                    }
                    return true;
                }
            }
            return false;
        }

        // ---- Its fall: where it stood it sinks and its legs curl in over it, and what is left fades. ----

        sealed class Fall
        {
            public SpriteRenderer Renderer;
            public float Since;
            public BossView View;
            public bool Turned;
            public int Frame = -1;
        }

        readonly System.Collections.Generic.List<Fall> _falls = new System.Collections.Generic.List<Fall>();

        void SpiderFalls(Vector3 at)
        {
            var (view, turned) = BossSectors[_spiderSector];
            var renderer = Renderer(_root.transform, "Fallen spider", null, Color.white);
            renderer.transform.localPosition = at;
            renderer.sortingOrder = 10000 - Mathf.RoundToInt(at.y * 10);
            renderer.flipX = turned;
            _falls.Add(new Fall { Renderer = renderer, Since = Time.time, View = view, Turned = turned });
            // Its last screech; ichor and silk thrown out of it; the room jolts.
            Hear(Sound.Hiss, at, 1);
            Splat(at + new Vector3(0, 0.6f, 0), 1.6f, false);
            if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, 0.18f);
            if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.5f);
            ScreenFlash(new Color(1, 1, 1, 0.35f), 0.15f);
        }

        const float FallFrameSeconds = 0.07f;
        const float FallLie = 1.2f;
        const float FallFade = 1.0f;

        void UpdateFalls()
        {
            for (int i = _falls.Count - 1; i >= 0; i--)
            {
                var fall = _falls[i];
                float age = Time.time - fall.Since;
                int frames = SpiderArt.FramesOf(SpiderPose.Die);
                float curled = frames * FallFrameSeconds;
                if (age > curled + FallLie + FallFade)
                {
                    Object.Destroy(fall.Renderer.gameObject);
                    _falls.RemoveAt(i);
                    continue;
                }
                int frame = Mathf.Min(frames - 1, Mathf.FloorToInt(age / FallFrameSeconds));
                if (frame != fall.Frame)
                {
                    fall.Frame = frame;
                    var view = fall.View;
                    fall.Renderer.sprite = Sprites.Get($"spider/{view}/{SpiderPose.Die}/{frame}/3", () => SpiderArt.Draw(view, SpiderPose.Die, frame, 3), new Vector2(0.5f, SpiderArt.Floor / (float)SpiderArt.Height));
                    // Twitching as it curls: a last drop of venom now and then.
                    if (frame % 5 == 4) Drip(fall.Renderer.transform.localPosition + new Vector3(0, 0.5f, 0), VenomColor);
                }
                float gone = Mathf.Clamp01((age - curled - FallLie) / FallFade);
                float grey = Mathf.Clamp01((age - curled * 0.6f) / 0.8f);
                fall.Renderer.color = new Color(1 - 0.35f * grey, 1 - 0.35f * grey, 1 - 0.3f * grey, 1 - gone);
            }
        }
    }
}
