/**
 * Coalesces a store's concurrent `load()` calls onto a single in-flight fetch while keeping `force`
 * honest: a forced load issued mid-flight queues one fresh fetch behind the stale in-flight one
 * (concurrent forces share it) rather than resolving with data requested before the call. The
 * player-progress stores rely on that guarantee for post-victory gate checks and for the
 * divergence-recovery reload after a failed server push.
 *
 * Deliberately not re-exported from the `$lib/common` barrel: the stores consume it, and the barrel
 * transitively imports the stores (via enemy-attributes → battle-attributes), so a barrel import
 * from a store would be circular. Import it by path, like `local-storage`.
 */
export class CoalescedLoader {
	/** Tail of the current load pipeline; concurrent callers await it instead of re-fetching. */
	private inFlight: Promise<void> | undefined;
	/** Whether a fresh (post-force) fetch is already queued behind the in-flight one. */
	private freshQueued = false;
	/** Bumped by {@link reset} so a queued fresh fetch from a discarded session never fires. */
	private epoch = 0;

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
			this.freshQueued = true;
			const chainEpoch = this.epoch;
			this.track(
				this.inFlight.then(() => {
					if (this.epoch !== chainEpoch) {
						return;
					}
					this.freshQueued = false;
					return this.fetchFn();
				})
			);
		}
		await this.inFlight;
	}

	/** Forget any in-flight or queued fetch (e.g. on logout / session replacement). */
	reset(): void {
		this.inFlight = undefined;
		this.freshQueued = false;
		this.epoch += 1;
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
