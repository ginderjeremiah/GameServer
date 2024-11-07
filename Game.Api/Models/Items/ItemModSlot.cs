using Game.Core;
using ItemModSlotEntity = Game.Core.Entities.ItemModSlot;

namespace Game.Api.Models.Items
{
    public class ItemModSlot : IModelFromSource<ItemModSlot, ItemModSlotEntity>
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public EItemModSlotType ItemModSlotTypeId { get; set; }
        public int? GuaranteedItemModId { get; set; }
        public decimal Probability { get; set; }

        public static ItemModSlot FromSource(ItemModSlotEntity itemSlot)
        {
            return new ItemModSlot
            {
                Id = itemSlot.Id,
                ItemId = itemSlot.ItemId,
                ItemModSlotTypeId = (EItemModSlotType)itemSlot.ItemModSlotTypeId,
                GuaranteedItemModId = itemSlot.GuaranteedItemModId,
                Probability = itemSlot.Probability,
            };
        }
    }
}
