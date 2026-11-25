using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The lava fields' and the void abyss's monsters, rules and dungeons (2026-11-25, Docs/BiomeRework.md B4b): six kinds
    // of their own each, of several makes and sizes, no slimes in their packs (the abyss's star slime in their place) and
    // the abyss stalker in its packs; a splitter comes apart into shards and the shards into motes; a steam vent hisses a
    // second and bursts on its own beat, burning whoever is over it and scalding monsters; a cracked plate gives way
    // under a player who stands on it a second, they fall (a tenth of their health, back on firm ground) and the plate is
    // back later; the ember forge and the broken gate have their mid-bosses for masters and the deep hollow is gone; a new
    // world of version 7 has the vents, the plates and the two biomes' lairs, and a world of version 6 is laid as 48cb129
    // laid it.
    public class B4bTests
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

        [TestCase(Biome.Lava, "salamander", "magma_hurler", "obsidian_beetle", "forge_slime")]
        [TestCase(Biome.Abyss, "void_eye", "void_splitter", "star_slime", "shadow_stalker")]
        public void TheLavaFieldsAndTheAbyssHaveSixKindsOfTheirOwnAndNoSlimes(Biome biome, params string[] fresh)
        {
            var kinds = KindsOf(biome);
            foreach (var id in fresh) CollectionAssert.Contains(kinds.Select(k => k.Id), id, $"{biome}: its new {id} is in its packs");
            Assert.That(kinds.Count, Is.GreaterThanOrEqualTo(6), string.Join(", ", kinds.Select(k => k.Id)));
            Assert.IsTrue(kinds.All(k => k.Biome == biome), "each of its own: " + string.Join(", ", kinds.Where(k => k.Biome != biome).Select(k => k.Id)));
            Assert.That(kinds.Select(k => k.Role).Distinct().Count(), Is.GreaterThanOrEqualTo(3), "three makes at least");
            Assert.That(kinds.Select(k => k.Size).Distinct().Count(), Is.GreaterThanOrEqualTo(3), "three sizes at least");
            Assert.IsTrue(kinds.All(k => k.Size <= MonsterSize.L), "the field holds L at most");
            Assert.IsTrue(Shipped.Packs.Packs.Where(p => p.Biome == biome && p.Zone < 3).All(p => p.Members.All(m => m.Monster.Id != fresh[3])), $"{fresh[3]} (the elite) from zone 3");
            Assert.IsFalse(Shipped.Packs.Packs.Where(p => p.Biome == biome).Any(p => p.Members.Any(m => m.Monster.Id == "slime")), "no slimes");
            if (biome == Biome.Abyss) CollectionAssert.Contains(kinds.Select(k => k.Id), "abyss_stalker", "the stalker comes in its packs now");
        }

        [Test]
        public void TheirMidBossesAreXlDormantAndComeInNoPackAndTheSplitterComesApartInTwoSteps()
        {
            foreach (var id in new[] { "forge_keeper", "gate_warden" })
            {
                var boss = Shipped.Find(id);
                Assert.AreEqual(MonsterSize.XL, boss.Size, id);
                Assert.AreEqual(0, boss.Weight, id);
                Assert.IsTrue(boss.Has(MonsterTraitKind.Dormant), id);
                Assert.IsFalse(Shipped.Packs.Packs.Any(p => p.Members.Any(m => m.Monster == boss)), id);
            }
            var splitter = Shipped.Find("void_splitter");
            Assert.AreEqual(MonsterSize.L, splitter.Size);
            var shard = Shipped.Find(splitter.Trait(MonsterTraitKind.Split).Arguments[1]);
            Assert.AreEqual(("void_shard", MonsterSize.M), (shard.Id, shard.Size));
            var mote = Shipped.Find(shard.Trait(MonsterTraitKind.Split).Arguments[1]);
            Assert.AreEqual(("void_mote", MonsterSize.XS), (mote.Id, mote.Size));

            var world = Room(30, Biome.Abyss);
            var player = world.AddPlayer("a", Gear.Wearing("obsidian_sword"));
            player.Position = TileMap.Center(5, 15);
            world.MonstersHold = true;
            var big = Spawn(world, "void_splitter", TileMap.Center(15, 15));
            world.Hurt(big, player, big.MaxHealth * 4);
            Run(world, 0.3f);
            var halves = world.Enemies.Where(e => !e.IsDead && e.Definition.Id == "void_shard").ToList();
            Assert.AreEqual(2, halves.Count, "two shards");
            world.Hurt(halves[0], player, halves[0].MaxHealth * 4);
            Run(world, 0.3f);
            Assert.AreEqual(2, world.Enemies.Count(e => !e.IsDead && e.Definition.Id == "void_mote"), "and a shard two motes");
        }

        // ---- The lava fields: steam vents ----

        // A room of the lava fields with a vent at (12, 15) and a player on it.
        static (World World, Player Player) Vented()
        {
            var world = Room(30, Biome.Lava, map => map[12, 15] = TileType.SteamVent);
            var player = world.AddPlayer("a", null);
            player.Position = TileMap.Center(12, 15);
            return (world, player);
        }

        [Test]
        public void AVentIsWalkedOverAndHissesASecondThenBurstsOnItsOwnBeat()
        {
            Assert.IsFalse(Maps.Room(4).IsSolid(1, 1));
            var (world, player) = Vented();
            Assert.IsFalse(world.Map.IsSolid(12, 15), "walked over");
            Assert.AreEqual(TileType.SteamVent, RoomSheet.Tile('v'));
            // Its beat is a hash of its tile: neighbours do not puff together.
            var phases = Enumerable.Range(0, 8).Select(i => World.VentPhase(10 + i, 20)).Distinct().Count();
            Assert.GreaterOrEqual(phases, 6);

            Hazard mark = null;
            long started = -1;
            int health0 = 0;
            Run(world, World.VentPeriod + 0.2f, () =>
            {
                player.Health = player.Stats.MaxHealth;
                if (mark == null && world.Hazards.FirstOrDefault(h => h.Mark == Hazard.Steam) is Hazard h)
                {
                    (mark, started) = (h, world.Tick);
                    health0 = (int)player.Health;
                }
            });
            Assert.IsNotNull(mark, "it hissed within its beat");
            Assert.AreEqual(World.Ticks(World.VentTelegraph), mark.ResolveTick - mark.StartTick, "a second's warning");
            Assert.AreEqual(World.VentRadius, mark.Size, 1e-4f);

            // Standing on it through a beat: burnt once a burst.
            var (again, standing) = Vented();
            int burns = 0;
            var events = Run(again, World.VentPeriod * 2 + 0.1f, () => standing.Health = standing.Stats.MaxHealth);
            burns = events.Count(e => e.Type == SimEventType.Hit && e.Target == standing.Id && e.Source == 0);
            Assert.AreEqual(2, again.VentsBurst, "two bursts in two beats");
            Assert.AreEqual(2, burns, "one burn a burst");
            Assert.AreEqual(2, again.VentBurns);
        }

        [Test]
        public void ABurstBurnsWhoeverStepsInWhileItLastsButNotOneAStepAway()
        {
            var (world, player) = Vented();
            player.Position = TileMap.Center(15, 15);
            var hazard = world.PuffVent(12, 15);
            Run(world, World.VentTelegraph + 0.1f, () => player.Health = player.Stats.MaxHealth);
            Assert.AreEqual(0, world.VentBurns, "three tiles off");
            // Into the burst while it lasts.
            player.Position = TileMap.Center(12, 15);
            var events = Run(world, 0.2f);
            Assert.AreEqual(1, world.VentBurns);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Hit && e.Target == player.Id));
            // After it is over, nothing.
            Run(world, World.VentBurst);
            int before = world.VentBurns;
            player.Position = TileMap.Center(12, 15);
            Run(world, 0.2f);
            Assert.AreEqual(before, world.VentBurns);
            Assert.IsNotNull(hazard);
        }

        [Test]
        public void ABurstScaldsTheMonstersOverItTheLavasOwnLess()
        {
            var world = Room(30, Biome.Lava, map =>
            {
                map[10, 15] = TileType.SteamVent;
                map[20, 15] = TileType.SteamVent;
            });
            var player = world.AddPlayer("a", null);
            player.Position = TileMap.Center(15, 5);
            world.MonstersHold = true;
            var own = Spawn(world, "salamander", TileMap.Center(10, 15));
            var stranger = Spawn(world, "star_slime", TileMap.Center(20, 15));
            world.PuffVent(10, 15);
            world.PuffVent(20, 15);
            Run(world, World.VentTelegraph + World.VentBurst, () => player.Health = player.Stats.MaxHealth);
            float ownShare = 1 - own.Health / own.MaxHealth, strangerShare = 1 - stranger.Health / stranger.MaxHealth;
            Assert.Greater(strangerShare, 0.05f, "a stranger to the fields is scalded");
            Assert.Greater(ownShare, 0, "the fields' own a little");
            Assert.Less(ownShare, strangerShare * 0.5f, "fire resistance");
        }

        // ---- The abyss: cracked plates ----

        static (World World, Player Player) Plated()
        {
            var world = Room(30, Biome.Abyss, map =>
            {
                for (int x = 12; x <= 16; x++) map[x, 15] = TileType.CrackedPlate;
            });
            var player = world.AddPlayer("a", null);
            player.Position = TileMap.Center(12, 15);
            return (world, player);
        }

        [Test]
        public void APlateHoldsASecondOfStandingAndAWalkAcrossIt()
        {
            var (world, player) = Plated();
            Assert.IsFalse(world.Map.IsSolid(12, 15));
            Assert.AreEqual(TileType.CrackedPlate, RoomSheet.Tile('d'));
            Run(world, World.PlateHold - 0.15f);
            Assert.AreEqual(TileType.CrackedPlate, world.Map[12, 15], "not yet");
            Assert.Greater(world.PlateStood(12, 15), 0.7f);
            // A step off and back starts it again.
            player.Position = TileMap.Center(12, 14);
            Run(world, 0.1f);
            Assert.AreEqual(0, world.PlateStood(12, 15));
            player.Position = TileMap.Center(12, 15);
            Run(world, World.PlateHold - 0.15f);
            Assert.AreEqual(TileType.CrackedPlate, world.Map[12, 15]);
            // Off, then walking across, a tile every quarter second.
            player.Position = TileMap.Center(11, 15);
            Run(world, 0.1f);
            for (int x = 12; x <= 16; x++)
            {
                player.Position = TileMap.Center(x, 15);
                Run(world, 0.25f);
            }
            Assert.AreEqual(0, world.PlatesBroken);
        }

        [Test]
        public void APlateStoodOnTooLongGivesWayAndTheyFallAndItIsBackLater()
        {
            var (world, player) = Plated();
            player.Position = TileMap.Center(14, 15);
            float full = player.Stats.MaxHealth;
            var events = Run(world, World.PlateHold + 0.1f);
            Assert.AreEqual(TileType.BrokenPlate, world.Map[14, 15]);
            Assert.IsTrue(world.Map.IsSolid(14, 15), "a hole is not walked");
            Assert.AreEqual(1, world.PlateFalls);
            Assert.AreEqual(full * (1 - World.FallShare), player.Health, 1.01f, "a tenth of their health");
            var under = world.Map[TileMap.Cell(player.Position.X), TileMap.Cell(player.Position.Y)];
            Assert.AreEqual(TileType.Floor, under, "up on firm ground, not a plate");
            Assert.Less(Vec2.Distance(player.Position, TileMap.Center(14, 15)), 2.5f, "the nearest");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxCrumble));
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxFall && e.Source == player.Id));
            Assert.Greater(player.Corrections, 0, "its client is told where they are");
            Assert.AreEqual(World.PlateBack, world.PlateBackIn(14, 15), 0.2f);
            Run(world, World.PlateBack + 0.1f);
            Assert.AreEqual(TileType.CrackedPlate, world.Map[14, 15], "the plate is back");
        }

        [Test]
        public void AHoleKeptInASaveComesBackToo()
        {
            var (world, player) = Plated();
            world.Build(15, 15, TileType.BrokenPlate);
            player.Position = TileMap.Center(12, 13);
            Run(world, 2);
            Assert.Greater(world.PlateBackIn(15, 15), 0, "given its time when a player came near");
            Run(world, World.PlateBack);
            Assert.AreEqual(TileType.CrackedPlate, world.Map[15, 15]);
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

        [TestCase(Biome.Lava, "ember_forge", "잿불 대장간", "forge_keeper", "잿불 대장장이", 'v')]
        [TestCase(Biome.Abyss, "broken_gate", "깨진 문 너머", "gate_warden", "공허의 문지기", 'd')]
        public void TheForgeAndTheBrokenGateHaveTheirMidBossesForMastersAndABigBodyInEachPackRoom(Biome biome, string id, string name, string boss, string bossName, char floor)
        {
            var row = Sheet.For(biome);
            Assert.AreEqual(id, row.Id);
            Assert.AreEqual(name, row.Name);
            Assert.IsTrue(row.Master);
            Assert.AreEqual(boss, row.Boss);
            Assert.AreEqual(7, row.Light.Length, "its gate room's light");
            Assert.Less(row.Experience, 100, "its chest gives the rest");
            Assert.IsNull(Sheet.Find("deep_hollow"), "every biome has its own: the deep hollow is gone");
            var chunks = Tiles.Shipped.Terrain.Rooms.Chunks.Where(c => c.Dungeon && c.Biomes.Contains(biome)).ToList();
            Assert.GreaterOrEqual(chunks.Count(c => c.Role == RoomRole.Pack), 3, "pack rooms of its own");
            Assert.GreaterOrEqual(chunks.Count(c => c.Role == RoomRole.Elite), 2, "a champions' room and a master's");
            Assert.IsTrue(chunks.Any(c => c.Rows.Any(r => r.IndexOf(floor) >= 0)), $"its own ('{floor}')");

            var world = Hand(biome, out var gate, id);
            world.MonstersHold = true;
            var player = world.AddPlayer("a", Gear.Wearing("obsidian_sword"));
            player.Position = gate.Center;
            Assert.IsTrue(world.EnterDungeon(player.Id, gate.Index));
            Run(world, World.EnterSeconds + 0.1f, () => player.Health = player.Stats.MaxHealth);
            var dungeon = world.DungeonOf(gate);
            Assert.AreEqual(name, dungeon.Name);
            var layout = dungeon.Layout;
            var room = layout.Nodes.First(n => n.Letter == 'C');
            player.Position = Collision.Resolve(world.Map, room.Center + new Vec2(0, -room.Box.H / 2f + 1.5f), World.PlayerRadius);
            Run(world, 0.3f, () => player.Health = player.Stats.MaxHealth);
            var pack = world.Enemies.Where(e => !e.IsDead && room.Contains(e.Position, 2)).ToList();
            Assert.AreEqual(1, pack.Count(e => e.Definition.Size == MonsterSize.L), string.Join(", ", pack.Select(e => e.Definition.Id)));
            Assert.IsTrue(pack.All(e => e.Definition.Biome == biome || e.Definition.Biome == null), string.Join(", ", pack.Select(e => e.Definition.Id)));

            // The rooms hold the biome's rule: vents in the forge, plates beyond the broken gate.
            var tile = biome == Biome.Lava ? TileType.SteamVent : TileType.CrackedPlate;
            int laid = 0;
            foreach (var node in layout.Nodes)
                for (int y = node.Box.Bottom; y < node.Box.Bottom + node.Box.H; y++)
                    for (int x = node.Box.Left; x < node.Box.Left + node.Box.W; x++)
                        if (world.Map[x, y] == tile) laid++;
            Assert.Greater(laid, 4, $"{tile} in its rooms");

            var bossRoom = layout.Room('B');
            player.Position = Collision.Resolve(world.Map, bossRoom.Center + new Vec2(0, -bossRoom.Box.H / 2f + 1.5f), World.PlayerRadius);
            Run(world, 0.3f, () => player.Health = player.Stats.MaxHealth);
            var master = world.Enemies.Single(e => e.Elite == EliteRank.Leader && world.DungeonAt(e.Position) == dungeon);
            Assert.AreEqual(boss, master.Definition.Id);
            Assert.AreEqual(bossName, World.EliteName(master.Definition, EliteRank.Leader, RiftAffix.None));
            Assert.IsTrue(World.IsDormant(master.Definition, master.State), "it stands dormant until the room wakes it");
        }

        // ---- Worlds: version 7 has the vents, the plates and the lairs; version 6 is as it was made ----

        static ulong LairHash(GeneratedWorld world)
        {
            ulong hash = 14695981039346656037UL;
            foreach (var l in world.Rooms.Lairs) { hash ^= (ulong)l; hash *= 1099511628211UL; }
            foreach (var c in world.Rooms.Cellars) { hash ^= (ulong)(c.X * 4096 + c.Y); hash *= 1099511628211UL; }
            return hash;
        }

        // Version 6 worlds (made since B3) as the code of 48cb129 laid them: their map, and their lairs and trapdoors.
        [TestCase(1UL, 17067767590909561504UL, 5515376707211511576UL)]
        [TestCase(2UL, 16905194744493567389UL, 534658694319747927UL)]
        [TestCase(3UL, 7475259576216890331UL, 13285007131332725121UL)]
        public void AWorldOfVersionSixIsLaidAsBefore(ulong seed, ulong map, ulong lairs)
        {
            Assert.AreEqual(7, WorldGenerator.LavaVoidVersion);
            Assert.Greater(WorldGenerator.LavaVoidVersion, WorldGenerator.CrystalLakeVersion);
            Assert.GreaterOrEqual(WorldGenerator.NewWorldVersion, WorldGenerator.LavaVoidVersion);
            var world = WorldGenerator.Generate(seed, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped, Bosses.Shipped.Arenas, WorldGenerator.CrystalLakeVersion,
                Tiles.Shipped.Terrain);
            Assert.AreEqual(map, RoomMapTests.Hash(world.Map));
            Assert.AreEqual(lairs, LairHash(world));
            Assert.IsFalse(world.Rooms.Lairs.Any(i => world.Rooms.Nodes[i].Biome == Biome.Lava || world.Rooms.Nodes[i].Biome == Biome.Abyss));
        }

        [Test]
        public void ANewWorldHasTheVentsThePlatesAndLairsOfTheEmberSmithAndTheGateWarden()
        {
            var lairBiomes = new HashSet<Biome>();
            WorldHost found = null;
            int foundLair = -1, vents = 0, plates = 0;
            static bool Trapdoor(TileType type) => type == TileType.Trapdoor || type == TileType.OpenTrapdoor;
            foreach (ulong seed in new ulong[] { 1, 2, 3 })
            {
                var six = WorldGenerator.Generate(seed, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped, Bosses.Shipped.Arenas, WorldGenerator.CrystalLakeVersion,
                    Tiles.Shipped.Terrain);
                var host = new WorldHost(seed, Monsters.Shipped, Gear.Data, new HostConfig { MapSize = WorldGenerator.GameSize, MapVersion = WorldGenerator.LavaVoidVersion },
                    new WorldConfig(), Bosses.Shipped, Classes.Shipped, Tiles.Shipped, story: Tales.Shipped);
                var rooms = host.Generated.Rooms;
                var map = host.Generated.Map;
                var biomes = host.Generated.Biomes;
                for (int y = 0; y < map.Height; y++)
                {
                    for (int x = 0; x < map.Width; x++)
                    {
                        if (map[x, y] == TileType.SteamVent)
                        {
                            vents++;
                            Assert.AreEqual(Biome.Lava, biomes[x, y], "vents only in the lava fields");
                        }
                        if (map[x, y] == TileType.CrackedPlate)
                        {
                            plates++;
                            Assert.AreEqual(Biome.Abyss, biomes[x, y], "plates only in the abyss");
                        }
                        // Version 7 lays its vents and plates on version 6's open floor (and the two biomes' looks may
                        // stand their things on its floor or its lava, before the camps' trapdoors, so a trapdoor may
                        // stand in another camp); every other tile is version 6's.
                        if (map[x, y] != six.Map[x, y] && !Trapdoor(map[x, y]) && !Trapdoor(six.Map[x, y]))
                            Assert.IsTrue(six.Map[x, y] == TileType.Floor || six.Map[x, y] == TileType.Lava && (biomes[x, y] == Biome.Lava || biomes[x, y] == Biome.Abyss),
                                $"only version 6's open floor changes ({x},{y}: {six.Map[x, y]} -> {map[x, y]})");
                    }
                }
                CollectionAssert.IsSubsetOf(six.Rooms.Lairs, rooms.Lairs, "version 6's lairs stay");
                foreach (int index in rooms.Lairs)
                {
                    var room = rooms.Nodes[index];
                    lairBiomes.Add(room.Biome);
                    if (found == null && (room.Biome == Biome.Lava || room.Biome == Biome.Abyss)) (found, foundLair) = (host, index);
                }
            }
            Assert.Greater(vents, 30, "the lava fields' vents");
            Assert.Greater(plates, 60, "the abyss's plates");
            Assert.IsTrue(lairBiomes.Contains(Biome.Lava), "an ember smith's lair");
            Assert.IsTrue(lairBiomes.Contains(Biome.Abyss), "a gate warden's lair");

            var world = found.World;
            var lair = found.Generated.Rooms.Nodes[foundLair];
            var player = world.AddPlayer("a", Gear.Wearing("obsidian_sword", "obsidian_armor"));
            player.Position = Collision.Resolve(world.Map, lair.Center + new Vec2(0, lair.Radius + 6), World.PlayerRadius);
            world.MonstersHold = true;
            Run(world, 1);
            var boss = world.Enemies.Single(e => !e.IsDead && e.Definition == world.LairBoss(lair.Biome));
            Assert.AreEqual(lair.Biome == Biome.Lava ? "forge_keeper" : "gate_warden", boss.Definition.Id);
            Assert.AreEqual(EliteRank.Leader, boss.Elite);
            Assert.IsTrue(World.IsDormant(boss.Definition, boss.State));
        }
    }
}
