using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Content Authoring persistence for tags. Builds fresh, navigation-free entities for every write.
    /// </summary>
    internal class AdminTags(IEntityStore entityStore) : IAdminTags
    {
        private readonly IEntityStore _entityStore = entityStore;

        public AdminSaveResult SaveTags(IReadOnlyList<Change<Contracts.Tag>> changes)
        {
            // Tags carry their own identity and have no owner to miss, but they support a real delete, so a
            // duplicate Edit/Delete of one tag would double-track it in EF — reject such a malformed batch up
            // front as a graceful business failure rather than 500-ing (Add ids are store-generated sentinels,
            // so they are excluded from the guard). An otherwise well-formed tag write never rejects.
            return ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Entities.Tag
                {
                    Name = item.Name,
                    TagCategoryId = item.TagCategoryId,
                }),
                edit: item => _entityStore.Update(new Entities.Tag
                {
                    Id = item.Id,
                    Name = item.Name,
                    TagCategoryId = item.TagCategoryId,
                }),
                delete: item => _entityStore.DeleteByKey<Entities.Tag>(item.Id),
                key: item => item.Id,
                resourceName: "tag");
        }
    }
}
