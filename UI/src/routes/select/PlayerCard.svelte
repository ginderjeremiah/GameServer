<button
	type="button"
	class="player-card"
	class:pending
	data-testid="player-card"
	disabled={disabled || pending}
	onclick={() => onSelect(summary.id)}
>
	<div class="card-main">
		<span class="name">{summary.name}</span>
		<span class="level">Level {summary.level}</span>
	</div>
	<div class="card-meta">
		{#if pending}
			<span class="entering">Entering…</span>
		{:else if lastPlayed}
			<span class="last-played">{lastPlayed}</span>
		{/if}
	</div>
</button>

<script lang="ts">
import type { IPlayerSummary } from '$lib/api';
import { formatLastPlayed } from './last-played';

interface Props {
	summary: IPlayerSummary;
	/** This card's character is the one currently being entered. */
	pending?: boolean;
	/** A selection/creation is in flight elsewhere, so this card is temporarily inert. */
	disabled?: boolean;
	onSelect: (playerId: number) => void;
}

let { summary, pending = false, disabled = false, onSelect }: Props = $props();

const lastPlayed = $derived(formatLastPlayed(summary.lastActivity));
</script>

<style lang="scss">
.player-card {
	display: flex;
	align-items: center;
	justify-content: space-between;
	width: 100%;
	padding: 14px 18px;
	background: var(--panel);
	border: 1px solid var(--border-subtle);
	border-radius: var(--border-radius);
	color: var(--text-primary);
	font-family: inherit;
	cursor: pointer;
	text-align: left;
	transition:
		border-color 160ms,
		background 160ms;

	&:hover:not(:disabled),
	&:focus-visible {
		border-color: var(--accent);
		background: var(--panel-2);
	}

	&:focus-visible {
		outline: 2px solid var(--accent);
		outline-offset: 2px;
	}

	&:disabled {
		cursor: default;
	}

	&.pending {
		border-color: var(--accent);
	}
}

.card-main {
	display: flex;
	flex-direction: column;
	gap: 3px;
	min-width: 0;
}

.name {
	font-size: 16px;
	font-weight: 500;
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.level {
	font-size: 12px;
	font-family: var(--mono);
	letter-spacing: 0.4px;
	color: var(--text-secondary);
}

.card-meta {
	flex-shrink: 0;
	padding-left: 12px;
	font-size: 11.5px;
	font-family: var(--mono);
	letter-spacing: 0.4px;
	color: var(--text-tertiary);
}

.entering {
	color: var(--accent);
}
</style>
