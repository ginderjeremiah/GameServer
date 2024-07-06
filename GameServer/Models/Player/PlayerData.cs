using GameCore.Sessions;
using GameServer.Models.Attributes;
using GameServer.Models.InventoryItems;

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
        public InventoryData InventoryData { get; set; }

        public PlayerData() { }

        public PlayerData(SessionPlayer sessionPlayer, SessionInventory sessionInventory)
        {
            UserName = sessionPlayer.UserName;
            Name = sessionPlayer.Name;
            Level = sessionPlayer.Level;
            Exp = sessionPlayer.Exp;
            Attributes = sessionPlayer.Attributes.Select(att => new BattlerAttribute(att)).ToList();
            SelectedSkills = sessionPlayer.SelectedSkills.Select(s => s.SkillId).ToList();
            StatPointsGained = sessionPlayer.StatPointsGained;
            StatPointsUsed = sessionPlayer.StatPointsUsed;
            InventoryData = new InventoryData(sessionInventory);
        }
    }
}
