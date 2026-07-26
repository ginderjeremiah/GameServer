using Game.Core.Players;
using Xunit;
using CoreAttribute = Game.Core.Attributes.Attribute;

namespace Game.Core.Tests.Players
{
    public class PlayerStatPointsTests
    {
        [Fact]
        public void TryUpdateAttributes_SpendingPoints_IncrementsStatPointsUsedByAmountSpent()
        {
            var stats = MakeStats(gained: 10, used: 0);

            var result = stats.TryUpdateAttributes([new Update(EAttribute.Strength, 3)]);

            Assert.True(result);
            Assert.Equal(3, stats.StatPointsUsed);
        }

        [Fact]
        public void TryUpdateAttributes_SpendAllAvailable_UsesAllPoints()
        {
            var stats = MakeStats(gained: 6, used: 0);

            var result = stats.TryUpdateAttributes([new Update(EAttribute.Strength, 6)]);

            Assert.True(result);
            Assert.Equal(6, stats.StatPointsUsed);
        }

        [Fact]
        public void TryUpdateAttributes_SpendMoreThanAvailable_ReturnsFalse()
        {
            var stats = MakeStats(gained: 5, used: 3);

            var result = stats.TryUpdateAttributes([new Update(EAttribute.Strength, 3)]);

            Assert.False(result);
            Assert.Equal(3, stats.StatPointsUsed);
        }

        [Fact]
        public void TryUpdateAttributes_MultipleAllocations_SumsCorrectly()
        {
            var stats = MakeStats(gained: 10, used: 0);

            var result = stats.TryUpdateAttributes([
                new Update(EAttribute.Strength, 2),
                new Update(EAttribute.Endurance, 3),
            ]);

            Assert.True(result);
            Assert.Equal(5, stats.StatPointsUsed);
            Assert.Equal(2, stats.StatAllocations.First(a => a.Attribute == EAttribute.Strength).Amount);
            Assert.Equal(3, stats.StatAllocations.First(a => a.Attribute == EAttribute.Endurance).Amount);
        }

        [Fact]
        public void TryUpdateAttributes_NegativeAllocationWouldGoBelow_Zero_ReturnsFalse()
        {
            var stats = MakeStats(gained: 10, used: 5);
            stats.StatAllocations.First(a => a.Attribute == EAttribute.Strength).Amount = 2;

            var result = stats.TryUpdateAttributes([new Update(EAttribute.Strength, -3)]);

            Assert.False(result);
        }

        [Fact]
        public void TryUpdateAttributes_ZeroAmount_Succeeds()
        {
            var stats = MakeStats(gained: 10, used: 0);

            var result = stats.TryUpdateAttributes([new Update(EAttribute.Strength, 0)]);

            Assert.True(result);
            Assert.Equal(0, stats.StatPointsUsed);
        }

        [Fact]
        public void TryUpdateAttributes_PartiallyUsedPoints_OnlyNewSpendAdded()
        {
            var stats = MakeStats(gained: 10, used: 4);

            var result = stats.TryUpdateAttributes([new Update(EAttribute.Strength, 3)]);

            Assert.True(result);
            Assert.Equal(7, stats.StatPointsUsed);
        }

        [Fact]
        public void TryUpdateAttributes_MixedAddEditAndZero_AppliesEachAllocation()
        {
            var stats = MakeStats(gained: 10, used: 1);
            stats.StatAllocations.First(a => a.Attribute == EAttribute.Endurance).Amount = 1;

            var result = stats.TryUpdateAttributes([
                new Update(EAttribute.Strength, 2),   // add to a zeroed allocation
                new Update(EAttribute.Endurance, 1),  // edit an existing allocation
                new Update(EAttribute.Agility, 0),    // zero — no-op spend
            ]);

            Assert.True(result);
            Assert.Equal(4, stats.StatPointsUsed); // 1 already used + 2 + 1 + 0
            Assert.Equal(2, stats.StatAllocations.First(a => a.Attribute == EAttribute.Strength).Amount);
            Assert.Equal(2, stats.StatAllocations.First(a => a.Attribute == EAttribute.Endurance).Amount);
            Assert.Equal(0, stats.StatAllocations.First(a => a.Attribute == EAttribute.Agility).Amount);
        }

