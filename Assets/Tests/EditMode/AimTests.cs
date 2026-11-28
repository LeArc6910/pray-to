using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // What the host tells a screen before it happens (0.113.0, Docs/EffectsRework.md 14절): the angle a boss's burst or
    // spiral will start at, whom a monster's single shots are aimed at, where a player's projectile will end, and the
    // element a hit struck with. The host and a client must see the same, and the fight must go as it did.
    public class AimTests
    {
        sealed class Table
        {
            public readonly WorldHost Host;
            public readonly WorldClient Client;
            public World World => Host.World;
            public Player Player => World.FindPlayer(Client.PlayerId);

            public Table(params string[] classes)
            {
                Host = new WorldHost(7, Monsters.Shipped, Gear.Data, new HostConfig { StarterGear = new[] { "obsidian_sword", "obsidian_armor" } },
                    new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped);
                Client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, classes, Tiles.Shipped);
                Host.Receive(1, Client.JoinMessage());
                Deliver();
                Tick();
            }

            public void Deliver()
            {
                foreach (var message in Host.TakeOutgoing())
                    if (message.Connection == 1) Client.Receive(message.Data);
            }

            // The player stands still; the host moves on and the client hears of it. What the client draws of the others
            // runs a few ticks behind the host (WorldClient.InterpolationDelayTicks), so a look follows a Settle.
            public List<SimEvent> Tick(int ticks = 1)
            {
                var events = new List<SimEvent>();
                for (int i = 0; i < ticks; i++)
                {
                    Host.Tick();
                    Deliver();
                    Client.AdvanceRenderClock(World.TickSeconds);
                    events.AddRange(Client.TakeEvents());
                }
                return events;
            }

            public EntityView Seen(int id) => Client.Others().First(e => e.Id == id);

            public void Settle() => Tick(8);

            // A boss of the shipped sheet set down by the player and brought to where it picks its attacks.
            public Boss Wake(string id)
            {
                var boss = World.PlaceBoss(Bosses.Shipped.Bosses.First(b => b.Id == id), Player.Position + new Vec2(0, 3), 9);
                boss.MaxHealth = boss.Health = 1e7f;
                for (int i = 0; i < 20 * World.TicksPerSecond && (boss.State == BossState.Dormant || boss.State == BossState.Enter); i++) Tick();
                Player.Health = Player.Stats.MaxHealth;
                return boss;
            }
        }

        static float Turn(float a, float b)
        {
            float d = Math.Abs(a - b) % (2 * MathF.PI);
            return Math.Min(d, 2 * MathF.PI - d);
        }

        [TestCase("cave_warden", "rock_burst")]
        [TestCase("crystal_queen", "shard_ring")]
        public void ABurstAllRoundStartsWhereTheWindUpSaidItWould(string bossId, string attackId)
        {
            var table = new Table();
            var boss = table.Wake(bossId);
            Assert.IsTrue(table.World.ForceAttack(boss, attackId), boss.State.ToString());
            table.Settle();

            // Settled as the wind-up began, and the client sees the same angle while it lasts.
            var seen = table.Seen(boss.Id);
            Assert.AreEqual(BossState.Telegraph, seen.BossState);
            Assert.IsTrue(seen.AimsRound);
            float aim = World.AimAngle(boss.AimStep);
            Assert.AreEqual(aim, seen.AimAngle, 1e-6f);

            var before = new HashSet<int>(table.World.Projectiles.Select(p => p.Id));
            for (int i = 0; i < 10 * World.TicksPerSecond && boss.State == BossState.Telegraph; i++)
            {
                Assert.AreEqual(aim, table.Seen(boss.Id).AimAngle, 1e-6f, "the angle holds through the wind-up");
                table.Tick();
            }

            // The first ring flies on those lines: one shot along the angle said, the rest evenly round from it.
            var shots = table.World.Projectiles.Where(p => !before.Contains(p.Id) && p.Source == boss.Id).ToList();
            int count = boss.Attack.Count;
            Assert.AreEqual(count, shots.Count);
            for (int i = 0; i < count; i++)
            {
                float way = aim + 2 * MathF.PI * i / count;
                Assert.IsTrue(shots.Any(s => Turn(s.Velocity.Angle, way) < 1e-3f), $"a shot along line {i}");
            }
            table.Settle();
            Assert.IsFalse(table.Seen(boss.Id).AimsRound, "nothing is aimed once the shots are out");
        }

        // The rocks a burrow or a leap flings (0.114.0): the angle is settled as the wind-up begins like a burst's, and
        // they leave from where the boss comes down. The client hears the angle and finds the spot (the circle the
        // boss marked), so its lines are the ways the rocks go; the rocks themselves fly as they did.
        [TestCase("vein_eater", "burrow")]
        [TestCase("vein_eater", "quake")]
        [TestCase("black_knight", "leap")]
        public void ALeapsRocksLeaveWhereAndAsTheWindUpSaidTheyWould(string bossId, string attackId)
        {
            var table = new Table();
            var boss = table.Wake(bossId);
            Assert.IsTrue(table.World.ForceAttack(boss, attackId), boss.State.ToString());
            table.Settle();

            var seen = table.Seen(boss.Id);
            Assert.AreEqual(BossState.Telegraph, seen.BossState);
            Assert.IsTrue(seen.AimsRound);
            float aim = World.AimAngle(boss.AimStep);
            Assert.AreEqual(aim, seen.AimAngle, 1e-6f);
            var down = table.Client.ComesDownAt(seen);
            Assert.IsNotNull(down, "the client finds where it will come down");
            Assert.Less(Vec2.Distance(boss.LandAt, down.Value), 0.02f, "and it is where the host will put it");

            var before = new HashSet<int>(table.World.Projectiles.Select(p => p.Id));
            for (int i = 0; i < 10 * World.TicksPerSecond && boss.State == BossState.Telegraph; i++) table.Tick();
            var rocks = table.World.Projectiles.Where(p => !before.Contains(p.Id) && p.Source == boss.Id).ToList();
            int count = boss.Attack.Count;
            Assert.AreEqual(count, rocks.Count);
            Assert.Less(Vec2.Distance(boss.Position, down.Value), 0.02f, "it came down there");
            for (int i = 0; i < count; i++)
            {
                float way = aim + 2 * MathF.PI * i / count;
                Assert.IsTrue(rocks.Any(s => Turn(s.Velocity.Angle, way) < 1e-3f), $"a rock along line {i}");
            }
            table.Settle();
            var after = table.Seen(boss.Id);
            Assert.IsFalse(after.AimsRound, "nothing is aimed once the rocks are out");
            Assert.IsNull(table.Client.ComesDownAt(after));
        }

        // A leap that flings nothing (the samurai's step) sends no angle.
        [Test]
        public void ALeapWithoutRocksSendsNoAngle()
        {
            var table = new Table();
            var boss = table.Wake("shrine_samurai");
            Assert.IsTrue(table.World.ForceAttack(boss, "shunpo"), boss.State.ToString());
            table.Settle();
            Assert.AreEqual(BossState.Telegraph, table.Seen(boss.Id).BossState);
            Assert.IsFalse(table.Seen(boss.Id).AimsRound);
        }

        [Test]
        public void TheSettledAngleIsTheSameForTheSameSeedAndNotAlwaysTheSame()
        {
            int[] Run()
            {
                var table = new Table();
                var boss = table.Wake("cave_warden");
                var steps = new List<int>();
                for (int i = 0; i < 4; i++)
                {
                    Assert.IsTrue(table.World.ForceAttack(boss, "rock_burst"));
                    steps.Add(boss.AimStep);
                    table.Tick(7);
                }
                return steps.ToArray();
            }

            var first = Run();
            CollectionAssert.AreEqual(first, Run());
            Assert.Greater(first.Distinct().Count(), 1);
            Assert.IsTrue(first.All(s => s >= 0 && s < World.AimSteps));
        }

        // A fan keeps the way the boss faced, which the screen knows already: nothing more is sent for it.
        [Test]
        public void AFanSendsNoAngle()
        {
            var table = new Table();
            var boss = table.Wake("vein_eater");
            Assert.IsTrue(table.World.ForceAttack(boss, "rock_throw"));
            table.Settle();
            Assert.AreEqual(BossState.Telegraph, table.Seen(boss.Id).BossState);
            Assert.IsFalse(table.Seen(boss.Id).AimsRound);
        }

        [TestCase("crystal_wisp", "prism")]
        [TestCase("void_wisp", "orb")]
        public void ASingleShotsTargetIsToldAndTheShotIsStillAimedAsItLeaves(string monsterId, string attackId)
        {
            var table = new Table();
            var world = table.World;
            var player = table.Player;
            var monster = world.SpawnEnemy(Monsters.Shipped.Find(monsterId), player.Position + new Vec2(-3, 0));
            monster.MaxHealth = monster.Health = 1e6f;
            table.Tick();
            Assert.IsTrue(world.ForceAttack(monster, attackId));
            table.Settle();

            var seen = table.Seen(monster.Id);
            Assert.AreEqual(EnemyState.Windup, seen.EnemyState);
            Assert.AreEqual(player.Id, seen.AimTarget, "the client is told whom the shot is for");
            Assert.AreEqual(table.Client.PlayerId, seen.AimTarget);

            // The player steps aside during the wind-up; the shot leaves toward where they are by then, as before.
            var aside = player.Position + new Vec2(0.5f, 1.5f);
            for (int i = 0; i < 20 && Vec2.Distance(player.Position, aside) > 0.01f; i++)
                world.MovePlayer(player.Id, player.Position + (aside - player.Position).ClampLength(0.3f), aside - player.Position, 10);
            Projectile shot = null;
            for (int i = 0; i < 5 * World.TicksPerSecond && shot == null; i++)
            {
                var at = (Monster: monster.Position, Player: player.Position);
                table.Tick();
                shot = world.Projectiles.FirstOrDefault(p => p.Source == monster.Id);
                if (shot != null) Assert.Less(Turn(shot.Velocity.Angle, (at.Player - at.Monster).Angle), 0.02f, "aimed at the target as it left");
                else Assert.AreEqual(player.Id, table.Seen(monster.Id).AimTarget, "told all through the wind-up");
            }
            Assert.IsNotNull(shot);
            table.Settle();
            Assert.AreEqual(0, table.Seen(monster.Id).AimTarget, "nothing is aimed once the shot is out");
        }

        // A fan keeps the way it faced when it began; no target is sent for it.
        [Test]
        public void AFanOfShotsNamesNoTarget()
        {
            var fan = Monsters.Shipped.Monsters.SelectMany(m => m.Attacks.Select(a => (Monster: m, Attack: a))).First(p => p.Attack.Shape == MonsterAttackShape.Spread);
            var table = new Table();
            var monster = table.World.SpawnEnemy(fan.Monster, table.Player.Position + new Vec2(-3, 0));
            table.Tick();
            Assert.IsTrue(table.World.ForceAttack(monster, fan.Attack.Id));
            table.Settle();
            Assert.AreEqual(EnemyState.Windup, table.Seen(monster.Id).EnemyState);
            Assert.AreEqual(0, table.Seen(monster.Id).AimTarget);
        }

        // Where the meteor bursts is where the host and the client said it would, from the moment it is thrown.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void AMeteorEndsWhereBothSidesSaidItWould(int what)
        {
            var table = new Table("fire_mage");
            var world = table.World;
            var player = table.Player;
            World.TeachAll(player, 1);
            player.Mana = player.Stats.MaxMana;
            int slot = player.Skills.ToList().FindIndex(s => s.Id == "meteor");
            Assert.GreaterOrEqual(slot, 0);
            var skill = player.Skills[slot];
            float range = skill.Range;

            // 0: open ground to the end of its range; 1: a wall in the way; 2: a monster standing in the way.
            var ways = Enumerable.Range(0, 64).Select(i => Vec2.FromAngle(i * MathF.PI / 32)).ToList();
            bool Clear(Vec2 way) => Enumerable.Range(1, (int)(range * 4) + 6).All(i => !Collision.Overlaps(world.Map, player.Position + way * (i * 0.25f), 0.6f));
            bool Walled(Vec2 way) => Enumerable.Range(1, (int)((range - 2) * 4)).Any(i => Collision.Overlaps(world.Map, player.Position + way * (i * 0.25f), 0.1f));
            var aim = what == 1 ? ways.First(Walled) : ways.First(Clear);
            if (what == 2)
            {
                var slime = world.SpawnEnemy(Monsters.Shipped.Find("slime"), player.Position + aim * 4);
                slime.MaxHealth = slime.Health = 1e6f;
                world.MonstersHold = true;
            }
            table.Tick();
            player.Mana = player.Stats.MaxMana;
            Assert.IsTrue(world.UseSkill(player.Id, slot, aim), $"mana {player.Mana} of {player.ManaCost(skill)}, cooldown {player.CooldownTicks(slot)}");
            // Until a snapshot with it reaches the client (they go out every other tick): then both sides are asked.
            table.Tick();
            if (!table.Client.Projectiles().Any(p => p.Friendly)) table.Tick();
            var shot = world.Projectiles.Single(p => p.Friendly);
            var said = World.Landing(world.Map, shot.Position, shot.Velocity, shot.Radius, shot.RemainingTicks, world.ShotStoppers());
            var view = table.Client.Projectiles().Single(p => p.Friendly);
            Assert.AreEqual(shot.RemainingTicks, view.TicksLeft);
            var seen = table.Client.LandingOf(view);
            // The wire carries places in 64ths of a tile and the speed in less; the two marks are the same to the eye.
            Assert.Less(Vec2.Distance(said, seen), 0.35f, $"host {said.X},{said.Y} client {seen.X},{seen.Y}");

            Vec2? burst = null;
            for (int i = 0; i < 10 * World.TicksPerSecond && burst == null; i++)
            {
                // And it holds on the way: asked again mid-flight, the answer is the same place.
                var flying = world.Projectiles.FirstOrDefault(p => p.Friendly);
                if (flying != null)
                    Assert.Less(Vec2.Distance(said, World.Landing(world.Map, flying.Position, flying.Velocity, flying.Radius, flying.RemainingTicks, world.ShotStoppers())), 1e-3f);
                world.Step();
                var found = world.TakeEvents().Where(e => e.Type == SimEventType.Burst).ToList();
                if (found.Count > 0) burst = found[0].Position;
            }
            Assert.IsNotNull(burst);
            Assert.Less(Vec2.Distance(said, burst.Value), 1e-3f, "the host bursts it exactly where it said");
            float flown = Vec2.Distance(burst.Value, player.Position);
            if (what == 0) Assert.AreEqual(range, flown, 0.6f);
            else Assert.Less(flown, range - 0.5f, "stopped short of its range");
        }

        [Test]
        public void ASkillsProjectileStrikesWithItsElementAndABareSwingWithNone()
        {
            var table = new Table("fire_mage");
            var world = table.World;
            var player = table.Player;
            World.TeachAll(player, 1);
            player.Mana = player.Stats.MaxMana;
            world.MonstersHold = true;
            var far = world.SpawnEnemy(Monsters.Shipped.Find("slime"), player.Position + new Vec2(5, 0));
            far.MaxHealth = far.Health = 1e6f;
            table.Tick();
            int slot = player.Skills.ToList().FindIndex(s => s.Id == "fireball");
            Assert.IsTrue(world.UseSkill(player.Id, slot, new Vec2(1, 0)));
            // Long after the cast: the hit itself says fire.
            var hits = table.Tick(World.Ticks(1.5f)).Where(e => e.Type == SimEventType.Hit && e.Target == far.Id).ToList();
            Assert.IsNotEmpty(hits);
            // (The burn that follows ticks with no tags of its own.)
            Assert.AreEqual(SkillTag.Fire, hits[0].Tags, "the client hears the element, and only the element");

            var near = world.SpawnEnemy(Monsters.Shipped.Find("slime"), player.Position + new Vec2(-0.9f, 0));
            near.MaxHealth = near.Health = 1e6f;
            world.MovePlayer(player.Id, player.Position, new Vec2(-1, 0), 1);
            world.SetAttacking(player.Id, true);
            var swings = table.Tick(World.Ticks(1.5f)).Where(e => e.Type == SimEventType.Hit && e.Target == near.Id).ToList();
            Assert.IsNotEmpty(swings);
            Assert.IsTrue(swings.All(h => h.Tags == SkillTag.None));
        }

        [Test]
        public void TheNewFieldsSurviveTheWire()
        {
            var self = new SelfState(0, new Vec2(10, 10), 100, 100, 5, new List<int>(), new List<int>(), 0, -1, 0, 0, 0, -1, -1, 0, 0, -1, 0, 0);
            var entities = new List<EntityState>
            {
                new EntityState(5, EntityKind.Boss, 0, new Vec2(11, 10), new Vec2(1, 0), 500, 500, (byte)BossState.Telegraph, aim: 700),
                new EntityState(6, EntityKind.Monster, 1, new Vec2(12, 10), new Vec2(1, 0), 50, 50, 0, elite: 0x0102),
                new EntityState(7, EntityKind.Monster, 1, new Vec2(13, 10), new Vec2(1, 0), 50, 50, 0, elite: 0x0102, aim: 123456),
            };
            var projectiles = new List<ProjectileState>
            {
                new ProjectileState(9, new Vec2(10, 11), new Vec2(8, 0), 0.3f, true, SkillTag.Magic | SkillTag.Fire, 37),
                new ProjectileState(10, new Vec2(10, 12), new Vec2(8, 0), 0.3f),
            };
            var events = new List<SimEvent>
            {
                new SimEvent(SimEventType.Hit, 1, 6, 12, true, cause: World.ElementalCause + 2, tags: SkillTag.Lightning),
                new SimEvent(SimEventType.Hit, 1, 6, 30, false, cause: 3, tags: SkillTag.Magic | SkillTag.Fire | SkillTag.Cold),
                new SimEvent(SimEventType.Hit, 1, 6, 7),
            };
            var back = SnapshotMessage.Decode(new SnapshotMessage(1, self, entities, projectiles, new List<HazardState>(), events).Encode());

            CollectionAssert.AreEqual(new[] { 700, 0, 123456 }, back.Entities.Select(e => e.Aim));
            CollectionAssert.AreEqual(new[] { 0, 0x0102, 0x0102 }, back.Entities.Select(e => e.Elite));
            CollectionAssert.AreEqual(new[] { 37, 0 }, back.Projectiles.Select(p => p.TicksLeft));
            Assert.AreEqual(SkillTag.Magic | SkillTag.Fire, back.Projectiles[0].Tags);
            Assert.IsTrue(back.Events[0].Critical);
            Assert.AreEqual(SkillTag.Lightning, World.ElementOfCause(back.Events[0].Cause));
            Assert.AreEqual(SkillTag.Lightning, back.Events[0].Tags);
            Assert.IsFalse(back.Events[1].Critical);
            Assert.AreEqual(SkillTag.None, World.ElementOfCause(back.Events[1].Cause));
            Assert.AreEqual(SkillTag.Fire | SkillTag.Cold, back.Events[1].Tags);
            Assert.AreEqual(SkillTag.None, back.Events[2].Tags);
        }
    }
}
