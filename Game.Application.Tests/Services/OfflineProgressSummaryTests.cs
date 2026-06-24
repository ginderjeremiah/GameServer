using Game.Application.Services;
using Game.Core.Proficiencies;
using Xunit;

namespace Game.Application.Tests.Services
{
    /// <summary>
    /// <see cref="OfflineProgressSummary.HasProgress"/> gates the welcome-back screen: it must report progress
    /// when the away window earned anything the gate renders — including a window that only advanced
    /// proficiencies or opened a node (spike #982 decision 9) — and skip the gate for a truly empty summary.
    /// </summary>
    public class OfflineProgressSummaryTests
    {
        private static OfflineProgressSummary Summary(
            int battlesWon = 0,
            IReadOnlyList<ProficiencyXpResult>? proficiencyGains = null,
            IReadOnlyList<ProficiencyOpened>? openedProficiencies = null) => new()
            {
                AwayMs = 1000,
                AutoChallengeBoss = false,
                ZoneId = 1,
                BattlesWon = battlesWon,
                BattlesLost = 0,
                BattlesDrawn = 0,
                TotalExp = 0,
                LevelsGained = 0,
                StatPointsGained = 0,
                CompletedChallenges = [],
                ProficiencyGains = proficiencyGains ?? [],
                OpenedProficiencies = openedProficiencies ?? [],
            };

        [Fact]
        public void HasProgress_EmptySummary_IsFalse()
        {
            Assert.False(Summary().HasProgress);
            Assert.False(OfflineProgressSummary.Empty(1000, false, 1).HasProgress);
        }

        [Fact]
        public void HasProgress_ProficiencyGainOnly_IsTrue()
        {
            var summary = Summary(proficiencyGains: [new ProficiencyXpResult(1, 5m, 1, 0m, [], [])]);

            Assert.True(summary.HasProgress);
        }

        [Fact]
        public void HasProgress_OpenedNodeOnly_IsTrue()
        {
            var summary = Summary(openedProficiencies: [new ProficiencyOpened(1, SeedSkillId: null)]);

            Assert.True(summary.HasProgress);
        }
    }
}
