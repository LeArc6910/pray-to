using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The boss bundle R2b (2026-11-28, Docs/BossRedesign.md 5.1·5.9, Docs/Bosses.md "기둥·광맥·벽 돌기"): pillars an
    // attack raises (and lets crumble), harmless the first time; a charge that breaks one and stuns the boss, which
    // then takes more; the blast that spares only the shaded; the vein eater's wall run and its vein (eaten: rage;
    // broken first: nothing); and the four bosses' sheets.
    public class BossPillarTests
    {
        static readonly Vec2 Spawn = TileMap.Center(20, 5);
        static readonly Vec2 Home = TileMap.Center(20, 26);

        static void Walk(World world, Player player, Vec2 to)
        {
            for (int i = 0; i < 200 && Vec2.Distance(player.Position, to) > 0.001f; i++)
                Assert.IsTrue(world.MovePlayer(player.Id, player.Position + (to - player.Position).ClampLength(1.2f), player.Facing, 10));
        }

        static BossDefinition Dummy(string props, string rage, params string[] attacks) => Dummy(100000, props, rage, attacks);

        static BossDefinition Dummy(int health, string props, string rage, params string[] attacks) => Bosses.Parse(
            Bosses.BossesHeader.TrimEnd('\n') + ",tempo,finisher_rest,edge,props,rage\n" + $"dummy,허수아비,{health},50,0,2,1,66|33,1,3,100,,,,{props},{rage}\n",
            Bosses.AttacksHeader.TrimEnd('\n') + ",warn,pool,follow,boost\n" + string.Concat(attacks.Select(a => "dummy," + a + "\n"))).Find("dummy");

        static string Row(string id, string shape, string target, string telegraph, string damage, string radius = "", string length = "", string angle = "",
            string width = "", string count = "", string spread = "", string speed = "", string cooldown = "1", string weight = "0", string warn = "",
            string pool = "", string phases = "") =>
            string.Join(",", id, id, phases, shape, target, telegraph, damage, radius, length, angle, width, count, spread, speed, cooldown, weight, "", warn, pool, "", "");

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

        [Test]
        public void ATrailRaisesAPillarWhereItEndsThatCrumbles()
        {
            var definition = Dummy("", "", Row("trail", "trail", "nearest", "0.5", "100", radius: "1", length: "6", count: "4", speed: "30", pool: "pillar:2"), Idle);
            var (world, boss, _) = Awake(definition, Home + new Vec2(0, -8));
            Assert.IsTrue(world.ForceAttack(boss, "trail"));
            Run(world, 1.2f);
            var pillar = world.Props.Single();
            Assert.AreEqual(PropKind.Pillar, pillar.Kind);
            Assert.AreEqual(6, Vec2.Distance(pillar.Position, boss.Position), 0.6f, "where the last blast landed");
            Assert.IsFalse(pillar.Breakable, "players do not break pillars");
            Run(world, 2);
            Assert.AreEqual(0, world.Props.Count, "its time up, it crumbles");
        }

        [Test]
        public void TheFirstPillarCircleOfAFightHarmsNoOneAndDoesNotBlockTheWay()
        {
            var definition = Dummy("", "", Row("rise", "circle", "nearest", "0.6", "200", radius: "1.4", count: "1", pool: "pillar:9"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -5));
            float before = player.Health;
            Assert.IsTrue(world.ForceAttack(boss, "rise"));
            Run(world, 1);
            Assert.AreEqual(before, player.Health, "the first rise is shown unharmed");
            var pillar = world.Props.Single();
            Assert.Less(Vec2.Distance(pillar.Position, player.Position), 0.5f, "it rises under the feet");
            // Walking through it is not stopped (Docs/Bosses.md: pillars stop the boss, not the players).
            var through = pillar.Position + new Vec2(2, 0);
            Walk(world, player, through);
            Assert.AreEqual(through.X, player.Position.X, 0.01f);
            Walk(world, player, Home + new Vec2(-3, -5));
            Run(world, 1);
            before = player.Health;
            Assert.IsTrue(world.ForceAttack(boss, "rise"));
            Run(world, 1);
            Assert.Less(player.Health, before, "the next one hurts");
        }

        [Test]
        public void AChargeIntoAPillarBreaksItAndTheStunnedBossTakesMore()
        {
            var definition = Dummy("1:pillar", "", Row("rush", "charge", "farthest", "0.5", "300", length: "14", width: "2", speed: "20"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -9));
            var pillar = world.Props.Single();
            Walk(world, player, Home + (pillar.Position - Home).Normalized * 9);
            Assert.IsTrue(world.ForceAttack(boss, "rush"));
            for (int i = 0; i < World.Ticks(2) && boss.State != BossState.Stun; i++) world.Step();
            Assert.AreEqual(BossState.Stun, boss.State);
            Assert.AreEqual(0, world.Props.Count, "the pillar breaks");
            float health = boss.Health;
            world.DamageBoss(boss, 0, 100, false);
            Assert.AreEqual(health - 100 * World.StunTakenShare, boss.Health, 0.5f, "its core bare, it takes more");
            Run(world, World.StunSeconds + 0.1f);
            health = boss.Health;
            world.DamageBoss(boss, 0, 100, false);
            Assert.AreEqual(health - 100, boss.Health, 0.5f);
        }

        [Test]
        public void TheCoverBlastSparesOnlyWhoeverAPillarShades()
        {
            var definition = Dummy("1:pillar", "", Row("core", "cover", "self", "1.4", "400", warn: "strong"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -9));
            var other = world.AddPlayer("b", null);
            var pillar = world.Props.Single();
            var behind = boss.Position + (pillar.Position - boss.Position).Normalized * (Vec2.Distance(pillar.Position, boss.Position) + 1.5f);
            Walk(world, player, behind);
            var open = boss.Position - (pillar.Position - boss.Position).Normalized * 4;
            Walk(world, other, open);
            Assert.IsTrue(world.Shaded(boss.Id, boss.Position, player.Position));
            Assert.IsFalse(world.Shaded(boss.Id, boss.Position, other.Position));
            float hid = player.Health, stood = other.Health;
            Assert.IsTrue(world.ForceAttack(boss, "core"));
            // The shadow is marked green as the place to stand (AttackWarning.Safe): harmless, and covering whoever hides.
            var safe = world.Hazards.Single(h => h.Safe);
            Assert.AreEqual(HazardShape.Line, safe.Shape);
            Assert.AreEqual(0, safe.Damage);
            Assert.IsTrue(safe.Contains(player.Position, 0), "the one hiding stands on it");
            Assert.IsFalse(safe.Contains(other.Position, 0));
            Run(world, 1.6f);
            Assert.AreEqual(hid, player.Health, "behind the pillar: spared");
            Assert.Less(other.Health, stood, "in the open: struck");
        }

        [Test]
        public void WithNoPillarTheCoverBlastRaisesTwoFirst()
        {
            var definition = Dummy("", "", Row("core", "cover", "self", "1.4", "400", warn: "strong"), Idle);
            var (world, boss, _) = Awake(definition, Home + new Vec2(0, -5));
            Assert.IsTrue(world.ForceAttack(boss, "core"));
            Assert.AreEqual(2, world.Props.Count(p => p.Kind == PropKind.Pillar));
        }

        [Test]
        public void TheWallRunComesOutAtTheEdgeAndThrowsEachTime()
        {
            var definition = Dummy("", "", Row("wall", "orbit", "nearest", "0.6", "1", radius: "0.4", angle: "40", width: "3", count: "3", spread: "90", speed: "10"), Idle);
            var (world, boss, _) = Awake(definition, Home + new Vec2(0, -3));
            Assert.IsTrue(world.ForceAttack(boss, "wall"));
            Assert.IsTrue(boss.Invulnerable, "in the wall, out of reach");
            int outs = 0;
            var spots = new System.Collections.Generic.List<Vec2>();
            for (int i = 0; i < World.Ticks(6); i++)
            {
                var was = boss.State;
                world.Step();
                if (was == BossState.Telegraph && boss.State == BossState.Recover)
                {
                    outs++;
                    spots.Add(boss.Position);
                    Assert.IsFalse(boss.Invulnerable, "out of the wall it can be hit");
                    Assert.GreaterOrEqual(world.Projectiles.Count(p => !p.Friendly), 3, "it throws its rocks");
                    Assert.Greater(Vec2.Distance(boss.Position, Home), 7, "at the edge");
                }
            }
            Assert.AreEqual(3, outs, "width times");
            Assert.Greater(Vec2.Distance(spots[0], spots[1]), 5, "round the edge");
        }

        [Test]
        public void EatingAVeinEnragesAndBreakingItFirstStopsIt()
        {
            var definition = Dummy(100, "1:vein*2", "1:hurt=0 seconds=5 rest=1", Row("eat", "eat", "self", "2", "", weight: "0"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -9));
            Assert.AreEqual(2, world.Props.Count(p => p.Kind == PropKind.Vein));
            Assert.IsTrue(world.Props.All(p => p.Breakable));
            Assert.IsTrue(world.ForceAttack(boss, "eat"));
            Run(world, 2.1f);
            Assert.IsTrue(boss.Raging, "the vein eaten, it rages");
            Assert.AreEqual(1, world.Props.Count);

            // The other: broken before it gets there.
            Run(world, 7);
            Assert.IsFalse(boss.Raging);
            var vein = world.Props.Single();
            Walk(world, player, vein.Position + new Vec2(0, -(vein.Radius + 0.6f)));
            Assert.IsTrue(world.MovePlayer(player.Id, player.Position, new Vec2(0, 1), 10));
            Assert.IsTrue(world.ForceAttack(boss, "eat"));
            world.SetAttacking(player.Id, true);
            for (int i = 0; i < World.Ticks(1.9f) && world.Props.Count > 0; i++) world.Step();
            world.SetAttacking(player.Id, false);
            Assert.AreEqual(0, world.Props.Count, "struck down before it is eaten");
            Run(world, 0.5f);
            Assert.IsFalse(boss.Raging, "nothing to eat, no rage");
        }

        [Test]
        public void TheFourBossesUseThem()
        {
            var warden = Bosses.Shipped.Find("cave_warden");
            Assert.AreEqual(2, warden.PropsByPhase[2].Sum(p => p.Count));
            Assert.IsTrue(warden.PropsByPhase[2].All(p => p.Kind == PropKind.Pillar));
            Assert.IsTrue(warden.Attacks.Any(a => a.Pool == PoolKind.Pillar && a.InPhase(1)), "phase 1 shows a pillar");
            Assert.AreEqual(AttackShape.Cover, warden.Attacks.Single(a => a.Id == "core_burst").Shape);
            Assert.IsFalse(warden.Attacks.Any(a => a.Id == "aftershock" || a.Id == "cave_in"), "left out (Docs/BossRedesign.md 5.1)");

            var groom = Bosses.Shipped.Find("vein_eater");
            Assert.AreEqual(AttackShape.Orbit, groom.Attacks.Single(a => a.Id == "wall_run").Shape);
            Assert.IsFalse(groom.Attacks.Single(a => a.Id == "wall_run").InPhase(1));
            Assert.AreEqual(AttackShape.Eat, groom.Attacks.Single(a => a.Id == "eat_vein").Shape);
            Assert.AreEqual(1, groom.PropsByPhase[2].Single().Count);
            Assert.AreEqual(2, groom.PropsByPhase[3].Single().Count);
            Assert.AreEqual(0, groom.RageIn(2).Hurt, "only eating sets it off");
            Assert.AreEqual(10, groom.RageIn(2).Seconds, 1e-4f);
            Assert.IsFalse(groom.Attacks.Any(a => a.Id == "drill_spin" || a.Id == "collapse_ring"));

            var samurai = Bosses.Shipped.Find("shrine_samurai").Edge;
            Assert.AreEqual((3, EdgeKind.Petal, 2.5f), (samurai.Phase, samurai.Kind, samurai.Width));
            var mother = Bosses.Shipped.Find("silk_mother").Edge;
            Assert.AreEqual((3, EdgeKind.Web, 2f), (mother.Phase, mother.Kind, mother.Width));
        }
    }
}
