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

        public void SaveItems(IReadOnlyList<Change<Contracts.Item>> changes)
        {
            ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Entities.Item
                {
                    Name = item.Name,
                    Description = item.Description,
                    ItemCategoryId = (int)item.ItemCategoryId,
                    RarityId = (int)item.RarityId,
                    IconPath = item.IconPath,
                }),
                edit: item =>
                {
                    var existing = _items.LookupItem(item.Id);
                    if (existing is not null)
                    {
                        existing.Name = item.Name;
                        existing.Description = item.Description;
                        existing.ItemCategoryId = (int)item.ItemCategoryId;
                        existing.RarityId = (int)item.RarityId;
                        existing.IconPath = item.IconPath;
                        existing.RetiredAt = item.RetiredAt;
                        _entityStore.Update(existing);
                    }
                });
        }

        public bool SetAttributes(AddEditAttributesData data)
        {
            var item = _items.LookupItem(data.Id);
            if (item is null)
            {
                return false;
            }

            ChangeSetProcessor.Apply(data.Changes,
                add: attribute => _entityStore.Insert(new Entities.ItemAttribute
                {
                    ItemId = item.Id,
                    AttributeId = (int)attribute.AttributeId,
                    Amount = attribute.Amount,
                }),
                // Operate on a fresh, navigation-free entity (not the cached one, whose loaded
                // Item back-reference would drag the whole graph into the change tracker).
                edit: attribute =>
                {
                    if (item.ItemAttributes.Any(att => (int)attribute.AttributeId == att.AttributeId))
                    {
                        _entityStore.Update(new Entities.ItemAttribute
                        {
                            ItemId = item.Id,
                            AttributeId = (int)attribute.AttributeId,
                            Amount = attribute.Amount,
                        });
                    }
                },
                delete: attribute =>
                {
                    if (item.ItemAttributes.Any(att => (int)attribute.AttributeId == att.AttributeId))
                    {
                        _entityStore.Delete(new Entities.ItemAttribute
                        {
                            ItemId = item.Id,
                            AttributeId = (int)attribute.AttributeId,
                        });
                    }
                });

            return true;
        }

        public void SaveModSlots(IReadOnlyList<Change<Contracts.ItemModSlot>> changes)
        {
            ChangeSetProcessor.Apply(changes,
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

        public async Task<bool> SetTags(SetTagsData data)
        {
            if (_items.LookupItem(data.Id) is null)
            {
                return false;
            }

            // Reconcile this item's join rows directly: read only its current tag ids and the desired ids
            // that actually exist, then add/remove a single navigation-free join row per difference — never
            // loading a tag's full item membership.
            var currentTagIds = await _tags.GetTagIdsForItem(data.Id).ToHashSetAsync();
            var desiredTagIds = await _tags.GetExistingTagIds(data.TagIds).ToHashSetAsync();

            foreach (var tagId in currentTagIds)
            {
                if (!desiredTagIds.Contains(tagId))
                {
                    _entityStore.Delete(new Entities.ItemTag { ItemId = data.Id, TagId = tagId });
                }
            }

            foreach (var tagId in desiredTagIds)
            {
                if (!currentTagIds.Contains(tagId))
                {
                    _entityStore.Insert(new Entities.ItemTag { ItemId = data.Id, TagId = tagId });
                }
            }

            return true;
        }
    }
}
