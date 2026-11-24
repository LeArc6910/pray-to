using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // What stands on the floor (Docs/BiomeRework.md 4-3·4-4, 묶음 B0), drawn as things apart from the tiles: the places'
    // structures, what players build that stands, fences, hedges and walls joined to their neighbours (Joins), a gate
    // where a door stands in a fence, and the biomes' decor (Decor, worked out on the client from the seed). Each is
    // sorted by its foot's y to the pixel (FootOrder) with the players, monsters and people, so a fence hides the legs
    // of someone just behind it and a house's roof the head of someone behind the house; a canopy or roof fades while a
    // player's or a monster's feet are behind it ([설정] 물체 뒤 흐리게).
    // For a browser: what stands in a chunk is listed once, when the chunk is laid or a tile in it (or within two of
    // it) changes, not looked for every frame; the sprites are pooled renderers handed out to what is on screen (and a
    // little round it) each frame, at most StandCap of them, the nearest first; and the pictures share a few atlas
    // pages (PropAtlas).
    partial class WorldView
    {
        // ---- Depth ----

        // Orders are kept round the middle of a band below the overlays (DarkOrder and up) and above the floor's own
        // marks (40 and below): the origin follows the player when they go further than DepthReach from it, so the band
        // never runs out however tall the map (a dungeon's pocket is above row 1024).
        const int DepthMiddle = 4600, DepthLowest = 100, DepthHighest = 8950;
        const float DepthReach = 48;
        float _depthOrigin = float.NaN;

        void SetDepthOrigin(float y)
        {
            if (float.IsNaN(_depthOrigin) || Mathf.Abs(y - _depthOrigin) > DepthReach) _depthOrigin = Mathf.Round(y);
        }

        // The sorting order of what has its foot at that y: one pixel lower on the screen is in front, and at one row
        // the floor's decor, then props, then bodies (StandArt ties).
        int FootOrder(float footY, int tie = StandArt.BodyTie) =>
            Mathf.Clamp(DepthMiddle - Mathf.RoundToInt((footY - _depthOrigin) * StandArt.Pixels) * StandArt.Ties + tie, DepthLowest, DepthHighest);

        // ---- The standing layer ----

        struct Stand
        {
            public int X, Y;
            public TileType Type;
            public StandArt.Kind Kind;
            public DecorPick Decor;
            public Biome Biome;
        }

        readonly Dictionary<(int X, int Y), List<Stand>> _standChunks = new Dictionary<(int, int), List<Stand>>();
        readonly HashSet<(int X, int Y)> _standDirty = new HashSet<(int, int)>();
        readonly List<(int X, int Y)> _standDone = new List<(int, int)>();
        readonly List<SpriteRenderer> _standPool = new List<SpriteRenderer>();
        readonly List<Stand> _standsInView = new List<Stand>(512);
        readonly List<Stand> _decorInView = new List<Stand>(256);
        readonly Dictionary<int, float> _fades = new Dictionary<int, float>();
        readonly HashSet<int> _fadeSeen = new HashSet<int>();
        readonly List<int> _fadeGone = new List<int>();
        Func<int, int, bool> _keptClear;
        bool _windowsLit;
        int _standUsed;
        Vector2 _standFrom;

        // For the playtests: what the layer drew last frame, the faded canopies, the cap and how many were left out.
        public int PropsDrawn { get; private set; }
        public int DecorDrawn { get; private set; }
        public int CanopiesFaded { get; private set; }
        public int StandsSkipped { get; private set; }
        public int StandRenderers => _standUsed;
        public int StandsListed { get; private set; }

        // On a phone fewer sprites (Docs/BiomeRework.md 4-3 웹 성능 3).
        static int StandCap => Controls.UsingTouch || Application.isMobilePlatform ? 250 : 400;

        // Whether canopies and roofs fade at all ([설정] 물체 뒤 흐리게), and at once (the playtests, 움직임: 줄이기).
        static bool FadeBehind => Settings.FadeBehind;
        static bool FadeAtOnce => Ui.Probing || Settings.ReduceMotion;

        // A chunk laid or cleared (LayChunk): its list is made again before it is next drawn, or forgotten.
        void StandsLaid(int chunkX, int chunkY, bool lay)
        {
            if (lay) _standDirty.Add((chunkX, chunkY));
            else
            {
                _standChunks.Remove((chunkX, chunkY));
                _standDirty.Remove((chunkX, chunkY));
            }
        }

        // A tile changed: the lists of the chunks within two tiles of it (a fence's gate, decor kept two off a door).
        void StandsChanged(int x, int y)
        {
            for (int dy = -2; dy <= 2; dy += 4)
                for (int dx = -2; dx <= 2; dx += 4)
                {
                    int cx = Math.Max(0, x + dx) / ChunkSize, cy = Math.Max(0, y + dy) / ChunkSize;
                    if (_chunks.Contains((cx, cy))) _standDirty.Add((cx, cy));
                }
        }

        void ListStands(int chunkX, int chunkY)
        {
            var map = _client.Map;
            if (!_standChunks.TryGetValue((chunkX, chunkY), out var list)) _standChunks[(chunkX, chunkY)] = list = new List<Stand>();
            list.Clear();
            _keptClear ??= (x, y) => InArena(_client, x, y) || InPlace(x, y);
            int x0 = chunkX * ChunkSize, y0 = chunkY * ChunkSize;
            int x1 = Math.Min(x0 + ChunkSize, map.Width), y1 = Math.Min(y0 + ChunkSize, map.Rows);
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    var type = map[x, y];
                    var kind = StandArt.KindAt(map, x, y);
                    if (kind != StandArt.Kind.None)
                    {
                        list.Add(new Stand { X = x, Y = y, Type = type, Kind = kind });
                        continue;
                    }
                    if (type != TileType.Floor) continue;
                    var biome = _client.Biomes[x, y];
                    var pick = StandArt.DecorAt(map, _client.Seed, biome, x, y, _keptClear);
                    if (pick.Any) list.Add(new Stand { X = x, Y = y, Type = type, Decor = pick, Biome = biome });
                }
            }
        }

        bool InPlace(int x, int y)
        {
            foreach (var site in _client.Places)
            {
                int dx = x - site.X, dy = y - site.Y, reach = site.Radius + 2;
                if (dx * dx + dy * dy <= reach * reach) return true;
            }
            return false;
        }

        void DrawStanding(float deltaTime)
        {
            var map = _client.Map;
            var phase = Calendar.Phase(_client.Clock);
            _windowsLit = phase == DayPhase.Evening || phase == DayPhase.Night;
            foreach (var chunk in _standDirty)
            {
                if (_chunks.Contains(chunk)) ListStands(chunk.X, chunk.Y);
                _standDone.Add(chunk);
            }
            foreach (var chunk in _standDone) _standDirty.Remove(chunk);
            _standDone.Clear();

            // What is on screen, and round it: tall things rise up to five tiles over their feet, wide ones reach left.
            float high = _camera.orthographicSize, wide = high * _camera.aspect;
            var middle = _camera.transform.position;
            _standFrom = new Vector2(middle.x, middle.y);
            float left = middle.x - wide - 4, right = middle.x + wide + 2, bottom = middle.y - high - 6, top = middle.y + high + 2;
            _standsInView.Clear();
            _decorInView.Clear();
            int listed = 0;
            for (int cy = Mathf.FloorToInt(bottom) / ChunkSize; cy <= Mathf.FloorToInt(top) / ChunkSize; cy++)
            {
                for (int cx = Mathf.FloorToInt(Mathf.Max(0, left)) / ChunkSize; cx <= Mathf.FloorToInt(right) / ChunkSize; cx++)
                {
                    if (!_standChunks.TryGetValue((cx, cy), out var list)) continue;
                    listed += list.Count;
                    foreach (var stand in list)
                    {
                        if (stand.X < left || stand.X > right || stand.Y < bottom || stand.Y > top) continue;
                        (stand.Decor.Any ? _decorInView : _standsInView).Add(stand);
                    }
                }
            }
            StandsListed = listed;
            int cap = StandCap;
            // Over the cap, the nearest first; props before decor.
            if (_standsInView.Count + _decorInView.Count > cap)
            {
                _nearer ??= Nearer;
                _standsInView.Sort(_nearer);
                _decorInView.Sort(_nearer);
            }
            _standUsed = 0;
            int props = 0, decor = 0, faded = 0, skipped = 0;
            _fadeSeen.Clear();
            foreach (var stand in _standsInView)
            {
                if (_standUsed >= cap)
                {
                    skipped++;
                    continue;
                }
                if (DrawStand(map, stand, deltaTime, cap, ref faded)) props++;
            }
            foreach (var stand in _decorInView)
            {
                if (_standUsed >= cap)
                {
                    skipped++;
                    continue;
                }
                var (fx, fy) = StandArt.DecorFoot(stand.X, stand.Y, stand.Decor);
                var pick = stand.Decor;
                var biome = stand.Biome;
                long key = 1L << 40 | (long)pick.Kind << 16 | (long)biome << 8 | pick.Variant;
                if (!PropAtlas.TryGet(key, out var sprite)) sprite = PropAtlas.Get(key, () => DecorArt.Image(pick.Kind, biome, pick.Variant));
                var renderer = NextStand();
                renderer.sprite = sprite;
                renderer.flipX = pick.Flip;
                renderer.color = Color.white;
                renderer.transform.localPosition = new Vector3(fx, fy - StandArt.DecorDrop, 0);
                renderer.sortingOrder = FootOrder(fy, StandArt.DecorTie);
                decor++;
            }
            for (int i = _standUsed; i < _standPool.Count; i++)
                if (_standPool[i].enabled) _standPool[i].enabled = false;
            _fadeGone.Clear();
            foreach (var pair in _fades) if (!_fadeSeen.Contains(pair.Key)) _fadeGone.Add(pair.Key);
            foreach (int key in _fadeGone) _fades.Remove(key);
            PropsDrawn = props;
            DecorDrawn = decor;
            CanopiesFaded = faded;
            StandsSkipped = skipped;
            PropAtlas.Upload();
        }

        Comparison<Stand> _nearer;

        int Nearer(Stand a, Stand b)
        {
            float da = (a.X + 0.5f - _standFrom.x) * (a.X + 0.5f - _standFrom.x) + (a.Y + 0.5f - _standFrom.y) * (a.Y + 0.5f - _standFrom.y);
            float db = (b.X + 0.5f - _standFrom.x) * (b.X + 0.5f - _standFrom.x) + (b.Y + 0.5f - _standFrom.y) * (b.Y + 0.5f - _standFrom.y);
            return da.CompareTo(db);
        }

        SpriteRenderer NextStand()
        {
            if (_standUsed == _standPool.Count) _standPool.Add(Renderer(_root.transform, "Stand", null, Color.white));
            var renderer = _standPool[_standUsed++];
            if (!renderer.enabled) renderer.enabled = true;
            return renderer;
        }

        // One structure, fence, wall or gate: its picture (in two parts when it has a canopy or roof, the top fading
        // while someone is behind it). False when it has no picture.
        bool DrawStand(TileMap map, Stand stand, float deltaTime, int cap, ref int faded)
        {
            int cell = stand.Y * map.Width + stand.X;
            bool open = stand.Kind == StandArt.Kind.Gate && _openDoors.Contains(cell);
            int shape = StandArt.ShapeAt(map, stand.X, stand.Y, open);
            bool lit = PropArt.LitAtNight(stand.Type) && _windowsLit;
            var type = stand.Type;
            var kind = stand.Kind;
            var (w, _) = Structures.Footprint(type);
            float x = stand.X + w / 2f;
            float foot = stand.Y + StandArt.FootLift(type, kind);
            int order = FootOrder(foot, StandArt.PropTie);
            int rows = StandArt.CanopyRows(type);
            long key = (long)type << 24 | (long)kind << 20 | (long)shape << 4 | (lit ? 1L : 0);
            if (rows <= 0)
            {
                if (!PropAtlas.TryGet(key, out var whole)) whole = PropAtlas.Get(key, () => StandArt.Picture(type, kind, shape, lit), image => StandArt.PivotX(kind, shape, image));
                if (whole == null) return false;
                PlaceStand(NextStand(), whole, x, stand.Y, order, 1);
                return true;
            }
            if (!PropAtlas.TryGet(key | 2, out var under) || !PropAtlas.TryGet(key | 4, out var over))
            {
                var picture = StandArt.Picture(type, kind, shape, lit);
                under = PropAtlas.Get(key | 2, () => PropAtlas.Rows(picture, rows, int.MaxValue));
                over = PropAtlas.Get(key | 4, () => PropAtlas.Rows(picture, 0, rows));
            }
            if (under == null || over == null) return false;
            float underHigh = under.rect.height / StandArt.Pixels;
            var canopy = StandArt.Canopy(x, stand.Y, (int)under.rect.width, (int)(under.rect.height + over.rect.height), rows);
            bool hidden = FadeBehind && Behind(foot, canopy);
            float target = hidden ? StandArt.FadeAlpha : 1;
            float alpha = _fades.TryGetValue(cell, out float was) ? was : 1;
            alpha = FadeAtOnce ? target : Mathf.MoveTowards(alpha, target, deltaTime * (1 - StandArt.FadeAlpha) / StandArt.FadeSeconds);
            _fades[cell] = alpha;
            _fadeSeen.Add(cell);
            if (alpha < 0.999f) faded++;
            PlaceStand(NextStand(), under, x, stand.Y, order, 1);
            if (_standUsed < cap) PlaceStand(NextStand(), over, x, stand.Y + underHigh, order, alpha);
            return true;
        }

        static void PlaceStand(SpriteRenderer renderer, Sprite sprite, float x, float y, int order, float alpha)
        {
            renderer.sprite = sprite;
            renderer.flipX = false;
            renderer.color = alpha >= 1 ? Color.white : new Color(1, 1, 1, alpha);
            renderer.transform.localPosition = new Vector3(x, y, 0);
            renderer.sortingOrder = order;
        }

        // Whether a player or a monster stands behind the thing with its body under the canopy (StandArt.Hides).
        bool Behind(float foot, (float Left, float Right, float Bottom, float Top) canopy)
        {
            if (_self != null && StandArt.Hides(_self.Position.x, _self.Position.y - _self.Feet, foot, canopy)) return true;
            foreach (var body in _others.Values)
                if (body != null && body.Root != null && body.Root.gameObject.activeSelf && StandArt.Hides(body.Position.x, body.Position.y - body.Feet, foot, canopy))
                    return true;
            return false;
        }

        // For the playtests: the shape the thing at a tile is drawn in (its joined sides, a gate's way and whether it is
        // open), its kind, and its order beside the player's.
        public (string Kind, int Shape, int Order) StandAt(int x, int y)
        {
            var map = _client.Map;
            var kind = StandArt.KindAt(map, x, y);
            if (kind == StandArt.Kind.None) return ("none", -1, 0);
            bool open = kind == StandArt.Kind.Gate && _openDoors.Contains(y * map.Width + x);
            return (kind.ToString(), StandArt.ShapeAt(map, x, y, open), FootOrder(y + StandArt.FootLift(map[x, y], kind), StandArt.PropTie));
        }

        public int SelfOrder => _self == null ? 0 : _self.Shape.sortingOrder;
        public float FadeOf(int x, int y) => _client?.Map == null ? 1 : _fades.TryGetValue(y * _client.Map.Width + x, out float alpha) ? alpha : 1;
    }
}
