namespace Game.Core
{
    // Rewards for the guide's goals (Docs/Guide.md). The guide itself runs on each player's device; when it passes a
    // goal that has a reward, the client asks for it, and the host gives it once, if the goal is met as it sees it.
    public sealed partial class World
    {
        // Gives the goal's rewards to the player the first time, when one of its conditions is met. index is the
        // goal's place in the guide, reported back in the GoalReward event.
        public bool ClaimGoal(int playerId, Goal goal, int index)
        {
            var player = FindPlayer(playerId);
            if (player == null || goal == null || goal.Rewards.Count == 0 || player.ClaimedGoals.Contains(goal.Id)) return false;
            var facts = GuideFacts.Of(this, player);
            bool met = false;
            foreach (var condition in goal.Conditions) met |= GuideProgress.IsMet(condition, facts);
            if (!met) return false;
            player.ClaimedGoals.Add(goal.Id);
            foreach (var reward in goal.Rewards)
            {
                if (reward.Material != null) player.Inventory.AddMaterial(reward.Material, reward.Count);
                else if (reward.Consumable != null) player.Inventory.AddConsumable(reward.Consumable, reward.Count);
                else GainExperience(player, reward.Count);
            }
            player.InventoryVersion++;
            _events.Add(new SimEvent(SimEventType.GoalReward, player.Id, amount: index));
            return true;
        }
    }
}
