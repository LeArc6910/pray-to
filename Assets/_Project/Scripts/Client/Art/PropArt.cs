using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Client
{
    // The structures of the story's places (Docs/Story.md, 장소 꾸밈, 2026-11-10), drawn in three-quarter view the way
    // Core Keeper and Stardew Valley draw theirs: each stands on the floor with a soft shadow at its foot and rises
    // above its tile, lit from the upper left (tops and left faces light, fronts in the middle, right faces and feet
    // dark), every shape ringed in a dark outline. Drawn from a few brushes (boxes, columns, ovals) so they are lit
    // alike. WorldView draws each with its feet on its footprint's bottom edge, in front of or behind people by
    // where they stand, and the floor of the place (Underlay) under it.
    static class PropArt
    {
        const int T = 16;
        static readonly Rgba Ink = Rgba.Hex("#1A1218");
        static readonly Rgba Shadow = new Rgba(8, 4, 12, 255);

        static readonly Ramp Stone = new Ramp(Rgba.Hex("#DCD7CB"), Rgba.Hex("#ADA89D"), Rgba.Hex("#837E76"), Rgba.Hex("#5A5650"));
        static readonly Ramp DarkStone = new Ramp(Rgba.Hex("#8A8478"), Rgba.Hex("#68635C"), Rgba.Hex("#4C4844"), Rgba.Hex("#34302E"));
        static readonly Ramp Wood = new Ramp(Rgba.Hex("#D2A46A"), Rgba.Hex("#A2703E"), Rgba.Hex("#764C2A"), Rgba.Hex("#4E301A"));
        static readonly Ramp DarkWood = new Ramp(Rgba.Hex("#8E6844"), Rgba.Hex("#684A30"), Rgba.Hex("#4A3422"), Rgba.Hex("#302014"));
        static readonly Ramp Plaster = new Ramp(Rgba.Hex("#F6EEDC"), Rgba.Hex("#E2D4B8"), Rgba.Hex("#BCAA8C"), Rgba.Hex("#8E7C62"));
        static readonly Ramp RoofRed = new Ramp(Rgba.Hex("#E07A5A"), Rgba.Hex("#B4483A"), Rgba.Hex("#82302C"), Rgba.Hex("#56201E"));
        static readonly Ramp RoofMoss = new Ramp(Rgba.Hex("#9AA05A"), Rgba.Hex("#6E7A3E"), Rgba.Hex("#4E5A2E"), Rgba.Hex("#323C1E"));
        static readonly Ramp RoofBlue = new Ramp(Rgba.Hex("#7AA0E8"), Rgba.Hex("#4A6CC0"), Rgba.Hex("#344C90"), Rgba.Hex("#223262"));
        static readonly Ramp Gold = new Ramp(Rgba.Hex("#FFE48A"), Rgba.Hex("#F0BE40"), Rgba.Hex("#B8862A"), Rgba.Hex("#7A5418"));
        static readonly Ramp Royal = new Ramp(Rgba.Hex("#F06A6A"), Rgba.Hex("#C8323A"), Rgba.Hex("#8A1E2A"), Rgba.Hex("#56121C"));
        static readonly Ramp Leaf = new Ramp(Rgba.Hex("#9AD86A"), Rgba.Hex("#5EA448"), Rgba.Hex("#3E7A38"), Rgba.Hex("#264E28"));
        static readonly Ramp Iron = new Ramp(Rgba.Hex("#AEB4C0"), Rgba.Hex("#727886"), Rgba.Hex("#4E5260"), Rgba.Hex("#34363E"));
        static readonly Ramp Water = new Ramp(Rgba.Hex("#A8DCFF"), Rgba.Hex("#4A86E0"), Rgba.Hex("#2E5AAE"), Rgba.Hex("#1C3A78"));
        static readonly Ramp Violet = new Ramp(Rgba.Hex("#E2C2FF"), Rgba.Hex("#A472F0"), Rgba.Hex("#6A40B0"), Rgba.Hex("#3E2470"));
        static readonly Ramp DemonCloth = new Ramp(Rgba.Hex("#B04A6A"), Rgba.Hex("#80284A"), Rgba.Hex("#581A34"), Rgba.Hex("#380E22"));
        static readonly Ramp Mushroom = new Ramp(Rgba.Hex("#9CC0FF"), Rgba.Hex("#5A80E0"), Rgba.Hex("#3C58A8"), Rgba.Hex("#26387A"));
        static readonly Ramp Cream = new Ramp(Rgba.Hex("#FFF6E2"), Rgba.Hex("#E8DCC0"), Rgba.Hex("#BCAA8A"), Rgba.Hex("#8A7A60"));
        static readonly Ramp Straw = new Ramp(Rgba.Hex("#F2DC8A"), Rgba.Hex("#D2B45A"), Rgba.Hex("#A2843A"), Rgba.Hex("#6E5624"));
        static readonly Ramp Bone = new Ramp(Rgba.Hex("#FFFCF0"), Rgba.Hex("#E2DCCA"), Rgba.Hex("#B4AC96"), Rgba.Hex("#7E7664"));
        static readonly Ramp Copper = new Ramp(Rgba.Hex("#F6B070"), Rgba.Hex("#D07A3E"), Rgba.Hex("#985028"), Rgba.Hex("#603018"));
        static readonly Ramp Paint = new Ramp(Rgba.Hex("#7AB8E0"), Rgba.Hex("#4A86B8"), Rgba.Hex("#305E88"), Rgba.Hex("#1E3C5A"));
        static readonly Rgba Rune = Rgba.Hex("#8AF0FF");
        static readonly Rgba RuneCore = Rgba.Hex("#E8FFFF");
        static readonly Rgba WindowLight = Rgba.Hex("#FFD27A");
        static readonly Rgba WindowCore = Rgba.Hex("#FFF2C2");
        static readonly Rgba Flame = Rgba.Hex("#FF9A3C");
        static readonly Rgba FlameCore = Rgba.Hex("#FFE07A");

        // ---- The brushes ----

        // A soft oval of shade on the floor, darkest in the middle.
        static void Ground(PixelImage image, float cx, float cy, float rx, float ry, int alpha = 110)
        {
            for (int y = (int)(cy - ry - 1); y <= (int)(cy + ry + 1); y++)
            {
                for (int x = (int)(cx - rx - 1); x <= (int)(cx + rx + 1); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float d = dx * dx + dy * dy;
                    if (d > 1) continue;
                    image.Blend(x, y, Shadow.WithAlpha((int)(alpha * (d < 0.45f ? 1f : 0.65f))));
                }
            }
        }

        static void Oval(PixelImage image, float cx, float cy, float rx, float ry, Rgba color)
        {
            for (int y = (int)(cy - ry - 1); y <= (int)(cy + ry + 1); y++)
            {
                for (int x = (int)(cx - rx - 1); x <= (int)(cx + rx + 1); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1) image[x, y] = color;
                }
            }
        }

        // An oval lit from the upper left: light at its upper left, dark toward its lower right.
        static void LitOval(PixelImage image, float cx, float cy, float rx, float ry, Ramp ramp)
        {
            for (int y = (int)(cy - ry - 1); y <= (int)(cy + ry + 1); y++)
            {
                for (int x = (int)(cx - rx - 1); x <= (int)(cx + rx + 1); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy > 1) continue;
                    float lit = -dx * 0.6f - dy * 0.8f;
                    image[x, y] = lit > 0.45f ? ramp.Light : lit > -0.25f ? ramp.Main : lit > -0.75f ? ramp.Shade : ramp.Dark;
                }
            }
        }

        static void Rect(PixelImage image, int x, int y, int w, int h, Rgba color) => image.Fill(x, y, w, h, color);

        // A block seen from above and in front: its top face (light, top rows) and its front (main, darker at its foot),
        // its right edge in shade.
        static void Box(PixelImage image, int x, int y, int w, int top, int front, Ramp ramp)
        {
            Rect(image, x, y, w, top, ramp.Light);
            Rect(image, x, y + top, w, front, ramp.Main);
            Rect(image, x + w - 1, y, 1, top, ramp.Main);
            Rect(image, x + w - 2, y + top, 2, front, ramp.Shade);
            Rect(image, x, y + top + front - 1, w, 1, ramp.Shade);
            Rect(image, x, y + top, w, 1, ramp.Shade.Mix(ramp.Main, 0.5f));
        }

        // An upright round thing: each column lit by how far left it is.
        static void Column(PixelImage image, int x, int y, int w, int h, Ramp ramp)
        {
            for (int i = 0; i < w; i++)
            {
                float t = w <= 1 ? 0.5f : i / (float)(w - 1);
                var color = t < 0.22f ? ramp.Light : t < 0.62f ? ramp.Main : t < 0.88f ? ramp.Shade : ramp.Dark;
                Rect(image, x + i, y, 1, h, color);
            }
        }

        // Darkens the lowest rows of what is drawn in a span: the shade where a thing meets the floor.
        static void Foot(PixelImage image, int x, int y, int w, int rows)
        {
            for (int j = 0; j < rows; j++)
                for (int i = x; i < x + w; i++)
                    image.Shade(i, y - j, 0.78f + 0.1f * j);
        }

        // Rings everything solid in the dark outline (inside the image, so leave a pixel round it), then lays it on
        // the floor's shadow.
        static PixelImage Finish(PixelImage body, PixelImage floor = null)
        {
            var result = floor ?? new PixelImage(body.Width, body.Height);
            var outlined = body.Copy();
            for (int y = 0; y < body.Height; y++)
            {
                for (int x = 0; x < body.Width; x++)
                {
                    if (body[x, y].A >= 200) continue;
                    if (Solid(body, x - 1, y) || Solid(body, x + 1, y) || Solid(body, x, y - 1) || Solid(body, x, y + 1)) outlined[x, y] = Ink;
                }
            }
            result.Draw(outlined, 0, 0);
            return result;
        }

        static bool Solid(PixelImage image, int x, int y) => image[x, y].A >= 200;

        static PixelImage ShadowLayer(int w, int h, float cx, float cy, float rx, float ry)
        {
            var floor = new PixelImage(w, h);
            Ground(floor, cx, cy, rx, ry);
            return floor;
        }

        // ---- The structures ----

        // The structure of the type, for walls by which of its four sides (N 1, E 2, S 4, W 8) meet the same wall.
        public static PixelImage Prop(TileType type, int sides = 0) => type switch
        {
            TileType.CastleWall => Wall(sides, Stone, castle: true),
            TileType.TimberWall => Wall(sides, Wood, castle: false),
            TileType.Cottage => Cottage(),
            TileType.Cabin => Cabin(),
            TileType.Tower => Tower(),
            TileType.BigFountain => BigFountain(),
            TileType.Fountain => SmallFountain(),
            TileType.Statue => Statue(),
            TileType.Pillar => Pillar(),
            TileType.LampPost => LampPost(),
            TileType.Throne => Throne(),
            TileType.RoyalBanner => Banner(Royal, Gold, demon: false),
            TileType.DemonBanner => Banner(DemonCloth, Bone, demon: true),
            TileType.Well => Well(),
            TileType.MushroomTree => MushroomTree(),
            TileType.Crates => Crates(),
            TileType.Barrel => Barrel(),
            TileType.Signpost => Signpost(),
            TileType.Bench => Bench(),
            TileType.Hedge => Hedge(),
            TileType.Flowerbed => Flowerbed(),
            TileType.Woodpile => Woodpile(),
            TileType.TrainingDummy => TrainingDummy(),
            TileType.WeaponRack => WeaponRack(),
            TileType.Campfire => Campfire(),
            TileType.Bones => Bones(),
            TileType.Spire => Spire(),
            TileType.Tent => SmallTent(),
            TileType.BigTent => BigTent(),
            TileType.Totem => Totem(),
            TileType.Cage => Cage(),
            TileType.Brazier => Brazier(),
            TileType.Boat => Boat(),
            TileType.Lighthouse => Lighthouse(),
            TileType.NetRack => NetRack(),
            TileType.Bollard => Bollard(),
            TileType.Anchor => Anchor(),
            TileType.BrokenPillar => BrokenPillar(),
            TileType.FallenColumn => FallenColumn(),
            TileType.Rubble => Rubble(),
            TileType.RuneStone => RuneStone(),
            TileType.Altar => Altar(),
            TileType.MineEntrance => MineEntrance(),
            TileType.MineCart => MineCart(),
            TileType.OrePile => OrePile(),
            TileType.Lantern => Lantern(),
            // The things players build, standing too (2026-11-10, Stands).
            TileType.Workbench => Workbench(),
            TileType.Furnace => Furnace(),
            TileType.Chest => Chest(),
            TileType.Anvil => Anvil(),
            TileType.Bed => Bed(),
            TileType.Table => StoneTable(),
            TileType.Torch => Torch(),
            TileType.Lamp => GlowLamp(),
            TileType.Pot => MushroomPot(),
            TileType.Banner => Banner(Paint, Gold, demon: false),
            TileType.WardStone => WardStone(),
            TileType.RiftAltar => RiftAltar(),
            // Machines (2026-11-11): the busiest drawn on their own (DrillBody and the rest), the others their tile as the
            // top of a block (Raised).
            TileType.Drill => DrillBody(),
            TileType.Smelter => SmelterBody(),
            TileType.Generator => GeneratorBody(),
            TileType.Turret => TurretBody(),
            TileType.Assembler => AssemblerBody(),
            TileType.ArcFurnace => ArcFurnaceBody(),
            _ when RaisedMachines.Contains(type) => Raised(TileArt.Object(type), RaiseOf(type)),
            _ => null,
        };

        // The machines that stand on the floor as blocks (2026-11-11): bodies their moving parts sit on top of
        // (WorldView.DrawMachines draws those parts LiftOf higher and just in front). Belts, splitters, filters, arms,
        // levers, poles, sprinklers, harvesters, water wheels and solar panels stay flat on the floor.
        static readonly HashSet<TileType> RaisedMachines = new HashSet<TileType>
        {
            TileType.Drill, TileType.Smelter, TileType.Generator, TileType.Turret, TileType.Keg, TileType.PreservesJar, TileType.Assembler,
            TileType.ArcFurnace, TileType.Coop, TileType.Barn, TileType.AlchemyTable, TileType.TeslaTower,
        };

        // The houses whose windows light up in the evening (2026-11-11, WorldView.DrawProps and AddNightLights).
        public static bool LitAtNight(TileType type) => type == TileType.Cottage || type == TileType.Cabin || type == TileType.Tower;

        // The drawing by day: its lit windows as dark glass with a sky glint.
        public static PixelImage Daylit(PixelImage lit)
        {
            var day = lit.Copy();
            var glass = Rgba.Hex("#4E6274");
            var glint = Rgba.Hex("#8EA6B8");
            for (int y = 0; y < day.Height; y++)
            {
                for (int x = 0; x < day.Width; x++)
                {
                    var c = day[x, y];
                    if (Same(c, WindowLight)) day[x, y] = glass;
                    else if (Same(c, WindowCore)) day[x, y] = glint;
                }
            }
            return day;
        }

        static bool Same(Rgba a, Rgba b) => a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A;

        // How many pixels a raised machine's top stands above the floor (a big machine's, BigLift).
        static int RaiseOf(TileType type) => type == TileType.Keg || type == TileType.PreservesJar ? 4 : 5;
        public const int BigLift = 7;

        // How many pixels above the tile's middle a machine's moving part and load sit: the middle of its top.
        public static float LiftOf(TileType type) => type switch
        {
            TileType.Drill => 4,
            TileType.Smelter => 6,
            TileType.Generator => 4,
            TileType.Turret => 2.5f,
            TileType.Assembler => 4,
            TileType.ArcFurnace => 6,
            _ => RaiseOf(type),
        };

        // ---- Machines drawn on their own (2026-11-11, 사람이 고른 다음 작업 5번) ----

        static readonly Rgba Hazard = Rgba.Hex("#F0C030");
        static readonly Rgba LampGreen = Rgba.Hex("#7CF08A");
        static readonly Rgba LampRed = Rgba.Hex("#F05A4A");

        // A drill: a steel housing with a motor block behind, hazard stripes and lamps on its front; the bit turns on
        // its top (WorldView.DrawMachines).
        static PixelImage DrillBody()
        {
            var body = new PixelImage(T, 22);
            Box(body, 3, 3, 10, 3, 5, DarkStone);
            for (int x = 4; x < 12; x += 2) body[x, 4] = DarkStone.Dark;
            Box(body, 1, 8, 14, 5, 7, Iron);
            for (int x = 1; x < 15; x++)
            {
                bool stripe = (x + 0) / 2 % 2 == 0;
                body[x, 16] = stripe ? Hazard : Ink;
                body[x, 17] = (x + 1) / 2 % 2 == 0 ? Hazard : Ink;
            }
            body[3, 14] = LampGreen;
            body[5, 14] = LampRed;
            Rect(body, 9, 14, 4, 1, Iron.Dark);
            Foot(body, 1, 19, 14, 1);
            return Finish(body, ShadowLayer(T, 22, 8, 20, 7, 2));
        }

        // A smelter: a stone block with a copper hopper on its top, a chimney behind and the fire in its mouth.
        static PixelImage SmelterBody()
        {
            var body = new PixelImage(T, 23);
            Column(body, 10, 1, 4, 8, DarkStone);
            Rect(body, 10, 1, 4, 1, Ink);
            Box(body, 1, 7, 14, 4, 10, Stone);
            for (int y = 12; y < 20; y += 3)
                for (int x = 2; x < 14; x++)
                    if ((x + y) % 5 == 0) body[x, y] = Stone.Shade;
            Rect(body, 3, 7, 5, 3, Copper.Main);
            Rect(body, 3, 7, 5, 1, Copper.Light);
            Rect(body, 4, 8, 3, 1, Copper.Dark);
            Rect(body, 5, 13, 6, 6, Ink);
            Rect(body, 6, 12, 4, 1, Ink);
            Rect(body, 6, 15, 4, 4, Flame);
            Rect(body, 7, 16, 2, 2, FlameCore);
            Foot(body, 1, 20, 14, 1);
            return Finish(body, ShadowLayer(T, 23, 8, 21, 7, 2));
        }

        // A generator: an iron block with a grate on top where its flame burns, copper coils at the sides and a dial.
        static PixelImage GeneratorBody()
        {
            var body = new PixelImage(T, 22);
            Box(body, 1, 8, 14, 5, 7, Iron);
            Rect(body, 5, 9, 6, 3, Ink);
            for (int x = 5; x < 11; x += 2) Rect(body, x, 9, 1, 3, Iron.Shade);
            foreach (int x in new[] { 1, 13 })
            {
                Rect(body, x, 13, 2, 6, Copper.Main);
                for (int y = 13; y < 19; y += 2) Rect(body, x, y, 2, 1, Copper.Light);
            }
            LitOval(body, 8, 16, 2.5f, 2.5f, Cream);
            body[8, 16] = Ink;
            body[9, 15] = Ink;
            Foot(body, 1, 19, 14, 1);
            return Finish(body, ShadowLayer(T, 22, 8, 20, 7, 2));
        }

        // A turret: a squat round stone base banded with iron; its barrel turns on top.
        static PixelImage TurretBody()
        {
            var body = new PixelImage(T, 20);
            Column(body, 2, 10, 12, 7, DarkStone);
            Rect(body, 2, 13, 12, 1, Iron.Main);
            Rect(body, 2, 14, 12, 1, Iron.Shade);
            LitOval(body, 8, 17, 6, 1.5f, DarkStone);
            LitOval(body, 8, 10, 6, 2.5f, Iron);
            LitOval(body, 8, 9.6f, 3, 1.3f, Iron);
            return Finish(body, ShadowLayer(T, 20, 8, 18, 7, 2));
        }

        // An assembler: a blue-painted cabinet with a lit work window on top, vents and a little green screen in front.
        static PixelImage AssemblerBody()
        {
            var body = new PixelImage(T, 22);
            Box(body, 1, 8, 14, 5, 7, Paint);
            Rect(body, 4, 9, 8, 3, Ink);
            Rect(body, 5, 10, 6, 1, Gold.Light);
            for (int y = 14; y < 19; y += 2) Rect(body, 3, y, 5, 1, Paint.Dark);
            Rect(body, 9, 14, 4, 3, Ink);
            Rect(body, 10, 15, 2, 1, LampGreen);
            Foot(body, 1, 19, 14, 1);
            return Finish(body, ShadowLayer(T, 22, 8, 20, 7, 2));
        }

        // An arc furnace: a dark iron block with a chimney, copper coils down its sides and a blue arc in its mouth.
        static PixelImage ArcFurnaceBody()
        {
            var body = new PixelImage(T, 24);
            Column(body, 2, 2, 3, 7, DarkStone);
            Box(body, 1, 8, 14, 5, 9, Iron);
            Rect(body, 1, 8, 14, 5, DarkStone.Light);
            foreach (int x in new[] { 1, 13 })
            {
                Rect(body, x, 13, 2, 8, Copper.Main);
                for (int y = 13; y < 21; y += 2) Rect(body, x, y, 2, 1, Copper.Light);
            }
            Rect(body, 5, 14, 6, 6, Ink);
            Rect(body, 6, 15, 4, 4, Paint.Main);
            Rect(body, 7, 15, 1, 4, Rune);
            body[8, 16] = RuneCore;
            body[6, 17] = Rune;
            Foot(body, 1, 21, 14, 1);
            return Finish(body, ShadowLayer(T, 24, 8, 22, 7, 2));
        }

        // A flat top-down drawing stood up as a block: under each column's lowest drawn pixel, that colour darkened
        // for lift pixels (darker toward the floor, lighter at the left edge), the top over it, and a shadow under it.
        public static PixelImage Raised(PixelImage top, int lift)
        {
            int w = top.Width, h = top.Height + lift;
            var body = new PixelImage(w, h);
            int left = w, right = -1;
            for (int x = 0; x < w; x++)
            {
                int bottom = -1;
                for (int y = top.Height - 1; y >= 0 && bottom < 0; y--) if (top[x, y].A >= 200) bottom = y;
                if (bottom < 0) continue;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
                // The face takes the colour just inside the top's edge (the edge itself is mostly outline).
                var face = top[x, Math.Max(0, bottom - 2)];
                if (face.A < 200) face = top[x, bottom];
                for (int j = 1; j <= lift; j++)
                {
                    body[x, bottom + j] = face;
                    body.Shade(x, bottom + j, j == lift ? 0.4f : 0.72f - 0.04f * j);
                }
            }
            body.Draw(top, 0, 0);
            // The face's corners: a highlight down its left side, a shade down its right.
            for (int y = 0; y < h; y++)
            {
                if (left < w && body[left, y].A >= 200 && top[left, Math.Min(y, top.Height - 1)].A < 200) body.Shade(left, y, 1.25f);
                if (right >= 0 && body[right, y].A >= 200 && (y >= top.Height || top[right, y].A < 200)) body.Shade(right, y, 0.8f);
            }
            if (right < 0) return body;
            var floor = ShadowLayer(w, h, (left + right) / 2f, h - 1.5f, (right - left) / 2f + 1, 2);
            floor.Draw(body, 0, 0);
            return floor;
        }

        // The things players build that stand on the floor like the structures (2026-11-10, 사람이 고른 다음 작업 1번):
        // the crafting stations, the chest, the bed and the furniture. Machines with moving parts drawn over them stay
        // tiles, so their parts are not hidden.
        public static bool Stands(TileType type) => type == TileType.Workbench || type == TileType.Furnace || type == TileType.Chest
            || type == TileType.Anvil || type == TileType.Bed || type == TileType.Table || type == TileType.Torch || type == TileType.Lamp
            || type == TileType.Pot || type == TileType.Banner || type == TileType.WardStone || type == TileType.RiftAltar
            || RaisedMachines.Contains(type);

        // Whether a machine's body stands raised (its parts drawn LiftOf higher).
        public static bool Raises(TileType type) => RaisedMachines.Contains(type);

        // Every structure, for the preview tool.
        public static readonly TileType[] All =
        {
            TileType.Cottage, TileType.Cabin, TileType.Tower, TileType.BigFountain, TileType.BigTent, TileType.MushroomTree,
            TileType.Statue, TileType.Pillar, TileType.LampPost, TileType.Throne, TileType.RoyalBanner, TileType.DemonBanner, TileType.Well,
            TileType.Crates, TileType.Barrel, TileType.Signpost, TileType.Bench, TileType.Hedge, TileType.Flowerbed, TileType.Woodpile,
            TileType.TrainingDummy, TileType.WeaponRack, TileType.Campfire, TileType.Bones, TileType.Spire, TileType.Totem, TileType.Cage,
            TileType.Brazier, TileType.Fountain, TileType.Tent,
            TileType.Boat, TileType.Lighthouse, TileType.NetRack, TileType.Bollard, TileType.Anchor, TileType.BrokenPillar, TileType.FallenColumn,
            TileType.Rubble, TileType.RuneStone, TileType.Altar, TileType.MineEntrance, TileType.MineCart, TileType.OrePile, TileType.Lantern,
            TileType.Workbench, TileType.Furnace, TileType.Chest, TileType.Anvil, TileType.Bed, TileType.Table, TileType.Torch, TileType.Lamp,
            TileType.Pot, TileType.Banner, TileType.WardStone, TileType.RiftAltar,
            TileType.Drill, TileType.Smelter, TileType.Generator, TileType.Turret, TileType.Keg, TileType.PreservesJar, TileType.Assembler,
            TileType.ArcFurnace, TileType.Coop, TileType.Barn, TileType.AlchemyTable, TileType.TeslaTower,
        };

        // A wall block: its top face over its tile (shifted up by its height, so a wall in front hides this one's foot)
        // and its face below. Edges where no wall goes on are outlined; a castle wall's open top edge is crenellated.
        static PixelImage Wall(int sides, Ramp ramp, bool castle)
        {
            const int height = 8;
            var image = new PixelImage(T, T + height);
            bool n = (sides & 1) != 0, e = (sides & 2) != 0, w = (sides & 8) != 0;
            // The top: flagstones or planks in courses.
            for (int y = 0; y < T; y++)
            {
                for (int x = 0; x < T; x++)
                {
                    var color = ramp.Light;
                    if (castle)
                    {
                        int course = y / 4, offset = course % 2 == 0 ? 0 : 4;
                        if (y % 4 == 3 || (x + offset) % 8 == 7) color = ramp.Main;
                    }
                    else if (x % 4 == 3) color = ramp.Main;
                    image[x, y] = color;
                }
            }
            if (castle && !n)
            {
                // Merlons along the open edge: raised blocks with gaps.
                for (int x = 0; x < T; x++)
                {
                    bool merlon = x % 6 < 4;
                    image[x, 0] = merlon ? ramp.Light.Mix(Rgba.White, 0.3f) : ramp.Shade;
                    image[x, 1] = merlon ? ramp.Light : ramp.Shade;
                    image[x, 2] = merlon ? ramp.Main : ramp.Light;
                }
            }
            // The face: courses of blocks or planks, darker toward the foot.
            for (int y = T; y < T + height; y++)
            {
                for (int x = 0; x < T; x++)
                {
                    int row = y - T;
                    var color = row < 1 ? ramp.Shade : ramp.Main;
                    if (castle && (row == 4 || (x + (row < 4 ? 0 : 4)) % 8 == 7)) color = ramp.Shade;
                    if (!castle && x % 4 == 3) color = ramp.Shade;
                    if (row >= height - 2) color = color.Scale(0.8f);
                    image[x, y] = color;
                }
            }
            // Outlines on the open sides and a light rim on the lit ones.
            for (int y = 0; y < T + height; y++)
            {
                if (!w) image[0, y] = Ink;
                if (!e) image[T - 1, y] = Ink;
                else if (y >= T) image[T - 1, y] = image[T - 1, y].Scale(0.92f);
                if (!w && y < T) image[1, y] = ramp.Light.Mix(Rgba.White, 0.25f);
                if (!e && y < T) image[T - 2, y] = ramp.Main;
            }
            if (!n) for (int x = 0; x < T; x++) image[x, 0] = image[x, 0].Scale(castle ? 1f : 1.05f);
            for (int x = 0; x < T; x++) image[x, T + height - 1] = Ink;
            if (!n && !castle) for (int x = 0; x < T; x++) image[x, 0] = Ink;
            return image;
        }

        // A village cottage, four tiles by three: plaster walls framed in timber, a red tiled roof with a stone
        // chimney, lit windows with flower boxes, a door with a step.
        static PixelImage Cottage()
        {
            const int w = 64, h = 74;
            var body = new PixelImage(w, h);
            // Chimney behind the roof.
            Column(body, 46, 4, 8, 16, Stone);
            Rect(body, 45, 3, 10, 2, Stone.Dark);
            // Roof: courses of tiles, light at the ridge, an eave in shadow.
            for (int y = 12; y < 46; y++)
            {
                int inset = Math.Max(0, (16 - (y - 12)) / 4);
                for (int x = 2 + inset; x < w - 2 - inset; x++)
                {
                    int course = (y - 12) / 4;
                    bool seam = (y - 12) % 4 == 3 || (x + course * 3) % 7 == 0;
                    float t = (x - 2f) / (w - 4f);
                    var color = t > 0.82f ? RoofRed.Shade : y < 16 ? RoofRed.Light : RoofRed.Main;
                    if (seam) color = color.Scale(0.78f);
                    body[x, y] = color;
                }
            }
            Rect(body, 2, 44, w - 4, 2, RoofRed.Dark);
            // Walls: plaster between timber posts and a beam, shade under the eave.
            Rect(body, 5, 46, w - 10, 22, Plaster.Main);
            Rect(body, 5, 46, w - 10, 3, Plaster.Shade);
            Rect(body, w - 8, 46, 3, 22, Plaster.Shade);
            foreach (int post in new[] { 5, 21, 42, w - 8 }) Rect(body, post, 46, 3, 22, DarkWood.Main);
            Rect(body, 5, 56, w - 10, 2, DarkWood.Main);
            // Door and step.
            Rect(body, 27, 52, 10, 16, Wood.Shade);
            for (int x = 28; x < 36; x += 3) Rect(body, x, 53, 1, 15, Wood.Dark);
            Rect(body, 27, 52, 10, 1, Wood.Light);
            body[34, 60] = Gold.Main;
            Rect(body, 25, 68, 14, 2, Stone.Light);
            // Windows with warm light and flower boxes.
            foreach (int wx in new[] { 10, 46 })
            {
                Rect(body, wx, 50, 9, 8, DarkWood.Dark);
                Rect(body, wx + 1, 51, 7, 6, WindowLight);
                Rect(body, wx + 1, 51, 3, 2, WindowCore);
                Rect(body, wx + 4, 51, 1, 6, DarkWood.Main);
                Rect(body, wx + 1, 54, 7, 1, DarkWood.Main);
                Rect(body, wx - 1, 59, 11, 3, Wood.Main);
                for (int i = 0; i < 4; i++)
                {
                    body[wx + 1 + i * 2, 58] = Leaf.Main;
                    body[wx + 1 + i * 2, 57] = i % 2 == 0 ? Royal.Light : Gold.Light;
                }
            }
            // Foundation stones along the foot.
            Rect(body, 4, 68, w - 8, 3, DarkStone.Main);
            for (int x = 6; x < w - 6; x += 5) body[x, 69] = DarkStone.Dark;
            return Finish(body, ShadowLayer(w, h, w / 2f + 2, h - 3, w / 2f, 4));
        }

        // The hero's log cabin, four tiles by three: walls of round logs with their ends showing, a mossy roof, a
        // stone chimney with smoke, a window and a plank door.
        static PixelImage Cabin()
        {
            const int w = 64, h = 74;
            var body = new PixelImage(w, h);
            Column(body, 10, 6, 8, 16, DarkStone);
            for (int i = 0; i < 3; i++) Oval(body, 13 + i, 3 - i, 3, 2, new Rgba(200, 200, 210, 140 - i * 30));
            for (int y = 14; y < 44; y++)
            {
                int inset = Math.Max(0, (14 - (y - 14)) / 4);
                for (int x = 2 + inset; x < w - 2 - inset; x++)
                {
                    bool plank = (x + (y / 6) * 2) % 6 == 0;
                    float t = (x - 2f) / (w - 4f);
                    var color = t > 0.82f ? RoofMoss.Shade : y < 18 ? RoofMoss.Light : RoofMoss.Main;
                    if (plank || y % 6 == 5) color = color.Scale(0.8f);
                    body[x, y] = color;
                }
            }
            Rect(body, 2, 42, w - 4, 2, RoofMoss.Dark);
            // Log courses, each lit on its top edge.
            for (int y = 44; y < 68; y++)
            {
                int row = (y - 44) % 4;
                var color = row == 0 ? Wood.Light : row == 3 ? Wood.Dark : Wood.Main;
                Rect(body, 5, y, w - 10, 1, color);
                Rect(body, w - 8, y, 3, 1, row == 0 ? Wood.Main : Wood.Shade);
            }
            Rect(body, 5, 44, w - 10, 2, Wood.Shade);
            // The log ends at the corners.
            for (int y = 45; y < 68; y += 4)
            {
                foreach (int cx in new[] { 4, w - 5 })
                {
                    Oval(body, cx + 0.5f, y + 1.5f, 2.5f, 2, Wood.Light);
                    body[cx, y + 1] = Wood.Shade;
                }
            }
            Rect(body, 38, 50, 11, 18, DarkWood.Main);
            for (int x = 40; x < 48; x += 3) Rect(body, x, 51, 1, 17, DarkWood.Dark);
            body[46, 59] = Iron.Light;
            Rect(body, 13, 50, 12, 9, DarkWood.Dark);
            Rect(body, 14, 51, 10, 7, WindowLight);
            Rect(body, 14, 51, 4, 2, WindowCore);
            Rect(body, 18, 51, 1, 7, DarkWood.Main);
            Rect(body, 4, 68, w - 8, 3, DarkStone.Main);
            return Finish(body, ShadowLayer(w, h, w / 2f + 2, h - 3, w / 2f, 4));
        }

        // A round tower of the castle wall, two tiles by two: stone courses, arrow slits, a crenellated top and a
        // blue cone of a roof with a pennant.
        static PixelImage Tower()
        {
            const int w = 32, h = 68;
            var body = new PixelImage(w, h);
            Column(body, 3, 26, 26, 37, Stone);
            for (int y = 30; y < 62; y += 5)
                for (int x = 4; x < 28; x++)
                    if ((x + (y / 5) * 4) % 8 != 0) body[x, y] = body[x, y].Scale(0.86f);
                    else Rect(body, x, y - 4, 1, 4, body[x, y].Scale(0.86f));
            foreach (int sy in new[] { 34, 48 }) Rect(body, 15, sy, 2, 6, Ink);
            Rect(body, 14, 54, 4, 8, DarkWood.Main);
            // The battlement: a wider ring with merlons.
            LitOval(body, 16, 24, 15, 5, Stone);
            for (int x = 1; x < 31; x++)
                if (x % 5 < 3) Rect(body, x, 17, 1, 5, x < 10 ? Stone.Light : x > 24 ? Stone.Shade : Stone.Main);
            // The roof cone.
            for (int y = 2; y < 22; y++)
            {
                float half = (y - 2) * 0.68f;
                for (int x = (int)(16 - half); x <= (int)(16 + half); x++)
                {
                    float t = (x - (16 - half)) / Math.Max(1, half * 2);
                    var color = t < 0.3f ? RoofBlue.Light : t < 0.7f ? RoofBlue.Main : RoofBlue.Shade;
                    if (y % 4 == 3) color = color.Scale(0.85f);
                    body[x, y] = color;
                }
            }
            Rect(body, 16, 0, 1, 3, Iron.Dark);
            Rect(body, 17, 0, 4, 2, Royal.Main);
            return Finish(body, ShadowLayer(w, h, w / 2f + 2, h - 4, 14, 4));
        }

        // A great fountain, two tiles by two: a stone basin of water and a pedestal with a bowl spilling over.
        static PixelImage BigFountain()
        {
            const int w = 32, h = 42;
            var body = new PixelImage(w, h);
            Oval(body, 16, 33, 15, 7, Stone.Shade);
            Rect(body, 1, 30, 30, 4, Stone.Main);
            LitOval(body, 16, 30, 15, 7, Stone);
            Oval(body, 16, 30, 12, 5, Water.Main);
            Oval(body, 15, 29, 9, 3, Water.Light.Mix(Water.Main, 0.5f));
            Column(body, 14, 12, 4, 18, Stone);
            LitOval(body, 16, 12, 7, 2.5f, Stone);
            Oval(body, 16, 11.5f, 5, 1.5f, Water.Light);
            for (int i = 0; i < 6; i++)
            {
                body[16 - 2 - i, 6 + i] = Water.Light;
                body[16 + 2 + i, 6 + i] = Water.Main;
            }
            Rect(body, 15, 3, 2, 8, Water.Light);
            return Finish(body, ShadowLayer(w, h, w / 2f + 1, h - 4, 15, 3));
        }

        static PixelImage SmallFountain()
        {
            var body = new PixelImage(T, 20);
            LitOval(body, 8, 14, 7, 4, Stone);
            Oval(body, 8, 14, 5, 2.5f, Water.Main);
            Column(body, 7, 4, 2, 10, Stone);
            body[8, 2] = Water.Light;
            body[8, 3] = Water.Light;
            return Finish(body, ShadowLayer(T, 20, 8.5f, 18, 7, 2));
        }

        // A stone knight on a plinth, his sword point down before him.
        static PixelImage Statue()
        {
            var body = new PixelImage(T, 42);
            Box(body, 2, 27, 12, 3, 11, Stone);
            Rect(body, 5, 33, 6, 3, Stone.Shade);
            LitOval(body, 8, 6, 3, 3, Stone);
            Rect(body, 6, 3, 4, 1, Stone.Shade);
            Column(body, 4, 10, 8, 10, Stone);
            Column(body, 5, 20, 6, 7, Stone);
            Rect(body, 3, 10, 2, 7, Stone.Light);
            Rect(body, 11, 10, 2, 7, Stone.Shade);
            Rect(body, 7, 12, 2, 14, Stone.Light);
            Rect(body, 5, 13, 6, 1, Stone.Dark);
            return Finish(body, ShadowLayer(T, 42, 9, 40, 7, 2));
        }

        static PixelImage Pillar()
        {
            var body = new PixelImage(T, 42);
            Box(body, 2, 2, 12, 2, 3, Stone);
            Column(body, 4, 7, 8, 26, Stone);
            for (int y = 9; y < 32; y++) body[7, y] = Stone.Main;
            Box(body, 2, 33, 12, 2, 5, Stone);
            return Finish(body, ShadowLayer(T, 42, 9, 40, 7, 2));
        }

        static PixelImage LampPost()
        {
            var body = new PixelImage(T, 42);
            Column(body, 7, 12, 2, 26, Iron);
            Box(body, 5, 37, 6, 1, 3, Iron);
            Rect(body, 5, 3, 6, 2, Iron.Dark);
            Rect(body, 6, 5, 4, 6, WindowLight);
            Rect(body, 6, 5, 2, 3, WindowCore);
            Rect(body, 5, 11, 6, 1, Iron.Dark);
            body[8, 2] = Iron.Main;
            return Finish(body, ShadowLayer(T, 42, 9, 40, 5, 1.5f));
        }

        // The queen's throne: a tall gold back with a red cushion, gems on its crest.
        static PixelImage Throne()
        {
            var body = new PixelImage(T, 34);
            Column(body, 3, 3, 10, 18, Gold);
            Rect(body, 5, 6, 6, 14, Royal.Main);
            Rect(body, 5, 6, 2, 14, Royal.Light);
            body[8, 2] = Gold.Light;
            body[7, 3] = Royal.Light;
            Box(body, 2, 20, 12, 3, 4, Royal);
            Rect(body, 1, 17, 2, 9, Gold.Main);
            Rect(body, 13, 17, 2, 9, Gold.Shade);
            Box(body, 2, 27, 12, 1, 4, Gold);
            return Finish(body, ShadowLayer(T, 34, 9, 32, 7, 2));
        }

        static PixelImage Banner(Ramp cloth, Ramp trim, bool demon)
        {
            var body = new PixelImage(T, 42);
            Column(body, 3, 3, 2, 36, demon ? Iron : Wood);
            Rect(body, 2, 1, 4, 2, trim.Main);
            for (int y = 5; y < 28; y++)
            {
                int right = demon && y > 22 && (y % 3 == 0) ? 11 : 13;
                for (int x = 5; x <= right; x++)
                {
                    var color = x < 7 ? cloth.Light : x > 11 ? cloth.Shade : cloth.Main;
                    if (x == 5 || x == right || y == 5) color = trim.Main;
                    body[x, y] = color;
                }
            }
            if (!demon)
            {
                // A swallowtail at the foot.
                for (int x = 5; x <= 13; x++) if (x < 8 || x > 10) body[x, 28] = x < 7 ? cloth.Light : cloth.Shade;
                Rect(body, 7, 12, 5, 3, trim.Main);
                body[7, 11] = trim.Light; body[9, 10] = trim.Light; body[11, 11] = trim.Light;
            }
            else
            {
                LitOval(body, 9, 13, 2.5f, 2.5f, trim);
                body[8, 13] = Ink; body[10, 13] = Ink;
                Rect(body, 8, 16, 3, 1, trim.Shade);
            }
            Box(body, 1, 38, 6, 1, 2, demon ? Iron : DarkWood);
            return Finish(body, ShadowLayer(T, 42, 6, 40, 4, 1.5f));
        }

        // A well: a round stone wall over dark water under a little tiled roof on two posts.
        static PixelImage Well()
        {
            var body = new PixelImage(T + 4, 34);
            Column(body, 2, 22, 16, 9, Stone);
            for (int x = 3; x < 17; x += 4) Rect(body, x, 22, 1, 9, Stone.Shade);
            Rect(body, 2, 26, 16, 1, Stone.Shade);
            LitOval(body, 10, 22, 8, 3, Stone);
            Oval(body, 10, 22, 6, 1.6f, Water.Dark);
            Rect(body, 3, 7, 2, 16, Wood.Main);
            Rect(body, 15, 7, 2, 16, Wood.Shade);
            Rect(body, 4, 11, 12, 1, Wood.Dark);
            Rect(body, 9, 12, 1, 6, Wood.Light);
            Box(body, 8, 17, 4, 1, 3, Wood);
            for (int y = 1; y < 8; y++)
            {
                int inset = Math.Max(0, 4 - y);
                for (int x = inset; x < T + 4 - inset; x++) body[x, y] = x > 15 ? RoofRed.Shade : y < 3 ? RoofRed.Light : (x + y) % 4 == 0 ? RoofRed.Shade : RoofRed.Main;
            }
            return Finish(body, ShadowLayer(T + 4, 34, 11, 32, 8, 2));
        }

        // A giant cave mushroom: a broad blue cap with pale spots over a thick stem, wider than its tile.
        static PixelImage MushroomTree()
        {
            const int w = 32, h = 44;
            var body = new PixelImage(w, h);
            Column(body, 13, 22, 7, 19, Cream);
            Rect(body, 12, 38, 9, 3, Cream.Shade);
            LitOval(body, 16, 22, 13, 4, new Ramp(Mushroom.Shade, Mushroom.Dark, Mushroom.Dark, Mushroom.Dark));
            LitOval(body, 16, 15, 15, 9, Mushroom);
            foreach (var (sx, sy) in new[] { (9, 11), (15, 8), (21, 12), (12, 17), (24, 17), (18, 14) })
            {
                body[sx, sy] = Cream.Light;
                body[sx + 1, sy] = Cream.Main;
                body[sx, sy + 1] = Cream.Main;
            }
            return Finish(body, ShadowLayer(w, h, 17, 41, 11, 3));
        }

        static PixelImage Crates()
        {
            var body = new PixelImage(T, 26);
            Box(body, 1, 12, 14, 4, 10, Wood);
            // An X of bracing across the front.
            for (int i = 0; i < 10; i++)
            {
                body[3 + i, 17 + i * 7 / 9] = Wood.Dark;
                body[12 - i, 17 + i * 7 / 9] = Wood.Dark;
            }
            Rect(body, 1, 16, 14, 1, Wood.Dark);
            Rect(body, 2, 16, 1, 10, Wood.Dark);
            Rect(body, 13, 16, 1, 10, Wood.Dark);
            Box(body, 4, 3, 9, 3, 7, Wood);
            Rect(body, 4, 6, 9, 1, Wood.Dark);
            body[8, 8] = Iron.Light;
            return Finish(body, ShadowLayer(T, 26, 9, 24, 7, 2));
        }

        static PixelImage Barrel()
        {
            var body = new PixelImage(T, 22);
            Column(body, 3, 6, 10, 13, Wood);
            LitOval(body, 8, 6, 5, 2.5f, Wood);
            Oval(body, 8, 6, 3.5f, 1.4f, Wood.Shade);
            foreach (int y in new[] { 9, 15 }) Column(body, 3, y, 10, 1, Iron);
            return Finish(body, ShadowLayer(T, 22, 9, 20, 6, 2));
        }

        static PixelImage Signpost()
        {
            var body = new PixelImage(T, 26);
            Column(body, 7, 9, 2, 15, Wood);
            for (int x = 1; x < 14; x++)
            {
                int top = x > 11 ? 3 + (x - 11) : 3, bottom = x > 11 ? 9 - (x - 11) : 9;
                for (int y = top; y < bottom; y++) body[x, y] = y == top ? Wood.Light : x > 11 ? Wood.Shade : Wood.Main;
            }
            Rect(body, 3, 5, 6, 1, DarkWood.Dark);
            Rect(body, 3, 7, 4, 1, DarkWood.Dark);
            return Finish(body, ShadowLayer(T, 26, 9, 24, 4, 1.5f));
        }

        static PixelImage Bench()
        {
            var body = new PixelImage(T, 18);
            Box(body, 1, 2, 14, 1, 4, Wood);
            Box(body, 1, 8, 14, 3, 2, Wood);
            foreach (int x in new[] { 2, 12 }) Rect(body, x, 13, 2, 3, Wood.Dark);
            return Finish(body, ShadowLayer(T, 18, 9, 16, 7, 2));
        }

        // A clipped hedge: a leafy block, its top lit, its leaves picked out.
        static PixelImage Hedge()
        {
            var body = new PixelImage(T, 22);
            Box(body, 1, 3, 14, 6, 11, Leaf);
            for (int y = 3; y < 20; y++)
                for (int x = 1; x < 15; x++)
                    if (PixelNoise.Value(x, y, 31) > 0.78f) body[x, y] = y < 9 ? Leaf.Main : Leaf.Shade;
            Rect(body, 1, 2, 14, 1, Leaf.Light);
            return Finish(body, ShadowLayer(T, 22, 9, 20, 7, 2));
        }

        static PixelImage Flowerbed()
        {
            var body = new PixelImage(T, 18);
            Box(body, 1, 8, 14, 2, 7, Wood);
            Rect(body, 2, 8, 12, 2, DarkWood.Dark);
            var petals = new[] { Royal.Light, Gold.Light, Cream.Light, Violet.Light };
            for (int i = 0; i < 6; i++)
            {
                int x = 2 + i * 2 + (i % 2);
                Rect(body, x, 5 + i % 2, 1, 4, Leaf.Main);
                body[x, 3 + i % 2] = petals[i % petals.Length];
                body[x + 1, 4 + i % 2] = petals[i % petals.Length].Scale(0.85f);
                body[x - 1, 4 + i % 2] = petals[i % petals.Length];
            }
            return Finish(body, ShadowLayer(T, 18, 9, 16, 7, 2));
        }

        static PixelImage Woodpile()
        {
            var body = new PixelImage(T, 20);
            foreach (var (x, y) in new[] { (4, 15), (8, 15), (12, 15), (6, 11), (10, 11), (8, 7) })
            {
                Oval(body, x, y, 2.4f, 2.4f, Wood.Shade);
                Oval(body, x - 0.3f, y - 0.3f, 2, 2, Wood.Light);
                body[x, y] = Wood.Main;
            }
            return Finish(body, ShadowLayer(T, 20, 9, 18, 7, 2));
        }

        static PixelImage TrainingDummy()
        {
            var body = new PixelImage(T, 34);
            Column(body, 7, 18, 2, 14, Wood);
            Rect(body, 2, 14, 12, 2, Wood.Main);
            LitOval(body, 8, 18, 4, 6, Straw);
            Rect(body, 4, 16, 8, 1, DarkWood.Main);
            Rect(body, 4, 20, 8, 1, DarkWood.Main);
            LitOval(body, 8, 7, 3.5f, 3.5f, Straw);
            body[7, 6] = Ink; body[9, 8] = Ink; body[9, 6] = Ink; body[7, 8] = Ink; body[8, 7] = Ink;
            Rect(body, 6, 30, 4, 2, Wood.Dark);
            return Finish(body, ShadowLayer(T, 34, 9, 32, 5, 1.5f));
        }

        static PixelImage WeaponRack()
        {
            var body = new PixelImage(T, 30);
            Rect(body, 2, 6, 2, 22, Wood.Main);
            Rect(body, 12, 6, 2, 22, Wood.Shade);
            Rect(body, 2, 9, 12, 1, Wood.Dark);
            Rect(body, 2, 23, 12, 2, Wood.Main);
            Column(body, 5, 2, 2, 18, Iron);
            Rect(body, 4, 19, 4, 1, Gold.Main);
            Rect(body, 5, 20, 2, 4, DarkWood.Main);
            Column(body, 9, 4, 1, 22, Wood);
            body[9, 1] = Iron.Light; body[9, 2] = Iron.Main; body[8, 3] = Iron.Main; body[10, 3] = Iron.Shade;
            return Finish(body, ShadowLayer(T, 30, 9, 28, 7, 2));
        }

        static PixelImage Campfire()
        {
            var body = new PixelImage(T, 22);
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                LitOval(body, 8 + (float)Math.Cos(a) * 5.5f, 16 + (float)Math.Sin(a) * 2.6f, 1.6f, 1.3f, DarkStone);
            }
            Rect(body, 3, 15, 10, 2, Wood.Main);
            Rect(body, 5, 13, 6, 2, Wood.Shade);
            body[3, 15] = Wood.Light;
            for (int y = 3; y < 15; y++)
            {
                float half = (15 - y) * 0.32f;
                for (int x = (int)(8 - half); x <= (int)(8 + half); x++) body[x, y] = Math.Abs(x - 8) < half * 0.45f && y > 7 ? FlameCore : Flame;
            }
            return Finish(body, ShadowLayer(T, 22, 9, 19, 7, 2));
        }

        static PixelImage Bones()
        {
            var body = new PixelImage(T, 14);
            LitOval(body, 7, 6, 3.2f, 2.8f, Bone);
            body[6, 6] = Ink; body[8, 6] = Ink;
            Rect(body, 6, 8, 3, 1, Bone.Shade);
            for (int i = 0; i < 10; i++)
            {
                body[2 + i, 11 - i / 3] = Bone.Main;
                body[2 + i, 8 + i / 3] = Bone.Light;
            }
            return Finish(body, ShadowLayer(T, 14, 8, 12, 6, 1.5f));
        }

        // A dark crystal spire of the demons: facets lit violet on the left, deep on the right, on a rock.
        static PixelImage Spire()
        {
            var body = new PixelImage(T, 42);
            LitOval(body, 8, 36, 6, 3, DarkStone);
            for (int y = 1; y < 36; y++)
            {
                float half = Math.Min(4.5f, (y - 1) * 0.3f + 0.5f);
                for (int x = (int)Math.Round(8 - half); x <= (int)Math.Round(8 + half); x++)
                {
                    var color = x < 7 ? Violet.Light : x < 9 ? Violet.Main : x < 11 ? Violet.Shade : Violet.Dark;
                    if ((y + x) % 9 == 0 && x < 9) color = Rgba.White.Mix(Violet.Light, 0.4f);
                    body[x, y] = color;
                }
            }
            return Finish(body, ShadowLayer(T, 42, 9, 39, 6, 2));
        }

        static PixelImage SmallTent()
        {
            var body = new PixelImage(T, 24);
            for (int y = 3; y < 21; y++)
            {
                float half = (y - 3) * 0.42f;
                for (int x = (int)(8 - half); x <= (int)(8 + half); x++)
                    body[x, y] = Math.Abs(x - 8) < (y - 10) * 0.25f ? Ink : x < 8 ? DemonCloth.Main : DemonCloth.Shade;
            }
            Rect(body, 8, 1, 1, 3, DarkWood.Main);
            return Finish(body, ShadowLayer(T, 24, 9, 22, 7, 2));
        }

        // A great demon tent, two tiles by two: dark red cloth over crossed poles, its flap open on the dark inside,
        // a gold seam and a skull over the door.
        static PixelImage BigTent()
        {
            const int w = 32, h = 44;
            var body = new PixelImage(w, h);
            for (int y = 6; y < 40; y++)
            {
                float half = Math.Min(15, (y - 6) * 0.5f + 1);
                for (int x = (int)(16 - half); x <= (int)(16 + half); x++)
                {
                    float t = (x - (16 - half)) / Math.Max(1, half * 2);
                    var color = t < 0.25f ? DemonCloth.Light : t < 0.5f ? DemonCloth.Main : t < 0.8f ? DemonCloth.Shade : DemonCloth.Dark;
                    if ((x - 16) % 6 == 0) color = color.Scale(0.82f);
                    body[x, y] = color;
                }
            }
            for (int y = 22; y < 40; y++)
            {
                float half = (y - 22) * 0.34f;
                for (int x = (int)(16 - half); x <= (int)(16 + half); x++) body[x, y] = Ink;
            }
            for (int y = 8; y < 22; y++) body[16, y] = Gold.Main;
            Rect(body, 13, 0, 1, 9, DarkWood.Main);
            Rect(body, 18, 0, 1, 9, DarkWood.Shade);
            LitOval(body, 16, 17, 2.5f, 2.2f, Bone);
            body[15, 17] = Ink; body[17, 17] = Ink;
            return Finish(body, ShadowLayer(w, h, 17, 41, 15, 3));
        }

        // A totem of the demons: carved faces with glowing eyes up a pole, a horned skull on top.
        static PixelImage Totem()
        {
            var body = new PixelImage(T, 50);
            Column(body, 4, 12, 8, 34, DarkWood);
            foreach (int y in new[] { 18, 30 })
            {
                Rect(body, 4, y, 8, 1, DarkWood.Dark);
                body[6, y + 3] = Violet.Light;
                body[9, y + 3] = Violet.Light;
                Rect(body, 6, y + 6, 4, 1, DarkWood.Dark);
            }
            LitOval(body, 8, 8, 4, 4, Bone);
            body[6, 8] = Violet.Light; body[10, 8] = Violet.Light;
            for (int i = 0; i < 5; i++)
            {
                body[3 - i / 2, 6 - i] = Bone.Main;
                body[12 + i / 2, 6 - i] = Bone.Shade;
            }
            Box(body, 2, 45, 12, 1, 3, DarkStone);
            return Finish(body, ShadowLayer(T, 50, 9, 48, 7, 2));
        }

        static PixelImage Cage()
        {
            var body = new PixelImage(T, 30);
            Rect(body, 3, 9, 10, 15, new Rgba(30, 20, 30, 255));
            LitOval(body, 8, 20, 2, 2, Bone);
            foreach (int x in new[] { 3, 6, 9, 12 }) Column(body, x, 7, 1, 17, Iron);
            LitOval(body, 8, 6, 6, 3, Iron);
            Rect(body, 8, 1, 1, 3, Iron.Dark);
            Box(body, 2, 24, 12, 1, 3, Iron);
            return Finish(body, ShadowLayer(T, 30, 9, 28, 7, 2));
        }

        static PixelImage Brazier()
        {
            var body = new PixelImage(T, 30);
            for (int y = 15; y < 27; y++)
            {
                body[4 + (y - 15) / 4, y] = Iron.Main;
                body[11 - (y - 15) / 4, y] = Iron.Shade;
                body[8, y] = Iron.Dark;
            }
            LitOval(body, 8, 13, 6, 2.5f, Iron);
            for (int y = 2; y < 12; y++)
            {
                float half = (12 - y) * 0.42f;
                for (int x = (int)(8 - half); x <= (int)(8 + half); x++) body[x, y] = Math.Abs(x - 8) < half * 0.4f ? Rgba.White.Mix(Violet.Light, 0.5f) : Violet.Main;
            }
            return Finish(body, ShadowLayer(T, 30, 9, 28, 5, 1.5f));
        }

        // ---- The harbour, the ruined temple and the mining village (2026-11-10) ----

        // A rowing boat moored on the lake, three tiles by two: a painted hull seen from above and in front, its
        // planked inside, a mast with the sail furled, ripples at the waterline.
        static PixelImage Boat()
        {
            const int w = 48, h = 44;
            var body = new PixelImage(w, h);
            // The hull's side: a lens narrowing to the keel and to its two ends.
            for (int y = 26; y < 39; y++)
            {
                float half = 21 - Math.Max(0, y - 30) * 0.7f;
                for (int x = (int)(24 - half); x <= (int)(24 + half); x++)
                {
                    float t = (x - (24 - half)) / (half * 2);
                    var color = t < 0.2f ? Wood.Light : t < 0.75f ? Wood.Main : Wood.Shade;
                    if (y == 31 || y == 32) color = t < 0.75f ? Paint.Main : Paint.Shade;
                    if (y >= 36) color = color.Scale(0.8f);
                    body[x, y] = color;
                }
            }
            // The gunwale and the planked inside.
            LitOval(body, 24, 26, 21, 5, Wood);
            Oval(body, 24, 26.5f, 18, 3.5f, DarkWood.Main);
            for (int x = 10; x < 40; x += 5) Rect(body, x, 24, 1, 5, DarkWood.Dark);
            Rect(body, 12, 25, 24, 1, DarkWood.Shade);
            // The mast and the furled sail.
            Column(body, 22, 3, 2, 24, DarkWood);
            for (int y = 6; y < 20; y++)
                for (int x = 24; x < 24 + (20 - y) / 2 + 2; x++) body[x, y] = x < 26 ? Cream.Light : Cream.Main;
            Rect(body, 18, 8, 10, 1, DarkWood.Main);
            Rect(body, 23, 1, 4, 2, Royal.Main);
            // Ripples where the hull meets the water.
            for (int x = 4; x < 44; x++)
                if ((x / 3) % 2 == 0) body[x, 39 + (x / 7) % 2] = Water.Light;
            return Finish(body);
        }

        // A lighthouse on the shore, two tiles by two: a tapering tower in red and white bands, a lamp room with its
        // great light, an iron cap.
        static PixelImage Lighthouse()
        {
            const int w = 32, h = 80;
            var body = new PixelImage(w, h);
            for (int y = 20; y < 74; y++)
            {
                float half = 7 + (y - 20) * 0.11f;
                for (int x = (int)(16 - half); x <= (int)(16 + half); x++)
                {
                    float t = (x - (16 - half)) / (half * 2);
                    var ramp = (y - 20) / 9 % 2 == 0 ? Plaster : Royal;
                    body[x, y] = t < 0.25f ? ramp.Light : t < 0.68f ? ramp.Main : ramp.Shade;
                }
            }
            Rect(body, 14, 62, 4, 11, DarkWood.Main);
            Rect(body, 15, 40, 2, 4, Ink);
            Box(body, 7, 17, 18, 2, 3, Iron);
            Rect(body, 9, 8, 14, 9, WindowLight);
            Rect(body, 11, 9, 6, 6, WindowCore);
            foreach (int x in new[] { 9, 15, 22 }) Rect(body, x, 8, 1, 9, Iron.Dark);
            for (int y = 1; y < 8; y++)
            {
                float half = y * 1.2f;
                for (int x = (int)(16 - half); x <= (int)(16 + half); x++) body[x, y] = x < 15 ? Iron.Light : x < 18 ? Iron.Main : Iron.Shade;
            }
            Box(body, 4, 72, 24, 2, 5, Stone);
            return Finish(body, ShadowLayer(w, h, 17, h - 3, 14, 3));
        }

        // Nets drying on a frame of two posts.
        static PixelImage NetRack()
        {
            var body = new PixelImage(T, 30);
            Column(body, 1, 4, 2, 24, DarkWood);
            Column(body, 13, 4, 2, 24, DarkWood);
            Rect(body, 1, 4, 14, 2, Wood.Main);
            for (int y = 6; y < 22; y++)
                for (int x = 3; x < 13; x++)
                    if ((x + y) % 3 == 0 || (x - y + 30) % 3 == 0) body[x, y] = y > 19 ? Straw.Shade : Straw.Main;
            body[6, 12] = Royal.Main; body[10, 16] = Royal.Main;
            return Finish(body, ShadowLayer(T, 30, 9, 28, 7, 2));
        }

        // An iron mooring post with a rope wound round it.
        static PixelImage Bollard()
        {
            var body = new PixelImage(T, 16);
            Column(body, 5, 5, 6, 8, Iron);
            LitOval(body, 8, 5, 4, 2, Iron);
            foreach (int y in new[] { 8, 10 }) Rect(body, 5, y, 6, 1, Straw.Main);
            body[11, 9] = Straw.Shade; body[12, 10] = Straw.Shade;
            return Finish(body, ShadowLayer(T, 16, 9, 14, 5, 1.5f));
        }

        // A ship's anchor stood on its crown.
        static PixelImage Anchor()
        {
            var body = new PixelImage(T, 24);
            Oval(body, 8, 3, 2.5f, 2.5f, Iron.Main);
            Oval(body, 8, 3, 1.2f, 1.2f, new Rgba(0, 0, 0, 0));
            Rect(body, 3, 7, 10, 2, Iron.Light);
            Column(body, 7, 5, 2, 15, Iron);
            for (int i = 0; i < 5; i++)
            {
                body[7 - i, 19 - i / 2] = Iron.Main;
                body[8 + i, 19 - i / 2] = Iron.Shade;
            }
            Rect(body, 2, 15, 2, 2, Iron.Light);
            Rect(body, 12, 15, 2, 2, Iron.Shade);
            return Finish(body, ShadowLayer(T, 24, 9, 22, 6, 1.5f));
        }

        // A temple pillar broken off part way up, moss on its stones.
        static PixelImage BrokenPillar()
        {
            var body = new PixelImage(T, 30);
            Column(body, 4, 9, 8, 16, Stone);
            for (int x = 4; x < 12; x++)
            {
                int top = 9 - (int)(PixelNoise.Value(x, 3, 41) * 5);
                Rect(body, x, top, 1, 9 - top, x < 6 ? Stone.Light : x < 10 ? Stone.Main : Stone.Shade);
            }
            Box(body, 2, 24, 12, 2, 4, Stone);
            foreach (var (x, y) in new[] { (5, 14), (6, 15), (10, 19), (4, 22), (11, 11) }) body[x, y] = Leaf.Shade;
            return Finish(body, ShadowLayer(T, 30, 9, 28, 7, 2));
        }

        // A column fallen on its side, two tiles long, cracked through.
        static PixelImage FallenColumn()
        {
            const int w = 32, h = 18;
            var body = new PixelImage(w, h);
            for (int y = 4; y < 14; y++)
            {
                var color = y < 6 ? Stone.Light : y < 10 ? Stone.Main : y < 12 ? Stone.Shade : Stone.Dark;
                Rect(body, 4, y, 25, 1, color);
            }
            LitOval(body, 4, 9, 3, 5, Stone);
            Oval(body, 4, 9, 1.5f, 3, Stone.Shade);
            for (int x = 6; x < 28; x += 4) Rect(body, x, 5, 1, 8, Stone.Main.Scale(0.88f));
            for (int y = 4; y < 14; y++) body[17 + (y % 3) - 1, y] = Ink;
            foreach (var (x, y) in new[] { (9, 4), (10, 4), (22, 5), (25, 4) }) body[x, y] = Leaf.Main;
            return Finish(body, ShadowLayer(w, h, 17, 15, 14, 2.5f));
        }

        // Broken stones lying about.
        static PixelImage Rubble()
        {
            var body = new PixelImage(T, 12);
            LitOval(body, 5, 7, 3, 2.5f, Stone);
            LitOval(body, 10, 8, 2.5f, 2, DarkStone);
            LitOval(body, 8, 5, 2, 1.6f, Stone);
            return Finish(body, ShadowLayer(T, 12, 8, 10, 6, 1.5f));
        }

        // A standing stone carved with a rune that still glows.
        static PixelImage RuneStone()
        {
            var body = new PixelImage(T, 36);
            for (int y = 3; y < 32; y++)
            {
                float half = Math.Min(5, 3 + (y - 3) * 0.12f);
                for (int x = (int)(8 - half); x <= (int)(8 + half); x++)
                {
                    float t = (x - (8 - half)) / (half * 2);
                    body[x, y] = t < 0.3f ? DarkStone.Light : t < 0.7f ? DarkStone.Main : DarkStone.Shade;
                }
            }
            foreach (var (x, y) in new[] { (8, 10), (8, 11), (8, 12), (8, 13), (8, 14), (8, 15), (7, 11), (6, 12), (9, 13), (10, 14), (7, 17), (8, 18), (9, 19) })
                body[x, y] = y < 15 ? RuneCore : Rune;
            Box(body, 2, 30, 12, 1, 3, DarkStone);
            return Finish(body, ShadowLayer(T, 36, 9, 34, 7, 2));
        }

        // The temple's altar, three tiles across: a stone block carved with glowing runes, cracked on top, an orb of
        // light resting on it.
        static PixelImage Altar()
        {
            const int w = 48, h = 32;
            var body = new PixelImage(w, h);
            Box(body, 3, 12, 42, 6, 12, Stone);
            Rect(body, 6, 20, 36, 1, Stone.Dark);
            for (int x = 8; x < 40; x += 6)
            {
                body[x, 23] = Rune; body[x + 1, 24] = Rune; body[x + 2, 23] = Rune; body[x + 1, 22] = RuneCore;
            }
            for (int i = 0; i < 6; i++) body[28 + i, 13 + i / 2] = Stone.Dark;
            LitOval(body, 24, 9, 4, 4, Violet);
            Oval(body, 23, 8, 1.5f, 1.5f, RuneCore);
            Rect(body, 22, 12, 5, 1, Stone.Shade);
            return Finish(body, ShadowLayer(w, h, 25, h - 3, 21, 3));
        }

        // The mouth of the mine, three tiles by two: rock heaped round a timber frame, the dark tunnel inside, the
        // rails running in, a lantern hung from the lintel.
        static PixelImage MineEntrance()
        {
            const int w = 48, h = 52;
            var body = new PixelImage(w, h);
            LitOval(body, 24, 24, 23, 20, DarkStone);
            for (int y = 8; y < 44; y += 3)
                for (int x = 2; x < 46; x++)
                    if (PixelNoise.Value(x, y, 51) > 0.82f && body[x, y].A > 0) body[x, y] = DarkStone.Dark;
            Rect(body, 13, 18, 22, 30, new Rgba(12, 8, 10, 255));
            for (int y = 18; y < 48; y++)
                for (int x = 13; x < 35; x++)
                    if (y > 38) body[x, y] = new Rgba(30, 22, 20, 255);
            Column(body, 10, 16, 4, 32, Wood);
            Column(body, 34, 16, 4, 32, Wood);
            Box(body, 7, 12, 34, 2, 4, Wood);
            foreach (int x in new[] { 20, 27 }) Rect(body, x, 40, 1, 8, Iron.Light);
            for (int y = 41; y < 48; y += 3) Rect(body, 18, y, 12, 1, DarkWood.Main);
            Rect(body, 31, 18, 1, 3, Iron.Dark);
            Rect(body, 29, 21, 5, 5, WindowLight);
            Rect(body, 30, 22, 2, 2, WindowCore);
            return Finish(body, ShadowLayer(w, h, 25, h - 3, 22, 3));
        }

        // A mine cart of iron heaped with ore.
        static PixelImage MineCart()
        {
            var body = new PixelImage(T, 20);
            Box(body, 2, 7, 12, 3, 7, Iron);
            foreach (var (x, y, ramp) in new[] { (5, 6, Copper), (9, 5, Copper), (11, 7, Iron), (7, 4, Copper) }) LitOval(body, x, y, 2, 1.6f, ramp);
            foreach (int x in new[] { 4, 11 }) Oval(body, x, 17, 1.8f, 1.8f, Ink);
            return Finish(body, ShadowLayer(T, 20, 9, 18, 7, 1.5f));
        }

        // A heap of ore dug out of the mine.
        static PixelImage OrePile()
        {
            var body = new PixelImage(T, 16);
            LitOval(body, 8, 10, 7, 4.5f, DarkStone);
            foreach (var (x, y) in new[] { (5, 8), (9, 7), (11, 10), (7, 11), (4, 11) }) body[x, y] = Copper.Light;
            foreach (var (x, y) in new[] { (8, 9), (12, 9), (6, 10) }) body[x, y] = Copper.Main;
            return Finish(body, ShadowLayer(T, 16, 9, 14, 7, 1.5f));
        }

        // A miner's lantern hung from the arm of a timber post.
        static PixelImage Lantern()
        {
            var body = new PixelImage(T, 36);
            Column(body, 4, 6, 3, 28, Wood);
            Rect(body, 4, 6, 9, 2, Wood.Main);
            Rect(body, 11, 8, 1, 2, Iron.Dark);
            Rect(body, 9, 10, 5, 6, WindowLight);
            Rect(body, 10, 11, 2, 2, WindowCore);
            Rect(body, 9, 10, 5, 1, Iron.Dark);
            Rect(body, 9, 15, 5, 1, Iron.Dark);
            Box(body, 2, 32, 7, 1, 2, DarkWood);
            return Finish(body, ShadowLayer(T, 36, 7, 34, 5, 1.5f));
        }

        // ---- The things players build (2026-11-10) ----

        // A workbench: a plank top with a saw and a hammer on it, a vise at one end, stout legs and a shelf.
        static PixelImage Workbench()
        {
            var body = new PixelImage(T, 22);
            Box(body, 1, 6, 14, 4, 4, Wood);
            foreach (int x in new[] { 2, 12 }) Column(body, x, 14, 2, 6, DarkWood);
            Rect(body, 3, 17, 10, 1, DarkWood.Main);
            Rect(body, 3, 7, 6, 1, Iron.Light);
            for (int i = 0; i < 3; i++) body[4 + i * 2, 8] = Iron.Shade;
            Rect(body, 10, 5, 1, 4, DarkWood.Main);
            Rect(body, 9, 5, 3, 1, Iron.Main);
            Box(body, 12, 3, 3, 2, 2, Iron);
            return Finish(body, ShadowLayer(T, 22, 9, 20, 7, 2));
        }

        // A furnace of stone: a block with the fire glowing in its arched mouth, a chimney at the back.
        static PixelImage Furnace()
        {
            var body = new PixelImage(T, 26);
            Column(body, 10, 1, 4, 8, DarkStone);
            Box(body, 1, 6, 14, 3, 16, Stone);
            for (int y = 11; y < 21; y += 3)
                for (int x = 2; x < 14; x++)
                    if ((x + y) % 5 == 0) body[x, y] = Stone.Shade;
            Rect(body, 5, 13, 6, 7, Ink);
            Rect(body, 6, 12, 4, 1, Ink);
            Rect(body, 6, 15, 4, 5, Flame);
            Rect(body, 7, 16, 2, 3, FlameCore);
            return Finish(body, ShadowLayer(T, 26, 9, 24, 7, 2));
        }

        // A wooden chest bound in iron with a gold lock.
        static PixelImage Chest()
        {
            var body = new PixelImage(T, 18);
            Box(body, 1, 3, 14, 4, 9, Wood);
            Rect(body, 1, 7, 14, 1, Wood.Dark);
            foreach (int x in new[] { 3, 12 }) Rect(body, x, 3, 1, 13, Iron.Main);
            Rect(body, 7, 7, 2, 3, Gold.Main);
            body[7, 7] = Gold.Light;
            return Finish(body, ShadowLayer(T, 18, 9, 16, 7, 2));
        }

        // An anvil on a stump: the face and horn of iron, the waist narrowing to the foot.
        static PixelImage Anvil()
        {
            var body = new PixelImage(T, 20);
            Column(body, 4, 11, 8, 7, Wood);
            LitOval(body, 8, 11, 4, 1.5f, Wood);
            Rect(body, 6, 7, 4, 4, Iron.Shade);
            Box(body, 3, 3, 10, 2, 3, Iron);
            for (int i = 0; i < 3; i++) Rect(body, 0 + i, 4, 3 - i, 1, Iron.Main);
            body[1, 4] = Iron.Light;
            return Finish(body, ShadowLayer(T, 20, 9, 18, 6, 2));
        }

        // A bed: the headboard at the back, a pillow, a blanket over the mattress, the frame's foot in front.
        static PixelImage Bed()
        {
            var body = new PixelImage(T, 24);
            Box(body, 1, 1, 14, 2, 6, Wood);
            Box(body, 2, 7, 12, 12, 2, Cream);
            LitOval(body, 8, 9, 4, 1.8f, Cream);
            Rect(body, 2, 12, 12, 7, Paint.Main);
            Rect(body, 2, 12, 12, 1, Paint.Light);
            Rect(body, 12, 12, 2, 7, Paint.Shade);
            Box(body, 1, 18, 14, 1, 3, Wood);
            return Finish(body, ShadowLayer(T, 24, 9, 22, 7, 2));
        }

        // A stone table: a slab on two thick legs.
        static PixelImage StoneTable()
        {
            var body = new PixelImage(T, 18);
            Box(body, 1, 4, 14, 4, 3, Stone);
            foreach (int x in new[] { 3, 10 }) Column(body, x, 11, 3, 5, Stone);
            return Finish(body, ShadowLayer(T, 18, 9, 16, 7, 2));
        }

        // A torch on a short post, its flame burning.
        static PixelImage Torch()
        {
            var body = new PixelImage(T, 24);
            Column(body, 7, 9, 2, 13, Wood);
            Rect(body, 6, 8, 4, 2, Iron.Main);
            for (int y = 1; y < 8; y++)
            {
                int half = y < 3 ? 0 : y < 6 ? 1 : 2;
                for (int x = 8 - half; x <= 7 + half; x++) body[x, y] = Math.Abs(x - 7.5f) < half * 0.6f || y < 4 ? FlameCore : Flame;
            }
            return Finish(body, ShadowLayer(T, 24, 9, 22, 4, 1.5f));
        }

        // A lamp of glowing mushrooms: a pale stem and a bright cyan cap.
        static PixelImage GlowLamp()
        {
            var body = new PixelImage(T, 22);
            Column(body, 7, 9, 3, 11, Cream);
            var glow = new Ramp(Rgba.Hex("#E2FFF8"), Rgba.Hex("#6FE8D0"), Rgba.Hex("#3AA898"), Rgba.Hex("#1E6A60"));
            LitOval(body, 8, 7, 6, 4, glow);
            foreach (var (x, y) in new[] { (5, 6), (9, 5), (11, 8) }) body[x, y] = Rgba.White;
            Box(body, 4, 19, 8, 1, 2, DarkStone);
            return Finish(body, ShadowLayer(T, 22, 9, 20, 6, 2));
        }

        // A clay pot of cave mushrooms.
        static PixelImage MushroomPot()
        {
            var body = new PixelImage(T, 20);
            Column(body, 3, 11, 10, 7, Copper);
            LitOval(body, 8, 11, 5, 1.8f, Copper);
            Oval(body, 8, 11, 4, 1.2f, DarkWood.Dark);
            foreach (var (x, top) in new[] { (5, 5), (9, 3), (11, 7) })
            {
                Rect(body, x, top + 3, 1, 10 - top - 2, Cream.Main);
                LitOval(body, x + 0.5f, top + 2, 2.5f, 1.6f, Mushroom);
            }
            return Finish(body, ShadowLayer(T, 20, 9, 18, 6, 2));
        }

        // A ward stone: a standing stone with a cyan rune that wards the base.
        static PixelImage WardStone()
        {
            var body = new PixelImage(T, 30);
            for (int y = 3; y < 26; y++)
            {
                float half = Math.Min(5, 3 + (y - 3) * 0.15f);
                for (int x = (int)(8 - half); x <= (int)(8 + half); x++)
                {
                    float t = (x - (8 - half)) / (half * 2);
                    body[x, y] = t < 0.3f ? Stone.Light : t < 0.7f ? Stone.Main : Stone.Shade;
                }
            }
            var ward = Rgba.Hex("#8CE0FF");
            foreach (var (x, y) in new[] { (8, 8), (7, 9), (9, 9), (6, 10), (10, 10), (8, 11), (8, 12), (8, 13), (7, 14), (9, 14), (8, 16) }) body[x, y] = y < 12 ? RuneCore : ward;
            Box(body, 2, 24, 12, 1, 3, Stone);
            return Finish(body, ShadowLayer(T, 30, 9, 28, 7, 2));
        }

        // The rift altar: a dark stone altar with a shard of the void floating over it.
        static PixelImage RiftAltar()
        {
            var body = new PixelImage(T, 26);
            Box(body, 1, 13, 14, 3, 8, DarkStone);
            Rect(body, 3, 18, 10, 1, Violet.Shade);
            for (int y = 2; y < 10; y++)
            {
                int half = y < 6 ? (y - 2) / 2 : (10 - y) / 2;
                for (int x = 8 - half; x <= 8 + half; x++) body[x, y] = x < 8 ? Violet.Light : Violet.Main;
            }
            body[7, 4] = Rgba.White;
            return Finish(body, ShadowLayer(T, 26, 9, 24, 7, 2));
        }

        // ---- The floor under things ----

        // What floor runs under the thing at (x, y), or null for none: in a place of the story, its own floor; else
        // a laid floor that two or more of its four neighbours have (a table in a paved room stands on paving).
        public static TileType? Underlay(TileMap map, IReadOnlyList<PlaceSite> places, int x, int y)
        {
            var here = map[x, y];
            if (here == TileType.Boat || here == TileType.StructurePart && AnchorOf(map, x, y) == TileType.Boat) return TileType.Water;
            if (here == TileType.Floor || Structures.IsWay(here) || TileMap.IsPaving(here) || TileMap.IsSoil(here) || here == TileType.Wall
                || here == TileType.Water || here == TileType.Lava || here.ToString().EndsWith("Ore"))
                return null;
            if (places != null)
            {
                foreach (var site in places)
                {
                    int dx = x - site.X, dy = y - site.Y;
                    if (dx * dx + dy * dy <= site.Radius * site.Radius) return site.Definition.Floor == TileType.Floor ? (TileType?)null : site.Definition.Floor;
                }
            }
            TileType? best = null;
            int most = 1;
            foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
            {
                var near = map[nx, ny];
                if (!TileMap.IsPaving(near)) continue;
                int count = 0;
                foreach (var (mx, my) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) }) if (map[mx, my] == near) count++;
                if (count > most)
                {
                    most = count;
                    best = near;
                }
            }
            return best;
        }

        // The type of the structure a StructurePart tile belongs to, from its lower-left tile.
        public static TileType? AnchorOf(TileMap map, int x, int y)
        {
            for (int j = 0; j < 3; j++)
            {
                for (int i = 0; i < 4; i++)
                {
                    var t = map[x - i, y - j];
                    if (t == TileType.StructurePart) continue;
                    var (w, h) = Structures.Footprint(t);
                    if (i < w && j < h && (w > 1 || h > 1)) return t;
                }
            }
            return null;
        }

        // Which of a wall's four sides meet the same wall: north 1, east 2, south 4, west 8.
        public static int Sides(TileMap map, int x, int y)
        {
            var here = map[x, y];
            return (map[x, y + 1] == here ? 1 : 0) | (map[x + 1, y] == here ? 2 : 0) | (map[x, y - 1] == here ? 4 : 0) | (map[x - 1, y] == here ? 8 : 0);
        }

        public static bool IsWall(TileType type) => type == TileType.CastleWall || type == TileType.TimberWall;

        // The lights the structures give (WorldLights): radius and colour.
        public static readonly (TileType Type, float Radius, string Color)[] Lights =
        {
            (TileType.Campfire, 6, "#FF9A3C"),
            (TileType.Spire, 4.5f, "#B07AFF"),
            (TileType.Fountain, 2.5f, "#8AC8FF"),
            (TileType.BigFountain, 3.5f, "#8AC8FF"),
            (TileType.LampPost, 5.5f, "#FFD27A"),
            (TileType.Brazier, 5, "#C08AFF"),
            (TileType.Cottage, 3.5f, "#FFC27A"),
            (TileType.Cabin, 3.5f, "#FFC27A"),
            (TileType.Lighthouse, 7, "#FFE0A0"),
            (TileType.RuneStone, 3.5f, "#8AF0FF"),
            (TileType.Altar, 4.5f, "#B08AFF"),
            (TileType.Lantern, 5, "#FFC27A"),
            (TileType.MineEntrance, 3, "#FFC27A"),
        };
    }
}
