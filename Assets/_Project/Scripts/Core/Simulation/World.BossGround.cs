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
                var outward = distance > 1e-3f ? away / distance : new Vec2(0, -1);
                enemy.Position = Collision.Resolve(Map, boss.Home + outward * edge, enemy.Definition.Radius, monster: true);
                if (enemy.State == EnemyState.Chase) enemy.State = EnemyState.Idle;
            }
        }
    }
}
