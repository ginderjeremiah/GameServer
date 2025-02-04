namespace Game.Abstractions.Entities
{
    /// <summary>
    /// An entity that has a primary key integer "Id" that starts with 0 (as opposed to 1).
    /// </summary>
    public interface IZeroBasedIdentityEntity
    {
        /// <summary>
        /// The primary key.
        /// </summary>
        public int Id { get; set; }
    }
}
