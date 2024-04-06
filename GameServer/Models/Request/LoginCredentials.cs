namespace GameServer.Models.Request
{
    public class LoginCredentials : IModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
