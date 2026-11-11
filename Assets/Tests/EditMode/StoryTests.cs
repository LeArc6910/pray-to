using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The story and its places (Docs/Story.md, 2026-11-10): the sheets, the places and roads carved into the caves,
    // taking and finishing steps, waypoints, and keeping it all in the save and over the network.
    public class StoryTests
    {
        static WorldHost Host(ulong seed = 7) =>
            new WorldHost(seed, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped,
                story: Tales.Shipped);

        static void Run(World world, int ticks)
        {
            for (int i = 0; i < ticks; i++) world.Step();
        }

        static Vec2 Beside(Vec2 at) => at + new Vec2(0, -0.9f);

        [Test]
        public void TheShippedStoryReads()
        {
            Assert.IsEmpty(Tales.Shipped.Errors, string.Join("\n", Tales.Shipped.Errors));
            Assert.AreEqual(7, Tales.Shipped.Places.Count);
            Assert.GreaterOrEqual(Tales.Shipped.Steps.Count, 9);
            var first = Tales.Shipped.Steps[0];
            Assert.AreEqual("chief", first.Who.Id);
            Assert.AreEqual(StoryGoal.Reach, first.Goal);
            Assert.AreEqual("royal_city", first.Place.Id);
            Assert.IsTrue(first.Lines.Any(l => l.Speaker.Id == "mina"), "a line by @mina");
        }

        [Test]
        public void SheetErrorsSayWhatIsWrong()
        {
            var data = StorySheet.Parse(
                "id,name,distance,radius,road\na,가,0,7,\nb,나,30,99,a\nc,다,30,5,zz\n",
                "id,name,place\np,사람,a\nq,사람,nowhere\n",
                "step,title,who,lines,goal\n1,하나,p,안녕,reach:nowhere\n3,셋,p,안녕,\n2,둘,x,안녕,\n",
                Gear.Data, Bosses.Shipped);
            string all = string.Join("\n", data.Errors);
            StringAssert.Contains("radius", all);
            StringAssert.Contains("road 'zz'", all);
            StringAssert.Contains("place 'nowhere'", all);
            StringAssert.Contains("goal의 장소 'nowhere'", all);
            StringAssert.Contains("step은 1부터 차례로", all);
            Assert.AreEqual(1, data.Places.Count);
        }

        [Test]
        public void PlacesAreCarvedJoinedByRoadsAndTheSameEveryTime()
        {
            var world = WorldGenerator.Generate(11, story: Tales.Shipped);
            var again = WorldGenerator.Generate(11, story: Tales.Shipped);
            Assert.AreEqual(Tales.Shipped.Places.Count, world.Places.Count);
            for (int y = 0; y < world.Map.Height; y++)
                for (int x = 0; x < world.Map.Width; x++)
                    Assert.AreEqual(world.Map[x, y], again.Map[x, y], $"({x}, {y})");

            var village = world.Places[0];
            Assert.AreEqual((world.SpawnX, world.SpawnY), (village.X, village.Y), "the village is round the spawn");
            foreach (var site in world.Places)
            {
                Assert.AreEqual(TileType.Waypoint, world.Map[site.X, site.Y], site.Definition.Id);
                Assert.IsFalse(world.Map.IsSolid(site.X, site.Y), "a waypoint is stood on");
                foreach (var arena in world.Arenas)
                    Assert.Greater(Vec2.Distance(site.Center, arena), WorldGenerator.ArenaRadius + site.Radius, $"{site.Definition.Id} keeps clear of the arenas");
                foreach (var (character, at) in site.People)
                    Assert.IsFalse(world.Map.IsSolid(TileMap.Cell(at.X), TileMap.Cell(at.Y)), $"{character.Id} stands on open floor");
            }
            Assert.Greater(Vec2.Distance(world.Places[1].Center, world.Spawn), 25f);

            // Every place can be walked to from the spawn, and a road runs between each and the one before.
            var field = new FlowField(world.Map);
            field.Build(new[] { world.Spawn }, int.MaxValue - 1);
            foreach (var site in world.Places) Assert.AreNotEqual(FlowField.Unreached, field.DistanceAt(site.X, site.Y), site.Definition.Id);
            int road = 0;
            for (int y = 0; y < world.Map.Height; y++)
                for (int x = 0; x < world.Map.Width; x++)
                    if (world.Map[x, y] == TileType.Road) road++;
            Assert.Greater(road, 60, "roads are laid");

            // Without the sheets the caves are as they always were.
            var plain = WorldGenerator.Generate(11);
            Assert.IsEmpty(plain.Places);
            Assert.AreEqual(0, Enumerable.Range(0, plain.Map.Width * plain.Map.Height).Count(i => plain.Map[i % plain.Map.Width, i / plain.Map.Width] == TileType.Road));
        }

        // The game's map (2026-11-11, the owner: "the waypoints at least 200 tiles apart"): every waypoint 200 tiles
        // or more from every other, and all walked to from the spawn.
        [Test]
        public void OnTheGameMapTheWaypointsStandTwoHundredTilesApart([Values(1, 2, 3, 4)] int seed)
        {
            var world = WorldGenerator.Generate((ulong)seed, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped);
            var ways = world.Places.Where(p => p.Definition.Waypoint).ToList();
            Assert.AreEqual(Tales.Shipped.Places.Count(p => p.Waypoint), ways.Count);
            foreach (var a in ways)
                foreach (var b in ways.Where(b => b != a))
                    Assert.GreaterOrEqual(Vec2.Distance(a.Center, b.Center), 200f, $"seed {seed}: {a.Definition.Id} to {b.Definition.Id}");
            var field = new FlowField(world.Map);
            field.Build(new[] { world.Spawn }, int.MaxValue - 1);
            foreach (var site in world.Places) Assert.AreNotEqual(FlowField.Unreached, field.DistanceAt(site.X, site.Y), $"seed {seed}: {site.Definition.Id}");
            // Small places along the roads between them (2026-11-12, CarveLandmarks), each walked into from the spawn.
            Assert.GreaterOrEqual(world.Landmarks.Count, 8, $"seed {seed}: landmarks");
            int r = WorldGenerator.LandmarkRadius;
            foreach (var landmark in world.Landmarks)
            {
                bool reached = false;
                for (int y = landmark.Y - r; y <= landmark.Y + r && !reached; y++)
                    for (int x = landmark.X - r; x <= landmark.X + r && !reached; x++)
                        reached = !world.Map.IsSolid(x, y) && field.DistanceAt(x, y) != FlowField.Unreached;
                Assert.IsTrue(reached, $"seed {seed}: the {landmark.Style} at {landmark.X},{landmark.Y} cannot be walked to");
                Assert.IsTrue(world.Places.All(p => Vec2.Distance(p.Center, landmark.Center) > p.Radius + r), $"seed {seed}: the {landmark.Style} is in a place");
            }
        }

        // Shops (2026-11-11, the owner: the merchants each in a shop of their own, not in the street): on the game's
        // map the people who keep shop stand behind the counter of a timber room with a door, walked into from the
        // spawn, the village's four round it and the story's traders round their place.
        [Test]
        public void OnTheGameMapTheMerchantsStandInShopsOfTheirOwn([Values(1, 2)] int seed)
        {
            var host = new WorldHost((ulong)seed, Monsters.Shipped, Gear.Data, config: new HostConfig { MapSize = WorldGenerator.GameSize },
                bosses: Bosses.Shipped, classes: Classes.Shipped, tiles: Tiles.Shipped, npcs: People.Shipped, story: Tales.Shipped);
            var world = host.World;
            var map = world.Map;
            var reached = new HashSet<(int, int)>();
            var open = new Queue<(int X, int Y)>();
            open.Enqueue((TileMap.Cell(world.Spawn.X), TileMap.Cell(world.Spawn.Y)));
            reached.Add(open.Peek());
            while (open.Count > 0)
            {
                var (x, y) = open.Dequeue();
                foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                    if (map.InBounds(nx, ny) && !map.IsSolid(nx, ny) && reached.Add((nx, ny))) open.Enqueue((nx, ny));
            }
            int inShops = 0;
            foreach (var npc in People.Shipped.Npcs)
            {
                var site = npc.Place != null ? world.Places.Single(p => p.Definition.Id == npc.Place) : world.Places.Single(p => p.Definition.Distance == 0);
                var counter = site.Shops.FirstOrDefault(c => map[c.X, c.Y] == npc.Tile);
                if (site.Shops.Count == 0 || counter == default) continue;
                inShops++;
                // Behind the counter the back wall, timber both sides of the room, and a door in its front wall.
                Assert.AreEqual(TileType.TimberWall, map[counter.X, counter.Y + 1], $"seed {seed}: {npc.Id} back wall");
                Assert.AreEqual(TileType.TimberWall, map[counter.X - 3, counter.Y], $"seed {seed}: {npc.Id} side wall");
                Assert.AreEqual(TileType.TimberWall, map[counter.X + 3, counter.Y], $"seed {seed}: {npc.Id} side wall");
                Assert.AreEqual(TileType.Door, map[counter.X, counter.Y - WorldGenerator.ShopHeight + 2], $"seed {seed}: {npc.Id} door");
                Assert.IsTrue(reached.Contains((counter.X, counter.Y - 1)), $"seed {seed}: {npc.Id}'s counter is walked up to from the spawn");
            }
            Assert.GreaterOrEqual(inShops, People.Shipped.Npcs.Count - 1, $"seed {seed}: the merchants are in shops");
        }

        [Test]
        public void EverySeedGivesPlacesClearOfTheArenasAndWithinReach([Range(1, 24)] int seed)
        {
            var world = WorldGenerator.Generate((ulong)seed, story: Tales.Shipped);
            var field = new FlowField(world.Map);
            field.Build(new[] { world.Spawn }, int.MaxValue - 1);
            foreach (var site in world.Places)
            {
                Assert.AreNotEqual(FlowField.Unreached, field.DistanceAt(site.X, site.Y), $"seed {seed}: {site.Definition.Id} cannot be walked to");
                foreach (var arena in world.Arenas)
                    Assert.Greater(Vec2.Distance(site.Center, arena), WorldGenerator.ArenaRadius + site.Radius, $"seed {seed}: {site.Definition.Id} on an arena");
                foreach (var other in world.Places.Where(o => o != site))
                    Assert.Greater(Vec2.Distance(site.Center, other.Center), site.Radius + other.Radius, $"seed {seed}: {site.Definition.Id} on {other.Definition.Id}");
                // Dressed (Docs/Story.md, 장소 꾸밈), and every person still walked to from the waypoint, with room
                // below them to talk from.
                int decor = 0;
                for (int y = site.Y - site.Radius; y <= site.Y + site.Radius; y++)
                    for (int x = site.X - site.Radius; x <= site.X + site.Radius; x++)
                        if (Structures.IsDecor(world.Map[x, y])) decor++;
                Assert.GreaterOrEqual(decor, 6, $"seed {seed}: {site.Definition.Id} is bare");
                // No structure is left with a tile missing, nor a tile of one standing on its own.
                for (int y = site.Y - site.Radius; y <= site.Y + site.Radius; y++)
                    for (int x = site.X - site.Radius; x <= site.X + site.Radius; x++)
                    {
                        var (w, h) = Structures.Footprint(world.Map[x, y]);
                        for (int j = 0; j < h; j++)
                            for (int i = 0; i < w; i++)
                                if (i > 0 || j > 0) Assert.AreEqual(TileType.StructurePart, world.Map[x + i, y + j], $"seed {seed}: {world.Map[x, y]} at {x},{y} is missing a part");
                        if (world.Map[x, y] == TileType.StructurePart)
                        {
                            bool owned = false;
                            for (int j = 0; j < 3 && !owned; j++)
                                for (int i = 0; i < 4 && !owned; i++)
                                {
                                    var (ow, oh) = Structures.Footprint(world.Map[x - i, y - j]);
                                    owned = world.Map[x - i, y - j] != TileType.StructurePart && i < ow && j < oh && (ow > 1 || oh > 1);
                                }
                            Assert.IsTrue(owned, $"seed {seed}: a structure part at {x},{y} belongs to nothing");
                        }
                    }
                var walked = Walked(world.Map, site.X, site.Y, site.Radius + 2);
                foreach (var (character, at) in site.People)
                {
                    var tile = (TileMap.Cell(at.X), TileMap.Cell(at.Y));
                    Assert.IsTrue(walked.Contains(tile), $"seed {seed}: {character.Id} shut in at {site.Definition.Id}");
                    Assert.IsTrue(walked.Contains((tile.Item1, tile.Item2 - 1)), $"seed {seed}: nowhere to talk to {character.Id} from");
                }
            }
        }

        // Along the roads (2026-11-11): lamp posts set into the rock at the edge, a signpost where each road leaves a
        // place, and a rest spot with a campfire and a bench; none of it on the road, and none of it shutting off the
        // floor beside it.
        [Test]
        public void RoadsHaveLampsSignpostsAndARestSpot([Range(1, 12)] int seed)
        {
            var world = WorldGenerator.Generate((ulong)seed, story: Tales.Shipped);
            var map = world.Map;
            // The small places by the roads (2026-11-12, CarveLandmarks) have their own campfires and are not road dressing.
            bool InPlace(int x, int y) => world.Places.Any(p => (x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y) <= p.Radius * p.Radius)
                || world.Landmarks.Any(l => (x - l.X) * (x - l.X) + (y - l.Y) * (y - l.Y) <= (WorldGenerator.LandmarkRadius + 1) * (WorldGenerator.LandmarkRadius + 1));
            int lamps = 0, signs = 0, rests = 0;
            var dressed = new List<(int X, int Y)>();
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    if (InPlace(x, y)) continue;
                    var here = map[x, y];
                    if (here != TileType.LampPost && here != TileType.Signpost && here != TileType.Campfire) continue;
                    bool byRoad = new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) }.Any(d => map[x + d.Item1, y + d.Item2] == TileType.Road);
                    Assert.IsTrue(byRoad, $"seed {seed}: {here} at {x},{y} is not beside a road");
                    dressed.Add((x, y));
                    if (here == TileType.LampPost) lamps++;
                    if (here == TileType.Signpost) signs++;
                    bool bench = false;
                    for (int dy = -3; dy <= 3; dy++)
                        for (int dx = -3; dx <= 3; dx++) bench |= map.InBounds(x + dx, y + dy) && map[x + dx, y + dy] == TileType.Bench;
                    if (here == TileType.Campfire && bench) rests++;
                }
            int roads = Tales.Shipped.Places.Count(p => p.Road.Length > 0);
            Assert.GreaterOrEqual(lamps, roads, $"seed {seed}: lamps along the roads");
            Assert.GreaterOrEqual(signs, roads, $"seed {seed}: signposts where the roads leave the places");
            Assert.GreaterOrEqual(rests, 1, $"seed {seed}: a rest spot");

            // Nothing set beside a road shuts off the floor round it.
            var field = new FlowField(map);
            field.Build(new[] { world.Spawn }, int.MaxValue - 1);
            foreach (var (x, y) in dressed)
                foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                    if (!map.IsSolid(nx, ny) && map[nx, ny] != TileType.Water && map[nx, ny] != TileType.Lava)
                        Assert.AreNotEqual(FlowField.Unreached, field.DistanceAt(nx, ny), $"seed {seed}: the floor at {nx},{ny} beside {map[x, y]} is shut off");
        }

        // Each style dresses its place with what marks it (2026-11-10): the harbour's lake, pier and boats, the temple's
        // altar and rune stones, the mine's entrance, rails and cart.
        [Test]
        public void TheHarbourTempleAndMineAreDressed([Range(1, 12)] int seed)
        {
            var world = WorldGenerator.Generate((ulong)seed, story: Tales.Shipped);
            int Count(string style, TileType type)
            {
                var site = world.Places.Single(p => p.Definition.Style == style);
                int n = 0;
                for (int y = site.Y - site.Radius; y <= site.Y + site.Radius; y++)
                    for (int x = site.X - site.Radius; x <= site.X + site.Radius; x++)
                        if (world.Map[x, y] == type) n++;
                return n;
            }
            Assert.Greater(Count("harbor", TileType.Water), 20, $"seed {seed}: the harbour's lake");
            Assert.Greater(Count("harbor", TileType.Dock), 6, $"seed {seed}: the pier");
            Assert.GreaterOrEqual(Count("harbor", TileType.Boat), 1, $"seed {seed}: boats");
            Assert.AreEqual(1, Count("ruins", TileType.Altar), $"seed {seed}: the altar");
            Assert.GreaterOrEqual(Count("ruins", TileType.RuneStone), 1, $"seed {seed}: rune stones");
            Assert.AreEqual(1, Count("mine", TileType.MineEntrance), $"seed {seed}: the mine entrance");
            Assert.Greater(Count("mine", TileType.Rail), 6, $"seed {seed}: rails");
        }

        // The demon nest's lord waits in an arena of its own beside the nest (2026-11-10, bosses.csv place), clear of
        // the other places, on the far side from the road in, walked to from the nest's waypoint.
        [Test]
        public void TheNestsLordHasAnArenaBesideIt([Range(1, 24)] int seed)
        {
            // The nest's and (2026-11-11) the harbour's: places.csv arena.
            var generated = WorldGenerator.Generate((ulong)seed, story: Tales.Shipped);
            Assert.AreEqual(generated.CaveArenas + 2, generated.Arenas.Count);
            foreach (string id in new[] { "demon_nest", "mist_harbor" })
            {
                Assert.IsTrue(generated.PlaceArenas.TryGetValue(id, out var arena), $"seed {seed}: no arena by {id}");
                var place = generated.Places.Single(p => p.Definition.Id == id);
                foreach (var other in generated.Places.Where(p => p != place))
                    Assert.Greater(Vec2.Distance(other.Center, arena), other.Radius + WorldGenerator.ArenaRadius, $"seed {seed}: {id}'s arena on {other.Definition.Id}");
                var field = new FlowField(generated.Map);
                field.Build(new[] { place.Center }, int.MaxValue - 1);
                Assert.AreNotEqual(FlowField.Unreached, field.DistanceAt(TileMap.Cell(arena.X), TileMap.Cell(arena.Y)), $"seed {seed}: {id}'s arena cannot be walked to");
            }
        }

        [Test]
        public void TheMadGeneralWaitsInTheNestsArenaAndDemonsKeepToTheNest()
        {
            var config = new WorldConfig { EnemyTarget = 0 };
            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, config, Bosses.Shipped, Classes.Shipped, Tiles.Shipped, story: Tales.Shipped);
            var world = host.World;
            var nest = world.Places.Single(p => p.Definition.Id == "demon_nest");
            var general = world.Bosses.Single(b => b.Definition.Id == "mad_general");
            Assert.Less(Vec2.Distance(general.Home, nest.Center), nest.Radius + WorldGenerator.ArenaRadius + 8, "beside the nest");
            Assert.AreEqual(Bosses.Shipped.Bosses.Count, world.Bosses.Count, "every boss has its arena");
            // Chapter 4's crown wraith (2026-11-11) waits beside the harbour, dropping the sunken crown and its two
            // legendaries.
            var harbor = world.Places.Single(p => p.Definition.Id == "mist_harbor");
            var wraith = world.Bosses.Single(b => b.Definition.Id == "crown_wraith");
            Assert.Less(Vec2.Distance(wraith.Home, harbor.Center), harbor.Radius + WorldGenerator.ArenaRadius + 32, "beside the harbour");
            Assert.AreEqual("sunken_crown", wraith.Definition.MaterialId);
            CollectionAssert.AreEquivalent(new[] { "wraith_circlet", "drowned_mantle" }, Gear.Data.Bases.Where(b => b.Legendary == "crown_wraith").Select(b => b.Id));

            // Monsters come over the whole map; round the nest only demons, and demons nowhere else.
            config.EnemyTarget = 400;
            Run(world, 3000);
            var demons = world.Enemies.Where(e => e.Definition.Place == "demon_nest").ToList();
            Assert.IsNotEmpty(demons, "some demons came");
            foreach (var demon in demons)
                Assert.LessOrEqual(Vec2.Distance(demon.Position, nest.Center), nest.Radius + World.HauntReach + 6, "a demon wandered far");
            foreach (var enemy in world.Enemies.Where(e => Vec2.Distance(e.Position, nest.Center) < nest.Radius + World.HauntReach - 6))
                Assert.AreEqual("demon_nest", enemy.Definition.Place, $"{enemy.Definition.Id} by the nest");
        }

        // The royal city's gates are cut where the roads come in, and its towers and great fountains are set clear of
        // them rather than cut away with the wall (2026-11-10): all four towers, and both fountains and lamp posts mirrored,
        // on every seed.
        [Test]
        public void TheRoyalCityKeepsItsTowersAndFountainsClearOfTheGates([Range(1, 24)] int seed)
        {
            var world = WorldGenerator.Generate((ulong)seed, story: Tales.Shipped);
            var city = world.Places.Single(p => p.Definition.Style == "kingdom");
            int towers = 0;
            var fountains = new List<(int X, int Y)>();
            for (int y = city.Y - city.Radius; y <= city.Y + city.Radius; y++)
                for (int x = city.X - city.Radius; x <= city.X + city.Radius; x++)
                {
                    if (world.Map[x, y] == TileType.Tower) towers++;
                    if (world.Map[x, y] == TileType.BigFountain) fountains.Add((x - city.X, y - city.Y));
                }
            Assert.AreEqual(4, towers, $"seed {seed}: towers");
            Assert.AreEqual(2, fountains.Count, $"seed {seed}: great fountains");
            // Mirrored across the middle, at the same height (a fountain is two tiles wide).
            Assert.AreEqual(fountains[0].Y, fountains[1].Y, $"seed {seed}: fountains at different heights");
            Assert.AreEqual(-1, fountains[0].X + fountains[1].X, $"seed {seed}: fountains not mirrored");
            // The lamp posts beside them too.
            var lamps = new List<(int X, int Y)>();
            for (int y = city.Y - city.Radius; y <= city.Y + city.Radius; y++)
                for (int x = city.X - city.Radius; x <= city.X + city.Radius; x++)
                    if (world.Map[x, y] == TileType.LampPost) lamps.Add((x - city.X, y - city.Y));
            Assert.AreEqual(2, lamps.Count, $"seed {seed}: lamp posts");
            Assert.AreEqual((0, lamps[0].Y), (lamps[0].X + lamps[1].X, lamps[1].Y), $"seed {seed}: lamp posts not mirrored");
            // And the small things: each has its mirror (2026-11-10), moved rather than left out where a spot holds
            // both, so at least two pairs of pillars and four pairs of the rest still stand.
            for (int y = city.Y - city.Radius; y <= city.Y + city.Radius; y++)
                for (int x = city.X - city.Radius; x <= city.X + city.Radius; x++)
                {
                    var type = world.Map[x, y];
                    if (!Structures.IsDecor(type) || type == TileType.StructurePart || type == TileType.CastleWall || type == TileType.Tower) continue;
                    int mirror = city.X - (x - city.X) - Structures.Footprint(type).W + 1;
                    Assert.AreEqual(type, world.Map[mirror, y], $"seed {seed}: {type} at {x - city.X},{y - city.Y} has no mirror");
                }
            int pillars = 0, small = 0;
            for (int y = city.Y - city.Radius; y <= city.Y + city.Radius; y++)
                for (int x = city.X - city.Radius; x <= city.X + city.Radius; x++)
                {
                    var type = world.Map[x, y];
                    if (type == TileType.Pillar) pillars++;
                    if (type == TileType.Statue || type == TileType.Hedge || type == TileType.RoyalBanner || type == TileType.Flowerbed) small++;
                }
            Assert.GreaterOrEqual(pillars, 4, $"seed {seed}: pillars");
            Assert.GreaterOrEqual(small, 8, $"seed {seed}: statues, hedges, banners and flowerbeds");
        }

        // The open tiles walked to from (x, y) within reach of it.
        static HashSet<(int, int)> Walked(TileMap map, int cx, int cy, int reach)
        {
            var seen = new HashSet<(int, int)> { (cx, cy) };
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue((cx, cy));
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if ((nx - cx) * (nx - cx) + (ny - cy) * (ny - cy) > reach * reach || map.IsSolid(nx, ny) || !seen.Add((nx, ny))) continue;
                    queue.Enqueue((nx, ny));
                }
            }
            return seen;
        }

        [Test]
        public void AStepIsTakenFromItsGiverAndDoneOnReachingThePlace()
        {
            var host = Host();
            var world = host.World;
            var player = world.AddPlayer("a", null);
            var chief = world.Places[0].PeopleAt(world.Clock).First(p => p.Character.Id == "chief").At;
            Assert.AreEqual(0, player.JourneyStep);

            player.Position = Beside(world.Places[0].PeopleAt(world.Clock).First(p => p.Character.Id == "mina").At);
            Assert.IsFalse(world.TalkInStory(player.Id), "mina does not give the first step");
            player.Position = Beside(chief);
            Assert.IsTrue(world.TalkInStory(player.Id));
            Assert.IsTrue(player.JourneyTaken);
            Assert.AreEqual(0, player.JourneyStep, "still to reach the royal city");

            int coins = player.Inventory.CountOf(Gear.Data.FindMaterial("coin"));
            player.Position = world.Places[1].Center + new Vec2(1, 0);
            Run(world, World.TicksPerSecond);
            Assert.AreEqual(1, player.JourneyStep, "reaching the city finishes it");
            Assert.IsFalse(player.JourneyTaken);
            Assert.AreEqual(coins + 30, player.Inventory.CountOf(Gear.Data.FindMaterial("coin")), "and pays its reward");
        }

        [Test]
        public void ABossStepAndABringStepAndATalkOnlyStep()
        {
            var host = Host();
            var world = host.World;
            var player = world.AddPlayer("a", null);
            var queen = world.Places[1].PeopleAt(world.Clock).First(p => p.Character.Id == "queen").At;
            world.RestoreJourney(player, 1, false, null);

            player.Position = Beside(queen);
            Assert.IsTrue(world.TalkInStory(player.Id));
            Assert.AreEqual(1, player.JourneyStep, "the warden is still up");
            player.Records[(RecordKind.Boss, "cave_warden")] = 1;
            Run(world, World.TicksPerSecond);
            Assert.AreEqual(2, player.JourneyStep, "done once the warden has fallen");

            // Step 4 asks for five roasted mushrooms, taken when the player talks to the hero again holding them.
            world.RestoreJourney(player, 3, false, null);
            var hero = world.Places[2].PeopleAt(world.Clock).First(p => p.Character.Id == "hero").At;
            player.Position = Beside(hero);
            Assert.IsTrue(world.TalkInStory(player.Id));
            Assert.AreEqual(3, player.JourneyStep);
            var food = Gear.Data.Consumables.First(c => c.Id == "roasted_mushroom");
            player.Inventory.AddConsumable(food, 6);
            Run(world, World.TicksPerSecond);
            Assert.AreEqual(3, player.JourneyStep, "brought things wait for the talk");
            Assert.IsTrue(world.TalkInStory(player.Id));
            Assert.AreEqual(4, player.JourneyStep);
            Assert.AreEqual(1, player.Inventory.CountOf(food), "five taken");

            // Step 8 is only words: done on talking.
            world.RestoreJourney(player, 7, false, null);
            Assert.IsTrue(world.TalkInStory(player.Id));
            Assert.AreEqual(8, player.JourneyStep);
        }

        [Test]
        public void WaypointsOpenWhenStoodOnAndTakeAPlayerBetweenOpenOnes()
        {
            var host = Host();
            var world = host.World;
            var player = world.AddPlayer("a", null);
            Run(world, World.TicksPerSecond);
            Assert.IsTrue(player.Waypoints.Contains("dawn_village"), "the spawn is on the village's waypoint");
            Assert.IsFalse(world.Teleport(player.Id, 1), "the city's is not open yet");

            player.Position = world.Places[1].Center;
            Run(world, World.TicksPerSecond);
            Assert.IsTrue(player.Waypoints.Contains("royal_city"));
            player.Position = world.Places[1].Center + new Vec2(4, 0);
            Assert.IsFalse(world.Teleport(player.Id, 0), "only from beside a waypoint");
            player.Position = world.Places[1].Center;
            int corrections = player.Corrections;
            Assert.IsTrue(world.Teleport(player.Id, 0));
            Assert.Less(Vec2.Distance(player.Position, world.Places[0].Center), 2f);
            Assert.Greater(player.Corrections, corrections, "the client snaps there");
        }

        // Choices (2026-11-10, story.csv choices and next): the queen asks at step 9; talking takes the step but does not
        // finish it, an answer finishes it and leads to step 10 or 11, and each branch ends the story so far.
        [Test]
        public void TheQueensQuestionBranchesTheStory([Values(0, 1)] int answer)
        {
            var asks = Tales.Shipped.Steps[8];
            Assert.AreEqual(2, asks.Choices.Count);
            Assert.AreEqual(9, asks.Choices[0].Step);
            Assert.AreEqual(10, asks.Choices[1].Step);
            Assert.AreEqual(21, Tales.Shipped.Steps[9].Next, "the first branch goes on to chapter 4, not into the second");

            var host = Host();
            var world = host.World;
            var player = world.AddPlayer("a", null);
            var queen = world.Places.SelectMany(p => p.PeopleAt(world.Clock)).Single(p => p.Character.Id == "queen");
            world.RestoreJourney(player, 8, false, null);
            player.Position = Beside(queen.At);
            int coins = player.Inventory.CountOf(Gear.Data.FindMaterial("coin"));
            Assert.IsTrue(world.TalkInStory(player.Id));
            Assert.AreEqual(8, player.JourneyStep, "talking does not answer");
            Assert.IsTrue(player.JourneyTaken);
            Assert.IsFalse(world.ChooseInStory(player.Id, 2), "no third answer");
            Assert.IsTrue(world.ChooseInStory(player.Id, answer));
            Assert.AreEqual(9 + answer, player.JourneyStep);
            Assert.AreEqual(answer, player.StoryChoices[8]);
            Assert.AreEqual(coins + 150, player.Inventory.CountOf(Gear.Data.FindMaterial("coin")), "the step's reward");

            // The queen answers in turn, and chapter 4 begins with the hero.
            Assert.IsTrue(world.TalkInStory(player.Id));
            Assert.AreEqual(21, player.JourneyStep);
            Assert.AreEqual("hero", world.StepOf(player).Who.Id);
        }

        [Test]
        public void ChoicesAreRememberedInTheSaveAndSentToTheClient()
        {
            var bytes = CharacterSave.Write(new Equipment(), new Inventory(), 10, journeyStep: 10, waypoints: new[] { "dawn_village" },
                storyChoices: new[] { (8, 1) });
            var saved = CharacterSave.Read(bytes, Gear.Data);
            CollectionAssert.AreEqual(new[] { (8, 1) }, saved.StoryChoices);

            var host = Host();
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped)
            {
                Story = Tales.Shipped,
                Character = bytes,
            };
            host.Receive(1, client.JoinMessage());
            for (int i = 0; i < 4; i++)
            {
                host.Tick();
                foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            }
            Assert.AreEqual(1, client.StoryChoices[8]);
            var back = CharacterSave.Read(client.SaveCharacter(), Gear.Data);
            CollectionAssert.AreEqual(new[] { (8, 1) }, back.StoryChoices);
        }

        [Test]
        public void ChoicesOnlyWhereNothingIsAskedAndToStepsThatAre()
        {
            var data = StorySheet.Parse(
                "id,name,distance,radius,road\na,가,0,7,\n",
                "id,name,place\np,사람,a\n",
                "step,title,who,lines,goal,choices,next\n1,하나,p,안녕,,예>9|아니오>1,\n2,둘,p,안녕,,하나뿐>1,\n3,셋,p,안녕,,,7\n4,넷,p,안녕,reach:a,예>2|아니오>1,\n",
                Gear.Data, Bosses.Shipped);
            string all = string.Join("\n", data.Errors);
            StringAssert.Contains("choices는 goal이 빈 단계", all);
            StringAssert.Contains("choices의 step '9'", all);
            StringAssert.Contains("choices는 둘 이상", all);
            StringAssert.Contains("next '7'", all);
        }

        // The people go about their plaza by day and are home at night (2026-11-11): wanderers move, those who stay do
        // not, and wherever they are is open floor of their own place.
        [Test]
        public void PeopleWalkTheirRoundsByDayAndAreHomeAtNight([Range(1, 6)] int seed)
        {
            var world = WorldGenerator.Generate((ulong)seed, story: Tales.Shipped);
            int moving = 0;
            foreach (var site in world.Places)
            {
                foreach (var (character, home) in site.People)
                {
                    var seen = new HashSet<(int, int)>();
                    for (long clock = 0; clock < Calendar.TicksPerDay; clock += World.TicksPerSecond * 3)
                    {
                        var at = site.PersonAt(character, home, clock);
                        Assert.IsFalse(world.Map.IsSolid(TileMap.Cell(at.X), TileMap.Cell(at.Y)), $"seed {seed}: {character.Id} in something solid");
                        Assert.LessOrEqual(Vec2.Distance(at, site.Center), site.Radius, $"seed {seed}: {character.Id} out of {site.Definition.Id}");
                        if (Calendar.IsNight(clock)) Assert.AreEqual(home, at, $"seed {seed}: {character.Id} is home at night");
                        seen.Add(((int)(at.X * 2), (int)(at.Y * 2)));
                    }
                    if (character.Walk == "stay") Assert.AreEqual(1, seen.Count, $"{character.Id} stays put");
                    else if (seen.Count > 1) moving++;
                }
            }
            Assert.GreaterOrEqual(moving, 6, $"seed {seed}: most people go about");
        }

        // The places are safe (2026-11-11): a monster knocked or wandering into one is set back outside its edge, and
        // none goes after a player standing inside one.
        [Test]
        public void MonstersKeepOutOfThePlaces()
        {
            var host = Host();
            var world = host.World;
            var village = world.Places[0];
            var player = world.AddPlayer("a", null);
            player.Position = village.Center + new Vec2(2, 0);
            var slime = Monsters.Shipped.Monsters.First(m => m.Id == "slime");
            var inside = world.SpawnEnemy(slime, village.Center + new Vec2(-2, 0));
            var outside = world.SpawnEnemy(slime, village.Center + new Vec2(village.Radius + 3, 0));
            Run(world, World.TicksPerSecond * 3);
            foreach (var enemy in new[] { inside, outside })
                Assert.Greater(Vec2.Distance(enemy.Position, village.Center), village.Radius + World.SafeMargin, "kept outside the village");
            Assert.AreEqual(player.Stats.MaxHealth, player.Health, 0.01f, "nothing reached the player inside");
            Assert.IsTrue(world.InSafePlace(player.Position));

            // Out of the village, the slime comes for them again.
            player.Position = outside.Position + new Vec2(2.5f, 0);
            Run(world, World.TicksPerSecond);
            Assert.AreNotEqual(EnemyState.Idle, outside.State, "a player outside is fair game");
        }

        static int Index(string chain, int nth = 0) => Tales.Shipped.Steps.Where(s => s.Chain == chain).ElementAt(nth).Index;

        [Test]
        public void SideStoriesOpenAfterTheirMainStepAndGoApartFromIt()
        {
            var story = Tales.Shipped;
            CollectionAssert.AreEqual(new[] { "harbor", "mine", "temple", "nest" }, story.Chains.Select(c => c.Id));
            Assert.AreEqual(6, story.Chain("nest").After, "the general waits until the crystal queen sleeps");
            foreach (var chain in story.Chains)
            {
                var rows = story.Steps.Where(s => s.Chain == chain.Id).ToList();
                Assert.AreEqual(rows[0], chain.First);
                Assert.AreEqual(story.Steps.Count, rows.Last().Next, $"{chain.Id} ends, not into another story");
                for (int i = 0; i + 1 < rows.Count; i++) Assert.AreEqual(rows[i + 1].Index, rows[i].Next, $"{chain.Id} goes row by row");
            }
            Assert.AreEqual(21, story.Steps[10].Next, "both branches go on to chapter 4, past the side stories' rows");
            // Chapter 4's three endings go on to chapter 5 (2026-11-11), which ends the story.
            foreach (int ending in new[] { 25, 26, 27 }) Assert.AreEqual(28, story.Steps[ending].Next, "each ending goes on to chapter 5");
            Assert.AreEqual(story.Steps.Count, story.Steps[32].Next, "chapter 5 ends the story");
            Assert.AreEqual("void_lord", story.Steps[31].Boss);

            var host = Host();
            var world = host.World;
            var player = world.AddPlayer("a", null);
            CollectionAssert.AreEqual(new[] { "" }, world.StepsOf(player).Select(s => s.Step.Chain), "only the main story at first");
            world.RestoreJourney(player, 1, false, null);
            CollectionAssert.AreEqual(new[] { "", "harbor", "mine" }, world.StepsOf(player).Select(s => s.Step.Chain), "the temple waits for step 3");

            // The fisher asks for minnows; the main story is not taken by it.
            PersonAt(world, "fisher_ida", player);
            Assert.IsTrue(world.TalkInStory(player.Id));
            Assert.IsTrue(player.SideTaken.Contains("harbor"));
            Assert.IsFalse(player.JourneyTaken);
            Assert.AreEqual(1, player.JourneyStep);
            player.Inventory.AddMaterial(Gear.Data.FindMaterial("cave_minnow"), 3);
            Assert.IsTrue(world.TalkInStory(player.Id));
            Assert.AreEqual(Index("harbor", 1), player.SideSteps["harbor"]);
            Assert.IsFalse(player.SideTaken.Contains("harbor"));
            Assert.AreEqual(0, player.Inventory.CountOf(Gear.Data.FindMaterial("cave_minnow")), "the minnows are taken");

            // The harbour master sends them to the temple, done on arriving.
            PersonAt(world, "harbor_master", player);
            Assert.IsTrue(world.TalkInStory(player.Id));
            player.Position = world.SiteOf(story.Place("old_temple")).Center + new Vec2(1, 0);
            Run(world, World.TicksPerSecond);
            Assert.AreEqual(Index("harbor", 2), player.SideSteps["harbor"]);

            // The temple keeper finishes it; their own story is not open yet, so the harbour's is what they give.
            PersonAt(world, "temple_keeper", player);
            int experience = player.Experience;
            Assert.IsTrue(world.TalkInStory(player.Id));
            Assert.AreEqual(story.Steps.Count, player.SideSteps["harbor"]);
            Assert.AreEqual(experience + 80, player.Experience);
            CollectionAssert.AreEqual(new[] { "", "mine" }, world.StepsOf(player).Select(s => s.Step.Chain), "the harbour's story is done");
            Assert.AreEqual(1, player.JourneyStep, "and the main story never moved");
        }

        // The journal's story so far (2026-11-11): the steps passed follow the answer given, and stop at the step on.
        [Test]
        public void TheStorySoFarFollowsTheAnswers()
        {
            var story = Tales.Shipped;
            var kept = story.Passed(0, 21, new Dictionary<int, int> { [8] = 1 });
            CollectionAssert.AreEqual(Enumerable.Range(0, 9).Append(10), kept.Select(s => s.Index), "through the queen's question to the branch kept");
            var told = story.Passed(0, 21, new Dictionary<int, int> { [8] = 0 });
            CollectionAssert.AreEqual(Enumerable.Range(0, 10), told.Select(s => s.Index));
            var crowned = story.Passed(0, story.Steps.Count, new Dictionary<int, int> { [8] = 1, [24] = 2 });
            CollectionAssert.AreEqual(new[] { 21, 22, 23, 24, 27, 28, 29, 30, 31, 32 }, crowned.Skip(10).Select(s => s.Index), "chapter 4 to the queen's ending, then chapter 5");
            Assert.AreEqual(3, story.Passed(0, 3, null).Count, "up to the step on now");
            int harbor = Index("harbor");
            Assert.AreEqual(2, story.Passed(harbor, Index("harbor", 2), null).Count);
        }

        static void PersonAt(World world, string id, Player player) =>
            player.Position = Beside(world.Places.SelectMany(p => p.PeopleAt(world.Clock)).Single(p => p.Character.Id == id).At);

        [Test]
        public void TheNestsSideStorySendsThemAfterTheGeneral()
        {
            var host = Host();
            var world = host.World;
            var player = world.AddPlayer("a", null);
            world.RestoreJourney(player, 5, false, null);
            Assert.IsFalse(world.StepsOf(player).Any(s => s.Step.Chain == "nest"), "not before the crystal queen");
            world.RestoreJourney(player, 6, false, null);
            var nest = world.StepsOf(player).Single(s => s.Step.Chain == "nest").Step;
            Assert.AreEqual("demon_watch", nest.Who.Id);
            Assert.AreEqual("mad_general", nest.Boss);

            PersonAt(world, "demon_watch", player);
            Assert.IsTrue(world.TalkInStory(player.Id));
            Assert.AreEqual(nest.Index, world.StepsOf(player).Single(s => s.Step.Chain == "nest").Step.Index, "the general is still up");
            player.Records[(RecordKind.Boss, "mad_general")] = 1;
            Run(world, World.TicksPerSecond);
            Assert.AreEqual(Index("nest", 1), player.SideSteps["nest"], "done once the general has fallen");

            // The elder gives the main step first, then the nest's.
            PersonAt(world, "demon_elder", player);
            Assert.IsTrue(world.TalkInStory(player.Id));
            Assert.IsTrue(player.JourneyTaken, "the main story's step 7 came first");
            Assert.AreEqual(Index("nest", 1), player.SideSteps["nest"]);
            world.RestoreJourney(player, Tales.Shipped.Steps.Count, false, null);
            Assert.IsTrue(world.TalkInStory(player.Id));
            Assert.AreEqual(Tales.Shipped.Steps.Count, player.SideSteps["nest"]);

            // The general drops his crest and his two legendaries, and the guide asks for the crest.
            var general = Bosses.Shipped.Bosses.Single(b => b.Id == "mad_general");
            Assert.AreEqual("general_crest", general.MaterialId);
            CollectionAssert.AreEquivalent(new[] { "general_blade", "legion_mantle" }, Gear.Data.Bases.Where(b => b.Legendary == "mad_general").Select(b => b.Id));
            Assert.IsTrue(Guide.Shipped.Goals.Any(g => g.Conditions.Any(c => c.Kind == "have" && c.Id == "general_crest")));
        }

        [Test]
        public void LinesFollowTheAnswerGivenEarlier()
        {
            var story = Tales.Shipped;
            var master = story.Steps[Index("harbor", 1)];
            var none = master.LinesFor(new Dictionary<int, int>());
            var told = master.LinesFor(new Dictionary<int, int> { [8] = 0 });
            var kept = master.LinesFor(new Dictionary<int, int> { [8] = 1 });
            Assert.IsTrue(none.All(l => l.When == null), "no answer yet: only the lines for everyone");
            Assert.IsTrue(told.Any(l => l.Text.Contains("근위대")) && !told.Any(l => l.Text.Contains("우리끼리")));
            Assert.IsTrue(kept.Any(l => l.Text.Contains("우리끼리")) && !kept.Any(l => l.Text.Contains("근위대")));
            Assert.AreEqual(none.Count + 1, told.Count);

            var hero = story.Character("hero");
            var said = Enumerable.Range(0, 12).Select(day => hero.LineFor(day)).ToList();
            var thanked = Enumerable.Range(0, 12).Select(day => hero.LineFor(day, new Dictionary<int, int> { [8] = 1 })).ToList();
            Assert.IsFalse(said.Any(l => l.Contains("고맙다")), "only after keeping the secret");
            Assert.IsTrue(thanked.Any(l => l.Contains("고맙다")));
            Assert.IsFalse(thanked.Any(l => l.Contains("여왕이 알았다지")));
        }

        [Test]
        public void SideStoryAndConditionErrorsSayWhatIsWrong()
        {
            var data = StorySheet.Parse(
                "id,name,distance,radius,road\na,가,0,7,\n",
                "id,name,place,lines\np,사람,a,안녕|?9=1 그래\nq,사람,a,?1=x 음\n",
                "step,title,who,lines,goal,choices,next,chain,after\n" +
                "1,하나,p,안녕,,,,,\n" +
                "2,둘,p,안녕|?1=1 어,,,,side,1\n" +
                "3,셋,p,안녕,,,,side,2\n" +
                "4,넷,p,안녕,,,1,side,\n" +
                "5,다섯,p,?1=0 어,,,,,\n",
                Gear.Data, Bosses.Shipped);
            string all = string.Join("\n", data.Errors);
            StringAssert.Contains("조건 '?1=1'의 step은 choices가 있는 step", all);
            StringAssert.Contains("next '1'은 같은 이야기", all);
            StringAssert.Contains("after는 곁이야기(chain)의 첫 줄에만", all);
            StringAssert.Contains("조건(?step=n) 없는 말", all);
            StringAssert.Contains("조건의 답 번호", all);
            StringAssert.Contains("characters.csv 'p'", all);

            var first = StorySheet.Parse("id,name,distance,radius,road\na,가,0,7,\n", "id,name,place\np,사람,a\n",
                "step,title,who,lines,chain\n1,하나,p,안녕,side\n", Gear.Data, Bosses.Shipped);
            StringAssert.Contains("step 1은 큰 이야기", string.Join("\n", first.Errors));
        }

        [Test]
        public void SideStoriesAreKeptInTheSaveAndSentToTheClient()
        {
            int harbor = Index("harbor", 1);
            var bytes = CharacterSave.Write(new Equipment(), new Inventory(), 10, journeyStep: 3, waypoints: new[] { "dawn_village" },
                sideStories: new[] { ("harbor", harbor, true), ("mine", Tales.Shipped.Steps.Count, false), ("gone", 3, false) });
            var saved = CharacterSave.Read(bytes, Gear.Data);
            CollectionAssert.AreEqual(new[] { ("harbor", harbor, true), ("mine", Tales.Shipped.Steps.Count, false), ("gone", 3, false) }, saved.SideStories);

            var host = Host();
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped)
            {
                Story = Tales.Shipped,
                Character = bytes,
            };
            host.Receive(1, client.JoinMessage());
            for (int i = 0; i < 4; i++)
            {
                host.Tick();
                foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            }
            Assert.AreEqual(harbor, client.SideSteps["harbor"]);
            Assert.IsTrue(client.SideTaken.Contains("harbor"));
            Assert.IsFalse(client.SideSteps.ContainsKey("gone"), "a side story no longer in the sheet is dropped");
            CollectionAssert.AreEqual(new[] { "", "harbor", "temple" }, client.CurrentSteps.Select(s => s.Step.Chain), "the mine's is done");
            var master = Tales.Shipped.Character("harbor_master");
            Assert.AreEqual(harbor, client.StepGivenBy(master)?.Step.Index);
            Assert.IsTrue(client.StepGivenBy(master)?.Taken);

            var back = CharacterSave.Read(client.SaveCharacter(), Gear.Data);
            CollectionAssert.AreEquivalent(new[] { ("harbor", harbor, true), ("mine", Tales.Shipped.Steps.Count, false) }, back.SideStories);
        }

        [Test]
        public void TheStoryOutlivesTheCharacterSaveAndReachesTheClient()
        {
            var bytes = CharacterSave.Write(new Equipment(), new Inventory(), 10, journeyStep: 3, journeyTaken: true, waypoints: new[] { "dawn_village", "royal_city" });
            var saved = CharacterSave.Read(bytes, Gear.Data);
            Assert.AreEqual(3, saved.JourneyStep);
            Assert.IsTrue(saved.JourneyTaken);
            CollectionAssert.AreEqual(new[] { "dawn_village", "royal_city" }, saved.Waypoints);

            var host = Host();
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped)
            {
                Story = Tales.Shipped,
                Character = bytes,
            };
            host.Receive(1, client.JoinMessage());
            for (int i = 0; i < 4; i++)
            {
                host.Tick();
                foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            }
            Assert.AreEqual(3, client.JourneyStep);
            Assert.IsTrue(client.JourneyTaken);
            Assert.AreEqual("hero", client.CurrentStep.Who.Id);
            CollectionAssert.IsSupersetOf(client.Waypoints, new[] { 0, 1 });
            Assert.AreEqual(host.World.Places.Count, client.Places.Count, "the client carves the same places from the seed");
            Assert.AreEqual(host.World.Places[2].Center, client.Places[2].Center);

            var back = CharacterSave.Read(client.SaveCharacter(), Gear.Data);
            Assert.AreEqual(3, back.JourneyStep);
            CollectionAssert.IsSupersetOf(back.Waypoints, new[] { "dawn_village", "royal_city" });
        }
    }
}
