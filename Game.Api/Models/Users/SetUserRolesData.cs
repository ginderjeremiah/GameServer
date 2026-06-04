namespace Game.Api.Models.Users
{
    public class SetUserRolesData
    {
        public int UserId { get; set; }

        public required List<int> RoleIds { get; set; }
    }
}
