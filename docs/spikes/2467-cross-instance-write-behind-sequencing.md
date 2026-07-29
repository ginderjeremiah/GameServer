# Spike #2467 — Cross-instance stale-absolute-overwrite in the write-behind drain

- **Spike issue:** [#2467](https://github.com/ginderjeremiah/GameServer/issues/2467)
- **Status:** Research complete; direction decided, split into implementation sub-issues, and **being implemented from #2492 onward** — see [Implementation issues](#implementation-issues) for what has shipped. Sections describing shipped behaviour document it rather than propose it.
- **Predecessor:** [#2460](https://github.com/ginderjeremiah/GameServer/issues/2460) closed the *within-instance* half by remembering parked player lanes across drain passes (`_parkedPlayerLanes`).

## The hazard, restated precisely

`DataProviderSynchronizer` drains a fleet-shared Redis queue. Reservation is sequential, but an item parked
on the shared processing list (a DB-infrastructure outage, or an acknowledge that faulted after a durable
apply) is only remembered in the parking instance's own memory. So:

1. Instance **A** reserves `E1` for player P (exp=100), exhausts its retries, classifies an infrastructure
   failure, and leaves the item reserved on the processing list.
2. Instance **B** reserves P's next event `E2` (exp=150). B has no knowledge of A's parked item, so it
   applies immediately.
3. Any instance later reclaims `E1` and applies it → Postgres regresses to exp=100.
4. Redis still holds the correct blob, so nothing self-corrects until P's next save re-dirties the same
   rows. A player dormant past the player-key TTL then gets the regressed state served from the DB
   fallback: durable progress loss, a re-completable challenge, a re-locked zone.

### Three findings that change the shape of the fix

**1. The issue's list of five affected handlers is incomplete.** Verified against every registered
`IPlayerUpdateHandler`. Only four handlers are genuinely convergent under reordering — the pure
insert-if-missing ones. Everything else writes absolute state and can regress:

| Handler | Write shape | Order-sensitive? |
|---|---|---|
| `SkillUnlockedHandler`, `ItemUnlockedHandler`, `ModUnlockedHandler` | insert-if-missing | No — convergent |
| `LessonUnlockedHandler` | insert, absorb unique violation | No — convergent |
| `PlayerCoreUpdatedHandler` | absolute `ExecuteUpdate` of all core fields | **Yes** |
| `ProgressUpdatedHandler` | absolute upsert per stat/challenge/proficiency row | **Yes** |
| `SelectedSkillsChangedHandler` | whole-set rebuild of every `PlayerSkill` flag | **Yes** |
| `ItemEquippedHandler` | vacate destination slot, then absolute place | **Yes** |
| `ItemUnequippedHandler` | absolute `EquipmentSlotId = null` | **Yes** (see below) |
| `LogPreferenceChangedHandler` | absolute `Enabled` per log type | **Yes** |
| `AttributeAllocationsChangedHandler` | absolute `Amount` per attribute | **Yes** |
| `ItemFavoriteChangedHandler` | absolute `Favorite` | **Yes** |
| `ModAppliedHandler` | delete-then-insert per `(player, item, slot)` | **Yes** |
| `ModRemovedHandler` | `ExecuteDelete` per `(player, item, slot)` | **Yes** |
| `LessonReadHandler` | absolute `ReadAt` | **Yes** |

`AttributeAllocationsChangedHandler` is the most consequential omission from the issue's list: a stale
apply durably reverts a player's spent stat points while `StatPointsUsed` on `Player` may reflect the newer
value, leaving the two disagreeing.

**2. `docs/backend-persistence.md` doesn't just understate the hazard for unequip — it asserts the
opposite.** The doc says *"The exception is unequip, where 'missing row' already is the desired end state
(unequipped = no equipped row), so its no-op converges without an insert."* That is true for the
reordered-behind-its-unlock case it was written about, but it reads as a general order-safety claim, and
unequip is **not** order-safe: a stale `ItemUnequippedEvent` applied after a newer `ItemEquippedEvent`
durably unequips an item the player is wearing. The doc's convergence argument needs to be split by
*reordering kind*, not by handler.

**3. Making `_parkedPlayerLanes` fleet-shared does not fix this, and was the first thing evaluated.**
The obvious cheap fix — publish the parked-player marker to Redis so every instance defers — fails on
timing, not on cost. Reservation is sequential and the park happens only *after* the retry budget is
exhausted (~0.6s by default), so `E2` is reserved **and applied** by B before A ever sets the marker. To
close the window the marker would have to be claimed at *reserve* time (a fleet-wide per-player in-flight
lock, atomic with the `LMOVE`), which reintroduces exactly the same-player cross-fleet serialization
`#1701` set out to relax and still leaves the marker to leak on a crash. To be fair to the mechanism: a
fleet-shared marker would still block every same-player event reserved *after* the park, which is most of a
long outage — it narrows the window rather than doing nothing, and that matters for #2475, which retires the
instance-local marker because the guard makes it redundant, not because it never protected anything.
**Sequencing is not merely the "obvious shape" — it is the only shape that is correct by construction**,
because it makes the guard depend on the events' own relative age rather than on any instance observing
another's state in time.

## Decisions

### 1. Where the sequence comes from — the producing aggregate, stamped at buffer time

Each write-behind aggregate (`Player`, `PlayerProgress`) owns a monotonic `long` counter, incremented once
per save, stamped onto each envelope as it is buffered into `PlayerUpdateBatch`.

Rejected alternatives:

- **Redis `INCR` per player at enqueue.** Adds a round trip to the hottest, most correctness-sensitive
  path in the game — the same cost the batched single-`LPUSH` flush exists to avoid. A hi/lo block
  allocator (`INCRBY` a block per connection) would amortize that to ~zero, and is the fallback if the
  seeding caveat below ever bites, but it adds a second per-player Redis key with its own TTL to reason
  about for a case the existing TTL invariant already excludes.
- **Reusing an existing marker.** Nothing on the aggregate is monotonic. `LastActivity` is a wall clock
  and would import cross-instance skew into a correctness guard.

The aggregate is the natural owner because **a player has exactly one live socket**, so the producer side
needs no distributed coordination at all — only the consumer side interleaves.

**The counter lives on the `Game.Core` aggregate, not in the data tier.** `backend.md` keeps persistence
detail out of `Game.Core`, so this is worth naming rather than leaving to #2473 to argue from scratch. The
precedent is `PlayerProgress`, which already carries write-behind plumbing in the domain
(`_dirtyStatistics`/`_dirtyChallenges`/`_dirtyProficiencies` and their `Dirty*` projections); a counter
beside them is consistent. `Player` carries nothing comparable, so it does gain its first write-behind-queue
concern. The alternative — keeping the counter entirely in the data tier — has no home to live in: it would
need a connection-scoped holder that the per-command DI scope doesn't provide, the same dependency the
progress aggregate declined to invert for its per-battle reload.

**Separate sequences per aggregate, not one shared.** The player and progress aggregates are separate
enqueues, ordered independently today, and write disjoint tables. Sharing one counter would mean threading
it between two repositories for zero benefit; two independent sequence spaces never interact because their
watermarks never cover the same row.

**Stamp at buffer time, not flush time.** This falls out for free and is strictly better: when a failed
flush carries envelopes forward into the *next* save's flush (`PlayerUpdateBatch` deliberately keeps them
buffered, #1494), the carried events retain their original lower sequence, so if the two saves' items are
later applied out of order the older one is correctly rejected rather than winning.

**All events of one save share one sequence.** The guard therefore has to reject on `<` (strictly older),
never `<=`: same-sequence siblings must all apply, and a duplicate re-apply of the same event must stay
idempotent under the queue's at-least-once contract. The consequence is that **the guard imposes no
ordering *within* a save** — despite the "per-player sequencing" framing, it is not a total order. Two
same-save events touching the same target still apply in whatever order the drain hands them over, exactly
as today. That is the right place to stop: a save's events are all raised from one consistent aggregate
state, so any order of them lands the same end state.

**Plumbing.** `Sequence` goes on `DomainEventEnvelope` — defaulted so an envelope enqueued by a pre-upgrade
instance mid-rolling-deploy still deserializes — not onto every domain event record.
`PlayerUpdateEventDispatcher` then hands handlers a small `PlayerUpdateContext` carrying it, rather than
widening `IPlayerUpdateHandler<T>.HandleAsync` with a positional parameter.

**Sequence 0 is a sentinel meaning "unsequenced", not a low sequence — and the `Id` precedent does not
transfer here.** `DomainEventEnvelope.Id` defaults to a fresh `Guid`, a harmless fabricated value that only
needs to be unique. A defaulted `Sequence` of 0 is semantically loaded: it is the smallest value the guard
can compare, so treating it as a real sequence makes it lose *every* comparison. That is a live
lost-write path during exactly the window the default exists to survive:

1. Player P is on an upgraded instance; their saves stamp 1, 2, 3 and an upgraded consumer advances P's
   watermarks to 3.
2. Mid-deploy, P reconnects and lands on a **pre-upgrade** instance — a rolling deploy is precisely the
   event that forces those reconnects, and a player's one live socket isn't pinned to an instance across
   them.
3. That instance enqueues envelopes with no `sequence` property, which upgraded consumers read as 0.
4. The guard compares against a watermark of 3, rejects, and acknowledges the event as a successful no-op.
   Every guarded write for P — equipment, mods, allocations, progress — is discarded for their whole
   session there.

So an event carrying sequence 0 **bypasses the guard entirely**: it applies unconditionally and leaves the
watermark untouched. That is precisely the pre-guard behaviour, which is the right semantic for an envelope
that carries no ordering information — a guard cannot rank an event that declined to rank itself, and
pretending it ranks lowest is what loses the write. Real counters therefore **start at 1**
(`COALESCE(MAX("LastAppliedSequence"), 0)` on cold-load seed, increment-then-stamp), so a first save never
stamps 0 and accidentally opts itself out.

The alternative considered and rejected: a deploy-ordering constraint — ship the producer fleet-wide, then
enable the guard a release later, so no sequence-0 envelope ever meets an advanced watermark (envelopes
already queued are covered by the *TTL ≫ drain time* invariant). It works, but it is an ops constraint that
has to be remembered at every deploy forever, and the sentinel makes it unnecessary.

### 2. Where the watermark lives — one generic table, granularity chosen per stream

`PlayerWriteWatermark(PlayerId, Stream, TargetKey, LastAppliedSequence)`.

**The compare must *be* the write, and the load-bearing mechanic is that the watermark row becomes the
per-target serialization point.** A read-then-compare-then-apply does not survive the very race this spike
exists to fix: two instances applying the same player's events concurrently both read
`LastAppliedSequence` = 4, both pass their guard, and under `READ COMMITTED` whichever commits last wins
the data row — which can be the older event. The guard is therefore a **conditional** statement,

```sql
INSERT INTO "PlayerWriteWatermarks" ("PlayerId", "Stream", "TargetKey", "LastAppliedSequence")
SELECT @p, @s, key, @seq FROM unnest(@keys) AS key ORDER BY key
ON CONFLICT ("PlayerId", "Stream", "TargetKey") DO UPDATE
  SET "LastAppliedSequence" = EXCLUDED."LastAppliedSequence"
  WHERE "PlayerWriteWatermarks"."LastAppliedSequence" <= EXCLUDED."LastAppliedSequence"
RETURNING "TargetKey"
```

One statement per event covering all of that event's keys, taking the row locks first, with the data write
applied only to the targets it `RETURNING`s and both in the same transaction. A fresh insert returns its key
regardless of the `WHERE`, which only gates the conflict branch, so a first-ever write for a target is never
rejected. The `ORDER BY` puts the deterministic lock order into the statement itself.

A separate seed-if-missing followed by a plain `UPDATE … WHERE "LastAppliedSequence" <= @seq` expresses the
same predicate and is the clearer illustration of it, but it is two statements with a race the single one
doesn't have — two instances can both find the row missing and both attempt the seed insert, so it needs its
own violation handling. Shipped took the `ON CONFLICT` form (`PlayerWriteWatermarkGuard`).

**The predicate is `<=`, not `<` — and note the operands are reversed from the rule in §1.** That rule is
stated on the *event's* sequence (reject when `eventSeq < watermark`); the SQL is stated on the *column*
(accept when `watermark <= eventSeq`). Both say the same thing, and getting this frame shift wrong is
exactly the slip this paragraph exists to prevent. Under `<` the statement would apply only when the
watermark is *strictly* below the event, rejecting equal sequences. That breaks same-save siblings landing
on one target: the first advances the row, the second finds `@seq < @seq` false and is silently skipped.
(Under the two-statement form it also leaves the seed with no working value — seeded at `@seq`, the
conditional update that follows immediately no-ops. That argument does *not* carry over to the shipped
`ON CONFLICT` shape, where a fresh insert returns its key regardless of the `WHERE`, so the same-save-sibling
reason is the one carrying this for the code that actually exists.) The cost of `<=` is that an exact
duplicate re-applies rather than being skipped, paying one redundant no-op write; that is the correct trade
under the queue's at-least-once contract and is the same choice §1 already made, so don't "optimize" it back
to `<`. (Sequence 0 never reaches this predicate at all — see the sentinel rule in §1.)

**That transaction is a new pattern on this path, and it is the real cost of the design — not a free
round-trip ride.** An earlier draft of this doc claimed the watermark upsert costs nothing because it rides
the handler's existing `SaveChangesAsync`. That is wrong for much of the handler set:
`ItemUnequippedHandler` and `ModRemovedHandler` are a bare `ExecuteUpdateAsync`/`ExecuteDeleteAsync` with
no `SaveChangesAsync` to ride at all, `LogPreferenceChangedHandler` and `LessonReadHandler` self-commit
their update fast path, `ItemEquippedHandler` is explicit that its vacate and place are separate commits,
and no handler on this path opens a transaction today (`BeginTransactionAsync` appears only in
`Repositories/Users.cs` and `ContentSeeder`). Worse, a conditional watermark update is an `ExecuteUpdate`,
which self-commits *outside* EF's implicit `SaveChanges` transaction — so even the handlers that do have a
`SaveChangesAsync` cannot simply enlist the watermark into it.

Left in separate commits, a crash between the watermark advance and the data write would advance the
watermark without the data, and the redelivered event would then be **rejected as stale — a silently lost
write, strictly worse than the bug being fixed**. So every guarded handler needs an explicit transaction
(or a single-statement CTE doing both). Two knock-on effects for #2474: the existing bespoke
unique-violation retries can no longer just `ChangeTracker.Clear()` and re-run, because a `DbUpdateException`
aborts the surrounding transaction — they must roll back and restart it; and `ItemEquippedHandler`'s
documented vacate/place crash window closes as a side effect, which is an improvement but invalidates the
reasoning in its current comment.

This is the price of one guard mechanism instead of two. Per-row version columns would let the
single-row handlers do it in one atomic conditional statement with no transaction at all — but they don't
survive `ModRemovedHandler`'s tombstone (below), so taking that cheaper path means shipping both
mechanisms. The drain is off the player's request path, so paying transaction framing here is the right
trade.

**A single "last applied sequence" per player — the issue's first suggested fork — is wrong, and this is
the most important finding of the spike.** The guard's granularity must be at least as fine as the row
identity the absolute write targets, or it manufactures a *worse* bug than the one it fixes. A per-player
watermark would reject a slightly-older event carrying an entirely different, still-current row: an older
`LogPreferenceChanged` for log type A discarded because a newer one for type B already landed, or — far
worse — an older `ProgressUpdated` carrying statistic X discarded because a newer one carrying only
statistic Y landed first. Progress events carry only a save's *dirty* rows, so a coarse watermark would
silently drop live writes on the game's highest-volume path.

`Stream` is a small enum with **one value per target space, not per handler**; `TargetKey` is the canonical
identity of the write target within that stream (`""` for the genuinely player-scoped streams, `"stat:7:19"`
for a `(statisticType, entityId)` pair). Per-space rather than per-handler because two handlers writing the
*same* rows have to share a stream or they cannot order against each other at all — a stale `ModApplied` is
only rejected because `ModRemoved` advanced the very watermark it compares against, which is the whole
tombstone argument below. The converse also holds: handlers writing *disjoint columns of a shared row* stay
separate (equipment vs. the favorite flag on the same `UnlockedItem`), since ordering those against each
other would reject writes that aren't stale in any sense that matters.

**Where a stream carries more than one *kind* of target, the key must be qualified by kind** — an item key
and a slot key distinguished by their prefix, the exact spelling owned by the stream. The watermark row's
identity is `(PlayerId, Stream, TargetKey)`, and the equipment stream keys on both an item and a slot
(below), so bare ids would make **item 3 and slot 3 the same row**. Slot ids are small and dense and item
ids start low, so that overlap is most of the low id range, not a corner case. Two things would break, both
of them the failure this section rejects a per-player watermark over: the dual-key check would silently
collapse to a single key whenever `ItemId == SlotId` (the
guard de-duplicates its key set, since `ON CONFLICT DO UPDATE` cannot affect one row twice in a statement),
and equipping item 3 would advance the watermark that also guards slot 3, so a reordered older event
targeting slot 3 would be rejected against a sequence set by a write to a different, still-current target.
An ordinal sort over prefixed keys is still a total order, so the deterministic lock order is unaffected.
The shipped `Progress` stream already follows this — `"stat:{typeId}:{entityId}"`, `"challenge:{id}"`,
`"prof:{id}"` — for the same reason, since it too carries three kinds of target in one stream.

Chosen over **per-row version columns** on the six-plus affected tables because:

- **It survives tombstones.** `ModRemovedHandler` deletes the row outright, so a per-row column takes the
  version to the grave with it: a stale `ModApplied` arriving after a newer `ModRemoved` finds no row to
  compare against and resurrects the mod. A watermark row outlives the data row it guards.
- One migration, not six, and it never widens `Player` — a row read on effectively every socket command.
- Granularity becomes a per-handler decision rather than a schema commitment.

Its honest costs: one conditional statement plus transaction framing per guarded event (above), and a row
count that grows with a player's distinct write targets (bounded by the guarded data itself).

**Equipment needs two keys checked, not a stamp on the vacated row.** `ItemEquippedHandler` clears the
destination slot's previous occupant as a side effect of an absolute "item I is in slot S" statement, and
the naive requirement — advance the *evicted* target's watermark too — collides head-on with a property
that handler deliberately has: the vacate is a single `ExecuteUpdateAsync` precisely so the prior occupant
is never materialized into a snapshot a concurrent commit could tear. Stamping the evicted target needs its
`ItemId`, which `ExecuteUpdate` cannot return, forcing either a read-then-write (giving back exactly the
tearable snapshot that comment defends against) or raw SQL with `UPDATE … RETURNING`.

Neither is necessary. Key the equipment stream on **both** the item and the slot, checking and advancing
`(item=I)` and `(slot=S)` together, and the eviction needs no stamp at all:

- `seq 5`: A→slot1 advances `item=A` and `slot1` to 5.
- `seq 6`: B→slot1 advances `item=B` and `slot1` to 6, vacating A's row without stamping it.
- Replay of `seq 5`: passes the `item=A` check (still 5, and the guard rejects only on strictly-older), but
  `slot1` is 6 → **rejected**. No double-occupancy, no unique-index trip.

The item key is what catches the mirror case a slot key alone misses — a later save moving A from slot1 to
slot2 leaves `slot1` untouched, so only `item=A` being at the newer sequence stops a replayed `A→slot1`
from dragging it back. It also covers unequip, which involves no slot at all: `seq 6` unequipping A stamps
`item=A` to 6, so a replayed `seq 5` equip is rejected on the item key alone. Both keys are needed; either
alone has a hole.

**The two checks are all-or-nothing.** If one key passes and the other rejects, the whole apply rolls back
with *neither* watermark advanced — a partial advance would leave one key claiming a write that never
happened, which is the same silently-lost-write shape the transaction requirement above exists to prevent.
The surrounding transaction gives this for free, but it is too load-bearing to leave to inference. It must
still be **disposed of as §3 requires** — rolled back and acknowledged as a no-op, not signalled by an
exception, which would escape the handler and dead-letter an ordinary reordering. The other cost is two
watermark rows locked per equip, which needs a deterministic lock order or two concurrent equips deadlock
against each other; the shipped guard supplies that generically (the upsert's own `ORDER BY` over the
ordinal-sorted key set), so no per-handler ordering rule is needed.

### 3. How a stale event is disposed of — acknowledged, counted, and surfaced

Acknowledge it as a successful no-op (it is genuinely already-superseded work, not a failure), but count
rejections per drain pass and log a single summary line when non-zero — the class already throttles this
way for `_lastReportedDeadLetterDepth` and `_infrastructureOutageLogged`. Silently dropping writes on a
path whose whole purpose is "never silently drop a write" would make a genuine reordering storm — or a bug
in the sequencing itself — invisible.

### 4. Which handlers get guarded — every one except the four convergent inserts

Per the table above: the pure insert-if-missing handlers (`SkillUnlocked`, `ItemUnlocked`, `ModUnlocked`,
`LessonUnlocked`) stay unguarded. Scoping to only the five handlers the issue named would leave
`AttributeAllocationsChanged`, `ItemUnequipped`, `ModApplied`/`ModRemoved`, `ItemFavoriteChanged`, and
`LessonRead` regressing exactly as before.

### 5. Interaction with #1739 — none, and that is a consequence of decision 2

Batching consecutive same-player events onto one `SaveChangesAsync` would, under a *player-scoped*
watermark, have to compare against the batch's highest sequence. Under per-target watermarks the two
designs are orthogonal: each event in the batch compares against its own targets with its own sequence, in
whatever order the batch applies them. Landing this spike's work first makes #1739 simpler, not harder.

### 6. #2460's deferral can be dropped once the guard lands

`_parkedPlayerLanes` exists solely to stop a newer event applying ahead of a parked older one. Once the
guard makes that harmless — the reclaimed older event is simply rejected — the deferral is pure convergence
latency for the affected player with no correctness value, and should be removed in the same change rather
than leaving two mechanisms guarding one invariant. The **pass-scoped** `playerLanes` map stays: it still
preserves ordering cheaply within a pass and avoids doing work only to reject it.

## Producer-counter seeding, and the one residual gap

The counter is persisted in each aggregate's cached representation (`PlayerCacheModel`; a reserved `_seq`
field in the progress hash), so it survives reconnects and instance migration. On a **cold DB load** (cache
miss) it is seeded from the player's highest `LastAppliedSequence`.

**Seed on the *value*, not the source: whenever a hydrated counter reads `Unsequenced`, seed it from the
aggregate's persisted watermark `MAX`.** There are three ways a counter arrives at 0, not one, and only a
value-based rule covers all of them:

1. A **cold DB load** — no column backs the counter, so the projection can't supply it.
2. A **cache miss** that falls through to the same load.
3. A **cache hit on a blob a pre-upgrade instance wrote.** The player blob is re-serialized in full on every
   save, so a pre-upgrade save erases `writeSequence` outright; the next upgraded read hydrates 0.

The third is the one that is easy to miss, because it is a *hit*, and it lands squarely inside the
rolling-deploy window §1's sentinel is about: P's saves reach 47 on an upgraded instance, P reconnects
mid-deploy onto a pre-upgrade instance whose (correctly sentinel-applied) saves leave a blob with no
counter, P reconnects onto an upgraded instance and hydrates 0. §1 closes the *envelope* direction of that
story; the cache is what reopens the other end, so the two halves only meet if the seed is value-based.

**`PlayerProgress` is genuinely safer here, and the reason is blob-vs-hash rather than anything about the
aggregates.** Its counter is a reserved field in a Redis *hash*, and a pre-upgrade save `HSET`s only its
dirty row fields, leaving `_seq` untouched. The counter freezes rather than resets — safe, since a resumed
counter is ≥ every watermark it will meet. The player blob has no such protection because it is one value
rewritten whole.

A corollary for the implementation: `PlayerCacheModel.WriteSequence` must stay **non-`required`**. `required`
is enforced at deserialization, so marking it would throw on exactly the pre-upgrade blobs it needs to
tolerate — worse than reseeding. The guard is the value check, not the schema.

**That `MAX` is scoped to the streams the aggregate itself produces**, not an unscoped per-player maximum:
`Player` seeds from `PlayerCore` plus the equipment/mod streams, `PlayerProgress` from `Progress`. The two
own deliberately separate counter spaces, and seeding both from one shared maximum — while *safe*, since it
is still monotonic and merely skips values — would make each counter jump on the other aggregate's traffic,
contradicting that separation and reading as a bug to whoever hits it. `COALESCE(MAX(…), 0)`, so a player
with no watermark rows yet starts at 0 and stamps 1.

That seed is correct whenever the queue has drained, which is the only way a cache miss normally happens:
the miss means the player was dormant past the multi-hour key TTL, and the *TTL ≫ max queue-drain time*
invariant means their events drained long ago. The residual gap is a cache key lost (eviction under
memory pressure, an operator delete) **while events are still undrained** — the counter reseeds below its
true high-water mark, and a stale event can then out-rank a newer one. That is the same scenario the TTL
invariant already excludes by design, and it is strictly no worse than today's behaviour, so it is
accepted and documented rather than engineered around. The hi/lo block allocator above is the escape hatch
if it ever proves real.

## Implementation issues

| Issue | Scope | Status |
|---|---|---|
| [#2473](https://github.com/ginderjeremiah/GameServer/issues/2473) | Carry a per-player write sequence on the envelope and the producing aggregates | **Shipped** (#2492), minus the cold-load seed |
| [#2474](https://github.com/ginderjeremiah/GameServer/issues/2474) | Add `PlayerWriteWatermark` and guard the absolute-value handlers against stale writes | Part (a) **shipped** (#2494): the table, the guard helper, rejection accounting, `PlayerCore` + `Progress` |
| [#2495](https://github.com/ginderjeremiah/GameServer/issues/2495) | Part (b) — guard the equipment and mod handlers (dual-key, tombstone) | **Shipped** (#2501) |
| [#2496](https://github.com/ginderjeremiah/GameServer/issues/2496) | Part (c) — guard the remaining single-row handlers | **Shipped** (#2502) |
| [#2500](https://github.com/ginderjeremiah/GameServer/issues/2500) | Seed the counter from the persisted watermark on a cold load | Open — **live lost-write path** |
| [#2475](https://github.com/ginderjeremiah/GameServer/issues/2475) | Retire `_parkedPlayerLanes`, made redundant by the guard | **Shipped** (#2510) |

The guard is complete: `DomainEventEnvelope.Sequence` carries the stamp with `Unsequenced = 0` as an
explicit sentinel constant, `Player.AdvanceWriteSequence()` pre-increments so a cold-loaded aggregate's
first stamp is 1, and all eleven order-sensitive handlers now compare against a `PlayerWriteWatermark` row.
The four pure insert-if-missing handlers are deliberately unguarded, being genuinely convergent. #2460's
instance-local deferral has been retired with it, so ordering now rests on the guard alone rather than on
two mechanisms with only one load-bearing.

**#2500 is the one remaining gap**, and it is the reason the chain isn't finished: the cold-load seed below
never shipped, so a counter that reseeds to 0 has every guarded write rejected until it climbs back.

**The cold-load seed specified above is *not* live**, and that matters more now that the guard reads the
stamp. #2473 deferred it deliberately — the table holding the watermarks didn't exist yet, and nothing
consumed the stamp, so a cold load reseeding from 0 was harmless. #2494 removed that precondition, and
#2501/#2502 widened the blast radius to every stream. Until #2500 lands, a returning player whose cache
lapsed reseeds at 0, stamps 1, and has **every** guarded write rejected against their own previous
high-water mark until the counter climbs back past it.

Only the whole-state streams self-heal: `PlayerCore`, `SelectedSkills` and `AttributeAllocations` each
carry the player's complete current state, so the first save past the old high-water mark repairs the row.
The per-target streams do not — `Progress`, `Equipment`, `Mods`, `LogPreference`, `ItemFavorite` and
`LessonRead` all carry only what one save touched, so a statistic, challenge completion, equip, mod, or
preference changed **only** inside the rejected window is never re-written and stays wrong permanently.

The lesson worth carrying: that deferral was recorded only in two code comments and in neither this table
nor #2474's body, which is precisely why it survived into a shipped guard. A deferral that crosses an issue
boundary belongs in the artifact the next implementer reads.

## Aside: where the original assumption eroded

The [#548 spike](./548-battle-end-io-write-behind.md) recorded, as a key enabling fact, that *"the queue
drains FIFO and sequentially … so per-player events apply in order — no stale-overwrite from
reordering."* That was accurate for a single-instance, strictly-serial drain. Multi-instance operation and
then #1701's bounded per-pass concurrency each eroded it a little further, and the tolerance argument in
`docs/backend-persistence.md` was extended along the way without re-deriving which reorderings actually
converge. The guard proposed here replaces that inherited assumption with an invariant the handlers
enforce for themselves.
