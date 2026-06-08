using Xunit;

namespace Game.Api.Tests.Architecture
{
    /// <summary>
    /// Architecture guard for the entity-isolation capstone (#138). The EF entity model now lives in
    /// <c>Game.Infrastructure</c> (next to <c>GameContext</c>) and must stay an implementation detail of
    /// the data tier: <c>Game.Api</c> and <c>Game.Application</c> reach persistence only through
    /// <c>Game.Abstractions</c> contracts/interfaces and the <c>Game.DataAccess</c> composition root, so
    /// neither may take a compile-time dependency on <c>Game.Infrastructure</c>.
    /// <para>
    /// The mechanism that enforces this is the <c>PrivateAssets="Compile"</c> on
    /// <c>Game.DataAccess -&gt; Game.Infrastructure</c> (Infrastructure's compile-time types do not flow
    /// transitively to <c>Game.Api</c>) plus <c>Game.Application</c> never referencing Infrastructure at
    /// all. Because every entity type lives in <c>Game.Infrastructure</c>, "does not reference the
    /// Game.Infrastructure assembly" is equivalent to "has no dependency on the entity model"; this test
    /// fails fast if either guarantee regresses.
    /// </para>
    /// </summary>
    public class EntityIsolationTests
    {
        [Theory]
        [InlineData(typeof(Game.Api.Startup))]
        [InlineData(typeof(Game.Application.Services.AccountService))]
        public void Layer_DoesNotReferenceInfrastructure(Type layerType)
        {
            var referencedAssemblies = layerType.Assembly
                .GetReferencedAssemblies()
                .Select(assembly => assembly.Name);

            Assert.DoesNotContain("Game.Infrastructure", referencedAssemblies);
        }
    }
}
