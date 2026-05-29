namespace Game.Api.Models.Player
{
    public class LoginCredentials : IModel
    {
        public required string Username { get; set; }
        public required string Password { get; set; }
    }
}
