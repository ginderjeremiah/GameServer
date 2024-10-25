using GameCore;
using GameServer.Models.Attributes;

namespace GameServer.Models.Items
{
    public class Item : IModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public EItemCategory ItemCategoryId { get; set; }
        public string IconPath { get; set; }
        public IEnumerable<BattlerAttribute> Attributes { get; set; }
    }

    internal static partial class ModelExtensions
    {
        public static Item ToModel(this GameCore.Entities.Item item)
        {
            return new Item
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                ItemCategoryId = (EItemCategory)item.ItemCategoryId,
                IconPath = item.IconPath,
                Attributes = item.ItemAttributes.Select(a => a.ToModel()),
            };
        }
    }
}
