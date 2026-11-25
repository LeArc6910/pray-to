using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Client
{
    // The loading screen's picture (2026-11-25, Docs/TitleRework.md 4-4, 묶음 T2): a place the character has been, as
    // the game draws it. A small cave of one biome is laid here (its floor and walls, its own props and floor cover, a
    // pool in the lake and the lava zone, a monster or two, a small creature) and painted with the game's own art
    // (TileArt, StandArt, CreatureArt, CritterArt) under the game's own dark and light (LightMap, the biome's glow and
    // light colour), then cut to a wide picture. The same biome and seed always give the same picture. Which biomes
    // and monsters to show is the screen's to decide (only what the character has met): this draws what it is given.
    static class LoadingArt
    {
        // The picture's size at one pixel a world pixel: sixteen to nine, ten tiles wide.
        public const int Width = 160, Height = 90;

        const int Px = TileArt.Size;
        // Rock kept round the tiles in view (tiles), so the map's edge is never seen and light has somewhere to end.
        const int Rim = 6;

        // The monsters shown in a biome when none are given: two of its own.
        public static IReadOnlyList<(string Id, Rgba Color)> MonstersOf(Biome biome) => biome switch
        {
            Biome.Mushroom => new[] { ("myconid", Rgba.Hex("#C86ED8")), ("glow_bat", Rgba.Hex("#5FE0C8")) },
            Biome.Ice => new[] { ("frost_wolf", Rgba.Hex("#C4D8EA")), ("ice_slime", Rgba.Hex("#8CC8F0")) },
            Biome.Sand => new[] { ("sand_scorpion", Rgba.Hex("#D2A050")), ("bomb_scarab", Rgba.Hex("#3E9A92")) },
            Biome.Crystal => new[] { ("crystal_tortoise", Rgba.Hex("#7FB8D8")), ("crystal_bat", Rgba.Hex("#7CD2E2")) },
            Biome.Lake => new[] { ("lake_crab", Rgba.Hex("#4AA0B4")), ("mud_slime", Rgba.Hex("#7A6A48")) },
            Biome.Lava => new[] { ("salamander", Rgba.Hex("#E8642A")), ("magma_slime", Rgba.Hex("#E0541E")) },
            Biome.Abyss => new[] { ("void_eye", Rgba.Hex("#8A5ADA")), ("star_slime", Rgba.Hex("#2E2A62")) },
            _ => new[] { ("slime", Rgba.Hex("#8CD94D")), ("cave_beetle", Rgba.Hex("#A0703C")) },
        };

        // A picture of a biome. monsters: who is in it (their ids and colours as the monster sheet gives them; at most
        // three are shown), null for two of the biome's own, empty for none. hero: someone standing in it in that look,
        // null for no one. The light is where the hero stands, or would.
        public static PixelImage Scene(Biome biome, int seed, IReadOnlyList<(string Id, Rgba Color)> monsters = null, CharacterLook? hero = null, int width = Width, int height = Height)
        {
            monsters ??= MonstersOf(biome);
            int salt = seed * 31 + (int)biome * 977;
            uint Roll(int x, int y, int kind) => PixelNoise.Hash(x, y, salt + kind);

            // The tiles in view, and the cave: an open floor with a ragged roof and foot, rock closing in at the corners.
            int tilesWide = (width + Px - 1) / Px + 1, tilesHigh = (height + Px - 1) / Px + 1;
            int left = Rim, bottom = Rim;
            var map = new TileMap(tilesWide + Rim * 2, tilesHigh + Rim * 2);
            int midX = left + tilesWide / 2, midY = bottom + tilesHigh / 2;
            for (int x = left - 2; x < left + tilesWide + 2; x++)
            {
                int fromSide = Math.Min(x - left, left + tilesWide - 1 - x);
                // In runs of two and three tiles, so the rock steps like a cave's and not like a wall's teeth.
                int roof = bottom + tilesHigh - 2 - (int)(Roll(x / 3, 0, 1) % 2) - (fromSide < 1 ? 1 : 0);
                int foot = bottom + (int)(Roll((x + 1) / 2, 0, 2) % 3 == 0 ? 2 : 1) + (fromSide < 2 && Roll(x / 2, 0, 3) % 2 == 0 ? 1 : 0);
                for (int y = foot; y <= roof; y++) map[x, y] = TileType.Floor;
            }
            // A knuckle of rock or two standing in from the roof.
            // (Never over where the hero stands.)
            bool poolLeft = Roll(0, 0, 6) % 2 == 0;
            int standX = midX + (poolLeft ? 1 : -1), standY = midY;
            int knuckle = left + 2 + (int)(Roll(0, 0, 4) % (uint)(tilesWide - 4));
            if (Math.Abs(knuckle - standX) <= 1) knuckle += knuckle < midX ? -3 : 3;
            for (int y = bottom + tilesHigh - 3 - (int)(Roll(0, 0, 5) % 2); y < bottom + tilesHigh; y++) map[knuckle, y] = TileType.Wall;

            // The lake's and the lava zone's pool, at one side.
            var wet = biome == Biome.Lake ? TileType.Water : biome == Biome.Lava ? TileType.Lava : TileType.Floor;
            if (wet != TileType.Floor)
            {
                int px = poolLeft ? left + 1 : left + tilesWide - 5;
                for (int y = bottom + 1; y <= bottom + 3; y++)
                    for (int x = px; x < px + 4; x++)
                    {
                        bool corner = (x == px || x == px + 3) && y == bottom + 3;
                        if (map[x, y] == TileType.Floor && !(corner && Roll(x, y, 7) % 2 == 0)) map[x, y] = wet;
                    }
            }

            // Where the hero stands (or would): the middle, a little to one side, on open floor.
            bool Open(int x, int y) => map.InBounds(x, y) && map[x, y] == TileType.Floor;
            bool Kept(int x, int y) => Math.Abs(x - standX) <= 1 && Math.Abs(y - standY) <= 1;

            // The biome's props: up to five, each on floor with floor at its sides, none beside another.
            var kinds = new List<TileType>();
            foreach (var kind in LavaVoidProps.Any(biome))
                if (kind.Weight > 0) kinds.Add(kind.Type == TileType.UnlitLamp ? TileType.LitLamp : kind.Type);
            var lamps = new List<(int X, int Y)>();
            int placed = 0;
            for (int attempt = 0; attempt < 60 && placed < 5 && kinds.Count > 0; attempt++)
            {
                int x = left + 1 + (int)(Roll(attempt, 1, 8) % (uint)(tilesWide - 2));
                int y = bottom + 1 + (int)(Roll(attempt, 2, 8) % (uint)(tilesHigh - 2));
                var type = kinds[(placed + (int)(Roll(attempt, 3, 8) % 2)) % kinds.Count];
                int wide = Structures.Footprint(type).W;
                bool fits = !Kept(x, y);
                for (int dy = -1; dy <= 1 && fits; dy++)
                    for (int dx = -1; dx <= wide && fits; dx++)
                        fits = Open(x + dx, y + dy);
                if (!fits) continue;
                map[x, y] = type;
                for (int i = 1; i < wide; i++) map[x + i, y] = TileType.StructurePart;
                if (type == TileType.LitLamp) lamps.Add((x, y));
                placed++;
            }

            var image = new PixelImage(tilesWide * Px, tilesHigh * Px);
            image.Fill(0, 0, image.Width, image.Height, Rgba.Hex("#0A0A0F"));
            (int X, int Y) Corner(float x, float y) => ((int)MathF.Round((x - left) * Px), (int)MathF.Round((bottom + tilesHigh - y) * Px));

            // The ground, and what lies flat on it.
            var bodies = new List<(float Foot, Action Draw)>();
            for (int y = bottom; y < bottom + tilesHigh; y++)
            {
                for (int x = left; x < left + tilesWide; x++)
                {
                    var (px, py) = Corner(x, y + 1);
                    image.Draw(TileArt.Ground(TileArt.GroundAt(map, x, y, false, biome)), px, py);
                    var type = map[x, y];
                    var kind = StandArt.KindAt(map, x, y);
                    if (kind == StandArt.Kind.None)
                    {
                        if (BiomeProps.Is(type))
                        {
                            var flat = TileArt.Object(type);
                            if (flat != null) image.Draw(flat, px, py);
                        }
                        continue;
                    }
                    // What stands, by its foot (as WorldView's standing layer).
                    var picture = StandArt.Picture(type, kind, StandArt.ShapeAt(map, x, y, false, biome), true);
                    if (picture == null) continue;
                    float cx = x + Structures.Footprint(type).W / 2f, foot = y + StandArt.FootLift(type, kind);
                    var (sx, sy) = Corner(cx, y);
                    int pivot = StandArt.PivotX(kind, 0, picture);
                    bodies.Add((foot + 0.0002f, () => image.Draw(picture, sx - pivot, sy - picture.Height)));
                }
            }
            // The floor's cover (moss, pebbles, tufts), as the game scatters it.
            for (int y = bottom - 1; y < bottom + tilesHigh + 1; y++)
            {
                for (int x = left - 1; x < left + tilesWide + 1; x++)
                {
                    if (!map.InBounds(x, y)) continue;
                    var pick = StandArt.DecorAt(map, (ulong)(seed + 1), biome, x, y, (cx, cy) => false);
                    if (!pick.Any) continue;
                    var cover = StandArt.DecorPicture(pick, biome);
                    var (fx, fy) = StandArt.DecorFoot(x, y, pick);
                    var (px, py) = Corner(fx, fy - StandArt.DecorDrop);
                    if (Decor.Flat(pick.Kind)) image.Draw(cover, px - cover.Width / 2, py - cover.Height);
                    else bodies.Add((fy + 0.0003f, () => image.Draw(cover, px - cover.Width / 2, py - cover.Height)));
                }
            }

            // Bodies: a shadow under each, the feet a little below the middle of where they stand.
            void Body(float x, float y, PixelImage sprite, int shadowWidth, int feetDown)
            {
                var (px, py) = Corner(x, y);
                int feet = py + feetDown;
                var shadow = CreatureArt.Shadow(Math.Max(6, shadowWidth));
                bodies.Add((y - 0.3f, () =>
                {
                    image.Draw(shadow, px - shadow.Width / 2, feet - shadow.Height / 2 - 1);
                    image.Draw(sprite, px - sprite.Width / 2, feet - sprite.Height);
                }));
            }
            if (hero is CharacterLook look)
            {
                var canvas = HeroArt.Draw(Look.Down, HeroMove.Stand, 0, HeroGear.None, look);
                var (px, py) = Corner(standX + 0.5f, standY + 0.5f);
                var shadow = CreatureArt.Shadow(10);
                bodies.Add((standY + 0.2f, () =>
                {
                    image.Draw(shadow, px - shadow.Width / 2, py + 5 - shadow.Height / 2 - 1);
                    image.Draw(canvas, px - HeroArt.FeetX, py + 5 - HeroArt.FeetY);
                }));
            }
            // The monsters: on open floor away from the hero, to both sides, turned toward the middle.
            int shown = 0;
            for (int attempt = 0; attempt < 80 && shown < Math.Min(3, monsters.Count); attempt++)
            {
                bool rightSide = (shown + (poolLeft ? 0 : 1)) % 2 == 0;
                int x = rightSide ? standX + 2 + (int)(Roll(attempt, 4, 9) % 2) : standX - 2 - (int)(Roll(attempt, 4, 9) % 2);
                int y = standY - 1 + (int)(Roll(attempt, 5, 9) % 3) - (shown == 2 ? 1 : 0);
                if (!Open(x, y) || !Open(x, y - 1) && !Open(x, y + 1)) continue;
                var sprite = CreatureArt.Monster(monsters[shown].Id, monsters[shown].Color, (int)(Roll(attempt, 6, 9) % 2));
                if (sprite == null) continue;
                // They are drawn facing right.
                if (rightSide) sprite = sprite.FlippedX();
                map[x, y] = TileType.StructurePart;
                Body(x + 0.5f, y + 0.5f, sprite, sprite.Width * 3 / 4, 5);
                shown++;
            }
            // Give the monsters' tiles back to the floor before the light is worked out.
            for (int y = bottom; y < bottom + tilesHigh; y++)
                for (int x = left; x < left + tilesWide; x++)
                    if (map[x, y] == TileType.StructurePart && !BiomeProps.Wide(map[x - 1, y])) map[x, y] = TileType.Floor;

            bodies.Sort((a, b) => b.Foot.CompareTo(a.Foot));
            foreach (var (_, draw) in bodies) draw();

            // A small creature or two of the biome, the ones that glow drawn over the dark.
            var glowing = new List<Action>();
            if (CritterArt.Lives(biome))
            {
                int critters = 0;
                for (int attempt = 0; attempt < 40 && critters < 2; attempt++)
                {
                    int x = left + 1 + (int)(Roll(attempt, 7, 10) % (uint)(tilesWide - 2));
                    int y = bottom + 1 + (int)(Roll(attempt, 8, 10) % (uint)(tilesHigh - 2));
                    if (!Open(x, y) || Kept(x, y)) continue;
                    var kind = CritterArt.Pick(biome, (int)(Roll(attempt, 9, 10) % 100), true);
                    if (CritterArt.Swims(kind)) continue;
                    var critter = CritterArt.Image(kind, (int)(Roll(attempt, 10, 10) % 2));
                    var (px, py) = Corner(x + 0.5f, y + (CritterArt.Flies(kind) ? 0.9f : 0.4f));
                    if (CritterArt.Glows(kind)) glowing.Add(() => image.Draw(critter, px - critter.Width / 2, py - critter.Height / 2));
                    else image.Draw(critter, px - critter.Width / 2, py - critter.Height / 2);
                    critters++;
                }
            }

            // The dark and the light: what the hero carries, the lamps, the biome's own glow and colour.
            const int margin = 3;
            int size = Math.Max(tilesWide, tilesHigh) + margin * 2;
            int lightLeft = left - margin, lightBottom = bottom - margin;
            // A little closer than the light a player carries, so the picture's edges fall into the dark.
            var sources = new List<LightSource> { new LightSource(new Vec2(standX + 0.5f, standY + 0.5f), 6.5f, 1.15f, Rgba.White, tints: false) };
            foreach (var (x, y) in lamps) sources.Add(new LightSource(TileMap.Center(x, y), 5.5f, 0.9f, Rgba.Hex("#FFB860"), tints: true));
            float glow = WorldLights.BiomeGlow(biome) + 0.04f;
            var tint = WorldLights.BiomeTint(biome);
            var lightMap = new LightMap(size);
            lightMap.Compute(map, lightLeft, lightBottom, sources, (x, y) => glow, (x, y) => tint);
            for (int py = 0; py < image.Height; py++)
            {
                for (int px = 0; px < image.Width; px++)
                {
                    float u = left + (px + 0.5f) / Px - lightLeft - 0.5f;
                    float v = bottom + tilesHigh - (py + 0.5f) / Px - lightBottom - 0.5f;
                    int i = (int)MathF.Floor(u), j = (int)MathF.Floor(v);
                    float fu = u - i, fv = v - j;
                    float r = 0, g = 0, b = 0, a = 0;
                    void Add(int ti, int tj, float weight)
                    {
                        var o = lightMap.Overlay(Math.Clamp(ti, 0, size - 1), Math.Clamp(tj, 0, size - 1));
                        r += o.R * weight;
                        g += o.G * weight;
                        b += o.B * weight;
                        a += o.A * weight;
                    }
                    Add(i, j, (1 - fu) * (1 - fv));
                    Add(i + 1, j, fu * (1 - fv));
                    Add(i, j + 1, (1 - fu) * fv);
                    Add(i + 1, j + 1, fu * fv);
                    image.Blend(px, py, new Rgba((int)r, (int)g, (int)b, (int)a));
                }
            }
            foreach (var draw in glowing) draw();

            // Cut to the picture's size about the middle, the edges drawn down into the dark so it sits in a frame.
            var result = new PixelImage(width, height);
            int offX = (image.Width - width) / 2, offY = (image.Height - height) / 2;
            var dark = Rgba.Hex("#07060A");
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var c = image[offX + x, offY + y].WithAlpha(255);
                    float ex = MathF.Abs((x + 0.5f) / width * 2 - 1), ey = MathF.Abs((y + 0.5f) / height * 2 - 1);
                    float edge = MathF.Max(0, MathF.Max(ex - 0.72f, (ey - 0.6f) * 0.7f)) / 0.28f;
                    result[x, y] = edge <= 0 ? c : c.Mix(dark, MathF.Min(0.85f, edge * edge));
                }
            }
            return result;
        }
    }
}
