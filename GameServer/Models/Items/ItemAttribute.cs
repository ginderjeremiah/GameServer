namespace GameServer.Models.Items
{
    public class ItemAttribute : IModel
    {
        public int ItemId { get; set; }
        public AttributeType AttributeId { get; set; }
        public decimal Amount { get; set; }

        public ItemAttribute() { }
        public ItemAttribute(DataAccess.Models.Items.ItemAttribute itemAttribute)
        {
            ItemId = itemAttribute.ItemId;
            AttributeId = (AttributeType)itemAttribute.AttributeId;
            Amount = itemAttribute.Amount;
        }
    }
}
