using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for a <see cref="Class"/>'s starting equipped item, keyed by its slot.</summary>
    public class ClassStarterEquipment : IModel
    {
        public int ItemId { get; set; }
        public EEquipmentSlot EquipmentSlot { get; set; }
    }
}
