using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Microsoft.EntityFrameworkCore;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Content Authoring persistence for items and their related collections. Reuses the cached entity
    /// lookup (<see cref="IItemEntityCache.LookupItem"/>) for existence/diff and the tag-graph queries on
    /// <see cref="ITagEntityQueries"/>; all writes go through the entity store. Changes are staged on the
    /// unit of work; the per-action commit filter persists them.
    /// </summary>
    internal class AdminItems(IItemEntityCache items, ITagEntityQueries tags, IEntityStore entityStore) : IAdminItems
    {
        private readonly IItemEntityCache _items = items;
        private readonly ITagEntityQueries _tags = tags;
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
                        _entityStore.Update(existing);
                    }
                },
                delete: item =>
                {
                    var existing = _items.LookupItem(item.Id);
                    if (existing is not null)
                    {
                        _entityStore.Delete(existing);
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
            var item = _items.LookupItem(data.Id);
            if (item is null)
            {
                return false;
            }

            _entityStore.Track(item);
            var currentTags = await _tags.GetTagEntitiesForItem(data.Id).ToListAsync();
            foreach (var currentTag in currentTags)
            {
                if (!data.TagIds.Contains(currentTag.Id))
                {
                    currentTag.Items.Clear();
                }
            }

            var tags = _tags.GetTags(data.TagIds);
            await foreach (var tag in tags)
            {
                if (!currentTags.Any(t => t.Id == tag.Id))
                {
                    tag.Items = [];
                    tag.Items.Add(item);
                }
            }

            return true;
        }
    }
}
