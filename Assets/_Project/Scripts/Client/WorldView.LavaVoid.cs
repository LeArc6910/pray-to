using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The lava fields' and the void abyss's rules on the screen (2026-11-25, Docs/BiomeRework.md B4b): a steam vent's
    // hiss under a thin red rim and its burst (Hazard.Steam, drawn with the monsters' marks in WorldView.MonsterFx), a
    // cracked plate cracking under whoever stands on it (worked out here from where the players stand, as the host does:
    // World.PlateHold), a plate giving way and a fall into the void (World.MonsterFxCrumble, World.MonsterFxFall), and the
    // abyss's dimmer torches (LavaVoidLight).
    sealed partial class WorldView
    {
        // Since the game began: vents seen hissing and bursting, plates seen giving way, falls, a plate coming back; and
        // the plates drawn cracking last frame (the playtests).
        public int VentsShown { get; private set; }
        public int VentBurstsShown { get; private set; }
        public int CrumblesShown { get; private set; }
        public int FallsShown { get; private set; }
        public int PlatesBackShown { get; private set; }
        public int CracksShown { get; private set; }
        // The lights dimmed in the abyss last time the dark was worked out (LavaVoidLight).
        public int AbyssLightsDimmed { get; private set; }

        // How long each plate has been stood on as the screen sees it (seconds, by tile key), and those stood on this frame.
        readonly Dictionary<int, float> _plateStood = new Dictionary<int, float>();
        readonly HashSet<int> _platesUnder = new HashSet<int>();
        readonly List<int> _platesLeft = new List<int>();

        static bool IsSteam(HazardState hazard) => hazard.MonsterShape + 1 == Hazard.Steam;

        // A vent's mark starts: its hiss, once.
        void SteamArmed(Mark mark, Vector3 at)
        {
            if (mark.Armed) return;
            mark.Armed = true;
            VentsShown++;
            Hear(Sound.Hiss, at, 0.45f);
        }

        // A vent burst: the column of steam and its sound.
        void SteamBurst(HazardState hazard)
        {
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            AddMobFx(MobFx.Vent, at, new MobContext { Size = (hazard.Size + World.PlayerRadius) * Ppu });
            VentBurstsShown++;
            Hear(Sound.Boom, at, 0.35f);
        }

        // What the host says of the plates: true when it was one of theirs.
        bool LavaVoidEvent(SimEvent e, Vector3 at)
        {
            switch (e.Amount)
            {
                case World.MonsterFxCrumble:
                    AddMobFx(MobFx.Crumble, at - new Vector3(0, 0.3f, 0), new MobContext());
                    Hear(Sound.Shatter, at, 0.7f);
                    CrumblesShown++;
                    if (Vector3.Distance(at, _self.Position) < ShakeRange) _shake = Mathf.Max(_shake, 0.08f);
                    return true;
                case World.MonsterFxFall:
                    AddMobFx(MobFx.Fall, at - new Vector3(0, 0.3f, 0), new MobContext());
                    Hear(Sound.Down, at, 0.8f);
                    FallsShown++;
                    if (e.Source == _client.PlayerId) _shake = Mathf.Max(_shake, 0.15f);
                    return true;
                case World.MonsterFxPlate:
                    PlatesBackShown++;
                    Hear(Sound.Place, at, 0.35f);
                    return true;
            }
            return false;
        }

        // The plates under the players' feet, cracking as they stand: the rim closes in over World.PlateHold.
        void DrawPlateCracks()
        {
            int shown = 0;
            _platesUnder.Clear();
            var map = _client.Map;
            void Under(Vector3 position)
            {
                int x = Mathf.FloorToInt(position.x), y = Mathf.FloorToInt(position.y);
                if (map.InBounds(x, y) && map[x, y] == TileType.CrackedPlate) _platesUnder.Add(y * map.Width + x);
            }
            Under(_self.Position);
            foreach (var body in _others.Values)
                if (body.Kind == BodyKind.Player) Under(body.Position);
            _platesLeft.Clear();
            foreach (int key in _plateStood.Keys)
                if (!_platesUnder.Contains(key)) _platesLeft.Add(key);
            foreach (int key in _platesLeft) _plateStood.Remove(key);
            foreach (int key in _platesUnder)
            {
                float stood = (_plateStood.TryGetValue(key, out float before) ? before : 0) + Time.deltaTime;
                _plateStood[key] = stood;
                if (!Feel.MonsterFx) continue;
                _fxStamps.Clear();
                MonsterFx.Stamps(MobFx.Crack, new MobContext { Progress = stood / World.PlateHold, Seed = key & 0xFFFF }, Time.time, _fxStamps);
                EmitFx(new Vector3(key % map.Width + 0.5f, key / map.Width + 0.5f, 0), 1, null);
                shown++;
            }
            CracksShown = shown;
        }
    }
}
