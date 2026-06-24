namespace Game.Core.Proficiencies
{
    /// <summary>
    /// Folds the per-battle <see cref="ProficiencyAccrualResult"/>s of an offline away-window into one aggregate
    /// for the welcome-back summary (spike #982 decision 9). The live path notifies per battle and needs no fold;
    /// the offline batch suppresses the push and reports the whole window at once. For each proficiency the fold
    /// sums the XP gained, keeps the final level/residual XP (battles apply in order, so the last touch is the
    /// window result), and unions the milestones crossed and reward skills granted; opened nodes are unioned by
    /// proficiency. First-seen order is preserved so the summary is deterministic.
    /// </summary>
    public class ProficiencyGainAccumulator
    {
        private readonly Dictionary<int, Gain> _gains = [];
        private readonly List<int> _gainOrder = [];
        private readonly Dictionary<int, ProficiencyOpened> _opened = [];
        private readonly List<int> _openedOrder = [];

        /// <summary>Folds one won battle's accrual into the running window aggregate.</summary>
        public void Add(ProficiencyAccrualResult accrual)
        {
            foreach (var result in accrual.Results)
            {
                if (!_gains.TryGetValue(result.ProficiencyId, out var gain))
                {
                    gain = new Gain(result.ProficiencyId);
                    _gains[result.ProficiencyId] = gain;
                    _gainOrder.Add(result.ProficiencyId);
                }

                gain.Apply(result);
            }

            foreach (var opened in accrual.Opened)
            {
                if (_opened.TryAdd(opened.ProficiencyId, opened))
                {
                    _openedOrder.Add(opened.ProficiencyId);
                }
            }
        }

        /// <summary>The folded window aggregate (empty when no battle trained or opened anything).</summary>
        public ProficiencyAccrualResult Build()
        {
            var results = _gainOrder.Select(id => _gains[id].ToResult()).ToList();
            var opened = _openedOrder.Select(id => _opened[id]).ToList();
            return new ProficiencyAccrualResult(results, opened);
        }

        // One proficiency's running window total: XP summed, level/XP overwritten to the latest, milestones and
        // granted skills unioned in first-seen order (a milestone/skill is crossed/granted once as levels only rise).
        private class Gain(int proficiencyId)
        {
            private readonly List<int> _milestonesCrossed = [];
            private readonly HashSet<int> _seenMilestones = [];
            private readonly List<int> _grantedSkillIds = [];
            private readonly HashSet<int> _seenSkills = [];

            private decimal _xpGained;
            private int _newLevel;
            private decimal _newXp;

            public void Apply(ProficiencyXpResult result)
            {
                _xpGained += result.XpGained;
                _newLevel = result.NewLevel;
                _newXp = result.NewXp;

                foreach (var milestone in result.MilestonesCrossed)
                {
                    if (_seenMilestones.Add(milestone))
                    {
                        _milestonesCrossed.Add(milestone);
                    }
                }

                foreach (var skillId in result.GrantedSkillIds)
                {
                    if (_seenSkills.Add(skillId))
                    {
                        _grantedSkillIds.Add(skillId);
                    }
                }
            }

            public ProficiencyXpResult ToResult() =>
                new(proficiencyId, _xpGained, _newLevel, _newXp, _milestonesCrossed, _grantedSkillIds);
        }
    }
}
