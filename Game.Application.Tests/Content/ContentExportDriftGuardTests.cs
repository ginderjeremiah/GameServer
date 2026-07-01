using Game.Application.Content;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.Content
{
    /// <summary>
    /// The CI drift guard for the content export (spike #1390, decision 4): re-derives the export from a
    /// freshly-migrated-and-seeded database and asserts byte-equality with the committed <c>content/*.json</c>
    /// files. A hand-edit to a committed file, or a content change that wasn't re-exported, fails the build.
    ///
    /// The DB is seeded from the existing <c>e2e-seed.sql</c> content slice — the only source-controlled
    /// content today — so the committed snapshot is the export of that slice. (#1419 reverses the direction:
    /// the seeder loads <c>content/*.json</c> and <c>e2e-seed.sql</c> is retired; this guard then becomes a
    /// pure round-trip check and its seed source switches accordingly.)
    ///
    /// To (re)generate the committed files after an intentional content change, run this test with the
    /// <c>CONTENT_EXPORT_REGEN=1</c> environment variable set, then commit the diff.
    /// </summary>
    [Collection("Integration")]
    public class ContentExportDriftGuardTests : ApplicationIntegrationTestBase
    {
        public ContentExportDriftGuardTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper)
        {
        }

        [Fact]
        public async Task ExportAll_MatchesCommittedContentFiles()
        {
            await SeedContentFromE2eScriptAsync();
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var exporter = scope.ServiceProvider.GetRequiredService<IContentExporter>();
            var files = exporter.ExportAll();

            var contentDirectory = RepoPaths.ContentDirectory();
            var regenerate = Environment.GetEnvironmentVariable("CONTENT_EXPORT_REGEN") == "1";
            if (regenerate)
            {
                Directory.CreateDirectory(contentDirectory);
            }

            foreach (var file in files)
            {
                var path = Path.Combine(contentDirectory, file.FileName);
                if (regenerate)
                {
                    await File.WriteAllTextAsync(path, file.Json, CancellationToken);
                }

                Assert.True(
                    File.Exists(path),
                    $"Missing committed content file '{file.FileName}'. Run with CONTENT_EXPORT_REGEN=1 to (re)generate the export, then commit the diff.");

                var committed = await File.ReadAllTextAsync(path, CancellationToken);
                Assert.True(
                    committed == file.Json,
                    $"Committed '{file.FileName}' is out of sync with the exporter. Run with CONTENT_EXPORT_REGEN=1 to re-export, then commit the diff.");
            }
        }

        /// <summary>
        /// Applies the content portion of <c>e2e-seed.sql</c> (everything before the e2e-only admin
        /// provisioning trigger) to the freshly-migrated database. The trigger is deliberately excluded so it
        /// does not leak into the shared integration database.
        /// </summary>
        private async Task SeedContentFromE2eScriptAsync()
        {
            var script = await File.ReadAllTextAsync(RepoPaths.E2eSeedScript(), CancellationToken);
            const string provisioningMarker = "-- e2e admin provisioning.";
            var markerIndex = script.IndexOf(provisioningMarker, StringComparison.Ordinal);
            var contentSql = markerIndex >= 0 ? script[..markerIndex] : script;

            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            await context.Database.ExecuteSqlRawAsync(contentSql, CancellationToken);
        }
    }
}
