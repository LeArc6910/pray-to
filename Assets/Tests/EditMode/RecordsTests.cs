using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The bestiary and achievements (Records.cs, Docs/Guide.md 도감과 업적).
    public class RecordsTests
    {
        static World Room() =>
            new World(Maps.Room(30), TileMap.Center(15, 15), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);

        static void Fell(World world, Player player, MonsterDefinition monster)
        {
            var enemy = world.SpawnEnemy(monster, player.Position + new Vec2(1, 0));
            enemy.Health = 1;
            world.Hurt(enemy, player, 5);
            world.Step();
        }

        [Test]
        public void FellingAMonsterCountsItsKind()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var slime = Monsters.Shipped.Find("slime");
            Fell(world, player, slime);
            Fell(world, player, slime);
            Assert.AreEqual(2, player.Records[(RecordKind.Kill, "slime")]);
            Assert.Greater(player.RecordsVersion, 0);
        }

        [Test]
        public void HoldingAMaterialOrGearFindsIt()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            player.Inventory.AddMaterial(Gear.Data.FindMaterial("cave_minnow"), 1);
            Assert.IsTrue(player.Inventory.TryAdd(Gear.Plain("iron_sword")));
            player.InventoryVersion++;
            world.Step();
            Assert.AreEqual(1, player.Records[(RecordKind.Found, "cave_minnow")]);
            Assert.AreEqual(1, player.Records[(RecordKind.Gear, "iron_sword")]);
            var fish = world.RecordEntries(RecordGroup.Fish);
            Assert.Greater(fish.Count, 5);
            Assert.AreEqual(1, Records.Progress(RecordGroup.Fish, player.Records, fish));
        }

        [Test]
        public void AnAchievementPaysOnceWhenReached()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var coin = Gear.Data.FindMaterial("coin");
            var kinds = world.RecordEntries(RecordGroup.Monsters).Take(5).Select(e => Monsters.Shipped.Find(e.Id)).ToList();
            foreach (var monster in kinds.Take(4)) Fell(world, player, monster);
            Assert.IsFalse(player.Achievements.Contains("hunter_5"));
            Assert.AreEqual(0, player.Inventory.CountOf(coin));

            world.TakeEvents();
            Fell(world, player, kinds[4]);
            Assert.IsTrue(player.Achievements.Contains("hunter_5"));
            Assert.AreEqual(100, player.Inventory.CountOf(coin));
            Fell(world, player, kinds[4]);
            Assert.AreEqual(100, player.Inventory.CountOf(coin), "only once");
        }

        [Test]
        public void RecordsAreSavedAndSent()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            Fell(world, player, Monsters.Shipped.Find("slime"));
            player.Achievements.Add("boss_1");
            var saved = CharacterSave.Read(CharacterSave.Write(new Equipment(), new Inventory(), 0,
                records: player.Records.Select(p => (p.Key.Kind.ToString(), p.Key.Id, p.Value)), achievements: player.Achievements), Gear.Data);
            var again = Room().AddPlayer("b", null);
            World.RestoreRecords(again, saved.Records, saved.Achievements);
            Assert.AreEqual(1, again.Records[(RecordKind.Kill, "slime")]);
            CollectionAssert.Contains(again.Achievements, "boss_1");

            var message = RecordsMessage.Decode(new RecordsMessage(new[] { (RecordKind.Kill, 3, 12), (RecordKind.Gear, 40, 1) }, new[] { 0, 5 }).Encode());
            Assert.AreEqual((RecordKind.Kill, 3, 12), message.Records[0]);
            CollectionAssert.AreEqual(new[] { 0, 5 }, message.Achievements);
        }

        [Test]
        public void EveryAchievementCanBeReached()
        {
            var world = Room();
            foreach (var achievement in Records.Achievements)
            {
                Assert.IsTrue(achievement.Rewards.All(r => Gear.Data.FindMaterial(r.Material) != null), achievement.Id);
                if (achievement.Group == RecordGroup.Felled) continue;
                int there = achievement.Group == RecordGroup.Bosses ? Bosses.Shipped.Bosses.Count : world.RecordEntries(achievement.Group).Count;
                Assert.GreaterOrEqual(there, achievement.Need, achievement.Id);
            }
        }
    }
}
