using Game.Api.Models.Attributes;
using Game.Core;
using ItemEntity = Game.Core.Entities.Item;

namespace Game.Api.Models.Items
{
    public class Item : IModelFromSource<Item, ItemEntity>
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public EItemCategory ItemCategoryId { get; set; }
        public string IconPath { get; set; }
        public IEnumerable<BattlerAttribute> Attributes { get; set; }
        public static Item FromSource(ItemEntity item)
        {
            return new Item
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                ItemCategoryId = (EItemCategory)item.ItemCategoryId,
                IconPath = item.IconPath,
                Attributes = item.ItemAttributes.To().Model<BattlerAttribute>(),
            };
        }
    }
}
