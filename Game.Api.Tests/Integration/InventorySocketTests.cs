using Game.Api.Models.Common;
using Game.Api.Models.Player;
using Game.Core;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using Xunit;

namespace Game.Api.Tests.Integration
{
    /// <summary>
    /// Exercises the player-write inventory socket commands (Equip/Unequip/ApplyMod/RemoveMod) through the
    /// real socket adapter path — command routing, parameter deserialization, and the success/error
    /// mapping onto <see cref="ApiSocketResponse"/>. These replace the former HTTP inventory tests on
    /// <see cref="PlayerControllerTests"/>; the commands moved to the socket so they serialize with the
    /// idle battle loop and can't lose a concurrent read-modify-write race against a background battle
    /// save (#463). The underlying domain logic is covered in Game.Application.Tests.
    /// </summary>
    [Collection("Integration")]
    public class InventorySocketTests : ApiIntegrationTestBase
    {
        public InventorySocketTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task EquipItem_UnlockedItem_SucceedsAndPersists()
        {
            var (userId, itemId) = await SeedPlayerWithInventoryAsync("equipuser", "equippass",
                async (context, playerId, item) =>
                    await TestDataSeeder.LinkItemToPlayerAsync(context, playerId, item.Id));
            var (client, _) = await LoginAndBuildClientAsync("equipuser", "equippass");
            await using var socketClient = await ConnectSocketAsync(userId);

            var parameters = new { itemId, equipmentSlotId = (int)EEquipmentSlot.WeaponSlot };
            var response = await socketClient.SendCommandRawAsync("EquipItem", parameters);

            Assert.Null(response.Error);
            // The save writes the cached player fire-and-forget, so poll the player snapshot until the
            // equip lands — the persistence the lost-update race could silently revert.
            await WaitForEquippedAsync(client, itemId);
        }

        [Fact]
        public async Task EquipItem_ItemNotUnlocked_ReturnsError()
        {
            // The player owns no items, so equipping any item id is rejected by the domain.
            var userId = await SeedPlayerAsync("noequip", "noequip");
            await LoginAsync("noequip", "noequip");
            await using var socketClient = await ConnectSocketAsync(userId);

            var parameters = new { itemId = 0, equipmentSlotId = (int)EEquipmentSlot.WeaponSlot };
            var response = await socketClient.SendCommandRawAsync("EquipItem", parameters);

            Assert.NotNull(response.Error);
        }

        [Fact]
        public async Task UnequipItem_EquippedItem_Succeeds()
        {
            var (userId, _) = await SeedPlayerWithInventoryAsync("unequipuser", "unequippass",
                async (context, playerId, item) =>
                    await TestDataSeeder.LinkItemToPlayerAsync(context, playerId, item.Id, EEquipmentSlot.WeaponSlot));
            await LoginAsync("unequipuser", "unequippass");
            await using var socketClient = await ConnectSocketAsync(userId);

            var parameters = new { equipmentSlotId = (int)EEquipmentSlot.WeaponSlot };
            var response = await socketClient.SendCommandRawAsync("UnequipItem", parameters);

            Assert.Null(response.Error);
        }

        [Fact]
        public async Task UnequipItem_EmptySlot_ReturnsError()
        {
            // Nothing is equipped in the weapon slot, so there is nothing to unequip.
            var userId = await SeedPlayerAsync("emptyslot", "emptyslot");
            await LoginAsync("emptyslot", "emptyslot");
            await using var socketClient = await ConnectSocketAsync(userId);

            var parameters = new { equipmentSlotId = (int)EEquipmentSlot.WeaponSlot };
            var response = await socketClient.SendCommandRawAsync("UnequipItem", parameters);

            Assert.NotNull(response.Error);
        }

        [Fact]
        public async Task ApplyMod_UnlockedModAndItem_Succeeds()
        {
            var modSlotId = 0;
            var modId = 0;
            var (userId, itemId) = await SeedPlayerWithInventoryAsync("applymoduser", "applymodpass",
                async (context, playerId, item) =>
                {
                    var modSlot = await TestDataSeeder.AddItemModSlotAsync(context, item.Id);
                    await TestDataSeeder.LinkItemToPlayerAsync(context, playerId, item.Id, EEquipmentSlot.WeaponSlot);
                    var mod = await TestDataSeeder.CreateItemModAsync(context, attributeId: EAttribute.Dexterity, attributeAmount: 7m);
                    await TestDataSeeder.LinkModToPlayerAsync(context, playerId, mod.Id);
                    modSlotId = modSlot.Id;
                    modId = mod.Id;
                });
            await LoginAsync("applymoduser", "applymodpass");
            await using var socketClient = await ConnectSocketAsync(userId);

            var parameters = new { itemId, itemModId = modId, itemModSlotId = modSlotId };
            var response = await socketClient.SendCommandRawAsync("ApplyMod", parameters);

            Assert.Null(response.Error);
        }

