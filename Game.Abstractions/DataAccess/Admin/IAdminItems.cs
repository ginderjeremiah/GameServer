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
        /// <summary>Applies an identity-level Add/Edit/Delete change set to the item catalogue.</summary>
        void SaveItems(IReadOnlyList<Change<Item>> changes);

        /// <summary>Applies a change set to an item's attributes. Returns <c>false</c> if the item does not exist.</summary>
        bool SetAttributes(AddEditAttributesData data);

        /// <summary>Applies an Add/Edit/Delete change set to an item's mod slots.</summary>
        void SaveModSlots(IReadOnlyList<Change<ItemModSlot>> changes);

        /// <summary>Replaces an item's tag associations. Returns <c>false</c> if the item does not exist.</summary>
        Task<bool> SetTags(SetTagsData data);
    }
}
