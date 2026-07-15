/* Welcome-back gate view-model (#1043). Runs the offline-progress check between the loading screen and
   the game shell: it issues `GetOfflineProgress`, reconciles the persisted idle-loop mode onto the fresh
   session, and either passes straight through to the game (no rewards) or re-syncs authoritative state and
   surfaces the summary gate. The idle loop is started (via the injected `enterGame`) only once the gate is
   dismissed — or immediately when there is nothing to show — so simulated and live battles never overlap.

   The engine/socket touch points are injected so the flow is unit-testable without the live engine; the
   `+page.svelte` wires the real `GetOfflineProgress`, player re-sync, mode reconciliation, and game start. */

import type { IEnemyInstance, IOfflineProgressModel } from '$lib/api';

export type WelcomePhase = 'checking' | 'summary' | 'entered';

export interface WelcomeBackDeps {
	/** Issues `GetOfflineProgress`; resolves with the summary model, or null when the command errors. */
	fetchProgress: () => Promise<IOfflineProgressModel | null>;
	/** Re-pulls the authoritative player aggregate updated by the applied offline rewards. */
	resyncPlayer: () => Promise<void>;
	/** Reconciles the backend-persisted idle-loop mode onto the fresh live session (re-arms auto-fight). */
	reconcileMode: (autoChallengeBoss: boolean) => void;
	/** Starts the game engine + idle loop — deferred until the gate is dismissed, or run straight away
	 *  when there is no reward window. `activeBattle` carries a battle the summary handed back still in
	 *  progress (#1595/#1596), so the engine resumes it via replay-to-offset (#1597) instead of the idle
	 *  loop's first fetch silently abandoning it. */
	enterGame: (activeBattle?: IEnemyInstance) => void;
}

export class WelcomeBackView {
	phase = $state<WelcomePhase>('checking');
	summary = $state<IOfflineProgressModel | null>(null);
	private cancelled = false;

	constructor(private readonly deps: WelcomeBackDeps) {}

	/**
	 * Runs the gate flow. Fetches the offline summary, reconciles the persisted loop mode (always — even a
	 * sub-threshold return should restore a boss-farmer's toggle), then passes straight through to the game
	 * when there is no progress, or re-syncs state and shows the summary gate when there is.
	 *
	 * A failed fetch is retried once before giving up: `GetOfflineProgress` re-anchors the server's away-time
	 * clock on every call (even a sub-threshold one), so an immediate retry is safe and turns a transient
	 * failure into a normal empty-progress result instead of silently skipping reconciliation (#1999) — a
	 * boss-farmer would otherwise enter with auto-fight off while the backend still thinks they're farming.
	 */
	async run(): Promise<void> {
		let progress = await this.deps.fetchProgress();
		if (this.cancelled) {
			return;
		}
		if (!progress) {
			progress = await this.deps.fetchProgress();
			if (this.cancelled) {
				return;
			}
		}

		if (progress) {
			this.deps.reconcileMode(progress.autoChallengeBoss);
		}

		// No reward window (sub-threshold, a fresh character, or a failed check): enter the game directly.
		if (!progress?.hasProgress) {
			this.enter();
			return;
		}

		// Pull the reward-updated player aggregate before the engine builds the live battler from it.
		await this.deps.resyncPlayer();
		if (this.cancelled) {
			return;
		}
		this.summary = progress;
		this.phase = 'summary';
	}

	/** Dismisses the gate (or the no-gate pass-through) and starts the game exactly once. Also a no-op
	 *  once {@link cancel} has been called (the page unmounted before this ran). */
	enter(): void {
		if (this.phase === 'entered' || this.cancelled) {
			return;
		}
		this.phase = 'entered';
		this.deps.enterGame(this.summary?.activeBattle);
	}

	/**
	 * Marks the gate cancelled so a `run()` continuation that resolves after the game page has already
	 * unmounted (e.g. the `GetOfflineProgress` round-trip lands after the player navigated away) can't
	 * start the engines behind a screen that no longer owns them — {@link enter} becomes a no-op, and
	 * a still-in-flight `run()` bails out at its next await rather than reconciling mode or entering.
	 * Call from the page's `onDestroy`.
	 */
	cancel(): void {
		this.cancelled = true;
	}
}
