using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Client.Tests
{
    // Saves and settings are files under persistentDataPath (DeviceFiles), and what an older build left in
    // PlayerPrefs moves into them the first time it is asked for (Docs/Saving.md).
    public class DeviceStorageTests
    {
        const string Save = "world:저장 시험";
        const string Number = "test.prefs.number";
        const string Text = "test.prefs.text";

        [TearDown]
        public void CleanUp()
        {
            SaveStore.Delete(Save);
            Prefs.Delete(Number);
            Prefs.Delete(Text);
        }

        [Test]
        public void SavesAreFilesAndAnOldPlayerPrefsSaveMovesIntoOne()
        {
            SaveStore.Write(Save, new byte[] { 1, 2, 3 });
            Assert.IsTrue(File.Exists(SaveStore.PathOf(Save)));
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, SaveStore.Read(Save));
            SaveStore.Delete(Save);
            Assert.IsFalse(SaveStore.Exists(Save));

            PlayerPrefs.SetString(SaveStore.OldKey(Save), System.Convert.ToBase64String(new byte[] { 4, 5 }));
            Assert.IsTrue(SaveStore.Exists(Save), "the old save is found");
            Assert.IsFalse(PlayerPrefs.HasKey(SaveStore.OldKey(Save)), "and leaves PlayerPrefs");
            CollectionAssert.AreEqual(new byte[] { 4, 5 }, SaveStore.Read(Save));

            // Deleting also forgets a copy still in PlayerPrefs, so it cannot come back.
            PlayerPrefs.SetString(SaveStore.OldKey(Save), System.Convert.ToBase64String(new byte[] { 6 }));
            SaveStore.Delete(Save);
            Assert.IsFalse(SaveStore.Exists(Save));
        }

        [Test]
        public void SettingsAreAFileAndOldPlayerPrefsValuesMoveIntoIt()
        {
            PlayerPrefs.SetInt(Number, 3);
            Assert.AreEqual(3, Prefs.Get(Number, 0));
            Assert.IsFalse(PlayerPrefs.HasKey(Number));

            // Line breaks, tabs and backslashes survive the file.
            const string awkward = "a\tb\nc\\d\\n";
            Prefs.Set(Text, awkward);
            Prefs.Set(Number, 7);
            Prefs.Reload();
            Assert.AreEqual(awkward, Prefs.Get(Text, ""));
            Assert.AreEqual(7, Prefs.Get(Number, 0));

            Prefs.Delete(Text);
            Prefs.Reload();
            Assert.IsFalse(Prefs.Has(Text));
            Assert.AreEqual("none", Prefs.Get(Text, "none"));
        }
    }
}
