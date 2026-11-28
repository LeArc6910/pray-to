using System;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The boss bundle R4a (2026-11-28, Docs/BossRedesign.md 5.3·5.5 and 7.1 T8·T9, Docs/Bosses.md "순서·막간"): crystals
    // that light and burst in order (raised when none stand), a beam that turns at a crystal, a pounce that breaks on
    // one, a marked circle that follows its player until it locks, bands that land one after another, a banner that
    // staggers its boss when broken and draws its hellfire, and the interludes (eggs that hatch unbroken; a legion that
    // ends when its banner breaks); and the two bosses' sheets.
    public class BossOrderTests
    {
        static readonly Vec2 Spawn = TileMap.Center(20, 5);
        static readonly Vec2 Home = TileMap.Center(20, 26);

        static void Walk(World world, Player player, Vec2 to)
        {
            for (int i = 0; i < 200 && Vec2.Distance(player.Position, to) > 0.001f; i++)
                Assert.IsTrue(world.MovePlayer(player.Id, player.Position + (to - player.Position).ClampLength(1.2f), player.Facing, 10));
        }

        static BossDefinition Dummy(int health, string props, string interlude, params string[] attacks) => Bosses.Parse(
            Bosses.BossesHeader.TrimEnd('\n') + ",tempo,finisher_rest,edge,props,rage,interlude\n" + $"dummy,허수아비,{health},50,0,2,1,66|33,1,3,100,,,,{props},,{interlude}\n",
            Bosses.AttacksHeader.TrimEnd('\n') + ",warn,pool,follow,boost\n" + string.Concat(attacks.Select(a => "dummy," + a + "\n"))).Find("dummy");

        static string Row(string id, string shape, string target, string telegraph, string damage, string radius = "", string length = "", string angle = "",
            string width = "", string count = "", string spread = "", string speed = "", string cooldown = "1", string weight = "0", string warn = "",
            string pool = "", string phases = "", string summon = "") =>
            string.Join(",", id, id, phases, shape, target, telegraph, damage, radius, length, angle, width, count, spread, speed, cooldown, weight, summon, warn, pool, "", "");

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

        // Stands beside a thing and swings at it until it breaks (or time is up).
        static void Break(World world, Player player, BossProp prop, float seconds = 3)
        {
            Walk(world, player, prop.Position + new Vec2(0, -(prop.Radius + 0.6f)));
            Assert.IsTrue(world.MovePlayer(player.Id, player.Position, new Vec2(0, 1), 10));
            world.SetAttacking(player.Id, true);
            for (int i = 0; i < World.Ticks(seconds) && world.Props.Contains(prop); i++) world.Step();
            world.SetAttacking(player.Id, false);
        }

        [Test]
        public void CrystalsLightAndBurstInTheSameOrder()
        {
            var definition = Dummy(100000, "1:crystal*3", "", Row("chime", "order", "nearest", "0.5", "100", radius: "2", count: "3", spread: "0.4"), Idle);
            var (world, boss, _) = Awake(definition, Home + new Vec2(0, -9));
            var crystals = world.Props.Where(p => p.Kind == PropKind.Crystal).ToList();
            Assert.AreEqual(3, crystals.Count);
            Assert.IsTrue(crystals.All(c => c.Breakable), "the queen's crystals can be broken");
            long start = world.Tick;
            Assert.IsTrue(world.ForceAttack(boss, "chime"));
            var bursts = world.Hazards.Where(h => h.Order > 0).OrderBy(h => h.Order).ToList();
            Assert.AreEqual(new[] { 1, 2, 3 }, bursts.Select(h => h.Order).ToArray());
            Assert.IsTrue(bursts.All(b => crystals.Any(c => Vec2.Distance(c.Position, b.Position) < 0.01f)), "each on a crystal");
            // Every crystal lights before the first bursts (the telegraph stretched to 3 x 0.4 + 0.4).
            Assert.GreaterOrEqual(bursts[0].ResolveTick - start, World.Ticks(3 * 0.4f + World.OrderPause) - 1);
            Assert.AreEqual(World.Ticks(World.OrderBurstGap), bursts[1].ResolveTick - bursts[0].ResolveTick, 1);
            Assert.AreEqual(World.Ticks(World.OrderBurstGap), bursts[2].ResolveTick - bursts[1].ResolveTick, 1);
            // The boss rests until the last has landed.
            Run(world, 1.7f);
            Assert.AreEqual(BossState.Recover, boss.State);
            Assert.GreaterOrEqual(boss.StateTicks, (int)(bursts[2].ResolveTick - world.Tick));
        }

        [Test]
        public void WithNoCrystalStandingTheyRiseRoundTheTarget()
        {
            var definition = Dummy(100000, "", "", Row("chime", "order", "nearest", "0.5", "100", radius: "2", count: "2", spread: "0.4"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -6));
            Assert.IsTrue(world.ForceAttack(boss, "chime"));
            var crystals = world.Props.Where(p => p.Kind == PropKind.Crystal).ToList();
            Assert.AreEqual(2, crystals.Count);
            Assert.IsTrue(crystals.All(c => Vec2.Distance(c.Position, player.Position) < 3.5f));
            Assert.AreEqual(2, world.Hazards.Count(h => h.Order > 0));
        }

        [Test]
        public void ABeamTurnsWhereItMeetsACrystal()
        {
            var definition = Dummy(100000, "", "", Row("rise", "circle", "nearest", "0.5", "10", radius: "1", count: "1", pool: "crystal:20"),
                Row("beam", "beam", "nearest", "0.8", "100", length: "14", width: "1.2", count: "1"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -4));
            Assert.IsTrue(world.ForceAttack(boss, "rise"));
            Run(world, 0.8f);
            var crystal = world.Props.Single(p => p.Kind == PropKind.Crystal);
            // Stand beyond the crystal, straight on from the boss.
            var way = (crystal.Position - boss.Position).Normalized;
            Walk(world, player, crystal.Position + way * 3);
            int before = world.Hazards.Count;
            Assert.IsTrue(world.ForceAttack(boss, "beam"));
            var beams = world.Hazards.Skip(before).Where(h => h.Shape == HazardShape.Line).ToList();
            Assert.AreEqual(2, beams.Count, "it turns once at the crystal");
            Assert.Less(Vec2.Distance(beams[1].Position, crystal.Position), crystal.Radius + 0.01f, "the turn starts at the crystal");
            Assert.AreEqual(0, Vec2.Dot(beams[0].Direction, beams[1].Direction), 0.05f, "a right angle");
            Assert.AreEqual(14, beams[0].Size + beams[1].Size, 0.01f, "the same length in all");
        }

        [Test]
        public void APounceIntoACrystalBreaksItAndStops()
        {
            var definition = Dummy(100000, "", "", Row("rise", "circle", "nearest", "0.5", "10", radius: "1", count: "1", pool: "crystal:20"),
                Row("pounce", "charge", "farthest", "0.5", "100", length: "12", width: "2.4", speed: "18", count: "2"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -4));
            Assert.IsTrue(world.ForceAttack(boss, "rise"));
            Run(world, 0.8f);
            var crystal = world.Props.Single(p => p.Kind == PropKind.Crystal);
            var way = (crystal.Position - boss.Position).Normalized;
            Walk(world, player, crystal.Position + way * 4);
            Assert.IsTrue(world.ForceAttack(boss, "pounce"));
            Run(world, 1.2f);
            Assert.IsFalse(world.Props.Contains(crystal), "the crystal shatters");
            Assert.AreEqual(BossState.Recover, boss.State, "the pounce stops there, and runs no more");
            Assert.Less(Vec2.Distance(boss.Position, crystal.Position), crystal.Radius + boss.Definition.Radius + 0.5f);
        }

        [Test]
        public void AMarkFollowsItsPlayerUntilItLocks()
        {
            var definition = Dummy(100000, "", "", Row("mark", "mark", "nearest", "1.4", "100", radius: "2.5", count: "1"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -6));
            float health = player.Health;
            Assert.IsTrue(world.ForceAttack(boss, "mark"));
            var mark = world.Hazards.Single(h => h.Damage == 100);
            Walk(world, player, Home + new Vec2(3, -6));
            world.Step();
            Assert.Less(Vec2.Distance(mark.Position, player.Position), 0.01f, "it follows");
            Run(world, 1.4f - World.MarkLock);
            var locked = mark.Position;
            // Locked: walk out of it.
            Walk(world, player, locked + new Vec2(-3.5f, 0));
            Run(world, 0.4f);
            Assert.AreEqual(locked.X, mark.Position.X, 0.01f, "it no longer follows");
            Run(world, 0.6f);
            Assert.IsFalse(world.Hazards.Contains(mark));
            Assert.AreEqual(health, player.Health, "walked out in time");
        }

        [Test]
        public void TwoMarkedPlayersEachGetTheirOwn()
        {
            var definition = Dummy(100000, "", "", Row("mark", "mark", "nearest", "1.4", "100", radius: "2.5", count: "2"), Idle);
            var (world, boss, a) = Awake(definition, Home + new Vec2(0, -6));
            var b = world.AddPlayer("b", null);
            Walk(world, b, Home + new Vec2(4, -6));
            world.Step();
            Assert.IsTrue(world.ForceAttack(boss, "mark"));
            var marks = world.Hazards.Where(h => h.Damage == 100).ToList();
            Assert.AreEqual(2, marks.Count);
            Assert.IsTrue(marks.Any(m => Vec2.Distance(m.Position, a.Position) < 0.01f) && marks.Any(m => Vec2.Distance(m.Position, b.Position) < 0.01f));
        }

        [Test]
        public void BandsLandOneAfterAnotherFromTheBossSide()
        {
            var definition = Dummy(100000, "", "", Row("bands", "bands", "self", "1.2", "100", count: "5", spread: "0.3", warn: "strong"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -6));
            boss.Position = Home + new Vec2(0, 4);
            Assert.IsTrue(world.ForceAttack(boss, "bands"));
            var bands = world.Hazards.Where(h => h.Order > 0).OrderBy(h => h.Order).ToList();
            Assert.AreEqual(5, bands.Count);
            for (int i = 1; i < 5; i++)
            {
                Assert.AreEqual(World.Ticks(0.3f), bands[i].ResolveTick - bands[i - 1].ResolveTick, 1);
                Assert.Less(bands[i].Position.Y + 0, bands[i - 1].Position.Y + 0.01f, "each a band further from the boss");
            }
            // The whole arena is covered: the player stands in exactly one band.
            Assert.AreEqual(1, bands.Count(h => h.Contains(player.Position, 0)));
            Assert.Greater(bands[0].Position.Y, boss.Home.Y + 5, "the first on the boss's side");
        }

        [Test]
        public void ABrokenBannerStaggersItsBossAndDrawsItsHellfire()
        {
            var definition = Dummy(200, "", "", Row("plant", "circle", "nearest", "0.5", "10", radius: "1", count: "1", pool: "banner:20"),
                Row("hellfire", "circle", "banner", "1", "100", radius: "1.5", count: "3", spread: "2"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -5));
            Assert.IsTrue(world.ForceAttack(boss, "plant"));
            Run(world, 0.8f);
            var banner = world.Props.Single(p => p.Kind == PropKind.Banner);
            Assert.IsTrue(banner.Breakable);
            Walk(world, player, Home + new Vec2(-6, 0));
            int before = world.Hazards.Count;
            Assert.IsTrue(world.ForceAttack(boss, "hellfire"));
            Assert.IsTrue(world.Hazards.Skip(before).All(h => Vec2.Distance(h.Position, banner.Position) <= 2.01f), "hellfire falls round the banner");
            Run(world, 2);
            Break(world, player, banner, 8);
            Assert.IsFalse(world.Props.Contains(banner));
            Assert.AreEqual(BossState.Stun, boss.State, "the boss staggers");
            Assert.AreEqual(World.Ticks(World.BannerStunSeconds), boss.StateTicks, 2);
        }

        [Test]
        public void TheEggInterludeEndsWhenEveryEggIsBroken()
        {
            var definition = Dummy(200, "", "3:eggs:3:12:crystal_hatchling", Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -5));
            world.DamageBoss(boss, player.Id, 140, false);
            Assert.AreEqual(3, boss.Phase);
            Run(world, boss.Definition.Roar + 0.1f);
            Assert.AreEqual(BossState.Interlude, boss.State, "the interlude comes as the roar ends");
            Assert.IsTrue(boss.Invulnerable);
            var eggs = world.Props.Where(p => p.Kind == PropKind.Egg).ToList();
            Assert.AreEqual(3, eggs.Count);
            foreach (var egg in eggs) Break(world, player, egg);
            world.Step();
            Assert.AreNotEqual(BossState.Interlude, boss.State, "every egg broken: it ends at once");
            Assert.AreEqual(0, world.Enemies.Count, "nothing hatches");
        }

        [Test]
        public void EggsLeftWholeHatchWhenTheInterludeRunsOut()
        {
            var definition = Dummy(200, "", "3:eggs:4:6:crystal_hatchling", Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -5));
            world.DamageBoss(boss, player.Id, 140, false);
            Run(world, boss.Definition.Roar + 0.1f);
            Assert.AreEqual(BossState.Interlude, boss.State);
            Run(world, 6);
            Assert.AreNotEqual(BossState.Interlude, boss.State);
            Assert.AreEqual(0, world.Props.Count(p => p.Kind == PropKind.Egg));
            Assert.AreEqual(4, world.Enemies.Count(e => e.Definition.Id == "crystal_hatchling"), "each egg left whole hatches");
        }

        [Test]
        public void TheLegionChargesUntilItsBannerBreaks()
        {
            var definition = Dummy(200, "", "3:legion:3:15:ranks",
                Row("ranks", "wave", "nearest", "1", "50", width: "1.8", count: "1", speed: "6", phases: "2|3"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -5));
            world.DamageBoss(boss, player.Id, 140, false);
            Run(world, boss.Definition.Roar + 0.1f);
            Assert.AreEqual(BossState.Interlude, boss.State);
            var banner = world.Props.Single(p => p.Kind == PropKind.Banner);
            Assert.Less(Vec2.Distance(banner.Position, boss.Home), Vec2.Distance(boss.Position, boss.Home), "the banner stands before him");
            Run(world, 2);
            Assert.IsTrue(world.Hazards.Any(h => h.Shape == HazardShape.Wave), "the legion charges");
            Break(world, player, banner, 6);
            world.Step();
            Assert.AreNotEqual(BossState.Interlude, boss.State, "the banner broken, the interlude is over");
            Assert.AreNotEqual(BossState.Stun, boss.State, "the interlude's banner does not stagger him");
            Assert.IsFalse(world.Hazards.Any(h => h.Shape == HazardShape.Wave && world.Tick < h.StartTick), "the charges not yet come do not come");
        }

        [Test]
        public void TheQueenAndMorgHaveTheirNewWays()
        {
            var queen = Bosses.Shipped.Find("crystal_queen");
            Assert.AreEqual(InterludeKind.Eggs, queen.Interlude.Kind);
            Assert.AreEqual(5, queen.Interlude.Count);
            Assert.AreEqual("crystal_hatchling", queen.Interlude.Hatch.Id);
            Assert.AreEqual(AttackShape.Order, queen.Attacks.Single(a => a.Id == "resonance").Shape);
            Assert.AreEqual(4, queen.Attacks.Single(a => a.Id == "resonance").ForPhase(3).Count);
            Assert.AreEqual(PoolKind.Crystal, queen.Attacks.Single(a => a.Id == "web_quake").Pool);
            Assert.AreEqual(AttackShape.Beam, queen.Attacks.Single(a => a.Id == "venom_share").Shape);
            var finisher = queen.Attacks.Single(a => a.Id == "shard_hail");
            Assert.AreEqual(AttackShape.Order, finisher.Shape);
            Assert.AreEqual(AttackWarning.Strong, finisher.Warning);

            var morg = Bosses.Shipped.Find("mad_general");
            Assert.AreEqual(InterludeKind.Legion, morg.Interlude.Kind);
            Assert.AreEqual(AttackShape.Wave, morg.Interlude.Charge.Shape);
            Assert.AreEqual(AttackTarget.Banner, morg.Attacks.Single(a => a.Id == "hellfire").Target);
            Assert.AreEqual(PoolKind.Banner, morg.Attacks.Single(a => a.Id == "flame_ring").Pool);
            Assert.AreEqual(AttackShape.Mark, morg.Attacks.Single(a => a.Id == "marked_volley").Shape);
            Assert.AreEqual(AttackShape.Bands, morg.Attacks.Single(a => a.Id == "meteors").Shape);
            Assert.AreEqual(3, morg.Attacks.Single(a => a.Id == "dark_cross").ForPhase(3).Count);
        }
    }
}
