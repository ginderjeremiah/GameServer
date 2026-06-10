# Spike #356 — Robust reference-data cache refresh (reload-and-swap)

- **Spike issue:** [#356](https://github.com/ginderjeremiah/GameServer/issues/356)
- **Status:** Research complete; direction decided with the project owner; split into implementation sub-issues (see [Implementation issues](#implementation-issues)).

## Goal

Reference-data loading uses synchronous blocking DB calls so that the cached lookups themselves can stay synchronous, and cache busting nulls the cache so the next reader lazily refills it. The spike question: how do we make this more robust **without** introducing `await` throughout the application — specifically, can cache busting eagerly load-and-replace the data instead of lazily refilling?

## How the cache works today

Each of the six cached reference repos (`Items`, `ItemMods`, `Skills`, `Enemies`, `Zones`, `Challenges` in `Game.DataAccess/Repositories`) holds a **static nullable list** on a **scoped** repository, filled on first access with a synchronous EF query:

```csharp
// Enemies.cs (abridged)
private static List<Enemy>? _enemyList;
private static Dictionary<int, ProbabilityTable<int>>? _zoneEnemyTables; // derived

private List<Enemy> AllEntities(bool refreshCache = false)
{
    if (_enemyList is null || refreshCache)
    {
        _enemyList = [.. _context.Enemies.AsNoTracking().Include(...).OrderBy(e => e.Id)];
        _zoneEnemyTables = null; // derived cache dropped, rebuilt lazily later
    }
    return _enemyList;
}

public void InvalidateCache() { _enemyList = null; _zoneEnemyTables = null; }
```

`AdminCacheInvalidationFilter` (an `IAsyncActionFilter`) runs after every successful admin write and calls `InvalidateCache()` on every `ICacheInvalidatable` resolved from DI. The next reader then refills the cache inline. The test infrastructure (`ReferenceCacheCleaner.InvalidateAll`) uses the same seam to reset the static state between tests.

## Findings — what lazy null-and-refill actually costs

1. **The refill cost lands on the wrong requests.** After an admin write, the next reader — typically a player's battle tick (`BattleService` → `GetDomainZone`/`GetRandomDomainEnemy`) or a loading-screen `Get*` socket command — pays the blocking `Include`-heavy query inline.
2. **Cache stampede.** There is no locking around the fill; every concurrent reader in the null window runs its own copy of the full query.
3. **Failure lands on a bystander.** A transient DB error at refill time throws in whichever player request reads next, instead of somewhere that can keep serving and log/retry.
4. **Derived caches can be observed out of step.** `Enemies` derives `_zoneEnemyTables` from the enemy list lazily; the list and tables are nulled and rebuilt as separate steps, so consistency between them depends on access order.
5. **Invalidation is single-instance.** The filter only invalidates the instance that served the admin write. Every other instance serves stale reference data indefinitely — at odds with the multi-instance architecture (Redis backplane, interchangeable API instances).
6. **Static mutable state on scoped services is the awkward seam.** The cache outlives the scoped repo that owns it and borrows whatever scoped `DbContext` happens to be in flight to refill; `ReferenceCacheCleaner` exists precisely to manage this from the outside in tests.

## Decisions

Settled with the project owner during the spike:

1. **Reads stay synchronous and lock-free — do not async-ify the read path.** Reference lookups are in-memory reads of an immutable snapshot; making them `Task`-returning would poison the whole call graph (the `BattleFactory`/`BattleSnapshot` `Func` resolvers exist specifically to keep `Game.Core` free of data-access concerns, and synchronous resolvers are part of that contract). The only places that genuinely touch the database are *startup load* and *reload* — async is confined to exactly those.
2. **Invalidation becomes eager build-then-swap (stale-while-revalidate), not null-and-refill.** A reload builds the complete new state off to the side while readers keep serving the old snapshot, then publishes it with a single atomic reference assignment. This eliminates the null window (no stampede, no inline refill on a player path), and a *failed* reload simply leaves the old snapshot serving — stale-but-valid — instead of throwing in a bystander request.
3. **The swap unit is an immutable snapshot that bundles derived structures.** Each set's cached list *plus* anything derived from it (the `Enemies` per-zone spawn tables) is built together into one snapshot object and swapped as one reference, so readers always see an internally consistent whole. Single reference writes are atomic in .NET; a `volatile` field (or `Volatile.Read`/`Write`) covers visibility.
4. **The admin filter awaits the reload — read-your-writes is preserved.** The Workbench depends on "every admin write invalidates the caches so the next read re-reads from the database". A fire-and-forget background reload would break that (the admin's follow-up read could see stale data). `AdminCacheInvalidationFilter` is already async, so `InvalidateCache()` becomes an awaited `ReloadAsync()`: the rare, latency-tolerant admin request pays the reload; players never see a gap. A reload failure after a successful write surfaces as an error on the admin response (the write persisted; the admin retries).
5. **Caches are eagerly loaded at startup, then the lazy-fill path is deleted.** With reload-and-swap in place, lazy fill only covers first access — so move that to a startup step that loads every set before serving traffic and fails fast at boot. The nullable statics, the null checks, and the unused `refreshCache` parameters all go; "cached lookups are synchronous" stops being a compromise and becomes structurally true.
6. **Cache state moves to singleton snapshot holders.** A singleton holder per set owns `Current` + `ReloadAsync()` and creates its own DI scope (`IServiceScopeFactory`) for the reload query; the scoped repos become thin readers over it. This fixes finding 6, makes the lifecycle explicit and testable, and gives the pub/sub follow-up a reload entry point that works outside any request scope. The `ICacheInvalidatable` DI-set discovery pattern carries over unchanged (with an async signature and a rename).
7. **Cross-instance invalidation via Redis pub/sub is the follow-up this enables.** A `reference data changed` broadcast (through the existing `IPubSubService`) triggers a *background* reload-and-swap on each instance. Note the ordering dependency: broadcasting under lazy refill would synchronize a null-cache stampede across the fleet; under reload-and-swap it composes cleanly. Bursts must be coalesced (a Workbench save fires several admin writes), on top of a per-holder single-flight guard.

## Accepted costs / future notes

- **The filter reloads all six sets per admin write** (it deliberately doesn't know which entity changed), and a Workbench save spans several writes. The sets are small and admin writes rare, so this is accepted; scoping reloads to the written entity or coalescing per request is a later optimization if it ever matters.
- **Reference-data version hashes** (`ComputeVersion`, recomputed per call) are unaffected, but immutable snapshots would make memoizing the hash per snapshot trivial if it ever shows up in profiling (already noted in `backend.md`).
- **Intrinsic reference data** (enum-derived sets like attributes/statistic types) never touches the DB and is unaffected.

## Implementation issues

Created as sub-issues of #356, in landing order:

- ✅ **[#357](https://github.com/ginderjeremiah/GameServer/issues/357)** _(tech debt, claude)_ — Eagerly load reference-data caches at startup, failing fast at boot. Small, standalone first slice; uses the existing fill path. **Done.**
- ✅ **[#358](https://github.com/ginderjeremiah/GameServer/issues/358)** _(tech debt, claude)_ — The core change: immutable snapshot holders (list + derived structures), `InvalidateCache()` → awaited `ReloadAsync()` in the admin filter, atomic swap, deletion of the lazy-fill path and nullable statics, test-infrastructure update. Depends on #357. **Done** — see [Reference-data cache reload](../backend.md#reference-data-cache-reload-build-then-swap).
- **[#359](https://github.com/ginderjeremiah/GameServer/issues/359)** _(enhancement, claude)_ — Cross-instance invalidation: publish a reference-data-changed message on admin writes and background-reload on every instance, with burst coalescing. Depends on #358.
