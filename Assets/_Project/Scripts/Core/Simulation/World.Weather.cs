using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // The day's weather and what the calendar holds (Docs/Time.md, 날씨, 날짜 행사), and where daylight falls into the
    // cave. All of it follows from the day (and the seed) alone, so every client knows without being told.
    public enum Weather : byte
    {
        Clear,
        // Rain on the land above seeps down and wets every crop.
        Rain,
        // Rain with thunder: wets the crops too, hides the sun from solar panels and swells the water for wheels.
        Storm,
        // Mist in the tunnels: monsters notice players later.
        Fog,
    }

    public enum DayEvent : byte
    {
        None,
        // Market day: the shops sell for less.
        Market,
        // The harvest festival: crops sell for more.
        Harvest,
        // A full moon: more monsters at night, and more experience from them.
        FullMoon,
        // The fishing contest (2026-11-09): a prize in coins for every fish landed, and twice the fishing experience.
        FishingContest,
    }

    public sealed partial class World
    {
        // Of a hundred days about this many are wet (rain or storm; storms are the first StormPercent of them) and this
        // many foggy. Never the first day.
        public const int RainPercent = 25;
        public const int StormPercent = 8;
        public const int FogPercent = 12;
        // How far a monster notices players in fog, as a share of its aggro range.
        public const int FogAggroPercent = 70;
        // Market days come every MarketEvery days, the fishing contest every FishingContestEvery, the harvest festival
        // every HarvestEvery, a full moon every FullMoonEvery; when two fall on one day the rarer wins.
        public const int MarketEvery = 5;
        public const int HarvestEvery = 7;
        public const int FullMoonEvery = 8;
        public const int FishingContestEvery = 6;
        // A contest fish's prize: this many coins for each step of its difficulty.
        public const int ContestPrizePerDifficulty = 5;
        public const int MarketPercent = 80;
        public const int HarvestPercent = 150;
        public const int FullMoonExperiencePercent = 150;

        public static Weather WeatherOf(int day)
        {
            if (day <= 1) return Weather.Clear;
            int roll = new GameRandom((ulong)day, stream: 13).Next(100);
            return roll < StormPercent ? Weather.Storm : roll < RainPercent ? Weather.Rain : roll < RainPercent + FogPercent ? Weather.Fog : Weather.Clear;
        }

        public static bool IsRainy(int day) => WeatherOf(day) is Weather.Rain or Weather.Storm;

        public Weather WeatherToday => WeatherOf(Today);

        public bool RainingToday => IsRainy(Today);

        public static string WeatherName(Weather weather) => weather switch
        {
            Weather.Rain => "비",
            Weather.Storm => "폭풍",
            Weather.Fog => "안개",
            _ => "맑음",
        };

        public static DayEvent EventOf(int day) =>
            day <= 1 ? DayEvent.None
            : day % FullMoonEvery == 0 ? DayEvent.FullMoon
            : day % HarvestEvery == 0 ? DayEvent.Harvest
            : day % FishingContestEvery == 0 ? DayEvent.FishingContest
            : day % MarketEvery == 0 ? DayEvent.Market
            : DayEvent.None;

        public static string EventName(DayEvent dayEvent) => dayEvent switch
        {
            DayEvent.Market => "장날",
            DayEvent.Harvest => "수확제",
            DayEvent.FullMoon => "보름달",
            DayEvent.FishingContest => "낚시 대회",
            _ => "",
        };

        public static string EventText(DayEvent dayEvent) => dayEvent switch
        {
            DayEvent.Market => $"장날: 가게 물건이 {100 - MarketPercent}% 싸다",
            DayEvent.Harvest => $"수확제: 작물을 {HarvestPercent - 100}% 더 비싸게 산다",
            DayEvent.FullMoon => $"보름달: 밤에 몬스터가 더 많고 경험치가 {FullMoonExperiencePercent - 100}% 더 나온다",
            DayEvent.FishingContest => $"낚시 대회: 낚은 물고기마다 상금(어려움 하나에 금화 {ContestPrizePerDifficulty}), 낚시 경험치 두 배",
            _ => "",
        };

        public DayEvent EventToday => EventOf(Today);

        // A full moon's night: from 22:00 of its day to 6:00 the next morning (the date turns at six).
        bool FullMoonNight => EventToday == DayEvent.FullMoon && Calendar.IsNight(Clock);

        // How far monsters notice players now.
        float AggroScale => WeatherToday == Weather.Fog ? FogAggroPercent / 100f : 1;

        int _weatherDay;

        // At the start of a wet day every crop takes water, as if each had been watered.
        void UpdateWeather()
        {
            int today = Today;
            if (today == _weatherDay) return;
            _weatherDay = today;
            if (!IsRainy(today)) return;
            int wetted = _crops.Values.ToList().Count(crop => WaterCrop(crop.X, crop.Y));
            _events.Add(new SimEvent(SimEventType.Rain, 0, amount: wetted));
        }

        // Where daylight falls into the cave through a crack in its roof (Docs/Time.md, 빛 구멍): one a few tiles from
        // the spawn, so a new player sees the day go by, and now and then one in a stretch of the cave. Only floor that
        // the seed made, never in the edge. The view lights them by the time of day; nothing else follows from them.
        public static List<(int X, int Y)> SkyWells(TileMap map, Vec2 spawn, ulong seed)
        {
            var wells = new List<(int X, int Y)>();
            int sx = TileMap.Cell(spawn.X), sy = TileMap.Cell(spawn.Y);
            for (int r = 2; r <= 6 && wells.Count == 0; r++)
            {
                for (int dy = -r; dy <= r && wells.Count == 0; dy++)
                {
                    for (int dx = -r; dx <= r && wells.Count == 0; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int x = sx + dx, y = sy + dy;
                        if (map.InBounds(x, y) && !map.IsEdge(x, y) && map[x, y] == TileType.Floor) wells.Add((x, y));
                    }
                }
            }
            const int block = 24;
            var random = new GameRandom(seed, stream: 17);
            for (int by = 0; by < map.Height; by += block)
            {
                for (int bx = 0; bx < map.Width; bx += block)
                {
                    bool chance = random.Chance(0.35f);
                    int x = bx + random.Next(block), y = by + random.Next(block);
                    if (!chance || !map.InBounds(x, y) || map.IsEdge(x, y) || map[x, y] != TileType.Floor) continue;
                    if (wells.Any(w => Math.Abs(w.X - x) + Math.Abs(w.Y - y) < block / 2)) continue;
                    wells.Add((x, y));
                }
            }
            return wells;
        }
    }
}
