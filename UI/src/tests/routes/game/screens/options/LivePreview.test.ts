import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { ELogType } from '$lib/api';
import type { LogPrefMap } from '$routes/game/screens/options/options-view.svelte';

// Stub LogRow so the test exercises the preview's ticker/effect logic, not the row's rendering.
vi.mock('$components', () => ({ LogRow: (() => {}) as unknown }));

// Control the reduced-motion gate per test.
const { prefersReducedMotion } = vi.hoisted(() => ({ prefersReducedMotion: vi.fn(() => false) }));
vi.mock('$lib/common', async (importOriginal) => ({
	...(await importOriginal<typeof import('$lib/common')>()),
	prefersReducedMotion
}));

import LivePreview from '$routes/game/screens/options/LivePreview.svelte';

// Every log type enabled, so the preview always has content to render.
const allOn: LogPrefMap = Object.fromEntries(
	Object.values(ELogType)
		.filter((v): v is number => typeof v === 'number')
		.map((id) => [id, true])
);

describe('LivePreview ticker', () => {
	beforeEach(() => {
		vi.useFakeTimers();
		prefersReducedMotion.mockReturnValue(false);
	});

	afterEach(() => {
		cleanup();
		vi.useRealTimers();
		prefersReducedMotion.mockReset();
	});

	it('starts a single repeating ticker that is not torn down each tick', () => {
		const setIntervalSpy = vi.spyOn(globalThis, 'setInterval');
		const clearIntervalSpy = vi.spyOn(globalThis, 'clearInterval');

		render(LivePreview, { props: { prefs: allOn } });

		expect(setIntervalSpy).toHaveBeenCalledTimes(1);

		// Drive several ticks: reassigning `events` must not recreate the timer (the churn bug), so
		// no further setInterval/clearInterval calls fire between ticks.
		vi.advanceTimersByTime(1600 * 4);

		expect(setIntervalSpy).toHaveBeenCalledTimes(1);
		expect(clearIntervalSpy).not.toHaveBeenCalled();
	});

	it('does not start the ticker under prefers-reduced-motion', () => {
		prefersReducedMotion.mockReturnValue(true);
		const setIntervalSpy = vi.spyOn(globalThis, 'setInterval');

		render(LivePreview, { props: { prefs: allOn } });
		vi.advanceTimersByTime(1600 * 3);

		expect(setIntervalSpy).not.toHaveBeenCalled();
	});
});
