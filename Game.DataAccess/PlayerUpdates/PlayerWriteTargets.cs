using Game.Infrastructure.Entities;

namespace Game.DataAccess.PlayerUpdates
{
    /// <summary>
    /// Builds the <see cref="PlayerWriteWatermark.TargetKey"/> values for the streams whose keys are shared by
    /// more than one handler. The format is part of the persisted key — two handlers that must order against
    /// each other only do so if they spell the same target identically — so it lives in one place rather than
    /// being restated per handler. A stream written by a single handler keeps its builders private to it.
    /// </summary>
    internal static class PlayerWriteTargets
    {
        /// <summary>
        /// <see cref="PlayerWriteStream.Equipment"/>. The two key families share one space, so each carries a
        /// prefix — item and slot ids are both small integers and would otherwise collide.
        /// </summary>
        public static class Equipment
        {
            public static string Item(int itemId) => $"item:{itemId}";

            public static string Slot(int slotId) => $"slot:{slotId}";
        }

        /// <summary>
        /// <see cref="PlayerWriteStream.Mods"/>. One key family, keyed on the mod slot's owning item as well
        /// as the slot itself so the key states the whole row identity rather than relying on mod-slot ids
        /// being unique across items.
        /// </summary>
        public static class Mods
        {
            public static string Slot(int itemId, int modSlotId) => $"{itemId}:{modSlotId}";
        }
    }
}
