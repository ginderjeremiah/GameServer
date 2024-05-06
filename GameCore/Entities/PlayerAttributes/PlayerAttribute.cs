using System.Data;

namespace GameCore.Entities.PlayerAttributes
{
    public class PlayerAttribute : IEntity
    {
        public int PlayerId { get; set; }
        public int AttributeId { get; set; }
        public decimal Amount { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            PlayerId = record["PlayerId"].AsInt();
            AttributeId = record["AttributeId"].AsInt();
            Amount = record["Amount"].AsDecimal();
        }
    }
}
