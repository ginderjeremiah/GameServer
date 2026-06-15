import { describe, it, expect, vi, afterEach } from 'vitest';
import { getEventCounter } from '$lib/common';

// getEventCounter reports a windowed events-per-second figure every `eventsPerReport` events, using a
// performance.now() delta and resetting its counter/window each time. Mock the clock so the windowed
// math is deterministic.
describe('getEventCounter', () => {
	afterEach(() => {
		vi.restoreAllMocks();
	});

	it('reports events-per-second once the threshold is reached', () => {
		const nowSpy = vi.spyOn(performance, 'now').mockReturnValueOnce(1000); // captured at creation

		const reports: number[] = [];
		const tick = getEventCounter((eps) => reports.push(eps), 10);

		nowSpy.mockReturnValueOnce(2000); // read on the 10th event: 1000ms elapsed
		for (let i = 0; i < 10; i++) {
			tick();
		}

		// 10 events over 1000ms = 10 events/second.
		expect(reports).toEqual([10]);
	});

	it('does not report before the threshold is reached', () => {
		vi.spyOn(performance, 'now').mockReturnValue(0);

		const reports: number[] = [];
		const tick = getEventCounter((eps) => reports.push(eps), 10);

		for (let i = 0; i < 9; i++) {
			tick();
		}

		expect(reports).toEqual([]);
	});

	it('resets the counter and window after each report', () => {
		const nowSpy = vi.spyOn(performance, 'now').mockReturnValueOnce(0); // creation, lastCheck = 0

		const reports: number[] = [];
		const tick = getEventCounter((eps) => reports.push(eps), 5);

		nowSpy.mockReturnValueOnce(500); // first report: 5 events / 500ms = 10/s
		for (let i = 0; i < 5; i++) {
			tick();
		}

		nowSpy.mockReturnValueOnce(1500); // second report: window restarts at 500, so 5 / 1000ms = 5/s
		for (let i = 0; i < 5; i++) {
			tick();
		}

		expect(reports).toEqual([10, 5]);
	});

	it('defaults to a 10-event window', () => {
		const nowSpy = vi.spyOn(performance, 'now').mockReturnValueOnce(0);

		const reports: number[] = [];
		const tick = getEventCounter((eps) => reports.push(eps));

		nowSpy.mockReturnValueOnce(1000);
		for (let i = 0; i < 9; i++) {
			tick();
		}
		expect(reports).toEqual([]); // not yet at 10

		tick(); // 10th event fires the report
		expect(reports).toEqual([10]);
	});
});
