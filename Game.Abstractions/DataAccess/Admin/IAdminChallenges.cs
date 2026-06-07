using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;

namespace Game.Abstractions.DataAccess.Admin
{
    /// <summary>
    /// Content Authoring persistence for challenges. A challenge carries no child relationships, so it
    /// has a single whole-record change set. Encapsulates the EF specifics behind an entity-free admin
    /// contract surface.
    /// </summary>
    public interface IAdminChallenges
    {
        /// <summary>Applies an identity-level Add/Edit/Delete change set to the challenge catalogue.</summary>
        void SaveChallenges(IReadOnlyList<Change<Challenge>> changes);
    }
}
