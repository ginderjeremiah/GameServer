import { describe, it, expect, vi } from 'vitest';
import { CoalescedLoader } from '$lib/common/coalesced-loader';

// CoalescedLoader coalesces concurrent load() calls onto a single in-flight fetch while keeping
// `force` honest: a forced load issued mid-flight queues one fresh fetch behind the stale
// in-flight one instead of resolving with data requested before the call.

/** Drains queued microtasks (via a macrotask) so chained fetches have fired before asserting. */
const flush = () => new Promise<void>((resolve) => setTimeout(resolve, 0));

/** A loader over manually-settled fetches so tests control settlement order. Settling a fetch
 *  marks the harness loaded, mirroring the stores' fetchers. */
const harness = () => {
	let loaded = false;
	const pending: Array<() => void> = [];
	const fetchFn = vi.fn(
		() =>
			new Promise<void>((resolve) => {
				pending.push(() => {
					loaded = true;
					resolve();
				});
			})
	);
	return {
		fetchFn,
		loader: new CoalescedLoader(fetchFn, () => loaded),
		/** Settles the n-th issued fetch (0-based, in issue order). */
		settle(index: number) {
			pending[index]?.();
		}
	};
};

describe('CoalescedLoader', () => {
	it('fetches on first load and no-ops once loaded', async () => {
		const h = harness();

		const first = h.loader.load();
		h.settle(0);
		await first;

		await h.loader.load();
		expect(h.fetchFn).toHaveBeenCalledTimes(1);
	});

	it('re-fetches an already-loaded store when forced', async () => {
		const h = harness();
		const first = h.loader.load();
		h.settle(0);
		await first;

		const forced = h.loader.load(true);
		expect(h.fetchFn).toHaveBeenCalledTimes(2);
		h.settle(1);
		await forced;
	});

	it('coalesces concurrent non-forced loads onto a single fetch', async () => {
		const h = harness();

		const loads = [h.loader.load(), h.loader.load(), h.loader.load()];
		expect(h.fetchFn).toHaveBeenCalledTimes(1);

		h.settle(0);
		await Promise.all(loads);
		expect(h.fetchFn).toHaveBeenCalledTimes(1);
	});

	it('a force issued mid-flight queues one fresh fetch behind the stale one', async () => {
		const h = harness();
		const initial = h.loader.load();

		let forcedResolved = false;
		const forced = h.loader.load(true).then(() => {
			forcedResolved = true;
		});
		// The fresh fetch is queued, not issued concurrently with the stale one.
		expect(h.fetchFn).toHaveBeenCalledTimes(1);

		h.settle(0);
		await initial;
		await flush();
		// The stale fetch settling releases the queued fresh fetch, but the forced caller
		// must not resolve until that fresh fetch completes.
		expect(h.fetchFn).toHaveBeenCalledTimes(2);
		expect(forcedResolved).toBe(false);

		h.settle(1);
		await forced;
	});

	it('concurrent mid-flight forces coalesce onto the same queued fresh fetch', async () => {
		const h = harness();
		const initial = h.loader.load();

		const forcedA = h.loader.load(true);
		const forcedB = h.loader.load(true);

		h.settle(0);
		await initial;
		await flush();
		expect(h.fetchFn).toHaveBeenCalledTimes(2);

		h.settle(1);
		await Promise.all([forcedA, forcedB]);
		expect(h.fetchFn).toHaveBeenCalledTimes(2);
	});

	it('a force issued while the queued fresh fetch is running chains another fetch', async () => {
		const h = harness();
		const initial = h.loader.load();
		const firstForce = h.loader.load(true);

		h.settle(0);
		await initial;
		await flush();
		expect(h.fetchFn).toHaveBeenCalledTimes(2);

		// Fetch 1 is now in flight and predates this force, so a third fetch must be queued.
		const secondForce = h.loader.load(true);
		h.settle(1);
		await firstForce;
		await flush();
		expect(h.fetchFn).toHaveBeenCalledTimes(3);

		h.settle(2);
		await secondForce;
	});

	it('a non-forced load issued mid-flight does not queue a fresh fetch', async () => {
		const h = harness();
		const initial = h.loader.load();
		const second = h.loader.load();

		h.settle(0);
		await Promise.all([initial, second]);
		await flush();
		expect(h.fetchFn).toHaveBeenCalledTimes(1);
	});

	it('retries on the next load when the first fetch did not mark the store loaded', async () => {
		let loaded = false;
		const fetchFn = vi.fn(async () => {
			// First attempt fails (stays unloaded), second succeeds — mirroring the stores' catch.
			loaded = fetchFn.mock.calls.length > 1;
		});
		const loader = new CoalescedLoader(fetchFn, () => loaded);

		await loader.load();
		expect(loaded).toBe(false);

		await loader.load();
		expect(loaded).toBe(true);
		expect(fetchFn).toHaveBeenCalledTimes(2);
	});

	it('reset() abandons a queued fresh fetch so it never fires post-reset', async () => {
		const h = harness();
		const initial = h.loader.load();
		const forced = h.loader.load(true);

		h.loader.reset();
		h.settle(0);
		await Promise.all([initial, forced]);
		await flush();
		// The queued fresh fetch belonged to the discarded session and must not be issued.
		expect(h.fetchFn).toHaveBeenCalledTimes(1);
	});

	it('a load after reset() starts a new fetch instead of awaiting the stale one', async () => {
		const h = harness();
		const stale = h.loader.load();

		h.loader.reset();
		const fresh = h.loader.load();
		expect(h.fetchFn).toHaveBeenCalledTimes(2);

		// The stale fetch settling must not clobber the new in-flight fetch: a force arriving
		// afterwards queues behind it (no immediate third fetch) instead of seeing an empty pipeline.
		h.settle(0);
		await stale;
		await flush();
		const forced = h.loader.load(true);
		expect(h.fetchFn).toHaveBeenCalledTimes(2);

		h.settle(1);
		await fresh;
		await flush();
		expect(h.fetchFn).toHaveBeenCalledTimes(3);

		h.settle(2);
		await forced;
	});
});
