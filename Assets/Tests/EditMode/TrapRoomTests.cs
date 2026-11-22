using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The trap rooms' ambush (2026-11-22, Docs/WorldRework.md 함정 방, 묶음 4b): the chest shuts the room with grates and
    // two bands come one after the other; won, the grates lift and the chest gives a tier higher; lost, they lift with
    // the chest shut until dawn; a save in the middle keeps the floor, not the grates; the clients are told.
    public class TrapRoomTests
    {
        const int Size = 512;

        static HostConfig RoomConfig => new HostConfig { MapSize = Size, MapVersion = WorldGenerator.RoomVersion };

        static WorldHost Host(ulong seed) => new WorldHost(seed, Monsters.Shipped, Gear.Data, RoomConfig, new WorldConfig(), Bosses.Shipped, Classes.Shipped,
            Tiles.Shipped, story: Tales.Shipped);

        static void Run(World world, float seconds)
        {
            for (int i = 0; i < World.Ticks(seconds); i++) world.Step();
        }

        static Vec2? OpenBeside(World world, Poi poi)
        {
            for (float reach = 1; reach <= World.PoiReach; reach += 0.25f)
                for (int k = 0; k < 32; k++)
                {
                    var at = poi.Center + Vec2.FromAngle(2 * MathF.PI * k / 32) * reach;
                    if (!Collision.Overlaps(world.Map, at, World.PlayerRadius)) return at;
                }
            return null;
        }

        // A world with a trap chest that can be stood beside, a strong player beside it, the monsters held.
        static (WorldHost Host, Poi Chest, Player Player) AtTrap(ulong seed = 3, string name = "a")
        {
            for (ulong s = seed; s < seed + 6; s++)
            {
                var host = Host(s);
                var world = host.World;
                var chest = world.Pois.Where(p => p.Kind == PoiKind.Chest && p.Definition.Role == RoomRole.Trap).OrderBy(p => p.Index)
                    .FirstOrDefault(p => OpenBeside(world, p) != null);
                if (chest == null) continue;
                var player = world.AddPlayer(name, Gear.Wearing("obsidian_sword", "obsidian_armor"));
                player.Position = OpenBeside(world, chest).Value;
                world.MonstersHold = true;
                return (host, chest, player);
            }
            throw new InvalidOperationException("no trap chest");
        }

        static void Fell(World world, Trap trap)
        {
            foreach (var enemy in world.Enemies.Where(e => e.PackId == trap.Band?.Id)) enemy.Health = 0;
        }

        [Test]
        public void TheShippedChunksHaveTrapChests()
        {
            var (host, chest, _) = AtTrap();
            Assert.AreEqual("trap_chest", chest.Definition.Id);
            Assert.AreEqual(TileType.IronChest, host.World.Map[chest.X, chest.Y]);
        }

        [Test]
        public void OpeningTheChestShutsTheRoomAndCallsTwoBands()
        {
            var (host, chest, player) = AtTrap();
            var world = host.World;
            Assert.IsTrue(world.CanUse(chest, player));
            world.TakeEvents();
            Assert.IsTrue(world.UsePoi(player.Id, chest.Index));
            var trap = world.TrapAt(chest.Index);
            Assert.IsNotNull(trap);
            Assert.AreEqual(TrapPhase.Shut, trap.Phase);
            Assert.AreSame(trap, world.TrapOf(player));
            Assert.IsFalse(world.Opened(chest.Index), "the chest waits for the bands");
            Assert.AreEqual(TileType.IronChest, world.Map[chest.X, chest.Y]);
            Assert.IsFalse(world.CanUse(chest, player), "not again while it is on");
            CollectionAssert.Contains(world.HeldTraps.ToList(), chest.Index);
            var events = world.TakeEvents();
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Trap && e.Amount == World.TrapSprung && e.Position.Equals(chest.Center)));

            // Every way out is shut: the doors are grates, and from the chest no floor past them is walked to.
            Assert.IsNotEmpty(trap.Doors);
            Assert.IsTrue(trap.Doors.All(d => world.Map[d.X, d.Y] == TileType.Grate));
            Assert.Less(trap.Doors.Count, 120, "doorways, not more");
            var flow = new FlowField(world.Map);
            flow.Build(new[] { player.Position }, 200);
            float reach = trap.Room.Radius + 13.5f;
            for (int y = 0; y < world.Map.Height; y++)
                for (int x = 0; x < world.Map.Width; x++)
                    if (flow.DistanceAt(x, y) != FlowField.Unreached)
                        Assert.LessOrEqual(Vec2.Distance(TileMap.Center(x, y), trap.Room.Center), reach, $"({x}, {y}) is walked to from inside");

            // The first band, a moment later, after the player; the second when the first fell.
            Run(world, World.TrapFirstSeconds - 0.3f);
            Assert.AreEqual(TrapPhase.Shut, trap.Phase);
            Run(world, 0.5f);
            Assert.AreEqual(TrapPhase.Fighting, trap.Phase);
            Assert.AreEqual(1, trap.Wave);
            var first = world.MembersOf(trap.Band).ToList();
            Assert.That(first.Count, Is.GreaterThanOrEqualTo(trap.Band.Definition.MinCount));
            Assert.IsTrue(first.All(e => e.TargetId == player.Id && e.State == EnemyState.Chase));
            Assert.IsTrue(first.All(e => Vec2.Distance(e.Position, player.Position) >= 1.9f), "not on top of the player");
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.Trap && e.Amount == World.TrapWave + 1));
            Fell(world, trap);
            Run(world, 0.2f);
            Assert.AreEqual(TrapPhase.Between, trap.Phase);
            Run(world, World.TrapBetweenSeconds + 0.2f);
            Assert.AreEqual(TrapPhase.Fighting, trap.Phase);
            Assert.AreEqual(2, trap.Wave);
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.Trap && e.Amount == World.TrapWave + 2));
            Assert.IsNotEmpty(world.MembersOf(trap.Band));
        }

        [Test]
        public void WinningLiftsTheGratesAndTheChestGivesATierHigher()
        {
            var (host, chest, player) = AtTrap();
            var world = host.World;
            int items = player.Inventory.Items.Count;
            Assert.IsTrue(world.UsePoi(player.Id, chest.Index));
            var trap = world.TrapAt(chest.Index);
            var doors = trap.Doors.ToList();
            for (int wave = 1; wave <= World.TrapWaves; wave++)
            {
                Run(world, World.TrapBetweenSeconds + 0.5f);
                Assert.AreEqual(wave, trap.Wave);
                Fell(world, trap);
                Run(world, 0.2f);
            }
            Assert.AreEqual(TrapPhase.Won, trap.Phase);
            Assert.IsNull(world.TrapOf(player));
            Assert.IsTrue(doors.All(d => world.Map[d.X, d.Y] == d.Was), "the grates are up");
            Assert.IsTrue(world.Opened(chest.Index));
            Assert.AreEqual(TileType.OpenChest, world.Map[chest.X, chest.Y]);
            // The gold chest's: two rare or better pieces, where the trap chest's own row has one.
            Assert.AreEqual(items + 2, player.Inventory.Items.Count, "a chest a tier higher (gold): two pieces");
            Assert.IsTrue(player.Inventory.Items.Skip(items).All(i => i.Rarity >= Rarity.Rare));
            var events = world.TakeEvents();
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Trap && e.Amount == World.TrapWon));
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.PoiOpened && e.Amount == chest.Index));
            Assert.IsFalse(world.UsePoi(player.Id, chest.Index), "once");
        }

        [Test]
        public void FallingInsideLiftsTheGratesAndTheChestWaitsForDawn()
        {
            var (host, chest, player) = AtTrap();
            var world = host.World;
            Assert.IsTrue(world.UsePoi(player.Id, chest.Index));
            var trap = world.TrapAt(chest.Index);
            var doors = trap.Doors.ToList();
            Run(world, World.TrapFirstSeconds + 0.3f);
            int band = trap.Band.Id;
            world.Hurt(player, 1e6f);
            Run(world, 0.2f);
            Assert.AreEqual(TrapPhase.Lost, trap.Phase);
            Assert.IsTrue(doors.All(d => world.Map[d.X, d.Y] == d.Was));
            Assert.IsFalse(world.Enemies.Any(e => e.PackId == band), "the band went");
            Assert.IsFalse(world.Opened(chest.Index));
            Assert.AreEqual(TileType.IronChest, world.Map[chest.X, chest.Y]);
            Assert.IsFalse(world.CanUse(chest, player), "not again today");
            CollectionAssert.Contains(world.HeldTraps.ToList(), chest.Index);
            world.RestoreClock(world.Clock + Calendar.TicksPerDay);
            Assert.IsTrue(world.CanUse(chest, player), "ready again the next day");
            CollectionAssert.DoesNotContain(world.HeldTraps.ToList(), chest.Index);
        }

        [Test]
        public void EveryoneInsideIsShutInAndRollsTheirOwn()
        {
            var (host, chest, player) = AtTrap();
            var world = host.World;
            var friend = world.AddPlayer("b", Gear.Wearing("obsidian_sword", "obsidian_armor"));
            friend.Position = player.Position;
            int items = friend.Inventory.Items.Count;
            Assert.IsTrue(world.UsePoi(player.Id, chest.Index));
            var trap = world.TrapAt(chest.Index);
            CollectionAssert.AreEquivalent(new[] { player.Id, friend.Id }, trap.Inside);
            Run(world, World.TrapFirstSeconds + 0.3f);
            int alone = trap.Band.Definition.Members.Sum(m => m.Max);
            Assert.That(world.MembersOf(trap.Band).Count(), Is.LessThanOrEqualTo(alone + World.PackExtraPerPlayer), "two more for the second player");
            // One falls: the other fights on.
            world.Hurt(player, 1e6f);
            Run(world, 0.2f);
            Assert.AreEqual(TrapPhase.Fighting, trap.Phase);
            for (int wave = trap.Wave; wave <= World.TrapWaves; wave++)
            {
                Fell(world, trap);
                Run(world, World.TrapBetweenSeconds + 0.5f);
            }
            Assert.AreEqual(TrapPhase.Won, trap.Phase);
            Assert.AreEqual(items + 2, friend.Inventory.Items.Count);
        }

        [Test]
        public void ASaveInTheMiddleKeepsTheFloorNotTheGrates()
        {
            var (host, chest, player) = AtTrap();
            var world = host.World;
            Assert.IsTrue(world.UsePoi(player.Id, chest.Index));
            var trap = world.TrapAt(chest.Index);
            Run(world, World.TrapFirstSeconds + 0.3f);
            Assert.IsTrue(world.ChangedTiles().Any(t => t.Type == TileType.Grate), "a player joining now is sent the grates");
            var save = WorldSave.Read(host.SaveWorld(now: 0), Tiles.Shipped, Gear.Data);
            Assert.IsFalse(save.Tiles.Any(t => t.Type == TileType.Grate), "the save keeps the floor the grates fell on");
            CollectionAssert.DoesNotContain(save.OpenedPois.ToList(), chest.Index);
            var again = Host(save.Seed);
            again.Restore(save);
            Assert.IsTrue(trap.Doors.All(d => again.World.Map[d.X, d.Y] == d.Was));
            Assert.IsTrue(again.World.CanUse(chest, null), "the trap is ready again");
            Assert.IsNull(again.World.TrapAt(chest.Index));

            // Won, the chest stays opened through a save.
            for (int wave = trap.Wave; wave <= World.TrapWaves; wave++)
            {
                Fell(world, trap);
                Run(world, World.TrapBetweenSeconds + 0.5f);
            }
            Assert.AreEqual(TrapPhase.Won, trap.Phase);
            var won = WorldSave.Read(host.SaveWorld(now: 0), Tiles.Shipped, Gear.Data);
            CollectionAssert.Contains(won.OpenedPois.ToList(), chest.Index);
        }

        [Test]
        public void TheClientSeesTheGratesTheChestHeldAndTheEvents()
        {
            var (host, chest, _) = AtTrap();
            var world = host.World;
            var client = new WorldClient("key", "c", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped) { Story = Tales.Shipped };
            host.Receive(1, client.JoinMessage());
            var events = new List<SimEvent>();
            void Deliver()
            {
                foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
                events.AddRange(client.TakeEvents());
            }
            host.Tick();
            Deliver();
            var player = world.FindPlayer(client.PlayerId);
            player.Position = OpenBeside(world, chest).Value;
            Assert.IsTrue(client.CanUse(chest));
            Assert.IsTrue(world.UsePoi(player.Id, chest.Index));
            var trap = world.TrapAt(chest.Index);
            for (int i = 0; i < World.Ticks(World.TrapFirstSeconds + 0.5f); i++)
            {
                host.Tick();
                Deliver();
            }
            Assert.IsTrue(trap.Doors.All(d => client.Map[d.X, d.Y] == TileType.Grate), "the grates went over as tile changes");
            Assert.IsTrue(client.TrapHeld(chest.Index));
            Assert.IsFalse(client.CanUse(chest), "no [열기] while the bands are in");
            var trapEvents = events.Where(e => e.Type == SimEventType.Trap).ToList();
            Assert.IsTrue(trapEvents.Any(e => e.Amount == World.TrapSprung && Vec2.Distance(e.Position, chest.Center) < 0.2f), "with the chest's place");
            Assert.IsTrue(trapEvents.Any(e => e.Amount == World.TrapWave + 1));
        }

        [Test]
        public void TheDoorsShutWellInEveryTrapRoom()
        {
            int rooms = 0;
            foreach (ulong seed in new ulong[] { 1, 2, 3, 4 })
            {
                var host = Host(seed);
                var world = host.World;
                world.MonstersHold = true;
                foreach (var chest in world.Pois.Where(p => p.Kind == PoiKind.Chest && p.Definition.Role == RoomRole.Trap))
                {
                    var at = OpenBeside(world, chest);
                    if (at == null) continue;
                    var player = world.AddPlayer("t" + chest.Index, Gear.Wearing("obsidian_sword"));
                    player.Position = at.Value;
                    if (!world.UsePoi(player.Id, chest.Index)) continue;
                    rooms++;
                    var trap = world.TrapAt(chest.Index);
                    // Seeds 1~4 (2026-11-22): 4~86 doorways, a big cave or ice room's ragged rim the most; 96~1161 floor tiles.
                    Assert.That(trap.Doors.Count, Is.InRange(1, 120), $"seed {seed}, room {trap.Room.Index} ({trap.Room.Chunk?.Id}, radius {trap.Room.Radius})");
                    Assert.Greater(trap.Floor.Count, 20, "room to fight in");
                    world.Hurt(player, 1e6f);
                    Run(world, 0.1f);
                    Assert.AreEqual(TrapPhase.Lost, trap.Phase);
                    world.RemovePlayer(player.Id);
                }
            }
            Assert.Greater(rooms, 4);
        }
    }
}
