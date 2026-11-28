using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The weapon arts on screen (Docs/Weapons.md 묶음 C): each art event (SimEventType.Art) starts its effect program
    // (SkillFx.Arts.cs), the body's motion, a shake and a sound. A beginning that lasts (a guard, a charge, an aim, a
    // thrown boomerang, an orb's well) is ended by that player's next blow or end. Nothing here decides anything.
    sealed partial class WorldView
    {
        // Each player's lasting beginning, and where a spear's rush began.
        readonly Dictionary<int, Fx> _artFx = new Dictionary<int, Fx>();
        readonly Dictionary<int, Vector3> _artFrom = new Dictionary<int, Vector3>();

        // For the playtests: arts seen (any player's), the last one and its phase.
        public int ArtsShown { get; private set; }
        public WeaponArtKind LastArt { get; private set; }
        public ArtPhase LastArtPhase { get; private set; }

        WeaponArt ArtDefinition(WeaponArtKind kind)
        {
            var kinds = _client.Loot?.WeaponKinds;
            if (kinds == null) return null;
            foreach (var weaponKind in kinds) if (weaponKind.Art?.Kind == kind) return weaponKind.Art;
            return null;
        }

        void ShowArt(SimEvent e)
        {
            var kind = World.ArtKindOf(e.Amount);
            var phase = World.ArtPhaseOf(e.Amount);
            var art = ArtDefinition(kind);
            var body = Find(e.Source);
            bool mine = e.Source == _client.PlayerId;
            ArtsShown++;
            LastArt = kind;
            LastArtPhase = phase;
            var at = new Vector3(e.Position.X, e.Position.Y, 0);

            // A boomerang's blows come while it is still going round; anything else ends what began.
            bool keeps = kind == WeaponArtKind.Whirl && phase == ArtPhase.Strike;
            if (!keeps && phase != ArtPhase.Start && _artFx.TryGetValue(e.Source, out var going))
            {
                _fx.Remove(going);
                _artFx.Remove(e.Source);
            }
            if (phase == ArtPhase.End || art == null) return;
            if (kind == WeaponArtKind.Dash && phase == ArtPhase.Start)
            {
                _artFrom[e.Source] = at;
                return;
            }

            float px = Sprites.PixelsPerUnit;
            var context = new FxContext { Dir = e.Direction.Angle, Main = FxRamp.Steel };
            float feet = body?.Feet ?? 0;
            var origin = (body != null && kind != WeaponArtKind.Gravity && kind != WeaponArtKind.Vital ? body.Position : at) - new Vector3(0, feet, 0);
            switch (kind)
            {
                case WeaponArtKind.Parry:
                case WeaponArtKind.Charge:
                case WeaponArtKind.Wave:
                    context.Radius = (art.Reach + World.PlayerRadius) * px;
                    break;
                case WeaponArtKind.Slam:
                case WeaponArtKind.Whirl:
                case WeaponArtKind.Gravity:
                    context.Radius = art.Radius * px;
                    context.Length = art.Time;
                    break;
                case WeaponArtKind.Barrage:
                    context.Length = art.Time;
                    break;
                case WeaponArtKind.Dash:
                    var from = _artFrom.TryGetValue(e.Source, out var start) ? start : at;
                    _artFrom.Remove(e.Source);
                    context.Length = Vector3.Distance(from, at) * px;
                    context.Radius = art.Radius * px;
                    origin = at - new Vector3(0, feet, 0);
                    break;
                case WeaponArtKind.Backstep:
                    context.Length = art.Reach * px;
                    origin = at - new Vector3(0, feet, 0);
                    break;
                case WeaponArtKind.Pierce:
                    context.Length = phase == ArtPhase.Start ? art.Reach * px : Vector3.Distance(body?.Position ?? at, at) * px;
                    break;
            }

            if (body != null && body.Kind == BodyKind.Player)
            {
                if (phase == ArtPhase.Start) Cast(body);
                else if (phase == ArtPhase.Strike)
                    Start(body, kind == WeaponArtKind.Parry || kind == WeaponArtKind.Charge || kind == WeaponArtKind.Slam ? Motion.Spin : Motion.Thrust);
            }

            if (Feel.SkillFx)
            {
                var fx = AddFx(FxProgram.Cast, SkillFx.ArtId(kind, phase), origin, context, mine, light: context.Radius / px + 2);
                if (fx != null && phase == ArtPhase.Start) _artFx[e.Source] = fx;
            }
            else if (phase == ArtPhase.Strike) AddFlash(at, Mathf.Max(0.6f, context.Radius / px), Palette.Weapons, FlashKind.Burst);

            if (mine)
            {
                float shake = phase != ArtPhase.Strike ? 0 : kind switch
                {
                    WeaponArtKind.Slam => 0.16f,
                    WeaponArtKind.Charge => 0.13f,
                    WeaponArtKind.Gravity => 0.1f,
                    WeaponArtKind.Pierce => 0.07f,
                    WeaponArtKind.Whirl or WeaponArtKind.Barrage => 0.02f,
                    _ => 0.05f,
                };
                if (phase == ArtPhase.Blocked) shake = 0.08f;
                _shake = Mathf.Max(_shake, shake);
            }
            var sound = phase == ArtPhase.Start ? Sound.Cast
                : phase == ArtPhase.Blocked ? Sound.Hammer
                : kind switch
                {
                    WeaponArtKind.Slam or WeaponArtKind.Gravity => Sound.Boom,
                    WeaponArtKind.Charge => Sound.Heavy,
                    WeaponArtKind.Dash or WeaponArtKind.Backstep => Sound.Dash,
                    WeaponArtKind.Pierce => Sound.Twang,
                    WeaponArtKind.Wave => Sound.Nova,
                    WeaponArtKind.Vital => Sound.Crit,
                    _ => Sound.Swing,
                };
            Hear(sound, at, kind == WeaponArtKind.Whirl && phase == ArtPhase.Strike ? 0.5f : 0.9f);
        }
    }
}
