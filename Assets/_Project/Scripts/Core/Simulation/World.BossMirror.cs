using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // ---- R4b (2026-11-28, Docs/BossRedesign.md 7.1 T6·T8 and 5.6·5.8, Docs/Bosses.md "분신·되감기·별 미로"): the crown
    // wraith's mirror images (only the true one's crown is gold), its puddles that carry its lightning, its sunken hands
    // and its mist; the void lord's rewind (its blasts of the last seconds land again, newest first), its gates (a beam
    // goes in at one and out of the other) and its star maze. ----
    public sealed partial class World
    {
        // How many images a boss may have at once, how far apart they copy a blow (the first after the boss), and at
        // what share of it.
        public const int MostClones = 3;
        public const float CloneLag = 0.35f;
        public const float CloneShare = 0.4f;
        // Where round the arena's middle (of its floor) the boss and its images form.
        const float CloneReach = 0.55f;
        // How long a sunken hand holds whoever it catches.
        public const float HandHold = 1;
        // What share of a lightning blast flashes through a puddle it touches.
        public const float PuddleShare = 0.7f;
        public const float PuddleRadius = 1.4f;
        public const float GateRadius = 0.7f;
        // How far back a boss's blasts are kept for its rewind, at what share they land again, how much quicker than
        // they first came (their gaps), the least gap between two, and at most how many.
        public const float EchoSeconds = 10;
        public const float EchoShare = 0.8f;
        public const float EchoPace = 0.7f;
        const float EchoLeast = 0.2f;
        public const int MostEchoes = 10;
        // The star maze: each ring's gap (degrees), the wait before the first ring falls, how often a fallen ring
        // strikes again, at what share of its fall.
        public const float MazeGap = 70;
        public const float MazeLead = 2;
        public const float MazeBurn = 1;
        public const float MazeBurnShare = 0.5f;
        // What the star maze's rings strike for, of the boss's roar.
        public const float MazeShare = 0.5f;
        // How often something falls while the wraith is mist.
        public const float MistRain = 1.5f;

        // The Cue's Amount as images form, as the boss comes out behind its target, and as a rewind begins.
        public const int CueClones = 4;
        public const int CueBehind = 5;
        public const int CueRewind = 6;

        // ---- Mirror images (AttackShape.Clone) ----

        // Where the boss and count images will form, marked (harmless) while it is mist.
        void LayCloneSpots(Boss boss, BossAttack attack, long resolve, int cause) => LayCloneSpots(boss, attack.Count, resolve, cause);

        void LayCloneSpots(Boss boss, int count, long resolve, int cause)
        {
            var roll = new GameRandom(_forageSeed + (ulong)Tick, stream: 6000 + (ulong)boss.Id);
            float turn = roll.NextFloat() * 2 * MathF.PI;
            float reach = FloorRadius(boss) * CloneReach;
            boss.CloneSpots.Clear();
            for (int i = 0; i <= count; i++)
                boss.CloneSpots.Add(LandingFor(boss, boss.Home + Vec2.FromAngle(turn + i * 2 * MathF.PI / (count + 1)) * reach));
            boss.CloneSelf = (int)(roll.NextUInt() % (uint)(count + 1));
            foreach (var spot in boss.CloneSpots)
                AddHazard(boss, cause, HazardShape.Circle, spot, boss.Facing, boss.Definition.Radius, 0, 0, resolve);
        }

        // Out of the mist: the boss at its own spot, an image at each of the others, for seconds.
        void FormClones(Boss boss, float seconds)
        {
            _props.RemoveAll(p => p.Owner == boss.Id && p.Kind == PropKind.Clone);
            if (boss.CloneSpots.Count == 0) return;
            boss.Position = boss.CloneSpots[Math.Min(boss.CloneSelf, boss.CloneSpots.Count - 1)];
            boss.LastPosition = boss.Position;
            for (int i = 0; i < boss.CloneSpots.Count; i++)
            {
                if (i == boss.CloneSelf) continue;
                _props.Add(new BossProp(_nextId++, boss.Id, PropKind.Clone, boss.CloneSpots[i], boss.Definition.Radius, 1, Tick + Ticks(seconds)));
            }
            boss.CloneSpots.Clear();
            _events.Add(new SimEvent(SimEventType.Cue, boss.Id, amount: CueClones, direction: boss.Facing));
        }

        // Every tick of a fight: its images take the step it took (a jump, a leap's landing, is not a step).
        void WalkClones(Boss boss)
        {
            var step = boss.Position - boss.LastPosition;
            boss.LastPosition = boss.Position;
            if (step.LengthSquared < 1e-8f || step.LengthSquared > 0.25f) return;
            float limit = Math.Max(0, FloorRadius(boss) - boss.Definition.Radius);
            foreach (var clone in PropsOf(boss, PropKind.Clone))
            {
                var at = clone.Position + step;
                var fromHome = at - boss.Home;
                if (fromHome.Length > limit) at = boss.Home + fromHome.Normalized * limit;
                clone.Position = Collision.Resolve(Map, at, clone.Radius);
            }
        }

        // A blow on the boss itself: its images scatter.
        void ScatterClones(Boss boss)
        {
            if (_props.RemoveAll(p => p.Owner == boss.Id && p.Kind == PropKind.Clone) > 0)
                _events.Add(new SimEvent(SimEventType.Impact, boss.Id, amount: 3));
        }

        // Its images lay the blow too, from where each stands toward its nearest, one after another, at a share.
        void Mimic(Boss boss, BossAttack attack, Player target, List<Player> fighters, long resolve, int cause)
        {
            if (!attack.Mimic) return;
            var clones = PropsOf(boss, PropKind.Clone).OrderBy(p => p.Id).ToList();
            if (clones.Count == 0) return;
            var (position, facing) = (boss.Position, boss.Facing);
            for (int i = 0; i < clones.Count; i++)
            {
                var clone = clones[i];
                var aim = fighters.OrderBy(p => Vec2.Distance(p.Position, clone.Position)).First().Position - clone.Position;
                boss.Position = clone.Position;
                boss.Facing = aim.LengthSquared > 1e-6f ? aim.Normalized : facing;
                int laid = _hazards.Count;
                LayHazards(boss, attack, target, fighters, boss.Facing, resolve + Ticks(CloneLag * (i + 1)), cause);
                for (int h = laid; h < _hazards.Count; h++)
                    _hazards[h].Damage = Math.Max(1, (int)MathF.Round(_hazards[h].Damage * CloneShare));
            }
            (boss.Position, boss.Facing) = (position, facing);
        }

        // Images running through the target across the boss's own run (boost cross, the crown wraith's phantom rush in
        // its second phase): turned CrossAngle either way, each CloneLag after the last and CloneShare as hard.
        void LayCrossing(Boss boss, BossAttack attack, Player target, Vec2 facing, long resolve, int cause)
        {
            for (int i = 0; i < attack.Cross; i++)
            {
                float turn = (i % 2 == 0 ? 1 : -1) * CrossAngle * (1 + i / 2);
                var way = Vec2.FromAngle(facing.Angle + turn);
                AddHazard(boss, cause, HazardShape.Line, target.Position - way * (attack.Length / 2), way, attack.Length, attack.Width,
                    Math.Max(1, (int)MathF.Round(attack.Damage * CloneShare)), resolve + Ticks(CloneLag * (i + 1)));
            }
        }

        public const float CrossAngle = MathF.PI / 3;

        // Where a falling star lands, drawn toward the boss (boost bend): up to bend tiles, never onto the boss.
        Vec2 Bent(Boss boss, BossAttack attack, Vec2 spot)
        {
            if (attack.Bend <= 0) return spot;
            var toward = boss.Position - spot;
            float distance = toward.Length;
            if (distance < 1e-3f) return spot;
            return spot + toward / distance * Math.Min(attack.Bend, Math.Max(0, distance - boss.Definition.Radius - attack.Radius));
        }

        // Its blow from behind (boost behind): it comes out behind its target, facing them.
        void ComeBehind(Boss boss, Player target)
        {
            var away = target.Position - boss.Position;
            var way = away.LengthSquared > 1e-6f ? away.Normalized : boss.Facing;
            var spot = LandingFor(boss, target.Position + way * (boss.Definition.Radius + PlayerRadius + 0.6f));
            if (Vec2.Distance(spot, target.Position) < boss.Definition.Radius) return;
            boss.Position = spot;
            boss.LastPosition = spot;
            _events.Add(new SimEvent(SimEventType.Cue, boss.Id, amount: CueBehind, direction: way * -1));
        }

        // ---- Puddles and lightning ----

        // A lightning boss's circle that touches a puddle of its own flashes through the whole puddle as it lands: the
        // puddle is marked too, once whatever touches it (World.StartTelegraph, after its blasts are laid).
        void Conduct(Boss boss, int from)
        {
            if (boss.Definition.Element != SkillTag.Lightning) return;
            var puddles = PropsOf(boss, PropKind.Puddle).ToList();
            if (puddles.Count == 0) return;
            int end = _hazards.Count;
            for (int i = from; i < end; i++)
            {
                var blast = _hazards[i];
                if (blast.Damage <= 0 || blast.Source != boss.Id || !(blast.Shape == HazardShape.Circle || blast.Shape == HazardShape.Share)) continue;
                foreach (var puddle in puddles)
                {
                    if (Vec2.Distance(puddle.Position, blast.Position) > puddle.Radius + blast.Size) continue;
                    if (_hazards.Any(h => h.Source == boss.Id && h.ResolveTick == blast.ResolveTick && Vec2.Distance(h.Position, puddle.Position) < 1e-3f && h.Size == puddle.Radius)) continue;
                    AddHazard(boss, blast.Cause, HazardShape.Circle, puddle.Position, boss.Facing, puddle.Radius, 0,
                        Math.Max(1, (int)MathF.Round(blast.Damage * PuddleShare)), blast.ResolveTick);
                }
            }
        }

        // A sunken hand out of every puddle (none, under each player): it holds whoever it catches.
        void LayHands(Boss boss, BossAttack attack, List<Player> fighters, long resolve, int cause)
        {
            var spots = PropsOf(boss, PropKind.Puddle).Select(p => p.Position).ToList();
            if (spots.Count == 0) spots = fighters.Select(p => p.Position).ToList();
            foreach (var spot in spots)
            {
                var hand = AddHazard(boss, cause, HazardShape.Circle, spot, boss.Facing, attack.Radius, 0, attack.Damage, resolve);
                hand.Effect = HitEffect.Root;
                hand.EffectAmount = HandHold;
            }
        }

        // ---- The rewind (AttackShape.Rewind) ----

        // A blast of a boss's has landed: kept for its rewind (not one laid again, nor a sequence's or a maze's).
        void KeepEcho(Hazard hazard)
        {
            if (hazard.Echo || hazard.Burning || hazard.Damage <= 0) return;
            if (!(hazard.Shape == HazardShape.Circle || hazard.Shape == HazardShape.Cone || hazard.Shape == HazardShape.Line
                || hazard.Shape == HazardShape.Ring || hazard.Shape == HazardShape.Share)) return;
            var boss = FindBoss(hazard.Source);
            if (boss == null || !boss.Definition.Attacks.Any(a => a.Shape == AttackShape.Rewind)) return;
            boss.Echoes.RemoveAll(e => Tick - e.Tick > Ticks(EchoSeconds));
            boss.Echoes.Add(new Echo(hazard.Shape, hazard.Position, hazard.Direction, hazard.Size, hazard.Width, hazard.Damage, Tick));
        }

        // Every blast of the last spread seconds again where it landed, the newest first, the gaps between them
        // shortened (EchoPace), at a share: what was just dodged is dangerous again.
        void LayRewind(Boss boss, BossAttack attack, long resolve, int cause)
        {
            var echoes = boss.Echoes.Where(e => Tick - e.Tick <= Ticks(attack.Spread)).OrderByDescending(e => e.Tick).Take(MostEchoes).ToList();
            if (echoes.Count == 0) return;
            long at = resolve;
            for (int i = 0; i < echoes.Count; i++)
            {
                if (i > 0) at += Math.Max(Ticks(EchoLeast), (long)((echoes[i - 1].Tick - echoes[i].Tick) * EchoPace));
                var echo = echoes[i];
                AddHazard(boss, cause, echo.Shape, echo.Position, echo.Direction, echo.Size, echo.Width,
                    Math.Max(1, (int)MathF.Round(echo.Damage * EchoShare)), at).Echo = true;
            }
        }

        // ---- Gates (the void lord's beam) ----

        // The gate a beam leaving from would come out of: the next of the boss's gates after the one it went in at.
        BossProp OtherGate(Boss boss, BossProp entered)
        {
            var gates = PropsOf(boss, PropKind.Gate).OrderBy(p => p.Id).ToList();
            if (gates.Count < 2) return null;
            return gates[(gates.IndexOf(entered) + 1) % gates.Count];
        }

        // ---- Interludes of R4b ----

        void StartMist(Boss boss, BossInterlude interlude)
        {
            boss.Position = boss.Home;
            boss.LastPosition = boss.Home;
            _props.RemoveAll(p => p.Owner == boss.Id && p.Kind == PropKind.Clone);
            boss.ChargeTicks = Ticks(MistRain);
        }

        void WorkMist(Boss boss, BossInterlude interlude, List<Player> fighters)
        {
            if (--boss.ChargeTicks > 0 || fighters.Count == 0) return;
            boss.ChargeTicks = Ticks(MistRain);
            // Where it will form shows through its last breath of mist.
            if (boss.StateTicks <= Ticks(MistRain) + 1 && boss.CloneSpots.Count == 0)
            {
                LayCloneSpots(boss, interlude.Count, Tick + boss.StateTicks, 0);
                return;
            }
            var rain = interlude.Charge.ForPhase(interlude.Phase - 1 > 0 ? interlude.Phase - 1 : 1);
            var target = fighters[_random.Next(fighters.Count)];
            int laid = _hazards.Count;
            LayHazards(boss, rain, target, fighters, boss.Facing, Tick + Ticks(rain.Telegraph), Cause(boss, rain));
            Conduct(boss, laid);
        }

        // The rings of the maze: count of them from the arena's edge in, each with its gap rolled, falling MazeLead
        // after it starts and then evenly to a second before its end.
        void StartMaze(Boss boss, BossInterlude interlude)
        {
            boss.Position = boss.Home;
            boss.LastPosition = boss.Home;
            var roll = new GameRandom(_forageSeed + (ulong)Tick, stream: 7000 + (ulong)boss.Id);
            float outer = boss.ArenaRadius + 1;
            float band = (FloorRadius(boss) - boss.Definition.Radius - 0.6f) / interlude.Count;
            float step = (interlude.Seconds - MazeLead - 1) / Math.Max(1, interlude.Count - 1);
            int damage = Math.Max(1, (int)MathF.Round(boss.Definition.RoarDamage * MazeShare));
            float gap = roll.NextFloat() * 2 * MathF.PI;
            for (int k = 0; k < interlude.Count; k++)
            {
                float outside = k == 0 ? outer : FloorRadius(boss) - band * k;
                float inside = FloorRadius(boss) - band * (k + 1);
                // Each gap well round from the last, so the way winds.
                if (k > 0) gap += (0.6f + roll.NextFloat() * 0.8f) * MathF.PI * (roll.NextFloat() < 0.5f ? -1 : 1);
                AddHazard(boss, 0, HazardShape.Maze, boss.Home, Vec2.FromAngle(gap), outside, inside, damage, Tick + Ticks(MazeLead + step * k)).Order = k + 1;
            }
        }

        // A ring of the maze that has fallen burns on while the maze lasts.
        void BurnMaze(Hazard ring)
        {
            var boss = FindBoss(ring.Source);
            if (boss == null || boss.State != BossState.Interlude || boss.Definition.Interlude?.Kind != InterludeKind.Maze) return;
            int damage = ring.Burning ? ring.Damage : Math.Max(1, (int)MathF.Round(ring.Damage * MazeBurnShare));
            var again = AddHazard(boss, ring.Cause, HazardShape.Maze, ring.Position, ring.Direction, ring.Size, ring.Width, damage, Tick + Ticks(MazeBurn));
            again.Burning = true;
            again.Order = ring.Order;
        }

        // For the playtests: a rewind's blasts on the ground (as if it had just struck), images formed round it.
        internal bool ForceClones(Boss boss, int count)
        {
            if (boss.IsDead || boss.State == BossState.Dormant) return false;
            LayCloneSpots(boss, Math.Clamp(count, 1, MostClones), Tick, 0);
            FormClones(boss, 12);
            return true;
        }
    }
}
