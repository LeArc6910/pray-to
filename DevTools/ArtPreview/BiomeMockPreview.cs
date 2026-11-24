using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core;
using static Game.Client.BM;

namespace Game.Client
{
    // Mockups of the biome rework for a person to choose from (out/biomemock/, --only biomemock, Docs/BiomeRework.md):
    // the joined fences, the rules for standing things, a board for each of the eight biomes, the palettes side by
    // side, the size classes and all eight scenes at a glance. Pictures only: nothing here is in the game, and the
    // writing is a small Latin font because the tool cannot draw the game's.
    static class BiomeMockPreview
    {
        static readonly Rgba Back = C("#14121A"), Panel = C("#1E1B26"), Text = C("#E8E4F0"), Dim = C("#8E8AA4"), Yellow = C("#FFD860"), Cyan = C("#70E0FF"), Red = C("#FF6070"), Green = C("#80E070");
        static readonly Rgba[] TagColour = { C("#A8A4B8"), C("#FFD860"), C("#FF9A40"), C("#70B8FF"), C("#FF6070"), C("#80E070") };
        static readonly string[] TagName = { "IN GAME NOW", "NEW (1ST WAVE)", "NEW (2ND WAVE)", "RECOLOUR", "MID-BOSS", "REDRAWN" };

        public static void Write(string output)
        {
            string folder = Directory.CreateDirectory(Path.Combine(output, "biomemock")).FullName;
            var index = new StringBuilder("# 생물군계 개편 시안\n\n`Docs/BiomeRework.md`의 제안을 그림으로 본 것이다(`dotnet run --project DevTools/ArtPreview -- --only biomemock`). 게임에 들어간 그림이 아니고, 글자는 도구의 작은 로마자 글꼴이다. 새 몬스터와 소품은 실루엣과 크기를 보려는 거친 그림이다.\n\n| 그림 | 보여 주는 것 |\n|---|---|\n");
            void Save(PixelImage image, string name, int scale, string what)
            {
                Png.Save(image.Times(scale), Path.Combine(folder, name));
                index.Append($"| `{name}` | {what} |\n");
            }

            Save(Fences(), "fences.png", 3, "이어지는 울타리(4-4): 조각 다섯, 이웃 마스크 16모양, 끝·문(가로·세로, 닫힘·열림)·닳은 변형, 같은 틀의 산울타리·돌담·철책, 지금 울타리 타일로 두른 방목장과 새 울타리로 두른 방목장(사람이 뒤와 앞에 선 모습)");
            Save(Depth(), "depth.png", 3, "입체 오브젝트 규칙(4-3): 발자국과 그림 상자, 그림자와 접지 음영, 발 y 정렬, 잎 흐려짐, 뒤집기·크기·색 변화, Poisson 배치와 격자 배치");
            var scenes = new Dictionary<string, MockScene>();
            var lit = new Dictionary<string, PixelImage>();
            foreach (var look in BiomeMockArt.Looks)
            {
                scenes[look.Id] = Scene(look);
                lit[look.Id] = scenes[look.Id].Render();
                Save(Board(look, lit[look.Id]), $"biome_{look.Id}.png", 3, $"{KoreanName(look.Id)}: 팔레트, 20x12칸 장면(바닥·벽, 밀도대로 놓은 소품, 작은 생물, 입자, 빛 색, 위험·고유 규칙, 랜드마크), 소품 네 층, 몬스터 줄(지금 것과 새 것, 크기 등급)");
            }
            Save(ComparePalettes(scenes, lit), "compare_palettes.png", 2, "결정 1: 보라 셋(버섯 숲·수정 동굴·공허의 심연)의 지금 타일, 새 팔레트, 새 팔레트에 빛과 소품. 나머지 다섯의 지금과 다듬은 것");
            Save(Sizes(), "sizes.png", 3, "결정 3: 크기 등급 XS~XL을 사람 옆에, 판정 원, 1칸·2칸·3칸 통로에 들어가는지, L(2x2)이 있는 방과 XL(3x3)이 있는 방");
            Save(Overview(lit), "overview.png", 1, "여덟 장면을 4x2로 한눈에(서로 갈리는지 보기)");
            File.WriteAllText(Path.Combine(folder, "index.md"), index.ToString());
            Console.WriteLine($"Wrote the biome mockups to {folder}");
        }

        static string KoreanName(string id) => id switch
        {
            "cave" => "흙 동굴", "mushroom" => "버섯 숲", "ice" => "얼음 동굴", "crystal" => "수정 동굴", "sand" => "모래 동굴", "lava" => "용암 지대", "lake" => "지하 호수", _ => "공허의 심연",
        };

        // ---- Small helpers ----

        static PixelImage Canvas(int w, int h)
        {
            var g = new PixelImage(w, h);
            g.Fill(0, 0, w, h, Back);
            return g;
        }

        static void Label(PixelImage g, int x, int y, string text, Rgba colour) => g.Small(x, y, text, colour, Back);

        static void Centre(PixelImage g, int cx, int y, string text, Rgba colour) => g.Small(cx - MockFont.SmallWidth(text) / 2, y, text, colour, Back);

        static void Frame(PixelImage g, int x, int y, int w, int h, Rgba colour, int dash = 0)
        {
            g.Line(x, y, x + w - 1, y, colour, 1, dash);
            g.Line(x, y + h - 1, x + w - 1, y + h - 1, colour, 1, dash);
            g.Line(x, y, x, y + h - 1, colour, 1, dash);
            g.Line(x + w - 1, y, x + w - 1, y + h - 1, colour, 1, dash);
        }

        // Open floor of a biome, without the dark edge a map's border gives.
        static PixelImage Floor(BiomeLook look, int wt, int ht, bool today = false)
        {
            var map = Enumerable.Repeat(new string('.', wt + 4), ht + 4).ToArray();
            var whole = today ? BiomeGround.Today(look, map) : BiomeGround.Draw(look, map);
            return Crop(whole, 32, 32, wt * T, ht * T);
        }

        static PixelImage Crop(PixelImage image, int x, int y, int w, int h)
        {
            var result = new PixelImage(w, h);
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++) result[i, j] = image[x + i, y + j];
            return result;
        }

        static void Grid(PixelImage g, int x, int y, int wt, int ht, Rgba colour)
        {
            for (int i = 0; i <= wt; i++) g.Line(x + i * T, y, x + i * T, y + ht * T, colour);
            for (int j = 0; j <= ht; j++) g.Line(x, y + j * T, x + wt * T, y + j * T, colour);
        }

        static MockObj Obj(PixelImage img, int x, int y, int shadow = -1, int lift = 0, float alpha = 1f) =>
            new MockObj { Img = img, X = x, Y = y, Shadow = shadow < 0 ? Math.Max(4, (int)(img.Width * 0.75f)) : shadow, Lift = lift, Alpha = alpha };

        static void Motes(PixelImage g, int salt, int count, Func<int, Rgba> colour, int tall = 1, bool cross = false, int left = 0, int top = 0, int width = 0, int height = 0)
        {
            if (width == 0) width = g.Width;
            if (height == 0) height = g.Height;
            for (int i = 0; i < count; i++)
            {
                int x = left + (int)(V(i, 1, salt) * width), y = top + (int)(V(i, 2, salt) * height);
                var c = colour(i);
                g.Blend(x, y, c);
                for (int j = 1; j < tall; j++) g.Blend(x, y + j, c.WithAlpha(c.A / (j + 1)));
                if (cross)
                {
                    g.Blend(x - 1, y, c.WithAlpha(110));
                    g.Blend(x + 1, y, c.WithAlpha(110));
                    g.Blend(x, y - 1, c.WithAlpha(110));
                    g.Blend(x, y + 1, c.WithAlpha(110));
                }
            }
        }

        static void Cloud(PixelImage g, int cx, int cy, int radius, Rgba colour)
        {
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                {
                    float d = MathF.Sqrt(x * x + y * y * 1.5f) / radius + (Smooth(cx + x, cy + y, 6, 91) - 0.5f) * 0.35f;
                    if (d >= 1) continue;
                    if (d > 0.82f) g.Blend(cx + x, cy + y, colour.WithAlpha(150));
                    else if ((x + y & 1) == 0) g.Blend(cx + x, cy + y, colour.WithAlpha(d > 0.5f ? 120 : 80));
                    else g.Blend(cx + x, cy + y, colour.WithAlpha(35));
                }
        }

        // ---- The eight scenes ----

