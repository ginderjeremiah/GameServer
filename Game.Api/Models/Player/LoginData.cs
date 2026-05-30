namespace Game.Api.Models.Player
{
    public class LoginData : IModel
    {
        public int CurrentZone { get; set; }
        public required PlayerData PlayerData { get; set; }
    }
}
