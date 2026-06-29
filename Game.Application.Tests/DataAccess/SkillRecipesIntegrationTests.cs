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
    /// Exercises the skill-recipe reference repo against a real database: the contract projection, the shared
    /// lean core model, and the derived input-skill → recipe-ids reverse index (including its exclusion of
    /// retired recipes, which stay resolvable by id).
    /// </summary>
    [Collection("Integration")]
    public class SkillRecipesIntegrationTests : ApplicationIntegrationTestBase
    {
        public SkillRecipesIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task GetSkillRecipe_AssemblesInputsAndConditions_AndReverseIndexIsExposed()
        {
            int recipeId, resultSkillId, inputA, inputB, proficiencyId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                resultSkillId = (await SeedSkillAsync(context, ESkillAcquisition.Synthesis)).Id;
                inputA = (await SeedSkillAsync(context, ESkillAcquisition.Player)).Id;
                inputB = (await SeedSkillAsync(context, ESkillAcquisition.Player)).Id;
                proficiencyId = (await SeedProficiencyAsync(context)).Id;

                var recipe = new Entities.SkillRecipe { ResultSkillId = resultSkillId };
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

            // The reverse index maps each input skill to the recipes it feeds.
            Assert.Equal([recipeId], recipes.RecipesForInputSkill(inputA));
            Assert.Equal([recipeId], recipes.RecipesForInputSkill(inputB));
            // The result skill is not an input, so it feeds nothing.
            Assert.Empty(recipes.RecipesForInputSkill(resultSkillId));

            var contract = Assert.Single(recipes.AllSkillRecipes());
            Assert.Equal(resultSkillId, contract.ResultSkillId);
            Assert.Equal([inputA, inputB], contract.InputSkillIds.OrderBy(id => id));
            Assert.Single(contract.Conditions, c => c.ProficiencyId == proficiencyId && c.MinLevel == 3);
        }

        [Fact]
        public async Task RetiredRecipe_IsExcludedFromReverseIndex_ButResolvableById()
        {
            int recipeId, resultSkillId, inputSkillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                resultSkillId = (await SeedSkillAsync(context, ESkillAcquisition.Synthesis)).Id;
                inputSkillId = (await SeedSkillAsync(context, ESkillAcquisition.Player)).Id;

                var recipe = new Entities.SkillRecipe { ResultSkillId = resultSkillId, RetiredAt = DateTime.UtcNow };
                context.SkillRecipes.Add(recipe);
                await context.SaveChangesAsync(CancellationToken);
                recipeId = recipe.Id;

                context.SkillRecipeInputs.Add(new Entities.SkillRecipeInput { RecipeId = recipeId, SkillId = inputSkillId });
                await context.SaveChangesAsync(CancellationToken);
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var recipes = scope.ServiceProvider.GetRequiredService<ISkillRecipes>();

            // Out of circulation: a retired recipe is never offered or hinted.
            Assert.Empty(recipes.RecipesForInputSkill(inputSkillId));
            // But it stays resolvable by id (already-synthesized results persist).
            var core = recipes.GetSkillRecipe(recipeId);
            Assert.True(core.IsRetired);
            Assert.Equal(resultSkillId, core.ResultSkillId);
        }

        private async Task<Entities.Skill> SeedSkillAsync(GameContext context, ESkillAcquisition acquisition)
        {
            var skill = new Entities.Skill
            {
                Name = "Skill",
                Description = "",
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
            var path = new Entities.Path { Name = "Fire", Description = "d", FalloffBase = 0.3m };
            context.Paths.Add(path);
            await context.SaveChangesAsync(CancellationToken);

            var proficiency = new Entities.Proficiency
            {
                Name = "Blades",
                Description = "d",
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
