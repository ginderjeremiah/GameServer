import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

vi.mock('svelte', async (importOriginal) => ({
	...(await importOriginal() as Record<string, unknown>),
	onDestroy: vi.fn()
}));

vi.stubGlobal('window', {
	setInterval: (...args: Parameters<typeof setInterval>) => globalThis.setInterval(...args),
	clearInterval: (...args: Parameters<typeof clearInterval>) => globalThis.clearInterval(...args),
	requestAnimationFrame: vi.fn()
});

import { LogicalEngine } from './logical-engine';
import { onLogicalUpdate, tickSize } from './logical-engine';

describe('LogicalEngine', () => {
	let engine: LogicalEngine;

	beforeEach(() => {
		vi.useFakeTimers();
		engine = new LogicalEngine();
	});

	afterEach(() => {
		engine.stop();
		vi.useRealTimers();
	});

	it('does not tick before start', () => {
		const cb = vi.fn();
		onLogicalUpdate(cb, false);

		vi.advanceTimersByTime(100);
		expect(cb).not.toHaveBeenCalled();
	});

	it('fires logical updates after start', () => {
		const cb = vi.fn();
		onLogicalUpdate(cb, false);

		engine.start();
		vi.advanceTimersByTime(tickSize + 10);

		expect(cb).toHaveBeenCalled();
		expect(cb.mock.calls[0][0]).toBe(tickSize);
	});

	it('accumulates time bank and fires multiple ticks', () => {
		const cb = vi.fn();
		onLogicalUpdate(cb, false);

		engine.start();
		vi.advanceTimersByTime(tickSize * 3);

		expect(cb.mock.calls.length).toBeGreaterThanOrEqual(2);
	});

	it('does not fire after stop', () => {
		const cb = vi.fn();
		onLogicalUpdate(cb, false);

		engine.start();
		vi.advanceTimersByTime(tickSize);
		const callCount = cb.mock.calls.length;

		engine.stop();
		vi.advanceTimersByTime(tickSize * 5);

		expect(cb.mock.calls.length).toBe(callCount);
	});

	it('start is idempotent', () => {
		engine.start();
		engine.start();

		// Should not create duplicate intervals
		const cb = vi.fn();
		onLogicalUpdate(cb, false);
		vi.advanceTimersByTime(tickSize * 2);

		// If two intervals ran, we'd get roughly double the ticks
		expect(cb.mock.calls.length).toBeLessThan(5);
	});

	it('always passes tickSize as the delta to subscribers', () => {
		const deltas: number[] = [];
		onLogicalUpdate((delta: number) => deltas.push(delta), false);

		engine.start();
		vi.advanceTimersByTime(tickSize * 3);

		for (const d of deltas) {
			expect(d).toBe(tickSize);
		}
	});

	it('resets state on stop', () => {
		engine.start();
		vi.advanceTimersByTime(tickSize * 3);

		engine.stop();
		expect(engine.time).toBe(0);
		expect(engine.tickRate).toBe(0);
	});
});
