using Game.Abstractions.Contracts;
using Game.Api.Models.Common;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using ItemModSlotEntity = Game.Infrastructure.Entities.ItemModSlot;

namespace Game.Api.Tests.Integration
{
    /// <summary>
    /// Covers the reference-data read endpoint on <see cref="Controllers.ItemsController"/>, which now
    /// indexes the contract <c>All()</c> list with an explicit bounds check instead of looking up an entity.
    /// </summary>
    [Collection("Integration")]
    public class ItemsControllerTests : ApiIntegrationTestBase
    {
        public ItemsControllerTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        private async Task<(HttpClient Client, int ItemId)> SetupAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var item = await TestDataSeeder.CreateItemAsync(context, "Slotted Sword");
            context.Set<ItemModSlotEntity>().Add(new ItemModSlotEntity
            {
                ItemId = item.Id,
                ItemModSlotTypeId = (int)EItemModType.Prefix,
                Index = 0,
            });
            await context.SaveChangesAsync(CancellationToken);

            var user = await TestDataSeeder.CreateUserAsync(context, "itemsuser", "itemspass");
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            return (CreateAuthenticatedClient(user.Id, player.Id), item.Id);
        }

        [Fact]
        public async Task SlotsForItem_ValidItem_ReturnsItsModSlotsAsContracts()
        {
            var (client, itemId) = await SetupAsync();
            using (client)
            {
                var response = await client.GetAsync($"/api/Items/SlotsForItem?itemId={itemId}&refreshCache=true", CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<ItemModSlot>>(CancellationToken);
                Assert.NotNull(result);
                Assert.Null(result.ErrorMessage);
                var slot = Assert.Single(result.Data!);
                Assert.Equal(itemId, slot.ItemId);
                Assert.Equal(EItemModType.Prefix, slot.ItemModSlotTypeId);
            }
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(99999)]
        public async Task SlotsForItem_OutOfRangeItemId_ReturnsEmpty(int itemId)
        {
            var (client, _) = await SetupAsync();
            using (client)
            {
                var response = await client.GetAsync($"/api/Items/SlotsForItem?itemId={itemId}&refreshCache=true", CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<ItemModSlot>>(CancellationToken);
                Assert.NotNull(result);
                Assert.Null(result.ErrorMessage);
                Assert.Empty(result.Data!);
            }
        }
    }
}
