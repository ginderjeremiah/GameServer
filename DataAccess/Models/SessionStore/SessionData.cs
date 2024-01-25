using DataAccess.Models.InventoryItems;
using DataAccess.Models.Stats;

namespace DataAccess.Models.SessionStore
{
    public class SessionData
    {
        public int CurrentZone { get; set; }
        public string ActiveEnemyHash { get; set; }
        public DateTime EarliestDefeat { get; set; }
        public bool Victory { get; set; }
        public string SessionId { get; }
        public DateTime LastUsed { get; set; }
        public DateTime EnemyCooldown { get; set; }
        public List<InventoryItem> InventoryItems { get; set; }
        public Player.Player PlayerData { get; }
        public BaseStats Stats { get; set; }
        public List<int> SelectedSkills { get; set; }

        public SessionData(string id, Player.Player playerData, List<InventoryItem> inventory, BaseStats stats, List<int> selectedSkills, int currentZone = 1, string activeEnemyHash = "", DateTime? enemyCooldown = null, DateTime? earliestDefeat = null, bool victory = false)
        {
            SessionId = id;
            LastUsed = DateTime.UtcNow;
            CurrentZone = currentZone;
            PlayerData = playerData;
            InventoryItems = inventory.ToList();
            EnemyCooldown = enemyCooldown ?? DateTime.UnixEpoch;
            ActiveEnemyHash = activeEnemyHash;
            EarliestDefeat = earliestDefeat ?? DateTime.UnixEpoch;
            Victory = victory;
            Stats = stats;
            SelectedSkills = selectedSkills;
        }
    }
}
