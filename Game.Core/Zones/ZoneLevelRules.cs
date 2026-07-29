namespace Game.Core.Zones
{
    /// <summary>
    /// The encounter-level invariants a zone must satisfy, as pure predicates. <see cref="Zone"/> enforces
    /// them at construction (throwing), but a zone is authored long before it is constructed: the
    /// content-authoring save reads these same rules so a mis-authored level rejects up front instead of
    /// committing a row that throws on every subsequent snapshot rebuild. Both callers therefore share one
    /// definition rather than restating the comparisons.
    /// Marked <see cref="ClientMirroredAttribute"/> so the Workbench's matching validation reads the bound
    /// from here rather than hand-copying the literal, the same way the content field lengths do.
    /// </summary>
    [ClientMirrored]
    public static class ZoneLevelRules
    {
        /// <summary>The lowest authorable encounter level, for both the idle bounds and the boss level.</summary>
        public const int MinZoneLevel = 1;

        /// <summary>
        /// Whether a single level value — an idle bound or the fixed boss level — is at least
        /// <see cref="MinZoneLevel"/>.
        /// </summary>
        public static bool IsValidLevel(int level)
        {
            return level >= MinZoneLevel;
        }

        /// <summary>
        /// Whether the idle encounter range is ordered: its lower bound no greater than its upper. An
        /// equal pair is a valid single-level range.
        /// </summary>
        public static bool IsOrderedRange(int levelMin, int levelMax)
        {
            return levelMin <= levelMax;
        }
    }
}
