using GameCore.BattleSimulation;

namespace GameServer.Models.Attributes
{
    public class Attribute : IModel
    {
        public AttributeType Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public Attribute() { }

        public Attribute(GameCore.Entities.Attribute attribute)
        {
            Id = (AttributeType)attribute.Id;
            Name = attribute.Name;
            Description = attribute.Description;
        }
    }
}
