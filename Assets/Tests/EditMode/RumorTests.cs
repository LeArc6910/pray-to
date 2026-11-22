using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // Rumours instead of arrows and the guide's goals for the things to find (2026-11-22, Docs/WorldRework.md 묶음 4c):
    // the people's rumour lines, what they point at, that the same day says the same, how well a way is known and what
    // the goal list says of it, the rumours kept with the map, and the goals for camps, shrines, chests, waystones,
    // minecarts and elites met by doing them.
    public class RumorTests
    {
        const int Size = 512;

        static HostConfig RoomConfig => new HostConfig { MapSize = Size, MapVersion = WorldGenerator.RoomVersion };

        static WorldHost Host(ulong seed = 3) => new WorldHost(seed, Monsters.Shipped, Gear.Data, RoomConfig, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped,
            Classes.Shipped, Tiles.Shipped, story: Tales.Shipped);

        static Vec2 OpenBeside(World world, Vec2 center)
        {
            for (float reach = 1; reach <= World.PoiReach; reach += 0.25f)
                for (int k = 0; k < 32; k++)
                {
                    var at = center + Vec2.FromAngle(2 * MathF.PI * k / 32) * reach;
                    if (!Collision.Overlaps(world.Map, at, World.PlayerRadius)) return at;
                }
            throw new InvalidOperationException($"no floor beside {center}");
        }

        static bool HasFloorBeside(World world, Poi poi)
        {
            try
            {
                OpenBeside(world, poi.Center);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        [Test]
        public void EveryPersonOfTheStoryTellsRumoursOfKindsThereAre()
        {
            Assert.IsTrue(Tales.Shipped.Success, string.Join("\n", Tales.Shipped.Errors));
            foreach (var who in Tales.Shipped.Characters)
            {
                Assert.IsNotEmpty(who.Rumors, who.Id);
                foreach (var (_, text) in who.Rumors) StringAssert.Contains("{dir}", text, who.Id);
            }
            var kinds = Tales.Shipped.Characters.SelectMany(c => c.Rumors.Select(r => r.Kind)).Distinct().ToList();
            foreach (var kind in new[] { RumorKind.Camp, RumorKind.Shrine, RumorKind.Chest, RumorKind.Hidden, RumorKind.Vault, RumorKind.Cart,
                         RumorKind.Station, RumorKind.Waystone, RumorKind.Elite, RumorKind.Wanderer })
                CollectionAssert.Contains(kinds, kind, $"someone tells of {kind}");
            Assert.IsTrue(Tales.Shipped.Characters.Single(c => c.Id == "mina").Rumors.Any(r => r.Kind == RumorKind.Wanderer),
                "the village child knows where the wandering merchant is today");
            // The hints as hearsay: the story's boss steps past the first chapter say no ring is the spot.
            foreach (var step in Tales.Shipped.Steps.Where(s => s.Goal == StoryGoal.Boss && s.Index > 2))
                StringAssert.DoesNotContain("붉은 고리가", step.Rumor, step.Title);

            var data = StorySheet.Parse(System.IO.File.ReadAllText(RepoFile.Find("Assets/_Project/Data/Story/places.csv")),
                "id,name,place,rumor\na,가,dawn_village,dragon:불을 뿜는다\nb,나,dawn_village,camp\n", "", Gear.Data, Bosses.Shipped);
            Assert.AreEqual(2, data.Errors.Count(e => e.Contains("rumor")), string.Join("\n", data.Errors));
        }

        [Test]
        public void ARumourPointsAtARealThingAndTheSameDaySaysTheSame()
        {
            var host = Host();
            var world = host.World;
            var targets = Rumors.Targets(world.Rooms, world.WandererAt, world.Biomes);
            Assert.Greater(targets.Count, 30);
            foreach (var target in targets)
            {
                switch (target.Kind)
                {
                    case RumorKind.Elite:
                        Assert.AreEqual(RoomRole.Elite, world.Rooms.Nodes[target.Index].Role);
                        break;
                    case RumorKind.Wanderer:
                        Assert.AreEqual((target.X, target.Y), world.WandererAt);
                        Assert.AreEqual(TileType.WandererStall, world.Map[target.X, target.Y]);
                        break;
                    default:
                        var poi = world.Pois[target.Index];
                        Assert.AreEqual((poi.X, poi.Y), (target.X, target.Y));
                        Assert.AreEqual(poi.Biome, target.Biome);
                        var expected = target.Kind switch
                        {
                            RumorKind.Camp => PoiKind.Camp,
                            RumorKind.Shrine => PoiKind.Shrine,
                            RumorKind.Cart => PoiKind.Cart,
                            RumorKind.Station => PoiKind.Station,
                            RumorKind.Waystone => PoiKind.Waystone,
                            _ => PoiKind.Chest,
                        };
                        Assert.AreEqual(expected, poi.Kind, target.Kind.ToString());
                        if (target.Kind == RumorKind.Hidden) Assert.AreEqual(RoomRole.Hidden, poi.Definition.Role);
                        if (target.Kind == RumorKind.Vault) Assert.AreEqual(RoomRole.Locked, poi.Definition.Role);
                        if (target.Kind == RumorKind.Station) Assert.IsTrue(world.Lines.Any(l => l.Broken == poi.Index), "only a line's broken end");
                        break;
                }
            }

            var miner = Tales.Shipped.Characters.Single(c => c.Id == "miner");
            var from = world.Spawn;
            var told = Rumors.Tell(miner, targets, 3, 5, from, null);
            Assert.IsNotNull(told);
            var (line, found) = told.Value;
            CollectionAssert.Contains(targets, found);
            Assert.IsTrue(found.Kind == RumorKind.Hidden || found.Kind == RumorKind.Chest);
            // The line names the way from where they stand and the thing's biome.
            StringAssert.Contains(BiomeMap.Name(found.Biome), line);
            StringAssert.Contains(Rumors.Compass(found.Center.X - from.X, found.Center.Y - from.Y) + "쪽", line);
            StringAssert.DoesNotContain("{", line);
            // One of the nearest of the kinds.
            var nearest = targets.Where(t => t.Kind is RumorKind.Hidden or RumorKind.Chest).OrderBy(t => Vec2.Distance(t.Center, from)).Take(Rumors.Nearest).ToList();
            CollectionAssert.Contains(nearest, found);

            // The same seed, person and day: the same; another host from the same seed too. Over days, more than one thing.
            Assert.AreEqual(told, Rumors.Tell(miner, targets, 3, 5, from, null));
            var again = Host();
            Assert.AreEqual(told, Rumors.Tell(miner, Rumors.Targets(again.World.Rooms, again.World.WandererAt, again.World.Biomes), 3, 5, from, null));
            Assert.Greater(Enumerable.Range(0, 20).Select(day => Rumors.Pick(targets, 3, "miner", day, from, new[] { RumorKind.Hidden, RumorKind.Chest }, null)).Distinct().Count(), 1);
            Assert.AreNotEqual(Rumors.Hash("miner"), Rumors.Hash("mina"));
            Assert.AreEqual(Rumors.Hash("miner"), Rumors.Hash("miner"));
        }

        [Test]
        public void AThingKnownIsNotToldAndTheWanderingMerchantIsNewsHoweverFar()
        {
            var world = Host().World;
            var targets = Rumors.Targets(world.Rooms, world.WandererAt, world.Biomes);
            var kinds = new[] { RumorKind.Camp };
            var known = new HashSet<RumorTarget>();
            for (int day = 0; day < 200; day++)
            {
                var pick = Rumors.Pick(targets, 3, "chief", day, world.Spawn, kinds, known.Contains);
                if (pick == null) break;
                Assert.IsFalse(known.Contains(pick.Value), "a camp already known is not told again");
                known.Add(pick.Value);
            }
            Assert.AreEqual(targets.Count(t => t.Kind == RumorKind.Camp), known.Count, "every camp in time, then nothing");
            Assert.IsNull(Rumors.Pick(targets, 3, "chief", 999, world.Spawn, kinds, known.Contains));
            Assert.IsNull(Rumors.Pick(targets, 3, "chief", 1, world.Spawn, Array.Empty<RumorKind>(), null), "no kinds, no rumour");

            // The wandering merchant: always among what may be told, even when it is the furthest thing of all (a stall in
            // the far corner here; a world with the people's sheet stands one by a camp, TravelTests).
            targets = Rumors.Targets(world.Rooms, (8, 8), world.Biomes);
            var wanderer = targets.Single(t => t.Kind == RumorKind.Wanderer);
            var far = new Vec2(world.Map.Width - wanderer.Center.X, world.Map.Height - wanderer.Center.Y);
            var picks = Enumerable.Range(0, 60).Select(day => Rumors.Pick(targets, 3, "mina", day, far, new[] { RumorKind.Chest, RumorKind.Wanderer }, t => true)).ToList();
            Assert.IsTrue(picks.All(p => p != null && p.Value.Kind == RumorKind.Wanderer), "known is never said of the merchant");
        }

        [Test]
        public void TheWayIsToldByHowWellItIsKnown()
        {
            Assert.AreEqual(WayKnown.Known, Rumors.Knowledge(false, false, 900), "arrows chosen");
            Assert.AreEqual(WayKnown.Known, Rumors.Knowledge(true, true, 900), "been there or the first chapter");
            Assert.AreEqual(WayKnown.Path, Rumors.Knowledge(true, false, Rumors.PathReach - 1), "near");
            Assert.AreEqual(WayKnown.Path, Rumors.Knowledge(true, false, 900, signposted: true), "a signpost seen");
            Assert.AreEqual(WayKnown.Rumor, Rumors.Knowledge(true, false, 900));

            var from = new Vec2(100, 100);
            var to = new Vec2(160, 160);
            Assert.AreEqual("북동 85칸", Rumors.Way(WayKnown.Known, from, to, Biome.Mushroom, Biome.Cave));
            Assert.AreEqual("북동쪽", Rumors.Way(WayKnown.Path, from, to, Biome.Mushroom, Biome.Cave));
            Assert.AreEqual("북동쪽, 버섯 숲 어딘가", Rumors.Way(WayKnown.Rumor, from, to, Biome.Mushroom, Biome.Cave));
            Assert.AreEqual("남쪽 어딘가", Rumors.Way(WayKnown.Rumor, from, new Vec2(100, 20), Biome.Cave, Biome.Cave), "in the same biome, no name");
            Assert.AreEqual("서", Rumors.Compass(-5, 0));

            // The faint ring holds the thing but is not centred on it, and is the same each time.
            for (int i = 0; i < 50; i++)
            {
                var center = Rumors.Circle(7, RumorKind.Chest, i, to, Rumors.RumorRadius);
                float off = Vec2.Distance(center, to);
                Assert.That(off, Is.InRange(Rumors.RumorRadius * 0.2f, Rumors.RumorRadius * 0.65f));
                Assert.AreEqual(center, Rumors.Circle(7, RumorKind.Chest, i, to, Rumors.RumorRadius));
            }

            // The first chapter (the way to the royal city, the warden, the hero's hut) is told as before.
            var story = Tales.Shipped;
            Assert.IsTrue(Rumors.FirstChapter(story, 0));
            Assert.IsTrue(Rumors.FirstChapter(story, 2));
            Assert.IsFalse(Rumors.FirstChapter(story, 3));
            Assert.IsFalse(Rumors.FirstChapter(story, story.Steps.Count), "the story done");
            Assert.IsFalse(Rumors.FirstChapter(null, 0));
        }

        [Test]
        public void RumoursHeardAreKeptWithTheMap()
        {
            const int width = 9, height = 7;
            var seen = new bool[width * height];
            seen[3] = seen[40] = true;
            var heard = new List<HeardRumor> { new HeardRumor(RumorKind.Camp, 4, 2), new HeardRumor(RumorKind.Wanderer, 0, 3), new HeardRumor(RumorKind.Elite, 120, 9) };
            var data = MapSave.Write(seen, width, height, heard);
            CollectionAssert.AreEqual(seen, MapSave.Read(data, width, height));
            var back = MapSave.ReadRumors(data, width, height);
            CollectionAssert.AreEqual(heard.Select(h => (h.Kind, h.Index, h.Day)), back.Select(h => (h.Kind, h.Index, h.Day)));
            Assert.IsEmpty(MapSave.ReadRumors(data, width + 1, height), "another map's");
            Assert.IsNull(MapSave.Read(data.Take(data.Length - 1).ToArray(), width, height), "cut short");
            Assert.IsEmpty(MapSave.ReadRumors(MapSave.Write(seen, width, height), width, height));

            // A map kept before (format 1: the runs alone) reads with no rumours.
            var old = (byte[])MapSave.Write(seen, width, height).Clone();
            old = old.Take(old.Length - 1).ToArray();
            old[4] = 1;
            CollectionAssert.AreEqual(seen, MapSave.Read(old, width, height));
            Assert.IsEmpty(MapSave.ReadRumors(old, width, height));

            // At most MaxRumors, the newest kept.
            var many = Enumerable.Range(0, MapSave.MaxRumors + 20).Select(i => new HeardRumor(RumorKind.Chest, i, 1)).ToList();
            var kept = MapSave.ReadRumors(MapSave.Write(seen, width, height, many), width, height);
            Assert.AreEqual(MapSave.MaxRumors, kept.Count);
            Assert.AreEqual(MapSave.MaxRumors + 19, kept.Last().Index);
        }

        [Test]
        public void TheGoalsOfTheThingsToFindAreMetByDoingThem()
        {
            var ids = Guide.Shipped.Goals.Select(g => g.Id).ToList();
            foreach (string id in new[] { "camp", "shrine", "chest", "wanderer", "waystone", "minecart", "hidden", "elite", "vault" })
            {
                Assert.Contains(id, ids);
                // After the first hour's bot stops (level 5), so the first half hour stays one line.
                Assert.Greater(ids.IndexOf(id), ids.IndexOf("level5"), id);
                Assert.IsNotEmpty(Guide.Shipped.Goals.Single(g => g.Id == id).Rumor, id);
                CollectionAssert.Contains(GuideProgress.AddedAfterCounts, id);
            }
            Assert.Greater(ids.IndexOf("vault"), ids.IndexOf("mithril_pickaxe"), "a vault wants the pickaxe that opens it");
            Assert.Greater(ids.IndexOf("minecart"), ids.IndexOf("anvil"), "iron bars first");
            StringAssert.DoesNotContain("HUD", Guide.Shipped.Goals.Single(g => g.Id == "queen").Hint);
            StringAssert.Contains("어딘가", Guide.Shipped.Goals.Single(g => g.Id == "queen").Rumor);

            var host = Host();
            var world = host.World;
            var client = new WorldClient("key", "a", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped) { Story = Tales.Shipped };
            host.Receive(1, client.JoinMessage());
            void Deliver()
            {
                foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            }
            host.Tick();
            Deliver();
            var player = world.FindPlayer(client.PlayerId);
            Goal Shipped(string id) => Guide.Shipped.Goals.Single(g => g.Id == id);
            int Index(string id) => ids.IndexOf(id);
            bool Met(Goal goal, GuideFacts facts) => goal.Conditions.Any(c => GuideProgress.IsMet(c, facts));
            foreach (string id in new[] { "camp", "shrine", "chest", "waystone", "minecart", "hidden", "elite", "vault" })
            {
                Assert.IsFalse(Met(Shipped(id), GuideFacts.Of(world, player)), id + " not yet, on the host");
                Assert.IsFalse(Met(Shipped(id), GuideFacts.Of(client)), id + " not yet, on the client");
            }

            var chest = world.Pois.First(p => p.Kind == PoiKind.Chest && p.Definition.Role == RoomRole.Treasure && HasFloorBeside(world, p));
            var hidden = world.Pois.First(p => p.Kind == PoiKind.Chest && p.Definition.Role == RoomRole.Hidden && HasFloorBeside(world, p));
            var shrine = world.Pois.First(p => p.Kind == PoiKind.Shrine && HasFloorBeside(world, p));
            var camp = world.Pois.First(p => p.Kind == PoiKind.Camp);
            player.Position = OpenBeside(world, chest.Center);
            Assert.IsTrue(world.UsePoi(player.Id, chest.Index));
            player.Position = OpenBeside(world, hidden.Center);
            Assert.IsTrue(world.UsePoi(player.Id, hidden.Index));
            player.Position = OpenBeside(world, shrine.Center);
            Assert.IsTrue(world.UsePoi(player.Id, shrine.Index));
            player.Position = OpenBeside(world, camp.Center);
            for (int i = 0; i < World.Ticks(World.RestSeconds) + 1; i++) host.Tick();
            world.OpenStone(player, world.Stones[0]);
            world.MendRail(world.Lines[0].Index);
            player.Inventory.TryAdd(Gear.Plain("iron_sword", rarity: Rarity.Rare));
            player.InventoryVersion++;
            for (int i = 0; i < 3; i++) host.Tick();
            Deliver();

            foreach (string id in new[] { "camp", "shrine", "chest", "waystone", "minecart", "hidden", "elite" })
            {
                Assert.IsTrue(Met(Shipped(id), GuideFacts.Of(world, player)), id + " met, on the host");
                Assert.IsTrue(Met(Shipped(id), GuideFacts.Of(client)), id + " met, on the client");
                Assert.IsTrue(world.ClaimGoal(player.Id, Shipped(id), Index(id)), id + " rewarded");
                Assert.IsFalse(world.ClaimGoal(player.Id, Shipped(id), Index(id)), id + " once");
            }
            Assert.IsFalse(Met(Shipped("vault"), GuideFacts.Of(world, player)), "a treasure chest is no vault");
            var vault = world.Pois.FirstOrDefault(p => p.Kind == PoiKind.Chest && p.Definition.Role == RoomRole.Locked && HasFloorBeside(world, p));
            if (vault != null)
            {
                player.Position = OpenBeside(world, vault.Center);
                Assert.IsTrue(world.UsePoi(player.Id, vault.Index));
                Assert.IsTrue(Met(Shipped("vault"), GuideFacts.Of(world, player)));
            }

            // The sheet: the new conditions and their mistakes.
            var sheet = GuideSheet.Parse("id,text,hint,done,reward,rumor\n" +
                "a,가,힌트,poi:camp,xp:1,소문\nb,나,,poi:castle,xp:1\nc,다,,rarity:shiny,xp:1\nd,라,,stone:2,xp:1\ne,마,,rail|rarity:epic,xp:1\n", Gear.Data);
            Assert.AreEqual(3, sheet.Errors.Count, string.Join("\n", sheet.Errors));
            Assert.AreEqual("소문", sheet.Goals[0].Rumor);
            Assert.AreEqual("힌트", sheet.Goals[0].Hint);
            Assert.AreEqual((int)Rarity.Epic, sheet.Goals[1].Conditions[1].Count);
            Assert.AreEqual(sheet.Goals[1].Hint, sheet.Goals[1].Rumor, "no rumor column: the hint");
        }
    }
}
