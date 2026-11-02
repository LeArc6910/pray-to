using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // Fishing (Docs/Fishing.md): with a fishing rod in the bag, a player facing water casts. After a few seconds a fish
    // bites, and pressing again within BiteSeconds lands it: a fish of the biome the water is in. Too late, and it gets
    // away; walking off, being hit or pressing before the bite reels the line in.
    public sealed partial class World
    {
        public const string FishingRod = "fishing_rod";
        public const float BiteMinSeconds = 2;
        public const float BiteMaxSeconds = 5;
        public const float BiteSeconds = 1.2f;
        // How far the player may move from where they cast before the line comes in.
        const float FishingLeash = 0.6f;
        // The chance of a second fish of the same kind.
        const float DoubleCatchChance = 0.12f;

        // A Fishing event's Amount.
        public const int FishingCast = 0;
        public const int FishingBite = 1;
        public const int FishingCaught = 2;
        public const int FishingEscaped = 3;
        public const int FishingReeled = 4;

        // Casts, lands a biting fish, or reels in a line that has nothing on it yet. False when there is nothing to
        // do: no rod, or no water ahead to cast into.
        public bool Fish(int playerId)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead) return false;
            var fishing = player.Fishing;
            if (fishing != null)
            {
                if (fishing.BiteTicks > 0) Land(player);
                else EndFishing(player, FishingReeled);
                return true;
            }
            if (!player.Inventory.Materials.Keys.Any(m => m.Id == FishingRod)) return false;
            var (x, y) = TileAhead(player.Position, player.Facing);
            if (!Map.InBounds(x, y) || Map[x, y] != TileType.Water) return false;
            player.Fishing = new FishingLine(x, y, player.Position, Ticks(BiteMinSeconds) + _random.Next(Ticks(BiteMaxSeconds - BiteMinSeconds) + 1));
            _events.Add(new SimEvent(SimEventType.Fishing, player.Id, amount: FishingCast, position: TileMap.Center(x, y)));
            return true;
        }

        void UpdateFishing(Player player)
        {
            var fishing = player.Fishing;
            if (fishing == null) return;
            if (player.IsDead || Vec2.Distance(player.Position, fishing.Stand) > FishingLeash)
            {
                EndFishing(player, FishingReeled);
                return;
            }
            if (fishing.WaitTicks > 0)
            {
                if (--fishing.WaitTicks > 0) return;
                fishing.BiteTicks = Ticks(BiteSeconds);
                _events.Add(new SimEvent(SimEventType.Fishing, player.Id, amount: FishingBite, position: fishing.Spot));
                return;
            }
            if (--fishing.BiteTicks <= 0) EndFishing(player, FishingEscaped);
        }

        // A fish of the biome the water is in (materials.csv fish), night fish among them at night (Docs/Time.md); the
        // plain cave's where that biome has none.
        void Land(Player player)
        {
            var fishing = player.Fishing;
            var biome = Biomes[fishing.X, fishing.Y];
            bool night = Calendar.IsNight(Clock);
            List<MaterialDefinition> Catches(Biome water) => Loot?.Materials.Where(m => m.Fish == water && (!m.FishAtNight || night)).ToList();
            var catches = Catches(biome);
            if (catches == null || catches.Count == 0) catches = Catches(Biome.Cave);
            if (catches != null && catches.Count > 0)
            {
                var fish = catches[_random.Next(catches.Count)];
                player.Inventory.AddMaterial(fish, _random.Chance(DoubleCatchChance) ? 2 : 1);
                player.InventoryVersion++;
            }
            EndFishing(player, FishingCaught);
        }

        void EndFishing(Player player, int how)
        {
            var fishing = player.Fishing;
            if (fishing == null) return;
            player.Fishing = null;
            _events.Add(new SimEvent(SimEventType.Fishing, player.Id, amount: how, position: fishing.Spot));
        }
    }

    // A line in the water: where it went in, where the player stood, and the ticks until a bite and of the bite.
    public sealed class FishingLine
    {
        internal FishingLine(int x, int y, Vec2 stand, int waitTicks)
        {
            X = x;
            Y = y;
            Stand = stand;
            WaitTicks = waitTicks;
        }

        public int X { get; }
        public int Y { get; }
        public Vec2 Spot => TileMap.Center(X, Y);
        public Vec2 Stand { get; }
        internal int WaitTicks { get; set; }
        internal int BiteTicks { get; set; }
    }
}
