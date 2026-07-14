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
                enemySkillFixup.ForeignKeyProperties.Select(fk => fk.PropertyName).OrderBy(p => p));
        }

        [Fact]
        public void BuildZeroBasedFixups_IdentifiesPlayerCurrentZoneIdAndClassId()
        {
            using var context = CreateContext();

            var fixups = GameContext.BuildZeroBasedFixups(context.Model);

            // Player.CurrentZoneId/ClassId (#1823) are required FKs to store-generated zero-based principals
            // (Zone, Class), so a player parked on either's record 0 (e.g. a brand-new character, whose starting
            // zone is 0) must not have its FK misread as an unset store-generated value on save.
            var playerType = context.Model.FindEntityType(typeof(Player));
            Assert.NotNull(playerType);
            Assert.True(fixups.TryGetValue(playerType, out var playerFixup));
            Assert.Equal(
                [nameof(Player.ClassId), nameof(Player.CurrentZoneId)],
                playerFixup.ForeignKeyProperties.Select(fk => fk.PropertyName).OrderBy(p => p));
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

            // Editing the first record of a zero-based set (Id == 0, the identity seed) the way the admin path
            // does: a fresh entity marked Modified. In EF Core 10 (#1003) this leaves the key NON-temporary
            // (verified below), so the PK branch's ForceZero is a no-op and the literal 0 is preserved — the
            // record-0 edit still targets the correct row. End-to-end coverage is in the admin integration tests
            // (AdminEnemiesIntegrationTests.SaveEnemies_EditsRecordZero_UpdatesTheCorrectRow); the PK branch is
            // retained as a defensive guard rather than because it fires here.
            var enemy = new Enemy { Id = 0, Name = "First enemy", DesignerNotes = "" };
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
            var enemy = new Enemy { Id = 0, Name = "New enemy", DesignerNotes = "" };
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

        [Fact]
        public void ApplyZeroBasedIdentityFixups_ForNavigationAddedChildOfANewPrincipal_LeavesThePropagatedForeignKeyTemporary()
        {
            using var context = CreateContext();

            // Adding a brand-new Enemy together with an EnemySkill attached via navigation, mirroring #1824's
            // failure scenario: EF's own relationship fixup propagates the new Enemy's still-pending temp key
            // onto EnemySkill.EnemyId. Before the fix, ForceZero blindly rewrote any temporary FK to the literal
            // 0, which would have silently repointed this insert at the existing record 0 instead of the real
            // new Enemy once its id is generated.
            var enemy = new Enemy
            {
                Id = 0,
                Name = "New enemy",
                DesignerNotes = "",
                EnemySkills = [new EnemySkill { SkillId = 0 }],
            };
            context.Add(enemy);

            var enemyKeyEntry = context.Entry(enemy).Property(nameof(Enemy.Id));
            Assert.True(enemyKeyEntry.IsTemporary, "Precondition: the new Enemy's key should be temporary until insert.");

            var enemySkill = enemy.EnemySkills[0];
            var enemyIdEntry = context.Entry(enemySkill).Property(nameof(EnemySkill.EnemyId));
            var skillIdEntry = context.Entry(enemySkill).Property(nameof(EnemySkill.SkillId));
            Assert.True(enemyIdEntry.IsTemporary, "Precondition: EF should propagate the new Enemy's temp key onto EnemyId.");
            Assert.True(skillIdEntry.IsTemporary, "Precondition: EF should mark the FK to record 0 temporary.");

            context.ApplyZeroBasedIdentityFixups();

            // EnemyId must be left untouched — still the propagated temp value, not forced to 0 — so EF's own
            // key-fixup (which runs during the real SaveChanges insert) can still resolve it to the new Enemy's
            // real generated id.
            Assert.True(enemyIdEntry.IsTemporary);
            Assert.Equal(enemyKeyEntry.CurrentValue, enemyIdEntry.CurrentValue);

            // SkillId is unrelated to any Added principal in this save (no Skill is being added), so the FK
            // branch still forces it to the literal 0 as before.
            Assert.False(skillIdEntry.IsTemporary);
            Assert.Equal(0, skillIdEntry.CurrentValue);
        }
    }
}
