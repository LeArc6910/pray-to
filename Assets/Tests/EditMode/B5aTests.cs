using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // A party in a dungeon (2026-11-25, Docs/BiomeRework.md B5a): a room that wakes with two players inside has its
    // monsters as much tougher as World.DungeonHealthPerPlayer says (the master World.BigElitePerPlayer) and each of them
    // worth World.DungeonExperiencePerPlayer more to each player, beside the PackExtraPerPlayer more monsters the field
    // brings too; one player's run is as it was. The same seed makes the same dungeon for one and for two, so the two
    // runs are set side by side room by room.
    public class B5aTests
    {
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

        // A dungeon of that row entered by that many players at its gate, monsters held still.
        static (World World, DungeonInstance Dungeon, List<Player> Players) Enter(Biome biome, string id, int players)
        {
            var map = new TileMap(320, 40, TileType.Floor);
            for (int x = 0; x < map.Width; x++)
            {
                map[x, 0] = TileType.Wall;
                map[x, map.Height - 1] = TileType.Wall;
            }
            map.AddPocket(DungeonPocket.Rows);
            var world = new World(map, new Vec2(10.5f, 10.5f), Monsters.Shipped, 11, new WorldConfig { EnemyTarget = 0, ZoneLevel = 2 }, Gear.Data, Tiles.Shipped);
            var gate = world.AddGate(40, 20, biome);
            Assert.IsTrue(world.ChooseDungeon(gate.Index, id));
            world.MonstersHold = true;
            var party = new List<Player>();
            for (int i = 0; i < players; i++)
            {
                var player = world.AddPlayer("p" + i, Gear.Wearing("iron_sword"));
                player.Position = gate.Center;
                party.Add(player);
            }
            foreach (var player in party) Assert.IsTrue(world.EnterDungeon(player.Id, gate.Index));
            Run(world, World.EnterSeconds + 0.1f, () => Heal(party));
            return (world, world.DungeonOf(gate), party);
        }

        static void Heal(List<Player> party)
        {
            foreach (var p in party) p.Health = p.Stats.MaxHealth;
        }

        // Everyone at the near edge of the room, a moment for its pack to come: what came.
        static List<Enemy> Wake(World world, DungeonInstance dungeon, List<Player> party, DungeonRoom room)
        {
            foreach (var p in party) p.Position = Collision.Resolve(world.Map, room.Center + new Vec2(0, -room.Box.H / 2f + 1.5f), World.PlayerRadius);
            Run(world, 0.3f, () => Heal(party));
            Assert.IsTrue(dungeon.RoomWoken(room.Index), $"{room.Letter} woke");
            int pack = dungeon.PackIds[room.Index];
            return world.Enemies.Where(e => !e.IsDead && e.PackId == pack).ToList();
        }

        [Test]
        public void OnePlayersRoomsAreAsTheyWere()
        {
            Assert.AreEqual(1f, World.DungeonParty(World.DungeonHealthPerPlayer, 1));
            Assert.AreEqual(1f, World.DungeonParty(World.DungeonExperiencePerPlayer, 1));
            Assert.AreEqual(1f, World.DungeonParty(World.BigElitePerPlayer, 0), "nobody inside counts as one");
            Assert.AreEqual(1 + 2 * World.DungeonHealthPerPlayer, World.DungeonParty(World.DungeonHealthPerPlayer, 3), 1e-5);

            var (world, dungeon, party) = Enter(Biome.Cave, "abandoned_mine", 1);
            var room = dungeon.Layout.Nodes.First(n => n.Letter == 'C');
            var pack = Wake(world, dungeon, party, room);
            Assert.AreEqual(1, world.PackOf(pack[0]).Players);
            foreach (var e in pack)
            {
                float zone = e.Definition.Health * (1 + World.ZoneHealthStep * (dungeon.Level - 1));
                Assert.AreEqual(zone, e.MaxHealth, 1, $"{e.Definition.Id}: its zone's health, nothing more");
            }
        }

        [TestCase(Biome.Cave, "abandoned_mine")]
        [TestCase(Biome.Ice, "frozen_crypt")]
        public void TwoPlayersMeetTougherPacksThatGiveEachMore(Biome biome, string id)
        {
            var one = Enter(biome, id, 1);
            var two = Enter(biome, id, 2);
            CollectionAssert.AreEqual(one.Dungeon.Layout.Nodes.Select(n => n.Box), two.Dungeon.Layout.Nodes.Select(n => n.Box), "the same dungeon for one and two");
            var room = one.Dungeon.Layout.Nodes.First(n => n.Letter == 'C');
            var alone = Wake(one.World, one.Dungeon, one.Players, room);
            var together = Wake(two.World, two.Dungeon, two.Players, two.Dungeon.Layout.Nodes[room.Index]);
            Assert.AreEqual(2, two.World.PackOf(together[0]).Players);
            Assert.AreEqual(alone.Count + World.PackExtraPerPlayer, together.Count, "two more monsters, as in the field");
            float tougher = World.DungeonParty(World.DungeonHealthPerPlayer, 2);
            foreach (var e in alone)
                foreach (var f in together.Where(f => f.Definition == e.Definition))
                    Assert.AreEqual(e.MaxHealth * tougher, f.MaxHealth, 1, $"{e.Definition.Id}: tougher for two");

            // One of the same kind falls beside each: each of the two gets more than the one alone did.
            var kind = alone.First(e => !e.Definition.Big).Definition;
            int Felled(World world, List<Player> party, List<Enemy> pack)
            {
                int before = party[0].Experience;
                var victim = pack.First(e => e.Definition == kind);
                foreach (var p in party) p.Position = Collision.Resolve(world.Map, victim.Position + new Vec2(0, -1.5f), World.PlayerRadius);
                world.Hurt(victim, party[0], victim.MaxHealth * 100);
                Assert.IsTrue(victim.IsDead, $"{kind.Id} fell");
                Run(world, 0.2f, () => Heal(party));
                Assert.IsTrue(party.All(p => p.Experience - before == party[0].Experience - before), "each the same");
                return party[0].Experience - before;
            }
            int each = Felled(one.World, one.Players, alone);
            int eachOfTwo = Felled(two.World, two.Players, together);
            Assert.Greater(each, 0);
            Assert.AreEqual(each * World.DungeonParty(World.DungeonExperiencePerPlayer, 2), eachOfTwo, 1, $"{kind.Id}: worth more to each of two");
        }

        [Test]
        public void TwoPlayersMeetATougherMasterAndChampions()
        {
            var one = Enter(Biome.Mushroom, "spore_depths", 1);
            var two = Enter(Biome.Mushroom, "spore_depths", 2);
            foreach (char letter in new[] { 'M', 'B' })
            {
                var room = one.Dungeon.Layout.Room(letter);
                var alone = Wake(one.World, one.Dungeon, one.Players, room);
                var together = Wake(two.World, two.Dungeon, two.Players, two.Dungeon.Layout.Room(letter));
                if (letter == 'M')
                {
                    Assert.AreEqual(World.ChampionCount, together.Count, "as many champions");
                    for (int i = 0; i < alone.Count; i++)
                        Assert.AreEqual(alone[i].MaxHealth * World.DungeonParty(World.DungeonHealthPerPlayer, 2), together[i].MaxHealth, 1, "tougher champions");
                    continue;
                }
                var master = alone.Single(e => e.Id == one.Dungeon.BossId);
                var theirs = together.Single(e => e.Id == two.Dungeon.BossId);
                Assert.AreEqual("mycelium_mother", theirs.Definition.Id);
                Assert.AreEqual(master.Affixes, theirs.Affixes, "the same affixes");
                Assert.AreEqual(master.MaxHealth * World.DungeonParty(World.BigElitePerPlayer, 2), theirs.MaxHealth, 1, "the master for two");
                Assert.AreEqual(alone.Count + World.PackExtraPerPlayer, together.Count, "two more minions");
            }
        }
    }
}
