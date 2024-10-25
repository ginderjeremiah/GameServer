namespace GameServer.Models.Items
{
    public class ItemModSlotType : IModel
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public ItemModSlotType() { }

        public ItemModSlotType(GameCore.Entities.ItemModSlotType slotType)
        {
            Id = slotType.Id;
            Name = slotType.Name;
        }
    }
}
