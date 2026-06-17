/* Combat-log panel resize view-model.

   Owns the draggable height of the log panel and the in-flight resize gesture.
   The panel sits in a flex column with the screen content above it, so growing
   the log shrinks the content area; the height is therefore clamped so neither
   the log nor the screen above it can collapse. The chosen size is persisted to
   local storage so it survives a refresh.

   The DOM-dependent inputs (the live pointer position and the height of the
   container the panel shares with the screen) are supplied by the LogPanel
   component; the clamping and persistence logic lives here so it stays
   unit-testable without a DOM. */

import { safeLocalStorage } from '$lib/common/local-storage';

/** Smallest the log panel itself is allowed to get. */
export const MIN_LOG_PANEL_HEIGHT = 96;
/** Default panel height — roughly the original fixed combat-log size. */
export const DEFAULT_LOG_PANEL_HEIGHT = 168;
/** Keep at least this much room for the screen content above the log. */
export const MIN_SCREEN_HEIGHT = 160;
/** Height change per keyboard arrow step on the resize separator. */
export const LOG_PANEL_KEYBOARD_STEP = 16;

const STORAGE_KEY = 'gameserver.logPanelHeight';

/** Clamp a candidate height to `[MIN_LOG_PANEL_HEIGHT, max]`. `max` is floored at
 *  the minimum so a tiny container can never invert the bounds. Pure for testing. */
export const clampLogPanelHeight = (height: number, max: number): number => {
	const upper = Math.max(MIN_LOG_PANEL_HEIGHT, max);
	return Math.min(Math.max(height, MIN_LOG_PANEL_HEIGHT), upper);
};

export class LogPanelView {
	/** Current panel height in px (drives the inline style and aria-valuenow). */
	height = $state(DEFAULT_LOG_PANEL_HEIGHT);
	/** True while a resize drag is in progress (drives the grabbing cursor). */
	dragging = $state(false);
	/** Latest known upper bound on the height — the container's share minus the
	 *  screen reserve. Unbounded until a container has been measured (e.g. SSR);
	 *  kept current at every measurement so the drag and aria-valuemax agree. */
	maxHeight = $state(Number.POSITIVE_INFINITY);

	private startY = 0;
	private startHeight = DEFAULT_LOG_PANEL_HEIGHT;

	/** Upper bound for a given container height — its share minus the screen reserve.
	 *  `undefined` (no measurement yet, e.g. SSR) leaves the height unbounded above. */
	private maxFor(available: number | undefined): number {
		return available === undefined ? Number.POSITIVE_INFINITY : available - MIN_SCREEN_HEIGHT;
	}

	/** The effective upper bound to report as aria-valuemax — the live max floored at
	 *  the minimum (mirroring `clampLogPanelHeight`), or `undefined` before any
	 *  container has been measured (so the attribute is omitted rather than `Infinity`). */
	get ariaMax(): number | undefined {
		return Number.isFinite(this.maxHeight) ? Math.round(Math.max(MIN_LOG_PANEL_HEIGHT, this.maxHeight)) : undefined;
	}

	/** Load any persisted height. Call after mount so the initial client render
	 *  matches the SSR markup (which has no storage) and avoids a hydration
	 *  mismatch on the inline height. `available` (the live container height, once
	 *  measured) caps the restored value so a size saved on a larger viewport can't
	 *  overflow a smaller one. */
	hydrate(available?: number): void {
		this.maxHeight = this.maxFor(available);
		const raw = safeLocalStorage()?.getItem(STORAGE_KEY);
		if (raw === null || raw === undefined) {
			return;
		}
		const parsed = Number(raw);
		if (Number.isFinite(parsed)) {
			this.height = clampLogPanelHeight(parsed, this.maxHeight);
		}
	}

	/** Re-clamp the current height to fit a (possibly shrunken) container — e.g. on
	 *  window resize — so it can never overflow the space it shares with the screen. */
	clampToAvailable(available: number): void {
		this.maxHeight = this.maxFor(available);
		this.height = clampLogPanelHeight(this.height, this.maxHeight);
	}

	/** Begin a resize. `available` is the height of the container the panel shares
	 *  with the screen content, used to cap how tall the log can grow. */
	beginResize(clientY: number, available: number): void {
		this.startY = clientY;
		this.startHeight = this.height;
		this.maxHeight = this.maxFor(available);
		this.dragging = true;
	}

	/** Update the height from the latest pointer position. Dragging the top edge
	 *  up (a smaller `clientY`) grows the log; dragging down shrinks it. */
	moveResize(clientY: number): void {
		if (!this.dragging) {
			return;
		}
		this.height = clampLogPanelHeight(this.startHeight + (this.startY - clientY), this.maxHeight);
	}

	/** Finish a resize and persist the chosen height. */
	endResize(): void {
		if (!this.dragging) {
			return;
		}
		this.dragging = false;
		this.persist();
	}

	/** Resize by a fixed keyboard step: a positive `delta` grows the log, a negative
	 *  one shrinks it. `available` is the live container height used to cap growth
	 *  (as in a drag). The result is clamped and persisted, mirroring a completed drag. */
	stepResize(delta: number, available?: number): void {
		this.maxHeight = this.maxFor(available);
		this.height = clampLogPanelHeight(this.height + delta, this.maxHeight);
		this.persist();
	}

	/** Jump the height to the smallest (`'min'`) or largest (`'max'`) the container
	 *  allows — the keyboard Home/End affordance. `available` caps the maximum; with
	 *  no measured container a `'max'` jump is a no-op (the bound is unknown). */
	resizeTo(edge: 'min' | 'max', available?: number): void {
		this.maxHeight = this.maxFor(available);
		if (edge === 'max' && !Number.isFinite(this.maxHeight)) {
			return;
		}
		const target = edge === 'min' ? MIN_LOG_PANEL_HEIGHT : this.maxHeight;
		this.height = clampLogPanelHeight(target, this.maxHeight);
		this.persist();
	}

	/** Persist the current height (best-effort; ignore quota/availability errors). */
	private persist(): void {
		try {
			safeLocalStorage()?.setItem(STORAGE_KEY, String(Math.round(this.height)));
		} catch {
			// Persistence is best-effort; ignore quota/availability errors.
		}
	}
}
