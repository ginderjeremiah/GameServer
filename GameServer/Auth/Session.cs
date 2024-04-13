using DataAccess;
using DataAccess.Entities.SessionStore;
using GameLibrary;
using GameServer.Models.Attributes;
using GameServer.Models.Enemies;
using GameServer.Models.InventoryItems;
using GameServer.Models.Player;

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

        public string SessionId => _sessionData.SessionId;
        public DateTime LastUsed { get => _sessionData.LastUsed; private set => _sessionData.LastUsed = SetSessionDirty(value); }
        public DateTime EnemyCooldown { get => _sessionData.EnemyCooldown; set => _sessionData.EnemyCooldown = SetSessionDirty(value); }
        public DateTime EarliestDefeat { get => _sessionData.EarliestDefeat; private set => _sessionData.EarliestDefeat = SetSessionDirty(value); }
        public bool Victory { get => _sessionData.Victory; private set => _sessionData.Victory = SetSessionDirty(value); }
        public int CurrentZone { get => _sessionData.CurrentZone; set => _sessionData.CurrentZone = SetSessionDirty(value); }
        public SessionInventory InventoryData { get; }
        public SessionPlayer Player { get; }
        public PlayerData PlayerData => new(Player, InventoryData);

        public Session(SessionData sessionData, IRepositoryManager repos)
        {
            _sessionData = sessionData;
            Player = new SessionPlayer(sessionData);
            InventoryData = new SessionInventory(sessionData.InventoryItems);
            _repos = repos;
        }

        public void SetActiveEnemy(EnemyInstance activeEnemy, DateTime earliestDefeat, bool victory)
        {
            _repos.SessionStore.SetActiveEnemyHash(_sessionData, activeEnemy.Hash());
            EarliestDefeat = earliestDefeat;
            Victory = victory;
        }

        public bool DefeatEnemy(EnemyInstance defeatedEnemy)
        {
            if (Victory && EarliestDefeat <= DateTime.UtcNow)
            {
                var activeEnemyHash = _repos.SessionStore.GetAndDeleteActiveEnemyHash(_sessionData);
                return defeatedEnemy.Hash() == activeEnemyHash;
            }

            return false;
        }

        public string GetNewToken()
        {
            var tokenData = $"{SessionId.ToBase64()}.{DateTime.UtcNow.Add(Constants.TokenLifetime).Ticks.ToBase64()}";
            return $"{tokenData}.{tokenData.Hash(Player.Salt.ToString(), 1).ToBase64()}";
        }

        public DefeatRewards GrantRewards(EnemyInstance enemy)
        {

            var expReward = GetExpReward(enemy);
            Player.Exp += expReward;
            if (Player.Exp > Player.Level * 100)
            {
                Player.Exp -= Player.Level * 100;
                Player.Level++;
                Player.StatPointsGained += 6;
            }
            _playerDirty = true;

            var freeSlots = InventoryData.GetFreeSlotIds();
            var drops = _repos.InventoryItems.RollDrops(enemy.EnemyId, CurrentZone, freeSlots.Count);

            for (int i = 0; i < drops.Count; i++)
            {
                var d = drops[i];
                var slotId = freeSlots[i];
                d.PlayerId = Player.PlayerId;
                d.SlotId = slotId;
                _repos.InventoryItems.AddInventoryItem(d);
                InventoryData.Inventory[slotId] = new InventoryItem(d);
                _sessionData.InventoryItems.Add(d);
                _sessionDirty = true;
            }

            return new DefeatRewards
            {
                Drops = drops.Select(d => new InventoryItem(d)).ToList(),
                ExpReward = expReward,
            };
        }

        public void UpdatePlayerAttributes(List<AttributeUpdate> changedAttributes)
        {
            if (Player.UpdateAttributes(changedAttributes))
                _playerDirty = true;
        }

        public bool TrySetSelectedSkills(List<int> skills)
        {
            //TODO: validate skills
            //Player.SelectedSkills = skills;
            //_skillsDirty = true;
            return true;
        }

        public bool TryUpdateInventoryItems(List<InventoryUpdate> inventoryUpdates)
        {
            var validUpdate = InventoryData.TrySetNewInventoryList(inventoryUpdates);
            _inventoryDirty = validUpdate || _inventoryDirty;
            return validUpdate;
        }

        private int GetExpReward(EnemyInstance enemy)
        {
            var enemyAttTotal = enemy.Attributes.Sum(att => att.Amount);
            var playerAttTotal = Player.Attributes.Sum(att => att.Amount);
            var attRatio = (double)(enemyAttTotal / playerAttTotal);
            double expMulti = attRatio is < 0.8 or > 1.2 ? Math.Pow(attRatio, 2) : 1.0;
            return (int)Math.Floor((double)enemyAttTotal * expMulti);
        }

        public void Save()
        {
            if (_sessionDirty || _playerDirty || _skillsDirty || _inventoryDirty)
            {
                _repos.SessionStore.Update(_sessionData, _playerDirty, _skillsDirty, _inventoryDirty);
                _sessionDirty = false;
                _playerDirty = false;
                _skillsDirty = false;
                _inventoryDirty = false;
            }
        }

        private T SetSessionDirty<T>(T data)
        {
            _sessionDirty = true;
            return data;
        }
    }
}
