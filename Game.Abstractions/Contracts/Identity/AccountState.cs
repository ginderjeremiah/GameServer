namespace Game.Abstractions.Contracts.Identity
{
    /// <summary>
    /// The live ban/role state of an account, keyed by user id rather than username (unlike
    /// <see cref="AccountCredentials"/>, which authenticates a login). Used to re-check an account between
    /// logins — on refresh and player selection — so a ban or role change lands there instead of only at
    /// the account's next login.
    /// </summary>
    public class AccountState
    {
        public required IReadOnlyList<string> Roles { get; set; }

        /// <summary>Whether the account is banned, in which case its refresh chain is rejected outright.</summary>
        public required bool IsBanned { get; set; }
    }
}
