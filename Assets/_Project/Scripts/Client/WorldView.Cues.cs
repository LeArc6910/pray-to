using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // ---- The boss bundle R1b (2026-11-28, Docs/BossRedesign.md 8, Docs/Bosses.md 늦은 공격·납도 반격·색 표지) on the
    // screen. A late blow (boost hold) waits past its telegraph by a roll only the host knows: its mark fills in the
    // telegraph's time and then blinks while it waits, and the boss holds its wind-up just short of the swing until the
    // host's cue (SimEventType.Cue), CueSeconds before it lands, when the hilt flashes or the foot stamps and the swing
    // comes on the beat. The samurai's noto counter shows a blossom over it while it waits; the knight's colour blows a
    // red or blue sign; and the knight's helmet comes off into the dust as its last phase begins. ----
    sealed partial class WorldView
    {
        int _bossPhaseInView = 1;

        // For the playtests: cues seen, the sign over the boss ("sakura", "near", "far" or ""), and the fill of the last
        // late blow's mark drawn (1 while it waits) with whether it blinked.
        public int CuesSeen { get; private set; }
        public string BadgeShown { get; private set; } = "";
        public float HeldFill { get; private set; } = -1;
        public bool HeldBlinked { get; private set; }
        // The last boss mark weighed for a late blow: attack, phase, hold, its ticks and its telegraph's (for the sweep).
        public string HeldWhy { get; private set; } = "";

        // Seconds into a wind-up as it is drawn: a late blow's runs to CueSeconds short of its telegraph and stays there
        // until the cue, then on; anything else as it is. A repeat (BossAgain) comes on its own beat.
        float WindSince(Body body, BossAttack attack, float since)
        {
            if (attack == null || attack.Hold <= 0 || body.BossAgain) return since;
            float stop = Mathf.Max(0, attack.Telegraph - World.CueSeconds);
            return body.CueAt >= body.BossSince ? stop + (Time.time - body.CueAt) : Mathf.Min(since, stop);
        }

        // A mark's fill as drawn: a late blow's fills over its telegraph (not the longer wait the host rolled) and then
        // stays full, blinking (HazardArt.BlinkFrom), until it lands. A mark no longer than its telegraph (a repeat's, a
        // charge's later run) is as it was.
        float HeldProgress(HazardState hazard, float progress)
        {
            var sheet = AttackOf(hazard);
            var attack = sheet?.ForPhase(_bossPhaseInView);
            if (sheet != null) HeldWhy = $"{sheet.Id}:p{_bossPhaseInView}:h{attack.Hold}:d{hazard.Duration}:t{World.Ticks(attack.Telegraph)}";
            // An order's marks fill together (R4a, WorldView.Order.cs).
            if (attack != null && attack.Shape == AttackShape.Order) return OrderProgress(hazard, attack, progress);
            if (attack == null || attack.Hold <= 0) return progress;
            float seconds = progress * hazard.Duration / World.TicksPerSecond;
            if (hazard.Duration <= World.Ticks(attack.Telegraph) + 1) return progress;
            float fill = Mathf.Clamp01(seconds / Mathf.Max(0.1f, attack.Telegraph));
            HeldFill = fill;
            if (fill > HazardArt.BlinkFrom) HeldBlinked = true;
            return fill;
        }

        void ShowCue(SimEvent e)
        {
            var body = Find(e.Source);
            if (body == null) return;
            // An interlude beginning or over (R4a).
            if (e.Amount == World.CueInterlude || e.Amount == World.CueInterludeOver)
            {
                ShowInterlude(e, body);
                return;
            }
            // Images formed, a blow from behind, a rewind (R4b).
            if (ShowMirrorCue(e, body)) return;
            CuesSeen++;
            body.CueAt = Time.time;
            var feet = body.Position;
            if (e.Amount == World.CueCounter)
            {
                // The noto counter struck: a white flash at the scabbard's mouth and the blossom scattered.
                body.Countered = true;
                AddFlash(feet + body.Hip, 0.9f, Color.white, FlashKind.Impact);
                Petals(feet + new Vector3(0, 0.8f, 0), Vector3.zero, 14, 4.5f, 0.6f, 0.4f);
                Hear(Sound.Heavy, feet, 1);
                return;
            }
            if (body.Id == "shrine_samurai")
            {
                // The hilt catches the light: the beat to dodge on.
                AddFlash(feet + body.Hip, 0.7f, Color.white, FlashKind.Impact);
                body.Glinted = true;
            }
            else if (body.Id == "black_knight") Thud(body, 0.14f, 0.9f);
            else AddFlash(feet + new Vector3(0, body.Top, 0), 0.5f, Color.white, FlashKind.Impact);
        }

        // The noto stance's frame: into it, then still but for its breathing.
        static int SheatheFrame(float since)
        {
            int settle = SamuraiArt.SheatheSettle;
            int all = SamuraiArt.FramesOf(SamuraiPose.Sheathe);
            float left = since;
            for (int f = 0; f < settle; f++)
            {
                left -= SamuraiArt.FrameSeconds(SamuraiPose.Sheathe, f);
                if (left < 0) return f;
            }
            int loop = Mathf.Max(1, all - settle);
            return settle + Mathf.FloorToInt(left / Mathf.Max(0.05f, SamuraiArt.FrameSeconds(SamuraiPose.Sheathe, settle))) % loop;
        }

        // The sign over a boss while it winds up: the blossom over the noto stance (do not strike), red over a blow
        // to come in close for, blue over one to get away from.
        void ShowBadge(Body body, EntityView view)
        {
            var attack = view.BossAttack;
            string kind = "";
            if (view.BossState == BossState.Telegraph && attack != null)
            {
                if (attack.Shape == AttackShape.Counter && !body.Countered) kind = "sakura";
                else if (attack.Warning == AttackWarning.Near) kind = "near";
                else if (attack.Warning == AttackWarning.Far) kind = "far";
            }
            if (kind.Length == 0)
            {
                if (body.Badge != null) body.Badge.enabled = false;
                return;
            }
            BadgeShown = kind;
            int frame;
            Sprite sprite;
            if (kind == "sakura")
            {
                frame = Mathf.FloorToInt(Time.time * 6) % Mathf.Max(1, EffectArt.SakuraSignFrames);
                sprite = Sprites.Centered($"fx/sakura-sign/{frame}", () => EffectArt.SakuraSign(frame));
            }
            else
            {
                bool near = kind == "near";
                frame = Mathf.FloorToInt(Time.time * 8) % Mathf.Max(1, EffectArt.ColorSignFrames);
                sprite = Sprites.Centered($"fx/color-sign/{(near ? "near" : "far")}/{frame}", () => EffectArt.ColorSign(near, frame));
            }
            if (body.Badge == null)
            {
                body.Badge = Renderer(body.Root, "Badge", sprite, Color.white);
                body.Badge.sortingOrder = 21000;
                HearWarning(body.Position);
            }
            else if (!body.Badge.enabled) HearWarning(body.Position);
            body.Badge.sprite = sprite;
            body.Badge.enabled = true;
            body.Badge.transform.localPosition = new Vector3(0, body.Top + 0.55f + Mathf.Sin(Time.time * 6) * 0.05f, 0);
        }

        // The knight's last phase: its helmet comes off and rolls into the dust beside it, and lies there a while.
        void PhaseShown(SimEvent e)
        {
            var body = Find(e.Source);
            if (body == null || body.Id != "black_knight" || e.Amount < 3) return;
            var at = body.Position + new Vector3(body.Shape.flipX ? -0.9f : 0.9f, -0.2f, 0);
            var helmet = Renderer(_root.transform, "Helmet", Sprites.Centered("knight/helmet", KnightArt.Helmet), Color.white);
            helmet.transform.localPosition = at;
            helmet.sortingOrder = FootOrder(at.y);
            Bits(at, Vector3.up * 0.5f, DustColor, 4);
            Hear(Sound.Thump, at, 0.7f);
            HelmetsDropped++;
            Later(30, () => { if (helmet != null) Object.Destroy(helmet.gameObject); });
        }

        public int HelmetsDropped { get; private set; }
    }
}
