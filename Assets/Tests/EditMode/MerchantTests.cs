using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static Game.Core.Tests.Builds;

namespace Game.Core.Tests
{
    // The merchant: coins, buying and selling, talking and gifts, friendship (Docs/Npcs.md).
    public class MerchantTests
    {
        static readonly MaterialDefinition Coin = Gear.Data.FindMaterial(World.Coin);
        static readonly ConsumableDefinition Potion = Gear.Data.Consumables.Single(c => c.Id == "healing_potion");
        static readonly MaterialDefinition Minnow = Gear.Data.FindMaterial("cave_minnow");
        static readonly MaterialDefinition StarMelon = Gear.Data.FindMaterial("star_melon");
        static readonly MaterialDefinition Stone = Gear.Data.FindMaterial("stone");
        static NpcDefinition Merchant => People.Shipped.Merchant;
        static ShopEntry Row(string id) => People.Shipped.Shop.Single(e => e.Npc == Merchant && (e.Material?.Id ?? e.Consumable.Id) == id);

        // A room with the merchant's stall put down near the spawn, and a player in front of it.
        static World Shop(out Player player, out (int X, int Y) stall)
        {
            var world = new World(Maps.Room(16), TileMap.Center(8, 8), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            world.SetNpcs(People.Shipped);
            player = world.AddPlayer("a", null);
            var near = Enumerable.Range(0, 16).SelectMany(y => Enumerable.Range(0, 16).Select(x => (x, y))).Single(t => world.Map[t.x, t.y] == TileType.MerchantStall);
            stall = near;
            Walk(world, player, TileMap.Center(stall.X, stall.Y - 1), new Vec2(0, 1));
            return world;
        }

        // Moving in (World.Housing.cs, Docs/Npcs.md 이사): a guest room walled round with x 20..26, y 3..8, its
        // inside 5 by 4, with a door in the bottom wall, a bed and a banner against the walls inside.
        static World WithGuestRoom(out Player player, bool door = true, bool decor = true, bool closed = true)
        {
            var world = new World(Maps.Room(30), TileMap.Center(10, 15), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            world.SetNpcs(People.Shipped);
            for (int x = 20; x <= 26; x++)
            {
                world.Map[x, 3] = TileType.Wall;
                world.Map[x, 8] = TileType.Wall;
            }
            for (int y = 3; y <= 8; y++)
            {
                world.Map[20, y] = TileType.Wall;
                world.Map[26, y] = closed || y != 6 ? TileType.Wall : TileType.Floor;
            }
            if (door) world.Map[23, 3] = TileType.Door;
            world.Map[21, 7] = TileType.Bed;
            if (decor) world.Map[25, 4] = TileType.Banner;
            player = world.AddPlayer("a", null);
            var stall = Enumerable.Range(0, 30).SelectMany(y => Enumerable.Range(0, 30).Select(x => (x, y))).Single(t => world.Map[t.x, t.y] == TileType.MerchantStall);
            Walk(world, player, TileMap.Center(stall.x, stall.y - 1), new Vec2(0, 1));
            return world;
        }

        static (int X, int Y) StallOf(World world, TileType tile) =>
            Enumerable.Range(0, world.Map.Height).SelectMany(y => Enumerable.Range(0, world.Map.Width).Select(x => (x, y))).Single(t => world.Map[t.x, t.y] == tile);

        [Test]
        public void APersonWhoLikesThePlayerMovesIntoAFitRoomAndWarmsFaster()
        {
            var world = WithGuestRoom(out var player);
            var old = StallOf(world, TileType.MerchantStall);
            Assert.AreEqual($"호감 {Housing.InviteHearts}부터 이사를 권할 수 있다", world.Invite(player.Id));
            World.FriendshipOf(player, Merchant).Points = Housing.InviteHearts * World.PointsPerHeart;
            Assert.IsFalse(world.LivesHere(Merchant));

            Assert.IsNull(world.Invite(player.Id));
            var now = StallOf(world, TileType.MerchantStall);
            Assert.AreEqual(TileType.Floor, world.Map[old.X, old.Y], "the old place is floor again");
            Assert.IsTrue(now.X > 20 && now.X < 26 && now.Y > 3 && now.Y < 8, $"in the room: {now}");
            Assert.AreEqual(7, now.Y, "against the top wall");
            Assert.IsTrue(world.LivesHere(Merchant));
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.MovedIn && e.Source == player.Id));
            Assert.AreEqual("곁에 아무도 없다", world.Invite(player.Id), "she has gone from beside the player");

            // Talking in her new room (in through the door): half as much again.
            player.Position = TileMap.Center(now.X, now.Y - 1);
            Assert.AreEqual("이미 방에 산다", world.Invite(player.Id));
            int before = World.FriendshipOf(player, Merchant).Points;
            Assert.IsTrue(world.Talk(player.Id));
            Assert.AreEqual(before + 15, World.FriendshipOf(player, Merchant).Points);

            // The room is one person's: the smith finds none free.
            player.Position = TileMap.Center(StallOf(world, TileType.SmithStall).X, StallOf(world, TileType.SmithStall).Y - 1);
            World.FriendshipOf(player, People.Shipped.Npcs.Single(n => n.Id == "blacksmith")).Points = 5 * World.PointsPerHeart;
            Assert.AreEqual("빈 방이 없다(한 방에 한 사람)", world.Invite(player.Id));

            // Taking the door out: she stays, but the room is no longer a home.
            world.Map[23, 3] = TileType.Wall;
            Assert.IsFalse(world.LivesHere(Merchant));
        }

