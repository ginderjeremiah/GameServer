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

        /// <summary>
        /// The reference-data id of the <em>punch</em> skill — the signature of the virtual <c>Unarmed</c>
        /// "fists" weapon a player fields bare-handed (spike #1342). The weapon-match loadout gate resolves an
        /// empty weapon slot's granted signature as <c>weapon?.GrantedSkillId ?? PunchSkillId</c>, so punch is
        /// fielded only bare-handed; a real <c>Unarmed</c> weapon replaces the fists with its own signature.
        /// A well-known seeded reference id like the starter-kit skills, mirrored to both simulators so they
        /// inject the identical fists' signature. A bare-handed battler only fields punch when this id resolves
        /// to a real skill (it is otherwise skipped), so the gate degrades gracefully if punch is unauthored.
        /// Punch is the foundational, always-available skill, so it takes the first reference id (0).
        /// </summary>
        public const int PunchSkillId = 0;

        /// <summary>
        /// The constant denominator term in the <see cref="EAttribute.Toughness"/> mitigation curve
        /// <c>Toughness / (Toughness + C)</c> — the Toughness at which mitigation reaches exactly 50%. A fixed
        /// constant (replacing the former <c>K·attackerLevel</c> normalization, #1487) means a Toughness
        /// investment retains its mitigation % across all of progression instead of decaying whenever Toughness
        /// grows slower than level, and each item's mitigation value is legible independent of enemy level.
        /// Early-game durability is carried by MaxHealth (which Endurance also provides) rather than a low
        /// half-point. Must match on both sides for battle parity. A strawman to tune during balancing.
        /// </summary>
        public const int ToughnessMitigationConstant = 200;

        /// <summary>The experience required to advance a level scales linearly as <c>Level * <see cref="ExpPerLevel"/></c>.</summary>
        public const int ExpPerLevel = 100;

        /// <summary>
        /// The size of the per-level <em>free pool</em> — the stat points awarded on each level-up for the
        /// player to allocate manually (through <c>PlayerStatPoints.TryUpdateAttributes</c>). This is the
        /// reduced share of attribute growth; the level-scaled class locked base (the attribute fingerprint)
        /// supplies the rest and is non-reallocatable (spike #1126 area D). A strawman to tune during
        /// balancing — the auto/free split.
        /// </summary>
        public const int StatPointsPerLevel = 2;
    }
}
