{#if emptyText !== undefined && entries.length === 0}
	<div class="tt-stat-empty">{emptyText}</div>
{:else}
	<div class="tt-stats-grid">
		{#each entries as entry (entry.name)}
			<div class="tt-stat-name">{entry.name}</div>
			<div class="tt-stat-value" class:positive={entry.value > 0} class:negative={entry.value < 0}>
				{entry.value > 0 ? '+' : ''}{format ? formatValue(entry.value) : entry.value}
			</div>
		{/each}
	</div>
{/if}

<script lang="ts">
interface StatEntry {
	name: string;
	value: number;
}

interface Props {
	/** Attribute name/value pairs; positive/negative values are signed and accent-coloured. */
	entries: StatEntry[];
	/** When set, an empty `entries` list renders this text instead of an empty grid. */
	emptyText?: string;
	/** When true, non-integer values are formatted to one decimal place (default: verbatim). */
	format?: boolean;
}

const { entries, emptyText, format = false }: Props = $props();

const formatValue = (v: number) => (Number.isInteger(v) ? v.toString() : v.toFixed(1));
</script>

<style lang="scss">
.tt-stats-grid {
	display: grid;
	grid-template-columns: 1fr auto;
	row-gap: 4px;
	column-gap: 12px;

	.tt-stat-name {
		font-size: 12px;
		color: var(--text-secondary);
	}

	.tt-stat-value {
		font-family: var(--mono);
		font-size: 11.5px;
		letter-spacing: 0.3px;
		text-align: right;
		color: color-mix(in srgb, var(--text-primary) 70%, transparent);

		&.positive {
			color: var(--success);
		}
		&.negative {
			color: var(--error);
		}
	}
}

.tt-stat-empty {
	font-size: 12px;
	font-style: italic;
	color: var(--text-muted);
	padding: 4px 0;
}
</style>
