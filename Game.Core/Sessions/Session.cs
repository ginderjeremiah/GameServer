﻿using Game.Core.BattleSimulation;
using Game.Core.DataAccess;
using Game.Core.Entities;

namespace Game.Core.Sessions
{
    public class Session
    {
        private readonly SessionData _sessionData;
        private readonly Player _player;
        private readonly IRepositoryManager _repos;
        private bool _sessionDirty = false;
        private bool _skillsDirty = false;
        private bool _playerDirty = false;
        private bool _inventoryDirty = false;

        public string SessionId => _sessionData.Id;
        public DateTime LastUsed { get => _sessionData.LastUsed; private set => _sessionData.LastUsed = SetSessionDirty(value); }
        public DateTime EnemyCooldown { get => _sessionData.EnemyCooldown; set => _sessionData.EnemyCooldown = SetSessionDirty(value); }
        public DateTime EarliestDefeat { get => _sessionData.EarliestDefeat; private set => _sessionData.EarliestDefeat = SetSessionDirty(value); }
        public bool Victory { get => _sessionData.Victory; private set => _sessionData.Victory = SetSessionDirty(value); }
        public int CurrentZone { get => _sessionData.CurrentZone; set => _sessionData.CurrentZone = SetSessionDirty(value); }
        public SessionInventory InventoryData { get; }
        public SessionPlayer Player { get; }
        public IEnumerable<BattlerAttribute> BattlerAttributes => Player.Attributes.Select(att => new BattlerAttribute(att));

        public Session(SessionData sessionData, Player playerData, IRepositoryManager repos)
        {
            _sessionData = sessionData;
            _player = playerData;
            Player = new SessionPlayer(playerData);
            InventoryData = new SessionInventory(playerData.InventoryItems);
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
            if (Victory && EarliestDefeat - TimeSpan.FromMilliseconds(50) <= DateTime.UtcNow)
            {
                var activeEnemyHash = _repos.SessionStore.GetAndDeleteActiveEnemyHash(_sessionData);
                return defeatedEnemy.Hash() == activeEnemyHash;
            }

            return false;
        }

        public async Task<DefeatRewards> GrantRewards(EnemyInstance enemy)
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

            var freeSlots = InventoryData.GetFreeSlotNumbers();
            var drops = RollDrops(enemy.Id, CurrentZone, freeSlots.Count);

            for (int i = 0; i < drops.Count; i++)
            {
                var d = drops[i];
                var slotNumber = freeSlots[i];
                d.PlayerId = Player.Id;
                d.InventorySlotNumber = slotNumber;
                _repos.Insert(d);
                InventoryData.Inventory[slotNumber] = d;
                _player.InventoryItems.Add(d);
            }

            if (drops.Count > 0)
            {
                _inventoryDirty = true;
                await _repos.SaveChangesAsync();
            }

            return new DefeatRewards
            {
                Drops = [.. drops],
                ExpReward = expReward,
            };
        }

        public void UpdatePlayerAttributes(IEnumerable<IAttributeUpdate> changedAttributes)
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

        public bool TryUpdateInventoryItems(IEnumerable<IInventoryUpdate> inventoryUpdates)
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
            var expMulti = attRatio is < 0.8 or > 1.2 ? Math.Pow(attRatio, 2) : 1.0;
            return (int)Math.Floor((double)enemyAttTotal * expMulti);
        }

        public async Task Save()
        {
            if (_sessionDirty)
            {
                _repos.SessionStore.Update(_sessionData);
                _sessionDirty = false;
            }

            if (_playerDirty || _skillsDirty || _inventoryDirty)
            {
                await _repos.Players.SavePlayer(_player, _playerDirty, _inventoryDirty, _skillsDirty);
                _playerDirty = false;
                _inventoryDirty = false;
                _skillsDirty = false;
            }
        }

        public List<InventoryItem> RollDrops(int enemyId, int zoneId, int max)
        {
            var rng = new Random();
            var drops = new List<InventoryItem>();
            var enemy = _repos.Enemies.GetEnemy(enemyId);
            if (enemy is not null)
            {
                foreach (var drop in enemy.EnemyDrops.Where(d => (decimal)rng.NextSingle() < d.DropRate))
                {
                    if (drops.Count < max)
                    {
                        drops.Add(GetItemInstance(drop.ItemId, rng));
                    }
                }
            }

            var zone = _repos.Zones.GetZone(zoneId);
            if (zone is not null)
            {
                foreach (var drop in zone.ZoneDrops.Where(d => (decimal)rng.NextSingle() < d.DropRate))
                {
                    if (drops.Count < max)
                    {
                        drops.Add(GetItemInstance(drop.ItemId, rng));
                    }
                }
            }

            return drops;
        }

        public IEnumerable<BattlerAttribute> GetInventoryAttributes()
        {
            return InventoryData.Equipped
                .SelectNotNull(item => item)
                .SelectMany(item => _repos.Items.GetItem(item.ItemId)?.ItemAttributes?
                    .Select(att => new BattlerAttribute(att))
                    .Concat(item.InventoryItemMods
                        .SelectMany(mod => _repos.ItemMods.GetItemMod(mod.ItemModId)?.ItemModAttributes?.
                            Select(att => new BattlerAttribute(att)) ?? []
                        )
                    ) ?? []
                );
        }

        public IEnumerable<Skill> GetSelectedSkills()
        {
            return Player.SelectedSkills.SelectNotNull(s => _repos.Skills.GetSkill(s.SkillId));
        }

        private T SetSessionDirty<T>(T data)
        {
            _sessionDirty = true;
            return data;
        }

        private InventoryItem GetItemInstance(int itemId, Random rng)
        {
            var item = _repos.Items.GetItem(itemId);
            var allMods = _repos.ItemMods.All();
            var slots = item?.ItemModSlots ?? [];
            var itemMods = new List<int>();
            var inventoryItemMods = new List<InventoryItemMod>();

            foreach (var slot in slots.Where(s => (decimal)rng.NextSingle() < s.Probability))
            {
                int? modId = null;
                if (slot.GuaranteedItemModId is null)
                {
                    var mods = _repos.ItemMods.GetModsForItemByType(slot.ItemId);
                    if (mods.TryGetValue(slot.ItemModSlotTypeId, out var modsForSlot))
                    {
                        //TODO Add weights for item mods
                        var actualMods = modsForSlot.Where(mod => !itemMods.Contains(mod.Id)).ToList();
                        if (actualMods.Count > 0)
                        {
                            modId = actualMods[rng.Next(0, actualMods.Count - 1)].Id;
                        }
                    }
                }
                else
                {
                    modId = allMods[slot.GuaranteedItemModId.Value].Id;
                }

                if (modId is not null)
                {
                    itemMods.Add(modId.Value);
                    inventoryItemMods.Add(new InventoryItemMod
                    {
                        ItemModId = modId.Value,
                        ItemModSlotId = slot.Id,
                    });
                }
            }

            return new InventoryItem
            {
                ItemId = itemId,
                Rating = 0, //TODO: implement Rating calculation
                Equipped = false,
                InventoryItemMods = inventoryItemMods
            };
        }
    }
}
