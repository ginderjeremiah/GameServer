using Game.Api.Models.InventoryItems;

namespace Game.Api.Models.Enemies
{
    public class DefeatRewards
    {
        public int ExpReward { get; set; }
        public List<InventoryItem> Drops { get; set; }

        public DefeatRewards() { }

        public DefeatRewards(Core.BattleSimulation.DefeatRewards rewards)
        {
            ExpReward = rewards.ExpReward;
            Drops = rewards.Drops.Select(item => new InventoryItem(item)).ToList();
        }
    }
}
