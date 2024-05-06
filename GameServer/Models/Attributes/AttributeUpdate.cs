using GameCore.Sessions;

namespace GameServer.Models.Attributes
{
    public class AttributeUpdate : IModel, IAttributeUpdate
    {
        public int AttributeId { get; set; }
        public int Amount { get; set; }
    }
}
