using System.ComponentModel.DataAnnotations;

namespace Game.Api.Models.Users
{
    public class SetUserRolesData
    {
        [Range(1, int.MaxValue)]
        public int UserId { get; set; }

        // A non-null role set is required (an empty set revokes all roles); the upper bound is a generous
        // sanity cap that rejects absurd payloads before they reach the data layer.
        [Required]
        [MaxLength(50)]
        public required List<int> RoleIds { get; set; }
    }
}
