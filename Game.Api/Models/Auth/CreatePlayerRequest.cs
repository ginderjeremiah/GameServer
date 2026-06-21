namespace Game.Api.Models.Auth
{
    /// <summary>
    /// Creates an additional character on the authenticated account. Carries the user-supplied name; the
    /// server validates it and enforces the per-account character cap (anti-cheat).
    /// </summary>
    public class CreatePlayerRequest : IModel
    {
        public required string Name { get; set; }
    }
}
