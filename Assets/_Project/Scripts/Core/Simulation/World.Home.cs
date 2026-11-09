using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // Beds and going home (Docs/Crafting.md, 기지와 귀환). A bed belongs to whoever placed it, by name, so it is
    // theirs again when they come back to the world; each player has one at a time. Players get up by their bed when
    // they fall and when they join, and a recall scroll takes them there; without a bed, home is the spawn.
    public sealed partial class World
    {
        public const float RecallSeconds = 2;
        // A Recall event's Amount: started, arrived, broken off by a hit.
        public const int RecallStarted = 1;
        public const int RecallArrived = 2;
        public const int RecallBroken = 0;

        readonly Dictionary<int, string> _beds = new Dictionary<int, string>();

        // Every claimed bed: where it is and whose.
        public IEnumerable<(int X, int Y, string Owner)> Beds =>
            _beds.Select(pair => (pair.Key % Map.Width, pair.Key / Map.Width, pair.Value)).ToList();

        // The player's bed, or null.
        public (int X, int Y)? BedOf(Player player)
        {
            foreach (var pair in _beds)
            {
                if (pair.Value == player.Name) return (pair.Key % Map.Width, pair.Key / Map.Width);
            }
            return null;
        }

        // Where the player gets up: on open floor beside their bed, or the spawn.
        public Vec2 HomeOf(Player player)
        {
            if (BedOf(player) is not (int bx, int by)) return Spawn;
            foreach (var (dx, dy) in new[] { (0, -1), (1, 0), (-1, 0), (0, 1), (1, -1), (-1, -1), (1, 1), (-1, 1) })
            {
                int x = bx + dx;
                int y = by + dy;
                if (Map.InBounds(x, y) && Map[x, y] == TileType.Floor) return TileMap.Center(x, y);
            }
            return Spawn;
        }

        // Puts the player at home; the client snaps to it.
        public void SendHome(Player player)
        {
            var home = HomeOf(player);
            if (player.Position.Equals(home)) return;
            player.Position = home;
            player.Corrections++;
        }

        // Placing a bed makes it the player's, and their old one nobody's.
        void ClaimBed(Player player, int x, int y)
        {
            foreach (int key in _beds.Where(pair => pair.Value == player.Name).Select(pair => pair.Key).ToList()) _beds.Remove(key);
            _beds[y * Map.Width + x] = player.Name;
        }

        void BedTileChanged(int x, int y, TileType type)
        {
            if (type != TileType.Bed) _beds.Remove(y * Map.Width + x);
        }

        internal void RestoreBed(int x, int y, string owner)
        {
            if (Map.InBounds(x, y) && Map[x, y] == TileType.Bed && owner.Length > 0) _beds[y * Map.Width + x] = owner;
        }

        // Locked doors (Docs/Crafting.md, 문 잠그기, 2026-11-09). A player locks the door ahead and it is theirs, by
        // name like a bed: they walk through it as through any door, and everyone else, monsters too, finds it a wall.
        // Only its owner unlocks it or takes it up with a pickaxe. Each lock a door key (y × width + x) and its owner.
        readonly Dictionary<int, string> _locks = new Dictionary<int, string>();

        // Goes up when a lock is put on or comes off, so the host sends the locks again (WorldHost.SendLocks).
        public int LockVersion { get; private set; }

        // Every locked door: where it is and whose.
        public IEnumerable<(int X, int Y, string Owner)> Locks =>
            _locks.Select(pair => (pair.Key % Map.Width, pair.Key / Map.Width, pair.Value)).ToList();

        // Who locked the door, or null.
        public string LockOwner(int x, int y) => Map.InBounds(x, y) && _locks.TryGetValue(y * Map.Width + x, out var owner) ? owner : null;

        // The doors the player walks through (Collision's passable): the ones they locked. Null when they locked none.
        public ISet<int> DoorsFor(Player player)
        {
            HashSet<int> doors = null;
            foreach (var pair in _locks)
            {
                if (pair.Value != player.Name) continue;
                (doors ??= new HashSet<int>()).Add(pair.Key);
            }
            return doors;
        }

        // The door a player at this position facing this way locks or unlocks: the tile ahead where they build, or the
        // one they would dig. The client asks the same to show its buttons.
        public static (int X, int Y)? DoorAhead(TileMap map, Vec2 position, Vec2 facing)
        {
            foreach (var (x, y) in new[] { TileAhead(position, facing), DigAhead(position, facing) })
            {
                if (map.InBounds(x, y) && TileMap.IsDoor(map[x, y])) return (x, y);
            }
            return null;
        }

        // Locks the door ahead. False when it is not an open door, or someone stands in it.
        public bool LockDoor(int playerId)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || DoorAhead(Map, player.Position, player.Facing) is not (int x, int y)) return false;
            if (Map[x, y] != TileType.Door) return false;
            if (_players.Any(p => !p.IsDead && Touches(p.Position, PlayerRadius, x, y))) return false;
            SetTile(x, y, TileType.LockedDoor);
            _locks[y * Map.Width + x] = player.Name;
            LockVersion++;
            return true;
        }

        // Takes the player's lock off the door ahead. False when it is not a door they locked.
        public bool UnlockDoor(int playerId)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || DoorAhead(Map, player.Position, player.Facing) is not (int x, int y)) return false;
            if (LockOwner(x, y) != player.Name) return false;
            SetTile(x, y, TileType.Door);
            return true;
        }

        void LockTileChanged(int x, int y, TileType type)
        {
            if (type != TileType.LockedDoor && _locks.Remove(y * Map.Width + x)) LockVersion++;
        }

        internal void RestoreLock(int x, int y, string owner)
        {
            if (!Map.InBounds(x, y) || Map[x, y] != TileType.LockedDoor || owner.Length == 0) return;
            _locks[y * Map.Width + x] = owner;
            LockVersion++;
        }

        // A recall scroll: home after RecallSeconds, unless a hit breaks it off first.
        void StartRecall(Player player)
        {
            player.RecallTicks = Ticks(RecallSeconds);
            _events.Add(new SimEvent(SimEventType.Recall, player.Id, amount: RecallStarted, position: player.Position));
        }

        void UpdateRecall(Player player)
        {
            if (player.RecallTicks <= 0 || --player.RecallTicks > 0) return;
            SendHome(player);
            _events.Add(new SimEvent(SimEventType.Recall, player.Id, amount: RecallArrived, position: player.Position));
        }

        void BreakRecall(Player player)
        {
            if (player.RecallTicks <= 0) return;
            player.RecallTicks = 0;
            _events.Add(new SimEvent(SimEventType.Recall, player.Id, amount: RecallBroken, position: player.Position));
        }
    }
}
