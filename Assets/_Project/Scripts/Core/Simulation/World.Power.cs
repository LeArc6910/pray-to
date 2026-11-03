using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // Generators and power poles joined within World.WireReach of each other, and the machines they reach. Running
    // generators give their power to the whole grid; machines in it share it (Docs/Automation.md, 전력망). A generator
    // burns fuel; a water wheel turns beside water; a solar panel works by day.
    public sealed class PowerGrid
    {
        internal List<Machine> Nodes { get; } = new List<Machine>();
        internal List<Machine> Users { get; } = new List<Machine>();
        // What the grid's burning generators give and its switched-on machines draw, this tick.
        public int Supply { get; internal set; }
        public int Demand { get; internal set; }
    }

    public sealed partial class World
    {
        // Poles and generators this close join one grid; a pole powers the machines this far from it (a generator,
        // World.PowerReach).
        public const float WireReach = 8;
        public const float PoleReach = 4;

        // Rebuilt when a machine is placed or dug up; supply and demand are counted every tick.
        List<PowerGrid> _grids = new List<PowerGrid>();
        readonly Dictionary<Machine, PowerGrid> _gridOf = new Dictionary<Machine, PowerGrid>();
        bool _gridsStale = true;

        public static bool IsGenerator(TileType type) => type == TileType.Generator || type == TileType.WaterWheel || type == TileType.SolarPanel;

        public static bool IsPowerNode(TileType type) => IsGenerator(type) || type == TileType.PowerPole;

        // How far a node gives power.
        static float NodeReach(Machine node) => IsGenerator(node.Kind) ? PowerReach : PoleReach;

        // Whether a generator gives its power now: switched on, and burning fuel, beside water, or in daylight.
        public bool Generating(Machine node) => node.Enabled && node.Kind switch
        {
            TileType.Generator => node.Timer > 0,
            TileType.WaterWheel => BesideWater(node.X, node.Y),
            // A storm hides the sun.
            TileType.SolarPanel => !Calendar.IsNight(Clock) && WeatherToday != Weather.Storm,
            _ => false,
        };

        // What a generator gives now: its power while it runs, half again for a water wheel in a storm's swollen water.
        public int PowerOf(Machine node) =>
            !Generating(node) ? 0 : node.Kind == TileType.WaterWheel && WeatherToday == Weather.Storm ? node.Definition.Power * 3 / 2 : node.Definition.Power;

        // The grid a node belongs to, or the one a machine draws from, or null.
        public PowerGrid GridOf(Machine machine)
        {
            if (_gridsStale) BuildGrids();
            return _gridOf.TryGetValue(machine, out var grid) ? grid : null;
        }

        // Whether two nodes are wired together (the view draws the wires).
        public static bool Wired(int ax, int ay, int bx, int by) => Vec2.Distance(TileMap.Center(ax, ay), TileMap.Center(bx, by)) <= WireReach;

        void BuildGrids()
        {
            _gridsStale = false;
            _grids = new List<PowerGrid>();
            _gridOf.Clear();
            var nodes = _machines.Values.Where(m => IsPowerNode(m.Kind)).ToList();
            foreach (var start in nodes)
            {
                if (_gridOf.ContainsKey(start)) continue;
                var grid = new PowerGrid();
                _grids.Add(grid);
                var open = new Queue<Machine>();
                open.Enqueue(start);
                _gridOf[start] = grid;
                while (open.Count > 0)
                {
                    var node = open.Dequeue();
                    grid.Nodes.Add(node);
                    foreach (var other in nodes)
                    {
                        if (_gridOf.ContainsKey(other) || Distance(node, other) > WireReach) continue;
                        _gridOf[other] = grid;
                        open.Enqueue(other);
                    }
                }
            }
            // A machine within reach of two grids draws from the one whose first node comes first in tile order.
            foreach (var machine in _machines.Values)
            {
                if (IsPowerNode(machine.Kind) || machine.Definition.Power <= 0) continue;
                var grid = _grids.FirstOrDefault(g => g.Nodes.Any(n => Distance(n, machine) <= NodeReach(n)));
                if (grid == null) continue;
                grid.Users.Add(machine);
                _gridOf[machine] = grid;
            }
        }

        // What every grid gives and draws this tick.
        void CountPower()
        {
            if (_gridsStale) BuildGrids();
            foreach (var grid in _grids)
            {
                grid.Supply = grid.Nodes.Sum(PowerOf);
                grid.Demand = grid.Users.Where(m => m.Enabled).Sum(m => m.Definition.Power);
            }
        }

        // Whether the machine gets a second step this tick. With power enough for the whole grid, every tick; with
        // less, a share of the ticks in proportion (supply / demand), the same for every machine in the grid.
        bool PoweredStep(Machine machine)
        {
            if (!_gridOf.TryGetValue(machine, out var grid) || IsPowerNode(machine.Kind) || grid.Supply <= 0) return false;
            if (grid.Supply >= grid.Demand) return true;
            machine.Charge += grid.Supply;
            if (machine.Charge < grid.Demand) return false;
            machine.Charge -= grid.Demand;
            return true;
        }

        // Whether the machine draws power from a grid with a running generator, for the view; for a generator, whether
        // it runs.
        public bool IsPowered(Machine machine)
        {
            if (IsGenerator(machine.Kind)) return Generating(machine);
            var grid = IsPowerNode(machine.Kind) ? null : GridOf(machine);
            if (grid == null) return false;
            return grid.Nodes.Any(Generating);
        }
    }
}
