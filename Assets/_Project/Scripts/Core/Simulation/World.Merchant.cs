using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // How well a player knows someone: points (World.PointsPerHeart a heart) and the days they last talked and gave a
    // gift, once a day each. Kept with the character (CharacterSave).
    public sealed class Friendship
    {
        public int Points { get; internal set; }
        public int TalkedDay { get; internal set; }
        public int GiftedDay { get; internal set; }
        public int Hearts => World.HeartsOf(Points);
    }

    // The merchant and other people (Docs/Npcs.md): buying and selling for coins, talking and gifts once a day, and
    // friendship that makes the prices kinder and opens more of the shop.
    public sealed partial class World
    {
        public const string Coin = "coin";
        public const int MaxHearts = 5;
        public const int PointsPerHeart = 100;
        public const int TalkPoints = 10;
        // A gift's reaction (an Npc event's Amount) and what it does to friendship; World.Talked for talking.
        public const int Disliked = 0;
        public const int Neutral = 1;
        public const int Liked = 2;
        public const int Loved = 3;
        public const int Talked = 4;
        static readonly int[] GiftPoints = { -20, 20, 45, 80 };
        // Daily shop rows on sale each day, and how much kinder the prices get with each heart (percent).
        public const int DailyShopItems = 2;
        public const int HeartPercent = 4;
        // The most of one row bought or sold at once.
        public const int MaxTrade = 99;

        public NpcData Npcs { get; private set; }

        MaterialDefinition CoinMaterial => Loot?.Materials.FirstOrDefault(m => m.Id == Coin);

        public static int HeartsOf(int points) => Math.Clamp(points / PointsPerHeart, 0, MaxHearts);

        public static int BuyPrice(ShopEntry entry, int hearts) => (entry.Buy * (100 - HeartPercent * hearts) + 99) / 100;

        public static int SellPrice(ShopEntry entry, int hearts) => entry.Sell * (100 + HeartPercent * hearts) / 100;

        // What the merchant pays for a piece of gear: more for a higher level, a rarer one and an upgraded one.
        public static int ItemValue(Item item, int hearts)
        {
            int[] rarity = { 1, 2, 3, 5, 8 };
            int value = (4 + item.Level) * rarity[Math.Clamp((int)item.Rarity, 0, rarity.Length - 1)] / 2 + item.Upgrade * 5;
            return value * (100 + HeartPercent * hearts) / 100;
        }

        // What is on sale the day: every everyday row and DailyShopItems of the daily rows, picked by the day alone so
        // every client works out the same.
        public static IReadOnlyList<ShopEntry> ShopFor(NpcData npcs, int day)
        {
            if (npcs == null) return Array.Empty<ShopEntry>();
            var daily = npcs.Shop.Where(e => e.Daily && e.Buy > 0).ToList();
            var random = new GameRandom((ulong)Math.Max(1, day), stream: 11);
            var picked = new HashSet<ShopEntry>();
            while (picked.Count < Math.Min(DailyShopItems, daily.Count)) picked.Add(daily[random.Next(daily.Count)]);
            return npcs.Shop.Where(e => e.Buy > 0 && (!e.Daily || picked.Contains(e))).ToList();
        }

        public int Today => Calendar.Day(Clock);

        // Gives the world its people, each at their tile: one is put down near the spawn when the map has none.
        internal void SetNpcs(NpcData npcs)
        {
            Npcs = npcs;
            PlaceNpcs();
        }

        // After a world save is restored too: something may have been built where the stall stood.
        internal void PlaceNpcs()
        {
            if (Npcs == null) return;
            foreach (var npc in Npcs.Npcs)
            {
                if (HasTile(npc.Tile)) continue;
                var spot = NpcSpot();
                if (spot != null) SetTile(spot.Value.X, spot.Value.Y, npc.Tile);
            }
        }

        bool HasTile(TileType type)
        {
            for (int y = 0; y < Map.Height; y++)
            {
                for (int x = 0; x < Map.Width; x++)
                {
                    if (Map[x, y] == type) return true;
                }
            }
            return false;
        }

        // A floor tile a few steps from the spawn with floor in front (below) of it, never right beside the spawn.
        (int X, int Y)? NpcSpot()
        {
            int sx = TileMap.Cell(Spawn.X), sy = TileMap.Cell(Spawn.Y);
            for (int r = 3; r <= 12; r++)
            {
                for (int dy = r; dy >= -r; dy--)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int x = sx + dx, y = sy + dy;
                        if (!Map.InBounds(x, y - 1) || Map.IsEdge(x, y) || Map[x, y] != TileType.Floor || Map[x, y - 1] != TileType.Floor) continue;
                        return (x, y);
                    }
                }
            }
            return null;
        }

        // The person whose tile is within reach of the position, and where it is, or null.
        public (NpcDefinition Npc, int X, int Y)? NpcNear(Vec2 position)
        {
            if (Npcs == null) return null;
            foreach (var npc in Npcs.Npcs)
            {
                var at = NearestTile(position, npc.Tile, StationReach);
                if (at != null) return (npc, at.Value.X, at.Value.Y);
            }
            return null;
        }

        public static Friendship FriendshipOf(Player player, NpcDefinition npc)
        {
            if (!player.Friendships.TryGetValue(npc.Id, out var friendship))
            {
                friendship = new Friendship();
                player.Friendships[npc.Id] = friendship;
            }
            return friendship;
        }

        // Puts back what a character save remembered, for the people this world has.
        public static void RestoreFriendships(Player player, IEnumerable<(string Npc, int Points, int TalkedDay, int GiftedDay)> saved)
        {
            foreach (var (npc, points, talked, gifted) in saved)
            {
                player.Friendships[npc] = new Friendship
                {
                    Points = Math.Clamp(points, 0, MaxHearts * PointsPerHeart),
                    TalkedDay = talked,
                    GiftedDay = gifted,
                };
            }
        }

        // The merchant within reach of a living player, and how well they know them.
        (Player Player, NpcDefinition Npc, Friendship Friendship)? AtMerchant(int playerId)
        {
            var player = FindPlayer(playerId);
            var near = player == null || player.IsDead ? null : NpcNear(player.Position);
            if (near == null || near.Value.Npc.Tile != TileType.MerchantStall) return null;
            return (player, near.Value.Npc, FriendshipOf(player, near.Value.Npc));
        }

        // Buys count of a row on sale today, for coins. False when it is not on sale, not open to them yet, or they
        // cannot pay.
        public bool Buy(int playerId, ShopEntry entry, int count)
        {
            var at = AtMerchant(playerId);
            var coin = CoinMaterial;
            if (at == null || coin == null || entry == null || !ShopFor(Npcs, Today).Contains(entry)) return false;
            var (player, _, friendship) = at.Value;
            if (friendship.Hearts < entry.Hearts) return false;
            count = Math.Clamp(count, 1, MaxTrade);
            int price = BuyPrice(entry, friendship.Hearts) * count;
            if (player.Inventory.CountOf(coin) < price) return false;
            player.Inventory.TakeMaterial(coin, price);
            if (entry.Material != null) player.Inventory.AddMaterial(entry.Material, count);
            else player.Inventory.AddConsumable(entry.Consumable, count);
            player.InventoryVersion++;
            return true;
        }

        // Sells up to count of a material or consumable the merchant buys. False when they do not buy it or the player
        // has none.
        public bool Sell(int playerId, object thing, int count)
        {
            var at = AtMerchant(playerId);
            var coin = CoinMaterial;
            var entry = Npcs?.Shop.FirstOrDefault(e => e.Thing == thing && e.Sell > 0);
            if (at == null || coin == null || entry == null) return false;
            var (player, _, friendship) = at.Value;
            int have = entry.Material != null ? player.Inventory.CountOf(entry.Material) : player.Inventory.CountOf(entry.Consumable);
            count = Math.Min(Math.Clamp(count, 1, MaxTrade), have);
            if (count <= 0) return false;
            if (entry.Material != null) player.Inventory.TakeMaterial(entry.Material, count);
            else
            {
                for (int i = 0; i < count; i++) player.Inventory.TakeConsumable(entry.Consumable);
            }
            player.Inventory.AddMaterial(coin, SellPrice(entry, friendship.Hearts) * count);
            player.InventoryVersion++;
            return true;
        }

        // Sells a piece of gear from the bag (not one being worn).
        public bool SellItem(int playerId, int itemId)
        {
            var at = AtMerchant(playerId);
            var coin = CoinMaterial;
            if (at == null || coin == null) return false;
            var (player, _, friendship) = at.Value;
            var item = player.Inventory.Items.FirstOrDefault(i => i.Id == itemId);
            if (item == null) return false;
            player.Inventory.Remove(item);
            player.Inventory.AddMaterial(coin, ItemValue(item, friendship.Hearts));
            player.InventoryVersion++;
            return true;
        }

        // Talks to the person within reach, once a day. False when there is no one or they talked today.
        public bool Talk(int playerId)
        {
            var player = FindPlayer(playerId);
            var near = player == null || player.IsDead ? null : NpcNear(player.Position);
            if (near == null) return false;
            var friendship = FriendshipOf(player, near.Value.Npc);
            if (friendship.TalkedDay == Today) return false;
            friendship.TalkedDay = Today;
            friendship.Points = Math.Min(MaxHearts * PointsPerHeart, friendship.Points + TalkPoints);
            player.InventoryVersion++;
            _events.Add(new SimEvent(SimEventType.Npc, player.Id, target: IndexOf(near.Value.Npc), amount: Talked, position: TileMap.Center(near.Value.X, near.Value.Y)));
            return true;
        }

        // Gives one of a material or consumable to the person within reach, once a day. Returns how they took it
        // (World.Disliked to World.Loved), or -1 when it could not be given.
        public int Gift(int playerId, object thing)
        {
            var player = FindPlayer(playerId);
            var near = player == null || player.IsDead ? null : NpcNear(player.Position);
            if (near == null || thing == null) return -1;
            var npc = near.Value.Npc;
            var friendship = FriendshipOf(player, npc);
            if (friendship.GiftedDay == Today) return -1;
            switch (thing)
            {
                case MaterialDefinition material when player.Inventory.CountOf(material) > 0 && material.Id != Coin:
                    player.Inventory.TakeMaterial(material);
                    break;
                case ConsumableDefinition consumable when player.Inventory.CountOf(consumable) > 0:
                    player.Inventory.TakeConsumable(consumable);
                    break;
                default:
                    return -1;
            }
            int reaction = npc.Loves.Contains(thing) ? Loved : npc.Likes.Contains(thing) ? Liked : npc.Dislikes.Contains(thing) ? Disliked : Neutral;
            friendship.GiftedDay = Today;
            friendship.Points = Math.Clamp(friendship.Points + GiftPoints[reaction], 0, MaxHearts * PointsPerHeart);
            player.InventoryVersion++;
            _events.Add(new SimEvent(SimEventType.Npc, player.Id, target: IndexOf(npc), amount: reaction, position: TileMap.Center(near.Value.X, near.Value.Y)));
            return reaction;
        }

        int IndexOf(NpcDefinition npc) => Npcs == null ? -1 : Npcs.Npcs.ToList().IndexOf(npc);
    }
}
