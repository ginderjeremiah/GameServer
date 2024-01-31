using DataAccess.Models.InventoryItems;
using DataAccess.Models.Players;
using DataAccess.Models.Stats;

namespace DataAccess.Models.SessionStore
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
        public BaseStats Stats { get; set; }
        public List<int> SelectedSkills { get; set; }
    }
}
