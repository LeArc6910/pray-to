using System.Linq;
using NUnit.Framework;
using static Game.Core.Tests.Builds;

namespace Game.Core.Tests
{
    // Base defense and turrets (Docs/Defense.md).
    public class DefenseTests
    {
        static readonly MaterialDefinition StoneItem = Gear.Data.FindMaterial("ward_stone");
        static readonly MaterialDefinition TurretItem = Gear.Data.FindMaterial("turret");
        static readonly MaterialDefinition AmmoItem = Gear.Data.FindMaterial(World.Ammo);
        static readonly MaterialDefinition Coin = Gear.Data.FindMaterial(World.Coin);

        // A big room with a ward stone in the middle (20, 20) and the player beside it, below.
        static World Base(out Player player, bool stone = true)
        {
            var world = new World(Maps.Room(40), TileMap.Center(20, 18), Monsters.Shipped, 3, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            player = world.AddPlayer("a", null);
            if (stone) Place(world, player, StoneItem, 20, 20, new Vec2(0, 1));
            return world;
        }

        static void KillWave(World world)
        {
            foreach (var monster in world.ActiveDefense.Monsters) monster.Health = 0;
            world.Step();
        }

        [Test]
        public void ItIsMadeAtTheWorkbenchAndTurretsTakeAmmo()
        {
            Assert.AreEqual(TileType.WardStone, StoneItem.Places);
            Assert.AreEqual(TileType.Turret, TurretItem.Places);
            Assert.AreEqual(10, Gear.Data.Recipes.Single(r => r.Id == "ammo").Count);
            Assert.AreEqual(2, Tiles.Shipped.Machine(TileType.Turret).Power, "power makes it shoot twice as fast");
        }

        [Test]
        public void ADefenseNeedsAStoneAndComesOnceADay()
        {
            var world = Base(out var player, stone: false);
            Assert.IsFalse(world.StartDefense(player.Id), "no stone");
            Place(world, player, StoneItem, 20, 20, new Vec2(0, 1));
            Assert.IsTrue(world.StartDefense(player.Id));
            Assert.IsFalse(world.StartDefense(player.Id), "one under way");
            Assert.AreEqual(World.DefenseStarted, world.TakeEvents().Single(e => e.Type == SimEventType.Defense).Amount);

            var defense = world.ActiveDefense;
            Assert.AreEqual(1, defense.Wave);
            Assert.AreEqual(3 + 1 + 2, defense.Monsters.Count);
            Assert.IsTrue(defense.Monsters.All(m => m.Rift == World.DefenseMonster));
            Assert.IsTrue(defense.Monsters.All(m => Vec2.Distance(m.Position, defense.Center) is >= 10.5f and <= 17.5f), "they come from far off");

            for (int wave = 1; wave <= World.DefenseWaves; wave++) KillWave(world);
            Assert.IsNull(world.ActiveDefense);
            Assert.IsFalse(world.StartDefense(player.Id), "done today");
            world.RestoreClock(world.Clock + Calendar.TicksPerDay);
            Assert.IsTrue(world.StartDefense(player.Id), "the next day");
        }

        [Test]
        public void LeftAloneTheMonstersBreakTheStone()
        {
            var world = Base(out var player);
            Assert.IsTrue(world.StartDefense(player.Id));
            // Off in a far corner, out of their sight.
            player.Position = TileMap.Center(2, 37);
            world.TakeEvents();
            bool? won = null;
            for (int i = 0; i < World.Ticks(300) && won == null; i++)
            {
                world.Step();
                var end = world.TakeEvents().FirstOrDefault(e => e.Type == SimEventType.Defense);
                if (end.Type == SimEventType.Defense) won = end.Amount == World.DefenseWon;
            }
            Assert.AreEqual(false, won);
            Assert.IsNull(world.ActiveDefense);
            Assert.IsEmpty(world.Enemies, "the rest go");
            Assert.AreEqual(1, world.DefenseLevel, "no next level");
        }

        [Test]
        public void HoldingItPaysTheDefendersAndRaisesTheLevel()
        {
            var world = Base(out var player);
            int coins = player.Inventory.CountOf(Coin);
            Assert.IsTrue(world.StartDefense(player.Id));
            for (int wave = 1; wave <= World.DefenseWaves; wave++)
            {
                Assert.AreEqual(wave, world.ActiveDefense.Wave);
                KillWave(world);
            }
            Assert.IsNull(world.ActiveDefense);
            Assert.AreEqual(2, world.DefenseLevel);
            Assert.AreEqual(coins + World.DefenseCoins(1), player.Inventory.CountOf(Coin));
            Assert.AreEqual(2, world.GroundLoot.Count(l => l.Material?.Id == World.Essence));
            Assert.GreaterOrEqual(world.GroundLoot.Count(l => l.Item != null), 1);
        }

        [Test]
        public void ALateWaveIsJoinedByTheNext()
        {
            var world = Base(out var player);
            Assert.IsTrue(world.StartDefense(player.Id));
            player.Position = TileMap.Center(2, 37);
            Run(world, World.DefenseWaveSeconds + 0.1f);
            Assert.AreEqual(2, world.ActiveDefense?.Wave ?? 2);
        }

        [Test]
        public void ATurretWithAmmoShootsTheNearestMonster()
        {
            var world = Base(out var player, stone: false);
            Place(world, player, TurretItem, 20, 20, new Vec2(0, 1));
            var slime = world.SpawnEnemy(Monsters.Shipped.Monsters.First(m => m.Id == "slime"), TileMap.Center(24, 20));
            float before = slime.Health;
            Run(world, 2);
            Assert.AreEqual(before, slime.Health, "no ammo, no shot");

            player.Inventory.AddMaterial(AmmoItem, 3);
            Assert.AreEqual(3, world.LoadMachine(player.Id, AmmoItem, 3));
            world.TakeEvents();
            world.Step();
            var events = world.TakeEvents();
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.TurretShot));
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Hit && e.Target == slime.Id && e.Source == 0));
            Assert.Less(slime.Health, before);
            Assert.AreEqual(2, world.MachineAt(20, 20).Inputs.Values.Sum());
        }

        [Test]
        public void TheLevelAndTheDayAreSaved()
        {
            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            host.World.RestoreDefense(4, 3);
            var save = WorldSave.Read(host.SaveWorld(now: 0), Tiles.Shipped, Gear.Data);
            Assert.AreEqual(4, save.DefenseLevel);
            Assert.AreEqual(3, save.DefenseDay);
            var again = new WorldHost(save.Seed, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            again.Restore(save);
            Assert.AreEqual(4, again.World.DefenseLevel);
            Assert.AreEqual(3, again.World.DefenseDay);
        }
    }
}
