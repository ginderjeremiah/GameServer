using Game.Abstractions.Contracts.Identity;
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
    /// Login only verifies credentials and lists the account's characters; loading and binding a player
    /// happens on the subsequent <see cref="AccountService.SelectPlayer"/> step.
    /// </summary>
    public enum LoginStatus
    {
        Success,
        InvalidCredentials,
        Banned,
        /// <summary>
        /// The account is in an exponential-backoff window after too many consecutive failed attempts; the
        /// attempt was rejected without verifying credentials. <see cref="AccountLoginResult.RetryAfter"/>
        /// carries how long to wait.
        /// </summary>
        TooManyAttempts,
    }

    /// <summary>
    /// Outcome classification of a player-selection attempt, so the caller can surface the appropriate
    /// message.
    /// </summary>
    public enum SelectPlayerStatus
    {
        Success,
        /// <summary>The supplied refresh token was missing, expired, already consumed, or not the caller's.</summary>
        InvalidToken,
        /// <summary>The selected player does not belong to the authenticated account (anti-cheat).</summary>
        NotOwned,
        /// <summary>The player belongs to the account but its aggregate could not be loaded.</summary>
        PlayerDataNotFound,
    }

    /// <summary>
    /// Outcome classification of a character-creation attempt, so the caller can surface the appropriate
    /// message. <see cref="InvalidName"/> is decided in the application layer (name validation); the rest
    /// map from the data tier's cap/ownership enforcement.
    /// </summary>
    public enum CreatePlayerStatus
    {
        Success,

        /// <summary>The supplied name failed validation (blank, too long, or control characters).</summary>
        InvalidName,

        /// <summary>The chosen class id does not resolve to a live (non-retired) class.</summary>
        InvalidClass,

        /// <summary>The account already holds the maximum number of characters.</summary>
        CapReached,

        /// <summary>No active account matched the authenticated caller.</summary>
        UserNotFound,
    }

    /// <summary>
    /// A signed access token paired with the opaque refresh token used to mint the next pair.
    /// </summary>
    public record AuthTokenPair(string AccessToken, string RefreshToken);

    /// <summary>
    /// Result of <see cref="AccountService.Login"/>: a status plus, on success, the issued (pre-selection)
    /// tokens, the account's player summaries (for the character-select step), and the authenticated user
    /// id. On a <see cref="LoginStatus.TooManyAttempts"/> rejection it carries the remaining backoff wait.
    /// </summary>
    public record AccountLoginResult(
        LoginStatus Status,
        AuthTokenPair? Tokens,
        IReadOnlyList<PlayerSummary>? PlayerSummaries,
        int UserId,
        TimeSpan? RetryAfter = null)
    {
        [MemberNotNullWhen(true, nameof(Tokens), nameof(PlayerSummaries))]
        public bool Success => Status == LoginStatus.Success;

        public static AccountLoginResult Failed(LoginStatus status)
        {
            return new AccountLoginResult(status, null, null, 0);
        }

        public static AccountLoginResult BackedOff(TimeSpan retryAfter)
        {
            return new AccountLoginResult(LoginStatus.TooManyAttempts, null, null, 0, retryAfter);
        }

        public static AccountLoginResult Succeeded(AuthTokenPair tokens, IReadOnlyList<PlayerSummary> playerSummaries, int userId)
        {
            return new AccountLoginResult(LoginStatus.Success, tokens, playerSummaries, userId);
        }
    }

    /// <summary>
    /// Result of <see cref="AccountService.SelectPlayer"/>: a status plus, on success, the rotated token
    /// pair (now carrying the selected player id) and the loaded player aggregate to enter the game with.
    /// </summary>
    public record AccountSelectPlayerResult(SelectPlayerStatus Status, AuthTokenPair? Tokens, Player? Player)
    {
        [MemberNotNullWhen(true, nameof(Tokens), nameof(Player))]
        public bool Success => Status == SelectPlayerStatus.Success;

        public static AccountSelectPlayerResult Failed(SelectPlayerStatus status)
        {
            return new AccountSelectPlayerResult(status, null, null);
        }

        public static AccountSelectPlayerResult Succeeded(AuthTokenPair tokens, Player player)
        {
            return new AccountSelectPlayerResult(SelectPlayerStatus.Success, tokens, player);
        }
    }

    /// <summary>
    /// Result of <see cref="AccountService.CreatePlayer"/>: a status plus, on success, the summary of the
    /// newly created character so the caller can present it (e.g. add it to the select list).
    /// </summary>
    public record AccountCreatePlayerResult(CreatePlayerStatus Status, PlayerSummary? Player)
    {
        [MemberNotNullWhen(true, nameof(Player))]
        public bool Success => Status == CreatePlayerStatus.Success;

        public static AccountCreatePlayerResult Succeeded(PlayerSummary player)
        {
            return new AccountCreatePlayerResult(CreatePlayerStatus.Success, player);
        }

        public static AccountCreatePlayerResult Failed(CreatePlayerStatus status)
        {
            return new AccountCreatePlayerResult(status, null);
        }
    }
}
