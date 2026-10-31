using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // A placed machine: which way it faces, and what it holds. A drill and a smelter keep what they made until it
    // can go out; a smelter also keeps what came in; a conveyor, a splitter and a robot arm carry one material along.
    // See Docs/Automation.md.
    public sealed class Machine
    {
        internal Machine(MachineDefinition definition, int x, int y, int direction)
        {
            Definition = definition;
            X = x;
            Y = y;
            Direction = direction;
        }

        public MachineDefinition Definition { get; }
        public TileType Kind => Definition.Tile;
        public int X { get; }
        public int Y { get; }
        // 0 right, 1 up, 2 left, 3 down: the way it faced when placed.
        public int Direction { get; internal set; }
        // A conveyor's, splitter's or arm's load and how far along the tile it has come (or the arm has swung), 0 to 1.
        public MaterialDefinition Carried { get; internal set; }
        public float Progress { get; internal set; }
        // Ticks toward the next ore or bar; for a splitter, which of its ways out is next.
        internal int Timer { get; set; }
        // For a splitter: the side its load came in from (a direction), so it never sends it back, or -1.
        internal int CameFrom { get; set; } = -1;
        internal Dictionary<MaterialDefinition, int> Input { get; } = new Dictionary<MaterialDefinition, int>();
        internal Dictionary<MaterialDefinition, int> Output { get; } = new Dictionary<MaterialDefinition, int>();

        public IReadOnlyDictionary<MaterialDefinition, int> Inputs => Input;
        public IReadOnlyDictionary<MaterialDefinition, int> Outputs => Output;

        public (int X, int Y) Front => (X + Steps[Direction].X, Y + Steps[Direction].Y);
        public (int X, int Y) Back => (X - Steps[Direction].X, Y - Steps[Direction].Y);

        public static readonly (int X, int Y)[] Steps = { (1, 0), (0, 1), (-1, 0), (0, -1) };

        // The nearest of the four directions to a facing.
        public static int DirectionOf(Vec2 facing) =>
            Math.Abs(facing.X) >= Math.Abs(facing.Y) ? (facing.X >= 0 ? 0 : 2) : (facing.Y >= 0 ? 1 : 3);
    }

    // Drills, conveyors, smelters, splitters and robot arms, stepped every tick in tile order so the result never
    // depends on anything else.
    public sealed partial class World
    {
        readonly SortedDictionary<int, Machine> _machines = new SortedDictionary<int, Machine>();
        List<Recipe> _smelts;

        public IEnumerable<Machine> Machines => _machines.Values;

        public Machine MachineAt(int x, int y) => _machines.TryGetValue(y * Map.Width + x, out var machine) ? machine : null;

        // The furnace's melting recipes: materials in, a material out.
        IReadOnlyList<Recipe> Smelts => _smelts ??= Loot?.Recipes.Where(r => r.Station == TileType.Furnace && r.Material != null).ToList() ?? new List<Recipe>();

        // A machine tile appeared or went: give it its state, or drop it.
        void MachineTileChanged(int x, int y, TileType type)
        {
            int key = y * Map.Width + x;
            var definition = Tiles?.Machine(type);
            if (definition == null) _machines.Remove(key);
            else _machines[key] = new Machine(definition, x, y, 0);
        }

        // Whatever the machine held goes to whoever dug it up.
        void Empty(Machine machine, Player player)
        {
            foreach (var pair in machine.Input.Concat(machine.Output)) player.Inventory.AddMaterial(pair.Key, pair.Value);
            if (machine.Carried != null) player.Inventory.AddMaterial(machine.Carried);
        }

        void UpdateMachines()
        {
            foreach (var machine in _machines.Values)
            {
                switch (machine.Kind)
                {
                    case TileType.Drill:
                        RunDrill(machine);
                        break;
                    case TileType.Smelter:
                        RunSmelter(machine);
                        break;
                    case TileType.Conveyor:
                        RunConveyor(machine);
                        break;
                    case TileType.Splitter:
                        RunSplitter(machine);
                        break;
                    case TileType.Arm:
                        RunArm(machine);
                        break;
                }
            }
        }

        // A splitter sends each load out of its front, left and right in turn, skipping a side that cannot take it and
        // the side it came in from.
        void RunSplitter(Machine splitter)
        {
            if (splitter.Carried == null) return;
            splitter.Progress = Math.Min(1, splitter.Progress + 1f / splitter.Definition.Ticks);
            if (splitter.Progress < 1) return;
            for (int k = 0; k < SplitterTurns.Length; k++)
            {
                int turn = (splitter.Timer + k) % SplitterTurns.Length;
                int direction = (splitter.Direction + SplitterTurns[turn]) & 3;
                if (direction == splitter.CameFrom) continue;
                var (dx, dy) = Machine.Steps[direction];
                if (!Push(splitter, splitter.Carried, splitter.X + dx, splitter.Y + dy)) continue;
                splitter.Carried = null;
                splitter.Progress = 0;
                splitter.CameFrom = -1;
                splitter.Timer = (turn + 1) % SplitterTurns.Length;
                return;
            }
        }

        // Front, left (a quarter turn anticlockwise) and right.
        static readonly int[] SplitterTurns = { 0, 1, 3 };

        // A robot arm picks one material up from the tile behind it, one that the tile ahead will take, swings it over
        // and puts it down there. It takes from a chest (the first material in sheet order that fits), a drill's or
        // smelter's output, a splitter, or the end of a conveyor that faces it.
        void RunArm(Machine arm)
        {
            var (fx, fy) = arm.Front;
            if (arm.Carried == null)
            {
                arm.Progress = 0;
                arm.Carried = TakeFor(arm, fx, fy);
                return;
            }
            arm.Progress = Math.Min(1, arm.Progress + 1f / arm.Definition.Ticks);
            if (arm.Progress < 1) return;
            if (!Push(arm, arm.Carried, fx, fy)) return;
            arm.Carried = null;
            arm.Progress = 0;
        }

        MaterialDefinition TakeFor(Machine arm, int fx, int fy)
        {
            var (bx, by) = arm.Back;
            bool Fits(MaterialDefinition material) => Takes(arm, material, fx, fy);
            var source = MachineAt(bx, by);
            if (source != null)
            {
                switch (source.Kind)
                {
                    case TileType.Drill:
                    case TileType.Smelter:
                        var made = source.Output.Keys.FirstOrDefault(Fits);
                        if (made != null) Add(source.Output, made, -1);
                        return made;
                    case TileType.Conveyor:
                    case TileType.Splitter:
                        var load = source.Carried;
                        bool ready = source.Kind == TileType.Splitter || (source.Progress >= 1 && source.Front == (arm.X, arm.Y));
                        if (load == null || !ready || !Fits(load)) return null;
                        source.Carried = null;
                        source.Progress = 0;
                        return load;
                    default:
                        return null;
                }
            }
            var chest = ChestAt(bx, by);
            var taken = chest == null || Loot == null ? null : Loot.Materials.FirstOrDefault(m => chest.CountOf(m) > 0 && Fits(m));
            if (taken == null) return null;
            chest.TakeMaterial(taken);
            int key = by * Map.Width + bx;
            _chestVersions[key] = _chestVersions.GetValueOrDefault(key) + 1;
            return taken;
        }

        // Pulls an ore from the vein ahead every so often, and sends one out of the back each tick it can.
        void RunDrill(Machine drill)
        {
            var (fx, fy) = drill.Front;
            var vein = Tiles.Find(Map[fx, fy]);
            if (vein != null && vein.Drillable && Count(drill.Output) < drill.Definition.Capacity && ++drill.Timer >= drill.Definition.Ticks)
            {
                drill.Timer = 0;
                Add(drill.Output, vein.Material, 1);
            }
            SendOut(drill, drill.Back);
        }

        // Melts whatever recipe its input allows every so often, and sends results out of the front.
        void RunSmelter(Machine smelter)
        {
            // The time only runs while there is something it can melt and room for the result.
            var recipe = Smelts.FirstOrDefault(r => r.Ingredients.All(i => smelter.Input.GetValueOrDefault(i.Material) >= i.Count)
                && Count(smelter.Output) + r.Count <= smelter.Definition.Capacity);
            if (recipe == null)
            {
                smelter.Timer = 0;
            }
            else if (++smelter.Timer >= smelter.Definition.Ticks)
            {
                smelter.Timer = 0;
                foreach (var (material, count) in recipe.Ingredients) Add(smelter.Input, material, -count);
                Add(smelter.Output, recipe.Material, recipe.Count);
            }
            SendOut(smelter, smelter.Front);
        }

        void RunConveyor(Machine belt)
        {
            if (belt.Carried == null) return;
            belt.Progress = Math.Min(1, belt.Progress + 1f / belt.Definition.Ticks);
            if (belt.Progress < 1) return;
            var (fx, fy) = belt.Front;
            if (Push(belt, belt.Carried, fx, fy)) belt.Carried = null;
        }

        void SendOut(Machine machine, (int X, int Y) to)
        {
            var first = machine.Output.Keys.FirstOrDefault();
            if (first != null && Push(machine, first, to.X, to.Y)) Add(machine.Output, first, -1);
        }

        // Whether the tile ever takes the material from the sender, busy or not: a conveyor not facing back at it, a
        // splitter, a chest, or a smelter that melts it. A robot arm only picks up what the tile ahead takes, so it
        // never sits holding something that can go nowhere.
        bool Takes(Machine from, MaterialDefinition material, int x, int y)
        {
            var target = MachineAt(x, y);
            if (target == null) return ChestAt(x, y) != null;
            return target.Kind switch
            {
                TileType.Conveyor => target.Front != (from.X, from.Y),
                TileType.Splitter => true,
                TileType.Smelter => Smelts.Any(r => r.Ingredients.Any(i => i.Material == material)),
                _ => false,
            };
        }

        // Hands one material to the tile: an empty conveyor not facing back at the sender, an empty splitter, a chest,
        // or a smelter that melts it and has room.
        bool Push(Machine from, MaterialDefinition material, int x, int y)
        {
            var target = MachineAt(x, y);
            if (target != null)
            {
                switch (target.Kind)
                {
                    case TileType.Conveyor:
                        if (target.Carried != null || target.Front == (from.X, from.Y)) return false;
                        target.Carried = material;
                        target.Progress = 0;
                        return true;
                    case TileType.Splitter:
                        if (target.Carried != null) return false;
                        target.Carried = material;
                        target.Progress = 0;
                        target.CameFrom = Array.FindIndex(Machine.Steps, s => (target.X + s.X, target.Y + s.Y) == (from.X, from.Y));
                        return true;
                    case TileType.Smelter:
                        if (!Smelts.Any(r => r.Ingredients.Any(i => i.Material == material)) || Count(target.Input) >= target.Definition.Capacity) return false;
                        Add(target.Input, material, 1);
                        return true;
                    default:
                        return false;
                }
            }
            var chest = ChestAt(x, y);
            if (chest == null) return false;
            chest.AddMaterial(material);
            int key = y * Map.Width + x;
            _chestVersions[key] = _chestVersions.GetValueOrDefault(key) + 1;
            return true;
        }

        static int Count(Dictionary<MaterialDefinition, int> stacks) => stacks.Values.Sum();

        static void Add(Dictionary<MaterialDefinition, int> stacks, MaterialDefinition material, int count)
        {
            int total = stacks.GetValueOrDefault(material) + count;
            if (total > 0) stacks[material] = total;
            else stacks.Remove(material);
        }

        // Puts back a machine as a world save held it.
        internal void RestoreMachine(int x, int y, int direction, int timer, MaterialDefinition carried, float progress,
            IEnumerable<(MaterialDefinition Material, int Count)> input, IEnumerable<(MaterialDefinition Material, int Count)> output)
        {
            var machine = MachineAt(x, y);
            if (machine == null) return;
            machine.Direction = direction & 3;
            machine.Timer = timer;
            machine.Carried = carried;
            machine.Progress = progress;
            foreach (var (material, count) in input) Add(machine.Input, material, count);
            foreach (var (material, count) in output) Add(machine.Output, material, count);
        }
    }
}
