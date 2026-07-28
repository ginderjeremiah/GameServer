using Game.Core.Players;
using Xunit;
using CoreAttribute = Game.Core.Attributes.Attribute;

namespace Game.Core.Tests.Players
{
    public class PlayerStatPointsTests
    {
        [Fact]
        public void UpdateAttributes_SpendingPoints_IncrementsStatPointsUsedByAmountSpent()
        {
            var stats = MakeStats(gained: 10, used: 0);

            var result = stats.UpdateAttributes([new Update(EAttribute.Strength, 3)]);

            Assert.Equal(UpdateAttributesOutcome.Changed, result);
            Assert.Equal(3, stats.StatPointsUsed);
        }

        [Fact]
        public void UpdateAttributes_SpendAllAvailable_UsesAllPoints()
        {
            var stats = MakeStats(gained: 6, used: 0);

            var result = stats.UpdateAttributes([new Update(EAttribute.Strength, 6)]);

            Assert.Equal(UpdateAttributesOutcome.Changed, result);
            Assert.Equal(6, stats.StatPointsUsed);
        }

        [Fact]
        public void UpdateAttributes_SpendMoreThanAvailable_Rejects()
        {
            var stats = MakeStats(gained: 5, used: 3);

            var result = stats.UpdateAttributes([new Update(EAttribute.Strength, 3)]);

            Assert.Equal(UpdateAttributesOutcome.Rejected, result);
            Assert.Equal(3, stats.StatPointsUsed);
        }

        [Fact]
        public void UpdateAttributes_MultipleAllocations_SumsCorrectly()
        {
            var stats = MakeStats(gained: 10, used: 0);

            var result = stats.UpdateAttributes([
                new Update(EAttribute.Strength, 2),
                new Update(EAttribute.Endurance, 3),
            ]);

            Assert.Equal(UpdateAttributesOutcome.Changed, result);
            Assert.Equal(5, stats.StatPointsUsed);
            Assert.Equal(2, stats.StatAllocations.First(a => a.Attribute == EAttribute.Strength).Amount);
            Assert.Equal(3, stats.StatAllocations.First(a => a.Attribute == EAttribute.Endurance).Amount);
        }

        [Fact]
        public void UpdateAttributes_NegativeAllocationWouldGoBelow_Zero_Rejects()
        {
            var stats = MakeStats(gained: 10, used: 5);
            stats.StatAllocations.First(a => a.Attribute == EAttribute.Strength).Amount = 2;

            var result = stats.UpdateAttributes([new Update(EAttribute.Strength, -3)]);

            Assert.Equal(UpdateAttributesOutcome.Rejected, result);
        }

        [Fact]
        public void UpdateAttributes_ZeroAmountOnAnExistingRow_IsAcceptedButUnchanged()
        {
            // Accepted (it breaks no rule) but it allocates nothing, so it must not report a change the
            // caller would persist (#2485).
            var stats = MakeStats(gained: 10, used: 0);

            var result = stats.UpdateAttributes([new Update(EAttribute.Strength, 0)]);

            Assert.Equal(UpdateAttributesOutcome.Unchanged, result);
            Assert.Equal(0, stats.StatPointsUsed);
            Assert.Equal(0d, stats.StatAllocations.Single(a => a.Attribute == EAttribute.Strength).Amount);
        }

        [Fact]
        public void UpdateAttributes_EmptyPayload_IsAcceptedButUnchanged()
        {
            // The guard passes vacuously on an empty set, which used to read as a real change and raise a
            // full-allocation-set write-behind event per call (#2485).
            var stats = MakeStats(gained: 10, used: 4);

            var result = stats.UpdateAttributes([]);

            Assert.Equal(UpdateAttributesOutcome.Unchanged, result);
            Assert.Equal(4, stats.StatPointsUsed);
        }

        [Fact]
        public void UpdateAttributes_EveryDeltaZeroAcrossSeveralAttributes_IsAcceptedButUnchanged()
        {
            var stats = MakeStats(gained: 10, used: 2);
            stats.StatAllocations.First(a => a.Attribute == EAttribute.Strength).Amount = 2;

            var result = stats.UpdateAttributes([
                new Update(EAttribute.Strength, 0),
                new Update(EAttribute.Endurance, 0),
            ]);

            Assert.Equal(UpdateAttributesOutcome.Unchanged, result);
            Assert.Equal(2, stats.StatPointsUsed);
            Assert.Equal(2d, stats.StatAllocations.Single(a => a.Attribute == EAttribute.Strength).Amount);
        }

