using System;
using System.Collections.Generic;
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

            public Session(HostConfig config = null) =>
                Host = new WorldHost(7, Monsters.Shipped, Gear.Data, config, new WorldConfig { EnemyTarget = 0 });

            public WorldClient Join(ulong connection, string name, string version = Protocol.Version)
            {
                var client = new WorldClient("key" + connection, name, Monsters.Shipped);
                Clients[connection] = client;
                Host.Receive(connection, new JoinMessage(client.PlayerKey, name, version).Encode());
                Deliver();
                return client;
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

            var welcome = WelcomeMessage.Decode(new WelcomeMessage(3, ulong.MaxValue, 64, 32, new Vec2(5, 6)).Encode());
            Assert.AreEqual(ulong.MaxValue, welcome.Seed);
            Assert.AreEqual(32, welcome.Height);

            var snapshot = SnapshotMessage.Decode(new SnapshotMessage(
                99,
                new SelfState(2, new Vec2(1, 2), 50, 100, 5),
                new[] { new EntityState(7, 0, new Vec2(3, 4), new Vec2(-1, 0), 12, 30, EnemyState.Windup) },
                new[] { new SimEvent(SimEventType.Hit, 1, 7, 6, critical: true) }).Encode());
            Assert.AreEqual(99, snapshot.Tick);
            Assert.AreEqual(2, snapshot.Self.Corrections);
            var entity = snapshot.Entities.Single();
            Assert.AreEqual(EnemyState.Windup, entity.State);
            Assert.AreEqual(-1, entity.Facing.X, 0.001f);
            Assert.AreEqual(0, entity.Facing.Y, 0.03f);
            Assert.IsTrue(snapshot.Events.Single().Critical);
            Assert.AreEqual(6, snapshot.Events.Single().Amount);
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
