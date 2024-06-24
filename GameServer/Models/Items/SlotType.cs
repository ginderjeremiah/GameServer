namespace GameServer.Models.Items
{
    public class SlotType : IModel
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public SlotType() { }

        public SlotType(GameCore.Entities.SlotType slotType)
        {
            Id = slotType.Id;
            Name = slotType.Name;
        }
    }
}
