using System.Collections;
using System.Linq;
using Game.Core;
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

        // The test ground (Docs/TestGround.md): started from the world list's row, its badge and hit log after a few
        // hits, its panel's six pages on a PC and a phone, and nothing written once it is left.
        [UnityTest, Category(Screens.Category)]
        public IEnumerator TheTestGroundsPanelAndHitLogDraw()
        {
            Assert.IsTrue(_root.OpenTitle("worlds"));
            yield return new WaitForSecondsRealtime(Settle);
            yield return Screens.Take("testground-worlds");
            _root.StartTestGroundNow();
            yield return TestSaves.UntilPlaying(_root, 1, 60);
            Assert.IsTrue(_root.Playing, "The test ground never started playing. " + _root.Status);
            Assert.IsTrue(_root.InTestGround);
            Assert.IsTrue(_root.HostWorld.TestGround);
            var player = _root.HostPlayer;
            StringAssert.StartsWith("철 세트", Playtest.Act(_root, "test gear 2 sword Rare High 3"));
            yield return null;
            StringAssert.Contains("동굴 수호자", Playtest.Act(_root, "test boss cave_warden"));
            yield return null;
            _root.HostWorld.MonstersHold = true;
            Playtest.Act(_root, "test mob cave_beetle leader");
            yield return null;
            var leader = _root.HostWorld.Enemies.First(e => e.Elite == EliteRank.Leader);
            _root.HostWorld.Hurt(player, leader, 40);
            _root.HostWorld.Hurt(player, _root.HostWorld.Enemies.First(e => e.Elite == EliteRank.None), 30);
            yield return new WaitForSecondsRealtime(Settle);
            Assert.AreEqual(2, _root.HostWorld.HitsOn(player.Id).Count);
            Assert.Greater(TestGround.ShownLines.Count, 1, "The hit log is on the screen.");
            yield return Screens.Take("testground-hits");
            foreach (bool touch in new[] { false, true })
            {
                Controls.ForceTouch(touch);
                yield return new WaitForSecondsRealtime(Settle);
                if (touch) yield return Screens.Take("testground-hits-touch");
                foreach (string page in new[] { "레벨", "장비", "이동", "몬스터", "생존", "기록" })
                {
                    Playtest.Act(_root, "test panel " + page);
                    yield return new WaitForSecondsRealtime(Settle);
                    Assert.AreEqual("시험판", _root.ScreenName);
                    yield return Screens.Take($"testground-panel-{page}{(touch ? "-touch" : "")}");
                }
                _root.ShowScreen(GameScreen.None);
            }
            Controls.ForceTouch(false);
            _root.ShowHub(true);
            yield return new WaitForSecondsRealtime(Settle);
            yield return Screens.Take("testground-hub");
            _root.ShowHub(false);
            _root.Leave();
            yield return null;
            Assert.IsFalse(SaveStore.Exists(SaveStore.CharacterKey(Name)), "The character played in the test ground is not saved.");
            Assert.IsFalse(SaveStore.Exists(SaveStore.WorldKey(TestGround.Name)), "Nor the world.");
            Assert.IsFalse(TestGround.Active);
        }
    }
}
