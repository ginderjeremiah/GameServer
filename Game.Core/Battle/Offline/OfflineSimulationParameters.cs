using Game.Core.Enemies;
using Game.Core.Items;
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

        /// <summary>Resolves a selected skill by id when reconstructing the player from the snapshot.</summary>
        public required Func<int, Skill> ResolveSkill { get; init; }

        /// <summary>
        /// Supplies a fresh battle RNG seed for each simulated battle, mirroring the live path's per-battle
        /// seed. Injected so tests can drive deterministic outcomes; production passes the same cryptographic
        /// seed source the live battle start uses.
        /// </summary>
        public required Func<uint> SeedSource { get; init; }
    }
}
