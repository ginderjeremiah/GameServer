using Game.Abstractions.Contracts.Identity;
using Game.Abstractions.DataAccess;
using Game.Core;

namespace Game.DataAccess.Repositories
{
    internal class Roles : IRoles
    {
        // Roles are intrinsic reference data: the ERole enum is the source of truth and the database
        // is only seeded from it (see GameContext). There is therefore nothing to query — the full
        // set can be constructed in memory directly from the enum.
        public List<Role> GetRoles()
        {
            return Enum.GetValues<ERole>()
                .Select(role => new Role
                {
                    Id = (int)role,
                    Name = role.ToString(),
                })
                .ToList();
        }
    }
}
