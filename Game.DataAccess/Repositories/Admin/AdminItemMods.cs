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

        public bool SaveItemMods(IReadOnlyList<Change<Contracts.ItemMod>> changes)
        {
            // An edit must target an existing item mod; a missing id is a not-found rejection (matching the
            // relationship setters), not a silent success. Validate the whole batch up front so the
            // commit filter doesn't persist the rest of the batch alongside an invalid edit.
            if (changes.Any(c => c.ChangeType == EChangeType.Edit && _itemMods.LookupItemMod(c.Item.Id) is null))
            {
                return false;
            }

            ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Entities.ItemMod
                {
                    Name = item.Name,
                    Description = item.Description,
                    ItemModTypeId = (int)item.ItemModTypeId,
                    RarityId = (int)item.RarityId,
                }),
                // Build a fresh, navigation-free entity rather than mutating the cached one, whose loaded
                // graph would otherwise be dragged into the change tracker.
                edit: item => _entityStore.Update(new Entities.ItemMod
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    ItemModTypeId = (int)item.ItemModTypeId,
                    RarityId = (int)item.RarityId,
                    RetiredAt = item.RetiredAt,
                }));

            return true;
        }

        public bool SetAttributes(AddEditAttributesData data)
        {
            var itemMod = _itemMods.LookupItemMod(data.Id);
            if (itemMod is null)
            {
                return false;
            }

            // Build a fresh, navigation-free entity per change (not the cached one, whose loaded ItemMod
            // back-reference would drag the whole graph into the change tracker).
            AttributeChangeSetProcessor.Apply(data.Changes, itemMod.ItemModAttributes,
                existingKey: att => att.AttributeId,
                toEntity: attribute => new Entities.ItemModAttribute
                {
                    ItemModId = itemMod.Id,
                    AttributeId = (int)attribute.AttributeId,
                    Amount = attribute.Amount,
                },
                _entityStore);

            return true;
        }

        public async Task<bool> SetTags(SetTagsData data)
        {
            if (_itemMods.LookupItemMod(data.Id) is null)
            {
                return false;
            }

            await TagAssignmentReconciler.ReconcileAsync(
                _tags.GetTagIdsForItemMod(data.Id),
                _tags.GetExistingTagIds(data.TagIds),
                _entityStore,
                tagId => new Entities.ItemModTag { ItemModId = data.Id, TagId = tagId });

            return true;
        }
    }
}
