using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // A place people live (Docs/Story.md, 장소, 2026-11-10): carved into the caves when the world is made, round a
    // plaza of its floor, joined to an earlier place by a road, with a waypoint in the middle.
    public sealed class PlaceDefinition
    {
        internal PlaceDefinition(int index, string id, string name, string kind, int distance, int radius, string road, bool waypoint, TileType floor, string text,
            string style = "", bool arena = false, int shops = 0)
        {
            Arena = arena;
            Shops = shops;
            Style = style;
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
        // How it is dressed (WorldGenerator.Decorate): village, kingdom, hut, nest, or "" for a bare plaza with lamps.
        public string Style { get; }
        // Whether a boss arena is carved beside it (places.csv arena, 2026-11-11), for a boss with this place.
        public bool Arena { get; }
        // How many shop buildings stand round it (places.csv shops, 2026-11-11): small timber rooms with a door, where
        // the people who keep shop there stand behind their stalls (World.PlaceNpcs).
        public int Shops { get; }
    }

    // Someone in the story (Docs/Story.md, 인물): where they live, how they look, and what they say when it is not
    // their turn in the story.
    public sealed class CharacterDefinition
    {
        internal CharacterDefinition(int index, string id, string name, PlaceDefinition place, string look, string color, IReadOnlyList<(string Text, StoryWhen When)> lines,
            string walk = "wander")
        {
            Walk = walk;
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
        // How they spend the day (characters.csv walk, 2026-11-11): "wander" round the plaza, "run" round it quicker
        // (children), or "stay" where they stand (PlaceSite.PersonAt).
        public string Walk { get; }
        // What they say when it is not their turn, each line maybe only after an answer (StoryWhen).
        public IReadOnlyList<(string Text, StoryWhen When)> Lines { get; }

        // The day's line among those that hold for the answers given (none given: the lines without a condition).
        public string LineFor(int day, IReadOnlyDictionary<int, int> choices = null)
        {
            var lines = Lines.Where(l => l.When == null || l.When.Holds(choices)).ToList();
            return lines.Count == 0 ? "" : lines[Math.Max(0, day) % lines.Count].Text;
        }
    }

    // A condition on a line (story.csv and characters.csv lines, 2026-11-11): "?9=1 ..." is said only to one who gave
    // the first answer where step 9 asked, "?9=0 ..." only to one who has not answered there yet.
    public sealed class StoryWhen
    {
        internal StoryWhen(int step, int choice, string text)
        {
            Step = step;
            Choice = choice;
            Text = text;
        }

        // The step that asked (index) and the answer (index), or -1 for not answered yet.
        public int Step { get; }
        public int Choice { get; }
        // As written, for errors.
        public string Text { get; }

        public bool Holds(IReadOnlyDictionary<int, int> choices) =>
            choices != null && choices.TryGetValue(Step, out int given) ? given == Choice : Choice < 0;
    }

    // A side story (story.csv chain, 2026-11-11): rows with the same chain id, gone through apart from the main story
    // and from each other. It opens once the main story is past After steps.
    public sealed class StoryChain
    {
        internal StoryChain(int index, string id, StoryStep first, int after)
        {
            Index = index;
            Id = id;
            First = first;
            After = after;
        }

        public int Index { get; }
        public string Id { get; }
        public StoryStep First { get; }
        // How many main steps come first (story.csv after on the chain's first row): open while JourneyStep >= After.
        public int After { get; }
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
        internal StoryStep(int index, string chapter, string title, CharacterDefinition who, IReadOnlyList<(CharacterDefinition Speaker, string Text, StoryWhen When)> lines,
            StoryGoal goal, PlaceDefinition place, string boss, MaterialDefinition material, ConsumableDefinition consumable, int count,
            IReadOnlyList<GoalReward> rewards, string hint, string chain = "")
        {
            Chain = chain;
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
        // What is said on talking to Who: each line by Who, or by another character when it began with @id, and maybe
        // only after an answer (?step=n before it).
        public IReadOnlyList<(CharacterDefinition Speaker, string Text, StoryWhen When)> Lines { get; }
        // The side story it belongs to (story.csv chain), or "" for the main story.
        public string Chain { get; }
        public StoryGoal Goal { get; }
        public PlaceDefinition Place { get; }
        public string Boss { get; }
        public MaterialDefinition Material { get; }
        public ConsumableDefinition Consumable { get; }
        public int Count { get; }
        public IReadOnlyList<GoalReward> Rewards { get; }
        public string Hint { get; }
        // What the player may answer at the end of the talk (story.csv choices, 2026-11-10), each with the step it
        // leads to (index); none for a step that only talks or asks for something.
        public IReadOnlyList<(string Text, int Step)> Choices { get; internal set; } = Array.Empty<(string, int)>();
        // The step that follows this one when done (story.csv next): the next row unless the sheet says another, or
        // the end (Steps.Count).
        public int Next { get; internal set; }

        public string RewardText => string.Join(" · ", Rewards.Select(r => r.Text));

        // The lines said to one who gave these answers (StoryWhen); at least the ones without a condition.
        public IReadOnlyList<(CharacterDefinition Speaker, string Text, StoryWhen When)> LinesFor(IReadOnlyDictionary<int, int> choices) =>
            Lines.Where(l => l.When == null || l.When.Holds(choices)).ToList();

        // Where the step leads: the place to reach, or else the giver's.
        public PlaceDefinition Target => Goal == StoryGoal.Reach ? Place : Who.Place;
    }

    public sealed class StoryData
    {
        public StoryData(IReadOnlyList<PlaceDefinition> places, IReadOnlyList<CharacterDefinition> characters, IReadOnlyList<StoryStep> steps, IReadOnlyList<string> errors,
            IReadOnlyList<StoryChain> chains = null)
        {
            Chains = chains ?? Array.Empty<StoryChain>();
            Places = places;
            Characters = characters;
            Steps = steps;
            Errors = errors;
        }

        public IReadOnlyList<PlaceDefinition> Places { get; }
        public IReadOnlyList<CharacterDefinition> Characters { get; }
        public IReadOnlyList<StoryStep> Steps { get; }
        // The side stories, in the order of their first rows.
        public IReadOnlyList<StoryChain> Chains { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool Success => Errors.Count == 0;

        public PlaceDefinition Place(string id) => Places.FirstOrDefault(p => p.Id == id);
        public CharacterDefinition Character(string id) => Characters.FirstOrDefault(c => c.Id == id);
        public StoryStep Step(int index) => index >= 0 && index < Steps.Count ? Steps[index] : null;
        public StoryChain Chain(string id) => Chains.FirstOrDefault(c => c.Id == id);

        // The steps a player has now and whether each is taken: the main story's, then each open side story's that is
        // not done, in sheet order. The same on host (Player) and client (WorldClient).
        public List<(StoryStep Step, bool Taken)> Current(int journeyStep, bool journeyTaken, Dictionary<string, int> sideSteps, HashSet<string> sideTaken)
        {
            var steps = new List<(StoryStep, bool)>();
            var main = Step(journeyStep);
            if (main != null && main.Chain.Length == 0) steps.Add((main, journeyTaken));
            foreach (var chain in Chains)
            {
                if (journeyStep < chain.After) continue;
                int at = sideSteps != null && sideSteps.TryGetValue(chain.Id, out int saved) ? saved : chain.First.Index;
                var step = Step(at);
                if (step != null && step.Chain == chain.Id) steps.Add((step, sideTaken != null && sideTaken.Contains(chain.Id)));
            }
            return steps;
        }

        // The steps passed on the way from first to the step reached (the main story from 0, a side story from its
        // first row), following the answers given where it asked; for the journal (2026-11-11).
        public List<StoryStep> Passed(int first, int reached, IReadOnlyDictionary<int, int> choices)
        {
            var passed = new List<StoryStep>();
            int at = first;
            for (int guard = 0; guard < Steps.Count && at != reached && Step(at) is StoryStep step; guard++)
            {
                passed.Add(step);
                at = step.Choices.Count > 0 && choices != null && choices.TryGetValue(step.Index, out int answer) && answer < step.Choices.Count
                    ? step.Choices[answer].Step
                    : step.Next;
            }
            return passed;
        }

        // The step the character gives now, if any (main story first), and whether it is taken.
        public (StoryStep Step, bool Taken)? GivenBy(CharacterDefinition who, int journeyStep, bool journeyTaken, Dictionary<string, int> sideSteps, HashSet<string> sideTaken)
        {
            foreach (var current in Current(journeyStep, journeyTaken, sideSteps, sideTaken))
            {
                if (current.Step.Who == who) return current;
            }
            return null;
        }
    }

    // Reads Assets/_Project/Data/Story/places.csv, characters.csv and story.csv (Docs/Story.md). A row that does not
    // read is left out with a reason in Errors, as the other sheets do.
    public static class StorySheet
    {
        public const string PlacesFile = "places.csv";
        public const string CharactersFile = "characters.csv";
        public const string StoryFile = "story.csv";
        public const int MaxRadius = 12;
        public const int MaxShops = 4;
        public static readonly string[] Styles = { "village", "kingdom", "hut", "nest", "harbor", "ruins", "mine" };

        public static StoryData Parse(string placesCsv, string charactersCsv, string storyCsv, LootData loot, BossData bosses = null)
        {
            var errors = new List<string>();
            var places = new List<PlaceDefinition>();
            var characters = new List<CharacterDefinition>();
            var steps = new List<StoryStep>();
            var chains = new List<StoryChain>();

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
                        string style = row["style"];
                        if (style.Length > 0 && !Styles.Contains(style)) throw new FormatException($"style은 비우거나 {string.Join(", ", Styles)} 가운데 하나여야 합니다.");
                        bool arena = row["arena"] == "yes";
                        int shops = row["shops"].Length == 0 ? 0 : SheetValues.ParseNumber(row["shops"], "shops");
                        if (shops < 0 || shops > MaxShops) throw new FormatException($"shops는 0~{MaxShops}이어야 합니다.");
                        places.Add(new PlaceDefinition(places.Count, id, row["name"], row["kind"], distance, radius, road, waypoint, floor, row["text"], style, arena, shops));
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
                        var lines = row["lines"].Split('|').Select(l => l.Trim()).Where(l => l.Length > 0).Select(When).ToList();
                        string walk = row["walk"].Length == 0 ? "wander" : row["walk"];
                        if (walk != "wander" && walk != "run" && walk != "stay") throw new FormatException($"walk '{walk}'는 비우거나 wander, run, stay 가운데 하나여야 합니다.");
                        characters.Add(new CharacterDefinition(characters.Count, id, row["name"], place, row["look"], row["color"], lines, walk));
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
                var links = new List<(StoryStep Step, string Choices, string Next, string After, int Line)>();
                foreach (var row in storyTable.Rows)
                {
                    line++;
                    try
                    {
                        int number = SheetValues.ParseNumber(row["step"], "step");
                        if (number != steps.Count + 1) throw new FormatException($"step은 1부터 차례로 적습니다(여기는 {steps.Count + 1}).");
                        var who = characters.FirstOrDefault(c => c.Id == row["who"]) ?? throw new FormatException($"who '{row["who"]}'가 {CharactersFile}에 없습니다.");
                        var lines = new List<(CharacterDefinition, string, StoryWhen)>();
                        foreach (string written in row["lines"].Split('|').Select(l => l.Trim()).Where(l => l.Length > 0))
                        {
                            var (part, when) = When(written);
                            if (part.StartsWith("@"))
                            {
                                int space = part.IndexOf(' ');
                                string speakerId = space < 0 ? part.Substring(1) : part.Substring(1, space - 1);
                                var speaker = characters.FirstOrDefault(c => c.Id == speakerId) ?? throw new FormatException($"말하는 사람 '@{speakerId}'가 {CharactersFile}에 없습니다.");
                                string text = space < 0 ? "" : part.Substring(space + 1).Trim();
                                if (text.Length == 0) throw new FormatException($"'@{speakerId}' 뒤에 할 말을 적어야 합니다.");
                                lines.Add((speaker, text, when));
                            }
                            else lines.Add((who, part, when));
                        }
                        if (lines.Count == 0) throw new FormatException("lines에 할 말을 하나 이상 적어야 합니다.");
                        if (lines.All(l => l.Item3 != null)) throw new FormatException("lines에 조건(?step=n) 없는 말을 하나 이상 적어야 합니다.");
                        string chain = row["chain"];
                        if (chain.Length > 0 && !chain.All(c => char.IsLetterOrDigit(c) || c == '_')) throw new FormatException($"chain '{chain}'은 영문·숫자·_로 적습니다.");
                        if (chain.Length > 0 && steps.Count == 0) throw new FormatException("step 1은 큰 이야기여야 합니다(chain을 비운다).");
                        var (goal, place, boss, material, consumable, count) = Goal(row["goal"], places, loot, bosses);
                        var rewards = row["reward"].Length == 0 || loot == null
                            ? new List<GoalReward>()
                            : row["reward"].Split('|').Select(part => GuideSheet.Reward(part.Trim(), loot)).ToList();
                        if (row["choices"].Length > 0 && goal != StoryGoal.None) throw new FormatException("choices는 goal이 빈 단계(말만 하는 단계)에만 적습니다.");
                        steps.Add(new StoryStep(steps.Count, row["chapter"], row["title"], who, lines, goal, place, boss, material, consumable, count, rewards, row["hint"], chain));
                        links.Add((steps[steps.Count - 1], row["choices"], row["next"], row["after"], line));
                    }
                    catch (FormatException e)
                    {
                        errors.Add($"{StoryFile} {line}줄: {e.Message}");
                    }
                }
                // Where each step leads, once every step is read (a choice or next may point at a later row).
                int StepNumber(string text, string what, string chain)
                {
                    if (text == "end") return steps.Count;
                    int number = SheetValues.ParseNumber(text, what);
                    if (number < 1 || number > steps.Count) throw new FormatException($"{what} '{text}'은 1~{steps.Count}의 step이거나 end여야 합니다.");
                    if (steps[number - 1].Chain != chain) throw new FormatException($"{what} '{text}'은 같은 이야기(chain '{chain}')의 step이어야 합니다.");
                    return number - 1;
                }
                foreach (var (step, choices, next, after, at) in links)
                {
                    try
                    {
                        // Unless the sheet says, the next row of the same story, or its end.
                        var following = steps.Skip(step.Index + 1).FirstOrDefault(s => s.Chain == step.Chain);
                        step.Next = next.Length == 0 ? following?.Index ?? steps.Count : StepNumber(next, "next", step.Chain);
                        bool first = steps.First(s => s.Chain == step.Chain) == step;
                        if (after.Length > 0 && (step.Chain.Length == 0 || !first)) throw new FormatException("after는 곁이야기(chain)의 첫 줄에만 적습니다.");
                        if (step.Chain.Length > 0 && first)
                        {
                            int opens = after.Length == 0 ? 0 : SheetValues.ParseNumber(after, "after");
                            if (opens < 0 || opens > steps.Count) throw new FormatException($"after는 0~{steps.Count}의 step이어야 합니다.");
                            if (opens > 0 && steps[opens - 1].Chain.Length > 0) throw new FormatException($"after '{after}'는 큰 이야기의 step이어야 합니다.");
                            chains.Add(new StoryChain(chains.Count, step.Chain, step, opens));
                        }
                        var answers = new List<(string, int)>();
                        foreach (string part in choices.Split('|').Select(c => c.Trim()).Where(c => c.Length > 0))
                        {
                            int arrow = part.LastIndexOf('>');
                            if (arrow < 1) throw new FormatException($"choices '{part}'는 <고를 말>><step>로 적습니다. 예: 말한다>10");
                            answers.Add((part.Substring(0, arrow).Trim(), StepNumber(part.Substring(arrow + 1).Trim(), "choices의 step", step.Chain)));
                        }
                        if (answers.Count == 1) throw new FormatException("choices는 둘 이상 적습니다.");
                        step.Choices = answers;
                    }
                    catch (FormatException e)
                    {
                        errors.Add($"{StoryFile} {at}줄: {e.Message}");
                    }
                }
                // A condition must name a step that asks, and one of its answers.
                string Wrong(StoryWhen when)
                {
                    if (when == null) return null;
                    var asked = when.Step >= 0 && when.Step < steps.Count ? steps[when.Step] : null;
                    if (asked == null || asked.Choices.Count == 0) return $"조건 '{when.Text}'의 step은 choices가 있는 step이어야 합니다.";
                    if (when.Choice >= asked.Choices.Count) return $"조건 '{when.Text}'의 답은 0~{asked.Choices.Count}이어야 합니다.";
                    return null;
                }
                foreach (var (step, _, _, _, at) in links)
                {
                    foreach (var said in step.Lines)
                    {
                        if (Wrong(said.When) is string error) errors.Add($"{StoryFile} {at}줄: {error}");
                    }
                }
                foreach (var character in characters)
                {
                    foreach (var said in character.Lines)
                    {
                        if (Wrong(said.When) is string error) errors.Add($"{CharactersFile} '{character.Id}': {error}");
                    }
                }
            }
            return new StoryData(places, characters, steps, errors, chains);
        }

        // "?9=1 말" → the words and the condition (answer 1 at step 9), or the line as it is.
        static (string Text, StoryWhen When) When(string line)
        {
            if (!line.StartsWith("?")) return (line, null);
            int space = line.IndexOf(' ');
            string condition = space < 0 ? line.Substring(1) : line.Substring(1, space - 1);
            string[] parts = condition.Split('=');
            if (parts.Length != 2) throw new FormatException($"조건 '?{condition}'은 ?<step>=<답 번호>로 적습니다. 예: ?9=1 (0은 아직 답하지 않음)");
            int step = SheetValues.ParseNumber(parts[0], "조건의 step");
            int answer = SheetValues.ParseNumber(parts[1], "조건의 답 번호");
            if (step < 1 || answer < 0) throw new FormatException($"조건 '?{condition}'의 step은 1 이상, 답 번호는 0 이상이어야 합니다.");
            string text = space < 0 ? "" : line.Substring(space + 1).Trim();
            if (text.Length == 0) throw new FormatException($"조건 '?{condition}' 뒤에 할 말을 적어야 합니다.");
            return (text, new StoryWhen(step - 1, answer - 1, "?" + condition));
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