        [Fact]
        public async Task ApplyMod_NonexistentMod_ReturnsError()
        {
            var userId = await SeedPlayerAsync("badmod", "badmod");
            await LoginAsync("badmod", "badmod");
            await using var socketClient = await ConnectSocketAsync(userId);

            // A mod id far beyond any seeded catalog entry is rejected before any application is attempted.
            var parameters = new { itemId = 0, itemModId = 99999, itemModSlotId = 0 };
            var response = await socketClient.SendCommandRawAsync("ApplyMod", parameters);

            Assert.NotNull(response.Error);
        }

        [Fact]
        public async Task RemoveMod_AppliedMod_Succeeds()
        {
            var modSlotId = 0;
            var (userId, itemId) = await SeedPlayerWithInventoryAsync("removemoduser", "removemodpass",
                async (context, playerId, item) =>
                {
                    var modSlot = await TestDataSeeder.AddItemModSlotAsync(context, item.Id);
                    await TestDataSeeder.LinkItemToPlayerAsync(context, playerId, item.Id, EEquipmentSlot.WeaponSlot);
                    var mod = await TestDataSeeder.CreateItemModAsync(context, attributeId: EAttribute.Dexterity, attributeAmount: 7m);
                    await TestDataSeeder.LinkModToPlayerAsync(context, playerId, mod.Id);
                    await TestDataSeeder.ApplyModToItemAsync(context, playerId, item.Id, modSlot.Id, mod.Id);
                    modSlotId = modSlot.Id;
                });
            await LoginAsync("removemoduser", "removemodpass");
            await using var socketClient = await ConnectSocketAsync(userId);

            var parameters = new { itemId, itemModSlotId = modSlotId };
            var response = await socketClient.SendCommandRawAsync("RemoveMod", parameters);

            Assert.Null(response.Error);
        }

        [Fact]
        public async Task RemoveMod_NoAppliedMod_ReturnsError()
        {
            var (userId, itemId) = await SeedPlayerWithInventoryAsync("noremovemoduser", "noremovemodpass",
                async (context, playerId, item) =>
                {
                    await TestDataSeeder.AddItemModSlotAsync(context, item.Id);
                    await TestDataSeeder.LinkItemToPlayerAsync(context, playerId, item.Id, EEquipmentSlot.WeaponSlot);
                });
            await LoginAsync("noremovemoduser", "noremovemodpass");
            await using var socketClient = await ConnectSocketAsync(userId);

            // The item is unlocked but has no mod applied in slot 0, so there is nothing to remove.
            var parameters = new { itemId, itemModSlotId = 0 };
            var response = await socketClient.SendCommandRawAsync("RemoveMod", parameters);

            Assert.NotNull(response.Error);
        }

        /// <summary>
        /// Seeds a user + player (no inventory) and reloads the reference caches so the player resolves on
        /// load. Returns the seeded user id.
        /// </summary>
        private async Task<int> SeedPlayerAsync(string username, string password)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, username, password);
            await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            await ReloadReferenceCachesAsync();
            return user.Id;
        }

        /// <summary>
        /// Seeds a user + player and a weapon item, runs the caller-provided inventory setup against the
        /// seeded item (mod slots, unlocked/applied mods, equipped state), then reloads the reference
        /// caches so the cached player aggregate already carries the inventory. Returns the seeded user
        /// and item ids.
        /// </summary>
        private async Task<(int UserId, int ItemId)> SeedPlayerWithInventoryAsync(
            string username,
            string password,
            Func<GameContext, int, Item, Task> seedInventory)
        {
            int itemId;
            int userId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var user = await TestDataSeeder.CreateUserAsync(context, username, password);
                var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
                var item = await TestDataSeeder.CreateItemAsync(context);
                await seedInventory(context, player.Id, item);
                itemId = item.Id;
                userId = user.Id;
            }

            // The caches no longer lazily refill, so reload them to resolve the seeded item on load.
            await ReloadReferenceCachesAsync();
            return (userId, itemId);
        }

        /// <summary>
        /// Opens an authenticated socket connection for the given user. The caller is responsible for
        /// having already logged in (which creates the session the command path resolves the player from)
        /// and for disposing the returned client.
        /// </summary>
        private async Task<TestSocketClient> ConnectSocketAsync(int userId)
        {
            var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);
            return socketClient;
        }

        /// <summary>
        /// Polls <c>GET /api/Player</c> until the given item shows as equipped (the cache write is
        /// fire-and-forget), failing after a short budget.
        /// </summary>
        private async Task WaitForEquippedAsync(HttpClient client, int itemId)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var response = await client.GetAsync("/api/Player", CancellationToken);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<PlayerData>>(CancellationToken);
                Assert.NotNull(result);
                Assert.NotNull(result.Data);
                var item = result.Data.InventoryData.UnlockedItems.SingleOrDefault(i => i.ItemId == itemId);
                if (item is not null && item.Equipped)
                {
                    return;
                }

                await Task.Delay(50, CancellationToken);
            }

            Assert.Fail("Player inventory did not reach the expected equipped state.");
        }
    }
}
