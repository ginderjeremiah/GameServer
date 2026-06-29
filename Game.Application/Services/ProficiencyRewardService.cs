using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Core.Attributes;
using Game.Core.Battle;
using Game.Core.Players;
using Game.Core.Proficiencies;
using Game.Core.Progress;
using static Game.Core.Proficiencies.ProficiencyXpCalculator;

namespace Game.Application.Services
{
    /// <summary>
    /// The shared proficiency-XP accrual step for a won battle (effect-based model, spike #1318). Given the
    /// battle's skill stats and the player's total attributes (power), it sums each path's activity for its
    /// <see cref="EActivityKey"/>, routes it to the path's current frontier tier, and has each path
    /// independently claim <c>pie × clamp(activity ÷ power)</c> of XP — power-normalization is the continuous
    /// difficulty curve (it subsumes the banded difficulty multiplier, which is deliberately not applied
    /// again). It then applies the leveling against each proficiency's authored curve and writes the absolute
    /// result through the progress aggregate. Both the live battle-completion handler and the offline-rewards
    /// batch run it, so the accrual is computed identically on both paths (the "offline == live" invariant).
    /// <para>
    /// Every avenue is wired here, folded across each leaf type's applicable keys
    /// (<see cref="DamageTypes.Applies"/>): the <b>offense</b> book trains offense keys on the typed damage the
    /// player dealt — direct hits and DoT alike (<see cref="BattleStats.TypedDamageDealt"/>) — the
    /// <b>incoming</b> book trains resist keys on the player's pre-mitigation typed exposure
    /// (<see cref="BattleStats.TypedDamageExposure"/>), and the event-keyed combat magnitudes — crit damage
    /// (Precision), dodged damage (Evasion), healing done (Restoration), and reflected damage dealt
    /// (Retribution, <see cref="BattleStats.PlayerReflectedDamageDealt"/> — #1363) — are damage-type-neutral
    /// and map straight to a single activity key. The <c>notify</c> flag drives the live client push: the live path
    /// notifies (a per-battle push), the offline batch suppresses it (the welcome-back summary is the
    /// notification — spike #982 decision 9).
    /// </para>
    /// </summary>
    public class ProficiencyRewardService(IProficiencies proficiencies, ISkills skills)
    {
        private readonly IProficiencies _proficiencies = proficiencies;
        private readonly ISkills _skills = skills;

