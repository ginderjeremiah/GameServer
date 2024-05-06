using GameServer.Models.InventoryItems;

namespace GameServer.Models.Enemies
{
    public class DefeatRewards
    {
        public int ExpReward { get; set; }
        public List<InventoryItem> Drops { get; set; }

        public DefeatRewards(GameCore.BattleSimulation.DefeatRewards rewards)
        {
            ExpReward = rewards.ExpReward;
            Drops = rewards.Drops.Select(item => new InventoryItem(item)).ToList();
        }
    }
}
