using Game.Core.Classes;
using Game.Core.Enemies;
using Game.Core.Items;
using Game.Core.Proficiencies;
using Game.Core.Skills;
using Game.Core.Zones;

namespace Game.Core.Battle.Offline
{
    /// <summary>
    /// The inputs to one offline-progress simulation. Grouped into a single object because the simulator
    /// needs the player snapshot, the loop it was running, the away/cap/cooldown timings, and the catalog
    /// resolvers — too many to thread as positional arguments. The resolvers mirror how
    /// <see cref="BattleFactory"/> and <see cref="BattleSnapshot"/> already take resolver funcs, keeping the
    /// domain free of data-access references; the application layer supplies the concrete catalog lookups.
    /// </summary>
    public record OfflineSimulationParameters
    {
        /// <summary>
        /// The player's battle-relevant state, captured once. A player's combat power is stationary while
        /// offline (allocations and gear cannot change), so this single snapshot drives every simulated
        /// battle and the exp-reward power measurement.
        /// </summary>
        public required BattleSnapshot Snapshot { get; init; }

        /// <summary>The idle loop the player was running at disconnect — idle-farming or boss-farming.</summary>
        public required OfflineLoopMode Mode { get; init; }

        /// <summary>
        /// The zone the loop operates in (the player's current zone). For <see cref="OfflineLoopMode.Idle"/>
        /// its level range drives the random encounter; for <see cref="OfflineLoopMode.Boss"/> its fixed boss
        /// level and <see cref="Zone.BossEnemyId"/> drive the deterministic boss build.
        /// </summary>
        public required Zone Zone { get; init; }

        /// <summary>How long the player was away, in milliseconds. Clamped to <see cref="CapMs"/>. A
        /// non-positive budget simulates nothing (a whole-skip).</summary>
        public required long AwayBudgetMs { get; init; }

        /// <summary>The maximum away time that is ever simulated, in milliseconds (the 10h cap). Bounds the
        /// CPU cost of a long absence and an all-draw zone that would otherwise earn nothing for the most
        /// work.</summary>
        public required long CapMs { get; init; }

        /// <summary>The post-battle cooldown gap, in milliseconds, consumed alongside each battle's duration.
        /// This is the throttle that bounds how many battles fit in the away period.</summary>
        public required int CooldownMs { get; init; }

        /// <summary>
        /// Resolves the enemy to fight for a rolled/fixed level. The semantics depend on <see cref="Mode"/> —
        /// a random per-zone spawn for idle, the zone's boss for boss mode — and the caller supplies the
        /// matching lookup, so the simulator stays mode-agnostic and data-access-free.
        /// </summary>
        public required Func<int, Enemy> ResolveEnemy { get; init; }

        /// <summary>Resolves an equipped item by id when reconstructing the player from the snapshot.</summary>
        public required Func<int, Item> ResolveItem { get; init; }

        /// <summary>Resolves an applied item mod by id when reconstructing the player from the snapshot.</summary>
        public required Func<int, ItemMod> ResolveMod { get; init; }

        /// <summary>Resolves a selected/granted skill by id when reconstructing the player from the snapshot, or
        /// <c>null</c> for an id with no skill (the weapon-match gate drops it — e.g. an unseeded bare-hands punch).</summary>
        public required Func<int, Skill?> ResolveSkill { get; init; }

        /// <summary>Resolves a proficiency definition by id when composing the snapshot's per-level/milestone
        /// bonuses (used only when the snapshot captured proficiency levels).</summary>
        public required Func<int, Proficiency> ResolveProficiency { get; init; }

        /// <summary>Resolves the player's class by id when composing the snapshot's level-scaled locked-base
        /// distribution. The whole away window fights at the snapshot's frozen level, so the locked base — a
        /// deterministic function of <c>(class, level)</c> — is stationary across the window like every other
        /// captured input.</summary>
        public required Func<int, Class> ResolveClass { get; init; }

        /// <summary>
        /// Supplies a fresh battle RNG seed for each simulated battle, mirroring the live path's per-battle
        /// seed. Injected so tests can drive deterministic outcomes; production passes the same cryptographic
        /// seed source the live battle start uses.
        /// </summary>
        public required Func<uint> SeedSource { get; init; }

        /// <summary>
        /// Optional CPU-waste guard: if set, the loop stops once its opening <see cref="StalemateCutoffBattles"/>
        /// battles have all been draws — neither a win nor a loss. That is an unwinnable-and-unloseable
        /// stalemate (e.g. an over-defended boss or an over-matched idle zone) that would otherwise simulate
        /// the whole away budget as maximum-duration draws for no reward — the most work for nothing, and a
        /// run a player could trigger repeatedly by reconnecting. A single win or loss anywhere in the opening
        /// batch means the loop is making progress, so the guard never fires for the rest of the run. Left
        /// <c>null</c> (the default) to run the full budget — the orchestration sets it; the cap still bounds
        /// every run regardless.
        /// </summary>
        public int? StalemateCutoffBattles { get; init; }
    }
}
