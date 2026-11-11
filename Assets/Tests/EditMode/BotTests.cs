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
