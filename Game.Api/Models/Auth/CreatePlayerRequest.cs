namespace Game.Api.Models.Auth
{
    /// <summary>
    /// Creates an additional character on the authenticated account. Carries the user-supplied name and the
    /// chosen class; the server validates both and enforces the per-account character cap (anti-cheat).
    /// </summary>
    public class CreatePlayerRequest : IModel
    {
        public required string Name { get; set; }

        /// <summary>The id of the class the character is created as (the archetype that seeds its kit).</summary>
        public required int ClassId { get; set; }
    }
}
