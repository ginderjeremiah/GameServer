using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Microsoft.EntityFrameworkCore;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Content Authoring persistence for item mods and their related collections. Reuses the cached
    /// entity lookup (<see cref="IItemModEntityCache.LookupItemMod"/>) for existence/diff and the
    /// tag-graph queries on <see cref="ITagEntityQueries"/>; all writes go through the entity store.
    /// </summary>
    internal class AdminItemMods(IItemModEntityCache itemMods, ITagEntityQueries tags, IEntityStore entityStore) : IAdminItemMods
    {
        private readonly IItemModEntityCache _itemMods = itemMods;
        private readonly ITagEntityQueries _tags = tags;
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
            var itemMod = _itemMods.LookupItemMod(data.Id);
            if (itemMod is null)
            {
                return false;
            }

            _entityStore.Track(itemMod);
            var currentTags = await _tags.GetTagEntitiesForItemMod(data.Id).ToListAsync();
            foreach (var currentTag in currentTags)
            {
                if (!data.TagIds.Contains(currentTag.Id))
                {
                    currentTag.ItemMods.Clear();
                }
            }

            var tags = _tags.GetTags(data.TagIds);
            await foreach (var tag in tags)
            {
                if (!currentTags.Any(t => t.Id == tag.Id))
                {
                    tag.ItemMods = [];
                    tag.ItemMods.Add(itemMod);
                }
            }

            return true;
        }
    }
}
