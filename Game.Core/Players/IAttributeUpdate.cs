namespace Game.Core.Players
{
    /// <summary>
    /// Represents an update to a player attribute.
    /// </summary>
    public interface IAttributeUpdate
    {
        /// <summary>
        /// The ID of the attribute to update.
        /// </summary>
        EAttribute Attribute { get; }

        /// <summary>
        /// The amount to update the attribute to.
        /// </summary>
        int Amount { get; }
    }
}
