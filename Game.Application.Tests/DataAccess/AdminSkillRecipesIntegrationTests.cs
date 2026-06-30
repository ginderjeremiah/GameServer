using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Exercises <see cref="IAdminSkillRecipes"/>: the retire-only identity save, the Synthesis-flag result
    /// guard, the input guards (non-empty, non-retired, not the result), the proficiency-condition guards, the
    /// acyclicity guard, and the child-collection reconcilers. Seed, write, and assert each use a separate DI
    /// scope, as a real admin call does.
    /// </summary>
    [Collection("Integration")]
    public class AdminSkillRecipesIntegrationTests : ApplicationIntegrationTestBase
    {
        public AdminSkillRecipesIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task SaveSkillRecipes_AddsARecipe()
        {
            int resultSkillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                resultSkillId = (await SeedSkillAsync(context, ESkillAcquisition.Synthesis)).Id;
            }
            await ReloadReferenceCachesAsync();

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminSkillRecipes>();
                Assert.True(admin.SaveSkillRecipes(
                [
                    new Change<Contracts.SkillRecipe> { ChangeType = EChangeType.Add, Item = NewRecipe(resultSkillId: resultSkillId) },
                ]).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using var assertScope = CreateScope();
            var assertContext = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            Assert.Contains(await assertContext.SkillRecipes.ToListAsync(CancellationToken), r => r.ResultSkillId == resultSkillId);
        }

        [Fact]
        public async Task SaveSkillRecipes_ResultNotSynthesisFlagged_ReturnsFailure()
        {
            int resultSkillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                resultSkillId = (await SeedSkillAsync(context, ESkillAcquisition.Player)).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminSkillRecipes>();

            var result = admin.SaveSkillRecipes(
            [
                new Change<Contracts.SkillRecipe> { ChangeType = EChangeType.Add, Item = NewRecipe(resultSkillId: resultSkillId) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Contains("not flagged as Synthesis-acquirable", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveSkillRecipes_RetiredResult_ReturnsFailure()
        {
            int resultSkillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                resultSkillId = (await SeedSkillAsync(context, ESkillAcquisition.Synthesis, retired: true)).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminSkillRecipes>();

            var result = admin.SaveSkillRecipes(
            [
                new Change<Contracts.SkillRecipe> { ChangeType = EChangeType.Add, Item = NewRecipe(resultSkillId: resultSkillId) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Contains("is retired and cannot be a recipe result", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveSkillRecipes_UnknownResultSkill_ReturnsFailure()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminSkillRecipes>();

            var result = admin.SaveSkillRecipes(
            [
                new Change<Contracts.SkillRecipe> { ChangeType = EChangeType.Add, Item = NewRecipe(resultSkillId: 99999) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Contains("Result skill 99999 does not exist", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveSkillRecipes_Delete_ReturnsRetiredNotDeleted()
        {
            int recipeId;
            using (var seedScope = CreateScope())
            {
                recipeId = (await SeedRecipeAsync(seedScope)).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminSkillRecipes>();

            var result = admin.SaveSkillRecipes(
            [
                new Change<Contracts.SkillRecipe> { ChangeType = EChangeType.Delete, Item = NewRecipe(id: recipeId) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Contains("retired, not deleted", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveSkillRecipes_EditOutOfRangeId_ReturnsNotFound()
        {
            int resultSkillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                resultSkillId = (await SeedSkillAsync(context, ESkillAcquisition.Synthesis)).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminSkillRecipes>();

            var result = admin.SaveSkillRecipes(
            [
                new Change<Contracts.SkillRecipe> { ChangeType = EChangeType.Edit, Item = NewRecipe(id: 99999, resultSkillId: resultSkillId) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("Skill recipe not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveSkillRecipes_ResultAlreadyProducedByLiveRecipe_ReturnsFailure()
        {
            int resultSkillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                resultSkillId = (await SeedSkillAsync(context, ESkillAcquisition.Synthesis)).Id;
                context.SkillRecipes.Add(new Entities.SkillRecipe { ResultSkillId = resultSkillId });
                await context.SaveChangesAsync(CancellationToken);
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminSkillRecipes>();

            var result = admin.SaveSkillRecipes(
            [
                new Change<Contracts.SkillRecipe> { ChangeType = EChangeType.Add, Item = NewRecipe(resultSkillId: resultSkillId) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Contains("more than one active recipe", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveSkillRecipes_DuplicateResultWithinBatch_ReturnsFailure()
        {
            int resultSkillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                resultSkillId = (await SeedSkillAsync(context, ESkillAcquisition.Synthesis)).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminSkillRecipes>();

            var result = admin.SaveSkillRecipes(
            [
                new Change<Contracts.SkillRecipe> { ChangeType = EChangeType.Add, Item = NewRecipe(resultSkillId: resultSkillId) },
                new Change<Contracts.SkillRecipe> { ChangeType = EChangeType.Add, Item = NewRecipe(resultSkillId: resultSkillId) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Contains("more than one active recipe", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveSkillRecipes_EditResultToOneAnotherLiveRecipeProduces_ReturnsFailure()
        {
            int r2Id, skillX;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                skillX = (await SeedSkillAsync(context, ESkillAcquisition.Synthesis)).Id;
                var skillY = (await SeedSkillAsync(context, ESkillAcquisition.Synthesis)).Id;
                context.SkillRecipes.Add(new Entities.SkillRecipe { ResultSkillId = skillX });
                var r2 = new Entities.SkillRecipe { ResultSkillId = skillY };
                context.SkillRecipes.Add(r2);
                await context.SaveChangesAsync(CancellationToken);
                r2Id = r2.Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminSkillRecipes>();

            // Re-pointing R2's result to X (already produced by R1) collides.
            var result = admin.SaveSkillRecipes(
            [
                new Change<Contracts.SkillRecipe> { ChangeType = EChangeType.Edit, Item = NewRecipe(id: r2Id, resultSkillId: skillX) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Contains("more than one active recipe", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveSkillRecipes_DuplicateResultButExistingProducerRetired_Succeeds()
        {
            int resultSkillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                resultSkillId = (await SeedSkillAsync(context, ESkillAcquisition.Synthesis)).Id;
                // A retired recipe is inert, so its result is free to be produced by a live recipe.
                context.SkillRecipes.Add(new Entities.SkillRecipe { ResultSkillId = resultSkillId, RetiredAt = DateTime.UtcNow });
                await context.SaveChangesAsync(CancellationToken);
            }
            await ReloadReferenceCachesAsync();

            using var writeScope = CreateScope();
            var admin = writeScope.ServiceProvider.GetRequiredService<IAdminSkillRecipes>();

            var result = admin.SaveSkillRecipes(
            [
                new Change<Contracts.SkillRecipe> { ChangeType = EChangeType.Add, Item = NewRecipe(resultSkillId: resultSkillId) },
            ]);

            Assert.True(result.Succeeded);
        }

        [Fact]
        public async Task SaveSkillRecipes_RetireProducerAndAddSameResultInOneBatch_Succeeds()
        {
            int r1Id, resultSkillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                resultSkillId = (await SeedSkillAsync(context, ESkillAcquisition.Synthesis)).Id;
                var r1 = new Entities.SkillRecipe { ResultSkillId = resultSkillId };
                context.SkillRecipes.Add(r1);
                await context.SaveChangesAsync(CancellationToken);
                r1Id = r1.Id;
            }
            await ReloadReferenceCachesAsync();

            using var writeScope = CreateScope();
            var admin = writeScope.ServiceProvider.GetRequiredService<IAdminSkillRecipes>();

            // Retiring R1 frees the result for the new recipe in the same batch.
            var retired = NewRecipe(id: r1Id, resultSkillId: resultSkillId);
            retired.RetiredAt = DateTime.UtcNow;
            var result = admin.SaveSkillRecipes(
            [
                new Change<Contracts.SkillRecipe> { ChangeType = EChangeType.Edit, Item = retired },
                new Change<Contracts.SkillRecipe> { ChangeType = EChangeType.Add, Item = NewRecipe(resultSkillId: resultSkillId) },
            ]);

            Assert.True(result.Succeeded);
        }

        [Fact]
        public async Task SetInputs_EmptySet_ReturnsFailure()
        {
            int recipeId;
            using (var seedScope = CreateScope())
            {
                recipeId = (await SeedRecipeAsync(seedScope)).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminSkillRecipes>();

            var result = admin.SetInputs(new SetSkillRecipeInputsData { Id = recipeId, SkillIds = [] });

            Assert.False(result.Succeeded);
            Assert.Contains("at least one input skill", result.ErrorMessage);
        }

        [Fact]
        public async Task SetInputs_InputIsResult_ReturnsFailure()
        {
            int recipeId, resultSkillId;
            using (var seedScope = CreateScope())
            {
                var recipe = await SeedRecipeAsync(seedScope);
                recipeId = recipe.Id;
                resultSkillId = recipe.ResultSkillId;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminSkillRecipes>();

            var result = admin.SetInputs(new SetSkillRecipeInputsData { Id = recipeId, SkillIds = [resultSkillId] });

            Assert.False(result.Succeeded);
            Assert.Contains("cannot include its own result skill", result.ErrorMessage);
        }

        [Fact]
        public async Task SetInputs_RetiredInput_ReturnsFailure()
        {
            int recipeId, retiredInputId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                recipeId = (await SeedRecipeAsync(seedScope)).Id;
                retiredInputId = (await SeedSkillAsync(context, ESkillAcquisition.Player, retired: true)).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminSkillRecipes>();

            var result = admin.SetInputs(new SetSkillRecipeInputsData { Id = recipeId, SkillIds = [retiredInputId] });

            Assert.False(result.Succeeded);
            Assert.Contains("is retired and cannot be a recipe input", result.ErrorMessage);
        }

        [Fact]
        public async Task SetInputs_ReconcilesInputs()
        {
            int recipeId, inputA, inputB;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                recipeId = (await SeedRecipeAsync(seedScope)).Id;
                inputA = (await SeedSkillAsync(context, ESkillAcquisition.Player)).Id;
                inputB = (await SeedSkillAsync(context, ESkillAcquisition.Player)).Id;
            }
            await ReloadReferenceCachesAsync();

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminSkillRecipes>();
                Assert.True(admin.SetInputs(new SetSkillRecipeInputsData { Id = recipeId, SkillIds = [inputA, inputB] }).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using var assertScope = CreateScope();
            var assertContext = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            var inputs = await assertContext.SkillRecipeInputs.Where(i => i.RecipeId == recipeId).ToListAsync(CancellationToken);
            Assert.Equal([inputA, inputB], inputs.Select(i => i.SkillId).OrderBy(id => id));
        }

        [Fact]
        public async Task SetInputs_WouldCreateCycle_ReturnsFailure()
        {
            // R1: skillC ← skillA. R2: skillA ← (to set) skillC. Setting R2's input to C closes the cycle
            // C depends on A (R1), A depends on C (R2). Both results are Synthesis-flagged.
            int r2Id, skillC;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var skillA = (await SeedSkillAsync(context, ESkillAcquisition.Synthesis)).Id;
                skillC = (await SeedSkillAsync(context, ESkillAcquisition.Synthesis)).Id;

                var r1 = new Entities.SkillRecipe { ResultSkillId = skillC };
                var r2 = new Entities.SkillRecipe { ResultSkillId = skillA };
                context.SkillRecipes.AddRange(r1, r2);
                await context.SaveChangesAsync(CancellationToken);
                r2Id = r2.Id;

                context.SkillRecipeInputs.Add(new Entities.SkillRecipeInput { RecipeId = r1.Id, SkillId = skillA });
                await context.SaveChangesAsync(CancellationToken);
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminSkillRecipes>();

            var result = admin.SetInputs(new SetSkillRecipeInputsData { Id = r2Id, SkillIds = [skillC] });

            Assert.False(result.Succeeded);
            Assert.Contains("cycle", result.ErrorMessage);
        }

        [Fact]
        public async Task SetConditions_UnknownProficiency_ReturnsFailure()
        {
            int recipeId;
            using (var seedScope = CreateScope())
            {
                recipeId = (await SeedRecipeAsync(seedScope)).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminSkillRecipes>();

            var result = admin.SetConditions(new SetSkillRecipeConditionsData
            {
                Id = recipeId,
                Conditions = [new Contracts.SkillRecipeCondition { ProficiencyId = 99999, MinLevel = 1 }],
            });

            Assert.False(result.Succeeded);
            Assert.Contains("Condition proficiency 99999 does not exist", result.ErrorMessage);
        }

        [Fact]
        public async Task SetConditions_LevelOutOfRange_ReturnsFailure()
        {
            int recipeId, proficiencyId;
            using (var seedScope = CreateScope())
            {
                recipeId = (await SeedRecipeAsync(seedScope)).Id;
                proficiencyId = (await SeedProficiencyAsync(seedScope)).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminSkillRecipes>();

            var result = admin.SetConditions(new SetSkillRecipeConditionsData
            {
                Id = recipeId,
                Conditions = [new Contracts.SkillRecipeCondition { ProficiencyId = proficiencyId, MinLevel = 99 }],
            });

            Assert.False(result.Succeeded);
            Assert.Contains("out of range", result.ErrorMessage);
        }

        [Fact]
        public async Task SetConditions_ReconcilesConditions()
        {
            int recipeId, proficiencyId;
            using (var seedScope = CreateScope())
            {
                recipeId = (await SeedRecipeAsync(seedScope)).Id;
                proficiencyId = (await SeedProficiencyAsync(seedScope)).Id;
            }
            await ReloadReferenceCachesAsync();

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminSkillRecipes>();
                Assert.True(admin.SetConditions(new SetSkillRecipeConditionsData
                {
                    Id = recipeId,
                    Conditions = [new Contracts.SkillRecipeCondition { ProficiencyId = proficiencyId, MinLevel = 4 }],
                }).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using var assertScope = CreateScope();
            var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            var condition = Assert.Single(await context.SkillRecipeConditions.Where(c => c.RecipeId == recipeId).ToListAsync(CancellationToken));
            Assert.Equal(proficiencyId, condition.ProficiencyId);
            Assert.Equal(4, condition.MinLevel);
        }

        private async Task<Entities.SkillRecipe> SeedRecipeAsync(IServiceScope scope)
        {
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var resultSkillId = (await SeedSkillAsync(context, ESkillAcquisition.Synthesis)).Id;
            var recipe = new Entities.SkillRecipe { ResultSkillId = resultSkillId };
            context.SkillRecipes.Add(recipe);
            await context.SaveChangesAsync(CancellationToken);
            return recipe;
        }

        private async Task<Entities.Proficiency> SeedProficiencyAsync(IServiceScope scope)
        {
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var path = new Entities.Path { Name = "Fire", Description = "d" };
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

        private async Task<Entities.Skill> SeedSkillAsync(GameContext context, ESkillAcquisition acquisition, bool retired = false)
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
                RetiredAt = retired ? DateTime.UtcNow : null,
                SkillDamageMultipliers = [],
                SkillEffects = [],
                RarityId = (int)ERarity.Common,
            };
            context.Skills.Add(skill);
            await context.SaveChangesAsync(CancellationToken);
            return skill;
        }

        private static Contracts.SkillRecipe NewRecipe(int id = 0, int resultSkillId = 0) => new()
        {
            Id = id,
            ResultSkillId = resultSkillId,
            InputSkillIds = [],
            Conditions = [],
        };
    }
}