        [Test]
        public void ARoomNeedsADoorSomethingNiceAndWalls()
        {
            foreach (var (world, player, fault) in new[]
            {
                (WithGuestRoom(out var a, door: false), a, "방에 문이 없다"),
                (WithGuestRoom(out var b, decor: false), b, "방에 장식(탁자, 깃발, 화분, 등불, 횃불)이 없다"),
                (WithGuestRoom(out var c, closed: false), c, "방이 닫혀 있지 않다(벽과 문으로 둘러야 한다)"),
            })
            {
                World.FriendshipOf(player, Merchant).Points = 5 * World.PointsPerHeart;
                Assert.AreEqual(fault, world.Invite(player.Id));
            }
            var room = Housing.RoomAt(Maps.Room(6), 2, 2, new[] { TileType.MerchantStall });
            Assert.IsTrue(room.Closed);
            Assert.AreEqual(16, room.Floor.Count);
            Assert.AreEqual("방에 침대가 없다", room.Fault);
        }

        [Test]
        public void TheSheetsReadCleanly()
        {
            Assert.IsTrue(People.Shipped.Success, string.Join("\n", People.Shipped.Errors));
            Assert.IsNotNull(Merchant);
            Assert.IsTrue(People.Shipped.Shop.All(e => e.Buy == 0 || e.Sell < e.Buy), "nothing sells back for more than it costs");
            Assert.IsNotNull(Coin);
        }

        [Test]
        public void ASellRowCannotPayMoreThanTheBuyPrice()
        {
            var data = NpcSheet.Parse("", "item,buy,sell\nstone,5,5\n", Gear.Data);
            Assert.IsTrue(data.Errors.Any(e => e.Contains(NpcSheet.ShopFile) && e.Contains("sell")), string.Join("\n", data.Errors));
        }

        [Test]
        public void TheStallStandsNearTheSpawnAndCannotBeDug()
        {
            var world = Shop(out var player, out var stall);
            Assert.LessOrEqual(Vec2.Distance(TileMap.Center(stall.X, stall.Y), world.Spawn), 12 * 1.5f);
            Assert.AreSame(Merchant, world.NpcNear(player.Position)?.Npc);
            Assert.IsNull(world.Tiles.Find(TileType.MerchantStall), "no tile row, so no pick takes it");
            // Put down only once.
            world.PlaceNpcs();
            Assert.AreEqual(1, Enumerable.Range(0, 16).Sum(y => Enumerable.Range(0, 16).Count(x => world.Map[x, y] == TileType.MerchantStall)));
        }

