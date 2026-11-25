using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Client.Tests
{
    // The title and its pages (Docs/TitleRework.md, 묶음 T3) drawn in the real client, a picture of each: the title over
    // its drifting cave, the character list, the making page, the world list and the new world page (on a PC and a
    // phone), the loading screen, and the hub in the same look with [나가기]'s question. Any error logged while they draw
    // fails the test, as in the other scenes.
    public class TitleScreenTests
    {
        const string Name = "playmode-title-test";
        const float Settle = 0.8f;

        GameRoot _root;

        [SetUp]
        public void CreateRoot()
        {
            Assert.IsNotNull(GameSheets.Load(), "GameSheets asset is missing. Run Game > Set Up Project first.");
            TestSaves.Forget(Name);
            _root = new GameObject("Game").AddComponent<GameRoot>();
            _root.PlayerName = Name;
            _root.WorldNameField = Name;
        }

        [UnityTearDown]
        public IEnumerator DestroyRoot()
        {
            Controls.ForceTouch(false);
            _root.Leave();
            Object.Destroy(_root.gameObject);
            yield return null;
            yield return null;
            TestSaves.Forget(Name);
        }

        IEnumerator Page(string page, string picture)
        {
            Assert.IsTrue(_root.OpenTitle(page), $"The title's page {page} opens.");
            yield return new WaitForSecondsRealtime(Settle);
            Assert.AreEqual(page == "make" ? "makecharacter" : page == "newworld" ? "makeworld" : page, _root.TitlePage);
            yield return Screens.Take(picture);
        }

        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheTitleAndItsPagesDraw()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.AreEqual("main", _root.TitlePage);
            yield return Screens.Take("title");
            foreach (bool touch in new[] { false, true })
            {
                Controls.ForceTouch(touch);
                string tag = touch ? "-touch" : "";
                if (touch)
                {
                    yield return Page("main", "title-touch");
                }
                yield return Page("characters", "title-characters" + tag);
                yield return Page("make", "title-make" + tag);
                yield return Page("worlds", "title-worlds" + tag);
                yield return Page("newworld", "title-newworld" + tag);
                yield return Page("settings", "title-settings" + tag);
            }
            Controls.ForceTouch(false);
            Assert.IsTrue(_root.OpenTitle("main"));
        }

        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheLoadingScreenAndTheHubDraw()
        {
            _root.StartSolo();
            Assert.IsTrue(_root.Loading, "A solo start makes its world behind the loading screen. " + _root.Status);
            yield return null;
            yield return null;
            if (_root.Loading)
            {
                Assert.IsNotEmpty(_root.LoadTip, "The loading screen has a tip.");
                Assert.AreEqual("Cave", _root.LoadBiome, "A new character has only been to the dirt cave.");
                yield return Screens.Take("title-loading");
            }
            yield return TestSaves.UntilPlaying(_root, 1);
            Assert.IsTrue(_root.Playing, "The solo session never started playing. " + _root.Status);
            _root.ShowHub(true);
            yield return new WaitForSecondsRealtime(Settle);
            yield return Screens.Take("title-hub");
            _root.AskLeave();
            yield return new WaitForSecondsRealtime(Settle);
            Assert.IsTrue(_root.LeaveAsked);
            yield return Screens.Take("title-hub-ask");
            Controls.ForceTouch(true);
            yield return new WaitForSecondsRealtime(Settle);
            yield return Screens.Take("title-hub-ask-touch");
            Controls.ForceTouch(false);
            _root.ShowHub(false);
            Assert.IsFalse(_root.LeaveAsked, "Shutting the hub lets the question go.");
        }
    }
}
