using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Content Authoring persistence for item mods and their related collections. Reuses the cached
    /// entity lookup (<see cref="IItemModEntityCache.LookupItemMod"/>) for existence/diff and the
    /// tag-assignment queries on <see cref="ITagAssignmentQueries"/>; all writes go through the entity store.
    /// </summary>
    internal class AdminItemMods(IItemModEntityCache itemMods, ITagAssignmentQueries tags, IEntityStore entityStore) : IAdminItemMods
    {
        private readonly IItemModEntityCache _itemMods = itemMods;
        private readonly ITagAssignmentQueries _tags = tags;
        private readonly IEntityStore _entityStore = entityStore;

        public void SaveItemMods(IReadOnlyList<Change<Contracts.ItemMod>> changes)
        {
            ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Entities.ItemMod
                {
                    Name = item.Name,
                    Description = item.Description,
                    ItemModTypeId = (int)item.ItemModTypeId,
                    RarityId = (int)item.RarityId,
                }),
                edit: item =>
                {
                    var existing = _itemMods.LookupItemMod(item.Id);
                    if (existing is not null)
                    {
                        existing.Name = item.Name;
                        existing.Description = item.Description;
                        existing.ItemModTypeId = (int)item.ItemModTypeId;
                        existing.RarityId = (int)item.RarityId;
                        existing.RetiredAt = item.RetiredAt;
                        _entityStore.Update(existing);
                    }
                });
        }

        public bool SetAttributes(AddEditAttributesData data)
        {
            var itemMod = _itemMods.LookupItemMod(data.Id);
            if (itemMod is null)
            {
                return false;
            }

            ChangeSetProcessor.Apply(data.Changes,
                add: attribute => _entityStore.Insert(new Entities.ItemModAttribute
                {
                    ItemModId = itemMod.Id,
                    AttributeId = (int)attribute.AttributeId,
                    Amount = attribute.Amount,
                }),
                // Operate on a fresh, navigation-free entity (not the cached one, whose loaded
                // ItemMod back-reference would drag the whole graph into the change tracker).
                edit: attribute =>
                {
                    if (itemMod.ItemModAttributes.Any(att => (int)attribute.AttributeId == att.AttributeId))
                    {
                        _entityStore.Update(new Entities.ItemModAttribute
                        {
                            ItemModId = itemMod.Id,
                            AttributeId = (int)attribute.AttributeId,
                            Amount = attribute.Amount,
                        });
                    }
                },
                delete: attribute =>
                {
                    if (itemMod.ItemModAttributes.Any(att => (int)attribute.AttributeId == att.AttributeId))
                    {
                        _entityStore.Delete(new Entities.ItemModAttribute
                        {
                            ItemModId = itemMod.Id,
                            AttributeId = (int)attribute.AttributeId,
                        });
                    }
                });

            return true;
        }

        public async Task<bool> SetTags(SetTagsData data)
        {
            if (_itemMods.LookupItemMod(data.Id) is null)
            {
                return false;
            }

            // Reconcile this mod's join rows directly: read only its current tag ids and the desired ids
            // that actually exist, then add/remove a single navigation-free join row per difference — never
            // loading a tag's full item-mod membership.
            var currentTagIds = await _tags.GetTagIdsForItemMod(data.Id).ToHashSetAsync();
            var desiredTagIds = await _tags.GetExistingTagIds(data.TagIds).ToHashSetAsync();

            foreach (var tagId in currentTagIds)
            {
                if (!desiredTagIds.Contains(tagId))
                {
                    _entityStore.Delete(new Entities.ItemModTag { ItemModId = data.Id, TagId = tagId });
                }
            }

            foreach (var tagId in desiredTagIds)
            {
                if (!currentTagIds.Contains(tagId))
                {
                    _entityStore.Insert(new Entities.ItemModTag { ItemModId = data.Id, TagId = tagId });
                }
            }

            return true;
        }
    }
}
