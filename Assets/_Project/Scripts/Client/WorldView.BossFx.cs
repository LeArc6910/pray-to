using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The bosses' own effects on the screen (E6, Docs/EffectsRework.md 11): what each boss adds over the shared
    // landing (WorldView.Hazards.cs) when one of its blows falls, and what falls onto its warnings. The pieces are
    // BossEffects' (Client/Art/BossFx.cs), laid on the pool the skills' and the monsters' effects use, so all three
    // share the cap on pieces and on lights.
    sealed partial class WorldView
    {
        sealed class BossEffect
        {
            public BossFx Fx;
            public BossContext Context;
            public Vector3 At;
            public float Age;
        }

        readonly List<BossEffect> _bossFx = new List<BossEffect>();
        // Drawn for this frame only, gathered while the warnings are drawn (what falls onto one).
        readonly List<(BossFx Fx, BossContext Context, Vector3 At)> _bossDraws = new List<(BossFx, BossContext, Vector3)>();
        // What a staged warning was laid as (the playtest's 'warn ... <boss> <attack>').
        readonly Dictionary<int, (string Boss, string Attack)> _stagedAs = new Dictionary<int, (string, string)>();

        // The most pieces any one of the bosses' effects laid in the last frame (the playtest checks it against
        // BossEffects.Most), and how many are running.
        public int BossFxPieces { get; private set; }
        public int BossFxCount => _bossFx.Count;

        public void StageWarningAs(string boss, string attack) => _stagedAs[StagedIds + _stagedSoFar] = (boss, attack);

        // Whose blow a hazard is: the boss in view and its attack's id, or what a staged warning was laid as.
        (string Boss, string Attack) BlowOf(HazardState hazard)
        {
            if (hazard.Id >= StagedIds) return _stagedAs.TryGetValue(hazard.Id, out var staged) ? staged : (null, null);
            var attack = AttackOf(hazard);
            return attack == null || _bossInView == null ? (null, null) : (_bossInView.Id, attack.Id);
        }

        void AddBossFx(BossFx fx, Vector3 at, BossContext context)
        {
            if (_bossFx.Count >= FxMostAlive) return;
            context.Seed = ++_fxSeed & 0xFFFF;
            _bossFx.Add(new BossEffect { Fx = fx, Context = context, At = at });
        }

        // What comes down onto a boss's warning while it fills, gathered here and drawn with the rest.
        void BossWarnExtras(HazardState hazard, float progress)
        {
            var (boss, attack) = BlowOf(hazard);
            if (boss == null) return;
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            // The crystal queen's rain and hail: each crystal's shadow, then the crystal itself.
            if (boss == "crystal_queen" && (attack == "crystal_rain" || attack == "shard_hail"))
                _bossDraws.Add((BossFx.ShardDrop, new BossContext { Size = hazard.Size * Ppu, Progress = progress, Big = attack == "shard_hail", Ramp = FxRamp.Crystal }, at));
            // The general's fire called down where he pointed, and the volley's burning arrows.
            if (boss == "mad_general" && (attack == "hellfire" || attack == "meteors"))
                _bossDraws.Add((BossFx.ShardDrop, new BossContext { Size = hazard.Size * Ppu, Progress = progress, Count = attack == "meteors" ? 2 : 1, Ramp = FxRamp.Fire }, at));
        }

        // A few bits thrown one way in what the ramp is made of (BossFx.Cinders): what Kick's square dots were, for the
        // bosses whose effects have been reworked.
        void Chips(Vector3 at, Vector3 toward, FxRamp ramp, int count, float reach = 14, float spread = 70)
        {
            if (!Feel.Bits) return;
            AddBossFx(BossFx.Cinders, at, new BossContext { Ramp = ramp, Count = count, Size = reach, Width = spread, Dir = toward.sqrMagnitude < 1e-6f ? 90 : Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg });
        }

        // Every piece of the bosses' effects for this frame.
        void DrawBossFx(float deltaTime)
        {
            int most = 0;
            foreach (var draw in _bossDraws)
            {
                _fxStamps.Clear();
                BossEffects.Stamps(draw.Fx, draw.Context, Time.time, _fxStamps);
                most = Mathf.Max(most, Pieces(_fxStamps));
                EmitFx(draw.At, 1, null);
            }
            _bossDraws.Clear();
            for (int i = _bossFx.Count - 1; i >= 0; i--)
            {
                var fx = _bossFx[i];
                fx.Age += deltaTime;
                if (Feel.SkillFxHold >= 0) fx.Age = Mathf.Min(fx.Age, Feel.SkillFxHold);
                _fxStamps.Clear();
                if (!BossEffects.Stamps(fx.Fx, fx.Context, fx.Age, _fxStamps))
                {
                    _bossFx.RemoveAt(i);
                    continue;
                }
                most = Mathf.Max(most, Pieces(_fxStamps));
                EmitFx(fx.At, 1, null);
            }
            BossFxPieces = most;
        }

        static int Pieces(List<FxStamp> stamps)
        {
            int count = 0;
            foreach (var stamp in stamps) if (stamp.Shape != FxShape.Light) count++;
            return count;
        }
    }
}
