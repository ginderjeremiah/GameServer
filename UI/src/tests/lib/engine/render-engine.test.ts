import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

vi.mock('svelte', async (importOriginal) => ({
	...((await importOriginal()) as Record<string, unknown>),
	onDestroy: vi.fn()
}));

const logicEngineStub = vi.hoisted(() => ({ time: 0 }));
vi.mock('$lib/engine/engine', () => ({ logicEngine: logicEngineStub }));

const rafCallbacks: (() => void)[] = [];
vi.stubGlobal('window', {
	requestAnimationFrame: vi.fn((cb: () => void) => {
		rafCallbacks.push(cb);
		return rafCallbacks.length;
	})
});

import { RenderEngine, onRenderUpdate } from '$lib/engine/render-engine';

describe('RenderEngine', () => {
	let engine: RenderEngine;
	let performanceNow: number;
	/** Accumulates [delta, logicalDelta] pairs from each onRenderUpdate notification. */
	let renderUpdates: [number, number][];
	let unhook: () => void;

	beforeEach(() => {
		rafCallbacks.length = 0;
		logicEngineStub.time = 0;
		performanceNow = 0;
		renderUpdates = [];
		vi.spyOn(performance, 'now').mockImplementation(() => performanceNow);
		unhook = onRenderUpdate((delta, logicalDelta) => renderUpdates.push([delta, logicalDelta]), false);
		engine = new RenderEngine();
	});

	afterEach(() => {
		engine.stop();
		unhook();
		vi.restoreAllMocks();
	});

	describe('start / stop guard', () => {
		it('schedules a requestAnimationFrame callback on start', () => {
			engine.start();
			expect(rafCallbacks.length).toBe(1);
		});

		it('is idempotent — a second start call does not double-schedule', () => {
			engine.start();
			engine.start();
			expect(rafCallbacks.length).toBe(1);
		});

		it('stop prevents the pending RAF callback from scheduling another', () => {
			engine.start();
			engine.stop();
			rafCallbacks.shift()?.(); // renderLoop checks running → false, schedules nothing
			expect(rafCallbacks.length).toBe(0);
		});

		it('can be restarted after stop', () => {
			engine.start();
			engine.stop();
			renderUpdates.length = 0;

			engine.start();
			expect(renderUpdates.length).toBe(1);
		});
	});

	describe('update notifications', () => {
		it('notifies onRenderUpdate immediately on the first frame', () => {
			engine.start();
			expect(renderUpdates.length).toBe(1);
		});

		it('passes delta as the elapsed time since the previous frame', () => {
			performanceNow = 1000;
			engine.start(); // delta = 1000 − 0 (initial time)

			expect(renderUpdates[0][0]).toBe(1000);

			performanceNow = 1016;
			rafCallbacks.shift()?.(); // second frame: delta = 1016 − 1000 = 16

			expect(renderUpdates[1][0]).toBe(16);
		});

		it('passes logicalDelta as the difference from logicEngine.time', () => {
			performanceNow = 2000;
			logicEngineStub.time = 1960;
			engine.start();

			expect(renderUpdates[0][1]).toBe(40); // 2000 − 1960
		});

		it('updates the time property to the current performance timestamp each frame', () => {
			performanceNow = 500;
			engine.start();
			expect(engine.time).toBe(500);

			performanceNow = 516;
			rafCallbacks.shift()?.();
			expect(engine.time).toBe(516);
		});

		it('fires a notification on every rendered frame', () => {
			engine.start();
			rafCallbacks.shift()?.(); // second frame
			rafCallbacks.shift()?.(); // third frame

			expect(renderUpdates.length).toBe(3);
		});
	});

	describe('tickRate', () => {
		it('is 0 before the first report interval elapses', () => {
			engine.start(); // only 1 countTick call — well below the 10-event threshold
			expect(engine.tickRate).toBe(0);
		});

		it('is updated after 10 frames (getEventCounter default report interval)', () => {
			engine.start(); // frame 1

			for (let i = 1; i <= 9; i++) {
				performanceNow = i * 16; // 16 ms per frame
				rafCallbacks.shift()?.(); // frames 2–10
			}

			expect(engine.tickRate).toBeGreaterThan(0);
		});
	});
});
