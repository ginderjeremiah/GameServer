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
    /// shared core model with its assembled levels, and the derived activity-key → paths routing index.
    /// </summary>
    [Collection("Integration")]
    public class ProficienciesIntegrationTests : ApplicationIntegrationTestBase
    {
        public ProficienciesIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task GetProficiency_AssemblesLevels_AndPathRoutingIsExposed()
        {
            int proficiencyId, skillId, pathId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                skillId = (await SeedSkillAsync(context, ESkillAcquisition.Player)).Id;

                var path = new Entities.Path { Name = "Fire", Description = "d", ActivityKey = (int)EActivityKey.Fire };
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

            // The routing index exposes the paths bound to an activity key; the accrual resolves each path's
            // frontier tier against it at battle completion.
            var routed = Assert.Single(proficiencies.PathsForActivityKey(EActivityKey.Fire));
            Assert.Equal(pathId, routed.Id);

            // The decipher "words of power" round-trip through GetProficiencies onto the contract verbatim.
            var contract = proficiencies.AllProficiencies().Single(p => p.Id == proficiencyId);
            Assert.Equal("Blades", contract.Name);
            Assert.Equal("aenkor", contract.Word);
            Assert.Equal("AYN-kor", contract.Pronunciation);
            Assert.Equal("The First Flame", contract.Translation);
            Assert.Contains(
                proficiencies.AllPaths(),
                p => p.Name == "Fire" && p.ActivityKey == EActivityKey.Fire);
        }

        [Fact]
        public async Task PathsForActivityKey_ReturnsEmpty_WhenNoPathTrainsTheKey()
        {
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var path = new Entities.Path { Name = "Fire", Description = "d", ActivityKey = (int)EActivityKey.Fire };
                context.Paths.Add(path);
                await context.SaveChangesAsync(CancellationToken);
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var proficiencies = scope.ServiceProvider.GetRequiredService<IProficiencies>();

            Assert.Empty(proficiencies.PathsForActivityKey(EActivityKey.Water));
        }

        [Fact]
        public async Task PathsForActivityKey_ExposesThePathBoundToTheKey_WithItsTiers()
        {
            int pathId, tierZeroId, tierOneId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();

                var path = new Entities.Path { Name = "Fire", Description = "d", ActivityKey = (int)EActivityKey.Fire };
                context.Paths.Add(path);
                await context.SaveChangesAsync(CancellationToken);
                pathId = path.Id;

                var tierZero = NewTier(path.Id, ordinal: 0, name: "Fire Magic");
                var tierOne = NewTier(path.Id, ordinal: 1, name: "Inferno Magic");
                context.Proficiencies.AddRange(tierZero, tierOne);
                await context.SaveChangesAsync(CancellationToken);
                tierZeroId = tierZero.Id;
                tierOneId = tierOne.Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var proficiencies = scope.ServiceProvider.GetRequiredService<IProficiencies>();

            var routed = Assert.Single(proficiencies.PathsForActivityKey(EActivityKey.Fire));
            Assert.Equal(pathId, routed.Id);
            Assert.Equal(
                [(tierZeroId, 0), (tierOneId, 1)],
                routed.Tiers.Select(t => (t.ProficiencyId, t.Ordinal)));
        }

        [Fact]
        public async Task PathsForActivityKey_ExcludesRetiredPaths()
        {
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();

                // A retired path keyed on Fire must drop out of the routing index, so the accrual routes no XP
                // into the frozen track (the path is out of circulation).
                var path = new Entities.Path
                {
                    Name = "Fire",
                    Description = "d",
                    ActivityKey = (int)EActivityKey.Fire,
                    RetiredAt = DateTime.UtcNow,
                };
                context.Paths.Add(path);
                await context.SaveChangesAsync(CancellationToken);

                context.Proficiencies.Add(NewTier(path.Id, ordinal: 0, name: "Fire Magic"));
                await context.SaveChangesAsync(CancellationToken);
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var proficiencies = scope.ServiceProvider.GetRequiredService<IProficiencies>();

            Assert.Empty(proficiencies.PathsForActivityKey(EActivityKey.Fire));
        }

        [Fact]
        public async Task GetPath_ExposesActivityKeyAndTiersOrderedByOrdinal()
        {
            int pathId, tierZeroId, tierOneId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();

                var pathEntity = new Entities.Path { Name = "Fire", Description = "d", ActivityKey = (int)EActivityKey.Fire };
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
            Assert.Equal(EActivityKey.Fire, path.ActivityKey);
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
                Word = "",
                Pronunciation = "",
                Translation = "",
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
