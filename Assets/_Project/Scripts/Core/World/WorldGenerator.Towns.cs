using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // The grown towns (2026-11-27, Docs/Towns.md, the owner: "마을·성이 너무 초라하다 … 규모를 좀 더 늘리고"). The
    // generator's version 10 carves the dawn village three times as wide (radius 30) and the royal city near five times
    // (radius 48), and lays them out as towns: the village round a market plaza with a well, crossing streets and a ring
    // street, an inn, a smithy, shops, a temple, a stable, houses and farm plots inside a palisade; the royal city with a
    // castle wall and towers round a city of streets, a market square, an inn, a smithy, shops, barracks with their yard,
    // a stable and paddock, two chapels and rows of houses, and inside a moat and an inner wall the court: gardens with
    // fountains, statues, pillars along a red carpet up to the throne before the palace, banners. The roads that come in
    // are laid through to the middle first, so nothing is built across them and the walls open there. Worlds of version 9
    // and before keep their small places.
    public static partial class WorldGenerator
    {
        public const int TownVersion = 10;
        public const int VillageRadius = 30;
        public const int CityRadius = 48;

        // A place's plaza radius in a world of the version: a town's grown one from version 10, else the sheet's.
        public static int RadiusOf(PlaceDefinition place, int version) =>
            version < TownVersion ? place.Radius : place.Style == "village" ? VillageRadius : place.Style == "kingdom" ? CityRadius : place.Radius;

        sealed class TownDresser
        {
            readonly TileMap _map;
            readonly int _cx, _cy, _r;
            readonly TileType _floor;
            readonly Func<int, int, bool> _free;
            readonly Action<int, int, TileType> _put;
            readonly Action<int, int, TileType> _lay;
            readonly HashSet<(int X, int Y)> _gates;

            // gatesFrom: the ways the roads come in are laid only this far out and further, to the street that meets them
            // (the ring street, the moat's): inside it the town is built as it is, reached by its own streets.
            public TownDresser(TileMap map, int cx, int cy, int r, TileType floor, Func<int, int, bool> free, Action<int, int, TileType> put,
                Action<int, int, TileType> lay, HashSet<(int X, int Y)> gates, float gatesFrom)
            {
                _map = map;
                _cx = cx;
                _cy = cy;
                _r = r;
                _floor = floor;
                _free = free;
                _put = put;
                _lay = lay;
                _gates = new HashSet<(int X, int Y)>(gates.Where(g => Dist(g.X, g.Y) >= gatesFrom));
            }

            TileType At(int dx, int dy) => _map[_cx + dx, _cy + dy];
            void Set(int dx, int dy, TileType type)
            {
                int x = _cx + dx, y = _cy + dy;
                if (IsInside(_map, x, y) && dx * dx + dy * dy <= _r * _r && !(dx == 0 && dy == 0)) _map[x, y] = type;
            }
            static float Dist(int dx, int dy) => MathF.Sqrt(dx * dx + dy * dy);

            // Every tile of the structure free (the plaza's floor, off the gates); then put.
            bool Fits(int dx, int dy, TileType type)
            {
                var (w, h) = Structures.Footprint(type);
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                        if (!_free(dx + i, dy + j) || _gates.Contains((dx + i, dy + j))) return false;
                return true;
            }

            bool Place(int dx, int dy, TileType type)
            {
                if (!Fits(dx, dy, type)) return false;
                _put(dx, dy, type);
                return true;
            }

            // The first of the spots it fits at.
            bool PlaceAny(TileType type, params (int X, int Y)[] spots)
            {
                foreach (var (x, y) in spots)
                    if (Place(x, y, type)) return true;
                return false;
            }

            // At the spot or the nearest it fits at, within reach tiles (ring by ring): false when none.
            bool PlaceNear(TileType type, int x, int y, int reach = 8)
            {
                for (int d = 0; d <= reach; d++)
                    for (int j = -d; j <= d; j++)
                        for (int i = -d; i <= d; i++)
                            if (Math.Max(Math.Abs(i), Math.Abs(j)) == d && Place(x + i, y + j, type)) return true;
                return false;
            }

            // Left and right alike (a structure of width w at x on the right, -x - w + 1 on the left).
            void Pair(int dx, int dy, TileType type)
            {
                Place(dx, dy, type);
                Place(-dx - Structures.Footprint(type).W + 1, dy, type);
            }

            // The roads that come in, laid through to the middle (Decorate's gates), and the streets.
            void Gates()
            {
                foreach (var (x, y) in _gates)
                    if (_free(x, y)) _lay(x, y, TileType.Road);
            }

            void Street(int x0, int y0, int x1, int y1, int half = 1)
            {
                for (int y = Math.Min(y0, y1) - (x0 == x1 ? 0 : half); y <= Math.Max(y0, y1) + (x0 == x1 ? 0 : half); y++)
                    for (int x = Math.Min(x0, x1) - (y0 == y1 ? 0 : half); x <= Math.Max(x0, x1) + (y0 == y1 ? 0 : half); x++)
                        _lay(x, y, TileType.Road);
            }

            void Ring(float inner, float outer, TileType type, bool overGates = false)
            {
                for (int dy = -_r; dy <= _r; dy++)
                    for (int dx = -_r; dx <= _r; dx++)
                    {
                        float d = Dist(dx, dy);
                        if (d <= inner || d > outer) continue;
                        if (!overGates && _gates.Contains((dx, dy))) continue;
                        if (type == TileType.Road) _lay(dx, dy, type);
                        else if (_free(dx, dy)) Set(dx, dy, type);
                    }
            }

            // Houses round a band, every step degrees from a start, wherever one fits (a cottage or a cabin by turns).
            void Houses(float radius, float start, float step, int count = 99)
            {
                int laid = 0;
                for (float a = start; a < start + 360 && laid < count; a += step)
                {
                    int x = (int)MathF.Round(radius * MathF.Cos(a * MathF.PI / 180)) - 2, y = (int)MathF.Round(radius * MathF.Sin(a * MathF.PI / 180)) - 1;
                    var type = laid % 3 == 2 ? TileType.Cabin : TileType.Cottage;
                    if (!Place(x, y, type)) continue;
                    laid++;
                    // A flowerbed or a barrel by its door, a lamp at its corner.
                    Place(x + 1, y - 1, laid % 2 == 0 ? TileType.Flowerbed : TileType.Barrel);
                    Place(x + 4, y - 1, TileType.LampPost);
                }
            }

            // A plot of soil fenced round with a scarecrow, w by h inside, its lower left at (x, y).
            void Farm(int x0, int y0, int w, int h)
            {
                bool Room(int x, int y)
                {
                    for (int j = -1; j <= h; j++)
                        for (int i = -1; i <= w; i++)
                            if (!_free(x + i, y + j) || _gates.Contains((x + i, y + j))) return false;
                    return true;
                }
                int x = x0, y = y0;
                bool found = false;
                for (int d = 0; d <= 6 && !found; d++)
                    for (int j = -d; j <= d && !found; j++)
                        for (int i = -d; i <= d && !found; i++)
                            if (Math.Max(Math.Abs(i), Math.Abs(j)) == d && Room(x0 + i, y0 + j)) (x, y, found) = (x0 + i, y0 + j, true);
                if (!found) return;
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                        _lay(x + i, y + j, TileType.Soil);
                for (int i = -1; i <= w; i++)
                {
                    Place(x + i, y - 1, TileType.Fence);
                    if (i != w / 2) Place(x + i, y + h, TileType.Fence);
                }
                for (int j = 0; j < h; j++)
                {
                    Place(x - 1, y + j, TileType.Fence);
                    Place(x + w, y + j, TileType.Fence);
                }
                Set(x + w / 2, y + h / 2, TileType.Scarecrow);
            }

            void Trees(float radius, float start, float step, TileType tree = TileType.MushroomTree)
            {
                for (float a = start; a < start + 360; a += step)
                    Place((int)MathF.Round(radius * MathF.Cos(a * MathF.PI / 180)), (int)MathF.Round(radius * MathF.Sin(a * MathF.PI / 180)), tree);
            }

            // ---- The dawn village, radius 30 ----

            public void Village(List<(int X, int Y)> wanted)
            {
                // The palisade round the edge (the roads come through it: Open never needs to cut, the gates are laid).
                Gates();
                Ring(_r - 1.2f, _r, TileType.TimberWall);
                // Two streets crossing at the well, a ring street, and the lanes out to the palisade.
                Street(-_r + 2, 0, _r - 2, 0);
                Street(0, -_r + 2, 0, _r - 2);
                Ring(14.5f, 16.5f, TileType.Road);
                // The market plaza: the well, stalls round it, benches, lamps, the notice board (the signpost).
                Place(-5, 3, TileType.Well);
                Place(4, 3, TileType.Fountain);
                foreach (var (x, y) in new[] { (-9, 3), (7, 3), (-9, -4), (7, -4), (-6, -7), (4, -7) }) Place(x, y, TileType.MarketStall);
                foreach (var (x, y) in new[] { (-3, 5), (2, 5), (-3, -5), (2, -5) }) Place(x, y, TileType.Bench);
                foreach (var (x, y) in new[] { (-2, 9), (2, 9), (-2, -9), (2, -9), (9, 2), (9, -2), (-9, 2), (-9, -2) }) Place(x, y, TileType.LampPost);
                Place(2, 2, TileType.Signpost);
                foreach (var (x, y) in new[] { (5, -4), (6, -5), (-6, 5) }) Place(x, y, TileType.Crates);
                // Inside the ring street: the inn (north-west), the smithy (north-east), shops (south-east), the temple (south-west).
                PlaceNear(TileType.Inn, -11, 6);
                foreach (var (x, y) in new[] { (-11, 10), (-6, 10), (-12, 4) }) Place(x, y, TileType.Bench);
                foreach (var (x, y) in new[] { (-4, 7), (-4, 8) }) Place(x, y, TileType.Barrel);
                PlaceNear(TileType.Smithy, 5, 7);
                Place(11, 8, TileType.WeaponRack);
                Place(11, 10, TileType.Woodpile);
                Place(5, 11, TileType.TrainingDummy);
                PlaceNear(TileType.Shop, 3, -11);
                PlaceNear(TileType.Shop, 8, -9);
                PlaceNear(TileType.Temple, -10, -11);
                foreach (var (x, y) in new[] { (-4, -11), (-12, -6) }) Place(x, y, TileType.Statue);
                Place(-4, -7, TileType.Flowerbed);
                // Outside the ring: the stable (east), the elder's and the villagers' houses all round, farm plots in the
                // south-west and north-east, mushroom trees by the palisade.
                PlaceNear(TileType.Stable, 19, -3);
                Farm(-25, -12, 6, 4);
                Farm(-17, -24, 7, 4);
                Farm(16, 17, 5, 4);
                Houses(22, 10, 18);
                Houses(26, 20, 22);
                Trees(27.5f, 5, 17);
                foreach (var (x, y) in new[] { (-19, 4), (19, 12), (-6, 20), (6, -21) }) Place(x, y, TileType.Woodpile);
                // The people of the story: by the well and by the inn.
                wanted.Add((_cx - 4, _cy + 2));
                wanted.Add((_cx - 8, _cy + 5));
            }

            // ---- The royal city, radius 48 ----

            public void Kingdom(List<(int X, int Y)> wanted)
            {
                Gates();
                // The castle wall round the edge, towers along it.
                Ring(_r - 2.2f, _r, TileType.CastleWall);
                for (int k = 0; k < 12; k++)
                {
                    float a = (15 + k * 30) * MathF.PI / 180;
                    Place((int)MathF.Round((_r - 4.5f) * MathF.Cos(a)) - 1, (int)MathF.Round((_r - 4.5f) * MathF.Sin(a)) - 1, TileType.Tower);
                }
                // The avenue from the south gate to the court, the cross street, the street round the moat.
                Street(0, -_r + 3, 0, -18);
                Street(-_r + 3, 0, -24, 0);
                Street(24, 0, _r - 3, 0);
                Ring(22.5f, 24.5f, TileType.Road);
                Ring(34.5f, 35.5f, TileType.Road);
                // The court: a moat (bridged where a way crosses it), the inner wall (open there), its towers.
                for (int dy = -22; dy <= 22; dy++)
                    for (int dx = -22; dx <= 22; dx++)
                    {
                        float d = Dist(dx, dy);
                        bool way = _gates.Contains((dx, dy)) || Math.Abs(dx) <= 1 && dy < 0;
                        if (d > 19.6f && d <= 21.6f) Set(dx, dy, way ? TileType.Dock : TileType.Water);
                        else if (d > 17.4f && d <= 19.6f && !way && _free(dx, dy)) Set(dx, dy, TileType.CastleWall);
                        else if (d > 17.4f && d <= 19.6f && way) _lay(dx, dy, TileType.Road);
                    }
                foreach (var (x, y) in new[] { (11, 11), (-13, 11), (11, -13), (-13, -13) }) Place(x, y, TileType.Tower);
                // The palace at the north of the court, the throne before its door, the red carpet from the inner gate.
                PlaceNear(TileType.Palace, -7, 9);
                PlaceNear(TileType.Throne, 0, 7, 3);
                for (int dy = -18; dy <= 6; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (At(dx, dy) == _floor || At(dx, dy) == TileType.Road) Set(dx, dy, TileType.RedCarpet);
                foreach (int dy in new[] { -14, -10, -6, -2, 2 }) Pair(3, dy, TileType.Pillar);
                Pair(4, -16, TileType.Statue);
                foreach (var (x, y) in new[] { (9, 9), (12, 6), (9, 3) }) Pair(x, y, TileType.RoyalBanner);
                Pair(3, 6, TileType.Brazier);
                // The gardens either side of the carpet: a fountain in hedges and flowerbeds.
                foreach (int side in new[] { 1, -1 })
                {
                    int fx = side > 0 ? 9 : -10;
                    Place(fx, -6, TileType.BigFountain);
                    for (int i = -3; i <= 4; i++)
                    {
                        Place(fx + i, -10, TileType.Hedge);
                        Place(fx + i, -1, TileType.Hedge);
                    }
                    foreach (var (x, y) in new[] { (fx - 2, -7), (fx + 3, -7), (fx - 2, -4), (fx + 3, -4) }) Place(x, y, TileType.Flowerbed);
                    Place(fx + (side > 0 ? 4 : -2), -2, TileType.LampPost);
                }
                // The city round the court. South, by the avenue: the market square, fountains, stalls, the inn and shops.
                foreach (int x in new[] { -10, -6, 5, 9 })
                    foreach (int y in new[] { -30, -33 })
                        Place(x, y, TileType.MarketStall);
                Pair(14, -31, TileType.BigFountain);
                foreach (var (x, y) in new[] { (3, -27), (3, -37), (-4, -27), (-4, -37) }) Place(x, y, TileType.LampPost);
                PlaceNear(TileType.Inn, 6, -43);
                PlaceNear(TileType.Shop, -10, -42);
                PlaceNear(TileType.Shop, 13, -38);
                PlaceNear(TileType.Smithy, -17, -37);
                Place(-11, -37, TileType.WeaponRack);
                // West: the barracks and their yard; east: the stable and its paddock.
                PlaceNear(TileType.Barracks, -38, 3);
                foreach (var (x, y) in new[] { (-34, -4), (-31, -4), (-28, -4), (-34, -7), (-31, -7) }) Place(x, y, TileType.TrainingDummy);
                foreach (var (x, y) in new[] { (-38, -3), (-27, 5) }) Place(x, y, TileType.WeaponRack);
                PlaceNear(TileType.Stable, 31, 3);
                for (int i = 0; i < 8; i++)
                {
                    Place(28 + i, -3, TileType.Fence);
                    Place(28 + i, -9, TileType.Fence);
                }
                Place(36, -6, TileType.Woodpile);
                // North-west and north-east: the chapels, a statue before each.
                PlaceNear(TileType.Temple, -27, 24);
                PlaceNear(TileType.Temple, 21, 24);
                Pair(24, 21, TileType.Statue);
                // Rows of houses through the rest, banners and lamps along the streets, trees by the wall.
                Houses(29, 8, 16);
                Houses(40, 4, 11);
                Trees(44.5f, 3, 13, TileType.MushroomTree);
                for (int k = 0; k < 16; k++)
                {
                    float a = (k * 22.5f + 11) * MathF.PI / 180;
                    Place((int)MathF.Round(25.6f * MathF.Cos(a)), (int)MathF.Round(25.6f * MathF.Sin(a)), k % 2 == 0 ? TileType.LampPost : TileType.RoyalBanner);
                }
                // The queen by her throne, her people on the palace steps.
                wanted.Add((_cx, _cy + 6));
                wanted.Add((_cx - 2, _cy + 5));
            }
        }
    }
}
