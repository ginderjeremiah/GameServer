using Game.Abstractions.Content;
using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Contracts = Game.Abstractions.Contracts;
using EntitySkill = Game.Infrastructure.Entities.Skill;

namespace Game.Application.Tests.Content
{
    /// <summary>
    /// Integration coverage for the JSON-driven content seeder (#1419): seeding a fresh, migrated database
    /// from the source-controlled export must reconstruct the whole static content graph with its explicit,
    /// 0-based-contiguous ids so the reference caches build cleanly (each snapshot asserts <c>Id == index</c>)
    /// and cross-entity references resolve. The base class truncates all reference tables before each test, so
    /// every case starts from a genuinely empty static catalogue.
    /// </summary>
    [Collection("Integration")]
    public class ContentSeederTests : ApplicationIntegrationTestBase
    {
        public ContentSeederTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper)
        {
        }

        [Fact]
        public async Task SeedAsync_FreshDatabase_BuildsTheCachesAndResolvesCrossReferences()
        {
            var seeded = await SeedFromExportAsync();
            Assert.True(seeded, "The fresh database should have been seeded.");

            // The eager cache load asserts Id == index for every static set as it builds each snapshot, so a
            // clean reload is the contiguity check the issue calls for.
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var provider = scope.ServiceProvider;

            // skill → portions: every skill carries at least one direct-hit damage portion.
            var skills = provider.GetRequiredService<ISkills>().AllSkills();
            Assert.NotEmpty(skills);
            Assert.All(skills, skill => Assert.NotEmpty(skill.DamagePortions));

            // zone → boss → enemy: a zone's authored boss resolves against the enemy catalogue.
            var zones = provider.GetRequiredService<IZones>().All();
            var enemies = provider.GetRequiredService<IEnemies>().All();
            var enemyIds = enemies.Select(enemy => enemy.Id).ToHashSet();
            var bossZones = zones.Where(zone => zone.BossEnemyId is not null).ToList();
            Assert.NotEmpty(bossZones);
            Assert.All(bossZones, zone => Assert.Contains(zone.BossEnemyId!.Value, enemyIds));

            // class kit → skills: a class's starter skills resolve against the skill catalogue.
            var skillIds = skills.Select(skill => skill.Id).ToHashSet();
            var classes = provider.GetRequiredService<IClasses>().All();
            Assert.NotEmpty(classes);
            Assert.All(classes, cls => Assert.All(cls.StarterSkillIds, id => Assert.Contains(id, skillIds)));
        }

        [Fact]
        public async Task SeedAsync_PreservesExplicitZeroBasedIds()
        {
            await SeedFromExportAsync();

            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var skillIds = await context.Set<EntitySkill>().Select(skill => skill.Id).OrderBy(id => id).ToListAsync(CancellationToken);

            // The export's ids are 0-based and contiguous; the seeder must preserve them exactly (including 0,
            // which EF's normal insert path would let the store generate).
            Assert.Equal(Enumerable.Range(0, skillIds.Count), skillIds);
        }

        [Fact]
        public async Task SeedAsync_AlreadyPopulated_IsANoOp()
        {
            Assert.True(await SeedFromExportAsync(), "The first seed should populate the database.");

            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var skillCountAfterFirstSeed = await context.Set<EntitySkill>().CountAsync(CancellationToken);

            // A second seed against a populated database must skip entirely, leaving the rows untouched.
            var seededAgain = await SeedFromExportAsync();
            Assert.False(seededAgain);
            Assert.Equal(skillCountAfterFirstSeed, await context.Set<EntitySkill>().CountAsync(CancellationToken));
        }

        [Fact]
        public async Task SeedAsync_ConcurrentFreshBoots_ExactlyOneSeedsAndTheLoserNoOpsCleanly()
        {
            // Simulates two instances booting simultaneously against a fresh database: both would pass the
            // outside-transaction presence check, so the advisory lock inside the transaction (not the
            // outer check) is what must keep the loser from racing the insert into a PK violation.
            using var scopeA = CreateScope();
            using var scopeB = CreateScope();
            var import = scopeA.ServiceProvider.GetRequiredService<IContentImportReader>().Read(RepoPaths.ContentDirectory());
            var seederA = scopeA.ServiceProvider.GetRequiredService<IContentSeeder>();
            var seederB = scopeB.ServiceProvider.GetRequiredService<IContentSeeder>();

            var results = await Task.WhenAll(
                seederA.SeedAsync(import, CancellationToken),
                seederB.SeedAsync(import, CancellationToken));

            Assert.Single(results, result => result);
            Assert.Single(results, result => !result);

            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var skillCount = await context.Set<EntitySkill>().CountAsync(CancellationToken);
            Assert.Equal(import.Skills.Count, skillCount);
        }

        [Fact]
        public async Task SeedAsync_TaggedItemMod_SeedsTagCatalogueAndResolvesJoin()
        {
            // Tags are not one of the intrinsic (migration-seeded) sets, so a fresh DB has none. Seeding a tagged
            // item/mod must therefore seed the referenced Tag first; before that fix the join row referenced a
            // non-existent Tag and the insert FK-failed.
            const int tagId = 1;
            var import = EmptyImport() with
            {
                Tags = [new Contracts.Tag { Id = tagId, Name = "Test Tag", TagCategoryId = (int)ETagCategory.Accessory }],
                ItemMods =
                [
                    new Contracts.ItemMod
                    {
                        Id = 0,
                        Name = "Tagged Mod",
                        Description = "A mod that carries a tag.",
                        ItemModTypeId = EItemModType.Prefix,
                        RarityId = ERarity.Common,
                        Attributes = [],
                        Tags = [tagId],
                        DesignerNotes = string.Empty,
                        RetiredAt = null,
                    },
                ],
            };

            using var scope = CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<IContentSeeder>();
            var seeded = await seeder.SeedAsync(import, CancellationToken);
            Assert.True(seeded, "The fresh database should have been seeded.");

            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var tagIds = await context.Tags.Select(tag => tag.Id).ToListAsync(CancellationToken);
            Assert.Equal([tagId], tagIds);

            var join = await context.ItemModTags.SingleAsync(CancellationToken);
            Assert.Equal(0, join.ItemModId);
            Assert.Equal(tagId, join.TagId);
        }

        private async Task<bool> SeedFromExportAsync()
        {
            using var scope = CreateScope();
            var reader = scope.ServiceProvider.GetRequiredService<IContentImportReader>();
            var seeder = scope.ServiceProvider.GetRequiredService<IContentSeeder>();
            return await seeder.SeedAsync(reader.Read(RepoPaths.ContentDirectory()), CancellationToken);
        }

        /// <summary>An otherwise-empty content graph, so a test can seed just the sets it exercises via a
        /// <c>with</c> expression.</summary>
        private static ContentImport EmptyImport()
        {
            return new ContentImport
            {
                Skills = [],
                Tags = [],
                ItemMods = [],
                Items = [],
                Enemies = [],
                Challenges = [],
                Zones = [],
                Classes = [],
                Paths = [],
                Proficiencies = [],
                SkillRecipes = [],
                Lessons = [],
            };
        }
    }
}
