using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client
{
    // The test ground (Docs/TestGround.md, the owner's request of 2026-11-26): the picked character, a copy of it, in a
    // world of the current generator made from a fixed seed, where nothing is saved. The panel (TestPanel.cs) changes
    // level, gear, place and monsters at once, and a small log on the screen shows every hit taken: who, with what, how
    // much of the health, and the armor weighed. Only a game started from the world list's [시험장] row has any of it.
    static class TestGround
    {
        // The world: a fixed seed whose world has all twelve bosses' arenas and all eight dungeon gates (checked with
        // CoreSandbox --loadtime on 2026-11-26), and the name the screens call it.
        public const ulong Seed = 20261126;
        public const string Name = "시험장";
        // The world list's row, a name no world can have (NewWorld names are trimmed text).
        public const string Row = "\u0001시험장";
        // How many healing potions [다 회복] leaves in the bag.
        public const int Potions = 10;

        // On while a test ground is played (GameRoot sets it); the hub's [시험판] and the key are there only then.
        public static bool Active { get; set; }

        // The hit log on the screen ([맞은 기록 보이기]); the panel's own page shows it whether or not.
        public static bool ShowHits { get; set; } = true;

        public static void Reset()
        {
            Active = false;
            ShowHits = true;
            Shown = Rect.zero;
            ShownLines = Array.Empty<string>();
            TestPanel.Forget();
        }

        // The panel's key on a PC: T, or F1 when T has been given to something else (Settings, 키 바꾸기).
        public static Key PanelKey => KeyBindings.IsBound(Key.T) ? Key.F1 : Key.T;

        public static string PanelKeyName => PanelKey == Key.T ? "T" : "F1";

        public static bool PanelPressed()
        {
            var keyboard = Keyboard.current;
            return Active && keyboard != null && KeyBindings.Capturing == null && !Controls.Typing && keyboard[PanelKey].wasPressedThisFrame;
        }

        // Puts the player there at once (the host's world; the screen follows by the correction).
        public static void Move(Player player, Vec2 to)
        {
            player.Position = to;
            player.Corrections++;
        }

        // A room of the biome in the zone to fight in: one where a pack lives first, nearest the spawn.
        public static RoomNode FieldRoom(World world, Biome biome, int zone)
        {
            var rooms = world.Rooms?.Nodes;
            if (rooms == null) return null;
            return rooms.Where(r => r.Biome == biome && world.ZoneLevelAt(r.Center) == zone && !world.InSafePlace(r.Center))
                .OrderBy(r => r.Role == RoomRole.Pack ? 0 : r.Role == RoomRole.Elite ? 1 : r.Role == RoomRole.Plain ? 2 : 3)
                .ThenBy(r => Vec2.Distance(r.Center, world.Spawn)).FirstOrDefault();
        }

        // The zones each biome's rooms lie in (for the panel's buttons).
        public static List<(Biome Biome, List<int> Zones)> FieldZones(World world)
        {
            var rooms = world.Rooms?.Nodes ?? (IReadOnlyList<RoomNode>)Array.Empty<RoomNode>();
            return Biomes.Select(b => (b, rooms.Where(r => r.Biome == b && !world.InSafePlace(r.Center)).Select(r => world.ZoneLevelAt(r.Center)).Distinct().OrderBy(z => z).ToList()))
                .Where(p => p.Item2.Count > 0).ToList();
        }

        // The biomes by their band (Docs/Difficulty.md): the order the panel lists them in.
        public static readonly Biome[] Biomes = { Biome.Cave, Biome.Mushroom, Biome.Sand, Biome.Ice, Biome.Crystal, Biome.Lake, Biome.Lava, Biome.Abyss };

        // One hit on one line: "슬라임 · 몸통 공격 · -12 (9%, 목표 6%)", and the armor that weighed it.
        public static string Line(HitRecord hit)
        {
            string aim = hit.Aim > 0 ? $", 목표 {hit.Aim * 100:0}%" : "";
            string saved = hit.Saved ? " · 버팀" : "";
            return $"{hit.Who} · {hit.What} · -{hit.Amount} ({hit.Fraction * 100:0}%{aim}){saved}";
        }

        public static string ArmorLine(HitRecord hit)
        {
            if (!hit.Armored) return $"  방어 무시 · 원래 {hit.Raw:0}";
            string zone = hit.ZoneLevels > 0 ? $" · 구역 +{hit.ZoneLevels * World.ZonePowerStep * 100:0}%" : "";
            return $"  원래 {hit.Raw:0} · 방어 {hit.Armor:0}/{hit.BandArmor:0}({DamageBands.BandNames[DamageBands.Clamp(hit.Band) - 1]}) → {hit.Share * 100:0}% 들어옴{zone}";
        }

        // Where the log was drawn in the last repaint and what it said, for the banners to keep off and the playtests.
        public static Rect Shown { get; private set; }
        public static IReadOnlyList<string> ShownLines { get; private set; } = Array.Empty<string>();

        // Not drawn this frame (a window or the hub over it).
        public static void Hide()
        {
            Shown = Rect.zero;
            ShownLines = Array.Empty<string>();
        }

        static GUIStyle _line, _head;
        static int _seenCount;
        static float _newAt = -10;

        // The log under the minimap column's lines: a head with [시험판] (the badge, always there), and under it the last
        // hits, newest on top, as many as fit above the middle of the screen (where the player stands) and clear of the
        // skill keys. Each hit's armor on a second line where there is room (a PC).
        public static void DrawOverlay(World world, Player player, int skills, Action openPanel)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Shown = Rect.zero;
                ShownLines = Array.Empty<string>();
            }
            if (world == null || player == null) return;
            bool touch = Controls.UsingTouch;
            float width = Mathf.Min(touch ? 300 : 400, Ui.Width - 20);
            float x = Ui.Width - 10 - width;
            float top = Mathf.Max(Ui.InfoBottom, Ui.RightColumnBottom) + 6;
            // Below the status box and goal list where the two would meet (a phone held upright).
            if (x < Ui.HudArea.xMax + 6) top = Mathf.Max(top, Ui.LeftColumnBottom + 6);
            float bottom = Ui.Height - 10;
            var keys = SkillBar.Bounds(skills);
            var quick = Hotbar.Bounds(skills);
            foreach (var r in new[] { keys, quick })
                if (r.width > 0 && r.xMax > x && r.x < x + width) bottom = Mathf.Min(bottom, r.y - 6);
            // Not over the player: the middle of the screen, where the camera keeps them, less room for the name tags over
            // their head (the 2026-11-26 sweep: on a phone held upright the log reached the player's tag).
            if (x < Ui.Width / 2 + 40) bottom = Mathf.Min(bottom, Ui.Height / 2 - 72);

            _line ??= new GUIStyle(GUI.skin.label) { padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0), wordWrap = false, clipping = TextClipping.Clip };
            Ui.Text(_line, touch ? 12 : 12);
            _head ??= new GUIStyle(GUI.skin.label) { padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0), wordWrap = false, alignment = TextAnchor.MiddleLeft };
            Ui.Text(_head, 13, bold: true);
            float headHeight = touch ? 40 : 28;
            // No room under the status box above the player (a long goal list on a phone held upright): only the head,
            // beside the box under the minimap column, so the badge and [시험판] stay.
            bool headOnly = false;
            if (bottom - top < headHeight)
            {
                headOnly = true;
                top = Mathf.Max(Ui.InfoBottom, Ui.RightColumnBottom) + 6;
                float left = Ui.HudArea.xMax + 6;
                width = Mathf.Min(width, Ui.Width - 10 - left);
                x = Ui.Width - 10 - width;
                if (width < 100) return;
            }
            var head = new Rect(x, top, width, headHeight);

            var hits = world.HitsOn(player.Id);
            int count = world.HitCount(player.Id);
            if (count != _seenCount)
            {
                _seenCount = count;
                _newAt = Time.unscaledTime;
            }
            float line = Mathf.Ceil(_line.lineHeight) + 2;
            bool detail = !touch;
            var rows = new List<(string Text, Color Color)>();
            if (ShowHits && !headOnly)
            {
                if (hits.Count == 0) rows.Add(("아직 맞지 않았다", new Color(0.8f, 0.78f, 0.72f)));
                foreach (var hit in hits.Reverse())
                {
                    var color = hit.Saved ? new Color(1f, 0.55f, 0.45f) : hit.Aim > 0 && hit.Fraction > hit.Aim * 1.5f ? new Color(1f, 0.75f, 0.45f) : new Color(0.95f, 0.93f, 0.88f);
                    rows.Add((Line(hit), color));
                    if (detail) rows.Add((ArmorLine(hit), new Color(0.75f, 0.8f, 0.85f)));
                }
            }
            int fit = Mathf.Max(0, Mathf.FloorToInt((bottom - head.yMax - 8) / line));
            // A phone: the last three at most, so the world above the player stays in sight.
            if (touch) fit = Mathf.Min(fit, 3);
            if (rows.Count > fit)
            {
                // Whole hits only: a hit's armor line does not stand without its line above.
                int keep = fit - (detail && fit % 2 == 1 ? 1 : 0);
                rows = rows.Take(Mathf.Max(0, keep)).ToList();
            }
            float listHeight = rows.Count > 0 ? rows.Count * line + 8 : 0;
            var box = new Rect(x, top, width, headHeight + listHeight);
            if (Event.current.type == EventType.Repaint)
            {
                var back = GUI.color;
                GUI.color = new Color(0.06f, 0.05f, 0.08f, 0.72f);
                GUI.DrawTexture(box, Texture2D.whiteTexture);
                // A gold edge on the left, brighter a moment after a new hit.
                float flash = Mathf.Clamp01(1 - (Time.unscaledTime - _newAt) / 0.8f);
                GUI.color = Color.Lerp(UiSkin.Gold * new Color(1, 1, 1, 0.8f), new Color(1f, 0.4f, 0.3f), flash);
                GUI.DrawTexture(new Rect(box.x, box.y, 3, box.height), Texture2D.whiteTexture);
                GUI.color = back;
            }
            float button = touch ? 96 : 104;
            _head.normal.textColor = UiSkin.Gold;
            if (Event.current.type == EventType.Repaint)
                GUI.Label(new Rect(head.x + 10, head.y, head.width - button - 16, head.height), ShowHits && head.width >= 250 ? "시험장 · 맞은 기록" : "시험장", _head);
            string label = touch ? "시험판" : $"시험판 ({PanelKeyName})";
            var open = new Rect(head.xMax - button - 4, head.y + 4, button, head.height - 8);
            Controls.Block(open);
            if (Ui.Button(open, label, "시험판")) openPanel();
            float y = head.yMax + 4;
            var shownLines = new List<string>();
            foreach (var (text, color) in rows)
            {
                var previous = GUI.color;
                if (Event.current.type == EventType.Repaint)
                {
                    GUI.color = new Color(0, 0, 0, 0.8f);
                    GUI.Label(new Rect(x + 11, y + 1, width - 16, line), text, _line);
                    GUI.color = color;
                    GUI.Label(new Rect(x + 10, y, width - 16, line), text, _line);
                    GUI.color = previous;
                }
                shownLines.Add(text);
                y += line;
            }
            if (Event.current.type == EventType.Repaint)
            {
                Shown = box;
                ShownLines = shownLines;
            }
        }
    }
}
