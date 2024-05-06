using GameCore;
using GameCore.Entities.Players;
using GameCore.Entities.SessionStore;
using GameCore.Infrastructure;
using GameCore.Sessions;
using GameServer;
using GameTests.Mocks.DataAccess;
using GameTests.Mocks.GameCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace GameTests.Mocks.GameServer
{
    internal class ApiAppFactory : WebApplicationFactory<Startup>
    {
        private MockRepositoryManager? _repositoryManager;
        public MockLogger Logger { get; set; } = new();
        public MockRepositoryManager Repositories => _repositoryManager ??= new MockRepositoryManager(CacheService);
        public MockCacheService CacheService { get; set; } = new();
        public Session Session { get; set; }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IApiLogger>(Logger);
                services.AddSingleton<ICacheService>(CacheService);
                services.AddSingleton<IRepositoryManager>(Repositories);
            });
        }

        public void AddAuthorizedSession(HttpClient client)
        {
            var salt = Guid.NewGuid();
            var playerData = new Player
            {
                Exp = 0,
                Level = 1,
                Salt = salt,
                PassHash = "Password123".Hash(salt.ToString()),
                PlayerId = 1,
                PlayerName = "SwankyJeans",
                StatPointsGained = 20,
                StatPointsUsed = 0,
                UserName = "SwankyJeans"
            };

            var sessionData = new SessionData
            {
                Attributes = new(),
                CurrentZone = 1,
                EarliestDefeat = DateTime.UtcNow,
                EnemyCooldown = DateTime.UtcNow,
                InventoryItems = new(),
                LastUsed = DateTime.UtcNow,
                PlayerData = playerData,
                PlayerSkills = new(),
                SessionId = Guid.NewGuid().ToString(),
                Victory = true
            };

            CacheService.Set(sessionData.SessionId, sessionData);
            ClientOptions.HandleCookies = false;
            Session = new(sessionData, Repositories);
            client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"{Constants.TOKEN_NAME}={Session.GetNewToken()}");
        }
    }
}