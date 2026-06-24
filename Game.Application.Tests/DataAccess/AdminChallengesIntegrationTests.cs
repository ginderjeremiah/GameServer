using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Contracts = Game.Abstractions.Contracts;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Exercises <see cref="IAdminChallenges"/> <c>SaveChallenges</c>: the zero-based-id Edit-existence
    /// rejection (an out-of-range id is a not-found rejection, not an EF 0-row update), the delete-not-supported
    /// guard, the duplicate-key rejection, and a successful Add/Edit round-trip. Seeding, the admin write, and
    /// the assertion each use a separate DI scope so the write runs against an empty change tracker.
    /// </summary>
    [Collection("Integration")]
    public class AdminChallengesIntegrationTests : ApplicationIntegrationTestBase
    {
        public AdminChallengesIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public void SaveChallenges_EditOutOfRangeId_ReturnsNotFound()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminChallenges>();

            var result = admin.SaveChallenges(
            [
                new Change<Contracts.Challenge> { ChangeType = EChangeType.Edit, Item = NewChallenge(id: 99999) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("Challenge not found.", result.ErrorMessage);
        }

        [Fact]
        public void SaveChallenges_EditNegativeId_ReturnsNotFound()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminChallenges>();

            var result = admin.SaveChallenges(
            [
                new Change<Contracts.Challenge> { ChangeType = EChangeType.Edit, Item = NewChallenge(id: -1) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("Challenge not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveChallenges_AddAndEdit_PersistAndUpdateInPlace()
        {
            int challengeId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                challengeId = (await TestDataSeeder.CreateChallengeAsync(context, name: "Original")).Id;
            }
            await ReloadReferenceCachesAsync();

            var changes = new List<Change<Contracts.Challenge>>
            {
                new() { ChangeType = EChangeType.Add, Item = NewChallenge(name: "Brand New", progressGoal: 25m) },
                new() { ChangeType = EChangeType.Edit, Item = NewChallenge(id: challengeId, name: "Renamed", progressGoal: 50m) },
            };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminChallenges>();
                Assert.True(admin.SaveChallenges(changes).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var edited = await context.Challenges.AsNoTracking().SingleAsync(c => c.Id == challengeId, CancellationToken);
                Assert.Equal("Renamed", edited.Name);
                Assert.Equal(50m, edited.ProgressGoal);
                Assert.Contains(await context.Challenges.AsNoTracking().ToListAsync(CancellationToken), c => c.Name == "Brand New");
            }
        }

        [Fact]
        public async Task SaveChallenges_DeleteOfChallenge_ReturnsFailureNotSupported()
        {
            int challengeId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                challengeId = (await TestDataSeeder.CreateChallengeAsync(context)).Id;
            }
            await ReloadReferenceCachesAsync();

            // Challenges are zero-based-id reference data: a hard delete would open an Id gap, so they are
            // retired, never deleted. A Delete change is a graceful business failure rather than a throw.
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminChallenges>();

            var result = admin.SaveChallenges(
            [
                new Change<Contracts.Challenge> { ChangeType = EChangeType.Delete, Item = NewChallenge(id: challengeId) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Contains("retired, not deleted", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveChallenges_DuplicateEditKey_ReturnsFailureWithoutThrowing()
        {
            int challengeId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                challengeId = (await TestDataSeeder.CreateChallengeAsync(context)).Id;
            }
            await ReloadReferenceCachesAsync();

            // Two Edits of the same id would double-track the row and surface as an opaque EF 500 mid-batch;
            // the processor must reject the malformed batch up front as a graceful failure.
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminChallenges>();

            var result = admin.SaveChallenges(
            [
                new Change<Contracts.Challenge> { ChangeType = EChangeType.Edit, Item = NewChallenge(id: challengeId, name: "A") },
                new Change<Contracts.Challenge> { ChangeType = EChangeType.Edit, Item = NewChallenge(id: challengeId, name: "B") },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("The submitted challenge change set contains duplicate entries.", result.ErrorMessage);
        }

        private static Contracts.Challenge NewChallenge(
            int id = 0, string name = "Test Challenge", decimal progressGoal = 10m) => new()
            {
                Id = id,
                Name = name,
                Description = "",
                ChallengeTypeId = EChallengeType.EnemiesKilled,
                ProgressGoal = progressGoal,
            };
    }
}
