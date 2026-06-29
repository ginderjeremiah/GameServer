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

        [Theory]
        [InlineData(ERarity.Common)]
        [InlineData(ERarity.Rare)]
        [InlineData(ERarity.Legendary)]
        [InlineData(ERarity.Mythic)]
        public void ToContract_RoundTripsRarity(ERarity rarity)
        {
            var entity = NewSkill(ESkillAcquisition.Player, rarity);

            var contract = SkillMapper.ToContract(entity);

            Assert.Equal(rarity, contract.RarityId);
        }

        private static EntitySkill NewSkill(ESkillAcquisition acquisition, ERarity rarity = ERarity.Common) => new()
        {
            Id = 0,
            Name = "Test",
            Description = "",
            IconPath = "",
            Word = "",
            Pronunciation = "",
            Translation = "",
            BaseDamage = 1m,
            CooldownMs = 1000,
            RarityId = (int)rarity,
            Acquisition = (int)acquisition,
            SkillDamageMultipliers = [],
            SkillEffects = [],
        };
    }
}
