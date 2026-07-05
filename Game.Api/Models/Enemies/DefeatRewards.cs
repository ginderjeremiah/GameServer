using Game.Application;

namespace Game.Api.Models.Enemies
{
    public class DefeatRewards
    {
        public int ExpReward { get; set; }
        public int NewLevel { get; set; }
        public int NewExp { get; set; }
        public int StatPointsGained { get; set; }
        public int StatPointsUsed { get; set; }

        /// <summary>
        /// The player's post-victory combat-rating capability measure (spike #1526 Decision 7) — display-only,
        /// never recomputed client-side (no parity surface).
        /// </summary>
        public double PlayerRating { get; set; }

        public DefeatRewards() { }

        public DefeatRewards(DefeatResult result)
        {
            ExpReward = result.ExpReward;
            NewLevel = result.NewLevel;
            NewExp = result.NewExp;
            StatPointsGained = result.StatPointsGained;
            StatPointsUsed = result.StatPointsUsed;
            PlayerRating = result.PlayerRating;
        }
    }
}
