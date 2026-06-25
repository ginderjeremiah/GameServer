<!-- In-game character switcher (#1072). An overlay that lets the player switch to another of the
	account's characters without re-logging in. It reuses the login-flow player-select UI (the shared
	`PlayerSelectPanel` driven by `PlayerSelectView`) with switch-flavoured behaviour injected: picking a
	character tears down the live game, credits the departed character server-side via `Login/SwitchPlayer`
	(the lossless-switch backend, #1071), then reloads so the boot gate resumes as the entered character —
	whose catch-up summary the existing welcome-back gate surfaces on re-entry.

	The game keeps running while the overlay is open, so cancelling is instant and lossless; only committing
	to a switch tears anything down. -->
<!-- The overlay chrome (backdrop, Escape dismissal, focus trap, focus capture+restore, scroll lock) is
	owned by the shared `Popover` primitive per docs/frontend.md; this component supplies only the content. -->
<Popover {open} {onClose} label="Switch character" closeLabel="Cancel">
	<div class="switcher-panel" data-testid="character-switcher">
		{#if phase === 'loading'}
			<div class="status-state" data-testid="switcher-loading">Loading characters…</div>
			<button type="button" class="cancel" onclick={onClose}>Cancel</button>
		{:else if phase === 'error'}
			<div class="status-state err" data-testid="switcher-error">
				{loadError ?? 'Could not load your characters.'}
			</div>
			<button type="button" class="cancel" onclick={onClose}>Cancel</button>
		{/if}

		{#if view}
			<PlayerSelectPanel
				{view}
				heading="Switch character."
				subtitle="Pick another hero, or forge a new one."
				emptyText="No other characters yet. Create one to switch to it."
			>
				{#snippet footer()}
					<button type="button" class="cancel" data-testid="switcher-cancel" disabled={view?.busy} onclick={onClose}>
						Cancel
					</button>
				{/snippet}
			</PlayerSelectPanel>
		{/if}
	</div>
</Popover>

<script lang="ts">
import { ApiRequest, apiSocket, getRefreshToken, setTokens } from '$lib/api';
import { playerManager, stopEngines } from '$lib/engine';
import Popover from '$components/Popover.svelte';
import { confirmSessionTakeover } from '../login/session-takeover';
import PlayerSelectPanel from '../select/PlayerSelectPanel.svelte';
import { PlayerSelectView, type PlayerSelectDeps } from '../select/player-select-view.svelte';

interface Props {
	/** Whether the switcher overlay is shown. The character list (re)loads each time it opens. */
	open: boolean;
	/** Closes the overlay, leaving the running game untouched. */
	onClose: () => void;
}

let { open, onClose }: Props = $props();

type Phase = 'loading' | 'error' | 'ready';
let phase = $state<Phase>('loading');
let loadError = $state<string | null>(null);
let view = $state<PlayerSelectView | null>(null);

// Picking a character commits to the switch: tear the live game down (the departed-character credit runs
// over HTTP, off this character's battle loop, so the socket and engines must be quiesced first), then
// credit + rebind through SwitchPlayer. Every terminal outcome ends in a full-page navigation, so the
// brief window with a torn-down game is never interactive.
const switchPlayer: PlayerSelectDeps['selectPlayer'] = async (playerId) => {
	const refreshToken = getRefreshToken();
	if (!refreshToken) {
		reloadToBoot();
		return { ok: false, error: 'Your session is no longer valid. Please log in again.' };
	}

	stopEngines();
	apiSocket.disconnect();

	const response = await new ApiRequest('Login/SwitchPlayer').post({ playerId, refreshToken });
	if (response.status !== 200) {
		// The game is already torn down, so there is nothing to fall back to inline — recover by reloading,
		// which the boot gate resolves into either the still-valid current session or the login screen.
		reloadToBoot();
		return { ok: false, error: response.error ?? 'Could not switch characters.' };
	}

	setTokens(response.data.tokens);
	return { ok: true, player: response.data.player };
};

const createPlayer: PlayerSelectDeps['createPlayer'] = async (name) => {
	// TODO(#1225): send the class chosen in the create-character class picker (placeholder until it ships).
	const response = await new ApiRequest('Login/CreatePlayer').post({ name, classId: 0 });
	if (response.status !== 200) {
		return { ok: false, error: response.error ?? 'Could not create the character.' };
	}
	return { ok: true, summary: response.data };
};

// Full-page navigation tears down every in-memory singleton (engines, managers, socket) cleanly, then the
// boot gate re-resolves the session from the (rotated) token. Mirrors logout's reload-based teardown.
// Used both for a successful switch — the boot gate resumes as the newly-bound character and the
// welcome-back gate surfaces its catch-up summary — and to recover from a failed switch after teardown,
// where the boot gate lands on the still-valid current session or the login screen.
const reloadToBoot = () => {
	window.location.href = '/';
};
const enterWorld = reloadToBoot;

const loadCharacters = async () => {
	phase = 'loading';
	loadError = null;
	view = null;

	const response = await new ApiRequest('Login/Players').get();
	if (response.status !== 200 || !response.data) {
		loadError = response.error ?? 'Could not load your characters.';
		phase = 'error';
		return;
	}

	// Exclude the character currently being played — you can only switch to a different one.
	const others = response.data.filter((summary) => summary.id !== playerManager.id);
	view = new PlayerSelectView(
		{ selectPlayer: switchPlayer, createPlayer, confirmTakeover: confirmSessionTakeover, enterWorld },
		others
	);
	phase = 'ready';
};

// Load (fresh) whenever the overlay opens; while closed the Popover renders nothing and no fetch runs.
$effect(() => {
	if (open) {
		void loadCharacters();
	}
});
</script>

<style lang="scss">
.switcher-panel {
	width: 400px;
	max-width: 100%;
}

.status-state {
	text-align: center;
	padding: 28px 0;
	font-size: 13px;
	font-family: var(--mono);
	letter-spacing: 0.5px;
	color: var(--text-secondary);

	&.err {
		color: var(--error);
	}
}

.cancel {
	display: block;
	width: 100%;
	margin-top: 24px;
	padding: 6px;
	background: transparent;
	border: none;
	color: var(--text-tertiary);
	font-family: var(--mono);
	font-size: 11.5px;
	letter-spacing: 0.6px;
	cursor: pointer;
	transition: color 160ms;

	&:hover:not(:disabled),
	&:focus-visible {
		color: var(--text-secondary);
	}

	&:focus-visible {
		outline: 2px solid var(--accent);
		outline-offset: 2px;
	}

	&:disabled {
		cursor: not-allowed;
		opacity: 0.6;
	}
}
</style>
