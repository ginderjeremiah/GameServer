import { createHook, getEventCounter } from '$lib/common';
import { logicEngine } from './engine';
import { tickSize } from './logical-engine';

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
			this.running = true;
			this.renderLoop();
		}
	}

	public stop() {
		this.running = false;
	}

	//use performace.now instead of animation frame timestamp, because frame stamp is before some amount of processing.
	//using frame timestamp can cause render loop to be behind logical loop
	private renderLoop() {
		if (this.running) {
			this.update();
			const that = this;
			window.requestAnimationFrame(() => that.renderLoop());
		}
	}

	private update = () => {
		this.countTick();
		const newTime = performance.now();
		const delta = newTime - this.time;
		this.time = newTime;
		this.logicalDelta = newTime - logicEngine.time;
		notifyRenderUpdate(delta, this.logicalDelta);
	};
}
