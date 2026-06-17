using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Content Authoring persistence for items and their related collections. Reuses the cached entity
    /// lookup (<see cref="IItemEntityCache.LookupItem"/>) for existence/diff and the tag-assignment queries
    /// on <see cref="ITagAssignmentQueries"/>; all writes go through the entity store. Changes are staged on
    /// the unit of work; the per-action commit filter persists them.
    /// </summary>
    internal class AdminItems(IItemEntityCache items, ITagAssignmentQueries tags, IEntityStore entityStore) : IAdminItems
    {
        private readonly IItemEntityCache _items = items;
        private readonly ITagAssignmentQueries _tags = tags;
        private readonly IEntityStore _entityStore = entityStore;

        public AdminSaveResult SaveItems(IReadOnlyList<Change<Contracts.Item>> changes)
        {
            // An edit must target an existing item; a missing id is a not-found rejection (matching the
            // relationship setters), not a silent success. Validate the whole batch up front so the
            // commit filter doesn't persist the rest of the batch alongside an invalid edit.
            if (changes.Any(c => c.ChangeType == EChangeType.Edit && _items.LookupItem(c.Item.Id) is null))
            {
                return AdminSaveResult.NotFound("Item");
            }

            return ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Entities.Item
                {
                    Name = item.Name,
                    Description = item.Description,
                    ItemCategoryId = (int)item.ItemCategoryId,
                    RarityId = (int)item.RarityId,
                    IconPath = item.IconPath,
                }),
                // Build a fresh, navigation-free entity rather than mutating the cached one, whose loaded
                // graph would otherwise be dragged into the change tracker.
                edit: item => _entityStore.Update(new Entities.Item
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    ItemCategoryId = (int)item.ItemCategoryId,
                    RarityId = (int)item.RarityId,
                    IconPath = item.IconPath,
                    RetiredAt = item.RetiredAt,
                }));
        }

        public AdminSaveResult SetAttributes(AddEditAttributesData data)
        {
            var item = _items.LookupItem(data.Id);
            if (item is null)
            {
                return AdminSaveResult.NotFound("Item");
            }

            // Build a fresh, navigation-free entity per change (not the cached one, whose loaded Item
            // back-reference would drag the whole graph into the change tracker).
            AttributeChangeSetProcessor.Apply(data.Changes, item.ItemAttributes,
                existingKey: att => att.AttributeId,
                toEntity: attribute => new Entities.ItemAttribute
                {
                    ItemId = item.Id,
                    AttributeId = (int)attribute.AttributeId,
                    Amount = attribute.Amount,
                },
                _entityStore);

            return AdminSaveResult.Success;
        }

        public AdminSaveResult SaveModSlots(IReadOnlyList<Change<Contracts.ItemModSlot>> changes)
        {
            return ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Entities.ItemModSlot
                {
                    ItemId = item.ItemId,
                    ItemModSlotTypeId = (int)item.ItemModSlotTypeId,
                }),
                edit: item => _entityStore.Update(new Entities.ItemModSlot
                {
                    Id = item.Id,
                    ItemId = item.ItemId,
                    ItemModSlotTypeId = (int)item.ItemModSlotTypeId,
                }),
                delete: item => _entityStore.Delete(new Entities.ItemModSlot
                {
                    Id = item.Id,
                }));
        }

        public async Task<AdminSaveResult> SetTags(SetTagsData data, CancellationToken cancellationToken = default)
        {
            if (_items.LookupItem(data.Id) is null)
            {
                return AdminSaveResult.NotFound("Item");
            }

            await TagAssignmentReconciler.ReconcileAsync(
                _tags.GetTagIdsForItem(data.Id),
                _tags.GetExistingTagIds(data.TagIds),
                _entityStore,
                tagId => new Entities.ItemTag { ItemId = data.Id, TagId = tagId },
                cancellationToken);

            return AdminSaveResult.Success;
        }
    }
}
