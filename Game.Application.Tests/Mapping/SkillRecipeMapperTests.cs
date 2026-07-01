using Game.DataAccess.Mapping;
using Xunit;
using Entities = Game.Infrastructure.Entities;

namespace Game.Application.Tests.Mapping
{
    /// <summary>
    /// Coverage for <see cref="SkillRecipeMapper"/>: the contract projection and the lean core model both
    /// round-trip the result skill, the input skill ids, and the proficiency conditions, ordered
    /// deterministically; the core model derives its retired flag from the entity's <c>RetiredAt</c>.
    /// </summary>
    public class SkillRecipeMapperTests
    {
        [Fact]
        public void ToContract_RoundTripsResultInputsAndConditions_Ordered()
        {
            var entity = NewRecipe(
                resultSkillId: 7,
                inputs: [3, 1],
                conditions: [(ProficiencyId: 5, MinLevel: 4), (ProficiencyId: 2, MinLevel: 1)]);

            var contract = SkillRecipeMapper.ToContract(entity);

            Assert.Equal(0, contract.Id);
            Assert.Equal(7, contract.ResultSkillId);
            Assert.Equal("designer intent", contract.DesignerNotes);
            Assert.Null(contract.RetiredAt);
            // Inputs ordered by skill id for a stable version hash.
            Assert.Equal([1, 3], contract.InputSkillIds);
            // Conditions ordered by proficiency id, carrying their min level.
            Assert.Equal(
                [(2, 1), (5, 4)],
                contract.Conditions.Select(c => (c.ProficiencyId, c.MinLevel)));
        }

        [Fact]
        public void ToCore_RoundTripsResultInputsAndConditions_Ordered()
        {
            var entity = NewRecipe(
                resultSkillId: 7,
                inputs: [3, 1],
                conditions: [(ProficiencyId: 5, MinLevel: 4)]);

            var core = SkillRecipeMapper.ToCore(entity);

            Assert.Equal(7, core.ResultSkillId);
            Assert.False(core.IsRetired);
            Assert.Equal([1, 3], core.InputSkillIds);
            var condition = Assert.Single(core.Conditions);
            Assert.Equal(5, condition.ProficiencyId);
            Assert.Equal(4, condition.MinLevel);
        }

        [Fact]
        public void ToCore_RetiredRecipe_IsRetired()
        {
            var entity = NewRecipe(resultSkillId: 1, inputs: [0], conditions: []);
            entity.RetiredAt = DateTime.UtcNow;

            Assert.True(SkillRecipeMapper.ToCore(entity).IsRetired);
        }

        private static Entities.SkillRecipe NewRecipe(
            int resultSkillId,
            int[] inputs,
            (int ProficiencyId, int MinLevel)[] conditions) => new()
            {
                Id = 0,
                ResultSkillId = resultSkillId,
                DesignerNotes = "designer intent",
                Inputs = [.. inputs.Select(id => new Entities.SkillRecipeInput { RecipeId = 0, SkillId = id })],
                Conditions = [.. conditions.Select(c => new Entities.SkillRecipeCondition
                {
                    RecipeId = 0,
                    ProficiencyId = c.ProficiencyId,
                    MinLevel = c.MinLevel,
                })],
            };
    }
}
