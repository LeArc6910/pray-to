using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // What one hit takes off a player (Docs/Difficulty.md, 2026-11-25): every monster's, elite's and boss attack's share
    // of the health of a character of its band, bare, in the band below's gear and in its own, held to the targets the
    // owner set. DevTools/CoreSandbox --hits prints the same table; a number that makes the game soft again fails here.
    public class DifficultyTests
    {
        static readonly System.Collections.Generic.IReadOnlyList<HitRow> Table = HitTable.Build(Gear.Data, Monsters.Shipped, Bosses.Shipped);

        [Test]
        public void EveryHitMeetsItsTarget()
        {
            var missed = Table.Where(r => !r.Meets)
                .Select(r => $"{r.Band} {r.Who} {r.What} {r.Kind}: bare {r.Bare:P0}, below {r.Below:P0}, geared {r.Geared:P0}")
                .ToList();
            Assert.IsEmpty(missed, string.Join("\n", missed));
        }

        [Test]
        public void TheTableHoldsTheOwnersNumbers()
        {
            float Mean(HitKind kind, Func<HitRow, float> of) => Table.Where(r => r.Checked && r.Kind == kind).Average(of);
            // Ordinary monsters: a fifth and more bare, about 5% in the band's gear, two to three times that a band behind.
            Assert.That(Mean(HitKind.Normal, r => r.Geared), Is.InRange(0.05f, 0.07f));
            Assert.That(Table.Where(r => r.Checked && r.Kind == HitKind.Normal).Min(r => r.Bare), Is.GreaterThanOrEqualTo(0.2f));
            // Elites and L bodies: 35~40% bare in the first bands, about 10% in gear.
            foreach (var kind in new[] { HitKind.Elite, HitKind.Large })
            {
                Assert.That(Mean(kind, r => r.Geared), Is.InRange(0.09f, 0.12f), kind.ToString());
                Assert.That(Table.Where(r => r.Checked && r.Kind == kind && r.Band <= 2).Average(r => r.Bare), Is.InRange(0.35f, 0.47f), kind.ToString());
            }
            // XL: 15~20% in gear, at least half bare.
            Assert.That(Mean(HitKind.Huge, r => r.Geared), Is.InRange(0.15f, 0.20f));
            Assert.That(Table.Where(r => r.Checked && r.Kind == HitKind.Huge).Min(r => r.Bare), Is.GreaterThanOrEqualTo(0.5f));
            // Bosses: a quick blow about 40%, a big slow one most of the health, a finishing blow 90~95%.
            Assert.That(Mean(HitKind.Quick, r => r.Geared), Is.InRange(0.37f, 0.43f));
            Assert.That(Mean(HitKind.Heavy, r => r.Geared), Is.InRange(0.70f, 0.90f));
            Assert.That(Mean(HitKind.Ultimate, r => r.Geared), Is.InRange(0.86f, 0.95f));
            Assert.IsTrue(Table.Where(r => r.Kind == HitKind.Ultimate).All(r => r.Geared < 1), "a full health bar lives through a finishing blow");
            Assert.IsTrue(Table.Where(r => r.Kind == HitKind.Ultimate && r.Band > 1).All(r => r.Below > 1.1f), "a band behind, it kills outright");
            // Every boss has its finishing blow, and the black knight's blows are the slow heavy kind.
            foreach (var boss in Bosses.Shipped.Bosses)
                Assert.IsTrue(boss.Attacks.Any(a => DamageBands.KindOf(a) == HitKind.Ultimate), boss.Id);
            Assert.IsTrue(Bosses.Shipped.Find("black_knight").Attacks.All(a => DamageBands.KindOf(a) is HitKind.Heavy or HitKind.Ultimate));
            // The samurai is quick: its ordinary blows about 40%.
            Assert.That(Table.Where(r => r.Who == Bosses.Shipped.Find("shrine_samurai").Name && r.Kind == HitKind.Quick).Average(r => r.Geared), Is.InRange(0.37f, 0.43f));
        }

        [Test]
        public void TheGateBetweenBandsIsTwoToThreeTimes()
        {
            foreach (var row in Table.Where(r => r.Checked && r.Band > 1 && r.Kind != HitKind.Ultimate))
                Assert.That(row.Gate, Is.InRange(DamageBands.GateLow, DamageBands.GateHigh), $"{row.Who} {row.What}");
            // A band's ordinary monsters, a band behind: about 12~15% a hit.
            Assert.That(Table.Where(r => r.Checked && r.Kind == HitKind.Normal && r.Band > 1).Average(r => r.Below), Is.InRange(0.12f, 0.16f));
        }

        [Test]
        public void EachKindOfHitHasItsOwnCurve()
        {
            // At the band's own set every kind but a finishing blow lets the same share through, so the band's column holds.
            foreach (var kind in new[] { HitKind.Normal, HitKind.Elite, HitKind.Large, HitKind.Huge, HitKind.Quick, HitKind.Heavy, HitKind.Roar })
                Assert.AreEqual(1 / (1 + DamageBands.GearGuard), DamageBands.Share(DamageBands.ArmorOf(2), 2, kind), 1e-5f, kind.ToString());
            Assert.AreEqual(0.75f, DamageBands.Share(DamageBands.ArmorOf(2), 2, HitKind.Ultimate), 1e-5f);
            // Floors: no armor ever shuts out an elite's, a big body's, a boss's or a finishing blow below its floor.
            float lots = DamageBands.ArmorOf(2) * 50;
            Assert.Less(DamageBands.Share(lots, 2, HitKind.Normal), 0.01f);
            Assert.AreEqual(0.10f, DamageBands.Share(lots, 2, HitKind.Elite), 0.01f);
            Assert.AreEqual(0.14f, DamageBands.Share(lots, 2, HitKind.Huge), 0.01f);
            Assert.AreEqual(0.18f, DamageBands.Share(lots, 2, HitKind.Slow), 0.01f);
            Assert.AreEqual(0.60f, DamageBands.Share(lots, 2, HitKind.Ultimate), 0.01f);
            // Short of the band's armor a boss's blow gives way faster than an ordinary monster's; past it, holds less.
            float half = DamageBands.ArmorOf(2) / 2, twice = DamageBands.ArmorOf(2) * 2;
            Assert.Greater(DamageBands.Share(half, 2, HitKind.Quick), DamageBands.Share(half, 2, HitKind.Normal));
            Assert.Greater(DamageBands.Share(twice, 2, HitKind.Quick), DamageBands.Share(twice, 2, HitKind.Elite));
            Assert.Greater(DamageBands.Share(twice, 2, HitKind.Elite), DamageBands.Share(twice, 2, HitKind.Normal));
            // A finishing blow is made against health: a tank's health holds it, its own gear barely.
            foreach (var row in Table.Where(r => r.Kind == HitKind.Ultimate))
            {
                Assert.That(row.Tank, Is.InRange(0.45f, 0.65f), $"{row.Who} {row.What}");
                Assert.Less(row.Above, row.Geared);
            }
            // A band ahead, an ordinary monster barely scratches; a boss's quick blow still does.
            Assert.Less(Table.Where(r => r.Checked && r.Kind == HitKind.Normal && r.Band < DamageBands.Bands).Max(r => r.Above), 0.03f);
            Assert.Greater(Table.Where(r => r.Checked && r.Kind == HitKind.Quick && r.Band < DamageBands.Bands).Min(r => r.Above), 0.15f);
        }

        [Test]
        public void ArmorIsWeighedAgainstTheBand()
        {
            for (int band = 1; band <= DamageBands.Bands; band++)
            {
                float armor = DamageBands.ArmorOf(band);
                Assert.AreEqual(1, DamageBands.Share(0, band), 1e-6f);
                Assert.AreEqual(1 / (1 + DamageBands.GearGuard), DamageBands.Share(armor, band), 1e-6f, "the band's set");
                Assert.That(DamageBands.Share(armor / 2, band) / DamageBands.Share(armor, band), Is.InRange(2f, 2.3f), "half of it lets twice through");
                Assert.Greater(DamageBands.Share(armor, band), DamageBands.Share(armor * 1.1f, band));
                if (band > 1) Assert.Greater(DamageBands.Share(armor, band), DamageBands.Share(armor, band - 1), "the same armor holds less against a later band");
            }
            Assert.AreEqual(1, DamageBands.Taken(0.2f, 0, 1), "at least 1");
            Assert.AreEqual(1, DamageBands.BandOfLevel(9));
            Assert.AreEqual(2, DamageBands.BandOfLevel(10));
            Assert.AreEqual(5, DamageBands.BandOfLevel(48));
        }

        [Test]
        public void TheBandsNumbersAreTheSheets()
        {
            for (int band = 1; band <= DamageBands.Bands; band++)
            {
                var set = PlayerStats.From(HitTable.Wearing(Gear.Data, band), level: DamageBands.LevelOf(band));
                Assert.AreEqual(DamageBands.ArmorOf(band), set.Armor, 0.01f, $"band {band} armor");
                Assert.AreEqual(DamageBands.HealthOf(band), set.MaxHealth, 0.01f, $"band {band} health");
            }
            // Every shipped monster names its band; a gone column would quietly put the mid-bosses in copper.
            var lines = File.ReadAllLines(RepoFile.Find("Assets/_Project/Data/Monsters/" + MonsterSheet.File)).Where(l => l.Length > 0).ToList();
            int column = Array.IndexOf(lines[0].Split(','), "band");
            Assert.GreaterOrEqual(column, 0);
            foreach (string line in lines.Skip(1)) Assert.IsNotEmpty(line.Split(',')[column], line);
            Assert.AreEqual(5, Monsters.Shipped.Find("silk_spiderling").Band);
            Assert.AreEqual(4, Monsters.Shipped.Find("gate_warden").Band);
            Assert.AreEqual(2, Monsters.Shipped.Find("demon_soldier").Band);
            Assert.AreEqual(DamageBands.BandOf(Biome.Lava), Monsters.Shipped.Find("magma_slime").Band);
        }

        [Test]
        public void APlayersArmorHoldsAgainstItsOwnBandsMonstersOnly()
        {
            var world = new World(Maps.Room(14), TileMap.Center(7, 7), Monsters.Shipped, 1, new WorldConfig { EnemyTarget = 0 }, Gear.Data);
            var player = world.AddPlayer("a", HitTable.Wearing(Gear.Data, 1));
            var slime = world.SpawnEnemy(Monsters.Shipped.Find("slime"), player.Position + new Vec2(5, 0));
            var wisp = world.SpawnEnemy(Monsters.Shipped.Find("void_wisp"), player.Position + new Vec2(-5, 0));
            float health = player.Health;
            int fromSlime = world.Hurt(player, slime, 30);
            int fromWisp = world.Hurt(player, wisp, 30);
            Assert.AreEqual(DamageBands.Taken(30, player.Stats.Armor, 1), fromSlime);
            Assert.AreEqual(DamageBands.Taken(30, player.Stats.Armor, 4), fromWisp);
            Assert.Greater(fromWisp, fromSlime * 3, "copper armor holds the slime off, not the abyss");
            Assert.AreEqual(health - fromSlime - fromWisp, player.Health, 0.01f);
        }

        [Test]
        public void ARiftsMonstersAreOfTheLastBand()
        {
            // Whatever band a kind comes from, a rift brings its hit up to the void band's, more each tier.
            var rows = Table.Where(r => r.Group == "균열").ToList();
            Assert.AreEqual(World.RiftKinds(Monsters.Shipped.Monsters).Count, rows.Count);
            Assert.IsTrue(rows.All(r => r.Band == DamageBands.Bands && r.Meets));
            var wisp = Monsters.Shipped.Find("void_wisp");
            Assert.AreEqual(World.RiftPowerAt(1, wisp) * 2, World.RiftPowerAt(5, wisp), 0.001f, "twice as hard by the fifth tier");
        }
    }

    // What a boss's attack takes off the band's own set: the sheet's damage through the curve of its kind.
    static class BossHits
    {
        public static float Takes(BossDefinition boss, BossAttack attack)
        {
            int band = DamageBands.BandOfLevel(boss.Level);
            return DamageBands.Through(attack.Damage, DamageBands.ArmorOf(band), band, DamageBands.KindOf(attack));
        }
    }
}
