using Game.Core;
using CoreAttribute = Game.Core.Attributes.Attribute;

namespace Game.Api.Models.Attributes
{
    public class Attribute : IModelFromSource<Attribute, CoreAttribute>
    {
        public EAttribute Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }

        public static Attribute FromSource(CoreAttribute attribute)
        {
            return new Attribute
            {
                Id = attribute.Id,
                Name = attribute.Name,
                Description = attribute.Description,
            };
        }
    }
}
