using GameServer.Auth;

namespace GameServer.Models.Response
{
    public class LoginResponse
    {
        public int CurrentZone { get; set; }
        public SessionPlayer PlayerData { get; set; }
    }
}
