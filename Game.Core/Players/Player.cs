using Game.Core.Battle;
using Game.Core.Battle.Events;
using Game.Core.Enemies;
using Game.Core.Items;
using Game.Core.Players.Events;
using Game.Core.Players.Inventories;
using Game.Core.Proficiencies;
using Game.Core.Skills;

namespace Game.Core.Players
{
    /// <summary>
    /// Represents a player character in the game.
    /// </summary>
    public class Player : AggregateRoot
    {
        public required int Id { get; set; }

        /// <summary>
        /// The class chosen at character creation. Permanent (never changed — a new archetype means a new
        /// character) and the persisted seam the durable class effects build on: the level-scaled locked-base
        /// attribute fingerprint (#1223) and the signature passive (#1224) are assembled from it at battle time.
        /// </summary>
        public required int ClassId { get; set; }
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
        /// Whether the player's idle loop is auto-challenging the boss — of <see cref="CurrentZoneId"/>, the
        /// single zone the loop operates in (the boss farmed is always the current zone's boss, never a
        /// separate one). Persisted so the offline-rewards simulation can resume the correct loop at next
        /// login: idle-farming <see cref="CurrentZoneId"/> when <c>false</c>, boss-farming its boss when
        /// <c>true</c>. Synced from the frontend's live auto-fight state via <see cref="SetAutoChallengeBoss"/>.
        /// </summary>
        public required bool AutoChallengeBoss { get; set; }

        public required PlayerStatPoints StatPoints { get; set; }
        public required Inventory Inventory { get; set; }
        public required List<Skill> SelectedSkills { get; set; }
        public required List<Skill> Skills { get; set; }
        public required List<LogPreference> LogPreferences { get; set; }
        public required List<PlayerLesson> Lessons { get; set; }

        /// <summary>
        /// The RNG seed of the most recently durably-credited battle (win/loss/draw), an idempotency backstop
        /// for the reconnect race in <see cref="Game.Application.Services.BattleService"/>'s abandon path
        /// (#1874): a crash between a battle's durable credit and the session's awaited cache save can leave
        /// the stale session still showing that exact battle active, and the next reconnect's abandon would
        /// otherwise replay and re-credit it. Persisted alongside the other core fields so it survives the
        /// crash that creates the gap. Only the single latest seed is kept — at most one battle can be
        /// credited-but-not-yet-session-cleared for a player at a time, so there is nothing to retain beyond it.
        /// </summary>
        public required uint? LastCreditedBattleSeed { get; set; }

        public void ChangeZone(int zoneId)
        {
            CurrentZoneId = zoneId;
            RaiseCoreUpdated();
        }

        /// <summary>
        /// Persists the active idle-loop mode: <paramref name="enabled"/> enters boss mode (auto-challenging
        /// the current zone's dedicated boss), <c>false</c> returns the loop to idle-farming. Anti-cheat
        /// validation of the current zone (in circulation, unlocked, has a boss) is the caller's
        /// responsibility. A no-op (the mode already matches <paramref name="enabled"/>) raises no event and
        /// returns <c>false</c>; otherwise raises a <see cref="PlayerCoreUpdatedEvent"/> so the change rides
        /// the existing write-behind save and returns <c>true</c>.
        /// </summary>
        public bool SetAutoChallengeBoss(bool enabled)
        {
            if (AutoChallengeBoss == enabled)
            {
                return false;
            }

            AutoChallengeBoss = enabled;
            RaiseCoreUpdated();
            return true;
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
            ApplyExp(amount);
            RaiseCoreUpdated();
        }

        /// <summary>
        /// Awards experience for a batch of offline victories in one pass. Each reward is applied through the
        /// same per-grant clamp and level-up loop as <see cref="GrantExp"/> — so no single victory's reward is
        /// truncated by the clamp and levels accrue correctly as exp builds — but only a single
        /// <see cref="PlayerCoreUpdatedEvent"/> is raised at the end rather than one per victory. An offline
        /// window can span thousands of victories, and the write-behind persists the final absolute core state
        /// regardless, so one core update is both correct and far cheaper than flooding the queue with one per
        /// kill (spike #879 decision 6). Per-level <see cref="PlayerLeveledUpEvent"/>s are still raised (they
        /// are in-process only, with no persistence handler, so the burst is harmless).
        /// </summary>
        public void GrantOfflineExp(IEnumerable<int> victoryExpRewards)
        {
            foreach (var reward in victoryExpRewards)
            {
                ApplyExp(reward);
            }

            RaiseCoreUpdated();
        }

