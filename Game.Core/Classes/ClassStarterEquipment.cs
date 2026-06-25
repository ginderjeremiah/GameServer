namespace Game.Core.Classes
{
    /// <summary>A class's starting equipped item (item id + the slot it occupies). Structurally immutable so
    /// the shared cached <see cref="Class"/> graph can be reused by reference.</summary>
    public sealed class ClassStarterEquipment
    {
        public required int ItemId { get; init; }
        public required EEquipmentSlot EquipmentSlot { get; init; }
    }
}
