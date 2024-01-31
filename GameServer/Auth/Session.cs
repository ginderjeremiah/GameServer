using DataAccess;
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
        private bool _sessionDirty = false;
        private bool _skillsDirty = false;
        private bool _playerDirty = false;
        private bool _inventoryDirty = false;
        private bool _equippedDirty = false;

        //TODO: Move to Constants static class
        public static TimeSpan TokenLifetime { get; } = TimeSpan.FromDays(1);
        public string SessionId => _sessionData.SessionId;
        public DateTime LastUsed { get => _sessionData.LastUsed; private set => _sessionData.LastUsed = SetSessionDirty(value); }
        public DateTime EnemyCooldown { get => _sessionData.EnemyCooldown; set => _sessionData.EnemyCooldown = SetSessionDirty(value); }
        public string ActiveEnemyHash { get => _sessionData.ActiveEnemyHash; private set => _sessionData.ActiveEnemyHash = SetSessionDirty(value); }
        public DateTime EarliestDefeat { get => _sessionData.EarliestDefeat; private set => _sessionData.EarliestDefeat = SetSessionDirty(value); }
        public bool Victory { get => _sessionData.Victory; private set => _sessionData.Victory = SetSessionDirty(value); }
        public int CurrentZone { get => _sessionData.CurrentZone; set => _sessionData.CurrentZone = SetSessionDirty(value); }
        public SessionInventory InventoryData { get; }
        public SessionPlayer PlayerData { get; }

        public Session(SessionData sessionData, IRepositoryManager repos)
        {
            _sessionData = sessionData;
            PlayerData = new SessionPlayer(sessionData);
            InventoryData = new SessionInventory(sessionData.InventoryItems);
            _repos = repos;
        }

        public void SetActiveEnemy(EnemyInstance activeEnemy, DateTime earliestDefeat, bool victory)
        {
            ActiveEnemyHash = activeEnemy.Hash();
            EarliestDefeat = earliestDefeat;
            Victory = victory;
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
        }

        public string GetNewToken()
        {
            var tokenData = $"{SessionId.ToBase64()}.{DateTime.UtcNow.Add(TokenLifetime).Ticks.ToBase64()}";
            return $"{tokenData}.{tokenData.Hash(PlayerData.Salt.ToString(), 1).ToBase64()}";
        }

        public DefeatRewards GrantRewards(EnemyInstance enemy)
        {

            var expReward = GetExpReward(enemy);
            PlayerData.Exp += expReward;
            if (PlayerData.Exp > PlayerData.Level * 100)
            {
                PlayerData.Exp -= PlayerData.Level * 100;
                PlayerData.Level++;
                PlayerData.StatPointsGained += 6;
            }
            _playerDirty = true;

            var freeSlots = InventoryData.GetFreeSlotIds();
            var drops = _repos.InventoryItems.RollDrops(enemy.EnemyId, CurrentZone, freeSlots.Count);

            for (int i = 0; i < drops.Count; i++)
            {
                var d = drops[i];
                var slotId = freeSlots[i];
                d.PlayerId = PlayerData.PlayerId;
                d.SlotId = slotId;
                _repos.InventoryItems.AddInventoryItem(d);
                InventoryData.Inventory[slotId] = d;
                _sessionData.InventoryItems.Add(d);
                _sessionDirty = true; //TODO: Make inventory item adds (and dels) update to db async?
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
                _playerDirty = true;
        }

        public bool TrySetSelectedSkills(List<int> skills)
        {
            //TODO: validate skills
            PlayerData.SelectedSkills = skills;
            _skillsDirty = true;
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

            var validUpdate = InventoryData.TrySetNewEquippedList(currentItems);

            _equippedDirty = validUpdate || _equippedDirty;

            return validUpdate;
        }

        public bool TryUpdateInventoryItems(List<InventoryUpdate> inventoryUpdates)
        {
            var currentItems = inventoryUpdates.Select(invUp => (invUp, _sessionData.InventoryItems.FirstOrDefault(invItem => invItem.InventoryItemId == invUp.InventoryItemId)));

            if (currentItems.Any(item => item.Item2 is null))
            {
                return false;
            }

            var validUpdate = InventoryData.TrySetNewInventoryList(currentItems);

            _inventoryDirty = validUpdate || _inventoryDirty;

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
                expMulti = levelDifference < 0 ? 2 - bonus : bonus;
            }
            return (int)Math.Floor(enemy.Stats.Total * expMulti);
        }

        public void Save()
        {
            if (_sessionDirty || _playerDirty || _skillsDirty || _inventoryDirty || _equippedDirty)
            {
                _repos.SessionStore.Update(_sessionData, _playerDirty, _skillsDirty, _inventoryDirty, _equippedDirty);
                _sessionDirty = false;
                _playerDirty = false;
                _skillsDirty = false;
                _inventoryDirty = false;
                _equippedDirty = false;
            }
        }

        private T SetSessionDirty<T>(T data)
        {
            _sessionDirty = true;
            return data;
        }
    }
}