        static MockScene Scene(BiomeLook look)
        {
            var s = new MockScene(look);
            var props = BiomeMockArt.Props(look.Id);
            var critters = BiomeMockArt.Critters(look.Id);
            var roster = BiomeMockArt.Roster(look.Id);
            PixelImage G(string name) => props.First(p => p.Name.StartsWith(name)).Img;
            PixelImage Cr(int i) => critters[i].Img;
            BiomeMockArt.Mon Mo(string name) => roster.First(m => m.Name.StartsWith(name));
            MockObj Monster(string name, float tx, float ty, int ox = 0, int oy = 0, bool flip = false)
            {
                var m = Mo(name);
                return s.Put(flip ? m.Img.FlippedX() : m.Img, tx, ty, ox, oy, lift: m.Lift);
            }
            var cover = props.Where(p => p.Tier == "cover").Select(p => p.Img).ToArray();
            var warm = C("#FFE8C0");
            MockObj hero;
            switch (look.Id)
            {
                case "cave":
                {
                    s.Put(G("MINE GATE"), 7, 2, 0, 4);
                    var lamp = s.Put(G("LAMP"), 4, 4);
                    s.Glow(lamp, look.Light, 76, 0.95f, 12);
                    s.Put(G("LAMP"), 13, 8, 2, 0);
                    s.Put(G("STALAGMITE"), 3, 3, -3, 0);
                    s.Put(G("STALAGMITE").FlippedX(), 13, 2, 3, 2);
                    s.Put(G("STALAGMITE"), 14, 4, 0, -3);
                    s.Put(G("PIT PROP"), 17, 6, 4, 2);
                    s.Put(G("PIT PROP"), 17, 8, 4, 2);
                    s.Put(G("CART"), 9, 8, 0, 2);
                    s.Put(G("ORE SACK"), 10, 8, 6, 4);
                    s.Put(G("CAVE-IN"), 3.5f, 8);
                    s.Put(G("BOULDER"), 12.5f, 5, 0, 4);
                    foreach (var (x, y, ox) in new[] { (6f, 9f, 0), (7f, 9f, 4), (7f, 8f, -6), (8f, 9f, 8), (5f, 8f, 5) }) s.Put(Mo("CAVE RAT").Img, x, y, ox, x == 7f ? 3 : -2);
                    Monster("BIG BEETLE", 14, 7, 0, 0, true);
                    Monster("SLIME", 10, 2);
                    s.Put(Cr(1), 6, 6, 0, 0, 4);
                    s.Put(Cr(0), 10, 4, 5, 0, 3);
                    s.Put(Cr(2), 4, 3, 6, -2, 0, 9);
                    hero = s.Put(Hero(), 8, 5);
                    s.Glow(hero, warm, 60, 0.7f, 8);
                    s.Under = g => g.Ring(lamp.X, lamp.Y - 4, 62, look.Light.WithAlpha(70));
                    s.Over = g => Motes(g, 101, 16, i => C("#FFE0A0").WithAlpha(150), 1, false, lamp.X - 40, lamp.Y - 50, 80, 70);
                    break;
                }
                case "mushroom":
                {
                    var giant = s.Put(G("GIANT CAP"), 17, 5, 0, 2);
                    s.Glow(giant, C("#7AA8FF"), 60, 0.4f, 26);
                    s.Put(G("MUSH TREE"), 2.5f, 2, 0, 2);
                    s.Put(G("MUSH TREE").FlippedX(), 7.5f, 9, 0, 0);
                    s.Put(G("PILLAR CAP"), 10, 2);
                    s.Put(G("PILLAR CAP"), 6, 6, 3, 0);
                    s.Put(G("PILLAR CAP").FlippedX(), 1, 5, 0, 2);
                    s.Put(G("SHELF STUMP"), 13, 8, 2, 0);
                    s.Put(G("FALLEN LOG"), 2.5f, 9, 4, 2);
                    foreach (var (x, y, ox) in new[] { (13f, 1f, 4), (15f, 2f, 0), (16f, 3f, -5), (14f, 3f, 6) }) s.Put(G("SPORE SAC"), x, y, ox, 0);
                    foreach (var (x, y) in new[] { (1f, 8f), (8f, 5f), (18f, 8f), (11f, 6f) }) s.Glow(s.Put(G("GLOW CAPS"), x, y), look.Light, 34, 0.6f, 4);
                    // A fairy ring: small caps in a circle.
                    for (int i = 0; i < 9; i++)
                    {
                        float a = i / 9f * MathF.PI * 2;
                        s.Objs.Add(Obj(G("CAPS"), 14 * T + 4 + (int)(MathF.Cos(a) * 15), 9 * T + 2 + (int)(MathF.Sin(a) * 9), 5));
                    }
                    Monster("MYCONID", 14, 2, 4, 2);
                    Monster("PUFFBALL", 12, 5, 0, 0);
                    Monster("CAP GIANT", 4, 6, 0, 0);
                    Monster("LURKER", 14, 9, 14, -6);
                    var snail = s.Put(Cr(1), 9, 8, -4, 0, 4);
                    foreach (var (x, y) in new[] { (6f, 3f), (11f, 8f), (3f, 5f), (16f, 7f) }) s.Glow(s.Put(Cr(0), x, y, 3, 0, 0, 8), C("#E8FF90"), 12, 0.7f, 1);
                    hero = s.Put(Hero(), 10, 7);
                    s.Glow(hero, warm, 56, 0.45f, 8);
                    s.Under = g => g.Line(snail.X - 22, snail.Y - 1, snail.X - 4, snail.Y - 1, look.Accent.WithAlpha(120));
                    s.Over = g =>
                    {
                        Cloud(g, 14 * T + 14, 2 * T + 6, 27, look.Accent);
                        Motes(g, 102, 46, i => look.Accent.WithAlpha(i % 3 == 0 ? 220 : 130), 2);
                    };
                    break;
                }
                case "ice":
                {
                    s.Put(G("ICEFALL"), 9.5f, 1, 0, 9, 0);
                    s.Glow(s.Put(G("ICE PILLAR"), 2, 8, 6, 0), look.Accent, 44, 0.5f, 18);
                    s.Glow(s.Put(G("ICE PILLAR"), 11.5f, 3, 0, 4), look.Accent, 44, 0.5f, 18);
                    s.Put(G("BEAST IN ICE"), 16, 9, 6, 2);
                    s.Put(G("ICE SPIKE"), 1, 3);
                    s.Put(G("ICE SPIKE").FlippedX(), 18, 3, 0, -4);
                    s.Put(G("ICE SPIKE"), 8, 8, 4, 0);
                    s.Put(G("FROZEN BUSH"), 5, 8, 0, 4);
                    s.Put(G("FROZEN BUSH"), 12, 8, 4, -2);
                    s.Put(G("SNOW MOUND"), 10, 6, 6, 4);
                    s.Put(G("SNOW MOUND"), 2, 5, -4, 0);
                    s.Glow(s.Put(G("ICE CRYSTALS"), 11, 9), look.Accent, 28, 0.6f, 5);
                    s.Glow(s.Put(G("ICE CRYSTALS"), 18, 6, -2, 4), look.Accent, 28, 0.6f, 5);
                    foreach (var (x, y, ox) in new[] { (14f, 5f, 0), (15f, 6f, 6), (16f, 4f, 2) }) Monster("FROST WOLF", x, y, ox, 0, true);
                    Monster("ICE SLIME", 4, 2, 0, 4);
                    s.Put(Cr(0), 13, 2, 0, 4, 4);
                    s.Put(Cr(1), 7, 2, 0, 0, 0, 10);
                    hero = s.Put(Hero(), 7, 5);
                    s.Glow(hero, warm, 56, 0.3f, 8);
                    int ix = 6 * T + 4, iy = 8 * T - 2;
                    s.Under = g =>
                    {
                        // The falling icicle's warning: a shade ring on the floor.
                        Shadow(g, ix, iy, 16, 90);
                        g.Ring(ix, iy, 9, Red.WithAlpha(200));
                        for (int i = 0; i < 4; i++) g.Line(hero.X - 26 + i * 3, hero.Y - 2 - i % 2 * 2, hero.X - 12 + i * 2, hero.Y - 2 - i % 2 * 2, C("#DCEFFA").WithAlpha(150));
                        for (int i = 0; i < 3; i++) g.Line(15 * T + 30 + i * 4, 5 * T + 8 + i * 5, 15 * T + 52 + i * 3, 5 * T + 8 + i * 5, C("#DCEFFA").WithAlpha(140));
                    };
                    s.Over = g =>
                    {
                        g.Draw(new Sp(6, 12).Spike(3f, 0f, 2.8f, 3f, 12f, R("#A8D4EC")).Out(C("#22364A")), ix - 4, iy - 44);
                        g.Line(ix, iy - 28, ix, iy - 14, C("#DCEFFA").WithAlpha(120), 1, 2);
                        Motes(g, 103, 70, i => Rgba.White.WithAlpha(i % 2 == 0 ? 230 : 140), 1, false);
                    };
                    break;
                }
                case "crystal":
                {
                    s.Glow(s.Put(G("GIANT GEODE"), 14, 3, 0, 4), C("#FF8AD8"), 70, 0.9f, 16);
                    var mass = s.Put(G("CRYSTAL MASS"), 5.5f, 5, 0, 2);
                    s.Glow(mass, C("#8AF0FF"), 50, 0.6f, 14);
                    s.Glow(s.Put(G("LEANING"), 16, 8, 4, 0), C("#8AF0FF"), 36, 0.5f, 20);
                    s.Glow(s.Put(G("CLUSTER"), 3, 2, 0, 2), C("#FF8AD8"), 30, 0.6f, 5);
                    s.Glow(s.Put(G("CLUSTER").FlippedX(), 7, 8, -2, 0), C("#8AF0FF"), 30, 0.6f, 5);
                    s.Glow(s.Put(G("CLUSTER"), 17, 6, 2, 0), C("#FF8AD8"), 30, 0.6f, 5);
                    s.Glow(s.Put(G("CLUSTER").FlippedX(), 4, 9, 4, -4), C("#FF8AD8"), 30, 0.6f, 5);
                    s.Glow(s.Put(G("SPIRE"), 9, 4, 4, 0), C("#8AF0FF"), 34, 0.5f, 14);
                    s.Put(G("SPIRE").FlippedX(), 13, 5, 0, 4);
                    var res = s.Put(G("RESONATOR"), 12, 6, 6, 6);
                    s.Put(G("GEODE"), 6, 3, 6, -4);
                    s.Put(G("GEODE"), 15, 9, 4, 0);
                    var mimic = Monster("MIMIC", 14, 7, 6, 0);
                    Monster("TORTOISE", 4, 7, 0, 0);
                    Monster("CRYSTAL BAT", 11, 3, 0, 4);
                    s.Put(Cr(0), 10, 5, -4, 4, 4);
                    s.Put(Cr(1), 8, 2, 0, 0, 0, 9);
                    hero = s.Put(Hero(flip: true), 8, 6, 2, 2);
                    s.Glow(hero, warm, 56, 0.35f, 8);
                    s.Under = g =>
                    {
                        g.Ring(res.X, res.Y - 4, 14, Cyan.WithAlpha(150));
                        g.Ring(res.X, res.Y - 4, 24, Cyan.WithAlpha(90));
                    };
                    s.Over = g =>
                    {
                        // A shot bouncing off the big crystal.
                        g.Line(hero.X - 6, hero.Y - 10, mass.X + 10, mass.Y - 12, Yellow, 1, 2);
                        g.Line(mass.X + 10, mass.Y - 12, mass.X + 34, mass.Y - 44, Yellow, 1, 2);
                        g.Disc(mass.X + 10, mass.Y - 12, 2, Rgba.White);
                        g.Small(mimic.X - 2, mimic.Y - 24, "!", Yellow, Back);
                        Motes(g, 104, 26, i => (i % 3 == 0 ? C("#FF7FD0") : i % 3 == 1 ? C("#7FF2FF") : Rgba.White).WithAlpha(230), 1, true);
                    };
                    break;
                }
                case "sand":
                {
                    s.Put(G("TEMPLE FRONT"), 12.5f, 2, 0, 2);
                    s.Put(G("PILLAR STUB"), 10, 3, -4, 0);
                    s.Put(G("PILLAR STUB").FlippedX(), 15, 3, 6, 0);
                    s.Put(G("RIB ARCH"), 9.5f, 8, 0, 2, 26);
                    s.Put(G("SUNK COLUMN"), 17, 7, 0, 0);
                    s.Put(G("CACTUS"), 6, 9, 0, 2);
                    s.Put(G("CACTUS").FlippedX(), 17, 3, 6, 4);
                    s.Put(G("CACTUS"), 1, 7, 0, 0);
                    s.Put(G("SKULL"), 8, 6, 0, 0);
                    s.Put(G("BROKEN POT"), 12, 4, 0, 0);
                    s.Put(G("BROKEN POT").FlippedX(), 13, 4, 2, 5);
                    var stream = s.Put(G("SAND STREAM"), 7, 4, 2, 0, 9);
                    var stream2 = s.Put(G("SAND STREAM"), 14, 9, 0, -4, 9);
                    // The abandoned camel pen: worn fence round bones and a pack.
                    s.Objs.AddRange(FenceMock.Layout('F', new[] { "FwFF", "F..l", "F...", "FFFF" }, 1 * T, 2 * T));
                    s.Put(G("BONES"), 2, 3, 4, 2);
                    s.Put(G("SKULL"), 3, 4, 0, -2);
                    Monster("SCORPION", 12, 8, 0, 6);
                    Monster("BOMB SCARAB", 5, 6, 0, 0);
                    s.Put(Cr(0), 9, 2, 0, 4, 4);
                    s.Put(Cr(1), 12, 6, 0, 4, 4);
                    hero = s.Put(Hero(), 9, 7, 6, 0);
                    s.Glow(hero, warm, 50, 0.15f, 8);
                    s.Under = g =>
                    {
                        // What lies under the stream: eyes in the sand.
                        g.Ring(stream.X, stream.Y - 1, 7, C("#5A3C24").WithAlpha(180));
                        g.Ring(stream2.X, stream2.Y - 1, 7, C("#5A3C24").WithAlpha(180));
                    };
                    s.Over = g => Motes(g, 105, 40, i => C("#EDD8A8").WithAlpha(i % 2 == 0 ? 170 : 90), 1);
                    break;
                }
                case "lava":
                {
                    for (int y = 1; y < 11; y += 2) s.Lights.Add((9 * T + 8, y * T + 8, 58, look.Light, 0.55f));
                    s.Put(G("GIANT ANVIL"), 15.5f, 3, 0, 0);
                    s.Put(G("BASALT COLUMNS"), 3.5f, 3, 0, 0);
                    s.Put(G("COOLED ROCK"), 15, 9, 0, 0);
                    s.Put(G("OBSIDIAN"), 2, 8, 0, 0);
                    s.Put(G("OBSIDIAN").FlippedX(), 13, 2, 4, 0);
                    s.Put(G("OBSIDIAN"), 18, 6, -2, 0);
                    s.Glow(s.Put(G("FIRE BUSH"), 5, 8, 0, 4), look.Light, 30, 0.7f, 8);
                    s.Glow(s.Put(G("FIRE BUSH"), 17, 4, 0, 6), look.Light, 30, 0.7f, 8);
                    s.Glow(s.Put(G("FIRE BUSH"), 2, 5, 0, 0), look.Light, 30, 0.7f, 8);
                    s.Put(G("SLAG HEAP"), 17, 9, 8, 6);
                    s.Put(G("SLAG HEAP"), 5, 2, 0, 0);
                    var vent = s.Put(G("STEAM VENT"), 5, 4, 4, 0, flat: true);
                    var vent2 = s.Put(G("STEAM VENT"), 13, 7, 0, 0, flat: true);
                    var hurler = Monster("MAGMA HURLER", 12.5f, 5, 4, 0, true);
                    Monster("SALAMANDER", 3, 6, 0, 4);
                    Monster("MAGMA SLIME", 14, 6, 6, 6);
                    s.Put(Cr(0), 4, 9, 0, 0, 4);
                    s.Put(Cr(1), 6, 7, 0, 0, 0, 10);
                    hero = s.Put(Hero(), 6, 5, 0, -1);
                    s.Glow(hero, warm, 50, 0.3f, 8);
                    s.Under = g =>
                    {
                        g.Ring(vent2.X, vent2.Y - 2, 15, Red.WithAlpha(210));
                        g.Ring(hero.X - 6, hero.Y + 5, 11, Red.WithAlpha(210));
                        g.Disc(hero.X - 6, hero.Y + 5, 10, Red.WithAlpha(50));
                    };
                    s.Over = g =>
                    {
                        // Steam from the vent that has gone off, and the hurler's lob on its way.
                        for (int i = 0; i < 26; i++) g.Disc(vent.X - 3 + (int)(V(i, 1, 106) * 8) + i / 5, vent.Y - 4 - i, 2 + i / 9, Rgba.White.WithAlpha(110 - i * 3));
                        for (int i = 0; i <= 10; i++)
                        {
                            float t = i / 10f;
                            g.Disc((int)(hurler.X - 12 + (hero.X - hurler.X) * t), (int)(hurler.Y - 26 - MathF.Sin(t * MathF.PI) * 26 + 26 * t), i == 5 ? 3 : 0.8f, i == 5 ? C("#FFB030") : Yellow.WithAlpha(190));
                        }
                        Motes(g, 107, 44, i => (i % 2 == 0 ? C("#FFC040") : C("#FF6A20")).WithAlpha(230), 2);
                    };
                    break;
                }
                case "lake":
                {
                    s.Lights.Add((7 * T, 5 * T, 80, look.Light, 0.35f));
                    s.Lights.Add((16 * T + 8, 6 * T, 44, look.Light, 0.3f));
                    s.Put(G("SUNKEN SHIP"), 6.5f, 4, 0, 6, 0);
                    s.Put(G("CAP ISLE"), 7.5f, 6, 0, 4);
                    foreach (var (x, y, ox) in new[] { (5f, 2f, 0), (6f, 2f, 4), (9f, 8f, 0), (3f, 5f, 0), (3f, 6f, 4), (11f, 6f, 0), (15f, 4f, 2), (7f, 8f, -4), (17f, 7f, 0), (11f, 4f, -2) }) s.Put(x % 2 == 0 ? G("REEDS").FlippedX() : G("REEDS"), x, y, ox, 2, 6);
                    s.Put(G("CORAL"), 12, 8, 0, 4);
                    s.Put(G("CORAL").FlippedX(), 14, 2, 0, 0);
                    s.Put(G("PEARL CLAM"), 13, 3, 4, 2);
                    s.Put(G("PEARL CLAM"), 5, 9, 0, 2);
                    s.Put(G("WRECK"), 15.5f, 9, 0, 2);
                    foreach (var (x, y) in new[] { (16f, 5f), (9f, 4f), (5f, 6f), (10f, 6f) }) s.Put(G("LILY PAD"), x, y, 2, -3, flat: true);
                    foreach (var (x, y) in new[] { (6f, 6f), (9f, 5f), (16f, 6f) }) s.Put(Cr(0), x, y, 4, 0, flat: true, alpha: 0.7f);
                    var tentacle = Monster("TENTACLE", 11, 5, 0, -6);
                    Monster("GREAT CRAB", 13, 6, 6, 2);
                    Monster("LAKE CRAB", 3, 9, 0, 0);
                    s.Put(Cr(1), 12, 9, 6, 4, 4);
                    foreach (var (x, y) in new[] { (2f, 3f), (14f, 8f), (18f, 3f) }) s.Glow(s.Put(Cr(2), x, y, 0, 0, 0, 9), C("#B0FFF0"), 12, 0.7f, 1);
                    hero = s.Put(Hero(), 12, 3, -4, 6);
                    s.Glow(hero, warm, 56, 0.35f, 8);
                    s.Under = g => g.Ring(tentacle.X, tentacle.Y - 2, 26, Red.WithAlpha(120));
                    s.Over = g => Motes(g, 108, 34, i => C("#D0FFF4").WithAlpha(i % 2 == 0 ? 120 : 70), 1, true);
                    break;
                }
                default:
                {
                    s.Glow(s.Put(G("BROKEN GATE"), 14, 3, 0, 6), look.Accent, 70, 0.9f, 22);
                    s.Put(G("FLOAT ROCK"), 4.5f, 8, 0, 0, 16, 9);
                    s.Put(G("FLOAT ROCK").FlippedX(), 10, 6, 0, 0, 0, 4);
                    s.Put(G("BROKEN COLUMN"), 2, 2, 4, 2);
                    s.Put(G("BROKEN COLUMN").FlippedX(), 16, 7, -6, -6);
                    s.Glow(s.Put(G("VOID CRYSTAL"), 6, 1, -2, 4), look.Accent, 30, 0.8f, 5);
                    s.Glow(s.Put(G("VOID CRYSTAL").FlippedX(), 3, 9, 0, 4), look.Accent, 30, 0.8f, 5);
                    s.Glow(s.Put(G("VOID CRYSTAL"), 15, 9, 0, 0), look.Accent, 30, 0.8f, 5);
                    s.Put(G("OBELISK"), 12, 9, 0, 0, 6, 4);
                    s.Put(G("OBELISK"), 1, 3, 4, 0, 6, 5);
                    var stake = s.Put(G("CHAIN STAKE"), 6, 7, 0, 2);
                    s.Put(G("TWIST VINE"), 14, 6, -2, 0);
                    s.Put(G("TWIST VINE").FlippedX(), 6, 3, 4, 0);
                    Monster("VOID EYE", 3, 6, 6, 4);
                    Monster("STALKER", 11, 2, 4, 4, true);
                    Monster("VOID LARVA", 5, 2, 0, 0);
                    Monster("VOID LARVA", 5, 2, 12, 5);
                    foreach (var (x, y) in new[] { (9f, 1f), (11f, 6f), (18f, 6f), (1f, 5f) }) s.Glow(s.Put(Cr(0), x, y, 0, 0, 0, 6), look.Extra, 14, 0.5f, 2);
                    hero = s.Put(Hero(), 7, 3, 4, 0);
                    s.Glow(hero, warm, 40, 0.5f, 8);
                    s.Under = g => g.Line(stake.X + 4, stake.Y - 9, 5 * T + 2, 8 * T - 4, C("#8A84A0"), 1, 2);
                    s.Over = g =>
                    {
                        g.Ring(hero.X + 22, hero.Y - 2, 9, Red.WithAlpha(200));
                        Motes(g, 109, 60, i => (i % 4 == 0 ? look.Accent : look.Extra).WithAlpha(i % 2 == 0 ? 230 : 120), 3);
                    };
                    break;
                }
            }
            s.Scatter(i => cover[i % cover.Length], 10f, 15, 200 + look.Id.Length * 7 + look.Id[0]);
            return s;
        }

