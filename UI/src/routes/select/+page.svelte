<div class="select-screen" data-testid="select-screen">
	{#if view}
		<PlayerSelectPanel
			{view}
			heading={view.players.length === 0 ? 'Create your character.' : 'Choose your character.'}
			subtitle={view.players.length === 0
				? 'Forge your first hero to begin your journey.'
				: 'Pick who to play as, or forge a new hero.'}
		>
			{#snippet footer()}
				<button type="button" class="sign-out" data-testid="sign-out" onclick={signOut}>
					Use a different account
				</button>
			{/snippet}
		</PlayerSelectPanel>
	{/if}
</div>

<script lang="ts">
import { onMount } from 'svelte';
import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { ApiRequest, getRotatedRefreshToken, logout, reportDeviceInfo, setTokens } from '$lib/api';
import type { IPlayerData } from '$lib/api';
import { playerManager } from '$lib/engine';
import { confirmSessionTakeover } from '../login/session-takeover';
import PlayerSelectPanel from './PlayerSelectPanel.svelte';
import { PlayerSelectView, type PlayerSelectDeps } from './player-select-view.svelte';
import { playerSelectHandoff } from './player-select-handoff';

let view = $state<PlayerSelectView | null>(null);

// Bind the chosen character: rotate the token to carry it (SelectPlayer), store the new pair, and
// return the loaded player. A missing refresh token means the session is no longer usable here.
const selectPlayer: PlayerSelectDeps['selectPlayer'] = async (playerId) => {
	const refreshToken = await getRotatedRefreshToken();
	if (!refreshToken) {
		return { ok: false, error: 'Your session is no longer valid. Please log in again.' };
	}

	const response = await new ApiRequest('Players/SelectPlayer').post({ playerId, refreshToken });
	if (response.status !== 200) {
		return { ok: false, error: response.error ?? 'Could not enter the game.' };
	}

	setTokens(response.data.tokens);
	return { ok: true, player: response.data.player };
};

const createPlayer: PlayerSelectDeps['createPlayer'] = async (name, classId) => {
	// classId is the picker's choice; the view-model guarantees one is selected before calling here.
	const response = await new ApiRequest('Players/CreatePlayer').post({ name, classId });
	if (response.status !== 200) {
		return { ok: false, error: response.error ?? 'Could not create the character.' };
	}
	return { ok: true, summary: response.data };
};

// The class picker's options come over HTTP so they're reachable here, before a player is selected
// (the socket — and the reference data it serves — requires a selected player). Resolve [] on failure
// so the picker simply stays hidden.
const loadCreationData: PlayerSelectDeps['loadCreationData'] = async () => {
	const response = await new ApiRequest('Players/CharacterCreationData').get();
	return response.status === 200 ? response.data : [];
};

// Enter the game as the loaded character: report this device's capabilities (fire-and-forget) now
// that the session is bound, seed the player manager, and hand off to the loading screen.
const enterWorld = (player: IPlayerData) => {
	void reportDeviceInfo();
	playerManager.initialize(player);
	goto(resolve('/loading'));
};

const signOut = () => {
	void logout();
};

onMount(() => {
	const summaries = playerSelectHandoff.take();
	// Reached without a handoff (a refresh or a direct deep-link): there is no list to show and no
	// way to re-fetch it, so return to login.
	if (!summaries) {
		goto(resolve('/'));
		return;
	}

	view = new PlayerSelectView(
		{ selectPlayer, createPlayer, confirmTakeover: confirmSessionTakeover, enterWorld, loadCreationData },
		summaries,
		// A freshly signed-up account arrives here with no characters; open the create form so the first
		// character creation (the only class-selection surface now — #1256) is front and centre.
		{ openCreateWhenEmpty: true }
	);
});
</script>

<style lang="scss">
.select-screen {
	width: 100%;
	height: 100%;
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	padding: 40px;
}

.sign-out {
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

	&:hover,
	&:focus-visible {
		color: var(--text-secondary);
	}

	&:focus-visible {
		outline: 2px solid var(--accent);
		outline-offset: 2px;
	}
}
</style>
