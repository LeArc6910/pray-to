using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class ClassTests
    {
        static readonly ClassDefinition Warrior = Classes.Shipped.Find("warrior");
        static readonly ClassDefinition FireMage = Classes.Shipped.Find("fire_mage");
        static readonly MonsterDefinition Slime = Monsters.Shipped.Find("slime");
        // Never notices anyone and takes a beating, so targets stay where they were put.
        static readonly MonsterDefinition Dummy = MonsterSheet.Parse(Monsters.Header + "dummy,허수아비,1,1000,0,0,1,0.4,0,0.5,0.5,1,1\n").Find("dummy");

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

        static void AssertError(ClassData data, params string[] parts) =>
            Assert.IsTrue(
                data.Errors.Any(error => parts.All(error.Contains)),
                $"No error mentions {string.Join(" + ", parts)}. Errors:\n{string.Join("\n", data.Errors)}");

        [Test]
        public void ShippedSheetsLoad()
        {
            Assert.IsTrue(Classes.Shipped.Success, string.Join("\n", Classes.Shipped.Errors));
            CollectionAssert.AreEqual(new[] { "whirlwind", "war_cry" }, Warrior.Skills.Select(s => s.Id));
            CollectionAssert.AreEqual(new[] { "fireball", "flame_brand" }, FireMage.Skills.Select(s => s.Id));
        }

        [Test]
        public void SkillsDescribeTheirNumbers()
        {
            Assert.AreEqual("피해 150% · 둘레 1.8칸 · 재사용 4초", SkillText.Numbers(Classes.Skill("whirlwind")));
            Assert.AreEqual("피해 120% · 사거리 10칸 · 폭발 1.2칸 · 재사용 1.5초", SkillText.Numbers(Classes.Skill("fireball")));
            Assert.AreEqual("6초 동안 근접 피해 30% 증가, 공격 속도 15% 증가 · 재사용 15초", SkillText.Numbers(Classes.Skill("war_cry")));
            Assert.AreEqual("8초 동안 모든 공격에 화염 태그 · 재사용 16초", SkillText.Numbers(Classes.Skill("flame_brand")));
            Assert.AreEqual("최대 생명력 20% 증가, 근접 피해 10% 증가", SkillText.Passive(Warrior));
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
                "lost,길 잃은,nobody,nova,,50,2,1,,,,,,\n");

            AssertError(data, "classes.csv 3행", "범위로 적을 수 없습니다");
            AssertError(data, "skills.csv 2행", "projectile 스킬은 speed 칸을 채워야 합니다");
            AssertError(data, "skills.csv 3행", "modifiers나 grants");
            AssertError(data, "skills.csv 6행", "'busy'의 스킬이 2개를 넘습니다");
            AssertError(data, "skills.csv 7행", "클래스 'nobody'가 classes.csv에 없습니다");
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

            var warriorFirst = world.AddPlayer("a", null, Warrior, FireMage);
            var mageFirst = world.AddPlayer("b", null, FireMage, Warrior);
            var classless = world.AddPlayer("c", null);

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
            var player = world.AddPlayer("a", Gear.Wearing("copper_sword"), Warrior);
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
            var player = world.AddPlayer("a", null, Warrior);
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
            var player = world.AddPlayer("a", Gear.Wearing("copper_staff"), FireMage);
            var friend = world.AddPlayer("b", null);
            Assert.IsTrue(world.MovePlayer(friend.Id, friend.Position + new Vec2(2, 0), new Vec2(1, 0), 10));
            var aimedAt = player.Position + new Vec2(5, 0);
            var target = world.SpawnEnemy(Dummy, aimedAt);
            var beside = world.SpawnEnemy(Dummy, player.Position + new Vec2(5, 1));
            var behind = world.SpawnEnemy(Dummy, player.Position + new Vec2(8, 0));

            Assert.IsTrue(world.UseSkill(player.Id, Slot(player, "fireball"), new Vec2(1, 0)));
            var events = Run(world, World.TicksPerSecond);

            // The staff's lowest roll is 2, raised by its own magic 10% and the fire mage's fire 15%: 2 × 125% × 120%
            // = 3, or 4.5 on a crit. Everything in the burst takes the same roll.
            int dealt = Dummy.Health - (int)target.Health;
            Assert.That(dealt, Is.EqualTo(3).Or.EqualTo(4));
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
            var player = world.AddPlayer("a", null, FireMage);
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
            var player = world.AddPlayer("a", Gear.Wearing("copper_sword"), Warrior);
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
            var player = world.AddPlayer("a", null, Warrior);
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
            var player = world.AddPlayer("a", null, FireMage);
            Assert.AreEqual(BossState.Dormant, boss.State);

            world.UseSkill(player.Id, Slot(player, "fireball"), new Vec2(0, 1));
            Run(world, World.TicksPerSecond);

            Assert.AreNotEqual(BossState.Dormant, boss.State);
            Assert.Less(boss.Health, boss.MaxHealth);
        }
    }
}