        // ---- A biome's board ----

        static PixelImage Board(BiomeLook look, PixelImage scene)
        {
            var props = BiomeMockArt.Props(look.Id);
            var critters = BiomeMockArt.Critters(look.Id);
            var roster = BiomeMockArt.Roster(look.Id);
            const int gap = 6;
            int slotTotal = 22 + roster.Sum(m => Math.Max(m.Img.Width, MockFont.SmallWidth(m.Name)) + gap);
            int w = Math.Max(604, slotTotal + 16), right = 334, rw = w - right - 6;
            var g = Canvas(w, 358);
            g.Text(6, 5, look.Name, Text, Back);
            g.Small(12 + MockFont.Width(look.Name), 7, look.Concept, Dim);
            g.Draw(scene, 6, 18);

            // The palette, and the light.
            int y = 18;
            Label(g, right, y, "PALETTE", Dim);
            int cw = 38;
            for (int i = 0; i < look.Chips.Length; i++)
            {
                g.Fill(right + i * (cw + 3), y + 8, cw, 12, C(look.Chips[i].Hex));
                Frame(g, right + i * (cw + 3), y + 8, cw, 12, C("#000000").WithAlpha(120));
                Label(g, right + i * (cw + 3), y + 22, look.Chips[i].Hex.Substring(1), Text);
                Label(g, right + i * (cw + 3), y + 29, look.Chips[i].Role, Dim);
            }
            int lx = right + look.Chips.Length * (cw + 3) + 4;
            g.Glow(lx + 10, y + 14, 12, look.Light, 255);
            g.Disc(lx + 10, y + 14, 4, look.Light);
            Label(g, lx, y + 22, look.Light.R.ToString("X2") + look.Light.G.ToString("X2") + look.Light.B.ToString("X2"), Text);
            Label(g, lx, y + 29, "LIGHT", Dim);

            // The props, tier by tier, on the biome's floor.
            y += 40;
            y = Tier(g, look, right, y, rw, 16, ("COVER  8-12 / 100 TILES, WALK THROUGH", props.Where(p => p.Tier == "cover").ToList()), ("CRITTERS", critters));
            y = Tier(g, look, right, y, rw, 38, ("MEDIUM  3-5 / 100 TILES", props.Where(p => p.Tier == "medium").ToList()), default);
            y = Tier(g, look, right, y, rw, 56, ("LARGE  0.5-1 / 100, FOOT BLOCKS", props.Where(p => p.Tier == "large").ToList()), ("LANDMARK  ROOMS ONLY", props.Where(p => p.Tier == "landmark").ToList()));
            Label(g, 6, 216, "RULE:", Yellow);
            Label(g, 30, 216, look.Hazard, Text);
            Label(g, 6, 224, "DUNGEON (5C):", Yellow);
            Label(g, 62, 224, look.Dungeon, Text);
            Label(g, 6, 240, "SIZES: XS 8-10PX   S 12-14   M 14-18   L 24-32 (2X2)   XL 40-56 (3X3)", Dim);

            // The monsters at their sizes, beside the player.
            int top = 262, band = 64, baseY = top + band - 4;
            g.Draw(Crop(BM.Light(Floor(look, (w - 12) / T + 1, 4), look.Light, look.Ambient, new List<(int, int, int, Rgba, float)>()), 0, 0, w - 12, band), 6, top);
            int x = 10;
            var hero = Hero();
            Shadow(g, x + 8, baseY - 1, 10);
            g.Draw(hero, x, baseY - hero.Height);
            Centre(g, x + 8, baseY + 7, "YOU", Text);
            x += 22;
            foreach (var m in roster)
            {
                int slot = Math.Max(m.Img.Width, MockFont.SmallWidth(m.Name)) + gap, cx = x + slot / 2;
                Shadow(g, cx, baseY - 1, m.Lift > 0 ? (int)(m.Img.Width * 0.55f) : (int)(m.Img.Width * 0.8f), m.Lift > 0 ? 80 : 105);
                g.Draw(m.Img, cx - m.Img.Width / 2, baseY - m.Lift - m.Img.Height);
                Centre(g, cx, baseY + 7, m.Size, TagColour[m.Tag]);
                Centre(g, cx, baseY + 14, m.Name, TagColour[m.Tag]);
                x += slot;
            }
            int kx = 6;
            for (int i = 0; i < TagName.Length; i++)
            {
                g.Fill(kx, 248, 5, 5, TagColour[i]);
                Label(g, kx + 7, 248, TagName[i], TagColour[i]);
                kx += MockFont.SmallWidth(TagName[i]) + 16;
            }
            return g;
        }

