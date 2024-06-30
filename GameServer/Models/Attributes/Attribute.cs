using GameCore;

namespace GameServer.Models.Attributes
{
    public class Attribute : IModel
    {
        public EAttribute Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public Attribute() { }

        public Attribute(GameCore.Entities.Attribute attribute)
        {
            Id = (EAttribute)attribute.Id;
            Name = attribute.Name;
            Description = attribute.Description;
        }
    }
}
