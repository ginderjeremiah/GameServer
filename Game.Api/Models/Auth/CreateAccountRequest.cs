using System.ComponentModel.DataAnnotations;

namespace Game.Api.Models.Auth
{
    /// <summary>
    /// Creates a new account and its first character. Carries the credentials plus the chosen class; the
    /// server validates the class and enforces username uniqueness (anti-cheat). Kept distinct from the
    /// login credentials so the class only rides the creation request, not every login.
    /// </summary>
    public class CreateAccountRequest : IModel
    {
        [Length(1, 25)]
        public required string Username { get; set; }

        [Length(1, 256)]
        public required string Password { get; set; }

        /// <summary>The id of the class the first character is created as (the archetype that seeds its kit).</summary>
        public required int ClassId { get; set; }
    }
}
