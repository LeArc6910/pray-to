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
        static readonly (Biome Biome, string Id, string Name)[] MadeBiomes =
        {
            (Biome.Cave, "cave", "DIRT CAVE"), (Biome.Mushroom, "mushroom", "MUSHROOM FOREST"), (Biome.Ice, "ice", "ICE CAVE"), (Biome.Sand, "sand", "SAND CAVE"),
            (Biome.Crystal, "crystal", "CRYSTAL CAVE"), (Biome.Lake, "lake", "UNDERGROUND LAKE"),
            (Biome.Lava, "lava", "LAVA ZONE"), (Biome.Abyss, "abyss", "VOID ABYSS"),
        };

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
            var world = WorldGenerator.Generate(1, WorldGenerator.GameSize, WorldGenerator.GameSize, story, WorldArenaSheet(monsters), WorldGenerator.NewWorldVersion, tiles.Terrain);
            foreach (var (biome, id, name) in MadeBiomes)
            {
                ok &= PropSheet(biome, name, out var sheet);
                Write(sheet, $"props_{id}.png", 3, $"{BiomeMap.Name(biome)}의 소품: 칸 소품의 그림 셋씩(막는 것·깨는 것·큰 것), 꾸밈 층의 종류마다 그림 셋과 흔들림 두 프레임, 작은 생물 두 프레임, 입자");
                var room = BestRoom(world, biome);
                Write(BiomeRoom(world, room, biome, lights, false, 1), $"room_{id}_unlit.png", 2, $"{BiomeMap.Name(biome)}의 방 하나(시드 1, 생성기 4판, 방 {room.Index}): 소품·꾸밈·작은 생물, 어둠 없이");
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
            // 묶음 B1c: the places' buildings and dressing in each biome's look, and the landmarks.
            Write(PlaceLooks((0, Biome.Cave, "OLD"), (PropArt.CaveLook, Biome.Cave, "DIRT CAVE"), (PropArt.ForestLook, Biome.Mushroom, "MUSHROOM FOREST")),
                "place_props.png", 3, "장소의 건물·꾸밈을 생물군계마다: 줄마다 예전 그림 / 흙 동굴(광산 야영지) / 버섯 숲. 집은 밤(창에 불)과 낮");
            // 묶음 B2a: the ice cave's and the sand cave's.
            Write(PlaceLooks((0, Biome.Cave, "OLD"), (PropArt.IceLook, Biome.Ice, "ICE CAVE"), (PropArt.SandLook, Biome.Sand, "SAND CAVE")),
                "place_props_ice_sand.png", 3, "장소의 건물·꾸밈: 예전 그림 / 얼음 동굴(눈 덮인 지붕, 얼음 벽돌, 서리 등) / 모래 동굴(흙벽돌 집, 차양, 항아리, 화로). 집은 밤과 낮");
            // 묶음 B3a: the crystal cave's and the lake's.
            Write(PlaceLooks((0, Biome.Cave, "OLD"), (PropArt.CrystalLook, Biome.Crystal, "CRYSTAL CAVE"), (PropArt.LakeLook, Biome.Lake, "UNDERGROUND LAKE")),
                "place_props_crystal_lake.png", 3, "장소의 건물·꾸밈: 예전 그림 / 수정 동굴(점판암 벽과 지붕, 지붕을 뚫고 자란 결정, 결정 등) / 지하 호수(기둥 위 갈대 지붕 오두막, 그물, 청록 등). 집은 밤과 낮");
            // 묶음 B4a: the lava zone's and the abyss's.
            Write(PlaceLooks((0, Biome.Cave, "OLD"), (PropArt.LavaLook, Biome.Lava, "LAVA ZONE"), (PropArt.AbyssLook, Biome.Abyss, "VOID ABYSS")),
                "place_props_lava_abyss.png", 3, "장소의 건물·꾸밈: 예전 그림 / 용암 지대(철판 지붕과 현무암 벽의 대장간 집, 불 붙은 굴뚝, 화로) / 공허의 심연(부서져 떠 있는 지붕의 폐허 집, 룬, 오벨리스크 등). 집은 밤과 낮");
            foreach (var site in world.Places.Where(p => p.Definition.Id == "dawn_village" || p.Definition.Id == "hero_hut"))
            {
                foreach (var (biome, id, _) in MadeBiomes)
                {
                    foreach (bool lit in new[] { false, true })
                    {
                        int half = site.Radius + 3;
                        var scene = new Scene(world.Map, site.X - half, site.Y - half, half * 2, half * 2, _ => false) { Biome = biome, Night = lit, BiomeAt = (x, y) => biome };
                        scene.Places(world.Places);
                        scene.Decor(1, (x, y) => biome);
                        scene.Player(site.X + 1.5f, site.Y - 1.5f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#3F7FD8"));
                        scene.Player(site.X - 5f, site.Y + 5.4f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#D8503F"));
                        if (lit) scene.Lit(lights, (x, y) => WorldLights.BiomeGlow(biome), (x, y) => WorldLights.BiomeTint(biome));
                        Write(scene.Image, $"place_{site.Definition.Id}_{id}{(lit ? "_lit" : "")}.png", 2,
                            $"{site.Definition.Name}이 {BiomeMap.Name(biome)}에 섰을 때{(lit ? ", 밤(어둠과 빛)" : ", 어둠 없이")}. 지붕 뒤에 선 사람은 지붕이 흐려진다");
                    }
                }
            }
            foreach (bool lit in new[] { false, true })
                Write(LandmarkSheet(lights, lit), lit ? "landmarks_lit.png" : "landmarks.png", 3, "랜드마크 셋(옛 광부 오두막과 울타리 마당, 갱도 입구 틀과 끊긴 선로, 요정 고리 큰 것·작은 것)" + (lit ? ", 어둠과 빛" : ", 어둠 없이"));
            foreach (bool lit in new[] { false, true })
                Write(IceSandLandmarkSheet(lights, lit), lit ? "landmarks_ice_sand_lit.png" : "landmarks_ice_sand.png", 3,
                    "얼음·모래의 랜드마크 넷(얼음 속 짐승, 얼음 성 잔해 기둥 / 묻힌 신전 정면, 거대 화석 두개골과 갈비뼈)" + (lit ? ", 어둠과 빛" : ", 어둠 없이"));
            foreach (bool lit in new[] { false, true })
                Write(CrystalLakeLandmarkSheet(lights, lit), lit ? "landmarks_crystal_lake_lit.png" : "landmarks_crystal_lake.png", 3,
                    "수정·호수의 랜드마크 넷(거대 정동, 수정 아치 / 가라앉은 배, 옛 물레방아)과 버섯 섬" + (lit ? ", 어둠과 빛" : ", 어둠 없이"));
            foreach (bool lit in new[] { false, true })
                Write(LavaVoidLandmarkSheet(lights, lit), lit ? "landmarks_lava_abyss_lit.png" : "landmarks_lava_abyss.png", 3,
                    "용암·심연의 랜드마크 넷(쓰러진 대장간 굴뚝, 거대 모루 / 깨진 문, 별 제단)과 용암 속 바위" + (lit ? ", 어둠과 빛" : ", 어둠 없이"));
            foreach (var kind in new[] { BiomeLandmarkKind.MinerHut, BiomeLandmarkKind.MineFrame, BiomeLandmarkKind.FairyRing, BiomeLandmarkKind.FrozenBeast, BiomeLandmarkKind.IceRuin,
                BiomeLandmarkKind.TempleFront, BiomeLandmarkKind.GiantSkull, BiomeLandmarkKind.GiantGeode, BiomeLandmarkKind.CrystalArch, BiomeLandmarkKind.SunkenBoat, BiomeLandmarkKind.OldWheel,
                BiomeLandmarkKind.FallenChimney, BiomeLandmarkKind.GiantAnvil, BiomeLandmarkKind.BrokenGate, BiomeLandmarkKind.StarAltar })
            {
                if (!world.BiomeLandmarks.Any(l => l.Kind == kind))
                {
                    Console.WriteLine($"biomes: seed 1 has no {kind}.");
                    continue;
                }
                var mark = world.BiomeLandmarks.First(l => l.Kind == kind);
                var biome = world.Biomes[mark.StandX, mark.StandY];
                foreach (bool lit in new[] { false, true })
                {
                    var scene = new Scene(world.Map, mark.Left + mark.Width / 2 - 11, mark.Bottom + mark.Height / 2 - 8, 22, 16, _ => false)
                        { Biome = biome, Night = true, BiomeAt = (x, y) => world.Biomes[x, y] };
                    scene.Places(world.Places);
                    scene.Decor(1, (x, y) => world.Biomes[x, y]);
                    scene.Player(mark.StandX + 0.5f, mark.StandY + 0.5f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#3F7FD8"));
                    if (lit) scene.Lit(lights, (x, y) => WorldLights.BiomeGlow(world.Biomes[x, y]), (x, y) => WorldLights.BiomeTint(world.Biomes[x, y], 0, x, y));
                    Write(scene.Image, $"landmark_{kind.ToString().ToLowerInvariant()}{(lit ? "_lit" : "")}.png", 3,
                        $"생성기 {world.Version}판 시드 1의 실제 {kind}({mark.Left},{mark.Bottom}){(lit ? ", 어둠과 빛" : ", 어둠 없이")}");
                }
            }
            foreach (var type in new[] { TileType.MinerHut, TileType.MineFrame, TileType.FrozenBeast, TileType.IceColumn, TileType.TempleFront, TileType.GiantSkull,
                TileType.GiantGeode, TileType.ArchPier, TileType.CrystalArch, TileType.SunkenBoat, TileType.OldWheel,
                TileType.FallenChimney, TileType.GiantAnvil, TileType.BrokenGate, TileType.StarAltar })
            {
                var picture = StandArt.Picture(type, StandArt.Kind.Prop, 0, false);
                // The arch's span is five tiles wide over the one tile it stands on, pier to pier.
                int span = type == TileType.CrystalArch ? 80 : Structures.Footprint(type).W * 16;
                if (picture == null || picture.Width > span + (type == TileType.IceColumn ? 6 : 2))
                {
                    Console.Error.WriteLine($"biomes: the landmark {type} has no picture or one wider than its footprint.");
                    ok = false;
                }
            }
            foreach (var type in PlaceLookTypes)
            {
                var old = PropArt.Prop(type);
                foreach (int look in new[] { PropArt.CaveLook, PropArt.ForestLook, PropArt.IceLook, PropArt.SandLook, PropArt.CrystalLook, PropArt.LakeLook, PropArt.LavaLook, PropArt.AbyssLook })
                {
                    var redrawn = PropArt.PlaceProp(type, look);
                    if (redrawn != null && redrawn.Width == old.Width && redrawn.Height == old.Height) continue;
                    Console.Error.WriteLine($"biomes: {type} in look {look} is not the size of the old drawing.");
                    ok = false;
                }
            }
            File.WriteAllText(Path.Combine(folder, "index.md"), index.ToString());
            Console.WriteLine($"Wrote the biomes' looks to {folder}");
            return ok;
        }

        static readonly TileType[] PlaceLookTypes =
        {
            TileType.Cottage, TileType.Cabin, TileType.Well, TileType.LampPost, TileType.Signpost, TileType.Bench, TileType.Flowerbed, TileType.Crates,
        };

        // Each of the places' things that is redrawn by biome: the old drawing, the dirt cave's and the mushroom forest's
        // side by side on their floors, the houses by night and by day.
        static PixelImage PlaceLooks(params (int Look, Biome Biome, string Name)[] looks)
        {
            const int colW = 150, pad = 6;
            var g = new PixelImage(pad * 2 + colW * 3, 16 + 92 * 2 + 62);
            g.Fill(0, 0, g.Width, g.Height, DepthBack);
            for (int c = 0; c < looks.Length; c++)
            {
                var (look, biome, name) = looks[c];
                int x0 = pad + c * colW;
                g.Small(x0, 4, name, DepthText, DepthBack);
                void Floor(int y0, int h)
                {
                    for (int y = 0; y < h; y += 16)
                        for (int x = 0; x < colW - 8; x += 16)
                            g.Draw(TileArt.Ground(TileArt.Index(GroundKind.Floor, 0, (x / 16 + y / 16 * 3) % 4, biome)), x0 + x, y0 + y);
                }
                PixelImage Of(TileType type, bool lit) => StandArt.Picture(type, StandArt.Kind.Prop, look, lit);
                int row = 0;
                foreach (var house in new[] { TileType.Cottage, TileType.Cabin })
                {
                    int y0 = 16 + row++ * 92;
                    Floor(y0, 80);
                    g.Draw(Of(house, true), x0 + 2, y0 + 78 - 74);
                    g.Draw(Of(house, false), x0 + 72, y0 + 78 - 74);
                }
                int y1 = 16 + 92 * 2;
                Floor(y1, 48);
                int dx = x0 + 2;
                foreach (var type in PlaceLookTypes.Skip(2))
                {
                    var image = Of(type, true);
                    g.Draw(image, dx, y1 + 46 - image.Height);
                    dx += image.Width + 6;
                }
                // A person for scale.
                var hero = HeroStanding(Look.Down, HeroMove.Stand, 0, "#3F7FD8");
                g.Draw(hero, dx, y1 + 46 - hero.Height);
            }
            return g;
        }

        // The three landmarks laid as the generator lays them, on a floor of their biome: the dirt cave's two side by
        // side, then the mushroom forest's rings, someone standing in each.
        static PixelImage LandmarkSheet(WorldLights lights, bool lit)
        {
            PixelImage Half(Biome biome, params ((int X, int Y, TileType Type)[] Things, int Left, int Bottom, int StandX, int StandY)[] sets)
            {
                var map = new TileMap(24, 13);
                for (int y = 1; y < 10; y++)
                    for (int x = 1; x < 23; x++)
                        map[x, y] = TileType.Floor;
                foreach (var (things, left, bottom, _, _) in sets)
                    foreach (var (dx, dy, type) in things)
                    {
                        var (w, h) = Structures.Footprint(type);
                        for (int j = 0; j < h; j++)
                            for (int i = 0; i < w; i++)
                                map[left + dx + i, bottom + dy + j] = i == 0 && j == 0 ? type : TileType.StructurePart;
                    }
                var scene = new Scene(map, 0, 0, 24, 13, _ => false) { Biome = biome, Night = true, BiomeAt = (x, y) => biome };
                scene.Places(null);
                scene.Decor(5, (x, y) => biome);
                foreach (var (_, left, bottom, sx, sy) in sets) scene.Player(left + sx + 0.5f, bottom + sy + 0.5f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#3F7FD8"));
                if (lit) scene.Lit(lights, (x, y) => WorldLights.BiomeGlow(biome), (x, y) => WorldLights.BiomeTint(biome));
                return scene.Image;
            }
            var cave = Half(Biome.Cave, (BiomeLandmarks.MinerHut, 2, 3, 1, 1), (BiomeLandmarks.MineFrame, 14, 5, 3, 2));
            var forest = Half(Biome.Mushroom, (BiomeLandmarks.FairyRing, 3, 2, 3, 2), (BiomeLandmarks.SmallRing, 15, 3, 2, 3));
            var g = new PixelImage(cave.Width, cave.Height + forest.Height + 2);
            g.Fill(0, 0, g.Width, g.Height, DepthBack);
            g.Draw(cave, 0, 0);
            g.Draw(forest, 0, cave.Height + 2);
            return g;
        }

        // The ice cave's and the sand cave's landmarks laid as the generator lays them (묶음 B2a): the beast in ice and
        // the keep's columns on the ice cave's floor, the temple's front under rock and the giant skull with its ribs
        // on the sand cave's.
        static PixelImage IceSandLandmarkSheet(WorldLights lights, bool lit)
        {
            PixelImage Half(Biome biome, bool rock, params (BiomeLandmarkKind Kind, int Left, int Bottom)[] sets)
            {
                var map = new TileMap(24, 13);
                for (int y = 1; y < 10; y++)
                    for (int x = 1; x < 23; x++)
                        map[x, y] = TileType.Floor;
                foreach (var (kind, left, bottom) in sets)
                    foreach (var (dx, dy, type) in IceSandProps.Layout(kind))
                    {
                        var (w, h) = Structures.Footprint(type);
                        for (int j = 0; j < h; j++)
                            for (int i = 0; i < w; i++)
                                map[left + dx + i, bottom + dy + j] = i == 0 && j == 0 ? type : TileType.StructurePart;
                    }
                var scene = new Scene(map, 0, 0, 24, 13, _ => false) { Biome = biome, Night = true, BiomeAt = (x, y) => biome };
                scene.Places(null);
                scene.Decor(5, (x, y) => biome);
                foreach (var (kind, left, bottom) in sets)
                {
                    var box = IceSandProps.Box(kind);
                    scene.Player(left + box.StandX + 0.5f, bottom + box.StandY + 0.5f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#3F7FD8"));
                }
                if (lit) scene.Lit(lights, (x, y) => WorldLights.BiomeGlow(biome), (x, y) => WorldLights.BiomeTint(biome));
                return scene.Image;
            }
            var ice = Half(Biome.Ice, false, (BiomeLandmarkKind.FrozenBeast, 2, 3), (BiomeLandmarkKind.IceRuin, 14, 4));
            var sand = Half(Biome.Sand, true, (BiomeLandmarkKind.TempleFront, 2, 6), (BiomeLandmarkKind.GiantSkull, 14, 3));
            var g = new PixelImage(ice.Width, ice.Height + sand.Height + 2);
            g.Fill(0, 0, g.Width, g.Height, DepthBack);
            g.Draw(ice, 0, 0);
            g.Draw(sand, 0, ice.Height + 2);
            return g;
        }

        // The crystal cave's and the lake's landmarks laid as the generator lays them (묶음 B3a): the giant geode and the
        // arch on the crystal cave's slate, the sunken boat in a pool with islets and the old wheel on its bank.
        static PixelImage CrystalLakeLandmarkSheet(WorldLights lights, bool lit)
        {
            PixelImage Half(Biome biome, bool pool, params (BiomeLandmarkKind Kind, int Left, int Bottom)[] sets)
            {
                var map = new TileMap(24, 13);
                for (int y = 1; y < 10; y++)
                    for (int x = 1; x < 23; x++)
                        map[x, y] = TileType.Floor;
                if (pool)
                {
                    // A pool along the top with a bay, and a small one by the wheel.
                    for (int y = 5; y < 10; y++)
                        for (int x = 1; x < 13; x++)
                            map[x, y] = TileType.Water;
                    for (int y = 3; y < 8; y++)
                        for (int x = 19; x < 23; x++)
                            map[x, y] = TileType.Water;
                    map[9, 7] = TileType.MossIsle;
                    map[10, 7] = TileType.StructurePart;
                    map[2, 8] = TileType.MossIsle;
                    map[3, 8] = TileType.StructurePart;
                    map[14, 2] = TileType.MossRock;
                    map[13, 6] = TileType.Coral;
                    map[8, 3] = TileType.GiantClam;
                }
                foreach (var (kind, left, bottom) in sets)
                    foreach (var (dx, dy, type) in CrystalLakeProps.Layout(kind))
                    {
                        var (w, h) = Structures.Footprint(type);
                        for (int j = 0; j < h; j++)
                            for (int i = 0; i < w; i++)
                                map[left + dx + i, bottom + dy + j] = i == 0 && j == 0 ? type : TileType.StructurePart;
                    }
                var scene = new Scene(map, 0, 0, 24, 13, _ => false) { Biome = biome, Night = true, BiomeAt = (x, y) => biome };
                scene.Places(null);
                scene.Decor(5, (x, y) => biome);
                foreach (var (kind, left, bottom) in sets)
                {
                    var box = CrystalLakeProps.Box(kind);
                    scene.Player(left + box.StandX + 0.5f, bottom + box.StandY + 0.5f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#3F7FD8"));
                }
                if (lit) scene.Lit(lights, (x, y) => WorldLights.BiomeGlow(biome), (x, y) => WorldLights.BiomeTint(biome, 0, x, y));
                return scene.Image;
            }
            var crystal = Half(Biome.Crystal, false, (BiomeLandmarkKind.GiantGeode, 2, 3), (BiomeLandmarkKind.CrystalArch, 14, 4));
            var lake = Half(Biome.Lake, true, (BiomeLandmarkKind.SunkenBoat, 4, 4), (BiomeLandmarkKind.OldWheel, 16, 2));
            var g = new PixelImage(crystal.Width, crystal.Height + lake.Height + 2);
            g.Fill(0, 0, g.Width, g.Height, DepthBack);
            g.Draw(crystal, 0, 0);
            g.Draw(lake, 0, crystal.Height + 2);
            return g;
        }

        // The lava zone's and the abyss's landmarks laid as the generator lays them (묶음 B4a): the fallen chimney and the
        // giant anvil on the basalt with a pool of lava and rocks in it between them, the broken gate and the star altar
        // on the abyss's plates.
        static PixelImage LavaVoidLandmarkSheet(WorldLights lights, bool lit)
        {
            PixelImage Half(Biome biome, bool pool, params (BiomeLandmarkKind Kind, int Left, int Bottom)[] sets)
            {
                var map = new TileMap(24, 13);
                for (int y = 1; y < 10; y++)
                    for (int x = 1; x < 23; x++)
                        map[x, y] = TileType.Floor;
                if (pool)
                {
                    // A pool between the two with rocks standing in it, and what gathers at its edge.
                    for (int y = 4; y < 10; y++)
                        for (int x = 9; x < 14; x++)
                            map[x, y] = TileType.Lava;
                    for (int x = 7; x < 16; x++) map[x, 9] = TileType.Lava;
                    map[11, 6] = TileType.LavaRock;
                    map[10, 8] = TileType.LavaRock;
                    map[12, 8] = TileType.LavaRock;
                    map[11, 2] = TileType.CooledBoulder;
                    map[12, 2] = TileType.StructurePart;
                    map[8, 2] = TileType.FireBush;
                    map[15, 2] = TileType.BasaltColumns;
                    map[16, 2] = TileType.StructurePart;
                }
                foreach (var (kind, left, bottom) in sets)
                    foreach (var (dx, dy, type) in LavaVoidProps.Layout(kind))
                    {
                        var (w, h) = Structures.Footprint(type);
                        for (int j = 0; j < h; j++)
                            for (int i = 0; i < w; i++)
                                map[left + dx + i, bottom + dy + j] = i == 0 && j == 0 ? type : TileType.StructurePart;
                    }
                var scene = new Scene(map, 0, 0, 24, 13, _ => false) { Biome = biome, Night = true, BiomeAt = (x, y) => biome };
                scene.Places(null);
                scene.Decor(5, (x, y) => biome);
                foreach (var (kind, left, bottom) in sets)
                {
                    var box = LavaVoidProps.Box(kind);
                    scene.Player(left + box.StandX + 0.5f, bottom + box.StandY + 0.5f, HeroStanding(Look.Down, HeroMove.Stand, 0, "#3F7FD8"));
                }
                if (lit) scene.Lit(lights, (x, y) => WorldLights.BiomeGlow(biome), (x, y) => WorldLights.BiomeTint(biome, 0, x, y));
                return scene.Image;
            }
            var lava = Half(Biome.Lava, true, (BiomeLandmarkKind.FallenChimney, 1, 3), (BiomeLandmarkKind.GiantAnvil, 16, 3));
            var abyss = Half(Biome.Abyss, false, (BiomeLandmarkKind.BrokenGate, 2, 4), (BiomeLandmarkKind.StarAltar, 14, 3));
            var g = new PixelImage(lava.Width, lava.Height + abyss.Height + 2);
            g.Fill(0, 0, g.Width, g.Height, DepthBack);
            g.Draw(lava, 0, 0);
            g.Draw(abyss, 0, lava.Height + 2);
            return g;
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
            if (lit) scene.Lit(lights, (x, y) => WorldLights.BiomeGlow(world.Biomes[x, y]), (x, y) => WorldLights.BiomeTint(world.Biomes[x, y], 0, x, y));
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
            var kinds = LavaVoidProps.Any(biome).Select(k => k.Type).ToList();
            if (biome == Biome.Cave) kinds.Insert(kinds.IndexOf(TileType.UnlitLamp) + 1, TileType.LitLamp);
            // Its thing to pick or break and what is left of it (Docs/Harvest.md, version 9).
            kinds.Add(HarvestProps.Of(biome));
            kinds.Add(HarvestProps.SpentOf(HarvestProps.Of(biome)));
            var decor = Enumerable.Range(0, 100).Select(roll => Decor.KindOf(biome, roll)).Distinct().ToList();
            // The lake's water's edge: reeds and lily pads (Decor.On).
            if (biome == Biome.Lake) decor.AddRange(new[] { DecorKind.Reeds, DecorKind.LilyPad });
            // The lava's edge: blisters of cooled lava and rafts of crust.
            if (biome == Biome.Lava) decor.AddRange(new[] { DecorKind.LavaBubble, DecorKind.CrustRaft });
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
                // What stands in a pool is shown on its water.
                if (CrystalLakeProps.Floats(type))
                    for (int y = 16; y < 48; y += 16)
                        for (int x = 0; x < cellW * 2 - 12; x += 16)
                            g.Draw(TileArt.Ground(TileArt.Index(GroundKind.Water, 0, (x / 16 + y / 16) % 4, biome)), x0 + x, y0 + y);
                // A rock in the lava on its lava.
                if (LavaVoidProps.InLava(type))
                    for (int y = 16; y < 48; y += 16)
                        for (int x = 0; x < cellW * 2 - 12; x += 16)
                            g.Draw(TileArt.Ground(TileArt.Index(GroundKind.Lava, y == 16 ? 1 : 0, (x / 16 + y / 16) % 4, biome)), x0 + x, y0 + y);
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
                    // Reeds are taller than the rest: every picture stands on the same line.
                    int rise = DecorArt.Image(kind, biome, v).Height - DecorArt.Height;
                    if (rise > 0)
                    {
                        g.Draw(DecorArt.Image(kind, biome, v), dx, y1 + 24 - rise);
                        dx += 14;
                        g.Draw(DecorArt.Image(kind, biome, v, 1), dx, y1 + 24 - rise);
                        dx += 14;
                        continue;
                    }
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
                if (CritterArt.HomeOf(critter) != biome) continue;
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
                if (CritterArt.HomeOf(mote) != biome) continue;
                g.Draw(CritterArt.Mote(mote), cx, y2 + 6);
                cx += 10;
            }
            g.Small(leftPad, y2 + 20, "CRITTERS (2 FRAMES EACH) AND MOTES", DepthDim, DepthBack);
            sheet = g;
            return ok;
        }
    }
}
