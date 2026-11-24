using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;

namespace Game.Client
{
    // Checks the pixel art for the slips that are easy to make by code and hard to see at sheet size (Docs/ArtPipeline.md
    // 그림 검사, 2026-11-11, the research report's candidate 21):
    //  - stray pixels: a drawn pixel with no drawn pixel in the eight round it, a speck that reads as noise at 16 px;
    //  - too many colours: more than MostColours in one sprite, where a few ramps should do;
    //  - a wobbling silhouette: between one frame of a loop and the next, more than MostWobble of the shape changing,
    //    which reads as jitter rather than motion.
    // The drawings made before this check have some of each, so it ratchets: lint-baseline.txt holds the counts as they
    // were, the check fails only when a count goes up, and lowering one is a matter of writing the new count there.
    // Everything found is listed in out/lint.md.
    static class ArtLint
    {
        const int MostColours = 16;
        const float MostWobble = 0.4f;
        const string BaselineFile = "lint-baseline.txt";

        public static bool Run(LootData loot, MonsterData monsters, string folder, string output)
        {
            var stills = new List<(string Name, PixelImage Image)>();
            foreach (var material in loot.Materials.Where(m => m.Base == null)) stills.Add(($"재료 {material.Id}", ItemArt.Material(material)));
            foreach (var item in loot.Bases) stills.Add(($"장비 {item.Id}", ItemArt.Gear(item)));
            foreach (var consumable in loot.Consumables) stills.Add(($"소모품 {consumable.Id}", ItemArt.Consumable(consumable)));
            foreach (TileType type in Enum.GetValues(typeof(TileType)))
            {
                PixelImage image;
                try
                {
                    image = TileArt.Object(type);
                }
                catch (Exception)
                {
                    continue;
                }
                if (image != null) stills.Add(($"칸 {type}", image));
            }

            // The standing layer's own (묶음 B0): the joined shapes, the gates and the floor's decor.
            for (int mask = 0; mask < 16; mask++)
            {
                stills.Add(($"울타리 {mask}", PropArt.Prop(TileType.Fence, mask)));
                stills.Add(($"산울타리 {mask}", PropArt.Prop(TileType.Hedge, mask)));
                // A wall block does not show its south side (the wall in front covers it), so only the eight others.
                if ((mask & Joins.S) == 0) stills.Add(($"철벽 {mask}", PropArt.Prop(TileType.IronWall, mask)));
            }
            foreach (bool along in new[] { true, false })
                foreach (bool open in new[] { true, false })
                    stills.Add(($"울타리 문 {(along ? "가로" : "세로")} {(open ? "열림" : "닫힘")}", PropArt.FenceGate(along, open)));
            foreach (Biome biome in Enum.GetValues(typeof(Biome)))
                foreach (var kind in Enumerable.Range(0, 100).Select(roll => Decor.KindOf(biome, roll)).Distinct())
                    for (int variant = 0; variant < Decor.Variants; variant++)
                        stills.Add(($"꾸밈 {biome} {kind} {variant}", DecorArt.Image(kind, biome, variant)));
            // The biomes' props in each drawing (묶음 B1a; a lamp and the giant cap have one) and the places' tree by biome:
            // looked at only for twins (two drawings of a prop the very same). Their halos and many materials are meant,
            // so they are not counted for stray pixels or colours.
            var props = new List<(string Name, PixelImage Image)>();
            foreach (TileType type in Enum.GetValues(typeof(TileType)))
            {
                if (!BiomeProps.Is(type)) continue;
                int drawings = BiomeProps.IsLamp(type) || type == TileType.GiantCap ? 1 : BiomeArt.Variants;
                for (int variant = 0; variant < drawings; variant++) props.Add(($"소품 {type} {variant}", BiomeArt.Prop(type, variant)));
            }
            foreach (var biome in new[] { Biome.Cave, Biome.Mushroom }) props.Add(($"장소 나무 {biome}", BiomeArt.PlaceTree(biome)));
            // The places' things in each biome's look and the landmarks (묶음 B1c), the same way.
            foreach (TileType type in Enum.GetValues(typeof(TileType)))
            {
                if (PropArt.HasBiomeLook(type))
                    foreach (int look in new[] { PropArt.CaveLook, PropArt.ForestLook }) props.Add(($"장소 {type} 모습 {look}", PropArt.PlaceProp(type, look)));
                if (type == TileType.RingCap)
                    for (int variant = 0; variant < BiomeArt.Variants; variant++) props.Add(($"랜드마크 {type} {variant}", BiomeArt.RingCap(variant)));
                else if (BiomeLandmarks.Is(type)) props.Add(($"랜드마크 {type}", PropArt.Landmark(type)));
            }

            var loops = new List<(string Name, IReadOnlyList<PixelImage> Frames)>();
            foreach (var look in new[] { Look.Down, Look.DownSide, Look.Side, Look.UpSide, Look.Up })
                loops.Add(($"플레이어 걷기 {look}", Enumerable.Range(0, HeroArt.WalkFrames).Select(f => HeroArt.Figure(HeroArt.Draw(look, HeroMove.Walk, f, HeroGear.None, Rgba.Hex("#3F7FD8")))).ToList()));
            foreach (var monster in monsters.Monsters)
                loops.Add(($"몬스터 {monster.Id}", Enumerable.Range(0, CreatureArt.IdleFrames).Select(f => CreatureArt.Monster(monster.Id, Rgba.Hex(monster.Color), f)).ToList()));

            var strays = new List<string>();
            var colourful = new List<string>();
            foreach (var (name, image) in stills.Concat(loops.Select(l => (l.Name, l.Frames[0]))))
            {
                int stray = Strays(image);
                if (stray > 0) strays.Add($"{name}: {stray}");
                int colours = Colours(image);
                if (colours > MostColours) colourful.Add($"{name}: {colours}색");
            }
            var wobbly = new List<string>();
            foreach (var (name, frames) in loops)
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    float wobble = Wobble(frames[i], frames[(i + 1) % frames.Count]);
                    if (wobble > MostWobble) wobbly.Add($"{name} {i}→{(i + 1) % frames.Count}: {wobble:P0}");
                }
            }

