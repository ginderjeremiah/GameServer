using Game.DataAccess.Repositories.Caching;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Covers the reference-cache holder's build-then-swap behaviour through the real DI scope factory. The
    /// holder resolves its own scope from DI, so per backend.md its behaviour is exercised here rather than
    /// as an isolated unit test; the build is stubbed so the failure branch can be forced deterministically.
    /// </summary>
    [Collection("Integration")]
    public class ReferenceCacheHolderTests : ApplicationIntegrationTestBase
    {
        public ReferenceCacheHolderTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public void Current_BeforeFirstLoad_Throws()
        {
            using var scope = CreateScope();
            var holder = new StubHolder(scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(), new StubBuild());

            // No lazy fill: reading an unloaded holder throws rather than returning an empty/torn default.
            Assert.Throws<InvalidOperationException>(() => holder.Current);
        }

        [Fact]
        public async Task ReloadAsync_WhenBuildFails_KeepsThePreviousSnapshot()
        {
            using var scope = CreateScope();
            var build = new StubBuild();
            var holder = new StubHolder(scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(), build);

            // First reload publishes a snapshot.
            await holder.ReloadAsync(CancellationToken);
            Assert.Equal(new[] { 1, 2, 3 }, holder.Current);

            // A failing reload must leave the previously published snapshot in place (stale-but-valid),
            // never an empty or torn one — the headline fail-safe of build-then-swap.
            build.Fail = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => holder.ReloadAsync(CancellationToken));
            Assert.Equal(new[] { 1, 2, 3 }, holder.Current);

            // A later successful reload swaps the new snapshot in.
            build.Fail = false;
            build.Value = new List<int> { 4, 5 };
            await holder.ReloadAsync(CancellationToken);
            Assert.Equal(new[] { 4, 5 }, holder.Current);
        }

        private sealed class StubBuild
        {
            public bool Fail { get; set; }
            public List<int> Value { get; set; } = new() { 1, 2, 3 };

            public List<int> Build() => Fail ? throw new InvalidOperationException("build failed") : Value;
        }

        private sealed class StubHolder(IServiceScopeFactory scopeFactory, StubBuild build)
            : ReferenceCacheHolder<List<int>>(scopeFactory)
        {
            protected override Task<List<int>> BuildSnapshotAsync(GameContext context, CancellationToken cancellationToken)
                => Task.FromResult(build.Build());
        }
    }
}
