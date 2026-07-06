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
	if (typeof Worker !== 'undefined') {
		let worker: Worker | null = new Worker(new URL('./tick-worker.ts', import.meta.url), {
			type: 'module'
		});
		let fallbackHandle: number | null = null;

		worker.onmessage = () => onTick();
		worker.onerror = (ev) => {
			console.error('The logical engine tick worker failed, falling back to setInterval', ev);
			worker?.terminate();
			worker = null;
			fallbackHandle = window.setInterval(onTick, pollingIntervalMs);
		};

		return {
			stop: () => {
				worker?.terminate();
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
