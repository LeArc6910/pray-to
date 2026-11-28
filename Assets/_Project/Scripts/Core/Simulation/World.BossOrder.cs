using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // ---- R4a (2026-11-28, Docs/BossRedesign.md 7.1 T8·T9 and 5.3·5.5, Docs/Bosses.md "순서·막간"): the crystal queen's
    // crystals that light and burst in order, her crown's light that turns at them, her egg interlude; Morg's banner
    // (broken, he staggers), his marked volley, his volley of fire arrows band after band, his legion interlude. The
    // sequence (Order, Bands) and the interlude are for every boss: the wraith and the void lord (R4b) use them too. ----
    public sealed partial class World
    {
        // Seconds between one crystal's burst and the next (the lights between are the attack's spread), and between
        // the last light and the first burst.
        public const float OrderBurstGap = 0.5f;
        public const float OrderPause = 0.4f;
        // How far round from its target crystals rise for an order with none standing.
        const float OrderRaiseReach = 2.6f;
        // A beam turns this many degrees where it meets a crystal, at most this many times.
        public const float BeamTurn = 90;
        public const int BeamBends = 2;
        // A marked circle stops following its player this long before it lands: the moment to walk out.
        public const float MarkLock = 0.8f;
        // A broken banner leaves its boss staggering this long (as stunned: World.StunTakenShare).
        public const float BannerStunSeconds = 1.5f;
        // What share of its boss's health a crystal, an egg and a banner take to break.
        public const float CrystalShare = 0.015f;
        public const float EggShare = 0.02f;
        public const float BannerShare = 0.04f;
        // How long the legion waits before its first charge.
        const float LegionFirst = 1.5f;

        // ---- Things an attack raises (pool pillar, crystal, banner) ----

        // A thing rises at a spot for seconds, unless the boss or another of its kind is in the way. Too many of the
        // boss's things standing and its oldest raised one of that kind goes first.
        BossProp RaiseProp(Boss boss, PropKind kind, Vec2 at, float seconds)
        {
            float radius = PropRadius(kind);
            var fromHome = at - boss.Home;
            float limit = Math.Max(0, FloorRadius(boss) - radius);
            if (fromHome.Length > limit) at = boss.Home + fromHome.Normalized * limit;
            at = Collision.Resolve(Map, at, radius);
            if (Vec2.Distance(at, boss.Position) < radius + boss.Definition.Radius + 0.2f) return null;
            if (PropsOf(boss, kind).Any(p => Vec2.Distance(p.Position, at) < p.Radius + radius)) return null;
            var mine = _props.Where(p => p.Owner == boss.Id).ToList();
            if (mine.Count >= BossTerrainSheet.MaxProps)
            {
                var oldest = mine.Where(p => p.Kind == kind).OrderBy(p => p.Until < 0 ? 0 : 1).ThenBy(p => p.Id).FirstOrDefault();
                if (oldest == null) return null;
                _props.Remove(oldest);
            }
            var prop = new BossProp(_nextId++, boss.Id, kind, at, radius, PropHealthFor(boss, kind), Tick + Ticks(seconds));
            _props.Add(prop);
            return prop;
        }

        // ---- Crystals in order (AttackShape.Order) ----

        // The crystals light one by one through the wind-up and burst in that order after it: its telegraph is long
        // enough for every light and a breath (World.StartTelegraph).
        public static float OrderTelegraph(BossAttack attack) => Math.Max(attack.Telegraph, attack.Count * attack.Spread + OrderPause);

        void LayOrder(Boss boss, BossAttack attack, Player target, long resolve, int cause)
        {
            var roll = new GameRandom(_forageSeed + (ulong)Tick, stream: 4000 + (ulong)boss.Id);
            var spots = PropsOf(boss, PropKind.Crystal).Select(p => p.Position).ToList();
            if (spots.Count == 0)
            {
                // None standing: count rise round the target for the wind-up (and a little after).
                float turn = roll.NextFloat() * 2 * MathF.PI;
                float seconds = OrderTelegraph(attack) + attack.Count * OrderBurstGap + 2;
                for (int i = 0; i < attack.Count; i++)
                {
                    var crystal = RaiseProp(boss, PropKind.Crystal, target.Position + Vec2.FromAngle(turn + i * 2 * MathF.PI / attack.Count) * OrderRaiseReach, seconds);
                    if (crystal != null) spots.Add(crystal.Position);
                }
                if (spots.Count == 0) spots.Add(target.Position);
            }
            // The order is rolled from a stream of its own, so nothing else rolls differently for it.
            for (int i = spots.Count - 1; i > 0; i--)
            {
                int j = (int)(roll.NextUInt() % (uint)(i + 1));
                (spots[i], spots[j]) = (spots[j], spots[i]);
            }
            int n = Math.Min(attack.Count, spots.Count);
            for (int i = 0; i < n; i++)
                AddHazard(boss, cause, HazardShape.Circle, spots[i], boss.Facing, attack.Radius, 0, attack.Damage, resolve + Ticks(OrderBurstGap * i)).Order = i + 1;
        }

        // ---- The crown's light (AttackShape.Beam) ----

        void LayBeam(Boss boss, BossAttack attack, Vec2 facing, long resolve, int cause)
        {
            int beams = Math.Max(1, attack.Count);
            for (int b = 0; b < beams; b++)
            {
                var way = Vec2.FromAngle(facing.Angle + (b - (beams - 1) / 2f) * attack.Angle * MathF.PI / 180f);
                var from = boss.Position;
                float left = attack.Length;
                BossProp skip = null;
                for (int bends = 0; left > 0.1f; bends++)
                {
                    var hit = bends >= BeamBends ? default : PropsOf(boss, PropKind.Crystal)
                        .Where(p => p != skip)
                        .Select(p => (Prop: p, Along: Vec2.Dot(p.Position - from, way)))
                        .Where(c => c.Along > 0.3f && c.Along < left && MathF.Abs(Vec2.Dot(c.Prop.Position - from, new Vec2(-way.Y, way.X))) <= c.Prop.Radius)
                        .OrderBy(c => c.Along).FirstOrDefault();
                    if (hit.Prop == null)
                    {
                        AddHazard(boss, cause, HazardShape.Line, from, way, left, attack.Width, attack.Damage, resolve);
                        break;
                    }
                    AddHazard(boss, cause, HazardShape.Line, from, way, hit.Along, attack.Width, attack.Damage, resolve);
                    from = from + way * hit.Along;
                    left -= hit.Along;
                    skip = hit.Prop;
                    // It turns toward the arena's middle.
                    var toMiddle = boss.Home - from;
                    float turn = BeamTurn * MathF.PI / 180f;
                    var left90 = Vec2.FromAngle(way.Angle + turn);
                    var right90 = Vec2.FromAngle(way.Angle - turn);
                    way = Vec2.Dot(left90, toMiddle) >= Vec2.Dot(right90, toMiddle) ? left90 : right90;
                }
            }
        }

        // ---- The marked volley (AttackShape.Mark) ----

        void LayMarks(Boss boss, BossAttack attack, Player target, List<Player> fighters, long resolve, int cause)
        {
            var marked = new List<Player> { target };
            var rest = fighters.Where(p => p != target).ToList();
            while (marked.Count < attack.Count && rest.Count > 0)
            {
                var next = rest[_random.Next(rest.Count)];
                rest.Remove(next);
                marked.Add(next);
            }
            foreach (var player in marked)
            {
                var mark = AddHazard(boss, cause, HazardShape.Circle, player.Position, boss.Facing, attack.Radius, 0, attack.Damage, resolve);
                mark.Follows = player.Id;
                mark.LockTick = resolve - Ticks(MarkLock);
            }
        }

        // A marked circle keeps over its player until it locks.
        void FollowMark(Hazard hazard)
        {
            if (hazard.Follows == 0 || Tick >= hazard.LockTick) return;
            var player = FindPlayer(hazard.Follows);
            if (player != null && !player.IsDead) hazard.Position = player.Position;
        }

        // ---- The volley of bands (AttackShape.Bands) ----

        // The arena cut across into count bands, the first on the boss's side, landing spread seconds one after another.
        void LayBands(Boss boss, BossAttack attack, long resolve, int cause)
        {
            var off = boss.Home - boss.Position;
            var way = off.LengthSquared > 1 ? off.Normalized : boss.Facing;
            var side = new Vec2(-way.Y, way.X);
            float reach = boss.ArenaRadius + 1;
            float band = reach * 2 / attack.Count;
            for (int i = 0; i < attack.Count; i++)
            {
                var middle = boss.Home + way * (-reach + band * (i + 0.5f));
                AddHazard(boss, cause, HazardShape.Line, middle - side * reach, side, reach * 2, band, attack.Damage, resolve + Ticks(attack.Spread * i)).Order = i + 1;
            }
        }

        // Ticks until the last of an attack's sequence lands (its rest covers them, so no other comes on top).
        long SequenceLeft(Boss boss, int cause) =>
            _hazards.Where(h => h.Source == boss.Id && h.Cause == cause && h.Order > 0).Select(h => h.ResolveTick - Tick).DefaultIfEmpty(0).Max();

        // ---- Banners ----

        // Where hellfire falls with a banner standing (AttackTarget.Banner): its nearest banner.
        Vec2? BannerSpot(Boss boss, Vec2 near) => PropsOf(boss, PropKind.Banner).OrderBy(p => Vec2.Distance(p.Position, near)).Select(p => (Vec2?)p.Position).FirstOrDefault();

        // A banner broken: in the interlude, the interlude ends (WorkInterlude); else the boss staggers.
        void BannerBroken(BossProp banner)
        {
            var boss = FindBoss(banner.Owner);
            if (boss == null || boss.IsDead || boss.Invulnerable || boss.State == BossState.Dormant) return;
            boss.Attack = null;
            boss.RepeatsLeft = 0;
            boss.RepeatNext = false;
            boss.ChargesLeft = 0;
            boss.Cues = false;
            boss.State = BossState.Stun;
            boss.StateTicks = Ticks(BannerStunSeconds);
            _events.Add(new SimEvent(SimEventType.Impact, boss.Id, amount: 5));
        }

        // A charge that runs into a crystal of its own: the crystal shatters and the charge stops there (no stun).
        BossProp CrystalHit(Boss boss) => PropsOf(boss, PropKind.Crystal)
            .FirstOrDefault(p => Vec2.Distance(p.Position, boss.Position) <= p.Radius + boss.Definition.Radius);

        // ---- The interlude (BossInterlude, T9) ----

        public static bool InInterlude(Boss boss) => boss.State == BossState.Interlude;

        void StartInterlude(Boss boss, List<Player> fighters)
        {
            var interlude = boss.Definition.Interlude;
            boss.InterludeNext = false;
            boss.Attack = null;
            boss.RepeatsLeft = 0;
            boss.RepeatNext = false;
            boss.State = BossState.Interlude;
            boss.StateTicks = Ticks(interlude.Seconds);
            // What attacks raised goes, so the interlude's own fit beside what stands for the phase.
            _props.RemoveAll(p => p.Owner == boss.Id && p.Until >= 0);
            float floor = FloorRadius(boss);
            var crowd = fighters.Count > 0 ? fighters.Aggregate(Vec2.Zero, (sum, p) => sum + p.Position) / fighters.Count : boss.Home;
            var back = (boss.Home - crowd).LengthSquared > 0.25f ? (boss.Home - crowd).Normalized : boss.Facing * -1;
            if (interlude.Kind == InterludeKind.Eggs)
            {
                // The queen hangs over the middle; her eggs lie evenly round it.
                boss.Position = boss.Home;
                float turn = new GameRandom(_forageSeed + (ulong)Tick, stream: 5000 + (ulong)boss.Id).NextFloat() * 2 * MathF.PI;
                for (int i = 0; i < interlude.Count; i++)
                {
                    var at = Collision.Resolve(Map, boss.Home + Vec2.FromAngle(turn + i * 2 * MathF.PI / interlude.Count) * (floor * 0.6f), PropRadius(PropKind.Egg));
                    AddInterludeProp(boss, PropKind.Egg, at);
                }
            }
            else
            {
                // Morg stands back behind his banner; his legion charges.
                boss.Position = boss.Home + back * Math.Max(0, floor * 0.75f - boss.Definition.Radius);
                boss.Facing = back * -1;
                AddInterludeProp(boss, PropKind.Banner, Collision.Resolve(Map, boss.Home + back * (floor * 0.4f), PropRadius(PropKind.Banner)));
                boss.ChargesToCome = interlude.Count;
                boss.ChargeTicks = Ticks(LegionFirst);
            }
            _events.Add(new SimEvent(SimEventType.Cue, boss.Id, amount: CueInterlude, direction: boss.Facing));
        }

        // The Cue's Amount as an interlude starts and as it ends.
        public const int CueInterlude = 2;
        public const int CueInterludeOver = 3;

        void AddInterludeProp(Boss boss, PropKind kind, Vec2 at)
        {
            var mine = _props.Where(p => p.Owner == boss.Id).ToList();
            if (mine.Count >= BossTerrainSheet.MaxProps) _props.Remove(mine.OrderBy(p => p.Id).First());
            // It stands as long as the interlude (Until -1 is "for the phase"; the interlude takes it away at its end).
            _props.Add(new BossProp(_nextId++, boss.Id, kind, at, PropRadius(kind), PropHealthFor(boss, kind)) { Interlude = true });
        }

        void WorkInterlude(Boss boss, List<Player> fighters)
        {
            var interlude = boss.Definition.Interlude;
            if (interlude.Kind == InterludeKind.Legion && boss.ChargesToCome > 0 && --boss.ChargeTicks <= 0 && fighters.Count > 0)
            {
                // The ranks as the phase before had them: the interlude is its own test, not the last phase's hardest.
                var charge = interlude.Charge.ForPhase(Math.Max(1, interlude.Phase - 1));
                var target = fighters[_random.Next(fighters.Count)];
                LayWaves(boss, charge, target, Tick + Ticks(charge.Telegraph), Cause(boss, charge));
                boss.ChargesToCome--;
                boss.ChargeTicks = Ticks(Math.Max(1, (interlude.Seconds - LegionFirst - 2) / Math.Max(1, interlude.Count - 1)));
            }
            var task = interlude.Kind == InterludeKind.Eggs ? PropKind.Egg : PropKind.Banner;
            bool done = !_props.Any(p => p.Owner == boss.Id && p.Interlude && p.Kind == task);
            if (done || --boss.StateTicks <= 0) EndInterlude(boss, fighters);
        }

        void EndInterlude(Boss boss, List<Player> fighters)
        {
            var interlude = boss.Definition.Interlude;
            foreach (var egg in _props.Where(p => p.Owner == boss.Id && p.Interlude && p.Kind == PropKind.Egg).ToList())
            {
                // An egg left whole hatches.
                if (interlude.Hatch != null)
                {
                    var at = Collision.Resolve(Map, egg.Position, interlude.Hatch.Radius);
                    if (!Collision.Overlaps(Map, at, interlude.Hatch.Radius)) SpawnEnemy(interlude.Hatch, at).FromBoss = true;
                }
                _events.Add(new SimEvent(SimEventType.Impact, boss.Id, amount: 2, position: egg.Position));
            }
            _props.RemoveAll(p => p.Owner == boss.Id && p.Interlude);
            // The legion's charges not yet come do not come.
            _hazards.RemoveAll(h => h.Source == boss.Id && (h.Shape == HazardShape.Wave && Tick < h.StartTick || h.Safe));
            boss.ChargesToCome = 0;
            _events.Add(new SimEvent(SimEventType.Cue, boss.Id, amount: CueInterludeOver, direction: boss.Facing));
            if (fighters.Count > 0) ChooseAttack(boss, fighters);
            else
            {
                boss.State = BossState.Recover;
                boss.StateTicks = 1;
            }
        }

        // For the playtests: the interlude starts now (the boss put in its phase first).
        internal bool ForceInterlude(Boss boss)
        {
            var interlude = boss.Definition.Interlude;
            var fighters = Fighters(boss);
            if (interlude == null || boss.IsDead || boss.State == BossState.Dormant || fighters.Count == 0) return false;
            boss.Phase = Math.Max(boss.Phase, interlude.Phase);
            ClearAttacksFrom(boss);
            PlaceProps(boss);
            StartInterlude(boss, fighters);
            return true;
        }
    }
}
