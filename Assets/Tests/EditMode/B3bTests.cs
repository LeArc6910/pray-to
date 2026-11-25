using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The crystal cave and the underground lake's monsters, rules and dungeons (2026-11-25, Docs/BiomeRework.md B3b): six
    // kinds of their own each, of several makes and sizes, and no slimes to fill their packs; shots glance off crystal
    // spires, a prism's shot breaks into three where it comes off, a tortoise's shell turns a player's shot back from in
    // front; a mimic stands as crystals until it is come to or struck and rings the mimics near it awake; a tentacle
    // keeps to the water's edge; the lake's shallows slow a walk; the geode and the sunken aqueduct have their mid-bosses
    // for masters; a new world of version 6 has their lairs and the lake's shallows, and a world of version 5 is laid as
    // bb20dac laid it.
    public class B3bTests
    {
        static readonly MonsterData Shipped = Monsters.Shipped;
        static DungeonData Sheet => Tiles.Shipped.Terrain.Dungeons;

        static List<SimEvent> Run(World world, float seconds, Action each = null)
        {
            var events = new List<SimEvent>();
            for (int i = World.Ticks(seconds); i > 0; i--)
            {
                world.Step();
                events.AddRange(world.TakeEvents());
                each?.Invoke();
            }
            return events;
        }

        static Enemy Spawn(World world, string id, Vec2 at) => world.SpawnEnemy(Shipped.Find(id), at);

        // A walked room of the size, every tile of the biome; changes laid on its map by the caller.
        static World Room(int size, Biome biome, Action<TileMap> lay = null)
        {
            var map = Maps.Room(size);
            lay?.Invoke(map);
            return new World(map, TileMap.Center(size / 2, size / 2), Shipped, 1, new WorldConfig { EnemyTarget = 0 }, biomes: BiomeMap.Of(size, size, (_, _) => biome));
        }

        // ---- The sheets ----

        static List<MonsterDefinition> KindsOf(Biome biome) => Shipped.Packs.Packs
            .Where(p => p.Place == null && p.Biome == biome && p.Weight > 0)
            .SelectMany(p => p.Members.Select(m => m.Monster)).Distinct().ToList();

        [TestCase(Biome.Crystal, "crystal_mimic", "crystal_tortoise", "crystal_shooter", "prism_wisp")]
        [TestCase(Biome.Lake, "tentacle", "great_crab", "mud_slime", "tide_chief")]
        public void TheCrystalCaveAndTheLakeHaveSixKindsOfTheirOwnAndNoSlimes(Biome biome, params string[] fresh)
        {
            var kinds = KindsOf(biome);
            foreach (var id in fresh) CollectionAssert.Contains(kinds.Select(k => k.Id), id, $"{biome}: its new {id} is in its packs");
            Assert.That(kinds.Count, Is.GreaterThanOrEqualTo(6), string.Join(", ", kinds.Select(k => k.Id)));
            Assert.IsTrue(kinds.All(k => k.Biome == biome), "each of its own: " + string.Join(", ", kinds.Where(k => k.Biome != biome).Select(k => k.Id)));
            Assert.That(kinds.Select(k => k.Role).Distinct().Count(), Is.GreaterThanOrEqualTo(3), "three makes at least");
            Assert.That(kinds.Select(k => k.Size).Distinct().Count(), Is.GreaterThanOrEqualTo(3), "three sizes at least");
            Assert.IsTrue(kinds.All(k => k.Size <= MonsterSize.L), "the field holds L at most");
            Assert.IsTrue(Shipped.Packs.Packs.Where(p => p.Biome == biome && p.Zone < 3).All(p => p.Members.All(m => m.Monster.Id != fresh[3])), $"{fresh[3]} (the elite) from zone 3");
        }

        [Test]
        public void TheirMidBossesAreXlDormantAndComeInNoPack()
        {
            foreach (var id in new[] { "geode_golem", "moss_turtle" })
            {
                var boss = Shipped.Find(id);
                Assert.AreEqual(MonsterSize.XL, boss.Size, id);
                Assert.AreEqual(0, boss.Weight, id);
                Assert.IsTrue(boss.Has(MonsterTraitKind.Dormant), id);
                Assert.IsFalse(Shipped.Packs.Packs.Any(p => p.Members.Any(m => m.Monster == boss)), id);
            }
            Assert.IsTrue(Shipped.Find("crystal_mimic").Has(MonsterTraitKind.Mimic));
            Assert.IsTrue(Shipped.Find("crystal_tortoise").Has(MonsterTraitKind.Reflect));
            Assert.IsTrue(Shipped.Find("prism_wisp").Has(MonsterTraitKind.Prism));
            Assert.IsTrue(Shipped.Find("tentacle").Has(MonsterTraitKind.Shore));
            foreach (var good in new[] { "mimic", "mimic:3", "reflect", "prism", "shore", "shore:2" }) Assert.IsTrue(MonsterSheet.Parse(File(good)).Success, good);
            StringAssert.Contains("mimic", string.Join("\n", MonsterSheet.Parse(File("mimic:9")).Errors));
            StringAssert.Contains("shore", string.Join("\n", MonsterSheet.Parse(File("shore:5")).Errors));
        }

        static string File(string traits) =>
            "id,name,level,health,damage,armor,speed,radius,aggro,reach,windup,cooldown,weight,traits\n" + $"a,가,1,10,1,0,1,0.4,5,1,1,1,0,{traits}\n";

        // ---- Crystal: shots glance off it ----

        // A player with a bow at the west of a room, facing east at a column of what the caller lays at x 12.
        static (World World, Player Player) Range(TileType column)
        {
            var world = Room(30, Biome.Crystal, map =>
            {
                for (int y = 10; y <= 20; y++) map[12, y] = column;
            });
            var player = world.AddPlayer("a", Gear.Wearing("copper_bow"));
            player.Position = TileMap.Center(6, 15);
            player.Facing = new Vec2(1, 0);
            return (world, player);
        }

        [Test]
        public void APlayersShotGlancesOffACrystalSpireButBreaksOnRock()
        {
            Assert.IsTrue(World.Glances(TileType.Spire));
            Assert.IsFalse(World.Glances(TileType.Wall));
            var (world, player) = Range(TileType.Spire);
            world.SetAttacking(player.Id, true);
            bool back = false;
            var events = Run(world, 1.5f, () => back |= world.Projectiles.Any(p => p.Friendly && p.Velocity.X < 0));
            Assert.Greater(world.ShotsGlanced, 0);
            Assert.IsTrue(back, "the shot came back off the spire");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxGlance));

            var (rock, archer) = Range(TileType.Wall);
            rock.SetAttacking(archer.Id, true);
            bool turned = false;
            Run(rock, 1.5f, () => turned |= rock.Projectiles.Any(p => p.Velocity.X < 0));
            Assert.AreEqual(0, rock.ShotsGlanced);
            Assert.IsFalse(turned, "rock stops it");
        }

        [Test]
        public void APrismsShotBreaksIntoThreeWhereItComesOffASpire()
        {
            var world = Room(30, Biome.Crystal, map =>
            {
                for (int x = 12; x <= 18; x++) map[x, 19] = TileType.Spire;
            });
            var player = world.AddPlayer("a", null);
            player.Position = TileMap.Center(15, 24);
            world.MonstersHold = true;
            var prism = Spawn(world, "prism_wisp", TileMap.Center(15, 14));
            Assert.IsTrue(world.ForceAttack(prism, "prism"));
            int most = 0;
            var events = Run(world, 1.6f, () => most = Math.Max(most, world.Projectiles.Count(p => p.Source == prism.Id)));
            Assert.AreEqual(3, most, "one shot, three after the spire");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxRefract && e.Source == prism.Id));
        }

        [Test]
        public void ATortoisesShellTurnsAShotFromInFrontBackButNotFromTheSide()
        {
            var (world, player) = Range(TileType.Floor);
            world.MonstersHold = true;
            var tortoise = Spawn(world, "crystal_tortoise", TileMap.Center(14, 15));
            tortoise.Facing = new Vec2(-1, 0);
            world.SetAttacking(player.Id, true);
            bool hostile = false;
            Run(world, 1.2f, () =>
            {
                hostile |= world.Projectiles.Any(p => !p.Friendly && p.Source == tortoise.Id);
                tortoise.Facing = new Vec2(-1, 0);
                if (tortoise.State == EnemyState.Recover) tortoise.State = EnemyState.Chase;
            });
            Assert.Greater(world.ShotsReflected, 0);
            Assert.IsTrue(hostile, "it went back as the tortoise's own");
            Assert.AreEqual(tortoise.MaxHealth, tortoise.Health, 1e-3f, "the shell took nothing");

            var (side, archer) = Range(TileType.Floor);
            side.MonstersHold = true;
            var turned = Spawn(side, "crystal_tortoise", TileMap.Center(14, 15));
            turned.Facing = new Vec2(0, 1);
            side.SetAttacking(archer.Id, true);
            Run(side, 1.2f, () => turned.Facing = new Vec2(0, 1));
            Assert.AreEqual(0, side.ShotsReflected);
            Assert.Less(turned.Health, turned.MaxHealth, "from the side it is struck");
        }

        // ---- Crystal: mimics ----

        [Test]
        public void AMimicStandsAsCrystalsThroughItsPacksAlarmAndWakesWhenComeToAndRingsTheOthers()
        {
            var world = Room(30, Biome.Crystal);
            var player = world.AddPlayer("a", null);
            player.Position = TileMap.Center(4, 15);
            var bat = Spawn(world, "crystal_bat", TileMap.Center(10, 15));
            var mimic = Spawn(world, "crystal_mimic", TileMap.Center(16, 15));
            var other = Spawn(world, "crystal_mimic", TileMap.Center(16, 18));
            var far = Spawn(world, "crystal_mimic", TileMap.Center(25, 25));
            world.Gather(new[] { bat, mimic, other, far });
            Run(world, 1.5f, () => player.Health = player.Stats.MaxHealth);
            Assert.AreNotEqual(EnemyState.Idle, bat.State, "the bat saw the player");
            Assert.IsTrue(World.IsDormant(mimic) && World.IsDormant(other), "the pack's alarm leaves the mimics as crystals");

            player.Position = TileMap.Center(14, 15);
            var events = Run(world, 0.5f, () => player.Health = player.Stats.MaxHealth);
            Assert.AreNotEqual(EnemyState.Idle, mimic.State, "within two tiles it wakes");
            Assert.AreNotEqual(EnemyState.Idle, other.State, "and rings the one three tiles off awake");
            Assert.AreEqual(EnemyState.Idle, far.State, "but not one across the room");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxWake && e.Source == mimic.Id));
        }

        [Test]
        public void AStruckMimicWakesTheLastOfAPackComesOutAndAnEliteOneDoesNotHide()
        {
            var world = Room(30, Biome.Crystal);
            var player = world.AddPlayer("a", Gear.Wearing("iron_sword"));
            player.Position = TileMap.Center(4, 15);
            var mimic = Spawn(world, "crystal_mimic", TileMap.Center(15, 15));
            world.Gather(new[] { mimic });
            Run(world, 0.5f);
            Assert.IsTrue(World.IsDormant(mimic));
            world.Hurt(mimic, player, 5);
            Run(world, 0.2f);
            Assert.AreNotEqual(EnemyState.Idle, mimic.State, "struck from afar, it wakes");

            var last = Room(30, Biome.Crystal);
            var walker = last.AddPlayer("b", null);
            walker.Position = TileMap.Center(4, 15);
            var bat = Spawn(last, "crystal_bat", TileMap.Center(9, 15));
            var hidden = Spawn(last, "crystal_mimic", TileMap.Center(15, 15));
            last.Gather(new[] { bat, hidden });
            Run(last, 1, () => walker.Health = walker.Stats.MaxHealth);
            Assert.IsTrue(World.IsDormant(hidden));
            bat.Health = 0;
            Run(last, 0.5f, () => walker.Health = walker.Stats.MaxHealth);
            Assert.AreNotEqual(EnemyState.Idle, hidden.State, "the last of its pack comes out");

            var camp = Room(30, Biome.Crystal);
            var seen = camp.AddPlayer("c", null);
            seen.Position = TileMap.Center(9, 15);
            var leader = Spawn(camp, "crystal_mimic", TileMap.Center(15, 15));
            leader.Elite = EliteRank.Leader;
            Assert.IsFalse(World.IsDormant(leader), "an elite one leads as itself");
            Run(camp, 0.5f, () => seen.Health = seen.Stats.MaxHealth);
            Assert.AreNotEqual(EnemyState.Idle, leader.State, "it sees as far as its kind does");
        }

        // ---- The lake ----

        [Test]
        public void ATentacleKeepsToTheWatersEdgeAndOneSetDownAwayGoesToIt()
        {
            var world = Room(30, Biome.Lake, map =>
            {
                for (int y = 1; y < 29; y++) map[10, y] = TileType.Water;
            });
            var player = world.AddPlayer("a", null);
            player.Position = TileMap.Center(19, 15);
            var tentacle = Spawn(world, "tentacle", TileMap.Center(11, 15));
            var stray = Spawn(world, "tentacle", TileMap.Center(18, 12));
            float furthest = 0;
            Run(world, 8, () =>
            {
                player.Health = player.Stats.MaxHealth;
                foreach (var t in new[] { tentacle, stray })
                    if (!t.IsDead) furthest = Math.Max(furthest, Math.Abs(t.Position.X - 10.5f));
            });
            Assert.LessOrEqual(furthest, 2.6f, "a tile from the water at most (its middle a little past its tile's)");
            Assert.IsTrue(world.ByWater(stray.Position), "the one set down by the wall went to the water");
            Assert.AreNotEqual(EnemyState.Idle, tentacle.State, "it is after the player all the same");
        }

        [Test]
        public void TheShallowsSlowAWalkAndAreWalkedOn()
        {
            var map = Maps.Room(20);
            for (int y = 1; y < 19; y++)
                for (int x = 10; x < 19; x++) map[x, y] = TileType.ShallowWater;
            Assert.IsFalse(map.IsSolid(12, 10));
            Assert.AreEqual(PlayerMotor.ShallowSlow, PlayerMotor.GroundSpeed(map, TileMap.Center(12, 10)), 1e-4f);
            Assert.AreEqual(1f, PlayerMotor.GroundSpeed(map, TileMap.Center(4, 10)), 1e-4f);
            var dry = PlayerMotor.Step(map, TileMap.Center(4, 10), new Vec2(1, 0), 5);
            var wet = PlayerMotor.Step(map, TileMap.Center(12, 10), new Vec2(1, 0), 5);
            Assert.AreEqual(PlayerMotor.ShallowSlow, (wet.X - 12.5f) / (dry.X - 4.5f), 0.02f);
            Assert.AreEqual(TileType.ShallowWater, RoomSheet.Tile('f'));
        }

        [Test]
        public void OneUnderTheGroundInTheLakeIsUnderTheWater()
        {
            var map = Maps.Room(10);
            map[3, 3] = TileType.ShallowWater;
            var lake = BiomeMap.Of(10, 10, (_, _) => Biome.Lake);
            var cave = BiomeMap.Of(10, 10, (_, _) => Biome.Cave);
            Assert.IsTrue(World.UnderWater(map, lake, TileMap.Center(5, 5), true));
            Assert.IsFalse(World.UnderWater(map, lake, TileMap.Center(5, 5), false), "only when under");
            Assert.IsFalse(World.UnderWater(map, cave, TileMap.Center(5, 5), true), "a mound in the cave");
            Assert.IsTrue(World.UnderWater(map, cave, TileMap.Center(3, 3), true), "the shallows anywhere");
        }

        // ---- Dungeons ----

        static World Hand(Biome biome, out DungeonGate gate, string dungeon)
        {
            var map = new TileMap(320, 40, TileType.Floor);
            for (int x = 0; x < map.Width; x++)
            {
                map[x, 0] = TileType.Wall;
                map[x, map.Height - 1] = TileType.Wall;
            }
            map.AddPocket(DungeonPocket.Rows);
            var world = new World(map, new Vec2(10.5f, 10.5f), Monsters.Shipped, 11, new WorldConfig { EnemyTarget = 0, ZoneLevel = 4 }, Gear.Data, Tiles.Shipped);
            gate = world.AddGate(40, 20, biome);
            Assert.IsTrue(world.ChooseDungeon(gate.Index, dungeon));
            return world;
        }

        [TestCase(Biome.Crystal, "geode_heart", "정동 속", "geode_golem", "정동 골렘", "crystal_tortoise", '^')]
        [TestCase(Biome.Lake, "sunken_aqueduct", "잠긴 수로", "moss_turtle", "이끼등 거북", "great_crab", 'f')]
        public void TheGeodeAndTheAqueductHaveTheirMidBossesForMastersAndABigBodyInEachPackRoom(Biome biome, string id, string name, string boss, string bossName,
            string big, char floor)
        {
            var row = Sheet.For(biome);
            Assert.AreEqual(id, row.Id);
            Assert.AreEqual(name, row.Name);
            Assert.IsTrue(row.Master);
            Assert.AreEqual(boss, row.Boss);
            Assert.AreEqual(7, row.Light.Length, "its gate room's light");
            Assert.Less(row.Experience, 100, "its chest gives the rest");
            var chunks = Tiles.Shipped.Terrain.Rooms.Chunks.Where(c => c.Dungeon && c.Biomes.Contains(biome)).ToList();
            Assert.GreaterOrEqual(chunks.Count(c => c.Role == RoomRole.Pack), 3, "pack rooms of its own");
            Assert.GreaterOrEqual(chunks.Count(c => c.Role == RoomRole.Elite), 2, "a champions' room and a master's");
            Assert.IsTrue(chunks.Any(c => c.Rows.Any(r => r.IndexOf(floor) >= 0)), $"its own ('{floor}')");
            Assert.AreEqual("deep_hollow", Sheet.For(Biome.Abyss).Id, "the abyss's gate still leads to the deep hollow");

            var world = Hand(biome, out var gate, id);
            world.MonstersHold = true;
            var player = world.AddPlayer("a", Gear.Wearing("mithril_sword"));
            player.Position = gate.Center;
            Assert.IsTrue(world.EnterDungeon(player.Id, gate.Index));
            Run(world, World.EnterSeconds + 0.1f);
            var dungeon = world.DungeonOf(gate);
            Assert.AreEqual(name, dungeon.Name);
            var layout = dungeon.Layout;
            var room = layout.Nodes.First(n => n.Letter == 'C');
            player.Position = Collision.Resolve(world.Map, room.Center + new Vec2(0, -room.Box.H / 2f + 1.5f), World.PlayerRadius);
            Run(world, 0.3f);
            var pack = world.Enemies.Where(e => !e.IsDead && room.Contains(e.Position, 2)).ToList();
            Assert.AreEqual(1, pack.Count(e => e.Definition.Size == MonsterSize.L), string.Join(", ", pack.Select(e => e.Definition.Id)));
            Assert.IsTrue(pack.All(e => e.Definition.Biome == biome || e.Definition.Biome == null), string.Join(", ", pack.Select(e => e.Definition.Id)));

            var bossRoom = layout.Room('B');
            player.Position = Collision.Resolve(world.Map, bossRoom.Center + new Vec2(0, -bossRoom.Box.H / 2f + 1.5f), World.PlayerRadius);
            Run(world, 0.3f);
            var master = world.Enemies.Single(e => e.Elite == EliteRank.Leader && world.DungeonAt(e.Position) == dungeon);
            Assert.AreEqual(boss, master.Definition.Id);
            Assert.AreEqual(bossName, World.EliteName(master.Definition, EliteRank.Leader, RiftAffix.None));
            Assert.IsTrue(World.IsDormant(master.Definition, master.State), "it stands dormant until the room wakes it");
            // The aqueduct's rooms are wet: its shallows were laid from the chunks.
            if (biome == Biome.Lake)
            {
                bool wet = false;
                for (int y = bossRoom.Box.Bottom; y < bossRoom.Box.Bottom + bossRoom.Box.H && !wet; y++)
                    for (int x = bossRoom.Box.Left; x < bossRoom.Box.Left + bossRoom.Box.W && !wet; x++) wet = world.Map[x, y] == TileType.ShallowWater;
                Assert.IsTrue(wet, "shallows in the master's room");
            }
        }

        // ---- Worlds: version 6 has the lairs and the shallows; version 5 is as it was made ----

        static ulong LairHash(GeneratedWorld world)
        {
            ulong hash = 14695981039346656037UL;
            foreach (var l in world.Rooms.Lairs) { hash ^= (ulong)l; hash *= 1099511628211UL; }
            foreach (var c in world.Rooms.Cellars) { hash ^= (ulong)(c.X * 4096 + c.Y); hash *= 1099511628211UL; }
            return hash;
        }

        // Version 5 worlds (made since B2) as the code of bb20dac laid them: their map, and their lairs and trapdoors.
        [TestCase(1UL, 7709216318012310603UL, 7046545737231749715UL)]
        [TestCase(2UL, 10640774155637039362UL, 10708562155261501298UL)]
        [TestCase(3UL, 14233214781456387143UL, 231375382477035725UL)]
        public void AWorldOfVersionFiveIsLaidAsBefore(ulong seed, ulong map, ulong lairs)
        {
            Assert.AreEqual(6, WorldGenerator.CrystalLakeVersion);
            Assert.Greater(WorldGenerator.CrystalLakeVersion, WorldGenerator.IceSandVersion);
            Assert.GreaterOrEqual(WorldGenerator.NewWorldVersion, WorldGenerator.CrystalLakeVersion);
            var world = WorldGenerator.Generate(seed, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped, Bosses.Shipped.Arenas, WorldGenerator.IceSandVersion,
                Tiles.Shipped.Terrain);
            Assert.AreEqual(map, RoomMapTests.Hash(world.Map));
            Assert.AreEqual(lairs, LairHash(world));
            Assert.IsFalse(world.Rooms.Lairs.Any(i => world.Rooms.Nodes[i].Biome == Biome.Crystal || world.Rooms.Nodes[i].Biome == Biome.Lake));
        }

        [Test]
        public void ANewWorldHasTheShallowsAndLairsOfTheGeodeGolemAndTheMossTurtle()
        {
            var lairBiomes = new HashSet<Biome>();
            WorldHost found = null;
            int foundLair = -1, shallows = 0;
            foreach (ulong seed in new ulong[] { 1, 2, 3 })
            {
                var five = WorldGenerator.Generate(seed, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped, Bosses.Shipped.Arenas, WorldGenerator.IceSandVersion,
                    Tiles.Shipped.Terrain);
                var host = new WorldHost(seed, Monsters.Shipped, Gear.Data, new HostConfig { MapSize = WorldGenerator.GameSize, MapVersion = WorldGenerator.CrystalLakeVersion },
                    new WorldConfig(), Bosses.Shipped, Classes.Shipped, Tiles.Shipped, story: Tales.Shipped);
                var rooms = host.Generated.Rooms;
                var map = host.Generated.Map;
                var biomes = host.Generated.Biomes;
                // Version 6 lays the lake's shallows on version 5's open floor (and the crystal cave's and the lake's looks
                // may stand props there too, before the camps' trapdoors, so a trapdoor may stand in another camp); every
                // other tile is version 5's.
                static bool Trapdoor(TileType type) => type == TileType.Trapdoor || type == TileType.OpenTrapdoor;
                for (int y = 0; y < map.Height; y++)
                {
                    for (int x = 0; x < map.Width; x++)
                    {
                        if (map[x, y] == TileType.ShallowWater)
                        {
                            shallows++;
                            Assert.AreEqual(Biome.Lake, biomes[x, y], "shallows only in the lake");
                        }
                        // The looks' things of the two biomes (B3a) may also stand in a pool: an islet, a sunken boat.
                        bool afloat = five.Map[x, y] == TileType.Water && map[x, y] != TileType.Floor
                            && (CrystalLakeProps.Is(map[x, y]) || map[x, y] == TileType.StructurePart || map[x, y] == TileType.SunkenBoat || map[x, y] == TileType.ShallowWater);
                        if (map[x, y] != five.Map[x, y] && !Trapdoor(map[x, y]) && !Trapdoor(five.Map[x, y]) && !afloat)
                            Assert.AreEqual(TileType.Floor, five.Map[x, y], $"only version 5's open floor changes ({x},{y}: {five.Map[x, y]} -> {map[x, y]})");
                    }
                }
                CollectionAssert.IsSubsetOf(five.Rooms.Lairs, rooms.Lairs, "version 5's lairs stay");
                foreach (int index in rooms.Lairs)
                {
                    var room = rooms.Nodes[index];
                    lairBiomes.Add(room.Biome);
                    if (found == null && (room.Biome == Biome.Crystal || room.Biome == Biome.Lake)) (found, foundLair) = (host, index);
                }
            }
            Assert.Greater(shallows, 50, "the lake's shallows");
            Assert.IsTrue(lairBiomes.Contains(Biome.Crystal), "a geode golem's lair");
            Assert.IsTrue(lairBiomes.Contains(Biome.Lake), "a moss turtle's lair");

            var world = found.World;
            var lair = found.Generated.Rooms.Nodes[foundLair];
            var player = world.AddPlayer("a", Gear.Wearing("obsidian_sword", "obsidian_armor"));
            player.Position = Collision.Resolve(world.Map, lair.Center + new Vec2(0, lair.Radius + 6), World.PlayerRadius);
            world.MonstersHold = true;
            Run(world, 1);
            var boss = world.Enemies.Single(e => !e.IsDead && e.Definition == world.LairBoss(lair.Biome));
            Assert.AreEqual(lair.Biome == Biome.Crystal ? "geode_golem" : "moss_turtle", boss.Definition.Id);
            Assert.AreEqual(EliteRank.Leader, boss.Elite);
            Assert.IsTrue(World.IsDormant(boss.Definition, boss.State));
        }
    }
}
