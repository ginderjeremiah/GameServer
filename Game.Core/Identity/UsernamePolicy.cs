namespace Game.Core.Identity
{
    /// <summary>
    /// Length bounds for a user's login username. <see cref="MaxLength"/> mirrors the persisted
    /// <c>User.Username</c> column (see <see cref="Game.Core.Players.PlayerName"/> for the equivalent
    /// player-name rule) so a valid username always fits storage. Named <c>UsernamePolicy</c> rather than
    /// <c>Username</c> so it doesn't collide with the <c>Username</c> property on the request models that
    /// reference it.
    /// </summary>
    public static class UsernamePolicy
    {
        /// <summary>The shortest a username may be.</summary>
        public const int MinLength = 1;

        /// <summary>The longest a username may be — mirrors the persisted <c>User.Username</c> column.</summary>
        public const int MaxLength = 20;
    }
}
