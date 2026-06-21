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

        /// <summary>
        /// When the player was last active, the anchor the offline-rewards flow reads to compute away time
        /// (<c>now − LastActivity</c>) server-side. Stamped on battle completion (the "last enemy defeated"
        /// anchor) via <see cref="StampActivity"/> so it is current at any disconnect.
        /// </summary>
        public required DateTime LastActivity { get; set; }

        /// <summary>
        /// The zone whose dedicated boss the player's idle loop is auto-challenging, or <c>null</c> when the
        /// loop is idle-farming. Persisted alongside <see cref="CurrentZoneId"/> (which carries the idle
        /// location) so the offline-rewards simulation can resume the correct loop at next login: idle when
        /// null, boss-farming this zone otherwise. Synced from the frontend's live auto-fight state via
        /// <see cref="SetAutoChallengeBoss"/>.
        /// </summary>
        public required int? AutoChallengeBossZoneId { get; set; }

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

        /// <summary>
        /// Persists the active idle-loop mode: a non-null <paramref name="bossZoneId"/> enters boss mode
        /// (auto-challenging that zone's dedicated boss), <c>null</c> returns the loop to idle-farming.
        /// Anti-cheat validation of the zone (exists, in circulation, unlocked, has a boss) is the caller's
        /// responsibility. Raises a <see cref="PlayerCoreUpdatedEvent"/> so the change rides the existing
        /// write-behind save.
        /// </summary>
        public void SetAutoChallengeBoss(int? bossZoneId)
        {
            AutoChallengeBossZoneId = bossZoneId;
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
        /// Awards experience to the player. The grant is clamped to <c>[0, MaxExpPerGrant]</c> so a
        /// tampered/replayed value can't drive an unbounded level-up loop on the serialized per-player
        /// command path; legitimate per-battle exp is already far below that ceiling. Raises one
        /// <see cref="PlayerLeveledUpEvent"/> per level gained, so the per-level event burst is bounded
        /// along with the loop.
        /// </summary>
        public void GrantExp(int amount)
        {
            Exp += Math.Clamp(amount, 0, ServerGameConstants.MaxExpPerGrant);
            // Guard the threshold against a non-positive level so a pre-initialized Level of 0 can't make
            // the threshold 0 and spin the loop.
            while (Exp >= Math.Max(1, Level) * GameConstants.ExpPerLevel)
            {
                Exp -= Math.Max(1, Level) * GameConstants.ExpPerLevel;
                Level++;
                StatPoints.StatPointsGained += GameConstants.StatPointsPerLevel;
                RaiseEvent(new PlayerLeveledUpEvent(this, Level, StatPoints.StatPointsGained));
            }

            RaiseCoreUpdated();
        }

        /// <summary>
        /// Unlocks an item for the player and raises an <see cref="ItemUnlockedEvent"/> only when the item
        /// was newly unlocked — re-granting an already-owned item is a no-op (no redundant write-behind or
        /// client "unlocked!" notification).
        /// </summary>
        public void UnlockItem(Item item)
        {
            if (Inventory.UnlockItem(item))
            {
                RaiseEvent(new ItemUnlockedEvent(Id, item.Id));
            }
        }

        /// <summary>
        /// Unlocks a modifier for the player and raises a <see cref="ModUnlockedEvent"/> only when the
        /// modifier was newly unlocked — re-granting an already-owned modifier is a no-op.
        /// </summary>
        public void UnlockMod(int itemModId)
        {
            if (Inventory.UnlockMod(itemModId))
            {
                RaiseEvent(new ModUnlockedEvent(Id, itemModId));
            }
        }

        /// <summary>
        /// Unlocks a skill for the player and raises a <see cref="SkillUnlockedEvent"/> only when the skill
        /// was newly unlocked. The skill is added to <see cref="Skills"/> unselected — earning a skill does
        /// not equip it (the player chooses their loadout via the selection flow). Mirrors
        /// <see cref="UnlockItem"/>: re-granting an already-owned skill is a no-op.
        /// </summary>
        public void UnlockSkill(Skill skill)
        {
            if (!Skills.Any(s => s.Id == skill.Id))
            {
                Skills.Add(skill);
                RaiseEvent(new SkillUnlockedEvent(Id, skill.Id));
            }
        }

        /// <summary>
        /// Completes a challenge for the player: unlocks each reward the challenge carries (item, mod,
        /// and/or skill — any of which may be absent) and raises a single <see cref="ChallengeCompletedEvent"/>
        /// describing the completion and what it unlocked. Consolidates the per-challenge reward
        /// orchestration in the domain so the application layer only has to resolve the reward reference
        /// data and hand it over.
        /// </summary>
        public void CompleteChallenge(int challengeId, Item? rewardItem, int? rewardItemModId, Skill? rewardSkill)
        {
            if (rewardItem is not null)
            {
                UnlockItem(rewardItem);
            }
            if (rewardItemModId.HasValue)
            {
                UnlockMod(rewardItemModId.Value);
            }
            if (rewardSkill is not null)
            {
                UnlockSkill(rewardSkill);
            }

            RaiseEvent(new ChallengeCompletedEvent(
                Id, challengeId, rewardItem?.Id, rewardItemModId, rewardSkill?.Id));
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

            if (HasDuplicate(orderedSkillIds))
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

        /// <summary>
        /// Returns whether <paramref name="ids"/> contains a duplicate. A nested scan is allocation-free
        /// and clearer than a HashSet for the loadout's tiny size (≤ <see cref="GameConstants.MaxSelectedSkills"/>).
        /// </summary>
        private static bool HasDuplicate(IReadOnlyList<int> ids)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                for (var j = i + 1; j < ids.Count; j++)
                {
                    if (ids[i] == ids[j])
                    {
                        return true;
                    }
                }
            }

            return false;
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

        public void RecordBattleCompleted(Enemy enemy, BattleResult result, bool isBossBattle, int zoneId, DateTime timestamp)
        {
            RaiseEvent(new BattleCompletedEvent(
                this, enemy, result.Victory, result.PlayerDied, result.TotalMs, result.Stats, isBossBattle, zoneId));

            // Backstop mirroring the online auto-fight-off: a recorded dedicated-boss loss or draw drops the
            // persisted loop back to idle, so the offline sim doesn't resume boss-farming a loop the player has
            // actually fallen out of, even if a frontend mode sync was missed. Cleared before StampActivity so
            // the single core-updated event it raises carries the change (rather than a redundant second event).
            if (isBossBattle && !result.Victory)
            {
                AutoChallengeBossZoneId = null;
            }

            StampActivity(timestamp);
        }

        /// <summary>
        /// Records that the player was active at <paramref name="timestamp"/>, advancing the away-time anchor
        /// the offline-rewards flow reads. Stamped on every battle completion (so a loss — which raises no other
        /// core update — still persists the anchor) and reset to "now" when offline progress is claimed at login,
        /// so the next away period starts fresh and a re-claim is a no-op. Raises a
        /// <see cref="PlayerCoreUpdatedEvent"/> so the change rides the existing write-behind save.
        /// </summary>
        public void StampActivity(DateTime timestamp)
        {
            LastActivity = timestamp;
            RaiseCoreUpdated();
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
                StatPoints.StatPointsGained, StatPoints.StatPointsUsed, LastActivity, AutoChallengeBossZoneId));
        }
    }
}