        [Fact]
        public void TryUpdateAttributes_OneAllocationWouldGoNegative_RejectsEntireSet()
        {
            // The Strength reduction is legal on its own, but Endurance would drop below zero, so the
            // whole set must be rejected with no allocation or point changes applied.
            var stats = MakeStats(gained: 10, used: 5);
            stats.StatAllocations.First(a => a.Attribute == EAttribute.Strength).Amount = 4;

            var result = stats.TryUpdateAttributes([
                new Update(EAttribute.Strength, -2),
                new Update(EAttribute.Endurance, -1),
            ]);

            Assert.False(result);
            Assert.Equal(5, stats.StatPointsUsed);
            Assert.Equal(4, stats.StatAllocations.First(a => a.Attribute == EAttribute.Strength).Amount);
            Assert.Equal(0, stats.StatAllocations.First(a => a.Attribute == EAttribute.Endurance).Amount);
        }

        [Fact]
        public void TryUpdateAttributes_NearIntMaxValueUpdates_RejectsWithoutOverflowing()
        {
            // Two updates whose sum overflows int (checked LINQ Sum would throw OverflowException)
            // must still be rejected via the normal all-or-nothing contract, not crash.
            var stats = MakeStats(gained: 10, used: 0);

            var result = stats.TryUpdateAttributes([
                new Update(EAttribute.Strength, 1_500_000_000),
                new Update(EAttribute.Endurance, 1_500_000_000),
            ]);

            Assert.False(result);
            Assert.Equal(0, stats.StatPointsUsed);
            Assert.Equal(0, stats.StatAllocations.First(a => a.Attribute == EAttribute.Strength).Amount);
            Assert.Equal(0, stats.StatAllocations.First(a => a.Attribute == EAttribute.Endurance).Amount);
        }

        [Fact]
        public void TryUpdateAttributes_UnknownAttribute_RejectsWithoutMutating()
        {
            // An update targeting an attribute the player has no allocation row for is rejected rather
            // than silently succeeding as a no-op (#488). Only core attributes are seeded as rows, so an
            // allocation into an unknown (or derived) attribute is an invalid request, not success.
            var allocations = new List<StatAllocation>
            {
                new() { Attribute = EAttribute.Strength, Amount = 0 },
            };
            var stats = new PlayerStatPoints { StatAllocations = allocations, StatPointsGained = 10, StatPointsUsed = 0 };

            var result = stats.TryUpdateAttributes([new Update(EAttribute.Luck, 3)]);

            Assert.False(result);
            Assert.Equal(0, stats.StatPointsUsed);
            Assert.Equal(0, stats.StatAllocations.Single().Amount);
        }

        [Fact]
        public void TryUpdateAttributes_KnownAndUnknownAttributes_RejectsEntireSet()
        {
            // A set mixing a valid allocation with one for an attribute that has no row is rejected as a
            // whole, leaving the valid allocation and the point pool untouched (#488).
            var allocations = new List<StatAllocation>
            {
                new() { Attribute = EAttribute.Strength, Amount = 0 },
            };
            var stats = new PlayerStatPoints { StatAllocations = allocations, StatPointsGained = 10, StatPointsUsed = 0 };

            var result = stats.TryUpdateAttributes([
                new Update(EAttribute.Strength, 2),
                new Update(EAttribute.Luck, 3),
            ]);

            Assert.False(result);
            Assert.Equal(0, stats.StatPointsUsed);
            Assert.Equal(0, stats.StatAllocations.Single().Amount);
        }

        [Fact]
        public void TryUpdateAttributes_DuplicateAttribute_RejectsWithNoMutation()
        {
            // A duplicate update for the same attribute is ambiguous, so the whole payload is rejected
            // with no mutation rather than silently keeping only the first update (#698).
            var stats = MakeStats(gained: 10, used: 0);

            var result = stats.TryUpdateAttributes([
                new Update(EAttribute.Strength, 5),
                new Update(EAttribute.Strength, -3),
            ]);

            Assert.False(result);
            Assert.Equal(0, stats.StatPointsUsed);
            Assert.Equal(0, stats.StatAllocations.First(a => a.Attribute == EAttribute.Strength).Amount);
        }

        [Fact]
        public void CreateAllocations_GrantsAZeroRowForEveryCoreAttributeInEnumOrder()
        {
            var allocations = PlayerStatPoints.CreateAllocations();

            var expected = Enum.GetValues<EAttribute>().Where(CoreAttribute.IsCore).ToList();
            Assert.Equal(expected, allocations.Select(allocation => allocation.Attribute));
            Assert.All(allocations, allocation => Assert.Equal(0d, allocation.Amount));
        }

        [Fact]
        public void CreateAllocations_ReturnsIndependentRowsPerCall()
        {
            // Each player owns its own mutable allocation rows; a shared instance would let one character's
            // spend leak into every other player seeded from the same call.
            var first = PlayerStatPoints.CreateAllocations();
            var second = PlayerStatPoints.CreateAllocations();

            first[0].Amount = 7d;

            Assert.Equal(0d, second[0].Amount);
        }

