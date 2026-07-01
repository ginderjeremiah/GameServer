using Game.Application.Content;
using Game.TestInfrastructure.Helpers;
using Xunit;

namespace Game.Application.Tests.Content
{
    /// <summary>
    /// The must-have CI half of the progression-graph lint (spike #1390, decision 5): loads the committed
    /// <c>content/*.json</c> export and asserts the whole graph has no <see cref="ContentGraphSeverity.Error"/>
    /// findings — a dangling reference or live content wedged into a permanently unusable state fails the build.
    /// Warnings (unreachable / dead content the runtime tolerates) are surfaced in the assertion message but do
    /// not gate the build, since authored content may legitimately have work-in-progress gaps.
    ///
    /// The committed files are the source of truth here (pinned to the DB by the content-export drift guard), so
    /// this needs no database — it reads the same JSON the client and Workbench would receive.
    /// </summary>
    public class ContentGraphLintTests
    {
        [Fact]
        public async Task CommittedContent_HasNoReachabilityErrors()
        {
            var graph = await ContentGraphJsonReader.ReadFromDirectoryAsync(RepoPaths.ContentDirectory(), TestContext.Current.CancellationToken);
            var checker = new ProgressionGraphChecker();

            var findings = checker.Check(graph);
            var errors = findings.Where(f => f.Severity == ContentGraphSeverity.Error).ToList();

            Assert.True(
                errors.Count == 0,
                $"The committed content graph has {errors.Count} reachability error(s):\n{string.Join("\n", errors)}");
        }
    }
}
