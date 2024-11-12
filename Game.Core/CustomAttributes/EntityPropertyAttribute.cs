namespace Game.Core.CustomAttributes
{
    /// <summary>
    /// An attribute used to annotate additional properties for an <see cref="Enum"/> that is converted to an entity.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class EntityPropertyAttribute(string key, object value) : Attribute
    {
        /// <summary>
        /// The name of the property.
        /// </summary>
        public string Key { get; set; } = key;

        /// <summary>
        /// The value of the property.
        /// </summary>
        public object Value { get; set; } = value;
    }
}
