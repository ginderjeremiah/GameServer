using Game.Core;
using AttributeEntity = Game.Core.Entities.Attribute;

namespace Game.Api.Models.Attributes
{
    public class Attribute : IMappedModel<Attribute, AttributeEntity>
    {
        public EAttribute Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public static Attribute FromSource(AttributeEntity entity)
        {
            return new Attribute()
            {
                Id = (EAttribute)entity.Id,
                Name = entity.Name,
                Description = entity.Description,
            };
        }

        public AttributeEntity ToSource()
        {
            return new AttributeEntity
            {
                Id = (int)Id,
                Name = Name,
                Description = Description,
            };
        }
    }
}
