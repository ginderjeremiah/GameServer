using Game.DataAccess.PlayerUpdates;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Pins the literal <see cref="Game.Infrastructure.Entities.PlayerWriteWatermark.TargetKey"/> strings. These
    /// are persisted comparison keys rather than display strings, so reformatting one orphans every live
    /// watermark row for its stream — the replacement row starts at 0 and accepts the next stale replay, which
    /// is the #2467 regression the table exists to prevent, reached by a refactor instead of a race. Asserting
    /// the exact bytes is what makes that change fail loudly. Pure formatting, so it is covered classically
    /// rather than through the guard's integration suite.
    /// </summary>
    public class PlayerWriteTargetsTests
    {
        [Fact]
        public void EquipmentKeys_AreDistinctForTheSameNumericId()
        {
            // Both families share the Equipment stream, and the guard de-duplicates its key set before
            // upserting, so unprefixed ids would make item 3 and slot 3 one row: an equip of item 3 into slot 3
            // would silently degenerate to a single-key check, and equipping item 3 anywhere would advance the
            // row guarding slot 3. Item and slot ids are both small and dense, so the overlap is not a corner
            // case. Not reachable from the integration suite, which cannot choose the seeded item's id.
            Assert.NotEqual(PlayerWriteTargets.Equipment.Item(3), PlayerWriteTargets.Equipment.Slot(3));
        }

        [Fact]
        public void EquipmentKeys_HaveTheirPersistedFormat()
        {
            Assert.Equal("item:42", PlayerWriteTargets.Equipment.Item(42));
            Assert.Equal("slot:3", PlayerWriteTargets.Equipment.Slot(3));
        }

        [Fact]
        public void ModsKey_HasItsPersistedFormat()
        {
            // Deliberately prefix-less where the other streams' keys are prefixed, so folding it into a shared
            // key builder that emits a kind would re-key every live row for this stream.
            Assert.Equal("7:3", PlayerWriteTargets.Mods.Slot(7, 3));
        }
    }
}
