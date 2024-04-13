using DataAccess.Entities.InventoryItems;
using DataAccess.Entities.PlayerAttributes;
using DataAccess.Entities.Players;

namespace DataAccess.Entities.SessionStore
{
    public class SessionData
    {
        public int CurrentZone { get; set; }
        public string ActiveEnemyHash { get; set; }
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
