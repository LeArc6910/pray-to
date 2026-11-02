using System.IO;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Client.Tests
{
    // The saves on this device listed, carried off in one file and brought back (SaveStore.List, SaveTransfer;
    // Docs/Saving.md 내보내기와 가져오기). Off the web a file goes into SaveTransfer.Folder, here a folder of its own.
    public class SaveTransferTests
    {
        const string Hero = "옮길 시험:캐릭터";
        const string Other = "옮길 시험 다른";
        const string Home = "옮길 시험 월드";
        string _folder;

        static readonly string[] Keys =
        {
            SaveKeys.Character(Hero), SaveKeys.Map(7, Hero), SaveKeys.Map(8, Hero), SaveKeys.Character(Other), SaveKeys.Map(7, Other),
            SaveKeys.World(Home),
        };

        [SetUp]
        public void PointTheFolderAside()
        {
            _folder = Path.Combine(Application.temporaryCachePath, "save-transfer-test");
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
            SaveTransfer.Folder = _folder;
        }

        [TearDown]
        public void CleanUp()
        {
            foreach (string key in Keys) SaveStore.Delete(key);
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
            SaveTransfer.Folder = null;
        }

        static void WriteAll()
        {
            SaveStore.Write(SaveKeys.Character(Hero), CharacterSave.Write(new Equipment(), new Inventory(), 40));
            SaveStore.Write(SaveKeys.Map(7, Hero), MapSave.Write(new[] { true, false }, 2, 1));
            SaveStore.Write(SaveKeys.Map(8, Hero), MapSave.Write(new[] { false, true }, 2, 1));
            SaveStore.Write(SaveKeys.Character(Other), CharacterSave.Write(new Equipment(), new Inventory()));
            SaveStore.Write(SaveKeys.Map(7, Other), MapSave.Write(new[] { true, true }, 2, 1));
            // Not a real world; only what a bundle checks, the mark at its start.
            SaveStore.Write(SaveKeys.World(Home), System.Text.Encoding.ASCII.GetBytes("PTWO and the rest"));
        }

        [Test]
        public void TheSavesOnTheDeviceAreListedByName()
        {
            WriteAll();
            var listed = SaveStore.List();
            CollectionAssert.IsSubsetOf(Keys, listed);

            SaveStore.Delete(SaveKeys.Character(Other));
            CollectionAssert.DoesNotContain(SaveStore.List(), SaveKeys.Character(Other));
        }

        [Test]
        public void ACharacterGoesWithItsMapsOnly()
        {
            WriteAll();
            CollectionAssert.AreEquivalent(
                new[] { SaveKeys.Character(Hero), SaveKeys.Map(7, Hero), SaveKeys.Map(8, Hero) },
                SaveTransfer.CharacterSaves(Hero, SaveStore.List()));
        }

        [Test]
        public void SavesCarriedOffComeBackAsTheyWere()
        {
            WriteAll();
            var heroSaves = SaveTransfer.CharacterSaves(Hero, SaveStore.List());
            var before = heroSaves.ToDictionary(key => key, SaveStore.Read);

            string message = SaveTransfer.Deliver(SaveTransfer.FileName("캐릭터 " + Hero), SaveTransfer.Pack(heroSaves));
            var files = SaveTransfer.FilesInFolder();
            Assert.AreEqual(1, files.Count, message);
            // The colon a file name cannot hold is gone; the Korean stays.
            Assert.AreEqual("pray-to 캐릭터 옮길 시험_캐릭터.ptsave", Path.GetFileName(files[0]));
            StringAssert.Contains(Path.GetFileName(files[0]), message);

            foreach (string key in heroSaves) SaveStore.Delete(key);
            var saves = SaveBundle.Read(File.ReadAllBytes(files[0]));
            Assert.AreEqual("캐릭터 '옮길 시험:캐릭터', 지도 2개", SaveTransfer.Describe(saves.Select(save => save.Key)));
            SaveTransfer.Bring(saves);
            foreach (string key in heroSaves) CollectionAssert.AreEqual(before[key], SaveStore.Read(key), key);
        }
    }
}
