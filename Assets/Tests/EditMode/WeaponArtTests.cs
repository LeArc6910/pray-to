using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // Each weapon kind's own art (Docs/Weapons.md 묶음 C, World.WeaponArts.cs).
    public class WeaponArtTests
    {
        static readonly MonsterDefinition Slime = Monsters.Shipped.Find("slime");

        static World Room(int size = 30) =>
            new World(Maps.Room(size), TileMap.Center(size / 2, size / 2), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });

        static List<SimEvent> Run(World world, int ticks)
        {
            var events = new List<SimEvent>();
            for (int i = 0; i < ticks; i++)
            {
                world.Step();
                events.AddRange(world.TakeEvents());
            }
            return events;
        }

        static (World World, Player Player) Holding(string weapon, string spare = null)
        {
            var world = Room();
            var equipment = Gear.Wearing(weapon);
            if (spare != null) equipment.EquipSpare(Gear.Plain(spare));
            var player = world.AddPlayer("a", equipment);
            // The monsters stand where they are put, so each art's reach is what is tried (a slime set free leaps out
            // of an orb's well at the player).
            world.MonstersHold = true;
            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
            return (world, player);
        }

        // A slime that lives through anything, at so far to the player's right (and up).
        static Enemy Tough(World world, Player player, float x, float y = 0)
        {
            var slime = world.SpawnEnemy(Slime, player.Position + new Vec2(x, y));
            slime.Health = slime.MaxHealth = 1_000_000;
            return slime;
        }

        static int Lost(Enemy enemy) => (int)(enemy.MaxHealth - enemy.Health);

        static readonly Vec2 Right = new Vec2(1, 0);

        [Test]
        public void EveryKindToFightWithHasItsArtAndThePickaxeNone()
        {
            CollectionAssert.IsEmpty(Gear.Data.Errors);
            var expected = new Dictionary<string, WeaponArtKind>
            {
                ["sword"] = WeaponArtKind.Parry, ["dagger"] = WeaponArtKind.Vital, ["greatsword"] = WeaponArtKind.Charge, ["spear"] = WeaponArtKind.Dash,
                ["hammer"] = WeaponArtKind.Slam, ["bow"] = WeaponArtKind.Backstep, ["crossbow"] = WeaponArtKind.Pierce, ["throwing"] = WeaponArtKind.Whirl,
                ["staff"] = WeaponArtKind.Wave, ["wand"] = WeaponArtKind.Barrage, ["orb"] = WeaponArtKind.Gravity,
            };
            foreach (var kind in Gear.Data.WeaponKinds)
            {
                if (kind.Id == "pickaxe")
                {
                    Assert.IsNull(kind.Art);
                    continue;
                }
                Assert.AreEqual(expected[kind.Id], kind.Art.Kind, kind.Id);
                Assert.IsNotEmpty(kind.Art.Name);
                Assert.Greater(kind.Art.Cooldown, 0);
            }
            Assert.IsNull(WeaponKind.Fists.Art);
        }

        [Test]
        public void ABadArtRowIsReported()
        {
            var errors = new List<string>();
            WeaponKind.Parse("id,name,branch,attack,speed,reach,arc,art,art_name,art_power,art_cooldown\nsword,검,melee,swing,1,1,120,parry,막기,250,6\n", errors);
            Assert.AreEqual(1, errors.Count, "a guard needs its reach, arc and time");
        }

        [Test]
        public void EachKindKeepsItsOwnCooldownThroughASwap()
        {
            var (world, player) = Holding("iron_sword", "iron_dagger");
            Tough(world, player, 1);
            Assert.IsTrue(world.UseWeaponArt(player.Id, Right));
            Assert.IsFalse(world.UseWeaponArt(player.Id, Right), "cooling down");
            Run(world, World.TicksPerSecond);
            world.SwapWeapons(player.Id);
            Assert.IsTrue(world.UseWeaponArt(player.Id, Right), "the dagger's own is ready");
            world.SwapWeapons(player.Id);
            Assert.Greater(world.ArtTicksLeft(player, player.Stats.Kind), 0, "the sword's is still cooling");
            Assert.IsFalse(world.UseWeaponArt(player.Id, Right));
            Run(world, World.TicksPerSecond * 6);
            Assert.IsTrue(world.UseWeaponArt(player.Id, Right), "ready again after its six seconds");
        }

        [Test]
        public void APickaxeHasNoArt()
        {
            var (world, player) = Holding("copper_pickaxe");
            Assert.IsFalse(world.UseWeaponArt(player.Id, Right));
        }

        [Test]
        public void ASwordsGuardTurnsABlowAsideAndAnswersIt()
        {
            var (world, player) = Holding("iron_sword");
            var slime = Tough(world, player, 1);
            world.UseWeaponArt(player.Id, Right);
            Run(world, 2);
            float health = player.Health;
            Assert.AreEqual(0, world.Hurt(player, 30));
            Assert.AreEqual(health, player.Health);
            var events = world.TakeEvents();
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Art && World.ArtPhaseOf(e.Amount) == ArtPhase.Blocked));
            Assert.Greater(Lost(slime), 0, "answered");
            Assert.IsNull(player.Art, "one blow only");
            Assert.Greater(world.Hurt(player, 30), 0, "the next goes through");
        }

        [Test]
        public void ASwordsGuardThatTakesNothingEnds()
        {
            var (world, player) = Holding("iron_sword");
            world.UseWeaponArt(player.Id, Right);
            var events = Run(world, World.TicksPerSecond / 2 + 1);
            Assert.IsNull(player.Art);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Art && World.ArtPhaseOf(e.Amount) == ArtPhase.End));
            Assert.Greater(world.Hurt(player, 30), 0);
        }

        [Test]
        public void ADaggerStabsTheNearestAheadForASureCrit()
        {
            var (world, player) = Holding("iron_dagger");
            Assert.IsFalse(world.UseWeaponArt(player.Id, Right), "nobody to stab: nothing spent");
            Assert.AreEqual(0, world.ArtTicksLeft(player, player.Stats.Kind));
            var near = Tough(world, player, 1);
            var behind = Tough(world, player, -1);
            Assert.IsTrue(world.UseWeaponArt(player.Id, Right));
            var hit = world.TakeEvents().Single(e => e.Type == SimEventType.Hit);
            Assert.AreEqual(near.Id, hit.Target);
            Assert.IsTrue(hit.Critical);
            Assert.AreEqual(World.ArtCause, hit.Cause);
            Assert.AreEqual(0, Lost(behind));
        }

        static int Cleave(int heldTicks)
        {
            var (world, player) = Holding("iron_greatsword");
            var slime = Tough(world, player, 1.2f);
            world.UseWeaponArt(player.Id, Right);
            Run(world, heldTicks);
            world.ReleaseWeaponArt(player.Id, Right);
            Run(world, 1);
            return Lost(slime);
        }

        [Test]
        public void AGreatswordCleavesHarderTheLongerItIsHeld()
        {
            int quick = Cleave(1), full = Cleave(World.TicksPerSecond);
            Assert.Greater(quick, 0);
            Assert.Greater(full, quick * 2.5f, "350% against about 100%");
        }

        [Test]
        public void AGreatswordLetsGoByItselfWhenFull()
        {
            var (world, player) = Holding("iron_greatsword");
            var slime = Tough(world, player, 1.2f);
            world.UseWeaponArt(player.Id, Right);
            Run(world, World.TicksPerSecond + 1);
            Assert.IsNull(player.Art);
            Assert.Greater(Lost(slime), 0);
        }

        [Test]
        public void ASpearRushesThreeTilesAndStrikesAllOnTheWay()
        {
            var (world, player) = Holding("iron_spear");
            var start = player.Position;
            var first = Tough(world, player, 1);
            var second = Tough(world, player, 2.5f);
            var aside = Tough(world, player, 1.5f, 3);
            world.UseWeaponArt(player.Id, Right);
            Assert.AreEqual(3, player.Position.X - start.X, 0.3f);
            Assert.Greater(Lost(first), 0);
            Assert.Greater(Lost(second), 0);
            Assert.AreEqual(0, Lost(aside));
        }

        [Test]
        public void AHammerIsLiftedThenStrikesAllRoundAndDazes()
        {
            var (world, player) = Holding("iron_hammer");
            var ahead = Tough(world, player, 1);
            var behind = Tough(world, player, -1.2f);
            var far = Tough(world, player, 0, 4);
            // One that has seen the player (an idle one is not dazed, as with the hammer's finisher).
            ahead.State = EnemyState.Chase;
            world.UseWeaponArt(player.Id, Right);
            world.SetAttacking(player.Id, true);
            var lifted = Run(world, 3);
            Assert.AreEqual(0, Lost(ahead), "not yet");
            Assert.IsFalse(lifted.Any(e => e.Type == SimEventType.Attack && e.Source == player.Id), "no basic attack while lifted");
            Run(world, World.TicksPerSecond / 2);
            Assert.Greater(Lost(ahead), 0);
            Assert.Greater(Lost(behind), 0);
            Assert.AreEqual(0, Lost(far));
            Assert.AreEqual(EnemyState.Stagger, ahead.State);
        }

        [Test]
        public void ABowJumpsBackAndLoosesThreeArrows()
        {
            var (world, player) = Holding("iron_bow");
            var start = player.Position;
            world.UseWeaponArt(player.Id, Right);
            Assert.AreEqual(-2, player.Position.X - start.X, 0.3f);
            Assert.AreEqual(3, world.Projectiles.Count);
            Assert.IsTrue(world.Projectiles.All(p => p.Velocity.X > 0 && p.Cause == World.ArtCause - 1));
        }

        [Test]
        public void ACrossbowAimsThenPiercesEverythingOnTheLine()
        {
            var (world, player) = Holding("iron_crossbow");
            var slimes = new[] { 2f, 5f, 9f }.Select(x => Tough(world, player, x)).ToList();
            var aside = Tough(world, player, 5, 2);
            world.UseWeaponArt(player.Id, Right);
            Run(world, 3);
            Assert.IsTrue(slimes.All(s => Lost(s) == 0), "aiming");
            Run(world, World.TicksPerSecond / 2);
            Assert.IsTrue(slimes.All(s => Lost(s) > 0));
            Assert.AreEqual(0, Lost(aside));
        }

        [Test]
        public void ABoomerangWhirlsRoundStrikingThreeTimes()
        {
            var (world, player) = Holding("iron_boomerang");
            var slime = Tough(world, player, 0, -2);
            world.UseWeaponArt(player.Id, Right);
            var events = Run(world, World.TicksPerSecond * 2);
            Assert.AreEqual(3, events.Count(e => e.Type == SimEventType.Hit && e.Target == slime.Id && e.Cause == World.ArtCause));
        }

        [Test]
        public void AStaffsWavePushesHardAhead()
        {
            var (world, player) = Holding("iron_staff");
            var slime = Tough(world, player, 1.5f);
            var behind = Tough(world, player, -1.5f);
            world.UseWeaponArt(player.Id, Right);
            Assert.Greater(Lost(slime), 0);
            Assert.AreEqual(0, Lost(behind));
            float before = slime.Position.X;
            Run(world, 10);
            Assert.Greater(slime.Position.X - before, 1f, "pushed well away");
        }

        [Test]
        public void AWandLoosesEightBoltsOverItsTime()
        {
            var (world, player) = Holding("iron_wand");
            world.UseWeaponArt(player.Id, Right);
            int most = 0, made = 0;
            var seen = new HashSet<int>();
            for (int i = 0; i < World.TicksPerSecond; i++)
            {
                Run(world, 1);
                foreach (var p in world.Projectiles) if (seen.Add(p.Id)) made++;
                most = System.Math.Max(most, world.Projectiles.Count);
            }
            Assert.AreEqual(8, made);
            Assert.IsNull(player.Art);
        }

        [Test]
        public void AnOrbsWellDrawsMonstersInThenBursts()
        {
            var (world, player) = Holding("iron_orb");
            var slime = Tough(world, player, 3.5f, 2.2f);
            var centre = player.Position + new Vec2(3.5f, 0);
            float before = Vec2.Distance(slime.Position, centre);
            world.UseWeaponArt(player.Id, Right);
            Run(world, World.TicksPerSecond);
            Assert.Less(Vec2.Distance(slime.Position, centre), before - 0.5f, "drawn in");
            Assert.AreEqual(0, Lost(slime));
            Run(world, World.TicksPerSecond);
            Assert.Greater(Lost(slime), 0, "burst");
        }

        [Test]
        public void ASwapCutsOffABusyArt()
        {
            var (world, player) = Holding("iron_hammer", "iron_sword");
            var slime = Tough(world, player, 1);
            world.UseWeaponArt(player.Id, Right);
            Run(world, 2);
            world.SwapWeapons(player.Id);
            Assert.IsNull(player.Art);
            Run(world, World.TicksPerSecond);
            Assert.AreEqual(0, Lost(slime));
        }

        [Test]
        public void TheSnapshotCarriesTheArtsCooldowns()
        {
            var self = new SelfState(0, new Vec2(10, 10), 100, 100, 5, new List<int>(), new List<int>(), 0, -1, 0, 0, 0, -1, -1, 0, 0, -1, 0, 0, 120, 45, 12);
            var back = SnapshotMessage.Decode(new SnapshotMessage(1, self, new List<EntityState>(), new List<ProjectileState>(), new List<HazardState>(), new List<SimEvent>()).Encode());
            Assert.AreEqual(120, back.Self.ArtCooldown);
            Assert.AreEqual(45, back.Self.SpareArtCooldown);
            Assert.AreEqual(12, back.Self.ArtAge);
        }

        [Test]
        public void TheHostTakesTheArtFromTheSkillCommand()
        {
            var press = SkillCommandMessage.Decode(new SkillCommandMessage(SkillCommandMessage.ArtPress, Right).Encode());
            Assert.AreEqual(SkillCommandMessage.ArtPress, press.Slot);
        }
    }
}
