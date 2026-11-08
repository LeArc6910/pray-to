using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    [Flags]
    public enum RiftAffix : byte
    {
        None = 0,
        // Rift monsters move a third faster.
        Swift = 1,
        // They have half again their health.
        Tough = 2,
        // They hit a third harder.
        Fierce = 4,
        // Each wave has half again as many.
        Swarm = 8,
    }

    // A void rift opened at an altar (Docs/Bosses.md, 공허 균열): waves of stronger monsters around it with one to three
    // affixes, then a guardian, against the clock. Beating it drops rewards at the altar and opens the next tier.
    public sealed class Rift
    {
        internal Rift(int tier, Vec2 center, RiftAffix affixes, long ticks)
        {
            Tier = tier;
            Center = center;
            Affixes = affixes;
            TicksLeft = ticks;
        }

        public int Tier { get; }
        public Vec2 Center { get; }
        public RiftAffix Affixes { get; }
        // 1 to World.RiftWaves for the waves, RiftWaves + 1 for the guardian.
        public int Wave { get; internal set; }
        public long TicksLeft { get; internal set; }
        internal List<Enemy> Monsters { get; } = new List<Enemy>();
        public int MonstersLeft => Monsters.Count(m => !m.IsDead);
    }

    public sealed partial class World
    {
        public const string RiftKey = "rift_key";
        public const int RiftWaves = 3;
        public const float RiftSeconds = 300;
        // Tiers past 5 get this much more time each (2026-11-09): their guardians take long to bring down.
        public const float RiftSecondsPerTier = 20;

        public static float RiftSecondsFor(int tier) => RiftSeconds + RiftSecondsPerTier * Math.Max(0, tier - 5);
        public const int MaxRiftTier = 10;
        // An Enemy's Rift: brought by a rift, or the rift's guardian. The snapshot carries it in the monster's phase.
        public const int RiftMonster = 1;
        public const int RiftGuardian = 2;
        // A Rift event's Amount.
        public const int RiftFailed = 0;
        public const int RiftOpened = 1;
        public const int RiftCleared = 2;
        // What the rift's legendaries name as the one that drops them, and how much likelier each tier makes them.
        public const string RiftLegendaries = "rift";
        public const int RiftLegendaryPerTier = 3;
        // What tiers 3 and up give for the rift band and charm (recipes.csv).
        public const string RiftShard = "rift_shard";
        // A rift monster's health and hit against its sheet at tier 1, and how much more each tier adds.
        const float RiftHealth = 5f;
        const float RiftHealthPerTier = 1.5f;
        const float RiftPower = 1.8f;
        const float RiftPowerPerTier = 0.45f;
        // How far from the altar its monsters appear.
        const float RiftInner = 3;
        const float RiftOuter = 7;

        // The highest tier this world may open; beating it opens the next. A world save keeps it.
        public int RiftTier { get; private set; } = 1;
        public Rift ActiveRift { get; private set; }

        internal void RestoreRiftTier(int tier) => RiftTier = Math.Clamp(tier, 1, MaxRiftTier);

        // Affixes for a rift of the tier: one, and one more every three tiers, at most three, never the same twice.
        static readonly RiftAffix[] AllAffixes = { RiftAffix.Swift, RiftAffix.Tough, RiftAffix.Fierce, RiftAffix.Swarm };

        public static string AffixNames(RiftAffix affixes) => string.Join(", ", AllAffixes.Where(a => (affixes & a) != 0).Select(a => a switch
        {
            RiftAffix.Swift => "빠름",
            RiftAffix.Tough => "단단함",
            RiftAffix.Fierce => "사나움",
            _ => "떼",
        }));

        // A player beside an altar with a key opens a rift of the world's tier there. False when one is open already,
        // or there is no altar or key.
        public bool OpenRift(int playerId)
        {
            var player = FindPlayer(playerId);
            var key = Loot?.FindMaterial(RiftKey);
            if (player == null || player.IsDead || ActiveRift != null || key == null || player.Inventory.CountOf(key) == 0) return false;
            var altar = NearestTile(player.Position, TileType.RiftAltar, StationReach);
            if (altar == null) return false;
            player.Inventory.TakeMaterial(key);
            player.InventoryVersion++;

            int count = Math.Min(3, 1 + (RiftTier - 1) / 3);
            var affixes = RiftAffix.None;
            var open = AllAffixes.ToList();
            for (int i = 0; i < count; i++)
            {
                var pick = open[_random.Next(open.Count)];
                open.Remove(pick);
                affixes |= pick;
            }
            ActiveRift = new Rift(RiftTier, TileMap.Center(altar.Value.X, altar.Value.Y), affixes, Ticks(RiftSecondsFor(RiftTier)));
            _events.Add(new SimEvent(SimEventType.Rift, player.Id, amount: RiftOpened, position: ActiveRift.Center));
            NextRiftWave();
            return true;
        }

        void UpdateRift()
        {
            var rift = ActiveRift;
            if (rift == null) return;
            if (--rift.TicksLeft <= 0)
            {
                EndRift(false);
                return;
            }
            if (rift.MonstersLeft > 0) return;
            if (rift.Wave > RiftWaves)
            {
                EndRift(true);
                return;
            }
            NextRiftWave();
        }

        // The next wave, or after the last one the guardian.
        void NextRiftWave()
        {
            var rift = ActiveRift;
            rift.Wave++;
            rift.Monsters.Clear();
            // Set by DevTools/CoreSandbox --rift: two players in void gear close tier 1 in about two minutes, and the
            // tiers after it ask for upgraded gear and the passive tree.
            float health = (RiftHealth + RiftHealthPerTier * (rift.Tier - 1)) * ((rift.Affixes & RiftAffix.Tough) != 0 ? 1.5f : 1);
            float power = (RiftPower + RiftPowerPerTier * (rift.Tier - 1)) * ((rift.Affixes & RiftAffix.Fierce) != 0 ? 1.3f : 1);
            float speed = (rift.Affixes & RiftAffix.Swift) != 0 ? 1.3f : 1;
            // Only the deepest monsters come through: the rift opens after the last boss.
            int deepest = _monsters.Monsters.Max(m => m.Level);
            var kinds = _monsters.Monsters.Where(m => m.Level >= deepest - 1).ToList();
            if (rift.Wave > RiftWaves)
            {
                var strongest = kinds.OrderByDescending(m => m.Health).First();
                SpawnRiftMonster(strongest, health * (5 + rift.Tier), power * 1.5f, speed, RiftGuardian);
                return;
            }
            int count = (5 + rift.Tier) * ((rift.Affixes & RiftAffix.Swarm) != 0 ? 3 : 2) / 2;
            for (int i = 0; i < count; i++) SpawnRiftMonster(kinds[_random.Next(kinds.Count)], health, power, speed, RiftMonster);
        }

        void SpawnRiftMonster(MonsterDefinition kind, float health, float power, float speed, int rank)
        {
            var rift = ActiveRift;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                float angle = _random.NextFloat() * MathF.PI * 2;
                float distance = RiftInner + _random.NextFloat() * (RiftOuter - RiftInner);
                var at = rift.Center + new Vec2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
                if (!Map.InBounds(TileMap.Cell(at.X), TileMap.Cell(at.Y)) || Collision.Overlaps(Map, at, kind.Radius)) continue;
                var enemy = SpawnEnemy(kind, at);
                enemy.MaxHealth = enemy.Health = kind.Health * health;
                enemy.Power = power;
                enemy.SpeedScale = speed;
                enemy.Rift = rank;
                rift.Monsters.Add(enemy);
                return;
            }
            // No room: the wave does with one fewer.
        }

        // Beaten: rewards at the altar and, from the highest tier, the next one. Run out: the rest of its monsters go.
        void EndRift(bool cleared)
        {
            var rift = ActiveRift;
            ActiveRift = null;
            _events.Add(new SimEvent(SimEventType.Rift, 0, amount: cleared ? RiftCleared : RiftFailed, position: rift.Center));
            if (!cleared)
            {
                foreach (var monster in rift.Monsters) monster.Health = 0;
                _enemies.RemoveAll(e => rift.Monsters.Contains(e));
                return;
            }
            if (rift.Tier == RiftTier) RiftTier = Math.Min(MaxRiftTier, RiftTier + 1);
            if (_lootGenerator == null) return;
            int level = ProgressLevel + rift.Tier * 2;
            var minimum = rift.Tier >= 4 ? Rarity.Rare : Rarity.Uncommon;
            for (int i = 0; i < 2 + rift.Tier / 2; i++) DropItem(_lootGenerator.RollItem(level, _random, 0, minimum), Scatter(rift.Center));
            var essence = Loot.FindMaterial(Essence);
            for (int i = 0; essence != null && i < 3 * rift.Tier; i++) DropMaterial(essence, Scatter(rift.Center));
            var key = Loot.FindMaterial(RiftKey);
            if (key != null && _random.Chance(0.5f)) DropMaterial(key, Scatter(rift.Center));
            // From tier 3, shards for the rift's own gear (tier - 2 of them); from tier 2, half the time a support
            // crystal (Docs/Classes.md, 스킬 변형).
            var shard = Loot.FindMaterial(RiftShard);
            for (int i = 0; shard != null && i < rift.Tier - 2; i++) DropMaterial(shard, Scatter(rift.Center));
            var crystals = SkillSupports.Ids.Select(Loot.FindMaterial).Where(m => m != null).ToList();
            if (rift.Tier >= 2 && crystals.Count > 0 && _random.Chance(0.5f)) DropMaterial(crystals[_random.Next(crystals.Count)], Scatter(rift.Center));
            // The rift's own legendaries (item_bases.csv legendary "rift"): each its chance and 3% a tier.
            foreach (var legendary in _lootGenerator.LegendariesOf(RiftLegendaries).ToList())
            {
                if (_random.Next(100) < legendary.LegendaryChance + RiftLegendaryPerTier * rift.Tier)
                    DropItem(_lootGenerator.RollLegendary(legendary, _random), Scatter(rift.Center));
            }
        }

        // The tile of the type nearest the position within reach, or null.
        (int X, int Y)? NearestTile(Vec2 position, TileType type, float reach)
        {
            (int X, int Y)? best = null;
            float bestDistance = float.MaxValue;
            int r = (int)MathF.Ceiling(reach) + 1;
            int cx = TileMap.Cell(position.X), cy = TileMap.Cell(position.Y);
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (!Map.InBounds(x, y) || Map[x, y] != type) continue;
                    float d = Vec2.Distance(position, TileMap.Center(x, y));
                    if (d > reach || d >= bestDistance) continue;
                    best = (x, y);
                    bestDistance = d;
                }
            }
            return best;
        }
    }
}
