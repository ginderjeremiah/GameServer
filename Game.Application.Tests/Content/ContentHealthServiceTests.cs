using Game.Abstractions.Contracts.Admin;
using Game.Application.Content;
using Xunit;

namespace Game.Application.Tests.Content
{
    /// <summary>
    /// Covers the mapping seam of <see cref="ContentHealthService"/> — projecting the checker's domain findings
    /// onto the admin report contract and tallying the severity counts. The graph-from-caches assembly mirrors
    /// <see cref="ContentExporter"/> and the checks themselves are covered by
    /// <see cref="ProgressionGraphCheckerTests"/>, so the incremental logic here is the projection.
    /// </summary>
    public class ContentHealthServiceTests
    {
        [Fact]
        public void ToReport_MapsSeverityCheckAndEntity()
        {
            var findings = new[]
            {
                new ContentGraphFinding(ContentGraphSeverity.Error, "ZoneBoss", "Zone", 1, "Boss is retired."),
            };

            var report = ContentHealthService.ToReport(findings);

            var finding = Assert.Single(report.Findings);
            Assert.Equal(EContentHealthSeverity.Error, finding.Severity);
            Assert.Equal("ZoneBoss", finding.Check);
            Assert.Equal("Zone", finding.EntityKind);
            Assert.Equal(1, finding.EntityId);
            Assert.Equal("Boss is retired.", finding.Message);
        }

        [Fact]
        public void ToReport_CountsErrorsAndWarningsSeparately()
        {
            var findings = new[]
            {
                new ContentGraphFinding(ContentGraphSeverity.Error, "ZoneBoss", "Zone", 1, "a"),
                new ContentGraphFinding(ContentGraphSeverity.Error, "ChallengeReward", "Challenge", 2, "b"),
                new ContentGraphFinding(ContentGraphSeverity.Warning, "OrphanSkill", "Skill", 3, "c"),
            };

            var report = ContentHealthService.ToReport(findings);

            Assert.Equal(2, report.ErrorCount);
            Assert.Equal(1, report.WarningCount);
            Assert.Equal(3, report.Findings.Count);
        }

        [Fact]
        public void ToReport_PreservesFindingOrder()
        {
            var findings = new[]
            {
                new ContentGraphFinding(ContentGraphSeverity.Warning, "OrphanSkill", "Skill", 3, "a"),
                new ContentGraphFinding(ContentGraphSeverity.Warning, "OrphanSkill", "Skill", 5, "b"),
            };

            var report = ContentHealthService.ToReport(findings);

            Assert.Equal([3, 5], report.Findings.Select(finding => finding.EntityId));
        }

        [Fact]
        public void ToReport_EmptyForAHealthyGraph()
        {
            var report = ContentHealthService.ToReport([]);

            Assert.Empty(report.Findings);
            Assert.Equal(0, report.ErrorCount);
            Assert.Equal(0, report.WarningCount);
        }
    }
}
