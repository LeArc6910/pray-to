using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // What machines made: a drill's ore, a smelter's bar, a harvester's crop, an assembler's make. Kept by the minute
    // for the last World.ProductionMinutes, for the production list (Docs/Automation.md, 생산 통계). Not saved.
    public sealed partial class World
    {
        public const int ProductionMinutes = 10;

        readonly Dictionary<object, int>[] _made = new Dictionary<object, int>[ProductionMinutes];
        long _madeMinute;

        // Goes up whenever anything is counted, so the host knows when to send the list again.
        public int ProductionVersion { get; private set; }

        static int TicksPerMinute => Ticks(60);

        // A material or consumable a machine made.
        void CountMade(object made, int count)
        {
            if (made == null || count <= 0) return;
            long minute = RollProduction();
            var bucket = _made[minute % ProductionMinutes] ??= new Dictionary<object, int>();
            bucket[made] = bucket.GetValueOrDefault(made) + count;
            ProductionVersion++;
        }

        // Moves on to this minute, emptying the minutes that have fallen out of the window.
        long RollProduction()
        {
            long minute = Tick / TicksPerMinute;
            if (minute - _madeMinute >= ProductionMinutes)
            {
                foreach (var bucket in _made) bucket?.Clear();
            }
            else
            {
                for (long m = _madeMinute + 1; m <= minute; m++) _made[m % ProductionMinutes]?.Clear();
            }
            _madeMinute = Math.Max(_madeMinute, minute);
            return minute;
        }

        // How many minutes the list covers: since the world started (a loaded one starts again at tick 0), at most the
        // window.
        public float ProductionSpan
        {
            get
            {
                long window = Tick - (Tick / TicksPerMinute - (ProductionMinutes - 1)) * TicksPerMinute;
                return Math.Max(1, Math.Min(Tick, window)) / (float)TicksPerMinute;
            }
        }

        // Everything made in the window, most first.
        public IReadOnlyList<(object Made, int Count)> Production()
        {
            RollProduction();
            var totals = new Dictionary<object, int>();
            foreach (var bucket in _made)
            {
                if (bucket == null) continue;
                foreach (var pair in bucket) totals[pair.Key] = totals.GetValueOrDefault(pair.Key) + pair.Value;
            }
            return totals.Select(p => (p.Key, p.Value)).OrderByDescending(p => p.Value).ThenBy(p => NameOf(p.Key), StringComparer.Ordinal).ToList();
        }

        static string NameOf(object made) => made switch
        {
            MaterialDefinition m => m.Name,
            ConsumableDefinition c => c.Name,
            _ => "",
        };
    }
}
