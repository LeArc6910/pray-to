using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // Ordinary monsters' attacks (2026-11-21, Docs/WorldRework.md 2b, Docs/Combat.md 몬스터의 공격 시트). A monster with
    // rows in monster_attacks.csv picks among them the way a boss does: by weight among those whose range, cooldown and
    // conditions fit, then telegraph, strike, rest. Everything that can hurt is a Hazard or a Projectile of the boss
    // engine (World.Boss.cs), so bots and players read them the same way. New to the engine, and usable by anything
    // that lays hazards or fires projectiles: a shot that comes off walls, a shot that comes back, a blink, a circle
    // that drags, a trap that goes off when stepped on, a mending line and a buff on other monsters. Monsters with no
    // row fight the old way (World.UpdateEnemy).
    public sealed partial class World
    {
        // At most this many marks on the ground aimed at one player at a time, and this many swarm strikes winding up
        // on them; the rest hold off (Docs/WorldRework.md 전조와 이펙트, 공격 순번).
        public const int MaxMarksPerTarget = 4;
        public const int MaxStrikesPerTarget = 3;
        // Of a pack that swoops, this many at a time on one player (Docs/MonsterPatterns.md 박쥐).
        public const int MaxSwoopsPerTarget = 2;
        // One that turns slowly (MonsterTraitKind.Turn) only attacks a target within this many degrees of its front.
        public const float TurnAim = 35;
        // A sting's poison (HitEffect.Venom) deals this share of the blow again over its seconds.
        public const float PoisonShare = 0.5f;
        // How far out one circles with no reach given (MonsterTraitKind.Orbit), and how long one that lives under the
        // ground stays up after an attack with no time given (MonsterTraitKind.Burrowed).
        public const float OrbitReach = 3;
        public const float SurfaceSeconds = 2.5f;
        // The longest one that lives under the ground goes after someone unseen before it comes up anyway.
        public const float BurrowedMostSeconds = 6;
        // A hit on a guarding monster's front does this share of its damage; a dazed one takes this much more.
        public const float GuardShare = 0.3f;
        public const float StaggerSeconds = 1.5f;
        public const float StaggerMore = 1.5f;
        // What a split leaves: this share of the health and of the hits.
        public const float SplitHealth = 0.3f;
        public const float SplitPower = 0.5f;
        // The last moment of a leap's telegraph is the hop itself.
        public const float HopSeconds = 0.2f;
        // Seconds between a sweep's wedges, a lob's circles and a volley's shots, and how far a sweep's wedges turn to
        // either side (a share of their angle).
        public const float SweepSeconds = 0.4f;
        public const float SweepTurn = 0.35f;
        public const float LobSeconds = 0.4f;
        public const float VolleySeconds = 0.25f;
        // A returning shot waits this long where it turns.
        public const float TurnPause = 0.25f;
        // A monster keeps at most this many pools and this many traps down; the oldest goes.
        public const int MaxMonsterPools = 4;
        public const int MaxMines = 4;
        // A fire pool burns for this share of the blow that left it a second; a trail's pool is this wide.
        public const float FirePoolShare = 0.25f;
        public const float TrailRadius = 0.4f;
        // A trail's pool burns or bites as a blow of this share of its monster's damage would have left it.
        public const float TrailShare = 0.5f;
        // Where a monster fell: the flash before its shards, the shards' pace and reach; the cloud's size, time and fill.
        public const float DeathburstSeconds = 0.5f;
        public const float DeathburstSpeed = 8;
        public const float DeathburstReach = 3;
        public const float DeathcloudRadius = 1.8f;
        public const float DeathcloudSeconds = 3;
        public const float DeathcloudFill = 7;
        // How far round it a monster counts its allies (monster_attacks.csv when allies>=N).
        public const float AllyReach = 8;

        // What a MonsterFx event's Amount says happened.
        public const int MonsterFxBounce = 1;
        public const int MonsterFxBlink = 2;
        public const int MonsterFxSplit = 3;
        public const int MonsterFxStagger = 4;
        public const int MonsterFxEmpower = 5;
        public const int MonsterFxDeathburst = 6;
        public const int MonsterFxMine = 7;
        public const int MonsterFxTurn = 8;
        // One vanished to come out behind its target; a deathburst put out by the element that quenches it; one that
        // lives under the ground or lay in wait came up; one went back under.
        public const int MonsterFxVanish = 9;
        public const int MonsterFxQuench = 10;
        public const int MonsterFxSurface = 11;
        public const int MonsterFxDive = 12;
        // A bomber went off (it leaves the view with its blast, so the screen is told where; Target is its attack's
        // place in the sheet).
        public const int MonsterFxExplode = 13;

        // ---- The wire (Docs/WorldRework.md 통신): a monster's state byte carries its EnemyState in the low three bits,
        // the Tell of the attack it winds up above them, then whether it is empowered and whether it is a small one. Its
        // phase field carries the rift mark in two bits and above it the attack it is on (its place in the sheet, from
        // 1), as a boss's does, so the screen draws the warning of an attack that starts at the monster from the sheet
        // and no hazard is sent for it. ----
        public const int EnemyStateMask = 7;
        public const int EnemyTellShift = 3;
        // In the tell's bits of one that is not winding up: it is under the ground (Enemy.Buried), drawn as a mound.
        public const int EnemyBuriedTell = 7;
        public const int EnemyEmpoweredBit = 64;
        public const int EnemySmallBit = 128;
        public const int EnemyRiftMask = 3;
        public const int EnemyAttackShift = 2;

        public static byte EnemyStateByte(Enemy enemy) =>
            (byte)((int)enemy.State
                | (enemy.State == EnemyState.Windup && enemy.Attack != null ? (int)enemy.Attack.Tell << EnemyTellShift
                    : enemy.Buried ? EnemyBuriedTell << EnemyTellShift : 0)
                | (enemy.Empowered ? EnemyEmpoweredBit : 0)
                | (enemy.Small ? EnemySmallBit : 0));

        public static int EnemyPhaseField(Enemy enemy) =>
            (enemy.Rift & EnemyRiftMask) | (enemy.Attack != null && enemy.State != EnemyState.Idle && enemy.State != EnemyState.Chase ? enemy.Attack.Index << EnemyAttackShift : 0);

        readonly List<(MonsterDefinition Definition, Vec2 Position, Enemy From, bool Small)> _queuedMonsters =
            new List<(MonsterDefinition Definition, Vec2 Position, Enemy From, bool Small)>();
        readonly List<(long Tick, Vec2 Position, int Source, int Count, float Damage, bool Chills)> _deathbursts =
            new List<(long Tick, Vec2 Position, int Source, int Count, float Damage, bool Chills)>();

        // An elite leader's big attack hits BigHitShare harder again (World.Elites.cs).
        static int DamageOf(Enemy enemy, MonsterAttack attack) =>
            Math.Max(1, (int)MathF.Round(enemy.Damage * attack.Damage / 100f * (IsBigAttack(enemy.Elite, enemy.Definition, attack) ? BigHitShare : 1)));

        // Every tick, whichever way the monster fights: a buff runs down and a trail is left.
        void TickMonster(Enemy enemy)
        {
            if (enemy.EmpowerTicks > 0) enemy.EmpowerTicks--;
            // What the spore clouds gave it, and the slimes a slime mother sheds (World.BiomeMonsters.cs).
            TellCloudHeal(enemy);
            Shed(enemy);
            // A dormant mid-boss that has come to fight (World.IceSand.cs).
            WakeDormant(enemy);
            bool buried = (enemy.State == EnemyState.Idle || enemy.State == EnemyState.Chase)
                && (enemy.Definition.Has(MonsterTraitKind.Burrowed) || WaitsInAmbush(enemy)) && Tick >= enemy.UpUntil;
            // One that lives under the ground and has gone after someone so long with no attack that fits (a big worm
            // whose target stands where its body cannot come up) comes up for a while all the same, to be struck
            // (2026-11-24, B2: the --dungeon bot stood beside a great worm it could never reach).
            if (buried && enemy.Buried && enemy.State == EnemyState.Chase && enemy.Definition.Has(MonsterTraitKind.Burrowed))
            {
                if (++enemy.UnderTicks > Ticks(BurrowedMostSeconds))
                {
                    enemy.UnderTicks = 0;
                    enemy.UpUntil = Tick + Ticks(enemy.Definition.Trait(MonsterTraitKind.Burrowed).Number(0, SurfaceSeconds));
                    buried = false;
                    _events.Add(new SimEvent(SimEventType.MonsterFx, enemy.Id, amount: MonsterFxSurface, position: enemy.Position));
                }
            }
            else enemy.UnderTicks = 0;
            if (buried != enemy.Buried)
            {
                enemy.Buried = buried;
                enemy.Knockback = Vec2.Zero;
                // Coming up for an attack shows by the attack itself; going back under gets its own puff.
                if (buried && Tick > 0) _events.Add(new SimEvent(SimEventType.MonsterFx, enemy.Id, amount: MonsterFxDive, position: enemy.Position));
            }
            var trail = enemy.Definition.Trait(MonsterTraitKind.Trail);
            // An elite with the fire affix leaves fire where it walks while it fights (World.Elites.cs).
            bool burning = trail == null && (enemy.Affixes & RiftAffix.Fire) != 0 && enemy.State != EnemyState.Idle && !enemy.Returning;
            if (trail == null && !burning || enemy.Away) return;
            if (++enemy.TrailTicks == 1) enemy.TrailAt = enemy.Position;
            if (Vec2.Distance(enemy.TrailAt, enemy.Position) < TrailRadius * 2) return;
            enemy.TrailAt = enemy.Position;
            var (kind, seconds) = burning ? (MonsterPool.Fire, FireSeconds) : MonsterAttackSheet.Pool(trail.Arguments[0] + ":" + trail.Arguments[1]);
            LayPool(enemy.Id, 0, kind, enemy.Position, TrailRadius, seconds, enemy.Damage * TrailShare);
        }

        // Monsters that came out of a split or a call join the world between ticks (a blow that fells one may land
        // while the monsters are being gone through), and the shards of the fallen fly when their flash is over.
        void UpdateMonsterExtras()
        {
            foreach (var (definition, position, from, small) in _queuedMonsters)
            {
                var spot = Collision.Resolve(Map, position, definition.Radius, monster: true);
                if (Collision.Overlaps(Map, spot, definition.Radius, monster: true)) continue;
                var enemy = SpawnEnemy(definition, spot);
                if (from == null) continue;
                enemy.Rift = from.Rift;
                enemy.ZoneLevels = from.ZoneLevels;
                enemy.SpeedScale = from.SpeedScale;
                enemy.Power = small ? from.Power * SplitPower : definition != from.Definition ? 1 + ZonePowerStep * from.ZoneLevels : from.Power;
                // Of another kind (a big slime's halves): that kind's own health, as its zone makes it.
                enemy.MaxHealth = enemy.Health = small ? Math.Max(1, from.MaxHealth * SplitHealth)
                    : definition != from.Definition ? definition.Health * (1 + ZoneHealthStep * from.ZoneLevels)
                    : from.MaxHealth;
                enemy.Small = small;
                // A big one's halves stay of its pack (its share of the reward, its room), one more than the roster.
                if (!small && definition != from.Definition && from.PackId != 0)
                {
                    enemy.PackId = from.PackId;
                    enemy.Home = spot;
                }
                // And its room's leash, when they are big too (a slime king's big slimes).
                enemy.BoundCenter = from.BoundCenter;
                enemy.BoundRadius = from.BoundRadius;
                enemy.TargetId = from.TargetId;
                if (FindPlayer(from.TargetId) != null) enemy.State = EnemyState.Chase;
            }
            _queuedMonsters.Clear();

            for (int i = _deathbursts.Count - 1; i >= 0; i--)
            {
                var burst = _deathbursts[i];
                if (Tick < burst.Tick) continue;
                _deathbursts.RemoveAt(i);
                float start = _random.NextFloat() * 2 * MathF.PI;
                for (int k = 0; k < burst.Count; k++)
                {
                    var direction = Vec2.FromAngle(start + 2 * MathF.PI * k / burst.Count);
                    _projectiles.Add(new Projectile(_nextId++, burst.Source, 0, burst.Position + direction * 0.2f, direction * DeathburstSpeed, MonsterShotRadius,
                        burst.Damage, Ticks(DeathburstReach / DeathburstSpeed))
                    {
                        // Its monster is gone by now, so the shard carries the cold itself.
                        Effect = burst.Chills ? HitEffect.Chill : HitEffect.None,
                        EffectAmount = PlayerChillSeconds,
                    });
                }
                _events.Add(new SimEvent(SimEventType.MonsterFx, burst.Source, amount: MonsterFxDeathburst, position: burst.Position));
            }
        }

        // What a monster's traits do when it falls (called once for every death, before its drops).
        void MonsterDied(Enemy enemy)
        {
            var definition = enemy.Definition;
            var split = definition.Trait(MonsterTraitKind.Split);
            // split:n:kind (2026-11-24, a big slime): n of that smaller kind, whole, wherever it falls. Plain split:n: n
            // small ones of its own, not in the first zone: the first minutes stay simple (Docs/MonsterPatterns.md 슬라임).
            var into = split != null && split.Arguments.Count > 1 ? _monsters.Monsters.FirstOrDefault(m => m.Id == split.Arguments[1]) : null;
            if (split != null && !enemy.Small && (into != null || enemy.ZoneLevels > 0 || enemy.Rift != 0))
            {
                int count = (int)split.Number(0, 2);
                var across = new Vec2(-enemy.Facing.Y, enemy.Facing.X);
                // Out of its body: the halves stand where its edge was.
                float apart = into != null ? Math.Max(0.5f, definition.Radius) : 0.5f;
                for (int i = 0; i < count; i++)
                {
                    float side = count == 1 ? 0 : -1 + 2 * i / (float)(count - 1);
                    _queuedMonsters.Add((into ?? definition, enemy.Position + across * (side * apart), enemy, into == null));
                }
                _events.Add(new SimEvent(SimEventType.MonsterFx, enemy.Id, amount: MonsterFxSplit, position: enemy.Position));
            }
            var burst = definition.Trait(MonsterTraitKind.Deathburst);
            // Felled by the element that quenches it (an ice slime melted by fire), it leaves no shards.
            bool quenched = burst != null && burst.Arguments.Count > 2 && Enum.TryParse(burst.Arguments[2], true, out SkillTag quench) && (enemy.LastTags & quench) != 0;
            if (quenched && !enemy.Small) _events.Add(new SimEvent(SimEventType.MonsterFx, enemy.Id, amount: MonsterFxQuench, position: enemy.Position));
            if (burst != null && !enemy.Small && !quenched)
            {
                int ticks = Ticks(DeathburstSeconds);
                // The flash: a mark that harms nothing, so it is seen and a bot steps off it.
                _hazards.Add(new Hazard(_nextId++, 0, 0, HazardShape.Circle, enemy.Position, enemy.Facing, 0.4f, 0, 0, Tick, Tick + ticks) { Mark = Hazard.DeathFlash });
                _deathbursts.Add((Tick + ticks, enemy.Position, enemy.Id, (int)burst.Number(0, 4), enemy.Damage * burst.Number(1, 50) / 100f,
                    definition.Element == SkillTag.Cold));
            }
            var cloud = definition.Trait(MonsterTraitKind.Deathcloud);
            // A puffball that went off left its blast's cloud already; felled first, it bursts into its own.
            if (cloud != null && !enemy.Small && !enemy.BlewUp)
            {
                _hazards.Add(new Hazard(_nextId++, 0, 0, HazardShape.Balm, enemy.Position, enemy.Facing, cloud.Number(0, DeathcloudRadius), cloud.Number(2, DeathcloudFill), 0,
                    Tick, Tick + Ticks(cloud.Number(1, DeathcloudSeconds))));
            }
        }

        // How much of a blow from there a monster takes: little on its guard (which is up except while it rests after
        // an attack or is dazed), more while it is dazed. A mender or a
        // caller struck while it works is stopped.
        float TakenShare(Enemy enemy, Vec2 from)
        {
            if (enemy.State == EnemyState.Channel || enemy.State == EnemyState.Windup && enemy.Attack != null && enemy.Attack.Tell == Tell.Call)
                EndAttack(enemy, broken: true);
            if (enemy.State == EnemyState.Stagger) return StaggerMore;
            // Shoulder to shoulder with its kind, and near a guarding elite leader (World.EliteGuard).
            float share = (1 - AuraGuard(enemy)) * (1 - EliteGuard(enemy));
            var guard = enemy.Definition.Trait(MonsterTraitKind.Guard);
            // Its guard is down while it rests after an attack: the opening for whoever got out of its way.
            if (guard == null || enemy.State == EnemyState.Recover) return share;
            var toward = from - enemy.Position;
            if (toward.LengthSquared < 1e-6f) return share;
            if (Vec2.Dot(toward.Normalized, enemy.Facing.Normalized) < MathF.Cos(guard.Number(0, 120) * 0.5f * MathF.PI / 180f)) return share;
            _events.Add(new SimEvent(SimEventType.Guarded, enemy.Id));
            return share * (1 - guard.Number(1, (1 - GuardShare) * 100) / 100f);
        }

        // The share of a blow a monster's aura turns: with another of its kind within the aura's reach, both take that
        // much less (aura:guard:reach:percent). 0 for one that stands alone.
        float AuraGuard(Enemy enemy) => AuraMate(enemy) != null ? enemy.Definition.Trait(MonsterTraitKind.Aura).Number(2, 0) / 100f : 0;

        // The nearest other of its kind inside its aura, or null.
        public Enemy AuraMate(Enemy enemy)
        {
            var aura = enemy.Definition.Trait(MonsterTraitKind.Aura);
            if (aura == null || enemy.IsDead) return null;
            float reach = aura.Number(1, 1.5f);
            Enemy mate = null;
            foreach (var other in _enemies)
            {
                if (other == enemy || other.IsDead || other.Definition != enemy.Definition) continue;
                float distance = Vec2.Distance(other.Position, enemy.Position);
                if (distance > reach) continue;
                reach = distance;
                mate = other;
            }
            return mate;
        }

        // One with an ambush it has not made yet lies under the ground until someone comes within the ambush's range.
        static MonsterAttack AmbushOf(Enemy enemy)
        {
            foreach (var attack in enemy.Definition.Attacks)
                if (attack.Shape == MonsterAttackShape.Ambush && (enemy.Used == null || !enemy.Used[attack.Index])) return attack;
            return null;
        }

        bool WaitsInAmbush(Enemy enemy) => enemy.State == EnemyState.Idle && !enemy.Small && enemy.Rift == 0 && AmbushOf(enemy) != null;

        // Idle and lying in wait: it comes up under the first player inside its ambush's range, and not before.
        void LieInWait(Enemy enemy)
        {
            var ambush = AmbushOf(enemy);
            var near = _players
                .Where(p => !p.IsDead && Gap(enemy, p) <= ambush.RangeMax && !InSafePlace(p.Position) && OpenToMonsters(enemy.Position, p.Position, enemy.Definition.Radius))
                .OrderBy(p => Vec2.Distance(p.Position, enemy.Position))
                .FirstOrDefault();
            if (near == null) return;
            enemy.TargetId = near.Id;
            Ready(enemy, ambush);
            StartAttack(enemy, ambush, near);
            Noticed(enemy, near);
        }

        // Turns a facing toward another by no more than the given degrees.
        static Vec2 TurnToward(Vec2 facing, Vec2 wanted, float degrees)
        {
            float from = facing.Angle, to = wanted.Angle;
            float delta = to - from;
            while (delta > MathF.PI) delta -= 2 * MathF.PI;
            while (delta < -MathF.PI) delta += 2 * MathF.PI;
            float most = degrees * MathF.PI / 180f;
            return MathF.Abs(delta) <= most ? wanted : Vec2.FromAngle(from + MathF.Sign(delta) * most);
        }

        static bool InFront(Enemy enemy, Vec2 toward, float degrees) =>
            toward.LengthSquared < 1e-6f || Vec2.Dot(toward.Normalized, enemy.Facing.Normalized) >= MathF.Cos(degrees * MathF.PI / 180f);

        // ---- Picking ----

        bool Ready(Enemy enemy, MonsterAttack attack)
        {
            if (enemy.ReadyAt == null)
            {
                enemy.ReadyAt = new long[MonsterAttackSheet.MaxAttacks + 1];
                enemy.Used = new bool[MonsterAttackSheet.MaxAttacks + 1];
            }
            return Tick >= enemy.ReadyAt[attack.Index] && !(attack.Once && enemy.Used[attack.Index]);
        }

        int AlliesOf(Enemy enemy) => _enemies.Count(e => e != enemy && !e.IsDead && Vec2.Distance(e.Position, enemy.Position) <= AllyReach);

        // The most hurt other monster within reach, for a mend.
        Enemy HurtAlly(Enemy enemy, float reach) => _enemies
            .Where(e => e != enemy && !e.IsDead && e.Health < e.MaxHealth * 0.95f && Vec2.Distance(e.Position, enemy.Position) <= reach)
            .OrderBy(e => e.Health / e.MaxHealth)
            .FirstOrDefault();

        bool Fits(Enemy enemy, MonsterAttack attack, Player target, float gap)
        {
            if (!Ready(enemy, attack)) return false;
            // Standing on its target (a leap lands there) is as near as can be, not out of range.
            gap = Math.Max(0, gap);
            if (gap < attack.RangeMin || gap > attack.RangeMax) return false;
            if (attack.HealthBelow > 0 && enemy.Health * 100 >= enemy.MaxHealth * attack.HealthBelow) return false;
            if (attack.AlliesAtLeast > 0 && AlliesOf(enemy) < attack.AlliesAtLeast) return false;
            switch (attack.Shape)
            {
                case MonsterAttackShape.Mend:
                    return HurtAlly(enemy, attack.Length) != null;
                case MonsterAttackShape.Empower:
                    return _enemies.Any(e => e != enemy && !e.IsDead && !e.Empowered && Vec2.Distance(e.Position, enemy.Position) <= attack.Radius);
                case MonsterAttackShape.Blink:
                case MonsterAttackShape.Summon:
                    return true;
            }
            if (attack.Target != MonsterAttackTarget.Self && !ClearLine(enemy.Position, target.Position, attack.Tell == Tell.Dash ? enemy.Definition.Radius : ThinLine))
                return false;
            // Its body goes there: not through what keeps monsters out (a door, a fence).
            if ((attack.Shape == MonsterAttackShape.Leap || attack.Shape == MonsterAttackShape.Burrow || attack.Shape == MonsterAttackShape.Ambush)
                && !OpenToMonsters(enemy.Position, target.Position, enemy.Definition.Radius)) return false;
            // One that fears the light does not land in it (World.BiomeMonsters.cs).
            if ((attack.Shape == MonsterAttackShape.Leap || attack.Shape == MonsterAttackShape.Burrow || attack.Shape == MonsterAttackShape.Ambush)
                && Shuns(enemy, target.Position)) return false;
            // A big body lands only where it fits, inside its room's leash (World.Sizes.cs).
            if ((attack.Shape == MonsterAttackShape.Leap || attack.Shape == MonsterAttackShape.Burrow || attack.Shape == MonsterAttackShape.Ambush)
                && enemy.Definition.Big
                && (!Clearance.Fits(target.Position, enemy.Definition.Radius) || !InBound(enemy, target.Position))) return false;
            // Too many warnings on one player are not read: the others wait their turn.
            if (attack.MarksGround && _enemies.Count(e => e != enemy && e.State == EnemyState.Windup && e.Attack != null && e.Attack.MarksGround && e.TargetId == target.Id)
                >= MaxMarksPerTarget) return false;
            // Circles that fall one after another count each: those lying round the player now, those still to come
            // and this attack's own may not pass the cap either (three wisps' icicles were six circles at once).
            if (attack.Shape == MonsterAttackShape.Lob || attack.Shape == MonsterAttackShape.Delayed)
            {
                int lying = _hazards.Count(h => h.Mark != 0 && !h.Attached && !h.Lingers && h.Damage > 0 && h.Shape == HazardShape.Circle
                    && Vec2.Distance(h.Position, target.Position) <= h.Size + 2);
                int coming = _enemies.Where(e => e != enemy && !e.IsDead && e.TargetId == target.Id && e.Attack != null
                    && (e.Attack.Shape == MonsterAttackShape.Lob || e.Attack.Shape == MonsterAttackShape.Delayed)).Sum(e => e.RepeatsLeft);
                if (lying + coming + attack.Count > MaxMarksPerTarget) return false;
            }
            if (attack.Shape == MonsterAttackShape.Strike
                && _enemies.Count(e => e != enemy && e.State == EnemyState.Windup && e.Attack?.Shape == MonsterAttackShape.Strike && e.TargetId == target.Id) >= MaxStrikesPerTarget)
                return false;
            if (attack.Shape == MonsterAttackShape.Swoop && _enemies.Count(e => e != enemy && e.Attack?.Shape == MonsterAttackShape.Swoop && e.TargetId == target.Id
                && (e.State == EnemyState.Windup || e.State == EnemyState.Charge)) >= MaxSwoopsPerTarget) return false;
            return true;
        }

        // How close it comes before it stops: the nearest reach among the attacks it could make now (or, with all of
        // them resting, among them all), but never nearer than a shooter's keep_distance.
        float EngageGap(Enemy enemy)
        {
            float ready = float.PositiveInfinity, any = float.PositiveInfinity;
            foreach (var attack in enemy.Definition.Attacks)
            {
                if (attack.Target == MonsterAttackTarget.Self || attack.Target == MonsterAttackTarget.Ally || attack.Shape == MonsterAttackShape.Blink
                    || float.IsInfinity(attack.RangeMax)) continue;
                any = Math.Min(any, attack.RangeMax);
                if (Ready(enemy, attack)) ready = Math.Min(ready, attack.RangeMax);
            }
            float gap = float.IsInfinity(ready) ? any : ready;
            if (float.IsInfinity(gap)) gap = enemy.Definition.Reach;
            return Math.Max(enemy.Definition.KeepDistance, gap * 0.9f);
        }

        // ---- The states of a monster that fights from the sheet (Idle is the same for all, in World.UpdateEnemy) ----

        void UpdateFighter(Enemy enemy, Player target)
        {
            var definition = enemy.Definition;
            switch (enemy.State)
            {
                case EnemyState.Chase:
                    enemy.Attack = null;
                    if (target == null || target.IsDead || Vec2.Distance(target.Position, enemy.Position) > definition.Aggro * LoseInterest || InSafePlace(target.Position))
                    {
                        enemy.State = EnemyState.Idle;
                        break;
                    }
                    var toTarget = target.Position - enemy.Position;
                    float gap = Gap(enemy, target);
                    // One that turns slowly attacks only what is in front of it; whoever walks round it has an opening.
                    var turn = definition.Trait(MonsterTraitKind.Turn);
                    bool facingIt = turn == null || InFront(enemy, toTarget, TurnAim);
                    // An elite never blows itself up (a fire bat's dive bomb): it is the fight, not a bomb.
                    var choices = definition.Attacks.Where(a => (facingIt || a.Target != MonsterAttackTarget.Nearest || a.Shape == MonsterAttackShape.Burrow)
                        && (enemy.Elite == EliteRank.None || a.Shape != MonsterAttackShape.Explode) && Fits(enemy, a, target, gap)).ToList();
                    if (choices.Count > 0 && StartAttack(enemy, _random.PickWeighted(choices, a => a.Weight), target)) break;

                    var facing = toTarget.LengthSquared > 1e-6f ? toTarget.Normalized : enemy.Facing;
                    if (turn != null)
                    {
                        // It comes on sideways if it must, always turning its front to its target.
                        facing = TurnToward(enemy.Facing, facing, turn.Number(0, 120) * TickSeconds);
                        if (gap > EngageGap(enemy) || !ClearLine(enemy.Position, target.Position, ThinLine))
                        {
                            var way = gap < 1.5f ? toTarget.Normalized : WayFor(enemy);
                            if (way.LengthSquared < 1e-6f) way = definition.Big ? Vec2.Zero : toTarget.Normalized;
                            enemy.Position = Collision.Move(Map, enemy.Position, definition.Radius, way * (enemy.Speed * TickSeconds), monster: true);
                        }
                        enemy.Facing = facing;
                        break;
                    }
                    var orbit = definition.Trait(MonsterTraitKind.Orbit);
                    if (orbit != null && gap <= orbit.Number(0, OrbitReach) + 2 && ClearLine(enemy.Position, target.Position, ThinLine))
                    {
                        Circle(enemy, target, orbit.Number(0, OrbitReach));
                        break;
                    }
                    if (definition.Has(MonsterTraitKind.Rooted))
                    {
                        enemy.Facing = facing;
                        break;
                    }
                    if (definition.KeepDistance > 0 && gap < definition.KeepDistance)
                    {
                        // A shooter backs away from a player who comes too close.
                        enemy.Position = Collision.Move(Map, enemy.Position, definition.Radius, -facing * (enemy.Speed * TickSeconds), monster: true);
                        enemy.Facing = facing;
                        break;
                    }
                    if (gap <= EngageGap(enemy) && ClearLine(enemy.Position, target.Position, ThinLine))
                    {
                        enemy.Facing = facing;
                        break;
                    }
                    if (enemy.Rift == DefenseMonster && gap >= 1.5f
                        && _flow.DistanceAt(TileMap.Cell(enemy.Position.X), TileMap.Cell(enemy.Position.Y)) == FlowField.Unreached)
                    {
                        enemy.State = EnemyState.Idle;
                        break;
                    }
                    var direction = gap < 1.5f ? facing : WayFor(enemy);
                    if (direction.LengthSquared < 1e-6f)
                    {
                        // A big body with no way that fits it (a narrow mouth, the edge of its room) waits there, facing
                        // its target, and uses what reaches from there.
                        if (definition.Big)
                        {
                            enemy.Facing = facing;
                            break;
                        }
                        direction = facing;
                    }
                    enemy.Facing = direction;
                    enemy.Position = Collision.Move(Map, enemy.Position, definition.Radius, direction * (enemy.Speed * TickSeconds), monster: true);
                    break;

                case EnemyState.Windup:
                    WindUp(enemy, target);
                    if (--enemy.StateTicks <= 0) Unleash(enemy, target);
                    break;

                case EnemyState.Charge:
                    Rush(enemy);
                    break;

                case EnemyState.Channel:
                    var ally = FindEnemy(enemy.TetherId);
                    if (ally == null || ally.IsDead || ally.Health >= ally.MaxHealth || Vec2.Distance(ally.Position, enemy.Position) > enemy.Attack.Length * 1.5f
                        || --enemy.StateTicks <= 0)
                    {
                        EndAttack(enemy, broken: true);
                        break;
                    }
                    ally.Health = Math.Min(ally.MaxHealth, ally.Health + ally.MaxHealth * enemy.Attack.Damage / 100f * TickSeconds);
                    var toAlly = ally.Position - enemy.Position;
                    if (toAlly.LengthSquared > 1e-6f) enemy.Facing = toAlly.Normalized;
                    break;

                case EnemyState.Stagger:
                    if (--enemy.StateTicks <= 0) Rest(enemy);
                    break;

                case EnemyState.Recover:
                    Repeat(enemy, target);
                    if (--enemy.StateTicks <= 0) enemy.State = EnemyState.Chase;
                    break;
            }
        }

        // One that never stands (MonsterTraitKind.Orbit): round its target at that reach, turning back where rock stops it.
        void Circle(Enemy enemy, Player target, float reach)
        {
            if (enemy.OrbitSide == 0) enemy.OrbitSide = (enemy.Id & 1) == 0 ? 1 : -1;
            var offset = enemy.Position - target.Position;
            float distance = offset.Length;
            var outward = distance > 1e-4f ? offset / distance : -enemy.Facing;
            var along = new Vec2(-outward.Y, outward.X) * enemy.OrbitSide;
            // Along the circle, and in or out toward its reach.
            float want = reach + enemy.Definition.Radius + PlayerRadius;
            var direction = (along + outward * Math.Clamp(want - distance, -1, 1)).Normalized;
            var before = enemy.Position;
            float step = enemy.Speed * TickSeconds;
            enemy.Position = Collision.Move(Map, enemy.Position, enemy.Definition.Radius, direction * step, monster: true);
            if (Vec2.Distance(before, enemy.Position) < step * 0.3f) enemy.OrbitSide = -enemy.OrbitSide;
            enemy.Facing = direction;
        }

        // For the playtests: monsters that are doing nothing stay so, whoever comes near; one made to attack still does.
        internal bool MonstersHold { get; set; }

        // For the playtests and tests: the monster starts that attack's telegraph now, on the nearest player.
        internal bool ForceAttack(Enemy enemy, string attackId)
        {
            var attack = enemy.Definition.Attacks.FirstOrDefault(a => a.Id == attackId);
            var target = _players.Where(p => !p.IsDead).OrderBy(p => Vec2.Distance(p.Position, enemy.Position)).FirstOrDefault();
            if (attack == null || target == null || enemy.IsDead) return false;
            _hazards.RemoveAll(h => h.Source == enemy.Id && !h.Lingers && h.Shape != HazardShape.Mine);
            enemy.TargetId = target.Id;
            Ready(enemy, attack);
            return StartAttack(enemy, attack, target);
        }

        Hazard Lay(Enemy enemy, MonsterAttack attack, HazardShape shape, Vec2 position, Vec2 direction, float size, float width, int damage, long resolve)
        {
            var hazard = new Hazard(_nextId++, enemy.Id, attack.Index, shape, position, direction, size, width, damage, Tick, resolve)
            {
                Attached = attack.Attached,
                Mark = (byte)((int)attack.Shape + 1),
                Effect = attack.Effect,
                EffectAmount = attack.EffectAmount,
                Leaves = attack.Pool,
                LeavesSeconds = attack.Linger,
                LeavesRadius = attack.PoolRadius,
            };
            _hazards.Add(hazard);
            return hazard;
        }

        // Faces the target and lays the warning. False when the attack cannot be made from here (a blink with nowhere
        // to go): it is put off a moment.
        bool StartAttack(Enemy enemy, MonsterAttack attack, Player target, bool again = false)
        {
            var definition = enemy.Definition;
            var aim = target.Position - enemy.Position;
            var facing = aim.LengthSquared > 1e-6f ? aim.Normalized : enemy.Facing;
            // An elite leader's big attack is warned longer (World.TelegraphOf).
            float telegraph = TelegraphOf(enemy, attack);
            int ticks = Ticks(again ? telegraph * AgainTelegraph : telegraph);
            long resolve = Tick + ticks;
            int damage = DamageOf(enemy, attack);

            if (attack.Shape == MonsterAttackShape.Blink)
            {
                var spot = attack.Behind ? SpotBehind(enemy, target, attack.Length) : BlinkSpot(enemy, target, attack.Length);
                if (spot == null)
                {
                    enemy.ReadyAt[attack.Index] = Tick + Ticks(1);
                    return false;
                }
                enemy.LandAt = spot.Value;
                // Where it will be: a mark that harms nothing.
                Lay(enemy, attack, HazardShape.Circle, enemy.LandAt, facing, definition.Radius + 0.25f, 0, 0, resolve);
                if (attack.Behind) _events.Add(new SimEvent(SimEventType.MonsterFx, enemy.Id, amount: MonsterFxVanish, position: enemy.Position));
            }
            // One that was under the ground comes up to make it (a burrow comes up where it lands, by itself).
            if (enemy.Buried && attack.Shape != MonsterAttackShape.Burrow && attack.Shape != MonsterAttackShape.Ambush)
                _events.Add(new SimEvent(SimEventType.MonsterFx, enemy.Id, amount: MonsterFxSurface, position: enemy.Position));
            enemy.Buried = false;

            enemy.Attack = attack;
            enemy.Facing = facing;
            enemy.State = EnemyState.Windup;
            enemy.StateTicks = ticks;
            if (!again) enemy.RepeatsLeft = 0;
            enemy.Used[attack.Index] = true;

            // The marks that start at its middle reach from a big body's edge (MonsterAttackSheet.MarkOf).
            var (_, reach, width) = MonsterAttackSheet.MarkOf(definition, attack);
            switch (attack.Shape)
            {
                case MonsterAttackShape.Strike:
                    Lay(enemy, attack, HazardShape.Cone, enemy.Position, facing, reach, width, damage, resolve);
                    break;
                case MonsterAttackShape.Sweep:
                    for (int i = 0; i < attack.Count; i++)
                    {
                        float turn = attack.Count == 1 ? 0 : (i % 2 == 0 ? -1 : 1) * attack.Angle * SweepTurn * MathF.PI / 180f;
                        Lay(enemy, attack, HazardShape.Cone, enemy.Position, Vec2.FromAngle(facing.Angle + turn), reach, width, damage, resolve + i * Ticks(SweepSeconds));
                    }
                    break;
                case MonsterAttackShape.Thrust:
                case MonsterAttackShape.Beam:
                    Lay(enemy, attack, HazardShape.Line, enemy.Position, facing, reach, width, damage, resolve);
                    break;
                case MonsterAttackShape.Slam:
                case MonsterAttackShape.Explode:
                    Lay(enemy, attack, HazardShape.Circle, enemy.Position, facing, reach, 0, damage, resolve);
                    break;
                case MonsterAttackShape.Nova:
                    if (attack.Length > 0) Lay(enemy, attack, HazardShape.Ring, enemy.Position, facing, reach, width, damage, resolve);
                    else Lay(enemy, attack, HazardShape.Circle, enemy.Position, facing, reach, 0, damage, resolve);
                    break;
                case MonsterAttackShape.Charge:
                case MonsterAttackShape.Swoop:
                    enemy.ChargeDirection = facing;
                    if (!again) enemy.RepeatsLeft = attack.Shape == MonsterAttackShape.Charge ? attack.Count - 1 : 0;
                    Lay(enemy, attack, HazardShape.Line, enemy.Position, facing, reach, width, 0, resolve);
                    break;
                case MonsterAttackShape.Leap:
                    // Onto where the target stands, but no further than the attack reaches.
                    float far = float.IsInfinity(attack.RangeMax) ? definition.Aggro : attack.RangeMax + definition.Radius + PlayerRadius + 0.5f;
                    var landing = aim.Length > far ? enemy.Position + facing * far : target.Position;
                    enemy.LeapFrom = enemy.Position;
                    enemy.LandAt = Collision.Resolve(Map, landing, definition.Radius, monster: true);
                    Lay(enemy, attack, HazardShape.Circle, enemy.LandAt, facing, attack.Radius, 0, damage, resolve);
                    break;
                case MonsterAttackShape.Burrow:
                case MonsterAttackShape.Ambush:
                    enemy.LandAt = Collision.Resolve(Map, target.Position, definition.Radius, monster: true);
                    Lay(enemy, attack, HazardShape.Circle, enemy.LandAt, facing, attack.Radius, 0, damage, resolve);
                    break;
                case MonsterAttackShape.Lob:
                case MonsterAttackShape.Delayed:
                    Lay(enemy, attack, HazardShape.Circle, target.Position, facing, attack.Radius, 0, damage, resolve);
                    enemy.RepeatsLeft = attack.Count - 1;
                    enemy.RepeatTicks = Ticks(LobSeconds);
                    break;
                case MonsterAttackShape.Pull:
                    Lay(enemy, attack, HazardShape.Pull, target.Position, facing, attack.Radius, attack.Speed, damage, resolve);
                    break;
                case MonsterAttackShape.Push:
                    // The circles walk out from the monster toward where the target stood, one after another.
                    float step = attack.Length / attack.Count;
                    float edge = MonsterSizes.Beyond(definition.Radius);
                    for (int i = 0; i < attack.Count; i++)
                        Lay(enemy, attack, HazardShape.Circle, enemy.Position + facing * (edge + step * (i + 1)), facing, attack.Radius, 0, damage, resolve + Ticks(step * i / attack.Speed));
                    break;
                case MonsterAttackShape.Mine:
                    var mines = _hazards.Where(h => h.Shape == HazardShape.Mine && h.Source == enemy.Id).OrderBy(h => h.StartTick).ToList();
                    for (int i = 0; i < attack.Count; i++)
                    {
                        if (mines.Count + i >= MaxMines) _hazards.Remove(mines[mines.Count + i - MaxMines]);
                        float angle = facing.Angle + MathF.PI / 2 + i * 2 * MathF.PI / attack.Count;
                        var at = attack.Spread > 0 ? target.Position + Vec2.FromAngle(angle) * attack.Spread : target.Position;
                        Lay(enemy, attack, HazardShape.Mine, at, facing, attack.Radius, 0, damage, resolve + Ticks(attack.Duration)).ArmTick = resolve;
                    }
                    break;
                case MonsterAttackShape.Mend:
                    enemy.TetherId = HurtAlly(enemy, attack.Length)?.Id ?? 0;
                    break;
            }
            return true;
        }

        // Whether a monster's body could go straight from one spot to the other.
        bool OpenToMonsters(Vec2 from, Vec2 to, float radius)
        {
            float length = Vec2.Distance(from, to);
            for (float along = 0; along <= length; along += 0.25f)
            {
                if (Collision.Overlaps(Map, Vec2.Lerp(from, to, length < 1e-4f ? 0 : along / length), radius * 0.5f, monster: true)) return false;
            }
            return !Collision.Overlaps(Map, to, radius * 0.5f, monster: true);
        }

        // A spot that far from the monster, away from the player if there is one that way, in the open and in sight.
        Vec2? BlinkSpot(Enemy enemy, Player target, float length)
        {
            float away = (enemy.Position - target.Position).Angle;
            foreach (float turn in new[] { 0f, 0.6f, -0.6f, 1.2f, -1.2f, 1.9f, -1.9f, MathF.PI })
            {
                var spot = enemy.Position + Vec2.FromAngle(away + turn) * length;
                if (!Map.InBounds(TileMap.Cell(spot.X), TileMap.Cell(spot.Y))) continue;
                if (Collision.Overlaps(Map, spot, enemy.Definition.Radius, monster: true)) continue;
                if (!OpenToMonsters(enemy.Position, spot, enemy.Definition.Radius)) continue;
                if (InSafePlace(spot)) continue;
                return spot;
            }
            return null;
        }

        // A spot that far behind the player (the way they are not looking), or as near to behind as is open and in
        // their sight: where a shadow step comes out.
        Vec2? SpotBehind(Enemy enemy, Player target, float length)
        {
            float back = (-target.Facing).Angle;
            float reach = length + enemy.Definition.Radius + PlayerRadius;
            foreach (float turn in new[] { 0f, 0.5f, -0.5f, 1f, -1f, 1.6f, -1.6f, 2.4f, -2.4f, MathF.PI })
            {
                var spot = target.Position + Vec2.FromAngle(back + turn) * reach;
                if (!Map.InBounds(TileMap.Cell(spot.X), TileMap.Cell(spot.Y))) continue;
                if (Collision.Overlaps(Map, spot, enemy.Definition.Radius, monster: true)) continue;
                if (!OpenToMonsters(target.Position, spot, enemy.Definition.Radius) || !ClearLine(target.Position, spot, ThinLine)) continue;
                if (InSafePlace(spot)) continue;
                return spot;
            }
            return null;
        }

        // While the warning shows: a leap's hop, a bomber running at its target with its circle.
        void WindUp(Enemy enemy, Player target)
        {
            var attack = enemy.Attack;
            switch (attack.Shape)
            {
                case MonsterAttackShape.Leap:
                    int hop = Math.Min(Ticks(HopSeconds), Ticks(attack.Telegraph));
                    if (enemy.StateTicks <= hop) enemy.Position = Vec2.Lerp(enemy.LeapFrom, enemy.LandAt, 1 - (enemy.StateTicks - 1) / (float)hop);
                    break;
                case MonsterAttackShape.Explode:
                    if (target != null && !target.IsDead)
                    {
                        var toward = target.Position - enemy.Position;
                        if (toward.Length > enemy.Definition.Radius + PlayerRadius)
                        {
                            enemy.Facing = toward.Normalized;
                            // Quicksand slows the roll (B2: a bomb scarab, World.GroundPace).
                            enemy.Position = Collision.Move(Map, enemy.Position, enemy.Definition.Radius, enemy.Facing * (enemy.Speed * GroundPace(enemy) * TickSeconds), monster: true);
                        }
                    }
                    foreach (var hazard in _hazards)
                        if (hazard.Source == enemy.Id && hazard.Attached && hazard.Shape == HazardShape.Circle) hazard.Position = enemy.Position;
                    break;
            }
            Repeat(enemy, target);
        }

        // A lob's later circles fall on where the target is by then; a volley's later shots are aimed anew.
        void Repeat(Enemy enemy, Player target)
        {
            var attack = enemy.Attack;
            if (attack == null || enemy.RepeatsLeft <= 0 || attack.Shape == MonsterAttackShape.Charge) return;
            if (--enemy.RepeatTicks > 0) return;
            enemy.RepeatsLeft--;
            if (target == null || target.IsDead) return;
            if (attack.Shape == MonsterAttackShape.Lob || attack.Shape == MonsterAttackShape.Delayed)
            {
                enemy.RepeatTicks = Ticks(LobSeconds);
                Lay(enemy, attack, HazardShape.Circle, target.Position, enemy.Facing, attack.Radius, 0, DamageOf(enemy, attack), Tick + Ticks(TelegraphOf(enemy, attack)));
            }
            else if (attack.Shape == MonsterAttackShape.Volley && enemy.State == EnemyState.Recover)
            {
                enemy.RepeatTicks = Ticks(VolleySeconds);
                var aim = target.Position - enemy.Position;
                if (aim.LengthSquared > 1e-6f) enemy.Facing = aim.Normalized;
                Fire(enemy, attack, enemy.Facing);
                _events.Add(new SimEvent(SimEventType.Attack, enemy.Id, direction: enemy.Facing));
            }
        }

        Projectile Fire(Enemy enemy, MonsterAttack attack, Vec2 direction)
        {
            float reach = attack.Length > 0 ? attack.Length : float.IsInfinity(attack.RangeMax) ? enemy.Definition.Aggro : attack.RangeMax * 1.5f;
            var shot = new Projectile(_nextId++, enemy.Id, attack.Index, enemy.Position + direction * enemy.Definition.Radius, direction * attack.Speed, attack.Radius,
                DamageOf(enemy, attack), Ticks(reach / attack.Speed))
            {
                Effect = attack.Effect,
                EffectAmount = attack.EffectAmount,
            };
            _projectiles.Add(shot);
            return shot;
        }

        // The telegraph is over. Marked areas land by themselves (UpdateHazards); this fires, rushes, moves or calls.
        void Unleash(Enemy enemy, Player target)
        {
            var attack = enemy.Attack;
            var definition = enemy.Definition;
            if (attack.Tell == Tell.Barrage && target != null && !target.IsDead && attack.Shape != MonsterAttackShape.Spread)
            {
                // A single shot is aimed when it leaves (as the old shooters'); a fan keeps the way it was shown.
                var aim = target.Position - enemy.Position;
                if (aim.LengthSquared > 1e-6f) enemy.Facing = aim.Normalized;
            }
            _events.Add(new SimEvent(SimEventType.Attack, enemy.Id, direction: enemy.Facing));
            switch (attack.Shape)
            {
                case MonsterAttackShape.Charge:
                case MonsterAttackShape.Swoop:
                    enemy.State = EnemyState.Charge;
                    enemy.Slid = false;
                    enemy.RushSpeed = attack.Speed;
                    enemy.StateTicks = Ticks(attack.Length / attack.Speed);
                    enemy.ChargeHits.Clear();
                    enemy.TrailAt = enemy.Position;
                    return;
                case MonsterAttackShape.Spread:
                    float spread = attack.Angle * MathF.PI / 180f;
                    for (int i = 0; i < attack.Count; i++)
                        Fire(enemy, attack, Vec2.FromAngle(enemy.Facing.Angle + (attack.Count == 1 ? 0 : -spread / 2 + spread * i / (attack.Count - 1))));
                    break;
                case MonsterAttackShape.Volley:
                    Fire(enemy, attack, enemy.Facing);
                    enemy.RepeatsLeft = attack.Count - 1;
                    enemy.RepeatTicks = Ticks(VolleySeconds);
                    break;
                case MonsterAttackShape.Bounce:
                    var bouncing = Fire(enemy, attack, enemy.Facing);
                    bouncing.BouncesLeft = attack.Count;
                    bouncing.RemainingTicks = Ticks(ProjectileSeconds);
                    break;
                case MonsterAttackShape.Boomerang:
                    var returning = Fire(enemy, attack, enemy.Facing);
                    returning.TurnTicks = Ticks(attack.Length / attack.Speed);
                    returning.RemainingTicks = returning.TurnTicks * 3 + Ticks(TurnPause + 2);
                    break;
                case MonsterAttackShape.Leap:
                    enemy.Position = enemy.LandAt;
                    break;
                case MonsterAttackShape.Burrow:
                case MonsterAttackShape.Ambush:
                    enemy.Position = enemy.LandAt;
                    if (target != null)
                    {
                        var look = target.Position - enemy.Position;
                        if (look.LengthSquared > 1e-6f) enemy.Facing = look.Normalized;
                    }
                    break;
                case MonsterAttackShape.Blink:
                    _events.Add(new SimEvent(SimEventType.MonsterFx, enemy.Id, amount: MonsterFxBlink, position: enemy.Position));
                    enemy.Position = enemy.LandAt;
                    enemy.Knockback = Vec2.Zero;
                    if (attack.Behind && target != null)
                    {
                        var look = target.Position - enemy.Position;
                        if (look.LengthSquared > 1e-6f) enemy.Facing = look.Normalized;
                    }
                    break;
                case MonsterAttackShape.Explode:
                    // It is gone with its blast (which lands this same tick) and leaves nothing.
                    enemy.Health = 0;
                    enemy.BlewUp = true;
                    _events.Add(new SimEvent(SimEventType.MonsterFx, enemy.Id, attack.Index, MonsterFxExplode, position: enemy.Position));
                    _events.Add(new SimEvent(SimEventType.Died, enemy.Id));
                    return;
                case MonsterAttackShape.Summon:
                    for (int i = 0; i < attack.Count; i++)
                    {
                        float angle = (i + _random.NextFloat() * 0.5f) * 2 * MathF.PI / attack.Count;
                        _queuedMonsters.Add((attack.Summon, enemy.Position + Vec2.FromAngle(angle) * Math.Max(1, attack.Spread), null, false));
                    }
                    break;
                case MonsterAttackShape.Mend:
                    var ally = FindEnemy(enemy.TetherId);
                    if (ally != null && !ally.IsDead)
                    {
                        enemy.State = EnemyState.Channel;
                        enemy.StateTicks = Ticks(attack.Duration);
                        enemy.ReadyAt[attack.Index] = Tick + enemy.StateTicks + Ticks(attack.Cooldown);
                        _events.Add(new SimEvent(SimEventType.Tether, enemy.Id, ally.Id, enemy.StateTicks));
                        return;
                    }
                    break;
                case MonsterAttackShape.Empower:
                    foreach (var other in _enemies)
                    {
                        if (other == enemy || other.IsDead || Vec2.Distance(other.Position, enemy.Position) > attack.Radius) continue;
                        other.EmpowerTicks = Ticks(attack.Duration);
                        other.EmpowerShare = attack.Damage / 100f;
                    }
                    _events.Add(new SimEvent(SimEventType.MonsterFx, enemy.Id, amount: MonsterFxEmpower, position: enemy.Position));
                    break;
            }
            // Straight into the attack that follows it (a shadow step's cut), with no rest between.
            if (attack.Then != null && target != null && !target.IsDead)
            {
                enemy.ReadyAt[attack.Index] = Tick + Ticks(attack.Cooldown);
                Ready(enemy, attack.Then);
                if (StartAttack(enemy, attack.Then, target)) return;
            }
            Rest(enemy);
        }

        // After an attack the monster rests its own cooldown (long enough for what the attack still has to do), and
        // the attack itself cannot be picked again for its own.
        void Rest(Enemy enemy)
        {
            var attack = enemy.Attack;
            int still = 0;
            if (attack != null)
            {
                enemy.ReadyAt[attack.Index] = Tick + Ticks(attack.Cooldown);
                if (attack.Shape == MonsterAttackShape.Sweep) still = (attack.Count - 1) * Ticks(SweepSeconds) + 1;
                else if (attack.Shape == MonsterAttackShape.Volley) still = enemy.RepeatsLeft * Ticks(VolleySeconds) + 1;
                else if (attack.Shape == MonsterAttackShape.Lob || attack.Shape == MonsterAttackShape.Delayed) still = enemy.RepeatsLeft * Ticks(LobSeconds) + 1;
            }
            // One that lives under the ground stays up a while after it has struck: the time to hit it.
            var burrowed = enemy.Definition.Trait(MonsterTraitKind.Burrowed);
            if (burrowed != null) still = Math.Max(still, Ticks(burrowed.Number(0, SurfaceSeconds)));
            enemy.State = EnemyState.Recover;
            enemy.StateTicks = Math.Max(Ticks(enemy.Definition.Cooldown), still);
        }

        // A mend that ran out or was broken by a hit, or a call stopped before it was made.
        void EndAttack(Enemy enemy, bool broken)
        {
            if (enemy.State == EnemyState.Channel || enemy.Attack?.Shape == MonsterAttackShape.Mend)
                _events.Add(new SimEvent(SimEventType.Tether, enemy.Id, enemy.TetherId, 0));
            _hazards.RemoveAll(h => h.Source == enemy.Id && h.Attached);
            enemy.TetherId = 0;
            Rest(enemy);
        }

        // The rush of a sheet charge or swoop: along the marked path, hurting each player it runs into once, leaving
        // its pool behind it. A charge that ends at a wall dazes one with the stagger trait; one with runs left winds
        // up the next in less time.
        void Rush(Enemy enemy)
        {
            var attack = enemy.Attack;
            var definition = enemy.Definition;
            var before = enemy.Position;
            var step = enemy.ChargeDirection * (enemy.RushSpeed * TickSeconds);
            enemy.Position = Collision.Move(Map, enemy.Position, definition.Radius, step, monster: true);
            enemy.Facing = enemy.ChargeDirection;
            foreach (var player in _players)
            {
                if (player.IsDead || enemy.ChargeHits.Contains(player.Id)) continue;
                if (Vec2.Distance(player.Position, enemy.Position) > Math.Max(definition.Radius, attack.Width / 2) + PlayerRadius) continue;
                enemy.ChargeHits.Add(player.Id);
                AfflictPlayer(player, enemy.Id, HurtPlayer(player, enemy.Id, DamageOf(enemy, attack), attack.Index));
                Affect(player, attack.Effect, attack.EffectAmount, enemy.Position, enemy.Id, DamageOf(enemy, attack));
            }
            if (attack.Pool != MonsterPool.None && Vec2.Distance(enemy.TrailAt, enemy.Position) >= attack.Width)
            {
                enemy.TrailAt = enemy.Position;
                LayPool(enemy.Id, attack.Index, attack.Pool, enemy.Position, attack.Width / 2, attack.Linger, DamageOf(enemy, attack), MaxPools);
            }

            bool blocked = Vec2.Distance(before, enemy.Position) < step.Length * 0.3f;
            if (--enemy.StateTicks > 0 && !blocked) return;
            // On the ice (B2, World.IceSand.cs) a charge that ran its length slides on a little, and one that runs into a
            // wall on the ice is dazed whatever its kind.
            if (!blocked && SlideOn(enemy)) return;
            bool icy = attack.Shape == MonsterAttackShape.Charge && (enemy.Slid || OnIce(enemy));
            if (blocked && attack.Shape == MonsterAttackShape.Charge && (definition.Has(MonsterTraitKind.Stagger) || icy))
            {
                enemy.RepeatsLeft = 0;
                // A big body smashes the stalagmite or pit prop it ran into, and is dazed all the same (B1c).
                SmashAhead(enemy);
                enemy.State = EnemyState.Stagger;
                // stagger:seconds for its own (a big beetle's longer daze), else StaggerSeconds.
                enemy.StateTicks = Ticks(definition.Trait(MonsterTraitKind.Stagger)?.Number(0, StaggerSeconds) ?? StaggerSeconds);
                _events.Add(new SimEvent(SimEventType.MonsterFx, enemy.Id, amount: MonsterFxStagger, position: enemy.Position));
                return;
            }
            var next = FindPlayer(enemy.TargetId);
            if (enemy.RepeatsLeft > 0 && next != null && !next.IsDead)
            {
                enemy.RepeatsLeft--;
                StartAttack(enemy, attack, next, again: true);
                return;
            }
            Rest(enemy);
        }

        // ---- Pieces of the hazard and projectile engine (called from World.Boss.cs) ----

        // What a blow does besides its damage.
        void Affect(Player player, HitEffect effect, float amount, Vec2 from, int source = 0, float blow = 0)
        {
            // A blow rolled through (World.Roll.cs) neither roots nor pushes, as it does no damage.
            if (player.IsDead || player.Dodging || player.Riding) return;
            switch (effect)
            {
                case HitEffect.Root:
                    player.Afflictions.RootTicks = Math.Max(player.Afflictions.RootTicks, Ticks(amount));
                    break;
                case HitEffect.Push:
                    var away = player.Position - from;
                    if (away.LengthSquared < 1e-6f) break;
                    // The host moves them and their client is told where they are now.
                    player.Position = Collision.Move(Map, player.Position, PlayerRadius, away.Normalized * amount, passable: DoorsFor(player));
                    player.Corrections++;
                    break;
                case HitEffect.Venom:
                    int bites = Math.Max(1, Ticks(amount) / Ticks(VenomSeconds));
                    player.Afflictions.PoisonTicks = bites * Ticks(VenomSeconds);
                    player.Afflictions.PoisonBite = Math.Max(1, blow * PoisonShare / bites);
                    player.Afflictions.PoisonSource = source;
                    break;
                case HitEffect.Chill:
                    player.Afflictions.ChillTicks = Math.Max(player.Afflictions.ChillTicks, Ticks(amount));
                    break;
            }
        }

        // One tick of a circle's drag on someone inside it: toward its middle at speed, not through walls. The host
        // moves its players with this and a client moves its own with the same (WorldClient.Step), so the two agree.
        public static Vec2 PullStep(TileMap map, Vec2 position, Vec2 centre, float size, float speed, ISet<int> doors = null)
        {
            var toward = centre - position;
            float distance = toward.Length;
            if (distance > size + PlayerRadius || distance < 0.05f) return position;
            return Collision.Move(map, position, PlayerRadius, toward / distance * Math.Min(distance, speed * TickSeconds), passable: doors);
        }

        // A hazard that has not landed yet: a pull drags, an armed trap watches for a step.
        void Pending(Hazard hazard)
        {
            if (hazard.Shape == HazardShape.Pull)
            {
                foreach (var player in _players)
                    if (!player.IsDead) player.Position = PullStep(Map, player.Position, hazard.Position, hazard.Size, hazard.Width, DoorsFor(player));
            }
            else if (hazard.Shape == HazardShape.Mine && Tick >= hazard.ArmTick)
            {
                // Once stepped on it still leaves the time to walk out of it.
                long fuse = Tick + Ticks((hazard.Size + PlayerRadius) / MonsterAttackSheet.WalkSpeed + MonsterAttackSheet.NoticeSeconds);
                if (hazard.ResolveTick <= fuse || !_players.Any(p => !p.IsDead && hazard.Contains(p.Position, PlayerRadius))) return;
                hazard.ResolveTick = fuse;
                _events.Add(new SimEvent(SimEventType.MonsterFx, hazard.Source, amount: MonsterFxMine, position: hazard.Position));
            }
        }

        // What a monster's landed area leaves: a pool its size, or along a line a few its width.
        void LeaveMonsterPool(Hazard hazard)
        {
            if (hazard.Leaves == MonsterPool.None) return;
            int damage = hazard.Damage;
            if (hazard.Shape == HazardShape.Line)
            {
                float radius = hazard.Width / 2;
                int count = Math.Min(MaxPools, Math.Max(1, (int)MathF.Round(hazard.Size / (radius * 2))));
                for (int i = 0; i < count; i++)
                    LayPool(hazard.Source, hazard.Cause, hazard.Leaves, hazard.Position + hazard.Direction * (hazard.Size * (i + 0.5f) / count), radius, hazard.LeavesSeconds, damage, MaxPools);
            }
            else if (hazard.Shape == HazardShape.Circle || hazard.Shape == HazardShape.Ring)
            {
                LayPool(hazard.Source, hazard.Cause, hazard.Leaves, hazard.Position, hazard.LeavesRadius > 0 ? hazard.LeavesRadius : hazard.Size, hazard.LeavesSeconds, damage);
            }
        }

        static HazardShape ShapeOf(MonsterPool kind) => kind switch
        {
            MonsterPool.Fire => HazardShape.Fire,
            MonsterPool.Ice => HazardShape.Ice,
            MonsterPool.Spore => HazardShape.Spore,
            MonsterPool.Web => HazardShape.Web,
            MonsterPool.Venom => HazardShape.Venom,
            MonsterPool.Cloud => HazardShape.Balm,
            _ => HazardShape.Sand,
        };

        // blow is the damage of the hit that left it: venom and spores bite for VenomShare of it, fire burns for
        // FirePoolShare of it a second.
        void LayPool(int source, int cause, MonsterPool kind, Vec2 position, float radius, float seconds, float blow, int most = MaxMonsterPools)
        {
            var shape = ShapeOf(kind);
            var pools = _hazards.Where(h => h.Lingers && h.Source == source).OrderBy(h => h.StartTick).ToList();
            if (pools.Count >= most) _hazards.Remove(pools[0]);
            int damage = shape == HazardShape.Venom || shape == HazardShape.Spore ? Math.Max(1, (int)MathF.Round(blow * VenomShare))
                : shape == HazardShape.Fire ? Math.Max(1, (int)MathF.Round(blow * FirePoolShare)) : 0;
            // A spore cloud harms no one; its width is how much of their health it fills the monsters in it a second.
            float width = shape == HazardShape.Balm ? DeathcloudFill : 0;
            _hazards.Add(new Hazard(_nextId++, source, cause, shape, position, new Vec2(0, -1), radius, width, damage, Tick, Tick + Ticks(seconds)) { Mark = (byte)(1 + (int)kind) });
        }

        // The pools monsters leave, while they lie there (webs and venom are the bosses' own, World.Linger).
        void LingerMonsterPool(Hazard pool)
        {
            if (pool.Shape == HazardShape.Balm)
            {
                FillFromCloud(pool);
                return;
            }
            bool bites = pool.Shape == HazardShape.Spore && Tick > pool.StartTick && (Tick - pool.StartTick) % Ticks(VenomSeconds) == 0;
            foreach (var player in _players)
            {
                if (player.IsDead || !pool.Contains(player.Position, PlayerRadius)) continue;
                switch (pool.Shape)
                {
                    case HazardShape.Fire:
                        player.Afflictions.Burn(pool.Damage, TicksPerSecond, pool.Source);
                        break;
                    case HazardShape.Ice:
                        player.Afflictions.ChillTicks = Math.Max(player.Afflictions.ChillTicks, Ticks(SnareSeconds));
                        break;
                    case HazardShape.Sand:
                        player.Afflictions.SnareTicks = Math.Max(player.Afflictions.SnareTicks, Ticks(SnareSeconds));
                        break;
                    case HazardShape.Spore:
                        if (bites) HurtPlayer(player, pool.Source, pool.Damage, pool.Cause, armor: false);
                        break;
                }
            }
        }

        // A shot that has hit rock comes off it if it still has bounces; one on its way out turns back there instead.
        bool Rebound(Projectile shot)
        {
            if (shot.TurnTicks > 0)
            {
                shot.Position -= shot.Velocity * TickSeconds;
                shot.TurnTicks = 1;
                return true;
            }
            if (shot.BouncesLeft <= 0) return false;
            var step = shot.Velocity * TickSeconds;
            var from = shot.Position - step;
            float radius = shot.Radius * 0.5f;
            bool acrossX = Collision.Overlaps(Map, from + new Vec2(step.X, 0), radius);
            bool acrossY = Collision.Overlaps(Map, from + new Vec2(0, step.Y), radius);
            if (!acrossX && !acrossY) acrossX = acrossY = true;
            shot.Velocity = new Vec2(acrossX ? -shot.Velocity.X : shot.Velocity.X, acrossY ? -shot.Velocity.Y : shot.Velocity.Y);
            shot.Position = from;
            shot.BouncesLeft--;
            _events.Add(new SimEvent(SimEventType.MonsterFx, shot.Source, amount: MonsterFxBounce, position: from + step));
            return true;
        }

        // A returning shot: out for its time, a pause, then back to whoever threw it. True once it is home (or has no
        // home to go to), when it is gone.
        bool ComesBack(Projectile shot)
        {
            if (shot.TurnTicks == 0) return false;
            if (shot.TurnTicks > 0)
            {
                if (--shot.TurnTicks > 0) return false;
                shot.TurnTicks = -1;
                shot.PauseTicks = Ticks(TurnPause);
                shot.Pace = shot.Velocity.Length;
                shot.Velocity = Vec2.Zero;
                // It can strike on the way back whoever it struck on the way out.
                shot.Pierced.Clear();
                _events.Add(new SimEvent(SimEventType.MonsterFx, shot.Source, amount: MonsterFxTurn, position: shot.Position));
                return false;
            }
            if (shot.PauseTicks > 0)
            {
                shot.PauseTicks--;
                return false;
            }
            var home = FindEnemy(shot.Source);
            if (home == null || home.IsDead) return shot.Velocity.LengthSquared < 1e-6f;
            var toward = home.Position - shot.Position;
            if (toward.Length <= home.Definition.Radius + shot.Radius + shot.Pace * TickSeconds) return true;
            shot.Velocity = toward.Normalized * shot.Pace;
            return false;
        }
    }
}
