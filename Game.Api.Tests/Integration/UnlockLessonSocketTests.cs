using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class UnlockLessonSocketTests : ApiIntegrationTestBase
    {
        private const string Username = "unlocklessonuser";
        private const string Password = "unlocklessonpass";

        public UnlockLessonSocketTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        private async Task<(int UserId, int PlayerId, int LessonId)> SeedPlayerAndLessonAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var lesson = await TestDataSeeder.CreateLessonAsync(context);
            var user = await TestDataSeeder.CreateUserAsync(context, Username, Password);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            // The caches no longer lazily refill, so reload them to resolve the seeded lesson id.
            await ReloadReferenceCachesAsync();

            return (user.Id, player.Id, lesson.Id);
        }

        [Fact]
        public async Task UnlockLesson_PersistsUnlockedAtToCachedPlayer()
        {
            var (userId, playerId, lessonId) = await SeedPlayerAndLessonAsync();
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            var response = await socketClient.SendCommandRawAsync("UnlockLesson", lessonId);

            Assert.Null(response.Error);

            var lessons = await WaitForLessonAsync(playerId, lessonId);
            var persisted = lessons.Single(l => l.LessonId == lessonId);
            Assert.NotEqual(default, persisted.UnlockedAt);
            Assert.Null(persisted.ReadAt);
        }

        [Fact]
        public async Task UnlockLesson_AlreadyUnlocked_IsIdempotent()
        {
            var (userId, playerId, lessonId) = await SeedPlayerAndLessonAsync();
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            Assert.Null((await socketClient.SendCommandRawAsync("UnlockLesson", lessonId)).Error);
            await WaitForLessonAsync(playerId, lessonId);

            // A re-fired client-side detector must not error or duplicate the row.
            var response = await socketClient.SendCommandRawAsync("UnlockLesson", lessonId);

            Assert.Null(response.Error);
            var lessons = (await GetPersistedPlayerAsync(playerId)).Lessons;
            Assert.Single(lessons);
        }

        [Fact]
        public async Task UnlockLesson_UnknownLessonId_ReturnsError()
        {
            var (userId, _, _) = await SeedPlayerAndLessonAsync();
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            var response = await socketClient.SendCommandRawAsync("UnlockLesson", 9999);

            Assert.NotNull(response.Error);
        }

        /// <summary>
        /// The command writes the cached player fire-and-forget, so poll the player snapshot until the
        /// expected lesson row lands (or fail after a short budget).
        /// </summary>
        private async Task<List<Game.Api.Models.Player.PlayerLesson>> WaitForLessonAsync(int playerId, int lessonId)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var lessons = (await GetPersistedPlayerAsync(playerId)).Lessons;
                if (lessons.Any(l => l.LessonId == lessonId))
                {
                    return lessons;
                }

                await Task.Delay(50, CancellationToken);
            }

            Assert.Fail($"Lesson {lessonId} was not unlocked in time.");
            return [];
        }
    }
}