        static int Tier(PixelImage g, BiomeLook look, int x, int y, int width, int tall, (string Title, List<BiomeMockArt.Prop> Items) a, (string Title, List<BiomeMockArt.Prop> Items) b)
        {
            int bandTop = y + 7, baseY = bandTop + tall + 2;
            g.Fill(x, bandTop, width, tall + 4, look.Floor.Scale(look.Id == "abyss" || look.Id == "lava" ? 1.5f : 1f));
            int cx = x + 3;
            foreach (var (title, items) in new[] { a, b })
            {
                if (items == null) continue;
                Label(g, cx, y, title, Dim);
                int start = cx;
                foreach (var p in items)
                {
                    int slot = Math.Max(p.Img.Width, MockFont.SmallWidth(p.Name)) + 5, mid = cx + slot / 2;
                    Shadow(g, mid, baseY - 1, (int)(p.Img.Width * 0.75f));
                    g.Draw(p.Img, mid - p.Img.Width / 2, baseY - p.Img.Height);
                    Centre(g, mid, baseY + 4, p.Name, Text);
                    cx += slot;
                }
                cx = Math.Max(cx, start + MockFont.SmallWidth(title) + 6) + 8;
            }
            return baseY + 12;
        }

        // ---- Fences ----

        static string MaskName(int mask) => mask == 0 ? "ALONE" : (((mask & 1) != 0 ? "N" : "") + ((mask & 2) != 0 ? "E" : "") + ((mask & 4) != 0 ? "S" : "") + ((mask & 8) != 0 ? "W" : ""));

