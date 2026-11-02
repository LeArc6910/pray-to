using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // Saves carried off a device in one file and back (SaveBundle, SaveKeys; Docs/Saving.md 내보내기와 가져오기).
    public class SaveBundleTests
    {
        static byte[] Character() => CharacterSave.Write(new Equipment(), new Inventory(), 120);

        static byte[] World() =>
            new WorldHost(5, Monsters.Shipped, Gear.Data, null, new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped)
                .SaveWorld(1000);

        static byte[] Map() => MapSave.Write(new[] { true, false, false, true }, 2, 2);

        static List<(string Key, byte[] Data)> Saves() => new List<(string, byte[])>
        {
            (SaveKeys.Character("모험가"), Character()),
            (SaveKeys.Map(5, "모험가"), Map()),
            (SaveKeys.World("내 월드"), World()),
        };

        [Test]
        public void KeysSayWhatTheyName()
        {
            Assert.IsTrue(SaveKeys.TryParse(SaveKeys.Character("모험가"), out var kind, out string name));
            Assert.AreEqual((SaveKind.Character, "모험가"), (kind, name));
            Assert.IsTrue(SaveKeys.TryParse(SaveKeys.World("a:b"), out kind, out name));
            Assert.AreEqual((SaveKind.World, "a:b"), (kind, name));
            // A map belongs to the character after the seed, colons in the name and all.
            Assert.IsTrue(SaveKeys.TryParse(SaveKeys.Map(18446744073709551615, "x:y"), out kind, out name));
            Assert.AreEqual((SaveKind.Map, "x:y"), (kind, name));

            foreach (string bad in new[] { null, "", "character:", "world:", "map:12:", "map:12", "map:-1:x", "map::x", "save.6162", "World:x" })
                Assert.IsFalse(SaveKeys.TryParse(bad, out _, out _), bad ?? "null");
        }

        [Test]
        public void EachSaveBeginsWithTheMarkOfItsKind()
        {
            Assert.AreEqual(SaveKeys.MagicOf(SaveKind.Character), Encoding.ASCII.GetString(Character(), 0, 4));
            Assert.AreEqual(SaveKeys.MagicOf(SaveKind.World), Encoding.ASCII.GetString(World(), 0, 4));
            Assert.AreEqual(SaveKeys.MagicOf(SaveKind.Map), Encoding.ASCII.GetString(Map(), 0, 4));
        }

        [Test]
        public void SavesComeBackOutAsTheyWentIn()
        {
            var saves = Saves();
            var back = SaveBundle.Read(SaveBundle.Write(saves));

            Assert.AreEqual(saves.Select(s => s.Key), back.Select(s => s.Key));
            for (int i = 0; i < saves.Count; i++) CollectionAssert.AreEqual(saves[i].Data, back[i].Data, saves[i].Key);
            Assert.AreEqual(120, CharacterSave.Read(back[0].Data, Gear.Data).Experience);
            Assert.IsEmpty(SaveBundle.Read(SaveBundle.Write(new (string, byte[])[0])));
        }

        [Test]
        public void OnlySavesGoIn()
        {
            Assert.Throws<ArgumentException>(() => SaveBundle.Write(new[] { ("settings.txt", new byte[] { 1 }) }));
            Assert.Throws<ArgumentException>(() => SaveBundle.Write(new[] { (SaveKeys.World("w"), new byte[0]) }));
        }

        [Test]
        public void AnythingElseIsRefusedWithAReason()
        {
            byte[] good = SaveBundle.Write(Saves());

            Refused(null, "저장 파일이 아닙니다");
            Refused(Encoding.UTF8.GetBytes("<!DOCTYPE html>"), "저장 파일이 아닙니다");
            Refused(Character(), "저장 파일이 아닙니다");
            // Cut anywhere, it says so.
            foreach (int length in new[] { 4, 8, 13, good.Length / 2, good.Length - 1 })
                Assert.Throws<InvalidDataException>(() => SaveBundle.Read(good.Take(length).ToArray()), $"cut at {length}");
            Refused(good.Take(good.Length - 1).ToArray(), "끊겼습니다");

            // From a newer game.
            byte[] newer = (byte[])good.Clone();
            newer[4] = 2;
            Refused(newer, "더 새 게임");

            // A world's data under a character's key.
            Refused(Bundle(w =>
            {
                w.Write(1);
                w.Write(SaveKeys.Character("모험가"));
                byte[] world = World();
                w.Write(world.Length);
                w.Write(world);
            }), "캐릭터 '모험가' 저장본이 깨졌습니다");

            // A key that is not a save's, which could name some other file.
            Refused(Bundle(w =>
            {
                w.Write(1);
                w.Write("../settings.txt");
                w.Write(1);
                w.Write((byte)0);
            }), "모르는 것");

            // The same save twice.
            Refused(Bundle(w =>
            {
                w.Write(2);
                for (int i = 0; i < 2; i++)
                {
                    w.Write(SaveKeys.Map(5, "a"));
                    byte[] map = Map();
                    w.Write(map.Length);
                    w.Write(map);
                }
            }), "두 번");

            // More than it says it holds.
            Refused(good.Concat(new byte[] { 0 }).ToArray(), "깨졌습니다");
            // A count no file could hold.
            Refused(Bundle(w => w.Write(int.MaxValue)), "깨졌습니다");
        }

        static byte[] Bundle(Action<BinaryWriter> rest)
        {
            using var stream = new MemoryStream();
            using (var w = new BinaryWriter(stream, new UTF8Encoding(false)))
            {
                w.Write(Encoding.ASCII.GetBytes("PTSB"));
                w.Write(1);
                rest(w);
            }
            return stream.ToArray();
        }

        static void Refused(byte[] file, string reason)
        {
            var e = Assert.Throws<InvalidDataException>(() => SaveBundle.Read(file));
            StringAssert.Contains(reason, e.Message);
        }

        [Test]
        public void MessagesNameTheSave()
        {
            Assert.AreEqual("캐릭터 '모험가'", SaveBundle.Describe(SaveKeys.Character("모험가")));
            Assert.AreEqual("월드 '내 월드'", SaveBundle.Describe(SaveKeys.World("내 월드")));
            Assert.AreEqual("'모험가'의 지도", SaveBundle.Describe(SaveKeys.Map(5, "모험가")));
        }
    }
}