        /// <summary>
        /// Applies a single experience grant: clamps it to <c>[0, MaxExpPerGrant]</c> and runs the level-up
        /// loop (<see cref="ExpProgression.ApplyExp"/>, shared with the offline simulator's in-loop level
        /// tracking, #1601), raising one <see cref="PlayerLeveledUpEvent"/> per level gained. Shared by the
        /// live <see cref="GrantExp"/> (one grant, one core update) and the batched
        /// <see cref="GrantOfflineExp"/> (many grants, one core update) so the clamp and level-up arithmetic
        /// cannot drift between the two.
        /// </summary>
        private void ApplyExp(int amount)
        {
            // Level is required and ≥ 1 on every construction path, so a Level of 0 here is a corrupt
            // aggregate, not a state to silently tolerate. Fail loudly (matching the fail-fast policy in
            // e.g. BattleSnapshot.FromPlayer) rather than papering over it with a Math.Max guard that would
            // spin the level-up loop on a zero threshold.
            if (Level < 1)
            {
                throw new InvalidOperationException(
                    $"Player {Id} has a corrupt Level of {Level}; expected at least 1.");
            }

            var startLevel = Level;
            var result = ExpProgression.ApplyExp(Level, Exp, amount);
            Level = result.Level;
            Exp = result.Exp;

            for (var levelsIn = 1; levelsIn <= result.LevelsGained; levelsIn++)
            {
                StatPoints.StatPointsGained += GameConstants.StatPointsPerLevel;
                RaiseEvent(new PlayerLeveledUpEvent(this, startLevel + levelsIn, StatPoints.StatPointsGained));
            }
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
            // A linear "already unlocked?" scan, unlike the deliberately O(1) UnlockItem/UnlockMod paths.
            // Skills grow far slower than items (and aren't read on the battle-start hot path the way the
            // unlocked-item set is), so the scan over the small skill count is acceptable here.
            if (!Skills.Any(s => s.Id == skill.Id))
            {
                Skills.Add(skill);
                RaiseEvent(new SkillUnlockedEvent(Id, skill.Id));
            }
        }

        /// <summary>
        /// Executes a skill-synthesis recipe (spike #1125): validates the player may forge it and, on success,
        /// unlocks the result skill. All validation is authoritative anti-cheat — a tampered client controls
        /// only the recipe id, so every fact is re-checked against the live recipe and player state:
        /// <list type="bullet">
        /// <item>the recipe must be live (a retired recipe is no longer offered);</item>
        /// <item>the player must own every input as an <em>unlocked</em> skill — innate item-granted skills are
        /// derived at battle assembly and never live in <see cref="Skills"/>, so they are excluded by
        /// construction (no equip-to-synthesize-then-unequip; spike #1125 decision 6);</item>
        /// <item>every proficiency-level condition must be met, a missing proficiency counting as level 0
        /// (mirroring the gear proficiency gate).</item>
        /// </list>
        /// Synthesis is non-consumptive: inputs are never removed. The result-skill grant is idempotent via
        /// <see cref="UnlockSkill"/>, so re-synthesizing an already-owned result raises no second event.
        /// Returns whether the synthesis was permitted; a rejection mutates nothing and raises no event.
        /// <para>
        /// The recipe's result is guaranteed to be <see cref="ESkillAcquisition.Synthesis"/>-flagged by
        /// admin-authoring validation (the flag is authoring intent, not a player-tamperable surface), so it is
        /// not re-checked here — and the lean battle <see cref="Skill"/> deliberately carries no acquisition
        /// flag (docs/backend.md → skill acquisition flags).
        /// </para>
        /// </summary>
        public bool TrySynthesizeSkill(
            SkillRecipe recipe, Skill resultSkill, IReadOnlyDictionary<int, int> proficiencyLevels)
        {
            if (recipe.IsRetired)
            {
                return false;
            }

            var unlockedSkillIds = Skills.Select(s => s.Id).ToHashSet();
            if (!recipe.InputSkillIds.All(unlockedSkillIds.Contains))
            {
                return false;
            }

            foreach (var condition in recipe.Conditions)
            {
                var level = proficiencyLevels.TryGetValue(condition.ProficiencyId, out var l) ? l : 0;
                if (level < condition.MinLevel)
                {
                    return false;
                }
            }

            UnlockSkill(resultSkill);
            return true;
        }

