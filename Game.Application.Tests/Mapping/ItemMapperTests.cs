using Game.Core;
using Game.DataAccess.Mapping;
using Xunit;
using EntityItem = Game.Infrastructure.Entities.Item;

namespace Game.Application.Tests.Mapping
{
    /// <summary>
    /// Coverage for <see cref="ItemMapper"/>: the nullable <c>GrantedSkillId</c> round-trips from the entity
    /// to both the client-visible contract (<see cref="ItemMapper.ToContract"/>) and the lean battle domain
    /// model (<see cref="ItemMapper.ToCore"/>). The contract field drives the items reference-data version
    /// hash, and the core field is the id the battle assembly resolves a granted skill from.
    /// </summary>
    public class ItemMapperTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(7)]
        public void ToContract_RoundTripsGrantedSkillId(int? grantedSkillId)
        {
            var entity = NewItem(grantedSkillId);

            var contract = ItemMapper.ToContract(entity);

            Assert.Equal(grantedSkillId, contract.GrantedSkillId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(7)]
        public void ToCore_RoundTripsGrantedSkillId(int? grantedSkillId)
        {
            var entity = NewItem(grantedSkillId);

            var core = ItemMapper.ToCore(entity);

            Assert.Equal(grantedSkillId, core.GrantedSkillId);
        }

        [Theory]
        [InlineData(null, 0)]
        [InlineData(0, 1)]
        [InlineData(4, 7)]
        public void ToContract_RoundTripsRequiredProficiency(int? requiredProficiencyId, int requiredProficiencyLevel)
        {
            var entity = NewItem(requiredProficiencyId: requiredProficiencyId, requiredProficiencyLevel: requiredProficiencyLevel);

            var contract = ItemMapper.ToContract(entity);

            Assert.Equal(requiredProficiencyId, contract.RequiredProficiencyId);
            Assert.Equal(requiredProficiencyLevel, contract.RequiredProficiencyLevel);
        }

        [Theory]
        [InlineData(null, 0)]
        [InlineData(0, 1)]
        [InlineData(4, 7)]
        public void ToCore_RoundTripsRequiredProficiency(int? requiredProficiencyId, int requiredProficiencyLevel)
        {
            var entity = NewItem(requiredProficiencyId: requiredProficiencyId, requiredProficiencyLevel: requiredProficiencyLevel);

            var core = ItemMapper.ToCore(entity);

            Assert.Equal(requiredProficiencyId, core.RequiredProficiencyId);
            Assert.Equal(requiredProficiencyLevel, core.RequiredProficiencyLevel);
        }

        private static EntityItem NewItem(int? grantedSkillId = null, int? requiredProficiencyId = null,
            int requiredProficiencyLevel = 0) => new()
            {
                Id = 0,
                Name = "Test",
                Description = "",
                IconPath = "",
                ItemCategoryId = (int)EItemCategory.Weapon,
                RarityId = (int)ERarity.Common,
                GrantedSkillId = grantedSkillId,
                RequiredProficiencyId = requiredProficiencyId,
                RequiredProficiencyLevel = requiredProficiencyLevel,
                ItemAttributes = [],
                ItemModSlots = [],
                Tags = [],
            };
    }
}
