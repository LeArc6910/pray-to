using System;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The stepped growth curve (Docs/Bosses.md, 계단식 성장): each tier of gear and each tenth level is a clear jump,
    // and a boss tuned for one tier is a wall for the tier below. These read the shipped sheets, so a number that
    // flattens the stairs fails here.
    public class GrowthCurveTests
    {
        static readonly string[] Tiers = { "copper", "iron", "mithril", "obsidian", "void" };
        static readonly string[] Slots = { "sword", "armor", "helmet", "boots", "ring", "amulet" };

        // The tier of gear a boss is met in: its level's tens (a level 23 boss is a mithril boss), copper below 10.
        static int TierOf(BossDefinition boss) => Math.Min(Tiers.Length - 1, boss.Level / 10);

        static PlayerStats Wearing(int tier, int level) =>
            PlayerStats.From(Gear.Wearing(Slots.Select(s => $"{Tiers[tier]}_{s}").ToArray()), level: level);

        // Sword hits to bring the boss down, through its armour.
        static float HitsToKill(BossDefinition boss, PlayerStats stats) => boss.Health * (100 + boss.Armor) / 100f / stats.Damage;

        // The share of the character's health the boss's hardest blow takes, through their armour weighed against the
        // boss's band (DamageBands).
        static float WorstHit(BossDefinition boss, PlayerStats stats) =>
            DamageBands.Taken(boss.Attacks.Max(a => a.Damage), stats.Armor, DamageBands.BandOfLevel(boss.Level)) / stats.MaxHealth;

        [Test]
        public void EveryLevelAddsHealthAndDamageAndEveryTenthIsAStep()
        {
            Assert.AreEqual(0, Leveling.HealthAt(1));
            Assert.AreEqual(0, Leveling.DamageAt(1));
            for (int level = 2; level <= Leveling.MaxLevel; level++)
            {
                int health = Leveling.HealthAt(level) - Leveling.HealthAt(level - 1);
                int damage = Leveling.DamageAt(level) - Leveling.DamageAt(level - 1);
                bool step = level % Leveling.StepLevels == 0;
                Assert.AreEqual(Leveling.HealthPerLevel + (step ? Leveling.StepHealth : 0), health, $"health at level {level}");
                Assert.AreEqual(Leveling.DamagePerLevel + (step ? Leveling.StepDamage : 0), damage, $"damage at level {level}");
            }
            // A step is worth several ordinary levels, or it is not felt.
            Assert.GreaterOrEqual(Leveling.StepHealth, 5 * Leveling.HealthPerLevel);
            Assert.GreaterOrEqual(Leveling.StepDamage, 5 * Leveling.DamagePerLevel);
            Assert.AreEqual(10, Leveling.NextStep(1));
            Assert.AreEqual(20, Leveling.NextStep(10));
            Assert.AreEqual(50, Leveling.NextStep(49));
            Assert.AreEqual(0, Leveling.NextStep(50));
            Assert.AreEqual("레벨 성장 생명력 +57 · 피해 +19% · 다음 계단 Lv 20", Leveling.Summary(10));
            Assert.AreEqual("레벨 성장 생명력 +297 · 피해 +99%", Leveling.Summary(50));
        }

        [Test]
        public void TheLevelShowsInTheCharactersNumbers()
        {
            var sword = Gear.Wearing("copper_sword");
            var first = PlayerStats.From(sword, level: 1);
            var ninth = PlayerStats.From(sword, level: 9);
            var tenth = PlayerStats.From(sword, level: 10);
            Assert.AreEqual(PlayerStats.BaseHealth, first.MaxHealth, 0.01f);
            Assert.AreEqual(PlayerStats.BaseHealth + 8 * Leveling.HealthPerLevel, ninth.MaxHealth, 0.01f);
            Assert.AreEqual(ninth.MaxHealth + Leveling.HealthPerLevel + Leveling.StepHealth, tenth.MaxHealth, 0.01f, "the step at level 10");
            Assert.AreEqual(first.Damage * 1.08f, ninth.Damage, 0.01f);
            Assert.AreEqual(first.Damage * 1.19f, tenth.Damage, 0.01f, "the step at level 10");
            // Skills grow with it too: they are worked out from the same damage.
            Assert.AreEqual(first.DamageFor(SkillTag.Magic | SkillTag.Fire) * 1.19f, tenth.DamageFor(SkillTag.Magic | SkillTag.Fire), 0.01f);
            // A character at the top level hits about twice as hard and has about four times the health of a new one, before gear.
            var top = PlayerStats.From(sword, level: Leveling.MaxLevel);
            Assert.That(top.Damage / first.Damage, Is.InRange(1.9f, 2.2f));
            Assert.That(top.MaxHealth / first.MaxHealth, Is.InRange(3.5f, 4.5f));
        }

        [Test]
        public void EachTierOfGearIsAboutTwiceTheOneBelow()
        {
            for (int tier = 1; tier < Tiers.Length; tier++)
            {
                var below = Wearing(tier - 1, 1);
                var above = Wearing(tier, 1);
                Assert.That(above.Damage / below.Damage, Is.InRange(1.9f, 2.8f), $"{Tiers[tier]} damage over {Tiers[tier - 1]}");
                Assert.That(above.Armor / below.Armor, Is.InRange(1.7f, 2.3f), $"{Tiers[tier]} armour over {Tiers[tier - 1]}");
                float gearHealth = above.MaxHealth - PlayerStats.BaseHealth, gearHealthBelow = below.MaxHealth - PlayerStats.BaseHealth;
                Assert.That(gearHealth / gearHealthBelow, Is.InRange(1.7f, 2.4f), $"{Tiers[tier]} health over {Tiers[tier - 1]}");
            }
        }

        [Test]
        public void ALegendaryIsNeverBelowItsTiersOrdinaryGear()
        {
            foreach (var legendary in Gear.Data.Bases.Where(b => b.Legendary.Length > 0 && b.Slot == ItemSlot.Weapon))
            {
                // The ordinary weapon of the same kind (melee, ranged, magic) at or below its level.
                var kind = legendary.Tags & (SkillTag.Melee | SkillTag.Ranged | SkillTag.Magic);
                var ordinary = Gear.Data.Bases
                    .Where(b => b.Legendary.Length == 0 && b.Weight == 100 && b.Slot == ItemSlot.Weapon && b.Level <= legendary.Level && (b.Tags & kind) == kind)
                    .OrderByDescending(b => b.Level).First();
                int Damage(ItemBase b) => b.Implicits.First(m => m.Stat == StatType.Damage && m.Kind == ModifierKind.Flat).Min;
                Assert.GreaterOrEqual(Damage(legendary), Damage(ordinary), $"{legendary.Id} against {ordinary.Id}");
            }
        }

        [Test]
        public void BossesGrowWithTheirLevel()
        {
            var ladder = Bosses.Shipped.Bosses.OrderBy(b => b.Level).ToList();
            for (int i = 1; i < ladder.Count; i++)
            {
                Assert.GreaterOrEqual(ladder[i].Health, ladder[i - 1].Health * 0.9f, $"{ladder[i].Id} after {ladder[i - 1].Id}");
            }
            // The first boss of each tier has well over twice the health of the first of the tier below.
            var firsts = ladder.GroupBy(TierOf).OrderBy(g => g.Key).Select(g => g.First()).ToList();
            Assert.AreEqual(Tiers.Length, firsts.Count, "a boss in every tier");
            for (int i = 1; i < firsts.Count; i++)
            {
                Assert.Greater(firsts[i].Health, firsts[i - 1].Health * 2f, $"{firsts[i].Id} after {firsts[i - 1].Id}");
            }
        }

        [Test]
        public void ABossIsAWallForTheTierBelow()
        {
            foreach (var boss in Bosses.Shipped.Bosses)
            {
                int tier = TierOf(boss);
                if (tier == 0) continue; // The first boss is met in the first gear: there is nothing below it.
                // Dressed for it, a level over the boss; and a tier down, not yet at the tier's first level.
                var ready = Wearing(tier, boss.Level + 1);
                var behind = Wearing(tier - 1, tier * 10 - 1);

                float slower = HitsToKill(boss, behind) / HitsToKill(boss, ready);
                Assert.Greater(slower, 2f, $"{boss.Id}: the tier below takes over twice as long ({slower:0.00})");
                // Its finishing blow (2026-11-25, Docs/Difficulty.md): dressed for it, nearly all of the health but not quite,
                // so a full health bar lives through one; a tier down, well past all of it.
                // The sets at the middle of their rolls (HitTable.Wearing): the lowest rolls of the tier do not live through it.
                var middle = PlayerStats.From(HitTable.Wearing(Gear.Data, tier + 1), level: boss.Level + 1);
                var middleBehind = PlayerStats.From(HitTable.Wearing(Gear.Data, tier), level: tier * 10 - 1);
                Assert.That(WorstHit(boss, middle), Is.InRange(0.85f, 0.96f), boss.Id);
                Assert.Greater(WorstHit(boss, ready), 0.96f, $"{boss.Id}: the tier's lowest rolls do not");
                Assert.Greater(WorstHit(boss, middleBehind), 1.6f, $"{boss.Id}: a tier down its finishing blow kills outright");
            }
        }

        [Test]
        public void TheBiggestBossesHealthStillFitsInASnapshot()
        {
            // Small numbers go exactly, as before.
            foreach (float health in new[] { 0f, 0.2f, 1, 690, 32767 })
            {
                Assert.AreEqual(MathF.Ceiling(health), Protocol.ReadHealth(Protocol.Health(health)), $"{health}");
            }
            // The last boss with four players, and far more: within a step of 64, never less than the truth, never
            // dead while alive.
            var last = Bosses.Shipped.Bosses.OrderByDescending(b => b.Health).First();
            float four = last.Health * (1 + 3 * last.HealthPerPlayer / 100f);
            Assert.Greater(four, ushort.MaxValue, "the case two bytes of whole points could not hold");
            foreach (float health in new[] { 32768f, 65535, 82000, four, four - 1, 1_000_000 })
            {
                float read = Protocol.ReadHealth(Protocol.Health(health));
                Assert.That(read - health, Is.InRange(0f, Protocol.CoarseHealthStep), $"{health}");
            }
        }

        [Test]
        public void ABiomesMonstersGrowWithTheGearThatReachesThem()
        {
            float Health(string id) => Monsters.Shipped.Find(id).Health;
            // Iron's crystal cave, mithril's lava, obsidian's abyss: each step stands longer.
            Assert.Greater(Health("magma_slime"), Health("crystal_bat") * 1.5f);
            Assert.Greater(Health("abyss_stalker"), Health("magma_slime") * 1.8f);
            // And hits harder: the usual hit of the ordinary (XS~M) monsters of each band, on average, well over the band
            // below's (they are made against the band's health, DamageBands).
            float Usual(int band) => Monsters.Shipped.Monsters.Where(m => m.Band == band && !m.Big)
                .Average(m => m.Damage * DamageBands.UsualPercent(m) / 100f);
            for (int band = 2; band <= 4; band++)
                Assert.Greater(Usual(band), Usual(band - 1) * 1.3f, $"band {band}");
        }
    }
}
