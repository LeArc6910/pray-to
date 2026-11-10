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
            Assert.AreEqual(4, Tales.Shipped.Places.Count);
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
            Assert.AreEqual(4, world.Places.Count);
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
            var chief = world.Places[0].People.First(p => p.Character.Id == "chief").At;
            Assert.AreEqual(0, player.JourneyStep);

            player.Position = Beside(world.Places[0].People.First(p => p.Character.Id == "mina").At);
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
            var queen = world.Places[1].People.First(p => p.Character.Id == "queen").At;
            world.RestoreJourney(player, 1, false, null);

            player.Position = Beside(queen);
            Assert.IsTrue(world.TalkInStory(player.Id));
            Assert.AreEqual(1, player.JourneyStep, "the warden is still up");
            player.Records[(RecordKind.Boss, "cave_warden")] = 1;
            Run(world, World.TicksPerSecond);
            Assert.AreEqual(2, player.JourneyStep, "done once the warden has fallen");

            // Step 4 asks for five roasted mushrooms, taken when the player talks to the hero again holding them.
            world.RestoreJourney(player, 3, false, null);
            var hero = world.Places[2].People.First(p => p.Character.Id == "hero").At;
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
            Assert.AreEqual(4, client.Places.Count, "the client carves the same places from the seed");
            Assert.AreEqual(host.World.Places[2].Center, client.Places[2].Center);

            var back = CharacterSave.Read(client.SaveCharacter(), Gear.Data);
            Assert.AreEqual(3, back.JourneyStep);
            CollectionAssert.IsSupersetOf(back.Waypoints, new[] { "dawn_village", "royal_city" });
        }
    }
}
