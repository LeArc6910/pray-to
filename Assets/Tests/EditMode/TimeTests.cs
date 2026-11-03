using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // Days, sleeping through the night, and what the night brings (Docs/Time.md).
    public class TimeTests
    {
        static readonly MaterialDefinition Bed = Gear.Data.FindMaterial("bed");
        static readonly long GameHour = Calendar.TicksPerDay / 24;

        static World Room(int enemies = 0, int size = 30) =>
            new World(Maps.Room(size), TileMap.Center(size / 2, size / 2), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = enemies }, Gear.Data,
                Tiles.Shipped);

        static List<SimEvent> Run(World world, long ticks)
        {
            var events = new List<SimEvent>();
            for (long i = 0; i < ticks; i++)
            {
                world.Step();
                events.AddRange(world.TakeEvents().Where(e => e.Type == SimEventType.Morning));
            }
            return events;
        }

        // Puts a bed of the player's at (x, y), with them standing just below it.
        static void PlaceBed(World world, Player player, int x, int y)
        {
            Moves.Walk(world, player, TileMap.Center(x, y + 1));
            world.MovePlayer(player.Id, player.Position, new Vec2(0, -1), 1);
            player.Inventory.AddMaterial(Bed);
            Assert.IsTrue(world.PlaceTile(player.Id, Bed));
        }

        [Test]
        public void ADayIsTwentyMinutesFromSixInTheMorning()
        {
            Assert.AreEqual(1200 * World.TicksPerSecond, Calendar.TicksPerDay);
            Assert.AreEqual(50 * World.TicksPerSecond, GameHour, "a game hour is 50 seconds");
            Assert.AreEqual("1일째 06:00", Calendar.Text(0));
            Assert.AreEqual("1일째 07:00", Calendar.Text(GameHour));
            Assert.AreEqual("1일째 23:30", Calendar.Text(GameHour * 17 + GameHour / 2));
            Assert.AreEqual("1일째 01:00", Calendar.Text(GameHour * 19), "still the first day's night");
            Assert.AreEqual("2일째 06:00", Calendar.Text(Calendar.TicksPerDay));
            Assert.AreEqual(Calendar.TicksPerDay * 3, Calendar.NextMorning(Calendar.TicksPerDay * 2 + 5));

            var phases = new[] { (0, DayPhase.Morning), (6, DayPhase.Day), (12, DayPhase.Evening), (16, DayPhase.Night), (23, DayPhase.Night) };
            foreach (var (hours, phase) in phases) Assert.AreEqual(phase, Calendar.Phase(GameHour * hours), $"{hours} hours after six");
            Assert.AreEqual("밤", Calendar.PhaseName(DayPhase.Night));
        }

        [Test]
        public void TheClockRunsWithTheWorld()
        {
            var world = Room();
            Run(world, 90);
            Assert.AreEqual(90, world.Clock);
            Assert.AreEqual(DayPhase.Morning, world.Phase);
        }

        [Test]
        public void WhenEveryoneIsInBedItIsMorning()
        {
            var world = Room();
            var a = world.AddPlayer("a", null);
            var b = world.AddPlayer("b", null);
            PlaceBed(world, a, 5, 5);
            PlaceBed(world, b, 9, 5);
            Moves.Walk(world, a, TileMap.Center(5, 6));

            Assert.IsFalse(world.Sleep(a.Id), "not in the morning");
            Run(world, GameHour * 12);
            Assert.AreEqual(DayPhase.Evening, world.Phase);
            Assert.IsFalse(world.Sleep(a.Id + 100), "nobody");

            Assert.IsTrue(world.Sleep(a.Id));
            Assert.IsTrue(a.Sleeping);
            Assert.AreEqual(1, world.Sleepers);
            long before = world.Clock;
            Run(world, 30);
            Assert.AreEqual(before + 30, world.Clock, "one awake, so the evening goes on");

            Moves.Walk(world, b, TileMap.Center(12, 12));
            Assert.IsFalse(world.Sleep(b.Id), "too far from b's bed");
            Moves.Walk(world, b, TileMap.Center(9, 6));
            Assert.IsTrue(world.Sleep(b.Id));
            var mornings = Run(world, 1);

            Assert.AreEqual(Calendar.TicksPerDay, world.Clock, "the next morning");
            Assert.AreEqual("2일째 06:00", Calendar.Text(world.Clock));
            Assert.AreEqual(2, mornings.Single().Amount, "day two");
            Assert.IsFalse(a.Sleeping || b.Sleeping, "everyone is up");
            Assert.AreEqual(0, world.Sleepers);
        }

        [Test]
        public void MovingOrAttackingGetsASleeperUp()
        {
            var world = Room();
            var a = world.AddPlayer("a", null);
            var b = world.AddPlayer("b", null);
            PlaceBed(world, a, 5, 5);
            Moves.Walk(world, a, TileMap.Center(5, 6));
            Run(world, GameHour * 16);
            Assert.AreEqual(DayPhase.Night, world.Phase);

            Assert.IsTrue(world.Sleep(a.Id));
            Moves.Walk(world, a, TileMap.Center(6, 7));
            Run(world, 1);
            Assert.IsFalse(a.Sleeping, "walked off");

            Moves.Walk(world, a, TileMap.Center(5, 6));
            Assert.IsTrue(world.Sleep(a.Id));
            // Facing away, or the swing would knock the bed down.
            world.MovePlayer(a.Id, a.Position, new Vec2(0, 1), 1);
            world.SetAttacking(a.Id, true);
            Run(world, 1);
            Assert.IsFalse(a.Sleeping, "swung");

            world.SetAttacking(a.Id, false);
            Assert.IsTrue(world.Sleep(a.Id));
            world.Wake(a.Id);
            Assert.IsFalse(a.Sleeping, "got up");
            Assert.IsFalse(world.Sleep(b.Id), "b has no bed");
        }

        [Test]
        public void SleepingThroughTheNightGrowsNoCrops()
        {
            var world = Room();
            var a = world.AddPlayer("a", null);
            PlaceBed(world, a, 5, 5);
            // A watered mushroom beside the bed.
            Moves.Walk(world, a, TileMap.Center(7, 6));
            world.MovePlayer(a.Id, a.Position, new Vec2(1, 0), 1);
            a.Inventory.AddMaterial(Gear.Data.FindMaterial("mushroom_spore"));
            Assert.IsTrue(world.Plant(a.Id, Tiles.Shipped.CropFrom(Gear.Data.FindMaterial("mushroom_spore"))));
            a.Inventory.AddMaterial(Gear.Data.FindMaterial(World.WateringCan));
            Assert.IsTrue(world.Water(a.Id));
            var crop = world.CropAt(8, 6);

            Run(world, GameHour * 13);
            Moves.Walk(world, a, TileMap.Center(5, 6));
            float before = world.CropAt(8, 6).Growth(world.Tick);
            Assert.IsTrue(world.Sleep(a.Id));
            Run(world, 1);

            Assert.AreEqual(Calendar.TicksPerDay, world.Clock, "slept to the next morning");
            Assert.AreEqual(before + 1f / crop.Definition.GrowTicks, world.CropAt(8, 6).Growth(world.Tick), 1e-6f,
                "one tick of growth, not the hours slept");
            Assert.IsTrue(world.CropAt(8, 6).IsWet(world.Tick), "and its water did not dry overnight");
        }

        [Test]
        public void TheNightBringsMoreMonsters()
        {
            var world = Room(enemies: 8, size: 60);
            world.AddPlayer("a", null);
            Run(world, GameHour * 15);
            Assert.AreEqual(8, world.Enemies.Count, "the evening's 8");

            Run(world, GameHour * 2);
            Assert.AreEqual(DayPhase.Night, world.Phase);
            Assert.AreEqual(8 * (100 + World.NightMonstersPercent) / 100, world.Enemies.Count, "a quarter more at night");
        }

        [Test]
        public void SomeFishBiteOnlyAtNight()
        {
            var firefly = Gear.Data.FindMaterial("firefly_catfish");
            Assert.AreEqual(Biome.Cave, firefly.Fish);
            Assert.IsTrue(firefly.FishAtNight);
            Assert.IsFalse(Gear.Data.FindMaterial("cave_minnow").FishAtNight);
            Assert.AreEqual(2, Gear.Data.Recipes.Single(r => r.Id == "grilled_firefly").Count, "one makes two grilled fish");

            var map = Maps.FromRows(
                "#######",
                "#....~#",
                "#....~#",
                "#....~#",
                "#######");
            var world = new World(map, TileMap.Center(4, 2), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            var player = world.AddPlayer("a", null);
            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
            player.Inventory.AddMaterial(Gear.Data.FindMaterial(World.FishingRod));

            int Catch(int times)
            {
                for (int i = 0; i < times; i++)
                {
                    Assert.IsTrue(world.Fish(player.Id));
                    for (int t = 0; t < World.Ticks(World.BiteMaxSeconds + 1) && !world.TakeEvents().Any(e => e.Type == SimEventType.Fishing && World.FishingKind(e.Amount) == World.FishingBite); t++)
                        world.Step();
                    if (player.Fishing != null) Reels.Land(world, player);
                }
                return player.Inventory.CountOf(firefly);
            }

            Assert.AreEqual(0, Catch(20), "none by day");
            Run(world, GameHour * 17 - world.Clock);
            Assert.AreEqual(DayPhase.Night, world.Phase);
            Assert.Greater(Catch(20), 0, "some at night");
        }
    }
}
