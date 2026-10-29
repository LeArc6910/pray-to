using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class SaveTests
    {
        static readonly ConsumableDefinition Potion = Gear.Data.FindConsumable("healing_potion");
        static readonly MaterialDefinition Stone = Gear.Data.FindMaterial("stone");
        static readonly MaterialDefinition Spore = Gear.Data.FindMaterial("mushroom_spore");
        static readonly RuneDefinition Ember = Gear.Data.FindRune("ember");

        static WorldHost Host(ulong seed = 7) =>
            new WorldHost(seed, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);

        static (Equipment Gear, Inventory Bag) Kit()
        {
            var equipment = new Equipment();
            var sword = Gear.Plain("copper_sword", 2, Rarity.Rare, Gear.Affix("sharp", 2), Gear.Affix("swift", 4));
            sword.TryInsertRune(0, Ember, Gear.Data.Runewords);
            equipment.Equip(sword);
            equipment.Equip(Gear.Plain("copper_helmet"));
            var bag = new Inventory();
            bag.TryAdd(Gear.Plain("iron_bow", 1, Rarity.Uncommon, Gear.Affix("swift", 5)));
            bag.AddRune(Ember, 2);
            bag.AddConsumable(Potion, 4);
            bag.AddMaterial(Stone, 17);
            return (equipment, bag);
        }

        [Test]
        public void CharactersComeBackAsTheyWere()
        {
            var (gear, bag) = Kit();

            var loaded = CharacterSave.Read(CharacterSave.Write(gear, bag), Gear.Data);

            Assert.IsEmpty(loaded.Warnings);
            var sword = loaded.Equipment[ItemSlot.Weapon];
            var original = gear[ItemSlot.Weapon];
            Assert.AreEqual("copper_sword", sword.Base.Id);
            Assert.AreEqual(Rarity.Rare, sword.Rarity);
            CollectionAssert.AreEqual(original.Implicits, sword.Implicits);
            CollectionAssert.AreEqual(original.Affixes.Select(a => (a.Affix.Id, a.Tier.Number, a.Modifier)), sword.Affixes.Select(a => (a.Affix.Id, a.Tier.Number, a.Modifier)));
            CollectionAssert.AreEqual(new[] { Ember, null }, sword.Sockets);
            Assert.AreEqual("copper_helmet", loaded.Equipment[ItemSlot.Helmet].Base.Id);
            Assert.AreEqual("iron_bow", loaded.Bag.Items.Single().Base.Id);
            Assert.AreEqual(2, loaded.Bag.CountOf(Ember));
            Assert.AreEqual(4, loaded.Bag.CountOf(Potion));
            Assert.AreEqual(17, loaded.Bag.CountOf(Stone));
            Assert.AreEqual(0, sword.Id, "the world it joins gives it an id");
        }

        [Test]
        public void WhatTheSheetsNoLongerHaveIsSkippedWithAWarning()
        {
            var (gear, bag) = Kit();

            var other = TestLoot.Parse();
            var loaded = CharacterSave.Read(CharacterSave.Write(gear, bag), other);

            Assert.IsNull(loaded.Equipment[ItemSlot.Weapon]);
            Assert.IsEmpty(loaded.Bag.Items);
            Assert.AreEqual(2, loaded.Bag.CountOf(other.FindRune("ember")), "the test sheets have an ember rune too");
            Assert.IsTrue(loaded.Warnings.Any(w => w.Contains("copper_sword")));
            Assert.IsTrue(loaded.Warnings.Any(w => w.Contains("healing_potion")));
            Assert.IsTrue(loaded.Warnings.Any(w => w.Contains("stone")));
        }

        [Test]
        public void BrokenOrNewerSavesAreRefused()
        {
            var (gear, bag) = Kit();
            byte[] good = CharacterSave.Write(gear, bag);
            byte[] newer = (byte[])good.Clone();
            newer[4] = 99;

            Assert.Throws<InvalidDataException>(() => CharacterSave.Read(good.Take(good.Length / 2).ToArray(), Gear.Data));
            Assert.Throws<InvalidDataException>(() => CharacterSave.Read(new byte[] { 1, 2, 3 }, Gear.Data));
            var e = Assert.Throws<InvalidDataException>(() => CharacterSave.Read(newer, Gear.Data));
            StringAssert.Contains("더 새 게임", e.Message);
            Assert.Throws<InvalidDataException>(() => WorldSave.Read(good, Tiles.Shipped), "a character is not a world");
        }

        [Test]
        public void WorldsKeepTheirTilesCropsAndProgress()
        {
            var host = Host();
            var world = host.World;
            var player = world.AddPlayer("a", null);
            player.Inventory.AddMaterial(Stone);
            player.Inventory.AddMaterial(Spore);
            // Facing down from the spawn: plant there, then face up and build there.
            Assert.IsTrue(world.Plant(player.Id, Tiles.Shipped.CropFrom(Spore)));
            world.MovePlayer(player.Id, player.Position, new Vec2(0, 1), 1);
            Assert.IsTrue(world.PlaceTile(player.Id, Stone));
            for (int i = 0; i < World.TicksPerSecond * 10; i++) world.Step();
            var crop = world.Crops.Single();
            var wall = world.ChangedTiles().Single();

            byte[] data = host.SaveWorld(now: 1_000);
            var save = WorldSave.Read(data, Tiles.Shipped);
            var again = Host(save.Seed);
            again.Restore(save, now: 1_000 + 60);

            Assert.IsEmpty(save.Warnings);
            Assert.AreEqual(TileType.Wall, again.World.Map[wall.X, wall.Y]);
            var grown = again.World.Crops.Single();
            Assert.AreEqual((crop.X, crop.Y), (grown.X, grown.Y));
            // Ten seconds of play and sixty with the world closed, of the mushroom's 180.
            Assert.AreEqual(70f / 180, grown.Growth(again.World.Tick), 0.01f);
            Assert.AreEqual(1, again.World.ProgressLevel);
        }

        [Test]
        public void AClockThatWentBackGrowsNothing()
        {
            var host = Host();
            var player = host.World.AddPlayer("a", null);
            player.Inventory.AddMaterial(Spore);
            host.World.Plant(player.Id, Tiles.Shipped.CropFrom(Spore));
            var save = WorldSave.Read(host.SaveWorld(now: 5_000), Tiles.Shipped);

            var again = Host(save.Seed);
            again.Restore(save, now: 4_000);

            Assert.AreEqual(0, again.World.Crops.Single().Growth(again.World.Tick), 0.001f);
            Assert.Throws<System.ArgumentException>(() => Host(8).Restore(save, now: 5_000), "another seed's host");
        }

        [Test]
        public void PlayersJoiningARestoredWorldSeeItsChanges()
        {
            var host = Host();
            var player = host.World.AddPlayer("a", null);
            player.Inventory.AddMaterial(Stone);
            host.World.PlaceTile(player.Id, Stone);
            var wall = host.World.ChangedTiles().Single();
            var save = WorldSave.Read(host.SaveWorld(now: 0), Tiles.Shipped);

            var again = Host(save.Seed);
            again.Restore(save, now: 0);
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped);
            again.Receive(1, client.JoinMessage());
            foreach (var message in again.TakeOutgoing()) client.Receive(message.Data);

            Assert.AreEqual(TileType.Wall, client.Map[wall.X, wall.Y]);
        }

        [Test]
        public void SavedCharactersJoinWithTheirOwnGearAndNoStarterKit()
        {
            var (gear, bag) = Kit();
            var host = Host();
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped)
            {
                Character = CharacterSave.Write(gear, bag),
            };

            host.Receive(1, client.JoinMessage());
            host.Tick();
            foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);

            var player = host.World.Players.Single();
            Assert.AreEqual("copper_sword", player.Equipment[ItemSlot.Weapon].Base.Id);
            Assert.AreNotEqual(0, player.Inventory.Items.Single().Id, "bag items get ids in this world");
            Assert.AreEqual(4, player.Inventory.CountOf(Potion), "the saved four, not the starter three on top");
            Assert.AreEqual(0, player.Inventory.CountOf(Spore));
            var resaved = CharacterSave.Read(client.SaveCharacter(), Gear.Data);
            Assert.AreEqual(17, resaved.Bag.CountOf(Stone), "the client saves what the host sent back");
        }

        [Test]
        public void ABrokenCharacterIsRefusedRatherThanReplaced()
        {
            var host = Host();
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped)
            {
                Character = new byte[] { 9, 9, 9, 9, 9 },
            };

            host.Receive(1, client.JoinMessage());
            foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);

            Assert.AreEqual(ClientStatus.Refused, client.Status);
            StringAssert.Contains("저장본", client.RefusedReason);
            Assert.IsEmpty(host.World.Players);
            Assert.IsNull(client.SaveCharacter(), "nothing to overwrite the save with");
        }
    }
}
