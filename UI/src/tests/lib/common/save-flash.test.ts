import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { SaveFlash } from '$lib/common';

beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

describe('SaveFlash', () => {
	it('starts inactive', () => {
		expect(new SaveFlash().active).toBe(false);
	});

	it('flash activates immediately and clears itself after the duration elapses', () => {
		const flash = new SaveFlash(100);
		flash.flash();
		expect(flash.active).toBe(true);

		vi.advanceTimersByTime(99);
		expect(flash.active).toBe(true);

		vi.advanceTimersByTime(1);
		expect(flash.active).toBe(false);
	});

	it('a second flash before the first clears restarts the timer instead of stacking', () => {
		const flash = new SaveFlash(100);
		flash.flash();
		vi.advanceTimersByTime(60);
		flash.flash(); // restarts the 100ms window

		vi.advanceTimersByTime(60);
		expect(flash.active).toBe(true); // the original timer (would have fired at 100ms) didn't fire early

		vi.advanceTimersByTime(40);
		expect(flash.active).toBe(false);
	});

	it('reset clears the flag immediately without waiting for the timer', () => {
		const flash = new SaveFlash(100);
		flash.flash();
		flash.reset();
		expect(flash.active).toBe(false);

		// The original timer firing afterward is a no-op against the already-clear flag.
		vi.advanceTimersByTime(100);
		expect(flash.active).toBe(false);
	});

	it('dispose cancels the pending auto-clear so it never fires after teardown', () => {
		const flash = new SaveFlash(100);
		flash.flash();
		flash.dispose();

		// Had the timer survived disposal, this would flip `active` back to false.
		vi.advanceTimersByTime(100);
		expect(flash.active).toBe(true);
	});
});
