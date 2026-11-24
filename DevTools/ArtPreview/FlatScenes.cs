using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // --only flat, the second half (2026-11-25, "입체감 없이 타일 형태로 칸 꽉 채우는거 다 찾아서 점검해"): the things the
    // object layer draws, as the GAME draws them (ground, object tiles matched to their neighbours, the standing layer by
    // foot, the machines' parts and loads), in the layouts they are really laid in: belts in a line with turns, floors
    // and carpets in patches, soil in plots, doors in walls and fences, stalls with their keepers, docks over water, the
    // rooms' walls in rock and a trap's grates. Drawn through the Scene the other previews use, by the game's own
    // helpers (StandArt, FlatArt, BeltArt, MachineArt), so the pictures are the game's.
    static partial class Program
    {
        // A small laid map and the machines on it (which way each faces), for a scene.
        sealed class FlatBoard
        {
            public readonly TileMap Map;
            public readonly Dictionary<(int X, int Y), int> Facing = new Dictionary<(int, int), int>();
            public readonly List<(int X, int Y)> OpenDoors = new List<(int, int)>();
            readonly int _rows;

            // Rows from the north; '#' rock, '~' water, anything else floor.
            public FlatBoard(string[] rows)
            {
                _rows = rows.Length;
                Map = new TileMap(rows[0].Length, rows.Length, TileType.Floor);
                for (int r = 0; r < rows.Length; r++)
                    for (int x = 0; x < rows[r].Length; x++)
                        Map[x, _rows - 1 - r] = rows[r][x] == '#' ? TileType.Wall : rows[r][x] == '~' ? TileType.Water : TileType.Floor;
            }

            // Column and row from the top left, as the layout is written.
            public void Put(int column, int row, TileType type, int facing = -1)
            {
                int y = _rows - 1 - row;
                Map[column, y] = type;
                if (facing >= 0) Facing[(column, y)] = facing;
                if (World.SizeOf(type) > 1)
                {
                    // A big machine's own tile is its lower left; the other three belong to it.
                    Map[column + 1, y] = Map[column, y + 1] = Map[column + 1, y + 1] = TileType.MachinePart;
                }
            }

            public void Fill(int column, int row, int w, int h, TileType type)
            {
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                        Put(column + i, row + j, type);
            }

            public void Run(int column, int row, int dx, int dy, int count, TileType type, int facing = -1)
            {
                for (int i = 0; i < count; i++) Put(column + dx * i, row + dy * i, type, facing);
            }

            public int Y(int row) => _rows - 1 - row;

            public (TileType Kind, int Facing)? MachineAt(int x, int y) =>
                Map.InBounds(x, y) && Facing.TryGetValue((x, y), out int facing) ? (Map[x, y], facing) : ((TileType, int)?)null;
        }

        // The scene of a board: what stands, the machines' parts as WorldView.DrawMachines draws them, the stalls'
        // keepers, counters and awnings as DrawPeople does.
        static Scene FlatScene(FlatBoard board, NpcData npcs)
        {
            var map = board.Map;
            var scene = new Scene(map, 0, 0, map.Width, map.Height, _ => false) { Night = false, Open = (x, y) => board.OpenDoors.Contains((x, y)) };
            scene.Places(null);
            foreach (var pair in board.Facing)
            {
                var (x, y) = pair.Key;
                var kind = map[x, y];
                int facing = pair.Value;
                var part = MachineArt.Part(kind, facing, board.MachineAt, x, y, 0);
                if (part.Image == null) continue;
                int size = World.SizeOf(kind);
                if (size > 1)
                {
                    // A big machine's body stands from its foot over its four tiles.
                    scene.Stand(x + size / 2f, y, part.Image, StandArt.PropTie);
                    continue;
                }
                var image = part.Turns ? Turn(part.Image, facing) : part.Image;
                float lift = MachineArt.Lift(kind);
                if (MachineArt.Stands(kind)) scene.Centered(x + 0.5f, y + 0.5f + lift, image, y, StandArt.PropTie);
                else scene.Flat(x + 0.5f, y + 0.5f, image);
            }
            for (int y = 0; y < map.Rows; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    var npc = npcs?.Npcs.FirstOrDefault(n => n.Tile == map[x, y]);
                    if (npc == null) continue;
                    var tile = map[x, y];
                    var figure = PeopleArt.Person(npc.Id, Rgba.Hex(npc.Color), 0) ?? HeroArt.Figure(HeroArt.Draw(Look.Down, HeroMove.Stand, 0, HeroGear.None, Rgba.Hex(npc.Color)));
                    scene.Stand(x + 0.5f, y + 0.3f, figure, StandArt.BodyTie);
                    scene.Centered(x + 0.5f, y + 0.5f, TileArt.MerchantCounter, y + 0.3f - 0.01f, StandArt.BodyTie);
                    scene.Centered(x + 0.5f, y + 1.5f, TileArt.Awning(tile), y + 0.3f - 0.02f, StandArt.BodyTie);
                }
            }
            return scene;
        }

        static void WriteFlatScenes(string output, NpcData npcs)
        {
            string folder = Directory.CreateDirectory(Path.Combine(output, "flat")).FullName;
            Png.Save(Captioned(Factory(npcs), "FACTORY: BELTS (A LINE, TURNS, FAST, UNDERGROUND), SPLITTER, FILTER, ARM, LEVER, POLES, PANEL, WHEEL, SPRINKLER, HARVESTER, BIG MACHINES").Times(3), Path.Combine(folder, "scene_factory.png"));
            Png.Save(Captioned(Home(npcs), "HOME: FLOORS AND CARPETS IN PATCHES, SKYLIGHT, DOORS IN ROCK, IN WALLS AND IN A FENCE (SHUT, OPEN, LOCKED), HUNG WALLS").Times(3), Path.Combine(folder, "scene_home.png"));
            Png.Save(Captioned(Farm(npcs), "FARM: SOIL PLOTS, SCARECROW, FOLD, COOP, CRAB POT, DOCK, STALLS, WAYPOINT").Times(3), Path.Combine(folder, "scene_farm.png"));
            Png.Save(Captioned(Rooms(npcs), "ROOMS: CRACKED, OBSIDIAN AND VOID WALLS IN ROCK, ORE, GRATES, TRAPDOORS, ICE, QUICKSAND, ARENA FLOORS").Times(3), Path.Combine(folder, "scene_rooms.png"));
            Console.WriteLine($"Wrote the flat tiles' scenes to {folder}");
        }

        static PixelImage Factory(NpcData npcs)
        {
            var board = new FlatBoard(new[]
            {
                "########################",
                "#......................#",
                "#......................#",
                "#......................#",
                "#......................#",
                "#......................#",
                "#......................#",
                "#......................#",
                "#..............~~~.....#",
                "#..............~~~.....#",
                "#......................#",
                "########################",
            });
            // A line east, a splitter in it, a turn south, a filter, a turn west, an underground stretch, and out north.
            board.Put(1, 2, TileType.Drill, 0);
            board.Run(2, 2, 1, 0, 3, TileType.Conveyor, 0);
            board.Put(5, 2, TileType.Splitter, 0);
            board.Run(6, 2, 1, 0, 2, TileType.Conveyor, 0);
            board.Put(5, 1, TileType.Conveyor, 1);
            board.Put(5, 3, TileType.Conveyor, 3);
            board.Put(8, 2, TileType.Conveyor, 3);
            board.Put(8, 3, TileType.Conveyor, 3);
            board.Put(8, 4, TileType.Filter, 3);
            board.Put(8, 5, TileType.Conveyor, 3);
            board.Put(8, 6, TileType.Conveyor, 2);
            board.Put(7, 6, TileType.Conveyor, 2);
            board.Put(6, 6, TileType.UndergroundBelt, 2);
            board.Put(3, 6, TileType.UndergroundBelt, 2);
            board.Put(2, 6, TileType.Conveyor, 1);
            board.Put(2, 5, TileType.Conveyor, 1);
            board.Put(2, 4, TileType.Smelter, 1);
            // A fast loop: east, south, west, north.
            board.Run(11, 1, 1, 0, 3, TileType.FastConveyor, 0);
            board.Put(14, 1, TileType.FastConveyor, 3);
            board.Run(14, 2, 0, 1, 2, TileType.FastConveyor, 3);
            board.Put(14, 4, TileType.FastConveyor, 2);
            board.Run(12, 4, 1, 0, 2, TileType.FastConveyor, 2);
            board.Put(11, 4, TileType.FastConveyor, 1);
            board.Run(11, 2, 0, 1, 2, TileType.FastConveyor, 1);
            // An arm between a belt and a chest; a lever; power.
            board.Put(9, 3, TileType.Arm, 0);
            board.Put(10, 3, TileType.Chest);
            board.Put(17, 1, TileType.Lever, 0);
            board.Put(18, 1, TileType.Generator, 0);
            board.Put(20, 1, TileType.PowerPole, 0);
            board.Put(20, 5, TileType.PowerPole, 0);
            board.Put(18, 4, TileType.SolarPanel, 0);
            board.Put(19, 4, TileType.SolarPanel, 0);
            board.Put(17, 6, TileType.TeslaTower, 0);
            board.Put(18, 9, TileType.WaterWheel, 0);
            board.Put(14, 8, TileType.WaterWheel, 0);
            // The farm machines by a plot.
            board.Fill(2, 8, 3, 2, TileType.Soil);
            board.Put(5, 8, TileType.Sprinkler, 0);
            board.Put(1, 9, TileType.Harvester, 0);
            board.Put(6, 9, TileType.Harvester, 2);
            // The big machines.
            board.Put(8, 9, TileType.BigAssembler, 0);
            board.Put(11, 9, TileType.BigSmelter, 0);
            var scene = FlatScene(board, npcs);
            scene.Player(4.5f, board.Y(2) + 1.3f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#3F7FD8"));
            scene.Player(9.5f, board.Y(3) - 0.6f, HeroStanding(Look.Up, HeroMove.Stand, 0, "#D8703F"));
            scene.Player(19.5f, board.Y(4) + 1.2f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#5FBF5A"));
            scene.Player(16.5f, board.Y(2) + 0.6f, HeroStanding(Look.Side, HeroMove.Stand, 0, "#C85AD8"));
            return scene.Image;
        }

        static PixelImage Home(NpcData npcs)
        {
            var board = new FlatBoard(new[]
            {
                "##########################",
                "#........................#",
                "#........................#",
                "#........................#",
                "#........................#",
                "####.#######.####.###..###",
                "#........................#",
                "#........................#",
                "#................#.......#",
                "#........................#",
                "#................#.......#",
                "#........................#",
                "#........................#",
                "##########################",
            });
            // Floors in patches beside bare floor, a carpet on one, a carpet on bare floor, a skylight.
            board.Fill(1, 1, 5, 3, TileType.StoneFloor);
            board.Fill(2, 2, 3, 2, TileType.RedCarpet);
            board.Fill(7, 1, 4, 3, TileType.BrickFloor);
            board.Put(11, 2, TileType.BrickFloor);
            board.Fill(13, 1, 4, 3, TileType.GlowFloor);
            board.Put(14, 4, TileType.GlowFloor);
            board.Fill(19, 1, 2, 2, TileType.Skylight);
            board.Fill(22, 1, 3, 2, TileType.BlueCarpet);
            board.Put(23, 3, TileType.YellowCarpet);
            board.Run(20, 3, 1, 0, 3, TileType.YellowCarpet);
            // Doors in the rock wall between the rooms: shut, open, locked; and one in a north-south wall.
            board.Put(4, 5, TileType.Door);
            board.Put(12, 5, TileType.Door);
            board.OpenDoors.Add((12, board.Y(5)));
            board.Put(17, 5, TileType.LockedDoor);
            board.Put(17, 9, TileType.Door);
            board.Put(17, 11, TileType.Door);
            board.OpenDoors.Add((17, board.Y(11)));
            board.Put(17, 12, TileType.Wall);
            board.Put(17, 6, TileType.Wall);
            board.Put(17, 7, TileType.Wall);
            // A house of built walls: hung walls in a run, iron walls, a door in them, a door alone.
            board.Run(2, 7, 1, 0, 2, TileType.IronWall);
            board.Put(4, 7, TileType.TapestryWall);
            board.Put(5, 7, TileType.ShieldWall);
            board.Put(6, 7, TileType.TapestryWall);
            board.Run(7, 7, 1, 0, 2, TileType.IronWall);
            board.Run(2, 8, 0, 1, 3, TileType.IronWall);
            board.Run(8, 8, 0, 1, 3, TileType.IronWall);
            board.Run(2, 11, 1, 0, 3, TileType.IronWall);
            board.Put(5, 11, TileType.Door);
            board.Run(6, 11, 1, 0, 3, TileType.IronWall);
            board.Put(8, 9, TileType.Door);
            board.Fill(3, 8, 5, 3, TileType.StoneFloor);
            board.Put(5, 9, TileType.Table);
            board.Put(11, 9, TileType.Door);
            board.Put(13, 7, TileType.ShieldWall);
            board.Put(14, 9, TileType.TapestryWall);
            // A fenced pen with its gates, for comparison.
            board.Run(19, 7, 1, 0, 5, TileType.Fence);
            board.Run(19, 8, 0, 1, 3, TileType.Fence);
            board.Run(23, 8, 0, 1, 3, TileType.Fence);
            board.Run(19, 11, 1, 0, 5, TileType.Fence);
            board.Put(21, 11, TileType.Door);
            board.Put(23, 9, TileType.LockedDoor);
            var scene = FlatScene(board, npcs);
            scene.Player(3.5f, board.Y(3) + 0.5f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#3F7FD8"));
            scene.Player(4.5f, board.Y(5) + 1.2f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#D8703F"));
            scene.Player(12.5f, board.Y(5) + 0.5f, HeroStanding(Look.Down, HeroMove.Walk, 1, "#5FBF5A"));
            scene.Player(17.6f, board.Y(11) + 0.5f, HeroStanding(Look.Side, HeroMove.Walk, 1, "#C85AD8"));
            scene.Player(5.5f, board.Y(7) + 1.3f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#D8C83F"));
            scene.Player(6.5f, board.Y(8) + 0.4f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#3FB8D8"));
            scene.Player(15.5f, board.Y(2) + 0.4f, HeroStanding(Look.Side, HeroMove.Stand, 0, "#D83F6A"));
            return scene.Image;
        }

        static PixelImage Farm(NpcData npcs)
        {
            var board = new FlatBoard(new[]
            {
                "##########################",
                "#........................#",
                "#........................#",
                "#........................#",
                "#........................#",
                "#........................#",
                "#........................#",
                "#........................#",
                "#..........~~~~~~~~~~....#",
                "#..........~~~~~~~~~~....#",
                "#..........~~~~~~~~~~....#",
                "#..........~~~~~~~~~~....#",
                "##########################",
            });
            board.Fill(1, 1, 3, 2, TileType.Soil);
            board.Put(4, 1, TileType.FertileSoil);
            board.Put(4, 2, TileType.FertileSoil);
            board.Fill(6, 1, 3, 2, TileType.MushroomSoil);
            board.Fill(10, 1, 3, 2, TileType.FrostSoil);
            board.Fill(14, 1, 3, 2, TileType.AshSoil);
            board.Fill(18, 1, 3, 2, TileType.VoidSoil);
            board.Put(22, 1, TileType.Soil);
            board.Put(23, 2, TileType.Soil);
            board.Run(1, 4, 1, 0, 6, TileType.Soil);
            board.Put(3, 5, TileType.Soil);
            board.Put(7, 4, TileType.Scarecrow);
            board.Put(9, 4, TileType.Fold);
            board.Put(11, 4, TileType.Coop);
            board.Put(13, 4, TileType.Barn);
            board.Put(15, 4, TileType.Keg);
            // A dock out over the water, with a branch, and a crab pot at the shore.
            board.Run(13, 8, 0, 1, 3, TileType.Dock);
            board.Run(14, 8, 0, 1, 3, TileType.Dock);
            board.Run(15, 10, 1, 0, 3, TileType.Dock);
            board.Put(19, 8, TileType.Dock);
            board.Put(10, 7, TileType.CrabPot);
            board.Put(21, 9, TileType.CrabPot);
            // Stalls with their keepers, and a waypoint.
            board.Put(2, 8, TileType.MerchantStall);
            board.Put(4, 8, TileType.SmithStall);
            board.Put(6, 8, TileType.FisherHut);
            board.Put(8, 8, TileType.AlchemyTable);
            board.Put(5, 10, TileType.Waypoint);
            board.Run(18, 4, 1, 0, 5, TileType.Road);
            board.Put(23, 4, TileType.Waypoint);
            var scene = FlatScene(board, npcs);
            scene.Crop(2, board.Y(1), 3);
            scene.Crop(3, board.Y(2), 1);
            scene.Player(7.5f, board.Y(4) + 1.3f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#3F7FD8"));
            scene.Player(13.9f, board.Y(9) + 0.5f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#D8703F"));
            scene.Player(4.5f, board.Y(8) - 0.4f, HeroStanding(Look.Up, HeroMove.Stand, 0, "#5FBF5A"));
            scene.Player(9.5f, board.Y(4) + 1.2f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#C85AD8"));
            return scene.Image;
        }

        static PixelImage Rooms(NpcData npcs)
        {
            var board = new FlatBoard(new[]
            {
                "##########################",
                "#......#####.............#",
                "#......#####.............#",
                "#......#####.............#",
                "#........................#",
                "####..######.............#",
                "####..######.............#",
                "#........................#",
                "#........................#",
                "#........................#",
                "#........................#",
                "##########################",
            });
            // The rooms' walls set in rock, as the generator lays them; ore beside them.
            board.Put(7, 3, TileType.CrackedWall);
            board.Put(8, 3, TileType.CrackedWall);
            board.Put(9, 3, TileType.ObsidianWall);
            board.Put(10, 3, TileType.ObsidianWall);
            board.Put(11, 3, TileType.VoidWall);
            board.Put(11, 2, TileType.VoidWall);
            board.Put(11, 1, TileType.CopperOre);
            board.Put(7, 0, TileType.IronOre);
            board.Put(0, 3, TileType.CrackedWall);
            board.Put(2, 5, TileType.ObsidianWall);
            board.Put(3, 5, TileType.ObsidianWall);
            board.Put(6, 5, TileType.VoidWall);
            board.Put(6, 6, TileType.VoidWall);
            board.Put(3, 6, TileType.CrackedWall);
            // A wall of each standing alone on the floor (a room's pillar).
            board.Put(2, 2, TileType.ObsidianWall);
            board.Put(4, 2, TileType.VoidWall);
            board.Put(3, 9, TileType.CrackedWall);
            // A trap's grates across a way (east-west and north-south), trapdoors.
            board.Run(4, 5, 1, 0, 2, TileType.Grate);
            board.Run(12, 6, 0, 1, 3, TileType.Grate);
            board.Run(1, 8, 1, 0, 1, TileType.Grate);
            board.Put(8, 8, TileType.Trapdoor);
            board.Put(10, 8, TileType.OpenTrapdoor);
            // Ice and quicksand; the arenas' floors.
            board.Fill(14, 1, 3, 3, TileType.IceFloor);
            board.Put(17, 2, TileType.IceFloor);
            board.Fill(19, 1, 3, 2, TileType.Quicksand);
            board.Put(20, 3, TileType.Quicksand);
            board.Fill(14, 6, 3, 3, TileType.ArenaInlay);
            board.Fill(18, 6, 3, 2, TileType.ArenaCarpet);
            board.Put(22, 6, TileType.RuneFloor);
            board.Put(23, 6, TileType.Cobweb);
            board.Put(22, 8, TileType.Petals);
            board.Put(23, 8, TileType.Fissure);
            board.Run(18, 9, 1, 0, 4, TileType.Rail);
            var scene = FlatScene(board, npcs);
            scene.Player(4.6f, board.Y(5) + 1.2f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#3F7FD8"));
            scene.Player(12.5f, board.Y(7) + 0.5f, HeroStanding(Look.Side, HeroMove.Stand, 0, "#D8703F"));
            scene.Player(9.3f, board.Y(8) + 0.5f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#5FBF5A"));
            scene.Player(3.5f, board.Y(2) + 0.2f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#C85AD8"));
            return scene.Image;
        }
    }
}
