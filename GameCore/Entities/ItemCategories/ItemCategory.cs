using System.Data;

namespace GameCore.Entities.ItemCategories
{
    public class ItemCategory : IEntity
    {
        public int ItemCategoryId { get; set; }
        public string CategoryName { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            ItemCategoryId = record["ItemCategoryId"].AsInt();
            CategoryName = record["CategoryName"].AsString();
        }
    }
}
