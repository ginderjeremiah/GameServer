using System.ComponentModel.DataAnnotations;

namespace Game.Api.Models.Users
{
    /// <summary>
    /// Identifies the target user for a single-user admin action (archive or ban).
    /// </summary>
    public class UserActionData
    {
        [Range(1, int.MaxValue)]
        public int UserId { get; set; }
    }
}
