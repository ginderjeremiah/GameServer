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

        public AdminSaveResult SaveItemMods(IReadOnlyList<Change<Contracts.ItemMod>> changes)
        {
            // Authoring guards: the rarity/type FKs point at enum-seeded reference tables, so an unmapped value
            // (a missing/0 payload) would 500 on the FK at commit — reject it up front as a clean failure.
            if (ReferenceFieldValidation.FindUndefinedEnum(changes, m => m.RarityId, "item mod rarity") is { } rarityRejection)
            {
                return rarityRejection;
            }

            if (ReferenceFieldValidation.FindUndefinedEnum(changes, m => m.ItemModTypeId, "item mod type") is { } typeRejection)
            {
                return typeRejection;
            }

            return ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Entities.ItemMod
                {
                    Name = item.Name,
                    Description = item.Description,
                    ItemModTypeId = (int)item.ItemModTypeId,
                    RarityId = (int)item.RarityId,
                    DesignerNotes = item.DesignerNotes,
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
                    DesignerNotes = item.DesignerNotes,
                    RetiredAt = item.RetiredAt,
                }),
                key: item => item.Id,
                resourceName: "item mod",
                // An edit must target an existing item mod; a missing id is a not-found rejection (matching the
                // relationship setters), validated up front by the processor before anything is staged.
                editExists: item => _itemMods.LookupItemMod(item.Id) is not null);
        }

        public AdminSaveResult SetAttributes(AddEditAttributesData data)
        {
            var itemMod = _itemMods.LookupItemMod(data.Id);
            if (itemMod is null)
            {
                return AdminSaveResult.NotFound("Item mod");
            }

            // Build a fresh, navigation-free entity per change (not the cached one, whose loaded ItemMod
            // back-reference would drag the whole graph into the change tracker).
            return KeyedChangeSetProcessor.Apply(data.Changes, itemMod.ItemModAttributes,
                itemKey: attribute => (int)attribute.AttributeId,
                existingKey: att => att.AttributeId,
                toEntity: attribute => new Entities.ItemModAttribute
                {
                    ItemModId = itemMod.Id,
                    AttributeId = (int)attribute.AttributeId,
                    Amount = attribute.Amount,
                },
                _entityStore,
                resourceName: "item mod attribute");
        }

        public async Task<AdminSaveResult> SetTags(SetTagsData data, CancellationToken cancellationToken = default)
        {
            if (_itemMods.LookupItemMod(data.Id) is null)
            {
                return AdminSaveResult.NotFound("Item mod");
            }

            await TagAssignmentReconciler.ReconcileAsync(
                _tags.GetTagIdsForItemMod(data.Id),
                _tags.GetExistingTagIds(data.TagIds),
                _entityStore,
                tagId => new Entities.ItemModTag { ItemModId = data.Id, TagId = tagId },
                cancellationToken);

            return AdminSaveResult.Success;
        }
    }
}
