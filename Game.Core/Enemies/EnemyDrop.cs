using Game.Core.Items;

namespace Game.Core.Enemies
{
    /// <summary>
    /// Represents an item that an enemy can drop when defeated.
    /// </summary>
    public class EnemyDrop
    {
        /// <summary>
        /// The item that can be dropped.
        /// </summary>
        public required Item Item { get; set; }

        /// <summary>
        /// The drop rate of the item.
        /// </summary>
        public required decimal DropRate { get; set; }
    }
}
