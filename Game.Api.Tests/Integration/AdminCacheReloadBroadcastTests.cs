using Game.Abstractions.Infrastructure;
using Game.Api.Models.Common;
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
    /// <summary>
    /// Verifies the publish side of cross-instance reference-cache invalidation (#359): a successful admin
    /// write broadcasts a reference-data-changed notification over the pub/sub backplane (in addition to
    /// the awaited local reload), so every other API instance can background-reload its own caches. The
    /// subscribe side is covered by Game.Application.Tests' ReferenceCacheSynchronizerTests.
    /// </summary>
    [Collection("Integration")]
    public class AdminCacheReloadBroadcastTests : ApiIntegrationTestBase
    {
        // Mirrors Game.DataAccess Constants.PUBSUB_REFERENCE_DATA_CHANNEL (internal to the data tier);
        // the channel name is the cross-instance wire contract this test pins.
        private const string ReferenceDataChannel = "referenceData";

        public AdminCacheReloadBroadcastTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task AdminWrite_BroadcastsReferenceDataChangedNotification()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "adminuser", "adminpass");
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            using var authClient = await CreateAuthenticatedClient(user.Id, player.Id, nameof(ERole.Admin));

            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var subscriptionId = $"broadcast-test-{Guid.NewGuid()}";
            await pubsub.Subscribe(ReferenceDataChannel, args => received.TrySetResult(args.message), subscriptionId);
            try
            {
                var changes = new[]
                {
                    new
                    {
                        Item = new
                        {
                            Id = 0,
                            Name = "Broadcast",
                            TagCategoryId = (int)ETagCategory.Accessory
                        },
                        ChangeType = 0 // Add
                    }
                };

                var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditTags", changes, CancellationToken);

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
                Assert.NotNull(result);
                Assert.Null(result.ErrorMessage);

                // The payload is the publishing instance's id — a subscribing instance uses it to skip
                // its own broadcasts, so it must never be empty.
                var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
                Assert.False(string.IsNullOrWhiteSpace(message));
            }
            finally
            {
                await pubsub.UnSubscribe(ReferenceDataChannel, subscriptionId);
            }
        }
    }
}
