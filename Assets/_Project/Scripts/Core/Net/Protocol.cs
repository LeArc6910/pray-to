using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Game.Core
{
    public enum MessageType : byte
    {
        Join = 1,
        Input = 2,
        Welcome = 10,
        Refused = 11,
        Snapshot = 12,
        Roster = 13,
    }

    // Every message is one byte of MessageType followed by little-endian fields, so host and clients read them the
    // same way on every platform. Core only produces and consumes byte arrays; the transport is Unity's business.
    public static class Protocol
    {
        // Host and client must run the same rules and sheets, so a different version is refused.
        public const string Version = "0.2.0";

        public static MessageType TypeOf(byte[] data) => data != null && data.Length > 0 ? (MessageType)data[0] : 0;

        internal static byte[] Build(MessageType type, Action<BinaryWriter> write)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write((byte)type);
                write(writer);
            }
            return stream.ToArray();
        }

        internal static T Read<T>(byte[] data, MessageType type, Func<BinaryReader, T> read)
        {
            if (TypeOf(data) != type) throw new InvalidDataException($"Expected a {type} message.");
            using var reader = new BinaryReader(new MemoryStream(data, 1, data.Length - 1), Encoding.UTF8);
            return read(reader);
        }

        internal static void Write(this BinaryWriter writer, Vec2 value)
        {
            writer.Write(value.X);
            writer.Write(value.Y);
        }

        internal static Vec2 ReadVec2(this BinaryReader reader) => new Vec2(reader.ReadSingle(), reader.ReadSingle());

        // A direction squeezed into one byte, 256 steps around the circle.
        internal static byte PackDirection(Vec2 direction)
        {
            if (direction.LengthSquared < 1e-8f) return 0;
            float turns = direction.Angle / (2 * MathF.PI);
            return (byte)((int)MathF.Round(turns * 256) & 255);
        }

        internal static Vec2 UnpackDirection(byte packed) => Vec2.FromAngle(packed / 256f * 2 * MathF.PI);
    }

    public sealed class JoinMessage
    {
        public JoinMessage(string playerKey, string name, string version = Protocol.Version)
        {
            PlayerKey = playerKey ?? "";
            Name = name ?? "";
            Version = version ?? "";
        }

        public string PlayerKey { get; }
        public string Name { get; }
        public string Version { get; }

        public byte[] Encode() => Protocol.Build(MessageType.Join, w =>
        {
            w.Write(PlayerKey);
            w.Write(Name);
            w.Write(Version);
        });

        public static JoinMessage Decode(byte[] data) =>
            Protocol.Read(data, MessageType.Join, r => new JoinMessage(r.ReadString(), r.ReadString(), r.ReadString()));
    }

    // Sent every tick. Sequence counts the client's ticks, so the host knows how far the player may have moved.
    public sealed class InputMessage
    {
        public InputMessage(int sequence, Vec2 position, Vec2 facing, bool attacking)
        {
            Sequence = sequence;
            Position = position;
            Facing = facing;
            Attacking = attacking;
        }

        public int Sequence { get; }
        public Vec2 Position { get; }
        public Vec2 Facing { get; }
        public bool Attacking { get; }

        public byte[] Encode() => Protocol.Build(MessageType.Input, w =>
        {
            w.Write(Sequence);
            w.Write(Position);
            w.Write(Facing);
            w.Write(Attacking);
        });

        public static InputMessage Decode(byte[] data) =>
            Protocol.Read(data, MessageType.Input, r => new InputMessage(r.ReadInt32(), r.ReadVec2(), r.ReadVec2(), r.ReadBoolean()));
    }

    // The client builds the same caves from the seed, so the map itself is never sent.
    public sealed class WelcomeMessage
    {
        public WelcomeMessage(int playerId, ulong seed, int width, int height, Vec2 spawn)
        {
            PlayerId = playerId;
            Seed = seed;
            Width = width;
            Height = height;
            Spawn = spawn;
        }

        public int PlayerId { get; }
        public ulong Seed { get; }
        public int Width { get; }
        public int Height { get; }
        public Vec2 Spawn { get; }

        public byte[] Encode() => Protocol.Build(MessageType.Welcome, w =>
        {
            w.Write(PlayerId);
            w.Write(Seed);
            w.Write(Width);
            w.Write(Height);
            w.Write(Spawn);
        });

        public static WelcomeMessage Decode(byte[] data) =>
            Protocol.Read(data, MessageType.Welcome, r => new WelcomeMessage(r.ReadInt32(), r.ReadUInt64(), r.ReadInt32(), r.ReadInt32(), r.ReadVec2()));
    }

    public sealed class RefusedMessage
    {
        public RefusedMessage(string reason) { Reason = reason ?? ""; }

        public string Reason { get; }

        public byte[] Encode() => Protocol.Build(MessageType.Refused, w => w.Write(Reason));

        public static RefusedMessage Decode(byte[] data) =>
            Protocol.Read(data, MessageType.Refused, r => new RefusedMessage(r.ReadString()));
    }

    public sealed class RosterMessage
    {
        public RosterMessage(IReadOnlyDictionary<int, string> names) { Names = names; }

        public IReadOnlyDictionary<int, string> Names { get; }

        public byte[] Encode() => Protocol.Build(MessageType.Roster, w =>
        {
            w.Write(Names.Count);
            foreach (var pair in Names)
            {
                w.Write(pair.Key);
                w.Write(pair.Value);
            }
        });

        public static RosterMessage Decode(byte[] data) => Protocol.Read(data, MessageType.Roster, r =>
        {
            int count = r.ReadInt32();
            var names = new Dictionary<int, string>();
            for (int i = 0; i < count; i++) names[r.ReadInt32()] = r.ReadString();
            return new RosterMessage(names);
        });
    }

    // Monster is an index into the monster sheet, which both sides load; -1 means a player.
    public readonly struct EntityState
    {
        public EntityState(int id, int monster, Vec2 position, Vec2 facing, float health, float maxHealth, EnemyState state)
        {
            Id = id;
            Monster = monster;
            Position = position;
            Facing = facing;
            Health = health;
            MaxHealth = maxHealth;
            State = state;
        }

        public int Id { get; }
        public int Monster { get; }
        public bool IsPlayer => Monster < 0;
        public Vec2 Position { get; }
        public Vec2 Facing { get; }
        public float Health { get; }
        public float MaxHealth { get; }
        public EnemyState State { get; }
        public bool IsDead => Health <= 0;
    }

    // What only the receiving player needs about themselves.
    public readonly struct SelfState
    {
        public SelfState(int corrections, Vec2 position, float health, float maxHealth, float moveSpeed)
        {
            Corrections = corrections;
            Position = position;
            Health = health;
            MaxHealth = maxHealth;
            MoveSpeed = moveSpeed;
        }

        public int Corrections { get; }
        public Vec2 Position { get; }
        public float Health { get; }
        public float MaxHealth { get; }
        public float MoveSpeed { get; }
        public bool IsDead => Health <= 0;
    }

    public sealed class SnapshotMessage
    {
        public SnapshotMessage(long tick, SelfState self, IReadOnlyList<EntityState> entities, IReadOnlyList<SimEvent> events)
        {
            Tick = tick;
            Self = self;
            Entities = entities;
            Events = events;
        }

        public long Tick { get; }
        public SelfState Self { get; }
        public IReadOnlyList<EntityState> Entities { get; }
        public IReadOnlyList<SimEvent> Events { get; }

        public byte[] Encode() => Protocol.Build(MessageType.Snapshot, w =>
        {
            w.Write(Tick);
            w.Write(Self.Corrections);
            w.Write(Self.Position);
            w.Write(Self.Health);
            w.Write(Self.MaxHealth);
            w.Write(Self.MoveSpeed);

            w.Write((ushort)Entities.Count);
            foreach (var entity in Entities)
            {
                w.Write(entity.Id);
                w.Write((short)entity.Monster);
                w.Write(entity.Position);
                w.Write(Protocol.PackDirection(entity.Facing));
                w.Write(entity.Health);
                w.Write(entity.MaxHealth);
                w.Write((byte)entity.State);
            }

            w.Write((ushort)Events.Count);
            foreach (var e in Events)
            {
                w.Write((byte)e.Type);
                w.Write(e.Source);
                w.Write(e.Target);
                w.Write(e.Amount);
                w.Write(e.Critical);
                w.Write(Protocol.PackDirection(e.Direction));
            }
        });

        public static SnapshotMessage Decode(byte[] data) => Protocol.Read(data, MessageType.Snapshot, r =>
        {
            long tick = r.ReadInt64();
            var self = new SelfState(r.ReadInt32(), r.ReadVec2(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

            int entityCount = r.ReadUInt16();
            var entities = new List<EntityState>(entityCount);
            for (int i = 0; i < entityCount; i++)
            {
                entities.Add(new EntityState(
                    r.ReadInt32(),
                    r.ReadInt16(),
                    r.ReadVec2(),
                    Protocol.UnpackDirection(r.ReadByte()),
                    r.ReadSingle(),
                    r.ReadSingle(),
                    (EnemyState)r.ReadByte()));
            }

            int eventCount = r.ReadUInt16();
            var events = new List<SimEvent>(eventCount);
            for (int i = 0; i < eventCount; i++)
            {
                var type = (SimEventType)r.ReadByte();
                int source = r.ReadInt32();
                int target = r.ReadInt32();
                int amount = r.ReadInt32();
                bool critical = r.ReadBoolean();
                events.Add(new SimEvent(type, source, target, amount, critical, Protocol.UnpackDirection(r.ReadByte())));
            }
            return new SnapshotMessage(tick, self, entities, events);
        });
    }
}
