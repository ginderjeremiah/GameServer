using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;
// Disambiguate the reference-data Path from System.IO.Path (a global implicit using).
using Path = Game.Abstractions.Contracts.Path;

namespace Game.Abstractions.DataAccess.Admin
{
    /// <summary>
    /// Content Authoring persistence for paths (the proficiency sequences) and the skills that contribute to
    /// them. Encapsulates the EF specifics behind an entity-free admin contract surface.
    /// </summary>
    public interface IAdminPaths
    {
        /// <summary>Applies an identity-level Add/Edit change set to the path catalogue (retire-only — a
        /// Delete is rejected). Fails (applying nothing) if an edit targets a path that does not exist.</summary>
        AdminSaveResult SavePaths(IReadOnlyList<Change<Path>> changes);

        /// <summary>Reconciles the skills that contribute to a path. Fails if the path does not exist, a
        /// contributing skill does not exist, or a contribution's home tier is not a tier of the path.</summary>
        AdminSaveResult SetContributions(SetPathContributionsData data);
    }
}
