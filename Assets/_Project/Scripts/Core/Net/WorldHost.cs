using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Core
{
    public sealed class HostConfig
    {
        public int MaxPlayers { get; set; } = 4;
        // Ticks between snapshots. Clients interpolate between them.
        public int SnapshotInterval { get; set; } = 2;
        // Entities further than this from a player are left out of that player's snapshots.
        public float ViewRange { get; set; } = 22;
        // These keep a snapshot inside one network packet (Netcode's limit is about 1250 bytes). At 17 bytes an
        // entity, 13 a projectile, 19 a hazard and 13 an event, the maximum comes to about 1.15 KB.
        public int MaxEntitiesPerSnapshot { get; set; } = 24;
        public int MaxProjectilesPerSnapshot { get; set; } = 24;
        public int MaxHazardsPerSnapshot { get; set; } = 10;
        public int MaxEventsPerSnapshot { get; set; } = 16;
        public string[] StarterGear { get; set; } = { "copper_sword" };
    }

    public readonly struct Outgoing
    {
        public Outgoing(ulong connection, byte[] data, bool reliable)
        {
            Connection = connection;
            Data = data;
            Reliable = reliable;
        }

        public ulong Connection { get; }
        public byte[] Data { get; }
        // Join replies and the roster must arrive; snapshots are replaced by the next one anyway.
        public bool Reliable { get; }
    }

    // Runs the world for everyone. The transport hands it whatever arrives with Receive and Disconnect, calls Tick
    // TicksPerSecond times a second, and sends whatever TakeOutgoing returns. A solo game is a host whose only
    // connection is the local player.
    public sealed class WorldHost
    {
        sealed class Connection
        {
            public Player Player;
            public int LastSequence;
        }

        readonly Dictionary<ulong, Connection> _connections = new Dictionary<ulong, Connection>();
        readonly List<Outgoing> _outgoing = new List<Outgoing>();
        readonly List<SimEvent> _pendingEvents = new List<SimEvent>();
        readonly MonsterData _monsters;
        readonly BossData _bosses;
        readonly LootData _loot;
        readonly HostConfig _config;
        readonly GameRandom _gearRandom;
        readonly ulong _seed;
        readonly GeneratedWorld _generated;

        // The first boss in the sheet waits in the arena; without boss data the caves have none.
        public WorldHost(ulong seed, MonsterData monsters, LootData loot, HostConfig config = null, WorldConfig worldConfig = null, BossData bosses = null)
        {
            _seed = seed;
            _monsters = monsters ?? throw new ArgumentNullException(nameof(monsters));
            _bosses = bosses;
            _loot = loot ?? throw new ArgumentNullException(nameof(loot));
            _config = config ?? new HostConfig();
            _gearRandom = new GameRandom(seed, stream: 3);
            _generated = WorldGenerator.Generate(seed);
            World = new World(_generated.Map, _generated.Spawn, monsters, seed, worldConfig);
            if (bosses != null && bosses.Bosses.Count > 0) World.PlaceBoss(bosses.Bosses[0], _generated.Arena, _generated.ArenaRadius);
        }

        public World World { get; }
        public int PlayerCount => _connections.Values.Count(c => c.Player != null);

        public void Receive(ulong connection, byte[] data)
        {
            try
            {
                switch (Protocol.TypeOf(data))
                {
                    case MessageType.Join:
                        Join(connection, JoinMessage.Decode(data));
                        break;
                    case MessageType.Input:
                        if (_connections.TryGetValue(connection, out var sender)) ApplyInput(sender, InputMessage.Decode(data));
                        break;
                }
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException)
            {
                // A truncated or garbled message from one client must not take the host down; drop it.
            }
        }

        public void Disconnect(ulong connection)
        {
            if (!_connections.Remove(connection, out var gone) || gone.Player == null) return;
            World.RemovePlayer(gone.Player.Id);
            BroadcastRoster();
        }

        public void Tick()
        {
            World.Step();
            _pendingEvents.AddRange(World.TakeEvents());
            if (World.Tick % _config.SnapshotInterval != 0) return;

            foreach (var pair in _connections)
            {
                if (pair.Value.Player != null) Send(pair.Key, Snapshot(pair.Value.Player).Encode(), reliable: false);
            }
            _pendingEvents.Clear();
        }

        public List<Outgoing> TakeOutgoing()
        {
            var outgoing = new List<Outgoing>(_outgoing);
            _outgoing.Clear();
            return outgoing;
        }

        void Join(ulong connection, JoinMessage join)
        {
            if (_connections.TryGetValue(connection, out var existing) && existing.Player != null) return;
            if (join.Version != Protocol.Version)
            {
                Send(connection, new RefusedMessage($"버전이 다릅니다. 호스트 {Protocol.Version}, 내 게임 {join.Version}").Encode(), reliable: true);
                return;
            }
            if (PlayerCount >= _config.MaxPlayers)
            {
                Send(connection, new RefusedMessage($"자리가 없습니다 (최대 {_config.MaxPlayers}명).").Encode(), reliable: true);
                return;
            }

            var equipment = new Equipment();
            foreach (string id in _config.StarterGear)
            {
                var itemBase = _loot.FindBase(id);
                equipment.Equip(new LootGenerator(_loot).RollItem(itemBase, itemBase.Level, Rarity.Common, _gearRandom));
            }
            string name = string.IsNullOrWhiteSpace(join.Name) ? "플레이어" : join.Name.Trim();
            var player = World.AddPlayer(name.Length > 16 ? name.Substring(0, 16) : name, equipment);
            _connections[connection] = new Connection { Player = player };

            var map = _generated.Map;
            Send(connection, new WelcomeMessage(player.Id, _seed, map.Width, map.Height, player.Position).Encode(), reliable: true);
            BroadcastRoster();
        }

        void ApplyInput(Connection sender, InputMessage input)
        {
            if (sender.Player == null || input.Sequence <= sender.LastSequence) return;
            int elapsed = sender.LastSequence == 0 ? 1 : input.Sequence - sender.LastSequence;
            sender.LastSequence = input.Sequence;
            World.MovePlayer(sender.Player.Id, input.Position, input.Facing, elapsed);
            World.SetAttacking(sender.Player.Id, input.Attacking);
        }

        SnapshotMessage Snapshot(Player viewer)
        {
            float Distance(Vec2 position) => Vec2.Distance(position, viewer.Position);
            bool InView(Vec2 position, float size = 0) => Distance(position) <= _config.ViewRange + size;

            var self = new SelfState(viewer.Corrections, viewer.Position, viewer.Health, viewer.Stats.MaxHealth, viewer.Stats.MoveSpeed);
            var players = World.Players.Select(p => new EntityState(
                p.Id, EntityKind.Player, 0, p.Position, p.Facing, p.Health, p.Stats.MaxHealth, 0));
            var bosses = World.Bosses
                .Where(b => !b.IsDead && InView(b.Position, b.Definition.Radius))
                .Select(b => new EntityState(
                    b.Id, EntityKind.Boss, IndexOf(_bosses?.Bosses, b.Definition), b.Position, b.Facing, b.Health, b.MaxHealth, (byte)b.State, b.Phase));
            var shownSoFar = players.Concat(bosses).ToList();
            var enemies = World.Enemies
                .Where(e => InView(e.Position))
                .OrderBy(e => Distance(e.Position))
                .Take(Math.Max(0, _config.MaxEntitiesPerSnapshot - shownSoFar.Count))
                .Select(e => new EntityState(
                    e.Id, EntityKind.Monster, IndexOf(_monsters.Monsters, e.Definition), e.Position, e.Facing, e.Health, e.Definition.Health, (byte)e.State));
            var entities = shownSoFar.Concat(enemies).ToList();

            var projectiles = World.Projectiles
                .Where(p => InView(p.Position))
                .OrderBy(p => Distance(p.Position))
                .Take(_config.MaxProjectilesPerSnapshot)
                .Select(p => new ProjectileState(p.Id, p.Position, p.Velocity, p.Radius))
                .ToList();

            var hazards = World.Hazards
                .Where(h => InView(h.Position, h.Size))
                .OrderBy(h => h.ResolveTick)
                .Take(_config.MaxHazardsPerSnapshot)
                .Select(h => new HazardState(
                    h.Id, h.Shape, h.Position, h.Direction, h.Size, h.Width,
                    (int)(h.ResolveTick - World.Tick), (int)(h.ResolveTick - h.StartTick), h.Damage > 0))
                .ToList();

            // When there are too many, what changes the fight matters most and swings least.
            var shown = new HashSet<int>(entities.Select(e => e.Id));
            var events = _pendingEvents
                .Where(e => shown.Contains(e.Source) || shown.Contains(e.Target))
                .OrderBy(e => e.Type == SimEventType.Attack ? 2 : e.Type == SimEventType.Hit ? 1 : 0)
                .Take(_config.MaxEventsPerSnapshot)
                .ToList();
            return new SnapshotMessage(World.Tick, self, entities, projectiles, hazards, events);
        }

        static int IndexOf<T>(IReadOnlyList<T> list, T item) where T : class
        {
            for (int i = 0; list != null && i < list.Count; i++)
            {
                if (list[i] == item) return i;
            }
            throw new InvalidOperationException($"{typeof(T).Name} is not in the host's sheets, so clients could not name it.");
        }

        void BroadcastRoster()
        {
            var names = _connections.Values.Where(c => c.Player != null).ToDictionary(c => c.Player.Id, c => c.Player.Name);
            byte[] roster = new RosterMessage(names).Encode();
            foreach (var pair in _connections)
            {
                if (pair.Value.Player != null) Send(pair.Key, roster, reliable: true);
            }
        }

        void Send(ulong connection, byte[] data, bool reliable) => _outgoing.Add(new Outgoing(connection, data, reliable));
    }
}
