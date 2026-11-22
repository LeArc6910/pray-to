using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The things to find in the rooms (2026-11-22, Docs/WorldRework.md 3단계, 묶음 3a·3b): the sheet, where they are laid,
    // what using them gives, the save and the wire; the hidden rooms in the rock and the pickaxe locks.
    public class PoiTests
    {
        const int Size = 512;

        static readonly Dictionary<ulong, GeneratedWorld> Made = new Dictionary<ulong, GeneratedWorld>();

        static GeneratedWorld Big(ulong seed)
        {
            if (!Made.TryGetValue(seed, out var world))
                Made[seed] = world = WorldGenerator.Generate(seed, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped, Bosses.Staged.Arenas, WorldGenerator.RoomVersion,
                    Tiles.Shipped.Terrain);
            return world;
        }

        static HostConfig RoomConfig => new HostConfig { MapSize = Size, MapVersion = WorldGenerator.RoomVersion };

        static WorldHost Host(ulong seed = 3) => new WorldHost(seed, Monsters.Shipped, Gear.Data, RoomConfig, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped,
            Classes.Shipped, Tiles.Shipped, story: Tales.Shipped);

        // A player standing on open floor beside the thing.
        static Player Beside(World world, Poi poi, string name = "a")
        {
            var player = world.AddPlayer(name, Gear.Wearing("iron_sword"));
            player.Position = OpenBeside(world, poi);
            return player;
        }

        static Vec2 OpenBeside(World world, Poi poi)
        {
            for (float reach = 1; reach <= World.PoiReach; reach += 0.25f)
                for (int k = 0; k < 32; k++)
                {
                    var at = poi.Center + Vec2.FromAngle(2 * MathF.PI * k / 32) * reach;
                    if (!Collision.Overlaps(world.Map, at, World.PlayerRadius)) return at;
                }
            throw new InvalidOperationException($"no floor beside {poi.Definition.Id} at ({poi.X}, {poi.Y})");
        }

        static Poi First(World world, PoiKind kind, Func<Poi, bool> also = null) =>
            world.Pois.Where(p => p.Kind == kind && (also == null || also(p))).OrderBy(p => p.Index).First(p =>
            {
                try
                {
                    OpenBeside(world, p);
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            });

        [Test]
        public void TheSheetReadsAndSaysWhatIsWrong()
        {
            var pois = Tiles.Shipped.Terrain.Pois;
            Assert.IsTrue(pois.Success, string.Join("\n", pois.Errors));
            Assert.IsTrue(Tiles.Shipped.Terrain.Success, string.Join("\n", Tiles.Shipped.Terrain.Errors));
            foreach (var role in new[] { RoomRole.Rest, RoomRole.Shrine, RoomRole.Cart, RoomRole.Treasure, RoomRole.Hidden, RoomRole.Locked, RoomRole.Trap })
                foreach (Biome biome in Enum.GetValues(typeof(Biome)))
                    Assert.IsTrue(pois.For(role, biome).Any(), $"{role} in {biome}");
            // The five blessings of the design, each a buff.
            Assert.AreEqual(5, pois.Pois.Count(p => p.Kind == PoiKind.Shrine));
            Assert.IsTrue(pois.Pois.Where(p => p.Kind == PoiKind.Shrine).All(p => p.Buff.Count > 0 && p.Again == PoiAgain.Daily && p.PerPlayer));
            Assert.IsTrue(pois.Pois.Where(p => p.Kind == PoiKind.Chest).All(p => p.Again == PoiAgain.Once && p.PerPlayer && p.Tile != null));
            Assert.AreEqual(PoiAgain.Always, pois.Find("camp").Again);
            // A locked room's vault holds the ore of the lock's tier.
            Assert.AreEqual("obsidian_ore", pois.Find("vault_obsidian").Reward(PoiRewardKind.Material).Material);
            Assert.AreEqual("void_ore", pois.Find("vault_void").Reward(PoiRewardKind.Material).Material);

            const string Header = "id,name,role,biomes,weight,reward,reward_level,per_player,again,signal,signal_range,buff,tile\n";
            Assert.AreSame(PoiData.None, PoiSheet.Parse(""));
            StringAssert.Contains("열이 없습니다", PoiSheet.Parse("id,role\nx,rest\n").Errors[0]);
            string Error(string row) => string.Join("\n", PoiSheet.Parse(Header + row, Gear.Data).Errors);
            StringAssert.Contains("reward", Error("a,가,treasure,,1,gems:3,,yes,once,glint,10,,WoodChest"));
            StringAssert.Contains("buff", Error("a,가,shrine,,1,bless:300,,yes,daily,beam,14,,"));
            StringAssert.Contains("tile", Error("a,가,treasure,,1,coins:3,,yes,once,glint,10,,Wall"));
            StringAssert.Contains("없는 재료", Error("a,가,locked,,1,material:moon_dust:3,,yes,once,glint,10,,GoldChest"));
            StringAssert.Contains("볼거리가 없습니다", Error("a,가,pack,,1,coins:3,,yes,once,,,,"));
            StringAssert.Contains("signal_range", Error("a,가,cart,,1,coins:3,,no,once,glint,,,"));
            StringAssert.Contains("always", Error("a,가,cart,,1,coins:3,,no,always,,,,"));
        }

        [Test]
        public void EachThingStandsInItsRoomOnItsTile()
        {
            for (ulong seed = 1; seed <= 3; seed++)
            {
                var world = Big(seed);
                var rooms = world.Rooms;
                Assert.Greater(rooms.Pois.Count, 100, $"seed {seed}: things to find");
                for (int i = 0; i < rooms.Pois.Count; i++)
                {
                    var poi = rooms.Pois[i];
                    var room = rooms.Nodes[poi.Room];
                    string where = $"seed {seed}: {poi.Definition.Id} at ({poi.X}, {poi.Y}) in a {room.Role} room";
                    Assert.AreEqual(i, poi.Index, where);
                    Assert.AreEqual(room.Biome, poi.Biome, where);
                    Assert.IsTrue(poi.Definition.For(room.Biome), where);
                    var type = world.Map[poi.X, poi.Y];
                    switch (poi.Kind)
                    {
                        case PoiKind.Camp:
                            Assert.AreEqual(RoomRole.Rest, room.Role, where);
                            Assert.AreEqual(TileType.Campfire, type, where);
                            break;
                        case PoiKind.Shrine:
                            Assert.IsTrue(room.Role == RoomRole.Shrine || room.Role == RoomRole.Hidden, where);
                            Assert.AreEqual(TileType.RuneStone, type, where);
                            break;
                        case PoiKind.Cart:
                            Assert.AreEqual(RoomRole.Cart, room.Role, where);
                            Assert.AreEqual(TileType.MineCart, type, where);
                            break;
                        default:
                            Assert.AreEqual(poi.Definition.Role, room.Role, where);
                            Assert.AreEqual(poi.Definition.Tile, type, where);
                            break;
                    }
                }
                // Every camp, shrine, treasure, locked and cart room has its thing.
                foreach (var room in rooms.Nodes.Where(n => n.Role is RoomRole.Rest or RoomRole.Shrine or RoomRole.Treasure or RoomRole.Locked or RoomRole.Cart or RoomRole.Hidden or RoomRole.Trap))
                    Assert.IsTrue(rooms.Pois.Any(p => p.Room == room.Index), $"seed {seed}: the {room.Role} room at ({room.X}, {room.Y})");
                // Chests come in all three kinds, and a locked room's chest is gold.
                foreach (var tile in new[] { TileType.WoodChest, TileType.IronChest, TileType.GoldChest })
                    Assert.IsTrue(rooms.Pois.Any(p => p.Definition.Tile == tile), $"seed {seed}: a {tile}");
                Assert.IsTrue(rooms.Pois.Where(p => rooms.Nodes[p.Room].Role == RoomRole.Locked && p.Kind == PoiKind.Chest).All(p => p.Definition.Tile == TileType.GoldChest));
            }
        }

        [Test]
        public void TheSameSeedLaysTheSameThingsAndCavesHaveNone()
        {
            GeneratedWorld Make() => WorldGenerator.Generate(5, 256, 256, Tales.Shipped, Bosses.Staged.Arenas, WorldGenerator.RoomVersion, Tiles.Shipped.Terrain);
            var a = Make().Rooms.Pois.Select(p => (p.Definition.Id, p.X, p.Y, p.Room)).ToList();
            var b = Make().Rooms.Pois.Select(p => (p.Definition.Id, p.X, p.Y, p.Room)).ToList();
            Assert.IsNotEmpty(a);
            CollectionAssert.AreEqual(a, b);
            // Without the sheet the rooms keep their marks but nothing is laid, and a chest stays a plain wooden one.
            var bare = WorldGenerator.Generate(5, 256, 256, Tales.Shipped, Bosses.Staged.Arenas, WorldGenerator.RoomVersion, Tiles.Shipped.Terrain.WithoutPois());
            Assert.IsEmpty(bare.Rooms.Pois);
            // A world of caves (version 1) has no rooms and nothing to find.
            var caves = new WorldHost(7, Monsters.Shipped, Gear.Data, new HostConfig { MapSize = 256 }, new WorldConfig { EnemyTarget = 0 }, tiles: Tiles.Shipped);
            Assert.IsEmpty(caves.World.Pois);
            Assert.IsNull(caves.Generated.Rooms);
        }

        [Test]
        public void AChestOpensOnceAndEachPlayerNearRollsTheirOwn()
        {
            var host = Host();
            var world = host.World;
            var chest = First(world, PoiKind.Chest, p => world.Map[p.X, p.Y] != TileType.OpenChest && host.Generated.Rooms.Nodes[p.Room].Role == RoomRole.Treasure);
            var opener = Beside(world, chest, "a");
            var friend = Beside(world, chest, "b");
            var far = world.AddPlayer("c", Gear.Wearing("iron_sword"));
            far.Position = world.Spawn;
            int Coins(Player p) => p.Inventory.CountOf(Gear.Data.Materials.First(m => m.Id == World.Coin));
            int ItemsOf(Player p) => p.Inventory.Items.Count;
            int before = ItemsOf(opener);

            Assert.IsTrue(world.UsePoi(opener.Id, chest.Index));
            var events = world.TakeEvents();
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.PoiOpened && e.Amount == chest.Index && e.Source == opener.Id));
            Assert.IsTrue(world.Opened(chest.Index));
            Assert.AreEqual(TileType.OpenChest, world.Map[chest.X, chest.Y]);
            var coins = chest.Definition.Reward(PoiRewardKind.Coins);
            Assert.That(Coins(opener), Is.InRange(coins.Min, coins.Max));
            Assert.That(Coins(friend), Is.InRange(coins.Min, coins.Max));
            Assert.AreEqual(0, Coins(far), "too far to share");
            Assert.Greater(ItemsOf(opener), before);
            Assert.Greater(ItemsOf(friend), 0);
            // Once: neither can open it again, and the host tells the clients it is open.
            Assert.IsFalse(world.UsePoi(friend.Id, chest.Index));
            Assert.IsFalse(world.UsePoi(opener.Id, chest.Index));
            Assert.IsNull(world.PoiNear(opener.Position, opener));
            // Out of reach, nothing.
            var other = First(world, PoiKind.Chest, p => p.Index != chest.Index && Vec2.Distance(p.Center, far.Position) > World.PoiReach + 1);
            Assert.IsFalse(world.UsePoi(far.Id, other.Index));
        }

        [Test]
        public void AShrineBlessesEachPlayerOnceADay()
        {
            var host = Host();
            var world = host.World;
            var shrine = First(world, PoiKind.Shrine);
            var player = Beside(world, shrine);
            player.Health = player.Stats.MaxHealth / 3;
            var stat = shrine.Definition.Buff[0].Stat;
            float Of(Player p) => stat switch
            {
                StatType.Damage => p.Stats.DamageFor(SkillTag.Melee),
                StatType.MoveSpeed => p.Stats.MoveSpeed,
                StatType.ItemRarity => p.Stats.ItemRarity,
                StatType.HealthRegen => p.Stats.HealthRegen,
                _ => p.Stats.Armor,
            };
            float plain = Of(player);
            Assert.IsTrue(world.UsePoi(player.Id, shrine.Index));
            Assert.AreSame(shrine.Definition, player.Blessing);
            Assert.Greater(Of(player), plain, shrine.Definition.Id);
            Assert.AreEqual(player.Stats.MaxHealth, player.Health, 0.01f, "and health full");
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.Blessed && e.Amount == shrine.Index));
            Assert.IsFalse(world.UsePoi(player.Id, shrine.Index), "once a day");
            CollectionAssert.Contains(world.PrayedToday("a").ToList(), shrine.Index);
            // Another player may pray there the same day.
            var other = Beside(world, shrine, "b");
            Assert.IsTrue(world.UsePoi(other.Id, shrine.Index));
            // The blessing runs out.
            for (int i = 0; i < World.Ticks(shrine.Definition.Reward(PoiRewardKind.Bless).Min) + 2; i++) world.Step();
            Assert.IsNull(player.Blessing);
            Assert.AreEqual(plain, Of(player), 0.01f);
            // Next day it gives again.
            world.RestoreClock(world.Clock + Calendar.TicksPerDay);
            world.Step();
            Assert.IsEmpty(world.PrayedToday("a"));
            Assert.IsTrue(world.UsePoi(player.Id, shrine.Index));
        }

        [Test]
        public void ACampHealsKeepsMonstersOffRestsAndIsWhereOneGetsUp()
        {
            var host = Host();
            var world = host.World;
            var camp = world.Pois.First(p => p.Kind == PoiKind.Camp);
            Assert.IsNull(world.PoiNear(camp.Center), "a camp is not used by hand");
            Assert.IsTrue(world.InCamp(camp.Center) && world.InSafePlace(camp.Center));
            var player = Beside(world, camp);
            player.Health = player.Stats.MaxHealth / 4;
            float before = player.Health;
            for (int i = 0; i < World.Ticks(World.RestSeconds) + 1; i++) world.Step();
            // A tenth a second, besides the usual.
            Assert.Greater(player.Health - before, player.Stats.MaxHealth * 0.1f * World.RestSeconds * 0.9f);
            Assert.Greater(player.RestedTicks, 0);
            Assert.AreEqual(camp.Index, world.LastCampOf("a"));
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.Blessed && e.Amount == camp.Index));
            // Rested: a tenth more experience.
            int xp = player.Experience;
            world.GainExperience(player, 100);
            Assert.AreEqual(110, player.Experience - xp);
            // A monster put into the ring is put out of it.
            var slime = world.SpawnEnemy(Monsters.Shipped.Find("slime"), camp.Center + new Vec2(2, 0));
            world.Step();
            Assert.GreaterOrEqual(Vec2.Distance(slime.Position, camp.Center), World.CampSafeReach - 0.01f);
            // Fallen, they get up by the camp, not at home.
            player.Position = world.Spawn + new Vec2(1, 1);
            player.Health = 0;
            player.RespawnTicks = 1;
            world.Step();
            Assert.Less(Vec2.Distance(player.Position, camp.Center), 3);
        }

        [Test]
        public void ACartIsTheOpenersAndGivesItsBiomesThings()
        {
            var host = Host(5);
            var world = host.World;
            var cart = First(world, PoiKind.Cart);
            var opener = Beside(world, cart, "a");
            var friend = Beside(world, cart, "b");
            Assert.IsTrue(world.UsePoi(opener.Id, cart.Index));
            var forage = Gear.Data.Materials.First(m => m.Id == World.ForageOf[cart.Biome]);
            Assert.That(opener.Inventory.CountOf(forage), Is.InRange(5, 10));
            Assert.AreEqual(0, friend.Inventory.CountOf(forage), "not per player");
            Assert.AreEqual(TileType.MineCart, world.Map[cart.X, cart.Y], "the cart stays");
            Assert.IsFalse(world.UsePoi(friend.Id, cart.Index));
        }

        [Test]
        public void WhatBecameOfThemIsSavedAndSent()
        {
            var host = Host();
            var world = host.World;
            var client = new WorldClient("key", "a", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped) { Story = Tales.Shipped };
            host.Receive(1, client.JoinMessage());
            void Deliver()
            {
                foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            }
            host.Tick();
            Deliver();
            var player = world.FindPlayer(client.PlayerId);
            Assert.AreEqual(world.Pois.Count, client.Pois.Count, "the client lays the same things from the seed");
            CollectionAssert.AreEqual(world.Pois.Select(p => (p.Definition.Id, p.X, p.Y)), client.Pois.Select(p => (p.Definition.Id, p.X, p.Y)));

            var chest = First(world, PoiKind.Chest);
            var shrine = First(world, PoiKind.Shrine);
            var camp = world.Pois.First(p => p.Kind == PoiKind.Camp);
            player.Position = OpenBeside(world, chest);
            Assert.IsTrue(world.UsePoi(player.Id, chest.Index));
            player.Position = OpenBeside(world, shrine);
            Assert.IsTrue(world.UsePoi(player.Id, shrine.Index));
            player.Position = OpenBeside(world, camp);
            for (int i = 0; i < World.Ticks(World.RestSeconds) + 1; i++) host.Tick();
            Deliver();
            Assert.IsTrue(client.PoiOpened(chest.Index));
            Assert.IsFalse(client.CanUse(chest));
            Assert.IsTrue(client.PrayedToday(shrine.Index));
            Assert.AreEqual(camp.Index, client.LastCamp);
            Assert.AreSame(shrine.Definition, client.Blessing);
            Assert.Greater(client.BlessingSeconds, 250);
            Assert.Greater(client.RestedSeconds, 500);
            Assert.AreEqual(TileType.OpenChest, client.Map[chest.X, chest.Y], "the open chest's tile went over as a change");

            // The world's save keeps it all; a world from before (format 12) reads with nothing opened.
            var save = WorldSave.Read(host.SaveWorld(now: 0), Tiles.Shipped, Gear.Data);
            CollectionAssert.Contains(save.OpenedPois.ToList(), chest.Index);
            var again = new WorldHost(save.Seed, Monsters.Shipped, Gear.Data, RoomConfig, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped,
                story: Tales.Shipped);
            again.Restore(save);
            Assert.IsTrue(again.World.Opened(chest.Index));
            Assert.AreEqual(TileType.OpenChest, again.World.Map[chest.X, chest.Y]);
            CollectionAssert.Contains(again.World.PrayedToday("a").ToList(), shrine.Index);
            Assert.AreEqual(camp.Index, again.World.LastCampOf("a"));
            Assert.AreEqual(world.OpenedPois.Count(), again.World.OpenedPois.Count());
        }

        // ---- 3b: hidden rooms in the rock, and the locks ----

        [Test]
        public void HiddenRoomsLieInTheRockBehindACrack()
        {
            for (ulong seed = 1; seed <= 3; seed++)
            {
                var world = Big(seed);
                var hidden = world.Rooms.Nodes.Where(n => n.Role == RoomRole.Hidden).ToList();
                var buried = hidden.Where(n => n.Buried).ToList();
                Assert.GreaterOrEqual(buried.Count, hidden.Count * 3 / 5, $"seed {seed}: hidden rooms in the rock, of {hidden.Count}");
                foreach (var room in buried)
                {
                    var (left, bottom, w, h) = room.Box.Value;
                    string where = $"seed {seed}: the hidden room at ({left}, {bottom})";
                    // Only a crack shows from the room: its door is a cracked wall, and the tile outside it is the room's floor.
                    Assert.IsFalse(world.Map.IsSolid(room.AnchorX, room.AnchorY), where);
                    int cracks = 0, open = 0;
                    for (int y = bottom - 1; y <= bottom + h; y++)
                    {
                        for (int x = left - 1; x <= left + w; x++)
                        {
                            bool inside = x >= left && y >= bottom && x < left + w && y < bottom + h;
                            if (inside && world.Map[x, y] == TileType.CrackedWall) cracks++;
                            // Round it is rock, but where its door looks out.
                            if (!inside && !world.Map.IsSolid(x, y)) open++;
                        }
                    }
                    Assert.GreaterOrEqual(cracks, 2, where + ": a crack two thick");
                    Assert.LessOrEqual(open, Math.Max(w, h) + 2, where + ": open only on its door's side");
                }
            }
        }

        [Test]
        public void ALockOpensOnlyToItsPickaxe()
        {
            var map = new TileMap(9, 9, TileType.Floor);
            map[5, 4] = TileType.ObsidianWall;
            map[5, 6] = TileType.VoidWall;
            map[5, 2] = TileType.CrackedWall;
            var world = new World(map, TileMap.Center(4, 4), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);
            Player With(string weapon, int y)
            {
                var player = world.AddPlayer(weapon + y, Gear.Wearing(weapon));
                player.Position = TileMap.Center(4, y);
                player.Facing = new Vec2(1, 0);
                return player;
            }
            List<SimEvent> Swing(Player player, int swings)
            {
                var events = new List<SimEvent>();
                for (int i = 0; i < swings * 60; i++)
                {
                    player.Attacking = true;
                    world.Step();
                    events.AddRange(world.TakeEvents());
                }
                player.Attacking = false;
                return events;
            }
            // Iron is not enough for obsidian: the swing says a better pickaxe is wanted, and the wall stands.
            var iron = With("iron_pickaxe", 4);
            var told = Swing(iron, 3);
            Assert.IsTrue(told.Any(e => e.Type == SimEventType.Dug && e.Amount == World.NeedsBetterPickaxe));
            Assert.AreEqual(TileType.ObsidianWall, map[5, 4]);
            world.RemovePlayer(iron.Id);
            // Mithril digs obsidian through, but not void stone.
            var mithril = With("mithril_pickaxe", 4);
            Swing(mithril, 12);
            Assert.AreEqual(TileType.Floor, map[5, 4]);
            mithril.Position = TileMap.Center(4, 6);
            Assert.IsTrue(Swing(mithril, 3).Any(e => e.Type == SimEventType.Dug && e.Amount == World.NeedsBetterPickaxe));
            Assert.AreEqual(TileType.VoidWall, map[5, 6]);
            // A cracked wall gives way to a sword.
            var sword = With("copper_sword", 2);
            Swing(sword, 3);
            Assert.AreEqual(TileType.Floor, map[5, 2]);
        }

        [Test]
        public void TheLocksAreSeenOneTierEarly()
        {
            var rooms = Tiles.Shipped.Terrain.Rooms;
            // Obsidian (a mithril pickaxe) before the lava fields; void stone (an obsidian pickaxe) in them.
            foreach (var chunk in rooms.Chunks.Where(c => c.Role == RoomRole.Locked))
            {
                bool obsidian = chunk.Rows.Any(r => r.Contains('O')), voids = chunk.Rows.Any(r => r.Contains('V'));
                if (voids) CollectionAssert.IsSubsetOf(chunk.Biomes, new[] { Biome.Lava, Biome.Abyss }, chunk.Id);
                if (obsidian && !voids) CollectionAssert.DoesNotContain(chunk.Biomes, Biome.Lava, chunk.Id);
            }
            Assert.IsTrue(rooms.For(RoomRole.Locked, Biome.Lava).All(c => c.Rows.Any(r => r.Contains('V'))));
            foreach (var biome in new[] { Biome.Sand, Biome.Crystal, Biome.Lake })
                Assert.IsTrue(rooms.For(RoomRole.Locked, biome).Any(c => c.Rows.Any(r => r.Contains('O'))), biome.ToString());
        }
    }
}
