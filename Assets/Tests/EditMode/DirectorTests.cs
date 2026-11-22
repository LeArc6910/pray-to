using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The pacing director (2026-11-22, Docs/WorldRework.md 완급 감독, 묶음 4b): the tension and the four states, the
    // wanderers sent while building up, the ambush in a narrow way and the raid in a wide place, the quiet rest, the
    // places it leaves alone, and the same world for the same seed.
    public class DirectorTests
    {
        const int Size = 512;

        static WorldHost Host(ulong seed = 5, bool director = true) => new WorldHost(seed, Monsters.Shipped, Gear.Data,
            new HostConfig { MapSize = Size, MapVersion = WorldGenerator.RoomVersion }, new WorldConfig { Director = director }, Bosses.Shipped, Classes.Shipped,
            Tiles.Shipped, story: Tales.Shipped);

        static void Run(World world, float seconds)
        {
            for (int i = 0; i < World.Ticks(seconds); i++) world.Step();
        }

        static Player Strong(World world, int level = 6)
        {
            var player = world.AddPlayer("a", Gear.Wearing("obsidian_sword", "obsidian_armor", "obsidian_helmet", "obsidian_boots"));
            player.Experience = Leveling.TotalFor(level);
            player.Health = player.Stats.MaxHealth;
            return player;
        }

        // Open floor in a room no pack, camp, place or arena is near: where the director may act.
        static Vec2 Open(WorldHost host, Func<RoomNode, bool> also = null)
        {
            var world = host.World;
            var probe = world.AddPlayer("probe", Gear.Wearing("iron_sword"));
            probe.Experience = Leveling.TotalFor(6);
            try
            {
                foreach (var room in host.Generated.Rooms.Nodes.Where(n => n.Kind == RoomKind.Room && (n.Role == RoomRole.Plain || n.Role == RoomRole.Ore) && !n.Roadside)
                    .Where(n => also == null || also(n)).OrderBy(n => n.Depth).ThenBy(n => n.Index))
                {
                    var at = TileMap.Center(room.AnchorX, room.AnchorY);
                    if (Collision.Overlaps(world.Map, at, World.PlayerRadius)) continue;
                    probe.Position = at;
                    if (world.LeftAlone(probe)) continue;
                    // Out of the packs' sight in their rooms.
                    if (host.Generated.Rooms.Nodes.Any(n => (n.Role == RoomRole.Pack || n.Role == RoomRole.Elite) && Vec2.Distance(n.Center, at) < n.Radius + 16)) continue;
                    return at;
                }
            }
            finally
            {
                world.RemovePlayer(probe.Id);
            }
            throw new InvalidOperationException("no room the director acts in");
        }

        [Test]
        public void TheTensionRisesWithTheFightingAndGoesRoundTheFourStates()
        {
            var host = Host();
            var world = host.World;
            var player = Strong(world);
            player.Position = Open(host);
            world.MonstersHold = true;
            var pace = world.PaceOf(player);
            Run(world, 1);
            Assert.AreEqual(PaceState.Build, pace.State);
            Assert.AreEqual(0, pace.Tension, 1e-3);

            // A hurt of a third of the most health: 33% × 1.5 = 50. Another to 70 and over: the peak.
            world.Hurt(player, player.Stats.MaxHealth / 3f);
            Assert.AreEqual(50, pace.Tension, 1.5);
            Run(world, 0.5f);
            Assert.AreEqual(PaceState.Build, pace.State);
            world.Hurt(player, player.Stats.MaxHealth * 0.2f);
            player.Health = player.Stats.MaxHealth;
            Run(world, 0.5f);
            Assert.AreEqual(PaceState.Peak, pace.State);
            Assert.AreEqual(1, pace.Peaks);

            // Held four seconds, then down; it falls only after five seconds with no fighting, four a second.
            Run(world, World.PeakSeconds + 0.3f);
            Assert.AreEqual(PaceState.Relax, pace.State);
            float before = pace.Tension;
            Run(world, 1);
            Assert.That(pace.Tension, Is.LessThan(before - 3).And.GreaterThan(before - 5), "falling four a second once calm");
            Run(world, (pace.Tension - World.RelaxTension) / World.CalmFall + 1);
            Assert.AreEqual(PaceState.Rest, pace.State);
            Assert.AreEqual(1, pace.Rests);
            long rest = pace.RestUntil - world.Tick;
            Assert.That(rest, Is.InRange(World.Ticks(World.RestMin) - 30, World.Ticks(World.RestMax)));
            Run(world, rest / (float)World.TicksPerSecond + 0.5f);
            Assert.AreEqual(PaceState.Build, pace.State, "building up again after the rest");
        }

        [Test]
        public void MonstersFallingNearAndAnEliteAddToIt()
        {
            var host = Host();
            var world = host.World;
            var player = Strong(world);
            player.Position = Open(host);
            world.MonstersHold = true;
            var pace = world.PaceOf(player);
            Run(world, 0.5f);
            var slime = Monsters.Shipped.Find("slime");
            for (int i = 0; i < 4; i++) world.SpawnEnemy(slime, player.Position + new Vec2(2, 0)).Health = 0;
            world.SpawnEnemy(slime, player.Position + new Vec2(World.FallReach + 3, 0)).Health = 0;
            Run(world, 0.2f);
            Assert.AreEqual(4 * World.FallTension, pace.Tension, 0.5, "four felled near; the one further off does not count");
            var elite = world.MakeEliteHere(Monsters.Shipped.Find("cave_beetle"), player.Position + new Vec2(3, 0), EliteRank.Champion, RiftAffix.Fierce);
            foreach (var champion in world.Enemies.Where(e => e.Elite != EliteRank.None))
            {
                champion.TargetId = player.Id;
                champion.State = EnemyState.Chase;
            }
            Run(world, 0.5f);
            Assert.AreEqual(4 * World.FallTension + 3 * World.EliteTension, pace.Tension, 1, "each champion after the player once");
            Run(world, 1);
            Assert.AreEqual(4 * World.FallTension + 3 * World.EliteTension, pace.Tension, 1, "once each");
        }

        [Test]
        public void WhileBuildingUpAWandererIsSentFromOutOfSight()
        {
            var host = Host();
            var world = host.World;
            var player = Strong(world);
            player.Position = Open(host);
            world.SetPace(player, PaceState.Build, 0);
            Run(world, World.SendMax + 1);
            var pace = world.PaceOf(player);
            Assert.That(pace.Sends, Is.GreaterThanOrEqualTo(1), "one in twenty to thirty seconds");
            Assert.Greater(world.Hunters, 0);
        }

        [Test]
        public void SentMonstersComeFromOutOfSightAndWalkToThePlayer()
        {
            var host = Host();
            var world = host.World;
            var player = Strong(world);
            player.Position = Open(host);
            world.SetPace(player, PaceState.Build, 0);
            var pace = world.PaceOf(player);
            Run(world, 0.5f);
            var before = world.Enemies.Select(e => e.Id).ToHashSet();
            pace.NextSend = world.Tick + 1;
            Run(world, 0.5f);
            var sent = world.Enemies.Where(e => !before.Contains(e.Id) && e.Wanders).ToList();
            Assert.IsNotEmpty(sent, "sent at once");
            foreach (var enemy in sent)
                Assert.That(Vec2.Distance(enemy.Position, player.Position), Is.InRange(World.SurgeNear - 1, World.SurgeFar + 2), "out of sight");
            float start = sent.Min(e => Vec2.Distance(e.Position, player.Position));
            world.MonstersHold = false;
            Run(world, 4);
            var still = sent.Where(e => !e.IsDead && world.FindEnemy(e.Id) != null).ToList();
            Assert.IsNotEmpty(still);
            Assert.Less(still.Min(e => Vec2.Distance(e.Position, player.Position)), start - 5, "coming to the player");
        }

        [Test]
        public void AnAmbushComesOutOfTheWallsOfANarrowWayAfterTheDust()
        {
            var host = Host(5);
            var world = host.World;
            var rooms = host.Generated.Rooms;
            var player = Strong(world);
            world.MonstersHold = true;
            bool sprung = false;
            foreach (var edge in rooms.Edges.Where(e => e.Kind == RoomEdgeKind.Way && e.Width <= World.AmbushWidth && e.Path != null && e.Path.Count >= 24))
            {
                var (x, y) = edge.Path[edge.Path.Count / 2];
                player.Position = TileMap.Center(x, y);
                if (Collision.Overlaps(world.Map, player.Position, World.PlayerRadius) || world.LeftAlone(player)) continue;
                world.TakeEvents();
                if (!world.Ambush(player)) continue;
                sprung = true;
                break;
            }
            Assert.IsTrue(sprung, "a narrow way with room before and behind");
            var dust = world.TakeEvents().Where(e => e.Type == SimEventType.Ambush && e.Amount == World.AmbushDust).ToList();
            Assert.AreEqual(2, dust.Count, "dust off a wall before and one behind");
            Run(world, World.AmbushWarn - 0.2f);
            Assert.AreEqual(0, world.Enemies.Count(e => e.TargetId == player.Id), "nothing yet: the dust warns first");
            Run(world, 0.4f);
            var come = world.Enemies.Where(e => e.TargetId == player.Id && e.State == EnemyState.Chase).ToList();
            Assert.That(come.Count, Is.InRange(World.AmbushMin, World.AmbushMax));
            Assert.IsTrue(come.All(e => Vec2.Distance(e.Position, player.Position) <= 10), "out of the walls close by");
            Assert.IsTrue(come.Any(e => Vec2.Dot(e.Position - player.Position, come[0].Position - player.Position) < 0), "before and behind");
            Assert.AreEqual(1, come.Select(e => e.PackId).Distinct().Count(), "one pack: they notice together");
        }

        [Test]
        public void ARaidComesFromFarOffMostlyFromBehind()
        {
            var host = Host(5);
            var world = host.World;
            var player = Strong(world);
            player.Position = Open(host, r => r.Radius >= 7);
            world.MonstersHold = true;
            Run(world, 0.5f);
            world.TakeEvents();
            int before = world.Enemies.Count;
            Assert.IsTrue(world.Raid(player));
            var howl = world.TakeEvents().Single(e => e.Type == SimEventType.Ambush && e.Amount == World.RaidHowl);
            Assert.That(Vec2.Distance(howl.Position, player.Position), Is.InRange(World.SurgeNear, World.SurgeFar + 1), "heard from far off");
            var raiders = world.Enemies.Skip(before).ToList();
            Assert.That(raiders.Count, Is.InRange(World.RaidMin, World.RaidMax));
            Assert.IsTrue(raiders.All(e => Vec2.Distance(e.Position, player.Position) >= World.SurgeNear - 4.5f), "out of sight");
            Assert.AreEqual(1, raiders.Select(e => e.PackId).Distinct().Count());
            float far = raiders.Average(e => Vec2.Distance(e.Position, player.Position));
            world.MonstersHold = false;
            Run(world, 3);
            Assert.Less(raiders.Where(e => world.FindEnemy(e.Id) != null).Average(e => Vec2.Distance(e.Position, player.Position)), far - 6, "running in");
        }

        [Test]
        public void TheRestIsQuiet()
        {
            var host = Host();
            var world = host.World;
            var player = Strong(world);
            player.Position = Open(host);
            world.SetPace(player, PaceState.Rest, 10);
            var pace = world.PaceOf(player);
            // A wanderer beside the player does not heed them while they rest.
            var wanderer = world.SpawnEnemy(Monsters.Shipped.Find("slime"), player.Position + new Vec2(3, 0));
            wanderer.Wanders = true;
            int sends = pace.Sends;
            Run(world, 30);
            Assert.AreEqual(PaceState.Rest, pace.State);
            Assert.AreNotEqual(player.Id, wanderer.TargetId, "a wanderer heeds no one at rest");
            Assert.AreEqual(sends, pace.Sends);
            Assert.AreEqual(0, world.Hunters, "nothing sent");
            Assert.AreEqual(player.Stats.MaxHealth, player.Health, 1e-3, "not hit");
            Assert.IsFalse(world.Enemies.Any(e => e.Wanders && e != wanderer && Vec2.Distance(e.Position, player.Position) < World.SpawnReach), "no wanderer comes round");
        }

        [Test]
        public void NothingIsSentByAPlaceACampOrToANewCharacter()
        {
            var host = Host();
            var world = host.World;
            var player = Strong(world, level: 2);
            player.Position = Open(host);
            Assert.IsTrue(world.LeftAlone(player), "under World.DirectorLevel: the first minutes");
            player.Experience = Leveling.TotalFor(6);
            Assert.IsFalse(world.LeftAlone(player));
            var camp = world.Pois.First(p => p.Kind == PoiKind.Camp);
            player.Position = camp.Center + new Vec2(World.CampSafeReach + 2, 0);
            Assert.IsTrue(world.LeftAlone(player), "round a camp");
            var boss = world.Bosses.First();
            player.Position = boss.Home + new Vec2(boss.ArenaRadius + 3, 0);
            Assert.IsTrue(world.LeftAlone(player), "by a boss's arena");
            player.Position = world.Spawn;
            Assert.IsTrue(world.LeftAlone(player), "in the first place");

            // Standing a long while in the place: nothing sent, no ambush, no raid.
            world.SetPace(player, PaceState.Build, 0);
            Run(world, 40);
            var pace = world.PaceOf(player);
            Assert.AreEqual(0, pace.Sends + pace.Ambushes + pace.Raids);
        }

        [Test]
        public void WithoutTheHostAskingItOnlyMeasures()
        {
            var host = Host(director: false);
            var world = host.World;
            var player = Strong(world);
            player.Position = Open(host);
            world.SetPace(player, PaceState.Build, 0);
            Run(world, 40);
            var pace = world.PaceOf(player);
            Assert.AreEqual(0, pace.Sends + pace.Ambushes + pace.Raids);
            Assert.AreEqual(0, world.Hunters);
            world.Hurt(player, player.Stats.MaxHealth / 2f);
            Assert.Greater(pace.Tension, 70, "measured all the same");
        }

        [Test]
        public void ASmallWorldHasNoDirector()
        {
            var host = new WorldHost(5, Monsters.Shipped, Gear.Data, worldConfig: new WorldConfig { Director = true });
            var world = host.World;
            var player = Strong(world);
            Assert.IsFalse(world.Directs);
            world.Hurt(player, player.Stats.MaxHealth / 2f);
            Assert.AreEqual(0, world.PaceOf(player).Tension, 1e-3);
        }

        [Test]
        public void TheSameSeedPlaysTheSame()
        {
            string Play()
            {
                var host = Host(7);
                var world = host.World;
                var player = Strong(world);
                player.Position = Open(host);
                world.SetPace(player, PaceState.Build, 0);
                var pace = world.PaceOf(player);
                pace.NextSurge = world.Tick + World.Ticks(10);
                Run(world, 70);
                return $"{pace.Sends} {pace.Ambushes} {pace.Raids} {pace.State} {pace.Tension:0.00} {player.Health:0.0} "
                    + string.Join(";", world.Enemies.Select(e => $"{e.Definition.Id}@{e.Position.X:0.00},{e.Position.Y:0.00}"));
            }
            string first = Play();
            Assert.AreEqual(first, Play());
            StringAssert.DoesNotStartWith("0 0 0", first, "the director did something in seventy seconds");
        }
    }
}
