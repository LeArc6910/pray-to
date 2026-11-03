using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The passive tree (World.Passives.cs, Docs/Classes.md 패시브 트리).
    public class PassiveTests
    {
        static readonly ClassData Data = Classes.Shipped;
        static PassiveNode Point(string id) => Data.FindPassive(id);

        static World Room(out Player player, int level)
        {
            var world = new World(Maps.Room(10), TileMap.Center(5, 5), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            player = world.AddPlayer("a", Gear.Wearing("copper_sword"));
            world.GainExperience(player, Leveling.TotalFor(level));
            return world;
        }

        [Test]
        public void TheShippedTreeHangsTogetherFromOneStart()
        {
            Assert.IsTrue(Data.Success, string.Join("\n", Data.Errors));
            Assert.That(Data.Passives.Count, Is.GreaterThanOrEqualTo(30));
            Assert.AreSame(Point("start"), Data.PassiveStart);
            Assert.IsTrue(Data.Passives.Where(p => !p.IsStart).All(p => p.Modifiers.Count > 0), "every point gives something");
            Assert.That(Data.Passives.Count(p => p.Notable), Is.GreaterThanOrEqualTo(8));
            CollectionAssert.AreEquivalent(new[] { "str1", "dex1", "int1", "sum1" }, Point("start").Links.Select(p => p.Id));
        }

        [Test]
        public void SheetErrorsNameTheRow()
        {
            var data = ClassSheet.Parse(Classes.ClassesHeader, Classes.SkillsHeader,
                "id,name,x,y,links,stats,kind\n" +
                "s,시작,0,0,,,start\n" +
                "a,가,1,0,s|nowhere,Damage+5%,\n" +
                "b,나,1,0,s,Damage+5%,\n" +
                "c,다,2,0,,Damage+5%,huge\n" +
                "d,라,3,0,,Damage+1~5,\n" +
                "e,마,4,0,,Damage+5%,\n");
            string errors = string.Join("\n", data.Errors);
            StringAssert.Contains("passives.csv 3행: 이어진 점 'nowhere'", errors);
            StringAssert.Contains("passives.csv 4행: (1, 0)에 이미", errors);
            StringAssert.Contains("passives.csv 5행: kind 'huge'", errors);
            StringAssert.Contains("passives.csv 6행", errors);
            StringAssert.Contains("'e'는 시작점에서 이어지지 않습니다", errors);
        }

        [Test]
        public void APointALevelSpentNextToWhatIsTaken()
        {
            var world = Room(out var player, level: 3);
            Assert.AreEqual(2, player.PassivePoints);
            Assert.IsFalse(world.AllocatePassive(player.Id, Point("str2")), "not next to the start or a taken point");
            Assert.IsFalse(world.AllocatePassive(player.Id, Point("start")), "the start is not taken");

            float health = player.Stats.MaxHealth;
            Assert.IsTrue(world.AllocatePassive(player.Id, Point("str1")));
            Assert.AreEqual(health * 1.05f, player.Stats.MaxHealth, 0.01f, "MaxHealth+5%");
            Assert.IsFalse(world.AllocatePassive(player.Id, Point("str1")), "once");
            Assert.IsTrue(world.AllocatePassive(player.Id, Point("str2")));
            Assert.AreEqual(0, player.PassivePoints);
            Assert.IsFalse(world.AllocatePassive(player.Id, Point("dex1")), "no points left");

            int version = player.InventoryVersion;
            Assert.IsTrue(world.ResetPassives(player.Id));
            Assert.AreEqual(2, player.PassivePoints);
            Assert.AreEqual(health, player.Stats.MaxHealth, 0.01f);
            Assert.Greater(player.InventoryVersion, version, "so the client hears of it and the character is saved");
        }

        [Test]
        public void TheBagMessageCarriesThePointsTaken()
        {
            var message = InventoryMessage.Decode(new InventoryMessage(new Equipment(), new Inventory(), new[] { 1, 12 }).Encode(Gear.Data), Gear.Data);
            CollectionAssert.AreEqual(new[] { 1, 12 }, message.Passives);
        }

        [Test]
        public void ASavedCharacterGetsBackWhatStillJoinsUpAndFits()
        {
            var world = Room(out var player, level: 3);
            // Out of order, one that does not join up, and more than two points.
            World.RestorePassives(player, Data, new[] { "str2", "str1", "dex5", "int1" });
            CollectionAssert.AreEquivalent(new[] { "str1", "str2" }, player.Passives.Select(p => p.Id), "str2 after str1; dex5 hangs on nothing; no third point");

            var saved = CharacterSave.Read(CharacterSave.Write(player.Equipment, player.Inventory, player.Experience, passives: player.Passives.Select(p => p.Id)),
                Gear.Data);
            CollectionAssert.AreEqual(new[] { "str1", "str2" }, saved.Passives);
        }
    }
}
