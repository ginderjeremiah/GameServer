namespace GameServer.Models.Items
{
    public class ItemCategory : IModel
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public ItemCategory() { }

        public ItemCategory(GameCore.Entities.ItemCategory itemCategory)
        {
            Id = itemCategory.Id;
            Name = itemCategory.Name;
        }
    }
}