        static void FenceCell(PixelImage g, PixelImage cell, int x, int y, bool shadow = true)
        {
            if (shadow) Shadow(g, x + 8, y + FenceMock.PostFoot, 7);
            g.Draw(cell, x, y - FenceMock.Head);
        }

        static PixelImage Fences()
        {
            var cave = BiomeMockArt.Look("cave");
            var g = Canvas(606, 600);
            g.Text(6, 5, "FENCES THAT JOIN", Text, Back);
            g.Small(110, 7, "4 NEIGHBOUR BITS (N1 E2 S4 W8) - 16 SHAPES FROM 5 PIECES - 3/4 VIEW, NOT ROTATED", Dim);

            // The five pieces, and how they stack.
            int y = 20;
            Label(g, 6, y, "THE 5 PIECES (DRAWN IN THIS ORDER: N STRIP, POST, E + W RAILS, S STRIP)", Yellow);
            g.Draw(Floor(cave, 24, 2), 6, y + 8);
            string[] pieceNames = { "POST", "E RAILS", "W RAILS", "N STRIP", "S STRIP" };
            int[] order = { 3, 0, 1, 2, 4 };
            for (int i = 0; i < 5; i++)
            {
                FenceCell(g, FenceMock.Piece(order[i]), 14 + i * 34, y + 18, false);
                Centre(g, 22 + i * 34, y + 43, pieceNames[order[i]], Text);
                if (i < 4) Label(g, 36 + i * 34, y + 24, "+", Dim);
            }
            Label(g, 182, y + 24, ">", Dim);
            int[] examples = { 10, 5, 6, 12, 7, 15 };
            for (int i = 0; i < examples.Length; i++)
            {
                FenceCell(g, FenceMock.Wood(examples[i]), 196 + i * 32, y + 18);
                Centre(g, 204 + i * 32, y + 43, MaskName(examples[i]), Text);
            }

            // The 16 shapes of each kind.
            y += 56;
            foreach (var (kind, title) in new[] { ('F', "WOOD FENCE - POST + TWO RAILS. E-W SHOWS THE RAILS' FACES, N-S ONLY THEIR TOPS (THINNER, DARKER)"), ('H', "HEDGE - NO POST, A LEAF MASS, LIT TOP, ROUND ENDS"), ('S', "STONE WALL - LOW AND THICK, LIGHT TOP"), ('I', "IRON RAILING / GRATE - THIN BARS AND SPEAR TIPS") })
            {
                Label(g, 6, y, title, Yellow);
                g.Draw(Floor(cave, 24, 2), 6, y + 8);
                for (int mask = 0; mask < 16; mask++)
                {
                    FenceCell(g, FenceMock.Cell(kind, mask), 10 + mask * 24, y + 18, kind == 'F' || kind == 'I');
                    Centre(g, 18 + mask * 24, y + 43, mask.ToString(), Dim);
                    Centre(g, 18 + mask * 24, y + 50, MaskName(mask), Text);
                }
                y += 62;
            }

            // Ends and worn pieces, then the gates.
            Label(g, 6, y, "ENDS (ONE NEIGHBOUR: TALLER ROUND HEAD)              WORN, FOR SET PIECES ONLY", Yellow);
            g.Draw(Floor(cave, 24, 2), 6, y + 8);
            int[] ends = { 2, 8, 4, 1, 0 };
            for (int i = 0; i < ends.Length; i++)
            {
                FenceCell(g, FenceMock.Wood(ends[i]), 12 + i * 26, y + 18);
                Centre(g, 20 + i * 26, y + 43, i == 4 ? "STAKE" : "END " + MaskName(ends[i]), Text);
            }
            Compose(g, FenceMock.Layout('F', new[] { "FwF" }, 216, y + 18));
            Centre(g, 240, y + 43, "A RAIL GONE", Text);
            Compose(g, FenceMock.Layout('F', new[] { "FlF" }, 300, y + 18));
            Centre(g, 324, y + 43, "A POST LEANS", Text);
            y += 54;
            Label(g, 6, y, "GATES - ONLY IN A STRAIGHT RUN. OPEN: THE LEAF STANDS ON ONE POST (EDGE-ON IN AN E-W RUN, FACE-ON IN A N-S RUN)", Yellow);
            g.Draw(Floor(cave, 24, 4), 6, y + 8);
            Compose(g, FenceMock.Layout('F', new[] { "FGF" }, 16, y + 28));
            Centre(g, 40, y + 60, "E-W SHUT", Text);
            Compose(g, FenceMock.Layout('F', new[] { "FOF" }, 96, y + 28));
            Centre(g, 120, y + 60, "E-W OPEN", Text);
            Compose(g, FenceMock.Layout('F', new[] { "F", "G", "F" }, 200, y + 16));
            Centre(g, 208, y + 66, "N-S SHUT", Text);
            Compose(g, FenceMock.Layout('F', new[] { "F", "O", "F" }, 264, y + 16));
            Centre(g, 272, y + 66, "N-S OPEN", Text);
            Compose(g, FenceMock.Layout('F', new[] { "FF", "GG", "FF" }, 320, y + 16));
            Centre(g, 336, y + 66, "TWO = DOUBLE", Text);

            // The same layout in each kind, with someone behind and in front.
            int dx = 444, dy = 20;
            string[] layout = { "FFFFFFFF", "F...F..F", "F...FFGF", "FFFFF...", "....F...", "....F..." };
            foreach (var (kind, title) in new[] { ('F', "WOOD"), ('H', "HEDGE"), ('S', "STONE WALL"), ('I', "IRON") })
            {
                Label(g, dx, dy, title + ": CORNERS, T, CROSS, ENDS", Yellow);
                g.Draw(Floor(cave, 9, 7), dx, dy + 8);
                var rows = kind == 'F' ? layout : layout.Select(r => r.Replace('G', 'F')).ToArray();
                var objects = FenceMock.Layout(kind, rows, dx + 8, dy + 20);
                objects.Add(Obj(Hero(), dx + 8 + 2 * T + 8, dy + 20 + 3 * T + 6));
                objects.Add(Obj(Hero(flip: true), dx + 8 + 6 * T + 8, dy + 20 + 4 * T + 8));
                Compose(g, objects);
                dy += 126;
            }

            // Before and after: a 6 by 5 pen.
            y += 84;
            Label(g, 6, y, "BEFORE - TODAY'S ONE FENCE TILE, ON THE TILE LAYER", Red);
            Label(g, 6, y + 7, "N-S RUN = E-W PIECES STACKED. PLAYER ALWAYS ON TOP", Dim);
            Label(g, 222, y, "AFTER - JOINED, 90 DEG CORNERS, FEET-SORTED, SHADOWS", Green);
            Label(g, 222, y + 7, "LEGS HIDE BEHIND THE TOP RUN. TWO GATES", Dim);
            string[] pen = { "FFFFFF", "F....F", "F....O", "F....F", "FFGFFF" };
            var before = Floor(cave, 12, 9, true);
            var old = TileArt.Object(TileType.Fence);
            for (int j = 0; j < pen.Length; j++)
                for (int i = 0; i < pen[j].Length; i++)
                    if (pen[j][i] == 'F') before.Draw(old, (3 + i) * T, (2 + j) * T);
            foreach (var (hx, hy) in new[] { (5 * T, 2 * T + 5), (7 * T + 8, 7 * T + 2), (5 * T + 8, 5 * T) }) before.Draw(Hero(), hx - 8, hy - Hero().Height);
            g.Draw(before, 6, y + 16);
            var after = Floor(cave, 12, 9, true);
            var things = FenceMock.Layout('F', pen, 3 * T, 2 * T);
            things.Add(Obj(Hero(), 5 * T, 2 * T + 5));
            things.Add(Obj(Hero(), 7 * T + 8, 7 * T + 2));
            things.Add(Obj(Hero(), 5 * T + 8, 5 * T));
            Compose(after, things);
            g.Draw(after, 222, y + 16);
            return g;
        }

