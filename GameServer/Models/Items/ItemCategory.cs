namespace GameServer.Models.Items
{
    public class ItemCategory : IModel
    {
        public int ItemCategoryId { get; set; }
        public string CategoryName { get; set; }

        public ItemCategory(DataAccess.Models.ItemCategories.ItemCategory itemCategory)
        {
            ItemCategoryId = itemCategory.ItemCategoryId;
            CategoryName = itemCategory.CategoryName;
        }
    }
}
