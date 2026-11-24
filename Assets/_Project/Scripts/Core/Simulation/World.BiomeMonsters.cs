using System;
using System.Collections.Generic;

namespace Game.Core
{
    // The rules the first two biomes' monsters bring (2026-11-24, Docs/BiomeRework.md B1b): the dirt cave's rats keep out
    // of lamplight (fearlight), the mushroom forest's spore clouds fill the monsters standing in them and say so, and a
    // slime mother sheds slimes as she is struck. The timber ambush of the dirt cave is the director's (World.Director.cs).
    public sealed partial class World
    {
        // A cloud fills a big body by this share of what it fills a small one (its health is four or twelve times as
        // much): an L body half, an XL one a quarter.
        public const float CloudBigShare = 0.5f;
        public const float CloudXlShare = 0.1f;
        // What a cloud gave a monster is told this often at most (SimEventType.Healed, Target CloudHealed), so the screen
        // shows it as green numbers without an event every tick.
        public const float CloudHealSeconds = 0.5f;
        public const int CloudHealed = 1;
        // A slime mother sheds no more than this many in her life.
        public const int MostShed = 10;

        // ---- Light (fearlight) ----

        // Tiles already looked at: whether a fearlight monster would shun their middle, for the map as it was then.
        readonly Dictionary<int, bool> _lit = new Dictionary<int, bool>();
        int _litVersion = -1;
        float _litReach = -1;

        // Whether a spot is within reach (Lights.FearReach) of a tile that gives light (Lights.Gives): where a monster
        // that fears the light does not go. Worked out a tile at a time for the middle of the spot's tile and kept until
        // the map changes.
        public bool FearsLight(Vec2 at, float reach = Lights.FearReach)
        {
            int x = TileMap.Cell(at.X), y = TileMap.Cell(at.Y);
            if (!Map.InBounds(x, y)) return false;
            if (_litVersion != Map.Version || _litReach != reach)
            {
                _lit.Clear();
                _litVersion = Map.Version;
                _litReach = reach;
            }
            int key = y * Map.Width + x;
            if (_lit.TryGetValue(key, out bool known)) return known;
            bool lit = NearestLight(TileMap.Center(x, y), reach) != null;
            if (_lit.Count > 4096) _lit.Clear();
            _lit[key] = lit;
            return lit;
        }

        // The middle of the nearest tile that gives light within reach of a spot, or null.
        public Vec2? NearestLight(Vec2 at, float reach = Lights.FearReach)
        {
            int cx = TileMap.Cell(at.X), cy = TileMap.Cell(at.Y), span = (int)MathF.Ceiling(reach);
            Vec2? best = null;
            float bestDistance = reach;
            for (int y = cy - span; y <= cy + span; y++)
            {
                for (int x = cx - span; x <= cx + span; x++)
                {
                    if (!Map.InBounds(x, y) || !Lights.Gives(Map[x, y])) continue;
                    var middle = TileMap.Center(x, y);
                    float distance = Vec2.Distance(middle, at);
                    if (distance > bestDistance) continue;
                    bestDistance = distance;
                    best = middle;
                }
            }
            return best;
        }

        static float FearReachOf(MonsterDefinition kind) => kind.Trait(MonsterTraitKind.Fearlight)?.Number(0, Lights.FearReach) ?? 0;

        // Whether a monster would not go to a spot for the light there.
        bool Shuns(Enemy enemy, Vec2 at)
        {
            float reach = FearReachOf(enemy.Definition);
            return reach > 0 && FearsLight(at, reach);
        }

        // After a fearlight monster's tick: a step into the light is taken back (along the edge if the step had a part
        // that stays out of it), and one the light found where it stands (a torch set down by it, a blow that threw it
        // in) goes out of it, away from the lamp.
        void ShunLight(Enemy enemy, Vec2 before)
        {
            float reach = FearReachOf(enemy.Definition);
            if (reach <= 0 || enemy.Away || !FearsLight(enemy.Position, reach)) return;
            var radius = enemy.Definition.Radius;
            if (!FearsLight(before, reach))
            {
                var step = enemy.Position - before;
                var along = new[] { before + new Vec2(step.X, 0), before + new Vec2(0, step.Y) };
                enemy.Position = before;
                foreach (var spot in along)
                {
                    if ((spot - before).LengthSquared < 1e-8f || FearsLight(spot, reach) || Collision.Overlaps(Map, spot, radius, monster: true)) continue;
                    enemy.Position = spot;
                    break;
                }
                return;
            }
            // In the light already: whatever it meant to do this tick, it goes straight out, away from the lamp.
            // (A tile is lit by its middle, so the lamp may be a little further than reach from where it stands.)
            var light = NearestLight(before, reach + 1);
            if (light == null) return;
            var away = before - light.Value;
            var way = away.LengthSquared > 1e-6f ? away.Normalized : new Vec2(1, 0);
            enemy.Position = Collision.Move(Map, before, radius, way * (Math.Max(enemy.Speed, 2) * TickSeconds), monster: true);
            enemy.Facing = way;
            enemy.Knockback = Vec2.Zero;
        }

