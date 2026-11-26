using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // ---- A boss coming in (2026-11-17, Docs/Bosses.md 입장 연출; the person: "the bosses made lately should have an
    // entrance, its motion and its staging"). The host decides it (BossState.Enter, arenas.csv): for a moment the
    // woken boss neither attacks nor can be hurt, the same for everyone in the game. Here it is shown: the view goes
    // over to the boss and back, a card names it (its name, what it is called, the level it is made for when that is
    // over the player's), and the boss makes its entrance: the four made lately each a motion of their own (up
    // through the floor, down a thread, up from sitting, out of a kneel and the hammer brought down), the rest a
    // common stir fitted to how each appears (arenas.csv appear). Seen before, it is short: no card, the view stays.
    // With the menu's "보스 등장 연출" off the view stays and the card gives way to the old banner. ----
    sealed partial class WorldView
    {
        sealed class EntranceShow
        {
            public int BossId;
            public BossDefinition Boss;
            public float Start;
            public float Seconds;
            public Vector3 At;
        }

        EntranceShow _entrance;
        GUIStyle _cardName;
        GUIStyle _cardTitle;

        // How long the card stays after the boss has come in, and how long the view takes to go over and to come back
        // (shares of the entrance).
        const float CardLingers = 0.7f;
        const float FocusIn = 0.18f;
        const float FocusOut = 0.2f;
        // How far toward the boss the view goes, and how much of the half screen the player may be left from its middle.
        const float FocusShare = 0.8f;
        const float FocusKeeps = 0.62f;

        // A boss with a motion of its own asleep in its first frame, or coming in.
        static bool Entering(Body body) => body.OwnEntrance && (body.BossState == BossState.Dormant || body.BossState == BossState.Enter);

        // The frame of its entrance now: asleep, the first of all; coming in, through the span evenly.
        static int EnterFrame(Body body, (int From, int To) span)
        {
            if (body.BossState == BossState.Dormant)
            {
                body.EnterFrame = -1;
                return 0;
            }
            float progress = Mathf.Clamp01((Time.time - body.BossSince) / body.EnterSeconds);
            body.EnterFrame = Mathf.Min(span.To, span.From + Mathf.FloorToInt(progress * (span.To - span.From + 1)));
            return body.EnterFrame;
        }

        // The whole entrance of a boss just woken starts its show (the card, the view); the short one and one with the
        // show turned off do not, and the caller announces the boss as before.
        bool BeginEntrance(EntityView view)
        {
            var arena = view.Boss.Arena;
            if (view.BossState != BossState.Enter || arena == null || view.EntersAgain || !Settings.BossIntro) return false;
            _entrance = new EntranceShow
            {
                BossId = view.Id, Boss = view.Boss, Start = Time.time, Seconds = Mathf.Max(0.5f, arena.Seconds),
                At = new Vector3(view.Position.X, view.Position.Y, 0),
            };
            return true;
        }

        // The entrance on show now, for the playtests: the boss's id, or null.
        public string EntranceShown => _entrance?.Boss.Id;
        // Whether the card's name and title, as last drawn, fit the screen's width.
        public bool EntranceFits { get; private set; } = true;

        // Where the view looks: at the player, and while a boss comes in, most of the way over to it, as far as keeps
        // the player on the screen (a phone held upright shows little to either side).
        Vector3 EntranceFocus(Vector3 self)
        {
            if (_entrance == null) return self;
            float progress = (Time.time - _entrance.Start) / _entrance.Seconds;
            if (progress >= 1 + CardLingers / _entrance.Seconds)
            {
                _entrance = null;
                return self;
            }
            float weight = Mathf.Clamp01(Mathf.Min(progress / FocusIn, (1 - progress) / FocusOut));
            weight = weight * weight * (3 - 2 * weight) * FocusShare;
            var offset = (_entrance.At - new Vector3(self.x, self.y, 0)) * weight;
            float halfHeight = _camera.orthographicSize, halfWidth = halfHeight * _camera.aspect;
            offset.x = Mathf.Clamp(offset.x, -halfWidth * FocusKeeps, halfWidth * FocusKeeps);
            offset.y = Mathf.Clamp(offset.y, -halfHeight * FocusKeeps, halfHeight * FocusKeeps);
            return new Vector3(self.x + offset.x, self.y + offset.y, self.z);
        }

        // The card: a dark band across the upper part of the screen, the boss's name large on it, what it is called
        // under that, and under that the level it is made for when the player is short of it. The name is drawn as
        // large as fits the screen's width, so a long name fits a phone held upright.
        void DrawEntranceCard()
        {
            if (_entrance == null || Event.current.type != EventType.Repaint) return;
            float age = Time.time - _entrance.Start;
            float total = _entrance.Seconds + CardLingers;
            float fade = Mathf.Clamp01(Mathf.Min(age / 0.25f, (total - age) / 0.4f));
            if (fade <= 0) return;
            _cardName ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
            _cardTitle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
            string name = _entrance.Boss.Name;
            string title = _entrance.Boss.Arena?.Title ?? "";
            string level = _entrance.Boss.Level > _client.Level ? $"권장 레벨 {_entrance.Boss.Level}" : "";
            float room = Ui.Width - 24;
            int nameSize = 36;
            foreach (int size in new[] { 36, 30, 24, 20, 16 })
            {
                nameSize = size;
                Ui.Text(_cardName, size, bold: true);
                if (_cardName.CalcSize(new GUIContent(name)).x <= room) break;
            }
            int titleSize = 16;
            foreach (int size in new[] { 16, 14, 12 })
            {
                titleSize = size;
                Ui.Text(_cardTitle, size, bold: false);
                if (_cardTitle.CalcSize(new GUIContent(title)).x <= room) break;
            }
            EntranceFits = _cardName.CalcSize(new GUIContent(name)).x <= room && _cardTitle.CalcSize(new GUIContent(title)).x <= room;
            float nameHeight = nameSize * 1.35f, titleHeight = title.Length > 0 ? titleSize * 1.5f : 0, levelHeight = level.Length > 0 ? titleSize * 1.5f : 0;
            float height = nameHeight + titleHeight + levelHeight + 18;
            // Under the boss's health bar and the top of the HUD, over the middle of the view.
            float top = Mathf.Max(64, Ui.Height * 0.2f);
            // The band opens from the middle.
            float open = Mathf.Clamp01(age / 0.3f);
            open = open * open * (3 - 2 * open);
            var before = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.62f * fade);
            GUI.DrawTexture(new Rect(Ui.Width * (1 - open) / 2, top, Ui.Width * open, height), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.82f, 0.4f, 0.8f * fade);
            GUI.DrawTexture(new Rect(Ui.Width * (1 - open) / 2, top, Ui.Width * open, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Ui.Width * (1 - open) / 2, top + height - 1, Ui.Width * open, 1), Texture2D.whiteTexture);
            GUI.color = before;
            float y = top + 9;
            Shadowed(new Rect(0, y, Ui.Width, nameHeight), name, new Color(1f, 0.9f, 0.62f, fade), _cardName);
            y += nameHeight;
            if (title.Length > 0)
            {
                Shadowed(new Rect(0, y, Ui.Width, titleHeight), title, new Color(0.92f, 0.9f, 0.86f, fade), _cardTitle);
                y += titleHeight;
            }
            if (level.Length > 0) Shadowed(new Rect(0, y, Ui.Width, levelHeight), level, new Color(1f, 0.55f, 0.45f, fade), _cardTitle);
        }

        // The card's box on the screen while it shows, for the layout checks and the playtests (zero when none).
        public Rect EntranceCardRect => _entrance == null ? default : new Rect(0, Mathf.Max(64, Ui.Height * 0.2f), Ui.Width, 110);

        static readonly Color SummonColor = new Color(0.66f, 0.72f, 1f);

        // After a boss is placed this frame: the sounds, dust and shakes of its entrance, each once as its frame comes.
        void EntranceExtras(Body body, EntityView view)
        {
            // Under the floor or up in the roof it casts no shadow: asleep, and under the floor until it breaks through.
            var entrance = view.Boss.Arena?.Entrance ?? BossEntrance.Common;
            bool hidden = entrance == BossEntrance.Burrow || entrance == BossEntrance.Thread;
            if (view.BossState == BossState.Dormant && hidden && !view.IsDead) body.Shadow.enabled = false;
            if (view.BossState != BossState.Enter)
            {
                body.EnterCue = -1;
                return;
            }
            if (entrance == BossEntrance.Burrow && body.EnterFrame < VeinEaterArt.EmergeBreak) body.Shadow.enabled = false;
            var feet = body.Position;
            float since = Time.time - body.BossSince;
            bool puff = Time.time >= body.DustAt;
            if (!body.OwnEntrance)
            {
                CommonEntrance(body, view, since);
                return;
            }
            int frame = body.EnterFrame, last = body.EnterCue;
            body.EnterCue = frame;
            // A frame passed since the last one drawn (a slow device may skip some).
            bool Reached(int at) => last < at && frame >= at;
            switch (view.Boss.Arena.Entrance)
            {
                case BossEntrance.Burrow:
                    // The mound heaving: earth trickling, the floor trembling; then the floor breaks.
                    if (frame < VeinEaterArt.EmergeBreak)
                    {
                        if (puff)
                        {
                            body.DustAt = Time.time + 0.09f;
                            Kick(feet + (Vector3)(Random.insideUnitCircle * 0.7f), Vector3.up * 0.6f, DustColor, 1);
                        }
                        if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.02f + 0.04f * frame / VeinEaterArt.EmergeBreak);
                    }
                    if (Reached(2) || Reached(8)) Hear(Sound.Dig, feet, 0.8f);
                    if (Reached(VeinEaterArt.EmergeBreak))
                    {
                        Impact(feet, 0.8f);
                        Cracks(feet, 2.6f, 7);
                        Hear(Sound.Break, feet, 1);
                        Hear(Sound.Boom, feet, 0.8f);
                    }
                    if (Reached(26)) Hear(Sound.Roar, feet, 1);
                    break;

                case BossEntrance.Thread:
                    // Silk drifting down ahead of it; its feet meet the floor; its threat.
                    if (frame < SpiderArt.DescendLands && puff)
                    {
                        body.DustAt = Time.time + 0.12f;
                        Kick(feet + new Vector3(Random.Range(-0.5f, 0.5f), 2.5f, 0), Vector3.down * 0.5f, SilkColor, 1);
                    }
                    if (Reached(SpiderArt.EnterFrames(body.EntersAgain).From + 1)) Hear(Sound.Cast, feet, 0.9f);
                    if (Reached(SpiderArt.DescendLands))
                    {
                        Thud(body, 0.16f, 0.9f);
                        AddFlash(feet, 2.2f, DustColor, FlashKind.Ring);
                    }
                    if (Reached(35))
                    {
                        Hear(Sound.Roar, feet, 0.8f);
                        AddFlash(feet + new Vector3(0, 0.4f, 0), 1.6f, VenomColor, FlashKind.Ring);
                    }
                    break;

                case BossEntrance.Seated:
                    // Still; petals come down round it as it rises; the hand comes to the hilt with a click and a glint.
                    if (puff && Random.value < 0.5f)
                    {
                        body.DustAt = Time.time + 0.1f;
                        var from = feet + new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(1.5f, 3f), 0);
                        Petals(from, new Vector3(Random.Range(-0.4f, 0.4f), -1, 0), 1, 0.9f, 1.4f);
                    }
                    if (Reached(SamuraiArt.EnterFrames(body.EntersAgain).From + 2)) Hear(Sound.Step, feet, 1);
                    if (Reached(SamuraiArt.SeizaGrip))
                    {
                        Hear(Sound.Click, feet, 1);
                        AddFlash(feet + new Vector3(0, 0.5f, 0), 0.7f, Color.white, FlashKind.Impact);
                        Petals(feet + new Vector3(0, 0.5f, 0), Vector3.up, 10, 2.2f, 0.9f, 2f);
                    }
                    break;

                case BossEntrance.Awaken:
                    // Grit trickling off the sleeping stone; its eyes light, then its core; it gets up with a thud,
                    // hauls its fists up and pounds the floor (WardenArt.Enter).
                    if (frame < WardenArt.EnterEyes && puff)
                    {
                        body.DustAt = Time.time + 0.12f;
                        Kick(feet + new Vector3(Random.Range(-0.8f, 0.8f), 1.2f, 0), Vector3.down * 0.4f, DustColor, 1);
                    }
                    if (Reached(2)) Hear(Sound.Break, feet, 0.5f);
                    if (Reached(WardenArt.EnterEyes)) AddFlash(feet + new Vector3(0, 1.9f, 0), 0.6f, AmberColor, FlashKind.Impact);
                    if (Reached(WardenArt.EnterCore))
                    {
                        AddFlash(feet + new Vector3(0, 1.3f, 0), 1.6f, AmberColor, FlashKind.Ring);
                        Hear(Sound.Nova, feet, 0.6f);
                    }
                    if (Reached(WardenArt.EnterStood)) Thud(body, 0.06f, 0.6f);
                    if (!body.EntersAgain && Reached(WardenArt.EnterStrike - 3)) Hear(Sound.Heavy, feet, 1);
                    if (!body.EntersAgain && Reached(WardenArt.EnterStrike))
                    {
                        Impact(feet, 1);
                        Cracks(feet, 2.6f, 7);
                        Hear(Sound.Roar, feet, 0.8f);
                    }
                    break;

                case BossEntrance.Stride:
                    // Up off its knee with a scrape; each stride a thud; the hammer comes down like a wall.
                    if (Reached(2)) Hear(Sound.Heavy, feet, 0.7f);
                    if (Reached(KnightArt.EnterStood)) Thud(body, 0.06f, 0.6f);
                    for (int f = KnightArt.EnterStood + 1; f <= frame; f++)
                        if (KnightArt.EnterStep(f) && Reached(f)) Thud(body, 0.09f, 0.7f);
                    int strike = KnightArt.StrikeFrame(KnightPose.Enter);
                    if (!body.EntersAgain && Reached(strike - 4)) Hear(Sound.Heavy, feet, 1);
                    if (!body.EntersAgain && Reached(strike))
                    {
                        var at = feet + new Vector3(0, -0.5f, 0);
                        Impact(at, 1);
                        Cracks(at, 3f, 8);
                        Hear(Sound.Boom, feet, 1);
                        Hear(Sound.Thump, feet, 1);
                    }
                    break;
            }
        }

        // The entrance of a boss with no motion of its own: it stirs out of the dim it slept in, with a ring of its
        // sort's colour as it wakes and a burst as the fight starts, and what its sort of appearing brings.
        void CommonEntrance(Body body, EntityView view, float since)
        {
            var feet = body.Position;
            float progress = Mathf.Clamp01(since / body.EnterSeconds);
            int step = progress < 0.05f ? 0 : progress < 0.55f ? 1 : progress < 0.9f ? 2 : 3;
            bool Reached(int at) => body.EnterCue < at && step >= at;
            var appearance = view.Boss.Arena?.Appearance ?? BossAppearance.Wait;
            var color = appearance switch
            {
                BossAppearance.Summon => SummonColor,
                BossAppearance.Descend => SilkColor,
                BossAppearance.March => WindupColor,
                _ => DustColor,
            };
            // It rises a little and settles: a slow swell, then a start as it wakes fully.
            float swell = 1 + 0.06f * Mathf.Sin(progress * Mathf.PI) + (step >= 2 ? 0.04f * Mathf.Sin(Time.time * 26) * (1 - progress) : 0);
            body.Shape.transform.localScale *= swell;
            body.Glow.transform.localScale = body.Shape.transform.localScale;
            if (Reached(1))
            {
                AddFlash(feet, body.Diameter * 1.6f, color, FlashKind.Ring);
                switch (appearance)
                {
                    case BossAppearance.Lurk:
                        // Up out of the floor, the water or the sand: it is thrown off.
                        for (int i = 0; i < 6; i++) Kick(feet + (Vector3)(Random.insideUnitCircle * body.Diameter * 0.5f), Random.insideUnitCircle, DustColor, 1);
                        Hear(Sound.Dig, feet, 0.8f);
                        break;
                    case BossAppearance.Summon:
                        AddFlash(feet, body.Diameter * 1.3f, color, FlashKind.Vortex);
                        Hear(Sound.Nova, feet, 0.7f);
                        break;
                    case BossAppearance.March:
                        Thud(body, 0.08f, 0.7f);
                        break;
                    case BossAppearance.Descend:
                        Thud(body, 0.1f, 0.7f);
                        break;
                    default:
                        // Stone waking: grit falling from it.
                        for (int i = 0; i < 4; i++) Kick(feet + new Vector3(Random.Range(-0.5f, 0.5f), 1, 0), Vector3.down * 0.4f, DustColor, 1);
                        Hear(Sound.Break, feet, 0.5f);
                        break;
                }
            }
            if (Reached(2))
            {
                Hear(Sound.Roar, feet, 0.8f);
                if (Settings.ScreenShake) _shake = Mathf.Max(_shake, 0.15f);
                AddFlash(feet, body.Diameter * 2f, color, FlashKind.Ring);
            }
            if (Reached(3)) AddFlash(feet, body.Diameter, Color.white, FlashKind.Burst);
            body.EnterCue = step;
        }
    }
}
