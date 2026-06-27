using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Entities = Game.Infrastructure.Entities;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Pins the per-battle allocation reduction (#449): the gameplay reference reads return the cache's
    /// shared, pre-materialized instances rather than rebuilding a fresh graph (or copying the list) on every
    /// call. Each read is exercised through the DI-resolved repository against the eagerly-loaded snapshot.
    /// </summary>
    [Collection("Integration")]
    public class ReferenceDataSharedInstanceTests : ApplicationIntegrationTestBase
    {
        public ReferenceDataSharedInstanceTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task GetItem_ReturnsSharedInstanceAndCorrectData()
        {
            int itemId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                itemId = (await TestDataSeeder.CreateItemAsync(context, name: "Shared Item")).Id;
            }

            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var items = scope.ServiceProvider.GetRequiredService<IItems>();

            var first = items.GetItem(itemId);
            var second = items.GetItem(itemId);

            // The optimization: repeated reads hand back the same pre-materialized instance, not a fresh graph.
            Assert.Same(first, second);
            Assert.Equal(itemId, first.Id);
            Assert.Equal("Shared Item", first.Name);
            Assert.NotEmpty(first.Attributes);
        }

        [Fact]
        public async Task GetItemMod_ReturnsSharedInstanceAndCorrectData()
        {
            int modId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                modId = (await TestDataSeeder.CreateItemModAsync(context, name: "Shared Mod")).Id;
            }

            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var itemMods = scope.ServiceProvider.GetRequiredService<IItemMods>();

            var first = itemMods.GetItemMod(modId);
            var second = itemMods.GetItemMod(modId);

            Assert.Same(first, second);
            Assert.Equal(modId, first.Id);
            Assert.Equal("Shared Mod", first.Name);
            Assert.NotEmpty(first.Attributes);
        }

        [Fact]
        public async Task GetSkill_ReturnsSharedInstanceAndCorrectData()
        {
            int skillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                skillId = (await TestDataSeeder.CreateSkillAsync(context, name: "Shared Skill")).Id;
            }

            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var skills = scope.ServiceProvider.GetRequiredService<ISkills>();

            var first = skills.GetSkill(skillId);
            var second = skills.GetSkill(skillId);

            Assert.Same(first, second);
            Assert.Equal(skillId, first.Id);
            Assert.Equal("Shared Skill", first.Name);
            Assert.NotEmpty(first.DamageMultipliers);
        }

        [Fact]
        public async Task GetProficiency_ReturnsSharedInstanceAndCorrectData()
        {
            int proficiencyId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var path = new Entities.Path { Name = "Shared Path", Description = "", FalloffBase = 0.3m };
                context.Paths.Add(path);
                await context.SaveChangesAsync(CancellationToken);

                var proficiency = new Entities.Proficiency
                {
                    Name = "Shared Proficiency",
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
                await context.SaveChangesAsync(CancellationToken);
            }

            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var proficiencies = scope.ServiceProvider.GetRequiredService<IProficiencies>();

            var first = proficiencies.GetProficiency(proficiencyId);
            var second = proficiencies.GetProficiency(proficiencyId);

            // The optimization: repeated reads hand back the same pre-materialized instance, not a fresh graph.
            Assert.Same(first, second);
            Assert.Equal(proficiencyId, first.Id);
            Assert.Equal("Shared Proficiency", first.Name);
            Assert.NotEmpty(first.Levels);
        }

        [Fact]
        public async Task GetClass_ReturnsSharedInstanceAndCorrectData()
        {
            int classId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                classId = (await TestDataSeeder.CreateClassAsync(context, name: "Shared Class")).Id;
            }

            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var classes = scope.ServiceProvider.GetRequiredService<IClasses>();

            var first = classes.GetClass(classId);
            var second = classes.GetClass(classId);

            // The optimization: repeated reads hand back the same pre-materialized instance, not a fresh graph.
            Assert.NotNull(first);
            Assert.Same(first, second);
            Assert.Equal(classId, first.Id);
            Assert.Equal("Shared Class", first.Name);
            Assert.Equal(EAttribute.Strength, first.SignaturePassive.Attribute);
        }

        [Fact]
        public async Task GetDomainEnemy_ReusesPreMappedTemplateAcrossCalls()
        {
            int enemyId;
            int skillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                skillId = (await TestDataSeeder.CreateSkillAsync(context)).Id;
                enemyId = (await TestDataSeeder.CreateEnemyAsync(context, name: "Shared Enemy")).Id;
                await TestDataSeeder.LinkSkillToEnemyAsync(context, enemyId, skillId);
            }

            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var enemies = scope.ServiceProvider.GetRequiredService<IEnemies>();

            var first = enemies.GetDomainEnemy(enemyId, level: 3);
            var second = enemies.GetDomainEnemy(enemyId, level: 9);

            Assert.NotNull(first);
            Assert.NotNull(second);

            // The optimization (#584): each encounter gets its own level-parameterized instance, but the
            // pre-mapped available-skill graph is shared from the template rather than re-mapped per call.
            Assert.NotSame(first, second);
            Assert.Same(first.AvailableSkills, second.AvailableSkills);
            Assert.Equal(3, first.Level);
            Assert.Equal(9, second.Level);
            Assert.Equal(enemyId, first.Id);
            Assert.Equal("Shared Enemy", first.Name);
            Assert.Contains(first.AvailableSkills, s => s.Id == skillId);
        }

        [Fact]
        public async Task GetDomainZone_ReturnsSharedInstanceAndCorrectData()
        {
            int zoneId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                zoneId = (await TestDataSeeder.CreateZoneAsync(context, "Shared Zone", levelMin: 4, levelMax: 7)).Id;
            }

            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var zones = scope.ServiceProvider.GetRequiredService<IZones>();

            var first = zones.GetDomainZone(zoneId);
            var second = zones.GetDomainZone(zoneId);

            // The optimization: repeated reads hand back the snapshot's pre-materialized instance, not a fresh map.
            Assert.Same(first, second);
            Assert.Equal(zoneId, first.Id);
            Assert.Equal(4, first.LevelMin);
            Assert.Equal(7, first.LevelMax);
        }

        [Fact]
        public async Task ChallengesAll_ReturnsSnapshotDirectlyWithoutCopying()
        {
            int challengeId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                challengeId = (await TestDataSeeder.CreateChallengeAsync(context)).Id;
            }

            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var challenges = scope.ServiceProvider.GetRequiredService<IChallenges>();

            var first = challenges.All();
            var second = challenges.All();

            // The optimization: All() hands back the immutable snapshot itself, not a fresh copy per call.
            Assert.Same(first, second);
            Assert.Equal(challengeId, Assert.Single(first).Id);
        }
    }
}
