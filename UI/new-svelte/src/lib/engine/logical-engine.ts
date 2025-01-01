import { createHook, getEventCounter } from '$lib/common';

export const tickSize = 40; //ms
const tickSizeX5 = tickSize * 5;

const logicalUpdateHook = createHook<[number]>();
const notifyLogicalUpdate = logicalUpdateHook.notify;
export const onLogicalUpdate = logicalUpdateHook.onNotified;

export class LogicalEngine {
	public time = 0;
	public tickRate = 0;

	private lastTime = 0;
	private timeBank = 0;
	private countTick = getEventCounter((t) => (this.tickRate = Math.round(t)));
	private tickHandle = 0;

	public start() {
		if (this.tickHandle) {
			return;
		}

		this.lastTime = performance.now();
		this.time = this.lastTime;

		const that = this;
		this.tickHandle = window.setInterval(() => that.logicLoop(), 10);
	}

	public stop() {
		if (!this.tickHandle) {
			return;
		}

		window.clearInterval(this.tickHandle);
		this.tickHandle = 0;
		this.lastTime = 0;
		this.timeBank = 0;
		this.time = 0;
		this.tickRate = 0;
	}

	private logicLoop() {
		const now = performance.now();
		const ts = now - this.lastTime;
		this.lastTime = now;
		this.update(ts);
	}

	private update(timeDelta: number) {
		if (timeDelta > tickSizeX5) {
			this.time += timeDelta - tickSizeX5;
			timeDelta = tickSizeX5;
		}

		this.timeBank += timeDelta;
		while (this.timeBank >= tickSize) {
			this.timeBank -= tickSize;
			this.time += tickSize;
			this.countTick();
			notifyLogicalUpdate(tickSize);
		}
	}
}
