using Game.Abstractions.DataAccess;
using Game.Application.DependencyInjection;
using Game.Core.Events;
using Game.DataAccess;
using Game.DataAccess.DependencyInjection;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    [Collection("Integration")]
    public class ReferenceDataInitializationTests : ApplicationIntegrationTestBase
    {
        public ReferenceDataInitializationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task InitializeReferenceCachesAsync_LoadsAllCachedSets_SoTheyServeWithoutTheDatabase()
        {
            int itemId, modId, skillId, enemyId, zoneId, challengeId;

            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();

                // Seed one record of every cached reference set.
                itemId = (await TestDataSeeder.CreateItemAsync(context)).Id;
                modId = (await TestDataSeeder.CreateItemModAsync(context)).Id;
                skillId = (await TestDataSeeder.CreateSkillAsync(context)).Id;
                enemyId = (await TestDataSeeder.CreateEnemyAsync(context)).Id;
                zoneId = (await TestDataSeeder.CreateZoneAsync(context)).Id;
                challengeId = (await TestDataSeeder.CreateChallengeAsync(context)).Id;

                // Eagerly load every cache up front — the startup step under test.
                await seedScope.ServiceProvider.InitializeReferenceCachesAsync(CancellationToken);

                // Wipe the reference tables. A lazy fill from here would see nothing, so any data the
                // reads below still return must have been cached by the eager load above.
                await DatabaseCleaner.TruncatePlayerDataAsync(context);
            }

            using var readScope = CreateScope();
            var provider = readScope.ServiceProvider;

            Assert.Equal(itemId, Assert.Single(provider.GetRequiredService<IItems>().All()).Id);
            Assert.Equal(modId, Assert.Single(provider.GetRequiredService<IItemMods>().All()).Id);
            Assert.Equal(skillId, Assert.Single(provider.GetRequiredService<ISkills>().AllSkills()).Id);
            Assert.Equal(enemyId, Assert.Single(provider.GetRequiredService<IEnemies>().All()).Id);
            Assert.Equal(zoneId, Assert.Single(provider.GetRequiredService<IZones>().All()).Id);
            Assert.Equal(challengeId, Assert.Single(provider.GetRequiredService<IChallenges>().All()).Id);
        }

        [Fact]
        public async Task InitializeReferenceCachesAsync_DatabaseUnreachable_Throws()
        {
            // A provider whose database cannot be reached; Redis points at the real test container so
            // only the database load fails.
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.ClearProviders());
            services.AddSingleton(Options.Create(new DataAccessOptions
            {
                DatabaseSystem = Abstractions.Infrastructure.DatabaseSystem.Postgres,
                DbConnectionString = "Host=localhost;Port=1;Database=nonexistent;Username=postgres;Password=postgres;Timeout=2;Command Timeout=2",
                CacheSystem = Abstractions.Infrastructure.CacheSystem.Redis,
                CacheConnectionString = Containers.CacheConnectionString,
                PubSubSystem = Abstractions.Infrastructure.PubSubSystem.Redis,
                PubSubConnectionString = Containers.PubSubConnectionString,
            }));
            services.AddDataAccess();
            services.AddDomainEventDispatcher();
            services.AddApplication();

            await using var provider = services.BuildServiceProvider();

            // The snapshot holders are per-provider singletons starting unpopulated, so the reload must hit
            // the (unreachable) database. Fail fast: the boot must surface the problem rather than swallow it.
            await Assert.ThrowsAnyAsync<Exception>(() => provider.InitializeReferenceCachesAsync(CancellationToken));
        }
    }
}
