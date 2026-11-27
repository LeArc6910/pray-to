using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // Each skill's own effect (2026-11-21, Docs/SkillEffects.md): SkillFx says which pieces are where at each moment
    // of an effect, and this draws them with sprites kept in a pool. Nothing here decides anything about the game: the
    // effects start from the events and the projectiles the host sends. Feel.SkillFx off brings the old look back.
    sealed partial class WorldView
    {
        sealed class Fx
        {
            public FxProgram Program;
            public string Skill;
            public FxContext Context;
            public Vector3 At;
            public float Age;
            // The player's own; others' are drawn a little dimmer so one's own read first.
            public bool Mine;
            // Whoever the effect stays on (a buff, a summoning), or null for one that stays where it happened.
            public Body Follow;
            // The caster's picture when the effect began, for afterimages.
            public Sprite Ghost;
            // How far its flash lights the dark, in tiles, and in what colour.
            public float Light;
        }

        // A skill just used whose projectiles have yet to show up in a snapshot: they are known for the skill's by
        // appearing beside its caster a moment later (the snapshot does not name a projectile's skill).
        sealed class Launch
        {
            public SkillDefinition Skill;
            public Vector3 From;
            public float Start;
            public float Until;
            public int Left;
            public bool Mine;
            public FxRamp Main;
        }

        sealed class SkillShot
        {
            public SkillDefinition Skill;
            public bool Mine;
            public float Age;
            public Vector3 Last;
            public float Dir;
            public FxRamp Main;
            public int Seed;
        }

        readonly List<Fx> _fx = new List<Fx>();
        readonly List<Launch> _launches = new List<Launch>();
        readonly Dictionary<int, SkillShot> _skillShots = new Dictionary<int, SkillShot>();
        // Friendly projectiles seen lately that no skill is known for yet (when first seen, where and which way they
        // were last), and ones of those already gone.
        readonly Dictionary<int, (float Seen, Vector3 Last, float Dir)> _youngShots = new Dictionary<int, (float, Vector3, float)>();
        readonly List<(Vector3 Last, float Dir, float Time)> _pendingGone = new List<(Vector3, float, float)>();
        const float YoungShotSeconds = 1f;
        int _skillShotsDrawn;
        int _fxShown;
        int _skillShotsShown;
        readonly List<FxStamp> _fxStamps = new List<FxStamp>();
        readonly List<SpriteRenderer> _fxPool = new List<SpriteRenderer>();
        readonly float[][] _fxPoints = { null, new float[2], new float[4], new float[6], new float[8] };
        static readonly Dictionary<long, Sprite> FxSprites = new Dictionary<long, Sprite>();
        int _fxUsed;
        int _fxSeed;
        // The lights the effects' pieces ask for (FxShape.Light): gathered while a frame's pieces are laid out, and
        // put on the light map the frame after. Three an effect, twelve in all (Docs/EffectsRework.md 8).
        List<LightSource> _fxLights = new List<LightSource>(), _fxLightsShown = new List<LightSource>();
        const int FxMostLights = 12;
        // Lights the effects' pieces put on the light map last frame, for the playtests.
        public int SkillFxLights => _fxLightsShown.Count;

        // The most pieces on screen at once, and the most effects alive: past these the newest lesser ones are dropped
        // (a web build draws each piece as a sprite).
        const int FxMostStamps = 420;
        const int FxMostAlive = 48;
        const float OthersFxAlpha = 0.6f;
        const int FxGroundOrder = 20;
        const int FxOverOrder = 19010;

        // Effects running and pieces drawn this frame, for the playtests.
        public int SkillFxAlive => _fx.Count;
        // (As of the last frame drawn through: the counts themselves start over in the middle of a frame.)
        public int SkillFxPieces => _fxShown;
        // Projectiles drawn in their skill's own look that frame.
        public int SkillShotsDrawn => _skillShotsShown;

        static bool HasSkillFx(SkillDefinition skill) => Feel.SkillFx && skill != null && SkillFx.Knows(skill.Id);

        // The colours of a skill with no element of its own: steel or wind, or the element a running buff lends.
        static FxRamp MainRamp(SkillDefinition skill, SkillTag granted)
        {
            var tags = skill.Tags | skill.Grants | granted;
            return SkillFx.MainOf(skill.Id, (tags & SkillTag.Fire) != 0, (tags & SkillTag.Cold) != 0, (tags & SkillTag.Lightning) != 0);
        }

        Fx AddFx(FxProgram program, string skill, Vector3 at, FxContext context, bool mine, Body follow = null, float light = 0)
        {
            if (_fx.Count >= FxMostAlive) return null;
            context.Seed = ++_fxSeed & 0xFFFF;
            var fx = new Fx { Program = program, Skill = skill, Context = context, At = at, Mine = mine, Follow = follow, Light = light };
            _fx.Add(fx);
            return fx;
        }

        // What a skill looks like where it is used: the caster's motion as before, then the skill's own effect.
        void ShowSkillFx(SimEvent e, SkillDefinition skill, Body caster)
        {
            bool mine = e.Source == _client.PlayerId;
            var context = new FxContext
            {
                Radius = skill.Radius > 0 ? (skill.Radius + World.PlayerRadius) * Sprites.PixelsPerUnit : 0,
                Dir = e.Direction.Angle,
                Main = MainRamp(skill, GrantedTags(e.Source)),
            };
            // Effects are laid out from the feet; a nova's is lifted so its edge is centred where the host measures from.
            var feet = caster.Position - new Vector3(0, caster.Feet, 0);
            Body follow = null;
            Sprite ghost = null;
            float light = 0;
            switch (skill.Kind)
            {
                case SkillKind.Nova:
                    Start(caster, Motion.Spin);
                    context.Lift = caster.Feet * Sprites.PixelsPerUnit;
                    if (mine) _shake = Mathf.Max(_shake, skill.Radius >= 3 ? 0.08f : 0.06f);
                    light = skill.Radius + 2;
                    break;
                case SkillKind.Buff:
                case SkillKind.Summon:
                case SkillKind.Heal:
                    Cast(caster);
                    follow = caster;
                    if (skill.Id == "spirit_pact") context.Points = (float[])SpiritsNear(caster, feet)?.Clone();
                    break;
                case SkillKind.Dash:
                    Start(caster, Motion.Thrust);
                    // How far the host really took them (a wall cuts a dash short), when the view already shows it.
                    float moved = Vector3.Distance(caster.Position, caster.LastPosition);
                    context.Length = (moved > 0.5f ? Mathf.Min(moved, skill.Range) : skill.Range) * Sprites.PixelsPerUnit;
                    ghost = caster.Shape.sprite;
                    if (mine) _shake = Mathf.Max(_shake, 0.05f);
                    break;
                case SkillKind.Projectile:
                    Start(caster, Motion.Thrust);
                    if (caster.Gear.Kind != HeroWeapon.Bow && caster.Gear.Kind != HeroWeapon.Staff)
                    {
                        caster.Combo = 0;
                        caster.SwingTime = SwingSeconds;
                        caster.Swing.transform.localRotation = Quaternion.Euler(0, 0, e.Direction.Angle * Mathf.Rad2Deg);
                        caster.Swing.transform.localScale = Vector3.one;
                    }
                    // A few more than the sheet says: a support gem adds projectiles.
                    _launches.Add(new Launch { Skill = skill, From = caster.Position, Start = Time.time, Until = Time.time + 1f, Left = Mathf.Max(1, skill.Count) + 2, Mine = mine, Main = context.Main });
                    follow = caster;
                    break;
            }
            var fx = AddFx(FxProgram.Cast, skill.Id, feet, context, mine, follow, light);
            if (fx != null) fx.Ghost = ghost;
        }

        // Where the caster's spirits are, from the caster's feet in pixels (the nearest four; the snapshot does not say
        // whose a spirit is, so with two summoners side by side a line may go to the other's).
        float[] SpiritsNear(Body caster, Vector3 feet)
        {
            int count = 0;
            var points = _fxPoints[4];
            foreach (var body in _others.Values)
            {
                if (body.Kind != BodyKind.Minion || count >= 4 || Vector3.Distance(body.Position, caster.Position) > 7.5f) continue;
                points[count * 2] = (body.Position.x - feet.x) * Sprites.PixelsPerUnit;
                points[count * 2 + 1] = (body.Position.y - feet.y) * Sprites.PixelsPerUnit - 8;
                count++;
            }
            if (count == 0) return null;
            var exact = _fxPoints[count];
            System.Array.Copy(points, exact, count * 2);
            return exact;
        }

        // A burst where a skill's projectile lands (the host says where).
        void ShowBurstFx(SkillDefinition skill, Vector3 at, bool mine)
        {
            AddFx(FxProgram.Hit, skill.Id, at, new FxContext { Radius = skill.Radius * Sprites.PixelsPerUnit, Main = MainRamp(skill, SkillTag.None) }, mine, light: skill.Radius + 2.5f);
        }

        // A skill's blast put on the floor by hand, for the playtests' pictures (the command blast, E3 of
        // Docs/EffectsRework.md): the same effect a landed projectile of that skill gets, at this radius in tiles.
        public void StageBlast(string skill, Vec2 at, float radius) =>
            AddFx(FxProgram.Hit, skill, new Vector3(at.X, at.Y, 0), new FxContext { Radius = radius * Sprites.PixelsPerUnit, Main = FxRamp.Fire }, mine: true, light: radius + 2.5f);

        // A skill's own effect put on the floor by hand as if cast there, facing dir (radians), for the playtests'
        // pictures (the command castfx, E4 of Docs/EffectsRework.md): several can be set side by side and held at an age.
        public void StageCast(string skill, Vec2 at, float radius, float dir) =>
            AddFx(FxProgram.Cast, skill, new Vector3(at.X, at.Y, 0), new FxContext
            {
                Radius = radius > 0 ? (radius + World.PlayerRadius) * Sprites.PixelsPerUnit : 0, Dir = dir, Length = 4.5f * Sprites.PixelsPerUnit,
                Main = SkillFx.MainOf(skill, false, false, false),
            }, mine: true);

        // Lightning's jump between two bodies.
        void ShowChainFx(Vector3 from, Vector3 to)
        {
            var along = to - from;
            AddFx(FxProgram.Chain, "chain_lightning", (from + to) / 2,
                new FxContext { Length = along.magnitude * Sprites.PixelsPerUnit, Dir = Mathf.Atan2(along.y, along.x) }, mine: true, light: 2.5f);
            Hear(Sound.Zap, to, 0.5f);
        }

        // A player's projectile as its skill draws it. False for one that is no skill's (a bow's arrow, a staff's bolt),
        // which keeps the plain look. A projectile is usually seen a moment before the event of the skill that fired it
        // is shown (projectiles are drawn before the frame's events), so one not yet known for a skill's is tried again
        // while it is young.
        // For the playtest: where the last skill's projectile was said to end, whether that is being said now, and where
        // the last skill's burst really was (the host's Burst event).
        public Vec2 LandingMark { get; private set; }
        public bool LandingMarkShown => Time.time - _landingMarkAt < 0.1f;
        public Vec2 LastSkillBurst { get; private set; }
        public int SkillBursts { get; private set; }
        float _landingMarkAt = -10;

        bool DrawSkillShot(ProjectileView projectile, bool isNew, float deltaTime)
        {
            if (!projectile.Friendly) return false;
            var at = new Vector3(projectile.Position.X, projectile.Position.Y, 0);
            float dir = projectile.Velocity.Angle;
            if (!_skillShots.TryGetValue(projectile.Id, out var shot))
            {
                if (isNew) _youngShots[projectile.Id] = (Time.time, at, dir);
                if (!_youngShots.TryGetValue(projectile.Id, out var young)) return false;
                if (Time.time - young.Seen > YoungShotSeconds)
                {
                    _youngShots.Remove(projectile.Id);
                    return false;
                }
                _youngShots[projectile.Id] = (young.Seen, at, dir);
                shot = MatchShot(projectile.Id, at);
                if (shot == null) return false;
            }
            shot.Age += deltaTime;
            shot.Last = at;
            shot.Dir = dir;
            if (!Feel.SkillFx) return false;
            // Where it will end as things stand (the first wall, the first body where it is now, or its range's end):
            // the client runs the host's own steps from the snapshot's numbers (WorldClient.LandingOf).
            float markX = 0, markY = 0, mark = 0;
            if (shot.Skill.Radius > 0 && projectile.TicksLeft > 0)
            {
                var landing = _client.LandingOf(projectile);
                markX = (landing.X - projectile.Position.X) * Sprites.PixelsPerUnit;
                markY = (landing.Y - projectile.Position.Y) * Sprites.PixelsPerUnit;
                mark = shot.Skill.Radius * Sprites.PixelsPerUnit;
                LandingMark = landing;
                _landingMarkAt = Time.time;
            }
            _fxStamps.Clear();
            SkillFx.Stamps(FxProgram.Shot, shot.Skill.Id, new FxContext
            {
                Dir = shot.Dir, Seed = shot.Seed, Main = shot.Main,
                Length = shot.Skill.Range * Sprites.PixelsPerUnit, Span = shot.Skill.Speed > 0 ? shot.Skill.Range / shot.Skill.Speed : 0,
                MarkX = markX, MarkY = markY, Mark = mark,
            }, shot.Age, _fxStamps);
            EmitFx(at, shot.Mine ? 1 : OthersFxAlpha, null);
            _skillShotsDrawn++;
            return true;
        }

        // The skill just used that this projectile is of: the nearest caster it can have come from since. Null for none.
        SkillShot MatchShot(int id, Vector3 at)
        {
            Launch best = null;
            float nearest = float.MaxValue;
            foreach (var launch in _launches)
            {
                // As far as it can have flown since the skill was used, and a little.
                float distance = Vector3.Distance(launch.From, at);
                if (launch.Left <= 0 || distance > nearest || distance > 1 + launch.Skill.Speed * (Time.time - launch.Start + 0.2f)) continue;
                nearest = distance;
                best = launch;
            }
            if (best == null || !HasSkillFx(best.Skill)) return null;
            best.Left--;
            _youngShots.Remove(id);
            var shot = new SkillShot { Skill = best.Skill, Mine = best.Mine, Main = best.Main, Seed = ++_fxSeed & 0xFFFF };
            _skillShots[id] = shot;
            return shot;
        }

        // A skill's projectile is gone: one that bursts is shown by the host's Burst; the others end here. One that
        // was gone before its skill's event was shown (it struck something right beside the caster) is matched now.
        void SkillShotGone(int id)
        {
            if (!_skillShots.TryGetValue(id, out var shot) && _youngShots.TryGetValue(id, out var young))
            {
                _pendingGone.Add((young.Last, young.Dir, Time.time));
            }
            _youngShots.Remove(id);
            if (shot == null) return;
            _skillShots.Remove(id);
            if (!Feel.SkillFx || shot.Skill.Radius > 0) return;
            AddFx(FxProgram.Hit, shot.Skill.Id, shot.Last, new FxContext { Dir = shot.Dir, Main = shot.Main }, shot.Mine);
        }

        // Projectiles that went before any skill was known for them: once this frame's events are shown, one whose
        // skill has turned up gets its ending after all.
        void EndGoneShots()
        {
            for (int i = _pendingGone.Count - 1; i >= 0; i--)
            {
                var (last, dir, time) = _pendingGone[i];
                var shot = MatchShot(-1 - i, last);
                if (shot != null)
                {
                    _skillShots.Remove(-1 - i);
                    if (Feel.SkillFx && shot.Skill.Radius <= 0) AddFx(FxProgram.Hit, shot.Skill.Id, last, new FxContext { Dir = dir, Main = shot.Main }, shot.Mine);
                }
                if (shot != null || Time.time - time > YoungShotSeconds) _pendingGone.RemoveAt(i);
            }
        }

        void BeginSkillFx()
        {
            _fxUsed = 0;
            (_fxLights, _fxLightsShown) = (_fxLightsShown, _fxLights);
            _fxLights.Clear();
            _skillShotsDrawn = 0;
            for (int i = _launches.Count - 1; i >= 0; i--)
            {
                if (Time.time > _launches[i].Until) _launches.RemoveAt(i);
            }
        }

        // Every running effect's pieces for this frame, then the quiet cues of the buffs running, and the rest of the
        // pool put away.
        void UpdateSkillFx(float deltaTime)
        {
            EndGoneShots();
            for (int i = _fx.Count - 1; i >= 0; i--)
            {
                var fx = _fx[i];
                fx.Age += deltaTime;
                if (Feel.SkillFxHold >= 0) fx.Age = Mathf.Min(fx.Age, Feel.SkillFxHold);
                if (fx.Follow != null && fx.Follow.Root != null) fx.At = fx.Follow.Position - new Vector3(0, fx.Follow.Feet, 0);
                _fxStamps.Clear();
                bool alive = Feel.SkillFx && SkillFx.Stamps(fx.Program, fx.Skill, fx.Context, fx.Age, _fxStamps);
                if (!alive)
                {
                    _fx.RemoveAt(i);
                    continue;
                }
                // An effect that lights the floor with its own pieces needs no flash of the old kind.
                if (EmitFx(fx.At, fx.Mine ? 1 : OthersFxAlpha, fx.Ghost)) fx.Light = 0;
            }
            if (Feel.SkillFx)
            {
                int selfBuffs = 0;
                for (int slot = 0; slot < _client.Skills.Count; slot++)
                {
                    if (_client.BuffLeft(slot) > 0) selfBuffs |= 1 << slot;
                }
                BuffCues(_self, _client.PlayerId, selfBuffs, true);
                foreach (var pair in _buffBits)
                {
                    if (pair.Value != 0 && _others.TryGetValue(pair.Key, out var body)) BuffCues(body, pair.Key, pair.Value, false);
                }
            }
            UpdateHitFx(deltaTime);
            for (int i = _fxUsed; i < _fxPool.Count; i++)
            {
                if (_fxPool[i].enabled) _fxPool[i].enabled = false;
            }
            _fxShown = _fxUsed;
            _skillShotsShown = _skillShotsDrawn;
        }

        // The cue of each buff running on a player, for as long as it runs (Docs/SkillEffects.md: small, near the body,
        // a slow pulse).
        void BuffCues(Body body, int playerId, int bits, bool mine)
        {
            var feet = body.Position - new Vector3(0, body.Feet, 0);
            for (int slot = 0; slot < 8; slot++)
            {
                if ((bits & (1 << slot)) == 0) continue;
                var skill = _client.SkillOf(playerId, slot);
                if (skill == null || !SkillFx.HasLoop(skill.Id)) continue;
                var context = new FxContext { Dir = Mathf.Atan2(body.Faced.Y, body.Faced.X), Seed = playerId & 0xFF };
                if (skill.Id == "spirit_pact") context.Points = SpiritsNear(body, feet);
                _fxStamps.Clear();
                SkillFx.Stamps(FxProgram.Loop, skill.Id, context, Time.time + playerId * 0.37f, _fxStamps);
                EmitFx(feet, mine ? 1 : OthersFxAlpha, null);
            }
        }

        // Lays this frame's pieces of one effect on pooled sprites, each on the world's pixel grid. True if the
        // effect asked for a light of its own.
        bool EmitFx(Vector3 origin, float alpha, Sprite ghost)
        {
            const float pixel = 1f / Sprites.PixelsPerUnit;
            int lights = 0;
            foreach (var stamp in _fxStamps)
            {
                if (stamp.Shape == FxShape.Light)
                {
                    if (lights++ < FxLayers.MostLights && _fxLights.Count < FxMostLights)
                    {
                        _fxLights.Add(new LightSource(new Vec2(origin.x + stamp.X * pixel, origin.y + stamp.Y * pixel), stamp.Size * pixel, stamp.Alpha * alpha,
                            SkillFxArt.Ramp(stamp.Ramp)[stamp.Frame & 3], tints: true));
                    }
                    continue;
                }
                if (_fxUsed >= FxMostStamps) return lights > 0;
                Sprite sprite;
                var color = Color.white;
                if (stamp.Shape == FxShape.Ghost)
                {
                    if (ghost == null) continue;
                    sprite = ghost;
                    var tint = SkillFxArt.Ramp(stamp.Ramp)[2];
                    color = new Color(tint.R / 255f, tint.G / 255f, tint.B / 255f);
                }
                else if (!FxSprites.TryGetValue(stamp.Key, out sprite) || sprite == null)
                {
                    var copy = stamp;
                    sprite = Sprites.Centered($"sfx/{(int)copy.Shape}/{(int)copy.Ramp}/{copy.Size}/{copy.Frame}", () => SkillFxArt.Draw(copy.Shape, copy.Ramp, copy.Size, copy.Frame));
                    FxSprites[stamp.Key] = sprite;
                }
                if (_fxUsed == _fxPool.Count)
                {
                    var made = Renderer(_root.transform, "SkillFx", null, Color.white);
                    _fxPool.Add(made);
                }
                var renderer = _fxPool[_fxUsed];
                renderer.enabled = true;
                renderer.sprite = sprite;
                if (stamp.Tint != 0) color = new Color((stamp.Tint >> 16 & 255) / 255f, (stamp.Tint >> 8 & 255) / 255f, (stamp.Tint & 255) / 255f);
                color.a = stamp.Alpha * alpha;
                renderer.color = color;
                var place = renderer.transform;
                // A sprite of odd width sits half a pixel off its centre, so its pixels still land on the grid.
                float x = origin.x + stamp.X * pixel, y = origin.y + stamp.Y * pixel;
                if (stamp.Angle == 0 && stamp.Shape != FxShape.Ghost)
                {
                    float halfWide = sprite.rect.width * 0.5f * pixel, halfTall = sprite.rect.height * 0.5f * pixel;
                    x = Mathf.Round((x - halfWide) * Sprites.PixelsPerUnit) * pixel + halfWide;
                    y = Mathf.Round((y - halfTall) * Sprites.PixelsPerUnit) * pixel + halfTall;
                }
                place.localPosition = new Vector3(x, y, 0);
                place.localRotation = stamp.Angle == 0 ? Quaternion.identity : Quaternion.Euler(0, 0, stamp.Angle);
                renderer.sortingOrder = stamp.Ground ? FxGroundOrder : stamp.Low ? BlastDustOrder : FxOverOrder + (_fxUsed & 511);
                _fxUsed++;
            }
            return lights > 0;
        }

        // A blast lights the dark around it for a moment, in its element's colour.
        void AddFxLights()
        {
            _lights.AddRange(_fxLightsShown);
            foreach (var fx in _fx)
            {
                if (fx.Light <= 0 || fx.Age > 0.45f || fx.Age < (fx.Program == FxProgram.Cast ? SkillFx.Lead : 0)) continue;
                var ramp = fx.Skill == "chain_lightning" || fx.Skill == "thunderclap" ? FxRamp.Bolt
                    : fx.Program == FxProgram.Hit ? FxRamp.Fire
                    : fx.Skill == "fire_ring" || fx.Skill == "inferno" ? FxRamp.Fire
                    : fx.Skill == "frost_ring" || fx.Skill == "blizzard" ? FxRamp.Cold
                    : fx.Skill == "spirit_blast" ? FxRamp.Spirit : fx.Context.Main;
                _lights.Add(new LightSource(new Vec2(fx.At.x, fx.At.y), fx.Light, 0.9f * (1 - fx.Age / 0.45f), SkillFxArt.Ramp(ramp)[1], tints: true));
            }
        }
    }
}
