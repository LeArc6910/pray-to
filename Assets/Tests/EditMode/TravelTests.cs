using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // Getting about a world of rooms (2026-11-22, Docs/WorldRework.md 광차·길잡이 돌·떠돌이 상인, 묶음 3c): the minecart
    // lines and their stations, riding and mending them, the waystones, the wandering merchant and the map scrap, the
    // save and the wire; and that a world of caves (version 1) is as it was.
    public class TravelTests
    {
        static readonly Dictionary<ulong, GeneratedWorld> Made = new Dictionary<ulong, GeneratedWorld>();

        static GeneratedWorld Big(ulong seed)
        {
            if (!Made.TryGetValue(seed, out var world))
                Made[seed] = world = WorldGenerator.Generate(seed, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped, Bosses.Staged.Arenas, WorldGenerator.RoomVersion,
                    Tiles.Shipped.Terrain);
            return world;
        }

        static HostConfig BigRooms => new HostConfig { MapSize = WorldGenerator.GameSize, MapVersion = WorldGenerator.RoomVersion };

        static WorldHost Host(ulong seed = 1) => new WorldHost(seed, Monsters.Shipped, Gear.Data, BigRooms, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped,
            Classes.Shipped, Tiles.Shipped, npcs: People.Shipped, story: Tales.Shipped);

        static readonly MaterialDefinition IronBar = Gear.Data.FindMaterial("iron_bar");
        static readonly MaterialDefinition Coin = Gear.Data.FindMaterial(World.Coin);

        static Vec2 Beside(World world, Poi poi)
        {
            for (float reach = 0.9f; reach <= World.PoiReach - 0.2f; reach += 0.2f)
                for (int k = 0; k < 32; k++)
                {
                    var at = poi.Center + Vec2.FromAngle(2 * MathF.PI * k / 32) * reach;
                    if (!Collision.Overlaps(world.Map, at, World.PlayerRadius)) return at;
                }
            return poi.Center;
        }

        [Test]
        public void TheSheetHasAStationAndAWaystoneAndSaysWhatIsWrong()
        {
            var pois = Tiles.Shipped.Terrain.Pois;
            Assert.IsTrue(pois.Success, string.Join("\n", pois.Errors));
            var station = pois.Find("station");
            Assert.AreEqual(PoiKind.Station, station.Kind);
            Assert.AreEqual(16, station.Reward(PoiRewardKind.Ride).Min);
            Assert.AreEqual("iron_bar", station.Reward(PoiRewardKind.Repair).Material);
            Assert.AreEqual(5, station.Reward(PoiRewardKind.Repair).Min);
            Assert.IsFalse(station.OnMark, "laid along the rails, not on a cart room's mark");
            var stone = pois.Find("waystone");
            Assert.AreEqual(PoiKind.Waystone, stone.Kind);
            Assert.AreEqual(TileType.Waystone, stone.Tile);
            Assert.IsFalse(pois.For(RoomRole.Cart, Biome.Cave).Contains(station), "a cart room's mark is never a station");
            Assert.IsFalse(pois.For(RoomRole.Rest, Biome.Cave).Contains(stone), "nor a camp's fire a waystone");

            const string head = "id,name,role,biomes,weight,reward,reward_level,per_player,again,signal,signal_range,buff,tile\n";
            var camp = "camp,야영지,rest,,10,rest:10,,no,always,smoke,20,,\n";
            void Wrong(string row, string why)
            {
                var data = PoiSheet.Parse(head + camp + row, Gear.Data);
                Assert.IsTrue(data.Errors.Any(e => e.Contains(why)), $"{row.Trim()}: {string.Join(" / ", data.Errors)}");
            }
            Wrong("s,역,shrine,,10,ride:16,,no,always,lamps,16,,\n", "role이 cart");
            Wrong("s,역,cart,,10,ride:16,,no,once,lamps,16,,\n", "always");
            Wrong("s,역,cart,,10,ride:99,,no,always,lamps,16,,\n", "초당");
            Wrong("s,역,cart,,10,ride:16|repair:nothing:5,,no,always,lamps,16,,\n", "없는 재료");
            Wrong("w,돌,rest,,10,waypoint:1,,yes,always,flame,16,,\n", "Waystone");
            Wrong("w,돌,cart,,10,waypoint:1,,yes,always,flame,16,,Waystone\n", "role이 rest");
            Wrong("x,수레,cart,,10,repair:iron_bar:5,,no,once,,,,\n", "ride와 함께");
        }

        [Test]
        public void LinesRunBetweenCartRoomsOfTwoBiomesAlongTheWays()
        {
            for (ulong seed = 1; seed <= 3; seed++)
            {
                var world = Big(seed);
                var rooms = world.Rooms;
                var lines = rooms.Lines;
                Assert.That(lines.Count, Is.InRange(4, WorldGenerator.MaxLines), $"seed {seed}: lines");
                var stations = new HashSet<int>();
                foreach (var line in lines)
                {
                    string where = $"seed {seed} line {line.Index}";
                    Assert.That(line.Route.Count - 1, Is.InRange(WorldGenerator.MinRail, WorldGenerator.MaxRail), where);
                    for (int i = 1; i < line.Route.Count; i++)
                    {
                        var (ax, ay) = line.Route[i - 1];
                        var (bx, by) = line.Route[i];
                        Assert.AreEqual(1, Math.Abs(ax - bx) + Math.Abs(ay - by), $"{where}: the rails go tile by tile");
                    }
                    Assert.IsTrue(line.Route.All(t => !world.Map.IsSolid(t.X, t.Y) && world.Map[t.X, t.Y] != TileType.Lava && world.Map[t.X, t.Y] != TileType.Water),
                        $"{where}: on open floor");
                    Assert.Greater(line.Route.Count(t => world.Map[t.X, t.Y] == TileType.Rail), line.Route.Count * 3 / 5, $"{where}: rails laid");
                    var a = rooms.Pois[line.StationA];
                    var b = rooms.Pois[line.StationB];
                    Assert.AreEqual(PoiKind.Station, a.Kind, where);
                    Assert.AreEqual(PoiKind.Station, b.Kind, where);
                    Assert.AreEqual((a.X, a.Y), line.Route[0], where);
                    Assert.AreEqual((b.X, b.Y), line.Route[line.Route.Count - 1], where);
                    Assert.AreEqual(RoomRole.Cart, rooms.Nodes[a.Room].Role, where);
                    Assert.AreEqual(RoomRole.Cart, rooms.Nodes[b.Room].Role, where);
                    Assert.AreNotEqual(a.Biome, b.Biome, $"{where}: it crosses from one biome to another");
                    Assert.AreEqual(line.StationB, line.Broken, $"{where}: the far end is the broken one");
                    Assert.LessOrEqual(rooms.Nodes[a.Room].Depth, rooms.Nodes[b.Room].Depth, $"{where}: A is nearer the spawn");
                    Assert.IsTrue(stations.Add(line.StationA) && stations.Add(line.StationB), $"{where}: a station to one line");
                    // The cart takes 10 to 19 seconds (Docs/WorldRework.md 광차).
                    Assert.That(line.Length / a.Definition.Reward(PoiRewardKind.Ride).Min, Is.InRange(8f, 19.5f), where);
                    Assert.AreEqual(TileMap.Center(a.X, a.Y), line.PointAt(0));
                    Assert.AreEqual(TileMap.Center(b.X, b.Y), line.PointAt(line.Length));
                }
            }
        }

        [Test]
        public void TheSameSeedLaysTheSameLinesAndStonesAndCavesAreAsTheyWere()
        {
            GeneratedWorld Make() => WorldGenerator.Generate(2, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped, Bosses.Staged.Arenas, WorldGenerator.RoomVersion,
                Tiles.Shipped.Terrain);
            var a = Make();
            var b = Make();
            CollectionAssert.AreEqual(a.Rooms.Lines.SelectMany(l => l.Route), b.Rooms.Lines.SelectMany(l => l.Route));
            CollectionAssert.AreEqual(a.Rooms.Pois.Select(p => (p.Definition.Id, p.X, p.Y)), b.Rooms.Pois.Select(p => (p.Definition.Id, p.X, p.Y)));
            // The stations and stones come after the things of the chunks' marks, so those keep their numbers.
            int first = a.Rooms.Pois.First(p => !p.Definition.OnMark).Index;
            Assert.IsTrue(a.Rooms.Pois.Skip(first).All(p => !p.Definition.OnMark));
            // Without the sheet, no rails are laid and no stones stand.
            var bare = WorldGenerator.Generate(2, 512, 512, Tales.Shipped, Bosses.Staged.Arenas, WorldGenerator.RoomVersion, Tiles.Shipped.Terrain.WithoutPois());
            Assert.IsEmpty(bare.Rooms.Lines);
            // A world of caves (version 1), given the terrain sheet or not, is the same tile for tile (RoomMapTests).
            var caves = WorldGenerator.Generate(1, 256, 256, Tales.Shipped, Bosses.Staged.Arenas, WorldGenerator.CaveVersion, Tiles.Shipped.Terrain);
            Assert.IsNull(caves.Rooms);
            Assert.AreEqual(17259121183764716915UL, RoomMapTests.Hash(caves.Map));
        }

        [Test]
        public void AWaystoneStandsByACampOfEachBiomeAndShutsNothing()
        {
            for (ulong seed = 1; seed <= 3; seed++)
            {
                var world = Big(seed);
                var stones = world.Rooms.Pois.Where(p => p.Kind == PoiKind.Waystone).ToList();
                Assert.GreaterOrEqual(stones.Count, 6, $"seed {seed}");
                Assert.AreEqual(stones.Count, stones.Select(s => s.Biome).Distinct().Count(), $"seed {seed}: one to a biome");
                foreach (var stone in stones)
                {
                    Assert.AreEqual(TileType.Waystone, world.Map[stone.X, stone.Y]);
                    Assert.AreEqual(RoomRole.Rest, world.Rooms.Nodes[stone.Room].Role);
                    var fire = world.Rooms.Pois.First(p => p.Kind == PoiKind.Camp && p.Room == stone.Room);
                    Assert.LessOrEqual(Math.Max(Math.Abs(fire.X - stone.X), Math.Abs(fire.Y - stone.Y)), 4, "by the fire");
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            if (dx != 0 || dy != 0) Assert.IsFalse(world.Map.IsSolid(stone.X + dx, stone.Y + dy), "open all round");
                }
            }
        }

        [Test]
        public void ALineIsMendedAtItsBrokenEndThenRiddenUntouchedToTheOtherEnd()
        {
            var host = Host(1);
            var world = host.World;
            var line = world.Lines[0];
            var near = world.Pois[line.StationA];
            var far = world.Pois[line.StationB];
            var player = world.AddPlayer("a", Gear.Wearing("iron_sword"));

            // Broken: the near end will not go, nor the far end without the iron.
            player.Position = Beside(world, near);
            Assert.AreSame(near, world.PoiNear(player.Position, player));
            Assert.IsFalse(world.UsePoi(player.Id, near.Index), "the line is broken at its far end");
            player.Position = Beside(world, far);
            Assert.IsFalse(world.UsePoi(player.Id, far.Index), "no iron bars");
            player.Inventory.AddMaterial(IronBar, 7);
            Assert.IsTrue(world.UsePoi(player.Id, far.Index));
            Assert.IsTrue(world.RailFixed(line.Index));
            Assert.AreEqual(2, player.Inventory.CountOf(IronBar), "five bars mend it");
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.Rail && e.Target == World.RailMended && e.Amount == far.Index));

            // Ridden from the far end: carried along the rails, no blow lands, what the client says is let pass.
            int corrections = player.Corrections;
            Assert.IsTrue(world.UsePoi(player.Id, far.Index));
            Assert.IsTrue(player.Riding);
            Assert.IsFalse(player.RideForward);
            var start = player.Position;
            for (int i = 0; i < World.TicksPerSecond; i++) world.Step();
            float speed = far.Definition.Reward(PoiRewardKind.Ride).Min;
            Assert.That(Vec2.Distance(player.Position, line.PointAt(line.Length - speed)), Is.LessThan(0.6f), "a second along the line");
            Assert.Greater(Vec2.Distance(player.Position, start), speed * 0.5f);
            Assert.AreEqual(0, world.Hurt(player, 50), "untouched while riding");
            Assert.IsTrue(world.MovePlayer(player.Id, start, new Vec2(1, 0), 1));
            Assert.AreNotEqual(start, player.Position, "the client's report does not hold the cart");
            Assert.IsFalse(world.UsePoi(player.Id, near.Index));
            int ticks = (int)Math.Ceiling(line.Length / speed * World.TicksPerSecond) + 2;
            for (int i = 0; i < ticks; i++) world.Step();
            Assert.IsFalse(player.Riding);
            Assert.Less(Vec2.Distance(player.Position, near.Center), 1.2f, "got off at the near station");
            Assert.Greater(player.Corrections, corrections, "the client is put there");
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.Rail && e.Target == World.RailArrived && e.Amount == near.Index));
            // And now from the near end too.
            Assert.IsTrue(world.UsePoi(player.Id, near.Index));
            Assert.IsTrue(player.RideForward);
            Assert.AreEqual(1, player.Rides - 1);
        }

        [Test]
        public void AWaystoneOpensWhenTouchedAndIsGoneToFromAnother()
        {
            var host = Host(2);
            var world = host.World;
            var stones = world.Stones;
            Assert.GreaterOrEqual(stones.Count, 2);
            var player = world.AddPlayer("a", Gear.Wearing("iron_sword"));
            var here = stones[0];
            var there = stones[1];
            player.Position = Beside(world, here);
            Assert.IsNull(world.PoiNear(player.Position, player), "a stone is not used by hand");
            for (int i = 0; i < World.TicksPerSecond; i++) world.Step();
            CollectionAssert.Contains(world.OpenStones(player).ToList(), here.Index);
            CollectionAssert.Contains(player.Waypoints, World.StoneId(here.Biome));
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.Waystone && e.Target == World.StoneOpened && e.Amount == here.Index));
            // Not to one never touched; then, once it is, there and back.
            Assert.IsFalse(world.Teleport(player.Id, World.StoneTarget + there.Index));
            world.OpenStone(player, there);
            Assert.IsTrue(world.Teleport(player.Id, World.StoneTarget + there.Index));
            Assert.Less(Vec2.Distance(player.Position, there.Center), 2.2f);
            Assert.IsFalse(world.Teleport(player.Id, World.StoneTarget + there.Index), "already there");
            // From a stone to a place's waypoint open to the player, and back.
            var site = world.Places.First(p => p.Definition.Waypoint);
            player.Waypoints.Add(site.Definition.Id);
            Assert.IsTrue(world.Teleport(player.Id, site.Definition.Index));
            Assert.IsTrue(site.Contains(player.Position));
            Assert.IsTrue(world.Teleport(player.Id, World.StoneTarget + here.Index));
            Assert.Less(Vec2.Distance(player.Position, here.Center), 2.2f);
            // Away from any, nothing.
            player.Position = world.Spawn + new Vec2(30, 30);
            Assert.IsFalse(world.Teleport(player.Id, World.StoneTarget + there.Index));
        }

        // Through the wire, as the waypoint list's row sends it: a waystone's target (StoneTarget + its index, past 255)
        // reaches the host whole (2026-11-22: the command's index went as one byte, so a press on a row sent the player
        // to a place, or nowhere).
        [Test]
        public void AWaystoneIsGoneToByTheClientsCommand()
        {
            var host = Host(2);
            var world = host.World;
            var client = new WorldClient("key", "a", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped)
                { Story = Tales.Shipped, Npcs = People.Shipped };
            host.Receive(1, client.JoinMessage());
            host.Tick();
            foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            var player = world.FindPlayer(client.PlayerId);
            var here = world.Stones[0];
            var there = world.Stones[1];
            world.OpenStone(player, here);
            world.OpenStone(player, there);
            player.Position = Beside(world, here);

            var sent = InventoryCommandMessage.Decode(client.TravelToStoneCommand(there));
            Assert.AreEqual(World.StoneTarget + there.Index, sent.Index);
            host.Receive(1, client.TravelToStoneCommand(there));
            host.Tick();
            Assert.Less(Vec2.Distance(player.Position, there.Center), 2.2f);
        }

        [Test]
        public void MendedLinesAreSavedAndWhatTheClientIsTold()
        {
            var host = Host(3);
            var world = host.World;
            var client = new WorldClient("key", "a", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped)
                { Story = Tales.Shipped, Npcs = People.Shipped };
            host.Receive(1, client.JoinMessage());
            void Deliver()
            {
                foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            }
            host.Tick();
            Deliver();
            var player = world.FindPlayer(client.PlayerId);
            Assert.AreEqual(world.Lines.Count, client.Lines.Count, "the client lays the same lines from the seed");
            var line = world.Lines[1];
            world.MendRail(line.Index);
            var stone = world.Stones[0];
            world.OpenStone(player, stone);
            var station = world.Pois[line.StationA];
            player.Position = Beside(world, station);
            Assert.IsTrue(world.UsePoi(player.Id, station.Index));
            host.Tick();
            Deliver();
            Assert.IsTrue(client.RailFixed(line.Index));
            Assert.IsFalse(client.RailFixed(world.Lines[0].Index));
            Assert.IsTrue(client.StoneOpen(stone.Index));
            Assert.IsTrue(client.Riding);
            Assert.That(Vec2.Distance(client.RideOf(client.PlayerId).Value.At, player.Position), Is.LessThan(3f), "the screen rides where the host does");
            // The character keeps the stone among its waypoints.
            Assert.IsTrue(client.SaveCharacter() == null || CharacterSave.Read(client.SaveCharacter(), Gear.Data).Waypoints.Contains(World.StoneId(stone.Biome)));

            // The world's save keeps the mended lines (format 14); a fresh host reads them back.
            var save = WorldSave.Read(host.SaveWorld(now: 0), Tiles.Shipped, Gear.Data);
            CollectionAssert.AreEqual(new[] { line.Index }, save.FixedRails);
            var again = Host(3);
            again.Restore(save);
            Assert.IsTrue(again.World.RailFixed(line.Index));
            Assert.IsFalse(again.World.RailFixed(world.Lines[0].Index));
            // A character's waystone comes back with it, whatever world it goes to.
            var back = again.World.AddPlayer("a", Gear.Wearing("iron_sword"));
            again.World.RestoreJourney(back, 0, false, new[] { World.StoneId(stone.Biome), "no_such_place" });
            CollectionAssert.Contains(again.World.OpenStones(back).ToList(), stone.Index);
            Assert.IsFalse(back.Waypoints.Contains("no_such_place"));
        }

        [Test]
        public void TheWanderingMerchantStandsByAnotherCampEachDayAndSellsItsBiomesSeeds()
        {
            var host = Host(1);
            var world = host.World;
            var npc = People.Shipped.Npcs.Single(n => n.Wanders);
            Assert.IsNull(npc.Place);
            var at = world.WandererAt ?? throw new AssertionException("no stall");
            Assert.AreEqual(npc.Tile, world.Map[at.X, at.Y]);
            var camp = world.Pois.Where(p => p.Kind == PoiKind.Camp).OrderBy(p => Math.Abs(p.X - at.X) + Math.Abs(p.Y - at.Y)).First();
            Assert.LessOrEqual(Math.Max(Math.Abs(camp.X - at.X), Math.Abs(camp.Y - at.Y)), 4, "by a camp's fire");
            int stalls = 0;
            for (int y = 0; y < world.Map.Height; y++)
                for (int x = 0; x < world.Map.Width; x++)
                    if (world.Map[x, y] == npc.Tile) stalls++;
            Assert.AreEqual(1, stalls);

            // The shop: potions and the map scrap every day, and of the daily rows only the camp's biome's seeds.
            var biome = world.Biomes[at.X, at.Y];
            var today = World.ShopFor(People.Shipped, world.Today, npc, biome);
            Assert.IsTrue(today.Any(e => e.Consumable?.Id == "map_scrap"));
            Assert.IsTrue(today.Any(e => e.Consumable?.Id == "healing_potion"));
            Assert.IsTrue(today.All(e => e.SoldIn(biome)));
            Assert.LessOrEqual(today.Count(e => e.Daily), World.WandererDailyItems);
            Assert.IsTrue(People.Shipped.Shop.Where(e => e.Npc == npc && e.Biomes.Count > 0).All(e => !World.ShopFor(People.Shipped, 3, npc).Contains(e)),
                "a biome's seeds are not sold where the merchant does not stand");

            // Bought from beside the stall, and read: the map scrap tells the reader's screen.
            var player = world.AddPlayer("a", Gear.Wearing("iron_sword"));
            player.Position = Collision.Resolve(world.Map, TileMap.Center(at.X, at.Y - 1), World.PlayerRadius);
            player.Inventory.AddMaterial(Coin, 100);
            var scrap = today.First(e => e.Consumable?.Id == "map_scrap");
            Assert.IsTrue(world.Buy(player.Id, scrap, 1));
            Assert.AreEqual(100 - World.BuyPrice(scrap, 0, world.Today), player.Inventory.CountOf(Coin));
            Assert.IsTrue(world.UseConsumable(player.Id, scrap.Consumable));
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.MapRead && e.Source == player.Id));
            Assert.IsNotNull(world.Invite(player.Id), "a wanderer will not move into a house");

            // The next day: another camp, and still one stall.
            int day = world.Today;
            world.RestoreClock(day * Calendar.TicksPerDay + 10);
            for (int i = 0; i < World.TicksPerSecond + 1; i++) world.Step();
            var moved = world.WandererAt ?? throw new AssertionException("no stall the next day");
            Assert.AreNotEqual(at, moved);
            Assert.AreEqual(TileType.Floor, world.Map[at.X, at.Y], "the old stall is gone");
            int camps = world.Pois.Count(p => p.Kind == PoiKind.Camp);
            for (int d = 2; d < 40; d++) Assert.AreNotEqual(World.WandererCamp(9, d, camps), World.WandererCamp(9, d - 1, camps));
        }

        [Test]
        public void EveryWorldOfRoomsHasALockToStandBefore()
        {
            for (ulong seed = 1; seed <= 6; seed++)
            {
                var world = seed <= 3 ? Big(seed) : WorldGenerator.Generate(seed, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped, Bosses.Staged.Arenas,
                    WorldGenerator.RoomVersion, Tiles.Shipped.Terrain);
                var locked = world.Rooms.Nodes.Where(n => n.Role == RoomRole.Locked && n.Box != null).ToList();
                var found = locked.Select(n => RoomGraph.LockOf(world.Map, n)).Where(l => l != null).Select(l => l.Value).ToList();
                Assert.Greater(found.Count, locked.Count / 2, $"seed {seed}: {found.Count} of {locked.Count} locks found");
                foreach (var (wall, stand) in found)
                {
                    Assert.IsTrue(world.Map[wall.X, wall.Y] == TileType.ObsidianWall || world.Map[wall.X, wall.Y] == TileType.VoidWall);
                    Assert.IsFalse(world.Map.IsSolid(stand.X, stand.Y));
                    Assert.AreEqual(1, Math.Abs(wall.X - stand.X) + Math.Abs(wall.Y - stand.Y));
                }
            }
        }
    }
}
