using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Battle.Events;
using Game.Core.Enemies;
using Game.Core.Items;
using Game.Core.Players.Events;
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
            {
                return false;
            }

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
            while (Exp >= Level * GameConstants.ExpPerLevel)
            {
                Exp -= Level * GameConstants.ExpPerLevel;
                Level++;
                StatPoints.StatPointsGained += GameConstants.StatPointsPerLevel;
                RaiseEvent(new PlayerLeveledUpEvent(this, Level, StatPoints.StatPointsGained));
            }

            RaiseCoreUpdated();
        }

        /// <summary>
        /// Unlocks an item for the player and raises an <see cref="ItemUnlockedEvent"/>.
        /// </summary>
        public void UnlockItem(Item item)
        {
            Inventory.UnlockItem(item);
            RaiseEvent(new ItemUnlockedEvent(Id, item.Id));
        }

        /// <summary>
        /// Unlocks a modifier for the player and raises a <see cref="ModUnlockedEvent"/>.
        /// </summary>
        public void UnlockMod(int itemModId)
        {
            Inventory.UnlockMod(itemModId);
            RaiseEvent(new ModUnlockedEvent(Id, itemModId));
        }

        /// <summary>
        /// Unlocks a skill for the player and raises a <see cref="SkillUnlockedEvent"/>. The skill is
        /// added to <see cref="Skills"/> unselected — earning a skill does not equip it (the player
        /// chooses their loadout via the selection flow). Mirrors <see cref="UnlockItem"/>: the in-memory
        /// set is de-duplicated and the event is raised for the idempotent write-behind insert to apply.
        /// </summary>
        public void UnlockSkill(Skill skill)
        {
            if (!Skills.Any(s => s.Id == skill.Id))
            {
                Skills.Add(skill);
            }

            RaiseEvent(new SkillUnlockedEvent(Id, skill.Id));
        }

        /// <summary>
        /// Replaces the player's equipped skill loadout with <paramref name="orderedSkillIds"/> in the
        /// given order, handling select, deselect, and reorder through one atomic path. Enforces the
        /// loadout rules as anti-cheat: the set must contain no duplicates, fit within
        /// <see cref="GameConstants.MaxSelectedSkills"/>, and consist only of skills the player has already unlocked.
        /// On success the equipped set + order is replaced and a single
        /// <see cref="SelectedSkillsChangedEvent"/> is raised; any validation failure rejects the change
        /// (returns <c>false</c>, raising no event and leaving the loadout untouched).
        /// </summary>
        public bool TrySetSelectedSkills(IReadOnlyList<int> orderedSkillIds)
        {
            if (orderedSkillIds.Count > GameConstants.MaxSelectedSkills)
            {
                return false;
            }

            if (orderedSkillIds.Distinct().Count() != orderedSkillIds.Count)
            {
                return false;
            }

            var unlockedById = Skills.ToDictionary(s => s.Id);
            if (!orderedSkillIds.All(unlockedById.ContainsKey))
            {
                return false;
            }

            SelectedSkills = orderedSkillIds.Select(id => unlockedById[id]).ToList();
            RaiseEvent(new SelectedSkillsChangedEvent(Id, orderedSkillIds.ToList()));
            return true;
        }

        public bool TryEquipItem(int itemId, EEquipmentSlot slot)
        {
            if (!Inventory.TryEquipItem(itemId, slot))
            {
                return false;
            }

            RaiseEvent(new ItemEquippedEvent(Id, itemId, (int)slot));
            return true;
        }

        public bool TryUnequipItem(EEquipmentSlot slot)
        {
            var equipSlot = Inventory.EquipmentSlots.FirstOrDefault(s => s.Value == slot);
            if (equipSlot?.ItemId is null)
            {
                return false;
            }

            var itemId = equipSlot.ItemId.Value;
            if (!Inventory.TryUnequipItem(slot))
            {
                return false;
            }

            RaiseEvent(new ItemUnequippedEvent(Id, itemId));
            return true;
        }

        public bool TryApplyMod(int itemId, int itemModId, int itemModSlotId, ItemMod mod)
        {
            if (!Inventory.TryApplyMod(itemId, itemModId, itemModSlotId, mod))
            {
                return false;
            }

            RaiseEvent(new ModAppliedEvent(Id, itemId, itemModSlotId, itemModId));
            return true;
        }

        public bool TryRemoveMod(int itemId, int itemModSlotId)
        {
            if (!Inventory.TryRemoveMod(itemId, itemModSlotId))
            {
                return false;
            }

            RaiseEvent(new ModRemovedEvent(Id, itemId, itemModSlotId));
            return true;
        }

        public bool TrySetFavorite(int itemId, bool favorite)
        {
            if (!Inventory.TrySetFavorite(itemId, favorite))
            {
                return false;
            }

            RaiseEvent(new ItemFavoriteChangedEvent(Id, itemId, favorite));
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

        public void RecordBattleCompleted(Enemy enemy, BattleResult result, bool isBossBattle, int zoneId)
        {
            RaiseEvent(new BattleCompletedEvent(
                this, enemy, result.Victory, result.PlayerDied, result.TotalMs, result.Stats, isBossBattle, zoneId));
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
