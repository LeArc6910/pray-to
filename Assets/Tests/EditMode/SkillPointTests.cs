using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // Skill points and ranks (Docs/Classes.md, 스킬 포인트와 단계), and the dash and heal skills.
    public class SkillPointTests
    {
        static readonly ClassDefinition Warrior = Classes.Shipped.Find("warrior");
        static readonly ClassDefinition FireMage = Classes.Shipped.Find("fire_mage");
        static readonly ClassDefinition Archer = Classes.Shipped.Find("archer");
        static readonly ClassDefinition Summoner = Classes.Shipped.Find("summoner");
        static SkillDefinition Skill(string id) => Classes.Skill(id);

        static World Room(int size = 30) =>
            new World(Maps.Room(size), TileMap.Center(size / 2, size / 2), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });

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

        static void AtLevel(Player player, int level) => player.Experience = Leveling.TotalFor(level);

        static int Slot(Player player, string id) => player.Skills.ToList().FindIndex(s => s.Id == id);

        [Test]
        public void ANewCharacterKnowsOnlyEachClassesFirstSkill()
        {
            var world = Room();
            var player = world.AddPlayer("a", null, Warrior, FireMage);

            CollectionAssert.AreEqual(new[] { "whirlwind", "fireball" }, player.Skills.Select(s => s.Id));
            Assert.AreEqual(1, player.RankOf(Skill("whirlwind")));
            Assert.IsFalse(player.Knows(Skill("war_cry")));
            Assert.AreEqual(0, player.SkillPoints, "level 1 has none; the basic skills cost nothing");
            Assert.IsFalse(world.LearnSkill(player.Id, Skill("war_cry")));
        }

        [Test]
        public void PointsComeWithLevelsAndSkillsWaitForTheirLevel()
        {
            var world = Room();
            var player = world.AddPlayer("a", null, Warrior);
            AtLevel(player, 3);
            Assert.AreEqual(2, player.SkillPoints, "a point a level past the first");

            Assert.IsFalse(world.LearnSkill(player.Id, Skill("charge")), "charge waits for level 5");
            Assert.IsFalse(world.LearnSkill(player.Id, Skill("fireball")), "not one of the player's classes");
            Assert.IsTrue(world.LearnSkill(player.Id, Skill("war_cry")));
            var events = world.TakeEvents();

            Assert.AreEqual(1, player.RankOf(Skill("war_cry")));
            Assert.AreEqual(1, player.SkillPoints);
            CollectionAssert.AreEqual(new[] { "whirlwind", "war_cry" }, player.Skills.Select(s => s.Id), "a learned skill takes a free slot");
            Assert.AreEqual(0, player.CooldownTicks(1), "and is ready at once");
            var learned = events.Single(e => e.Type == SimEventType.SkillLearned);
            Assert.AreEqual(player.Id, learned.Source);
            Assert.AreSame(Skill("war_cry"), Classes.Shipped.Skills[learned.Target - 1]);
            Assert.AreEqual(1, learned.Amount);

            Assert.IsTrue(world.LearnSkill(player.Id, Skill("whirlwind")), "the basic skill can rise too");
            Assert.AreEqual(2, player.RankOf(Skill("whirlwind")));
            Assert.AreEqual(0, player.SkillPoints);
            Assert.IsFalse(world.LearnSkill(player.Id, Skill("war_cry")), "no point left");
        }

        [Test]
        public void RanksStopAtFiveAndSlotsAtFour()
        {
            var world = Room();
            var player = world.AddPlayer("a", null, Warrior);
            AtLevel(player, 30);
            for (int i = 0; i < 6; i++) world.LearnSkill(player.Id, Skill("whirlwind"));
            Assert.AreEqual(SkillRules.MaxRank, player.RankOf(Skill("whirlwind")));

            foreach (string id in new[] { "war_cry", "charge", "ground_slam", "iron_skin" }) Assert.IsTrue(world.LearnSkill(player.Id, Skill(id)), id);
            CollectionAssert.AreEqual(new[] { "whirlwind", "war_cry", "charge", "ground_slam" }, player.Skills.Select(s => s.Id), "the fifth waits outside the slots");
            Assert.IsTrue(player.Knows(Skill("iron_skin")));

            var all = ClassRules.AllSkillsFor(player.Classes);
            Assert.IsTrue(world.ChangeClasses(player.Id, player.Classes, new[] { 4, 0, 1, 2 }));
            Assert.AreEqual("iron_skin", player.Skills[0].Id, "a learned skill can be swapped in");
            Assert.IsTrue(world.ChangeClasses(player.Id, player.Classes, new[] { 5, 0, 1, 2 }));
            Assert.AreNotEqual("blade_storm", player.Skills[0].Id, "one not learned cannot");
            Assert.AreEqual(29 - 4 - 4, player.SkillPoints);
            Assert.AreEqual(6, all.Count);
        }

        [Test]
        public void RanksRaiseDamageAndShortenCooldowns()
        {
            var whirlwind = Skill("whirlwind");
            Assert.AreEqual(1f, SkillRules.DamageScale(1));
            Assert.AreEqual(1.6f, SkillRules.DamageScale(5), 1e-5f);
            Assert.AreEqual(whirlwind.CooldownTicks, SkillRules.CooldownTicks(whirlwind, 1));
            Assert.AreEqual((int)System.Math.Round(whirlwind.CooldownTicks * 0.8f), SkillRules.CooldownTicks(whirlwind, 5));
            var warCry = Skill("war_cry");
            Assert.AreEqual((int)System.Math.Round(warCry.DurationTicks * 1.4f), SkillRules.DurationTicks(warCry, 5));

            int HitWith(int rank)
            {
                var world = Room();
                var player = world.AddPlayer("a", null, Warrior);
                player.SkillRanks[whirlwind] = rank;
                var dummy = world.SpawnEnemy(Monsters.Dummy, player.Position + new Vec2(1, 0));
                world.UseSkill(player.Id, 0, Vec2.Zero);
                Assert.AreEqual(SkillRules.CooldownTicks(whirlwind, rank), player.CooldownTicks(0));
                return world.TakeEvents().Where(e => e.Type == SimEventType.Hit && e.Target == dummy.Id && !e.Critical).Sum(e => e.Amount);
            }
            int low = HitWith(1), high = HitWith(5);
            Assert.Greater(low, 0);
            Assert.AreEqual(low * 1.6f, high, low * 0.1f + 1);
        }

        [Test]
        public void RanksStayThroughOtherClassesAndASave()
        {
            var world = Room();
            var player = world.AddPlayer("a", null, Warrior);
            AtLevel(player, 5);
            world.LearnSkill(player.Id, Skill("war_cry"));
            world.LearnSkill(player.Id, Skill("war_cry"));
            int x = TileMap.Cell(player.Position.X) + 1, y = TileMap.Cell(player.Position.Y);
            world.Map[x, y] = TileType.Workbench;

            Assert.IsTrue(world.ChangeClasses(player.Id, new[] { FireMage }, null));
            CollectionAssert.AreEqual(new[] { "fireball" }, player.Skills.Select(s => s.Id));
            Assert.AreEqual(2, player.RankOf(Skill("war_cry")), "the warrior's ranks wait for the warrior");
            Assert.AreEqual(2, player.SkillPoints, "and still count as spent");
            Assert.IsTrue(world.ChangeClasses(player.Id, new[] { Warrior }, null));
            CollectionAssert.AreEqual(new[] { "whirlwind", "war_cry" }, player.Skills.Select(s => s.Id));

            var ranks = player.SkillRanks.Select(p => (p.Key.Id, p.Value)).Append(("gone_skill", 3));
            var saved = CharacterSave.Read(CharacterSave.Write(new Equipment(), new Inventory(), player.Experience, skillRanks: ranks), Gear.Data);
            CollectionAssert.Contains(saved.SkillRanks, ("war_cry", 2));

            var again = Room().AddPlayer("b", null, Warrior);
            again.Experience = saved.Experience;
            World.RestoreSkills(again, Classes.Shipped, saved.SkillRanks);
            Assert.AreEqual(2, again.RankOf(Skill("war_cry")));
            Assert.AreEqual(1, again.RankOf(Skill("whirlwind")));
            Assert.AreEqual(2, again.SkillPoints, "the unknown skill is dropped");
            CollectionAssert.AreEqual(new[] { "whirlwind", "war_cry" }, again.Skills.Select(s => s.Id));
        }

        [Test]
        public void AnOldSaveHasAllItsPointsToSpend()
        {
            var old = CharacterSave.Read(CharacterSave.Write(new Equipment(), new Inventory(), Leveling.TotalFor(6)), Gear.Data);
            var player = Room().AddPlayer("a", null, Archer);
            player.Experience = old.Experience;
            World.RestoreSkills(player, Classes.Shipped, old.SkillRanks);

            CollectionAssert.AreEqual(new[] { "volley" }, player.Skills.Select(s => s.Id));
            Assert.AreEqual(5, player.SkillPoints);
        }

        [Test]
        public void AChargeRushesAheadAndStrikesWhatItPasses()
        {
            var world = Room();
            var player = world.AddPlayer("a", null, Warrior);
            player.SkillRanks[Skill("charge")] = 1;
            world.ChangeClasses(player.Id, player.Classes, new[] { 0, 2 });
            player.ClearCooldown(Skill("charge"));
            var start = player.Position;
            var dummy = world.SpawnEnemy(Monsters.Dummy, start + new Vec2(2.5f, 0));

            Assert.IsTrue(world.UseSkill(player.Id, Slot(player, "charge"), new Vec2(1, 0)));
            var events = world.TakeEvents();

            Assert.AreEqual(start.X + Skill("charge").Range, player.Position.X, 0.3f, "it went its range");
            Assert.AreEqual(start.Y, player.Position.Y, 1e-3f);
            Assert.AreEqual(1, events.Count(e => e.Type == SimEventType.Hit && e.Target == dummy.Id), "struck once on the way");
        }

        [Test]
        public void AChargeStopsAtAWall()
        {
            var world = Room(12);
            var player = world.AddPlayer("a", null, Archer);
            player.SkillRanks[Skill("gale_step")] = 1;
            world.ChangeClasses(player.Id, player.Classes, new[] { 0, 2 });
            player.ClearCooldown(Skill("gale_step"));
            int wallX = TileMap.Cell(player.Position.X) + 2;
            for (int y = 0; y < 12; y++) world.Map[wallX, y] = TileType.Wall;

            Assert.IsTrue(world.UseSkill(player.Id, Slot(player, "gale_step"), new Vec2(1, 0)));
            Assert.Less(player.Position.X, wallX, "the wall stops it");
            Assert.Greater(player.Position.X, TileMap.Center(wallX - 2, 0).X + 0.5f, "after moving up to it");
            Assert.IsFalse(world.TakeEvents().Any(e => e.Type == SimEventType.Hit), "gale step strikes nothing");
        }

        [Test]
        public void LifeSpringHealsAShareOfMostHealth()
        {
            var world = Room();
            var player = world.AddPlayer("a", null, Summoner);
            player.SkillRanks[Skill("life_spring")] = 3;
            world.ChangeClasses(player.Id, player.Classes, new[] { 0, 2 });
            player.ClearCooldown(Skill("life_spring"));
            player.Health = 10;

            Assert.IsTrue(world.UseSkill(player.Id, Slot(player, "life_spring"), Vec2.Zero));
            float expected = 10 + player.Stats.MaxHealth * 0.30f * SkillRules.DamageScale(3);
            Assert.AreEqual(expected, player.Health, 1.01f);
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.Healed && e.Source == player.Id));

            player.Health = player.Stats.MaxHealth - 1;
            for (int i = 0; i < 40 * World.TicksPerSecond; i++) world.Step();
            world.UseSkill(player.Id, Slot(player, "life_spring"), Vec2.Zero);
            Assert.AreEqual(player.Stats.MaxHealth, player.Health, 1e-3f, "never past the most");
        }

        [Test]
        public void EveryShippedSkillCanBeLearnedAndUsed()
        {
            foreach (var owner in Classes.Shipped.Classes)
            {
                var world = Room();
                var player = world.AddPlayer("a", null, owner);
                world.SpawnEnemy(Monsters.Dummy, player.Position + new Vec2(1.5f, 0));
                AtLevel(player, 18);
                foreach (var skill in owner.Skills.Skip(1)) Assert.IsTrue(world.LearnSkill(player.Id, skill), skill.Id);
                Assert.AreEqual(17 - 5, player.SkillPoints);
                var all = ClassRules.AllSkillsFor(player.Classes);
                for (int first = 0; first < all.Count; first += 4)
                {
                    var order = Enumerable.Range(0, 4).Select(i => (first + i) % all.Count).ToArray();
                    Assert.IsTrue(world.ChangeClasses(player.Id, player.Classes, order));
                    for (int slot = 0; slot < player.Skills.Count; slot++)
                    {
                        player.ClearCooldown(player.Skills[slot]);
                        player.Mana = player.Stats.MaxMana;
                        Assert.IsTrue(world.UseSkill(player.Id, slot, new Vec2(1, 0)), player.Skills[slot].Id);
                        Run(world, 2);
                    }
                }
            }
        }
    }
}
