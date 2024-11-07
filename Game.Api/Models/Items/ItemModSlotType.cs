using ItemModSlotTypeEntity = Game.Core.Entities.ItemModSlotType;

namespace Game.Api.Models.Items
{
    public class ItemModSlotType : IModelFromSource<ItemModSlotType, ItemModSlotTypeEntity>
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public static ItemModSlotType FromSource(ItemModSlotTypeEntity slotType)
        {
            return new ItemModSlotType
            {
                Id = slotType.Id,
                Name = slotType.Name,
            };
        }
    }
}
