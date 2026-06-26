<div class="select-panel">
	<DiamondMark size={18} marginBottom={32} />

	<div class="heading">
		<h1 data-testid="select-heading">{heading}</h1>
		<p class="subtitle">{subtitle}</p>
	</div>

	<StatusLine type={view.error ? 'err' : 'idle'} text={view.error ?? ''} />

	<div class="character-list" data-testid="character-list">
		{#each view.players as summary (summary.id)}
			<PlayerCard
				{summary}
				pending={view.pendingId === summary.id}
				disabled={view.busy}
				onSelect={(id) => view.select(id)}
			/>
		{/each}
	</div>

	{#if emptyText && view.players.length === 0 && !view.showCreate}
		<p class="empty" data-testid="empty-list">{emptyText}</p>
	{/if}

	{#if view.showCreate}
		<CreateCharacter
			bind:value={view.newName}
			valid={view.nameValidation.ok}
			validationMsg={view.nameValidation.msg}
			error={view.createError}
			creating={view.creating}
			classes={view.classes}
			classesLoading={view.classesLoading}
			selectedClassId={view.selectedClassId}
			onSelectClass={(id) => view.selectClass(id)}
			onRetryClasses={() => view.retryLoadClasses()}
			onSubmit={() => view.create()}
			onCancel={() => view.toggleCreate()}
		/>
	{:else}
		<button
			type="button"
			class="add-character"
			data-testid="show-create"
			disabled={view.busy}
			onclick={() => view.toggleCreate()}
		>
			+ New character
		</button>
	{/if}

	{@render footer()}
</div>

<script lang="ts">
import type { Snippet } from 'svelte';
import DiamondMark from '$components/DiamondMark.svelte';
import StatusLine from '../login/StatusLine.svelte';
import PlayerCard from './PlayerCard.svelte';
import CreateCharacter from './CreateCharacter.svelte';
import type { PlayerSelectView } from './player-select-view.svelte';

interface Props {
	/** The reactive selection view-model — shared by the login-flow select screen and the in-game
	 *  character switcher, which inject different selection/world-entry behaviour. */
	view: PlayerSelectView;
	heading: string;
	subtitle: string;
	/** Shown when the character list is empty (the switcher excludes the current character, so an
	 *  account with a single character has nothing else to switch to). */
	emptyText?: string;
	/** Footer affordance, which differs by host (sign out vs. cancel the switch). */
	footer: Snippet;
}

let { view, heading, subtitle, emptyText, footer }: Props = $props();
</script>

<style lang="scss">
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

.empty {
	margin: 14px 0 0;
	text-align: center;
	font-size: 12.5px;
	color: var(--text-tertiary);
	font-family: var(--mono);
	letter-spacing: 0.4px;
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
</style>
