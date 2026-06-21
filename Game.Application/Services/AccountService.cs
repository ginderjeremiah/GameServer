using Game.Abstractions.Auth;
using Game.Abstractions.Contracts.Identity;
using Game.Abstractions.DataAccess;
using Game.Application.Auth;
using Game.Core.Players;
using Microsoft.Extensions.Logging;

namespace Game.Application.Services
{
    /// <summary>
    /// Orchestrates account and authentication use cases: creating accounts (and their initial player
    /// graph), authenticating a login, and rotating/revoking refresh tokens. The API layer is a thin
    /// adapter over this service — it owns only request-scoped session wiring and HTTP mapping.
    /// </summary>
    public class AccountService(
        IUsers users,
        IPlayerRepository playerRepo,
        IRefreshTokenStore refreshTokenStore,
        IAccessTokenService accessTokenService,
        IPasswordHasher passwordHasher,
        LoginBackoffGuard backoffGuard,
        NewPlayerFactory newPlayerFactory,
        ILogger<AccountService> logger)
    {
        private readonly IUsers _users = users;
        private readonly IPlayerRepository _playerRepo = playerRepo;
        private readonly IRefreshTokenStore _refreshTokenStore = refreshTokenStore;
        private readonly IAccessTokenService _accessTokenService = accessTokenService;
        private readonly IPasswordHasher _passwordHasher = passwordHasher;
        private readonly LoginBackoffGuard _backoffGuard = backoffGuard;
        private readonly NewPlayerFactory _newPlayerFactory = newPlayerFactory;
        private readonly ILogger<AccountService> _logger = logger;

        /// <summary>
        /// Creates a new account: validates the username is available, hashes the password, and hands the
        /// credential material plus the new-player blueprint to the Identity context for persistence. The
        /// new-player defaults (starter skills, attributes, and log preferences) are a domain concern
        /// owned by <see cref="NewPlayerFactory"/>; this method only orchestrates — it builds no entity
        /// graphs. The up-front check is a fast path; the data tier's active-username uniqueness guard is
        /// the authority, so a username claimed concurrently (past the check) is still reported as taken.
        /// </summary>
        public async Task<CreateAccountStatus> CreateAccount(string username, string password)
        {
            if (await _users.CheckIfUsernameExists(username))
            {
                return CreateAccountStatus.UsernameTaken;
            }

            var account = new NewAccount
            {
                Username = username,
                PassHash = _passwordHasher.Hash(password),
            };

            var created = await _users.CreateAccount(account, _newPlayerFactory.Create(username));

            return created ? CreateAccountStatus.Success : CreateAccountStatus.UsernameTaken;
        }

        /// <summary>
        /// Authenticates a login: verifies the credentials and issues a fresh (pre-selection) access/refresh
        /// token pair plus the account's player summaries. It deliberately does <b>not</b> bind or load a
        /// player — the client picks a character on the subsequent <see cref="SelectPlayer"/> step, which
        /// rotates the tokens to carry the chosen player. Distinct failure reasons are reported via the
        /// result status so the caller can surface the appropriate message.
        /// </summary>
        public async Task<AccountLoginResult> Login(string username, string password)
        {
            // Defence-in-depth on top of the per-IP rate limiter: if too many consecutive failures have
            // accrued for this account, reject before any database hit or PBKDF2 work. Keyed per account so a
            // slow distributed guess is slowed regardless of source IP; it is a bounded backoff (never a hard
            // lockout), so an attacker who knows a username can only briefly slow the owner, not lock them out.
            var activeBackoff = await _backoffGuard.GetActiveBackoff(username);
            if (activeBackoff is TimeSpan retryAfter)
            {
                return AccountLoginResult.BackedOff(retryAfter);
            }

            var account = await _users.GetUser(username);
            if (account is null)
            {
                await _backoffGuard.RegisterFailure(username);
                return AccountLoginResult.Failed(LoginStatus.InvalidCredentials);
            }

            var verification = _passwordHasher.Verify(password, account.PassHash);
            if (verification == PasswordVerificationResult.Failed)
            {
                await _backoffGuard.RegisterFailure(username);
                return AccountLoginResult.Failed(LoginStatus.InvalidCredentials);
            }

            // Credentials verified — the attempt is not a brute-force guess, so reset the failure streak
            // before any later (ban / no-player) rejection, which is not a credential failure.
            await _backoffGuard.Reset(username);

            // Reject a banned account only after its credentials check out, so an anonymous probe can't
            // enumerate ban status — only the account owner learns they are banned. This is also the first
            // gate after verification, so a banned login never loads the player or re-hashes the credential.
            if (account.IsBanned)
            {
                return AccountLoginResult.Failed(LoginStatus.Banned);
            }

            // Transparently re-hash a credential stored with an outdated work factor now that we have
            // verified the plaintext, so existing accounts upgrade without a forced reset. This is
            // best-effort: an opportunistic upgrade must never cost the user an otherwise-valid login — if
            // the write fails transiently the old hash still verifies, so the next login simply retries it.
            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            {
                try
                {
                    await _users.UpdatePasswordHash(account.Id, _passwordHasher.Hash(password));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to migrate password hash for user {UserId}; will retry on next login.", account.Id);
                }
            }

            // List the account's characters for the select step; the pre-selection token carries no player.
            var summaries = await _users.GetPlayerSummaries(account.Id);
            var tokens = await IssueTokens(account.Id, account.Roles, playerId: null);

            return AccountLoginResult.Succeeded(tokens, summaries, account.Id);
        }

        /// <summary>
        /// Selects which of the account's characters to enter as: validates the player belongs to the
        /// authenticated account (anti-cheat), loads its aggregate, and rotates the token pair to carry the
        /// chosen player id as the selected-player anchor. Ownership and the player load are checked before
        /// the refresh token is consumed, so a rejected selection leaves the caller's token intact. Distinct
        /// failure reasons are reported via the result status so the caller can surface the right message.
        /// </summary>
        public async Task<AccountSelectPlayerResult> SelectPlayer(int userId, int playerId, string refreshToken)
        {
            var playerIds = await _users.GetPlayerIds(userId);
            if (!playerIds.Contains(playerId))
            {
                return AccountSelectPlayerResult.Failed(SelectPlayerStatus.NotOwned);
            }

            var player = await _playerRepo.GetPlayer(playerId);
            if (player is null)
            {
                return AccountSelectPlayerResult.Failed(SelectPlayerStatus.PlayerDataNotFound);
            }

            // Consume the caller's (pre-selection) refresh token and re-issue a rotated pair carrying the
            // chosen player. Consuming only after the validations pass upholds single-use rotation without
            // burning the token on a rejected selection. The roles come from the consumed token (the "roles
            // are fixed for the session" model), and the user it resolves to must match the authenticated
            // caller.
            var session = await _refreshTokenStore.Consume(refreshToken);
            if (session is null || session.UserId != userId)
            {
                return AccountSelectPlayerResult.Failed(SelectPlayerStatus.InvalidToken);
            }

            var tokens = await IssueTokens(userId, session.Roles, playerId);
            return AccountSelectPlayerResult.Succeeded(tokens, player);
        }

        /// <summary>
        /// Validates and rotates a refresh token: consuming it (single use) and, when valid, issuing a
        /// brand-new token pair carrying the same user, roles, and selected player. Returns
        /// <see langword="null"/> when the supplied token is missing, expired, or already consumed.
        /// </summary>
        public async Task<AuthTokenPair?> Refresh(string refreshToken)
        {
            var session = await _refreshTokenStore.Consume(refreshToken);
            if (session is null)
            {
                return null;
            }

            return await IssueTokens(session.UserId, session.Roles, session.PlayerId);
        }

        /// <summary>
        /// Revokes a refresh token without issuing a replacement (logout) by consuming it (single use).
        /// Returns the user the token resolved to so the caller can evict that user's session deterministically
        /// even when the access token has already expired, or <see langword="null"/> when the token was
        /// missing, expired, or already consumed.
        /// </summary>
        public async Task<int?> Logout(string refreshToken)
        {
            var session = await _refreshTokenStore.Consume(refreshToken);
            return session?.UserId;
        }

        /// <summary>
        /// Issues a fresh access/refresh token pair for the given user, both carrying the selected player
        /// id (<see langword="null"/> before selection). The refresh token is rotated on every use (login,
        /// select, and refresh all call this), so a previously issued refresh token is never reused.
        /// </summary>
        private async Task<AuthTokenPair> IssueTokens(int userId, IReadOnlyList<string> roles, int? playerId)
        {
            var accessToken = _accessTokenService.CreateAccessToken(userId, roles, playerId);
            var refreshToken = await _refreshTokenStore.Issue(userId, roles, playerId, AuthConstants.RefreshTokenLifetime);
            return new AuthTokenPair(accessToken, refreshToken);
        }
    }
}
