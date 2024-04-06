namespace GameServer.Models.Player
{
    public class LoginData : IModel
    {
        public int CurrentZone { get; set; }
        public PlayerData PlayerData { get; set; }
    }
}
