using Game.Abstractions.Infrastructure;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Game.Infrastructure.Tests
{
    /// <summary>
    /// Pins the zero-based-identity save fixup in <see cref="GameContext"/>: the per-entity-type targets it
    /// derives from the model (the cheap-to-cache replacement for the per-save metadata walk), and that those
    /// targets force the literal seed value 0 back onto the temporary key EF assigns when it reads 0 as an
    /// unset store-generated value. Building the context never opens a connection and the change tracker runs
    /// in-memory, so these stay in-process unit tests (the end-to-end persistence is covered by integration
    /// tests against a real database).
    /// </summary>
    public class GameContextZeroBasedIdentityFixupTests
    {
        private static GameContext CreateContext()
        {
            var options = new InfrastructureOptions
            {
                DatabaseSystem = DatabaseSystem.Postgres,
                DbConnectionString = "Host=localhost;Database=Game",
            };

            return GameContextFactory.GetGameContext(options, NullLoggerFactory.Instance);
        }

        [Fact]
        public void BuildZeroBasedFixups_IdentifiesTheZeroBasedIdentityPrimaryKey()
        {
            using var context = CreateContext();

            var fixups = GameContext.BuildZeroBasedFixups(context.Model);

            var enemyType = context.Model.FindEntityType(typeof(Enemy));
            Assert.NotNull(enemyType);
            Assert.True(fixups.TryGetValue(enemyType, out var enemyFixup));
            Assert.Equal(nameof(Enemy.Id), enemyFixup.KeyProperty);
        }

        [Fact]
        public void BuildZeroBasedFixups_IdentifiesForeignKeysToStoreGeneratedZeroBasedPrincipals()
        {
            using var context = CreateContext();

            var fixups = GameContext.BuildZeroBasedFixups(context.Model);

            // EnemySkill is a pure join row whose EnemyId/SkillId both reference store-generated zero-based
            // principals (Enemy, Skill), so both are fixup targets — but it has no zero-based identity of its own.
            var enemySkillType = context.Model.FindEntityType(typeof(EnemySkill));
            Assert.NotNull(enemySkillType);
            Assert.True(fixups.TryGetValue(enemySkillType, out var enemySkillFixup));
            Assert.Null(enemySkillFixup.KeyProperty);
            Assert.Equal(
                [nameof(EnemySkill.EnemyId), nameof(EnemySkill.SkillId)],
                enemySkillFixup.ForeignKeyProperties.OrderBy(p => p));
        }

        [Fact]
        public void BuildZeroBasedFixups_ExcludesEntitiesWithoutAZeroBasedKeyOrForeignKey()
        {
            using var context = CreateContext();

            var fixups = GameContext.BuildZeroBasedFixups(context.Model);

            // A connection-tracking table is neither a zero-based identity entity nor a holder of a zero-based
            // FK, so it must be skipped outright (the per-save loop never looks at it).
            var browserInfoType = context.Model.FindEntityType(typeof(BrowserInfo));
            Assert.NotNull(browserInfoType);
            Assert.False(fixups.ContainsKey(browserInfoType));
        }

        [Fact]
        public void ApplyZeroBasedIdentityFixups_ForModifiedRecordZero_DoesNotDisturbAConcreteKey()
        {
            using var context = CreateContext();

            // Editing the first record of a zero-based set (Id == 0, the identity seed). When EF reads 0 as an
            // unset store-generated value it marks the key temporary in the save pipeline, and the PK branch
            // forces the real 0 back (end-to-end coverage is in the admin integration tests). When the key is a
            // concrete value the branch is a no-op — verified here, since forcing a non-temporary key would
            // throw "part of a key ... cannot be modified".
            var enemy = new Enemy { Id = 0, Name = "First enemy" };
            context.Entry(enemy).State = EntityState.Modified;

            var keyEntry = context.Entry(enemy).Property(nameof(Enemy.Id));
            Assert.False(keyEntry.IsTemporary);

            context.ApplyZeroBasedIdentityFixups();

            Assert.False(keyEntry.IsTemporary);
            Assert.Equal(0, keyEntry.CurrentValue);
        }

        [Fact]
        public void ApplyZeroBasedIdentityFixups_ForAddedRecord_LeavesTheStoreGeneratedKeyTemporary()
        {
            using var context = CreateContext();

            // A brand-new record's id must still be store-generated, so the Added-row key is deliberately left
            // temporary (only the Modified record-0 case is forced).
            var enemy = new Enemy { Id = 0, Name = "New enemy" };
            context.Add(enemy);

            var keyEntry = context.Entry(enemy).Property(nameof(Enemy.Id));
            Assert.True(keyEntry.IsTemporary);

            context.ApplyZeroBasedIdentityFixups();

            Assert.True(keyEntry.IsTemporary);
        }

        [Fact]
        public void ApplyZeroBasedIdentityFixups_ForForeignKeyToRecordZero_ForcesTheRealZero()
        {
            using var context = CreateContext();

            // A join row referencing record 0 of two store-generated zero-based principals: each FK == 0 reads
            // as an unset store-generated value, so EF marks it temporary.
            var enemySkill = new EnemySkill { EnemyId = 0, SkillId = 0 };
            context.Add(enemySkill);

            var enemyIdEntry = context.Entry(enemySkill).Property(nameof(EnemySkill.EnemyId));
            var skillIdEntry = context.Entry(enemySkill).Property(nameof(EnemySkill.SkillId));
            Assert.True(enemyIdEntry.IsTemporary, "Precondition: EF should mark the FK to record 0 temporary.");
            Assert.True(skillIdEntry.IsTemporary, "Precondition: EF should mark the FK to record 0 temporary.");

            context.ApplyZeroBasedIdentityFixups();

            Assert.False(enemyIdEntry.IsTemporary);
            Assert.Equal(0, enemyIdEntry.CurrentValue);
            Assert.False(skillIdEntry.IsTemporary);
            Assert.Equal(0, skillIdEntry.CurrentValue);
        }
    }
}
