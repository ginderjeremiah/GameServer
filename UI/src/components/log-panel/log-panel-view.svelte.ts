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

/** Smallest the log panel itself is allowed to get. */
export const MIN_LOG_PANEL_HEIGHT = 96;
/** Default panel height — roughly the original fixed combat-log size. */
export const DEFAULT_LOG_PANEL_HEIGHT = 168;
/** Keep at least this much room for the screen content above the log. */
export const MIN_SCREEN_HEIGHT = 160;

const STORAGE_KEY = 'gameserver.logPanelHeight';

/** Clamp a candidate height to `[MIN_LOG_PANEL_HEIGHT, max]`. `max` is floored at
 *  the minimum so a tiny container can never invert the bounds. Pure for testing. */
export const clampLogPanelHeight = (height: number, max: number): number => {
	const upper = Math.max(MIN_LOG_PANEL_HEIGHT, max);
	return Math.min(Math.max(height, MIN_LOG_PANEL_HEIGHT), upper);
};

/** Returns local storage when available (absent during SSR), or null otherwise.
 *  Access is wrapped because reading `localStorage` throws in some privacy modes. */
const storage = (): Storage | null => {
	try {
		return typeof localStorage !== 'undefined' ? localStorage : null;
	} catch {
		return null;
	}
};

export class LogPanelView {
	/** Current panel height in px (drives the inline style). */
	height = $state(DEFAULT_LOG_PANEL_HEIGHT);
	/** True while a resize drag is in progress (drives the grabbing cursor). */
	dragging = $state(false);

	private startY = 0;
	private startHeight = DEFAULT_LOG_PANEL_HEIGHT;
	private maxHeight = DEFAULT_LOG_PANEL_HEIGHT;

	/** Upper bound for a given container height — its share minus the screen reserve.
	 *  `undefined` (no measurement yet, e.g. SSR) leaves the height unbounded above. */
	private maxFor(available: number | undefined): number {
		return available === undefined ? Number.POSITIVE_INFINITY : available - MIN_SCREEN_HEIGHT;
	}

	/** Load any persisted height. Call after mount so the initial client render
	 *  matches the SSR markup (which has no storage) and avoids a hydration
	 *  mismatch on the inline height. `available` (the live container height, once
	 *  measured) caps the restored value so a size saved on a larger viewport can't
	 *  overflow a smaller one. */
	hydrate(available?: number): void {
		const raw = storage()?.getItem(STORAGE_KEY);
		if (raw === null || raw === undefined) {
			return;
		}
		const parsed = Number(raw);
		if (Number.isFinite(parsed)) {
			this.height = clampLogPanelHeight(parsed, this.maxFor(available));
		}
	}

	/** Re-clamp the current height to fit a (possibly shrunken) container — e.g. on
	 *  window resize — so it can never overflow the space it shares with the screen. */
	clampToAvailable(available: number): void {
		this.height = clampLogPanelHeight(this.height, this.maxFor(available));
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
		try {
			storage()?.setItem(STORAGE_KEY, String(Math.round(this.height)));
		} catch {
			// Persistence is best-effort; ignore quota/availability errors.
		}
	}
}
