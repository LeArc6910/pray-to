using NUnit.Framework;

namespace Game.Core.Tests
{
    // The character's gender (2026-11-28, Docs/Gender.md): one bit over the look's 24, a save or a message from before it
    // is the man the hero was drawn as, and changing it keeps the colours and moves only a hair that was the other's start.
    public class GenderTests
    {
        [Test]
        public void TheGenderRidesInItsOwnBit()
        {
            var woman = new CharacterLook(5, 3, 2, 4, 7, 9, CharacterLook.Female);
            Assert.AreEqual(1u << 24, woman.Packed & 0xFF000000u);
            Assert.AreEqual(woman, CharacterLook.Unpack(woman.Packed));
            Assert.IsTrue(CharacterLook.Unpack(woman.Packed).IsFemale);
            Assert.AreEqual(new CharacterLook(5, 3, 2, 4, 7, 9).Packed, woman.Packed & 0xFFFFFFu);
        }

        [Test]
        public void ALookFromBeforeTheGenderIsTheManAsDrawn()
        {
            uint old = new CharacterLook(3, 7, 4, 2, 11, 5).Packed;
            Assert.Less(old, 1u << 24);
            Assert.AreEqual(CharacterLook.Male, CharacterLook.Unpack(old).Gender);
            Assert.AreEqual(CharacterLook.Male, CharacterLook.Default.Gender);
            Assert.AreEqual(CharacterLook.Default.Packed, CharacterLook.DefaultOf(CharacterLook.Male).Packed);
        }

        [Test]
        public void ChangingTheGenderKeepsTheColoursAndMovesOnlyAStartingHair()
        {
            var man = CharacterLook.Default;
            var woman = man.WithGender(CharacterLook.Female);
            Assert.AreEqual(CharacterLook.StartHair(CharacterLook.Female), woman.Hair);
            Assert.AreEqual(CharacterLook.DefaultOf(CharacterLook.Female), woman);
            Assert.AreEqual(man, woman.WithGender(CharacterLook.Male));
            // A hair picked by hand stays.
            var spiky = new CharacterLook(3, 2, 1, 0, 4, 4);
            Assert.AreEqual(3, spiky.WithGender(CharacterLook.Female).Hair);
        }

        [Test]
        public void TheDiceGiveBothGendersMostlyInHairThatSuitsThem()
        {
            var random = new GameRandom(7);
            int women = 0, suited = 0;
            const int rolls = 400;
            for (int i = 0; i < rolls; i++)
            {
                var look = CharacterLook.Random(random);
                if (look.IsFemale) women++;
                if (CharacterLook.Suits(look.Hair, look.Gender)) suited++;
            }
            Assert.That(women, Is.InRange(rolls * 0.4, rolls * 0.6));
            Assert.Greater(suited, rolls * 0.7);
        }

        [Test]
        public void ASaveKeepsTheGender()
        {
            var woman = new CharacterLook(6, 4, 3, 2, 1, 0, CharacterLook.Female);
            var save = CharacterSave.Read(CharacterSave.Write(Gear.Wearing("copper_sword"), new Inventory(), 0, look: woman), Gear.Data);
            Assert.AreEqual(woman, save.Look);
            Assert.AreEqual(21, save.Version);
        }
    }
}
