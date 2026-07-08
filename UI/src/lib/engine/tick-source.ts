// Dedicated-worker timers are not throttled when the tab is hidden (unlike page timers), so
// driving the logical engine's polling loop from a worker is what keeps a backgrounded character's
// simulation ticking at full rate instead of falling behind wall-clock. Environments without
// `Worker` (SSR, jsdom unit tests) fall back to `window.setInterval` — that fallback also doubles
// as the unit-test seam, since `LogicalEngine` itself has no environment branching.
export const pollingIntervalMs = 10;

export interface TickSource {
	stop(): void;
}

export function createTickSource(onTick: () => void): TickSource {
	let worker: Worker | null = null;

	if (typeof Worker !== 'undefined') {
		try {
			worker = new Worker(new URL('./tick-worker.ts', import.meta.url), {
				type: 'module'
			});
		} catch (err) {
			// A CSP worker-src restriction throws synchronously from the constructor itself,
			// unlike a worker script failure, which only ever surfaces via the async onerror below.
			console.error('The logical engine tick worker failed to start, falling back to setInterval', err);
		}
	}

	if (worker !== null) {
		const activeWorker = worker;
		let fallbackHandle: number | null = null;

		activeWorker.onmessage = () => onTick();
		activeWorker.onerror = (ev) => {
			if (worker === null) {
				return;
			}
			console.error('The logical engine tick worker failed, falling back to setInterval', ev);
			activeWorker.terminate();
			worker = null;
			fallbackHandle = window.setInterval(onTick, pollingIntervalMs);
		};

		return {
			stop: () => {
				worker?.terminate();
				worker = null;
				if (fallbackHandle !== null) {
					window.clearInterval(fallbackHandle);
				}
			}
		};
	}

	const handle = window.setInterval(onTick, pollingIntervalMs);
	return {
		stop: () => window.clearInterval(handle)
	};
}
