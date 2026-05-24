using Game.Api.Models.InventoryItems;
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

        /// <summary>Items dropped and added to inventory, with their assigned slot numbers.</summary>
        public List<InventoryItem> DroppedItems { get; set; } = [];

        public DefeatRewards() { }

        public DefeatRewards(DefeatResult result)
        {
            ExpReward = result.ExpReward;
            NewLevel = result.NewLevel;
            NewExp = result.NewExp;
            StatPointsGained = result.StatPointsGained;
            StatPointsUsed = result.StatPointsUsed;
            DroppedItems = result.DroppedItems
                .Select(d => new InventoryItem(d.InventoryItemId, d.SlotNumber, d.Item))
                .ToList();
        }
    }
}
