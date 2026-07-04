using Game.Api.Models.Enemies;
using Game.Application.Services;

namespace Game.Api.Models.Progress
{
    /// <summary>
    /// The welcome-back summary returned by <c>GetOfflineProgress</c>: what the player's idle loop earned while
    /// they were away. The client gate renders it (away duration, loop mode, battle tally, exp/levels/stat
    /// points, the challenges completed with what they unlocked, and the proficiency gains / opened nodes), then
    /// re-syncs authoritative state on entering the game. An empty summary (<see cref="HasProgress"/> false)
    /// skips the gate. The proficiency fields reuse the live push's models, so the gate renders offline and live
    /// proficiency gains the same way.
    /// </summary>
    public class OfflineProgressModel : IModelFromSource<OfflineProgressModel, OfflineProgressSummary>
    {
        public long AwayMs { get; set; }
        public bool AutoChallengeBoss { get; set; }
        public int ZoneId { get; set; }
        public int BattlesWon { get; set; }
        public int BattlesLost { get; set; }
        public int BattlesDrawn { get; set; }
        public long TotalExp { get; set; }
        public int LevelsGained { get; set; }
        public int StatPointsGained { get; set; }
        public bool HasProgress { get; set; }
        public required List<ChallengeCompletedModel> CompletedChallenges { get; set; }
        public required List<ProficiencyXpResultModel> ProficiencyGains { get; set; }
        public required List<ProficiencyOpenedModel> OpenedProficiencies { get; set; }

        /// <summary>Non-null when the player's pre-existing battle was still genuinely in progress rather than
        /// concluded (#1595): the still-active battle to resume — the client's replay-to-offset fast-forward
        /// (#1597) is a separate follow-up. When set, every other field above is at its empty/default value.
        /// </summary>
        public EnemyInstance? ActiveBattle { get; set; }

        public static OfflineProgressModel FromSource(OfflineProgressSummary source)
        {
            return new OfflineProgressModel
            {
                AwayMs = source.AwayMs,
                AutoChallengeBoss = source.AutoChallengeBoss,
                ZoneId = source.ZoneId,
                BattlesWon = source.BattlesWon,
                BattlesLost = source.BattlesLost,
                BattlesDrawn = source.BattlesDrawn,
                TotalExp = source.TotalExp,
                LevelsGained = source.LevelsGained,
                StatPointsGained = source.StatPointsGained,
                HasProgress = source.HasProgress,
                CompletedChallenges = source.CompletedChallenges
                    .Select(c => new ChallengeCompletedModel
                    {
                        ChallengeId = c.ChallengeId,
                        RewardItemId = c.RewardItemId,
                        RewardItemModId = c.RewardItemModId,
                    })
                    .ToList(),
                ProficiencyGains = source.ProficiencyGains
                    .Select(p => new ProficiencyXpResultModel
                    {
                        ProficiencyId = p.ProficiencyId,
                        XpGained = p.XpGained,
                        NewLevel = p.NewLevel,
                        NewXp = p.NewXp,
                        MilestonesCrossed = p.MilestonesCrossed.ToList(),
                        GrantedSkillIds = p.GrantedSkillIds.ToList(),
                    })
                    .ToList(),
                OpenedProficiencies = source.OpenedProficiencies
                    .Select(o => new ProficiencyOpenedModel
                    {
                        ProficiencyId = o.ProficiencyId,
                    })
                    .ToList(),
                ActiveBattle = source.ActiveBattle is not null ? EnemyInstance.FromSource(source.ActiveBattle) : null,
            };
        }
    }
}
