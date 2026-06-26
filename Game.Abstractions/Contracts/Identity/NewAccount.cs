namespace Game.Abstractions.Contracts.Identity
{
    /// <summary>
    /// The credential material for a brand-new account, handed to the Identity / User Admin context to
    /// persist a new user. The orchestration layer hashes the password and supplies this material; the
    /// data tier maps it to the user entity. A new account has no characters — its first character is
    /// created later, on the select screen (issue #1256).
    /// </summary>
    /// <remarks>
    /// Like <see cref="AccountCredentials"/> this is an internal write contract of the Identity context.
    /// It deliberately does <b>not</b> implement <see cref="IModel"/>: it carries the password hash and
    /// must never cross the API boundary as a serialized model.
    /// </remarks>
    public class NewAccount
    {
        public required string Username { get; init; }
        public required string PassHash { get; init; }
    }
}
