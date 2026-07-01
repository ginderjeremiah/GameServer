using Game.Abstractions.Content;
using Game.Application.Content;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.Content
{
    /// <summary>
    /// The CI drift guard for the content export (spike #1390, decision 4): re-derives the export from a
    /// freshly-migrated-and-seeded database and asserts byte-equality with the committed <c>content/*.json</c>
    /// files. A hand-edit to a committed file, or a content change that wasn't re-exported, fails the build.
    ///
    /// The DB is seeded from the committed <c>content/*.json</c> export through the JSON-driven content seeder
    /// (#1419), so this is a round-trip: seed the export → re-derive it → assert byte-equality. It catches a
    /// hand-edit to a committed file, a content change that wasn't re-exported, or a seeder/exporter asymmetry.
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
            await SeedContentFromExportAsync();
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var exporter = scope.ServiceProvider.GetRequiredService<IContentExporter>();
            var files = await exporter.ExportAllAsync(CancellationToken);

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
        /// Seeds the freshly-migrated database from the committed <c>content/*.json</c> export through the
        /// JSON-driven content seeder — the same path the app uses on startup.
        /// </summary>
        private async Task SeedContentFromExportAsync()
        {
            using var scope = CreateScope();
            var reader = scope.ServiceProvider.GetRequiredService<IContentImportReader>();
            var seeder = scope.ServiceProvider.GetRequiredService<IContentSeeder>();
            await seeder.SeedAsync(reader.Read(RepoPaths.ContentDirectory()), CancellationToken);
        }
    }
}
