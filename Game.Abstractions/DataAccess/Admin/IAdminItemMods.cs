using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;

namespace Game.Abstractions.DataAccess.Admin
{
    /// <summary>
    /// Content Authoring persistence for item mods and their related collections (attributes, tags).
    /// Encapsulates the EF specifics behind an entity-free admin contract surface.
    /// </summary>
    public interface IAdminItemMods
    {
        /// <summary>Applies an identity-level Add/Edit/Delete change set to the item-mod catalogue.
        /// Fails (applying nothing) if an edit targets an item mod that does not exist.</summary>
        AdminSaveResult SaveItemMods(IReadOnlyList<Change<ItemMod>> changes);

        /// <summary>Applies a change set to an item mod's attributes. Fails if the item mod does not exist.</summary>
        AdminSaveResult SetAttributes(AddEditAttributesData data);

        /// <summary>Replaces an item mod's tag associations. Fails if the item mod does not exist.</summary>
        Task<AdminSaveResult> SetTags(SetTagsData data);
    }
}
