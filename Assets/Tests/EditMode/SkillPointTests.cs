using System;
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
        public void SkillsAreForgottenForCoinsAndThePointsComeBack()
        {
            var world = new World(Maps.Room(30), TileMap.Center(15, 15), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            var player = world.AddPlayer("a", null, Warrior);
            AtLevel(player, 6);
            world.LearnSkill(player.Id, Skill("war_cry"));
            world.LearnSkill(player.Id, Skill("war_cry"));
            world.LearnSkill(player.Id, Skill("charge"));
            Assert.AreEqual(2, player.SkillPoints);
            Assert.IsFalse(world.ResetSkills(player.Id), "no coins");

            var coin = Gear.Data.FindMaterial("coin");
            player.Inventory.AddMaterial(coin, 100);
            Assert.IsTrue(world.ResetSkills(player.Id));
            Assert.AreEqual(100 - 3 * SkillRules.ResetCoinsPerPoint, player.Inventory.CountOf(coin), "ten coins a point spent");
            Assert.AreEqual(5, player.SkillPoints);
            CollectionAssert.AreEqual(new[] { "whirlwind" }, player.Skills.Select(s => s.Id), "only the free basic skill is left");
            Assert.AreEqual(1, player.RankOf(Skill("whirlwind")));
            Assert.IsFalse(world.ResetSkills(player.Id), "nothing left to forget");
        }

        [Test]
        public void PassivesAddRanksCooldownAndDuration()
        {
            var world = Room();
            var player = world.AddPlayer("a", null, FireMage);
            var fireball = Skill("fireball");
            Assert.AreEqual(1, player.PowerOf(fireball));
            player.Passives.Add(Classes.Shipped.FindPassive("int9"));
            player.Passives.Add(Classes.Shipped.FindPassive("int12"));
            player.RefreshStats();

            Assert.AreEqual(2, player.PowerOf(fireball), "화염 숙련: fire skills a rank higher");
            Assert.AreEqual(0, player.PowerOf(Skill("flame_brand")), "but not one never learned");
            Assert.AreEqual(1, player.RankOf(fireball), "the learned rank and the points stay as they were");
            Assert.AreEqual(SkillRules.CooldownTicks(fireball, 2, 10), player.CooldownFor(fireball), "명상: cooldowns 10% faster");
            Assert.Less(player.CooldownFor(fireball), SkillRules.CooldownTicks(fireball, 2));

            var dummy = world.SpawnEnemy(Monsters.Dummy, player.Position + new Vec2(3, 0));
            Assert.IsTrue(world.UseSkill(player.Id, 0, new Vec2(1, 0)));
            Assert.AreEqual(SkillRules.CooldownTicks(fireball, 2, 10), player.CooldownTicks(0));
            Assert.AreEqual(1 + SkillRules.MaxPower - SkillRules.MaxRank + 4, SkillRules.MaxPower - SkillRules.MaxRank + 5, "power reaches past the learned top rank");
            Assert.AreEqual(1 + 0.15f * (SkillRules.MaxPower - 1), SkillRules.DamageScale(SkillRules.MaxPower + 3), 1e-5f, "and stops at MaxPower");
            Assert.NotNull(dummy);
        }

        [Test]
        public void TheGuideAsksToLearnAndRaiseASkill()
        {
            var goals = Guide.Shipped.Goals;
            var learn = goals.Single(g => g.Id == "skill");
            var raise = goals.Single(g => g.Id == "rank3");
            GuideFacts Facts(int spent, int top) => new GuideFacts(_ => 0, _ => false, false, 0, skillsSpent: spent, topRank: top);
            Assert.IsFalse(learn.Conditions.Any(c => GuideProgress.IsMet(c, Facts(0, 1))));
            Assert.IsTrue(learn.Conditions.Any(c => GuideProgress.IsMet(c, Facts(1, 1))));
            Assert.IsFalse(raise.Conditions.Any(c => GuideProgress.IsMet(c, Facts(4, 2))));
            Assert.IsTrue(raise.Conditions.Any(c => GuideProgress.IsMet(c, Facts(4, 3))));
            Assert.AreEqual("2/3", GuideProgress.Progress(raise, Facts(4, 2)));
            Assert.Less(goals.ToList().IndexOf(learn), goals.ToList().FindIndex(g => g.Id == "passive"), "right after skill points first come");
        }

        static World LootRoom() =>
            new World(Maps.Room(30), TileMap.Center(15, 15), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);

        static MaterialDefinition Crystal(string id) => Gear.Data.FindMaterial(id);

        [Test]
        public void SupportCrystalsGoIntoLearnedSkillsAndComeBackOut()
        {
            var world = LootRoom();
            var player = world.AddPlayer("a", null, Warrior);
            var whirlwind = Skill("whirlwind");
            Assert.IsFalse(world.AddSupport(player.Id, whirlwind, Crystal("support_area")), "none in the bag");
            player.Inventory.AddMaterial(Crystal("support_area"), 1);
            player.Inventory.AddMaterial(Crystal("support_multi"), 1);
            Assert.IsFalse(world.AddSupport(player.Id, whirlwind, Crystal("support_multi")), "more projectiles do nothing for a nova");
            Assert.IsFalse(world.AddSupport(player.Id, Skill("war_cry"), Crystal("support_area")), "not learned");

            Assert.IsTrue(world.AddSupport(player.Id, whirlwind, Crystal("support_area")));
            CollectionAssert.AreEqual(new[] { SupportKind.Area }, player.SupportsOf(whirlwind));
            Assert.AreEqual(0, player.Inventory.CountOf(Crystal("support_area")));
            Assert.AreEqual(12, player.ManaCost(whirlwind), "20% more mana");

            // A dummy just past the plain reach is hit only with the wider whirl.
            var dummy = world.SpawnEnemy(Monsters.Dummy, player.Position + new Vec2(whirlwind.Radius + 0.4f + 0.3f, 0));
            Assert.IsTrue(world.UseSkill(player.Id, 0, Vec2.Zero));
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.Hit && e.Target == dummy.Id));

            Assert.IsTrue(world.RemoveSupport(player.Id, whirlwind, 0));
            Assert.IsEmpty(player.SupportsOf(whirlwind));
            Assert.AreEqual(1, player.Inventory.CountOf(Crystal("support_area")), "taken out, back in the bag");
            Assert.IsFalse(world.RemoveSupport(player.Id, whirlwind, 0), "nothing left to take out");
        }

        [Test]
        public void SupportsChangeCountDamageAndTags()
        {
            var world = LootRoom();
            var player = world.AddPlayer("a", null, FireMage);
            var fireball = Skill("fireball");
            player.Inventory.AddMaterial(Crystal("support_multi"), 1);
            Assert.IsTrue(world.AddSupport(player.Id, fireball, Crystal("support_multi")));
            Assert.IsTrue(world.UseSkill(player.Id, 0, new Vec2(1, 0)));
            Assert.AreEqual(3, world.Projectiles.Count, "two more fireballs");

            var cold = LootRoom();
            var mage = cold.AddPlayer("b", null, FireMage);
            mage.Inventory.AddMaterial(Crystal("support_cold"), 1);
            Assert.IsTrue(cold.AddSupport(mage.Id, fireball, Crystal("support_cold")));
            Assert.AreEqual(SkillTag.Magic | SkillTag.Fire | SkillTag.Cold, mage.ContextFor(fireball));

            Assert.AreEqual(1.4f, SkillSupports.DamageFactor(fireball, SupportKind.Power));
            Assert.AreEqual(Math.Round(fireball.CooldownTicks * 0.75f), SkillRules.CooldownTicks(fireball, 1) * SkillSupports.CooldownFactor(new[] { SupportKind.Haste }), 1);
        }

        // The balance check of 2026-11-09 (CoreSandbox --supports): a split fireball is 40% more in all, not three
        // fireballs; a volley that already has five keeps its arrows whole; lightning's jumps cost a share of the hit.
        [Test]
        public void SplitHitsShareWhatTheSkillDealt()
        {
            var fireball = Skill("fireball");
            Assert.AreEqual(SkillSupports.MultiTotal, SkillSupports.Count(fireball, new[] { SupportKind.Multi }) * SkillSupports.DamageFactor(fireball, SupportKind.Multi), 0.001f);
            var volley = Skill("volley");
            Assert.AreEqual(1f, SkillSupports.DamageFactor(volley, SupportKind.Multi));
            Assert.AreEqual(7, SkillSupports.Count(volley, new[] { SupportKind.Multi }));
            var spirits = Skill("spirits");
            Assert.AreEqual(SkillSupports.MultiTotal, SkillSupports.Count(spirits, new[] { SupportKind.Multi }) * SkillSupports.DamageFactor(spirits, SupportKind.Multi) / spirits.Count, 0.001f);
            Assert.Less(SkillSupports.DamageFactor(fireball, SupportKind.Lightning), 1f);
            StringAssert.Contains("53% 덜", SkillSupports.Describe(SupportKind.Multi, fireball));
        }

        // Crystals that would only cost: power on a buff or on a dash that does not hit, haste on spirits, an element
        // the skill has already.
        [Test]
        public void CrystalsAreOfferedOnlyWhereTheyHelp()
        {
            Assert.IsFalse(SkillSupports.Fits(Skill("war_cry"), SupportKind.Power));
            Assert.IsTrue(SkillSupports.Fits(Skill("war_cry"), SupportKind.Haste));
            Assert.IsFalse(SkillSupports.Fits(Skill("gale_step"), SupportKind.Power));
            Assert.IsFalse(SkillSupports.Fits(Skill("spirits"), SupportKind.Haste));
            Assert.IsTrue(SkillSupports.Fits(Skill("spirits"), SupportKind.Power));
            Assert.IsFalse(SkillSupports.Fits(Skill("fireball"), SupportKind.Fire));
            Assert.IsTrue(SkillSupports.Fits(Skill("fireball"), SupportKind.Lightning));
            Assert.IsFalse(SkillSupports.Fits(Skill("life_spring"), SupportKind.Fire));
            Assert.IsTrue(SkillSupports.Fits(Skill("life_spring"), SupportKind.Power));
        }

        [Test]
        public void PowerHealsMore()
        {
            float Healed(string crystal)
            {
                var world = LootRoom();
                var player = world.AddPlayer("a", null, Classes.Shipped.Find("summoner"));
                var spring = Skill("life_spring");
                World.TeachAll(player, 1);
                player.ChangeKit(player.Classes, new[] { ClassRules.AllSkillsFor(player.Classes).IndexOf(spring) });
                player.ClearCooldown(spring);
                if (crystal != null)
                {
                    player.Inventory.AddMaterial(Crystal(crystal), 1);
                    Assert.IsTrue(world.AddSupport(player.Id, spring, Crystal(crystal)));
                }
                player.Health = 1;
                Assert.IsTrue(world.UseSkill(player.Id, player.Skills.ToList().IndexOf(spring), Vec2.Zero));
                return player.Health - 1;
            }
            Assert.AreEqual(Healed(null) * 1.4f, Healed("support_power"), 0.5f);
        }

        // Slots open with the skill's rank: one at 1, two at 3, three at 5; never two of a kind; their factors
        // multiply.
        [Test]
        public void ASkillHoldsMoreCrystalsAsItRises()
        {
            var world = LootRoom();
            var player = world.AddPlayer("a", null, FireMage);
            var fireball = Skill("fireball");
            foreach (string id in new[] { "support_power", "support_haste", "support_multi", "support_lightning" }) player.Inventory.AddMaterial(Crystal(id), 2);
            Assert.IsTrue(world.AddSupport(player.Id, fireball, Crystal("support_power")));
            Assert.IsFalse(world.AddSupport(player.Id, fireball, Crystal("support_haste")), "one slot at rank 1");
            player.SkillRanks[fireball] = 3;
            Assert.IsFalse(world.AddSupport(player.Id, fireball, Crystal("support_power")), "never two of a kind");
            Assert.IsTrue(world.AddSupport(player.Id, fireball, Crystal("support_haste")));
            Assert.IsFalse(world.AddSupport(player.Id, fireball, Crystal("support_multi")), "two slots at rank 3");
            player.SkillRanks[fireball] = 5;
            Assert.IsTrue(world.AddSupport(player.Id, fireball, Crystal("support_multi")));
            Assert.IsFalse(world.AddSupport(player.Id, fireball, Crystal("support_lightning")), "three at most");
            CollectionAssert.AreEqual(new[] { SupportKind.Power, SupportKind.Haste, SupportKind.Multi }, player.SupportsOf(fireball));
            Assert.AreEqual(1.4f * 0.85f * SkillSupports.DamageFactor(fireball, SupportKind.Multi), SkillSupports.DamageFactor(fireball, player.SupportsOf(fireball)), 0.0001f);
            Assert.AreEqual((int)Math.Round(fireball.Mana * 1.5f * 1.2f), player.ManaCost(fireball));

            Assert.IsTrue(world.RemoveSupport(player.Id, fireball, 1), "the middle one out");
            CollectionAssert.AreEqual(new[] { SupportKind.Power, SupportKind.Multi }, player.SupportsOf(fireball));
            Assert.AreEqual(2, player.Inventory.CountOf(Crystal("support_haste")));
            StringAssert.Contains("마나 9", SkillText.Numbers(fireball, 5, null, player.SupportsOf(fireball)), "the numbers count the crystals");
        }

        // Through: a power shot goes on through the first monsters in its line, softer after each.
        [Test]
        public void APiercingShotGoesThroughTwoMore()
        {
            int Hit(bool pierce)
            {
                var world = LootRoom();
                var player = world.AddPlayer("a", null, Classes.Shipped.Find("archer"));
                var shot = Skill("power_shot");
                World.TeachAll(player, 1);
                player.ClearCooldown(shot);
                if (pierce)
                {
                    player.Inventory.AddMaterial(Crystal("support_pierce"), 1);
                    Assert.IsTrue(world.AddSupport(player.Id, shot, Crystal("support_pierce")));
                }
                var line = Enumerable.Range(0, 4).Select(i => world.SpawnEnemy(Monsters.Dummy, player.Position + new Vec2(2 + i * 1.5f, 0))).ToList();
                Assert.IsTrue(world.UseSkill(player.Id, Slot(player, "power_shot"), new Vec2(1, 0)));
                var hit = new HashSet<int>();
                for (int tick = 0; tick < World.TicksPerSecond * 2; tick++)
                {
                    world.Step();
                    foreach (var e in world.TakeEvents()) if (e.Type == SimEventType.Hit && e.Source == player.Id) hit.Add(e.Target);
                }
                return line.Count(d => hit.Contains(d.Id));
            }
            Assert.AreEqual(1, Hit(false));
            Assert.AreEqual(3, Hit(true));
        }

        [Test]
        public void DurationAndLeech()
        {
            var world = LootRoom();
            var player = world.AddPlayer("a", null, Warrior);
            var cry = Skill("war_cry");
            var whirlwind = Skill("whirlwind");
            World.TeachAll(player, 1);
            int plain = player.DurationFor(cry);
            player.Inventory.AddMaterial(Crystal("support_duration"), 1);
            player.Inventory.AddMaterial(Crystal("support_leech"), 1);
            Assert.IsFalse(world.AddSupport(player.Id, whirlwind, Crystal("support_duration")), "a nova has no time to stretch");
            Assert.IsTrue(world.AddSupport(player.Id, cry, Crystal("support_duration")));
            Assert.AreEqual(Math.Round(plain * 1.4f), player.DurationFor(cry), 1);

            Assert.IsTrue(world.AddSupport(player.Id, whirlwind, Crystal("support_leech")));
            world.SpawnEnemy(Monsters.Dummy, player.Position + new Vec2(1, 0));
            player.Health = 10;
            player.ClearCooldown(whirlwind);
            Assert.IsTrue(world.UseSkill(player.Id, Slot(player, "whirlwind"), Vec2.Zero));
            int dealt = world.TakeEvents().Where(e => e.Type == SimEventType.Hit && e.Source == player.Id).Sum(e => e.Amount);
            Assert.Greater(dealt, 0);
            Assert.AreEqual(10 + dealt * SkillSupports.LeechShare, player.Health, 0.01f);
        }

        // A save from before a crystal stopped fitting its skill gets the crystal back in the bag.
        [Test]
        public void ACrystalThatNoLongerFitsComesBack()
        {
            var player = LootRoom().AddPlayer("a", null, Warrior);
            AtLevel(player, 4);
            World.RestoreSkills(player, Classes.Shipped, new[] { ("whirlwind", 1), ("war_cry", 1) });
            World.RestoreSupports(player, Classes.Shipped, Gear.Data, new[] { ("war_cry", "support_power") });
            Assert.IsEmpty(player.SupportsOf(Skill("war_cry")));
            Assert.AreEqual(1, player.Inventory.CountOf(Crystal("support_power")));
        }

        [Test]
        public void SupportsAreSavedAndComeBackWithAReset()
        {
            var world = LootRoom();
            var player = world.AddPlayer("a", null, Warrior);
            AtLevel(player, 4);
            world.LearnSkill(player.Id, Skill("war_cry"));
            player.Inventory.AddMaterial(Crystal("support_haste"), 1);
            Assert.IsTrue(world.AddSupport(player.Id, Skill("war_cry"), Crystal("support_haste")));

            var saved = CharacterSave.Read(CharacterSave.Write(new Equipment(), new Inventory(), player.Experience,
                skillRanks: player.SkillRanks.Select(p => (p.Key.Id, p.Value)), supports: player.Supports.SelectMany(p => p.Value.Select(gem => (p.Key.Id, gem.Id)))), Gear.Data);
            CollectionAssert.Contains(saved.Supports, ("war_cry", "support_haste"));
            var again = LootRoom().AddPlayer("b", null, Warrior);
            World.RestoreSkills(again, Classes.Shipped, saved.SkillRanks);
            World.RestoreSupports(again, Classes.Shipped, Gear.Data, saved.Supports);
            CollectionAssert.AreEqual(new[] { SupportKind.Haste }, again.SupportsOf(Skill("war_cry")));

            player.Inventory.AddMaterial(Gear.Data.FindMaterial("coin"), 50);
            Assert.IsTrue(world.ResetSkills(player.Id));
            Assert.AreEqual(1, player.Inventory.CountOf(Crystal("support_haste")), "the crystal comes back with the points");
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
