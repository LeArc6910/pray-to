using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // The enemies' shots (E2 of Docs/EffectsRework.md, ShotArt): each boss's in what it is made of, all with the red
    // rim that tells them from a player's; a tail drawn into the picture, the core beating; a ring and bits where one
    // ends; its light on the floor as it flies; and before an aimed volley, dotted lines the way each will go.
    sealed partial class WorldView
    {
        sealed class EnemyShot
        {
            public ShotElement Element;
            public int Reach;
            public int Step;
            // Drawn here; else it keeps its boss's own picture (rock, crescent, venom) and only breaks up as these do.
            public bool Own;
            public int Frame = -1;
        }

        sealed class ShotPop
        {
            public SpriteRenderer Renderer;
            public ShotElement Element;
            public float Age;
            public int Frame;
        }

        readonly Dictionary<int, EnemyShot> _enemyShots = new Dictionary<int, EnemyShot>();
        readonly List<ShotPop> _shotPops = new List<ShotPop>();
        readonly List<SpriteRenderer> _aimDots = new List<SpriteRenderer>();
        int _aimUsed;

        // A shot is one piece and its end one more, so a storm of thirty-two stays far inside the screen's 420; the
        // ends are held to this many at once, and the aim lines to this many dots.
        const int MostShotPops = 40;
        const int MostAimDots = 72;
        const float AimStep = 0.6f;
        const float AimReach = 7;

        // For the playtest and the tests: shots drawn here, ends playing, dots of the aim lines laid.
        public int EnemyShotCount { get; private set; }
        public int ShotPopCount => _shotPops.Count;
        public int AimDotCount { get; private set; }

        // Shots hung in the air for a picture (the playtest's 'shots'): an enemy's of a given element, or a player's.
        readonly List<ProjectileView> _stagedShots = new List<ProjectileView>();
        readonly Dictionary<int, ShotElement> _stagedElements = new Dictionary<int, ShotElement>();
        readonly List<(Vec2 At, Vec2 Facing, float Radius, float Degrees, int Count, float Progress)> _stagedAims = new List<(Vec2, Vec2, float, float, int, float)>();

        public void StageShot(Vec2 at, Vec2 velocity, float radius, int element)
        {
            int id = StagedIds + 0x100000 + _stagedSoFar++;
            if (element >= 0) _stagedElements[id] = (ShotElement)(element % ShotArt.Elements);
            _stagedShots.Add(new ProjectileView(id, at, velocity, radius, element < 0, element < 0 ? SkillTag.Fire : SkillTag.None));
        }

        public void StageAim(Vec2 at, Vec2 facing, float radius, float degrees, int count, float progress) =>
            _stagedAims.Add((at, facing, radius, degrees, count, progress));

        // The staged shots end where they hang (each with its ring), and the staged lines go.
        public void EndStagedShots()
        {
            _stagedShots.Clear();
            _stagedAims.Clear();
        }

        IEnumerable<ProjectileView> ProjectilesToDraw()
        {
            foreach (var projectile in _client.Projectiles()) yield return projectile;
            for (int i = 0; i < _stagedShots.Count; i++) yield return _stagedShots[i];
        }

        static Sprite EnemyShotSprite(EnemyShot shot, int frame) =>
            Sprites.Centered($"shot/enemy/{(int)shot.Element}/{shot.Reach}/{shot.Step}/{frame}", () => ShotArt.Shot(shot.Element, shot.Reach, shot.Step, frame));

        static Sprite ShotPopSprite(ShotElement element, int frame) =>
            Sprites.Centered($"shot/pop/{(int)element}/{frame}", () => ShotArt.Pop(element, frame));

        static Sprite AimDotSprite => Sprites.Centered("shot/aim", ShotArt.AimDot);

        // An enemy's shot as it is first seen: what it is made of by the boss in view, drawn as large as it hits and
        // at the angle it flies. One that hangs still has no tail.
        EnemyShot MakeEnemyShot(ProjectileView projectile, bool own)
        {
            if (!_stagedElements.TryGetValue(projectile.Id, out var element)) element = ShotArt.ElementOf(_bossInView?.Id);
            var shot = new EnemyShot
            {
                Element = element,
                Reach = ShotArt.Reach(projectile.Radius),
                Step = projectile.Velocity.LengthSquared < 0.25f ? -1 : ShotArt.Step(projectile.Velocity.Angle),
                Own = own,
            };
            _enemyShots[projectile.Id] = shot;
            return shot;
        }

        // The core beats, two frames (held on the first when motion is reduced), each shot a little out of step.
        void BeatEnemyShot(int id, SpriteRenderer renderer)
        {
            if (!_enemyShots.TryGetValue(id, out var shot) || !shot.Own) return;
            if (renderer.enabled) EnemyShotCount++;
            int frame = Settings.ReduceMotion ? 0 : (int)(Time.time * ShotArt.FramesPerSecond + (id & 1)) % ShotArt.Frames;
            if (frame == shot.Frame) return;
            shot.Frame = frame;
            renderer.sprite = EnemyShotSprite(shot, frame);
        }

        // The light a shot throws, in its element's colour (a player's keeps its own).
        Rgba ShotLightColor(int id, SpriteRenderer renderer) =>
            _enemyShots.TryGetValue(id, out var shot) && shot.Own ? ShotArt.Glow(shot.Element) : Sprites.ToRgba(renderer.color);

        // Where an enemy's shot ended (struck, stopped by a wall, or spent): a ring and bits of what it was made of.
        // The bosses' venom and crescents end their own way.
        void EnemyShotGone(int id, Vector3 at, bool plain)
        {
            if (!_enemyShots.TryGetValue(id, out var shot)) return;
            _enemyShots.Remove(id);
            _stagedElements.Remove(id);
            if (!plain || _shotPops.Count >= MostShotPops) return;
            var renderer = Renderer(_root.transform, "ShotEnd", ShotPopSprite(shot.Element, 0), Color.white);
            renderer.transform.localPosition = at;
            renderer.sortingOrder = 20000;
            _shotPops.Add(new ShotPop { Renderer = renderer, Element = shot.Element });
        }

        void UpdateShotPops(float deltaTime)
        {
            for (int i = _shotPops.Count - 1; i >= 0; i--)
            {
                var pop = _shotPops[i];
                pop.Age += deltaTime;
                // Held at an age for a picture, as a blow is (the playtest's 'fxhold').
                if (Feel.SkillFxHold >= 0) pop.Age = Mathf.Min(pop.Age, Feel.SkillFxHold);
                int frame = (int)(pop.Age / ShotArt.PopSeconds * ShotArt.PopFrames);
                if (frame >= ShotArt.PopFrames)
                {
                    Object.Destroy(pop.Renderer.gameObject);
                    _shotPops.RemoveAt(i);
                    continue;
                }
                if (frame == pop.Frame) continue;
                pop.Frame = frame;
                pop.Renderer.sprite = ShotPopSprite(pop.Element, frame);
            }
        }

        // Before a boss lets fly, where each shot will go. A fan (boss_attacks.csv angle) goes out round the way the
        // boss faces from the moment it winds up (World.Burst). A burst or a spiral all the way round starts at an
        // angle the host settles as the wind-up begins and sends with the boss (EntityView.AimAngle, 0.113.0): one
        // line a shot, evenly round from it. There can be thirty-two of those, so each line is as long as its share of
        // the round's own dots allows (a storm's are short ticks at the boss's edge, a ring of eight reach far).
        void AimOf(Body body, EntityView view)
        {
            var attack = view.BossAttack;
            if (view.BossState != BossState.Telegraph || attack == null) return;
            float progress = Mathf.Clamp01((Time.time - body.BossSince) / TelegraphOf(body, attack));
            if (view.AimsRound && attack.Count > 0 && (attack.Shape == AttackShape.Burst || attack.Shape == AttackShape.Spiral))
            {
                // LayAim spreads count lines over the degrees given, centred on the way given: all round but the last gap.
                float degrees = 360f * (attack.Count - 1) / attack.Count;
                int each = Mathf.Clamp(MostRoundDots / attack.Count, 2, 8);
                LayAim(view.Position, Vec2.FromAngle(view.AimAngle + degrees * Mathf.Deg2Rad / 2), view.Boss.Radius, degrees, attack.Count, progress,
                    Mathf.Min(AimReach, 0.3f + each * AimStep), MostAimDots + MostRoundDots);
                RoundAimDegrees = Mathf.Repeat(view.AimAngle * Mathf.Rad2Deg, 360);
                _roundAimAt = Time.time;
                return;
            }
            if (attack.Shape != AttackShape.Burst || attack.Angle <= 0 || attack.Angle >= 360) return;
            LayAim(view.Position, view.Facing, view.Boss.Radius, attack.Angle, attack.Count, progress);
        }

        // A burst all the way round has dots of its own past the others' (MostAimDots), so its lines never starve a fan's.
        const int MostRoundDots = 96;
        float _roundAimAt = -10;
        // For the playtest: the angle (degrees, 0 to 360) the last round's first line was drawn at, and whether one is
        // drawn now; and the ways (degrees) the enemy shots drawn now are flying.
        public float RoundAimDegrees { get; private set; } = -1;
        public bool RoundAimShown => Time.time - _roundAimAt < 0.1f;
        public List<float> EnemyShotWays()
        {
            var ways = new List<float>();
            foreach (var projectile in _client.Projectiles())
                if (!projectile.Friendly && projectile.Velocity.LengthSquared > 0.01f) ways.Add(Mathf.Repeat(projectile.Velocity.Angle * Mathf.Rad2Deg, 360));
            return ways;
        }

        // Dots out from the boss's edge, running outward and growing surer as the wind-up fills: unturned 3px dots, so
        // they stay crisp at any angle.
        void LayAim(Vec2 at, Vec2 facing, float radius, float degrees, int count, float progress, float farthest = AimReach, int most = MostAimDots)
        {
            if (count <= 0 || facing.LengthSquared < 1e-6f) return;
            float spread = degrees * Mathf.Deg2Rad, start = facing.Angle - spread / 2;
            int gaps = Mathf.Max(1, count - 1);
            float reach = farthest * Mathf.Clamp01(1 - Mathf.Pow(1 - Mathf.Min(1, progress * 1.4f), 3));
            float march = Settings.ReduceMotion ? 0 : Time.time * 2.4f % AimStep;
            var color = new Color(1, 1, 1, 0.45f + 0.55f * progress);
            for (int i = 0; i < count; i++)
            {
                var way = Vec2.FromAngle(start + (count == 1 ? spread / 2 : spread * i / gaps));
                for (float d = radius + 0.3f + march; d < radius + reach && _aimUsed < most; d += AimStep)
                {
                    if (_aimUsed == _aimDots.Count)
                    {
                        var made = Renderer(_root.transform, "Aim", AimDotSprite, Color.white);
                        // Over the warnings on the floor, under the bodies and the shots.
                        made.sortingOrder = DarkOrder + 12;
                        _aimDots.Add(made);
                    }
                    var dot = _aimDots[_aimUsed++];
                    dot.enabled = true;
                    dot.color = color;
                    var p = at + way * d;
                    dot.transform.localPosition = new Vector3(Mathf.Round(p.X * Sprites.PixelsPerUnit) / Sprites.PixelsPerUnit, Mathf.Round(p.Y * Sprites.PixelsPerUnit) / Sprites.PixelsPerUnit, 0);
                }
            }
        }

        // After the bodies are drawn: the staged lines, and the dots no line used this frame put away.
        void EndAim()
        {
            foreach (var aim in _stagedAims) LayAim(aim.At, aim.Facing, aim.Radius, aim.Degrees, aim.Count, aim.Progress);
            for (int i = _aimUsed; i < _aimDots.Count; i++) _aimDots[i].enabled = false;
            AimDotCount = _aimUsed;
            _aimUsed = 0;
            TargetAimCount = _targetAims;
            _targetAims = 0;
        }
    }
}
