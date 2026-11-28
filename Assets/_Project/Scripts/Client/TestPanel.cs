using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The test ground's panel (Docs/TestGround.md): a full screen of buttons in six pages, 레벨, 장비, 이동, 몬스터, 생존 and
    // 맞은 기록. Each button runs one of Playtest's "test" commands (Playtest.TestGround.cs), which change the host's
    // world, and its answer shows on the line under the pages. Rows are 44 points on a touch screen and the page scrolls
    // by a finger, so it fits a phone held either way and a small PC window.
    static class TestPanel
    {
        enum Section
        {
            Level,
            Gear,
            Travel,
            Monsters,
            Survive,
            Hits,
        }

        static readonly string[] SectionNames = { "레벨", "장비", "이동", "몬스터", "생존", "맞은 기록" };

        static Section _section;
        static Vector2 _scroll;
        static Action _later;
        static Action _close;
        static string _status = "";

        // What is picked on the pages.
        static int _level = 10;
        static int _band = 2;
        static string _weapon = "sword";
        static Rarity _rarity = Rarity.Common;
        static TestKit.Roll _roll = TestKit.Roll.Middle;
        static int _upgrade;
        static Biome _mobBiome = Biome.Cave;
        static string _mobRank = "normal";
        static bool _picked;
        static List<(Biome Biome, List<int> Zones)> _zones;

        public static string SectionName => SectionNames[(int)_section];
        public static string Status => _status;

        // A page by its name (the playtests' "test panel <name>"): 레벨 장비 이동 몬스터 생존 기록, or level gear travel
        // monsters survive hits.
        public static void Pick(string name)
        {
            int index = Array.IndexOf(SectionNames, name);
            if (index < 0 && name == "기록") index = (int)Section.Hits;
            if (index < 0 && Enum.TryParse(name, true, out Section section)) index = (int)section;
            if (index >= 0) Show((Section)index);
        }

        static void Show(Section section)
        {
            if (_section != section) _scroll = Vector2.zero;
            _section = section;
        }

        // A press is done in the next Update (GameRoot.TestGroundKeys), before the frame's GUI: a page that changes (another
        // tab, another biome's monsters, the panel shut) is then laid out the same in its layout and its drawing.
        public static void Tick()
        {
            var action = _later;
            _later = null;
            action?.Invoke();
        }

        public static void Forget()
        {
            _section = Section.Level;
            _scroll = Vector2.zero;
            _later = null;
            _status = "";
            _picked = false;
            _zones = null;
        }

        static GUIStyle _note, _heading, _cell;

        static float Row => Controls.UsingTouch ? 44 : 30;
        const float Gap = 4;

        // The panel over the darkened world (GameRoot.DrawFullScreen). close shuts it.
        public static void Draw(GameRoot root, Action close)
        {
            var world = root.HostWorld;
            var player = root.HostPlayer;
            _close = close;
            if (world == null || player == null)
            {
                close();
                return;
            }
            if (!_picked)
            {
                _picked = true;
                _level = Math.Max(1, player.Level);
                _band = DamageBands.BandOfLevel(player.Level);
            }
            var area = ScreenStack.AreaOf(GameScreen.Test);
            Controls.Block(area);
            UiMotion.BeginWindow(ScreenStack.MotionKey(GameScreen.Test), area, UiKind.Machine);
            var inside = UiSkin.Window(area, UiKind.Machine, "시험판" + (Controls.UsingTouch ? "" : $" ({TestGround.PanelKeyName})"), UiIcons.Target, out var head);
            if (UiSkin.CloseButton(head))
            {
                close();
                UiMotion.EndWindow();
                return;
            }
            _note ??= new GUIStyle(GUI.skin.label) { wordWrap = true, padding = new RectOffset(2, 2, 1, 1), margin = new RectOffset(0, 0, 0, 0) };
            Ui.Text(_note, 12);
            _heading ??= new GUIStyle(GUI.skin.label) { wordWrap = false, padding = new RectOffset(2, 2, 4, 1), margin = new RectOffset(0, 0, 0, 0) };
            Ui.Text(_heading, 13, bold: true);
            _heading.normal.textColor = UiSkin.Gold;
            _cell ??= new GUIStyle(GUI.skin.button) { wordWrap = false, clipping = TextClipping.Clip };
            Ui.Text(_cell, Controls.UsingTouch ? 13 : 12);

            // The pages' tabs: one row where they fit at 64 points each, else two of three.
            int perRow = inside.width >= SectionNames.Length * 64 + (SectionNames.Length - 1) * Gap ? SectionNames.Length : 3;
            int tabRows = (SectionNames.Length + perRow - 1) / perRow;
            float tab = Controls.UsingTouch ? 44 : 32;
            float tabWidth = (inside.width - Gap * (perRow - 1)) / perRow;
            Ui.Row("시험판 쪽");
            for (int i = 0; i < SectionNames.Length; i++)
            {
                var rect = new Rect(inside.x + (i % perRow) * (tabWidth + Gap), inside.y + (i / perRow) * (tab + Gap), tabWidth, tab);
                var section = (Section)i;
                if (Ui.Button(rect, SectionNames[i], SectionNames[i], _cell, Ui.Look.Normal, _section == section)) _later = () => Show(section);
            }
            Ui.Row("");
            float y = inside.y + tabRows * (tab + Gap) + 2;
            // The last answer, gold, on two lines at most.
            string status = _status.Length > 0 ? _status : "누르면 바로 바뀐다. 여기서 한 일은 저장되지 않는다";
            string wrapped = Ui.Wrap(status, _note, inside.width - 4);
            float statusHeight = Mathf.Min(_note.CalcHeight(new GUIContent(wrapped), inside.width), 2 * (_note.lineHeight + 2) + 2);
            var color = _note.normal.textColor;
            _note.normal.textColor = _status.StartsWith("할 수 없다", StringComparison.Ordinal) ? new Color(1f, 0.6f, 0.5f) : UiSkin.Gold;
            GUI.Label(new Rect(inside.x, y, inside.width, statusHeight), wrapped, _note);
            _note.normal.textColor = color;
            y += statusHeight + 4;

            var body = Rect.MinMaxRect(inside.x, y, inside.xMax, inside.yMax);
            Ui.TouchScroll(body, ref _scroll);
            GUILayout.BeginArea(body);
            _scroll = Ui.BeginScrollView(_scroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            float width = body.width - 18;
            GUILayout.BeginVertical(GUILayout.Width(width));
            switch (_section)
            {
                case Section.Level: LevelPage(root, player, width); break;
                case Section.Gear: GearPage(root, player, width); break;
                case Section.Travel: TravelPage(root, world, width); break;
                case Section.Monsters: MonstersPage(root, world, width); break;
                case Section.Survive: SurvivePage(root, world, player, width); break;
                default: HitsPage(world, player, width); break;
            }
            GUILayout.Space(8);
            GUILayout.EndVertical();
            Ui.EndScrollView();
            GUILayout.EndArea();
            UiMotion.EndWindow();
        }

        // ---- The parts of a page ----

        static void Heading(string text) => GUILayout.Label(text, _heading);

        static void Note(string text, float width)
        {
            GUILayout.Label(Ui.Wrap(text, _note, width - 4), _note, GUILayout.Width(width));
        }

        // Runs the command (from Tick) and keeps its answer for the line under the tabs (and in the "획득" lines when the
        // panel shuts with it).
        static void Run(GameRoot root, string command, bool shut = false)
        {
            _status = Playtest.Act(root, command);
            if (!shut) return;
            root.Notice(_status);
            _close?.Invoke();
        }

        // Buttons in rows of as many as fit at least minWidth wide; each known to the playtests by its label in the row.
        static void Grid(string row, float width, float minWidth, IEnumerable<(string Label, bool Chosen, Action Press)> cells, Ui.Look look = Ui.Look.Normal)
        {
            var list = cells.ToList();
            if (list.Count == 0) return;
            int columns = Mathf.Clamp(Mathf.FloorToInt((width + Gap) / (minWidth + Gap)), 1, list.Count);
            float cell = (width - Gap * (columns - 1)) / columns;
            Ui.Row(row);
            for (int start = 0; start < list.Count; start += columns)
            {
                var line = GUILayoutUtility.GetRect(width, Row, GUILayout.Width(width), GUILayout.Height(Row));
                for (int i = start; i < Math.Min(list.Count, start + columns); i++)
                {
                    var (label, chosen, press) = list[i];
                    var rect = new Rect(line.x + (i - start) * (cell + Gap), line.y, cell, line.height);
                    if (Ui.Button(rect, label, label, _cell, look, chosen)) _later = press;
                }
                GUILayout.Space(Gap);
            }
            Ui.Row("");
        }

        static (string, bool, Action) Cell(string label, bool chosen, Action press) => (label, chosen, press);

        // ---- 레벨 ----

        static readonly int[] Levels = { 1, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50 };

        static void LevelPage(GameRoot root, Player player, float width)
        {
            string classes = player.Classes.Count > 0 ? string.Join(" + ", player.Classes.Select(c => c.Name)) : "클래스 없음";
            Note($"지금 Lv {player.Level} · {classes} · 스킬 포인트 {player.SkillPoints} · 특성 포인트 {player.PassivePoints}", width);
            if (player.Classes.Count == 0) Note("클래스가 없으면 배울 스킬도 없다: 아래 클래스를 먼저 고른다.", width);
            Heading($"레벨 고르기: Lv {_level}");
            Grid("레벨", width, Controls.UsingTouch ? 56 : 52, Levels.Select(l => Cell($"Lv {l}", _level == l, () => _level = l))
                .Concat(new[] { Cell("-1", false, () => _level = Math.Max(1, _level - 1)), Cell("+1", false, () => _level = Math.Min(Leveling.MaxLevel, _level + 1)) }));
            Heading("포인트");
            Grid("레벨 포인트", width, 120, new[]
            {
                Cell("포인트만 받기", false, () => Run(root, $"test level {_level} points")),
                Cell("공격 빌드", false, () => Run(root, $"test level {_level} attack")),
                Cell("방어 빌드", false, () => Run(root, $"test level {_level} defense")),
            }, Ui.Look.Primary);
            Note("포인트만 받기: 스킬과 특성을 비우고 그 레벨의 포인트를 준다(직접 찍는다). 공격·방어 빌드: 열린 스킬을 다 배우고 특성과 결정을 다 쓴다(방어는 생명력·방어·재생 먼저).", width);
            Heading("클래스 (최대 3)");
            var all = root.ClassSheet.Classes;
            var mine = player.Classes.Select(c => c.Id).ToList();
            Grid("클래스", width, 110, all.Select(c => Cell(c.Name, mine.Contains(c.Id), () =>
            {
                var next = mine.Contains(c.Id) ? mine.Where(id => id != c.Id).ToList() : mine.Concat(new[] { c.Id }).Take(ClassRules.DefaultMaxClasses).ToList();
                if (next.Count > 0) Run(root, "test classes " + string.Join(",", next));
            })));
            Heading("직접 찍기");
            Grid("레벨 창", width, 120, new[]
            {
                Cell("스킬 창", false, () => root.ShowScreen(GameScreen.Skills)),
                Cell("특성 창", false, () => root.ShowScreen(GameScreen.Passives)),
                Cell("캐릭터 창", false, () => root.ShowScreen(GameScreen.Character)),
            });
            // The gender (Docs/Gender.md): the figure and the groan when struck.
            bool woman = root.LocalClient?.Look.IsFemale == true;
            Grid("성별", width, 76, new[]
            {
                Cell("남자", !woman, () => Run(root, "test gender 0")),
                Cell("여자", woman, () => Run(root, "test gender 1")),
            });
        }

        // ---- 장비 ----

        static readonly (string Id, string Name)[] WeaponKinds =
        {
            ("sword", "검"), ("dagger", "단검"), ("greatsword", "대검"), ("spear", "창"), ("hammer", "망치"),
            ("bow", "활"), ("crossbow", "석궁"), ("boomerang", "부메랑"), ("staff", "지팡이"), ("wand", "완드"), ("orb", "오브"),
        };
        static readonly Rarity[] Rarities = { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic };
        static readonly (TestKit.Roll Roll, string Name)[] Rolls = { (TestKit.Roll.Low, "낮은 값"), (TestKit.Roll.Middle, "가운데"), (TestKit.Roll.High, "높은 값") };

        static void GearPage(GameRoot root, Player player, float width)
        {
            var loot = root.LootSheet;
            Note($"입은 것: 방어 {player.Stats.Armor:0} · 생명력 {player.Stats.MaxHealth:0} · 피해 {player.Stats.Damage:0}", width);
            Heading("단계");
            Grid("단계", width, 64, Enumerable.Range(1, DamageBands.Bands).Select(b => Cell(DamageBands.BandNames[b - 1], _band == b, () => _band = b)));
            Heading("무기");
            Grid("무기", width, 64, WeaponKinds.Select(w => Cell(w.Name, _weapon == w.Id, () => _weapon = w.Id)));
            Heading("등급");
            Grid("등급", width, 64, Rarities.Select(r => Cell(ItemText.RarityName(r), _rarity == r, () => _rarity = r)));
            Heading("수치");
            Grid("수치", width, 76, Rolls.Select(r => Cell(r.Name, _roll == r.Roll, () => _roll = r.Roll)));
            Heading("강화");
            Grid("강화", width, 44, Enumerable.Range(0, Item.MaxUpgrade + 1).Select(u => Cell($"+{u}", _upgrade == u, () => _upgrade = u)));
            string options = $"{_rarity} {_roll} {_upgrade}";
            Grid("장비 입기", width, 120, new[]
            {
                Cell($"{DamageBands.BandNames[_band - 1]} 세트 입기", false, () => Run(root, $"test gear {_band} {_weapon} {options}")),
                Cell("맨몸", false, () => Run(root, "test bare")),
            }, Ui.Look.Primary);
            Note("일반·가운데·+0 세트가 한 방 표(--hits)의 '제 단계 장비'와 같다. 홈은 모두 열려 있다.", width);

            Heading("한 벌씩");
            var pieces = loot.Bases.Where(b => !b.IsLegendary && b.Weight > 0 && b.Level == BandBaseLevel(_band) && !b.Id.EndsWith("_pickaxe", StringComparison.Ordinal)).ToList();
            Grid("한 벌씩", width, 104, pieces.Select(b => Cell(b.Name, player.Equipment[b.Slot]?.Base == b, () => Run(root, $"test piece {b.Id} {options}"))));
            // The second weapon (Docs/Weapons.md, 무기 두 벌): the band's eleven into the spare slot, and the swap.
            Heading("보조 무기 (무기 종류 11, 바꾸기 X)");
            string tierName = DamageBands.TierNames[_band - 1];
            var spares = TestKit.Weapons.Select(w => loot.Bases.FirstOrDefault(b => b.Id == $"{tierName}_{w}")).Where(b => b != null).ToList();
            Grid("보조 무기", width, 104, spares.Select(b => Cell(b.Name, player.Equipment.Spare?.Base == b, () => Run(root, $"test spare {b.Id} {_roll} {_upgrade}"))));
            Grid("무기 바꾸기", width, 120, new[]
            {
                Cell("손과 보조 바꾸기", false, () => Run(root, "test swap")),
                Cell("보조 빼기", false, () => Run(root, "test spare none")),
            });
            // The weapon's own art (Docs/Weapons.md 묶음 C): used from here, and every kind's cooldown cleared.
            var art = player.Stats.Kind?.Art;
            Heading($"무기 기술 ({Controls.ArtKeyName}·오른쪽 클릭): {(art == null ? "없음" : $"{art.Name} {art.Power:0}% · {art.Cooldown:0.#}초")}");
            Grid("무기 기술", width, 120, new[]
            {
                Cell("기술 쓰기", false, () => Run(root, "test art")),
                Cell("대기 비우기", false, () => Run(root, "test artready")),
            });
            // The band's hunting sets and the boss sets worn in it, all their pieces at once (Docs/Gear.md).
            Heading("세트 (사냥·보스, 한 번에 모두)");
            var sets = loot.Sets.Where(set => loot.Bases.Any(b => b.Set == set && GearBand(b) == _band)).ToList();
            Grid("세트", width, 150, sets.Select(set => Cell(set.Name, loot.Bases.Where(b => b.Set == set).All(b => player.Equipment[b.Slot]?.Base == b),
                () => Run(root, $"test set {set.Id} {_roll} {_upgrade}"))));
            Heading("정예 고유");
            Grid("정예 고유", width, 150, loot.Bases.Where(b => b.IsEliteUnique && GearBand(b) == _band).Select(b => Cell(b.Name, player.Equipment[b.Slot]?.Base == b,
                () => Run(root, $"test piece {b.Id} Legendary {_roll} {_upgrade}"))));
            Heading("전설 (보스·균열)");
            Grid("전설", width, 150, loot.Bases.Where(b => b.IsLegendary && !b.IsEliteUnique).OrderBy(b => b.Level).Select(b => Cell(b.Name, player.Equipment[b.Slot]?.Base == b, () => Run(root, $"test piece {b.Id} Legendary {_roll} {_upgrade}"))));

            Heading("룬 (입은 것의 빈 홈에)");
            Grid("룬", width, 76, loot.Runes.OrderBy(r => r.Level).Select(r => Cell(r.Name, false, () => Run(root, $"test rune {r.Id}"))));
            Grid("룬 빼기", width, 120, new[] { Cell("룬 모두 빼기", false, () => Run(root, "test runes clear")) });
            Heading("음식 (먹으면 버프)");
            Grid("음식", width, 104, loot.Consumables.Where(c => c.IsFood).Select(c => Cell(c.Name, false, () => Run(root, $"test eat {c.Id}"))));
        }

        // The band a piece is worn in: its level's (the last band's 45 too).
        static int GearBand(ItemBase b) => DamageBands.BandOfLevel(Math.Min(b.Level, 40));

        // The level a band's ordinary bases are made at (item_bases.csv: 1, 10, 20, 30, 40).
        static int BandBaseLevel(int band) => band <= 1 ? 1 : (band - 1) * 10;

        // ---- 이동 ----

        static void TravelPage(GameRoot root, World world, float width)
        {
            Grid("이동 마을", width, 120, new[] { Cell("시작 마을", false, () => Run(root, "test home", shut: true)) }, Ui.Look.Primary);
            Note("누르면 시험판이 닫히고 그곳에 선다.", width);
            Heading("보스 (가면 잠든 채 되살아나 있다)");
            var bosses = root.BossSheet?.Bosses.OrderBy(b => b.Level).ToList() ?? new List<BossDefinition>();
            float revive = Controls.UsingTouch ? 92 : 84;
            Ui.Row("보스");
            foreach (var boss in bosses)
            {
                bool here = world.BossHome(boss.Id) != null;
                var line = GUILayoutUtility.GetRect(width, Row, GUILayout.Width(width), GUILayout.Height(Row));
                var go = new Rect(line.x, line.y, line.width - revive - Gap, line.height);
                var back = new Rect(go.xMax + Gap, line.y, revive, line.height);
                var was = GUI.enabled;
                GUI.enabled = was && here;
                string band = DamageBands.BandNames[DamageBands.BandOfLevel(boss.Level) - 1];
                string id = boss.Id;
                if (Ui.Button(go, $"{boss.Name} · Lv {boss.Level} · {band}", boss.Name, _cell, Ui.Look.Normal, false)) _later = () => Run(root, $"test boss {id}", shut: true);
                if (Ui.Button(back, "되살리기", boss.Name + " 되살리기", _cell, Ui.Look.Normal, false)) _later = () => Run(root, $"test revive {id}");
                GUI.enabled = was;
                GUILayout.Space(Gap);
            }
            Ui.Row("");
            Heading("던전 (문 앞에서 바로 들어간다)");
            var dungeons = world.Gates.Where(g => !g.Cellar).Select(g => (Gate: g, Definition: world.DungeonFor(g))).Where(d => d.Definition != null)
                .GroupBy(d => d.Definition.Id).Select(g => g.First()).OrderBy(d => d.Definition.Level).ToList();
            Grid("던전", width, 150, dungeons.Select(d => Cell($"{d.Definition.NameFor(d.Gate.Biome)} · {d.Definition.Level}구역", false, () => Run(root, $"test dungeon {d.Definition.Id}", shut: true))));
            Heading($"들판 (구역마다 몬스터 피해 +{World.ZonePowerStep * 100:0}%)");
            _zones ??= TestGround.FieldZones(world);
            foreach (var (biome, zones) in _zones)
            {
                string name = BiomeMap.Name(biome);
                Grid("들판", width, 96, zones.Select(z => Cell($"{name} {z}구역", false, () => Run(root, $"test field {biome} {z}", shut: true))));
            }
        }

        // ---- 몬스터 ----

        static readonly (string Id, string Name)[] Ranks = { ("normal", "보통"), ("leader", "우두머리"), ("champion", "챔피언"), ("pack", "무리 5") };

        static void MonstersPage(GameRoot root, World world, float width)
        {
            Grid("몬스터 정리", width, 120, new[]
            {
                Cell("모두 치우기", false, () => Run(root, "test clear")),
                Cell(world.MonstersHold ? "멈춤 풀기" : "멈추기", world.MonstersHold, () => Run(root, world.MonstersHold ? "test hold off" : "test hold on")),
            }, Ui.Look.Primary);
            Heading("생물군계");
            Grid("생물군계", width, 76, TestGround.Biomes.Select(b => Cell(BiomeMap.Name(b), _mobBiome == b, () => _mobBiome = b)));
            Heading("어떻게");
            Grid("몬스터 종류", width, 76, Ranks.Select(r => Cell(r.Name, _mobRank == r.Id, () => _mobRank = r.Id)));
            Note("우두머리는 수호 접사와 같은 종 둘을 거느리고, XL 우두머리는 중간 보스다. 챔피언은 셋이 사나움 접사를 나눈다. 그 자리의 구역만큼 세진다.", width);
            Heading("부르기 (바라보는 쪽 앞에)");
            var kinds = root.MonsterSheet.Monsters.Where(m => (m.Biome ?? Biome.Cave) == _mobBiome).OrderBy(m => m.Size).ThenBy(m => m.Level).ToList();
            Grid("몬스터", width, 120, kinds.Select(m => Cell($"{m.Name} {MonsterSizes.Name(m.Size).ToUpperInvariant()}", false, () => Run(root, $"test mob {m.Id} {_mobRank}", shut: true))));
        }

        // ---- 생존 ----

        static void SurvivePage(GameRoot root, World world, Player player, float width)
        {
            bool undying = world.Undying(player.Id), rising = world.RisingOf(player.Id) != null;
            Grid("생존", width, 150, new[]
            {
                Cell($"다 회복 · 물약 {TestGround.Potions}개", false, () => Run(root, "test heal")),
                Cell(undying ? "죽지 않기: 켬" : "죽지 않기: 끔", undying, () => Run(root, undying ? "test undying off" : "test undying on")),
                Cell(rising ? "여기서 일어나기: 켬" : "여기서 일어나기: 끔", rising, () => Run(root, rising ? "test rising off" : "test rising here")),
                Cell(TestGround.ShowHits ? "맞은 기록 보이기: 켬" : "맞은 기록 보이기: 끔", TestGround.ShowHits, () => Run(root, TestGround.ShowHits ? "test hits hide" : "test hits show")),
                Cell("맞은 기록 지우기", false, () => Run(root, "test hits clear")),
            });
            Note("죽지 않기: 생명력이 1 밑으로 내려가지 않는다(맞은 기록에는 다 적힌다, '버팀'). 여기서 일어나기: 쓰러지면 누른 자리에서 일어난다. 물약은 회복 40%, 다시 쓰기까지 12초.", width);
        }

        // ---- 맞은 기록 ----

        static void HitsPage(World world, Player player, float width)
        {
            var hits = world.HitsOn(player.Id);
            Note($"최근 {hits.Count}번 (모두 {world.HitCount(player.Id)}번). 생명력 {player.Stats.MaxHealth:0} · 방어 {player.Stats.Armor:0}. " +
                "% 는 최대 생명력에서 가져간 몫, 목표는 그 단계 장비를 입은 그 단계 캐릭터에게 맞춘 값(Docs/Difficulty.md). " +
                "방어 a/b: 내 방어/때린 쪽 단계의 기준 방어, 그 비로 들어온 몫이 정해진다.", width);
            if (hits.Count == 0) Note("아직 맞지 않았다.", width);
            foreach (var hit in hits.Reverse())
            {
                Note(TestGround.Line(hit), width);
                Note(TestGround.ArmorLine(hit).Trim() + (hit.Kind != HitKind.None ? $" · {KindName(hit.Kind)}" : ""), width);
                GUILayout.Space(4);
            }
        }

        static string KindName(HitKind kind) => kind switch
        {
            HitKind.Normal => "보통 몬스터",
            HitKind.Elite => "정예",
            HitKind.Large => "L 몸집",
            HitKind.Huge => "XL·중간 보스",
            HitKind.Quick => "보스의 빠른 공격",
            HitKind.Slow => "보스의 느린 공격",
            HitKind.Heavy => "보스의 큰 공격",
            HitKind.Ultimate => "보스의 필살기",
            HitKind.Barrage => "보스의 연사 한 발",
            HitKind.Combo => "보스의 연속 돌진",
            HitKind.FollowUp => "보스의 뒤따르는 공격",
            HitKind.Share => "나눠 맞는 원(혼자)",
            HitKind.Roar => "보스의 포효",
            _ => "",
        };
    }
}
