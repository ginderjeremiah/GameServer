import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// Hoisted so the (hoisted) vi.mock factories below can reference them safely.
const h = vi.hoisted(() => ({
	toastWarning: vi.fn(),
	unhook: vi.fn(),
	unhookLogicalUpdate: vi.fn(),
	state: {
		lsStore: {} as Record<string, string>,
		lsAvailable: true,
		setItemThrows: false,
		idleTimeLostCb: undefined as ((ms: number) => void) | undefined,
		logicalUpdateCb: undefined as ((tickMs: number, unhook: () => void) => void) | undefined
	}
}));

vi.mock('$stores', () => ({ toastWarning: h.toastWarning }));

vi.mock('$lib/common/local-storage', () => ({
	safeLocalStorage: () =>
		h.state.lsAvailable
			? {
					getItem: (k: string) => h.state.lsStore[k] ?? null,
					setItem: (k: string, v: string) => {
						if (h.state.setItemThrows) {
							throw new DOMException('quota exceeded', 'QuotaExceededError');
						}
						h.state.lsStore[k] = v;
					}
				}
			: null
}));

// Capture the callbacks the monitor registers so the test can drive lost-time and regular-tick
// events directly.
vi.mock('$lib/engine/logical-engine', () => ({
	onIdleTimeLost: (cb: (ms: number) => void) => {
		h.state.idleTimeLostCb = cb;
		return h.unhook;
	},
	onLogicalUpdate: (cb: (tickMs: number, unhook: () => void) => void) => {
		h.state.logicalUpdateCb = cb;
		return h.unhookLogicalUpdate;
	}
}));

import { BackgroundThrottleMonitor, backgroundThrottleGuidance } from '$lib/engine/background-throttle-notice';

let hidden = false;
const goHidden = () => {
	hidden = true;
	document.dispatchEvent(new Event('visibilitychange'));
};
const goVisible = () => {
	hidden = false;
	document.dispatchEvent(new Event('visibilitychange'));
};

