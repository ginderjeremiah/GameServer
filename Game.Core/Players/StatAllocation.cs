namespace Game.Core.Players
{
    /// <summary>
    /// Represents a stat allocation for a particular <see cref="EAttribute"/>.
    /// </summary>
    public class StatAllocation
    {
        public required EAttribute Attribute { get; set; }

        public required double Amount { get; set; }
    }
}
