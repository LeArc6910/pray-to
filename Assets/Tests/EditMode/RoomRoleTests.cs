using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // What the rooms of the generator's version 2 are for (Docs/WorldRework.md 방의 역할, 방 조각 시트): the roles'
    // numbers and spacing, the chunks of rooms.csv, the new tiles, and the floors that change a walk.
    public class RoomRoleTests
    {
        const int Size = WorldGenerator.GameSize;

        static readonly Dictionary<ulong, GeneratedWorld> Made = new Dictionary<ulong, GeneratedWorld>();

        static GeneratedWorld Rooms(ulong seed)
        {
            if (!Made.TryGetValue(seed, out var world))
                Made[seed] = world = WorldGenerator.Generate(seed, Size, Size, Tales.Shipped, Bosses.Staged.Arenas, WorldGenerator.RoomVersion, Tiles.Shipped.Terrain);
            return world;
        }

        static IEnumerable<RoomNode> Beside(RoomGraph rooms, RoomNode room) =>
            room.Edges.Select(e => rooms.Nodes[rooms.Edges[e].A == room.Index ? rooms.Edges[e].B : rooms.Edges[e].A]);

        // Rooms from one room to the nearest other that is wanted, up to a limit; limit + 1 when none is that near.
        static int RoomsTo(RoomGraph rooms, RoomNode from, int limit, Func<RoomNode, bool> wanted)
        {
            var seen = new HashSet<int> { from.Index };
            var ring = new List<RoomNode> { from };
            for (int step = 1; step <= limit; step++)
            {
                ring = ring.SelectMany(r => Beside(rooms, r)).Where(r => seen.Add(r.Index)).ToList();
                if (ring.Any(wanted)) return step;
            }
            return limit + 1;
        }

        // Every open tile walked to from the spawn; through: the walls a pickaxe opens are walked through too.
        static bool[] Walk(GeneratedWorld world, bool through)
        {
            var map = world.Map;
            var seen = new bool[map.Width * map.Height];
            var stack = new Stack<int>();
            seen[world.SpawnY * map.Width + world.SpawnX] = true;
            stack.Push(world.SpawnY * map.Width + world.SpawnX);
            while (stack.Count > 0)
            {
                int at = stack.Pop();
                int x = at % map.Width, y = at / map.Width;
                foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (!map.InBounds(nx, ny) || seen[ny * map.Width + nx] || map[nx, ny] == TileType.Lava) continue;
                    if (map.IsSolid(nx, ny) && !(through && TileMap.IsRoomWall(map[nx, ny]))) continue;
                    seen[ny * map.Width + nx] = true;
                    stack.Push(ny * map.Width + nx);
                }
            }
            return seen;
        }

        [Test]
        public void TheRoomSheetsReadAndHaveAChunkForEveryRole()
        {
            var rooms = Tiles.Shipped.Terrain.Rooms;
            Assert.IsTrue(rooms.Success, string.Join("\n", rooms.Errors));
            Assert.GreaterOrEqual(rooms.Chunks.Count, 30, "chunks in rooms.csv");
            foreach (RoomRole role in Enum.GetValues(typeof(RoomRole)))
            {
                if (role == RoomRole.Plain) continue;
                // One that every biome can use, so no biome is left without the role.
                Assert.IsTrue(rooms.Chunks.Any(c => c.Role == role && c.Biomes.Count == 0) || role == RoomRole.Locked, $"{role}: a chunk for any biome");
                foreach (Biome biome in Enum.GetValues(typeof(Biome))) Assert.IsTrue(rooms.For(role, biome).Any(), $"{role} in {biome}");
            }
            // Each biome has rooms drawn for it alone.
            foreach (Biome biome in Enum.GetValues(typeof(Biome)))
                Assert.IsTrue(rooms.Chunks.Any(c => c.Biomes.Count > 0 && c.For(biome)), $"{biome}: a chunk of its own");
            foreach (var chunk in rooms.Chunks)
            {
                Assert.That(chunk.Width, Is.InRange(RoomSheet.MinSize, RoomSheet.MaxSize), chunk.Id);
                Assert.That(chunk.Height, Is.InRange(RoomSheet.MinSize, RoomSheet.MaxSize), chunk.Id);
                // Turned any way it is the same letters.
                string letters = string.Concat(chunk.Rows.SelectMany(r => r).OrderBy(c => c));
                for (int way = 0; way < 8; way++)
                {
                    var (w, h) = chunk.Size(way);
                    var turned = new List<char>();
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                            turned.Add(chunk.At(x, y, way));
                    Assert.AreEqual(letters, string.Concat(turned.OrderBy(c => c)), $"{chunk.Id} turned {way}");
                }
                // The eight ways are eight different drawings unless the chunk is the same mirrored or turned.
                Assert.AreEqual(chunk.Rows[0][0], chunk.At(0, 0, 0));
                Assert.AreEqual(chunk.Rows[0][chunk.Width - 1], chunk.At(0, 0, 4), $"{chunk.Id} mirrored");
                Assert.AreEqual(chunk.Rows[chunk.Height - 1][chunk.Width - 1], chunk.At(0, 0, 2), $"{chunk.Id} turned half round");
                // Something shut in is shut by a wall that is dug.
                if (chunk.Role == RoomRole.Hidden) Assert.IsTrue(chunk.Rows.Any(r => r.Contains('X')), $"{chunk.Id}: a cracked wall");
                if (chunk.Role == RoomRole.Locked) Assert.IsTrue(chunk.Rows.Any(r => r.Contains('O') || r.Contains('V')) && chunk.Rows.Any(r => r.Contains('G')), $"{chunk.Id}: a lock and a grate");
            }
            // The roles' numbers: the sheet's rows over the code's.
            Assert.AreEqual(7, rooms.Rule(Biome.Mushroom, RoomRole.Hidden).Per100);
            Assert.AreEqual(4, rooms.Rule(Biome.Cave, RoomRole.Hidden).Per100);
            Assert.AreEqual(6, rooms.Rule(Biome.Cave, RoomRole.Rest).MinGap);
            Assert.AreEqual(100, RoomSheet.Defaults.Where(r => r.Role != RoomRole.Gate).Sum(r => r.Per100), "the roles' shares of a hundred rooms");
        }

        [Test]
        public void TheRoomSheetSaysWhatIsWrong()
        {
            const string header = "id,role,biomes,where,weight,turn,rows\n";
            RoomData Read(string rows) => RoomSheet.Parse(header + rows);
            Assert.IsTrue(Read("a,pack,,field,10,yes,?.?/.M./?.?\n").Success);
            Assert.IsFalse(RoomSheet.Parse("id,role\na,pack\n").Success, "columns missing");
            Assert.IsFalse(Read("a,boss,,field,10,yes,?.?/.M./?.?\n").Success, "no such role");
            Assert.IsFalse(Read("a,pack,swamp,field,10,yes,?.?/.M./?.?\n").Success, "no such biome");
            Assert.IsFalse(Read("a,pack,,field,10,yes,?.?/.M/?.?\n").Success, "rows of different lengths");
            Assert.IsFalse(Read("a,pack,,field,10,yes,?.?/.Z./?.?\n").Success, "a letter it does not know");
            Assert.IsFalse(Read("a,pack,,field,10,yes,#####/#.+.#/#####\n").Success, "a door inside");
            Assert.IsFalse(Read("a,pack,,field,10,yes,+###/#..#/####\n").Success, "a door on a corner");
            Assert.IsFalse(Read("a,treasure,,field,10,yes,#####/#C#.+/#####\n").Success, "a chest no door leads to");
            Assert.IsFalse(Read("a,pack,,field,10,yes,..\n").Success, "too small");
            Assert.IsFalse(Read("a,pack,,field,10,yes,?.?/.M./?.?\na,pack,,field,10,yes,?.?/.M./?.?\n").Success, "an id twice");
            // Behind a wall that is dug counts as led to.
            var hidden = Read("a,hidden,cave|ice,,5,no,#####/#C..#/##X##/##+##\n");
            Assert.IsTrue(hidden.Success, string.Join("\n", hidden.Errors));
            var chunk = hidden.Chunks[0];
            Assert.IsTrue(chunk.Walled);
            Assert.IsFalse(chunk.Turn);
            Assert.IsTrue(chunk.For(Biome.Ice) && !chunk.For(Biome.Lava));
            Assert.IsTrue(chunk.Field && chunk.Dungeon, "no where: both");

            Assert.IsFalse(RoomSheet.Parse("", "biome,role,per_100,min_gap,where\n,rest,400,6,loop\n").Success, "more than a hundred in a hundred");
            var roles = RoomSheet.Parse("", "biome,role,per_100,min_gap,where\n,rest,9,2,any\nice,rest,1,,\n");
            Assert.IsTrue(roles.Success, string.Join("\n", roles.Errors));
            Assert.AreEqual(9, roles.Rule(Biome.Cave, RoomRole.Rest).Per100);
            Assert.AreEqual(1, roles.Rule(Biome.Ice, RoomRole.Rest).Per100);
            Assert.AreEqual(6, roles.Rule(Biome.Ice, RoomRole.Rest).MinGap, "a blank is the code's number");
            Assert.AreSame(RoomData.None, RoomSheet.Parse(""));
        }

        [Test]
        public void EveryRoleHasItsShareOfTheRoomsAndItsDistance()
        {
            for (ulong seed = 1; seed <= 3; seed++)
            {
                var rooms = Rooms(seed).Rooms;
                var plain = rooms.Nodes.Where(n => n.Kind == RoomKind.Room).ToList();
                int count = plain.Count;
                double Share(RoomRole role) => 100.0 * plain.Count(n => n.Role == role) / count;
                // Of a hundred rooms (Docs/WorldRework.md 방의 역할): the design's numbers, give or take.
                Assert.That(Share(RoomRole.Pack), Is.InRange(34, 50), $"seed {seed}: pack");
                // The roads run through rooms along the ways (1c), and those stay plain but for a camp, a shrine or a cart.
                Assert.That(Share(RoomRole.Plain), Is.InRange(12, 29), $"seed {seed}: plain");
                Assert.That(Share(RoomRole.Ore), Is.InRange(7, 14), $"seed {seed}: ore");
                Assert.That(Share(RoomRole.Rest), Is.InRange(2.5, 5), $"seed {seed}: rest");
                Assert.That(Share(RoomRole.Elite), Is.InRange(2, 4), $"seed {seed}: elite");
                Assert.That(Share(RoomRole.Treasure), Is.InRange(3.5, 6.5), $"seed {seed}: treasure");
                Assert.That(Share(RoomRole.Shrine), Is.InRange(2, 5), $"seed {seed}: shrine");
                Assert.That(Share(RoomRole.Hidden), Is.InRange(3, 6.5), $"seed {seed}: hidden");
                Assert.That(Share(RoomRole.Locked), Is.InRange(2, 4), $"seed {seed}: locked");
                Assert.That(Share(RoomRole.Trap), Is.InRange(1, 3), $"seed {seed}: trap");
                Assert.That(Share(RoomRole.Cart), Is.InRange(1, 3), $"seed {seed}: cart");
                Assert.That(Share(RoomRole.Pond), Is.InRange(1.5, 5), $"seed {seed}: pond");
                // A dungeon's gate in each biome.
                foreach (Biome biome in Enum.GetValues(typeof(Biome)))
                    Assert.AreEqual(1, plain.Count(n => n.Biome == biome && n.Role == RoomRole.Gate), $"seed {seed}: the gate of {biome}");

                foreach (var room in plain)
                {
                    var role = room.Role;
                    string where = $"seed {seed}: the {role} room at ({room.X}, {room.Y})";
                    // Apart: no two camps, leaders, locks, traps or carts within their distance (RoomSheet.Defaults).
                    if (role == RoomRole.Rest || role == RoomRole.Elite || role == RoomRole.Locked || role == RoomRole.Trap || role == RoomRole.Cart)
                    {
                        int gap = Tiles.Shipped.Terrain.Rooms.Rule(room.Biome, role).MinGap;
                        Assert.Greater(RoomsTo(rooms, room, gap, n => n.Role == role), gap, where + ": another too near");
                    }
                    // By the spawn it is quiet, and nothing dangerous near it.
                    if (room.Depth <= 1) Assert.AreEqual(RoomRole.Plain, role, where + " beside the spawn");
                    if (room.Depth <= 3) Assert.IsFalse(role == RoomRole.Elite || role == RoomRole.Trap, where + " near the spawn");
                    // No monsters where a road runs (Docs/Design.md).
                    if (room.Roadside)
                        Assert.IsTrue(role == RoomRole.Plain || role == RoomRole.Rest || role == RoomRole.Cart || role == RoomRole.Shrine || role == RoomRole.Ore || role == RoomRole.Pond,
                            where + " on a road");
                }
                // A treasure has a pack or a leader beside it or one room further (but where roads and towns leave no room for one).
                var treasures = plain.Where(n => n.Role == RoomRole.Treasure).ToList();
                int guarded = treasures.Count(t => RoomsTo(rooms, t, 2, n => n.Role == RoomRole.Pack || n.Role == RoomRole.Elite) <= 2);
                Assert.GreaterOrEqual(guarded, treasures.Count * 9 / 10, $"seed {seed}: treasures with a pack near");

                // Something that is not one more cave room within three rooms of nearly every room.
                int bare = plain.Count(n => (n.Role == RoomRole.Plain || n.Role == RoomRole.Pack)
                    && RoomsTo(rooms, n, WorldGenerator.SpecialWithin, o => o.Kind != RoomKind.Room || o.Role != RoomRole.Plain && o.Role != RoomRole.Pack) > WorldGenerator.SpecialWithin);
                Assert.LessOrEqual(bare, count / 100, $"seed {seed}: rooms with nothing but plain rooms and packs within {WorldGenerator.SpecialWithin} rooms");
                Assert.AreEqual(0, rooms.Nodes.Count(n => n.Kind != RoomKind.Room && n.Role != RoomRole.Plain), "the spawn's room and the arenas have no role");
            }
        }

        [Test]
        public void TheChunksAreSetIntoTheRoomsAsDrawn()
        {
            var world = Rooms(2);
            var rooms = world.Rooms;
            var map = world.Map;
            var set = rooms.Nodes.Where(n => n.Chunk != null).ToList();
            Assert.GreaterOrEqual(set.Count, 300, "rooms with a chunk");
            Assert.GreaterOrEqual(set.Select(n => n.Chunk.Id).Distinct().Count(), 40, "different chunks used");
            Assert.IsTrue(set.Select(n => n.ChunkWay).Distinct().Count() == 8, "turned all eight ways");
            foreach (var room in set)
            {
                Assert.AreEqual(room.Chunk.Role, room.Role, $"the chunk {room.Chunk.Id} in a {room.Role} room");
                Assert.IsTrue(room.Chunk.For(room.Biome), $"{room.Chunk.Id} in {room.Biome}");
            }
            // Every role that is more than a cave room is drawn: it has its chunk (ore rooms are their veins).
            foreach (var room in rooms.Nodes.Where(n => n.Role != RoomRole.Plain && n.Role != RoomRole.Pack && n.Role != RoomRole.Ore))
                Assert.IsNotNull(room.Chunk, $"the {room.Role} room at ({room.X}, {room.Y})");
            // What the chunks marked stands where they say.
            Assert.GreaterOrEqual(rooms.Marks.Count(m => m.Kind == RoomMarkKind.Monster), 800, "places for a pack's monsters");
            foreach (var mark in rooms.Marks)
            {
                var type = map[mark.X, mark.Y];
                var room = rooms.Nodes[mark.Room];
                string where = $"{mark.Kind} of {room.Chunk.Id} at ({mark.X}, {mark.Y})";
                switch (mark.Kind)
                {
                    case RoomMarkKind.Monster:
                    case RoomMarkKind.Elite: Assert.IsFalse(map.IsSolid(mark.X, mark.Y), where); break;
                    // A chest is wood, iron or gold by its row of pois.csv (묶음 3a).
                    case RoomMarkKind.Chest: Assert.That(type, Is.EqualTo(TileType.WoodChest).Or.EqualTo(TileType.IronChest).Or.EqualTo(TileType.GoldChest), where); break;
                    case RoomMarkKind.Cart: Assert.AreEqual(TileType.MineCart, type, where); break;
                    case RoomMarkKind.Campfire: Assert.AreEqual(TileType.Campfire, type, where); break;
                    case RoomMarkKind.Shrine: Assert.AreEqual(TileType.RuneStone, type, where); break;
                    case RoomMarkKind.Trap: Assert.AreEqual(TileType.RuneFloor, type, where); break;
                }
            }
            foreach (var kind in new[] { RoomMarkKind.Elite, RoomMarkKind.Chest, RoomMarkKind.Campfire, RoomMarkKind.Shrine, RoomMarkKind.Trap, RoomMarkKind.DungeonDoor })
                Assert.IsTrue(rooms.Marks.Any(m => m.Kind == kind), $"{kind} marked somewhere");
            // The new tiles are all in the world.
            var counts = new Dictionary<TileType, int>();
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    counts[map[x, y]] = counts.GetValueOrDefault(map[x, y]) + 1;
            foreach (var type in new[] { TileType.CrackedWall, TileType.Grate, TileType.ObsidianWall, TileType.VoidWall, TileType.IceFloor, TileType.Quicksand })
                Assert.Greater(counts.GetValueOrDefault(type), 20, $"{type} tiles");
            // Ice only in the ice caves and quicksand only in the sand caves, but for a chunk at a border.
            int strayIce = 0, straySand = 0;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (map[x, y] == TileType.IceFloor && world.Biomes[x, y] != Biome.Ice) strayIce++;
                    if (map[x, y] == TileType.Quicksand && world.Biomes[x, y] != Biome.Sand) straySand++;
                }
            }
            Assert.Less(strayIce, counts[TileType.IceFloor] / 10);
            Assert.Less(straySand, counts[TileType.Quicksand] / 10);
            // Ways shut by a cracked wall are short cuts, never the only way: every room is reached by the others.
            var cracked = rooms.Edges.Where(e => e.Kind == RoomEdgeKind.Cracked).ToList();
            Assert.GreaterOrEqual(cracked.Count, 10, "ways shut by a cracked wall");
            var reached = new HashSet<int> { rooms.Nodes.First(n => n.Kind == RoomKind.Spawn).Index };
            var open = new Stack<int>(reached);
            while (open.Count > 0)
            {
                var node = rooms.Nodes[open.Pop()];
                foreach (int way in node.Edges)
                {
                    var edge = rooms.Edges[way];
                    if (edge.Kind == RoomEdgeKind.Cracked) continue;
                    int next = edge.A == node.Index ? edge.B : edge.A;
                    if (reached.Add(next)) open.Push(next);
                }
            }
            Assert.AreEqual(rooms.Nodes.Count, reached.Count, "rooms reached only through a cracked wall");
        }

        [Test]
        public void EveryRoleRoomIsWalkedToAndTheShutOnesOnlyThroughTheirWall()
        {
            for (ulong seed = 1; seed <= 4; seed++)
            {
                var world = Rooms(seed);
                var rooms = world.Rooms;
                var walked = Walk(world, through: false);
                var dug = Walk(world, through: true);
                bool At(bool[] seen, int x, int y) => seen[y * Size + x];
                bool Beside(bool[] seen, int x, int y) => At(seen, x, y) || At(seen, x + 1, y) || At(seen, x - 1, y) || At(seen, x, y + 1) || At(seen, x, y - 1);
                int shut = 0;
                foreach (var room in rooms.Nodes)
                {
                    string where = $"seed {seed}: the {room.Biome} {room.Role} room at ({room.X}, {room.Y})";
                    // A place of the story may stand on a room with no role; every room with one is walked to.
                    if (room.Role != RoomRole.Plain || room.Chunk != null) Assert.IsTrue(At(walked, room.AnchorX, room.AnchorY), where);
                    if (room.Role == RoomRole.Hidden || room.Role == RoomRole.Locked)
                    {
                        Assert.IsNotNull(room.Inside, where + ": its inside");
                        var (ix, iy) = room.Inside.Value;
                        Assert.IsFalse(At(walked, ix, iy), where + " is open");
                        Assert.IsTrue(At(dug, ix, iy), where + " is not come to even by digging");
                        shut++;
                    }
                    else Assert.IsNull(room.Inside, where);
                }
                Assert.Greater(shut, 40, $"seed {seed}: rooms behind a wall");
                foreach (var mark in rooms.Marks)
                {
                    var room = rooms.Nodes[mark.Room];
                    bool behind = room.Role == RoomRole.Hidden || room.Role == RoomRole.Locked;
                    Assert.IsTrue(Beside(behind ? dug : walked, mark.X, mark.Y), $"seed {seed}: {mark.Kind} of {room.Chunk.Id} at ({mark.X}, {mark.Y})");
                    if (behind) Assert.IsFalse(Beside(walked, mark.X, mark.Y), $"seed {seed}: {mark.Kind} of {room.Chunk.Id} at ({mark.X}, {mark.Y}) is not shut in");
                }
            }
        }

        [Test]
        public void TheSameSeedAndSheetsGiveTheSameRoles()
        {
            GeneratedWorld Make(TerrainData terrain) => WorldGenerator.Generate(9, 256, 256, Tales.Shipped, Bosses.Staged.Arenas, WorldGenerator.RoomVersion, terrain);
            var a = Make(Tiles.Shipped.Terrain).Rooms;
            var b = Make(Tiles.Shipped.Terrain).Rooms;
            CollectionAssert.AreEqual(a.Nodes.Select(n => (n.Role, n.Chunk?.Id, n.ChunkWay, n.AnchorX, n.AnchorY)).ToList(),
                b.Nodes.Select(n => (n.Role, n.Chunk?.Id, n.ChunkWay, n.AnchorX, n.AnchorY)).ToList());
            CollectionAssert.AreEqual(a.Marks.Select(m => (m.Room, m.Kind, m.X, m.Y)).ToList(), b.Marks.Select(m => (m.Room, m.Kind, m.X, m.Y)).ToList());
            Assert.IsTrue(a.Nodes.Any(n => n.Chunk != null), "small maps take chunks too");
            // Without the room sheets the rooms still get roles, by the code's numbers, and no chunks.
            var bare = Make(TerrainSheet.Default).Rooms;
            Assert.IsTrue(bare.Nodes.All(n => n.Chunk == null));
            Assert.IsTrue(bare.Nodes.Any(n => n.Role == RoomRole.Pack) && bare.Nodes.Any(n => n.Role == RoomRole.Ore));
            Assert.AreEqual(0, bare.Marks.Count);
        }

        [Test]
        public void TheNewTilesAreWalledWalkedAndDugByTheirRules()
        {
            var map = new TileMap(8, 8, TileType.Floor);
            foreach (var wall in new[] { TileType.CrackedWall, TileType.Grate, TileType.ObsidianWall, TileType.VoidWall })
            {
                map[3, 3] = wall;
                Assert.IsTrue(map.IsSolid(3, 3), $"{wall} is solid");
            }
            foreach (var floor in new[] { TileType.IceFloor, TileType.Quicksand })
            {
                map[3, 3] = floor;
                Assert.IsFalse(map.IsSolid(3, 3), $"{floor} is walked on");
                Assert.IsFalse(TileMap.IsGround(floor), $"{floor} is not built on");
                Assert.IsNull(Tiles.Shipped.Find(floor), $"{floor} is not dug");
            }
            Assert.IsNull(Tiles.Shipped.Find(TileType.Grate), "a grate is not dug");
            Assert.AreEqual(0, Tiles.Shipped.Find(TileType.CrackedWall).Pickaxe, "any pickaxe");
            Assert.Less(Tiles.Shipped.Find(TileType.CrackedWall).Hardness, Tiles.Shipped.Find(TileType.Wall).Hardness);
            Assert.AreEqual(Tiles.Shipped.Find(TileType.ObsidianOre).Pickaxe, Tiles.Shipped.Find(TileType.ObsidianWall).Pickaxe);
            Assert.AreEqual(Tiles.Shipped.Find(TileType.VoidOre).Pickaxe, Tiles.Shipped.Find(TileType.VoidWall).Pickaxe);
        }

        // ---- Floors that change a walk (PlayerMotor, Docs/Combat.md 바닥) ----

        const float Speed = 5;

        static TileMap Rink(TileType floor)
        {
            var map = new TileMap(64, 64, floor);
            for (int i = 0; i < 64; i++) map[i, 0] = map[i, 63] = map[0, i] = map[63, i] = TileType.Wall;
            return map;
        }

        [Test]
        public void IceKeepsAPlayerSlidingButTheStickStillSteers()
        {
            var ice = Rink(TileType.IceFloor);
            var at = TileMap.Center(10, 32);
            var velocity = Vec2.Zero;
            var east = new Vec2(1, 0);

            // From rest to a run: a third of a second.
            int ticks = 0;
            while (velocity.Length < Speed - 0.01f && ticks < 200)
            {
                at = PlayerMotor.Step(ice, at, ref velocity, east, Speed);
                ticks++;
            }
            Assert.That(ticks * World.TickSeconds, Is.InRange(0.2f, 0.3f), "seconds from rest to full speed on ice");
            Assert.LessOrEqual(velocity.Length, Speed + 1e-3f, "a slide is never faster than a walk");

            // The stick let go: four tenths of a second and a tile to a stop.
            var from = at;
            ticks = 0;
            while (velocity.Length > 0 && ticks < 200)
            {
                at = PlayerMotor.Step(ice, at, ref velocity, Vec2.Zero, Speed);
                ticks++;
            }
            Assert.That(ticks * World.TickSeconds, Is.InRange(0.35f, 0.45f), "seconds sliding after the stick is let go");
            Assert.That(Vec2.Distance(from, at), Is.InRange(0.8f, 1.2f), "tiles slid after the stick is let go");

            // Turned right round at a run: half a second to be running the other way, two thirds of a tile past.
            for (int i = 0; i < 30; i++) at = PlayerMotor.Step(ice, at, ref velocity, east, Speed);
            from = at;
            float furthest = at.X;
            ticks = 0;
            while (velocity.X > -Speed + 0.01f && ticks < 200)
            {
                at = PlayerMotor.Step(ice, at, ref velocity, new Vec2(-1, 0), Speed);
                furthest = Math.Max(furthest, at.X);
                ticks++;
            }
            Assert.That(ticks * World.TickSeconds, Is.InRange(0.45f, 0.55f), "seconds to turn right round on ice");
            Assert.That(furthest - from.X, Is.InRange(0.5f, 0.75f), "tiles carried on past where the stick was turned");

            // A quarter turn at a run: carried less than a tile wide of the new line.
            velocity = east * Speed;
            from = at;
            for (int i = 0; i < 30; i++) at = PlayerMotor.Step(ice, at, ref velocity, new Vec2(0, 1), Speed);
            Assert.That(at.X - from.X, Is.InRange(0.7f, 1.0f), "tiles carried wide at a quarter turn");
            Assert.AreEqual(0, velocity.X, 0.01f);

            // Off the ice a player stops where the stick stops, and the step that keeps no speed never slides.
            var floor = Rink(TileType.Floor);
            velocity = Vec2.Zero;
            at = TileMap.Center(10, 32);
            for (int i = 0; i < 10; i++) at = PlayerMotor.Step(floor, at, ref velocity, east, Speed);
            var stopped = PlayerMotor.Step(floor, at, ref velocity, Vec2.Zero, Speed);
            Assert.AreEqual(at, stopped);
            Assert.AreEqual(PlayerMotor.Step(floor, at, east, Speed), PlayerMotor.Step(floor, at, ref velocity, east, Speed), "on plain floor the two steps are one");

            // A wall takes the speed it stops.
            velocity = east * Speed;
            at = new Vec2(62.4f, 32.5f);
            for (int i = 0; i < 20; i++) at = PlayerMotor.Step(ice, at, ref velocity, east, Speed);
            Assert.AreEqual(0, velocity.Length, 0.01f);
            Assert.Less(at.X, 63 - World.PlayerRadius + 0.01f);
        }

        [Test]
        public void QuicksandSlowsAWalk()
        {
            var sand = Rink(TileType.Quicksand);
            var floor = Rink(TileType.Floor);
            var a = TileMap.Center(10, 32);
            var b = a;
            var c = a;
            var velocity = Vec2.Zero;
            for (int i = 0; i < 40; i++)
            {
                a = PlayerMotor.Step(sand, a, new Vec2(1, 0), Speed);
                b = PlayerMotor.Step(floor, b, new Vec2(1, 0), Speed);
                c = PlayerMotor.Step(sand, c, ref velocity, new Vec2(1, 0), Speed);
            }
            Assert.AreEqual(PlayerMotor.QuicksandSlow, (a.X - 10.5f) / (b.X - 10.5f), 0.01f);
            Assert.AreEqual(a, c, "with speed kept or not");
            Assert.AreEqual(0.55f, PlayerMotor.QuicksandSlow, 1e-6f, "Docs/WorldRework.md: 걸음 55%");
        }

        // The client slides by itself and tells the host where it is; the host takes every step (a slide is never
        // faster than a walk) and the two stay on the same tile of ice to the hair.
        [Test]
        public void TheHostTakesEveryStepOfASlideTheClientMakes()
        {
            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, null, Classes.Shipped, Tiles.Shipped);
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped);
            host.Receive(1, client.JoinMessage());
            void Deliver()
            {
                foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            }
            Deliver();
            var player = host.World.FindPlayer(client.PlayerId);
            // A rink round the player on both maps, as the same seed would lay it on both.
            int px = TileMap.Cell(player.Position.X), py = TileMap.Cell(player.Position.Y);
            for (int y = py - 6; y <= py + 6; y++)
            {
                for (int x = px - 6; x <= px + 12; x++)
                {
                    var type = x == px + 12 ? TileType.Wall : TileType.IceFloor;
                    host.World.Map[x, y] = type;
                    client.Map[x, y] = type;
                }
            }
            var inputs = new List<Vec2>();
            inputs.AddRange(Enumerable.Repeat(new Vec2(1, 0), 20));
            inputs.AddRange(Enumerable.Repeat(Vec2.Zero, 20));
            inputs.AddRange(Enumerable.Repeat(new Vec2(0, 1), 15));
            inputs.AddRange(Enumerable.Repeat(new Vec2(-1, -1), 25));
            inputs.AddRange(Enumerable.Repeat(new Vec2(1, 0), 90));
            inputs.AddRange(Enumerable.Repeat(Vec2.Zero, 20));
            bool slid = false;
            for (int i = 0; i < inputs.Count; i++)
            {
                var before = client.Position;
                host.Receive(1, client.Step(inputs[i], Vec2.Zero, false));
                host.Tick();
                Deliver();
                Assert.AreEqual(client.Position, player.Position, $"tick {i}");
                if (inputs[i].LengthSquared == 0 && Vec2.Distance(before, client.Position) > 0.01f) slid = true;
            }
            Assert.IsTrue(slid, "the client slid with the stick let go");
            Assert.AreEqual(0, player.Corrections, "the host never put the player back");
            Assert.AreEqual(0, client.Velocity.Length, 0.01f, "stopped against the wall at the rink's end");
            Assert.Greater(client.Position.X, px + 10, "and got there");

            // A roll that ends on ice slides on.
            host.Receive(1, client.Step(new Vec2(-1, 0), Vec2.Zero, false, roll: true));
            host.Tick();
            Deliver();
            for (int i = 0; i < World.RollTicks + 12; i++)
            {
                var before = client.Position;
                host.Receive(1, client.Step(Vec2.Zero, Vec2.Zero, false));
                host.Tick();
                Deliver();
                if (i == World.RollTicks + 2) Assert.Greater(Vec2.Distance(before, client.Position), 0.01f, "still sliding after the roll");
            }
            Assert.AreEqual(client.Position, player.Position);
            Assert.AreEqual(0, player.Corrections);
        }
    }
}
