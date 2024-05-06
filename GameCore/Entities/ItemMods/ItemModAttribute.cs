using System.Data;

namespace GameCore.Entities.ItemMods
{
    public class ItemModAttribute : IEntity
    {
        public int ItemModId { get; set; }
        public int AttributeId { get; set; }
        public decimal Amount { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            ItemModId = record["ItemModId"].AsInt();
            AttributeId = record["AttributeId"].AsInt();
            Amount = record["Amount"].AsDecimal();
        }
    }
}
