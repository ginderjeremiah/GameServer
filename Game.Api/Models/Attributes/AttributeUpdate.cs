using Game.Core.Sessions;

namespace Game.Api.Models.Attributes
{
    public class AttributeUpdate : IModel, IAttributeUpdate
    {
        public int AttributeId { get; set; }
        public int Amount { get; set; }
    }
}
