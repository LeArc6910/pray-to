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
