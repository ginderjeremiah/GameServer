namespace GameServer.Models.Attributes
{
    public class AttributeUpdate : IModel
    {
        public int AttributeId { get; set; }
        public int Amount { get; set; }
    }
}