        // ---- Spore clouds (HazardShape.Balm, MonsterPool.Cloud and the deathcloud trait) ----

        static float CloudShare(MonsterDefinition kind) => kind.Size == MonsterSize.XL ? CloudXlShare : kind.Size == MonsterSize.L ? CloudBigShare : 1;

        // A cloud's tick on the monsters in it: each fills by the cloud's percent of its health a second (a big body by
        // its share; standing in several, as from one), and what it got is counted to be told (TellCloudHeal).
        void FillFromCloud(Hazard cloud)
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.IsDead || enemy.Away || Vec2.Distance(enemy.Position, cloud.Position) > cloud.Size + enemy.Definition.Radius * 0.5f) continue;
                // Clouds over each other fill no faster than one.
                if (enemy.CloudTick == Tick) continue;
                enemy.CloudTick = Tick;
                float before = enemy.Health;
                enemy.Health = Math.Min(enemy.MaxHealth, enemy.Health + enemy.MaxHealth * cloud.Width / 100f * TickSeconds * CloudShare(enemy.Definition));
                enemy.CloudGain += enemy.Health - before;
            }
        }

        // Whether a monster stands in a spore cloud now (the playtests and the tests ask).
        public bool InCloud(Enemy enemy)
        {
            foreach (var hazard in _hazards)
                if (hazard.Shape == HazardShape.Balm && Vec2.Distance(enemy.Position, hazard.Position) <= hazard.Size + enemy.Definition.Radius * 0.5f) return true;
            return false;
        }

        // What the clouds gave a monster, told every CloudHealSeconds as a Healed event of the monster's (Target
        // CloudHealed): the screen's green numbers.
        void TellCloudHeal(Enemy enemy)
        {
            if (enemy.CloudGain < 1 || Tick < enemy.CloudToldAt) return;
            _events.Add(new SimEvent(SimEventType.Healed, enemy.Id, CloudHealed, (int)MathF.Round(enemy.CloudGain)));
            enemy.CloudGain = 0;
            enemy.CloudToldAt = Tick + Ticks(CloudHealSeconds);
        }

        // ---- Shedding (MonsterTraitKind.Shed) ----

        // Each step of health it has lost since it came drops one of the trait's kind at its edge, whole, of its pack
        // and its room (as a split's halves are, UpdateMonsterExtras).
        void Shed(Enemy enemy)
        {
            var shed = enemy.Definition.Trait(MonsterTraitKind.Shed);
            if (shed == null || enemy.Small || enemy.IsDead) return;
            float step = enemy.MaxHealth * shed.Number(1, 10) / 100f;
            int due = Math.Min(MostShed, (int)((enemy.MaxHealth - enemy.Health) / Math.Max(1, step)));
            if (enemy.Shed >= due) return;
            var kind = _monsters.Monsters.FirstOrDefaultOf(shed.Arguments[0]);
            if (kind == null) return;
            while (enemy.Shed < due)
            {
                enemy.Shed++;
                var way = Vec2.FromAngle(_random.NextFloat() * 2 * MathF.PI);
                var spot = enemy.Position + way * (enemy.Definition.Radius + kind.Radius + 0.2f);
                _queuedMonsters.Add((kind, spot, enemy, false));
                _events.Add(new SimEvent(SimEventType.MonsterFx, enemy.Id, amount: MonsterFxSplit, position: spot));
            }
        }
    }

    static class MonsterListExtensions
    {
        public static MonsterDefinition FirstOrDefaultOf(this IReadOnlyList<MonsterDefinition> monsters, string id)
        {
            foreach (var monster in monsters)
                if (monster.Id == id) return monster;
            return null;
        }
    }
}
