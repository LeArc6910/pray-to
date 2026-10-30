using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game.Client
{
    // Turns the pixel art in Art/ into Unity sprites and tiles: 16 pixels to a world unit (one tile), point
    // filtered so pixels stay square. Sprites are made once and kept for the rest of the run. The cave tiles share
    // one atlas texture so the tilemap draws in few batches; each tile in it has a one-pixel border copied from its
    // edge so neighbouring tiles never bleed in.
    static class Sprites
    {
        public const int PixelsPerUnit = TileArt.Size;
        const int Cell = TileArt.Size + 2;

        static readonly Dictionary<string, Sprite> Made = new Dictionary<string, Sprite>();
        static Texture2D _atlas;
        static Tile[] _ground;
        static readonly Dictionary<TileType, Tile> Objects = new Dictionary<TileType, Tile>();
        static readonly TileType[] ObjectTypes =
        {
            TileType.Workbench, TileType.Furnace, TileType.Chest, TileType.Drill, TileType.Smelter, TileType.Conveyor, TileType.Torch,
        };

        // A sprite made from the drawing the first time its key is asked for. The pivot is in the sprite's own
        // terms, (0.5, 0) for the middle of its bottom edge.
        public static Sprite Get(string key, Func<PixelImage> draw, Vector2 pivot)
        {
            // Unity's null check: a sprite whose texture was destroyed is made again.
            if (Made.TryGetValue(key, out var sprite) && sprite != null) return sprite;
            var image = draw();
            var texture = Texture(image.Width, image.Height);
            texture.SetPixels32(Pixels(image));
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, image.Width, image.Height), pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            Made[key] = sprite;
            return sprite;
        }

        public static Sprite Centered(string key, Func<PixelImage> draw) => Get(key, draw, new Vector2(0.5f, 0.5f));

        public static Sprite Standing(string key, Func<PixelImage> draw) => Get(key, draw, new Vector2(0.5f, 0));

        // Icons for loot, loads and the bag window, by what they are. These run every frame, so a known icon is
        // found without building its key.
        static readonly Dictionary<object, Sprite> Icons = new Dictionary<object, Sprite>();

        public static Sprite Icon(ItemBase item) => Known(item) ?? MakeIcon(item);

        public static Sprite Icon(RuneDefinition rune) => Known(rune) ?? MakeIcon(rune);

        public static Sprite Icon(ConsumableDefinition consumable) => Known(consumable) ?? MakeIcon(consumable);

        public static Sprite Icon(MaterialDefinition material) => Known(material) ?? MakeIcon(material);

        // Kept apart from Icon, whose fast path would otherwise allocate the closures these need.
        static Sprite MakeIcon(ItemBase item) => Remember(item, Centered("gear/" + item.Id, () => ItemArt.Gear(item)));

        static Sprite MakeIcon(RuneDefinition rune) => Remember(rune, Centered("rune/" + rune.Id, () => ItemArt.Rune(rune)));

        static Sprite MakeIcon(ConsumableDefinition consumable) =>
            Remember(consumable, Centered("consumable/" + consumable.Id, () => ItemArt.Consumable(consumable)));

        static Sprite MakeIcon(MaterialDefinition material) =>
            Remember(material, Centered("material/" + material.Id, () => ItemArt.Material(material)));

        static Sprite Known(object definition) => Icons.TryGetValue(definition, out var sprite) && sprite != null ? sprite : null;

        static Sprite Remember(object definition, Sprite sprite) => Icons[definition] = sprite;

        // Draws a sprite as large as fits in a GUI area, centred, in white whatever GUI.color is.
        public static void DrawInGui(Rect area, Sprite sprite)
        {
            if (sprite == null) return;
            var texture = sprite.texture;
            var rect = sprite.textureRect;
            float scale = Mathf.Min(area.width / rect.width, area.height / rect.height);
            var size = new Vector2(rect.width * scale, rect.height * scale);
            var target = new Rect(area.center - size / 2, size);
            var uv = new Rect(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height);
            var previous = GUI.color;
            GUI.color = new Color(1, 1, 1, previous.a);
            GUI.DrawTextureWithTexCoords(target, texture, uv);
            GUI.color = previous;
        }

        // The ground tile for an index from TileArt.GroundAt.
        public static TileBase Ground(int index)
        {
            BuildAtlas();
            return _ground[index];
        }

        // The tile for a placed thing, or null for tiles that are only ground.
        public static TileBase Object(TileType type)
        {
            BuildAtlas();
            return Objects.TryGetValue(type, out var tile) ? tile : null;
        }

        static void BuildAtlas()
        {
            if (_atlas != null) return;
            int count = TileArt.GroundCount + ObjectTypes.Length;
            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            int size = Mathf.NextPowerOfTwo(columns * Cell);
            var atlas = new PixelImage(size, size);
            var images = new PixelImage[count];
            for (int i = 0; i < TileArt.GroundCount; i++) images[i] = TileArt.Ground(i);
            for (int i = 0; i < ObjectTypes.Length; i++) images[TileArt.GroundCount + i] = TileArt.Object(ObjectTypes[i]);
            for (int i = 0; i < count; i++) Extruded(atlas, images[i], i % columns * Cell, i / columns * Cell);

            _atlas = Texture(size, size);
            _atlas.SetPixels32(Pixels(atlas));
            _atlas.Apply(false, true);

            Tile MakeTile(int i)
            {
                // The atlas image has row 0 at the top and Unity counts from the bottom.
                int x = i % columns * Cell + 1;
                int y = size - (i / columns * Cell + 1) - TileArt.Size;
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = Sprite.Create(_atlas, new Rect(x, y, TileArt.Size, TileArt.Size), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
                tile.colliderType = Tile.ColliderType.None;
                return tile;
            }

            _ground = new Tile[TileArt.GroundCount];
            for (int i = 0; i < TileArt.GroundCount; i++) _ground[i] = MakeTile(i);
            for (int i = 0; i < ObjectTypes.Length; i++) Objects[ObjectTypes[i]] = MakeTile(TileArt.GroundCount + i);
        }

        // Draws the image into the atlas with its edge pixels repeated one pixel outward.
        static void Extruded(PixelImage atlas, PixelImage image, int left, int top)
        {
            for (int y = -1; y <= image.Height; y++)
            {
                for (int x = -1; x <= image.Width; x++)
                {
                    atlas[left + 1 + x, top + 1 + y] = image[Mathf.Clamp(x, 0, image.Width - 1), Mathf.Clamp(y, 0, image.Height - 1)];
                }
            }
        }

        static Texture2D Texture(int width, int height) => new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        // Unity's rows run bottom to top.
        static Color32[] Pixels(PixelImage image)
        {
            var pixels = new Color32[image.Width * image.Height];
            for (int y = 0; y < image.Height; y++)
            {
                int row = (image.Height - 1 - y) * image.Width;
                for (int x = 0; x < image.Width; x++)
                {
                    var p = image[x, y];
                    pixels[row + x] = new Color32(p.R, p.G, p.B, p.A);
                }
            }
            return pixels;
        }

        // The hex form of a colour, for sprite keys.
        public static string Key(Rgba color) => $"{color.R:X2}{color.G:X2}{color.B:X2}";

        public static Rgba ToRgba(Color color) => new Rgba(
            Mathf.RoundToInt(color.r * 255), Mathf.RoundToInt(color.g * 255), Mathf.RoundToInt(color.b * 255), Mathf.RoundToInt(color.a * 255));
    }
}
