using GameLibrary;
using GameLibrary.Database.Interfaces;
using System.Data;

namespace DataAccess.Entities.ItemCategories
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
