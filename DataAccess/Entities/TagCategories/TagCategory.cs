using GameLibrary;
using GameLibrary.Database.Interfaces;
using System.Data;

namespace DataAccess.Entities.TagCategories
{
    public class TagCategory : IEntity
    {
        public int TagCategoryId { get; set; }
        public string TagCategoryName { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            TagCategoryId = record["TagCategoryId"].AsInt();
            TagCategoryName = record["TagCategoryName"].AsString();
        }
    }
}
