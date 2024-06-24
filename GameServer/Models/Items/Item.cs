using GameServer.Models.Attributes;

namespace GameServer.Models.Items
{
    public class Item : IModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int ItemCategoryId { get; set; }
        public string IconPath { get; set; }
        public List<BattlerAttribute> Attributes { get; set; }

        public Item() { }

        public Item(GameCore.Entities.Item item)
        {
            Id = item.Id;
            Name = item.Name;
            Description = item.Description;
            ItemCategoryId = item.ItemCategoryId;
            IconPath = item.IconPath;
            Attributes = item.ItemAttributes.Select(itemAtt => new BattlerAttribute(itemAtt)).ToList();
        }
    }
}
