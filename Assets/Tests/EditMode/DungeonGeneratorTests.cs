using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The dungeons of dungeons.csv (Docs/WorldRework.md 5단계 던전의 내용, 묶음 5b): their sheets, the generator (the same
    // from the same seed, inside its slot, every room reached), and a run through one: the packs coming as the players
    // near, the key, the locked room, the master, the end chest (each player's own roll, the day's first run more), the
    // record kept in the world's save and the character's, the shortcut, the hidden room's crack, the safe rooms, and a
    // client laying the same and opening the same doors.
    public class DungeonGeneratorTests
    {
        static DungeonData Sheet => Tiles.Shipped.Terrain.Dungeons;

        static TileMap Pocket(int width = DungeonPocket.Span)
        {
            var map = new TileMap(width, 8, TileType.Wall);
            map.AddPocket(DungeonPocket.Rows);
            return map;
        }

        static ulong Hash(TileMap map, int slot)
        {
            ulong hash = 14695981039346656037UL;
            var (left, bottom, w, h) = DungeonPocket.Box(map, slot);
            for (int y = bottom; y < bottom + h; y++)
                for (int x = left; x < left + w; x++)
                {
                    hash ^= (byte)map[x, y];
                    hash *= 1099511628211UL;
                }
            return hash;
        }

        [Test]
        public void TheDungeonSheetsReadAndEveryGateHasADungeon()
        {
            Assert.IsTrue(Sheet.Success, string.Join("\n", Sheet.Errors));
            Assert.GreaterOrEqual(Sheet.Dungeons.Count, 4);
            Assert.AreEqual(3, Sheet.Graphs.Count);
            foreach (var dungeon in Sheet.Dungeons)
            {
                if (dungeon.Boss != null) Assert.IsNotNull(Monsters.Shipped.Find(dungeon.Boss), $"{dungeon.Id}: its master {dungeon.Boss}");
                Assert.AreEqual(PoiKind.Chest, Tiles.Shipped.Terrain.Pois.Find(dungeon.Chest).Kind, dungeon.Id);
            }
            // The design's two first: the dirt caves and the mushroom forest; every biome's gate leads somewhere.
            Assert.AreEqual("slime_cellar", Sheet.For(Biome.Cave).Id);
            Assert.AreEqual("spore_depths", Sheet.For(Biome.Mushroom).Id);
            foreach (Biome biome in Enum.GetValues(typeof(Biome))) Assert.IsNotNull(Sheet.For(biome), biome.ToString());
            Assert.AreEqual("deep_hollow@sand", Sheet.For(Biome.Sand).KeyFor(Biome.Sand));
            Assert.AreEqual("모래 동굴의 깊은 굴", Sheet.For(Biome.Sand).NameFor(Biome.Sand));
            // The chunks made for dungeons stay out of the world's rooms.
            var rooms = Tiles.Shipped.Terrain.Rooms;
            Assert.IsTrue(rooms.Chunks.Any(c => c.Dungeon && !c.Field));
            foreach (RoomRole role in Enum.GetValues(typeof(RoomRole)))
                foreach (Biome biome in Enum.GetValues(typeof(Biome)))
                    Assert.IsFalse(rooms.For(role, biome).Any(c => !c.Field), $"{role} {biome}");

            const string head = "id,name,biome,level,graph,rooms,boss,chest,minutes\n";
            const string graphs = "id,spine,branches,loop\nshort,E C R B X,T,shortcut\n";
            string Error(string row, string g = graphs) => string.Join("\n", DungeonSheet.Parse(head + row, g).Errors);
            Assert.AreSame(DungeonData.None, DungeonSheet.Parse("", ""));
            Assert.IsEmpty(Error("a,가,cave,1,short,5-6,elite:slime,chest_iron,5\n"));
            StringAssert.Contains("graph", Error("a,가,cave,1,long,5-6,elite,chest_iron,5\n"));
            StringAssert.Contains("rooms", Error("a,가,cave,1,short,3-4,elite,chest_iron,5\n"));
            StringAssert.Contains("boss", Error("a,가,cave,1,short,5,warden,chest_iron,5\n"));
            StringAssert.Contains("곁가지까지", Error("a,가,cave,1,short,17-18,elite,chest_iron,5\n"));
            StringAssert.Contains("E로 시작", Error("a,가,cave,1,short,5,elite,x,5\n", "id,spine,branches,loop\nshort,C R B X,,\n"));
            StringAssert.Contains("바로 앞", Error("a,가,cave,1,short,5,elite,x,5\n", "id,spine,branches,loop\nshort,E B C R X,,\n"));
            StringAssert.Contains("K(열쇠 방)", Error("a,가,cave,1,short,5,elite,x,5\n", "id,spine,branches,loop\nshort,E L C R B X,,\n"));
            StringAssert.Contains("branches에만", Error("a,가,cave,1,short,5,elite,x,5\n", "id,spine,branches,loop\nshort,E H C B X,,\n"));
            StringAssert.Contains("R(쉼터)", Error("a,가,cave,1,short,5,elite,x,5\n", "id,spine,branches,loop\nshort,E C C B X,,shortcut\n"));
            // A chest that is not a chest of pois.csv.
            var tiles = TileSheet.Parse("tile,name,hardness,material,count\n", Gear.Data, poisCsv: "", dungeonsCsv: head + "a,가,cave,1,short,5,elite,camp,5\n", dungeonGraphsCsv: graphs);
            StringAssert.Contains("상자가 아닙니다", string.Join("\n", tiles.Errors));
        }

        [Test]
        public void EveryDungeonIsLaidTheSameFromItsSeedInsideItsSlotAndReachesEveryRoom()
        {
            foreach (var definition in Sheet.Dungeons)
            {
                var biome = definition.Biome ?? Biome.Sand;
                int snakes = 0;
                for (ulong seed = 1; seed <= 200; seed++)
                {
                    var map = Pocket(2 * DungeonPocket.Span);
                    var other = Pocket(2 * DungeonPocket.Span);
                    var layout = DungeonGenerator.Lay(map, 1, seed * 7919, definition, biome, Tiles.Shipped.Terrain);
                    if (DungeonGenerator.LastTries > DungeonGenerator.Tries) snakes++;
                    var again = DungeonGenerator.Lay(other, 1, seed * 7919, definition, biome, Tiles.Shipped.Terrain);
                    string at = $"{definition.Id} seed {seed}";
                    Assert.AreEqual(Hash(map, 1), Hash(other, 1), $"{at}: the same tiles");
                    Assert.AreEqual(layout.Entrance, again.Entrance, at);
                    Assert.IsTrue(DungeonGenerator.Reaches(map, layout), $"{at}: the entrance reaches every room, door and thing");
                    // Its letters: the graph's, the spine as long as the sheet says, the side rooms after it.
                    var spine = layout.Nodes.Where(n => !n.Branch).Select(n => n.Letter).ToList();
                    Assert.That(spine.Count, Is.InRange(definition.MinRooms, definition.MaxRooms), at);
                    Assert.AreEqual('E', spine[0], at);
                    Assert.AreEqual("BX", new string(spine.Skip(spine.Count - 2).ToArray()), at);
                    Assert.AreEqual(definition.Graph.Branches.Count, layout.Nodes.Count(n => n.Branch), at);
                    Assert.AreEqual(2, layout.Exits.Count, at);
                    Assert.AreEqual(1, layout.Things.Count(t => t.End), $"{at}: an end chest");
                    Assert.AreEqual(definition.Chest, layout.Things.Single(t => t.End).Definition.Id, at);
                    Assert.AreEqual(definition.Graph.Shortcut, layout.ShortcutFrom != null, $"{at}: the shortcut");
                    Assert.IsNotEmpty(layout.BossDoor, $"{at}: the end shut until the boss falls");
                    Assert.AreEqual(definition.Graph.Spine.Contains('L'), layout.KeyDoor.Count > 0, at);
                    Assert.AreEqual(definition.Graph.Branches.Contains('H'), layout.Crack.Count > 0, at);
                    Assert.IsNotNull(layout.Room('B').Elite, $"{at}: the master's spot");
                    // Every pack room has its spots, and the boxes stand apart in their own cells.
                    foreach (var room in layout.Nodes.Where(n => DungeonSheet.HasPack(n.Letter) && n.Letter != 'B' && n.Letter != 'M'))
                        Assert.GreaterOrEqual(room.Monsters.Count, 4, $"{at}: {room.Letter} {room.Chunk}");
                    // Nothing open outside the slot's inside.
                    var (ix, iy) = DungeonPocket.Inside(map, 1);
                    var (left, bottom, w, h) = DungeonPocket.Box(map, 1);
                    for (int y = bottom; y < bottom + h; y++)
                        for (int x = left; x < left + w; x++)
                            if (!map.IsSolid(x, y) || map[x, y] != TileType.Wall && map[x, y] != TileType.Torch)
                                Assert.IsTrue(x > ix && y > iy && x < ix + DungeonPocket.Inner - 1 && y < iy + DungeonPocket.Inner - 1, $"{at}: {map[x, y]} at {x}, {y}");
                    Assert.IsTrue(Enumerable.Range(0, DungeonPocket.Span).All(x => Enumerable.Range(map.Height, DungeonPocket.Rows).All(y => map[x, y] == TileType.Wall)),
                        $"{at}: the other slot untouched");
                }
                Assert.LessOrEqual(snakes, 2, $"{definition.Id}: laid in a snake {snakes} times in 200");
            }
        }

        // ---- A run ----

        // A plain floor of 320 by 40 with the pocket above it, a gate of the biome on it, and its dungeon chosen.
        static World Hand(Biome biome, out DungeonGate gate, string dungeon = null, int zone = 1)
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
            if (dungeon != null) Assert.IsTrue(world.ChooseDungeon(gate.Index, dungeon));
            return world;
        }

        static void Run(World world, float seconds)
        {
            for (int i = World.Ticks(seconds); i > 0; i--) world.Step();
        }

        static Player At(World world, string name, Vec2 at)
        {
            var player = world.AddPlayer(name, Gear.Wearing("iron_sword"));
            player.Position = at;
            return player;
        }

        static DungeonInstance GoIn(World world, DungeonGate gate, params Player[] players)
        {
            foreach (var player in players)
            {
                player.Position = gate.Center;
                Assert.IsTrue(world.EnterDungeon(player.Id, gate.Index));
            }
            Run(world, World.EnterSeconds + 0.1f);
            var dungeon = world.DungeonOf(gate);
            foreach (var player in players) Assert.AreSame(dungeon, world.DungeonAt(player.Position));
            return dungeon;
        }

        // Puts the players in the room, lets its pack come, and fells all of it.
        static List<Enemy> Clear(World world, DungeonInstance dungeon, DungeonRoom room, params Player[] players)
        {
            foreach (var player in players) player.Position = Collision.Resolve(world.Map, room.Center + new Vec2(0, -room.Box.H / 2f + 1.5f), World.PlayerRadius);
            Run(world, 0.3f);
            Assert.IsTrue(dungeon.RoomWoken(room.Index), $"{room.Letter}: woken");
            var pack = world.Enemies.Where(e => !e.IsDead && room.Contains(e.Position, 2)).ToList();
            foreach (var enemy in pack) enemy.Health = 0;
            Run(world, 0.3f);
            foreach (var enemy in world.Enemies.Where(e => !e.IsDead && room.Contains(e.Position, 3)).ToList()) enemy.Health = 0;
            Run(world, 0.3f);
            Assert.IsTrue(dungeon.RoomCleared(room.Index), $"{room.Letter}: cleared");
            return pack;
        }

        [Test]
        public void ARunGoesRoomByRoomToTheMasterAndTheEndChestAndIsRecorded()
        {
            var world = Hand(Biome.Ice, out var gate, "frozen_crypt", zone: 3);
            world.MonstersHold = true;
            var a = At(world, "a", gate.Center);
            var b = At(world, "b", gate.Center);
            var dungeon = GoIn(world, gate, a, b);
            var layout = dungeon.Layout;
            Assert.AreEqual("얼어붙은 묘", dungeon.Name);
            Assert.AreEqual("frozen_crypt", dungeon.Key);
            Assert.AreEqual(Math.Max(3, world.ZoneLevelAt(gate.Center)), dungeon.Level, "its row's level, or the gate's zone's if higher");
            Assert.GreaterOrEqual(dungeon.Started, 0);
            // Standing at the entrance, nothing has come yet; the grates are shut.
            Assert.IsEmpty(world.Enemies.Where(e => world.DungeonAt(e.Position) == dungeon && !layout.Room('C').Contains(e.Position, 20)));
            foreach (var (x, y) in layout.KeyDoor.Concat(layout.BossDoor)) Assert.AreEqual(TileType.Grate, world.Map[x, y]);
            Assert.IsNull(World.ThingNear(dungeon, layout.Things.Single(t => t.End).Center), "the end chest waits for the master");

            // Room by room along the spine; the key room's pack opens the locked room to whoever comes to its door.
            foreach (var room in layout.Nodes.Where(n => !n.Branch && DungeonSheet.HasPack(n.Letter) && n.Letter != 'B'))
            {
                if (room.Letter == 'L')
                {
                    Assert.IsTrue(dungeon.Has(DungeonFlags.KeyTaken));
                    Assert.IsFalse(dungeon.Has(DungeonFlags.KeyDoorOpen), "not open until someone comes to it");
                    var (dx, dy) = layout.KeyDoor[0];
                    a.Position = Collision.Resolve(world.Map, TileMap.Center(dx, dy) + (rooms(layout, room).Center - room.Center).Normalized * 2f, World.PlayerRadius);
                    Run(world, 0.1f);
                    Assert.IsTrue(dungeon.Has(DungeonFlags.KeyDoorOpen));
                    Assert.IsTrue(layout.KeyDoor.All(d => world.Map[d.X, d.Y] == TileType.Floor));
                }
                var pack = Clear(world, dungeon, room, a, b);
                if (room.Letter == 'M')
                {
                    Assert.AreEqual(World.ChampionCount, pack.Count(e => e.Elite == EliteRank.Champion));
                    continue;
                }
                // Four to eight, and two more for the second player.
                Assert.That(pack.Count, Is.InRange(World.DungeonPackLeast + World.PackExtraPerPlayer, World.DungeonPackMost + World.PackExtraPerPlayer), $"{room.Letter} {pack.Count}");
                Assert.IsTrue(pack.All(e => e.Elite == EliteRank.None));
            }
            Assert.IsTrue(dungeon.Has(DungeonFlags.KeyTaken));

            // The master: a frost wisp leader with two affixes and twelve times its zone's health, more for the second player.
            var boss = layout.Room('B');
            foreach (var p in new[] { a, b }) p.Position = Collision.Resolve(world.Map, boss.Center + new Vec2(0, -boss.Box.H / 2f + 1.5f), World.PlayerRadius);
            Run(world, 0.3f);
            var master = world.Enemies.Single(e => e.Elite == EliteRank.Leader && world.DungeonAt(e.Position) == dungeon);
            Assert.AreEqual("frost_wisp", master.Definition.Id);
            Assert.AreEqual(World.BigEliteAffixes, Enumerable.Range(0, 8).Count(i => ((int)master.Affixes & 1 << i) != 0));
            float zoneHealth = master.Definition.Health * (1 + World.ZoneHealthStep * (dungeon.Level - 1));
            float tough = (master.Affixes & RiftAffix.Tough) != 0 ? World.ToughShare : 1;
            Assert.AreEqual(zoneHealth * World.BigEliteHealth * tough * (1 + World.BigElitePerPlayer), master.MaxHealth, 1);
            Assert.GreaterOrEqual(world.Enemies.Count(e => !e.IsDead && boss.Contains(e.Position, 2)), 1 + World.MinMinions);
            Assert.AreEqual("서리 정령 대장", World.EliteName(master.Definition, EliteRank.Leader, RiftAffix.None));

            // Nobody in its room a while: it is whole again.
            master.Health = master.MaxHealth / 2;
            foreach (var p in new[] { a, b }) p.Position = layout.Entrance;
            Run(world, World.BossResetSeconds + 0.5f);
            Assert.AreEqual(master.MaxHealth, master.Health);

            foreach (var p in new[] { a, b }) p.Position = Collision.Resolve(world.Map, boss.Center + new Vec2(0, -boss.Box.H / 2f + 1.5f), World.PlayerRadius);
            Run(world, 0.2f);
            master.Health = 0;
            Run(world, 0.3f);
            Assert.IsTrue(dungeon.Has(DungeonFlags.BossDown));
            Assert.IsTrue(layout.BossDoor.All(d => world.Map[d.X, d.Y] == TileType.Floor), "the end room is open");
            var events = world.TakeEvents().Where(e => e.Type == SimEventType.Dungeon).Select(e => e.Amount).ToList();
            CollectionAssert.IsSupersetOf(events, new[] { World.DungeonKey, World.DungeonDoorOpened, World.DungeonBossWoke, World.DungeonBossDown, World.DungeonRoomCleared });

            // The end chest: each one there their own roll, and the world's first run of the day there gives more.
            var chest = layout.Things.Single(t => t.End);
            a.Position = Collision.Resolve(world.Map, chest.Center + new Vec2(0, -1.2f), World.PlayerRadius);
            b.Position = a.Position + new Vec2(1, 0);
            int aItems = a.Inventory.Items.Count(), bItems = b.Inventory.Items.Count(), aRunes = a.Inventory.Runes.Values.Sum();
            Assert.AreSame(chest, World.ThingNear(dungeon, a.Position));
            Assert.IsTrue(world.UseDungeonThing(a.Id, chest.Index));
            Assert.IsFalse(world.UseDungeonThing(b.Id, chest.Index), "once");
            int rolled = Tiles.Shipped.Terrain.Pois.Find("chest_gold").Reward(PoiRewardKind.Items).Min;
            Assert.AreEqual(aItems + rolled + World.FirstRunItems, a.Inventory.Items.Count());
            Assert.AreEqual(bItems + rolled + World.FirstRunItems, b.Inventory.Items.Count(), "the other's own roll");
            Assert.GreaterOrEqual(a.Inventory.Runes.Values.Sum(), aRunes + World.FirstRunRunes);
            Assert.AreEqual(TileType.OpenChest, world.Map[chest.X, chest.Y]);
            Assert.AreEqual(DungeonState.Done, dungeon.State);
            Assert.IsTrue(dungeon.Has(DungeonFlags.FirstToday | DungeonFlags.BestTime | DungeonFlags.ChestOpen));
            Assert.Greater(dungeon.ClearSeconds, 0);
            Assert.AreEqual(1, a.Records[(RecordKind.Dungeon, "frozen_crypt")]);
            Assert.AreEqual(1, b.Records[(RecordKind.Dungeon, "frozen_crypt")]);
            var record = world.DungeonRecordOf("frozen_crypt");
            Assert.AreEqual((1, dungeon.ClearSeconds, world.Today, 1), (record.Entered, record.BestSeconds, record.LastClearDay, record.Clears));

            // The world's save keeps the record; the character's the runs (as a record of the bestiary's kind).
            var save = WorldSave.Read(WorldSave.Write(world, 11, 5, WorldGenerator.RoomVersion), Tiles.Shipped, Gear.Data);
            var kept = save.DungeonRecords.Single();
            Assert.AreEqual(("frozen_crypt", 1, record.BestSeconds, record.LastClearDay, 1), (kept.Key, kept.Entered, kept.BestSeconds, kept.LastClearDay, kept.Clears));
            var character = CharacterSave.Read(CharacterSave.Write(a.Equipment, a.Inventory, records: a.Records.Select(r => (r.Key.Kind.ToString(), r.Key.Id, r.Value))), Gear.Data);
            CollectionAssert.Contains(character.Records.ToList(), ("Dungeon", "frozen_crypt", 1));
            // On the wire by its row's index (RecordsMessage), and a row of any biome with its biome.
            Assert.AreEqual("frozen_crypt", Sheet.KeyOfIndex(Sheet.IndexOfKey("frozen_crypt")));
            Assert.AreEqual("deep_hollow@abyss", Sheet.KeyOfIndex(Sheet.IndexOfKey("deep_hollow@abyss")));
            Assert.AreEqual(-1, Sheet.IndexOfKey("gate3"));

            // The next run the same day: no more of the day's reward, and the next dungeon is another.
            var again = Hand(Biome.Ice, out var gateAgain, "frozen_crypt", zone: 3);
            again.RestoreDungeons(save.DungeonRecords);
            var c = At(again, "c", gateAgain.Center);
            var second = GoIn(again, gateAgain, c);
            Assert.AreEqual(1, second.Serial);
            Assert.AreNotEqual(dungeon.Seed, second.Seed);
            Assert.AreEqual(record.BestSeconds, again.DungeonRecordOf("frozen_crypt").BestSeconds);
        }

        static DungeonRoom rooms(DungeonLayout layout, DungeonRoom room) => layout.Nodes[room.Parent];

        [Test]
        public void TheShortcutTheHiddenCrackTheSafeRoomsAndTheFire()
        {
            var world = Hand(Biome.Lava, out var gate, "ember_forge", zone: 3);
            world.MonstersHold = true;
            var a = At(world, "a", gate.Center);
            var dungeon = GoIn(world, gate, a);
            var layout = dungeon.Layout;

            // The entrance's stone does nothing until the rest room's was touched.
            var (fx, fy) = layout.ShortcutFrom.Value;
            var (tx, ty) = layout.ShortcutTo.Value;
            a.Position = Collision.Resolve(world.Map, TileMap.Center(fx, fy) + (layout.Room('E').Center - TileMap.Center(fx, fy)).Normalized * 1.2f, World.PlayerRadius);
            Run(world, 0.1f);
            Assert.IsTrue(layout.Room('E').Contains(a.Position), "still at the entrance");
            var rest = layout.Room('R');
            a.Position = Collision.Resolve(world.Map, TileMap.Center(tx, ty) + (rest.Center - TileMap.Center(tx, ty)).Normalized * 1.2f, World.PlayerRadius);
            Run(world, 0.1f);
            Assert.IsTrue(dungeon.Has(DungeonFlags.ShortcutOpen));
            a.Position = layout.Entrance;
            Run(world, 0.1f);
            a.Position = Collision.Resolve(world.Map, TileMap.Center(fx, fy) + (layout.Room('E').Center - TileMap.Center(fx, fy)).Normalized * 1.2f, World.PlayerRadius);
            Run(world, 0.1f);
            Assert.IsTrue(rest.Contains(a.Position, 1), "taken on to the rest room");

            // The fire heals; and nothing comes into the rest room or the entrance.
            a.Health = 10;
            var fire = layout.Things.Single(t => t.Kind == DungeonThingKind.Fire);
            a.Position = Collision.Resolve(world.Map, fire.Center + new Vec2(0, -1.5f), World.PlayerRadius);
            Run(world, 1);
            Assert.Greater(a.Health, 10 + a.Stats.MaxHealth * 0.05f);
            var slime = world.SpawnEnemy(Monsters.Shipped.Find("slime"), rest.Center + new Vec2(1, 1));
            Run(world, 0.1f);
            Assert.IsFalse(rest.Contains(slime.Position), "put out of the rest room");

            // The hidden room's crack opens to a blow, as the dungeon's progress, not a changed tile.
            var hidden = layout.Room('H');
            var (cx, cy) = layout.Crack[layout.Crack.Count / 2];
            var parent = layout.Nodes[hidden.Parent];
            var toward = new Vec2(Math.Sign(hidden.Cell.X - parent.Cell.X), Math.Sign(hidden.Cell.Y - parent.Cell.Y));
            a.Position = TileMap.Center(cx, cy) - toward * 1.0f;
            a.Facing = toward;
            Assert.AreEqual(TileType.CrackedWall, world.Map[cx, cy]);
            world.SetAttacking(a.Id, true);
            Run(world, 1.5f);
            world.SetAttacking(a.Id, false);
            Assert.IsTrue(dungeon.Has(DungeonFlags.CrackOpen));
            Assert.IsTrue(layout.Crack.All(t => world.Map[t.X, t.Y] == TileType.Floor));
            Assert.IsEmpty(world.ChangedTiles(), "nothing of the pocket is a changed tile");
            // Its chest: a hidden room's row.
            var cache = layout.Things.Single(t => t.Room == hidden.Index);
            Assert.AreEqual(RoomRole.Hidden, cache.Definition.Role);
        }

        [Test]
        public void TheClientLaysTheSameDungeonAndOpensTheSameDoors()
        {
            var host = new WorldHost(3, Monsters.Shipped, Gear.Data, new HostConfig { MapSize = 512, MapVersion = WorldGenerator.RoomVersion }, new WorldConfig { EnemyTarget = 0 },
                Bosses.Shipped, Classes.Shipped, Tiles.Shipped, story: Tales.Shipped);
            var alice = new WorldClient("key-a", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped) { Story = Tales.Shipped };
            void Pass(float seconds)
            {
                for (int i = World.Ticks(seconds); i >= 0; i--)
                {
                    host.Tick();
                    foreach (var message in host.TakeOutgoing()) if (message.Connection == 1) alice.Receive(message.Data);
                    alice.AdvanceRenderClock(World.TickSeconds);
                }
            }
            host.Receive(1, alice.JoinMessage());
            Pass(0.2f);
            var world = host.World;
            world.MonstersHold = true;
            var gate = world.Gates[0];
            var a = world.FindPlayer(alice.PlayerId);
            a.Position = Collision.Resolve(world.Map, gate.Center, World.PlayerRadius);
            a.Corrections++;
            Pass(0.2f);
            Assert.AreEqual(world.DungeonFor(gate).NameFor(gate.Biome), alice.DungeonNameOf(alice.Gates[0]), "the gate's button names its dungeon");
            host.Receive(1, alice.EnterDungeonCommand(alice.GateNear()));
            Pass(World.EnterSeconds + 0.3f);
            var dungeon = world.DungeonAt(a.Position);
            var mine = alice.DungeonHere;
            Assert.IsNotNull(mine);
            Assert.AreEqual(dungeon.Name, mine.Name);
            Assert.AreEqual(dungeon.Layout.Nodes.Count, mine.Layout.Nodes.Count);
            Assert.AreEqual(Hash(world.Map, dungeon.Slot), Hash(alice.Map, dungeon.Slot));
            Assert.AreEqual(dungeon.Started, mine.Started);

            // The master falls on the host: the end room's grate opens on the client too.
            var boss = dungeon.Layout.Room('B');
            a.Position = Collision.Resolve(world.Map, boss.Center + new Vec2(0, -boss.Box.H / 2f + 1.5f), World.PlayerRadius);
            a.Corrections++;
            Pass(0.4f);
            alice.TakeEvents();
            foreach (var enemy in world.Enemies.Where(e => boss.Contains(e.Position, 3)).ToList()) enemy.Health = 0;
            Pass(0.4f);
            Assert.IsTrue(mine.Has(DungeonFlags.BossDown));
            // The dungeon's own turns come from no one; they still reach whoever is in it (the screen's "주인을 쓰러뜨렸다").
            var heard = alice.TakeEvents().Where(e => e.Type == SimEventType.Dungeon).Select(e => e.Amount).ToList();
            CollectionAssert.Contains(heard, World.DungeonBossDown, "the master's fall is told to the client");
            CollectionAssert.Contains(heard, World.DungeonDoorOpened, "and the end room's door opening");
            Assert.AreEqual(Hash(world.Map, dungeon.Slot), Hash(alice.Map, dungeon.Slot), "the same doors open");
            Assert.IsTrue(mine.RoomCleared(boss.Index));

            // The end chest by the client's own button.
            var chest = mine.Layout.Things.Single(t => t.End);
            a.Position = Collision.Resolve(world.Map, chest.Center + new Vec2(0, -1.2f), World.PlayerRadius);
            a.Corrections++;
            Pass(0.3f);
            Assert.AreEqual(chest.Index, alice.DungeonThingNear()?.Index);
            host.Receive(1, alice.UseDungeonThingCommand(alice.DungeonThingNear()));
            Pass(0.3f);
            Assert.AreEqual(DungeonState.Done, mine.State);
            Assert.Greater(mine.ClearSeconds, 0);
            Assert.AreEqual(TileType.OpenChest, alice.Map[chest.X, chest.Y]);
            Assert.AreEqual(Hash(world.Map, dungeon.Slot), Hash(alice.Map, dungeon.Slot));
        }
    }
}
