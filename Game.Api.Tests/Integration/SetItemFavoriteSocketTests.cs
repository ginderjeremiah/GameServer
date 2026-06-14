using Game.Api.Models.InventoryItems;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Api.Tests.Integration
{
    /// <summary>
    /// Integration tests for the <c>SetItemFavorite</c> socket command. #251 was a real persistence bug
    /// in exactly this favorite flow, so an end-to-end socket test guards the command's write path.
    /// </summary>
    [Collection("Integration")]
    public class SetItemFavoriteSocketTests : ApiIntegrationTestBase
    {
        private const string Username = "favoriteuser";
        private const string Password = "favoritepass";

        public SetItemFavoriteSocketTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        private async Task<(int UserId, int PlayerId, int ItemId)> SeedPlayerWithUnlockedItemAsync()
        {
            int userId;
            int playerId;
            int itemId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();

                var user = await TestDataSeeder.CreateUserAsync(context, Username, Password);
                var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
                var item = await TestDataSeeder.CreateItemAsync(context);
                await TestDataSeeder.LinkItemToPlayerAsync(context, player.Id, item.Id);
                userId = user.Id;
                playerId = player.Id;
                itemId = item.Id;
            }

            // The caches no longer lazily refill, so reload them to resolve the seeded item on load.
            await ReloadReferenceCachesAsync();
            return (userId, playerId, itemId);
        }

        [Fact]
        public async Task SetItemFavorite_PersistsFavoriteFlagToCachedPlayer()
        {
            var (userId, playerId, itemId) = await SeedPlayerWithUnlockedItemAsync();
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            var response = await socketClient.SendCommandRawAsync("SetItemFavorite", new { ItemId = itemId, Favorite = true });

            Assert.Null(response.Error);

            var items = await WaitForFavoriteAsync(playerId, itemId, favorite: true);
            Assert.True(items.Single(i => i.ItemId == itemId).Favorite);
        }

        [Fact]
        public async Task SetItemFavorite_ItemNotUnlocked_ReturnsError()
        {
            var (userId, _, _) = await SeedPlayerWithUnlockedItemAsync();
            // Logging in creates the session the WebSocket handshake requires.
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            // An item id the player has not unlocked must be rejected, not silently accepted.
            var response = await socketClient.SendCommandRawAsync("SetItemFavorite", new { ItemId = 9999, Favorite = true });

            Assert.NotNull(response.Error);
        }

        /// <summary>
        /// The command writes the cached player fire-and-forget, so poll the player snapshot until the
        /// expected favorite flag lands (or fail after a short budget).
        /// </summary>
        private async Task<List<InventoryItem>> WaitForFavoriteAsync(int playerId, int itemId, bool favorite)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var items = (await GetPersistedPlayerAsync(playerId)).InventoryData.UnlockedItems;
                if (items.Any(i => i.ItemId == itemId && i.Favorite == favorite))
                {
                    return items;
                }

                await Task.Delay(50, CancellationToken);
            }

            Assert.Fail($"Item {itemId} favorite flag did not reach expected value {favorite}.");
            return [];
        }
    }
}
