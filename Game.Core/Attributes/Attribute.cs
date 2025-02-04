using static Game.Core.EAttribute;

namespace Game.Core.Attributes
{
    /// <inheritdoc cref="EAttribute"/>
    public class Attribute
    {
        /// <summary>
        /// The enum value of the attribute.
        /// </summary>
        public EAttribute Id { get; set; }

        /// <summary>
        /// The name of the attribute.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// A text description of what the attribute represents.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Creates a new attribute based on the given enum value.
        /// </summary>
        /// <param name="id"></param>
        public Attribute(EAttribute id)
        {
            Id = id;
            Name = id.ToString().Capitalize().SpaceWords();
            Description = GetDescription(id);
        }

        private static string GetDescription(EAttribute value)
        {
            return value switch
            {
                Strength => "A measure of one's raw physical force.",
                _ => "A measure of one's raw physical force."
            };
        }
    }
}
