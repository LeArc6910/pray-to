using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class GuideTests
    {
        // A character with a class, as one is a minute in (the first goal, 2026-11-09).
        static GuideFacts Facts(Dictionary<string, int> counts = null, TileType[] tiles = null, bool crop = false, float distance = 0, int classes = 1) =>
            new GuideFacts(
                id => counts != null && counts.TryGetValue(id, out int count) ? count : 0,
                type => tiles != null && tiles.Contains(type),
                crop,
                distance,
                classes: classes);

        static Goal Shipped(string id) => Guide.Shipped.Goals.Single(goal => goal.Id == id);

        [Test]
        public void ShippedGoalsStartWithDiggingAndBuilding()
        {
            var data = Guide.Shipped;
            Assert.IsTrue(data.Success, string.Join("\n", data.Errors));
            Assert.AreEqual("class", data.Goals[0].Id, "a class first, picked in the game (2026-11-09)");
            Assert.AreEqual("stone", data.Goals[1].Id);
            Assert.AreEqual("workbench", data.Goals[2].Id);
            Assert.IsTrue(data.Goals.All(goal => goal.Hint.Length > 0), "every goal says how");
            var copperGear = data.Goals.Single(goal => goal.Id == "copper_gear");
            Assert.Greater(copperGear.Conditions.Count, 1, "any of several items");
        }

        [Test]
        public void TheGoalListShowsHowFarAlong()
        {
            var stone = Shipped("stone");
            var count = stone.Conditions[0].Count;
            Assert.AreEqual($"3/{count}", GuideProgress.Progress(stone, Facts(new Dictionary<string, int> { [stone.Conditions[0].Id] = 3 })));
            Assert.AreEqual($"{count}/{count}", GuideProgress.Progress(stone, Facts(new Dictionary<string, int> { [stone.Conditions[0].Id] = 99 })), "never past the count");
            Assert.IsNull(GuideProgress.Progress(Shipped("workbench"), Facts()), "a workbench to place has nothing to count");
        }

        [Test]
        public void GoalsPassInOrderAndSkipWhatIsAlreadyDone()
        {
            var progress = new GuideProgress(Guide.Shipped, 0);
            Assert.AreEqual("class", progress.Current.Id);
            Assert.IsEmpty(progress.Update(Facts(classes: 0)), "no class yet");
            CollectionAssert.AreEqual(new[] { "class" }, progress.Update(Facts()).Select(goal => goal.Id));
            Assert.AreEqual("stone", progress.Current.Id);

            var passed = progress.Update(Facts(new Dictionary<string, int> { ["stone"] = 12 }, new[] { TileType.Workbench, TileType.Torch }));

            CollectionAssert.AreEqual(new[] { "stone", "workbench", "torch" }, passed.Select(goal => goal.Id));
            Assert.AreEqual("floor", progress.Current.Id, "laying a floor comes next");
        }

        [Test]
        public void APassedGoalStaysDone()
        {
            var progress = new GuideProgress(Guide.Shipped, 0);
            progress.Update(Facts(new Dictionary<string, int> { ["stone"] = 10 }));
            Assert.AreEqual(2, progress.Done, "the class and the stones");

            progress.Update(Facts());

            Assert.AreEqual(2, progress.Done, "the stones went into a workbench");
        }

        [Test]
        public void SavedProgressIsKeptInRange()
        {
            Assert.AreEqual(0, new GuideProgress(Guide.Shipped, -3).Done);
            var finished = new GuideProgress(Guide.Shipped, 999);
            Assert.AreEqual(Guide.Shipped.Goals.Count, finished.Done);
            Assert.IsNull(finished.Current);
        }

        [Test]
        public void EveryKindOfConditionIsChecked()
        {
            var data = GuideSheet.Parse(
                "id,text,hint,done\n" +
                "a,가,힌트,have:copper_ore:3\n" +
                "b,나,힌트,place:Chest\n" +
                "c,다,힌트,plant\n" +
                "d,라,힌트,reach:40\n" +
                "e,마,힌트,have:iron_sword|have:iron_bow\n",
                Gear.Data);
            Assert.IsTrue(data.Success, string.Join("\n", data.Errors));
            var progress = new GuideProgress(data, 0);

            progress.Update(Facts(new Dictionary<string, int> { ["copper_ore"] = 2 }));
            Assert.AreEqual("a", progress.Current.Id, "two of three");
            progress.Update(Facts(new Dictionary<string, int> { ["copper_ore"] = 3 }, new[] { TileType.Chest }, crop: true, distance: 39));
            Assert.AreEqual("d", progress.Current.Id, "39 tiles is not 40");
            progress.Update(Facts(new Dictionary<string, int> { ["iron_bow"] = 1 }, distance: 40));
            Assert.IsNull(progress.Current);
        }

        [Test]
        public void PassivesLifeSkillsRiftsAndFriendsAreChecked()
        {
            var data = GuideSheet.Parse(
                "id,text,hint,done\n" +
                "a,가,힌트,passive:2\n" +
                "b,나,힌트,life:fishing:3\n" +
                "c,다,힌트,rift:2\n" +
                "d,라,힌트,friend:1\n",
                Gear.Data);
            Assert.IsTrue(data.Success, string.Join("\n", data.Errors));
            var progress = new GuideProgress(data, 0);
            GuideFacts With(int passives = 0, int fishing = 1, int rift = 1, int hearts = 0) =>
                new GuideFacts(_ => 0, _ => false, false, 0, 1, passives, skill => skill == LifeSkill.Fishing ? fishing : 1, rift, hearts);

            progress.Update(With(passives: 1));
            Assert.AreEqual("a", progress.Current.Id);
            progress.Update(With(passives: 2, fishing: 3));
            Assert.AreEqual("c", progress.Current.Id);
            progress.Update(With(passives: 2, fishing: 3, rift: 2, hearts: 1));
            Assert.IsNull(progress.Current);

            var bad = GuideSheet.Parse("id,text,hint,done\na,가,힌트,life:dancing:2\nb,나,힌트,friend:0\n", Gear.Data);
            Assert.AreEqual(2, bad.Errors.Count, string.Join("\n", bad.Errors));
        }

        [Test]
        public void ProgressIsKeptByIdSoNewGoalsComeUpInTheirPlace()
        {
            var data = Guide.Shipped;
            // Someone who had passed the first nine goals before new ones went in among them.
            var progress = new GuideProgress(data, GuideProgress.FromCount(data, 9));
            Assert.AreEqual(9, progress.Passed.Count);
            Assert.IsFalse(progress.Passed.Any(GuideProgress.AddedAfterCounts.Contains), "none of the new ones");
            Assert.AreEqual("class", progress.Current.Id, "the first new goal before where they were: picking a class, which a character from then has");
            Assert.IsTrue(GuideProgress.AddedAfterCounts.All(id => data.Goals.Any(g => g.Id == id)), "every new goal is in the sheet");
        }

        [Test]
        public void SheetErrorsNameTheRow()
        {
            var data = GuideSheet.Parse(
                "id,text,hint,done\n" +
                "a,가,힌트,have:gold\n" +
                "b,나,힌트,place:Castle\n" +
                "c,,힌트,plant\n" +
                "d,라,힌트,fly\n" +
                "e,마,힌트,plant\n" +
                "e,바,힌트,plant\n",
                Gear.Data);

            Assert.AreEqual(5, data.Errors.Count, string.Join("\n", data.Errors));
            StringAssert.Contains("2행", data.Errors[0]);
            StringAssert.Contains("gold", data.Errors[0]);
            StringAssert.Contains("Castle", data.Errors[1]);
            StringAssert.Contains("text", data.Errors[2]);
            StringAssert.Contains("fly", data.Errors[3]);
            StringAssert.Contains("7행", data.Errors[4]);
            StringAssert.Contains("이미", data.Errors[4]);
        }
            [Test]
        public void GoalsGiveTheirRewardOnceAndOnlyWhenMet()
        {
            var stone = Guide.Shipped.Goals.Single(g => g.Id == "stone");
            Assert.AreEqual("회복 물약 1", stone.RewardText);
            var world = new World(Maps.Room(12), TileMap.Center(6, 6), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            var player = world.AddPlayer("a", null);
            var potion = Gear.Data.Consumables.Single(c => c.Id == "healing_potion");
            int before = player.Inventory.CountOf(potion);

            Assert.IsFalse(world.ClaimGoal(player.Id, stone, 0), "not met yet");
            player.Inventory.AddMaterial(Gear.Data.FindMaterial("stone"), 10);
            Assert.IsTrue(world.ClaimGoal(player.Id, stone, 0));
            Assert.AreEqual(before + 1, player.Inventory.CountOf(potion));
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.GoalReward && e.Source == player.Id && e.Amount == 0));
            Assert.IsFalse(world.ClaimGoal(player.Id, stone, 0), "once");
            Assert.AreEqual(before + 1, player.Inventory.CountOf(potion));

            // Experience rewards and the level condition.
            var level5 = Guide.Shipped.Goals.Single(g => g.Id == "level5");
            Assert.IsFalse(world.ClaimGoal(player.Id, level5, 1));
            world.GainExperience(player, Leveling.TotalFor(5));
            Assert.IsTrue(world.ClaimGoal(player.Id, level5, 1));
            var copper = Guide.Shipped.Goals.Single(g => g.Id == "copper");
            Assert.IsTrue(copper.Rewards.Single().IsExperience);
            player.Inventory.AddMaterial(Gear.Data.FindMaterial("copper_ore"), 6);
            int xp = player.Experience;
            Assert.IsTrue(world.ClaimGoal(player.Id, copper, 2));
            Assert.AreEqual(xp + 20, player.Experience);

            // Claims ride in the character save.
            var saved = CharacterSave.Read(CharacterSave.Write(player.Equipment, player.Inventory, player.Experience, player.ClaimedGoals), Gear.Data);
            CollectionAssert.AreEquivalent(new[] { "stone", "level5", "copper" }, saved.ClaimedGoals);
        }

        [Test]
        public void EveryGoalHasARewardAndTheNewContentIsInTheGuide()
        {
            Assert.IsTrue(Guide.Shipped.Success, string.Join("\n", Guide.Shipped.Errors));
            Assert.IsTrue(Guide.Shipped.Goals.All(g => g.Rewards.Count > 0));
            foreach (string id in new[] { "floor", "hoe", "fishing", "bed", "sprinkler", "generator", "void_lord" })
            {
                Assert.IsTrue(Guide.Shipped.Goals.Any(g => g.Id == id), id);
            }
            var broken = GuideSheet.Parse("id,text,hint,done,reward\na,가,,plant,nothing:1\nb,나,,level:99,xp:5\n", Gear.Data);
            Assert.IsTrue(broken.Errors.Any(e => e.Contains("nothing")), string.Join("\n", broken.Errors));
            Assert.IsTrue(broken.Errors.Any(e => e.Contains("레벨")), string.Join("\n", broken.Errors));
        }
}
}
