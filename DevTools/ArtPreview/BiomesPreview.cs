using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core;

namespace Game.Client
{
    // The biomes' looks as the game draws them (묶음 B1a, Docs/BiomeRework.md 4-1·4-3, --only biomes, out/biomes/): for
    // each biome whose package is made, a sheet of its props in every drawing with its decor (and the swaying ones'
    // frames) and its critters, and a real room of a generated world of version 3 with its props, decor, critters and
    // someone walking it, unlit and lit (the biome's light colour, lamps and glowing caps). Drawn by BiomeArt, DecorArt,
    // CritterArt, StandArt and TileArt, the game's own code. False when a prop has no picture or one too wide for its
    // footprint.
    static partial class Program
    {
        static readonly (Biome Biome, string Id, string Name)[] MadeBiomes = { (Biome.Cave, "cave", "DIRT CAVE"), (Biome.Mushroom, "mushroom", "MUSHROOM FOREST") };

        static bool WriteBiomes(string output, WorldLights lights, TileData tiles, StoryData story, MonsterData monsters)
        {
            string folder = Directory.CreateDirectory(Path.Combine(output, "biomes")).FullName;
            var index = new StringBuilder("# 생물군계의 모습(묶음 B1a)\n\n`dotnet run --project DevTools/ArtPreview -- --only biomes`. 게임의 그림 코드(`BiomeArt`, `DecorArt`, `CritterArt`, `StandArt`, `TileArt`)로 그렸다.\n\n| 그림 | 보여 주는 것 |\n|---|---|\n");
            void Write(PixelImage image, string name, int scale, string what)
            {
                Png.Save(image.Times(scale), Path.Combine(folder, name));
                index.Append($"| `{name}` | {what} |\n");
            }
            bool ok = true;
            var world = WorldGenerator.Generate(1, WorldGenerator.GameSize, WorldGenerator.GameSize, story, WorldArenaSheet(monsters), WorldGenerator.PropVersion, tiles.Terrain);
            foreach (var (biome, id, name) in MadeBiomes)
            {
                ok &= PropSheet(biome, name, out var sheet);
                Write(sheet, $"props_{id}.png", 3, $"{BiomeMap.Name(biome)}의 소품: 칸 소품의 그림 셋씩(막는 것·깨는 것·큰 것), 꾸밈 층의 종류마다 그림 셋과 흔들림 두 프레임, 작은 생물 두 프레임, 입자");
                var room = BestRoom(world, biome);
                Write(BiomeRoom(world, room, biome, lights, false, 1), $"room_{id}_unlit.png", 2, $"{BiomeMap.Name(biome)}의 방 하나(시드 1, 생성기 3판, 방 {room.Index}): 소품·꾸밈·작은 생물, 어둠 없이");
                Write(BiomeRoom(world, room, biome, lights, true, 1), $"room_{id}.png", 2, "같은 방에 어둠과 빛(생물군계 빛 색, 등잔·빛 버섯의 빛, 사람이 든 빛)");
            }
            // The dirt cave's village with its timber lamps in place of the forest's trees, and a lamp lit beside one out.
            var village = world.Places.FirstOrDefault(p => p.Definition.Id == "dawn_village");
            if (village != null)
            {
                int half = village.Radius + 4;
                var scene = new Scene(world.Map, village.X - half, village.Y - half, half * 2, half * 2, _ => false) { Night = false, BiomeAt = (x, y) => world.Biomes[x, y] };
                scene.Places(world.Places);
                scene.Decor(1, (x, y) => world.Biomes[x, y]);
                scene.Player(village.X + 1.5f, village.Y - 1.5f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#3F7FD8"));
                Write(scene.Image, "place_dawn_village.png", 2, "여명 마을(흙 동굴): 파란 버섯 나무 자리에 갱목과 등잔, 건물의 날카로운 그림자");
            }
            var hut = world.Places.FirstOrDefault(p => p.Definition.Id == "hero_hut");
            if (hut != null)
            {
                int half = hut.Radius + 3;
                var scene = new Scene(world.Map, hut.X - half, hut.Y - half, half * 2, half * 2, _ => false) { Night = false, BiomeAt = (x, y) => world.Biomes[x, y] };
                scene.Places(world.Places);
                Write(scene.Image, "place_hero_hut.png", 3, "용사의 오두막(3판): 텃밭 울타리의 두 줄이 모서리 기둥에서 직각으로 만난다");
            }
            File.WriteAllText(Path.Combine(folder, "index.md"), index.ToString());
            Console.WriteLine($"Wrote the biomes' looks to {folder}");
            return ok;
        }

        // The room of the biome with the most props standing round its middle, off the places.
        static RoomNode BestRoom(GeneratedWorld world, Biome biome)
        {
            RoomNode best = null;
            int most = -1;
            foreach (var room in world.Rooms.Nodes)
            {
                if (room.Biome != biome || room.Kind != RoomKind.Room || room.Role == RoomRole.Gate) continue;
                if (world.Places.Any(p => Math.Abs(p.X - room.X) < p.Radius + 20 && Math.Abs(p.Y - room.Y) < p.Radius + 16)) continue;
                int kinds = 0, count = 0;
                var seen = new HashSet<TileType>();
                for (int y = room.Y - RoomHalfH; y < room.Y + RoomHalfH; y++)
                    for (int x = room.X - RoomHalfW; x < room.X + RoomHalfW; x++)
                    {
                        if (!world.Map.InBounds(x, y) || world.Biomes[x, y] != biome) continue;
                        var type = world.Map[x, y];
                        if (!BiomeProps.Is(type)) continue;
                        count++;
                        if (seen.Add(type)) kinds++;
                    }
                // Many kinds, and about as many props as a room usually has (not the most crowded one).
                int score = kinds * 20 - Math.Abs(count - 16);
                if (score > most)
                {
                    most = score;
                    best = room;
                }
            }
            return best;
        }

        const int RoomHalfW = 15, RoomHalfH = 10;

        static PixelImage BiomeRoom(GeneratedWorld world, RoomNode room, Biome biome, WorldLights lights, bool lit, ulong seed)
        {
            var map = world.Map;
            int left = room.X - RoomHalfW, bottom = room.Y - RoomHalfH;
            var scene = new Scene(map, left, bottom, RoomHalfW * 2, RoomHalfH * 2, _ => false) { Biome = biome, Night = true, BiomeAt = (x, y) => world.Biomes[x, y] };
            scene.Places(world.Places);
            scene.Decor(seed, (x, y) => world.Biomes[x, y]);
            // Someone in the middle, and someone behind the tallest thing in view (its cap fades).
            var at = (X: room.X, Y: room.Y);
            for (int reach = 0; reach < 8 && map[at.X, at.Y] != TileType.Floor; reach++)
                for (int dy = -reach; dy <= reach; dy++)
                    for (int dx = -reach; dx <= reach; dx++)
                        if (map[at.X, at.Y] != TileType.Floor && map[room.X + dx, room.Y + dy] == TileType.Floor) at = (room.X + dx, room.Y + dy);
            scene.Player(at.X + 0.5f, at.Y + 0.5f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#3F7FD8"));
            // The critters: a few on open floor by a hash of the tile, the flying ones a little above it.
            int critters = 0;
            for (int y = bottom + 1; y < bottom + RoomHalfH * 2 - 1 && critters < 8; y++)
            {
                for (int x = left + 1; x < left + RoomHalfW * 2 - 1 && critters < 8; x++)
                {
                    if (map[x, y] != TileType.Floor || PixelNoise.Hash(x, y, 4401) % 37 != 0) continue;
                    var kind = CritterArt.Pick(biome, (int)(PixelNoise.Hash(x, y, 4402) % 100), true);
                    var image = CritterArt.Image(kind, (int)(PixelNoise.Hash(x, y, 4403) % 2));
                    if (CritterArt.Glows(kind)) scene.Shot(x + 0.5f, y + 0.9f, image);
                    else scene.Icon(x + 0.5f, y + (CritterArt.Flies(kind) ? 0.9f : 0.4f), image);
                    critters++;
                }
            }
            if (lit) scene.Lit(lights, (x, y) => WorldLights.BiomeGlow(world.Biomes[x, y]), (x, y) => WorldLights.BiomeTint(world.Biomes[x, y]));
            int props = 0;
            for (int y = bottom; y < bottom + RoomHalfH * 2; y++)
                for (int x = left; x < left + RoomHalfW * 2; x++)
                    if (BiomeProps.Is(map[x, y])) props++;
            return Captioned(scene.Image, $"{biome.ToString().ToUpperInvariant()} ROOM {room.Index}: {props} PROPS, {scene.DecorShown} DECOR, {critters} CRITTERS{(lit ? ", LIT" : "")}");
        }

        // Every prop of the biome in its three drawings on the biome's floor, its decor (with the frames of what sways)
        // and its critters. False when a picture is missing or wider than its footprint allows.
        static bool PropSheet(Biome biome, string name, out PixelImage sheet)
        {
            bool ok = true;
            var kinds = BiomeProps.Of(biome).Select(k => k.Type).ToList();
            if (biome == Biome.Cave) kinds.Insert(kinds.IndexOf(TileType.UnlitLamp) + 1, TileType.LitLamp);
            var decor = Enumerable.Range(0, 100).Select(roll => Decor.KindOf(biome, roll)).Distinct().ToList();
            var floor = TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, 0, biome));
            const int cellW = 62, rowH = 62, leftPad = 6;
            int rows = (kinds.Count + 3) / 4;
            var g = new PixelImage(leftPad * 2 + cellW * 4 * 2, 16 + rows * rowH + 66 + 40);
            g.Fill(0, 0, g.Width, g.Height, DepthBack);
            g.Small(leftPad, 4, $"{name}: PROPS (TILES, 3 DRAWINGS EACH), THE PLACES' TREE, DECOR (REST, SWAY RIGHT, SWAY LEFT), CRITTERS, MOTES", DepthText, DepthBack);
            void Floor(int x0, int y0, int w, int h)
            {
                for (int y = 0; y < h; y += 16)
                    for (int x = 0; x < w; x += 16)
                        g.Draw(TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, (x / 16 + y / 16 * 3) % 4, biome)), x0 + x, y0 + y);
            }
            for (int i = 0; i < kinds.Count; i++)
            {
                var type = kinds[i];
                int x0 = leftPad + i % 4 * cellW * 2, y0 = 16 + i / 4 * rowH;
                Floor(x0, y0, cellW * 2 - 12, 48);
                g.Small(x0, y0 + 50, type.ToString().ToUpperInvariant(), DepthDim, DepthBack);
                int span = Structures.Footprint(type).W * 16;
                for (int v = 0; v < BiomeArt.Variants; v++)
                {
                    var image = BiomeArt.Prop(type, v);
                    if (image == null)
                    {
                        Console.Error.WriteLine($"biomes: {type} has no picture.");
                        ok = false;
                        continue;
                    }
                    // A prop may overhang its footprint a little (a cap, a cart's wheel), never by a tile.
                    int limit = type == TileType.GiantCap ? 56 : span + 6;
                    if (image.Width > limit)
                    {
                        Console.Error.WriteLine($"biomes: {type} drawing {v} is {image.Width} wide for a footprint of {span}.");
                        ok = false;
                    }
                    if (type == TileType.GiantCap)
                    {
                        // One drawing, taller than the row: it stands over the row above's empty end.
                        if (v == 0) g.Draw(image, x0 + 30, y0 + 46 - image.Height);
                        continue;
                    }
                    int step = Math.Max(span + 4, 20);
                    g.Draw(image, x0 + 2 + v * step + step / 2 - image.Width / 2, y0 + 46 - image.Height);
                }
            }
            int y1 = 16 + rows * rowH;
            // The places' tree as this biome draws it, then the decor.
            Floor(leftPad, y1, g.Width - leftPad * 2, 48);
            var tree = BiomeArt.PlaceTree(biome);
            g.Draw(tree, leftPad + 4, y1 + 46 - tree.Height);
            int dx = leftPad + 44;
            foreach (var kind in decor)
            {
                for (int v = 0; v < Decor.Variants; v++)
                {
                    g.Draw(DecorArt.Image(kind, biome, v), dx, y1 + 8);
                    if (Decor.Sways(kind))
                    {
                        g.Draw(DecorArt.Image(kind, biome, v, 1), dx, y1 + 20);
                        g.Draw(DecorArt.Image(kind, biome, v, 2), dx, y1 + 32);
                    }
                    dx += 14;
                }
                dx += 6;
            }
            g.Small(leftPad + 44, y1 + 50, string.Join("  ", decor.Select(k => k.ToString().ToUpperInvariant())), DepthDim, DepthBack);
            int y2 = y1 + 62;
            Floor(leftPad, y2, g.Width - leftPad * 2, 16);
            int cx = leftPad + 6;
            foreach (CritterKind critter in Enum.GetValues(typeof(CritterKind)))
            {
                bool cave = critter <= CritterKind.LampMoth;
                if (cave != (biome == Biome.Cave)) continue;
                for (int f = 0; f < CritterArt.Frames; f++)
                {
                    var image = CritterArt.Image(critter, f);
                    g.Draw(image, cx, y2 + 10 - image.Height);
                    cx += 12;
                }
                cx += 8;
            }
            cx += 12;
            foreach (MoteKind mote in Enum.GetValues(typeof(MoteKind)))
            {
                bool cave = mote <= MoteKind.Splash;
                if (cave != (biome == Biome.Cave)) continue;
                g.Draw(CritterArt.Mote(mote), cx, y2 + 6);
                cx += 10;
            }
            g.Small(leftPad, y2 + 20, "CRITTERS (2 FRAMES EACH) AND MOTES", DepthDim, DepthBack);
            sheet = g;
            return ok;
        }
    }
}
