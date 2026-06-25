namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>The full set of starter equipment to associate with a single class (<see cref="ClassId"/>).</summary>
    public class SetClassStarterEquipmentData
    {
        public int ClassId { get; set; }

        public required List<ClassStarterEquipment> Equipment { get; set; }
    }
}
