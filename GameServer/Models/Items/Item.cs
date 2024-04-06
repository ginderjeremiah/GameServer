namespace GameServer.Models.Items
{
    public class Item : IModel
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string ItemDesc { get; set; }
        public int ItemCategoryId { get; set; }

        public Item(DataAccess.Models.Items.Item item)
        {
            ItemId = item.ItemId;
            ItemName = item.ItemName;
            ItemDesc = item.ItemDesc;
            ItemCategoryId = item.ItemCategoryId;
        }
    }
}
