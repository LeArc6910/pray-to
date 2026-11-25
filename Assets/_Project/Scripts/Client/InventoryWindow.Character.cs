using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The character's sheet (U, Docs/UiRework.md 4-3 캐릭터, U4): at the left the hero large as dressed now (the weapon
    // in hand, HeroArt) among the six worn slots, the name, level and experience bar, the classes, what the level gives,
    // and the picked piece's panel; at the right the stats in groups (공격, 방어, 마나·스킬, 원소, 이동·기타) as a table of
    // name and value with a glyph each, the pet with its picture, level, bar, what it gives and its name, and the life
    // skills with their levels, bars and perks ([고르기]). On a phone held upright one column. A worn piece pressed
    // shows its panel with the bag's buttons ([벗기] …), as the old tab did.
    sealed partial class InventoryWindow
    {
        Vector2 _sheetLeftScroll, _sheetRightScroll;

        // The two columns of the sheet (the picture's and the stats'), or one on a narrow screen.
        internal static (Rect Left, Rect Right, bool Split) LaySheet(Rect body)
        {
            if (body.width < 640) return (body, body, false);
            float width = Mathf.Clamp(Mathf.Round(body.width * 0.36f), 290, 390);
            return (new Rect(body.x, body.y, width, body.height), Rect.MinMaxRect(body.x + width + 10, body.y, body.xMax, body.yMax), true);
        }

        void DrawCharacterSheet(Rect body)
        {
            var (left, right, split) = LaySheet(body);
            if (split)
            {
                TouchPanes(left, ref _sheetLeftScroll, right, ref _sheetRightScroll);
                if (Event.current.type == EventType.Repaint) UiSkin.Inset(left);
                _sheetLeftScroll = Pane(Rect.MinMaxRect(left.x + 4, left.y + 4, left.xMax - 2, left.yMax - 4), _sheetLeftScroll, DrawFigure);
                _sheetRightScroll = Pane(right, _sheetRightScroll, DrawSheetStats);
                return;
            }
            Ui.TouchScroll(body, ref _sheetRightScroll);
            _sheetRightScroll = Pane(body, _sheetRightScroll, () =>
            {
                DrawFigure();
                GUILayout.Space(10);
                DrawSheetStats();
            });
        }

        // ---- The hero ----

        void DrawFigure()
        {
            // The name and level, the bar to the next, the classes.
            var line = Room(22);
            Write(line, _client.Name, new Color(1f, 0.86f, 0.45f));
            Write(line, $"Lv {_client.Level}", Ink, align: TextAnchor.MiddleRight);
            float progress = Leveling.Progress(_client.Experience);
            var bar = Room(16);
            UiSkin.Bar(bar, progress, $"{(int)(progress * 100)}%", textSize: 12, experience: true);
            // The old first line, for anyone reading it whole.
            Ui.Wrap($"{_client.Name} · Lv {_client.Level} ({(int)(progress * 100)}%) · " +
                (_client.Classes.Count == 0 ? "클래스 없음" : string.Join(" + ", _client.Classes.Select(c => c.Name))), GUI.skin.label, 99999);
            var classes = Room(22);
            float x = classes.x;
            if (_client.Classes.Count == 0) Write(classes, "클래스 없음 · [스킬]에서 고른다", UiSkin.Muted);
            foreach (var chosen in _client.Classes)
            {
                var color = ToColor(GrowthArt.ClassColor(chosen));
                float width = Style(12, color).CalcSize(new GUIContent(chosen.Name)).x + 14;
                var chip = new Rect(x, classes.y + 2, width, classes.height - 4);
                if (Event.current.type == EventType.Repaint)
                {
                    UiSkin.Inset(chip);
                    Fill(new Rect(chip.x + 2, chip.y + 2, 3, chip.height - 4), color);
                }
                Write(new Rect(chip.x + 8, chip.y, chip.width - 8, chip.height), chosen.Name, color);
                x += width + 4;
            }
            // What the level alone gives, and the level of the next step (Docs/Bosses.md, 계단식 성장).
            GUILayout.Label(Fit(Leveling.Summary(_client.Level), _textWidth));
            GUILayout.Space(4);
            DrawDoll();
            DrawPicked();
            GUILayout.Space(6);
            GUILayout.Label(Fit($"스킬 포인트 {_client.SkillPoints} · 특성 포인트 {_client.PassivePoints} · 배운 특성 {_client.Passives.Count}", _textWidth));
        }

        // The hero large in the middle as dressed now, the slots at either side: weapon, ring and amulet at the left,
        // helmet, chest and boots at the right (Core Keeper and Diablo lay a doll so). Pressing a worn piece picks it. The
        // bag draws the same doll (U6), its slots on the row "착용".
        void DrawDoll(string row = "캐릭터")
        {
            float cell = Controls.UsingTouch ? 52 : 48;
            float column = cell * 3 + CellGap * 2;
            float hero = Mathf.Min(_textWidth - 2 * cell - 24, 150);
            var doll = Room(column + 8);
            float left = doll.x + (doll.width - (2 * cell + hero + 16)) / 2;
            var stage = new Rect(left + cell + 8, doll.y, hero, column + 8);
            if (Event.current.type == EventType.Repaint)
            {
                UiSkin.Inset(stage);
                Fill(new Rect(stage.x + 2, stage.y + 2, stage.width - 4, stage.height - 4), new Color(0.24f, 0.18f, 0.12f, 0.35f));
                // A floor under the feet.
                Fill(new Rect(stage.x + 12, stage.yMax - 22, stage.width - 24, 6), new Color(0, 0, 0, 0.35f));
                var weapon = _client.Equipment[ItemSlot.Weapon];
                var gear = ItemArt.Held(weapon?.Base);
                var sprite = Sprites.Centered($"sheethero/{gear.Key}", () => GrowthArt.Hero(gear, WorldView.SelfTunic));
                int scale = Mathf.Max(2, Mathf.FloorToInt(Mathf.Min((stage.width - 8) / GrowthArt.HeroWidth, (stage.height - 12) / GrowthArt.HeroHeight)));
                Picture(new Rect(stage.x, stage.y + 2, stage.width, stage.height - 10), sprite, scale);
            }
            var places = new (ItemSlot Slot, float X, int Row)[]
            {
                (ItemSlot.Weapon, left, 0), (ItemSlot.Ring, left, 1), (ItemSlot.Amulet, left, 2),
                (ItemSlot.Helmet, stage.xMax + 8, 0), (ItemSlot.Chest, stage.xMax + 8, 1), (ItemSlot.Boots, stage.xMax + 8, 2),
            };
            Ui.Row(row);
            foreach (var (slot, columnX, line) in places)
            {
                var area = new Rect(columnX, doll.y + 4 + line * (cell + CellGap), cell, cell);
                var item = _client.Equipment[slot];
                if (item == null)
                {
                    EmptyCell(area, ItemText.SlotName(slot));
                    continue;
                }
                if (Cell(area, Sprites.Icon(item.Base), ItemText.Name(item), item.Upgrade > 0 ? $"+{item.Upgrade}" : null, Palette.Of(item.Rarity), _picked == item.Id, ItemTooltip(item)))
                    Pick(item.Id, null);
            }
        }

        // ---- The stats, the life skills, the pet ----

        void DrawSheetStats()
        {
            var stats = _client.Stats;
            // A weapon's added elements after the blow's own damage: " + 화염 6.2".
            string added = string.Concat(World.ElementOrder.Where(e => stats.AddedFor(e) > 0).Select(e => $" + {StatText.TagName(e)} {stats.AddedFor(e):0.#}"));
            var attack = new List<(string[], string, string)>
            {
                (GrowthArt.Sword, $"{stats.Attack.Name} 피해", $"{stats.Damage:0.#}{added}"),
                (GrowthArt.Speed, "초당 공격", $"{stats.AttacksPerSecond:0.##}"),
                (GrowthArt.Target, "치명타 확률", $"{stats.CritChance:0.#}%"),
                (GrowthArt.Target, "치명타 피해", $"×{stats.CritDamage:0}%"),
            };
            // What each damaging skill would hit for now: the same tag rules as the host, so swapping gear or starting a
            // buff shows on every skill.
            foreach (var skill in _client.Skills.Where(s => s.Damage > 0))
                attack.Add((GrowthArt.Rank, skill.Name, $"{stats.DamageFor(stats.ContextFor(skill)) * skill.Damage / 100f * SkillRules.DamageScale(_client.PowerOf(skill)):0.#}"));
            var defence = new List<(string[], string, string)>
            {
                (UiIcons.Heart, "생명력", $"{stats.MaxHealth:0}"),
                (GrowthArt.Plus, "생명력 재생", $"{stats.HealthRegen:0.#}/초"),
                (GrowthArt.Shield, "방어도", $"{stats.Armor:0}"),
            };
            var mana = new List<(string[], string, string)>
            {
                (GrowthArt.Drop, "마나", $"{stats.MaxMana:0}"),
                (GrowthArt.Drop, "마나 재생", $"{stats.ManaRegen:0.#}/초"),
                (GrowthArt.Hourglass, "스킬 재사용 속도", $"+{stats.CooldownRecoveryFor(SkillTag.None):0}%"),
                (GrowthArt.Hourglass, "스킬 지속 시간", $"+{stats.SkillDurationFor(SkillTag.None):0}%"),
            };
            // The elements (the game has no resistances): what each adds to a blow, and the skill ranks the tree and gear add.
            var elements = new List<(string[], string, string)>();
            foreach (var element in World.ElementOrder)
                elements.Add((GrowthArt.GlyphOf(StatType.Damage, element), $"{StatText.TagName(element)} 추가 피해", stats.AddedFor(element) > 0 ? $"+{stats.AddedFor(element):0.#}" : "-"));
            foreach (var tag in new[] { SkillTag.Melee, SkillTag.Ranged, SkillTag.Fire, SkillTag.Cold, SkillTag.Lightning, SkillTag.Summon })
            {
                int ranks = stats.SkillRankFor(tag);
                if (ranks > 0) elements.Add((GrowthArt.Rank, $"{StatText.TagName(tag)} 스킬 단계", $"+{ranks}"));
            }
            var other = new List<(string[], string, string)>
            {
                (GrowthArt.Boot, "이동 속도", $"{stats.MoveSpeed:0.#}"),
                (GrowthArt.Pick, "채굴 속도", $"{stats.MiningSpeed:0.#}"),
                (GrowthArt.Gem, "아이템 희귀도", $"+{stats.ItemRarity:0}%"),
            };
            var groups = new List<(string Title, Color Color, List<(string[], string, string)> Rows)>
            {
                ("공격", ToColor(GrowthArt.Attack), attack),
                ("방어", ToColor(GrowthArt.Defence), defence),
                ("마나·스킬", ToColor(GrowthArt.Magic), mana),
                ("원소", new Color(1f, 0.6f, 0.3f), elements),
                ("이동·기타", ToColor(GrowthArt.Other), other),
            };
            // Two columns where the pane is wide enough, the taller group first in each pair.
            bool two = _textWidth >= 500;
            float gap = 8;
            float width = two ? Mathf.Floor((_textWidth - gap) / 2) : _textWidth;
            for (int i = 0; i < groups.Count; i += two ? 2 : 1)
            {
                int rows = groups[i].Rows.Count;
                if (two && i + 1 < groups.Count) rows = Math.Max(rows, groups[i + 1].Rows.Count);
                var room = Room(28 + rows * 18 + 6);
                StatGroup(new Rect(room.x, room.y, width, room.height), groups[i]);
                if (two && i + 1 < groups.Count) StatGroup(new Rect(room.x + width + gap, room.y, width, room.height), groups[i + 1]);
                GUILayout.Space(gap);
            }
            // The pet before the life skills, as on the old tab (its name field within sight when the sheet opens).
            DrawPet();
            DrawLifeSkills();
        }

        // A group of stats on a card: its name in its colour, then a glyph, the stat's name and its value a row.
        void StatGroup(Rect r, (string Title, Color Color, List<(string[] Glyph, string Name, string Value)> Rows) group)
        {
            if (Event.current.type == EventType.Repaint)
            {
                UiSkin.Inset(r);
                Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, 3), new Color(group.Color.r, group.Color.g, group.Color.b, 0.8f));
            }
            Write(new Rect(r.x + 10, r.y + 6, r.width - 20, 18), group.Title, group.Color);
            float y = r.y + 28;
            var label = Style(12, Ink);
            var value = Style(12, new Color(1f, 0.9f, 0.65f), TextAnchor.MiddleRight);
            foreach (var (glyph, name, number) in group.Rows)
            {
                var row = new Rect(r.x + 8, y, r.width - 16, 18);
                // The playtests read "휘두르기 피해 15 + 화염 6" as a person reads the row.
                Ui.Wrap($"{name} {number}", label, 99999);
                if (Event.current.type == EventType.Repaint)
                {
                    if (glyph != null)
                    {
                        var size = UiSkin.IconSize(glyph);
                        UiSkin.Icon(new Rect(row.x, row.center.y - size.y / 2, size.x, size.y), glyph, new Color(group.Color.r, group.Color.g, group.Color.b, 0.9f));
                    }
                    GUI.Label(new Rect(row.x + 22, row.y, row.width - 22, row.height), name, label);
                    GUI.Label(row, number, value);
                }
                y += 18;
            }
        }

        // Each life skill's level and bar to the next, what it gives, and its special perks: picked, or two to pick from
        // once the level is reached (World.Life.cs, 2026-11-09).
        void DrawLifeSkills()
        {
            GUILayout.Space(6);
            var head = Room(22);
            Write(head, "생활 기술", UiSkin.Gold);
            Fill(new Rect(head.x, head.yMax - 1, head.width, 1), new Color(UiSkin.Gold.r, UiSkin.Gold.g, UiSkin.Gold.b, 0.3f));
            GUILayout.Space(4);
            foreach (LifeSkill skill in Enum.GetValues(typeof(LifeSkill)))
            {
                int experience = _client.LifeExperience[(int)skill];
                int level = LifeSkills.LevelOf(experience);
                bool top = level >= LifeSkills.MaxLevel;
                int into = experience - LifeSkills.TotalFor(level);
                int span = top ? 1 : LifeSkills.TotalFor(level + 1) - LifeSkills.TotalFor(level);
                string next = top ? "최고" : $"{into}/{span}";
                var line = Room(20);
                string title = $"{LifeSkills.Name(skill)} {level}";
                Write(new Rect(line.x, line.y, 110, line.height), title, Ink);
                UiSkin.Bar(new Rect(line.x + 110, line.y + 3, Mathf.Max(40, line.width - 110), line.height - 6), top ? 1 : into / (float)Math.Max(1, span), next, textSize: 12, experience: true);
                GUILayout.Label(Fit($"{LifeSkills.Name(skill)} {level} ({next}) · {LifeSkills.Perk(skill, level)}", _textWidth));
                for (int tier = 0; tier < LifeSkills.PerkTiers; tier++)
                {
                    int picked = _client.LifePerks[LifeSkills.PerkIndex(skill, tier)];
                    int at = LifeSkills.PerkLevels[tier];
                    if (picked != 0)
                    {
                        var (name, text) = LifeSkills.PerkOf(skill, tier, picked);
                        GUILayout.Label(Fit($"  {at}레벨 혜택: {name} - {text}", _textWidth));
                        continue;
                    }
                    if (level < at) continue;
                    GUILayout.Label(Fit($"  {at}레벨 혜택을 하나 고른다(바꿀 수 없다)", _textWidth));
                    for (int choice = 1; choice <= 2; choice++)
                    {
                        var (name, text) = LifeSkills.PerkOf(skill, tier, choice);
                        float width = _textWidth - 80;
                        string words = $"  {name}: {text}";
                        Ui.Row(words);
                        var row = Room(Mathf.Max(GrowthButton, TextHeight(words, width)) + 2);
                        Write(new Rect(row.x, row.y, width, row.height), words, Good, wrap: true);
                        if (Ui.Button(new Rect(row.xMax - 72, row.y + (row.height - GrowthButton) / 2, 72, GrowthButton), "고르기", Ui.Look.Primary))
                            Send(_client.ChoosePerkCommand(skill, tier, choice), Sound.Learn);
                    }
                }
                GUILayout.Space(6);
            }
        }

        // The pet (Docs/Animals.md 펫 키우기): its picture, level and bar to the next, what it gives now, and its name.
        void DrawPet()
        {
            GUILayout.Space(6);
            var head = Room(22);
            Write(head, "펫", UiSkin.Gold);
            Fill(new Rect(head.x, head.yMax - 1, head.width, 1), new Color(UiSkin.Gold.r, UiSkin.Gold.g, UiSkin.Gold.b, 0.3f));
            GUILayout.Space(4);
            var pet = _client.Pet;
            if (pet == null)
            {
                GUILayout.Label(Fit("펫 없음 · 몬스터가 가끔 떨어뜨리는 알을 [재료·제작]의 [부화]로 깨운다", _textWidth));
                return;
            }
            int level = _client.PetLevel;
            _client.PetExperience.TryGetValue(pet.Id, out int had);
            bool top = level >= Pets.MaxLevel;
            int into = had - Pets.TotalFor(level);
            int span = top ? 1 : Pets.TotalFor(level + 1) - Pets.TotalFor(level);
            string next = top ? "최고 레벨" : $"경험 {into}/{span}";
            string gives = string.Join(", ", Pets.ModifiersAt(pet, level).Select(StatText.Describe));
            if (pet.Bites) gives += $", 곁의 몬스터를 {Pets.BiteSeconds:0}초마다 문다(피해의 {Pets.BiteShareAt(level) * 100:0}%)";
            string called = _client.PetName(pet);
            string kind = called == pet.Name ? "" : $"({pet.Name}) ";
            var card = Room(76);
            var well = new Rect(card.x, card.y, 76, 76);
            if (Event.current.type == EventType.Repaint)
            {
                UiSkin.SlotFrame(well, new Color(0.55f, 0.85f, 0.4f));
                Picture(well, PetSprite(pet), 3);
            }
            float x = well.xMax + 10;
            Write(new Rect(x, card.y + 2, card.xMax - x, 18), $"{called} {kind}", new Color(1f, 0.86f, 0.45f));
            Write(new Rect(x, card.y + 2, card.xMax - x, 18), $"Lv {level}", Ink, align: TextAnchor.MiddleRight);
            UiSkin.Bar(new Rect(x, card.y + 24, card.xMax - x, 14), top ? 1 : into / (float)Math.Max(1, span), next, textSize: 12, experience: true);
            float gaveHeight = Mathf.Min(34, TextHeight(gives, card.xMax - x));
            Write(new Rect(x, card.y + 42, card.xMax - x, gaveHeight), gives, Good, wrap: true);
            GUILayout.Label(Fit($"펫 {called} {kind}Lv {level} ({next}) · {gives}. 함께 싸우면 자란다", _textWidth));
            // Its name (2026-11-09): typed, or one suggested (a phone may have no keyboard up). The field stands just left
            // of [이름 짓기] (the playtests click it there).
            if (_petNameFor != pet.Id)
            {
                _petNameFor = pet.Id;
                _petName = _client.PetNames.TryGetValue(pet.Id, out var given) ? given : "";
            }
            float button = GrowthButton;
            var row = Room(button);
            Write(new Rect(row.x, row.y, 40, row.height), "이름", Ink);
            var field = new Rect(row.x + 40, row.y + (row.height - 24) / 2, 140, 24);
            var name = new Rect(field.xMax + 4, row.y, 88, button);
            var suggest = new Rect(name.xMax + 4, row.y, 56, button);
            // Through the page's own text box on a PC's browser, so a Korean name can be typed (WebText).
            _petName = WebText.Field(field, _petName ?? "", Pets.NameMost, PetNameControl, GUI.skin.textField);
            bool typing = GUI.GetNameOfFocusedControl() == PetNameControl;
            // Esc lets go of the field (the game's own Esc waits while typing).
            if (typing && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                GUIUtility.keyboardControl = 0;
                Event.current.Use();
            }
            bool enter = WebText.Submitted(PetNameControl);
            Ui.Row("펫");
            if (Ui.Button(name, "이름 짓기") || enter)
            {
                _client.NamePet(pet, _petName);
                _petName = _client.PetNames.TryGetValue(pet.Id, out var kept) ? kept : "";
                GUIUtility.keyboardControl = 0;
                Sounds.Play(Sound.Learn);
            }
            if (Ui.Button(suggest, "추천"))
            {
                var names = Pets.SuggestedNames;
                _petName = names[(Array.IndexOf(names.ToArray(), _petName) + 1 + UnityEngine.Random.Range(0, names.Count - 1)) % names.Count];
            }
        }

        // The field's name, as IMGUI and the playtests know it.
        const string PetNameControl = "펫 이름 칸";
        string _petNameFor;
        string _petName;

        // Whether the pet's name field has the keyboard, so walking and hotkeys keep still while typing.
        public bool Typing => Open && _typing;
        bool _typing;

        Sprite PetSprite(PetDefinition pet)
        {
            var look = _client.MonsterKinds?.Monsters.FirstOrDefault(m => m.Id == pet.Look);
            var color = look != null && ColorUtility.TryParseHtmlString(look.Color, out var parsed) ? Sprites.ToRgba(parsed) : Rgba.Hex("#8CD94D");
            return Sprites.Centered($"PetIcon/{pet.Look}", () => CreatureArt.Monster(pet.Look, color, 0));
        }
    }
}
