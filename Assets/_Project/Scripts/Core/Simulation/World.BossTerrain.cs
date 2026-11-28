using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // ---- R2 (2026-11-28, Docs/BossRedesign.md 7.1 T3·T9, Docs/Bosses.md "지형·물체·분노"): a boss's ground (lava that
    // bites and slows and cools into rock, shallow water, cracks), what closes in from the arena's edge (edge column),
    // the things standing in the arena (BossProp: vents, pillars, veins) and its rage (BossRage). ----
    public sealed partial class World
    {
        // How often lava, a crack or a biting edge hurts whoever stands in it.
        public const float GroundSeconds = 0.5f;
        // Seconds the rock lava cools into lies there.
        public const float CooledSeconds = 6;
        // Seconds after an edge comes before it hurts (the roar, then time to walk in).
        public const float EdgeGraceSeconds = 3;
        // How big an island of cooled rock in a lava edge is.
        public const float IslandRadius = 1.3f;
        // How long a boss that charged into a pillar stands stunned.
        public const float StunSeconds = 2;
        // What a boss's ground or edge bites for every GroundSeconds, of the blow that laid it (the edge: of the boss's
        // roar). Less than venom's (World.VenomShare): lava lies long and across the way, and is to be walked round, not
        // to kill (2026-11-28 bot fights: at venom's share the bots fell in phase 3).
        public const float GroundShare = 0.07f;

        readonly List<BossProp> _props = new List<BossProp>();
        public IReadOnlyList<BossProp> Props => _props;

        // ---- The edge ----

        public static bool EdgeUp(Boss boss) => boss.Definition.Edge != null && boss.Phase >= boss.Definition.Edge.Phase && boss.State != BossState.Dormant;

        // How far from the middle the open floor reaches: the whole arena until its edge comes.
        public static float FloorRadius(Boss boss) => EdgeUp(boss) ? boss.ArenaRadius - boss.Definition.Edge.Width : boss.ArenaRadius;

        // Where the islands of cooled rock stand in a lava edge, evenly round from a quarter turn.
        public static IEnumerable<Vec2> Islands(Boss boss) =>
            EdgeUp(boss) ? IslandSpots(boss.Home, boss.ArenaRadius, boss.Definition.Edge) : Enumerable.Empty<Vec2>();

        // The same for the screen (WorldClient.EdgeOf).
        public static IEnumerable<Vec2> IslandSpots(Vec2 home, float arena, BossEdge edge)
        {
            if (edge == null || edge.Kind != EdgeKind.Lava) yield break;
            for (int i = 0; i < edge.Islands; i++)
                yield return home + Vec2.FromAngle(MathF.PI / 4 + i * 2 * MathF.PI / edge.Islands) * (arena - edge.Width / 2);
        }

        // Whether a spot is in the boss's edge (and not on one of its islands).
        public static bool InEdge(Boss boss, Vec2 at)
        {
            if (!EdgeUp(boss)) return false;
            float distance = Vec2.Distance(at, boss.Home);
            if (distance <= FloorRadius(boss) || distance > boss.ArenaRadius + ArenaMargin) return false;
            return !Islands(boss).Any(i => Vec2.Distance(i, at) <= IslandRadius);
        }

        // Whether a spot is on cooled rock of the boss's: a pool of it, or an island.
        public bool OnRock(int bossId, Vec2 at)
        {
            if (_hazards.Any(h => h.Shape == HazardShape.Rock && h.Source == bossId && h.Contains(at, 0))) return true;
            var boss = FindBoss(bossId);
            return boss != null && Islands(boss).Any(i => Vec2.Distance(i, at) <= IslandRadius);
        }

        // Whether a spot is somewhere the bot should not stand: a boss's edge that hurts or snares.
        public bool BadGround(Vec2 at) => _bosses.Any(b => !b.IsDead && InEdge(b, at) && b.Definition.Edge.Kind != EdgeKind.Water && EdgeBites(b));

        bool EdgeBites(Boss boss) => boss.EdgeTick >= 0 && Tick >= boss.EdgeTick + Ticks(EdgeGraceSeconds);

        void EdgeComes(Boss boss)
        {
            if (boss.Definition.Edge == null || boss.Phase != boss.Definition.Edge.Phase) return;
            boss.EdgeTick = Tick;
        }

        // Every tick of a fight: the edge does its work on whoever stands in it.
        void WorkEdge(Boss boss)
        {
            if (!EdgeUp(boss) || !EdgeBites(boss)) return;
            var edge = boss.Definition.Edge;
            bool bites = edge.Kind != EdgeKind.Web && edge.Kind != EdgeKind.Water && (Tick - boss.EdgeTick) % Ticks(GroundSeconds) == 0;
            int damage = Math.Max(1, (int)MathF.Round(boss.Definition.RoarDamage * GroundShare));
            foreach (var player in _players)
            {
                if (player.IsDead || !InEdge(boss, player.Position)) continue;
                if (edge.Kind == EdgeKind.Lava || edge.Kind == EdgeKind.Water) player.Afflictions.WadeTicks = Math.Max(player.Afflictions.WadeTicks, Ticks(SnareSeconds));
                if (edge.Kind == EdgeKind.Web) player.Afflictions.SnareTicks = Math.Max(player.Afflictions.SnareTicks, Ticks(SnareSeconds));
                if (bites) HurtPlayer(player, boss.Id, damage, 0);
            }
        }

        // ---- The ground: what an attack leaves (boss_attacks.csv pool) ----

        // What a landed blast leaves, for its attack's linger: a web or venom as before, or ground. A trail's leaves
        // one strip when its last blast lands, a cross's each arm; a charge's run is laid where it ends (EndCharge).
        void LeavePool(Hazard hazard)
        {
            if (hazard.Cause <= 0 || hazard.Shape != HazardShape.Circle && hazard.Shape != HazardShape.Line) return;
            var boss = FindBoss(hazard.Source);
            if (boss == null || boss.IsDead || hazard.Cause > boss.Definition.Attacks.Count) return;
            var attack = boss.Definition.Attacks[hazard.Cause - 1].ForPhase(boss.Phase);
            if (attack.Pool == PoolKind.None || boss.Phase < attack.PoolFrom || attack.Linger <= 0) return;
            // A pillar rises where it landed (a trail's, where its last blast lands: R2b).
            if (attack.Pool == PoolKind.Pillar || attack.Pool == PoolKind.Crystal || attack.Pool == PoolKind.Banner)
            {
                var kind = attack.Pool == PoolKind.Pillar ? PropKind.Pillar : attack.Pool == PoolKind.Crystal ? PropKind.Crystal : PropKind.Banner;
                if (hazard.Shape == HazardShape.Circle && (attack.Shape != AttackShape.Trail || hazard.StripFrom != null)) RaiseProp(boss, kind, hazard.Position, attack.Linger);
                return;
            }
            if (hazard.Shape == HazardShape.Line)
            {
                if (attack.Shape == AttackShape.Cross) LayGround(boss, hazard.Cause, attack.Pool, hazard.Position, hazard.Direction, hazard.Size, hazard.Width, attack.Linger, attack.Damage);
                return;
            }
            if (attack.Shape == AttackShape.Trail)
            {
                if (hazard.StripFrom is Vec2 from)
                {
                    var run = hazard.Position - from;
                    if (run.LengthSquared > 1e-4f) LayGround(boss, hazard.Cause, attack.Pool, from, run.Normalized, run.Length, hazard.Size * 2, attack.Linger, attack.Damage);
                }
                return;
            }
            LayGround(boss, hazard.Cause, attack.Pool, hazard.Position, hazard.Direction, hazard.Size, 0, attack.Linger, attack.Damage);
        }

        // Lays a pool or strip (width 0 for a circle of size). Too many of the boss's at once and the oldest goes. The
        // first lava or crack of a fight bites no one (Docs/BossRedesign.md 4.2: a new kind is shown once unharmed).
        void LayGround(Boss boss, int cause, PoolKind kind, Vec2 at, Vec2 direction, float size, float width, float seconds, int blow)
        {
            var pools = _hazards.Where(h => h.Lingers && h.Shape != HazardShape.Tether && h.Source == boss.Id).OrderBy(h => h.StartTick).ToList();
            if (pools.Count >= MaxPools) _hazards.Remove(pools[0]);
            bool bites = kind == PoolKind.Venom || kind == PoolKind.Lava || kind == PoolKind.Crack || kind == PoolKind.Pit;
            int damage = bites ? Math.Max(1, (int)MathF.Round(blow * (kind == PoolKind.Venom ? VenomShare : GroundShare))) : 0;
            if (kind == PoolKind.Lava || kind == PoolKind.Crack || kind == PoolKind.Pit)
            {
                if (boss.TaughtTick < 0) boss.TaughtTick = Tick;
                if (boss.TaughtTick == Tick) damage = 0;
            }
            var shape = kind switch
            {
                PoolKind.Web => HazardShape.Web,
                PoolKind.Venom => HazardShape.Venom,
                PoolKind.Lava => HazardShape.Lava,
                PoolKind.Water => HazardShape.Water,
                PoolKind.Crack => HazardShape.Crack,
                PoolKind.Pit => HazardShape.Pit,
                _ => HazardShape.Rock,
            };
            _hazards.Add(new Hazard(_nextId++, boss.Id, cause, shape, at, direction, size, width, damage, Tick, Tick + Ticks(seconds)));
        }

        // Lava, water and cracks while they lie there. Cooled rock does nothing; nor does lava to whoever stands on rock.
        void LingerGround(Hazard pool)
        {
            if (pool.Shape == HazardShape.Rock) return;
            // A sand pit drags and bites at its middle (R3).
            if (pool.Shape == HazardShape.Pit)
            {
                WorkPit(pool.Source, pool.Cause, pool.Position, pool.Size, pool.Damage, pool.StartTick);
                return;
            }
            bool bites = pool.Damage > 0 && pool.Shape != HazardShape.Water && Tick > pool.StartTick && (Tick - pool.StartTick) % Ticks(GroundSeconds) == 0;
            bool wades = pool.Shape != HazardShape.Crack;
            if (!bites && !wades) return;
            foreach (var player in _players)
            {
                if (player.IsDead || !pool.Contains(player.Position, PlayerRadius)) continue;
                if (pool.Shape == HazardShape.Lava && OnRock(pool.Source, player.Position)) continue;
                if (wades) player.Afflictions.WadeTicks = Math.Max(player.Afflictions.WadeTicks, Ticks(SnareSeconds));
                if (bites) HurtPlayer(player, pool.Source, pool.Damage, pool.Cause);
            }
        }

        // Lava whose time is up cools into rock of the same size, where it lay.
        void Cool(Hazard lava)
        {
            var boss = FindBoss(lava.Source);
            if (boss == null || boss.IsDead) return;
            LayGround(boss, lava.Cause, PoolKind.Rock, lava.Position, lava.Direction, lava.Size, lava.Width, CooledSeconds, 0);
        }

        // A charge that leaves ground (pool) lays it along the way it ran.
        void EndCharge(Boss boss, BossAttack attack, Vec2 from)
        {
            if (attack.Pool == PoolKind.None || boss.Phase < attack.PoolFrom || attack.Linger <= 0) return;
            var run = boss.Position - from;
            if (run.LengthSquared < 1) return;
            LayGround(boss, Cause(boss, attack), attack.Pool, from, run.Normalized, run.Length, attack.Width, attack.Linger, attack.Damage);
        }

        // ---- Things standing in the arena (props column) ----

        // How big each kind stands, and how much a breakable one takes (0: cannot be broken).
        // A vein breaks at a share of its boss's health (World.PropHealthFor); vents and pillars do not break.
        public static float PropRadius(PropKind kind) => kind switch
        {
            PropKind.Pillar => 0.9f,
            PropKind.Egg => 0.5f,
            PropKind.Banner => 0.6f,
            _ => 0.8f,
        };

        // From a phase whose props the sheet gives, those replace whatever of the boss's stood before. Evenly round the
        // middle at 0.6 of the floor, from a turn rolled on a stream of its own (nothing else rolls differently).
        void PlaceProps(Boss boss)
        {
            var given = boss.Phase < boss.Definition.PropsByPhase.Count ? boss.Definition.PropsByPhase[boss.Phase] : null;
            if (given == null) return;
            _props.RemoveAll(p => p.Owner == boss.Id);
            var kinds = given.SelectMany(g => Enumerable.Repeat(g.Kind, g.Count)).ToList();
            float turn = new GameRandom(_forageSeed + (ulong)Tick, stream: 2000 + (ulong)boss.Id).NextFloat() * 2 * MathF.PI;
            float reach = FloorRadius(boss) * 0.6f;
            // The first sand pit is the big one in the middle (R3); the rest stand round it.
            if (kinds.Remove(PropKind.Pit))
                _props.Add(new BossProp(_nextId++, boss.Id, PropKind.Pit, boss.Home, BigPitRadius, 0));
            for (int i = 0; i < kinds.Count; i++)
            {
                float radius = kinds[i] == PropKind.Pit ? SmallPitRadius : PropRadius(kinds[i]);
                var at = kinds[i] == PropKind.Pit ? boss.Home + Vec2.FromAngle(turn + i * 2 * MathF.PI / kinds.Count) * reach
                    : Collision.Resolve(Map, boss.Home + Vec2.FromAngle(turn + i * 2 * MathF.PI / kinds.Count) * reach, radius);
                _props.Add(new BossProp(_nextId++, boss.Id, kinds[i], at, radius, PropHealthFor(boss, kinds[i])));
            }
        }

        // A boss's shot that meets a pillar of its own stops there.
        bool Blocked(Projectile shot) => !shot.Friendly && _props.Any(p => p.Kind == PropKind.Pillar && p.Owner == shot.Source
            && Vec2.Distance(p.Position, shot.Position) <= p.Radius + shot.Radius);


        // ---- Vents and the blast that spares rock ----

        // Every vent bursts count times, spread seconds apart, taking turns (the even ones, then the odd): the way
        // through is past a vent that has just burst. With no vents it bursts under the boss.
        void LayVents(Boss boss, BossAttack attack, long resolve, int cause)
        {
            var vents = _props.Where(p => p.Owner == boss.Id && p.Kind == PropKind.Vent).Select(p => p.Position).ToList();
            if (vents.Count == 0) vents.Add(boss.Position);
            for (int beat = 0; beat < attack.Count; beat++)
            {
                long at = resolve + Ticks(attack.Spread * beat);
                for (int v = 0; v < vents.Count; v++)
                    if (vents.Count == 1 || (v + beat) % 2 == 0) AddHazard(boss, cause, HazardShape.Circle, vents[v], boss.Facing, attack.Radius, 0, attack.Damage, at);
            }
        }

        // The whole arena, sparing rock; with none down, rock is laid round the boss for the telegraph and a moment past.
        void LayRefuge(Boss boss, BossAttack attack, long resolve, int cause)
        {
            bool rock = Islands(boss).Any() || _hazards.Any(h => h.Shape == HazardShape.Rock && h.Source == boss.Id);
            if (!rock) LayGround(boss, cause, PoolKind.Rock, boss.Position, boss.Facing, boss.Definition.Radius + attack.Radius, 0, (resolve - Tick) * TickSeconds + 1, 0);
            AddHazard(boss, cause, HazardShape.Circle, boss.Home, boss.Facing, boss.ArenaRadius + ArenaMargin, 0, attack.Damage, resolve).Spares = true;
        }

        // ---- Rage (T9) ----

        // Hurt taken counts toward its rage (not while raging or cowering).
        void Heat(Boss boss, float amount)
        {
            var rage = boss.Definition.RageIn(boss.Phase);
            if (rage == null || rage.Hurt <= 0 || boss.Raging || boss.SpendNext || boss.State == BossState.Spent) return;
            boss.RageHurt += amount;
            if (boss.RageHurt >= rage.Hurt * boss.MaxHealth) Enrage(boss);
        }

        // Starts its rage (hurt enough, or a vein eaten: R2b).
        internal void Enrage(Boss boss)
        {
            var rage = boss.Definition.RageIn(boss.Phase);
            if (rage == null || boss.Raging) return;
            boss.RageTicks = Ticks(rage.Seconds);
            boss.RageHurt = 0;
            _events.Add(new SimEvent(SimEventType.Rage, boss.Id, amount: 1));
        }

        // Every tick of a fight: the rage runs down; spent, the boss cowers once it is done with the attack it is on.
        void RunRage(Boss boss)
        {
            if (boss.RageTicks > 0 && --boss.RageTicks == 0) boss.SpendNext = true;
        }

        // Instead of choosing an attack: cower, open (BossState.Spent), if its rage has just been spent.
        bool Spend(Boss boss)
        {
            if (!boss.SpendNext) return false;
            boss.SpendNext = false;
            var rage = boss.Definition.RageIn(boss.Phase);
            boss.Attack = null;
            boss.State = BossState.Spent;
            boss.StateTicks = Math.Max(1, Ticks(rage?.Rest ?? 0));
            _events.Add(new SimEvent(SimEventType.Rage, boss.Id, amount: 0));
            return true;
        }

        // A raging boss walks faster.
        static float RageSpeed(Boss boss) => boss.Raging ? boss.Definition.RageIn(boss.Phase)?.Speed ?? 1 : 1;

        // A new phase or a reset: its rage, its edge's start and its first ground start over.
        static void CalmDown(Boss boss)
        {
            boss.RageTicks = 0;
            boss.RageHurt = 0;
            boss.SpendNext = false;
        }
    }
}
