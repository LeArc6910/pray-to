using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // What a weapon art event says happened (SimEventType.Art, Amount = WeaponArtKind + phase * 16).
    public enum ArtPhase : byte
    {
        // Begun: a guard taken, a charge or an aim begun, a hammer lifted, a boomerang thrown, an orb's well laid.
        Start,
        // The blow itself.
        Strike,
        // A sword's guard took a blow (the counter follows as a Strike).
        Blocked,
        // Over without a blow: a guard that took nothing, an art cut off by a swap or a fall.
        End,
    }

    // A weapon art going on (World.WeaponArts.cs): which, how old, where it aims, and the weapon's numbers as it began.
    public sealed class ActiveArt
    {
        internal ActiveArt(WeaponArt art, PlayerStats stats, BasicAttack attack, Vec2 aim, Vec2 centre)
        {
            Art = art;
            Stats = stats;
            Attack = attack;
            Aim = aim;
            Centre = centre;
        }

        public WeaponArt Art { get; }
        public WeaponArtKind Kind => Art.Kind;
        internal PlayerStats Stats { get; }
        internal BasicAttack Attack { get; }
        internal Vec2 Aim { get; set; }
        // An orb's well.
        internal Vec2 Centre { get; }
        public int Age { get; internal set; }
        // How many of its hits or shots are out.
        internal int Done { get; set; }
        // Whether it keeps the player from basic attacks and other arts (all but an orb's well, which goes on alone).
        public bool Busy => Kind != WeaponArtKind.Gravity;
    }

    // Weapon arts (Docs/Weapons.md 묶음 C): one for each weapon kind, from weapon_kinds.csv, used with that weapon in
    // hand. No mana; a cooldown for each kind apart (Player.ArtReady). Damage is the weapon's own hit (its PlayerStats)
    // times the art's percent, with the weapon's tags; the hits carry ArtCause.
    public sealed partial class World
    {
        // The Cause of an art's hits (0 is the basic attack, 1..4 the skill slots, ElementalCause and up the elements).
        public const int ArtCause = 50;
        // How hard a staff's wave pushes, times a swing's.
        const float WavePush = 3;
        // How far past an orb's burst it draws monsters, and how fast (tiles a second).
        const float WellPull = 0.75f;
        const float WellPullSpeed = 3.5f;
        // How fast a bow's jump back goes, tiles a second (it is over in one step, like a dash).
        const float ArtStep = 0.25f;

        // The art of the weapon in hand, or null (a pickaxe, fists, or a kind with none).
        public static WeaponArt ArtOf(Player player) => player.Stats.Kind?.Art;

        // Ticks until the kind's art can be used again (0 when ready).
        public int ArtTicksLeft(Player player, WeaponKind kind) =>
            kind == null || !player.ArtReady.TryGetValue(kind.Id, out long ready) ? 0 : (int)Math.Max(0, ready - Tick);

        // Uses the art of the weapon in hand toward aim (the way the player faces when zero). False when it cannot:
        // dead, riding, rolling, no art, still cooling down, another art busy, or (a dagger) nobody ahead to stab.
        public bool UseWeaponArt(int playerId, Vec2 aim)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || player.Riding || player.Rolling) return false;
            var kind = player.Stats.Kind;
            var art = kind?.Art;
            if (art == null || ArtTicksLeft(player, kind) > 0 || player.Art?.Busy == true) return false;
            if (aim.IsFinite && aim.LengthSquared > 1e-6f) player.Facing = aim.Normalized;
            var facing = player.Facing;
            var stats = player.Stats;
            if (art.Kind == WeaponArtKind.Vital && VitalTarget(player, art) == null) return false;

            player.ArtReady[kind.Id] = Tick + Ticks(art.Cooldown);
            var centre = art.Kind == WeaponArtKind.Gravity ? Reach(player.Position, facing, art.Reach) : player.Position;
            var active = new ActiveArt(art, stats, stats.Attack, facing, centre);
            switch (art.Kind)
            {
                case WeaponArtKind.Vital:
                    Stab(player, active);
                    return true;
                case WeaponArtKind.Dash:
                    Rush(player, active);
                    return true;
                case WeaponArtKind.Backstep:
                    Backstep(player, active);
                    return true;
                case WeaponArtKind.Wave:
                    ArtEvent(player, active, ArtPhase.Strike, player.Position);
                    ArtStrike(player, active, (at, radius) => Combat.InArc(player.Position, facing, at, radius, art.Reach, art.Arc), art.Power, player.Position, push: WavePush);
                    return true;
            }
            // The rest go on over ticks (UpdateArt).
            player.Art = active;
            ArtEvent(player, active, ArtPhase.Start, centre);
            return true;
        }

        // Lets go of a held art (a greatsword's charge): it cleaves now, as gathered so far. False when nothing is held.
        public bool ReleaseWeaponArt(int playerId, Vec2 aim)
        {
            var player = FindPlayer(playerId);
            var active = player?.Art;
            if (active == null || !active.Art.Holds) return false;
            if (aim.IsFinite && aim.LengthSquared > 1e-6f) player.Facing = active.Aim = aim.Normalized;
            Cleave(player, active);
            return true;
        }

        // Cuts off a busy art (a swap, a fall); an orb's well goes on.
        void CancelArt(Player player)
        {
            if (player.Art == null || !player.Art.Busy) return;
            ArtEvent(player, player.Art, ArtPhase.End, player.Position);
            player.Art = null;
        }

        void UpdateArt(Player player)
        {
            var active = player.Art;
            if (active == null) return;
            if (player.IsDead)
            {
                player.Art = null;
                return;
            }
            active.Age++;
            var art = active.Art;
            int time = Math.Max(1, Ticks(art.Time));
            switch (art.Kind)
            {
                case WeaponArtKind.Parry:
                    if (active.Age >= time) Finish(player, ArtPhase.End);
                    break;
                case WeaponArtKind.Charge:
                    if (active.Age >= time) Cleave(player, active);
                    break;
                case WeaponArtKind.Slam:
                    if (active.Age < time) break;
                    ArtEvent(player, active, ArtPhase.Strike, player.Position);
                    ArtStrike(player, active, (at, radius) => Vec2.Distance(at, player.Position) <= art.Radius + radius, art.Power, player.Position, stun: art.Stun, line: false);
                    player.Art = null;
                    break;
                case WeaponArtKind.Pierce:
                    if (active.Age < time) break;
                    var end = Reach(player.Position, active.Aim, art.Reach);
                    ArtEvent(player, active, ArtPhase.Strike, end);
                    var from = player.Position;
                    ArtStrike(player, active, (at, radius) => Combat.DistanceToSegment(at, from, end) <= art.Radius + radius, art.Power, from, line: false);
                    player.Art = null;
                    break;
                case WeaponArtKind.Whirl:
                    // Count blows, evenly over Time, each a share of the whole.
                    while (active.Done < art.Count && active.Age >= (active.Done + 1) * time / art.Count)
                    {
                        active.Done++;
                        ArtEvent(player, active, ArtPhase.Strike, player.Position);
                        ArtStrike(player, active, (at, radius) => Vec2.Distance(at, player.Position) <= art.Radius + radius, art.Power / art.Count, player.Position, push: 0, line: false);
                    }
                    if (active.Done >= art.Count) player.Art = null;
                    break;
                case WeaponArtKind.Barrage:
                    while (active.Done < art.Count && active.Age >= active.Done * time / art.Count)
                    {
                        // Fanned out and back: the middle, then each side wider in turn.
                        int i = active.Done++;
                        float side = art.Count <= 1 ? 0 : (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) / (float)((art.Count + 1) / 2);
                        ArtShot(player, active, Rotate(active.Aim, side * art.Arc / 2 * MathF.PI / 180), art.Power);
                    }
                    if (active.Done >= art.Count) player.Art = null;
                    break;
                case WeaponArtKind.Gravity:
                    if (active.Age < time)
                    {
                        Draw(active.Centre, art.Radius + WellPull);
                        break;
                    }
                    ArtEvent(player, active, ArtPhase.Strike, active.Centre);
                    ArtStrike(player, active, (at, radius) => Vec2.Distance(at, active.Centre) <= art.Radius + radius, art.Power, active.Centre, line: false);
                    player.Art = null;
                    break;
                default:
                    player.Art = null;
                    break;
            }
        }

        void Finish(Player player, ArtPhase phase)
        {
            ArtEvent(player, player.Art, phase, player.Position);
            player.Art = null;
        }

        // A sword's guard: the blow is turned aside, the guard ends, and the sword answers round the player. Called from
        // HurtPlayer for a blow (not a burn); true when it was turned.
        bool Parries(Player player)
        {
            var active = player.Art;
            if (active == null || active.Kind != WeaponArtKind.Parry) return false;
            var art = active.Art;
            ArtEvent(player, active, ArtPhase.Blocked, player.Position);
            ArtEvent(player, active, ArtPhase.Strike, player.Position);
            var facing = active.Aim;
            ArtStrike(player, active, (at, radius) => Combat.InArc(player.Position, facing, at, radius, art.Reach, art.Arc), art.Power, player.Position);
            player.Art = null;
            return true;
        }

        void Cleave(Player player, ActiveArt active)
        {
            var art = active.Art;
            float gathered = Math.Min(1, active.Age / (float)Math.Max(1, Ticks(art.Time)));
            float power = art.Low + (art.Power - art.Low) * gathered;
            var facing = active.Aim;
            ArtEvent(player, active, ArtPhase.Strike, player.Position);
            ArtStrike(player, active, (at, radius) => Combat.InArc(player.Position, facing, at, radius, art.Reach, art.Arc), power, player.Position, push: 1 + gathered);
            player.Art = null;
        }

        // The nearest monster or boss ahead within the dagger's reach, not behind rock.
        object VitalTarget(Player player, WeaponArt art)
        {
            object best = null;
            float nearest = float.MaxValue;
            foreach (var enemy in _enemies)
            {
                if (enemy.IsDead || enemy.Away || !Combat.InArc(player.Position, player.Facing, enemy.Position, enemy.Definition.Radius, art.Reach, art.Arc)) continue;
                float d = Vec2.Distance(enemy.Position, player.Position);
                if (d < nearest && ClearLine(player.Position, enemy.Position, ThinLine)) (best, nearest) = (enemy, d);
            }
            foreach (var boss in _bosses)
            {
                if (boss.IsDead || !Combat.InArc(player.Position, player.Facing, boss.Position, boss.Definition.Radius, art.Reach, art.Arc)) continue;
                float d = Vec2.Distance(boss.Position, player.Position);
                if (d < nearest && ClearLine(player.Position, boss.Position, ThinLine)) (best, nearest) = (boss, d);
            }
            return best;
        }

        void Stab(Player player, ActiveArt active)
        {
            var art = active.Art;
            var target = VitalTarget(player, art);
            var stats = active.Stats;
            float more = art.Power / 100f;
            float damage = stats.Damage * stats.CritDamage / 100f * more;
            var at = target is Enemy e ? e.Position : target is Boss b ? b.Position : player.Position;
            ArtEvent(player, active, ArtPhase.Strike, at);
            if (target is Enemy enemy)
            {
                HitEnemy(player, enemy, damage, true, player.Position, stats.AttackTags, ArtCause);
                AddElements(player, enemy, null, true, more);
            }
            else if (target is Boss boss)
            {
                if (boss.State == BossState.Dormant) Wake(boss);
                HitBoss(player, boss, damage, true, stats.AttackTags, ArtCause);
                AddElements(player, null, boss, true, more);
            }
        }

        // A spear's rush: on the way it faces, stopped by walls, striking once everything within Radius of the way.
        void Rush(Player player, ActiveArt active)
        {
            var art = active.Art;
            var start = player.Position;
            ArtEvent(player, active, ArtPhase.Start, start);
            var doors = DoorsFor(player);
            var hit = new HashSet<int>();
            for (float gone = 0; gone < art.Reach; gone += ArtStep)
            {
                var next = Collision.Move(Map, player.Position, PlayerRadius, active.Aim * Math.Min(ArtStep, art.Reach - gone), passable: doors);
                if (Vec2.Distance(next, player.Position) < 1e-4f) break;
                player.Position = next;
                var here = player.Position;
                ArtStrike(player, active, (at, radius) => Vec2.Distance(at, here) <= art.Radius + radius, art.Power, here, skip: hit, line: false);
            }
            ArtEvent(player, active, ArtPhase.Strike, player.Position);
            if (Vec2.Distance(start, player.Position) > 1e-4f) player.Corrections++;
        }

        // A bow's jump back, then its arrows, fanned.
        void Backstep(Player player, ActiveArt active)
        {
            var art = active.Art;
            var start = player.Position;
            var doors = DoorsFor(player);
            for (float gone = 0; gone < art.Reach; gone += ArtStep)
            {
                var next = Collision.Move(Map, player.Position, PlayerRadius, -active.Aim * Math.Min(ArtStep, art.Reach - gone), passable: doors);
                if (Vec2.Distance(next, player.Position) < 1e-4f) break;
                player.Position = next;
            }
            if (Vec2.Distance(start, player.Position) > 1e-4f) player.Corrections++;
            ArtEvent(player, active, ArtPhase.Strike, player.Position);
            for (int i = 0; i < art.Count; i++)
            {
                float degrees = (i - (art.Count - 1) / 2f) * art.Arc;
                ArtShot(player, active, Rotate(active.Aim, degrees * MathF.PI / 180), art.Power);
            }
        }

        // One of the weapon's own shots (its speed, range, look; an arrow's if it is no shooting weapon), at power.
        void ArtShot(Player player, ActiveArt active, Vec2 direction, float power)
        {
            var shot = active.Attack.Shoots ? active.Attack : BasicAttack.Arrow;
            var stats = active.Stats;
            float more = power / 100f;
            float damage = RollDamage(stats, out bool critical) * more;
            _projectiles.Add(new Projectile(_nextId++, player.Id, ArtCause - 1, player.Position + direction * PlayerRadius, direction * shot.Speed, shot.Radius, damage,
                Ticks(shot.Range / shot.Speed), friendly: true, tags: stats.AttackTags, critical: critical, more: more)
            {
                Burst = shot.Burst,
                Look = shot.Look,
            });
        }

        // Everything inside (by its position and radius) takes the weapon's hit times power, but those in skip (hit ones
        // join it); not through rock unless line is false. push 0 knocks nobody back (a boomerang going round).
        void ArtStrike(Player player, ActiveArt active, Func<Vec2, float, bool> inside, float power, Vec2 from, float push = 1, float stun = 0,
            HashSet<int> skip = null, bool line = true)
        {
            var stats = active.Stats;
            float more = power / 100f;
            foreach (var enemy in _enemies.ToList())
            {
                if (enemy.IsDead || enemy.Away || !inside(enemy.Position, enemy.Definition.Radius)) continue;
                if (line && !ClearLine(from, enemy.Position, ThinLine)) continue;
                if (skip != null && !skip.Add(enemy.Id)) continue;
                float damage = RollDamage(stats, out bool critical) * more;
                HitEnemy(player, enemy, damage, critical, from, stats.AttackTags, ArtCause, knockback: push > 0, push: push);
                AddElements(player, enemy, null, critical, more);
                if (stun > 0 && !enemy.IsDead) Daze(enemy, stun);
            }
            foreach (var boss in _bosses)
            {
                if (boss.IsDead || !inside(boss.Position, boss.Definition.Radius)) continue;
                if (line && !ClearLine(from, boss.Position, ThinLine)) continue;
                if (skip != null && !skip.Add(boss.Id)) continue;
                if (boss.State == BossState.Dormant) Wake(boss);
                if (boss.Invulnerable) continue;
                HitBoss(player, boss, RollDamage(stats, out bool critical) * more, critical, stats.AttackTags, ArtCause);
                AddElements(player, null, boss, critical, more);
            }
        }

        // An orb's well draws the monsters within radius toward it (not bosses, nor one under the ground).
        void Draw(Vec2 centre, float radius)
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.IsDead || enemy.Away) continue;
                var toward = centre - enemy.Position;
                float distance = toward.Length;
                if (distance > radius + enemy.Definition.Radius || distance < 0.15f) continue;
                float step = Math.Min(distance - 0.1f, WellPullSpeed * TickSeconds);
                enemy.Position = Collision.Move(Map, enemy.Position, enemy.Definition.Radius, toward / distance * step, monster: true);
            }
        }

        // From the position the way given up to length tiles, stopping short of the first wall.
        Vec2 Reach(Vec2 position, Vec2 direction, float length)
        {
            var at = position;
            for (float gone = 0; gone < length; gone += ArtStep)
            {
                var next = at + direction * Math.Min(ArtStep, length - gone);
                if (Collision.Overlaps(Map, next, 0.1f)) break;
                at = next;
            }
            return at;
        }

        void ArtEvent(Player player, ActiveArt active, ArtPhase phase, Vec2 at) =>
            _events.Add(new SimEvent(SimEventType.Art, player.Id, amount: (int)active.Kind + (int)phase * 16, direction: active.Aim, position: at));

        public static WeaponArtKind ArtKindOf(int amount) => (WeaponArtKind)(amount % 16);
        public static ArtPhase ArtPhaseOf(int amount) => (ArtPhase)(amount / 16);
    }
}
