using System;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The boss bundle R3 (2026-11-28, Docs/BossRedesign.md 7.1 T4·T5·T7, Docs/Bosses.md "끌기·숨기·벽"): pulls and
    // pushes that can be walked against, sand pits that drag and bite at their middle (harmless the first time), the
    // mound that chases and bursts up where it stopped, threads that drag back and snap and are cut, walls of water with
    // a gap, walls that close in, and the half of the arena the tidal wave strikes; and the three bosses' sheets.
    public class BossForcesTests
    {
        static readonly Vec2 Spawn = TileMap.Center(20, 5);
        static readonly Vec2 Home = TileMap.Center(20, 26);

        static void Walk(World world, Player player, Vec2 to)
        {
            for (int i = 0; i < 200 && Vec2.Distance(player.Position, to) > 0.001f; i++)
                Assert.IsTrue(world.MovePlayer(player.Id, player.Position + (to - player.Position).ClampLength(1.2f), player.Facing, 10));
        }

        static BossData Sheet(string props, string edge, params string[] attacks) => Bosses.Parse(
            Bosses.BossesHeader.TrimEnd('\n') + ",tempo,finisher_rest,edge,props,rage\n" + $"dummy,허수아비,100000,50,0,2,1,66|33,1,3,100,,,{edge},{props},\n",
            Bosses.AttacksHeader.TrimEnd('\n') + ",warn,pool,follow,boost,pull\n" + string.Concat(attacks.Select(a => "dummy," + a + "\n")));

        static BossDefinition Dummy(string props, string edge, params string[] attacks)
        {
            var data = Sheet(props, edge, attacks);
            Assert.IsTrue(data.Success, string.Join("\n", data.Errors));
            return data.Find("dummy");
        }

        static void Refused(string row, string expected)
        {
            var data = Sheet("", "", row);
            Assert.IsTrue(data.Errors.Any(e => e.Contains(expected)), string.Join("\n", data.Errors));
        }

        static string Row(string id, string shape, string target, string telegraph, string damage, string radius = "", string length = "", string angle = "",
            string width = "", string count = "", string spread = "", string speed = "", string cooldown = "1", string weight = "0", string warn = "",
            string pool = "", string phases = "", string pull = "") =>
            string.Join(",", id, id, phases, shape, target, telegraph, damage, radius, length, angle, width, count, spread, speed, cooldown, weight, "", warn, pool, "", "", pull);

        static readonly string Idle = Row("idle", "circle", "nearest", "9", "1", radius: "0.5", count: "1", cooldown: "9", weight: "1");

        static (World World, Boss Boss, Player Player) Awake(BossDefinition definition, Vec2 at)
        {
            var world = new World(Maps.Room(40), Spawn, Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });
            var boss = world.PlaceBoss(definition, Home, 10);
            var player = world.AddPlayer("a", null);
            Walk(world, player, at);
            world.Step();
            return (world, boss, player);
        }

        static void Run(World world, float seconds)
        {
            for (int i = 0; i < World.Ticks(seconds); i++) world.Step();
        }

        // A tick of walking at the player's own speed, as a client would report it.
        static void Step(World world, Player player, Vec2 way)
        {
            Assert.IsTrue(world.MovePlayer(player.Id, player.Position + way.Normalized * (player.MoveSpeed * World.TickSeconds), way, 1));
            world.Step();
        }

        [Test]
        public void ASandPitDragsToItsMiddleCanBeWalkedOutOfAndBitesOnlyFromTheSecond()
        {
            var definition = Dummy("", "", Row("pit", "circle", "nearest", "0.5", "20", radius: "2.2", count: "1", pool: "pit:6"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -5));
            Assert.IsTrue(world.ForceAttack(boss, "pit"));
            Run(world, 0.6f);
            var pit = world.Hazards.Single(h => h.Shape == HazardShape.Pit);
            Assert.AreEqual(0, pit.Damage, "the first pit of a fight is shown unharmed");
            // Standing a little off its middle, the player is drawn in at World.PitPull.
            Walk(world, player, pit.Position + new Vec2(1.5f, 0));
            Run(world, 0.5f);
            Assert.AreEqual(1.5f - World.PitPull * 0.5f, Vec2.Distance(player.Position, pit.Position), 0.1f);
            // Walking straight out against it gets out.
            for (int i = 0; i < World.Ticks(1.2f); i++) Step(world, player, new Vec2(1, 0));
            Assert.Greater(Vec2.Distance(player.Position, pit.Position), pit.Size, "a walk beats the drag");

            // The second pit bites whoever stands in its middle.
            Walk(world, player, Home + new Vec2(0, -5));
            Assert.IsTrue(world.ForceAttack(boss, "pit"));
            Run(world, 0.6f);
            var second = world.Hazards.Last(h => h.Shape == HazardShape.Pit);
            Assert.Greater(second.Damage, 0);
            float before = player.Health;
            Walk(world, player, second.Position);
            Run(world, 1.1f);
            Assert.Less(player.Health, before, "its jaws at the middle");
        }

        [Test]
        public void TheAntlionsBigPitStandsInTheMiddleFromPhaseTwoAndSmallOnesRoundItInPhaseThree()
        {
            var antlion = Bosses.Shipped.Find("antlion");
            var world = new World(Maps.Room(40), Spawn, Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });
            var boss = world.PlaceBoss(antlion, Home, 10);
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(0, -7));
            world.Step();
            Run(world, 3);
            Assert.AreEqual(0, world.Props.Count(p => p.Kind == PropKind.Pit));
            world.Hurt(boss, (int)(boss.MaxHealth * 0.36f));
            Run(world, 0.5f);
            var big = world.Props.Single(p => p.Kind == PropKind.Pit);
            Assert.AreEqual(0, Vec2.Distance(big.Position, Home), 0.01f, "in the middle");
            Assert.AreEqual(World.BigPitRadius, big.Radius, 0.01f);
            world.Hurt(boss, (int)(boss.MaxHealth * 0.33f));
            Run(world, 0.5f);
            var pits = world.Props.Where(p => p.Kind == PropKind.Pit).ToList();
            Assert.AreEqual(4, pits.Count);
            Assert.AreEqual(3, pits.Count(p => Math.Abs(p.Radius - World.SmallPitRadius) < 0.01f), "three small ones round it");
        }

        [Test]
        public void ADragBitePullsTowardTheBossAndAPushThrowsOutOfARingsSafeMiddle()
        {
            var definition = Dummy("", "",
                Row("drag", "cone", "nearest", "1", "100", length: "3", angle: "90", pull: "2"),
                Row("mire", "ring", "self", "1", "100", radius: "2.4", length: "6", pull: "-1.5"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -6));
            float before = Vec2.Distance(player.Position, boss.Position);
            Assert.IsTrue(world.ForceAttack(boss, "drag"));
            Run(world, 0.5f);
            Assert.AreEqual(before - 1, Vec2.Distance(player.Position, boss.Position), 0.15f, "two tiles a second toward the boss");
            Run(world, 1);
            Assert.IsFalse(world.Hazards.Any(h => h.Shape == HazardShape.Pull), "the pull ends as the bite lands");

            Walk(world, player, boss.Position + new Vec2(1, 0));
            Assert.IsTrue(world.ForceAttack(boss, "mire"));
            Run(world, 0.5f);
            Assert.AreEqual(1 + 0.75f, Vec2.Distance(player.Position, boss.Position), 0.15f, "pushed out toward the ring");
            // Walking in against it keeps the player in the safe middle.
            for (int i = 0; i < World.Ticks(0.45f); i++) Step(world, player, boss.Position - player.Position);
            Assert.Less(Vec2.Distance(player.Position, boss.Position), 2.4f - World.PlayerRadius);
            float health = player.Health;
            Run(world, 0.2f);
            Assert.AreEqual(health, player.Health, "in the middle the ring misses");
        }

        [Test]
        public void AMoundChasesThenStopsAndTheBossBurstsUpWhereItStopped()
        {
            var definition = Dummy("", "", Row("mound", "burrow", "nearest", "2", "100", radius: "1.4", length: "3"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -6));
            Assert.IsTrue(world.ForceAttack(boss, "mound"));
            Assert.IsTrue(boss.Away, "under the floor");
            Assert.IsFalse(world.Hazards.Any(h => h.Shape == HazardShape.Circle && h.Damage > 0), "nothing marked while it chases");
            var start = boss.Position;
            Run(world, 1);
            Assert.AreEqual(3, Vec2.Distance(start, boss.Position), 0.2f, "it comes after the target at its speed");
            Run(world, 0.25f);
            var mark = world.Hazards.Single(h => h.Shape == HazardShape.Circle && h.Damage > 0);
            Assert.AreEqual(World.MoundStop, (mark.ResolveTick - mark.StartTick) * World.TickSeconds, 0.06f, "marked for the moment it stops");
            var stopped = boss.Position;
            // Off the mark before it bursts: unharmed.
            float before = player.Health;
            Walk(world, player, mark.Position + new Vec2(3, 0));
            Run(world, 0.8f);
            Assert.AreEqual(before, player.Health);
            Assert.AreEqual(0, Vec2.Distance(stopped, boss.Position), 0.01f, "up where it stopped");
            Assert.IsFalse(boss.Away);
        }

        [Test]
        public void AThreadDragsBackPastItsSlackAndThreeCutsPartIt()
        {
            var definition = Dummy("", "", Row("thread", "tether", "nearest", "0.5", "300", count: "1", spread: "8"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -3));
            Assert.IsTrue(world.ForceAttack(boss, "thread"));
            Run(world, 0.6f);
            var thread = world.Hazards.Single(h => h.Shape == HazardShape.Tether);
            // Pulled out past the slack (as a push would), it is dragged back toward the boss.
            player.Position = boss.Position + new Vec2(0, -World.ThreadSlack - 2);
            Run(world, 0.5f);
            Assert.AreEqual(World.ThreadSlack + 2 - World.ThreadPull * 0.5f, Vec2.Distance(player.Position, boss.Position), 0.15f);
            Assert.AreEqual(Vec2.Distance(player.Position, boss.Position), thread.Size, 0.05f, "it runs from the boss to the player");
            // Swinging across it, three cuts part it.
            Walk(world, player, boss.Position + new Vec2(0, -4));
            player.Facing = (boss.Position - player.Position).Normalized;
            world.SetAttacking(player.Id, true);
            Run(world, 6);
            Assert.IsFalse(world.Hazards.Any(h => h.Shape == HazardShape.Tether), "cut through");
        }

        [Test]
        public void TwoPlayersThreadedTogetherSnapItWhenTooCloseAndAreBothHurt()
        {
            var definition = Dummy("", "", Row("thread", "tether", "nearest", "0.5", "300", count: "1", spread: "8"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -3));
            var other = world.AddPlayer("b", null);
            Walk(world, other, Home + new Vec2(4, -3));
            Assert.IsTrue(world.ForceAttack(boss, "thread"));
            Run(world, 0.6f);
            Assert.AreEqual(1, world.Hazards.Count(h => h.Shape == HazardShape.Tether), "one thread between the two");
            // Too close in its first moment: not yet.
            float a = player.Health, b = other.Health;
            Walk(world, other, player.Position + new Vec2(1, 0));
            Run(world, 0.2f);
            Assert.AreEqual(1, world.Hazards.Count(h => h.Shape == HazardShape.Tether));
            Run(world, 1);
            Assert.AreEqual(0, world.Hazards.Count(h => h.Shape == HazardShape.Tether), "snapped");
            Assert.Less(player.Health, a);
            Assert.Less(other.Health, b);
        }

        [Test]
        public void AWaveStrikesAndPushesWhoeverIsNotInItsGap()
        {
            var definition = Dummy("", "", Row("wave", "wave", "nearest", "0.5", "200", width: "1.5", count: "1", speed: "8"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -5));
            var other = world.AddPlayer("b", null);
            Walk(world, other, Home + new Vec2(0, -6));
            Assert.IsTrue(world.ForceAttack(boss, "wave"));
            var wave = world.Hazards.Single(h => h.Shape == HazardShape.Wave);
            var gap = world.Hazards.Single(h => h.Safe);
            Assert.AreEqual(wave.Width, gap.Width, 1e-4f, "its gap is marked green");
            // One in the gap, one well out of it.
            var side = new Vec2(-wave.Direction.Y, wave.Direction.X);
            float across = Vec2.Dot(player.Position - wave.Position, side);
            Walk(world, player, player.Position - side * across);
            Walk(world, other, player.Position + side * 4);
            float a = player.Health, b = other.Health;
            var otherAt = other.Position;
            Run(world, 0.5f + 22 / 8f + 0.2f);
            Assert.AreEqual(a, player.Health, "the gap is safe");
            Assert.Less(other.Health, b);
            Assert.Greater(Vec2.Dot(other.Position - otherAt, wave.Direction), 1, "pushed on");
            Assert.IsFalse(world.Hazards.Any(h => h.Shape == HazardShape.Wave));
        }

        [Test]
        public void ClosingWallsSpareOnlyTheMiddle()
        {
            var definition = Dummy("", "", Row("close", "close", "self", "0.5", "200", width: "1.5", speed: "8"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -5));
            var other = world.AddPlayer("b", null);
            Walk(world, other, Home + new Vec2(0, -6));
            Assert.IsTrue(world.ForceAttack(boss, "close"));
            Assert.AreEqual(2, world.Hazards.Count(h => h.Shape == HazardShape.Wave));
            var strip = world.Hazards.Single(h => h.Safe);
            var side = new Vec2(-strip.Direction.Y, strip.Direction.X);
            Walk(world, player, Home + strip.Direction * 2);
            Walk(world, other, Home + strip.Direction * 2 + side * 3);
            float a = player.Health, b = other.Health;
            Run(world, 0.5f + 11 / 8f + 0.3f);
            Assert.AreEqual(a, player.Health, "the middle is left");
            Assert.Less(other.Health, b);
        }

        [Test]
        public void TheTidalWaveStrikesTheHalfTheBossFaces()
        {
            var definition = Dummy("", "", Row("half", "half", "nearest", "1.2", "200", warn: "strong"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -4));
            var other = world.AddPlayer("b", null);
            Walk(world, other, Home + new Vec2(0, 4));
            float a = player.Health, b = other.Health;
            Assert.IsTrue(world.ForceAttack(boss, "half"));
            Run(world, 1.4f);
            Assert.Less(player.Health, a, "the side its head is raised to");
            Assert.AreEqual(b, other.Health, "the other side is safe");
        }

        [Test]
        public void ThePullColumnIsCheckedAndTheThreeBossesUseTheirNewAttacks()
        {
            Refused(Row("x", "cone", "nearest", "1", "100", length: "3", angle: "90", pull: "-1"), "밀어냄");
            Refused(Row("x", "cone", "nearest", "1", "100", length: "3", angle: "90", pull: "4.5"), "거슬러 걸을 수");
            Refused(Row("x", "burrow", "nearest", "1", "100", radius: "1", length: "3"), "쫓는 둔덕");

            var antlion = Bosses.Shipped.Find("antlion");
            Assert.IsTrue(antlion.Attacks.Single(a => a.Id == "mound").Chases);
            Assert.AreEqual(2, antlion.Attacks.Single(a => a.Id == "drag_bite").Pull, 1e-4f);
            Assert.AreEqual(3, antlion.Attacks.Single(a => a.Id == "drag_bite").ForPhase(3).Pull, 1e-4f, "1.5 times in phase 3");
            Assert.Less(antlion.Attacks.Single(a => a.Id == "quicksand").Pull, 0);
            Assert.AreEqual(AttackTarget.Middle, antlion.Attacks.Single(a => a.Id == "maw").Target);
            Assert.AreEqual(PoolKind.Pit, antlion.Attacks.Single(a => a.Id == "pit").Pool);
            foreach (var gone in new[] { "sand_cross", "sand_ripple", "sandstorm" }) Assert.IsFalse(antlion.Attacks.Any(a => a.Id == gone), gone);

            var serpent = Bosses.Shipped.Find("tide_serpent");
            Assert.IsTrue(serpent.Attacks.Single(a => a.Id == "dive").Chases);
            Assert.AreEqual(2, serpent.Attacks.Single(a => a.Id == "wave").ForPhase(3).Count);
            Assert.AreEqual(AttackShape.Close, serpent.Attacks.Single(a => a.Id == "squeeze").Shape);
            Assert.AreEqual(AttackShape.Half, serpent.Attacks.Single(a => a.Id == "tsunami").Shape);
            Assert.AreEqual(EdgeKind.Water, serpent.Edge.Kind);
            foreach (var gone in new[] { "tidal_cross", "undertow", "bubble_spiral", "deep_share" }) Assert.IsFalse(serpent.Attacks.Any(a => a.Id == gone), gone);

            var mother = Bosses.Shipped.Find("silk_mother").Attacks.Single(a => a.Id == "thread");
            Assert.AreEqual(AttackShape.Tether, mother.Shape);
            Assert.IsFalse(mother.InPhase(1));
            Assert.AreEqual(2, mother.ForPhase(3).Count);
        }
    }
}
