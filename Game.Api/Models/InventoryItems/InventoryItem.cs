namespace Game.Api.Models.InventoryItems
{
    public class InventoryItem : IModel
    {
        public int ItemId { get; set; }
        public bool Equipped { get; set; }
        public int? EquipmentSlotId { get; set; }
        public bool Favorite { get; set; }
        public List<AppliedModModel> AppliedMods { get; set; } = [];
    }

    public class AppliedModModel
    {
        public int ItemModId { get; set; }
        public int ItemModSlotId { get; set; }
    }
}
