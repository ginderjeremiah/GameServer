using Game.Abstractions.Contracts;
using Game.Api.Models.Attributes;
using Game.Api.Models.Common;
using Game.Api.Models.InventoryItems;
using Game.Api.Models.Player;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class PlayerControllerTests : ApiIntegrationTestBase
    {
        public PlayerControllerTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        /// <summary>
        /// Creates a user, player, and skill, then logs in and returns an authenticated client with a valid session.
        /// </summary>
        private async Task<(HttpClient Client, int PlayerId)> CreateAuthenticatedPlayerAsync(
            string username = "playeruser",
            string password = "playerpass",
            int statPointsGained = 100)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, username, password);
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            // The caches no longer lazily refill, so reload them to resolve the player's linked skill on load.
            await ReloadReferenceCachesAsync();

            if (statPointsGained != 100)
            {
                player.StatPointsGained = statPointsGained;
                await context.SaveChangesAsync();
            }

            // Login to create session and obtain a bearer access token
            var (authClient, _) = await LoginAndBuildClientAsync(username, password);
            return (authClient, player.Id);
        }

        [Fact]
        public async Task GetPlayer_Authenticated_ReturnsPlayerData()
        {
            var (authClient, _) = await CreateAuthenticatedPlayerAsync();
            using var client = authClient;

            var response = await client.GetAsync("/api/Player", CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PlayerData>>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
            Assert.NotNull(result.Data);
            Assert.Equal("TestPlayer", result.Data.Name);
        }

        [Fact]
        public async Task GetPlayer_Unauthenticated_Returns401()
        {
            var response = await Client.GetAsync("/api/Player", CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task UpdatePlayerStats_ValidUpdate_ReturnsUpdatedAttributes()
        {
            // Give extra stat points so we can allocate
            var (authClient, _) = await CreateAuthenticatedPlayerAsync(
                username: "statsuser", password: "statspass", statPointsGained: 106);
            using var client = authClient;

            var updates = new List<AttributeUpdate>
            {
                new() { AttributeId = (int)Game.Core.EAttribute.Strength, Amount = 3 },
            };

            var response = await client.PostAsJsonAsync("/api/Player/UpdatePlayerStats", updates, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<BattlerAttribute>>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
            Assert.NotNull(result.Data);

            var attributes = result.Data.ToList();
            Assert.NotEmpty(attributes);
        }

        [Fact]
        public async Task UpdatePlayerStats_SpendMoreThanAvailable_ReturnsError()
        {
            var (authClient, _) = await CreateAuthenticatedPlayerAsync(
                username: "overspend", password: "overspend");
            using var client = authClient;

            var updates = new List<AttributeUpdate>
            {
                new() { AttributeId = (int)Game.Core.EAttribute.Strength, Amount = 999 },
            };

            var response = await client.PostAsJsonAsync("/api/Player/UpdatePlayerStats", updates, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<BattlerAttribute>>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.ErrorMessage);
        }

        // The inventory endpoints exercise the HTTP adapter path only — routing, model binding, and the
        // success/error mapping through ApiResponse (the underlying PlayerService logic is covered in
        // Game.Application.Tests). A false result from the service maps to a 400 via ErrorStatusFilter.

        [Fact]
        public async Task EquipItem_UnlockedItem_ReturnsSuccess()
        {
            var (authClient, itemId) = await CreatePlayerWithInventoryAsync("equipuser", "equippass",
                async (context, playerId, item) =>
                    await TestDataSeeder.LinkItemToPlayerAsync(context, playerId, item.Id));
            using var client = authClient;

            var request = new EquipRequest { ItemId = itemId, EquipmentSlotId = (int)EEquipmentSlot.WeaponSlot };
            var response = await client.PostAsJsonAsync("/api/Player/EquipItem", request, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public async Task EquipItem_ItemNotUnlocked_ReturnsBadRequest()
        {
            // The player owns no items, so equipping any item id is rejected by the domain.
            var (authClient, _) = await CreateAuthenticatedPlayerAsync(username: "noequip", password: "noequip");
            using var client = authClient;

            var request = new EquipRequest { ItemId = 0, EquipmentSlotId = (int)EEquipmentSlot.WeaponSlot };
            var response = await client.PostAsJsonAsync("/api/Player/EquipItem", request, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task EquipItem_Unauthenticated_Returns401()
        {
            var request = new EquipRequest { ItemId = 0, EquipmentSlotId = (int)EEquipmentSlot.WeaponSlot };
            var response = await Client.PostAsJsonAsync("/api/Player/EquipItem", request, CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task UnequipItem_EquippedItem_ReturnsSuccess()
        {
            var (authClient, _) = await CreatePlayerWithInventoryAsync("unequipuser", "unequippass",
                async (context, playerId, item) =>
                    await TestDataSeeder.LinkItemToPlayerAsync(context, playerId, item.Id, EEquipmentSlot.WeaponSlot));
            using var client = authClient;

            var request = new EquipRequest { EquipmentSlotId = (int)EEquipmentSlot.WeaponSlot };
            var response = await client.PostAsJsonAsync("/api/Player/UnequipItem", request, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public async Task UnequipItem_EmptySlot_ReturnsBadRequest()
        {
            // Nothing is equipped in the weapon slot, so there is nothing to unequip.
            var (authClient, _) = await CreateAuthenticatedPlayerAsync(username: "emptyslot", password: "emptyslot");
            using var client = authClient;

            var request = new EquipRequest { EquipmentSlotId = (int)EEquipmentSlot.WeaponSlot };
            var response = await client.PostAsJsonAsync("/api/Player/UnequipItem", request, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task ApplyMod_UnlockedModAndItem_ReturnsSuccess()
        {
            var modSlotId = 0;
            var modId = 0;
            var (authClient, itemId) = await CreatePlayerWithInventoryAsync("applymoduser", "applymodpass",
                async (context, playerId, item) =>
                {
                    var modSlot = await TestDataSeeder.AddItemModSlotAsync(context, item.Id);
                    await TestDataSeeder.LinkItemToPlayerAsync(context, playerId, item.Id, EEquipmentSlot.WeaponSlot);
                    var mod = await TestDataSeeder.CreateItemModAsync(context, attributeId: EAttribute.Dexterity, attributeAmount: 7m);
                    await TestDataSeeder.LinkModToPlayerAsync(context, playerId, mod.Id);
                    modSlotId = modSlot.Id;
                    modId = mod.Id;
                });
            using var client = authClient;

            var request = new ApplyModRequest { ItemId = itemId, ItemModId = modId, ItemModSlotId = modSlotId };
            var response = await client.PostAsJsonAsync("/api/Player/ApplyMod", request, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public async Task ApplyMod_NonexistentMod_ReturnsBadRequest()
        {
            var (authClient, _) = await CreateAuthenticatedPlayerAsync(username: "badmod", password: "badmod");
            using var client = authClient;

            // A mod id far beyond any seeded catalog entry is rejected before any application is attempted.
            var request = new ApplyModRequest { ItemId = 0, ItemModId = 99999, ItemModSlotId = 0 };
            var response = await client.PostAsJsonAsync("/api/Player/ApplyMod", request, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task RemoveMod_AppliedMod_ReturnsSuccess()
        {
            var modSlotId = 0;
            var (authClient, itemId) = await CreatePlayerWithInventoryAsync("removemoduser", "removemodpass",
                async (context, playerId, item) =>
                {
                    var modSlot = await TestDataSeeder.AddItemModSlotAsync(context, item.Id);
                    await TestDataSeeder.LinkItemToPlayerAsync(context, playerId, item.Id, EEquipmentSlot.WeaponSlot);
                    var mod = await TestDataSeeder.CreateItemModAsync(context, attributeId: EAttribute.Dexterity, attributeAmount: 7m);
                    await TestDataSeeder.LinkModToPlayerAsync(context, playerId, mod.Id);
                    await TestDataSeeder.ApplyModToItemAsync(context, playerId, item.Id, modSlot.Id, mod.Id);
                    modSlotId = modSlot.Id;
                });
            using var client = authClient;

            var request = new RemoveModRequest { ItemId = itemId, ItemModSlotId = modSlotId };
            var response = await client.PostAsJsonAsync("/api/Player/RemoveMod", request, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public async Task RemoveMod_NoAppliedMod_ReturnsBadRequest()
        {
            var (authClient, itemId) = await CreatePlayerWithInventoryAsync("noremovemoduser", "noremovemodpass",
                async (context, playerId, item) =>
                {
                    await TestDataSeeder.AddItemModSlotAsync(context, item.Id);
                    await TestDataSeeder.LinkItemToPlayerAsync(context, playerId, item.Id, EEquipmentSlot.WeaponSlot);
                });
            using var client = authClient;

            // The item is unlocked but has no mod applied in slot 0, so there is nothing to remove.
            var request = new RemoveModRequest { ItemId = itemId, ItemModSlotId = 0 };
            var response = await client.PostAsJsonAsync("/api/Player/RemoveMod", request, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.ErrorMessage);
        }

        /// <summary>
        /// Seeds a user + player and a weapon item, runs the caller-provided inventory setup against the
        /// seeded item (mod slots, unlocked/applied mods, equipped state), then reloads the reference
        /// caches and logs in so the cached player aggregate already carries the inventory. Returns an
        /// authenticated client and the seeded item id.
        /// </summary>
        private async Task<(HttpClient Client, int ItemId)> CreatePlayerWithInventoryAsync(
            string username,
            string password,
            Func<GameContext, int, Game.Infrastructure.Entities.Item, Task> seedInventory)
        {
            int itemId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var user = await TestDataSeeder.CreateUserAsync(context, username, password);
                var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
                var item = await TestDataSeeder.CreateItemAsync(context);
                await seedInventory(context, player.Id, item);
                itemId = item.Id;
            }

            // The caches no longer lazily refill, so reload them to resolve the seeded item on load.
            await ReloadReferenceCachesAsync();

            var (client, _) = await LoginAndBuildClientAsync(username, password);
            return (client, itemId);
        }
    }
}
