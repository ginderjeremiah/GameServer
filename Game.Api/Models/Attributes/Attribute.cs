using Game.Core;
using CoreAttribute = Game.Core.Attributes.Attribute;

namespace Game.Api.Models.Attributes
{
    public class Attribute : IModelFromSource<Attribute, CoreAttribute>
    {
        public EAttribute Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public EAttributeType AttributeType { get; set; }
        public bool IsPercentage { get; set; }
        public bool IsHarmful { get; set; }
        public required string Code { get; set; }
        public int DisplayOrder { get; set; }
        public int Decimals { get; set; }

        public static Attribute FromSource(CoreAttribute attribute)
        {
            return new Attribute
            {
                Id = attribute.Id,
                Name = attribute.Name,
                Description = attribute.Description,
                AttributeType = attribute.AttributeType,
                IsPercentage = attribute.IsPercentage,
                IsHarmful = attribute.IsHarmful,
                Code = attribute.Code,
                DisplayOrder = attribute.DisplayOrder,
                Decimals = attribute.Decimals,
            };
        }
    }
}
