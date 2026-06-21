import { createHook, getEventCounter } from '$lib/common';
import { logicEngine } from './engine';

const renderUpdateHook = createHook<[number, number]>();
const notifyRenderUpdate = renderUpdateHook.notify;
export const onRenderUpdate = renderUpdateHook.onNotified;

export class RenderEngine {
	public time = 0;
	public logicalDelta = 0;
	public tickRate = 0;

	private running = false;
	private countTick = getEventCounter((t) => (this.tickRate = Math.round(t)));

	public start() {
		if (!this.running) {
			// Re-seed the clock so the first frame's delta is ~one frame, not the entire wall-clock gap
			// the engine was stopped (mirrors LogicalEngine.start, which resets its clock to avoid this).
			this.time = performance.now();
			this.running = true;
			this.renderLoop();
		}
	}

	public stop() {
		this.running = false;
	}

	//use performance.now instead of animation frame timestamp, because frame stamp is before some amount of processing.
	//using frame timestamp can cause render loop to appear behind logical loop
	private renderLoop() {
		if (this.running) {
			this.update();
			window.requestAnimationFrame(() => this.renderLoop());
		}
	}

	private update = () => {
		this.countTick();
		const newTime = performance.now();
		const delta = newTime - this.time;
		this.time = newTime;
		// Floor at 0: the logical engine's tab-background catch-up branch advances logicEngine.time by the
		// discarded excess, which can momentarily push it past the render clock. A negative logicalDelta
		// would drive the render-only charge/effect interpolation backwards for a frame (purely cosmetic —
		// logical state and battle parity are unaffected).
		this.logicalDelta = Math.max(0, newTime - logicEngine.time);
		notifyRenderUpdate(delta, this.logicalDelta);
	};
}
