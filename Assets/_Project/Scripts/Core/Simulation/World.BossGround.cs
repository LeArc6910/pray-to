using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // A boss's ground (2026-11-28, the owner: "보스 아레나 주변에는 일반 몬스터가 스폰되지 않게 해줘 (보스가 소환하는
    // 몬스터는 제외)"): the arena and BossGroundMargin tiles round it. The field's monsters (the director's wanderers,
    // ambushes and raids, the packs of the day, the hunting grounds, the steady spawn) do not come into being there,
    // and one that walks or is knocked in is set back just outside its edge, as at the places of the story
    // (World.KeepOutOfPlaces). What the boss calls (summon) is not stopped, nor the ward stone's raiders. Bosses in a
    // dungeon's pocket keep to their rooms already (no field spawn comes into the pocket). Docs/Bosses.md 보스 둘레.
    public sealed partial class World
    {
        public const float BossGroundMargin = 6;

        public bool InBossGround(Vec2 position) => _bosses.Any(b => !b.IsDead && !Map.InPocket(b.Home)
            && Vec2.Distance(position, b.Home) <= b.ArenaRadius + BossGroundMargin);

        // Where no field monster comes into being: the safe places and the bosses' ground.
        public bool NoFieldSpawn(Vec2 position) => InSafePlace(position) || InBossGround(position);

        void KeepOutOfBossGrounds(Enemy enemy)
        {
            if (enemy.FromBoss || enemy.Rift == DefenseMonster || _bosses.Count == 0) return;
            foreach (var boss in _bosses)
            {
                if (boss.IsDead || Map.InPocket(boss.Home)) continue;
                float edge = boss.ArenaRadius + BossGroundMargin + enemy.Definition.Radius;
                var away = enemy.Position - boss.Home;
                float distance = away.Length;
                if (distance >= edge) continue;
                float angle = distance > 1e-3f ? away.Angle : -MathF.PI / 2;
                var spot = OffGround(boss, angle, edge, enemy.Definition.Radius);
                // An arena walled round by rock: no open floor outside near it, so it goes (seen in the web check, 2026-11-28).
                if (spot == null) { _offGround.Add(enemy); return; }
                enemy.Position = spot.Value;
                if (enemy.State == EnemyState.Chase) enemy.State = EnemyState.Idle;
            }
        }

        readonly HashSet<Enemy> _offGround = new HashSet<Enemy>();

        void RemoveOffGround()
        {
            if (_offGround.Count == 0) return;
            _enemies.RemoveAll(_offGround.Contains);
            _offGround.Clear();
        }

        // The open floor just outside a boss's ground, nearest the way it was pushed: straight out first, then turning
        // either way, a little further each round. The resolved spot must still be outside (a wall can push it back in).
        Vec2? OffGround(Boss boss, float angle, float edge, float radius)
        {
            for (float further = 0; further <= 6; further += 2)
                for (int turn = 0; turn <= 12; turn++)
                {
                    float a = angle + (turn % 2 == 0 ? 1 : -1) * (turn + 1) / 2 * MathF.PI / 12;
                    var at = Collision.Resolve(Map, boss.Home + Vec2.FromAngle(a) * (edge + further), radius, monster: true);
                    if (Vec2.Distance(at, boss.Home) >= edge - 0.01f && !Collision.Overlaps(Map, at, radius, monster: true)) return at;
                }
            return null;
        }
    }
}
