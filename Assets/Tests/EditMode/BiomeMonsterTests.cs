using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The monsters and rules of the first two biomes (2026-11-24, Docs/BiomeRework.md B1b): each has six kinds of its own
    // of several makes and sizes; the dirt cave's rats keep out of lamplight and come out of the timbers of its wide ways;
    // its big beetle is dazed by a wall; a slime mother sheds slimes; the mushroom forest's spore clouds (a puffball's
    // blast, a cap giant's lump, a glowing shooter's sac) fill the monsters in them and tell the screen how much; a camp
    // there may be led by an L body, never an XL.
    public class BiomeMonsterTests
    {
        static readonly MonsterData Shipped = Monsters.Shipped;

        static World Room(int size = 30) =>
            new World(Maps.Room(size), TileMap.Center(size / 2, size / 2), Shipped, 1, new WorldConfig { EnemyTarget = 0 });

        static List<SimEvent> Run(World world, float seconds, Action each = null)
        {
            var events = new List<SimEvent>();
            for (int i = 0; i < World.Ticks(seconds); i++)
            {
                world.Step();
                events.AddRange(world.TakeEvents());
                each?.Invoke();
            }
            return events;
        }

        static Enemy Spawn(World world, string id, Vec2 at) => world.SpawnEnemy(Shipped.Find(id), at);

        // ---- The sheets ----

        // The kinds a biome's own field packs hold (its rows of packs.csv; the plain cave's rows for the dirt cave).
        static List<MonsterDefinition> KindsOf(Biome? biome) => Shipped.Packs.Packs
            .Where(p => p.Place == null && p.Biome == biome && p.Weight > 0)
            .SelectMany(p => p.Members.Select(m => m.Monster)).Distinct().ToList();

        [TestCase(null, "cave_rat", "big_beetle", "armored_beetle")]
        [TestCase(Biome.Mushroom, "puffball", "cap_giant", "glow_shooter", "myconid_captain")]
        public void EachOfTheFirstTwoBiomesHasSixKindsOfSeveralMakesAndSizes(Biome? biome, params string[] fresh)
        {
            var kinds = KindsOf(biome);
            foreach (var id in fresh) CollectionAssert.Contains(kinds.Select(k => k.Id), id, $"{biome}: its new {id} is in its packs");
            Assert.That(kinds.Count, Is.GreaterThanOrEqualTo(6), string.Join(", ", kinds.Select(k => k.Id)));
            Assert.That(kinds.Select(k => k.Role).Distinct().Count(), Is.GreaterThanOrEqualTo(3), "three makes at least (swarm, shooter, heavy…)");
            Assert.That(kinds.Select(k => k.Size).Distinct().Count(), Is.GreaterThanOrEqualTo(3), "three sizes at least");
            Assert.IsTrue(kinds.All(k => k.Size <= MonsterSize.L), "the field holds L at most");
        }

        [Test]
        public void TheMidBossesAreXlAndComeInNoPack()
        {
            foreach (var id in new[] { "slime_mother", "mycelium_mother" })
            {
                var boss = Shipped.Find(id);
                Assert.AreEqual(MonsterSize.XL, boss.Size, id);
                Assert.AreEqual(0, boss.Weight, $"{id}: never one by one in the field");
                Assert.IsFalse(Shipped.Packs.Packs.Any(p => p.Members.Any(m => m.Monster == boss)), $"{id}: in no pack");
                Assert.That(boss.Attacks.Count, Is.InRange(2, 3), id);
            }
            Assert.IsTrue(Shipped.Find("slime_mother").Has(MonsterTraitKind.Shed));
            Assert.IsTrue(Shipped.Find("mycelium_mother").Attacks.Any(a => a.Shape == MonsterAttackShape.Summon && a.Summon.Id == "puffball"));
            Assert.IsTrue(Shipped.Find("mycelium_mother").Attacks.Count(a => a.Pool == MonsterPool.Cloud) >= 2, "it lays several clouds");
        }

        [Test]
        public void ANewTraitReadsAndABadOneSaysWhatIsWrong()
        {
            const string Header = "id,name,level,health,damage,armor,speed,radius,aggro,reach,windup,cooldown,weight,traits,size\n";
            Assert.IsTrue(MonsterSheet.Parse(Header + "r,쥐,1,8,5,0,3,0.22,9,0.4,0.45,0.8,0,fearlight,xs\n").Success);
            Assert.IsTrue(MonsterSheet.Parse(Header + "r,쥐,1,8,5,0,3,0.22,9,0.4,0.45,0.8,0,fearlight:4,xs\n").Success);
            Assert.IsFalse(MonsterSheet.Parse(Header + "r,쥐,1,8,5,0,3,0.22,9,0.4,0.45,0.8,0,fearlight:40,xs\n").Success);
            string small = "s,작은,1,10,1,0,2,0.4,8,0.5,0.5,1,0,,m\n";
            Assert.IsTrue(MonsterSheet.Parse(Header + small + "b,큰,3,600,1,0,1,1.3,8,0.8,1,2,0,shed:s:10,xl\n").Success);
            StringAssert.Contains("더 작은 등급", MonsterSheet.Parse(Header + small + "b,큰,3,600,1,0,1,0.4,8,0.8,1,2,0,shed:s:10,m\n").Errors.Single());
            StringAssert.Contains("없습니다", MonsterSheet.Parse(Header + "b,큰,3,600,1,0,1,1.3,8,0.8,1,2,0,shed:nope:10,xl\n").Errors.Single());
            Assert.IsFalse(MonsterSheet.Parse(Header + small + "b,큰,3,600,1,0,1,1.3,8,0.8,1,2,0,shed:s,xl\n").Success);
            Assert.IsTrue(MonsterSheet.Parse(Header + "b,벌레,1,60,1,0,2,0.4,8,4,0.7,2,0,stagger:2.5,m\n").Success);
            Assert.AreEqual((MonsterPool.Cloud, 3f, 1.6f), MonsterAttackSheet.PoolOf("cloud:3:1.6"));
        }

        // ---- Light (fearlight) ----

        [Test]
        public void TheLightGivingTilesAreOneListAndReachSixTiles()
        {
            CollectionAssert.IsSubsetOf(new[] { TileType.Torch, TileType.Lamp, TileType.Campfire }, Lights.All);
            Assert.IsFalse(Lights.Gives(TileType.Wall));
            var world = Room();
            Assert.IsFalse(world.FearsLight(TileMap.Center(10, 15)));
            world.Map[5, 15] = TileType.Torch;
            Assert.IsTrue(world.FearsLight(TileMap.Center(10, 15)), "five tiles off");
            Assert.IsTrue(world.FearsLight(TileMap.Center(11, 15)), "six");
            Assert.IsFalse(world.FearsLight(TileMap.Center(12, 15)), "seven");
            Assert.IsFalse(world.FearsLight(TileMap.Center(10, 15), 3), "a shorter fear of its own");
            world.Map[5, 15] = TileType.Floor;
            Assert.IsFalse(world.FearsLight(TileMap.Center(10, 15)), "the torch taken up: dark again");
        }

        [Test]
        public void ACrowdOfRatsAtTheLightsEdgeDoesNotShoveOneIn()
        {
            // Ten rats bunched where they stop: pushing apart, none is shoved over the edge (the web check caught two).
            var world = Room(40);
            var player = world.AddPlayer("a", null);
            player.Position = TileMap.Center(6, 20);
            world.Map[5, 20] = TileType.Torch;
            var rats = Enumerable.Range(0, 10).Select(i => Spawn(world, "cave_rat", TileMap.Center(13, 20) + new Vec2(0.05f * (i % 3), 0.05f * (i / 3)))).ToList();
            Run(world, 8, () =>
            {
                foreach (var rat in rats)
                    Assert.IsFalse(world.FearsLight(rat.Position), $"a rat shoved into the light at {rat.Position.X:0.0},{rat.Position.Y:0.0}");
            });
        }

        [Test]
        public void RatsDoNotComeIntoTorchlightAndOneTheLightFindsGoesOut()
        {
            var world = Room(40);
            var player = world.AddPlayer("a", null);
            player.Position = TileMap.Center(6, 20);
            world.Map[5, 20] = TileType.Torch;
            var rats = Enumerable.Range(0, 5).Select(i => Spawn(world, "cave_rat", TileMap.Center(14, 18 + i))).ToList();
            float nearest = float.MaxValue;
            Run(world, 8, () =>
            {
                foreach (var rat in rats)
                {
                    Assert.IsFalse(world.FearsLight(rat.Position), $"a rat in the light at {rat.Position.X:0.0},{rat.Position.Y:0.0}");
                    nearest = Math.Min(nearest, Vec2.Distance(rat.Position, player.Position));
                }
            });
            Assert.IsTrue(rats.All(r => r.State != EnemyState.Idle), "they saw the player");
            Assert.That(nearest, Is.GreaterThan(Lights.FearReach - 1.5f), "they waited at the edge of the light");
            Assert.AreEqual(player.Stats.MaxHealth, player.Health, "nothing reached the player in the light");

            // A torch set down beside one: it goes out of its light, away from it.
            var lone = Room(40);
            lone.AddPlayer("c", null).Position = TileMap.Center(35, 35);
            var near = Spawn(lone, "cave_rat", TileMap.Center(15, 20));
            lone.Map[13, 20] = TileType.Torch;
            Assert.IsTrue(lone.FearsLight(near.Position));
            Run(lone, 3);
            Assert.IsFalse(lone.FearsLight(near.Position), "out of the new light");
            Assert.Greater(near.Position.X, 19, "away from the lamp");

            // In the dark the same rats bite.
            var dark = Room(40);
            var other = dark.AddPlayer("b", null);
            other.Position = TileMap.Center(6, 20);
            Spawn(dark, "cave_rat", TileMap.Center(13, 20));
            var events = Run(dark, 8);
            Assert.Greater(events.Count(e => e.Type == SimEventType.Hit && e.Target == other.Id), 0);
        }

        // ---- Spore clouds ----

        [Test]
        public void APuffballBurstsIntoACloudThatFillsTheMonstersInItAndSaysHowMuch()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            world.MonstersHold = true;
            var puff = Spawn(world, "puffball", player.Position + new Vec2(1.2f, 0));
            var beetle = Spawn(world, "cave_beetle", player.Position + new Vec2(1.5f, 1));
            beetle.Health = 10;
            Assert.IsTrue(world.ForceAttack(puff, "burst"));
            var events = Run(world, 1.2f);
            Assert.IsTrue(puff.IsDead, "it went off");
            var cloud = world.Hazards.Single(h => h.Shape == HazardShape.Balm);
            Assert.AreEqual(1.8f, cloud.Size, 1e-4f);
            Assert.AreEqual(World.DeathcloudFill, cloud.Width, 1e-4f);
            Assert.Greater(events.Count(e => e.Type == SimEventType.Hit && e.Target == player.Id), 0, "the blast struck");
            Assert.AreEqual(1, world.Hazards.Count(h => h.Shape == HazardShape.Balm), "one cloud, not a second for its fall");
            float before = beetle.Health;
            var after = Run(world, 2);
            Assert.Greater(beetle.Health, before + 4, "the beetle in the cloud filled");
            Assert.IsTrue(world.InCloud(beetle));
            var told = after.Where(e => e.Type == SimEventType.Healed && e.Source == beetle.Id).ToList();
            Assert.That(told.Count, Is.InRange(3, 5), "told every half second");
            Assert.IsTrue(told.All(e => e.Target == World.CloudHealed && e.Amount >= 1));
            Assert.AreEqual(0, after.Count(e => e.Type == SimEventType.Hit && e.Target == player.Id), "the cloud does nothing to a player");

            // Felled before it goes off, it leaves a smaller cloud of its own.
            var other = Room();
            var hunter = other.AddPlayer("b", null);
            var felled = Spawn(other, "puffball", hunter.Position + new Vec2(4, 0));
            other.Hurt(felled, hunter, 1000);
            Run(other, 0.1f);
            Assert.AreEqual(1.2f, other.Hazards.Single(h => h.Shape == HazardShape.Balm).Size, 1e-4f);
        }

        [Test]
        public void ACloudFillsABigBodyByItsShare()
        {
            var world = Room();
            world.AddPlayer("a", null);
            world.MonstersHold = true;
            var mother = Spawn(world, "slime_mother", TileMap.Center(10, 15));
            var slime = Spawn(world, "slime", TileMap.Center(20, 15));
            mother.Health = mother.MaxHealth * 0.95f;
            slime.Health = 1;
            var puff = Spawn(world, "puffball", mother.Position + new Vec2(1.2f, 1.2f));
            var other = Spawn(world, "puffball", slime.Position + new Vec2(0, 1));
            puff.Health = other.Health = 0.01f;
            world.Hurt(puff, world.Players[0], 100);
            world.Hurt(other, world.Players[0], 100);
            float motherBefore = mother.Health, slimeBefore = slime.Health;
            Run(world, 1);
            float motherShare = (mother.Health - motherBefore) / mother.MaxHealth, slimeShare = (slime.Health - slimeBefore) / slime.MaxHealth;
            Assert.AreEqual(World.CloudXlShare, motherShare / slimeShare, 0.05f);
        }

        [Test]
        public void ACapGiantsLumpAndAGlowingShootersSacLeaveCloudsWhereTheyLand()
        {
            foreach (var (id, attack, radius) in new[] { ("cap_giant", "spore_lump", 1.6f), ("glow_shooter", "glow_sac", 1.0f) })
            {
                var world = Room();
                var player = world.AddPlayer("a", null);
                world.MonstersHold = true;
                var thrower = Spawn(world, id, player.Position + new Vec2(5, 0));
                var at = player.Position;
                Assert.IsTrue(world.ForceAttack(thrower, attack), id);
                Run(world, thrower.Definition.Attacks.First(a => a.Id == attack).Telegraph + 0.1f);
                var cloud = world.Hazards.Single(h => h.Shape == HazardShape.Balm);
                Assert.Less(Vec2.Distance(cloud.Position, at), 0.01f, $"{id}: where the target stood");
                Assert.AreEqual(radius, cloud.Size, 1e-4f, id);
            }
        }

        // ---- The dirt cave's heavies ----

        [Test]
        public void ABigBeetleThatRamsAWallIsDazedLong()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            player.Position = TileMap.Center(27, 15);
            world.MonstersHold = true;
            var beetle = Spawn(world, "big_beetle", TileMap.Center(22, 15));
            Assert.IsTrue(world.ForceAttack(beetle, "ram"));
            Assert.Greater(beetle.Definition.Attacks.First(a => a.Id == "ram").Telegraph, 1f, "a long warning");
            var events = Run(world, 1.2f + 0.8f);
            Assert.AreEqual(EnemyState.Stagger, beetle.State, "into the wall");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxStagger));
            Run(world, World.StaggerSeconds + 0.1f);
            Assert.AreEqual(EnemyState.Stagger, beetle.State, "longer than a beetle's daze");
            Run(world, 2.5f - World.StaggerSeconds);
            Assert.AreNotEqual(EnemyState.Stagger, beetle.State);
        }

        [Test]
        public void ASlimeMotherShedsSlimesAsSheIsStruckAndSplitsIntoBigOnesAtTheEnd()
        {
            var world = Room(40);
            var player = world.AddPlayer("a", null);
            world.MonstersHold = true;
            var mother = Spawn(world, "slime_mother", TileMap.Center(28, 20));
            world.Hurt(mother, player, mother.MaxHealth * 0.25f + 1);
            var events = Run(world, 0.2f);
            var slimes = world.Enemies.Where(e => e.Definition.Id == "slime").ToList();
            Assert.AreEqual(2, slimes.Count, "one for each tenth of her health");
            Assert.IsTrue(slimes.All(s => !s.Small && s.MaxHealth == Shipped.Find("slime").Health), "whole slimes");
            Assert.IsTrue(slimes.All(s => Vec2.Distance(s.Position, mother.Position) < mother.Definition.Radius + 1.5f), "at her edge");
            Assert.AreEqual(2, events.Count(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxSplit));
            world.Hurt(mother, player, 1);
            Run(world, 0.2f);
            Assert.AreEqual(2, world.Enemies.Count(e => e.Definition.Id == "slime"), "no more before the next tenth");
            world.Hurt(mother, player, mother.MaxHealth * 10);
            Run(world, 0.2f);
            Assert.AreEqual(2, world.Enemies.Count(e => e.Definition.Id == "big_slime"), "two big slimes where she fell");
            Assert.LessOrEqual(world.Enemies.Count(e => e.Definition.Id == "slime"), 2 + World.MostShed);
        }

        // ---- The timber ambush ----

        const int Size = 512;

        static WorldHost Host(ulong seed = 5) => new WorldHost(seed, Monsters.Shipped, Gear.Data,
            new HostConfig { MapSize = Size, MapVersion = WorldGenerator.RoomVersion }, new WorldConfig { Director = true }, Bosses.Shipped, Classes.Shipped,
            Tiles.Shipped, story: Tales.Shipped);

        static Player Strong(World world)
        {
            var player = world.AddPlayer("a", Gear.Wearing("obsidian_sword", "obsidian_armor", "obsidian_helmet", "obsidian_boots"));
            player.Experience = Leveling.TotalFor(6);
            player.Health = player.Stats.MaxHealth;
            return player;
        }

        // A player in the middle of a dirt cave way three wide (too wide for the narrow ways' ambush), where the director
        // may act; null when the world has none.
        static bool IntoTimberWay(WorldHost host, Player player)
        {
            var world = host.World;
            foreach (var edge in host.Generated.Rooms.Edges.Where(e => e.Kind == RoomEdgeKind.Way && e.Width > World.AmbushWidth && e.Path != null && e.Path.Count >= 20))
            {
                var (x, y) = edge.Path[edge.Path.Count / 2];
                player.Position = TileMap.Center(x, y);
                if (Collision.Overlaps(world.Map, player.Position, World.PlayerRadius) || world.LeftAlone(player) || world.TimberWayAt(player.Position) == null
                    || world.FearsLight(player.Position)) continue;
                return true;
            }
            return false;
        }

        [Test]
        public void RatsComeOutOfTheTimbersOfADirtCaveWayAfterTheDust()
        {
            var host = Host();
            var world = host.World;
            var player = Strong(world);
            world.MonstersHold = true;
            Assert.IsTrue(IntoTimberWay(host, player), "a dirt cave way three wide");
            Assert.IsFalse(world.Ambush(player), "no narrow way here: the old ambush does not come");
            world.TakeEvents();
            Assert.IsTrue(world.TimberAmbush(player));
            var dust = world.TakeEvents().Where(e => e.Type == SimEventType.Ambush && e.Amount == World.AmbushDust).ToList();
            Assert.AreEqual(1, dust.Count, "dust off the timbers");
            int dx = TileMap.Cell(dust[0].Position.X), dy = TileMap.Cell(dust[0].Position.Y);
            Assert.IsTrue(world.TimberSource(dx, dy), "the dust falls off rock (or timber) by the way");
            Run(world, World.AmbushWarn - 0.2f);
            Assert.AreEqual(0, world.Enemies.Count(e => e.TargetId == player.Id), "nothing yet: the dust warns first");
            Run(world, 0.4f);
            var rats = world.Enemies.Where(e => e.TargetId == player.Id && e.State == EnemyState.Chase).ToList();
            Assert.That(rats.Count, Is.InRange(World.TimberMin, World.TimberMax));
            Assert.IsTrue(rats.All(r => r.Definition.Id == World.TimberKind));
            Assert.IsTrue(rats.All(r => Vec2.Distance(r.Position, dust[0].Position) < 2.5f), "out of the timbers where the dust fell");
            Assert.IsTrue(rats.All(r => Vec2.Distance(r.Position, player.Position) >= 2 && Vec2.Distance(r.Position, player.Position) <= 12), "close, not on the player");
            Assert.AreEqual(1, rats.Select(e => e.PackId).Distinct().Count(), "one pack");
        }

        [Test]
        public void TheTimberSpotIsFloorByRockOutOfTheLight()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            player.Position = TileMap.Center(15, 15);
            var spot = world.TimberSpot(15, 2, player.Position);
            Assert.IsNotNull(spot);
            Assert.IsFalse(world.Map.IsSolid(TileMap.Cell(spot.Value.Spot.X), TileMap.Cell(spot.Value.Spot.Y)));
            Assert.AreEqual(1, TileMap.Cell(spot.Value.Spot.Y), "the floor along the wall");
            Assert.IsNull(world.TimberSpot(15, 15, player.Position), "no rock within two of the middle of the room");
            world.Map[15, 3] = TileType.Torch;
            Assert.IsNull(world.TimberSpot(15, 2, player.Position), "not out of a lit wall: the rats fear it");
        }

        [Test]
        public void TheTimberAmbushKeepsTheAmbushesClockAndWaitsForTheFirstBoss()
        {
            int Timbers(bool firstBossDown)
            {
                var host = Host();
                var world = host.World;
                var player = Strong(world);
                if (!IntoTimberWay(host, player)) Assert.Fail("a dirt cave way three wide");
                if (firstBossDown) world.Restore(Array.Empty<TileChange>(), Array.Empty<(CropDefinition, int, int, long, long)>(), 2);
                world.MonstersHold = true;
                world.SetPace(player, PaceState.Build, 0);
                var pace = world.PaceOf(player);
                pace.NextSurge = world.Tick + World.Ticks(2);
                pace.NextSend = world.Tick + World.Ticks(1000);
                var spot = player.Position;
                Run(world, 4, () => player.Position = spot);
                // On the ambushes' clock: the next surge not before SurgeMin.
                if (pace.Timbers > 0) Assert.Greater(pace.NextSurge, world.Tick + World.Ticks(World.SurgeMin - 5));
                return pace.Timbers;
            }
            Assert.AreEqual(0, Timbers(firstBossDown: false), "the first chapter is as it was");
            Assert.AreEqual(1, Timbers(firstBossDown: true));
        }

        // ---- Big leaders ----

        [Test]
        public void ACampMayBeLedByABigBodyWhereItsRoomHoldsIt()
        {
            int bigLeaders = 0;
            foreach (ulong seed in new ulong[] { 1, 2 })
            {
                var host = new WorldHost(seed, Shipped, Gear.Data, new HostConfig { MapSize = WorldGenerator.GameSize, MapVersion = WorldGenerator.RoomVersion },
                    tiles: Tiles.Shipped, story: Tales.Shipped);
                var world = host.World;
                for (int day = 0; day < 6; day++)
                {
                    foreach (var room in host.Generated.Rooms.Nodes.Where(n => n.Kind == RoomKind.Room && n.Role == RoomRole.Elite && !n.Roadside))
                    {
                        var pack = world.RoomPack(room, 0);
                        if (pack.Definition == null || pack.Roster.Count == 0) continue;
                        var lead = pack.Roster[0];
                        Assert.AreNotEqual(MonsterSize.XL, lead.Size);
                        Assert.IsTrue(pack.Roster.Skip(1).All(k => !k.Big), $"room {room.Index}: no big minion");
                        if (!lead.Big) continue;
                        bigLeaders++;
                        Assert.AreEqual(EliteRank.Leader, pack.Elite, "champions are never big");
                        Assert.GreaterOrEqual(room.Radius, MonsterSizes.LeastRoomRadius);
                        Assert.IsTrue(world.Clearance.Fits(pack.Spots[0], lead.Radius), $"room {room.Index}: it stands where it fits");
                    }
                    world.RestoreClock(Calendar.NextMorning(world.Clock));
                }
            }
            Assert.Greater(bigLeaders, 0, "a big beetle, a big slime or a cap giant led a camp somewhere");
        }

        [Test]
        public void ABigLeaderIsTwiceItsBodyNotFourTimes()
        {
            var world = Room();
            var beetle = world.MakeEliteHere(Shipped.Find("big_beetle"), TileMap.Center(15, 15), EliteRank.Leader, RiftAffix.None);
            Assert.AreEqual(Shipped.Find("big_beetle").Health * World.BigLeaderHealth, beetle.MaxHealth, 1);
            Assert.AreEqual("들이받기", World.BigAttackOf(beetle.Definition).Name);
        }
    }
}
