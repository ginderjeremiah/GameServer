# Reference-Data Cache Reload (build-then-swap)

> Satellite of [backend.md → Reference-data cache reload](./backend.md#reference-data-cache-reload-build-then-swap). Read this when working on the cache holders, the admin-write reload path, or the cross-instance invalidation plumbing.

Cache busting is an **eager build-then-swap** (stale-while-revalidate), not a null-and-lazily-refill (see the [cache-reload spike](./spikes/356-reference-data-cache-reload.md)). Readers never observe an empty or torn snapshot, and never pay a refill query inline.

## The holder

A **singleton holder** per set owns the current immutable snapshot and exposes a lock-free read plus `ReloadAsync()`. A reload builds the whole new snapshot off to the side (on its own DbContext) and publishes it with a single atomic reference swap, so a **failed reload leaves the old snapshot in place**. Derived structures (e.g. the per-zone spawn tables built from the enemy list) are bundled into the snapshot so they swap atomically and a reader can never see a new list against stale derived data. A per-holder semaphore serializes reloads to preserve read-your-writes.

## Admin-write reload & cross-instance invalidation

An admin write triggers an **awaited reload** after the write commits (so the Workbench reads its own writes with zero gap) and **broadcasts a cross-instance invalidation over the Redis backplane**. Other instances debounce the notification and run one background reload sweep; the publishing instance skips its own message. A failed background sweep is retried with backoff and never disturbs readers. The local reload is bounded by a timeout so a wedged database connection can't hold the admin request open indefinitely.

The broadcast publish itself is **awaited, not fire-and-forget** — unlike the write-behind wake signal, it has no durable write backing it, so a genuine send failure must surface (logged at the admin-write site) rather than vanish silently.

## Periodic reconciliation backstop

A periodic reconciliation sweep backstops Redis pub/sub's at-most-once delivery. A subscriber instance mid-reconnect (or otherwise transiently disconnected) when a notification is published simply never receives it — no error, no retry. Rather than serve stale reference data indefinitely, `CoalescingReferenceCacheReloader` races its signal wait against `ReferenceCacheReloadPolicy.ReconciliationInterval` (5 minutes by default) and runs a sweep if that interval elapses with no signal at all, logged distinctly from a signal-triggered sweep so an operator can tell them apart. Each sweep (signal or periodic) restarts the interval from zero, so this is "no sweep in N minutes," not a fixed wall-clock cadence.

## Snapshot read-once idiom (repo reads)

Because the swap is atomic, a reference repo must capture `holder.Current` **once per logical operation** (`var snapshot = holder.Current;`) and read everything off that local — re-reading it mid-operation could mix an old and a new snapshot.
