using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The shipped gear catalogue (Docs/Gear.md): hunting sets, elite uniques, boss sets, and how they line up.
    public class GearCatalogTests
    {
        static readonly LootData Data = Gear.Data;
        static readonly string[] Bands = { "copper", "iron", "mithril", "obsidian", "void" };

        // The middle of the flat stats that make a piece good: a weapon's damage, armour's armour and health, a ring's health.
        static float MainOf(ItemBase b) => b.Implicits.Where(m => m.Kind == ModifierKind.Flat && m.Tags == SkillTag.None
                && (m.Stat == StatType.Damage || m.Stat == StatType.Armor || m.Stat == StatType.MaxHealth))
            .Sum(m => (m.Min + m.Max) / 2f);

        static string Piece(ItemBase b) => b.Slot == ItemSlot.Weapon
            ? (b.Tags & SkillTag.Ranged) != 0 ? "bow" : (b.Tags & SkillTag.Magic) != 0 ? "staff" : "sword"
            : b.Slot == ItemSlot.Chest ? "armor" : b.Slot.ToString().ToLowerInvariant();

        static ItemBase Ore(int band, string piece) => Data.FindBase($"{Bands[band - 1]}_{piece}");

        static int BandOf(ItemBase b) => b.Level >= 45 ? 5 : DamageBands.BandOfLevel(b.Level);

        [Test]
        public void TheShippedSheetsReadCleanAndNameRealMonstersAndClasses()
        {
            CollectionAssert.IsEmpty(Data.Errors);
            var monsters = new HashSet<string>(Monsters.Shipped.Monsters.Select(m => m.Id));
            foreach (var material in Data.Materials)
            {
                foreach (var (monster, _) in material.Drops)
                    Assert.IsTrue(monster == World.RiftTrophy || monsters.Contains(monster), $"{material.Id} drops from '{monster}'");
            }
            var classes = new HashSet<string>(Classes.Shipped.Classes.Select(c => c.Id));
            foreach (var b in Data.Bases.Where(b => b.ClassId.Length > 0)) Assert.IsTrue(classes.Contains(b.ClassId), b.Id);
            foreach (var bonus in Data.Sets.SelectMany(s => s.AllBonuses).Where(b => b.ClassId.Length > 0)) Assert.IsTrue(classes.Contains(bonus.ClassId), bonus.ClassId);
            var bosses = new HashSet<string>(Bosses.Shipped.Bosses.Select(b => b.Id));
            foreach (var b in Data.Bases.Where(b => b.IsLegendary && !b.IsEliteUnique && b.Legendary != "rift")) Assert.IsTrue(bosses.Contains(b.Legendary), b.Id);
        }

        [Test]
        public void EveryBandHasTwoCraftedSixPieceSetsWithAClassAndEveryBossAThreePieceSet()
        {
            for (int band = 1; band <= 5; band++)
            {
                var crafted = Data.Sets.Where(s => Data.Bases.Any(b => b.Set == s && !b.IsLegendary && BandOf(b) == band)).ToList();
                Assert.AreEqual(2, crafted.Count, $"band {band}");
                foreach (var set in crafted)
                {
                    var pieces = Data.Bases.Where(b => b.Set == set).ToList();
                    Assert.AreEqual(6, pieces.Select(p => p.Slot).Distinct().Count(), set.Id);
                    Assert.AreEqual(6, set.MostPieces);
                    Assert.IsTrue(set.AllBonuses.Any(b => b.ClassId.Length > 0), set.Id + " has a class bonus");
                    Assert.IsTrue(set.AllBonuses.Any(b => b.Effects.Count > 0), set.Id + " has an effect");
                    foreach (var piece in pieces) Assert.IsTrue(Data.Recipes.Any(r => r.Item == piece), piece.Id + " has a recipe");
                }
                Assert.AreEqual(4, Data.Bases.Count(b => b.IsEliteUnique && BandOf(b) == band), $"band {band} elites");
            }
            Assert.IsTrue(Data.Bases.Where(b => b.IsEliteUnique).All(b => b.Effects.Count > 0));
            foreach (var boss in Bosses.Shipped.Bosses)
            {
                var pieces = Data.Bases.Where(b => b.Legendary == boss.Id).ToList();
                Assert.AreEqual(3, pieces.Count, boss.Id);
                Assert.IsTrue(pieces.All(p => p.Set != null && p.Set == pieces[0].Set), boss.Id + " pieces form one set");
                Assert.IsTrue(pieces[0].Set.AllBonuses.Any(b => b.Effects.Count > 0), boss.Id);
            }
        }

        [Test]
        // Ore < crafted < elite in a band; a boss's piece a little over the next band's ore at +5.
        public void GearLinesUpOreCraftedEliteBoss()
        {
            foreach (var b in Data.Bases.Where(b => b.Set != null && !b.IsLegendary || b.IsEliteUnique))
            {
                var ore = Ore(BandOf(b), Piece(b));
                if (MainOf(ore) == 0) continue;
                float ratio = MainOf(b) / MainOf(ore);
                if (b.IsEliteUnique) Assert.That(ratio, Is.InRange(1.2f, 1.45f), b.Id);
                else Assert.That(ratio, Is.InRange(1.05f, 1.3f), b.Id);
            }
            foreach (var boss in Bosses.Shipped.Bosses)
            {
                int band = DamageBands.BandOfLevel(boss.Level);
                foreach (var b in Data.Bases.Where(b => b.Legendary == boss.Id))
                {
                    var next = Ore(System.Math.Min(5, band + 1), Piece(b));
                    var main = b.Implicits.Where(m => m.Kind == ModifierKind.Flat && m.Tags == SkillTag.None && (m.Stat == StatType.Damage || m.Stat == StatType.Armor)).ToList();
                    var oreMain = next.Implicits.Where(m => main.Any(x => x.Stat == m.Stat) && m.Kind == ModifierKind.Flat && m.Tags == SkillTag.None).ToList();
                    if (main.Count == 0 || oreMain.Count == 0) continue;
                    float upgraded = oreMain.Sum(m => (m.Min + m.Max) / 2f) * (1 + Item.MaxUpgrade * Item.UpgradePercent / 100f);
                    float mine = main.Where(m => oreMain.Any(o => o.Stat == m.Stat)).Sum(m => (m.Min + m.Max) / 2f);
                    Assert.That(mine / upgraded, Is.InRange(0.97f, 1.15f), $"{b.Id} against {next.Id} +5");
                }
            }
        }

        [Test]
        public void ABeetleDropsItsShellNowAndThen()
        {
            var beetle = Monsters.Shipped.Find("cave_beetle");
            var shell = Data.FindMaterial("beetle_shell");
            var world = new World(Maps.Room(14), TileMap.Center(7, 7), Monsters.Shipped, 3, new WorldConfig { EnemyTarget = 0 }, Data);
            var player = world.AddPlayer("a", Gear.Wearing("void_sword", "void_armor", "void_helmet"));
            int dropped = 0;
            for (int i = 0; i < 60; i++)
            {
                int before = world.GroundLoot.Count(l => l.Material == shell);
                var enemy = world.SpawnEnemy(beetle, player.Position + new Vec2(2, 0));
                world.Hurt(enemy, player, 100000);
                dropped += world.GroundLoot.Count(l => l.Material == shell) - before;
            }
            Assert.That(dropped, Is.InRange(5, 28), "about a quarter of 60");
        }

        [Test]
        public void AnElitesUniqueIsCalledUnique()
        {
            var unique = Data.Bases.First(b => b.IsEliteUnique);
            var item = new LootGenerator(Data).RollLegendary(unique, new GameRandom(1));
            Assert.IsTrue(ItemText.Describe(item)[0].StartsWith("[고유]"));
            Assert.IsTrue(ItemText.Describe(item).Any(l => l.Contains("★")));
        }
    }
}
