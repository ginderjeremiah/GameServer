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
        /// results. Call only for a victory — XP is earned on victory. <paramref name="difficultyMultiplier"/> is
        /// the <c>DefeatRewards</c> difficulty factor for the battle; pass <paramref name="notify"/> <c>true</c>
        /// on the live path to raise the client push, or <c>false</c> for the offline batch.
        /// </summary>
        public IReadOnlyList<ProficiencyXpResult> AccrueAndApply(
            PlayerProgress progress, BattleStats stats, double difficultyMultiplier, Player player, bool notify)
        {
            var slices = ProficiencyXpCalculator.Split(
                ServerGameConstants.ProficiencyXpPerVictory, difficultyMultiplier, BuildContributions(stats, progress));
            if (slices.Count == 0)
            {
                return [];
            }

            var results = new List<ProficiencyXpResult>();
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

                results.Add(new ProficiencyXpResult(
                    slice.ProficiencyId, xpGain, newLevel, newXp, proficiency.MilestonesCrossed(oldLevel, newLevel)));
            }

            if (notify)
            {
                player.RaiseProficiencyXpGained(results);
            }

            return results;
        }

        // The weighted contributions of every skill that fired in the battle, routed to each path's frontier
        // tier. A path is represented if at least one contributing skill fired (representation, not frequency —
        // a fast-cooldown skill earns no more pie than a slow one). A fired skill's pull on the frontier tier
        // is its falloff-free attention (skillTierWeight × contributionWeight, tier weight flat 1 until #979)
        // paired with the absolute falloff over the home-tier→frontier distance, so a stale skill supplements
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
