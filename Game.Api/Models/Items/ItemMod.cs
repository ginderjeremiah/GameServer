using Game.Api.Models.Attributes;
using ItemModEntity = Game.Abstractions.Entities.ItemMod;

namespace Game.Api.Models.Items
{
    public class ItemMod : IModelFromSource<ItemMod, ItemModEntity>
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public int ItemModTypeId { get; set; }
        public required IEnumerable<BattlerAttribute> Attributes { get; set; }

        public static ItemMod FromSource(ItemModEntity itemMod)
        {
            return new ItemMod
            {
                Id = itemMod.Id,
                Name = itemMod.Name,
                Description = itemMod.Description,
                ItemModTypeId = itemMod.ItemModTypeId,
                Attributes = itemMod.ItemModAttributes.To().Model<BattlerAttribute>(),
            };
        }
    }
}
