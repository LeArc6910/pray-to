using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // Ordinary monsters' attacks from monster_attacks.csv (2026-11-21, Docs/WorldRework.md 2b): the sheet, the walk-out
    // rule, each new piece of the engine, the traits and what goes over the wire.
    public class MonsterAttackTests
    {
        const string MonsterHeader = "id,name,level,health,damage,armor,speed,radius,aggro,reach,windup,cooldown,weight,behavior,shot_speed,keep_distance,role,traits,pack\n";
        const string AttackHeader =
            "monster,id,name,shape,target,telegraph,damage,radius,length,angle,width,count,spread,speed,cooldown,weight,range_min,range_max,pool,when,warn,effect,duration,summon\n";

        // One monster for each piece. They stand still (aggro 0) until an attack is forced.
        static readonly MonsterData Pieces = MonsterSheet.Parse(
            MonsterHeader +
            "bouncer,튕김이,1,100,10,0,2,0.3,0,6,0.5,1,1,melee,,,shooter,,\n" +
            "thrower,던짐이,1,100,10,0,2,0.3,0,6,0.5,1,1,melee,,,shooter,,\n" +
            "blinker,점멸이,1,100,10,0,2,0.3,0,6,0.5,1,1,melee,,,shooter,,\n" +
            "puller,끌이,1,100,10,0,2,0.3,0,6,0.5,1,1,melee,,,shooter,,\n" +
            "miner,덫꾼,1,100,10,0,2,0.3,0,6,0.5,1,1,melee,,,support,,\n" +
            "mender,돌보미,1,100,10,0,2,0.3,0,6,0.5,1,1,melee,,,support,,\n" +
            "chief,대장,1,100,10,0,2,0.3,0,6,0.5,1,1,melee,,,support,,\n" +
            "ram,숫양,1,100,10,0,2,0.4,0,6,0.5,1,1,melee,,,bruiser,guard:120|stagger,\n" +
            "blob,덩이,1,100,10,0,2,0.4,0,6,0.5,1,1,melee,,,swarm,split:2|deathcloud,always\n" +
            "shard,파편이,1,100,10,0,2,0.4,0,6,0.5,1,1,melee,,,swarm,deathburst:4:50,\n" +
            "ember,불씨,1,100,10,0,2,0.4,0,6,0.5,1,1,melee,,,bruiser,trail:fire:3,\n" +
            "fodder,졸개,1,100,10,0,2,0.4,0,0.5,0.5,1,1,melee,,,swarm,,\n",
            AttackHeader +
            "bouncer,shot,튕기는 탄,bounce,nearest,0.3,100,0.3,,,,2,,9,1,100,0,20,,,,,,\n" +
            "thrower,disc,돌아오는 탄,boomerang,nearest,0.3,100,0.3,7,,,,,8,1,100,0,20,,,,,,\n" +
            "blinker,shift,점멸,blink,self,0.4,,,4,,,,,,1,100,0,20,,,,,,\n" +
            "puller,well,우물,pull,nearest,2.0,100,2.5,,,,,,2,1,100,0,20,,,,,,\n" +
            "miner,trap,덫,mine,nearest,1.0,100,1.0,,,,2,2,,1,100,0,20,,,,,6,\n" +
            "mender,mend,치유,mend,ally,0.5,10,,6,,,,,,1,100,0,20,,,,,3,\n" +
            "chief,rally,북돋기,empower,self,0.5,30,5,,,,,,,1,100,0,20,,,,,4,\n" +
            "ram,rush,돌진,charge,nearest,0.5,100,,6,,0.9,,,10,1,100,0,20,,,,,,\n" +
            "blob,slam,내려찍기,slam,self,0.6,100,1.2,,,,,,,1,100,0,20,spore:3,,,,,\n" +
            "shard,jab,찌르기,thrust,nearest,0.5,100,,2.5,,0.6,,,,1,100,0,20,,,,push:2,,\n" +
            "ember,bite,깨물기,strike,nearest,0.5,100,,1.1,80,,,,,1,100,0,20,,,,,,\n");

        static World Room(MonsterData monsters = null, int size = 30, TileMap map = null) =>
            new World(map ?? Maps.Room(size), TileMap.Center(size / 2, size / 2), monsters ?? Pieces, 1, new WorldConfig { EnemyTarget = 0 });

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

        // ---- The sheet ----

        [Test]
        public void TheShippedSheetIsValidAndEveryMarkCanBeWalkedOutOf()
        {
            Assert.IsTrue(Monsters.Shipped.Success, string.Join("\n", Monsters.Shipped.Errors));
            var attacks = Monsters.Shipped.Monsters.SelectMany(m => m.Attacks.Select(a => (Monster: m, Attack: a))).ToList();
            Assert.IsNotEmpty(attacks);
            foreach (var (monster, attack) in attacks)
            {
                // Docs/WorldRework.md 소모전과 회피전: (radius + 0.35) / 5 + 0.25 seconds for a circle of that radius.
                Assert.GreaterOrEqual(attack.Telegraph + 1e-4f, MonsterAttackSheet.LeastTelegraph(attack),
                    $"{monster.Id}.{attack.Id}: a walking player cannot get out of it in {attack.Telegraph}s");
                Assert.IsTrue(attack.Tell == Tell.Area || attack.Tell == Tell.Dash || attack.Tell == Tell.Barrage || attack.Tell == Tell.Call, $"{monster.Id}.{attack.Id}");
            }
            foreach (var monster in Monsters.Shipped.Monsters.Where(m => m.Attacks.Count > 0))
                Assert.LessOrEqual(monster.Attacks.Count, 3, $"{monster.Id}: a kind has two or three patterns, so it is known by sight");
            // Every ordinary kind fights from the sheet (2b-2); only the spider queen's brood keeps the old one blow.
            foreach (var monster in Monsters.Shipped.Monsters.Where(m => m.Id != "silk_spiderling"))
            {
                Assert.IsNotEmpty(monster.Attacks, monster.Id);
                Assert.AreNotEqual(MonsterRole.None, monster.Role, monster.Id);
                Assert.GreaterOrEqual(monster.Attacks.Count + monster.Traits.Count, 2, $"{monster.Id}: more than one thing to do");
            }
            Assert.IsEmpty(Monsters.Shipped.Find("silk_spiderling").Attacks);
            Assert.AreEqual(MonsterRole.Swarm, Monsters.Shipped.Find("slime").Role);
            Assert.IsTrue(Monsters.Shipped.Find("cave_beetle").Has(MonsterTraitKind.Guard));
            Assert.AreEqual("solo", Monsters.Shipped.Find("abyss_stalker").Pack);
        }

        [Test]
        public void TheWalkOutRuleIsTheDocumentsFormula()
        {
            var circle = new MonsterAttack { Shape = MonsterAttackShape.Lob, Radius = 1.2f };
            Assert.AreEqual((1.2f + 0.35f) / 5 + 0.25f, MonsterAttackSheet.LeastTelegraph(circle), 1e-5f);
            var shot = new MonsterAttack { Shape = MonsterAttackShape.Spread, Radius = 0.3f };
            Assert.AreEqual(0, MonsterAttackSheet.LeastTelegraph(shot), "a shot marks nothing to walk out of");
            var well = new MonsterAttack { Shape = MonsterAttackShape.Pull, Radius = 2.5f, Speed = 2 };
            Assert.AreEqual(2.85f / 3 + 0.25f, MonsterAttackSheet.LeastTelegraph(well), 1e-5f, "a pull slows the way out by its drag");
        }

        [Test]
        public void TheSheetNamesWhatIsWrong()
        {
            string monsters = MonsterHeader + "a,가,1,10,1,0,1,0.3,5,1,0.5,1,1,melee,,,sideways,,\n" + "b,나,1,10,1,0,1,0.3,5,1,0.5,1,1,melee,,,,fly,\n"
                + "c,다,1,10,1,0,1,0.3,5,1,0.5,1,1,melee,,,,,\n";
            var data = MonsterSheet.Parse(monsters, AttackHeader +
                "nobody,x,엑스,slam,self,0.5,100,1,,,,,,,1,100,,,,,,,,\n" +
                "c,line,선,thrust,nearest,0.5,100,,,,,,,,1,100,,,,,,,,\n" +
                "c,pool,웅덩이,slam,self,0.5,100,1,,,,,,,1,100,,,mud:3,,,,,\n" +
                "c,when,조건,slam,self,0.5,100,1,,,,,,,1,100,,,,sometimes,,,,\n" +
                "c,ok,됨,slam,self,0.5,100,1,,,,,,,1,100,,,,hp<50|once,,,,\n" +
                "c,ok,또,slam,self,0.5,100,1,,,,,,,1,100,,,,,,,,\n");

            string all = string.Join("\n", data.Errors);
            Assert.IsTrue(data.Errors.Any(e => e.Contains("monsters.csv 2행") && e.Contains("role")), all);
            Assert.IsTrue(data.Errors.Any(e => e.Contains("monsters.csv 3행") && e.Contains("traits")), all);
            Assert.IsTrue(data.Errors.Any(e => e.Contains("monster_attacks.csv 2행") && e.Contains("nobody")), all);
            Assert.IsTrue(data.Errors.Any(e => e.Contains("monster_attacks.csv 3행") && e.Contains("length")), all);
            Assert.IsTrue(data.Errors.Any(e => e.Contains("monster_attacks.csv 4행") && e.Contains("pool")), all);
            Assert.IsTrue(data.Errors.Any(e => e.Contains("monster_attacks.csv 5행") && e.Contains("when")), all);
            Assert.IsTrue(data.Errors.Any(e => e.Contains("monster_attacks.csv 7행") && e.Contains("이미")), all);
            var kept = data.Monsters.Single(m => m.Id == "c").Attacks.Single();
            Assert.AreEqual(50, kept.HealthBelow);
            Assert.IsTrue(kept.Once);
        }

        // ---- The converted monsters ----

        [Test]
        public void ASlimePouncesOnWhereItsTargetStood()
        {
            var world = Room(Monsters.Shipped);
            var player = world.AddPlayer("a", null);
            var slime = world.SpawnEnemy(Monsters.Shipped.Find("slime"), player.Position + new Vec2(3.5f, 0));

            Run(world, 0.2f);
            Assert.AreEqual(EnemyState.Windup, slime.State);
            Assert.AreEqual("pounce", slime.Attack.Id);
            var mark = world.Hazards.Single();
            Assert.AreEqual(HazardShape.Circle, mark.Shape);
            Assert.Less(Vec2.Distance(mark.Position, player.Position), 0.01f, "the landing is marked under the player");
            Assert.IsFalse(mark.Attached, "a landing away from the monster is sent as a hazard");

            var events = Run(world, 0.6f);
            Assert.AreEqual(9, Taken(events, player), "standing in the mark is hit for the slime's damage");
            Assert.Less(Vec2.Distance(slime.Position, player.Position), 0.9f, "and the slime has landed there");
            Assert.IsEmpty(world.Hazards);
        }

        [Test]
        public void WalkingOutOfAPounceAvoidsItAndThenTheSlimeBites()
        {
            var world = Room(Monsters.Shipped);
            var player = world.AddPlayer("a", null);
            var slime = world.SpawnEnemy(Monsters.Shipped.Find("slime"), player.Position + new Vec2(3.5f, 0));
            Run(world, 2 * World.TickSeconds);
            Assert.AreEqual(EnemyState.Windup, slime.State);
            // A quarter of a second to notice, then walk at the plain pace.
            Run(world, 0.25f);
            var events = Run(world, 0.4f, () => world.MovePlayer(player.Id, PlayerMotor.Step(world.Map, player.Position, new Vec2(0, 1), player.MoveSpeed), new Vec2(0, 1), 1));
            Assert.AreEqual(0, Taken(events, player), "out of the circle before it lands");

            // Standing beside it now, it bites: a wedge that starts at the slime, drawn from its state on the screen.
            Moves.Walk(world, player, slime.Position + new Vec2(1.0f, 0));
            Run(world, 1.3f);
            for (int i = 0; i < World.Ticks(3) && slime.Attack?.Id != "bite"; i++) world.Step();
            Assert.AreEqual("bite", slime.Attack?.Id);
            Assert.IsTrue(world.Hazards.Single(h => h.Source == slime.Id).Attached);
            world.TakeEvents();
            Assert.AreEqual(9, Taken(Run(world, 0.6f), player));
        }

        [Test]
        public void ABeetleGuardsItsFrontAndIsDazedByAWall()
        {
            var monsters = Monsters.Shipped;
            var world = Room(monsters, 10);
            var player = world.AddPlayer("a", null);
            var beetle = world.SpawnEnemy(monsters.Find("cave_beetle"), player.Position + new Vec2(-2, 0));
            Run(world, 0.1f);
            Assert.AreEqual("ram", beetle.Attack?.Id);
            Assert.IsTrue(world.Hazards.Single().Attached);

            // A blow on its front is mostly turned; one from behind lands whole.
            float before = beetle.Health;
            world.Hurt(beetle, player, 100);
            float front = before - beetle.Health;
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.Guarded && e.Source == beetle.Id));
            Moves.Walk(world, player, beetle.Position + new Vec2(-1.5f, 0.01f));
            before = beetle.Health;
            world.Hurt(beetle, player, 100);
            float back = before - beetle.Health;
            Assert.AreEqual(back * World.GuardShare, front, 1.5f);

            // The player is out of its path now: it runs on into the wall and is dazed, unguarded and taking more.
            beetle.Health = beetle.MaxHealth;
            var events = Run(world, 1.6f);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxStagger), "it hit the wall");
            Assert.AreEqual(EnemyState.Stagger, beetle.State);
            Moves.Walk(world, player, beetle.Position - beetle.Facing * 1.5f);
            beetle.MaxHealth = beetle.Health = 10000;
            before = beetle.Health;
            world.Hurt(beetle, player, 100);
            Assert.AreEqual(back * World.StaggerMore, before - beetle.Health, 1.5f, "dazed, it takes half again as much");
            world.TakeEvents();
            beetle.Facing = (player.Position - beetle.Position).Normalized;
            before = beetle.Health;
            world.Hurt(beetle, player, 100);
            Assert.AreEqual(back * World.StaggerMore, before - beetle.Health, 1.5f, "and its guard is down");
            Assert.IsFalse(world.TakeEvents().Any(e => e.Type == SimEventType.Guarded));
            Run(world, World.StaggerSeconds + 0.1f);
            Assert.AreNotEqual(EnemyState.Stagger, beetle.State);
        }

        [Test]
        public void AFrostWispRingsItselfAndHoldsWhoeverItCatches()
        {
            var world = Room(Monsters.Shipped);
            var player = world.AddPlayer("a", null);
            var wisp = world.SpawnEnemy(Monsters.Shipped.Find("frost_wisp"), player.Position + new Vec2(2, 0));
            Assert.IsTrue(world.ForceAttack(wisp, "frost_ring"));
            Assert.IsTrue(world.Hazards.Single().Attached);

            var events = Run(world, 0.9f);
            Assert.Greater(Taken(events, player), 0);
            Assert.IsTrue(player.Afflictions.Rooted);
            Assert.AreEqual(0, player.MoveSpeed);
            Assert.IsTrue(player.Afflictions.Chilled, "its element still chills");
            Run(world, 1.1f);
            Assert.IsFalse(player.Afflictions.Rooted);
            Assert.Greater(player.MoveSpeed, 0);
        }

        [Test]
        public void IciclesFallOneAfterAnotherOnWhereThePlayerIsByThen()
        {
            var world = Room(Monsters.Shipped);
            var player = world.AddPlayer("a", null);
            var wisp = world.SpawnEnemy(Monsters.Shipped.Find("frost_wisp"), player.Position + new Vec2(5, 0));
            Assert.IsTrue(world.ForceAttack(wisp, "icicle"));
            var first = world.Hazards.Single();
            Run(world, 0.2f);
            Moves.Walk(world, player, player.Position + new Vec2(0, 3));
            Run(world, World.LobSeconds);
            var second = world.Hazards.Single(h => h != first);
            Assert.Less(Vec2.Distance(second.Position, player.Position), 0.01f, "the second is aimed at where they went");
            Assert.Greater(second.ResolveTick, first.ResolveTick);
        }

        [Test]
        public void AVoidWispBlinksAwayFromAPlayerWhoComesClose()
        {
            var world = Room(Monsters.Shipped);
            var player = world.AddPlayer("a", null);
            var wisp = world.SpawnEnemy(Monsters.Shipped.Find("void_wisp"), player.Position + new Vec2(1.5f, 0));
            Run(world, 0.1f);
            Assert.AreEqual("shift", wisp.Attack?.Id, "inside its blink's range that is what it does");
            var from = wisp.Position;
            var mark = world.Hazards.Single();
            Assert.AreEqual(0, mark.Damage, "the mark where it will be harms nothing");
            Assert.AreEqual(4, Vec2.Distance(mark.Position, from), 0.01f);
            var events = Run(world, 0.45f);
            Assert.Less(Vec2.Distance(wisp.Position, mark.Position), 0.01f);
            Assert.Greater(Vec2.Distance(wisp.Position, player.Position), Vec2.Distance(from, player.Position), "away from the player");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxBlink && Vec2.Distance(e.Position, from) < 0.01f));
        }

        // ---- The new pieces of the engine ----

        [Test]
        public void ABouncingShotComesOffWalls()
        {
            var world = Room(size: 12);
            var player = world.AddPlayer("a", null);
            var bouncer = world.SpawnEnemy(Pieces.Find("bouncer"), player.Position + new Vec2(-3, -2));
            // Aimed at the player, who steps aside: the shot goes on to the wall.
            Assert.IsTrue(world.ForceAttack(bouncer, "shot"));
            Run(world, 0.31f);
            var shot = world.Projectiles.Single();
            Moves.Walk(world, player, player.Position + new Vec2(-2, 3));
            var first = shot.Velocity;
            var events = Run(world, 3f);
            var bounces = events.Where(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxBounce).ToList();
            Assert.AreEqual(2, bounces.Count, "twice off the walls, and the third wall ends it");
            Assert.IsEmpty(world.Projectiles);
            Assert.AreEqual(first.Length, 9, 0.01f);
        }

        [Test]
        public void ABounceTurnsTheShotAndKeepsItsPace()
        {
            var world = Room(size: 12);
            var player = world.AddPlayer("a", null);
            var bouncer = world.SpawnEnemy(Pieces.Find("bouncer"), player.Position + new Vec2(-2, 0));
            world.ForceAttack(bouncer, "shot");
            Run(world, 0.31f);
            Moves.Walk(world, player, player.Position + new Vec2(0, 3));
            var shot = world.Projectiles.Single();
            Assert.Greater(shot.Velocity.X, 0);
            for (int i = 0; i < 60 && shot.Velocity.X > 0; i++) world.Step();
            Assert.Less(shot.Velocity.X, 0, "straight back off the wall it flew at");
            Assert.AreEqual(9, shot.Velocity.Length, 0.01f);
            Assert.IsFalse(Collision.Overlaps(world.Map, shot.Position, shot.Radius * 0.5f));
        }

        [Test]
        public void AReturningShotStrikesOnTheWayOutAndOnTheWayBack()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var thrower = world.SpawnEnemy(Pieces.Find("thrower"), player.Position + new Vec2(-3, 0));
            world.ForceAttack(thrower, "disc");
            var events = Run(world, 4f);
            var hits = events.Where(e => e.Type == SimEventType.Hit && e.Target == player.Id).ToList();
            Assert.AreEqual(2, hits.Count, "once out, once back");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxTurn), "it stops where it turns");
            Assert.IsEmpty(world.Projectiles, "home again, it is gone");
        }

        [Test]
        public void AReturningShotFliesItsLengthBeforeItTurns()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var thrower = world.SpawnEnemy(Pieces.Find("thrower"), player.Position + new Vec2(-3, 0));
            world.ForceAttack(thrower, "disc");
            Run(world, 0.31f);
            Moves.Walk(world, player, player.Position + new Vec2(0, 4));
            var events = Run(world, 1.2f);
            var turn = events.Single(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxTurn);
            Assert.AreEqual(7, Vec2.Distance(turn.Position, thrower.Position), 0.6f);
        }

        [Test]
        public void ABlinkNeedsSomewhereToGo()
        {
            // A cell with no room four tiles any way: the blink is put off and the monster stays.
            var map = Maps.FromRows(
                "#####",
                "#...#",
                "#...#",
                "#...#",
                "#####");
            var world = Room(map: map);
            var player = world.AddPlayer("a", null);
            var blinker = world.SpawnEnemy(Pieces.Find("blinker"), TileMap.Center(3, 3));
            Assert.IsFalse(world.ForceAttack(blinker, "shift"));
            Assert.IsEmpty(world.Hazards);
        }

        [Test]
        public void APullDragsWhoeverStandsInItAndAWalkerGetsOut()
        {
            var world = Room();
            var still = world.AddPlayer("a", null);
            var puller = world.SpawnEnemy(Pieces.Find("puller"), still.Position + new Vec2(6, 0));
            world.ForceAttack(puller, "well");
            var well = world.Hazards.Single();
            Assert.AreEqual(HazardShape.Pull, well.Shape);
            Moves.Walk(world, still, well.Position + new Vec2(2.2f, 0));
            Run(world, 0.5f);
            Assert.AreEqual(2.2f - 2 * 0.5f, Vec2.Distance(still.Position, well.Position), 0.1f, "dragged toward the middle at its speed");
            var events = Run(world, 1.6f);
            Assert.Greater(Taken(events, still), 0, "and caught when it bursts");

            // From the middle, after the time to notice, walking out against the drag is quick enough.
            world.ForceAttack(puller, "well");
            well = world.Hazards.Single();
            Moves.Walk(world, still, well.Position);
            Run(world, 0.25f);
            events = Run(world, 1.8f, () => world.MovePlayer(still.Id, PlayerMotor.Step(world.Map, still.Position, new Vec2(0, 1), still.MoveSpeed), new Vec2(0, 1), 1));
            Assert.AreEqual(0, Taken(events, still));
        }

        [Test]
        public void ATrapWaitsUntilItIsSteppedOnOrItsTimeIsUp()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var miner = world.SpawnEnemy(Pieces.Find("miner"), player.Position + new Vec2(6, 0));
            world.ForceAttack(miner, "trap");
            var traps = world.Hazards.Where(h => h.Shape == HazardShape.Mine).ToList();
            Assert.AreEqual(2, traps.Count);
            Assert.IsTrue(traps.All(t => Vec2.Distance(t.Position, player.Position) > 1.9f), "beside the player, not under them");
            long expires = traps[0].ResolveTick;
            Assert.AreEqual(World.Ticks(1.0f) + World.Ticks(6), expires - world.Tick);

            // Stepped on before it is armed: nothing yet.
            Moves.Walk(world, player, traps[0].Position);
            Run(world, 0.5f);
            Assert.AreEqual(expires, traps[0].ResolveTick);
            // Armed: the step sets it off, with the time to walk out of it left.
            var events = Run(world, 0.6f);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxMine));
            Assert.Less(traps[0].ResolveTick, expires);
            Assert.AreEqual(expires, traps[1].ResolveTick, "the other still waits");
            events.AddRange(Run(world, 0.6f));
            Assert.Greater(Taken(events, player), 0);

            // The other goes off by itself when its time is up, hurting nobody.
            Moves.Walk(world, player, player.Position + new Vec2(0, 6));
            events = Run(world, 6f);
            Assert.IsFalse(world.Hazards.Any(h => h.Shape == HazardShape.Mine));
            Assert.AreEqual(0, Taken(events, player));

            // No more than the cap at once: the oldest go.
            for (int i = 0; i < 4; i++)
            {
                world.ForceAttack(miner, "trap");
                Run(world, 1.1f);
            }
            Assert.LessOrEqual(world.Hazards.Count(h => h.Shape == HazardShape.Mine), World.MaxMines);
        }

        [Test]
        public void AMendFillsAnAllyUntilTheMenderIsHit()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var mender = world.SpawnEnemy(Pieces.Find("mender"), player.Position + new Vec2(6, 0));
            var hurt = world.SpawnEnemy(Pieces.Find("fodder"), player.Position + new Vec2(6, 3));
            hurt.Health = 40;
            Assert.IsTrue(world.ForceAttack(mender, "mend"));
            var events = Run(world, 1.5f);
            Assert.AreEqual(EnemyState.Channel, mender.State);
            Assert.AreEqual(hurt.Id, mender.TetherId);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Tether && e.Source == mender.Id && e.Target == hurt.Id && e.Amount > 0));
            Assert.AreEqual(40 + 10, hurt.Health, 1.5f, "ten percent of its health a second");

            world.Hurt(mender, player, 1);
            Assert.AreEqual(EnemyState.Recover, mender.State);
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.Tether && e.Source == mender.Id && e.Amount == 0), "the line breaks");
            float at = hurt.Health;
            Run(world, 0.5f);
            Assert.AreEqual(at, hurt.Health);
        }

        [Test]
        public void AnEmpoweredMonsterHitsHarderAndMovesFasterForAWhile()
        {
            var world = Room();
            world.AddPlayer("a", null);
            var chief = world.SpawnEnemy(Pieces.Find("chief"), world.Spawn + new Vec2(6, 0));
            var near = world.SpawnEnemy(Pieces.Find("fodder"), world.Spawn + new Vec2(6, 2));
            var far = world.SpawnEnemy(Pieces.Find("fodder"), world.Spawn + new Vec2(6, 9));
            world.ForceAttack(chief, "rally");
            var events = Run(world, 0.6f);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxEmpower));
            Assert.IsTrue(near.Empowered);
            Assert.AreEqual(13, near.Damage, 0.01f);
            Assert.AreEqual(2.6f, near.Speed, 0.01f);
            Assert.IsFalse(far.Empowered, "out of its reach");
            Assert.IsFalse(chief.Empowered, "not itself");
            Assert.AreNotEqual(0, World.EnemyStateByte(near) & World.EnemyEmpoweredBit);
            Run(world, 4.1f);
            Assert.IsFalse(near.Empowered);
            Assert.AreEqual(10, near.Damage, 0.01f);
        }

        [Test]
        public void ACallIsStoppedByAHitBeforeItIsMade()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var chief = world.SpawnEnemy(Pieces.Find("chief"), player.Position + new Vec2(1, 0));
            var near = world.SpawnEnemy(Pieces.Find("fodder"), player.Position + new Vec2(8, 2));
            world.ForceAttack(chief, "rally");
            Run(world, 0.2f);
            world.Hurt(chief, player, 1);
            Run(world, 0.5f);
            Assert.IsFalse(near.Empowered);
        }

        // ---- Traits ----

        [Test]
        public void ASplitLeavesSmallOnesOnlyPastTheFirstZone()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var first = world.SpawnEnemy(Pieces.Find("blob"), player.Position + new Vec2(5, 0));
            world.Hurt(first, player, 1000);
            Run(world, 0.1f);
            Assert.AreEqual(0, world.Enemies.Count(e => e.Definition.Id == "blob"), "in the first zone it only falls");

            var big = world.SpawnEnemy(Pieces.Find("blob"), player.Position + new Vec2(5, 0));
            big.ZoneLevels = 1;
            big.MaxHealth = big.Health = 150;
            world.Hurt(big, player, 1000);
            var events = world.TakeEvents();
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxSplit));
            Run(world, 0.1f);
            var small = world.Enemies.Where(e => e.Definition.Id == "blob").ToList();
            Assert.AreEqual(2, small.Count);
            Assert.IsTrue(small.All(e => e.Small && e.MaxHealth == 150 * World.SplitHealth && e.Damage == 10 * World.SplitPower));
            Assert.AreNotEqual(0, World.EnemyStateByte(small[0]) & World.EnemySmallBit);
            foreach (var one in small) world.Hurt(one, player, 1000);
            Run(world, 0.1f);
            Assert.AreEqual(0, world.Enemies.Count(e => e.Definition.Id == "blob"), "a small one does not split again");
        }

        [Test]
        public void WhereItFellACloudFillsTheMonstersInIt()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var blob = world.SpawnEnemy(Pieces.Find("blob"), player.Position + new Vec2(5, 0));
            var near = world.SpawnEnemy(Pieces.Find("fodder"), player.Position + new Vec2(5, 1));
            var far = world.SpawnEnemy(Pieces.Find("fodder"), player.Position + new Vec2(5, 5));
            near.Health = far.Health = 50;
            world.Hurt(blob, player, 1000);
            var cloud = world.Hazards.Single(h => h.Shape == HazardShape.Balm);
            Assert.AreEqual(World.DeathcloudRadius, cloud.Size);
            Moves.Walk(world, player, cloud.Position);
            var events = Run(world, 1f);
            Assert.AreEqual(50 + World.DeathcloudFill, near.Health, 1f);
            Assert.AreEqual(50, far.Health);
            Assert.AreEqual(0, Taken(events, player), "it does nothing to players");
            Run(world, World.DeathcloudSeconds);
            Assert.IsEmpty(world.Hazards);
        }

        [Test]
        public void WhereItFellFlashesAndThenShardsFlyOut()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var shard = world.SpawnEnemy(Pieces.Find("shard"), player.Position + new Vec2(5, 0));
            world.Hurt(shard, player, 1000);
            var flash = world.Hazards.Single();
            Assert.AreEqual(Hazard.DeathFlash, flash.Mark);
            Assert.AreEqual(0, flash.Damage);
            Run(world, World.DeathburstSeconds - 0.1f);
            Assert.IsEmpty(world.Projectiles);
            var events = Run(world, 0.2f);
            Assert.AreEqual(4, world.Projectiles.Count);
            Assert.IsTrue(world.Projectiles.All(p => p.Damage == 5 && !p.Friendly));
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.MonsterFx && e.Amount == World.MonsterFxDeathburst));
            Run(world, 1f);
            Assert.IsEmpty(world.Projectiles, "they fly a few tiles and are gone");
        }

        [Test]
        public void ATrailIsLeftWhereItWalksAndAPoolWhereItsBlowLanded()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var ember = world.SpawnEnemy(Pieces.Find("ember"), player.Position + new Vec2(4, 0));
            for (int i = 0; i < 40; i++)
            {
                ember.Position += new Vec2(0, 0.1f);
                world.Step();
            }
            var pools = world.Hazards.Where(h => h.Shape == HazardShape.Fire).ToList();
            Assert.IsNotEmpty(pools);
            Assert.LessOrEqual(pools.Count, World.MaxMonsterPools, "the oldest go");
            Moves.Walk(world, player, pools.Last().Position);
            Run(world, 0.2f);
            Assert.IsTrue(player.Afflictions.Burning, "standing in fire burns");

            // A blow with a pool leaves it where it landed, for its time.
            var blob = world.SpawnEnemy(Pieces.Find("blob"), player.Position + new Vec2(-8, 0));
            world.ForceAttack(blob, "slam");
            Run(world, 0.7f);
            var spores = world.Hazards.Single(h => h.Shape == HazardShape.Spore);
            Assert.AreEqual(1.2f, spores.Size);
            Moves.Walk(world, player, spores.Position + new Vec2(0, 0.8f));
            world.TakeEvents();
            Assert.Greater(Taken(Run(world, 1.1f), player), 0, "spores bite whoever stands in them");
            Run(world, 3f);
            Assert.IsFalse(world.Hazards.Any(h => h.Shape == HazardShape.Spore));
        }

        [Test]
        public void AHitCanShoveThePlayerAndTheirClientIsTold()
        {
            var world = Room();
            var player = world.AddPlayer("a", null);
            var shard = world.SpawnEnemy(Pieces.Find("shard"), player.Position + new Vec2(-1.5f, 0));
            var from = player.Position;
            int corrections = player.Corrections;
            world.ForceAttack(shard, "jab");
            Run(world, 0.6f);
            Assert.AreEqual(2, player.Position.X - from.X, 0.05f, "two tiles away from the blow");
            Assert.Greater(player.Corrections, corrections);
        }

        // ---- Turns and the wire ----

        [Test]
        public void NoMoreThanFourMarksAreAimedAtOnePlayerAtOnce()
        {
            var world = Room(Monsters.Shipped);
            var player = world.AddPlayer("a", null);
            var slime = Monsters.Shipped.Find("slime");
            for (int i = 0; i < 8; i++) world.SpawnEnemy(slime, player.Position + Vec2.FromAngle(i * MathF.PI / 4) * 3.5f);
            int most = 0;
            bool waited = false;
            for (int i = 0; i < World.Ticks(1.2f); i++)
            {
                world.Step();
                int marking = world.Enemies.Count(e => e.State == EnemyState.Windup && e.Attack != null && e.Attack.MarksGround);
                most = Math.Max(most, marking);
                waited |= marking == World.MaxMarksPerTarget && world.Enemies.Any(e => e.State == EnemyState.Chase);
            }
            Assert.AreEqual(World.MaxMarksPerTarget, most);
            Assert.IsTrue(waited, "the others hold off until a mark has landed");
        }

        [Test]
        public void AnAttackThatStartsAtTheMonsterGoesOverTheWireInItsState()
        {
            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, null, Classes.Shipped, Tiles.Shipped);
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, new string[0], Tiles.Shipped);
            host.Receive(1, client.JoinMessage());
            void Tick(int ticks)
            {
                for (int i = 0; i < ticks; i++)
                {
                    foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
                    byte[] input = client.Step(Vec2.Zero, Vec2.Zero, false);
                    if (input != null) host.Receive(1, input);
                    host.Tick();
                    foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
                    client.AdvanceRenderClock(World.TickSeconds);
                }
            }
            Tick(3);
            var world = host.World;
            var player = world.Players.Single();
            var open = Enumerable.Range(0, 8).Select(k => Vec2.FromAngle(k * MathF.PI / 4)).First(d => !Collision.Overlaps(world.Map, player.Position + d * 1.2f, 0.5f));
            var slime = world.SpawnEnemy(Monsters.Shipped.Find("slime"), player.Position + open * 1.2f);

            Assert.IsTrue(world.ForceAttack(slime, "bite"));
            Tick(6);
            var seen = client.Others().Single(e => e.Id == slime.Id);
            Assert.AreEqual(EnemyState.Windup, seen.EnemyState);
            Assert.AreEqual(Tell.Area, seen.EnemyTell);
            Assert.AreEqual("bite", seen.MonsterAttack.Id);
            Assert.IsEmpty(client.Hazards(), "its wedge is drawn from the state: no hazard is sent");
            Assert.IsNotEmpty(world.Hazards, "though the host has it, for bots and for the hit");

            Tick(World.Ticks(2));
            Assert.IsTrue(world.ForceAttack(slime, "pounce"));
            Tick(6);
            var mark = client.Hazards().Single().State;
            Assert.IsTrue(mark.FromMonster);
            Assert.AreEqual((int)MonsterAttackShape.Leap, mark.MonsterShape);
            seen = client.Others().Single(e => e.Id == slime.Id);
            Assert.AreEqual("pounce", seen.MonsterAttack.Id);
            Assert.AreEqual(0, seen.Phase, "no rift mark");

            // One that fights the old way reads as before: no warning kind, no attack.
            var shooter = world.SpawnEnemy(Monsters.Shipped.Find("silk_spiderling"), player.Position + open * 3);
            Tick(World.Ticks(1.5f));
            var old = client.Others().Single(e => e.Id == shooter.Id);
            Assert.IsNull(old.MonsterAttack);
            Assert.AreEqual(Tell.None, old.EnemyTell);
        }

        [Test]
        public void TheClientIsDraggedByAPullAsTheHostDragsIt()
        {
            var host = new WorldHost(7, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, null, Classes.Shipped, Tiles.Shipped);
            var client = new WorldClient("key", "앨리스", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, new string[0], Tiles.Shipped);
            host.Receive(1, client.JoinMessage());
            void Tick(int ticks)
            {
                for (int i = 0; i < ticks; i++)
                {
                    foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
                    byte[] input = client.Step(Vec2.Zero, Vec2.Zero, false);
                    if (input != null) host.Receive(1, input);
                    host.Tick();
                    foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
                    client.AdvanceRenderClock(World.TickSeconds);
                }
            }
            Tick(3);
            var world = host.World;
            var player = world.Players.Single();
            var wisp = world.SpawnEnemy(Monsters.Shipped.Find("void_wisp"), player.Position);
            Assert.IsTrue(world.ForceAttack(wisp, "gravity_well"));
            var well = world.Hazards.Single(h => h.Shape == HazardShape.Pull);
            // The well is under the player; put its middle a little off so there is somewhere to be dragged.
            well.Position = Collision.Resolve(world.Map, player.Position + new Vec2(0.9f, 0.9f), 0.35f);
            float before = Vec2.Distance(client.Position, well.Position);
            int corrections = player.Corrections;
            Tick(15);
            Assert.Less(Vec2.Distance(client.Position, well.Position), before - 0.3f, "the client's own player moves without a key pressed");
            Assert.Less(Vec2.Distance(client.Position, player.Position), 0.2f, "and the host agrees where");
            Assert.AreEqual(corrections, player.Corrections);
        }
    }
}
