using System.Globalization;
using Game.Infrastructure.Entities;

namespace Game.DataAccess.PlayerUpdates
{
    /// <summary>
    /// Builds the <see cref="PlayerWriteWatermark.TargetKey"/> values for the streams whose keys are shared by
    /// more than one handler. The format is part of the persisted key — two handlers that must order against
    /// each other only do so if they spell the same target identically — so it lives in one place rather than
    /// being restated per handler. A stream written by a single handler keeps its builders private to it.
    /// </summary>
    /// <remarks>
    /// Ids are formatted with <see cref="CultureInfo.InvariantCulture"/> because these are persisted
    /// comparison keys, not display strings. Under a culture whose numeric formatting differs, a caller would
    /// spell an existing target differently, seed a <em>second</em> watermark row starting at 0, and the guard
    /// would stop seeing the first — accepting the next stale replay and reintroducing the very regression the
    /// table exists to prevent, silently.
    /// </remarks>
    internal static class PlayerWriteTargets
    {
        /// <summary>
        /// <see cref="PlayerWriteStream.Equipment"/>. The two key families share one space, so each carries a
        /// prefix — item and slot ids are both small integers and would otherwise collide.
        /// </summary>
        public static class Equipment
        {
            public static string Item(int itemId) => $"item:{itemId.ToString(CultureInfo.InvariantCulture)}";

            public static string Slot(int slotId) => $"slot:{slotId.ToString(CultureInfo.InvariantCulture)}";
        }

        /// <summary>
        /// <see cref="PlayerWriteStream.Mods"/>. One key family, keyed on the mod slot's owning item as well
        /// as the slot itself so the key states the whole row identity rather than relying on mod-slot ids
        /// being unique across items.
        /// </summary>
        public static class Mods
        {
            /// <remarks>
            /// The exact string is the persisted key, not merely a convention: this format has no kind prefix
            /// while every other stream's does, so reformatting it — including by folding it into a shared
            /// key-building helper that emits one — orphans every existing row for this stream. The
            /// replacement rows start at 0 and accept the next stale <c>ModApplied</c>, resurrecting a removed
            /// mod. Migrate the rows rather than reformatting the key.
            /// </remarks>
            public static string Slot(int itemId, int modSlotId)
                => $"{itemId.ToString(CultureInfo.InvariantCulture)}:{modSlotId.ToString(CultureInfo.InvariantCulture)}";
        }
    }
}