        [Fact]
        public void UpdateAttributes_DeltasSummingToZero_IsAChangeAndMustPersist()
        {
            // The reason the unchanged test is per-update rather than on the sum: moving points between
            // attributes nets zero spend but genuinely reallocates the spread.
            var stats = MakeStats(gained: 10, used: 2);
            stats.StatAllocations.First(a => a.Attribute == EAttribute.Strength).Amount = 2;

            var result = stats.UpdateAttributes([
                new Update(EAttribute.Strength, -2),
                new Update(EAttribute.Endurance, 2),
            ]);

            Assert.Equal(UpdateAttributesOutcome.Changed, result);
            Assert.Equal(2, stats.StatPointsUsed);
            Assert.Equal(0d, stats.StatAllocations.Single(a => a.Attribute == EAttribute.Strength).Amount);
            Assert.Equal(2d, stats.StatAllocations.Single(a => a.Attribute == EAttribute.Endurance).Amount);
        }

        [Fact]
        public void UpdateAttributes_ZeroAmountCreatingAMissingRow_IsAChange()
        {
            // The row enters the persisted allocation set even at zero, so this is a real change to the
            // aggregate rather than an accepted no-op. Bounded rather than spammable: the row exists from
            // here on, so a repeat of the same payload is Unchanged.
            var stats = DamagedStats(gained: 10, used: 1);

            var result = stats.UpdateAttributes([new Update(EAttribute.Dexterity, 0)]);

            Assert.Equal(UpdateAttributesOutcome.Changed, result);
            Assert.Equal(0d, stats.StatAllocations.Single(a => a.Attribute == EAttribute.Dexterity).Amount);
            Assert.Equal(UpdateAttributesOutcome.Unchanged, stats.UpdateAttributes([new Update(EAttribute.Dexterity, 0)]));
        }

        [Fact]
        public void UpdateAttributes_PartiallyUsedPoints_OnlyNewSpendAdded()
        {
            var stats = MakeStats(gained: 10, used: 4);

            var result = stats.UpdateAttributes([new Update(EAttribute.Strength, 3)]);

            Assert.Equal(UpdateAttributesOutcome.Changed, result);
            Assert.Equal(7, stats.StatPointsUsed);
        }

        [Fact]
        public void UpdateAttributes_MixedAddEditAndZero_AppliesEachAllocation()
        {
            var stats = MakeStats(gained: 10, used: 1);
            stats.StatAllocations.First(a => a.Attribute == EAttribute.Endurance).Amount = 1;

            var result = stats.UpdateAttributes([
                new Update(EAttribute.Strength, 2),   // add to a zeroed allocation
                new Update(EAttribute.Endurance, 1),  // edit an existing allocation
                new Update(EAttribute.Agility, 0),    // zero — no-op spend
            ]);

            Assert.Equal(UpdateAttributesOutcome.Changed, result);
            Assert.Equal(4, stats.StatPointsUsed); // 1 already used + 2 + 1 + 0
            Assert.Equal(2, stats.StatAllocations.First(a => a.Attribute == EAttribute.Strength).Amount);
            Assert.Equal(2, stats.StatAllocations.First(a => a.Attribute == EAttribute.Endurance).Amount);
            Assert.Equal(0, stats.StatAllocations.First(a => a.Attribute == EAttribute.Agility).Amount);
        }

        [Fact]
        public void UpdateAttributes_OneAllocationWouldGoNegative_RejectsEntireSet()
        {
            // The Strength reduction is legal on its own, but Endurance would drop below zero, so the
            // whole set must be rejected with no allocation or point changes applied.
            var stats = MakeStats(gained: 10, used: 5);
            stats.StatAllocations.First(a => a.Attribute == EAttribute.Strength).Amount = 4;

            var result = stats.UpdateAttributes([
                new Update(EAttribute.Strength, -2),
                new Update(EAttribute.Endurance, -1),
            ]);

            Assert.Equal(UpdateAttributesOutcome.Rejected, result);
            Assert.Equal(5, stats.StatPointsUsed);
            Assert.Equal(4, stats.StatAllocations.First(a => a.Attribute == EAttribute.Strength).Amount);
            Assert.Equal(0, stats.StatAllocations.First(a => a.Attribute == EAttribute.Endurance).Amount);
        }

