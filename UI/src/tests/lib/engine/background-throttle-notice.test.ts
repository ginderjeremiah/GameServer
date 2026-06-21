import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// Hoisted so the (hoisted) vi.mock factories below can reference them safely.
const h = vi.hoisted(() => ({
	toastWarning: vi.fn(),
	unhook: vi.fn(),
	state: {
		lsStore: {} as Record<string, string>,
		lsAvailable: true,
		idleTimeLostCb: undefined as ((ms: number) => void) | undefined
	}
}));

vi.mock('$stores', () => ({ toastWarning: h.toastWarning }));

vi.mock('$lib/common/local-storage', () => ({
	safeLocalStorage: () =>
		h.state.lsAvailable
			? {
					getItem: (k: string) => h.state.lsStore[k] ?? null,
					setItem: (k: string, v: string) => {
						h.state.lsStore[k] = v;
					}
				}
			: null
}));

// Capture the callback the monitor registers so the test can drive lost-time events directly.
vi.mock('$lib/engine/logical-engine', () => ({
	onIdleTimeLost: (cb: (ms: number) => void) => {
		h.state.idleTimeLostCb = cb;
		return h.unhook;
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
		h.state.idleTimeLostCb = undefined;
		hidden = false;
		Object.defineProperty(document, 'hidden', { configurable: true, get: () => hidden });
		monitor = new BackgroundThrottleMonitor();
	});

	afterEach(() => {
		monitor.stop();
		vi.unstubAllGlobals();
	});

	const lose = (ms: number) => h.state.idleTimeLostCb?.(ms);

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