        /// <summary>
        /// Accrues a won battle's proficiency XP onto <paramref name="progress"/> and returns the per-proficiency
        /// results plus any nodes opened. Call only for a victory — XP is earned on victory.
        /// <paramref name="totalAttributes"/> is the player's power (the sum of core additive attribute
        /// modifiers, <c>DefeatRewards.PlayerPower</c>) each path's activity is normalized by; pass
        /// <paramref name="notify"/> <c>true</c> on the live path to raise the client push, or <c>false</c>
        /// for the offline batch (which folds the returned results onto the welcome-back summary instead).
        /// </summary>
        public ProficiencyAccrualResult AccrueAndApply(
            PlayerProgress progress, BattleStats stats, double totalAttributes, Player player, bool notify)
        {
            var slices = ProficiencyXpCalculator.Split(
                ServerGameConstants.ProficiencyXpPerVictory, totalAttributes,
                ServerGameConstants.MaxExpRewardMultiplier, BuildActivities(stats, progress));
            if (slices.Count == 0)
            {
                return ProficiencyAccrualResult.Empty;
            }

            var results = new List<ProficiencyXpResult>();
            var opened = new List<ProficiencyOpened>();
            var openedIds = new HashSet<int>();
            foreach (var slice in slices)
            {
                // The slice's proficiency is the path's frontier tier — un-maxed by construction (the routing
                // skips fully-maxed paths and resolves a partially-maxed path to its first un-maxed tier), so a
                // maxed proficiency never reaches here. That routing, not a downstream guard, is what banks
                // nothing on a maxed path now.
                var proficiency = _proficiencies.GetProficiency(slice.ProficiencyId);

                var (oldLevel, oldXp) = progress.TryGetProficiency(slice.ProficiencyId, out var existing)
                    ? (existing.Level, existing.Xp)
                    : (0, 0m);

                // Round to the persisted XP scale. A trivial enemy's slice — a path's activity tiny relative to
                // the player's power — can round to nothing; skip it rather than persist an information-free
                // zero-gain row.
                var xpGain = Math.Round((decimal)slice.Xp, 3, MidpointRounding.AwayFromZero);
                if (xpGain <= 0)
                {
                    continue;
                }

                var (newLevel, newXp) = proficiency.ApplyXp(oldLevel, oldXp, xpGain);
                progress.SetProficiencyProgress(slice.ProficiencyId, newLevel, newXp);

                // Grant the reward skill of every milestone the gain crossed (UnlockSkill is idempotent, so the
                // offline batch re-running per battle never double-grants).
                var grantedSkillIds = proficiency.RewardSkillsCrossed(oldLevel, newLevel);
                foreach (var skillId in grantedSkillIds)
                {
                    player.UnlockSkill(_skills.GetSkill(skillId));
                }

                // Maxing a tier opens the next nodes: its within-path successor, plus any cross-path gateway
                // whose prerequisites are now all maxed. Opening is notification-only — no skill is granted (the
                // opened tier's native skill is re-homed onto this tier's max-level milestone reward, already
                // applied above by RewardSkillsCrossed).
                if (proficiency.IsMaxed(newLevel) && !proficiency.IsMaxed(oldLevel))
                {
                    OpenSuccessors(proficiency, progress, opened, openedIds);
                }

                results.Add(new ProficiencyXpResult(
                    slice.ProficiencyId, xpGain, newLevel, newXp,
                    proficiency.MilestonesCrossed(oldLevel, newLevel), grantedSkillIds));
            }

            if (notify)
            {
                player.RaiseProficiencyXpGained(results, opened);
            }

            return new ProficiencyAccrualResult(results, opened);
        }

        // Opens the nodes a just-maxed proficiency unlocks: the next tier within its own path (revealed by
        // maxing the tier before it — spike #982 decision 10), and any cross-path gateway it gates whose every
        // prerequisite is now maxed (decision 10's themed gateways). Within-path order is implicit in the
        // ordinals, so a successor tier needs no authored prerequisite row.
        private void OpenSuccessors(
            Proficiency maxed, PlayerProgress progress,
            List<ProficiencyOpened> opened, HashSet<int> openedIds)
        {
            if (_proficiencies.GetPath(maxed.PathId).NextTier(maxed.PathOrdinal) is { } nextTier)
            {
                Open(nextTier.ProficiencyId, opened, openedIds);
            }

            foreach (var gatedId in _proficiencies.DependentsOf(maxed.Id))
            {
                if (AllPrerequisitesMaxed(gatedId, progress))
                {
                    Open(gatedId, opened, openedIds);
                }
            }
        }

        // Records the open for the client push (notification-only — opening grants no skill; the opened tier's
        // native training skill is re-homed onto the predecessor tier's max-level milestone reward). De-duped
        // within the battle so two prerequisites maxing in one fight open a shared gateway once.
        private static void Open(int proficiencyId, List<ProficiencyOpened> opened, HashSet<int> openedIds)
        {
            if (!openedIds.Add(proficiencyId))
            {
                return;
            }

            opened.Add(new ProficiencyOpened(proficiencyId));
        }

        // Whether every prerequisite of the gated proficiency is at its cap on the player's current progress.
        // A gateway opens only once all its themed prerequisites are maxed (the just-maxed one included, since
        // its new level is already written through the progress aggregate before this runs).
        private bool AllPrerequisitesMaxed(int gatedId, PlayerProgress progress)
        {
            foreach (var prerequisiteId in _proficiencies.GetProficiency(gatedId).PrerequisiteIds)
            {
                var level = progress.TryGetProficiency(prerequisiteId, out var existing) ? existing.Level : 0;
                if (!_proficiencies.GetProficiency(prerequisiteId).IsMaxed(level))
                {
                    return false;
                }
            }

            return true;
        }

