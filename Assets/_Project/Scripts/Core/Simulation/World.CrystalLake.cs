using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // The rules the crystal cave and the underground lake bring (2026-11-25, Docs/BiomeRework.md B3b): shots glance off
    // the crystals that stand in a room (a player's and a monster's alike, as a monster's bouncing shot comes off rock),
    // and a prism wisp's shot breaks into three where it glances; a crystal tortoise turns a shot that strikes its shell
    // back at whoever fired it; a crystal mimic stands as a clump of crystals until someone comes within two tiles or
    // strikes it, and its waking rings the mimics near it awake; a tentacle never leaves the water's edge; one that goes
    // under in the lake goes under the water (the screen draws its ripples, World.UnderWater); and the shallows of a new
    // world's lake (TileType.ShallowWater, WorldGenerator.CrystalLakeDungeons.cs) are walked at ShallowSlow
    // (PlayerMotor.GroundSpeed). All of it is the host's and decided as the world runs, so every world of version 2 and
    // after has it but the shallows.
    public sealed partial class World
    {
        // ---- Crystal: shots glance off crystal ----

        // A shot comes off crystal this many times at most in its flight (then it breaks there, as on rock).
        public const int GlanceMost = 2;
        // MonsterFx: a shot glanced off crystal (Position: where; Source: whoever fired it, a player or a monster), a
        // tortoise's shell turned a shot back (Source: the tortoise), a prism's shot broke into three.
        public const int MonsterFxGlance = 18;
        public const int MonsterFxReflect = 19;
        public const int MonsterFxRefract = 20;
        // A shot turned back by a shell strikes for this share of the shell's own damage, flies at most this fast and
        // this far.
        public const float ReflectShare = 0.6f;
        public const float ReflectSpeed = 9;
        public const float ReflectReach = 8;
        // A prism's shot breaks into three this many degrees apart, each this share of its blow.
        public const float RefractDegrees = 28;
        public const float RefractShare = 0.7f;
        // No shot glances within this reach of a boss that is up: an arena's spires stay as they were for its fight.
        const float GlanceBossReach = 30;

        // The tiles that turn a shot (4-1 수정 동굴: "큰 결정 덩이는 탄을 튕긴다"): the crystal spires of the rooms and the
        // dungeons. The crystal props of the cave's looks join them here.
        // The crystal cave's own standing crystals too (B3a): the spire, the great mass and the leaning pillar.
        public static bool Glances(TileType type) => type == TileType.Spire
            || type == TileType.CrystalSpire || type == TileType.CrystalMass || type == TileType.LeaningCrystal;

        // Whether a circle there touches a tile that turns shots.
        public bool GlancingAt(Vec2 at, float radius)
        {
            int x0 = TileMap.Cell(at.X - radius), x1 = TileMap.Cell(at.X + radius), y0 = TileMap.Cell(at.Y - radius), y1 = TileMap.Cell(at.Y + radius);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (Map.InBounds(x, y) && Glances(Map[x, y]) && Touches(at, radius, x, y)) return true;
            return false;
        }

        // How many shots have glanced off crystal, and how many a shell turned back (the playtests and tests).
        public int ShotsGlanced { get; private set; }
        public int ShotsReflected { get; private set; }

        // A shot that has run into something solid: when that is crystal it comes off it as a bouncing shot comes off
        // rock (the side it struck turns it), whoever fired it, up to GlanceMost times. A boss's shots and every shot
        // near a boss that is up are left as they were. True when it glanced and flies on.
        bool Glance(Projectile shot)
        {
            if (shot.Glanced >= GlanceMost || !GlancingAt(shot.Position, shot.Radius * 0.5f)) return false;
            if (_bosses.Any(b => b.Id == shot.Source || !b.IsDead && Vec2.Distance(b.Position, shot.Position) < GlanceBossReach)) return false;
            var step = shot.Velocity * TickSeconds;
            var from = shot.Position - step;
            float radius = shot.Radius * 0.5f;
            bool acrossX = Collision.Overlaps(Map, from + new Vec2(step.X, 0), radius);
            bool acrossY = Collision.Overlaps(Map, from + new Vec2(0, step.Y), radius);
            if (!acrossX && !acrossY) acrossX = acrossY = true;
            shot.Velocity = new Vec2(acrossX ? -shot.Velocity.X : shot.Velocity.X, acrossY ? -shot.Velocity.Y : shot.Velocity.Y);
            shot.Position = from;
            shot.Glanced++;
            ShotsGlanced++;
            // A player's shot that comes back may strike what it went through once more.
            shot.Pierced.Clear();
            _events.Add(new SimEvent(SimEventType.MonsterFx, shot.Source, amount: MonsterFxGlance, position: from + step));
            Refract(shot);
            return true;
        }

        // A prism's shot (MonsterTraitKind.Prism) breaks into three the first time it comes off anything: itself on, and
        // one to either side.
        void Refract(Projectile shot)
        {
            if (shot.Friendly || shot.Refracted) return;
            var shooter = FindEnemy(shot.Source);
            if (shooter == null || !shooter.Definition.Has(MonsterTraitKind.Prism)) return;
            shot.Refracted = true;
            shot.Damage = Math.Max(1, shot.Damage * RefractShare);
            float speed = shot.Velocity.Length, angle = shot.Velocity.Angle, turn = RefractDegrees * MathF.PI / 180f;
            foreach (float side in new[] { -turn, turn })
            {
                _projectiles.Add(new Projectile(_nextId++, shot.Source, shot.Cause, shot.Position, Vec2.FromAngle(angle + side) * speed, shot.Radius, shot.Damage,
                    shot.RemainingTicks)
                {
                    Effect = shot.Effect,
                    EffectAmount = shot.EffectAmount,
                    Refracted = true,
                    Glanced = shot.Glanced,
                });
            }
            _events.Add(new SimEvent(SimEventType.MonsterFx, shot.Source, amount: MonsterFxRefract, position: shot.Position));
        }

        // ---- Crystal: a shell that turns shots back (MonsterTraitKind.Reflect) ----

        // A player's shot that has come to a monster whose shell turns shots: from in front of it (its guard's arc), while
        // its guard is up, it does not strike but goes back as the monster's own shot, mirrored off the shell. True when
        // it was turned (the player's shot is gone).
        bool ReflectOff(Projectile shot)
        {
            if (!shot.Friendly) return false;
            foreach (var enemy in _enemies)
            {
                if (enemy.IsDead || enemy.Away || enemy.Buried || shot.Pierced.Contains(enemy.Id) || !enemy.Definition.Has(MonsterTraitKind.Reflect)) continue;
                if (Vec2.Distance(enemy.Position, shot.Position) > shot.Radius + enemy.Definition.Radius) continue;
                if (enemy.State == EnemyState.Recover || enemy.State == EnemyState.Stagger) return false;
                var guard = enemy.Definition.Trait(MonsterTraitKind.Guard);
                float arc = guard?.Number(0, 120) ?? 120;
                var coming = -shot.Velocity;
                if (coming.LengthSquared < 1e-6f || Vec2.Dot(coming.Normalized, enemy.Facing.Normalized) < MathF.Cos(arc * 0.5f * MathF.PI / 180f)) return false;
                // Mirrored off the shell's face: back the way it came, turned by how far off square it struck.
                var normal = enemy.Facing.Normalized;
                var velocity = shot.Velocity - normal * (2 * Vec2.Dot(shot.Velocity, normal));
                float speed = Math.Min(ReflectSpeed, Math.Max(4, velocity.Length));
                var direction = velocity.LengthSquared > 1e-6f ? velocity.Normalized : normal;
                var at = enemy.Position + direction * (enemy.Definition.Radius + 0.1f);
                _projectiles.Add(new Projectile(_nextId++, enemy.Id, 0, at, direction * speed, MonsterShotRadius, Math.Max(1, enemy.Damage * ReflectShare), Ticks(ReflectReach / speed)));
                ShotsReflected++;
                _events.Add(new SimEvent(SimEventType.Guarded, enemy.Id));
                _events.Add(new SimEvent(SimEventType.MonsterFx, enemy.Id, amount: MonsterFxReflect, position: shot.Position));
                // Struck from afar before it noticed anyone, it has now.
                Provoked(enemy, FindPlayer(shot.Source));
                return true;
            }
            return false;
        }

        // ---- Crystal: mimics (MonsterTraitKind.Mimic) ----

        // A mimic's waking rings the others within this reach awake (4-1 수정 동굴: "공명 수정 ... 흉내쟁이를 깨운다").
        public const float MimicRing = 4;

        static float? MimicReach(MonsterDefinition kind) => kind.Trait(MonsterTraitKind.Mimic)?.Number(0, 2);

        // Whether a monster stands as crystals while idle: a mimic, but not one made an elite (a camp's leader, a dungeon's
        // champion: they lead the fight and are not hidden in it).
        public static bool Mimics(Enemy enemy) => enemy.Elite == EliteRank.None && enemy.Definition.Has(MonsterTraitKind.Mimic);

        // How near a player an idle monster notices: a dormant one's reach, a mimic's own, else null (its aggro).
        static float? NoticeReach(Enemy enemy) =>
            enemy.Definition.Trait(MonsterTraitKind.Dormant)?.Number(0, 3) ?? (Mimics(enemy) ? MimicReach(enemy.Definition) : null);

        // A mimic still standing as crystals when the rest of its pack has fallen in a fight comes out after the nearest
        // player within the reach a chase is given up at (a room is not left holding a pack no one can find).
        void WakeLastMimic(Enemy enemy)
        {
            if (enemy.State != EnemyState.Idle || enemy.Returning || !Mimics(enemy)) return;
            var pack = PackOf(enemy);
            if (pack == null || !pack.Alerted) return;
            if (_enemies.Any(e => e != enemy && !e.IsDead && e.PackId == enemy.PackId && !Mimics(e))) return;
            var target = _players.Where(p => !p.IsDead && !InSafePlace(p.Position) && Vec2.Distance(p.Position, enemy.Position) <= enemy.Definition.Aggro * AggroScale * LoseInterest)
                .OrderBy(p => Vec2.Distance(p.Position, enemy.Position)).FirstOrDefault();
            if (target == null) return;
            enemy.TargetId = target.Id;
            enemy.State = EnemyState.Chase;
        }

        // A mimic that has woken: the others still standing as crystals near it wake with it, after the same target.
        void RingMimics(Enemy enemy)
        {
            var target = FindPlayer(enemy.TargetId);
            if (target == null) return;
            foreach (var other in _enemies)
            {
                if (other == enemy || other.IsDead || other.State != EnemyState.Idle || other.Returning || !Mimics(other)) continue;
                if (Vec2.Distance(other.Position, enemy.Position) > MimicRing) continue;
                other.TargetId = target.Id;
                other.State = EnemyState.Chase;
            }
        }

        // ---- The lake: tentacles keep to the water's edge (MonsterTraitKind.Shore) ----

        // The tiles of water a shore keeper keeps by: the lake's deep water and its shallows.
        public static bool Wet(TileType type) => type == TileType.Water || type == TileType.ShallowWater;

        readonly Dictionary<int, bool> _shore = new Dictionary<int, bool>();
        int _shoreVersion = -1;
        float _shoreReach = -1;

        // Whether a spot is within reach of water (by its tile's middle), kept until the map changes.
        public bool ByWater(Vec2 at, float reach = 1)
        {
            int x = TileMap.Cell(at.X), y = TileMap.Cell(at.Y);
            if (!Map.InBounds(x, y)) return false;
            if (_shoreVersion != Map.Version || _shoreReach != reach)
            {
                _shore.Clear();
                _shoreVersion = Map.Version;
                _shoreReach = reach;
            }
            int key = y * Map.Width + x;
            if (_shore.TryGetValue(key, out bool known)) return known;
            bool wet = false;
            int span = (int)MathF.Ceiling(reach);
            var middle = TileMap.Center(x, y);
            for (int dy = -span; dy <= span && !wet; dy++)
                for (int dx = -span; dx <= span && !wet; dx++)
                    if (Map.InBounds(x + dx, y + dy) && Wet(Map[x + dx, y + dy]) && Vec2.Distance(TileMap.Center(x + dx, y + dy), middle) <= reach + 0.5f) wet = true;
            if (_shore.Count > 4096) _shore.Clear();
            _shore[key] = wet;
            return wet;
        }

        static float ShoreReachOf(MonsterDefinition kind) => kind.Trait(MonsterTraitKind.Shore)?.Number(0, 1) ?? 0;

        // Whether a monster that keeps to the shore would not go to a spot.
        bool OffShore(Enemy enemy, Vec2 at)
        {
            float reach = ShoreReachOf(enemy.Definition);
            return reach > 0 && !ByWater(at, reach);
        }

        // After a shore keeper's tick: a step away from the water is taken back (along the edge if part of it keeps by the
        // water), and one that stands away from any water (where its pack was set down) goes to the nearest edge within
        // ShoreSeek once; with none near, it stays where it is.
        public const int ShoreSeek = 10;

        void KeepToShore(Enemy enemy, Vec2 before)
        {
            float reach = ShoreReachOf(enemy.Definition);
            if (reach <= 0 || ByWater(enemy.Position, reach)) return;
            var radius = enemy.Definition.Radius;
            if (ByWater(before, reach))
            {
                var step = enemy.Position - before;
                var along = new[] { before + new Vec2(step.X, 0), before + new Vec2(0, step.Y) };
                enemy.Position = before;
                foreach (var spot in along)
                {
                    if ((spot - before).LengthSquared < 1e-8f || !ByWater(spot, reach) || Collision.Overlaps(Map, spot, radius, monster: true)) continue;
                    enemy.Position = spot;
                    break;
                }
                enemy.Knockback = Vec2.Zero;
                return;
            }
            if (enemy.ShoreSought) return;
            enemy.ShoreSought = true;
            int cx = TileMap.Cell(enemy.Position.X), cy = TileMap.Cell(enemy.Position.Y);
            Vec2? best = null;
            float bestDistance = float.MaxValue;
            for (int y = cy - ShoreSeek; y <= cy + ShoreSeek; y++)
            {
                for (int x = cx - ShoreSeek; x <= cx + ShoreSeek; x++)
                {
                    if (!Map.InBounds(x, y) || Map.IsSolid(x, y) || Map[x, y] == TileType.Lava) continue;
                    var middle = TileMap.Center(x, y);
                    float distance = Vec2.Distance(middle, enemy.Position);
                    if (distance >= bestDistance || !ByWater(middle, reach) || Collision.Overlaps(Map, middle, radius, monster: true)) continue;
                    bestDistance = distance;
                    best = middle;
                }
            }
            if (best == null) return;
            enemy.Position = best.Value;
            enemy.Home = best.Value;
        }

        // ---- The lake: under the water ----

        // Whether one under the ground there is under the water instead: in the lake, or where its tile is water. The
        // screen draws its ripples and bubbles in place of a mound of earth (a tentacle, a crab gone down to come up
        // elsewhere).
        public static bool UnderWater(TileMap map, BiomeMap biomes, Vec2 at, bool buried)
        {
            if (!buried || map == null) return false;
            int x = TileMap.Cell(at.X), y = TileMap.Cell(at.Y);
            if (!map.InBounds(x, y)) return false;
            return Wet(map[x, y]) || biomes != null && biomes[x, y] == Biome.Lake;
        }

        public bool UnderWater(Enemy enemy) => UnderWater(Map, Biomes, enemy.Position, enemy.Buried);
    }
}
