namespace GameCore.Sessions
{
    public interface IAttributeUpdate
    {
        public int AttributeId { get; set; }
        public int Amount { get; set; }
    }
}
