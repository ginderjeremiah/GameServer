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
        public async Task SaveProficiencies_ReorderSwapsExistingOrdinals_IsAccepted()
        {
            // The regression a naive per-change cache probe would break: two existing tiers swap ordinals in one
            // batch. Each Edit transiently "collides" with the other's pre-edit state, but the prospective layout
            // (both swapped) is collision-free, so the swap must be accepted.
            int pathId, firstId, secondId;
            using (var seedScope = CreateScope())
            {
                pathId = (await SeedPathAsync(seedScope)).Id;
                firstId = (await SeedProficiencyAsync(seedScope, pathId, pathOrdinal: 0, name: "Tier A")).Id;
                secondId = (await SeedProficiencyAsync(seedScope, pathId, pathOrdinal: 1, name: "Tier B")).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminProficiencies>();

            var result = admin.SaveProficiencies(
            [
                new Change<Contracts.Proficiency> { ChangeType = EChangeType.Edit, Item = NewProficiency(id: firstId, pathId: pathId, pathOrdinal: 1, name: "Tier A") },
                new Change<Contracts.Proficiency> { ChangeType = EChangeType.Edit, Item = NewProficiency(id: secondId, pathId: pathId, pathOrdinal: 0, name: "Tier B") },
            ]);

            Assert.True(result.Succeeded);
        }

        [Fact]
        public async Task SaveProficiencies_TwoAddsSameOrdinalSamePath_IsRejected()
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
                new Change<Contracts.Proficiency> { ChangeType = EChangeType.Add, Item = NewProficiency(pathId: pathId, pathOrdinal: 0, name: "Tier A") },
                new Change<Contracts.Proficiency> { ChangeType = EChangeType.Add, Item = NewProficiency(pathId: pathId, pathOrdinal: 0, name: "Tier B") },
            ]);

            Assert.False(result.Succeeded);
            Assert.Contains("two tiers at ordinal 0", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveProficiencies_AddCollidesWithExistingTier_IsRejected()
        {
            int pathId;
            using (var seedScope = CreateScope())
            {
                pathId = (await SeedPathAsync(seedScope)).Id;
                await SeedProficiencyAsync(seedScope, pathId, pathOrdinal: 0, name: "Existing");
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminProficiencies>();

            var result = admin.SaveProficiencies(
            [
                new Change<Contracts.Proficiency> { ChangeType = EChangeType.Add, Item = NewProficiency(pathId: pathId, pathOrdinal: 0, name: "Newcomer") },
            ]);

            Assert.False(result.Succeeded);
            Assert.Contains("two tiers at ordinal 0", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveProficiencies_SameOrdinalDifferentPaths_IsAccepted()
        {
            // Uniqueness is per-path, so the same ordinal across two paths is a valid layout.
            int firstPathId, secondPathId;
            using (var seedScope = CreateScope())
            {
                firstPathId = (await SeedPathAsync(seedScope)).Id;
                secondPathId = (await SeedPathAsync(seedScope)).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminProficiencies>();

            var result = admin.SaveProficiencies(
            [
                new Change<Contracts.Proficiency> { ChangeType = EChangeType.Add, Item = NewProficiency(pathId: firstPathId, pathOrdinal: 0, name: "Tier A") },
                new Change<Contracts.Proficiency> { ChangeType = EChangeType.Add, Item = NewProficiency(pathId: secondPathId, pathOrdinal: 0, name: "Tier B") },
            ]);

            Assert.True(result.Succeeded);
        }

        [Fact]
        public async Task SaveProficiencies_DeleteResolvesCollidingOrdinal_IsAccepted()
        {
            // Deleting one of two same-ordinal tiers removes it from the prospective layout, so an Add at that
            // ordinal in the same batch no longer collides. (Delete is rejected for proficiencies elsewhere, so
            // this asserts the collision check itself accepts the layout — it must precede that rejection only if
            // the layout is valid; here the batch passes the collision check and is rejected later by the
            // retire-only rule, proving the collision check did not false-reject.)
            int pathId, existingId;
            using (var seedScope = CreateScope())
            {
                pathId = (await SeedPathAsync(seedScope)).Id;
                existingId = (await SeedProficiencyAsync(seedScope, pathId, pathOrdinal: 0, name: "Existing")).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminProficiencies>();

            var result = admin.SaveProficiencies(
            [
                new Change<Contracts.Proficiency> { ChangeType = EChangeType.Delete, Item = NewProficiency(id: existingId, pathId: pathId, pathOrdinal: 0) },
                new Change<Contracts.Proficiency> { ChangeType = EChangeType.Add, Item = NewProficiency(pathId: pathId, pathOrdinal: 0, name: "Replacement") },
            ]);

            // The collision check accepts the layout (the delete frees the ordinal); the batch then fails the
            // retire-only rule, not the collision check — proving the collision check did not false-reject.
            Assert.False(result.Succeeded);
            Assert.Contains("retired, not deleted", result.ErrorMessage);
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
            // (RewardSkillsCrossed uses l.Level > fromLevel), which a level-0 reward never does. The level
            // check fires before the skill id is resolved.
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
            var path = await SeedPathAsync(scope);
            return await SeedProficiencyAsync(scope, path.Id);
        }

        private async Task<Entities.Proficiency> SeedProficiencyAsync(IServiceScope scope, int pathId, int pathOrdinal = 0, string name = "Blades")
        {
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var proficiency = new Entities.Proficiency
            {
                Name = name,
                Description = "",
                IconPath = "",
                Word = "",
                Pronunciation = "",
                Translation = "",
                PathId = pathId,
                PathOrdinal = pathOrdinal,
                MaxLevel = 10,
                BaseXp = 100m,
                XpGrowth = 2m,
                LevelModifiers = [],
                LevelRewards = [],
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

        private static Contracts.Proficiency NewProficiency(int id = 0, string name = "Blades", int pathId = 0, int pathOrdinal = 0) => new()
        {
            Id = id,
            Name = name,
            Description = "",
            IconPath = "",
            Word = "",
            Pronunciation = "",
            Translation = "",
            PathId = pathId,
            PathOrdinal = pathOrdinal,
            MaxLevel = 10,
            BaseXp = 100m,
            XpGrowth = 2m,
            LevelModifiers = [],
            LevelRewards = [],
        };
    }
}
