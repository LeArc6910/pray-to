using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // The rules the ice cave and the sand cave bring (2026-11-24, Docs/BiomeRework.md B2b): a charge that ends on ice
    // slides on and is dazed by a wall it slides into; icicles fall by the walls of the ice cave's big halls while a fight
    // is on, their shadow on the floor a second before; a bomber rolls slowly through quicksand; a mid-boss stands as a
    // statue or a beast in the ice until it is roused; and the sand cave's buried monsters lie under a stream of falling
    // sand (the screen draws it, World.UnderFallingSand says where). All of it is the host's, by its own random stream.
    public sealed partial class World
    {
        // ---- Ice: charges slide ----

        // A charge that ends on ice goes on this many tiles more (4-1 얼음 동굴: "늑대가 얼음 위에서 더 미끄러져 벽에 부딪힌다").
        public const float IceSlideTiles = 2;
        // MonsterFx: a charge went on sliding on the ice (Position: where it began to slide); a dormant one woke.
        public const int MonsterFxSlide = 16;
        public const int MonsterFxWake = 17;

        // Whether a monster's middle stands on ice.
        bool OnIce(Enemy enemy) => PlayerMotor.OnIce(Map, enemy.Position);

        // A charge's run is over: on ice it slides once more, IceSlideTiles at its pace. True when it does.
        bool SlideOn(Enemy enemy)
        {
            if (enemy.Slid || enemy.Attack?.Shape != MonsterAttackShape.Charge || !OnIce(enemy)) return false;
            enemy.Slid = true;
            enemy.StateTicks = Math.Max(1, (int)MathF.Ceiling(IceSlideTiles / (Math.Max(1, enemy.RushSpeed) * TickSeconds) - 1e-3f));
            _events.Add(new SimEvent(SimEventType.MonsterFx, enemy.Id, amount: MonsterFxSlide, position: enemy.Position));
            return true;
        }

        // ---- Icicles (4-1 얼음 동굴: "떨어지는 고드름, 큰 방의 벽 곁, 천장 그늘 원으로 1초 예고") ----

        // While a fight is on (a monster within IcicleFightReach is after them) and they stand in the ice cave in a wide
        // place (World.Wide) within IcicleWallReach of rock, an icicle falls near a player every IcicleMin~IcicleMax
        // seconds: its shadow on the floor IcicleTelegraph before, IcicleRadius wide, IcicleSpread at most from them. It
        // hurts the player for IcicleDamage (a hit of the band of the ground it falls on, Docs/Difficulty.md: about 5% of
        // the health in iron gear) and chills, and a monster under it for a share of its
        // health (a big body less): luring them under is the hall's question.
        public const float IcicleWallReach = 2.5f;
        public const float IcicleFightReach = 12;
        public const float IcicleMin = 3.5f;
        public const float IcicleMax = 6.5f;
        public const float IcicleTelegraph = 1f;
        public const float IcicleRadius = 0.8f;
        public const float IcicleSpread = 1.6f;
        public const float IcicleDamage = 34;
        public const float IcicleChill = 1.5f;
        public const float IcicleMonsterShare = 0.25f;
        const int IcicleCheckTicks = 6;

        GameRandom _iceRandom;
        GameRandom IceRandom => _iceRandom ??= new GameRandom(_forageSeed, stream: 61);
        // When the next icicle may fall over each player, and the icicles falling now (to strike the monsters under them).
        readonly Dictionary<int, long> _nextIcicle = new Dictionary<int, long>();
        readonly List<(int Hazard, int Player, Vec2 At, long Lands)> _icicles = new List<(int, int, Vec2, long)>();
        // How many icicles have fallen (the playtests and --pace).
        public int IciclesFallen { get; private set; }

        // Whether a player standing there would have icicles over them: the ice cave, a wide place, rock near.
        public bool UnderIcicles(Vec2 at)
        {
            int x = TileMap.Cell(at.X), y = TileMap.Cell(at.Y);
            if (!Map.InBounds(x, y) || Biomes[x, y] != Biome.Ice || InSafePlace(at) || !Wide(at)) return false;
            int reach = (int)MathF.Ceiling(IcicleWallReach);
            for (int dy = -reach; dy <= reach; dy++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    if (!Map.InBounds(x + dx, y + dy) || !Map.IsSolid(x + dx, y + dy)) continue;
                    if (Vec2.Distance(TileMap.Center(x + dx, y + dy), at) <= IcicleWallReach + 0.5f) return true;
                }
            }
            return false;
        }

        // Whether a monster after the player is near enough that a fight is on.
        bool Fighting(Player player) => _enemies.Any(e => !e.IsDead && e.TargetId == player.Id && e.State != EnemyState.Idle
            && Vec2.Distance(e.Position, player.Position) <= IcicleFightReach);

        void UpdateIcicles()
        {
            for (int i = _icicles.Count - 1; i >= 0; i--)
            {
                var icicle = _icicles[i];
                if (Tick < icicle.Lands) continue;
                _icicles.RemoveAt(i);
                IciclesFallen++;
                var by = FindPlayer(icicle.Player);
                foreach (var enemy in _enemies.ToList())
                {
                    if (enemy.IsDead || enemy.Away || Vec2.Distance(enemy.Position, icicle.At) > IcicleRadius + enemy.Definition.Radius * 0.5f) continue;
                    float share = enemy.Definition.Size == MonsterSize.XL ? CloudXlShare : enemy.Definition.Big ? CloudBigShare : 1;
                    if (by != null) HitEnemy(by, enemy, enemy.MaxHealth * IcicleMonsterShare * share, false, icicle.At, SkillTag.Cold, knockback: false);
                }
            }
            if (Tick % IcicleCheckTicks != 0) return;
            foreach (var player in _players)
            {
                if (player.IsDead || player.Riding || !UnderIcicles(player.Position) || !Fighting(player))
                {
                    _nextIcicle.Remove(player.Id);
                    continue;
                }
                if (!_nextIcicle.TryGetValue(player.Id, out long next))
                {
                    // The first falls a while after the fight comes under the ice, not at once.
                    _nextIcicle[player.Id] = Tick + Ticks(IcicleMin + IceRandom.NextFloat() * (IcicleMax - IcicleMin));
                    continue;
                }
                if (Tick < next) continue;
                _nextIcicle[player.Id] = Tick + Ticks(IcicleMin + IceRandom.NextFloat() * (IcicleMax - IcicleMin));
                // Not on top of the monsters' own marks round them (the cap on marks, World.MaxMarksPerTarget).
                int marks = _hazards.Count(h => h.Mark != 0 && !h.Attached && !h.Lingers && h.Damage > 0 && Vec2.Distance(h.Position, player.Position) <= h.Size + 2);
                if (marks >= MaxMarksPerTarget) continue;
                var spot = IcicleSpot(player.Position);
                if (spot == null) continue;
                var hazard = new Hazard(_nextId++, 0, 0, HazardShape.Circle, spot.Value, new Vec2(0, -1), IcicleRadius, 0, (int)IcicleDamage, Tick, Tick + Ticks(IcicleTelegraph))
                {
                    Mark = Hazard.Icicle,
                    Effect = HitEffect.Chill,
                    EffectAmount = IcicleChill,
                };
                _hazards.Add(hazard);
                _icicles.Add((hazard.Id, player.Id, spot.Value, hazard.ResolveTick));
            }
        }

        // Where an icicle falls near a player: open floor within IcicleSpread of them that is itself under the ice's
        // ceiling (near rock), tried a few times; null when none was found.
        Vec2? IcicleSpot(Vec2 near)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var at = near + Vec2.FromAngle(IceRandom.NextFloat() * 2 * MathF.PI) * (IceRandom.NextFloat() * IcicleSpread);
                int x = TileMap.Cell(at.X), y = TileMap.Cell(at.Y);
                if (!Map.InBounds(x, y) || Map.IsSolid(x, y) || Map[x, y] == TileType.Water || Map[x, y] == TileType.Lava) continue;
                if (!UnderIcicles(at)) continue;
                return at;
            }
            return null;
        }

        // For the playtests and tests: an icicle over a player now, wherever they stand (a fight or not).
        internal Hazard DropIcicle(Player player, Vec2 at)
        {
            var hazard = new Hazard(_nextId++, 0, 0, HazardShape.Circle, at, new Vec2(0, -1), IcicleRadius, 0, (int)IcicleDamage, Tick, Tick + Ticks(IcicleTelegraph))
            {
                Mark = Hazard.Icicle,
                Effect = HitEffect.Chill,
                EffectAmount = IcicleChill,
            };
            _hazards.Add(hazard);
            _icicles.Add((hazard.Id, player.Id, at, hazard.ResolveTick));
            return hazard;
        }

        // ---- Quicksand (4-2 자폭 풍뎅이: "모래늪에서 느리다") ----

        // How fast a monster rolls or runs at its target on the floor under it: quicksand slows it as it slows a player.
        float GroundPace(Enemy enemy) => PlayerMotor.GroundSpeed(Map, enemy.Position);

        // ---- Dormant mid-bosses (MonsterTraitKind.Dormant) ----

        // How near a dormant one notices a player is World.NoticeReach (with a crystal mimic's, B3b).

        // Whether a monster stands dormant now: one of that trait with no one to fight (the screen draws it as a statue).
        public static bool IsDormant(MonsterDefinition kind, EnemyState state) => state == EnemyState.Idle && (kind.Has(MonsterTraitKind.Dormant) || kind.Has(MonsterTraitKind.Mimic));

        // The same for a monster in the world (an elite mimic leads its fight and stands as itself, World.Mimics).
        public static bool IsDormant(Enemy enemy) => IsDormant(enemy.Definition, enemy.State) && (enemy.Definition.Has(MonsterTraitKind.Dormant) || Mimics(enemy));

        // A dormant one that has come to fight is told to the screen once (the stone cracks, the ice bursts).
        void WakeDormant(Enemy enemy)
        {
            bool mimic = Mimics(enemy);
            if (!mimic && !enemy.Definition.Has(MonsterTraitKind.Dormant)) return;
            if (mimic) WakeLastMimic(enemy);
            bool dormant = enemy.State == EnemyState.Idle;
            if (enemy.Dormant && !dormant)
            {
                _events.Add(new SimEvent(SimEventType.MonsterFx, enemy.Id, amount: MonsterFxWake, position: enemy.Position));
                // A mimic's crystals ring the others near it awake (B3b).
                if (mimic) RingMimics(enemy);
            }
            enemy.Dormant = dormant;
        }

        // ---- Falling sand (4-1 모래 동굴: "모래가 흐르는 곳 아래에는 무언가 묻혀 있다") ----

        // Whether a stream of sand falls over this monster: one under the ground (waiting in ambush or between its
        // attacks) in the sand cave. The screen draws the stream so the buried are seen before they come up.
        public static bool UnderFallingSand(BiomeMap biomes, Vec2 at, bool buried) =>
            buried && biomes != null && biomes[TileMap.Cell(at.X), TileMap.Cell(at.Y)] == Biome.Sand;

        public bool UnderFallingSand(Enemy enemy) => UnderFallingSand(Biomes, enemy.Position, enemy.Buried);
    }
}
