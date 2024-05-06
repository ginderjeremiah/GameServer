using GameCore.Entities.InventoryItems;
using GameCore.Entities.PlayerAttributes;
using GameCore.Entities.Players;

namespace GameCore.Entities.SessionStore
{
    public class SessionData
    {
        public int CurrentZone { get; set; }
        public DateTime EarliestDefeat { get; set; }
        public bool Victory { get; set; }
        public string SessionId { get; set; }
        public DateTime LastUsed { get; set; }
        public DateTime EnemyCooldown { get; set; }
        public List<InventoryItem> InventoryItems { get; set; }
        public Player PlayerData { get; set; }
        public List<PlayerAttribute> Attributes { get; set; }
        public List<PlayerSkill> PlayerSkills { get; set; }
    }
}
