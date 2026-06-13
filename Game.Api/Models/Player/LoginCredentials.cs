using System.ComponentModel.DataAnnotations;

namespace Game.Api.Models.Player
{
    public class LoginCredentials : IModel
    {
        [Length(1, 25)]
        public required string Username { get; set; }

        [Length(1, 256)]
        public required string Password { get; set; }
    }
}
