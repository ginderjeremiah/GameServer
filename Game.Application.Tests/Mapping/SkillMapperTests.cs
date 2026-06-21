using Game.Core;
using Game.DataAccess.Mapping;
using Xunit;
using EntitySkill = Game.Infrastructure.Entities.Skill;

namespace Game.Application.Tests.Mapping
{
    /// <summary>
    /// Coverage for <see cref="SkillMapper.ToContract"/>: the acquisition bitmask round-trips from the
    /// entity's stored int to the contract's <see cref="ESkillAcquisition"/> flags. That field is part of
    /// the client-visible contract, so it drives the skills reference-data version hash.
    /// </summary>
    public class SkillMapperTests
    {
        [Theory]
        [InlineData(ESkillAcquisition.None)]
        [InlineData(ESkillAcquisition.Player)]
        [InlineData(ESkillAcquisition.Item)]
        [InlineData(ESkillAcquisition.Enemy)]
        [InlineData(ESkillAcquisition.Player | ESkillAcquisition.Item | ESkillAcquisition.Enemy)]
        public void ToContract_RoundTripsAcquisitionFlags(ESkillAcquisition acquisition)
        {
            var entity = NewSkill(acquisition);

            var contract = SkillMapper.ToContract(entity);

            Assert.Equal(acquisition, contract.Acquisition);
        }

        private static EntitySkill NewSkill(ESkillAcquisition acquisition) => new()
        {
            Id = 0,
            Name = "Test",
            Description = "",
            IconPath = "",
            BaseDamage = 1m,
            CooldownMs = 1000,
            Acquisition = (int)acquisition,
            SkillDamageMultipliers = [],
            SkillEffects = [],
        };
    }
}
