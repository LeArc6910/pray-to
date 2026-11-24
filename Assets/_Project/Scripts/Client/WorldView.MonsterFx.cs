using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // Ordinary monsters' warnings and effects (2026-11-21, Docs/Combat.md 몬스터의 공격 시트). An attack that starts at
    // the monster (a wedge, a line, a circle round itself, a rush) comes over the wire only as the monster's state and
    // the attack's place in the sheet; its mark is laid here from the sheet and the way the monster faced. Marks away
    // from the monster (a landing, a trap, a pool) come as hazards, named as a monster's with the kind of attack. Both
    // are drawn the same: a faint fill that grows, and MonsterFx's thin red rim at the real reach, the monster's own
    // wind-up cue, the blow and what thins out. Nothing here decides anything about the game. Feel.MonsterFx off draws
    // the marks the way a boss's are and nothing else.
    sealed partial class WorldView
    {
        sealed class MobEffect
        {
            public MobFx Fx;
            public MobContext Context;
            public Vector3 At;
            public float Age;
            public Body Follow;
        }

        sealed class MobShot
        {
            public MonsterAttackShape Shape;
            public FxRamp Ramp;
            public uint Tint;
            public float Dir;
            public int Seed;
        }

        struct MobDraw
        {
            public MobFx Fx;
            public MobContext Context;
            public Vector3 At;
            public float Time;
        }

        readonly List<MobEffect> _mobFx = new List<MobEffect>();
        // What is drawn anew every frame (marks, wind-up cues, pools, loops), gathered while the bodies and the marks
        // are gone through and laid on the pool's sprites once those are free (DrawMonsterFx).
        readonly List<MobDraw> _mobDraws = new List<MobDraw>();
        readonly Dictionary<int, MobShot> _mobShots = new Dictionary<int, MobShot>();
        // Who mends whom (SimEventType.Tether).
        readonly Dictionary<int, int> _tethers = new Dictionary<int, int>();
        // The marks of attacks that start at their monster, made here each frame.
        readonly List<HazardView> _attachedMarks = new List<HazardView>();
        // Whose colours a mark is drawn in and which attack laid it, by the mark's id.
        readonly Dictionary<int, (FxRamp Ramp, uint Tint, MonsterAttack Attack, MobLook Look)> _markLooks = new Dictionary<int, (FxRamp, uint, MonsterAttack, MobLook)>();
        int _mobPieces;
        int _mobShotsDrawn;
        int _mobShotsShown;

        // For the playtests: monsters' marks and pools on the ground, their one-off effects running, the pieces drawn
        // for all of it last frame and their shots drawn in their own look.
        public int MonsterMarksShown => _marks.Values.Count(m => m.State.FromMonster && !IsMonsterPool(m.State.Shape) && !IsPool(m.State));
        public int MonsterPoolsShown => _marks.Values.Count(m => m.State.FromMonster && IsMonsterPool(m.State.Shape));
        public int MonsterFxAlive => _mobFx.Count;
        public int MonsterFxPieces => _mobPieces;
        public int MonsterShotsDrawn => _mobShotsShown;
        // Blows of monsters' attacks shown since the game began (a count that only grows: a playtest that looks a
        // moment late still sees that one was shown).
        public int MonsterBlowsShown { get; private set; }
        // The marks over the heads of a pack that noticed someone (SimEventType.Alerted, Docs/WorldRework.md 무리).
        public int AlertsShown { get; private set; }
        float _alertHeard;
        static readonly Color AlertColor = new Color(1f, 0.82f, 0.25f);
        // Bombers' blasts shown since the game began.
        public int ExplosionsShown { get; private set; }
        // Mounds drawn last frame for monsters under the ground, and the lines between those that stand in a line.
        public int MonsterMoundsShown { get; private set; }
        public int MonsterLinksShown { get; private set; }
        int _mounds;
        int _links;

        const float Ppu = Sprites.PixelsPerUnit;

        static bool IsMonsterPool(HazardShape shape) =>
            shape == HazardShape.Fire || shape == HazardShape.Ice || shape == HazardShape.Spore || shape == HazardShape.Sand || shape == HazardShape.Balm;

        // The host's clock in seconds: marks made here run on it, so they stop when the host does (a playtest's picture).
        double MobNow => _client.HostTick / World.TicksPerSecond;

        static Sound TellSound(Tell tell) => tell switch
        {
            Tell.Dash => Sound.MobDash,
            Tell.Barrage => Sound.MobBarrage,
            Tell.Call => Sound.MobCall,
            _ => Sound.MobArea,
        };

        void AddMobFx(MobFx fx, Vector3 at, MobContext context, Body follow = null)
        {
            if (!Feel.MonsterFx || _mobFx.Count >= FxMostAlive) return;
            context.Seed = ++_fxSeed & 0xFFFF;
            _mobFx.Add(new MobEffect { Fx = fx, Context = context, At = at, Follow = follow });
        }

        void QueueMob(MobFx fx, Vector3 at, MobContext context, float time)
        {
            if (Feel.MonsterFx) _mobDraws.Add(new MobDraw { Fx = fx, Context = context, At = at, Time = time });
        }

        // A monster's body, after it is placed: its size, what it is winding up (the mark, the cue, the hop of a leap)
        // and the marks it carries (dazed, empowered, mending).
        void MonsterExtras(Body body, EntityView view)
        {
            if (!body.MobLooked)
            {
                body.MobLooked = true;
                body.MobRamp = MonsterFx.RampOf(view.Monster.Id, view.Monster.Element);
                body.MobTint = MonsterFx.TintOf(body.Color);
                body.MobKind = MonsterFx.LookOf(view.Monster.Id);
                body.MobLast = body.Position;
            }
            // One under the ground is not drawn as itself: only its mound, and a ripple behind it when it travels.
            bool buried = view.Buried && Feel.MonsterFx;
            // An elite leader is no longer drawn bigger (it was 1.3 times, its pixels uneven): it wears a gold ring
            // round its picture (AnimateCreature, CreatureArt.Crowned), its glow and its name tag (2026-11-24, B0b).
            float size = buried ? 0 : view.Small ? 0.6f : 1;
            if (body.Root.localScale.x != size) body.Root.localScale = Vector3.one * size;

            var state = view.EnemyState;
            var attack = view.MonsterAttack;
            bool winding = state == EnemyState.Windup && attack != null;
            var feet = body.Position - new Vector3(0, body.Feet, 0);
            if (buried)
            {
                var went = body.Position - body.MobLast;
                bool moving = went.sqrMagnitude > 1e-6f;
                if (moving) body.MobDir = new Vec2(went.x, went.y).Normalized;
                QueueMob(MobFx.Mound, feet, new MobContext { Ramp = body.MobRamp, Tint = body.MobTint, Dir = body.MobDir.Angle, Armed = moving, Seed = view.Id & 0xFFFF, Look = body.MobKind },
                    Time.time);
                _mounds++;
            }
            body.MobLast = body.Position;
            body.MobBuried = buried;
            // Shoulder to shoulder (MonsterTraitKind.Aura): a thin red line to the nearest of its kind in reach, drawn
            // once, from the one with the lower id.
            var aura = view.Monster.Trait(MonsterTraitKind.Aura);
            if (aura != null && !buried)
            {
                float reach = aura.Number(1, 1.5f);
                Body mate = null;
                foreach (var other in _others.Values)
                {
                    if (other == body || other.Kind != BodyKind.Monster || other.Id != body.Id) continue;
                    float distance = Vector2.Distance(other.Position, body.Position);
                    if (distance > reach) continue;
                    reach = distance;
                    mate = other;
                }
                if (mate != null && body.Position.x <= mate.Position.x)
                {
                    var line = mate.Position - new Vector3(0, mate.Feet, 0) - feet;
                    QueueMob(MobFx.Link, feet, new MobContext { Dir = Mathf.Atan2(line.y, line.x), Size = line.magnitude * Ppu }, Time.time);
                    _links++;
                }
            }
            // A swoop leaves afterimages behind it.
            if (state == EnemyState.Charge && body.MobAttack != null && body.MobAttack.Shape == MonsterAttackShape.Swoop)
                QueueMob(MobFx.Wake, feet, new MobContext { Ramp = body.MobRamp, Tint = body.MobTint, Dir = view.Facing.Angle, Look = body.MobKind }, Time.time);
            if (winding && (!body.MobWinding || body.MobAttack != attack))
            {
                body.MobSince = MobNow;
                body.MobElapsed = 0;
                body.MobDir = view.Facing;
                body.MobAt = view.Position;
                // A charge's later runs wind up in less time (World.AgainTelegraph); an elite leader's big attack in more
                // (World.TelegraphOf).
                body.MobTelegraph = World.TelegraphOf(view.Elite, view.Monster, attack) * (body.MobState == EnemyState.Charge ? World.AgainTelegraph : 1);
                // A bomber's fuse is its own sound: beeps that come faster.
                Hear(attack.Shape == MonsterAttackShape.Explode ? Sound.Fuse : TellSound(attack.Tell), body.Position, 0.8f);
            }
            bool wasWinding = body.MobWinding;
            body.MobWinding = winding;
            body.MobState = state;
            if (attack != null) body.MobAttack = attack;
            else if (state == EnemyState.Idle || state == EnemyState.Chase) body.MobAttack = null;

            var doing = body.MobAttack;
            if (doing != null && (state == EnemyState.Windup || state == EnemyState.Recover || wasWinding))
            {
                float before = body.MobElapsed;
                float elapsed = (float)(MobNow - body.MobSince);
                body.MobElapsed = elapsed;
                if (doing.Attached) AttachMarks(body, view, doing, before, elapsed, winding, wasWinding, state);
                if (winding)
                {
                    QueueMob(MobFx.Tell, feet, new MobContext
                    {
                        Shape = doing.Shape, Ramp = body.MobRamp, Tint = body.MobTint, Dir = body.MobDir.Angle, Life = body.MobTelegraph,
                        Size = (doing.Radius + World.PlayerRadius) * Ppu, Seed = view.Id & 0xFFFF, Look = body.MobKind,
                    }, elapsed);
                    if (doing.Shape == MonsterAttackShape.Leap && Feel.MonsterFx) Hop(body, elapsed, body.MobTelegraph);
                }
            }

            EliteExtras(body, view, winding ? attack : null, feet);
            if (state == EnemyState.Stagger) QueueMob(MobFx.Stars, feet, new MobContext(), Time.time);
            if (view.Empowered) QueueMob(MobFx.Empowered, feet, new MobContext(), Time.time + view.Id * 0.3f);
            if (_tethers.TryGetValue(view.Id, out int allyId))
            {
                if (state != EnemyState.Channel && state != EnemyState.Windup) _tethers.Remove(view.Id);
                else if (state == EnemyState.Channel && _others.TryGetValue(allyId, out var ally))
                {
                    var allyFeet = ally.Position - new Vector3(0, ally.Feet, 0);
                    var line = allyFeet - feet;
                    QueueMob(MobFx.Tether, feet, new MobContext { Dir = Mathf.Atan2(line.y, line.x), Size = line.magnitude * Ppu }, Time.time);
                    QueueMob(MobFx.Mended, allyFeet, new MobContext(), Time.time);
                }
            }
        }

        // A leaper gathers itself low, then goes up and over in the last moment of its wind-up (World.HopSeconds).
        static void Hop(Body body, float elapsed, float telegraph)
        {
            float start = Mathf.Max(0, telegraph - World.HopSeconds);
            var shape = body.Shape.transform;
            if (elapsed < start)
            {
                float low = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, start));
                shape.localScale = new Vector3(1 + 0.15f * low, 1 - 0.25f * low, 1);
            }
            else
            {
                float u = Mathf.Clamp01((elapsed - start) / World.HopSeconds);
                shape.localScale = new Vector3(0.9f, 1.15f, 1);
                shape.localPosition += new Vector3(0, Mathf.Sin(u * Mathf.PI) * 0.7f, 0);
            }
            body.Glow.transform.localScale = shape.localScale;
            body.Glow.transform.localPosition = shape.localPosition;
        }

        // The marks of an attack that starts at the monster, as the host lays them (World.StartAttack): made from the
        // sheet, where the monster stood and the way it faced when it began. When one's time is up, its blow is shown.
        void AttachMarks(Body body, EntityView view, MonsterAttack attack, float before, float elapsed, bool winding, bool wasWinding, EnemyState state)
        {
            int count = attack.Shape == MonsterAttackShape.Sweep ? Mathf.Max(1, attack.Count) : 1;
            bool rush = attack.Tell == Tell.Dash;
            for (int i = 0; i < count; i++)
            {
                float total = body.MobTelegraph + i * World.SweepSeconds;
                var facing = body.MobDir;
                if (count > 1) facing = Vec2.FromAngle(facing.Angle + (i % 2 == 0 ? -1 : 1) * attack.Angle * World.SweepTurn * Mathf.Deg2Rad);
                // An exploder carries its circle with it.
                var at = attack.Shape == MonsterAttackShape.Explode ? view.Position : body.MobAt;
                // From the sheet and the body, as the host lays it (a big body's reach from its edge).
                var (shape, reach, width) = MonsterAttackSheet.MarkOf(view.Monster, attack);
                int id = -1 - (view.Id * 4 + i);
                // The first is over when the wind-up is (or was broken off); the later wedges of a sweep by the clock.
                bool over = i == 0 ? !winding : elapsed >= total;
                if (over)
                {
                    // It landed if the monster went on to rest from it (or, straight into the next attack, by the
                    // clock). Not by the clock alone: the screen may have seen the wind-up begin late (2026-11-22: a
                    // slime's half-second bite showed no blow when the first of it came a tenth of a second late).
                    bool landed = i == 0 ? wasWinding && (state == EnemyState.Recover || elapsed >= total * 0.8f) : before < total;
                    if (landed && !rush) MonsterBlow(attack.Shape, shape, new Vector3(at.X, at.Y, 0), facing, reach, width, body.MobRamp, body.MobTint, body.MobKind);
                    continue;
                }
                _markLooks[id] = (body.MobRamp, body.MobTint, attack, body.MobKind);
                _attachedMarks.Add(new HazardView(
                    new HazardState(id, shape, at, facing, reach, width, Mathf.CeilToInt((total - elapsed) * World.TicksPerSecond), Mathf.CeilToInt(total * World.TicksPerSecond),
                        !rush, World.WarningOf(view.Elite, view.Monster, attack), (byte)(HazardState.MonsterMark | ((int)attack.Shape + 1))),
                    Mathf.Clamp01(elapsed / total)));
            }
        }

        Mark MakeMonsterMark(HazardState hazard)
        {
            // Pools, pulls and traps are circles.
            bool round = hazard.Shape != HazardShape.Cone && hazard.Shape != HazardShape.Line && hazard.Shape != HazardShape.Ring;
            var mark = MakeMark(round
                ? new HazardState(hazard.Id, HazardShape.Circle, hazard.Position, hazard.Direction, hazard.Size, hazard.Width, hazard.TicksLeft, hazard.Duration, hazard.Harmful)
                : hazard);
            mark.State = hazard;
            return mark;
        }

        // Whose colours a monster's mark away from it is drawn in: the nearest monster doing that kind of attack.
        (FxRamp Ramp, uint Tint, MonsterAttack Attack, MobLook Look) LookOf(HazardState hazard)
        {
            if (_markLooks.TryGetValue(hazard.Id, out var look)) return look;
            look = (FxRamp.Plain, 0x1D8D8D8u, null, MobLook.Plain);
            float best = 14;
            foreach (var body in _others.Values)
            {
                if (body.Kind != BodyKind.Monster || body.MobAttack == null || (int)body.MobAttack.Shape != hazard.MonsterShape) continue;
                float distance = Vector2.Distance(body.Position, new Vector2(hazard.Position.X, hazard.Position.Y));
                if (distance >= best) continue;
                best = distance;
                look = (body.MobRamp, body.MobTint, body.MobAttack, body.MobKind);
            }
            if (_markLooks.Count > 96) _markLooks.Clear();
            _markLooks[hazard.Id] = look;
            return look;
        }

        // One of a monster's marks this frame: the faint fill on the mark's own sprites, and the rim and the rest as
        // MonsterFx's pieces. What a blow left on the floor is all pieces.
        void DrawMonsterMark(Mark mark, HazardView view)
        {
            var hazard = view.State;
            mark.State = hazard;
            mark.Root.localPosition = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            mark.Root.localRotation = Quaternion.Euler(0, 0, hazard.Direction.Angle * Mathf.Rad2Deg);
            var at = mark.Root.localPosition;
            float p = view.Progress;
            if (IsMonsterPool(hazard.Shape))
            {
                mark.Edge.color = Color.clear;
                mark.Fill.color = Color.clear;
                if (!Feel.MonsterFx)
                {
                    // The plain look: a tinted disc.
                    var plain = hazard.Shape == HazardShape.Balm ? new Color(0.4f, 0.9f, 0.4f) : DangerColor;
                    mark.Fill.transform.localScale = Vector3.one * (hazard.Size * 2);
                    mark.Fill.color = new Color(plain.r, plain.g, plain.b, 0.2f);
                    return;
                }
                float seconds = hazard.Duration / (float)World.TicksPerSecond;
                float fade = Mathf.Clamp01(Mathf.Min(p * seconds / 0.2f, (1 - p) * seconds / 0.35f));
                QueueMob(MobFx.Pool, at, new MobContext { Area = hazard.Shape, Size = hazard.Size * Ppu, Progress = fade, Seed = hazard.Id & 0xFFFF }, Time.time);
                return;
            }

            int shapeIndex = hazard.MonsterShape;
            bool flash = shapeIndex + 1 == Hazard.DeathFlash;
            var shape = flash ? MonsterAttackShape.Slam : (MonsterAttackShape)shapeIndex;
            bool blink = shape == MonsterAttackShape.Blink;
            var color = hazard.Harmful ? DangerColor : PathColor;
            if (Feel.MonsterFx)
            {
                // The rim is the pieces'; the fill only fills in, faintly.
                mark.Edge.color = Color.clear;
                mark.Fill.color = blink || flash ? Color.clear : new Color(color.r, color.g, color.b, 0.05f + 0.13f * p);
            }
            else
            {
                mark.Edge.color = new Color(color.r, color.g, color.b, 0.22f);
                mark.Fill.color = new Color(color.r, color.g, color.b, 0.25f + 0.3f * p);
            }
            switch (hazard.Shape)
            {
                case HazardShape.Ring:
                    mark.Fill.transform.localScale = Vector3.one * (hazard.Size * 2);
                    break;
                case HazardShape.Line:
                    mark.Fill.transform.localScale = new Vector3(hazard.Size * p, hazard.Width, 1);
                    mark.Fill.transform.localPosition = new Vector3(hazard.Size * p * 0.5f, 0, 0);
                    break;
                default:
                    mark.Fill.transform.localScale = Vector3.one * (hazard.Size * 2 * p);
                    break;
            }
            if (!Feel.MonsterFx) return;

            var look = LookOf(hazard);
            bool line = hazard.Shape == HazardShape.Line, cone = hazard.Shape == HazardShape.Cone;
            // A trap is armed once its attack's telegraph is over; the rest of its time it waits.
            bool armed = hazard.Shape == HazardShape.Mine
                && p >= (look.Attack != null ? look.Attack.Telegraph / (look.Attack.Telegraph + look.Attack.Duration) : 0.15f);
            if (armed && !mark.Armed)
            {
                mark.Armed = true;
                Hear(Sound.TrapSet, at, 0.7f);
            }
            QueueMob(MobFx.Mark, at, new MobContext
            {
                Shape = shape,
                Area = hazard.Shape,
                Ramp = look.Ramp,
                Tint = look.Tint,
                Dir = hazard.Direction.Angle,
                // The rim is where the hit reaches: the player's own radius counts (Docs/SkillEffects.md rule 5).
                Size = (hazard.Size + (blink ? 0 : World.PlayerRadius)) * Ppu,
                Width = cone ? hazard.Width : line ? (hazard.Width + 2 * World.PlayerRadius) * Ppu : hazard.Width * Ppu,
                Progress = p,
                Seed = hazard.Id & 0xFFFF,
                Armed = armed,
                Harmless = flash,
                Look = look.Look,
            }, Time.time);
        }

        // A monster's mark away from it has landed (the host's Impact): its blow, in its monster's colours.
        void MonsterLanded(HazardState hazard)
        {
            if (!hazard.Harmful || IsMonsterPool(hazard.Shape) || hazard.MonsterShape + 1 == Hazard.DeathFlash) return;
            var look = LookOf(hazard);
            MonsterBlow((MonsterAttackShape)hazard.MonsterShape, hazard.Shape, new Vector3(hazard.Position.X, hazard.Position.Y, 0), hazard.Direction, hazard.Size, hazard.Width,
                look.Ramp, look.Tint, look.Look);
        }

        // The blow itself and its sound: the brightest and shortest part, then what thins out (MonsterFx.Land).
        void MonsterBlow(MonsterAttackShape shape, HazardShape area, Vector3 at, Vec2 facing, float reach, float width, FxRamp ramp, uint tint, MobLook kind = MobLook.Plain)
        {
            bool line = area == HazardShape.Line, cone = area == HazardShape.Cone;
            MonsterBlowsShown++;
            AddMobFx(MobFx.Land, at, new MobContext
            {
                Shape = shape,
                Area = area,
                Ramp = ramp,
                Tint = tint,
                Dir = facing.Angle,
                Size = (reach + World.PlayerRadius) * Ppu,
                Width = cone ? width : width * Ppu,
                Look = kind,
            });
            // The kind's own sound where it has one (spores puff, crystal rings, a sting hisses, magma sizzles, water
            // splashes); else the shape's.
            var sound = shape switch
            {
                MonsterAttackShape.Explode => Sound.Boom,
                MonsterAttackShape.Beam => Sound.Zap,
                MonsterAttackShape.Thrust when kind == MobLook.Sand => Sound.Sting,
                MonsterAttackShape.Lob or MonsterAttackShape.Sweep when kind == MobLook.Spore => Sound.Puff,
                MonsterAttackShape.Leap or MonsterAttackShape.Thrust when kind == MobLook.Ember || kind == MobLook.Hex => Sound.Sizzle,
                _ when kind == MobLook.Water => Sound.Splash,
                MonsterAttackShape.Burrow or MonsterAttackShape.Ambush => Sound.Burrow,
                MonsterAttackShape.Lob when kind == MobLook.Sand => Sound.Burrow,
                _ when kind == MobLook.Crystal => Sound.Chime,
                MonsterAttackShape.Leap => Sound.Plop,
                MonsterAttackShape.Strike or MonsterAttackShape.Sweep => Sound.Swing,
                MonsterAttackShape.Thrust => Sound.Heavy,
                MonsterAttackShape.Pull => Sound.Implode,
                MonsterAttackShape.Push => Sound.Splash,
                MonsterAttackShape.Mine or MonsterAttackShape.Explode or MonsterAttackShape.Delayed => Sound.Boom,
                MonsterAttackShape.Nova or MonsterAttackShape.Lob when ramp == FxRamp.Cold => Sound.Shatter,
                _ => Sound.Thump,
            };
            Hear(sound, at, line || cone ? 0.6f : 0.8f);
        }

        // A monster's shot in its own look: known for its monster's by coming out beside one that has just fired.
        bool DrawMonsterShot(ProjectileView projectile, bool isNew)
        {
            if (!Feel.MonsterFx) return false;
            if (!_mobShots.TryGetValue(projectile.Id, out var shot))
            {
                if (!isNew) return false;
                Body from = null;
                float best = 1.8f;
                foreach (var body in _others.Values)
                {
                    if (body.Kind != BodyKind.Monster || body.MobAttack == null || body.MobAttack.Tell != Tell.Barrage) continue;
                    float distance = Vector2.Distance(body.Position, new Vector2(projectile.Position.X, projectile.Position.Y));
                    if (distance >= best) continue;
                    best = distance;
                    from = body;
                }
                if (from == null) return false;
                shot = new MobShot { Shape = from.MobAttack.Shape, Ramp = from.MobRamp, Tint = from.MobTint, Seed = ++_fxSeed & 0xFFFF, Dir = projectile.Velocity.Angle };
                _mobShots[projectile.Id] = shot;
            }
            bool still = projectile.Velocity.LengthSquared < 0.25f;
            if (!still) shot.Dir = projectile.Velocity.Angle;
            _fxStamps.Clear();
            MonsterFx.Stamps(MobFx.Shot, new MobContext { Shape = shot.Shape, Ramp = shot.Ramp, Tint = shot.Tint, Dir = shot.Dir, Seed = shot.Seed, Armed = still }, Time.time, _fxStamps);
            EmitFx(new Vector3(projectile.Position.X, projectile.Position.Y, 0), 1, null);
            _mobShotsDrawn++;
            return true;
        }

        void MonsterShotGone(int id) => _mobShots.Remove(id);

        // What the host says a monster's attack or trait did (Guarded, Tether, MonsterFx).
        void ShowMonsterEvent(SimEvent e)
        {
            var body = Find(e.Source);
            var ramp = body != null && body.Kind == BodyKind.Monster ? body.MobRamp : FxRamp.Plain;
            uint tint = body != null && body.Kind == BodyKind.Monster ? body.MobTint : 0x1D8D8D8u;
            var at = new Vector3(e.Position.X, e.Position.Y, 0);
            switch (e.Type)
            {
                // A pack's alarm: a mark over each head in turn, and one call for the pack (not one each).
                case SimEventType.Alerted:
                    if (body == null) break;
                    _floating.Add(new Floating { Position = body.Position + new Vector3(0, body.Diameter * 0.6f + 0.35f, 0), Text = "!", Color = AlertColor, Big = true });
                    AlertsShown++;
                    if (Time.time - _alertHeard > 0.6f)
                    {
                        _alertHeard = Time.time;
                        Hear(Sound.Warning, body.Position, 0.45f);
                    }
                    break;
                case SimEventType.Guarded:
                    if (body == null) break;
                    // Toward whoever struck it: the nearest player, as near as the screen knows.
                    var toward = _self.Position - body.Position;
                    AddMobFx(MobFx.Guard, body.Position - new Vector3(0, body.Feet, 0), new MobContext { Dir = Mathf.Atan2(toward.y, toward.x) }, body);
                    Hear(Sound.Clang, body.Position, 0.7f);
                    break;
                case SimEventType.Tether:
                    if (e.Amount > 0)
                    {
                        _tethers[e.Source] = e.Target;
                        if (body != null) Hear(Sound.Mend, body.Position, 0.7f);
                    }
                    else _tethers.Remove(e.Source);
                    break;
                case SimEventType.MonsterFx:
                    switch (e.Amount)
                    {
                        case World.MonsterFxBounce:
                            AddMobFx(MobFx.Bounce, at, new MobContext { Ramp = ramp, Tint = tint });
                            Hear(Sound.Ricochet, at, 0.7f);
                            break;
                        case World.MonsterFxTurn:
                            AddMobFx(MobFx.Turn, at, new MobContext { Ramp = ramp, Tint = tint });
                            break;
                        case World.MonsterFxBlink:
                            AddMobFx(MobFx.BlinkOut, at - new Vector3(0, body != null ? body.Feet : 0.3f, 0), new MobContext { Ramp = ramp, Tint = tint });
                            Hear(Sound.Blink, at, 0.8f);
                            break;
                        case World.MonsterFxSplit:
                            AddMobFx(MobFx.Split, at, new MobContext { Ramp = ramp, Tint = tint, Dir = body != null ? Mathf.Atan2(body.Faced.Y, body.Faced.X) : 0 });
                            Hear(Sound.Plop, at, 0.8f);
                            break;
                        case World.MonsterFxStagger:
                            AddMobFx(MobFx.Stagger, at - new Vector3(0, body != null ? body.Feet : 0.3f, 0),
                                new MobContext { Ramp = ramp, Tint = tint, Dir = body != null ? Mathf.Atan2(body.Faced.Y, body.Faced.X) : 0 });
                            Hear(Sound.Daze, at);
                            if (Vector3.Distance(at, _self.Position) < ShakeRange) _shake = Mathf.Max(_shake, 0.08f);
                            break;
                        // B1c: a big body's charge smashed a stalagmite or a pit prop (the tile change takes the prop away).
                        case World.MonsterFxSmash:
                            AddMobFx(MobFx.Deathburst, at, new MobContext { Ramp = FxRamp.Plain, Tint = 0x19E8060u });
                            Hear(Sound.Shatter, at, 0.9f);
                            if (Vector3.Distance(at, _self.Position) < ShakeRange) _shake = Mathf.Max(_shake, 0.12f);
                            break;
                        case World.MonsterFxEmpower:
                            AddMobFx(MobFx.Rally, at - new Vector3(0, body != null ? body.Feet : 0.3f, 0), new MobContext { Size = 2.5f * Ppu });
                            Hear(Sound.Buff, at, 0.7f);
                            break;
                        case World.MonsterFxDeathburst:
                            AddMobFx(MobFx.Deathburst, at, new MobContext { Ramp = ramp == FxRamp.Plain ? FxRamp.Cold : ramp, Tint = tint });
                            Hear(Sound.Shatter, at, 0.8f);
                            break;
                        case World.MonsterFxMine:
                            AddMobFx(MobFx.Trip, at, new MobContext { Size = 1.2f * Ppu });
                            Hear(Sound.TrapSnap, at);
                            break;
                        case World.MonsterFxVanish:
                            AddMobFx(MobFx.Vanish, at - new Vector3(0, body != null ? body.Feet : 0.3f, 0), new MobContext { Ramp = body != null ? ramp : FxRamp.Violet, Tint = tint });
                            Hear(Sound.Vanish, at, 0.8f);
                            break;
                        case World.MonsterFxQuench:
                            AddMobFx(MobFx.Quench, at, new MobContext { Ramp = FxRamp.Cold });
                            Hear(Sound.Sizzle, at, 0.8f);
                            break;
                        case World.MonsterFxSurface:
                            AddMobFx(MobFx.Surface, at - new Vector3(0, body != null ? body.Feet : 0.3f, 0), new MobContext { Ramp = ramp, Tint = tint });
                            Hear(Sound.Burrow, at, 0.8f);
                            break;
                        case World.MonsterFxDive:
                            AddMobFx(MobFx.Dive, at - new Vector3(0, body != null ? body.Feet : 0.3f, 0), new MobContext { Ramp = ramp, Tint = tint });
                            Hear(Sound.Burrow, at, 0.5f);
                            break;
                        case World.MonsterFxCall:
                            AddMobFx(MobFx.Rally, at - new Vector3(0, body != null ? body.Feet : 0.3f, 0), new MobContext { Size = 3f * Ppu });
                            Hear(Sound.MobCall, at, 0.9f);
                            break;
                        case World.MonsterFxExplode:
                        {
                            // The bomber leaves the view with its blast, so the host says where; its reach is its
                            // attack's (Target is the attack's place in its sheet) or, unseen, a bat's.
                            var bomb = body != null && body.Kind == BodyKind.Monster ? body.MobAttack : null;
                            float reach = bomb != null && bomb.Shape == MonsterAttackShape.Explode ? bomb.Radius : 1.5f;
                            MonsterBlow(MonsterAttackShape.Explode, HazardShape.Circle, at - new Vector3(0, body != null ? body.Feet : 0.3f, 0), new Vec2(1, 0), reach, 0,
                                body != null ? ramp : FxRamp.Fire, tint, body != null ? body.MobKind : MobLook.Ember);
                            if (Vector3.Distance(at, _self.Position) < ShakeRange) _shake = Mathf.Max(_shake, 0.12f);
                            ExplosionsShown++;
                            break;
                        }
                    }
                    break;
            }
        }

        // Every piece of the monsters' effects for this frame, on the pool the skills' effects use (so the two share
        // the cap on pieces): first what was gathered while the bodies and marks were drawn, then the one-offs.
        void DrawMonsterFx(float deltaTime)
        {
            int before = _fxUsed;
            foreach (var draw in _mobDraws)
            {
                _fxStamps.Clear();
                MonsterFx.Stamps(draw.Fx, draw.Context, draw.Time, _fxStamps);
                EmitFx(draw.At, 1, null);
            }
            _mobDraws.Clear();
            for (int i = _mobFx.Count - 1; i >= 0; i--)
            {
                var fx = _mobFx[i];
                fx.Age += deltaTime;
                if (Feel.SkillFxHold >= 0) fx.Age = Mathf.Min(fx.Age, Feel.SkillFxHold);
                if (fx.Follow != null && fx.Follow.Root != null) fx.At = fx.Follow.Position - new Vector3(0, fx.Follow.Feet, 0);
                _fxStamps.Clear();
                if (!Feel.MonsterFx || !MonsterFx.Stamps(fx.Fx, fx.Context, fx.Age, _fxStamps))
                {
                    _mobFx.RemoveAt(i);
                    continue;
                }
                EmitFx(fx.At, 1, null);
            }
            // Held where they stand (HitEffect.Root): a ring of ice at the player's own feet.
            if (Feel.MonsterFx && (_client.SelfStatus & StatusBits.Rooted) != 0)
            {
                _fxStamps.Clear();
                _fxStamps.Add(new FxStamp { Shape = FxShape.Broken, Ramp = FxRamp.Cold, Size = 16, Frame = SkillFxArt.BrokenFrame(2, 6, (int)(Time.time * 4)), Alpha = 1, Ground = true });
                for (int i = 0; i < 4; i++)
                    _fxStamps.Add(new FxStamp { Shape = FxShape.Shard, Ramp = FxRamp.Cold, Size = 6, X = Mathf.Cos(i * Mathf.PI / 2 + 0.8f) * 6, Y = Mathf.Sin(i * Mathf.PI / 2 + 0.8f) * 4, Angle = 90, Alpha = 1 });
                EmitFx(_self.Position - new Vector3(0, _self.Feet, 0), 1, null);
            }
            // Poisoned (HitEffect.Venom): green drops off the player.
            if (Feel.MonsterFx && (_client.SelfStatus & StatusBits.Poisoned) != 0)
            {
                _fxStamps.Clear();
                MonsterFx.Stamps(MobFx.Poisoned, new MobContext(), Time.time, _fxStamps);
                EmitFx(_self.Position - new Vector3(0, _self.Feet, 0), 1, null);
            }
            MonsterMoundsShown = _mounds;
            MonsterLinksShown = _links;
            EliteGuardLinksShown = _guardLinks;
            _mounds = 0;
            _links = 0;
            _guardLinks = 0;
            _mobPieces = _fxUsed - before;
            _mobShotsShown = _mobShotsDrawn;
            _mobShotsDrawn = 0;
        }

        // For the playtests ("sizes", Docs/Playtest.md): how a monster is drawn now: its picture's width and height in
        // pixels, its health bar's width and its feet below its middle in tiles, and its body's scale (1 for everything
        // but a split's small one and one under the ground). Null for none on the screen.
        public (int Width, int Height, float Bar, float Feet, float Scale)? MonsterDrawn(int id)
        {
            if (!_others.TryGetValue(id, out var body) || body.Kind != BodyKind.Monster || body.Shape.sprite == null) return null;
            var rect = body.Shape.sprite.rect;
            return ((int)rect.width, (int)rect.height, MonsterLook.BarWidth(body.Diameter), body.Feet, body.Root.localScale.x);
        }

        // ---- Field elites (Docs/WorldRework.md 들판 정예, World.Elites.cs) ----

        // Name tags drawn in the last repaint, and the widest of them in points (a phone held upright must hold it).
        public int EliteTagsShown { get; private set; }
        public float EliteTagWidest { get; private set; }
        // The lines drawn last frame from a guarding leader to the minions it shields (RiftAffix.Guard).
        public int EliteGuardLinksShown { get; private set; }
        int _guardLinks;

        // After an elite is placed: the warning sign over it while it winds up its big attack (as a boss's strongest),
        // and a guarding leader's lines to the minions it shields.
        void EliteExtras(Body body, EntityView view, MonsterAttack winding, Vector3 feet)
        {
            if (view.Elite == EliteRank.None) return;
            ShowWarningSign(body, winding != null && World.IsBigAttack(view.Elite, view.Monster, winding));
            if (view.Elite != EliteRank.Leader || (view.Affixes & RiftAffix.Guard) == 0 || body.MobBuried) return;
            foreach (var other in _others.Values)
            {
                if (other == body || other.Kind != BodyKind.Monster || other.Elite != EliteRank.None || other.MobBuried) continue;
                if (Vector2.Distance(other.Position, body.Position) > World.GuardReach) continue;
                var line = other.Position - new Vector3(0, other.Feet, 0) - feet;
                QueueMob(MobFx.Link, feet, new MobContext { Dir = Mathf.Atan2(line.y, line.x), Size = line.magnitude * Ppu }, Time.time);
                _guardLinks++;
            }
        }

        // Each elite's name over its head, gold for a leader and blue for a champion, its affixes under it; kept inside
        // the screen's width (a phone held upright, Docs/Design.md 기술 원칙 7). Call from DrawGui. Champions of one name
        // standing together share one tag ("… ×3") rather than piling unreadable copies on each other, and a tag that would
        // still cover another is lifted above it (EliteTagsShown counts the elites whose name is on the screen).
        void DrawEliteTags()
        {
            bool repaint = Event.current.type == EventType.Repaint;
            _tagGroups.Clear();
            foreach (var body in _others.Values)
            {
                if (body.Kind != BodyKind.Monster || body.Elite == EliteRank.None || string.IsNullOrEmpty(body.EliteName)) continue;
                float scale = body.Root.localScale.y;
                if (scale <= 0) continue;
                var head = body.Position + new Vector3(0, scale * (body.Top + 0.15f) + 0.3f, 0);
                Vector3 screen = _camera.WorldToScreenPoint(head);
                if (screen.z < 0) continue;
                Vector2 point = Ui.FromScreen(screen);
                TagGroup group = null;
                foreach (var other in _tagGroups)
                    if (other.Name == body.EliteName && Vector2.Distance(other.Point / other.Count, point) < TagGatherPoints) { group = other; break; }
                if (group == null)
                {
                    group = new TagGroup { Name = body.EliteName, Line = body.EliteLine, Leader = body.Elite == EliteRank.Leader };
                    _tagGroups.Add(group);
                }
                group.Point += point;
                group.Count++;
            }
            int shown = 0;
            float widest = 0;
            _tagRects.Clear();
            foreach (var group in _tagGroups)
            {
                var at = group.Point / group.Count;
                string name = group.Count > 1 ? group.Name + " ×" + group.Count : group.Name;
                bool line = !string.IsNullOrEmpty(group.Line);
                float width = Mathf.Max(TagWidth(name), line ? TagWidth(group.Line) : 0);
                // The block of the name (and the affix line under it), lifted until it covers no tag already drawn.
                var block = new Rect(at.x - width / 2, at.y - TagNameRise - 12, width, TagNameRise + 24);
                for (int tries = 0; tries < 4 && _tagRects.Exists(r => r.Overlaps(block)); tries++) block.y -= block.height + 2;
                _tagRects.Add(block);
                widest = Mathf.Max(widest, EliteLabel(new Vector2(at.x, block.y + 12), name, group.Leader ? EliteGold : ChampionBlue));
                if (line) widest = Mathf.Max(widest, EliteLabel(new Vector2(at.x, block.y + 12 + TagNameRise), group.Line, EliteLineColor));
                shown += group.Count;
            }
            if (!repaint) return;
            EliteTagsShown = shown;
            EliteTagWidest = widest;
        }

        sealed class TagGroup
        {
            public string Name, Line;
            public bool Leader;
            public Vector2 Point;
            public int Count;
        }
        readonly List<TagGroup> _tagGroups = new List<TagGroup>();
        readonly List<Rect> _tagRects = new List<Rect>();
        // Points between the heads of same-named champions that share a tag, and from a name down to its affix line.
        const float TagGatherPoints = 70f;
        const float TagNameRise = 20f;

        float TagWidth(string text) => _labelStyle.CalcSize(new GUIContent(text)).x + 8;

        // A label centred over the point (in Ui points) but moved in to stay whole on the screen; its width in points.
        float EliteLabel(Vector2 point, string text, Color color)
        {
            float width = TagWidth(text);
            float x = Mathf.Clamp(point.x - width / 2, 2, Mathf.Max(2, Ui.Width - width - 2));
            Shadowed(new Rect(x, point.y - 12, width, 24), text, color, _labelStyle);
            return width;
        }
    }
}
