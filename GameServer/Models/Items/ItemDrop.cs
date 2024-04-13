﻿namespace GameServer.Models.Items
{
    public class ItemDrop : IModel
    {
        public int ItemId { get; set; }
        public decimal DropRate { get; set; }

        public ItemDrop(DataAccess.Entities.Drops.ItemDrop drop)
        {
            ItemId = drop.ItemId;
            DropRate = drop.DropRate;
        }
    }
}
