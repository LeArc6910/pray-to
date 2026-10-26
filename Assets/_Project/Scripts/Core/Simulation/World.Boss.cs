using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // Bosses: they sleep in their arena until a player walks in, then pick attacks by weight for their current
    // phase, walk into range, mark where the attack will land and let it land after the telegraph. Every hit can
    // be dodged by reading the marks. Crossing a health threshold starts the next phase with a roar. If everyone
    // leaves or falls, the boss resets.
    public sealed partial class World
    {
        // How far outside the arena players still count as fighting it.
        const float ArenaMargin = 6;
        const float ResetSeconds = 5;
        // An attack starts after this long even if the boss could not get into range.
        const float ApproachSeconds = 3;
        const float ProjectileSeconds = 4;

        readonly List<Boss> _bosses = new List<Boss>();
        readonly List<Hazard> _hazards = new List<Hazard>();
        readonly List<Projectile> _projectiles = new List<Projectile>();
        readonly List<(BossDefinition Definition, Vec2 Home, float ArenaRadius, int Ticks)> _bossRespawns =
            new List<(BossDefinition Definition, Vec2 Home, float ArenaRadius, int Ticks)>();

        public IReadOnlyList<Boss> Bosses => _bosses;
        public IReadOnlyList<Hazard> Hazards => _hazards;
        public IReadOnlyList<Projectile> Projectiles => _projectiles;

        public Boss PlaceBoss(BossDefinition definition, Vec2 home, float arenaRadius)
        {
            var boss = new Boss(_nextId++, definition, home, arenaRadius);
            _bosses.Add(boss);
            return boss;
        }

        public Boss FindBoss(int id) => _bosses.FirstOrDefault(b => b.Id == id);

        // Players near enough to the arena to be in the fight.
        List<Player> Fighters(Boss boss) =>
            _players.Where(p => !p.IsDead && Vec2.Distance(p.Position, boss.Home) <= boss.ArenaRadius + ArenaMargin).ToList();

        void Wake(Boss boss)
        {
            var definition = boss.Definition;
            int players = Math.Max(1, _players.Count(p => !p.IsDead));
            boss.MaxHealth = definition.Health * (1 + definition.HealthPerPlayer / 100f * (players - 1));
            boss.Health = boss.MaxHealth;
            boss.Phase = 1;
            boss.EmptyTicks = 0;
            _events.Add(new SimEvent(SimEventType.BossAwake, boss.Id));
            ChooseAttack(boss, Fighters(boss));
        }

        void UpdateBosses()
        {
            for (int i = _bossRespawns.Count - 1; i >= 0; i--)
            {
                var respawn = _bossRespawns[i];
                if (--respawn.Ticks > 0)
                {
                    _bossRespawns[i] = respawn;
                    continue;
                }
                _bossRespawns.RemoveAt(i);
                PlaceBoss(respawn.Definition, respawn.Home, respawn.ArenaRadius);
            }

            foreach (var boss in _bosses)
            {
                if (!boss.IsDead) UpdateBoss(boss);
            }
        }

        void UpdateBoss(Boss boss)
        {
            if (boss.State == BossState.Dormant)
            {
                if (_players.Any(p => !p.IsDead && Vec2.Distance(p.Position, boss.Home) <= boss.ArenaRadius - 1)) Wake(boss);
                return;
            }

            var fighters = Fighters(boss);
            if (fighters.Count == 0)
            {
                if (++boss.EmptyTicks >= Ticks(ResetSeconds)) ResetBoss(boss);
                return;
            }
            boss.EmptyTicks = 0;

            switch (boss.State)
            {
                case BossState.Approach:
                    var target = FindPlayer(boss.TargetId);
                    if (target == null || target.IsDead || !fighters.Contains(target))
                    {
                        ChooseAttack(boss, fighters);
                        break;
                    }
                    if (Vec2.Distance(target.Position, boss.Position) <= boss.Attack.Range || --boss.StateTicks <= 0)
                    {
                        StartTelegraph(boss, target, fighters);
                        break;
                    }
                    MoveBoss(boss, (target.Position - boss.Position).Normalized * (boss.Definition.Speed * TickSeconds));
                    break;

                case BossState.Telegraph:
                    if (--boss.StateTicks <= 0) Unleash(boss);
                    break;

                case BossState.Charge:
                    Charge(boss);
                    break;

                case BossState.Recover:
                case BossState.Roar:
                    if (--boss.StateTicks <= 0) ChooseAttack(boss, fighters);
                    break;
            }
        }

        void ChooseAttack(Boss boss, List<Player> fighters)
        {
            if (fighters.Count == 0) return;
            var choices = boss.Definition.Attacks.Where(a => a.InPhase(boss.Phase) && a.Weight > 0).ToList();
            boss.Attack = _random.PickWeighted(choices, a => a.Weight);
            boss.TargetId = PickTarget(boss, boss.Attack.Target, fighters).Id;
            boss.State = BossState.Approach;
            boss.StateTicks = Ticks(ApproachSeconds);
        }

        Player PickTarget(Boss boss, AttackTarget mode, List<Player> fighters)
        {
            switch (mode)
            {
                case AttackTarget.Farthest:
                    return fighters.OrderByDescending(p => Vec2.Distance(p.Position, boss.Position)).First();
                case AttackTarget.Random:
                    return fighters[_random.Next(fighters.Count)];
                default:
                    return fighters.OrderBy(p => Vec2.Distance(p.Position, boss.Position)).First();
            }
        }

        // Marks where the attack will land. Circles stay where their target stood, so moving away dodges them.
        void StartTelegraph(Boss boss, Player target, List<Player> fighters)
        {
            var attack = boss.Attack;
            var aim = target.Position - boss.Position;
            if (aim.LengthSquared > 1e-6f) boss.Facing = aim.Normalized;
            boss.State = BossState.Telegraph;
            boss.StateTicks = Ticks(attack.Telegraph);
            long resolve = Tick + boss.StateTicks;
            int cause = Cause(boss, attack);

            switch (attack.Shape)
            {
                case AttackShape.Circle:
                    var centres = attack.Target == AttackTarget.Self ? new[] { boss.Position }
                        : attack.Target == AttackTarget.Each ? fighters.Select(p => p.Position).ToArray()
                        : new[] { target.Position };
                    foreach (var centre in centres)
                    {
                        for (int i = 0; i < attack.Count; i++)
                        {
                            var offset = attack.Spread > 0
                                ? Vec2.FromAngle(_random.NextFloat() * 2 * MathF.PI) * (MathF.Sqrt(_random.NextFloat()) * attack.Spread)
                                : Vec2.Zero;
                            AddHazard(boss, cause, HazardShape.Circle, centre + offset, boss.Facing, attack.Radius, 0, attack.Damage, resolve);
                        }
                    }
                    break;
                case AttackShape.Cone:
                    AddHazard(boss, cause, HazardShape.Cone, boss.Position, boss.Facing, attack.Length, attack.Angle, attack.Damage, resolve);
                    break;
                case AttackShape.Line:
                    AddHazard(boss, cause, HazardShape.Line, boss.Position, boss.Facing, attack.Length, attack.Width, attack.Damage, resolve);
                    break;
                case AttackShape.Charge:
                    // Only marks the path; the charge itself does the hurting.
                    AddHazard(boss, cause, HazardShape.Line, boss.Position, boss.Facing, attack.Length, attack.Width, 0, resolve);
                    boss.ChargeDirection = boss.Facing;
                    break;
            }
        }

        // The telegraph is over. Marked areas resolve by themselves in UpdateHazards; this fires, charges or summons.
        void Unleash(Boss boss)
        {
            var attack = boss.Attack;
            _events.Add(new SimEvent(SimEventType.Attack, boss.Id, direction: boss.Facing));
            switch (attack.Shape)
            {
                case AttackShape.Charge:
                    boss.State = BossState.Charge;
                    boss.StateTicks = Ticks(attack.Length / attack.Speed);
                    boss.ChargeHits.Clear();
                    return;
                case AttackShape.Burst:
                    Burst(boss, attack);
                    break;
                case AttackShape.Summon:
                    for (int i = 0; i < attack.Count; i++)
                    {
                        float angle = (i + _random.NextFloat() * 0.5f) * 2 * MathF.PI / attack.Count;
                        var position = Collision.Resolve(Map, boss.Position + Vec2.FromAngle(angle) * attack.Spread, attack.Summon.Radius);
                        if (!Collision.Overlaps(Map, position, attack.Summon.Radius)) SpawnEnemy(attack.Summon, position);
                    }
                    break;
            }
            boss.State = BossState.Recover;
            boss.StateTicks = Ticks(attack.Cooldown);
        }

        // Evenly spaced projectiles: all the way round from a random start, or fanned out toward the target.
        void Burst(Boss boss, BossAttack attack)
        {
            bool fan = attack.Angle > 0 && attack.Angle < 360;
            float spread = (fan ? attack.Angle : 360) * MathF.PI / 180f;
            float start = fan ? boss.Facing.Angle - spread / 2 : _random.NextFloat() * 2 * MathF.PI;
            int gaps = fan ? Math.Max(1, attack.Count - 1) : attack.Count;
            for (int i = 0; i < attack.Count; i++)
            {
                var direction = Vec2.FromAngle(start + (fan && attack.Count == 1 ? spread / 2 : spread * i / gaps));
                _projectiles.Add(new Projectile(
                    _nextId++,
                    boss.Id,
                    Cause(boss, attack),
                    boss.Position + direction * boss.Definition.Radius,
                    direction * attack.Speed,
                    attack.Radius,
                    attack.Damage,
                    Ticks(ProjectileSeconds)));
            }
        }

        void Charge(Boss boss)
        {
            var attack = boss.Attack;
            var before = boss.Position;
            var step = boss.ChargeDirection * (attack.Speed * TickSeconds);
            MoveBoss(boss, step);
            boss.Facing = boss.ChargeDirection;

            foreach (var player in _players)
            {
                if (player.IsDead || boss.ChargeHits.Contains(player.Id)) continue;
                if (Vec2.Distance(player.Position, boss.Position) > boss.Definition.Radius + PlayerRadius) continue;
                boss.ChargeHits.Add(player.Id);
                HurtPlayer(player, boss.Id, attack.Damage, Cause(boss, attack));
            }

            bool blocked = Vec2.Distance(before, boss.Position) < step.Length * 0.3f;
            if (--boss.StateTicks > 0 && !blocked) return;
            _events.Add(new SimEvent(SimEventType.Impact, boss.Id, amount: 6));
            boss.State = BossState.Recover;
            boss.StateTicks = Ticks(attack.Cooldown);
        }

        // Bosses stay in their arena so a fight cannot be dragged off through the caves.
        void MoveBoss(Boss boss, Vec2 delta)
        {
            var position = Collision.Move(Map, boss.Position, boss.Definition.Radius, delta);
            var fromHome = position - boss.Home;
            float limit = Math.Max(0, boss.ArenaRadius - boss.Definition.Radius);
            if (fromHome.Length > limit) position = boss.Home + fromHome.Normalized * limit;
            if (delta.LengthSquared > 1e-8f) boss.Facing = delta.Normalized;
            boss.Position = position;
        }

        void DamageBoss(Boss boss, int source, int amount, bool critical)
        {
            boss.Health -= amount;
            _events.Add(new SimEvent(SimEventType.Hit, source, boss.Id, amount, critical));
            if (boss.IsDead)
            {
                ClearAttacksFrom(boss);
                _events.Add(new SimEvent(SimEventType.Died, boss.Id));
                _events.Add(new SimEvent(SimEventType.BossDefeated, boss.Id));
                return;
            }

            int phase = 1;
            float percent = boss.Health * 100 / boss.MaxHealth;
            foreach (int threshold in boss.Definition.Thresholds)
            {
                if (percent <= threshold) phase++;
            }
            if (phase <= boss.Phase) return;

            // A new phase: roar, unhurt, then blast everyone standing too close.
            var definition = boss.Definition;
            boss.Phase = phase;
            boss.Attack = null;
            boss.State = BossState.Roar;
            boss.StateTicks = Ticks(definition.Roar);
            _events.Add(new SimEvent(SimEventType.PhaseChanged, boss.Id, amount: phase));
            if (definition.RoarDamage > 0 && definition.RoarRadius > 0)
            {
                AddHazard(boss, 0, HazardShape.Circle, boss.Position, boss.Facing, definition.RoarRadius, 0, definition.RoarDamage, Tick + boss.StateTicks);
            }
        }

        void ResetBoss(Boss boss)
        {
            ClearAttacksFrom(boss);
            boss.Health = boss.Definition.Health;
            boss.MaxHealth = boss.Definition.Health;
            boss.Phase = 1;
            boss.Position = boss.Home;
            boss.Attack = null;
            boss.State = BossState.Dormant;
            boss.EmptyTicks = 0;
        }

        void RemoveDefeatedBosses()
        {
            foreach (var boss in _bosses.Where(b => b.IsDead).ToList())
            {
                ClearAttacksFrom(boss);
                _bosses.Remove(boss);
                _bossRespawns.Add((boss.Definition, boss.Home, boss.ArenaRadius, Ticks(_config.BossRespawnSeconds)));
            }
        }

        void ClearAttacksFrom(Boss boss)
        {
            _hazards.RemoveAll(h => h.Source == boss.Id);
            _projectiles.RemoveAll(p => p.Source == boss.Id);
        }

        void AddHazard(Boss boss, int cause, HazardShape shape, Vec2 position, Vec2 direction, float size, float width, int damage, long resolve) =>
            _hazards.Add(new Hazard(_nextId++, boss.Id, cause, shape, position, direction, size, width, damage, Tick, resolve));

        static int Cause(Boss boss, BossAttack attack)
        {
            for (int i = 0; i < boss.Definition.Attacks.Count; i++)
            {
                if (boss.Definition.Attacks[i] == attack) return i + 1;
            }
            return 0;
        }

        void UpdateHazards()
        {
            for (int i = _hazards.Count - 1; i >= 0; i--)
            {
                var hazard = _hazards[i];
                if (Tick < hazard.ResolveTick) continue;
                _hazards.RemoveAt(i);
                if (hazard.Damage <= 0) continue;
                foreach (var player in _players)
                {
                    if (!player.IsDead && hazard.Contains(player.Position, PlayerRadius)) HurtPlayer(player, hazard.Source, hazard.Damage, hazard.Cause);
                }
                _events.Add(new SimEvent(SimEventType.Impact, hazard.Source, hazard.Id, (int)MathF.Ceiling(hazard.Size)));
            }
        }

        void UpdateProjectiles()
        {
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = _projectiles[i];
                projectile.Position += projectile.Velocity * TickSeconds;
                if (--projectile.RemainingTicks <= 0 || Collision.Overlaps(Map, projectile.Position, projectile.Radius * 0.5f))
                {
                    _projectiles.RemoveAt(i);
                    continue;
                }
                var victim = _players.FirstOrDefault(p => !p.IsDead && Vec2.Distance(p.Position, projectile.Position) <= projectile.Radius + PlayerRadius);
                if (victim == null) continue;
                HurtPlayer(victim, projectile.Source, projectile.Damage, projectile.Cause);
                _projectiles.RemoveAt(i);
            }
        }
    }
}
