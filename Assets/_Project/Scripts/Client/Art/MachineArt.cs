using System;
using Game.Core;

namespace Game.Client
{
    // Which picture a machine's moving part is, and how it sits on its body: read by WorldView.DrawMachines and by the
    // preview tool (ArtPreview's FlatScenes), so the previews show the machines as the game does.
    static class MachineArt
    {
        // What the machine at a tile is and which way it faces, or null for none (the belts look at their neighbours).
        public delegate (TileType Kind, int Facing)? Lookup(int x, int y);

        // The part's picture for a machine facing `facing` at (x, y), and whether the view turns it by the facing (a
        // quarter turn a step) or it is drawn the way it stands. Null picture: no part (a pole, a panel).
        public static (PixelImage Image, bool Turns) Part(TileType kind, int facing, Lookup at, int x, int y, int frame) => kind switch
        {
            TileType.Drill => (ItemArt.DrillBit, true),
            // Belts lie the way they run, curved where they are fed from a side (BeltArt): not turned by the view.
            TileType.Conveyor => (BeltArt.Belt(false, facing, BeltArt.Feed(at, x, y, facing), frame), false),
            TileType.FastConveyor => (BeltArt.Belt(true, facing, BeltArt.Feed(at, x, y, facing), frame), false),
            TileType.UndergroundBelt => (BeltArt.Underground(facing, frame), false),
            TileType.PowerPole or TileType.SolarPanel or TileType.TeslaTower => (null, false),
            TileType.WaterWheel => (ItemArt.WaterWheel, false),
            TileType.Turret => (ItemArt.TurretBarrel, false),
            TileType.Splitter or TileType.Filter => (ItemArt.SplitterArrows, true),
            TileType.Arm => (ItemArt.RobotArm, true),
            TileType.Sprinkler => (ItemArt.SprinklerHead, false),
            TileType.Harvester => (ItemArt.HarvesterReel, true),
            TileType.Generator => (null, false),
            TileType.Lever => (ItemArt.LeverHandle, false),
            TileType.BigAssembler => (PropArt.Raised(TileArt.BigAssembler, PropArt.BigLift), false),
            TileType.BigSmelter => (PropArt.Raised(TileArt.BigSmelter, PropArt.BigLift), false),
            _ => (ItemArt.Chevron, true),
        };

        // How far above its tile's middle a machine's part and load sit, in tiles: the top of a raised body.
        public static float Lift(TileType kind) => PropArt.Raises(kind) ? PropArt.LiftOf(kind) / TileArt.Size
            : World.SizeOf(kind) > 1 ? PropArt.BigLift / (float)TileArt.Size
            : kind == TileType.WaterWheel ? PropArt.WheelLift / (float)TileArt.Size : 0;

        // Whether the part is sorted by the machine's foot with what stands (on a raised or stood body), not laid on
        // the floor.
        public static bool Stands(TileType kind) => PropArt.Stands(kind) || World.SizeOf(kind) > 1;

        // How high over a power node's tile its wires hang, in tiles: a pole's arm, the top of anything else.
        public static float WireHeight(TileType kind) => kind == TileType.PowerPole ? PropArt.PoleWire : 0.85f;
    }
}
