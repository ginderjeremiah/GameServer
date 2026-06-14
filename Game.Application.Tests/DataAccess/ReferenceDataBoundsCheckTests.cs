using Game.Abstractions.DataAccess;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Pins the gameplay <c>Get*</c> bounds-checking (#487): an id that no longer resolves (a stale player row,
    /// migration drift) surfaces as a descriptive <see cref="ArgumentOutOfRangeException"/> naming the id and
    /// set rather than a bare indexer crash on the hot player-load path. Exercised through the DI-resolved
    /// repositories against the eagerly-loaded reference snapshots; both the above-range and negative branches
    /// are covered.
    /// </summary>
    [Collection("Integration")]
    public class ReferenceDataBoundsCheckTests : ApplicationIntegrationTestBase
    {
        public ReferenceDataBoundsCheckTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public void GetItem_IdOutOfRange_ThrowsDescriptiveArgumentOutOfRange()
        {
            using var scope = CreateScope();
            var items = scope.ServiceProvider.GetRequiredService<IItems>();

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => items.GetItem(int.MaxValue));
            Assert.Contains("item", ex.Message);
            Assert.Throws<ArgumentOutOfRangeException>(() => items.GetItem(-1));
        }

        [Fact]
        public void GetSkill_IdOutOfRange_ThrowsDescriptiveArgumentOutOfRange()
        {
            using var scope = CreateScope();
            var skills = scope.ServiceProvider.GetRequiredService<ISkills>();

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => skills.GetSkill(int.MaxValue));
            Assert.Contains("skill", ex.Message);
            Assert.Throws<ArgumentOutOfRangeException>(() => skills.GetSkill(-1));
        }

        [Fact]
        public void GetItemMod_IdOutOfRange_ThrowsDescriptiveArgumentOutOfRange()
        {
            using var scope = CreateScope();
            var itemMods = scope.ServiceProvider.GetRequiredService<IItemMods>();

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => itemMods.GetItemMod(int.MaxValue));
            Assert.Contains("item mod", ex.Message);
            Assert.Throws<ArgumentOutOfRangeException>(() => itemMods.GetItemMod(-1));
        }

        [Fact]
        public void GetChallenge_IdOutOfRange_ThrowsDescriptiveArgumentOutOfRange()
        {
            using var scope = CreateScope();
            var challenges = scope.ServiceProvider.GetRequiredService<IChallenges>();

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => challenges.GetChallenge(int.MaxValue));
            Assert.Contains("challenge", ex.Message);
            Assert.Throws<ArgumentOutOfRangeException>(() => challenges.GetChallenge(-1));
        }
    }
}
