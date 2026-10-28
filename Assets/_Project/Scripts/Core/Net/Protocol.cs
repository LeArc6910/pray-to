using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Game.Core
{
    public enum MessageType : byte
    {
        Join = 1,
        Input = 2,
        InventoryCommand = 3,
        SkillCommand = 4,
        Welcome = 10,
        Refused = 11,
        Snapshot = 12,
        Roster = 13,
        Inventory = 14,
    }

    // Every message is one byte of MessageType followed by little-endian fields, so host and clients read them the
    // same way on every platform. Core only produces and consumes byte arrays; the transport is Unity's business.
    public static class Protocol
    {
        // Host and client must run the same rules and sheets, so a different version is refused.
        public const string Version = "0.5.0";

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

        // 1/64 of a tile in 16 bits: enough for drawing, and covers ±512 tiles.
        internal static void WriteCompact(this BinaryWriter writer, Vec2 value)
        {
            writer.Write(Sixtyfourths(value.X));
            writer.Write(Sixtyfourths(value.Y));
        }

        internal static Vec2 ReadCompact(this BinaryReader reader) => new Vec2(reader.ReadInt16() / 64f, reader.ReadInt16() / 64f);

        static short Sixtyfourths(float value) => (short)Math.Clamp((int)MathF.Round(value * 64), short.MinValue, short.MaxValue);

        internal static ushort Ticks(int value) => (ushort)Math.Clamp(value, 0, ushort.MaxValue);

        internal static ushort Whole(float value) => (ushort)Math.Clamp((int)MathF.Ceiling(value), 0, ushort.MaxValue);

        internal static ushort Eighths(float value) => (ushort)Math.Clamp((int)MathF.Round(value * 8), 0, ushort.MaxValue);

        // A direction squeezed into one byte, 256 steps around the circle.
        internal static byte PackDirection(Vec2 direction)
        {
            if (direction.LengthSquared < 1e-8f) return 0;
            float turns = direction.Angle / (2 * MathF.PI);
            return (byte)((int)MathF.Round(turns * 256) & 255);
        }

        internal static Vec2 UnpackDirection(byte packed) => Vec2.FromAngle(packed / 256f * 2 * MathF.PI);

        // A short list of sheet indexes, a byte each.
        internal static void WriteIndexes(BinaryWriter writer, IReadOnlyList<int> indexes)
        {
            writer.Write((byte)Math.Min(indexes.Count, byte.MaxValue));
            foreach (int index in indexes.Take(byte.MaxValue)) writer.Write((byte)index);
        }

        internal static List<int> ReadIndexes(BinaryReader reader)
        {
            var indexes = new List<int>();
            for (int count = reader.ReadByte(); count > 0; count--) indexes.Add(reader.ReadByte());
            return indexes;
        }
    }

    public sealed class JoinMessage
    {
        public JoinMessage(string playerKey, string name, IReadOnlyList<string> classes = null, string version = Protocol.Version)
        {
            PlayerKey = playerKey ?? "";
            Name = name ?? "";
            Classes = classes ?? Array.Empty<string>();
            Version = version ?? "";
        }

        public string PlayerKey { get; }
        public string Name { get; }
        // Class ids the player picked, in order. The host keeps what its rules allow and says which in Welcome.
        public IReadOnlyList<string> Classes { get; }
        public string Version { get; }

        // Classes come after the version, so a host can still read an older client's version and say why it
        // refuses it.
        public byte[] Encode() => Protocol.Build(MessageType.Join, w =>
        {
            w.Write(PlayerKey);
            w.Write(Name);
            w.Write(Version);
            w.Write((byte)Math.Min(Classes.Count, byte.MaxValue));
            foreach (string id in Classes.Take(byte.MaxValue)) w.Write(id ?? "");
        });

        public static JoinMessage Decode(byte[] data) => Protocol.Read(data, MessageType.Join, r =>
        {
            string key = r.ReadString();
            string name = r.ReadString();
            string version = r.ReadString();
            var classes = new List<string>();
            if (version == Protocol.Version)
            {
                for (int count = r.ReadByte(); count > 0; count--) classes.Add(r.ReadString());
            }
            return new JoinMessage(key, name, classes, version);
        });
    }

    // Uses the skill in a slot, aimed where the player points. Sent reliably: a press must not get lost.
    public sealed class SkillCommandMessage
    {
        public SkillCommandMessage(int slot, Vec2 aim)
        {
            Slot = slot;
            Aim = aim;
        }

        public int Slot { get; }
        public Vec2 Aim { get; }

        public byte[] Encode() => Protocol.Build(MessageType.SkillCommand, w =>
        {
            w.Write((byte)Slot);
            w.Write(Aim);
        });

        public static SkillCommandMessage Decode(byte[] data) =>
            Protocol.Read(data, MessageType.SkillCommand, r => new SkillCommandMessage(r.ReadByte(), r.ReadVec2()));
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
        public WelcomeMessage(int playerId, ulong seed, int width, int height, Vec2 spawn, IReadOnlyList<int> classes = null)
        {
            PlayerId = playerId;
            Seed = seed;
            Width = width;
            Height = height;
            Spawn = spawn;
            Classes = classes ?? Array.Empty<int>();
        }

        public int PlayerId { get; }
        public ulong Seed { get; }
        public int Width { get; }
        public int Height { get; }
        public Vec2 Spawn { get; }
        // The classes the host accepted, as indexes into the class sheet.
        public IReadOnlyList<int> Classes { get; }

        public byte[] Encode() => Protocol.Build(MessageType.Welcome, w =>
        {
            w.Write(PlayerId);
            w.Write(Seed);
            w.Write(Width);
            w.Write(Height);
            w.Write(Spawn);
            Protocol.WriteIndexes(w, Classes);
        });

        public static WelcomeMessage Decode(byte[] data) => Protocol.Read(data, MessageType.Welcome, r =>
            new WelcomeMessage(r.ReadInt32(), r.ReadUInt64(), r.ReadInt32(), r.ReadInt32(), r.ReadVec2(), Protocol.ReadIndexes(r)));
    }

    public sealed class RefusedMessage
    {
        public RefusedMessage(string reason) { Reason = reason ?? ""; }

        public string Reason { get; }

        public byte[] Encode() => Protocol.Build(MessageType.Refused, w => w.Write(Reason));

        public static RefusedMessage Decode(byte[] data) =>
            Protocol.Read(data, MessageType.Refused, r => new RefusedMessage(r.ReadString()));
    }

    // Everyone in the game: their names, and their classes as indexes into the class sheet.
    public sealed class RosterMessage
    {
        public RosterMessage(IReadOnlyDictionary<int, string> names, IReadOnlyDictionary<int, IReadOnlyList<int>> classes = null)
        {
            Names = names;
            Classes = classes ?? new Dictionary<int, IReadOnlyList<int>>();
        }

        public IReadOnlyDictionary<int, string> Names { get; }
        public IReadOnlyDictionary<int, IReadOnlyList<int>> Classes { get; }

        public byte[] Encode() => Protocol.Build(MessageType.Roster, w =>
        {
            w.Write(Names.Count);
            foreach (var pair in Names)
            {
                w.Write(pair.Key);
                w.Write(pair.Value);
                Protocol.WriteIndexes(w, Classes.TryGetValue(pair.Key, out var classes) ? classes : Array.Empty<int>());
            }
        });

        public static RosterMessage Decode(byte[] data) => Protocol.Read(data, MessageType.Roster, r =>
        {
            int count = r.ReadInt32();
            var names = new Dictionary<int, string>();
            var classes = new Dictionary<int, IReadOnlyList<int>>();
            for (int i = 0; i < count; i++)
            {
                int id = r.ReadInt32();
                names[id] = r.ReadString();
                classes[id] = Protocol.ReadIndexes(r);
            }
            return new RosterMessage(names, classes);
        });
    }

    public enum EntityKind : byte
    {
        Player,
        Monster,
        Boss,
        // Ground loot. Type is the item base's index and State its rarity, or for a rune the rune's index and
        // State RuneDrop.
        Drop,
    }

    public static class DropState
    {
        public const byte Rune = 255;
    }

    // Positions travel as 1/64 of a tile in 16 bits and health as whole numbers, to keep a snapshot in one packet.
    // Type is an index into the monster or boss sheet, which both sides load. State is an EnemyState or BossState,
    // or for a player which skill slots have a buff running (bit 0 for slot 0).
    public readonly struct EntityState
    {
        public EntityState(int id, EntityKind kind, int type, Vec2 position, Vec2 facing, float health, float maxHealth, byte state, int phase = 0)
        {
            Id = id;
            Kind = kind;
            Type = type;
            Position = position;
            Facing = facing;
            Health = health;
            MaxHealth = maxHealth;
            State = state;
            Phase = phase;
        }

        public int Id { get; }
        public EntityKind Kind { get; }
        public int Type { get; }
        public bool IsPlayer => Kind == EntityKind.Player;
        public Vec2 Position { get; }
        public Vec2 Facing { get; }
        public float Health { get; }
        public float MaxHealth { get; }
        public byte State { get; }
        // A boss's current phase.
        public int Phase { get; }
        public bool IsDead => Health <= 0;
    }

    // Clients move projectiles on by their velocity between snapshots, so they are drawn where they really are.
    public readonly struct ProjectileState
    {
        public ProjectileState(int id, Vec2 position, Vec2 velocity, float radius, int skill = -1)
        {
            Id = id;
            Position = position;
            Velocity = velocity;
            Radius = radius;
            Skill = skill;
        }

        public int Id { get; }
        public Vec2 Position { get; }
        public Vec2 Velocity { get; }
        public float Radius { get; }
        // Which skill in the class sheet fired it, or -1 for a boss's.
        public int Skill { get; }
    }

    // A telegraphed area. TicksLeft counts from the snapshot's tick to when it lands, out of Duration in all.
    public readonly struct HazardState
    {
        public HazardState(int id, HazardShape shape, Vec2 position, Vec2 direction, float size, float width, int ticksLeft, int duration, bool harmful)
        {
            Id = id;
            Shape = shape;
            Position = position;
            Direction = direction;
            Size = size;
            Width = width;
            TicksLeft = ticksLeft;
            Duration = duration;
            Harmful = harmful;
        }

        public int Id { get; }
        public HazardShape Shape { get; }
        public Vec2 Position { get; }
        public Vec2 Direction { get; }
        public float Size { get; }
        public float Width { get; }
        public int TicksLeft { get; }
        public int Duration { get; }
        // False for a charge's path, which only shows where the boss will run.
        public bool Harmful { get; }
    }

    // What only the receiving player needs about themselves.
    public readonly struct SelfState
    {
        public SelfState(int corrections, Vec2 position, float health, float maxHealth, float moveSpeed, IReadOnlyList<int> cooldowns = null, IReadOnlyList<int> buffs = null)
        {
            Corrections = corrections;
            Position = position;
            Health = health;
            MaxHealth = maxHealth;
            MoveSpeed = moveSpeed;
            Cooldowns = cooldowns ?? Array.Empty<int>();
            Buffs = buffs ?? Array.Empty<int>();
        }

        public int Corrections { get; }
        public Vec2 Position { get; }
        public float Health { get; }
        public float MaxHealth { get; }
        public float MoveSpeed { get; }
        // Per skill slot: ticks until it is ready, and until the buff it put on runs out.
        public IReadOnlyList<int> Cooldowns { get; }
        public IReadOnlyList<int> Buffs { get; }
        public bool IsDead => Health <= 0;
    }

    public sealed class SnapshotMessage
    {
        public SnapshotMessage(
            long tick,
            SelfState self,
            IReadOnlyList<EntityState> entities,
            IReadOnlyList<ProjectileState> projectiles,
            IReadOnlyList<HazardState> hazards,
            IReadOnlyList<SimEvent> events)
        {
            Tick = tick;
            Self = self;
            Entities = entities;
            Projectiles = projectiles;
            Hazards = hazards;
            Events = events;
        }

        public long Tick { get; }
        public SelfState Self { get; }
        public IReadOnlyList<EntityState> Entities { get; }
        public IReadOnlyList<ProjectileState> Projectiles { get; }
        public IReadOnlyList<HazardState> Hazards { get; }
        public IReadOnlyList<SimEvent> Events { get; }

        public byte[] Encode() => Protocol.Build(MessageType.Snapshot, w =>
        {
            w.Write(Tick);
            w.Write(Self.Corrections);
            w.Write(Self.Position);
            w.Write(Self.Health);
            w.Write(Self.MaxHealth);
            w.Write(Self.MoveSpeed);
            w.Write((byte)Self.Cooldowns.Count);
            for (int slot = 0; slot < Self.Cooldowns.Count; slot++)
            {
                w.Write(Protocol.Ticks(Self.Cooldowns[slot]));
                w.Write(Protocol.Ticks(slot < Self.Buffs.Count ? Self.Buffs[slot] : 0));
            }

            w.Write((ushort)Entities.Count);
            foreach (var entity in Entities)
            {
                w.Write(entity.Id);
                w.Write((byte)entity.Kind);
                w.Write((byte)entity.Type);
                w.WriteCompact(entity.Position);
                w.Write(Protocol.PackDirection(entity.Facing));
                w.Write(Protocol.Whole(entity.Health));
                w.Write(Protocol.Whole(entity.MaxHealth));
                w.Write(entity.State);
                w.Write((byte)entity.Phase);
            }

            w.Write((ushort)Projectiles.Count);
            foreach (var projectile in Projectiles)
            {
                w.Write(projectile.Id);
                w.WriteCompact(projectile.Position);
                w.WriteCompact(projectile.Velocity);
                w.Write((byte)Math.Clamp((int)MathF.Round(projectile.Radius * 32), 0, 255));
                w.Write((byte)(projectile.Skill + 1));
            }

            w.Write((ushort)Hazards.Count);
            foreach (var hazard in Hazards)
            {
                w.Write(hazard.Id);
                w.Write((byte)hazard.Shape);
                w.WriteCompact(hazard.Position);
                w.Write(Protocol.PackDirection(hazard.Direction));
                w.Write(Protocol.Eighths(hazard.Size));
                w.Write(Protocol.Eighths(hazard.Width));
                w.Write((short)Math.Clamp(hazard.TicksLeft, short.MinValue, short.MaxValue));
                w.Write((short)Math.Clamp(hazard.Duration, 0, short.MaxValue));
                w.Write(hazard.Harmful);
            }

            w.Write((ushort)Events.Count);
            foreach (var e in Events)
            {
                w.Write((byte)e.Type);
                w.Write(e.Source);
                // An event that happens somewhere has no target, so its position takes the target's four bytes.
                if (SimEvent.HasPosition(e.Type)) w.WriteCompact(e.Position);
                else w.Write(e.Target);
                w.Write((short)Math.Clamp(e.Amount, short.MinValue, short.MaxValue));
                w.Write(e.Critical);
                w.Write(Protocol.PackDirection(e.Direction));
            }
        });

        public static SnapshotMessage Decode(byte[] data) => Protocol.Read(data, MessageType.Snapshot, r =>
        {
            long tick = r.ReadInt64();
            int corrections = r.ReadInt32();
            var position = r.ReadVec2();
            float health = r.ReadSingle();
            float maxHealth = r.ReadSingle();
            float moveSpeed = r.ReadSingle();
            var cooldowns = new List<int>();
            var buffs = new List<int>();
            for (int slots = r.ReadByte(); slots > 0; slots--)
            {
                cooldowns.Add(r.ReadUInt16());
                buffs.Add(r.ReadUInt16());
            }
            var self = new SelfState(corrections, position, health, maxHealth, moveSpeed, cooldowns, buffs);

            int entityCount = r.ReadUInt16();
            var entities = new List<EntityState>(entityCount);
            for (int i = 0; i < entityCount; i++)
            {
                entities.Add(new EntityState(
                    r.ReadInt32(),
                    (EntityKind)r.ReadByte(),
                    r.ReadByte(),
                    r.ReadCompact(),
                    Protocol.UnpackDirection(r.ReadByte()),
                    r.ReadUInt16(),
                    r.ReadUInt16(),
                    r.ReadByte(),
                    r.ReadByte()));
            }

            int projectileCount = r.ReadUInt16();
            var projectiles = new List<ProjectileState>(projectileCount);
            for (int i = 0; i < projectileCount; i++)
            {
                projectiles.Add(new ProjectileState(r.ReadInt32(), r.ReadCompact(), r.ReadCompact(), r.ReadByte() / 32f, r.ReadByte() - 1));
            }

            int hazardCount = r.ReadUInt16();
            var hazards = new List<HazardState>(hazardCount);
            for (int i = 0; i < hazardCount; i++)
            {
                hazards.Add(new HazardState(
                    r.ReadInt32(),
                    (HazardShape)r.ReadByte(),
                    r.ReadCompact(),
                    Protocol.UnpackDirection(r.ReadByte()),
                    r.ReadUInt16() / 8f,
                    r.ReadUInt16() / 8f,
                    r.ReadInt16(),
                    r.ReadInt16(),
                    r.ReadBoolean()));
            }

            int eventCount = r.ReadUInt16();
            var events = new List<SimEvent>(eventCount);
            for (int i = 0; i < eventCount; i++)
            {
                var type = (SimEventType)r.ReadByte();
                int source = r.ReadInt32();
                bool located = SimEvent.HasPosition(type);
                var at = located ? r.ReadCompact() : Vec2.Zero;
                int target = located ? 0 : r.ReadInt32();
                int amount = r.ReadInt16();
                bool critical = r.ReadBoolean();
                events.Add(new SimEvent(type, source, target, amount, critical, Protocol.UnpackDirection(r.ReadByte()), position: at));
            }
            return new SnapshotMessage(tick, self, entities, projectiles, hazards, events);
        });
    }
}
