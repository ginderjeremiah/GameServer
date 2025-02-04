namespace Game.Core.Attributes.Modifiers
{
    /// <summary>
    /// Represents an exception thrown when a modifier type is not supported.
    /// </summary>
    public class ModifierTypeNotSupportedException : Exception
    {
        /// <summary>
        /// The default constructor.
        /// </summary>
        /// <param name="type"></param>
        public ModifierTypeNotSupportedException(EModifierType type) : base($"The type ${type} is not supported by this class.") { }
    }
}
