import { onIdleTimeLost, onLogicalUpdate } from './logical-engine';
import { toastWarning } from '$stores';
import { safeLocalStorage } from '$lib/common/local-storage';
import type { Action } from '$lib/common';

// A backgrounded tab is throttled by the browser, so the logical engine discards the idle time it
// can't process within its catch-up cap (see logical-engine `update`). Once a single hidden period
// has silently cost the player more than this much idle progress, nudge them — once, ever — to
// allowlist the game as always-active so the foreground rate keeps running while the tab is hidden.
const LOST_TIME_NOTICE_THRESHOLD_MS = 60_000;
const NOTICE_SHOWN_KEY = 'gameserver.bgThrottleNoticeShown';

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
	private unhookResumeGuard?: Action;
	private lostWhileHiddenMs = 0;
	private resumeCatchUpPending = false;
	private running = false;

	public start() {
		if (this.running || typeof document === 'undefined') {
			return;
		}
		this.running = true;
		this.lostWhileHiddenMs = 0;
		this.resumeCatchUpPending = false;
		this.clearResumeGuard();
		this.unhookIdleTimeLost = onIdleTimeLost((lostMs) => this.recordLostTime(lostMs));
		document.addEventListener('visibilitychange', this.handleVisibilityChange);
	}

	public stop() {
		if (!this.running) {
			return;
		}
		this.running = false;
		this.lostWhileHiddenMs = 0;
		this.resumeCatchUpPending = false;
		this.clearResumeGuard();
		this.unhookIdleTimeLost?.();
		this.unhookIdleTimeLost = undefined;
		document.removeEventListener('visibilitychange', this.handleVisibilityChange);
	}

	private recordLostTime(lostMs: number) {
		// A throttled tab keeps firing (slowed) timers while hidden, so it accumulates here directly.
		if (document.hidden) {
			this.lostWhileHiddenMs += lostMs;
			return;
		}
		// A frozen/discarded tab (Memory Saver — the audience this notice targets) runs no timers
		// while hidden; its single catch-up poll fires just after the resume `visibilitychange`, with
		// `document.hidden` already false. Credit that first post-resume event to the hidden period
		// that just ended rather than dropping it as a foreground stall.
		if (this.resumeCatchUpPending) {
			this.resumeCatchUpPending = false;
			this.clearResumeGuard();
			this.lostWhileHiddenMs += lostMs;
			this.evaluate();
		}
		// Otherwise it's a genuine foreground stall the catch-up cap clipped — ignore it.
	}

	private handleVisibilityChange = () => {
		if (document.hidden) {
			// Entering a new hidden period — measure it on its own.
			this.lostWhileHiddenMs = 0;
			this.resumeCatchUpPending = false;
			this.clearResumeGuard();
			return;
		}
		// Back in the foreground. A throttled tab has already accumulated its loss (evaluate it now);
		// a frozen tab's loss arrives as the next catch-up poll, so arm the one-shot resume credit —
		// but only through the very next logical tick. If that tick isn't itself the catch-up poll,
		// nothing was actually lost to backgrounding, so the credit must not linger to be misapplied
		// to some unrelated foreground stall much later.
		this.resumeCatchUpPending = true;
		this.armResumeGuard();
		this.evaluate();
	};

	private armResumeGuard() {
		this.clearResumeGuard();
		this.unhookResumeGuard = onLogicalUpdate((_tickMs, unhook) => {
			unhook();
			this.unhookResumeGuard = undefined;
			this.resumeCatchUpPending = false;
		});
	}

	private clearResumeGuard() {
		this.unhookResumeGuard?.();
		this.unhookResumeGuard = undefined;
	}

	// Nudges once if the hidden period cost a meaningful amount of idle progress.
	private evaluate() {
		if (this.lostWhileHiddenMs >= LOST_TIME_NOTICE_THRESHOLD_MS) {
			this.maybeShowNotice();
		}
	}

	private maybeShowNotice() {
		const storage = safeLocalStorage();
		if (storage?.getItem(NOTICE_SHOWN_KEY)) {
			return;
		}
		// Persist before showing so the one-time guarantee holds even if the toast is dismissed instantly.
		try {
			storage?.setItem(NOTICE_SHOWN_KEY, '1');
		} catch {
			// Quota exceeded or storage blocked — the notice still shows this session; a persistence
			// failure here just means the one-time guarantee may not survive a reload.
		}
		toastWarning(backgroundThrottleGuidance(), { duration: 0 });
	}
}
