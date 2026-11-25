using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // T1 of the title rework (Docs/TitleRework.md, 2026-11-25): the saves' new head (look, time played, when made and last
    // played, biomes been to), the lists' summaries, looks on the wire, and worlds made a stage at a time and only once.
    public class TitleTests
    {
        static readonly CharacterLook Picked = new CharacterLook(3, 7, 4, 2, 11, 5);

        static WorldClient Client(string name = "앨리스") =>
            new WorldClient("key-" + name, name, Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped);

        static WorldHost Host(ulong seed = 7, GeneratedWorld generated = null, int size = WorldGenerator.DefaultSize, int version = WorldGenerator.CaveVersion) =>
            new WorldHost(seed, Monsters.Shipped, Gear.Data, new HostConfig { MapSize = size, MapVersion = version }, new WorldConfig { EnemyTarget = 0 },
                Bosses.Shipped, Classes.Shipped, Tiles.Shipped, generated: generated);

        static void Deliver(WorldHost host, IReadOnlyDictionary<ulong, WorldClient> clients)
        {
            foreach (var message in host.TakeOutgoing())
                if (clients.TryGetValue(message.Connection, out var client)) client.Receive(message.Data);
        }

        // ---- Saves ----

        [Test]
        public void ACharacterKeepsItsLookTimeAndBiomes()
        {
            var gear = Gear.Wearing("copper_sword");
            int visited = BiomeVisits.Mark(BiomeVisits.Mark(0, Biome.Cave), Biome.Ice);
            byte[] data = CharacterSave.Write(gear, new Inventory(), 900, classes: new[] { "warrior" },
                records: new[] { ("Kill", "slime", 3), ("Kill", "bat", 9), ("Boss", "spider_queen", 1), ("Found", "stone", 40) },
                look: Picked, playedSeconds: 3600.5, createdAt: 1_700_000_000, lastPlayedAt: 1_700_003_600, visited: visited);

            var save = CharacterSave.Read(data, Gear.Data);
            Assert.AreEqual(CharacterSave.FormatVersion, save.Version);
            Assert.AreEqual(Picked, save.Look);
            Assert.AreEqual(3600.5, save.PlayedSeconds, 1e-9);
            Assert.AreEqual(1_700_000_000, save.CreatedAt);
            Assert.AreEqual(1_700_003_600, save.LastPlayedAt);
            Assert.AreEqual(visited, save.Visited);

            var summary = CharacterSave.ReadSummary(data, "앨리스");
            Assert.AreEqual("앨리스", summary.Name);
            Assert.AreEqual(Picked, summary.Look);
            Assert.AreEqual(900, summary.Experience);
            Assert.AreEqual(Leveling.LevelFor(900), summary.Level);
            CollectionAssert.AreEqual(new[] { "warrior" }, summary.Classes);
            Assert.AreEqual(3600.5, summary.PlayedSeconds, 1e-9);
            Assert.AreEqual(1_700_000_000, summary.CreatedAt);
            Assert.AreEqual(1_700_003_600, summary.LastPlayedAt);
            CollectionAssert.AreEqual(new[] { Biome.Cave, Biome.Ice }, summary.VisitedBiomes);
            CollectionAssert.AreEqual(new[] { "bat", "slime" }, summary.Monsters, "the most felled first");
            CollectionAssert.AreEqual(new[] { "spider_queen" }, summary.Bosses);
        }

        [Test]
        public void ACharacterFromBeforeFormat20ReadsWithTheDefaults()
        {
            byte[] data = CharacterSave.Write(Gear.Wearing("copper_sword"), new Inventory(), 50, look: Picked, playedSeconds: 10, createdAt: 5, lastPlayedAt: 6, visited: 3);
            // Format 19: the same bytes without the look, the time, the two clocks and the biomes at the end.
            byte[] old = data.Take(data.Length - (4 + 8 + 8 + 8 + 2)).ToArray();
            BitConverter.GetBytes(19).CopyTo(old, 4);

            var save = CharacterSave.Read(old, Gear.Data);
            Assert.AreEqual(19, save.Version);
            Assert.AreEqual(CharacterLook.Default, save.Look);
            Assert.AreEqual(0, save.PlayedSeconds);
            Assert.AreEqual(0, save.CreatedAt);
            Assert.AreEqual(0, save.Visited);
            Assert.AreEqual("copper_sword", save.Equipment[ItemSlot.Weapon].Base.Id);
            var summary = CharacterSave.ReadSummary(old);
            Assert.AreEqual(50, summary.Experience);
            Assert.AreEqual(CharacterLook.Default, summary.Look);
            Assert.IsEmpty(summary.VisitedBiomes);
        }

        [Test]
        public void BrokenSavesAreRefusedBySummariesToo()
        {
            byte[] character = CharacterSave.Write(Gear.Wearing("copper_sword"), new Inventory());
            Assert.Throws<InvalidDataException>(() => CharacterSave.ReadSummary(character.Take(character.Length / 2).ToArray()));
            Assert.Throws<InvalidDataException>(() => CharacterSave.ReadSummary(new byte[] { 1, 2, 3 }));
            Assert.Throws<InvalidDataException>(() => CharacterSave.ReadSummary(null));
            Assert.Throws<InvalidDataException>(() => WorldSave.ReadSummary(character), "a character is not a world");
            byte[] world = Host().SaveWorld(now: 1);
            Assert.Throws<InvalidDataException>(() => WorldSave.ReadSummary(world.Take(20).ToArray()));
            Assert.Throws<InvalidDataException>(() => CharacterSave.ReadSummary(world), "a world is not a character");
        }

        [Test]
        public void AWorldKeepsWhenItWasMadeAndHowLongItWasPlayed()
        {
            var host = Host();
            host.World.AddPlayer("a", null);
            host.Tick();
            host.NotePlayed(125.25);
            byte[] data = host.SaveWorld(now: 1_000);

            var save = WorldSave.Read(data, Tiles.Shipped);
            Assert.AreEqual(1_000, save.CreatedAt, "made when first saved");
            Assert.AreEqual(1_000, save.SavedAt);
            Assert.AreEqual(125.25, save.PlayedSeconds, 1e-9);
            Assert.IsTrue(BiomeVisits.Has(save.Visited, Biome.Cave), "the player stood in the dirt cave");

            // Played again later: still made then, played for longer, last played now.
            var again = Host(save.Seed);
            again.Restore(save);
            again.NotePlayed(60);
            var later = WorldSave.ReadSummary(again.SaveWorld(now: 5_000), "내 월드");
            Assert.AreEqual("내 월드", later.Name);
            Assert.AreEqual(1_000, later.CreatedAt);
            Assert.AreEqual(5_000, later.LastPlayedAt);
            Assert.AreEqual(185.25, later.PlayedSeconds, 1e-9);
            Assert.AreEqual(7UL, later.Seed);
            Assert.AreEqual(WorldGenerator.DefaultSize, later.Width);
            Assert.AreEqual(WorldGenerator.CaveVersion, later.Generator);
            Assert.AreEqual(1, later.Day);
            Assert.AreEqual(Biome.Cave, later.Deepest);
            Assert.AreEqual(1, later.ProgressLevel);
        }

        [Test]
        public void AWorldFromBeforeFormat17ReadsWithTheDefaults()
        {
            var host = Host();
            host.NotePlayed(30);
            var old = host.SaveWorld(now: 77).ToList();
            old.RemoveRange(4 + 4 + 8 + 4 + 4 + 1 + 8, 18);
            old[4] = 16;

            var save = WorldSave.Read(old.ToArray(), Tiles.Shipped);
            Assert.AreEqual(0, save.CreatedAt);
            Assert.AreEqual(0, save.PlayedSeconds);
            Assert.AreEqual(77, save.SavedAt);
            var summary = WorldSave.ReadSummary(old.ToArray());
            Assert.AreEqual(16, summary.Version);
            Assert.AreEqual(77, summary.LastPlayedAt);
            Assert.IsNull(summary.Deepest);
            // Opened again, it is stamped as made at its next save.
            var again = Host();
            again.Restore(save);
            Assert.AreEqual(90, WorldSave.Read(again.SaveWorld(now: 90), Tiles.Shipped).CreatedAt);
        }

        [Test]
        public void TheDeepestBiomeGoesByTheLevels()
        {
            Assert.IsNull(BiomeVisits.Deepest(0));
            int visited = BiomeVisits.Mark(BiomeVisits.Mark(BiomeVisits.Mark(0, Biome.Cave), Biome.Lake), Biome.Sand);
            Assert.AreEqual(Biome.Lake, BiomeVisits.Deepest(visited));
            CollectionAssert.AreEqual(new[] { Biome.Cave, Biome.Sand, Biome.Lake }, BiomeVisits.In(visited));
            Assert.AreEqual(Enum.GetValues(typeof(Biome)).Length, BiomeVisits.Order.Distinct().Count(), "every biome has its place");
        }

        [Test]
        public void TheClientKeepsItsCharactersTimeAndBiomesAndStampsItsSaves()
        {
            var host = Host();
            var saved = CharacterSave.Write(Gear.Wearing("copper_sword"), new Inventory(), 10, look: Picked, playedSeconds: 100, createdAt: 40, lastPlayedAt: 50,
                visited: BiomeVisits.Bit(Biome.Ice));
            var client = Client();
            client.Character = saved;
            Assert.AreEqual(Picked, client.Look);
            host.Receive(1, client.JoinMessage());
            Deliver(host, new Dictionary<ulong, WorldClient> { [1] = client });
            client.NotePlayed(20);
            client.Step(Vec2.Zero, Vec2.Zero, false);
            host.Tick();
            Deliver(host, new Dictionary<ulong, WorldClient> { [1] = client });

            Assert.AreEqual(120, client.PlayedSeconds, 1e-9);
            Assert.IsTrue(BiomeVisits.Has(client.Visited, Biome.Ice), "the save's");
            Assert.IsTrue(BiomeVisits.Has(client.Visited, Biome.Cave), "the spawn's");
            var summary = CharacterSave.ReadSummary(client.SaveCharacter(now: 900));
            Assert.AreEqual(Picked, summary.Look);
            Assert.AreEqual(40, summary.CreatedAt, "made when it was");
            Assert.AreEqual(900, summary.LastPlayedAt);
            Assert.AreEqual(120, summary.PlayedSeconds, 1e-9);

            // A new character is made at its first save.
            var fresh = Client("밥");
            host.Receive(2, fresh.JoinMessage());
            host.Tick();
            Deliver(host, new Dictionary<ulong, WorldClient> { [2] = fresh });
            Assert.AreEqual(300, CharacterSave.ReadSummary(fresh.SaveCharacter(now: 300)).CreatedAt);
            Assert.AreEqual(300, CharacterSave.ReadSummary(fresh.SaveCharacter(now: 400)).CreatedAt);
        }

        // ---- Looks on the wire ----

        [Test]
        public void EveryoneSeesEachOthersLook()
        {
            var join = JoinMessage.Decode(new JoinMessage("k", "앨리스", look: Picked.Packed).Encode());
            Assert.AreEqual(Picked.Packed, join.Look);
            Assert.AreEqual(CharacterLook.Default.Packed, JoinMessage.Decode(new JoinMessage("k", "앨리스").Encode()).Look);
            var roster = RosterMessage.Decode(new RosterMessage(new Dictionary<int, string> { [1] = "a" }, null, new Dictionary<int, uint> { [1] = Picked.Packed }).Encode());
            Assert.AreEqual(Picked.Packed, roster.Looks[1]);

            var host = Host();
            var alice = Client();
            alice.Look = Picked;
            var bob = Client("밥");
            var clients = new Dictionary<ulong, WorldClient> { [1] = alice, [2] = bob };
            host.Receive(1, alice.JoinMessage());
            host.Receive(2, bob.JoinMessage());
            Deliver(host, clients);
            for (int i = 0; i < 6; i++)
            {
                foreach (var pair in clients)
                {
                    byte[] input = pair.Value.Step(Vec2.Zero, Vec2.Zero, false);
                    if (input != null) host.Receive(pair.Key, input);
                }
                host.Tick();
                Deliver(host, clients);
                foreach (var client in clients.Values) client.AdvanceRenderClock(World.TickSeconds);
            }

            Assert.AreEqual(Picked.Packed, host.World.FindPlayer(alice.PlayerId).Look);
            Assert.AreEqual(Picked, bob.LookOf(alice.PlayerId));
            Assert.AreEqual(CharacterLook.Default, alice.LookOf(bob.PlayerId));
            Assert.AreEqual(Picked, alice.LookOf(alice.PlayerId), "its own");
            var seen = bob.Others().Single(v => v.Id == alice.PlayerId);
            Assert.AreEqual(Picked, seen.Look);
            Assert.AreEqual(CharacterLook.Default, alice.Others().Single(v => v.Id == bob.PlayerId).Look);
        }

        // ---- Making the world ----

        [Test]
        public void MadeAStageAtATimeTheWorldIsTheSame([Values(1, 2, 3, 4, 5, 6, 7)] int version)
        {
            foreach (ulong seed in new ulong[] { 1, 2, 3 })
            {
                var whole = WorldGenerator.Generate(seed, 256, 256, Tales.Shipped, Bosses.Staged.Arenas, version, Tiles.Shipped.Terrain);
                var stages = WorldGenerator.Steps(seed, 256, 256, Tales.Shipped, Bosses.Staged.Arenas, version, Tiles.Shipped.Terrain).ToList();
                var stepped = stages.Last().World;

                Assert.AreEqual(GenerationStage.Done, stages.Last().Key);
                Assert.IsTrue(stages.Take(stages.Count - 1).All(s => s.World == null), "only the last carries the world");
                CollectionAssert.AreEqual(GenerationStage.KeysFor(version, true, version >= WorldGenerator.RoomVersion && DungeonPocket.Fits(256)),
                    stages.Take(stages.Count - 1).Select(s => s.Key), $"version {version}");
                Assert.AreEqual(0, stages[0].Fraction);
                Assert.AreEqual(1, stages.Last().Fraction);
                for (int i = 1; i < stages.Count; i++) Assert.GreaterOrEqual(stages[i].Fraction, stages[i - 1].Fraction);

                Assert.AreEqual(RoomMapTests.Hash(whole.Map), RoomMapTests.Hash(stepped.Map), $"seed {seed}, version {version}");
                Assert.AreEqual(whole.Map.Rows, stepped.Map.Rows);
                Assert.AreEqual(seed, stepped.Seed);
                Assert.AreEqual(whole.Spawn, stepped.Spawn);
                CollectionAssert.AreEqual(whole.Arenas, stepped.Arenas);
                CollectionAssert.AreEqual(whole.ArenaKinds, stepped.ArenaKinds);
                CollectionAssert.AreEqual(whole.Places.Select(p => (p.X, p.Y)), stepped.Places.Select(p => (p.X, p.Y)));
                Assert.AreEqual(whole.Rooms?.Nodes.Count, stepped.Rooms?.Nodes.Count);
                Assert.AreEqual(whole.Rooms?.Pois.Count, stepped.Rooms?.Pois.Count);
                Assert.AreEqual(whole.BiomeLandmarks.Count, stepped.BiomeLandmarks.Count);
            }
        }

        [Test]
        public void AWorldMadeInStepsCanBeWalkedThroughOneStageAtATime()
        {
            var generation = new WorldGeneration(4, 256, 256, Tales.Shipped, Bosses.Staged.Arenas, WorldGenerator.NewWorldVersion, Tiles.Shipped.Terrain);
            Assert.AreEqual("", generation.Stage);
            var seen = new List<string>();
            float last = 0;
            while (generation.Step())
            {
                seen.Add(generation.Stage);
                Assert.GreaterOrEqual(generation.Fraction, last);
                last = generation.Fraction;
                Assert.IsNull(generation.World);
            }
            Assert.IsTrue(generation.Done);
            Assert.AreEqual(GenerationStage.Done, generation.Stage);
            Assert.AreEqual(1, generation.Fraction);
            Assert.IsFalse(generation.Step());
            CollectionAssert.AreEqual(GenerationStage.KeysFor(WorldGenerator.NewWorldVersion, true, DungeonPocket.Fits(256)), seen);
            Assert.AreEqual(RoomMapTests.Hash(WorldGenerator.Generate(4, 256, 256, Tales.Shipped, Bosses.Staged.Arenas, WorldGenerator.NewWorldVersion,
                Tiles.Shipped.Terrain).Map), RoomMapTests.Hash(generation.World.Map));
        }

        [Test]
        public void ASoloGameMakesTheWorldOnce()
        {
            const int size = 256;
            var made = WorldGenerator.Generate(5, size, size, null, Bosses.Shipped.Arenas, WorldGenerator.NewWorldVersion, Tiles.Shipped.Terrain);
            var copy = made.Copy();
            var host = Host(5, made, size, WorldGenerator.NewWorldVersion);
            Assert.AreSame(made, host.Generated, "the host builds on the world given");
            // The host's world changes its map (a wall built), the copy keeps the map as made.
            var player = host.World.AddPlayer("x", null);
            var stone = Gear.Data.FindMaterial("stone");
            player.Inventory.AddMaterial(stone);
            host.World.PlaceTile(player.Id, stone);
            var wall = host.World.ChangedTiles().Single();
            Assert.AreNotEqual(made.Map[wall.X, wall.Y], copy.Map[wall.X, wall.Y]);

            var client = Client();
            client.Made = copy;
            host.Receive(1, client.JoinMessage());
            Deliver(host, new Dictionary<ulong, WorldClient> { [1] = client });

            Assert.AreEqual(ClientStatus.Playing, client.Status);
            Assert.IsTrue(client.MapReused);
            Assert.AreSame(copy.Map, client.Map);
            Assert.AreNotSame(host.World.Map, client.Map);
            Assert.AreEqual(host.World.Map[wall.X, wall.Y], client.Map[wall.X, wall.Y], "the wall came as a change, as to anyone");
            Assert.AreEqual(RoomMapTests.Hash(host.World.Map), RoomMapTests.Hash(client.Map));

            // A world made from another seed is not taken.
            var other = Client("밥");
            other.Made = WorldGenerator.Generate(6, size, size, null, Bosses.Shipped.Arenas, WorldGenerator.NewWorldVersion, Tiles.Shipped.Terrain);
            host.Receive(2, other.JoinMessage());
            Deliver(host, new Dictionary<ulong, WorldClient> { [2] = other });
            Assert.IsFalse(other.MapReused);
            Assert.AreEqual(RoomMapTests.Hash(host.World.Map), RoomMapTests.Hash(other.Map));
        }

        [Test]
        public void AJoinerMakesTheMapInStepsAndCatchesUp()
        {
            const int size = 256;
            var host = Host(9, null, size, WorldGenerator.NewWorldVersion);
            var player = host.World.AddPlayer("x", null);
            var stone = Gear.Data.FindMaterial("stone");
            player.Inventory.AddMaterial(stone);
            host.World.PlaceTile(player.Id, stone);
            var wall = host.World.ChangedTiles().Single();

            var client = Client();
            client.DeferMap = true;
            host.Receive(1, client.JoinMessage());
            Deliver(host, new Dictionary<ulong, WorldClient> { [1] = client });
            host.Tick();
            Deliver(host, new Dictionary<ulong, WorldClient> { [1] = client });

            Assert.AreEqual(ClientStatus.Joining, client.Status, "still making the map");
            Assert.IsNotNull(client.MapGeneration);
            Assert.IsNull(client.Map);
            var generation = client.MapGeneration;
            int steps = 0;
            while (client.StepMap()) steps++;
            Assert.Greater(steps, 5);
            Assert.IsTrue(generation.Done);
            Assert.IsNull(client.MapGeneration);
            Assert.AreEqual(ClientStatus.Playing, client.Status);
            Assert.IsFalse(client.MapReused);
            Assert.AreEqual(TileType.Wall, client.Map[wall.X, wall.Y], "the changes held meanwhile came after");
            Assert.AreEqual(RoomMapTests.Hash(host.World.Map), RoomMapTests.Hash(client.Map));
            Assert.IsTrue(client.Names.ContainsKey(client.PlayerId), "the roster held meanwhile came after");
            Assert.IsNotNull(client.Step(Vec2.Zero, Vec2.Zero, false), "it plays");
        }
    }
}
