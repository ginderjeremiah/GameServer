using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Events;
using Game.Core.Items;
using Game.Core.Players.Inventories;
using Game.Core.Skills;

namespace Game.Core.Players
{
    /// <summary>
    /// Represents a player character in the game.
    /// </summary>
    public class Player : AggregateRoot
    {
        public required int Id { get; set; }
        public required string Name { get; set; }
        public required int Level { get; set; }
        public required int Exp { get; set; }
        public required int CurrentZoneId { get; set; }
        public required PlayerStatPoints StatPoints { get; set; }
        public required Inventory Inventory { get; set; }
        public required List<Skill> SelectedSkills { get; set; }
        public required List<Skill> Skills { get; set; }
        public required List<LogPreference> LogPreferences { get; set; }

        public void ChangeZone(int zoneId)
        {
            CurrentZoneId = zoneId;
            RaiseCoreUpdated();
        }

        public bool TryUpdateAttributes(IEnumerable<IAttributeUpdate> changedAttributes)
        {
            if (!StatPoints.TryUpdateAttributes(changedAttributes))
                return false;

            RaiseCoreUpdated();
            RaiseEvent(new AttributeAllocationsChangedEvent(
                Id,
                StatPoints.StatAllocations.Select(a => new AttributeAllocationEntry(a.Attribute, a.Amount)).ToList()));
            return true;
        }

        /// <summary>
        /// Awards experience to the player. Raises <see cref="PlayerLeveledUpEvent"/> if a
        /// level-up occurs.
        /// </summary>
        public void GrantExp(int amount)
        {
            Exp += amount;
            while (Exp > Level * 100)
            {
                Exp -= Level * 100;
                Level++;
                StatPoints.StatPointsGained += 6;
                RaiseEvent(new PlayerLeveledUpEvent(Id, Level, StatPoints.StatPointsGained));
            }

            RaiseCoreUpdated();
        }

        /// <summary>
        /// Unlocks an item for the player and raises an <see cref="ItemUnlockedEvent"/>.
        /// </summary>
        public void UnlockItem(int itemId)
        {
            Inventory.UnlockItem(itemId);
            RaiseEvent(new ItemUnlockedEvent(Id, itemId));
        }

        /// <summary>
        /// Unlocks a modifier for the player and raises a <see cref="ModUnlockedEvent"/>.
        /// </summary>
        public void UnlockMod(int itemModId)
        {
            Inventory.UnlockMod(itemModId);
            RaiseEvent(new ModUnlockedEvent(Id, itemModId));
        }

        public bool TryEquipItem(int itemId, EEquipmentSlot slot)
        {
            if (!Inventory.TryEquipItem(itemId, slot))
                return false;

            RaiseEvent(new ItemEquippedEvent(Id, itemId, (int)slot));
            return true;
        }

        public bool TryUnequipItem(EEquipmentSlot slot)
        {
            var equipSlot = Inventory.EquipmentSlots.FirstOrDefault(s => s.Value == slot);
            if (equipSlot?.ItemId is null)
                return false;

            var itemId = equipSlot.ItemId.Value;
            if (!Inventory.TryUnequipItem(slot))
                return false;

            RaiseEvent(new ItemUnequippedEvent(Id, itemId));
            return true;
        }

        public bool TryApplyMod(int itemId, int itemModId, int itemModSlotId, ItemMod mod)
        {
            if (!Inventory.TryApplyMod(itemId, itemModId, itemModSlotId, mod))
                return false;

            RaiseEvent(new ModAppliedEvent(Id, itemId, itemModSlotId, itemModId));
            return true;
        }

        public bool TryRemoveMod(int itemId, int itemModSlotId)
        {
            if (!Inventory.TryRemoveMod(itemId, itemModSlotId))
                return false;

            RaiseEvent(new ModRemovedEvent(Id, itemId, itemModSlotId));
            return true;
        }

        public void UpdateLogPreference(ELogType logType, bool enabled)
        {
            var pref = LogPreferences.FirstOrDefault(p => p.LogType == logType);
            if (pref is not null)
            {
                pref.Enabled = enabled;
            }
            else
            {
                LogPreferences.Add(new LogPreference { LogType = logType, Enabled = enabled });
            }

            RaiseEvent(new LogPreferenceChangedEvent(Id, logType, enabled));
        }

        public void RecordEnemyDefeat(int enemyId, int expReward)
        {
            RaiseEvent(new EnemyDefeatedEvent(Id, enemyId, expReward));
        }

        public void RecordBattleCompleted(int enemyId, BattleResult result)
        {
            RaiseEvent(new BattleCompletedEvent(Id, enemyId, result.Victory, result.PlayerDied, result.TotalMs, result.Stats));
        }

        public IEnumerable<AttributeModifier> GetAllModifiers()
        {
            return StatPoints.ToAttributeModifiers()
                .Concat(Inventory.GetEquippedAttributeModifiers());
        }

        public AttributeCollection GetAttributes()
        {
            return new AttributeCollection(GetAllModifiers());
        }

        private void RaiseCoreUpdated()
        {
            RaiseEvent(new PlayerCoreUpdatedEvent(
                Id, Level, Exp, CurrentZoneId,
                StatPoints.StatPointsGained, StatPoints.StatPointsUsed));
        }
    }
}
