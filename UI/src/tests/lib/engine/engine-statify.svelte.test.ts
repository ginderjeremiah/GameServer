import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { flushSync } from 'svelte';
import { statify } from '$lib/common';
import { stubEngineWindow } from './engine-window-stub';

// RenderEngine reads the shared logical clock; the real module pulls in the whole engine graph.
const logicEngineStub = vi.hoisted(() => ({ time: 0 }));
vi.mock('$lib/engine/engine', () => ({ logicEngine: logicEngineStub }));

const { rafCallbacks, reset: resetEngineWindow } = stubEngineWindow();

import { LogicalEngine, tickSize } from '$lib/engine/logical-engine';
import { RenderEngine } from '$lib/engine/render-engine';

/** Whether `statify` installed a `$state`-backed accessor for the field on this instance. */
const isStatified = (instance: object, field: string) =>
	Object.getOwnPropertyDescriptor(instance, field)?.get !== undefined;

// Both engines are wrapped in `statify` (`$lib/engine/engine.ts`), which turns every *enumerable* field
// into a `$state` accessor. Their clocks and loop bookkeeping are written on every tick/frame with no
// reactive consumer, so they are `#`-private to stay out of the proxy (#2123) — `tickRate` is the sole
// reactive field (the nav sidebar's rate readout). These pin that split, which is invisible at the call
// site: a `#`-field silently promoted back to a public one would still pass every other engine test.
describe('engine internals under statify (#2123)', () => {
	describe('LogicalEngine', () => {
		let engine: LogicalEngine;

		beforeEach(() => {
			vi.useFakeTimers();
			engine = statify(new LogicalEngine());
		});

		afterEach(() => {
			engine.stop();
			vi.useRealTimers();
		});

		it('statifies tickRate and nothing else', () => {
			expect(isStatified(engine, 'tickRate')).toBe(true);
			// `time` is a prototype getter over a `#`-private field, so statify never enumerated it.
			expect(Object.getOwnPropertyDescriptor(engine, 'time')).toBeUndefined();
		});

		it('advances the logical clock without notifying a $derived that reads it', () => {
			let observed = -1;
			const cleanup = $effect.root(() => {
				const time = $derived(engine.time);
				$effect(() => {
					observed = time;
				});
			});
			flushSync();
			expect(observed).toBe(0);

			engine.start();
			vi.advanceTimersByTime(tickSize * 3);
			flushSync();

			// The clock really did move; the proxy simply never saw it.
			expect(engine.time).toBeGreaterThan(0);
			expect(observed).toBe(0);

			cleanup();
		});

		it('keeps tickRate reactive', () => {
			let observed = -1;
			const cleanup = $effect.root(() => {
				const rate = $derived(engine.tickRate);
				$effect(() => {
					observed = rate;
				});
			});
			flushSync();
			expect(observed).toBe(0);

			engine.tickRate = 25;
			flushSync();
			expect(observed).toBe(25);

			cleanup();
		});
	});

	describe('RenderEngine', () => {
		let engine: RenderEngine;
		let performanceNow: number;

		beforeEach(() => {
			resetEngineWindow();
			logicEngineStub.time = 0;
			performanceNow = 0;
			vi.spyOn(performance, 'now').mockImplementation(() => performanceNow);
			engine = statify(new RenderEngine());
		});

		afterEach(() => {
			engine.stop();
			vi.restoreAllMocks();
		});

		it('statifies tickRate and nothing else', () => {
			expect(isStatified(engine, 'tickRate')).toBe(true);
			expect(Object.getOwnPropertyDescriptor(engine, 'time')).toBeUndefined();
			expect(Object.getOwnPropertyDescriptor(engine, 'logicalDelta')).toBeUndefined();
		});

		it('advances the render clock and logicalDelta without notifying a $derived that reads them', () => {
			let observedTime = -1;
			let observedDelta = -1;
			const cleanup = $effect.root(() => {
				const time = $derived(engine.time);
				const logicalDelta = $derived(engine.logicalDelta);
				$effect(() => {
					observedTime = time;
					observedDelta = logicalDelta;
				});
			});
			flushSync();
			expect(observedTime).toBe(0);
			expect(observedDelta).toBe(0);

			performanceNow = 1000;
			logicEngineStub.time = 960;
			engine.start(); // first frame
			performanceNow = 1016;
			rafCallbacks.shift()?.(); // second frame
			flushSync();

			expect(engine.time).toBe(1016);
			expect(engine.logicalDelta).toBe(56); // 1016 − 960
			expect(observedTime).toBe(0);
			expect(observedDelta).toBe(0);

			cleanup();
		});

		it('keeps tickRate reactive', () => {
			let observed = -1;
			const cleanup = $effect.root(() => {
				const rate = $derived(engine.tickRate);
				$effect(() => {
					observed = rate;
				});
			});
			flushSync();
			expect(observed).toBe(0);

			engine.tickRate = 60;
			flushSync();
			expect(observed).toBe(60);

			cleanup();
		});
	});
});
