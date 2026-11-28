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
    // are drawn the same: the shape's own picture at the real reach as a boss's warning is (a two-pixel rim from the
    // start, stripes over what has filled, blinking at the last; E5 of Docs/EffectsRework.md) but fainter inside, and
    // MonsterFx's pieces: the monster's own wind-up cue, the blow and what thins out. Nothing here decides anything
    // about the game. Feel.MonsterFx off draws the marks the way a boss's are and nothing else.
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
        // Where the thrower of a mark away from it stood when the mark was first seen.
        readonly Dictionary<int, Vector3> _markThrowers = new Dictionary<int, Vector3>();
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
        // Streams of falling sand drawn over the sand cave's buried, and icicles shown falling, since the game began (B2b).
        public int SandFallsShown { get; private set; }
        // B3b: ripples drawn over the lake's ones under the water (a frame's worth, last frame), shots seen glancing off
        // crystal, shots a shell turned back, prism shots breaking, and splashes in the shallows, since the game began.
        public int RipplesShown { get; private set; }
        int _ripples;
        public int GlancesShown { get; private set; }
        public int ReflectsShown { get; private set; }
        public int RefractsShown { get; private set; }
        public int SplashesShown { get; private set; }
        public int IciclesShown { get; private set; }
        // Mounds drawn last frame for monsters under the ground, and the lines between those that stand in a line.
        public int MonsterMoundsShown { get; private set; }
        public int MonsterLinksShown { get; private set; }
        int _mounds;
        int _links;
        // E5: bodies drawn rushing last frame (dust and lines of speed), and marks drawn as a picture of their shape.
        public int MonsterRushesShown { get; private set; }
        public int MonsterWarnsShown { get; private set; }
        int _rushes;
        int _mobWarns;

        const float Ppu = Sprites.PixelsPerUnit;

        static bool IsMonsterPool(HazardShape shape) =>
            shape == HazardShape.Fire || shape == HazardShape.Ice || shape == HazardShape.Spore || shape == HazardShape.Sand || shape == HazardShape.Balm;

        // The host's clock in seconds: marks made here run on it, so they stop when the host does (a playtest's picture).
        double MobNow => _client.HostTick / World.TicksPerSecond;

        // A field monster's wind-up sound (2026-11-28, the owner: too loud; was 0.8, now -6 dB). A boss's wind-up has its
        // own (WorldView.HearWarning, off for now); a bomber's fuse keeps this too, as it is a warning of the same kind.
        public const float MonsterTellVolume = 0.4f;

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

        // What a dormant one breaks out of when it wakes: ice, a mimic's crystals and the golem's geode, the turtle's moss
        // and water, else stone and sand.
        static FxRamp WakeRamp(string id) => id switch
        {
            "frost_mammoth" or "crystal_mimic" => FxRamp.Cold,
            "geode_golem" or "gate_warden" => FxRamp.Violet,
            "forge_keeper" => FxRamp.Fire,
            "moss_turtle" => FxRamp.Spirit,
            _ => FxRamp.Dust,
        };

        // A splash where someone wades the shallows (B3b, the footsteps of WorldView.cs).
        void Splash(Vector3 feet)
        {
            AddMobFx(MobFx.Splash, feet, new MobContext());
            SplashesShown++;
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
                // In the lake it is under the water: rings and bubbles, not earth (World.UnderWater, B3b).
                bool water = World.UnderWater(_client.Map, _client.Biomes, new Vec2(body.Position.x, body.Position.y), true);
                QueueMob(water ? MobFx.Ripple : MobFx.Mound, feet,
                    new MobContext { Ramp = body.MobRamp, Tint = body.MobTint, Dir = body.MobDir.Angle, Armed = moving, Seed = view.Id & 0xFFFF, Look = body.MobKind }, Time.time);
                _mounds++;
                if (water) _ripples++;
                // In the sand cave sand pours from the ceiling onto it (World.UnderFallingSand, B2b).
                if (World.UnderFallingSand(_client.Biomes, new Vec2(body.Position.x, body.Position.y), true))
                {
                    QueueMob(MobFx.SandFall, feet, new MobContext { Seed = view.Id & 0xFFFF }, Time.time);
                    SandFallsShown++;
                }
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
            // A charge throws up dust behind its feet and draws lines of speed off its back (E5).
            if (state == EnemyState.Charge && body.MobAttack != null && body.MobAttack.Shape == MonsterAttackShape.Charge && !buried)
            {
                QueueMob(MobFx.Rush, feet, new MobContext { Ramp = body.MobRamp, Tint = body.MobTint, Dir = view.Facing.Angle, Look = body.MobKind, Seed = view.Id & 0xFFFF,
                    Size = view.Monster.Radius * Ppu }, Time.time);
                _rushes++;
            }
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
                Hear(attack.Shape == MonsterAttackShape.Explode ? Sound.Fuse : TellSound(attack.Tell), body.Position, MonsterTellVolume);
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
                    // Where a fan's shots will go: a fan keeps the way it faced when it began (World.Unleash).
                    if (doing.Shape == MonsterAttackShape.Spread && Feel.MonsterFx)
                    {
                        LayAim(view.Position, body.MobDir, view.Monster.Radius, doing.Count > 1 ? doing.Angle : 0, Mathf.Max(1, doing.Count),
                            Mathf.Clamp01(elapsed / Mathf.Max(0.01f, body.MobTelegraph)), Mathf.Min(MobAimReach, doing.Length > 0 ? doing.Length : MobAimReach));
                    }
                }
                // A single shot (a volley's, a bouncing or a returning one) is aimed as it leaves, at whoever the host
                // says it is for (EntityView.AimTarget, 0.113.0): its line goes to where that player is drawn now, so it
                // follows them through the wind-up and between a volley's shots, as the shot itself will.
                if (view.AimTarget != 0 && Feel.MonsterFx && AimedAt(view.AimTarget) is Vector3 mark)
                {
                    var way = new Vec2(mark.x - view.Position.X, mark.y - view.Position.Y);
                    if (way.LengthSquared > 1e-4f)
                    {
                        LayAim(view.Position, way.Normalized, view.Monster.Radius, 0, 1, winding ? Mathf.Clamp01(elapsed / Mathf.Max(0.01f, body.MobTelegraph)) : 1,
                            Mathf.Min(MobAimReach, doing.Length > 0 ? doing.Length : MobAimReach));
                        TargetAimDegrees = Mathf.Repeat(way.Angle * Mathf.Rad2Deg, 360);
                        _targetAims++;
                    }
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

        // Where the player a shot is for stands on this screen: oneself, or another as drawn. Null when not in sight.
        Vector3? AimedAt(int id) => id == _client.PlayerId ? _self.Position : _others.TryGetValue(id, out var other) && other.Kind == BodyKind.Player ? other.Position : (Vector3?)null;

        // For the playtest: lines drawn to a shot's target last frame, and the way (degrees) the last one went.
        int _targetAims;
        public int TargetAimCount { get; private set; }
        public float TargetAimDegrees { get; private set; } = -1;

        // A monster's aim lines reach less far than a boss's: several may aim at once.
        const float MobAimReach = 5;

        Mark MakeMonsterMark(HazardState hazard)
        {
            var root = new GameObject("Telegraph").transform;
            root.SetParent(_root.transform, false);
            root.localPosition = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            // The disc of what a blow left, for the plain look; a warning's picture is made when it is first drawn.
            var fill = Renderer(root, "Fill", Shapes.Circle, Color.clear);
            fill.sortingOrder = DarkOrder + 11;
            return new Mark { Root = root, Edge = fill, Fill = fill, State = hazard };
        }

        // Whose colours a monster's mark away from it is drawn in: the nearest monster doing that kind of attack.
        (FxRamp Ramp, uint Tint, MonsterAttack Attack, MobLook Look) LookOf(HazardState hazard)
        {
            if (_markLooks.TryGetValue(hazard.Id, out var look)) return look;
            // An icicle of the ice halls (Hazard.Icicle, B2b) is the cold's own, no monster's.
            if (hazard.MonsterShape + 1 == Hazard.Icicle) return (FxRamp.Cold, 0x1DCEFFAu, null, MobLook.Frost);
            // A steam vent of the lava fields (Hazard.Steam, B4b) is the fire's own.
            if (IsSteam(hazard)) return (FxRamp.Fire, 0x1FFB060u, null, MobLook.Ember);
            look = (FxRamp.Plain, 0x1D8D8D8u, null, MobLook.Plain);
            float best = 14;
            Body thrower = null;
            foreach (var body in _others.Values)
            {
                if (body.Kind != BodyKind.Monster || body.MobAttack == null || (int)body.MobAttack.Shape != hazard.MonsterShape) continue;
                float distance = Vector2.Distance(body.Position, new Vector2(hazard.Position.X, hazard.Position.Y));
                if (distance >= best) continue;
                best = distance;
                look = (body.MobRamp, body.MobTint, body.MobAttack, body.MobKind);
                thrower = body;
            }
            if (_markLooks.Count > 96)
            {
                _markLooks.Clear();
                _markThrowers.Clear();
            }
            _markLooks[hazard.Id] = look;
            if (thrower != null) _markThrowers[hazard.Id] = thrower.Position;
            return look;
        }

        // Pools that lie in a row (F2): what a blow along a line left (World.LeaveMonsterPool lays a few circles its
        // width along it) and a charge's trail. The screen is not told they belong together, so it finds them: the
        // same kind and size, laid one after another within a second, each within reach of the last and all on one
        // line. The first draws one band from the first's middle to the last's; the rest draw nothing. The band is
        // the circles' hull, so it never shows less than what hurts.
        readonly Dictionary<int, (Vector3 Mid, float Dir, float Length, float Fade, bool Lead)> _poolBands = new Dictionary<int, (Vector3, float, float, float, bool)>();
        readonly List<HazardView> _poolRow = new List<HazardView>();
        int _poolBandsDrawn;
        // For the playtest: bands drawn this frame (and the last whole frame's).
        public int PoolBandsShown { get; private set; }
        public int PoolBands => _poolBandsDrawn;

        void GroupPools(IEnumerable<HazardView> views)
        {
            _poolBandsDrawn = PoolBandsShown;
            PoolBandsShown = 0;
            _poolBands.Clear();
            _poolRow.Clear();
            if (!Feel.MonsterFx) return;
            foreach (var view in views)
                if (view.State.FromMonster && IsMonsterPool(view.State.Shape) && view.State.Shape != HazardShape.Balm) _poolRow.Add(view);
            if (_poolRow.Count < 2) return;
            _poolRow.Sort((a, b) => a.State.Id.CompareTo(b.State.Id));
            for (int from = 0; from < _poolRow.Count;)
            {
                int to = from;
                while (to + 1 < _poolRow.Count && InRow(_poolRow[from].State, _poolRow[to].State, _poolRow[to + 1].State, to > from)) to++;
                if (to > from)
                {
                    var first = _poolRow[from];
                    var last = _poolRow[to];
                    var span = last.State.Position - first.State.Position;
                    var mid = first.State.Position + span * 0.5f;
                    // It comes in with its first and goes out with its last.
                    float fade = Mathf.Clamp01(Mathf.Min(first.Progress * first.State.Duration / World.TicksPerSecond / 0.2f, (1 - last.Progress) * last.State.Duration / World.TicksPerSecond / 0.35f));
                    for (int i = from; i <= to; i++)
                        _poolBands[_poolRow[i].State.Id] = (new Vector3(mid.X, mid.Y, 0), span.Angle, span.Length, fade, i == from);
                }
                from = to + 1;
            }
        }

        static bool InRow(HazardState first, HazardState last, HazardState next, bool started)
        {
            if (next.Shape != last.Shape || Mathf.Abs(next.Size - last.Size) > 0.01f) return false;
            if (Mathf.Abs(next.TicksLeft - last.TicksLeft) > World.TicksPerSecond) return false;
            var step = next.Position - last.Position;
            float far = step.Length;
            if (far < last.Size * 0.5f || far > last.Size * 3.2f) return false;
            if (!started) return true;
            var way = last.Position - first.Position;
            return Vec2.Dot(way, step) > 0.96f * way.Length * far;
        }

        // One of a monster's marks this frame: the picture of its shape (HazardArt.Warn, fainter inside than a boss's)
        // and what goes with it as MonsterFx's pieces. What a blow left on the floor is all pieces.
        void DrawMonsterMark(Mark mark, HazardView view)
        {
            var hazard = view.State;
            mark.State = hazard;
            mark.Root.localPosition = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            var at = mark.Root.localPosition;
            float p = view.Progress;
            if (IsMonsterPool(hazard.Shape))
            {
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
                // Pools laid in a row (a blow along a line, a charge's trail) are one band, drawn by the first (F2).
                if (_poolBands.TryGetValue(hazard.Id, out var row))
                {
                    if (!row.Lead) return;
                    PoolBandsShown++;
                    QueueMob(MobFx.Pool, row.Mid, new MobContext { Area = hazard.Shape, Size = hazard.Size * Ppu, Width = row.Length * Ppu, Dir = row.Dir, Progress = row.Fade, Seed = hazard.Id & 0xFFFF }, Time.time);
                    return;
                }
                QueueMob(MobFx.Pool, at, new MobContext { Area = hazard.Shape, Size = hazard.Size * Ppu, Progress = fade, Seed = hazard.Id & 0xFFFF }, Time.time);
                return;
            }

            int shapeIndex = hazard.MonsterShape;
            bool flash = shapeIndex + 1 == Hazard.DeathFlash;
            // An icicle is drawn as a thing coming down: its shadow grows on the floor and it falls at the end.
            bool icicle = shapeIndex + 1 == Hazard.Icicle;
            if (icicle && !mark.Armed)
            {
                mark.Armed = true;
                IciclesShown++;
                Hear(Sound.MobArea, at, 0.5f);
            }
            // A steam vent hisses under a slam's ring at its own reach (B4b, WorldView.LavaVoid.cs).
            bool steam = IsSteam(hazard);
            if (steam) SteamArmed(mark, at);
            var shape = flash || steam ? MonsterAttackShape.Slam : icicle ? MonsterAttackShape.Lob : (MonsterAttackShape)shapeIndex;
            bool blink = shape == MonsterAttackShape.Blink;

            var look = LookOf(hazard);
            bool line = hazard.Shape == HazardShape.Line, cone = hazard.Shape == HazardShape.Cone;
            // A trap is armed once its attack's telegraph is over; the rest of its time it waits.
            float armsAt = look.Attack != null ? look.Attack.Telegraph / (look.Attack.Telegraph + look.Attack.Duration) : 0.15f;
            bool armed = hazard.Shape == HazardShape.Mine && p >= armsAt;
            if (armed && !mark.Armed)
            {
                mark.Armed = true;
                Hear(Sound.TrapSet, at, 0.7f);
            }
            var context = new MobContext
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
            };
            // A thrown thing is seen on its way over from its thrower.
            if (shape == MonsterAttackShape.Lob && !icicle && _markThrowers.TryGetValue(hazard.Id, out var thrower))
            {
                context.Thrown = true;
                context.FromX = (thrower.x - at.x) * Ppu;
                context.FromY = (thrower.y - at.y) * Ppu;
            }

            // The shape's picture: made for this size and angle, and again if the mark under this id changes shape
            // (a monster's next attack takes the same id).
            long key = (long)hazard.Shape << 56 | (long)(Mathf.RoundToInt(context.Size) & 0xFFFF) << 36 | (long)(Mathf.RoundToInt(context.Width * 4) & 0xFFFFF) << 16
                | (long)(Mathf.RoundToInt(context.Dir * Mathf.Rad2Deg) & 0xFFFF);
            if (mark.Warn == null || mark.WarnKey != key)
            {
                Free(mark.Warn);
                mark.Warn = MakeLayer(mark.Root, "Warn", MonsterFx.Field(context), DarkOrder + 10);
                mark.WarnKey = key;
            }
            var layer = mark.Warn;
            _mobWarns++;
            int step = MonsterFx.WarnStep(context, armsAt);
            // The last of its time it blinks whole (with motion reduced it stays at its brighter half).
            bool lit = MonsterFx.WarnBlinks(context) && (Settings.ReduceMotion || (int)(Time.time * 25) % 2 == 0);
            // An armed trap's rim pulses slowly.
            bool pulse = armed && !Settings.ReduceMotion && Mathf.Sin(Time.time * 6) > 0;
            int arrows = MonsterFx.WarnArrows(context, Time.time, Settings.ReduceMotion);
            int shown = step | (lit ? 256 : 0) | (pulse ? 512 : 0) | (arrows + 1) << 10 | (Feel.MonsterFx ? 1 << 20 : 0);
            if (shown != layer.Shown)
            {
                layer.Shown = shown;
                HazardArt.Warn(layer.Field, MonsterFx.Tone(context), step / (float)HazardArt.FillSteps, lit, pulse, layer.Buffer, true, Feel.MonsterFx, arrows);
                Upload(layer);
            }
            // A trap not yet armed is drawn faintly, rim and all (it does nothing yet if stepped on).
            layer.Renderer.color = hazard.Shape == HazardShape.Mine && !armed ? new Color(1, 1, 1, 0.5f) : Color.white;
            QueueMob(MobFx.Mark, at, context, Time.time);
        }

        // A monster's mark away from it has landed (the host's Impact): its blow, in its monster's colours.
        void MonsterLanded(HazardState hazard)
        {
            if (!hazard.Harmful || IsMonsterPool(hazard.Shape) || hazard.MonsterShape + 1 == Hazard.DeathFlash) return;
            // A vent's burst is its column of steam (B4b).
            if (IsSteam(hazard))
            {
                SteamBurst(hazard);
                return;
            }
            var look = LookOf(hazard);
            MonsterBlow(hazard.MonsterShape + 1 == Hazard.Icicle ? MonsterAttackShape.Lob : (MonsterAttackShape)hazard.MonsterShape, hazard.Shape, new Vector3(hazard.Position.X, hazard.Position.Y, 0), hazard.Direction, hazard.Size, hazard.Width,
                look.Ramp, look.Tint, look.Look);
        }

        // Shapes burning at once, bosses' and monsters' together: past this a monster's blow is its pieces alone.
        const int MostMobBlasts = 16;
        // For the playtest: shots drawn in a kind's own colour, and blows that landed as their warning's shape.
        public int MobOwnShots { get; private set; }
        public int MobShapeBlows { get; private set; }

        // The blow itself and its sound: the brightest and shortest part, then what thins out (MonsterFx.Land).
        void MonsterBlow(MonsterAttackShape shape, HazardShape area, Vector3 at, Vec2 facing, float reach, float width, FxRamp ramp, uint tint, MobLook kind = MobLook.Plain)
        {
            bool line = area == HazardShape.Line, cone = area == HazardShape.Cone;
            MonsterBlowsShown++;
            var context = new MobContext
            {
                Shape = shape,
                Area = area,
                Ramp = ramp,
                Tint = tint,
                Dir = facing.Angle,
                Size = (reach + World.PlayerRadius) * Ppu,
                Width = cone ? width : width * Ppu,
                Look = kind,
                Seed = ++_fxSeed & 0xFFFF,
            };
            // A wedge, a thrust, a beam or a ring lands as its warning's own shape burning in what the kind is made
            // of (F2): the same picture the warning was, fainter than a boss's, with no scar left.
            if (Feel.MonsterFx && MonsterFx.ShapeBlow(context) && _blasts.Count < MostMobBlasts)
            {
                var warned = context;
                if (line) warned.Width = (width + 2 * World.PlayerRadius) * Ppu;
                var layer = MakeLayer(_root.transform, "MobBlast", MonsterFx.Field(warned), 19000);
                layer.Renderer.transform.localPosition = at;
                _blasts.Add(new Blast { Layer = layer, Ramp = MonsterFx.BlowRamp(context), Seed = context.Seed, Strength = MonsterFx.BlowStrength });
                UpdateBlast(_blasts[_blasts.Count - 1], 0);
                MobShapeBlows++;
            }
            AddMobFx(MobFx.Land, at, context);
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
                // A burst of what it is made of sounds as that element's spell (2026-11-28); a plain one thumps.
                MonsterAttackShape.Nova when Recordings.SpellOf(kind) != SpellElement.None => Sound.Nova,
                MonsterAttackShape.Strike or MonsterAttackShape.Sweep => Sound.Swing,
                MonsterAttackShape.Thrust => Sound.Heavy,
                MonsterAttackShape.Pull => Sound.Implode,
                MonsterAttackShape.Push => Sound.Splash,
                MonsterAttackShape.Mine or MonsterAttackShape.Explode or MonsterAttackShape.Delayed => Sound.Boom,
                MonsterAttackShape.Nova or MonsterAttackShape.Lob when ramp == FxRamp.Cold => Sound.Shatter,
                _ => Sound.Thump,
            };
            Hear(sound, at, line || cone ? 0.6f : 0.8f, sound == Sound.Nova ? Recordings.NovaName(Recordings.SpellOf(kind)) : null);
        }

        // A monster's shot, known for its monster's by coming out beside one that has just fired: the enemy's shot
        // (WorldView.Shots.cs, E2) in what its monster is made of, turned anew when it bounces or comes back. It
        // ends in its element's ring and bits and lights the floor as every enemy's shot does.
        void DrawMonsterShot(ProjectileView projectile, bool isNew, SpriteRenderer renderer)
        {
            if (!Feel.MonsterFx) return;
            if (!_mobShots.TryGetValue(projectile.Id, out var shot))
            {
                if (!isNew) return;
                // The one that fired stands back along the way the shot flies: the screen may first see a shot some
                // way out from its monster (2026-11-27: a web client saw a fan's shots two tiles out and drew them plain).
                Body from = null;
                float best = 5;
                var here = new Vector2(projectile.Position.X, projectile.Position.Y);
                var way = new Vector2(projectile.Velocity.X, projectile.Velocity.Y);
                way = way.sqrMagnitude > 1e-6f ? way.normalized : Vector2.zero;
                foreach (var body in _others.Values)
                {
                    if (body.Kind != BodyKind.Monster || body.MobAttack == null || body.MobAttack.Tell != Tell.Barrage) continue;
                    var back = (Vector2)body.Position - here;
                    float along = -Vector2.Dot(back, way), aside = (back + way * along).magnitude;
                    float distance = back.magnitude;
                    // Beside it (1.8 tiles, as before), or behind the shot within a body's width of its line.
                    if (distance >= 1.8f && (along < 0 || aside > 0.9f)) continue;
                    if (distance >= best) continue;
                    best = distance;
                    from = body;
                }
                if (from == null) return;
                shot = new MobShot { Shape = from.MobAttack.Shape, Ramp = from.MobRamp, Tint = from.MobTint, Seed = ++_fxSeed & 0xFFFF, Dir = projectile.Velocity.Angle };
                _mobShots[projectile.Id] = shot;
                // It flashes where it left its monster, in what it is made of (F2): only when seen leaving.
                if (best < from.Diameter / 2 + 2.2f)
                {
                    uint own = MonsterFx.ShotTint(from.MobKind, from.MobRamp, from.MobTint);
                    Muzzle(from.Position, from.Diameter / 2, own != 0 ? FxRamp.Plain : ShotArt.RampOf(MonsterFx.ShotOf(from.MobKind, from.MobRamp)), own, projectile.Velocity.Angle * Mathf.Rad2Deg, 0, 1);
                }
                if (_enemyShots.TryGetValue(projectile.Id, out var made))
                {
                    made.Element = MonsterFx.ShotOf(from.MobKind, from.MobRamp);
                    made.Tint = MonsterFx.ShotTint(from.MobKind, from.MobRamp, from.MobTint);
                    if (made.Tint != 0) MobOwnShots++;
                    made.Frame = -1;
                    if (!made.Own)
                    {
                        // Beside a boss with a shot of its own: the monster's is still its own.
                        made.Own = true;
                        renderer.color = Color.white;
                        renderer.transform.localScale = Vector3.one;
                        renderer.transform.localRotation = Quaternion.identity;
                    }
                }
            }
            bool still = projectile.Velocity.LengthSquared < 0.25f;
            if (!still) shot.Dir = projectile.Velocity.Angle;
            if (_enemyShots.TryGetValue(projectile.Id, out var drawn))
            {
                int step = still ? -1 : ShotArt.Step(projectile.Velocity.Angle);
                if (step != drawn.Step)
                {
                    drawn.Step = step;
                    drawn.Frame = -1;
                }
            }
            if (still)
            {
                _fxStamps.Clear();
                MonsterFx.Stamps(MobFx.Shot, new MobContext { Shape = shot.Shape, Ramp = shot.Ramp, Tint = shot.Tint, Dir = shot.Dir, Seed = shot.Seed, Armed = true }, Time.time, _fxStamps);
                EmitFx(new Vector3(projectile.Position.X, projectile.Position.Y, 0), 1, null);
            }
            _mobShotsDrawn++;
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
                    // The abyss's plates giving way and the falls through them (B4b, WorldView.LavaVoid.cs).
                    if (LavaVoidEvent(e, at)) break;
                    switch (e.Amount)
                    {
                        case World.MonsterFxBounce:
                            AddMobFx(MobFx.Bounce, at, new MobContext { Ramp = ramp, Tint = tint });
                            Hear(Sound.Ricochet, at, 0.7f);
                            break;
                        // B3b: a shot off crystal (whoever fired it), a shell turning a shot back, a prism's shot breaking.
                        case World.MonsterFxGlance:
                            AddMobFx(MobFx.Glance, at, new MobContext { Ramp = FxRamp.Cold });
                            Hear(Sound.Ricochet, at, 0.8f);
                            GlancesShown++;
                            break;
                        case World.MonsterFxReflect:
                        {
                            var face = body != null ? body.Faced : new Vec2(1, 0);
                            AddMobFx(MobFx.Reflect, at, new MobContext { Dir = Mathf.Atan2(face.Y, face.X) });
                            Hear(Sound.Ricochet, at, 1f);
                            ReflectsShown++;
                            break;
                        }
                        case World.MonsterFxRefract:
                            AddMobFx(MobFx.Refract, at, new MobContext { Ramp = FxRamp.Violet });
                            Hear(Sound.Ricochet, at, 0.6f);
                            RefractsShown++;
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
                        // B2b: a charge slides on over the ice; a dormant mid-boss breaks out of its stone or its ice.
                        case World.MonsterFxSlide:
                            AddMobFx(MobFx.Slide, at - new Vector3(0, body != null ? body.Feet : 0.3f, 0),
                                new MobContext { Dir = body != null ? Mathf.Atan2(body.Faced.Y, body.Faced.X) : 0, Seed = e.Source & 0xFFFF });
                            Hear(Sound.Shatter, at, 0.45f);
                            break;
                        case World.MonsterFxWake:
                            AddMobFx(MobFx.Rouse, at - new Vector3(0, body != null ? body.Feet : 0.3f, 0),
                                new MobContext { Ramp = WakeRamp(body?.Id), Seed = e.Source & 0xFFFF });
                            Hear(Sound.Shatter, at, 1f);
                            if (Vector3.Distance(at, _self.Position) < ShakeRange) _shake = Mathf.Max(_shake, 0.18f);
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
            // The abyss's plates cracking under the players' feet (B4b).
            DrawPlateCracks();
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
            RipplesShown = _ripples;
            _ripples = 0;
            MonsterLinksShown = _links;
            EliteGuardLinksShown = _guardLinks;
            _mounds = 0;
            _links = 0;
            MonsterRushesShown = _rushes;
            MonsterWarnsShown = _mobWarns;
            _rushes = 0;
            _mobWarns = 0;
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
