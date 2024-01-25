using GameServer.Auth;

namespace GameServer.Models.Response
{
    public class LoginResponse
    {
        public int CurrentZone { get; set; }
        public PlayerData PlayerData { get; set; }
    }
}
