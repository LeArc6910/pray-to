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

        [UnityTest]
        public IEnumerator HostSessionRuns()
        {
            _root.StartHost();
            yield return new WaitForSecondsRealtime(Seconds);
            Assert.IsTrue(_root.Playing, "The host session never started playing. " + _root.Status);
        }
    }
}
