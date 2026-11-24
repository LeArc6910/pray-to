using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The rest of B1 (2026-11-24, Docs/BiomeRework.md 묶음 B1c): the dirt cave's abandoned mine and the mushroom forest's
    // spore den with their mid-bosses for masters, the camps' trapdoors down to the cellar every biome shares, the
    // mid-bosses' lairs (the field's only XL), a big beetle's charge smashing props, and the trap rooms shut at their
    // narrows.
    public class B1cTests
    {
        static DungeonData Sheet => Tiles.Shipped.Terrain.Dungeons;

        static List<SimEvent> Run(World world, float seconds)
        {
            var events = new List<SimEvent>();
            for (int i = World.Ticks(seconds); i > 0; i--)
            {
                world.Step();
                events.AddRange(world.TakeEvents());
            }
            return events;
        }

        // ---- The sheets ----

        [Test]
        public void TheTwoBiomesDungeonsHaveTheirMidBossesForMastersAndACellarForEveryBiome()
        {
            Assert.IsTrue(Sheet.Success, string.Join("\n", Sheet.Errors));
            var mine = Sheet.For(Biome.Cave);
            var den = Sheet.For(Biome.Mushroom);
            Assert.AreEqual("abandoned_mine", mine.Id);
            Assert.AreEqual("버려진 갱도", mine.Name);
            Assert.AreEqual("spore_depths", den.Id);
            foreach (var (row, boss) in new[] { (mine, "slime_mother"), (den, "mycelium_mother") })
            {
                Assert.IsTrue(row.Master, $"{row.Id}: its master in person");
                Assert.AreEqual(boss, row.Boss);
                Assert.AreEqual(MonsterSize.XL, row.MasterKind(Monsters.Shipped).Size, $"{row.Id}: an XL mid-boss");
                Assert.AreEqual(7, row.Light.Length, $"{row.Id}: its light colour");
                Assert.Less(row.Experience, 100, $"{row.Id}: its chest gives the rest");
                Assert.IsFalse(row.Cellar);
            }
            Assert.IsNotNull(Sheet.Graphs.FirstOrDefault(g => g.Id == "mine"));
            // Every rows' masters are monsters of the sheet; a big elite's is not an XL, a master in person is.
            foreach (var row in Sheet.Dungeons.Where(d => d.Boss != null))
                Assert.AreEqual(row.Master, row.MasterKind(Monsters.Shipped).Size == MonsterSize.XL, row.Id);
            // The cellar: of no biome of its own, never behind a gate, behind every biome's trapdoor.
            var cellar = Sheet.Dungeons.Single(d => d.Cellar);
            Assert.IsNull(cellar.Biome);
            foreach (Biome biome in Enum.GetValues(typeof(Biome)))
            {
                Assert.AreSame(cellar, Sheet.CellarFor(biome));
                Assert.IsFalse(Sheet.For(biome).Cellar, $"{biome}: a gate leads to a dungeon, not a cellar");
                var trapdoor = new DungeonGate(0, 1, 1, biome, cellar: true);
                Assert.AreSame(cellar, Sheet.For(trapdoor));
                Assert.AreEqual($"{BiomeMap.Name(biome)}의 지하실", cellar.NameFor(biome));
            }
            // The mine's own chunks stand its props.
            var chunks = Tiles.Shipped.Terrain.Rooms.Chunks.Where(c => c.Dungeon && c.Biomes.Contains(Biome.Cave)).ToList();
            Assert.GreaterOrEqual(chunks.Count, 4);
            Assert.IsTrue(chunks.Any(c => c.Rows.Any(r => r.IndexOf('p') >= 0)) && chunks.Any(c => c.Rows.Any(r => r.IndexOf('g') >= 0)), "pit props and stalagmites");
            Assert.IsTrue(Tiles.Shipped.Terrain.Rooms.Chunks.Any(c => c.Dungeon && c.Biomes.Contains(Biome.Mushroom) && c.Rows.Any(r => r.IndexOf('Z') >= 0)), "spore sacs in the den");
        }

        [Test]
        public void TheDungeonSheetSaysWhatIsWrongWithItsNewColumns()
        {
            const string head = "id,name,biome,level,graph,rooms,boss,chest,minutes,entry,light,experience\n";
            const string graphs = "id,spine,branches,loop\nshort,E C R B X,T,shortcut\n";
            string Error(string row) => string.Join("\n", DungeonSheet.Parse(head + row, graphs).Errors);
            Assert.IsEmpty(Error("a,가,cave,1,short,5,slime_mother,chest_iron,5,gate,#FFB060,75\n"));
            Assert.IsEmpty(Error("a,가,,1,short,5,elite,chest_iron,5,cellar,,\nb,나,,1,short,5,elite,chest_iron,5,,,\n"), "one of any biome for each entry");
            StringAssert.Contains("entry", Error("a,가,cave,1,short,5,elite,chest_iron,5,door,,\n"));
            StringAssert.Contains("light", Error("a,가,cave,1,short,5,elite,chest_iron,5,gate,orange,\n"));
            StringAssert.Contains("experience", Error("a,가,cave,1,short,5,elite,chest_iron,5,gate,,500\n"));
            StringAssert.Contains("entry마다", Error("a,가,,1,short,5,elite,chest_iron,5,cellar,,\nb,나,,1,short,5,elite,chest_iron,5,cellar,,\n"));
            // A sheet without the new columns reads as before (the scenes' sheets until the local side updates them).
            var old = DungeonSheet.Parse("id,name,biome,level,graph,rooms,boss,chest,minutes\na,가,cave,1,short,5,elite:slime,chest_iron,5\n", graphs);
            Assert.IsTrue(old.Success);
            Assert.IsFalse(old.Dungeons[0].Master || old.Dungeons[0].Cellar);
            Assert.AreEqual(100, old.Dungeons[0].Experience);
        }

        // ---- A run of the mine: its master in person ----

        static World Hand(Biome biome, out DungeonGate gate, string dungeon, int zone = 1)
        {
            var map = new TileMap(320, 40, TileType.Floor);
            for (int x = 0; x < map.Width; x++)
            {
                map[x, 0] = TileType.Wall;
                map[x, map.Height - 1] = TileType.Wall;
            }
            map.AddPocket(DungeonPocket.Rows);
            var world = new World(map, new Vec2(10.5f, 10.5f), Monsters.Shipped, 11, new WorldConfig { EnemyTarget = 0, ZoneLevel = zone }, Gear.Data, Tiles.Shipped);
            gate = world.AddGate(40, 20, biome);
            Assert.IsTrue(world.ChooseDungeon(gate.Index, dungeon));
            return world;
        }

        [Test]
        public void TheMinesMasterIsTheSlimeMotherHerselfAndEachPackRoomHasABigBody()
        {
            var world = Hand(Biome.Cave, out var gate, "abandoned_mine");
            world.MonstersHold = true;
            var player = world.AddPlayer("a", Gear.Wearing("iron_sword"));
            player.Position = gate.Center;
            Assert.IsTrue(world.EnterDungeon(player.Id, gate.Index));
            Run(world, World.EnterSeconds + 0.1f);
            var dungeon = world.DungeonOf(gate);
            Assert.AreEqual("버려진 갱도", dungeon.Name);
            var layout = dungeon.Layout;
            CollectionAssert.IsSubsetOf(new[] { 'E', 'K', 'L', 'M', 'R', 'B', 'X' }, layout.Nodes.Select(n => n.Letter).ToList());

            // A pack room: its pack and one big body (L) of the cave's packs.
            var room = layout.Nodes.First(n => n.Letter == 'C');
            player.Position = Collision.Resolve(world.Map, room.Center + new Vec2(0, -room.Box.H / 2f + 1.5f), World.PlayerRadius);
            Run(world, 0.3f);
            var pack = world.Enemies.Where(e => !e.IsDead && room.Contains(e.Position, 2)).ToList();
            Assert.AreEqual(1, pack.Count(e => e.Definition.Size == MonsterSize.L), string.Join(", ", pack.Select(e => e.Definition.Id)));
            Assert.IsTrue(pack.All(e => e.Elite == EliteRank.None));

            // The boss room: the slime mother in person, one affix, her own health (no big elite's twelvefold).
            var boss = layout.Room('B');
            player.Position = Collision.Resolve(world.Map, boss.Center + new Vec2(0, -boss.Box.H / 2f + 1.5f), World.PlayerRadius);
            Run(world, 0.3f);
            var master = world.Enemies.Single(e => e.Elite == EliteRank.Leader && world.DungeonAt(e.Position) == dungeon);
            Assert.AreEqual("slime_mother", master.Definition.Id);
            Assert.AreEqual(World.MasterAffixes, Enumerable.Range(0, 8).Count(i => ((int)master.Affixes & 1 << i) != 0));
            float zoneHealth = master.Definition.Health * (1 + World.ZoneHealthStep * (dungeon.Level - 1));
            float tough = (master.Affixes & RiftAffix.Tough) != 0 ? World.ToughShare : 1;
            Assert.AreEqual(zoneHealth * World.MasterHealth * tough, master.MaxHealth, 1);
            Assert.IsFalse(World.EliteName(master.Definition, EliteRank.Leader, RiftAffix.None).EndsWith("대장"), "a mid-boss leads under its own name");
            Assert.AreEqual("어미 슬라임", World.EliteName(master.Definition, EliteRank.Leader, RiftAffix.None));
        }

        [Test]
        public void ADungeonsMonstersGiveItsShareOfExperience()
        {
            var world = Hand(Biome.Cave, out var gate, "abandoned_mine");
            world.MonstersHold = true;
            var player = world.AddPlayer("a", Gear.Wearing("iron_sword"));
            var kind = Monsters.Shipped.Find("cave_beetle");
            int Gain(Vec2 at)
            {
                player.Position = at;
                var beetle = world.SpawnEnemy(kind, at + new Vec2(1.5f, 0));
                int before = player.Experience;
                world.Hurt(beetle, player, beetle.MaxHealth * 10);
                Run(world, 0.2f);
                return player.Experience - before;
            }
            int field = Gain(new Vec2(60.5f, 20.5f));
            player.Position = gate.Center;
            Assert.IsTrue(world.EnterDungeon(player.Id, gate.Index));
            Run(world, World.EnterSeconds + 0.1f);
            var dungeon = world.DungeonOf(gate);
            int inside = Gain(dungeon.Layout.Entrance);
            Assert.Greater(field, 0);
            Assert.AreEqual(Math.Max(1, field * Sheet.Find("abandoned_mine").Experience / 100), inside, 1, "the mine's share of what the same kind gives in the field");
        }

        // ---- Worlds already made ----

        // The worlds of version 3 (made since B1a) are laid exactly as the code of fe99e94 laid them: the trapdoors and
        // lairs are version 4's (the person's decision: a world keeps its layout). Hashes from fe99e94's generator.
        [TestCase(1UL, 16174971526392596917UL)]
        [TestCase(2UL, 12281051876954751881UL)]
        [TestCase(3UL, 11017054651573430369UL)]
        public void AWorldOfVersionThreeIsLaidAsBefore(ulong seed, ulong hash)
        {
            Assert.AreEqual(WorldGenerator.LandmarkVersion, WorldGenerator.NewWorldVersion);
            var map = WorldGenerator.Generate(seed, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped, Bosses.Shipped.Arenas, WorldGenerator.PropVersion,
                Tiles.Shipped.Terrain).Map;
            Assert.AreEqual(hash, RoomMapTests.Hash(map));
        }

        // ---- The camps' trapdoors ----

        static WorldHost Host(int version, ulong seed = 1) => new WorldHost(seed, Monsters.Shipped, Gear.Data,
            new HostConfig { MapSize = WorldGenerator.GameSize, MapVersion = version }, new WorldConfig(), Bosses.Shipped, Classes.Shipped, Tiles.Shipped, story: Tales.Shipped);

        [Test]
        public void SomeCampsOfAWorldOfVersionFourHaveATrapdoorThatOpensWhenTheirBandFallsAndLeadsToTheCellar()
        {
            foreach (int version in new[] { WorldGenerator.RoomVersion, WorldGenerator.PropVersion })
            {
                var old = Host(version);
                Assert.IsEmpty(old.World.Gates.Where(g => g.Cellar), $"a world of version {version} keeps its camps as they were");
                Assert.IsEmpty(old.Generated.Rooms.Lairs);
            }

            var host = Host(WorldGenerator.LandmarkVersion);
            var world = host.World;
            var rooms = host.Generated.Rooms;
            var cellars = world.Gates.Where(g => g.Cellar).ToList();
            int camps = rooms.Nodes.Count(n => n.Role == RoomRole.Elite);
            Assert.That(cellars.Count, Is.InRange(1, camps), "some camps");
            Assert.LessOrEqual(cellars.Count, camps * 0.5f, "about three in ten");
            // The gates keep their numbers; the trapdoors come after them. A client finds the same.
            Assert.IsTrue(world.Gates.TakeWhile(g => !g.Cellar).Count() == world.Gates.Count - cellars.Count);
            var again = DungeonPocket.Gates(WorldGenerator.Generate(1, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped, Bosses.Shipped.Arenas, WorldGenerator.LandmarkVersion,
                Tiles.Shipped.Terrain).Rooms, world.Map);
            CollectionAssert.AreEqual(world.Gates.Select(g => (g.X, g.Y, g.Cellar)), again.Select(g => (g.X, g.Y, g.Cellar)));
            foreach (var cellar in cellars)
            {
                Assert.AreEqual(TileType.Trapdoor, world.Map[cellar.X, cellar.Y]);
                Assert.AreEqual(RoomRole.Elite, rooms.Nodes[cellar.Room].Role);
                Assert.IsTrue(world.Map.IsSolid(cellar.X, cellar.Y), "it stands: its button takes one down");
                Assert.IsFalse(World.GateOpen(world.Map, cellar));
            }

            // At a shut trapdoor nobody goes down; its camp's band falls and it opens for good (a changed tile, saved and
            // sent); then down to the cellar of its biome.
            var player = world.AddPlayer("a", Gear.Wearing("obsidian_sword", "obsidian_armor"));
            player.Experience = Leveling.TotalFor(20);
            player.Health = player.Stats.MaxHealth;
            foreach (var cellar in cellars)
            {
                var camp = rooms.Nodes[cellar.Room];
                player.Position = Collision.Resolve(world.Map, TileMap.Center(cellar.X, cellar.Y - 1), World.PlayerRadius);
                Assert.IsFalse(world.EnterDungeon(player.Id, cellar.Index), "shut");
                world.MonstersHold = true;
                Run(world, 1);
                var elites = world.Enemies.Where(e => !e.IsDead && e.Elite != EliteRank.None && Vec2.Distance(e.Position, camp.Center) <= camp.Radius + 4).ToList();
                if (elites.Count == 0) continue;
                world.TakeTileChanges();
                foreach (var elite in elites) world.Hurt(elite, player, elite.MaxHealth * 10);
                var events = Run(world, 0.5f);
                Assert.AreEqual(TileType.OpenTrapdoor, world.Map[cellar.X, cellar.Y], "its band fell");
                Assert.IsTrue(world.TakeTileChanges().Any(c => c.X == cellar.X && c.Y == cellar.Y && c.Type == TileType.OpenTrapdoor), "a changed tile");
                Assert.IsTrue(events.Any(e => e.Type == SimEventType.Dungeon && e.Amount == World.DungeonCellarOpened));
                Assert.IsTrue(world.ChangedTiles().Any(c => c.X == cellar.X && c.Y == cellar.Y), "kept in the save");
                player.Position = Collision.Resolve(world.Map, TileMap.Center(cellar.X, cellar.Y - 1), World.PlayerRadius);
                Assert.IsTrue(world.EnterDungeon(player.Id, cellar.Index));
                Run(world, World.EnterSeconds + 0.1f);
                var dungeon = world.DungeonOf(cellar);
                Assert.AreEqual("cellar", dungeon.Definition.Id);
                Assert.AreEqual($"{BiomeMap.Name(cellar.Biome)}의 지하실", dungeon.Name);
                Assert.AreEqual($"cellar@{cellar.Biome.ToString().ToLowerInvariant()}", dungeon.Key);
                Assert.AreSame(dungeon, world.DungeonAt(player.Position));
                return;
            }
            Assert.Fail("no camp with a band to fell");
        }

        // ---- The lairs ----

        [Test]
        public void ANewWorldHasLairsWhereTheMidBossesStandInRoomsOpenSevenBySeven()
        {
            var host = Host(WorldGenerator.LandmarkVersion);
            var world = host.World;
            var rooms = host.Generated.Rooms;
            Assert.IsNotEmpty(rooms.Lairs);
            foreach (int index in rooms.Lairs)
            {
                var room = rooms.Nodes[index];
                Assert.IsTrue(room.Biome == Biome.Cave || room.Biome == Biome.Mushroom, $"{room.Biome}: a biome with a mid-boss of its own");
                Assert.AreEqual(RoomRole.Pack, room.Role);
                Assert.IsTrue(world.OpenSquareIn(room.Center, room.Radius), "seven by seven of open floor");
                Assert.GreaterOrEqual(world.ZoneLevelAt(room.Center), 2, "not on the first boss's way");
                Assert.AreEqual(MonsterSize.XL, world.LairBoss(room.Biome).Size);
            }
            CollectionAssert.AreEqual(rooms.Lairs, WorldGenerator.Generate(1, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped, Bosses.Shipped.Arenas,
                WorldGenerator.LandmarkVersion, Tiles.Shipped.Terrain).Rooms.Lairs, "the same seed, the same lairs");

            // Its pack: the mid-boss as a leader under its own name with one affix, a few of the biome's own, held to the room.
            var lair = rooms.Nodes[rooms.Lairs[0]];
            var player = world.AddPlayer("a", Gear.Wearing("obsidian_sword", "obsidian_armor"));
            player.Position = Collision.Resolve(world.Map, lair.Center + new Vec2(0, lair.Radius + 6), World.PlayerRadius);
            world.MonstersHold = true;
            Run(world, 1);
            var boss = world.Enemies.Single(e => !e.IsDead && e.Definition == world.LairBoss(lair.Biome));
            Assert.AreEqual(EliteRank.Leader, boss.Elite);
            Assert.AreEqual(1, Enumerable.Range(0, 8).Count(i => ((int)boss.Affixes & 1 << i) != 0));
            Assert.IsFalse(World.EliteName(boss.Definition, boss.Elite, boss.Affixes).EndsWith("대장"));
            Assert.LessOrEqual(Vec2.Distance(boss.Position, lair.Center), lair.Radius + 1);
            var with = world.Enemies.Where(e => !e.IsDead && e.PackId == boss.PackId && e != boss).ToList();
            Assert.That(with.Count, Is.InRange(World.LairMinionsLeast, World.LairMinionsMost + World.PackExtraAtNight * 2));
            Assert.IsTrue(with.All(e => !e.Definition.Big));
            float zoneHealth = boss.Definition.Health * (1 + World.ZoneHealthStep * (world.ZoneLevelAt(lair.Center) - 1));
            float tough = (boss.Affixes & RiftAffix.Tough) != 0 ? World.ToughShare : 1;
            Assert.AreEqual(zoneHealth * tough, boss.MaxHealth, 1, "an XL leader keeps its own health");
            // Nowhere else in the field an XL.
            foreach (var room in rooms.Nodes.Where(n => n.Role == RoomRole.Pack && !rooms.Lairs.Contains(n.Index)).Take(40))
                Assert.IsFalse(world.RoomPack(room, 0).Roster.Any(k => k.Size == MonsterSize.XL), $"{room.Index}");
        }

        // ---- A big beetle smashes props ----

        [Test]
        public void ABigBeetleChargingIntoAStalagmiteSmashesItAndIsDazed()
        {
            var world = new World(Maps.Room(30), TileMap.Center(15, 15), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });
            var player = world.AddPlayer("a", null);
            player.Position = TileMap.Center(27, 15);
            world.Map[25, 15] = TileType.Stalagmite;
            world.Map[25, 16] = TileType.PitProp;
            world.MonstersHold = true;
            var beetle = world.SpawnEnemy(Monsters.Shipped.Find("big_beetle"), TileMap.Center(20, 15));
            Assert.IsTrue(world.ForceAttack(beetle, "ram"));
            var events = Run(world, 2.2f);
            Assert.AreEqual(TileType.Floor, world.Map[25, 15], "smashed");
            Assert.IsTrue(world.TakeTileChanges().Any(c => c.X == 25 && c.Y == 15 && c.Type == TileType.Floor), "a changed tile");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxSmash));
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxStagger), "dazed all the same");
            Assert.IsTrue(World.Smashed(TileType.PitProp) && !World.Smashed(TileType.Boulder) && !World.Smashed(TileType.Wall));

            // An M beetle's charge smashes nothing.
            var small = new World(Maps.Room(30), TileMap.Center(15, 15), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });
            var other = small.AddPlayer("a", null);
            other.Position = TileMap.Center(27, 15);
            small.Map[25, 15] = TileType.Stalagmite;
            small.MonstersHold = true;
            var beetle2 = small.SpawnEnemy(Monsters.Shipped.Find("cave_beetle"), TileMap.Center(20, 15));
            var charge = beetle2.Definition.Attacks.FirstOrDefault(a => a.Shape == MonsterAttackShape.Charge);
            if (charge != null && small.ForceAttack(beetle2, charge.Id)) Run(small, 2.2f);
            Assert.AreEqual(TileType.Stalagmite, small.Map[25, 15]);
        }

        // ---- The trap rooms shut at their narrows ----

        [Test]
        public void ATrapRoomIsShutAtTheMouthsOfItsWays()
        {
            // A ragged room of radius 8 round (40, 40): open floor to radius 11, and two ways three wide out of it, east and
            // north, leading far.
            var map = new TileMap(100, 100, TileType.Wall);
            for (int y = 0; y < 100; y++)
                for (int x = 0; x < 100; x++)
                    if (Vec2.Distance(TileMap.Center(x, y), new Vec2(40.5f, 40.5f)) <= 11 + (x + y) % 3) map[x, y] = TileType.Floor;
            for (int x = 40; x < 95; x++)
                for (int y = 39; y <= 41; y++) map[x, y] = TileType.Floor;
            for (int y = 40; y < 95; y++)
                for (int x = 39; x <= 41; x++) map[x, y] = TileType.Floor;
            map[40, 40] = TileType.WoodChest;
            var room = new RoomNode(0, 40, 40, 8, Biome.Cave, TerrainStyle.Cave, RoomKind.Room);
            var floor = new HashSet<int>();
            var doors = new List<(int X, int Y)>();
            int fewest = int.MaxValue;
            foreach (float reach in World.TrapReaches)
            {
                World.CutByReach(map, room, 40, 40, reach, floor, doors);
                fewest = Math.Min(fewest, doors.Count);
            }
            Assert.IsTrue(World.CutByNarrows(map, room, 40, 40, floor, doors));
            Assert.AreEqual(6, doors.Count, "three across each way");
            Assert.Less(doors.Count, fewest, "fewer than the cut by distance");
            Assert.IsTrue(doors.All(d => d.X >= 39 && d.X <= 41 || d.Y >= 39 && d.Y <= 41), "on the ways");
            Assert.IsTrue(floor.Contains(30 * 100 + 40) && floor.Contains(48 * 100 + 33), "the room's floor is shut in");
            Assert.IsFalse(floor.Contains(40 * 100 + 90) || floor.Contains(90 * 100 + 40), "the ways' far ends are not");
            // Walked from the chest with the grates down, nothing past them is reached.
            foreach (var (x, y) in doors) map[x, y] = TileType.Grate;
            var seen = new HashSet<int>();
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue((40, 41));
            seen.Add(41 * 100 + 40);
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (!map.IsSolid(x + dx, y + dy) && seen.Add((y + dy) * 100 + x + dx)) queue.Enqueue((x + dx, y + dy));
            }
            Assert.IsFalse(seen.Contains(40 * 100 + 90) || seen.Contains(90 * 100 + 40));
        }
    }
}
