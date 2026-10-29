using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Client.Tests
{
    // Plays the real client for a few seconds. Unity's test framework fails a test when anything logs an error or
    // throws, so these catch what the compile check cannot: missing assets, null references in the view, Netcode
    // setup problems. The local setup script runs them after Game > Set Up Project has made the assets.
    public class SessionTests
    {
        const float Seconds = 5;

        GameRoot _root;

        [SetUp]
        public void CreateRoot()
        {
            Assert.IsNotNull(GameSheets.Load(), "GameSheets asset is missing. Run Game > Set Up Project first.");
            _root = new GameObject("Game").AddComponent<GameRoot>();
        }

        [UnityTearDown]
        public IEnumerator DestroyRoot()
        {
            Object.Destroy(_root.gameObject);
            // Let Netcode shut down before the next test starts a host on the same port.
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SoloSessionRuns()
        {
            _root.StartSolo();
            yield return new WaitForSecondsRealtime(Seconds);
            Assert.IsTrue(_root.Playing, "The solo session never started playing. " + _root.Status);
            Assert.Greater(Object.FindObjectsByType<SpriteRenderer>().Length, 0, "Nothing was drawn.");
        }

        // The menu starts with two classes picked, so there are four skills: a nova, a buff, a projectile and a
        // buff that adds fire, which together draw every kind of skill effect.
        [UnityTest]
        public IEnumerator EverySkillCanBeUsed()
        {
            _root.StartSolo();
            yield return new WaitForSecondsRealtime(1);
            Assert.IsTrue(_root.Playing, "The solo session never started playing. " + _root.Status);
            Assert.AreEqual(4, _root.SkillCount, "Expected the two starting classes' four skills.");
            for (int slot = 0; slot < _root.SkillCount; slot++)
            {
                Assert.IsTrue(_root.UseSkill(slot), $"The skill in slot {slot} was not ready.");
                yield return new WaitForSecondsRealtime(0.5f);
            }
            yield return new WaitForSecondsRealtime(Seconds);
            Assert.IsTrue(_root.Playing);
        }

        // Leaving saves the world and the character where SaveStore keeps them, and starting again reads them back.
        [UnityTest]
        public IEnumerator SoloSavesAndLoads()
        {
            const string name = "playmode-save-test";
            SaveStore.Delete(SaveStore.WorldKey(name));
            SaveStore.Delete(SaveStore.CharacterKey(name));
            _root.PlayerName = name;
            _root.WorldNameField = name;
            try
            {
                _root.StartSolo();
                yield return new WaitForSecondsRealtime(2);
                Assert.IsTrue(_root.Playing, "The solo session never started playing. " + _root.Status);
                _root.Leave();
                Assert.IsTrue(SaveStore.Exists(SaveStore.WorldKey(name)), "no world save");
                Assert.IsTrue(SaveStore.Exists(SaveStore.CharacterKey(name)), "no character save");

                _root.StartSolo();
                yield return new WaitForSecondsRealtime(2);
                Assert.IsTrue(_root.Playing, "The saved world did not load. " + _root.Status);
                _root.Leave();
            }
            finally
            {
                SaveStore.Delete(SaveStore.WorldKey(name));
                SaveStore.Delete(SaveStore.CharacterKey(name));
            }
        }

        [UnityTest]
        public IEnumerator HostSessionRuns()
        {
            _root.StartHost();
            yield return new WaitForSecondsRealtime(Seconds);
            Assert.IsTrue(_root.Playing, "The host session never started playing. " + _root.Status);
        }
    }
}
