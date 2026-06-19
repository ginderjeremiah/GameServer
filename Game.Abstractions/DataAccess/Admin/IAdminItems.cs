using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;

namespace Game.Abstractions.DataAccess.Admin
{
    /// <summary>
    /// Content Authoring persistence for items and their related collections (attributes, mod slots,
    /// tags). Encapsulates the EF specifics behind an entity-free admin contract surface.
    /// </summary>
    public interface IAdminItems
    {
        /// <summary>Applies an identity-level Add/Edit/Delete change set to the item catalogue.
        /// Fails (applying nothing) if an edit targets an item that does not exist.</summary>
        AdminSaveResult SaveItems(IReadOnlyList<Change<Item>> changes);

        /// <summary>Applies a change set to an item's attributes. Fails if the item does not exist.</summary>
        AdminSaveResult SetAttributes(AddEditAttributesData data);

        /// <summary>Applies an Add/Edit/Delete change set to item mod slots. Fails (applying nothing) if an
        /// add targets an item that does not exist; an edit/delete of a slot the named item does not have is
        /// a guarded no-op.</summary>
        AdminSaveResult SaveModSlots(IReadOnlyList<Change<ItemModSlot>> changes);

        /// <summary>Replaces an item's tag associations. Fails if the item does not exist.</summary>
        Task<AdminSaveResult> SetTags(SetTagsData data, CancellationToken cancellationToken = default);
    }
}
