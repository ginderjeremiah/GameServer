using ItemModSlotTypeEntity = Game.Core.Entities.ItemModType;

namespace Game.Api.Models.Items
{
    public class ItemModType : IModelFromSource<ItemModType, ItemModSlotTypeEntity>
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public static ItemModType FromSource(ItemModSlotTypeEntity slotType)
        {
            return new ItemModType
            {
                Id = slotType.Id,
                Name = slotType.Name,
            };
        }
    }
}
