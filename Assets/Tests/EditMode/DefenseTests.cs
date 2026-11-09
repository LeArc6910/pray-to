using System.Collections.Generic;
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
        public void WalledInTheStoneTheMonstersBreakThroughWhatWasBuilt()
        {
            var world = Base(out var player);
            Assert.IsTrue(world.StartDefense(player.Id));
            // Off in a far corner, out of their sight, and a ring of built walls round the stone, two tiles out.
            player.Position = TileMap.Center(2, 37);
            var ring = new List<TileChange>();
            for (int x = 18; x <= 22; x++)
            {
                for (int y = 18; y <= 22; y++)
                {
                    if (x == 18 || x == 22 || y == 18 || y == 22) ring.Add(new TileChange(x, y, TileType.Wall));
                }
            }
            world.Restore(ring, System.Array.Empty<(CropDefinition, int, int, long, long)>(), 0);
            Assert.IsTrue(world.BuiltBarrier(18, 20));
            Assert.IsFalse(world.BuiltBarrier(0, 20), "the cave's own rock is no barrier");
            world.TakeEvents();

            bool broke = false;
            float stone = world.ActiveDefense.StoneHealth;
            for (int i = 0; i < World.TicksPerSecond * 120 && !broke; i++)
            {
                world.Step();
                broke = ring.Any(t => world.Map[t.X, t.Y] == TileType.Floor);
                Assert.AreEqual(stone, world.ActiveDefense?.StoneHealth ?? stone, "the stone is safe while the ring stands");
            }
            Assert.IsTrue(broke, "they broke in");
            Assert.IsTrue(world.GroundLoot.Any(l => l.Material?.Id == "stone"), "the broken wall's stone lies there");
        }

        [Test]
        public void ACrackedWallIsMendedFromTheBagAndIronHoldsLonger()
        {
            // Mid-defense (the monsters' way round the walls is rebuilt as they break), the waves left standing still.
            var world = Base(out var player);
            Assert.IsTrue(world.StartDefense(player.Id));
            var slime = world.SpawnEnemy(Monsters.Shipped.Find("slime"), TileMap.Center(30, 30));
            var stone = Gear.Data.FindMaterial("stone");
            var iron = Gear.Data.FindMaterial("iron_bar");
            Place(world, player, stone, 20, 16, new Vec2(0, -1));
            Place(world, player, Gear.Data.FindMaterial("iron_wall"), 22, 16, new Vec2(0, -1));
            Assert.IsTrue(world.BuiltBarrier(22, 16));

            // Nothing to mend yet, then one blow cracks the stone wall.
            Face(world, player, 20, 16, new Vec2(0, -1));
            player.Inventory.AddMaterial(stone, 3);
            Assert.IsFalse(world.Repair(player.Id), "a whole wall needs no mending");
            world.DamageBarrier(slime, 20, 16);
            world.TakeEvents();
            Assert.IsTrue(world.Repair(player.Id));
            Assert.AreEqual(2, player.Inventory.CountOf(stone), "one stone mends it");
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.Dug && e.Amount == World.Repaired));
            Assert.IsFalse(world.Repair(player.Id), "and it is whole again");

            // The iron wall takes iron, and stands far more blows than stone.
            int stoneBlows = 0, ironBlows = 0;
            while (world.Map[20, 16] == TileType.Wall) { world.DamageBarrier(slime, 20, 16); stoneBlows++; }
            while (world.Map[22, 16] == TileType.IronWall) { world.DamageBarrier(slime, 22, 16); ironBlows++; }
            Assert.GreaterOrEqual(ironBlows, stoneBlows * 2, $"iron {ironBlows} blows, stone {stoneBlows}");
            world.Restore(new[] { new TileChange(22, 16, TileType.IronWall) }, System.Array.Empty<(CropDefinition, int, int, long, long)>(), 0);
            world.DamageBarrier(slime, 22, 16);
            Face(world, player, 22, 16, new Vec2(0, -1));
            Assert.IsFalse(world.Repair(player.Id), "stone does not mend iron");
            player.Inventory.AddMaterial(iron);
            Assert.IsTrue(world.Repair(player.Id));
            Assert.AreEqual(0, player.Inventory.CountOf(iron));
        }

        [Test]
        public void FrostAndFireAmmoChillAndBurn()
        {
            var world = Base(out var player, stone: false);
            Place(world, player, TurretItem, 20, 20, new Vec2(0, 1));
            var slime = world.SpawnEnemy(Monsters.Shipped.Monsters.First(m => m.Id == "slime"), TileMap.Center(24, 20));
            slime.MaxHealth = slime.Health = 10000;
            var fire = Gear.Data.FindMaterial(World.FireAmmo);
            var frost = Gear.Data.FindMaterial(World.FrostAmmo);
            player.Inventory.AddMaterial(AmmoItem, 1);
            player.Inventory.AddMaterial(frost, 1);
            player.Inventory.AddMaterial(fire, 1);
            Assert.AreEqual(1, world.LoadMachine(player.Id, AmmoItem, 1));
            Assert.AreEqual(1, world.LoadMachine(player.Id, frost, 1));
            Assert.AreEqual(1, world.LoadMachine(player.Id, fire, 1));
            world.TakeEvents();

            var kinds = new List<int>();
            for (int i = 0; i < World.TicksPerSecond * 4 && kinds.Count < 3; i++)
            {
                world.Step();
                kinds.AddRange(world.TakeEvents().Where(e => e.Type == SimEventType.TurretShot).Select(e => e.Amount / World.TurretShotKind));
                if (kinds.Count == 1) Assert.IsTrue(slime.Afflictions.Burning, "fire first: it burns");
                if (kinds.Count == 2) Assert.IsTrue(slime.Afflictions.Chilled, "then frost: it slows");
            }
            CollectionAssert.AreEqual(new[] { 2, 1, 0 }, kinds, "fire, frost, then plain");
        }

        [Test]
        public void ATeslaTowerRunsOnPowerAndItsBoltLeaps()
        {
            var world = Base(out var player, stone: false);
            Place(world, player, Gear.Data.FindMaterial("tesla_tower"), 20, 20, new Vec2(0, 1));
            var slime = Monsters.Shipped.Monsters.First(m => m.Id == "slime");
            var near = new[] { new Vec2(24, 20.5f), new Vec2(25.5f, 20.5f), new Vec2(26.5f, 21.5f), new Vec2(30, 30) }
                .Select(at => world.SpawnEnemy(slime, at)).ToList();
            foreach (var monster in near) monster.MaxHealth = monster.Health = 10000;
            Run(world, 3);
            Assert.IsTrue(near.All(m => m.Health == m.MaxHealth), "no power, no bolt");

            var gel = Gear.Data.FindMaterial("slime_gel");
            Place(world, player, Gear.Data.FindMaterial("generator"), 18, 18, new Vec2(0, 1));
            player.Inventory.AddMaterial(gel, 5);
            Assert.IsTrue(world.Refuel(player.Id, gel));
            world.TakeEvents();
            var shots = new List<SimEvent>();
            for (int i = 0; i < World.TicksPerSecond * 3 && shots.Count == 0; i++)
            {
                world.Step();
                shots.AddRange(world.TakeEvents().Where(e => e.Type == SimEventType.TurretShot));
            }
            Assert.AreEqual(1 + World.TeslaLeaps, shots.Count, "the bolt and its leaps");
            Assert.IsTrue(shots.All(e => e.Amount / World.TurretShotKind == 3));
            Assert.IsTrue(near.Take(3).All(m => m.Health < m.MaxHealth));
            Assert.AreEqual(near[3].MaxHealth, near[3].Health, "too far to leap to");
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
