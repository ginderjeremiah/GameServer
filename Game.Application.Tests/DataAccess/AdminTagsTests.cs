using Game.Abstractions.Contracts.Admin;
using Game.DataAccess.Repositories;
using Game.DataAccess.Repositories.Admin;
using Xunit;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Unit-tests the entity-store operations <see cref="AdminTags.SaveTags"/> stages for each change kind,
    /// without a database. The delete path in particular must stage a key-only delete (no fabricated
    /// <c>Name</c>), since <c>Tag.Name</c> is a required scalar the delete doesn't need.
    /// </summary>
    public class AdminTagsTests
    {
        // Reports back exactly the ids supplied at construction as "existing" — a database-free stand-in for
        // the existence check SaveTags now performs against Edit/Delete ids (#2210).
        private sealed class FakeTagAssignmentQueries(params int[] existingIds) : ITagAssignmentQueries
        {
            public IAsyncEnumerable<int> GetExistingTagIds(IReadOnlyCollection<int> tagIds) =>
                tagIds.Where(existingIds.Contains).ToAsyncEnumerable();

            public IAsyncEnumerable<int> GetTagIdsForItem(int itemId) => throw new NotSupportedException();

            public IAsyncEnumerable<int> GetTagIdsForItemMod(int itemModId) => throw new NotSupportedException();
        }

        private static Change<Contracts.Tag> Change(EChangeType type, int id, string name = "Tag", int categoryId = 0) =>
            new() { ChangeType = type, Item = new Contracts.Tag { Id = id, Name = name, TagCategoryId = categoryId } };

        [Fact]
        public async Task SaveTags_Add_InsertsTagWithNameAndCategory()
        {
            var store = new RecordingEntityStore();
            var adminTags = new AdminTags(store, new FakeTagAssignmentQueries());

            var result = await adminTags.SaveTags([Change(EChangeType.Add, id: 0, name: "Fire", categoryId: 3)]);

            Assert.True(result.Succeeded);
            var inserted = Assert.IsType<Entities.Tag>(Assert.Single(store.Inserted));
            Assert.Equal("Fire", inserted.Name);
            Assert.Equal(3, inserted.TagCategoryId);
            Assert.Empty(store.Updated);
            Assert.Empty(store.DeletedByKey);
        }

        [Fact]
        public async Task SaveTags_Edit_UpdatesTagByIdWithNewValues()
        {
            var store = new RecordingEntityStore();
            var adminTags = new AdminTags(store, new FakeTagAssignmentQueries(7));

            var result = await adminTags.SaveTags([Change(EChangeType.Edit, id: 7, name: "Ice", categoryId: 2)]);

            Assert.True(result.Succeeded);
            var updated = Assert.IsType<Entities.Tag>(Assert.Single(store.Updated));
            Assert.Equal(7, updated.Id);
            Assert.Equal("Ice", updated.Name);
            Assert.Equal(2, updated.TagCategoryId);
            Assert.Empty(store.Inserted);
            Assert.Empty(store.DeletedByKey);
        }

        [Fact]
        public async Task SaveTags_Delete_DeletesByKeyWithoutFabricatingName()
        {
            var store = new RecordingEntityStore();
            var adminTags = new AdminTags(store, new FakeTagAssignmentQueries(42));

            var result = await adminTags.SaveTags([Change(EChangeType.Delete, id: 42)]);

            Assert.True(result.Succeeded);
            var (entityType, keyValues) = Assert.Single(store.DeletedByKey);
            Assert.Equal(typeof(Entities.Tag), entityType);
            Assert.Equal([42], keyValues);
            // The delete never materializes a full entity, so nothing is staged through the instance paths.
            Assert.Empty(store.Deleted);
            Assert.Empty(store.Inserted);
            Assert.Empty(store.Updated);
        }

        [Fact]
        public async Task SaveTags_AddWithUndefinedCategory_ReturnsFailureWithoutStaging()
        {
            var store = new RecordingEntityStore();
            var adminTags = new AdminTags(store, new FakeTagAssignmentQueries());

            // TagCategoryId 0 has no backing ETagCategory member (the enum starts at 1); without the up-front
            // guard this would 500 on the FK at commit instead of rejecting gracefully.
            var result = await adminTags.SaveTags([Change(EChangeType.Add, id: 0, name: "Fire", categoryId: 0)]);

            Assert.False(result.Succeeded);
            Assert.Equal("0 is not a valid tag category.", result.ErrorMessage);
            Assert.Empty(store.Inserted);
        }

        [Fact]
        public async Task SaveTags_EditWithUndefinedCategory_ReturnsFailureWithoutStaging()
        {
            var store = new RecordingEntityStore();
            var adminTags = new AdminTags(store, new FakeTagAssignmentQueries(7));

            var result = await adminTags.SaveTags([Change(EChangeType.Edit, id: 7, name: "Ice", categoryId: 99)]);

            Assert.False(result.Succeeded);
            Assert.Equal("99 is not a valid tag category.", result.ErrorMessage);
            Assert.Empty(store.Updated);
        }

        [Fact]
        public async Task SaveTags_DeleteWithUndefinedCategoryPayload_Succeeds()
        {
            var store = new RecordingEntityStore();
            var adminTags = new AdminTags(store, new FakeTagAssignmentQueries(42));

            // Deletes carry no meaningful category payload (the field just defaults to 0), so the reference
            // guard skips them like every other change-type it's applied to.
            var result = await adminTags.SaveTags([Change(EChangeType.Delete, id: 42)]);

            Assert.True(result.Succeeded);
        }

        [Fact]
        public async Task SaveTags_EditMissingId_ReturnsNotFoundWithoutStaging()
        {
            var store = new RecordingEntityStore();
            var adminTags = new AdminTags(store, new FakeTagAssignmentQueries());

            var result = await adminTags.SaveTags([Change(EChangeType.Edit, id: 7, name: "Ice", categoryId: 2)]);

            Assert.False(result.Succeeded);
            Assert.Equal("Tag not found.", result.ErrorMessage);
            Assert.Empty(store.Updated);
        }

        [Fact]
        public async Task SaveTags_DeleteMissingId_ReturnsNotFoundWithoutStaging()
        {
            var store = new RecordingEntityStore();
            var adminTags = new AdminTags(store, new FakeTagAssignmentQueries());

            var result = await adminTags.SaveTags([Change(EChangeType.Delete, id: 42)]);

            Assert.False(result.Succeeded);
            Assert.Equal("Tag not found.", result.ErrorMessage);
            Assert.Empty(store.DeletedByKey);
        }
    }
}
