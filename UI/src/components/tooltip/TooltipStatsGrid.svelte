<div class="tt-stats-grid">
	{#each entries as entry (entry.name)}
		<div class="tt-stat-name">{entry.name}</div>
		<div class="tt-stat-value" class:positive={entry.value > 0} class:negative={entry.value < 0}>
			{entry.value > 0 ? '+' : ''}{entry.value}
		</div>
	{/each}
</div>

<script lang="ts">
interface StatEntry {
	name: string;
	value: number;
}

interface Props {
	/** Attribute name/value pairs; positive/negative values are signed and accent-coloured. */
	entries: StatEntry[];
}

const { entries }: Props = $props();
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
</style>
