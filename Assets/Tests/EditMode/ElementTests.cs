using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class ElementTests
    {
        static readonly ClassDefinition FireMage = Classes.Shipped.Find("fire_mage");
        static readonly ClassDefinition StormCaller = Classes.Shipped.Find("storm_caller");
        static readonly MonsterDefinition Dummy = Monsters.Dummy;
        static readonly MonsterDefinition Slime = Monsters.Shipped.Find("slime");
        static readonly MonsterDefinition IceSlime = Monsters.Shipped.Find("ice_slime");

        static World Room(int size = 30, MonsterData monsters = null) =>
            new World(Maps.Room(size), TileMap.Center(size / 2, size / 2), monsters ?? Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 },
                Gear.Data);

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

        static int Slot(Player player, string skillId) => player.Skills.ToList().FindIndex(s => s.Id == skillId);

        // One basic attack, then the button let go.
        static List<SimEvent> AttackOnce(World world, Player player)
        {
            world.SetAttacking(player.Id, true);
            world.Step();
            world.SetAttacking(player.Id, false);
            return world.TakeEvents();
        }

        [Test]
        public void FireBurnsForAShareOfTheHitOverThreeSeconds()
        {
            var world = Room();
            var player = world.AddPlayer("a", Gear.Wearing("mithril_sword"), FireMage);
            var dummy = world.SpawnEnemy(Dummy, player.Position + new Vec2(0, -1));
            world.UseSkill(player.Id, Slot(player, "flame_brand"), Vec2.Zero);

            var events = AttackOnce(world, player);
            int dealt = events.Single(e => e.Type == SimEventType.Hit && e.Target == dummy.Id).Amount;
            Assert.IsTrue(dummy.Afflictions.Burning);
            Assert.AreEqual(StatusBits.Burning, dummy.Afflictions.Bits);

            events = Run(world, World.BurnSeconds + 0.1f);
            var burns = events.Where(e => e.Type == SimEventType.Hit && e.Target == dummy.Id).ToList();
            Assert.That(burns.Count, Is.InRange(5, 7), "reported every half second");
            Assert.IsTrue(burns.All(e => e.Source == player.Id), "the burn is the player's");
            Assert.AreEqual(dealt * World.BurnShare, burns.Sum(e => e.Amount), 1);
            Assert.AreEqual(Dummy.Health - dealt - burns.Sum(e => e.Amount), dummy.Health, 1);
            Assert.IsFalse(dummy.Afflictions.Burning);
        }

        [Test]
        public void ABurnThatKillsCountsAsThePlayersKill()
        {
            var weak = MonsterSheet.Parse(Monsters.Header.Replace("weight\n", "weight,drop_chance\n") + "weak,약골,1,25,0,0,1,0.4,0,0.5,0.5,1,1,100\n");
            var world = Room(monsters: weak);
            var player = world.AddPlayer("a", Gear.Wearing("mithril_sword"), FireMage);
            var target = world.SpawnEnemy(weak.Monsters[0], player.Position + new Vec2(0, -1));
            world.UseSkill(player.Id, Slot(player, "flame_brand"), Vec2.Zero);
            // The sword's lowest 17, fire mage's 15% more: 20 of 25, and the burn's 30% of 20 finishes it.
            var hit = AttackOnce(world, player);
            Assume.That(target.IsDead, Is.False, "a critical hit killed it at once");
            Assert.AreEqual(20, hit.Single(e => e.Type == SimEventType.Hit).Amount);

            var events = Run(world, World.BurnSeconds + 0.5f);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Died && e.Source == target.Id));
            Assert.IsTrue(world.GroundLoot.Any() || player.Inventory.Items.Count > 0, "it dropped what it carried (and the player may have picked it up)");
        }

        [Test]
        public void ColdSlowsAMonsterDown()
        {
            var world = Room(40);
            // Two lanes far apart: in each a player shoots a slime six tiles off once, with cold in one and not the other.
            var frost = world.AddPlayer("frost", Gear.Wearing("frost_staff"));
            var plain = world.AddPlayer("plain", Gear.Wearing("copper_staff"));
            Moves.Walk(world, frost, TileMap.Center(5, 10));
            Moves.Walk(world, plain, TileMap.Center(5, 30));
            world.MovePlayer(frost.Id, frost.Position, new Vec2(1, 0), 1);
            world.MovePlayer(plain.Id, plain.Position, new Vec2(1, 0), 1);
            var chilled = world.SpawnEnemy(Slime, TileMap.Center(11, 10));
            var free = world.SpawnEnemy(Slime, TileMap.Center(11, 30));
            world.SetAttacking(frost.Id, true);
            world.SetAttacking(plain.Id, true);
            world.Step();
            world.SetAttacking(frost.Id, false);
            world.SetAttacking(plain.Id, false);
            Run(world, 0.7f);
            Assert.IsTrue(chilled.Afflictions.Chilled, "the frost bolt landed");
            Assert.IsFalse(free.Afflictions.Chilled);

            Run(world, 1.2f);
            float behind = Vec2.Distance(chilled.Position, frost.Position) - Vec2.Distance(free.Position, plain.Position);
            Assert.Greater(behind, 0.6f, "the chilled slime lags behind");
            Run(world, World.ChillSeconds);
            Assert.IsFalse(chilled.Afflictions.Chilled, "the chill wears off");
        }

        [Test]
        public void LightningJumpsToTheTwoNearestForHalf()
        {
            var world = Room();
            var player = world.AddPlayer("a", Gear.Wearing("storm_bow"));
            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
            var target = world.SpawnEnemy(Dummy, player.Position + new Vec2(4, 0));
            var near = world.SpawnEnemy(Dummy, target.Position + new Vec2(0, 1.5f));
            var nearer = world.SpawnEnemy(Dummy, target.Position + new Vec2(0, -1));
            var third = world.SpawnEnemy(Dummy, target.Position + new Vec2(2.5f, 1.5f));
            var far = world.SpawnEnemy(Dummy, target.Position + new Vec2(0, 6));

            AttackOnce(world, player);
            var events = Run(world, 0.5f);

            int dealt = events.First(e => e.Type == SimEventType.Hit && e.Target == target.Id).Amount;
            var chains = events.Where(e => e.Type == SimEventType.Chained).ToList();
            CollectionAssert.AreEquivalent(new[] { near.Id, nearer.Id }, chains.Select(e => e.Target));
            Assert.IsTrue(chains.All(e => e.Source == target.Id));
            foreach (var jumped in new[] { near, nearer })
            {
                int amount = events.Single(e => e.Type == SimEventType.Hit && e.Target == jumped.Id).Amount;
                Assert.AreEqual(dealt * World.ChainShare, amount, 1);
            }
            Assert.AreEqual(Dummy.Health, third.Health, "only two");
            Assert.AreEqual(Dummy.Health, far.Health);
        }

        [Test]
        public void MonstersResistOrAreWeakToElements()
        {
            Assert.AreEqual(75, IceSlime.Resist(SkillTag.Cold));
            Assert.AreEqual(-25, IceSlime.Resist(SkillTag.Fire));
            Assert.AreEqual(0.25f, World.ElementFactor(IceSlime, SkillTag.Magic | SkillTag.Cold), 0.001f);
            Assert.AreEqual(1.25f, World.ElementFactor(IceSlime, SkillTag.Magic | SkillTag.Fire), 0.001f);
            Assert.AreEqual(1.25f, World.ElementFactor(IceSlime, SkillTag.Cold | SkillTag.Fire), 0.001f, "the weakest resistance counts");
            Assert.AreEqual(1, World.ElementFactor(IceSlime, SkillTag.Melee), 0.001f);

            // Ice slimes shrug off the chill too.
            var world = Room();
            var player = world.AddPlayer("a", Gear.Wearing("copper_sword"), StormCaller);
            var slime = world.SpawnEnemy(IceSlime, player.Position + new Vec2(1.5f, 0));
            world.UseSkill(player.Id, Slot(player, "frost_ring"), Vec2.Zero);
            Assert.Less(slime.Health, IceSlime.Health);
            Assert.IsFalse(slime.Afflictions.Chilled, "immune at 75");

            var data = MonsterSheet.Parse(Monsters.Header.Replace("weight\n", "weight,resist,element\n") +
                "a,가,1,1,0,0,1,0.3,0,0.5,0.5,1,1,water:10,\n" +
                "b,나,1,1,0,0,1,0.3,0,0.5,0.5,1,1,fire:150,\n" +
                "c,다,1,1,0,0,1,0.3,0,0.5,0.5,1,1,,magic\n");
            Assert.IsTrue(data.Errors.Any(e => e.Contains("2행") && e.Contains("water")), string.Join("\n", data.Errors));
            Assert.IsTrue(data.Errors.Any(e => e.Contains("3행") && e.Contains("100")), string.Join("\n", data.Errors));
            Assert.IsTrue(data.Errors.Any(e => e.Contains("4행") && e.Contains("element")), string.Join("\n", data.Errors));
        }

        [Test]
        public void ColdMonstersChillThePlayersTheyHit()
        {
            var world = Room();
            var player = world.AddPlayer("a", Gear.Wearing("copper_sword"));
            world.SpawnEnemy(IceSlime, player.Position + new Vec2(0.9f, 0));
            float speed = player.MoveSpeed;

            var events = Run(world, 1.5f);
            Assume.That(events.Any(e => e.Type == SimEventType.Hit && e.Target == player.Id), "the slime struck");
            Assert.IsTrue(player.Afflictions.Chilled);
            Assert.AreEqual(speed * World.PlayerChillSpeed, player.MoveSpeed, 0.001f);
        }

        [Test]
        public void TheStormCallerBringsBothElements()
        {
            CollectionAssert.AreEqual(new[] { "chain_lightning", "frost_ring" }, StormCaller.Skills.Select(s => s.Id));
            Assert.IsTrue((Classes.Skill("chain_lightning").Tags & SkillTag.Lightning) != 0);
            Assert.IsTrue((Classes.Skill("frost_ring").Tags & SkillTag.Cold) != 0);
            foreach (string id in new[] { "frost_staff", "storm_bow" })
            {
                var recipe = Gear.Data.Recipes.Single(r => r.Item?.Id == id);
                Assert.IsTrue(recipe.Ingredients.Any(i => i.Material.Id == "ice_crystal" || i.Material.Id == "glow_spore"), id);
            }
        }
    }
}
