using RoleEntity = Game.Abstractions.Entities.Role;

namespace Game.Api.Models.Users
{
    public class Role : IModelFromSource<Role, RoleEntity>
    {
        public int Id { get; set; }
        public required string Name { get; set; }

        public static Role FromSource(RoleEntity role)
        {
            return new Role
            {
                Id = role.Id,
                Name = role.Name,
            };
        }
    }
}
