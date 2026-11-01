using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class HomeTests
    {
        static readonly MaterialDefinition Bed = Gear.Data.FindMaterial("bed");
        static readonly ConsumableDefinition Scroll = Gear.Data.FindConsumable("recall_scroll");

        static World Room(out Player player, string name = "a")
        {
            var world = new World(Maps.Room(30), TileMap.Center(15, 15), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0, RespawnSeconds = 1 },
                Gear.Data, Tiles.Shipped);
            player = world.AddPlayer(name, null);
            return world;
        }

        static void Run(World world, float seconds)
        {
            for (int i = 0; i < World.Ticks(seconds); i++) world.Step();
        }

        // Walks the player to (x, y), facing the given way.
        static void Stand(World world, Player player, int x, int y, Vec2 facing)
        {
            var target = TileMap.Center(x, y);
            for (int i = 0; i < 30 && Vec2.Distance(player.Position, target) > 0.01f; i++)
            {
                world.MovePlayer(player.Id, player.Position + (target - player.Position).ClampLength(1.5f), facing, 10);
            }
            world.MovePlayer(player.Id, player.Position, facing, 1);
        }

        static void PlaceBed(World world, Player player, int x, int y)
        {
            Stand(world, player, x, y + 1, new Vec2(0, -1));
            player.Inventory.AddMaterial(Bed);
            Assert.IsTrue(world.PlaceTile(player.Id, Bed));
            Assert.AreEqual(TileType.Bed, world.Map[x, y]);
        }

        [Test]
        public void APlayerGetsUpBesideTheirBedAfterFalling()
        {
            var world = Room(out var player);
            PlaceBed(world, player, 5, 5);
            Assert.AreEqual((5, 5), world.BedOf(player));
            var home = world.HomeOf(player);
            Assert.AreEqual(TileType.Floor, world.Map[TileMap.Cell(home.X), TileMap.Cell(home.Y)]);
            Assert.Less(Vec2.Distance(home, TileMap.Center(5, 5)), 1.5f);

            world.SpawnEnemy(Monsters.Brute.Monsters[0], player.Position + new Vec2(0.9f, 0));
            for (int i = 0; i < World.TicksPerSecond * 3 && !player.IsDead; i++) world.Step();
            Assume.That(player.IsDead);
            Run(world, 1.2f);
            Assert.IsFalse(player.IsDead);
            Assert.AreEqual(home, player.Position);
        }

        [Test]
        public void EachPlayerHasOneBedAndDiggingItUpSendsThemToTheSpawn()
        {
            var world = Room(out var player);
            PlaceBed(world, player, 5, 5);
            PlaceBed(world, player, 20, 5);
            Assert.AreEqual((20, 5), world.BedOf(player), "the newer bed");
            Assert.AreEqual(1, world.Beds.Count());
            Assert.AreEqual(TileType.Bed, world.Map[5, 5], "the old bed stays, nobody's");

            var other = world.AddPlayer("b", null);
            Assert.IsNull(world.BedOf(other));
            Assert.AreEqual(world.Spawn, world.HomeOf(other));

            // Dig the bed up (anything takes down a placed thing).
            Stand(world, player, 20, 6, new Vec2(0, -1));
            world.SetAttacking(player.Id, true);
            Run(world, 0.2f);
            world.SetAttacking(player.Id, false);
            Assert.AreEqual(TileType.Floor, world.Map[20, 5]);
            Assert.IsNull(world.BedOf(player));
            Assert.AreEqual(world.Spawn, world.HomeOf(player));
        }

        [Test]
        public void ARecallScrollTakesThePlayerHomeUnlessAHitBreaksIt()
        {
            var world = Room(out var player);
            PlaceBed(world, player, 5, 5);
            Stand(world, player, 25, 25, new Vec2(1, 0));
            player.Inventory.AddConsumable(Scroll, 2);
            var events = new System.Collections.Generic.List<SimEvent>();

            Assert.IsTrue(world.UseConsumable(player.Id, Scroll), "usable at full health");
            Assert.IsTrue(player.Recalling);
            events.AddRange(world.TakeEvents());
            Run(world, World.RecallSeconds - 0.2f);
            Assert.AreEqual(TileMap.Center(25, 25), player.Position, "not yet");
            Run(world, 0.3f);
            Assert.AreEqual(world.HomeOf(player), player.Position);
            Assert.IsFalse(player.Recalling);

            // Again, far from home, struck halfway.
            Stand(world, player, 25, 25, new Vec2(1, 0));
            Run(world, Scroll.Cooldown);
            world.TakeEvents();
            Assert.IsTrue(world.UseConsumable(player.Id, Scroll));
            Run(world, 0.5f);
            world.SpawnEnemy(Monsters.Brute.Monsters[0], player.Position + new Vec2(0.9f, 0));
            for (int i = 0; i < World.TicksPerSecond && player.Recalling; i++) world.Step();
            var recalls = world.TakeEvents().Where(e => e.Type == SimEventType.Recall).ToList();
            Assert.IsTrue(recalls.Any(e => e.Amount == World.RecallBroken));
            Assert.Less(Vec2.Distance(player.Position, TileMap.Center(25, 25)), 2, "still where it was");
            Assert.AreEqual(0, player.Inventory.CountOf(Scroll), "the scroll is spent either way");
        }

        [Test]
        public void BedsAreSavedWithTheWorldAndPlayersStartBesideThem()
        {
            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            var world = host.World;
            var player = world.AddPlayer("앨리스", null);
            // Somewhere on the floor next to the spawn clearing.
            int sx = TileMap.Cell(world.Spawn.X);
            int sy = TileMap.Cell(world.Spawn.Y);
            PlaceBed(world, player, sx + 2, sy + 2);
            var bed = world.BedOf(player);

            var save = WorldSave.Read(host.SaveWorld(now: 100), Tiles.Shipped, Gear.Data);
            CollectionAssert.AreEqual(new[] { (sx + 2, sy + 2, "앨리스") }, save.Beds);
            var again = new WorldHost(save.Seed, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped,
                Tiles.Shipped);
            again.Restore(save, now: 100);

            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped);
            again.Receive(1, client.JoinMessage());
            again.Tick();
            var back = again.World.Players.Single();
            Assert.AreEqual(bed, again.World.BedOf(back));
            Assert.AreEqual(again.World.HomeOf(back), back.Position, "starts beside the bed");
        }
    }
}
