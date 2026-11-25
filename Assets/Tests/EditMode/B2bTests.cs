using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The ice cave and the sand cave's monsters, rules and dungeons (2026-11-24, Docs/BiomeRework.md B2b): six kinds of
    // their own each of several makes and sizes and no slimes to fill their packs; a charge that ends on ice slides on
    // and a wall on the ice dazes it; icicles fall by the walls of the ice halls while a fight is on, a second's shadow
    // before; a bomb scarab rolls slowly through quicksand; the mid-bosses stand dormant until roused; the sand cave's
    // buried lie under falling sand and come up if they cannot strike; the frozen crypt and the buried temple have them
    // for masters; a new world of version 5 has their lairs, and a world of version 4 is laid as 803d7a9 laid it.
    public class B2bTests
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

        // A walled room of the size, its floor of the given kind, every tile of the biome.
        static World Room(int size, Biome biome, TileType floor = TileType.Floor)
        {
            var map = Maps.Room(size);
            for (int y = 1; y < size - 1; y++)
                for (int x = 1; x < size - 1; x++) map[x, y] = floor;
            return new World(map, TileMap.Center(size / 2, size / 2), Shipped, 1, new WorldConfig { EnemyTarget = 0 }, biomes: BiomeMap.Of(size, size, (_, _) => biome));
        }

        // ---- The sheets ----

        static List<MonsterDefinition> KindsOf(Biome biome) => Shipped.Packs.Packs
            .Where(p => p.Place == null && p.Biome == biome && p.Weight > 0)
            .SelectMany(p => p.Members.Select(m => m.Monster)).Distinct().ToList();

        [TestCase(Biome.Ice, "frost_wolf", "yeti", "frost_beetle", "frost_lord")]
        [TestCase(Biome.Sand, "bomb_scarab", "great_worm", "sandstone_beetle", "scorpion_queen")]
        public void TheIceAndSandCavesHaveSixKindsOfTheirOwnAndNoSlimes(Biome biome, params string[] fresh)
        {
            var kinds = KindsOf(biome);
            foreach (var id in fresh) CollectionAssert.Contains(kinds.Select(k => k.Id), id, $"{biome}: its new {id} is in its packs");
            Assert.That(kinds.Count, Is.GreaterThanOrEqualTo(6), string.Join(", ", kinds.Select(k => k.Id)));
            Assert.IsTrue(kinds.All(k => k.Biome == biome), "each of its own: " + string.Join(", ", kinds.Where(k => k.Biome != biome).Select(k => k.Id)));
            Assert.That(kinds.Select(k => k.Role).Distinct().Count(), Is.GreaterThanOrEqualTo(3), "three makes at least");
            Assert.That(kinds.Select(k => k.Size).Distinct().Count(), Is.GreaterThanOrEqualTo(3), "three sizes at least");
            Assert.IsTrue(kinds.All(k => k.Size <= MonsterSize.L), "the field holds L at most");
            // The variant and the elite come from zone 3 at the earliest... the elite does: a camp's leader.
            Assert.IsTrue(Shipped.Packs.Packs.Where(p => p.Biome == biome && p.Zone < 3).All(p => p.Members.All(m => m.Monster.Id != fresh[3])), $"{fresh[3]} from zone 3");
        }

        [Test]
        public void TheirMidBossesAreXlDormantAndComeInNoPack()
        {
            foreach (var id in new[] { "frost_mammoth", "temple_guardian" })
            {
                var boss = Shipped.Find(id);
                Assert.AreEqual(MonsterSize.XL, boss.Size, id);
                Assert.AreEqual(0, boss.Weight, id);
                Assert.IsTrue(boss.Has(MonsterTraitKind.Dormant), id);
                Assert.IsFalse(Shipped.Packs.Packs.Any(p => p.Members.Any(m => m.Monster == boss)), id);
            }
            Assert.AreEqual(3f, Shipped.Find("frost_mammoth").Trait(MonsterTraitKind.Dormant).Number(0, 0), 1e-4f);
            Assert.IsTrue(MonsterSheet.Parse(File("dormant")).Success);
            StringAssert.Contains("dormant", string.Join("\n", MonsterSheet.Parse(File("dormant:20")).Errors));
        }

        static string File(string traits) =>
            "id,name,level,health,damage,armor,speed,radius,aggro,reach,windup,cooldown,weight,traits\n" + $"a,가,1,10,1,0,1,0.4,5,1,1,1,0,{traits}\n";

        // ---- Ice: a charge slides ----

        static Enemy Charge(World world, Vec2 from, Vec2 at)
        {
            var player = world.AddPlayer("a", null);
            player.Position = at;
            world.MonstersHold = true;
            var wolf = Spawn(world, "frost_wolf", from);
            Assert.IsTrue(world.ForceAttack(wolf, "lunge"));
            return wolf;
        }

        [Test]
        public void AWolfsChargeSlidesTwoTilesFurtherOnTheIce()
        {
            var floor = Room(40, Biome.Ice);
            var onFloor = Charge(floor, TileMap.Center(8, 20), TileMap.Center(12, 20));
            Run(floor, 1.6f);
            var ice = Room(40, Biome.Ice, TileType.IceFloor);
            var onIce = Charge(ice, TileMap.Center(8, 20), TileMap.Center(12, 20));
            var events = Run(ice, 1.6f);
            float plain = onFloor.Position.X - 8.5f, slid = onIce.Position.X - 8.5f;
            Assert.AreEqual(5, plain, 0.3f, "its marked length");
            Assert.AreEqual(plain + World.IceSlideTiles, slid, 0.3f, "two more on the ice");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxSlide && e.Source == onIce.Id));
            Assert.AreNotEqual(EnemyState.Stagger, onIce.State);
        }

        [Test]
        public void AWolfThatSlidesIntoAWallOnTheIceIsDazedButNotOnFloor()
        {
            var ice = Room(40, Biome.Ice, TileType.IceFloor);
            var wolf = Charge(ice, TileMap.Center(32, 20), TileMap.Center(36, 20));
            Assert.IsFalse(wolf.Definition.Has(MonsterTraitKind.Stagger), "a wolf has no stagger of its own");
            Run(ice, 0.7f + 0.6f);
            Assert.AreEqual(EnemyState.Stagger, wolf.State, "the ice took it into the wall");

            var floor = Room(40, Biome.Ice);
            var other = Charge(floor, TileMap.Center(32, 20), TileMap.Center(36, 20));
            Run(floor, 0.7f + 0.6f);
            Assert.AreNotEqual(EnemyState.Stagger, other.State, "on the floor it stops short of the wall");
        }

        // ---- Ice: icicles ----

        [Test]
        public void IciclesFallByTheWallsOfAnIceHallWhileAFightIsOn()
        {
            var world = Room(30, Biome.Ice);
            var player = world.AddPlayer("a", null);
            player.Position = TileMap.Center(2, 15);
            Assert.IsTrue(world.UnderIcicles(player.Position), "by the wall of a wide ice room");
            Assert.IsFalse(world.UnderIcicles(TileMap.Center(15, 15)), "not in the middle, far from rock");
            world.MonstersHold = true;
            Run(world, 8, () => player.Health = player.Stats.MaxHealth);
            Assert.IsFalse(world.Hazards.Any(h => h.Mark == Hazard.Icicle), "no fight, no icicles");

            var wolf = Spawn(world, "frost_wolf", TileMap.Center(8, 15));
            var seen = new List<Hazard>();
            Run(world, 14, () =>
            {
                player.Health = player.Stats.MaxHealth;
                player.Position = TileMap.Center(2, 15);
                (wolf.TargetId, wolf.State) = (player.Id, EnemyState.Chase);
                foreach (var h in world.Hazards.Where(h => h.Mark == Hazard.Icicle)) if (!seen.Contains(h)) seen.Add(h);
            });
            Assert.That(seen.Count, Is.InRange(2, (int)(14 / World.IcicleMin) + 1), "every few seconds");
            foreach (var icicle in seen)
            {
                Assert.AreEqual(World.Ticks(World.IcicleTelegraph), (int)(icicle.ResolveTick - icicle.StartTick), "a second's shadow");
                Assert.AreEqual(World.IcicleRadius, icicle.Size, 1e-4f);
                Assert.LessOrEqual(Vec2.Distance(icicle.Position, TileMap.Center(2, 15)), World.IcicleSpread + 1e-3f);
                Assert.IsFalse(world.Map.IsSolid(TileMap.Cell(icicle.Position.X), TileMap.Cell(icicle.Position.Y)));
            }

            // Not in another biome.
            var cave = Room(30, Biome.Cave);
            Assert.IsFalse(cave.UnderIcicles(TileMap.Center(2, 15)));
        }

        [Test]
        public void AnIcicleHurtsAndChillsWhoeverStaysUnderItAndAMonsterLuredThere()
        {
            var world = Room(30, Biome.Ice);
            var player = world.AddPlayer("a", null);
            player.Position = TileMap.Center(3, 15);
            world.MonstersHold = true;
            world.DropIcicle(player, player.Position);
            var events = Run(world, World.IcicleTelegraph + 0.1f);
            Assert.Greater(events.Where(e => e.Type == SimEventType.Hit && e.Target == player.Id).Sum(e => e.Amount), 0, "stayed under it");
            Assert.Greater(player.Afflictions.ChillTicks, 0, "chilled");

            // Out of its shadow in the second it gives: nothing. A walk of IcicleRadius + a body is well inside a second.
            var other = Room(30, Biome.Ice);
            var walker = other.AddPlayer("b", null);
            walker.Position = TileMap.Center(3, 15);
            other.MonstersHold = true;
            other.DropIcicle(walker, walker.Position);
            int steps = 0;
            var after = Run(other, World.IcicleTelegraph + 0.1f, () =>
            {
                if (steps++ < World.Ticks(0.5f)) walker.Position = PlayerMotor.Step(other.Map, walker.Position, new Vec2(1, 0), 5);
            });
            Assert.AreEqual(0, after.Count(e => e.Type == SimEventType.Hit && e.Target == walker.Id), "walked out on PC or phone alike");

            // A monster under it takes a share of its health; a big body less.
            var lure = Room(30, Biome.Ice);
            var hunter = lure.AddPlayer("c", null);
            lure.MonstersHold = true;
            var wolf = Spawn(lure, "frost_wolf", TileMap.Center(10, 15));
            var yeti = Spawn(lure, "yeti", TileMap.Center(20, 15));
            lure.DropIcicle(hunter, wolf.Position);
            lure.DropIcicle(hunter, yeti.Position);
            Run(lure, World.IcicleTelegraph + 0.1f);
            float wolfShare = 1 - wolf.Health / wolf.MaxHealth, yetiShare = 1 - yeti.Health / yeti.MaxHealth;
            Assert.That(wolfShare, Is.InRange(0.1f, World.IcicleMonsterShare + 1e-3f));
            Assert.Greater(yetiShare, 0);
            Assert.Less(yetiShare, wolfShare, "a big body takes less");
            Assert.AreEqual(2, lure.IciclesFallen);
        }

        // ---- Sand ----

        [Test]
        public void ABombScarabRollsSlowlyThroughQuicksand()
        {
            float Rolled(TileType floor)
            {
                var world = Room(30, Biome.Sand, floor);
                var player = world.AddPlayer("a", null);
                player.Position = TileMap.Center(20, 15);
                world.MonstersHold = true;
                var scarab = Spawn(world, "bomb_scarab", TileMap.Center(10, 15));
                Assert.IsTrue(world.ForceAttack(scarab, "roll"));
                Run(world, 0.6f);
                return scarab.Position.X - 10.5f;
            }
            float sand = Rolled(TileType.Floor), mire = Rolled(TileType.Quicksand);
            Assert.Greater(sand, 1);
            Assert.AreEqual(PlayerMotor.QuicksandSlow, mire / sand, 0.05f);
        }

        [Test]
        public void TheBuriedOfTheSandCaveLieUnderFallingSand()
        {
            var sand = Room(30, Biome.Sand);
            sand.AddPlayer("a", null).Position = TileMap.Center(2, 2);
            var scorpion = Spawn(sand, "sand_scorpion", TileMap.Center(20, 20));
            Run(sand, 0.2f);
            Assert.IsTrue(scorpion.Buried, "lying in wait");
            Assert.IsTrue(sand.UnderFallingSand(scorpion));

            var cave = Room(30, Biome.Cave);
            cave.AddPlayer("a", null).Position = TileMap.Center(2, 2);
            var other = Spawn(cave, "sand_scorpion", TileMap.Center(20, 20));
            Run(cave, 0.2f);
            Assert.IsTrue(other.Buried);
            Assert.IsFalse(cave.UnderFallingSand(other), "only in the sand cave");
            Assert.IsFalse(World.UnderFallingSand(sand.Biomes, scorpion.Position, buried: false));
        }

        [Test]
        public void OneUnderTheGroundThatCannotStrikeComesUpAWhile()
        {
            // A wall between: no lob over it, no way to come up beside the player.
            var map = Maps.FromRows(
                "####################",
                "#.........#........#",
                "#.........#........#",
                "#.........#........#",
                "#.........#........#",
                "#.........#........#",
                "####################");
            var world = new World(map, TileMap.Center(2, 3), Shipped, 1, new WorldConfig { EnemyTarget = 0 }, biomes: BiomeMap.Of(20, 7, (_, _) => Biome.Sand));
            var player = world.AddPlayer("a", null);
            player.Position = TileMap.Center(13, 3);
            var worm = Spawn(world, "sand_worm", TileMap.Center(7, 3));
            (worm.TargetId, worm.State) = (player.Id, EnemyState.Chase);
            Run(world, 0.5f);
            Assert.IsTrue(worm.Buried);
            var events = Run(world, World.BurrowedMostSeconds + 0.5f, () => player.Health = player.Stats.MaxHealth);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxSurface && e.Source == worm.Id));
            Assert.IsFalse(worm.Buried, "up to be struck");
        }

        // ---- Dormant mid-bosses ----

        [Test]
        public void ATempleGuardianStandsDormantUntilSomeoneComesRightUpOrStrikesIt()
        {
            var world = Room(30, Biome.Sand);
            var player = world.AddPlayer("a", Gear.Wearing("iron_sword"));
            player.Position = TileMap.Center(15, 8);
            var guardian = Spawn(world, "temple_guardian", TileMap.Center(15, 15));
            Run(world, 1);
            Assert.AreEqual(EnemyState.Idle, guardian.State, "seven tiles off: a statue");
            Assert.IsTrue(World.IsDormant(guardian.Definition, guardian.State));
            player.Position = TileMap.Center(15, 12) + new Vec2(0, 0.6f);
            var events = Run(world, 0.5f);
            Assert.AreNotEqual(EnemyState.Idle, guardian.State, "within three tiles it wakes");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxWake && e.Source == guardian.Id));

            var other = Room(30, Biome.Ice);
            var archer = other.AddPlayer("b", Gear.Wearing("iron_sword"));
            archer.Position = TileMap.Center(15, 6);
            var mammoth = Spawn(other, "frost_mammoth", TileMap.Center(15, 15));
            Run(other, 0.5f);
            Assert.AreEqual(EnemyState.Idle, mammoth.State);
            other.Hurt(mammoth, archer, 10);
            Run(other, 0.2f);
            Assert.AreNotEqual(EnemyState.Idle, mammoth.State, "struck from afar, it wakes");
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
            var world = new World(map, new Vec2(10.5f, 10.5f), Monsters.Shipped, 11, new WorldConfig { EnemyTarget = 0, ZoneLevel = 3 }, Gear.Data, Tiles.Shipped);
            gate = world.AddGate(40, 20, biome);
            Assert.IsTrue(world.ChooseDungeon(gate.Index, dungeon));
            return world;
        }

        [TestCase(Biome.Ice, "frozen_crypt", "얼어붙은 묘", "frost_mammoth", "서리 매머드", "yeti", 'i')]
        [TestCase(Biome.Sand, "buried_temple", "묻힌 신전", "temple_guardian", "신전 수호 석상", "great_worm", 'q')]
        public void TheCryptAndTheTempleHaveTheirMidBossesForMastersAndABigBodyInEachPackRoom(Biome biome, string id, string name, string boss, string bossName,
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
            Assert.IsTrue(chunks.Any(c => c.Rows.Any(r => r.IndexOf(floor) >= 0)), $"its floor ('{floor}')");

            var world = Hand(biome, out var gate, id);
            world.MonstersHold = true;
            var player = world.AddPlayer("a", Gear.Wearing("iron_sword"));
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
            Assert.IsTrue(pack.Any(e => e.Definition.Id == big) || pack.Any(e => e.Definition.Big));

            var bossRoom = layout.Room('B');
            player.Position = Collision.Resolve(world.Map, bossRoom.Center + new Vec2(0, -bossRoom.Box.H / 2f + 1.5f), World.PlayerRadius);
            Run(world, 0.3f);
            var master = world.Enemies.Single(e => e.Elite == EliteRank.Leader && world.DungeonAt(e.Position) == dungeon);
            Assert.AreEqual(boss, master.Definition.Id);
            Assert.AreEqual(World.MasterAffixes, Enumerable.Range(0, 8).Count(i => ((int)master.Affixes & 1 << i) != 0));
            Assert.AreEqual(bossName, World.EliteName(master.Definition, EliteRank.Leader, RiftAffix.None));
            Assert.IsTrue(World.IsDormant(master.Definition, master.State), "it stands dormant until the room wakes it");
        }

        // ---- Worlds: version 5 has the lairs; version 4 is as it was made ----

        // Version 4 worlds (made since B1c) as the code of 803d7a9 laid them: their map, and their lairs and trapdoors.
        [TestCase(1UL, 9814433732961777148UL, 7198821043363096466UL)]
        [TestCase(2UL, 7208613334697104811UL, 12362688262614983531UL)]
        [TestCase(3UL, 13829938329100446942UL, 5129030307403003578UL)]
        public void AWorldOfVersionFourIsLaidAsBefore(ulong seed, ulong map, ulong lairs)
        {
            Assert.Greater(WorldGenerator.IceSandVersion, WorldGenerator.LandmarkVersion);
            // A new world is of version 5 or after (B3b: version 6).
            Assert.GreaterOrEqual(WorldGenerator.NewWorldVersion, WorldGenerator.IceSandVersion);
            var world = WorldGenerator.Generate(seed, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped, Bosses.Shipped.Arenas, WorldGenerator.LandmarkVersion,
                Tiles.Shipped.Terrain);
            Assert.AreEqual(map, RoomMapTests.Hash(world.Map));
            ulong hash = 14695981039346656037UL;
            foreach (var l in world.Rooms.Lairs) { hash ^= (ulong)l; hash *= 1099511628211UL; }
            foreach (var c in world.Rooms.Cellars) { hash ^= (ulong)(c.X * 4096 + c.Y); hash *= 1099511628211UL; }
            Assert.AreEqual(lairs, hash);
            Assert.IsTrue(world.Rooms.Lairs.All(i => world.Rooms.Nodes[i].Biome == Biome.Cave || world.Rooms.Nodes[i].Biome == Biome.Mushroom));
        }

        [Test]
        public void ANewWorldHasLairsOfTheFrostMammothAndTheTempleGuardianToo()
        {
            var lairBiomes = new HashSet<Biome>();
            WorldHost found = null;
            int foundLair = -1;
            foreach (ulong seed in new ulong[] { 1, 2, 3 })
            {
                var four = WorldGenerator.Generate(seed, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped, Bosses.Shipped.Arenas, WorldGenerator.LandmarkVersion,
                    Tiles.Shipped.Terrain);
                var host = new WorldHost(seed, Monsters.Shipped, Gear.Data, new HostConfig { MapSize = WorldGenerator.GameSize, MapVersion = WorldGenerator.IceSandVersion },
                    new WorldConfig(), Bosses.Shipped, Classes.Shipped, Tiles.Shipped, story: Tales.Shipped);
                var rooms = host.Generated.Rooms;
                // Version 5 also stands the ice cave's and the sand cave's props and landmarks (B2a) on version 4's open
                // floor, before the camps' trapdoors are laid (so a trapdoor may stand in another camp); every other tile
                // is version 4's.
                var map = host.Generated.Map;
                static bool Trapdoor(TileType type) => type == TileType.Trapdoor || type == TileType.OpenTrapdoor;
                for (int y = 0; y < map.Height; y++)
                    for (int x = 0; x < map.Width; x++)
                        if (map[x, y] != four.Map[x, y] && !Trapdoor(map[x, y]) && !Trapdoor(four.Map[x, y]))
                            Assert.AreEqual(TileType.Floor, four.Map[x, y], $"only version 4's open floor changes ({x},{y}: {four.Map[x, y]} -> {map[x, y]})");
                CollectionAssert.IsSubsetOf(four.Rooms.Lairs, rooms.Lairs, "version 4's lairs stay");
                foreach (int index in rooms.Lairs)
                {
                    var room = rooms.Nodes[index];
                    lairBiomes.Add(room.Biome);
                    if (found == null && (room.Biome == Biome.Ice || room.Biome == Biome.Sand)) (found, foundLair) = (host, index);
                }
            }
            Assert.IsTrue(lairBiomes.Contains(Biome.Ice), "a frost mammoth's lair");
            Assert.IsTrue(lairBiomes.Contains(Biome.Sand), "a temple guardian's lair");

            // Its mid-boss stands dormant there, the leader of its lair under its own name.
            var world = found.World;
            var lair = found.Generated.Rooms.Nodes[foundLair];
            var player = world.AddPlayer("a", Gear.Wearing("obsidian_sword", "obsidian_armor"));
            player.Position = Collision.Resolve(world.Map, lair.Center + new Vec2(0, lair.Radius + 6), World.PlayerRadius);
            world.MonstersHold = true;
            Run(world, 1);
            var boss = world.Enemies.Single(e => !e.IsDead && e.Definition == world.LairBoss(lair.Biome));
            Assert.AreEqual(lair.Biome == Biome.Ice ? "frost_mammoth" : "temple_guardian", boss.Definition.Id);
            Assert.AreEqual(EliteRank.Leader, boss.Elite);
            Assert.IsTrue(World.IsDormant(boss.Definition, boss.State));
        }
    }
}