        // ---- The rules for standing things ----

        static PixelImage Depth()
        {
            var cave = BiomeMockArt.Look("cave");
            var mush = BiomeMockArt.Look("mushroom");
            var caveProps = BiomeMockArt.Props("cave");
            var mushProps = BiomeMockArt.Props("mushroom");
            var crystalProps = BiomeMockArt.Props("crystal");
            PixelImage Find(List<BiomeMockArt.Prop> list, string name) => list.First(p => p.Name.StartsWith(name)).Img;
            var tree = Find(mushProps, "MUSH TREE");
            var boulder = Find(caveProps, "BOULDER");
            var cluster = Find(crystalProps, "CLUSTER");
            var crate = Trim(PropArt.Prop(TileType.Crates));
            var lamp = Trim(PropArt.Prop(TileType.LampPost));
            var g = Canvas(620, 506);
            g.Text(6, 5, "STANDING THINGS ARE OBJECTS, NOT TILES", Text, Back);

            // 1. Footprint against the picture's box.
            int x = 6, y = 20;
            Label(g, x, y, "1 FOOTPRINT (YELLOW, WHAT BLOCKS) VS PICTURE (CYAN). PIVOT (RED) = MIDDLE OF THE FOOTPRINT'S BOTTOM EDGE", Yellow);
            var floor = Floor(mush, 19, 5);
            Grid(floor, 0, 0, 19, 5, Rgba.White.WithAlpha(22));
            var list = new List<MockObj>();
            var marks = new List<(int X, int Y, int W, PixelImage Img, string Name)>();
            void Stand(PixelImage img, int tx, int wide, string name)
            {
                int fx = tx * T + wide * T / 2, fy = 4 * T + 14;
                floor.BlendFill(tx * T, 4 * T, wide * T, T, Yellow.WithAlpha(70));
                Frame(floor, tx * T, 4 * T, wide * T, T, Yellow);
                list.Add(Obj(img, fx, fy));
                marks.Add((fx, fy, wide, img, name));
            }
            Stand(tree, 1, 2, "TREE 2X2");
            Stand(boulder, 5, 2, "BOULDER 2X2");
            Stand(cluster, 9, 1, "CLUSTER 1X1");
            Stand(crate, 12, 1, "CRATES 1X1");
            Stand(lamp, 15, 1, "LAMP 1X1");
            Compose(floor, list);
            foreach (var m in marks)
            {
                Frame(floor, m.X - m.Img.Width / 2, m.Y - m.Img.Height, m.Img.Width, m.Img.Height, Cyan.WithAlpha(200), 2);
                floor.Fill(m.X - 1, m.Y - 1, 2, 2, Red);
                floor.Small(m.X - MockFont.SmallWidth(m.Name) / 2, 4 * T - 4 - Math.Max(m.Img.Height, 22) - 6, m.Name, Text, Back);
            }
            g.Small(x + 19 * T + 8, y + 20, "A 2X2 TREE BLOCKS ONLY ITS BOTTOM ROW:", Text);
            g.Small(x + 19 * T + 8, y + 28, "YOU WALK BEHIND THE CAP, AND THE CAP FADES (4).", Text);
            g.Small(x + 19 * T + 8, y + 40, "BLOCKING IS THE FOOTPRINT, NEVER THE PICTURE.", Text);
            g.Small(x + 19 * T + 8, y + 52, "FLAT THINGS (MOSS, FROST, RIPPLES) STAY IN THE FLOOR.", Dim);
            g.Draw(floor, x, y + 8);

            // 2. Shadow and contact shading.
            y += 96;
            Label(g, x, y, "2 SHADOW (OVAL UNDER THE FEET, CRISP, NEVER CAST SIDEWAYS) AND CONTACT SHADING (DARK BOTTOM ROW)", Yellow);
            var floor2 = Floor(cave, 37, 3);
            var rock = R("#6A5440");
            var bare = new Sp(20, 16).Ell(10f, 10f, 10f, 10f, rock).Ell(7f, 7f, 6f, 6f, rock).I;
            var spike = new Sp(9, 21).Spike(4.5f, 21f, 4.5f, 4f, 0f, R("#7A5E44")).I;
            string[] names = { "NOTHING: IT FLOATS", "+ SHADOW", "+ OUTLINE AND CONTACT ROW", "A FLOATING THING: SHADOW APART, X0.7" };
            for (int i = 0; i < 4; i++)
            {
                int cx = 60 + i * 148, by = 40;
                var a = i < 2 ? bare : bare.Outlined(Ink);
                var b = i < 2 ? spike : spike.Outlined(Ink);
                if (i == 3)
                {
                    Shadow(floor2, cx, by - 1, 12, 80);
                    floor2.Draw(Find(BiomeMockArt.Props("abyss"), "FLOAT ROCK"), cx - 15, by - 9 - 26);
                }
                else
                {
                    if (i > 0)
                    {
                        Shadow(floor2, cx - 6, by - 1, 17);
                        Shadow(floor2, cx + 18, by - 1, 8);
                    }
                    floor2.Draw(a, cx - 6 - a.Width / 2, by - a.Height);
                    floor2.Draw(b, cx + 18 - b.Width / 2, by - b.Height);
                }
                floor2.Small(cx - 50, 2, names[i], Text, Back);
            }
            g.Draw(floor2, x, y + 8);

            // 3. Sorting by the feet.
            y += 64;
            Label(g, x, y, "3 SORTED BY THE FEET'S Y, TO THE PIXEL: LOWER ON SCREEN IS IN FRONT", Yellow);
            var floor3 = Floor(mush, 19, 5);
            var sorted = new List<MockObj>();
            void Pair(int cx, int propY, int heroY, PixelImage prop, string text)
            {
                sorted.Add(Obj(prop, cx, propY));
                sorted.Add(Obj(Hero(), cx + 5, heroY));
                floor3.Line(cx - 22, propY, cx + 26, propY, Yellow.WithAlpha(150), 1, 2);
                floor3.Line(cx - 22, heroY, cx + 26, heroY, Cyan.WithAlpha(150), 1, 2);
                floor3.Small(cx - 30, 72, text, Text, Back);
            }
            Pair(34, 56, 64, boulder, "IN FRONT");
            Pair(104, 62, 54, boulder, "BEHIND: HIDDEN");
            Pair(180, 60, 66, tree, "IN FRONT OF TRUNK");
            Pair(260, 64, 58, tree, "BEHIND TRUNK");
            Compose(floor3, sorted);
            g.Draw(floor3, x, y + 8);

            // 4. The canopy fading.
            int px = 330;
            Label(g, px, y, "4 CANOPY FADES TO 45% ONLY WHEN FEET ARE", Yellow);
            Label(g, px, y + 7, "  INSIDE ITS BOX AND BEHIND THE TRUNK", Yellow);
            var floor4 = Floor(mush, 17, 5);
            var cap = BiomeMockArt.MushTree(50, 24, 11, 22, R("#3E6AC8"), R("#D8D0B0"), C("#CFE4FF"), 2, C("#2A4A90"));
            var stem = BiomeMockArt.MushTree(50, 24, 11, 22, R("#3E6AC8"), R("#D8D0B0"), C("#CFE4FF"), 1);
            string[] fadeNames = { "BEHIND: FADED", "BESIDE: SOLID", "A MONSTER TOO" };
            for (int i = 0; i < 3; i++)
            {
                int cx = 44 + i * 88, by = 70;
                var here = new List<MockObj> { Obj(stem, cx, by, 12) };
                if (i == 0) here.Add(Obj(Hero(), cx + 12, by - 14));
                if (i == 1) here.Add(Obj(Hero(), cx + 32, by - 2));
                if (i == 2) here.Add(Obj(BiomeMockArt.Old("myconid"), cx - 12, by - 12));
                Compose(floor4, here);
                DrawAlpha(floor4, cap, cx - cap.Width / 2, by - cap.Height, i == 1 ? 1f : 0.45f);
                if (i != 1) Frame(floor4, cx - 25, by - 46, 50, 34, Cyan.WithAlpha(120), 2);
                floor4.Small(cx - 26, 73, fadeNames[i], Text, Back);
            }
            g.Draw(floor4, px, y + 16);

            // 5. Variation.
            y += 100;
            Label(g, x, y, "5 VARIATION FROM THE TILE'S HASH: FLIP 50%, 2-4 SHAPES, RAMP ONE STEP 20%, +-1PX SIZE (SMALL PLANTS), +-3PX PLACE", Yellow);
            var floor5 = Floor(mush, 24, 4);
            Grid(floor5, 0, 0, 24, 4, Rgba.White.WithAlpha(22));
            var caps = Find(mushProps, "GLOW CAPS");
            var pillar = Find(mushProps, "PILLAR CAP");
            var vary = new List<MockObj>();
            var variants = new (PixelImage Img, string Name)[]
            {
                (caps, "BASE"), (caps.FlippedX(), "FLIP"), (Tinted(caps, Rgba.White, 0.18f), "LIGHTER"), (Tinted(caps, C("#102010"), 0.25f), "DARKER"),
                (BiomeMockArt.Tuft(R("#5E8A3A"), 7, 6, 2, C("#B8F25A")), "TUFT"), (BiomeMockArt.Tuft(R("#5E8A3A"), 8, 7, 9, C("#B8F25A")), "+1PX"), (BiomeMockArt.Tuft(R("#5E8A3A"), 6, 5, 5, C("#B8F25A")), "-1PX"),
                (pillar, "SHAPE A"), (BiomeMockArt.MushTree(11, 7, 3, 12, R("#E88AB0"), R("#D8D0B0"), C("#FFD8E8"), 0, C("#A04A70")), "SHAPE B"), (BiomeMockArt.MushTree(15, 9, 4, 19, R("#D878A0"), R("#D8D0B0"), C("#FFD8E8"), 0, C("#A04A70")), "SHAPE C"),
            };
            for (int i = 0; i < variants.Length; i++)
            {
                int ox = (int)(V(i, 1, 120) * 7) - 3, oy = (int)(V(i, 2, 120) * 5) - 2;
                vary.Add(Obj(variants[i].Img, 18 + i * 37 + ox, 44 + oy));
                floor5.Small(4 + i * 37, 54, variants[i].Name, Text, Back);
            }
            Compose(floor5, vary);
            g.Draw(floor5, x, y + 8);

            // 6. Poisson against a grid.
            y += 80;
            Label(g, x, y, "6 PLACING: A GRID (LEFT) READS AS DOTS. POISSON DISCS IN CLUMPS, MORE NEAR WALLS, ROADS AND DOORS KEPT CLEAR (RIGHT)", Yellow);
            string[] room =
            {
                "##################", "#................#", "#................#", "#.................", "#.................", "#................#", "#................#", "##################",
            };
            var gridScene = new MockScene(mush, room);
            var cover = mushProps.Where(p => p.Tier == "cover").Select(p => p.Img).ToArray();
            var medium = mushProps.Where(p => p.Tier == "medium").Select(p => p.Img).ToArray();
            for (int j = 1; j < 7; j += 1)
                for (int i = 1; i < 17; i += 2) gridScene.Put((i + j) % 6 == 1 ? medium[(i + j) % medium.Length] : cover[(i / 2 + j) % cover.Length], i, j, 0, -3);
            g.Draw(gridScene.Render(true, false), x, y + 8);
            var poisson = new MockScene(mush, room);
            poisson.Put(Find(mushProps, "MUSH TREE"), 3.5f, 2, 0, 4);
            foreach (var (tx, ty) in new[] { (2f, 5f), (3f, 6f), (2f, 6f), (12f, 1f), (13f, 2f), (14f, 1f), (9f, 6f), (15f, 6f) }) poisson.Put(medium[(int)(tx + ty) % medium.Length], tx, ty, (int)(V((int)tx, (int)ty, 121) * 9) - 4, (int)(V((int)tx, (int)ty, 122) * 7) - 3);
            // The road through the middle stays clear: scatter on the rows above and below only.
            var keep = room.Select((r, j) => j == 3 || j == 4 ? r.Replace('.', ' ') : r).ToArray();
            var scatterScene = new MockScene(mush, keep);
            scatterScene.Objs.AddRange(poisson.Objs);
            scatterScene.Scatter(i => cover[i % cover.Length], 16f, 13, 310);
            poisson.Objs.Clear();
            poisson.Objs.AddRange(scatterScene.Objs);
            var right = poisson.Render(true, false);
            right.Line(0, 4 * T, 18 * T, 4 * T, Yellow.WithAlpha(110), 1, 3);
            right.Small(6 * T, 4 * T - 7, "ROAD: KEPT CLEAR", Yellow, Back);
            g.Draw(right, x + 300, y + 8);
            return g;
        }

