namespace GameServer.Models.Attributes
{
    public class Attribute : IModel
    {
        public AttributeType AttributeId { get; set; }
        public string AttributeName { get; set; }
        public string AttributeDesc { get; set; }

        public Attribute(DataAccess.Entities.Attributes.Attribute attribute)
        {
            AttributeId = (AttributeType)attribute.AttributeId;
            AttributeName = attribute.AttributeName;
            AttributeDesc = attribute.AttributeDesc;
        }
    }
}
