using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // How plainly a boss's attacks are announced (2026-11-12, AttackWarning, Docs/Bosses.md 광맥 포식자 그룸): an
    // attack the sheet marks none lays nothing on the ground and gives no glow, only the boss's own wind-up shows it;
    // the strongest (strong) show their area and a warning sign over the boss. And the vein eater's own wind-ups and
    // blows: a pose for each kind of attack (VeinEaterArt), dust, a mound running underground, rocks falling from the
    // roof, and a burst of its own where each blow lands.
    sealed partial class WorldView
    {
        static readonly Color DirtColor = new Color(0.5f, 0.37f, 0.26f);
        static readonly Color RockColor = new Color(0.6f, 0.55f, 0.5f);
        static readonly Color OreColor = new Color(1f, 0.62f, 0.25f);
        static readonly Color RageColor = new Color(1f, 0.3f, 0.2f);
        static readonly Color SlashColor = new Color(1f, 0.92f, 0.8f);

        static Sprite WarningSprite => Sprites.Centered("fx/warning", EffectArt.WarningSign);
        static Sprite RockSprite => Sprites.Centered("shot/rock", () => EffectArt.Rock(7));
        static Sprite BoulderSprite => Sprites.Centered("fx/boulder", () => EffectArt.Rock(13));
        static Sprite MoundSprite => Sprites.Standing("fx/mound", () =>
        {
            var image = new PixelImage(20, 7);
            for (int x = 0; x < 20; x++)
            {
                int h = (int)Mathf.Round(6 * Mathf.Sqrt(Mathf.Max(0, 1 - Mathf.Pow((x - 9.5f) / 10f, 2))));
                for (int y = 0; y < h; y++) image[x, 6 - y] = y == h - 1 ? new Rgba(150, 116, 84) : (x + y) % 3 == 0 ? new Rgba(82, 62, 48) : new Rgba(110, 82, 60);
            }
            return image.Outlined(new Rgba(24, 16, 12));
        });

        // The boss in view, for which attack laid a hazard (its cause) and whose shots are rocks.
        BossDefinition _bossInView;

        // For the playtests: marks drawn on the ground (an unmarked attack's are kept but clear), and whether a
        // warning sign shows over a boss.
        public int MarksShown => _marks.Values.Count(m => m.State.Warning != AttackWarning.Hidden && !IsPool(m.State));
        public bool WarningShown => _others.Values.Any(b => b.Sign != null && b.Sign.enabled);

        // How far the last frame got (1 entities, 2 past them, 3 past the marks, 4 past the events), and how each boss
        // body is drawn: where, its sprite and its colour, for the playtests to see why a boss would not show.
        int _rendered;
        public string RenderTrace => _rendered + " " + string.Join(" ", _others.Values.Where(b => b.Kind == BodyKind.Boss).Select(b =>
            $"{b.Id}@{b.Root.localPosition.x:0.0},{b.Root.localPosition.y:0.0}:{(b.Shape.sprite == null ? "nosprite" : b.Shape.sprite.name + "/" + b.Shape.sprite.rect.width + "x" + b.Shape.sprite.rect.height)}:a{b.Shape.color.a:0.00}:o{b.Shape.sortingOrder}:{(b.Shape.enabled ? "on" : "off")}:f{b.Frame}"));

        BossAttack AttackOf(HazardState hazard) =>
            _bossInView != null && hazard.Cause >= 1 && hazard.Cause <= _bossInView.Attacks.Count ? _bossInView.Attacks[hazard.Cause - 1] : null;

        // A boss's attack and when it started on it (or changed state), for the poses and the warning sign.
        void TrackBoss(Body body, EntityView view)
        {
            _bossInView = view.Boss;
            if (body.BossState != view.BossState || body.BossAttack != view.BossAttack)
            {
                // A charge that runs again winds up straight out of the last run (World.StartTelegraph, again).
                if (view.BossState == BossState.Telegraph) body.BossAgain = body.BossState == BossState.Charge;
                body.BossSince = Time.time;
            }
            body.BossState = view.BossState;
            body.BossAttack = view.BossAttack;
            body.BossPhase = view.Phase;
        }

        // An attack the boss alone announces gives no glow: only its wind-up.
        static bool Unmarked(EntityView view) => view.BossAttack != null && view.BossAttack.Warning == AttackWarning.Hidden;

        // After a boss is placed this frame: the warning sign and, for the vein eater, its dust and mounds.
        void BossExtras(Body body, EntityView view)
        {
            bool strong = view.BossState == BossState.Telegraph && view.BossAttack != null && view.BossAttack.Warning == AttackWarning.Strong;
            ShowWarningSign(body, strong);
            RunLater();
            if (body.Id == "vein_eater") VeinEaterExtras(body, view);
            else if (body.Mound != null) body.Mound.enabled = false;
            if (body.Id == "shrine_samurai") SamuraiExtras(body, view);
            if (body.Id == "black_knight") KnightExtras(body, view);
            if (body.Id == "silk_mother") SpiderExtras(body, view);
        }

        void ShowWarningSign(Body body, bool on)
        {
            if (!on)
            {
                if (body.Sign != null) body.Sign.enabled = false;
                return;
            }
            if (body.Sign == null)
            {
                body.Sign = Renderer(body.Root, "Warning", WarningSprite, Color.white);
                body.Sign.sortingOrder = 21000;
                Hear(Sound.Warning, body.Position, 1);
            }
            else if (!body.Sign.enabled) Hear(Sound.Warning, body.Position, 1);
            body.Sign.enabled = true;
            float beat = Mathf.Abs(Mathf.Sin(Time.time * 9));
            body.Sign.transform.localPosition = new Vector3(0, body.Top + 0.5f + Mathf.Sin(Time.time * 10) * 0.06f, 0);
            body.Sign.transform.localScale = Vector3.one * (1 + 0.15f * beat);
            body.Sign.color = new Color(1, 1, 1, 0.6f + 0.4f * beat);
        }

        // ---- The vein eater ----

        // Its pose from what it is doing: each kind of attack has a wind-up of its own (VeinPose), run through its
        // telegraph in four steps, and a follow-through just after; a spin turns it round and round.
        void AnimateVeinEater(Body body, Vec2 facing, bool dead, bool moving)
        {
            var attack = body.BossAttack;
            float since = Time.time - body.BossSince;
            int sector = SectorOf(facing);
            var pose = VeinPose.Idle;
            int frame = Mathf.FloorToInt(Time.time * 2.4f) % VeinEaterArt.FramesOf(VeinPose.Idle);
            bool rage = false;
            int Through(VeinPose p, float progress) => Mathf.Clamp(Mathf.FloorToInt(progress * VeinEaterArt.FramesOf(p)), 0, VeinEaterArt.FramesOf(p) - 1);
            // The blow after a wind-up, and the wind-up itself (2026-11-13, more frames: VeinEaterArt.FramesOf).
            VeinPose BlowOf(AttackShape shape) => shape switch
            {
                AttackShape.Cone => VeinPose.Swipe,
                AttackShape.Burst => VeinPose.Throw,
                AttackShape.Burrow => VeinPose.Rise,
                _ => VeinPose.Pound,
            };
            if (dead)
            {
                frame = 0;
            }
            else if (moving && (body.BossState == BossState.Approach || body.BossState == BossState.Recover || body.BossState == BossState.Dormant))
            {
                // Walking: a stride each way about every tile and a bit at its pace.
                pose = VeinPose.Walk;
                frame = Mathf.FloorToInt(body.Walked * 7) % VeinEaterArt.FramesOf(pose);
            }
            else if (body.BossState == BossState.Roar)
            {
                pose = VeinPose.Roar;
                int rise = Mathf.FloorToInt(since / 0.08f);
                frame = rise < VeinEaterArt.RoarRise ? rise : VeinEaterArt.RoarRise + Mathf.FloorToInt(Time.time * 10) % 4;
            }
            else if (body.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / Mathf.Max(0.1f, attack.Telegraph));
                switch (attack.Shape)
                {
                    case AttackShape.Cone:
                    case AttackShape.Burst:
                    case AttackShape.Circle:
                    case AttackShape.Ring:
                    case AttackShape.Line:
                    case AttackShape.Cross:
                    {
                        // The wind-up, then the blow's swing in the telegraph's last moment.
                        var blow = BlowOf(attack.Shape);
                        var windUp = attack.Shape == AttackShape.Cone ? VeinPose.Rear : attack.Shape == AttackShape.Burst ? VeinPose.Lift : VeinPose.Slam;
                        int strike = VeinEaterArt.StrikeFrame(blow);
                        float wound = 1 - Mathf.Min(0.5f, strike * VeinEaterArt.FrameSeconds(blow, 0) / Mathf.Max(0.1f, attack.Telegraph));
                        if (progress < wound) (pose, frame) = (windUp, Through(windUp, progress / wound));
                        else (pose, frame) = (blow, Mathf.Min(strike - 1, Mathf.FloorToInt((progress - wound) * attack.Telegraph / VeinEaterArt.FrameSeconds(blow, 0))));
                        break;
                    }
                    case AttackShape.Charge:
                        (pose, frame) = (VeinPose.Crouch, Through(VeinPose.Crouch, progress));
                        rage = attack.Count > 1;
                        break;
                    case AttackShape.Burrow:
                        // Digging down over the first part, then gone under the floor.
                        (pose, frame) = (VeinPose.Sink, Through(VeinPose.Sink, progress / 0.4f));
                        break;
                    case AttackShape.Leap:
                        (pose, frame) = (VeinPose.Leap, Through(VeinPose.Leap, progress / 0.7f));
                        break;
                    case AttackShape.Trail:
                        (pose, frame) = (VeinPose.Plunge, Through(VeinPose.Plunge, progress));
                        break;
                    case AttackShape.Spiral:
                        (pose, frame) = (VeinPose.Curl, Through(VeinPose.Curl, progress));
                        break;
                    case AttackShape.Summon:
                    {
                        pose = VeinPose.Roar;
                        int rise = Mathf.FloorToInt(since / 0.08f);
                        frame = rise < VeinEaterArt.RoarRise ? rise : VeinEaterArt.RoarRise + Mathf.FloorToInt(Time.time * 10) % 4;
                        break;
                    }
                    default:
                        (pose, frame) = (VeinPose.Slam, Through(VeinPose.Slam, progress));
                        break;
                }
            }
            else if (body.BossState == BossState.Charge)
            {
                // Running flat out: legs at twice the walk.
                pose = VeinPose.Rush;
                frame = Mathf.FloorToInt(Time.time * 16) % VeinEaterArt.FramesOf(pose);
                rage = attack != null && attack.Count > 1;
            }
            else if (body.BossState == BossState.Recover && attack != null)
            {
                int last = VeinEaterArt.FramesOf(VeinPose.Curl) - 1;
                switch (attack.Shape)
                {
                    case AttackShape.Spiral:
                    {
                        // Spinning on the spot while the volleys fly, then uncurling.
                        float spin = attack.Width * 0.3f + 0.2f;
                        if (since < spin)
                        {
                            (pose, frame) = (VeinPose.Curl, last);
                            sector = Mathf.FloorToInt(Time.time * 18) % 8;
                        }
                        else if (since - spin < 0.04f * (last + 1)) (pose, frame) = (VeinPose.Curl, last - Mathf.FloorToInt((since - spin) / 0.04f));
                        break;
                    }
                    case AttackShape.Charge:
                    {
                        // Out of the run: straightening up again.
                        int back = VeinEaterArt.FramesOf(VeinPose.Crouch) - 1 - Mathf.FloorToInt(since / 0.03f);
                        if (back >= 0) (pose, frame) = (VeinPose.Crouch, back);
                        rage = attack.Count > 1;
                        break;
                    }
                    case AttackShape.Trail:
                    {
                        // The drills held in the floor a moment, then pulled out.
                        int plunged = VeinEaterArt.FramesOf(VeinPose.Plunge) - 1;
                        if (since < 0.3f) (pose, frame) = (VeinPose.Plunge, plunged);
                        else if (since - 0.3f < 0.05f * (plunged + 1)) (pose, frame) = (VeinPose.Plunge, plunged - Mathf.FloorToInt((since - 0.3f) / 0.05f));
                        break;
                    }
                    case AttackShape.Summon:
                        break;
                    default:
                    {
                        // From the strike on, each frame for its own time (a burrow bursts up, a quake lands).
                        var blow = attack.Shape == AttackShape.Leap ? VeinPose.Pound : BlowOf(attack.Shape);
                        float left = since;
                        for (int f = VeinEaterArt.StrikeFrame(blow); f < VeinEaterArt.FramesOf(blow); f++)
                        {
                            left -= VeinEaterArt.FrameSeconds(blow, f);
                            if (left >= 0) continue;
                            (pose, frame) = (blow, f);
                            break;
                        }
                        break;
                    }
                }
            }

            var (view, turned) = BossSectors[sector];
            int phase = Mathf.Clamp(body.BossPhase, 1, 3);
            int code = 300000 + ((((int)pose * 8 + (int)view) * 64 + frame) * 4 + phase) * 4 + (rage ? 2 : 0) + (turned ? 1 : 0);
            if (code == body.Frame) return;
            body.Frame = code;
            string key = $"vein/{view}/{pose}/{frame}/{phase}/{rage}";
            System.Func<PixelImage> draw = () => VeinEaterArt.Draw(view, pose, frame, phase, rage);
            body.Shape.sprite = Sprites.Standing(key, draw);
            body.Glow.sprite = Sprites.Standing(key + "/white", () => draw().Silhouette());
            body.Shape.flipX = turned;
            body.Glow.flipX = turned;
            // The name and the warning sign sit over the body, not over the room left above it for a leap.
            body.Top = -body.Feet + VeinEaterArt.BodyHeight / (float)Sprites.PixelsPerUnit;
        }

        // Dust, a mound running underground, steam; and rocks falling where the cave-in will land are drawn with the
        // marks (FallingRock).
        void VeinEaterExtras(Body body, EntityView view)
        {
            var attack = view.BossAttack;
            float since = Time.time - body.BossSince;
            var feet = body.Position;
            var ahead = new Vector3(view.Facing.X, view.Facing.Y, 0);
            bool mounded = false;
            bool puff = Time.time >= body.DustAt;
            if (puff) body.DustAt = Time.time + 0.07f;

            if (view.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / Mathf.Max(0.1f, attack.Telegraph));
                switch (attack.Shape)
                {
                    case AttackShape.Charge:
                        // Pawing the ground: dust kicked out behind; in a frenzy, steam off its back.
                        if (puff) Kick(feet - ahead * 0.9f, -ahead, DirtColor, 3);
                        if (puff && attack.Count > 1) Kick(feet + new Vector3(0, 1.8f, 0), Vector3.up, RageColor, 2);
                        break;
                    case AttackShape.Burst:
                        if (puff && progress < 0.5f) Kick(feet + ahead * 0.6f, Vector3.up, DirtColor, 2);
                        break;
                    case AttackShape.Burrow:
                        // Digging in, then a mound running under the floor to where it will burst up. Gone under, it
                        // leaves no shadow.
                        if (progress >= 0.35f) body.Shadow.enabled = false;
                        if (puff && progress < 0.4f) Kick(feet, Vector3.up, DirtColor, 4);
                        if (progress >= 0.3f && LandingOf(attack, out var land))
                        {
                            float run = Mathf.Clamp01((progress - 0.3f) / 0.55f);
                            var at = Vector3.Lerp(feet, land, run);
                            ShowMound(body, at);
                            mounded = true;
                            if (puff) Kick(at, Vector3.up, DirtColor, 2);
                        }
                        break;
                    case AttackShape.Leap:
                        if (puff && progress < 0.4f) Kick(feet, Vector3.up, DirtColor, 2);
                        break;
                    case AttackShape.Trail:
                        if (puff) Kick(feet + ahead * 1.2f, Vector3.up, RockColor, 2);
                        break;
                    case AttackShape.Spiral:
                        if (puff) Kick(feet + new Vector3(0, 1, 0), UnityEngine.Random.insideUnitCircle.normalized, OreColor, 2);
                        break;
                    case AttackShape.Summon:
                        // Head back, a roar at the roof: dust falls round the room.
                        if (puff) Kick(feet + (Vector3)(UnityEngine.Random.insideUnitCircle * 5) + new Vector3(0, 2, 0), Vector3.down, RockColor, 1);
                        _shake = Mathf.Max(_shake, 0.04f);
                        break;
                    default:
                        // Fists up for the cave-in: the roof trickles dust.
                        if (puff) Kick(feet + (Vector3)(UnityEngine.Random.insideUnitCircle * 4) + new Vector3(0, 2.5f, 0), Vector3.down, RockColor, 1);
                        break;
                }
            }
            else if (view.BossState == BossState.Charge && puff)
            {
                // Tearing along: earth thrown up behind, red in a frenzy.
                Kick(feet - ahead * 0.8f, -ahead + Vector3.up * 0.5f, attack != null && attack.Count > 1 ? RageColor : DirtColor, 4);
            }
            else if (view.BossState == BossState.Recover && attack != null && attack.Shape == AttackShape.Spiral && since < attack.Width * 0.3f + 0.2f && puff)
            {
                Kick(feet + new Vector3(0, 1, 0), UnityEngine.Random.insideUnitCircle.normalized, OreColor, 3);
            }
            // In the last phase its ore smoulders.
            if (view.Phase >= 3 && puff && UnityEngine.Random.value < 0.35f) Kick(feet + new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), 2.2f, 0), Vector3.up, OreColor, 1);
            if (!mounded && body.Mound != null) body.Mound.enabled = false;
        }

        // Where the attack the boss is on comes down: its mark on the ground.
        bool LandingOf(BossAttack attack, out Vector3 at)
        {
            int cause = _bossInView == null ? 0 : _bossInView.Attacks.ToList().IndexOf(attack) + 1;
            foreach (var mark in _marks.Values)
            {
                if (cause == 0 || mark.State.Cause != cause) continue;
                at = new Vector3(mark.State.Position.X, mark.State.Position.Y, 0);
                return true;
            }
            at = default;
            return false;
        }

        void ShowMound(Body body, Vector3 at)
        {
            if (body.Mound == null)
            {
                body.Mound = Renderer(_root.transform, "Mound", MoundSprite, Color.white);
                body.Mound.sortingOrder = 9000;
            }
            body.Mound.enabled = true;
            body.Mound.transform.localPosition = at + new Vector3(Mathf.Sin(Time.time * 40) * 0.04f, -0.2f, 0);
        }

        // A few bits of dust, earth or sparks thrown one way.
        void Kick(Vector3 at, Vector3 toward, Color color, int count)
        {
            if (!Feel.Bits) return;
            for (int i = 0; i < count; i++)
            {
                var renderer = Renderer(_root.transform, "Dust", Shapes.Square, color);
                renderer.transform.localPosition = at + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.3f);
                renderer.transform.localScale = Vector3.one * ((UnityEngine.Random.value < 0.5f ? 2f : 3f) / Sprites.PixelsPerUnit);
                renderer.sortingOrder = 19000;
                var velocity = (toward.normalized + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.6f)) * UnityEngine.Random.Range(1.5f, 3.5f);
                _bits.Add(new Bit { Renderer = renderer, Velocity = velocity, Color = color, Life = 0.5f });
            }
        }

        // A rock falling from the roof onto a strong circle of the vein eater's cave-in, landing as the mark fills.
        void FallingRock(Mark mark, HazardState hazard, float progress)
        {
            var attack = AttackOf(hazard);
            // The black knight's falling roof too (2026-11-12).
            if (attack == null || attack.Shape != AttackShape.Circle || !(_bossInView.Id == "vein_eater" || _bossInView.Id == "black_knight" && attack.Target == AttackTarget.Each))
            {
                if (mark.Rock != null) mark.Rock.enabled = false;
                return;
            }
            if (mark.Rock == null)
            {
                mark.Rock = Renderer(_root.transform, "Falling rock", BoulderSprite, Color.white);
                mark.Rock.sortingOrder = 20500;
            }
            // Out of sight above for the first part, then dropping faster and faster.
            float fall = Mathf.Clamp01((progress - 0.35f) / 0.65f);
            mark.Rock.enabled = fall > 0;
            float height = 7 * (1 - fall * fall);
            mark.Rock.transform.localPosition = new Vector3(hazard.Position.X, hazard.Position.Y + height, 0);
            mark.Rock.transform.localScale = Vector3.one * (0.7f + 0.5f * fall) * Mathf.Max(1, hazard.Size / 1.2f);
        }

        // Where one of the vein eater's blows lands, a burst of its own; false for anyone else's (HazardLanded draws
        // those).
        bool VeinEaterLanded(HazardState hazard)
        {
            if (SamuraiLanded(hazard)) return true;
            var attack = AttackOf(hazard);
            if (attack == null || _bossInView.Id != "vein_eater") return false;
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            var along = new Vector3(hazard.Direction.X, hazard.Direction.Y, 0);
            switch (attack.Shape)
            {
                case AttackShape.Cone:
                    // The drills strike across the wedge: three hits, sparks and grit off them (no blade's whirl).
                    for (int i = -1; i <= 1; i++)
                    {
                        var turned = Quaternion.Euler(0, 0, i * attack.Angle * 0.3f) * along;
                        AddFlash(at + turned * (hazard.Size * 0.6f), 0.7f, SlashColor, FlashKind.Impact);
                        Kick(at + turned * (hazard.Size * 0.6f), turned, OreColor, 3);
                    }
                    AddFlash(at + along * (hazard.Size * 0.55f), hazard.Size * 0.4f, DirtColor, FlashKind.Burst);
                    Burst(at + along * (hazard.Size * 0.6f), OreColor, 1);
                    return true;
                case AttackShape.Burrow:
                    // Bursting up out of the floor.
                    AddFlash(at, hazard.Size * 1.1f, DirtColor, FlashKind.Burst);
                    AddFlash(at, hazard.Size * 1.6f, DirtColor, FlashKind.Ring);
                    AddFlash(at, 1.2f, Color.white, FlashKind.Impact);
                    Burst(at, DirtColor, 2.5f);
                    Burst(at, OreColor, 1);
                    _shake = Mathf.Max(_shake, 0.4f);
                    return true;
                case AttackShape.Leap:
                    // Coming down: a shockwave and the floor thrown up.
                    AddFlash(at, hazard.Size * 2, SlashColor, FlashKind.Ring);
                    AddFlash(at, hazard.Size * 1.3f, DirtColor, FlashKind.Ring);
                    AddFlash(at, hazard.Size, DirtColor, FlashKind.Burst);
                    Burst(at, DirtColor, 2.5f);
                    Burst(at, RockColor, 1.5f);
                    _shake = Mathf.Max(_shake, 0.45f);
                    return true;
                case AttackShape.Circle:
                    // The rock from the roof smashing.
                    AddFlash(at, hazard.Size, RockColor, FlashKind.Burst);
                    AddFlash(at, 0.8f, Color.white, FlashKind.Impact);
                    Burst(at, RockColor, 1.4f);
                    _shake = Mathf.Max(_shake, 0.2f);
                    return true;
                case AttackShape.Trail:
                    // A spike of rock bursting up through the floor.
                    AddFlash(at, hazard.Size * 0.9f, RockColor, FlashKind.Burst);
                    AddFlash(at, 0.6f, Color.white, FlashKind.Impact);
                    Kick(at, Vector3.up, RockColor, 5);
                    return true;
            }
            return false;
        }

        // ---- The shrine samurai (2026-11-12, Docs/Bosses.md 벚꽃 검귀 시구레): iaido. A long, plain stance with the hand on
        // the hilt (and the marks on the ground), then the draw too fast to see: the picture stops for a moment, a white
        // line is left where the blade went, and a beat later the cut opens along it in sparks and petals. ----

        static readonly Color SteelColor = new Color(0.86f, 0.92f, 1f);
        static readonly Color PetalColor = new Color(1f, 0.72f, 0.82f);

        // Seconds the picture holds still when the blade comes out, and how long the line it leaves stays.
        const float DrawStop = 0.07f;
        const float CutLine = 0.32f;
        // A beat after the line, the cut opens.
        const float CutDelay = 0.13f;

        // A way faced turned one step toward the viewer (+1, down the screen) or away (-1, up); facing straight up or down,
        // a step aside.
        static int Twist(int sector, int toward)
        {
            if (toward == 0) return sector;
            int target = toward > 0 ? 6 : 2;
            if (sector == target || sector == (target + 4) % 8) return (sector + toward + 8) % 8;
            int clockwise = (target - sector + 8) % 8;
            return clockwise < 4 ? (sector + 1) % 8 : (sector + 7) % 8;
        }

        // Faster than this (tiles a second) it is darting, not walking.
        const float DartSpeed = 6;
        // The sheathing ends this long after the cut.
        static float NotoDone => SamuraiArt.SecondsToHome;
        // How far through a draw's telegraph the blade starts easing out of the scabbard.
        const float PullFrom = 0.6f;

        // How long this wind-up lasts: a charge that runs again (the triple draw) takes less (World.AgainTelegraph).
        static float TelegraphOf(Body body, BossAttack attack) => Mathf.Max(0.1f, attack.Telegraph * (body.BossAgain ? World.AgainTelegraph : 1));

        // Cuts across (a cross, a crescent wave, the petals) come down from overhead; everything else is drawn from the
        // scabbard.
        static bool FromAbove(BossAttack attack) =>
            attack.Shape == AttackShape.Cross || attack.Shape == AttackShape.Burst || attack.Shape == AttackShape.Circle && attack.Target == AttackTarget.Each;

        static Sprite CrescentSprite => Sprites.Centered("shot/crescent", () => EffectArt.Swing(24, 2));

        // Cherry blossom (2026-11-12, the person: "make the blossom stand out, more splendid"): petals pale and deep and
        // whole flowers, in their own colours.
        static Sprite[] _petalSprites;
        static Sprite[] PetalSprites => _petalSprites ??= new[]
        {
            Sprites.Centered("fx/petal0", () => EffectArt.Petal(0)),
            Sprites.Centered("fx/petal1", () => EffectArt.Petal(1)),
            Sprites.Centered("fx/petal2", () => EffectArt.Petal(2)),
        };

        // Petals thrown from a point, along toward (every way when it is zero): slowing, blown down and aside, swaying
        // and turning over as they fall, bright until late. One in four is a whole flower.
        void Petals(Vector3 at, Vector3 toward, int count, float speed, float life, float spread = 0.3f)
        {
            if (!Feel.Bits) return;
            var sprites = PetalSprites;
            var way = toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector3.zero;
            float scatter = way == Vector3.zero ? 1 : 0.5f;
            for (int i = 0; i < count; i++)
            {
                var sprite = sprites[UnityEngine.Random.value < 0.25f ? 2 : UnityEngine.Random.value < 0.5f ? 0 : 1];
                var petal = Renderer(_root.transform, "Petal", sprite, Color.white);
                petal.transform.localPosition = at + (Vector3)(UnityEngine.Random.insideUnitCircle * spread);
                petal.sortingOrder = 19450;
                var velocity = (way + (Vector3)(UnityEngine.Random.insideUnitCircle * scatter)) * speed * UnityEngine.Random.Range(0.6f, 1.2f);
                _bits.Add(new Bit
                {
                    Renderer = petal, Velocity = velocity, Color = Color.white, Life = life * UnityEngine.Random.Range(0.8f, 1.25f),
                    Drag = 2.2f, Fall = new Vector3(-0.25f, -0.7f, 0), Sway = 0.8f, Spin = UnityEngine.Random.Range(-240f, 240f),
                    Phase = UnityEngine.Random.Range(0, Mathf.PI * 2), Tumble = true, LateFade = true,
                });
            }
        }

        // The air of its shrine: petals blowing in from the top and the right of the screen, thicker each phase.
        void PetalWind(int phase)
        {
            if (!Feel.Bits || UnityEngine.Random.value > 0.2f + 0.2f * phase) return;
            float high = _camera.orthographicSize, wide = high * _camera.aspect;
            var middle = _camera.transform.position - _root.transform.position;
            var at = UnityEngine.Random.value < 0.6f
                ? new Vector3(middle.x + UnityEngine.Random.Range(-wide, wide + 2), middle.y + high + 0.5f, 0)
                : new Vector3(middle.x + wide + 0.5f, middle.y + UnityEngine.Random.Range(-high, high), 0);
            var petal = Renderer(_root.transform, "Petal", PetalSprites[UnityEngine.Random.value < 0.2f ? 2 : UnityEngine.Random.value < 0.6f ? 0 : 1], Color.white);
            petal.transform.localPosition = at;
            petal.sortingOrder = 19300;
            float speed = UnityEngine.Random.Range(0.8f, 1.4f);
            _bits.Add(new Bit
            {
                Renderer = petal, Velocity = new Vector3(-1.3f, -1f, 0) * speed, Color = new Color(1, 1, 1, 0.85f),
                Life = (2 * high + 1) / speed + 1, Drag = 0, Sway = 0.9f, Spin = UnityEngine.Random.Range(-180f, 180f),
                Phase = UnityEngine.Random.Range(0, Mathf.PI * 2), Tumble = true, LateFade = true,
            });
        }

        // Afterimages (2026-11-12, the person: "leave afterimages on the dash"): its silhouette left where it passed,
        // white, pink and deep pink in turn, each fading where it stands, and a petal shed behind it now and then.
        // They are spaced by distance, not by time (2026-11-13): a dash leaves an even row of them however the frames
        // fall, and the stretch it covers while the picture stops for the draw is filled in.
        const float EchoGap = 0.55f;

        void Echo(Body body)
        {
            if (WhiteOf(body) == null) return;
            var now = body.Shape.transform.position - _root.transform.position;
            if (!body.Echoing)
            {
                body.Echoing = true;
                body.EchoFrom = now;
                Ghost(body, now);
                return;
            }
            var step = now - body.EchoFrom;
            float distance = step.magnitude;
            int count = Mathf.Min(12, Mathf.FloorToInt(distance / EchoGap));
            for (int k = 1; k <= count; k++) Ghost(body, body.EchoFrom + step * (k * EchoGap / distance));
            if (count > 0) body.EchoFrom = distance > 12 * EchoGap ? now : body.EchoFrom + step * (count * EchoGap / distance);
            if (Time.time < body.EchoAt) return;
            body.EchoAt = Time.time + 0.05f;
            Petals(body.Position + new Vector3(0, 0.4f, 0), body.LastPosition - body.Position, 1, 1.2f, 0.9f);
        }

        // The white picture of the frame a body shows, made now if its animation left it for later (Body.White).
        static Sprite WhiteOf(Body body)
        {
            if (body.White != null)
            {
                body.Glow.sprite = body.White();
                body.White = null;
            }
            return body.Glow.sprite;
        }

        void Ghost(Body body, Vector3 at)
        {
            body.Echoes++;
            var color = (body.Echoes % 3) switch
            {
                0 => new Color(1, 1, 1, 0.62f),
                1 => new Color(1f, 0.62f, 0.78f, 0.58f),
                _ => new Color(0.95f, 0.35f, 0.58f, 0.5f),
            };
            var ghost = Renderer(_root.transform, "Afterimage", body.Glow.sprite, color);
            ghost.transform.localPosition = at;
            ghost.flipX = body.Shape.flipX;
            ghost.sortingOrder = body.Shape.sortingOrder - 1;
            _bits.Add(new Bit { Renderer = ghost, Velocity = Vector3.zero, Color = color, Life = 0.34f });
        }

        // Things to happen a moment later: the cut opening along a line already drawn.
        readonly System.Collections.Generic.List<(float At, System.Action Do)> _later = new System.Collections.Generic.List<(float, System.Action)>();

        void Later(float seconds, System.Action action) => _later.Add((Time.time + seconds, action));

        void RunLater()
        {
            for (int i = _later.Count - 1; i >= 0; i--)
            {
                if (Time.time < _later[i].At) continue;
                var action = _later[i].Do;
                _later.RemoveAt(i);
                action();
            }
        }

        void AnimateSamurai(Body body, Vec2 facing, bool dead, bool moving, float speed)
        {
            var attack = body.BossAttack;
            float since = Time.time - body.BossSince;
            int sector = SectorOf(facing);
            var pose = SamuraiPose.Idle;
            int frame = Mathf.FloorToInt(Time.time * 2.4f) % SamuraiArt.FramesOf(SamuraiPose.Idle);
            int Through(SamuraiPose p, float progress) => Mathf.Clamp(Mathf.FloorToInt(progress * SamuraiArt.FramesOf(p)), 0, SamuraiArt.FramesOf(p) - 1);
            if (dead)
            {
                frame = 0;
            }
            else if (body.BossState == BossState.Roar)
            {
                // The blade drawn and raised, then shaking with the shout (2026-11-13, more frames: SamuraiArt).
                pose = SamuraiPose.Kiai;
                int rise = Mathf.FloorToInt(since / 0.035f);
                frame = rise < SamuraiArt.KiaiRise ? rise : SamuraiArt.KiaiRise + Mathf.FloorToInt(Time.time * 8) % 4;
            }
            else if (body.BossState == BossState.Charge || moving && speed > DartSpeed)
            {
                // The dash of a cut: low, the blade out and trailing. Darting in between attacks it runs as low but
                // with the blade still home and the hand on the hilt (2026-11-13: the blade no longer comes and goes
                // with every dart).
                pose = body.BossState == BossState.Charge ? SamuraiPose.Dash : SamuraiPose.Rush;
                frame = Mathf.FloorToInt(Time.time * 32) % SamuraiArt.FramesOf(pose);
            }
            else if (moving && (body.BossState == BossState.Approach || body.BossState == BossState.Recover || body.BossState == BossState.Dormant))
            {
                pose = SamuraiPose.Walk;
                frame = Mathf.FloorToInt(body.Walked * 16) % SamuraiArt.FramesOf(pose);
            }
            else if (body.BossState == BossState.Telegraph && attack != null)
            {
                float length = TelegraphOf(body, attack);
                float progress = Mathf.Clamp01(since / length);
                // The draw starts slow: held still in the stance, then the blade eased out of the scabbard over the last
                // part of the telegraph. A cut from above takes the stance and eases the blade out quickly, then raises it.
                // Between the runs of a chain of dashes (2026-11-13) the trailing blade goes home in a flash and is
                // eased out again: draw, sheathe, draw (before, the shorter wind-up stopped half way through the
                // stance and the blade simply vanished).
                if (body.BossAgain)
                {
                    float home = SamuraiArt.AgainSeconds;
                    if (since < home) (pose, frame) = (SamuraiPose.Again, Mathf.Min(SamuraiArt.FramesOf(SamuraiPose.Again) - 1, Mathf.FloorToInt(since / SamuraiArt.FrameSeconds(SamuraiPose.Again, 0))));
                    else (pose, frame) = (SamuraiPose.Pull, Through(SamuraiPose.Pull, (since - home) / Mathf.Max(0.05f, length - home)));
                }
                else if (FromAbove(attack))
                {
                    if (progress < 0.2f) (pose, frame) = (SamuraiPose.Stance, Through(SamuraiPose.Stance, progress / 0.2f));
                    else if (progress < 0.35f) (pose, frame) = (SamuraiPose.Pull, Through(SamuraiPose.Pull, (progress - 0.2f) / 0.15f));
                    else (pose, frame) = (SamuraiPose.Jodan, Through(SamuraiPose.Jodan, (progress - 0.35f) / 0.65f));
                }
                else if (progress < PullFrom) (pose, frame) = (SamuraiPose.Stance, Through(SamuraiPose.Stance, progress / PullFrom));
                else (pose, frame) = (SamuraiPose.Pull, Through(SamuraiPose.Pull, (progress - PullFrom) / (1 - PullFrom)));
            }
            else if (body.BossState == BossState.Recover && attack != null)
            {
                // Then all at once, each frame for its own time (SamuraiArt.FrameSeconds, the person's beat): the smear,
                // the cut held a moment, carried on and recovered; the blood shaken off; the blade turned and slid home;
                // the hand left on the hilt (zanshin), then let go.
                // The cut itself: down from overhead, round from behind out of a dash, right round for the turn, or the
                // plain draw across the front.
                var cut = FromAbove(attack) ? SamuraiPose.Kiri
                    : attack.Shape == AttackShape.Charge ? SamuraiPose.Sweep
                    : attack.Shape == AttackShape.Circle && attack.Target == AttackTarget.Self ? SamuraiPose.Whirl
                    : SamuraiPose.Draw;
                float left = since;
                foreach (var part in new[] { cut, SamuraiPose.Chiburi, SamuraiPose.Noto })
                {
                    for (int f = 0; f < SamuraiArt.FramesOf(part) && left >= 0; f++)
                    {
                        left -= SamuraiArt.FrameSeconds(part, f);
                        if (left < 0) (pose, frame) = (part, f);
                    }
                    if (left < 0) break;
                }
            }

            // The trunk turns with the cut: wound away from the viewer while it gathers, unwound toward the viewer as the
            // blade comes through, square again as it sheathes (SamuraiArt.TwistOf). The hand and the blade keep to the
            // way it faces whichever way the trunk is turned (2026-11-13: the aim), so the cut goes where the attack
            // does in all eight ways.
            var (view, turned, aim) = SamuraiArt.Facing(sector, pose, frame);
            // Where the mouth of its scabbard is, for the glint before a draw and the click of the guard after.
            var hip = SamuraiArt.HipOf(view);
            body.Hip = new Vector3((turned ? -hip.X : hip.X) / (float)Sprites.PixelsPerUnit, -body.Feet + hip.Y / (float)Sprites.PixelsPerUnit, 0);
            // Only the last phase looks different (darker armour), so the first two share their pictures.
            int phase = body.BossPhase >= 3 ? 3 : 1;
            int code = 400000 + (((((int)pose * 8 + (int)view) * 64 + frame) * 4 + phase) * 2 + (turned ? 1 : 0)) * 3 + aim + 1;
            if (code == body.Frame) return;
            body.Frame = code;
            string key = $"samurai/{view}/{pose}/{frame}/{phase}/{aim}";
            System.Func<PixelImage> draw = () => SamuraiArt.Draw(view, pose, frame, phase, aim);
            // It stands on its foot line, with rows of the canvas under it for a step or the blade toward the viewer.
            var pivot = new Vector2(0.5f, SamuraiArt.Foot / (float)SamuraiArt.Height);
            body.Shape.sprite = Sprites.Get(key, draw, pivot);
            // Its white picture waits until a flash or an afterimage shows it (WhiteOf): most of its many frames are
            // never seen white, and making them all would hold half again as much memory.
            body.White = () => Sprites.Get(key + "/white", () => draw().Silhouette(), pivot);
            body.Shape.flipX = turned;
            body.Glow.flipX = turned;
            body.Top = -body.Feet + SamuraiArt.BodyHeight / (float)Sprites.PixelsPerUnit;
        }

        // While it winds up, petals stir round it; dashing, it leaves afterimages; out of a dash, the line it cut.
        void SamuraiExtras(Body body, EntityView view)
        {
            var attack = view.BossAttack;
            float since = Time.time - body.BossSince;
            bool puff = Time.time >= body.DustAt;
            if (puff) body.DustAt = Time.time + 0.08f;
            var feet = body.Position;
            if (view.BossState == BossState.Telegraph && attack != null)
            {
                float progress = Mathf.Clamp01(since / TelegraphOf(body, attack));
                // Gathering itself: petals swirling in round it, more as the draw nears, a wide storm for the ultimate.
                bool strong = attack.Warning == AttackWarning.Strong;
                if (puff && UnityEngine.Random.value < 0.4f + progress)
                {
                    var middle = feet + new Vector3(0, 0.8f, 0);
                    var from = middle + (Vector3)(UnityEngine.Random.insideUnitCircle.normalized * (strong ? 3.2f : 2.2f));
                    var inward = middle - from;
                    Petals(from, inward + new Vector3(-inward.y, inward.x, 0) * 0.8f, strong ? 3 : 2, 2.4f, 0.8f, 0.1f);
                }
                // Just before the blade moves, the guard catches the light (over its head, for a cut from above): the
                // last warning, and the beat to dodge on (2026-11-13).
                if (!body.Glinted && since >= TelegraphOf(body, attack) - 0.16f && !attack.GoesAway)
                {
                    body.Glinted = true;
                    AddFlash(FromAbove(attack) ? feet + new Vector3(0, body.Top, 0) : feet + body.Hip, 0.5f, Color.white, FlashKind.Impact);
                }
                // A flash step: gone in a burst of petals just before it lands behind its target.
                if (attack.GoesAway && progress > 0.6f)
                {
                    body.Shape.color = new Color(1, 1, 1, Mathf.Max(0.08f, 1 - (progress - 0.6f) * 4));
                    if (!body.Vanished)
                    {
                        body.Vanished = true;
                        Echo(body);
                        Petals(feet + new Vector3(0, 0.8f, 0), Vector3.zero, 16, 4, 1.3f, 0.4f);
                    }
                }
            }
            if (view.BossState != BossState.Telegraph) body.Vanished = body.Glinted = false;
            // Petals on the air all through the fight, thicker each phase.
            if (puff) PetalWind(Mathf.Clamp(body.BossPhase, 1, 3));
            // The blade home: the guard clicks against the scabbard's mouth.
            if (view.BossState == BossState.Recover && attack != null && since >= NotoDone && !body.Clicked)
            {
                body.Clicked = true;
                var hip = feet + body.Hip;
                AddFlash(hip, 0.4f, Color.white, FlashKind.Impact);
                Hear(Sound.Click, hip, 1);
                // The air the cut stirred lets its blossom go: a few petals drifting down round it (the blade is home,
                // so nothing hides it).
                Petals(feet + new Vector3(0, 0.9f, 0), Vector3.zero, 6, 1.6f, 1.6f, 0.7f);
            }
            if (view.BossState != BossState.Recover) body.Clicked = false;
            // Darting in, or dashing: streaks behind it.
            var moved = body.Position - body.LastPosition;
            bool darting = moved.magnitude / Mathf.Max(1e-3f, Time.deltaTime) > DartSpeed;
            if (darting || view.BossState == BossState.Charge) SpeedLines(feet, moved);
            // Afterimages all along a dash or a dart (2026-11-12, the person: "leave afterimages on the dash").
            if (darting || view.BossState == BossState.Charge) Echo(body);
            else body.Echoing = false;
            // The ultimate's wind-up dims the room.
            bool gathering = view.BossState == BossState.Telegraph && attack != null && attack.Warning == AttackWarning.Strong;
            Dim(gathering ? 0.5f * Mathf.Clamp01(since / Mathf.Max(0.1f, attack.Telegraph)) : 0);
            if (view.BossState == BossState.Charge)
            {
                if (!body.Dashing)
                {
                    body.Dashing = true;
                    body.DashFrom = feet;
                }
            }
            else if (body.Dashing)
            {
                // Out of the dash: the line it cut, and the cut opening along it a beat later.
                body.Dashing = false;
                Cut(body.DashFrom + new Vector3(0, 0.5f, 0), feet + new Vector3(0, 0.5f, 0), 1.2f);
            }
        }

        // The draw itself: the picture stops for an instant, the sound of steel.
        void SamuraiDraws(Body body)
        {
            // A long lunge into the cut and a white flash of the whole screen.
            body.LungeReach = 6;
            ScreenFlash(new Color(1, 1, 1, 0.35f), 0.09f);
            if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, DrawStop);
            _shake = Mathf.Max(_shake, 0.18f);
            Hear(Sound.Heavy, body.Position, 1);
            Hear(Sound.Crit, body.Position, 0.6f);
            // Blossom blown off the draw: a few petals only, and those thrown back behind it, so the blade stays clear
            // in front (2026-11-12, the person: "fewer petals right in front of the blade as it swings"); a ring of
            // pink at its feet and a star of light where the blade left the scabbard.
            var back = -(Vector3)body.Lunge;
            Petals(body.Position + new Vector3(0, 0.8f, 0) + back * 0.4f, back, 5, 4.5f, 1.3f, 0.35f);
            AddFlash(body.Position + new Vector3(0, 0.2f, 0), 1.6f, PetalColor, FlashKind.Ring);
            AddFlash(body.Position + body.Hip, 0.7f, Color.white, FlashKind.Impact);
        }

        // Where the blade went (2026-11-13): not a bar but a blade of light with a point at each end (EffectArt.Slash: a
        // white core, pale steel, a blush of blossom), over a soft pink glow; whole for a moment, then closing to
        // nothing across its width. A beat later the cut opens: its two edges part, sparks and a few petals along it.
        void Cut(Vector3 from, Vector3 to, float heavy)
        {
            var along = to - from;
            float length = along.magnitude;
            if (length < 0.05f) return;
            float angle = Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg;
            var normal = new Vector3(-along.y, along.x, 0) / length;
            var middle = (from + to) / 2;
            void Line(float span, float weight, Color color, float life, int order, Vector3 drift)
            {
                // Drawn at its own length (in steps of half a tile), so its pixels stay the world's.
                int pixels = Mathf.Clamp(Mathf.RoundToInt(span * Sprites.PixelsPerUnit / 8f) * 8, 16, 400);
                var line = Renderer(_root.transform, "Cut", Sprites.Centered("fx/slash/" + pixels, () => EffectArt.Slash(pixels)), color);
                line.transform.localPosition = middle;
                line.transform.localRotation = Quaternion.Euler(0, 0, angle);
                var scale = new Vector3(span * Sprites.PixelsPerUnit / pixels, weight, 1);
                line.transform.localScale = scale;
                line.sortingOrder = order;
                _bits.Add(new Bit { Renderer = line, Velocity = drift, Color = color, Life = life, Thin = true, LateFade = true, Scale = scale });
            }
            Line(length + 1.3f, 1.7f * heavy, new Color(1f, 0.6f, 0.78f, 0.4f), CutLine * 1.3f, 19499, Vector3.zero);
            Line(length + 0.9f, heavy, Color.white, CutLine, 19500, Vector3.zero);
            Later(CutDelay, () =>
            {
                // The two edges of the cut part and fade.
                Line(length + 0.5f, 0.45f, Color.white, 0.25f, 19502, normal * 1.6f);
                Line(length + 0.5f, 0.45f, new Color(1f, 0.75f, 0.85f), 0.25f, 19502, -normal * 1.6f);
                int steps = Mathf.Max(2, Mathf.RoundToInt(length / 0.9f));
                for (int i = 0; i <= steps; i++)
                {
                    var at = Vector3.Lerp(from, to, i / (float)steps);
                    AddFlash(at, 0.6f * heavy, SteelColor, FlashKind.Impact);
                    Kick(at, normal * (i % 2 == 0 ? 1 : -1), SteelColor, 2);
                    if (i % 2 == 0) Petals(at, normal * (i % 4 == 0 ? 1 : -1), 1, 3.2f, 1.2f, 0.2f);
                }
                AddFlash(middle, Mathf.Min(2.4f, length * 0.35f) * heavy, PetalColor, FlashKind.Burst);
                _shake = Mathf.Max(_shake, 0.16f * heavy);
                Hear(Sound.Hit, middle, 0.8f);
            });
        }

        // A cut that sweeps round (2026-11-13): the same blade of light bent along an arc (EffectArt.SlashArc), round a
        // point toward the way it faces, so many degrees wide: one curve where there were straight pieces. A beat
        // later it opens: one edge rides out and one draws in, sparks along it, and petals thrown outward, past the
        // blade's reach.
        void CutArc(Vector3 at, Vector3 along, float radius, float degrees, float heavy)
        {
            int pixels = Mathf.Clamp(Mathf.RoundToInt(radius * Sprites.PixelsPerUnit / 4f) * 4, 12, 160);
            int wide = Mathf.Clamp(Mathf.RoundToInt(degrees / 10f) * 10, 30, 340);
            var sprite = Sprites.Centered($"fx/slasharc/{pixels}/{wide}", () => EffectArt.SlashArc(pixels, wide));
            float facing = Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg;
            float fit = radius * Sprites.PixelsPerUnit / pixels;
            void Sweep(float size, Color color, float life, int order, bool late)
            {
                var arc = Renderer(_root.transform, "Cut", sprite, color);
                arc.transform.localPosition = at;
                arc.transform.localRotation = Quaternion.Euler(0, 0, facing);
                arc.transform.localScale = Vector3.one * (fit * size);
                arc.sortingOrder = order;
                _bits.Add(new Bit { Renderer = arc, Velocity = Vector3.zero, Color = color, Life = life, LateFade = late });
            }
            Sweep(1.04f, new Color(1f, 0.6f, 0.78f, 0.45f), CutLine * 1.3f, 19499, true);
            Sweep(1, Color.white, CutLine, 19500, true);
            Later(CutDelay, () =>
            {
                Sweep(1.08f, new Color(1, 1, 1, 0.85f), 0.24f, 19502, false);
                Sweep(0.92f, new Color(1f, 0.75f, 0.85f, 0.85f), 0.24f, 19502, false);
                int steps = Mathf.Max(3, Mathf.RoundToInt(radius * degrees * Mathf.Deg2Rad / 0.9f));
                for (int i = 0; i <= steps; i++)
                {
                    float a = (facing - degrees / 2 + degrees * i / steps) * Mathf.Deg2Rad;
                    var outward = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                    var point = at + outward * radius;
                    AddFlash(point, 0.6f * heavy, SteelColor, FlashKind.Impact);
                    Kick(point, outward, SteelColor, 2);
                    if (i % 2 == 0) Petals(point + outward * 0.3f, outward, 1, 3.2f, 1.2f, 0.2f);
                }
                var heart = at + along.normalized * (radius * 0.85f);
                AddFlash(heart, Mathf.Min(2.4f, radius * 0.6f) * heavy, PetalColor, FlashKind.Burst);
                _shake = Mathf.Max(_shake, 0.2f * heavy);
                Hear(Sound.Hit, heart, 0.9f);
            });
        }

        // The whole screen washed white for an instant (the draw), or darkened (the ultimate's wind-up).
        void ScreenFlash(Color color, float life)
        {
            var flash = Renderer(_root.transform, "Screen flash", Shapes.Square, color);
            var at = _camera.transform.position;
            flash.transform.localPosition = new Vector3(at.x, at.y, 0) - _root.transform.position;
            flash.transform.localScale = new Vector3(60, 40, 1);
            flash.sortingOrder = 30000;
            _bits.Add(new Bit { Renderer = flash, Velocity = Vector3.zero, Color = color, Life = life });
        }

        SpriteRenderer _dim;

        // The ultimate's wind-up: the room dims as it gathers itself (the marks and effects stay bright over it).
        void Dim(float amount)
        {
            if (_dim == null)
            {
                _dim = Renderer(_root.transform, "Dim", Shapes.Square, Color.clear);
                _dim.transform.localScale = new Vector3(60, 40, 1);
                _dim.sortingOrder = DarkOrder + 5;
            }
            var at = _camera.transform.position;
            _dim.transform.localPosition = new Vector3(at.x, at.y, 0) - _root.transform.position;
            _dim.color = new Color(0.05f, 0, 0.04f, amount);
        }

        // Thin white streaks behind something moving fast.
        void SpeedLines(Vector3 at, Vector3 moving)
        {
            if (moving.sqrMagnitude < 1e-4f) return;
            var dir = moving.normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            for (int i = 0; i < 2; i++)
            {
                var line = Renderer(_root.transform, "Speed", Shapes.Square, new Color(1, 1, 1, 0.7f));
                line.transform.localPosition = at - dir * UnityEngine.Random.Range(0.6f, 1.4f) + new Vector3(-dir.y, dir.x, 0) * UnityEngine.Random.Range(-0.7f, 0.7f) + new Vector3(0, 0.8f, 0);
                line.transform.localRotation = Quaternion.Euler(0, 0, angle);
                line.transform.localScale = new Vector3(UnityEngine.Random.Range(0.8f, 1.6f), 1f / Sprites.PixelsPerUnit, 1);
                line.sortingOrder = 19400;
                _bits.Add(new Bit { Renderer = line, Velocity = -dir * 4, Color = new Color(1, 1, 1, 0.7f), Life = 0.16f });
            }
        }

        // Where one of the samurai's cuts lands: its line drawn the way the blade went, the cut opening after.
        bool SamuraiLanded(HazardState hazard)
        {
            var attack = AttackOf(hazard);
            if (attack == null || _bossInView.Id != "shrine_samurai") return false;
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            var along = new Vector3(hazard.Direction.X, hazard.Direction.Y, 0);
            var across = new Vector3(-along.y, along.x, 0);
            switch (attack.Shape)
            {
                case AttackShape.Cone:
                {
                    // A sweep across the front: one curve of light round the wedge.
                    CutArc(at, along, hazard.Size * 0.72f, attack.Angle, 1.1f);
                    return true;
                }
                case AttackShape.Line:
                    // One arm of the cross: a long straight cut.
                    Cut(at, at + along * hazard.Size, 1.2f);
                    return true;
                case AttackShape.Ring:
                    // The thousand flashes: lines every way through the band, a white shockwave, the picture stopping.
                    for (int i = 0; i < 9; i++)
                    {
                        float a = (i * 40 + UnityEngine.Random.Range(-12f, 12f)) * Mathf.Deg2Rad;
                        var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                        var mid = at + dir * ((hazard.Size + hazard.Width) / 2);
                        var cross = new Vector3(-dir.y, dir.x, 0) * 2.6f;
                        Cut(mid - cross, mid + cross, 1.3f);
                    }
                    AddFlash(at, hazard.Size, Color.white, FlashKind.Ring);
                    AddFlash(at, hazard.Size * 0.7f, PetalColor, FlashKind.Ring);
                    // A storm of blossom whirling out from it.
                    for (int i = 0; i < 40; i++)
                    {
                        float a = i * 9 * Mathf.Deg2Rad;
                        var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                        Petals(at + dir * 0.5f, dir + new Vector3(-dir.y, dir.x, 0) * 0.7f, 1, hazard.Size * 1.4f, 1.8f, 0.2f);
                    }
                    ScreenFlash(new Color(1, 1, 1, 0.75f), 0.18f);
                    if (Settings.ScreenShake && Feel.HitStop) _hitStop = Mathf.Max(_hitStop, DrawStop * 2.2f);
                    _shake = Mathf.Max(_shake, 0.45f);
                    return true;
                case AttackShape.Circle when attack.Target == AttackTarget.Self:
                    // A turn with the blade: its curve of light almost all the way round, opening behind it.
                    CutArc(at, along.sqrMagnitude > 1e-4f ? along : Vector3.right, hazard.Size * 0.8f, 330, 1);
                    return true;
                case AttackShape.Circle:
                    // A cut in the air: an X where it lands, in petals.
                    Cut(at - (along + across) * hazard.Size * 0.8f, at + (along + across) * hazard.Size * 0.8f, 0.9f);
                    Cut(at - (along - across) * hazard.Size * 0.8f, at + (along - across) * hazard.Size * 0.8f, 0.9f);
                    Petals(at, Vector3.zero, 10, 3, 1.4f, hazard.Size * 0.5f);
                    return true;
                case AttackShape.Leap:
                    // Out of nowhere beside its target: one cut straight down through where it stood.
                    Cut(at + new Vector3(-0.8f, 1.6f, 0), at + new Vector3(0.8f, -0.8f, 0), 1.3f);
                    Petals(at + new Vector3(0, 0.8f, 0), Vector3.zero, 16, 4, 1.3f, 0.4f);
                    return true;
            }
            return false;
        }
    }
}
