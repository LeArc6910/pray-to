using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // The biomes' looked-at things on the floor (Decor, Docs/BiomeRework.md 4-3 꾸밈 층, 묶음 B0): the same seed and tile
    // give the same thing on every client, nothing stands next to another, the density is the low proof-set one, and a
    // road, a door, a built thing or a laid floor keeps its tile and the ones round it bare.
    public class DecorTests
    {
        [Test]
        public void TheSameSeedAndTileGiveTheSameThing()
        {
            int same = 0, picked = 0;
            for (int y = -40; y < 120; y++)
            {
                for (int x = -40; x < 120; x++)
                {
                    var a = Decor.At(7, Biome.Mushroom, x, y);
                    var b = Decor.At(7, Biome.Mushroom, x, y);
                    Assert.AreEqual(a.Kind, b.Kind);
                    Assert.AreEqual(a.Variant, b.Variant);
                    Assert.AreEqual(a.Flip, b.Flip);
                    Assert.AreEqual(a.OffsetX, b.OffsetX);
                    Assert.AreEqual(a.OffsetY, b.OffsetY);
                    if (!a.Any) continue;
                    picked++;
                    if (Decor.At(8, Biome.Mushroom, x, y).Any) same++;
                }
            }
            Assert.Greater(picked, 0);
            Assert.Less(same, picked / 3, "another seed lays its things elsewhere");
        }

        [TestCase(1UL)]
        [TestCase(42UL)]
        public void ThingsKeepATileApartAtTheLowDensity(ulong seed)
        {
            const int size = 200;
            int count = 0;
            var kinds = new HashSet<DecorKind>();
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var pick = Decor.At(seed, Biome.Cave, x, y);
                    if (!pick.Any) continue;
                    count++;
                    kinds.Add(pick.Kind);
                    Assert.LessOrEqual(Math.Abs((int)pick.OffsetX), Decor.Shift);
                    Assert.LessOrEqual(Math.Abs((int)pick.OffsetY), Decor.Lift);
                    Assert.Less(pick.Variant, Decor.Variants);
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            if ((dx != 0 || dy != 0) && Decor.At(seed, Biome.Cave, x + dx, y + dy).Any)
                                Assert.Fail($"two things side by side at {x},{y} and {x + dx},{y + dy}");
                }
            }
            float perHundred = count * 100f / (size * size);
            Assert.That(perHundred, Is.InRange(1.5f, 5f), "B0's low density: a few in a hundred tiles");
            Assert.AreEqual(2, kinds.Count, "the cave's two kinds both show");
        }

        [Test]
        public void EveryBiomeHasItsOwnKindsAndNoneIsNothing()
        {
            foreach (Biome biome in Enum.GetValues(typeof(Biome)))
            {
                var kinds = new HashSet<DecorKind>();
                for (int roll = 0; roll < 100; roll++) kinds.Add(Decor.KindOf(biome, roll));
                Assert.IsFalse(kinds.Contains(DecorKind.None), biome.ToString());
                Assert.GreaterOrEqual(kinds.Count, 2, biome.ToString());
            }
        }

        [Test]
        public void RoadsDoorsAndBuiltThingsKeepTheirSurroundingsBare()
        {
            var map = new TileMap(30, 30, TileType.Floor);
            for (int i = 0; i < 30; i++)
            {
                map[i, 0] = TileType.Wall;
                map[0, i] = TileType.Wall;
            }
            map[10, 10] = TileType.Road;
            map[20, 20] = TileType.Door;
            map[5, 20] = TileType.Workbench;
            map[20, 5] = TileType.StoneFloor;
            map[25, 25] = TileType.Water;

            Assert.IsTrue(Decor.Clear(map, 15, 15), "open floor");
            Assert.IsTrue(Decor.Clear(map, 1, 1), "beside the cave's rock");
            Assert.IsFalse(Decor.Clear(map, 0, 5), "rock itself");
            Assert.IsFalse(Decor.Clear(map, 12, 10), "two tiles off a road");
            Assert.IsTrue(Decor.Clear(map, 13, 10), "three tiles off a road");
            Assert.IsFalse(Decor.Clear(map, 20, 22), "before a door");
            Assert.IsFalse(Decor.Clear(map, 6, 21), "beside something built");
            Assert.IsFalse(Decor.Clear(map, 21, 5), "beside a laid floor");
            Assert.IsFalse(Decor.Clear(map, 24, 24), "beside water");
            Assert.IsFalse(Decor.Clear(map, 5, 20), "on something built");
        }
    }
}
