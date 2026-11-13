using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The venom spider of the mushroom hollow (2026-11-13, Docs/Bosses.md 독그물 거미 아락사): it shoots webs that stay on
    // the floor and hold whoever steps in them, and later plucks them so that every web bursts at once.
    public class SpiderTests
    {
        static readonly Vec2 Spawn = TileMap.Center(20, 5);
        static readonly Vec2 Home = TileMap.Center(20, 26);
        const float ArenaRadius = 8;

        const string Header =
            "boss,id,name,phases,shape,target,telegraph,damage,radius,length,angle,width,count,spread,speed,cooldown,weight,summon,warn\n";

        static BossDefinition With(string rows) =>
            Bosses.Parse(Bosses.BossesHeader + "dummy,허수아비,100000,50,0,2,1,,1,3,10\n", Header + rows).Find("dummy");

        static World Arena(out Boss boss, BossDefinition definition)
        {
            var world = new World(Maps.Room(40), Spawn, Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });
            boss = world.PlaceBoss(definition, Home, ArenaRadius);
            return world;
        }

        // In steps the host accepts, small ones while a web holds the player.
        static void Walk(World world, Player player, Vec2 to)
        {
            for (int i = 0; i < 400 && Vec2.Distance(player.Position, to) > 0.001f; i++)
                Assert.IsTrue(world.MovePlayer(player.Id, player.Position + (to - player.Position).ClampLength(0.5f), player.Facing, 10));
        }

        static List<SimEvent> Run(World world, float seconds)
        {
            var events = new List<SimEvent>();
            for (int i = 0; i < World.Ticks(seconds); i++)
            {
                world.Step();
                events.AddRange(world.TakeEvents());
            }
            return events;
        }

        const string Web = "dummy,web,거미줄,,web,nearest,1,20,1.5,4,,,1,,,30,1,,\n";
        const string Pluck = "dummy,pluck,터뜨리기,,pluck,self,1,50,1.2,,,,,,,30,0,,\n";

        [Test]
        public void AWebLandsWhereItsTargetStoodAndHoldsWhoeverStandsInIt()
        {
            var world = Arena(out var boss, With(Web));
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(0, -4));
            float free = player.MoveSpeed;
            Run(world, 0.1f);
            var mark = world.Hazards.Single();
            Assert.AreEqual(HazardShape.Circle, mark.Shape, "marked like any circle while it comes");
            Assert.AreEqual(4, mark.Lingers);

            float health = player.Health;
            Run(world, 1);
            Assert.Less(player.Health, health, "it hurts where it lands");
            var web = world.Hazards.Single();
            Assert.AreEqual(HazardShape.Web, web.Shape);
            Assert.AreEqual(0, web.Damage, "a web does no harm");
            Assert.AreEqual(mark.Position, web.Position);
            Run(world, 0.1f);
            Assert.AreEqual(free * World.WebSpeed, player.MoveSpeed, 0.001f, "stuck in it");

            // Out of the web it lets go a moment later.
            Walk(world, player, Home + new Vec2(5, -4));
            Run(world, 0.4f);
            Assert.AreEqual(free, player.MoveSpeed, 0.001f);

            // And it is gone after its time.
            Run(world, 4);
            Assert.IsFalse(world.Hazards.Any(h => h.Shape == HazardShape.Web));
            Assert.AreNotEqual(BossState.Dormant, boss.State);
        }

        [Test]
        public void PluckingBurstsEveryWebAndTakesThemAway()
        {
            var world = Arena(out var boss, With(Web + Pluck));
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(0, -4));
            Run(world, 1.2f);
            var web = world.Hazards.Single(h => h.Shape == HazardShape.Web);
            float before = player.Health;

            // Standing in the web as it is plucked.
            Assert.IsTrue(world.ForceAttack(boss, "pluck"));
            var burst = world.Hazards.Single(h => h.Shape == HazardShape.Circle);
            Assert.AreEqual(web.Position, burst.Position, "the burst is on the web");
            Assert.AreEqual(web.Size + World.PluckReach, burst.Size, 0.001f);
            Run(world, 1.1f);
            Assert.Less(player.Health, before, "standing in it hurts");
            Assert.IsFalse(world.Hazards.Any(h => h.Shape == HazardShape.Web), "the plucked web is gone");

            // With no web down, it bursts under each fighter instead.
            Assert.IsTrue(world.ForceAttack(boss, "pluck"));
            var under = world.Hazards.Single();
            Assert.AreEqual(player.Position, under.Position);
            Assert.AreEqual(1.2f, under.Size, 0.001f);
        }

        [Test]
        public void ItKeepsOnlySoManyWebsDown()
        {
            var world = Arena(out var boss, With("dummy,web,거미줄,,web,self,0.5,20,1,30,,,4,6,,0.2,1,,\n"));
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(0, -4));
            Run(world, 12);
            int webs = world.Hazards.Count(h => h.Shape == HazardShape.Web);
            Assert.AreEqual(World.MostWebs, webs);
        }

        [Test]
        public void AWebNeedsHowLongItStays()
        {
            var wrong = Bosses.Parse(Bosses.BossesHeader + "dummy,허수아비,1000,50,0,2,1,,1,3,10\n", Header + "dummy,a,공격,,web,nearest,1,30,2,,,,1,,,1,1,,\n");
            Assert.IsFalse(wrong.Success);
            StringAssert.Contains("length", string.Join(" ", wrong.Errors));
        }

        // The sheet: in the mushroom hollow (the seventh boss with no place), every attack marked on the floor, only the
        // ceiling drop and the web burst with the warning sign and both hitting harder than anything else; each phase
        // brings attacks of its own; it darts and freezes as it closes in; it never charges (the queen and the knights
        // do), its own kind of attack being the web.
        [Test]
        public void TheSpiderMarksEveryAttackAndWarnsOfItsTwoWorst()
        {
            var spider = Bosses.Shipped.Find("venom_spider");
            Assert.IsNull(spider.Place);
            Assert.AreEqual(6, Bosses.Shipped.Bosses.Where(b => b.Place == null).ToList().IndexOf(spider), "the seventh cave arena");
            CollectionAssert.AreEquivalent(new[] { "drop", "pluck" }, spider.Attacks.Where(a => a.Warning == AttackWarning.Strong).Select(a => a.Id));
            Assert.IsTrue(spider.Attacks.All(a => a.Warning != AttackWarning.Hidden));
            Assert.Greater(spider.Attacks.Where(a => a.Warning == AttackWarning.Strong).Min(a => a.Damage),
                spider.Attacks.Where(a => a.Warning != AttackWarning.Strong).Max(a => a.Damage));
            for (int phase = 2; phase <= spider.PhaseCount; phase++)
                Assert.IsTrue(spider.Attacks.Any(a => a.Phases.Count > 0 && a.Phases.Min() == phase), $"phase {phase} brings something new");
            Assert.IsTrue(spider.Bursts);
            Assert.IsFalse(spider.Attacks.Any(a => a.Shape == AttackShape.Charge));
            Assert.IsTrue(spider.Attacks.Any(a => a.Shape == AttackShape.Web && a.Phases.Count == 0), "webs from the first phase");
            var pluck = spider.Attacks.Single(a => a.Shape == AttackShape.Pluck);
            CollectionAssert.AreEqual(new[] { 3 }, pluck.Phases, "it plucks once the floor is full of webs");
            Assert.AreEqual("spiderling", spider.Attacks.Single(a => a.Shape == AttackShape.Summon).Summon.Id);
            Assert.AreEqual(Tell.Area, World.TellOf(AttackShape.Web));
            Assert.AreEqual(Tell.Area, World.TellOf(AttackShape.Pluck));
        }
    }
}
