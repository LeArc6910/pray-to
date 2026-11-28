using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // Kinds of weapon, the second weapon and the skills that draw on their class's weapon (Docs/Weapons.md).
    public class WeaponTests
    {
        static readonly MonsterDefinition Slime = Monsters.Shipped.Find("slime");
        static readonly string[] Tiers = { "copper", "iron", "mithril", "obsidian", "void" };
        static readonly string[] Kinds = { "sword", "dagger", "greatsword", "spear", "hammer", "bow", "crossbow", "boomerang", "staff", "wand", "orb" };

        static World Room(int size = 20) =>
            new World(Maps.Room(size), TileMap.Center(size / 2, size / 2), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 });

        static ClassDefinition Class(string id) => Classes.Shipped.Classes.First(c => c.Id == id);

        static SkillDefinition Skill(string id) => Classes.Shipped.Skills.First(s => s.Id == id);

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

        static Equipment Holding(string held, string spare)
        {
            var equipment = Gear.Wearing(held);
            equipment.EquipSpare(Gear.Plain(spare));
            return equipment;
        }

        [Test]
        public void EveryWeaponHasAKindAndEveryStepHasAllElevenFromOre()
        {
            CollectionAssert.IsEmpty(Gear.Data.Errors);
            Assert.AreEqual(12, Gear.Data.WeaponKinds.Count, "eleven to fight with and the pickaxe");
            foreach (var weapon in Gear.Data.Bases.Where(b => b.Slot == ItemSlot.Weapon))
                Assert.IsTrue(Gear.Data.WeaponKinds.Contains(weapon.Kind), weapon.Id);
            foreach (string tier in Tiers)
            {
                foreach (string kind in Kinds)
                {
                    var weapon = Gear.Data.FindBase($"{tier}_{kind}");
                    Assert.AreEqual(kind == "boomerang" ? "throwing" : kind, weapon.Kind.Id);
                    Assert.IsTrue(Gear.Data.Recipes.Any(r => r.Item == weapon), weapon.Id + " is made");
                    Assert.Greater(weapon.Weight, 0, weapon.Id + " drops");
                }
            }
            Assert.AreEqual(SkillTag.None, Gear.Data.FindBase("copper_pickaxe").Kind.Branch, "a pickaxe is no class's weapon");
            Assert.AreEqual("dagger", Gear.Data.FindBase("ratking_dagger").Kind.Id);
            Assert.AreEqual("hammer", Gear.Data.FindBase("warden_maul").Kind.Id);
            Assert.AreEqual("orb", Gear.Data.FindBase("titan_cinder").Kind.Id);
        }

        // Damage a second of the basic attack at the middle roll, against one target (a thrown weapon strikes it twice).
        static float PerSecond(string id)
        {
            var stats = PlayerStats.From(Gear.Wearing(id));
            float hits = stats.Attack.Returns ? 2 : 1;
            // Gear.Wearing rolls the lowest; the middle of the range is what a kind is written for.
            var damage = Gear.Data.FindBase(id).Implicits.First(m => m.Stat == StatType.Damage && m.Kind == ModifierKind.Flat);
            return stats.Damage * (damage.Min + damage.Max) / 2f / damage.Min * stats.AttacksPerSecond * hits;
        }

        [Test]
        public void TheKindsOfAStepDealAboutTheSameASecond()
        {
            foreach (string tier in Tiers)
            {
                float sword = PerSecond($"{tier}_sword"), bow = PerSecond($"{tier}_bow"), staff = PerSecond($"{tier}_staff");
                foreach (string kind in new[] { "dagger", "greatsword", "spear", "hammer" })
                    Assert.That(PerSecond($"{tier}_{kind}") / sword, Is.InRange(0.85f, 1.12f), $"{tier} {kind}");
                foreach (string kind in new[] { "crossbow", "boomerang" })
                    Assert.That(PerSecond($"{tier}_{kind}") / bow, Is.InRange(0.85f, 1.12f), $"{tier} {kind}");
                foreach (string kind in new[] { "wand", "orb" })
                    Assert.That(PerSecond($"{tier}_{kind}") / staff, Is.InRange(0.8f, 1.15f), $"{tier} {kind}");
                // Reach costs a little: a bow or staff a little under a sword.
                Assert.That(bow / sword, Is.InRange(0.75f, 1f), tier);
            }
        }

        [Test]
        public void TheKindSetsThePaceAndTheShapeOfTheBasicAttack()
        {
            var sword = PlayerStats.From(Gear.Wearing("iron_sword"));
            Assert.AreEqual(PlayerStats.BaseAttacksPerSecond, sword.AttacksPerSecond, 1e-3);
            Assert.AreEqual(PlayerStats.BaseAttacksPerSecond * 1.6f, PlayerStats.From(Gear.Wearing("iron_dagger")).AttacksPerSecond, 1e-3);
            Assert.AreEqual(PlayerStats.BaseAttacksPerSecond * 0.5f, PlayerStats.From(Gear.Wearing("iron_hammer")).AttacksPerSecond, 1e-3);
            Assert.AreEqual(1.9f, PlayerStats.From(Gear.Wearing("iron_spear")).Attack.Reach, 1e-3);
            Assert.AreEqual(1, PlayerStats.From(Gear.Wearing("iron_crossbow")).Attack.Pierce);
            Assert.IsTrue(PlayerStats.From(Gear.Wearing("iron_boomerang")).Attack.Returns);
            Assert.AreEqual(BasicAttackKind.Bolt, PlayerStats.From(Gear.Wearing("iron_orb")).Attack.Kind);
            Assert.Greater(PlayerStats.From(Gear.Wearing("iron_orb")).Attack.Burst, 1);
        }

        [Test]
        public void TheSpareSwapsInAndTheNextAttackWaits()
        {
            var world = Room();
            var player = world.AddPlayer("a", Holding("copper_sword", "copper_bow"));
            Assert.AreEqual("copper_bow", player.Equipment.Spare.Base.Id);
            Assert.AreEqual(BasicAttackKind.Swing, player.Stats.Attack.Kind);

            Assert.IsTrue(world.SwapWeapons(player.Id));
            Assert.AreEqual("copper_bow", player.Equipment[ItemSlot.Weapon].Base.Id);
            Assert.AreEqual("copper_sword", player.Equipment.Spare.Base.Id);
            Assert.AreEqual(BasicAttackKind.Arrow, player.Stats.Attack.Kind);
            Assert.IsTrue(world.TakeEvents().Any(e => e.Type == SimEventType.Swapped && e.Source == player.Id));

            world.SetAttacking(player.Id, true);
            var events = Run(world, World.Ticks(World.SwapSeconds) - 1);
            Assert.IsFalse(events.Any(e => e.Type == SimEventType.Attack), "the swap holds the next attack back");
            events = Run(world, 2);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Attack && World.AttackKindOf(e.Amount) == BasicAttackKind.Arrow));
            Assert.AreEqual(0, World.ComboStepOf(events.First(e => e.Type == SimEventType.Attack).Amount), "the combo starts again");
        }

        [Test]
        public void ASpareGoesOnFromTheBagAndBackAndOnlyAWeapon()
        {
            var world = Room();
            var bag = new Inventory();
            var bow = Gear.Plain("copper_bow");
            var helmet = Gear.Plain("copper_helmet");
            bag.TryAdd(bow);
            bag.TryAdd(helmet);
            var player = world.AddPlayer("a", Gear.Wearing("copper_sword"), bag);
            Assert.IsFalse(world.EquipSpare(player.Id, helmet.Id));
            Assert.IsTrue(world.EquipSpare(player.Id, bow.Id));
            Assert.AreSame(bow, player.Equipment.Spare);
            Assert.IsNull(player.Inventory.Find(bow.Id));
            Assert.IsTrue(world.UnequipSpare(player.Id));
            Assert.IsNull(player.Equipment.Spare);
            Assert.AreSame(bow, player.Inventory.Find(bow.Id));
        }

        [Test]
        public void TheSparesAttackStatsStayWithItAndItsOthersCount()
        {
            // The guardian's maul gives armour as well as damage.
            var maul = Gear.Plain("warden_maul");
            var bare = PlayerStats.From(Gear.Wearing("copper_bow"));
            var equipment = Gear.Wearing("copper_bow");
            equipment.EquipSpare(maul);
            var stats = PlayerStats.From(equipment);

            Assert.Greater(stats.Armor, bare.Armor, "its armour counts while it is carried");
            Assert.AreEqual(bare.Damage, stats.Damage, 1e-3, "its damage does not");
            Assert.AreEqual(bare.AttacksPerSecond, stats.AttacksPerSecond, 1e-3, "nor its pace");
            Assert.AreEqual("hammer", stats.Spare.Kind.Id);
            Assert.Greater(stats.Spare.Damage, stats.Damage);
            Assert.AreEqual(stats.Armor, stats.Spare.Armor, 1e-3);
        }

        [Test]
        public void ASkillDrawsOnItsClasssWeaponWhereverItIs()
        {
            var warrior = Class("warrior");
            var whirlwind = Skill("whirlwind");
            var volley = Skill("volley");
            Assert.AreEqual(SkillTag.Melee, warrior.Weapon);
            Assert.AreEqual(SkillTag.Ranged, Class("archer").Weapon);
            Assert.AreEqual(SkillTag.Magic, Class("summoner").Weapon);

            var classes = new[] { warrior, Class("archer") };
            var swordInHand = PlayerStats.From(Holding("iron_sword", "iron_bow"), classes);
            var bowInHand = PlayerStats.From(Holding("iron_bow", "iron_sword"), classes);
            var context = SkillTag.Melee;
            Assert.AreEqual(swordInHand.DamageFor(context), bowInHand.ForSkill(whirlwind).DamageFor(context), 1e-3,
                "a warrior's skill hits as hard with the bow in hand as with the sword");
            Assert.AreEqual(bowInHand.DamageFor(SkillTag.Ranged), swordInHand.ForSkill(volley).DamageFor(SkillTag.Ranged), 1e-3);
            Assert.IsTrue(bowInHand.Wields(SkillTag.Melee));

            // With no melee weapon worn, the stronger of the two at OffBranchShare.
            var twoBows = PlayerStats.From(Holding("iron_bow", "copper_bow"), classes);
            Assert.IsFalse(twoBows.Wields(SkillTag.Melee));
            Assert.AreEqual(twoBows.DamageFor(context) * PlayerStats.OffBranchShare, twoBows.ForSkill(whirlwind).DamageFor(context), 1e-3);
            // A pickaxe is no warrior's weapon.
            Assert.IsFalse(PlayerStats.From(Gear.Wearing("copper_pickaxe"), classes).Wields(SkillTag.Melee));
        }

        [Test]
        public void TheArchersPaceIsForRangedAttacksOnly()
        {
            var archer = new[] { Class("archer") };
            Assert.AreEqual(PlayerStats.From(Gear.Wearing("iron_sword")).AttacksPerSecond, PlayerStats.From(Gear.Wearing("iron_sword"), archer).AttacksPerSecond, 1e-3);
            Assert.Greater(PlayerStats.From(Gear.Wearing("iron_bow"), archer).AttacksPerSecond, PlayerStats.From(Gear.Wearing("iron_bow")).AttacksPerSecond);
        }

        [Test]
        public void ACrossbowBoltGoesThroughOneAndStrikesTheNext()
        {
            var world = Room();
            var player = world.AddPlayer("a", Gear.Wearing("copper_crossbow"));
            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
            var near = world.SpawnEnemy(Slime, player.Position + new Vec2(2, 0));
            var far = world.SpawnEnemy(Slime, player.Position + new Vec2(4, 0));
            var beyond = world.SpawnEnemy(Slime, player.Position + new Vec2(6, 0));
            world.SetAttacking(player.Id, true);
            Run(world, 1);
            world.SetAttacking(player.Id, false);
            Run(world, World.TicksPerSecond);
            Assert.Less(near.Health, near.MaxHealth);
            Assert.Less(far.Health, far.MaxHealth);
            Assert.AreEqual(beyond.MaxHealth, beyond.Health, "it goes through one only");
        }

        [Test]
        public void ABoomerangStrikesOnTheWayOutAndBackAndIsCaught()
        {
            var world = Room();
            var player = world.AddPlayer("a", Gear.Wearing("copper_boomerang"));
            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
            var slime = world.SpawnEnemy(Slime, player.Position + new Vec2(2.5f, 0));
            world.SetAttacking(player.Id, true);
            Run(world, 1);
            world.SetAttacking(player.Id, false);
            var events = Run(world, World.TicksPerSecond * 2);
            Assert.AreEqual(2, events.Count(e => e.Type == SimEventType.Hit && e.Target == slime.Id), "out and back");
            Assert.AreEqual(0, world.Projectiles.Count, "back in hand");
        }

        // Each kind's shot shows as its own (BasicAttack.Look, Docs/Weapons.md 묶음 B), and the snapshot carries it.
        [TestCase("copper_bow", ShotLook.Plain)]
        [TestCase("copper_crossbow", ShotLook.Bolt)]
        [TestCase("copper_boomerang", ShotLook.Boomerang)]
        [TestCase("copper_staff", ShotLook.Plain)]
        [TestCase("copper_wand", ShotLook.Spark)]
        [TestCase("copper_orb", ShotLook.Orb)]
        public void EachKindsShotHasItsLookAndTheSnapshotCarriesIt(string weapon, ShotLook look)
        {
            var world = Room();
            var player = world.AddPlayer("a", Gear.Wearing(weapon));
            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
            world.SetAttacking(player.Id, true);
            Run(world, 1);
            Assert.AreEqual(1, world.Projectiles.Count);
            Assert.AreEqual(look, world.Projectiles[0].Look);
            var self = new SelfState(0, new Vec2(10, 10), 100, 100, 5, new List<int>(), new List<int>(), 0, -1, 0, 0, 0, -1, -1, 0, 0, -1, 0, 0);
            var shots = new List<ProjectileState> { new ProjectileState(9, new Vec2(10, 11), new Vec2(8, 0), 0.3f, true, SkillTag.Ranged, 20, look) };
            var back = SnapshotMessage.Decode(new SnapshotMessage(1, self, new List<EntityState>(), shots, new List<HazardState>(), new List<SimEvent>()).Encode());
            Assert.AreEqual(look, back.Projectiles[0].Look);
        }

        [Test]
        public void AnOrbBurstsWhereItStopsAndStrikesThoseAround()
        {
            var world = Room();
            var player = world.AddPlayer("a", Gear.Wearing("copper_orb"));
            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
            var first = world.SpawnEnemy(Slime, player.Position + new Vec2(2, 0));
            var beside = world.SpawnEnemy(Slime, player.Position + new Vec2(2.4f, 1f));
            world.SetAttacking(player.Id, true);
            Run(world, 1);
            world.SetAttacking(player.Id, false);
            Run(world, World.TicksPerSecond);
            Assert.Less(first.Health, first.MaxHealth);
            Assert.Less(beside.Health, beside.MaxHealth, "caught in the burst");
        }

        [Test]
        public void AHammersFinisherDazes()
        {
            var world = Room();
            var player = world.AddPlayer("a", Gear.Wearing("void_hammer"));
            world.MovePlayer(player.Id, player.Position, new Vec2(1, 0), 1);
            // A slime of many lives, so it outlasts the three blows.
            var slime = world.SpawnEnemy(Slime, player.Position + new Vec2(1.2f, 0));
            slime.Health = slime.MaxHealth = 1_000_000;
            world.SetAttacking(player.Id, true);
            var events = new List<SimEvent>();
            for (int i = 0; i < World.TicksPerSecond * 4 && !events.Any(e => e.Type == SimEventType.MonsterFx && e.Source == slime.Id && e.Amount == World.MonsterFxStagger); i++)
            {
                world.MovePlayer(player.Id, player.Position, (slime.Position - player.Position).Normalized, 1);
                events.AddRange(Run(world, 1));
            }
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.MonsterFx && e.Source == slime.Id && e.Amount == World.MonsterFxStagger), "dazed by the third blow");
            Assert.AreEqual(EnemyState.Stagger, slime.State);
        }

        [Test]
        public void TheSpareIsSavedAndSentAndAnOldSaveKeepsItsOneWeapon()
        {
            var equipment = Holding("iron_sword", "iron_bow");
            var saved = CharacterSave.Read(CharacterSave.Write(equipment, new Inventory()), Gear.Data);
            Assert.AreEqual("iron_sword", saved.Equipment[ItemSlot.Weapon].Base.Id);
            Assert.AreEqual("iron_bow", saved.Equipment.Spare.Base.Id);

            var old = CharacterSave.Read(CharacterSave.Write(Gear.Wearing("iron_sword"), new Inventory()), Gear.Data);
            Assert.AreEqual("iron_sword", old.Equipment[ItemSlot.Weapon].Base.Id);
            Assert.IsNull(old.Equipment.Spare);

            var sent = InventoryMessage.Decode(new InventoryMessage(equipment, new Inventory()).Encode(Gear.Data), Gear.Data);
            Assert.AreEqual("iron_sword", sent.Equipment[ItemSlot.Weapon].Base.Id);
            Assert.AreEqual("iron_bow", sent.Equipment.Spare.Base.Id);
        }
    }
}
