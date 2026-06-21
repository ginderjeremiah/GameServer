import { onIdleTimeLost } from './logical-engine';
import { toastWarning } from '$stores';
import { safeLocalStorage } from '$lib/common/local-storage';
import type { Action } from '$lib/common';

// A backgrounded tab is throttled by the browser, so the logical engine discards the idle time it
// can't process within its catch-up cap (see logical-engine `update`). Once a single hidden period
// has silently cost the player more than this much idle progress, nudge them — once, ever — to
// allowlist the game as always-active so the foreground rate keeps running while the tab is hidden.
const LOST_TIME_NOTICE_THRESHOLD_MS = 60_000;
const NOTICE_SHOWN_KEY = 'gs:bgThrottleNoticeShown';

const BASE_MESSAGE = 'Idle progress pauses while this tab runs in the background.';

/**
 * The notice text, tailored to the current browser. Chromium browsers expose a per-site
 * always-active list; Firefox and Safari have no per-site equivalent, so we say so plainly and
 * fall back to keeping the tab in the foreground.
 */
export const backgroundThrottleGuidance = (): string => {
	const ua = typeof navigator !== 'undefined' ? navigator.userAgent : '';
	// Edge's UA also contains "Chrome", so it must be matched first.
	if (/Edg\//.test(ua)) {
		return `${BASE_MESSAGE} To keep progressing, open Edge Settings → System and performance and add this site to “Never put these sites to sleep.”`;
	}
	if (/Chrome\//.test(ua)) {
		return `${BASE_MESSAGE} To keep progressing, open Chrome Settings → Performance and add this site to “Always keep these sites active.”`;
	}
	if (/Firefox\//.test(ua)) {
		return `${BASE_MESSAGE} Firefox has no per-site always-active setting — keep the game in a focused window to keep progressing.`;
	}
	if (/Safari\//.test(ua)) {
		return `${BASE_MESSAGE} Safari has no per-site always-active setting — keep the game tab in the foreground to keep progressing.`;
	}
	return `${BASE_MESSAGE} Check your browser’s performance settings for a “keep sites active” option, or keep the game tab in the foreground.`;
};

/**
 * Watches for meaningful idle time lost to background-tab throttling and shows a one-time,
 * dismissible notice nudging the user to allowlist the game as always-active. Tied to the engine
 * lifecycle (started/stopped alongside the logical engine in `engine.ts`).
 */
export class BackgroundThrottleMonitor {
	private unhookIdleTimeLost?: Action;
	private lostWhileHiddenMs = 0;
	private running = false;

	public start() {
		if (this.running || typeof document === 'undefined') {
			return;
		}
		this.running = true;
		this.lostWhileHiddenMs = 0;
		this.unhookIdleTimeLost = onIdleTimeLost((lostMs) => this.recordLostTime(lostMs));
		document.addEventListener('visibilitychange', this.handleVisibilityChange);
	}

	public stop() {
		if (!this.running) {
			return;
		}
		this.running = false;
		this.lostWhileHiddenMs = 0;
		this.unhookIdleTimeLost?.();
		this.unhookIdleTimeLost = undefined;
		document.removeEventListener('visibilitychange', this.handleVisibilityChange);
	}

	// Only count time discarded while the tab is hidden: the catch-up cap can also clip a foreground
	// main-thread stall, but the backgrounding case is the one this notice addresses.
	private recordLostTime(lostMs: number) {
		if (document.hidden) {
			this.lostWhileHiddenMs += lostMs;
		}
	}

	private handleVisibilityChange = () => {
		if (document.hidden) {
			// Entering a new hidden period — measure it on its own.
			this.lostWhileHiddenMs = 0;
			return;
		}
		// Back in the foreground: if the hidden period cost a meaningful amount of idle progress, nudge.
		if (this.lostWhileHiddenMs >= LOST_TIME_NOTICE_THRESHOLD_MS) {
			this.maybeShowNotice();
		}
		this.lostWhileHiddenMs = 0;
	};

	private maybeShowNotice() {
		const storage = safeLocalStorage();
		if (storage?.getItem(NOTICE_SHOWN_KEY)) {
			return;
		}
		// Persist before showing so the one-time guarantee holds even if the toast is dismissed instantly.
		storage?.setItem(NOTICE_SHOWN_KEY, '1');
		toastWarning(backgroundThrottleGuidance(), { duration: 0 });
	}
}
