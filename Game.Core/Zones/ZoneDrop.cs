using Game.Core.Items;

namespace Game.Core.Zones
{
    /// <summary>
    /// Represents a drop that can be found in a zone.
    /// </summary>
    public class ZoneDrop
    {
        /// <summary>
        /// The item that can be dropped.
        /// </summary>
        public required Item Item { get; set; }

        /// <summary>
        /// The probability of the item being dropped.
        /// </summary>
        public required decimal Probability { get; set; }
    }
}
