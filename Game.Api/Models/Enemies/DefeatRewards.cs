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

        public DefeatRewards() { }

        public DefeatRewards(DefeatResult result)
        {
            ExpReward = result.ExpReward;
            NewLevel = result.NewLevel;
            NewExp = result.NewExp;
            StatPointsGained = result.StatPointsGained;
            StatPointsUsed = result.StatPointsUsed;
        }
    }
}
