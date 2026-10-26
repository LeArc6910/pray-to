using System;
using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class GameRandomTests
    {
        [Test]
        public void MatchesThePcg32ReferenceOutput()
        {
            // pcg32-demo from the reference C implementation: seed 42, stream 54.
            var random = new GameRandom(42, 54);
            uint[] expected = { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };
            foreach (uint value in expected) Assert.AreEqual(value, random.NextUInt());
        }

        [Test]
        public void BoundedRollsMatchTheReference()
        {
            // pcg32_boundedrand_r(rng, 6) from the reference C implementation, freshly seeded with 42 and 54.
            var random = new GameRandom(42, 54);
            int[] rolls = Enumerable.Range(0, 6).Select(_ => random.Next(6)).ToArray();
            CollectionAssert.AreEqual(new[] { 3, 3, 2, 1, 1, 4 }, rolls);
        }

        [Test]
        public void SameSeedAndStreamRepeatAndOtherStreamsDiffer()
        {
            uint[] Take(GameRandom random) => Enumerable.Range(0, 8).Select(_ => random.NextUInt()).ToArray();

            CollectionAssert.AreEqual(Take(new GameRandom(7, 3)), Take(new GameRandom(7, 3)));
            CollectionAssert.AreNotEqual(Take(new GameRandom(7, 3)), Take(new GameRandom(7, 4)));
            CollectionAssert.AreNotEqual(Take(new GameRandom(7, 3)), Take(new GameRandom(8, 3)));
        }

        [Test]
        public void RangesStayInBoundsAndReachEveryValue()
        {
            var random = new GameRandom(1);
            var seen = Enumerable.Range(0, 2000).Select(_ => random.Next(-3, 4)).Distinct().OrderBy(v => v);
            CollectionAssert.AreEqual(Enumerable.Range(-3, 7), seen);

            for (int i = 0; i < 1000; i++)
            {
                float value = random.NextFloat();
                Assert.That(value, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }

            // Widest possible range: must not overflow.
            Assert.DoesNotThrow(() => random.Next(int.MinValue, int.MaxValue));
        }

        [Test]
        public void ChanceRespectsTheEdges()
        {
            var random = new GameRandom(5);
            for (int i = 0; i < 1000; i++)
            {
                Assert.IsFalse(random.Chance(0f));
                Assert.IsTrue(random.Chance(1f));
            }
        }

        [Test]
        public void PickWeightedFollowsTheWeights()
        {
            var random = new GameRandom(3);
            string[] items = { "never", "once", "thrice" };
            int[] weights = { 0, 1, 3 };
            int Weight(string item) => weights[Array.IndexOf(items, item)];

            var counts = new int[items.Length];
            for (int i = 0; i < 4000; i++) counts[Array.IndexOf(items, random.PickWeighted(items, Weight))]++;

            Assert.AreEqual(0, counts[0]);
            Assert.That(counts[2] / (double)counts[1], Is.InRange(2.6, 3.4));
            Assert.Throws<ArgumentException>(() => random.PickWeighted(items, _ => 0));
        }

        [Test]
        public void RejectsEmptyRanges()
        {
            var random = new GameRandom(1);
            Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(5, 5));
        }
    }
}
