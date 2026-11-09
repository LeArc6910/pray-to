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

        // Pets (Pets.cs, Docs/Animals.md 펫): an egg hatches into a pet that gives its bonus; another egg swaps it, the
        // old one back in its egg; the crab nips monsters beside its player; the pet is saved and rides in snapshots.
        [Test]
        public void AnEggHatchesAPetThatHelps()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var slimeEgg = Gear.Data.FindMaterial("slime_egg");
            var crabEgg = Gear.Data.FindMaterial("crab_egg");
            float regen = player.Stats.HealthRegen;
            float armor = player.Stats.Armor;
            Assert.IsFalse(world.Hatch(player.Id, slimeEgg), "no egg in the bag");
            player.Inventory.AddMaterial(slimeEgg, 1);
            player.Inventory.AddMaterial(crabEgg, 1);
            Assert.IsTrue(world.Hatch(player.Id, slimeEgg));
            Assert.AreEqual("pet_slime", player.Pet.Id);
            Assert.AreEqual(regen + 1, player.Stats.HealthRegen, 0.001f);
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.Hatched && e.Source == player.Id));

            Assert.IsTrue(world.Hatch(player.Id, crabEgg));
            Assert.AreEqual(1, player.Inventory.CountOf(slimeEgg), "the slime back in its egg");
            Assert.AreEqual(armor + 8, player.Stats.Armor, 0.001f);

            var dummy = world.SpawnEnemy(Monsters.Dummy, player.Position + new Vec2(1.5f, 0));
            for (int tick = 0; tick < World.Ticks(Pets.BiteSeconds) + 1; tick++) world.Step();
            Assert.Less(dummy.Health, dummy.MaxHealth, "the crab nipped it");

            var saved = CharacterSave.Read(CharacterSave.Write(new Equipment(), new Inventory(), 0, pet: player.Pet.Id), Gear.Data);
            var again = Room().AddPlayer("b", null);
            World.RestorePet(again, saved.Pet);
            Assert.AreEqual("pet_crab", again.Pet.Id);
            Assert.AreEqual(Pets.Find("pet_crab"), PlayerPhase.PetOf(PlayerPhase.Sleeping | Pets.IndexOf(again.Pet) << PlayerPhase.PetShift));
            Assert.IsTrue(PlayerPhase.IsSleeping(PlayerPhase.Sleeping | 3 << PlayerPhase.PetShift));
        }

        [Test]
        public void EveryPetHasAnEggAndMonstersThatDropIt()
        {
            foreach (var pet in Pets.All)
            {
                Assert.IsNotNull(Gear.Data.Materials.FirstOrDefault(m => m.Id == pet.EggId), pet.EggId);
                Assert.IsTrue(pet.Sources.All(id => Monsters.Shipped.Monsters.Any(m => m.Id == id)), pet.Id);
                Assert.IsNotNull(Monsters.Shipped.Monsters.FirstOrDefault(m => m.Id == pet.Look), pet.Look);
            }
            Assert.LessOrEqual(Pets.All.Count, 31, "the pet number fits in a player's phase");
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
