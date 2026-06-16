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
            ChangeSetProcessor.Apply(changes,
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
                delete: item => _entityStore.DeleteByKey<Entities.Tag>(item.Id));

            // Tags carry their own identity and have no owner to miss, so a tag write never rejects — it
            // succeeds to share the unified result contract every admin write reports through.
            return AdminSaveResult.Success;
        }
    }
}
