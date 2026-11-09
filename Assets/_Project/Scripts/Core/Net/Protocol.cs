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
        ClassCommand = 5,
        Welcome = 10,
        Refused = 11,
        Snapshot = 12,
        Roster = 13,
        Inventory = 14,
        TileChanges = 15,
        CropChanges = 16,
        Chest = 17,
        Machines = 18,
        Kit = 19,
        Stock = 20,
        Production = 21,
        Friendships = 22,
        Life = 23,
        Records = 24,
    }

    // Every message is one byte of MessageType followed by little-endian fields, so host and clients read them the
    // same way on every platform. Core only produces and consumes byte arrays; the transport is Unity's business.
    public static class Protocol
    {
        // An entity's phase byte also carries its burning and chilled marks (StatusBits) in its top two bits.
        public static int WithStatus(int phase, byte status) => (phase & PhaseMask) | ((status & 3) << 6);
        public const int PhaseMask = 0x3F;

        // Host and client must run the same rules and sheets, so a different version is refused.
        public const string Version = "0.59.0";

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
        public JoinMessage(string playerKey, string name, IReadOnlyList<string> classes = null, string version = Protocol.Version, byte[] character = null,
            IReadOnlyList<int> skillOrder = null)
        {
            SkillOrder = skillOrder ?? Array.Empty<int>();
            PlayerKey = playerKey ?? "";
            Name = name ?? "";
            Classes = classes ?? Array.Empty<string>();
            Version = version ?? "";
            Character = character ?? Array.Empty<byte>();
        }

        public string PlayerKey { get; }
        public string Name { get; }
        // Class ids the player picked, in order. The host keeps what its rules allow and says which in Welcome.
        public IReadOnlyList<string> Classes { get; }
        public string Version { get; }
        // The player's saved character (CharacterSave), or empty for a new one.
        public byte[] Character { get; }
        // The order of the skills in the slots (ClassRules.SkillsFor), or empty for the classes' own order.
        public IReadOnlyList<int> SkillOrder { get; }

        // Classes come after the version, so a host can still read an older client's version and say why it
        // refuses it.
        public byte[] Encode() => Protocol.Build(MessageType.Join, w =>
        {
            w.Write(PlayerKey);
            w.Write(Name);
            w.Write(Version);
            w.Write((byte)Math.Min(Classes.Count, byte.MaxValue));
            foreach (string id in Classes.Take(byte.MaxValue)) w.Write(id ?? "");
            w.Write(Character.Length);
            w.Write(Character);
            Protocol.WriteIndexes(w, SkillOrder);
        });

        public static JoinMessage Decode(byte[] data) => Protocol.Read(data, MessageType.Join, r =>
        {
            string key = r.ReadString();
            string name = r.ReadString();
            string version = r.ReadString();
            var classes = new List<string>();
            byte[] character = null;
            IReadOnlyList<int> order = null;
            if (version == Protocol.Version)
            {
                for (int count = r.ReadByte(); count > 0; count--) classes.Add(r.ReadString());
                int length = r.ReadInt32();
                if (length < 0 || length > 1 << 20) throw new InvalidDataException("Character data too large.");
                character = r.ReadBytes(length);
                order = Protocol.ReadIndexes(r);
            }
            return new JoinMessage(key, name, classes, version, character, order);
        });
    }

    // Tiles players have dug out or built on. Sent reliably as they change, and all of them to a player who joins;
    // each message holds at most MaxPerMessage so it stays small.
    public sealed class TileChangesMessage
    {
        public const int MaxPerMessage = 200;

        public TileChangesMessage(IReadOnlyList<TileChange> changes) { Changes = changes; }

        public IReadOnlyList<TileChange> Changes { get; }

        public byte[] Encode() => Protocol.Build(MessageType.TileChanges, w =>
        {
            w.Write((ushort)Changes.Count);
            foreach (var change in Changes)
            {
                w.Write((ushort)change.X);
                w.Write((ushort)change.Y);
                w.Write((byte)change.Type);
            }
        });

        public static TileChangesMessage Decode(byte[] data) => Protocol.Read(data, MessageType.TileChanges, r =>
        {
            int count = r.ReadUInt16();
            var changes = new List<TileChange>(count);
            for (int i = 0; i < count; i++) changes.Add(new TileChange(r.ReadUInt16(), r.ReadUInt16(), (TileType)r.ReadByte()));
            return new TileChangesMessage(changes);
        });

        // The changes split into messages of at most MaxPerMessage.
        public static IEnumerable<byte[]> EncodeAll(IReadOnlyList<TileChange> changes)
        {
            for (int start = 0; start < changes.Count; start += MaxPerMessage)
            {
                yield return new TileChangesMessage(changes.Skip(start).Take(MaxPerMessage).ToList()).Encode();
            }
        }
    }

    // Crops planted or watered (with what, as an index into the crops sheet, how far it had grown and its water's
    // span in host ticks) or harvested. Sent like TileChanges; how far along a crop is now, each side works out from
    // those and the tick (Crop).
    public sealed class CropChangesMessage
    {
        public const int MaxPerMessage = 120;
        const byte Gone = 255;

        public CropChangesMessage(IReadOnlyList<(int X, int Y, int Crop, long Grown, long WetFrom, long WetUntil)> changes) { Changes = changes; }

        // Crop is -1 for a harvested one.
        public IReadOnlyList<(int X, int Y, int Crop, long Grown, long WetFrom, long WetUntil)> Changes { get; }

        public byte[] Encode() => Protocol.Build(MessageType.CropChanges, w =>
        {
            w.Write((ushort)Changes.Count);
            foreach (var (x, y, crop, grown, wetFrom, wetUntil) in Changes)
            {
                w.Write((ushort)x);
                w.Write((ushort)y);
                w.Write(crop < 0 ? Gone : (byte)crop);
                w.Write((int)Math.Clamp(grown, 0, int.MaxValue));
                w.Write(wetFrom);
                w.Write(wetUntil);
            }
        });

        public static CropChangesMessage Decode(byte[] data) => Protocol.Read(data, MessageType.CropChanges, r =>
        {
            int count = r.ReadUInt16();
            var changes = new List<(int, int, int, long, long, long)>(count);
            for (int i = 0; i < count; i++)
            {
                int x = r.ReadUInt16();
                int y = r.ReadUInt16();
                byte crop = r.ReadByte();
                long grown = r.ReadInt32();
                long wetFrom = r.ReadInt64();
                changes.Add((x, y, crop == Gone ? -1 : crop, grown, wetFrom, r.ReadInt64()));
            }
            return new CropChangesMessage(changes);
        });

        public static IEnumerable<byte[]> EncodeAll(IReadOnlyList<(int X, int Y, int Crop, long Grown, long WetFrom, long WetUntil)> changes)
        {
            for (int start = 0; start < changes.Count; start += MaxPerMessage)
            {
                yield return new CropChangesMessage(changes.Skip(start).Take(MaxPerMessage).ToList()).Encode();
            }
        }
    }

    // The machines near a player: which way each faces and what a conveyor carries how far along. Sent unreliably
    // How well the player knows each person (World.Merchant.cs): the person's sheet index, the points and the days
    // they last talked and gave a gift. Sent when it changes; the client keeps it in its character save.
    public sealed class FriendshipsMessage
    {
        public FriendshipsMessage(IReadOnlyList<(int Npc, int Points, int TalkedDay, int GiftedDay, int RequestDay, int HeartGifts)> friendships,
            IReadOnlyList<(int Entry, int Count)> sold = null)
        {
            Friendships = friendships;
            Sold = sold ?? Array.Empty<(int, int)>();
        }

        public IReadOnlyList<(int Npc, int Points, int TalkedDay, int GiftedDay, int RequestDay, int HeartGifts)> Friendships { get; }
        // How many of each shop row (by its index) its person bought today (World.SoldToday), the rows with any.
        public IReadOnlyList<(int Entry, int Count)> Sold { get; }

        public byte[] Encode() => Protocol.Build(MessageType.Friendships, w =>
        {
            w.Write((byte)Friendships.Count);
            foreach (var (npc, points, talked, gifted, request, heartGifts) in Friendships)
            {
                w.Write((byte)npc);
                w.Write((ushort)points);
                w.Write(talked);
                w.Write(gifted);
                w.Write(request);
                w.Write((byte)heartGifts);
            }
            w.Write((ushort)Sold.Count);
            foreach (var (entry, count) in Sold)
            {
                w.Write((ushort)entry);
                w.Write((byte)Math.Clamp(count, 0, 255));
            }
        });

        public static FriendshipsMessage Decode(byte[] data) => Protocol.Read(data, MessageType.Friendships, r =>
        {
            int n = r.ReadByte();
            var list = new List<(int, int, int, int, int, int)>(n);
            for (int i = 0; i < n; i++) list.Add((r.ReadByte(), r.ReadUInt16(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadByte()));
            var sold = new List<(int, int)>();
            for (int k = r.ReadUInt16(); k > 0; k--) sold.Add((r.ReadUInt16(), r.ReadByte()));
            return new FriendshipsMessage(list, sold);
        });
    }

    // The player's experience in each life skill (World.Life.cs), in LifeSkill order. Sent when it changes; the
    // client keeps it in its character save.
    public sealed class LifeMessage
    {
        public LifeMessage(IReadOnlyList<int> experience) { Experience = experience; }

        public IReadOnlyList<int> Experience { get; }

        public byte[] Encode() => Protocol.Build(MessageType.Life, w =>
        {
            w.Write((byte)Experience.Count);
            foreach (int experience in Experience) w.Write(experience);
        });

        public static LifeMessage Decode(byte[] data) => Protocol.Read(data, MessageType.Life, r =>
        {
            var list = new List<int>();
            for (int n = r.ReadByte(); n > 0; n--) list.Add(r.ReadInt32());
            return new LifeMessage(list);
        });
    }

    // The player's bestiary (Records.cs): each record as its kind, the sheet index of what it counts (monster,
    // boss, material or gear base) and how many; and the indices of the achievements had. Sent when it changes.
    public sealed class RecordsMessage
    {
        public RecordsMessage(IReadOnlyList<(RecordKind Kind, int Index, int Count)> records, IReadOnlyList<int> achievements)
        {
            Records = records;
            Achievements = achievements;
        }

        public IReadOnlyList<(RecordKind Kind, int Index, int Count)> Records { get; }
        public IReadOnlyList<int> Achievements { get; }

        public byte[] Encode() => Protocol.Build(MessageType.Records, w =>
        {
            w.Write((ushort)Records.Count);
            foreach (var (kind, index, count) in Records)
            {
                w.Write((byte)kind);
                w.Write((ushort)index);
                w.Write(count);
            }
            w.Write((byte)Achievements.Count);
            foreach (int index in Achievements) w.Write((byte)index);
        });

        public static RecordsMessage Decode(byte[] data) => Protocol.Read(data, MessageType.Records, r =>
        {
            var records = new List<(RecordKind, int, int)>();
            for (int n = r.ReadUInt16(); n > 0; n--) records.Add(((RecordKind)r.ReadByte(), r.ReadUInt16(), r.ReadInt32()));
            var achievements = new List<int>();
            for (int n = r.ReadByte(); n > 0; n--) achievements.Add(r.ReadByte());
            return new RecordsMessage(records, achievements);
        });
    }

    // What machines made in the last few minutes (World.Production), for everyone. Kind 0 is a material, 1 a
    // consumable; Index is its sheet index. Sent every few seconds when it changed.
    public sealed class ProductionMessage
    {
        public ProductionMessage(float minutes, IReadOnlyList<(int Kind, int Index, int Count)> made)
        {
            Minutes = minutes;
            Made = made;
        }

        // How many minutes the counts cover.
        public float Minutes { get; }
        public IReadOnlyList<(int Kind, int Index, int Count)> Made { get; }

        public byte[] Encode() => Protocol.Build(MessageType.Production, w =>
        {
            w.Write(Minutes);
            w.Write((ushort)Made.Count);
            foreach (var (kind, index, count) in Made)
            {
                w.Write((byte)kind);
                w.Write((ushort)index);
                w.Write(count);
            }
        });

        public static ProductionMessage Decode(byte[] data) => Protocol.Read(data, MessageType.Production, r =>
        {
            float minutes = r.ReadSingle();
            int n = r.ReadUInt16();
            var made = new List<(int, int, int)>(n);
            for (int i = 0; i < n; i++) made.Add((r.ReadByte(), r.ReadUInt16(), r.ReadInt32()));
            return new ProductionMessage(minutes, made);
        });
    }

    // What the chests near the player hold, by material sheet index, so the crafting list can count them
    // (World.StockNear). Sent when it changes; each replaces the last.
    public sealed class StockMessage
    {
        public StockMessage(IReadOnlyList<(int Material, int Count)> stock) { Stock = stock; }

        public IReadOnlyList<(int Material, int Count)> Stock { get; }

        public byte[] Encode() => Protocol.Build(MessageType.Stock, w =>
        {
            w.Write((ushort)Stock.Count);
            foreach (var (material, count) in Stock)
            {
                w.Write((ushort)material);
                w.Write(count);
            }
        });

        public static StockMessage Decode(byte[] data) => Protocol.Read(data, MessageType.Stock, r =>
        {
            int n = r.ReadUInt16();
            var stock = new List<(int, int)>(n);
            for (int i = 0; i < n; i++) stock.Add((r.ReadUInt16(), r.ReadInt32()));
            return new StockMessage(stock);
        });
    }

    // every few ticks, like a slower snapshot; each one replaces the last. Materials are sheet indexes + 1 (0 for
    // none). See Docs/Automation.md.
    public sealed class MachinesMessage
    {
        // Beside Machine.OffBit on the direction byte: powered by a generator.
        const int PoweredBit = 0x40;

        public MachinesMessage(IReadOnlyList<MachineState> machines) { Machines = machines; }

        public IReadOnlyList<MachineState> Machines { get; }

        public byte[] Encode() => Protocol.Build(MessageType.Machines, w =>
        {
            w.Write((ushort)Machines.Count);
            foreach (var m in Machines)
            {
                w.Write((ushort)m.X);
                w.Write((ushort)m.Y);
                w.Write((byte)m.Kind);
                w.Write((byte)(m.Direction | (m.On ? 0 : Machine.OffBit) | (m.Powered ? PoweredBit : 0)));
                w.Write((byte)(m.Carried + 1));
                w.Write((byte)Math.Clamp((int)MathF.Round(m.Progress * 255), 0, 255));
                w.Write((ushort)(m.Setting + 1));
                w.Write((byte)Math.Clamp(m.Holding, 0, 255));
                w.Write((byte)Math.Clamp(m.Held, 0, 255));
                w.Write((byte)m.Picks.Count);
                foreach (int pick in m.Picks) w.Write((ushort)pick);
            }
        });

        public static MachinesMessage Decode(byte[] data) => Protocol.Read(data, MessageType.Machines, r =>
        {
            int count = r.ReadUInt16();
            var machines = new List<MachineState>(count);
            for (int i = 0; i < count; i++)
            {
                int x = r.ReadUInt16();
                int y = r.ReadUInt16();
                var kind = (TileType)r.ReadByte();
                int direction = r.ReadByte();
                int carried = r.ReadByte() - 1;
                float progress = r.ReadByte() / 255f;
                int setting = r.ReadUInt16() - 1;
                int holding = r.ReadByte();
                int held = r.ReadByte();
                var picks = new int[r.ReadByte()];
                for (int k = 0; k < picks.Length; k++) picks[k] = r.ReadUInt16();
                machines.Add(new MachineState(x, y, kind, direction & 3, carried, progress, (direction & Machine.OffBit) == 0, setting, (direction & PoweredBit) != 0,
                    holding, held, picks));
            }
            return new MachinesMessage(machines);
        });
    }

    public readonly struct MachineState
    {
        public MachineState(int x, int y, TileType kind, int direction, int carried, float progress, bool on = true, int setting = -1, bool powered = false,
            int holding = 0, int held = 0, IReadOnlyList<int> picks = null)
        {
            Picks = picks ?? Array.Empty<int>();
            Holding = holding;
            Held = held;
            Setting = setting;
            Powered = powered;
            X = x;
            Y = y;
            Kind = kind;
            Direction = direction;
            Carried = carried;
            Progress = progress;
            On = on;
        }

        public int X { get; }
        public int Y { get; }
        public TileType Kind { get; }
        public int Direction { get; }
        // A conveyor's load as a material sheet index, or -1.
        public int Carried { get; }
        public float Progress { get; }
        // Switched on (World.ToggleMachine).
        public bool On { get; }
        // A filter's chosen material as a sheet index, an assembler's recipe as one, a generator's or power pole's
        // grid as supply × 256 + demand (MachineState.Grid), a coop's chickens, or -1.
        public int Setting { get; }

        // Packs a grid's supply and demand into a setting.
        public static int Grid(int supply, int demand) => Math.Clamp(supply, 0, 254) * 256 + Math.Clamp(demand, 0, 255);
        // Working twice as fast beside a running generator.
        public bool Powered { get; }
        // How much it holds to work on, and how much it made that waits to go out (up to 255 each).
        public int Holding { get; }
        public int Held { get; }
        // A filter's chosen materials, as sheet indexes.
        public IReadOnlyList<int> Picks { get; }
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
        public WelcomeMessage(int playerId, ulong seed, int width, int height, Vec2 spawn, IReadOnlyList<int> classes = null, IReadOnlyList<int> skillOrder = null)
        {
            SkillOrder = skillOrder ?? Array.Empty<int>();
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
        // The order the host put the skills in (ClassRules.SkillsFor).
        public IReadOnlyList<int> SkillOrder { get; }

        public byte[] Encode() => Protocol.Build(MessageType.Welcome, w =>
        {
            w.Write(PlayerId);
            w.Write(Seed);
            w.Write(Width);
            w.Write(Height);
            w.Write(Spawn);
            Protocol.WriteIndexes(w, Classes);
            Protocol.WriteIndexes(w, SkillOrder);
        });

        public static WelcomeMessage Decode(byte[] data) => Protocol.Read(data, MessageType.Welcome, r =>
            new WelcomeMessage(r.ReadInt32(), r.ReadUInt64(), r.ReadInt32(), r.ReadInt32(), r.ReadVec2(), Protocol.ReadIndexes(r), Protocol.ReadIndexes(r)));
    }

    // A player asking for other classes (indexes into the class sheet) or another order of their skills; and the
    // host telling them the classes and order they have now (MessageType.Kit). See Docs/Classes.md.
    public sealed class KitMessage
    {
        public KitMessage(MessageType type, IReadOnlyList<int> classes, IReadOnlyList<int> skillOrder)
        {
            Type = type;
            Classes = classes ?? Array.Empty<int>();
            SkillOrder = skillOrder ?? Array.Empty<int>();
        }

        public MessageType Type { get; }
        public IReadOnlyList<int> Classes { get; }
        public IReadOnlyList<int> SkillOrder { get; }

        public byte[] Encode() => Protocol.Build(Type, w =>
        {
            Protocol.WriteIndexes(w, Classes);
            Protocol.WriteIndexes(w, SkillOrder);
        });

        public static KitMessage Decode(byte[] data, MessageType type) =>
            Protocol.Read(data, type, r => new KitMessage(type, Protocol.ReadIndexes(r), Protocol.ReadIndexes(r)));
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
        // A player's summoned spirit. Type is its skill's index in the class data; Health counts down the ticks it
        // has left, from MaxHealth.
        Minion,
    }

    // The world's void rifts (World.Rift.cs): the tier it may open, and the open rift if any (Tier 0 when none).
    public readonly struct RiftState
    {
        public RiftState(int unlocked, int tier, int wave, int monstersLeft, int secondsLeft, RiftAffix affixes)
        {
            Unlocked = unlocked;
            Tier = tier;
            Wave = wave;
            MonstersLeft = monstersLeft;
            SecondsLeft = secondsLeft;
            Affixes = affixes;
        }

        public int Unlocked { get; }
        public int Tier { get; }
        public int Wave { get; }
        public int MonstersLeft { get; }
        public int SecondsLeft { get; }
        public RiftAffix Affixes { get; }
        public bool Open => Tier > 0;
    }

    // The world's base defense (World.Defense.cs): the level it defends at next, whether it was done today, and the
    // defense under way if any (Level 0 when none), with the ward stone's health in percent.
    public readonly struct DefenseState
    {
        public DefenseState(int unlocked, bool doneToday, int level, int wave, int monstersLeft, int stonePercent)
        {
            Unlocked = unlocked;
            DoneToday = doneToday;
            Level = level;
            Wave = wave;
            MonstersLeft = monstersLeft;
            StonePercent = stonePercent;
        }

        public int Unlocked { get; }
        public bool DoneToday { get; }
        public int Level { get; }
        public int Wave { get; }
        public int MonstersLeft { get; }
        public int StonePercent { get; }
        public bool Active => Level > 0;
    }

    // A player entity's phase (under its StatusBits).
    public static class PlayerPhase
    {
        public const int Sleeping = 1;
        // Above the sleeping bit, the pet's number (Pets.IndexOf), so everyone draws it.
        public const int PetShift = 1;

        public static bool IsSleeping(int phase) => (phase & Sleeping) != 0;

        public static PetDefinition PetOf(int phase) => Pets.ByIndex((phase & Protocol.PhaseMask) >> PetShift);
    }

    public static class DropState
    {
        public const byte Rune = 255;
        // Type is then the consumable's index.
        public const byte Consumable = 254;
        // Type is then the material's index.
        public const byte Material = 253;
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
        public ProjectileState(int id, Vec2 position, Vec2 velocity, float radius, bool friendly = false, SkillTag tags = SkillTag.None)
        {
            Id = id;
            Position = position;
            Velocity = velocity;
            Radius = radius;
            Friendly = friendly;
            Tags = tags;
        }

        public int Id { get; }
        public Vec2 Position { get; }
        public Vec2 Velocity { get; }
        public float Radius { get; }
        // A player's, and the tags it hits with, which is all the screen needs to colour and shape it.
        public bool Friendly { get; }
        public SkillTag Tags { get; }
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
        public SelfState(
            int corrections,
            Vec2 position,
            float health,
            float maxHealth,
            float moveSpeed,
            IReadOnlyList<int> cooldowns = null,
            IReadOnlyList<int> buffs = null,
            int consumableCooldown = 0,
            int food = -1,
            int foodTicks = 0,
            byte status = 0,
            int experience = 0,
            int bedX = -1,
            int bedY = -1,
            float mana = 0,
            float maxMana = PlayerStats.BaseMana)
        {
            Mana = mana;
            MaxMana = maxMana;
            Status = status;
            Experience = experience;
            BedX = bedX;
            BedY = bedY;
            Food = food;
            FoodTicks = foodTicks;
            Corrections = corrections;
            Position = position;
            Health = health;
            MaxHealth = maxHealth;
            MoveSpeed = moveSpeed;
            Cooldowns = cooldowns ?? Array.Empty<int>();
            Buffs = buffs ?? Array.Empty<int>();
            ConsumableCooldown = consumableCooldown;
        }

        public int Corrections { get; }
        public Vec2 Position { get; }
        public float Health { get; }
        public float MaxHealth { get; }
        public float MoveSpeed { get; }
        public float Mana { get; }
        public float MaxMana { get; }
        // Per skill slot: ticks until it is ready, and until the buff it put on runs out.
        public IReadOnlyList<int> Cooldowns { get; }
        public IReadOnlyList<int> Buffs { get; }
        // Ticks until any consumable can be used again.
        public int ConsumableCooldown { get; }
        // Burning, chilled (StatusBits).
        public byte Status { get; }
        // All the experience the character has; the level follows (Leveling).
        public int Experience { get; }
        // The sheet index of the food whose buff is on (-1 for none), and the ticks it has left.
        public int Food { get; }
        public int FoodTicks { get; }
        // The tile of the player's bed, or -1 (World.BedOf), so the client can offer to sleep beside it.
        public int BedX { get; }
        public int BedY { get; }
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
            IReadOnlyList<SimEvent> events,
            long clock = 0,
            int sleepers = 0,
            RiftState rift = default,
            DefenseState defense = default)
        {
            Defense = defense;
            Tick = tick;
            Clock = clock;
            Sleepers = sleepers;
            Rift = rift;
            Self = self;
            Entities = entities;
            Projectiles = projectiles;
            Hazards = hazards;
            Events = events;
        }

        public long Tick { get; }
        // The world's clock (Calendar) at Tick, and how many players are lying in their beds.
        public long Clock { get; }
        public int Sleepers { get; }
        public RiftState Rift { get; }
        public DefenseState Defense { get; }
        public SelfState Self { get; }
        public IReadOnlyList<EntityState> Entities { get; }
        public IReadOnlyList<ProjectileState> Projectiles { get; }
        public IReadOnlyList<HazardState> Hazards { get; }
        public IReadOnlyList<SimEvent> Events { get; }

        public byte[] Encode() => Protocol.Build(MessageType.Snapshot, w =>
        {
            w.Write(Tick);
            w.Write(Clock);
            w.Write((byte)Math.Clamp(Sleepers, 0, 255));
            w.Write((byte)Rift.Unlocked);
            w.Write((byte)Rift.Tier);
            w.Write((byte)Rift.Wave);
            w.Write((byte)Math.Clamp(Rift.MonstersLeft, 0, 255));
            w.Write((ushort)Math.Clamp(Rift.SecondsLeft, 0, ushort.MaxValue));
            w.Write((byte)Rift.Affixes);
            w.Write((byte)Defense.Unlocked);
            w.Write(Defense.DoneToday);
            w.Write((byte)Defense.Level);
            w.Write((byte)Defense.Wave);
            w.Write((byte)Math.Clamp(Defense.MonstersLeft, 0, 255));
            w.Write((byte)Math.Clamp(Defense.StonePercent, 0, 100));
            w.Write(Self.Corrections);
            w.Write(Self.Position);
            w.Write(Self.Health);
            w.Write(Self.MaxHealth);
            w.Write(Self.MoveSpeed);
            w.Write(Self.Mana);
            w.Write(Self.MaxMana);
            w.Write((byte)Self.Cooldowns.Count);
            for (int slot = 0; slot < Self.Cooldowns.Count; slot++)
            {
                w.Write(Protocol.Ticks(Self.Cooldowns[slot]));
                w.Write(Protocol.Ticks(slot < Self.Buffs.Count ? Self.Buffs[slot] : 0));
            }
            w.Write(Protocol.Ticks(Self.ConsumableCooldown));
            w.Write((ushort)(Self.Food + 1));
            w.Write(Self.FoodTicks);
            w.Write(Self.Status);
            w.Write(Self.Experience);
            w.Write((short)Math.Clamp(Self.BedX, -1, short.MaxValue));
            w.Write((short)Math.Clamp(Self.BedY, -1, short.MaxValue));

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
                // The seven tags fit under the top bit, which says whose it is.
                w.Write((byte)((projectile.Friendly ? 0x80 : 0) | ((int)projectile.Tags & 0x7f)));
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
            long clock = r.ReadInt64();
            int sleepers = r.ReadByte();
            var rift = new RiftState(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadUInt16(), (RiftAffix)r.ReadByte());
            var defense = new DefenseState(r.ReadByte(), r.ReadBoolean(), r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
            int corrections = r.ReadInt32();
            var position = r.ReadVec2();
            float health = r.ReadSingle();
            float maxHealth = r.ReadSingle();
            float moveSpeed = r.ReadSingle();
            float mana = r.ReadSingle();
            float maxMana = r.ReadSingle();
            var cooldowns = new List<int>();
            var buffs = new List<int>();
            for (int slots = r.ReadByte(); slots > 0; slots--)
            {
                cooldowns.Add(r.ReadUInt16());
                buffs.Add(r.ReadUInt16());
            }
            int consumableCooldown = r.ReadUInt16();
            int food = r.ReadUInt16() - 1;
            int foodTicks = r.ReadInt32();
            byte status = r.ReadByte();
            int experience = r.ReadInt32();
            int bedX = r.ReadInt16();
            int bedY = r.ReadInt16();
            var self = new SelfState(corrections, position, health, maxHealth, moveSpeed, cooldowns, buffs, consumableCooldown, food, foodTicks, status, experience,
                bedX, bedY, mana, maxMana);

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
                int id = r.ReadInt32();
                var at = r.ReadCompact();
                var velocity = r.ReadCompact();
                float radius = r.ReadByte() / 32f;
                byte owner = r.ReadByte();
                projectiles.Add(new ProjectileState(id, at, velocity, radius, (owner & 0x80) != 0, (SkillTag)(owner & 0x7f)));
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
            return new SnapshotMessage(tick, self, entities, projectiles, hazards, events, clock, sleepers, rift, defense);
        });
    }
}
