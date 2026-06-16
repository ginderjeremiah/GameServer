using Game.Abstractions;
using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;
using Game.Core;
using Game.DataAccess.Repositories.Admin;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    public class AttributeChangeSetProcessorTests
    {
        // Stands in for the three real attribute-keyed entities (ItemAttribute / ItemModAttribute /
        // SkillDamageMultiplier), which differ only in their FK and Amount/Multiplier property name.
        private sealed record TestEntity(int AttributeId, decimal Amount);

        private static Change<BattlerAttribute> Change(EChangeType type, EAttribute attribute, decimal amount) => new()
        {
            ChangeType = type,
            Item = new BattlerAttribute { AttributeId = attribute, Amount = amount },
        };

        private static void Apply(
            IReadOnlyList<Change<BattlerAttribute>> changes,
            IReadOnlyCollection<TestEntity> existing,
            RecordingEntityStore store)
        {
            AttributeChangeSetProcessor.Apply(changes, existing,
                existingKey: e => e.AttributeId,
                toEntity: a => new TestEntity((int)a.AttributeId, a.Amount),
                store);
        }

        [Fact]
        public void Apply_Add_InsertsTheMappedEntity_RegardlessOfMembership()
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
