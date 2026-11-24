using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The size classes of the monsters (2026-11-24, Docs/BiomeRework.md 4-2 and B0b): the sheet's size column and its
    // bands, the room a body has at each tile (Clearance), big bodies walking only where they fit and keeping to their
    // rooms, coming only in rooms that hold them, struck at their edge and thrown by their class's share.
    public class MonsterSizeTests
    {
        static readonly MonsterData Shipped = Monsters.Shipped;

        static World WorldOf(TileMap map, Vec2 spawn) => new World(map, spawn, Shipped, 1, new WorldConfig { EnemyTarget = 0 });

        static void Run(World world, float seconds, Action each = null)
        {
            for (int i = 0; i < World.Ticks(seconds); i++)
            {
                each?.Invoke();
                world.Step();
                world.TakeEvents();
            }
        }

        const string Header = "id,name,level,health,damage,armor,speed,radius,aggro,reach,windup,cooldown,weight,traits,size,base\n";

        // ---- The sheet ----

        [Test]
        public void EveryShippedKindHasTheSizeItsRadiusSays()
        {
            Assert.IsTrue(Shipped.Success, string.Join("\n", Shipped.Errors));
            foreach (var monster in Shipped.Monsters)
            {
                Assert.IsTrue(MonsterSizes.Fits(monster.Size, monster.Radius), monster.Id);
                Assert.AreEqual(MonsterSizes.FromRadius(monster.Radius), monster.Size, monster.Id);
            }
            Assert.AreEqual(MonsterSize.M, Shipped.Find("slime").Size);
            Assert.AreEqual(MonsterSize.S, Shipped.Find("glow_bat").Size);
            Assert.AreEqual(MonsterSize.L, Shipped.Find("big_slime").Size);
            Assert.AreEqual(MonsterSize.XL, Shipped.Find("slime_king").Size);
            Assert.IsTrue(Shipped.Find("big_slime").Big);
            Assert.IsFalse(Shipped.Find("cave_beetle").Big);
            // Three classes or more among the kinds that come (Docs/BiomeRework.md 4-6).
            Assert.GreaterOrEqual(Shipped.Monsters.Select(m => m.Size).Distinct().Count(), 3);
        }

        [Test]
        public void ABlankSizeComesFromTheRadiusAndAGapIsAnError()
        {
            var data = MonsterSheet.Parse(Header
                + "a,가,1,10,1,0,3,0.22,5,0.5,0.5,1,1,,,\n"
                + "b,나,1,10,1,0,2,0.33,5,0.5,0.5,1,1,,,\n"
                + "c,다,1,10,1,0,2,0.42,5,0.5,0.5,1,1,,,\n"
                + "d,라,1,40,1,0,1.5,0.8,5,0.5,0.8,1,1,,,\n"
                + "e,마,1,120,1,0,1,1.3,5,0.5,1,1,1,,,\n");
            Assert.IsTrue(data.Success, string.Join("\n", data.Errors));
            CollectionAssert.AreEqual(new[] { MonsterSize.XS, MonsterSize.S, MonsterSize.M, MonsterSize.L, MonsterSize.XL }, data.Monsters.Select(m => m.Size));

            Assert.IsFalse(MonsterSheet.Parse(Header + "g,가,1,10,1,0,1,0.6,5,0.5,0.5,1,1,,,\n").Success, "0.6 is between M and L");
            var wrong = MonsterSheet.Parse(Header + "w,가,1,10,1,0,1,0.4,5,0.5,0.5,1,1,,l,\n");
            Assert.IsFalse(wrong.Success);
            StringAssert.Contains("l 등급의 radius는 0.7~0.9", wrong.Errors.Single());
            Assert.IsFalse(MonsterSheet.Parse(Header + "w,가,1,10,1,0,1,0.4,5,0.5,0.5,1,1,,huge,\n").Success, "no such class");
        }

        [Test]
        public void ABigBodyIsSlowAndWarnsLongAndAVariantIsHeldToItsBase()
        {
            // An L walks no faster than 2.2 and winds up no quicker than 0.7 seconds; an XL 1.6 and 0.9.
            var fast = MonsterSheet.Parse(Header + "f,가,1,40,1,0,2.5,0.8,5,0.5,0.8,1,1,,,\n");
            StringAssert.Contains("speed 2.2 이하", fast.Errors.Single());
            var quick = MonsterSheet.Parse(Header + "q,가,1,120,1,0,1,1.3,5,0.5,0.6,1,1,,,\n");
            StringAssert.Contains("windup 0.9초 이상", quick.Errors.Single());
            // A big slime of a slime (18 health, 2.4 a second): ×4 health and ×0.7 pace within the slack.
            string slime = "slime,슬라임,1,18,6,0,2.4,0.4,8,0.5,0.5,1,1,,,\n";
            Assert.IsTrue(MonsterSheet.Parse(Header + slime + "big,큰 슬라임,2,80,12,5,1.7,0.75,8,0.6,0.8,1.6,0,,l,slime\n").Success);
            var frail = MonsterSheet.Parse(Header + slime + "big,큰 슬라임,2,20,12,5,1.7,0.75,8,0.6,0.8,1.6,0,,l,slime\n");
            StringAssert.Contains("health는 36~144", frail.Errors.Single());
            var brisk = MonsterSheet.Parse(Header + slime + "big,큰 슬라임,2,80,12,5,2.2,0.75,8,0.6,0.8,1.6,0,,l,slime\n");
            StringAssert.Contains("speed는", brisk.Errors.Single());
            Assert.IsFalse(MonsterSheet.Parse(Header + slime + "big,큰 슬라임,2,80,12,5,1.7,0.75,8,0.6,0.8,1.6,0,,l,nobody\n").Success, "no such base");
            // A split into another kind names a smaller one.
            Assert.IsFalse(MonsterSheet.Parse(Header + slime + "big,큰 슬라임,2,80,12,5,1.7,0.75,8,0.6,0.8,1.6,0,split:2:big,l,\n").Success);
            Assert.IsTrue(MonsterSheet.Parse(Header + slime + "big,큰 슬라임,2,80,12,5,1.7,0.75,8,0.6,0.8,1.6,0,split:2:slime,l,\n").Success);
            // Every attack of a big body warns as long as its class asks.
            var attacks = "monster,id,name,shape,target,telegraph,damage,radius,length,angle,width,count,spread,speed,cooldown,weight,range_min,range_max\n"
                + "big,slam,누르기,slam,nearest,0.5,100,1.1,,,,,,,2,100,0,1\n";
            var hasty = MonsterSheet.Parse(Header + "big,큰 슬라임,2,80,12,5,1.7,0.75,8,0.6,0.8,1.6,0,,l,\n", attacks);
            StringAssert.Contains("공격 예고는 0.7초 이상", hasty.Errors.Single());
        }

        [Test]
        public void APackHoldsNoXlAndAtMostTwoL()
        {
            var monsters = MonsterSheet.Parse(Header
                + "slime,슬라임,1,18,6,0,2.4,0.4,8,0.5,0.5,1,1,,,\n"
                + "big,큰 슬라임,2,80,12,5,1.7,0.75,8,0.6,0.8,1.6,0,,l,\n"
                + "king,왕,3,400,12,5,1.2,1.3,8,0.6,1,1.6,0,,xl,\n");
            Assert.IsTrue(monsters.Success, string.Join("\n", monsters.Errors));
            const string head = "id,biome,zone,weight,members,size,place\n";
            Assert.IsTrue(PackSheet.Parse(head + "a,,1,1,slime:3-4|big:1-2,,\n", monsters).Success);
            StringAssert.Contains("XL이라 무리에 넣지 않습니다", PackSheet.Parse(head + "a,,1,1,slime:3|king:1,,\n", monsters).Errors.Single());
            StringAssert.Contains("2마리까지", PackSheet.Parse(head + "a,,1,1,slime:3|big:3,,\n", monsters).Errors.Single());
            StringAssert.Contains("첫 종", PackSheet.Parse(head + "a,,1,1,big:1|slime:3,,\n", monsters).Errors.Single());
        }

        // ---- Room to walk ----

        // Three ways out of a room to the east, one, two and three tiles wide (rows read top down).
        static TileMap Ways() => Maps.FromRows(
            "##################################",
            "#.........########################",
            "#.........########################",
            "#.........########################",
            "#................................#",
            "#.........########################",
            "#.........########################",
            "#................................#",
            "#................................#",
            "#.........########################",
            "#.........########################",
            "#................................#",
            "#................................#",
            "#................................#",
            "#.........########################",
            "##################################");

        [Test]
        public void TheRoomAtATileIsHalfTheWaysWidth()
        {
            var map = Ways();
            var room = new Clearance(map);
            // Row 4 from the top (y = 11) is one tile wide, rows 7-8 (y = 7, 8) two, rows 11-13 (y = 2..4) three.
            Assert.AreEqual(0.5f, room.At(20, 11), 0.05f, "one tile: half a tile");
            Assert.AreEqual(1.0f, room.At(20, 7), 0.05f, "two tiles: its middle runs along the corners");
            Assert.AreEqual(1.5f, room.At(20, 3), 0.05f, "three tiles");
            Assert.AreEqual(1.0f, room.At(20, 2), 0.05f, "the side tile of three: its corner on the middle line");
            Assert.AreEqual(0, room.At(20, 9), "rock");
            Assert.AreEqual(Clearance.Cap, room.At(5, 8), 0.05f, "the room's middle");

            var slime = Shipped.Find("slime");
            var big = Shipped.Find("big_slime");
            var king = Shipped.Find("slime_king");
            Assert.IsTrue(room.Fits(20, 11, slime.Radius), "an M body passes a one-tile gap");
            Assert.IsFalse(room.Fits(20, 11, big.Radius), "an L body does not");
            Assert.IsTrue(room.Fits(20, 7, big.Radius), "an L body passes two tiles (its diameter 1.5 and 0.4)");
            Assert.IsFalse(room.Fits(20, 7, king.Radius), "an XL body does not");
            Assert.IsTrue(room.Fits(20, 3, king.Radius), "an XL body passes three tiles");

            // Rock dug out widens the way at once (TileMap.Version).
            for (int x = 15; x < 26; x++) map[x, 10] = TileType.Floor;
            Assert.IsTrue(room.Fits(20, 11, big.Radius), "dug wider, it fits");
            map[20, 7] = TileType.Wall;
            Assert.AreEqual(0, room.At(20, 7));
        }

        // A big body after a player down a corridor too narrow for it waits at its mouth; an ordinary one walks on.
        [TestCase("big_slime", 11, false)]
        [TestCase("slime", 11, true)]
        [TestCase("big_slime", 7, true)]
        [TestCase("slime_king", 7, false)]
        [TestCase("slime_king", 3, true)]
        public void ABigBodyWalksOnlyWhereItFits(string id, int row, bool through)
        {
            var map = Ways();
            var world = WorldOf(map, TileMap.Center(28, row));
            var player = world.AddPlayer("a", null);
            // Within its aggro and half again, so it keeps after them.
            player.Position = TileMap.Center(15, row);
            var kind = Shipped.Find(id);
            var monster = world.SpawnEnemy(kind, new Vec2(6, row + 0.5f));
            monster.MaxHealth = monster.Health = 100000;
            monster.State = EnemyState.Chase;
            monster.TargetId = player.Id;
            float deepest = 0;
            Run(world, 8, () =>
            {
                player.Health = player.Stats.MaxHealth;
                deepest = Math.Max(deepest, monster.Position.X);
                // (A leap's hop goes over the rock between; it lands clear of it.)
                bool hopping = monster.State == EnemyState.Windup && monster.Attack?.Shape == MonsterAttackShape.Leap;
                if (!hopping) Assert.IsFalse(Collision.Overlaps(map, monster.Position, kind.Radius - 0.02f, monster: true), $"{id} is never in rock ({monster.Position})");
            });
            if (through) Assert.Greater(deepest, 11.5f, $"{id} went down the way");
            else Assert.Less(deepest, 10.2f, $"{id} waited at the mouth ({deepest})");
        }

        [Test]
        public void ABigBodyKeepsToItsRoomsLeash()
        {
            var world = WorldOf(Maps.Room(40), TileMap.Center(20, 20));
            var player = world.AddPlayer("a", null);
            var big = world.SpawnFitting(Shipped.Find("big_slime"), TileMap.Center(10, 20), leash: 5);
            Assert.IsNotNull(big);
            big.MaxHealth = big.Health = 100000;
            player.Position = TileMap.Center(21, 20);
            big.State = EnemyState.Chase;
            big.TargetId = player.Id;
            float furthest = 0;
            Run(world, 8, () =>
            {
                player.Health = player.Stats.MaxHealth;
                furthest = Math.Max(furthest, Vec2.Distance(big.Position, big.Home));
            });
            Assert.LessOrEqual(furthest, 5 + 0.75f, "never much past its leash");
            Assert.Greater(furthest, 3, "it came as far as it could");
        }

        // ---- Where they come ----

        [Test]
        public void TheBigSlimesPackComesOnlyInARoomThatHoldsIt()
        {
            var host = new WorldHost(1, Shipped, Gear.Data, new HostConfig { MapSize = WorldGenerator.GameSize, MapVersion = WorldGenerator.RoomVersion },
                tiles: Tiles.Shipped, story: Tales.Shipped);
            var world = host.World;
            var big = Shipped.Find("big_slime");
            int seen = 0, small = 0;
            for (int day = 0; day < 4; day++)
            {
                foreach (var room in host.Generated.Rooms.Nodes.Where(n => n.Kind == RoomKind.Room && (n.Role == RoomRole.Pack || n.Role == RoomRole.Elite) && !n.Roadside))
                {
                    var pack = world.RoomPack(room, 0);
                    if (pack.Definition == null) continue;
                    if (room.Radius < MonsterSizes.LeastRoomRadius) small++;
                    for (int i = 0; i < pack.Roster.Count; i++)
                    {
                        if (!pack.Roster[i].Big) continue;
                        if (pack.Roster[i] == big) seen++;
                        Assert.AreEqual(MonsterSize.L, pack.Roster[i].Size, "the field holds L at most");
                        // A camp may be led by a big body (B1b), never with a big minion or a band of big champions.
                        if (room.Role == RoomRole.Elite) Assert.IsTrue(i == 0 && pack.Elite == EliteRank.Leader, $"room {room.Index}: a big body in a camp leads it");
                        Assert.GreaterOrEqual(room.Radius, MonsterSizes.LeastRoomRadius, $"room {room.Index}");
                        Assert.IsTrue(world.Clearance.Fits(pack.Spots[i], pack.Roster[i].Radius), $"room {room.Index}: it stands where it fits");
                        Assert.LessOrEqual(Vec2.Distance(pack.Spots[i], room.Center), room.Radius + 0.01f, $"room {room.Index}: inside its room");
                        for (int j = 0; j < pack.Spots.Count; j++)
                            if (j != i) Assert.GreaterOrEqual(Vec2.Distance(pack.Spots[j], pack.Spots[i]), pack.Roster[i].Radius + pack.Roster[j].Radius, $"room {room.Index}: apart");
                    }
                }
                world.RestoreClock(Calendar.NextMorning(world.Clock));
            }
            Assert.Greater(seen, 0, "the big slime's pack came somewhere");
            Assert.Greater(small, 0, "small rooms were among them, and none of them held it");
        }

        [Test]
        public void AWakenBigBodyIsTiedToItsRoomAndNeverComesAlone()
        {
            var host = new WorldHost(1, Shipped, Gear.Data, new HostConfig { MapSize = WorldGenerator.GameSize, MapVersion = WorldGenerator.RoomVersion },
                tiles: Tiles.Shipped, story: Tales.Shipped);
            var world = host.World;
            RoomNode room = null;
            for (int day = 0; day < 8 && room == null; day++)
            {
                if (day > 0) world.RestoreClock(Calendar.NextMorning(world.Clock));
                room = host.Generated.Rooms.Nodes.FirstOrDefault(n => n.Kind == RoomKind.Room && n.Role == RoomRole.Pack && !n.Roadside
                    && world.RoomPack(n, 0).Roster.Any(k => k.Big));
            }
            Assert.IsNotNull(room, "a room with the big slime's pack");
            var player = world.AddPlayer("a", null);
            player.Position = room.Center + new Vec2(0, room.Radius + 30);
            Run(world, 1);
            var big = world.MembersOf(world.RoomPack(room, 0)).FirstOrDefault(e => e.Definition.Big);
            Assert.IsNotNull(big, "it came with its pack");
            Assert.AreEqual(room.Center, big.BoundCenter);
            Assert.AreEqual(room.Radius + MonsterSizes.RoomLeash, big.BoundRadius, 1e-4f);
            // The field's kinds one by one never hold a big body.
            foreach (var kind in Shipped.Monsters.Where(m => m.Big)) Assert.AreEqual(0, kind.Weight, kind.Id);
        }

        [Test]
        public void AnXlBodyNeedsSevenBySevenOfOpenFloor()
        {
            var world = WorldOf(Maps.Room(12), TileMap.Center(6, 6));
            Assert.IsTrue(world.OpenSquareIn(TileMap.Center(6, 6), 6), "ten by ten inside the walls");
            var narrow = WorldOf(Maps.FromRows(
                "############",
                "#..........#",
                "#..........#",
                "#....#.....#",
                "#..........#",
                "#..........#",
                "#....#.....#",
                "#..........#",
                "#..........#",
                "############"), TileMap.Center(6, 5));
            Assert.IsFalse(narrow.OpenSquareIn(TileMap.Center(6, 5), 6), "eight rows with pillars: no seven by seven clear");
        }

        // ---- Blows ----

        [Test]
        public void ABigBodyIsStruckAtItsEdge()
        {
            var big = Shipped.Find("big_slime");
            var slime = Shipped.Find("slime");
            var at = new Vec2(0, 0);
            float reach = World.MeleeReach;
            // Its middle as far as an M body's would be out of reach, but its edge is in reach.
            float distance = reach + World.PlayerRadius + slime.Radius + 0.2f;
            Assert.IsFalse(Combat.InArc(at, new Vec2(1, 0), new Vec2(distance, 0), slime.Radius, reach, 90));
            Assert.IsTrue(Combat.InArc(at, new Vec2(1, 0), new Vec2(distance, 0), big.Radius, reach, 90));
            // Its middle outside the swing's arc but its flank inside: struck. An M body there is not.
            var flank = Vec2.FromAngle(55 * MathF.PI / 180) * 1.3f;
            Assert.IsFalse(Combat.InArc(at, new Vec2(1, 0), flank, slime.Radius, reach, 90));
            Assert.IsTrue(Combat.InArc(at, new Vec2(1, 0), flank, big.Radius, reach, 90));

            // In the world: a swing at the big slime two tiles off lands; at a slime there it does not.
            foreach (var (kind, hits) in new[] { (big, true), (slime, false) })
            {
                var world = WorldOf(Maps.Room(20), TileMap.Center(10, 10));
                var player = world.AddPlayer("a", Gear.Wearing("copper_sword"));
                var dummy = world.SpawnEnemy(kind, player.Position + new Vec2(World.PlayerRadius + MonsterSizes.SlimRadius + reach + 0.25f, 0));
                dummy.MaxHealth = dummy.Health = 100000;
                world.MonstersHold = true;
                world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
                world.SetAttacking(player.Id, true);
                var spot = dummy.Position;
                Run(world, 0.3f, () =>
                {
                    dummy.Position = spot;
                    dummy.Knockback = Vec2.Zero;
                });
                Assert.AreEqual(hits, dummy.Health < dummy.MaxHealth, kind.Id);
            }
        }

        [Test]
        public void ABlowThrowsABodyByItsClass()
        {
            var lengths = new Dictionary<MonsterSize, float>();
            foreach (var id in new[] { "glow_bat", "slime", "big_slime", "slime_king" })
            {
                var world = WorldOf(Maps.Room(30), TileMap.Center(15, 15));
                var player = world.AddPlayer("a", null);
                var kind = Shipped.Find(id);
                var monster = world.SpawnEnemy(kind, player.Position + new Vec2(3, 0));
                monster.MaxHealth = monster.Health = 100000;
                world.Hurt(monster, player, 1);
                lengths[kind.Size] = monster.Knockback.Length;
            }
            Assert.AreEqual(lengths[MonsterSize.M] * 1.4f, lengths[MonsterSize.S], 1e-3f);
            Assert.AreEqual(lengths[MonsterSize.M] * 0.3f, lengths[MonsterSize.L], 1e-3f);
            Assert.AreEqual(0, lengths[MonsterSize.XL], "an XL body is not thrown at all");
            Assert.Greater(lengths[MonsterSize.M], 0);
        }

        [Test]
        public void ABigBodysMarksReachFromItsEdgeAndAreWalkedOutOf()
        {
            var big = Shipped.Find("big_slime");
            var slam = big.Attacks.First(a => a.Id == "slam");
            var (shape, reach, _) = MonsterAttackSheet.MarkOf(big, slam);
            Assert.AreEqual(HazardShape.Circle, shape);
            Assert.AreEqual(slam.Radius + big.Radius - MonsterSizes.SlimRadius, reach, 1e-4f, "measured from its edge");
            var bite = Shipped.Find("slime").Attacks.First(a => a.Id == "bite");
            Assert.AreEqual(bite.Length, MonsterAttackSheet.MarkOf(Shipped.Find("slime"), bite).Reach, "an M body's as the sheet says");
            // The world lays it so.
            var world = WorldOf(Maps.Room(20), TileMap.Center(10, 10));
            var player = world.AddPlayer("a", null);
            var monster = world.SpawnEnemy(big, player.Position + new Vec2(1.5f, 0));
            monster.State = EnemyState.Chase;
            monster.TargetId = player.Id;
            Assert.IsTrue(world.ForceAttack(monster, "slam"));
            Assert.AreEqual(reach, world.Hazards.Single(h => h.Source == monster.Id).Size, 1e-4f);
            // And every big kind's marks leave a walking player time to get out of them, its body counted.
            foreach (var kind in Shipped.Monsters.Where(m => m.Big))
                foreach (var attack in kind.Attacks)
                    Assert.GreaterOrEqual(attack.Telegraph + 1e-4f, MonsterAttackSheet.LeastTelegraph(attack, kind), $"{kind.Id}.{attack.Id}");
        }

        [Test]
        public void ABigSlimeSplitsIntoTwoWholeSlimesOfItsPack()
        {
            var world = WorldOf(Maps.Room(30), TileMap.Center(15, 15));
            var player = world.AddPlayer("a", null);
            var big = world.SpawnEnemy(Shipped.Find("big_slime"), player.Position + new Vec2(4, 0));
            world.Gather(new[] { big });
            world.Hurt(big, player, 100000);
            Run(world, 0.2f);
            var halves = world.Enemies.Where(e => !e.IsDead && e.Definition.Id == "slime").ToList();
            Assert.AreEqual(2, halves.Count, "even in the first zone: it is what a big slime is");
            Assert.IsTrue(halves.All(h => !h.Small && h.MaxHealth == Shipped.Find("slime").Health));
            Assert.IsTrue(halves.All(h => h.PackId == big.PackId), "of its pack, for its share of the reward");
            Assert.Greater(Vec2.Distance(halves[0].Position, halves[1].Position), 0.8f, "where its edges were");
        }

        [Test]
        public void SpawnFittingPutsABigBodyWhereItFitsOrNowhere()
        {
            var world = WorldOf(Ways(), TileMap.Center(5, 8));
            var king = Shipped.Find("slime_king");
            var inRoom = world.SpawnFitting(king, TileMap.Center(5, 8));
            Assert.IsNotNull(inRoom);
            Assert.IsTrue(world.Clearance.Fits(inRoom.Position, king.Radius));
            Assert.IsNull(world.SpawnFitting(king, TileMap.Center(25, 7), reach: 2), "a two-tile corridor holds no XL body");
            Assert.IsNotNull(world.SpawnFitting(Shipped.Find("big_slime"), TileMap.Center(25, 7), reach: 2), "but an L one");
        }
    }
}
