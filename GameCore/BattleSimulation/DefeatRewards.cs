using GameCore.Entities.InventoryItems;

namespace GameCore.BattleSimulation
{
    public class DefeatRewards
    {
        public int ExpReward { get; set; }
        public List<InventoryItem> Drops { get; set; }
    }
}