        [Fact]
        public void UpdateAttributes_NearIntMaxValueUpdates_RejectsWithoutOverflowing()
        {
            // Two updates whose sum overflows int (checked LINQ Sum would throw OverflowException)
            // must still be rejected via the normal all-or-nothing contract, not crash.
            var stats = MakeStats(gained: 10, used: 0);

            var result = stats.UpdateAttributes([
                new Update(EAttribute.Strength, 1_500_000_000),
                new Update(EAttribute.Endurance, 1_500_000_000),
            ]);

            Assert.Equal(UpdateAttributesOutcome.Rejected, result);
            Assert.Equal(0, stats.StatPointsUsed);
            Assert.Equal(0, stats.StatAllocations.First(a => a.Attribute == EAttribute.Strength).Amount);
            Assert.Equal(0, stats.StatAllocations.First(a => a.Attribute == EAttribute.Endurance).Amount);
        }

        [Fact]
        public void UpdateAttributes_DerivedAttribute_RejectsWithoutMutating()
        {
            // An update targeting a derived attribute is rejected rather than silently succeeding as a
            // no-op (#488): only the core attributes are directly allocatable, so MaxHealth — computed
            // from Endurance — is an invalid request, not success.
            var stats = MakeStats(gained: 10, used: 0);

            var result = stats.UpdateAttributes([new Update(EAttribute.MaxHealth, 3)]);

            Assert.Equal(UpdateAttributesOutcome.Rejected, result);
            Assert.Equal(0, stats.StatPointsUsed);
            Assert.DoesNotContain(stats.StatAllocations, allocation => allocation.Attribute == EAttribute.MaxHealth);
        }

        [Fact]
        public void UpdateAttributes_DerivedAttributeCarryingAStoredRow_IsStillRejected()
        {
            // The rule is "the attribute is derived", not "the attribute has no row" — a stray non-core row
            // (which Rehydrate preserves rather than prunes) must not become a licence to allocate into it.
            var stats = MakeStats(gained: 10, used: 0);
            stats.StatAllocations.Add(new StatAllocation { Attribute = EAttribute.MaxHealth, Amount = 5d });

            var result = stats.UpdateAttributes([new Update(EAttribute.MaxHealth, 3)]);

            Assert.Equal(UpdateAttributesOutcome.Rejected, result);
            Assert.Equal(0, stats.StatPointsUsed);
            Assert.Equal(5d, stats.StatAllocations.Single(a => a.Attribute == EAttribute.MaxHealth).Amount);
        }

        [Fact]
        public void UpdateAttributes_CoreAndDerivedAttributes_RejectsEntireSet()
        {
            // A set mixing a valid allocation with a derived one is rejected as a whole, leaving the valid
            // allocation and the point pool untouched (#488).
            var stats = MakeStats(gained: 10, used: 0);

            var result = stats.UpdateAttributes([
                new Update(EAttribute.Strength, 2),
                new Update(EAttribute.MaxHealth, 3),
            ]);

            Assert.Equal(UpdateAttributesOutcome.Rejected, result);
            Assert.Equal(0, stats.StatPointsUsed);
            Assert.Equal(0, stats.StatAllocations.First(a => a.Attribute == EAttribute.Strength).Amount);
        }

        [Fact]
        public void UpdateAttributes_CoreAttributeWithNoRow_CreatesTheRowAndApplies()
        {
            // The #2459-shaped aggregate: every row but Strength is gone. A core attribute is allocatable
            // because it is core, so the spend lands on a row created here rather than failing until some
            // other path reseeds it.
            var stats = DamagedStats(gained: 10, used: 1);

            var result = stats.UpdateAttributes([new Update(EAttribute.Dexterity, 2)]);

            Assert.Equal(UpdateAttributesOutcome.Changed, result);
            Assert.Equal(3, stats.StatPointsUsed);
            Assert.Equal(2d, stats.StatAllocations.Single(a => a.Attribute == EAttribute.Dexterity).Amount);
        }

        [Fact]
        public void UpdateAttributes_NegativeUpdateOnAMissingRow_RejectsAsBelowZero()
        {
            // A created row starts at zero, so it is subject to the same non-negative rule as a stored one —
            // creating it must not hand the payload a free unallocation.
            var stats = DamagedStats(gained: 10, used: 1);

            var result = stats.UpdateAttributes([new Update(EAttribute.Dexterity, -1)]);

            Assert.Equal(UpdateAttributesOutcome.Rejected, result);
            Assert.Equal(1, stats.StatPointsUsed);
            Assert.DoesNotContain(stats.StatAllocations, allocation => allocation.Attribute == EAttribute.Dexterity);
        }

