namespace Game.Core
{
    /// <summary>
    /// Gameplay constants that must stay identical on both sides of the frontend/backend boundary.
    /// Marked <see cref="ClientMirroredAttribute"/> so the code generator
    /// (<c>Game.Api.CodeGen.ApiCodeGenerator</c>) emits each public constant into the TypeScript
    /// client (<c>UI/src/lib/api/types/game-constants.ts</c>), making this class the single source of
    /// truth.
    /// </summary>
    [ClientMirrored]
    public static class GameConstants
    {
        /// <summary>The fixed battle-simulation tick size in milliseconds. Must match on both sides for battle parity.</summary>
        public const int MsPerTick = 40;

        /// <summary>The default maximum simulated battle duration in milliseconds before a battle times out.</summary>
        public const int DefaultMaxBattleMs = 2 * 60 * 1000;

        /// <summary>The maximum number of skills a player may equip in their battle loadout. Enemies bring the same cap into battle for player/enemy symmetry.</summary>
        public const int MaxSelectedSkills = 4;

        /// <summary>The experience required to advance a level scales linearly as <c>Level * <see cref="ExpPerLevel"/></c>.</summary>
        public const int ExpPerLevel = 100;

        /// <summary>The number of stat points awarded on each level-up.</summary>
        public const int StatPointsPerLevel = 6;
    }
}
