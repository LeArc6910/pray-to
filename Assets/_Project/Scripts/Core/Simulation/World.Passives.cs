using System.Linq;

namespace Game.Core
{
    // The passive tree (Docs/Classes.md, 패시브 트리): a point for every level past the first, spent on points of the
    // tree next to ones already taken, starting from its start. Taking them all back costs coins (2026-11-09).
    public sealed partial class World
    {
        // Takes the point for the player. False when it cannot: no point to spend, already taken, the start, or not
        // next to the start or a point they have.
        public bool AllocatePassive(int playerId, PassiveNode node)
        {
            var player = FindPlayer(playerId);
            if (player == null || node == null || node.IsStart || player.PassivePoints <= 0 || player.Passives.Contains(node)) return false;
            if (!node.Links.Any(n => n.IsStart || player.Passives.Contains(n))) return false;
            player.Passives.Add(node);
            PassivesChanged(player);
            return true;
        }

        // Gives every point back for ClassRules.PassiveResetCoinsPerPoint coins a point. False when there were none or
        // the coins are short.
        public bool ResetPassives(int playerId)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.Passives.Count == 0) return false;
            var coin = CoinMaterial;
            int cost = player.Passives.Count * ClassRules.PassiveResetCoinsPerPoint;
            if (coin != null)
            {
                if (player.Inventory.CountOf(coin) < cost) return false;
                player.Inventory.TakeMaterial(coin, cost);
            }
            player.InventoryVersion++;
            player.Passives.Clear();
            PassivesChanged(player);
            return true;
        }

        // Puts back the points a saved character had, as far as they still join up with the start and the levels
        // allow, earlier ones in the save first; any others are dropped (their points come back to spend).
        public static void RestorePassives(Player player, ClassData classes, System.Collections.Generic.IEnumerable<string> ids)
        {
            player.Passives.Clear();
            var wanted = ids.Select(id => classes?.FindPassive(id)).Where(n => n != null && !n.IsStart).Distinct().ToList();
            while (player.PassivePoints > 0)
            {
                var next = wanted.FirstOrDefault(node => node.Links.Any(n => n.IsStart || player.Passives.Contains(n)));
                if (next == null) break;
                player.Passives.Add(next);
                wanted.Remove(next);
            }
            player.RefreshStats();
        }

        void PassivesChanged(Player player)
        {
            player.RefreshStats();
            // The bag message carries the points, and the character is saved when it changes.
            player.InventoryVersion++;
        }
    }
}