        [Test]
        public void APersonTakesOnlySoManyOfAThingADay()
        {
            var world = Shop(out var player, out _);
            var ore = Gear.Data.FindMaterial("iron_ore");
            player.Inventory.AddMaterial(ore, World.DailySellLimit + 5);
            int coins = player.Inventory.CountOf(Coin);
            Assert.IsTrue(world.Sell(player.Id, ore, World.MaxTrade));
            Assert.AreEqual(5, player.Inventory.CountOf(ore), "the rest stays in the bag");
            Assert.AreEqual(coins + World.DailySellLimit * Row("iron_ore").Sell, player.Inventory.CountOf(Coin));
            Assert.AreEqual(0, world.SellLeft(Row("iron_ore")));
            Assert.IsFalse(world.Sell(player.Id, ore, 1), "enough for today");
            player.Inventory.AddMaterial(Minnow, 1);
            Assert.IsTrue(world.Sell(player.Id, Minnow, 1), "another thing is still wanted");

            world.RestoreClock(world.Clock + Calendar.TicksPerDay);
            Assert.AreEqual(World.DailySellLimit, world.SellLeft(Row("iron_ore")), "a new day");
            Assert.IsTrue(world.Sell(player.Id, ore, 5));

            var message = FriendshipsMessage.Decode(new FriendshipsMessage(new[] { (0, 10, 1, 1, 0, 0) }, new[] { (3, 7) }).Encode());
            CollectionAssert.AreEqual(new[] { (3, 7) }, message.Sold);
        }

        [Test]
        public void BuyingCostsCoinsAndSellingPaysThem()
        {
            var world = Shop(out var player, out _);
            player.Inventory.AddMaterial(Coin, 30);
            Assert.IsTrue(world.Buy(player.Id, Row("healing_potion"), 1));
            Assert.AreEqual(10, player.Inventory.CountOf(Coin));
            Assert.AreEqual(1, player.Inventory.CountOf(Potion));
            Assert.IsFalse(world.Buy(player.Id, Row("healing_potion"), 1), "ten coins are not enough");

            player.Inventory.AddMaterial(Minnow, 3);
            Assert.IsTrue(world.Sell(player.Id, Minnow, 10), "as many as they have");
            Assert.AreEqual(10 + 3 * Row("cave_minnow").Sell, player.Inventory.CountOf(Coin));
            Assert.AreEqual(0, player.Inventory.CountOf(Minnow));
            Assert.IsFalse(world.Sell(player.Id, Stone, 1), "the merchant does not buy stone");
        }

        [Test]
        public void GearSellsByLevelRarityAndUpgrade()
        {
            var world = Shop(out var player, out _);
            var generator = new LootGenerator(Gear.Data);
            var item = generator.RollItem(20, new GameRandom(3), 0, Rarity.Rare);
            Assert.IsTrue(player.Inventory.TryAdd(item));
            int value = World.ItemValue(item, 0);
            Assert.AreEqual((4 + item.Level) * (item.Rarity == Rarity.Rare ? 3 : item.Rarity == Rarity.Epic ? 5 : 8) / 2 + item.Upgrade * 5, value);
            Assert.IsTrue(world.SellItem(player.Id, item.Id));
            Assert.AreEqual(value, player.Inventory.CountOf(Coin));
            Assert.IsFalse(player.Inventory.Items.Contains(item));
        }

        [Test]
        public void OnlyTodaysDailyRowsAreOnSale()
        {
            var today = World.ShopFor(People.Shipped, 4);
            Assert.AreEqual(World.DailyShopItems, today.Count(e => e.Daily));
            Assert.AreEqual(People.Shipped.Shop.Count(e => e.Npc == Merchant && e.Buy > 0 && !e.Daily), today.Count(e => !e.Daily));
            CollectionAssert.AreEqual(today, World.ShopFor(People.Shipped, 4), "the same each time");

            var world = Shop(out var player, out _);
            player.Inventory.AddMaterial(Coin, 1000);
            var missing = People.Shipped.Shop.First(e => e.Daily && e.Hearts == 0 && !World.ShopFor(People.Shipped, world.Today).Contains(e));
            Assert.IsFalse(world.Buy(player.Id, missing, 1), "not on sale today");
        }

