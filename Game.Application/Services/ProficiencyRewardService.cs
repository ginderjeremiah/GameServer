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
    /// The shared proficiency-XP accrual step for a won battle (spike #982 area C). Given the battle's skill
    /// stats and its difficulty multiplier, it splits the fixed XP pie across the proficiencies represented in
    /// the fight, applies the leveling against each proficiency's authored curve, and writes the absolute
    /// result through the progress aggregate. Both the live battle-completion handler and the offline-rewards
    /// batch run it, so the accrual is computed identically on both paths (the "offline == live" invariant).
    /// <para>
    /// It resolves the reference data (the skill rarity → tier weight and the skill → proficiency
    /// contributions) and delegates the math to <see cref="ProficiencyXpCalculator"/> and the
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
                ServerGameConstants.ProficiencyXpPerVictory, difficultyMultiplier, BuildContributions(stats));
            if (slices.Count == 0)
            {
                return [];
            }

            var results = new List<ProficiencyXpResult>();
            foreach (var slice in slices)
            {
                var proficiency = _proficiencies.GetProficiency(slice.ProficiencyId);

                var (oldLevel, oldXp) = progress.TryGetProficiency(slice.ProficiencyId, out var existing)
                    ? (existing.Level, existing.Xp)
                    : (0, 0m);

                // A maxed proficiency banks no further XP — its slice is simply spent (the player's other
                // contributing proficiencies still progress; this is why a skill may contribute to several).
                if (oldLevel >= proficiency.MaxLevel)
                {
                    continue;
                }

                // Round to the persisted XP scale. A trivial enemy's slice can round to nothing; skip it rather
                // than persist an information-free zero-gain row.
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

        // The weighted contributions of every skill that fired in the battle: a proficiency is represented if
        // at least one contributing skill fired, and a fired skill's pull is skillTierWeight × contributionWeight
        // (not multiplied by how often it fired — representation, not frequency, so a fast-cooldown skill earns
        // no more pie than a slow one). Tier weight is flat 1 until #979 lands (ProficiencyTierWeight).
        private List<WeightedContribution> BuildContributions(BattleStats stats)
        {
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
                    contributions.Add(new WeightedContribution(
                        contribution.ProficiencyId, tierWeight * contribution.Weight));
                }
            }

            return contributions;
        }
    }
}
