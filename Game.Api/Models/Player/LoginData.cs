using Game.Api.Models;

namespace Game.Api.Models.Player
{
    public class LoginData : IModel
    {
        public int CurrentZone { get; set; }
        public PlayerData PlayerData { get; set; }
    }
}
