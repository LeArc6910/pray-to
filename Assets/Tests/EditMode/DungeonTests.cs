using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The dungeons' pocket and its gates (Docs/WorldRework.md 5단계, 묶음 5a): rows of rock above the world, a dungeon laid
    // in a slot of them for a gate, going in and out, getting up at its entrance, sharing it, it going back to rock, the
    // wire, the save, and the world itself made as before.
    public class DungeonTests
    {
        // A plain floor of 320 by 40 with the pocket above it: two slots.
        static World Hand(out DungeonGate first, out DungeonGate second)
        {
            var map = new TileMap(320, 40, TileType.Floor);
            for (int x = 0; x < map.Width; x++)
            {
                map[x, 0] = TileType.Wall;
                map[x, map.Height - 1] = TileType.Wall;
            }
            map.AddPocket(DungeonPocket.Rows);
            var world = new World(map, new Vec2(10.5f, 10.5f), Monsters.Shipped, 7, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            first = world.AddGate(40, 20, Biome.Mushroom);
            second = world.AddGate(200, 20, Biome.Ice);
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

        static ulong PocketHash(TileMap map)
        {
            ulong hash = 14695981039346656037UL;
            for (int y = map.Height; y < map.Rows; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    hash ^= (byte)map[x, y];
                    hash *= 1099511628211UL;
                }
            return hash;
        }

        [Test]
        public void AWorldOfRoomsHasThePocketAboveItAndIsMadeAsBefore()
        {
            // The hashes of the world's rows were taken before the pocket was added (a53f48d): its rows are laid last.
            var big = WorldGenerator.Generate(1, 1024, 1024, version: WorldGenerator.RoomVersion);
            Assert.AreEqual(7327942302621511750UL, RoomMapTests.Hash(big.Map));
            Assert.AreEqual(3036046982128852288UL, RoomMapTests.Hash(WorldGenerator.Generate(2, 512, 512, version: WorldGenerator.RoomVersion).Map));
            Assert.AreEqual(3143973646108956218UL, RoomMapTests.Hash(WorldGenerator.Generate(5, 256, 256, version: WorldGenerator.RoomVersion).Map));
            Assert.AreEqual(1024 + DungeonPocket.Rows, big.Map.Rows);
            Assert.AreEqual(DungeonPocket.MaxSlots, DungeonPocket.SlotsOf(big.Map));
            for (int y = big.Map.Height; y < big.Map.Rows; y++)
                for (int x = 0; x < big.Map.Width; x++)
                    Assert.AreEqual(TileType.Wall, big.Map[x, y], "the pocket is rock until a dungeon is laid");
            // The caves of version 1, and a map too narrow for a slot, have none.
            Assert.IsFalse(WorldGenerator.Generate(1, 1024, 1024).Map.HasPocket);
            Assert.IsFalse(WorldGenerator.Generate(1, 128, 128, version: WorldGenerator.RoomVersion).Map.HasPocket);
            Assert.AreEqual(1, DungeonPocket.SlotsOf(WorldGenerator.Generate(5, 256, 256, version: WorldGenerator.RoomVersion).Map));
            // A gate room of each biome big enough, each gate on open floor between its posts.
            big = WorldGenerator.Generate(1, 1024, 1024, Tales.Shipped, Bosses.Staged.Arenas, WorldGenerator.RoomVersion, Tiles.Shipped.Terrain);
            var gates = DungeonPocket.Gates(big.Rooms, big.Map);
            Assert.GreaterOrEqual(gates.Count, 6);
            foreach (var gate in gates)
            {
                Assert.IsFalse(big.Map.IsSolid(gate.X, gate.Y), $"gate {gate.Index} at {gate.X}, {gate.Y}");
                Assert.AreEqual(big.Rooms.Nodes[gate.Room].Biome, gate.Biome);
            }
        }

        [Test]
        public void EveryDungeonLaidIsWalkedFromItsEntranceToItsDoors()
        {
            var map = new TileMap(640, 32, TileType.Floor);
            map.AddPocket(DungeonPocket.Rows);
            for (ulong seed = 1; seed <= 200; seed++)
            {
                int slot = (int)(seed % 4);
                var layout = DungeonPocket.Carve(map, slot, seed);
                var start = (TileMap.Cell(layout.Entrance.X), TileMap.Cell(layout.Entrance.Y));
                Assert.AreEqual(slot, DungeonPocket.SlotAt(map, layout.Entrance), $"seed {seed}");
                Assert.IsFalse(Collision.Overlaps(map, layout.Entrance, World.PlayerRadius), $"seed {seed}: the entrance is open");
                var seen = new HashSet<(int, int)> { start };
                var open = new Queue<(int X, int Y)>(seen);
                while (open.Count > 0)
                {
                    var (x, y) = open.Dequeue();
                    foreach (var next in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                        if (!map.IsSolid(next.Item1, next.Item2) && seen.Add(next)) open.Enqueue(next);
                }
                Assert.GreaterOrEqual(layout.Rooms.Count, 4);
                Assert.AreEqual(2, layout.Exits.Count);
                foreach (var exit in layout.Exits) Assert.IsTrue(seen.Contains(exit), $"seed {seed}: the door at {exit}");
                foreach (var (x, y) in seen)
                {
                    Assert.AreEqual(slot, DungeonPocket.SlotAt(map, x, y), $"seed {seed}: floor out of its slot at {x}, {y}");
                    var inside = DungeonPocket.Inside(map, slot);
                    Assert.IsTrue(x > inside.X && y > inside.Y && x < inside.X + DungeonPocket.Inner - 1 && y < inside.Y + DungeonPocket.Inner - 1,
                        $"seed {seed}: floor in the slot's wall at {x}, {y}");
                }
                DungeonPocket.Clear(map, slot);
            }
            Assert.AreEqual(0, Enumerable.Range(map.Height, DungeonPocket.Rows).Count(y => Enumerable.Range(0, map.Width).Any(x => !map.IsSolid(x, y))), "back to rock");
        }

        [Test]
        public void APlayerGoesInByTheGateAndOutByADoor()
        {
            var world = Hand(out var gate, out _);
            var player = At(world, "a", gate.Center);
            Assert.AreSame(gate, world.GateNear(player.Position));
            Assert.IsTrue(world.EnterDungeon(player.Id, gate.Index));
            var dungeon = world.DungeonOf(gate);
            Assert.IsNotNull(dungeon, "made as soon as someone starts going in");
            Assert.AreEqual(Biome.Mushroom, world.Biomes.At(dungeon.Layout.Entrance), "the pocket's slot takes the dungeon's biome");
            Assert.AreEqual("포자 굴", dungeon.Name, "its row of dungeons.csv (묶음 5b)");
            Run(world, World.EnterSeconds - 0.2f);
            Assert.IsFalse(world.InPocket(player.Position), "still gathering");
            Run(world, 0.3f);
            Assert.IsTrue(world.InPocket(player.Position));
            Assert.AreSame(dungeon, world.DungeonAt(player.Position));
            Assert.Less(Vec2.Distance(player.Position, dungeon.Layout.Entrance), 1f);
            Assert.AreEqual(dungeon.Level, world.ZoneLevelAt(player.Position), "the dungeon's level, not the far rows'");
            Assert.IsTrue(world.LeftAlone(player), "the pacing director leaves the pocket alone");
            Assert.IsNull(world.GateNear(player.Position));

            // Nothing is dug or built here.
            var wall = dungeon.Layout.Rooms[0];
            player.Position = TileMap.Center(wall.Left, wall.Bottom + 3);
            player.Facing = new Vec2(-1, 0);
            Assert.AreEqual(TileType.Wall, world.Map[wall.Left - 1, wall.Bottom + 3]);
            player.Equipment.Equip(Gear.Plain("copper_pickaxe"));
            world.SetAttacking(player.Id, true);
            Run(world, 4);
            world.SetAttacking(player.Id, false);
            Assert.AreEqual(TileType.Wall, world.Map[wall.Left - 1, wall.Bottom + 3], "the pocket's rock is never dug");
            var torch = Gear.Data.Materials.First(m => m.Places == TileType.Torch);
            player.Inventory.AddMaterial(torch, 3);
            player.Facing = new Vec2(1, 0);
            Assert.IsFalse(world.PlaceTile(player.Id, torch), "nor built on");
            Assert.IsEmpty(world.ChangedTiles());

            // Out by the door of the entrance room: at the gate again.
            var door = dungeon.Layout.Exits[0];
            player.Position = TileMap.Center(door.X, door.Y);
            Assert.IsTrue(world.LeaveDungeon(player.Id, byMenu: false));
            Run(world, World.LeaveSeconds + 0.1f);
            Assert.IsFalse(world.InPocket(player.Position));
            Assert.Less(Vec2.Distance(player.Position, gate.Center), 1f);
            var events = world.TakeEvents().Where(e => e.Type == SimEventType.Dungeon).Select(e => e.Amount).ToList();
            CollectionAssert.AreEqual(new[] { World.DungeonEntering, World.DungeonEntered, World.DungeonLeaving, World.DungeonLeft }, events);

            // From the menu, anywhere in it, it takes longer.
            Assert.IsTrue(world.EnterDungeon(player.Id, gate.Index));
            Run(world, World.EnterSeconds + 0.1f);
            Assert.AreSame(dungeon, world.DungeonAt(player.Position), "the same dungeon while it stands");
            Assert.IsFalse(world.LeaveDungeon(player.Id, byMenu: false), "not by a door away from one");
            Assert.IsTrue(world.LeaveDungeon(player.Id, byMenu: true));
            Run(world, World.MenuLeaveSeconds - 0.2f);
            Assert.IsTrue(world.InPocket(player.Position));
            Run(world, 0.3f);
            Assert.IsFalse(world.InPocket(player.Position));
        }

        [Test]
        public void SteppingOffOrAHitStopsGoingIn()
        {
            var world = Hand(out var gate, out _);
            var player = At(world, "a", gate.Center);
            Assert.IsFalse(world.EnterDungeon(player.Id, 5), "no such gate");
            player.Position = gate.Center + new Vec2(4, 0);
            Assert.IsFalse(world.EnterDungeon(player.Id, gate.Index), "too far from it");
            player.Position = gate.Center;
            Assert.IsTrue(world.EnterDungeon(player.Id, gate.Index));
            Run(world, 0.5f);
            player.Position = gate.Center + new Vec2(4, 0);
            Run(world, 1.5f);
            Assert.IsFalse(world.InPocket(player.Position), "stepped off the gate");
            player.Position = gate.Center;
            Assert.IsTrue(world.EnterDungeon(player.Id, gate.Index));
            Run(world, 0.5f);
            world.Hurt(player, 5);
            Run(world, 1.5f);
            Assert.IsFalse(world.InPocket(player.Position), "a hit stops it");
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.Dungeon && e.Amount == World.DungeonBroken));
        }

        [Test]
        public void OneWhoFallsInsideGetsUpAtItsEntranceEveryTime()
        {
            var world = Hand(out var gate, out _);
            var player = At(world, "a", gate.Center);
            world.EnterDungeon(player.Id, gate.Index);
            Run(world, World.EnterSeconds + 0.1f);
            var dungeon = world.DungeonAt(player.Position);
            var far = dungeon.Layout.Rooms[dungeon.Layout.Rooms.Count - 1];
            for (int fall = 0; fall < 5; fall++)
            {
                player.Position = TileMap.Center(far.Left + far.W / 2, far.Bottom + far.H / 2);
                world.Hurt(player, 100000);
                Assert.IsTrue(player.IsDead);
                if (fall % 2 == 1) world.GetUp(player.Id);
                Run(world, 6);
                Assert.IsFalse(player.IsDead, $"fall {fall}");
                Assert.AreSame(dungeon, world.DungeonAt(player.Position), $"fall {fall}: still in the dungeon");
                Assert.Less(Vec2.Distance(player.Position, dungeon.Layout.Entrance), 1f, $"fall {fall}: at its entrance");
            }
            Assert.AreEqual(5, world.TakeEvents().Count(e => e.Type == SimEventType.Dungeon && e.Amount == World.DungeonRose));
            // Out in the world, a fall is as before: home.
            world.LeaveDungeon(player.Id, byMenu: true);
            Run(world, World.MenuLeaveSeconds + 0.1f);
            world.Hurt(player, 100000);
            Run(world, 6);
            Assert.IsFalse(world.InPocket(player.Position));
            Assert.AreEqual(world.Spawn, player.Position);
        }

        [Test]
        public void PlayersAtOneGateShareItsDungeonAndOtherGatesTakeOtherSlots()
        {
            var world = Hand(out var gate, out var other);
            var a = At(world, "a", gate.Center);
            var b = At(world, "b", gate.Center + new Vec2(0.5f, 0));
            var c = At(world, "c", other.Center);
            Assert.IsTrue(world.EnterDungeon(a.Id, gate.Index));
            Run(world, 0.5f);
            Assert.IsTrue(world.EnterDungeon(b.Id, gate.Index));
            Assert.IsTrue(world.EnterDungeon(c.Id, other.Index));
            Run(world, World.EnterSeconds + 0.1f);
            var shared = world.DungeonOf(gate);
            Assert.AreSame(shared, world.DungeonAt(a.Position));
            Assert.AreSame(shared, world.DungeonAt(b.Position));
            Assert.AreEqual(2, world.PlayersIn(shared).Count());
            var apart = world.DungeonAt(c.Position);
            Assert.IsNotNull(apart);
            Assert.AreNotEqual(shared.Slot, apart.Slot);
            Assert.AreEqual(Biome.Ice, world.Biomes.At(c.Position));
            Assert.AreEqual(Biome.Mushroom, world.Biomes.At(a.Position));
            Assert.AreNotEqual(world.AreaOf(a.Position), world.AreaOf(c.Position));
            Assert.AreEqual(-1, world.AreaOf(world.Spawn));

            // Both slots taken: a third gate finds none.
            var third = world.AddGate(280, 20, Biome.Lava);
            var d = At(world, "d", third.Center);
            Assert.IsFalse(world.EnterDungeon(d.Id, third.Index));
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.Dungeon && e.Amount == World.DungeonFull && e.Source == d.Id));

            // One falls: the other is still in, and gets them up where they fell, as anywhere.
            world.Hurt(a, 100000);
            b.Position = a.Position;
            Run(world, World.ReviveSeconds + 0.2f);
            Assert.IsFalse(a.IsDead);
            Assert.AreSame(shared, world.DungeonAt(a.Position));
        }

        [Test]
        public void ADungeonNobodyIsInGoesBackToRockAndTheNextIsAnother()
        {
            var world = Hand(out var gate, out _);
            var player = At(world, "a", gate.Center);
            world.EnterDungeon(player.Id, gate.Index);
            Run(world, World.EnterSeconds + 0.1f);
            var first = world.DungeonOf(gate);
            ulong firstSeed = first.Seed;
            int version = world.DungeonVersion;
            world.LeaveDungeon(player.Id, byMenu: true);
            Run(world, World.MenuLeaveSeconds + 0.1f);
            Run(world, World.EmptySeconds - 1);
            Assert.IsNotNull(world.DungeonOf(gate), "kept a while for one who comes back");
            Run(world, 1.2f);
            Assert.IsNull(world.DungeonOf(gate));
            Assert.Greater(world.DungeonVersion, version);
            Assert.IsTrue(Enumerable.Range(world.Map.Height, DungeonPocket.Rows).All(y => world.Map[first.Layout.Exits[0].X, y] == TileType.Wall), "back to rock");
            world.EnterDungeon(player.Id, gate.Index);
            Run(world, World.EnterSeconds + 0.1f);
            var second = world.DungeonOf(gate);
            Assert.AreEqual(1, second.Serial);
            Assert.AreNotEqual(firstSeed, second.Seed, "every time in is another");
            Assert.AreEqual(2, world.DungeonRecordOf(second.Key).Entered, "entered twice, counted by the dungeon's key (묶음 5b)");
        }

        [Test]
        public void PocketPositionsGoOnTheWireExactlyAndFarOnesWhole()
        {
            var self = new SelfState(0, new Vec2(500.3f, 1100.7f), 10, 10, 5);
            var entities = new List<EntityState>
            {
                new EntityState(1, EntityKind.Monster, 0, new Vec2(512.27f, 1119.03f), new Vec2(1, 0), 5, 5, 0),
                new EntityState(2, EntityKind.Player, 0, new Vec2(3.5f, 2.25f), new Vec2(1, 0), 5, 5, 0),
                new EntityState(3, EntityKind.Drop, 0, new Vec2(488.9f, 1090.1f), Vec2.Zero, 0, 0, 0),
            };
            var projectiles = new List<ProjectileState> { new ProjectileState(4, new Vec2(501.11f, 1101.9f), new Vec2(3, 0), 0.2f) };
            var hazards = new List<HazardState> { new HazardState(5, HazardShape.Circle, new Vec2(495.5f, 1105.25f), new Vec2(0, 1), 2, 0, 10, 20, true) };
            var events = new List<SimEvent> { new SimEvent(SimEventType.Dungeon, 2, amount: World.DungeonEntered, position: new Vec2(499.5f, 1060.5f)) };
            var decoded = SnapshotMessage.Decode(new SnapshotMessage(9, self, entities, projectiles, hazards, events).Encode());
            Assert.AreEqual(self.Position, decoded.Self.Position);
            // To a sixty-fourth of a tile each way, as before.
            void Near(Vec2 sent, Vec2 got, string what)
            {
                Assert.LessOrEqual(MathF.Abs(sent.X - got.X), 1 / 128f + 1e-4f, what);
                Assert.LessOrEqual(MathF.Abs(sent.Y - got.Y), 1 / 128f + 1e-4f, what);
            }
            for (int i = 0; i < entities.Count; i++) Near(entities[i].Position, decoded.Entities[i].Position, $"entity {i}");
            Assert.AreEqual(entities[1].Position, decoded.Entities[1].Position, "the far one goes whole");
            Near(projectiles[0].Position, decoded.Projectiles[0].Position, "projectile");
            Near(hazards[0].Position, decoded.Hazards[0].Position, "hazard");
            Near(events[0].Position, decoded.Events[0].Position, "event");

            // A near one costs four bytes as before; a far one ten.
            int near = new SnapshotMessage(9, self, entities.Take(1).ToList(), new List<ProjectileState>(), new List<HazardState>(), new List<SimEvent>()).Encode().Length;
            int none = new SnapshotMessage(9, self, new List<EntityState>(), new List<ProjectileState>(), new List<HazardState>(), new List<SimEvent>()).Encode().Length;
            int far = new SnapshotMessage(9, self, entities.Skip(1).Take(1).ToList(), new List<ProjectileState>(), new List<HazardState>(), new List<SimEvent>()).Encode().Length;
            Assert.AreEqual(far - none - 6, near - none);

            var sent = new DungeonStatus(2, 7, 3, 0xFEDCBA9876543210UL, DungeonState.Done, 12, 4, DungeonFlags.KeyTaken | DungeonFlags.BossDown, 0b1011, 0b100, 123456789L, 312, 3);
            var got = DungeonsMessage.Decode(new DungeonsMessage(new[] { sent }).Encode()).Dungeons.Single();
            Assert.AreEqual((sent.Slot, sent.Gate, sent.Serial, sent.Seed, sent.State, sent.Level, sent.Definition), (got.Slot, got.Gate, got.Serial, got.Seed, got.State, got.Level, got.Definition));
            Assert.AreEqual((sent.Flags, sent.Cleared, sent.Used, sent.Started, sent.ClearSeconds, sent.Deaths), (got.Flags, got.Cleared, got.Used, got.Started, got.ClearSeconds, got.Deaths));
        }

        static readonly HostConfig RoomConfig = new HostConfig { MapSize = 512, MapVersion = WorldGenerator.RoomVersion };

        static WorldHost Host() => new WorldHost(3, Monsters.Shipped, Gear.Data, RoomConfig, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped,
            Classes.Shipped, Tiles.Shipped, story: Tales.Shipped);

        static WorldClient Client(string name) =>
            new WorldClient("key-" + name, name, Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped) { Story = Tales.Shipped };

        static void Pass(WorldHost host, float seconds, params (ulong Connection, WorldClient Client)[] clients)
        {
            for (int i = World.Ticks(seconds); i >= 0; i--)
            {
                host.Tick();
                foreach (var message in host.TakeOutgoing())
                    foreach (var (connection, client) in clients)
                        if (message.Connection == connection) client.Receive(message.Data);
                foreach (var (_, client) in clients) client.AdvanceRenderClock(World.TickSeconds);
            }
        }

        [Test]
        public void TheHostSendsTheDungeonAndEachClientLaysTheSameAndSeesOnlyItsPart()
        {
            var host = Host();
            Assert.Greater(DungeonPocket.SlotsOf(host.World.Map), 1);
            Assert.IsNotEmpty(host.World.Gates, "a world of rooms of 512 has a gate room");
            var alice = Client("앨리스");
            var bob = Client("밥");
            host.Receive(1, alice.JoinMessage());
            host.Receive(2, bob.JoinMessage());
            Pass(host, 0.2f, (1, alice), (2, bob));
            CollectionAssert.AreEqual(host.World.Gates.Select(g => (g.X, g.Y)), alice.Gates.Select(g => (g.X, g.Y)), "the client finds the same gates");

            var gate = host.World.Gates[0];
            var a = host.World.FindPlayer(alice.PlayerId);
            a.Position = Collision.Resolve(host.World.Map, gate.Center, World.PlayerRadius);
            a.Corrections++;
            Pass(host, 0.2f, (1, alice), (2, bob));
            Assert.AreSame(alice.Gates[0], alice.GateNear());
            host.Receive(1, alice.EnterDungeonCommand(alice.GateNear()));
            Pass(host, 0.2f, (1, alice), (2, bob));
            Assert.IsNotNull(alice.DungeonOf(alice.Gates[0]), "laid on the client while still gathering");
            Assert.Greater(alice.DungeonCharge, 0);
            Pass(host, World.EnterSeconds, (1, alice), (2, bob));
            var dungeon = host.World.DungeonAt(a.Position);
            Assert.IsNotNull(dungeon);
            Assert.AreEqual(PocketHash(host.World.Map), PocketHash(alice.Map), "host and client lay the same pocket");
            Assert.AreEqual(PocketHash(host.World.Map), PocketHash(bob.Map));
            Assert.AreEqual(RoomMapTests.Hash(host.World.Map), RoomMapTests.Hash(alice.Map));
            Assert.Less(Vec2.Distance(alice.Position, a.Position), 0.01f, "the client was put in");
            Assert.AreEqual(dungeon.Slot, alice.DungeonHere?.Slot);
            Assert.AreEqual(gate.Biome, alice.Biomes.At(alice.Position));
            Assert.AreEqual(0, alice.DungeonCharge);

            // A monster of the world just below the slot, nearer than a snapshot's reach: not sent to one in the pocket.
            var map = host.World.Map;
            int x = TileMap.Cell(a.Position.X);
            map[x, map.Height + DungeonPocket.Wall + 1] = TileType.Floor;
            map[x, map.Height - 4] = TileType.Floor;
            a.Position = TileMap.Center(x, map.Height + DungeonPocket.Wall + 1);
            a.Corrections++;
            host.World.MonstersHold = true;
            var below = host.World.SpawnEnemy(Monsters.Shipped.Monsters[0], TileMap.Center(x, map.Height - 4));
            var inside = host.World.SpawnEnemy(Monsters.Shipped.Monsters[0], dungeon.Layout.Entrance);
            Assert.Less(Vec2.Distance(below.Position, a.Position), new HostConfig().ViewRange);
            Pass(host, 0.3f, (1, alice), (2, bob));
            var seen = alice.Others().Select(e => e.Id).ToList();
            CollectionAssert.DoesNotContain(seen, below.Id, "the world's monster is not in the pocket's snapshot");
            CollectionAssert.Contains(seen, inside.Id);
            var her = alice.Others().First(e => e.Id == inside.Id);
            Assert.Less(Vec2.Distance(her.Position, inside.Position), 0.05f);
            // The other player, out in the world far off, comes whole.
            var bobThere = alice.Others().First(e => e.Id == bob.PlayerId);
            Assert.Less(Vec2.Distance(bobThere.Position, host.World.FindPlayer(bob.PlayerId).Position), 0.05f);

            // Out again: back at the gate on the client too.
            a.Position = dungeon.Layout.Entrance;
            a.Corrections++;
            host.Receive(1, alice.LeaveDungeonCommand(byMenu: true));
            Pass(host, World.MenuLeaveSeconds + 0.2f, (1, alice), (2, bob));
            Assert.IsFalse(alice.InPocket);
            Assert.Less(Vec2.Distance(alice.Position, gate.Center), 1f);

            // Empty a minute, it goes back to rock on the clients as well.
            inside.Health = 0;
            Pass(host, World.EmptySeconds + 0.5f, (1, alice), (2, bob));
            Assert.IsEmpty(alice.Dungeons);
            Assert.AreEqual(PocketHash(host.World.Map), PocketHash(alice.Map));
            Assert.AreEqual(PocketHash(WorldGenerator.Generate(3, 512, 512, Tales.Shipped, Bosses.Shipped.Arenas, WorldGenerator.RoomVersion, Tiles.Shipped.Terrain).Map),
                PocketHash(alice.Map), "all rock again");
        }

        [Test]
        public void TheSaveKeepsHowOftenEachGateWasEnteredButNoneOfThePocket()
        {
            var world = Hand(out var gate, out var other);
            var player = At(world, "a", gate.Center);
            world.EnterDungeon(player.Id, gate.Index);
            Run(world, World.EnterSeconds + 0.1f);
            byte[] data = WorldSave.Write(world, 7, 5, WorldGenerator.RoomVersion);
            var save = WorldSave.Read(data, Tiles.Shipped, Gear.Data);
            Assert.AreEqual(world.DungeonOf(gate).Key, save.DungeonRecords.Single().Key);
            Assert.AreEqual(1, save.DungeonRecords.Single().Entered);
            Assert.IsTrue(save.Tiles.All(t => t.Y < world.Map.Height), "no tile of the pocket is saved");

            var again = Hand(out var gateAgain, out _);
            again.RestoreDungeons(save.DungeonRecords);
            var back = At(again, "a", gateAgain.Center);
            again.EnterDungeon(back.Id, gateAgain.Index);
            Assert.AreEqual(1, again.DungeonOf(gateAgain).Serial, "the next one in is the second, not the first again");
            Assert.AreNotEqual(world.DungeonOf(gate).Seed, again.DungeonOf(gateAgain).Seed);

            // A save of format 15 counted by gate: its count goes to the dungeon that gate leads to now.
            var fifteen = Hand(out var gate15, out _);
            fifteen.RestoreDungeons(null, new[] { (gate15.Index, 3) });
            Assert.AreEqual(3, fifteen.DungeonRecordOf(fifteen.DungeonFor(gate15).KeyFor(gate15.Biome)).Entered);

            // A save of format 14 (no counts at its end) reads with none.
            var fresh = Hand(out _, out _);
            var old = WorldSave.Write(fresh, 7, 5, WorldGenerator.RoomVersion).ToList();
            old.RemoveRange(old.Count - 4, 4);
            old[4] = 14;
            Assert.IsEmpty(WorldSave.Read(old.ToArray(), Tiles.Shipped).DungeonEntries);
            Assert.IsEmpty(WorldSave.Read(old.ToArray(), Tiles.Shipped).DungeonRecords);
        }
    }
}
