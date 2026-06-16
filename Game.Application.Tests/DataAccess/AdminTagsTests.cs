using Game.Abstractions.Contracts.Admin;
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
        private static Change<Contracts.Tag> Change(EChangeType type, int id, string name = "Tag", int categoryId = 0) =>
            new() { ChangeType = type, Item = new Contracts.Tag { Id = id, Name = name, TagCategoryId = categoryId } };

        [Fact]
        public void SaveTags_Add_InsertsTagWithNameAndCategory()
        {
            var store = new RecordingEntityStore();
            var adminTags = new AdminTags(store);

            var result = adminTags.SaveTags([Change(EChangeType.Add, id: 0, name: "Fire", categoryId: 3)]);

            Assert.True(result.Succeeded);
            var inserted = Assert.IsType<Entities.Tag>(Assert.Single(store.Inserted));
            Assert.Equal("Fire", inserted.Name);
            Assert.Equal(3, inserted.TagCategoryId);
            Assert.Empty(store.Updated);
            Assert.Empty(store.DeletedByKey);
        }

        [Fact]
        public void SaveTags_Edit_UpdatesTagByIdWithNewValues()
        {
            var store = new RecordingEntityStore();
            var adminTags = new AdminTags(store);

            var result = adminTags.SaveTags([Change(EChangeType.Edit, id: 7, name: "Ice", categoryId: 2)]);

            Assert.True(result.Succeeded);
            var updated = Assert.IsType<Entities.Tag>(Assert.Single(store.Updated));
            Assert.Equal(7, updated.Id);
            Assert.Equal("Ice", updated.Name);
            Assert.Equal(2, updated.TagCategoryId);
            Assert.Empty(store.Inserted);
            Assert.Empty(store.DeletedByKey);
        }

        [Fact]
        public void SaveTags_Delete_DeletesByKeyWithoutFabricatingName()
        {
            var store = new RecordingEntityStore();
            var adminTags = new AdminTags(store);

            var result = adminTags.SaveTags([Change(EChangeType.Delete, id: 42)]);

            Assert.True(result.Succeeded);
            var (entityType, keyValues) = Assert.Single(store.DeletedByKey);
            Assert.Equal(typeof(Entities.Tag), entityType);
            Assert.Equal([42], keyValues);
            // The delete never materializes a full entity, so nothing is staged through the instance paths.
            Assert.Empty(store.Deleted);
            Assert.Empty(store.Inserted);
            Assert.Empty(store.Updated);
        }
    }
}
