using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Content Authoring persistence for tags. Builds fresh, navigation-free entities for every write.
    /// </summary>
    internal class AdminTags(IEntityStore entityStore, ITagAssignmentQueries tagAssignmentQueries) : IAdminTags
    {
        private readonly IEntityStore _entityStore = entityStore;
        private readonly ITagAssignmentQueries _tagAssignmentQueries = tagAssignmentQueries;

        public async Task<AdminSaveResult> SaveTags(IReadOnlyList<Change<Contracts.Tag>> changes, CancellationToken cancellationToken = default)
        {
            // A tag's category is a FK into the enum-seeded TagCategories table, so an unmapped value (a
            // missing/0 payload) would 500 on the FK at commit — reject it up front as a clean failure.
            if (ReferenceFieldValidation.FindUndefinedEnum(changes, t => (ETagCategory)t.TagCategoryId, "tag category") is { } categoryRejection)
            {
                return categoryRejection;
            }

            // Tags have no in-memory cache to check membership against (unlike the zero-based reference sets'
            // editExists), so an Edit/Delete naming an id that no longer exists (a stale admin client — a second
            // tab, a double-clicked delete) is checked against the database up front. Without this, the resulting
            // 0-row UPDATE/DELETE throws DbUpdateConcurrencyException out of the deferred commit instead of a
            // clean not-found rejection.
            var targetedIds = changes.Where(c => c.ChangeType != EChangeType.Add).Select(c => c.Item.Id).ToHashSet();
            if (targetedIds.Count > 0)
            {
                var existingIds = await _tagAssignmentQueries.GetExistingTagIds(targetedIds)
                    .ToHashSetAsync(cancellationToken: cancellationToken);
                if (targetedIds.Any(id => !existingIds.Contains(id)))
                {
                    return AdminSaveResult.NotFound("Tag");
                }
            }

            // Tags carry their own identity and have no owner to miss, but they support a real delete, so a
            // duplicate Edit/Delete of one tag would double-track it in EF — reject such a malformed batch up
            // front as a graceful business failure rather than 500-ing (Add ids are store-generated sentinels,
            // so they are excluded from the guard).
            return ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(ToEntity(item)),
                edit: item =>
                {
                    var entity = ToEntity(item);
                    entity.Id = item.Id;
                    _entityStore.Update(entity);
                },
                delete: item => _entityStore.DeleteByKey<Entities.Tag>(item.Id),
                key: item => item.Id,
                resourceName: "tag");
        }

        private static Entities.Tag ToEntity(Contracts.Tag item)
        {
            return new Entities.Tag
            {
                Name = item.Name,
                TagCategoryId = item.TagCategoryId,
            };
        }
    }
}
