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

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Exercises <see cref="IAdminClasses"/>: the retire-only identity save (with the signature-passive
    /// scalar fields) and the starter-skill / starter-equipment / attribute-distribution reconcilers with
    /// their anti-tamper guards. Seed, write, and assert each use a separate DI scope, as a real admin call
    /// does.
    /// </summary>
    [Collection("Integration")]
    public class AdminClassesIntegrationTests : ApplicationIntegrationTestBase
    {
        public AdminClassesIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task SaveClasses_AddsANewClass()
        {
            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminClasses>();
                Assert.True(admin.SaveClasses(
                [
                    new Change<Contracts.Class> { ChangeType = EChangeType.Add, Item = NewClass() },
                ]).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using var assertScope = CreateScope();
            var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            // The integration DB accumulates across tests, so assert the added class is present (matching its
            // distinctive saved fields) rather than asserting it is the only one.
            var saved = await context.Classes.Where(c => c.Name == "Warrior").ToListAsync(CancellationToken);
            Assert.Contains(saved, c => c.Word == "aenkor"
                && c.PassiveAttributeId == (int)EAttribute.Endurance
                && c.PassiveAmount == 2m);
        }

        [Fact]
        public async Task SaveClasses_EditMissingClass_ReturnsNotFound()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminClasses>();

            var result = admin.SaveClasses(
            [
                new Change<Contracts.Class> { ChangeType = EChangeType.Edit, Item = NewClass(id: 9999) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Contains("not found", result.ErrorMessage);
        }

        [Fact]
        public async Task SetStarterSkills_NonPlayerSkill_ReturnsFailure()
        {
            int classId, enemySkillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                classId = (await TestDataSeeder.CreateClassAsync(context, "Warrior")).Id;
                enemySkillId = (await TestDataSeeder.CreateSkillAsync(context, "Snarl", acquisition: ESkillAcquisition.Enemy)).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminClasses>();

            var result = admin.SetStarterSkills(new SetClassStarterSkillsData { ClassId = classId, SkillIds = [enemySkillId] });

            Assert.False(result.Succeeded);
            Assert.Contains("Player-acquirable", result.ErrorMessage);
        }

        [Fact]
        public async Task SetStarterSkills_ReconcilesSkills()
        {
            int classId, skillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                classId = (await TestDataSeeder.CreateClassAsync(context, "Warrior")).Id;
                skillId = (await TestDataSeeder.CreateSkillAsync(context, "Cleave")).Id;
            }
            await ReloadReferenceCachesAsync();

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminClasses>();
                Assert.True(admin.SetStarterSkills(new SetClassStarterSkillsData { ClassId = classId, SkillIds = [skillId] }).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using var assertScope = CreateScope();
            var context2 = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            var starter = Assert.Single(await context2.ClassStarterSkills.Where(s => s.ClassId == classId).ToListAsync(CancellationToken));
            Assert.Equal(skillId, starter.SkillId);
        }

        [Fact]
        public async Task SetStarterSkills_RemovesSkillsNoLongerDesired()
        {
            int classId, firstSkillId, secondSkillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                classId = (await TestDataSeeder.CreateClassAsync(context, "Warrior")).Id;
                firstSkillId = (await TestDataSeeder.CreateSkillAsync(context, "Cleave")).Id;
                secondSkillId = (await TestDataSeeder.CreateSkillAsync(context, "Bash")).Id;
            }
            await ReloadReferenceCachesAsync();

            using (var firstScope = CreateScope())
            {
                var admin = firstScope.ServiceProvider.GetRequiredService<IAdminClasses>();
                Assert.True(admin.SetStarterSkills(new SetClassStarterSkillsData { ClassId = classId, SkillIds = [firstSkillId] }).Succeeded);
                await firstScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }
            // Reload so the reconciler sees the first skill as the existing set, then swap it for the second.
            await ReloadReferenceCachesAsync();

            using (var secondScope = CreateScope())
            {
                var admin = secondScope.ServiceProvider.GetRequiredService<IAdminClasses>();
                Assert.True(admin.SetStarterSkills(new SetClassStarterSkillsData { ClassId = classId, SkillIds = [secondSkillId] }).Succeeded);
                await secondScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using var assertScope = CreateScope();
            var context2 = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            // The reconciler deleted the no-longer-desired first skill and inserted the second.
            var starter = Assert.Single(await context2.ClassStarterSkills.Where(s => s.ClassId == classId).ToListAsync(CancellationToken));
            Assert.Equal(secondSkillId, starter.SkillId);
        }

        [Fact]
        public async Task SaveClasses_RetiredClass_StaysResolvableById()
        {
            int classId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                classId = (await TestDataSeeder.CreateClassAsync(context, "Warrior")).Id;
            }
            await ReloadReferenceCachesAsync();

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminClasses>();
                var retire = NewClass(id: classId);
                retire.RetiredAt = DateTime.UtcNow;
                Assert.True(admin.SaveClasses([new Change<Contracts.Class> { ChangeType = EChangeType.Edit, Item = retire }]).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using var assertScope = CreateScope();
            var context2 = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            // Retirement is a soft flag, not a delete: the row is still present and resolvable by id.
            var saved = await context2.Classes.FindAsync([classId], CancellationToken);
            Assert.NotNull(saved);
            Assert.NotNull(saved.RetiredAt);
        }

        [Fact]
        public async Task SetStarterEquipment_ItemCategoryMismatch_ReturnsFailure()
        {
            int classId, weaponItemId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                classId = (await TestDataSeeder.CreateClassAsync(context, "Warrior")).Id;
                // CreateItemAsync defaults to the Weapon category; equipping it into the Helm slot is a mismatch.
                weaponItemId = (await TestDataSeeder.CreateItemAsync(context, "Sword")).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminClasses>();

            var result = admin.SetStarterEquipment(new SetClassStarterEquipmentData
            {
                ClassId = classId,
                Equipment = [new Contracts.ClassStarterEquipment { ItemId = weaponItemId, EquipmentSlot = EEquipmentSlot.HelmSlot }],
            });

            Assert.False(result.Succeeded);
            Assert.Contains("cannot be equipped", result.ErrorMessage);
        }

        [Fact]
        public async Task SetStarterEquipment_ReconcilesEquipment()
        {
            int classId, weaponItemId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                classId = (await TestDataSeeder.CreateClassAsync(context, "Warrior")).Id;
                weaponItemId = (await TestDataSeeder.CreateItemAsync(context, "Sword")).Id;
            }
            await ReloadReferenceCachesAsync();

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminClasses>();
                Assert.True(admin.SetStarterEquipment(new SetClassStarterEquipmentData
                {
                    ClassId = classId,
                    Equipment = [new Contracts.ClassStarterEquipment { ItemId = weaponItemId, EquipmentSlot = EEquipmentSlot.WeaponSlot }],
                }).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using var assertScope = CreateScope();
            var context2 = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            var equipped = Assert.Single(await context2.ClassStarterEquipment.Where(e => e.ClassId == classId).ToListAsync(CancellationToken));
            Assert.Equal(weaponItemId, equipped.ItemId);
            Assert.Equal((int)EEquipmentSlot.WeaponSlot, equipped.EquipmentSlotId);
        }

        [Fact]
        public async Task SetAttributeDistributions_ReconcilesDistributions()
        {
            int classId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                classId = (await TestDataSeeder.CreateClassAsync(context, "Warrior")).Id;
            }
            await ReloadReferenceCachesAsync();

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminClasses>();
                Assert.True(admin.SetAttributeDistributions(new SetClassAttributeDistributionsData
                {
                    ClassId = classId,
                    AttributeDistributions =
                    [
                        new Contracts.AttributeDistribution { AttributeId = EAttribute.Strength, BaseAmount = 10m, AmountPerLevel = 2m },
                    ],
                }).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using var assertScope = CreateScope();
            var context2 = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            var distribution = Assert.Single(await context2.ClassAttributeDistributions.Where(ad => ad.ClassId == classId).ToListAsync(CancellationToken));
            Assert.Equal((int)EAttribute.Strength, distribution.AttributeId);
            Assert.Equal(10m, distribution.BaseAmount);
            Assert.Equal(2m, distribution.AmountPerLevel);
        }

        [Fact]
        public async Task SetStarterSkills_MissingClass_ReturnsNotFound()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminClasses>();

            var result = admin.SetStarterSkills(new SetClassStarterSkillsData { ClassId = 9999, SkillIds = [] });

            Assert.False(result.Succeeded);
            Assert.Contains("not found", result.ErrorMessage);
        }

        private static Contracts.Class NewClass(int id = 0, string name = "Warrior") => new()
        {
            Id = id,
            Name = name,
            Description = "",
            Word = "aenkor",
            PassiveAttributeId = EAttribute.Endurance,
            PassiveAmount = 2m,
            PassiveScalingAttributeId = null,
            PassiveScalingAmount = 0m,
            PassiveModifierType = EModifierType.Additive,
            StarterSkillIds = [],
            StarterEquipment = [],
            AttributeDistributions = [],
        };
    }
}
