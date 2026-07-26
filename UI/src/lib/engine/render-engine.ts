import { createHook, getEventCounter } from '$lib/common';
import { logicEngine } from './engine';

const renderUpdateHook = createHook<[number, number]>();
const notifyRenderUpdate = renderUpdateHook.notify;
export const onRenderUpdate = renderUpdateHook.onNotified;

export class RenderEngine {
	/** The measured frame rate, the one field with a reactive consumer (the nav sidebar's readout). */
	public tickRate = 0;

	// Per-frame internals are `#`-private so statify never proxies them (#2123): all of these are written
	// on every animation frame, and nothing reads them reactively.
	#time = 0;
	#logicalDelta = 0;
	#running = false;
	#rafHandle?: number;
	#countTick = getEventCounter((t) => (this.tickRate = Math.round(t)));

	/** The render clock in `performance.now()` terms. Deliberately non-reactive — poll it, don't `$derived` it. */
	public get time() {
		return this.#time;
	}

	/** How far the render clock leads the logical clock (floored at 0). Non-reactive, like `time`. */
	public get logicalDelta() {
		return this.#logicalDelta;
	}

	public start() {
		if (!this.#running) {
			// Re-seed the clock so the first frame's delta is ~one frame, not the entire wall-clock gap
			// the engine was stopped (mirrors LogicalEngine.start, which resets its clock to avoid this).
			this.#time = performance.now();
			this.#running = true;
			this.renderLoop();
		}
	}

	public stop() {
		this.#running = false;
		// Cancel the pending frame so a start() within the same frame can't leave the old callback
		// running alongside the new loop (mirrors LogicalEngine.stop clearing its tickSource handle).
		if (this.#rafHandle !== undefined) {
			window.cancelAnimationFrame(this.#rafHandle);
			this.#rafHandle = undefined;
		}
	}

	//use performance.now instead of animation frame timestamp, because frame stamp is before some amount of processing.
	//using frame timestamp can cause render loop to appear behind logical loop
	private renderLoop() {
		if (this.#running) {
			this.update();
			this.#rafHandle = window.requestAnimationFrame(() => this.renderLoop());
		}
	}

	private update() {
		this.#countTick();
		const newTime = performance.now();
		const delta = newTime - this.#time;
		this.#time = newTime;
		// Floor at 0: the logical engine's tab-background catch-up branch advances logicEngine.time by the
		// discarded excess, which can momentarily push it past the render clock. A negative logicalDelta
		// would drive the render-only charge/effect interpolation backwards for a frame (purely cosmetic —
		// logical state and battle parity are unaffected).
		this.#logicalDelta = Math.max(0, newTime - logicEngine.time);
		notifyRenderUpdate(delta, this.#logicalDelta);
	}
}
