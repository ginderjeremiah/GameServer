using Game.Application.Content;
using Game.TestInfrastructure.Helpers;
using Xunit;

namespace Game.Application.Tests.Content
{
    /// <summary>
    /// The must-have CI half of the progression-graph lint (spike #1390, decision 5): loads the committed
    /// <c>content/*.json</c> export and asserts the whole graph is clean.
    ///
    /// <see cref="ContentGraphSeverity.Error"/> findings — a dangling reference or live content wedged into a
    /// permanently unusable state — always fail the build. <see cref="ContentGraphSeverity.Warning"/> findings
    /// (unreachable / dead content the runtime tolerates) also fail the build **unless** they are on the
    /// <see cref="AcceptedWarnings"/> allowlist — the "no warnings, or only ones we consciously accept and
    /// document" bar from #1435. The allowlist is the documentation: an entry is a deliberate, reviewed decision
    /// to tolerate a work-in-progress gap in the authored content, not a silent accumulation of them.
    ///
    /// The committed files are the source of truth here (pinned to the DB by the content-export drift guard), so
    /// this needs no database — it reads the same JSON the client and Workbench would receive.
    /// </summary>
    public class ContentGraphLintTests
    {
        /// <summary>
        /// Warnings on the committed content that are a consciously-accepted, documented work-in-progress gap.
        /// Each entry is the finding's <see cref="ContentGraphFinding.ToString"/> value; add one only with a
        /// comment saying why it is tolerated. Empty today — the seed slice is fully lint-clean (#1435).
        /// </summary>
        private static readonly HashSet<string> AcceptedWarnings = [];

        [Fact]
        public async Task CommittedContent_HasNoReachabilityErrors()
        {
            var findings = await CheckCommittedContentAsync();
            var errors = findings.Where(f => f.Severity == ContentGraphSeverity.Error).ToList();

            Assert.True(
                errors.Count == 0,
                $"The committed content graph has {errors.Count} reachability error(s):\n{string.Join("\n", errors)}");
        }

        [Fact]
        public async Task CommittedContent_HasNoUnacceptedWarnings()
        {
            var findings = await CheckCommittedContentAsync();
            var warnings = findings
                .Where(f => f.Severity == ContentGraphSeverity.Warning && !AcceptedWarnings.Contains(f.ToString()))
                .ToList();

            Assert.True(
                warnings.Count == 0,
                $"The committed content graph has {warnings.Count} unaccepted warning(s). Fix the content, or add "
                + $"the finding to AcceptedWarnings with a note on why the gap is deliberate:\n{string.Join("\n", warnings)}");
        }

        private static async Task<IReadOnlyList<ContentGraphFinding>> CheckCommittedContentAsync()
        {
            var graph = await ContentGraphJsonReader.ReadFromDirectoryAsync(RepoPaths.ContentDirectory(), TestContext.Current.CancellationToken);
            return new ProgressionGraphChecker().Check(graph);
        }
    }
}
