using Game.Core;
using Game.Core.Skills;
using Xunit;

namespace Game.Core.Tests.Skills
{
    /// <summary>
    /// Coverage for <see cref="PrimaryDamageTypeResolver"/> — the shared tie-break rule <see
    /// cref="Skill.PrimaryDamageType"/>, <c>ProgressionGraphChecker</c>, and <c>AdminItems</c> all resolve
    /// through. <see cref="SkillTests"/> already pins these scenarios via <see cref="Skill"/> (double
    /// weights); this exercises the resolver directly with a <c>decimal</c> weight selector, as the read
    /// contract and persisted entity call sites use.
    /// </summary>
    public class PrimaryDamageTypeResolverTests
    {
        private sealed record Portion(EDamageType Type, decimal Weight);

        [Fact]
        public void Resolve_PicksTheHighestWeightPortion()
        {
            var portions = new[]
            {
                new Portion(EDamageType.Physical, 0.4m),
                new Portion(EDamageType.Fire, 0.6m),
            };

            var result = PrimaryDamageTypeResolver.Resolve(portions, p => p.Weight, p => p.Type);

            Assert.Equal(EDamageType.Fire, result);
        }

        [Fact]
        public void Resolve_OnWeightTie_PicksTheFirstAuthoredPortion()
        {
            var portions = new[]
            {
                new Portion(EDamageType.Water, 1.0m),
                new Portion(EDamageType.Fire, 1.0m),
            };

            var result = PrimaryDamageTypeResolver.Resolve(portions, p => p.Weight, p => p.Type);

            Assert.Equal(EDamageType.Water, result);
        }

        [Fact]
        public void Resolve_NoPortions_FallsBackToPhysical()
        {
            var result = PrimaryDamageTypeResolver.Resolve(
                Array.Empty<Portion>(), p => p.Weight, p => p.Type);

            Assert.Equal(EDamageType.Physical, result);
        }
    }
}
