using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class WorldTests
    {
        static readonly MonsterDefinition Slime = Monsters.Shipped.Find("slime");

        static World Room(MonsterData monsters = null, int size = 14) =>
            new World(Maps.Room(size), TileMap.Center(size / 2, size / 2), monsters ?? Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });

        static void Face(World world, Player player, Vec2 facing) => world.MovePlayer(player.Id, player.Position, facing, 1);

        static List<SimEvent> Run(World world, int ticks)
        {
            var events = new List<SimEvent>();
            for (int i = 0; i < ticks; i++)
            {
                world.Step();
                events.AddRange(world.TakeEvents());
            }
            return events;
        }

        [Test]
        public void SwingsHitEnemiesInFrontAndNotBehind()
        {
            var world = Room();
            var player = world.AddPlayer("a", Gear.Wearing("copper_sword"));
            var front = world.SpawnEnemy(Slime, player.Position + new Vec2(1.2f, 0));
            var behind = world.SpawnEnemy(Slime, player.Position + new Vec2(-1.2f, 0));
            Face(world, player, new Vec2(1, 0));

            world.SetAttacking(player.Id, true);
            var events = Run(world, 1);

            // The copper sword's lowest roll is 4; a critical hit deals 150%.
            Assert.That(Slime.Health - front.Health, Is.EqualTo(4).Or.EqualTo(6));
            Assert.AreEqual(Slime.Health, behind.Health);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Attack && e.Source == player.Id));
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Hit && e.Source == player.Id && e.Target == front.Id));
        }

        [Test]
        public void HoldingAttackSwingsAtTheWeaponsRate()
        {
            var world = Room();
            var player = world.AddPlayer("a", Gear.Wearing("copper_sword"));

            world.SetAttacking(player.Id, true);
            var swings = Run(world, World.TicksPerSecond).Count(e => e.Type == SimEventType.Attack && e.Source == player.Id);

            Assert.AreEqual(2, swings);
        }

        [Test]
        public void EnemiesDieAndLeaveTheWorld()
        {
            var world = Room();
            var player = world.AddPlayer("a", Gear.Wearing("copper_sword"));
            var slime = world.SpawnEnemy(Slime, player.Position + new Vec2(1.2f, 0));
            Face(world, player, new Vec2(1, 0));

            world.SetAttacking(player.Id, true);
            var events = Run(world, 20 * World.TicksPerSecond);

            Assert.IsTrue(slime.IsDead);
            CollectionAssert.DoesNotContain(world.Enemies, slime);
            Assert.AreEqual(1, events.Count(e => e.Type == SimEventType.Died && e.Source == slime.Id));
        }

        [Test]
        public void EnemiesWindUpThenStrikeAPlayerWhoStaysInReach()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var slime = world.SpawnEnemy(Slime, player.Position + new Vec2(0.9f, 0));

            Run(world, 2);
            Assert.AreEqual(EnemyState.Windup, slime.State);
            var events = Run(world, World.Ticks(Slime.Windup));

            Assert.AreEqual(PlayerStats.BaseHealth - Slime.Damage, player.Health, 0.01f);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Hit && e.Source == slime.Id && e.Target == player.Id));
        }

        [Test]
        public void SteppingAwayDuringTheWindUpDodgesTheStrike()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var slime = world.SpawnEnemy(Slime, player.Position + new Vec2(0.9f, 0));

            Run(world, 2);
            Assert.AreEqual(EnemyState.Windup, slime.State);
            var events = new List<SimEvent>();
            for (int i = 0; i < World.Ticks(Slime.Windup); i++)
            {
                var next = PlayerMotor.Step(world.Map, player.Position, new Vec2(-1, 0), player.Stats.MoveSpeed);
                Assert.IsTrue(world.MovePlayer(player.Id, next, new Vec2(-1, 0), 1));
                events.AddRange(Run(world, 1));
            }

            Assert.AreEqual(PlayerStats.BaseHealth, player.Health);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Attack && e.Source == slime.Id), "the slime still swings at the air");
        }

        [Test]
        public void ArmorSoftensStrikes()
        {
            var world = Room();
            var player = world.AddPlayer("a", Gear.Wearing("copper_armor"));
            world.SpawnEnemy(Slime, player.Position + new Vec2(0.9f, 0));

            Run(world, 2 + World.Ticks(Slime.Windup));

            // Copper armor's lowest rolls: Armor+6 and MaxHealth+5. 9 × 100 / 106 rounds to 8.
            Assert.AreEqual(105 - 8, player.Health, 0.01f);
        }

        [Test]
        public void DeadPlayersRespawnAtTheSpawnWithFullHealth()
        {
            var world = Room(Monsters.Brute);
            var player = world.AddPlayer("a", null);
            world.SpawnEnemy(Monsters.Brute.Find("brute"), player.Position + new Vec2(0.9f, 0));

            var events = Run(world, 2 + World.Ticks(0.5f));
            Assert.IsTrue(player.IsDead);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Died && e.Source == player.Id));
            Assert.IsFalse(world.MovePlayer(player.Id, player.Position + new Vec2(0.1f, 0), new Vec2(1, 0), 1), "the dead stay put");

            int corrections = player.Corrections;
            Run(world, World.Ticks(5));
            Assert.IsFalse(player.IsDead);
            Assert.AreEqual(player.Stats.MaxHealth, player.Health);
            Assert.AreEqual(world.Spawn, player.Position);
            Assert.Greater(player.Corrections, corrections);
        }

        [Test]
        public void MovesAreAcceptedUnlessTooFarOrIntoAWall()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var start = player.Position;

            var step = PlayerMotor.Step(world.Map, start, new Vec2(1, 0), player.Stats.MoveSpeed);
            Assert.IsTrue(world.MovePlayer(player.Id, step, new Vec2(1, 0), 1));
            Assert.AreEqual(step, player.Position);

            Assert.IsFalse(world.MovePlayer(player.Id, step + new Vec2(3, 0), new Vec2(1, 0), 1));
            Assert.AreEqual(step, player.Position);
            Assert.AreEqual(1, player.Corrections);

            // Five ticks late, a longer step is fine.
            var far = step + new Vec2(player.Stats.MoveSpeed * 5 * World.TickSeconds, 0);
            Assert.IsTrue(world.MovePlayer(player.Id, far, new Vec2(1, 0), 5));

            // Close enough to reach, but overlapping the wall at x = 4 of a 5-tile room.
            var small = Room(size: 5);
            var other = small.AddPlayer("b", null);
            Assert.IsFalse(small.MovePlayer(other.Id, new Vec2(3.8f, 2.5f), new Vec2(1, 0), 10));
            Assert.IsTrue(small.MovePlayer(other.Id, new Vec2(3.6f, 2.5f), new Vec2(1, 0), 10));
        }

        [Test]
        public void GearSetsThePlayersNumbers()
        {
            var world = Room();
            var player = world.AddPlayer("a", Gear.Wearing("copper_bow", "copper_armor"));

            Assert.AreEqual(105, player.Stats.MaxHealth, 0.001f);
            Assert.AreEqual(105, player.Health, 0.001f);
            Assert.AreEqual(6, player.Stats.Armor, 0.001f);
            Assert.AreEqual(3, player.Stats.Damage, 0.001f);
            Assert.AreEqual(2.1f, player.Stats.AttacksPerSecond, 0.001f);
            Assert.AreEqual(SkillTag.Ranged, player.Stats.AttackTags);

            player.Equip(Gear.Plain("iron_armor"));
            Assert.AreEqual(112, player.Stats.MaxHealth, 0.001f);
            Assert.AreEqual(112, player.Health, 0.001f, "keeps the same share of health");
        }

        [Test]
        public void TheSpawnerFillsTheCavesAwayFromTheSpawn()
        {
            var generated = WorldGenerator.Generate(5);
            var config = new WorldConfig { EnemyTarget = 10 };
            var world = new World(generated.Map, generated.Spawn, Monsters.Shipped, 5, config);

            Assert.AreEqual(10, world.Enemies.Count);
            Assert.IsTrue(world.Enemies.All(e => Vec2.Distance(e.Position, generated.Spawn) >= config.SpawnDistance));
            Assert.IsTrue(world.Enemies.All(e => !Collision.Overlaps(generated.Map, e.Position, e.Definition.Radius)));
        }

        [Test]
        public void SameSeedAndInputsPlayOutTheSame()
        {
            string Play()
            {
                var generated = WorldGenerator.Generate(9);
                var world = new World(generated.Map, generated.Spawn, Monsters.Shipped, 9, new WorldConfig { EnemyTarget = 30, SpawnDistance = 4 });
                var player = world.AddPlayer("a", Gear.Wearing("copper_sword"));
                world.SetAttacking(player.Id, true);
                for (int tick = 0; tick < 600; tick++)
                {
                    var input = Vec2.FromAngle(tick / 40f);
                    world.MovePlayer(player.Id, PlayerMotor.Step(world.Map, player.Position, input, player.Stats.MoveSpeed), input, 1);
                    world.Step();
                }
                return string.Join(";", world.Enemies.Select(e => $"{e.Id}:{e.Position}:{e.Health}:{e.State}")) + $"|{player.Health}";
            }

            Assert.AreEqual(Play(), Play());
        }

        [Test]
        public void ShippedMonsterSheetIsValid()
        {
            Assert.IsTrue(Monsters.Shipped.Success, string.Join("\n", Monsters.Shipped.Errors));
            Assert.IsTrue(Monsters.Shipped.Monsters.Any(m => m.Level == 1));
        }

        [Test]
        public void MonsterSheetReportsBadRows()
        {
            var data = MonsterSheet.Parse(Monsters.Header + "big,거인,1,10,1,0,1,0.8,5,0.5,0.5,1,1\n");

            Assert.IsFalse(data.Success);
            StringAssert.Contains("monsters.csv 2행", data.Errors.Single());
            StringAssert.Contains("radius", data.Errors.Single());
        }
    }
}
