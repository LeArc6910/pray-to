using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The arenas' sheet (arenas.csv): each boss's arena dressed to its own idea, and the entrance a boss makes when it
    // wakes (Docs/Bosses.md 아레나 컨셉과 입장 연출).
    public class ArenaTests
    {
        const string Header = "boss,arena,appear,entrance,seconds,again,title\n";

        [Test]
        public void TheShippedSheetGivesEveryBossAnArenaOfItsOwn()
        {
            var arenas = Bosses.Staged.Arenas;
            Assert.IsEmpty(arenas.Errors);
            foreach (var boss in Bosses.Staged.Bosses)
            {
                Assert.IsNotNull(boss.Arena, $"{boss.Id} has no row");
                Assert.AreNotEqual(ArenaKind.Plain, boss.Arena.Kind, boss.Id);
                Assert.IsNotEmpty(boss.Arena.Title, boss.Id);
                Assert.Greater(boss.Arena.Seconds, 0, boss.Id);
                Assert.LessOrEqual(boss.Arena.Again, 1.5f, $"{boss.Id}: seen before, it is short");
            }
            Assert.AreEqual(Bosses.Staged.Bosses.Count, arenas.Arenas.Select(a => a.Kind).Distinct().Count(), "no two arenas alike");
            Assert.AreEqual(BossEntrance.Burrow, Bosses.Staged.Find("vein_eater").Arena.Entrance);
            Assert.AreEqual(BossEntrance.Seated, Bosses.Staged.Find("shrine_samurai").Arena.Entrance);
            Assert.AreEqual(BossEntrance.Stride, Bosses.Staged.Find("black_knight").Arena.Entrance);
            Assert.AreEqual(BossEntrance.Thread, Bosses.Staged.Find("silk_mother").Arena.Entrance);
            Assert.AreEqual(BossEntrance.Awaken, Bosses.Staged.Find("cave_warden").Arena.Entrance);
            Assert.AreEqual(BossEntrance.Ambush, Bosses.Staged.Find("antlion").Arena.Entrance);
            Assert.AreEqual(BossEntrance.Cocoon, Bosses.Staged.Find("crystal_queen").Arena.Entrance);
            Assert.AreEqual(BossEntrance.Kneel, Bosses.Staged.Find("mad_general").Arena.Entrance);
            Assert.AreEqual(4, Bosses.Staged.Bosses.Count(b => b.Arena.Entrance == BossEntrance.Common));
            // The sheet read without it leaves the bosses as they were.
            Assert.IsNull(Bosses.Shipped.Bosses[0].Arena);
        }

        [Test]
        public void ABadRowIsReportedWithItsLine()
        {
            BossData Fresh() => Bosses.Parse(Bosses.BossesHeader + "dummy,허수아비,1000,50,0,2,1,,1,3,10\n",
                Bosses.AttacksHeader + "dummy,a,공격,,circle,nearest,1,30,2,,,,1,,,1,1,\n");
            Assert.IsEmpty(ArenaSheet.Parse(Header + "dummy,Hall,March,Stride,3,1,기사\n", Fresh()).Errors);
            StringAssert.Contains("2행", ArenaSheet.Parse(Header + "nobody,Hall,March,,3,1,x\n", Fresh()).Errors.Single());
            StringAssert.Contains("arena", ArenaSheet.Parse(Header + "dummy,Castle,March,,3,1,x\n", Fresh()).Errors.Single());
            StringAssert.Contains("seconds", ArenaSheet.Parse(Header + "dummy,Hall,March,,30,1,x\n", Fresh()).Errors.Single());
            StringAssert.Contains("again", ArenaSheet.Parse(Header + "dummy,Hall,March,,3,4,x\n", Fresh()).Errors.Single());
            StringAssert.Contains("이미", ArenaSheet.Parse(Header + "dummy,Hall,March,,3,1,x\ndummy,Web,Lurk,,3,1,x\n", Fresh()).Errors.Single());
            // No sheet at all: plain arenas, no entrance.
            var bare = Fresh();
            Assert.IsEmpty(ArenaSheet.Parse("", bare).Errors);
            Assert.IsNull(bare.Bosses[0].Arena);
            Assert.AreEqual(ArenaKind.Plain, bare.Arenas.CaveKind(0));
        }

        // ---- The entrance ----

        static readonly Vec2 Spawn = TileMap.Center(20, 5);
        static readonly Vec2 Home = TileMap.Center(20, 26);

        static BossDefinition Entering(float seconds = 3, float again = 1)
        {
            var data = Bosses.Parse(Bosses.BossesHeader + "dummy,허수아비,1000,50,0,2,1,,1,3,10\n",
                Bosses.AttacksHeader + "dummy,a,공격,,circle,nearest,0.5,30,2,,,,1,,,0.5,1,\n");
            ArenaSheet.Parse(Header + $"dummy,Hall,March,Stride,{seconds},{again},기사\n", data);
            return data.Find("dummy");
        }

        static World Room(out Boss boss, BossDefinition definition)
        {
            var world = new World(Maps.Room(40), Spawn, Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });
            boss = world.PlaceBoss(definition, Home, 8);
            return world;
        }

        static void Walk(World world, Player player, Vec2 to)
        {
            for (int i = 0; i < 100 && Vec2.Distance(player.Position, to) > 0.001f; i++)
                Assert.IsTrue(world.MovePlayer(player.Id, player.Position + (to - player.Position).ClampLength(2), player.Facing, 10));
        }

        static List<SimEvent> Run(World world, float seconds)
        {
            var events = new List<SimEvent>();
            for (int i = 0; i < World.Ticks(seconds); i++)
            {
                world.Step();
                events.AddRange(world.TakeEvents());
            }
            return events;
        }

        [Test]
        public void AWokenBossComesInBeforeItFights()
        {
            var world = Room(out var boss, Entering(seconds: 3));
            var alice = world.AddPlayer("a", null);
            Walk(world, alice, Home + new Vec2(0, -6));

            var events = Run(world, 0.1f);
            Assert.AreEqual(BossState.Enter, boss.State);
            Assert.IsFalse(boss.EntersAgain);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.BossAwake));
            Assert.IsTrue(boss.Invulnerable, "it cannot be hurt while it comes in");
            Assert.AreEqual(World.EntranceWhole, World.BossPhaseField(boss) >> World.BossAttackShift, "the wire says which entrance");
            Assert.AreEqual((byte)BossState.Enter, World.BossStateByte(boss) & 0x0F);

            // Nothing is aimed, nothing lands and it does not move for the whole of it.
            Run(world, 2.7f);
            Assert.AreEqual(BossState.Enter, boss.State);
            Assert.IsEmpty(world.Hazards);
            Assert.IsEmpty(world.Projectiles);
            Assert.IsNull(boss.Attack);
            Assert.AreEqual(Home, boss.Position);
            Assert.AreEqual(alice.Stats.MaxHealth, alice.Health);

            // Then the fight starts as it always did.
            Run(world, 0.4f);
            Assert.AreNotEqual(BossState.Enter, boss.State);
            Assert.IsNotNull(boss.Attack);
            Assert.IsFalse(boss.Invulnerable);
        }

        [Test]
        public void ABlowWakesItIntoItsEntranceAndDoesNotHurtIt()
        {
            var world = Room(out var boss, Entering());
            var alice = world.AddPlayer("a", null);
            // Just outside where walking in wakes it, facing it.
            Walk(world, alice, Home + new Vec2(0, -8.5f));
            Run(world, 0.1f);
            Assert.AreEqual(BossState.Dormant, boss.State);
            boss.Position = alice.Position + new Vec2(0, 1.2f);
            world.MovePlayer(alice.Id, alice.Position, new Vec2(0, 1), 1);
            world.SetAttacking(alice.Id, true);
            Run(world, 0.5f);
            Assert.AreEqual(BossState.Enter, boss.State);
            Assert.AreEqual(boss.MaxHealth, boss.Health);
        }

        [Test]
        public void SeenOnceItComesInTheShortWay()
        {
            var world = Room(out var boss, Entering(seconds: 3, again: 1));
            var alice = world.AddPlayer("a", null);
            Walk(world, alice, Home + new Vec2(0, -6));
            Run(world, 3.2f);
            Assert.AreNotEqual(BossState.Enter, boss.State);
            Assert.Contains("dummy", world.BossesMet.ToList());

            // Everyone leaves; it goes back to sleep; the next time it is quick.
            Walk(world, alice, Spawn);
            Run(world, 6);
            Assert.AreEqual(BossState.Dormant, boss.State);
            Walk(world, alice, Home + new Vec2(0, -6));
            Run(world, 0.1f);
            Assert.AreEqual(BossState.Enter, boss.State);
            Assert.IsTrue(boss.EntersAgain);
            Assert.AreEqual(World.EntranceAgain, World.BossPhaseField(boss) >> World.BossAttackShift);
            Run(world, 1.1f);
            Assert.AreNotEqual(BossState.Enter, boss.State);
        }

        [Test]
        public void ThoseWhoHaveFelledItSeeTheShortWayAndANewcomerTheWhole()
        {
            var world = Room(out var boss, Entering());
            var veteran = world.AddPlayer("a", null);
            veteran.Records[(RecordKind.Boss, "dummy")] = 2;
            Walk(world, veteran, Home + new Vec2(0, -6));
            Run(world, 0.1f);
            Assert.IsTrue(boss.EntersAgain, "a veteran alone");

            var other = Room(out var second, Entering());
            var old = other.AddPlayer("a", null);
            old.Records[(RecordKind.Boss, "dummy")] = 1;
            var fresh = other.AddPlayer("b", null);
            Walk(other, fresh, Home + new Vec2(0, -9));
            Walk(other, old, Home + new Vec2(0, -6));
            Run(other, 0.1f);
            Assert.AreEqual(BossState.Enter, second.State);
            Assert.IsFalse(second.EntersAgain, "a newcomer beside the veteran");
        }

        [Test]
        public void ABossWithNoRowWakesAtOnceAsBefore()
        {
            var plain = Bosses.Parse(Bosses.BossesHeader + "dummy,허수아비,1000,50,0,2,1,,1,3,10\n",
                Bosses.AttacksHeader + "dummy,a,공격,,circle,nearest,0.5,30,2,,,,1,,,0.5,1,\n").Find("dummy");
            var world = Room(out var boss, plain);
            Walk(world, world.AddPlayer("a", null), Home + new Vec2(0, -6));
            Run(world, 0.1f);
            Assert.AreNotEqual(BossState.Enter, boss.State);
            Assert.AreNotEqual(BossState.Dormant, boss.State);
        }

        [Test]
        public void IfEveryoneLeavesDuringTheEntranceItGoesBackToSleep()
        {
            var world = Room(out var boss, Entering(seconds: 5));
            var alice = world.AddPlayer("a", null);
            Walk(world, alice, Home + new Vec2(0, -6));
            Run(world, 0.5f);
            Assert.AreEqual(BossState.Enter, boss.State);
            Walk(world, alice, Spawn);
            Run(world, 6);
            Assert.AreEqual(BossState.Dormant, boss.State);
        }

        // ---- The arenas ----

        static HashSet<(int, int)> Walked(TileMap map, int fromX, int fromY)
        {
            var seen = new HashSet<(int, int)> { (fromX, fromY) };
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue((fromX, fromY));
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    if (map.IsSolid(x + dx, y + dy) || !seen.Add((x + dx, y + dy))) continue;
                    queue.Enqueue((x + dx, y + dy));
                }
            }
            return seen;
        }

        [Test]
        public void EachKindIsDressedWithItsOwnThings([Values(1ul, 2ul, 3ul)] ulong seed)
        {
            int Count(TileMap map, params TileType[] types)
            {
                int n = 0;
                for (int y = 0; y < map.Height; y++)
                    for (int x = 0; x < map.Width; x++)
                        if (types.Contains(map[x, y])) n++;
                return n;
            }
            var signs = new Dictionary<ArenaKind, (TileType[] Types, int AtLeast)[]>
            {
                [ArenaKind.Ruin] = new[] { (new[] { TileType.Pillar, TileType.BrokenPillar }, 5), (new[] { TileType.Statue }, 2), (new[] { TileType.ArenaInlay }, 20) },
                [ArenaKind.Nest] = new[] { (new[] { TileType.Cobweb }, 30), (new[] { TileType.Spire }, 4), (new[] { TileType.Cocoon }, 3) },
                [ArenaKind.Caldera] = new[] { (new[] { TileType.Lava }, 20), (new[] { TileType.Fissure }, 12) },
                [ArenaKind.Altar] = new[] { (new[] { TileType.RuneFloor }, 40), (new[] { TileType.RuneStone }, 4), (new[] { TileType.Brazier }, 4) },
                [ArenaKind.Flooded] = new[] { (new[] { TileType.Water }, 30), (new[] { TileType.Dock }, 8), (new[] { TileType.Pillar }, 3) },
                [ArenaKind.Pit] = new[] { (new[] { TileType.ArenaInlay }, 60), (new[] { TileType.Bones }, 6) },
                [ArenaKind.Warcamp] = new[] { (new[] { TileType.DemonBanner }, 3), (new[] { TileType.Brazier }, 3), (new[] { TileType.WeaponRack, TileType.Totem }, 3) },
                [ArenaKind.Chapel] = new[] { (new[] { TileType.Water }, 12), (new[] { TileType.RuneFloor }, 6), (new[] { TileType.Throne }, 1), (new[] { TileType.Lantern }, 3) },
                [ArenaKind.Mine] = new[] { (new[] { TileType.Rail }, 25), (new[] { TileType.MineCart }, 1), (new[] { TileType.OrePile }, 3), (new[] { TileType.Fissure }, 6) },
                [ArenaKind.Dojo] = new[] { (new[] { TileType.CherryTree }, 4), (new[] { TileType.Torii }, 2), (new[] { TileType.Petals }, 20), (new[] { TileType.ArenaInlay }, 30) },
                [ArenaKind.Hall] = new[] { (new[] { TileType.ArenaCarpet }, 15), (new[] { TileType.Throne }, 1), (new[] { TileType.ArmorStand }, 5), (new[] { TileType.Pillar }, 4) },
                [ArenaKind.Web] = new[] { (new[] { TileType.Cobweb }, 60), (new[] { TileType.Cocoon }, 5), (new[] { TileType.Bones }, 3) },
            };
            foreach (ArenaKind kind in System.Enum.GetValues(typeof(ArenaKind)))
            {
                var map = WorldGenerator.ArenaSample(kind, seed);
                var again = WorldGenerator.ArenaSample(kind, seed);
                for (int y = 0; y < map.Height; y++)
                    for (int x = 0; x < map.Width; x++)
                        Assert.AreEqual(map[x, y], again[x, y], $"{kind}: the same seed, the same room");
                if (kind == ArenaKind.Plain)
                {
                    Assert.AreEqual(0, Count(map, System.Enum.GetValues(typeof(TileType)).Cast<TileType>().Where(t => t != TileType.Floor && t != TileType.Wall).ToArray()));
                    continue;
                }
                foreach (var (types, atLeast) in signs[kind])
                    Assert.GreaterOrEqual(Count(map, types), atLeast, $"{kind} seed {seed}: {string.Join("/", types)}");

                // The room for the fight is whole: nothing solid within the rim, and every open tile of the round
                // room is walked to from the way in.
                int c = map.Width / 2, r = WorldGenerator.ArenaRadius;
                var bare = WorldGenerator.ArenaSample(ArenaKind.Plain, seed);
                var walked = Walked(map, c, 2);
                for (int y = 0; y < map.Height; y++)
                {
                    for (int x = 0; x < map.Width; x++)
                    {
                        int far = (x - c) * (x - c) + (y - c) * (y - c);
                        if (far < 77) Assert.IsFalse(map.IsSolid(x, y), $"{kind} seed {seed}: {map[x, y]} stands in the fight at {x - c},{y - c}");
                        if (!map.IsSolid(x, y)) Assert.IsTrue(walked.Contains((x, y)), $"{kind} seed {seed}: {x - c},{y - c} is shut off");
                        if (far > 118) Assert.AreEqual(bare[x, y], map[x, y], $"{kind} seed {seed}: changed outside the room at {x - c},{y - c}");
                        // The way in keeps its three tiles.
                        if (y < c - r && x >= c - 1 && x <= c + 1 && y >= 1) Assert.IsFalse(map.IsSolid(x, y), $"{kind} seed {seed}: the way in is blocked");
                        // Lava never lies where someone walking in would step.
                        if (map[x, y] == TileType.Lava) Assert.Greater(System.Math.Abs(x - c) + (y > c ? 99 : 0), 2, $"{kind} seed {seed}: lava by the way in");
                    }
                }
            }
        }

        [Test]
        public void EverySeedDressesItsArenasAndKeepsEveryWay([Range(1, 12)] int seed)
        {
            var plain = WorldGenerator.Generate((ulong)seed, story: Tales.Shipped);
            var world = WorldGenerator.Generate((ulong)seed, story: Tales.Shipped, arenas: Bosses.Staged.Arenas);
            var again = WorldGenerator.Generate((ulong)seed, story: Tales.Shipped, arenas: Bosses.Staged.Arenas);
            Assert.AreEqual(plain.Arenas.Count, world.Arenas.Count);
            Assert.AreEqual(world.Arenas.Count, world.ArenaKinds.Count);
            Assert.IsTrue(plain.ArenaKinds.All(k => k == ArenaKind.Plain));

            // Each boss's arena is of its kind, where the host puts the boss.
            var cave = Bosses.Staged.Bosses.Where(b => b.Place == null).ToList();
            for (int i = 0; i < cave.Count && i < world.CaveArenas; i++) Assert.AreEqual(cave[i].Arena.Kind, world.ArenaKinds[i], $"seed {seed}: {cave[i].Id}");
            foreach (var boss in Bosses.Staged.Bosses.Where(b => b.Place != null))
            {
                Assert.IsTrue(world.PlaceArenas.TryGetValue(boss.Place, out var at), $"seed {seed}: {boss.Id} has no arena");
                Assert.AreEqual(boss.Arena.Kind, world.ArenaKindAt(at), $"seed {seed}: {boss.Id}");
            }

            var map = world.Map;
            int changed = 0;
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    Assert.AreEqual(map[x, y], again.Map[x, y], $"seed {seed}: not the same twice at {x},{y}");
                    if (map[x, y] == plain.Map[x, y]) continue;
                    changed++;
                    // Only inside an arena's reach.
                    Assert.IsTrue(world.Arenas.Any(a => (x - TileMap.Cell(a.X)) * (x - TileMap.Cell(a.X)) + (y - TileMap.Cell(a.Y)) * (y - TileMap.Cell(a.Y)) <= 118),
                        $"seed {seed}: {map[x, y]} at {x},{y} is outside every arena");
                }
            }
            Assert.Greater(changed, 60 * world.Arenas.Count, $"seed {seed}: the arenas are dressed");

            // Everything that was walked to is still walked to, and the middle of each arena stays open for the fight.
            var before = new FlowField(plain.Map);
            before.Build(new[] { plain.Spawn }, int.MaxValue - 1);
            var after = new FlowField(map);
            after.Build(new[] { world.Spawn }, int.MaxValue - 1);
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    if (!map.IsSolid(x, y) && before.DistanceAt(x, y) != FlowField.Unreached)
                        Assert.AreNotEqual(FlowField.Unreached, after.DistanceAt(x, y), $"seed {seed}: {x},{y} was cut off");
            foreach (var arena in world.Arenas)
            {
                int cx = TileMap.Cell(arena.X), cy = TileMap.Cell(arena.Y);
                Assert.AreNotEqual(FlowField.Unreached, after.DistanceAt(cx, cy), $"seed {seed}: an arena cannot be walked to");
                for (int dy = -8; dy <= 8; dy++)
                    for (int dx = -8; dx <= 8; dx++)
                        if (dx * dx + dy * dy < 77) Assert.IsFalse(map.IsSolid(cx + dx, cy + dy), $"seed {seed}: {map[cx + dx, cy + dy]} stands in the fight");
            }
        }

        [Test]
        public void TheHostAndTheClientMakeTheSameArenasAndTheBossComesInOverTheWire()
        {
            var host = new WorldHost(11, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Staged, Classes.Shipped, Tiles.Shipped, story: Tales.Shipped);
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Staged, Gear.Data, Classes.Shipped, null, Tiles.Shipped) { Story = Tales.Shipped };
            host.Receive(1, client.JoinMessage());
            void Pump(float seconds)
            {
                for (int i = 0; i < World.Ticks(seconds); i++)
                {
                    host.Tick();
                    foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
                }
                client.AdvanceRenderClock(1);
            }
            Pump(0.2f);
            CollectionAssert.AreEqual(host.Generated.ArenaKinds, client.ArenaKinds);
            Assert.AreEqual(ArenaKind.Ruin, client.ArenaKinds[0]);
            for (int y = 0; y < client.Map.Height; y += 3)
                for (int x = 0; x < client.Map.Width; x += 3)
                    Assert.AreEqual(host.World.Map[x, y], client.Map[x, y]);

            // Walked into the first arena, the warden comes in; the client sees it entering, the whole way.
            var player = host.World.Players.Single();
            var boss = host.World.Bosses.First(b => b.Definition.Id == "cave_warden");
            player.Position = boss.Home + new Vec2(0, -5);
            Pump(0.5f);
            Assert.AreEqual(BossState.Enter, boss.State);
            var seen = client.Others().Single(e => e.Kind == EntityKind.Boss);
            Assert.AreEqual(BossState.Enter, seen.BossState);
            Assert.IsFalse(seen.EntersAgain);
            Assert.IsNull(seen.BossAttack);
            Assert.AreEqual("무너진 신전의 파수꾼", seen.Boss.Arena.Title);
        }
    }
}
