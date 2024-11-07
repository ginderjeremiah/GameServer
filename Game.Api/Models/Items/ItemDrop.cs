using Game.Core.Entities;

namespace Game.Api.Models.Items
{
    public class ItemDrop : IModelFromSource<ItemDrop, EnemyDrop>, IModelFromSource<ItemDrop, ZoneDrop>
    {
        public int ItemId { get; set; }
        public decimal DropRate { get; set; }

        public static ItemDrop FromSource(EnemyDrop entity)
        {
            return new ItemDrop
            {
                ItemId = entity.ItemId,
                DropRate = entity.DropRate,
            };
        }

        public static ItemDrop FromSource(ZoneDrop entity)
        {
            return new ItemDrop
            {
                ItemId = entity.ItemId,
                DropRate = entity.DropRate,
            };
        }
    }
}
