using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // Field elites (2026-11-22, Docs/WorldRework.md 들판 정예, 묶음 2-2): the camps hold a leader's pack or a band of
    // champions with affixes, the same for the same seed, room and day; their body, names, the long strong warning of a
    // leader's big attack, what the affixes do, what they give, and what goes over the wire.
    public class EliteTests
    {
        const int Size = 512;

        static WorldHost Rooms(ulong seed) => new WorldHost(seed, Monsters.Shipped, Gear.Data,
            new HostConfig { MapSize = Size, MapVersion = WorldGenerator.RoomVersion }, tiles: Tiles.Shipped, story: Tales.Shipped);

        static World Room(int size = 30) =>
            new World(Maps.Room(size), TileMap.Center(size / 2, size / 2), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data, Tiles.Shipped);

        static List<SimEvent> Run(World world, float seconds, Action each = null)
        {
            var events = new List<SimEvent>();
            for (int i = 0; i < World.Ticks(seconds); i++)
            {
                each?.Invoke();
                world.Step();
                events.AddRange(world.TakeEvents());
            }
            return events;
        }

        static MonsterDefinition Kind(string id) => Monsters.Shipped.Find(id);

        // The elite camp nearest the spawn by rooms that holds a band (not by a road, an arena's door or a place of the
        // story), and a player standing in the room next to it, in its wake reach.
        static (WorldHost Host, RoomNode Room, Player Player) BesideCamp(ulong seed)
        {
            var host = Rooms(seed);
            var world = host.World;
            var rooms = host.Generated.Rooms;
            var camp = rooms.Nodes.Where(n => n.Kind == RoomKind.Room && n.Role == RoomRole.Elite && !n.Roadside && !world.InSafePlace(n.Center)
                    && !n.Edges.Any(i => rooms.Nodes[rooms.Edges[i].A == n.Index ? rooms.Edges[i].B : rooms.Edges[i].A].Kind == RoomKind.Arena))
                .OrderBy(n => n.Depth).ThenBy(n => n.Index).FirstOrDefault();
            if (camp == null) return (host, null, null);
            var player = world.AddPlayer("a", Gear.Wearing("obsidian_sword", "obsidian_armor"));
            player.Position = OpenNear(world, camp.Center, 22, 40);
            return (host, camp, player);
        }

        static Vec2 OpenNear(World world, Vec2 from, float least, float most)
        {
            for (float reach = least; reach <= most; reach += 0.5f)
            {
                for (int k = 0; k < 64; k++)
                {
                    var at = from + Vec2.FromAngle(2 * MathF.PI * k / 64) * reach;
                    if (!Collision.Overlaps(world.Map, at, 0.5f) && !Collision.Overlaps(world.Map, at, 0.5f, monster: true) && !world.InSafePlace(at)) return at;
                }
            }
            throw new InvalidOperationException("no open floor there");
        }

        static int Bits(RiftAffix affixes) => Enumerable.Range(0, 8).Count(i => ((int)affixes & (1 << i)) != 0);

        [Test]
        public void CampsHoldALeadersPackOrABandOfChampions()
        {
            int leaders = 0, champions = 0;
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var (host, camp, player) = BesideCamp(seed);
                if (camp == null) continue;
                var world = host.World;
                Run(world, 1);
                var pack = world.Packs.SingleOrDefault(p => p.Room == camp);
                Assert.IsNotNull(pack, $"seed {seed}: the camp's band");
                Assert.IsTrue(pack.Active, $"seed {seed}");
                Assert.AreEqual(1, world.Packs.Count(p => p.Room == camp), "a camp holds its band alone");
                var members = world.MembersOf(pack).ToList();
                var elites = members.Where(m => m.Elite != EliteRank.None).ToList();
                int zone = world.ZoneLevelAt(camp.Center);
                Assert.AreNotEqual(EliteRank.None, pack.Elite, $"seed {seed}: a camp is no ordinary pack now");
                foreach (var member in members)
                    Assert.That(Vec2.Distance(member.Position, camp.Center), Is.LessThanOrEqualTo(camp.Radius + 1.5f), "in its camp");
                if (pack.Elite == EliteRank.Leader)
                {
                    leaders++;
                    var leader = elites.Single();
                    Assert.AreEqual(EliteRank.Leader, leader.Elite);
                    Assert.AreSame(World.LeaderKind(pack.Definition), leader.Definition, "the sturdiest kind of the pack leads it");
                    Assert.That(Bits(leader.Affixes), Is.InRange(1, zone >= World.SecondAffixZone ? 2 : 1), $"seed {seed}: one or two affixes");
                    int minions = members.Count - 1;
                    int most = (leader.Affixes & RiftAffix.Swarm) != 0 ? (int)MathF.Round(World.MaxMinions * World.SwarmShare) : World.MaxMinions;
                    Assert.That(minions, Is.InRange(World.MinMinions, most), $"seed {seed}: three to five minions (half again with the swarm)");
                    // Its body: four times its kind's health as its zone made it (half again tough), its hits half again.
                    float health = leader.Definition.Health * (1 + World.ZoneHealthStep * Math.Max(0, zone - 1)) * World.LeaderHealth
                        * ((leader.Affixes & RiftAffix.Tough) != 0 ? World.ToughShare : 1);
                    Assert.AreEqual(health, leader.MaxHealth, 0.01f);
                    // On the chunk's E when there is one.
                    var mark = host.Generated.Rooms.Marks.Where(m => m.Room == camp.Index && m.Kind == RoomMarkKind.Elite).ToList();
                    if (mark.Count > 0) Assert.That(Vec2.Distance(leader.Position, TileMap.Center(mark[0].X, mark[0].Y)), Is.LessThan(2.5f), "the leader stands on E");
                }
                else
                {
                    champions++;
                    Assert.AreEqual(World.ChampionCount, elites.Count, "three champions");
                    Assert.AreEqual(members.Count, elites.Count, "and nothing else");
                    Assert.AreEqual(1, elites.Select(e => e.Definition).Distinct().Count(), "of one kind");
                    Assert.AreEqual(1, elites.Select(e => e.Affixes).Distinct().Count(), "sharing their affix");
                    Assert.AreEqual(1, Bits(elites[0].Affixes));
                    Assert.AreEqual(RiftAffix.None, elites[0].Affixes & (RiftAffix.Swarm | RiftAffix.Summon | RiftAffix.Guard), "nothing about minions");
                }
                Assert.IsTrue(members.All(m => m.State == EnemyState.Idle), "nobody seen yet");
            }
            Assert.Greater(leaders + champions, 4, "camps were found");
            Assert.Greater(leaders, 0, "leaders lead most camps");
        }

        [Test]
        public void TheSameSeedRoomAndDayMakeTheSameBandAndTheNextDayAnother()
        {
            string Band(ulong seed, int days)
            {
                var (host, camp, player) = BesideCamp(seed);
                var world = host.World;
                for (int day = 0; day < days; day++) world.RestoreClock(Calendar.NextMorning(world.Clock));
                Run(world, 1);
                var pack = world.Packs.Single(p => p.Room == camp);
                return $"{pack.Elite} {pack.Affixes} " + string.Join(";", world.MembersOf(pack).OrderBy(m => m.Id).Select(m => $"{m.Definition.Id}@{m.Position.X:0.0},{m.Position.Y:0.0}/{m.Elite}"));
            }

            ulong found = Enumerable.Range(1, 12).Select(i => (ulong)i).First(s => BesideCamp(s).Room != null);
            Assert.AreEqual(Band(found, 0), Band(found, 0), "the same band for the same seed and day");
            var days = Enumerable.Range(0, 5).Select(d => Band(found, d)).ToList();
            Assert.Greater(days.Distinct().Count(), 1, "another band on another day");
        }

        [Test]
        public void NamesAreTheAffixTheKindAndTheirRank()
        {
            var myconid = Kind("myconid");
            Assert.AreEqual("불길의 버섯 병정 대장", World.EliteName(myconid, EliteRank.Leader, RiftAffix.Fire));
            Assert.AreEqual("불길의 버섯 병정 대장", World.EliteName(myconid, EliteRank.Leader, RiftAffix.Fire | RiftAffix.Tough), "the field's affix names it");
            Assert.AreEqual("사나운 모래 전갈", World.EliteName(Kind("sand_scorpion"), EliteRank.Champion, RiftAffix.Fierce));
            Assert.IsNull(World.EliteName(myconid, EliteRank.None, RiftAffix.None));
            Assert.AreEqual("단단함 · 불길", World.EliteAffixLine(RiftAffix.Fire | RiftAffix.Tough));
            Assert.AreEqual("빠름 · 단단함 · 사나움 · 떼 · 불길 · 얼음 폭발 · 수호 · 부름", World.EliteAffixLine((RiftAffix)0xFF));
            // The rift's own four keep their names.
            Assert.AreEqual("빠름, 떼", World.AffixNames(RiftAffix.Swift | RiftAffix.Swarm | RiftAffix.Fire));
        }

        [Test]
        public void ALeadersBigAttackIsWarnedLongAndStronglyAndHitsHard()
        {
            foreach (var kind in Monsters.Shipped.Monsters.Where(m => m.Attacks.Count > 0))
            {
                var big = World.BigAttackOf(kind);
                if (kind.Id == "fire_bat") Assert.AreEqual("swoop", big.Id, "never its blast");
                if (big == null) continue;
                Assert.GreaterOrEqual(World.TelegraphOf(EliteRank.Leader, kind, big), World.EliteTelegraph, kind.Id);
                Assert.AreEqual(AttackWarning.Strong, World.WarningOf(EliteRank.Leader, kind, big), kind.Id);
                Assert.AreEqual(big.Telegraph, World.TelegraphOf(EliteRank.Champion, kind, big), "a champion's as the sheet says");
                Assert.AreEqual(big.Warning, World.WarningOf(EliteRank.None, kind, big));
                foreach (var other in kind.Attacks.Where(a => a != big))
                    Assert.AreEqual(other.Telegraph, World.TelegraphOf(EliteRank.Leader, kind, other), $"{kind.Id}.{other.Id}: only the big one");
            }
            Assert.AreEqual("spore_smash", World.BigAttackOf(Kind("myconid")).Id);
            Assert.AreEqual("pounce", World.BigAttackOf(Kind("slime")).Id);

            // In the world: a slime leader's pounce winds up a whole second and lands for half again more.
            var world = Room();
            var player = world.AddPlayer("a", new Equipment());
            var leader = world.MakeEliteHere(Kind("slime"), player.Position + new Vec2(3, 0), EliteRank.Leader, RiftAffix.Tough);
            var plain = world.SpawnEnemy(Kind("slime"), player.Position + new Vec2(-3, 0));
            world.MonstersHold = true;
            Assert.IsTrue(world.ForceAttack(leader, "pounce"));
            Assert.AreEqual(World.Ticks(World.EliteTelegraph), leader.StateTicks);
            var hazard = world.Hazards.Single(h => h.Source == leader.Id);
            var events = Run(world, 1.2f);
            int hit = events.Where(e => e.Type == SimEventType.Hit && e.Target == player.Id && e.Source == leader.Id).Sum(e => e.Amount);
            float expected = Kind("slime").Damage * World.LeaderPower * World.BigHitShare;
            Assert.AreEqual(Combat.Mitigate(MathF.Round(expected), player.Stats.Armor), hit, 1, "the big hit");
            Assert.AreEqual(hazard.Damage, (int)MathF.Round(expected));
            Assert.IsTrue(world.ForceAttack(plain, "pounce"));
            Assert.AreEqual(World.Ticks(Kind("slime").Attacks.First(a => a.Id == "pounce").Telegraph), plain.StateTicks, "an ordinary slime as ever");
        }

        [Test]
        public void TheStrongWarningGoesOverTheWireForAMarkAwayFromTheLeader()
        {
            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, null, Classes.Shipped, Tiles.Shipped);
            var client = new WorldClient("k", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped);
            host.Receive(1, client.JoinMessage());
            void Deliver()
            {
                foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            }
            Deliver();
            var world = host.World;
            var player = world.FindPlayer(client.PlayerId);
            var spot = OpenNear(world, player.Position, 4, 8);
            var leader = world.MakeEliteHere(Kind("slime"), spot, EliteRank.Leader, RiftAffix.Fire | RiftAffix.Guard, Kind("slime"), 3);
            world.MonstersHold = true;
            Assert.IsTrue(world.ForceAttack(leader, "pounce"));
            SnapshotMessage last = null;
            for (int i = 0; i < 6; i++)
            {
                host.Tick();
                foreach (var message in host.TakeOutgoing())
                {
                    client.Receive(message.Data);
                    if (Protocol.TypeOf(message.Data) == MessageType.Snapshot) last = SnapshotMessage.Decode(message.Data);
                }
                client.AdvanceRenderClock(World.TickSeconds);
            }
            Assert.IsNotNull(last);
            // The landing circle of a leader's pounce is laid away from it: its warning is strong.
            Assert.AreEqual(AttackWarning.Strong, last.Hazards.Single(h => h.Harmful).Warning);
            var sent = last.Entities.Single(e => e.Id == leader.Id);
            Assert.AreEqual(World.EliteField(leader), sent.Elite);
            Assert.IsTrue(last.Entities.Where(e => e.Kind == EntityKind.Monster && e.Id != leader.Id).All(e => e.Elite == 0), "its minions are ordinary");
            var view = client.Others().Single(v => v.Id == leader.Id);
            Assert.AreEqual(EliteRank.Leader, view.Elite);
            Assert.AreEqual(RiftAffix.Fire | RiftAffix.Guard, view.Affixes);
            Assert.AreEqual("수호의 슬라임 대장", view.EliteName);
            Assert.AreEqual(World.EliteTelegraph, view.AttackTelegraph, 1e-4f);
            Assert.AreEqual(AttackWarning.Strong, view.AttackWarning);
        }

        [Test]
        public void EliteMarksSurviveEncodingAndCostLittle()
        {
            var self = new SelfState(0, Vec2.Zero, 10, 10, 5, new List<int>(), new List<int>(), 0, -1, 0, 0, 0, -1, -1);
            var plain = new EntityState(5, EntityKind.Monster, 0, new Vec2(1, 2), new Vec2(1, 0), 10, 10, 0);
            var elite = new EntityState(6, EntityKind.Monster, 1, new Vec2(3, 2), new Vec2(1, 0), 50, 60, 0, 0, (int)EliteRank.Champion << 8 | (int)(RiftAffix.Frost));
            byte[] without = new SnapshotMessage(1, self, new[] { plain, plain }, new ProjectileState[0], new HazardState[0], new SimEvent[0]).Encode();
            byte[] with = new SnapshotMessage(1, self, new[] { plain, elite }, new ProjectileState[0], new HazardState[0], new SimEvent[0]).Encode();
            Assert.AreEqual(without.Length + 4, with.Length, "an elite costs four bytes, an ordinary monster nothing");
            var decoded = SnapshotMessage.Decode(with);
            Assert.AreEqual(0, decoded.Entities[0].Elite);
            Assert.AreEqual(elite.Elite, decoded.Entities[1].Elite);
            Assert.AreEqual(50, decoded.Entities[1].Health, 1);
        }

        [Test]
        public void TheAffixesDoWhatTheySay()
        {
            var world = Room(40);
            var player = world.AddPlayer("a", Gear.Wearing("obsidian_sword", "obsidian_armor"));
            var slime = Kind("slime");

            // Swift, tough, fierce: pace, health, hits.
            var plain = world.MakeEliteHere(slime, player.Position + new Vec2(-12, -12), EliteRank.Champion, RiftAffix.Frost);
            var strong = world.MakeEliteHere(slime, player.Position + new Vec2(12, -12), EliteRank.Champion, RiftAffix.Swift);
            Assert.AreEqual(plain.Speed * World.SwiftShare, strong.Speed, 1e-3f);
            var tough = world.MakeEliteHere(slime, player.Position + new Vec2(-12, 12), EliteRank.Leader, RiftAffix.Tough | RiftAffix.Fierce);
            Assert.AreEqual(slime.Health * World.LeaderHealth * World.ToughShare, tough.MaxHealth, 0.01f);
            Assert.AreEqual(World.LeaderPower * World.FierceShare, tough.Power, 1e-4f);
            Assert.AreEqual(slime.Health * World.ChampionHealth, plain.MaxHealth, 0.01f);

            // Guard: a minion beside its guarding leader takes less; one far from it the whole blow.
            var world2 = Room(40);
            var hitter = world2.AddPlayer("b", Gear.Wearing("obsidian_sword"));
            var beetle = Kind("cave_beetle");
            var leader = world2.MakeEliteHere(Kind("myconid"), hitter.Position + new Vec2(6, 0), EliteRank.Leader, RiftAffix.Guard, Kind("slime"), 2);
            var minions = world2.Enemies.Where(e => e.PackId == leader.PackId && e != leader).ToList();
            foreach (var m in minions) m.MaxHealth = m.Health = 1000;
            var near = minions[0];
            Assert.AreSame(leader, world2.GuardOf(near));
            float before = near.Health;
            world2.Hurt(near, hitter, 100);
            float guarded = before - near.Health;
            leader.Position += new Vec2(0, 12);
            Assert.IsNull(world2.GuardOf(near));
            before = near.Health;
            world2.Hurt(near, hitter, 100);
            float open = before - near.Health;
            Assert.AreEqual(open * (1 - World.EliteGuardShare), guarded, 1.5f, "forty per cent less beside the leader");

            // Summon: three more at two thirds and three more at a third.
            var world3 = Room(40);
            var caller = world3.AddPlayer("c", Gear.Wearing("obsidian_sword"));
            var summoner = world3.MakeEliteHere(Kind("myconid"), caller.Position + new Vec2(5, 0), EliteRank.Leader, RiftAffix.Summon, Kind("glow_bat"), 3);
            world3.MonstersHold = true;
            int Pack() => world3.Enemies.Count(e => e.PackId == summoner.PackId && !e.IsDead);
            Assert.AreEqual(4, Pack());
            summoner.Health = summoner.MaxHealth * 0.6f;
            var events = Run(world3, 0.2f);
            Assert.AreEqual(4 + World.SummonCount, Pack(), "three called");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxCall));
            Assert.IsTrue(world3.Enemies.Where(e => e.PackId == summoner.PackId && e != summoner).All(e => e.Definition.Id == "glow_bat"), "of its minions' kind");
            Run(world3, 0.5f);
            Assert.AreEqual(4 + World.SummonCount, Pack(), "once for each step down");
            summoner.Health = summoner.MaxHealth * 0.3f;
            Run(world3, 0.2f);
            Assert.AreEqual(4 + 2 * World.SummonCount, Pack());

            // Frost: a circle under it every few seconds while it fights, going off a moment later; fire where it walks.
            var world4 = Room(40);
            var target = world4.AddPlayer("d", Gear.Wearing("obsidian_armor"));
            var icy = world4.MakeEliteHere(Kind("cave_beetle"), target.Position + new Vec2(6, 0), EliteRank.Champion, RiftAffix.Frost);
            var burning = world4.MakeEliteHere(Kind("myconid"), target.Position + new Vec2(-5, 3), EliteRank.Champion, RiftAffix.Fire);
            int frosts = 0, fires = 0;
            Run(world4, World.FrostSeconds + 1.5f, () =>
            {
                frosts = Math.Max(frosts, world4.Hazards.Count(h => h.Source == icy.Id && h.Shape == HazardShape.Circle && h.Size == World.FrostRadius));
                fires += world4.Hazards.Count(h => burning.PackId != 0 && world4.Enemies.Any(e => e.Id == h.Source && e.PackId == burning.PackId) && h.Shape == HazardShape.Fire) > 0 ? 1 : 0;
            });
            Assert.That(icy.State, Is.Not.EqualTo(EnemyState.Idle), "it came for the player");
            Assert.GreaterOrEqual(frosts, 1, "a frost circle");
            Assert.Greater(fires, 0, "fire where it walked");
            Assert.AreEqual(0, world4.Hazards.Count(h => h.Source == plain.Id), "the other world's elites are not here");
        }

        [Test]
        public void AFelledLeaderGivesARarePieceAPotionAndEightTimesTheExperience()
        {
            var world = Room();
            var player = world.AddPlayer("a", Gear.Wearing("obsidian_sword"));
            var slime = Kind("slime");
            var plain = world.SpawnEnemy(slime, player.Position + new Vec2(2, 0));
            int before = player.Experience;
            world.Hurt(plain, player, 10000);
            int one = player.Experience - before;
            Assert.Greater(one, 0);
            world.Step();

            var leader = world.MakeEliteHere(slime, player.Position + new Vec2(2, 0), EliteRank.Leader, RiftAffix.Fierce);
            int floor = world.GroundLoot.Count;
            before = player.Experience;
            world.Hurt(leader, player, 100000);
            Assert.AreEqual(one * World.LeaderExperience, player.Experience - before);
            var dropped = world.GroundLoot.Skip(floor).ToList();
            Assert.IsTrue(dropped.Any(l => l.Item != null && l.Item.Rarity >= Rarity.Rare), "a rare or better piece, always");
            Assert.IsTrue(dropped.Any(l => l.Consumable != null), "a potion");
            world.Step();

            var champion = world.MakeEliteHere(slime, player.Position + new Vec2(2, 0), EliteRank.Champion, RiftAffix.Swift);
            before = player.Experience;
            world.Hurt(champion, player, 100000);
            Assert.AreEqual(one * World.ChampionExperience, player.Experience - before);
            // Over many champions, about two in five drop a magic or better piece.
            int pieces = 0;
            for (int i = 0; i < 60; i++)
            {
                world.Step();
                floor = world.GroundLoot.Count;
                var one2 = world.MakeEliteHere(slime, player.Position + new Vec2(2, 0), EliteRank.Champion, RiftAffix.Swift);
                world.Hurt(one2, player, 100000);
                if (world.GroundLoot.Skip(floor).Any(l => l.Item != null && l.Item.Rarity >= Rarity.Uncommon)) pieces++;
            }
            Assert.That(pieces, Is.InRange(15, 50));
        }

        [Test]
        public void ALeaderBlowsItselfUpNeverAndAFireBatLeaderSwoops()
        {
            var world = Room(40);
            var player = world.AddPlayer("a", Gear.Wearing("obsidian_armor"));
            var bat = world.MakeEliteHere(Kind("fire_bat"), player.Position + new Vec2(4, 0), EliteRank.Leader, RiftAffix.Tough);
            bat.Health = bat.MaxHealth * 0.1f;
            Run(world, 8, () => player.Health = player.Stats.MaxHealth);
            Assert.IsFalse(bat.IsDead, "no dive bomb");
            Assert.IsTrue(world.Enemies.Contains(bat));
        }
    }
}
