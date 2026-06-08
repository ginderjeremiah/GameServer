using Game.Abstractions.DataAccess;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using TagEntity = Game.Infrastructure.Entities.Tag;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Verifies the reference-data read methods on <see cref="ITags"/> return read contracts and that the
    /// underlying entity->contract projections translate in EF (the tag-by-item/-item-mod filters in
    /// particular run as SQL subqueries).
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

        [Fact]
        public async Task GetTagsForItem_ReturnsOnlyTheItemsTagsAsContracts()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var categoryId = (await context.TagCategories.FirstAsync(CancellationToken)).Id;

            var item = await TestDataSeeder.CreateItemAsync(context, "Tagged Sword");
            var linked = new TagEntity { Name = "Sharp", TagCategoryId = categoryId };
            var unlinked = new TagEntity { Name = "Blunt", TagCategoryId = categoryId };
            context.Tags.AddRange(linked, unlinked);
            await context.SaveChangesAsync(CancellationToken);

            var itemEntity = await context.Items.Include(i => i.Tags).FirstAsync(i => i.Id == item.Id, CancellationToken);
            itemEntity.Tags.Add(linked);
            await context.SaveChangesAsync(CancellationToken);

            var tags = scope.ServiceProvider.GetRequiredService<ITags>();

            var result = await tags.GetTagsForItem(item.Id).ToListAsync(CancellationToken);

            var tag = Assert.Single(result);
            Assert.Equal("Sharp", tag.Name);
            Assert.Equal(linked.Id, tag.Id);
        }

        [Fact]
        public async Task GetTagsForItemMod_ReturnsOnlyTheItemModsTagsAsContracts()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var categoryId = (await context.TagCategories.FirstAsync(CancellationToken)).Id;

            var itemMod = await TestDataSeeder.CreateItemModAsync(context, "Tagged Mod");
            var linked = new TagEntity { Name = "Elemental", TagCategoryId = categoryId };
            var unlinked = new TagEntity { Name = "Physical", TagCategoryId = categoryId };
            context.Tags.AddRange(linked, unlinked);
            await context.SaveChangesAsync(CancellationToken);

            var modEntity = await context.ItemMods.Include(im => im.Tags).FirstAsync(im => im.Id == itemMod.Id, CancellationToken);
            modEntity.Tags.Add(linked);
            await context.SaveChangesAsync(CancellationToken);

            var tags = scope.ServiceProvider.GetRequiredService<ITags>();

            var result = await tags.GetTagsForItemMod(itemMod.Id).ToListAsync(CancellationToken);

            var tag = Assert.Single(result);
            Assert.Equal("Elemental", tag.Name);
        }
    }
}
