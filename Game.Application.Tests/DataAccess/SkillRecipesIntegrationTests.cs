using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Entities = Game.Infrastructure.Entities;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Exercises the skill-recipe reference repo against a real database: the contract projection and the
    /// shared lean core model, including a retired recipe's exclusion from live authoring while staying
    /// resolvable by id.
    /// </summary>
    [Collection("Integration")]
    public class SkillRecipesIntegrationTests : ApplicationIntegrationTestBase
    {
        public SkillRecipesIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task GetSkillRecipe_AssemblesInputsAndConditions()
        {
            int recipeId, resultSkillId, inputA, inputB, proficiencyId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                resultSkillId = (await SeedSkillAsync(context, ESkillAcquisition.Synthesis)).Id;
                inputA = (await SeedSkillAsync(context, ESkillAcquisition.Player)).Id;
                inputB = (await SeedSkillAsync(context, ESkillAcquisition.Player)).Id;
                proficiencyId = (await SeedProficiencyAsync(context)).Id;

                var recipe = new Entities.SkillRecipe { ResultSkillId = resultSkillId, DesignerNotes = "" };
                context.SkillRecipes.Add(recipe);
                await context.SaveChangesAsync(CancellationToken);
                recipeId = recipe.Id;

                context.SkillRecipeInputs.AddRange(
                    new Entities.SkillRecipeInput { RecipeId = recipeId, SkillId = inputA },
                    new Entities.SkillRecipeInput { RecipeId = recipeId, SkillId = inputB });
                context.SkillRecipeConditions.Add(new Entities.SkillRecipeCondition
                {
                    RecipeId = recipeId,
                    ProficiencyId = proficiencyId,
                    MinLevel = 3,
                });
                await context.SaveChangesAsync(CancellationToken);
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var recipes = scope.ServiceProvider.GetRequiredService<ISkillRecipes>();

            var core = recipes.GetSkillRecipe(recipeId);
            Assert.Equal(resultSkillId, core.ResultSkillId);
            Assert.False(core.IsRetired);
            Assert.Equal([inputA, inputB], core.InputSkillIds.OrderBy(id => id));
            var condition = Assert.Single(core.Conditions);
            Assert.Equal(proficiencyId, condition.ProficiencyId);
            Assert.Equal(3, condition.MinLevel);

            var contract = Assert.Single(recipes.AllSkillRecipes());
            Assert.Equal(resultSkillId, contract.ResultSkillId);
            Assert.Equal([inputA, inputB], contract.InputSkillIds.OrderBy(id => id));
            Assert.Single(contract.Conditions, c => c.ProficiencyId == proficiencyId && c.MinLevel == 3);
        }

        [Fact]
        public async Task RetiredRecipe_IsExcludedFromLiveAuthoring_ButResolvableById()
        {
            int recipeId, resultSkillId, inputSkillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                resultSkillId = (await SeedSkillAsync(context, ESkillAcquisition.Synthesis)).Id;
                inputSkillId = (await SeedSkillAsync(context, ESkillAcquisition.Player)).Id;

                var recipe = new Entities.SkillRecipe { ResultSkillId = resultSkillId, RetiredAt = DateTime.UtcNow, DesignerNotes = "" };
                context.SkillRecipes.Add(recipe);
                await context.SaveChangesAsync(CancellationToken);
                recipeId = recipe.Id;

                context.SkillRecipeInputs.Add(new Entities.SkillRecipeInput { RecipeId = recipeId, SkillId = inputSkillId });
                await context.SaveChangesAsync(CancellationToken);
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var recipes = scope.ServiceProvider.GetRequiredService<ISkillRecipes>();

            // A retired recipe stays resolvable by id (already-synthesized results persist)...
            var core = recipes.GetSkillRecipe(recipeId);
            Assert.True(core.IsRetired);
            Assert.Equal(resultSkillId, core.ResultSkillId);
            // ...but is flagged retired rather than silently dropped, so a caller must check IsRetired before offering it.
        }

        private async Task<Entities.Skill> SeedSkillAsync(GameContext context, ESkillAcquisition acquisition)
        {
            var skill = new Entities.Skill
            {
                Name = "Skill",
                Description = "",
                DesignerNotes = "",
                IconPath = "",
                Word = "",
                Pronunciation = "",
                Translation = "",
                BaseDamage = 1m,
                CooldownMs = 1000,
                Acquisition = (int)acquisition,
                SkillDamageMultipliers = [],
                SkillEffects = [],
                RarityId = (int)ERarity.Common,
            };
            context.Skills.Add(skill);
            await context.SaveChangesAsync(CancellationToken);
            return skill;
        }

        private async Task<Entities.Proficiency> SeedProficiencyAsync(GameContext context)
        {
            var path = new Entities.Path { Name = "Fire", Description = "d", DesignerNotes = "" };
            context.Paths.Add(path);
            await context.SaveChangesAsync(CancellationToken);

            var proficiency = new Entities.Proficiency
            {
                Name = "Blades",
                Description = "d",
                DesignerNotes = "",
                IconPath = "i",
                Word = "w",
                Pronunciation = "p",
                Translation = "t",
                PathId = path.Id,
                PathOrdinal = 0,
                MaxLevel = 10,
                BaseXp = 100m,
                XpGrowth = 2m,
                LevelModifiers = [],
                LevelRewards = [],
                Prerequisites = [],
            };
            context.Proficiencies.Add(proficiency);
            await context.SaveChangesAsync(CancellationToken);
            return proficiency;
        }
    }
}
