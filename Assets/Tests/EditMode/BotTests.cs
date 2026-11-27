using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The playtest bots' path finding, memory and getting-nowhere (Core/Bot, Docs/Playtest.md 상태로 움직이는 봇), and a
    // new character's first hour played by FirstHourBot in fresh worlds of a few seeds.
    public class BotTests
    {
        static readonly MonsterData None = MonsterSheet.Parse(Monsters.Header);

        // The player stands at the 'P' (floor), facing east.
        static World Cave(out Player player, string pickaxe, params string[] rows)
        {
            var map = Maps.FromRows(rows.Select(r => r.Replace('P', '.')).ToArray());
            int row = rows.ToList().FindIndex(r => r.Contains('P'));
            var at = TileMap.Center(rows[row].IndexOf('P'), rows.Length - 1 - row);
            var world = new World(map, at, None, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            player = world.AddPlayer("a", pickaxe == null ? new Equipment() : Gear.Wearing(pickaxe));
            return world;
        }

        [Test]
        public void DigsThroughAThinWallRatherThanWalkingFarRound()
        {
            var world = Cave(out var player, "copper_pickaxe",
                "#############",
                "#P....#....G#",
                "#.....#.....#",
                "#.....#.....#",
                "#.....#.....#",
                "#.....#.....#",
                "#...........#",
                "#############");
            var nav = new BotNav(world);
            nav.Fit(player);
            var path = nav.To(1, 6, 11, 6);
            Assert.IsNotNull(path);
            Assert.Contains((6, 6), path, "through the wall between");
            Assert.AreEqual((11, 6), path.Last());

            nav.PickaxeLevel = -1;
            var round = nav.To(1, 6, 11, 6);
            Assert.IsNotNull(round, "without a pickaxe it walks round");
            Assert.IsFalse(round.Contains((6, 6)));
            Assert.Greater(round.Count, path.Count);
        }

        [Test]
        public void NeverCrossesWaterNorTheMapsEdge()
        {
            var world = Cave(out var player, "copper_pickaxe",
                "#######",
                "#######",
                "#P.~..#",
                "#..~..#",
                "#..~..#",
                "#######");
            var nav = new BotNav(world);
            nav.Fit(player);
            var path = nav.To(1, 3, 5, 3);
            Assert.IsNotNull(path, "round the water through the rock above it");
            Assert.IsFalse(path.Any(t => world.Map[t.X, t.Y] == TileType.Water));
            Assert.IsFalse(path.Any(t => world.Map.IsEdge(t.X, t.Y)));
        }

        // On a map of rooms (generator version 2, 2026-11-22) a far goal is gone to room by room: each path ends at a room
        // on the way within a leg, so no search looks across the whole map, and the legs get there.
        [Test]
        public void GoesFarRoomByRoomOnAMapOfRooms()
        {
            var host = new WorldHost(3, Monsters.Shipped, Gear.Data, config: new HostConfig { MapSize = 512, MapVersion = WorldGenerator.RoomVersion },
                bosses: Bosses.Shipped, classes: Classes.Shipped, tiles: Tiles.Shipped, story: Tales.Shipped);
            host.Receive(1, new JoinMessage("bot", "봇").Encode());
            var world = host.World;
            var player = world.Players[0];
            var nav = new BotNav(world) { Rooms = host.Generated.Rooms };
            nav.Fit(player);
            var far = host.Generated.Places.OrderByDescending(p => Vec2.Distance(p.Center, world.Spawn)).First();
            int x = TileMap.Cell(world.Spawn.X), y = TileMap.Cell(world.Spawn.Y);
            int legs = 0;
            while (System.Math.Max(System.Math.Abs(far.X - x), System.Math.Abs(far.Y - y)) > nav.FarTiles)
            {
                var path = nav.Toward(x, y, far.X, far.Y);
                Assert.IsNotNull(path, $"leg {legs} from {x},{y}");
                Assert.IsNotEmpty(path);
                Assert.Greater(nav.LastRest, 0, "a leg stops short of a far goal");
                Assert.Less(nav.Searched, 20000, "a leg looks at a small part of the map");
                (x, y) = path[path.Count - 1];
                Assert.LessOrEqual(System.Math.Max(System.Math.Abs(path[path.Count - 1].X - path[0].X), System.Math.Abs(path[path.Count - 1].Y - path[0].Y)), nav.LegTiles + 2);
                Assert.Less(++legs, 60, "the legs get there");
            }
            Assert.IsNotNull(nav.Toward(x, y, far.X, far.Y), "the last stretch");
            Assert.AreEqual(0, nav.LastRest);
            Assert.IsNull(new BotNav(world).Rooms, "a bot with no rooms searches as it did");
        }

        [Test]
        public void DigsTowardTheNearestVeinAndStandsBesideAWorkbench()
        {
            var world = Cave(out var player, "copper_pickaxe",
                "##########",
                "#P.#####.#",
                "#........#",
                "##########");
            world.Map[4, 1] = TileType.Workbench;
            world.Map[7, 2] = TileType.CopperOre;
            var memory = new BotMemory(world, player);
            var pilot = new BotPilot(world, player, memory);

            var dig = pilot.Next(BotTarget.Dig(TileType.CopperOre));
            Assert.AreNotEqual(BotDo.NoWay, dig.Do);
            Assert.AreEqual((7, 2), dig.Goal, "the vein is the end of the way");

            var beside = pilot.Next(BotTarget.Beside(TileType.Workbench));
            Assert.AreNotEqual(BotDo.NoWay, beside.Do);
            var goal = beside.Goal.Value;
            Assert.AreEqual(TileType.Workbench, world.Map[goal.X, goal.Y], "the way ends at the workbench, the step before it beside it");
        }

        [Test]
        public void SeesThroughOpenFloorAndKnowsItsEdge()
        {
            var world = Cave(out var player, null,
                "########################",
                "#P.....................#",
                "########################");
            var memory = new BotMemory(world, player);
            Assert.IsTrue(memory.Observe(), "the first look is progress");
            Assert.IsTrue(memory.Seen(1 + BotMemory.Sight, 1));
            Assert.IsFalse(memory.Seen(2 + BotMemory.Sight, 1), "a screen's half and no further");
            Assert.IsTrue(memory.IsFrontier(1 + BotMemory.Sight, 1));
            Assert.IsFalse(memory.IsFrontier(2, 1));
            Assert.IsTrue(memory.Seen(2, 2), "the rock that bounds the floor");
        }

        [Test]
        public void GivesUpAfterGettingNowhereAndWritesWhere()
        {
            var world = Cave(out var player, null,
                "##########",
                "#........#",
                "#P.......#",
                "#........#",
                "##########");
            var memory = new BotMemory(world, player);
            var pilot = new BotPilot(world, player, memory) { StallTicks = 30, GiveUp = 2, Goal = "test" };
            BotIntent last = null;
            int stalls = 0;
            // The bot is told where to go but never moves.
            for (int i = 0; i < 200 && (last == null || last.Do != BotDo.NoWay); i++)
            {
                world.Step();
                last = pilot.Next(BotTarget.At(8, 2));
                if (last.Stalled) stalls++;
            }
            Assert.AreEqual(BotDo.NoWay, last.Do);
            Assert.AreEqual(2, stalls, "stalled once, went back, stalled again and gave up");
            Assert.AreEqual(1, memory.Stuck.Count);
            var stuck = memory.Stuck[0];
            Assert.AreEqual("test", stuck.Goal);
            Assert.AreEqual("at 8 2", stuck.Target);
            StringAssert.Contains("@", stuck.Map);
            Assert.IsNotEmpty(stuck.Recent);
        }

        // Hurt badly with a monster near (2026-11-24, the first hour on generator 3: a bot fought a sand cave's crowds
        // to the last blow by its bed, again and again), a bot that retreats goes away from it along open floor, rests
        // once out of its way and comes back to the fight when its health is mostly back. One that does not (the web
        // bot) fights on as before.
        [Test]
        public void GetsAwayToRestWhenHurtAmongMonstersAndComesBack()
        {
            var world = new World(Maps.Room(40), TileMap.Center(20, 20), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            var player = world.AddPlayer("a", null);
            var slime = world.SpawnEnemy(Monsters.Shipped.Find("slime"), player.Position + new Vec2(0.8f, 0));
            world.MonstersHold = true;
            player.Health = player.Stats.MaxHealth * (BotPilot.RetreatBelow - 0.05f);

            var stays = new BotPilot(world, player, new BotMemory(world, player));
            Assert.AreEqual(BotDo.Fight, stays.Next(BotTarget.Monster()).Do, "without Retreats it fights on");

            var memory = new BotMemory(world, player);
            var pilot = new BotPilot(world, player, memory) { Retreats = true };
            var away = pilot.Next(BotTarget.Monster());
            Assert.AreEqual(BotDo.Move, away.Do, away.ToString());
            Assert.AreEqual("물러남", away.Why);
            Assert.LessOrEqual(Vec2.Dot(away.Direction, slime.Position - player.Position), 0, "not toward the slime");
            Assert.AreEqual(1, memory.Retreats);
            var end = away.Goal.Value;
            Assert.GreaterOrEqual(Vec2.Distance(TileMap.Center(end.X, end.Y), slime.Position), BotPilot.RetreatClear, "to where the slime is well off");

            // Out of its way (it waits where it stood), the bot rests; with its health mostly back it goes at it again.
            player.Position = slime.Position + new Vec2(-BotPilot.RetreatClear - 1, 0);
            var rest = pilot.Next(BotTarget.Monster());
            Assert.AreEqual(BotDo.Wait, rest.Do, rest.ToString());
            Assert.AreEqual("물러나 쉼", rest.Why);
            player.Health = player.Stats.MaxHealth * BotPilot.RetreatUntil;
            var back = pilot.Next(BotTarget.Monster());
            Assert.AreEqual(BotDo.Move, back.Do, back.ToString());
            Assert.Greater(Vec2.Dot(back.Direction, slime.Position - player.Position), 0, "back toward the slime");
            Assert.AreEqual(1, memory.Retreats);
        }

        // A foe that goes under the ground and mends (a sand worm, 2026-11-24: a bot struck one for eight minutes by the
        // water it meant to fish at) counts as getting on only when its health goes below the lowest it has been: one
        // that mends back past every blow is given up after StallTicks.
        [Test]
        public void GivesUpOnAFoeThatMendsPastItsBlows()
        {
            var world = new World(Maps.Room(20), TileMap.Center(10, 10), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            var player = world.AddPlayer("a", null);
            var foe = world.SpawnEnemy(Monsters.Shipped.Find("slime"), player.Position + new Vec2(0.8f, 0));
            world.MonstersHold = true;
            var pilot = new BotPilot(world, player, new BotMemory(world, player)) { StallTicks = 30 };
            float most = foe.MaxHealth;
            bool fought = true;
            for (int i = 0; i < 120 && fought; i++)
            {
                world.Step();
                // Struck down a little, then mended back above where it was: never lower than its first low.
                foe.Health = i == 0 ? most * 0.5f : i % 2 == 0 ? most * 0.9f : most * 0.6f;
                fought = pilot.Next(BotTarget.Monster()).Do == BotDo.Fight;
            }
            Assert.IsFalse(fought, "let be once it mended past the blows for StallTicks");
        }

        // Driven far from a boss's drops (2026-11-27, the first hour's seed 6: the bot backed 33 tiles off to rest after
        // the warden fell and then found "nothing to pick up" within its 12 tiles till it gave the goal up), "loot" by
        // the material's name goes back for it; plain "loot" still looks only near.
        [Test]
        public void GoesBackForANamedDropItWasDrivenFarFrom()
        {
            var world = new World(Maps.Room(60), TileMap.Center(10, 30), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            var player = world.AddPlayer("a", null);
            var core = Gear.Data.FindMaterial("warden_core");
            var drop = world.DropMaterial(core, player.Position + new Vec2(33, 0));
            for (int i = 0; i < World.TicksPerSecond; i++) world.Step();
            var pilot = new BotPilot(world, player, new BotMemory(world, player));

            var near = pilot.Next(BotTarget.Loot);
            Assert.AreEqual(BotDo.Arrived, near.Do, near.ToString());
            var far = pilot.Next(BotTarget.LootOf("warden_core"));
            Assert.AreEqual(BotDo.Move, far.Do, far.ToString());
            Assert.Greater(Vec2.Dot(far.Direction, drop.Position - player.Position), 0, "toward the core");
            Assert.AreEqual("loot warden_core", BotTarget.Parse(new[] { "loot", "warden_core" }).ToString());

            // Walked there as the bot would, it has the core.
            for (int i = 0; i < 40 * World.TicksPerSecond && player.Inventory.CountOf(core) == 0; i++)
            {
                var intent = pilot.Next(BotTarget.LootOf("warden_core"));
                if (intent.Do == BotDo.Move)
                {
                    var to = PlayerMotor.Step(world.Map, player.Position, intent.Direction.Normalized, player.MoveSpeed, world.DoorsFor(player));
                    world.MovePlayer(player.Id, to, intent.Direction.Normalized, 1);
                }
                world.Step();
            }
            Assert.AreEqual(1, player.Inventory.CountOf(core), "picked up");
        }

        static BotSheets Shipped => new BotSheets
        {
            Monsters = Monsters.Shipped, Loot = Gear.Data, Bosses = Bosses.Shipped, Classes = Classes.Shipped, Tiles = Tiles.Shipped,
            Guide = Guide.Shipped, Npcs = People.Shipped, Story = Tales.Shipped,
        };

        // The first hour's first steps (Docs/Research2026-11.md 바로 시작할 순서: the bot gets to the first monster, the
        // first thing made and the first boss's arena without sticking, in fresh worlds of several seeds).
        [TestCase(1UL)]
        [TestCase(3UL)]
        [TestCase(5UL)]
        public void FirstHourReachesTheFirstMonsterCraftAndBoss(ulong seed)
        {
            var run = BotRun.FirstHour(Shipped, seed, minutes: 15, stopAfter: "boss");
            var m = run.Memory;
            string table = BotReport.Table(new[] { run }) + BotReport.Goals(new[] { run }) + BotReport.Stuck(new[] { run });
            Assert.AreEqual("cave_warden", run.FirstBoss);
            Assert.IsTrue(m.Moments.ContainsKey(BotMemory.FirstKill), "a monster felled\n" + table);
            Assert.IsTrue(m.Goals.ContainsKey("workbench"), "a workbench made and set down\n" + table);
            Assert.IsTrue(m.Moments.ContainsKey(BotMemory.Arena + "cave_warden"), "at the warden's arena\n" + table);
            foreach (string goal in new[] { "class", "stone", "workbench", "torch", "copper", "copper_gear" })
                Assert.IsTrue(m.Goals.ContainsKey(goal), $"{goal} met\n{table}");
            Assert.Greater(m.Explored, 0.2f, table);
        }

        [Test]
        public void TheSameSeedPlaysTheSame()
        {
            var a = BotRun.FirstHour(Shipped, 2, minutes: 2, stopAfter: "copper");
            var b = BotRun.FirstHour(Shipped, 2, minutes: 2, stopAfter: "copper");
            Assert.AreEqual(a.Ticks, b.Ticks);
            Assert.AreEqual(a.Memory.SeenTiles, b.Memory.SeenTiles);
            CollectionAssert.AreEqual(a.Memory.Goals.OrderBy(g => g.Key), b.Memory.Goals.OrderBy(g => g.Key));
        }
    }
}
