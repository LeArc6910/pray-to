using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The boss bundle R2a (2026-11-28, Docs/BossRedesign.md 7.1 T3·T9, Docs/Bosses.md 지형·물체·분노): lava that bites
    // and slows (harmless the first time), cools into rock and spares whoever stands on rock; cracks and water; an edge
    // that closes in with islands; vents that burst in turns; pillars that stop shots and stun a charge; rage that
    // adds a blow and ends in an opening; and the snapshot that carries it.
    public class BossTerrainTests
    {
        static readonly Vec2 Spawn = TileMap.Center(20, 5);
        static readonly Vec2 Home = TileMap.Center(20, 26);

        static void Walk(World world, Player player, Vec2 to)
        {
            // Short steps: wading is slower (World.PlayerWadeSpeed).
            for (int i = 0; i < 200 && Vec2.Distance(player.Position, to) > 0.001f; i++)
                Assert.IsTrue(world.MovePlayer(player.Id, player.Position + (to - player.Position).ClampLength(1.2f), player.Facing, 10));
        }

        // One boss with the extra columns given and the attack rows (from shape on).
        static BossDefinition Dummy(string edge, string props, string rage, params string[] attacks) => Bosses.Parse(
            Bosses.BossesHeader.TrimEnd('\n') + ",tempo,finisher_rest,edge,props,rage\n" + $"dummy,허수아비,100000,50,0,2,1,66|33,1,3,100,,,{edge},{props},{rage}\n",
            Bosses.AttacksHeader.TrimEnd('\n') + ",warn,pool,follow,boost\n" + string.Concat(attacks.Select(a => "dummy," + a + "\n"))).Find("dummy");

        // An attack row from its id on: id, name, phases, shape, target, telegraph, damage, radius, length, angle, width,
        // count, spread, speed, cooldown, weight, summon, warn, pool, follow, boost.
        static string Row(string id, string shape, string target, string telegraph, string damage, string radius = "", string length = "", string angle = "",
            string width = "", string count = "", string spread = "", string speed = "", string cooldown = "1", string weight = "0", string warn = "",
            string pool = "", string phases = "") =>
            string.Join(",", id, id, phases, shape, target, telegraph, damage, radius, length, angle, width, count, spread, speed, cooldown, weight, "", warn, pool, "", "");

        // What the boss does by itself between the attacks a test forces (weight 0): a long wind-up that harms little.
        static readonly string Idle = Row("idle", "circle", "nearest", "9", "1", radius: "0.5", count: "1", cooldown: "9", weight: "1");

        static (World World, Boss Boss, Player Player) Awake(BossDefinition definition, Vec2 at, float arena = 10)
        {
            var world = new World(Maps.Room(40), Spawn, Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });
            var boss = world.PlaceBoss(definition, Home, arena);
            var player = world.AddPlayer("a", null);
            Walk(world, player, at);
            world.Step();
            return (world, boss, player);
        }

        static void Run(World world, float seconds)
        {
            for (int i = 0; i < World.Ticks(seconds); i++) world.Step();
        }

        static readonly string Trail = Row("trail", "trail", "farthest", "0.5", "300", radius: "1", length: "6", count: "4", speed: "30", pool: "lava:6");

        [Test]
        public void ATrailLeavesOneStripOfLavaHarmlessTheFirstTime()
        {
            var (world, boss, player) = Awake(Dummy("", "", "", Trail, Idle), Home + new Vec2(0, -9));
            Assert.IsTrue(world.ForceAttack(boss, "trail"));
            Run(world, 1.2f);
            var strips = world.Hazards.Where(h => h.Shape == HazardShape.Lava).ToList();
            Assert.AreEqual(1, strips.Count, "one strip, laid as the last blast lands");
            Assert.Greater(strips[0].Width, 0);
            Assert.AreEqual(0, strips[0].Damage, "the first lava of a fight bites no one");
            Assert.IsTrue(strips[0].Contains(boss.Position + new Vec2(0, -3), 0), "it runs along the trail");
            Run(world, 3);
            Assert.IsTrue(world.ForceAttack(boss, "trail"));
            Run(world, 1.2f);
            var second = world.Hazards.Where(h => h.Shape == HazardShape.Lava).OrderBy(h => h.StartTick).Last();
            Assert.Greater(second.Damage, 0, "the next one bites");

            // Standing in it: slower, and bitten every half second.
            var inLava = second.Position + second.Direction * (second.Size / 2);
            Walk(world, player, inLava);
            float before = player.Health;
            float walk = player.MoveSpeed;
            world.Step();
            Assert.IsTrue(player.Afflictions.Wading);
            Assert.Less(player.MoveSpeed, walk + 1e-4f);
            Run(world, 1.1f);
            Assert.Less(player.Health, before, "lava bites");
        }

        [Test]
        public void LavaCoolsIntoRockAndAFinisherSparesWhoeverStandsOnIt()
        {
            var definition = Dummy("", "", "",
                Row("pour", "circle", "nearest", "0.5", "1", radius: "2", count: "1", pool: "lava:1"),
                Row("rain", "refuge", "self", "1.4", "500", radius: "1.5", warn: "strong"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -5));
            var other = world.AddPlayer("b", null);
            Walk(world, other, Home + new Vec2(7, 0));
            var spot = player.Position;
            Assert.IsTrue(world.ForceAttack(boss, "pour"));
            Run(world, 0.6f);
            Assert.IsTrue(world.Hazards.Any(h => h.Shape == HazardShape.Lava));
            Run(world, 1.1f);
            var rock = world.Hazards.SingleOrDefault(h => h.Shape == HazardShape.Rock);
            Assert.IsNotNull(rock, "lava whose time is up cools into rock");
            Assert.AreEqual(spot.X, rock.Position.X, 0.01f);
            Assert.IsTrue(world.OnRock(boss.Id, spot));

            float onRock = player.Health, offRock = other.Health;
            Assert.IsTrue(world.ForceAttack(boss, "rain"));
            Assert.IsTrue(world.Hazards.Any(h => h.Spares));
            Run(world, 1.5f);
            Assert.AreEqual(onRock, player.Health, "on rock: spared");
            Assert.Less(other.Health, offRock, "off it: struck");
        }

        [Test]
        public void WithNoRockTheFinisherLaysSomeRoundTheBoss()
        {
            var (world, boss, player) = Awake(Dummy("", "", "", Row("rain", "refuge", "self", "1.4", "500", radius: "1.5", warn: "strong"), Idle), Home + new Vec2(0, -2.2f));
            float before = player.Health;
            Assert.IsTrue(world.ForceAttack(boss, "rain"));
            var rock = world.Hazards.Single(h => h.Shape == HazardShape.Rock);
            Assert.AreEqual(boss.Definition.Radius + 1.5f, rock.Size, 1e-3f);
            Run(world, 1.5f);
            Assert.AreEqual(before, player.Health, "beside the boss: spared");
        }

        [Test]
        public void ACrossLeavesCracksAlongItsArmsAndALatePoolWaitsForItsPhase()
        {
            var definition = Dummy("", "", "",
                Row("cross", "cross", "self", "0.6", "300", length: "6", angle: "0", width: "1.2", pool: "crack:8"),
                Row("slam", "circle", "nearest", "0.5", "5", radius: "2", count: "1", pool: "lava:5:2"), Idle);
            var (world, boss, _) = Awake(definition, Home + new Vec2(3, -5));
            Assert.IsTrue(world.ForceAttack(boss, "cross"));
            Run(world, 0.7f);
            var cracks = world.Hazards.Where(h => h.Shape == HazardShape.Crack).ToList();
            Assert.AreEqual(2, cracks.Count, "one along each beam");
            Assert.IsTrue(cracks.All(c => c.Width > 0 && c.Size > 11));
            Assert.IsTrue(world.ForceAttack(boss, "slam"));
            Run(world, 0.6f);
            Assert.IsFalse(world.Hazards.Any(h => h.Shape == HazardShape.Lava), "phase 1 leaves no lava (lava:5:2)");
            boss.Phase = 2;
            Assert.IsTrue(world.ForceAttack(boss, "slam"));
            Run(world, 0.6f);
            Assert.IsTrue(world.Hazards.Any(h => h.Shape == HazardShape.Lava), "phase 2 does");
        }

        [Test]
        public void ShallowWaterOnlySlows()
        {
            var (world, boss, player) = Awake(Dummy("", "", "", Row("wet", "circle", "nearest", "0.5", "1", radius: "2", count: "1", pool: "water:5"), Idle), Home + new Vec2(0, -5));
            Assert.IsTrue(world.ForceAttack(boss, "wet"));
            Run(world, 0.6f);
            Walk(world, player, Home + new Vec2(4, 0));
            Walk(world, player, Home + new Vec2(0, -5));
            float before = player.Health;
            Run(world, 0.3f);
            Assert.IsTrue(player.Afflictions.Wading);
            Run(world, 1.5f);
            Assert.AreEqual(before, player.Health);
        }

        [Test]
        public void AnEdgeClosesInAtItsPhaseWithIslandsAndBitesAfterItsGrace()
        {
            var definition = Dummy("3:lava:3:4", "", "", Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -8.5f));
            Assert.AreEqual(10, World.FloorRadius(boss), 1e-4f, "no edge before its phase");
            world.DamageBoss(boss, 0, (int)(boss.MaxHealth * 0.7f), false);
            Assert.AreEqual(3, boss.Phase);
            Assert.AreEqual(7, World.FloorRadius(boss), 1e-4f);
            Assert.AreEqual(4, World.Islands(boss).Count());
            Assert.IsTrue(World.InEdge(boss, player.Position));
            float before = player.Health;
            Run(world, World.EdgeGraceSeconds - 0.2f);
            Assert.AreEqual(before, player.Health, "unhurt in its grace");
            Run(world, 1.2f);
            Assert.Less(player.Health, before, "then it bites");
            Assert.IsTrue(world.BadGround(player.Position));
            var island = World.Islands(boss).First();
            Walk(world, player, island);
            Assert.IsFalse(World.InEdge(boss, island), "an island is safe");
            Assert.IsTrue(world.OnRock(boss.Id, island));
        }

        [Test]
        public void VentsStandFromTheirPhaseAndBurstInTurns()
        {
            var definition = Dummy("", "2:vent*3", "",
                Row("vents", "vent", "self", "0.8", "300", radius: "1.6", count: "3", spread: "1.5"), Idle);
            var (world, boss, _) = Awake(definition, Home + new Vec2(0, -5));
            Assert.AreEqual(0, world.Props.Count);
            world.DamageBoss(boss, 0, (int)(boss.MaxHealth * 0.4f), false);
            Assert.AreEqual(2, boss.Phase);
            var vents = world.Props.Where(p => p.Owner == boss.Id).ToList();
            Assert.AreEqual(3, vents.Count);
            Assert.IsTrue(vents.All(v => v.Kind == PropKind.Vent && !v.Breakable));
            Run(world, 2);
            int laid = world.Hazards.Count;
            Assert.IsTrue(world.ForceAttack(boss, "vents"));
            var bursts = world.Hazards.Skip(laid).Where(h => h.Shape == HazardShape.Circle).ToList();
            Assert.AreEqual(5, bursts.Count, "the even vents, the odd one, the even ones again");
            Assert.IsTrue(bursts.All(b => vents.Any(v => Vec2.Distance(v.Position, b.Position) < 1e-3f)));
            Assert.AreEqual(3, bursts.Select(b => b.ResolveTick).Distinct().Count());
            Assert.AreEqual(World.Ticks(3f), bursts.Max(b => b.ResolveTick) - bursts.Min(b => b.ResolveTick), 1, "three beats 1.5 seconds apart");
        }

        [Test]
        public void APillarStopsTheBossShotsAndStunsACharge()
        {
            var definition = Dummy("", "1:pillar", "",
                Row("rush", "charge", "farthest", "0.5", "300", length: "14", width: "2", speed: "20"),
                Row("shot", "burst", "nearest", "0.5", "300", radius: "0.3", angle: "10", count: "1", speed: "10"), Idle);
            var world = new World(Maps.Room(40), Spawn, Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });
            var boss = world.PlaceBoss(definition, Home, 10);
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(0, -9));
            world.Step();
            var pillar = world.Props.Single();
            Assert.AreEqual(PropKind.Pillar, pillar.Kind);
            // Behind the pillar from the boss.
            var behind = Home + (pillar.Position - Home).Normalized * 9;
            Walk(world, player, behind);
            float before = player.Health;
            Assert.IsTrue(world.ForceAttack(boss, "shot"));
            Run(world, 2);
            Assert.AreEqual(before, player.Health, "the shot stops at the pillar");
            Assert.IsTrue(world.ForceAttack(boss, "rush"));
            for (int i = 0; i < World.Ticks(2) && boss.State != BossState.Stun; i++) world.Step();
            Assert.AreEqual(BossState.Stun, boss.State, "into the pillar: stunned");
            Assert.AreEqual(before, player.Health);
            Run(world, World.StunSeconds + 0.1f);
            Assert.AreNotEqual(BossState.Stun, boss.State);
        }

        [Test]
        public void RageAfterEnoughHurtAddsABlowAndEndsInAnOpening()
        {
            var definition = Dummy("", "", "1:hurt=0.05 seconds=2 rest=1.5",
                Row("slam", "circle", "nearest", "0.6", "30", radius: "1", count: "1", weight: "1"));
            var (world, boss, _) = Awake(definition, Home + new Vec2(0, -3));
            world.DamageBoss(boss, 0, (int)(boss.MaxHealth * 0.03f), false);
            Assert.IsFalse(boss.Raging);
            world.TakeEvents();
            world.DamageBoss(boss, 0, (int)(boss.MaxHealth * 0.03f), false);
            Assert.IsTrue(boss.Raging, "hurt past its share: it rages");
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.Rage && e.Amount == 1));
            Assert.IsTrue(world.ForceAttack(boss, "slam"));
            Assert.AreEqual(1, boss.RepeatsLeft, "raging, a blow comes once more");
            bool spent = false;
            for (int i = 0; i < World.Ticks(5) && !spent; i++)
            {
                world.Step();
                spent = boss.State == BossState.Spent;
            }
            Assert.IsTrue(spent, "spent, it cowers");
            Assert.IsFalse(boss.Raging);
            Assert.IsFalse(boss.Invulnerable, "open to blows");
            Run(world, 1.6f);
            Assert.AreNotEqual(BossState.Spent, boss.State);
        }

        [Test]
        public void TheSnapshotCarriesPropsAndRage()
        {
            var self = new SelfState(0, new Vec2(1, 1), 50, 100, 5);
            var boss = new EntityState(7, EntityKind.Boss, 0, new Vec2(2, 2), new Vec2(0, -1), 10, 10, 0, flags: BossFlags.Raging);
            var message = new SnapshotMessage(1, self, new[] { boss }, new ProjectileState[0], new HazardState[0], new SimEvent[0])
            {
                Props = new[] { new PropState(9, PropKind.Vent, new Vec2(3, 4), 0.8f, 255) },
            };
            var back = SnapshotMessage.Decode(message.Encode());
            Assert.AreEqual(BossFlags.Raging, back.Entities[0].Flags);
            Assert.AreEqual(1, back.Props.Count);
            Assert.AreEqual(PropKind.Vent, back.Props[0].Kind);
            Assert.AreEqual(3, back.Props[0].Position.X, 0.05f);
            Assert.AreEqual(0.8f, back.Props[0].Radius, 0.05f);
        }

        [Test]
        public void TheSheetSaysWhatItMeans()
        {
            Assert.IsFalse(Bosses.Parse(Bosses.BossesHeader + "d,d,100,0,0,2,1,,1,3,10\n",
                Bosses.AttacksHeader.TrimEnd('\n') + ",warn,pool\nd,a,a,,burst,self,0.5,10,0.3,,,,4,,8,1,1,,,lava:5\n").Success, "no lava from shots");
            Assert.IsFalse(Bosses.Parse(Bosses.BossesHeader.TrimEnd('\n') + ",edge\nd,d,100,0,0,2,1,66,1,3,10,2:lava:9\n",
                Bosses.AttacksHeader + "d,a,a,,circle,nearest,0.5,10,1,,,,1,,,1,1,\n").Success, "an edge too wide");
            Assert.IsFalse(Bosses.Parse(Bosses.BossesHeader.TrimEnd('\n') + ",props\nd,d,100,0,0,2,1,66,1,3,10,2:vent*7\n",
                Bosses.AttacksHeader + "d,a,a,,circle,nearest,0.5,10,1,,,,1,,,1,1,\n").Success, "more props than a snapshot carries");
        }

        [Test]
        public void TheLavaTitanIsBuiltOnTheGround()
        {
            var titan = Bosses.Shipped.Find("lava_titan");
            Assert.AreEqual(EdgeKind.Lava, titan.Edge.Kind);
            Assert.AreEqual(3, titan.Edge.Phase);
            Assert.AreEqual(4, titan.Edge.Islands);
            Assert.AreEqual(3, titan.PropsByPhase[2].Sum(p => p.Count));
            Assert.AreEqual(5, titan.PropsByPhase[3].Sum(p => p.Count));
            Assert.IsNull(titan.RageIn(1));
            Assert.AreEqual(20, titan.RageIn(2).Seconds, 1e-4f);
            Assert.AreEqual(30, titan.RageIn(3).Seconds, 1e-4f);
            Assert.AreEqual(AttackShape.Refuge, titan.Attacks.Single(a => a.Id == "fire_rain").Shape);
            Assert.AreEqual(PoolKind.Rock, titan.Attacks.Single(a => a.Id == "meteor").Pool);
            Assert.IsTrue(titan.Attacks.Any(a => a.Pool == PoolKind.Lava && a.InPhase(1)), "phase 1 shows the lava");
            Assert.LessOrEqual(titan.Attacks.Count, 15);
        }
    }
}
