using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class NetTests
    {
        // A host and its clients in one process, passing the encoded messages straight across.
        sealed class Session
        {
            public readonly WorldHost Host;
            public readonly Dictionary<ulong, WorldClient> Clients = new Dictionary<ulong, WorldClient>();

            public Session(HostConfig config = null, BossData bosses = null) =>
                Host = new WorldHost(7, Monsters.Shipped, Gear.Data, config, new WorldConfig { EnemyTarget = 0 }, bosses, Classes.Shipped, Tiles.Shipped);

            public WorldClient Join(ulong connection, string name, string version = Protocol.Version, params string[] classes)
            {
                var client = new WorldClient("key" + connection, name, Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, classes, Tiles.Shipped);
                Clients[connection] = client;
                Host.Receive(connection, version == Protocol.Version ? client.JoinMessage() : new JoinMessage(client.PlayerKey, name, null, version).Encode());
                Deliver();
                return client;
            }

            // Hands a command from a client to the host, as the transport would.
            public void Send(ulong connection, byte[] message)
            {
                Assert.IsNotNull(message);
                Host.Receive(connection, message);
            }

            public void Deliver()
            {
                foreach (var message in Host.TakeOutgoing())
                {
                    if (Clients.TryGetValue(message.Connection, out var client)) client.Receive(message.Data);
                }
            }

            public void Tick(int ticks = 1, Func<WorldClient, (Vec2 Move, Vec2 Aim, bool Attack)> controls = null)
            {
                for (int i = 0; i < ticks; i++)
                {
                    foreach (var pair in Clients)
                    {
                        var (move, aim, attack) = controls?.Invoke(pair.Value) ?? (Vec2.Zero, Vec2.Zero, false);
                        byte[] input = pair.Value.Step(move, aim, attack);
                        if (input != null) Host.Receive(pair.Key, input);
                    }
                    Host.Tick();
                    Deliver();
                    foreach (var client in Clients.Values) client.AdvanceRenderClock(World.TickSeconds);
                }
            }
        }

        [Test]
        public void MessagesSurviveEncoding()
        {
            var input = InputMessage.Decode(new InputMessage(12, new Vec2(1.5f, -2), new Vec2(0, 1), true).Encode());
            Assert.AreEqual(12, input.Sequence);
            Assert.AreEqual(new Vec2(1.5f, -2), input.Position);
            Assert.IsTrue(input.Attacking);

            var join = JoinMessage.Decode(new JoinMessage("key", "앨리스", new[] { "warrior", "fire_mage" }).Encode());
            Assert.AreEqual("앨리스", join.Name);
            CollectionAssert.AreEqual(new[] { "warrior", "fire_mage" }, join.Classes);

            var welcome = WelcomeMessage.Decode(new WelcomeMessage(3, ulong.MaxValue, 64, 32, new Vec2(5, 6), new[] { 1, 0 }).Encode());
            Assert.AreEqual(ulong.MaxValue, welcome.Seed);
            Assert.AreEqual(32, welcome.Height);
            CollectionAssert.AreEqual(new[] { 1, 0 }, welcome.Classes);

            var skill = SkillCommandMessage.Decode(new SkillCommandMessage(3, new Vec2(0.6f, -0.8f)).Encode());
            Assert.AreEqual(3, skill.Slot);
            Assert.AreEqual(new Vec2(0.6f, -0.8f), skill.Aim);

            var roster = RosterMessage.Decode(new RosterMessage(
                new Dictionary<int, string> { [4] = "앨리스", [5] = "밥" },
                new Dictionary<int, IReadOnlyList<int>> { [4] = new[] { 1 } }).Encode());
            Assert.AreEqual("밥", roster.Names[5]);
            CollectionAssert.AreEqual(new[] { 1 }, roster.Classes[4]);
            Assert.IsEmpty(roster.Classes[5]);

            var snapshot = SnapshotMessage.Decode(new SnapshotMessage(
                99,
                new SelfState(2, new Vec2(1, 2), 50, 100, 5, new[] { 0, 120, 7, 0 }, new[] { 0, 0, 0, 240 }, 250),
                new[] { new EntityState(7, EntityKind.Boss, 0, new Vec2(3.3f, 4.71f), new Vec2(-1, 0), 1234.2f, 2000, (byte)BossState.Telegraph, 2) },
                new[]
                {
                    new ProjectileState(8, new Vec2(5, 6), new Vec2(-7, 0.5f), 0.3f),
                    new ProjectileState(10, new Vec2(1, 1), new Vec2(12, 0), 0.25f, true, SkillTag.Ranged | SkillTag.Fire | SkillTag.Lightning),
                },
                new[] { new HazardState(9, HazardShape.Cone, new Vec2(3, 4), new Vec2(0, 1), 4.5f, 110, 12, 21, true) },
                new[]
                {
                    new SimEvent(SimEventType.Hit, 1, 7, 6, critical: true),
                    new SimEvent(SimEventType.Burst, 1, amount: 2, position: new Vec2(12.5f, -3.25f)),
                }).Encode());
            Assert.AreEqual(99, snapshot.Tick);
            Assert.AreEqual(2, snapshot.Self.Corrections);
            CollectionAssert.AreEqual(new[] { 0, 120, 7, 0 }, snapshot.Self.Cooldowns);
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 240 }, snapshot.Self.Buffs);
            Assert.AreEqual(250, snapshot.Self.ConsumableCooldown);
            var entity = snapshot.Entities.Single();
            Assert.AreEqual(EntityKind.Boss, entity.Kind);
            Assert.AreEqual((byte)BossState.Telegraph, entity.State);
            Assert.AreEqual(2, entity.Phase);
            Assert.AreEqual(1235, entity.Health, "health travels as a whole number, rounded up");
            Assert.AreEqual(3.3f, entity.Position.X, 1 / 64f);
            Assert.AreEqual(4.71f, entity.Position.Y, 1 / 64f);
            var projectile = snapshot.Projectiles[0];
            Assert.AreEqual(-7, projectile.Velocity.X, 1 / 64f);
            Assert.AreEqual(0.3f, projectile.Radius, 1 / 32f);
            Assert.IsFalse(projectile.Friendly, "a boss's");
            Assert.AreEqual(SkillTag.None, projectile.Tags);
            Assert.IsTrue(snapshot.Projectiles[1].Friendly);
            Assert.AreEqual(SkillTag.Ranged | SkillTag.Fire | SkillTag.Lightning, snapshot.Projectiles[1].Tags);
            var hazard = snapshot.Hazards.Single();
            Assert.AreEqual(HazardShape.Cone, hazard.Shape);
            Assert.AreEqual(110, hazard.Width, 1 / 8f);
            Assert.AreEqual(4.5f, hazard.Size, 1 / 8f);
            Assert.AreEqual(12, hazard.TicksLeft);
            Assert.AreEqual(21, hazard.Duration);
            Assert.IsTrue(hazard.Harmful);
            Assert.AreEqual(-1, entity.Facing.X, 0.001f);
            Assert.AreEqual(0, entity.Facing.Y, 0.03f);
            var hit = snapshot.Events[0];
            Assert.IsTrue(hit.Critical);
            Assert.AreEqual(6, hit.Amount);
            Assert.AreEqual(7, hit.Target);
            var burst = snapshot.Events[1];
            Assert.AreEqual(SimEventType.Burst, burst.Type);
            Assert.AreEqual(2, burst.Amount);
            Assert.AreEqual(new Vec2(12.5f, -3.25f), burst.Position);
        }

        [Test]
        public void TwoPlayersJoinTheSameCavesAndSeeEachOther()
        {
            var session = new Session();
            var alice = session.Join(1, "앨리스");
            var bob = session.Join(2, "밥");

            Assert.AreEqual(ClientStatus.Playing, alice.Status);
            Assert.AreEqual(ClientStatus.Playing, bob.Status);
            Assert.AreNotEqual(alice.PlayerId, bob.PlayerId);
            var map = session.Host.World.Map;
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++) Assert.AreEqual(map[x, y], bob.Map[x, y]);
            }
            CollectionAssert.AreEquivalent(new[] { "앨리스", "밥" }, alice.Names.Values);

            // Alice walks right for a second; Bob sees her arrive a few ticks later.
            session.Tick(World.TicksPerSecond, c => c == alice ? (new Vec2(1, 0), new Vec2(1, 0), false) : (Vec2.Zero, Vec2.Zero, false));
            session.Tick(10);

            var hostAlice = session.Host.World.FindPlayer(alice.PlayerId);
            Assert.AreEqual(alice.Position, hostAlice.Position, "the host took every step");
            var seenByBob = bob.Others().Single(e => e.Id == alice.PlayerId);
            Assert.AreEqual(hostAlice.Position.X, seenByBob.Position.X, 0.01f);
            Assert.AreEqual("앨리스", seenByBob.Name);
            Assert.Greater(seenByBob.Position.X, session.Host.World.Spawn.X + 2);
        }

        [Test]
        public void ClientsSwingAndTheHostKills()
        {
            var session = new Session();
            var alice = session.Join(1, "앨리스");
            var slime = session.Host.World.SpawnEnemy(Monsters.Shipped.Find("slime"), alice.Position + new Vec2(1.2f, 0));

            var events = new List<SimEvent>();
            for (int i = 0; i < 20 * World.TicksPerSecond && !slime.IsDead; i++)
            {
                session.Tick(1, _ => (Vec2.Zero, new Vec2(1, 0), true));
                events.AddRange(alice.TakeEvents());
            }

            Assert.IsTrue(slime.IsDead);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Hit && e.Source == alice.PlayerId && e.Target == slime.Id));
            session.Tick(4);
            Assert.IsFalse(alice.Others().Any(e => e.Id == slime.Id), "gone from the client too");
        }

        [Test]
        public void TheHostCorrectsMovesItRefuses()
        {
            var session = new Session();
            var alice = session.Join(1, "앨리스");
            var start = alice.Position;

            session.Host.Receive(1, new InputMessage(1, start + new Vec2(4, 0), new Vec2(1, 0), false).Encode());
            session.Host.Tick();
            session.Host.Tick();
            session.Deliver();

            Assert.AreEqual(start, session.Host.World.FindPlayer(alice.PlayerId).Position);
            Assert.AreEqual(start, alice.Position);
        }

        [Test]
        public void WrongVersionsAndFullHostsAreRefused()
        {
            var session = new Session(new HostConfig { MaxPlayers = 1 });
            var stranger = session.Join(1, "옛날 사람", version: "0.0.1");
            Assert.AreEqual(ClientStatus.Refused, stranger.Status);
            StringAssert.Contains("버전", stranger.RefusedReason);

            session.Join(2, "앨리스");
            var late = session.Join(3, "밥");
            Assert.AreEqual(ClientStatus.Refused, late.Status);
            Assert.AreEqual(1, session.Host.PlayerCount);
        }

        [Test]
        public void OlderClientsAreToldTheVersionDiffers()
        {
            // How a 0.4.0 client wrote its join, before classes.
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)MessageType.Join);
                writer.Write("key");
                writer.Write("옛날 사람");
                writer.Write("0.4.0");
            }
            var session = new Session();
            var old = new WorldClient("key", "옛날 사람", Monsters.Shipped);
            session.Clients[1] = old;

            session.Host.Receive(1, stream.ToArray());
            session.Deliver();

            Assert.AreEqual(ClientStatus.Refused, old.Status);
            StringAssert.Contains("0.4.0", old.RefusedReason);
        }

        [Test]
        public void TheHostKeepsTheClassesItsRulesAllow()
        {
            var session = new Session();
            var alice = session.Join(1, "앨리스", Protocol.Version, "fire_mage", "nobody", "fire_mage", "warrior");
            var bob = session.Join(2, "밥");

            CollectionAssert.AreEqual(new[] { "fire_mage", "warrior" }, alice.Classes.Select(c => c.Id));
            CollectionAssert.AreEqual(new[] { "fireball", "flame_brand", "whirlwind", "war_cry" }, alice.Skills.Select(s => s.Id));
            CollectionAssert.AreEqual(alice.Skills, session.Host.World.FindPlayer(alice.PlayerId).Skills);
            Assert.AreEqual(120, alice.Stats.MaxHealth, 0.001f, "the warrior passive counts on the client too");
            CollectionAssert.AreEqual(alice.Classes, bob.ClassesOf(alice.PlayerId));
            Assert.IsEmpty(bob.Skills);

            var strict = new Session(new HostConfig { MaxClasses = 1 });
            var carol = strict.Join(1, "캐롤", Protocol.Version, "warrior", "fire_mage");
            CollectionAssert.AreEqual(new[] { "warrior" }, carol.Classes.Select(c => c.Id));
            Assert.AreEqual(2, carol.Skills.Count);
        }

        [Test]
        public void SkillsGoThroughTheHost()
        {
            var session = new Session();
            var alice = session.Join(1, "앨리스", Protocol.Version, "fire_mage");
            var bob = session.Join(2, "밥");
            var world = session.Host.World;
            var host = world.FindPlayer(alice.PlayerId);
            const int fireball = 0;
            const int flameBrand = 1;

            session.Send(1, alice.SkillCommand(flameBrand, Vec2.Zero));
            session.Tick(4);

            Assert.IsTrue(host.HasBuff(flameBrand));
            Assert.Greater(alice.BuffLeft(flameBrand), 7);
            Assert.Greater(alice.CooldownLeft(flameBrand), 15);
            Assert.IsTrue((alice.Stats.AttackTags & SkillTag.Fire) != 0, "the client works the buff into its stats");
            var seen = bob.Others().Single(e => e.Id == alice.PlayerId);
            Assert.AreEqual(1 << flameBrand, seen.State, "others see which slots have a buff running");
            Assert.AreEqual("flame_brand", bob.SkillOf(alice.PlayerId, flameBrand).Id);

            var target = world.SpawnEnemy(Monsters.Shipped.Find("slime"), alice.Position + new Vec2(4, 0));
            session.Send(1, alice.SkillCommand(fireball, new Vec2(1, 0)));
            Assert.IsNull(alice.SkillCommand(fireball, new Vec2(1, 0)), "the cooldown starts on screen at once");
            session.Tick(4);
            var flying = bob.Projectiles().Single();
            Assert.IsTrue(flying.Friendly);
            Assert.AreEqual(SkillTag.Magic | SkillTag.Fire, flying.Tags, "the fireball's tags, flame brand's fire included");

            var bursts = new List<SimEvent>();
            for (int i = 0; i < 20; i++)
            {
                session.Tick();
                bursts.AddRange(bob.TakeEvents().Where(e => e.Type == SimEventType.Burst));
            }

            var burst = bursts.Single();
            Assert.AreEqual(alice.PlayerId, burst.Source);
            Assert.AreEqual("fireball", bob.SkillOf(burst.Source, burst.Amount).Id);
            Assert.Less(Vec2.Distance(burst.Position, target.Position), 1.5f);
            Assert.Less(target.Health, Monsters.Shipped.Find("slime").Health);
            Assert.IsEmpty(bob.Projectiles());
        }

        [Test]
        public void PlayersStartWithPotionsAndDrinkThemThroughTheHost()
        {
            var session = new Session();
            var alice = session.Join(1, "앨리스");
            var world = session.Host.World;
            var potion = Gear.Data.FindConsumable("healing_potion");
            session.Tick(2);

            Assert.AreEqual(3, alice.Bag.CountOf(potion), "HostConfig.StarterConsumables");
            Assert.AreSame(potion, alice.QuickConsumable);
            Assert.IsNull(alice.UseConsumableCommand(potion), "nothing to heal at full health");

            // A slime's hit, then a potion.
            world.SpawnEnemy(Monsters.Shipped.Find("slime"), alice.Position + new Vec2(0.9f, 0));
            for (int i = 0; i < World.TicksPerSecond && alice.Health >= alice.MaxHealth; i++) session.Tick();
            Assert.Less(alice.Health, alice.MaxHealth);
            session.Send(1, alice.UseConsumableCommand(potion));
            Assert.IsNull(alice.UseConsumableCommand(potion), "the wait starts on screen at once");
            var events = new List<SimEvent>();
            for (int i = 0; i < 4; i++)
            {
                session.Tick();
                events.AddRange(alice.TakeEvents());
            }

            Assert.AreEqual(2, alice.Bag.CountOf(potion));
            Assert.Greater(alice.ConsumableCooldownLeft, 9);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Healed && e.Source == alice.PlayerId));

            world.DropConsumable(potion, alice.Position + new Vec2(3, 0));
            // Others() lags the newest snapshot by the interpolation delay.
            session.Tick(10);
            Assert.AreSame(potion, alice.Others().Single(e => e.Kind == EntityKind.Drop).DropConsumable);
        }

        [Test]
        public void DugTilesReachEveryoneAndLateJoiners()
        {
            var session = new Session();
            var alice = session.Join(1, "앨리스");
            var world = session.Host.World;
            var map = world.Map;
            // The first rock east of the spawn along its row; the row up to it is open floor to walk.
            int y = TileMap.Cell(world.Spawn.Y);
            int x = TileMap.Cell(world.Spawn.X);
            while (!map.IsSolid(x, y)) x++;
            var rock = Tiles.Shipped.Find(map[x, y]).Material;
            Walk(world, world.FindPlayer(alice.PlayerId), TileMap.Center(x - 1, y));
            session.Tick(4);

            for (int i = 0; i < 10 * World.TicksPerSecond && map.IsSolid(x, y); i++)
            {
                session.Tick(1, c => (Vec2.Zero, new Vec2(1, 0), true));
            }
            session.Tick(2);

            Assert.AreEqual(TileType.Floor, map[x, y]);
            Assert.AreEqual(TileType.Floor, alice.Map[x, y], "sent to whoever is playing");
            Assert.AreEqual(1, alice.Bag.CountOf(rock));
            var bob = session.Join(2, "밥");
            Assert.AreEqual(TileType.Floor, bob.Map[x, y], "and to whoever joins later");

            // Build it back up from stone through the host.
            var stone = Gear.Data.FindMaterial("stone");
            world.DropMaterial(stone, world.FindPlayer(alice.PlayerId).Position);
            session.Tick(2);
            session.Send(1, alice.PlaceCommand(stone));
            session.Tick(2);
            Assert.AreEqual(TileType.Wall, alice.Map[x, y]);
            Assert.AreEqual(TileType.Wall, bob.Map[x, y]);
        }

        [Test]
        public void CraftingGoesThroughTheHost()
        {
            var session = new Session();
            var alice = session.Join(1, "앨리스");
            var host = session.Host.World.FindPlayer(alice.PlayerId);
            var gel = Gear.Data.FindMaterial("slime_gel");
            var potion = Gear.Data.FindConsumable("healing_potion");
            var recipe = Gear.Data.Recipes.Single(r => r.Consumable == potion);
            // Dropped at her feet, so she picks them up at once.
            for (int i = 0; i < 3; i++) session.Host.World.DropMaterial(gel, host.Position);
            session.Tick(2);

            Assert.AreEqual(3, alice.Bag.CountOf(gel));
            Assert.IsTrue(alice.CanCraft(recipe));
            session.Send(1, alice.CraftCommand(recipe));
            session.Tick(2);

            Assert.AreEqual(4, alice.Bag.CountOf(potion), "three to start with and one made");
            Assert.AreEqual(0, alice.Bag.CountOf(gel));
            Assert.IsFalse(alice.CanCraft(recipe));

            session.Host.World.DropMaterial(gel, alice.Position + new Vec2(3, 0));
            session.Tick(10);
            Assert.AreSame(gel, alice.Others().Single(e => e.Kind == EntityKind.Drop).DropMaterial);
        }

        [Test]
        public void CropsGrowTheSameForEveryone()
        {
            var session = new Session();
            var alice = session.Join(1, "앨리스");
            var world = session.Host.World;
            var mushroom = Tiles.Shipped.Crops.Single();
            var spore = mushroom.Seed;
            session.Tick(2);

            Assert.AreEqual(2, alice.Bag.CountOf(spore), "HostConfig.StarterMaterials");
            Assert.AreEqual(6, alice.Bag.CountOf(Gear.Data.FindMaterial("torch")), "torches for the dark");
            Assert.AreSame(mushroom, alice.CropFrom(spore));
            session.Send(1, alice.PlantCommand(mushroom));
            session.Tick(2);

            var crop = alice.Crops.Single();
            Assert.AreSame(mushroom, crop.Definition);
            Assert.AreEqual(1, alice.Bag.CountOf(spore));
            var bob = session.Join(2, "밥");
            Assert.AreEqual((crop.X, crop.Y, crop.PlantedTick), (bob.Crops.Single().X, bob.Crops.Single().Y, bob.Crops.Single().PlantedTick),
                "a late joiner gets what is growing");

            session.Tick(mushroom.GrowTicks / 2);
            Assert.AreEqual(0.5f, alice.Crops.Single().Growth(alice.HostTick), 0.05f, "worked out from the tick, not sent");

            // Alice walks onto it once it is ripe; everyone sees it go.
            session.Tick(mushroom.GrowTicks / 2);
            Walk(world, world.FindPlayer(alice.PlayerId), TileMap.Center(crop.X, crop.Y));
            session.Tick(4);
            Assert.IsEmpty(alice.Crops);
            Assert.IsEmpty(bob.Crops);
            Assert.AreEqual(2, alice.Bag.CountOf(Gear.Data.FindMaterial("mushroom")));
        }

        [Test]
        public void LeavingRemovesThePlayerForEveryone()
        {
            var session = new Session();
            var alice = session.Join(1, "앨리스");
            var bob = session.Join(2, "밥");
            session.Tick(4);

            session.Host.Disconnect(2);
            session.Clients.Remove(2);
            session.Tick(10);

            Assert.IsNull(session.Host.World.FindPlayer(bob.PlayerId));
            CollectionAssert.AreEqual(new[] { "앨리스" }, alice.Names.Values);
            Assert.IsFalse(alice.Others().Any(e => e.Id == bob.PlayerId));
        }

        [Test]
        public void SnapshotsOnlyCarryEnemiesNearTheViewer()
        {
            var session = new Session(new HostConfig { ViewRange = 10 });
            var alice = session.Join(1, "앨리스");
            var world = session.Host.World;
            var near = world.SpawnEnemy(Monsters.Shipped.Find("slime"), world.Spawn + new Vec2(3, 0));
            var far = world.SpawnEnemy(Monsters.Shipped.Find("slime"), world.Spawn + new Vec2(30, 0));

            session.Tick(10);

            var seen = alice.Others().Select(e => e.Id).ToList();
            CollectionAssert.Contains(seen, near.Id);
            CollectionAssert.DoesNotContain(seen, far.Id);
        }

        [Test]
        public void SnapshotsStayInsideOnePacketInABigFight()
        {
            var session = new Session();
            session.Join(1, "앨리스");
            var host = session.Host;
            for (int i = 0; i < 60; i++) host.World.SpawnEnemy(Monsters.Shipped.Find("slime"), host.World.Spawn + Vec2.FromAngle(i) * 1.2f);
            host.World.SetAttacking(host.World.Players[0].Id, true);

            int largest = 0;
            for (int tick = 0; tick < 60; tick++)
            {
                host.Tick();
                foreach (var message in host.TakeOutgoing()) largest = System.Math.Max(largest, message.Data.Length);
            }

            Assert.LessOrEqual(largest, 1200);
        }

        // Puts a player somewhere on the host at once, in steps the host accepts; its client is corrected after.
        static void Walk(World world, Player player, Vec2 to)
        {
            for (int i = 0; i < 100 && Vec2.Distance(player.Position, to) > 0.001f; i++)
            {
                var step = (to - player.Position).ClampLength(2);
                Assert.IsTrue(world.MovePlayer(player.Id, player.Position + step, step, 10));
            }
        }

        [Test]
        public void SnapshotsStayInsideOnePacketInABossFight()
        {
            // Strong gear, so the four of them get the boss through its phases while slimes crowd in, and every skill
            // used as soon as it is ready for fireballs and bursts on top.
            var session = new Session(new HostConfig { StarterGear = new[] { "obsidian_sword", "obsidian_armor" } }, Bosses.Shipped);
            var host = session.Host;
            var world = host.World;
            var boss = world.Bosses.Single();
            for (ulong i = 1; i <= 4; i++) session.Join(i, "플레이어" + i, Protocol.Version, i % 2 == 0 ? "warrior" : "fire_mage", i % 2 == 0 ? "fire_mage" : "warrior");
            for (int i = 0; i < 4; i++) Walk(world, world.Players[i], boss.Home + Vec2.FromAngle(i * 1.6f) * 2.5f);
            for (int i = 0; i < 40; i++) world.SpawnEnemy(Monsters.Shipped.Find("slime"), boss.Home + Vec2.FromAngle(i) * 6);
            for (int i = 0; i < 30; i++) world.DropItem(Gear.Plain("iron_sword"), boss.Home + Vec2.FromAngle(i) * 3);

            int largest = 0;
            var seen = new HashSet<SimEventType>();
            for (int tick = 0; tick < 60 * World.TicksPerSecond && world.Bosses.Count > 0; tick++)
            {
                foreach (var player in world.Players)
                {
                    world.MovePlayer(player.Id, player.Position, boss.Position - player.Position, 1);
                    world.SetAttacking(player.Id, true);
                    for (int slot = 0; slot < player.Skills.Count; slot++) world.UseSkill(player.Id, slot, boss.Position - player.Position);
                }
                host.Tick();
                foreach (var message in host.TakeOutgoing())
                {
                    largest = Math.Max(largest, message.Data.Length);
                    if (Protocol.TypeOf(message.Data) == MessageType.Snapshot)
                    {
                        foreach (var e in SnapshotMessage.Decode(message.Data).Events) seen.Add(e.Type);
                    }
                }
            }

            Assert.LessOrEqual(largest, 1200);
            Assert.IsTrue(seen.Contains(SimEventType.Burst));
            Assert.IsTrue(seen.Contains(SimEventType.PhaseChanged), $"the fight got far enough to matter: boss {(world.Bosses.Count > 0 ? world.Bosses[0].Health + "/" + world.Bosses[0].MaxHealth + " phase " + world.Bosses[0].Phase + " " + world.Bosses[0].State : "gone")}, players {string.Join(",", world.Players.Select(p => p.Health))}, events {string.Join(",", seen)}");
        }

        [Test]
        public void ClientsSeeTheBossAndItsTelegraphsCountingDown()
        {
            var session = new Session(bosses: Bosses.Shipped);
            var alice = session.Join(1, "앨리스");
            var world = session.Host.World;
            var boss = world.Bosses.Single();
            Walk(world, world.FindPlayer(alice.PlayerId), boss.Home + new Vec2(0, -4));

            var progress = new List<float>();
            for (int i = 0; i < 5 * World.TicksPerSecond && progress.Count < 5; i++)
            {
                session.Tick();
                var hazards = alice.Hazards();
                if (hazards.Count > 0) progress.Add(hazards[0].Progress);
            }

            var seenBoss = alice.Others().Single(e => e.Kind == EntityKind.Boss);
            Assert.AreEqual(boss.Definition, seenBoss.Boss);
            Assert.AreNotEqual(BossState.Dormant, seenBoss.BossState);
            Assert.AreEqual(5, progress.Count, "a telegraph showed up");
            Assert.That(progress, Is.Ordered);
            Assert.Greater(progress[4], progress[0]);
        }

        [Test]
        public void ProjectilesAreDrawnWhereTheyAreNow()
        {
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Shipped);
            client.Receive(new WelcomeMessage(1, 7, 32, 32, new Vec2(5, 5)).Encode());
            client.Receive(new SnapshotMessage(
                100,
                new SelfState(0, new Vec2(5, 5), 100, 100, 5),
                new EntityState[0],
                new[] { new ProjectileState(3, new Vec2(10, 10), new Vec2(6, 0), 0.3f) },
                new HazardState[0],
                new SimEvent[0]).Encode());

            client.AdvanceRenderClock(0.1f);
            Assert.AreEqual(10.6f, client.Projectiles().Single().Position.X, 0.02f);

            // Without news from the host, the guess stops after a quarter of a second.
            client.AdvanceRenderClock(1);
            Assert.AreEqual(11.5f, client.Projectiles().Single().Position.X, 0.02f);
        }

        [Test]
        public void ItemsSurviveEncodingWithAffixesSocketsAndRunewords()
        {
            var sword = Gear.Plain("copper_sword", 2, Rarity.Common, Gear.Affix("sharp", 2));
            sword.TryInsertRune(0, Gear.Data.FindRune("wind"), Gear.Data.Runewords);
            sword.TryInsertRune(1, Gear.Data.FindRune("spark"), Gear.Data.Runewords);
            var bag = new Inventory();
            bag.TryAdd(sword);
            bag.TryAdd(Gear.Plain("iron_armor", 1, Rarity.Rare, Gear.Affix("sturdy", 3), Gear.Affix("giant", 5)));
            bag.AddRune(Gear.Data.FindRune("star"), 3);
            var equipment = new Equipment();
            equipment.Equip(Gear.Plain("copper_ring"));

            var decoded = InventoryMessage.Decode(new InventoryMessage(equipment, bag).Encode(Gear.Data), Gear.Data);

            Assert.AreEqual(2, decoded.Bag.Items.Count);
            CollectionAssert.AreEqual(ItemText.Describe(bag.Items[0]), ItemText.Describe(decoded.Bag.Items[0]));
            CollectionAssert.AreEqual(ItemText.Describe(bag.Items[1]), ItemText.Describe(decoded.Bag.Items[1]));
            Assert.AreEqual("storm", decoded.Bag.Items[0].Runeword?.Id);
            Assert.AreEqual(3, decoded.Bag.CountOf(Gear.Data.FindRune("star")));
            Assert.AreEqual("copper_ring", decoded.Equipment[ItemSlot.Ring].Base.Id);
            Assert.IsNull(decoded.Equipment[ItemSlot.Weapon]);
        }

        [Test]
        public void ClientsLearnTheirGearAndWhatTheyPickUp()
        {
            var session = new Session();
            var alice = session.Join(1, "앨리스");
            session.Tick();

            var host = session.Host.World.FindPlayer(alice.PlayerId);
            Assert.AreEqual("copper_sword", alice.Equipment[ItemSlot.Weapon].Base.Id);
            Assert.AreEqual(host.Stats.Damage, alice.Stats.Damage);
            alice.TakeAcquired();

            session.Host.World.DropItem(Gear.Plain("iron_sword"), host.Position);
            session.Host.World.DropRune(Gear.Data.FindRune("ember"), host.Position);
            session.Tick(2);

            Assert.AreEqual("iron_sword", alice.Bag.Items.Single().Base.Id);
            CollectionAssert.AreEquivalent(new[] { "획득: [일반] 철 검", "획득: 불씨 룬" }, alice.TakeAcquired());
        }

        [Test]
        public void GearCommandsGoThroughTheHost()
        {
            var session = new Session();
            var alice = session.Join(1, "앨리스");
            var world = session.Host.World;
            var player = world.FindPlayer(alice.PlayerId);
            world.DropItem(Gear.Plain("iron_sword", sockets: 1), player.Position);
            world.DropRune(Gear.Data.FindRune("earth"), player.Position);
            session.Tick(2);
            var iron = alice.Bag.Items.Single();

            session.Host.Receive(1, alice.EquipCommand(iron.Id));
            session.Tick();
            Assert.AreEqual(iron.Id, alice.Equipment[ItemSlot.Weapon].Id);
            Assert.AreEqual("copper_sword", alice.Bag.Items.Single().Base.Id);

            session.Host.Receive(1, alice.InsertRuneCommand(iron.Id, Gear.Data.FindRune("earth")));
            session.Tick();
            Assert.AreEqual("earth", alice.Equipment[ItemSlot.Weapon].Sockets[0].Id);
            Assert.AreEqual(9 + 4, alice.Stats.Damage, 0.001f, "the earth rune adds 4 to a weapon");
            Assert.AreEqual(player.Stats.Damage, alice.Stats.Damage);

            var copper = alice.Bag.Items.Single();
            session.Host.Receive(1, alice.DiscardCommand(copper.Id));
            // Others are drawn a few ticks in the past, so wait for the drop to reach that point.
            session.Tick(10);
            Assert.AreEqual(0, alice.Bag.Items.Count);
            var drop = alice.Others().Single(e => e.Kind == EntityKind.Drop);
            Assert.AreEqual("copper_sword", drop.DropBase.Id);
            Assert.AreEqual(Rarity.Common, drop.DropRarity);

            session.Host.Receive(1, alice.UnequipCommand(ItemSlot.Weapon));
            session.Tick();
            Assert.IsNull(alice.Equipment[ItemSlot.Weapon]);
            Assert.AreEqual(iron.Id, alice.Bag.Items.Single().Id);
        }

        [Test]
        public void GarbledMessagesAreIgnored()
        {
            var session = new Session();
            var alice = session.Join(1, "앨리스");
            var random = new GameRandom(5);

            Assert.DoesNotThrow(() =>
            {
                session.Host.Receive(1, new byte[] { (byte)MessageType.Input, 1, 2 });
                session.Host.Receive(1, new byte[0]);
                session.Host.Receive(9, new byte[] { (byte)MessageType.Join, 200 });
                alice.Receive(new byte[] { (byte)MessageType.Snapshot, 1 });
                for (int i = 0; i < 200; i++)
                {
                    var noise = new byte[random.Next(1, 40)];
                    for (int b = 0; b < noise.Length; b++) noise[b] = (byte)random.Next(256);
                    session.Host.Receive(1, noise);
                    alice.Receive(noise);
                }
                session.Tick(2);
            });
        }
    }
}
