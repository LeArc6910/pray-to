using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // The story and its places (Docs/Story.md, 2026-11-10). Each player goes through story.csv a step at a time: they
    // talk to the step's giver, who says its lines; a step with nothing more to do is done then, and one with a goal
    // is done when its place is reached or its boss has fallen, or, for things to bring, when the player talks to the
    // giver again holding them. Each place's waypoint opens for a player who comes within WaypointReach of it, and
    // from beside any open waypoint they go to any other open one.
    public sealed partial class World
    {
        // How near a person a player talks to them, and how near a waypoint it opens and can be used.
        public const float TalkReach = 1.8f;
        public const float WaypointReach = 1.6f;
        // A Story event's Amount for a step taken (talked to its giver, its goal still to do), and Waypoint's for one
        // newly open and for one gone to.
        public const int StoryTaken = -1;
        public const int WaypointOpened = 1;
        public const int WaypointUsed = 2;

        public StoryData Story { get; private set; }
        public IReadOnlyList<PlaceSite> Places { get; private set; } = Array.Empty<PlaceSite>();

        // The story sheets and where the generator put their places (the same seed and sheets on host and clients).
        internal void SetStory(StoryData story, IReadOnlyList<PlaceSite> places)
        {
            Story = story;
            Places = places ?? Array.Empty<PlaceSite>();
        }

        // The place the position is in, or null.
        public PlaceSite PlaceAt(Vec2 position) => Places.FirstOrDefault(p => p.Contains(position));

        public PlaceSite SiteOf(PlaceDefinition place) => place == null ? null : Places.FirstOrDefault(p => p.Definition == place);

        // The person of the story nearest the position within TalkReach, and where they stand, or null.
        public static (CharacterDefinition Character, Vec2 At)? CharacterNear(IReadOnlyList<PlaceSite> places, Vec2 position)
        {
            (CharacterDefinition, Vec2)? best = null;
            float nearest = TalkReach;
            foreach (var site in places)
            {
                if (Vec2.Distance(position, site.Center) > site.Radius + TalkReach + 1) continue;
                foreach (var (character, at) in site.People)
                {
                    float distance = Vec2.Distance(position, at);
                    if (distance > nearest) continue;
                    nearest = distance;
                    best = (character, at);
                }
            }
            return best;
        }

        // The open waypoint within reach, or null.
        public static PlaceSite WaypointNear(IReadOnlyList<PlaceSite> places, Vec2 position) =>
            places.FirstOrDefault(p => p.Definition.Waypoint && Vec2.Distance(position, p.Center) <= WaypointReach);

        // The step the player is on, or null once the story so far is done.
        public StoryStep StepOf(Player player) => Story?.Step(player.JourneyStep);

        // Whether the step's goal is met for the player now.
        public bool GoalMet(Player player, StoryStep step)
        {
            switch (step.Goal)
            {
                case StoryGoal.Reach:
                    return SiteOf(step.Place)?.Contains(player.Position) ?? false;
                case StoryGoal.Boss:
                    return player.Records.TryGetValue((RecordKind.Boss, step.Boss), out int felled) && felled > 0;
                case StoryGoal.Have:
                    return (step.Material != null ? player.Inventory.CountOf(step.Material) : player.Inventory.CountOf(step.Consumable)) >= step.Count;
            }
            return true;
        }

        // The player talks to the person in reach. The step's giver gives the step (and, holding what it asks, takes
        // it); anyone else only chats, which the screen shows by itself. False when nothing changed.
        public bool TalkInStory(int playerId)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || Story == null) return false;
            var near = CharacterNear(Places, player.Position);
            var step = StepOf(player);
            if (near == null || step == null || near.Value.Character != step.Who) return false;
            if (!player.JourneyTaken)
            {
                player.JourneyTaken = true;
                player.JourneyVersion++;
                _events.Add(new SimEvent(SimEventType.Story, player.Id, target: step.Index, amount: StoryTaken, position: near.Value.At));
            }
            // A step that asks waits for the answer (ChooseInStory).
            if (step.Choices.Count == 0 && (step.Goal == StoryGoal.None || GoalMet(player, step))) CompleteStep(player, step);
            return true;
        }

        // The player answers what the step's giver in reach asked (story.csv choices, 2026-11-10): the answer is
        // remembered, the step done, and the story goes on at the step the answer leads to. False when there is no
        // such question or answer.
        public bool ChooseInStory(int playerId, int choice)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || Story == null) return false;
            var near = CharacterNear(Places, player.Position);
            var step = StepOf(player);
            if (near == null || step == null || near.Value.Character != step.Who || choice < 0 || choice >= step.Choices.Count) return false;
            player.StoryChoices[step.Index] = choice;
            CompleteStep(player, step, step.Choices[choice].Step);
            return true;
        }

        void CompleteStep(Player player, StoryStep step, int? next = null)
        {
            if (step.Goal == StoryGoal.Have)
            {
                if (step.Material != null) player.Inventory.TakeMaterial(step.Material, step.Count);
                else for (int i = 0; i < step.Count; i++) player.Inventory.TakeConsumable(step.Consumable);
            }
            foreach (var reward in step.Rewards)
            {
                if (reward.IsExperience) GainExperience(player, reward.Count);
                else if (reward.Material != null) player.Inventory.AddMaterial(reward.Material, reward.Count);
                else player.Inventory.AddConsumable(reward.Consumable, reward.Count);
            }
            player.InventoryVersion++;
            player.JourneyStep = next ?? step.Next;
            player.JourneyTaken = false;
            player.JourneyVersion++;
            _events.Add(new SimEvent(SimEventType.Story, player.Id, target: step.Index, amount: step.Index + 1, position: player.Position));
        }

        // Every half second: the waypoints the players have come to open, and a taken step whose place is reached or
        // whose boss has fallen is done.
        void UpdateStory()
        {
            if (Story == null || Places.Count == 0 || Tick % (TicksPerSecond / 2) != 0) return;
            foreach (var player in _players)
            {
                if (player.IsDead) continue;
                var waypoint = WaypointNear(Places, player.Position);
                if (waypoint != null && player.Waypoints.Add(waypoint.Definition.Id))
                {
                    player.JourneyVersion++;
                    _events.Add(new SimEvent(SimEventType.Waypoint, player.Id, target: waypoint.Definition.Index, amount: WaypointOpened, position: waypoint.Center));
                }
                var step = StepOf(player);
                if (step != null && player.JourneyTaken && (step.Goal == StoryGoal.Reach || step.Goal == StoryGoal.Boss) && GoalMet(player, step))
                    CompleteStep(player, step);
            }
        }

        // From beside an open waypoint to another open one: the player stands just below it. False when they are not by
        // an open waypoint or that one is not open to them.
        public bool Teleport(int playerId, int placeIndex)
        {
            var player = FindPlayer(playerId);
            if (player == null || player.IsDead || Story == null || placeIndex < 0 || placeIndex >= Story.Places.Count) return false;
            var here = WaypointNear(Places, player.Position);
            var there = SiteOf(Story.Places[placeIndex]);
            if (here == null || there == null || here == there || !player.Waypoints.Contains(here.Definition.Id) || !player.Waypoints.Contains(there.Definition.Id))
                return false;
            player.Position = Collision.Resolve(Map, TileMap.Center(there.X, there.Y - 1), PlayerRadius);
            player.Corrections++;
            _events.Add(new SimEvent(SimEventType.Waypoint, player.Id, target: there.Definition.Index, amount: WaypointUsed, position: player.Position));
            return true;
        }

        // Puts back what a character save remembered: the step (no further than the story goes), whether it was taken,
        // the open waypoints that are still places and the answers given.
        public void RestoreJourney(Player player, int step, bool taken, IEnumerable<string> waypoints, IEnumerable<(int Step, int Choice)> choices = null)
        {
            int last = Story?.Steps.Count ?? int.MaxValue;
            player.JourneyStep = Math.Clamp(step, 0, last);
            player.JourneyTaken = taken && player.JourneyStep < last;
            foreach (var (at, choice) in choices ?? Enumerable.Empty<(int, int)>()) player.StoryChoices[at] = choice;
            foreach (string id in waypoints ?? Enumerable.Empty<string>())
            {
                if (Story == null || Story.Place(id) != null) player.Waypoints.Add(id);
            }
            player.JourneyVersion++;
        }
    }
}
