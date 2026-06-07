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
        /// <summary>Applies an identity-level Add/Edit/Delete change set to the tag catalogue.</summary>
        void SaveTags(IReadOnlyList<Change<Tag>> changes);
    }
}
