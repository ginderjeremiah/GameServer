using GameServer.Auth;
using GameServer.Models.Attributes;

namespace GameServer.Models.Player
{
    public class PlayerData : IModel
    {
        public string UserName { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public int Exp { get; set; }
        public List<BattlerAttribute> Attributes { get; set; }
        public List<int> SelectedSkills { get; set; }
        public int StatPointsGained { get; set; }
        public int StatPointsUsed { get; set; }

        public PlayerData(SessionPlayer sessionPlayer)
        {
            UserName = sessionPlayer.UserName;
            Name = sessionPlayer.PlayerName;
            Level = sessionPlayer.Level;
            Exp = sessionPlayer.Exp;
            Attributes = sessionPlayer.Attributes.Select(att => new BattlerAttribute(att)).ToList();
            SelectedSkills = sessionPlayer.SelectedSkills;
            StatPointsGained = sessionPlayer.StatPointsGained;
            StatPointsUsed = sessionPlayer.StatPointsUsed;
        }
    }
}
