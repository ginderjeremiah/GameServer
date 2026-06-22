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
    /// Exercises the proficiency reference repo against a real database: the contract projection, the
    /// shared core model with its assembled levels, and the derived skill → contributions reverse index.
    /// </summary>
    [Collection("Integration")]
    public class ProficienciesIntegrationTests : ApplicationIntegrationTestBase
    {
        public ProficienciesIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task GetProficiency_AssemblesLevels_AndContributionIndexIsExposed()
        {
            int proficiencyId, skillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                skillId = (await SeedSkillAsync(context, ESkillAcquisition.Player)).Id;

                var proficiency = new Entities.Proficiency
                {
                    Name = "Blades",
                    Description = "d",
                    IconPath = "i",
                    MaxLevel = 10,
                    BaseXp = 100m,
                    XpGrowth = 2m,
                    StartsUnlocked = true,
                    LevelModifiers = [],
                    LevelRewards = [],
                    Prerequisites = [],
                    SkillContributions = [],
                };
                context.Proficiencies.Add(proficiency);
                await context.SaveChangesAsync(CancellationToken);
                proficiencyId = proficiency.Id;

                context.ProficiencyLevelModifiers.Add(new Entities.ProficiencyLevelModifier
                {
                    ProficiencyId = proficiencyId,
                    Level = 1,
                    AttributeId = (int)EAttribute.Strength,
                    ModifierType = (int)EModifierType.Additive,
                    Amount = 1m,
                });
                context.ProficiencyLevelRewards.Add(new Entities.ProficiencyLevelReward
                {
                    ProficiencyId = proficiencyId,
                    Level = 5,
                    RewardSkillId = skillId,
                });
                context.SkillProficiencies.Add(new Entities.SkillProficiency
                {
                    SkillId = skillId,
                    ProficiencyId = proficiencyId,
                    Weight = 1.5m,
                });
                await context.SaveChangesAsync(CancellationToken);
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var proficiencies = scope.ServiceProvider.GetRequiredService<IProficiencies>();

            var core = proficiencies.GetProficiency(proficiencyId);
            Assert.Equal("Blades", core.Name);
            Assert.Equal(10, core.MaxLevel);
            Assert.Equal(2d, core.XpGrowth);

            var level1 = core.Levels.Single(l => l.Level == 1);
            Assert.Equal(EAttribute.Strength, level1.Modifiers.Single().Attribute);
            Assert.Null(level1.RewardSkillId);

            var level5 = core.Levels.Single(l => l.Level == 5);
            Assert.Empty(level5.Modifiers);
            Assert.Equal(skillId, level5.RewardSkillId);

            var contributions = proficiencies.ContributionsForSkill(skillId);
            Assert.Equal(proficiencyId, contributions.Single().ProficiencyId);
            Assert.Equal(1.5d, contributions.Single().Weight);

            Assert.Contains(proficiencies.AllProficiencies(), p => p.Id == proficiencyId && p.Name == "Blades");
        }

        [Fact]
        public async Task ContributionsForSkill_ReturnsEmpty_WhenSkillFeedsNoProficiency()
        {
            int skillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                skillId = (await SeedSkillAsync(context, ESkillAcquisition.Player)).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var proficiencies = scope.ServiceProvider.GetRequiredService<IProficiencies>();

            Assert.Empty(proficiencies.ContributionsForSkill(skillId));
        }

        private async Task<Entities.Skill> SeedSkillAsync(GameContext context, ESkillAcquisition acquisition)
        {
            var skill = new Entities.Skill
            {
                Name = "Slash",
                Description = "",
                IconPath = "",
                BaseDamage = 1m,
                CooldownMs = 1000,
                Acquisition = (int)acquisition,
                SkillDamageMultipliers = [],
                SkillEffects = [],
                RarityId = (int)ERarity.Common
            };
            context.Skills.Add(skill);
            await context.SaveChangesAsync(CancellationToken);
            return skill;
        }
    }
}
