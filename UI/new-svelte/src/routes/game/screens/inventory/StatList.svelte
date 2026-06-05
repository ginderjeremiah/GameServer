{#if attrs.length}
	<div class="stat-grid">
		{#each attrs as attr (attr.name)}
			<div class="stat-name">{attr.name}</div>
			<div class="stat-value" class:positive={attr.value > 0} class:negative={attr.value < 0}>
				{attr.value > 0 ? '+' : ''}{formatValue(attr.value)}
			</div>
		{/each}
	</div>
{:else}
	<div class="stat-empty">{emptyText}</div>
{/if}

<script lang="ts">
import type { StatEntry } from './inventory-view.svelte';

interface Props {
	attrs: StatEntry[];
	emptyText?: string;
}

const { attrs, emptyText = 'No stats.' }: Props = $props();

const formatValue = (v: number) => (Number.isInteger(v) ? v.toString() : v.toFixed(1));
</script>

<style lang="scss">
.stat-grid {
	display: grid;
	grid-template-columns: 1fr auto;
	row-gap: 4px;
	column-gap: 12px;
}

.stat-name {
	font-size: 12px;
	color: rgba(240, 240, 240, 0.78);
}

.stat-value {
	font-family: var(--mono);
	font-size: 11.5px;
	letter-spacing: 0.3px;
	text-align: right;
	color: rgba(240, 240, 240, 0.7);

	&.positive {
		color: #bde0b4;
	}

	&.negative {
		color: #f0a094;
	}
}

.stat-empty {
	font-size: 12px;
	font-style: italic;
	color: rgba(240, 240, 240, 0.4);
	padding: 4px 0;
}
</style>
