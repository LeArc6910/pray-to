using System;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The boss bundle R4b (2026-11-28, Docs/BossRedesign.md 5.6·5.8 and 7.1 T6·T8, Docs/Bosses.md "분신·되감기·별 미로"):
    // mirror images that form out of mist, go at one blow, scatter when the boss is struck, walk as it walks and copy its
    // blows later and softer; a blow from behind; lightning that flashes through a puddle; sunken hands that hold; the
    // rewind that lays the last blasts again newest first; a beam through a gate; the star maze; the mist interlude;
    // and the two bosses' sheets.
    public class BossMirrorTests
    {
        static readonly Vec2 Spawn = TileMap.Center(20, 5);
        static readonly Vec2 Home = TileMap.Center(20, 26);

        static void Walk(World world, Player player, Vec2 to)
        {
            for (int i = 0; i < 200 && Vec2.Distance(player.Position, to) > 0.001f; i++)
                Assert.IsTrue(world.MovePlayer(player.Id, player.Position + (to - player.Position).ClampLength(1.2f), player.Facing, 10));
        }

        static BossDefinition Dummy(int health, string element, string props, string interlude, params string[] attacks) => Bosses.Parse(
            Bosses.BossesHeader.TrimEnd('\n') + ",element,tempo,finisher_rest,edge,props,rage,interlude\n" + $"dummy,허수아비,{health},50,0,2,1,66|33,1,3,100,{element},,,,{props},,{interlude}\n",
            Bosses.AttacksHeader.TrimEnd('\n') + ",warn,pool,follow,boost,pull\n" + string.Concat(attacks.Select(a => "dummy," + a + "\n"))).Find("dummy");

        static string Row(string id, string shape, string target, string telegraph, string damage, string radius = "", string length = "", string angle = "",
            string width = "", string count = "", string spread = "", string speed = "", string cooldown = "1", string weight = "0", string warn = "",
            string pool = "", string phases = "", string summon = "", string boost = "") =>
            string.Join(",", id, id, phases, shape, target, telegraph, damage, radius, length, angle, width, count, spread, speed, cooldown, weight, summon, warn, pool, "", boost, "");

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

        // Into phase 2 and past its roar (images and boosts come from phase 2).
        static void ToPhaseTwo(World world, Boss boss, Player player)
        {
            world.DamageBoss(boss, player.Id, (int)(boss.MaxHealth * 0.4f), false);
            Assert.AreEqual(2, boss.Phase);
            Run(world, boss.Definition.Roar + 0.1f);
        }

        [Test]
        public void ImagesFormOutOfTheMistAndGoAtOneBlow()
        {
            var definition = Dummy(100000, "", "", "", Row("mirror", "clone", "self", "1.2", "", count: "2", spread: "12"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -5));
            Assert.IsTrue(world.ForceAttack(boss, "mirror"));
            Assert.IsTrue(boss.Invulnerable, "mist cannot be struck");
            var spots = world.Hazards.Where(h => h.Damage == 0 && h.Shape == HazardShape.Circle).ToList();
            Assert.AreEqual(3, spots.Count, "where it and its two images will form");
            Run(world, 1.3f);
            var clones = world.Props.Where(p => p.Kind == PropKind.Clone).ToList();
            Assert.AreEqual(2, clones.Count);
            Assert.IsTrue(spots.Any(s => Vec2.Distance(s.Position, boss.Position) < 0.01f), "the boss forms at one of the spots");
            Assert.IsTrue(clones.All(c => c.Breakable && c.Health <= 1), "an image goes at one blow");
            // One blow on an image: it is gone, the other stands.
            var clone = clones[0];
            Walk(world, player, clone.Position + new Vec2(0, -(clone.Radius + 0.6f)));
            Assert.IsTrue(world.MovePlayer(player.Id, player.Position, new Vec2(0, 1), 10));
            world.SetAttacking(player.Id, true);
            for (int i = 0; i < World.Ticks(2) && world.Props.Contains(clone); i++) world.Step();
            world.SetAttacking(player.Id, false);
            Assert.IsFalse(world.Props.Contains(clone));
        }

        [Test]
        public void ABlowOnTheBossItselfScattersItsImages()
        {
            var definition = Dummy(100000, "", "", "", Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -5));
            Assert.IsTrue(world.ForceClones(boss, 3));
            Assert.AreEqual(3, world.Props.Count(p => p.Kind == PropKind.Clone));
            world.DamageBoss(boss, player.Id, 10, false);
            Assert.AreEqual(0, world.Props.Count(p => p.Kind == PropKind.Clone));
        }

        [Test]
        public void ImagesWalkAsTheBossWalks()
        {
            var definition = Dummy(100000, "", "", "", Row("reach", "cone", "nearest", "1", "10", length: "2", angle: "90", weight: "1"));
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -8));
            Assert.IsTrue(world.ForceClones(boss, 2));
            var clone = world.Props.First(p => p.Kind == PropKind.Clone);
            var (bossFrom, cloneFrom) = (boss.Position, clone.Position);
            Run(world, 0.5f);
            var walked = boss.Position - bossFrom;
            Assume.That(walked.Length, Is.GreaterThan(0.3f), "the boss walks toward the player");
            Assert.AreEqual(walked.X, (clone.Position - cloneFrom).X, 0.15f);
            Assert.AreEqual(walked.Y, (clone.Position - cloneFrom).Y, 0.15f);
        }

        [Test]
        public void ImagesCopyAMimickedBlowLaterAndSofter()
        {
            var definition = Dummy(100000, "", "", "", Row("grasp", "cone", "nearest", "0.8", "100", length: "3", angle: "110", boost: "2:mimic=1"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -5));
            ToPhaseTwo(world, boss, player);
            Assert.IsTrue(world.ForceClones(boss, 2));
            int before = world.Hazards.Count;
            Assert.IsTrue(world.ForceAttack(boss, "grasp"));
            var cones = world.Hazards.Skip(before).Where(h => h.Shape == HazardShape.Cone).OrderBy(h => h.ResolveTick).ToList();
            Assert.AreEqual(3, cones.Count, "the boss's and each image's");
            Assert.AreEqual(100, cones[0].Damage);
            Assert.AreEqual((int)MathF.Round(100 * World.CloneShare), cones[1].Damage);
            Assert.AreEqual(World.Ticks(World.CloneLag), cones[1].ResolveTick - cones[0].ResolveTick, 1);
            Assert.AreEqual(World.Ticks(World.CloneLag), cones[2].ResolveTick - cones[1].ResolveTick, 1);
            var clones = world.Props.Where(p => p.Kind == PropKind.Clone).ToList();
            Assert.IsTrue(cones.Skip(1).All(c => clones.Any(k => Vec2.Distance(k.Position, c.Position) < 0.01f)), "each from where an image stands");
        }

        [Test]
        public void ABlowFromBehindComesOutOnTheFarSide()
        {
            var definition = Dummy(100000, "", "", "", Row("grasp", "cone", "nearest", "0.8", "100", length: "3", angle: "110", boost: "2:behind=1"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -6));
            ToPhaseTwo(world, boss, player);
            var before = boss.Position;
            Assert.IsTrue(world.ForceAttack(boss, "grasp"));
            Assert.Less(Vec2.Dot((boss.Position - player.Position).Normalized, (before - player.Position).Normalized), -0.5f, "behind the target");
            Assert.Less(Vec2.Distance(boss.Position, player.Position), 3, "close enough to grasp");
            Assert.Greater(Vec2.Dot(boss.Facing, player.Position - boss.Position), 0, "facing the target");
        }

        [Test]
        public void LightningThatTouchesAPuddleFlashesThroughIt()
        {
            var definition = Dummy(100000, "lightning", "1:puddle*2", "", Row("bolt", "circle", "nearest", "1", "100", radius: "1.2", count: "1"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -5));
            var puddle = world.Props.Where(p => p.Kind == PropKind.Puddle).OrderBy(p => Vec2.Distance(p.Position, player.Position)).First();
            Assert.IsFalse(puddle.Breakable);
            Assert.AreEqual(World.PuddleRadius, puddle.Radius, 1e-4f);
            // Stand at the puddle's edge: the bolt falls there and the whole puddle flashes.
            Walk(world, player, puddle.Position + (Home - puddle.Position).Normalized * (puddle.Radius - 0.2f));
            int before = world.Hazards.Count;
            Assert.IsTrue(world.ForceAttack(boss, "bolt"));
            var flash = world.Hazards.Skip(before).SingleOrDefault(h => Vec2.Distance(h.Position, puddle.Position) < 0.01f);
            Assert.IsNotNull(flash, "the puddle is marked too");
            Assert.AreEqual(puddle.Radius, flash.Size, 1e-4f);
            Assert.AreEqual((int)MathF.Round(100 * World.PuddleShare), flash.Damage);
            // Not without lightning.
            var dull = Dummy(100000, "", "1:puddle*2", "", Row("bolt", "circle", "nearest", "1", "100", radius: "1.2", count: "1"), Idle);
            var (world2, boss2, player2) = Awake(dull, Home + new Vec2(0, -5));
            var puddle2 = world2.Props.Where(p => p.Kind == PropKind.Puddle).OrderBy(p => Vec2.Distance(p.Position, player2.Position)).First();
            Walk(world2, player2, puddle2.Position);
            Assert.IsTrue(world2.ForceAttack(boss2, "bolt"));
            Assert.AreEqual(1, world2.Hazards.Count(h => h.Damage > 0 && h.Shape == HazardShape.Circle));
        }

        [Test]
        public void SunkenHandsRiseFromEveryPuddleAndHold()
        {
            var definition = Dummy(100000, "", "1:puddle*3", "", Row("hands", "hands", "self", "1", "50", radius: "1.3"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -5));
            var puddles = world.Props.Where(p => p.Kind == PropKind.Puddle).ToList();
            Walk(world, player, puddles[0].Position);
            int before = world.Hazards.Count;
            Assert.IsTrue(world.ForceAttack(boss, "hands"));
            var hands = world.Hazards.Skip(before).ToList();
            Assert.AreEqual(3, hands.Count, "one out of every puddle");
            Assert.IsTrue(hands.All(h => puddles.Any(p => Vec2.Distance(p.Position, h.Position) < 0.01f)));
            Run(world, 1.05f);
            Assert.IsTrue(player.Afflictions.Rooted, "held where they stand");
            Run(world, World.HandHold + 0.1f);
            Assert.IsFalse(player.Afflictions.Rooted);
        }

        [Test]
        public void TheRewindLaysTheLastBlastsAgainNewestFirst()
        {
            var definition = Dummy(100000, "", "", "",
                Row("drop", "circle", "nearest", "0.5", "100", radius: "1", count: "1"),
                Row("rewind", "rewind", "self", "1", "", spread: "5"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -6));
            var first = player.Position;
            Assert.IsTrue(world.ForceAttack(boss, "drop"));
            Walk(world, player, first + new Vec2(3, 0));
            Run(world, 1);
            var second = player.Position;
            Assert.IsTrue(world.ForceAttack(boss, "drop"));
            Walk(world, player, first + new Vec2(-3, 0));
            Run(world, 1);
            int before = world.Hazards.Count;
            Assert.IsTrue(world.ForceAttack(boss, "rewind"));
            var echoes = world.Hazards.Skip(before).Where(h => h.Echo).OrderBy(h => h.ResolveTick).ToList();
            Assert.AreEqual(2, echoes.Count);
            Assert.Less(Vec2.Distance(echoes[0].Position, second), 0.01f, "the newest lands first");
            Assert.Less(Vec2.Distance(echoes[1].Position, first), 0.01f);
            Assert.AreEqual((int)MathF.Round(100 * World.EchoShare), echoes[0].Damage);
            // The echoes themselves are not kept to come again.
            Run(world, 3);
            int again = world.Hazards.Count;
            Assert.IsTrue(world.ForceAttack(boss, "rewind"));
            Assert.AreEqual(2, world.Hazards.Skip(again).Count(h => h.Echo), "only the first two blasts, not their echoes");
        }

        [Test]
        public void ABeamGoesInAtOneGateAndOutOfTheOther()
        {
            var definition = Dummy(100000, "", "1:gate*2", "", Row("beam", "beam", "nearest", "0.9", "100", length: "13", width: "1.6", count: "1"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -4));
            var gates = world.Props.Where(p => p.Kind == PropKind.Gate).ToList();
            Assert.AreEqual(2, gates.Count);
            Assert.IsFalse(gates[0].Breakable);
            // Stand beyond a gate, straight on from the boss.
            var gate = gates.OrderBy(g => Vec2.Distance(g.Position, player.Position)).First();
            var way = (gate.Position - boss.Position).Normalized;
            Walk(world, player, gate.Position + way * 1.5f);
            way = (player.Position - boss.Position).Normalized;
            int before = world.Hazards.Count;
            Assert.IsTrue(world.ForceAttack(boss, "beam"));
            var beams = world.Hazards.Skip(before).Where(h => h.Shape == HazardShape.Line).ToList();
            Assert.AreEqual(2, beams.Count, "in at one gate, out of the other");
            var other = gates.Single(g => g != gate);
            Assert.Less(Vec2.Distance(beams[1].Position, other.Position), 0.01f, "it comes out of the other gate");
            Assert.AreEqual(1, Vec2.Dot(beams[0].Direction, beams[1].Direction), 0.01f, "going the same way");
            Assert.AreEqual(13, beams[0].Size + beams[1].Size, 0.01f, "the same length in all");
        }

        [Test]
        public void TheStarMazeFallsFromTheOutsideInEachRingWithAGap()
        {
            var definition = Dummy(200, "", "", "3:maze:3:12:-", Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -5));
            world.DamageBoss(boss, player.Id, 140, false);
            Run(world, boss.Definition.Roar + 0.1f);
            Assert.AreEqual(BossState.Interlude, boss.State);
            Assert.IsTrue(boss.Invulnerable);
            var rings = world.Hazards.Where(h => h.Shape == HazardShape.Maze).OrderBy(h => h.Order).ToList();
            Assert.AreEqual(3, rings.Count);
            for (int k = 1; k < rings.Count; k++)
            {
                Assert.Greater(rings[k].ResolveTick, rings[k - 1].ResolveTick, "the outer falls first");
                Assert.AreEqual(rings[k - 1].Width, rings[k].Size, 0.01f, "each ring inside the last");
            }
            var outer = rings[0];
            float middle = (outer.Size + outer.Width) / 2;
            Assert.IsFalse(outer.Contains(boss.Home + outer.Direction * Math.Min(middle, outer.Width + 1), World.PlayerRadius), "its gap is safe");
            Assert.IsTrue(outer.Contains(boss.Home - outer.Direction * Math.Min(middle, outer.Width + 1), World.PlayerRadius), "across from its gap is not");
            Assert.IsFalse(outer.Contains(boss.Home, World.PlayerRadius), "the middle is not in the outer ring");
            // Once fallen it burns on.
            Run(world, (outer.ResolveTick - world.Tick) * World.TickSeconds + 0.05f);
            Assert.IsTrue(world.Hazards.Any(h => h.Shape == HazardShape.Maze && h.Burning && h.Order == 1), "the fallen ring burns on");
            Run(world, 12);
            Assert.AreNotEqual(BossState.Interlude, boss.State);
            Assert.IsFalse(world.Hazards.Any(h => h.Shape == HazardShape.Maze), "the maze is gone with the interlude");
        }

        [Test]
        public void TheMistInterludeRainsAndEndsWithImages()
        {
            var definition = Dummy(200, "lightning", "", "2:mist:2:6:bolt", Row("bolt", "circle", "random", "1", "1", radius: "1.2", count: "1"), Idle);
            var (world, boss, player) = Awake(definition, Home + new Vec2(0, -5));
            world.DamageBoss(boss, player.Id, 80, false);
            Assert.AreEqual(2, boss.Phase);
            Run(world, boss.Definition.Roar + 0.1f);
            Assert.AreEqual(BossState.Interlude, boss.State);
            Assert.IsTrue(boss.Invulnerable);
            Run(world, 1.6f);
            Assert.IsTrue(world.Hazards.Any(h => h.Damage > 0), "something falls while it is mist");
            Run(world, 5);
            Assert.AreNotEqual(BossState.Interlude, boss.State);
            Assert.AreEqual(2, world.Props.Count(p => p.Kind == PropKind.Clone), "it forms again with two images");
        }

        [Test]
        public void TheWraithAndTheVoidLordHaveTheirNewWays()
        {
            var wraith = Bosses.Shipped.Find("crown_wraith");
            Assert.AreEqual(InterludeKind.Mist, wraith.Interlude.Kind);
            Assert.AreEqual(2, wraith.Interlude.Phase);
            Assert.AreEqual(AttackShape.Circle, wraith.Interlude.Charge.Shape);
            Assert.AreEqual(AttackShape.Clone, wraith.Attacks.Single(a => a.Id == "mirror").Shape);
            Assert.AreEqual(3, wraith.Attacks.Single(a => a.Id == "mirror").ForPhase(3).Count);
            Assert.AreEqual(AttackShape.Hands, wraith.Attacks.Single(a => a.Id == "sunken_hands").Shape);
            var grasp = wraith.Attacks.Single(a => a.Id == "grasp");
            Assert.IsFalse(grasp.Behind);
            Assert.IsTrue(grasp.ForPhase(2).Behind && grasp.ForPhase(2).Mimic);
            Assert.IsTrue(wraith.Attacks.Single(a => a.Id == "soul_lance").ForPhase(3).Mimic);
            Assert.AreEqual(4, wraith.PropsByPhase[3].Sum(p => p.Count));
            Assert.IsFalse(wraith.Attacks.Any(a => a.Id == "soul_ripple" || a.Id == "deep_share"));

            var lord = Bosses.Shipped.Find("void_lord");
            Assert.AreEqual(InterludeKind.Maze, lord.Interlude.Kind);
            Assert.AreEqual(3, lord.Interlude.Phase);
            Assert.AreEqual(AttackShape.Rewind, lord.Attacks.Single(a => a.Id == "rewind").Shape);
            Assert.AreEqual(8, lord.Attacks.Single(a => a.Id == "rewind").ForPhase(3).Spread, 0.01f);
            Assert.AreEqual(AttackShape.Beam, lord.Attacks.Single(a => a.Id == "beam").Shape);
            Assert.AreEqual(AttackShape.Leap, lord.Attacks.Single(a => a.Id == "blink").Shape);
            var hand = lord.Attacks.Single(a => a.Id == "grasp");
            Assert.Greater(hand.Pull, 0, "the grasp pulls");
            Assert.Less(hand.ForPhase(3).Pull, 0, "gravity turned over: it pushes");
            Assert.Less(lord.Attacks.Single(a => a.Id == "event_horizon").Pull, 0, "pushed out of the safe middle");
            Assert.Greater(lord.Attacks.Single(a => a.Id == "black_holes").Pull, 0);
            Assert.AreEqual(PropKind.Gate, lord.PropsByPhase[2].Single().Kind);
            foreach (var gone in new[] { "collapse", "void_storm", "call_wisps", "void_cross", "void_spiral", "void_lance", "void_ripple" })
                Assert.IsFalse(lord.Attacks.Any(a => a.Id == gone), gone);
        }
    }
}
