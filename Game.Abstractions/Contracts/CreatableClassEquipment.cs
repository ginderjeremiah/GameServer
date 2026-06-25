using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>A starter equipment piece of a creatable class, with its item name resolved server-side
    /// for the pre-selection create-character preview (see <see cref="CreatableClassSkill"/>).</summary>
    public class CreatableClassEquipment : IModel
    {
        public int ItemId { get; set; }
        public EEquipmentSlot EquipmentSlot { get; set; }
        public required string Name { get; set; }
    }
}
