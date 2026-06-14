# Spike #548 — Move battle-end statistics/challenge-progress persistence off the synchronous response path

- **Spike issue:** [#548](https://github.com/ginderjeremiah/GameServer/issues/548)
- **Status:** Research complete; direction decided with the project owner; split into implementation sub-issues (see [Implementation issues](#implementation-issues)).

## Goal

The backend re-simulates each battle and persists the result synchronously before responding. Measurement (the `BattleRoundTripPerformanceTests` baseline) shows the per-battle response-path I/O (~4.9 ms median on a dev box) is dominated by **synchronous Postgres for the statistics/challenge-progress aggregate**, while the `Player` aggregate is already write-behind. Move that progress I/O off the response path, mirroring the existing `Player` write-behind pattern.

## How it works today

Two persistence paths run on every battle-end command, and they behave very differently:

1. **`Player` aggregate — already write-behind.** `PlayerRepository.SavePlayer` dispatches domain events that `PlayerPersistencePublisher` pushes onto the `PlayerUpdateQueue`; `DataProviderSynchronizer` drains it off-path. The Redis cache is the source of truth (`GetPlayer` is cache-first with a DB miss-reload, sliding 48h TTL — #439). No synchronous Postgres on the response path.
2. **`PlayerProgress` (statistics + challenges) — synchronous EF Core.** Every battle raises `BattleCompletedEvent`; `BattleStatisticsEventHandler` does `_progressRepo.Load` (two tracked SELECTs) → `RecordBattleCompleted` / `EvaluateChallenges` (in memory) → `_progressRepo.Save` (stages) → the command's `UnitOfWork.CommitAsync` (one `SaveChanges`). Not cached, not write-behind.

Measured baseline (dev box; ms):

| Phase | min | median |
|---|---|---|
| Full round trip (`EndBattleVictory` + `Commit`) | 4.36 | 4.91 |
| ├─ Progress `Load` (cold, 2 SELECTs) | 1.88 | 2.17 |
| ├─ `CommitAsync` (Postgres write) | 1.45 | 1.59 |
| └─ `SavePlayer` (Redis publish + cache) | 0.86 | 1.19 |

`Load` + `Commit` are ~77% of the round trip — all synchronous Postgres — and `Load` is the single biggest slice. The highest-volume write in the game (per-battle stats) is the one path that bypassed the write-behind machinery.

## Key enabling facts (why this is a clean mirror of `Player`)

- **Every `DataProviderSynchronizer` handler writes ABSOLUTE values** (absolute `ExecuteUpdate`, existence-checked insert, delete-then-rebuild). That is what makes the queue retry-safe — the queue retries on transient failure and dead-letters on exhaustion.
- **`PlayerProgress` already holds absolute stat values in memory** (`stat.Value`); `Increment`/`SetMax`/`SetMin` mutate them and `Save` writes them absolutely. So absolute-value persistence events fit naturally and are idempotent under retry.
- **The queue drains FIFO and sequentially** (a message is retried inline before the next is dequeued), so per-player events apply in order — no stale-overwrite from reordering.

## Decisions (proposed)

1. **Keep `PlayerProgress` a separate cached aggregate — do not fold it into the `Player` blob.** The `Player` key is read on *every* socket command; progress is touched only on battle-complete and stats-page views, and its row set grows per-skill/per-enemy over time. Folding it in would make every command deserialize a large, growing stat set it doesn't need. The battle path reads two cache keys instead of one — both Redis GETs off the DB.

2. **Cache `PlayerProgress` as a read-through aggregate, cache-as-source-of-truth, mirroring `GetPlayer`.** `Load` becomes cache-first on a `Progress_{playerId}` key with a DB miss-reload (`GetStatistics`/`GetChallenges`), under the same sliding idle TTL as the player cache (#439). The cached value is a serializable stats+challenges DTO; the `Player` comes from the player cache, so progress is never re-serialized into the player blob. This removes the two `Load` SELECTs from the response path.

3. **Persist via a single batched, absolute-value `ProgressUpdated` event on the existing `PlayerUpdateQueue`.** A battle touches ~10–20 stat rows; granular per-stat events would be far worse than today's single commit, so one batched event per save is the unit. Add one case to the `DataProviderSynchronizer` switch that upserts each carried stat/challenge to its absolute value (idempotent, same shape as `HandleLogPreferenceChanged` / `HandleSelectedSkillsChanged`). This reuses the existing retry / dead-letter / ordering pipeline wholesale.
   - **`PlayerProgress` gains dirty-tracking** — its mutations already funnel through `GetOrCreate`, so the event can carry only the rows changed this battle, not the whole (growing) stat set.
   - **The repo's `Save` becomes cache-write + enqueue** (mirroring `PlayerRepository.SavePlayer`), so `BattleStatisticsEventHandler` is unchanged and `UnitOfWork` no longer persists progress.
   - **Enqueue mechanism (decided): direct repo enqueue.** The repo publishes the `ProgressUpdated` envelope directly, reusing the queue + consumer (the resilient parts). The fuller-symmetry alternative — making `PlayerProgress` an `AggregateRoot` raising one `ProgressUpdatedEvent` routed through `DomainEventDispatcher` + a `ProgressPersistencePublisher` — was considered and set aside: it is more consistent with `Player` but adds dispatcher ceremony for a single event consumed only by the data tier.

4. **Cache-as-source-of-truth forces all progress reads from the cache, not the DB.** While the queue drains, the DB lags the cache, so a DB read would serve stale progress. Concretely:
   - **`GetCompletedChallengeIds` (zone-unlock gating) must read cached progress.** Otherwise a just-completed unlock challenge (cache updated, DB lagging) would wrongly block the player from the newly-unlocked zone. This is a forced consequence, not a free choice.
   - **`GetStatistics` / `GetChallenges` (stats page) read cached progress** for the same reason.

## Implementation issues

Created as sub-issues of #548, in landing order:

- **[#550](https://github.com/ginderjeremiah/GameServer/issues/550)** _(tech debt, claude, scope: medium)_ — Cache `PlayerProgress` as a read-through cached aggregate (cache-first `Load` + DB miss-reload + sliding TTL; route `GetStatistics` / `GetChallenges` / `GetCompletedChallengeIds` through the cache; keep the synchronous commit for now). Removes the ~2.2 ms `Load`. Standalone, lower-risk first slice.
- **[#551](https://github.com/ginderjeremiah/GameServer/issues/551)** _(tech debt, claude, scope: medium)_ — Write-behind the progress persistence: dirty-tracking in `PlayerProgress`, the batched absolute `ProgressUpdated` event on `PlayerUpdateQueue`, the `DataProviderSynchronizer` handler, and removal of the synchronous commit. Depends on #550.
- **[#552](https://github.com/ginderjeremiah/GameServer/issues/552)** _(tech debt, claude, scope: small)_ — Trim the write-behind publish: `FireAndForget` the wake-publish in `RedisPubSubService.Publish(channel, queue, data)` and batch a save's events into one multi-value LPUSH. Benefits both player and progress paths. Independent of #550/#551.

**Dropped during the spike:** parallelizing the two `Load` SELECTs. EF Core forbids concurrent operations on one `DbContext` (`Task.WhenAll` throws), and combining them via `Player`-navigation `Include`s just trades two clean independent queries for a cartesian-explosion / `AsSplitQuery` wash (`AsSplitQuery` issues ~the same number of round-trips). The cache in #550 removes both queries entirely, making the micro-optimization moot.

## Accepted costs / future notes

- The battle path reads two cache keys (player + progress) instead of one — both Redis GETs, neither on the DB.
- The progress cache adds memory per active player; the sliding idle TTL (the #439 pattern) ages out dormant players, and a `volatile-*` Redis eviction policy sheds them under pressure.
- `ProgressUpdated` reuses the existing **passive** dead-letter queue (no auto-redelivery consumer yet) — the same durability posture as the player events, and the same future work.
