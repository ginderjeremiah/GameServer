/**
 * Coalesces a store's concurrent `load()` calls onto a single in-flight fetch while keeping `force`
 * honest: a forced load issued mid-flight queues one fresh fetch behind the stale in-flight one
 * (concurrent forces share it) rather than resolving with data requested before the call. The
 * player-progress stores rely on that guarantee for post-victory gate checks and for the
 * divergence-recovery reload after a failed server push. They also call {@link invalidate} when a
 * push mutates local state while a fetch is in flight, so that fetch's response — computed before
 * the push landed — is discarded instead of reverting it.
 *
 * Deliberately not re-exported from the `$lib/common` barrel: the stores consume it, and the barrel
 * transitively imports the stores (via enemy-attributes → battle-attributes), so a barrel import
 * from a store would be circular. Import it by path, like `local-storage`.
 */
export class CoalescedLoader {
	/** Tail of the current load pipeline; concurrent callers await it instead of re-fetching. */
	private inFlight: Promise<void> | undefined;
	/** Whether a fresh fetch is already queued behind the in-flight one (via a force or {@link invalidate}). */
	private freshQueued = false;
	/** Bumped by {@link reset} and {@link invalidate}; exposed via {@link currentEpoch}/{@link isStale}
	 *  so `fetchFn` can recognize its own in-flight response as no longer trustworthy. */
	private epoch = 0;
	/** Bumped only by {@link reset}. A queued fresh fetch aborts on this moving (a discarded session)
	 *  but, unlike {@link epoch}, does NOT move on {@link invalidate} — an invalidation must not abort
	 *  a chain that still owes real data to a `loaded` store or a waiting `force` caller. */
	private resetEpoch = 0;

	/** `fetchFn` must never reject — the stores' fetchers catch and record errors internally. */
	constructor(
		private readonly fetchFn: () => Promise<void>,
		private readonly isLoaded: () => boolean
	) {}

	/**
	 * Fetches via `fetchFn`. Idempotent — no-ops once loaded unless `force`, which always resolves
	 * with data from a fetch issued no earlier than this call.
	 */
	async load(force = false): Promise<void> {
		if (this.isLoaded() && !force) {
			return;
		}
		if (!this.inFlight) {
			await this.track(this.fetchFn());
			return;
		}
		if (force && !this.freshQueued) {
			// The in-flight fetch predates this force, so its response may be stale — queue one
			// fresh fetch behind it; later forces coalesce onto that queued fetch.
			this.queueFreshFetch(this.inFlight);
		}
		await this.inFlight;
	}

	/** Forget any in-flight or queued fetch (e.g. on logout / session replacement). */
	reset(): void {
		this.inFlight = undefined;
		this.freshQueued = false;
		this.epoch += 1;
		this.resetEpoch += 1;
	}

	/** Bumps the epoch so an already-in-flight fetch's eventual response is recognized as stale by
	 *  {@link isStale}, without discarding the in-flight fetch itself (unlike {@link reset}, which is
	 *  a full session teardown). Call this when a push mutates the store's data directly: the fetch's
	 *  response was computed before the push landed, so a later `load()` should not still get to
	 *  overwrite the push with it.
	 *
	 *  If that now-stale in-flight fetch was the only thing that would ever bring the store to
	 *  `loaded`, queue a fresh fetch behind it — otherwise a push landing before the store's first
	 *  load ever completes strands it on push-delta-only data forever. A fetch already queued (from
	 *  an earlier force, or a previous invalidation) is left alone: it isn't aborted by this bump (see
	 *  {@link resetEpoch}), so it still delivers real data to whatever is waiting on it. */
	invalidate(): void {
		this.epoch += 1;
		if (this.inFlight && !this.isLoaded() && !this.freshQueued) {
			this.queueFreshFetch(this.inFlight);
		}
	}

	/** Current epoch, bumped by {@link reset} and {@link invalidate}. `fetchFn` should capture this
	 *  before its first `await` and pass it to {@link isStale} afterward to detect either running
	 *  mid-flight. */
	get currentEpoch(): number {
		return this.epoch;
	}

	/** Whether `epoch` (captured from {@link currentEpoch} before an await) no longer matches — a
	 *  `reset()` or `invalidate()` ran mid-flight, so a write gated on this epoch is stale (a
	 *  discarded session, or a push whose mutation the write would otherwise revert). */
	isStale(epoch: number): boolean {
		return epoch !== this.epoch;
	}

	/** Queues a fresh fetch behind `inFlight`, replacing it as the tracked in-flight promise. Aborts
	 *  without fetching only if {@link reset} runs before `inFlight` settles — an {@link invalidate}
	 *  in that window must not cancel a fetch this chain owes to a `loaded` store or a `force` caller. */
	private queueFreshFetch(inFlight: Promise<void>): void {
		this.freshQueued = true;
		const chainResetEpoch = this.resetEpoch;
		this.track(
			inFlight.then(() => {
				if (this.resetEpoch !== chainResetEpoch) {
					return;
				}
				this.freshQueued = false;
				return this.fetchFn();
			})
		);
	}

	/** Publishes `pipeline` as the awaitable in-flight load, clearing it once it settles unless a
	 *  chained fetch has replaced it in the meantime. */
	private track(pipeline: Promise<void>): Promise<void> {
		const tracked = pipeline.finally(() => {
			if (this.inFlight === tracked) {
				this.inFlight = undefined;
			}
		});
		this.inFlight = tracked;
		return tracked;
	}
}