        // ---- The palettes, today against proposed ----

        static PixelImage ComparePalettes(Dictionary<string, MockScene> scenes, Dictionary<string, PixelImage> lit)
        {
            var g = Canvas(790, 640);
            g.Text(6, 5, "DECISION 1 - HOW FAR TO CHANGE THE BIOMES' COLOURS", Text, Back);
            g.Small(6, 16, "(A) LEAVE THE GROUND, ADD LIGHT + PROPS   (B, RECOMMENDED) NEW PALETTES FOR THE THREE PURPLES, REFINE THE OTHER FIVE   (C) ALL NEW", Dim);
            int cw = 16 * T, ch = 7 * T;
            Label(g, 6, 28, "THE THREE PURPLES: TODAY THEY SHARE A DARK PURPLE FLOOR. PROPOSED: OLIVE MOSS / BLUE-GREY SLATE / PURPLE-BLACK (PURPLE BELONGS TO THE ABYSS)", Yellow);
            string[] purple = { "mushroom", "crystal", "abyss" };
            string[] rowNames = { "TODAY", "NEW PALETTE (GROUND ONLY)", "NEW + COLOURED LIGHT + PROPS" };
            for (int i = 0; i < 3; i++)
            {
                var look = BiomeMockArt.Look(purple[i]);
                int x = 6 + i * (cw + 6), oy = (purple[i] == "abyss" ? 1 : 0) * T;
                var today = Crop(BiomeGround.Today(look, look.Map), 2 * T, oy, cw, ch);
                var bare = Crop(scenes[look.Id].Render(false, false), 2 * T, oy, cw, ch);
                var full = Crop(lit[look.Id], 2 * T, oy, cw, ch);
                var rows = new[] { today, bare, full };
                for (int r = 0; r < 3; r++)
                {
                    int y = 38 + r * (ch + 12);
                    Label(g, x, y, look.Name + " - " + rowNames[r], r == 0 ? Red : Green);
                    g.Draw(rows[r], x, y + 7);
                    if (r < 2)
                    {
                        string hex = r == 0 ? look.TodayFloor : look.Chips[0].Hex;
                        g.Fill(x + cw - 50, y + 9, 48, 9, C(hex));
                        g.Small(x + cw - 48, y + 11, hex.Substring(1), Text, Back);
                    }
                }
            }
            int y2 = 38 + 3 * (ch + 12) + 6;
            Label(g, 6, y2, "THE OTHER FIVE: TODAY (TOP) AND REFINED (BOTTOM) - SAME HUE FAMILY, OWN FLOOR PATTERN AND WALL SHAPE, LIGHT AND PROPS", Yellow);
            string[] rest = { "cave", "ice", "sand", "lava", "lake" };
            int sw = 9 * T + 8, sh = 6 * T;
            for (int i = 0; i < 5; i++)
            {
                var look = BiomeMockArt.Look(rest[i]);
                int x = 6 + i * (sw + 5), ox = (rest[i] == "lava" ? 4 : rest[i] == "lake" ? 1 : 1) * T;
                Label(g, x, y2 + 10, look.Name, Text);
                g.Draw(Crop(BiomeGround.Today(look, look.Map), ox, T, sw, sh), x, y2 + 17);
                g.Fill(x + sw - 30, y2 + 19, 28, 9, C(look.TodayFloor));
                g.Small(x + sw - 28, y2 + 21, look.TodayFloor.Substring(1), Text, Back);
                g.Draw(Crop(lit[look.Id], ox, T, sw, sh), x, y2 + 21 + sh);
                g.Fill(x + sw - 30, y2 + 23 + sh, 28, 9, C(look.Chips[0].Hex));
                g.Small(x + sw - 28, y2 + 25 + sh, look.Chips[0].Hex.Substring(1), Text, Back);
            }
            return g;
        }

