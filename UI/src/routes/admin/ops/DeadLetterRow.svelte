<tr class="dl-row" class:selected data-testid="dl-row">
	<td class="c">
		<input
			type="checkbox"
			class="dl-check"
			checked={selected}
			data-testid="dl-row-check"
			aria-label="Select entry {entry.index}"
			onchange={onToggle}
		/>
	</td>
	<td class="r idx">{entry.index}</td>
	<td><ReasonBadge reason={entry.reason} /></td>
	<td class="evt">{entry.eventType ?? '—'}</td>
	<td class="r player">{entry.playerId ?? '—'}</td>
	<td class="c">
		<button
			type="button"
			class="expand"
			class:open={expanded}
			data-testid="dl-row-expand"
			aria-expanded={expanded}
			aria-label={expanded ? 'Hide payload' : 'Show payload'}
			onclick={() => (expanded = !expanded)}
		>
			<svg
				width="12"
				height="12"
				viewBox="0 0 16 16"
				fill="none"
				stroke="currentColor"
				stroke-width="1.6"
				aria-hidden="true"
			>
				<path d="M5 3.5L10 8l-5 4.5" stroke-linecap="round" stroke-linejoin="round" />
			</svg>
		</button>
	</td>
</tr>
{#if expanded}
	<tr class="dl-payload-row" data-testid="dl-row-payload">
		<td colspan={columns}>
			<pre class="dl-payload">{formatPayload(entry.rawPayload)}</pre>
		</td>
	</tr>
{/if}

<script lang="ts">
import ReasonBadge from './ReasonBadge.svelte';
import { formatPayload } from './dead-letters.svelte';
import type { IDeadLetterEntry } from '$lib/api';

interface Props {
	entry: IDeadLetterEntry;
	selected: boolean;
	/** Total table column count, so the expanded payload row spans the full width. */
	columns: number;
	onToggle: () => void;
}

const { entry, selected, columns, onToggle }: Props = $props();
let expanded = $state(false);
</script>

<style lang="scss">
.dl-row {
	td {
		padding: 9px 12px;
		border-top: 1px solid var(--border-subtle);
		vertical-align: middle;
	}
	td.c {
		text-align: center;
	}
	td.r {
		text-align: right;
	}

	&.selected td {
		background: color-mix(in srgb, var(--accent) 7%, transparent);
	}

	.idx {
		font-family: var(--mono);
		font-size: 11px;
		color: var(--text-muted);
	}
	.evt {
		font-family: var(--mono);
		font-size: 12px;
		color: var(--text-secondary);
	}
	.player {
		font-family: var(--mono);
		font-size: 12px;
		color: var(--text-tertiary);
	}
}

.dl-check {
	accent-color: var(--accent);
	cursor: pointer;
	width: 14px;
	height: 14px;
}

.expand {
	appearance: none;
	background: transparent;
	border: 1px solid var(--border-subtle);
	color: var(--text-tertiary);
	width: 24px;
	height: 24px;
	border-radius: 3px;
	cursor: pointer;
	display: inline-flex;
	align-items: center;
	justify-content: center;
	transition: all 0.13s;

	svg {
		transition: transform 0.16s ease;
	}
	&.open svg {
		transform: rotate(90deg);
	}
	&:hover {
		border-color: var(--border-light);
		color: var(--text-secondary);
	}
}

.dl-payload-row td {
	padding: 0 12px 12px 42px;
	border-top: none;
}
.dl-payload {
	margin: 0;
	padding: 12px 14px;
	max-height: 320px;
	overflow: auto;
	background: color-mix(in srgb, var(--black) 25%, transparent);
	border: 1px solid var(--border-subtle);
	border-radius: 4px;
	font-family: var(--mono);
	font-size: 11.5px;
	line-height: 1.5;
	color: var(--text-secondary);
	white-space: pre-wrap;
	word-break: break-word;
}
</style>
