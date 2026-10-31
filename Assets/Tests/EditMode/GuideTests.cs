using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class GuideTests
    {
        static GuideFacts Facts(Dictionary<string, int> counts = null, TileType[] tiles = null, bool crop = false, float distance = 0) =>
            new GuideFacts(
                id => counts != null && counts.TryGetValue(id, out int count) ? count : 0,
                type => tiles != null && tiles.Contains(type),
                crop,
                distance);

        [Test]
        public void ShippedGoalsStartWithDiggingAndBuilding()
        {
            var data = Guide.Shipped;
            Assert.IsTrue(data.Success, string.Join("\n", data.Errors));
            Assert.AreEqual("stone", data.Goals[0].Id);
            Assert.AreEqual("workbench", data.Goals[1].Id);
            Assert.IsTrue(data.Goals.All(goal => goal.Hint.Length > 0), "every goal says how");
            var copperGear = data.Goals.Single(goal => goal.Id == "copper_gear");
            Assert.Greater(copperGear.Conditions.Count, 1, "any of several items");
        }

        [Test]
        public void GoalsPassInOrderAndSkipWhatIsAlreadyDone()
        {
            var progress = new GuideProgress(Guide.Shipped, 0);
            Assert.AreEqual("stone", progress.Current.Id);
            Assert.IsEmpty(progress.Update(Facts()));

            var passed = progress.Update(Facts(new Dictionary<string, int> { ["stone"] = 12 }, new[] { TileType.Workbench, TileType.Torch }));

            CollectionAssert.AreEqual(new[] { "stone", "workbench", "torch" }, passed.Select(goal => goal.Id));
            Assert.AreEqual("copper", progress.Current.Id);
        }

        [Test]
        public void APassedGoalStaysDone()
        {
            var progress = new GuideProgress(Guide.Shipped, 0);
            progress.Update(Facts(new Dictionary<string, int> { ["stone"] = 10 }));
            Assert.AreEqual(1, progress.Done);

            progress.Update(Facts());

            Assert.AreEqual(1, progress.Done, "the stones went into a workbench");
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
    }
}
