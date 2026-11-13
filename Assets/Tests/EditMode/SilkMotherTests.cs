using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The silk mother of the forgotten shrine's second arena (2026-11-13, Docs/Bosses.md 실 잣는 어미 아라네): a spider
    // that takes the arena piece by piece. Its webs stay where they land and snare whoever walks through them, its venom
    // stays as pools that hurt whoever stands in them (boss_attacks.csv pool), and its last blow drives all eight legs
    // down round it (a cross of four beams).
    public class SilkMotherTests
    {
        static readonly Vec2 Spawn = TileMap.Center(20, 5);
        static readonly Vec2 Home = TileMap.Center(20, 26);
        const float ArenaRadius = 8;

        const string Header =
            "boss,id,name,phases,shape,target,telegraph,damage,radius,length,angle,width,count,spread,speed,cooldown,weight,summon,warn,pool\n";

        static BossData Sheet(string rows) => Bosses.Parse(Bosses.BossesHeader + "dummy,허수아비,1000,50,0,2,1,,1,3,10\n", Header + rows);

        static World Arena(out Boss boss, string rows)
        {
            var world = new World(Maps.Room(40), Spawn, Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });
            boss = world.PlaceBoss(Sheet(rows).Find("dummy"), Home, ArenaRadius);
            return world;
        }

        static void Walk(World world, Player player, Vec2 to, float stride = 2)
        {
            for (int i = 0; i < 100 && Vec2.Distance(player.Position, to) > 0.001f; i++)
                Assert.IsTrue(world.MovePlayer(player.Id, player.Position + (to - player.Position).ClampLength(stride), player.Facing, 10));
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

        [Test]
        public void TheSheetTellsItsWindUpsFromItsWarnings()
        {
            var mother = Bosses.Shipped.Find("silk_mother");
            Assert.AreEqual("old_temple:2", mother.Place);
            Assert.AreEqual(36, mother.Level);
            // Its fangs are read from its body alone; the two heaviest blows carry the warning sign; the rest are marked.
            Assert.AreEqual(AttackWarning.Hidden, mother.Attacks.Single(a => a.Id == "fang").Warning);
            CollectionAssert.AreEquivalent(new[] { "thread_drop", "eight_legs" }, mother.Attacks.Where(a => a.Warning == AttackWarning.Strong).Select(a => a.Id));
            Assert.Greater(mother.Attacks.Where(a => a.Warning == AttackWarning.Strong).Min(a => a.Damage),
                mother.Attacks.Where(a => a.Warning != AttackWarning.Strong).Max(a => a.Damage), "the warning is kept for what hurts most");
            Assert.Greater(mother.Attacks.Where(a => a.Warning == AttackWarning.Strong).Min(a => a.Telegraph),
                mother.Attacks.Where(a => a.Warning != AttackWarning.Strong).Max(a => a.Telegraph), "and for what gives longest to get clear");
            for (int phase = 2; phase <= mother.PhaseCount; phase++)
                Assert.IsTrue(mother.Attacks.Any(a => a.Phases.Count > 0 && a.Phases.Min() == phase), $"phase {phase} brings something new");
            // Silk and venom stay on the ground.
            Assert.AreEqual(PoolKind.Web, mother.Attacks.Single(a => a.Id == "web_shot").Pool);
            Assert.AreEqual(PoolKind.Web, mother.Attacks.Single(a => a.Id == "thread_drop").Pool);
            Assert.AreEqual(PoolKind.Venom, mother.Attacks.Single(a => a.Id == "venom_pool").Pool);
            Assert.IsTrue(mother.Attacks.Where(a => a.Pool != PoolKind.None).All(a => a.Linger >= 5 && a.Linger <= 8));
            // Nobody before it leaves anything behind.
            foreach (var boss in Bosses.Shipped.Bosses.Where(b => b.Id != "silk_mother"))
                Assert.IsTrue(boss.Attacks.All(a => a.Pool == PoolKind.None), boss.Id);
            // Every attack fits in what the view is told of it (World.BossPhaseField carries fifteen).
            Assert.LessOrEqual(mother.Attacks.Count, 15);
            // Its young come only when it calls them.
            var young = mother.Attacks.Single(a => a.Shape == AttackShape.Summon).Summon;
            Assert.AreEqual("silk_spiderling", young.Id);
            Assert.AreEqual(0, young.Weight, "never met in the caves");
        }

        [Test]
        public void ThePoolColumnIsCheckedWhenTheSheetIsRead()
        {
            Assert.IsTrue(Sheet("dummy,a,거미줄,,circle,nearest,1,30,2,,,,1,,,1,1,,,web:6\n").Success);
            foreach (string cell in new[] { "glue:3", "web", "web:0", "web:99", "venom:x" })
            {
                var wrong = Sheet($"dummy,a,거미줄,,circle,nearest,1,30,2,,,,1,,,1,1,,,{cell}\n");
                Assert.IsFalse(wrong.Success, cell);
                Assert.IsTrue(wrong.Errors.Any(e => e.Contains("pool")), string.Join(" / ", wrong.Errors));
            }
            // Only what lands as a circle can leave a pool.
            Assert.IsFalse(Sheet("dummy,a,물기,,cone,nearest,1,30,,4,90,,,,,1,1,,,web:6\n").Success);
        }

        [Test]
        public void AWebStaysWhereItLandedAndSnaresWhoeverWalksThroughIt()
        {
            var world = Arena(out var boss, "dummy,a,거미줄 뿜기,,circle,nearest,1,30,2,,,,1,,,5,1,,,web:3\n");
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(0, -5));
            var stood = player.Position;
            float pace = player.MoveSpeed;

            Run(world, 0.2f);
            Assert.AreEqual(HazardShape.Circle, world.Hazards.Single().Shape);
            Run(world, 1.0f);
            Assert.AreEqual(PlayerStats.BaseHealth - 30, player.Health, 0.01f, "the blow itself");
            var web = world.Hazards.Single();
            Assert.AreEqual(HazardShape.Web, web.Shape);
            Assert.IsTrue(web.Lingers);
            Assert.AreEqual(stood, web.Position);
            Assert.AreEqual(2f, web.Size);
            Assert.AreEqual(0, web.Damage, "a web does not hurt");

            Run(world, 0.1f);
            Assert.IsTrue(player.Afflictions.Snared);
            Assert.AreEqual(pace * World.PlayerSnareSpeed, player.MoveSpeed, 0.001f);
            Assert.AreNotEqual(0, player.Afflictions.Bits & StatusBits.Snared);
            Assert.AreEqual(PlayerStats.BaseHealth - 30, player.Health, 0.01f, "no more harm from standing in it");

            // Out of it (at a snared pace: the host will not let them go faster), free again a moment later.
            Assert.IsFalse(world.MovePlayer(player.Id, player.Position + new Vec2(2, 0), player.Facing, 10), "too fast for someone snared");
            Walk(world, player, stood + new Vec2(4, 0), 0.9f);
            Run(world, World.SnareSeconds + 0.1f);
            Assert.IsFalse(player.Afflictions.Snared);
            Assert.AreEqual(pace, player.MoveSpeed, 0.001f);

            // And the web itself is gone when its time is up, without a blast.
            var events = Run(world, 3f);
            Assert.IsFalse(world.Hazards.Any(h => h.Lingers));
            Assert.IsFalse(events.Any(e => e.Type == SimEventType.Impact && e.Target == web.Id));
        }

        [Test]
        public void VenomHurtsWhoeverStandsInItEveryHalfSecond()
        {
            var world = Arena(out var boss, "dummy,a,독 웅덩이,,circle,nearest,1,50,2,,,,1,,,6,1,,,venom:3\n");
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(0, -5));
            var stood = player.Position;

            Run(world, 1.25f);
            var pool = world.Hazards.Single();
            Assert.AreEqual(HazardShape.Venom, pool.Shape);
            int bite = (int)System.MathF.Round(50 * World.VenomShare);
            Assert.AreEqual(bite, pool.Damage);
            float after = PlayerStats.BaseHealth - 50;
            Assert.AreEqual(after, player.Health, 0.01f);

            Run(world, 1.05f);
            Assert.AreEqual(after - 2 * bite, player.Health, 0.01f, "two bites in a second");
            Assert.IsFalse(player.Afflictions.Snared, "venom does not snare");

            Walk(world, player, stood + new Vec2(4, 0));
            Run(world, 1f);
            Assert.AreEqual(after - 2 * bite, player.Health, 0.01f, "none outside it");
        }

        [Test]
        public void PoolsGoWithTheBossAndNeverFillTheArena()
        {
            var world = Arena(out var boss, "dummy,a,거미줄 비,,circle,nearest,0.5,1,1,,,,6,4,,0.2,1,,,web:30\n");
            var player = world.AddPlayer("a", null);
            Walk(world, player, Home + new Vec2(0, -5));
            Run(world, 4f);
            Assert.AreEqual(World.MaxPools, world.Hazards.Count(h => h.Lingers), "the oldest make way");

            world.Hurt(boss, (int)boss.MaxHealth + 1);
            Run(world, 0.1f);
            Assert.IsFalse(world.Hazards.Any(), "its webs fall with it");
        }

        [Test]
        public void EightLegsComeDownRoundItWithGapsBetweenAndRoomUnderItsBelly()
        {
            var world = Arena(out var boss, "dummy,a,여덟 다리,,cross,self,1,40,2.2,6,22.5,1,2,,,5,1,,strong\n");
            var player = world.AddPlayer("a", null);
            // Under a leg (22.5 degrees round from east), three and a half tiles out.
            Walk(world, player, boss.Position + Vec2.FromAngle(22.5f * System.MathF.PI / 180f) * 3.5f);
            var friend = world.AddPlayer("b", null);
            // Between two legs (45 degrees), further out where the gap is wide.
            Walk(world, friend, boss.Position + Vec2.FromAngle(System.MathF.PI / 4) * 5);
            var bold = world.AddPlayer("c", null);
            // Right up against it, on the line of a leg: under its belly.
            Walk(world, bold, boss.Position + Vec2.FromAngle(22.5f * System.MathF.PI / 180f) * (boss.Definition.Radius + World.PlayerRadius));

            Run(world, 0.2f);
            Assert.AreEqual(8, world.Hazards.Count, "eight legs");
            Assert.IsTrue(world.Hazards.All(h => h.Shape == HazardShape.Line && h.Size == 6 - 2.2f));
            Run(world, 1f);
            Assert.AreEqual(PlayerStats.BaseHealth - 40, player.Health, 0.01f, "under a leg, once");
            Assert.AreEqual(PlayerStats.BaseHealth, friend.Health, 0.01f, "between two legs");
            Assert.AreEqual(PlayerStats.BaseHealth, bold.Health, 0.01f, "under its belly");
        }

        // The shrine has two arenas now: the samurai's, and the silk mother's further out (places.csv arena 2).
        [Test]
        public void ItWaitsInTheShrinesSecondArena()
        {
            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped, story: Tales.Shipped);
            var mother = host.World.Bosses.Single(b => b.Definition.Id == "silk_mother");
            var samurai = host.World.Bosses.Single(b => b.Definition.Id == "shrine_samurai");
            Assert.Greater(Vec2.Distance(mother.Home, samurai.Home), 2 * WorldGenerator.ArenaRadius, "an arena each");
            Assert.AreEqual(2, Tales.Shipped.Places.Single(p => p.Id == "old_temple").Arenas);
            Assert.AreEqual(BossState.Dormant, mother.State);
        }
    }
}
