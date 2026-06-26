using System.ComponentModel.DataAnnotations;

namespace Game.Api.Models.Auth
{
    /// <summary>
    /// Creates a new account from a username and password. The account is created with <b>no</b> character —
    /// the first character is created on the select screen through the class picker (issue #1256) — so this
    /// request carries no class. The server enforces username uniqueness (anti-cheat).
    /// </summary>
    public class CreateAccountRequest : IModel
    {
        [Length(1, 25)]
        public required string Username { get; set; }

        [Length(1, 256)]
        public required string Password { get; set; }
    }
}
