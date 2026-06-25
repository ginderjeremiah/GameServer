using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Core.Battle;
using Game.Core.Players;
using Game.Core.Proficiencies;
using Game.Core.Progress;
using static Game.Core.Proficiencies.ProficiencyXpCalculator;

namespace Game.Application.Services
{
    /// <summary>
    /// The shared proficiency-XP accrual step for a won battle (spike #982 area C, refined by the Paths
    /// rework #1161). Given the battle's skill stats and its difficulty multiplier, it routes each
    /// represented path to its current frontier tier, splits the fixed XP pie across those tiers by
    /// falloff-free attention scaled by on-tier efficiency (the absolute-falloff model), applies the leveling
    /// against each proficiency's authored curve, and writes the absolute result through the progress
    /// aggregate. Both the live battle-completion handler and the offline-rewards batch run it, so the accrual
    /// is computed identically on both paths (the "offline == live" invariant).
    /// <para>
    /// It resolves the reference data (the skill rarity → tier weight, the skill → path contributions, and
    /// each path's frontier/falloff) and delegates the math to <see cref="ProficiencyXpCalculator"/> and the
    /// <see cref="Proficiency"/> curve. The <c>notify</c> flag drives the live client push: the live path
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
        /// <paramref name="difficultyMultiplier"/> is the <c>DefeatRewards</c> difficulty factor for the battle;
        /// pass <paramref name="notify"/> <c>true</c> on the live path to raise the client push, or <c>false</c>
        /// for the offline batch (which folds the returned results onto the welcome-back summary instead).
        /// </summary>
        public ProficiencyAccrualResult AccrueAndApply(
            PlayerProgress progress, BattleStats stats, double difficultyMultiplier, Player player, bool notify)
        {
            var slices = ProficiencyXpCalculator.Split(
                ServerGameConstants.ProficiencyXpPerVictory, difficultyMultiplier, BuildContributions(stats, progress));
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

                // Round to the persisted XP scale. A trivial enemy's slice — or a coasting path whose falloff
                // nearly evaporated it — can round to nothing; skip it rather than persist an information-free
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
                // whose prerequisites are now all maxed. Each open grants the opened tier's seed skill.
                if (proficiency.IsMaxed(newLevel) && !proficiency.IsMaxed(oldLevel))
                {
                    OpenSuccessors(proficiency, progress, player, opened, openedIds);
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
            Proficiency maxed, PlayerProgress progress, Player player,
            List<ProficiencyOpened> opened, HashSet<int> openedIds)
        {
            if (_proficiencies.GetPath(maxed.PathId).NextTier(maxed.PathOrdinal) is { } nextTier)
            {
                Open(nextTier.ProficiencyId, player, opened, openedIds);
            }

            foreach (var gatedId in _proficiencies.DependentsOf(maxed.Id))
            {
                if (AllPrerequisitesMaxed(gatedId, progress))
                {
                    Open(gatedId, player, opened, openedIds);
                }
            }
        }

        // Grants the opened tier's seed skill (the native, full-pace training vehicle for a node with no world
        // skill source — decision 8) and records the open for the client push. De-duped within the battle so
        // two prerequisites maxing in one fight open a shared gateway once.
        private void Open(int proficiencyId, Player player, List<ProficiencyOpened> opened, HashSet<int> openedIds)
        {
            if (!openedIds.Add(proficiencyId))
            {
                return;
            }

            var seedSkillId = _proficiencies.GetProficiency(proficiencyId).SeedSkillId;
            if (seedSkillId is { } skillId)
            {
                player.UnlockSkill(_skills.GetSkill(skillId));
            }

            opened.Add(new ProficiencyOpened(proficiencyId, seedSkillId));
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

        // The weighted contributions of every skill that fired in the battle, routed to each path's frontier
        // tier. A path is represented if at least one contributing skill fired (representation, not frequency —
        // a fast-cooldown skill earns no more pie than a slow one). A fired skill's pull on the frontier tier
        // is its falloff-free attention (skillTierWeight × contributionWeight, the tier weight derived from the
        // skill's rarity — see ProficiencyTierWeight, #1123) paired with the absolute falloff over the
        // home-tier→frontier distance, so a stale skill supplements
        // the current tier only at a discount. A fully-maxed path (no frontier) banks nothing; a skill homed
        // deeper than the frontier never trains a tier below where it was acquired.
        private List<WeightedContribution> BuildContributions(BattleStats stats, PlayerProgress progress)
        {
            int LevelOf(int proficiencyId) =>
                progress.TryGetProficiency(proficiencyId, out var existing) ? existing.Level : 0;

            var contributions = new List<WeightedContribution>();
            foreach (var (skillId, skillStats) in stats.SkillStats)
            {
                if (skillStats.Uses <= 0)
                {
                    continue;
                }

                var tierWeight = ProficiencyTierWeight.For(_skills.GetSkill(skillId).Rarity);
                foreach (var contribution in _proficiencies.ContributionsForSkill(skillId))
                {
                    var path = _proficiencies.GetPath(contribution.PathId);
                    if (path.Frontier(LevelOf) is not { } frontier)
                    {
                        continue;
                    }

                    var distance = frontier.Ordinal - contribution.HomeTier;
                    if (distance < 0)
                    {
                        continue;
                    }

                    contributions.Add(new WeightedContribution(
                        frontier.ProficiencyId, tierWeight * contribution.Weight, path.FalloffAt(distance)));
                }
            }

            return contributions;
        }
    }
}
