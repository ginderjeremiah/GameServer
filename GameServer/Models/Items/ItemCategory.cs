namespace GameServer.Models.Items
{
    public class ItemCategory : IModel
    {
        public int ItemCategoryId { get; set; }
        public string CategoryName { get; set; }

        public ItemCategory(GameCore.Entities.ItemCategories.ItemCategory itemCategory)
        {
            ItemCategoryId = itemCategory.ItemCategoryId;
            CategoryName = itemCategory.CategoryName;
        }
    }
}