        [Fact]
        public void UpdateAttributes_RejectedSet_LeavesNoRowBehindForItsCoreUpdates()
        {
            // All-or-nothing covers the created rows too: a rejected payload must not leave the row its
            // valid half would have created, which would report an allocation the player never made.
            var stats = DamagedStats(gained: 10, used: 1);

            var result = stats.UpdateAttributes([
                new Update(EAttribute.Dexterity, 2),
                new Update(EAttribute.MaxHealth, 1),
            ]);

            Assert.Equal(UpdateAttributesOutcome.Rejected, result);
            Assert.Equal(1, stats.StatPointsUsed);
            Assert.Equal(EAttribute.Strength, stats.StatAllocations.Single().Attribute);
        }

        [Fact]
        public void UpdateAttributes_DuplicateAttribute_RejectsWithNoMutation()
        {
            // A duplicate update for the same attribute is ambiguous, so the whole payload is rejected
            // with no mutation rather than silently keeping only the first update (#698).
            var stats = MakeStats(gained: 10, used: 0);

            var result = stats.UpdateAttributes([
                new Update(EAttribute.Strength, 5),
                new Update(EAttribute.Strength, -3),
            ]);

            Assert.Equal(UpdateAttributesOutcome.Rejected, result);
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
        public void Rehydrate_MissingCoreRows_RestoresThemAtZero()
        {
            // The state a player is left in by a DB reload after the pre-fix write-behind handler deleted
            // their zero-amount rows (#2459): only the allocated attribute survives, so every other stat is
            // permanently unallocatable until its row comes back.
            List<StatAllocation> allocations = [new() { Attribute = EAttribute.Strength, Amount = 1d }];

            var stats = PlayerStatPoints.Rehydrate(allocations, statPointsGained: 10, statPointsUsed: 1);

            var expected = Enum.GetValues<EAttribute>().Where(CoreAttribute.IsCore).ToHashSet();
            Assert.Equal(expected, stats.StatAllocations.Select(allocation => allocation.Attribute).ToHashSet());
            Assert.Equal(1d, stats.StatAllocations.Single(a => a.Attribute == EAttribute.Strength).Amount);
            Assert.All(
                stats.StatAllocations.Where(a => a.Attribute != EAttribute.Strength),
                allocation => Assert.Equal(0d, allocation.Amount));
            Assert.Equal(10, stats.StatPointsGained);
            Assert.Equal(1, stats.StatPointsUsed);
        }

        [Fact]
        public void Rehydrate_AlreadyCompleteSet_AddsNothingAndKeepsExistingAmounts()
        {
            var allocations = PlayerStatPoints.CreateAllocations();
            allocations.First(a => a.Attribute == EAttribute.Strength).Amount = 2d;

            var stats = PlayerStatPoints.Rehydrate(allocations, statPointsGained: 10, statPointsUsed: 2);

            Assert.Equal(6, stats.StatAllocations.Count);
            Assert.Equal(2d, stats.StatAllocations.Single(a => a.Attribute == EAttribute.Strength).Amount);
        }

        [Fact]
        public void Rehydrate_NonCoreRow_IsPreservedRatherThanDropped()
        {
            // A row for a non-core attribute shouldn't exist, but player data is never silently discarded:
            // the repair only fills gaps, it does not prune.
            List<StatAllocation> allocations = [new() { Attribute = EAttribute.MaxHealth, Amount = 5d }];

            var stats = PlayerStatPoints.Rehydrate(allocations, statPointsGained: 10, statPointsUsed: 0);

            Assert.Equal(5d, stats.StatAllocations.Single(a => a.Attribute == EAttribute.MaxHealth).Amount);
        }

        /// <summary>
        /// A player whose stored allocations are missing every core row but Strength — the state a DB reload
        /// left behind after the pre-fix write-behind handler deleted the zero-amount rows (#2459).
        /// </summary>
        private static PlayerStatPoints DamagedStats(int gained, int used)
        {
            return new PlayerStatPoints
            {
                StatAllocations = [new StatAllocation { Attribute = EAttribute.Strength, Amount = 1d }],
                StatPointsGained = gained,
                StatPointsUsed = used,
            };
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
