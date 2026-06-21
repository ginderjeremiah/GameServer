/* Welcome-back gate view-model (#1043). Runs the offline-progress check between the loading screen and
   the game shell: it issues `GetOfflineProgress`, reconciles the persisted idle-loop mode onto the fresh
   session, and either passes straight through to the game (no rewards) or re-syncs authoritative state and
   surfaces the summary gate. The idle loop is started (via the injected `enterGame`) only once the gate is
   dismissed — or immediately when there is nothing to show — so simulated and live battles never overlap.

   The engine/socket touch points are injected so the flow is unit-testable without the live engine; the
   `+page.svelte` wires the real `GetOfflineProgress`, player re-sync, mode reconciliation, and game start. */

import type { IOfflineProgressModel } from '$lib/api';

export type WelcomePhase = 'checking' | 'summary' | 'entered';

export interface WelcomeBackDeps {
	/** Issues `GetOfflineProgress`; resolves with the summary model, or null when the command errors. */
	fetchProgress: () => Promise<IOfflineProgressModel | null>;
	/** Re-pulls the authoritative player aggregate updated by the applied offline rewards. */
	resyncPlayer: () => Promise<void>;
	/** Reconciles the backend-persisted idle-loop mode onto the fresh live session (re-arms auto-fight). */
	reconcileMode: (autoChallengeBoss: boolean) => void;
	/** Starts the game engine + idle loop — deferred until the gate is dismissed, or run straight away
	 *  when there is no reward window. */
	enterGame: () => void;
}

export class WelcomeBackView {
	phase = $state<WelcomePhase>('checking');
	summary = $state<IOfflineProgressModel | null>(null);

	constructor(private readonly deps: WelcomeBackDeps) {}

	/**
	 * Runs the gate flow. Fetches the offline summary, reconciles the persisted loop mode (always — even a
	 * sub-threshold return should restore a boss-farmer's toggle), then passes straight through to the game
	 * when there is no progress, or re-syncs state and shows the summary gate when there is.
	 */
	async run(): Promise<void> {
		const progress = await this.deps.fetchProgress();

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
		this.summary = progress;
		this.phase = 'summary';
	}

	/** Dismisses the gate (or the no-gate pass-through) and starts the game exactly once. */
	enter(): void {
		if (this.phase === 'entered') {
			return;
		}
		this.phase = 'entered';
		this.deps.enterGame();
	}
}
