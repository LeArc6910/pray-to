using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The rest of the ordinary monsters on the attack sheet (2026-11-22, Docs/WorldRework.md 2b-2,
    // Docs/MonsterPatterns.md): the pieces the engine gained for them (lying in wait, living under the ground,
    // circling, turning slowly, an aura, a blink to behind, one attack straight into the next, poison, a deathburst
    // that fire quenches) and each kind doing what its row says.
    public class MonsterKindTests
    {
        static readonly MonsterData Shipped = Monsters.Shipped;

        static World Room(int size = 30) =>
            new World(Maps.Room(size), TileMap.Center(size / 2, size / 2), Shipped, 1, new WorldConfig { EnemyTarget = 0 });

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

        static int Taken(IEnumerable<SimEvent> events, Player player) => events.Where(e => e.Type == SimEventType.Hit && e.Target == player.Id).Sum(e => e.Amount);

        static Enemy Spawn(World world, string id, Vec2 at) => world.SpawnEnemy(Shipped.Find(id), at);

        [Test]
        public void OneThatLiesInWaitComesUpOnlyUnderWhoeverWalksOverIt()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var scorpion = Spawn(world, "sand_scorpion", player.Position + new Vec2(6, 0));

            Run(world, 2);
            Assert.AreEqual(EnemyState.Idle, scorpion.State, "inside its aggro, but it waits");
            Assert.IsTrue(scorpion.Buried);
            Assert.IsTrue(scorpion.Away);
            Assert.AreEqual(World.EnemyBuriedTell, (World.EnemyStateByte(scorpion) >> World.EnemyTellShift) & 7, "the screen is told it is a mound");
            float before = scorpion.Health;
            world.Hurt(scorpion, player, 50);
            Assert.AreEqual(before, scorpion.Health, "under the ground it cannot be hit");

            Moves.Walk(world, player, scorpion.Position + new Vec2(-3, 0));
            Run(world, 0.1f);
            Assert.AreEqual("ambush", scorpion.Attack?.Id);
            var mark = world.Hazards.Single(h => h.Source == scorpion.Id);
            Assert.AreEqual(0, Vec2.Distance(mark.Position, player.Position), 0.05f, "it comes up under them");

            var events = Run(world, 0.7f);
            Assert.Greater(Taken(events, player), 0, "standing still on the mark is hit");
            Assert.IsFalse(scorpion.Buried);
            Assert.Less(Vec2.Distance(scorpion.Position, player.Position), 0.5f);
            Run(world, 3);
            Assert.IsFalse(scorpion.Buried, "it has made its ambush: it fights above ground from now on");
            world.Hurt(scorpion, player, 50);
            Assert.Less(scorpion.Health, before);
        }

        [Test]
        public void AScorpionsPincerHoldsAndItsStingFollowsWithPoison()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var scorpion = Spawn(world, "sand_scorpion", player.Position + new Vec2(1.2f, 0));
            scorpion.State = EnemyState.Chase;
            scorpion.TargetId = player.Id;
            Assert.IsTrue(world.ForceAttack(scorpion, "pincer"));
            var events = Run(world, 0.55f);
            Assert.IsTrue(player.Afflictions.Rooted, "caught");
            Assert.AreEqual("sting", scorpion.Attack.Id, "the sting follows the pincer with no rest between");
            Assert.AreEqual(EnemyState.Windup, scorpion.State);

            events = Run(world, 0.6f);
            int sting = Taken(events, player);
            Assert.Greater(sting, 0, "held, they cannot walk out of the sting");
            Assert.IsTrue(player.Afflictions.Poisoned);
            Assert.AreNotEqual(0, player.Afflictions.Bits & StatusBits.Poisoned);

            // Six bites over three seconds, together about half the sting again.
            scorpion.Health = 0;
            events = Run(world, 3.2f);
            var bites = events.Where(e => e.Type == SimEventType.Hit && e.Target == player.Id).ToList();
            Assert.AreEqual(6, bites.Count);
            Assert.IsFalse(player.Afflictions.Poisoned);
        }

        [Test]
        public void OneThatLivesUnderTheGroundCanOnlyBeHitAfterItStrikes()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var worm = Spawn(world, "sand_worm", player.Position + new Vec2(6, 0));
            float full = worm.Health;

            // Under the ground until it attacks.
            bool attacked = false;
            for (int i = 0; i < World.Ticks(3) && !attacked; i++)
            {
                world.Step();
                attacked = worm.State == EnemyState.Windup;
                if (!attacked)
                {
                    Assert.IsTrue(worm.Buried);
                    world.Hurt(worm, player, 10);
                    Assert.AreEqual(full, worm.Health);
                }
            }
            Assert.IsTrue(attacked);

            // After the blow it stays up for the time its trait gives, and can be hit then.
            for (int i = 0; i < World.Ticks(1.2f) && worm.State != EnemyState.Recover; i++) world.Step();
            Assert.AreEqual(EnemyState.Recover, worm.State);
            Assert.GreaterOrEqual(worm.StateTicks, World.Ticks(2.5f) - 1);
            Assert.IsFalse(worm.Away);
            world.Hurt(worm, player, 10);
            Assert.Less(worm.Health, full);

            player.Health = player.Stats.MaxHealth;
            world.TakeEvents();
            var events = Run(world, 2.6f, () => player.Health = player.Stats.MaxHealth);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxDive && e.Source == worm.Id), "then it goes back under");
        }

        [Test]
        public void BatsCircleAndNoMoreThanTwoSwoopAtOnce()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var bats = Enumerable.Range(0, 5).Select(i => Spawn(world, "crystal_bat", player.Position + Vec2.FromAngle(i * 1.2f) * 4)).ToList();

            int most = 0, swoops = 0;
            float walked = 0;
            var last = bats.ToDictionary(b => b, b => b.Position);
            Run(world, 6, () =>
            {
                player.Health = player.Stats.MaxHealth;
                most = Math.Max(most, bats.Count(b => b.Attack?.Shape == MonsterAttackShape.Swoop && (b.State == EnemyState.Windup || b.State == EnemyState.Charge)));
                foreach (var bat in bats)
                {
                    if (bat.State == EnemyState.Chase) walked += Vec2.Distance(last[bat], bat.Position);
                    last[bat] = bat.Position;
                }
            });
            swoops = most;
            Assert.AreEqual(World.MaxSwoopsPerTarget, swoops, "two at a time, the rest wait their turn");
            // Five bats for six seconds at 3.6 tiles a second, less the time they swoop and rest: they never stand.
            Assert.Greater(walked, 30, "between swoops they keep circling");
            Assert.IsTrue(bats.All(b => Vec2.Distance(b.Position, player.Position) < 8));
        }

        [Test]
        public void ASwoopLeavesItsPoolAlongItsPath()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var bat = Spawn(world, "fire_bat", player.Position + new Vec2(3, 0));
            bat.State = EnemyState.Chase;
            bat.TargetId = player.Id;
            Assert.IsTrue(world.ForceAttack(bat, "swoop"));
            Moves.Walk(world, player, player.Position + new Vec2(0, 2.5f));
            Run(world, 0.95f);
            var fires = world.Hazards.Where(h => h.Shape == HazardShape.Fire).ToList();
            Assert.GreaterOrEqual(fires.Count, 5, "fire along the five tiles it flew");
            Assert.Greater(fires.Max(f => f.Position.X) - fires.Min(f => f.Position.X), 3);
            Run(world, 2.2f, () => bat.State = EnemyState.Recover);
            Assert.IsFalse(world.Hazards.Any(h => h.Shape == HazardShape.Fire), "gone after its two seconds");
        }

        [Test]
        public void AFireBatNearDeathFliesInAndBurstsUnlessItIsFelledFirst()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var bat = Spawn(world, "fire_bat", player.Position + new Vec2(3, 0));
            bat.Health = bat.MaxHealth * 0.2f;
            Run(world, 0.2f);
            Assert.AreEqual("dive_bomb", bat.Attack?.Id);
            var events = Run(world, 1f);
            Assert.IsTrue(bat.IsDead || !world.Enemies.Contains(bat), "it goes with its blast");
            Assert.Greater(Taken(events, player), 0);

            // Felled while it flashes, there is no blast.
            player.Health = player.Stats.MaxHealth;
            var second = Spawn(world, "fire_bat", player.Position + new Vec2(3, 0));
            second.Health = second.MaxHealth * 0.2f;
            Run(world, 0.2f);
            Assert.AreEqual("dive_bomb", second.Attack?.Id);
            world.Hurt(second, player, 1000);
            world.TakeEvents();
            events = Run(world, 1.2f);
            Assert.AreEqual(0, events.Count(e => e.Type == SimEventType.Hit && e.Target == player.Id && e.Source == second.Id));
        }

        [Test]
        public void OneThatTurnsSlowlyOnlyStrikesWhatIsInFrontOfIt()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var crab = Spawn(world, "lake_crab", player.Position + new Vec2(1.2f, 0));
            crab.State = EnemyState.Chase;
            crab.TargetId = player.Id;
            // Facing straight away from the player.
            crab.Facing = new Vec2(1, 0);

            world.Step();
            Assert.AreEqual(EnemyState.Chase, crab.State, "its back is to them: it turns first");
            float turned = MathF.Acos(Math.Clamp(Vec2.Dot(crab.Facing, new Vec2(1, 0)), -1, 1)) * 180 / MathF.PI;
            Assert.AreEqual(120 * World.TickSeconds, turned, 0.5f, "120 degrees a second");

            // A blow on its back lands whole while it turns; once it has come round it guards and strikes.
            crab.MaxHealth = crab.Health = 10000;
            world.Hurt(crab, player, 100);
            float back = 10000 - crab.Health;
            int ticks = 0;
            while (crab.State == EnemyState.Chase && ticks++ < 90) world.Step();
            Assert.AreEqual("claws", crab.Attack?.Id);
            Assert.Greater(ticks, World.Ticks(1.0f), "half a turn takes it over a second");
            float before = crab.Health;
            world.Hurt(crab, player, 100);
            // Its claws turn half (guard:140:50).
            Assert.AreEqual(back * 0.5f, before - crab.Health, 1.5f);
            Assert.AreEqual(2, world.Hazards.Count(h => h.Source == crab.Id && h.Shape == HazardShape.Cone), "both claws are marked");
        }

        [Test]
        public void SoldiersSideBySideTakeLessAndASpearReachesFar()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var one = Spawn(world, "demon_soldier", player.Position + new Vec2(3, 0));
            one.MaxHealth = one.Health = 10000;
            one.Facing = new Vec2(1, 0);
            world.MonstersHold = true;

            world.Hurt(one, player, 100);
            float alone = 10000 - one.Health;
            Assert.IsNull(world.AuraMate(one));

            var two = Spawn(world, "demon_soldier", one.Position + new Vec2(0, 1.2f));
            Assert.AreSame(two, world.AuraMate(one));
            float before = one.Health;
            world.Hurt(one, player, 100);
            Assert.AreEqual(alone * 0.8f, before - one.Health, 1.5f, "shoulder to shoulder, a fifth less");

            // Its shield turns half of a blow on its front, on top of the line's share.
            one.Facing = new Vec2(-1, 0);
            before = one.Health;
            world.TakeEvents();
            world.Hurt(one, player, 100);
            Assert.AreEqual(alone * 0.8f * 0.5f, before - one.Health, 1.5f);
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.Guarded));

            // Apart again, the line is broken.
            two.Position = one.Position + new Vec2(0, 3);
            Assert.IsNull(world.AuraMate(one));

            world.MonstersHold = false;
            two.Health = 0;
            one.State = EnemyState.Chase;
            one.TargetId = player.Id;
            Assert.IsTrue(world.ForceAttack(one, "spear"));
            var line = world.Hazards.Single(h => h.Source == one.Id);
            Assert.AreEqual(HazardShape.Line, line.Shape);
            Assert.AreEqual(2.8f, line.Size, 1e-4f);
        }

        [Test]
        public void AStalkerStepsOutBehindItsTargetAndCutsAtOnce()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            player.Facing = new Vec2(-1, 0);
            var stalker = Spawn(world, "abyss_stalker", player.Position + new Vec2(-5, 0));
            stalker.State = EnemyState.Chase;
            stalker.TargetId = player.Id;
            Assert.IsTrue(world.ForceAttack(stalker, "shadow_step"));
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxVanish));
            Assert.IsTrue(stalker.Away, "gone while its mark shows");
            float full = stalker.Health;
            world.Hurt(stalker, player, 100);
            Assert.AreEqual(full, stalker.Health);
            var mark = world.Hazards.Single(h => h.Source == stalker.Id);
            Assert.AreEqual(0, mark.Damage, "the mark of where it comes out harms nothing");
            Assert.Greater(mark.Position.X, player.Position.X + 1, "behind them: they look the other way");

            Run(world, 0.65f);
            Assert.Greater(stalker.Position.X, player.Position.X + 1);
            Assert.AreEqual("cleave", stalker.Attack.Id, "straight into the cut");
            Assert.AreEqual(EnemyState.Windup, stalker.State);
            Assert.IsFalse(stalker.Away);
            Assert.Less(Vec2.Dot(stalker.Facing, new Vec2(1, 0)), -0.9f, "facing them");

            var events = Run(world, 0.8f);
            Assert.Greater(Taken(events, player), 0, "one who stands is cut");

            // Walking out of the wedge in its telegraph avoids it.
            player.Health = player.Stats.MaxHealth;
            Run(world, 1.2f, () => player.Health = player.Stats.MaxHealth);
            Assert.IsTrue(world.ForceAttack(stalker, "cleave"));
            world.TakeEvents();
            var away = (player.Position - stalker.Position).Normalized;
            int waited = World.Ticks(MonsterAttackSheet.NoticeSeconds);
            events = Run(world, 0.8f, () =>
            {
                if (waited-- > 0) return;
                world.MovePlayer(player.Id, player.Position + away * (MonsterAttackSheet.WalkSpeed * World.TickSeconds), player.Facing, 10);
            });
            Assert.AreEqual(0, Taken(events, player), "seen a quarter second late and walked straight out");
        }

        [Test]
        public void AStalkerChargesThreeTimes()
        {
            var world = Room(40);
            var player = world.AddPlayer("a", null);
            var stalker = Spawn(world, "abyss_stalker", player.Position + new Vec2(-4, 0));
            stalker.State = EnemyState.Chase;
            stalker.TargetId = player.Id;
            Assert.IsTrue(world.ForceAttack(stalker, "triple"));
            int rushes = 0;
            var state = stalker.State;
            Run(world, 4, () =>
            {
                player.Health = player.Stats.MaxHealth;
                if (stalker.State == EnemyState.Charge && state != EnemyState.Charge) rushes++;
                state = stalker.State;
                if (stalker.State == EnemyState.Recover) stalker.StateTicks = 1000;
            });
            Assert.AreEqual(3, rushes);
        }

        [Test]
        public void AnIceSlimeLeavesIceWhereItLandsAndShardsUnlessFireFelledIt()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var slime = Spawn(world, "ice_slime", player.Position + new Vec2(3, 0));
            Run(world, 0.2f);
            Assert.AreEqual("pounce", slime.Attack?.Id);
            Run(world, 0.7f);
            var ice = world.Hazards.Single(h => h.Shape == HazardShape.Ice);
            Assert.AreEqual(1.1f, ice.Size, 1e-4f);
            Assert.IsTrue(player.Afflictions.Chilled, "standing on the ice, slowed");

            // Felled by steel: six shards that chill, though the slime that threw them is gone.
            world.Hurt(slime, player, 10000);
            player.Position = slime.Position + new Vec2(5, 0);
            player.Afflictions.Clear();
            var events = Run(world, World.DeathburstSeconds + 0.1f);
            Assert.AreEqual(6, world.Projectiles.Count);
            player.Position = world.Projectiles.First().Position + world.Projectiles.First().Velocity.Normalized * 0.5f;
            Run(world, 0.3f);
            Assert.IsTrue(player.Afflictions.Chilled, "a shard chills");

            // Felled by fire: it melts, and nothing flies.
            Run(world, 5);
            var second = Spawn(world, "ice_slime", player.Position + new Vec2(3, 0));
            world.MonstersHold = true;
            world.TakeEvents();
            world.Hurt(second, player, 10000, SkillTag.Fire);
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxQuench));
            Run(world, World.DeathburstSeconds + 0.2f);
            Assert.IsEmpty(world.Projectiles);
        }

        [Test]
        public void AMagmaSlimeLeavesFireBehindItAndWhereItErupts()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var slime = Spawn(world, "magma_slime", player.Position + new Vec2(7, 0));
            Run(world, 1.2f, () => player.Health = player.Stats.MaxHealth);
            Assert.IsTrue(world.Hazards.Any(h => h.Shape == HazardShape.Fire && h.Size < 0.5f), "a trail where it walked");
            Assert.IsTrue(world.ForceAttack(slime, "eruption"));
            Run(world, 0.85f, () => player.Health = player.Stats.MaxHealth);
            Assert.IsTrue(world.Hazards.Any(h => h.Shape == HazardShape.Fire && MathF.Abs(h.Size - 0.9f) < 1e-3f), "and a pool where it came down, narrow enough to strike it from outside");
        }

        [Test]
        public void ARootedMushroomThrowsFromAfarAndFansUpClose()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var mushroom = Spawn(world, "spore_shooter", player.Position + new Vec2(6, 0));
            var stood = mushroom.Position;
            Run(world, 0.2f);
            Assert.AreEqual("spore_sac", mushroom.Attack?.Id);
            Run(world, 0.8f);
            Assert.IsTrue(world.Hazards.Any(h => h.Shape == HazardShape.Spore), "a cloud where the sac fell");

            Moves.Walk(world, player, mushroom.Position + new Vec2(-2, 0));
            int shots = 0;
            Run(world, 4, () =>
            {
                player.Health = player.Stats.MaxHealth;
                shots = Math.Max(shots, world.Projectiles.Count(p => !p.Friendly));
            });
            Assert.AreEqual(3, shots, "three thorns in a fan");
            Assert.AreEqual(0, Vec2.Distance(stood, mushroom.Position), 1e-3f, "it never moves, not even away from someone close");
        }

        [Test]
        public void ACrystalWispsBeamIsALineAndItsLightComesOffWalls()
        {
            var world = Room(16);
            var player = world.AddPlayer("a", null);
            var wisp = Spawn(world, "crystal_wisp", player.Position + new Vec2(5, 0));
            wisp.State = EnemyState.Chase;
            wisp.TargetId = player.Id;
            Assert.IsTrue(world.ForceAttack(wisp, "ray"));
            var line = world.Hazards.Single(h => h.Source == wisp.Id);
            Assert.AreEqual(HazardShape.Line, line.Shape);
            Assert.AreEqual(7, line.Size, 1e-4f);
            var events = Run(world, 1f);
            Assert.Greater(Taken(events, player), 0);

            player.Health = player.Stats.MaxHealth;
            Run(world, 2, () => { player.Health = player.Stats.MaxHealth; wisp.State = EnemyState.Recover; wisp.StateTicks = 10; });
            wisp.State = EnemyState.Chase;
            Assert.IsTrue(world.ForceAttack(wisp, "prism"));
            // Out of its way once it has left, so it flies on to the walls.
            bool stepped = false;
            events = Run(world, 5, () =>
            {
                if (wisp.State == EnemyState.Recover) wisp.StateTicks = 100;
                if (stepped || !world.Projectiles.Any()) return;
                stepped = true;
                player.Position += new Vec2(0, 3);
            });
            Assert.AreEqual(2, events.Count(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxBounce), "two bounces, then it breaks on the third wall");
        }

        [Test]
        public void AMyconidSweepsWideAndFillsItsFellowsWhenItFalls()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var myconid = Spawn(world, "myconid", player.Position + new Vec2(1.5f, 0));
            Run(world, 0.2f);
            Assert.AreEqual("spore_smash", myconid.Attack?.Id);
            var cone = world.Hazards.Single(h => h.Source == myconid.Id);
            Assert.AreEqual(HazardShape.Cone, cone.Shape);
            Assert.AreEqual(90, cone.Width, 1e-4f);
            world.Hurt(myconid, player, 10000);
            Assert.IsTrue(world.Hazards.Any(h => h.Shape == HazardShape.Balm));
        }

        [Test]
        public void ATideWispsWavesShoveAndItMendsItsFellows()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var wisp = Spawn(world, "tide_wisp", player.Position + new Vec2(5, 0));
            wisp.State = EnemyState.Chase;
            wisp.TargetId = player.Id;
            Assert.IsTrue(world.ForceAttack(wisp, "wave"));
            Assert.AreEqual(4, world.Hazards.Count(h => h.Source == wisp.Id), "four circles walking out toward them");
            var at = player.Position;
            var events = Run(world, 1.6f);
            Assert.Greater(Taken(events, player), 0);
            Assert.Greater(Vec2.Distance(at, player.Position), 1.5f, "shoved");
            Assert.IsTrue(player.Afflictions.Chilled, "and slowed: its water is cold");

            var crab = Spawn(world, "lake_crab", wisp.Position + new Vec2(2, 2));
            crab.Health = crab.MaxHealth * 0.5f;
            world.MonstersHold = true;
            wisp.State = EnemyState.Chase;
            Assert.IsTrue(world.ForceAttack(wisp, "mend"));
            Run(world, 2.5f, () => player.Health = player.Stats.MaxHealth);
            Assert.Greater(crab.Health, crab.MaxHealth * 0.6f);
        }

        [Test]
        public void ACasterLaysAWallOfFireAndTraps()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var caster = Spawn(world, "demon_caster", player.Position + new Vec2(4, 0));
            caster.State = EnemyState.Chase;
            caster.TargetId = player.Id;
            Assert.IsTrue(world.ForceAttack(caster, "fire_wall"));
            Run(world, 0.85f);
            var fires = world.Hazards.Where(h => h.Shape == HazardShape.Fire).ToList();
            Assert.GreaterOrEqual(fires.Count, 5, "fire all along the line");

            Run(world, 3.2f, () => { player.Health = player.Stats.MaxHealth; if (caster.State == EnemyState.Recover) caster.StateTicks = 100; });
            caster.State = EnemyState.Chase;
            Assert.IsTrue(world.ForceAttack(caster, "rune_trap"));
            Assert.AreEqual(2, world.Hazards.Count(h => h.Shape == HazardShape.Mine));
        }

        [Test]
        public void CirclesThrownOneAfterAnotherCountEachTowardTheCapOnMarks()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            for (int i = 0; i < 4; i++) Spawn(world, "frost_wisp", player.Position + Vec2.FromAngle(i * 1.5f) * 6);
            int most = 0;
            Run(world, 8, () =>
            {
                player.Health = player.Stats.MaxHealth;
                most = Math.Max(most, world.Hazards.Count(h => h.Shape == HazardShape.Circle && h.Damage > 0 && !h.Attached));
            });
            Assert.AreEqual(World.MaxMarksPerTarget, most, "four wisps would throw eight icicles at once; four fall at a time");
        }

        [Test]
        public void ABlowsPoolCanBeNarrowerThanTheBlow()
        {
            var data = MonsterSheet.Parse(
                "id,name,level,health,damage,armor,speed,radius,aggro,reach,windup,cooldown,weight,behavior,shot_speed,keep_distance,role,traits,pack\n" +
                "a,가,1,100,10,0,2,0.4,0,6,0.5,1,1,melee,,,bruiser,turn:120|aura:guard:1.5:20|guard:100:50,\n",
                "monster,id,name,shape,target,telegraph,damage,radius,length,angle,width,count,spread,speed,cooldown,weight,range_min,range_max,pool,when,warn,effect,duration,summon,then\n" +
                "a,slam,찍기,slam,self,0.6,100,1.6,,,,,,,1,100,0,20,fire:4:0.9,,,venom:3,,,jab\n" +
                "a,jab,찌르기,thrust,nearest,0.5,100,,2,,0.6,,,,1,100,0,20,,,,chill:2,,,\n");
            Assert.IsTrue(data.Success, string.Join("\n", data.Errors));
            var slam = data.Find("a").Attacks[0];
            Assert.AreEqual(0.9f, slam.PoolRadius, 1e-5f);
            Assert.AreEqual(HitEffect.Venom, slam.Effect);
            Assert.AreSame(data.Find("a").Attacks[1], slam.Then);

            var world = new World(Maps.Room(20), TileMap.Center(10, 10), data, 1, new WorldConfig { EnemyTarget = 0 });
            var player = world.AddPlayer("a", null);
            var monster = world.SpawnEnemy(data.Find("a"), player.Position + new Vec2(1, 0));
            Assert.IsTrue(world.ForceAttack(monster, "slam"));
            Run(world, 0.65f);
            Assert.AreEqual(0.9f, world.Hazards.Single(h => h.Shape == HazardShape.Fire).Size, 1e-5f);
            Assert.AreEqual("jab", monster.Attack.Id);

            // What the sheet refuses.
            var bad = MonsterSheet.Parse(
                "id,name,level,health,damage,armor,speed,radius,aggro,reach,windup,cooldown,weight,behavior,shot_speed,keep_distance,role,traits,pack\n" +
                "a,가,1,100,10,0,2,0.4,0,6,0.5,1,1,melee,,,bruiser,aura:haste:1:20,\n" +
                "b,나,1,100,10,0,2,0.4,0,6,0.5,1,1,melee,,,bruiser,turn:5,\n" +
                "c,다,1,100,10,0,2,0.4,0,6,0.5,1,1,melee,,,bruiser,,\n",
                "monster,id,name,shape,target,telegraph,damage,radius,length,angle,width,count,spread,speed,cooldown,weight,range_min,range_max,pool,when,warn,effect,duration,summon,then\n" +
                "c,slam,찍기,slam,self,0.6,100,1.6,,,,,,,1,100,0,20,,,,,,,nothing\n");
            string all = string.Join("\n", bad.Errors);
            Assert.IsTrue(bad.Errors.Any(e => e.Contains("aura")), all);
            Assert.IsTrue(bad.Errors.Any(e => e.Contains("turn")), all);
            Assert.IsTrue(bad.Errors.Any(e => e.Contains("then")), all);
        }

        // ---- The bot (BotPilot) among monsters' marks ----

        [Test]
        public void TheBotStepsOutOfAMarkAndNotBackIntoIt()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var slime = Spawn(world, "slime", player.Position + new Vec2(3.2f, 0));
            world.MonstersHold = true;
            var pilot = new BotPilot(world, player, new BotMemory(world, player));
            Assert.IsTrue(world.ForceAttack(slime, "pounce"));

            var intent = pilot.Next(BotTarget.Monster());
            Assert.AreEqual(BotDo.Move, intent.Do);
            Assert.AreEqual("예고를 피함", intent.Why);
            var mark = world.Hazards.Single();
            player.Position += intent.Direction.Normalized * 1.6f;
            Assert.IsFalse(mark.Contains(player.Position, World.PlayerRadius), "a step and a half that way is out of it");

            // Out of it, with the slime (and so the bot's way) on the far side of the mark: it does not walk back in.
            intent = pilot.Next(BotTarget.Monster());
            var next = player.Position + (intent.Do == BotDo.Move ? intent.Direction.Normalized * 0.5f : Vec2.Zero);
            Assert.IsFalse(mark.Contains(next, World.PlayerRadius), "it waits at the edge");
        }

        [Test]
        public void TheBotLeavesAThrownMarkAlongItsWayToTheThrower()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var wisp = Spawn(world, "frost_wisp", player.Position + new Vec2(6, 0));
            world.MonstersHold = true;
            var pilot = new BotPilot(world, player, new BotMemory(world, player));
            // A path first (toward the wisp), then the icicle on the bot's head.
            Assert.AreEqual(BotDo.Move, pilot.Next(BotTarget.Monster()).Do);
            Assert.IsTrue(world.ForceAttack(wisp, "icicle"));
            var intent = pilot.Next(BotTarget.Monster());
            Assert.AreEqual("예고를 피함", intent.Why);
            Assert.Greater(Vec2.Dot(intent.Direction.Normalized, new Vec2(1, 0)), 0.5f, "out of the circle on the wisp's side, not away from it");
        }

        [Test]
        public void TheBotDoesNotSwingAtAMound()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var worm = Spawn(world, "sand_worm", player.Position + new Vec2(1, 0));
            world.MonstersHold = true;
            world.Step();
            Assert.IsTrue(worm.Buried);
            var pilot = new BotPilot(world, player, new BotMemory(world, player));
            Assert.AreNotEqual(BotDo.Fight, pilot.Next(BotTarget.Monster()).Do, "under the ground it cannot be hit");
            Assert.IsTrue(world.ForceAttack(worm, "sand_spit"));
            player.Position = worm.Position + new Vec2(-1, 0.05f);
            // Up to throw, it can be fought (the bot may first step off the mark it stands in).
            var intent = pilot.Next(BotTarget.Monster());
            Assert.IsTrue(intent.Do == BotDo.Fight || intent.Why == "예고를 피함", intent.Do + " " + intent.Why);
        }

        [Test]
        public void TheBotGoesRoundAGuardsFrontButNotRoundItsBack()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var crab = Spawn(world, "lake_crab", player.Position + new Vec2(1.1f, 0));
            world.MonstersHold = true;
            crab.Facing = new Vec2(-1, 0);
            var pilot = new BotPilot(world, player, new BotMemory(world, player));

            // Right in front of its claws: it strikes while it steps round, along the crab's side and not away from it.
            var intent = pilot.Next(BotTarget.Monster());
            Assert.AreEqual(BotDo.Fight, intent.Do);
            Assert.AreEqual("방패 옆으로", intent.Why);
            Assert.Less(MathF.Abs(Vec2.Dot(intent.Step, new Vec2(1, 0))), 0.5f, "round it, not at it or away");
            Assert.Greater(intent.Direction.X, 0, "and it faces the crab while it goes");

            // Walked round, the bot keeps in reach and ends up behind the slow-turning crab, where its blows land whole.
            for (int i = 0; i < World.Ticks(2); i++)
            {
                var step = BotPilot.Flank(crab, player.Position);
                if (step.LengthSquared == 0) break;
                player.Position += step * (5 * World.TickSeconds);
            }
            Assert.AreEqual(Vec2.Zero, BotPilot.Flank(crab, player.Position), "it stops behind");
            Assert.Greater(player.Position.X, crab.Position.X, "on the crab's back");
            Assert.Less(Vec2.Distance(player.Position, crab.Position), 1.6f, "still in reach");
            crab.State = EnemyState.Recover;
            Assert.AreEqual(Vec2.Zero, BotPilot.Flank(crab, crab.Position + new Vec2(-1, 0)), "its guard is down while it rests");

            // One that faces whoever it chases at once (a beetle) is gone round only while it winds up; one with no
            // guard never.
            var beetle = Spawn(world, "cave_beetle", player.Position + new Vec2(0, 3));
            beetle.Facing = (player.Position - beetle.Position).Normalized;
            beetle.State = EnemyState.Chase;
            Assert.AreEqual(Vec2.Zero, BotPilot.Flank(beetle, player.Position));
            beetle.State = EnemyState.Windup;
            Assert.AreNotEqual(Vec2.Zero, BotPilot.Flank(beetle, player.Position));
            var slime = Spawn(world, "slime", player.Position + new Vec2(0, -3));
            slime.Facing = (player.Position - slime.Position).Normalized;
            slime.State = EnemyState.Windup;
            Assert.AreEqual(Vec2.Zero, BotPilot.Flank(slime, player.Position));
        }

        [Test]
        public void TheBotStrikesTheMenderBeforeTheNearest()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            world.MonstersHold = true;
            var crab = Spawn(world, "lake_crab", player.Position + new Vec2(0.9f, 0));
            var wisp = Spawn(world, "tide_wisp", player.Position + new Vec2(-1.0f, 0));
            Assert.IsTrue(BotPilot.First(wisp));
            Assert.IsFalse(BotPilot.First(crab));
            var pilot = new BotPilot(world, player, new BotMemory(world, player));
            var intent = pilot.Next(BotTarget.Monster());
            Assert.AreEqual(BotDo.Fight, intent.Do);
            Assert.Less(intent.Direction.X, 0, "at the wisp, though the crab is nearer");

            // Seen further off, the way goes to the mender too.
            var far = Room();
            var walker = far.AddPlayer("b", null);
            far.MonstersHold = true;
            Spawn(far, "lake_crab", walker.Position + new Vec2(4, 0));
            Spawn(far, "tide_wisp", walker.Position + new Vec2(-6, 0));
            var going = new BotPilot(far, walker, new BotMemory(far, walker)).Next(BotTarget.Monster());
            Assert.AreEqual(BotDo.Move, going.Do);
            Assert.Less(going.Direction.X, 0, "toward the wisp");
        }

        // Every kind, left alone with a player who stands still, makes each of its attacks sooner or later and none of
        // them throws: the whole sheet runs.
        [Test]
        public void EveryKindUsesItsAttacksAgainstAPlayerWhoStands()
        {
            foreach (var kind in Shipped.Monsters.Where(m => m.Attacks.Count > 0))
            {
                var world = Room(40);
                var player = world.AddPlayer("a", null);
                var used = new HashSet<string>();
                var monsters = new List<Enemy>();
                for (int i = 0; i < 3; i++) monsters.Add(world.SpawnEnemy(kind, player.Position + Vec2.FromAngle(i * 0.5f) * (2.5f + i * 2.25f)));
                // A dormant one (B2) or a mimic (B3b) notices only a player right by it: these are roused, as a struck one
                // would be.
                if (kind.Has(MonsterTraitKind.Dormant) || kind.Has(MonsterTraitKind.Mimic))
                    foreach (var monster in monsters) (monster.TargetId, monster.State) = (player.Id, EnemyState.Chase);
                Run(world, 20, () =>
                {
                    player.Health = player.Stats.MaxHealth;
                    foreach (var monster in monsters)
                    {
                        if (monster.IsDead) continue;
                        // Near death and hurt, so the attacks with conditions come too (a bomber, a mender).
                        if (world.Tick == World.Ticks(12)) monster.Health = monster.MaxHealth * 0.25f;
                        if (monster.Attack != null) used.Add(monster.Attack.Id);
                    }
                });
                foreach (var attack in kind.Attacks)
                    Assert.Contains(attack.Id, used.ToList(), $"{kind.Id} never used {attack.Id}");
            }
        }
    }
}
