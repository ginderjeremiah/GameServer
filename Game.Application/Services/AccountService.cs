using Game.Abstractions.Auth;
using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Core.Players;
using PlayerAttributeEntity = Game.Abstractions.Entities.PlayerAttribute;
using PlayerEntity = Game.Abstractions.Entities.Player;
using PlayerSkillEntity = Game.Abstractions.Entities.PlayerSkill;
using UserEntity = Game.Abstractions.Entities.User;

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
        IEntityStore entityStore,
        IRefreshTokenStore refreshTokenStore,
        IAccessTokenService accessTokenService)
    {
        private const int StarterSkillCount = 3;
        private const int AttributeCount = 6;
        private const decimal StartingAttributeAmount = 5m;
        private const int StartingZoneId = 0;

        private readonly IUsers _users = users;
        private readonly IPlayerRepository _playerRepo = playerRepo;
        private readonly IEntityStore _entityStore = entityStore;
        private readonly IRefreshTokenStore _refreshTokenStore = refreshTokenStore;
        private readonly IAccessTokenService _accessTokenService = accessTokenService;

        /// <summary>
        /// Creates a new account: validates the username is available, hashes the password, and inserts
        /// the user together with its initial player graph (starter skills, attributes, and log
        /// preferences). The inserts are persisted by the surrounding unit of work.
        /// </summary>
        public async Task<CreateAccountStatus> CreateAccount(string username, string password)
        {
            if (await _users.CheckIfUsernameExists(username))
            {
                return CreateAccountStatus.UsernameTaken;
            }

            var salt = Guid.NewGuid();
            var user = new UserEntity
            {
                Username = username,
                PassHash = password.Hash(salt.ToString()),
                Salt = salt,
                LastLogin = DateTime.UtcNow,
            };

            _entityStore.Insert(user);

            var playerEntity = new PlayerEntity
            {
                User = user,
                Name = username,
                Level = 1,
                Exp = 0,
                CurrentZoneId = StartingZoneId,
                StatPointsGained = 0,
                StatPointsUsed = 0,
            };

            playerEntity.PlayerSkills = Enumerable.Range(0, StarterSkillCount).Select(id => new PlayerSkillEntity
            {
                Player = playerEntity,
                Selected = true,
                SkillId = id,
            }).ToList();

            playerEntity.PlayerAttributes = Enumerable.Range(0, AttributeCount).Select(id => new PlayerAttributeEntity
            {
                Player = playerEntity,
                AttributeId = id,
                Amount = StartingAttributeAmount,
            }).ToList();

            playerEntity.LogPreferences =
            [
                new() { Player = playerEntity, LogTypeId = (int)ELogType.Damage, Enabled = false, },
                new() { Player = playerEntity, LogTypeId = (int)ELogType.Debug, Enabled = false, },
                new() { Player = playerEntity, LogTypeId = (int)ELogType.Exp, Enabled = true, },
                new() { Player = playerEntity, LogTypeId = (int)ELogType.LevelUp, Enabled = true, },
                new() { Player = playerEntity, LogTypeId = (int)ELogType.ItemFound, Enabled = true, },
                new() { Player = playerEntity, LogTypeId = (int)ELogType.EnemyDefeated, Enabled = true, },
            ];

            _entityStore.Insert(playerEntity);

            return CreateAccountStatus.Success;
        }

        /// <summary>
        /// Authenticates a login: verifies the credentials, loads the player aggregate, and issues a
        /// fresh access/refresh token pair. Distinct failure reasons are reported via the result status
        /// so the caller can surface the appropriate message.
        /// </summary>
        public async Task<AccountLoginResult> Login(string username, string password)
        {
            var user = await _users.GetUser(username);
            if (user is null || !password.VerifyHash(user.Salt.ToString(), user.PassHash))
            {
                return AccountLoginResult.Failed(LoginStatus.InvalidCredentials);
            }

            var playerRef = user.Players.FirstOrDefault();
            if (playerRef is null)
            {
                return AccountLoginResult.Failed(LoginStatus.NoPlayer);
            }

            var player = await _playerRepo.GetPlayer(playerRef.Id);
            if (player is null)
            {
                return AccountLoginResult.Failed(LoginStatus.PlayerDataNotFound);
            }

            var roles = user.Roles.Select(role => role.Name).ToList();
            var tokens = await IssueTokens(user.Id, roles);

            return AccountLoginResult.Succeeded(tokens, player, user.Id);
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
