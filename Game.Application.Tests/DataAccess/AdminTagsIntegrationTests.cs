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
using Entities = Game.Infrastructure.Entities;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Exercises <see cref="IAdminTags"/> end-to-end through the entity store and unit of work. The focus is
    /// the key-only hard delete: a tag carries a <c>required</c> Name yet is removed by key alone, the delete
    /// stays in the change-tracker batch, and an in-use tag's assignment join rows cascade away (see
    /// <c>docs/backend.md</c> → "Tags are the deliberate exception"). Seeding, the admin write, and the
    /// assertion each use a separate DI scope so the write runs against an empty change tracker, mirroring the
    /// per-request scope of a real admin call.
    /// </summary>
    [Collection("Integration")]
    public class AdminTagsIntegrationTests : ApplicationIntegrationTestBase
    {
        public AdminTagsIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        private static Change<Contracts.Tag> Delete(Entities.Tag tag) =>
            new()
            {
                ChangeType = EChangeType.Delete,
                // The contract still carries Name (it's required), but the delete persists by key only.
                Item = new Contracts.Tag { Id = tag.Id, Name = tag.Name, TagCategoryId = tag.TagCategoryId },
            };

        [Fact]
        public async Task SaveTags_Delete_RemovesTagByKey()
        {
            int tagId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var tag = await TestDataSeeder.CreateTagAsync(context, "Doomed");
                tagId = tag.Id;

                using (var writeScope = CreateScope())
                {
                    var admin = writeScope.ServiceProvider.GetRequiredService<IAdminTags>();
                    Assert.True((await admin.SaveTags([Delete(tag)])).Succeeded);
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
        public async Task SaveTags_Delete_InUseTag_CascadesAssignmentJoinRows()
        {
            int tagId, itemId, itemModId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var tag = await TestDataSeeder.CreateTagAsync(context, "Shared");
                var item = await TestDataSeeder.CreateItemAsync(context, "Tagged Item");
                var itemMod = await TestDataSeeder.CreateItemModAsync(context, "Tagged Mod");
                tagId = tag.Id;
                itemId = item.Id;
                itemModId = itemMod.Id;

                context.ItemTags.Add(new Entities.ItemTag { ItemId = itemId, TagId = tagId });
                context.ItemModTags.Add(new Entities.ItemModTag { ItemModId = itemModId, TagId = tagId });
                await context.SaveChangesAsync(CancellationToken);

                using (var writeScope = CreateScope())
                {
                    var admin = writeScope.ServiceProvider.GetRequiredService<IAdminTags>();
                    Assert.True((await admin.SaveTags([Delete(tag)])).Succeeded);
                    await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
                }
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                Assert.False(await context.Tags.AnyAsync(t => t.Id == tagId, CancellationToken));
                Assert.False(await context.ItemTags.AnyAsync(it => it.TagId == tagId, CancellationToken));
                Assert.False(await context.ItemModTags.AnyAsync(imt => imt.TagId == tagId, CancellationToken));
                // The cascade is scoped to the deleted tag — the owning item and item mod survive.
                Assert.True(await context.Items.AnyAsync(i => i.Id == itemId, CancellationToken));
                Assert.True(await context.ItemMods.AnyAsync(im => im.Id == itemModId, CancellationToken));
            }
        }

        [Fact]
        public async Task SaveTags_Delete_LeavesUntargetedTagsIntact()
        {
            int doomedId, keptId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var doomed = await TestDataSeeder.CreateTagAsync(context, "Doomed");
                var kept = await TestDataSeeder.CreateTagAsync(context, "Kept");
                doomedId = doomed.Id;
                keptId = kept.Id;

                using (var writeScope = CreateScope())
                {
                    var admin = writeScope.ServiceProvider.GetRequiredService<IAdminTags>();
                    Assert.True((await admin.SaveTags([Delete(doomed)])).Succeeded);
                    await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
                }
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                Assert.False(await context.Tags.AnyAsync(t => t.Id == doomedId, CancellationToken));
                Assert.True(await context.Tags.AnyAsync(t => t.Id == keptId, CancellationToken));
            }
        }

        [Fact]
        public async Task SaveTags_EditMissingId_ReturnsNotFoundAndLeavesRowUntouched()
        {
            int missingId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var tag = await TestDataSeeder.CreateTagAsync(context, "Ephemeral");
                missingId = tag.Id;
                // Remove it directly (bypassing the admin write) so the id is genuinely absent, mirroring a
                // stale admin client naming an id a concurrent delete already removed.
                context.Tags.Remove(tag);
                await context.SaveChangesAsync(CancellationToken);
            }

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminTags>();
                var edit = new Change<Contracts.Tag>
                {
                    ChangeType = EChangeType.Edit,
                    Item = new Contracts.Tag { Id = missingId, Name = "Ghost", TagCategoryId = (int)ETagCategory.Accessory },
                };

                var result = await admin.SaveTags([edit]);

                Assert.False(result.Succeeded);
                Assert.Equal("Tag not found.", result.ErrorMessage);
            }
        }

        [Fact]
        public async Task SaveTags_DeleteMissingId_ReturnsNotFound()
        {
            int missingId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var tag = await TestDataSeeder.CreateTagAsync(context, "Ephemeral");
                missingId = tag.Id;
                context.Tags.Remove(tag);
                await context.SaveChangesAsync(CancellationToken);
            }

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminTags>();
                var delete = new Change<Contracts.Tag>
                {
                    ChangeType = EChangeType.Delete,
                    Item = new Contracts.Tag { Id = missingId, Name = "Ghost", TagCategoryId = (int)ETagCategory.Accessory },
                };

                var result = await admin.SaveTags([delete]);

                Assert.False(result.Succeeded);
                Assert.Equal("Tag not found.", result.ErrorMessage);
            }
        }
    }
}
