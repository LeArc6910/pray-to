using System;
using System.Diagnostics;
using Game.Core;

namespace Game.Client
{
    // Makes a world a slice of time per frame (Docs/TitleRework.md 4-4): the web has no threads, so the stages of
    // WorldGenerator.Steps run on the main thread until about BudgetMilliseconds have gone, and the loading screen draws in
    // between. A stage longer than the budget still runs whole (the longest, the rooms of the game's map, about 0.1 s on
    // a desktop). Used for a solo or hosted world (the generation itself) and for a joined one (WorldClient.StepMap).
    sealed class WorldLoader
    {
        public const double BudgetMilliseconds = 12;

        readonly WorldGeneration _generation;
        readonly Func<bool> _step;

        // A world made here: step runs one stage of it.
        public WorldLoader(WorldGeneration generation) : this(generation, generation.Step)
        {
        }

        // step: runs one stage, true while more remain (WorldClient.StepMap for a joined world).
        public WorldLoader(WorldGeneration generation, Func<bool> step)
        {
            _generation = generation ?? throw new ArgumentNullException(nameof(generation));
            _step = step ?? throw new ArgumentNullException(nameof(step));
        }

        public bool Done { get; private set; }
        // The stage being made (GenerationStage's keys: "rooms", "places", ...) and how far along, 0 to 1.
        public string Stage => _generation.Stage;
        public float Fraction => Done ? 1 : _generation.Fraction;
        // The world, once Done (for a joined world the client has already taken it).
        public GeneratedWorld World => _generation.World;
        // Milliseconds spent making it, and the longest frame of it.
        public double Milliseconds { get; private set; }
        public double LongestFrame { get; private set; }

        // Runs stages until the budget is spent or the world is made; true once it is.
        public bool Advance(double budget = BudgetMilliseconds)
        {
            if (Done) return true;
            var clock = Stopwatch.StartNew();
            do
            {
                if (_step()) continue;
                Done = true;
                break;
            }
            while (clock.Elapsed.TotalMilliseconds < budget);
            double spent = clock.Elapsed.TotalMilliseconds;
            Milliseconds += spent;
            LongestFrame = Math.Max(LongestFrame, spent);
            return Done;
        }
    }
}
