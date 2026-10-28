using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class HealingTests
    {
        static readonly ConsumableDefinition Potion = Gear.Data.FindConsumable("healing_potion");
        // Hits once for 40 a moment after noticing someone, then rests for half a minute; one swing kills it.
        static readonly MonsterData Pebbles = MonsterSheet.Parse(Monsters.Header + "pebble,조약돌,1,1,40,0,1,0.3,8,0.5,0.1,30,1\n");

        static World Room(MonsterData monsters) =>
            new World(Maps.Room(14), TileMap.Center(7, 7), monsters, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data);

        static List<SimEvent> Run(World world, float seconds)
        {
            var events = new List<SimEvent>();
            for (int i = 0; i < World.Ticks(seconds); i++)
            {
                world.Step();
                events.AddRange(world.TakeEvents());
            }
            return events;
        }

        // A player with a sword and 60 of their 100 health, the pebble that hit them already gone.
        static Player Hurt(World world)
        {
            var player = world.AddPlayer("a", Gear.Wearing("copper_sword"));
            world.SpawnEnemy(Pebbles.Monsters[0], player.Position + new Vec2(0.9f, 0));
            Run(world, 0.5f);
            Assert.AreEqual(60, player.Health, 0.01f);

            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
            world.SetAttacking(player.Id, true);
            Run(world, World.TickSeconds);
            world.SetAttacking(player.Id, false);
            Assert.IsEmpty(world.Enemies.Where(e => !e.IsDead));
            return player;
        }

        [Test]
        public void ShippedPotionHealsAThirdOverTwoSeconds()
        {
            Assert.AreEqual(35, Potion.Heal);
            Assert.AreEqual(2, Potion.Seconds, 0.001f);
            Assert.AreEqual(10, Potion.Cooldown, 0.001f);
        }

        [Test]
        public void ConsumableSheetErrorsNameTheRow()
        {
            var data = TestLoot.Parse(consumables:
                "id,name,level,heal,seconds,cooldown,weight,description\n" +
                "weak,맹물,1,0,1,5,1,\n" +
                "rush,조급한 물약,1,10,1,0,1,\n");

            TestLoot.AssertError(data, "consumables.csv 2행", "heal은 1~100");
            TestLoot.AssertError(data, "consumables.csv 3행", "cooldown은 0보다");
            Assert.IsEmpty(TestLoot.Parse().Consumables, "no sheet, no consumables");
            Assert.IsTrue(TestLoot.Parse().Success);
        }

        [Test]
        public void RestingHealsOnceUnhurtForSixSeconds()
        {
            var world = Room(Pebbles);
            var player = Hurt(world);

            Run(world, 5);
            Assert.AreEqual(60, player.Health, 0.01f, "still within six seconds of the hit");

            Run(world, 3);
            // About two and a half seconds of resting at 3% of 100 a second.
            Assert.That(player.Health, Is.InRange(66f, 69f));
        }

        [Test]
        public void PotionsHealOverTimeThenEveryConsumableWaits()
        {
            var world = Room(Pebbles);
            var player = Hurt(world);
            player.Inventory.AddConsumable(Potion, 2);

            Assert.IsTrue(world.UseConsumable(player.Id, Potion));
            var healed = world.TakeEvents().Single(e => e.Type == SimEventType.Healed);
            Assert.AreEqual(35, healed.Amount);
            Assert.AreEqual(1, player.Inventory.CountOf(Potion));

            Run(world, 1);
            Assert.AreEqual(77.5f, player.Health, 0.6f, "half of it after one second");
            Run(world, 1);
            Assert.AreEqual(95, player.Health, 0.01f);
            Assert.IsFalse(world.UseConsumable(player.Id, Potion), "still waiting");

            Run(world, 8);
            Assert.AreEqual(player.Stats.MaxHealth, player.Health, "rested the last of it back");
            Assert.IsFalse(world.UseConsumable(player.Id, Potion), "nothing to heal");
            Assert.AreEqual(1, player.Inventory.CountOf(Potion));
        }

        [Test]
        public void MonstersDropPotionsThatGoIntoTheBag()
        {
            var jars = MonsterSheet.Parse(Monsters.Header.Replace("weight\n", "weight,potion_chance\n") + "jar,항아리,1,1,0,0,1,0.3,0,0.5,0.5,1,1,100\n");
            var world = Room(jars);
            var player = world.AddPlayer("a", Gear.Wearing("copper_sword"));
            world.SpawnEnemy(jars.Monsters[0], player.Position + new Vec2(0.9f, 0));
            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);

            world.SetAttacking(player.Id, true);
            var events = Run(world, World.TickSeconds);
            world.SetAttacking(player.Id, false);
            // It may land within reach and be picked up the same tick; otherwise walk over to it.
            foreach (var drop in world.GroundLoot.ToList())
            {
                Assert.AreSame(Potion, drop.Consumable);
                world.MovePlayer(player.Id, drop.Position, new Vec2(1, 0), 10);
                events.AddRange(Run(world, World.TickSeconds));
            }

            Assert.AreEqual(1, player.Inventory.CountOf(Potion));
            Assert.IsEmpty(world.GroundLoot);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Pickup && e.Source == player.Id));
        }
    }
}
