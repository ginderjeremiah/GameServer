using Game.Abstractions.Entities;

namespace Game.Abstractions.DataAccess
{
    public interface IRoles
    {
        /// <summary>
        /// Returns every access role that can be granted to a user.
        /// </summary>
        List<Role> GetRoles();
    }
}
