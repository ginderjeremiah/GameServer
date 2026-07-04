import { describe, it, expect, vi, afterEach } from 'vitest';

import { createTickSource, pollingIntervalMs } from '$lib/engine/tick-source';

afterEach(() => {
	vi.unstubAllGlobals();
	vi.useRealTimers();
});

describe('createTickSource', () => {
	it('falls back to window.setInterval when Worker is unavailable (the jsdom/SSR case)', () => {
		vi.stubGlobal('Worker', undefined);
		vi.useFakeTimers();

		const onTick = vi.fn();
		const source = createTickSource(onTick);

		vi.advanceTimersByTime(pollingIntervalMs * 3);
		expect(onTick.mock.calls.length).toBeGreaterThanOrEqual(2);

		const callCount = onTick.mock.calls.length;
		source.stop();
		vi.advanceTimersByTime(pollingIntervalMs * 5);
		expect(onTick).toHaveBeenCalledTimes(callCount);
	});

	class MockWorker {
		static instances: MockWorker[] = [];
		public onmessage: (() => void) | null = null;
		public onerror: ((ev: unknown) => void) | null = null;
		public terminate = vi.fn();

		constructor(
			public url: URL,
			public options: unknown
		) {
			MockWorker.instances.push(this);
		}
	}

	afterEach(() => {
		MockWorker.instances = [];
	});

	it('drives ticks from a Worker and terminates it on stop when Worker is available', () => {
		vi.stubGlobal('Worker', MockWorker);

		const onTick = vi.fn();
		const source = createTickSource(onTick);

		expect(MockWorker.instances).toHaveLength(1);
		const worker = MockWorker.instances[0];
		expect(worker.options).toEqual({ type: 'module' });
		expect(worker.url.pathname).toContain('tick-worker');

		worker.onmessage?.();
		worker.onmessage?.();
		expect(onTick).toHaveBeenCalledTimes(2);

		source.stop();
		expect(worker.terminate).toHaveBeenCalledTimes(1);
	});

	it('logs a diagnosable error instead of silently stalling if the worker fails', () => {
		vi.stubGlobal('Worker', MockWorker);
		const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

		createTickSource(vi.fn());
		const worker = MockWorker.instances[0];
		worker.onerror?.(new ErrorEvent('error', { message: 'boom' }));

		expect(errorSpy).toHaveBeenCalledTimes(1);
		errorSpy.mockRestore();
	});
});
