using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // Packs (2026-11-22, Docs/WorldRework.md 2단계 무리, 묶음 2-1): the sheet, packs in their rooms by biome and zone,
    // noticing together, the leash, the caps, clumps in a world of caves, and the same pack for the same seed, room
    // and day.
    public class PackTests
    {
        const int Size = 512;

        static WorldHost Rooms(ulong seed = 5) => new WorldHost(seed, Monsters.Shipped, Gear.Data,
            new HostConfig { MapSize = Size, MapVersion = WorldGenerator.RoomVersion }, tiles: Tiles.Shipped, story: Tales.Shipped);

        static void Run(World world, float seconds)
        {
            for (int i = 0; i < World.Ticks(seconds); i++) world.Step();
        }

        // The pack room nearest the spawn by rooms, with a player in the room next to it (in its wake reach, out of
        // its monsters' sight).
        static (RoomNode Room, Player Player) BesidePackRoom(WorldHost host, int nth = 0)
        {
            var world = host.World;
            var rooms = host.Generated.Rooms;
            var room = rooms.Nodes.Where(n => n.Kind == RoomKind.Room && n.Role == RoomRole.Pack && !n.Roadside && !world.InSafePlace(n.Center))
                .OrderBy(n => n.Depth).ThenBy(n => n.Index).Skip(nth).First();
            var player = world.AddPlayer("a", Gear.Wearing("obsidian_sword", "obsidian_armor"));
            player.Position = OpenNear(world, room.Center, 22, 34);
            return (room, player);
        }

        // Open floor between the two distances from a point, nearest the nearer.
        static Vec2 OpenNear(World world, Vec2 from, float least, float most)
        {
            for (float reach = least; reach <= most; reach += 0.5f)
            {
                for (int k = 0; k < 64; k++)
                {
                    var at = from + Vec2.FromAngle(2 * MathF.PI * k / 64) * reach;
                    if (!Collision.Overlaps(world.Map, at, 0.5f) && !Collision.Overlaps(world.Map, at, 0.5f, monster: true)) return at;
                }
            }
            throw new InvalidOperationException("no open floor there");
        }

        [Test]
        public void TheShippedSheetReadsAndKeepsTheRules()
        {
            var packs = Monsters.Shipped.Packs;
            Assert.IsNotNull(packs);
            Assert.IsTrue(packs.Success, string.Join("\n", packs.Errors));
            // Every biome has a pack of its own, and the plain cave one from the first zone.
            foreach (Biome biome in Enum.GetValues(typeof(Biome)))
                if (biome != Biome.Cave) Assert.IsTrue(packs.Packs.Any(p => p.Biome == biome), $"{biome} has its own pack");
            Assert.IsTrue(packs.Packs.Any(p => p.Biome == null && p.Place == null && p.Zone == 1));
            Assert.IsTrue(packs.Packs.Any(p => p.Place == "demon_nest"), "the nest's soldiers keep its rooms");
            foreach (var pack in packs.Packs)
            {
                // 4 to 8 alone (Docs/WorldRework.md 목표): a swarm of three to six, the rest a few of the other roles.
                Assert.That(pack.MinCount, Is.GreaterThanOrEqualTo(4), pack.Id);
                Assert.That(pack.MaxCount, Is.LessThanOrEqualTo(8), pack.Id);
                Assert.IsTrue(pack.Members.All(m => m.Monster.Pack != "solo"), pack.Id);
                int swarm = pack.Members.Where(m => m.Monster.Role == MonsterRole.Swarm || m.Monster.Pack == "always").Sum(m => m.Min);
                Assert.That(swarm, Is.GreaterThanOrEqualTo(3), $"{pack.Id}: a swarm of three at least");
            }
        }

        [Test]
        public void ABadRowSaysWhatIsWrong()
        {
            var monsters = MonsterSheet.Parse(File("monsters.csv"), File("monster_attacks.csv"));
            var data = PackSheet.Parse("id,biome,zone,weight,members,size,place\n"
                + "a,,1,100,nobody:3,,\n"
                + "b,,1,100,abyss_stalker:1|slime:3,,\n"
                + "c,,1,100,slime:5-3,,\n"
                + "d,,1,100,slime:6|cave_beetle:5,,\n"
                + "e,,1,100,slime:4,huge,\n"
                + "f,,0,100,slime:4,,\n"
                + "g,,1,100,slime:4,,\n", monsters);
            string all = string.Join("\n", data.Errors);
            Assert.AreEqual(6, data.Errors.Count, all);
            Assert.IsTrue(data.Errors.Any(e => e.Contains("2행") && e.Contains("nobody")), all);
            Assert.IsTrue(data.Errors.Any(e => e.Contains("3행") && e.Contains("solo")), all);
            Assert.IsTrue(data.Errors.Any(e => e.Contains("4행")), all);
            Assert.IsTrue(data.Errors.Any(e => e.Contains("5행") && e.Contains(PackSheet.MaxMembers.ToString())), all);
            Assert.IsTrue(data.Errors.Any(e => e.Contains("6행") && e.Contains("size")), all);
            Assert.IsTrue(data.Errors.Any(e => e.Contains("7행") && e.Contains("zone")), all);
            Assert.AreSame(data, monsters.Packs);
        }

        static string File(string name) => System.IO.File.ReadAllText(RepoFile.Find("Assets/_Project/Data/Monsters/" + name));

        [Test]
        public void PacksComeInTheirRoomsOfTheirBiomeAndZone()
        {
            var host = Rooms();
            var world = host.World;
            var (room, player) = BesidePackRoom(host);
            Assert.IsEmpty(world.Enemies, "no monster before anyone is near");
            Run(world, 1);

            var pack = world.Packs.Single(p => p.Room == room && p.Slot == 0);
            Assert.IsTrue(pack.Active);
            Assert.IsNotNull(pack.Definition);
            Assert.IsTrue(pack.Definition.Biome == null || pack.Definition.Biome == room.Biome, $"{pack.Definition.Id} in {room.Biome}");
            Assert.That(pack.Definition.Zone, Is.LessThanOrEqualTo(world.ZoneLevelAt(room.Center)));
            var members = world.MembersOf(pack).ToList();
            Assert.That(members.Count, Is.InRange(pack.Definition.MinCount, pack.Definition.MaxCount));
            foreach (var member in members)
            {
                Assert.IsTrue(pack.Definition.Members.Any(m => m.Monster == member.Definition), member.Definition.Id);
                Assert.That(Vec2.Distance(member.Position, room.Center), Is.LessThanOrEqualTo(room.Radius + 1.5f), "in its room");
                Assert.AreEqual(EnemyState.Idle, member.State, "nobody seen yet");
                Assert.That(members.Where(m => m != member).Min(m => Vec2.Distance(m.Position, member.Position)), Is.GreaterThanOrEqualTo(0.9f), "spread out");
            }

            // Nothing comes into being on a road or in a camp, nor within RoadClearance of one.
            foreach (var enemy in world.Enemies)
            {
                int x = TileMap.Cell(enemy.Position.X), y = TileMap.Cell(enemy.Position.Y);
                for (int dy = -5; dy <= 5; dy++)
                    for (int dx = -5; dx <= 5; dx++)
                        if (dx * dx + dy * dy <= 25 && world.Map.InBounds(x + dx, y + dy))
                            Assert.IsFalse(Structures.IsWay(world.Map[x + dx, y + dy]), $"{enemy.Definition.Id} at {x},{y} by a road");
                var rest = host.Generated.Rooms.Nodes.Where(n => n.Role == RoomRole.Rest);
                Assert.IsFalse(rest.Any(r => Vec2.Distance(enemy.Position, r.Center) <= r.Radius + 1), "not in a camp");
            }

            // Everyone gone far: the pack goes, and comes again as it was.
            var kinds = members.Select(m => (m.Definition.Id, m.Position)).OrderBy(k => k.Position.X).ThenBy(k => k.Position.Y).ToList();
            player.Position = OpenNear(world, room.Center, World.PackSleepReach + 5, World.PackSleepReach + 40);
            Run(world, 1);
            Assert.IsFalse(pack.Active);
            Assert.IsFalse(world.Enemies.Any(e => e.PackId == pack.Id));
            player.Position = OpenNear(world, room.Center, 22, 34);
            Run(world, 1);
            Assert.AreEqual(kinds, world.MembersOf(pack).Select(m => (m.Definition.Id, m.Position)).OrderBy(k => k.Position.X).ThenBy(k => k.Position.Y).ToList());
        }

        [Test]
        public void APackNoticesTogether()
        {
            var host = Rooms();
            var world = host.World;
            var (room, player) = BesidePackRoom(host);
            Run(world, 1);
            var pack = world.Packs.Single(p => p.Room == room && p.Slot == 0);
            var members = world.MembersOf(pack).ToList();
            world.TakeEvents();

            // Right by one; the one of them furthest from there is well out of its own sight.
            var first = members[0];
            player.Position = OpenNear(world, first.Position, 1.5f, 4);
            var furthest = members.OrderByDescending(m => Vec2.Distance(m.Position, player.Position)).First();
            long noticed = -1;
            var alerted = new HashSet<int>();
            for (int i = 0; i < World.Ticks(3) && alerted.Count < members.Count; i++)
            {
                world.Step();
                foreach (var e in world.TakeEvents().Where(e => e.Type == SimEventType.Alerted))
                {
                    Assert.AreEqual(player.Id, e.Target);
                    alerted.Add(e.Source);
                    if (noticed < 0) noticed = world.Tick;
                }
            }
            Assert.That(noticed, Is.GreaterThan(0), "someone noticed");
            Assert.AreEqual(members.Count, alerted.Count, "every one of them, each with its mark");
            Assert.IsTrue(pack.Alerted);
            Assert.IsTrue(members.All(m => m.IsDead || m.State != EnemyState.Idle || m.Buried), string.Join(", ", members.Select(m => m.State)));
            Assert.That(world.Tick - noticed, Is.LessThanOrEqualTo(World.Ticks(World.PackAlertSeconds) + 1), "all within the alarm's time");
            Assert.IsTrue(furthest.TargetId == player.Id, "even the furthest");
        }

        [Test]
        public void AStruckMonsterWakesItsPack()
        {
            var host = Rooms();
            var world = host.World;
            var (room, player) = BesidePackRoom(host);
            Run(world, 1);
            var pack = world.Packs.Single(p => p.Room == room && p.Slot == 0);
            var members = world.MembersOf(pack).Where(m => !m.Buried).ToList();
            world.Hurt(members[0], player, 1);
            Run(world, World.PackAlertSeconds + 0.1f);
            Assert.IsTrue(world.MembersOf(pack).All(m => m.TargetId == player.Id), "the whole pack comes for whoever struck one, from out of its sight");
        }

        [Test]
        public void APackMonsterDrawnTooFarGoesHomeHealing()
        {
            var host = Rooms();
            var world = host.World;
            var (room, player) = BesidePackRoom(host);
            Run(world, 1);
            var pack = world.Packs.Single(p => p.Room == room && p.Slot == 0);
            var member = world.MembersOf(pack).First(m => !m.Buried && !m.Definition.Has(MonsterTraitKind.Rooted));

            // Drawn past the leash after the player: it turns back.
            var away = OpenNear(world, pack.Home, World.PackLeash + 1, World.PackLeash + 8);
            member.Position = away;
            member.Health = member.MaxHealth / 2;
            player.Position = OpenNear(world, away, 1.2f, 2);
            member.TargetId = player.Id;
            member.State = EnemyState.Chase;
            world.Step();
            Assert.IsTrue(member.Returning);
            Assert.AreEqual(EnemyState.Idle, member.State);

            float health = member.Health;
            float before = Vec2.Distance(member.Position, member.Home);
            var hits = new List<SimEvent>();
            for (int i = 0; i < World.Ticks(2); i++)
            {
                world.Step();
                hits.AddRange(world.TakeEvents().Where(e => e.Type == SimEventType.Hit && e.Source == member.Id));
                if (Vec2.Distance(player.Position, member.Position) > 2.5f) player.Position = OpenNear(world, member.Position, 1.2f, 2);
            }
            Assert.IsEmpty(hits, "it heeds no one on its way home");
            Assert.AreEqual(EnemyState.Idle, member.State);
            Assert.That(member.Health, Is.EqualTo(Math.Min(member.MaxHealth, health + member.MaxHealth * World.ReturnHealPercent / 100f * 2)).Within(member.MaxHealth * 0.02f),
                "a tenth of its health a second");
            Assert.That(Vec2.Distance(member.Position, member.Home), Is.LessThanOrEqualTo(before + 0.01f), "on its way home, or stuck trying");

            // Never stuck for long, even in sight: home, standing again.
            for (int i = 0; i < World.Ticks(45) && member.Returning; i++) world.Step();
            Assert.IsFalse(member.Returning);
        }

        [Test]
        public void ThePartiesAndTheHostKeepTheirCaps()
        {
            var host = Rooms(7);
            var world = host.World;
            var rooms = host.Generated.Rooms;
            // Four players, each alone in a different part of the map, each among pack rooms.
            var crowded = rooms.Nodes.Where(n => n.Role == RoomRole.Pack && !n.Roadside)
                .OrderByDescending(n => rooms.Nodes.Count(o => o.Role == RoomRole.Pack && Vec2.Distance(o.Center, n.Center) < World.PackWakeReach)).ToList();
            var spots = new List<RoomNode>();
            foreach (var room in crowded)
            {
                if (spots.All(s => Vec2.Distance(s.Center, room.Center) > World.PackSleepReach)) spots.Add(room);
                if (spots.Count == 4) break;
            }
            Assert.AreEqual(4, spots.Count);
            var players = spots.Select((room, i) =>
            {
                var player = world.AddPlayer("p" + i, Gear.Wearing("obsidian_sword", "obsidian_armor"));
                player.Position = OpenNear(world, room.Center, 0, 12);
                return player;
            }).ToList();
            var wanderersSeen = new HashSet<int>();
            for (int second = 0; second < 20; second++)
            {
                Run(world, 1);
                int all = world.Enemies.Count(e => e.Rift == 0);
                // A few wanderers too, never in a pack's room (2026-11-24: with the host's cap full of packs one that
                // falls is not made again, so they are counted while they go about).
                foreach (var one in world.Enemies.Where(e => e.Wanders))
                {
                    wanderersSeen.Add(one.Id);
                    Assert.AreEqual(0, one.PackId);
                }
                Assert.That(all, Is.LessThanOrEqualTo(World.HostCap));
                foreach (var player in players)
                {
                    int near = world.Enemies.Count(e => e.Rift == 0 && players.OrderBy(p => Vec2.Distance(p.Position, e.Position)).First() == player);
                    Assert.That(near, Is.LessThanOrEqualTo(World.PartyCap), $"{player.Name} at second {second}");
                }
            }
            Assert.IsTrue(players.All(p => world.Packs.Any(k => k.Active && Vec2.Distance(k.Home, p.Position) < World.PackWakeReach)), "each has packs about");
            Assert.That(wanderersSeen.Count, Is.GreaterThan(0));
        }

        [Test]
        public void AWorldOfCavesBringsClumps()
        {
            var host = new WorldHost(3, Monsters.Shipped, Gear.Data, new HostConfig { MapSize = Size }, tiles: Tiles.Shipped);
            var world = host.World;
            var player = world.AddPlayer("a", Gear.Wearing("obsidian_sword", "obsidian_armor"));
            Assert.IsNull(world.Rooms);
            Run(world, 12);
            var clumps = world.Packs.Where(p => p.Room == null).ToList();
            Assert.That(clumps.Count, Is.GreaterThanOrEqualTo(2), "a clump every few seconds");
            foreach (var clump in clumps)
            {
                var members = world.MembersOf(clump).ToList();
                Assert.That(members.Count, Is.GreaterThanOrEqualTo(clump.Definition.MinCount - 1), clump.Definition.Id);
                // Together, where one monster would have come.
                Assert.IsTrue(members.All(m => Vec2.Distance(m.Home, clump.Home) <= 4 * World.PackSpacingMax), "together");
                Assert.That(Vec2.Distance(clump.Home, player.Position), Is.InRange(11f, World.SpawnReach + 1));
            }
            Assert.That(world.Enemies.Count(e => e.Rift == 0), Is.LessThanOrEqualTo(new WorldConfig().EnemyTarget), "the caves' cap as it always was");
        }

        [Test]
        public void TheSameSeedRoomAndDayMakeTheSamePackAndAFelledOneWaitsForDawn()
        {
            List<(string, Vec2)> Pack(WorldHost host, RoomNode room) =>
                host.World.MembersOf(host.World.Packs.Single(p => p.Room?.Index == room.Index && p.Slot == 0)).Select(m => (m.Definition.Id, m.Position)).ToList();

            var one = Rooms(9);
            var two = Rooms(9);
            var (room, player) = BesidePackRoom(one, 2);
            BesidePackRoom(two, 2);
            Run(one.World, 1);
            Run(two.World, 1);
            Assert.AreEqual(Pack(one, room), Pack(two, room));

            // All of it felled: the room stays empty, even when left and come back to the same day.
            var world = one.World;
            var pack = world.Packs.Single(p => p.Room == room && p.Slot == 0);
            int size = pack.Size;
            foreach (var member in world.MembersOf(pack).ToList())
            {
                member.Buried = false;
                world.Hurt(member, player, 1e6f);
            }
            Run(world, 1);
            Assert.IsTrue(pack.Cleared);
            player.Position = OpenNear(world, room.Center, World.PackSleepReach + 5, World.PackSleepReach + 40);
            Run(world, 1);
            player.Position = OpenNear(world, room.Center, 22, 34);
            Run(world, 1);
            Assert.IsEmpty(world.MembersOf(pack), "nothing again the same day");

            // The next dawn it is full again: another day's pack.
            world.RestoreClock(Calendar.NextMorning(world.Clock));
            player.Position = OpenNear(world, room.Center, World.PackSleepReach + 5, World.PackSleepReach + 40);
            Run(world, 1);
            player.Position = OpenNear(world, room.Center, 22, 34);
            Run(world, 1);
            Assert.That(world.MembersOf(pack).Count(), Is.GreaterThanOrEqualTo(pack.Definition.MinCount));
            Assert.IsFalse(pack.Cleared);
            Assert.That(size, Is.GreaterThan(0));
        }

        [Test]
        public void ASmallRoomOfAMazeHoldsItsWholePack()
        {
            // 2026-11-22: the web sweep found a mushroom forest room holding a pack of one. Its rooms are three or four
            // tiles across and most of that is their ways in; the rest of the pack stands in the maze round the room.
            var host = new WorldHost(1, Monsters.Shipped, Gear.Data, new HostConfig { MapSize = WorldGenerator.GameSize, MapVersion = WorldGenerator.RoomVersion },
                tiles: Tiles.Shipped, story: Tales.Shipped);
            var world = host.World;
            int mushroom = 0;
            foreach (var room in host.Generated.Rooms.Nodes.Where(n => n.Kind == RoomKind.Room && n.Role == RoomRole.Pack && !n.Roadside))
            {
                var pack = world.RoomPack(room, 0);
                if (pack.Definition == null) continue;
                if (room.Biome == Biome.Mushroom) mushroom++;
                Assert.That(pack.Size, Is.GreaterThanOrEqualTo(pack.Definition.MinCount), $"room {room.Index} ({room.Biome}, radius {room.Radius}): {pack.Definition.Id}");
                Assert.IsTrue(pack.Spots.All(s => World.InPackRoom(room, s)), $"room {room.Index}: in or round its room");
            }
            Assert.Greater(mushroom, 10, "the mushroom forest's small rooms were among them");
        }
    }
}
