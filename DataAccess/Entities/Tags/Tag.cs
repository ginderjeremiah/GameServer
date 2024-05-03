using GameLibrary;
using GameLibrary.Database.Interfaces;
using System.Data;

namespace DataAccess.Entities.Tags
{
    public class Tag : IEntity
    {
        public int TagId { get; set; }
        public string TagName { get; set; }
        public int TagCategoryId { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            TagId = record["TagId"].AsInt();
            TagName = record["TagName"].AsString();
            TagCategoryId = record["TagCategoryId"].AsInt();
        }
    }
}
