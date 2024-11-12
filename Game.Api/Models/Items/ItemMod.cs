using Game.Api.Models.Attributes;
using ItemModEntity = Game.Core.Entities.ItemMod;

namespace Game.Api.Models.Items
{
    public class ItemMod : IModelFromSource<ItemMod, ItemModEntity>
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool Removable { get; set; }
        public string Description { get; set; }
        public int ItemModTypeId { get; set; }
        public IEnumerable<BattlerAttribute> Attributes { get; set; }

        public static ItemMod FromSource(ItemModEntity itemMod)
        {
            return new ItemMod
            {
                Id = itemMod.Id,
                Name = itemMod.Name,
                Removable = itemMod.Removable,
                Description = itemMod.Description,
                ItemModTypeId = itemMod.ItemModTypeId,
                Attributes = itemMod.ItemModAttributes.To().Model<BattlerAttribute>(),
            };
        }
    }
}
