using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // Void rifts (World.Rift.cs, Docs/Bosses.md 공허 균열).
    public class RiftTests
    {
        static readonly MaterialDefinition Key = Gear.Data.FindMaterial(World.RiftKey);

        // A player beside an altar in the middle of a big empty room.
        static World Altar(out Player player, bool key = true)
        {
            var map = Maps.Room(30);
            map[15, 16] = TileType.RiftAltar;
            var world = new World(map, TileMap.Center(15, 15), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            player = world.AddPlayer("a", null);
            if (key) player.Inventory.AddMaterial(Key);
            return world;
        }

        static void KillWave(World world)
        {
            foreach (var monster in world.Enemies) monster.Health = 0;
            world.Step();
            world.Step();
        }

        [Test]
        public void TheAltarAndKeysAreMade()
        {
            Assert.AreEqual(TileType.RiftAltar, Gear.Data.FindMaterial("rift_altar").Places);
            var recipe = Gear.Data.Recipes.Single(r => r.Material == Key);
            Assert.AreEqual(TileType.Anvil, recipe.Station);
            Assert.IsTrue(recipe.Ingredients.Any(i => i.Material.Id == "void_essence"), "from the final boss");
        }

        [Test]
        public void AKeyAtTheAltarOpensARiftOfTheWorldsTier()
        {
            var world = Altar(out var player, key: false);
            Assert.IsFalse(world.OpenRift(player.Id), "no key");
            player.Inventory.AddMaterial(Key);
            Assert.IsTrue(world.OpenRift(player.Id));
            Assert.AreEqual(0, player.Inventory.CountOf(Key), "used up");
            Assert.IsFalse(world.OpenRift(player.Id), "one at a time");

            var rift = world.ActiveRift;
            Assert.AreEqual(1, rift.Tier);
            Assert.AreEqual(1, rift.Wave);
            Assert.AreEqual(TileMap.Center(15, 16), rift.Center);
            Assert.AreEqual(1, System.Enum.GetValues(typeof(RiftAffix)).Cast<RiftAffix>().Count(a => a != RiftAffix.None && (rift.Affixes & a) != 0),
                "one affix at the first tier");
            Assert.That(world.Enemies.Count, Is.InRange(5, 9));
            Assert.IsTrue(world.Enemies.All(e => e.Rift == World.RiftMonster));
            Assert.IsTrue(world.Enemies.All(e => Vec2.Distance(e.Position, rift.Center) <= 7.5f));
        }

        [Test]
        public void ThreeWavesThenTheGuardianThenRewardsAndTheNextTier()
        {
            var world = Altar(out var player);
            world.OpenRift(player.Id);
            for (int wave = 1; wave <= World.RiftWaves; wave++)
            {
                Assert.AreEqual(wave, world.ActiveRift.Wave);
                KillWave(world);
            }
            Assert.AreEqual(World.RiftWaves + 1, world.ActiveRift.Wave);
            var guardian = world.Enemies.Single();
            Assert.AreEqual(World.RiftGuardian, guardian.Rift);
            Assert.Greater(guardian.MaxHealth, guardian.Definition.Health * 5, "far tougher than its sheet");

            // Away from the altar, so nothing is picked up before it is counted.
            Moves.Walk(world, player, TileMap.Center(3, 3));
            world.TakeEvents();
            KillWave(world);
            Assert.IsNull(world.ActiveRift);
            Assert.AreEqual(2, world.RiftTier, "the next tier opens");
            Assert.That(world.GroundLoot.Count(l => l.Item != null), Is.GreaterThanOrEqualTo(2));
            Assert.That(world.GroundLoot.Count(l => l.Material?.Id == World.Essence), Is.EqualTo(3));
            var cleared = world.TakeEvents().Single(e => e.Type == SimEventType.Rift);
            Assert.AreEqual(World.RiftCleared, cleared.Amount);
        }

        [Test]
        public void RunningOutOfTimeClosesItAndItsMonstersGo()
        {
            var world = Altar(out var player);
            world.OpenRift(player.Id);
            for (int i = 0; i < World.Ticks(World.RiftSeconds) + 1; i++) world.Step();
            Assert.IsNull(world.ActiveRift);
            Assert.IsEmpty(world.Enemies);
            Assert.AreEqual(1, world.RiftTier, "no next tier");
        }

        [Test]
        public void HigherTiersAreTougherAndHaveMoreAffixes()
        {
            var world = Altar(out var player);
            world.RestoreRiftTier(7);
            world.OpenRift(player.Id);
            var rift = world.ActiveRift;
            Assert.AreEqual(3, System.Enum.GetValues(typeof(RiftAffix)).Cast<RiftAffix>().Count(a => a != RiftAffix.None && (rift.Affixes & a) != 0));
            var monster = world.Enemies.First();
            Assert.Greater(monster.MaxHealth, monster.Definition.Health * 3);
            Assert.Greater(monster.Damage, monster.Definition.Damage * 1.9f);
        }

        [Test]
        public void TheWorldSaveKeepsTheTier()
        {
            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            host.World.RestoreRiftTier(4);
            var save = WorldSave.Read(host.SaveWorld(0), Tiles.Shipped);
            Assert.AreEqual(4, save.RiftTier);
            var again = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
            again.Restore(save);
            Assert.AreEqual(4, again.World.RiftTier);
        }
    }
}
