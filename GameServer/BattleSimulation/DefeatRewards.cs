using DataAccess.Models.InventoryItems;

namespace GameServer.BattleSimulation
{
    public class DefeatRewards
    {
        public int ExpReward { get; set; }
        public List<InventoryItem> Drops { get; set; }
    }
}
