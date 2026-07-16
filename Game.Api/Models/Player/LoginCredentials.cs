using System.ComponentModel.DataAnnotations;
using Game.Core.Identity;

namespace Game.Api.Models.Player
{
    public class LoginCredentials : IModel
    {
        [Length(UsernamePolicy.MinLength, UsernamePolicy.MaxLength)]
        public required string Username { get; set; }

        [Length(1, 256)]
        public required string Password { get; set; }
    }
}
