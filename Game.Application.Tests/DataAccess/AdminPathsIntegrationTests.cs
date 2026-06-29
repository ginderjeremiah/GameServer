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
    /// Exercises <see cref="IAdminPaths"/>: the retire-only identity save and the skill-contributions
    /// reconciler with its skill-exists and home-tier-is-a-real-tier guards. Seed, write, and assert each use
    /// a separate DI scope, as a real admin call does.
    /// </summary>
    [Collection("Integration")]
    public class AdminPathsIntegrationTests : ApplicationIntegrationTestBase
    {
        public AdminPathsIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task SavePaths_AddsANewPath()
        {
            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminPaths>();
                Assert.True(admin.SavePaths(
                [
                    new Change<Contracts.Path> { ChangeType = EChangeType.Add, Item = NewPath() },
                ]).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using var assertScope = CreateScope();
            var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            Assert.Contains(await context.Paths.ToListAsync(CancellationToken), p => p.Name == "Fire");
        }

        [Fact]
        public async Task SetContributions_UnknownSkill_ReturnsFailure()
        {
            int pathId;
            using (var seedScope = CreateScope())
            {
                pathId = (await SeedPathWithTierAsync(seedScope)).PathId;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminPaths>();

            var result = admin.SetContributions(new SetPathContributionsData
            {
                Id = pathId,
                Contributions = [new Contracts.SkillPathContribution { SkillId = 99999, HomeTier = 0, Weight = 1m }],
            });

            Assert.False(result.Succeeded);
            Assert.Contains("does not exist", result.ErrorMessage);
        }

        [Fact]
        public async Task SetContributions_HomeTierIsNotATierOfThePath_ReturnsFailure()
        {
            int pathId, skillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                skillId = (await SeedSkillAsync(context)).Id;
                pathId = (await SeedPathWithTierAsync(seedScope)).PathId;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminPaths>();

            // The path has a single tier (ordinal 0); ordinal 5 is not a tier of it.
            var result = admin.SetContributions(new SetPathContributionsData
            {
                Id = pathId,
                Contributions = [new Contracts.SkillPathContribution { SkillId = skillId, HomeTier = 5, Weight = 1m }],
            });

            Assert.False(result.Succeeded);
            Assert.Contains("not a tier of path", result.ErrorMessage);
        }

        [Fact]
        public async Task SetContributions_ReconcilesAgainstTheHomeTier()
        {
            int pathId, skillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                skillId = (await SeedSkillAsync(context)).Id;
                pathId = (await SeedPathWithTierAsync(seedScope)).PathId;
            }
            await ReloadReferenceCachesAsync();

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminPaths>();
                Assert.True(admin.SetContributions(new SetPathContributionsData
                {
                    Id = pathId,
                    Contributions = [new Contracts.SkillPathContribution { SkillId = skillId, HomeTier = 0, Weight = 2.5m }],
                }).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using var assertScope = CreateScope();
            var assertContext = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            var contribution = Assert.Single(await assertContext.SkillPathContributions
                .Where(c => c.PathId == pathId)
                .ToListAsync(CancellationToken));
            Assert.Equal(skillId, contribution.SkillId);
            Assert.Equal(0, contribution.HomeTier);
            Assert.Equal(2.5m, contribution.Weight);
        }

        [Fact]
        public async Task SavePaths_RetiringPathThatGatesLiveGateway_ReturnsFailure()
        {
            int prereqPathId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                prereqPathId = await SeedPathAsync(context, "Fire");
                var prereqProficiencyId = await SeedProficiencyAsync(context, prereqPathId, "Fire Magic");
                var gatewayPathId = await SeedPathAsync(context, "Inferno");
                var gatewayProficiencyId = await SeedProficiencyAsync(context, gatewayPathId, "Inferno Magic");
                await SeedPrerequisiteAsync(context, gatewayProficiencyId, prereqProficiencyId);
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminPaths>();

            var result = admin.SavePaths([RetirePathChange(prereqPathId, "Fire")]);

            Assert.False(result.Succeeded);
            Assert.Contains("soft-lock", result.ErrorMessage);
        }

        [Fact]
        public async Task SavePaths_RetiringPathThatGatesOnlyRetiredGateway_Succeeds()
        {
            int prereqPathId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                prereqPathId = await SeedPathAsync(context, "Fire");
                var prereqProficiencyId = await SeedProficiencyAsync(context, prereqPathId, "Fire Magic");
                // The dependent gateway's path is already retired, so retiring its prerequisite's path
                // soft-locks nothing live.
                var gatewayPathId = await SeedPathAsync(context, "Inferno", retired: true);
                var gatewayProficiencyId = await SeedProficiencyAsync(context, gatewayPathId, "Inferno Magic");
                await SeedPrerequisiteAsync(context, gatewayProficiencyId, prereqProficiencyId);
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminPaths>();

            Assert.True(admin.SavePaths([RetirePathChange(prereqPathId, "Fire")]).Succeeded);
        }

        [Fact]
        public async Task SavePaths_RetiringPathThatGatesNothing_Succeeds()
        {
            int pathId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                pathId = await SeedPathAsync(context, "Fire");
                await SeedProficiencyAsync(context, pathId, "Fire Magic");
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminPaths>();

            Assert.True(admin.SavePaths([RetirePathChange(pathId, "Fire")]).Succeeded);
        }

        [Fact]
        public async Task SavePaths_RetiringBothThePrerequisiteAndGatewayPaths_Succeeds()
        {
            int prereqPathId, gatewayPathId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                prereqPathId = await SeedPathAsync(context, "Fire");
                var prereqProficiencyId = await SeedProficiencyAsync(context, prereqPathId, "Fire Magic");
                gatewayPathId = await SeedPathAsync(context, "Inferno");
                var gatewayProficiencyId = await SeedProficiencyAsync(context, gatewayPathId, "Inferno Magic");
                await SeedPrerequisiteAsync(context, gatewayProficiencyId, prereqProficiencyId);
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminPaths>();

            // Retiring the gateway's path in the same batch leaves no live dependent, so the prerequisite
            // path can retire too.
            var result = admin.SavePaths(
            [
                RetirePathChange(prereqPathId, "Fire"),
                RetirePathChange(gatewayPathId, "Inferno"),
            ]);

            Assert.True(result.Succeeded);
        }

        /// <summary>Seeds a path with a single tier (a proficiency at ordinal 0) and returns that tier.</summary>
        private async Task<Entities.Proficiency> SeedPathWithTierAsync(IServiceScope scope)
        {
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var path = new Entities.Path { Name = "Fire", Description = "", FalloffBase = 0.3m };
            context.Paths.Add(path);
            await context.SaveChangesAsync(CancellationToken);

            var proficiency = new Entities.Proficiency
            {
                Name = "Fire Magic",
                Description = "",
                IconPath = "",
                Word = "",
                Pronunciation = "",
                Translation = "",
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

        private async Task<Entities.Skill> SeedSkillAsync(GameContext context)
        {
            var skill = new Entities.Skill
            {
                Name = "Fireball",
                Description = "",
                IconPath = "",
                Word = "",
                Pronunciation = "",
                Translation = "",
                BaseDamage = 1m,
                CooldownMs = 1000,
                Acquisition = (int)ESkillAcquisition.Player,
                SkillDamageMultipliers = [],
                SkillEffects = [],
                RarityId = (int)ERarity.Common,
            };
            context.Skills.Add(skill);
            await context.SaveChangesAsync(CancellationToken);
            return skill;
        }

        private async Task<int> SeedPathAsync(GameContext context, string name, bool retired = false)
        {
            var path = new Entities.Path
            {
                Name = name,
                Description = "",
                FalloffBase = 0.3m,
                RetiredAt = retired ? DateTime.UtcNow : null,
            };
            context.Paths.Add(path);
            await context.SaveChangesAsync(CancellationToken);
            return path.Id;
        }

        private async Task<int> SeedProficiencyAsync(GameContext context, int pathId, string name)
        {
            var proficiency = new Entities.Proficiency
            {
                Name = name,
                Description = "",
                IconPath = "",
                Word = "",
                Pronunciation = "",
                Translation = "",
                PathId = pathId,
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
            return proficiency.Id;
        }

        private async Task SeedPrerequisiteAsync(GameContext context, int gatewayProficiencyId, int prerequisiteProficiencyId)
        {
            context.ProficiencyPrerequisites.Add(new Entities.ProficiencyPrerequisite
            {
                ProficiencyId = gatewayProficiencyId,
                PrerequisiteProficiencyId = prerequisiteProficiencyId,
            });
            await context.SaveChangesAsync(CancellationToken);
        }

        private static Change<Contracts.Path> RetirePathChange(int id, string name) => new()
        {
            ChangeType = EChangeType.Edit,
            Item = new Contracts.Path
            {
                Id = id,
                Name = name,
                Description = "",
                FalloffBase = 0.3m,
                RetiredAt = DateTime.UtcNow,
                Contributions = [],
            },
        };

        private static Contracts.Path NewPath(int id = 0, string name = "Fire") => new()
        {
            Id = id,
            Name = name,
            Description = "",
            FalloffBase = 0.3m,
            Contributions = [],
        };
    }
}
