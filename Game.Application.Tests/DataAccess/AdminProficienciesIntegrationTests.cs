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
    /// Exercises <see cref="IAdminProficiencies"/>: the retire-only identity save, the anti-tamper
    /// Player-acquirable skill validation on seed/reward skills, the prerequisite guards, and the
    /// child-collection reconcilers. Seed, write, and assert each use a separate DI scope, as a real
    /// admin call does.
    /// </summary>
    [Collection("Integration")]
    public class AdminProficienciesIntegrationTests : ApplicationIntegrationTestBase
    {
        public AdminProficienciesIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task SaveProficiencies_AddsANewProficiency()
        {
            int pathId;
            using (var seedScope = CreateScope())
            {
                pathId = (await SeedPathAsync(seedScope)).Id;
            }
            await ReloadReferenceCachesAsync();

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminProficiencies>();
                Assert.True(admin.SaveProficiencies(
                [
                    new Change<Contracts.Proficiency> { ChangeType = EChangeType.Add, Item = NewProficiency(pathId: pathId) },
                ]).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using var assertScope = CreateScope();
            var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            Assert.Contains(await context.Proficiencies.ToListAsync(CancellationToken), p => p.Name == "Blades");
        }

        [Fact]
        public async Task SaveProficiencies_EditOutOfRangeId_ReturnsNotFound()
        {
            int pathId;
            using (var seedScope = CreateScope())
            {
                pathId = (await SeedPathAsync(seedScope)).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminProficiencies>();

            var result = admin.SaveProficiencies(
            [
                new Change<Contracts.Proficiency> { ChangeType = EChangeType.Edit, Item = NewProficiency(id: 99999, pathId: pathId) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("Proficiency not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveProficiencies_UnknownPath_ReturnsFailure()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminProficiencies>();

            var result = admin.SaveProficiencies(
            [
                new Change<Contracts.Proficiency> { ChangeType = EChangeType.Add, Item = NewProficiency(pathId: 99999) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Contains("Path 99999 does not exist", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveProficiencies_Delete_ReturnsRetiredNotDeleted()
        {
            int proficiencyId;
            using (var seedScope = CreateScope())
            {
                proficiencyId = (await SeedProficiencyAsync(seedScope)).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminProficiencies>();

            var result = admin.SaveProficiencies(
            [
                new Change<Contracts.Proficiency> { ChangeType = EChangeType.Delete, Item = NewProficiency(id: proficiencyId) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Contains("retired, not deleted", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveProficiencies_SeedSkillNotPlayerAcquirable_ReturnsFailure()
        {
            int skillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                skillId = (await SeedSkillAsync(context, ESkillAcquisition.Enemy)).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminProficiencies>();

            var result = admin.SaveProficiencies(
            [
                new Change<Contracts.Proficiency> { ChangeType = EChangeType.Add, Item = NewProficiency(seedSkillId: skillId) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Contains("not flagged as Player-acquirable", result.ErrorMessage);
        }

        [Fact]
        public void SetModifiers_UnknownProficiency_ReturnsNotFound()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminProficiencies>();

            var result = admin.SetModifiers(new SetProficiencyModifiersData { Id = 99999, Modifiers = [] });

            Assert.False(result.Succeeded);
            Assert.Equal("Proficiency not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task SetModifiers_ReconcilesAddEditDelete()
        {
            int proficiencyId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var proficiency = await SeedProficiencyAsync(seedScope);
                proficiencyId = proficiency.Id;

                context.ProficiencyLevelModifiers.AddRange(
                    new Entities.ProficiencyLevelModifier
                    {
                        ProficiencyId = proficiencyId,
                        Level = 1,
                        AttributeId = (int)EAttribute.Strength,
                        ModifierType = (int)EModifierType.Additive,
                        Amount = 1m,
                    },
                    new Entities.ProficiencyLevelModifier
                    {
                        ProficiencyId = proficiencyId,
                        Level = 2,
                        AttributeId = (int)EAttribute.Endurance,
                        ModifierType = (int)EModifierType.Additive,
                        Amount = 2m,
                    });
                await context.SaveChangesAsync(CancellationToken);
            }
            await ReloadReferenceCachesAsync();

            // Desired set: keep (1, Strength) but edit its amount, add (3, Agility), drop (2, Endurance).
            var data = new SetProficiencyModifiersData
            {
                Id = proficiencyId,
                Modifiers =
                [
                    Modifier(level: 1, EAttribute.Strength, 99m),
                    Modifier(level: 3, EAttribute.Agility, 5m),
                ],
            };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminProficiencies>();
                Assert.True(admin.SetModifiers(data).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using var assertScope = CreateScope();
            var assertContext = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            var modifiers = await assertContext.ProficiencyLevelModifiers
                .Where(m => m.ProficiencyId == proficiencyId)
                .ToListAsync(CancellationToken);

            Assert.Equal(2, modifiers.Count);
            Assert.DoesNotContain(modifiers, m => m.AttributeId == (int)EAttribute.Endurance);
            Assert.Equal(99m, modifiers.Single(m => m.Level == 1 && m.AttributeId == (int)EAttribute.Strength).Amount);
            Assert.Equal(5m, modifiers.Single(m => m.Level == 3 && m.AttributeId == (int)EAttribute.Agility).Amount);
        }

        [Theory]
        [InlineData(-1)] // below the just-opened state, never a payout level
        [InlineData(11)] // past the cap (MaxLevel 10), so it would never fire
        public async Task SetModifiers_LevelOutOfRange_ReturnsFailure(int level)
        {
            int proficiencyId;
            using (var seedScope = CreateScope())
            {
                proficiencyId = (await SeedProficiencyAsync(seedScope)).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminProficiencies>();

            var result = admin.SetModifiers(new SetProficiencyModifiersData
            {
                Id = proficiencyId,
                Modifiers = [Modifier(level, EAttribute.Strength, 1m)],
            });

            Assert.False(result.Succeeded);
            Assert.Contains("out of range", result.ErrorMessage);
        }

        [Fact]
        public async Task SetModifiers_LevelZero_IsAllowedAndPersisted()
        {
            // Level 0 is the just-opened state: a modifier authored there is an "on-open" bonus that the domain
            // honors cumulatively (Proficiency.ModifiersForLevel uses l.Level <= level). The authoring path must
            // accept what the domain is built and tested to apply.
            int proficiencyId;
            using (var seedScope = CreateScope())
            {
                proficiencyId = (await SeedProficiencyAsync(seedScope)).Id;
            }
            await ReloadReferenceCachesAsync();

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminProficiencies>();
                Assert.True(admin.SetModifiers(new SetProficiencyModifiersData
                {
                    Id = proficiencyId,
                    Modifiers = [Modifier(level: 0, EAttribute.Strength, 7m)],
                }).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using var assertScope = CreateScope();
            var assertContext = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            var modifier = await assertContext.ProficiencyLevelModifiers
                .SingleAsync(m => m.ProficiencyId == proficiencyId, CancellationToken);
            Assert.Equal(0, modifier.Level);
            Assert.Equal(7m, modifier.Amount);
        }

        [Fact]
        public async Task SetRewards_LevelOutOfRange_ReturnsFailureBeforeTheSkillCheck()
        {
            int proficiencyId;
            using (var seedScope = CreateScope())
            {
                proficiencyId = (await SeedProficiencyAsync(seedScope)).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminProficiencies>();

            // A level past the cap is rejected on the level check (so the unresolved skill id is never reached).
            var result = admin.SetRewards(new SetProficiencyRewardsData
            {
                Id = proficiencyId,
                Rewards = [new Contracts.ProficiencyLevelReward { Level = 11, RewardSkillId = 99999 }],
            });

            Assert.False(result.Succeeded);
            Assert.Contains("out of range", result.ErrorMessage);
        }

        [Fact]
        public async Task SetRewards_LevelZero_ReturnsFailure()
        {
            // Unlike modifiers, a level-0 reward is rejected: a reward fires only by crossing a milestone
            // (RewardSkillsCrossed uses l.Level > fromLevel), which a level-0 reward never does — the on-open
            // grant is the proficiency's SeedSkillId. The level check fires before the skill id is resolved.
            int proficiencyId;
            using (var seedScope = CreateScope())
            {
                proficiencyId = (await SeedProficiencyAsync(seedScope)).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminProficiencies>();

            var result = admin.SetRewards(new SetProficiencyRewardsData
            {
                Id = proficiencyId,
                Rewards = [new Contracts.ProficiencyLevelReward { Level = 0, RewardSkillId = 99999 }],
            });

            Assert.False(result.Succeeded);
            Assert.Contains("out of range", result.ErrorMessage);
        }

        [Fact]
        public async Task SetRewards_RewardSkillNotPlayerAcquirable_ReturnsFailure()
        {
            int proficiencyId, skillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                skillId = (await SeedSkillAsync(context, ESkillAcquisition.Enemy)).Id;
                proficiencyId = (await SeedProficiencyAsync(seedScope)).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminProficiencies>();

            var result = admin.SetRewards(new SetProficiencyRewardsData
            {
                Id = proficiencyId,
                Rewards = [new Contracts.ProficiencyLevelReward { Level = 5, RewardSkillId = skillId }],
            });

            Assert.False(result.Succeeded);
            Assert.Contains("not flagged as Player-acquirable", result.ErrorMessage);
        }

        [Fact]
        public async Task SetPrerequisites_SelfReference_ReturnsFailure()
        {
            int proficiencyId;
            using (var seedScope = CreateScope())
            {
                proficiencyId = (await SeedProficiencyAsync(seedScope)).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminProficiencies>();

            var result = admin.SetPrerequisites(new SetProficiencyPrerequisitesData
            {
                Id = proficiencyId,
                PrerequisiteIds = [proficiencyId],
            });

            Assert.False(result.Succeeded);
            Assert.Contains("cannot be its own prerequisite", result.ErrorMessage);
        }

        [Fact]
        public async Task SetPrerequisites_WouldFormACycle_ReturnsFailure()
        {
            int gatedId, prerequisiteId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var gated = await SeedProficiencyAsync(seedScope);
                var prerequisite = await SeedProficiencyAsync(seedScope);
                gatedId = gated.Id;
                prerequisiteId = prerequisite.Id;

                // gated already depends on prerequisite; making prerequisite depend on gated closes a cycle.
                context.Set<Entities.ProficiencyPrerequisite>().Add(new Entities.ProficiencyPrerequisite
                {
                    ProficiencyId = gatedId,
                    PrerequisiteProficiencyId = prerequisiteId,
                });
                await context.SaveChangesAsync(CancellationToken);
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminProficiencies>();

            var result = admin.SetPrerequisites(new SetProficiencyPrerequisitesData
            {
                Id = prerequisiteId,
                PrerequisiteIds = [gatedId],
            });

            Assert.False(result.Succeeded);
            Assert.Contains("cycle", result.ErrorMessage);
        }

        private async Task<Entities.Path> SeedPathAsync(IServiceScope scope)
        {
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var path = new Entities.Path { Name = "Fire", Description = "", FalloffBase = 0.3m };
            context.Paths.Add(path);
            await context.SaveChangesAsync(CancellationToken);
            return path;
        }

        private async Task<Entities.Proficiency> SeedProficiencyAsync(IServiceScope scope)
        {
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var path = await SeedPathAsync(scope);
            var proficiency = new Entities.Proficiency
            {
                Name = "Blades",
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

        private static Contracts.ProficiencyLevelModifier Modifier(int level, EAttribute attribute, decimal amount) => new()
        {
            Level = level,
            AttributeId = attribute,
            ModifierTypeId = EModifierType.Additive,
            Amount = amount,
        };

        private static Contracts.Proficiency NewProficiency(int id = 0, int? seedSkillId = null, string name = "Blades", int pathId = 0) => new()
        {
            Id = id,
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
            SeedSkillId = seedSkillId,
            LevelModifiers = [],
            LevelRewards = [],
            PrerequisiteIds = [],
        };
    }
}
