using Game.Core;
using Game.Core.Players;

namespace Game.Api.Models.Attributes
{
    public class AttributeUpdate : IModel, IAttributeUpdate
    {
        public int AttributeId { get; set; }
        public int Amount { get; set; }

        EAttribute IAttributeUpdate.Attribute => (EAttribute)AttributeId;
    }
}
