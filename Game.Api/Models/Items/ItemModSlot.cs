using Game.Core;
using ItemModSlotEntity = Game.Abstractions.Entities.ItemModSlot;

namespace Game.Api.Models.Items
{
    public class ItemModSlot : IModelFromSource<ItemModSlot, ItemModSlotEntity>
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public EItemModType ItemModSlotTypeId { get; set; }

        public static ItemModSlot FromSource(ItemModSlotEntity itemSlot)
        {
            return new ItemModSlot
            {
                Id = itemSlot.Id,
                ItemId = itemSlot.ItemId,
                ItemModSlotTypeId = (EItemModType)itemSlot.ItemModSlotTypeId,
            };
        }
    }
}
