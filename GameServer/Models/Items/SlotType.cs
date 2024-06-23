namespace GameServer.Models.Items
{
    public class SlotType : IModel
    {
        public int SlotTypeId { get; set; }
        public string SlotTypeName { get; set; }

        public SlotType() { }

        public SlotType(GameCore.Entities.SlotTypes.SlotType slotType)
        {
            SlotTypeId = slotType.SlotTypeId;
            SlotTypeName = slotType.SlotTypeName;
        }
    }
}
