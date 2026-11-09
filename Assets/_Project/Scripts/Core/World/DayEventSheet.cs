using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // One kind of day (Docs/Time.md, 행사): its name, how many days apart it comes, the strength of its effect (a price
    // percent, a prize) and the line the screen shows. What it does stays in code, by its DayEvent.
    public sealed class DayEventDefinition
    {
        internal DayEventDefinition(DayEvent kind, string name, int every, int percent, string text)
        {
            Kind = kind;
            Name = name;
            Every = every;
            Percent = percent;
            Text = text.Replace("{percent}", percent.ToString()).Replace("{off}", (100 - percent).ToString()).Replace("{more}", (percent - 100).ToString());
        }

        public DayEvent Kind { get; }
        public string Name { get; }
        public int Every { get; }
        public int Percent { get; }
        public string Text { get; }
    }

    public sealed class DayEventData
    {
        public DayEventData(IReadOnlyList<DayEventDefinition> events, IReadOnlyList<string> errors)
        {
            Events = events;
            Errors = errors;
        }

        public IReadOnlyList<DayEventDefinition> Events { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool Success => Errors.Count == 0;

        public DayEventDefinition Find(DayEvent kind) => Events.FirstOrDefault(e => e.Kind == kind);
    }

    // Reads Assets/_Project/Data/World/events.csv (2026-11-09: the days moved out of code into a sheet).
    public static class DayEventSheet
    {
        public const string File = "events.csv";

        // The days as they were in code, for a world whose game did not read the sheet (tests, sims).
        public static readonly DayEventData Default = new DayEventData(new[]
        {
            new DayEventDefinition(DayEvent.Market, "장날", 5, 80, "장날: 가게 물건이 {off}% 싸다"),
            new DayEventDefinition(DayEvent.FishingContest, "낚시 대회", 6, 5, "낚시 대회: 낚은 물고기마다 상금(어려움 하나에 금화 {percent}), 낚시 경험치 두 배"),
            new DayEventDefinition(DayEvent.Harvest, "수확제", 7, 150, "수확제: 작물을 {more}% 더 비싸게 산다"),
            new DayEventDefinition(DayEvent.FullMoon, "보름달", 8, 150, "보름달: 밤에 몬스터가 더 많고 경험치가 {more}% 더 나온다"),
            new DayEventDefinition(DayEvent.Festival, "축제", 11, 0, "축제: 상인이 평소에는 팔지 않는 스킬 결정을 싸게 판다"),
        }, Array.Empty<string>());

        public static DayEventData Parse(string csv)
        {
            var errors = new List<string>();
            var events = new List<DayEventDefinition>();
            if (string.IsNullOrWhiteSpace(csv)) return new DayEventData(events, errors);
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns("event", "name", "every", "text");
            if (missing.Count > 0)
            {
                errors.Add($"{File}: 열이 없습니다: {string.Join(", ", missing)}");
                return new DayEventData(events, errors);
            }
            foreach (var row in table.Rows)
            {
                try
                {
                    var kind = SheetValues.ParseEnum<DayEvent>(row["event"], "event");
                    if (kind == DayEvent.None) throw new FormatException("None은 행사가 아닙니다.");
                    if (events.Any(e => e.Kind == kind)) throw new FormatException($"'{row["event"]}'가 두 번 있습니다.");
                    int every = SheetValues.ParseNumber(row["every"], "every");
                    if (every < 2) throw new FormatException("every는 2 이상이어야 합니다(첫날에는 행사가 없다).");
                    int percent = row["percent"].Length == 0 ? 0 : SheetValues.ParseNumber(row["percent"], "percent");
                    if (row["name"].Length == 0 || row["text"].Length == 0) throw new FormatException("name과 text를 적어야 합니다.");
                    events.Add(new DayEventDefinition(kind, row["name"], every, percent, row["text"]));
                }
                catch (FormatException e)
                {
                    errors.Add($"{File} {row.Line}행: {e.Message}");
                }
            }
            return new DayEventData(events, errors);
        }
    }
}
