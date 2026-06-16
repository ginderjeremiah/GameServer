using Game.Abstractions.DataAccess;
using Game.DataAccess.Repositories;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Entities = Game.Infrastructure.Entities;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Exercises the generic key-only delete affordance on <see cref="IEntityStore"/> directly against EF, so
    /// it is pinned independently of any one admin repository. A single-column key is the Tag case; a
    /// composite key proves the affordance generalizes to any future required-scalar entity.
    /// </summary>
    [Collection("Integration")]
    public class EntityStoreIntegrationTests : ApplicationIntegrationTestBase
    {
        public EntityStoreIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task DeleteByKey_SingleColumnKey_RemovesTheRowWithoutFabricatingScalars()
        {
            int tagId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                tagId = (await TestDataSeeder.CreateTagAsync(context, "Doomed")).Id;

                using (var writeScope = CreateScope())
                {
                    var store = writeScope.ServiceProvider.GetRequiredService<IEntityStore>();
                    store.DeleteByKey<Entities.Tag>(tagId);
                    await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
                }
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                Assert.False(await context.Tags.AnyAsync(t => t.Id == tagId, CancellationToken));
            }
        }

        [Fact]
        public async Task DeleteByKey_CompositeKey_RemovesTheJoinRow()
        {
            int enemyId, skillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var enemy = await TestDataSeeder.CreateEnemyAsync(context);
                var skill = await TestDataSeeder.CreateSkillAsync(context, "Linked");
                await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
                enemyId = enemy.Id;
                skillId = skill.Id;

                using (var writeScope = CreateScope())
                {
                    var store = writeScope.ServiceProvider.GetRequiredService<IEntityStore>();
                    // EnemySkill's key is { EnemyId, SkillId } — order follows the model's key definition.
                    store.DeleteByKey<Entities.EnemySkill>(enemyId, skillId);
                    await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
                }
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                Assert.False(await context.EnemySkills.AnyAsync(es => es.EnemyId == enemyId && es.SkillId == skillId, CancellationToken));
            }
        }

        [Fact]
        public void DeleteByKey_WrongKeyValueCount_Throws()
        {
            using var scope = CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IEntityStore>();

            // Tag has a single-column key, so supplying two values is a programming error, not a 0-row delete.
            var ex = Assert.Throws<ArgumentException>(() => store.DeleteByKey<Entities.Tag>(1, 2));
            Assert.Equal("keyValues", ex.ParamName);
        }
    }
}
