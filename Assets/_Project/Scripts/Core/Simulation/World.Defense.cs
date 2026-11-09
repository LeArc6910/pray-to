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

        // The way to the stone round what is built, and the way through it (World.BuiltBarrier counted open), rebuilt
        // every so often while the defense lasts as walls go up and come down.
        readonly FlowField _toStone;
        readonly FlowField _toStoneThrough;
        const int DefenseFlowInterval = 15;
        // What it takes a monster to break a built tile, for each point of its hardness (tiles.csv).
        public const float BarrierHealthPerHardness = 40;

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
            BuildDefenseFlow();
            _events.Add(new SimEvent(SimEventType.Defense, player.Id, amount: DefenseStarted, position: ActiveDefense.Center));
            NextDefenseWave();
            return true;
        }

        // The stone itself is solid, so the ways lead to the tiles beside it.
        void BuildDefenseFlow()
        {
            var defense = ActiveDefense;
            var beside = Machine.Steps.Select(step => TileMap.Center(defense.X + step.X, defense.Y + step.Y)).ToList();
            _toStone.Build(beside, (int)DefenseOuter * 3);
            _toStoneThrough.Build(beside, (int)DefenseOuter * 3);
        }

        // A tile a player built that blocks monsters (a wall, a door, a machine; not a chest with something in it, as
        // digging leaves those too): monsters come for the ward stone break through these when there is no way round.
        public bool BuiltBarrier(int x, int y) =>
            Map.InBounds(x, y) && !Map.IsEdge(x, y) && Map.BlocksMonsters(x, y) && _changedTiles.TryGetValue(y * Map.Width + x, out var built) && built == Map[x, y]
            && Map[x, y] != TileType.WardStone && (ChestAt(x, y) is not Inventory chest || IsEmpty(chest));

        // A Dug event's Amount when a crack was mended (2026-11-09).
        public const int Repaired = 0;

        // What mends a built tile: an iron bar for an iron wall, a stone for anything else.
        public MaterialDefinition RepairMaterial(TileType tile) => Loot?.Materials.FirstOrDefault(m => m.Id == (tile == TileType.IronWall ? "iron_bar" : "stone"));

        // Mends the cracks in the built tile ahead (a wall, a door, a machine monsters struck) with one stone, or an
        // iron bar for an iron wall. False when there is nothing cracked ahead or nothing to mend it with.
        public bool Repair(int playerId)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead) return false;
            var (x, y) = TileAhead(player.Position, player.Facing);
            if (AnchorOf(Map, x, y) is (int ax, int ay)) (x, y) = (ax, ay);
            int key = y * Map.Width + x;
            if (!Map.InBounds(x, y) || !_wallDamage.ContainsKey(key) || !BuiltBarrier(x, y)) return false;
            var material = RepairMaterial(Map[x, y]);
            if (material == null || !player.Inventory.TakeMaterial(material, 1)) return false;
            player.InventoryVersion++;
            _wallDamage.Remove(key);
            _events.Add(new SimEvent(SimEventType.Dug, player.Id, amount: Repaired, position: TileMap.Center(x, y)));
            return true;
        }

        // A monster's blow on a built tile: it cracks like a wall being dug, and gives way at its hardness ×
        // BarrierHealthPerHardness. What it was drops there, and whatever a machine held.
        internal void DamageBarrier(Enemy enemy, int x, int y)
        {
            // A blow on a big machine's part is a blow on the machine.
            if (AnchorOf(Map, x, y) is (int ax, int ay)) (x, y) = (ax, ay);
            var tile = Tiles?.Find(Map[x, y]);
            float hardness = Math.Max(1, tile?.Hardness ?? 1);
            int key = y * Map.Width + x;
            float damage = _wallDamage.GetValueOrDefault(key) + enemy.Damage / BarrierHealthPerHardness;
            bool broken = damage >= hardness - 1e-4f;
            int dug = broken ? DugThrough : Math.Clamp((int)MathF.Round(damage / hardness * 100), 1, DugThrough - 1);
            _events.Add(new SimEvent(SimEventType.Dug, enemy.Id, amount: dug, position: TileMap.Center(x, y)));
            if (!broken)
            {
                _wallDamage[key] = damage;
                return;
            }
            _wallDamage.Remove(key);
            var at = TileMap.Center(x, y);
            var machine = MachineAt(x, y);
            if (machine != null)
            {
                foreach (var pair in machine.Input.Concat(machine.Output).ToList())
                {
                    for (int i = 0; i < pair.Value; i++) DropMaterial(pair.Key, Scatter(at));
                }
                if (machine.Carried != null) DropMaterial(machine.Carried, Scatter(at));
            }
            ClearTile(x, y);
            for (int i = 0; tile?.Material != null && i < tile.Count; i++) DropMaterial(tile.Material, Scatter(at));
            BuildDefenseFlow();
        }

        void UpdateDefense()
        {
            var defense = ActiveDefense;
            if (defense == null) return;
            if (Tick % DefenseFlowInterval == 0) BuildDefenseFlow();
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
                        || _toStoneThrough.DistanceAt(TileMap.Cell(at.X), TileMap.Cell(at.Y)) == FlowField.Unreached) continue;
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
            // Round what is built if there is a way, else through it: beating on the built tile in the way.
            bool open = _toStone.DistanceAt(TileMap.Cell(enemy.Position.X), TileMap.Cell(enemy.Position.Y)) != FlowField.Unreached;
            var direction = gap < 1.5f ? toStone.Normalized : (open ? _toStone : _toStoneThrough).DirectionFrom(enemy.Position);
            if (direction.LengthSquared < 1e-6f) direction = toStone.Normalized;
            enemy.Facing = direction;
            if (!open)
            {
                var probe = enemy.Position + direction * (enemy.Definition.Radius + 0.45f);
                int bx = TileMap.Cell(probe.X), by = TileMap.Cell(probe.Y);
                if (BuiltBarrier(bx, by))
                {
                    if (enemy.StateTicks > 0)
                    {
                        enemy.StateTicks--;
                        return true;
                    }
                    enemy.StateTicks = Ticks(enemy.Definition.Cooldown + enemy.Definition.Windup);
                    _events.Add(new SimEvent(SimEventType.Attack, enemy.Id, direction: enemy.Facing));
                    DamageBarrier(enemy, bx, by);
                    return true;
                }
            }
            enemy.Position = Collision.Move(Map, enemy.Position, enemy.Definition.Radius, direction * (enemy.Speed * TickSeconds), monster: true);
            return true;
        }

        // A turret's ammo: plain, frost (chills what it hits) and fire (burns it). Used in this order of preference:
        // the strongest it has first.
        public const string Ammo = "ammo";
        public const string FrostAmmo = "frost_ammo";
        public const string FireAmmo = "fire_ammo";
        static readonly string[] AmmoKinds = { FireAmmo, FrostAmmo, Ammo };
        public static bool IsAmmo(MaterialDefinition material) => material != null && Array.IndexOf(AmmoKinds, material.Id) >= 0;
        // How far a turret shoots, and how hard: more as bosses fall (World.ProgressLevel).
        public const float TurretRange = 7;
        public float TurretDamage => 20 + ProgressLevel;
        // A tesla tower: its reach, its bolt's share of a turret's damage (percent), and how many more monsters the bolt
        // leaps on to within TeslaLeap of the last.
        public const float TeslaRange = 6;
        public const int TeslaDamagePercent = 120;
        public const int TeslaLeaps = 2;
        const float TeslaLeap = 3;
        // A TurretShot event's Amount is its length × 10 plus TurretShotKind × what it was: 0 plain, 1 frost, 2 fire,
        // 3 lightning.
        public const int TurretShotKind = 1000;

        // The nearest monster in range a shot from here can reach (never a boss).
        Enemy TurretTarget(Vec2 at, float range, IEnumerable<Enemy> skip = null) => _enemies
            .Where(e => !e.IsDead && Vec2.Distance(e.Position, at) <= range && (skip == null || !skip.Contains(e))
                && ClearLine(at + (e.Position - at).Normalized * 0.7f, e.Position, 0.2f))
            .OrderBy(e => Vec2.Distance(e.Position, at))
            .FirstOrDefault();

        // Once its time is up a turret with ammo shoots the nearest monster it can see in range (never a boss), one
        // ammo a shot, the strongest it has. Without ammo or a target it stays ready, and shoots the moment it has both.
        void RunTurret(Machine turret)
        {
            if (turret.Timer < turret.Definition.Ticks) turret.Timer++;
            if (turret.Timer < turret.Definition.Ticks) return;
            var ammo = AmmoKinds.Select(id => turret.Input.Keys.FirstOrDefault(m => m.Id == id)).FirstOrDefault(m => m != null);
            if (ammo == null) return;
            var at = TileMap.Center(turret.X, turret.Y);
            var target = TurretTarget(at, TurretRange);
            if (target == null) return;
            turret.Timer = 0;
            Add(turret.Input, ammo, -1);
            var tags = ammo.Id == FireAmmo ? SkillTag.Fire : ammo.Id == FrostAmmo ? SkillTag.Cold : SkillTag.None;
            int kind = ammo.Id == FireAmmo ? 2 : ammo.Id == FrostAmmo ? 1 : 0;
            TurretHit(at, target, TurretDamage, tags, kind);
        }

        // A tesla tower runs only on power (machines.csv power_only): when its time is up it throws a bolt at the nearest
        // monster in reach, which leaps on to TeslaLeaps more near it. No ammo.
        void RunTesla(Machine tower)
        {
            if (tower.Timer < tower.Definition.Ticks) tower.Timer++;
            if (tower.Timer < tower.Definition.Ticks) return;
            var from = TileMap.Center(tower.X, tower.Y);
            var target = TurretTarget(from, TeslaRange);
            if (target == null) return;
            tower.Timer = 0;
            var struck = new List<Enemy>();
            for (int leap = 0; target != null && leap <= TeslaLeaps; leap++)
            {
                struck.Add(target);
                TurretHit(from, target, TurretDamage * TeslaDamagePercent / 100f, SkillTag.Lightning, 3);
                from = target.Position;
                target = _enemies
                    .Where(e => !e.IsDead && !struck.Contains(e) && Vec2.Distance(e.Position, from) <= TeslaLeap)
                    .OrderBy(e => Vec2.Distance(e.Position, from))
                    .FirstOrDefault();
            }
        }

        // A turret's or tower's shot landing: the streak for the view, the damage (its element against the monster's
        // resistance), a little push, and a frost shot's chill or a fire shot's burn.
        void TurretHit(Vec2 from, Enemy target, float damage, SkillTag tags, int kind)
        {
            var toTarget = target.Position - from;
            _events.Add(new SimEvent(SimEventType.TurretShot, 0, amount: (int)(toTarget.Length * 10) + TurretShotKind * kind, direction: toTarget.Normalized,
                position: from));
            int amount = Combat.Mitigate(damage * ElementFactor(target.Definition, tags), target.Definition.Armor);
            target.Health -= amount;
            target.Knockback = toTarget.Normalized * KnockbackSpeed * 0.5f;
            _events.Add(new SimEvent(SimEventType.Hit, 0, target.Id, amount));
            if (!target.IsDead)
            {
                if ((tags & SkillTag.Fire) != 0 && target.Definition.Resist(SkillTag.Fire) < Immune) target.Afflictions.Burn(amount * BurnShare, Ticks(BurnSeconds), 0);
                if ((tags & SkillTag.Cold) != 0 && target.Definition.Resist(SkillTag.Cold) < Immune)
                    target.Afflictions.ChillTicks = Math.Max(target.Afflictions.ChillTicks, Ticks(ChillSeconds));
                return;
            }
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
