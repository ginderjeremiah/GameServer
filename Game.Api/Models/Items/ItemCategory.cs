using ItemCategoryEntity = Game.Core.Entities.ItemCategory;

namespace Game.Api.Models.Items
{
    public class ItemCategory : IModelFromSource<ItemCategory, ItemCategoryEntity>
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public ItemCategory(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public static ItemCategory FromSource(ItemCategoryEntity itemCategory)
        {
            return new ItemCategory(itemCategory.Id, itemCategory.Name);
        }
    }
}
