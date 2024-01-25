namespace DataAccess.Models.Items
{
    public class Item
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string ItemDesc { get; set; }
        public int ItemCategoryId { get; set; }
    }
}
