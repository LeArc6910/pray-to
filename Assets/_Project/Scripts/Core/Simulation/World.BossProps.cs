using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // ---- R2b (2026-11-28, Docs/BossRedesign.md 5.1·5.9, Docs/Bosses.md "기둥·광맥·벽 돌기"): the cave warden's pillars
    // (raised where its rocks land, stopping its shots, stunning it when it charges into one: its core shows and takes
    // more), the vein eater's veins (it runs to eat one and rages; broken first, it cannot) and its run through the
    // arena's wall. ----
    public sealed partial class World
    {
        // How much more a stunned boss takes (its chest rock fallen away, the core bare).
        public const float StunTakenShare = 1.5f;
        // How long the vein eater stands out of the wall after it throws, before it digs in again: the moment to hit it.
        public const float OrbitOpen = 0.9f;
        // How far past its body the shaking where it will come out of the wall reaches.
        public const float OrbitMark = 0.4f;
        // What share of the boss's health a vein takes to break.
        public const float VeinShare = 0.03f;
        // How far inside a pillar's edge the way to a spot must pass for the spot to be in its shadow (no longer used by
        // the shadow itself; kept for what reads it).
        public const float ShadeMargin = 0.15f;
        // The shadow behind a pillar from the core overload (2026-11-28, the person's sketch: "not that long; a room just
        // behind the pillar"): a fan from the pillar's middle, away from the blast, this long and this wide (degrees).
        public const float ShadeLength = 3.5f;
        public const float ShadeAngle = 80;
        // How near a vein the boss must come to eat it, past both their edges.
        const float EatReach = 0.3f;

        // ---- Pillars ----

        IEnumerable<BossProp> PropsOf(Boss boss, PropKind kind) => _props.Where(p => p.Owner == boss.Id && p.Kind == kind);

        // A pillar rises at a spot for seconds (an attack's pool), unless the boss or another pillar is in the way. Too
        // many of the boss's things standing and its oldest raised pillar crumbles first.
        void RaisePillar(Boss boss, Vec2 at, float seconds) => RaiseProp(boss, PropKind.Pillar, at, seconds);

        // Every tick of a fight: raised pillars whose time is up crumble.
        void WorkProps(Boss boss) => _props.RemoveAll(p => p.Owner == boss.Id && p.Until >= 0 && Tick >= p.Until);

        // The pillar of its own a charging boss has run into, if any.
        BossProp PillarHit(Boss boss) => PropsOf(boss, PropKind.Pillar)
            .FirstOrDefault(p => Vec2.Distance(p.Position, boss.Position) <= p.Radius + boss.Definition.Radius);

        // Walking, a boss goes round its pillars rather than into them (so a charge only stuns it when it runs into one).
        Vec2 OutOfPillars(Boss boss, Vec2 position)
        {
            foreach (var pillar in PropsOf(boss, PropKind.Pillar))
            {
                var off = position - pillar.Position;
                float keep = pillar.Radius + boss.Definition.Radius + 0.05f;
                if (off.Length >= keep) continue;
                position = pillar.Position + (off.LengthSquared > 1e-6f ? off.Normalized : boss.Facing * -1) * keep;
            }
            return position;
        }

        // Its strong blast all round with nothing standing to hide behind: two pillars rise first, evenly round the
        // middle as its props stand (Docs/BossRedesign.md 5.1 핵 폭주: only behind a pillar is safe).
        void Shelters(Boss boss, BossAttack attack)
        {
            if (attack.Shape != AttackShape.Cover || PropsOf(boss, PropKind.Pillar).Any()) return;
            float turn = (boss.Position - boss.Home).LengthSquared > 1e-4f ? (boss.Position - boss.Home).Angle + MathF.PI / 2 : 0;
            for (int i = 0; i < 2; i++)
                RaisePillar(boss, boss.Home + Vec2.FromAngle(turn + i * MathF.PI) * (FloorRadius(boss) * 0.6f), attack.Telegraph + 6);
        }

        // Whether a spot is in a pillar's shadow from a blast at from: inside the fan that opens from the pillar's middle
        // away from the blast (ShadeLength, ShadeAngle), the same fan the screen marks green. For the blast that spares
        // the shaded, the bots and the screen.
        public bool Shaded(int bossId, Vec2 from, Vec2 at)
        {
            foreach (var pillar in _props.Where(p => p.Owner == bossId && p.Kind == PropKind.Pillar))
            {
                if (ShadeOf(pillar, from) is Vec2 away && InShade(pillar.Position, away, at)) return true;
            }
            return false;
        }

        // The way a pillar's shadow opens from a blast at from, or null for a pillar on the blast.
        static Vec2? ShadeOf(BossProp pillar, Vec2 from)
        {
            var way = pillar.Position - from;
            return way.LengthSquared < 1e-4f ? (Vec2?)null : way.Normalized;
        }

        static bool InShade(Vec2 pillar, Vec2 away, Vec2 at)
        {
            var offset = at - pillar;
            float distance = offset.Length;
            if (distance > ShadeLength) return false;
            return distance < 1e-4f || Vec2.Dot(offset / distance, away) >= MathF.Cos(ShadeAngle * 0.5f * MathF.PI / 180f);
        }

        // For the playtests: the boss stands stunned now, as after running into a pillar; its veins are broken.
        internal void ForceStun(Boss boss)
        {
            if (boss.IsDead || boss.State == BossState.Dormant) return;
            boss.Attack = null;
            boss.RepeatsLeft = 0;
            boss.RepeatNext = false;
            boss.State = BossState.Stun;
            boss.StateTicks = Ticks(StunSeconds);
        }

        internal void ForceBreakVeins(Boss boss) => _props.RemoveAll(p => p.Owner == boss.Id && p.Kind == PropKind.Vein);

        // ---- Veins ----

        // What a breakable thing standing in the arena takes to break (a vein: a share of its boss's health).
        float PropHealthFor(Boss boss, PropKind kind)
        {
            float share = kind switch
            {
                PropKind.Vein => VeinShare,
                PropKind.Crystal => CrystalShare,
                PropKind.Egg => EggShare,
                PropKind.Banner => BannerShare,
                _ => 0,
            };
            return share > 0 ? Math.Max(1, boss.MaxHealth * share) : 0;
        }

        // A player's blow on a vein: it takes the hit (no armor) and breaks at none left.
        void StrikeProp(BossProp prop, float damage)
        {
            if (!prop.Breakable || prop.Health <= 0) return;
            prop.Health -= damage;
            if (prop.Health <= 0)
            {
                _props.Remove(prop);
                _events.Add(new SimEvent(SimEventType.Impact, prop.Owner, amount: 3, position: prop.Position));
                // A banner broken outside the interlude leaves its boss staggering (R4a).
                if (prop.Kind == PropKind.Banner && !prop.Interlude) BannerBroken(prop);
            }
        }

        // Every breakable thing touching a circle takes the hit (a skill's burst, a shot).
        void HitProps(Vec2 centre, float radius, float damage)
        {
            foreach (var prop in _props.Where(p => p.Breakable && Vec2.Distance(p.Position, centre) <= radius + p.Radius).ToList()) StrikeProp(prop, damage);
        }

        // A swing: every breakable thing in its arc (its damage rolled only when one is there, so nothing else rolls
        // differently in a fight without one).
        bool SwingProps(Player player, float reach, float arc, Func<float> damage)
        {
            bool hit = false;
            foreach (var prop in _props.Where(p => p.Breakable && Combat.InArc(player.Position, player.Facing, p.Position, p.Radius, reach, arc)).ToList())
            {
                StrikeProp(prop, damage());
                hit = true;
            }
            return hit;
        }

        public bool TouchesBreakable(Vec2 at, float radius) => _props.Any(p => p.Breakable && Vec2.Distance(p.Position, at) <= radius + p.Radius);

        // The vein a boss would run to: its nearest.
        BossProp VeinFor(Boss boss) => PropsOf(boss, PropKind.Vein).OrderBy(v => Vec2.Distance(v.Position, boss.Position)).FirstOrDefault();

        // Its eating run begins: the way to the vein is marked (no harm), and it runs there through the telegraph.
        void StartEating(Boss boss, BossAttack attack, long resolve, int cause)
        {
            var vein = VeinFor(boss);
            boss.EatVein = vein?.Id ?? 0;
            if (vein == null) return;
            var way = vein.Position - boss.Position;
            if (way.LengthSquared > 1e-6f) boss.Facing = way.Normalized;
            AddHazard(boss, cause, HazardShape.Line, boss.Position, boss.Facing, Math.Max(0.5f, way.Length), boss.Definition.Radius * 2, 0, resolve);
        }

        // Every tick of its run: on toward the vein, to reach it as the telegraph ends.
        void RunToVein(Boss boss)
        {
            var vein = _props.FirstOrDefault(p => p.Id == boss.EatVein);
            if (vein == null) return;
            var way = vein.Position - boss.Position;
            float gap = way.Length - vein.Radius - boss.Definition.Radius;
            if (gap <= 0) return;
            float step = Math.Max(boss.Definition.Speed * TickSeconds, gap / Math.Max(1, boss.StateTicks));
            MoveBoss(boss, way.Normalized * Math.Min(step, gap));
        }

        // The run is over: the vein still there and in reach, it is eaten and the boss rages.
        void Eat(Boss boss)
        {
            var vein = _props.FirstOrDefault(p => p.Id == boss.EatVein);
            boss.EatVein = 0;
            if (vein == null || Vec2.Distance(vein.Position, boss.Position) > vein.Radius + boss.Definition.Radius + EatReach) return;
            _props.Remove(vein);
            _events.Add(new SimEvent(SimEventType.Impact, boss.Id, amount: 4, position: vein.Position));
            Enrage(boss);
        }

        // ---- The wall run (AttackShape.Orbit) ----

        // Where it comes out of the wall next: the first time ahead of where it dug in, then spread degrees on each time.
        void NextOrbit(Boss boss, BossAttack attack, bool first)
        {
            var fromHome = boss.Position - boss.Home;
            if (first) boss.OrbitAngle = fromHome.LengthSquared > 1e-4f ? fromHome.Angle + attack.Spread * MathF.PI / 180f : 0;
            else boss.OrbitAngle += attack.Spread * MathF.PI / 180f;
            float reach = Math.Max(0, FloorRadius(boss) - boss.Definition.Radius - 0.3f);
            boss.LandAt = LandingFor(boss, boss.Home + Vec2.FromAngle(boss.OrbitAngle) * reach);
        }

        // Out of the wall: it turns to the nearest and throws its rocks fanned at them.
        void ComeOutOfWall(Boss boss, BossAttack attack)
        {
            boss.Position = boss.LandAt;
            var fighters = Fighters(boss);
            if (fighters.Count > 0)
            {
                var toward = PickTarget(boss, AttackTarget.Nearest, fighters).Position - boss.Position;
                if (toward.LengthSquared > 1e-6f) boss.Facing = toward.Normalized;
            }
            _events.Add(new SimEvent(SimEventType.Impact, boss.Id, amount: 4));
            Burst(boss, attack);
        }
    }
}
