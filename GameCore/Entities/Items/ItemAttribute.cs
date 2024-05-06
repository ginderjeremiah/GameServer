using System.Data;

namespace GameCore.Entities.Items
{
    public class ItemAttribute : IEntity
    {
        public int ItemId { get; set; }
        public int AttributeId { get; set; }
        public decimal Amount { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            ItemId = record["ItemId"].AsInt();
            AttributeId = record["AttributeId"].AsInt();
            Amount = record["Amount"].AsDecimal();
        }
    }
}
