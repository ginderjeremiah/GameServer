using DataAccess.Models.InventoryItems;

namespace GameServer.Models.Enemies
{
    public class DefeatRewards : IModel
    {
        public int ExpReward { get; set; }
        public List<InventoryItem> Drops { get; set; }
    }
}
