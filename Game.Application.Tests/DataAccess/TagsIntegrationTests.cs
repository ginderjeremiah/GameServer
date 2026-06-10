using Game.Abstractions.DataAccess;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using TagEntity = Game.Infrastructure.Entities.Tag;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Verifies the reference-data read methods on <see cref="ITags"/> return read contracts and that the
    /// underlying entity->contract projections translate in EF.
    /// </summary>
    [Collection("Integration")]
    public class TagsIntegrationTests : ApplicationIntegrationTestBase
    {
        public TagsIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task All_ReturnsAContractForEverySeededTag()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var categoryId = (await context.TagCategories.FirstAsync(CancellationToken)).Id;

            context.Tags.AddRange(
                new TagEntity { Name = "Fire", TagCategoryId = categoryId },
                new TagEntity { Name = "Ice", TagCategoryId = categoryId });
            await context.SaveChangesAsync(CancellationToken);

            var tags = scope.ServiceProvider.GetRequiredService<ITags>();

            var result = await tags.All().ToListAsync(CancellationToken);

            Assert.Contains(result, t => t.Name == "Fire" && t.TagCategoryId == categoryId);
            Assert.Contains(result, t => t.Name == "Ice");
        }
    }
}