        /// <summary>
        /// Completes a challenge for the player: unlocks each reward the challenge carries (item and/or
        /// mod — either of which may be absent) and, when <paramref name="notify"/> is <c>true</c>,
        /// raises a single <see cref="ChallengeCompletedEvent"/> describing the completion and what it
        /// unlocked. Consolidates the per-challenge reward orchestration in the domain so the application
        /// layer only has to resolve the reward reference data and hand it over.
        /// <para>
        /// Challenges no longer grant skills (skills come from the starter kit, item grants, and proficiency
        /// milestones — spike #982); the skill-unlock path now lives on the proficiency reward layer.
        /// </para>
        /// <para>
        /// <paramref name="notify"/> is the live client-push toggle. The live battle-completion path notifies
        /// (the push makes a just-unlocked reward usable without a refresh); the offline-rewards batch passes
        /// <c>false</c> to suppress the per-challenge push (spike #879 decision 7) — the welcome-back summary
        /// is the notification, and the client re-syncs its authoritative state on leaving the gate. The
        /// reward unlocks still raise their own (persistence-only) events either way, so the completion is
        /// durably recorded regardless of <paramref name="notify"/>.
        /// </para>
        /// </summary>
        public void CompleteChallenge(int challengeId, Item? rewardItem, int? rewardItemModId, bool notify = true)
        {
            if (rewardItem is not null)
            {
                UnlockItem(rewardItem);
            }
            if (rewardItemModId.HasValue)
            {
                UnlockMod(rewardItemModId.Value);
            }

            if (notify)
            {
                RaiseEvent(new ChallengeCompletedEvent(
                    Id, challengeId, rewardItem?.Id, rewardItemModId));
            }
        }