        [Fact]
        public void EnsureAllocatableAttributesArePresent_MissingCoreRows_RestoresThemAtZero()
        {
            // The state a player is left in by a DB reload after the pre-fix write-behind handler deleted
            // their zero-amount rows (#2459): only the allocated attribute survives, so every other stat is
            // permanently unallocatable until its row comes back.
            var allocations = new List<StatAllocation>
            {
                new() { Attribute = EAttribute.Strength, Amount = 1d },
            };
            var stats = new PlayerStatPoints { StatAllocations = allocations, StatPointsGained = 10, StatPointsUsed = 1 };

            stats.EnsureAllocatableAttributesArePresent();

            var expected = Enum.GetValues<EAttribute>().Where(CoreAttribute.IsCore).ToHashSet();
            Assert.Equal(expected, stats.StatAllocations.Select(allocation => allocation.Attribute).ToHashSet());
            Assert.Equal(1d, stats.StatAllocations.Single(a => a.Attribute == EAttribute.Strength).Amount);
            Assert.All(
                stats.StatAllocations.Where(a => a.Attribute != EAttribute.Strength),
                allocation => Assert.Equal(0d, allocation.Amount));
        }

        [Fact]
        public void EnsureAllocatableAttributesArePresent_RestoredRow_BecomesAllocatableAgain()
        {
            // The repair's whole point: the restored row lifts the #488 rejection, so the previously blocked
            // stat accepts an allocation instead of failing forever.
            var allocations = new List<StatAllocation>
            {
                new() { Attribute = EAttribute.Strength, Amount = 1d },
            };
            var stats = new PlayerStatPoints { StatAllocations = allocations, StatPointsGained = 10, StatPointsUsed = 1 };
            Assert.False(stats.TryUpdateAttributes([new Update(EAttribute.Dexterity, 2)]));

            stats.EnsureAllocatableAttributesArePresent();

            Assert.True(stats.TryUpdateAttributes([new Update(EAttribute.Dexterity, 2)]));
            Assert.Equal(3, stats.StatPointsUsed);
            Assert.Equal(2d, stats.StatAllocations.Single(a => a.Attribute == EAttribute.Dexterity).Amount);
        }

        [Fact]
        public void EnsureAllocatableAttributesArePresent_AlreadyComplete_IsAnIdempotentNoOp()
        {
            var stats = MakeStats(gained: 10, used: 2);
            stats.StatAllocations.First(a => a.Attribute == EAttribute.Strength).Amount = 2d;

            stats.EnsureAllocatableAttributesArePresent();
            stats.EnsureAllocatableAttributesArePresent();

            Assert.Equal(6, stats.StatAllocations.Count);
            Assert.Equal(2d, stats.StatAllocations.Single(a => a.Attribute == EAttribute.Strength).Amount);
        }

        [Fact]
        public void EnsureAllocatableAttributesArePresent_NonCoreRow_IsPreservedRatherThanDropped()
        {
            // A row for a non-core attribute shouldn't exist, but player data is never silently discarded:
            // the repair only fills gaps, it does not prune.
            var allocations = new List<StatAllocation>
            {
                new() { Attribute = EAttribute.MaxHealth, Amount = 5d },
            };
            var stats = new PlayerStatPoints { StatAllocations = allocations, StatPointsGained = 10, StatPointsUsed = 0 };

            stats.EnsureAllocatableAttributesArePresent();

            Assert.Equal(5d, stats.StatAllocations.Single(a => a.Attribute == EAttribute.MaxHealth).Amount);
        }

        private static PlayerStatPoints MakeStats(int gained, int used)
        {
            var allocations = new List<StatAllocation>
            {
                new() { Attribute = EAttribute.Strength,  Amount = 0 },
                new() { Attribute = EAttribute.Endurance, Amount = 0 },
                new() { Attribute = EAttribute.Intellect, Amount = 0 },
                new() { Attribute = EAttribute.Agility,   Amount = 0 },
                new() { Attribute = EAttribute.Dexterity, Amount = 0 },
                new() { Attribute = EAttribute.Luck,      Amount = 0 },
            };
            return new PlayerStatPoints
            {
                StatAllocations = allocations,
                StatPointsGained = gained,
                StatPointsUsed = used,
            };
        }

        private record Update(EAttribute Attribute, int Amount) : IAttributeUpdate;
    }
}
