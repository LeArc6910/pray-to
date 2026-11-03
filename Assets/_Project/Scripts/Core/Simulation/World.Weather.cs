using System;
using System.Linq;

namespace Game.Core
{
    // Rainy days (Docs/Time.md, 날씨): rain on the land above seeps down through the cave roof and wets every crop.
    // Which days rain follows from the day alone, so every client knows without being told.
    public sealed partial class World
    {
        // About one day in four rains; never the first.
        public const int RainPercent = 25;

        public static bool IsRainy(int day) => day > 1 && new GameRandom((ulong)day, stream: 13).Next(100) < RainPercent;

        public bool RainingToday => IsRainy(Today);

        int _weatherDay;

        // At the start of a rainy day every crop takes water, as if each had been watered.
        void UpdateWeather()
        {
            int today = Today;
            if (today == _weatherDay) return;
            _weatherDay = today;
            if (!IsRainy(today)) return;
            int wetted = _crops.Values.ToList().Count(crop => WaterCrop(crop.X, crop.Y));
            _events.Add(new SimEvent(SimEventType.Rain, 0, amount: wetted));
        }
    }
}