        /// <summary>
        /// Announces a won battle's proficiency-XP accrual for the live client push, carrying every
        /// proficiency the battle trained (XP gained, new level/XP, milestones crossed, reward skills) and any
        /// nodes it opened. The XP and skill grants themselves are persisted through their own paths; this
        /// event is notification-only, mirroring the challenge-completion push. The offline batch never calls
        /// this — its gains ride the welcome-back summary instead (spike #982 decision 9). A no-op when nothing
        /// was trained or opened.
        /// </summary>
        public void RaiseProficiencyXpGained(
            IReadOnlyList<ProficiencyXpResult> results, IReadOnlyList<ProficiencyOpened> opened)
        {
            if (results.Count == 0 && opened.Count == 0)
            {
                return;
            }

            RaiseEvent(new ProficiencyXpGainedEvent(Id, results, opened));
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

        public bool TryEquipItem(int itemId, EEquipmentSlot slot, IReadOnlyDictionary<int, int> proficiencyLevels)
        {
            if (!Inventory.TryEquipItem(itemId, slot, proficiencyLevels))
            {
                return false;
            }

            RaiseEvent(new ItemEquippedEvent(Id, itemId, (int)slot));
            return true;
        }

        public bool TryUnequipItem(EEquipmentSlot slot)
        {
            if (Inventory.TryUnequipItem(slot) is not int itemId)
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

        // Returns whether the preference's value actually changed, so callers (SaveLogPreferences) can skip
        // enqueuing a write-behind event and save for an unchanged toggle, matching the "no mutation → no
        // save" pattern every sibling command follows.
        public bool UpdateLogPreference(ELogType logType, bool enabled)
        {
            var pref = LogPreferences.FirstOrDefault(p => p.LogType == logType);
            if (pref is not null)
            {
                if (pref.Enabled == enabled)
                {
                    return false;
                }

                pref.Enabled = enabled;
            }
            else
            {
                LogPreferences.Add(new LogPreference { LogType = logType, Enabled = enabled });
            }

            RaiseEvent(new LogPreferenceChangedEvent(Id, logType, enabled));
            return true;
        }

        /// <summary>
        /// Records that a mechanic-anchored lesson's trigger fired client-side (spike #1392). Client-detected
        /// triggers are trusted — nothing is rewarded, so a dishonest client can only show itself tutorials
        /// early. A no-op when the lesson already has a row (already unlocked or read), mirroring
        /// <see cref="UnlockSkill"/>'s re-grant no-op. Returns whether the lesson was newly unlocked, so the
        /// caller can skip a redundant write-behind save.
        /// </summary>
        public bool UnlockLesson(int lessonId, DateTime timestamp)
        {
            if (Lessons.Any(l => l.LessonId == lessonId))
            {
                return false;
            }

            Lessons.Add(new PlayerLesson { LessonId = lessonId, UnlockedAt = timestamp });
            RaiseEvent(new LessonUnlockedEvent(Id, lessonId, timestamp));
            return true;
        }

        /// <summary>
        /// Marks a lesson's coach-mark tour as completed. A screen-anchored lesson plays immediately on first
        /// visit with no prior <see cref="UnlockLesson"/> call, so a locked lesson normalizes straight to read
        /// (backfilling <see cref="PlayerLesson.UnlockedAt"/> with this same timestamp) rather than rejecting;
        /// an already-read lesson (e.g. a Help-screen replay) is a no-op. Returns whether the lesson's state
        /// actually changed.
        /// </summary>
        public bool MarkLessonRead(int lessonId, DateTime timestamp)
        {
            var lesson = Lessons.FirstOrDefault(l => l.LessonId == lessonId);
            if (lesson is not null && lesson.ReadAt is not null)
            {
                return false;
            }

            if (lesson is null)
            {
                lesson = new PlayerLesson { LessonId = lessonId, UnlockedAt = timestamp };
                Lessons.Add(lesson);
            }

            lesson.ReadAt = timestamp;
            RaiseEvent(new LessonReadEvent(Id, lessonId, lesson.UnlockedAt, timestamp));
            return true;
        }

        // Records a non-victory (loss/draw) battle outcome. No combat ratings are threaded onto the event —
        // XP only accrues on a win — so a victory MUST go through RecordBattleVictory instead, which requires
        // them; that split (rather than a defaultable parameter) makes a victory caller forgetting the ratings
        // a compile error instead of a silent zero-XP accrual. notify mirrors CompleteChallenge's live-push
        // toggle: the offline/switch settlement of a stale battle has no socket to push to, so it passes false.
        // battleSeed is the credited battle's RNG seed (PlayerState.BattleSeed), recorded onto
        // LastCreditedBattleSeed (#1874) — null only for callers with no seed to thread (none today).
        public void RecordBattleCompleted(
            Enemy enemy, BattleResult result, bool isBossBattle, int zoneId, DateTime timestamp,
            uint? battleSeed = null, bool notify = true)
        {
            RecordBattleOutcome(enemy, result, isBossBattle, zoneId, timestamp, playerRating: 0, enemyRating: 0, battleSeed, notify);
        }

        // Records a victorious battle outcome, threading the combat ratings the win was rated against so the
        // progress handler can normalize proficiency accrual by max(playerRating, enemyRating) (spike #1526
        // Decision 5). Both current victory paths route through BattleService.RecordVictory, which supplies them.
        // notify mirrors RecordBattleCompleted's live-push toggle; battleSeed mirrors its idempotency marker.
        public void RecordBattleVictory(
            Enemy enemy, BattleResult result, bool isBossBattle, int zoneId, DateTime timestamp,
            double playerRating, double enemyRating, uint? battleSeed = null, bool notify = true)
        {
            RecordBattleOutcome(enemy, result, isBossBattle, zoneId, timestamp, playerRating, enemyRating, battleSeed, notify);
        }

        private void RecordBattleOutcome(
            Enemy enemy, BattleResult result, bool isBossBattle, int zoneId, DateTime timestamp,
            double playerRating, double enemyRating, uint? battleSeed, bool notify)
        {
            RaiseEvent(new BattleCompletedEvent(
                this, enemy, result.Victory, result.PlayerDied, result.TotalMs, result.Stats, isBossBattle, zoneId,
                playerRating, enemyRating, notify));

            // Idempotency backstop (#1874): remember this battle's seed so a stale-session replay of the exact
            // same fight (see LastCreditedBattleSeed) can be recognized as already-credited rather than paid
            // out a second time. Set unconditionally here; the caller (BattleService) is the sole gatekeeper
            // that decides whether to call this method at all for a given seed.
            LastCreditedBattleSeed = battleSeed;

            // Backstop mirroring the online auto-fight-off: a recorded dedicated-boss loss or draw drops the
            // persisted loop back to idle, so the offline sim doesn't resume boss-farming a loop the player has
            // actually fallen out of, even if a frontend mode sync was missed. Cleared before StampActivity so
            // the single core-updated event it raises carries the change (rather than a redundant second event).
            if (isBossBattle && !result.Victory)
            {
                AutoChallengeBoss = false;
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

        private void RaiseCoreUpdated()
        {
            RaiseEvent(new PlayerCoreUpdatedEvent(
                Id, Level, Exp, CurrentZoneId,
                StatPoints.StatPointsGained, StatPoints.StatPointsUsed, LastActivity, AutoChallengeBoss,
                LastCreditedBattleSeed));
        }
    }
}
