namespace Game.Abstractions.Auth
{
    /// <summary>
    /// Authentication policy constants shared across the layers that issue tokens. The access-token
    /// lifetime is consumed by the access-token implementation; the refresh-token lifetime is consumed
    /// by the application layer when issuing a token pair.
    /// </summary>
    public static class AuthConstants
    {
        public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromHours(48);
    }
}
