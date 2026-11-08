using System.Linq;
using NUnit.Framework;
using static Game.Core.Tests.Builds;

namespace Game.Core.Tests
{
    // The blacksmith and the fisher beside the merchant: shops of their own, requests and heart gifts (Docs/Npcs.md).
    public class PeopleTests
    {
        static NpcDefinition Person(string id) => People.Shipped.Find(id);
        static readonly MaterialDefinition Coin = Gear.Data.FindMaterial(World.Coin);
        static readonly MaterialDefinition Minnow = Gear.Data.FindMaterial("cave_minnow");

        // A room with everyone's stalls near the spawn, and the player in front of the given person's.
        static World Town(string id, out Player player)
        {
            var world = new World(Maps.Room(24), TileMap.Center(12, 12), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            world.SetNpcs(People.Shipped);
            player = world.AddPlayer("a", null);
            StandAt(world, player, id);
            return world;
        }

        static void StandAt(World world, Player player, string id)
        {
            var tile = Person(id).Tile;
            var at = Enumerable.Range(0, 24).SelectMany(y => Enumerable.Range(0, 24).Select(x => (x, y))).Single(t => world.Map[t.x, t.y] == tile);
            player.Position = TileMap.Center(at.x, at.y - 1);
            Assert.AreSame(Person(id), world.NpcNear(player.Position)?.Npc, $"in front of {id}");
        }

        [Test]
        public void FourPeopleEachWithAStallAndAShop()
        {
            Assert.IsTrue(People.Shipped.Success, string.Join("\n", People.Shipped.Errors));
            CollectionAssert.AreEqual(new[] { "merchant", "blacksmith", "fisher", "alchemist" }, People.Shipped.Npcs.Select(n => n.Id));
            Assert.AreEqual(3, People.Shipped.StoryOf(Person("alchemist")).Count, "the alchemist has a story too");
            var world = Town("blacksmith", out _);
            foreach (var npc in People.Shipped.Npcs)
            {
                Assert.AreEqual(1, Enumerable.Range(0, 24).Sum(y => Enumerable.Range(0, 24).Count(x => world.Map[x, y] == npc.Tile)), npc.Id);
                Assert.IsNotEmpty(World.ShopFor(People.Shipped, 3, npc), $"{npc.Id} sells something");
            }
            Assert.IsTrue(World.ShopFor(People.Shipped, 3, Person("blacksmith")).All(e => e.Npc == Person("blacksmith")));
        }

        [Test]
        public void EachSellsOnlyWhatIsTheirsAndOnlySomeBuyGear()
        {
            var world = Town("fisher", out var player);
            player.Inventory.AddMaterial(Coin, 500);
            var ironBar = People.Shipped.Shop.Single(e => e.Npc == Person("blacksmith") && e.Material?.Id == "iron_bar");
            Assert.IsFalse(world.Buy(player.Id, ironBar, 1), "the blacksmith's row, at the fisher's");
            var bait = People.Shipped.Shop.Single(e => e.Npc == Person("fisher") && e.Material?.Id == World.Bait);
            Assert.IsTrue(world.Buy(player.Id, bait, 2));

            var item = new LootGenerator(Gear.Data).RollItem(10, new GameRandom(1));
            Assert.IsTrue(player.Inventory.TryAdd(item));
            Assert.IsFalse(world.SellItem(player.Id, item.Id), "the fisher does not buy gear");
            StandAt(world, player, "blacksmith");
            Assert.IsTrue(world.SellItem(player.Id, item.Id));
        }

        [Test]
        public void TheFisherPaysMoreForFish()
        {
            int Price(string npc) => People.Shipped.Shop.Single(e => e.Npc == Person(npc) && e.Material == Minnow).Sell;
            Assert.Greater(Price("fisher"), Price("merchant"));
            var world = Town("fisher", out var player);
            player.Inventory.AddMaterial(Minnow, 2);
            int before = player.Inventory.CountOf(Coin);
            Assert.IsTrue(world.Sell(player.Id, Minnow, 2));
            Assert.AreEqual(before + 2 * Price("fisher"), player.Inventory.CountOf(Coin));
        }

        [Test]
        public void ARequestADayForWhatTheyAsk()
        {
            var world = Town("blacksmith", out var player);
            var request = World.RequestFor(People.Shipped, Person("blacksmith"), 0, world.Today);
            Assert.IsNotNull(request);
            Assert.AreEqual(0, request.Hearts, "only requests open at no hearts");
            Assert.IsFalse(world.CompleteRequest(player.Id), "nothing to bring yet");
            foreach (var (thing, count) in request.Need) player.Inventory.AddMaterial((MaterialDefinition)thing, count);
            int coins = player.Inventory.CountOf(Coin);
            Assert.IsTrue(world.CompleteRequest(player.Id));
            Assert.IsTrue(request.Need.All(n => player.Inventory.CountOf((MaterialDefinition)n.Thing) == 0));
            int promised = request.Rewards.Where(r => r.Thing == Coin).Sum(r => r.Count);
            Assert.AreEqual(coins + promised, player.Inventory.CountOf(Coin));
            Assert.AreEqual(request.Points, World.FriendshipOf(player, Person("blacksmith")).Points);
            Assert.AreEqual(World.RequestDone, world.TakeEvents().Single(e => e.Type == SimEventType.Npc).Amount);

            foreach (var (thing, count) in request.Need) player.Inventory.AddMaterial((MaterialDefinition)thing, count);
            Assert.IsFalse(world.CompleteRequest(player.Id), "once a day");
            Assert.AreNotSame(request, World.RequestFor(People.Shipped, Person("blacksmith"), 0, world.Today + 1), "another the next day");
        }

        [Test]
        public void HeartsBringAGiftAndAStoryOnce()
        {
            var world = Town("fisher", out var player);
            var fisher = Person("fisher");
            World.FriendshipOf(player, fisher).Points = 2 * World.PointsPerHeart;
            var bait = Gear.Data.FindMaterial(World.Bait);
            Assert.IsTrue(world.Talk(player.Id));
            Assert.AreEqual(20, player.Inventory.CountOf(bait), "the two-heart gift");
            var events = world.TakeEvents().Where(e => e.Type == SimEventType.Npc).Select(e => e.Amount).ToList();
            CollectionAssert.AreEqual(new[] { World.Talked, World.HeartGift + 2 }, events);
            Assert.IsTrue(fisher.Stories.ContainsKey(2));

            world.RestoreClock(world.Clock + Calendar.TicksPerDay);
            Assert.IsTrue(world.Talk(player.Id));
            Assert.AreEqual(20, player.Inventory.CountOf(bait), "only once");
        }

        [Test]
        public void RequestsAndGiftsAreKeptWithTheCharacter()
        {
            var data = CharacterSave.Write(new Equipment(), new Inventory(), 0, null, null, new[] { ("fisher", 210, 1, 2, 3, 4) });
            var saved = CharacterSave.Read(data, Gear.Data).Friendships.Single();
            Assert.AreEqual(3, saved.RequestDay);
            Assert.AreEqual(4, saved.HeartGifts);
        }
    }
}
