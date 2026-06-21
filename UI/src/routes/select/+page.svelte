<div class="select-screen" data-testid="select-screen">
	<div class="select-panel">
		<DiamondMark size={18} marginBottom={32} />

		<div class="heading">
			<h1 data-testid="select-heading">Choose your character.</h1>
			<p class="subtitle">Pick who to play as, or forge a new hero.</p>
		</div>

		{#if view}
			<StatusLine type={view.error ? 'err' : 'idle'} text={view.error ?? ''} />

			<div class="character-list" data-testid="character-list">
				{#each view.players as summary (summary.id)}
					<PlayerCard
						{summary}
						pending={view.pendingId === summary.id}
						disabled={view.busy}
						onSelect={(id) => view?.select(id)}
					/>
				{/each}
			</div>

			{#if view.showCreate}
				<CreateCharacter
					bind:value={view.newName}
					valid={view.nameValidation.ok}
					validationMsg={view.nameValidation.msg}
					error={view.createError}
					creating={view.creating}
					onSubmit={() => view?.create()}
					onCancel={() => view?.toggleCreate()}
				/>
			{:else}
				<button
					type="button"
					class="add-character"
					data-testid="show-create"
					disabled={view.busy}
					onclick={() => view?.toggleCreate()}
				>
					+ New character
				</button>
			{/if}

			<button type="button" class="sign-out" data-testid="sign-out" onclick={signOut}> Use a different account </button>
		{/if}
	</div>
</div>

<script lang="ts">
import { onMount } from 'svelte';
import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { ApiRequest, getRefreshToken, logout, reportDeviceInfo, setTokens } from '$lib/api';
import type { IPlayerData } from '$lib/api';
import { playerManager } from '$lib/engine';
import DiamondMark from '$components/DiamondMark.svelte';
import StatusLine from '../login/StatusLine.svelte';
import { confirmSessionTakeover } from '../login/session-takeover';
import PlayerCard from './PlayerCard.svelte';
import CreateCharacter from './CreateCharacter.svelte';
import { PlayerSelectView, type PlayerSelectDeps } from './player-select-view.svelte';
import { playerSelectHandoff } from './player-select-handoff';

let view = $state<PlayerSelectView | null>(null);

// Bind the chosen character: rotate the token to carry it (SelectPlayer), store the new pair, and
// return the loaded player. A missing refresh token means the session is no longer usable here.
const selectPlayer: PlayerSelectDeps['selectPlayer'] = async (playerId) => {
	const refreshToken = getRefreshToken();
	if (!refreshToken) {
		return { ok: false, error: 'Your session is no longer valid. Please log in again.' };
	}

	const response = await new ApiRequest('Login/SelectPlayer').post({ playerId, refreshToken });
	if (response.status !== 200) {
		return { ok: false, error: response.error ?? 'Could not enter the game.' };
	}

	setTokens(response.data.tokens);
	return { ok: true, player: response.data.player };
};

const createPlayer: PlayerSelectDeps['createPlayer'] = async (name) => {
	const response = await new ApiRequest('Login/CreatePlayer').post({ name });
	if (response.status !== 200) {
		return { ok: false, error: response.error ?? 'Could not create the character.' };
	}
	return { ok: true, summary: response.data };
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
		{ selectPlayer, createPlayer, confirmTakeover: confirmSessionTakeover, enterWorld },
		summaries
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

.select-panel {
	width: 400px;
	max-width: 100%;
}

.heading {
	text-align: center;
	margin-bottom: 28px;

	h1 {
		margin: 0;
		padding: 0;
		font-size: 28px;
		font-weight: 400;
		letter-spacing: -0.5px;
		line-height: 1.1;
		color: var(--text-primary);
	}

	.subtitle {
		margin: 10px 0 0;
		font-size: 12.5px;
		color: var(--text-secondary);
		font-family: var(--mono);
		letter-spacing: 0.6px;
	}
}

.character-list {
	display: flex;
	flex-direction: column;
	gap: 10px;
}

.add-character {
	width: 100%;
	margin-top: 12px;
	padding: 13px 0;
	background: transparent;
	color: var(--text-secondary);
	border: 1px dashed color-mix(in srgb, var(--text-primary) 30%, transparent);
	border-radius: 2px;
	font-size: 13px;
	font-weight: 500;
	font-family: inherit;
	letter-spacing: 1px;
	text-transform: uppercase;
	cursor: pointer;
	transition: all 160ms;

	&:hover:not(:disabled),
	&:focus-visible {
		color: var(--text-primary);
		border-color: var(--accent);
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
