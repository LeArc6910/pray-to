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
        // The festival (2026-11-09): the merchant sells what he never does otherwise (shop.csv rows with event Festival).
        Festival,
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
        // The days (Docs/Time.md, 행사): read from events.csv by the game (2026-11-09), as in code before for a world that
        // was given none. Each comes every so many days; when two fall on one day the rarer wins.
        public static DayEventData DayEvents { get; set; } = DayEventSheet.Default;

        static int PercentOf(DayEvent kind) => DayEvents.Find(kind)?.Percent ?? DayEventSheet.Default.Find(kind)?.Percent ?? 0;

        // A contest fish's prize: this many coins for each step of its difficulty.
        public static int ContestPrizePerDifficulty => PercentOf(DayEvent.FishingContest);
        public static int MarketPercent => PercentOf(DayEvent.Market);
        public static int HarvestPercent => PercentOf(DayEvent.Harvest);
        public static int FullMoonExperiencePercent => PercentOf(DayEvent.FullMoon);

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

        public static DayEvent EventOf(int day)
        {
            if (day <= 1) return DayEvent.None;
            DayEventDefinition rarest = null;
            foreach (var definition in DayEvents.Events)
            {
                if (day % definition.Every == 0 && (rarest == null || definition.Every > rarest.Every)) rarest = definition;
            }
            return rarest?.Kind ?? DayEvent.None;
        }

        public static string EventName(DayEvent dayEvent) => DayEvents.Find(dayEvent)?.Name ?? "";

        public static string EventText(DayEvent dayEvent) => DayEvents.Find(dayEvent)?.Text ?? "";

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