describe('BackgroundThrottleMonitor', () => {
	let monitor: BackgroundThrottleMonitor;

	beforeEach(() => {
		vi.clearAllMocks();
		h.state.lsStore = {};
		h.state.lsAvailable = true;
		h.state.setItemThrows = false;
		h.state.idleTimeLostCb = undefined;
		h.state.logicalUpdateCb = undefined;
		hidden = false;
		Object.defineProperty(document, 'hidden', { configurable: true, get: () => hidden });
		monitor = new BackgroundThrottleMonitor();
	});

	afterEach(() => {
		monitor.stop();
		vi.unstubAllGlobals();
		// vi.unstubAllGlobals doesn't undo a defineProperty, so drop our own-property override of
		// document.hidden and let it fall back to the jsdom prototype getter.
		delete (document as unknown as Record<string, unknown>).hidden;
	});

	const lose = (ms: number) => h.state.idleTimeLostCb?.(ms);
	// Simulates a regular logical tick that lost nothing — the common case, since the worker tick
	// source polls every 10ms regardless of visibility.
	const tick = () => h.state.logicalUpdateCb?.(10, h.unhookLogicalUpdate);

	it('shows a one-time dismissible notice after a hidden period loses past the threshold', () => {
		monitor.start();
		goHidden();
		lose(30_000);
		lose(40_000);
		goVisible();

		expect(h.toastWarning).toHaveBeenCalledTimes(1);
		expect(h.toastWarning).toHaveBeenCalledWith(expect.any(String), { duration: 0 });
	});

	it('does not notify when the hidden period loses less than the threshold', () => {
		monitor.start();
		goHidden();
		lose(10_000);
		goVisible();

		expect(h.toastWarning).not.toHaveBeenCalled();
	});

	it('credits a frozen tab whose loss arrives only as the post-resume catch-up poll', () => {
		monitor.start();
		goHidden(); // frozen: no timers fire while hidden, so nothing accumulates
		goVisible(); // resume flips document.hidden before the catch-up poll
		lose(70_000); // the single catch-up poll, fired with document.hidden already false

		expect(h.toastWarning).toHaveBeenCalledTimes(1);
	});

	it('credits only the first post-resume event, then treats later loss as a foreground stall', () => {
		monitor.start();
		goHidden();
		goVisible();
		lose(100); // the post-resume catch-up poll — consumes the one-shot credit (below threshold)
		lose(70_000); // a genuine later foreground stall — ignored

		expect(h.toastWarning).not.toHaveBeenCalled();
	});

	it('disarms the resume credit once a regular tick passes with nothing lost, so a later foreground stall is not misattributed', () => {
		monitor.start();
		goHidden();
		goVisible(); // arms the one-shot resume credit
		tick(); // a normal post-resume tick with nothing lost — the credit must not survive this
		lose(70_000); // hours-later foreground stall (OS suspend, debugger pause, etc.)

		expect(h.toastWarning).not.toHaveBeenCalled();
	});

	it('does not let a stale resume-guard subscription from an earlier cycle disarm a fresh one', () => {
		monitor.start();
		goHidden();
		goVisible(); // arms guard #1
		goHidden();
		goVisible(); // re-arms: guard #1 must be unhooked, not left to fire later
		lose(70_000); // credited to guard #2's hidden period

		expect(h.toastWarning).toHaveBeenCalledTimes(1);
	});

	it('unsubscribes the pending resume guard on stop', () => {
		monitor.start();
		goHidden();
		goVisible(); // arms the guard
		monitor.stop();

		expect(h.unhookLogicalUpdate).toHaveBeenCalled();
	});

	it('ignores idle time lost while the tab is visible (a foreground stall is not backgrounding)', () => {
		monitor.start();
		lose(120_000); // visible — not background throttling
		goHidden();
		goVisible();

		expect(h.toastWarning).not.toHaveBeenCalled();
	});

	it('measures each hidden period on its own (a prior near-miss does not carry over)', () => {
		monitor.start();
		goHidden();
		lose(50_000);
		goVisible(); // below threshold; accumulator resets
		goHidden();
		lose(50_000);
		goVisible(); // again below threshold on its own

		expect(h.toastWarning).not.toHaveBeenCalled();
	});

	it('notifies at most once, ever (persisted flag suppresses later periods)', () => {
		monitor.start();
		goHidden();
		lose(70_000);
		goVisible(); // fires
		goHidden();
		lose(70_000);
		goVisible(); // suppressed

		expect(h.toastWarning).toHaveBeenCalledTimes(1);
	});

	it('stops counting and unsubscribes after stop', () => {
		monitor.start();
		monitor.stop();
		expect(h.unhook).toHaveBeenCalled();

		goHidden();
		lose(70_000);
		goVisible();
		expect(h.toastWarning).not.toHaveBeenCalled();
	});

	it('start is idempotent', () => {
		monitor.start();
		monitor.start();
		goHidden();
		lose(70_000);
		goVisible();

		expect(h.toastWarning).toHaveBeenCalledTimes(1);
	});

	it('stop before start is a no-op', () => {
		expect(() => monitor.stop()).not.toThrow();
		expect(h.unhook).not.toHaveBeenCalled();
	});

	it('degrades gracefully when local storage is unavailable (still notifies, no persistence)', () => {
		h.state.lsAvailable = false;
		monitor.start();
		goHidden();
		lose(70_000);
		goVisible();

		expect(h.toastWarning).toHaveBeenCalledTimes(1);
	});

	it('still shows the notice when persisting the one-time flag throws (quota exceeded)', () => {
		h.state.setItemThrows = true;
		monitor.start();
		goHidden();
		lose(70_000);

		expect(() => goVisible()).not.toThrow();
		expect(h.toastWarning).toHaveBeenCalledTimes(1);
	});

	it('does not start when document is unavailable (SSR)', () => {
		vi.stubGlobal('document', undefined);
		const ssrMonitor = new BackgroundThrottleMonitor();

		expect(() => ssrMonitor.start()).not.toThrow();
		// start bailed before subscribing, so the hook was never registered.
		expect(h.state.idleTimeLostCb).toBeUndefined();
	});
});

describe('backgroundThrottleGuidance', () => {
	afterEach(() => vi.unstubAllGlobals());

	it.each([
		['Mozilla/5.0 Chrome/120 Safari/537.36 Edg/120.0', /Edge Settings/], // Edge before Chrome
		['Mozilla/5.0 Chrome/120.0 Safari/537.36', /Chrome Settings/],
		['Mozilla/5.0 Firefox/120.0', /Firefox has no/],
		['Mozilla/5.0 Version/17.0 Safari/605.1.15', /Safari has no/]
	])('tailors guidance to the UA %s', (ua, matcher) => {
		vi.stubGlobal('navigator', { userAgent: ua });
		expect(backgroundThrottleGuidance()).toMatch(matcher);
	});

	it('falls back to generic guidance for an unrecognized UA', () => {
		vi.stubGlobal('navigator', { userAgent: 'SomeBot/1.0' });
		expect(backgroundThrottleGuidance()).toMatch(/keep sites active/);
	});

	it('falls back to generic guidance when navigator is unavailable', () => {
		vi.stubGlobal('navigator', undefined);
		expect(backgroundThrottleGuidance()).toMatch(/keep sites active/);
	});
});