        // ---- The size classes ----

        static PixelImage Sizes()
        {
            var cave = BiomeMockArt.Look("cave");
            var ice = BiomeMockArt.Roster("ice");
            var caveMons = BiomeMockArt.Roster("cave");
            var g = Canvas(560, 566);
            g.Text(6, 5, "DECISION 3 - MONSTER SIZE CLASSES", Text, Back);
            g.Small(6, 16, "HIT CIRCLE = 60-70% OF THE PICTURE. PICTURES AT WHOLE SCALES ONLY (L AND XL ARE DRAWN BIG, NEVER STRETCHED)", Dim);

            // The line-up on a tile grid.
            var floor = Floor(cave, 34, 5);
            Grid(floor, 0, 0, 34, 5, Rgba.White.WithAlpha(26));
            var line = new (string Size, string Spec, PixelImage Img, float Radius, int Tiles)[]
            {
                ("YOU", "16X24", Hero(), 0.35f, 2),
                ("XS", "8-10PX R0.20-0.25  HP X0.4 SPEED X1.4  PACKS OF 4-8", caveMons.First(m => m.Name == "CAVE RAT").Img, 0.22f, 5),
                ("S", "12-14PX R0.28-0.35  HP X0.7  3-5", BiomeMockArt.Roster("mushroom").First(m => m.Name == "PUFFBALL").Img, 0.32f, 4),
                ("M", "14-18PX R0.38-0.45  TODAY'S SIZE  2-4", ice.First(m => m.Name == "FROST WOLF").Img, 0.42f, 5),
                ("L 2X2", "24-32PX R0.7-0.9  HP X4 SPEED X0.7  1-2", ice.First(m => m.Name == "YETI").Img, 0.8f, 6),
                ("XL 3X3", "40-56PX R1.2-1.4  HP X12 SPEED X0.5  ALONE", ice.First(m => m.Name == "FROST MAMMOTH").Img, 1.3f, 7),
            };
            int tx = 1;
            var objs = new List<MockObj>();
            var notes = new List<(int X, string Size, string Spec)>();
            foreach (var e in line)
            {
                int cx = tx * T + e.Tiles * T / 2 - 8, fy = 4 * T + 4;
                int r = (int)MathF.Round(e.Radius * T);
                for (int a = 0; a < 360; a += 4) floor.Blend(cx + (int)MathF.Round(MathF.Cos(a * MathF.PI / 180) * r), fy - 2 + (int)MathF.Round(MathF.Sin(a * MathF.PI / 180) * r * 0.9f), Yellow);
                objs.Add(Obj(e.Img, cx, fy));
                notes.Add((cx, e.Size, e.Spec));
                tx += e.Tiles;
            }
            Compose(floor, objs);
            g.Draw(floor, 6, 28);
            for (int i = 0; i < notes.Count; i++)
            {
                Centre(g, 6 + notes[i].X, 112, notes[i].Size, i == 0 ? Text : Yellow);
                if (i > 0) Label(g, 6, 122 + (i - 1) * 7, notes[i].Size.PadRight(7) + notes[i].Spec, i >= 4 ? Yellow : Text);
            }

            // Corridors.
            int y = 164;
            Label(g, 6, y, "CORRIDORS: A BODY PASSES WHERE WIDTH >= DIAMETER + 0.4. FOUR BIOMES HAVE 2-TILE CORRIDORS; DOORS ARE 1 TILE", Yellow);
            string[] halls =
            {
                "##################################", "#######.......####........###.....", "#.............####........###.....", "#######.......####........###.....", "#######...........................",
                "#######...........................", "#.............####................", "#######.......####........###.....", "#######.......####........###.....", "##################################",
            };
            var hall = new MockScene(cave, halls);
            var ground = hall.Render(false, false);
            var hallObjs = new List<MockObj>();
            void Body(string size, PixelImage img, float radius, float tileX, float tileY, bool fits)
            {
                int cx = (int)(tileX * T), cy = (int)(tileY * T);
                int r = (int)MathF.Round(radius * T);
                for (int a = 0; a < 360; a += 3) ground.Blend(cx + (int)MathF.Round(MathF.Cos(a * MathF.PI / 180) * r), cy + (int)MathF.Round(MathF.Sin(a * MathF.PI / 180) * r), fits ? Green : Red);
                hallObjs.Add(Obj(img, cx, cy + Math.Min(r, 6)));
                ground.Small(cx - 14, cy + r + 3, size + (fits ? " OK" : " STUCK"), fits ? Green : Red, Back);
            }
            var wolf = line[3].Img;
            var yeti = line[4].Img;
            var mammoth = line[5].Img;
            Body("M", wolf, 0.42f, 3.5f, 2.5f, true);
            Body("L", yeti, 0.8f, 3.5f, 6.5f, false);
            Body("L", yeti, 0.8f, 15.5f, 5f, true);
            Body("XL", mammoth, 1.3f, 20.2f, 5f, false);
            Body("XL", mammoth, 1.3f, 27.5f, 5.5f, true);
            Compose(ground, hallObjs);
            ground.Small(1 * T, 1 * T + 2, "1 TILE (A DOOR)", Text, Back);
            ground.Small(14 * T + 2, 3 * T + 2, "2 TILES", Text, Back);
            ground.Small(26 * T + 2, 3 * T + 2, "3 TILES", Text, Back);
            ground.Small(19 * T, 8 * T + 2, "XL CANNOT TAKE THE 2-TILE WAY", Red, Back);
            g.Draw(ground, 6, y + 8);

            // Rooms.
            y += 176;
            Label(g, 6, y, "(A, RECOMMENDED) FIELD PACKS UP TO L 2X2: ROOMS OF RADIUS 5+,", Green);
            Label(g, 6, y + 7, "TIED TO THE ROOM, STOPS AT A NARROW MOUTH AND THROWS", Green);
            Label(g, 286, y, "(B) XL 3X3 IN THE FIELD TOO: NEEDS 7X7 OPEN FLOOR.", Red);
            Label(g, 286, y + 7, "IN (A) XL IS ONLY A DUNGEON'S MASTER OR A SET PIECE", Red);
            string[] room =
            {
                "#################", "####.........####", "###...........###", "##.............##", "##.............##", "#...............#", "#................", "#................", "##.............##", "##.............##", "###...........###", "#################",
            };
            var roomA = new MockScene(cave, room);
            var bigBeetle = caveMons.First(m => m.Name == "BIG BEETLE").Img;
            roomA.Put(bigBeetle, 8, 5, 0, 4);
            foreach (var (rx, ry) in new[] { (4f, 7f), (5f, 8f), (4f, 8f), (6f, 7f), (5f, 6f) }) roomA.Put(caveMons[3].Img, rx, ry, (int)(V((int)rx, (int)ry, 130) * 8), 0);
            roomA.Put(caveMons[1].Img, 12, 3);
            roomA.Put(Hero(flip: true), 14, 7);
            var a1 = roomA.Render(true, false);
            Frame(a1, 7 * T + 8, 4 * T + 2, 2 * T, 2 * T, Yellow, 2);
            a1.Small(7 * T + 4, 4 * T - 6, "L 2X2", Yellow, Back);
            g.Draw(a1, 6, y + 16);
            var roomB = new MockScene(cave, room);
            roomB.Put(BiomeMockArt.Roster("cave").Last().Img, 8, 6, 0, 6);
            roomB.Put(Hero(flip: true), 14, 7);
            var b1 = roomB.Render(true, false);
            Frame(b1, 5 * T, 2 * T + 8, 7 * T, 7 * T, Cyan, 3);
            Frame(b1, 7 * T, 4 * T + 6, 3 * T, 3 * T, Yellow, 2);
            b1.Small(5 * T + 2, 2 * T + 1, "7X7 OPEN", Cyan, Back);
            b1.Small(7 * T, 4 * T - 1, "XL 3X3", Yellow, Back);
            g.Draw(b1, 286, y + 16);
            return g;
        }

        // ---- All eight at a glance ----

        static PixelImage Overview(Dictionary<string, PixelImage> lit)
        {
            int w = 20 * T, h = 12 * T;
            var g = Canvas(4 * (w + 6) + 6, 2 * (h + 16) + 20);
            g.Text(6, 5, "THE EIGHT BIOMES AT A GLANCE", Text, Back);
            for (int i = 0; i < BiomeMockArt.Looks.Length; i++)
            {
                var look = BiomeMockArt.Looks[i];
                int x = 6 + i % 4 * (w + 6), y = 18 + i / 4 * (h + 16);
                g.Text(x, y, look.Name, Text, Back);
                g.Draw(lit[look.Id], x, y + 10);
            }
            return g;
        }
    }
}