            // Different things drawn the very same (2026-11-11, the content audit: five materials fell through to the stone's
            // picture). Every id past the first in a group of identical pictures counts; a placed thing and its item are meant to match.
            var twins = stills.Where(s => !s.Name.StartsWith("칸 ")).Concat(props).GroupBy(s => Key(s.Image)).Where(g => g.Count() > 1).Select(g => g.Select(s => s.Name).ToList()).ToList();
            int twinCount = twins.Sum(g => g.Count - 1);

            var counts = new Dictionary<string, int> { ["strays"] = strays.Count, ["colours"] = colourful.Count, ["wobble"] = wobbly.Count, ["twins"] = twinCount };
            var report = new List<string>
            {
                "# 그림 검사",
                "",
                $"그림 {stills.Count}개, 움직임 {loops.Count}개를 보았다(`Docs/ArtPipeline.md` 그림 검사).",
                "",
                $"## 외딴 픽셀 ({strays.Count})", "",
            };
            report.AddRange(strays.Select(s => "- " + s));
            report.AddRange(new[] { "", $"## 색이 {MostColours}개를 넘는 그림 ({colourful.Count})", "" });
            report.AddRange(colourful.Select(s => "- " + s));
            report.AddRange(new[] { "", $"## 프레임 사이 실루엣이 {MostWobble:P0} 넘게 바뀌는 움직임 ({wobbly.Count})", "" });
            report.AddRange(wobbly.Select(s => "- " + s));
            report.AddRange(new[] { "", $"## 다른 것인데 픽셀까지 같은 그림 ({twinCount})", "" });
            report.AddRange(twins.Select(g => "- " + string.Join(" = ", g)));
            File.WriteAllLines(Path.Combine(output, "lint.md"), report);

            string baselinePath = Path.Combine(folder, BaselineFile);
            var baseline = File.Exists(baselinePath)
                ? File.ReadAllLines(baselinePath).Select(l => l.Split(' ')).Where(p => p.Length == 2 && int.TryParse(p[1], out _)).ToDictionary(p => p[0], p => int.Parse(p[1]))
                : new Dictionary<string, int>();
            bool fine = true;
            foreach (var (key, count) in counts)
            {
                int allowed = baseline.TryGetValue(key, out int was) ? was : int.MaxValue;
                Console.WriteLine($"art lint {key}: {count} (baseline {(allowed == int.MaxValue ? "-" : allowed.ToString())})");
                if (count > allowed)
                {
                    Console.Error.WriteLine($"art lint: {key} went up from {allowed} to {count}; see {Path.Combine(output, "lint.md")}");
                    fine = false;
                }
            }
            return fine;
        }

        static string Key(PixelImage image)
        {
            var key = new System.Text.StringBuilder($"{image.Width}x{image.Height}:");
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    var c = image[x, y];
                    key.Append(c.A == 0 ? "." : $"{c.R:x2}{c.G:x2}{c.B:x2}{c.A:x2}");
                }
            return key.ToString();
        }

        static bool Drawn(PixelImage image, int x, int y) => x >= 0 && y >= 0 && x < image.Width && y < image.Height && image[x, y].A > 0;

        static int Strays(PixelImage image)
        {
            int count = 0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    if (!Drawn(image, x, y)) continue;
                    bool alone = true;
                    for (int dy = -1; dy <= 1 && alone; dy++)
                        for (int dx = -1; dx <= 1 && alone; dx++)
                            if ((dx != 0 || dy != 0) && Drawn(image, x + dx, y + dy)) alone = false;
                    if (alone) count++;
                }
            }
            return count;
        }

        static int Colours(PixelImage image)
        {
            var seen = new HashSet<int>();
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    var c = image[x, y];
                    if (c.A > 0) seen.Add(c.R << 16 | c.G << 8 | c.B);
                }
            return seen.Count;
        }

        // The share of the two frames' combined shape that is drawn in one and not the other.
        static float Wobble(PixelImage a, PixelImage b)
        {
            int width = Math.Max(a.Width, b.Width), height = Math.Max(a.Height, b.Height);
            int either = 0, differ = 0;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    bool inA = Drawn(a, x, y), inB = Drawn(b, x, y);
                    if (inA || inB) either++;
                    if (inA != inB) differ++;
                }
            return either == 0 ? 0 : differ / (float)either;
        }
    }
}
