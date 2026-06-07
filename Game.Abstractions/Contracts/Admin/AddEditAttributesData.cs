namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// A change set against the attribute-valued collection of a single content record (an item's or
    /// item mod's attributes, or a skill's damage multipliers), keyed by the owner's <see cref="Id"/>.
    /// </summary>
    public class AddEditAttributesData
    {
        public int Id { get; set; }
        public required List<Change<BattlerAttribute>> Changes { get; set; }
    }
}
