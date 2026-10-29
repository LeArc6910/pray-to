using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game.Client
{
    // Stand-in graphics drawn from code until there is art: white shapes tinted by their SpriteRenderer, and two
    // tiles. Every sprite is one world unit (one tile) across.
    static class Shapes
    {
        const int Size = 64;
        const float ArcHalfAngle = 60 * Mathf.Deg2Rad;

        static Sprite _circle;
        static Sprite _ring;
        static Sprite _arc;
        static Sprite _square;
        static readonly System.Collections.Generic.Dictionary<int, Sprite> Wedges = new System.Collections.Generic.Dictionary<int, Sprite>();

        public static Sprite Circle => _circle ??= Make((u, v) => Edge(1 - Radius(u, v)));

        public static Sprite Ring => _ring ??= Make((u, v) => Mathf.Min(Edge(1 - Radius(u, v)), Edge(Radius(u, v) - 0.75f)));

        // A 120 degree slice of a ring pointing along +x, the shape of a melee swing.
        public static Sprite Arc => _arc ??= Make((u, v) =>
        {
            float radius = Radius(u, v);
            float angle = Mathf.Abs(Mathf.Atan2(v, u));
            return Mathf.Min(Edge(1 - radius), Edge(radius - 0.45f), Edge((ArcHalfAngle - angle) * 4));
        });

        public static Sprite Square => _square ??= Make((u, v) => 1);

        // A slice of a disc pointing along +x with its tip at the centre, for cone telegraphs.
        public static Sprite Wedge(float degrees)
        {
            int key = Mathf.RoundToInt(degrees);
            if (Wedges.TryGetValue(key, out var sprite)) return sprite;
            float half = key * 0.5f * Mathf.Deg2Rad;
            sprite = Make((u, v) => Mathf.Min(Edge(1 - Radius(u, v)), key >= 360 ? 1 : Edge((half - Mathf.Abs(Mathf.Atan2(v, u))) * 4)));
            Wedges[key] = sprite;
            return sprite;
        }

        public static Tile FloorTile() => MakeTile(new Color32(58, 47, 40, 255), new Color32(66, 54, 46, 255), raised: false);

        public static Tile WallTile() => MakeTile(new Color32(104, 90, 76, 255), new Color32(132, 116, 98, 255), raised: true);

        // Ore veins: rock with flecks in the ore's colour, so they stand out in a wall.
        public static Tile OreTile(Color32 ore) => MakeTile(new Color32(104, 90, 76, 255), ore, raised: true, flecks: true);

        // Placed objects, on a darker ground so they read as things standing on the floor rather than rock: the
        // workbench in wood, the furnace grey with fire, the chest in gold-trimmed wood.
        public static Tile WorkbenchTile() => MakeTile(new Color32(110, 76, 44, 255), Palette.Workbench, raised: true);

        public static Tile FurnaceTile() => MakeTile(new Color32(84, 84, 90, 255), Palette.Furnace, raised: true, flecks: true);

        public static Tile ChestTile() => MakeTile(new Color32(120, 78, 40, 255), Palette.Chest, raised: true);

        // Machines: the drill in steel, the smelter like a hotter furnace, the conveyor a dark belt flat on the floor.
        public static Tile DrillTile() => MakeTile(new Color32(80, 86, 96, 255), Palette.Drill, raised: true, flecks: true);

        public static Tile SmelterTile() => MakeTile(new Color32(96, 70, 60, 255), Palette.Smelter, raised: true, flecks: true);

        public static Tile ConveyorTile() => MakeTile(Palette.Conveyor, new Color32(110, 110, 120, 255), raised: false);

        // The boss arena's floor, darker and redder so it reads as a different place.
        public static Tile ArenaTile() => MakeTile(new Color32(52, 34, 34, 255), new Color32(64, 42, 40, 255), raised: false);

        static float Radius(float u, float v) => Mathf.Sqrt(u * u + v * v);

        // Soft one-pixel edge: 1 inside, 0 outside.
        static float Edge(float inside) => Mathf.Clamp01(inside * Size / 2f);

        static Sprite Make(Func<float, float, float> alpha)
        {
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size * 2 - 1;
                    float v = (y + 0.5f) / Size * 2 - 1;
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(u, v)) * 255));
                }
            }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }

        // A 16-pixel tile with a speckled face. Walls get a lighter top edge so they read as raised. Flecks are
        // bigger, denser specks, for ore.
        static Tile MakeTile(Color32 dark, Color32 light, bool raised, bool flecks = false)
        {
            const int tileSize = 16;
            var pixels = new Color32[tileSize * tileSize];
            for (int y = 0; y < tileSize; y++)
            {
                for (int x = 0; x < tileSize; x++)
                {
                    bool speck = flecks ? ((x / 2 * 5 + y / 2 * 3) % 4) == 0 : ((x * 7 + y * 13) % 11) == 0;
                    bool top = raised && y >= tileSize - 3;
                    pixels[y * tileSize + x] = top || speck ? light : dark;
                }
            }
            var texture = new Texture2D(tileSize, tileSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply();

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = Sprite.Create(texture, new Rect(0, 0, tileSize, tileSize), new Vector2(0.5f, 0.5f), tileSize);
            tile.colliderType = Tile.ColliderType.None;
            return tile;
        }
    }
}
