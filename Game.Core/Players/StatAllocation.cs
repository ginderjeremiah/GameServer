namespace Game.Core.Players
{
    /// <summary>
    /// A class used to represent a stat allocation for a particular <see cref="EAttribute"/>.
    /// </summary>
    public class StatAllocation
    {
        /// <summary>
        /// The enum value of the <see cref="Attribute"/> the allocation is for.
        /// </summary>
        public required EAttribute AttributeId { get; set; }

        /// <summary>
        /// The amount of stat points allocated to the attribute.
        /// </summary>
        public required double Amount { get; set; }
    }
}
