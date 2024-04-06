using DataAccess.Models.PlayerAttributes;
using GameServer.Auth;

namespace GameServer.Models.Response
{
    public class PlayerData
    {
        public string UserName { get; set; }
        public string PlayerName { get; set; }
        public int Level { get; set; }
        public int Exp { get; set; }
        public List<PlayerAttribute> Attributes { get; set; }
        public List<int> SelectedSkills { get; set; }
        public int StatPointsGained { get; set; }
        public int StatPointsUsed { get; set; }

        public PlayerData(SessionPlayer sessionPlayer)
        {
            UserName = sessionPlayer.UserName;
            PlayerName = sessionPlayer.PlayerName;
            Level = sessionPlayer.Level;
            Exp = sessionPlayer.Exp;
            Attributes = sessionPlayer.Attributes;
            SelectedSkills = sessionPlayer.SelectedSkills;
            StatPointsGained = sessionPlayer.StatPointsGained;
            StatPointsUsed = sessionPlayer.StatPointsUsed;
        }
    }
}
