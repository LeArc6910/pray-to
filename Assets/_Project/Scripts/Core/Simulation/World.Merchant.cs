using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // How well a player knows someone: points (World.PointsPerHeart a heart), the days they last talked, gave a gift
    // and did the day's request, once a day each, and which heart gifts they have had (a bit per heart). Kept with
    // the character (CharacterSave).
    public sealed class Friendship
    {
        public int Points { get; internal set; }
        public int TalkedDay { get; internal set; }
        public int GiftedDay { get; internal set; }
        public int RequestDay { get; internal set; }
        public int HeartGifts { get; internal set; }
        public int Hearts => World.HeartsOf(Points);

        public (int Points, int TalkedDay, int GiftedDay, int RequestDay, int HeartGifts) Saved => (Points, TalkedDay, GiftedDay, RequestDay, HeartGifts);
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
        // A request done; a story step done; and HeartGift + hearts for a heart gift given on talking.
        public const int RequestDone = 5;
        public const int StoryDone = 6;
        public const int HeartGift = 16;
        static readonly int[] GiftPoints = { -20, 20, 45, 80 };
        // Daily shop rows on sale each day, and how much kinder the prices get with each heart (percent).
        public const int DailyShopItems = 2;
        public const int HeartPercent = 4;
        // The most of one row bought or sold at once.
        public const int MaxTrade = 99;
        // How many of one thing a person buys from everyone in a day; past that they have had enough until tomorrow,
        // so machines that never stop do not make coins without end (Docs/Npcs.md, 하루에 사는 양).
        public const int DailySellLimit = 20;

        readonly Dictionary<ShopEntry, int> _soldToday = new Dictionary<ShopEntry, int>();
        int _soldDay;

        // How many of the row's thing its person has bought today, from anyone.
        public int SoldToday(ShopEntry entry)
        {
            if (_soldDay != Today)
            {
                _soldDay = Today;
                _soldToday.Clear();
            }
            return entry == null ? 0 : _soldToday.GetValueOrDefault(entry);
        }

        public int SellLeft(ShopEntry entry) => Math.Max(0, DailySellLimit - SoldToday(entry));

        public NpcData Npcs { get; private set; }

        MaterialDefinition CoinMaterial => Loot?.Materials.FirstOrDefault(m => m.Id == Coin);

        public static int HeartsOf(int points) => Math.Clamp(points / PointsPerHeart, 0, MaxHearts);

        public static int BuyPrice(ShopEntry entry, int hearts, int day = 0)
        {
            int price = (entry.Buy * (100 - HeartPercent * hearts) + 99) / 100;
            // Market day: cheaper still.
            return EventOf(day) == DayEvent.Market ? Math.Max(1, (price * MarketPercent + 99) / 100) : price;
        }

        // What a person pays for one: the row's price, more for silver or gold (MaterialDefinition.Quality), more with
        // each heart, and for a crop more on the day of the harvest festival.
        public static int SellPrice(ShopEntry entry, int hearts, int quality = 0, int day = 0)
        {
            int price = entry.Sell * MaterialDefinition.QualityPercent(quality) / 100 * (100 + HeartPercent * hearts) / 100;
            return EventOf(day) == DayEvent.Harvest && entry.Material?.HasQuality == true ? price * HarvestPercent / 100 : price;
        }

        // What the merchant pays for a piece of gear: more for a higher level, a rarer one and an upgraded one.
        public static int ItemValue(Item item, int hearts)
        {
            int[] rarity = { 1, 2, 3, 5, 8 };
            int value = (4 + item.Level) * rarity[Math.Clamp((int)item.Rarity, 0, rarity.Length - 1)] / 2 + item.Upgrade * 5;
            return value * (100 + HeartPercent * hearts) / 100;
        }

        // What a person has on sale the day: every everyday row of theirs and DailyShopItems of their daily rows,
        // picked by the day alone so every client works out the same. Without a person, the merchant.
        public static IReadOnlyList<ShopEntry> ShopFor(NpcData npcs, int day, NpcDefinition npc = null)
        {
            if (npcs == null) return Array.Empty<ShopEntry>();
            npc ??= npcs.Merchant;
            var own = npcs.Shop.Where(e => e.Npc == npc).ToList();
            var daily = own.Where(e => e.Daily && e.Buy > 0).ToList();
            var random = new GameRandom((ulong)Math.Max(1, day) * 31 + (ulong)Math.Max(0, npcs.IndexOf(npc)), stream: 11);
            var picked = new HashSet<ShopEntry>();
            while (picked.Count < Math.Min(DailyShopItems, daily.Count)) picked.Add(daily[random.Next(daily.Count)]);
            return own.Where(e => e.Buy > 0 && (!e.Daily || picked.Contains(e))).ToList();
        }

        // What a person asks of a player the day: one of their requests that the player is friendly enough for, a
        // different one each day; null when they have none.
        public static RequestDefinition RequestFor(NpcData npcs, NpcDefinition npc, int hearts, int day)
        {
            var open = npcs?.Requests.Where(r => r.Npc == npc && !r.IsStory && r.Hearts <= hearts).ToList();
            if (open == null || open.Count == 0) return null;
            return open[(Math.Max(0, day) * 7 + npcs.IndexOf(npc) * 3) % open.Count];
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
                        // Apart from the other people's places, so each has their own.
                        if (Npcs.Npcs.Any(n => NearestTile(TileMap.Center(x, y), n.Tile, 4) != null)) continue;
                        return (x, y);
                    }
                }
            }
            return null;
        }

        // The person whose tile is nearest the position within reach, and where it is, or null.
        public (NpcDefinition Npc, int X, int Y)? NpcNear(Vec2 position)
        {
            if (Npcs == null) return null;
            (NpcDefinition, int, int)? best = null;
            float bestDistance = float.MaxValue;
            foreach (var npc in Npcs.Npcs)
            {
                var at = NearestTile(position, npc.Tile, StationReach);
                if (at == null) continue;
                float distance = Vec2.Distance(position, TileMap.Center(at.Value.X, at.Value.Y));
                if (distance >= bestDistance) continue;
                best = (npc, at.Value.X, at.Value.Y);
                bestDistance = distance;
            }
            return best;
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
        public static void RestoreFriendships(Player player,
            IEnumerable<(string Npc, int Points, int TalkedDay, int GiftedDay, int RequestDay, int HeartGifts)> saved)
        {
            foreach (var (npc, points, talked, gifted, request, heartGifts) in saved)
            {
                player.Friendships[npc] = new Friendship
                {
                    Points = Math.Clamp(points, 0, MaxHearts * PointsPerHeart),
                    TalkedDay = talked,
                    GiftedDay = gifted,
                    RequestDay = request,
                    HeartGifts = heartGifts,
                };
            }
        }

        // The person within reach of a living player, and how well they know them.
        (Player Player, NpcDefinition Npc, Friendship Friendship)? AtMerchant(int playerId)
        {
            var player = FindPlayer(playerId);
            var near = player == null || player.IsDead ? null : NpcNear(player.Position);
            if (near == null) return null;
            return (player, near.Value.Npc, FriendshipOf(player, near.Value.Npc));
        }

        // Gives the material or consumable to the player.
        void Give(Player player, object thing, int count, Vec2 at = default)
        {
            if (thing is MaterialDefinition material) player.Inventory.AddMaterial(material, count);
            else if (thing is ItemBase itemBase)
            {
                // Gear from a story: rare at its level, into the bag, or at the person's feet when it is full.
                for (int i = 0; i < count && _lootGenerator != null; i++)
                {
                    var item = Register(_lootGenerator.RollItem(itemBase, itemBase.Level, Rarity.Rare, _random));
                    if (!player.Inventory.TryAdd(item)) DropItem(item, at);
                }
            }
            else player.Inventory.AddConsumable((ConsumableDefinition)thing, count);
        }

        // The next step of the person's story for this player (how many they have done, plus one), or null when the
        // story is over or they have none.
        public static RequestDefinition StoryStepFor(NpcData npcs, NpcDefinition npc, int done) =>
            npcs?.StoryOf(npc).FirstOrDefault(r => r.Step > done);

        // Brings the person in reach what the next step of their story asks: takes it, gives the rewards (gear
        // among them) and friendship, and moves the story on. False when the step is not open yet (too few hearts) or
        // the bag lacks something.
        public bool CompleteStory(int playerId)
        {
            var at = AtMerchant(playerId);
            if (at == null) return false;
            var (player, npc, friendship) = at.Value;
            player.StorySteps.TryGetValue(npc.Id, out int done);
            var step = StoryStepFor(Npcs, npc, done);
            if (step == null || friendship.Hearts < step.Hearts || step.Need.Any(n => CountOf(player, n.Thing) < n.Count)) return false;
            foreach (var (thing, count) in step.Need)
            {
                if (thing is MaterialDefinition material) player.Inventory.TakeKind(material, count);
                else for (int i = 0; i < count; i++) player.Inventory.TakeConsumable((ConsumableDefinition)thing);
            }
            var tile = NearestTile(player.Position, npc.Tile, StationReach) ?? (0, 0);
            var place = TileMap.Center(tile.X, tile.Y);
            foreach (var (thing, count) in step.Rewards) Give(player, thing, count, player.Position);
            player.StorySteps[npc.Id] = step.Step;
            friendship.Points = Math.Min(MaxHearts * PointsPerHeart, friendship.Points + step.Points);
            player.InventoryVersion++;
            _events.Add(new SimEvent(SimEventType.Npc, player.Id, amount: StoryDone, position: place));
            return true;
        }

        // Puts back the story steps a character save held (Docs/Npcs.md, 이야기).
        public static void RestoreStories(Player player, IEnumerable<(string Npc, int Step)> saved)
        {
            player.StorySteps.Clear();
            foreach (var (npc, step) in saved ?? Enumerable.Empty<(string, int)>()) if (step > 0) player.StorySteps[npc] = step;
        }

        static int CountOf(Player player, object thing) =>
            thing is MaterialDefinition material ? player.Inventory.CountOfKind(material) : player.Inventory.CountOf((ConsumableDefinition)thing);

        // Brings the person within reach what they asked for today: takes it, gives what they promised and friendship.
        // False when there is no request, it was done today, or the player has not got everything.
        public bool CompleteRequest(int playerId)
        {
            var at = AtMerchant(playerId);
            if (at == null) return false;
            var (player, npc, friendship) = at.Value;
            var request = RequestFor(Npcs, npc, friendship.Hearts, Today);
            if (request == null || friendship.RequestDay == Today || request.Need.Any(n => CountOf(player, n.Thing) < n.Count)) return false;
            foreach (var (thing, count) in request.Need)
            {
                if (thing is MaterialDefinition material) player.Inventory.TakeKind(material, count);
                else for (int i = 0; i < count; i++) player.Inventory.TakeConsumable((ConsumableDefinition)thing);
            }
            foreach (var (thing, count) in request.Rewards) Give(player, thing, count);
            friendship.RequestDay = Today;
            friendship.Points = Math.Min(MaxHearts * PointsPerHeart, friendship.Points + request.Points);
            player.InventoryVersion++;
            var tile = NearestTile(player.Position, npc.Tile, StationReach) ?? (0, 0);
            _events.Add(new SimEvent(SimEventType.Npc, player.Id, amount: RequestDone, position: TileMap.Center(tile.X, tile.Y)));
            return true;
        }

        // On talking: the first heart gift the player has reached and not had yet.
        void GiveHeartGift(Player player, NpcDefinition npc, Friendship friendship, Vec2 at)
        {
            foreach (var (hearts, thing, count) in npc.Gifts.OrderBy(g => g.Hearts))
            {
                if (hearts > friendship.Hearts || (friendship.HeartGifts & (1 << hearts)) != 0) continue;
                friendship.HeartGifts |= 1 << hearts;
                Give(player, thing, count);
                _events.Add(new SimEvent(SimEventType.Npc, player.Id, amount: HeartGift + hearts, position: at));
                return;
            }
        }

        // Buys count of a row on sale today, for coins. False when it is not on sale, not open to them yet, or they
        // cannot pay.
        public bool Buy(int playerId, ShopEntry entry, int count)
        {
            var at = AtMerchant(playerId);
            var coin = CoinMaterial;
            if (at == null || coin == null || entry == null || entry.Npc != at.Value.Npc || !ShopFor(Npcs, Today, at.Value.Npc).Contains(entry)) return false;
            var (player, _, friendship) = at.Value;
            if (friendship.Hearts < entry.Hearts) return false;
            count = Math.Clamp(count, 1, MaxTrade);
            int price = BuyPrice(entry, friendship.Hearts, Today) * count;
            if (player.Inventory.CountOf(coin) < price) return false;
            player.Inventory.TakeMaterial(coin, price);
            if (entry.Material != null) player.Inventory.AddMaterial(entry.Material, count);
            else player.Inventory.AddConsumable(entry.Consumable, count);
            player.InventoryVersion++;
            return true;
        }

        // Sells up to count of a material or consumable the merchant buys, as many as they still take today. False when
        // they do not buy it, have had enough of it today, or the player has none.
        public bool Sell(int playerId, object thing, int count)
        {
            var at = AtMerchant(playerId);
            var coin = CoinMaterial;
            var kind = thing is MaterialDefinition m ? m.Kind : thing;
            var entry = at == null ? null : Npcs?.Shop.FirstOrDefault(e => e.Thing == kind && e.Sell > 0 && e.Npc == at.Value.Npc);
            if (at == null || coin == null || entry == null) return false;
            var (player, _, friendship) = at.Value;
            // A silver or gold one sells as itself, for more.
            var sold = thing as MaterialDefinition ?? entry.Material;
            int have = entry.Material != null ? player.Inventory.CountOf(sold) : player.Inventory.CountOf(entry.Consumable);
            count = Math.Min(Math.Min(Math.Clamp(count, 1, MaxTrade), have), SellLeft(entry));
            if (count <= 0) return false;
            _soldToday[entry] = SoldToday(entry) + count;
            if (entry.Material != null) player.Inventory.TakeMaterial(sold, count);
            else
            {
                for (int i = 0; i < count; i++) player.Inventory.TakeConsumable(entry.Consumable);
            }
            player.Inventory.AddMaterial(coin, SellPrice(entry, friendship.Hearts, sold?.Quality ?? 0, Today) * count);
            player.InventoryVersion++;
            return true;
        }

        // Sells a piece of gear from the bag (not one being worn).
        public bool SellItem(int playerId, int itemId)
        {
            var at = AtMerchant(playerId);
            var coin = CoinMaterial;
            if (at == null || coin == null || !at.Value.Npc.BuysGear) return false;
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
            friendship.Points = Math.Min(MaxHearts * PointsPerHeart, friendship.Points + Warmed(near.Value.Npc, TalkPoints));
            player.InventoryVersion++;
            _events.Add(new SimEvent(SimEventType.Npc, player.Id, amount: Talked, position: TileMap.Center(near.Value.X, near.Value.Y)));
            GiveHeartGift(player, near.Value.Npc, friendship, TileMap.Center(near.Value.X, near.Value.Y));
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
            // A silver or gold one pleases as its plain kind does, and a little more.
            var kind = thing is MaterialDefinition plain ? plain.Kind : thing;
            int quality = (thing as MaterialDefinition)?.Quality ?? 0;
            int reaction = npc.Loves.Contains(kind) ? Loved : npc.Likes.Contains(kind) ? Liked : npc.Dislikes.Contains(kind) ? Disliked : Neutral;
            friendship.GiftedDay = Today;
            friendship.Points = Math.Clamp(friendship.Points + Warmed(npc, GiftPoints[reaction] + (reaction > Disliked ? 10 * quality : 0)), 0, MaxHearts * PointsPerHeart);
            player.InventoryVersion++;
            _events.Add(new SimEvent(SimEventType.Npc, player.Id, amount: reaction, position: TileMap.Center(near.Value.X, near.Value.Y)));
            return reaction;
        }

    }
}
