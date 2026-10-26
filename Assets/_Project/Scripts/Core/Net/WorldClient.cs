using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Core
{
    public enum ClientStatus
    {
        Joining,
        Playing,
        Refused,
    }

    // What the screen draws for one entity, already interpolated.
    public readonly struct EntityView
    {
        public EntityView(
            int id,
            EntityKind kind,
            MonsterDefinition monster,
            BossDefinition boss,
            string name,
            Vec2 position,
            Vec2 facing,
            float health,
            float maxHealth,
            byte state,
            int phase)
        {
            Id = id;
            Kind = kind;
            Monster = monster;
            Boss = boss;
            Name = name;
            Position = position;
            Facing = facing;
            Health = health;
            MaxHealth = maxHealth;
            State = state;
            Phase = phase;
        }

        public int Id { get; }
        public EntityKind Kind { get; }
        public bool IsPlayer => Kind == EntityKind.Player;
        // Set for monsters and bosses respectively.
        public MonsterDefinition Monster { get; }
        public BossDefinition Boss { get; }
        public string Name { get; }
        public Vec2 Position { get; }
        public Vec2 Facing { get; }
        public float Health { get; }
        public float MaxHealth { get; }
        public byte State { get; }
        public EnemyState EnemyState => (EnemyState)State;
        public BossState BossState => (BossState)State;
        public int Phase { get; }
        public bool IsDead => Health <= 0;
        public float Radius => Kind == EntityKind.Boss ? Boss.Radius : Kind == EntityKind.Monster ? Monster.Radius : World.PlayerRadius;
    }

    public readonly struct ProjectileView
    {
        public ProjectileView(int id, Vec2 position, float radius)
        {
            Id = id;
            Position = position;
            Radius = radius;
        }

        public int Id { get; }
        public Vec2 Position { get; }
        public float Radius { get; }
    }

    public readonly struct HazardView
    {
        public HazardView(HazardState state, float progress)
        {
            State = state;
            Progress = progress;
        }

        public HazardState State { get; }
        // 0 when it appears, 1 when it lands.
        public float Progress { get; }
    }

    // One player's side of the game. It moves its own player at once with the same PlayerMotor the host uses and
    // reports the result, and shows everyone else a little in the past, smoothly between the host's snapshots.
    // Telegraphs and projectiles are shown at the host's present instead, because the player dodges them now.
    public sealed class WorldClient
    {
        // How far behind the newest snapshot other entities are drawn. Two snapshot intervals and a bit, so one
        // late or lost snapshot does not make them stop.
        const double InterpolationDelayTicks = 5;
        const int KeptSnapshots = 12;
        // Without new snapshots, guessing the host's clock further ahead than this does more harm than good.
        const float MaxGuessSeconds = 0.25f;

        readonly MonsterData _monsters;
        readonly BossData _bosses;
        readonly List<SnapshotMessage> _snapshots = new List<SnapshotMessage>();
        readonly List<SimEvent> _events = new List<SimEvent>();
        readonly Dictionary<int, string> _names = new Dictionary<int, string>();
        int _sequence;
        int _seenCorrections;
        double _renderTick = -1;
        float _sinceLatest;

        public WorldClient(string playerKey, string name, MonsterData monsters, BossData bosses = null)
        {
            PlayerKey = playerKey ?? "";
            Name = name ?? "";
            _monsters = monsters ?? throw new ArgumentNullException(nameof(monsters));
            _bosses = bosses;
        }

        public string PlayerKey { get; }
        public string Name { get; }
        public ClientStatus Status { get; private set; } = ClientStatus.Joining;
        public string RefusedReason { get; private set; }
        public int PlayerId { get; private set; }
        public TileMap Map { get; private set; }
        // Where the boss's arena is, known from the seed like the rest of the map.
        public Vec2 Arena { get; private set; }
        public float ArenaRadius { get; private set; }

        // The local player, moved here first; PreviousPosition is where it was one tick earlier, for smoothing.
        public Vec2 Position { get; private set; }
        public Vec2 PreviousPosition { get; private set; }
        public Vec2 Facing { get; private set; } = new Vec2(0, -1);
        public float Health { get; private set; }
        public float MaxHealth { get; private set; } = 1;
        public float MoveSpeed { get; private set; } = PlayerStats.BaseMoveSpeed;
        public bool IsDead => Status == ClientStatus.Playing && _snapshots.Count > 0 && Health <= 0;
        public IReadOnlyDictionary<int, string> Names => _names;

        public byte[] JoinMessage() => new JoinMessage(PlayerKey, Name).Encode();

        public void Receive(byte[] data)
        {
            try
            {
                switch (Protocol.TypeOf(data))
                {
                    case MessageType.Welcome:
                        Welcome(WelcomeMessage.Decode(data));
                        break;
                    case MessageType.Refused:
                        Status = ClientStatus.Refused;
                        RefusedReason = RefusedMessage.Decode(data).Reason;
                        break;
                    case MessageType.Roster:
                        _names.Clear();
                        foreach (var pair in RosterMessage.Decode(data).Names) _names[pair.Key] = pair.Value;
                        break;
                    case MessageType.Snapshot:
                        if (Status == ClientStatus.Playing) Apply(SnapshotMessage.Decode(data));
                        break;
                }
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException)
            {
                // A garbled message is dropped; the next snapshot replaces it.
            }
        }

        // One fixed tick of the local player. Returns the input message to send to the host, or null before joining.
        public byte[] Step(Vec2 move, Vec2 aim, bool attack)
        {
            if (Status != ClientStatus.Playing) return null;
            PreviousPosition = Position;
            if (!IsDead)
            {
                Position = PlayerMotor.Step(Map, Position, move, MoveSpeed);
                if (aim.LengthSquared > 1e-6f) Facing = aim.Normalized;
            }
            _sequence++;
            return new InputMessage(_sequence, Position, Facing, attack && !IsDead).Encode();
        }

        // Moves the clock that other entities are drawn at. Call once per rendered frame.
        public void AdvanceRenderClock(float seconds)
        {
            if (_snapshots.Count == 0) return;
            _sinceLatest += seconds;
            double target = _snapshots[_snapshots.Count - 1].Tick - InterpolationDelayTicks;
            _renderTick += seconds * World.TicksPerSecond;
            // Far off (first snapshot, a stall, a burst) jumps; a small drift is eased out so motion stays smooth.
            if (Math.Abs(_renderTick - target) > World.TicksPerSecond / 2.0) _renderTick = target;
            else _renderTick += (target - _renderTick) * 0.05;
        }

        // Everyone but the local player, interpolated at the render clock.
        public List<EntityView> Others()
        {
            var views = new List<EntityView>();
            if (_snapshots.Count == 0) return views;

            int after = _snapshots.FindIndex(s => s.Tick > _renderTick);
            var newer = after < 0 ? _snapshots[_snapshots.Count - 1] : _snapshots[after];
            var older = after <= 0 ? newer : _snapshots[after - 1];
            float t = newer.Tick == older.Tick ? 1 : (float)Math.Clamp((_renderTick - older.Tick) / (newer.Tick - older.Tick), 0, 1);
            var before = older.Entities.ToDictionary(e => e.Id);

            foreach (var entity in newer.Entities)
            {
                if (entity.Id == PlayerId) continue;
                var position = before.TryGetValue(entity.Id, out var then) ? Vec2.Lerp(then.Position, entity.Position, t) : entity.Position;
                var shown = t < 0.5f && before.ContainsKey(entity.Id) ? then : entity;
                views.Add(new EntityView(
                    entity.Id,
                    entity.Kind,
                    entity.Kind == EntityKind.Monster ? _monsters.Monsters[entity.Type] : null,
                    entity.Kind == EntityKind.Boss ? _bosses.Bosses[entity.Type] : null,
                    entity.IsPlayer && _names.TryGetValue(entity.Id, out string name) ? name : "",
                    position,
                    shown.Facing,
                    shown.Health,
                    shown.MaxHealth,
                    shown.State,
                    shown.Phase));
            }
            return views;
        }

        // The host's current tick as best this client can tell: the newest snapshot plus the time since it came.
        public double HostTick =>
            _snapshots.Count == 0 ? 0 : _snapshots[_snapshots.Count - 1].Tick + Math.Min(_sinceLatest, MaxGuessSeconds) * World.TicksPerSecond;

        // Projectiles moved on to where they are now.
        public List<ProjectileView> Projectiles()
        {
            var views = new List<ProjectileView>();
            if (_snapshots.Count == 0) return views;
            var latest = _snapshots[_snapshots.Count - 1];
            float seconds = (float)((HostTick - latest.Tick) / World.TicksPerSecond);
            foreach (var projectile in latest.Projectiles)
            {
                views.Add(new ProjectileView(projectile.Id, projectile.Position + projectile.Velocity * seconds, projectile.Radius));
            }
            return views;
        }

        // Telegraphs with how far along they are right now; ones that have already landed are left out.
        public List<HazardView> Hazards()
        {
            var views = new List<HazardView>();
            if (_snapshots.Count == 0) return views;
            var latest = _snapshots[_snapshots.Count - 1];
            double now = HostTick;
            foreach (var hazard in latest.Hazards)
            {
                double lands = latest.Tick + hazard.TicksLeft;
                if (now > lands) continue;
                double starts = lands - Math.Max(1, hazard.Duration);
                views.Add(new HazardView(hazard, (float)Math.Clamp((now - starts) / Math.Max(1, hazard.Duration), 0, 1)));
            }
            return views;
        }

        public List<SimEvent> TakeEvents()
        {
            var events = new List<SimEvent>(_events);
            _events.Clear();
            return events;
        }

        void Welcome(WelcomeMessage welcome)
        {
            PlayerId = welcome.PlayerId;
            var generated = WorldGenerator.Generate(welcome.Seed, welcome.Width, welcome.Height);
            Map = generated.Map;
            Arena = generated.Arena;
            ArenaRadius = generated.ArenaRadius;
            Position = welcome.Spawn;
            PreviousPosition = welcome.Spawn;
            Status = ClientStatus.Playing;
        }

        void Apply(SnapshotMessage snapshot)
        {
            if (_snapshots.Count > 0 && snapshot.Tick <= _snapshots[_snapshots.Count - 1].Tick) return;
            if (snapshot.Entities.Any(e => e.Kind == EntityKind.Monster && e.Type >= _monsters.Monsters.Count))
                throw new InvalidDataException("Snapshot names a monster this client does not know.");
            if (snapshot.Entities.Any(e => e.Kind == EntityKind.Boss && (_bosses == null || e.Type >= _bosses.Bosses.Count)))
                throw new InvalidDataException("Snapshot names a boss this client does not know.");

            _snapshots.Add(snapshot);
            _sinceLatest = 0;
            if (_snapshots.Count > KeptSnapshots) _snapshots.RemoveAt(0);
            _events.AddRange(snapshot.Events);

            var self = snapshot.Self;
            Health = self.Health;
            MaxHealth = self.MaxHealth;
            MoveSpeed = self.MoveSpeed;
            if (self.Corrections != _seenCorrections)
            {
                _seenCorrections = self.Corrections;
                Position = self.Position;
                PreviousPosition = self.Position;
            }
        }
    }
}
