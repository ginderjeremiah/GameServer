using Game.Abstractions.Auth;
using Game.Abstractions.Contracts.Identity;
using Game.Abstractions.DataAccess;
using Game.Core.Players;

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
        NewPlayerFactory newPlayerFactory)
    {
        private readonly IUsers _users = users;
        private readonly IPlayerRepository _playerRepo = playerRepo;
        private readonly IRefreshTokenStore _refreshTokenStore = refreshTokenStore;
        private readonly IAccessTokenService _accessTokenService = accessTokenService;
        private readonly IPasswordHasher _passwordHasher = passwordHasher;
        private readonly NewPlayerFactory _newPlayerFactory = newPlayerFactory;

        /// <summary>
        /// Creates a new account: validates the username is available, hashes the password, and hands the
        /// credential material plus the new-player blueprint to the Identity context for persistence. The
        /// new-player defaults (starter skills, attributes, and log preferences) are a domain concern
        /// owned by <see cref="NewPlayerFactory"/>; this method only orchestrates — it builds no entity
        /// graphs. The inserts are persisted by the surrounding unit of work.
        /// </summary>
        public async Task<CreateAccountStatus> CreateAccount(string username, string password)
        {
            if (await _users.CheckIfUsernameExists(username))
            {
                return CreateAccountStatus.UsernameTaken;
            }

            var salt = Guid.NewGuid();
            var account = new NewAccount
            {
                Username = username,
                PassHash = _passwordHasher.Hash(password, salt),
                Salt = salt,
            };

            _users.CreateAccount(account, _newPlayerFactory.Create(username));

            return CreateAccountStatus.Success;
        }

        /// <summary>
        /// Authenticates a login: verifies the credentials, loads the player aggregate, and issues a
        /// fresh access/refresh token pair. Distinct failure reasons are reported via the result status
        /// so the caller can surface the appropriate message.
        /// </summary>
        public async Task<AccountLoginResult> Login(string username, string password)
        {
            var account = await _users.GetUser(username);
            if (account is null)
            {
                return AccountLoginResult.Failed(LoginStatus.InvalidCredentials);
            }

            var verification = _passwordHasher.Verify(password, account.Salt, account.PassHash);
            if (verification == PasswordVerificationResult.Failed)
            {
                return AccountLoginResult.Failed(LoginStatus.InvalidCredentials);
            }

            if (account.PlayerIds.Count == 0)
            {
                return AccountLoginResult.Failed(LoginStatus.NoPlayer);
            }

            var player = await _playerRepo.GetPlayer(account.PlayerIds[0]);
            if (player is null)
            {
                return AccountLoginResult.Failed(LoginStatus.PlayerDataNotFound);
            }

            // Transparently migrate a credential stored under the legacy scheme (or an outdated work
            // factor) now that we have verified the plaintext, so existing accounts upgrade without a
            // forced reset. The salt is reused; only the derived hash changes.
            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            {
                await _users.UpdatePasswordHash(account.Id, _passwordHasher.Hash(password, account.Salt));
            }

            var tokens = await IssueTokens(account.Id, account.Roles);

            return AccountLoginResult.Succeeded(tokens, player, account.Id);
        }

        /// <summary>
        /// Validates and rotates a refresh token: consuming it (single use) and, when valid, issuing a
        /// brand-new token pair carrying the same user and roles. Returns <see langword="null"/> when the
        /// supplied token is missing, expired, or already consumed.
        /// </summary>
        public async Task<AuthTokenPair?> Refresh(string refreshToken)
        {
            var session = await _refreshTokenStore.Consume(refreshToken);
            if (session is null)
            {
                return null;
            }

            return await IssueTokens(session.UserId, session.Roles);
        }

        /// <summary>
        /// Revokes a refresh token without issuing a replacement (logout). No-op if the token is unknown.
        /// </summary>
        public async Task Logout(string refreshToken)
        {
            await _refreshTokenStore.Revoke(refreshToken);
        }

        /// <summary>
        /// Issues a fresh access/refresh token pair for the given user. The refresh token is rotated on
        /// every use (both login and refresh call this), so a previously issued refresh token is never
        /// reused.
        /// </summary>
        private async Task<AuthTokenPair> IssueTokens(int userId, IReadOnlyList<string> roles)
        {
            var accessToken = _accessTokenService.CreateAccessToken(userId, roles);
            var refreshToken = await _refreshTokenStore.Issue(userId, roles, AuthConstants.RefreshTokenLifetime);
            return new AuthTokenPair(accessToken, refreshToken);
        }
    }
}
