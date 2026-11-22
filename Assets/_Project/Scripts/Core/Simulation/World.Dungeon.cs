using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public enum DungeonState : byte
    {
        Closed,
        Open,
        // Its end reached (5b: the boss down and the chest opened); still there until everyone has gone out.
        Done,
    }

    // A dungeon made in a slot of the pocket (DungeonPocket) for one gate: whoever comes in by that gate while it stands
    // comes here (Docs/WorldRework.md 5단계 협동).
    public sealed class DungeonInstance
    {
        internal DungeonInstance(int slot, DungeonGate gate, int serial, ulong seed, int level, DungeonLayout layout)
        {
            Slot = slot;
            Gate = gate;
            Serial = serial;
            Seed = seed;
            Level = level;
            Layout = layout;
            State = DungeonState.Open;
        }

        public int Slot { get; }
        public DungeonGate Gate { get; }
        // How many times its gate had been entered before this one was made (its seed's part).
        public int Serial { get; }
        public ulong Seed { get; }
        // The level of what is in it, and its loot: the zone's at its gate.
        public int Level { get; }
        public DungeonLayout Layout { get; }
        public DungeonState State { get; internal set; }
        public Biome Biome => Gate.Biome;
        public string Name => World.DungeonName(Gate.Biome);
        // Ticks no one has been in it.
        internal int EmptyTicks { get; set; }
    }

    // Instance dungeons (Docs/WorldRework.md 5단계, 묶음 5a): the pocket above the world and the gates out in it. A player at a
    // gate goes in (EnterDungeon, EnterSeconds to gather) and comes out at its entrance; by a door out of it, or from the
    // menu anywhere in it (LeaveByMenu, longer), they come out at the gate. One who falls in it gets up at its entrance,
    // however often (the person's decision, 2026-11-21). A dungeon nobody has been in for EmptySeconds goes back to rock;
    // the next one in by its gate gets a new one. What stays of it is how often each gate was entered (the world's save,
    // format 15). In the pocket nothing is dug or built (TileMap.IsEdge), no monster of the field comes and the pacing
    // director leaves players alone.
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

        List<DungeonGate> _gates = new List<DungeonGate>();
        int[] _entered = Array.Empty<int>();
        DungeonInstance[] _dungeons = Array.Empty<DungeonInstance>();

        public IReadOnlyList<DungeonGate> Gates => _gates;
        // The dungeons standing in the pocket now, by slot.
        public IEnumerable<DungeonInstance> Dungeons => _dungeons.Where(d => d != null);
        // Goes up whenever a dungeon is made or goes back to rock, so the host tells the players (DungeonsMessage).
        public int DungeonVersion { get; private set; }

        // A dungeon's name until dungeons.csv (5b) names each: its biome's.
        public static string DungeonName(Biome biome) => $"{BiomeMap.Name(biome)}의 던전";

        void SetGates(RoomGraph rooms)
        {
            _gates = DungeonPocket.Gates(rooms, Map);
            _entered = new int[_gates.Count];
            _dungeons = new DungeonInstance[DungeonPocket.SlotsOf(Map)];
        }

        // A gate laid by hand (tests, sims): the floor at x, y leads into a dungeon of that biome.
        public DungeonGate AddGate(int x, int y, Biome biome = Biome.Cave)
        {
            var gate = new DungeonGate(_gates.Count, x, y, biome);
            _gates.Add(gate);
            Array.Resize(ref _entered, _gates.Count);
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

        // The players in a dungeon, standing or fallen.
        public IEnumerable<Player> PlayersIn(DungeonInstance dungeon) => _players.Where(p => dungeon != null && DungeonAt(p.Position) == dungeon);

        // How often each gate has been entered (the world's save, format 15), and those numbers back.
        public IEnumerable<(int Gate, int Entered)> DungeonEntries => Enumerable.Range(0, _entered.Length).Where(i => _entered[i] > 0).Select(i => (i, _entered[i]));

        public void RestoreDungeons(IEnumerable<(int Gate, int Entered)> entries)
        {
            foreach (var (gate, entered) in entries ?? Enumerable.Empty<(int, int)>())
                if (gate >= 0 && gate < _entered.Length) _entered[gate] = Math.Max(0, entered);
        }

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

        // The gate's dungeon, made in a free slot when it is not standing; null when every slot is taken.
        DungeonInstance OpenDungeon(DungeonGate gate)
        {
            if (DungeonOf(gate) is DungeonInstance standing) return standing;
            int slot = Array.IndexOf(_dungeons, null);
            if (slot < 0) return null;
            int serial = _entered[gate.Index]++;
            ulong seed = DungeonPocket.SeedFor(_forageSeed, gate.Index, serial);
            var layout = DungeonPocket.Carve(Map, slot, seed);
            var dungeon = new DungeonInstance(slot, gate, serial, seed, ZoneLevelAt(gate.Center), layout);
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

        // Once a tick: a dungeon nobody has been in (nor is coming into) for EmptySeconds goes back to rock.
        void UpdateDungeons()
        {
            foreach (var dungeon in _dungeons)
            {
                if (dungeon == null) continue;
                dungeon.EmptyTicks = Used(dungeon) ? 0 : dungeon.EmptyTicks + 1;
                if (dungeon.EmptyTicks >= Ticks(EmptySeconds)) CloseDungeon(dungeon);
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
    }
}
