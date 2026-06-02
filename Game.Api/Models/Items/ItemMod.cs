using Game.Api.Models.Attributes;
using Game.Core;
using ItemModEntity = Game.Abstractions.Entities.ItemMod;

namespace Game.Api.Models.Items
{
    public class ItemMod : IModelFromSource<ItemMod, ItemModEntity>
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public EItemModType ItemModTypeId { get; set; }
        public ERarity RarityId { get; set; }
        public required IEnumerable<BattlerAttribute> Attributes { get; set; }
        public required IEnumerable<int> Tags { get; set; }

        public static ItemMod FromSource(ItemModEntity itemMod)
        {
            return new ItemMod
            {
                Id = itemMod.Id,
                Name = itemMod.Name,
                Description = itemMod.Description,
                ItemModTypeId = (EItemModType)itemMod.ItemModTypeId,
                RarityId = (ERarity)itemMod.RarityId,
                Attributes = itemMod.ItemModAttributes.To().Model<BattlerAttribute>(),
                Tags = itemMod.Tags.Select(t => t.Id),
            };
        }
    }
}
