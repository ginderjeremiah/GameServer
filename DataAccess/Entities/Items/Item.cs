using GameLibrary;
using GameLibrary.Database.Interfaces;
using System.Data;
using System.Text.Json;

namespace DataAccess.Entities.Items
{
    public class Item : IEntity
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string ItemDesc { get; set; }
        public int ItemCategoryId { get; set; }
        public string IconPath { get; set; }
        public List<ItemAttribute> Attributes { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            ItemId = record["ItemId"].AsInt();
            ItemName = record["ItemName"].AsString();
            ItemDesc = record["ItemDesc"].AsString();
            ItemCategoryId = record["ItemCategoryId"].AsInt();
            IconPath = record["IconPath"].AsString();
            Attributes = JsonSerializer.Deserialize<List<ItemAttribute>>(record["AttributesJSON"].AsString()) ?? new List<ItemAttribute>();
        }
    }
}
