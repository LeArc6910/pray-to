using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // Fishing (Docs/Fishing.md): with a fishing rod in the bag, a player facing water casts. After a few seconds a fish
    // of the biome the water is in bites, and a marker starts sliding to and fro across the reel gauge, faster for a
    // harder fish. Pressing again while it is in the middle zone lands it; pressing outside it, or not within
    // BiteSeconds, and it gets away. Walking off, being hit or pressing before the bite reels the line in.
    public sealed partial class World
    {
        public const string FishingRod = "fishing_rod";
        // How long a fish takes to bite (before the fishing skill), set against what fish fetch (DevTools/CoreSandbox
        // --economy): a few hundred coins an hour in the plain cave.
        public const float BiteMinSeconds = 8;
        public const float BiteMaxSeconds = 20;
        public const float BiteSeconds = 4;
        // How far the player may move from where they cast before the line comes in.
        const float FishingLeash = 0.6f;
        // The chance of a second fish of the same kind.
        const float DoubleCatchChance = 0.12f;

        // A Fishing event's Amount.
        public const int FishingCast = 0;
        public const int FishingBite = 1;
        public const int FishingCaught = 2;
        public const int FishingEscaped = 3;
        public const int FishingReeled = 4;

        // A bite's Amount also carries the fish's difficulty, for the view's gauge: FishingBite + 16 × difficulty.
        public static int FishingKind(int amount) => amount & 15;
        public static int FishingDifficultyOf(int amount) => Math.Max(1, amount >> 4);

        // Casts, lands a biting fish, or reels in a line that has nothing on it yet. False when there is nothing to
        // do: no rod, or no water ahead to cast into.
        // While a fish bites, reportedTicks is how long the bite had gone on when the player pressed, as their screen
        // showed it (it cannot be more than has really passed); null for now.
        public bool Fish(int playerId, int? reportedTicks = null)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead) return false;
            var fishing = player.Fishing;
            if (fishing != null)
            {
                if (fishing.BiteTicks > 0)
                {
                    int at = Math.Clamp(reportedTicks ?? fishing.BiteElapsed, 0, fishing.BiteElapsed);
                    if (InCatchZone(GaugeAt(at, fishing.Difficulty), fishing.Difficulty, LifeLevel(player, LifeSkill.Fishing))) Land(player);
                    else EndFishing(player, FishingEscaped);
                }
                else
                {
                    EndFishing(player, FishingReeled);
                }
                return true;
            }
            if (!player.Inventory.Materials.Keys.Any(m => m.Id == FishingRod)) return false;
            var (x, y) = TileAhead(player.Position, player.Facing);
            if (!Map.InBounds(x, y) || Map[x, y] != TileType.Water) return false;
            int wait = Ticks(BiteMinSeconds) + _random.Next(Ticks(BiteMaxSeconds - BiteMinSeconds) + 1);
            wait = Math.Max(1, wait * (100 - LifeSkills.Bonus(LifeLevel(player, LifeSkill.Fishing), LifeSkills.BitePercent)) / 100);
            player.Fishing = new FishingLine(x, y, player.Position, wait);
            _events.Add(new SimEvent(SimEventType.Fishing, player.Id, amount: FishingCast, position: TileMap.Center(x, y)));
            return true;
        }

        void UpdateFishing(Player player)
        {
            var fishing = player.Fishing;
            if (fishing == null) return;
            if (player.IsDead || Vec2.Distance(player.Position, fishing.Stand) > FishingLeash)
            {
                EndFishing(player, FishingReeled);
                return;
            }
            if (fishing.WaitTicks > 0)
            {
                if (--fishing.WaitTicks > 0) return;
                // The fish is chosen as it bites, so the gauge can go at its speed.
                fishing.Catch = PickFish(fishing.X, fishing.Y);
                fishing.Difficulty = Difficulty(fishing.Catch);
                fishing.BiteTicks = Ticks(BiteSeconds);
                fishing.BiteElapsed = 0;
                _events.Add(new SimEvent(SimEventType.Fishing, player.Id, amount: FishingBite + 16 * fishing.Difficulty, position: fishing.Spot));
                return;
            }
            fishing.BiteElapsed++;
            if (--fishing.BiteTicks <= 0) EndFishing(player, FishingEscaped);
        }

        public const int MaxDifficulty = 5;

        // How hard a fish is to land, 1 to 5: by how deep its water is, one more for a night fish.
        public static int Difficulty(MaterialDefinition fish)
        {
            if (fish == null) return 1;
            int depth = fish.Fish switch
            {
                Biome.Mushroom => 2,
                Biome.Ice => 3,
                Biome.Crystal => 3,
                Biome.Lava => 4,
                Biome.Abyss => 5,
                Biome.Lake => 4,
                _ => 1,
            };
            return Math.Min(MaxDifficulty, depth + (fish.FishAtNight ? 1 : 0));
        }

        // Where the marker is on the reel gauge (0 the left end, 1 the right) after the bite has gone on so many ticks:
        // it slides from the left to the right and back, once in 1.6 seconds for the easiest fish, 0.8 for the hardest.
        public static float GaugeAt(int ticks, int difficulty)
        {
            int period = Math.Max(2, Ticks(1.8f - 0.2f * Math.Clamp(difficulty, 1, MaxDifficulty)));
            float phase = (ticks % period) / (float)period;
            return phase < 0.5f ? phase * 2 : 2 - phase * 2;
        }

        // Ticks into the bite when the marker first reaches the middle of the gauge.
        public static int CenterTicks(int difficulty) => Math.Max(2, Ticks(1.8f - 0.2f * Math.Clamp(difficulty, 1, MaxDifficulty))) / 4;

        // Half the width of the catch zone in the middle of the gauge: narrower for a harder fish, wider with the
        // fishing skill.
        public static float CatchHalfWidth(int difficulty, int fishingLevel) =>
            (0.24f - 0.03f * Math.Clamp(difficulty, 1, MaxDifficulty)) * (100 + LifeSkills.Bonus(fishingLevel, LifeSkills.ZonePercent)) / 100f;

        public static bool InCatchZone(float gauge, int difficulty, int fishingLevel) => Math.Abs(gauge - 0.5f) <= CatchHalfWidth(difficulty, fishingLevel);

        // A fish of the water's biome, night fish among them at night, or of the plain cave where that biome has none.
        MaterialDefinition PickFish(int x, int y)
        {
            var biome = Biomes[x, y];
            bool night = Calendar.IsNight(Clock);
            List<MaterialDefinition> Catches(Biome water) => Loot?.Materials.Where(m => m.Fish == water && (!m.FishAtNight || night)).ToList();
            var catches = Catches(biome);
            if (catches == null || catches.Count == 0) catches = Catches(Biome.Cave);
            return catches == null || catches.Count == 0 ? null : catches[_random.Next(catches.Count)];
        }

        // The fish that bit (materials.csv fish, Docs/Time.md for night fish), sometimes two; it teaches fishing.
        void Land(Player player)
        {
            var fishing = player.Fishing;
            var fish = fishing.Catch;
            if (fish != null)
            {
                player.Inventory.AddMaterial(fish, _random.Chance(DoubleCatchChance) ? 2 : 1);
                player.InventoryVersion++;
                GainLifeExperience(player, LifeSkill.Fishing, 3 + 2 * fishing.Difficulty);
            }
            EndFishing(player, FishingCaught);
        }

        void EndFishing(Player player, int how)
        {
            var fishing = player.Fishing;
            if (fishing == null) return;
            player.Fishing = null;
            _events.Add(new SimEvent(SimEventType.Fishing, player.Id, amount: how, position: fishing.Spot));
        }
    }

    // A line in the water: where it went in, where the player stood, and the ticks until a bite and of the bite.
    public sealed class FishingLine
    {
        internal FishingLine(int x, int y, Vec2 stand, int waitTicks)
        {
            X = x;
            Y = y;
            Stand = stand;
            WaitTicks = waitTicks;
        }

        public int X { get; }
        public int Y { get; }
        public Vec2 Spot => TileMap.Center(X, Y);
        public Vec2 Stand { get; }
        internal int WaitTicks { get; set; }
        internal int BiteTicks { get; set; }
        // How long the bite has gone on, the fish on the line and how hard it is (World.Difficulty).
        internal int BiteElapsed { get; set; }
        internal MaterialDefinition Catch { get; set; }
        public int Difficulty { get; internal set; } = 1;
    }
}
