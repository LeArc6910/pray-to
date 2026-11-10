using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // A place people live (Docs/Story.md, 장소, 2026-11-10): carved into the caves when the world is made, round a
    // plaza of its floor, joined to an earlier place by a road, with a waypoint in the middle.
    public sealed class PlaceDefinition
    {
        internal PlaceDefinition(int index, string id, string name, string kind, int distance, int radius, string road, bool waypoint, TileType floor, string text)
        {
            Index = index;
            Id = id;
            Name = name;
            Kind = kind;
            Distance = distance;
            Radius = radius;
            Road = road;
            Waypoint = waypoint;
            Floor = floor;
            Text = text;
        }

        public int Index { get; }
        public string Id { get; }
        public string Name { get; }
        // What it is (village, kingdom, hut, nest), for the screen.
        public string Kind { get; }
        // Tiles from the spawn; 0 is round the spawn itself.
        public int Distance { get; }
        public int Radius { get; }
        // The place its road comes from (an earlier row), or "" for none.
        public string Road { get; }
        public bool Waypoint { get; }
        // What the plaza is laid with (Floor for bare cave floor).
        public TileType Floor { get; }
        // A line about it: under its name on arriving, and in the waypoint list.
        public string Text { get; }
    }

    // Someone in the story (Docs/Story.md, 인물): where they live, how they look, and what they say when it is not
    // their turn in the story.
    public sealed class CharacterDefinition
    {
        internal CharacterDefinition(int index, string id, string name, PlaceDefinition place, string look, string color, IReadOnlyList<string> lines)
        {
            Index = index;
            Id = id;
            Name = name;
            Place = place;
            Look = look;
            Color = color;
            Lines = lines;
        }

        public int Index { get; }
        public string Id { get; }
        public string Name { get; }
        public PlaceDefinition Place { get; }
        // Which drawing (queen, hero, elder, girl, demon, guard, villager); any other is a villager.
        public string Look { get; }
        public string Color { get; }
        public IReadOnlyList<string> Lines { get; }

        public string LineFor(int day) => Lines.Count == 0 ? "" : Lines[Math.Max(0, day) % Lines.Count];
    }

    public enum StoryGoal
    {
        // Done on talking.
        None,
        // Arrive at a place.
        Reach,
        // Fell a boss (once is enough, before or after).
        Boss,
        // Bring things back to whoever gave the step, who takes them.
        Have,
    }

    // One step of the story (Docs/Story.md, 이야기 단계): who gives it and what they say, then what to do.
    public sealed class StoryStep
    {
        internal StoryStep(int index, string chapter, string title, CharacterDefinition who, IReadOnlyList<(CharacterDefinition Speaker, string Text)> lines,
            StoryGoal goal, PlaceDefinition place, string boss, MaterialDefinition material, ConsumableDefinition consumable, int count,
            IReadOnlyList<GoalReward> rewards, string hint)
        {
            Index = index;
            Chapter = chapter;
            Title = title;
            Who = who;
            Lines = lines;
            Goal = goal;
            Place = place;
            Boss = boss;
            Material = material;
            Consumable = consumable;
            Count = count;
            Rewards = rewards;
            Hint = hint;
        }

        public int Index { get; }
        public string Chapter { get; }
        public string Title { get; }
        public CharacterDefinition Who { get; }
        // What is said on talking to Who: each line by Who, or by another character when it began with @id.
        public IReadOnlyList<(CharacterDefinition Speaker, string Text)> Lines { get; }
        public StoryGoal Goal { get; }
        public PlaceDefinition Place { get; }
        public string Boss { get; }
        public MaterialDefinition Material { get; }
        public ConsumableDefinition Consumable { get; }
        public int Count { get; }
        public IReadOnlyList<GoalReward> Rewards { get; }
        public string Hint { get; }

        public string RewardText => string.Join(" · ", Rewards.Select(r => r.Text));

        // Where the step leads: the place to reach, or else the giver's.
        public PlaceDefinition Target => Goal == StoryGoal.Reach ? Place : Who.Place;
    }

    public sealed class StoryData
    {
        public StoryData(IReadOnlyList<PlaceDefinition> places, IReadOnlyList<CharacterDefinition> characters, IReadOnlyList<StoryStep> steps, IReadOnlyList<string> errors)
        {
            Places = places;
            Characters = characters;
            Steps = steps;
            Errors = errors;
        }

        public IReadOnlyList<PlaceDefinition> Places { get; }
        public IReadOnlyList<CharacterDefinition> Characters { get; }
        public IReadOnlyList<StoryStep> Steps { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool Success => Errors.Count == 0;

        public PlaceDefinition Place(string id) => Places.FirstOrDefault(p => p.Id == id);
        public CharacterDefinition Character(string id) => Characters.FirstOrDefault(c => c.Id == id);
        public StoryStep Step(int index) => index >= 0 && index < Steps.Count ? Steps[index] : null;
    }

    // Reads Assets/_Project/Data/Story/places.csv, characters.csv and story.csv (Docs/Story.md). A row that does not
    // read is left out with a reason in Errors, as the other sheets do.
    public static class StorySheet
    {
        public const string PlacesFile = "places.csv";
        public const string CharactersFile = "characters.csv";
        public const string StoryFile = "story.csv";
        public const int MaxRadius = 12;

        public static StoryData Parse(string placesCsv, string charactersCsv, string storyCsv, LootData loot, BossData bosses = null)
        {
            var errors = new List<string>();
            var places = new List<PlaceDefinition>();
            var characters = new List<CharacterDefinition>();
            var steps = new List<StoryStep>();

            var placeTable = CsvTable.Read(placesCsv ?? "");
            var missing = placeTable.MissingColumns("id", "name", "distance", "radius");
            if (missing.Count > 0) errors.Add($"{PlacesFile}: 열이 없습니다: {string.Join(", ", missing)}");
            else
            {
                int line = 1;
                foreach (var row in placeTable.Rows)
                {
                    line++;
                    try
                    {
                        string id = row["id"];
                        if (id.Length == 0 || row["name"].Length == 0) throw new FormatException("id와 name을 적어야 합니다.");
                        if (places.Any(p => p.Id == id)) throw new FormatException($"'{id}'가 두 번 있습니다.");
                        int distance = SheetValues.ParseNumber(row["distance"], "distance");
                        int radius = SheetValues.ParseNumber(row["radius"], "radius");
                        if (distance < 0) throw new FormatException("distance는 0 이상이어야 합니다.");
                        if (radius < 3 || radius > MaxRadius) throw new FormatException($"radius는 3~{MaxRadius}이어야 합니다.");
                        string road = row["road"];
                        if (road.Length > 0 && places.All(p => p.Id != road)) throw new FormatException($"road '{road}'는 위에 적은 장소여야 합니다.");
                        var floor = row["floor"].Length == 0 ? TileType.Floor : SheetValues.ParseEnum<TileType>(row["floor"], "floor");
                        if (floor != TileType.Floor && !TileMap.IsPaving(floor)) throw new FormatException("floor는 비우거나(맨바닥) 깔 수 있는 바닥이어야 합니다.");
                        bool waypoint = row["waypoint"].Length == 0 || row["waypoint"] == "yes";
                        places.Add(new PlaceDefinition(places.Count, id, row["name"], row["kind"], distance, radius, road, waypoint, floor, row["text"]));
                    }
                    catch (FormatException e)
                    {
                        errors.Add($"{PlacesFile} {line}줄: {e.Message}");
                    }
                }
            }

            var characterTable = CsvTable.Read(charactersCsv ?? "");
            missing = characterTable.MissingColumns("id", "name", "place");
            if (missing.Count > 0) errors.Add($"{CharactersFile}: 열이 없습니다: {string.Join(", ", missing)}");
            else
            {
                int line = 1;
                foreach (var row in characterTable.Rows)
                {
                    line++;
                    try
                    {
                        string id = row["id"];
                        if (id.Length == 0 || row["name"].Length == 0) throw new FormatException("id와 name을 적어야 합니다.");
                        if (characters.Any(c => c.Id == id)) throw new FormatException($"'{id}'가 두 번 있습니다.");
                        var place = places.FirstOrDefault(p => p.Id == row["place"]) ?? throw new FormatException($"place '{row["place"]}'가 {PlacesFile}에 없습니다.");
                        var lines = row["lines"].Split('|').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
                        characters.Add(new CharacterDefinition(characters.Count, id, row["name"], place, row["look"], row["color"], lines));
                    }
                    catch (FormatException e)
                    {
                        errors.Add($"{CharactersFile} {line}줄: {e.Message}");
                    }
                }
                foreach (var place in places)
                {
                    if (characters.Count(c => c.Place == place) > place.Radius * 2) errors.Add($"{CharactersFile}: '{place.Id}'에 인물이 너무 많습니다(반지름의 두 배까지).");
                }
            }

            var storyTable = CsvTable.Read(storyCsv ?? "");
            missing = storyTable.MissingColumns("step", "title", "who", "lines");
            if (missing.Count > 0) errors.Add($"{StoryFile}: 열이 없습니다: {string.Join(", ", missing)}");
            else
            {
                int line = 1;
                foreach (var row in storyTable.Rows)
                {
                    line++;
                    try
                    {
                        int number = SheetValues.ParseNumber(row["step"], "step");
                        if (number != steps.Count + 1) throw new FormatException($"step은 1부터 차례로 적습니다(여기는 {steps.Count + 1}).");
                        var who = characters.FirstOrDefault(c => c.Id == row["who"]) ?? throw new FormatException($"who '{row["who"]}'가 {CharactersFile}에 없습니다.");
                        var lines = new List<(CharacterDefinition, string)>();
                        foreach (string part in row["lines"].Split('|').Select(l => l.Trim()).Where(l => l.Length > 0))
                        {
                            if (part.StartsWith("@"))
                            {
                                int space = part.IndexOf(' ');
                                string speakerId = space < 0 ? part.Substring(1) : part.Substring(1, space - 1);
                                var speaker = characters.FirstOrDefault(c => c.Id == speakerId) ?? throw new FormatException($"말하는 사람 '@{speakerId}'가 {CharactersFile}에 없습니다.");
                                string text = space < 0 ? "" : part.Substring(space + 1).Trim();
                                if (text.Length == 0) throw new FormatException($"'@{speakerId}' 뒤에 할 말을 적어야 합니다.");
                                lines.Add((speaker, text));
                            }
                            else lines.Add((who, part));
                        }
                        if (lines.Count == 0) throw new FormatException("lines에 할 말을 하나 이상 적어야 합니다.");
                        var (goal, place, boss, material, consumable, count) = Goal(row["goal"], places, loot, bosses);
                        var rewards = row["reward"].Length == 0 || loot == null
                            ? new List<GoalReward>()
                            : row["reward"].Split('|').Select(part => GuideSheet.Reward(part.Trim(), loot)).ToList();
                        steps.Add(new StoryStep(steps.Count, row["chapter"], row["title"], who, lines, goal, place, boss, material, consumable, count, rewards, row["hint"]));
                    }
                    catch (FormatException e)
                    {
                        errors.Add($"{StoryFile} {line}줄: {e.Message}");
                    }
                }
            }
            return new StoryData(places, characters, steps, errors);
        }

        // "" (done on talking), "reach:<place>", "boss:<boss id>" or "have:<material or consumable>:<count>".
        static (StoryGoal, PlaceDefinition, string, MaterialDefinition, ConsumableDefinition, int) Goal(string text, List<PlaceDefinition> places, LootData loot, BossData bosses)
        {
            if (text.Length == 0) return (StoryGoal.None, null, null, null, null, 0);
            string[] parts = text.Split(':');
            switch (parts[0])
            {
                case "reach" when parts.Length == 2:
                    var place = places.FirstOrDefault(p => p.Id == parts[1]) ?? throw new FormatException($"goal의 장소 '{parts[1]}'가 {PlacesFile}에 없습니다.");
                    return (StoryGoal.Reach, place, null, null, null, 0);
                case "boss" when parts.Length == 2:
                    if (bosses != null && bosses.Bosses.All(b => b.Id != parts[1])) throw new FormatException($"goal의 보스 '{parts[1]}'가 보스 시트에 없습니다.");
                    return (StoryGoal.Boss, null, parts[1], null, null, 1);
                case "have" when parts.Length == 3:
                    int count = SheetValues.ParseNumber(parts[2], "개수");
                    if (count < 1) throw new FormatException("have의 개수는 1 이상이어야 합니다.");
                    var material = loot?.Materials.FirstOrDefault(m => m.Id == parts[1]);
                    var consumable = loot?.Consumables.FirstOrDefault(c => c.Id == parts[1]);
                    if (loot != null && material == null && consumable == null) throw new FormatException($"goal의 '{parts[1]}'는 재료나 소모품이 아닙니다.");
                    return (StoryGoal.Have, null, null, material, consumable, count);
            }
            throw new FormatException($"goal '{text}'는 비우거나 reach:<장소>, boss:<보스>, have:<id>:<개수>로 적습니다.");
        }
    }
}
