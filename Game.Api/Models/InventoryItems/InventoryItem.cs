namespace Game.Api.Models.InventoryItems
{
    public class InventoryItem : IModel
    {
        public int ItemId { get; set; }
        public bool Equipped { get; set; }
        public int? EquipmentSlotId { get; set; }
        public List<AppliedModModel> AppliedMods { get; set; } = [];

        public InventoryItem() { }

        public InventoryItem(Abstractions.Entities.UnlockedItem ui, IEnumerable<Abstractions.Entities.AppliedMod>? appliedMods = null)
        {
            ItemId = ui.ItemId;
            Equipped = ui.EquipmentSlotId.HasValue;
            EquipmentSlotId = ui.EquipmentSlotId;
            AppliedMods = (appliedMods ?? [])
                .Where(am => am.ItemId == ui.ItemId)
                .Select(am => new AppliedModModel
                {
                    ItemModId = am.ItemModId,
                    ItemModSlotId = am.ItemModSlotId,
                }).ToList();
        }
    }

    public class AppliedModModel
    {
        public int ItemModId { get; set; }
        public int ItemModSlotId { get; set; }
    }
}
