using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The skills screen (K, Docs/UiRework.md 4-3 스킬, U4), as Diablo-like skill pages lay one out:
    //  - along the top the four slots as the skill bar shows them (icon, key, rank), each with [바꾸기] (pick the slot,
    //    then the learned skill for it), and the points left in large gold with [되돌리기 (금화 N)] in red;
    //  - under it each chosen class's six skills in the order they open, as cards (icon, name, rank pips, "Lv 8부터"
    //    under a lock) with [배우기]/[올리기] and [변형]; on a phone held upright or a low screen a list of rows;
    //  - beside them the picked skill's panel: what it does now and at the next rank, side by side, its mana and
    //    cooldown, [배우기]/[올리기], [칸에 넣기] (then a slot: the skill goes there, swapping with one already in a
    //    slot), and its crystal slots with [빼기] and the crystals in the bag to put in ("… 끼우기"). On a phone the
    //    panel is a sheet over the list, opened by tapping a skill, with [목록으로] back.
    //  - While a class slot is open the classes to pick come first as cards (a hero with the class's weapon, its colour,
    //    passive and first skills, [고르기], [미리보기]); beside a workbench, changing the classes at the bottom.
    // The rows the playtests read keep their words ("회전베기 · 1단계/5", "전사: …", "N번 칸에: …").
    sealed partial class InventoryWindow
    {
        // The skill whose panel shows; a learned one waiting for the slot it goes in ([칸에 넣기]); the phone's sheet.
        SkillDefinition _skillPicked;
        SkillDefinition _placing;
        bool _skillSheet;
        // [변형] scrolls the panel to the crystals once laid out.
        bool _supportsFirst;
        float _supportsAt;
        Vector2 _skillListScroll, _skillDetailScroll;
        // The slot being filled from the learned skills not in a slot ([바꾸기]), or -1.
        int _swapSlot = -1;
        // The classes picked for a change beside a workbench but not yet taken, as ids; null shows the ones the player
        // has (Docs/Classes.md, 게임 중에 바꾸기).
        List<string> _pickedClasses;
        int _pickedForKit = -1;

        // Where the skills screen's parts go in the area under the tabs (also for the play mode layout tests).
        internal readonly struct SkillLayout
        {
            public SkillLayout(Rect bar, Rect slots, Rect points, Rect list, Rect detail, bool side, bool cards, int perRow, bool stacked, float slot)
            {
                Bar = bar;
                Slots = slots;
                Points = points;
                List = list;
                Detail = detail;
                Side = side;
                Cards = cards;
                PerRow = perRow;
                Stacked = stacked;
                Slot = slot;
            }

            // The slots and the points along the top; the slots' part and the points' part of it.
            public Rect Bar { get; }
            public Rect Slots { get; }
            public Rect Points { get; }
            // The classes' skills; the picked one's panel (beside the list, or on a phone the sheet over it).
            public Rect List { get; }
            public Rect Detail { get; }
            public bool Side { get; }
            // Cards in rows of PerRow, or a list of rows.
            public bool Cards { get; }
            public int PerRow { get; }
            // [바꾸기] under each slot (a phone held upright), not beside it.
            public bool Stacked { get; }
            public float Slot { get; }
        }

        const float SwapWidth = 60;

        internal static SkillLayout LaySkills(Rect body)
        {
            float button = GrowthButton;
            float slot = Ui.Short ? 40 : 48;
            float cell = slot + 4 + SwapWidth;
            bool wide = body.width >= 4 * (cell + 8) + 250;
            Rect slots, points, bar;
            if (wide)
            {
                float height = Mathf.Max(slot, button);
                slots = new Rect(body.x, body.y, 4 * (cell + 8) - 8, height);
                points = Rect.MinMaxRect(slots.xMax + 12, body.y, body.xMax, body.y + height);
                bar = new Rect(body.x, body.y, body.width, height);
            }
            else
            {
                slots = new Rect(body.x, body.y, body.width, slot + 4 + button);
                points = new Rect(body.x, slots.yMax + 6, body.width, button);
                bar = Rect.MinMaxRect(body.x, body.y, body.xMax, points.yMax);
            }
            var rest = Rect.MinMaxRect(body.x, bar.yMax + (Ui.Short ? 6 : 10), body.xMax, body.yMax);
            bool side = body.width >= 640;
            Rect list = rest, detail = rest;
            if (side)
            {
                float width = Mathf.Clamp(Mathf.Round(body.width * 0.34f), 270, 380);
                detail = Rect.MinMaxRect(rest.xMax - width, rest.y, rest.xMax, rest.yMax);
                list = Rect.MinMaxRect(rest.x, rest.y, detail.x - 10, rest.yMax);
            }
            float inner = list.width - 22;
            float least = Controls.UsingTouch ? 128 : 118;
            bool cards = side && !Ui.Short && inner >= 3 * least;
            int perRow = !cards ? 1 : inner >= 6 * least ? 6 : 3;
            return new SkillLayout(bar, slots, points, list, detail, side, cards, perRow, !wide, slot);
        }

        void DrawSkillsScreen(Rect body)
        {
            if (_pickedForKit != _client.KitVersion)
            {
                _pickedClasses = null;
                _pickedForKit = _client.KitVersion;
            }
            var lay = LaySkills(body);
            var all = ClassRules.AllSkillsFor(_client.Classes);
            if (_skillPicked != null && !all.Contains(_skillPicked)) _skillPicked = null;
            _skillPicked ??= _client.Skills.FirstOrDefault() ?? all.FirstOrDefault();
            if (_placing != null && (_client.RankOf(_placing) == 0 || !all.Contains(_placing))) _placing = null;
            if (_swapSlot >= _client.Skills.Count) _swapSlot = -1;
            if (_skillPicked == null) _skillSheet = false;

            bool sheet = !lay.Side && _skillSheet;
            if (lay.Side) TouchPanes(lay.List, ref _skillListScroll, lay.Detail, ref _skillDetailScroll);
            else if (sheet) Ui.TouchScroll(lay.Detail, ref _skillDetailScroll);
            else Ui.TouchScroll(lay.List, ref _skillListScroll);

            DrawSlotBar(lay);
            DrawSkillPoints(lay.Points);
            // Before any class there is no skill to tell of: the classes' cards take the whole width.
            bool none = _client.Classes.Count == 0;
            var list = none && lay.Side ? Rect.MinMaxRect(lay.List.x, lay.List.y, lay.Detail.xMax, lay.List.yMax) : lay.List;
            if (!sheet || none)
            {
                bool cards = lay.Cards;
                int perRow = lay.PerRow;
                bool side = lay.Side;
                _skillListScroll = Pane(list, _skillListScroll, () => DrawSkillList(cards, perRow, side));
            }
            if (!none && (lay.Side || sheet)) DrawSkillDetail(lay.Detail, sheet);
        }

        // ---- The slots and the points ----

        void DrawSlotBar(SkillLayout lay)
        {
            var skills = _client.Skills;
            var all = ClassRules.AllSkillsFor(_client.Classes);
            int learned = all.Count(s => _client.RankOf(s) > 0);
            var e = Event.current;
            Ui.Row("스킬 칸");
            for (int i = 0; i < ClassRules.SkillSlots; i++)
            {
                var (cell, swap) = SlotRects(lay, i);
                var skill = i < skills.Count ? skills[i] : null;
                string key = Controls.UsingTouch ? $"{i + 1}" : Controls.SkillKeyName(i);
                // [바꾸기] first: it takes its press before the slot under it would.
                if (skill != null)
                {
                    GUI.enabled = learned > skills.Count;
                    if (Ui.Button(swap, _swapSlot == i ? "그만" : "바꾸기"))
                    {
                        _swapSlot = _swapSlot == i ? -1 : i;
                        _placing = null;
                        _skillSheet = false;
                    }
                    GUI.enabled = true;
                }
                Ui.Probe($"스킬 칸 {i + 1}", cell);
                bool hover = cell.Contains(e.mousePosition) && (!Controls.UsingTouch || Ui.Held);
                if (e.type == EventType.Repaint)
                {
                    var tint = skill != null ? Palette.Of(skill.Tags) : (Color?)null;
                    UiSkin.SlotFrame(cell, tint);
                    if (skill != null) Picture(cell, Sprites.Icon(skill), 2);
                    else Write(cell, "빈 칸", UiSkin.Muted, align: TextAnchor.MiddleCenter);
                    Write(new Rect(cell.x + 3, cell.yMax - 15, 20, 14), key, Ink, shadow: true);
                    if (skill != null && _client.PowerOf(skill) > 1)
                        Write(new Rect(cell.x, cell.y + 1, cell.width - 3, 14), SkillBar.RankMark(_client.PowerOf(skill)), new Color(1f, 0.85f, 0.4f), align: TextAnchor.UpperRight, shadow: true);
                    if (_placing != null && skill != null)
                    {
                        float breath = UiMotion.Reduced ? 1 : 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6);
                        Outline(new Rect(cell.x - 3, cell.y - 3, cell.width + 6, cell.height + 6), new Color(1f, 0.85f, 0.4f, breath), 2);
                    }
                    else if (skill != null && skill == _skillPicked) Outline(new Rect(cell.x - 2, cell.y - 2, cell.width + 4, cell.height + 4), new Color(1f, 0.9f, 0.6f), 2);
                    else if (hover) Outline(new Rect(cell.x - 2, cell.y - 2, cell.width + 4, cell.height + 4), new Color(1, 1, 1, 0.6f), 2);
                }
                if (skill != null) Hover(cell, SkillTooltip(skill));
                if (!GUI.Button(cell, GUIContent.none, GUIStyle.none) || skill == null) continue;
                UiMotion.Confirm();
                if (_placing != null) Place(_placing, i);
                else
                {
                    _skillPicked = skill;
                    _skillSheet = true;
                    _swapSlot = -1;
                    // Its actions ([올리기], [칸에 넣기]) are at the top of the detail: show them, not where a
                    // short screen was left scrolled (the crystals, after [변형]).
                    _skillDetailScroll = Vector2.zero;
                }
            }
        }

        // Where a slot and its [바꾸기] go in the bar: beside each other, or [바꾸기] under the slot on a phone held upright.
        internal static (Rect Cell, Rect Swap) SlotRects(SkillLayout lay, int slot)
        {
            float button = GrowthButton;
            float size = lay.Slot;
            if (lay.Stacked)
            {
                float width = (lay.Slots.width - 3 * 8) / 4;
                float x = lay.Slots.x + slot * (width + 8);
                float swapWidth = Mathf.Min(width, 76);
                var cell = new Rect(Mathf.Round(x + (width - size) / 2), lay.Slots.y, size, size);
                return (cell, new Rect(Mathf.Round(x + (width - swapWidth) / 2), cell.yMax + 4, swapWidth, button));
            }
            float left = lay.Slots.x + slot * (size + 4 + SwapWidth + 8);
            var beside = new Rect(left, lay.Slots.y + (lay.Slots.height - size) / 2, size, size);
            return (beside, new Rect(beside.xMax + 4, lay.Slots.y + (lay.Slots.height - button) / 2, SwapWidth, button));
        }

        // The skill into the slot: swapped with the slot's when it is in another slot already, else in its place.
        void Place(SkillDefinition skill, int slot)
        {
            var all = ClassRules.AllSkillsFor(_client.Classes);
            var order = _client.SkillOrder.ToList();
            int at = _client.Skills.ToList().IndexOf(skill);
            _placing = null;
            if (at == slot || slot >= order.Count) return;
            if (at >= 0) (order[at], order[slot]) = (order[slot], order[at]);
            else order[slot] = all.IndexOf(skill);
            Send(_client.ClassCommand(_client.Classes, order), Sound.Click);
        }

        void DrawSkillPoints(Rect area)
        {
            float button = GrowthButton;
            if (_placing != null)
            {
                // Waiting for a slot: the skill's name, what to do, and [그만].
                var stop = new Rect(area.xMax - 72, area.y + (area.height - button) / 2, 72, button);
                Write(Rect.MinMaxRect(area.x, area.y, stop.x - 8, area.yMax), $"{_placing.Name}: 넣을 칸을 누른다", new Color(1f, 0.85f, 0.45f), wrap: true);
                Ui.Row("칸에 넣기");
                if (Ui.Button(stop, "그만")) _placing = null;
                return;
            }
            int points = _client.SkillPoints;
            // Every learned skill forgotten for coins, the points back to spend again (World.ResetSkills).
            int cost = _client.SkillResetCost;
            var coin = _client.Loot.FindMaterial(World.Coin);
            float resetWidth = Controls.UsingTouch ? 156 : 146;
            var reset = new Rect(area.xMax - resetWidth, area.y + (area.height - button) / 2, resetWidth, button);
            if (_client.Classes.Count > 0)
            {
                GUI.enabled = cost > 0 && coin != null && _client.Bag.CountOf(coin) >= cost;
                Ui.Row("스킬 포인트");
                if (Ui.Button(reset, cost > 0 ? $"되돌리기 (금화 {cost})" : "되돌리기", Ui.Look.Danger)) Send(_client.ResetSkillsCommand(), Sound.Click);
                GUI.enabled = true;
            }
            // The points in large gold (4-3: 스킬 포인트는 위 오른쪽), the word and where they come from beside.
            var number = Style(24, points > 0 ? new Color(1f, 0.84f, 0.35f) : UiSkin.Muted);
            string count = points.ToString();
            float width = number.CalcSize(new GUIContent(count)).x;
            var gem = UiSkin.IconSize(GrowthArt.Point);
            float x = area.x;
            UiSkin.Icon(new Rect(x, area.center.y - gem.y / 2, gem.x, gem.y), GrowthArt.Point, points > 0 ? UiSkin.Gold : UiSkin.Muted);
            x += gem.x + 6;
            Write(new Rect(x, area.y, width + 4, area.height), count, points > 0 ? new Color(1f, 0.84f, 0.35f) : UiSkin.Muted, 24, shadow: true);
            x += width + 8;
            var words = Rect.MinMaxRect(x, area.y, reset.x - 8, area.yMax);
            if (words.width > 60)
                Write(words, area.height >= 44 ? $"스킬 포인트 {points}\nLv {_client.Level} · 레벨마다 1" : $"스킬 포인트 {points} · 레벨마다 1", points > 0 ? Ink : UiSkin.Muted, wrap: true);
        }

        // ---- The list: the classes to pick, each class's skills, changing classes ----

        void DrawSkillList(bool cards, int perRow, bool side)
        {
            if (_swapSlot >= 0)
            {
                DrawSwapChoices();
                return;
            }
            // Classes are picked here (2026-11-09, Docs/Classes.md 클래스 고르기): none at first, a slot open at level 1,
            // a second at 10 and a third at 20; a pick into an open slot is free, anywhere.
            int classSlots = ClassRules.SlotsAt(_client.Level);
            int nextSlot = ClassRules.NextSlotLevel(_client.Level);
            if (_client.Classes.Count < classSlots) DrawClassCards(classSlots);
            else
                GUILayout.Label(Fit(nextSlot > 0
                    ? $"클래스 {_client.Classes.Count}/{ClassRules.DefaultMaxClasses} · 레벨 {nextSlot}에 하나 더 고를 수 있다"
                    : $"클래스 {_client.Classes.Count}/{ClassRules.DefaultMaxClasses}", _textWidth));
            if (_client.Classes.Count == 0) return;

            float gap = 8;
            float cardHeight = Controls.UsingTouch ? 112 : 100;
            float rowHeight = Controls.UsingTouch ? 54 : 46;
            foreach (var owner in _client.Classes)
            {
                GUILayout.Space(6);
                ClassBand(owner);
                if (cards)
                {
                    float width = Mathf.Floor((_textWidth - gap * (perRow - 1)) / perRow);
                    for (int start = 0; start < owner.Skills.Count; start += perRow)
                    {
                        var row = Room(cardHeight);
                        for (int j = 0; j < perRow && start + j < owner.Skills.Count; j++)
                            SkillCard(owner.Skills[start + j], new Rect(row.x + j * (width + gap), row.y, width, cardHeight), true, side);
                        GUILayout.Space(gap);
                    }
                }
                else
                {
                    foreach (var skill in owner.Skills)
                    {
                        SkillCard(skill, Room(rowHeight), false, side);
                        GUILayout.Space(4);
                    }
                }
            }
            DrawClassChange(classSlots);
        }

        // A band over a class's skills: its colour, "전사 스킬", and its passive.
        void ClassBand(ClassDefinition owner)
        {
            var band = Room(24);
            var color = ToColor(GrowthArt.ClassColor(owner));
            Fill(new Rect(band.x, band.y + 2, 4, band.height - 4), color);
            Fill(new Rect(band.x + 4, band.yMax - 2, band.width - 4, 1), new Color(color.r, color.g, color.b, 0.35f));
            string title = $"{owner.Name} 스킬";
            float width = Style(12, color).CalcSize(new GUIContent(title)).x;
            Ui.Row(title);
            Write(new Rect(band.x + 10, band.y, width + 4, band.height - 2), title, color);
            Write(Rect.MinMaxRect(band.x + 20 + width, band.y, band.xMax, band.yMax - 2), "패시브: " + SkillText.Passive(owner), Good);
        }

        // A skill as a card (big icon, name, rank pips, lock) or a row (on a phone or a low screen), with [배우기]/[올리기]
        // and [변형]; pressing it elsewhere shows its panel (on a phone, the sheet).
        void SkillCard(SkillDefinition skill, Rect r, bool card, bool side)
        {
            int rank = _client.RankOf(skill);
            int power = _client.PowerOf(skill);
            bool open = _client.Level >= skill.Level;
            var held = _client.GemsIn(skill);
            string state = rank > 0 ? $"{RankText(skill)}/{SkillRules.MaxRank}" : open ? "배우지 않음" : $"Lv {skill.Level}부터";
            if (held.Count > 0) state += $" · 결정 {held.Count}/{SkillSupports.SlotsFor(rank)}";
            Ui.Row($"{skill.Name} · {state}");
            if (!side) Hover(r, SkillTooltip(skill));
            var e = Event.current;
            bool picked = skill == _skillPicked;
            bool hover = r.Contains(e.mousePosition) && (!Controls.UsingTouch || Ui.Held);
            var tag = Palette.Of(skill.Tags);
            float button = GrowthButton;
            Rect icon, learn, support;
            if (card)
            {
                icon = new Rect(r.x + 6, r.y + 24, 40, 40);
                float buttons = r.width - 12;
                float learnWidth = Mathf.Round((buttons - 4) * 0.56f);
                learn = new Rect(r.x + 6, r.yMax - button - 6, learnWidth, button);
                support = Rect.MinMaxRect(learn.xMax + 4, learn.y, r.xMax - 6, learn.yMax);
            }
            else
            {
                icon = new Rect(r.x + 4, r.y + (r.height - 40) / 2, 40, 40);
                float supportWidth = Controls.UsingTouch ? 56 : 50;
                float learnWidth = Controls.UsingTouch ? 68 : 60;
                support = new Rect(r.xMax - 4 - supportWidth, r.y + (r.height - button) / 2, supportWidth, button);
                learn = new Rect(support.x - 4 - learnWidth, support.y, learnWidth, button);
            }
            if (e.type == EventType.Repaint)
            {
                UiSkin.Inset(r);
                Fill(new Rect(r.x + 2, r.y + 2, card ? r.width - 4 : 3, card ? 3 : r.height - 4), new Color(tag.r, tag.g, tag.b, rank > 0 ? 0.9f : 0.35f));
                if (picked) Outline(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), new Color(1f, 0.85f, 0.4f), 2);
                else if (hover) Outline(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), new Color(1, 1, 1, 0.45f), 1);
                UiSkin.SlotFrame(icon, rank > 0 ? tag : (Color?)null);
                var previous = GUI.color;
                if (rank == 0) GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * (open ? 0.75f : 0.4f));
                Picture(icon, Sprites.Icon(skill), 2);
                GUI.color = previous;
                if (!open)
                {
                    var lockSize = UiSkin.IconSize(UiIcons.Lock);
                    UiSkin.Icon(new Rect(icon.xMax - lockSize.x - 1, icon.yMax - lockSize.y - 1, lockSize.x, lockSize.y), UiIcons.Lock, new Color(0.85f, 0.8f, 0.7f));
                }
            }
            var nameColor = rank > 0 ? (picked ? new Color(1f, 0.88f, 0.5f) : Ink) : open ? new Color(0.85f, 0.8f, 0.72f) : UiSkin.Muted;
            var stateColor = rank > 0 ? Good : open ? UiSkin.Muted : Bad;
            string stateShown = rank > 0 ? RankText(skill) : open ? "배우지 않음" : $"Lv {skill.Level}부터";
            if (card)
            {
                Write(new Rect(r.x + 8, r.y + 5, r.width - 14, 16), skill.Name, nameColor);
                float textX = icon.xMax + 6;
                Pips(new Vector2(textX, icon.y + 4), rank, Math.Max(0, power - rank), SkillRules.MaxRank);
                Write(new Rect(textX, icon.y + 14, r.xMax - textX - 4, 14), stateShown, stateColor);
                if (rank > 0)
                    Write(new Rect(textX, icon.y + 28, r.xMax - textX - 4, 14), $"결정 {held.Count}/{SkillSupports.SlotsFor(rank)}", UiSkin.Muted);
            }
            else
            {
                float textX = icon.xMax + 8;
                var text = Rect.MinMaxRect(textX, r.y + 4, learn.x - 6, r.yMax - 4);
                Write(new Rect(text.x, text.y, text.width, 16), skill.Name, nameColor);
                Pips(new Vector2(text.x, text.y + 21), rank, Math.Max(0, power - rank), SkillRules.MaxRank);
                Write(new Rect(text.x + 5 * 8 + 6, text.y + 16, text.width - 46, 16), stateShown + (held.Count > 0 ? $" · 결정 {held.Count}" : ""), stateColor);
            }
            GUI.enabled = _client.CanRaise(skill);
            string label = rank >= SkillRules.MaxRank ? "최고" : rank > 0 ? "올리기" : "배우기";
            if (Ui.Button(learn, label, rank == 0 && GUI.enabled ? Ui.Look.Primary : Ui.Look.Normal))
            {
                _skillPicked = skill;
                Send(_client.LearnSkillCommand(skill), Sound.Click);
            }
            GUI.enabled = rank > 0;
            if (Ui.Button(support, "변형"))
            {
                _skillPicked = skill;
                _skillSheet = true;
                _supportsFirst = true;
            }
            GUI.enabled = true;
            // The rest of the card picks the skill (its buttons above took their presses first).
            Ui.Probe(skill.Name, r);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                UiMotion.Confirm();
                _skillPicked = skill;
                _skillSheet = true;
                _skillDetailScroll = Vector2.zero;
            }
        }

        // With a slot picked ([바꾸기]): the learned skills not in a slot, each a button that puts it there.
        void DrawSwapChoices()
        {
            var all = ClassRules.AllSkillsFor(_client.Classes);
            var unused = all.Where(s => _client.RankOf(s) > 0 && !_client.Skills.Contains(s)).ToList();
            GUILayout.Label(Fit($"{_swapSlot + 1}번 칸({(Controls.UsingTouch ? $"{_swapSlot + 1}" : Controls.SkillKeyName(_swapSlot))})에 넣을 배운 스킬을 고른다", _textWidth));
            foreach (var skill in unused)
            {
                GUILayout.BeginHorizontal();
                Icon(Sprites.Icon(skill));
                if (WrapButton($"{_swapSlot + 1}번 칸에: {skill.Name}", _textWidth - IconRoom))
                {
                    var order = _client.SkillOrder.ToList();
                    order[_swapSlot] = all.IndexOf(skill);
                    _swapSlot = -1;
                    Send(_client.ClassCommand(_client.Classes, order), Sound.Click);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(6);
            if (Ui.LayoutButton("그만", GUILayout.Width(80), GUILayout.Height(GrowthButton))) _swapSlot = -1;
        }

        // ---- The picked skill's panel ----

        void DrawSkillDetail(Rect pane, bool sheet)
        {
            var skill = _skillPicked;
            if (Event.current.type == EventType.Repaint)
            {
                if (sheet) UiSkin.Card(pane);
                else UiSkin.Inset(pane);
            }
            float top = pane.y + 6;
            if (sheet)
            {
                Ui.Row("고른 스킬");
                if (Ui.Button(new Rect(pane.x + 8, top, 104, GrowthButton), "목록으로"))
                {
                    _skillSheet = false;
                    return;
                }
                top += GrowthButton + 6;
            }
            if (skill == null)
            {
                Write(new Rect(pane.x + 10, top, pane.width - 20, 60), "스킬을 누르면 여기에 자세히 나온다", UiSkin.Muted, wrap: true);
                return;
            }
            int rank = _client.RankOf(skill);
            int power = _client.PowerOf(skill);
            var tag = Palette.Of(skill.Tags);
            // The head: the icon three times its size, the name, the class and tags, the rank.
            var icon = new Rect(pane.x + 10, top + 2, 56, 56);
            if (Event.current.type == EventType.Repaint)
            {
                UiSkin.SlotFrame(icon, tag, glow: rank >= SkillRules.MaxRank);
                var previous = GUI.color;
                if (rank == 0) GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * 0.7f);
                Picture(icon, Sprites.Icon(skill), 3);
                GUI.color = previous;
            }
            float x = icon.xMax + 10;
            Write(new Rect(x, top, pane.xMax - x - 8, 18), skill.Name, new Color(1f, 0.85f, 0.45f));
            string tags = skill.Tags == SkillTag.None ? "" : " · " + StatText.TagNames(skill.Tags);
            Write(new Rect(x, top + 18, pane.xMax - x - 8, 16), skill.Class.Name + tags, tag);
            Pips(new Vector2(x, top + 40), rank, Math.Max(0, power - rank), SkillRules.MaxRank, 8);
            Write(new Rect(x + 5 * 10 + 6, top + 36, pane.xMax - x - 62, 16),
                rank > 0 ? RankText(skill) : _client.Level >= skill.Level ? "배우지 않음" : $"Lv {skill.Level}부터", rank > 0 ? Good : UiSkin.Muted);
            var body = Rect.MinMaxRect(pane.x + 4, icon.yMax + 8, pane.xMax - 2, pane.yMax - 4);
            _skillDetailScroll = Pane(body, _skillDetailScroll, () => DrawSkillDetailBody(skill));
            if (_supportsFirst && Event.current.type == EventType.Repaint)
            {
                _supportsFirst = false;
                _skillDetailScroll.y = Mathf.Max(0, _supportsAt - 4);
            }
        }

        void DrawSkillDetailBody(SkillDefinition skill)
        {
            int rank = _client.RankOf(skill);
            int power = Math.Max(1, _client.PowerOf(skill));
            var supports = _client.SupportsOf(skill);
            GUILayout.Label(Ui.Wrap(skill.Description, GUI.skin.label, _textWidth));
            GUILayout.Space(4);
            // This rank and the next side by side (4-3: 숫자 설명, 다음 단계 비교), a part a line: what changes in green.
            var now = SkillText.Numbers(skill, power, _client.Stats, supports).Split(new[] { " · " }, StringSplitOptions.None);
            bool raising = rank > 0 && rank < SkillRules.MaxRank;
            var next = raising ? SkillText.Numbers(skill, power + 1, _client.Stats, supports).Split(new[] { " · " }, StringSplitOptions.None) : null;
            bool paired = next != null && next.Length == now.Length;
            float half = Mathf.Floor((_textWidth - 8) / 2);
            var head = Room(18);
            Write(new Rect(head.x, head.y, paired ? half : head.width, head.height), rank > 0 ? $"지금 ({RankText(skill)})" : "배우면 (1단계)", UiSkin.Gold);
            if (paired) Write(new Rect(head.x + half + 8, head.y, half, head.height), $"다음 ({rank + 1}단계)", UiSkin.Gold);
            for (int i = 0; i < now.Length; i++)
            {
                float height = Mathf.Max(TextHeight(now[i], paired ? half : _textWidth), paired ? TextHeight(next[i], half) : 0) + 2;
                var line = Room(height);
                if (i % 2 == 0) Fill(line, new Color(1, 1, 1, 0.035f));
                Write(new Rect(line.x, line.y, paired ? half : line.width, line.height), now[i], Ink, wrap: true);
                if (paired) Write(new Rect(line.x + half + 8, line.y, half, line.height), next[i], next[i] != now[i] ? Good : UiSkin.Muted, wrap: true);
            }
            if (next != null && !paired) GUILayout.Label(Ui.Wrap("다음 단계: " + string.Join(" · ", next), GUI.skin.label, _textWidth));
            GUILayout.Space(8);

            // [배우기]/[올리기] and [칸에 넣기]: the screen's main action, and the way to a slot.
            var buttons = Room(GrowthButton);
            float learnWidth = Mathf.Min(120, (buttons.width - 6) / 2);
            Ui.Row("고른 스킬");
            GUI.enabled = _client.CanRaise(skill);
            if (Ui.Button(new Rect(buttons.x, buttons.y, learnWidth, buttons.height), rank >= SkillRules.MaxRank ? "최고" : rank > 0 ? "올리기" : "배우기", Ui.Look.Primary))
                Send(_client.LearnSkillCommand(skill), Sound.Click);
            bool inSlot = _client.Skills.Contains(skill);
            GUI.enabled = rank > 0 && (!inSlot || _client.Skills.Count > 1);
            if (Ui.Button(new Rect(buttons.x + learnWidth + 6, buttons.y, Mathf.Min(120, buttons.width - learnWidth - 6), buttons.height), _placing == skill ? "그만" : "칸에 넣기"))
            {
                _placing = _placing == skill ? null : skill;
                _swapSlot = -1;
            }
            GUI.enabled = true;
            if (inSlot) GUILayout.Label(Fit($"{_client.Skills.ToList().IndexOf(skill) + 1}번 칸에 들어 있다", _textWidth));
            GUILayout.Space(8);
            if (Event.current.type == EventType.Repaint) _supportsAt = GUILayoutUtility.GetLastRect().yMax;
            DrawSupports(skill);
        }

        // The skill's crystal slots (one, two from rank 3, three at rank 5): each crystal in it with [빼기], the
        // slots still shut, and while one is free, the crystals in the bag that would go in, each a button
        // (Docs/Classes.md 스킬 변형).
        void DrawSupports(SkillDefinition skill)
        {
            int rank = _client.RankOf(skill);
            int slots = SkillSupports.SlotsFor(rank);
            var held = _client.GemsIn(skill);
            var title = Room(20);
            Ui.Row($"{skill.Name}의 변형");
            Write(title, rank > 0 ? $"변형 결정 {held.Count}/{slots}" : "변형 결정", UiSkin.Gold);
            Fill(new Rect(title.x, title.yMax - 1, title.width, 1), new Color(UiSkin.Gold.r, UiSkin.Gold.g, UiSkin.Gold.b, 0.3f));
            if (rank == 0)
            {
                GUILayout.Label(Fit("배우면 결정 칸이 열린다(1단계에 한 칸, 3단계에 둘, 5단계에 셋)", _textWidth));
                return;
            }
            float button = GrowthButton;
            for (int slot = 0; slot < SkillSupports.MaxSlots; slot++)
            {
                if (slot < held.Count && SkillSupports.KindOf(held[slot]) is SupportKind kind)
                {
                    string text = $"{slot + 1}칸 {held[slot].Name}: {SkillSupports.Describe(kind, skill)}";
                    float width = _textWidth - IconRoom - 64;
                    var line = Room(Mathf.Max(button, TextHeight(text, width)) + 4);
                    var well = new Rect(line.x, line.y + (line.height - 28) / 2, 28, 28);
                    UiSkin.SlotFrame(well, Palette.Rune);
                    Picture(well, Sprites.Icon(held[slot]), 1);
                    Write(new Rect(line.x + IconRoom + 4, line.y, width, line.height), text, Ink, wrap: true);
                    if (Ui.Button(new Rect(line.xMax - 58, line.y + (line.height - button) / 2, 58, button), "빼기")) Send(_client.RemoveSupportCommand(skill, slot), Sound.Click);
                }
                else
                {
                    var line = Room(22);
                    bool shut = slot >= slots;
                    var well = new Rect(line.x, line.y + 1, 20, 20);
                    UiSkin.SlotFrame(well, null);
                    if (shut) UiSkin.Icon(well, UiIcons.Lock, UiSkin.Muted);
                    Write(new Rect(line.x + 28, line.y, line.width - 28, line.height),
                        shut ? $"{slot + 1}칸은 스킬 {SkillSupports.RankForSlot(slot)}단계부터" : $"{slot + 1}칸 비어 있다", shut ? UiSkin.Muted : Ink);
                }
            }
            if (held.Count >= slots) return;
            var kinds = _client.SupportsOf(skill);
            var gems = _client.Bag.Materials.Keys.Where(m => SkillSupports.KindOf(m) is SupportKind k && SkillSupports.CanAdd(skill, rank, kinds, k)).ToList();
            GUILayout.Space(4);
            if (gems.Count == 0)
            {
                GUILayout.Label(Fit("끼울 결정이 가방에 없다. 모루에서 강화 정수로 만든다(확산·분열·가속·증폭·화염·냉기·번개·관통·지속·흡혈)", _textWidth));
                return;
            }
            foreach (var gem in gems)
            {
                GUILayout.BeginHorizontal();
                Icon(Sprites.Icon(gem));
                if (WrapButton($"{gem.Name} 끼우기: {SkillSupports.Describe(SkillSupports.KindOf(gem).Value, skill)}", _textWidth - IconRoom))
                    Send(_client.SupportCommand(skill, gem), Sound.Craft);
                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }
        }

        // ---- Picking classes ----

        // The class whose skills are shown before it is picked.
        ClassDefinition _previewClass;

        // The classes not taken yet as cards: a hero holding the class's weapon in its colour, the name, what it is, its
        // passive, its first skills, [고르기] (free) and [미리보기]. The first at the start, more as the level opens slots.
        void DrawClassCards(int slots)
        {
            GUILayout.Label(Fit(_client.Classes.Count == 0
                ? $"클래스를 하나 고른다(무료). 그 클래스의 첫 스킬을 바로 쓴다. 레벨 {ClassRules.SlotLevels[1]}에 둘째, {ClassRules.SlotLevels[2]}에 셋째를 더 고른다. 고른 뒤 다른 것으로 바꾸려면 작업대 곁에서 금화를 낸다"
                : $"클래스 칸이 열렸다({_client.Classes.Count}/{slots}): 하나 더 고른다(무료)", _textWidth));
            float button = GrowthButton;
            foreach (var option in _client.AllClasses.Where(c => !_client.Classes.Contains(c)))
            {
                string first = option.Skills.Count > 0 ? $" · 첫 스킬 {option.Skills[0].Name}" : "";
                string row = $"{option.Name}: {option.Description} · 패시브: {SkillText.Passive(option)}{first}";
                var color = ToColor(GrowthArt.ClassColor(option));
                float buttons = 84;
                float textWidth = _textWidth - 92 - buttons - 12;
                string passive = "패시브: " + SkillText.Passive(option);
                float textHeight = 18 + TextHeight(option.Description, textWidth) + TextHeight(passive, textWidth) + 32;
                float height = Mathf.Max(Mathf.Max(92, 2 * button + 16), textHeight + 10);
                var r = Room(height);
                Ui.Row(row);
                if (Event.current.type == EventType.Repaint)
                {
                    UiSkin.Inset(r);
                    Fill(new Rect(r.x + 2, r.y + 2, 3, r.height - 4), color);
                    var well = new Rect(r.x + 8, r.y + 6, 76, Mathf.Min(r.height - 12, 80));
                    UiSkin.Inset(well);
                    Fill(new Rect(well.x + 2, well.y + 2, well.width - 4, well.height - 4), new Color(color.r, color.g, color.b, 0.18f));
                    var hero = Sprites.Centered($"classhero/{option.Id}", () => GrowthArt.Hero(HeroGear.Plain(GrowthArt.WeaponOf(option)), WorldView.SelfTunic));
                    Picture(well, hero, 3);
                }
                float x = r.x + 92;
                float y = r.y + 6;
                Write(new Rect(x, y, textWidth, 18), option.Name, color);
                y += 18;
                float h = TextHeight(option.Description, textWidth);
                Write(new Rect(x, y, textWidth, h), option.Description, Ink, wrap: true);
                y += h;
                h = TextHeight(passive, textWidth);
                Write(new Rect(x, y, textWidth, h), passive, Good, wrap: true);
                y += h + 4;
                // The first three skills, the first one's at once.
                for (int i = 0; i < Math.Min(3, option.Skills.Count); i++)
                {
                    var cell = new Rect(x + i * 30, y, 26, 26);
                    UiSkin.SlotFrame(cell, Palette.Of(option.Skills[i].Tags));
                    Picture(cell, Sprites.Icon(option.Skills[i]), 1);
                    Hover(cell, $"{option.Skills[i].Name} · {(i == 0 ? "고르면 바로" : $"Lv {option.Skills[i].Level}부터")}");
                }
                var pick = new Rect(r.xMax - buttons - 6, r.y + 6, buttons, button);
                if (Ui.Button(pick, "고르기", Ui.Look.Primary))
                    Send(_client.ClassCommand(_client.Classes.Concat(new[] { option }).ToList(), null), Sound.Buff);
                // Every skill of the class before picking it (2026-11-09, 사람이 고른 다음 작업 후보 6번).
                if (Ui.Button(new Rect(pick.x, pick.yMax + 4, buttons, button), _previewClass == option ? "닫기" : "미리보기"))
                {
                    _previewClass = _previewClass == option ? null : option;
                    Sounds.Play(Sound.Click);
                }
                if (_previewClass == option) DrawClassPreview(option);
                GUILayout.Space(6);
            }
            GUILayout.Space(4);
        }

        // A class's six skills as they would come: each with its icon, the level it opens at and what it does at the
        // first rank with the stats the player has now.
        void DrawClassPreview(ClassDefinition option)
        {
            float width = _textWidth - IconRoom - 16;
            foreach (var skill in option.Skills)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(16);
                Icon(Sprites.Icon(skill));
                string opens = skill == option.Skills[0] ? "고르면 바로" : $"Lv {skill.Level}부터";
                GUILayout.Label(Fit($"{skill.Name} · {opens}\n{SkillText.Numbers(skill, 1, _client.Stats, Array.Empty<SupportKind>())}", width), GUILayout.Width(width));
                GUILayout.EndHorizontal();
            }
        }

        // Beside a workbench: the classes picked again in order (a class given up costs coins, ClassRules.ChangeCoins).
        void DrawClassChange(int classSlots)
        {
            GUILayout.Space(10);
            var band = Room(22);
            Fill(new Rect(band.x, band.yMax - 1, band.width, 1), new Color(UiSkin.Gold.r, UiSkin.Gold.g, UiSkin.Gold.b, 0.3f));
            Write(band, "클래스 바꾸기", UiSkin.Gold);
            bool near = _client.Map != null && _client.Map.Near(_client.Position, TileType.Workbench, World.StationReach);
            int changeCoins = ClassRules.ChangeCoins(_client.Level);
            GUILayout.Label(Fit(near
                ? $"클래스 바꾸기(고른 순서대로 칸을 채운다. 고른 클래스를 내주면 금화 {changeCoins}, 순서만 바꾸기는 무료. 배운 스킬은 남는다)"
                : $"고른 클래스를 다른 것으로 바꾸려면 작업대 곁에서(금화 {changeCoins}). 빈 칸에 더하기는 어디서나 무료", _textWidth));
            _pickedClasses ??= _client.Classes.Select(c => c.Id).ToList();
            int most = Math.Max(classSlots, _client.Classes.Count);
            float button = GrowthButton;
            int perRow = Math.Max(1, (int)((_textWidth + 6) / 150));
            var options = _client.AllClasses.ToList();
            for (int start = 0; start < options.Count; start += perRow)
            {
                var row = Room(button);
                float width = (row.width - 6 * (perRow - 1)) / perRow;
                for (int j = 0; j < perRow && start + j < options.Count; j++)
                {
                    var option = options[start + j];
                    int picked = _pickedClasses.IndexOf(option.Id);
                    bool full = picked < 0 && _pickedClasses.Count >= most;
                    GUI.enabled = near && !full;
                    var rect = new Rect(row.x + j * (width + 6), row.y, width, row.height);
                    Hover(rect, option.Description);
                    if (Ui.Button(rect, (picked >= 0 ? $"[{picked + 1}] " : "[ ] ") + option.Name, picked >= 0 ? Ui.Look.Primary : Ui.Look.Normal))
                    {
                        if (picked >= 0) _pickedClasses.RemoveAt(picked);
                        else _pickedClasses.Add(option.Id);
                        Sounds.Play(Sound.Click);
                    }
                }
                GUILayout.Space(4);
            }
            bool differs = !_pickedClasses.SequenceEqual(_client.Classes.Select(c => c.Id));
            // Giving a class up costs coins (World.ChangeClasses); the same ones in another order, or one more, do not.
            bool givesUp = _client.Classes.Any(c => !_pickedClasses.Contains(c.Id));
            int changeCost = givesUp ? changeCoins : 0;
            var gold = _client.Loot.FindMaterial(World.Coin);
            bool affordable = changeCost == 0 || gold != null && _client.Bag.CountOf(gold) >= changeCost;
            GUI.enabled = near && differs && affordable;
            var change = Room(button);
            if (Ui.Button(new Rect(change.x, change.y, Mathf.Min(change.width, 220), change.height), changeCost > 0 ? $"이대로 바꾸기 (금화 {changeCost})" : "이대로 바꾸기"))
            {
                var classes = _pickedClasses.Select(id => _client.AllClasses.First(c => c.Id == id)).ToList();
                Send(_client.ClassCommand(classes, null), Sound.Buff);
            }
            GUI.enabled = true;
            GUILayout.Space(6);
        }

        // "2단계", or "2+1단계" when the passive tree or gear add a rank to it.
        string RankText(SkillDefinition skill)
        {
            int rank = _client.RankOf(skill);
            int bonus = _client.PowerOf(skill) - rank;
            return bonus > 0 ? $"{rank}+{bonus}단계" : $"{rank}단계";
        }

        // A skill's description and numbers at the rank it works at, and what the next learned rank would make them.
        string SkillTooltip(SkillDefinition skill)
        {
            int rank = _client.RankOf(skill);
            int power = Math.Max(1, _client.PowerOf(skill));
            string tags = skill.Tags == SkillTag.None ? "" : $" · {StatText.TagNames(skill.Tags)}";
            var supports = _client.SupportsOf(skill);
            string text = $"{skill.Name} ({skill.Class.Name}){tags}\n{skill.Description}\n{SkillText.Numbers(skill, power, _client.Stats, supports)}";
            if (rank > 0 && rank < SkillRules.MaxRank) text += $"\n다음 단계: {SkillText.Numbers(skill, power + 1, _client.Stats, supports)}";
            var gems = _client.GemsIn(skill);
            if (gems.Count > 0) text += $"\n변형: {string.Join(", ", gems.Select(g => g.Name))}";
            return text;
        }
    }
}
