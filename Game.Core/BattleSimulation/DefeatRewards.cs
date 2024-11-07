using Game.Core.Entities;

namespace Game.Core.BattleSimulation
{
    public class DefeatRewards
    {
        public int ExpReward { get; set; }
        public List<InventoryItem> Drops { get; set; }
    }
}
