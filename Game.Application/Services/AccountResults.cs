using Game.Core.Players;
using System.Diagnostics.CodeAnalysis;

namespace Game.Application.Services
{
    /// <summary>
    /// Outcome of an account-creation attempt. User-facing messaging is the caller's concern.
    /// </summary>
    public enum CreateAccountStatus
    {
        Success,
        UsernameTaken,
    }

    /// <summary>
    /// Outcome classification of a login attempt, so the caller can surface the appropriate message.
    /// </summary>
    public enum LoginStatus
    {
        Success,
        InvalidCredentials,
        Banned,
        NoPlayer,
        PlayerDataNotFound,
    }

    /// <summary>
    /// A signed access token paired with the opaque refresh token used to mint the next pair.
    /// </summary>
    public record AuthTokenPair(string AccessToken, string RefreshToken);

    /// <summary>
    /// Result of <see cref="AccountService.Login"/>: a status plus, on success, the issued tokens, the
    /// loaded player aggregate, and the authenticated user id (needed to establish the session).
    /// </summary>
    public record AccountLoginResult(LoginStatus Status, AuthTokenPair? Tokens, Player? Player, int UserId)
    {
        [MemberNotNullWhen(true, nameof(Tokens), nameof(Player))]
        public bool Success => Status == LoginStatus.Success;

        public static AccountLoginResult Failed(LoginStatus status)
        {
            return new AccountLoginResult(status, null, null, 0);
        }

        public static AccountLoginResult Succeeded(AuthTokenPair tokens, Player player, int userId)
        {
            return new AccountLoginResult(LoginStatus.Success, tokens, player, userId);
        }
    }
}
