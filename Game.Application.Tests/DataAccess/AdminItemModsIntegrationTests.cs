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
    /// Exercises <see cref="IAdminItemMods"/> write paths: the identity-level Edit-existence rejection and
    /// delete-not-supported guard on <c>SaveItemMods</c>, the membership-guarded attribute upsert and its
    /// duplicate-key rejection, and the tag-association replace. Seeding, the admin write, and the assertion
    /// each use a separate DI scope so the write runs against an empty change tracker, as a real admin call does.
    /// </summary>
    [Collection("Integration")]
    public class AdminItemModsIntegrationTests : ApplicationIntegrationTestBase
    {
        public AdminItemModsIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public void SaveItemMods_EditUnknownItemMod_ReturnsNotFound()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminItemMods>();

            var result = admin.SaveItemMods(
            [
                new Change<Contracts.ItemMod> { ChangeType = EChangeType.Edit, Item = NewItemMod(id: 99999) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("Item mod not found.", result.ErrorMessage);
        }

        [Fact]
        public void SaveItemMods_UndefinedRarity_ReturnsFailureWithoutPersisting()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminItemMods>();

            var result = admin.SaveItemMods(
            [
                new Change<Contracts.ItemMod> { ChangeType = EChangeType.Add, Item = NewItemMod(name: "Bad Rarity", rarity: (ERarity)0) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("0 is not a valid item mod rarity.", result.ErrorMessage);
        }

        [Fact]
        public void SaveItemMods_UndefinedType_ReturnsFailureWithoutPersisting()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminItemMods>();

            var result = admin.SaveItemMods(
            [
                new Change<Contracts.ItemMod> { ChangeType = EChangeType.Add, Item = NewItemMod(name: "Bad Type", type: (EItemModType)0) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("0 is not a valid item mod type.", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveItemMods_AddAndEdit_PersistAndUpdateInPlace()
        {
            int modId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                modId = (await TestDataSeeder.CreateItemModAsync(context, name: "Original")).Id;
            }
            await ReloadReferenceCachesAsync();

            var changes = new List<Change<Contracts.ItemMod>>
            {
                new() { ChangeType = EChangeType.Add, Item = NewItemMod(name: "Brand New") },
                new() { ChangeType = EChangeType.Edit, Item = NewItemMod(id: modId, name: "Renamed", rarity: ERarity.Rare) },
            };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminItemMods>();
                Assert.True(admin.SaveItemMods(changes).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var edited = await context.ItemMods.AsNoTracking().SingleAsync(m => m.Id == modId, CancellationToken);
                Assert.Equal("Renamed", edited.Name);
                Assert.Equal((int)ERarity.Rare, edited.RarityId);
                Assert.Contains(await context.ItemMods.AsNoTracking().ToListAsync(CancellationToken), m => m.Name == "Brand New");
            }
        }

        [Fact]
        public async Task SaveItemMods_DeleteOfItemMod_ReturnsFailureNotSupported()
        {
            int modId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                modId = (await TestDataSeeder.CreateItemModAsync(context)).Id;
            }
            await ReloadReferenceCachesAsync();

            // Item mods are zero-based-id reference data: a hard delete would open an Id gap, so it is
            // retired (a RetiredAt edit), never deleted. A Delete change is a graceful business failure.
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminItemMods>();

            var result = admin.SaveItemMods(
            [
                new Change<Contracts.ItemMod> { ChangeType = EChangeType.Delete, Item = NewItemMod(id: modId) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Contains("retired, not deleted", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveItemMods_DuplicateEditKey_ReturnsFailureWithoutThrowing()
        {
            int modId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                modId = (await TestDataSeeder.CreateItemModAsync(context)).Id;
            }
            await ReloadReferenceCachesAsync();

            // Two Edits of the same id would double-track the row and surface as an opaque EF 500 mid-batch;
            // the processor must reject the malformed batch up front as a graceful failure.
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminItemMods>();

            var result = admin.SaveItemMods(
            [
                new Change<Contracts.ItemMod> { ChangeType = EChangeType.Edit, Item = NewItemMod(id: modId, name: "A") },
                new Change<Contracts.ItemMod> { ChangeType = EChangeType.Edit, Item = NewItemMod(id: modId, name: "B") },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("The submitted item mod change set contains duplicate entries.", result.ErrorMessage);
        }

        [Fact]
        public void SetAttributes_UnknownItemMod_ReturnsNotFound()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminItemMods>();

            var result = admin.SetAttributes(new AddEditAttributesData { Id = 99999, Changes = [] });

            Assert.False(result.Succeeded);
            Assert.Equal("Item mod not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task SetAttributes_AddsEditsAndDeletesAgainstMembership()
        {
            int modId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                // CreateItemModAsync seeds a single Strength attribute (amount 5).
                modId = (await TestDataSeeder.CreateItemModAsync(context)).Id;
            }
            await ReloadReferenceCachesAsync();

            // Add Intellect, edit the existing Strength, and delete a non-member (Endurance) — the delete must
            // reconcile away as a guarded no-op rather than an EF 0-row delete.
            var data = new AddEditAttributesData
            {
                Id = modId,
                Changes =
                [
                    new Change<Contracts.BattlerAttribute> { ChangeType = EChangeType.Add, Item = NewAttribute(EAttribute.Intellect, 7m) },
                    new Change<Contracts.BattlerAttribute> { ChangeType = EChangeType.Edit, Item = NewAttribute(EAttribute.Strength, 99m) },
                    new Change<Contracts.BattlerAttribute> { ChangeType = EChangeType.Delete, Item = NewAttribute(EAttribute.Endurance, 0m) },
                ],
            };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminItemMods>();
                Assert.True(admin.SetAttributes(data).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var attributes = await context.ItemModAttributes.AsNoTracking()
                    .Where(a => a.ItemModId == modId)
                    .ToListAsync(CancellationToken);

                Assert.Equal(2, attributes.Count);
                Assert.Equal(99m, attributes.Single(a => a.AttributeId == (int)EAttribute.Strength).Amount);
                Assert.Equal(7m, attributes.Single(a => a.AttributeId == (int)EAttribute.Intellect).Amount);
                Assert.DoesNotContain(attributes, a => a.AttributeId == (int)EAttribute.Endurance);
            }
        }

        [Fact]
        public async Task SetAttributes_DuplicateKeyInBatch_ReturnsFailureWithoutThrowing()
        {
            int modId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                modId = (await TestDataSeeder.CreateItemModAsync(context)).Id;
            }
            await ReloadReferenceCachesAsync();

            // Two Adds of the same attribute would double-track the composite key; reject the batch up front.
            var data = new AddEditAttributesData
            {
                Id = modId,
                Changes =
                [
                    new Change<Contracts.BattlerAttribute> { ChangeType = EChangeType.Add, Item = NewAttribute(EAttribute.Agility, 1m) },
                    new Change<Contracts.BattlerAttribute> { ChangeType = EChangeType.Add, Item = NewAttribute(EAttribute.Agility, 2m) },
                ],
            };

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminItemMods>();

            var result = admin.SetAttributes(data);

            Assert.False(result.Succeeded);
            Assert.Equal("The submitted item mod attribute change set contains duplicate entries.", result.ErrorMessage);
        }

        [Fact]
        public async Task SetTags_UnknownItemMod_ReturnsNotFound()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminItemMods>();

            var result = await admin.SetTags(new SetTagsData { Id = 99999, TagIds = [] }, CancellationToken);

            Assert.False(result.Succeeded);
            Assert.Equal("Item mod not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task SetTags_ReplacesAssociations()
        {
            int modId, keptTagId, addedTagId, removedTagId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                modId = (await TestDataSeeder.CreateItemModAsync(context)).Id;
                keptTagId = (await TestDataSeeder.CreateTagAsync(context, name: "Kept")).Id;
                addedTagId = (await TestDataSeeder.CreateTagAsync(context, name: "Added")).Id;
                removedTagId = (await TestDataSeeder.CreateTagAsync(context, name: "Removed")).Id;
                context.ItemModTags.AddRange(
                    new Entities.ItemModTag { ItemModId = modId, TagId = keptTagId },
                    new Entities.ItemModTag { ItemModId = modId, TagId = removedTagId });
                await context.SaveChangesAsync(CancellationToken);
            }
            await ReloadReferenceCachesAsync();

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminItemMods>();
                var result = await admin.SetTags(new SetTagsData { Id = modId, TagIds = [keptTagId, addedTagId] }, CancellationToken);
                Assert.True(result.Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var tagIds = (await context.ItemModTags.AsNoTracking()
                    .Where(t => t.ItemModId == modId)
                    .Select(t => t.TagId)
                    .ToListAsync(CancellationToken))
                    .OrderBy(id => id)
                    .ToList();

                Assert.Equal([keptTagId, addedTagId], tagIds);
                Assert.DoesNotContain(removedTagId, tagIds);
            }
        }

        private static Contracts.ItemMod NewItemMod(
            int id = 0, string name = "Test Mod", EItemModType type = EItemModType.Prefix, ERarity rarity = ERarity.Common) => new()
            {
                Id = id,
                Name = name,
                Description = "",
                ItemModTypeId = type,
                RarityId = rarity,
                Attributes = [],
                Tags = [],
            };

        private static Contracts.BattlerAttribute NewAttribute(EAttribute attribute, decimal amount) =>
            new() { AttributeId = attribute, Amount = amount };
    }
}