        [Test]
        public void TheMerchantsStoryGoesStepByStepWithHearts()
        {
            var world = Shop(out var player, out _);
            var story = People.Shipped.StoryOf(Merchant);
            Assert.AreEqual(3, story.Count);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, story.Select(s => s.Step));
            Assert.IsFalse(People.Shipped.Requests.Where(r => r.IsStory).Any(r => World.RequestFor(People.Shipped, r.Npc, 5, 3) == r), "never the day's request");

            var first = World.StoryStepFor(People.Shipped, Merchant, 0);
            Assert.AreEqual("m_story1", first.Id);
            player.Inventory.AddMaterial(Gear.Data.FindMaterial("iron_bar"), 5);
            player.Inventory.AddMaterial(Stone, 30);
            Assert.IsFalse(world.CompleteStory(player.Id), "a heart first");

            World.FriendshipOf(player, Merchant).Points = World.PointsPerHeart;
            Assert.IsTrue(world.CompleteStory(player.Id));
            Assert.AreEqual(1, player.StorySteps["merchant"]);
            Assert.AreEqual(0, player.Inventory.CountOf(Stone));
            Assert.AreEqual(150, player.Inventory.CountOf(Gear.Data.FindMaterial("coin")));
            Assert.AreEqual(2, player.Inventory.CountOf(Gear.Data.Consumables.First(c => c.Id == "recall_scroll")));
            Assert.Greater(World.FriendshipOf(player, Merchant).Points, World.PointsPerHeart);
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.Npc && e.Amount == World.StoryDone));
            Assert.AreEqual("m_story2", World.StoryStepFor(People.Shipped, Merchant, 1).Id);
            Assert.IsFalse(world.CompleteStory(player.Id), "the next step needs three hearts and its things");

            // The last step gives gear: a rare void ring.
            player.StorySteps["merchant"] = 2;
            World.FriendshipOf(player, Merchant).Points = World.MaxHearts * World.PointsPerHeart;
            player.Inventory.AddMaterial(Gear.Data.FindMaterial("void_essence"), 1);
            player.Inventory.AddMaterial(Gear.Data.FindMaterial("crystal_eel"), 2);
            Assert.IsTrue(world.CompleteStory(player.Id));
            var ring = player.Inventory.Items.Single(i => i.Base.Id == "void_ring");
            Assert.AreEqual(Rarity.Rare, ring.Rarity);
            Assert.IsNull(World.StoryStepFor(People.Shipped, Merchant, 3), "the story is over");
            Assert.IsFalse(world.CompleteStory(player.Id));

            var saved = CharacterSave.Read(CharacterSave.Write(player.Equipment, player.Inventory, stories: player.StorySteps.Select(p => (p.Key, p.Value))), Gear.Data);
            CollectionAssert.Contains(saved.Stories, ("merchant", 3));
        }

        [Test]
        public void TalkingAndGiftsMakeFriendsOnceADay()
        {
            var world = Shop(out var player, out _);
            Assert.IsTrue(world.Talk(player.Id));
            Assert.IsFalse(world.Talk(player.Id), "once a day");
            player.Inventory.AddMaterial(StarMelon, 2);
            Assert.AreEqual(World.Loved, world.Gift(player.Id, StarMelon));
            Assert.AreEqual(-1, world.Gift(player.Id, StarMelon), "one gift a day");
            var friendship = World.FriendshipOf(player, Merchant);
            Assert.AreEqual(World.TalkPoints + 80, friendship.Points);
            Assert.AreEqual(1, player.Inventory.CountOf(StarMelon));
            var events = world.TakeEvents().Where(e => e.Type == SimEventType.Npc).Select(e => e.Amount).ToList();
            CollectionAssert.AreEqual(new[] { World.Talked, World.Loved }, events);

            // The next day: a disliked gift takes points away.
            world.RestoreClock(world.Clock + Calendar.TicksPerDay);
            player.Inventory.AddMaterial(Stone, 1);
            Assert.AreEqual(World.Disliked, world.Gift(player.Id, Stone));
            Assert.AreEqual(World.TalkPoints + 80 - 20, friendship.Points);
            Assert.IsTrue(world.Talk(player.Id));
        }

        [Test]
        public void HeartsMakePricesKinderAndOpenTheBackShelf()
        {
            var world = Shop(out var player, out _);
            player.Inventory.AddMaterial(Coin, 1000);
            var key = Row("rift_key");
            Assert.AreEqual(3, key.Hearts);
            Assert.IsFalse(world.Buy(player.Id, key, 1), "not friends enough yet");

            World.FriendshipOf(player, Merchant).Points = 5 * World.PointsPerHeart;
            Assert.AreEqual(16, World.BuyPrice(Row("healing_potion"), 5), "a fifth off");
            Assert.AreEqual(Row("cave_minnow").Sell * 120 / 100, World.SellPrice(Row("cave_minnow"), 5));
            Assert.IsTrue(world.Buy(player.Id, key, 1));
            Assert.AreEqual(1000 - World.BuyPrice(key, 5), player.Inventory.CountOf(Coin));
        }

        [Test]
        public void FarFromTheStallNothingHappens()
        {
            var world = Shop(out var player, out _);
            player.Inventory.AddMaterial(Coin, 100);
            Walk(world, player, TileMap.Center(12, 3), new Vec2(0, 1));
            Assert.IsNull(world.NpcNear(player.Position));
            Assert.IsFalse(world.Buy(player.Id, Row("healing_potion"), 1));
            Assert.IsFalse(world.Talk(player.Id));
        }

        [Test]
        public void FriendshipsAreKeptWithTheCharacter()
        {
            var data = CharacterSave.Write(new Equipment(), new Inventory(), 0, null, null, new[] { ("merchant", 230, 4, 5, 6, 4) });
            var save = CharacterSave.Read(data, Gear.Data);
            Assert.AreEqual(("merchant", 230, 4, 5, 6, 4), save.Friendships.Single());
        }

        [Test]
        public void PlayersHearTheirFriendshipsAndTradeThroughTheHost()
        {
            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped, npcs: People.Shipped);
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped) { Npcs = People.Shipped };
            host.Receive(1, client.JoinMessage());
            var player = host.World.Players.Single();
            var stall = host.World.ChangedTiles().Single(t => t.Type == TileType.MerchantStall);
            // In front of the stall (the host takes the player's word for where they are, so put them there).
            player.Position = TileMap.Center(stall.X, stall.Y - 1);
            player.Inventory.AddMaterial(Coin, 50);
            player.InventoryVersion++;
            void Pump()
            {
                for (int i = 0; i < 10; i++)
                {
                    host.Tick();
                    foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
                }
            }
            Pump();
            Assert.AreEqual(TileType.MerchantStall, client.Map[stall.X, stall.Y], "joiners see the stall");
            int potions = client.Bag.CountOf(Potion);

            host.Receive(1, client.TalkCommand());
            host.Receive(1, client.BuyCommand(Row("healing_potion"), 2));
            Pump();
            Assert.AreEqual(World.TalkPoints, client.PointsWith(Merchant));
            Assert.IsTrue(client.TalkedToday(Merchant));
            Assert.AreEqual(potions + 2, client.Bag.CountOf(Potion));
            Assert.AreEqual(10, client.Bag.CountOf(Coin));

            host.Receive(1, client.SellCommand(Potion, 1));
            host.Receive(1, client.GiftCommand(Potion));
            Pump();
            Assert.AreEqual(potions, client.Bag.CountOf(Potion), "one sold, one given");
            Assert.AreEqual(10 + Row("healing_potion").Sell, client.Bag.CountOf(Coin));
            Assert.IsTrue(client.GiftedToday(Merchant));
            var saved = CharacterSave.Read(client.SaveCharacter(), Gear.Data);
            Assert.AreEqual("merchant", saved.Friendships.Single().Npc);
        }
    }
}
