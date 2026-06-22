using Game.Abstractions.Contracts.Identity;
using Game.Abstractions.DataAccess;
using Game.Core;

namespace Game.DataAccess.Repositories
{
    internal class Roles : IRoles
    {
        // Roles are intrinsic reference data: the ERole enum is the source of truth and the database
        // is only seeded from it (see GameContext). There is therefore nothing to query — the full
        // set is built once from the enum (avoiding the per-call reflection-backed ToString()).
        private static readonly Role[] _roles = Enum.GetValues<ERole>()
            .Select(role => new Role
            {
                Id = (int)role,
                Name = role.ToString(),
            })
            .ToArray();

        // Returns a fresh list per call so a caller cannot mutate the shared precomputed set.
        public List<Role> GetRoles()
        {
            return [.. _roles];
        }
    }
}
