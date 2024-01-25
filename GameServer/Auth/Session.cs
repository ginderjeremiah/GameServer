using DataAccess;
using DataAccess.Caches;
using DataAccess.Models.SessionStore;
using GameLibrary;
using GameServer.BattleSimulation;
using GameServer.Models.Common;
using GameServer.Models.Request;

namespace GameServer.Auth
{
    public class Session
    {
        private readonly SessionData _sessionData;
        private readonly IRepositoryManager _repos;

        public static TimeSpan TokenLifetime { get; } = TimeSpan.FromDays(1);
        public string SessionId => _sessionData.SessionId;
        public DateTime LastUsed { get => _sessionData.LastUsed; private set => _sessionData.LastUsed = value; }
        public DateTime EnemyCooldown { get => _sessionData.EnemyCooldown; set => _sessionData.EnemyCooldown = value; }
        public string ActiveEnemyHash { get => _sessionData.ActiveEnemyHash; private set => _sessionData.ActiveEnemyHash = value; }
        public DateTime EarliestDefeat { get => _sessionData.EarliestDefeat; private set => _sessionData.EarliestDefeat = value; }
        public bool Victory { get => _sessionData.Victory; private set => _sessionData.Victory = value; }
        public InventoryData Inventory { get; }
        public PlayerData PlayerData { get; }
        public int CurrentZone
        {
            get => _sessionData.CurrentZone;
            set
            {
                _sessionData.CurrentZone = value;
                SaveSession();
            }
        }

        public Session(SessionData sessionData, IRepositoryManager repos)
        {
            _sessionData = sessionData;
            PlayerData = new PlayerData(sessionData);
            Inventory = new InventoryData(sessionData.InventoryItems);
            _repos = repos;
        }

        public void SetActiveEnemy(EnemyInstance activeEnemy, DateTime earliestDefeat, bool victory)
        {
            ActiveEnemyHash = activeEnemy.Hash();
            EarliestDefeat = earliestDefeat;
            Victory = victory;
            SaveSession();
        }

        public bool ValidEnemyDefeat(EnemyInstance defeatedEnemy)
        {
            return Victory && EarliestDefeat <= DateTime.UtcNow && defeatedEnemy.Hash() == ActiveEnemyHash;
        }

        public void ResetActiveEnemy()
        {
            ActiveEnemyHash = "";
            EarliestDefeat = DateTime.UnixEpoch;
            Victory = false;
            SaveSession();
        }

        public string GetNewToken()
        {
            var tokenData = $"{SessionId.ToBase64()}.{DateTime.UtcNow.Add(TokenLifetime).Ticks.ToBase64()}";
            return $"{tokenData}.{tokenData.Hash(PlayerData.Salt.ToString(), 1).ToBase64()}";
        }

        public DefeatRewards GrantRewards(EnemyInstance enemy, ILootCache lootCache)
        {

            var expReward = GetExpReward(enemy);
            PlayerData.Exp += expReward;
            if (PlayerData.Exp > PlayerData.Level * 100)
            {
                PlayerData.Exp -= PlayerData.Level * 100;
                PlayerData.Level++;
                PlayerData.StatPointsGained += 6;
            }
            SavePlayerData();

            var freeSlots = Inventory.GetFreeSlotIds();
            var drops = lootCache.RollDrops(enemy.EnemyId, CurrentZone, freeSlots.Count);

            for (int i = 0; i < drops.Count; i++)
            {
                var d = drops[i];
                var slotId = freeSlots[i];
                d.PlayerId = PlayerData.PlayerId;
                d.SlotId = slotId;
                _repos.InventoryItems.AddInventoryItem(d);
                Inventory.Inventory[slotId] = d;
                _sessionData.InventoryItems.Add(d);
            }

            return new DefeatRewards
            {
                Drops = drops,
                ExpReward = expReward,
            };
        }

        public void UpdatePlayerStats(BattleBaseStats changedStats)
        {
            if (PlayerData.ChangeStats(changedStats))
                SavePlayerData();
        }

        public bool TrySetSelectedSkills(List<int> skills)
        {
            //TODO: validate skills
            PlayerData.SelectedSkills = skills;
            SaveSkills();
            return true;
        }

#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types. --- Non-nulls are guaranteed by code but VS doesn't recognize
        public bool TryUpdateEquippedItems(List<InventoryUpdate> inventoryUpdates)
        {
            var currentItems = inventoryUpdates.Select(invUp => (invUp, _sessionData.InventoryItems.FirstOrDefault(invItem => invItem.InventoryItemId == invUp.InventoryItemId)));

            if (currentItems.Any(item => item.Item2 is null))
            {
                return false;
            }

            var validUpdate = Inventory.TrySetNewEquippedList(currentItems);

            if (validUpdate)
            {
                _repos.InventoryItems.UpdateEquippedItemSlots(PlayerData.PlayerId, Inventory.Equipped.Where(i => i is not null));
            }

            return validUpdate;
        }

        public bool TryUpdateInventoryItems(List<InventoryUpdate> inventoryUpdates)
        {
            var currentItems = inventoryUpdates.Select(invUp => (invUp, _sessionData.InventoryItems.FirstOrDefault(invItem => invItem.InventoryItemId == invUp.InventoryItemId)));

            if (currentItems.Any(item => item.Item2 is null))
            {
                return false;
            }

            var validUpdate = Inventory.TrySetNewInventoryList(currentItems);

            if (validUpdate)
            {
                _repos.InventoryItems.UpdateInventoryItemSlots(PlayerData.PlayerId, Inventory.Inventory.Where(i => i is not null));
            }

            return validUpdate;
        }
#pragma warning restore CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types. --- Non-nulls are guaranteed by code but VS doesn't recognize

        private int GetExpReward(EnemyInstance enemy)
        {
            var levelDifference = PlayerData.Level - enemy.EnemyLevel;
            double expMulti = 1;
            if (levelDifference is < (-2) or > 2)
            {
                var bonus = 4 / Math.Pow(levelDifference, 2);
                expMulti = levelDifference > 0 ? 2 - bonus : bonus;
            }
            return (int)Math.Floor(enemy.Stats.Total * expMulti);
        }

        private void SaveSession()
        {
            _repos.SessionStore.SaveSession(_sessionData);
        }

        private void SavePlayerData()
        {
            _repos.SessionStore.SavePlayer(_sessionData);
        }

        private void SaveSkills()
        {
            _repos.SessionStore.SaveSkills(_sessionData);
        }
    }
}
