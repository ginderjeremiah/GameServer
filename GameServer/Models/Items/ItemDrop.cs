namespace GameServer.Models.Items
{
    public class ItemDrop : IModel
    {
        public int ItemId { get; set; }
        public decimal DropRate { get; set; }

        public ItemDrop(GameCore.Entities.EnemyDrop drop)
        {
            ItemId = drop.ItemId;
            DropRate = drop.DropRate;
        }

        public ItemDrop(GameCore.Entities.ZoneDrop drop)
        {
            ItemId = drop.ItemId;
            DropRate = drop.DropRate;
        }
    }
}
