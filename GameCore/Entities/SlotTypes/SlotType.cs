using System.Data;

namespace GameCore.Entities.SlotTypes
{
    public class SlotType : IEntity
    {
        public int SlotTypeId { get; set; }
        public string SlotTypeName { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            SlotTypeId = record["SlotTypeId"].AsInt();
            SlotTypeName = record["SlotTypeName"].AsString();
        }
    }
}
