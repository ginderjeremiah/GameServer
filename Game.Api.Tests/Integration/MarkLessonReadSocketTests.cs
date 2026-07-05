using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class MarkLessonReadSocketTests : ApiIntegrationTestBase
    {
        private const string Username = "marklessonreaduser";
        private const string Password = "marklessonreadpass";

        private async Task<(int UserId, int PlayerId, int LessonId)> SeedPlayerAndLessonAsync(bool unlocked)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var lesson = await TestDataSeeder.CreateLessonAsync(context);
            var user = await TestDataSeeder.CreateUserAsync(context, Username, Password);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            if (unlocked)
            {
                await TestDataSeeder.AddPlayerLessonAsync(context, player.Id, lesson.Id);
            }

            // The caches no longer lazily refill, so reload them to resolve the seeded lesson id.
            await ReloadReferenceCachesAsync();

            return (user.Id, player.Id, lesson.Id);
        }

        public MarkLessonReadSocketTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task MarkLessonRead_PreviouslyUnlocked_SetsReadAt()
        {
            var (userId, playerId, lessonId) = await SeedPlayerAndLessonAsync(unlocked: true);
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            var response = await socketClient.SendCommandRawAsync("MarkLessonRead", lessonId);

            Assert.Null(response.Error);

            var lessons = await WaitForReadAsync(playerId, lessonId);
            Assert.NotNull(lessons.Single(l => l.LessonId == lessonId).ReadAt);
        }

        [Fact]
        public async Task MarkLessonRead_NeverUnlocked_NormalizesToReadWithoutError()
        {
            // A screen-anchored lesson plays immediately on first visit with no prior UnlockLesson call.
            var (userId, playerId, lessonId) = await SeedPlayerAndLessonAsync(unlocked: false);
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            var response = await socketClient.SendCommandRawAsync("MarkLessonRead", lessonId);

            Assert.Null(response.Error);

            var lessons = await WaitForReadAsync(playerId, lessonId);
            var persisted = lessons.Single(l => l.LessonId == lessonId);
            Assert.NotEqual(default, persisted.UnlockedAt);
            Assert.NotNull(persisted.ReadAt);
        }

        [Fact]
        public async Task MarkLessonRead_UnknownLessonId_ReturnsError()
        {
            var (userId, _, _) = await SeedPlayerAndLessonAsync(unlocked: false);
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            var response = await socketClient.SendCommandRawAsync("MarkLessonRead", 9999);

            Assert.NotNull(response.Error);
        }

        /// <summary>
        /// The command writes the cached player fire-and-forget, so poll the player snapshot until the
        /// expected read timestamp lands (or fail after a short budget).
        /// </summary>
        private async Task<List<Game.Api.Models.Player.PlayerLesson>> WaitForReadAsync(int playerId, int lessonId)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var lessons = (await GetPersistedPlayerAsync(playerId)).Lessons;
                if (lessons.Any(l => l.LessonId == lessonId && l.ReadAt is not null))
                {
                    return lessons;
                }

                await Task.Delay(50, CancellationToken);
            }

            Assert.Fail($"Lesson {lessonId} was not marked read in time.");
            return [];
        }
    }
}
