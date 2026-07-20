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
        /// their own identity and keep the hard-delete lifecycle. An Edit or Delete naming an id that no
        /// longer exists is rejected as a not-found failure rather than staged.</summary>
        Task<AdminSaveResult> SaveTags(IReadOnlyList<Change<Tag>> changes, CancellationToken cancellationToken = default);
    }
}
