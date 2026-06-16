using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;

namespace Game.Abstractions.DataAccess.Admin
{
    /// <summary>
    /// Content Authoring persistence for tags. Encapsulates the EF specifics behind an entity-free
    /// admin contract surface.
    /// </summary>
    public interface IAdminTags
    {
        /// <summary>Applies an identity-level Add/Edit/Delete change set to the tag catalogue. Tags carry
        /// their own identity and keep the hard-delete lifecycle, so this never rejects — it succeeds to
        /// share the unified <see cref="AdminSaveResult"/> contract every admin write reports through.</summary>
        AdminSaveResult SaveTags(IReadOnlyList<Change<Tag>> changes);
    }
}
