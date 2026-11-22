using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public enum DungeonState : byte
    {
        Closed,
        Open,
        // Its end reached (the boss down and the end chest opened); still there until everyone has gone out.
        Done,
    }

    // How far a run through a dungeon has come (묶음 5b): the host keeps it, the clients get it with each DungeonsMessage
    // and open the same doors from it (DungeonInstance.ApplyTiles).
    [Flags]
    public enum DungeonFlags : ushort
    {
        None = 0,
        // The key room's pack fell: the locked room's grate opens to whoever comes to it.
        KeyTaken = 1,
        KeyDoorOpen = 2,
        // The hidden room's cracked wall was broken.
        CrackOpen = 4,
        // Touched in the rest room, the shortcut's stone at the entrance leads there.
        ShortcutOpen = 8,
        // The dungeon's master fell: the end room's grate is open.
        BossDown = 16,
        // The end chest was opened: the run is done.
        ChestOpen = 32,
        // That run was the dungeon's fastest yet, and the world's first of the day there (the day's reward).
        BestTime = 64,
        FirstToday = 128,
    }

    // What the world keeps of a dungeon between runs (its save, format 16): how often it was entered (its seeds' part),
    // its fastest run and the day of its last (the day's first run gives more). By the dungeon's key (DungeonDefinition.KeyFor).
    public sealed class DungeonRecord
    {
        public DungeonRecord(string key, int entered = 0, int bestSeconds = 0, int lastClearDay = -1, int clears = 0)
        {
            Key = key;
            Entered = entered;
            BestSeconds = bestSeconds;
            LastClearDay = lastClearDay;
            Clears = clears;
        }

        public string Key { get; }
        public int Entered { get; internal set; }
        // 0 before the first run to its end.
        public int BestSeconds { get; internal set; }
        public int LastClearDay { get; internal set; }
        public int Clears { get; internal set; }
    }

    // A dungeon made in a slot of the pocket (DungeonPocket) for one gate: whoever comes in by that gate while it stands
    // comes here (Docs/WorldRework.md 5단계 협동). The host runs its rooms (World.Dungeon.cs); a client lays it from the same
    // seed and keeps the progress the host sends.
    public sealed class DungeonInstance
    {
        internal DungeonInstance(int slot, DungeonGate gate, int serial, ulong seed, int level, DungeonLayout layout, string key = null)
        {
            Slot = slot;
            Gate = gate;
            Serial = serial;
            Seed = seed;
            Level = level;
            Layout = layout;
            Key = key ?? World.GateKey(gate.Index);
            State = DungeonState.Open;
            PackIds = new int[layout.Nodes.Count];
        }

        public int Slot { get; }
        public DungeonGate Gate { get; }
        // How many times its dungeon had been entered before this one was made (its seed's part).
        public int Serial { get; }
        public ulong Seed { get; }
        // The zone level of what is in it, and of its loot: its row's, or the gate's if higher.
        public int Level { get; }
        public DungeonLayout Layout { get; }
        // What its record is kept under: its row's id (and biome for a row of any biome), or the gate's number for 5a's plain one.
        public string Key { get; }
        public DungeonState State { get; internal set; }
        public DungeonDefinition Definition => Layout.Definition;
        public Biome Biome => Gate.Biome;
        public string Name => Definition?.NameFor(Gate.Biome) ?? World.DungeonName(Gate.Biome);
        public DungeonFlags Flags { get; internal set; }
        // A bit for each room whose pack fell (Layout.Nodes order), and for each thing used (Layout.Things order).
        public uint ClearedRooms { get; internal set; }
        public uint UsedThings { get; internal set; }
        // The host's tick the first player came in, or -1 before; the seconds the run took, once done.
        public long Started { get; internal set; } = -1;
        public int ClearSeconds { get; internal set; }
        // How often a player fell in it.
        public int Deaths { get; internal set; }
        // Ticks no one has been in it.
        internal int EmptyTicks { get; set; }
        // Each room's pack (0: none yet), and the master's id once it came.
        internal int[] PackIds { get; }
        internal uint WokenRooms { get; set; }
        internal int BossId { get; set; }

        public bool Has(DungeonFlags flag) => (Flags & flag) == flag;
        public bool RoomCleared(int room) => room >= 0 && room < 32 && (ClearedRooms & 1u << room) != 0;
        public bool RoomWoken(int room) => room >= 0 && room < 32 && (WokenRooms & 1u << room) != 0;
        public bool ThingUsed(int thing) => thing >= 0 && thing < 32 && (UsedThings & 1u << thing) != 0;

        // The tiles its progress changed, laid over what its seed laid: the doors opened, the chests opened. The host and
        // every client lay the same, so none of it goes in TileChanges or a save.
        internal void ApplyTiles(TileMap map)
        {
            void Open(IEnumerable<(int X, int Y)> tiles)
            {
                foreach (var (x, y) in tiles) map[x, y] = TileType.Floor;
            }
            if (Has(DungeonFlags.KeyDoorOpen)) Open(Layout.KeyDoor);
            if (Has(DungeonFlags.BossDown)) Open(Layout.BossDoor);
            if (Has(DungeonFlags.CrackOpen)) Open(Layout.Crack);
            foreach (var thing in Layout.Things)
                if (thing.Kind == DungeonThingKind.Chest && ThingUsed(thing.Index)) map[thing.X, thing.Y] = TileType.OpenChest;
        }
    }

    // Instance dungeons (Docs/WorldRework.md 5단계, 묶음 5a·5b): the pocket above the world and the gates out in it. A player
    // at a gate goes in (EnterDungeon, EnterSeconds to gather) and comes out at its entrance; by a door out of it, or from
    // the menu anywhere in it (LeaveByMenu, longer), they come out at the gate. One who falls in it gets up at its entrance,
    // however often (the person's decision, 2026-11-21). A dungeon nobody has been in for EmptySeconds goes back to rock;
    // the next one in by its gate gets a new one. In the pocket nothing is dug or built (TileMap.IsEdge) but the hidden
    // room's crack, no monster of the field comes and the pacing director leaves players alone.
    //
    // 묶음 5b: a gate leads to its biome's row of dungeons.csv, laid by DungeonGenerator. Each pack room's pack comes when a
    // player nears it and stays until felled (a fall does not bring it back); the champions' room holds three champions and
    // the boss room the dungeon's master, a big elite with its minions. The key room's pack opens the locked room, the
    // master's fall the end room, where the end chest gives each player there their own roll and, the world's first run of
    // the day there, more. The entrance and the rest room are safe; the rest room's fire heals and its stone opens the
    // shortcut from the entrance. What the world keeps is each dungeon's record (DungeonRecord); each player's character
    // keeps how often they finished each (RecordKind.Dungeon).
    public sealed partial class World
    {
        // How near a gate or a door out a player must stand, from their centre to its tile's.
        public const float GateReach = 2.2f;
        public const float ExitReach = 1.8f;
        public const float EnterSeconds = 1.5f;
        public const float LeaveSeconds = 1.5f;
        public const float MenuLeaveSeconds = 3;
        public const float EmptySeconds = 60;
        // DungeonGoal going out: by a door, or from the menu.
        public const int LeaveByDoor = -1;
        public const int LeaveByMenu = -2;
        // A Dungeon event's Amount.
        public const int DungeonBroken = 0;
        public const int DungeonEntering = 1;
        public const int DungeonEntered = 2;
        public const int DungeonLeft = 3;
        public const int DungeonFull = 4;
        public const int DungeonRose = 5;
        public const int DungeonLeaving = 6;
        public const int DungeonLeavingMenu = 7;
        // 묶음 5b: the key taken, a door opened (the locked room's, the end room's, the crack), the master woken and down, a
        // room's pack felled, the shortcut opened and taken, a chest or shrine used, the run done.
        public const int DungeonKey = 8;
        public const int DungeonDoorOpened = 9;
        public const int DungeonBossWoke = 10;
        public const int DungeonBossDown = 11;
        public const int DungeonRoomCleared = 12;
        public const int DungeonShortcutOpened = 13;
        public const int DungeonShortcutTaken = 14;
        public const int DungeonUsed = 15;
        public const int DungeonCleared = 16;

        // A room's pack comes when a living player is this near its box (so it stands there before they see in).
        public const float DungeonWakeReach = 12;
        // A pack room holds at least this many, and one more of its first kind for each player past the first
        // (World.PackExtraPerPlayer): the design's four to eight, and as many more as players.
        public const int DungeonPackLeast = 4;
        public const int DungeonPackMost = 8;
        // The master: a leader of its kind with two affixes and BigEliteHealth times its zone's health (in place of a
        // camp leader's LeaderHealth), and this share more for each player past the first, as the bosses have.
        public const int BigEliteAffixes = 2;
        public const float BigEliteHealth = 12;
        public const float BigElitePerPlayer = 0.6f;
        // Nobody standing in the boss room this long: its master is whole again (the design's "보스는 가득 찬다").
        public const float BossResetSeconds = 3;
        // The shortcut's stones and the locked room's grate open to a player this near.
        public const float StoneReach = 1.6f;
        public const float DoorReach = 3;
        // The day's first run of a dungeon in the world gives each at its end chest a rare piece or better and a rune more.
        public const int FirstRunItems = 1;
        public const int FirstRunRunes = 1;
        const int DungeonCheckTicks = 5;

        List<DungeonGate> _gates = new List<DungeonGate>();
        readonly Dictionary<string, DungeonRecord> _dungeonRecords = new Dictionary<string, DungeonRecord>();
        // A row chosen for a gate's next dungeon (playtests, tests), in place of its biome's.
        readonly Dictionary<int, DungeonDefinition> _chosenDungeons = new Dictionary<int, DungeonDefinition>();
        DungeonInstance[] _dungeons = Array.Empty<DungeonInstance>();
        readonly Dictionary<int, int> _bossAway = new Dictionary<int, int>();

        public IReadOnlyList<DungeonGate> Gates => _gates;
        // The dungeons standing in the pocket now, by slot.
        public IEnumerable<DungeonInstance> Dungeons => _dungeons.Where(d => d != null);
        // Goes up whenever a dungeon is made, goes back to rock or its run moves on, so the host tells the players
        // (DungeonsMessage).
        public int DungeonVersion { get; private set; }

        // A dungeon's name without a row of dungeons.csv (5a's plain one): its biome's.
        public static string DungeonName(Biome biome) => $"{BiomeMap.Name(biome)}의 던전";

        // The record key of 5a's plain dungeon behind a gate (no dungeons.csv).
        public static string GateKey(int gate) => $"gate{gate}";

        // The row a gate leads to with these sheets (DungeonData.For its biome), or null.
        public static DungeonDefinition DungeonFor(DungeonData data, DungeonGate gate) => gate == null ? null : data?.For(gate.Biome);

        public static string DungeonNameOf(DungeonData data, DungeonGate gate) => DungeonFor(data, gate)?.NameFor(gate.Biome) ?? DungeonName(gate.Biome);

        DungeonData DungeonSheetData => Tiles?.Terrain?.Dungeons;

        public DungeonDefinition DungeonFor(DungeonGate gate) =>
            gate != null && _chosenDungeons.TryGetValue(gate.Index, out var chosen) ? chosen : DungeonFor(DungeonSheetData, gate);

        // The next dungeon made for the gate is this row of dungeons.csv (playtests, tests); false without such a row.
        public bool ChooseDungeon(int gate, string id)
        {
            var row = DungeonSheetData?.Find(id);
            if (row == null || gate < 0 || gate >= _gates.Count) return false;
            _chosenDungeons[gate] = row;
            return true;
        }

        void SetGates(RoomGraph rooms)
        {
            _gates = DungeonPocket.Gates(rooms, Map);
            _dungeons = new DungeonInstance[DungeonPocket.SlotsOf(Map)];
        }

        // A gate laid by hand (tests, sims): the floor at x, y leads into a dungeon of that biome.
        public DungeonGate AddGate(int x, int y, Biome biome = Biome.Cave)
        {
            var gate = new DungeonGate(_gates.Count, x, y, biome);
            _gates.Add(gate);
            if (_dungeons.Length != DungeonPocket.SlotsOf(Map)) _dungeons = new DungeonInstance[DungeonPocket.SlotsOf(Map)];
            return gate;
        }

        // Whether a spot is in the pocket above the world (a dungeon, or the rock between them).
        public bool InPocket(Vec2 position) => Map.InPocket(position);

        // Which part of the map a spot is in: -1 the world, a slot of the pocket, or MaxSlots for the pocket's rock past
        // the slots. The host sends a player only what is in their part (WorldHost.Snapshot).
        public int AreaOf(Vec2 position) => !InPocket(position) ? -1 : DungeonPocket.SlotAt(Map, position) is int slot && slot >= 0 ? slot : DungeonPocket.MaxSlots;

        // The dungeon standing where the spot is, or null.
        public DungeonInstance DungeonAt(Vec2 position)
        {
            int slot = DungeonPocket.SlotAt(Map, position);
            return slot >= 0 && slot < _dungeons.Length ? _dungeons[slot] : null;
        }

        // The dungeon standing for a gate, or null.
        public DungeonInstance DungeonOf(DungeonGate gate) => gate == null ? null : _dungeons.FirstOrDefault(d => d != null && d.Gate == gate);

        // The gate within GateReach of the spot, nearest first, or null.
        public static DungeonGate GateNear(IEnumerable<DungeonGate> gates, Vec2 position)
        {
            DungeonGate best = null;
            float bestDistance = GateReach;
            foreach (var gate in gates)
            {
                float distance = Vec2.Distance(position, gate.Center);
                if (distance > bestDistance) continue;
                (best, bestDistance) = (gate, distance);
            }
            return best;
        }

        public DungeonGate GateNear(Vec2 position) => InPocket(position) ? null : GateNear(_gates, position);

        // A door out of the dungeon within ExitReach of the spot, or null.
        public static (int X, int Y)? ExitNear(DungeonLayout layout, Vec2 position)
        {
            if (layout == null) return null;
            foreach (var exit in layout.Exits)
                if (Vec2.Distance(position, TileMap.Center(exit.X, exit.Y)) <= ExitReach) return exit;
            return null;
        }

        // The thing within PoiReach of the spot that can be used now (a chest not opened, the end chest once the master
        // fell, a shrine not yet used), nearest first; the fire is not used by hand. Null without one.
        public static DungeonThing ThingNear(DungeonInstance dungeon, Vec2 position)
        {
            if (dungeon == null) return null;
            DungeonThing best = null;
            float bestDistance = PoiReach;
            foreach (var thing in dungeon.Layout.Things)
            {
                if (thing.Kind == DungeonThingKind.Fire || dungeon.ThingUsed(thing.Index) || thing.End && !dungeon.Has(DungeonFlags.BossDown)) continue;
                float distance = Vec2.Distance(position, thing.Center);
                if (distance > bestDistance) continue;
                (best, bestDistance) = (thing, distance);
            }
            return best;
        }

        // The players in a dungeon, standing or fallen.
        public IEnumerable<Player> PlayersIn(DungeonInstance dungeon) => _players.Where(p => dungeon != null && DungeonAt(p.Position) == dungeon);

        // ---- The records (the world's save, format 16) ----

        DungeonRecord RecordOf(string key)
        {
            if (!_dungeonRecords.TryGetValue(key, out var record)) _dungeonRecords[key] = record = new DungeonRecord(key);
            return record;
        }

        public IEnumerable<DungeonRecord> DungeonRecords => _dungeonRecords.Values.Where(r => r.Entered > 0 || r.Clears > 0).OrderBy(r => r.Key, StringComparer.Ordinal);

        public DungeonRecord DungeonRecordOf(string key) => key != null && _dungeonRecords.TryGetValue(key, out var record) ? record : null;

        // Puts back what a save kept; a save of format 15 counted by gate (entered only), and those go to the key that
        // gate leads to now.
        public void RestoreDungeons(IEnumerable<DungeonRecord> records, IEnumerable<(int Gate, int Entered)> byGate = null)
        {
            foreach (var saved in records ?? Enumerable.Empty<DungeonRecord>())
            {
                if (string.IsNullOrEmpty(saved.Key)) continue;
                var record = RecordOf(saved.Key);
                record.Entered = Math.Max(record.Entered, saved.Entered);
                record.BestSeconds = saved.BestSeconds;
                record.LastClearDay = saved.LastClearDay;
                record.Clears = saved.Clears;
            }
            foreach (var (gate, entered) in byGate ?? Enumerable.Empty<(int, int)>())
            {
                if (gate < 0 || gate >= _gates.Count) continue;
                var record = RecordOf(DungeonFor(_gates[gate])?.KeyFor(_gates[gate].Biome) ?? GateKey(gate));
                record.Entered = Math.Max(record.Entered, entered);
            }
        }

        // ---- Going in and out ----

        // A player at a gate starts going in: the gate's dungeon is made now if it is not standing (so every client has
        // laid it before they arrive) and they come in after EnterSeconds, unless they step off the gate, are hit or fall.
        // False when they cannot: no gate of that index in reach, no pocket, or every slot taken (a DungeonFull event).
        public bool EnterDungeon(int playerId, int gateIndex)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || player.Riding || player.DungeonCharging || gateIndex < 0 || gateIndex >= _gates.Count) return false;
            var gate = _gates[gateIndex];
            if (InPocket(player.Position) || Vec2.Distance(player.Position, gate.Center) > GateReach || _dungeons.Length == 0) return false;
            if (OpenDungeon(gate) == null)
            {
                _events.Add(new SimEvent(SimEventType.Dungeon, player.Id, amount: DungeonFull, position: gate.Center));
                return false;
            }
            BreakRecall(player);
            player.DungeonTicks = Ticks(EnterSeconds);
            player.DungeonGoal = gate.Index;
            _events.Add(new SimEvent(SimEventType.Dungeon, player.Id, amount: DungeonEntering, position: gate.Center));
            return true;
        }

        // A player in a dungeon starts going out: by a door (within ExitReach of one, LeaveSeconds) or from the menu
        // anywhere in it (MenuLeaveSeconds). A hit stops it; going by a door, so does stepping off it.
        public bool LeaveDungeon(int playerId, bool byMenu)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || player.DungeonCharging) return false;
            var dungeon = DungeonAt(player.Position);
            if (dungeon == null || !byMenu && ExitNear(dungeon.Layout, player.Position) == null) return false;
            player.DungeonTicks = Ticks(byMenu ? MenuLeaveSeconds : LeaveSeconds);
            player.DungeonGoal = byMenu ? LeaveByMenu : LeaveByDoor;
            _events.Add(new SimEvent(SimEventType.Dungeon, player.Id, amount: byMenu ? DungeonLeavingMenu : DungeonLeaving, position: player.Position));
            return true;
        }

        // The gate's dungeon, made in a free slot when it is not standing; null when every slot is taken. Its row is the
        // gate's biome's in dungeons.csv; without the sheet, 5a's plain dungeon.
        DungeonInstance OpenDungeon(DungeonGate gate)
        {
            if (DungeonOf(gate) is DungeonInstance standing) return standing;
            int slot = Array.IndexOf(_dungeons, null);
            if (slot < 0) return null;
            var definition = DungeonFor(gate);
            string key = definition?.KeyFor(gate.Biome) ?? GateKey(gate.Index);
            var record = RecordOf(key);
            int serial = record.Entered++;
            DungeonLayout layout;
            ulong seed;
            if (definition != null)
            {
                seed = DungeonPocket.SeedFor(_forageSeed, key, serial);
                layout = DungeonGenerator.Lay(Map, slot, seed, definition, gate.Biome, Tiles.Terrain);
            }
            else
            {
                seed = DungeonPocket.SeedFor(_forageSeed, gate.Index, serial);
                layout = DungeonPocket.Carve(Map, slot, seed);
            }
            int level = Math.Max(definition?.Level ?? 0, ZoneLevelAt(gate.Center));
            var dungeon = new DungeonInstance(slot, gate, serial, seed, level, layout, key);
            _dungeons[slot] = dungeon;
            Biomes.SetPocket(slot, gate.Biome);
            DungeonVersion++;
            return dungeon;
        }

        // Its slot back to rock, and whatever was left in it gone.
        void CloseDungeon(DungeonInstance dungeon)
        {
            dungeon.State = DungeonState.Closed;
            _dungeons[dungeon.Slot] = null;
            bool Inside(Vec2 at) => DungeonPocket.SlotAt(Map, at) == dungeon.Slot;
            foreach (int id in dungeon.PackIds) _packs.Remove(id);
            _bossAway.Remove(dungeon.Slot);
            _enemies.RemoveAll(e => Inside(e.Position));
            _groundLoot.RemoveAll(l => Inside(l.Position));
            _hazards.RemoveAll(h => Inside(h.Position));
            _projectiles.RemoveAll(p => Inside(p.Position));
            DungeonPocket.Clear(Map, dungeon.Slot);
            DungeonVersion++;
        }

        // Each tick for each living player: going in or out.
        void UpdateDungeonCharge(Player player)
        {
            if (!player.DungeonCharging) return;
            // Going in by a gate, or out by a door: stepping off it stops it.
            if (player.DungeonGoal >= 0)
            {
                var gate = player.DungeonGoal < _gates.Count ? _gates[player.DungeonGoal] : null;
                if (gate == null || Vec2.Distance(player.Position, gate.Center) > GateReach + 0.3f || player.Riding)
                {
                    BreakDungeonCharge(player);
                    return;
                }
            }
            else if (player.DungeonGoal == LeaveByDoor && ExitNear(DungeonAt(player.Position)?.Layout, player.Position) == null)
            {
                BreakDungeonCharge(player);
                return;
            }
            if (--player.DungeonTicks > 0) return;
            if (player.DungeonGoal >= 0) ArriveInDungeon(player, _gates[player.DungeonGoal]);
            else if (DungeonAt(player.Position) is DungeonInstance dungeon) LeaveTo(player, dungeon.Gate);
        }

        void ArriveInDungeon(Player player, DungeonGate gate)
        {
            // Made at the start; made again should it have gone back to rock meanwhile.
            var dungeon = OpenDungeon(gate);
            if (dungeon == null)
            {
                _events.Add(new SimEvent(SimEventType.Dungeon, player.Id, amount: DungeonFull, position: gate.Center));
                return;
            }
            dungeon.EmptyTicks = 0;
            if (dungeon.Started < 0)
            {
                dungeon.Started = Tick;
                DungeonVersion++;
            }
            player.Position = Collision.Resolve(Map, dungeon.Layout.Entrance, PlayerRadius);
            player.Corrections++;
            EndFishing(player, FishingReeled);
            player.Sleeping = false;
            _events.Add(new SimEvent(SimEventType.Dungeon, player.Id, amount: DungeonEntered, position: player.Position));
        }

        void LeaveTo(Player player, DungeonGate gate)
        {
            player.Position = Collision.Resolve(Map, gate.Center, PlayerRadius);
            player.Corrections++;
            _events.Add(new SimEvent(SimEventType.Dungeon, player.Id, amount: DungeonLeft, position: player.Position));
        }

        // A hit, a fall or a step away stops going in or out.
        void BreakDungeonCharge(Player player)
        {
            if (!player.DungeonCharging) return;
            player.DungeonTicks = 0;
            _events.Add(new SimEvent(SimEventType.Dungeon, player.Id, amount: DungeonBroken, position: player.Position));
        }

        // Where one who fell in a dungeon gets up: at its entrance (null out of one).
        Vec2? DungeonRising(Player player) =>
            DungeonAt(player.Position) is DungeonInstance dungeon ? Collision.Resolve(Map, dungeon.Layout.Entrance, PlayerRadius) : (Vec2?)null;

        // A player fell: counted against the run of the dungeon they fell in.
        void NoteDungeonFall(Player player)
        {
            if (DungeonAt(player.Position) is DungeonInstance dungeon) dungeon.Deaths++;
        }

        // Once a tick: each dungeon's run, and a dungeon nobody has been in (nor is coming into) for EmptySeconds goes back
        // to rock.
        void UpdateDungeons()
        {
            foreach (var dungeon in _dungeons)
            {
                if (dungeon == null) continue;
                dungeon.EmptyTicks = Used(dungeon) ? 0 : dungeon.EmptyTicks + 1;
                if (dungeon.EmptyTicks >= Ticks(EmptySeconds))
                {
                    CloseDungeon(dungeon);
                    continue;
                }
                if (dungeon.Layout.Nodes.Count > 0) RunDungeon(dungeon);
            }
        }

        bool Used(DungeonInstance dungeon) => _players.Any(p => DungeonAt(p.Position) == dungeon || p.DungeonCharging && p.DungeonGoal == dungeon.Gate.Index);

        // Every dungeon nobody is in nor coming into back to rock now, without the wait (playtests); how many.
        public int CloseEmptyDungeons()
        {
            int closed = 0;
            foreach (var dungeon in _dungeons.Where(d => d != null && !Used(d)).ToList())
            {
                CloseDungeon(dungeon);
                closed++;
            }
            return closed;
        }

        // ---- A run (묶음 5b) ----

        void Progress(DungeonInstance dungeon, DungeonFlags flag)
        {
            if (dungeon.Has(flag)) return;
            dungeon.Flags |= flag;
            dungeon.ApplyTiles(Map);
            DungeonVersion++;
        }

        void TellDungeon(DungeonInstance dungeon, int what, Vec2 at, int source = 0) =>
            _events.Add(new SimEvent(SimEventType.Dungeon, source, amount: what, position: at));

        void RunDungeon(DungeonInstance dungeon)
        {
            var layout = dungeon.Layout;
            var inside = _players.Where(p => !p.IsDead && DungeonAt(p.Position) == dungeon).ToList();
            foreach (var player in inside)
            {
                // The rest room's fire heals as a camp's does.
                foreach (var fire in layout.Things.Where(t => t.Kind == DungeonThingKind.Fire))
                {
                    if (Vec2.Distance(player.Position, fire.Center) > CampRestReach) continue;
                    float rate = (fire.Definition?.Reward(PoiRewardKind.Rest)?.Min ?? 10) / 100f * TickSeconds;
                    player.Health = Math.Min(player.Stats.MaxHealth, player.Health + player.Stats.MaxHealth * rate);
                    player.Mana = Math.Min(player.Stats.MaxMana, player.Mana + player.Stats.MaxMana * rate);
                }
                // The shortcut: its stone in the rest room opens it; then the entrance's takes whoever touches it there.
                if (layout.ShortcutTo is (int tx, int ty) && layout.ShortcutFrom is (int fx, int fy))
                {
                    if (!dungeon.Has(DungeonFlags.ShortcutOpen) && Vec2.Distance(player.Position, TileMap.Center(tx, ty)) <= StoneReach)
                    {
                        Progress(dungeon, DungeonFlags.ShortcutOpen);
                        TellDungeon(dungeon, DungeonShortcutOpened, TileMap.Center(tx, ty), player.Id);
                    }
                    else if (dungeon.Has(DungeonFlags.ShortcutOpen) && !player.DungeonCharging && Vec2.Distance(player.Position, TileMap.Center(fx, fy)) <= StoneReach)
                    {
                        var to = TileMap.Center(tx, ty);
                        var rest = layout.Room('R');
                        var toward = rest != null ? (rest.Center - to).Normalized : new Vec2(0, 1);
                        player.Position = Collision.Resolve(Map, to + toward * 1.6f, PlayerRadius);
                        player.Corrections++;
                        TellDungeon(dungeon, DungeonShortcutTaken, player.Position, player.Id);
                    }
                }
                // The locked room's grate, the key in hand.
                if (dungeon.Has(DungeonFlags.KeyTaken) && !dungeon.Has(DungeonFlags.KeyDoorOpen)
                    && layout.KeyDoor.Any(d => Vec2.Distance(player.Position, TileMap.Center(d.X, d.Y)) <= DoorReach))
                {
                    Progress(dungeon, DungeonFlags.KeyDoorOpen);
                    var (dx, dy) = layout.KeyDoor[layout.KeyDoor.Count / 2];
                    TellDungeon(dungeon, DungeonDoorOpened, TileMap.Center(dx, dy), player.Id);
                }
            }
            if (Tick % DungeonCheckTicks != 0) return;

            // The packs: each comes as a player nears its room, and its room is cleared when the last of it falls.
            foreach (var room in layout.Nodes)
            {
                if (!DungeonSheet.HasPack(room.Letter) || dungeon.RoomCleared(room.Index)) continue;
                if (!dungeon.RoomWoken(room.Index))
                {
                    if (inside.Any(p => room.Contains(p.Position, DungeonWakeReach))) WakeRoom(dungeon, room, inside.Count);
                    continue;
                }
                int pack = dungeon.PackIds[room.Index];
                if (_enemies.Any(e => e.PackId == pack && !e.IsDead)) continue;
                dungeon.ClearedRooms |= 1u << room.Index;
                DungeonVersion++;
                TellDungeon(dungeon, DungeonRoomCleared, room.Center);
                if (room.Letter == 'K')
                {
                    Progress(dungeon, DungeonFlags.KeyTaken);
                    TellDungeon(dungeon, DungeonKey, room.Center);
                }
            }

            // The master: down, or whole again with nobody standing in its room.
            if (dungeon.BossId != 0 && !dungeon.Has(DungeonFlags.BossDown))
            {
                var boss = _enemies.FirstOrDefault(e => e.Id == dungeon.BossId && !e.IsDead);
                var room = layout.Room('B');
                if (boss == null)
                {
                    Progress(dungeon, DungeonFlags.BossDown);
                    TellDungeon(dungeon, DungeonBossDown, room?.Center ?? layout.Entrance);
                    var (dx, dy) = layout.BossDoor.Count > 0 ? layout.BossDoor[layout.BossDoor.Count / 2] : (TileMap.Cell(layout.Entrance.X), TileMap.Cell(layout.Entrance.Y));
                    TellDungeon(dungeon, DungeonDoorOpened, TileMap.Center(dx, dy));
                }
                else if (room != null && !inside.Any(p => room.Contains(p.Position, 2)))
                {
                    _bossAway.TryGetValue(dungeon.Slot, out int away);
                    _bossAway[dungeon.Slot] = away += DungeonCheckTicks;
                    if (away >= Ticks(BossResetSeconds) && boss.Health < boss.MaxHealth)
                    {
                        boss.Health = boss.MaxHealth;
                        boss.Summons = 0;
                        if (boss.State != EnemyState.Idle && !boss.Returning) TurnBack(boss);
                    }
                }
                else _bossAway[dungeon.Slot] = 0;
            }
        }

        // The pack of a room comes: an ordinary pack of the dungeon's biome and level (four to eight, more for more
        // players), three champions in the champions' room, the master and its minions in the boss room.
        void WakeRoom(DungeonInstance dungeon, DungeonRoom room, int players)
        {
            dungeon.WokenRooms |= 1u << room.Index;
            var random = new GameRandom(dungeon.Seed, 60 + (ulong)room.Index);
            var choices = DungeonPacks(dungeon);
            if (choices.Count == 0)
            {
                dungeon.ClearedRooms |= 1u << room.Index;
                DungeonVersion++;
                return;
            }
            var definition = random.PickWeighted(choices, p => p.Weight);
            int extra = PackExtraPerPlayer * Math.Max(0, players - 1);
            var roster = new List<MonsterDefinition>();
            var rank = EliteRank.None;
            var affixes = RiftAffix.None;
            if (room.Letter == 'B')
            {
                var lead = (dungeon.Definition?.Boss is string id ? _monsters.Find(id) : null) ?? LeaderKind(definition);
                rank = EliteRank.Leader;
                affixes = RollAffixes(LeaderAffixes, BigEliteAffixes, random);
                roster.Add(lead);
                int minions = MinMinions + random.Next(MaxMinions - MinMinions + 1) + extra;
                if ((affixes & RiftAffix.Swarm) != 0) minions = (int)MathF.Round(minions * SwarmShare);
                var own = definition.Members.Select(m => m.Monster).Where(m => m != lead).ToList();
                for (int i = 0; i < minions; i++) roster.Add(own.Count > 0 ? own[random.Next(own.Count)] : MinionKind(definition, lead));
            }
            else if (room.Letter == 'M')
            {
                rank = EliteRank.Champion;
                affixes = RollAffixes(ChampionAffixes, 1, random);
                for (int i = 0; i < ChampionCount; i++) roster.Add(LeaderKind(definition));
            }
            else
            {
                foreach (var member in definition.Members)
                {
                    int count = member.Min + random.Next(member.Max - member.Min + 1);
                    for (int i = 0; i < count; i++) roster.Add(member.Monster);
                }
                while (roster.Count < DungeonPackLeast) roster.Add(definition.Members[0].Monster);
                while (roster.Count > DungeonPackMost) roster.RemoveAt(roster.Count - 1);
                for (int i = 0; i < extra; i++) roster.Add(definition.Members[0].Monster);
            }

            var pack = new Pack(_nextPackId++, null, 0, room.Center) { Definition = definition, Active = true, Alerted = false, Day = Today, Shares = true, Elite = rank, Affixes = affixes };
            _packs[pack.Id] = pack;
            dungeon.PackIds[room.Index] = pack.Id;
            bool Fits(Vec2 at)
            {
                int x = TileMap.Cell(at.X), y = TileMap.Cell(at.Y);
                return room.Contains(x, y) && !Map.IsSolid(x, y) && Map[x, y] != TileType.Lava && Map[x, y] != TileType.Water
                    && !Collision.Overlaps(Map, at, 0.45f, monster: true);
            }
            var middle = room.Elite ?? room.Center;
            if (!Fits(middle)) middle = Spread(room.Center, new List<Vec2>(), 1, random, Fits).DefaultIfEmpty(room.Center).First();
            var spots = new List<Vec2>();
            if (rank != EliteRank.None) spots.Add(middle);
            spots.AddRange(Spread(middle, room.Monsters.Where(Fits).ToList(), roster.Count - spots.Count, random,
                at => Fits(at) && spots.All(s => Vec2.Distance(s, at) >= PackSpacingMin)));
            while (spots.Count < roster.Count) spots.Add(middle);
            for (int i = 0; i < roster.Count; i++)
            {
                var enemy = Join(pack, roster[i], Collision.Resolve(Map, spots[i], roster[i].Radius, monster: true), -1, dungeon.Level, random);
                if (rank == EliteRank.Champion || rank == EliteRank.Leader && i == 0) MakeElite(enemy, rank, affixes);
                if (room.Letter != 'B' || i != 0) continue;
                // The master: BigEliteHealth in place of a leader's, and more for each player.
                enemy.MaxHealth = enemy.Health = enemy.MaxHealth * BigEliteHealth / LeaderHealth * (1 + BigElitePerPlayer * Math.Max(0, players - 1));
                dungeon.BossId = enemy.Id;
                TellDungeon(dungeon, DungeonBossWoke, enemy.Position, enemy.Id);
            }
            DungeonVersion++;
        }

        // The packs of packs.csv a dungeon's rooms hold: its biome's own up to its level, else the plain cave's.
        List<PackDefinition> DungeonPacks(DungeonInstance dungeon)
        {
            var packs = PackSheetData?.Packs;
            if (packs == null) return new List<PackDefinition>();
            var own = packs.Where(k => k.Place == null && k.Weight > 0 && k.Biome == dungeon.Biome && k.Zone <= dungeon.Level).ToList();
            if (own.Count > 0) return own;
            return packs.Where(k => k.Place == null && k.Weight > 0 && k.Biome == null && k.Zone <= dungeon.Level).ToList();
        }

        // A monster that came into the entrance or the rest room is put back at its edge and gives up the chase, as at
        // a camp.
        void KeepOutOfDungeonRooms(Enemy enemy)
        {
            if (!Map.InPocket(enemy.Position) || !(DungeonAt(enemy.Position) is DungeonInstance dungeon)) return;
            foreach (var room in dungeon.Layout.Nodes)
            {
                if (room.Letter != 'E' && room.Letter != 'R') continue;
                float radius = enemy.Definition.Radius;
                if (!room.Contains(enemy.Position, radius + 0.5f)) continue;
                var (l, b, w, h) = room.Box;
                float edge = radius + 1;
                var p = enemy.Position;
                var outs = new[]
                {
                    (Dist: p.X - (l - edge), To: new Vec2(l - edge, p.Y)),
                    (Dist: l + w + edge - p.X, To: new Vec2(l + w + edge, p.Y)),
                    (Dist: p.Y - (b - edge), To: new Vec2(p.X, b - edge)),
                    (Dist: b + h + edge - p.Y, To: new Vec2(p.X, b + h + edge)),
                };
                var nearest = outs.OrderBy(o => o.Dist).First().To;
                var put = Collision.Resolve(Map, nearest, radius, monster: true);
                if (!Collision.Overlaps(Map, put, radius, monster: true)) enemy.Position = put;
                if (enemy.State == EnemyState.Chase) enemy.State = EnemyState.Idle;
            }
        }

        // ---- Using what is in a dungeon ----

        // A player opens the chest or uses the shrine of that index in the dungeon they are in, within reach. A chest
        // gives each player in the dungeon within ChestShareReach their own roll; the end chest, once the master fell,
        // ends the run. False when out of reach, used already, or the end chest before the master fell.
        public bool UseDungeonThing(int playerId, int index)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead) return false;
            var dungeon = DungeonAt(player.Position);
            if (dungeon == null || index < 0 || index >= dungeon.Layout.Things.Count) return false;
            var thing = dungeon.Layout.Things[index];
            if (thing.Kind == DungeonThingKind.Fire || dungeon.ThingUsed(index) || thing.End && !dungeon.Has(DungeonFlags.BossDown)) return false;
            if (Vec2.Distance(player.Position, thing.Center) > PoiReach) return false;
            dungeon.UsedThings |= 1u << index;
            var owners = _players.Where(p => !p.IsDead && DungeonAt(p.Position) == dungeon && (p == player || Vec2.Distance(p.Position, thing.Center) <= ChestShareReach)).ToList();
            if (thing.Definition != null)
            {
                var poi = new Poi(-1, thing.Definition, thing.X, thing.Y, thing.Room, dungeon.Biome);
                foreach (var owner in owners) Grant(owner, poi);
            }
            dungeon.ApplyTiles(Map);
            DungeonVersion++;
            TellDungeon(dungeon, DungeonUsed, thing.Center, player.Id);
            if (thing.End) FinishRun(dungeon, owners);
            return true;
        }

        // The end chest opened: the run's time against the dungeon's best, the day's first run's reward, and each one
        // there one more run of this dungeon in their records.
        void FinishRun(DungeonInstance dungeon, List<Player> owners)
        {
            var record = RecordOf(dungeon.Key);
            int seconds = dungeon.Started < 0 ? 0 : (int)Math.Max(1, (Tick - dungeon.Started) / TicksPerSecond);
            dungeon.ClearSeconds = seconds;
            var flags = DungeonFlags.ChestOpen;
            if (record.BestSeconds == 0 || seconds < record.BestSeconds)
            {
                record.BestSeconds = seconds;
                flags |= DungeonFlags.BestTime;
            }
            if (record.LastClearDay != Today)
            {
                record.LastClearDay = Today;
                flags |= DungeonFlags.FirstToday;
                int level = Math.Max(ProgressLevel, dungeon.Level);
                foreach (var owner in owners)
                {
                    for (int i = 0; i < FirstRunItems && _lootGenerator != null; i++)
                        Give(owner, _lootGenerator.RollItem(level, _random, (int)owner.Stats.ItemRarity, Rarity.Rare));
                    for (int i = 0; i < FirstRunRunes && _lootGenerator != null; i++)
                        if (_lootGenerator.RollRune(level, _random) is RuneDefinition rune) owner.Inventory.AddRune(rune);
                    owner.InventoryVersion++;
                }
            }
            record.Clears++;
            foreach (var owner in _players.Where(p => DungeonAt(p.Position) == dungeon)) Record(owner, RecordKind.Dungeon, dungeon.Key);
            dungeon.Flags |= flags;
            dungeon.State = DungeonState.Done;
            DungeonVersion++;
            TellDungeon(dungeon, DungeonCleared, dungeon.Layout.Things.First(t => t.End).Center);
        }

        // Blows on the hidden room's cracked wall (World.Dig): broken, it opens as the dungeon's progress, not as a
        // changed tile. True when the tile was such a wall.
        bool StrikeDungeonCrack(int x, int y, bool broken)
        {
            if (!Map.InPocket(y) || !(DungeonAt(TileMap.Center(x, y)) is DungeonInstance dungeon) || !dungeon.Layout.Crack.Contains((x, y))) return false;
            if (!broken) return true;
            Progress(dungeon, DungeonFlags.CrackOpen);
            TellDungeon(dungeon, DungeonDoorOpened, TileMap.Center(x, y));
            return true;
        }

        // Whether the tile is a dungeon's cracked wall still standing (World.DigTarget lets it be struck in the pocket).
        bool IsDungeonCrack(int x, int y) =>
            Map.InPocket(y) && Map[x, y] == TileType.CrackedWall && DungeonAt(TileMap.Center(x, y)) is DungeonInstance dungeon && dungeon.Layout.Crack.Contains((x, y));
    }
}
