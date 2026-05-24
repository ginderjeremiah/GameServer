using Game.Core;
using AttributeEntity = Game.Abstractions.Entities.Attribute;

namespace Game.Api.Models.Attributes
{
    public class Attribute : IModelFromSource<Attribute, AttributeEntity>
    {
        public EAttribute Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public static Attribute FromSource(AttributeEntity entity)
        {
            return new Attribute
            {
                Id = (EAttribute)entity.Id,
                Name = entity.Name,
                Description = entity.Description,
            };
        }
    }
}
