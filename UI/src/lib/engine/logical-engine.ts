import { createHook, getEventCounter } from '$lib/common';
import { MS_PER_TICK } from '$lib/api/types/game-constants';
import { createTickSource, type TickSource } from './tick-source';

// The logical tick size in ms — generated from the backend GameConstants so it can never drift
// from the simulation the server replays (battle parity depends on it).
export const tickSize = MS_PER_TICK;
const tickSizeX5 = tickSize * 5;

const logicalUpdateHook = createHook<[number]>();
const notifyLogicalUpdate = logicalUpdateHook.notify;
export const onLogicalUpdate = logicalUpdateHook.onNotified;

// Fires with the ms of idle time the catch-up cap discarded on an over-budget poll (e.g. a
// backgrounded, throttled tab). Consumed by the background-throttle notice, not the battle loop.
const idleTimeLostHook = createHook<[number]>();
const notifyIdleTimeLost = idleTimeLostHook.notify;
export const onIdleTimeLost = idleTimeLostHook.onNotified;

export class LogicalEngine {
	/** The measured tick rate, the one field with a reactive consumer (the nav sidebar's readout). */
	public tickRate = 0;

	// Per-tick internals are `#`-private so statify never proxies them (#2123): `#time` advances every
	// tick and `#lastTime`/`#timeBank` on every poll (~10ms), and nothing reads them reactively.
	#time = 0;
	#lastTime = 0;
	#timeBank = 0;
	#countTick = getEventCounter((t) => (this.tickRate = Math.round(t)));
	#tickSource?: TickSource;

	/** The logical clock in `performance.now()` terms. Deliberately non-reactive — poll it, don't `$derived` it. */
	public get time() {
		return this.#time;
	}

	public start() {
		if (this.#tickSource) {
			return;
		}

		this.#lastTime = performance.now();
		this.#time = this.#lastTime;

		this.#tickSource = createTickSource(() => this.logicLoop());
	}

	public stop() {
		if (!this.#tickSource) {
			return;
		}

		this.#tickSource.stop();
		this.#tickSource = undefined;
		this.#lastTime = 0;
		this.#timeBank = 0;
		this.#time = 0;
		this.tickRate = 0;
	}

	private logicLoop() {
		const now = performance.now();
		const ts = now - this.#lastTime;
		this.#lastTime = now;
		this.update(ts);
	}

	private update(timeDelta: number) {
		if (timeDelta > tickSizeX5) {
			const lostTime = timeDelta - tickSizeX5;
			this.#time += lostTime;
			timeDelta = tickSizeX5;
			notifyIdleTimeLost(lostTime);
		}

		this.#timeBank += timeDelta;
		while (this.#timeBank >= tickSize) {
			this.#timeBank -= tickSize;
			this.#time += tickSize;
			this.#countTick();
			notifyLogicalUpdate(tickSize);
		}
	}
}
