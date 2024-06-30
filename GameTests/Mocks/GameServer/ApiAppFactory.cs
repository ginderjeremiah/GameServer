using GameCore;
using GameCore.Entities;
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
            var playerId = 1;
            var playerData = new Player
            {
                Exp = 0,
                Level = 1,
                Salt = salt,
                PassHash = "Password123".Hash(salt.ToString()),
                Id = playerId,
                Name = "SwankyJeans",
                StatPointsGained = 20,
                StatPointsUsed = 0,
                UserName = "SwankyJeans",
                PlayerAttributes =
                [
                    new PlayerAttribute { PlayerId = playerId, AttributeId = 0, Amount = 5.0m },
                    new PlayerAttribute { PlayerId = playerId, AttributeId = 1, Amount = 5.0m },
                    new PlayerAttribute { PlayerId = playerId, AttributeId = 2, Amount = 5.0m },
                    new PlayerAttribute { PlayerId = playerId, AttributeId = 3, Amount = 5.0m },
                    new PlayerAttribute { PlayerId = playerId, AttributeId = 4, Amount = 5.0m },
                    new PlayerAttribute { PlayerId = playerId, AttributeId = 5, Amount = 5.0m }
                ],
                InventoryItems =
                [
                    new InventoryItem { PlayerId = playerId, Id = 1, Equipped = true, InventorySlotNumber = 1, InventoryItemMods = []},
                    new InventoryItem { PlayerId = playerId, Id = 2, Equipped = true, InventorySlotNumber = 3, InventoryItemMods = []},
                    new InventoryItem { PlayerId = playerId, Id = 3, Equipped = false, InventorySlotNumber = 1, InventoryItemMods = []},
                    new InventoryItem { PlayerId = playerId, Id = 4, Equipped = false, InventorySlotNumber = 2, InventoryItemMods = []},
                    new InventoryItem { PlayerId = playerId, Id = 5, Equipped = false, InventorySlotNumber = 3, InventoryItemMods = []},
                ],
                PlayerSkills =
                [
                    new PlayerSkill { PlayerId = playerId, Selected = true, SkillId = 0, }
                ],
            };

            var sessionData = new SessionData(Guid.NewGuid().ToString())
            {
                CurrentZone = 1,
                EarliestDefeat = DateTime.UtcNow,
                EnemyCooldown = DateTime.UtcNow,

                LastUsed = DateTime.UtcNow,
                PlayerData = playerData,

                Victory = true
            };

            CacheService.Set(sessionData.Id, sessionData);
            ClientOptions.HandleCookies = false;
            Session = new(sessionData, Repositories);
            client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"{Constants.TOKEN_NAME}={Session.GetNewToken()}");
        }

        public Session RefreshSession()
        {
            var sessionData = CacheService.Get<SessionData>(Session.SessionId);
            Session = new(sessionData, Repositories);
            return Session;
        }
    }
}