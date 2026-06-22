using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Exercises <see cref="IAdminSkills"/> change-set write paths: the effect Edit/Delete membership guard
    /// and the damage-multiplier change-set upsert. Seeding, the admin write, and the assertion each use a
    /// separate DI scope so the write runs against an empty change tracker, as a real admin call does.
    /// </summary>
    [Collection("Integration")]
    public class AdminSkillsIntegrationTests : ApplicationIntegrationTestBase
    {
        public AdminSkillsIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public void SetEffects_UnknownSkill_ReturnsNotFound()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminSkills>();

            var result = admin.SetEffects(new SetSkillEffectsData { Id = 99999, Changes = [] });

            Assert.False(result.Succeeded);
            Assert.Equal("Skill not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task SetEffects_AddsEditsAndDeletes()
        {
            int skillId, editedEffectId, removedEffectId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var skill = await TestDataSeeder.CreateSkillAsync(context);
                var editedEffect = await SeedEffectAsync(context, skill.Id, EAttribute.Strength, amount: 10m);
                var removedEffect = await SeedEffectAsync(context, skill.Id, EAttribute.Endurance, amount: 20m);

                skillId = skill.Id;
                editedEffectId = editedEffect.Id;
                removedEffectId = removedEffect.Id;
            }
            await ReloadReferenceCachesAsync();

            var data = new SetSkillEffectsData
            {
                Id = skillId,
                Changes =
                [
                    new Change<Contracts.SkillEffect>
                    {
                        ChangeType = EChangeType.Add,
                        Item = NewEffect(EAttribute.Intellect, amount: 30m,
                            scalingAttribute: EAttribute.Dexterity, scalingAmount: 0.5m),
                    },
                    new Change<Contracts.SkillEffect>
                    {
                        ChangeType = EChangeType.Edit,
                        Item = NewEffect(EAttribute.Strength, amount: 99m, id: editedEffectId),
                    },
                    new Change<Contracts.SkillEffect>
                    {
                        ChangeType = EChangeType.Delete,
                        Item = NewEffect(EAttribute.Endurance, amount: 20m, id: removedEffectId),
                    },
                ],
            };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminSkills>();
                Assert.True(admin.SetEffects(data).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var effects = await context.SkillEffects
                    .Where(e => e.SkillId == skillId)
                    .ToListAsync(CancellationToken);

                Assert.Equal(2, effects.Count);
                Assert.DoesNotContain(effects, e => e.Id == removedEffectId);
                Assert.Equal(99m, effects.Single(e => e.Id == editedEffectId).Amount);
                var added = effects.Single(e => e.AttributeId == (int)EAttribute.Intellect);
                Assert.Equal(30m, added.Amount);
                // The added effect's caster-attribute scaling round-trips through persistence.
                Assert.Equal((int)EAttribute.Dexterity, added.ScalingAttributeId);
                Assert.Equal(0.5m, added.ScalingAmount);
            }
        }

        [Fact]
        public async Task SetEffects_EditAndDeleteOfUnknownEffect_AreGuardedNoOps()
        {
            int skillId, effectId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var skill = await TestDataSeeder.CreateSkillAsync(context);
                var effect = await SeedEffectAsync(context, skill.Id, EAttribute.Strength, amount: 10m);

                skillId = skill.Id;
                effectId = effect.Id;
            }
            await ReloadReferenceCachesAsync();

            // An edit/delete naming an effect id the skill does not have must reconcile away, leaving the
            // real effect untouched — never a silent EF 0-row update/delete.
            var data = new SetSkillEffectsData
            {
                Id = skillId,
                Changes =
                [
                    new Change<Contracts.SkillEffect>
                    {
                        ChangeType = EChangeType.Edit,
                        Item = NewEffect(EAttribute.Strength, amount: 999m, id: 99999),
                    },
                    new Change<Contracts.SkillEffect>
                    {
                        ChangeType = EChangeType.Delete,
                        Item = NewEffect(EAttribute.Strength, amount: 10m, id: 88888),
                    },
                ],
            };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminSkills>();
                Assert.True(admin.SetEffects(data).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var effects = await context.SkillEffects
                    .Where(e => e.SkillId == skillId)
                    .ToListAsync(CancellationToken);

                var effect = Assert.Single(effects);
                Assert.Equal(effectId, effect.Id);
                Assert.Equal(10m, effect.Amount);
            }
        }

        [Fact]
        public async Task SetMultipliers_AddOfAlreadyPresentMultiplier_Upserts()
        {
            int skillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                // CreateSkillAsync seeds a single Strength damage multiplier (1.0).
                var skill = await TestDataSeeder.CreateSkillAsync(context);
                skillId = skill.Id;
            }
            await ReloadReferenceCachesAsync();

            // An Add of a multiplier the skill already has must upsert, not duplicate-insert into a
            // composite-PK violation at commit.
            var data = new AddEditAttributesData
            {
                Id = skillId,
                Changes =
                [
                    new Change<Contracts.BattlerAttribute>
                    {
                        ChangeType = EChangeType.Add,
                        Item = new Contracts.BattlerAttribute { AttributeId = EAttribute.Strength, Amount = 4m },
                    },
                ],
            };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminSkills>();
                Assert.True(admin.SetMultipliers(data).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var multipliers = await context.SkillDamageMultipliers
                    .Where(m => m.SkillId == skillId)
                    .ToListAsync(CancellationToken);

                var multiplier = Assert.Single(multipliers);
                Assert.Equal((int)EAttribute.Strength, multiplier.AttributeId);
                Assert.Equal(4m, multiplier.Multiplier);
            }
        }

        [Fact]
        public async Task SaveSkills_AddAndEdit_PersistsRarity()
        {
            // Add a new skill authored as Legendary — its rarity persists on insert.
            using (var addScope = CreateScope())
            {
                var admin = addScope.ServiceProvider.GetRequiredService<IAdminSkills>();
                var result = admin.SaveSkills(
                [
                    new Change<Contracts.Skill>
                    {
                        ChangeType = EChangeType.Add,
                        Item = NewSkill("Rarity Skill", ERarity.Legendary),
                    },
                ]);
                Assert.True(result.Succeeded);
                await addScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            int skillId;
            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var skill = await context.Skills.SingleAsync(s => s.Name == "Rarity Skill", CancellationToken);
                Assert.Equal((int)ERarity.Legendary, skill.RarityId);
                skillId = skill.Id;
            }
            // The edit's existence guard reads the cache, so refresh it before re-tiering the new skill.
            await ReloadReferenceCachesAsync();

            // Re-tiering to Mythic persists on edit.
            using (var editScope = CreateScope())
            {
                var admin = editScope.ServiceProvider.GetRequiredService<IAdminSkills>();
                var result = admin.SaveSkills(
                [
                    new Change<Contracts.Skill>
                    {
                        ChangeType = EChangeType.Edit,
                        Item = NewSkill("Rarity Skill", ERarity.Mythic, id: skillId),
                    },
                ]);
                Assert.True(result.Succeeded);
                await editScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var skill = await context.Skills.SingleAsync(s => s.Id == skillId, CancellationToken);
                Assert.Equal((int)ERarity.Mythic, skill.RarityId);
            }
        }

        private static Contracts.Skill NewSkill(string name, ERarity rarity, int id = 0)
        {
            return new Contracts.Skill
            {
                Id = id,
                Name = name,
                BaseDamage = 5m,
                Description = "",
                CooldownMs = 1000,
                IconPath = "",
                RarityId = rarity,
                Acquisition = ESkillAcquisition.Player,
                DamageMultipliers = [],
                Effects = [],
            };
        }

        private static async Task<Entities.SkillEffect> SeedEffectAsync(
            GameContext context, int skillId, EAttribute attribute, decimal amount)
        {
            var effect = new Entities.SkillEffect
            {
                SkillId = skillId,
                Target = (int)ESkillEffectTarget.Self,
                AttributeId = (int)attribute,
                ModifierType = (int)EModifierType.Additive,
                Amount = amount,
                DurationMs = 1000,
                ScalingAttributeId = (int)EAttribute.Strength,
                ScalingAmount = 0m,
            };
            context.SkillEffects.Add(effect);
            await context.SaveChangesAsync();
            return effect;
        }

        private static Contracts.SkillEffect NewEffect(
            EAttribute attribute, decimal amount, int id = 0,
            EAttribute scalingAttribute = EAttribute.Strength, decimal scalingAmount = 0m)
        {
            return new Contracts.SkillEffect
            {
                Id = id,
                Target = ESkillEffectTarget.Self,
                AttributeId = attribute,
                ModifierTypeId = EModifierType.Additive,
                Amount = amount,
                DurationMs = 1000,
                ScalingAttributeId = scalingAttribute,
                ScalingAmount = scalingAmount,
            };
        }
    }
}
