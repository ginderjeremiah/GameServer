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
            int proficiencyId, skillId, pathId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                skillId = (await SeedSkillAsync(context, ESkillAcquisition.Player)).Id;

                var path = new Entities.Path { Name = "Fire", Description = "d", FalloffBase = 0.3m };
                context.Paths.Add(path);
                await context.SaveChangesAsync(CancellationToken);
                pathId = path.Id;

                var proficiency = new Entities.Proficiency
                {
                    Name = "Blades",
                    Description = "d",
                    IconPath = "i",
                    Word = "aenkor",
                    Pronunciation = "AYN-kor",
                    Translation = "The First Flame",
                    PathId = path.Id,
                    PathOrdinal = 0,
                    MaxLevel = 10,
                    BaseXp = 100m,
                    XpGrowth = 2m,
                    StartsUnlocked = true,
                    LevelModifiers = [],
                    LevelRewards = [],
                    Prerequisites = [],
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
                context.SkillPathContributions.Add(new Entities.SkillPathContribution
                {
                    SkillId = skillId,
                    PathId = proficiency.PathId,
                    HomeTier = proficiency.PathOrdinal,
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

            // The reverse index exposes the contribution's path, home tier, and weight directly; the accrual
            // resolves the frontier tier and falloff against the path at battle completion.
            var contribution = Assert.Single(proficiencies.ContributionsForSkill(skillId));
            Assert.Equal(pathId, contribution.PathId);
            Assert.Equal(0, contribution.HomeTier);
            Assert.Equal(1.5d, contribution.Weight);

            // The decipher "words of power" round-trip through GetProficiencies onto the contract verbatim.
            var contract = proficiencies.AllProficiencies().Single(p => p.Id == proficiencyId);
            Assert.Equal("Blades", contract.Name);
            Assert.Equal("aenkor", contract.Word);
            Assert.Equal("AYN-kor", contract.Pronunciation);
            Assert.Equal("The First Flame", contract.Translation);
            Assert.Contains(
                proficiencies.AllPaths(),
                p => p.Name == "Fire" && p.Contributions.Any(c => c.SkillId == skillId && c.HomeTier == 0));
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

        [Fact]
        public async Task ContributionsForSkill_ExposesThePathAndHomeTier()
        {
            int pathId, skillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                skillId = (await SeedSkillAsync(context, ESkillAcquisition.Player)).Id;

                var path = new Entities.Path { Name = "Fire", Description = "d", FalloffBase = 0.3m };
                context.Paths.Add(path);
                await context.SaveChangesAsync(CancellationToken);
                pathId = path.Id;

                context.Proficiencies.AddRange(
                    NewTier(path.Id, ordinal: 0, name: "Fire Magic"),
                    NewTier(path.Id, ordinal: 1, name: "Inferno Magic"));
                await context.SaveChangesAsync(CancellationToken);

                // Homed at tier 1: the index must carry the authored home tier verbatim (not collapse it to
                // tier 0), since the accrual resolves the falloff distance from it.
                context.SkillPathContributions.Add(new Entities.SkillPathContribution
                {
                    SkillId = skillId,
                    PathId = path.Id,
                    HomeTier = 1,
                    Weight = 2m,
                });
                await context.SaveChangesAsync(CancellationToken);
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var proficiencies = scope.ServiceProvider.GetRequiredService<IProficiencies>();

            var contribution = Assert.Single(proficiencies.ContributionsForSkill(skillId));
            Assert.Equal(pathId, contribution.PathId);
            Assert.Equal(1, contribution.HomeTier);
            Assert.Equal(2d, contribution.Weight);
        }

        [Fact]
        public async Task ContributionsForSkill_ReturnsEmpty_WhenSkillOnlyFeedsARetiredPath()
        {
            int skillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                skillId = (await SeedSkillAsync(context, ESkillAcquisition.Player)).Id;

                // A retired path: a skill that only contributes to it must drop out of the reverse index, so
                // the accrual routes no XP into the frozen track (the path is out of circulation).
                var path = new Entities.Path { Name = "Fire", Description = "d", FalloffBase = 0.3m, RetiredAt = DateTime.UtcNow };
                context.Paths.Add(path);
                await context.SaveChangesAsync(CancellationToken);

                context.Proficiencies.Add(NewTier(path.Id, ordinal: 0, name: "Fire Magic"));
                await context.SaveChangesAsync(CancellationToken);

                context.SkillPathContributions.Add(new Entities.SkillPathContribution
                {
                    SkillId = skillId,
                    PathId = path.Id,
                    HomeTier = 0,
                    Weight = 2m,
                });
                await context.SaveChangesAsync(CancellationToken);
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var proficiencies = scope.ServiceProvider.GetRequiredService<IProficiencies>();

            Assert.Empty(proficiencies.ContributionsForSkill(skillId));
        }

        [Fact]
        public async Task GetPath_ExposesFalloffBaseAndTiersOrderedByOrdinal()
        {
            int pathId, tierZeroId, tierOneId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();

                var pathEntity = new Entities.Path { Name = "Fire", Description = "d", FalloffBase = 0.3m };
                context.Paths.Add(pathEntity);
                await context.SaveChangesAsync(CancellationToken);
                pathId = pathEntity.Id;

                // Insert the deeper tier first to prove the routing model orders by ordinal, not insertion.
                var tierOne = NewTier(pathEntity.Id, ordinal: 1, name: "Inferno Magic");
                var tierZero = NewTier(pathEntity.Id, ordinal: 0, name: "Fire Magic");
                context.Proficiencies.AddRange(tierOne, tierZero);
                await context.SaveChangesAsync(CancellationToken);
                tierZeroId = tierZero.Id;
                tierOneId = tierOne.Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var proficiencies = scope.ServiceProvider.GetRequiredService<IProficiencies>();

            var path = proficiencies.GetPath(pathId);
            Assert.Equal(0.3d, path.FalloffBase);
            Assert.Equal(
                [(tierZeroId, 0), (tierOneId, 1)],
                path.Tiers.Select(t => (t.ProficiencyId, t.Ordinal)));

            // An untrained path's frontier is its first tier; one maxed tier 0 advances it to tier 1.
            Assert.Equal(tierZeroId, path.Frontier(_ => 0)?.ProficiencyId);
            Assert.Equal(tierOneId, path.Frontier(id => id == tierZeroId ? 10 : 0)?.ProficiencyId);
        }

        private static Entities.Proficiency NewTier(int pathId, int ordinal, string name) => new()
        {
            Name = name,
            Description = "d",
            IconPath = "i",
            Word = "w",
            Pronunciation = "p",
            Translation = "t",
            PathId = pathId,
            PathOrdinal = ordinal,
            MaxLevel = 10,
            BaseXp = 100m,
            XpGrowth = 2m,
            StartsUnlocked = ordinal == 0,
            LevelModifiers = [],
            LevelRewards = [],
            Prerequisites = [],
        };

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
