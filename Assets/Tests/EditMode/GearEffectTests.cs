using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The gear effect frame (Docs/Gear.md): effects and class bonuses written in the sheets, set steps with effects,
    // and what each effect does in a fight.
    public class GearEffectTests
    {
        const string Header = "id,name,slot,level,tags,implicit,sockets,weight,tool,legendary,chance,require,set,effects,class,class_bonus,tint\n";

        static readonly LootData Data = LootSheets.Parse(Header +
                "heal_blade,피의 검,weapon,1,melee,Damage+10,0,0,,,,,,kill_heal:20,warrior,MaxHealth+50\n" +
                "ward_boots,보호 장화,boots,1,,Armor+1,0,0,,,,,,roll_ward:30/2/5,,,#FF8800\n" +
                "thorn_mail,가시 갑옷,chest,1,,Armor+1,0,0,,,,,,thorns:50\n" +
                "rage_ring,분노 반지,ring,1,,MaxHealth+1,0,0,,,,,,low_health:50/100/40\n" +
                "first_amulet,첫 타 목걸이,amulet,1,,Damage+1%,0,0,,,,,,first_strike:100\n" +
                "set_helm,세트 투구,helmet,1,,Armor+1,0,0,,,,,trio\n" +
                "set_chest,세트 갑옷,chest,1,,Armor+1,0,0,,,,,trio\n" +
                "set_boots,세트 장화,boots,1,,Armor+1,0,0,,,,,trio\n",
            TestLoot.Affixes, TestLoot.Runes, TestLoot.RunewordsHeader, "", "", "",
            "id,name,pieces,modifiers,effects,class,class_bonus\n" +
            "trio,삼총사,2,Armor+5,,,\n" +
            "trio,,3,,kill_mana:7,archer,CritChance+10\n");

        static readonly ClassDefinition Warrior = Classes.Shipped.Find("warrior");
        static readonly ClassDefinition Archer = Classes.Shipped.Find("archer");

        static Item Piece(string id)
        {
            var itemBase = Data.FindBase(id);
            return new Item(itemBase, 1, Rarity.Legendary, itemBase.Implicits.Select(r => new Modifier(r.Stat, r.Kind, r.Min, r.Tags)).ToList(), new ItemAffix[0], 0);
        }

        static Equipment Wearing(params string[] ids)
        {
            var equipment = new Equipment();
            foreach (var id in ids) equipment.Equip(Piece(id));
            return equipment;
        }

        static World Room() => new World(Maps.Room(14), TileMap.Center(7, 7), Monsters.Brute, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data);

        [Test]
        public void TheSheetsReadEffectsClassBonusesAndTints()
        {
            CollectionAssert.IsEmpty(Data.Errors);
            var blade = Data.FindBase("heal_blade");
            Assert.AreEqual(GearEffectKind.KillHeal, blade.Effects.Single().Kind);
            Assert.AreEqual("warrior", blade.ClassId);
            Assert.AreEqual("#FF8800", Data.FindBase("ward_boots").Tint);
            var ward = Data.FindBase("ward_boots").Effects.Single();
            Assert.AreEqual((30f, 2f, 5f), (ward.A, ward.B, ward.C));
            var set = Data.Sets.Single();
            Assert.AreEqual(3, set.MostPieces);
            Assert.AreEqual("archer", set.AllBonuses[1].ClassId);
        }

        [Test]
        public void BadEffectsAndLoneClassBonusesAreErrors()
        {
            var data = LootSheets.Parse(Header +
                "a,가,ring,1,,MaxHealth+1,0,0,,,,,,heal_all:5\n" +
                "b,나,ring,1,,MaxHealth+1,0,0,,,,,,roll_ward:5\n" +
                "c,다,ring,1,,MaxHealth+1,0,0,,,,,,,,MaxHealth+5\n" +
                "d,라,ring,1,,MaxHealth+1,0,0,,,,,,,,,red\n",
                TestLoot.Affixes, TestLoot.Runes, TestLoot.Runewords, "", "", "");
            TestLoot.AssertError(data, "heal_all");
            TestLoot.AssertError(data, "roll_ward", "3개");
            TestLoot.AssertError(data, "class_bonus");
            TestLoot.AssertError(data, "tint");
        }

        [Test]
        public void AClassBonusCountsOnlyForItsClass()
        {
            var gear = Wearing("heal_blade");
            float plain = PlayerStats.From(gear).MaxHealth;
            float archer = PlayerStats.From(gear, new[] { Archer }).MaxHealth;
            float warrior = PlayerStats.From(gear, new[] { Warrior }).MaxHealth;
            Assert.AreEqual(plain, PlayerStats.From(Wearing(), new[] { Archer }).MaxHealth + 0, 0.01f, "the blade gives an archer no health");
            Assert.Greater(warrior, PlayerStats.From(Wearing(), new[] { Warrior }).MaxHealth + 49, "a warrior gets the blade's +50");
            Assert.AreEqual(archer, PlayerStats.From(Wearing(), new[] { Archer }).MaxHealth, 0.01f);
            Assert.IsTrue(ItemText.Describe(Piece("heal_blade"), classes: new[] { "warrior" }).Any(l => l.Contains("전사 보너스") && l.Contains("(적용)")));
        }

        [Test]
        public void ASetsStepsGiveStatsEffectsAndTheirClassBonus()
        {
            var two = Wearing("set_helm", "set_chest");
            var three = Wearing("set_helm", "set_chest", "set_boots");
            Assert.AreEqual(PlayerStats.From(Wearing("set_helm")).Armor + 1 + 5, PlayerStats.From(two).Armor, 0.01f, "two: the piece's 1 and the set's 5");
            Assert.IsFalse(PlayerStats.From(two).Effects.Any);
            Assert.AreEqual(7, PlayerStats.From(three).Effects.KillMana, 0.01f, "three: mana for kills");
            Assert.AreEqual(PlayerStats.From(three).CritChance + 10, PlayerStats.From(three, new[] { Archer }).CritChance, 0.01f, "an archer's +10 crit");
            var lines = ItemText.Describe(three[ItemSlot.Helmet], worn: two);
            Assert.IsTrue(lines.Any(l => l.Contains("삼총사 (2/3)")), string.Join("\n", lines));
            Assert.IsTrue(lines.Any(l => l.Contains("2개") && l.Contains("(적용)")));
            Assert.IsTrue(lines.Any(l => l.Contains("3개") && !l.Contains("(적용)") && l.Contains("마나 7")));
        }

        [Test]
        public void AKillHealsAShareOfHealth()
        {
            var world = Room();
            var player = world.AddPlayer("a", Wearing("heal_blade"));
            player.Health = 10;
            var enemy = world.SpawnEnemy(Monsters.Dummy, player.Position + new Vec2(1, 0));
            world.Hurt(enemy, player, 5000);
            Assert.IsTrue(enemy.IsDead);
            Assert.AreEqual(10 + player.Stats.MaxHealth * 0.2f, player.Health, 0.01f);
        }

        [Test]
        public void ARollRaisesAWardThatTakesBlowsAndWaitsToComeAgain()
        {
            var world = Room();
            var player = world.AddPlayer("a", Wearing("ward_boots"));
            var brute = world.SpawnEnemy(Monsters.Dummy, player.Position + new Vec2(5, 0));
            Assert.IsTrue(world.Roll(player.Id, new Vec2(0, 1)));
            float ward = player.Stats.MaxHealth * 0.3f;
            Assert.AreEqual(ward, player.Ward, 0.01f);
            for (int i = 0; i < World.RollTicks + 4; i++) world.Step();
            float before = player.Health;
            world.Hurt(player, brute, 1);
            Assert.AreEqual(before, player.Health, 0.01f, "the ward takes a small blow");
            Assert.Less(player.Ward, ward);
            for (int i = 0; i < World.Ticks(2); i++) world.Step();
            Assert.AreEqual(0, player.Ward, 0.01f, "it fades after its time");
            for (int i = 0; i < World.RollCooldownTicks + 2; i++) world.Step();
            Assert.IsTrue(world.Roll(player.Id, new Vec2(0, 1)));
            Assert.AreEqual(0, player.Ward, 0.01f, "not again within five seconds");
        }

        [Test]
        public void ThornsGiveBackAShareOfTheBlow()
        {
            var world = Room();
            var player = world.AddPlayer("a", Wearing("thorn_mail"));
            var brute = world.SpawnEnemy(Monsters.Brute.Monsters[0], player.Position + new Vec2(5, 0));
            float health = brute.Health;
            int taken = world.Hurt(player, brute, 40);
            Assert.Greater(taken, 0);
            Assert.AreEqual(health - MathF(taken * 0.5f), brute.Health, 1.01f);
        }

        static float MathF(float value) => (float)System.Math.Round(value);

        [Test]
        public void LowHealthHitsHarderAndTakesLess()
        {
            var full = Room();
            var calm = full.AddPlayer("a", Wearing("rage_ring"));
            int calmTaken = full.Hurt(calm, full.SpawnEnemy(Monsters.Brute.Monsters[0], calm.Position + new Vec2(5, 0)), 20);

            var low = Room();
            var angry = low.AddPlayer("a", Wearing("rage_ring"));
            angry.Health = angry.Stats.MaxHealth * 0.4f;
            int angryTaken = low.Hurt(angry, low.SpawnEnemy(Monsters.Brute.Monsters[0], angry.Position + new Vec2(5, 0)), 20);
            Assert.AreEqual(calmTaken * 0.6f, angryTaken, 1.01f, "40% less under half health");

            var dummy = low.SpawnEnemy(Monsters.Dummy, angry.Position + new Vec2(-2, 0));
            low.Hurt(dummy, angry, 100);
            var other = full.SpawnEnemy(Monsters.Dummy, calm.Position + new Vec2(-2, 0));
            full.Hurt(other, calm, 100);
            Assert.AreEqual((other.MaxHealth - other.Health) * 2, dummy.MaxHealth - dummy.Health, 2.01f, "twice the damage under half health");
        }

        [Test]
        public void TheFirstBlowOnAnUnhurtTargetHitsHarder()
        {
            var world = Room();
            var player = world.AddPlayer("a", Wearing("first_amulet"));
            var dummy = world.SpawnEnemy(Monsters.Dummy, player.Position + new Vec2(2, 0));
            world.Hurt(dummy, player, 100);
            float first = dummy.MaxHealth - dummy.Health;
            float before = dummy.Health;
            world.Hurt(dummy, player, 100);
            float second = before - dummy.Health;
            Assert.AreEqual(second * 2, first, 2.01f);
        }
    }
}
