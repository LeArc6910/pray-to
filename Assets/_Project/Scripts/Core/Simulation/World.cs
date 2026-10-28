using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public sealed class WorldConfig
    {
        public int EnemyTarget { get; set; } = 24;
        public float SpawnIntervalSeconds { get; set; } = 2;
        // New enemies appear at least this far from every player and from the spawn point.
        public float SpawnDistance { get; set; } = 12;
        public float RespawnSeconds { get; set; } = 5;
        public int ZoneLevel { get; set; } = 1;
        // A defeated boss returns to its arena after this long.
        public float BossRespawnSeconds { get; set; } = 120;
    }

    // The host's simulation. It steps at a fixed TicksPerSecond and is the only thing that changes game state;
    // the one exception is a player's own position, which its client reports (see MovePlayer). Bosses, their
    // telegraphed areas and projectiles are in World.Boss.cs; drops, pickups and inventory commands in World.Loot.cs;
    // players joining with their classes and using skills in World.Skills.cs.
    public sealed partial class World
    {
        public const int TicksPerSecond = 30;
        public const float TickSeconds = 1f / TicksPerSecond;
        public const float PlayerRadius = 0.35f;
        // How far past the player's edge a swing reaches, and how wide it is.
        public const float MeleeReach = 1.1f;
        public const float MeleeArcDegrees = 120;
        // Enemies chase until the player is this many times their aggro range away.
        const float LoseInterest = 1.5f;
        const float KnockbackSpeed = 7;
        const float KnockbackDecay = 0.75f;
        const int FlowFieldInterval = 10;
        const int FlowFieldRange = 24;
        // Reach is measured from the player's position when the swing started; give a little slack for
        // the enemy and player having moved during the wind-up.
        const float StrikeSlack = 0.2f;

        readonly List<Player> _players = new List<Player>();
        readonly List<Enemy> _enemies = new List<Enemy>();
        readonly List<SimEvent> _events = new List<SimEvent>();
        readonly MonsterData _monsters;
        readonly WorldConfig _config;
        readonly GameRandom _random;
        readonly FlowField _flow;
        int _nextId = 1;
        int _spawnTimer;

        // Without loot data nothing drops.
        public World(TileMap map, Vec2 spawn, MonsterData monsters, ulong seed, WorldConfig config = null, LootData loot = null)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Spawn = spawn;
            _monsters = monsters ?? throw new ArgumentNullException(nameof(monsters));
            _config = config ?? new WorldConfig();
            _random = new GameRandom(seed, stream: 2);
            Loot = loot;
            _lootGenerator = loot != null ? new LootGenerator(loot) : null;
            _flow = new FlowField(map);
            for (int i = 0; i < _config.EnemyTarget; i++) TrySpawnEnemy();
        }

        public TileMap Map { get; }
        public Vec2 Spawn { get; }
        public long Tick { get; private set; }
        public IReadOnlyList<Player> Players => _players;
        public IReadOnlyList<Enemy> Enemies => _enemies;

        public static int Ticks(float seconds) => Math.Max(1, (int)MathF.Round(seconds * TicksPerSecond));

        public void RemovePlayer(int id) => _players.RemoveAll(p => p.Id == id);

        public Player FindPlayer(int id) => _players.FirstOrDefault(p => p.Id == id);

        public Enemy FindEnemy(int id) => _enemies.FirstOrDefault(e => e.Id == id);

        public Enemy SpawnEnemy(MonsterDefinition definition, Vec2 position)
        {
            var enemy = new Enemy(_nextId++, definition, position);
            _enemies.Add(enemy);
            return enemy;
        }

        // The owning client moves its player with PlayerMotor and reports where it ended up, so its own movement
        // never waits for the network. The host accepts the report unless the player could not have got there in
        // the elapsed ticks or it overlaps a wall; then the player stays put and the client is corrected.
        public bool MovePlayer(int id, Vec2 reported, Vec2 facing, int elapsedTicks)
        {
            var player = FindPlayer(id);
            if (player == null) return false;
            if (facing.LengthSquared > 1e-6f && !player.IsDead) player.Facing = facing.Normalized;
            if (reported.Equals(player.Position)) return true;

            float allowed = player.Stats.MoveSpeed * TickSeconds * Math.Clamp(elapsedTicks, 1, 10) * 1.25f + 0.05f;
            bool valid = !player.IsDead
                && reported.IsFinite
                && Vec2.Distance(player.Position, reported) <= allowed
                && !Collision.Overlaps(Map, reported, PlayerRadius - 0.01f);
            if (valid)
            {
                player.Position = reported;
                return true;
            }
            player.Corrections++;
            return false;
        }

        // While held, the player swings whenever the weapon is ready.
        public void SetAttacking(int id, bool attacking)
        {
            var player = FindPlayer(id);
            if (player != null) player.Attacking = attacking;
        }

        public List<SimEvent> TakeEvents()
        {
            var events = new List<SimEvent>(_events);
            _events.Clear();
            return events;
        }

        public void Step()
        {
            Tick++;
            foreach (var player in _players) UpdatePlayer(player);
            _enemies.RemoveAll(e => e.IsDead);
            RemoveDefeatedBosses();

            if (Tick % FlowFieldInterval == 1)
            {
                _flow.Build(_players.Where(p => !p.IsDead).Select(p => p.Position), FlowFieldRange);
            }
            foreach (var enemy in _enemies) UpdateEnemy(enemy);
            UpdateBosses();
            UpdateHazards();
            UpdateProjectiles();
            UpdateLoot();
            SeparateEnemies();

            if (--_spawnTimer <= 0)
            {
                _spawnTimer = Ticks(_config.SpawnIntervalSeconds);
                if (_enemies.Count < _config.EnemyTarget) TrySpawnEnemy();
            }
        }

        void UpdatePlayer(Player player)
        {
            player.TickSkills();
            if (player.IsDead)
            {
                if (--player.RespawnTicks > 0) return;
                player.Position = Spawn;
                player.Health = player.Stats.MaxHealth;
                player.Corrections++;
                return;
            }

            player.Health = Math.Min(player.Stats.MaxHealth, player.Health + player.Stats.HealthRegen * TickSeconds);
            if (player.AttackCooldown > 0) player.AttackCooldown--;
            if (player.Attacking && player.AttackCooldown == 0) Swing(player);
        }

        void Swing(Player player)
        {
            var stats = player.Stats;
            player.AttackCooldown = Math.Max(1, (int)MathF.Round(TicksPerSecond / Math.Max(0.1f, stats.AttacksPerSecond)));
            _events.Add(new SimEvent(SimEventType.Attack, player.Id, direction: player.Facing));

            foreach (var enemy in _enemies)
            {
                if (enemy.IsDead) continue;
                if (!Combat.InArc(player.Position, player.Facing, enemy.Position, enemy.Definition.Radius, MeleeReach, MeleeArcDegrees)) continue;

                int amount = RollDamage(stats, enemy.Definition.Armor, out bool critical);
                HitEnemy(player, enemy, amount, critical, player.Position);
            }

            foreach (var boss in _bosses)
            {
                if (boss.IsDead) continue;
                if (!Combat.InArc(player.Position, player.Facing, boss.Position, boss.Definition.Radius, MeleeReach, MeleeArcDegrees)) continue;
                if (boss.State == BossState.Dormant) Wake(boss);
                if (boss.Invulnerable) continue;
                int amount = RollDamage(stats, boss.Definition.Armor, out bool critical);
                DamageBoss(boss, player.Id, amount, critical);
            }
        }

        int RollDamage(PlayerStats stats, float armor, out bool critical)
        {
            critical = _random.Chance(stats.CritChance / 100f);
            return Combat.Mitigate(stats.Damage * (critical ? stats.CritDamage / 100f : 1f), armor);
        }

        void UpdateEnemy(Enemy enemy)
        {
            var definition = enemy.Definition;
            if (enemy.Knockback.LengthSquared > 0.01f)
            {
                enemy.Position = Collision.Move(Map, enemy.Position, definition.Radius, enemy.Knockback * TickSeconds);
                enemy.Knockback *= KnockbackDecay;
            }

            var target = FindPlayer(enemy.TargetId);
            switch (enemy.State)
            {
                case EnemyState.Idle:
                    var nearest = _players
                        .Where(p => !p.IsDead && Vec2.Distance(p.Position, enemy.Position) <= definition.Aggro)
                        .OrderBy(p => Vec2.Distance(p.Position, enemy.Position))
                        .FirstOrDefault();
                    if (nearest == null) break;
                    enemy.TargetId = nearest.Id;
                    enemy.State = EnemyState.Chase;
                    break;

                case EnemyState.Chase:
                    if (target == null || target.IsDead || Vec2.Distance(target.Position, enemy.Position) > definition.Aggro * LoseInterest)
                    {
                        enemy.State = EnemyState.Idle;
                        break;
                    }
                    var toTarget = target.Position - enemy.Position;
                    if (Gap(enemy, target) <= definition.Reach)
                    {
                        enemy.Facing = toTarget.Normalized;
                        enemy.State = EnemyState.Windup;
                        enemy.StateTicks = Ticks(definition.Windup);
                        break;
                    }
                    var direction = Gap(enemy, target) < 1.5f ? toTarget.Normalized : _flow.DirectionFrom(enemy.Position);
                    if (direction.LengthSquared < 1e-6f) direction = toTarget.Normalized;
                    enemy.Facing = direction;
                    enemy.Position = Collision.Move(Map, enemy.Position, definition.Radius, direction * (definition.Speed * TickSeconds));
                    break;

                case EnemyState.Windup:
                    if (--enemy.StateTicks > 0) break;
                    _events.Add(new SimEvent(SimEventType.Attack, enemy.Id, direction: enemy.Facing));
                    if (target != null && !target.IsDead && Gap(enemy, target) <= definition.Reach + StrikeSlack) HurtPlayer(target, enemy.Id, definition.Damage);
                    enemy.State = EnemyState.Recover;
                    enemy.StateTicks = Ticks(definition.Cooldown);
                    break;

                case EnemyState.Recover:
                    if (--enemy.StateTicks <= 0) enemy.State = EnemyState.Chase;
                    break;
            }
        }

        void HurtPlayer(Player player, int source, float damage, int cause = 0)
        {
            if (player.IsDead) return;
            int amount = Combat.Mitigate(damage, player.Stats.Armor);
            player.Health -= amount;
            _events.Add(new SimEvent(SimEventType.Hit, source, player.Id, amount, cause: cause));
            if (!player.IsDead) return;

            player.Health = 0;
            player.Attacking = false;
            player.ClearBuffs();
            player.RespawnTicks = Ticks(_config.RespawnSeconds);
            _events.Add(new SimEvent(SimEventType.Died, player.Id));
        }

        static float Gap(Enemy enemy, Player player) =>
            Vec2.Distance(enemy.Position, player.Position) - enemy.Definition.Radius - PlayerRadius;

        // Overlapping enemies push each other apart so a crowd spreads out instead of stacking on one tile.
        void SeparateEnemies()
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                for (int j = i + 1; j < _enemies.Count; j++)
                {
                    var a = _enemies[i];
                    var b = _enemies[j];
                    var apart = b.Position - a.Position;
                    float minimum = a.Definition.Radius + b.Definition.Radius;
                    float distance = apart.Length;
                    if (distance >= minimum) continue;

                    var push = (distance > 1e-4f ? apart / distance : new Vec2(1, 0)) * ((minimum - distance) / 2);
                    a.Position = Collision.Resolve(Map, a.Position - push, a.Definition.Radius);
                    b.Position = Collision.Resolve(Map, b.Position + push, b.Definition.Radius);
                }
            }
        }

        void TrySpawnEnemy()
        {
            var choices = _monsters.Monsters.Where(m => m.Level <= _config.ZoneLevel && m.Weight > 0).ToList();
            if (choices.Count == 0) return;

            for (int attempt = 0; attempt < 30; attempt++)
            {
                int x = _random.Next(Map.Width);
                int y = _random.Next(Map.Height);
                var position = TileMap.Center(x, y);
                if (Map.IsSolid(x, y)
                    || Vec2.Distance(position, Spawn) < _config.SpawnDistance
                    || _players.Any(p => Vec2.Distance(p.Position, position) < _config.SpawnDistance))
                {
                    continue;
                }
                SpawnEnemy(_random.PickWeighted(choices, m => m.Weight), position);
                return;
            }
        }
    }

    public static class PlayerMotor
    {
        // One tick of a player's own movement. Input is the stick or WASD direction; longer than 1 is clamped.
        public static Vec2 Step(TileMap map, Vec2 position, Vec2 input, float moveSpeed) =>
            Collision.Move(map, position, World.PlayerRadius, input.ClampLength(1) * (moveSpeed * World.TickSeconds));
    }

    public static class Combat
    {
        // Armor 100 halves damage and 300 quarters it, so it never blocks everything. A hit always does at least 1.
        public static int Mitigate(float damage, float armor) =>
            Math.Max(1, (int)MathF.Round(damage * 100f / (100f + Math.Max(0, armor))));

        public static bool InArc(Vec2 origin, Vec2 facing, Vec2 target, float targetRadius, float reach, float arcDegrees)
        {
            var toTarget = target - origin;
            float distance = toTarget.Length;
            if (distance - targetRadius - World.PlayerRadius > reach) return false;
            if (distance < 1e-4f) return true;
            float cosine = Vec2.Dot(toTarget / distance, facing.Normalized);
            return cosine >= MathF.Cos(arcDegrees * 0.5f * MathF.PI / 180f);
        }
    }
}
