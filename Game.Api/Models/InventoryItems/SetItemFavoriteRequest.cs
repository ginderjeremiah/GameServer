namespace Game.Api.Models.InventoryItems
{
    public class SetItemFavoriteRequest
    {
        public int ItemId { get; set; }
        public bool Favorite { get; set; }
    }
}
