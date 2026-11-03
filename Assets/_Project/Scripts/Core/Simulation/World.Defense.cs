using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // A base defense under way (Docs/Defense.md): waves of monsters come from far off for the ward stone, and the
    // stone must last until the last of them falls.
    public sealed class Defense
    {
        internal Defense(int level, int x, int y, float stone)
        {
            Level = level;
            X = x;
            Y = y;
            MaxStoneHealth = StoneHealth = stone;
        }

        public int Level { get; }
        // The ward stone's tile.
        public int X { get; }
        public int Y { get; }
        public Vec2 Center => TileMap.Center(X, Y);
        public float StoneHealth { get; internal set; }
        public float MaxStoneHealth { get; }
        // 1 to World.DefenseWaves.
        public int Wave { get; internal set; }
        // Ticks since the wave came.
        public long WaveTicks { get; internal set; }
        internal List<Enemy> Monsters { get; } = new List<Enemy>();
        public int MonstersLeft => Monsters.Count(m => !m.IsDead);
    }

    public sealed partial class World
    {
        public const int DefenseWaves = 5;
        public const int MaxDefenseLevel = 10;
        // A wave that is still about after this long is joined by the next.
        public const float DefenseWaveSeconds = 40;
        // Where the monsters come from: this far from the stone.
        const float DefenseInner = 11;
        const float DefenseOuter = 17;
        // The ward stone's health at level 1, and how much more each level gives it.
        const float StoneHealth = 600;
        const float StoneHealthPerLevel = 150;
        // An Enemy's Rift mark for one that came to the stone (the view tints it).
        public const int DefenseMonster = 3;
        // A Defense event's Amount.
        public const int DefenseLost = 0;
        public const int DefenseStarted = 1;
        public const int DefenseWon = 2;

        // The level this world defends at next (beating it raises it), and the day it was last defended: once a day.
        public int DefenseLevel { get; private set; } = 1;
        public int DefenseDay { get; private set; }
        public Defense ActiveDefense { get; private set; }

        readonly FlowField _toStone;

        internal void RestoreDefense(int level, int day)
        {
            DefenseLevel = Math.Clamp(level, 1, MaxDefenseLevel);
            DefenseDay = Math.Max(0, day);
        }

        public bool DefendedToday => DefenseDay == Today;

        // The coins each defender gets for holding a level.
        public static int DefenseCoins(int level) => 40 + 30 * level;

        // A player beside a ward stone starts the day's defense there. False when one (or a rift) is under way, it was
        // done today, or there is no stone.
        public bool StartDefense(int playerId)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || ActiveDefense != null || ActiveRift != null || DefendedToday) return false;
            var stone = NearestTile(player.Position, TileType.WardStone, StationReach);
            if (stone == null) return false;
            DefenseDay = Today;
            ActiveDefense = new Defense(DefenseLevel, stone.Value.X, stone.Value.Y, StoneHealth + StoneHealthPerLevel * (DefenseLevel - 1));
            // The stone itself is solid, so the way leads to the tiles beside it.
            _toStone.Build(Machine.Steps.Select(step => TileMap.Center(stone.Value.X + step.X, stone.Value.Y + step.Y)), (int)DefenseOuter * 3);
            _events.Add(new SimEvent(SimEventType.Defense, player.Id, amount: DefenseStarted, position: ActiveDefense.Center));
            NextDefenseWave();
            return true;
        }

        void UpdateDefense()
        {
            var defense = ActiveDefense;
            if (defense == null) return;
            // Dug up or built over: there is nothing left to defend.
            if (defense.StoneHealth <= 0 || Map[defense.X, defense.Y] != TileType.WardStone)
            {
                EndDefense(false);
                return;
            }
            defense.WaveTicks++;
            bool cleared = defense.MonstersLeft == 0;
            if (defense.Wave >= DefenseWaves)
            {
                if (cleared) EndDefense(true);
                return;
            }
            if (cleared || defense.WaveTicks >= Ticks(DefenseWaveSeconds)) NextDefenseWave();
        }

        // The next wave: more monsters each wave and each level, of the kinds that live around the stone and the
        // deeper ones as the level climbs, tougher with the level.
        void NextDefenseWave()
        {
            var defense = ActiveDefense;
            defense.Wave++;
            defense.WaveTicks = 0;
            int zone = Math.Max(ZoneLevelAt(defense.Center), 1 + (defense.Level - 1) / 3);
            var kinds = _monsters.Monsters.Where(m => m.Level <= zone && m.Weight > 0).ToList();
            if (kinds.Count == 0) kinds = _monsters.Monsters.ToList();
            float health = 1 + 0.3f * (defense.Level - 1) + 0.1f * (defense.Wave - 1);
            float power = 1 + 0.15f * (defense.Level - 1);
            int count = 3 + defense.Level + 2 * defense.Wave;
            for (int i = 0; i < count; i++)
            {
                var kind = kinds[_random.Next(kinds.Count)];
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    float angle = _random.NextFloat() * MathF.PI * 2;
                    float distance = DefenseInner + _random.NextFloat() * (DefenseOuter - DefenseInner);
                    var at = defense.Center + new Vec2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
                    // Only where it can walk to the stone from.
                    if (!Map.InBounds(TileMap.Cell(at.X), TileMap.Cell(at.Y)) || Collision.Overlaps(Map, at, kind.Radius)
                        || _toStone.DistanceAt(TileMap.Cell(at.X), TileMap.Cell(at.Y)) == FlowField.Unreached) continue;
                    var enemy = SpawnEnemy(kind, at);
                    enemy.MaxHealth = enemy.Health = kind.Health * health;
                    enemy.Power = power;
                    enemy.Rift = DefenseMonster;
                    defense.Monsters.Add(enemy);
                    break;
                }
            }
        }

        // A monster come for the stone with no player in reach walks to it and, there, beats on it.
        bool MarchOnStone(Enemy enemy)
        {
            var defense = ActiveDefense;
            if (defense == null || enemy.Rift != DefenseMonster) return false;
            var toStone = defense.Center - enemy.Position;
            float gap = toStone.Length - enemy.Definition.Radius - 0.5f;
            if (gap <= Math.Max(enemy.Definition.Reach, 0.4f))
            {
                enemy.Facing = toStone.Normalized;
                if (enemy.StateTicks > 0)
                {
                    enemy.StateTicks--;
                    return true;
                }
                enemy.StateTicks = Ticks(enemy.Definition.Cooldown + enemy.Definition.Windup);
                defense.StoneHealth = Math.Max(0, defense.StoneHealth - enemy.Damage);
                _events.Add(new SimEvent(SimEventType.Attack, enemy.Id, direction: enemy.Facing));
                return true;
            }
            var direction = gap < 1.5f ? toStone.Normalized : _toStone.DirectionFrom(enemy.Position);
            if (direction.LengthSquared < 1e-6f) direction = toStone.Normalized;
            enemy.Facing = direction;
            enemy.Position = Collision.Move(Map, enemy.Position, enemy.Definition.Radius, direction * (enemy.Speed * TickSeconds), monster: true);
            return true;
        }

        public const string Ammo = "ammo";
        // How far a turret shoots, and how hard: more as bosses fall (World.ProgressLevel).
        public const float TurretRange = 7;
        public float TurretDamage => 20 + ProgressLevel;

        // Once its time is up a turret with ammo shoots the nearest monster it can see in range (never a boss), one
        // ammo a shot. Without ammo or a target it stays ready, and shoots the moment it has both.
        void RunTurret(Machine turret)
        {
            if (turret.Timer < turret.Definition.Ticks) turret.Timer++;
            if (turret.Timer < turret.Definition.Ticks) return;
            var ammo = turret.Input.Keys.FirstOrDefault(m => m.Id == Ammo);
            if (ammo == null) return;
            var at = TileMap.Center(turret.X, turret.Y);
            var target = _enemies
                .Where(e => !e.IsDead && Vec2.Distance(e.Position, at) <= TurretRange
                    && ClearLine(at + (e.Position - at).Normalized * 0.7f, e.Position, 0.2f))
                .OrderBy(e => Vec2.Distance(e.Position, at))
                .FirstOrDefault();
            if (target == null) return;
            turret.Timer = 0;
            Add(turret.Input, ammo, -1);
            var toTarget = target.Position - at;
            _events.Add(new SimEvent(SimEventType.TurretShot, 0, amount: (int)(toTarget.Length * 10), direction: toTarget.Normalized, position: at));
            int amount = Combat.Mitigate(TurretDamage, target.Definition.Armor);
            target.Health -= amount;
            target.Knockback = toTarget.Normalized * KnockbackSpeed * 0.5f;
            _events.Add(new SimEvent(SimEventType.Hit, 0, target.Id, amount));
            if (!target.IsDead) return;
            _events.Add(new SimEvent(SimEventType.Died, target.Id));
            DropFrom(target, null);
        }

        // Held: rewards at the stone and, from the highest level, the next one. Fallen: the monsters go home.
        void EndDefense(bool won)
        {
            var defense = ActiveDefense;
            ActiveDefense = null;
            _events.Add(new SimEvent(SimEventType.Defense, 0, amount: won ? DefenseWon : DefenseLost, position: defense.Center));
            foreach (var monster in defense.Monsters.Where(m => !m.IsDead)) monster.Health = 0;
            _enemies.RemoveAll(e => defense.Monsters.Contains(e) && e.IsDead);
            if (!won) return;
            if (defense.Level == DefenseLevel) DefenseLevel = Math.Min(MaxDefenseLevel, DefenseLevel + 1);
            // Coins straight to everyone who stood by the stone; essence and gear at it.
            var coin = CoinMaterial;
            foreach (var player in _players.Where(p => !p.IsDead && Vec2.Distance(p.Position, defense.Center) <= DefenseOuter + 3))
            {
                if (coin == null) break;
                player.Inventory.AddMaterial(coin, DefenseCoins(defense.Level));
                player.InventoryVersion++;
            }
            var essence = Loot?.Materials.FirstOrDefault(m => m.Id == Essence);
            for (int i = 0; essence != null && i < 2 * defense.Level; i++) DropMaterial(essence, Scatter(defense.Center));
            if (_lootGenerator == null) return;
            int level = ProgressLevel + defense.Level * 2;
            for (int i = 0; i < 1 + defense.Level / 2; i++) DropItem(_lootGenerator.RollItem(level, _random, 0, Rarity.Uncommon), Scatter(defense.Center));
        }
    }
}
