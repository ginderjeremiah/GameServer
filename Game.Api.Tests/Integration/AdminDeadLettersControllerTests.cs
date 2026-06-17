using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.Infrastructure;
using Game.Api.Models.Common;
using Game.Core;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Game.Api.Tests.Integration
{
    /// <summary>
    /// Exercises the admin dead-letter Ops endpoints end-to-end over HTTP: the read-only inspection surface
    /// and the guarded replay (#794), including the Admin-role gate. The queue mechanics themselves are
    /// covered by the data-tier integration tests; these verify the controller wiring, the Admin gate, and
    /// the request/response shape.
    /// </summary>
    [Collection("Integration")]
    public class AdminDeadLettersControllerTests : ApiIntegrationTestBase
    {
        // Mirror the (internal) Game.DataAccess.Constants — these are the stable Redis queue names.
        private const string PlayerUpdateQueue = "PlayerUpdateQueue";
        private const string PlayerUpdateDeadLetterQueue = "PlayerUpdateDeadLetterQueue";

        public AdminDeadLettersControllerTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task GetPlayerUpdateDeadLetters_NonAdmin_IsForbidden()
        {
            var client = Factory.CreateClient();
            TestAuthHelper.AddAuthHeader(client, userId: 5001);

            var response = await client.GetAsync("/api/AdminTools/GetPlayerUpdateDeadLetters", CancellationToken);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            client.Dispose();
        }

        [Fact]
        public async Task GetPlayerUpdateDeadLetters_Admin_SurfacesAndClassifiesQueuedEntries()
        {
            await SeedDeadLettersAsync(
                "not-json",
                Envelope(nameof(Game.Core.Players.Events.ItemUnlockedEvent), "{\"playerId\":42,\"itemId\":7}"));

            using var client = AdminClient();
            var response = await client.GetAsync("/api/AdminTools/GetPlayerUpdateDeadLetters", CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<DeadLetterInspection>>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.Data);
            Assert.Equal(2, result.Data.TotalCount);

            Assert.Equal(EDeadLetterReason.Malformed, result.Data.Entries[0].Reason);

            var replayable = result.Data.Entries[1];
            Assert.Equal(EDeadLetterReason.Replayable, replayable.Reason);
            Assert.Equal("ItemUnlockedEvent", replayable.EventType);
            Assert.Equal(42, replayable.PlayerId);
        }

        [Fact]
        public async Task ReplayPlayerUpdateDeadLetters_All_MovesEntriesOntoThePlayerQueue()
        {
            var first = Envelope(nameof(Game.Core.Players.Events.ItemUnlockedEvent), "{\"playerId\":1,\"itemId\":10}");
            var second = Envelope(nameof(Game.Core.Players.Events.ItemUnlockedEvent), "{\"playerId\":2,\"itemId\":20}");
            await SeedDeadLettersAsync(first, second);

            using var client = AdminClient();
            var response = await client.PostAsJsonAsync("/api/AdminTools/ReplayPlayerUpdateDeadLetters", new { All = true }, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<DeadLetterReplayResult>>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.Data);
            Assert.Equal(2, result.Data.ReplayedCount);
            Assert.Equal(0, result.Data.RemainingCount);

            Assert.Equal(0, await DeadLetterDepthAsync());
            Assert.Equal([first, second], await PeekPlayerQueueAsync());
        }

        [Fact]
        public async Task ReplayPlayerUpdateDeadLetters_Selected_MovesOnlyTheChosenEntry()
        {
            var keep = Envelope(nameof(Game.Core.Players.Events.ItemUnlockedEvent), "{\"playerId\":1,\"itemId\":10}");
            var replay = Envelope(nameof(Game.Core.Players.Events.ItemUnlockedEvent), "{\"playerId\":2,\"itemId\":20}");
            await SeedDeadLettersAsync(keep, replay);

            using var client = AdminClient();
            var response = await client.PostAsJsonAsync("/api/AdminTools/ReplayPlayerUpdateDeadLetters", new { All = false, Payloads = new[] { replay } }, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<DeadLetterReplayResult>>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.Data);
            Assert.Equal(1, result.Data.ReplayedCount);
            Assert.Equal(1, result.Data.RemainingCount);

            Assert.Equal([replay], await PeekPlayerQueueAsync());
        }

        private HttpClient AdminClient()
        {
            var client = Factory.CreateClient();
            TestAuthHelper.AddAuthHeader(client, userId: 5002, nameof(ERole.Admin));
            return client;
        }

        private async Task SeedDeadLettersAsync(params string[] messages)
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            await pubsub.GetQueue(PlayerUpdateDeadLetterQueue).AddRangeToQueueAsync(messages);
        }

        private async Task<long> DeadLetterDepthAsync()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            return await pubsub.GetQueue(PlayerUpdateDeadLetterQueue).GetLengthAsync();
        }

        private async Task<IReadOnlyList<string>> PeekPlayerQueueAsync()
        {
            using var scope = CreateScope();
            var pubsub = scope.ServiceProvider.GetRequiredService<IPubSubService>();
            return await pubsub.GetQueue(PlayerUpdateQueue).PeekAsync(50);
        }

        private static string Envelope(string type, string payloadJson)
            => new { type, payload = payloadJson }.Serialize();
    }
}
