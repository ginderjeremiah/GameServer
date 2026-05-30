using Game.Api.Models.Attributes;
using Game.Core;
using ItemEntity = Game.Abstractions.Entities.Item;

namespace Game.Api.Models.Items
{
    public class Item : IModelFromSource<Item, ItemEntity>
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public EItemCategory ItemCategoryId { get; set; }
        public ERarity RarityId { get; set; }
        public required string IconPath { get; set; }
        public required IEnumerable<BattlerAttribute> Attributes { get; set; }
        public required IEnumerable<ItemModSlot> ModSlots { get; set; }
        public static Item FromSource(ItemEntity item)
        {
            return new Item
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                ItemCategoryId = (EItemCategory)item.ItemCategoryId,
                RarityId = (ERarity)item.RarityId,
                IconPath = item.IconPath,
                Attributes = item.ItemAttributes.To().Model<BattlerAttribute>(),
                ModSlots = item.ItemModSlots.To().Model<ItemModSlot>(),
            };
        }
    }
}
