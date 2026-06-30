using Game.Abstractions;
using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;
using Game.DataAccess.Repositories.Admin;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    public class KeyedChangeSetProcessorTests
    {
        // Stands in for the real composite-key-keyed child entities (ItemAttribute / ItemModAttribute /
        // SkillDamageMultiplier / SkillDamagePortion), which differ only in their FK and value property name.
        private sealed record TestEntity(int AttributeId, decimal Amount);

        private static Change<BattlerAttribute> Change(EChangeType type, EAttribute attribute, decimal amount) => new()
        {
            ChangeType = type,
            Item = new BattlerAttribute { AttributeId = attribute, Amount = amount },
        };

        private static AdminSaveResult Apply(
            IReadOnlyList<Change<BattlerAttribute>> changes,
            IReadOnlyCollection<TestEntity> existing,
            RecordingEntityStore store)
        {
            return KeyedChangeSetProcessor.Apply(changes, existing,
                itemKey: a => (int)a.AttributeId,
                existingKey: e => e.AttributeId,
                toEntity: a => new TestEntity((int)a.AttributeId, a.Amount),
                store,
                resourceName: "test attribute");
        }

        [Fact]
        public void Apply_AddOfAbsentAttribute_InsertsTheMappedEntity()
        {
            var store = new RecordingEntityStore();

            Apply([Change(EChangeType.Add, EAttribute.Strength, 5m)], existing: [], store);

            var inserted = Assert.IsType<TestEntity>(Assert.Single(store.Inserted));
            Assert.Equal((int)EAttribute.Strength, inserted.AttributeId);
            Assert.Equal(5m, inserted.Amount);
            Assert.Empty(store.Updated);
            Assert.Empty(store.Deleted);
        }

        [Fact]
        public void Apply_AddOfAlreadyPresentAttribute_Upserts()
        {
            // An Add of an attribute the owner already has must update, not duplicate-insert into a
            // composite-PK violation at commit.
            var store = new RecordingEntityStore();
            var existing = new[] { new TestEntity((int)EAttribute.Strength, 1m) };

            Apply([Change(EChangeType.Add, EAttribute.Strength, 5m)], existing, store);

            var updated = Assert.IsType<TestEntity>(Assert.Single(store.Updated));
            Assert.Equal((int)EAttribute.Strength, updated.AttributeId);
            Assert.Equal(5m, updated.Amount);
            Assert.Empty(store.Inserted);
            Assert.Empty(store.Deleted);
        }

        [Fact]
        public void Apply_DuplicateAddInSameBatch_RejectsGracefullyWithoutApplying()
        {
            // An attribute key is meaningful for an Add (a composite-PK insert), so two Adds of the same
            // key would double-track the row and EF rejects mid-batch as an opaque 500. The malformed batch
            // is rejected up front as a graceful business failure before anything is staged.
            var store = new RecordingEntityStore();

            var result = Apply(
            [
                Change(EChangeType.Add, EAttribute.Strength, 5m),
                Change(EChangeType.Add, EAttribute.Strength, 7m),
            ], existing: [], store);

            Assert.False(result.Succeeded);
            Assert.Equal("The submitted test attribute change set contains duplicate entries.", result.ErrorMessage);
            Assert.Empty(store.Inserted);
            Assert.Empty(store.Updated);
            Assert.Empty(store.Deleted);
        }

        [Fact]
        public void Apply_DuplicateEditInSameBatch_RejectsGracefullyWithoutApplying()
        {
            // Two Edits of the same existing key both pass the membership guard and double-track the row.
            var store = new RecordingEntityStore();
            var existing = new[] { new TestEntity((int)EAttribute.Strength, 1m) };

            var result = Apply(
            [
                Change(EChangeType.Edit, EAttribute.Strength, 5m),
                Change(EChangeType.Edit, EAttribute.Strength, 7m),
            ], existing, store);

            Assert.False(result.Succeeded);
            Assert.Equal("The submitted test attribute change set contains duplicate entries.", result.ErrorMessage);
            Assert.Empty(store.Updated);
        }

        [Fact]
        public void Apply_DuplicateDeleteInSameBatch_RejectsGracefullyWithoutApplying()
        {
            var store = new RecordingEntityStore();
            var existing = new[] { new TestEntity((int)EAttribute.Strength, 1m) };

            var result = Apply(
            [
                Change(EChangeType.Delete, EAttribute.Strength, 0m),
                Change(EChangeType.Delete, EAttribute.Strength, 0m),
            ], existing, store);

            Assert.False(result.Succeeded);
            Assert.Equal("The submitted test attribute change set contains duplicate entries.", result.ErrorMessage);
            Assert.Empty(store.Deleted);
        }

        [Fact]
        public void Apply_SameKeyAcrossDifferentChangeTypes_RejectsGracefullyWithoutApplying()
        {
            // An Edit and a Delete naming the same key map to two ops on distinct instances sharing that key.
            var store = new RecordingEntityStore();
            var existing = new[] { new TestEntity((int)EAttribute.Strength, 1m) };

            var result = Apply(
            [
                Change(EChangeType.Edit, EAttribute.Strength, 5m),
                Change(EChangeType.Delete, EAttribute.Strength, 0m),
            ], existing, store);

            Assert.False(result.Succeeded);
            Assert.Equal("The submitted test attribute change set contains duplicate entries.", result.ErrorMessage);
            Assert.Empty(store.Updated);
            Assert.Empty(store.Deleted);
        }

        [Fact]
        public void Apply_DistinctKeys_Succeeds()
        {
            var store = new RecordingEntityStore();

            var result = Apply(
            [
                Change(EChangeType.Add, EAttribute.Strength, 5m),
                Change(EChangeType.Add, EAttribute.Agility, 7m),
            ], existing: [], store);

            Assert.True(result.Succeeded);
            Assert.Equal(2, store.Inserted.Count);
        }

        [Fact]
        public void Apply_EditOfPresentAttribute_Updates()
        {
            var store = new RecordingEntityStore();
            var existing = new[] { new TestEntity((int)EAttribute.Strength, 1m) };

            Apply([Change(EChangeType.Edit, EAttribute.Strength, 9m)], existing, store);

            var updated = Assert.IsType<TestEntity>(Assert.Single(store.Updated));
            Assert.Equal((int)EAttribute.Strength, updated.AttributeId);
            Assert.Equal(9m, updated.Amount);
            Assert.Empty(store.Inserted);
            Assert.Empty(store.Deleted);
        }

        [Fact]
        public void Apply_EditOfAbsentAttribute_IsGuardedNoOp()
        {
            // The documented child-collection contract: an edit targeting an attribute the owner doesn't
            // have is silently reconciled away, not applied and not rejected (see docs/backend-admin.md).
            var store = new RecordingEntityStore();
            var existing = new[] { new TestEntity((int)EAttribute.Strength, 1m) };

            Apply([Change(EChangeType.Edit, EAttribute.Agility, 9m)], existing, store);

            Assert.Empty(store.Inserted);
            Assert.Empty(store.Updated);
            Assert.Empty(store.Deleted);
        }

        [Fact]
        public void Apply_DeleteOfPresentAttribute_Deletes()
        {
            var store = new RecordingEntityStore();
            var existing = new[] { new TestEntity((int)EAttribute.Strength, 1m) };

            Apply([Change(EChangeType.Delete, EAttribute.Strength, 0m)], existing, store);

            var deleted = Assert.IsType<TestEntity>(Assert.Single(store.Deleted));
            Assert.Equal((int)EAttribute.Strength, deleted.AttributeId);
            Assert.Empty(store.Inserted);
            Assert.Empty(store.Updated);
        }

        [Fact]
        public void Apply_DeleteOfAbsentAttribute_IsGuardedNoOp()
        {
            var store = new RecordingEntityStore();

            Apply([Change(EChangeType.Delete, EAttribute.Endurance, 0m)], existing: [], store);

            Assert.Empty(store.Inserted);
            Assert.Empty(store.Updated);
            Assert.Empty(store.Deleted);
        }

        [Fact]
        public void Apply_MixedChangeSet_AppliesEveryAddButOnlyResolvableEditsAndDeletes()
        {
            var store = new RecordingEntityStore();
            var existing = new[]
            {
                new TestEntity((int)EAttribute.Strength, 1m),
                new TestEntity((int)EAttribute.Endurance, 2m),
            };

            Apply(
            [
                Change(EChangeType.Add, EAttribute.Agility, 3m),      // insert
                Change(EChangeType.Edit, EAttribute.Strength, 9m),    // update — present
                Change(EChangeType.Edit, EAttribute.Intellect, 7m),  // no-op — absent
                Change(EChangeType.Delete, EAttribute.Endurance, 0m), // delete — present
                Change(EChangeType.Delete, EAttribute.Luck, 0m),      // no-op — absent
            ], existing, store);

            Assert.Equal((int)EAttribute.Agility, Assert.IsType<TestEntity>(Assert.Single(store.Inserted)).AttributeId);
            Assert.Equal((int)EAttribute.Strength, Assert.IsType<TestEntity>(Assert.Single(store.Updated)).AttributeId);
            Assert.Equal((int)EAttribute.Endurance, Assert.IsType<TestEntity>(Assert.Single(store.Deleted)).AttributeId);
        }
    }
}
