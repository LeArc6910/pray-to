using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // ---- R3 (2026-11-28, Docs/BossRedesign.md 7.1 T4·T5·T7, Docs/Bosses.md "끌기·숨기·벽"): forces on a player's
    // walk (a pull toward a blow's middle, a push out of a ring's safe middle, a sand pit's drag, a thread that drags back
    // whoever stretches it), the mound that chases under the floor and bursts up where it stops, and walls of water
    // that sweep the arena with a gap to find. Every force is slower than a walk, so it can be walked against; the host
    // moves its players with these and a client moves its own with the same (WorldClient.Step), so the two agree. ----
    public sealed partial class World
    {
        // How fast a sand pit drags (30% of a walk), and how near its middle its jaws bite (every GroundSeconds).
        public const float PitPull = 1.5f;
        public const float PitCore = 0.7f;
        // A phase's first pit (PropKind.Pit) stands in the arena's middle this wide; the rest round it this wide.
        public const float BigPitRadius = 3;
        public const float SmallPitRadius = 1.5f;
        // A chasing mound stops this long before it bursts up (AttackShape.Burrow with length: the moment to step off).
        public const float MoundStop = 0.8f;
        // A thread (AttackShape.Tether): how far it stretches before it drags back, how fast, how near two held
        // together may come before it snaps, how many cuts part it, and how long after it takes hold it may snap.
        public const float ThreadSlack = 6;
        public const float ThreadPull = 2.5f;
        public const float ThreadSnap = 2;
        public const int ThreadCuts = 3;
        public const float ThreadGrace = 1;
        // A wall of water (AttackShape.Wave, Close): how thick it is, how far it pushes whoever it strikes, and how far
        // either side of its gap it reaches (past any arena's floor).
        public const float WaveThick = 0.9f;
        public const float WaterPush = 1.5f;
        public const float WaveSpan = 18;

        // ---- Steps shared with the client ----

        // One tick of a push out from a middle (HazardShape.Push): whoever is within size goes outward at speed.
        public static Vec2 PushStep(TileMap map, Vec2 position, Vec2 centre, float size, float speed, ISet<int> doors = null)
        {
            var away = position - centre;
            float distance = away.Length;
            if (distance > size + PlayerRadius) return position;
            var way = distance < 0.05f ? new Vec2(1, 0) : away / distance;
            return Collision.Move(map, position, PlayerRadius, way * (speed * TickSeconds), passable: doors);
        }

        // One tick of a sand pit's drag (HazardShape.Pit, PropKind.Pit).
        public static Vec2 PitStep(TileMap map, Vec2 position, Vec2 centre, float radius, ISet<int> doors = null) =>
            PullStep(map, position, centre, radius - PlayerRadius, PitPull, doors);

        // One tick of a thread stretched too far: back toward its other end.
        public static Vec2 ThreadStep(TileMap map, Vec2 position, Vec2 other, ISet<int> doors = null)
        {
            var toward = other - position;
            float distance = toward.Length;
            if (distance <= ThreadSlack) return position;
            return Collision.Move(map, position, PlayerRadius, toward / distance * Math.Min(distance - ThreadSlack, ThreadPull * TickSeconds), passable: doors);
        }

        // Where a wall's middle line is at a tick: its start until it moves, then along its way to the end.
        public static Vec2 WaveFront(Vec2 start, Vec2 way, float travel, long startTick, long endTick, long tick)
        {
            if (tick <= startTick || endTick <= startTick) return start;
            float done = Math.Min(1, (tick - startTick) / (float)(endTick - startTick));
            return start + way * (travel * done);
        }

        // Whether a wall still to pass a spot will strike whoever stands there: not past it, not beyond where it stops,
        // not in its gap (for the bots: World.WaveStrikes on the host's walls).
        public static bool WaveCrosses(Vec2 start, Vec2 way, float travel, float gap, Vec2 front, Vec2 at)
        {
            float reach = WaveThick / 2 + PlayerRadius;
            if (Vec2.Dot(at - front, way) < -reach) return false;
            if (Vec2.Dot(at - (start + way * travel), way) > reach) return false;
            var side = new Vec2(-way.Y, way.X);
            return !(gap > 0 && MathF.Abs(Vec2.Dot(at - start, side)) <= gap / 2 - PlayerRadius);
        }

        public bool WaveStrikes(Hazard wave, Vec2 at) => wave.Shape == HazardShape.Wave
            && WaveCrosses(wave.Position, wave.Direction, wave.Size, wave.Width, WaveFront(wave.Position, wave.Direction, wave.Size, wave.StartTick, wave.ResolveTick, Tick), at);

        // ---- Pulls and pushes while a blow winds up (pull column) ----

        // A pull toward the middle of what was just laid (the boss's own for a cone, line or charge), the whole arena
        // over; or a push out of a ring's (or circle's) middle, within its safe middle (or the circle).
        void LayDrag(Boss boss, BossAttack attack, int laid, long resolve, int cause)
        {
            var centre = laid < _hazards.Count && (attack.Shape == AttackShape.Circle || attack.Shape == AttackShape.Ring) ? _hazards[laid].Position : boss.Position;
            if (attack.Pull > 0)
                AddHazard(boss, cause, HazardShape.Pull, centre, boss.Facing, boss.ArenaRadius * 2 + ArenaMargin, attack.Pull, 0, resolve);
            else
                AddHazard(boss, cause, HazardShape.Push, centre, boss.Facing, attack.Radius, -attack.Pull, 0, resolve);
        }

        // A push while it is pending: everyone in it goes outward.
        void Pushes(Hazard hazard)
        {
            foreach (var player in _players)
                if (!player.IsDead) player.Position = PushStep(Map, player.Position, hazard.Position, hazard.Size, hazard.Width, DoorsFor(player));
        }

        // ---- Sand pits (pool pit, props pit) ----

        // A pit's drag on everyone in it, and its jaws on whoever is at its middle every GroundSeconds (from its start).
        void WorkPit(int source, int cause, Vec2 centre, float radius, int bite, long since)
        {
            bool bites = bite > 0 && Tick > since && (Tick - since) % Ticks(GroundSeconds) == 0;
            foreach (var player in _players)
            {
                if (player.IsDead || Vec2.Distance(player.Position, centre) > radius) continue;
                player.Position = PitStep(Map, player.Position, centre, radius, DoorsFor(player));
                if (bites && Vec2.Distance(player.Position, centre) <= PitCore + PlayerRadius) HurtPlayer(player, source, bite, cause);
            }
        }

        // Every tick of a fight: the boss's standing pits (from a phase's props), biting for the ground's share of its roar.
        void WorkPits(Boss boss)
        {
            int bite = Math.Max(1, (int)MathF.Round(boss.Definition.RoarDamage * GroundShare));
            foreach (var pit in _props)
                if (pit.Owner == boss.Id && pit.Kind == PropKind.Pit) WorkPit(boss.Id, 0, pit.Position, pit.Radius, bite, 0);
        }

        // Whether a spot is in one of a boss's pits (ground or standing), for the bots.
        public bool InPit(Vec2 at) =>
            _props.Any(p => p.Kind == PropKind.Pit && Vec2.Distance(p.Position, at) <= p.Radius)
            || _hazards.Any(h => h.Shape == HazardShape.Pit && Vec2.Distance(h.Position, at) <= h.Size);

        // ---- The chasing mound (T5) ----

        // While it winds up the boss goes under after its target, slower than a walk; World.MoundStop before the end it
        // stops, and where it stopped is marked: it bursts up there (Unleash).
        void Chase(Boss boss)
        {
            var attack = boss.Attack;
            int stop = Ticks(MoundStop);
            if (boss.StateTicks > stop)
            {
                var target = FindPlayer(boss.TargetId);
                if (target == null || target.IsDead) return;
                var way = target.Position - boss.Position;
                if (way.Length > 0.1f) MoveBoss(boss, way.Normalized * Math.Min(way.Length, attack.Length * TickSeconds));
                boss.LandAt = boss.Position;
            }
            else if (boss.StateTicks == stop)
            {
                boss.LandAt = boss.Position;
                AddHazard(boss, Cause(boss, attack), HazardShape.Circle, boss.Position, boss.Facing, attack.Radius, 0, attack.Damage, Tick + stop);
            }
        }

        // ---- Threads (T4, AttackShape.Tether) ----

        // When its telegraph ends: alone, a thread from the boss to the nearest; together, threads between pairs of
        // players (the nearest the boss with the farthest), count of them. Threads of the boss's from before go.
        void LayThreads(Boss boss, BossAttack attack)
        {
            _hazards.RemoveAll(h => h.Shape == HazardShape.Tether && h.Source == boss.Id);
            var fighters = Fighters(boss).OrderBy(p => Vec2.Distance(p.Position, boss.Position)).ToList();
            long ends = Tick + Ticks(attack.Spread);
            int cause = Cause(boss, attack);
            if (fighters.Count == 1)
            {
                Thread(boss, cause, attack.Damage, 0, boss.Position, fighters[0], ends);
                return;
            }
            for (int i = 0; i < attack.Count && 2 * i + 1 < fighters.Count; i++)
            {
                var near = fighters[i];
                var far = fighters[fighters.Count - 1 - i];
                if (near == far) break;
                Thread(boss, cause, attack.Damage, near.Id, near.Position, far, ends);
            }
        }

        void Thread(Boss boss, int cause, int damage, int anchorId, Vec2 anchor, Player bound, long ends)
        {
            var run = bound.Position - anchor;
            var thread = new Hazard(_nextId++, boss.Id, cause, HazardShape.Tether, anchor, run.LengthSquared > 1e-6f ? run.Normalized : boss.Facing,
                run.Length, ThreadCuts, damage, Tick, ends)
            {
                BoundId = bound.Id,
                AnchorId = anchorId,
                SnapsFrom = Tick + Ticks(ThreadGrace),
            };
            _hazards.Add(thread);
            _events.Add(new SimEvent(SimEventType.Impact, boss.Id, bound.Id, 0, position: bound.Position));
        }

        // Every tick a thread holds: its ends follow who holds them; stretched too far it drags its player back (both,
        // between two); two held too close snap it and are both hurt. Either end gone, or cut through, and it is gone.
        void WorkThread(Hazard thread)
        {
            var boss = FindBoss(thread.Source);
            var bound = FindPlayer(thread.BoundId);
            var anchorPlayer = thread.AnchorId != 0 ? FindPlayer(thread.AnchorId) : null;
            bool held = boss != null && !boss.IsDead && boss.State != BossState.Dormant && bound != null && !bound.IsDead
                && (thread.AnchorId == 0 || anchorPlayer != null && !anchorPlayer.IsDead) && thread.Width > 0;
            if (!held)
            {
                thread.ResolveTick = Tick;
                return;
            }
            var anchor = anchorPlayer?.Position ?? boss.Position;
            if (Vec2.Distance(bound.Position, anchor) > ThreadSlack)
            {
                bound.Position = ThreadStep(Map, bound.Position, anchor, DoorsFor(bound));
                if (anchorPlayer != null) anchorPlayer.Position = ThreadStep(Map, anchorPlayer.Position, bound.Position, DoorsFor(anchorPlayer));
                anchor = anchorPlayer?.Position ?? boss.Position;
            }
            var run = bound.Position - anchor;
            thread.Position = anchor;
            if (run.LengthSquared > 1e-6f) thread.Direction = run.Normalized;
            thread.Size = run.Length;
            if (anchorPlayer != null && Tick >= thread.SnapsFrom && run.Length < ThreadSnap)
            {
                foreach (var held2 in new[] { bound, anchorPlayer })
                    AfflictPlayer(held2, boss.Id, HurtPlayer(held2, boss.Id, thread.Damage, thread.Cause));
                _events.Add(new SimEvent(SimEventType.Impact, boss.Id, thread.Id, 2, position: anchor + run * 0.5f));
                thread.ResolveTick = Tick;
            }
        }

        // A swing that crosses a thread cuts it once (its arc over any point along it).
        bool SwingThreads(Player player, float reach, float arc)
        {
            bool hit = false;
            foreach (var thread in _hazards.Where(h => h.Shape == HazardShape.Tether && h.Width > 0 && Tick < h.ResolveTick).ToList())
            {
                int steps = Math.Max(1, (int)MathF.Ceiling(thread.Size / 0.4f));
                bool crosses = false;
                for (int i = 1; i < steps && !crosses; i++)
                    crosses = Combat.InArc(player.Position, player.Facing, thread.Position + thread.Direction * (thread.Size * i / steps), 0.1f, reach, arc);
                if (!crosses) continue;
                hit = true;
                thread.Width -= 1;
                _events.Add(new SimEvent(SimEventType.Impact, thread.Source, thread.Id, thread.Width <= 0 ? 1 : 0, position: thread.Position + thread.Direction * (thread.Size / 2)));
                if (thread.Width <= 0) thread.ResolveTick = Tick;
            }
            return hit;
        }

        // ---- Walls of water (T7) ----

        // count walls, spread seconds apart, from the side of the arena away from the target through it; each gap rolled
        // on a stream of its own (nothing else rolls differently for it), the later ones well off the one before. Each
        // gap's way across is marked green until its wall comes.
        void LayWaves(Boss boss, BossAttack attack, Player target, long resolve, int cause)
        {
            var toward = target.Position - boss.Home;
            var way = toward.Length > 0.5f ? toward.Normalized : boss.Facing;
            float reach = boss.ArenaRadius + 1;
            float room = FloorRadius(boss) * 0.6f;
            var roll = new GameRandom(_forageSeed + (ulong)Tick, stream: 3000 + (ulong)boss.Id);
            float last = float.NaN;
            for (int i = 0; i < attack.Count; i++)
            {
                float offset = (roll.NextFloat() * 2 - 1) * room;
                if (!float.IsNaN(last) && MathF.Abs(offset - last) < attack.Width * 1.5f)
                    offset = last + (last > 0 ? -1 : 1) * attack.Width * 1.5f;
                last = offset;
                // Each wall after the first turned angle degrees on from the last (R4a, Morg's ranks criss-crossing).
                var along = attack.Angle != 0 ? Vec2.FromAngle(way.Angle + i * attack.Angle * MathF.PI / 180f) : way;
                var across = new Vec2(-along.Y, along.X);
                var start = boss.Home - along * reach + across * offset;
                long from = resolve + Ticks(attack.Spread * i);
                AddWave(boss, cause, start, along, reach * 2, attack.Width, attack.Damage, from, from + Ticks(reach * 2 / attack.Speed));
                if (attack.Width > 0) AddHazard(boss, cause, HazardShape.Line, start, along, reach * 2, attack.Width, 0, from).Safe = true;
            }
        }

        // Two walls from either side across the boss's facing, stopping width apart over the middle (marked green).
        void LayClose(Boss boss, BossAttack attack, long resolve, int cause)
        {
            var axis = boss.Facing;
            var side = new Vec2(-axis.Y, axis.X);
            float reach = boss.ArenaRadius + 1;
            float travel = reach - attack.Width / 2 - WaveThick / 2;
            foreach (int sign in new[] { -1, 1 })
                AddWave(boss, cause, boss.Home + side * (sign * reach), side * -sign, travel, 0, attack.Damage, resolve, resolve + Ticks(travel / attack.Speed));
            AddHazard(boss, cause, HazardShape.Line, boss.Home - axis * reach, axis, reach * 2, attack.Width, 0, resolve).Safe = true;
        }

        void AddWave(Boss boss, int cause, Vec2 start, Vec2 way, float travel, float gap, int damage, long from, long to) =>
            _hazards.Add(new Hazard(_nextId++, boss.Id, cause, HazardShape.Wave, start, way, travel, gap, damage, from, to));

        // A wall on its way: whoever its front passes over (not in its gap, not rolling through) is struck once and
        // pushed on.
        void WorkWave(Hazard wave)
        {
            if (Tick < wave.StartTick) return;
            var front = WaveFront(wave.Position, wave.Direction, wave.Size, wave.StartTick, wave.ResolveTick, Tick);
            var side = new Vec2(-wave.Direction.Y, wave.Direction.X);
            foreach (var player in _players)
            {
                if (player.IsDead || wave.Struck.Contains(player.Id)) continue;
                if (MathF.Abs(Vec2.Dot(player.Position - front, wave.Direction)) > WaveThick / 2 + PlayerRadius) continue;
                if (wave.Width > 0 && MathF.Abs(Vec2.Dot(player.Position - wave.Position, side)) <= wave.Width / 2 - PlayerRadius / 2) continue;
                if (Dodges(player, wave.Source)) continue;
                wave.Struck.Add(player.Id);
                AfflictPlayer(player, wave.Source, HurtPlayer(player, wave.Source, wave.Damage, wave.Cause));
                player.Position = Collision.Move(Map, player.Position, PlayerRadius, wave.Direction * WaterPush, passable: DoorsFor(player));
                player.Corrections++;
            }
        }

        // ---- The tidal wave (AttackShape.Half) ----

        // The half of the arena the boss faces, from its middle on.
        void LayHalf(Boss boss, BossAttack attack, long resolve, int cause)
        {
            float reach = boss.ArenaRadius + ArenaMargin;
            AddHazard(boss, cause, HazardShape.Line, boss.Home, boss.Facing, reach, reach * 2, attack.Damage, resolve);
        }
    }
}