        // The per-path activity for the battle, routed to each path's frontier tier. A typed quantity folds
        // across every key its applies() set includes (Fire feeds Fire + Elemental; Burn feeds Burn + Fire +
        // Elemental + Dot), mapped to the offense or resist activity key for its book; each key's summed quantity
        // is the activity of every (non-retired) path bound to it, so a path trains in proportion to the share its
        // key captures (focus beats spread within a book) and the axes train in parallel. A fully-maxed path (no
        // frontier) banks nothing; a retired path is absent from the index (frozen). The avenues:
        //   • Offense (output book): the typed damage the player dealt — direct hits and DoT alike
        //     (BattleStats.TypedDamageDealt), post-mitigation, routed to the offense keys.
        //   • Resist (incoming book): the player's pre-mitigation typed exposure
        //     (BattleStats.TypedDamageExposure), routed to the resist keys, so a resist never throttles its own
        //     training signal.
        //   • Events: crit damage, dodged damage, healing done, and reflected damage dealt — damage-type-neutral
        //     magnitudes that map straight to a single activity key (Crit / Dodge / Heal / Reflect) without
        //     applies() routing.
        private List<PathActivity> BuildActivities(BattleStats stats, PlayerProgress progress)
        {
            int LevelOf(int proficiencyId) =>
                progress.TryGetProficiency(proficiencyId, out var existing) ? existing.Level : 0;

            var activityByKey = new Dictionary<EActivityKey, double>();
            // ForDamageKey resolves for every key; ForDamageKeyResist is null for the amp-only weapon keys
            // (#1340), whose exposure routes to the shared Physical resist key only — the fold skips those nulls.
            FoldIntoActivityKeys(activityByKey, stats.TypedDamageDealt, key => ActivityKeys.ForDamageKey(key));
            FoldIntoActivityKeys(activityByKey, stats.TypedDamageExposure, ActivityKeys.ForDamageKeyResist);

            // Event-keyed activities: combat magnitudes that are not typed damage, so each maps straight to a
            // single global activity key (no applies() routing, no per-type split). Only positive amounts are
            // recorded — a battle with no crit / dodge / healing / reflection trains none of these.
            void AddEvent(EActivityKey key, double amount)
            {
                if (amount > 0)
                {
                    activityByKey[key] = activityByKey.GetValueOrDefault(key) + amount;
                }
            }

            AddEvent(EActivityKey.Crit, stats.CriticalDamageDealt);
            AddEvent(EActivityKey.Dodge, stats.DamageDodged);
            AddEvent(EActivityKey.Heal, stats.PlayerDamageHealed);
            AddEvent(EActivityKey.Reflect, stats.PlayerReflectedDamageDealt);

            // Route each key's activity to the frontier tier of every path bound to it.
            var activities = new List<PathActivity>();
            foreach (var (activityKey, activity) in activityByKey)
            {
                foreach (var path in _proficiencies.PathsForActivityKey(activityKey))
                {
                    if (path.Frontier(LevelOf) is { } frontier)
                    {
                        activities.Add(new PathActivity(frontier.ProficiencyId, activity));
                    }
                }
            }

            return activities;
        }

        // Folds a book's per-leaf-type quantities into per-activity-key totals: each type adds its quantity to
        // every key its applies() set resolves to, mapped to that book's activity key by toActivityKey (offense
        // or resist). Accumulates into the shared map so both books route through one pass below; offense and
        // resist keys are disjoint, so the two books never collide on a key.
        private static void FoldIntoActivityKeys(
            Dictionary<EActivityKey, double> activityByKey,
            IReadOnlyDictionary<EDamageType, double> quantityByType,
            Func<EDamageTypeKey, EActivityKey?> toActivityKey)
        {
            foreach (var (type, quantity) in quantityByType)
            {
                foreach (var key in DamageTypes.Applies(type))
                {
                    // A key with no activity key for this book (an amp-only weapon key on the resist side) is
                    // skipped — its quantity routes only through the keys that do resolve.
                    if (toActivityKey(key) is EActivityKey activityKey)
                    {
                        activityByKey[activityKey] = activityByKey.GetValueOrDefault(activityKey) + quantity;
                    }
                }
            }
        }
    }
}
