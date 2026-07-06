using Game.Abstractions.DataAccess;
using Game.Core.Battle;
using Game.Core.Players;
using Game.Core.Proficiencies;
using Game.Core.Progress;

namespace Game.Application.Services
{
    /// <summary>
    /// Thin application-layer adapter over the domain accrual math (<see cref="ProficiencyAccrual"/>, moved to
    /// <c>Game.Core</c> in #1602 so the offline simulator can run the identical math in-loop): bundles
    /// <see cref="IProficiencies"/> into the <see cref="ProficiencyCatalog"/> resolvers the domain layer takes,
    /// writes the accrual through the <see cref="PlayerProgress"/> aggregate, grants the milestone reward
    /// skills the domain layer only reports (<see cref="ISkills"/>), and raises the live client push. Both the
    /// live battle-completion handler (<see cref="Events.BattleStatisticsEventHandler"/>) and the offline
    /// consolidation (<see cref="OfflineProgressService"/>, applying the simulator's already-computed in-loop
    /// result) go through this same apply step, so a milestone grant is never duplicated between the two paths.
    /// </summary>
    public class ProficiencyRewardService(IProficiencies proficiencies, ISkills skills)
    {
        private readonly ISkills _skills = skills;
        private readonly ProficiencyCatalog _catalog = new(
            proficiencies.GetProficiency, proficiencies.GetPath, proficiencies.PathsForActivityKey, proficiencies.DependentsOf);

        /// <summary>
        /// Accrues a won battle's proficiency XP onto <paramref name="progress"/> and returns the per-proficiency
        /// results plus any nodes opened. Call only for a victory — XP is earned on victory.
        /// <paramref name="ratingDenominator"/> is <c>max(playerRating, enemyRating)</c> (spike #1526 Decision 5)
        /// each path's activity is normalized by; pass <paramref name="notify"/> <c>true</c> on the live path to
        /// raise the client push, or <c>false</c> for the offline batch (which folds the returned results onto
        /// the welcome-back summary instead).
        /// </summary>
        public ProficiencyAccrualResult AccrueAndApply(
            PlayerProgress progress, BattleStats stats, double ratingDenominator, Player player, bool notify)
        {
            var accrual = ProficiencyAccrual.Accrue(
                _catalog, stats, ratingDenominator,
                id => progress.TryGetProficiency(id, out var existing) ? existing.Level : 0,
                id => progress.TryGetProficiency(id, out var existing) ? existing.Xp : 0m,
                progress.SetProficiencyProgress);

            GrantRewardSkills(accrual, player);

            if (notify)
            {
                player.RaiseProficiencyXpGained(accrual.Results, accrual.Opened);
            }

            return accrual;
        }

        /// <summary>
        /// Grants the reward skill of every milestone <paramref name="accrual"/> crossed
        /// (<see cref="Player.UnlockSkill"/> is idempotent, so re-granting an already-owned skill is a no-op) —
        /// the side effect the pure domain accrual only reports via <see cref="ProficiencyXpResult.GrantedSkillIds"/>.
        /// Exposed separately so <see cref="OfflineProgressService"/> can apply it once for a whole away
        /// window's folded accrual (the simulator computes the accrual in-loop itself, bypassing
        /// <see cref="AccrueAndApply"/>).
        /// </summary>
        public void GrantRewardSkills(ProficiencyAccrualResult accrual, Player player)
        {
            foreach (var result in accrual.Results)
            {
                foreach (var skillId in result.GrantedSkillIds)
                {
                    player.UnlockSkill(_skills.GetSkill(skillId));
                }
            }
        }
    }
}
