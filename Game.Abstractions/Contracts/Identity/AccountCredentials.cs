namespace Game.Abstractions.Contracts.Identity
{
    /// <summary>
    /// The data the login use case needs to authenticate an account: the stored credential material
    /// plus the identifiers resolved once the credentials check out (the user id, granted role names,
    /// and the ids of the user's players), along with whether the account is banned.
    /// </summary>
    /// <remarks>
    /// This is an internal read contract of the Identity / User Admin context. It deliberately does
    /// <b>not</b> implement <see cref="IModel"/>: it carries the password hash and must never cross the
    /// API boundary as a serialized model.
    /// </remarks>
    public class AccountCredentials
    {
        public int Id { get; set; }
        public required string PassHash { get; set; }
        public required IReadOnlyList<string> Roles { get; set; }
        public required IReadOnlyList<int> PlayerIds { get; set; }

        /// <summary>Whether the account is banned, in which case login is rejected once the password verifies.</summary>
        public required bool IsBanned { get; set; }
    }
}
