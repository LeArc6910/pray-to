using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class ClassTests
    {
        static readonly ClassDefinition Warrior = Classes.Shipped.Find("warrior");
        static readonly ClassDefinition FireMage = Classes.Shipped.Find("fire_mage");
        static readonly ClassDefinition Archer = Classes.Shipped.Find("archer");
        static readonly ClassDefinition Summoner = Classes.Shipped.Find("summoner");
        static readonly MonsterDefinition Slime = Monsters.Shipped.Find("slime");
        static readonly MonsterDefinition Dummy = Monsters.Dummy;

        static World Room(int size = 30, MonsterData monsters = null) =>
            new World(Maps.Room(size), TileMap.Center(size / 2, size / 2), monsters ?? Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });

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

        static int Slot(Player player, string skillId) => player.Skills.ToList().FindIndex(s => s.Id == skillId);

        // Every skill of the player's classes learned at rank 1, as a character that has spent its points would.
        static Player Learned(Player player)
        {
            World.TeachAll(player, 1);
            return player;
        }

        static void AssertError(ClassData data, params string[] parts) =>
            Assert.IsTrue(
                data.Errors.Any(error => parts.All(error.Contains)),
                $"No error mentions {string.Join(" + ", parts)}. Errors:\n{string.Join("\n", data.Errors)}");

        [Test]
        public void ShippedSheetsLoad()
        {
            Assert.IsTrue(Classes.Shipped.Success, string.Join("\n", Classes.Shipped.Errors));
            CollectionAssert.AreEqual(new[] { "whirlwind", "war_cry", "charge", "ground_slam", "iron_skin", "blade_storm" }, Warrior.Skills.Select(s => s.Id));
            CollectionAssert.AreEqual(new[] { "fireball", "flame_brand", "fire_ring", "meteor", "flame_burst", "inferno" }, FireMage.Skills.Select(s => s.Id));
            CollectionAssert.AreEqual(new[] { "volley", "eagle_eye", "gale_step", "power_shot", "explosive_arrow", "arrow_rain" }, Archer.Skills.Select(s => s.Id));
            CollectionAssert.AreEqual(new[] { "spirits", "spirit_pact", "life_spring", "spirit_horde", "spirit_blast", "guardian" }, Summoner.Skills.Select(s => s.Id));
            // Each class's skills open up at the same levels; the first is the free basic skill.
            foreach (var owner in new[] { Warrior, FireMage, Archer, Summoner })
            {
                CollectionAssert.AreEqual(new[] { 1, 3, 5, 8, 12, 18 }, owner.Skills.Select(s => s.Level), owner.Id);
                Assert.IsTrue(owner.Skills[0].IsBasic);
                Assert.IsFalse(owner.Skills[1].IsBasic);
            }
        }

        [Test]
        public void SkillsDescribeTheirNumbers()
        {
            Assert.AreEqual("피해 150% · 둘레 1.8칸 · 마나 10 · 재사용 4초", SkillText.Numbers(Classes.Skill("whirlwind")));
            Assert.AreEqual("피해 120% · 사거리 10칸 · 폭발 1.2칸 · 마나 5 · 재사용 1.5초", SkillText.Numbers(Classes.Skill("fireball")));
            Assert.AreEqual("6초 동안 근접 피해 30% 증가, 공격 속도 15% 증가 · 마나 15 · 재사용 15초", SkillText.Numbers(Classes.Skill("war_cry")));
            Assert.AreEqual("8초 동안 모든 공격에 화염 태그 · 마나 15 · 재사용 16초", SkillText.Numbers(Classes.Skill("flame_brand")));
            Assert.AreEqual("최대 생명력 20% 증가, 근접 피해 10% 증가", SkillText.Passive(Warrior));
            Assert.AreEqual("피해 70% × 5발 · 사거리 9칸 · 마나 12 · 재사용 4초", SkillText.Numbers(Classes.Skill("volley")));
            Assert.AreEqual("2마리 · 15초 · 한 번에 피해 40% · 7칸 안의 적 · 마나 25 · 재사용 18초", SkillText.Numbers(Classes.Skill("spirits")));
        }

        [Test]
        public void SheetErrorsNameTheRowAndWhatIsMissing()
        {
            var data = ClassSheet.Parse(
                Classes.ClassesHeader +
                "mage,마법사,,\n" +
                "lucky,행운아,,CritChance+1~3\n" +
                "busy,바쁜 이,,\n",
                Classes.SkillsHeader +
                "bolt,번개 화살,mage,projectile,magic|lightning,100,1,0,,10,,,,\n" +
                "aura,기운,mage,buff,,,10,,,,5,,,\n" +
                "spark,불티,busy,nova,fire,50,2,1,,,,,,\n" +
                "flare,불꽃,busy,nova,fire,50,2,1,,,,,,\n" +
                "extra,하나 더,busy,nova,fire,50,2,1,,,,,,\n" +
                "fourth,넷째,busy,nova,fire,50,2,1,,,,,,\n" +
                "fifth,다섯째,busy,nova,fire,50,2,1,,,,,,\n" +
                "sixth,여섯째,busy,nova,fire,50,2,1,,,,,,\n" +
                "seventh,일곱째,busy,nova,fire,50,2,1,,,,,,\n" +
                "lost,길 잃은,nobody,nova,,50,2,1,,,,,,\n");

            AssertError(data, "classes.csv 3행", "범위로 적을 수 없습니다");
            AssertError(data, "skills.csv 2행", "projectile 스킬은 speed 칸을 채워야 합니다");
            AssertError(data, "skills.csv 3행", "modifiers나 grants");
            AssertError(data, "skills.csv 10행", "'busy'의 스킬이 6개를 넘습니다");
            AssertError(data, "skills.csv 11행", "클래스 'nobody'가 classes.csv에 없습니다");
            Assert.AreEqual(5, data.Errors.Count, string.Join("\n", data.Errors));
        }

        [Test]
        public void ChoosingKeepsKnownClassesOnceUpToTheLimit()
        {
            var chosen = ClassRules.Choose(Classes.Shipped, new[] { "fire_mage", "nobody", "fire_mage", "warrior" }, 2);

            CollectionAssert.AreEqual(new[] { FireMage, Warrior }, chosen);
            CollectionAssert.AreEqual(new[] { Warrior }, ClassRules.Choose(Classes.Shipped, new[] { "warrior", "fire_mage" }, 1));
            Assert.IsEmpty(ClassRules.Choose(Classes.Shipped, null, 2));
        }

        [Test]
        public void SkillsTakeSlotsInTheOrderClassesWereChosen()
        {
            var world = Room();

            var warriorFirst = Learned(world.AddPlayer("a", null, Warrior, FireMage));
            var mageFirst = Learned(world.AddPlayer("b", null, FireMage, Warrior));
            var classless = Learned(world.AddPlayer("c", null));

            CollectionAssert.AreEqual(new[] { "whirlwind", "war_cry", "fireball", "flame_brand" }, warriorFirst.Skills.Select(s => s.Id));
            CollectionAssert.AreEqual(new[] { "fireball", "flame_brand", "whirlwind", "war_cry" }, mageFirst.Skills.Select(s => s.Id));
            Assert.IsEmpty(classless.Skills);
            Assert.IsFalse(world.UseSkill(classless.Id, 0, Vec2.Zero));
        }

        [Test]
        public void PassivesCountOnlyForTheirTags()
        {
            var warrior = PlayerStats.From(Gear.Wearing("copper_sword"), new[] { Warrior });
            var mage = PlayerStats.From(Gear.Wearing("copper_sword"), new[] { FireMage });

            Assert.AreEqual(120, warrior.MaxHealth, 0.001f);
            Assert.AreEqual(4 * 1.1f, warrior.Damage, 0.001f, "a sword swing is melee");
            Assert.AreEqual(4, mage.Damage, 0.001f, "fire damage does nothing for a plain sword swing");
            Assert.AreEqual(4 * 1.15f, mage.DamageFor(mage.ContextFor(Classes.Skill("fireball"))), 0.001f);
        }

        [Test]
        public void FlameBrandTurnsEveryAttackFire()
        {
            var both = new[] { Warrior, FireMage };
            var plain = PlayerStats.From(Gear.Wearing("copper_sword"), both);
            var branded = PlayerStats.From(Gear.Wearing("copper_sword"), both, new[] { Classes.Skill("flame_brand") });
            var whirlwind = Classes.Skill("whirlwind");

            Assert.AreEqual(SkillTag.Melee, plain.AttackTags);
            Assert.AreEqual(SkillTag.Melee | SkillTag.Fire, branded.AttackTags);
            Assert.AreEqual(4 * 1.1f, plain.DamageFor(plain.ContextFor(whirlwind)), 0.001f);
            // The warrior's melee 10% and the fire mage's fire 15% now both count.
            Assert.AreEqual(4 * 1.25f, branded.DamageFor(branded.ContextFor(whirlwind)), 0.001f);
            Assert.AreEqual(4 * 1.25f, branded.Damage, 0.001f);
        }

        [Test]
        public void WhirlwindHitsEveryoneAroundOnce()
        {
            var world = Room();
            var player = Learned(world.AddPlayer("a", Gear.Wearing("copper_sword"), Warrior));
            var near = new[] { new Vec2(1.2f, 0), new Vec2(-1.2f, 0), new Vec2(0, 1.5f) }
                .Select(offset => world.SpawnEnemy(Slime, player.Position + offset))
                .ToList();
            var far = world.SpawnEnemy(Slime, player.Position + new Vec2(4, 0));
            int slot = Slot(player, "whirlwind");

            Assert.IsTrue(world.UseSkill(player.Id, slot, Vec2.Zero));
            var events = world.TakeEvents();

            // 4 × 110% (warrior) × 150% = 6.6, or 9.9 on a crit; one roll for every target.
            int dealt = Slime.Health - (int)near[0].Health;
            Assert.That(dealt, Is.EqualTo(7).Or.EqualTo(10));
            foreach (var enemy in near) Assert.AreEqual(Slime.Health - dealt, enemy.Health);
            Assert.AreEqual(Slime.Health, far.Health);
            Assert.AreEqual(3, events.Count(e => e.Type == SimEventType.Hit && e.Source == player.Id));
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.SkillUsed && e.Source == player.Id && e.Amount == slot));
        }

        [Test]
        public void SkillsCoolDownEachOnTheirOwn()
        {
            var world = Room();
            var player = Learned(world.AddPlayer("a", null, Warrior));
            int whirlwind = Slot(player, "whirlwind");
            int warCry = Slot(player, "war_cry");

            Assert.IsTrue(world.UseSkill(player.Id, whirlwind, Vec2.Zero));
            Assert.IsFalse(world.UseSkill(player.Id, whirlwind, Vec2.Zero), "still cooling down");
            Assert.IsTrue(world.UseSkill(player.Id, warCry, Vec2.Zero), "another slot is ready");

            Run(world, player.Skills[whirlwind].CooldownTicks - 1);
            Assert.AreEqual(1, player.CooldownTicks(whirlwind));
            Assert.IsFalse(world.UseSkill(player.Id, whirlwind, Vec2.Zero));
            Run(world, 1);
            Assert.IsTrue(world.UseSkill(player.Id, whirlwind, Vec2.Zero));
        }

        [Test]
        public void FireballFliesPastPlayersAndBurstsOnTheFirstEnemy()
        {
            var world = Room();
            var player = Learned(world.AddPlayer("a", Gear.Wearing("copper_staff"), FireMage));
            var friend = Learned(world.AddPlayer("b", null));
            Assert.IsTrue(world.MovePlayer(friend.Id, friend.Position + new Vec2(2, 0), new Vec2(1, 0), 10));
            var aimedAt = player.Position + new Vec2(5, 0);
            var target = world.SpawnEnemy(Dummy, aimedAt);
            var beside = world.SpawnEnemy(Dummy, player.Position + new Vec2(5, 1));
            var behind = world.SpawnEnemy(Dummy, player.Position + new Vec2(8, 0));

            Assert.IsTrue(world.UseSkill(player.Id, Slot(player, "fireball"), new Vec2(1, 0)));
            var events = Run(world, World.TicksPerSecond);

            // The staff's lowest roll is 3, raised by its own magic 30% and the fire mage's fire 15%: 3 × 145% × 120%
            // = 5.2, or 7.8 on a crit. Everything in the burst takes the same roll (and the same burn after it).
            int dealt = events.First(e => e.Type == SimEventType.Hit && e.Target == target.Id).Amount;
            Assert.That(dealt, Is.EqualTo(5).Or.EqualTo(8));
            Assert.AreEqual(target.Health, beside.Health);
            Assert.AreEqual(Dummy.Health, behind.Health);
            Assert.AreEqual(friend.Stats.MaxHealth, friend.Health);
            var burst = events.Single(e => e.Type == SimEventType.Burst);
            Assert.AreEqual(player.Id, burst.Source);
            Assert.Less(Vec2.Distance(burst.Position, aimedAt), 1, "on the near side of the first enemy");
            Assert.IsEmpty(world.Projectiles);
        }

        [Test]
        public void FireballBurstsAtTheEndOfItsRangeOrOnAWall()
        {
            var world = Room(40);
            var player = Learned(world.AddPlayer("a", null, FireMage));
            int fireball = Slot(player, "fireball");

            world.UseSkill(player.Id, fireball, new Vec2(1, 0));
            var open = Run(world, World.TicksPerSecond * 2).Single(e => e.Type == SimEventType.Burst);
            Assert.AreEqual(10, Vec2.Distance(open.Position, player.Position), 0.5f);

            // The room's wall is 19 tiles down.
            Run(world, player.Skills[fireball].CooldownTicks);
            world.MovePlayer(player.Id, player.Position, new Vec2(0, -1), 1);
            Assert.IsTrue(world.UseSkill(player.Id, fireball, Vec2.Zero), "aims the way the player faces");
            var walled = Run(world, World.TicksPerSecond * 2).Single(e => e.Type == SimEventType.Burst);
            Assert.Less(walled.Position.Y, player.Position.Y);
        }

        [Test]
        public void WarCryLastsItsDuration()
        {
            var world = Room();
            var player = Learned(world.AddPlayer("a", Gear.Wearing("copper_sword"), Warrior));
            int slot = Slot(player, "war_cry");

            Assert.IsTrue(world.UseSkill(player.Id, slot, Vec2.Zero));
            Assert.IsTrue(player.HasBuff(slot));
            Assert.AreEqual(4 * 1.4f, player.Stats.Damage, 0.001f, "warrior 10% and war cry 30% melee");
            Assert.AreEqual(2 * 1.15f, player.Stats.AttacksPerSecond, 0.001f);

            Run(world, player.Skills[slot].DurationTicks);

            Assert.IsFalse(player.HasBuff(slot));
            Assert.AreEqual(4 * 1.1f, player.Stats.Damage, 0.001f);
            Assert.AreEqual(2, player.Stats.AttacksPerSecond, 0.001f);
        }

        [Test]
        public void FallingEndsBuffs()
        {
            var world = Room(14, Monsters.Brute);
            var player = Learned(world.AddPlayer("a", null, Warrior));
            int slot = Slot(player, "war_cry");
            world.UseSkill(player.Id, slot, Vec2.Zero);
            world.SpawnEnemy(Monsters.Brute.Monsters[0], player.Position + new Vec2(1, 0));

            Run(world, World.TicksPerSecond);

            Assert.IsTrue(player.IsDead);
            Assert.IsFalse(player.HasBuff(slot));
            Assert.AreEqual(1.1f, player.Stats.Damage, 0.001f);
            Assert.IsFalse(world.UseSkill(player.Id, Slot(player, "whirlwind"), Vec2.Zero), "the fallen cannot use skills");
        }

        [Test]
        public void SkillsWakeAndHurtBosses()
        {
            var world = new World(Maps.Room(40), TileMap.Center(20, 16), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });
            var boss = world.PlaceBoss(Bosses.Shipped.Bosses[0], TileMap.Center(20, 26), 8);
            var player = Learned(world.AddPlayer("a", null, FireMage));
            Assert.AreEqual(BossState.Dormant, boss.State);

            world.UseSkill(player.Id, Slot(player, "fireball"), new Vec2(0, 1));
            Run(world, World.TicksPerSecond);

            Assert.AreNotEqual(BossState.Dormant, boss.State);
            Assert.Less(boss.Health, boss.MaxHealth);
        }
    
        [Test]
        public void SummonAndVolleySheetErrors()
        {
            var data = ClassSheet.Parse(
                Classes.ClassesHeader + "caller,부르는 이,,\n",
                Classes.SkillsHeader.Replace("duration,", "duration,count,") +
                "call,부르기,caller,summon,summon,40,10,,5,6,,2,,,\n" +
                "rain,화살비,caller,projectile,ranged,50,3,,10,8,,9,,,\n");

            AssertError(data, "skills.csv 2행", "summon", "duration");
            AssertError(data, "skills.csv 3행", "count");
        }

        [Test]
        public void VolleyFansItsArrowsOutAroundTheAim()
        {
            var world = Room();
            var player = Learned(world.AddPlayer("a", Gear.Wearing("copper_bow"), Archer));
            var ahead = world.SpawnEnemy(Dummy, player.Position + new Vec2(5, 0));
            // 24 degrees off the aim, where the outermost arrows go.
            var aside = world.SpawnEnemy(Dummy, player.Position + new Vec2(5 * 0.9135f, 5 * 0.4067f));

            Assert.IsTrue(world.UseSkill(player.Id, Slot(player, "volley"), new Vec2(1, 0)));
            Assert.AreEqual(5, world.Projectiles.Count);
            var angles = world.Projectiles.Select(p => MathF.Atan2(p.Velocity.Y, p.Velocity.X) * 180 / MathF.PI).OrderBy(a => a).ToList();
            for (int i = 0; i < 5; i++) Assert.AreEqual(-24 + 12 * i, angles[i], 0.01f);

            Run(world, World.TicksPerSecond);
            Assert.Less(ahead.Health, Dummy.Health);
            Assert.Less(aside.Health, Dummy.Health);
            Assert.IsEmpty(world.Projectiles);
        }

        [Test]
        public void SpiritsGoAfterEnemiesNearTheirPlayerAndTheirHitsCountAsThePlayers()
        {
            var world = Room();
            var player = Learned(world.AddPlayer("a", Gear.Wearing("copper_sword"), Summoner));
            var near = world.SpawnEnemy(Dummy, player.Position + new Vec2(4, 0));
            var far = world.SpawnEnemy(Dummy, player.Position + new Vec2(-10, 0));
            int slot = Slot(player, "spirits");

            Assert.IsTrue(world.UseSkill(player.Id, slot, Vec2.Zero));
            Assert.AreEqual(2, world.Minions.Count);
            Assert.IsTrue(world.Minions.All(m => m.OwnerId == player.Id && m.Slot == slot));
            var events = Run(world, World.TicksPerSecond * 3);

            Assert.Less(near.Health, Dummy.Health);
            Assert.AreEqual(Dummy.Health, far.Health, "beyond the skill's 7 tiles from the player");
            var hits = events.Where(e => e.Type == SimEventType.Hit && e.Target == near.Id).ToList();
            Assert.IsNotEmpty(hits);
            Assert.IsTrue(hits.All(e => e.Source == player.Id && e.Cause == slot + 1));
            // The sword's lowest roll is 4, raised by the summoner's summon 20%: 4 × 120% × 40% = 1.9, more on a crit.
            Assert.IsTrue(hits.All(e => e.Amount >= 2), string.Join(", ", hits.Select(e => e.Amount)));
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Attack && world.Minions.Any(m => m.Id == e.Source)));
            Assert.IsTrue(world.Minions.All(m => Vec2.Distance(m.Position, near.Position) < 1.2f), "close to what they strike");
        }

        [Test]
        public void SpiritsFollowTheirPlayerAndLeaveWhenTheirTimeIsUp()
        {
            var world = Room();
            var player = Learned(world.AddPlayer("a", null, Summoner));
            int slot = Slot(player, "spirits");
            world.UseSkill(player.Id, slot, Vec2.Zero);
            var first = world.Minions.Select(m => m.Id).ToList();

            var moved = player.Position + new Vec2(3, 0);
            for (int i = 0; i < 10; i++)
            {
                world.MovePlayer(player.Id, player.Position + new Vec2(0.3f, 0), new Vec2(1, 0), 3);
                Run(world, 3);
            }
            Assert.AreEqual(moved.X, player.Position.X, 0.01f);
            Run(world, World.TicksPerSecond);
            Assert.IsTrue(world.Minions.All(m => Vec2.Distance(m.Position, player.Position) < 1.2f), "waiting around the player");

            var events = Run(world, player.Skills[slot].DurationTicks - World.TicksPerSecond * 2);
            Assert.IsEmpty(world.Minions);
            CollectionAssert.AreEquivalent(first, events.Where(e => e.Type == SimEventType.Died).Select(e => e.Source));
        }

        [Test]
        public void SummoningAgainSendsTheOldSpiritsAway()
        {
            var data = ClassSheet.Parse(
                Classes.ClassesHeader + "caller,부르는 이,,\n",
                Classes.SkillsHeader.Replace("duration,", "duration,count,") + "call,부르기,caller,summon,summon,40,1,,5,6,10,3,,,\n");
            Assert.IsTrue(data.Success, string.Join("\n", data.Errors));
            var world = Room();
            var player = Learned(world.AddPlayer("a", null, data.Find("caller")));
            world.UseSkill(player.Id, 0, Vec2.Zero);
            var first = world.Minions.Select(m => m.Id).ToList();
            Assert.AreEqual(3, first.Count);

            Run(world, World.TicksPerSecond);
            Assert.IsTrue(world.UseSkill(player.Id, 0, Vec2.Zero));
            var events = world.TakeEvents();
            Assert.AreEqual(3, world.Minions.Count);
            CollectionAssert.IsEmpty(world.Minions.Select(m => m.Id).Intersect(first));
            CollectionAssert.AreEquivalent(first, events.Where(e => e.Type == SimEventType.Died).Select(e => e.Source));
        }

        [Test]
        public void SpiritsLeaveDormantBossesAlone()
        {
            var world = new World(Maps.Room(40), TileMap.Center(20, 16), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });
            var boss = world.PlaceBoss(Bosses.Shipped.Bosses[0], TileMap.Center(20, 20), 2);
            var player = Learned(world.AddPlayer("a", null, Summoner));
            world.UseSkill(player.Id, Slot(player, "spirits"), Vec2.Zero);

            Run(world, World.TicksPerSecond * 2);
            Assert.AreEqual(BossState.Dormant, boss.State);
            Assert.AreEqual(boss.MaxHealth, boss.Health);
        }

        [Test]
        public void SpiritsLeaveWithTheirFallenPlayer()
        {
            var world = Room(14, Monsters.Brute);
            var player = Learned(world.AddPlayer("a", null, Summoner));
            world.UseSkill(player.Id, Slot(player, "spirits"), Vec2.Zero);
            world.SpawnEnemy(Monsters.Brute.Monsters[0], player.Position + new Vec2(1, 0));

            Run(world, World.TicksPerSecond);

            Assert.IsTrue(player.IsDead);
            Assert.IsEmpty(world.Minions);
        }
    
        [Test]
        public void ThreeClassesShareTwoClassesWorthOfPassives()
        {
            Assert.AreEqual(3, ClassRules.DefaultMaxClasses);
            Assert.AreEqual(1f, ClassRules.PassiveShare(2));
            Assert.AreEqual(2f / 3, ClassRules.PassiveShare(3), 1e-6f);

            var world = Room();
            var one = Learned(world.AddPlayer("a", null, Warrior));
            var three = Learned(world.AddPlayer("b", null, Warrior, FireMage, Archer));
            float bare = Learned(world.AddPlayer("c", null)).Stats.MaxHealth;
            Assert.AreEqual(bare * 1.2f, one.Stats.MaxHealth, 0.01f, "MaxHealth+20%");
            Assert.AreEqual(bare * 1.13f, three.Stats.MaxHealth, 0.01f, "two thirds of it, rounded: 13%");
            // One skill of each, then the first class's second, until a player picks.
            CollectionAssert.AreEqual(new[] { "whirlwind", "fireball", "volley", "war_cry" }, three.Skills.Select(s => s.Id));
            Assert.AreEqual(18, ClassRules.AllSkillsFor(three.Classes).Count, "eighteen to learn and pick the four from");
        }

        [Test]
        public void SkillsCanBeReorderedAnywhereAndKeepTheirCooldowns()
        {
            var world = Room();
            var player = Learned(world.AddPlayer("a", null, Warrior, FireMage));
            CollectionAssert.AreEqual(new[] { "whirlwind", "war_cry", "fireball", "flame_brand" }, player.Skills.Select(s => s.Id));
            world.UseSkill(player.Id, Slot(player, "whirlwind"), Vec2.Zero);
            int cooling = player.CooldownTicks(0);

            // Indexes into every skill the two have: the warrior's six (whirlwind 0, war_cry 1, ...), then the fire
            // mage's (fireball 6, flame_brand 7, fire_ring 8, meteor 9, ...).
            Assert.IsTrue(world.ChangeClasses(player.Id, player.Classes, new[] { 6, 0, 7, 1 }));
            CollectionAssert.AreEqual(new[] { "fireball", "whirlwind", "flame_brand", "war_cry" }, player.Skills.Select(s => s.Id));
            CollectionAssert.AreEqual(new[] { 6, 0, 7, 1 }, player.SkillOrder);
            Assert.AreEqual(cooling, player.CooldownTicks(1), "whirlwind's cooldown went with it");
            Assert.AreEqual(0, player.CooldownTicks(0));

            // A skill not in the slots can take one: meteor in place of war_cry.
            Assert.IsTrue(world.ChangeClasses(player.Id, player.Classes, new[] { 6, 0, 7, 9 }));
            CollectionAssert.AreEqual(new[] { "fireball", "whirlwind", "flame_brand", "meteor" }, player.Skills.Select(s => s.Id));
            Assert.AreEqual(Slot(player, "meteor"), 3);
            Assert.Greater(player.CooldownTicks(3), 0, "a skill new to the slots starts cooling down");

            Assert.IsTrue(world.ChangeClasses(player.Id, player.Classes, new[] { 0, 0, 1, 2 }), "the same skill twice: the defaults");
            CollectionAssert.AreEqual(new[] { "whirlwind", "war_cry", "fireball", "flame_brand" }, player.Skills.Select(s => s.Id));
        }

        [Test]
        public void OtherClassesOnlyBesideAWorkbenchAndTheirSkillsStartCoolingDown()
        {
            var world = Room();
            var player = Learned(world.AddPlayer("a", null, Warrior));
            Assert.IsFalse(world.ChangeClasses(player.Id, new[] { FireMage }, null), "no workbench near");
            Assert.AreSame(Warrior, player.Classes.Single());

            int x = TileMap.Cell(player.Position.X) + 1;
            int y = TileMap.Cell(player.Position.Y);
            world.Map[x, y] = TileType.Workbench;
            world.UseSkill(player.Id, Slot(player, "war_cry"), Vec2.Zero);
            Assert.IsTrue(player.HasBuff(Slot(player, "war_cry")));
            Assert.IsTrue(world.ChangeClasses(player.Id, new[] { FireMage, Warrior }, null));

            // Only learned skills take slots: of the fire mage's, the free fireball; the warrior's fill the rest.
            CollectionAssert.AreEqual(new[] { "fireball", "whirlwind", "war_cry", "charge" }, player.Skills.Select(s => s.Id));
            Assert.AreEqual(Classes.Skill("fireball").CooldownTicks, player.CooldownTicks(0), "a new skill starts as if just used");
            Assert.IsTrue(player.HasBuff(2), "war cry stayed, buff and all");
            CollectionAssert.AreEqual(new[] { FireMage, Warrior }, player.Classes);
        }
}
}
